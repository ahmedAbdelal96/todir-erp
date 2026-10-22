using System.Globalization;
using Shared.Primitives.Financial;

namespace Modules.FinanceBase.Entities;

public sealed class FinanceBaseDomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public enum RoundingMode { HalfUp, HalfEven, Down, Up }
public enum RoundingContext { MoneySettlement, LineNetAmount, DocumentSubtotal, DocumentTotal, FXBaseAmount, PaymentScheduleLine, PaymentAllocation, CashRounding }
public enum LineDocumentRoundingStrategy { RoundEachLineThenSum, SumExactLinesThenRound }
public sealed record RoundingPolicyContextRule(RoundingContext Context, RoundingMode Mode);

public sealed record RoundingPolicySnapshot
{
    private RoundingPolicySnapshot(Guid id, long version, int scale, LineDocumentRoundingStrategy strategy, IReadOnlyDictionary<RoundingContext, RoundingMode> contextModes) { Id = id; Version = version; Scale = scale; LineDocumentStrategy = strategy; ContextModes = contextModes; }
    public Guid Id { get; }
    public long Version { get; }
    public int Scale { get; }
    public LineDocumentRoundingStrategy LineDocumentStrategy { get; }
    public IReadOnlyDictionary<RoundingContext, RoundingMode> ContextModes { get; }
    public RoundingMode ModeFor(RoundingContext context) => ContextModes.TryGetValue(context, out var mode) ? mode : throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "The rounding policy has no rule for the requested calculation context.");
    public static RoundingPolicySnapshot Create(Guid id, long version, RoundingMode mode, int scale)
    {
        return Create(id, version, scale, LineDocumentRoundingStrategy.RoundEachLineThenSum, Enum.GetValues<RoundingContext>().Select(context => new RoundingPolicyContextRule(context, mode)));
    }
    public static RoundingPolicySnapshot Create(Guid id, long version, int scale, LineDocumentRoundingStrategy strategy, IEnumerable<RoundingPolicyContextRule> rules)
    {
        var materialized = rules.ToArray();
        var expected = Enum.GetValues<RoundingContext>();
        if (id == Guid.Empty || version <= 0 || !Enum.IsDefined(strategy) || scale is < 0 or > 8 || materialized.Any(rule => !Enum.IsDefined(rule.Context) || !Enum.IsDefined(rule.Mode)) || materialized.Select(rule => rule.Context).Distinct().Count() != expected.Length || expected.Except(materialized.Select(rule => rule.Context)).Any())
            throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "The rounding policy snapshot is invalid.");
        return new(id, version, scale, strategy, new System.Collections.ObjectModel.ReadOnlyDictionary<RoundingContext, RoundingMode>(materialized.ToDictionary(rule => rule.Context, rule => rule.Mode)));
    }
}

public static class MoneyRounding
{
    public static decimal Round(decimal amount, int scale, RoundingMode mode)
    {
        try { return FinancialRounding.Round(amount, scale, Enum.Parse<FinancialRoundingModeValue>(mode.ToString(), true)); }
        catch (FinancialValueException exception) { throw new FinanceBaseDomainException(exception.Code, exception.Message); }
    }
}

public enum ExchangeRatePurpose { Accounting, Commercial, Contractual, BankActual, Settlement, Tax }
public enum ExchangeRateStatus { Draft, Approved, Superseded, Rejected }
public sealed record ExchangeRateCandidate(string FromCurrency, string ToCurrency, decimal Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, ExchangeRateStatus Status, Guid RateId = default, long Version = 1, string Source = "Manual", bool ManualOverride = false);

public sealed record ExchangeRateSnapshot
{
    private ExchangeRateSnapshot(Guid tenantId, Guid legalEntityId, Guid? approvedRateId, string fromCurrency, string toCurrency, decimal rate, ExchangeRatePurpose purpose, DateOnly effectiveDate, string source, long version, bool manualOverride, DateTimeOffset capturedAt)
    {
        TenantId = tenantId; LegalEntityId = legalEntityId; ApprovedRateId = approvedRateId; FromCurrency = fromCurrency; ToCurrency = toCurrency; Rate = rate; Purpose = purpose; EffectiveDate = effectiveDate; Source = source; Version = version; ManualOverride = manualOverride; CapturedAt = capturedAt;
    }

