using Core.Transactions;
using Modules.Sales.DTOs;
using Modules.Sales.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Sales.Repositories;

public sealed record SalesReturnLineWork(Guid Id, Guid? SourceLineId, Guid? ProductVariantId, decimal Quantity,
    string Description, Guid? UnitOfMeasureId, Guid? WarehouseId, Guid? LocationId, string StockStatus,
    Guid? LotId, Guid? SerialId, Guid? InventoryOperationId);

public sealed record SalesReturnExecutionWork(Guid ReturnId, Guid LegalEntityId, Guid PartyId, string SourceDocumentType,
    Guid? SourceDocumentId, string Reason, long Version, string State, Guid? InventoryReceiptId, Guid? CreditNoteId,
    IReadOnlyList<SalesReturnLineWork> Lines);

public sealed partial class SalesRepository
{
    public async Task ValidateReturnableQuantitiesAsync(TenantId tenant, CreateReturnRequest request, CancellationToken ct)
    {
        if (request.SourceDocumentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
        {
            await using var connection = await OpenAsync(tenant, ct);
            foreach (var line in request.Lines)
            {
                if (line.SourceLineId is null) throw new SalesException("sales.return_source_line_required");
                await using var command = new NpgsqlCommand("select l.fulfilled_quantity-coalesce((select sum(rl.quantity) from business_sales.sales_return_request_line rl join business_sales.sales_return_request rr on rr.tenant_id=rl.tenant_id and rr.legal_entity_id=rl.legal_entity_id and rr.id=rl.return_id where rl.tenant_id=$1 and rl.legal_entity_id=$2 and rl.source_line_id=$3 and rr.state not in ('Rejected','Cancelled')),0) from business_sales.sales_order_line l where l.tenant_id=$1 and l.legal_entity_id=$2 and l.order_id=$4 and l.id=$3", connection);
                Add(command, tenant.Value, request.LegalEntityId, line.SourceLineId.Value, request.SourceDocumentId ?? Guid.Empty);
                var remaining = await command.ExecuteScalarAsync(ct);
                if (remaining is not decimal available || available < line.Quantity) throw new SalesException("sales.return_quantity_exceeded");
            }
        }
    }

    public async Task<SalesReturnExecutionWork> PrepareReturnExecutionAsync(TenantId tenant, Guid actor, Guid legalEntityId,
        Guid returnId, ReturnCommandRequest request, CancellationToken ct) => await transactions.ExecuteAsync<SalesReturnExecutionWork>(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200) throw new SalesException("sales.idempotency_key_invalid");
        var executionKey = request.IdempotencyKey.Trim();
        await using var header = new NpgsqlCommand("select party_id,state,source_document_type,source_document_id,reason,version,execution_idempotency_key,inventory_receipt_id,credit_note_id from business_sales.sales_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        Add(header, tenant.Value, legalEntityId, returnId);
        await using var reader = await header.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new SalesException("sales.return_not_found");
        var partyId = reader.GetGuid(0); var state = reader.GetString(1); var sourceType = reader.GetString(2);
        Guid? sourceId = reader.IsDBNull(3) ? null : reader.GetGuid(3); var reason = reader.GetString(4); var version = reader.GetInt64(5);
        var existingKey = reader.IsDBNull(6) ? null : reader.GetString(6); Guid? inventoryReceipt = reader.IsDBNull(7) ? null : reader.GetGuid(7); Guid? creditNote = reader.IsDBNull(8) ? null : reader.GetGuid(8);
        await reader.CloseAsync();
        if (state == "Executed") return new(returnId, legalEntityId, partyId, sourceType, sourceId, reason, version, state, inventoryReceipt, creditNote, await ReadReturnLinesAsync(context.Connection, context.Transaction, tenant.Value, legalEntityId, returnId, token));
        if (state == "Processing")
        {
            if (!string.Equals(existingKey, executionKey, StringComparison.Ordinal)) throw new SalesException("sales.return_execution_conflict");
            return new(returnId, legalEntityId, partyId, sourceType, sourceId, reason, version, state, inventoryReceipt, creditNote, await ReadReturnLinesAsync(context.Connection, context.Transaction, tenant.Value, legalEntityId, returnId, token));
        }
        var retryingFailedExecution = state == "Failed" && string.Equals(existingKey, executionKey, StringComparison.Ordinal);
        if (!retryingFailedExecution)
        {
            if (state != "Approved") throw new SalesException("sales.return_not_approved");
            if (version != request.Version) throw new SalesException("sales.stale_return");
        }
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(context, "update business_sales.sales_return_request set state='Processing',execution_idempotency_key=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, returnId, executionKey, now);
        var lines = await ReadReturnLinesAsync(context.Connection, context.Transaction, tenant.Value, legalEntityId, returnId, token);
        await AddHistoryAsync(context, tenant.Value, legalEntityId, "SalesReturn", returnId, null, state, "Processing", actor, request.Reason, now, token);
        AddOutbox(context, tenant, "SalesReturnProcessingV1", new { returnId }, now, returnId);
        return new(returnId, legalEntityId, partyId, sourceType, sourceId, reason, version + 1, "Processing", inventoryReceipt, creditNote, lines);
    }, ct);

    public Task CompleteReturnAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid returnId, Guid? inventoryReceiptId,
        Guid? creditNoteId, CancellationToken ct) => ExecuteSimpleAsync(tenant, async (context, token) =>
    {
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(context, "update business_sales.sales_return_request set state='Executed',inventory_receipt_id=$4,credit_note_id=$5,failure_code=null,completed_at=$6,version=version+1,updated_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Processing'", token, tenant.Value, legalEntityId, returnId, inventoryReceiptId ?? (object)DBNull.Value, creditNoteId ?? (object)DBNull.Value, now);
        await AddHistoryAsync(context, tenant.Value, legalEntityId, "SalesReturn", returnId, null, "Processing", "Executed", actor, null, now, token);
        AddOutbox(context, tenant, "SalesReturnCompletedV1", new { returnId, inventoryReceiptId, creditNoteId }, now, returnId);
    }, ct);

