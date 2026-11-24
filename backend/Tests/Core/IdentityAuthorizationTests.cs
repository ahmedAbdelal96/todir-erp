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
        var membershipId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [new MembershipRoleAssignment(tenant, membershipId, role.Id, PermissionScope.ForLegalEntity(tenant, entity))],
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

    [Fact]
    public void InactiveRolePermissionCannotBeActivatedByDifferentActiveRole()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var activeRole = new Role(Guid.NewGuid(), tenant, "reader", "Reader", false, true, 1);
        var inactiveRole = new Role(Guid.NewGuid(), tenant, "former-admin", "Former Admin", false, false, 2);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [
                new(tenant, membershipId, activeRole.Id, PermissionScope.ForTenant(tenant)),
                new(tenant, membershipId, inactiveRole.Id, PermissionScope.ForTenant(tenant))
            ],
            [activeRole, inactiveRole],
            [
                new(tenant, activeRole.Id, "reports.read"),
                new(tenant, inactiveRole.Id, "payments.reverse")
            ],
            2);

        Assert.False(new PermissionAuthorizer().Authorize(subject, "payments.reverse", PermissionScope.ForTenant(tenant)));
    }

    [Fact]
    public void ActiveRoleWithPermissionIsAllowedAndInactiveRoleAloneIsDenied()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var activeRole = new Role(Guid.NewGuid(), tenant, "reader", "Reader", false, true, 1);
        var inactiveRole = new Role(Guid.NewGuid(), tenant, "inactive-reader", "Inactive Reader", false, false, 2);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [
                new(tenant, membershipId, activeRole.Id, PermissionScope.ForTenant(tenant)),
                new(tenant, membershipId, inactiveRole.Id, PermissionScope.ForTenant(tenant))
            ],
            [activeRole, inactiveRole],
            [
                new(tenant, activeRole.Id, "reports.read"),
                new(tenant, inactiveRole.Id, "payments.reverse")
            ],
            2);

        var authorizer = new PermissionAuthorizer();
        Assert.True(authorizer.Authorize(subject, "reports.read", PermissionScope.ForTenant(tenant)));
        Assert.False(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForTenant(tenant)));
    }

    [Fact]
    public void AnyMatchingActiveRoleMayGrantButInactiveAdministratorCannotEscalate()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var firstRole = new Role(Guid.NewGuid(), tenant, "reader", "Reader", false, true, 1);
        var secondRole = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var inactiveAdmin = new Role(Guid.NewGuid(), tenant, "administrator", "Inactive Administrator", false, false, 2);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [
                new(tenant, membershipId, firstRole.Id, PermissionScope.ForTenant(tenant)),
                new(tenant, membershipId, secondRole.Id, PermissionScope.ForTenant(tenant)),
                new(tenant, membershipId, inactiveAdmin.Id, PermissionScope.ForTenant(tenant))
            ],
            [firstRole, secondRole, inactiveAdmin],
            [
                new(tenant, firstRole.Id, "reports.read"),
                new(tenant, secondRole.Id, "payments.reverse"),
                new(tenant, inactiveAdmin.Id, "*")
            ],
            2);

        var authorizer = new PermissionAuthorizer();
        Assert.True(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForTenant(tenant)));
        Assert.False(authorizer.Authorize(subject, "platform.identity.manage", PermissionScope.ForTenant(tenant)));
    }

    [Fact]
    public void DeactivatedRoleIsDeniedOnTheNextAuthorizationDecision()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [new(tenant, membershipId, role.Id, PermissionScope.ForTenant(tenant))],
            [role], [new(tenant, role.Id, "payments.reverse")], 1);

        var authorizer = new PermissionAuthorizer();
        Assert.True(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForTenant(tenant)));
        var deactivated = subject with { Roles = [role with { IsActive = false }] };
        Assert.False(authorizer.Authorize(deactivated, "payments.reverse", PermissionScope.ForTenant(tenant)));
    }

    [Fact]
    public void AssignmentMustBelongToCurrentMembershipAndExactEntityOrBranchScope()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var otherMembershipId = Guid.NewGuid();
        var entity = LegalEntityId.New();
        var otherEntity = LegalEntityId.New();
        var branch = BranchId.New();
        var otherBranch = BranchId.New();
        var role = new Role(Guid.NewGuid(), tenant, "operator", "Operator", false, true, 1);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [
                new(tenant, otherMembershipId, role.Id, PermissionScope.ForTenant(tenant)),
                new(tenant, membershipId, role.Id, PermissionScope.ForBranch(tenant, entity, branch))
            ],
            [role], [new(tenant, role.Id, "payments.reverse")], 1);

        var authorizer = new PermissionAuthorizer();
        Assert.True(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForBranch(tenant, entity, branch)));
        Assert.False(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForLegalEntity(tenant, entity)));
        Assert.False(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForBranch(tenant, entity, otherBranch)));
        Assert.False(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForLegalEntity(tenant, otherEntity)));
    }

    [Fact]
    public void MissingPermissionAndWildcardRequestsFailClosed()
    {
        var tenant = TenantId.New();
        var membershipId = Guid.NewGuid();
        var role = new Role(Guid.NewGuid(), tenant, "reader", "Reader", false, true, 1);
        var subject = new AuthorizationSubject(tenant, membershipId,
            [new(tenant, membershipId, role.Id, PermissionScope.ForTenant(tenant))],
            [role], [new(tenant, role.Id, "reports.read")], 1);

        var authorizer = new PermissionAuthorizer();
        Assert.False(authorizer.Authorize(subject, "payments.reverse", PermissionScope.ForTenant(tenant)));
        Assert.False(authorizer.Authorize(subject, "*", PermissionScope.ForTenant(tenant)));
        Assert.False(authorizer.Authorize(subject, "admin", PermissionScope.ForTenant(tenant)));
    }
}