    public Guid TenantId { get; }
    public Guid LegalEntityId { get; }
    public Guid? ApprovedRateId { get; }
    public string FromCurrency { get; }
    public string ToCurrency { get; }
    public decimal Rate { get; }
    public ExchangeRatePurpose Purpose { get; }
    public DateOnly EffectiveDate { get; }
    public string Source { get; }
    public long Version { get; }
    public bool ManualOverride { get; }
    public DateTimeOffset CapturedAt { get; }

    public static ExchangeRateSnapshot Create(Guid tenantId, Guid legalEntityId, Guid? approvedRateId, string fromCurrency, string toCurrency, decimal rate, ExchangeRatePurpose purpose, DateOnly effectiveDate, string source, long version, bool manualOverride, DateTimeOffset capturedAt)
    {
        var from = CurrencyCode.Normalize(fromCurrency); var to = CurrencyCode.Normalize(toCurrency);
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || approvedRateId is { } rateId && rateId == Guid.Empty || from == to || rate <= 0m || !Enum.IsDefined(purpose) || string.IsNullOrWhiteSpace(source) || version <= 0 || capturedAt == default)
            throw new FinanceBaseDomainException("finbase.rate_snapshot_invalid", "Exchange-rate snapshot context is incomplete or invalid.");
        return new(tenantId, legalEntityId, approvedRateId, from, to, rate, purpose, effectiveDate, source.Trim(), version, manualOverride, capturedAt);
    }

    public static ExchangeRateSnapshot FromApprovedRate(Guid tenantId, Guid legalEntityId, ExchangeRateCandidate rate, DateTimeOffset capturedAt)
    {
        if (rate.Status != ExchangeRateStatus.Approved || rate.RateId == Guid.Empty)
            throw new FinanceBaseDomainException("finbase.rate_not_approved", "Only a persisted approved exchange rate can be captured.");
        return Create(tenantId, legalEntityId, rate.RateId, rate.FromCurrency, rate.ToCurrency, rate.Rate, rate.Purpose, rate.EffectiveDate, rate.Source, rate.Version, rate.ManualOverride, capturedAt);
    }
}

public sealed record CurrencyConversion(Money Source, Money Target, decimal Rate, DateOnly? RateDate, ExchangeRateSnapshot? Snapshot, RoundingPolicySnapshot RoundingPolicy);

public static class CurrencyConverter
{
    public static CurrencyConversion Convert(Money source, string targetCurrency, ExchangeRateSnapshot? snapshot, RoundingPolicySnapshot roundingPolicy)
    {
        var target = CurrencyCode.Normalize(targetCurrency);
        if (string.Equals(source.Currency, target, StringComparison.Ordinal))
            return new(source, Money.Create(source.Amount.ToString(CultureInfo.InvariantCulture), target), 1m, null, null, roundingPolicy);
        if (snapshot is null || snapshot.FromCurrency != source.Currency || snapshot.ToCurrency != target)
            throw new FinanceBaseDomainException("finbase.rate_missing", "An explicit matching exchange-rate snapshot is required.");
        decimal converted;
        try { converted = checked(source.Amount * snapshot.Rate); }
        catch (OverflowException) { throw new FinanceBaseDomainException("finbase.amount_overflow", "The converted amount exceeds the supported range."); }
        var rounded = MoneyRounding.Round(converted, roundingPolicy.Scale, roundingPolicy.ModeFor(RoundingContext.FXBaseAmount));
        return new(source, Money.Create(rounded.ToString(CultureInfo.InvariantCulture), target), snapshot.Rate, snapshot.EffectiveDate, snapshot, roundingPolicy);
    }
}

