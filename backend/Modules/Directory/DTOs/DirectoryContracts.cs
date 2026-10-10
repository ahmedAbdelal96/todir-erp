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
    IReadOnlyList<PartyRoleCode> Roles,
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

public sealed record PartyRoleDto(string Code, string Status, long Version);
public sealed record PartyIdentifierDto(Guid Id, string Type, string? CountryCode, string Value, string? Authority, bool IsPrimary, string VerificationStatus);
public sealed record ContactPointDto(Guid Id, string Type, string Value, string? Label, bool IsPrimary);
public sealed record AddressDto(Guid Id, string Usage, string CountryCode, string? City, string? Region, string? PostalCode, string Line1, string? Line2, bool IsDefault);
public sealed record PartySummary(Guid Id, PartyType PartyType, PartyStatus Status, string DisplayName, string? LegalName, IReadOnlyList<PartyRoleDto> Roles, DateTimeOffset UpdatedAt, long Version);
public sealed record PartyDetails(
    Guid Id, PartyType PartyType, PartyStatus Status, string DisplayName, string? LegalName, string? TradeName,
    string? NameAr, string? NameEn, string? FirstName, string? MiddleName, string? LastName, string? CountryCode,
    string? PrimaryLanguage, IReadOnlyList<PartyRoleDto> Roles, IReadOnlyList<PartyIdentifierDto> Identifiers,
    IReadOnlyList<ContactPointDto> ContactPoints, IReadOnlyList<AddressDto> Addresses, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, long Version);
public sealed record PagedParties(IReadOnlyList<PartySummary> Items, int TotalCount, int Page, int PageSize);
public sealed record DuplicateCandidateDto(Guid Id, Guid CandidatePartyId, string Reason, decimal Confidence, string Status);
public sealed record PartyReadinessDto(Guid PartyId, bool Ready, IReadOnlyList<string> Missing, IReadOnlyList<string> Warnings);
public sealed class DirectoryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
