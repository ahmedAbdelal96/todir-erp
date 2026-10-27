using Npgsql;
using Core.Authorization;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class AuthorizationIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task DatabaseSubjectAndBackendAuthorizerEnforceTenantAndBranchScopes()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the authorization integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var otherTenantId = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        var roleId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'auth-{tenantId.Value:N}', 'Auth', 'Active', 'auth-{tenantId.Value:N}.local', 'auth-{tenantId.Value:N}.local', now(), 1); set app.tenant_id = '{otherTenantId.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{otherTenantId.Value}', 'other-{otherTenantId.Value:N}', 'Other', 'Active', 'other-{otherTenantId.Value:N}.local', 'other-{otherTenantId.Value:N}.local', now(), 1); set app.tenant_id = '{tenantId.Value}'; insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ('{entityId}', '{tenantId.Value}', 'AUTH', 'Auth Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1); insert into platform.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version) values ('{branchId}', '{tenantId.Value}', '{entityId}', 'AUTH', 'Auth Branch', 'Active', now(), 1); insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, status, created_at, security_stamp, version) values ('{identityId.Value}', '{tenantId.Value}', 'auth@auth-{tenantId.Value:N}.local', 'AUTH@AUTH-{tenantId.Value:N}.LOCAL', 'auth', 'Auth User', true, 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{membershipId.Value}', '{tenantId.Value}', '{identityId.Value}', 'Auth member', 'Active', now(), 1); insert into platform.role (id, tenant_id, code, name, version) values ('{roleId}', '{tenantId.Value}', 'auth-reader', 'Auth Reader', 3); insert into platform.role_permission (tenant_id, role_id, permission_code) values ('{tenantId.Value}', '{roleId}', 'kernel.read'); insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type, scope_id) values ('{tenantId.Value}', '{membershipId.Value}', '{roleId}', 'Branch', '{branchId}'); reset app.tenant_id; reset role;");
        try
        {
            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var store = new NpgsqlAuthorizationSubjectStore(dataSource);
            var subject = await store.LoadAsync(tenantId, membershipId.Value);
            Assert.NotNull(subject);
            var authorizer = new PermissionAuthorizer();
            Assert.True(authorizer.Authorize(subject!, "kernel.read", PermissionScope.ForBranch(tenantId, new(entityId), new(branchId))));
            Assert.False(authorizer.Authorize(subject!, "kernel.read", PermissionScope.ForLegalEntity(tenantId, new(entityId))));
            Assert.False(authorizer.Authorize(subject!, "kernel.read", PermissionScope.ForTenant(otherTenantId)));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform.membership_role_assignment where membership_id = '{membershipId.Value}'; delete from platform.role_permission where role_id = '{roleId}'; delete from platform.role where id = '{roleId}'; delete from platform.membership where id = '{membershipId.Value}'; delete from platform.identity where id = '{identityId.Value}'; delete from platform.branch where id = '{branchId}'; delete from platform.legal_entity where id = '{entityId}'; delete from platform.tenant where id = '{tenantId.Value}'; set app.tenant_id = '{otherTenantId.Value}'; delete from platform.tenant where id = '{otherTenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
