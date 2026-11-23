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
    string? RequirementReason, IReadOnlyList<Guid> InspectionIds);

/// <summary>Quality resolves mandatory receiving controls before Inventory commits a receipt.</summary>
public interface IQualityReceivingCapabilities
{
    Task<QualityReceiptResolution> ResolvePurchaseReceiptAsync(Guid tenantId, Guid actorId,
        Guid legalEntityId, Guid purchaseOrderId, Guid supplierPartyId,
        string idempotencyKey, IReadOnlyList<QualityReceiptLine> lines, CancellationToken cancellationToken = default);
}
