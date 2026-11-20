using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

public sealed partial class ManufacturingRepository
{
    public Task<IReadOnlyList<ProductionOperationResponse>> ListOperationsAsync(TenantId tenant, Guid orderId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var result = new List<ProductionOperationResponse>();
            await using var command = Command(context, "select id,production_order_id,sequence_no,name,work_center_id,state,completed_qty,actual_good_qty,actual_scrap_qty,actual_minutes,quality_gate_required,row_version from business_manufacturing.production_operation where tenant_id=$1 and production_order_id=$2 order by sequence_no,id", tenant.Value, orderId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(ReadOperation(reader));
            return (IReadOnlyList<ProductionOperationResponse>)result;
        }, ct);

    public Task<ProductionOperationResponse> TransitionOperationAsync(TenantId tenant, Guid actor, Guid operationId, string action, OperationActionRequest request, Guid correlationId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.operation_idempotency_required");
            var operation = await ReadOperationForUpdateAsync(context, tenant.Value, operationId, token) ?? throw new ManufacturingException("manufacturing.operation_not_found");
            if (await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_operation_event where tenant_id=$1 and production_operation_id=$2 and idempotency_key=$3", token, tenant.Value, operationId, request.IdempotencyKey) is not null)
                return await ReadOperationAsync(context, tenant.Value, operationId, token) ?? throw new ManufacturingException("manufacturing.operation_not_found");
            if (operation.RowVersion != request.ExpectedVersion) throw new ManufacturingException("manufacturing.concurrency_conflict");

            var next = action switch
            {
                "start" when operation.State == "NotStarted" => "InProgress",
                "pause" when operation.State == "InProgress" => "Paused",
                "resume" when operation.State == "Paused" => "InProgress",
                "block" when operation.State is "InProgress" or "Paused" => "Blocked",
                "progress" when operation.State is "InProgress" or "Paused" => operation.State == "Paused" ? "Paused" : "PartiallyComplete",
                "complete" when operation.State is "InProgress" or "PartiallyComplete" => "Complete",
                _ => throw new ManufacturingException("manufacturing.operation_transition_invalid")
            };
            if (action is "progress" or "complete" && request.Quantity is not > 0) throw new ManufacturingException("manufacturing.operation_quantity_required");
            if (action == "complete" && operation.QualityGateRequired && await ScalarAsync<string?>(context, "select quality_state from business_manufacturing.production_order where tenant_id=$1 and id=$2", token, tenant.Value, operation.ProductionOrderId) is not "Passed")
                throw new ManufacturingException("manufacturing.quality_gate_pending");
            if (action == "start" && await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation p where p.tenant_id=$1 and p.production_order_id=$2 and p.sequence_no < $3 and p.state <> 'Complete')", token, tenant.Value, operation.ProductionOrderId, operation.SequenceNo))
                throw new ManufacturingException("manufacturing.operation_predecessor_incomplete");

            var quantity = request.Quantity ?? 0m;
            if (quantity > 0 && await ScalarAsync<decimal>(context, "select ordered_qty from business_manufacturing.production_order where tenant_id=$1 and id=$2", token, tenant.Value, operation.ProductionOrderId) < operation.CompletedQty + quantity)
                throw new ManufacturingException("manufacturing.operation_quantity_exceeds_order");
            var eventType = action switch { "start" => "Started", "pause" => "Paused", "resume" => "Resumed", "progress" => "Progressed", "complete" => "Completed", "block" => "Blocked", _ => throw new ManufacturingException("manufacturing.operation_transition_invalid") };
            var eventId = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.production_operation_event(tenant_id,id,production_operation_id,event_type,quantity,actual_minutes,reason,idempotency_key,actor_id,payload) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10::jsonb)", token, tenant.Value, eventId, operationId, eventType, Db(request.Quantity), Db(request.ActualMinutes), Db(request.Reason), request.IdempotencyKey, actor, JsonSerializer.Serialize(new { action, operationId }));
            await ExecuteAsync(context, "update business_manufacturing.production_operation set state=$1,started_at=case when $2='Started' then coalesce(started_at,now()) else started_at end,paused_at=case when $2 in ('Paused','Blocked') then now() when $2='Resumed' then null else paused_at end,completed_at=case when $2='Completed' then now() else completed_at end,started_by=coalesce(started_by,case when $2='Started' then $3 else null end),completed_qty=completed_qty+$4,actual_good_qty=actual_good_qty+$4,actual_minutes=coalesce(actual_minutes,0)+coalesce($5,0),row_version=row_version+1 where tenant_id=$6 and id=$7 and row_version=$8", token, next, eventType, actor, quantity, Db(request.ActualMinutes), tenant.Value, operationId, request.ExpectedVersion);
            await ExecuteAsync(context, "update business_manufacturing.production_order o set operation_state=case when exists(select 1 from business_manufacturing.production_operation p where p.tenant_id=o.tenant_id and p.production_order_id=o.id and p.state='Blocked') then 'Blocked' when exists(select 1 from business_manufacturing.production_operation p where p.tenant_id=o.tenant_id and p.production_order_id=o.id and p.state <> 'Complete') then case when exists(select 1 from business_manufacturing.production_operation p where p.tenant_id=o.tenant_id and p.production_order_id=o.id and p.state in ('InProgress','Paused','PartiallyComplete')) then 'InProgress' else 'NotStarted' end else 'Complete' end,updated_at=now(),row_version=row_version+1 where o.tenant_id=$1 and o.id=$2", token, tenant.Value, operation.ProductionOrderId);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), $"ProductionOperation{eventType}V1", JsonSerializer.Serialize(new { productionOrderId = operation.ProductionOrderId, operationId, quantity, eventType }), tenant, DateTimeOffset.UtcNow, correlationId, operation.ProductionOrderId, "manufacturing"));
            return await ReadOperationAsync(context, tenant.Value, operationId, token) ?? throw new ManufacturingException("manufacturing.operation_not_found");
        }, ct);

    public Task<GenealogyResponse> LinkGenealogyAsync(TenantId tenant, Guid actor, Guid orderId, GenealogyLinkRequest request, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.InputQuantity <= 0 || request.OutputQuantity <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.genealogy_invalid");
            if (request.Certainty is not ("Exact" or "Commingled" or "Possible")) throw new ManufacturingException("manufacturing.genealogy_invalid");
            var existing = await ReadGenealogyByKeyAsync(context, tenant.Value, request.IdempotencyKey, token);
            if (existing is not null) return existing;
            var output = await ScalarAsync<Guid?>(context, "select production_order_id from business_manufacturing.production_output where tenant_id=$1 and id=$2", token, tenant.Value, request.OutputId);
            var inputOrder = await ScalarAsync<Guid?>(context, "select r.production_order_id from business_manufacturing.production_material_event e join business_manufacturing.production_material_requirement r on r.tenant_id=e.tenant_id and r.id=e.requirement_id where e.tenant_id=$1 and e.id=$2", token, tenant.Value, request.InputEventId);
            if (output != orderId || inputOrder != orderId) throw new ManufacturingException("manufacturing.genealogy_order_mismatch");
            var allocatedInput = await ScalarAsync<decimal>(context, "select coalesce(sum(input_quantity),0) from business_manufacturing.production_genealogy where tenant_id=$1 and input_event_id=$2", token, tenant.Value, request.InputEventId);
            var eventQuantity = await ScalarAsync<decimal>(context, "select quantity from business_manufacturing.production_material_event where tenant_id=$1 and id=$2", token, tenant.Value, request.InputEventId);
            if (allocatedInput + request.InputQuantity > eventQuantity) throw new ManufacturingException("manufacturing.genealogy_input_overallocated");
            var id = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.production_genealogy(tenant_id,id,production_order_id,output_id,input_event_id,input_lot_id,output_lot_id,input_quantity,output_quantity,certainty) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, orderId, request.OutputId, request.InputEventId, Db(request.InputLotId), Db(request.OutputLotId), request.InputQuantity, request.OutputQuantity, request.Certainty);
            await ExecuteAsync(context, "insert into business_manufacturing.manufacturing_idempotency(tenant_id,id,operation,idempotency_key,request_hash,response_json) values($1,$2,'genealogy',$3,$4,$5::jsonb)", token, tenant.Value, Guid.CreateVersion7(), request.IdempotencyKey, request.IdempotencyKey, JsonSerializer.Serialize(new { genealogyId = id }));
            await AuditAsync(context, tenant.Value, "ProductionGenealogy", id, "linked", actor, null, request, token);
            return new GenealogyResponse(id, orderId, request.OutputId, request.InputEventId, request.InputLotId, request.OutputLotId, request.InputQuantity, request.OutputQuantity, request.Certainty);
        }, ct);

    public Task<IReadOnlyList<GenealogyResponse>> ListForwardGenealogyAsync(TenantId tenant, Guid outputId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) => await ReadGenealogyListAsync(context, tenant.Value, "output_id=$2", outputId, token), ct);

    public Task<IReadOnlyList<GenealogyResponse>> ListReverseGenealogyAsync(TenantId tenant, Guid inputEventId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) => await ReadGenealogyListAsync(context, tenant.Value, "input_event_id=$2", inputEventId, token), ct);

    public Task<ProductionCloseReadinessResponse> GetCloseReadinessAsync(TenantId tenant, Guid orderId, CancellationToken ct) =>
        coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
            var material = await ReadMaterialTotalsAsync(context, tenant.Value, orderId, token);
            var operationsComplete = !await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and production_order_id=$2 and state <> 'Complete')", token, tenant.Value, orderId);
            var outputUnreconciled = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_output where tenant_id=$1 and production_order_id=$2 and inventory_receipt_status <> 'Committed')", token, tenant.Value, orderId);
            var wip = await ScalarAsync<decimal>(context, "select greatest(coalesce((select sum(amount) from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and production_order_id=$2),0)-coalesce((select sum(final_value) from business_manufacturing.production_output where tenant_id=$1 and production_order_id=$2 and inventory_receipt_status='Committed'),0),0)", token, tenant.Value, orderId);
            var reason = order.CompletedQty < order.OrderedQty ? "manufacturing.output_incomplete" : material.Issued > material.Accounted ? "manufacturing.material_unaccounted" : !operationsComplete ? "manufacturing.operations_incomplete" : outputUnreconciled ? "manufacturing.output_unreconciled" : order.QualityState is "Pending" or "Blocked" ? "manufacturing.quality_unreconciled" : order.CostState is "Uncosted" or "Accumulating" ? "manufacturing.cost_unreconciled" : null;
            return new ProductionCloseReadinessResponse(orderId, reason is null ? "Ready" : "Blocked", material.Issued, material.Accounted, order.CompletedQty, order.ScrapQty, operationsComplete, order.QualityState, order.CostState, wip, reason);
        }, ct);

    private static ProductionOperationResponse ReadOperation(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetDecimal(9), reader.GetBoolean(10), reader.GetInt64(11));
    private static async Task<ProductionOperationResponse?> ReadOperationAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        await using var command = Command(context, "select id,production_order_id,sequence_no,name,work_center_id,state,completed_qty,actual_good_qty,actual_scrap_qty,actual_minutes,quality_gate_required,row_version from business_manufacturing.production_operation where tenant_id=$1 and id=$2", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadOperation(reader) : null;
    }
    private static async Task<ProductionOperationResponse?> ReadOperationForUpdateAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        await using var command = Command(context, "select id,production_order_id,sequence_no,name,work_center_id,state,completed_qty,actual_good_qty,actual_scrap_qty,actual_minutes,quality_gate_required,row_version from business_manufacturing.production_operation where tenant_id=$1 and id=$2 for update", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadOperation(reader) : null;
    }
    private static async Task<GenealogyResponse?> ReadGenealogyByKeyAsync(ITransactionContext context, Guid tenant, string key, CancellationToken token)
    {
        await using var command = Command(context, "select g.id,g.production_order_id,g.output_id,g.input_event_id,g.input_lot_id,g.output_lot_id,g.input_quantity,g.output_quantity,g.certainty from business_manufacturing.production_genealogy g join business_manufacturing.manufacturing_idempotency i on i.tenant_id=g.tenant_id and i.operation='genealogy' and i.idempotency_key=$2 and i.response_json->>'genealogyId'=g.id::text where g.tenant_id=$1", tenant, key);
        await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadGenealogy(reader) : null;
    }
    private static async Task<IReadOnlyList<GenealogyResponse>> ReadGenealogyListAsync(ITransactionContext context, Guid tenant, string predicate, Guid id, CancellationToken token)
    {
        var result = new List<GenealogyResponse>();
        await using var command = Command(context, $"select id,production_order_id,output_id,input_event_id,input_lot_id,output_lot_id,input_quantity,output_quantity,certainty from business_manufacturing.production_genealogy where tenant_id=$1 and {predicate} order by created_at,id", tenant, id);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) result.Add(ReadGenealogy(reader)); return result;
    }
    private static GenealogyResponse ReadGenealogy(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetString(8));
    private static async Task<(decimal Issued, decimal Accounted)> ReadMaterialTotalsAsync(ITransactionContext context, Guid tenant, Guid orderId, CancellationToken token)
    {
        await using var command = Command(context, "select coalesce(sum(issued_qty),0),coalesce(sum(consumed_qty+returned_qty+scrapped_qty),0) from business_manufacturing.production_material_requirement where tenant_id=$1 and production_order_id=$2", tenant, orderId);
        await using var reader = await command.ExecuteReaderAsync(token); await reader.ReadAsync(token); return (reader.GetDecimal(0), reader.GetDecimal(1));
    }
}
