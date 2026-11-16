using Npgsql;
using Xunit;

namespace IntegrationTests;

public sealed class ServiceDatabaseIntegrityAcceptanceTests
{
    [Fact]
    public async Task ServiceSeedHasRealCrossModuleReferencesAndTypedOutcomes()
    {
        var connectionString = ServiceAcceptanceDatabaseGuard.RequireConfiguredConnection();
        await ServiceAcceptanceDatabaseGuard.VerifyAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        var forcedRls = await ScalarAsync<long>(connection, "select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_service' and c.relkind='r' and c.relrowsecurity and c.relforcerowsecurity");
        var tables = await ScalarAsync<long>(connection, "select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_service' and c.relkind='r'");
        var movementRows = 0L;
        var oldAccepted = 0L;
        var serviceVariantRefs = 0L;
        var tenantIds = new[] { Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b") };
        foreach (var tenantId in tenantIds)
        {
            await SetTenantAsync(connection, tenantId);
            movementRows += await ScalarAsync<long>(connection, "select count(*) from business_service.work_part_movement where state='Processed' and inventory_operation_id is not null");
            oldAccepted += await ScalarAsync<long>(connection, "select count(*) from business_service.offline_command where state='Processed' and result->>'accepted'='true'");
            serviceVariantRefs += await ScalarAsync<long>(connection, "select count(*) from business_service.work_part_requirement p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.id=p.product_id join business_inventory.warehouse w on w.tenant_id=p.tenant_id and w.id=p.warehouse_id join foundation_catalog.uom u on u.tenant_id=p.tenant_id and u.id=p.unit_of_measure_id");
        }
        Assert.Equal(tables, forcedRls);
        Assert.True(movementRows >= 1);
        Assert.Equal(0, oldAccepted);
        Assert.True(serviceVariantRefs >= 1);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected an aggregate result.");
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}