    public Task FailReturnAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid returnId, string code, CancellationToken ct) => ExecuteSimpleAsync(tenant, async (context, token) =>
    {
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(context, "update business_sales.sales_return_request set state='Failed',failure_code=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Processing'", token, tenant.Value, legalEntityId, returnId, code, now);
        await AddHistoryAsync(context, tenant.Value, legalEntityId, "SalesReturn", returnId, null, "Processing", "Failed", actor, code, now, token);
        AddOutbox(context, tenant, "SalesReturnFailedV1", new { returnId, code }, now, returnId);
    }, ct);

    public async Task<SalesReturnExecutionResult> GetReturnExecutionResultAsync(TenantId tenant, Guid legalEntityId, Guid returnId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select state,inventory_receipt_id,credit_note_id,version from business_sales.sales_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, legalEntityId, returnId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new SalesException("sales.return_not_found");
        return new(returnId, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetInt64(3));
    }

    public Task RecordReturnInventoryOperationAsync(TenantId tenant, Guid legalEntityId, Guid returnId, Guid lineId, Guid operationId, CancellationToken ct)
        => ExecuteSimpleAsync(tenant, async (context, token) => await ExecuteAsync(context,
            "update business_sales.sales_return_request_line set inventory_operation_id=coalesce(inventory_operation_id,$4) where tenant_id=$1 and legal_entity_id=$2 and return_id=$3 and id=$5",
            token, tenant.Value, legalEntityId, returnId, operationId, lineId), ct);

    private static async Task<IReadOnlyList<SalesReturnLineWork>> ReadReturnLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid legalEntityId, Guid returnId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,source_line_id,product_variant_id,quantity,description,unit_of_measure_id,warehouse_id,location_id,stock_status,lot_id,serial_id,inventory_operation_id from business_sales.sales_return_request_line where tenant_id=$1 and legal_entity_id=$2 and return_id=$3 order by id", connection, transaction);
        Add(command, tenant, legalEntityId, returnId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<SalesReturnLineWork>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.IsDBNull(11) ? null : reader.GetGuid(11)));
        return result;
    }
}
