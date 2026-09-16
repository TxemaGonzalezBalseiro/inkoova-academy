using FluentAssertions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

public class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);

    [Fact]
    public void A_subscription_cannot_start_with_a_period_already_over()
    {
        var result = Subscription.Start(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "sub_123", Now.AddDays(-1), Now);

        result.Error.Code.Should().Be("subscription.period_in_past");
    }

    [Fact]
    public void Cancelling_keeps_access_until_the_end_of_the_paid_period()
    {
        var subscription = Start(periodEnd: Now.AddDays(20));

        subscription.ScheduleCancellation(Now);

        subscription.CancelAtPeriodEnd.Should().BeTrue();
        subscription.GrantsAccessAt(Now.AddDays(19), Grace).Should().BeTrue();
        subscription.GrantsAccessAt(Now.AddDays(21), Grace).Should().BeFalse();
    }

    [Fact]
    public void A_failed_payment_extends_access_by_the_grace_period_and_no_further()
    {
        var subscription = Start(periodEnd: Now.AddDays(1));

        subscription.MarkPastDue(Now);

        subscription.GrantsAccessAt(Now.AddDays(2), Grace).Should().BeTrue();
        subscription.GrantsAccessAt(Now.AddDays(4), Grace).Should().BeFalse();
    }

    [Fact]
    public void Recovering_from_past_due_clears_the_grace_window()
    {
        var subscription = Start(periodEnd: Now.AddDays(1));
        subscription.MarkPastDue(Now);

        subscription.Renew(Now.AddDays(31), Now.AddDays(2));

        subscription.PastDueSince.Should().BeNull();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.GrantsAccessAt(Now.AddDays(30), Grace).Should().BeTrue();
    }

    [Fact]
    public void A_cancelled_subscription_grants_no_access()
    {
        var subscription = Start(periodEnd: Now.AddDays(20));

        subscription.Cancel(Now);

        subscription.GrantsAccessAt(Now, Grace).Should().BeFalse();
    }

    private static Subscription Start(DateTimeOffset periodEnd) => Subscription.Start(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "sub_123", periodEnd, Now).Value;
}

public class EntitlementTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_plan_derived_entitlement_must_carry_an_expiry()
    {
        var result = Entitlement.Grant(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EntitlementSource.PlanIncluded, Now, validUntil: null);

        result.Error.Code.Should().Be("entitlement.plan_without_expiry");
    }

    [Fact]
    public void A_manual_grant_must_be_justified()
    {
        var result = Entitlement.Grant(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EntitlementSource.Manual, Now, validUntil: null, note: null);

        result.Error.Code.Should().Be("entitlement.manual_without_note");
    }

    [Fact]
    public void A_purchase_entitlement_never_expires()
    {
        var entitlement = Entitlement.Grant(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EntitlementSource.Purchase, Now, validUntil: null).Value;

        entitlement.IsValidAt(Now.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public void Revoking_ends_access_immediately()
    {
        var entitlement = Entitlement.Grant(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EntitlementSource.Purchase, Now, null).Value;

        entitlement.Revoke(Now, "reembolso").IsSuccess.Should().BeTrue();

        entitlement.IsValidAt(Now).Should().BeFalse();
        entitlement.Revoke(Now, "otra vez").Error.Code.Should().Be("entitlement.already_revoked");
    }

    [Fact]
    public void Extending_only_moves_the_expiry_forward()
    {
        var entitlement = Entitlement.Grant(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            EntitlementSource.PlanIncluded, Now, Now.AddDays(30)).Value;

        entitlement.ExtendTo(Now.AddDays(10));
        entitlement.ValidUntil.Should().Be(Now.AddDays(30));

        entitlement.ExtendTo(Now.AddDays(60));
        entitlement.ValidUntil.Should().Be(Now.AddDays(60));
    }
}

public class PurchaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_commission_base_excludes_vat_and_the_processor_fee()
    {
        // 129,00 € gross, 22,39 € VAT, 2,17 € Stripe fee.
        var purchase = Record(gross: 12_900, tax: 2_239, fee: 217);

        purchase.NetBase().AmountInCents.Should().Be(12_900 - 2_239 - 217);
    }

    [Fact]
    public void A_full_refund_zeroes_the_base_and_stops_the_commission()
    {
        var purchase = Record(gross: 12_900, tax: 2_239, fee: 217);

        purchase.Refund(Money.Euros(12_900), Now).IsSuccess.Should().BeTrue();

        purchase.Status.Should().Be(PurchaseStatus.Refunded);
        purchase.NetBase().AmountInCents.Should().Be(0);
        purchase.IsCommissionable.Should().BeFalse();
    }

    [Fact]
    public void A_partial_refund_reduces_the_base_proportionally_to_what_was_returned()
    {
        var purchase = Record(gross: 12_900, tax: 2_239, fee: 217);

        purchase.Refund(Money.Euros(5_000), Now);

        purchase.Status.Should().Be(PurchaseStatus.PartiallyRefunded);
        purchase.NetBase().AmountInCents.Should().Be(12_900 - 2_239 - 217 - 5_000);
    }

    [Fact]
    public void Refunding_more_than_was_charged_is_refused()
    {
        var purchase = Record(gross: 1_000, tax: 0, fee: 0);

        purchase.Refund(Money.Euros(1_500), Now).Error.Code.Should().Be("purchase.refund_exceeds_gross");
    }

    [Fact]
    public void A_chargeback_makes_the_purchase_non_commissionable()
    {
        var purchase = Record(gross: 12_900, tax: 2_239, fee: 217);

        purchase.MarkChargedBack(Now);

        purchase.IsCommissionable.Should().BeFalse();
    }

    [Fact]
    public void Deductions_larger_than_the_amount_charged_are_refused_at_creation()
    {
        var result = Purchase.Record(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Euros(1_000), Money.Euros(800), Money.Euros(300),
            "pi_1", null, Now);

        result.Error.Code.Should().Be("purchase.deductions_exceed_gross");
    }

    private static Purchase Record(long gross, long tax, long fee) => Purchase.Record(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        Money.Euros(gross), Money.Euros(tax), Money.Euros(fee),
        "pi_test", null, Now).Value;
}

public class MoneyTests
{
    [Fact]
    public void Percentages_round_half_away_from_zero_so_settlements_are_reproducible()
    {
        // 20 % of 12,34 € is 2,468 €, which must land on 2,47 € every time.
        Money.Euros(1_234).Percentage(20).AmountInCents.Should().Be(247);
    }

    [Fact]
    public void Operating_across_currencies_is_refused()
    {
        var euros = Money.Euros(1_000);
        var dollars = Money.Create(1_000, "USD").Value;

        euros.Add(dollars).Error.Code.Should().Be("money.currency_mismatch");
    }

    [Fact]
    public void A_subtraction_that_would_go_negative_is_refused()
    {
        Money.Euros(500).Subtract(Money.Euros(800)).Error.Code.Should().Be("money.negative");
    }

    [Fact]
    public void A_currency_that_is_not_three_letters_is_refused()
    {
        Money.Create(100, "EURO").Error.Code.Should().Be("money.currency_invalid");
    }
}
