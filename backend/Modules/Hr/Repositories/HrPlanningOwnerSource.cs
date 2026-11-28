using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

/// <summary>Exposes non-sensitive active workforce counts for driver-based planning.</summary>
public sealed class HrPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "hr";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select e.legal_entity_id,date_trunc('month',e.start_date)::date,e.status,count(*) from business_hr.employment e where e.tenant_id=$1 and ($2::uuid is null or e.legal_entity_id=$2) and e.status in ('Active','Suspended','NoticePeriod') group by e.legal_entity_id,date_trunc('month',e.start_date)::date,e.status order by 2,e.legal_entity_id,e.status";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var legalEntity = reader.GetGuid(0); var date = reader.GetFieldValue<DateOnly>(1); var status = reader.GetString(2); var identity = $"headcount:{legalEntity:D}:{date:yyyy-MM}:{status}"; var dimensions = JsonSerializer.Serialize(new { employmentStatus = status }); facts.Add(new(SourceModule, identity, "headcount", identity, legalEntity, new(date.Year, date.Month, 1), "HEADCOUNT", dimensions, "Workforce", reader.GetInt64(3), "XXX", identity, "Operational")); }
        return facts;
    }
}
