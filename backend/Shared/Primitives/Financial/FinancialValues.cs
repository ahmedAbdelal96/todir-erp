using System.Globalization;

namespace Shared.Primitives.Financial;

/// <summary>Stable error raised when a shared financial value cannot be created or used.</summary>
public sealed class FinancialValueException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public enum FinancialRoundingModeValue { HalfUp, HalfEven, Down, Up }

/// <summary>Exact currency-scale rounding shared by finance policy owners and their consumers.</summary>
public static class FinancialRounding
{
    public static decimal Round(decimal amount, int scale, FinancialRoundingModeValue mode)
    {
        if (scale is < 0 or > 8) throw new FinancialValueException("finbase.rounding_scale_invalid", "Rounding scale must be between zero and eight.");
        try
        {
            var factor = scale switch { 0 => 1m, 1 => 10m, 2 => 100m, 3 => 1000m, 4 => 10000m, 5 => 100000m, 6 => 1000000m, 7 => 10000000m, 8 => 100000000m, _ => throw new FinancialValueException("finbase.rounding_scale_invalid", "Rounding scale must be between zero and eight.") };
            var shifted = checked(amount * factor);
            var rounded = mode switch
            {
                FinancialRoundingModeValue.HalfUp => decimal.Round(shifted, 0, MidpointRounding.AwayFromZero),
                FinancialRoundingModeValue.HalfEven => decimal.Round(shifted, 0, MidpointRounding.ToEven),
                FinancialRoundingModeValue.Down => decimal.Truncate(shifted),
                FinancialRoundingModeValue.Up => shifted >= 0m ? decimal.Ceiling(shifted) : decimal.Floor(shifted),
                _ => throw new FinancialValueException("finbase.rounding_policy_invalid", "The rounding mode is not supported.")
            };
            return rounded / factor;
        }
        catch (OverflowException) { throw new FinancialValueException("finbase.amount_overflow", "The financial amount exceeds the supported range."); }
    }
}

/// <summary>Currency-code normalization shared by financial domain consumers.</summary>
public static class CurrencyCode
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new FinancialValueException("finbase.currency_invalid", "Currency code is required.");
        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(character => character is < 'A' or > 'Z')) throw new FinancialValueException("finbase.currency_invalid", "Currency code must be a three-letter ISO code.");
        return code;
    }
}

/// <summary>Exact amount paired with its currency; conversion always requires an explicit context.</summary>
public readonly record struct Money
{
    private Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Create(string amount, string currency)
    {
        if (!decimal.TryParse(amount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new FinancialValueException("finbase.invalid_decimal", "Money amount must be an invariant decimal string.");
        return new(value, CurrencyCode.Normalize(currency));
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        try { return new Money(checked(Amount + other.Amount), Currency); }
        catch (OverflowException) { throw new FinancialValueException("finbase.amount_overflow", "The financial amount exceeds the supported range."); }
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        try { return new Money(checked(Amount - other.Amount), Currency); }
        catch (OverflowException) { throw new FinancialValueException("finbase.amount_overflow", "The financial amount exceeds the supported range."); }
    }

    public Money Multiply(decimal factor)
    {
        try { return new Money(checked(Amount * factor), Currency); }
        catch (OverflowException) { throw new FinancialValueException("finbase.amount_overflow", "The financial amount exceeds the supported range."); }
    }

    public Money Negate()
    {
        try { return new Money(checked(-Amount), Currency); }
        catch (OverflowException) { throw new FinancialValueException("finbase.amount_overflow", "The financial amount exceeds the supported range."); }
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency is null || other.Currency is null) throw new FinancialValueException("finbase.currency_invalid", "A money value must have a currency.");
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal)) throw new FinancialValueException("finbase.currency_mismatch", "Money values must use the same currency.");
    }
}

/// <summary>High-precision per-unit amount; it is distinct from a payable Money amount.</summary>
public readonly record struct UnitPrice
{
    private UnitPrice(decimal amount, string currency) { Amount = amount; Currency = currency; }
    public decimal Amount { get; }
    public string Currency { get; }

    public static UnitPrice Create(string amount, string currency)
    {
        if (!decimal.TryParse(amount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new FinancialValueException("finbase.invalid_decimal", "Unit price must be an invariant decimal string.");
        return new(value, CurrencyCode.Normalize(currency));
    }
}

/// <summary>Percentage represented canonically as a ratio in the inclusive range zero to one.</summary>
public readonly record struct Percentage
{
    private Percentage(decimal ratio) => Ratio = ratio;
    public decimal Ratio { get; }
    public static Percentage FromRatio(decimal ratio) => ratio is >= 0m and <= 1m ? new(ratio) : throw new FinancialValueException("finbase.percentage_invalid", "A percentage ratio must be between zero and one.");
    public static Percentage FromPercent(decimal percent) => percent is >= 0m and <= 100m ? new(percent / 100m) : throw new FinancialValueException("finbase.percentage_invalid", "A percentage value must be between zero and one hundred.");
}