public static class ExchangeRateResolver
{
    public static ExchangeRateCandidate Resolve(IEnumerable<ExchangeRateCandidate> candidates, string fromCurrency, string toCurrency, ExchangeRatePurpose purpose, DateOnly businessDate, int maxAgeDays)
    {
        if (maxAgeDays < 0) throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "Rate staleness policy is invalid.");
        var from = CurrencyCode.Normalize(fromCurrency); var to = CurrencyCode.Normalize(toCurrency);
        if (from == to) return new(from, to, 1m, purpose, businessDate, ExchangeRateStatus.Approved);
        var eligible = candidates.Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Purpose == purpose && x.Status == ExchangeRateStatus.Approved && x.Rate > 0m && x.EffectiveDate <= businessDate).ToArray();
        var result = eligible.OrderByDescending(x => x.EffectiveDate).FirstOrDefault();
        if (result is null) throw new FinanceBaseDomainException("finbase.rate_missing", "No approved exchange rate is available for the requested date.");
        if (eligible.Count(x => x.EffectiveDate == result.EffectiveDate) > 1) throw new FinanceBaseDomainException("finbase.rate_not_approved", "Multiple approved rates exist for the same pair, purpose, and date.");
        if (businessDate.DayNumber - result.EffectiveDate.DayNumber > maxAgeDays) throw new FinanceBaseDomainException("finbase.rate_stale", "The latest approved exchange rate is stale.");
        return result;
    }
}

public enum PaymentAllocation { Percentage, Fixed, Balance }
public enum DueDateBasis { Immediate, DaysAfterDate, EndOfMonth, DaysAfterEndOfMonth, DayOfFollowingMonth }
public sealed record PaymentTermRuleInput(int Sequence, PaymentAllocation Allocation, decimal? Percentage, decimal? FixedAmount, DueDateBasis DueDateBasis, int Days, int? DayOfMonth, int MonthOffset);
public sealed record PaymentScheduleLine(int Sequence, decimal Amount, DateOnly DueDate, PaymentAllocation Allocation);

public static class PaymentScheduleCalculator
{
    public static IReadOnlyList<PaymentScheduleLine> Calculate(decimal total, DateOnly basisDate, IReadOnlyList<PaymentTermRuleInput> rules, int scale)
        => Calculate(total, basisDate, rules, scale, RoundingMode.HalfUp);

    public static IReadOnlyList<PaymentScheduleLine> Calculate(decimal total, DateOnly basisDate, IReadOnlyList<PaymentTermRuleInput> rules, RoundingPolicySnapshot policy)
        => Calculate(total, basisDate, rules, policy.Scale, policy.ModeFor(RoundingContext.PaymentAllocation));

