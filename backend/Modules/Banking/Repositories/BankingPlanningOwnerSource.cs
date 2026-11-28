using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Banking.Repositories;

/// <summary>Exposes Banking-owned cash-position projections without duplicating Ledger actuals.</summary>
public sealed class BankingPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "banking";
    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken); }
        const string sql = "select treasury_account_id,legal_entity_id,as_of::date,currency_code,coalesce(available_balance,coalesce(book_balance,0)+coalesce(in_transit_amount,0),0),coalesce(source_watermark,as_of) from finance_banking.cash_position_projection where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) order by as_of,treasury_account_id,currency_code";
        await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken)) { var date = reader.GetFieldValue<DateOnly>(2); var currency = reader.GetString(3).Trim(); var watermark = reader.GetFieldValue<DateTimeOffset>(5).UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture); facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), currency, watermark, reader.GetGuid(1), new(date.Year, date.Month, 1), "CASH_POSITION", "{}", "TreasuryForecast", reader.GetDecimal(4), currency, reader.GetGuid(0).ToString("D"), "Cash")); }
        return facts;
    }
}
