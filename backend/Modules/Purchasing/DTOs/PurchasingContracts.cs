using System.ComponentModel.DataAnnotations;

namespace Modules.Purchasing.DTOs;

public sealed record PurchaseOrderLineRequest(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal Quantity, decimal? UnitPrice = null, string? PriceOverrideReason = null, Guid? ProductTemplateId = null);
public sealed record CreatePurchaseOrderRequest(Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, Guid? PriceListId, bool? ApprovalRequired, IReadOnlyList<PurchaseOrderLineRequest> Lines);
public sealed record UpdatePurchaseOrderRequest(long Version, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, IReadOnlyList<PurchaseOrderLineRequest> Lines);
public sealed record PurchaseOrderCommandRequest(long Version, string IdempotencyKey, string? Reason = null);
public sealed record CreateSupplierInvoiceRequest(long Version, string IdempotencyKey, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, string? SupplierInvoiceNumber);
public sealed record PurchaseOrderLineView(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal OrderedQuantity, decimal UnitPrice, decimal InvoicedQuantity, decimal CancelledQuantity, string? PriceEvidence);
public sealed record PurchaseOrderView(Guid Id, Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, string State, string ApprovalState, string ReceiptState, string InvoicingState, long Version, decimal NetTotal, IReadOnlyList<PurchaseOrderLineView> Lines, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderListItem(Guid Id, Guid SupplierPartyId, DateOnly OrderDate, string State, string CurrencyCode, decimal NetTotal, long Version, DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderPage(IReadOnlyList<PurchaseOrderListItem> Items, int Page, int PageSize, bool HasMore);
public sealed record PurchaseInvoiceLinkView(Guid InvoiceId, string Purpose, string IdempotencyKey, DateTimeOffset CreatedAt);
