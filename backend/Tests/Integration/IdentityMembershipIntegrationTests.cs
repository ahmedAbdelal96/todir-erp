using Npgsql;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class IdentityMembershipIntegrationTests
{
    [Fact]
    public async Task InvitationAcceptsOnceAndSuspensionRevokesMembership()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the identity membership integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var otherTenantId = TenantId.New();
        var identityId = IdentityId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantId.Value}', 'identity-{tenantId.Value:N}', 'Identity', 'Active', now(), 1); set app.tenant_id = '{otherTenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{otherTenantId.Value}', 'identity-{otherTenantId.Value:N}', 'Other', 'Active', now(), 1); reset app.tenant_id; insert into platform_iam.identity (id, normalized_email, email_verified, status, created_at, security_stamp, version) values ('{identityId.Value}', 'INVITED-{identityId.Value:N}@EXAMPLE.COM', false, 'Active', now(), '{Guid.CreateVersion7()}', 1); reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var adminStore = new NpgsqlIdentityMembershipAdminStore(dataSource);
            var invitation = await adminStore.InviteAsync(tenantId, identityId, "Invited member", DateTimeOffset.UtcNow.AddMinutes(10));

            Assert.False(string.IsNullOrWhiteSpace(invitation.Token));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_iam.membership where id = '{invitation.MembershipId.Value}' and status = 'Invited'"));
            Assert.Equal(64, (await ScalarAsync(admin, $"select length(token_digest) from platform_iam.membership_invitation where tenant_id = '{tenantId.Value}' and identity_id = '{identityId.Value}'")));
            Assert.False(await adminStore.AcceptAsync(otherTenantId, invitation.Token));
            Assert.True(await adminStore.AcceptAsync(tenantId, invitation.Token));
            Assert.False(await adminStore.AcceptAsync(tenantId, invitation.Token));

            var identityStore = new NpgsqlIdentityStore(dataSource);
            var activeMembership = await identityStore.FindActiveMembershipAsync(identityId, tenantId);
            Assert.NotNull(activeMembership);
            Assert.Equal(invitation.MembershipId, activeMembership!.Id);
            Assert.Single(await identityStore.ListActiveMembershipsAsync(identityId, tenantId));
            Assert.Empty(await identityStore.ListActiveMembershipsAsync(identityId, otherTenantId));
            Assert.True(await adminStore.SuspendAsync(tenantId, invitation.MembershipId));
            Assert.False(await adminStore.SuspendAsync(tenantId, invitation.MembershipId));
            Assert.Null(await identityStore.FindActiveMembershipAsync(identityId, tenantId));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_iam.membership_invitation where tenant_id = '{tenantId.Value}' and identity_id = '{identityId.Value}'; delete from platform_iam.membership where tenant_id = '{tenantId.Value}' and identity_id = '{identityId.Value}'; delete from platform_iam.identity where id = '{identityId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; set app.tenant_id = '{otherTenantId.Value}'; delete from platform_tenancy.tenant where id = '{otherTenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
