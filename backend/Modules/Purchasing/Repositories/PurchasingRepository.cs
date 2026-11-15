using System.Globalization;
using System.Text.Json;
using Core.Transactions;
using Modules.Purchasing.DTOs;
using Modules.Purchasing.Entities;
using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing.Repositories;

public sealed class PurchasingRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Guid> CreateAsync(TenantId tenant, Guid actor, CreatePurchaseOrderRequest request, bool approvalRequired, IReadOnlyList<ResolvedLine> lines, Guid correlation, CancellationToken ct)
    {
        var id = Uuid7.NewGuid();
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        var state = approvalRequired ? PurchaseOrderStates.PendingApproval : PurchaseOrderStates.Draft;
        var approval = approvalRequired ? "Pending" : "NotRequired";
        var total = lines.Sum(x => x.Quantity * x.UnitPrice);
        await ExecuteAsync(connection, transaction, """
            insert into business_purchasing.purchase_order(tenant_id,legal_entity_id,id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,matching_state,price_list_id,project_id,task_id,cost_category,net_total,gross_total,created_by,created_at,updated_at)
            values($1,$2,$3,$4,$5,$6,$7,$8,$9,'NotReceived','NotInvoiced','NotRequired',$10,$11,$12,$13,$14,$14,$15,$16,$16)
            """, ct, tenant.Value, request.LegalEntityId, id, request.BranchId, request.SupplierPartyId, request.CurrencyCode, request.OrderDate, state, approval, request.PriceListId, request.ProjectId, request.TaskId, request.CostCategory, total, actor, now);
        await InsertLinesAsync(connection, transaction, tenant, request.LegalEntityId, id, lines, now, ct);
        await AddHistoryAsync(connection, transaction, tenant, request.LegalEntityId, id, null, state, "Created", actor, correlation, now, ct);
        await transaction.CommitAsync(ct);
        return id;
    }

    public async Task<long> UpdateAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, UpdatePurchaseOrderRequest request, IReadOnlyList<ResolvedLine> lines, Guid correlation, CancellationToken ct, bool amendment = false, string? reason = null)
    {
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderAsync(connection, transaction, tenant, legalEntityId, id, ct);
        if (current is null) throw new PurchasingException("purchasing.po_not_found");
        if (current.Value.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        if (!amendment && current.Value.State is not (PurchaseOrderStates.Draft or PurchaseOrderStates.PendingApproval)) throw new PurchasingException("purchasing.approved_requires_amendment");
        if (amendment && current.Value.State is PurchaseOrderStates.Cancelled or PurchaseOrderStates.Closed) throw new PurchasingException("purchasing.po_not_amendable");
        if (lines.Count == 0) throw new PurchasingException("purchasing.lines_required");
        var total = lines.Sum(x => x.Quantity * x.UnitPrice);
        if (amendment && lines.Any(x => x.Quantity < 0)) throw new PurchasingException("purchasing.quantity_invalid");
        if (amendment) await ExecuteAsync(connection, transaction, "select set_config('app.purchasing_amendment','true',true)", ct);
        var nextState = amendment ? PurchaseOrderStates.PendingApproval : current.Value.State;
        var nextApproval = amendment ? "Pending" : current.Value.Approval;
        await ExecuteAsync(connection, transaction, """
            update business_purchasing.purchase_order set supplier_party_id=$5,currency_code=$6,order_date=$7,project_id=$12,task_id=$13,cost_category=$14,state=$8,approval_state=$9,approved_by=case when $9='Approved' then approved_by else null end,approved_at=case when $9='Approved' then approved_at else null end,net_total=$10,gross_total=$10,row_version=row_version+1,updated_at=$11
            where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$4
            """, ct, tenant.Value, current.Value.EntityId, id, request.Version, request.SupplierPartyId, request.CurrencyCode, request.OrderDate, nextState, nextApproval, total, now, request.ProjectId, request.TaskId, request.CostCategory);
        await ExecuteAsync(connection, transaction, "delete from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3", ct, tenant.Value, current.Value.EntityId, id);
        await InsertLinesAsync(connection, transaction, tenant, current.Value.EntityId, id, lines, now, ct);
        if (amendment)
        {
            var amendmentId = Uuid7.NewGuid();
            await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_amendment(tenant_id,legal_entity_id,purchase_order_id,id,amendment_number,reason,before_snapshot,after_snapshot,actor_id,created_at) values($1,$2,$3,$4,(select coalesce(max(amendment_number),0)+1 from business_purchasing.purchase_order_amendment where tenant_id=$1 and purchase_order_id=$3),$5,$6::jsonb,$7::jsonb,$8,$9)", ct, tenant.Value, current.Value.EntityId, id, amendmentId, reason ?? "Amended", "{}", JsonSerializer.Serialize(lines, Json), actor, now);
        }
        await AddHistoryAsync(connection, transaction, tenant, current.Value.EntityId, id, current.Value.State, nextState, amendment ? "Amended" : "Updated", actor, correlation, now, ct);
        await transaction.CommitAsync(ct);
        return request.Version + 1;
    }

    public async Task<PurchaseOrderView?> GetAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => await GetAsync(tenant, legalEntityId, id, true, ct);

    public async Task<PurchaseOrderView?> GetAsync(TenantId tenant, Guid legalEntityId, Guid id, bool includePrice, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at,project_id,task_id,cost_category from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var header = new Header(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), reader.GetDecimal(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13), reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.IsDBNull(15) ? null : reader.GetGuid(15), reader.IsDBNull(16) ? null : reader.GetString(16));
        await reader.CloseAsync();
        var lines = await ReadLinesAsync(connection, tenant, legalEntityId, id, includePrice, ct);
        return new(header.Id, header.EntityId, header.BranchId, header.SupplierPartyId, header.Currency, header.OrderDate, header.State, header.Approval, header.Receipt, header.Invoicing, header.Version, header.Total, lines, header.CreatedAt, header.UpdatedAt, header.ProjectId, header.TaskId, header.CostCategory);
    }

    public async Task<PurchaseOrderPage> ListAsync(TenantId tenant, Guid legalEntityId, string? state, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, null, tenant, ct);
        var offset = (page - 1) * pageSize;
        await using var countCommand = new NpgsqlCommand("select count(*) from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and ($3 is null or state=$3) and ($4 is null or id::text ilike '%' || $4 || '%' or supplier_party_id::text ilike '%' || $4 || '%')", connection);
        countCommand.Parameters.AddWithValue(tenant.Value); countCommand.Parameters.AddWithValue(legalEntityId); countCommand.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)state ?? DBNull.Value); countCommand.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id,supplier_party_id,order_date,state,currency_code,net_total,row_version,updated_at from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and ($3 is null or state=$3) and ($4 is null or id::text ilike '%' || $4 || '%' or supplier_party_id::text ilike '%' || $4 || '%') order by order_date desc,id desc offset $5 limit $6", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)state ?? DBNull.Value); command.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim()); command.Parameters.AddWithValue(offset); command.Parameters.AddWithValue(pageSize);
        var items = new List<PurchaseOrderListItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7)));
        return new(items, page, pageSize, (page * pageSize) < total, total);
    }

    public Task<bool> IsApprovedAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => StateAsync(tenant, legalEntityId, id, "Approved", ct);

    public async Task<PurchaseReceiptResult?> GetReceiptReplayAsync(TenantId tenant, Guid entity, Guid orderId, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select distinct receipt_id from business_purchasing.purchase_order_receipt_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and idempotency_key=$4 order by receipt_id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(orderId); command.Parameters.AddWithValue(idempotencyKey);
        var ids = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
        if (ids.Count == 0) return null; var order = await GetAsync(tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found"); return new(orderId, ids, order.Version, order.ReceiptState);
    }

    public async Task<PurchaseWorkflowView?> GetWorkflowAsync(TenantId tenant, Guid entity, Guid orderId, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select payload_hash,state,receipt_payload::text,invoice_payload::text,invoice_id,last_error from business_purchasing.purchase_order_workflow where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and workflow_type='ReceiveAndBill' and idempotency_key=$4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(orderId); command.Parameters.AddWithValue(idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5)) : null;
    }

    public async Task<PurchaseWorkflowView> StartWorkflowAsync(TenantId tenant, Guid entity, Guid orderId, string idempotencyKey, string payloadHash, string receiptPayload, string invoicePayload, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_workflow(tenant_id,legal_entity_id,purchase_order_id,workflow_type,idempotency_key,payload_hash,state,receipt_payload,invoice_payload,created_at,updated_at) values($1,$2,$3,'ReceiveAndBill',$4,$5,'Started',$6::jsonb,$7::jsonb,now(),now()) on conflict(tenant_id,purchase_order_id,workflow_type,idempotency_key) do nothing", ct, tenant.Value, entity, orderId, idempotencyKey, payloadHash, receiptPayload, invoicePayload);
        await transaction.CommitAsync(ct);
        var result = await GetWorkflowAsync(tenant, entity, orderId, idempotencyKey, ct) ?? throw new PurchasingException("purchasing.workflow_not_found");
        if (!string.Equals(result.PayloadHash, payloadHash, StringComparison.Ordinal)) throw new PurchasingException("purchasing.idempotency_conflict");
        return result;
    }

    public async Task SetWorkflowStateAsync(TenantId tenant, Guid entity, Guid orderId, string idempotencyKey, string state, Guid? invoiceId, string? error, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await ExecuteAsync(connection, null, "update business_purchasing.purchase_order_workflow set state=$5,invoice_id=coalesce($6,invoice_id),last_error=$7,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and workflow_type='ReceiveAndBill' and idempotency_key=$4", ct, tenant.Value, entity, orderId, idempotencyKey, state, invoiceId, error);
    }

    public async Task<PurchaseReceiptResult> ApplyReceiptAsync(TenantId tenant, Guid actor, Guid entity, Guid orderId, ReceivePurchaseOrderRequest request, Shared.Contracts.Inventory.PurchaseReceiptResult inventoryResult, Guid correlation, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderAsync(connection, transaction, tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        var replayCount = await ExecuteScalarAsync<long>(connection, transaction, "select count(*) from business_purchasing.purchase_order_receipt_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and idempotency_key=$4", ct, tenant.Value, entity, orderId, request.IdempotencyKey);
        if (replayCount > 0)
        { await transaction.CommitAsync(ct); var replayed = await GetAsync(tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found"); return new(orderId, inventoryResult.ReceiptIds, replayed.Version, replayed.ReceiptState); }
        if (current.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        var policy = await ReadReceiptPolicyAsync(connection, transaction, tenant, entity, ct);
        foreach (var received in inventoryResult.Lines)
        {
            await using var lineCommand = new NpgsqlCommand("select ordered_quantity,cancelled_quantity,closed_short_quantity,received_quantity from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and id=$4 for update", connection, transaction);
            lineCommand.Parameters.AddWithValue(tenant.Value); lineCommand.Parameters.AddWithValue(entity); lineCommand.Parameters.AddWithValue(orderId); lineCommand.Parameters.AddWithValue(received.PurchaseOrderLineId);
            await using var lineReader = await lineCommand.ExecuteReaderAsync(ct); if (!await lineReader.ReadAsync(ct)) throw new PurchasingException("purchasing.po_line_not_found");
            var ordered = lineReader.GetDecimal(0); var cancelled = lineReader.GetDecimal(1); var shorted = lineReader.GetDecimal(2); var alreadyReceived = lineReader.GetDecimal(3); await lineReader.CloseAsync();
            var baseLimit = ordered - cancelled - shorted;
            var limit = policy.Policy switch
            {
                "Allow" => decimal.MaxValue,
                "Tolerance" => baseLimit * (1m + policy.Tolerance),
                _ => baseLimit
            };
            if (alreadyReceived + received.Quantity > limit)
                throw new PurchasingException(policy.Policy == "Approval" ? "purchasing.over_receipt_approval_required" : "purchasing.over_receipt_not_allowed");
            await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order_line set received_quantity=received_quantity+$5 where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and id=$4", ct, tenant.Value, entity, orderId, received.PurchaseOrderLineId, received.Quantity);
            await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_receipt_link(tenant_id,legal_entity_id,purchase_order_id,receipt_id,receipt_line_id,idempotency_key,created_at) values($1,$2,$3,$4,$5,$6,now()) on conflict do nothing", ct, tenant.Value, entity, orderId, received.ReceiptId, received.PurchaseOrderLineId, request.IdempotencyKey);
        }
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set receipt_state=case when (select coalesce(sum(ordered_quantity-cancelled_quantity-closed_short_quantity-received_quantity),0) from business_purchasing.purchase_order_line where tenant_id=$1 and purchase_order_id=$3)=0 then 'Received' else 'PartiallyReceived' end,state=case when state in ('Approved','Open') then 'PartiallyCompleted' else state end,row_version=row_version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$4", ct, tenant.Value, entity, orderId, request.Version);
        await AddHistoryAsync(connection, transaction, tenant, entity, orderId, current.State, "ReceiptLinked", "ReceiptLinked", actor, correlation, clock.GetUtcNow(), ct);
        await AddOutboxAsync(connection, transaction, tenant, "PurchaseOrderReceiptLinkedV1", new { purchaseOrderId = orderId, receiptIds = inventoryResult.ReceiptIds, idempotencyKey = request.IdempotencyKey }, correlation, ct);
        await transaction.CommitAsync(ct);
        var updated = await GetAsync(tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found"); return new(orderId, inventoryResult.ReceiptIds, updated.Version, updated.ReceiptState);
    }

    public async Task<long> CloseShortAsync(TenantId tenant, Guid actor, Guid entity, Guid orderId, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new PurchasingException("purchasing.close_short_reason_required");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderAsync(connection, transaction, tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (current.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        if (current.State is not (PurchaseOrderStates.Approved or PurchaseOrderStates.Open or PurchaseOrderStates.PartiallyCompleted)) throw new PurchasingException("purchasing.po_not_closable");
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order_line set closed_short_quantity=ordered_quantity-cancelled_quantity-received_quantity where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and ordered_quantity>cancelled_quantity+received_quantity", ct, tenant.Value, entity, orderId);
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set state='Closed',receipt_state='Closed',closed_reason=$4,row_version=row_version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$5", ct, tenant.Value, entity, orderId, request.Reason, request.Version);
        await AddHistoryAsync(connection, transaction, tenant, entity, orderId, current.State, "Closed", "CloseShort:" + request.Reason, actor, correlation, clock.GetUtcNow(), ct); await transaction.CommitAsync(ct); return request.Version + 1;
    }

    public async Task<PurchaseMatchView> SaveMatchAsync(TenantId tenant, Guid actor, Guid entity, Guid orderId, MatchPurchaseOrderRequest request, string state, decimal variance, string details, Guid correlation, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderAsync(connection, transaction, tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (current.Version != request.Version) throw new PurchasingException("purchasing.match_stale");
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(details))).ToLowerInvariant();
        await using (var replay = new NpgsqlCommand("select id,state,variance_amount,source_version,details::text from business_purchasing.purchase_order_match_result where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and idempotency_key=$4 for update", connection, transaction))
        {
            replay.Parameters.AddWithValue(tenant.Value); replay.Parameters.AddWithValue(entity); replay.Parameters.AddWithValue(orderId); replay.Parameters.AddWithValue(request.IdempotencyKey);
            await using var reader = await replay.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(4) != details) throw new PurchasingException("purchasing.idempotency_conflict");
                var replayed = new PurchaseMatchView(reader.GetGuid(0), orderId, request.SupplierInvoiceId, request.Basis, reader.GetString(1), reader.GetDecimal(2), reader.GetInt64(3), reader.GetString(4));
                await reader.CloseAsync(); await transaction.CommitAsync(ct); return replayed;
            }
        }
        var id = Uuid7.NewGuid();
        await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_match_result(tenant_id,legal_entity_id,purchase_order_id,id,basis,state,variance_amount,source_version,details,supplier_invoice_id,idempotency_key,payload_hash,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10,$11,$12,now())", ct, tenant.Value, entity, orderId, id, request.Basis, state, variance, request.Version, details, request.SupplierInvoiceId, request.IdempotencyKey, payloadHash);
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set matching_state=$4,row_version=row_version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$5", ct, tenant.Value, entity, orderId, state == "Matched" || state == "WithinTolerance" ? "Matched" : state, request.Version);
        await AddOutboxAsync(connection, transaction, tenant, "PurchaseMatchEvaluatedV1", new { purchaseOrderId = orderId, supplierInvoiceId = request.SupplierInvoiceId, state, variance }, correlation, ct);
        await AddHistoryAsync(connection, transaction, tenant, entity, orderId, current.State, "MatchEvaluated", "Match:" + state, actor, correlation, clock.GetUtcNow(), ct); await transaction.CommitAsync(ct);
        return new(id, orderId, request.SupplierInvoiceId, request.Basis, state, variance, request.Version, details);
    }

    public async Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, Guid entity, Guid orderId, CreateSupplierReturnRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0 || string.IsNullOrWhiteSpace(request.Reason)) throw new PurchasingException("purchasing.return_request_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var order = await ReadHeaderAsync(connection, transaction, tenant, entity, orderId, ct) ?? throw new PurchasingException("purchasing.po_not_found"); if (order.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        var id = Uuid7.NewGuid(); await ExecuteAsync(connection, transaction, "insert into business_purchasing.supplier_return_request(tenant_id,legal_entity_id,id,purchase_order_id,supplier_party_id,state,resolution,reason,version,created_by,original_invoice_id,created_at,updated_at) values($1,$2,$3,$4,$5,'Draft',$6,$7,1,$8,$9,now(),now())", ct, tenant.Value, entity, id, orderId, order.SupplierPartyId, request.Resolution, request.Reason, actor, request.OriginalInvoiceId);
        foreach (var line in request.Lines)
        {
            await using var availableCommand = new NpgsqlCommand("select l.received_quantity-coalesce((select sum(rl.quantity) from business_purchasing.supplier_return_request_line rl join business_purchasing.supplier_return_request r on r.tenant_id=rl.tenant_id and r.id=rl.return_request_id where rl.tenant_id=$1 and rl.purchase_order_line_id=$2 and r.state <> 'Cancelled'),0) from business_purchasing.purchase_order_line l where l.tenant_id=$1 and l.legal_entity_id=$3 and l.purchase_order_id=$4 and l.id=$2", connection, transaction); availableCommand.Parameters.AddWithValue(tenant.Value); availableCommand.Parameters.AddWithValue(line.PurchaseOrderLineId); availableCommand.Parameters.AddWithValue(entity); availableCommand.Parameters.AddWithValue(orderId); var available = await availableCommand.ExecuteScalarAsync(ct); if (available is not decimal remaining || line.Quantity > remaining) throw new PurchasingException("purchasing.return_quantity_exceeded");
            await ExecuteAsync(connection, transaction, "insert into business_purchasing.supplier_return_request_line(tenant_id,legal_entity_id,return_request_id,id,purchase_order_line_id,quantity,product_variant_id,unit_of_measure_id,warehouse_id,location_id,lot_id,serial_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", ct, tenant.Value, entity, id, Uuid7.NewGuid(), line.PurchaseOrderLineId, line.Quantity, line.ProductVariantId, line.UnitOfMeasureId, line.WarehouseId, line.LocationId, line.LotId, line.SerialId);
        }
        await AddOutboxAsync(connection, transaction, tenant, "SupplierReturnRequestedV1", new { returnRequestId = id, purchaseOrderId = orderId, resolution = request.Resolution }, Guid.CreateVersion7(), ct);
        await transaction.CommitAsync(ct); return id;
    }

    public async Task<SupplierReturnView?> GetReturnAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct); await using var command = new NpgsqlCommand("select id,purchase_order_id,supplier_party_id,state,resolution,reason,version,credit_note_id from business_purchasing.supplier_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetGuid(7)) : null; }

    public async Task<long> ApproveReturnAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct); var changed = await ExecuteAsync(connection, null, "update business_purchasing.supplier_return_request set state='Approved',approved_by=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Draft' and version=$5", ct, tenant.Value, entity, id, actor, version); if (changed == 0) throw new PurchasingException("purchasing.return_stale"); return version + 1; }

    public async Task CompleteReturnAsync(TenantId tenant, Guid entity, Guid id, IReadOnlyList<Guid> inventoryOperations, Guid? creditNoteId, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct); var changed = await ExecuteAsync(connection, transaction, "update business_purchasing.supplier_return_request set state='Executed',executed_at=now(),credit_note_id=$4,version=version+1,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Approved'", ct, tenant.Value, entity, id, creditNoteId); if (changed == 0) throw new PurchasingException("purchasing.return_stale"); foreach (var operation in inventoryOperations) await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_return_link(tenant_id,legal_entity_id,return_request_id,inventory_operation_id,invoice_id,created_at) values($1,$2,$3,$4,$5,now()) on conflict do nothing", ct, tenant.Value, entity, id, operation, creditNoteId); await AddOutboxAsync(connection, transaction, tenant, "SupplierReturnCompletedV1", new { returnRequestId = id, creditNoteId, inventoryOperations }, Guid.CreateVersion7(), ct); await transaction.CommitAsync(ct); }

    public async Task<Guid?> GetReturnInvoiceAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct); await using var command = new NpgsqlCommand("select original_invoice_id from business_purchasing.supplier_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); var value = await command.ExecuteScalarAsync(ct); return value is Guid invoice ? invoice : null; }

    public async Task<IReadOnlyList<SupplierReturnLineRequest>> GetReturnLinesAsync(TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct); await using var command = new NpgsqlCommand("select purchase_order_line_id,quantity,product_variant_id,unit_of_measure_id,warehouse_id,location_id,lot_id,serial_id from business_purchasing.supplier_return_request_line where tenant_id=$1 and legal_entity_id=$2 and return_request_id=$3", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<SupplierReturnLineRequest>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7))); return result; }

    public async Task<bool> ApprovalRequiredAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select approval_required from business_purchasing.purchase_settings where tenant_id=$1 and legal_entity_id=$2", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        var value = await command.ExecuteScalarAsync(ct); return value is bool required && required;
    }

    public async Task<SupplierPurchaseProfileView?> GetProfileAsync(TenantId tenant, Guid entity, Guid supplier, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select supplier_code,payment_term_id,default_currency_code,default_price_list_id,default_buyer_id,default_warehouse_id,lead_time_days,supplier_reference_required,row_version from business_purchasing.supplier_purchase_profile where tenant_id=$1 and legal_entity_id=$2 and supplier_party_id=$3 and status='Active'", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(supplier);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
        return new(entity, supplier, reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.GetBoolean(7), reader.GetInt64(8));
    }

    public async Task<long> SaveProfileAsync(TenantId tenant, Guid actor, SupplierPurchaseProfileRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await ExecuteAsync(connection, null, "insert into business_purchasing.supplier_purchase_profile(tenant_id,legal_entity_id,supplier_party_id,supplier_code,payment_term_id,default_currency_code,default_price_list_id,default_buyer_id,default_warehouse_id,lead_time_days,supplier_reference_required,purchase_price_context,status,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb,'Active',$13,now(),now()) on conflict(tenant_id,legal_entity_id,supplier_party_id) do update set supplier_code=excluded.supplier_code,payment_term_id=excluded.payment_term_id,default_currency_code=excluded.default_currency_code,default_price_list_id=excluded.default_price_list_id,default_buyer_id=excluded.default_buyer_id,default_warehouse_id=excluded.default_warehouse_id,lead_time_days=excluded.lead_time_days,supplier_reference_required=excluded.supplier_reference_required,purchase_price_context=excluded.purchase_price_context,status='Active',updated_at=now(),row_version=business_purchasing.supplier_purchase_profile.row_version+1", ct, tenant.Value, request.LegalEntityId, request.SupplierPartyId, request.SupplierCode, request.PaymentTermId, request.DefaultCurrencyCode, request.DefaultPriceListId, request.DefaultBuyerId, request.DefaultWarehouseId, request.LeadTimeDays, request.SupplierReferenceRequired, request.PurchasePriceContextJson ?? "{}", actor);
        var profile = await GetProfileAsync(tenant, request.LegalEntityId, request.SupplierPartyId, ct); return profile?.Version ?? 1;
    }

    public async Task<PurchaseSettingsView> GetSettingsAsync(TenantId tenant, Guid entity, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select approval_required,approval_threshold,over_receipt_policy,over_receipt_tolerance,invoice_without_receipt,three_way_matching,buyer_approver_separation,quantity_tolerance,price_absolute_tolerance,price_percentage_tolerance,tax_tolerance,invoice_eligibility_basis from business_purchasing.purchase_settings where tenant_id=$1 and legal_entity_id=$2", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return new(entity, false, null, "Disallow", 0, true, false, false, 0, 0, 0, 0, "PO");
        return new(entity, reader.GetBoolean(0), reader.IsDBNull(1) ? null : reader.GetDecimal(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10), reader.GetString(11));
    }

    public async Task<PurchaseSettingsView> SaveSettingsAsync(TenantId tenant, Guid actor, PurchaseSettingsRequest request, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await ExecuteAsync(connection, null, "insert into business_purchasing.purchase_settings(tenant_id,legal_entity_id,approval_required,approval_threshold,over_receipt_policy,over_receipt_tolerance,invoice_without_receipt,three_way_matching,buyer_approver_separation,quantity_tolerance,price_absolute_tolerance,price_percentage_tolerance,tax_tolerance,invoice_eligibility_basis,updated_by,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,now()) on conflict(tenant_id,legal_entity_id) do update set approval_required=excluded.approval_required,approval_threshold=excluded.approval_threshold,over_receipt_policy=excluded.over_receipt_policy,over_receipt_tolerance=excluded.over_receipt_tolerance,invoice_without_receipt=excluded.invoice_without_receipt,three_way_matching=excluded.three_way_matching,buyer_approver_separation=excluded.buyer_approver_separation,quantity_tolerance=excluded.quantity_tolerance,price_absolute_tolerance=excluded.price_absolute_tolerance,price_percentage_tolerance=excluded.price_percentage_tolerance,tax_tolerance=excluded.tax_tolerance,invoice_eligibility_basis=excluded.invoice_eligibility_basis,updated_by=excluded.updated_by,updated_at=now()", ct, tenant.Value, request.LegalEntityId, request.ApprovalRequired, request.ApprovalThreshold, request.OverReceiptPolicy, request.OverReceiptTolerance, request.InvoiceWithoutReceipt, request.ThreeWayMatching, request.BuyerApproverSeparation, request.QuantityTolerance, request.PriceAbsoluteTolerance, request.PricePercentageTolerance, request.TaxTolerance, request.InvoiceEligibilityBasis, actor);
        return await GetSettingsAsync(tenant, request.LegalEntityId, ct);
    }

    public async Task<long> ApproveAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => await TransitionAsync(tenant, actor, entity, id, request, "Approved", "Approved", "Approved", correlation, true, ct);

    public async Task<long> RejectAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => await TransitionAsync(tenant, actor, entity, id, request, "Draft", "Rejected", "Rejected", correlation, false, ct);

    public Task<long> CancelAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => TransitionAsync(tenant, actor, entity, id, request, "Cancelled", "Cancelled", "Cancelled", correlation, false, ct);

    public Task<long> CloseAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => TransitionAsync(tenant, actor, entity, id, request, "Closed", "Approved", "Closed", correlation, false, ct);

    public async Task<(Guid InvoiceId, string PayloadHash)?> GetLinkedInvoiceAsync(TenantId tenant, Guid legalEntityId, Guid id, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select invoice_id,payload_hash from business_purchasing.purchase_order_invoice_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and idempotency_key=$4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetString(1)) : null;
    }

    public async Task<Guid> LinkInvoiceAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, Guid invoiceId, string purpose, string idempotencyKey, string payloadHash, IReadOnlyDictionary<Guid, decimal> quantities, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await using (var existing = new NpgsqlCommand("select invoice_id,payload_hash from business_purchasing.purchase_order_invoice_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and purpose=$4 and idempotency_key=$5 for update", connection, transaction))
        { existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(legalEntityId); existing.Parameters.AddWithValue(id); existing.Parameters.AddWithValue(purpose); existing.Parameters.AddWithValue(idempotencyKey); await using var reader = await existing.ExecuteReaderAsync(ct); if (await reader.ReadAsync(ct)) { var linkedId = reader.GetGuid(0); var linkedHash = reader.GetString(1); await reader.CloseAsync(); if (linkedHash != payloadHash) throw new PurchasingException("purchasing.idempotency_conflict"); await transaction.CommitAsync(ct); return linkedId; } }
        var linkInserted = await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_invoice_link(tenant_id,legal_entity_id,purchase_order_id,invoice_id,purpose,idempotency_key,payload_hash,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict do nothing", ct, tenant.Value, legalEntityId, id, invoiceId, purpose, idempotencyKey, payloadHash, actor, clock.GetUtcNow());
        if (linkInserted == 0)
        {
            await using var replay = new NpgsqlCommand("select invoice_id,payload_hash from business_purchasing.purchase_order_invoice_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and purpose=$4 and idempotency_key=$5 for update", connection, transaction);
            replay.Parameters.AddWithValue(tenant.Value); replay.Parameters.AddWithValue(legalEntityId); replay.Parameters.AddWithValue(id); replay.Parameters.AddWithValue(purpose); replay.Parameters.AddWithValue(idempotencyKey);
            await using var reader = await replay.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new PurchasingException("purchasing.invoice_link_not_found");
            var linkedId = reader.GetGuid(0); var linkedHash = reader.GetString(1); await reader.CloseAsync();
            if (linkedHash != payloadHash) throw new PurchasingException("purchasing.idempotency_conflict");
            await transaction.CommitAsync(ct); return linkedId;
        }
        var settings = await ReadInvoiceEligibilityAsync(connection, transaction, tenant, legalEntityId, ct);
        foreach (var item in quantities)
        {
            await using var line = new NpgsqlCommand("select ordered_quantity,received_quantity,invoiced_quantity,cancelled_quantity,closed_short_quantity from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and id=$4 for update", connection, transaction);
            line.Parameters.AddWithValue(tenant.Value); line.Parameters.AddWithValue(legalEntityId); line.Parameters.AddWithValue(id); line.Parameters.AddWithValue(item.Key);
            await using var reader = await line.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new PurchasingException("purchasing.po_line_not_found");
            var ordered = reader.GetDecimal(0); var received = reader.GetDecimal(1); var invoiced = reader.GetDecimal(2); var cancelled = reader.GetDecimal(3); var shorted = reader.GetDecimal(4); await reader.CloseAsync();
            var available = settings == "Receipt" ? received - invoiced : ordered - cancelled - shorted - invoiced;
            if (item.Value > available) throw new PurchasingException("purchasing.invoice_quantity_exceeded");
            await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order_line set invoiced_quantity=invoiced_quantity+$5 where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and id=$4", ct, tenant.Value, legalEntityId, id, item.Key, item.Value);
        }
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set invoicing_state=case when not exists(select 1 from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and invoiced_quantity < ordered_quantity-cancelled_quantity-closed_short_quantity) then 'Invoiced' else 'PartiallyInvoiced' end,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", ct, tenant.Value, legalEntityId, id, clock.GetUtcNow());
        await AddOutboxAsync(connection, transaction, tenant, "PurchaseInvoiceLinkedV1", new { purchaseOrderId = id, invoiceId, idempotencyKey, quantities }, Guid.CreateVersion7(), ct);
        await transaction.CommitAsync(ct); return invoiceId;
    }

    public async Task<Shared.Contracts.Purchasing.PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        var order = await GetAsync(tenant, legalEntityId, id, ct); if (order is null) return null;
        return new(id, "SupplierInvoice", "", legalEntityId, order.BranchId, order.SupplierPartyId, order.OrderDate, order.OrderDate, order.OrderDate, order.CurrencyCode, order.Lines.Select(x => new Shared.Contracts.Purchasing.PurchaseOrderSourceLine(x.ProductVariantId, x.UnitOfMeasureId, x.UnitCode, x.Description, x.OrderedQuantity.ToString(CultureInfo.InvariantCulture), x.UnitPrice.ToString(CultureInfo.InvariantCulture), x.Id.ToString("D"))).ToArray(), null);
    }

    public async Task<IReadOnlyList<Guid>> GetLinkedInvoiceIdsAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select invoice_id from business_purchasing.purchase_order_invoice_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 order by created_at,invoice_id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id);
        var result = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
        return result;
    }

    public async Task<IReadOnlyList<Guid>> GetReceiptIdsAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select distinct receipt_id from business_purchasing.purchase_order_receipt_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 order by receipt_id", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id);
        var result = new List<Guid>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
        return result;
    }

    private async Task<long> TransitionAsync(TenantId tenant, Guid actor, Guid entity, Guid id, PurchaseOrderCommandRequest request, string nextState, string approval, string history, Guid correlation, bool approve, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderAsync(connection, transaction, tenant, entity, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (current.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        if (approve && current.Approval is not ("Pending" or "NotRequired")) throw new PurchasingException("purchasing.po_not_approvable");
        if (nextState == "Closed" && current.State is not ("Approved" or "Open" or "PartiallyCompleted")) throw new PurchasingException("purchasing.po_not_closable");
        if (nextState == "Cancelled" && current.State is "Cancelled" or "Closed") throw new PurchasingException("purchasing.po_already_closed");
        var persistedApproval = nextState == "Cancelled" ? current.Approval : approval;
        if (nextState == "Cancelled") await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order_line set cancelled_quantity=ordered_quantity-received_quantity-closed_short_quantity where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and ordered_quantity>received_quantity+closed_short_quantity", ct, tenant.Value, current.EntityId, id);
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set state=$4,approval_state=$5,approved_by=case when $5='Approved' then $6 else null end,approved_at=case when $5='Approved' then $7 else null end,cancelled_at=case when $4='Cancelled' then $7 else cancelled_at end,closed_reason=case when $4='Closed' then $8 else closed_reason end,row_version=row_version+1,updated_at=$7 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$9", ct, tenant.Value, current.EntityId, id, nextState, persistedApproval, actor, clock.GetUtcNow(), request.Reason, request.Version);
        if (approve || nextState == "Draft") await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_approval(tenant_id,legal_entity_id,purchase_order_id,id,decision,actor_id,reason,idempotency_key,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict do nothing", ct, tenant.Value, current.EntityId, id, Uuid7.NewGuid(), approve ? "Approved" : "Rejected", actor, request.Reason, request.IdempotencyKey, clock.GetUtcNow());
        await AddHistoryAsync(connection, transaction, tenant, current.EntityId, id, current.State, nextState, history + (request.Reason is null ? "" : ":" + request.Reason), actor, correlation, clock.GetUtcNow(), ct);
        var eventType = nextState switch { "Approved" => "PurchaseOrderApprovedV1", "Cancelled" => "PurchaseOrderCancelledV1", "Closed" => "PurchaseOrderClosedV1", _ => "PurchaseOrderAmendedV1" };
        await AddOutboxAsync(connection, transaction, tenant, eventType, new { purchaseOrderId = id, legalEntityId = current.EntityId, fromState = current.State, toState = nextState, actorId = actor, projectId = current.ProjectId, taskId = current.TaskId, costCategory = current.CostCategory, committedAmount = current.Total, currencyCode = current.Currency }, correlation, ct);
        await transaction.CommitAsync(ct); return request.Version + 1;
    }

    private static async Task<Header?> ReadHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid id, CancellationToken ct) => await ReadHeaderCoreAsync(connection, transaction, "select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at,project_id,task_id,cost_category from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", tenant, entity, id, ct);
    private static async Task<Header?> ReadHeaderByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid id, CancellationToken ct) => await ReadHeaderCoreAsync(connection, transaction, "select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at,project_id,task_id,cost_category from business_purchasing.purchase_order where tenant_id=$1 and id=$2 for update", tenant, null, id, ct);
    private static async Task<Header?> ReadHeaderCoreAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, TenantId tenant, Guid? entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenant.Value); if (entity is { } e) command.Parameters.AddWithValue(e); command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), reader.GetDecimal(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13), reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.IsDBNull(15) ? null : reader.GetGuid(15), reader.IsDBNull(16) ? null : reader.GetString(16));
    }

    private async Task<bool> StateAsync(TenantId tenant, Guid entity, Guid id, string expected, CancellationToken ct)
    { var order = await GetAsync(tenant, entity, id, ct); return order?.State == expected; }

    private static async Task<List<PurchaseOrderLineView>> ReadLinesAsync(NpgsqlConnection connection, TenantId tenant, Guid entity, Guid id, bool includePrice, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select id,sequence,product_variant_id,unit_of_measure_id,unit_code,description,ordered_quantity,case when $4 then unit_price else 0 end,invoiced_quantity,cancelled_quantity,case when $4 then price_evidence::text else null end,received_quantity,closed_short_quantity from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 order by sequence", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(includePrice); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PurchaseOrderLineView>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetDecimal(11), reader.GetDecimal(12))); return result; }

    private static async Task<string> ReadInvoiceEligibilityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select coalesce((select invoice_eligibility_basis from business_purchasing.purchase_settings where tenant_id=$1 and legal_entity_id=$2),'PO')", connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); return (string)(await command.ExecuteScalarAsync(ct) ?? "PO"); }
    private static async Task InsertLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid orderId, IReadOnlyList<ResolvedLine> lines, DateTimeOffset now, CancellationToken ct)
    { var sequence = 1; foreach (var line in lines) { await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_line(tenant_id,legal_entity_id,purchase_order_id,id,sequence,product_variant_id,unit_of_measure_id,unit_code,description,ordered_quantity,unit_price,price_evidence,price_override_reason,tax_evidence,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb,$13,$14::jsonb,$15)", ct, tenant.Value, entity, orderId, Uuid7.NewGuid(), sequence++, line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode, line.Description, line.Quantity, line.UnitPrice, line.Evidence, line.OverrideReason, line.TaxEvidence, now); } }
    private static async Task AddHistoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid id, string? from, string to, string reason, Guid actor, Guid correlation, DateTimeOffset now, CancellationToken ct) => await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_state_history(tenant_id,legal_entity_id,purchase_order_id,id,from_state,to_state,reason,actor_id,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", ct, tenant.Value, entity, id, Uuid7.NewGuid(), from, to, reason, actor, correlation, now);
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TenantId tenant, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,$2)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); command.Parameters.AddWithValue(transaction is not null); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, CancellationToken ct, params object?[] values) { await using var command = new NpgsqlCommand(sql, connection, transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<T> ExecuteScalarAsync<T>(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, CancellationToken ct, params object?[] values)
    { await using var command = new NpgsqlCommand(sql, connection, transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return (T)(await command.ExecuteScalarAsync(ct) ?? default(T)!); }
    private static async Task<(string Policy, decimal Tolerance)> ReadReceiptPolicyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select coalesce(over_receipt_policy,'Disallow'),coalesce(over_receipt_tolerance,0) from business_purchasing.purchase_settings where tenant_id=$1 and legal_entity_id=$2", connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetDecimal(1)) : ("Disallow", 0m); }
    private static async Task AddOutboxAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string messageType, object payload, Guid correlation, CancellationToken ct)
    { await ExecuteAsync(connection, transaction, "insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status,next_attempt_at) values($1,$2,$3::jsonb,$4,$5,$6,$6,'purchasing',null,'Pending',$5)", ct, Guid.CreateVersion7(), messageType, JsonSerializer.Serialize(payload, Json), tenant.Value, DateTimeOffset.UtcNow, correlation); }
    private readonly record struct Header(Guid Id, Guid EntityId, Guid? BranchId, Guid SupplierPartyId, string Currency, DateOnly OrderDate, string State, string Approval, string Receipt, string Invoicing, long Version, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? ProjectId, Guid? TaskId, string? CostCategory);
    public sealed record PurchaseWorkflowView(string PayloadHash, string State, string ReceiptPayload, string InvoicePayload, Guid? InvoiceId, string? LastError);
    public sealed record ResolvedLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal Quantity, decimal UnitPrice, string? Evidence, string? OverrideReason, string? TaxEvidence = null);
}
