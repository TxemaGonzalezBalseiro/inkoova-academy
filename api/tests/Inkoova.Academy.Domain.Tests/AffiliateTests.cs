using FluentAssertions;
using Inkoova.Academy.Domain.Affiliates;
using Inkoova.Academy.Domain.ValueObjects;

namespace Inkoova.Academy.Domain.Tests;

public class CommissionTests
{
    private static readonly DateTimeOffset PaidAt = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_commission_cannot_be_approved_before_the_withdrawal_window_closes()
    {
        var commission = Accrue();

        // Day 5 of the 14-day window (T-16 acceptance criteria).
        var early = commission.Approve(PaidAt.AddDays(5));

        early.IsFailure.Should().BeTrue();
        early.Error.Code.Should().Be("commission.window_open");
        commission.Status.Should().Be(CommissionStatus.Pending);
    }

    [Fact]
    public void A_commission_is_approved_once_the_window_has_closed()
    {
        var commission = Accrue();

        var onDay15 = commission.Approve(PaidAt.AddDays(15));

        onDay15.IsSuccess.Should().BeTrue();
        commission.Status.Should().Be(CommissionStatus.Approved);
        commission.IsPayable.Should().BeTrue();
    }

    [Fact]
    public void A_refund_inside_the_window_reverses_the_commission_for_good()
    {
        var commission = Accrue();

        commission.Reverse("reembolso").IsSuccess.Should().BeTrue();

        commission.Status.Should().Be(CommissionStatus.Reversed);
        commission.Approve(PaidAt.AddDays(15)).Error.Code.Should().Be("commission.reversed");
        commission.IsPayable.Should().BeFalse();
    }

    [Fact]
    public void Only_an_approved_commission_can_be_attached_to_a_payout()
    {
        var commission = Accrue();

        commission.AttachToPayout(Guid.CreateVersion7()).Error.Code.Should().Be("commission.not_approved");

        commission.Approve(PaidAt.AddDays(15));
        commission.AttachToPayout(Guid.CreateVersion7()).IsSuccess.Should().BeTrue();
        commission.Status.Should().Be(CommissionStatus.Paid);
        commission.IsPayable.Should().BeFalse();
    }

    [Fact]
    public void The_amount_is_the_configured_percentage_of_the_net_base()
    {
        var commission = Commission.Accrue(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Money.Euros(10_444), percent: 20, PaidAt).Value;

        commission.Amount.AmountInCents.Should().Be(2_089);
    }

    [Fact]
    public void A_percentage_outside_the_allowed_range_is_refused()
    {
        Commission.Accrue(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                Money.Euros(1_000), percent: 80, PaidAt)
            .Error.Code.Should().Be("commission.percent_out_of_range");
    }

    private static Commission Accrue() => Commission.Accrue(
        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        Money.Euros(10_444), percent: 20, PaidAt).Value;
}

public class AffiliateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_affiliate_without_tax_data_cannot_be_paid()
    {
        var affiliate = Create();
        affiliate.Activate();

        affiliate.CanBePaid.Should().BeFalse();

        affiliate.SetTaxData("12345678Z", "ES", "ES91 2100 0418 4502 0005 1332");

        affiliate.CanBePaid.Should().BeTrue();
    }

    [Fact]
    public void Self_billing_requires_a_recorded_written_agreement()
    {
        var affiliate = Create();

        affiliate.UseSelfBilling("  ").Error.Code.Should().Be("affiliate.self_billing_without_agreement");

        affiliate.UseSelfBilling("acuerdos/2026/txema-autofactura.pdf").IsSuccess.Should().BeTrue();
        affiliate.InvoicingMode.Should().Be(InvoicingMode.SelfBilling);
    }

    [Fact]
    public void A_suspended_affiliate_cannot_be_reactivated_without_being_reinstated_first()
    {
        var affiliate = Create();
        affiliate.Suspend();

        affiliate.Activate().Error.Code.Should().Be("affiliate.suspended");

        affiliate.Reinstate();
        affiliate.Activate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_public_code_is_normalised_to_uppercase()
    {
        var affiliate = Affiliate.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "txema20", 20, 12, Now).Value;

        affiliate.Code.Should().Be("TXEMA20");
    }

    [Fact]
    public void The_iban_is_stored_without_spaces_so_two_spellings_do_not_look_different()
    {
        var affiliate = Create();
        affiliate.SetTaxData("12345678Z", "es", "es91 2100 0418 4502 0005 1332");

        affiliate.Iban.Should().Be("ES9121000418450200051332");
        affiliate.CountryCode.Should().Be("ES");
    }

    private static Affiliate Create() => Affiliate.Create(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "TXEMA20", 20, 12, Now).Value;
}

