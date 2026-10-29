using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class SalesPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task SalesSeedIsTenantScopedAndEverySalesTableUsesForcedRls()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for database integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for database integration tests.");

        var tenantIds = new List<Guid>();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand("select id from platform.tenant order by id limit 2", admin);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tenantIds.Add(reader.GetGuid(0));
        }
        Assert.Equal(2, tenantIds.Count);

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        await using (var empty = new NpgsqlCommand("select count(*) from business_sales.quotation", runtime))
            Assert.Equal(0L, (long)(await empty.ExecuteScalarAsync())!);

        foreach (var tenantId in tenantIds)
        {
            await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime);
            context.Parameters.AddWithValue(tenantId.ToString("D"));
            await context.ExecuteNonQueryAsync();
            await using var count = new NpgsqlCommand("select count(*) from business_sales.quotation", runtime);
            Assert.True((long)(await count.ExecuteScalarAsync())! > 0);
        }

        await using var policy = new NpgsqlCommand("select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_sales' and c.relkind='r' and (not c.relrowsecurity or not c.relforcerowsecurity)", runtime);
        Assert.Equal(0L, (long)(await policy.ExecuteScalarAsync())!);
    }
}
