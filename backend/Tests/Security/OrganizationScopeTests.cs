using Npgsql;
using Xunit;

namespace SecurityTests;

[Collection("database-security")]
public sealed class OrganizationScopeTests
{
    [Fact]
    public async Task TenantEntityAndBranchScopesRemainDistinct()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the organization security gate.");
        var tenantId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var appPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await ExecuteAsync(admin, $"ALTER ROLE erp_app PASSWORD '{appPassword}'");

        try
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantId}', 'scope-{tenantId:N}', 'Scope Tenant', 'Active', 'scope-{tenantId:N}.local', 'scope-{tenantId:N}.local', now(), 1); INSERT INTO platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) VALUES ('{entityId}', '{tenantId}', 'HQ', 'Scope Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1); INSERT INTO platform.branch (id, tenant_id, legal_entity_id, code, name, status, created_at, version) VALUES ('{branchId}', '{tenantId}', '{entityId}', 'MAIN', 'Main Branch', 'Active', now(), 1); RESET app.tenant_id; RESET ROLE;");

            var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;
            await using var app = new NpgsqlConnection(appConnectionString);
            await app.OpenAsync();
            await using var transaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantId}';", transaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.legal_entity", transaction))!);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM platform.branch", transaction))!);
            await transaction.RollbackAsync();
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId}'; DELETE FROM platform.branch WHERE id = '{branchId}'; DELETE FROM platform.legal_entity WHERE id = '{entityId}'; DELETE FROM platform.tenant WHERE id = '{tenantId}'; RESET app.tenant_id; RESET ROLE;");
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
