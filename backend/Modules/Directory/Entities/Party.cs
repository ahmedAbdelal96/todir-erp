using Shared.Primitives.Identifiers;

namespace Modules.Directory.Entities;

public enum PartyType { Person, Organization }
public enum PartyStatus { Draft, Active, Blocked, Archived, Merged }
public enum PartyRoleCode { Customer, Supplier }
public enum PartyRoleStatus { Active, Inactive }

public sealed record PartyRole(PartyRoleCode Code, PartyRoleStatus Status, DateTimeOffset ActiveFrom, DateTimeOffset? ActiveTo);

public sealed class Party
{
    private readonly List<PartyRole> roles = [];

    private Party(
        TenantId tenantId,
        Guid id,
        PartyType partyType,
        string displayName,
        string? legalName,
        string? tradeName,
        string? nameAr,
        string? nameEn,
        string? firstName,
        string? middleName,
        string? lastName,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        Id = id;
        PartyType = partyType;
        DisplayName = displayName;
        LegalName = legalName;
        TradeName = tradeName;
        NameAr = nameAr;
        NameEn = nameEn;
        FirstName = firstName;
        MiddleName = middleName;
        LastName = lastName;
        Status = PartyStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Version = 1;
    }

    public TenantId TenantId { get; }
    public Guid Id { get; }
    public PartyType PartyType { get; }
    public string DisplayName { get; private set; }
    public string? LegalName { get; private set; }
    public string? TradeName { get; private set; }
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string? FirstName { get; private set; }
    public string? MiddleName { get; private set; }
    public string? LastName { get; private set; }
    public PartyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<PartyRole> Roles => roles.AsReadOnly();

    public static Party Create(
        TenantId tenantId,
        PartyType partyType,
        string displayName,
        string? legalName,
        string? tradeName,
        string? nameAr,
        string? nameEn,
        string? firstName,
        string? middleName,
        string? lastName,
        DateTimeOffset now)
    {
        if (tenantId.Value == Guid.Empty) throw new ArgumentException("directory.tenant_required", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("directory.display_name_required", nameof(displayName));
        if (displayName.Trim().Length > 300) throw new ArgumentException("directory.display_name_too_long", nameof(displayName));
        if (partyType == PartyType.Organization && (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(middleName) || !string.IsNullOrWhiteSpace(lastName)))
            throw new ArgumentException("directory.organization_person_name_fields_invalid", nameof(firstName));
        return new Party(tenantId, Guid.CreateVersion7(), partyType, displayName.Trim(), CleanOptional(legalName), CleanOptional(tradeName), CleanOptional(nameAr), CleanOptional(nameEn), CleanOptional(firstName), CleanOptional(middleName), CleanOptional(lastName), now);
    }

    public void UpdateIdentity(
        string displayName,
        string? legalName,
        string? tradeName,
        string? nameAr,
        string? nameEn,
        string? firstName,
        string? middleName,
        string? lastName,
        DateTimeOffset now)
    {
        if (Status is PartyStatus.Archived or PartyStatus.Merged) throw new InvalidOperationException("directory.party_not_editable");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("directory.display_name_required", nameof(displayName));
        if (PartyType == PartyType.Organization && (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(middleName) || !string.IsNullOrWhiteSpace(lastName)))
            throw new ArgumentException("directory.organization_person_name_fields_invalid", nameof(firstName));
        DisplayName = displayName.Trim();
        LegalName = CleanOptional(legalName);
        TradeName = CleanOptional(tradeName);
        NameAr = CleanOptional(nameAr);
        NameEn = CleanOptional(nameEn);
        FirstName = CleanOptional(firstName);
        MiddleName = CleanOptional(middleName);
        LastName = CleanOptional(lastName);
        Touch(now);
    }

    public void EnableRole(PartyRoleCode code, DateTimeOffset now)
    {
        if (Status is PartyStatus.Archived or PartyStatus.Merged) throw new InvalidOperationException("directory.party_not_editable");
        if (roles.Any(role => role.Code == code && role.Status == PartyRoleStatus.Active)) return;
        roles.RemoveAll(role => role.Code == code);
        roles.Add(new PartyRole(code, PartyRoleStatus.Active, now, null));
        Touch(now);
    }

    public void DisableRole(PartyRoleCode code, DateTimeOffset now)
    {
        var role = roles.SingleOrDefault(item => item.Code == code && item.Status == PartyRoleStatus.Active);
        if (role is null) return;
        roles.Remove(role);
        roles.Add(role with { Status = PartyRoleStatus.Inactive, ActiveTo = now });
        Touch(now);
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status != PartyStatus.Active) throw new InvalidOperationException("directory.party_not_active");
        Status = PartyStatus.Archived;
        Touch(now);
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status != PartyStatus.Archived) throw new InvalidOperationException("directory.party_not_archived");
        Status = PartyStatus.Active;
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
