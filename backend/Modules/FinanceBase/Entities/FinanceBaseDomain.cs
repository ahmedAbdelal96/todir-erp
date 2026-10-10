using System.Globalization;

namespace Modules.FinanceBase.Entities;

public sealed class FinanceBaseDomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Create(string amount, string currency)
    {
        if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) throw new FinanceBaseDomainException("finbase.money_invalid", "Money amount must be a decimal string.");
        var code = CurrencyCode.Normalize(currency);
        return new(value, code);
    }

    public Money Add(Money other)
    {
        if (!Currency.Equals(other.Currency, StringComparison.Ordinal)) throw new FinanceBaseDomainException("finbase.money_currency_mismatch", "Money values must use the same currency.");
        return this with { Amount = Amount + other.Amount };
    }
}

public static class CurrencyCode
{
    public static string Normalize(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(character => character is < 'A' or > 'Z')) throw new FinanceBaseDomainException("finbase.currency_invalid", "Currency code must be a three-letter ISO code.");
        return code;
    }
}

public enum RoundingMode { HalfUp, HalfEven, Down, Up }
public enum RoundingContext { MoneySettlement, LineNetAmount, DocumentSubtotal, DocumentTotal, PaymentAllocation }

public static class MoneyRounding
{
    public static decimal Round(decimal amount, int scale, RoundingMode mode)
    {
        if (scale is < 0 or > 8) throw new FinanceBaseDomainException("finbase.rounding_scale_invalid", "Rounding scale must be between 0 and 8.");
        var factor = Pow10(scale);
        var shifted = amount * factor;
        var rounded = mode switch
        {
            RoundingMode.HalfUp => decimal.Round(shifted, 0, MidpointRounding.AwayFromZero),
            RoundingMode.HalfEven => decimal.Round(shifted, 0, MidpointRounding.ToEven),
            RoundingMode.Down => decimal.Truncate(shifted),
            RoundingMode.Up => shifted >= 0 ? decimal.Ceiling(shifted) : decimal.Floor(shifted),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        return rounded / factor;
    }

    private static decimal Pow10(int scale) => scale switch { 0 => 1m, 1 => 10m, 2 => 100m, 3 => 1000m, 4 => 10000m, 5 => 100000m, 6 => 1000000m, 7 => 10000000m, 8 => 100000000m, _ => throw new ArgumentOutOfRangeException(nameof(scale)) };
}

public enum ExchangeRatePurpose { Accounting, Commercial, Contractual, BankActual, Settlement, Tax }
public enum ExchangeRateStatus { Draft, Approved, Superseded, Rejected }
public sealed record ExchangeRateCandidate(string FromCurrency, string ToCurrency, decimal Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, ExchangeRateStatus Status);

public static class ExchangeRateResolver
{
    public static ExchangeRateCandidate Resolve(IEnumerable<ExchangeRateCandidate> candidates, string fromCurrency, string toCurrency, ExchangeRatePurpose purpose, DateOnly businessDate, int maxAgeDays)
    {
        var from = CurrencyCode.Normalize(fromCurrency); var to = CurrencyCode.Normalize(toCurrency);
        if (from == to) return new(from, to, 1m, purpose, businessDate, ExchangeRateStatus.Approved);
        var result = candidates.Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Purpose == purpose && x.Status == ExchangeRateStatus.Approved && x.EffectiveDate <= businessDate).OrderByDescending(x => x.EffectiveDate).FirstOrDefault();
        if (result is null) throw new FinanceBaseDomainException("finbase.exchange_rate_missing", "No approved exchange rate is available for the requested date.");
        if (businessDate.DayNumber - result.EffectiveDate.DayNumber > maxAgeDays) throw new FinanceBaseDomainException("finbase.exchange_rate_stale", "The latest approved exchange rate is stale.");
        return result;
    }
}

public enum PaymentAllocation { Percentage, Fixed, Balance }
public enum DueDateBasis { InvoiceDate, DeliveryDate, MonthEnd }
public sealed record PaymentTermRuleInput(int Sequence, PaymentAllocation Allocation, decimal? Percentage, decimal? FixedAmount, DueDateBasis DueDateBasis, int Days, int? DayOfMonth, int MonthOffset);
public sealed record PaymentScheduleLine(int Sequence, decimal Amount, DateOnly DueDate, PaymentAllocation Allocation);

public static class PaymentScheduleCalculator
{
    public static IReadOnlyList<PaymentScheduleLine> Calculate(decimal total, DateOnly basisDate, IReadOnlyList<PaymentTermRuleInput> rules, int scale)
    {
        if (rules.Count == 0 || rules.GroupBy(x => x.Sequence).Any(x => x.Count() != 1)) throw new FinanceBaseDomainException("finbase.payment_term_rules_invalid", "Payment term rules must have unique sequence values.");
        if (rules.Count(x => x.Allocation == PaymentAllocation.Balance) > 1) throw new FinanceBaseDomainException("finbase.payment_term_balance_invalid", "A payment term may contain at most one balance rule.");
        if (rules.Where(x => x.Allocation == PaymentAllocation.Percentage).Sum(x => x.Percentage ?? -1m) > 100m) throw new FinanceBaseDomainException("finbase.payment_term_percentage_invalid", "Payment percentages cannot exceed one hundred.");
        var ordered = rules.OrderBy(x => x.Sequence).ToArray(); var remaining = total; var result = new List<PaymentScheduleLine>();
        foreach (var rule in ordered)
        {
            var amount = rule.Allocation switch { PaymentAllocation.Percentage => MoneyRounding.Round(total * (rule.Percentage ?? throw new FinanceBaseDomainException("finbase.payment_term_percentage_required", "Percentage is required.")) / 100m, scale, RoundingMode.HalfUp), PaymentAllocation.Fixed => rule.FixedAmount ?? throw new FinanceBaseDomainException("finbase.payment_term_fixed_required", "Fixed amount is required."), PaymentAllocation.Balance => remaining, _ => throw new ArgumentOutOfRangeException(nameof(rules), "Payment allocation is not supported.") };
            if (rule.Allocation != PaymentAllocation.Balance && Math.Abs(amount) > Math.Abs(remaining) && Math.Sign(amount) == Math.Sign(remaining)) throw new FinanceBaseDomainException("finbase.payment_term_overshoot", "Payment term rules cannot overshoot the document total.");
            if (rule.Allocation != PaymentAllocation.Balance) remaining -= amount;
            else remaining = 0m;
            result.Add(new(rule.Sequence, amount, DueDate(basisDate, rule), rule.Allocation));
        }
        if (remaining != 0m) throw new FinanceBaseDomainException("finbase.payment_term_total_mismatch", "Payment term rules must allocate the full document total.");
        return result;
    }

    private static DateOnly DueDate(DateOnly basis, PaymentTermRuleInput rule) => rule.DueDateBasis switch
    {
        DueDateBasis.InvoiceDate => basis.AddDays(rule.Days),
        DueDateBasis.DeliveryDate => basis.AddDays(rule.Days),
        DueDateBasis.MonthEnd => new DateOnly(basis.Year, basis.Month, DateTime.DaysInMonth(basis.Year, basis.Month)).AddMonths(rule.MonthOffset).AddDays(rule.Days),
        _ => basis
    };
}
