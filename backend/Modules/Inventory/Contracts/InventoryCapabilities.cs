namespace Todir.Inventory.Contracts;

public sealed record InventoryAvailabilityRequest(Guid ProductVariantId, Guid LegalEntityId, Guid? WarehouseId = null,
    IReadOnlyList<Guid>? WarehouseIds = null, bool IncludeReservations = true);
public sealed record InventoryAvailabilityLine(Guid ProductVariantId, Guid WarehouseId, decimal OnHand,
    decimal Reserved, decimal Available, decimal InTransit, string BaseUnitCode);
public sealed record InventoryReservationRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId,
    Guid? LocationId, decimal Quantity, string SourceModule, Guid SourceDocumentId, Guid? SourceLineId,
    string IdempotencyKey, DateTimeOffset? ExpiresAt = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record InventoryAdjustmentRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId,
    Guid? LocationId, decimal Quantity, Guid EnteredUomId, string Direction, string Reason,
    DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey, string StockStatus = "Available",
    Guid? LotId = null, decimal? UnitCost = null, string? CurrencyCode = null);
public sealed record InventoryMovementRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId,
    Guid? LocationId, decimal Quantity, Guid EnteredUomId, decimal ConversionFactorSnapshot,
    string SourceModule, Guid? SourceDocumentId, string IdempotencyKey, DateOnly OperationalDate,
    DateOnly PostingDate, string? ReasonCode = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record InventoryReturnRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal Quantity, string SourceModule, Guid SourceDocumentId, Guid SourceLineId,
    string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, string StockStatus = "Available",
    Guid? LotId = null, Guid? SerialId = null, decimal? UnitCost = null, string? CurrencyCode = null);
public sealed record InventoryTransferRequest(Guid ProductVariantId, Guid LegalEntityId, Guid SourceWarehouseId,
    Guid? SourceLocationId, Guid DestinationWarehouseId, Guid? DestinationLocationId, decimal Quantity,
    string Mode, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate);
public sealed record InventoryMovementHistoryQuery(Guid ProductVariantId, Guid LegalEntityId, Guid? WarehouseId,
    Guid? LocationId, Guid? LotId, Guid? SerialId, DateTimeOffset? From, DateTimeOffset? To,
    string? Cursor, int PageSize = 100);
public sealed record InventoryMovementHistoryItem(Guid Id, Guid OperationId, Guid ProductVariantId,
    Guid WarehouseId, Guid LocationId, decimal SignedBaseQuantity, DateTimeOffset EffectiveAt,
    long Sequence, string MovementType, string SourceModule, Guid? SourceDocumentId);
public sealed record InventoryValuationQuery(Guid LegalEntityId, Guid ProductVariantId, DateOnly AsOfDate);
public sealed record InventoryValuationSnapshot(Guid ProductVariantId, decimal Quantity, decimal CarryingValue,
    string CurrencyCode, string CostingMethod, DateOnly AsOfDate);
public sealed record InventoryReconciliationSnapshot(Guid LegalEntityId, DateOnly AsOfDate, decimal InventoryValue,
    decimal LedgerValue, decimal Difference, string CurrencyCode, string Status, IReadOnlyList<string> Diagnostics);
public sealed record InventoryValuationTotal(decimal Quantity, decimal CarryingValue, string CurrencyCode);
public sealed record InventoryNegativeStockCondition(Guid Id, Guid ProductVariantId, Guid WarehouseId, Guid LocationId,
    string StockStatus, Guid? LotId, Guid? SerialId, string Status, decimal ShortfallBaseQuantity,
    Guid OpenedByMovementId, Guid LastSeenMovementId, Guid? ResolvedByMovementId, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt);
public sealed record InventoryScanResult(string Kind, Guid RecordId, Guid? ProductVariantId, Guid? WarehouseId,
    string Reference, string Status);
public sealed record InventoryLocationSuggestion(Guid WarehouseId, Guid LocationId, string LocationCode,
    decimal? AvailableQuantity, Guid? LotId, Guid? SerialId, string Reason);

public interface IInventoryCapabilities
{
    Task<IReadOnlyList<InventoryAvailabilityLine>> GetBatchAvailabilityAsync(Guid tenantId,
        InventoryAvailabilityRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, InventoryReservationRequest request,
        CancellationToken cancellationToken = default);
    Task ReleaseReservationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId,
        CancellationToken cancellationToken = default);
    Task CancelReservationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId,
        CancellationToken cancellationToken = default);
    Task<int> ExpireReservationsAsync(Guid tenantId, Guid legalEntityId, int limit = 500,
        CancellationToken cancellationToken = default);
    Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId,
        InventoryMovementRequest issue, CancellationToken cancellationToken = default);
    Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, InventoryReturnRequest request,
        CancellationToken cancellationToken = default);
    Task<Guid> AdjustAsync(Guid tenantId, Guid actorId, InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default);
    Task<Guid> ReverseOperationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId,
        DateOnly operationalDate, DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementHistoryItem>> GetMovementHistoryAsync(Guid tenantId,
        InventoryMovementHistoryQuery query, CancellationToken cancellationToken = default);
    Task<InventoryValuationSnapshot?> GetValuationAsync(Guid tenantId, InventoryValuationQuery query,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryNegativeStockCondition>> GetNegativeStockConditionsAsync(Guid tenantId, Guid legalEntityId,
        string? status = "Active", DateTimeOffset? beforeOpenedAt = null, Guid? beforeId = null, int pageSize = 100,
        CancellationToken cancellationToken = default);
    Task<InventoryLocationSuggestion?> SuggestPutawayAsync(Guid tenantId, Guid legalEntityId, Guid productVariantId,
        Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryLocationSuggestion>> SuggestPickingAsync(Guid tenantId, Guid legalEntityId,
        Guid productVariantId, Guid? warehouseId, int limit = 100, CancellationToken cancellationToken = default);
    Task<InventoryReconciliationSnapshot> ReconcileAsync(Guid tenantId, Guid legalEntityId,
        DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task<InventoryScanResult?> ResolveScanAsync(Guid tenantId, Guid legalEntityId, string code,
        CancellationToken cancellationToken = default);
}
