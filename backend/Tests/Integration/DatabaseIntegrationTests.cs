using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class DatabaseIntegrationTests
{
    [Fact]
    public async Task RuntimeRoleCannotReadAnotherTenantAfterFuzzedTenantContextSwitches()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the database integration gate.");
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantA}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantA}', 'fuzz-a-{tenantA:N}', 'A', 'Active', 'fuzz-a-{tenantA:N}.local', 'fuzz-a-{tenantA:N}.local', now(), 1); set app.tenant_id = '{tenantB}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantB}', 'fuzz-b-{tenantB:N}', 'B', 'Active', 'fuzz-b-{tenantB:N}.local', 'fuzz-b-{tenantB:N}.local', now(), 1); reset app.tenant_id; reset role;");
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
        try
        {
            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();
            foreach (var visible in new[] { tenantA, tenantB, Guid.CreateVersion7(), Guid.CreateVersion7() })
            {
                await using var transaction = await app.BeginTransactionAsync();
                await ExecuteAsync(app, $"set local app.tenant_id = '{visible}';", transaction);
                var count = await ScalarAsync(app, "select count(*) from platform.tenant", transaction);
                Assert.Equal(visible == tenantA || visible == tenantB ? 1L : 0L, (long)count!);
                await transaction.RollbackAsync();
            }
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantA}'; delete from platform.tenant where id = '{tenantA}'; set app.tenant_id = '{tenantB}'; delete from platform.tenant where id = '{tenantB}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteScalarAsync();
    }
}
