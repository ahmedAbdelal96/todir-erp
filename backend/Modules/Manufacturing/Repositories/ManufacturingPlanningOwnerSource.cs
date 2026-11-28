using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

/// <summary>Exposes Manufacturing-owned uncovered demand quantities for planning drivers.</summary>
public sealed class ManufacturingPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "manufacturing";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select d.id,r.legal_entity_id,d.need_date,d.uncovered_qty,r.policy_version,d.product_variant_id,'PRODUCTION_DEMAND','XXX' from business_manufacturing.planning_demand d join business_manufacturing.planning_run r on r.tenant_id=d.tenant_id and r.id=d.planning_run_id where d.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) and r.state='Completed' and d.uncovered_qty>0 union all select p.id,d.legal_entity_id,p.as_of::date,p.total_unit_cost,p.source_version_hash,p.product_variant_id,'PRODUCTION_COST',p.currency_code from business_manufacturing.production_cost_projection p join business_manufacturing.production_definition d on d.tenant_id=p.tenant_id and d.id=p.production_definition_id where p.tenant_id=$1 and ($2::uuid is null or d.legal_entity_id=$2) order by 3,1";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); var kind = reader.GetString(6); var dimensions = JsonSerializer.Serialize(new { productVariantId = reader.GetGuid(5) }); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), kind, reader.GetString(4), reader.GetGuid(1), new(date.Year, date.Month, 1), kind, dimensions, "Forecast", reader.GetDecimal(3), reader.GetString(7).Trim(), reader.GetGuid(0).ToString("D"), "Operational")); }
        return facts;
    }
}
