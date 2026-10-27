using Npgsql;
using Xunit;

namespace SecurityTests;

[Collection("database-security")]
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task RuntimeRoleCannotReadOrWriteAnotherTenantsInvoiceOrCreateCrossTenantLine()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION to run the invoicing RLS gate.");
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var entityA = Guid.CreateVersion7();
        var entityB = Guid.CreateVersion7();
        var invoiceA = Guid.CreateVersion7();
        var invoiceB = Guid.CreateVersion7();

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var runtimePassword = await SetRuntimePasswordAsync(admin);
        await using var app = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = "erp_app",
            Password = runtimePassword
        }.ConnectionString);

        try
        {
            await ExecuteAsync(admin, $"""
                SET ROLE erp_owner;
                SET app.tenant_id = '{tenantA}';
                INSERT INTO platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) VALUES ('{tenantA}','invoice-a-{tenantA:N}','Invoice Tenant A','Active','invoice-a-{tenantA:N}.local','invoice-a-{tenantA:N}.local',now(),1);
                INSERT INTO platform.legal_entity (id,tenant_id,code,legal_name,country_code,base_currency_code,timezone,locale,status,created_at,version) VALUES ('{entityA}','{tenantA}','A','Entity A','EG','EGP','Africa/Cairo','ar-EG','Active',now(),1);
                INSERT INTO business_invoicing.invoice (tenant_id,legal_entity_id,id,direction,document_type,lifecycle_state,party_id,document_date,posting_date,tax_date,currency_code,created_at,updated_at) VALUES ('{tenantA}','{entityA}','{invoiceA}','Receivable','Invoice','Draft','{Guid.CreateVersion7()}',current_date,current_date,current_date,'EGP',now(),now());
                SET app.tenant_id = '{tenantB}';
                INSERT INTO platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) VALUES ('{tenantB}','invoice-b-{tenantB:N}','Invoice Tenant B','Active','invoice-b-{tenantB:N}.local','invoice-b-{tenantB:N}.local',now(),1);
                INSERT INTO platform.legal_entity (id,tenant_id,code,legal_name,country_code,base_currency_code,timezone,locale,status,created_at,version) VALUES ('{entityB}','{tenantB}','B','Entity B','EG','EGP','Africa/Cairo','ar-EG','Active',now(),1);
                INSERT INTO business_invoicing.invoice (tenant_id,legal_entity_id,id,direction,document_type,lifecycle_state,party_id,document_date,posting_date,tax_date,currency_code,created_at,updated_at) VALUES ('{tenantB}','{entityB}','{invoiceB}','Receivable','Invoice','Draft','{Guid.CreateVersion7()}',current_date,current_date,current_date,'EGP',now(),now());
                INSERT INTO business_invoicing.invoice_line (tenant_id,legal_entity_id,invoice_id,id,sequence,description,quantity) VALUES ('{tenantB}','{entityB}','{invoiceB}','{Guid.CreateVersion7()}',1,'Tenant B issued line',1);
                RESET app.tenant_id;
                RESET ROLE;
                """);
            await app.OpenAsync();
            await using var transaction = await app.BeginTransactionAsync();
            await ExecuteAsync(app, $"SET LOCAL app.tenant_id = '{tenantA}';", transaction);
            Assert.Equal(1L, (long)(await ScalarAsync(app, "SELECT count(*) FROM business_invoicing.invoice", transaction))!);
            Assert.Equal(0L, (long)(await ScalarAsync(app, "SELECT count(*) FROM business_invoicing.invoice WHERE tenant_id='" + tenantB + "'", transaction))!);
            Assert.Equal(0, await ExecuteAsync(app, $"UPDATE business_invoicing.invoice SET gross_total=10 WHERE tenant_id='{tenantB}' AND id='{invoiceB}';", transaction));

            await using var crossTenantChild = new NpgsqlCommand("INSERT INTO business_invoicing.invoice_line (tenant_id,legal_entity_id,invoice_id,id,sequence,description,quantity) VALUES ($1,$2,$3,$4,2,'Forged cross-tenant line',1)", app, transaction);
            crossTenantChild.Parameters.AddWithValue(tenantB);
            crossTenantChild.Parameters.AddWithValue(entityB);
            crossTenantChild.Parameters.AddWithValue(invoiceB);
            crossTenantChild.Parameters.AddWithValue(Guid.CreateVersion7());
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => crossTenantChild.ExecuteNonQueryAsync());
            Assert.True(rejected.SqlState is "42501" or "23514", $"Cross-tenant child insert failed with SQLSTATE {rejected.SqlState}.");
            await transaction.RollbackAsync();
        }
        finally
        {
            await ExecuteAsync(admin, $"""
                SET ROLE erp_owner;
                SET app.tenant_id = '{tenantA}';
                DELETE FROM business_invoicing.invoice WHERE tenant_id='{tenantA}' AND id='{invoiceA}';
                DELETE FROM platform.legal_entity WHERE tenant_id='{tenantA}' AND id='{entityA}';
                DELETE FROM platform.tenant WHERE id='{tenantA}';
                SET app.tenant_id = '{tenantB}';
                DELETE FROM business_invoicing.invoice_line WHERE tenant_id='{tenantB}' AND legal_entity_id='{entityB}' AND invoice_id='{invoiceB}';
                DELETE FROM business_invoicing.invoice WHERE tenant_id='{tenantB}' AND id='{invoiceB}';
                DELETE FROM platform.legal_entity WHERE tenant_id='{tenantB}' AND id='{entityB}';
                DELETE FROM platform.tenant WHERE id='{tenantB}';
                RESET app.tenant_id;
                RESET ROLE;
                """);
        }
    }

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
