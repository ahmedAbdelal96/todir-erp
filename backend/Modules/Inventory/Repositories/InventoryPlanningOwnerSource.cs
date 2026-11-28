using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Inventory.Repositories;

/// <summary>Exposes immutable inventory valuation movements as operational planning facts.</summary>
public sealed class InventoryPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "inventory";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select id,legal_entity_id,effective_at::date,currency_code,value_delta,created_at from business_inventory.valuation_entry where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) order by effective_at,id";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), "valuation", reader.GetFieldValue<DateTimeOffset>(5).UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(1), new(date.Year, date.Month, 1), "INVENTORY_VALUE", "{}", "InventoryValuation", reader.GetDecimal(4), reader.GetString(3).Trim(), reader.GetGuid(0).ToString("D"), "Operational")); }
        return facts;
    }
}
