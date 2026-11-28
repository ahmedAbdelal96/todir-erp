using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Subledger.Repositories;

/// <summary>Exposes open receivable/payable balances for DSO/DPO planning drivers.</summary>
public sealed class SubledgerPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "subledger";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select id,legal_entity_id,due_date,direction,open_base_carrying_amount,base_currency,source_version,source_reference from finance_subledger.party_open_item where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and is_open and open_base_carrying_amount>0 order by due_date,id";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); var direction = reader.GetString(3); var dimensions = JsonSerializer.Serialize(new { direction }); facts.Add(new(SourceModule, reader.GetString(7), "open-item", reader.GetInt64(6).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(1), new(date.Year, date.Month, 1), direction == "Receivable" ? "RECEIVABLE_OPEN" : "PAYABLE_OPEN", dimensions, "WorkingCapital", reader.GetDecimal(4), reader.GetString(5).Trim(), reader.GetGuid(0).ToString("D"), "Accrual")); }
        return facts;
    }
}
