using Shared.Contracts.Directory;
using Shared.Primitives.Identifiers;

namespace Modules.Directory.Services;

public sealed class DirectoryCapabilities(DirectoryService directory) : IDirectoryCapabilities, IDirectoryImportCapabilities, IDirectoryPartyRoleCapabilities, IDirectoryPartyWriteCapabilities
{
    public Task<bool> HasActiveRoleAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, string roleCode, CancellationToken cancellationToken = default)
        => directory.HasActiveRoleAsync(tenantId, partyId, legalEntityId, roleCode, cancellationToken);

    public async Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        var party = await directory.GetVisibleAsync(tenantId, partyId, legalEntityId, cancellationToken);
        return party?.Status == Entities.PartyStatus.Active;
    }

    public Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => directory.FindActivePartiesAsync(tenantId, partyIds, legalEntityId, cancellationToken);

    public Task<InvoicePartySnapshotContract?> ReadInvoiceSnapshotAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default)
        => directory.ReadInvoiceSnapshotAsync(tenantId, partyId, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> ResolveExternalPartyReferencesAsync(TenantId tenantId, IReadOnlyCollection<string> references, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => directory.ResolveExternalPartyReferencesAsync(tenantId, references, legalEntityId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> FindStablePartyReferencesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default)
        => directory.FindStablePartyReferencesAsync(tenantId, partyIds, legalEntityId, cancellationToken);

    public async Task<Guid> CreateCustomerPartyAsync(TenantId tenantId, Guid actorId, Guid membershipId, DirectoryPartyCreateContract request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var type = Enum.TryParse<Entities.PartyType>(request.PartyType, true, out var partyType) ? partyType : throw new InvalidOperationException("directory.party_type_invalid");
        var party = await directory.CreateAsync(tenantId, actorId, membershipId, new Modules.Directory.DTOs.CreatePartyRequest(type, request.DisplayName, request.LegalName, null, null, null, null, null, null, request.CountryCode, request.PrimaryLanguage, [Entities.PartyRoleCode.Customer]), Guid.CreateVersion7(), null, false, idempotencyKey, cancellationToken);
        return party.Id;
    }

    public async Task EnsureCustomerRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default)
    {
        var party = await directory.GetVisibleAsync(tenantId, partyId, legalEntityId, cancellationToken) ?? throw new InvalidOperationException("directory.party_not_found");
        if (party.Roles.Any(role => string.Equals(role.Code.ToString(), "Customer", StringComparison.OrdinalIgnoreCase) && string.Equals(role.Status, "Active", StringComparison.OrdinalIgnoreCase))) return;
        var version = party.Version;
        await directory.AddRoleAsync(tenantId, actorId, membershipId, partyId, new Modules.Directory.DTOs.AddRoleRequest(Entities.PartyRoleCode.Customer, version), Guid.CreateVersion7(), null, cancellationToken);
    }
}
