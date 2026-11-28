using System.Globalization;
using Npgsql;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Sales.Repositories;

/// <summary>Exposes confirmed sales-order drivers without becoming the revenue ledger.</summary>
public sealed class SalesPlanningOwnerSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "sales";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenant.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = """
            select o.id,o.legal_entity_id,o.order_date,o.currency_code,o.version,
                   sum((l.ordered_quantity-l.cancelled_quantity)*l.unit_price)
            from business_sales.sales_order o
            join business_sales.sales_order_line l on l.tenant_id=o.tenant_id and l.legal_entity_id=o.legal_entity_id and l.order_id=o.id
            where o.tenant_id=$1 and ($2::uuid is null or o.legal_entity_id=$2) and o.commercial_state='Confirmed'
            group by o.id,o.legal_entity_id,o.order_date,o.currency_code,o.version
            order by o.order_date,o.id
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var facts = new List<PlanningOwnerFinancialFact>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var date = reader.GetFieldValue<DateOnly>(2);
            facts.Add(new(SourceModule, reader.GetGuid(0).ToString("D"), "order-total", reader.GetInt64(4).ToString(CultureInfo.InvariantCulture), reader.GetGuid(1), new(date.Year, date.Month, 1), "REVENUE_PIPELINE", "{}", "Forecast", reader.GetDecimal(5), reader.GetString(3).Trim(), reader.GetGuid(0).ToString("D"), "Commercial"));
        }

        return facts;
    }
}
