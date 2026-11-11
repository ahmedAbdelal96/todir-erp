using Shared.Primitives.Identifiers;
using Shared.Contracts.Invoicing;

namespace Shared.Contracts.Purchasing;

public sealed record PurchaseOrderSourceLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, string Quantity, string UnitPrice, string? SourceLineReference);
public sealed record PurchaseOrderInvoiceSource(Guid PurchaseOrderId, string Purpose, string IdempotencyKey, Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, string CurrencyCode, IReadOnlyList<PurchaseOrderSourceLine> Lines, string? SupplierInvoiceNumber);

public interface IPurchasingSourceCapabilities
{
    Task<PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public sealed record FixedAssetPurchasingSourceLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode,
    string Description, string Quantity, string FinalUnitPrice, string NetAmount, string? SourceLineReference);

public sealed record FixedAssetPurchasingSourceSnapshot(Guid PurchaseOrderId, Guid SupplierInvoiceId,
    IReadOnlyList<Guid> ReceiptIds, Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId,
    DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, string CurrencyCode,
    string SupplierInvoiceNumber, decimal EligibleAmount, IReadOnlyList<FixedAssetPurchasingSourceLine> Lines,
    InvoiceComplianceSnapshotContract InvoiceEvidence);

public interface IPurchasingFixedAssetCapabilities
{
    Task<FixedAssetPurchasingSourceSnapshot?> GetFinalizedCapitalizationSourceAsync(TenantId tenantId,
        Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public interface IPurchasingReadCapabilities
{
    Task<bool> IsPurchaseOrderApprovedAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public sealed record PurchaseLandedCostLine(Guid PurchaseOrderLineId, Guid? ProductVariantId, decimal Quantity, decimal UnitPrice, string CurrencyCode);
public sealed record PurchaseLandedCostContext(Guid PurchaseOrderId, Guid LegalEntityId, Guid SupplierPartyId, string CurrencyCode, IReadOnlyList<PurchaseLandedCostLine> Lines);

public interface IPurchasingLandedCostCapabilities
{
    Task<PurchaseLandedCostContext?> GetLandedCostContextAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
