using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Maintenance;

public sealed record MaintenanceDowntimeInterval(Guid SubjectId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

/// <summary>Read-only maintenance truth used by capacity consumers.</summary>
public interface IMaintenanceDowntimeCapabilities
{
    Task<IReadOnlyList<MaintenanceDowntimeInterval>> ListDowntimeAsync(TenantId tenantId, Guid legalEntityId, IReadOnlyList<Guid> subjectIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
