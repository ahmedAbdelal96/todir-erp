using Modules.Directory.Entities;
using Shared.Primitives.Identifiers;
using Xunit;

namespace DirectoryTests;

public sealed class PartyDomainTests
{
    [Fact]
    public void PersonPartyStoresPersonIdentityAndStartsActive()
    {
        var party = Party.Create(
            TenantId.New(),
            PartyType.Person,
            "أحمد علي",
            legalName: null,
            tradeName: null,
            nameAr: "أحمد علي",
            nameEn: "Ahmed Ali",
            firstName: "أحمد",
            middleName: null,
            lastName: "علي",
            DateTimeOffset.UtcNow);

        Assert.Equal(PartyType.Person, party.PartyType);
        Assert.Equal(PartyStatus.Active, party.Status);
        Assert.Equal("أحمد علي", party.DisplayName);
        Assert.Equal("أحمد", party.FirstName);
        Assert.Equal("علي", party.LastName);
    }

    [Fact]
    public void PartyCanCarryCustomerAndSupplierRolesWithoutDuplicatingIdentity()
    {
        var party = Party.Create(TenantId.New(), PartyType.Organization, "شركة النور", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        party.EnableRole(PartyRoleCode.Customer, DateTimeOffset.UtcNow);
        party.EnableRole(PartyRoleCode.Supplier, DateTimeOffset.UtcNow);

        Assert.Equal(2, party.Roles.Count);
        Assert.Contains(party.Roles, role => role.Code == PartyRoleCode.Customer);
        Assert.Contains(party.Roles, role => role.Code == PartyRoleCode.Supplier);
    }

    [Fact]
    public void PartyLifecycleUsesArchiveAndRestoreInsteadOfDestructiveDelete()
    {
        var party = Party.Create(TenantId.New(), PartyType.Organization, "شركة النور", null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        party.Archive(DateTimeOffset.UtcNow);
        Assert.Equal(PartyStatus.Archived, party.Status);
        Assert.Throws<InvalidOperationException>(() => party.Archive(DateTimeOffset.UtcNow));

        party.Restore(DateTimeOffset.UtcNow);
        Assert.Equal(PartyStatus.Active, party.Status);
    }

    [Fact]
    public void PartyRejectsBlankDisplayName()
    {
        Assert.Throws<ArgumentException>(() => Party.Create(TenantId.New(), PartyType.Organization, " ", null, null, null, null, null, null, null, DateTimeOffset.UtcNow));
    }
}
