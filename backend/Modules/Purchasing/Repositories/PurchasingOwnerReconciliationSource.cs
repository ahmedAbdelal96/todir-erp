using Npgsql;
using System.Globalization;
using Shared.Contracts.Planning;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing.Repositories;

public sealed class PurchasingOwnerReconciliationSource(NpgsqlDataSource dataSource) : IPlanningOwnerReconciliationSource
{
    public string SourceModule => "purchasing";

    public async Task<IReadOnlyList<PlanningOwnerFinancialFact>> ListFinancialFactsAsync(TenantId tenantId, Guid? legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); tenant.Parameters.AddWithValue(tenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(cancellationToken);
        var result = new List<PlanningOwnerFinancialFact>();
        const string requisitions = "select r.id,l.id,r.row_version,r.legal_entity_id,r.requested_date,(l.requested_quantity-l.converted_quantity-l.cancelled_quantity)*l.unit_price,r.currency_code from business_purchasing.purchase_requisition r join business_purchasing.purchase_requisition_line l on l.tenant_id=r.tenant_id and l.legal_entity_id=r.legal_entity_id and l.requisition_id=r.id where r.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) and r.state in ('Approved','PartiallyConverted') and l.requested_quantity-l.converted_quantity-l.cancelled_quantity>0";
        await using (var command = new NpgsqlCommand(requisitions, connection))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(new("purchasing", reader.GetGuid(0).ToString("D"), reader.GetGuid(1).ToString("D"), reader.GetInt64(2).ToString(CultureInfo.InvariantCulture), reader.GetGuid(3), new(reader.GetFieldValue<DateOnly>(4).Year, reader.GetFieldValue<DateOnly>(4).Month, 1), "OPEX", "{}", "Precommitment", reader.GetDecimal(5), reader.GetString(6).Trim()));
        }
        const string orders = "select o.id,l.id,o.row_version,o.legal_entity_id,o.order_date,(l.ordered_quantity-l.received_quantity-l.cancelled_quantity-l.closed_short_quantity)*l.unit_price,o.currency_code from business_purchasing.purchase_order o join business_purchasing.purchase_order_line l on l.tenant_id=o.tenant_id and l.legal_entity_id=o.legal_entity_id and l.purchase_order_id=o.id where o.tenant_id=$1 and ($2::uuid is null or o.legal_entity_id=$2) and o.state in ('Approved','Open','PartiallyCompleted') and l.ordered_quantity-l.received_quantity-l.cancelled_quantity-l.closed_short_quantity>0";
        await using (var command = new NpgsqlCommand(orders, connection))
        {
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.HasValue ? legalEntityId.Value : DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(new("purchasing", reader.GetGuid(0).ToString("D"), reader.GetGuid(1).ToString("D"), reader.GetInt64(2).ToString(CultureInfo.InvariantCulture), reader.GetGuid(3), new(reader.GetFieldValue<DateOnly>(4).Year, reader.GetFieldValue<DateOnly>(4).Month, 1), "OPEX", "{}", "Commitment", reader.GetDecimal(5), reader.GetString(6).Trim()));
        }
        return result;
    }
}
