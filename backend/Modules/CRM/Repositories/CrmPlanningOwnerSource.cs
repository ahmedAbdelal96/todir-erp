using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.CRM.Repositories;

/// <summary>Exposes weighted open-pipeline drivers; CRM remains the opportunity owner.</summary>
public sealed class CrmPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "crm";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select o.id,o.legal_entity_id,coalesce(o.expected_close_date,current_date),o.currency_code,o.version,o.estimated_amount*s.probability,o.forecast_category,o.stage_id from business_crm.opportunity o join business_crm.crm_pipeline_stage s on s.tenant_id=o.tenant_id and s.legal_entity_id=o.legal_entity_id and s.id=o.stage_id where o.tenant_id=$1 and ($2::uuid is null or o.legal_entity_id=$2) and o.status='Open' order by coalesce(o.expected_close_date,current_date),o.id";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); var dimensions = JsonSerializer.Serialize(new { stageId = reader.GetGuid(7), forecastCategory = reader.GetString(6) }); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), "weighted-pipeline", reader.GetInt64(4).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(1), new(date.Year, date.Month, 1), "REVENUE_PIPELINE", dimensions, "Forecast", reader.GetDecimal(5), reader.GetString(3).Trim(), reader.GetGuid(0).ToString("D"), "Commercial")); }
        return facts;
    }
}
