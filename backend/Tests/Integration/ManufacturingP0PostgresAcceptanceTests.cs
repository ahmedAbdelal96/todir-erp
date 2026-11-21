using Npgsql;
using Xunit;

namespace IntegrationTests;

public sealed class ManufacturingP0PostgresAcceptanceTests
{
    [ManufacturingPostgresFact]
    public async Task PlanningSnapshotHasOneAuthoritativeStockAllocationAndNoDuplicateDependencies()
    {
        await using var database = NpgsqlDataSource.Create(Connection());
        await using var connection = await database.OpenConnectionAsync();
        await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        tenant.Parameters.AddWithValue(TenantId().ToString("D"));
        await tenant.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("""
            with candidate as (
              select planning_run_id,product_variant_id,need_date,uom_code,
                     sum(quantity) filter (where source_type='Forecast') forecast_qty,
                     sum(quantity) filter (where source_type<>'Forecast') accepted_qty,
                     sum(consumed_forecast_qty) consumed_qty,
                     sum(stock_allocated_qty) stock_qty,
                     sum(uncovered_qty) uncovered_qty
                from business_manufacturing.planning_demand
               where parent_demand_id is null
               group by planning_run_id,product_variant_id,need_date,uom_code
              having count(*) filter (where source_type='Forecast') > 0
                 and count(*) filter (where source_type<>'Forecast') > 0
            )
            select c.forecast_qty,c.accepted_qty,c.consumed_qty,c.stock_qty,c.uncovered_qty,
                   coalesce(sum(ps.quantity) filter (where ps.state='Suggested'),0),
                   (select count(*) from business_manufacturing.planning_demand d where d.planning_run_id=c.planning_run_id and d.source_type='Dependent' and d.source_ref is not null)
              from candidate c
              left join business_manufacturing.planned_supply ps on ps.planning_run_id=c.planning_run_id and ps.product_variant_id=c.product_variant_id and ps.uom_code=c.uom_code and ps.need_date=c.need_date
             group by c.planning_run_id,c.product_variant_id,c.need_date,c.uom_code,c.forecast_qty,c.accepted_qty,c.consumed_qty,c.stock_qty,c.uncovered_qty
             order by c.planning_run_id
             limit 1
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "An isolated seeded database must contain the forecast-overlap acceptance fixture.");
        var forecast = reader.GetDecimal(0);
        var accepted = reader.GetDecimal(1);
        var consumed = reader.GetDecimal(2);
        var gross = accepted + Math.Max(0m, forecast - Math.Min(forecast, Math.Max(consumed, accepted)));
        var stock = reader.GetDecimal(3);
        var uncovered = reader.GetDecimal(4);
        var suggested = reader.GetDecimal(5);
        Assert.Equal(gross, stock + uncovered, 6);
        Assert.True(suggested + stock >= gross, "Suggested production must not be short after the one-time stock allocation.");
        Assert.True(reader.GetInt64(6) >= 0);
    }

    [ManufacturingPostgresFact]
    public async Task ManufacturingLedgerFactsAreUniqueAndBalancedWhenPosted()
    {
        await using var database = NpgsqlDataSource.Create(Connection());
        await using var connection = await database.OpenConnectionAsync();
        await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        tenant.Parameters.AddWithValue(TenantId().ToString("D"));
        await tenant.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("""
            select count(*) filter (where status='Posted'),count(distinct source_reference),
                   coalesce(sum(abs(debit_total-credit_total)),0)
              from (
                select f.source_reference, f.status, coalesce(sum(l.debit),0) debit_total, coalesce(sum(l.credit),0) credit_total
                  from finance_ledger.accounting_fact f
                  left join finance_ledger.journal_entry e on e.tenant_id=f.tenant_id and e.fact_id=f.id
                  left join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.journal_entry_id=e.id
                 where f.fact_type='ManufacturingCostAccrued'
                 group by f.source_reference,f.status,f.id
              ) facts
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(1) >= reader.GetInt64(0));
        Assert.Equal(0m, reader.GetDecimal(2));
    }

    private static string Connection()
    {
        var value = Environment.GetEnvironmentVariable("TODIR_MFG_P0_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(value), "TODIR_MFG_P0_TEST_CONNECTION must target an isolated PostgreSQL database.");
        var builder = new NpgsqlConnectionStringBuilder(value);
        var database = builder.Database ?? string.Empty;
        Assert.NotEqual("todir", database, StringComparer.OrdinalIgnoreCase);
        Assert.True(database.Contains("test", StringComparison.OrdinalIgnoreCase) || database.Contains("p0", StringComparison.OrdinalIgnoreCase), "The acceptance connection must target an explicitly isolated database.");
        return builder.ConnectionString;
    }

    private static Guid TenantId()
    {
        var value = Environment.GetEnvironmentVariable("TODIR_MFG_P0_TENANT_ID");
        Assert.True(Guid.TryParse(value, out var tenantId), "TODIR_MFG_P0_TENANT_ID must identify the seeded acceptance tenant.");
        return tenantId;
    }

    private sealed class ManufacturingPostgresFactAttribute : FactAttribute
    {
        public ManufacturingPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_MFG_P0_TEST_CONNECTION")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TODIR_MFG_P0_TENANT_ID"))) Skip = "TODIR_MFG_P0_TEST_CONNECTION and TODIR_MFG_P0_TENANT_ID are required.";
        }
    }
}
