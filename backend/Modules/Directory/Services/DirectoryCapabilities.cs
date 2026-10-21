using Shared.Contracts.Directory;
using Shared.Primitives.Identifiers;

namespace Modules.Directory.Services;

public sealed class DirectoryCapabilities(DirectoryService directory) : IDirectoryCapabilities
{
    public async Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        var party = await directory.GetVisibleAsync(tenantId, partyId, legalEntityId, cancellationToken);
        return party?.Status == Entities.PartyStatus.Active;
    }

    public Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => directory.FindActivePartiesAsync(tenantId, partyIds, legalEntityId, cancellationToken);
}
