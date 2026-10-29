using System.Globalization;
using System.Text.Json;
using Core.Transactions;
using Modules.Sales.DTOs;
using Modules.Sales.Entities;
using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;

namespace Modules.Sales.Repositories;

public sealed partial class SalesRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, TimeProvider clock)
{
    public async Task<Guid> CreateQuotationAsync(TenantId tenant, Guid actor, CreateQuotationRequest request, IReadOnlyList<QuotationLineRequest> lines, string taxEvidence, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var revision = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var number = await NextNumberAsync(context, tenant.Value, request.LegalEntityId, "quote", request.QuoteDate.Year, token);
            var totals = Totals(lines);
            await ExecuteAsync(context, "insert into business_sales.quotation(tenant_id,id,legal_entity_id,branch_id,number,party_id,quote_date,valid_until,currency_code,state,current_revision_number,version,customer_reference,requested_delivery_date,salesperson_id,sales_team_id,sales_channel_id,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',1,1,$10,$11,$12,$13,$14,$15,$16,$16)", token,
                tenant.Value, id, request.LegalEntityId, request.BranchId ?? (object)DBNull.Value, number, request.PartyId, request.QuoteDate, request.ValidUntil ?? (object)DBNull.Value, request.CurrencyCode,
                request.CustomerReference ?? (object)DBNull.Value, request.RequestedDeliveryDate ?? (object)DBNull.Value, request.SalespersonId ?? (object)DBNull.Value, request.SalesTeamId ?? (object)DBNull.Value, request.SalesChannelId ?? (object)DBNull.Value, actor, now);
            await InsertRevisionAsync(context, tenant.Value, revision, request.LegalEntityId, id, 1, "Draft", lines, totals, actor, now, taxEvidence, token);
            await AddHistoryAsync(context, tenant.Value, request.LegalEntityId, "Quotation", id, null, null, "Draft", actor, "created", now, token);
            AddOutbox(context, tenant, "QuotationDraftCreatedV1", new { quotationId = id, revisionNumber = 1 }, now, id);
            return id;
        }, ct);
    }

    public async Task<QuotationDetail?> GetQuotationAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var header = new NpgsqlCommand("select branch_id,number,party_id,quote_date,valid_until,state,current_revision_number,version,currency_code,customer_reference,requested_delivery_date from business_sales.quotation where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(header, tenant.Value, legalEntityId, id);
        await using var reader = await header.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var branch = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0); var number = reader.GetString(1); var party = reader.GetGuid(2); var quoteDate = reader.GetFieldValue<DateOnly>(3);
        var valid = reader.IsDBNull(4) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(4); var state = reader.GetString(5); var current = reader.GetInt32(6); var version = reader.GetInt64(7); var currency = reader.GetString(8);
        var customerReference = reader.IsDBNull(9) ? null : reader.GetString(9); var requested = reader.IsDBNull(10) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(10);
        await reader.CloseAsync();
        var revision = await ReadRevisionAsync(connection, tenant.Value, legalEntityId, id, current, ct);
        var lines = await ReadLinesAsync(connection, tenant.Value, legalEntityId, revision.id, ct);
        var accepts = await ReadAcceptancesAsync(connection, tenant.Value, legalEntityId, id, ct);
        var history = await ReadHistoryAsync(connection, tenant.Value, legalEntityId, id, ct);
        return new(id, legalEntityId, branch, number, party, quoteDate, valid, state, current, version, currency, customerReference, requested,
            revision.net, revision.discount, revision.gross, lines, accepts, history);
    }

    public async Task<SalesPage<QuotationListItem>> ListQuotationsAsync(TenantId tenant, Guid legalEntityId, string? state, Guid? partyId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 200);
        await using var connection = await OpenAsync(tenant, ct);
        var filter = string.IsNullOrWhiteSpace(state) ? "" : " and q.state=$3";
        var party = partyId is null ? "" : string.IsNullOrWhiteSpace(state) ? " and q.party_id=$3" : " and q.party_id=$4";
        var limitParameter = string.IsNullOrWhiteSpace(state) ? (partyId is null ? 3 : 4) : (partyId is null ? 4 : 5);
        var offsetParameter = limitParameter + 1;
        var sql = $"select q.id,q.branch_id,q.number,q.party_id,q.quote_date,q.valid_until,q.state,q.current_revision_number,q.currency_code,q.version,r.gross_total,count(*) over() from business_sales.quotation q join business_sales.quotation_revision r on r.tenant_id=q.tenant_id and r.quotation_id=q.id and r.revision_number=q.current_revision_number where q.tenant_id=$1 and q.legal_entity_id=$2{filter}{party} order by q.updated_at desc,q.id limit ${limitParameter} offset ${offsetParameter}";
        await using var command = new NpgsqlCommand(sql, connection); Add(command, tenant.Value, legalEntityId); var index = 3;
        if (!string.IsNullOrWhiteSpace(state)) { command.Parameters.AddWithValue(index.ToString(CultureInfo.InvariantCulture), state!); index++; }
        if (partyId is { } p) { command.Parameters.AddWithValue(index.ToString(CultureInfo.InvariantCulture), p); index++; }
        command.Parameters.AddWithValue(index.ToString(CultureInfo.InvariantCulture), pageSize); index++; command.Parameters.AddWithValue(index.ToString(CultureInfo.InvariantCulture), (page - 1) * pageSize);
        var items = new List<QuotationListItem>(); var total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) { total = reader.GetInt32(11); items.Add(new(reader.GetGuid(0), legalEntityId, reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetInt32(7), reader.GetDecimal(10), reader.GetString(8), reader.GetInt64(9))); }
        return new(items, total, page, pageSize);
    }

    public async Task<long> UpdateQuotationAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, UpdateQuotationRequest request, IReadOnlyList<QuotationLineRequest> lines, string taxEvidence, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var current = await ReadHeaderForUpdateAsync(context, tenant.Value, legalEntityId, id, token) ?? throw new SalesException("sales.quotation_not_found");
            if (current.version != request.Version) throw new SalesException("sales.stale_quotation");
            if (current.state is "Accepted" or "Converted" or "Cancelled") throw new SalesException("sales.quotation_revision_required");
            var totals = Totals(lines);
            var revision = current.revision;
            if (current.state == "Draft")
            {
                await ExecuteAsync(context, "update business_sales.quotation set party_id=$4,quote_date=$5,valid_until=$6,currency_code=$7,customer_reference=$8,requested_delivery_date=$9,salesperson_id=$10,sales_team_id=$11,sales_channel_id=$12,version=version+1,updated_at=$13 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token,
                    tenant.Value, legalEntityId, id, request.PartyId, request.QuoteDate, request.ValidUntil ?? (object)DBNull.Value, request.CurrencyCode, request.CustomerReference ?? (object)DBNull.Value, request.RequestedDeliveryDate ?? (object)DBNull.Value, request.SalespersonId ?? (object)DBNull.Value, request.SalesTeamId ?? (object)DBNull.Value, request.SalesChannelId ?? (object)DBNull.Value, now);
                await ExecuteAsync(context, "update business_sales.quotation_revision set net_total=$4,discount_total=$5,gross_total=$6,snapshot=$7,pricing_evidence=$8,tax_preview_evidence=$9,created_by=$10 where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$11", token,
                    tenant.Value, legalEntityId, id, totals.net, totals.discount, totals.gross, Jsonb(JsonSerializer.Serialize(new { lines, request.CurrencyCode })), Jsonb("{}"), Jsonb(taxEvidence), actor, revision);
                await ReplaceLinesAsync(context, tenant.Value, legalEntityId, (await ReadRevisionAsync(context.Connection, tenant.Value, legalEntityId, id, revision, token)).id, lines, token);
            }
            else
            {
                await ExecuteAsync(context, "update business_sales.quotation_revision set status='Superseded',sealed_at=coalesce(sealed_at,$4) where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$5", token, tenant.Value, legalEntityId, id, now, revision);
                revision++;
                await ExecuteAsync(context, "update business_sales.quotation set party_id=$4,quote_date=$5,valid_until=$6,currency_code=$7,customer_reference=$8,requested_delivery_date=$9,salesperson_id=$10,sales_team_id=$11,sales_channel_id=$12,current_revision_number=$13,version=version+1,updated_at=$14 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token,
                    tenant.Value, legalEntityId, id, request.PartyId, request.QuoteDate, request.ValidUntil ?? (object)DBNull.Value, request.CurrencyCode, request.CustomerReference ?? (object)DBNull.Value, request.RequestedDeliveryDate ?? (object)DBNull.Value, request.SalespersonId ?? (object)DBNull.Value, request.SalesTeamId ?? (object)DBNull.Value, request.SalesChannelId ?? (object)DBNull.Value, revision, now);
                await InsertRevisionAsync(context, tenant.Value, Guid.CreateVersion7(), legalEntityId, id, revision, "Open", lines, totals, actor, now, taxEvidence, token);
            }
            AddOutbox(context, tenant, "QuotationChangedV1", new { quotationId = id, revisionNumber = revision }, now, id);
            return current.version + 1;
        }, ct);
    }

    public async Task<int> OpenQuotationAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, TransitionQuotationRequest request, CancellationToken ct) => await TransitionAsync(tenant, actor, legalEntityId, id, request.Version, "Open", request.Reason, "QuotationOpenedV1", ct);
    public async Task<int> DeclineQuotationAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, TransitionQuotationRequest request, CancellationToken ct) => await TransitionAsync(tenant, actor, legalEntityId, id, request.Version, "Declined", request.Reason, "QuotationDeclinedV1", ct);
    public async Task<int> CancelQuotationAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, TransitionQuotationRequest request, CancellationToken ct) => await TransitionAsync(tenant, actor, legalEntityId, id, request.Version, "Cancelled", request.Reason, "QuotationCancelledV1", ct);

    public async Task<QuotationDetail> AcceptQuotationAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, AcceptQuotationRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var existing = await ReadAcceptanceByKeyAsync(context, tenant.Value, legalEntityId, id, request.IdempotencyKey, token);
            if (existing is not null) { if (existing.Value.revision != request.RevisionNumber) throw new SalesException("sales.idempotency_conflict"); return 0; }
            var current = await ReadHeaderForUpdateAsync(context, tenant.Value, legalEntityId, id, token) ?? throw new SalesException("sales.quotation_not_found");
            if (current.version < 1 || current.state is "Cancelled" or "Declined" or "Expired" or "Converted") throw new SalesException("sales.quotation_not_acceptible");
            if (request.RevisionNumber != current.revision) throw new SalesException("sales.stale_quotation_revision");
            if (await ReadAcceptanceByRevisionAsync(context, tenant.Value, legalEntityId, id, request.RevisionNumber, token) is not null) throw new SalesException("sales.acceptance_conflict");
            if (string.IsNullOrWhiteSpace(request.AcceptanceMethod) || request.AcceptanceMethod.Length > 32) throw new SalesException("sales.acceptance_method_invalid");
            await ExecuteAsync(context, "insert into business_sales.quotation_acceptance(tenant_id,id,legal_entity_id,quotation_id,revision_number,accepted_at,acceptance_method,recorded_by,evidence_reference,note,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, id, request.RevisionNumber, now, request.AcceptanceMethod.Trim(), actor, request.EvidenceReference ?? (object)DBNull.Value, request.Note ?? (object)DBNull.Value, request.IdempotencyKey.Trim());
            await ExecuteAsync(context, "update business_sales.quotation set state='Accepted',version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, id, now);
            await ExecuteAsync(context, "update business_sales.quotation_revision set status='Accepted',sealed_at=$4 where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$5", token, tenant.Value, legalEntityId, id, now, request.RevisionNumber);
            await AddHistoryAsync(context, tenant.Value, legalEntityId, "Quotation", id, request.RevisionNumber, current.state, "Accepted", actor, request.Note, now, token);
            AddOutbox(context, tenant, "QuotationAcceptedV1", new { quotationId = id, revisionNumber = request.RevisionNumber }, now, id);
            return 0;
        }, ct);
        return (await GetQuotationAsync(tenant, legalEntityId, id, ct)) ?? throw new SalesException("sales.quotation_not_found");
    }

    public async Task<Guid> CreateRevisionAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, long expectedVersion, IReadOnlyList<QuotationLineRequest> lines, string taxEvidence, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var current = await ReadHeaderForUpdateAsync(context, tenant.Value, legalEntityId, id, token) ?? throw new SalesException("sales.quotation_not_found");
            if (current.version != expectedVersion) throw new SalesException("sales.stale_quotation");
            if (current.state != "Accepted") throw new SalesException("sales.revision_not_required");
            var next = current.revision + 1; var totals = Totals(lines);
            await ExecuteAsync(context, "update business_sales.quotation_revision set status='Superseded',sealed_at=coalesce(sealed_at,$4) where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$5", token, tenant.Value, legalEntityId, id, now, current.revision);
            await InsertRevisionAsync(context, tenant.Value, Guid.CreateVersion7(), legalEntityId, id, next, "Open", lines, totals, actor, now, taxEvidence, token);
            await ExecuteAsync(context, "update business_sales.quotation set state='Open',current_revision_number=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, id, next, now);
            await AddHistoryAsync(context, tenant.Value, legalEntityId, "Quotation", id, next, "Accepted", "Open", actor, "material_revision", now, token);
            AddOutbox(context, tenant, "QuotationRevisionCreatedV1", new { quotationId = id, revisionNumber = next }, now, id);
            return id;
        }, ct);
    }

    public async Task<ConversionResult> ConvertToInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid id, ConvertQuotationRequest request, Shared.Contracts.Invoicing.IInvoicingSourceCapabilities invoicing, CancellationToken ct)
    {
        var quote = await GetQuotationAsync(tenant, legalEntityId, id, ct) ?? throw new SalesException("sales.quotation_not_found");
        if (quote.State is not ("Accepted" or "Converted") || quote.CurrentRevisionNumber != request.AcceptedRevision) throw new SalesException("sales.accepted_revision_required");
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, request.AcceptedRevision, request.ConversionMode, request.DeliverNow, request.WarehouseId, lines = quote.Lines }))).ToLowerInvariant();
        var prior = await ReadConversionAsync(tenant, legalEntityId, id, request.AcceptedRevision, request.IdempotencyKey, payloadHash, ct);
        if (prior is not null) return new(id, request.AcceptedRevision, prior.Value.targetId, "Draft", []);
        var invoice = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership,
            new Shared.Contracts.Invoicing.InvoiceSourceDocumentContract("sales", id, "Quotation", request.IdempotencyKey.Trim(), "Receivable", legalEntityId, quote.BranchId, quote.PartyId, null, quote.QuoteDate, quote.QuoteDate, quote.QuoteDate, quote.RequestedDeliveryDate, quote.CurrencyCode, "Exclusive", null, null,
                null, quote.Lines.Select(line => new Shared.Contracts.Invoicing.InvoiceSourceLineContract(line.ProductVariantId, line.UnitOfMeasureId, line.UomCode, line.Description, line.Quantity.ToString("0.######", CultureInfo.InvariantCulture), line.UnitPrice.ToString("0.######", CultureInfo.InvariantCulture), null, null, line.Id.ToString("D"))).ToArray()), Guid.CreateVersion7(), ct);
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var existing = await ReadConversionAsync(context, tenant.Value, legalEntityId, id, request.AcceptedRevision, token);
            if (existing is not null && existing.Value.targetId != invoice) throw new SalesException("sales.conversion_conflict");
            if (existing is null)
            {
                await ExecuteAsync(context, "insert into business_sales.sales_conversion_link(tenant_id,id,legal_entity_id,source_type,source_id,source_revision,target_module,target_type,target_id,purpose,idempotency_key,payload_hash,created_by,created_at) values($1,$2,$3,'Quotation',$4,$5,'invoicing','Invoice',$6,'DirectInvoice',$7,$8,$9,$10)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, id, request.AcceptedRevision, invoice, request.IdempotencyKey.Trim(), payloadHash, actor, now);
                await ExecuteAsync(context, "update business_sales.quotation set state='Converted',version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state='Accepted'", token, tenant.Value, legalEntityId, id, now);
                await AddHistoryAsync(context, tenant.Value, legalEntityId, "Quotation", id, request.AcceptedRevision, "Accepted", "Converted", actor, "invoice_created", now, token);
                AddOutbox(context, tenant, "QuotationConvertedV1", new { quotationId = id, revisionNumber = request.AcceptedRevision, invoiceId = invoice }, now, id);
            }
            return 0;
        }, ct);
        return new(id, request.AcceptedRevision, invoice, "Draft", []);
    }

    public async Task<Guid> CreateOrderAsync(TenantId tenant, Guid actor, CreateOrderRequest request, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var number = await NextNumberAsync(context, tenant.Value, request.LegalEntityId, "order", request.OrderDate.Year, token);
            await ExecuteAsync(context, "insert into business_sales.sales_order(tenant_id,id,legal_entity_id,branch_id,number,quotation_id,quotation_revision,party_id,order_date,currency_code,warehouse_id,commercial_state,fulfillment_state,invoicing_state,credit_state,customer_reference,requested_delivery_date,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Draft','Pending','NotInvoiced','NotChecked',$12,$13,$14,$15,$15)", token, tenant.Value, id, request.LegalEntityId, request.BranchId ?? (object)DBNull.Value, number, request.QuotationId ?? (object)DBNull.Value, request.QuotationId is null ? DBNull.Value : 1, request.PartyId, request.OrderDate, request.CurrencyCode, request.WarehouseId ?? (object)DBNull.Value, request.CustomerReference ?? (object)DBNull.Value, request.RequestedDeliveryDate ?? (object)DBNull.Value, actor, now);
            await InsertOrderLinesAsync(context, tenant.Value, request.LegalEntityId, id, lines, token);
            AddOutbox(context, tenant, "SalesOrderCreatedV1", new { orderId = id }, now, id);
            return id;
        }, ct);
    }

    public async Task<SalesOrderDetail?> GetOrderAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select branch_id,number,party_id,order_date,currency_code,warehouse_id,commercial_state,fulfillment_state,invoicing_state,credit_state,version from business_sales.sales_order where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id);
        await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
        var result = new SalesOrderDetail(id, legalEntityId, reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetInt64(10), []); await reader.CloseAsync();
        return result with { Lines = await ReadOrderLinesAsync(connection, tenant.Value, legalEntityId, id, ct) };
    }

    public async Task<SalesPage<SalesOrderListItem>> ListOrdersAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); pageSize = Math.Clamp(pageSize, 1, 200); await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select o.id,o.branch_id,o.number,o.party_id,o.commercial_state,o.fulfillment_state,o.invoicing_state,o.version,coalesce(sum(l.ordered_quantity*l.unit_price),0),count(*) over() from business_sales.sales_order o left join business_sales.sales_order_line l on l.tenant_id=o.tenant_id and l.order_id=o.id where o.tenant_id=$1 and o.legal_entity_id=$2 group by o.id,o.branch_id,o.number,o.party_id,o.commercial_state,o.fulfillment_state,o.invoicing_state,o.version,o.updated_at order by o.updated_at desc,o.id limit $3 offset $4", connection); Add(command, tenant.Value, legalEntityId, pageSize, (page - 1) * pageSize); var list = new List<SalesOrderListItem>(); var total = 0; await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) { total = reader.GetInt32(9); list.Add(new(reader.GetGuid(0), legalEntityId, reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7), reader.GetDecimal(8))); } return new(list, total, page, pageSize);
    }

    public async Task<int> ConfirmOrderAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, OrderCommandRequest request, CancellationToken ct) => await OrderStateAsync(tenant, actor, legalEntityId, id, request.Version, "Confirmed", request.Reason, "SalesOrderConfirmedV1", ct);
    public async Task<int> CancelOrderAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, OrderCommandRequest request, CancellationToken ct) => await OrderStateAsync(tenant, actor, legalEntityId, id, request.Version, "Cancelled", request.Reason, "SalesOrderCancelledV1", ct);

    public async Task<Guid> ConvertQuotationToOrderAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid quotationId,
        ConvertQuotationToOrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
            throw new SalesException("sales.idempotency_key_invalid");
        var quote = await GetQuotationAsync(tenant, legalEntityId, quotationId, ct) ?? throw new SalesException("sales.quotation_not_found");
        if (quote.State != "Accepted" || quote.CurrentRevisionNumber != request.AcceptedRevision)
            throw new SalesException("sales.accepted_revision_required");
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { quotationId, request.AcceptedRevision, request.WarehouseId, lines = quote.Lines }))).ToLowerInvariant();
        await using var existingConnection = await OpenAsync(tenant, ct);
        await using var existingCommand = new NpgsqlCommand("select target_id,idempotency_key,payload_hash from business_sales.sales_conversion_link where tenant_id=$1 and legal_entity_id=$2 and source_type='Quotation' and source_id=$3 and source_revision=$4 and purpose='QuotationOrder'", existingConnection);
        Add(existingCommand, tenant.Value, legalEntityId, quotationId, request.AcceptedRevision);
        await using (var reader = await existingCommand.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(1) != request.IdempotencyKey.Trim() || reader.GetString(2) != payloadHash) throw new SalesException("sales.idempotency_conflict");
                return reader.GetGuid(0);
            }
        }
        var orderId = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await ExecuteAsync(context, "insert into business_sales.sales_order(tenant_id,id,legal_entity_id,branch_id,number,quotation_id,quotation_revision,party_id,order_date,currency_code,warehouse_id,commercial_state,fulfillment_state,invoicing_state,credit_state,customer_reference,requested_delivery_date,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Draft','Pending','NotInvoiced','NotChecked',$12,$13,$14,$15,$15)", token, tenant.Value, orderId, legalEntityId, quote.BranchId ?? (object)DBNull.Value, await NextNumberAsync(context, tenant.Value, legalEntityId, "order", quote.QuoteDate.Year, token), quotationId, request.AcceptedRevision, quote.PartyId, quote.QuoteDate, quote.CurrencyCode, request.WarehouseId ?? (object)DBNull.Value, quote.CustomerReference ?? (object)DBNull.Value, quote.RequestedDeliveryDate ?? (object)DBNull.Value, actor, now);
            await InsertOrderLinesAsync(context, tenant.Value, legalEntityId, orderId, quote.Lines.Select(line => new QuotationLineRequest(line.ProductVariantId, line.UnitOfMeasureId, line.Description, line.Quantity, line.UnitPrice, line.DiscountAmount, line.UomCode)).ToArray(), token);
            await ExecuteAsync(context, "insert into business_sales.sales_conversion_link(tenant_id,id,legal_entity_id,source_type,source_id,source_revision,target_module,target_type,target_id,purpose,idempotency_key,payload_hash,created_by,created_at) values($1,$2,$3,'Quotation',$4,$5,'sales','SalesOrder',$6,'QuotationOrder',$7,$8,$9,$10)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, quotationId, request.AcceptedRevision, orderId, request.IdempotencyKey.Trim(), payloadHash, actor, now);
            AddOutbox(context, tenant, "QuotationConvertedToOrderV1", new { quotationId, orderId, revisionNumber = request.AcceptedRevision }, now, quotationId);
            return 0;
        }, ct);
        return orderId;
    }

    public async Task<int> AmendOrderAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid orderId, UpdateOrderRequest request,
        IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct) => await transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        var order = await ReadOrderHeaderForUpdateAsync(context, tenant.Value, legalEntityId, orderId, token) ?? throw new SalesException("sales.order_not_found");
        if (order.version != request.Version) throw new SalesException("sales.stale_order");
        if (order.state != "Draft") throw new SalesException("sales.order_amendment_blocked");
        await ExecuteAsync(context, "delete from business_sales.sales_order_line where tenant_id=$1 and legal_entity_id=$2 and order_id=$3", token, tenant.Value, legalEntityId, orderId);
        await InsertOrderLinesAsync(context, tenant.Value, legalEntityId, orderId, lines, token);
        var now = clock.GetUtcNow();
        await ExecuteAsync(context, "update business_sales.sales_order set customer_reference=$4,requested_delivery_date=$5,version=version+1,updated_at=$6 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, orderId, request.CustomerReference ?? (object)DBNull.Value, request.RequestedDeliveryDate ?? (object)DBNull.Value, now);
        AddOutbox(context, tenant, "SalesOrderAmendedV1", new { orderId }, now, orderId);
        return (int)order.version + 1;
    }, ct);

    public async Task<ConversionResult> ConvertOrderToInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid orderId, string idempotencyKey, Shared.Contracts.Invoicing.IInvoicingSourceCapabilities invoicing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new SalesException("sales.idempotency_key_invalid");
        var order = await GetOrderAsync(tenant, legalEntityId, orderId, ct) ?? throw new SalesException("sales.order_not_found");
        var lines = order.Lines.Where(line => line.InvoiceableQuantity > 0m).ToArray(); if (lines.Length == 0) throw new SalesException("sales.order_nothing_to_invoice");
        var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { orderId, lines }))).ToLowerInvariant();
        var existing = await ReadOrderConversionAsync(tenant, legalEntityId, orderId, idempotencyKey, payloadHash, ct); if (existing is not null) return new(orderId, 0, existing.Value.targetId, "Draft", []);
        var invoice = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership, new Shared.Contracts.Invoicing.InvoiceSourceDocumentContract("sales", orderId, "SalesOrder", idempotencyKey.Trim(), "Receivable", legalEntityId, order.BranchId, order.PartyId, null, order.OrderDate, order.OrderDate, order.OrderDate, null, order.CurrencyCode, "Exclusive", null, null, null,
            lines.Select(line => new Shared.Contracts.Invoicing.InvoiceSourceLineContract(line.ProductVariantId, line.UnitOfMeasureId, null, line.Description, line.InvoiceableQuantity.ToString("0.######", CultureInfo.InvariantCulture), line.UnitPrice.ToString("0.######", CultureInfo.InvariantCulture), null, null, line.Id.ToString("D"))).ToArray()), Guid.CreateVersion7(), ct);
        var now = clock.GetUtcNow();
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (await ReadOrderConversionAsync(context, tenant.Value, legalEntityId, orderId, token) is not null) return 0;
            await ExecuteAsync(context, "insert into business_sales.sales_conversion_link(tenant_id,id,legal_entity_id,source_type,source_id,source_revision,target_module,target_type,target_id,purpose,idempotency_key,payload_hash,created_by,created_at) values($1,$2,$3,'SalesOrder',$4,null,'invoicing','Invoice',$5,'OrderInvoice',$6,$7,$8,$9)", token, tenant.Value, Guid.CreateVersion7(), legalEntityId, orderId, invoice, idempotencyKey.Trim(), payloadHash, actor, now);
            foreach (var line in lines)
                await ExecuteAsync(context, "update business_sales.sales_order_line set invoiced_quantity=invoiced_quantity+$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and invoiced_quantity+$4<=ordered_quantity-cancelled_quantity", token, tenant.Value, legalEntityId, line.Id, line.InvoiceableQuantity);
            await ExecuteAsync(context, "update business_sales.sales_order set invoicing_state=case when not exists(select 1 from business_sales.sales_order_line where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 and invoiced_quantity<ordered_quantity-cancelled_quantity) then 'Invoiced' else 'PartiallyInvoiced' end,version=version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, orderId, now);
            AddOutbox(context, tenant, "SalesOrderInvoiceCreatedV1", new { orderId, invoiceId = invoice }, now, orderId); return 0;
        }, ct);
        return new(orderId, 0, invoice, "Draft", []);
    }

    public async Task<Guid> CreateReturnAsync(TenantId tenant, Guid actor, CreateReturnRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0) throw new SalesException("sales.return_lines_required"); var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (request.SourceDocumentType.Equals("SalesOrder", StringComparison.OrdinalIgnoreCase))
                foreach (var line in request.Lines)
                {
                    if (line.SourceLineId is not { } sourceLineId) throw new SalesException("sales.return_source_line_required");
                    var available = await ExecuteScalarAsync(context, "select l.fulfilled_quantity-coalesce((select sum(rl.quantity) from business_sales.sales_return_request_line rl join business_sales.sales_return_request rr on rr.tenant_id=rl.tenant_id and rr.legal_entity_id=rl.legal_entity_id and rr.id=rl.return_id where rl.tenant_id=$1 and rl.legal_entity_id=$2 and rl.source_line_id=$3 and rr.state not in ('Rejected','Cancelled')),0) from business_sales.sales_order_line l where l.tenant_id=$1 and l.legal_entity_id=$2 and l.id=$3 and l.order_id=$4 for update", token, tenant.Value, request.LegalEntityId, sourceLineId, request.SourceDocumentId!.Value);
                    if (available is not decimal remaining || remaining < line.Quantity) throw new SalesException("sales.return_quantity_exceeded");
                }
            await ExecuteAsync(context, "insert into business_sales.sales_return_request(tenant_id,id,legal_entity_id,branch_id,party_id,source_document_type,source_document_id,state,reason,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,'Requested',$8,$9,$10,$10)", token, tenant.Value, id, request.LegalEntityId, request.BranchId ?? (object)DBNull.Value, request.PartyId, request.SourceDocumentType, request.SourceDocumentId ?? (object)DBNull.Value, request.Reason.Trim(), actor, now);
            foreach (var line in request.Lines)
                await ExecuteAsync(context, "insert into business_sales.sales_return_request_line(tenant_id,id,legal_entity_id,return_id,source_line_id,product_variant_id,description,quantity,unit_of_measure_id,warehouse_id,location_id,stock_status,lot_id,serial_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, id, line.SourceLineId ?? (object)DBNull.Value, line.ProductVariantId ?? (object)DBNull.Value, line.Description.Trim(), line.Quantity, line.UnitOfMeasureId ?? (object)DBNull.Value, line.WarehouseId ?? (object)DBNull.Value, line.LocationId ?? (object)DBNull.Value, line.StockStatus.Trim(), line.LotId ?? (object)DBNull.Value, line.SerialId ?? (object)DBNull.Value);
            AddOutbox(context, tenant, "SalesReturnRequestedV1", new { returnId = id }, now, id); return id;
        }, ct);
    }

    public async Task<SalesReturnView?> GetReturnAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select party_id,state,source_document_type,source_document_id,reason,version from business_sales.sales_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; var partyId = reader.GetGuid(0); var state = reader.GetString(1); var sourceType = reader.GetString(2); Guid? sourceId = reader.IsDBNull(3) ? null : reader.GetGuid(3); var reason = reader.GetString(4); var version = reader.GetInt64(5); await reader.CloseAsync();
        await using var linesCommand = new NpgsqlCommand("select source_line_id,product_variant_id,quantity,description,unit_of_measure_id,warehouse_id,location_id,stock_status,lot_id,serial_id from business_sales.sales_return_request_line where tenant_id=$1 and legal_entity_id=$2 and return_id=$3 order by id", connection); Add(linesCommand, tenant.Value, legalEntityId, id); await using var linesReader = await linesCommand.ExecuteReaderAsync(ct); var lines = new List<ReturnLineRequest>(); while (await linesReader.ReadAsync(ct)) lines.Add(new(linesReader.IsDBNull(0) ? null : linesReader.GetGuid(0), linesReader.IsDBNull(1) ? null : linesReader.GetGuid(1), linesReader.GetDecimal(2), linesReader.GetString(3), linesReader.IsDBNull(4) ? null : linesReader.GetGuid(4), linesReader.IsDBNull(5) ? null : linesReader.GetGuid(5), linesReader.IsDBNull(6) ? null : linesReader.GetGuid(6), linesReader.GetString(7), linesReader.IsDBNull(8) ? null : linesReader.GetGuid(8), linesReader.IsDBNull(9) ? null : linesReader.GetGuid(9)));
        return new SalesReturnView(id, legalEntityId, partyId, state, sourceType, sourceId, reason, version, lines);
    }

    public async Task<long> TransitionReturnAsync(TenantId tenant, Guid actor, Guid legalEntityId, Guid id, string target, ReturnCommandRequest request, CancellationToken ct)
    {
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select version,state from business_sales.sales_return_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new SalesException("sales.return_not_found"); var version = reader.GetInt64(0); var state = reader.GetString(1); await reader.CloseAsync();
            if (version != request.Version) throw new SalesException("sales.stale_return"); if (state is "Executed" or "Cancelled") throw new SalesException("sales.return_state_conflict");
            var now = clock.GetUtcNow(); await ExecuteAsync(context, "update business_sales.sales_return_request set state=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, id, target, now); AddOutbox(context, tenant, $"SalesReturn{target}V1", new { returnId = id }, now, id); return version + 1;
        }, ct);
    }

    public async Task SaveProfileAsync(TenantId tenant, Guid actor, SalesCustomerProfileRequest request, CancellationToken ct) => await ExecuteSimpleAsync(tenant, async (context, token) => { var now = clock.GetUtcNow(); await ExecuteAsync(context, "insert into business_sales.sales_customer_profile(tenant_id,id,legal_entity_id,party_id,default_salesperson_id,default_sales_team_id,default_sales_channel_id,default_warehouse_id,default_quote_validity_days,customer_po_required,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$11) on conflict(tenant_id,legal_entity_id,party_id) do update set default_salesperson_id=excluded.default_salesperson_id,default_sales_team_id=excluded.default_sales_team_id,default_sales_channel_id=excluded.default_sales_channel_id,default_warehouse_id=excluded.default_warehouse_id,default_quote_validity_days=excluded.default_quote_validity_days,customer_po_required=excluded.customer_po_required,version=business_sales.sales_customer_profile.version+1,updated_at=excluded.updated_at", token, tenant.Value, Guid.CreateVersion7(), request.LegalEntityId, request.PartyId, request.DefaultSalespersonId ?? (object)DBNull.Value, request.DefaultSalesTeamId ?? (object)DBNull.Value, request.DefaultSalesChannelId ?? (object)DBNull.Value, request.DefaultWarehouseId ?? (object)DBNull.Value, request.DefaultQuoteValidityDays ?? (object)DBNull.Value, request.CustomerPoRequired, now); }, ct);

    private async Task<int> TransitionAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, string target, string? reason, string eventType, CancellationToken ct) => await transactions.ExecuteAsync(tenant, async (context, token) => { var current = await ReadHeaderForUpdateAsync(context, tenant.Value, entity, id, token) ?? throw new SalesException("sales.quotation_not_found"); if (current.version != version) throw new SalesException("sales.stale_quotation"); if (current.state == target) return (int)current.version; if (current.state is "Accepted" or "Converted") throw new SalesException("sales.quotation_state_conflict"); var now = clock.GetUtcNow(); if (target == "Open") await ExecuteAsync(context, "update business_sales.quotation_revision set status='Open',sealed_at=$4 where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$5", token, tenant.Value, entity, id, now, current.revision); await ExecuteAsync(context, "update business_sales.quotation set state=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity, id, target, now); await AddHistoryAsync(context, tenant.Value, entity, "Quotation", id, current.revision, current.state, target, actor, reason, now, token); AddOutbox(context, tenant, eventType, new { quotationId = id, state = target }, now, id); return (int)current.version + 1; }, ct);
    private async Task<int> OrderStateAsync(TenantId tenant, Guid actor, Guid entity, Guid id, long version, string target, string? reason, string eventType, CancellationToken ct) => await transactions.ExecuteAsync(tenant, async (context, token) => { var row = await ReadOrderHeaderAsync(context, tenant.Value, entity, id, token) ?? throw new SalesException("sales.order_not_found"); if (row.version != version) throw new SalesException("sales.stale_order"); if (row.state == "Cancelled") throw new SalesException("sales.order_state_conflict"); var now = clock.GetUtcNow(); await ExecuteAsync(context, "update business_sales.sales_order set commercial_state=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, entity, id, target, now); AddOutbox(context, tenant, eventType, new { orderId = id, state = target }, now, id); return (int)row.version + 1; }, ct);

    private static async Task<string> NextNumberAsync(ITransactionContext context, Guid tenant, Guid entity, string kind, int year, CancellationToken ct)
    { await ExecuteAsync(context, "insert into business_sales.sales_number_sequence(tenant_id,legal_entity_id) values($1,$2) on conflict do nothing", ct, tenant, entity); var column = kind == "order" ? "order_last" : "quote_last"; await using var command = new NpgsqlCommand($"update business_sales.sales_number_sequence set {column}={column}+1 where tenant_id=$1 and legal_entity_id=$2 returning {column}", context.Connection, context.Transaction); Add(command, tenant, entity); var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); return $"{(kind == "order" ? "SO" : "Q")}-{year}-{value:D6}"; }
    private static async Task InsertRevisionAsync(ITransactionContext context, Guid tenant, Guid id, Guid entity, Guid quote, int revision, string status, IReadOnlyList<QuotationLineRequest> lines, (decimal net, decimal discount, decimal gross) totals, Guid actor, DateTimeOffset now, string taxEvidence, CancellationToken ct)
    { var revisionId = id; await ExecuteAsync(context, "insert into business_sales.quotation_revision(tenant_id,id,legal_entity_id,quotation_id,revision_number,status,snapshot,net_total,discount_total,gross_total,tax_preview_evidence,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", ct, tenant, revisionId, entity, quote, revision, status, Jsonb(JsonSerializer.Serialize(new { lines })), totals.net, totals.discount, totals.gross, Jsonb(taxEvidence), actor, now); await InsertRevisionLinesAsync(context, tenant, entity, revisionId, lines, ct); }
    private static async Task ReplaceLinesAsync(ITransactionContext context, Guid tenant, Guid entity, Guid revision, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct) { await ExecuteAsync(context, "delete from business_sales.quotation_revision_line where tenant_id=$1 and legal_entity_id=$2 and revision_id=$3", ct, tenant, entity, revision); await InsertRevisionLinesAsync(context, tenant, entity, revision, lines, ct); }
    private static async Task InsertRevisionLinesAsync(ITransactionContext context, Guid tenant, Guid entity, Guid revision, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct) { var seq = 0; foreach (var line in lines) { var net = line.Quantity * (line.UnitPrice ?? 0m) - line.DiscountAmount; await ExecuteAsync(context, "insert into business_sales.quotation_revision_line(tenant_id,id,legal_entity_id,revision_id,sequence,product_variant_id,unit_of_measure_id,uom_code,description,quantity,unit_price,discount_amount,net_amount) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", ct, tenant, Guid.CreateVersion7(), entity, revision, ++seq, line.ProductVariantId ?? (object)DBNull.Value, line.UnitOfMeasureId ?? (object)DBNull.Value, line.UomCode ?? (object)DBNull.Value, line.Description.Trim(), line.Quantity, line.UnitPrice ?? 0m, line.DiscountAmount, net); } }
    private static async Task InsertOrderLinesAsync(ITransactionContext context, Guid tenant, Guid entity, Guid order, IReadOnlyList<QuotationLineRequest> lines, CancellationToken ct) { var seq = 0; foreach (var line in lines) await ExecuteAsync(context, "insert into business_sales.sales_order_line(tenant_id,id,legal_entity_id,order_id,sequence,product_variant_id,unit_of_measure_id,description,ordered_quantity,unit_price) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", ct, tenant, Guid.CreateVersion7(), entity, order, ++seq, line.ProductVariantId ?? (object)DBNull.Value, line.UnitOfMeasureId ?? (object)DBNull.Value, line.Description.Trim(), line.Quantity, line.UnitPrice ?? 0m); }
    private static (decimal net, decimal discount, decimal gross) Totals(IReadOnlyList<QuotationLineRequest> lines) { var discount = lines.Sum(x => x.DiscountAmount); var net = lines.Sum(x => x.Quantity * (x.UnitPrice ?? 0m) - x.DiscountAmount); return (net, discount, net); }
    private static async Task AddHistoryAsync(ITransactionContext context, Guid tenant, Guid entity, string type, Guid id, int? revision, string? from, string to, Guid actor, string? reason, DateTimeOffset now, CancellationToken ct) => await ExecuteAsync(context, "insert into business_sales.sales_document_state_history(tenant_id,id,legal_entity_id,document_type,document_id,revision_number,from_state,to_state,actor_id,reason,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", ct, tenant, Guid.CreateVersion7(), entity, type, id, revision ?? (object)DBNull.Value, from ?? (object)DBNull.Value, to, actor, reason ?? (object)DBNull.Value, now);
    private static void AddOutbox(ITransactionContext context, TenantId tenant, string type, object payload, DateTimeOffset now, Guid id) => context.AddOutbox(new(Guid.CreateVersion7(), type, JsonSerializer.Serialize(payload), tenant, now, Guid.CreateVersion7(), id, "sales"));
    private static async Task<(Guid id, decimal net, decimal discount, decimal gross)> ReadRevisionAsync(NpgsqlConnection connection, Guid tenant, Guid entity, Guid quote, int revision, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,net_total,discount_total,gross_total from business_sales.quotation_revision where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$4", connection); Add(command, tenant, entity, quote, revision); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new SalesException("sales.revision_not_found"); return (reader.GetGuid(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3)); }
    private static async Task<IReadOnlyList<QuotationLineView>> ReadLinesAsync(NpgsqlConnection connection, Guid tenant, Guid entity, Guid revision, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,sequence,product_variant_id,unit_of_measure_id,uom_code,description,quantity,unit_price,discount_amount,net_amount from business_sales.quotation_revision_line where tenant_id=$1 and legal_entity_id=$2 and revision_id=$3 order by sequence", connection); Add(command, tenant, entity, revision); await using var reader = await command.ExecuteReaderAsync(ct); var list = new List<QuotationLineView>(); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9))); return list; }
    private static async Task<IReadOnlyList<QuotationAcceptanceView>> ReadAcceptancesAsync(NpgsqlConnection connection, Guid tenant, Guid entity, Guid quote, CancellationToken ct) { await using var command = new NpgsqlCommand("select revision_number,accepted_at,acceptance_method,recorded_by,evidence_reference,note from business_sales.quotation_acceptance where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 order by revision_number", connection); Add(command, tenant, entity, quote); await using var reader = await command.ExecuteReaderAsync(ct); var list = new List<QuotationAcceptanceView>(); while (await reader.ReadAsync(ct)) list.Add(new(reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5))); return list; }
    private static async Task<IReadOnlyList<SalesHistoryView>> ReadHistoryAsync(NpgsqlConnection connection, Guid tenant, Guid entity, Guid quote, CancellationToken ct) { await using var command = new NpgsqlCommand("select to_state,revision_number,from_state,occurred_at,actor_id,reason from business_sales.sales_document_state_history where tenant_id=$1 and legal_entity_id=$2 and document_type='Quotation' and document_id=$3 order by occurred_at,id", connection); Add(command, tenant, entity, quote); await using var reader = await command.ExecuteReaderAsync(ct); var list = new List<SalesHistoryView>(); while (await reader.ReadAsync(ct)) list.Add(new("state_changed", reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(3), reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5))); return list; }
    private static async Task<(Guid id, int revision, string state, long version)?> ReadHeaderForUpdateAsync(ITransactionContext context, Guid tenant, Guid entity, Guid id, CancellationToken ct) { await using var command = new NpgsqlCommand("select current_revision_number,state,version from business_sales.quotation where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); Add(command, tenant, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; return (id, reader.GetInt32(0), reader.GetString(1), reader.GetInt64(2)); }
    private static async Task<(Guid id, long version, string state)?> ReadOrderHeaderAsync(ITransactionContext context, Guid tenant, Guid entity, Guid id, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,version,commercial_state from business_sales.sales_order where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction); Add(command, tenant, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; return (reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2)); }
    private static Task<(Guid id, long version, string state)?> ReadOrderHeaderForUpdateAsync(ITransactionContext context, Guid tenant, Guid entity, Guid id, CancellationToken ct) => ReadOrderHeaderAsync(context, tenant, entity, id, ct);
    private static async Task<IReadOnlyList<SalesOrderLineView>> ReadOrderLinesAsync(NpgsqlConnection connection, Guid tenant, Guid entity, Guid order, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,sequence,product_variant_id,unit_of_measure_id,description,ordered_quantity,fulfilled_quantity,cancelled_quantity,invoiced_quantity,unit_price from business_sales.sales_order_line where tenant_id=$1 and legal_entity_id=$2 and order_id=$3 order by sequence", connection); Add(command, tenant, entity, order); await using var reader = await command.ExecuteReaderAsync(ct); var list = new List<SalesOrderLineView>(); while (await reader.ReadAsync(ct)) { var ordered = reader.GetDecimal(5); var fulfilled = reader.GetDecimal(6); var cancelled = reader.GetDecimal(7); list.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), ordered, fulfilled, cancelled, reader.GetDecimal(8), ordered - fulfilled - cancelled, reader.GetDecimal(9))); } return list; }
    private async Task<(Guid targetId, string key, string hash)?> ReadConversionAsync(TenantId tenant, Guid entity, Guid source, int revision, string key, string payloadHash, CancellationToken ct) { await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); tenantCommand.Parameters.AddWithValue(tenant.Value.ToString("D")); await tenantCommand.ExecuteNonQueryAsync(ct); await using var command = new NpgsqlCommand("select target_id,idempotency_key,payload_hash from business_sales.sales_conversion_link where tenant_id=$1 and legal_entity_id=$2 and source_type='Quotation' and source_id=$3 and source_revision=$4 and purpose='DirectInvoice'", connection); Add(command, tenant.Value, entity, source, revision); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; if (reader.GetString(1) != key.Trim() || reader.GetString(2) != payloadHash) throw new SalesException("sales.idempotency_conflict"); return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2)); }
    private static async Task<(Guid targetId, string key, string hash)?> ReadConversionAsync(ITransactionContext context, Guid tenant, Guid entity, Guid source, int revision, CancellationToken ct) { await using var command = new NpgsqlCommand("select target_id,idempotency_key,payload_hash from business_sales.sales_conversion_link where tenant_id=$1 and legal_entity_id=$2 and source_type='Quotation' and source_id=$3 and source_revision=$4 and purpose='DirectInvoice' for update", context.Connection, context.Transaction); Add(command, tenant, entity, source, revision); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2)); }
    private async Task<(Guid targetId, string key, string hash)?> ReadOrderConversionAsync(TenantId tenant, Guid entity, Guid source, string key, string payloadHash, CancellationToken ct) { await using var connection = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select target_id,idempotency_key,payload_hash from business_sales.sales_conversion_link where tenant_id=$1 and legal_entity_id=$2 and source_type='SalesOrder' and source_id=$3 and purpose='OrderInvoice'", connection); Add(command, tenant.Value, entity, source); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; if (reader.GetString(1) != key.Trim() || reader.GetString(2) != payloadHash) throw new SalesException("sales.idempotency_conflict"); return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2)); }
    private static async Task<(Guid targetId, string key, string hash)?> ReadOrderConversionAsync(ITransactionContext context, Guid tenant, Guid entity, Guid source, CancellationToken ct) { await using var command = new NpgsqlCommand("select target_id,idempotency_key,payload_hash from business_sales.sales_conversion_link where tenant_id=$1 and legal_entity_id=$2 and source_type='SalesOrder' and source_id=$3 and purpose='OrderInvoice' for update", context.Connection, context.Transaction); Add(command, tenant, entity, source); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2)); }
    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct) { var connection = await dataSource.OpenConnectionAsync(ct); await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); return connection; }
    private async Task ExecuteSimpleAsync(TenantId tenant, Func<ITransactionContext, CancellationToken, Task> action, CancellationToken ct) => await transactions.ExecuteAsync(tenant, async (context, token) => { await action(context, token); return 0; }, ct);
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); await command.ExecuteNonQueryAsync(ct); }
    private static async Task<object?> ExecuteScalarAsync(ITransactionContext context, string sql, CancellationToken ct, params object[] values) { await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); Add(command, values); return await command.ExecuteScalarAsync(ct); }
    private static async Task<(Guid id, int revision)?> ReadAcceptanceByKeyAsync(ITransactionContext context, Guid tenant, Guid entity, Guid quote, string key, CancellationToken ct) { await using var command = new NpgsqlCommand("select id,revision_number from business_sales.quotation_acceptance where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and idempotency_key=$4 for update", context.Connection, context.Transaction); Add(command, tenant, entity, quote, key.Trim()); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; return (reader.GetGuid(0), reader.GetInt32(1)); }
    private static async Task<Guid?> ReadAcceptanceByRevisionAsync(ITransactionContext context, Guid tenant, Guid entity, Guid quote, int revision, CancellationToken ct) { await using var command = new NpgsqlCommand("select id from business_sales.quotation_acceptance where tenant_id=$1 and legal_entity_id=$2 and quotation_id=$3 and revision_number=$4 for update", context.Connection, context.Transaction); Add(command, tenant, entity, quote, revision); return await command.ExecuteScalarAsync(ct) is Guid id ? id : null; }
    private static void Add(NpgsqlCommand command, params object[] values) { for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue($"p{i + 1}", values[i] ?? DBNull.Value); }
    private static void Add(NpgsqlCommand command, Guid tenant, Guid entity, params object[] values) { command.Parameters.AddWithValue("p1", tenant); command.Parameters.AddWithValue("p2", entity); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue($"p{i + 3}", values[i] ?? DBNull.Value); }
    private static NpgsqlParameter Jsonb(string value) => new() { Value = string.IsNullOrWhiteSpace(value) ? "{}" : value, NpgsqlDbType = NpgsqlDbType.Jsonb };
}
