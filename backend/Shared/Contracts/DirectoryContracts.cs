using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Directory;

/// <summary>Tenant and legal-entity scoped party validation for module consumers.</summary>
public interface IDirectoryCapabilities
{
    Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
}
