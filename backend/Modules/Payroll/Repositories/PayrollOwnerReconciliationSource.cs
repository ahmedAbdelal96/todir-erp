using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Payroll.Repositories;

/// <summary>Publishes finalized payroll recognition facts without exposing Payroll persistence to Planning.</summary>
public sealed class PayrollOwnerReconciliationSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "payroll";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = "select f.id,r.id,r.accounting_state,r.legal_entity_id,coalesce(p.period_start,r.created_at::date),f.total_amount,f.currency_code from business_payroll.accounting_fact f join business_payroll.payroll_run r on r.tenant_id=f.tenant_id and r.id=f.run_id left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where f.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) and f.fact_type='PayrollPosted' and f.status='Posted' and r.finalization_state='Finalized' order by p.period_start,r.id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var period = reader.GetFieldValue<DateOnly>(4);
            result.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), reader.GetGuid(1).ToString("D"), reader.GetString(2), reader.GetGuid(3), new(period.Year, period.Month, 1), "OPEX", "{\"source\":\"payroll\"}", "Actual", reader.GetDecimal(5), reader.GetString(6).Trim(), $"payroll:{reader.GetGuid(1):D}", "Accrual"));
        }
        return result;
    }
}
