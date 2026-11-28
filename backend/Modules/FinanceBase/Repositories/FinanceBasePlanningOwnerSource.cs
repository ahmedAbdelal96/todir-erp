using System.Text.Json;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Repositories;

/// <summary>Exposes approved FX references; FinBase remains the currency and rate authority.</summary>
public sealed class FinanceBasePlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "finbase";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select id,legal_entity_id,effective_date,from_currency,to_currency,rate,purpose,coalesce(version,1) from foundation_finbase.exchange_rate where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and status='Approved' order by effective_date,id";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); var from = reader.GetString(3).Trim(); var to = reader.GetString(4).Trim(); var dimensions = JsonSerializer.Serialize(new { fromCurrency = from, purpose = reader.GetString(6) }); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), $"{from}:{to}", reader.GetInt64(7).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(1), new(date.Year, date.Month, 1), "FX_RATE", dimensions, "PlanningAssumption", reader.GetDecimal(5), to, reader.GetGuid(0).ToString("D"), "Rate")); }
        return facts;
    }
}
