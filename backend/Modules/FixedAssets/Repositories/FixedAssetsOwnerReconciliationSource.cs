using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Repositories;

/// <summary>Publishes posted depreciation facts through the owner boundary for Planning.</summary>
public sealed class FixedAssetsOwnerReconciliationSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "fixed-assets";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = "select r.id,l.id,r.legal_entity_id,r.period_start,l.amount,b.currency_code,coalesce(l.ledger_fact_reference,l.id::text) from business_assets.asset_depreciation_run r join business_assets.asset_depreciation_run_line l on l.tenant_id=r.tenant_id and l.run_id=r.id join business_assets.asset_book b on b.tenant_id=l.tenant_id and b.id=l.asset_book_id where r.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) and r.status='Posted' and l.amount>0 order by r.period_start,r.id,l.id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var period = reader.GetFieldValue<DateOnly>(3);
            result.Add(new(SourceModule, reader.GetString(6), reader.GetGuid(1).ToString("D"), reader.GetGuid(0).ToString("D"), reader.GetGuid(2), new(period.Year, period.Month, 1), "DEPRECIATION", "{}", "Actual", reader.GetDecimal(4), reader.GetString(5).Trim(), $"depreciation:{reader.GetGuid(0):D}:{reader.GetGuid(1):D}", "Accrual"));
        }
        return result;
    }
}