public class PayoutTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly PeriodStart = new(2026, 8, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 8, 31);

    [Fact]
    public void A_settlement_is_reproducible_from_the_same_commissions()
    {
        var affiliate = PayableAffiliate();
        var commissions = ApprovedCommissions(affiliate.Id, 3_000, 2_500, 1_200);

        var first = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd, commissions, Money.Euros(0), Now).Value;

        var second = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd,
            ApprovedCommissions(affiliate.Id, 3_000, 2_500, 1_200), Money.Euros(0), Now).Value;

        second.Total.Should().Be(first.Total);
        first.Total.AmountInCents.Should().Be(6_700);
    }

    [Fact]
    public void A_balance_below_fifty_euros_does_not_reach_the_minimum_payout()
    {
        var affiliate = PayableAffiliate();
        var payout = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd,
            ApprovedCommissions(affiliate.Id, 2_000), Money.Euros(0), Now).Value;

        payout.ReachesMinimum.Should().BeFalse();

        payout.RegisterInvoice("F-2026-001");
        payout.MarkPaid(Now).Error.Code.Should().Be("payout.below_minimum");
    }

    [Fact]
    public void A_carry_over_from_a_previous_month_is_added_to_the_total()
    {
        var affiliate = PayableAffiliate();

        var payout = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd,
            ApprovedCommissions(affiliate.Id, 3_000), Money.Euros(2_500), Now).Value;

        payout.Total.AmountInCents.Should().Be(5_500);
        payout.ReachesMinimum.Should().BeTrue();
    }

    [Fact]
    public void An_affiliate_without_tax_data_cannot_be_settled()
    {
        var affiliate = Affiliate.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "SINDATOS", 20, 12, Now).Value;
        affiliate.Activate();

        var result = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd, [], Money.Euros(0), Now);

        result.Error.Code.Should().Be("payout.affiliate_not_payable");
    }

    [Fact]
    public void Commissions_of_another_affiliate_cannot_be_settled_here()
    {
        var affiliate = PayableAffiliate();
        var foreign = ApprovedCommissions(Guid.CreateVersion7(), 3_000);

        var result = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd, foreign, Money.Euros(0), Now);

        result.Error.Code.Should().Be("payout.commission_other_affiliate");
    }

    [Fact]
    public void A_payout_cannot_be_marked_paid_before_an_invoice_is_registered()
    {
        var affiliate = PayableAffiliate();
        var payout = Payout.Settle(
            Guid.CreateVersion7(), affiliate, PeriodStart, PeriodEnd,
            ApprovedCommissions(affiliate.Id, 8_000), Money.Euros(0), Now).Value;

        payout.MarkPaid(Now).Error.Code.Should().Be("payout.not_ready");

        payout.RegisterInvoice("F-2026-001").IsSuccess.Should().BeTrue();
        payout.MarkPaid(Now).IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Paid);
    }

    private static Affiliate PayableAffiliate()
    {
        var affiliate = Affiliate.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "TXEMA20", 20, 12, Now).Value;
        affiliate.Activate();
        affiliate.SetTaxData("12345678Z", "ES", "ES9121000418450200051332");
        return affiliate;
    }

    private static List<Commission> ApprovedCommissions(Guid affiliateId, params long[] amountsInCents)
    {
        var result = new List<Commission>();

        foreach (var amount in amountsInCents)
        {
            // The net base is derived so the 20 % commission lands exactly on the amount asked for.
            var commission = Commission.Accrue(
                Guid.CreateVersion7(), Guid.CreateVersion7(), affiliateId,
                Money.Euros(amount * 5), percent: 20, new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.Zero)).Value;

            commission.Approve(new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
            result.Add(commission);
        }

        return result;
    }
}

public class ReferralTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_click_extends_the_attribution_window_and_keeps_the_first_click_date()
    {
        var referral = Referral.Track(Guid.CreateVersion7(), "visitor-1", Guid.CreateVersion7(), Now).Value;

        referral.RegisterClick(Now.AddDays(10));

        referral.FirstClickAt.Should().Be(Now);
        referral.LastClickAt.Should().Be(Now.AddDays(10));
        referral.ExpiresAt.Should().Be(Now.AddDays(40));
    }

    [Fact]
    public void An_expired_referral_cannot_convert()
    {
        var referral = Referral.Track(Guid.CreateVersion7(), "visitor-1", Guid.CreateVersion7(), Now).Value;

        referral.Convert(Guid.CreateVersion7(), Now.AddDays(31)).Error.Code.Should().Be("referral.expired");
    }

    [Fact]
    public void A_referral_converts_only_once()
    {
        var referral = Referral.Track(Guid.CreateVersion7(), "visitor-1", Guid.CreateVersion7(), Now).Value;

        referral.Convert(Guid.CreateVersion7(), Now.AddDays(1)).IsSuccess.Should().BeTrue();
        referral.Convert(Guid.CreateVersion7(), Now.AddDays(2)).Error.Code.Should().Be("referral.already_converted");
    }
}

public class DiscountCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_code_stops_working_once_its_redemption_limit_is_reached()
    {
        var productId = Guid.CreateVersion7();
        var code = DiscountCode.CreatePercentage(
            Guid.CreateVersion7(), "LANZAMIENTO", 20, [productId], Now, null, maxRedemptions: 2, null).Value;

        code.Redeem(productId, Now).IsSuccess.Should().BeTrue();
        code.Redeem(productId, Now).IsSuccess.Should().BeTrue();
        code.Redeem(productId, Now).Error.Code.Should().Be("discount.exhausted");
    }

    [Fact]
    public void A_code_restricted_to_a_product_does_not_apply_to_another()
    {
        var allowed = Guid.CreateVersion7();
        var code = DiscountCode.CreatePercentage(
            Guid.CreateVersion7(), "PACKSEGUROS", 20, [allowed], Now, null, null, null).Value;

        code.Redeem(Guid.CreateVersion7(), Now).Error.Code.Should().Be("discount.product_not_applicable");
    }

    [Fact]
    public void A_code_outside_its_validity_window_is_refused()
    {
        var code = DiscountCode.CreatePercentage(
            Guid.CreateVersion7(), "NAVIDAD", 20, [], Now, Now.AddDays(10), null, null).Value;

        code.Redeem(Guid.CreateVersion7(), Now.AddDays(11)).Error.Code.Should().Be("discount.out_of_window");
    }

    [Fact]
    public void An_empty_product_list_means_the_code_applies_to_everything()
    {
        var code = DiscountCode.CreatePercentage(
            Guid.CreateVersion7(), "GLOBAL", 10, [], Now, null, null, null).Value;

        code.Redeem(Guid.CreateVersion7(), Now).IsSuccess.Should().BeTrue();
    }
}
