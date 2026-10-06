using Npgsql;
using Xunit;

namespace Todir.Database.Security.Tests;

[Collection("database-security")]
public sealed class IdentityMembershipTests
{
    [Fact]
    public async Task OneIdentityCanHaveIsolatedActiveMembershipsPerTenant()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the identity security gate.");
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var identity = Guid.CreateVersion7();
        var membershipA = Guid.CreateVersion7();
        var membershipB = Guid.CreateVersion7();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "runtime-roles-and-schemas.sql")));
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{password}'");
        await ExecuteAsync(admin, $"SET ROLE erp_owner; {await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "tenant-boundary.sql"))} RESET ROLE;");
        await ExecuteAsync(admin, $"SET ROLE erp_owner; {await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "organization-schema.sql"))} {await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "identity-membership-schema.sql"))} RESET ROLE;");

        try
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; INSERT INTO platform_tenancy.tenant (id, slug, display_name, status, created_at, version) VALUES ('{tenantA}', 'identity-a-{tenantA:N}', 'A', 'Active', now(), 1); SET app.tenant_id = '{tenantB}'; INSERT INTO platform_tenancy.tenant (id, slug, display_name, status, created_at, version) VALUES ('{tenantB}', 'identity-b-{tenantB:N}', 'B', 'Active', now(), 1); INSERT INTO platform_iam.identity (id, normalized_email, status, created_at, security_stamp, version) VALUES ('{identity}', 'USER@EXAMPLE.COM', 'Active', now(), '{Guid.CreateVersion7()}', 1); SET app.tenant_id = '{tenantA}'; INSERT INTO platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) VALUES ('{membershipA}', '{tenantA}', '{identity}', 'A member', 'Active', now(), 1); SET app.tenant_id = '{tenantB}'; INSERT INTO platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) VALUES ('{membershipB}', '{tenantB}', '{identity}', 'B member', 'Suspended', now(), 1); RESET app.tenant_id; RESET ROLE;");
            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();
            await using var transaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantA}';", transaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform_iam.membership WHERE status = 'Active'", transaction))!);
            await transaction.RollbackAsync();
            await using var tenantBTransaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantB}';", tenantBTransaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform_iam.membership WHERE status = 'Suspended'", tenantBTransaction))!);
            await tenantBTransaction.RollbackAsync();
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; DELETE FROM platform_iam.membership WHERE id = '{membershipA}'; SET app.tenant_id = '{tenantB}'; DELETE FROM platform_iam.membership WHERE id = '{membershipB}'; RESET app.tenant_id; DELETE FROM platform_iam.identity WHERE id = '{identity}'; SET app.tenant_id = '{tenantA}'; DELETE FROM platform_tenancy.tenant WHERE id = '{tenantA}'; SET app.tenant_id = '{tenantB}'; DELETE FROM platform_tenancy.tenant WHERE id = '{tenantB}'; RESET app.tenant_id; RESET ROLE;");
        }
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteScalarAsync();
    }
}
