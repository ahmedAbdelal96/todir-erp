using Npgsql;
using Xunit;

namespace SecurityTests;

[Collection("database-security")]
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task RuntimeRoleCannotReadOrWriteAnotherTenant()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the tenant security gate.");
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var app = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "erp_app",
            Password = await SetRuntimePasswordAsync(admin)
        }.ConnectionString);

        try
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantA}', 'tenant-a', 'Tenant A', 'Active', 'tenant-a.local', 'tenant-a.local', now(), 1); SET app.tenant_id = '{tenantB}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantB}', 'tenant-b', 'Tenant B', 'Active', 'tenant-b.local', 'tenant-b.local', now(), 1); RESET app.tenant_id; RESET ROLE;");
            await app.OpenAsync();

            await using var transaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantA}';", transaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.tenant", transaction))!);
            Assert.Equal(0, await ExecuteAsync(app, $"UPDATE platform.tenant SET display_name = 'blocked' WHERE id = '{tenantB}';", transaction));
            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, $"INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{Guid.CreateVersion7()}', 'forbidden', 'Forbidden', 'Active', 'forbidden.local', 'forbidden.local', now(), 1);", transaction));
            await transaction.RollbackAsync();

            Assert.Equal(0L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.tenant"))!);
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; DELETE FROM platform.tenant WHERE id = '{tenantA}'; SET app.tenant_id = '{tenantB}'; DELETE FROM platform.tenant WHERE id = '{tenantB}'; RESET app.tenant_id; RESET ROLE;");
        }
    }

    private static async Task<string> SetRuntimePasswordAsync(NpgsqlConnection admin)
    {
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{password}'");
        return password;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string commandText, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(commandText, connection, transaction);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string commandText, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(commandText, connection, transaction);
        return await command.ExecuteScalarAsync();
    }
}
