using FluentAssertions;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Domain.Catalog;
using Inkoova.Academy.Domain.ValueObjects;
using Inkoova.Academy.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// The acceptance criterion from T-04: a duplicated Stripe event must not change state a
/// second time. The processor is driven directly with a normalised event so the test does
/// not depend on Stripe signatures, which are exercised separately by the CLI.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WebhookIdempotencyTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private AcademyApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        DapperTypeHandlers.Register();
        _factory = new AcademyApiFactory(fixture.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_repeated_checkout_event_grants_access_exactly_once()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var (userId, productSlug, productId) = await SeedUserAndProductAsync(services);

        var evt = new PaymentEvent
        {
            EventId = $"evt_{Guid.CreateVersion7():N}",
            RawType = "checkout.session.completed",
            Kind = PaymentEventKind.CheckoutCompleted,
            OccurredAt = Now,
            CustomerId = "cus_test",
            PaymentIntentId = $"pi_{Guid.CreateVersion7():N}",
            AmountTotalCents = 9_900,
            AmountTaxCents = 1_719,
            Currency = "eur",
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadataKeys.UserId] = userId.ToString(),
                [PaymentMetadataKeys.ProductSlug] = productSlug
            }
        };

        var processor = services.GetRequiredService<StripeWebhookProcessor>();

        (await processor.ProcessAsync(evt, Ct)).IsSuccess.Should().BeTrue();
        (await processor.ProcessAsync(evt, Ct)).IsSuccess.Should().BeTrue();

        var purchases = services.GetRequiredService<IPurchaseRepository>();
        var recorded = await purchases.GetForUserAsync(userId, Ct);
        recorded.Should().HaveCount(1, "el segundo evento es una repetición y no debe crear otra compra");

        var accessPolicy = services.GetRequiredService<IAccessPolicy>();
        (await accessPolicy.CanAccessProductAsync(userId, productId, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Two_different_events_for_the_same_payment_intent_still_produce_one_purchase()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var (userId, productSlug, _) = await SeedUserAndProductAsync(services);
        var paymentIntentId = $"pi_{Guid.CreateVersion7():N}";

        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadataKeys.UserId] = userId.ToString(),
            [PaymentMetadataKeys.ProductSlug] = productSlug
        };

        var checkout = new PaymentEvent
        {
            EventId = $"evt_{Guid.CreateVersion7():N}",
            RawType = "checkout.session.completed",
            Kind = PaymentEventKind.CheckoutCompleted,
            OccurredAt = Now,
            PaymentIntentId = paymentIntentId,
            AmountTotalCents = 9_900,
            Currency = "eur",
            Metadata = metadata
        };

        // Stripe sends both for the same purchase; the payment intent keeps them from
        // producing two rows.
        var intent = checkout with
        {
            EventId = $"evt_{Guid.CreateVersion7():N}",
            RawType = "payment_intent.succeeded",
            Kind = PaymentEventKind.PaymentIntentSucceeded
        };

        var processor = services.GetRequiredService<StripeWebhookProcessor>();
        await processor.ProcessAsync(checkout, Ct);
        await processor.ProcessAsync(intent, Ct);

        var purchases = services.GetRequiredService<IPurchaseRepository>();
        (await purchases.GetForUserAsync(userId, Ct)).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_refund_revokes_the_access_the_purchase_granted()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var (userId, productSlug, productId) = await SeedUserAndProductAsync(services);
        var paymentIntentId = $"pi_{Guid.CreateVersion7():N}";

        var processor = services.GetRequiredService<StripeWebhookProcessor>();
        var accessPolicy = services.GetRequiredService<IAccessPolicy>();

        await processor.ProcessAsync(new PaymentEvent
        {
            EventId = $"evt_{Guid.CreateVersion7():N}",
            RawType = "checkout.session.completed",
            Kind = PaymentEventKind.CheckoutCompleted,
            OccurredAt = Now,
            PaymentIntentId = paymentIntentId,
            AmountTotalCents = 9_900,
            Currency = "eur",
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadataKeys.UserId] = userId.ToString(),
                [PaymentMetadataKeys.ProductSlug] = productSlug
            }
        }, Ct);

        (await accessPolicy.CanAccessProductAsync(userId, productId, Ct)).Should().BeTrue();

        await processor.ProcessAsync(new PaymentEvent
        {
            EventId = $"evt_{Guid.CreateVersion7():N}",
            RawType = "charge.refunded",
            Kind = PaymentEventKind.ChargeRefunded,
            OccurredAt = Now.AddDays(3),
            PaymentIntentId = paymentIntentId,
            AmountTotalCents = 9_900,
            AmountRefundedCents = 9_900,
            Currency = "eur"
        }, Ct);

        (await accessPolicy.CanAccessProductAsync(userId, productId, Ct)).Should().BeFalse();
    }

    private static async Task<(Guid UserId, string ProductSlug, Guid ProductId)> SeedUserAndProductAsync(
        IServiceProvider services)
    {
        // Los ÚLTIMOS caracteres, no los primeros: un GUID v7 empieza por marca de tiempo,
        // así que dos tests que corren en el mismo instante comparten prefijo y chocarían
        // contra el índice único del slug. La cola es la parte aleatoria.
        var suffix = Guid.CreateVersion7().ToString("N")[^12..];
        var slug = $"producto-webhook-{suffix}";

        var products = services.GetRequiredService<IProductRepository>();
        var product = Product.Create(
            Guid.CreateVersion7(), ProductType.Course, Slug.Create(slug).Value,
            "Producto de prueba", Money.Euros(9_900), Now).Value;

        await products.UpsertAsync(product, Ct);

        // app_user has a foreign key to identity_user, so both rows are needed.
        var identity = services.GetRequiredService<Infrastructure.Identity.IdentityStore>();
        var userId = Guid.CreateVersion7();
        var email = $"webhook-{suffix}@test.local";

        await identity.TryCreateAsync(
            new Infrastructure.Identity.IdentityUserRow(
                userId, email.ToUpperInvariant(), email, true, "hash", "stamp", "concurrency",
                null, true, 0, Now),
            Ct);

        var users = services.GetRequiredService<IUserRepository>();
        await users.UpsertAsync(
            Domain.Users.User.Create(userId, email, "Comprador de prueba", Now).Value, Ct);

        return (userId, slug, product.Id);
    }
}
