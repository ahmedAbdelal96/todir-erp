namespace Shared.Contracts.Inventory;

public sealed record PurchaseReceiptRequest(Guid LegalEntityId, Guid PurchaseOrderId, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, IReadOnlyList<PurchaseReceiptLine> Lines, Guid? SupplierPartyId = null);
public sealed record PurchaseReceiptLine(Guid PurchaseOrderLineId, Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, Guid UnitOfMeasureId, decimal Quantity, Guid? LotId, Guid? SerialId, string InitialStockStatus = "Available");
public sealed record PurchaseReceiptResult(IReadOnlyList<Guid> ReceiptIds, IReadOnlyList<PurchaseReceiptLineResult> Lines, bool IdempotentReplay);
public sealed record PurchaseReceiptLineResult(Guid PurchaseOrderLineId, Guid ReceiptId, decimal Quantity);
public sealed record PurchaseReturnRequest(Guid LegalEntityId, Guid ReturnRequestId, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, IReadOnlyList<PurchaseReturnLine> Lines, string FromStockStatus = "Available");
public sealed record PurchaseReturnLine(Guid PurchaseOrderLineId, Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, Guid UnitOfMeasureId, decimal Quantity, Guid? LotId, Guid? SerialId);

public interface IInventoryPurchasingCapabilities
{
    Task<PurchaseReceiptResult> ReceivePurchaseAsync(Guid tenantId, Guid actorId, PurchaseReceiptRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ReturnPurchaseAsync(Guid tenantId, Guid actorId, PurchaseReturnRequest request, CancellationToken cancellationToken = default);
}
