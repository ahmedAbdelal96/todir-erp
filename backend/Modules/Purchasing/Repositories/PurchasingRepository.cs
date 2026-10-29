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
            insert into business_purchasing.purchase_order(tenant_id,legal_entity_id,id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,matching_state,price_list_id,net_total,gross_total,created_by,created_at,updated_at)
            values($1,$2,$3,$4,$5,$6,$7,$8,$9,'NotReceived','NotInvoiced','NotRequired',$10,$11,$11,$12,$13,$13)
            """, ct, tenant.Value, request.LegalEntityId, id, request.BranchId, request.SupplierPartyId, request.CurrencyCode, request.OrderDate, state, approval, request.PriceListId, total, actor, now);
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
            update business_purchasing.purchase_order set supplier_party_id=$5,currency_code=$6,order_date=$7,state=$8,approval_state=$9,approved_by=case when $9='Approved' then approved_by else null end,approved_at=case when $9='Approved' then approved_at else null end,net_total=$10,gross_total=$10,row_version=row_version+1,updated_at=$11
            where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$4
            """, ct, tenant.Value, current.Value.EntityId, id, request.Version, request.SupplierPartyId, request.CurrencyCode, request.OrderDate, nextState, nextApproval, total, now);
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

    public async Task<PurchaseOrderView?> GetAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var header = new Header(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), reader.GetDecimal(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13));
        await reader.CloseAsync();
        var lines = await ReadLinesAsync(connection, tenant, legalEntityId, id, ct);
        return new(header.Id, header.EntityId, header.BranchId, header.SupplierPartyId, header.Currency, header.OrderDate, header.State, header.Approval, header.Receipt, header.Invoicing, header.Version, header.Total, lines, header.CreatedAt, header.UpdatedAt);
    }

    public async Task<PurchaseOrderPage> ListAsync(TenantId tenant, Guid legalEntityId, string? state, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await SetTenantAsync(connection, null, tenant, ct);
        var offset = (page - 1) * pageSize;
        await using var command = new NpgsqlCommand("select id,supplier_party_id,order_date,state,currency_code,net_total,row_version,updated_at from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and ($3 is null or state=$3) order by order_date desc,id desc offset $4 limit $5", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)state ?? DBNull.Value); command.Parameters.AddWithValue(offset); command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<PurchaseOrderListItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct) && items.Count <= pageSize) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7)));
        var more = items.Count > pageSize; if (more) items.RemoveAt(items.Count - 1);
        return new(items, page, pageSize, more);
    }

    public Task<bool> IsApprovedAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct) => StateAsync(tenant, legalEntityId, id, "Approved", ct);

    public async Task<bool> ApprovalRequiredAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select approval_required from business_purchasing.purchase_settings where tenant_id=$1 and legal_entity_id=$2", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        var value = await command.ExecuteScalarAsync(ct); return value is bool required && required;
    }

    public async Task<long> ApproveAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => await TransitionAsync(tenant, actor, id, request, "Approved", "Approved", "Approved", correlation, true, ct);

    public async Task<long> RejectAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => await TransitionAsync(tenant, actor, id, request, "Draft", "Rejected", "Rejected", correlation, false, ct);

    public Task<long> CancelAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => TransitionAsync(tenant, actor, id, request, "Cancelled", "Cancelled", "Cancelled", correlation, false, ct);

    public Task<long> CloseAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, Guid correlation, CancellationToken ct)
        => TransitionAsync(tenant, actor, id, request, "Closed", "Approved", "Closed", correlation, false, ct);

    public async Task<Guid?> GetLinkedInvoiceAsync(TenantId tenant, Guid legalEntityId, Guid id, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await SetTenantAsync(connection, null, tenant, ct);
        await using var command = new NpgsqlCommand("select invoice_id from business_purchasing.purchase_order_invoice_link where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 and idempotency_key=$4", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(idempotencyKey);
        var value = await command.ExecuteScalarAsync(ct); return value is Guid guid ? guid : null;
    }

    public async Task LinkInvoiceAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, Guid invoiceId, string purpose, string idempotencyKey, string payloadHash, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_invoice_link(tenant_id,legal_entity_id,purchase_order_id,invoice_id,purpose,idempotency_key,payload_hash,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict (tenant_id,legal_entity_id,purchase_order_id,purpose,idempotency_key) do nothing", ct, tenant.Value, legalEntityId, id, invoiceId, purpose, idempotencyKey, payloadHash, actor, clock.GetUtcNow());
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set invoicing_state=case when invoicing_state='NotInvoiced' then 'PartiallyInvoiced' else invoicing_state end,updated_at=$4,row_version=row_version+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", ct, tenant.Value, legalEntityId, id, clock.GetUtcNow());
        await transaction.CommitAsync(ct);
    }

    public async Task<Shared.Contracts.Purchasing.PurchaseOrderInvoiceSource?> GetInvoiceSourceAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        var order = await GetAsync(tenant, legalEntityId, id, ct); if (order is null) return null;
        return new(id, "SupplierInvoice", "", legalEntityId, order.BranchId, order.SupplierPartyId, order.OrderDate, order.OrderDate, order.OrderDate, order.CurrencyCode, order.Lines.Select(x => new Shared.Contracts.Purchasing.PurchaseOrderSourceLine(x.ProductVariantId, x.UnitOfMeasureId, x.UnitCode, x.Description, x.OrderedQuantity.ToString(CultureInfo.InvariantCulture), x.UnitPrice.ToString(CultureInfo.InvariantCulture), x.Id.ToString("D"))).ToArray(), null);
    }

    private async Task<long> TransitionAsync(TenantId tenant, Guid actor, Guid id, PurchaseOrderCommandRequest request, string nextState, string approval, string history, Guid correlation, bool approve, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var transaction = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, transaction, tenant, ct);
        var current = await ReadHeaderByIdAsync(connection, transaction, tenant, id, ct) ?? throw new PurchasingException("purchasing.po_not_found");
        if (current.Version != request.Version) throw new PurchasingException("purchasing.po_stale");
        if (approve && current.Approval is not ("Pending" or "NotRequired")) throw new PurchasingException("purchasing.po_not_approvable");
        if (nextState == "Closed" && current.State is not ("Approved" or "Open" or "PartiallyCompleted")) throw new PurchasingException("purchasing.po_not_closable");
        if (nextState == "Cancelled" && current.State is "Cancelled" or "Closed") throw new PurchasingException("purchasing.po_already_closed");
        var persistedApproval = nextState == "Cancelled" ? current.Approval : approval;
        await ExecuteAsync(connection, transaction, "update business_purchasing.purchase_order set state=$4,approval_state=$5,approved_by=case when $5='Approved' then $6 else null end,approved_at=case when $5='Approved' then $7 else null end,cancelled_at=case when $4='Cancelled' then $7 else cancelled_at end,closed_reason=case when $4='Closed' then $8 else closed_reason end,row_version=row_version+1,updated_at=$7 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and row_version=$9", ct, tenant.Value, current.EntityId, id, nextState, persistedApproval, actor, clock.GetUtcNow(), request.Reason, request.Version);
        if (approve || nextState == "Draft") await ExecuteAsync(connection, transaction, "insert into business_purchasing.purchase_order_approval(tenant_id,legal_entity_id,purchase_order_id,id,decision,actor_id,reason,idempotency_key,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict do nothing", ct, tenant.Value, current.EntityId, id, Uuid7.NewGuid(), approve ? "Approved" : "Rejected", actor, request.Reason, request.IdempotencyKey, clock.GetUtcNow());
        await AddHistoryAsync(connection, transaction, tenant, current.EntityId, id, current.State, nextState, history + (request.Reason is null ? "" : ":" + request.Reason), actor, correlation, clock.GetUtcNow(), ct);
        await transaction.CommitAsync(ct); return request.Version + 1;
    }

    private static async Task<Header?> ReadHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid id, CancellationToken ct) => await ReadHeaderCoreAsync(connection, transaction, "select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", tenant, entity, id, ct);
    private static async Task<Header?> ReadHeaderByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid id, CancellationToken ct) => await ReadHeaderCoreAsync(connection, transaction, "select id,legal_entity_id,branch_id,supplier_party_id,currency_code,order_date,state,approval_state,receipt_state,invoicing_state,row_version,net_total,created_at,updated_at from business_purchasing.purchase_order where tenant_id=$1 and id=$2 for update", tenant, null, id, ct);
    private static async Task<Header?> ReadHeaderCoreAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, TenantId tenant, Guid? entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenant.Value); if (entity is { } e) command.Parameters.AddWithValue(e); command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), reader.GetDecimal(11), reader.GetFieldValue<DateTimeOffset>(12), reader.GetFieldValue<DateTimeOffset>(13));
    }

    private async Task<bool> StateAsync(TenantId tenant, Guid entity, Guid id, string expected, CancellationToken ct)
    { var order = await GetAsync(tenant, entity, id, ct); return order?.State == expected; }

    private static async Task<List<PurchaseOrderLineView>> ReadLinesAsync(NpgsqlConnection connection, TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select id,sequence,product_variant_id,unit_of_measure_id,unit_code,description,ordered_quantity,unit_price,invoiced_quantity,cancelled_quantity,price_evidence::text from business_purchasing.purchase_order_line where tenant_id=$1 and legal_entity_id=$2 and purchase_order_id=$3 order by sequence", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PurchaseOrderLineView>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0),reader.GetInt32(1),reader.IsDBNull(2)?null:reader.GetGuid(2),reader.IsDBNull(3)?null:reader.GetGuid(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.GetString(5),reader.GetDecimal(6),reader.GetDecimal(7),reader.GetDecimal(8),reader.GetDecimal(9),reader.IsDBNull(10)?null:reader.GetString(10))); return result; }
    private static async Task InsertLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid orderId, IReadOnlyList<ResolvedLine> lines, DateTimeOffset now, CancellationToken ct)
    { var sequence=1; foreach(var line in lines){await ExecuteAsync(connection,transaction,"insert into business_purchasing.purchase_order_line(tenant_id,legal_entity_id,purchase_order_id,id,sequence,product_variant_id,unit_of_measure_id,unit_code,description,ordered_quantity,unit_price,price_evidence,price_override_reason,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12::jsonb,$13,$14)",ct,tenant.Value,entity,orderId,Uuid7.NewGuid(),sequence++,line.ProductVariantId,line.UnitOfMeasureId,line.UnitCode,line.Description,line.Quantity,line.UnitPrice,line.Evidence,line.OverrideReason,now);}}
    private static async Task AddHistoryAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,TenantId tenant,Guid entity,Guid id,string? from,string to,string reason,Guid actor,Guid correlation,DateTimeOffset now,CancellationToken ct)=>await ExecuteAsync(connection,transaction,"insert into business_purchasing.purchase_order_state_history(tenant_id,legal_entity_id,purchase_order_id,id,from_state,to_state,reason,actor_id,correlation_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)",ct,tenant.Value,entity,id,Uuid7.NewGuid(),from,to,reason,actor,correlation,now);
    private static async Task SetTenantAsync(NpgsqlConnection connection,NpgsqlTransaction? transaction,TenantId tenant,CancellationToken ct){await using var command=new NpgsqlCommand("select set_config('app.tenant_id',$1,true)",connection,transaction);command.Parameters.AddWithValue(tenant.Value.ToString("D"));await command.ExecuteNonQueryAsync(ct);}
    private static async Task<int> ExecuteAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string sql,CancellationToken ct,params object?[] values){await using var command=new NpgsqlCommand(sql,connection,transaction);foreach(var value in values)command.Parameters.AddWithValue(value??DBNull.Value);return await command.ExecuteNonQueryAsync(ct);}
    private readonly record struct Header(Guid Id,Guid EntityId,Guid? BranchId,Guid SupplierPartyId,string Currency,DateOnly OrderDate,string State,string Approval,string Receipt,string Invoicing,long Version,decimal Total,DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt);
    public sealed record ResolvedLine(Guid? ProductVariantId, Guid? UnitOfMeasureId, string? UnitCode, string Description, decimal Quantity, decimal UnitPrice, string? Evidence, string? OverrideReason);
}
