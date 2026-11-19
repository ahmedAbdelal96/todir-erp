using Modules.Inventory.Services;
using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Inventory.Contracts;

public sealed class InventoryCapabilityAdapter(InventoryService service) : Shared.Contracts.Inventory.IInventoryCapabilities
{
    public Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.InventoryReservationRequest request, CancellationToken cancellationToken = default) =>
        service.ReserveAsync(tenantId, actorId, new Todir.Inventory.Contracts.InventoryReservationRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, request.LocationId, request.Quantity, request.SourceModule, request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey, request.ExpiresAt, request.LotId, request.SerialId), cancellationToken);

    public Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId, Shared.Contracts.Inventory.InventoryMovementRequest issue, CancellationToken cancellationToken = default) =>
        service.ConsumeReservationAsync(tenantId, actorId, reservationId, new Todir.Inventory.Contracts.InventoryMovementRequest(issue.ProductVariantId, issue.LegalEntityId, issue.WarehouseId, issue.LocationId, issue.Quantity, issue.EnteredUomId, issue.ConversionFactorSnapshot, issue.SourceModule, issue.SourceDocumentId, issue.IdempotencyKey, issue.OperationalDate, issue.PostingDate, issue.ReasonCode, issue.LotId, issue.SerialId), cancellationToken);

    public async Task<Guid> ReceiveAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.InventoryMovementRequest receipt, CancellationToken cancellationToken = default)
    {
        var result = await service.ReceiveAsync(new TenantId(tenantId), actorId, receipt.LegalEntityId,
            new ConfirmStockMovementRequest(receipt.ProductVariantId, receipt.WarehouseId, receipt.LocationId, receipt.EnteredUomId,
                receipt.Quantity, receipt.OperationalDate, receipt.PostingDate, receipt.IdempotencyKey, receipt.SourceModule,
                receipt.SourceDocumentId, StockStatus.Available, receipt.LotId, receipt.SerialId), Guid.CreateVersion7(), null, cancellationToken);
        return result.OperationId;
    }

    public Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.InventoryReturnRequest request, CancellationToken cancellationToken = default) =>
        service.ReceiveReturnAsync(tenantId, actorId, new Todir.Inventory.Contracts.InventoryReturnRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, request.LocationId, request.EnteredUomId, request.Quantity, request.SourceModule, request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey, request.OperationalDate, request.PostingDate, request.StockStatus, request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode), cancellationToken);

    public Task<Guid> ReverseOperationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId,
        DateOnly operationalDate, DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default) =>
        service.ReverseOperationAsync(tenantId, actorId, legalEntityId, operationId, operationalDate, postingDate, idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<Shared.Contracts.Inventory.InventoryMovementHistoryItem>> GetMovementHistoryAsync(Guid tenantId,
        Shared.Contracts.Inventory.InventoryMovementHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await service.GetMovementHistoryAsync(tenantId,
            new Todir.Inventory.Contracts.InventoryMovementHistoryQuery(request.ProductVariantId, request.LegalEntityId,
                request.WarehouseId, request.LocationId, null, null, null, null, null, request.PageSize), cancellationToken);
        return rows.Select(row => new Shared.Contracts.Inventory.InventoryMovementHistoryItem(row.Id, row.OperationId,
            row.ProductVariantId, row.WarehouseId, row.LocationId, row.SignedBaseQuantity, row.EffectiveAt, row.Sequence,
            row.MovementType, row.SourceModule, row.SourceDocumentId)).ToArray();
    }
}
