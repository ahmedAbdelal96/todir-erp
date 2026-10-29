namespace Todir.Sales.Contracts;

public sealed record SalesInventoryAvailabilityRequest(
    Guid LegalEntityId,
    IReadOnlyList<Guid> ProductVariantIds,
    Guid? WarehouseId,
    bool IncludeReservations = true);

public sealed record SalesInventoryAvailabilityLine(
    Guid ProductVariantId,
    Guid WarehouseId,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    decimal InTransit,
    string BaseUnitCode);

public sealed record SalesInventoryReservationRequest(
    Guid LegalEntityId,
    Guid ProductVariantId,
    Guid WarehouseId,
    decimal Quantity,
    Guid SourceDocumentId,
    Guid SourceLineId,
    string IdempotencyKey,
    DateTimeOffset? ExpiresAt = null);

public sealed record SalesInventoryIssueRequest(
    Guid LegalEntityId,
    Guid ReservationId,
    Guid ProductVariantId,
    Guid UnitOfMeasureId,
    Guid WarehouseId,
    decimal Quantity,
    Guid SourceDocumentId,
    Guid SourceLineId,
    string IdempotencyKey,
    DateOnly OperationalDate,
    DateOnly PostingDate);

public sealed record SalesInventoryIssueResult(Guid OperationId, decimal FulfilledQuantity);

public sealed record SalesInventoryReturnRequest(
    Guid LegalEntityId,
    Guid ProductVariantId,
    Guid WarehouseId,
    Guid? LocationId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    Guid SourceDocumentId,
    Guid SourceLineId,
    string IdempotencyKey,
    DateOnly OperationalDate,
    DateOnly PostingDate,
    string StockStatus = "Available",
    Guid? LotId = null,
    Guid? SerialId = null,
    decimal? UnitCost = null,
    string? CurrencyCode = null);

/// <summary>
/// Sales-facing capability boundary. Sales stores only references returned by Inventory;
/// it never reads or writes Inventory tables.
/// </summary>
public interface ISalesInventoryCapabilities
{
    Task<IReadOnlyList<SalesInventoryAvailabilityLine>> GetAvailabilityAsync(
        Guid tenantId, SalesInventoryAvailabilityRequest request, CancellationToken cancellationToken = default);

    Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, SalesInventoryReservationRequest request,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<SalesInventoryIssueResult> IssueAsync(Guid tenantId, Guid actorId, SalesInventoryIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, SalesInventoryReturnRequest request,
        CancellationToken cancellationToken = default);

    Task ReverseAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId, DateOnly operationalDate,
        DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default);
}
