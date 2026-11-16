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

public interface IInventoryCapabilities
{
    Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, InventoryReservationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId, InventoryMovementRequest issue, CancellationToken cancellationToken = default);
    Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, InventoryReturnRequest request, CancellationToken cancellationToken = default);
}
