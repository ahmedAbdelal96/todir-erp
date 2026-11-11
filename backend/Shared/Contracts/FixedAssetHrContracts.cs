using Shared.Primitives.Identifiers;

namespace Shared.Contracts.FixedAssets;

public interface IHumanResourcesFixedAssetCapability
{
    Task<bool> IsOffboardingEffectiveAsync(TenantId tenantId, Guid employeeId, Guid legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
