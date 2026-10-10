using Npgsql;
using Core.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class ImportSchedulingIntegrationTests
{
    [Fact]
    public async Task ImportChunkSchedulingIsTransactionalAndIdempotent()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the import scheduling integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var batchId = Guid.CreateVersion7();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'import-{tenantId.Value:N}', 'Import', 'Active', 'import-{tenantId.Value:N}.local', 'import-{tenantId.Value:N}.local', now(), 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var scheduler = new NpgsqlImportJobScheduler(new NpgsqlTransactionCoordinator(dataSource));
            var batch = new ImportBatch(batchId, tenantId, "directory", "party-import", ImportStatus.Execute, Guid.CreateVersion7(), System.Text.Json.JsonSerializer.SerializeToElement(new { mapping = "stable" }), 0, Guid.CreateVersion7(), DateTimeOffset.UtcNow);

            var first = await scheduler.ScheduleChunkAsync(batch, 0, 100);
            var duplicate = await scheduler.ScheduleChunkAsync(batch, 0, 100);

            Assert.Equal(first, duplicate);
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_import.import_chunk_job where tenant_id = '{tenantId.Value}' and batch_id = '{batchId}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_messaging.outbox_message where tenant_id = '{tenantId.Value}' and message_type = 'platform.import.chunk.requested'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_messaging.outbox_message where tenant_id = '{tenantId.Value}'; delete from platform_import.import_chunk_job where tenant_id = '{tenantId.Value}'; delete from platform_import.import_batch where tenant_id = '{tenantId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
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
