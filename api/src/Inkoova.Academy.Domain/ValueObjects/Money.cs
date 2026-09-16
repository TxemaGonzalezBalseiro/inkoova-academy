using Inkoova.Academy.Domain.Common;

namespace Inkoova.Academy.Domain.ValueObjects;

/// <summary>
/// Amount in minor units (cents), never a decimal. Stripe works in minor units and so
/// does the affiliate settlement, so keeping cents end to end removes rounding drift.
/// </summary>
public readonly record struct Money
{
    public long AmountInCents { get; }
    public string Currency { get; }

    private Money(long amountInCents, string currency)
    {
        AmountInCents = amountInCents;
        Currency = currency;
    }

    public static Result<Money, Error> Create(long amountInCents, string currency)
    {
        if (amountInCents < 0)
        {
            return Error.Validation("money.negative", "El importe no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return Error.Validation("money.currency_invalid", "La moneda debe ser un código ISO-4217 de 3 letras.");
        }

        return new Money(amountInCents, currency.ToUpperInvariant());
    }

    public static Money Euros(long amountInCents) => new(amountInCents, "EUR");

    public static Money Zero(string currency) => new(0, currency.ToUpperInvariant());

    public Result<Money, Error> Add(Money other) =>
        SameCurrency(other, out var error)
            ? new Money(AmountInCents + other.AmountInCents, Currency)
            : error!;

    public Result<Money, Error> Subtract(Money other)
    {
        if (!SameCurrency(other, out var error))
        {
            return error!;
        }

        var result = AmountInCents - other.AmountInCents;
        return result < 0
            ? Error.Validation("money.negative", "La resta produce un importe negativo.")
            : new Money(result, Currency);
    }

    /// <summary>
    /// Percentage of the amount, rounded half away from zero, used for affiliate
    /// commissions. Deterministic so that a settlement can be recomputed byte-identical.
    /// </summary>
    public Money Percentage(decimal percent)
    {
        var raw = AmountInCents * percent / 100m;
        var rounded = Math.Round(raw, MidpointRounding.AwayFromZero);
        return new Money((long)rounded, Currency);
    }

    private bool SameCurrency(Money other, out Error? error)
    {
        if (Currency != other.Currency)
        {
            error = Error.Validation("money.currency_mismatch", $"No se pueden operar {Currency} con {other.Currency}.");
            return false;
        }

        error = null;
        return true;
    }

    public decimal ToDecimal() => AmountInCents / 100m;

    public override string ToString() => $"{ToDecimal():0.00} {Currency}";
}
