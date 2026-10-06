using Npgsql;
using Todir.Platform.Security;
using Todir.Platform.Services;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class SupportAccessIntegrationTests
{
    [Fact]
    public async Task PersistentSupportAccessExpiresByBoundaryAndWritesAudit()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the support access integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var supportActorId = Guid.CreateVersion7();
        var approverId = Guid.CreateVersion7();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantId.Value}', 'support-{tenantId.Value:N}', 'Support', 'Active', now(), 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var workflow = new NpgsqlSupportAccessWorkflow(dataSource, new NpgsqlAuditWriter(dataSource), TimeProvider.System);
            var grant = await workflow.RequestAsync(tenantId, supportActorId, "controlled incident response", DateTimeOffset.UtcNow.AddMinutes(10), true);
            Assert.Equal(SupportAccessStatus.Requested, grant.Status);
            Assert.NotNull(await workflow.ApproveAsync(tenantId, grant.Id, approverId, true));
            Assert.False(await workflow.CanAccessAsync(TenantId.New(), grant.Id, supportActorId, true));
            Assert.True(await workflow.CanAccessAsync(tenantId, grant.Id, supportActorId, true));
            Assert.True(await workflow.RevokeAsync(tenantId, grant.Id, true));
            Assert.False(await workflow.CanAccessAsync(tenantId, grant.Id, supportActorId, true));

            Assert.Equal(4L, await ScalarAsync(admin, $"select count(*) from platform_audit.audit_entry where tenant_id = '{tenantId.Value}' and entity_id = '{grant.Id}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_iam.support_access_grant where tenant_id = '{tenantId.Value}' and id = '{grant.Id}' and status = 'Revoked'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_audit.audit_entry where tenant_id = '{tenantId.Value}'; delete from platform_iam.support_access_grant where tenant_id = '{tenantId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
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
