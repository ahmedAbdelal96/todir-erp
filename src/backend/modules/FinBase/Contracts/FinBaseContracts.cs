using Todir.Modules.FinBase.Domain;

namespace Todir.Modules.FinBase.Contracts;

public sealed record CurrencyDto(string Code, int NumericCode, string NameAr, string NameEn, int MinorUnit, string Status, string DatasetVersion);
public sealed record EntityCurrencyDto(Guid LegalEntityId, string CurrencyCode, bool Enabled, bool AllowSales, bool AllowPurchase, bool AllowPayment, bool AllowManualRate, string Status, long Version);
public sealed record EnableCurrencyRequest(bool AllowSales = true, bool AllowPurchase = true, bool AllowPayment = true, bool AllowManualRate = false);
public sealed record CreateExchangeRateRequest(Guid LegalEntityId, string FromCurrency, string ToCurrency, string Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string SourceType, string? ProviderReference);
public sealed record ConvertMoneyRequest(Guid LegalEntityId, string Amount, string FromCurrency, string ToCurrency, ExchangeRatePurpose Purpose, DateOnly BusinessDate);
public sealed record ConvertedMoneyDto(string SourceAmount, string SourceCurrency, string TargetAmount, string TargetCurrency, string Rate, DateOnly RateDate, ExchangeRatePurpose Purpose);
public sealed record ExchangeRateDto(Guid Id, Guid LegalEntityId, string FromCurrency, string ToCurrency, string Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string SourceType, string? ProviderReference, ExchangeRateStatus Status, long Version);
public sealed record RoundingPolicyDto(Guid Id, string Code, string NameAr, string NameEn, RoundingMode Mode, int MoneyScale, string CashIncrement, string Status, long Version);
public sealed record FinancialPolicyDto(Guid LegalEntityId, Guid RoundingPolicyId, int ExchangeRateMaxAgeDays, bool ManualRateOverrideAllowed, long Version);
public sealed record UpdateFinancialPolicyRequest(Guid RoundingPolicyId, int ExchangeRateMaxAgeDays, bool ManualRateOverrideAllowed, long Version);
public sealed record CreateRoundingPolicyRequest(string Code, string NameAr, string NameEn, RoundingMode Mode, int MoneyScale, string CashIncrement);
public sealed record CreatePaymentTermRequest(string Code, string DisplayNameAr, string DisplayNameEn, string? Description, DateOnly EffectiveFrom, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record PaymentTermRuleRequest(int Sequence, PaymentAllocation AllocationType, decimal? Percentage, string? FixedAmount, DueDateBasis DueDateBasis, int Days, int? DayOfMonth, int MonthOffset);
public sealed record PaymentTermDto(Guid Id, string Code, string DisplayNameAr, string DisplayNameEn, int VersionNumber, DateOnly EffectiveFrom, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record PaymentScheduleRequest(string TotalAmount, DateOnly BasisDate, int Scale, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record PaymentScheduleLineDto(int Sequence, string Amount, DateOnly DueDate, PaymentAllocation Allocation);
public sealed class FinBaseException(string code, string message) : Exception(message) { public string Code { get; } = code; }
