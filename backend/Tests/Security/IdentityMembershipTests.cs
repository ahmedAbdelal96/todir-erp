using Npgsql;
using Xunit;

namespace SecurityTests;

[Collection("database-security")]
public sealed class IdentityMembershipTests
{
    [Fact]
    public async Task UserAccountIsBoundToOneTenantAndRlsHidesTheOtherTenant()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the identity security gate.");
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var identityA = Guid.CreateVersion7();
        var identityB = Guid.CreateVersion7();
        var membershipA = Guid.CreateVersion7();
        var membershipB = Guid.CreateVersion7();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{password}'; SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantA}', 'identity-a-{tenantA:N}', 'A', 'Active', 'identity-a-{tenantA:N}.local', 'identity-a-{tenantA:N}.local', now(), 1); SET app.tenant_id = '{tenantB}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantB}', 'identity-b-{tenantB:N}', 'B', 'Active', 'identity-b-{tenantB:N}.local', 'identity-b-{tenantB:N}.local', now(), 1); SET app.tenant_id = '{tenantA}'; INSERT INTO platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, status, created_at, security_stamp, version) VALUES ('{identityA}', '{tenantA}', 'a@identity-a-{tenantA:N}.local', 'A@IDENTITY-A-{tenantA:N}.LOCAL', 'a', 'A User', 'Active', now(), '{Guid.CreateVersion7()}', 1); INSERT INTO platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version) VALUES ('{membershipA}', '{tenantA}', '{identityA}', 'A User', 'Active', now(), 1); SET app.tenant_id = '{tenantB}'; INSERT INTO platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, status, created_at, security_stamp, version) VALUES ('{identityB}', '{tenantB}', 'b@identity-b-{tenantB:N}.local', 'B@IDENTITY-B-{tenantB:N}.LOCAL', 'b', 'B User', 'Active', now(), '{Guid.CreateVersion7()}', 1); INSERT INTO platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version) VALUES ('{membershipB}', '{tenantB}', '{identityB}', 'B User', 'Active', now(), 1); RESET app.tenant_id; RESET ROLE;");
        try
        {
            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;
            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();
            await using var transaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantA}';", transaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.identity", transaction))!);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.membership", transaction))!);
            await transaction.RollbackAsync();
            await using var tenantBTransaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantB}';", tenantBTransaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.identity", tenantBTransaction))!);
            await tenantBTransaction.RollbackAsync();
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantA}'; DELETE FROM platform.membership WHERE id = '{membershipA}'; DELETE FROM platform.identity WHERE id = '{identityA}'; DELETE FROM platform.tenant WHERE id = '{tenantA}'; SET app.tenant_id = '{tenantB}'; DELETE FROM platform.membership WHERE id = '{membershipB}'; DELETE FROM platform.identity WHERE id = '{identityB}'; DELETE FROM platform.tenant WHERE id = '{tenantB}'; RESET app.tenant_id; RESET ROLE;");
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
