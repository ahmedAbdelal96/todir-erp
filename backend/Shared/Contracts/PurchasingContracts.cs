using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Purchasing;

public sealed record PurchaseOrderSourceLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, string Quantity, string UnitPrice, string? SourceLineReference);
public sealed record PurchaseOrderInvoiceSource(Guid PurchaseOrderId, string Purpose, string IdempotencyKey, Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, string CurrencyCode, IReadOnlyList<PurchaseOrderSourceLine> Lines, string? SupplierInvoiceNumber);

public interface IPurchasingSourceCapabilities
{
    Task<PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}

public interface IPurchasingReadCapabilities
{
    Task<bool> IsPurchaseOrderApprovedAsync(TenantId tenantId, Guid legalEntityId, Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
