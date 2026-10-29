using System.ComponentModel.DataAnnotations;

namespace Modules.Purchasing.DTOs;

public sealed record PurchaseOrderLineRequest(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal Quantity, decimal? UnitPrice = null, string? PriceOverrideReason = null, Guid? ProductTemplateId = null);
public sealed record CreatePurchaseOrderRequest(Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, Guid? PriceListId, bool? ApprovalRequired, IReadOnlyList<PurchaseOrderLineRequest> Lines);
public sealed record UpdatePurchaseOrderRequest(long Version, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, IReadOnlyList<PurchaseOrderLineRequest> Lines);
public sealed record PurchaseOrderCommandRequest(long Version, string IdempotencyKey, string? Reason = null);
public sealed record PurchaseInvoiceLineRequest(Guid PurchaseOrderLineId, decimal Quantity);
public sealed record CreateSupplierInvoiceRequest(long Version, string IdempotencyKey, DateOnly DocumentDate, DateOnly PostingDate, DateOnly TaxDate, string? SupplierInvoiceNumber, IReadOnlyList<PurchaseInvoiceLineRequest>? Lines = null);
public sealed record SupplierPurchaseProfileRequest(Guid LegalEntityId, Guid SupplierPartyId, string? SupplierCode, Guid? PaymentTermId, string? DefaultCurrencyCode, Guid? DefaultPriceListId, Guid? DefaultBuyerId, Guid? DefaultWarehouseId, int? LeadTimeDays, bool SupplierReferenceRequired, string? PurchasePriceContextJson = null);
public sealed record SupplierPurchaseProfileView(Guid LegalEntityId, Guid SupplierPartyId, string? SupplierCode, Guid? PaymentTermId, string? DefaultCurrencyCode, Guid? DefaultPriceListId, Guid? DefaultBuyerId, Guid? DefaultWarehouseId, int? LeadTimeDays, bool SupplierReferenceRequired, long Version);
public sealed record PurchaseSettingsRequest(Guid LegalEntityId, bool ApprovalRequired, decimal? ApprovalThreshold, string OverReceiptPolicy, decimal OverReceiptTolerance, bool InvoiceWithoutReceipt, bool ThreeWayMatching, bool BuyerApproverSeparation, decimal QuantityTolerance, decimal PriceAbsoluteTolerance, decimal PricePercentageTolerance, decimal TaxTolerance, string InvoiceEligibilityBasis);
public sealed record PurchaseSettingsView(Guid LegalEntityId, bool ApprovalRequired, decimal? ApprovalThreshold, string OverReceiptPolicy, decimal OverReceiptTolerance, bool InvoiceWithoutReceipt, bool ThreeWayMatching, bool BuyerApproverSeparation, decimal QuantityTolerance, decimal PriceAbsoluteTolerance, decimal PricePercentageTolerance, decimal TaxTolerance, string InvoiceEligibilityBasis);
public sealed record PurchaseReceiptLineRequest(Guid PurchaseOrderLineId, Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, Guid UnitOfMeasureId, decimal Quantity, Guid? LotId = null, Guid? SerialId = null);
public sealed record ReceivePurchaseOrderRequest(Guid LegalEntityId, long Version, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, IReadOnlyList<PurchaseReceiptLineRequest> Lines);
public sealed record PurchaseReceiptResult(Guid PurchaseOrderId, IReadOnlyList<Guid> ReceiptIds, long Version, string ReceiptState);
public sealed record MatchPurchaseOrderRequest(long Version, Guid SupplierInvoiceId, string Basis, decimal QuantityTolerance, decimal PriceAbsoluteTolerance, decimal PricePercentageTolerance, decimal TaxTolerance, string IdempotencyKey, bool ApproveException = false, string? Reason = null);
public sealed record PurchaseMatchView(Guid Id, Guid PurchaseOrderId, Guid SupplierInvoiceId, string Basis, string State, decimal VarianceAmount, long SourceVersion, string DetailsJson);
public sealed record CreateSupplierReturnRequest(long Version, string Resolution, string Reason, IReadOnlyList<SupplierReturnLineRequest> Lines);
public sealed record SupplierReturnLineRequest(Guid PurchaseOrderLineId, decimal Quantity, Guid? ProductVariantId, Guid? UnitOfMeasureId, Guid? WarehouseId, Guid? LocationId, Guid? LotId = null, Guid? SerialId = null);
public sealed record SupplierReturnView(Guid Id, Guid PurchaseOrderId, Guid SupplierPartyId, string State, string Resolution, string Reason, long Version, Guid? CreditNoteId);
public sealed record PurchaseOrderLineView(Guid Id, int Sequence, Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal OrderedQuantity, decimal UnitPrice, decimal InvoicedQuantity, decimal CancelledQuantity, string? PriceEvidence);
public sealed record PurchaseOrderView(Guid Id, Guid LegalEntityId, Guid? BranchId, Guid SupplierPartyId, string CurrencyCode, DateOnly OrderDate, string State, string ApprovalState, string ReceiptState, string InvoicingState, long Version, decimal NetTotal, IReadOnlyList<PurchaseOrderLineView> Lines, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderListItem(Guid Id, Guid SupplierPartyId, DateOnly OrderDate, string State, string CurrencyCode, decimal NetTotal, long Version, DateTimeOffset UpdatedAt);
public sealed record PurchaseOrderPage(IReadOnlyList<PurchaseOrderListItem> Items, int Page, int PageSize, bool HasMore);
public sealed record PurchaseInvoiceLinkView(Guid InvoiceId, string Purpose, string IdempotencyKey, DateTimeOffset CreatedAt);
