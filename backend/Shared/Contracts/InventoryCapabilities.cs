namespace Shared.Contracts.Inventory;

public sealed record InventoryReservationRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId, Guid? LocationId,
    decimal Quantity, string SourceModule, Guid SourceDocumentId, Guid? SourceLineId, string IdempotencyKey,
    DateTimeOffset? ExpiresAt = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record InventoryMovementRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId, Guid? LocationId,
    decimal Quantity, Guid EnteredUomId, decimal ConversionFactorSnapshot, string SourceModule, Guid? SourceDocumentId,
    string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, string? ReasonCode = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record InventoryReturnRequest(Guid ProductVariantId, Guid LegalEntityId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal Quantity, string SourceModule, Guid SourceDocumentId, Guid SourceLineId, string IdempotencyKey,
    DateOnly OperationalDate, DateOnly PostingDate, string StockStatus = "Available", Guid? LotId = null, Guid? SerialId = null,
    decimal? UnitCost = null, string? CurrencyCode = null);
public sealed record InventoryMovementHistoryRequest(Guid ProductVariantId, Guid LegalEntityId, Guid? WarehouseId = null,
    Guid? LocationId = null, Guid? SourceDocumentId = null, int PageSize = 100);
public sealed record InventoryMovementHistoryItem(Guid Id, Guid OperationId, Guid ProductVariantId, Guid WarehouseId,
    Guid LocationId, decimal SignedBaseQuantity, DateTimeOffset EffectiveAt, long Sequence, string MovementType,
    string SourceModule, Guid? SourceDocumentId);
public sealed record InventoryOperationSnapshot(Guid OperationId, Guid LegalEntityId, string Status, string SourceModule,
    Guid? SourceDocumentId, string IdempotencyKey, string IdempotencyHash, decimal BaseQuantity, decimal ValuationAmount,
    string? CurrencyCode, bool IsReversal, Guid? ProductVariantId = null, Guid? WarehouseId = null,
    Guid? LocationId = null, Guid? EnteredUomId = null, decimal? ConversionFactorSnapshot = null,
    Guid? LotId = null, Guid? SerialId = null);
public sealed record InventoryAvailabilitySnapshot(Guid ProductVariantId, Guid WarehouseId, decimal OnHand,
    decimal Reserved, decimal Available, decimal InTransit, string BaseUnitCode);

public interface IInventoryCapabilities
{
    Task<IReadOnlyList<InventoryAvailabilitySnapshot>> GetAvailabilityAsync(Guid tenantId, Guid legalEntityId,
        Guid productVariantId, bool includeReservations = true, CancellationToken cancellationToken = default);
    Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, InventoryReservationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId, InventoryMovementRequest issue, CancellationToken cancellationToken = default);
    Task<Guid> ReceiveAsync(Guid tenantId, Guid actorId, InventoryMovementRequest receipt, CancellationToken cancellationToken = default);
    Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, InventoryReturnRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ReverseOperationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId,
        DateOnly operationalDate, DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementHistoryItem>> GetMovementHistoryAsync(Guid tenantId, InventoryMovementHistoryRequest request,
        CancellationToken cancellationToken = default);
    Task<InventoryOperationSnapshot?> GetOperationSnapshotAsync(Guid tenantId, Guid legalEntityId, Guid operationId,
        CancellationToken cancellationToken = default);
    Task<InventoryOperationSnapshot?> FindOperationBySourceAsync(Guid tenantId, Guid legalEntityId, string sourceModule,
        Guid sourceDocumentId, string idempotencyKey, CancellationToken cancellationToken = default);
}
