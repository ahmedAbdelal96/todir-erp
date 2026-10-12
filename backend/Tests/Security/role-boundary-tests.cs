using System.Security.Cryptography;
using Npgsql;
using Xunit;

namespace SecurityTests;

[Collection("database-security")]
public sealed class RoleBoundaryTests
{
    [Fact]
    public async Task RuntimeRoleCanUseOwnedTablesButCannotAlterThem()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the P04 database security gate.");

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "runtime-roles-and-schemas.sql");
        var bootstrap = await File.ReadAllTextAsync(scriptPath);
        var tableName = $"permission_probe_{Guid.CreateVersion7():N}";
        var appPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, bootstrap);
            // The value is generated as hexadecimal bytes, so it is safe to embed
            // in this DDL statement; PostgreSQL utility statements do not accept
            // regular query parameters for ALTER ROLE passwords.
            await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{appPassword}'");
            await ExecuteAsync(admin, $"SET ROLE erp_owner; CREATE TABLE platform.{tableName} (id integer PRIMARY KEY, value text NOT NULL); RESET ROLE;");
            await ExecuteAsync(admin, $"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.{tableName} TO erp_app;");

            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Username = "erp_app",
                Password = appPassword
            }.ConnectionString;

            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();

            Assert.Equal("erp_owner", await ScalarAsync(app, $"SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid = 'platform.{tableName}'::regclass"));
            Assert.False((bool)(await ScalarAsync(app, "SELECT rolsuper FROM pg_roles WHERE rolname = current_user"))!);
            Assert.False((bool)(await ScalarAsync(app, "SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user"))!);

            await ExecuteAsync(app, $"INSERT INTO platform.{tableName} (id, value) VALUES (1, 'runtime');");
            Assert.Equal("runtime", await ScalarAsync(app, $"SELECT value FROM platform.{tableName} WHERE id = 1"));
            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, $"ALTER TABLE platform.{tableName} ADD COLUMN forbidden integer"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; DROP TABLE IF EXISTS platform.{tableName}; RESET ROLE;");
        }
    }

    [Fact]
    public async Task DispatcherIsLimitedToApprovedSecurityDefinerOperations()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the dispatcher security gate.");

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        Assert.True((bool)(await ScalarAsync(admin, "SELECT has_function_privilege('erp_dispatcher', 'platform.claim_pending_outbox(integer)', 'EXECUTE')"))!);
        Assert.True((bool)(await ScalarAsync(admin, "SELECT has_function_privilege('erp_dispatcher', 'platform.observe_pending_outbox()', 'EXECUTE')"))!);
        Assert.True((bool)(await ScalarAsync(admin, "SELECT has_function_privilege('erp_dispatcher', 'platform.expire_support_access_grants()', 'EXECUTE')"))!);
        Assert.True((bool)(await ScalarAsync(admin, "SELECT has_table_privilege('erp_dispatcher', 'platform.outbox_message', 'SELECT')"))!);
        Assert.True((bool)(await ScalarAsync(admin, "SELECT has_table_privilege('erp_dispatcher', 'platform.outbox_message', 'UPDATE')"))!);
        Assert.False((bool)(await ScalarAsync(admin, "SELECT has_table_privilege('erp_dispatcher', 'platform.outbox_message', 'INSERT')"))!);
        Assert.False((bool)(await ScalarAsync(admin, "SELECT has_table_privilege('erp_dispatcher', 'platform.support_access_grant', 'DELETE')"))!);
    }

    [Fact]
    public async Task RuntimeRoleCannotModifyAuditEntries()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the audit security gate.");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{password}'");
        await using var app = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString);
        await app.OpenAsync();

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, "UPDATE platform.audit_entry SET action = action WHERE false"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, "DELETE FROM platform.audit_entry WHERE false"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        return await command.ExecuteScalarAsync();
    }
}
