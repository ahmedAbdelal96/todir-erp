using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Directory.Services;

public sealed class DirectoryService(Repositories.DirectoryRepository repository)
{
    public Task<PartyDetails> CreateAsync(TenantId tenantId, Guid actorId, Guid membershipId, CreatePartyRequest request, Guid correlationId, string? traceParent, bool allowDuplicate, CancellationToken cancellationToken) => repository.CreateAsync(tenantId, actorId, membershipId, request, correlationId, traceParent, allowDuplicate, cancellationToken);
    public Task<PartyDetails> UpdateAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, UpdatePartyRequest request, Guid correlationId, string? traceParent, CancellationToken cancellationToken) => repository.UpdateAsync(tenantId, actorId, membershipId, partyId, request, correlationId, traceParent, cancellationToken);
    public Task<PartyDetails> ChangeStatusAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, bool restore, long version, Guid correlationId, string? traceParent, CancellationToken cancellationToken) => repository.ChangeStatusAsync(tenantId, actorId, membershipId, partyId, restore, version, correlationId, traceParent, cancellationToken);
    public Task<PartyDetails> AddRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, AddRoleRequest request, Guid correlationId, string? traceParent, CancellationToken cancellationToken) => repository.AddRoleAsync(tenantId, actorId, membershipId, partyId, request, correlationId, traceParent, cancellationToken);
    public Task<PartyDetails> AddIdentifierAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, IdentifierInput input, Guid correlationId, CancellationToken cancellationToken) => repository.AddIdentifierAsync(tenantId, actorId, membershipId, partyId, input, correlationId, cancellationToken);
    public Task<PartyDetails> AddContactPointAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, ContactPointInput input, Guid correlationId, CancellationToken cancellationToken) => repository.AddContactPointAsync(tenantId, actorId, membershipId, partyId, input, correlationId, cancellationToken);
    public Task<PartyDetails> AddAddressAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, AddressInput input, Guid correlationId, CancellationToken cancellationToken) => repository.AddAddressAsync(tenantId, actorId, membershipId, partyId, input, correlationId, cancellationToken);
    public Task<PartyDetails> DisableRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, PartyRoleCode role, Guid correlationId, CancellationToken cancellationToken) => repository.DisableRoleAsync(tenantId, actorId, membershipId, partyId, role, correlationId, cancellationToken);
    public Task<PagedParties> ListAsync(TenantId tenantId, string? search, PartyType? partyType, PartyRoleCode? role, PartyStatus? status, int page, int pageSize, CancellationToken cancellationToken) => repository.ListAsync(tenantId, search, partyType, role, status, page, pageSize, cancellationToken);
    public Task<PartyDetails?> GetAsync(TenantId tenantId, Guid partyId, bool includeIdentifiers, CancellationToken cancellationToken) => repository.GetAsync(tenantId, partyId, includeIdentifiers, cancellationToken);
    public Task<IReadOnlyList<DuplicateCandidateDto>> DuplicatesAsync(TenantId tenantId, Guid partyId, CancellationToken cancellationToken) => repository.DuplicatesAsync(tenantId, partyId, cancellationToken);
    public Task<PartyReadinessDto?> ReadinessAsync(TenantId tenantId, Guid partyId, CancellationToken cancellationToken) => repository.ReadinessAsync(tenantId, partyId, cancellationToken);
}
