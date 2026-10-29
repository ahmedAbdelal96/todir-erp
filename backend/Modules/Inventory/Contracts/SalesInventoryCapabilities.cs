using Todir.Sales.Contracts;

namespace Todir.Inventory.Contracts;

public sealed class SalesInventoryCapabilities(IInventoryCapabilities inventory) : ISalesInventoryCapabilities
{
    public async Task<IReadOnlyList<SalesInventoryAvailabilityLine>> GetAvailabilityAsync(Guid tenantId,
        SalesInventoryAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var result = new List<SalesInventoryAvailabilityLine>();
        foreach (var productVariantId in request.ProductVariantIds.Distinct())
        {
            var lines = await inventory.GetBatchAvailabilityAsync(tenantId,
                new InventoryAvailabilityRequest(productVariantId, request.LegalEntityId, request.WarehouseId,
                    request.WarehouseId is null ? null : [request.WarehouseId.Value], request.IncludeReservations), cancellationToken);
            result.AddRange(lines.Select(line => new SalesInventoryAvailabilityLine(line.ProductVariantId, line.WarehouseId,
                line.OnHand, line.Reserved, line.Available, line.InTransit, line.BaseUnitCode)));
        }
        return result;
    }

    public Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, SalesInventoryReservationRequest request,
        CancellationToken cancellationToken = default) => inventory.ReserveAsync(tenantId, actorId,
        new InventoryReservationRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, null,
            request.Quantity, "sales", request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey,
            request.ExpiresAt), cancellationToken);

    public Task ReleaseAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId,
        CancellationToken cancellationToken = default) => inventory.ReleaseReservationAsync(tenantId, actorId,
        legalEntityId, reservationId, cancellationToken);

    public async Task<SalesInventoryIssueResult> IssueAsync(Guid tenantId, Guid actorId, SalesInventoryIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var operationId = await inventory.ConsumeReservationAsync(tenantId, actorId, request.ReservationId,
            new InventoryMovementRequest(request.ProductVariantId, request.LegalEntityId, request.WarehouseId, null,
                request.Quantity, request.UnitOfMeasureId, 1m, "sales", request.SourceDocumentId, request.IdempotencyKey,
                request.OperationalDate, request.PostingDate), cancellationToken);
        return new SalesInventoryIssueResult(operationId, request.Quantity);
    }

    public Task ReverseAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId, DateOnly operationalDate,
        DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default) => inventory.ReverseOperationAsync(
            tenantId, actorId, legalEntityId, operationId, operationalDate, postingDate, idempotencyKey, cancellationToken);
}
