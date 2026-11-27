using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Expenses.Repositories;

/// <summary>Publishes finalized expense recognition through the owner boundary.</summary>
public sealed class ExpensesOwnerReconciliationSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "expenses";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = "select id,version,legal_entity_id,expense_date,coalesce(base_amount,amount),coalesce(base_currency_code,currency_code),project_id,task_id,cost_category from business_expenses.expense where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and expense_state='Finalized' and accounting_state='Posted' order by expense_date,id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var expenseId = reader.GetGuid(0);
            var date = reader.GetFieldValue<DateOnly>(3);
            var dimensions = new Dictionary<string, object?>
            {
                ["projectId"] = reader.IsDBNull(6) ? null : reader.GetGuid(6).ToString("D"),
                ["taskId"] = reader.IsDBNull(7) ? null : reader.GetGuid(7).ToString("D"),
                ["costCategory"] = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
            result.Add(new(SourceModule, expenseId.ToString("D"), expenseId.ToString("D"), reader.GetInt64(1).ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetGuid(2), new(date.Year, date.Month, 1), "OPEX", System.Text.Json.JsonSerializer.Serialize(dimensions), "Actual", reader.GetDecimal(4), reader.GetString(5).Trim(), expenseId.ToString("D"), "Accrual"));
        }
        return result;
    }
}
