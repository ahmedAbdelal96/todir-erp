using System.Security.Claims;
using Core.Authorization;
using Modules.Tax.Services;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Tax.Tests;

public sealed class TaxAuthorizationTests
{
    [Fact]
    public async Task MissingAuthenticationIsUnauthorized()
    {
        var (store, authorizer, _) = Setup();
        var access = await TaxAuthorization.ResolveAsync(new ClaimsPrincipal(new ClaimsIdentity()), store, authorizer, "tax.import", Guid.CreateVersion7());
        Assert.Null(access);
    }

    [Fact]
    public async Task ImportPermissionAndProfileManagePermissionAreIndependent()
    {
        var entity = Guid.CreateVersion7();
        var (store, authorizer, user) = Setup("tax.import");
        Assert.True((await TaxAuthorization.ResolveAsync(user, store, authorizer, "tax.import", entity))!.Allowed);
        Assert.False((await TaxAuthorization.ResolveAsync(user, store, authorizer, "tax.product_profile.manage", entity))!.Allowed);
    }

    [Fact]
    public async Task ExportAndRegistrationReadPermissionsAreIndependent()
    {
        var entity = Guid.CreateVersion7();
        var (store, authorizer, user) = Setup("tax.export", "tax.party_profile.read");
        Assert.True((await TaxAuthorization.ResolveAsync(user, store, authorizer, "tax.export", entity))!.Allowed);
        Assert.True((await TaxAuthorization.ResolveAsync(user, store, authorizer, "tax.party_profile.read", entity))!.Allowed);
        Assert.False((await TaxAuthorization.ResolveAsync(user, store, authorizer, "tax.registration.read", entity))!.Allowed);
        var authorized = Setup("tax.export", "tax.party_profile.read", "tax.registration.read");
        Assert.True((await TaxAuthorization.ResolveAsync(authorized.User, authorized.Store, authorized.Authorizer, "tax.registration.read", entity))!.Allowed);
    }

    private static (IAuthorizationSubjectStore Store, IPermissionAuthorizer Authorizer, ClaimsPrincipal User) Setup(params string[] permissionCodes)
    {
        var tenant = TenantId.New(); var membership = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var roleId = Guid.CreateVersion7();
        var role = new Role(roleId, tenant, "TAX_TEST", "Tax Test", false, true, 1);
        var scope = PermissionScope.ForTenant(tenant);
        var subject = new AuthorizationSubject(tenant, membership, [new(tenant, membership, roleId, scope)], [role],
            permissionCodes.Select(code => new RolePermissionEntry(tenant, roleId, code)).ToArray(), 1);
        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", tenant.Value.ToString("D")),
            new Claim("membership_id", membership.ToString("D")),
            new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))
        ], "test"));
        return (new FixedSubjectStore(subject), new PermissionAuthorizer(), claims);
    }

    private sealed class FixedSubjectStore(AuthorizationSubject subject) : IAuthorizationSubjectStore
    {
        public Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default)
            => Task.FromResult<AuthorizationSubject?>(tenantId == subject.TenantId && membershipId == subject.MembershipId ? subject : null);
    }
}
