using System.Security.Cryptography;
using Npgsql;
using Modules.Sample;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class SampleModuleIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task SampleCreateWritesModuleAuditAndOutboxAtomically()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Sample module integration gate.");
        var tenantId = TenantId.New();
        var actorId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'sample-{tenantId.Value:N}', 'Sample', 'Active', 'sample-{tenantId.Value:N}.local', 'sample-{tenantId.Value:N}.local', now(), 1); reset app.tenant_id; reset role;");
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
        var service = new SampleItemService(new NpgsqlTransactionCoordinator(dataSource));
        var correlationId = Guid.CreateVersion7();

        try
        {
            var item = await service.CreateAsync(tenantId, actorId, membershipId, "verification-item", correlationId, "00-00000000000000000000000000000001-0000000000000001-01");

            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from sample.sample_item where id = '{item.Id}' and tenant_id = '{tenantId.Value}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.audit_entry where entity_id = '{item.Id}' and tenant_id = '{tenantId.Value}' and action = 'sample_item_created'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.outbox_message where causation_id = '{item.Id}' and tenant_id = '{tenantId.Value}' and message_type = 'SampleEntityCreatedV1'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform.outbox_message where tenant_id = '{tenantId.Value}'; delete from platform.audit_entry where tenant_id = '{tenantId.Value}'; delete from sample.sample_projection where tenant_id = '{tenantId.Value}'; delete from sample.sample_item where tenant_id = '{tenantId.Value}'; delete from platform.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
