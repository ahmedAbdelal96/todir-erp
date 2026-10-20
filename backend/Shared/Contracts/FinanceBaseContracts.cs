using Shared.Primitives.Identifiers;

namespace Shared.Contracts.FinanceBase;

public enum FinancialCalculationPurpose { Accounting, Sales, Purchase, Tax, Reporting }
public enum FinancialRoundingContext { MoneySettlement, LineNetAmount, DocumentSubtotal, DocumentTotal, FxBaseAmount, PaymentScheduleLine, PaymentAllocation, CashRounding }
public enum FinancialRoundingMode { HalfUp, HalfEven, Down, Up }
public enum FinancialLineDocumentStrategy { RoundEachLineThenSum, SumExactLinesThenRound }

/// <summary>Wire-safe financial amount. Amount remains an invariant decimal string in JSON.</summary>
public sealed record MoneyContract(string Amount, string CurrencyCode);

public sealed record FinancialPolicySnapshotContract(
    Guid LegalEntityId,
    Guid RoundingPolicyId,
    int ExchangeRateMaxAgeDays,
    bool ManualRateOverrideAllowed,
    long Version);

public sealed record FinancialRoundingRuleSnapshotContract(
    FinancialRoundingContext Context,
    FinancialRoundingMode Mode);

public sealed record FinancialRoundingPolicySnapshotContract(
    Guid Id,
    long Version,
    int MoneyScale,
    string CashIncrement,
    FinancialLineDocumentStrategy LineDocumentStrategy,
    IReadOnlyList<FinancialRoundingRuleSnapshotContract> ContextRules);

/// <summary>Immutable approved-rate facts captured for one financial calculation.</summary>
public sealed record ExchangeRateSnapshotContract(
    Guid TenantId,
    Guid LegalEntityId,
    Guid? ApprovedRateId,
    string FromCurrency,
    string ToCurrency,
    string Rate,
    FinancialCalculationPurpose Purpose,
    DateOnly EffectiveDate,
    string Source,
    long Version,
    bool ManualOverride,
    DateTimeOffset CapturedAt);

/// <summary>Resolved entity currency, policy, rounding, and optional rate facts for a calculation.</summary>
public sealed record FinancialCalculationContextSnapshotContract(
    TenantId TenantId,
    LegalEntityId LegalEntityId,
    string BaseCurrencyCode,
    string TransactionCurrencyCode,
    FinancialCalculationPurpose Purpose,
    DateOnly BusinessDate,
    FinancialPolicySnapshotContract FinancialPolicy,
    FinancialRoundingPolicySnapshotContract RoundingPolicy,
    ExchangeRateSnapshotContract? ExchangeRate,
    DateTimeOffset CapturedAt);

public sealed record PaymentTermRuleSnapshotContract(
    int Sequence,
    string AllocationType,
    string? PercentageRatio,
    string? FixedAmount,
    string DueDateBasis,
    int Days,
    int? DayOfMonth,
    int MonthOffset);

/// <summary>Immutable payment-term definition and rules selected for a business date.</summary>
public sealed record PaymentTermSnapshotContract(
    Guid PaymentTermId,
    Guid VersionId,
    string Code,
    long VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlyList<PaymentTermRuleSnapshotContract> Rules);

public sealed record PaymentScheduleLineContract(
    int Sequence,
    DateOnly DueDate,
    MoneyContract Amount);

/// <summary>Public in-process boundary for resolved financial context and immutable term snapshots.</summary>
public interface IFinanceBaseCapabilities
{
    Task<FinancialCalculationContextSnapshotContract?> ResolveCalculationContextAsync(
        TenantId tenantId,
        LegalEntityId legalEntityId,
        string transactionCurrencyCode,
        FinancialCalculationPurpose purpose,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<PaymentTermSnapshotContract?> ResolvePaymentTermAsync(
        TenantId tenantId,
        Guid paymentTermId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}
