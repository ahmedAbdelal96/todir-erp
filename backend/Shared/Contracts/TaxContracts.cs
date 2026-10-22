using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Tax;

public sealed record TaxContextContract(
    TenantId TenantId,
    LegalEntityId LegalEntityId,
    string Direction,
    string DocumentKind,
    string TransactionType,
    DateOnly DocumentDate,
    DateOnly? TaxDate,
    DateOnly? SupplyDate,
    DateOnly? PostingDate,
    Guid? PartyId,
    string? PartyCountryCode,
    string? PlaceOfSupplyCountryCode,
    string? JurisdictionCode,
    Guid? ProductTemplateId,
    Guid? ProductVariantId,
    decimal Quantity,
    Guid? UnitOfMeasureId,
    string PriceMode,
    string CommercialAmount,
    string DiscountAmount,
    string TaxableCharges,
    string TransactionCurrency,
    string BaseCurrency,
    Guid? RegistrationId,
    string? TaxSchemeCode = null,
    string? DocumentSemantics = null,
    IReadOnlyDictionary<string, string>? Dimensions = null);

public sealed record TaxRecoverabilityContract(string Kind, string RecoverablePercentage, string RecoverableAmount, string NonRecoverableAmount);
public sealed record TaxLineContract(string TaxTypeCode, string TaxCode, string Treatment, string? Rate, string? FixedAmountPerUnit, string TaxableBase, string TaxAmount, string CurrencyCode, string JurisdictionCode, string? LegalReasonCode, string? LegalReasonText, string? ReportingCode, Guid RuleId, string RuleVersion, TaxRecoverabilityContract? Recoverability = null);
public sealed record TaxExplanationContract(string CountryPackCode, string CountryPackVersion, string TaxDate, string Jurisdiction, string RegistrationStatus, string? PartyTreatment, string? ProductCategory, Guid? MatchedRuleId, IReadOnlyList<string> ComponentCodes, string Rounding, string Summary, string? PlaceOfSupplyReason = null, string? PartyEvidence = null, string? ProductEvidence = null, string? LegalReasonCode = null, string? LegalReasonText = null);
public sealed record TaxRoundingEvidenceContract(string Strategy, int MoneyScale, string Mode, string UnroundedTaxTotal, string PreReconciliationTotal, string Residual, string AllocationTarget, string FinalTotal);
public sealed record TaxSnapshotEvidenceContract(string CapabilityState, string? RegistrationSnapshot, string? PartyTaxSnapshot, string? ProductTaxSnapshot, string JurisdictionSnapshot, string? TaxFxSnapshot, string? RoundingPolicyVersion, string? QuantitySnapshot = null);
public sealed record TaxResultContract(string EvaluationState, string? Treatment, string NetAmount, string TaxTotal, string GrossAmount, IReadOnlyList<TaxLineContract> TaxLines, string JurisdictionSnapshot, string RegistrationSnapshot, string? RuleSetVersion, string CalculationVersion, TaxExplanationContract Explanation, string? TaxFxSnapshot = null, string? RoundingPolicyVersion = null, TaxRoundingEvidenceContract? RoundingEvidence = null, string? QuantitySnapshot = null);

public sealed record TaxSnapshotCaptureContract(string SourceDocumentKind, Guid SourceDocumentId, Guid? SourceLineId, TaxContextContract Context);
public sealed record TaxSnapshotContract(Guid SnapshotId, string SourceDocumentKind, Guid SourceDocumentId, Guid? SourceLineId, string TaxDate, string EvaluationState, string? RuleSetVersion, string CalculationVersion, TaxContextContract Input, TaxResultContract Result, DateTimeOffset CapturedAt, TaxSnapshotEvidenceContract? Evidence = null);

public interface ITaxCapabilities
{
    Task<TaxResultContract> CalculateAsync(TaxContextContract context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxResultContract>> CalculateBatchAsync(IReadOnlyList<TaxContextContract> contexts, CancellationToken cancellationToken = default);
    Task<TaxSnapshotContract> CaptureSnapshotAsync(TaxSnapshotCaptureContract snapshot, CancellationToken cancellationToken = default);
}
