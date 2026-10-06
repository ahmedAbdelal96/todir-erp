using System.Security.Cryptography;
using Npgsql;
using Xunit;

namespace Todir.Database.Security.Tests;

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
            await ExecuteAsync(admin, $"SET ROLE erp_owner; CREATE TABLE platform_tenancy.{tableName} (id integer PRIMARY KEY, value text NOT NULL); RESET ROLE;");
            await ExecuteAsync(admin, $"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform_tenancy.{tableName} TO erp_app;");

            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Username = "erp_app",
                Password = appPassword
            }.ConnectionString;

            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();

            Assert.Equal("erp_owner", await ScalarAsync(app, $"SELECT pg_get_userbyid(relowner) FROM pg_class WHERE oid = 'platform_tenancy.{tableName}'::regclass"));
            Assert.False((bool)(await ScalarAsync(app, "SELECT rolsuper FROM pg_roles WHERE rolname = current_user"))!);
            Assert.False((bool)(await ScalarAsync(app, "SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user"))!);

            await ExecuteAsync(app, $"INSERT INTO platform_tenancy.{tableName} (id, value) VALUES (1, 'runtime');");
            Assert.Equal("runtime", await ScalarAsync(app, $"SELECT value FROM platform_tenancy.{tableName} WHERE id = 1"));
            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, $"ALTER TABLE platform_tenancy.{tableName} ADD COLUMN forbidden integer"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; DROP TABLE IF EXISTS platform_tenancy.{tableName}; RESET ROLE;");
        }
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
