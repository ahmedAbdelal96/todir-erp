using Npgsql;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class IdentityMembershipIntegrationTests
{
    [Fact]
    public async Task AccountHasOneTenantBoundAuthorizationSubject()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the identity integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'identity-{tenantId.Value:N}', 'Identity', 'Active', 'identity-{tenantId.Value:N}.local', 'identity-{tenantId.Value:N}.local', now(), 1); insert into platform_iam.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, status, created_at, security_stamp, version) values ('{identityId.Value}', '{tenantId.Value}', 'user@identity-{tenantId.Value:N}.local', 'USER@IDENTITY-{tenantId.Value:N}.LOCAL', 'user', 'Identity User', true, 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{membershipId.Value}', '{tenantId.Value}', '{identityId.Value}', 'Identity User', 'Active', now(), 1); reset app.tenant_id; reset role;");
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var identityStore = new NpgsqlIdentityStore(dataSource);
            var activeMembership = await identityStore.FindActiveMembershipAsync(identityId, tenantId);
            Assert.NotNull(activeMembership);
            Assert.Equal(membershipId, activeMembership!.Id);
            Assert.Null(await identityStore.FindActiveMembershipAsync(identityId, TenantId.New()));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_iam.membership where id = '{membershipId.Value}'; delete from platform_iam.identity where id = '{identityId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
