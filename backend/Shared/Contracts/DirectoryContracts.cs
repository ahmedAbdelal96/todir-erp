using Shared.Primitives.Identifiers;

namespace Shared.Contracts.Directory;

/// <summary>Tenant and legal-entity scoped party validation for module consumers.</summary>
public interface IDirectoryCapabilities
{
    Task<bool> IsActivePartyAsync(TenantId tenantId, Guid partyId, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> FindActivePartiesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<InvoicePartySnapshotContract?> ReadInvoiceSnapshotAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default);
}

/// <summary>Role checks for financial workflows that need a customer or supplier capability.</summary>
public interface IDirectoryPartyRoleCapabilities
{
    Task<bool> HasActiveRoleAsync(TenantId tenantId, Guid partyId, Guid legalEntityId, string roleCode, CancellationToken cancellationToken = default);
}

public sealed record DirectoryPartyCreateContract(string PartyType, string DisplayName, string? LegalName, string? CountryCode, string? PrimaryLanguage);
public interface IDirectoryPartyWriteCapabilities
{
    Task<Guid> CreateCustomerPartyAsync(TenantId tenantId, Guid actorId, Guid membershipId, DirectoryPartyCreateContract request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task EnsureCustomerRoleAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid partyId, Guid legalEntityId, CancellationToken cancellationToken = default);
}

public sealed record InvoicePartyIdentifierContract(string Type, string Value, string? CountryCode, string? Authority);
public sealed record InvoicePartyAddressContract(Guid AddressId, string Usage, string CountryCode, string? City, string? Region, string? PostalCode, string Line1, string? Line2, bool IsDefault);
public sealed record InvoicePartySnapshotContract(Guid PartyId, string DisplayName, string? LegalName, string? TradeName, string? CountryCode,
    IReadOnlyList<InvoicePartyIdentifierContract> TaxIdentifiers, IReadOnlyList<InvoicePartyAddressContract> Addresses, IReadOnlyList<string> ActiveRoles);

/// <summary>Exact, non-fuzzy external reference resolution for controlled downstream imports.</summary>
public interface IDirectoryImportCapabilities
{
    Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> ResolveExternalPartyReferencesAsync(TenantId tenantId, IReadOnlyCollection<string> references, Guid? legalEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> FindStablePartyReferencesAsync(TenantId tenantId, IReadOnlyCollection<Guid> partyIds, Guid? legalEntityId, CancellationToken cancellationToken = default);
}
