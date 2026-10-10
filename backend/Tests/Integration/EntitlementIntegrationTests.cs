using System.Security.Cryptography;
using Npgsql;
using Core.Entitlements;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class EntitlementIntegrationTests
{
    [Fact]
    public async Task LatestTenantSnapshotLoadsThroughTheRlsBoundStore()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the entitlement integration gate.");
        var tenantId = TenantId.New();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantId.Value}', 'entitlement-{tenantId.Value:N}', 'Entitlement', 'Active', now(), 1); insert into platform_entitlements.entitlement_snapshot (tenant_id, snapshot_version, issued_at, valid_until, key_id, modules, features, limits, scopes, signature) values ('{tenantId.Value}', 1, now(), now() + interval '1 hour', 'current', '[\"sample\"]'::jsonb, '[]'::jsonb, '{{}}'::jsonb, '{{}}'::jsonb, 'test-signature'); reset app.tenant_id; reset role;");
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
        var store = new NpgsqlEntitlementSnapshotStore(dataSource);

        try
        {
            var snapshot = await store.LoadLatestAsync(tenantId);
            Assert.NotNull(snapshot);
            Assert.Equal(tenantId, snapshot.TenantId);
            Assert.Contains("sample", snapshot.Modules);
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_entitlements.entitlement_snapshot where tenant_id = '{tenantId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
