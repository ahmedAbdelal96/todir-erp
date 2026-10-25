using Modules.FinanceBase.Entities;
using Shared.Contracts.FinanceBase;

namespace Modules.FinanceBase.DTOs;

public sealed record CurrencyDto(string Code, int NumericCode, string NameAr, string NameEn, int MinorUnit, string Status, string DatasetVersion);
public sealed record EntityCurrencyDto(Guid LegalEntityId, string CurrencyCode, bool Enabled, bool AllowSales, bool AllowPurchase, bool AllowPayment, bool AllowManualRate, string Status, long Version);
public sealed record EnableCurrencyRequest(bool AllowSales = true, bool AllowPurchase = true, bool AllowPayment = true, bool AllowManualRate = false);
public sealed record CreateExchangeRateRequest(Guid LegalEntityId, string FromCurrency, string ToCurrency, string Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string SourceType, string? ProviderReference, string? Reason = null);
public sealed record ConvertMoneyRequest(Guid LegalEntityId, string Amount, string FromCurrency, string ToCurrency, ExchangeRatePurpose Purpose, DateOnly BusinessDate);
public sealed record ExchangeRateSnapshotDto(Guid TenantId, Guid LegalEntityId, Guid? ApprovedRateId, string FromCurrency, string ToCurrency, string Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string Source, long Version, bool ManualOverride, DateTimeOffset CapturedAt);
public sealed record ConvertedMoneyDto(string SourceAmount, string SourceCurrency, string TargetAmount, string TargetCurrency, string Rate, DateOnly RateDate, ExchangeRatePurpose Purpose, ExchangeRateSnapshotDto? Snapshot = null);
public sealed record ExchangeRateDto(Guid Id, Guid LegalEntityId, string FromCurrency, string ToCurrency, string Rate, ExchangeRatePurpose Purpose, DateOnly EffectiveDate, string SourceType, string? ProviderReference, ExchangeRateStatus Status, long Version);
public sealed record CreateExchangeRateSourceConfigRequest(string SourceCode, string FromCurrency, string ToCurrency, ExchangeRatePurpose Purpose, int Priority, string? CredentialReference, int SyncIntervalSeconds = 3600);
public sealed record ExchangeRateSourceConfigDto(Guid Id, Guid LegalEntityId, string SourceCode, string FromCurrency, string ToCurrency, ExchangeRatePurpose Purpose, int Priority, bool Enabled, string? CredentialReference, int SyncIntervalSeconds, int FailureCount, long Version);
public sealed record RoundingPolicyContextRuleRequest(RoundingContext Context, RoundingMode Mode);
public sealed record RoundingPolicyDto(Guid Id, string Code, string NameAr, string NameEn, RoundingMode Mode, int MoneyScale, string CashIncrement, string Status, long Version, LineDocumentRoundingStrategy LineDocumentStrategy = LineDocumentRoundingStrategy.RoundEachLineThenSum, IReadOnlyList<RoundingPolicyContextRuleRequest>? ContextRules = null);
public sealed record FinancialPolicyDto(Guid LegalEntityId, Guid RoundingPolicyId, int ExchangeRateMaxAgeDays, bool ManualRateOverrideAllowed, long Version);
public sealed record UpdateFinancialPolicyRequest(Guid RoundingPolicyId, int ExchangeRateMaxAgeDays, bool ManualRateOverrideAllowed, long Version);
public sealed record CreateRoundingPolicyRequest(string Code, string NameAr, string NameEn, RoundingMode Mode, int MoneyScale, string CashIncrement, IReadOnlyList<RoundingPolicyContextRuleRequest>? ContextRules = null, LineDocumentRoundingStrategy LineDocumentStrategy = LineDocumentRoundingStrategy.RoundEachLineThenSum);
public sealed record CreatePaymentTermRequest(string Code, string DisplayNameAr, string DisplayNameEn, string? Description, DateOnly EffectiveFrom, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record CreatePaymentTermVersionRequest(DateOnly EffectiveFrom, string? Description, IReadOnlyList<PaymentTermRuleRequest> Rules, long ExpectedTermVersion);
public sealed record PaymentTermLifecycleRequest(long ExpectedTermVersion);
public sealed record PaymentTermListItemDto(Guid Id, string Code, string DisplayNameAr, string DisplayNameEn, string Status, long Version, int? CurrentVersionNumber, DateOnly? CurrentEffectiveFrom);
public sealed record PaymentTermVersionDto(Guid Id, int VersionNumber, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Status, string? Description, IReadOnlyList<PaymentTermRuleRequest> Rules, long TermVersion = 0);
public sealed record PaymentTermHistoryDto(Guid Id, string Code, string DisplayNameAr, string DisplayNameEn, string Status, long Version, IReadOnlyList<PaymentTermVersionDto> Versions);
public sealed record PaymentTermRuleRequest(int Sequence, PaymentAllocation AllocationType, string? Percentage, string? FixedAmount, DueDateBasis DueDateBasis, int Days, int? DayOfMonth, int MonthOffset);
public sealed record PaymentTermDto(Guid Id, string Code, string DisplayNameAr, string DisplayNameEn, int VersionNumber, DateOnly EffectiveFrom, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record PaymentScheduleRequest(string TotalAmount, DateOnly BasisDate, int Scale, IReadOnlyList<PaymentTermRuleRequest> Rules);
public sealed record PaymentScheduleLineDto(int Sequence, string Amount, DateOnly DueDate, PaymentAllocation Allocation);
public sealed class FinanceBaseException(string code, string message) : FinanceBaseCapabilityException(code, message) { }
