namespace Shared.Contracts.Invoicing;

using Shared.Primitives.Identifiers;

public sealed record InvoiceSourceReferenceContract(string SourceModule, Guid SourceDocumentId, string Purpose,
    string IdempotencyKey, string PayloadHash);
public sealed record InvoiceSourceLineContract(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description,
    string Quantity, string? AgreedUnitPrice = null, string? PriceOverrideReason = null, string? BusinessCategoryCode = null,
    string? SourceLineReference = null);
public sealed record InvoiceSourceChargeContract(string Code, string Description, string Amount, string TaxTreatment,
    string? BusinessCategoryCode = null);
public sealed record InvoiceSourceDocumentContract(string SourceModule, Guid SourceDocumentId, string Purpose, string IdempotencyKey,
    string Direction, Guid LegalEntityId, Guid? BranchId, Guid PartyId, Guid? AddressId, DateOnly DocumentDate, DateOnly PostingDate,
    DateOnly TaxDate, DateOnly? SupplyDate, string CurrencyCode, string PriceMode, Guid? PaymentTermId,
    string? SupplierInvoiceNumber, string? DocumentDiscountAmount, IReadOnlyList<InvoiceSourceLineContract> Lines,
    IReadOnlyList<InvoiceSourceChargeContract>? Charges = null);

public interface IInvoicingSourceCapabilities
{
    Task<Guid> CreateDraftFromSourceAsync(TenantId tenantId, Guid actorId, Guid membershipId, InvoiceSourceDocumentContract request,
        Guid correlationId, CancellationToken cancellationToken = default);
}

public interface IInvoiceComplianceSnapshotCapability
{
    Task<InvoiceComplianceSnapshotContract?> GetInvoiceComplianceSnapshotAsync(TenantId tenantId, Guid legalEntityId, Guid invoiceId,
        CancellationToken cancellationToken = default);
}

public enum InvoiceAttachmentRole { CustomerPO, SupplierOriginalInvoice, Contract, DeliveryProof, TaxEvidence, OtherSupporting }
public sealed record InvoiceAttachmentReferenceRequest(Guid LegalEntityId, Guid InvoiceId, Shared.Contracts.Platform.VerifiedPlatformFileReference File,
    InvoiceAttachmentRole Role);
public sealed record InvoiceIssuedArtifactReferenceRequest(Guid LegalEntityId, Guid InvoiceId, Shared.Contracts.Platform.VerifiedPlatformFileReference File,
    string TemplateCode, string TemplateVersion, string LanguageCode);
public sealed record InvoiceIssuedArtifactRecord(Guid Id, Guid FileRef, string Sha256, string TemplateCode, string TemplateVersion, string LanguageCode);

public interface IInvoiceFileReferenceCapability
{
    Task<Guid> AddAttachmentReferenceAsync(TenantId tenantId, Guid actorId, Guid membershipId, InvoiceAttachmentReferenceRequest request,
        Guid correlationId, CancellationToken cancellationToken = default);
    Task<Guid> RecordIssuedArtifactReferenceAsync(TenantId tenantId, Guid actorId, InvoiceIssuedArtifactReferenceRequest request,
        Guid correlationId, CancellationToken cancellationToken = default);
    Task<InvoiceIssuedArtifactRecord?> GetOriginalIssuedArtifactAsync(TenantId tenantId, Guid legalEntityId, Guid invoiceId, string templateCode,
        string templateVersion, string languageCode,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceFrozenTaxComponentContract(string TaxCode, string TaxTypeCode, string Treatment, string TaxableBase,
    string TaxAmount, string EvidenceJson);
public sealed record InvoiceFrozenDiscountContract(string DiscountType, string SourceAmount, string AllocatedAmount);
public sealed record InvoiceFrozenChargeContract(string Code, string Description, string Amount, string TaxTreatment,
    IReadOnlyList<InvoiceFrozenChargeAllocationContract> Allocations);
public sealed record InvoiceFrozenChargeAllocationContract(Guid LineId, string Amount);
public sealed record InvoiceFrozenRoundingRuleContract(string Context, string Mode);
public sealed record InvoiceFrozenExchangeRateContract(Guid? ApprovedRateId, string FromCurrency, string ToCurrency, string Rate,
    string Purpose, DateOnly EffectiveDate, string Source, long Version, bool ManualOverride, DateTimeOffset CapturedAt);
public sealed record InvoiceFrozenFinancialContextContract(string BaseCurrencyCode, string TransactionCurrencyCode, string Purpose,
    DateOnly BusinessDate, long FinancialPolicyVersion, int ExchangeRateMaxAgeDays, bool ManualRateOverrideAllowed,
    long RoundingPolicyVersion, int MoneyScale, string CashIncrement, string LineDocumentStrategy,
    IReadOnlyList<InvoiceFrozenRoundingRuleContract> RoundingRules, InvoiceFrozenExchangeRateContract? ExchangeRate, DateTimeOffset CapturedAt);
public sealed record InvoiceFrozenLineContract(Guid LineId, int Sequence, Guid? ProductVariantId, string Description, string Quantity,
    Guid? UnitOfMeasureId, string? UnitCode, string? ResolvedUnitPrice, string FinalUnitPrice, string PriceEvidenceJson,
    string NetAmount, string TaxAmount, string GrossAmount, IReadOnlyList<InvoiceFrozenDiscountContract> Discounts,
    IReadOnlyList<InvoiceFrozenTaxComponentContract> Taxes);
public sealed record InvoiceComplianceSnapshotContract(int SchemaVersion, Guid InvoiceId, Guid LegalEntityId, string Direction,
    string DocumentType, string DocumentNumber, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, DateOnly? SupplyDate,
    string CurrencyCode, string PriceMode, string PartyEvidenceJson, string? AddressEvidenceJson, string? PaymentTermEvidenceJson,
    string NetAmount, string TaxableAmount, string DiscountAmount, string ChargeAmount, string TaxAmount, string GrossAmount,
    IReadOnlyList<InvoiceFrozenLineContract> Lines, IReadOnlyList<InvoiceFrozenChargeContract> Charges,
    IReadOnlyList<InvoiceScheduleContract> PaymentSchedule,
    InvoiceFrozenFinancialContextContract? FinancialContext = null);
public sealed record InvoiceScheduleContract(int Sequence, DateOnly DueDate, string Amount, string CurrencyCode);
