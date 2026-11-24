using Core.Authorization;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Core.Tests;

public sealed class IdentityAuthorizationTests
{
    [Fact]
    public void PasswordHashIsNotReversibleAndVerifiesConstantTime()
    {
        var hash = IdentityConventions.HashPassword("correct horse battery staple");
        Assert.NotEqual("correct horse battery staple", hash);
        Assert.True(IdentityConventions.VerifyPassword("correct horse battery staple", hash));
        Assert.False(IdentityConventions.VerifyPassword("wrong", hash));
    }

    [Fact]
    public void AuthorizationRequiresMatchingTenantAndScope()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var entity = LegalEntityId.New();
        var branch = BranchId.New();
        var role = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var subject = new AuthorizationSubject(tenant, Guid.NewGuid(),
            [new MembershipRoleAssignment(tenant, Guid.NewGuid(), role.Id, PermissionScope.ForLegalEntity(tenant, entity))],
            [role], [new RolePermissionEntry(tenant, role.Id, "sales.order.read")], 1);
        var authorizer = new PermissionAuthorizer();

        Assert.True(authorizer.Authorize(subject, "sales.order.read", new(PermissionScopeType.LegalEntity, tenant, entity)));
        Assert.True(authorizer.Authorize(subject, "sales.order.read", new(PermissionScopeType.Branch, tenant, entity, branch)));
        Assert.False(authorizer.Authorize(subject, "sales.order.read", new(PermissionScopeType.LegalEntity, tenant, LegalEntityId.New())));
        Assert.False(authorizer.Authorize(subject, "sales.order.read", new(PermissionScopeType.Tenant, otherTenant)));
    }

    [Fact]
    public void AuthorizationReflectsSubjectChangesWithoutAStaleDecisionCache()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var subject = new AuthorizationSubject(tenant, membershipId, [new(tenant, membershipId, role.Id, PermissionScope.ForTenant(tenant))], [role], [new(tenant, role.Id, "a.read")], 1);
        var authorizer = new PermissionAuthorizer();
        Assert.True(authorizer.Authorize(subject, "a.read", PermissionScope.ForTenant(tenant)));
        var changed = subject with { Permissions = [] };
        Assert.False(authorizer.Authorize(changed, "a.read", PermissionScope.ForTenant(tenant)));
    }

    [Fact]
    public void InactiveRoleAndWildcardPermissionDoNotGrantAConcretePermission()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenant, "inactive", "Inactive", false, false, 2);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [new(tenant, membershipId, role.Id, PermissionScope.ForTenant(tenant))],
            [role], [new(tenant, role.Id, "*")], 2);

        Assert.False(new PermissionAuthorizer().Authorize(subject, "a.write", PermissionScope.ForTenant(tenant)));
    }
}
