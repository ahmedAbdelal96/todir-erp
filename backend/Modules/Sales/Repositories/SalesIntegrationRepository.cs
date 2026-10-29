using Modules.Sales.DTOs;
using Modules.Sales.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;
using Core.Transactions;

namespace Modules.Sales.Repositories;

public sealed partial class SalesRepository
{
    public async Task RecordCreditDecisionAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid orderId,
        string decision, string policyCode, decimal? availableCredit, decimal proposedExposure, long? exposureVersion,
        string? reason, CancellationToken ct) => await ExecuteSimpleAsync(tenant, async (context, token) =>
    {
        var now = clock.GetUtcNow();
        await ExecuteAsync(context, "update business_sales.sales_order set credit_state=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, orderId, decision, now);
        await ExecuteAsync(context, "insert into business_sales.sales_credit_decision(tenant_id,id,legal_entity_id,order_id,decision,policy_code,available_credit,proposed_exposure,exposure_version,reason,actor_id,decided_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, orderId, decision, policyCode, availableCredit ?? (object)DBNull.Value, proposedExposure, exposureVersion ?? (object)DBNull.Value, reason ?? (object)DBNull.Value, actor, now);
        AddOutbox(context, tenant, "SalesCreditDecisionRecordedV1", new { orderId, decision, policyCode, proposedExposure }, now, orderId);
    }, ct);

    public async Task<IReadOnlyList<SalesReservationView>> ListReservationsAsync(TenantId tenant, Guid legalEntityId, Guid orderId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select order_line_id,inventory_reservation_id,coalesce(reserved_quantity,quantity),state,coalesce(consumed_quantity,0),coalesce(released_quantity,0),coalesce(remaining_quantity,quantity) from business_sales.sales_order_reservation where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 order by order_line_id", connection);
        Add(command, tenant.Value, legalEntityId, orderId); await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<SalesReservationView>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6))); return result;
    }

    public Task RecordReservationAsync(TenantId tenant, Guid legalEntityId, Guid orderId, Guid orderLineId, Guid inventoryReservationId, decimal quantity, CancellationToken ct) => ExecuteSimpleAsync(tenant, async (context, token) =>
    {
        await ExecuteAsync(context, "insert into business_sales.sales_order_reservation(tenant_id,id,legal_entity_id,order_id,order_line_id,inventory_reservation_id,quantity,reserved_quantity,consumed_quantity,released_quantity,remaining_quantity,state,created_at) values($1,$2,$3,$4,$5,$6,$7,$7,0,0,$7,'Active',$8) on conflict(tenant_id,order_line_id) do update set inventory_reservation_id=excluded.inventory_reservation_id,quantity=excluded.quantity,reserved_quantity=excluded.reserved_quantity,consumed_quantity=0,released_quantity=0,remaining_quantity=excluded.remaining_quantity,state='Active',released_at=null", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, orderId, orderLineId, inventoryReservationId, quantity, clock.GetUtcNow());
    }, ct);

    public Task MarkReservationReleasedAsync(TenantId tenant, Guid legalEntityId, Guid orderId, Guid reservationId, string state, CancellationToken ct) => ExecuteSimpleAsync(tenant, async (context, token) =>
    {
        await ExecuteAsync(context, "update business_sales.sales_order_reservation set released_quantity=remaining_quantity,remaining_quantity=0,state=$4,released_at=$5 where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and inventory_reservation_id=$6 and state='Active'", token, tenant.Value, legalEntityId, orderId, state, clock.GetUtcNow(), reservationId);
    }, ct);

    public Task RecordReservationConsumedAsync(TenantId tenant, Guid legalEntityId, Guid orderId, Guid orderLineId,
        Guid inventoryReservationId, Guid inventoryOperationId, decimal quantity, string idempotencyKey, CancellationToken ct)
        => RecordReservationConsumptionsAsync(tenant, legalEntityId, orderId,
            [(orderLineId, inventoryReservationId, inventoryOperationId, quantity, idempotencyKey)], ct);

    public Task RecordReservationConsumptionsAsync(TenantId tenant, Guid legalEntityId, Guid orderId,
        IReadOnlyList<(Guid orderLineId, Guid inventoryReservationId, Guid inventoryOperationId, decimal quantity, string idempotencyKey)> lines,
        CancellationToken ct) => ExecuteSimpleAsync(tenant, async (context, token) =>
        {
            foreach (var line in lines)
            {
                var inserted = await ExecuteScalarAsync(context, "insert into business_sales.sales_reservation_consumption(tenant_id,id,legal_entity_id,order_id,order_line_id,inventory_reservation_id,inventory_operation_id,quantity,idempotency_key,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10) on conflict(tenant_id,order_line_id,idempotency_key) do nothing returning id", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, orderId, line.orderLineId, line.inventoryReservationId, line.inventoryOperationId, line.quantity, line.idempotencyKey.Trim(), clock.GetUtcNow());
                if (inserted is null) continue;
                var updated = await ExecuteScalarAsync(context, "update business_sales.sales_order_reservation set consumed_quantity=consumed_quantity+$6,remaining_quantity=remaining_quantity-$6,state=case when remaining_quantity-$6=0 then 'Consumed' else 'Active' end where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and order_line_id=$4 and inventory_reservation_id=$5 and remaining_quantity >= $6 returning id", token, tenant.Value, legalEntityId, orderId, line.orderLineId, line.inventoryReservationId, line.quantity);
                if (updated is not Guid) throw new SalesException("sales.reservation_consumption_conflict");
            }
        }, ct);

    public async Task<SalesFulfillmentResult> RecordFulfillmentAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid orderId, string idempotencyKey,
        IReadOnlyList<(Guid lineId, decimal requested, decimal fulfilled, Guid? operationId)> lines, CancellationToken ct)
    {
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var existing = new NpgsqlCommand("select id,state from business_sales.sales_fulfillment where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and idempotency_key=$4", context.Connection, context.Transaction))
            { Add(existing, tenant.Value, legalEntityId, orderId, idempotencyKey.Trim()); await using var reader = await existing.ExecuteReaderAsync(token); if (await reader.ReadAsync(token)) return new SalesFulfillmentResult(reader.GetGuid(0), [], reader.GetString(1)); }
            var fulfillmentId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await ExecuteAsync(context, "insert into business_sales.sales_fulfillment(tenant_id,id,legal_entity_id,order_id,idempotency_key,state,created_by,created_at) values($1,$2,$3,$4,$5,'Completed',$6,$7)", token, tenant.Value, fulfillmentId, legalEntityId, orderId, idempotencyKey.Trim(), actor, now);
            var output = new List<SalesInventoryFulfillmentLine>();
            foreach (var line in lines)
            {
                var updated = await ExecuteScalarAsync(context, "update business_sales.sales_order_line set fulfilled_quantity=fulfilled_quantity+$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fulfilled_quantity+cancelled_quantity+$4<=ordered_quantity returning id", token, tenant.Value, legalEntityId, line.lineId, line.fulfilled);
                if (updated is not Guid) throw new SalesException("sales.fulfillment_conflict");
                await ExecuteAsync(context, "insert into business_sales.sales_fulfillment_line(tenant_id,id,legal_entity_id,fulfillment_id,order_line_id,requested_quantity,fulfilled_quantity,inventory_operation_id) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, fulfillmentId, line.lineId, line.requested, line.fulfilled, line.operationId ?? (object)DBNull.Value);
                output.Add(new(line.lineId, line.requested, line.fulfilled, line.operationId));
            }
            await ExecuteAsync(context, "update business_sales.sales_order set fulfillment_state=case when not exists(select 1 from business_sales.sales_order_line where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and fulfilled_quantity+cancelled_quantity<ordered_quantity) then 'Fulfilled' when exists(select 1 from business_sales.sales_order_line where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and fulfilled_quantity>0) then 'Backordered' else 'Pending' end,version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, orderId, now);
            AddOutbox(context, tenant, "SalesFulfillmentCompletedV1", new { orderId, fulfillmentId, lines = output }, now, fulfillmentId);
            return new SalesFulfillmentResult(fulfillmentId, output, "Completed");
        }, ct);
    }
}
