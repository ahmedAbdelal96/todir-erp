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
