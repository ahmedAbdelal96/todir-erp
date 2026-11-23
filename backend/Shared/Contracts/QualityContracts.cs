using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Quality;

/// <summary>Quality owns inspection decisions; Manufacturing may only persist a verified snapshot.</summary>
public sealed record QualityDecisionSnapshot(Guid ProductionOrderId, Guid? ProductionOperationId,
    bool CapabilityAvailable, string DecisionState, string EvidenceReference);

public interface IQualityDecisionCapabilities
{
    Task<QualityDecisionSnapshot?> GetProductionDecisionAsync(TenantId tenantId, Guid legalEntityId,
        Guid productionOrderId, Guid? productionOperationId, CancellationToken cancellationToken = default);
}

public sealed record QualityReceiptLine(Guid PurchaseOrderLineId, Guid ProductVariantId, decimal Quantity,
    Guid? LotId, Guid? SerialId);

public sealed record QualityReceiptResolution(Guid PurchaseOrderId, Guid LegalEntityId, Guid SupplierPartyId,
    IReadOnlyList<QualityReceiptLine> Lines, bool QualityRequired, string InitialStockStatus,
    string? RequirementReason, IReadOnlyList<Guid> InspectionIds,
    IReadOnlyDictionary<Guid, string>? InitialStockStatuses = null);

/// <summary>Quality resolves mandatory receiving controls before Inventory commits a receipt.</summary>
public interface IQualityReceivingCapabilities
{
    Task<QualityReceiptResolution> ResolvePurchaseReceiptAsync(Guid tenantId, Guid actorId,
        Guid legalEntityId, Guid purchaseOrderId, Guid supplierPartyId,
        string idempotencyKey, IReadOnlyList<QualityReceiptLine> lines, CancellationToken cancellationToken = default);
}

public sealed record InventoryQualityStatusRequest(Guid LegalEntityId, Guid ProductVariantId, Guid WarehouseId,
    Guid? LocationId, Guid UnitOfMeasureId, decimal Quantity, string FromStatus, string ToStatus,
    DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey, Guid? LotId = null, Guid? SerialId = null);

public sealed record InventoryQualityStatusResult(Guid OperationId, string Status, bool IdempotentReplay);

/// <summary>Inventory remains the owner of stock status and physical availability.</summary>
public interface IInventoryQualityCapabilities
{
    Task<InventoryQualityStatusResult> ChangeQualityStatusAsync(Guid tenantId, Guid actorId,
        InventoryQualityStatusRequest request, CancellationToken cancellationToken = default);
}
