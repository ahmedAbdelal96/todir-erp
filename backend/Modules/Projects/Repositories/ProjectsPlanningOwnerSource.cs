using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Projects.Repositories;

/// <summary>Exposes Projects-owned cost, commitment, and approved EAC facts.</summary>
public sealed class ProjectsPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "projects";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select c.id,c.project_id,p.legal_entity_id,c.fact_date,c.base_amount,c.base_currency,c.state,c.created_at,'Cost' as fact_kind,c.cost_category from business_projects.project_cost_fact c join business_projects.project p on p.tenant_id=c.tenant_id and p.id=c.project_id where c.tenant_id=$1 and ($2::uuid is null or p.legal_entity_id=$2) and c.state='Finalized' union all select c.id,c.project_id,p.legal_entity_id,c.commitment_date,c.open_amount,c.currency_code,c.state,c.created_at,'Commitment' as fact_kind,'COMMITMENT' as cost_category from business_projects.project_commitment c join business_projects.project p on p.tenant_id=c.tenant_id and p.id=c.project_id where c.tenant_id=$1 and ($2::uuid is null or p.legal_entity_id=$2) and c.state in ('Open','PartiallyActualized') and c.open_amount>0 union all select f.id,f.project_id,p.legal_entity_id,current_date,f.eac_amount,coalesce(p.currency_code,'XXX'),f.state,f.created_at,'EAC' as fact_kind,'EAC' as cost_category from business_projects.project_forecast f join business_projects.project p on p.tenant_id=f.tenant_id and p.id=f.project_id where f.tenant_id=$1 and ($2::uuid is null or p.legal_entity_id=$2) and f.state='Approved' and f.eac_amount>0 order by 4,1";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(3); var kind = reader.GetString(8); var sourceKind = kind switch { "Cost" => "ProjectActual", "Commitment" => "Commitment", _ => "ProjectForecast" }; var dimensions = JsonSerializer.Serialize(new { projectId = reader.GetGuid(1), category = reader.GetString(9) }); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), kind, reader.GetFieldValue<DateTimeOffset>(7).UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(2), new(date.Year, date.Month, 1), "PROJECT_COST", dimensions, sourceKind, reader.GetDecimal(4), reader.GetString(5).Trim(), reader.GetGuid(0).ToString("D"), "Accrual")); }
        return facts;
    }
}