    private static List<PaymentScheduleLine> Calculate(decimal total, DateOnly basisDate, IReadOnlyList<PaymentTermRuleInput> rules, int scale, RoundingMode allocationRoundingMode)
    {
        if (scale is < 0 or > 8) throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "Payment schedule scale is invalid.");
        if (rules.Count == 0 || rules.Any(x => x.Sequence <= 0) || rules.GroupBy(x => x.Sequence).Any(x => x.Count() != 1)) throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Payment term rules must have positive, unique sequence values.");
        if (rules.Count(x => x.Allocation == PaymentAllocation.Balance) > 1) throw new FinanceBaseDomainException("finbase.payment_term_balance_invalid", "A payment term may contain at most one balance rule.");
        var percentageRules = rules.Where(x => x.Allocation == PaymentAllocation.Percentage).ToArray();
        if (percentageRules.Any(x => x.Percentage is null or < 0m or > 1m)) throw new FinanceBaseDomainException("finbase.percentage_invalid", "Payment percentages use a ratio from zero to one.");
        if (percentageRules.Sum(x => x.Percentage!.Value) > 1m) throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Payment percentages cannot exceed the document total.");
        foreach (var rule in rules) ValidateRuleShape(rule);
        var ordered = rules.OrderBy(x => x.Sequence).ToArray(); var remaining = total; var result = new List<PaymentScheduleLine>();
        if (ordered.Any(x => x.Allocation == PaymentAllocation.Balance) && ordered[^1].Allocation != PaymentAllocation.Balance) throw new FinanceBaseDomainException("finbase.payment_term_invalid", "The Balance rule must be the final schedule line.");
        foreach (var rule in ordered)
        {
            try
            {
                var amount = rule.Allocation switch { PaymentAllocation.Percentage => MoneyRounding.Round(checked(total * rule.Percentage!.Value), scale, allocationRoundingMode), PaymentAllocation.Fixed => rule.FixedAmount ?? throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Fixed amount is required."), PaymentAllocation.Balance => remaining, _ => throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Payment allocation is not supported.") };
                var exceedsRemainder = total switch { > 0m => amount < 0m || amount > remaining, < 0m => amount > 0m || amount < remaining, _ => amount != 0m };
                if (rule.Allocation != PaymentAllocation.Balance && exceedsRemainder) throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Payment term lines cannot exceed or reverse the document total.");
                if (rule.Allocation != PaymentAllocation.Balance) remaining = checked(remaining - amount);
                else remaining = 0m;
                result.Add(new(rule.Sequence, amount, DueDate(basisDate, rule), rule.Allocation));
            }
            catch (OverflowException) { throw new FinanceBaseDomainException("finbase.amount_overflow", "The payment schedule exceeds the supported amount range."); }
        }
        if (remaining != 0m || result.Sum(x => x.Amount) != total) throw new FinanceBaseDomainException("finbase.payment_schedule_mismatch", "Payment term rules must allocate the full document total.");
        return result;
    }

    private static DateOnly DueDate(DateOnly basis, PaymentTermRuleInput rule)
    {
        try
        {
            return rule.DueDateBasis switch
            {
                DueDateBasis.Immediate => basis,
                DueDateBasis.DaysAfterDate => basis.AddDays(rule.Days),
                DueDateBasis.EndOfMonth => EndOfMonth(basis.AddMonths(rule.MonthOffset)),
                DueDateBasis.DaysAfterEndOfMonth => EndOfMonth(basis.AddMonths(rule.MonthOffset)).AddDays(rule.Days),
                DueDateBasis.DayOfFollowingMonth when rule.DayOfMonth is >= 1 and <= 31 => AtDayOfMonth(basis.AddMonths(1 + rule.MonthOffset), rule.DayOfMonth.Value),
                _ => throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Due-date rule is invalid.")
            };
        }
        catch (ArgumentOutOfRangeException) { throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Calculated payment due date is outside the supported calendar."); }
    }

    private static void ValidateRuleShape(PaymentTermRuleInput rule)
    {
        var allocationIsValid = rule.Allocation switch
        {
            PaymentAllocation.Percentage => rule.Percentage is not null && rule.FixedAmount is null,
            PaymentAllocation.Fixed => rule.FixedAmount is not null && rule.Percentage is null,
            PaymentAllocation.Balance => rule.FixedAmount is null && rule.Percentage is null,
            _ => false
        };
        var dueDateIsValid = rule.DueDateBasis switch
        {
            DueDateBasis.Immediate => rule.Days == 0 && rule.DayOfMonth is null && rule.MonthOffset == 0,
            DueDateBasis.DaysAfterDate => rule.Days >= 0 && rule.DayOfMonth is null && rule.MonthOffset == 0,
            DueDateBasis.EndOfMonth => rule.Days == 0 && rule.DayOfMonth is null && rule.MonthOffset >= 0,
            DueDateBasis.DaysAfterEndOfMonth => rule.Days >= 0 && rule.DayOfMonth is null && rule.MonthOffset >= 0,
            DueDateBasis.DayOfFollowingMonth => rule.Days == 0 && rule.DayOfMonth is >= 1 and <= 31 && rule.MonthOffset >= 0,
            _ => false
        };
        if (!allocationIsValid || !dueDateIsValid) throw new FinanceBaseDomainException("finbase.payment_term_invalid", "Payment term allocation or due-date rule is invalid.");
    }

    private static DateOnly EndOfMonth(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
    private static DateOnly AtDayOfMonth(DateOnly date, int day) => new(date.Year, date.Month, Math.Min(day, DateTime.DaysInMonth(date.Year, date.Month)));
}
