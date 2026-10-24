using Npgsql;
using System.Globalization;
using Xunit;

namespace Modules.Ledger.Tests;

public sealed class LedgerSecurityIntegrationTests
{
    private static readonly string[] TenantTables = ["account", "journal", "accounting_period", "accounting_fact", "account_mapping", "journal_entry", "journal_entry_line"];
    private static readonly string[] AllLedgerTables = ["account_category", "account", "journal", "fiscal_year", "accounting_period", "financial_lock", "accounting_configuration_version", "accounting_fact", "posting_rule", "posting_rule_version", "posting_rule_leg", "account_role", "account_mapping", "account_mapping_version", "journal_entry", "journal_entry_line"];

    [Fact]
    public async Task RuntimeRoleHasNoRlsBypassAndEveryLedgerTableIsForcedRls()
    {
        var adminConnection = LedgerTestSupport.RequireConnection("TODIR_P04_ADMIN_CONNECTION");
        await using var dataSource = NpgsqlDataSource.Create(adminConnection);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("select count(*) filter (where relrowsecurity and relforcerowsecurity), count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='finance_ledger' and c.relname = any($1)", connection);
        command.Parameters.AddWithValue(AllLedgerTables);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(AllLedgerTables.Length, reader.GetInt64(0));
        Assert.Equal(AllLedgerTables.Length, reader.GetInt64(1));
        await reader.DisposeAsync();
        await using var role = new NpgsqlCommand("select not rolsuper, not rolbypassrls from pg_roles where rolname='erp_app'", connection);
        await using var roleReader = await role.ExecuteReaderAsync();
        Assert.True(await roleReader.ReadAsync());
        Assert.True(roleReader.GetBoolean(0));
        Assert.True(roleReader.GetBoolean(1));
    }

    [Fact]
    public async Task RuntimeTenantContextCannotReadUpdateOrDeleteAnotherTenant()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        foreach (var table in TenantTables)
        {
            await using var own = new NpgsqlCommand($"select count(*) from finance_ledger.{table} where tenant_id=$1", connection);
            own.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            Assert.True(Convert.ToInt64(await own.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0, $"Seed did not populate finance_ledger.{table}.");
        }

        var delta = new Shared.Primitives.Identifiers.TenantId(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
        await LedgerTestSupport.SetTenantAsync(connection, delta);
        foreach (var table in TenantTables)
        {
            await using var hidden = new NpgsqlCommand($"select count(*) from finance_ledger.{table} where tenant_id=$1", connection);
            hidden.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            Assert.True(Convert.ToInt64(await hidden.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0, $"RLS leaked finance_ledger.{table}.");
            await using var update = new NpgsqlCommand($"update finance_ledger.{table} set tenant_id=tenant_id where tenant_id=$1", connection);
            update.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            Assert.Equal(0, await update.ExecuteNonQueryAsync());
            await using var delete = new NpgsqlCommand($"delete from finance_ledger.{table} where tenant_id=$1", connection);
            delete.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            try { Assert.Equal(0, await delete.ExecuteNonQueryAsync()); }
            catch (PostgresException exception) { Assert.Equal("42501", exception.SqlState); }
        }
    }

    [Fact]
    public async Task SameTenantEntitiesDoNotCrossResolveAccountsOrEntries()
    {
        await using var dataSource = NpgsqlDataSource.Create(LedgerTestSupport.RequireConnection());
        await using var connection = await dataSource.OpenConnectionAsync();
        await LedgerTestSupport.SetTenantAsync(connection, LedgerTestSupport.AtlasTenant);
        var entityIds = new List<Guid>();
        await using (var entities = new NpgsqlCommand("select id from platform.legal_entity where tenant_id=$1 order by id", connection))
        {
            entities.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
            await using var reader = await entities.ExecuteReaderAsync();
            while (await reader.ReadAsync()) entityIds.Add(reader.GetGuid(0));
        }
        Assert.Equal(2, entityIds.Count);
        foreach (var entityId in entityIds)
        {
            await using var accounts = new NpgsqlCommand("select count(*) from finance_ledger.account where tenant_id=$1 and legal_entity_id=$2", connection);
            accounts.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); accounts.Parameters.AddWithValue(entityId);
            Assert.True(Convert.ToInt64(await accounts.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0);
            await using var journals = new NpgsqlCommand("select count(*) from finance_ledger.journal where tenant_id=$1 and legal_entity_id=$2", connection);
            journals.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value); journals.Parameters.AddWithValue(entityId);
            Assert.True(Convert.ToInt64(await journals.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0);
        }
        await using var mismatch = new NpgsqlCommand("select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.legal_entity_id=l.legal_entity_id and e.id=l.journal_entry_id join finance_ledger.account a on a.tenant_id=l.tenant_id and a.legal_entity_id=l.legal_entity_id and a.id=l.account_id where l.tenant_id=$1 and e.legal_entity_id<>a.legal_entity_id", connection);
        mismatch.Parameters.AddWithValue(LedgerTestSupport.AtlasTenant.Value);
        Assert.Equal(0L, Convert.ToInt64(await mismatch.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
    }
}
