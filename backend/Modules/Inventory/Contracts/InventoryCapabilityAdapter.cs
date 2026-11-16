using Modules.Inventory.Services;

namespace Modules.Inventory.Contracts;

public sealed class InventoryCapabilityAdapter(InventoryService service) : Shared.Contracts.Inventory.IInventoryCapabilities
{
    public Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.InventoryReservationRequest request, CancellationToken cancellationToken = default) =>
        service.ReserveAsync(tenantId, actorId, new Todir.Inventory.Contracts.InventoryReservationRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, request.LocationId, request.Quantity, request.SourceModule, request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey, request.ExpiresAt, request.LotId, request.SerialId), cancellationToken);

    public Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId, Shared.Contracts.Inventory.InventoryMovementRequest issue, CancellationToken cancellationToken = default) =>
        service.ConsumeReservationAsync(tenantId, actorId, reservationId, new Todir.Inventory.Contracts.InventoryMovementRequest(issue.ProductVariantId, issue.LegalEntityId, issue.WarehouseId, issue.LocationId, issue.Quantity, issue.EnteredUomId, issue.ConversionFactorSnapshot, issue.SourceModule, issue.SourceDocumentId, issue.IdempotencyKey, issue.OperationalDate, issue.PostingDate, issue.ReasonCode, issue.LotId, issue.SerialId), cancellationToken);

    public Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.InventoryReturnRequest request, CancellationToken cancellationToken = default) =>
        service.ReceiveReturnAsync(tenantId, actorId, new Todir.Inventory.Contracts.InventoryReturnRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, request.LocationId, request.EnteredUomId, request.Quantity, request.SourceModule, request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey, request.OperationalDate, request.PostingDate, request.StockStatus, request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode), cancellationToken);
}
