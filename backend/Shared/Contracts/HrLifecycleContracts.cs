using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Hr;

public interface IHumanResourcesLifecycleCapability
{
    Task<bool> ShouldDeactivateMembershipAsync(TenantId tenantId, Guid employeeId, Guid membershipId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
