using System.Text.Json;
using Modules.Directory.Entities;

namespace Modules.Directory.DTOs;

public sealed record CreatePartyRequest(
    PartyType PartyType,
    string DisplayName,
    string? LegalName,
    string? TradeName,
    string? NameAr,
    string? NameEn,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? CountryCode,
    string? PrimaryLanguage,
    IReadOnlyList<PartyRoleCode>? Roles = null,
    IReadOnlyList<IdentifierInput>? Identifiers = null,
    IReadOnlyList<ContactPointInput>? ContactPoints = null,
    IReadOnlyList<AddressInput>? Addresses = null);

public sealed record UpdatePartyRequest(
    string DisplayName,
    string? LegalName,
    string? TradeName,
    string? NameAr,
    string? NameEn,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? CountryCode,
    string? PrimaryLanguage,
    long Version);

public sealed record AddRoleRequest(PartyRoleCode Role, long Version);
public sealed record IdentifierInput(string Type, string Value, string? CountryCode, string? Authority, bool IsPrimary = false);
public sealed record ContactPointInput(string Type, string Value, string? Label, bool IsPrimary = false);
public sealed record AddressInput(string Usage, string CountryCode, string? City, string? Region, string? PostalCode, string Line1, string? Line2, bool IsDefault = false);
public sealed record ContactPersonInput(string DisplayName, string? FirstName = null, string? LastName = null, string? JobTitle = null, string? Department = null, string? LanguageCode = null);
public sealed record ContactPersonLinkInput(Guid ContactPersonId, string Usage = "General", bool IsPrimary = false);
public sealed record AddContactPersonRequest(ContactPersonInput Person, ContactPersonLinkInput Link);
public sealed record AliasInput(string Alias, string? LanguageCode = null, string AliasType = "Alternate");
public sealed record VisibilityInput(string ScopeType, Guid? LegalEntityId = null);
public sealed record MergePartyRequest(Guid MasterPartyId, string Reason, bool ConfirmIrreversible = false);
public sealed record DirectoryImportRow(
    string ExternalReference,
    PartyType PartyType,
    string DisplayName,
    string? CountryCode = null,
    string? NameAr = null,
    string? NameEn = null,
    string? LegalName = null,
    string? TradeName = null,
    string? Role = null,
    string? IdentifierType = null,
    string? IdentifierValue = null,
    string? IdentifierCountryCode = null,
    string? Email = null,
    string? Phone = null,
    string? AddressUsage = null,
    string? AddressLine1 = null,
    string? City = null,
    string? Region = null,
    string? PostalCode = null);
public sealed record DirectoryImportRequest(IReadOnlyList<DirectoryImportRow> Rows, bool Commit = false);
public sealed record DirectoryImportRowResult(string ExternalReference, string Action, Guid? PartyId, IReadOnlyList<string> Errors, IReadOnlyList<Guid> PossibleMatches);
public sealed record DirectoryImportResult(Guid OperationId, bool Committed, IReadOnlyList<DirectoryImportRowResult> Rows);
public sealed record DirectoryMergeResult(Guid MasterPartyId, Guid SourcePartyId, string Status, IReadOnlyList<string> Contributors);
public sealed record AuditChangeDto(string Field, JsonElement? BeforeValue, JsonElement? AfterValue);
public sealed record AuditEntryDto(Guid Id, string Action, string EntityType, Guid EntityId, string? Reason, DateTimeOffset OccurredAt, string? BeforeJson, string? AfterJson, Guid? ActorId, string ActorType, string? ActorName, IReadOnlyList<AuditChangeDto> Changes);
public sealed record ReadinessCapabilityDto(string Capability, bool Ready, IReadOnlyList<string> Missing, IReadOnlyList<string> Warnings);

public sealed record PartyRoleDto(string Code, string Status, long Version);
public sealed record PartyIdentifierDto(Guid Id, string Type, string? CountryCode, string Value, string? Authority, bool IsPrimary, string VerificationStatus);
public sealed record ContactPointDto(Guid Id, string Type, string Value, string? Label, bool IsPrimary);
public sealed record AddressDto(Guid Id, string Usage, string CountryCode, string? City, string? Region, string? PostalCode, string Line1, string? Line2, bool IsDefault);
public sealed record ContactPersonDto(Guid Id, string DisplayName, string? JobTitle, string? Department, string Status, IReadOnlyList<string> Usages);
public sealed record PartyAliasDto(Guid Id, string Alias, string? LanguageCode, string AliasType);
public sealed record PartyVisibilityDto(Guid Id, string ScopeType, Guid? LegalEntityId);
public sealed record PartySummary(Guid Id, PartyType PartyType, PartyStatus Status, string DisplayName, string? LegalName, IReadOnlyList<PartyRoleDto> Roles, DateTimeOffset UpdatedAt, long Version);
public sealed record PartyDetails(
    Guid Id, PartyType PartyType, PartyStatus Status, string DisplayName, string? LegalName, string? TradeName,
    string? NameAr, string? NameEn, string? FirstName, string? MiddleName, string? LastName, string? CountryCode,
    string? PrimaryLanguage, IReadOnlyList<PartyRoleDto> Roles, IReadOnlyList<PartyIdentifierDto> Identifiers,
    IReadOnlyList<ContactPointDto> ContactPoints, IReadOnlyList<AddressDto> Addresses, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, long Version, IReadOnlyList<ContactPersonDto>? ContactPersons = null,
    IReadOnlyList<PartyAliasDto>? Aliases = null, IReadOnlyList<PartyVisibilityDto>? Visibility = null);
public sealed record PagedParties(IReadOnlyList<PartySummary> Items, int TotalCount, int Page, int PageSize);
public sealed record DuplicateCandidateDto(Guid Id, Guid CandidatePartyId, string Reason, decimal Confidence, string Status);
public sealed record PartyReadinessDto(Guid PartyId, bool Ready, IReadOnlyList<string> Missing, IReadOnlyList<string> Warnings, IReadOnlyList<ReadinessCapabilityDto>? Capabilities = null);

// Public Directory contracts allow other modules to depend on facts without reaching
// Directory tables, repositories, or entities. Consumers are optional until implemented.
public interface IPartyMergeContributor
{
    string Name { get; }
    Task<IReadOnlyList<string>> ValidateAsync(Guid tenantId, Guid sourcePartyId, Guid masterPartyId, CancellationToken cancellationToken = default);
    Task ContributeAsync(Guid tenantId, Guid sourcePartyId, Guid masterPartyId, CancellationToken cancellationToken = default);
}

public interface IPartyReadinessRule
{
    string Capability { get; }
    Task<ReadinessCapabilityDto> EvaluateAsync(Guid tenantId, PartyDetails party, CancellationToken cancellationToken = default);
}

public interface IDirectoryPartyQueries
{
    Task<PartyDetails?> GetPartyAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken = default);
    Task<PagedParties> SearchAsync(Guid tenantId, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PartyReadinessDto?> GetReadinessAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken = default);
}
public sealed class DirectoryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
