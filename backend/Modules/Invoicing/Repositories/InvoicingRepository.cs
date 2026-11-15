using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Invoicing.DTOs;
using Modules.Invoicing.Entities;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Contracts.Invoicing;
using Shared.Primitives.Identifiers;

namespace Modules.Invoicing.Repositories;

public sealed class InvoicingRepository(NpgsqlDataSource dataSource, ITransactionCoordinator transactions, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsSupplierNumberConflict(Exception exception) =>
        exception is PostgresException { SqlState: "23505", ConstraintName: "ux_invoice_supplier_external_number" };

    public async Task<Guid?> GetBranchIdAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select branch_id from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, legalEntityId, invoiceId);
        var result = await command.ExecuteScalarAsync(ct);
        await transaction.CommitAsync(ct);
        if (result is null) throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found.");
        return result is DBNull ? null : (Guid)result;
    }

    public async Task<Guid?> GetExistingSourceInvoiceIdAsync(TenantId tenant, Guid legalEntityId, InvoiceSourceReferenceContract source, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await LockSourceIdentityAsync(connection, transaction, tenant, legalEntityId, source, ct);
        await using var command = new NpgsqlCommand("select id,source_idempotency_key,source_payload_hash from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and source_module=$3 and source_document_id=$4 and source_purpose=$5", connection, transaction);
        Add(command, tenant.Value, legalEntityId, source.SourceModule, source.SourceDocumentId, source.Purpose);
        await using var reader = await command.ExecuteReaderAsync(ct);
        Guid? existingId = null;
        if (await reader.ReadAsync(ct))
        {
            existingId = reader.GetGuid(0);
            if (reader.GetString(1) != source.IdempotencyKey || reader.GetString(2).Trim() != source.PayloadHash)
                throw new InvoiceException("invoicing.source_idempotency_conflict", "This source document was already used with different request facts.");
        }
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return existingId;
    }

    public Task<Guid> AddAttachmentReferenceAsync(TenantId tenant, Guid actorId, Guid membershipId, InvoiceAttachmentReferenceRequest request,
        Guid correlationId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        await EnsureInvoiceExistsAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, request.InvoiceId, token);
        var id = Guid.CreateVersion7();
        await using (var insert = new NpgsqlCommand("insert into business_invoicing.invoice_attachment(tenant_id,legal_entity_id,invoice_id,id,file_ref,attachment_role,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict (tenant_id,invoice_id,file_ref,attachment_role) do nothing returning id", context.Connection, context.Transaction))
        {
            Add(insert, tenant.Value, request.LegalEntityId, request.InvoiceId, id, request.File.FileRef, request.Role.ToString(), actorId, clock.GetUtcNow());
            if (await insert.ExecuteScalarAsync(token) is Guid insertedId) id = insertedId;
            else
            {
                await using var find = new NpgsqlCommand("select id from business_invoicing.invoice_attachment where tenant_id=$1 and invoice_id=$2 and file_ref=$3 and attachment_role=$4", context.Connection, context.Transaction);
                Add(find, tenant.Value, request.InvoiceId, request.File.FileRef, request.Role.ToString());
                id = await find.ExecuteScalarAsync(token) is Guid existingId ? existingId : throw new InvoiceException("invoicing.attachment_conflict", "The invoice attachment could not be recorded.");
            }
        }
        var now = clock.GetUtcNow();
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "User", actorId, membershipId,
            "invoicing", "invoice.attachment_added", "invoice", request.InvoiceId, null, JsonSerializer.Serialize(new { request.File.FileRef, role = request.Role.ToString(), request.File.Sha256 }, JsonOptions), null,
            "platform-files", correlationId, null, now, null), token);
        context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceAttachmentAddedV1", JsonSerializer.Serialize(new { request.InvoiceId, request.LegalEntityId, request.File.FileRef, role = request.Role.ToString() }, JsonOptions), tenant, now, correlationId, request.InvoiceId, "invoicing"));
        return id;
    }, ct);

    public Task<Guid> RecordIssuedArtifactReferenceAsync(TenantId tenant, Guid actorId, InvoiceIssuedArtifactReferenceRequest request,
        Guid correlationId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        var lifecycle = await EnsureInvoiceExistsAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, request.InvoiceId, token);
        if (lifecycle != "Finalized") throw new InvoiceException("invoicing.artifact_requires_finalized", "An original issued artifact can only be attached to a finalized invoice.");
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using (var insert = new NpgsqlCommand("insert into business_invoicing.invoice_issued_artifact(tenant_id,legal_entity_id,invoice_id,id,file_ref,sha256,template_code,template_version,language_code,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) on conflict (tenant_id,invoice_id,language_code,template_code,template_version) do nothing returning id", context.Connection, context.Transaction))
        {
            Add(insert, tenant.Value, request.LegalEntityId, request.InvoiceId, id, request.File.FileRef, request.File.Sha256, request.TemplateCode, request.TemplateVersion, request.LanguageCode, actorId, now);
            if (await insert.ExecuteScalarAsync(token) is Guid insertedId) id = insertedId;
            else
            {
                await using var existing = new NpgsqlCommand("select id,file_ref,sha256 from business_invoicing.invoice_issued_artifact where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and language_code=$4 and template_code=$5 and template_version=$6", context.Connection, context.Transaction);
                Add(existing, tenant.Value, request.LegalEntityId, request.InvoiceId, request.LanguageCode, request.TemplateCode, request.TemplateVersion);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InvoiceException("invoicing.artifact_conflict", "The original document could not be resolved after a competing write.");
                var existingArtifactId = reader.GetGuid(0);
                if (reader.GetGuid(1) != request.File.FileRef || reader.GetString(2).Trim() != request.File.Sha256)
                    throw new InvoiceException("invoicing.artifact_conflict", "A different original document is already recorded for this template and language.");
                await reader.CloseAsync();
                return existingArtifactId;
            }
        }
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "System", actorId, null,
            "invoicing", "invoice.issued_artifact_recorded", "invoice", request.InvoiceId, null, JsonSerializer.Serialize(new { request.File.FileRef, request.File.Sha256, request.TemplateCode, request.TemplateVersion, request.LanguageCode }, JsonOptions), null,
            "document-renderer", correlationId, null, now, null), token);
        context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceIssuedArtifactRecordedV1", JsonSerializer.Serialize(new { request.InvoiceId, request.LegalEntityId, request.File.FileRef, request.File.Sha256, request.TemplateCode, request.TemplateVersion, request.LanguageCode }, JsonOptions), tenant, now, correlationId, request.InvoiceId, "invoicing"));
        return id;
    }, ct);

    public Task<Guid> CreateDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, CreateInvoiceDraftRequest request, string direction, string partyDisplayName,
        InvoiceSourceReferenceContract? source, Guid correlationId, CancellationToken ct)
    {
        var invoiceId = Guid.CreateVersion7();
        var lines = ParseLines(request.Lines);
        return transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var now = clock.GetUtcNow();
            if (source is not null)
            {
                await LockSourceIdentityAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, source, token);
                await using var existing = new NpgsqlCommand("select id,source_idempotency_key,source_payload_hash from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and source_module=$3 and source_document_id=$4 and source_purpose=$5", context.Connection, context.Transaction);
                Add(existing, tenant.Value, request.LegalEntityId, source.SourceModule, source.SourceDocumentId, source.Purpose);
                await using var existingReader = await existing.ExecuteReaderAsync(token);
                if (await existingReader.ReadAsync(token))
                {
                    var existingId = existingReader.GetGuid(0);
                    var existingKey = existingReader.GetString(1);
                    var existingHash = existingReader.GetString(2).Trim();
                    await existingReader.CloseAsync();
                    if (existingKey != source.IdempotencyKey || existingHash != source.PayloadHash)
                        throw new InvoiceException("invoicing.source_idempotency_conflict", "This source document was already used with different request facts.");
                    return existingId;
                }
            }
            await using (var command = new NpgsqlCommand("insert into business_invoicing.invoice(tenant_id,legal_entity_id,id,branch_id,direction,document_type,lifecycle_state,party_id,address_id,document_date,posting_date,tax_date,supply_date,currency_code,price_mode,payment_term_id,document_discount_amount,supplier_invoice_number,supplier_invoice_number_key,source_module,source_document_id,source_purpose,source_idempotency_key,source_payload_hash,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,'Invoice','Draft',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,1,$23,$23)", context.Connection, context.Transaction))
            {
                Add(command, tenant.Value, request.LegalEntityId, invoiceId, Db(request.BranchId), direction, request.PartyId, Db(request.AddressId), request.DocumentDate,
                    request.PostingDate, request.TaxDate, Db(request.SupplyDate), NormalizeCurrency(request.CurrencyCode), request.PriceMode, Db(request.PaymentTermId), ParseOptional(request.DocumentDiscountAmount) ?? 0m, Db(Clean(request.SupplierInvoiceNumber)),
                    Db(NormalizeSupplierNumber(request.SupplierInvoiceNumber)), Db(source?.SourceModule), Db(source?.SourceDocumentId), Db(source?.Purpose), Db(source?.IdempotencyKey), Db(source?.PayloadHash), now);
                await command.ExecuteNonQueryAsync(token);
            }
            await InsertLinesAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, lines, token);
            await InsertChargesAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, request.Charges, token);
            await UpsertProjectionAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, partyDisplayName, null, "Unknown", "Pending", token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId, "User", actorId, membershipId,
                "invoicing", "invoice.draft_created", "invoice", invoiceId, null, JsonSerializer.Serialize(new
                {
                    direction,
                    request.PartyId,
                    request.DocumentDate,
                    request.CurrencyCode,
                    priceOverrides = request.Lines.Where(line => line.UnitPrice is not null).Select(line => new { line.SourceLineReference, line.UnitPrice, line.PriceOverrideReason }).ToArray()
                }, JsonOptions), null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceDraftCreatedV1", JsonSerializer.Serialize(new { invoiceId, request.LegalEntityId, direction, partyId = request.PartyId }, JsonOptions), tenant, now, correlationId, invoiceId, "invoicing"));
            return invoiceId;
        }, ct);
    }

    public async Task<InvoiceCorrectionSource?> GetCorrectionSourceAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        InvoiceCorrectionSource? source = null;
        await using (var header = new NpgsqlCommand("select branch_id,direction,document_type,party_id,address_id,document_date,currency_code,payment_term_id,price_mode,lifecycle_state,gross_total from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction))
        {
            Add(header, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await header.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
                source = new(legalEntityId, NGuid(reader, 0), reader.GetString(1), reader.GetGuid(3), NGuid(reader, 4), reader.GetFieldValue<DateOnly>(5),
                    reader.GetString(6).Trim(), NGuid(reader, 7), reader.GetString(8), reader.GetString(9), reader.GetString(2), NDecimal(reader, 10), []);
        }
        if (source is null) { await transaction.CommitAsync(ct); return null; }
        var lines = new List<InvoiceCorrectionSourceLine>();
        await using (var command = new NpgsqlCommand("""
            select l.id,l.sequence,l.product_variant_id,l.unit_of_measure_id,l.unit_code_snapshot,l.description,l.quantity,
                   coalesce((select sum(cl.quantity) from business_invoicing.invoice_relationship rel
                       join business_invoicing.invoice correction on correction.tenant_id=rel.tenant_id and correction.legal_entity_id=rel.legal_entity_id and correction.id=rel.invoice_id
                       join business_invoicing.invoice_line cl on cl.tenant_id=correction.tenant_id and cl.legal_entity_id=correction.legal_entity_id and cl.invoice_id=correction.id
                       where rel.tenant_id=l.tenant_id and rel.legal_entity_id=l.legal_entity_id and rel.related_invoice_id=l.invoice_id
                         and rel.relationship_type='Credits' and correction.lifecycle_state='Finalized' and cl.source_line_reference=l.id::text),0) as credited_quantity,
                   p.final_unit_price,l.line_discount,l.price_override_reason,l.business_category_code
            from business_invoicing.invoice_line l
            left join business_invoicing.invoice_price_snapshot p on p.tenant_id=l.tenant_id and p.legal_entity_id=l.legal_entity_id and p.invoice_id=l.invoice_id and p.line_id=l.id
            where l.tenant_id=$1 and l.legal_entity_id=$2 and l.invoice_id=$3 order by l.sequence
            """, connection, transaction))
        {
            Add(command, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                lines.Add(new(reader.GetGuid(0), reader.GetInt32(1), NGuid(reader, 2), NGuid(reader, 3), NText(reader, 4), reader.GetString(5),
                    reader.GetDecimal(6), reader.GetDecimal(7), NDecimal(reader, 8), reader.GetDecimal(9), NText(reader, 10), NText(reader, 11)));
        }
        await transaction.CommitAsync(ct);
        return source with { Lines = lines };
    }

    public Task<Guid> CreateCorrectionDraftAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid originalInvoiceId,
        CreateInvoiceDraftRequest request, string direction, string documentType, string reason, string idempotencyKey, string payloadHash,
        string partyDisplayName, Guid correlationId, CancellationToken ct)
    {
        var invoiceId = Guid.CreateVersion7();
        var lines = ParseLines(request.Lines);
        var relationshipType = documentType == "CreditNote" ? "Credits" : "Debits";
        return transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var original = new NpgsqlCommand("select lifecycle_state,document_type,direction,party_id,currency_code from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(original, tenant.Value, request.LegalEntityId, originalInvoiceId);
                await using var reader = await original.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new InvoiceException("invoicing.invoice_not_found", "The original invoice was not found.");
                if (reader.GetString(0) != "Finalized" || reader.GetString(1) == "CreditNote" || reader.GetString(2) != direction
                    || reader.GetGuid(3) != request.PartyId || !reader.GetString(4).Trim().Equals(request.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    throw new InvoiceException("invoicing.correction_not_allowed", "A correction must reference a finalized document for the same party, direction, legal entity, and currency.");
            }
            await using (var existing = new NpgsqlCommand("select invoice_id,payload_hash from business_invoicing.invoice_relationship where tenant_id=$1 and legal_entity_id=$2 and related_invoice_id=$3 and relationship_type=$4 and idempotency_key=$5", context.Connection, context.Transaction))
            {
                Add(existing, tenant.Value, request.LegalEntityId, originalInvoiceId, relationshipType, idempotencyKey);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var existingId = reader.GetGuid(0);
                    if (!reader.GetString(1).Trim().Equals(payloadHash, StringComparison.Ordinal))
                        throw new InvoiceException("invoicing.idempotency_conflict", "This correction request key was already used for different facts.");
                    return existingId;
                }
            }
            var now = clock.GetUtcNow();
            await using (var insert = new NpgsqlCommand("insert into business_invoicing.invoice(tenant_id,legal_entity_id,id,branch_id,direction,document_type,lifecycle_state,party_id,address_id,document_date,posting_date,tax_date,supply_date,currency_code,price_mode,payment_term_id,document_discount_amount,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Draft',$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,1,$17,$17)", context.Connection, context.Transaction))
            {
                Add(insert, tenant.Value, request.LegalEntityId, invoiceId, Db(request.BranchId), direction, documentType, request.PartyId, Db(request.AddressId),
                    request.DocumentDate, request.PostingDate, request.TaxDate, Db(request.SupplyDate), NormalizeCurrency(request.CurrencyCode), request.PriceMode,
                    Db(request.PaymentTermId), ParseOptional(request.DocumentDiscountAmount) ?? 0m, now);
                await insert.ExecuteNonQueryAsync(token);
            }
            await InsertLinesAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, lines, token);
            await InsertChargesAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, request.Charges, token);
            await using (var relationship = new NpgsqlCommand("insert into business_invoicing.invoice_relationship(tenant_id,legal_entity_id,invoice_id,related_invoice_id,relationship_type,reason,idempotency_key,payload_hash,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", context.Connection, context.Transaction))
            {
                Add(relationship, tenant.Value, request.LegalEntityId, invoiceId, originalInvoiceId, relationshipType, reason.Trim(), idempotencyKey, payloadHash, now);
                await relationship.ExecuteNonQueryAsync(token);
            }
            await UpsertProjectionAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, invoiceId, partyDisplayName, null, "Unknown", "Pending", token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId,
                "User", actorId, membershipId, "invoicing", "invoice.correction_draft_created", "invoice", invoiceId, null,
                JsonSerializer.Serialize(new { originalInvoiceId, documentType, reason = reason.Trim(), idempotencyKey }, JsonOptions), null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceDraftCreatedV1", JsonSerializer.Serialize(new { invoiceId, request.LegalEntityId, direction, documentType, originalInvoiceId }, JsonOptions), tenant, now, correlationId, invoiceId, "invoicing"));
            return invoiceId;
        }, ct);
    }

    public async Task<long> UpdateDraftAsync(TenantId tenant, Guid invoiceId, Guid legalEntityId, Guid actorId, Guid membershipId, UpdateInvoiceDraftRequest request, string partyDisplayName, string direction, Guid correlationId, CancellationToken ct)
    {
        var lines = ParseLines(request.Lines);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var update = new NpgsqlCommand("update business_invoicing.invoice set party_id=$4,address_id=$5,document_date=$6,posting_date=$7,tax_date=$8,supply_date=$9,currency_code=$10,price_mode=$11,payment_term_id=$12,document_discount_amount=$13,supplier_invoice_number=$14,supplier_invoice_number_key=$15,row_version=row_version+1,updated_at=$16 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and lifecycle_state='Draft' and row_version=$17 returning row_version", context.Connection, context.Transaction);
            Add(update, tenant.Value, legalEntityId, invoiceId, request.PartyId, Db(request.AddressId), request.DocumentDate, request.PostingDate, request.TaxDate, Db(request.SupplyDate),
                NormalizeCurrency(request.CurrencyCode), request.PriceMode, Db(request.PaymentTermId), ParseOptional(request.DocumentDiscountAmount) ?? 0m, Db(Clean(request.SupplierInvoiceNumber)), Db(NormalizeSupplierNumber(request.SupplierInvoiceNumber)), clock.GetUtcNow(), request.Version);
            var result = await update.ExecuteScalarAsync(token);
            if (result is null)
            {
                var lifecycle = await ScalarAsync<string>(context.Connection, context.Transaction, "select lifecycle_state from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, invoiceId);
                if (lifecycle is null) throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found.");
                if (lifecycle != "Draft") throw new InvoiceException("invoicing.already_finalized", "A finalized invoice cannot be changed.");
                throw new InvoiceException("invoicing.draft_changed", "The invoice changed after it was loaded. Refresh and try again.");
            }
            await using (var delete = new NpgsqlCommand("delete from business_invoicing.invoice_line where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", context.Connection, context.Transaction))
            {
                Add(delete, tenant.Value, legalEntityId, invoiceId);
                await delete.ExecuteNonQueryAsync(token);
            }
            await using (var deleteCharges = new NpgsqlCommand("delete from business_invoicing.invoice_charge where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", context.Connection, context.Transaction))
            { Add(deleteCharges, tenant.Value, legalEntityId, invoiceId); await deleteCharges.ExecuteNonQueryAsync(token); }
            await InsertLinesAsync(context.Connection, context.Transaction, tenant, legalEntityId, invoiceId, lines, token);
            await InsertChargesAsync(context.Connection, context.Transaction, tenant, legalEntityId, invoiceId, request.Charges, token);
            await UpsertProjectionAsync(context.Connection, context.Transaction, tenant, legalEntityId, invoiceId, partyDisplayName, null, "Unknown", "Pending", token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null, "User", actorId, membershipId,
                "invoicing", "invoice.draft_updated", "invoice", invoiceId, null, JsonSerializer.Serialize(new
                {
                    priceOverrides = request.Lines.Where(line => line.UnitPrice is not null)
                    .Select(line => new { line.SourceLineReference, line.UnitPrice, line.PriceOverrideReason }).ToArray()
                }, JsonOptions), null, "api", correlationId, null, clock.GetUtcNow(), null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceDraftUpdatedV1", JsonSerializer.Serialize(new { invoiceId, legalEntityId, version = (long)result }, JsonOptions), tenant, clock.GetUtcNow(), correlationId, invoiceId, "invoicing"));
            return (long)result;
        }, ct);
    }

    public async Task<InvoiceComplianceSnapshotContract?> GetComplianceSnapshotAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select snapshot::text from business_invoicing.invoice_compliance_snapshot where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", connection, transaction);
        Add(command, tenant.Value, legalEntityId, invoiceId);
        var json = (string?)await command.ExecuteScalarAsync(ct);
        await transaction.CommitAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<InvoiceComplianceSnapshotContract>(json, JsonOptions);
    }

    public async Task<InvoiceIssuedArtifactRecord?> GetOriginalIssuedArtifactAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId,
        string templateCode, string templateVersion, string languageCode, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,file_ref,sha256,template_code,template_version,language_code from business_invoicing.invoice_issued_artifact where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and template_code=$4 and template_version=$5 and language_code=$6", connection, transaction);
        Add(command, tenant.Value, legalEntityId, invoiceId, templateCode, templateVersion, languageCode);
        await using var reader = await command.ExecuteReaderAsync(ct);
        InvoiceIssuedArtifactRecord? artifact = null;
        if (await reader.ReadAsync(ct))
            artifact = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2).Trim(), reader.GetString(3), reader.GetString(4), reader.GetString(5));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return artifact;
    }

    public async Task CancelDraftAsync(TenantId tenant, Guid invoiceId, Guid legalEntityId, long expectedVersion, Guid actorId, Guid membershipId, string reason, Guid correlationId, CancellationToken ct)
    {
        await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Length > 500)
                throw new InvoiceException("invoicing.cancellation_reason_required", "A clear reason is required to cancel a draft.");
            await using var update = new NpgsqlCommand("update business_invoicing.invoice set lifecycle_state='CancelledDraft',row_version=row_version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and lifecycle_state='Draft' and row_version=$4", context.Connection, context.Transaction);
            Add(update, tenant.Value, legalEntityId, invoiceId, expectedVersion, clock.GetUtcNow());
            if (await update.ExecuteNonQueryAsync(token) != 1)
            {
                var state = await ScalarAsync<string>(context.Connection, context.Transaction, "select lifecycle_state from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", token, tenant.Value, legalEntityId, invoiceId);
                if (state is null) throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found.");
                if (state != "Draft") throw new InvoiceException("invoicing.already_finalized", "Only an editable draft can be cancelled.");
                throw new InvoiceException("invoicing.draft_changed", "The invoice changed after it was loaded. Refresh and try again.");
            }
            var now = clock.GetUtcNow();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null, "User", actorId, membershipId,
                "invoicing", "invoice.draft_cancelled", "invoice", invoiceId, null, null, reason.Trim(), "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceDraftCancelledV1", JsonSerializer.Serialize(new { invoiceId, legalEntityId, reason = reason.Trim() }, JsonOptions), tenant, now, correlationId, invoiceId, "invoicing"));
            return true;
        }, ct);
    }

    public async Task<InvoiceAggregate?> GetAggregateAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select id,tenant_id,legal_entity_id,branch_id,direction,document_type,party_id,address_id,document_date,posting_date,tax_date,supply_date,currency_code,payment_term_id,document_discount_amount,supplier_invoice_number,row_version,lifecycle_state,price_mode,source_module,source_document_id,source_purpose,source_idempotency_key,source_payload_hash from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, legalEntityId, invoiceId);
        InvoiceAggregate? aggregate = null;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                var header = new
                {
                    Id = reader.GetGuid(0),
                    TenantId = reader.GetGuid(1),
                    EntityId = reader.GetGuid(2),
                    BranchId = NGuid(reader, 3),
                    Direction = Enum.Parse<InvoiceDirection>(reader.GetString(4)),
                    DocumentType = Enum.Parse<InvoiceDocumentType>(reader.GetString(5)),
                    PartyId = reader.GetGuid(6),
                    AddressId = NGuid(reader, 7),
                    DocumentDate = reader.GetFieldValue<DateOnly>(8),
                    PostingDate = reader.GetFieldValue<DateOnly>(9),
                    TaxDate = reader.GetFieldValue<DateOnly>(10),
                    SupplyDate = NDate(reader, 11),
                    Currency = reader.GetString(12).Trim(),
                    TermId = NGuid(reader, 13),
                    DocumentDiscountAmount = reader.GetDecimal(14),
                    SupplierNumber = NText(reader, 15),
                    Version = reader.GetInt64(16),
                    Lifecycle = Enum.Parse<InvoiceLifecycle>(reader.GetString(17)),
                    PriceMode = reader.GetString(18),
                    Source = NText(reader, 19) is { } module ? new InvoiceSourceReferenceContract(module, reader.GetGuid(20), reader.GetString(21), reader.GetString(22), reader.GetString(23).Trim()) : null
                };
                var lines = new List<InvoiceLineDraft>();
                await reader.DisposeAsync();
                await using var lineCommand = new NpgsqlCommand("select id,sequence,product_variant_id,unit_of_measure_id,unit_code_snapshot,description,quantity,explicit_unit_price,line_discount,source_line_reference,price_override_reason,business_category_code from business_invoicing.invoice_line where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by sequence", connection, transaction);
                Add(lineCommand, tenant.Value, legalEntityId, invoiceId);
                await using var lineReader = await lineCommand.ExecuteReaderAsync(ct);
                while (await lineReader.ReadAsync(ct)) lines.Add(new(lineReader.GetGuid(0), lineReader.GetInt32(1), NGuid(lineReader, 2), NGuid(lineReader, 3), NText(lineReader, 4), lineReader.GetString(5), lineReader.GetDecimal(6), NDecimal(lineReader, 7) is { } price ? D(price) : null, lineReader.GetDecimal(8), NText(lineReader, 9), NText(lineReader, 10), NText(lineReader, 11)));
                await lineReader.CloseAsync();
                var charges = new List<InvoiceChargeDraft>();
                await using (var chargeCommand = new NpgsqlCommand("select id,category,description,amount,tax_treatment,business_category_code from business_invoicing.invoice_charge where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by id", connection, transaction))
                {
                    Add(chargeCommand, tenant.Value, legalEntityId, invoiceId);
                    await using var chargeReader = await chargeCommand.ExecuteReaderAsync(ct);
                    while (await chargeReader.ReadAsync(ct)) charges.Add(new(chargeReader.GetGuid(0), chargeReader.GetString(1), chargeReader.GetString(2), chargeReader.GetDecimal(3), chargeReader.GetString(4), NText(chargeReader, 5)));
                }
                aggregate = new(header.Id, header.TenantId, header.EntityId, header.BranchId, header.Direction, header.DocumentType, header.PartyId, header.AddressId,
                    header.DocumentDate, header.PostingDate, header.TaxDate, header.SupplyDate, header.Currency, header.TermId, header.SupplierNumber, header.Version, header.Lifecycle, lines, header.DocumentDiscountAmount, charges, header.PriceMode, header.Source);
            }
        }
        await transaction.CommitAsync(ct);
        return aggregate;
    }

    public async Task<InvoiceDetail?> GetDetailAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var headerCommand = new NpgsqlCommand("select i.id,i.branch_id,i.direction,i.document_type,i.document_number,i.party_id,i.currency_code,i.document_date,i.posting_date,i.tax_date,i.supply_date,i.payment_term_id,i.supplier_invoice_number,i.lifecycle_state,i.row_version,i.net_total,i.discount_total,i.charge_total,i.tax_total,i.gross_total,i.rounding_adjustment,i.journal_entry_id,p.evidence,l.settlement_summary,l.regulatory_summary,i.document_discount_amount,i.taxable_total,i.price_mode,i.source_module,i.source_document_id,i.source_purpose from business_invoicing.invoice i left join business_invoicing.invoice_party_snapshot p on p.tenant_id=i.tenant_id and p.legal_entity_id=i.legal_entity_id and p.invoice_id=i.id left join business_invoicing.invoice_list_projection l on l.tenant_id=i.tenant_id and l.invoice_id=i.id where i.tenant_id=$1 and i.legal_entity_id=$2 and i.id=$3", connection, transaction);
        Add(headerCommand, tenant.Value, legalEntityId, invoiceId);
        Guid? branchId; string direction; string documentType; string? documentNumber; Guid partyId; string currency; DateOnly documentDate; DateOnly postingDate; DateOnly taxDate; DateOnly? supplyDate; Guid? termId; string? supplierNumber; string lifecycle; long version; InvoiceTotals? totals; Guid? journalId; string partyName; string? legalName; string? tradeName; string settlement; string regulatory; decimal documentDiscount; string priceMode; InvoiceSourceTrace? source;
        await using (var reader = await headerCommand.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            branchId = NGuid(reader, 1); direction = reader.GetString(2); documentType = reader.GetString(3); documentNumber = NText(reader, 4); partyId = reader.GetGuid(5); currency = reader.GetString(6).Trim();
            documentDate = reader.GetFieldValue<DateOnly>(7); postingDate = reader.GetFieldValue<DateOnly>(8); taxDate = reader.GetFieldValue<DateOnly>(9); supplyDate = NDate(reader, 10); termId = NGuid(reader, 11); supplierNumber = NText(reader, 12); lifecycle = reader.GetString(13); version = reader.GetInt64(14);
            totals = reader.IsDBNull(19) ? null : new(D(reader.GetDecimal(15)), D(reader.GetDecimal(16)), D(reader.GetDecimal(17)), D(reader.GetDecimal(26)), D(reader.GetDecimal(18)), D(reader.GetDecimal(19)), D(reader.GetDecimal(20)), currency);
            journalId = NGuid(reader, 21); partyName = ""; legalName = null; tradeName = null;
            documentDiscount = reader.GetDecimal(25);
            priceMode = reader.GetString(27);
            source = reader.IsDBNull(28) ? null : new(reader.GetString(28), reader.GetGuid(29), reader.GetString(30));
            if (!reader.IsDBNull(22))
            {
                using var party = JsonDocument.Parse(reader.GetString(22));
                var root = party.RootElement;
                partyName = root.TryGetProperty("displayName", out var display) ? display.GetString() ?? "" : "";
                legalName = root.TryGetProperty("legalName", out var legal) && legal.ValueKind != JsonValueKind.Null ? legal.GetString() : null;
                tradeName = root.TryGetProperty("tradeName", out var trade) && trade.ValueKind != JsonValueKind.Null ? trade.GetString() : null;
            }
            settlement = NText(reader, 23) ?? "Unknown"; regulatory = NText(reader, 24) ?? "Pending";
        }
        if (partyName.Length == 0) partyName = await ScalarAsync<string>(connection, transaction, "select party_display_name from business_invoicing.invoice_list_projection where tenant_id=$1 and invoice_id=$2", ct, tenant.Value, invoiceId) ?? "";
        var addressEvidence = await ScalarAsync<string>(connection, transaction, "select evidence::text from business_invoicing.invoice_address_snapshot where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", ct, tenant.Value, legalEntityId, invoiceId);

        var lines = new List<InvoiceLineResult>();
        var storedLineTotals = new Dictionary<Guid, (decimal? Tax, decimal? Gross)>();
        await using (var lineCommand = new NpgsqlCommand("select l.id,l.sequence,l.product_variant_id,l.unit_of_measure_id,l.unit_code_snapshot,l.description,l.quantity,l.line_discount,p.resolved_unit_price,p.final_unit_price,l.line_net_total,l.line_tax_total,l.line_gross_total,l.product_evidence::text,l.unit_evidence::text from business_invoicing.invoice_line l left join business_invoicing.invoice_price_snapshot p on p.tenant_id=l.tenant_id and p.legal_entity_id=l.legal_entity_id and p.invoice_id=l.invoice_id and p.line_id=l.id where l.tenant_id=$1 and l.legal_entity_id=$2 and l.invoice_id=$3 order by l.sequence", connection, transaction))
        {
            Add(lineCommand, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await lineCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var quantity = reader.GetDecimal(6); var resolved = NDecimal(reader, 8); var final = NDecimal(reader, 9);
                var net = NDecimal(reader, 10) ?? (final is null ? 0m : quantity * final.Value - reader.GetDecimal(7));
                var storedTax = NDecimal(reader, 11); var storedGross = NDecimal(reader, 12);
                var lineId = reader.GetGuid(0);
                storedLineTotals[lineId] = (storedTax, storedGross);
                lines.Add(new(lineId, reader.GetInt32(1), NGuid(reader, 2), NGuid(reader, 3), NText(reader, 4), reader.GetString(5), D(quantity), resolved is null ? null : D(resolved.Value), D(final ?? 0m), D(net), "0", D(net), null, null, NText(reader, 13), NText(reader, 14)));
            }
        }
        var schedules = new List<InvoiceScheduleLine>();
        await using (var scheduleCommand = new NpgsqlCommand("select sequence,due_date,amount,currency_code from business_invoicing.invoice_payment_schedule where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by sequence", connection, transaction))
        {
            Add(scheduleCommand, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await scheduleCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) schedules.Add(new(reader.GetInt32(0), reader.GetFieldValue<DateOnly>(1), D(reader.GetDecimal(2)), reader.GetString(3).Trim()));
        }
        var taxesByLine = new Dictionary<Guid, List<Shared.Contracts.Tax.TaxLineContract>>();
        await using (var taxCommand = new NpgsqlCommand("select line_id,tax_code,tax_type_code,treatment,tax_base,tax_amount,evidence from business_invoicing.invoice_tax_component where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by line_id,sequence", connection, transaction))
        {
            Add(taxCommand, tenant.Value, legalEntityId, invoiceId);
            await using var taxReader = await taxCommand.ExecuteReaderAsync(ct);
            while (await taxReader.ReadAsync(ct))
            {
                var lineId = taxReader.GetGuid(0);
                using var evidence = JsonDocument.Parse(taxReader.GetString(6));
                var root = evidence.RootElement;
                string? String(string name) => root.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : null;
                Guid GuidValue(string name) => root.TryGetProperty(name, out var property) && property.TryGetGuid(out var value) ? value : Guid.Empty;
                var item = new Shared.Contracts.Tax.TaxLineContract(taxReader.GetString(2), taxReader.GetString(1), taxReader.GetString(3), String("rate"), null,
                    D(taxReader.GetDecimal(4)), D(taxReader.GetDecimal(5)), currency, String("jurisdictionCode") ?? "", String("legalReasonCode"), String("legalReasonText"), String("reportingCode"),
                    GuidValue("ruleId"), String("ruleVersion") ?? "", null);
                if (!taxesByLine.TryGetValue(lineId, out var values)) taxesByLine[lineId] = values = [];
                values.Add(item);
            }
        }
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var components = taxesByLine.GetValueOrDefault(line.Id) ?? [];
            var taxTotal = components.Sum(component => ParseRequired(component.TaxAmount));
            var stored = storedLineTotals[line.Id];
            lines[index] = line with { TaxAmount = D(stored.Tax ?? taxTotal), GrossAmount = D(stored.Gross ?? (ParseRequired(line.NetAmount) + taxTotal)), TaxComponents = components };
        }
        var chargeGroups = new Dictionary<Guid, (string Code, string Description, decimal Amount, string Treatment, List<InvoiceChargeAllocationDetail> Allocations)>();
        await using (var chargeCommand = new NpgsqlCommand("select c.id,c.category,c.description,c.amount,c.tax_treatment,a.line_id,a.allocated_amount from business_invoicing.invoice_charge c left join business_invoicing.invoice_charge_allocation a on a.tenant_id=c.tenant_id and a.legal_entity_id=c.legal_entity_id and a.invoice_id=c.invoice_id and a.charge_id=c.id where c.tenant_id=$1 and c.legal_entity_id=$2 and c.invoice_id=$3 order by c.id,a.line_id", connection, transaction))
        {
            Add(chargeCommand, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await chargeCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                if (!chargeGroups.TryGetValue(id, out var value)) value = (reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), []);
                if (!reader.IsDBNull(5)) value.Allocations.Add(new(reader.GetGuid(5), D(reader.GetDecimal(6))));
                chargeGroups[id] = value;
            }
        }
        var charges = chargeGroups.Select(item => new InvoiceChargeDetail(item.Key, item.Value.Code, item.Value.Description, D(item.Value.Amount), item.Value.Treatment, item.Value.Allocations)).ToArray();
        var discounts = new List<InvoiceDiscountAllocationDetail>();
        await using (var discountCommand = new NpgsqlCommand("select line_id,discount_type,source_amount,allocated_amount from business_invoicing.invoice_discount_allocation where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by discount_type,line_id", connection, transaction))
        {
            Add(discountCommand, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await discountCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) discounts.Add(new(reader.GetGuid(0), reader.GetString(1), D(reader.GetDecimal(2)), D(reader.GetDecimal(3))));
        }
        await transaction.CommitAsync(ct);
        return new(invoiceId, legalEntityId, branchId, direction, documentType, documentNumber, partyId, partyName, legalName, tradeName, currency, documentDate,
            postingDate, taxDate, supplyDate, termId, supplierNumber, lifecycle, version, totals, lines, schedules, settlement, regulatory, journalId,
            D(documentDiscount), charges, discounts, priceMode, addressEvidence, source);
    }

    public async Task<InvoicePage> ListAsync(TenantId tenant, InvoiceListQuery query, string direction, CancellationToken ct)
    {
        if (query.Page is < 1 or > 10000 || query.PageSize is < 1 or > 100 || (query.ToDate.HasValue && query.FromDate > query.ToDate))
            throw new InvoiceException("invoicing.query_invalid", "Invoice search filters are invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        const string filter = "i.tenant_id=$1 and i.legal_entity_id=$2 and i.direction=$3 and ($4::uuid is null or i.branch_id=$4) and ($5::uuid is null or i.party_id=$5) and ($6::date is null or i.document_date >= $6) and ($7::date is null or i.document_date <= $7) and ($8::text is null or i.currency_code=$8) and ($9::text is null or i.lifecycle_state=$9) and ($10::text is null or p.party_display_name ilike '%' || $10 || '%' or coalesce(i.document_number,'') ilike '%' || $10 || '%' or coalesce(i.supplier_invoice_number,'') ilike '%' || $10 || '%')";
        await using var count = new NpgsqlCommand($"select count(*) from business_invoicing.invoice i join business_invoicing.invoice_list_projection p on p.tenant_id=i.tenant_id and p.invoice_id=i.id where {filter}", connection, transaction);
        Add(count, tenant.Value, query.LegalEntityId, direction, Db(query.BranchId), Db(query.PartyId), Db(query.FromDate), Db(query.ToDate), Db(query.CurrencyCode is null ? null : NormalizeCurrency(query.CurrencyCode)), Db(query.Lifecycle), Db(Clean(query.Search)));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        await using var page = new NpgsqlCommand($"select i.id,i.direction,i.document_type,i.document_number,p.party_display_name,i.document_date,i.currency_code,i.gross_total,i.lifecycle_state,i.row_version,i.legal_entity_id,i.branch_id from business_invoicing.invoice i join business_invoicing.invoice_list_projection p on p.tenant_id=i.tenant_id and p.invoice_id=i.id where {filter} order by i.document_date desc,i.id desc limit $11 offset $12", connection, transaction);
        Add(page, tenant.Value, query.LegalEntityId, direction, Db(query.BranchId), Db(query.PartyId), Db(query.FromDate), Db(query.ToDate), Db(query.CurrencyCode is null ? null : NormalizeCurrency(query.CurrencyCode)), Db(query.Lifecycle), Db(Clean(query.Search)), query.PageSize, (query.Page - 1) * query.PageSize);
        var items = new List<InvoiceListItem>();
        await using var reader = await page.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), NText(reader, 3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6).Trim(), reader.IsDBNull(7) ? null : D(reader.GetDecimal(7)), reader.GetString(8), reader.GetInt64(9), reader.GetGuid(10), NGuid(reader, 11)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public static async Task<InvoiceFinalizationRecord?> LockForFinalizationAsync(ITransactionContext context, TenantId tenant, Guid legalEntityId, Guid invoiceId,
        InvoiceFinalizationRequest request, string payloadHash, decimal candidateGrossTotal, CancellationToken ct)
    {
        await using var lockCommand = new NpgsqlCommand("select lifecycle_state,row_version,finalization_key,direction,party_id,supplier_invoice_number_key,currency_code,document_type from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        Add(lockCommand, tenant.Value, legalEntityId, invoiceId);
        await using var reader = await lockCommand.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found.");
        var lifecycle = reader.GetString(0); var version = reader.GetInt64(1); var priorKey = NText(reader, 2); var direction = reader.GetString(3); var partyId = reader.GetGuid(4);
        var supplierNumberKey = NText(reader, 5); var currencyCode = reader.GetString(6).Trim(); var documentType = reader.GetString(7); await reader.CloseAsync();
        if (lifecycle != "Draft")
        {
            await using var prior = new NpgsqlCommand("select r.idempotency_key,r.payload_hash,r.document_number,r.journal_entry_id,r.open_item_ids,i.net_total,i.taxable_total,i.discount_total,i.charge_total,i.tax_total,i.gross_total,i.rounding_adjustment,i.currency_code from business_invoicing.invoice_finalization_record r join business_invoicing.invoice i on i.tenant_id=r.tenant_id and i.legal_entity_id=r.legal_entity_id and i.id=r.invoice_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.invoice_id=$3", context.Connection, context.Transaction);
            Add(prior, tenant.Value, legalEntityId, invoiceId);
            await using var priorReader = await prior.ExecuteReaderAsync(ct);
            if (await priorReader.ReadAsync(ct))
            {
                var key = priorReader.GetString(0); var hash = priorReader.GetString(1).Trim(); var number = priorReader.GetString(2); var journal = priorReader.GetGuid(3); var items = (Guid[])priorReader.GetValue(4);
                var totals = new InvoiceTotals(D(priorReader.GetDecimal(5)), D(priorReader.GetDecimal(7)), D(priorReader.GetDecimal(8)), D(priorReader.GetDecimal(6)), D(priorReader.GetDecimal(9)), D(priorReader.GetDecimal(10)), D(priorReader.GetDecimal(11)), priorReader.GetString(12).Trim());
                if (key == request.IdempotencyKey && hash == payloadHash)
                {
                    await priorReader.CloseAsync();
                    var schedule = new List<InvoiceScheduleLine>();
                    await using var scheduleCommand = new NpgsqlCommand("select sequence,due_date,amount,currency_code from business_invoicing.invoice_payment_schedule where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by sequence", context.Connection, context.Transaction);
                    Add(scheduleCommand, tenant.Value, legalEntityId, invoiceId);
                    await using var scheduleReader = await scheduleCommand.ExecuteReaderAsync(ct);
                    while (await scheduleReader.ReadAsync(ct)) schedule.Add(new(scheduleReader.GetInt32(0), scheduleReader.GetFieldValue<DateOnly>(1), D(scheduleReader.GetDecimal(2)), scheduleReader.GetString(3).Trim()));
                    return new(number, journal, items, totals, schedule, true);
                }
            }
            throw new InvoiceException("invoicing.already_finalized", "This invoice is already finalized. Create a correction document to change its financial facts.");
        }
        if (version != request.Version) throw new InvoiceException("invoicing.draft_changed", "The invoice changed after it was loaded. Refresh and try again.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200) throw new InvoiceException("invoicing.idempotency_key_invalid", "A valid finalization request key is required.");
        if (priorKey is not null && priorKey != request.IdempotencyKey) throw new InvoiceException("invoicing.finalization_conflict", "The invoice is already being finalized with another request key.");
        if (direction == "Payable" && supplierNumberKey is not null)
        {
            await using var duplicateSupplierNumber = new NpgsqlCommand("select exists(select 1 from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and party_id=$3 and supplier_invoice_number_key=$4 and lifecycle_state='Finalized' and id<>$5)", context.Connection, context.Transaction);
            Add(duplicateSupplierNumber, tenant.Value, legalEntityId, partyId, supplierNumberKey, invoiceId);
            if ((bool)(await duplicateSupplierNumber.ExecuteScalarAsync(ct) ?? false)) throw new InvoiceException("invoicing.supplier_duplicate", "This supplier invoice number was already recorded for this supplier.");
        }
        await using var duplicate = new NpgsqlCommand("select payload_hash from business_invoicing.invoice_finalization_record where tenant_id=$1 and legal_entity_id=$2 and idempotency_key=$3", context.Connection, context.Transaction);
        Add(duplicate, tenant.Value, legalEntityId, request.IdempotencyKey);
        if (await duplicate.ExecuteScalarAsync(ct) is string existingHash && !string.Equals(existingHash.Trim(), payloadHash, StringComparison.Ordinal))
            throw new InvoiceException("invoicing.idempotency_conflict", "This request key was already used for different invoice facts.");
        await EnsureCorrectionCapacityAsync(context, tenant, legalEntityId, invoiceId, direction, partyId, currencyCode, documentType, candidateGrossTotal, ct);
        return null;
    }

    private static async Task EnsureCorrectionCapacityAsync(ITransactionContext context, TenantId tenant, Guid legalEntityId, Guid invoiceId,
        string direction, Guid partyId, string currencyCode, string documentType, decimal candidateGrossTotal, CancellationToken ct)
    {
        Guid? originalInvoiceId = null;
        string? relationshipType = null;
        await using (var relation = new NpgsqlCommand("select related_invoice_id,relationship_type from business_invoicing.invoice_relationship where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3", context.Connection, context.Transaction))
        {
            Add(relation, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await relation.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { originalInvoiceId = reader.GetGuid(0); relationshipType = reader.GetString(1); }
        }
        if (originalInvoiceId is null) return;
        var expectedRelationship = documentType switch { "CreditNote" => "Credits", "DebitNote" => "Debits", _ => null };
        if (expectedRelationship is null || relationshipType != expectedRelationship)
            throw new InvoiceException("invoicing.correction_link_invalid", "The correction document type does not match its original-document relationship.");

        decimal originalGross;
        await using (var original = new NpgsqlCommand("select lifecycle_state,document_type,direction,party_id,currency_code,gross_total from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
        {
            Add(original, tenant.Value, legalEntityId, originalInvoiceId.Value);
            await using var reader = await original.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != "Finalized" || reader.GetString(1) == "CreditNote" || reader.GetString(2) != direction
                || reader.GetGuid(3) != partyId || !reader.GetString(4).Trim().Equals(currencyCode, StringComparison.OrdinalIgnoreCase) || reader.IsDBNull(5))
                throw new InvoiceException("invoicing.correction_not_allowed", "The finalized original no longer matches the correction document.");
            originalGross = reader.GetDecimal(5);
        }
        await using (var lineReferences = new NpgsqlCommand("""
            select exists(
                select 1 from business_invoicing.invoice_line candidate
                left join business_invoicing.invoice_line source on source.tenant_id=candidate.tenant_id and source.legal_entity_id=candidate.legal_entity_id
                    and source.invoice_id=$4 and source.id::text=candidate.source_line_reference
                where candidate.tenant_id=$1 and candidate.legal_entity_id=$2 and candidate.invoice_id=$3
                  and (source.id is null or candidate.product_variant_id is distinct from source.product_variant_id
                       or candidate.unit_of_measure_id is distinct from source.unit_of_measure_id)
            )
            """, context.Connection, context.Transaction))
        {
            Add(lineReferences, tenant.Value, legalEntityId, invoiceId, originalInvoiceId.Value);
            if ((bool)(await lineReferences.ExecuteScalarAsync(ct) ?? false))
                throw new InvoiceException("invoicing.correction_line_not_found", "Every correction line must retain its original product, unit, and line reference.");
        }
        if (relationshipType != "Credits") return;

        await using (var quantities = new NpgsqlCommand("""
            select exists(
                select 1 from business_invoicing.invoice_line candidate
                join business_invoicing.invoice_line source on source.tenant_id=candidate.tenant_id and source.legal_entity_id=candidate.legal_entity_id
                    and source.invoice_id=$4 and source.id::text=candidate.source_line_reference
                where candidate.tenant_id=$1 and candidate.legal_entity_id=$2 and candidate.invoice_id=$3
                  and candidate.quantity > source.quantity - coalesce((
                      select sum(prior_line.quantity) from business_invoicing.invoice_relationship prior_relation
                      join business_invoicing.invoice prior_correction on prior_correction.tenant_id=prior_relation.tenant_id
                          and prior_correction.legal_entity_id=prior_relation.legal_entity_id and prior_correction.id=prior_relation.invoice_id
                      join business_invoicing.invoice_line prior_line on prior_line.tenant_id=prior_correction.tenant_id
                          and prior_line.legal_entity_id=prior_correction.legal_entity_id and prior_line.invoice_id=prior_correction.id
                      where prior_relation.tenant_id=$1 and prior_relation.legal_entity_id=$2 and prior_relation.related_invoice_id=$4
                        and prior_relation.relationship_type='Credits' and prior_correction.lifecycle_state='Finalized'
                        and prior_line.source_line_reference=source.id::text and prior_correction.id<>$3),0)
            )
            """, context.Connection, context.Transaction))
        {
            Add(quantities, tenant.Value, legalEntityId, invoiceId, originalInvoiceId.Value);
            if ((bool)(await quantities.ExecuteScalarAsync(ct) ?? false))
                throw new InvoiceException("invoicing.credit_quantity_exceeded", "The finalized corrections already use the remaining quantity on one or more original lines.");
        }
        await using var amount = new NpgsqlCommand("select coalesce(sum(coalesce(relation.amount, correction.gross_total)),0) from business_invoicing.invoice_relationship relation join business_invoicing.invoice correction on correction.tenant_id=relation.tenant_id and correction.legal_entity_id=relation.legal_entity_id and correction.id=relation.invoice_id where relation.tenant_id=$1 and relation.legal_entity_id=$2 and relation.related_invoice_id=$3 and relation.relationship_type='Credits' and correction.lifecycle_state='Finalized' and correction.id<>$4", context.Connection, context.Transaction);
        Add(amount, tenant.Value, legalEntityId, originalInvoiceId.Value, invoiceId);
        var creditedGross = (decimal)(await amount.ExecuteScalarAsync(ct) ?? 0m);
        if (candidateGrossTotal > originalGross - creditedGross)
            throw new InvoiceException("invoicing.credit_amount_exceeded", "The credit note amount exceeds the remaining value of the original invoice.");
    }

    public async Task<InvoiceFinalizedResult> GetFinalizedResultAsync(TenantId tenant, Guid legalEntityId, Guid invoiceId, string idempotencyKey, string payloadHash, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select r.payload_hash,r.document_number,r.journal_entry_id,r.open_item_ids,i.net_total,i.taxable_total,i.discount_total,i.charge_total,i.tax_total,i.gross_total,i.rounding_adjustment,i.currency_code from business_invoicing.invoice_finalization_record r join business_invoicing.invoice i on i.tenant_id=r.tenant_id and i.legal_entity_id=r.legal_entity_id and i.id=r.invoice_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.invoice_id=$3 and r.idempotency_key=$4", connection, transaction);
        Add(command, tenant.Value, legalEntityId, invoiceId, idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvoiceException("invoicing.already_finalized", "This invoice is already finalized with another request key.");
        var storedHash = reader.GetString(0).Trim(); var number = reader.GetString(1); var journal = reader.GetGuid(2); var openItemIds = (Guid[])reader.GetValue(3);
        if (!string.Equals(storedHash, payloadHash, StringComparison.Ordinal)) throw new InvoiceException("invoicing.idempotency_conflict", "This request key was already used for different invoice facts.");
        var totals = new InvoiceTotals(D(reader.GetDecimal(4)), D(reader.GetDecimal(6)), D(reader.GetDecimal(7)), D(reader.GetDecimal(5)), D(reader.GetDecimal(8)), D(reader.GetDecimal(9)), D(reader.GetDecimal(10)), reader.GetString(11).Trim());
        await reader.CloseAsync();
        var schedule = new List<InvoiceScheduleLine>();
        await using (var scheduleCommand = new NpgsqlCommand("select sequence,due_date,amount,currency_code from business_invoicing.invoice_payment_schedule where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by sequence", connection, transaction))
        {
            Add(scheduleCommand, tenant.Value, legalEntityId, invoiceId);
            await using var scheduleReader = await scheduleCommand.ExecuteReaderAsync(ct);
            while (await scheduleReader.ReadAsync(ct)) schedule.Add(new(scheduleReader.GetInt32(0), scheduleReader.GetFieldValue<DateOnly>(1), D(scheduleReader.GetDecimal(2)), scheduleReader.GetString(3).Trim()));
        }
        await transaction.CommitAsync(ct);
        return new(invoiceId, number, "Finalized", totals, schedule, journal, openItemIds, "Submit this finalized document through the configured compliance workflow.");
    }

    public async Task FinalizeInTransactionAsync(ITransactionContext context, TenantId tenant, Guid invoiceId, Guid legalEntityId, string direction,
        string documentType, string documentNumber, InvoiceFinalizationRequest request, string payloadHash, InvoiceFinalizationData data,
        Guid journalEntryId, IReadOnlyList<Guid> openItemIds, Guid actorId, Guid membershipId, Guid correlationId, string sequenceKey, string periodKey,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var partySnapshot = new NpgsqlParameter { Value = data.PartyEvidenceJson, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb };
        await using (var command = new NpgsqlCommand("insert into business_invoicing.invoice_party_snapshot(tenant_id,legal_entity_id,invoice_id,party_id,schema_version,evidence,captured_at) select tenant_id,legal_entity_id,id,party_id,1,$4,$5 from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
        { Add(command, tenant.Value, legalEntityId, invoiceId, partySnapshot, now); await command.ExecuteNonQueryAsync(ct); }
        if (data.AddressEvidenceJson is not null)
        {
            var addressSnapshot = new NpgsqlParameter { Value = data.AddressEvidenceJson, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb };
            await using var command = new NpgsqlCommand("insert into business_invoicing.invoice_address_snapshot(tenant_id,legal_entity_id,invoice_id,address_id,schema_version,evidence,captured_at) select tenant_id,legal_entity_id,id,address_id,1,$4,$5 from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
            Add(command, tenant.Value, legalEntityId, invoiceId, addressSnapshot, now); await command.ExecuteNonQueryAsync(ct);
        }
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.Prices.Select(price => BatchCommand(
            "insert into business_invoicing.invoice_price_snapshot(tenant_id,legal_entity_id,invoice_id,line_id,resolved_unit_price,final_unit_price,evidence,schema_version) values($1,$2,$3,$4,$5,$6,$7,1)",
            tenant.Value, legalEntityId, invoiceId, price.LineId, Db(price.ResolvedUnitPrice is null ? null : ParseRequired(price.ResolvedUnitPrice)), ParseRequired(price.FinalUnitPrice),
            new NpgsqlParameter { Value = price.EvidenceJson, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb })), ct);
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.Units.Select(unit => BatchCommand(
            "update business_invoicing.invoice_line set unit_of_measure_id=$4,unit_code_snapshot=$5,product_evidence=$6::jsonb,unit_evidence=$7::jsonb where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and id=$8",
            tenant.Value, legalEntityId, invoiceId, Db(unit.UnitOfMeasureId), Db(unit.UnitCode), Db(unit.ProductEvidenceJson), Db(unit.UnitEvidenceJson), unit.LineId)), ct);
        if (await ExecuteBatchAsync(context.Connection, context.Transaction, data.LineAmounts.Select(amount => BatchCommand(
            "update business_invoicing.invoice_line set line_net_total=$4,line_tax_total=$5,line_gross_total=$6 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and id=$7",
            tenant.Value, legalEntityId, invoiceId, ParseRequired(amount.NetAmount), ParseRequired(amount.TaxAmount), ParseRequired(amount.GrossAmount), amount.LineId)), ct) != data.LineAmounts.Count)
            throw new InvoiceException("invoicing.invoice_not_found", "An invoice line disappeared during finalization.");
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.Taxes.Select(tax => BatchCommand(
            "insert into business_invoicing.invoice_tax_component(tenant_id,legal_entity_id,invoice_id,line_id,sequence,tax_code,tax_type_code,treatment,tax_base,tax_amount,evidence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",
            tenant.Value, legalEntityId, invoiceId, tax.LineId, tax.Sequence, tax.TaxCode, tax.TaxTypeCode, tax.Treatment, ParseRequired(tax.TaxableBase), ParseRequired(tax.TaxAmount),
            new NpgsqlParameter { Value = tax.EvidenceJson, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb })), ct);
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.DiscountAllocations.Select(allocation => BatchCommand(
            "insert into business_invoicing.invoice_discount_allocation(tenant_id,legal_entity_id,invoice_id,line_id,discount_type,source_amount,allocated_amount) values($1,$2,$3,$4,$5,$6,$7)",
            tenant.Value, legalEntityId, invoiceId, allocation.LineId, allocation.DiscountType, ParseRequired(allocation.SourceAmount), ParseRequired(allocation.AllocatedAmount))), ct);
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.ChargeAllocations.Select(allocation => BatchCommand(
            "insert into business_invoicing.invoice_charge_allocation(tenant_id,legal_entity_id,invoice_id,charge_id,line_id,allocated_amount) values($1,$2,$3,$4,$5,$6)",
            tenant.Value, legalEntityId, invoiceId, allocation.ChargeId, allocation.LineId, ParseRequired(allocation.Amount))), ct);
        await ExecuteBatchAsync(context.Connection, context.Transaction, data.Schedule.Select(line => BatchCommand(
            "insert into business_invoicing.invoice_payment_schedule(tenant_id,legal_entity_id,invoice_id,sequence,due_date,amount,currency_code,term_id,term_version_id,evidence) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)",
            tenant.Value, legalEntityId, invoiceId, line.Sequence, line.DueDate, ParseRequired(line.Amount), line.CurrencyCode, Db(data.PaymentTermId), Db(data.PaymentTermVersionId),
            data.PaymentTermEvidenceJson is null ? DBNull.Value : new NpgsqlParameter { Value = data.PaymentTermEvidenceJson, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb })), ct);
        if (documentType is "CreditNote" or "DebitNote")
        {
            await using var relationship = new NpgsqlCommand("update business_invoicing.invoice_relationship set amount=$4 where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and relationship_type=$5", context.Connection, context.Transaction);
            Add(relationship, tenant.Value, legalEntityId, invoiceId, ParseRequired(data.Totals.GrossAmount), documentType == "CreditNote" ? "Credits" : "Debits");
            if (await relationship.ExecuteNonQueryAsync(ct) != 1)
                throw new InvoiceException("invoicing.correction_link_missing", "A correction cannot be finalized without its original-document relationship.");
        }
        await using (var update = new NpgsqlCommand("update business_invoicing.invoice set lifecycle_state='Finalized',document_number=$4,net_total=$5,taxable_total=$6,discount_total=$7,charge_total=$8,tax_total=$9,gross_total=$10,rounding_adjustment=$11,finalization_key=$12,finalized_at=$13,finalized_by=$14,journal_entry_id=$15,row_version=row_version+1,updated_at=$13 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and lifecycle_state='Draft' and row_version=$16", context.Connection, context.Transaction))
        {
            Add(update, tenant.Value, legalEntityId, invoiceId, documentNumber, ParseRequired(data.Totals.NetAmount), ParseRequired(data.Totals.TaxableAmount), ParseRequired(data.Totals.DiscountAmount), ParseRequired(data.Totals.ChargeAmount), ParseRequired(data.Totals.TaxAmount), ParseRequired(data.Totals.GrossAmount), ParseRequired(data.Totals.RoundingAdjustment), request.IdempotencyKey, now, actorId, journalEntryId, request.Version);
            if (await update.ExecuteNonQueryAsync(ct) != 1) throw new InvoiceException("invoicing.draft_changed", "The invoice changed before finalization completed.");
        }
        await using (var number = new NpgsqlCommand("insert into business_invoicing.invoice_number_reference(tenant_id,legal_entity_id,invoice_id,sequence_key,period_key,document_number,allocated_at) values($1,$2,$3,$4,$5,$6,$7)", context.Connection, context.Transaction))
        { Add(number, tenant.Value, legalEntityId, invoiceId, sequenceKey, periodKey, documentNumber, now); await number.ExecuteNonQueryAsync(ct); }
        await using (var record = new NpgsqlCommand("insert into business_invoicing.invoice_finalization_record(tenant_id,legal_entity_id,invoice_id,idempotency_key,payload_hash,document_number,journal_entry_id,open_item_ids,actor_id,finalized_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", context.Connection, context.Transaction))
        { Add(record, tenant.Value, legalEntityId, invoiceId, request.IdempotencyKey, payloadHash, documentNumber, journalEntryId, openItemIds.ToArray(), actorId, now); await record.ExecuteNonQueryAsync(ct); }
        var nextDue = data.Schedule.OrderBy(item => item.DueDate).Select(item => (DateOnly?)item.DueDate).FirstOrDefault();
        await UpsertProjectionAsync(context.Connection, context.Transaction, tenant, legalEntityId, invoiceId, data.PartyDisplayName, nextDue, openItemIds.Count == 0 ? "Settled" : "Open", data.RegulatorySummary, ct);
        string? sourceModule = null; Guid? sourceDocumentId = null; string? sourcePurpose = null;
        await using (var source = new NpgsqlCommand("select source_module,source_document_id,source_purpose from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
        {
            Add(source, tenant.Value, legalEntityId, invoiceId);
            await using var sourceReader = await source.ExecuteReaderAsync(ct);
            if (await sourceReader.ReadAsync(ct))
            {
                sourceModule = sourceReader.IsDBNull(0) ? null : sourceReader.GetString(0);
                sourceDocumentId = sourceReader.IsDBNull(1) ? null : sourceReader.GetGuid(1);
                sourcePurpose = sourceReader.IsDBNull(2) ? null : sourceReader.GetString(2);
            }
        }
        var sourcePayload = new { invoiceId, legalEntityId, direction, documentType, documentNumber, journalEntryId, openItemIds, sourceModule, sourceDocumentId, sourcePurpose, amount = ParseRequired(data.Totals.GrossAmount), currencyCode = data.Totals.CurrencyCode };
        context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceFinalizedV1", JsonSerializer.Serialize(sourcePayload, JsonOptions), tenant, now, correlationId, invoiceId, "invoicing"));
        if (documentType is "CreditNote" or "DebitNote")
        {
            var relationshipType = documentType == "CreditNote" ? "Credits" : "Debits";
            Guid? originalInvoiceId;
            await using (var relationship = new NpgsqlCommand("select related_invoice_id from business_invoicing.invoice_relationship where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 and relationship_type=$4", context.Connection, context.Transaction))
            {
                Add(relationship, tenant.Value, legalEntityId, invoiceId, relationshipType);
                originalInvoiceId = await relationship.ExecuteScalarAsync(ct) is Guid relatedId ? relatedId : null;
            }
            if (originalInvoiceId is null) throw new InvoiceException("invoicing.correction_link_missing", "The original document relationship is unavailable.");
            context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceCorrectedV1", JsonSerializer.Serialize(new
            { invoiceId, originalInvoiceId, legalEntityId, direction, documentType, documentNumber, amount = ParseRequired(data.Totals.GrossAmount), currencyCode = data.Totals.CurrencyCode, sourceModule, sourceDocumentId, sourcePurpose }, JsonOptions),
                tenant, now, correlationId, invoiceId, "invoicing"));
        }
        var complianceSnapshot = await BuildComplianceSnapshotAsync(context, tenant, legalEntityId, invoiceId, direction, documentType, documentNumber, data, ct);
        var snapshotJson = new NpgsqlParameter { Value = JsonSerializer.Serialize(complianceSnapshot, JsonOptions), NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb };
        await using (var snapshot = new NpgsqlCommand("insert into business_invoicing.invoice_compliance_snapshot(tenant_id,legal_entity_id,invoice_id,schema_version,snapshot,created_at) values($1,$2,$3,$4,$5,$6)", context.Connection, context.Transaction))
        { Add(snapshot, tenant.Value, legalEntityId, invoiceId, complianceSnapshot.SchemaVersion, snapshotJson, now); await snapshot.ExecuteNonQueryAsync(ct); }
        context.AddOutbox(new(Guid.CreateVersion7(), "InvoiceComplianceRequestedV1", JsonSerializer.Serialize(new
        { invoiceId, legalEntityId, documentNumber, schemaVersion = complianceSnapshot.SchemaVersion, idempotencyKey = request.IdempotencyKey }, JsonOptions), tenant, now, correlationId, invoiceId, "invoicing"));
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null, "User", actorId, membershipId, "invoicing", "invoice.finalized", "invoice", invoiceId, null,
            JsonSerializer.Serialize(new { documentNumber, journalEntryId, openItemIds }, JsonOptions), null, "api", correlationId, null, now, null), ct);
    }

    private static async Task InsertLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid invoiceId, IReadOnlyList<InvoiceLineDraft> lines, CancellationToken ct)
    {
        await ExecuteBatchAsync(connection, transaction, lines.Select(line => BatchCommand(
            "insert into business_invoicing.invoice_line(tenant_id,legal_entity_id,invoice_id,id,sequence,product_variant_id,unit_of_measure_id,unit_code_snapshot,description,quantity,explicit_unit_price,price_override_reason,business_category_code,line_discount,source_line_reference) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)",
            tenant.Value, entity, invoiceId, line.Id, line.Sequence, Db(line.ProductVariantId), Db(line.UnitOfMeasureId), Db(Clean(line.UnitCode)), line.Description.Trim(), line.Quantity,
            Db(ParseOptional(line.ExplicitUnitPrice)), Db(Clean(line.PriceOverrideReason)), Db(Clean(line.BusinessCategoryCode)), line.DiscountAmount, Db(Clean(line.SourceLineReference)))), ct);
    }

    private static NpgsqlBatchCommand BatchCommand(string sql, params object?[] values)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in values)
            command.Parameters.Add(value is NpgsqlParameter parameter ? parameter : new NpgsqlParameter { Value = value ?? DBNull.Value });
        return command;
    }

    private static async Task<int> ExecuteBatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        IEnumerable<NpgsqlBatchCommand> commands, CancellationToken ct)
    {
        await using var batch = new NpgsqlBatch(connection, transaction);
        foreach (var command in commands) batch.BatchCommands.Add(command);
        return batch.BatchCommands.Count == 0 ? 0 : await batch.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertChargesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid invoiceId, IReadOnlyList<InvoiceChargeInput>? input, CancellationToken ct)
    {
        foreach (var charge in ParseCharges(input))
        {
            await using var command = new NpgsqlCommand("insert into business_invoicing.invoice_charge(tenant_id,legal_entity_id,invoice_id,id,category,description,amount,tax_treatment,business_category_code) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", connection, transaction);
            Add(command, tenant.Value, entity, invoiceId, charge.Id, charge.Code, charge.Description, charge.Amount, charge.TaxTreatment, Db(charge.BusinessCategoryCode));
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static InvoiceChargeDraft[] ParseCharges(IReadOnlyList<InvoiceChargeInput>? input)
    {
        if (input is null) return [];
        if (input.Count > 100) throw new InvoiceException("invoicing.charges_invalid", "An invoice may contain at most 100 typed charges.");
        var allowedCodes = new HashSet<string>(["SHIPPING", "HANDLING", "INSURANCE", "SERVICE_FEE"], StringComparer.Ordinal);
        return input.Select(charge =>
        {
            var code = Clean(charge.Code)?.ToUpperInvariant();
            var treatment = Clean(charge.TaxTreatment);
            var amount = ParseOptional(charge.Amount);
            if (code is null || !allowedCodes.Contains(code) || string.IsNullOrWhiteSpace(charge.Description) || charge.Description.Trim().Length > 500
                || amount is null || amount <= 0m || treatment is not ("Taxable" or "NonTaxable"))
                throw new InvoiceException("invoicing.charge_invalid", "Each charge needs an approved type, positive amount, description, and tax treatment.");
            return new InvoiceChargeDraft(Guid.CreateVersion7(), code, charge.Description.Trim(), amount.Value, treatment, Clean(charge.BusinessCategoryCode));
        }).ToArray();
    }

    private static async Task UpsertProjectionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid invoiceId, string partyName, DateOnly? dueDate, string settlement, string regulatory, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_invoicing.invoice_list_projection(tenant_id,legal_entity_id,invoice_id,party_display_name,settlement_summary,regulatory_summary,due_date,updated_at) values($1,$2,$3,$4,$5,$6,$7,now()) on conflict(tenant_id,invoice_id) do update set party_display_name=excluded.party_display_name,settlement_summary=excluded.settlement_summary,regulatory_summary=excluded.regulatory_summary,due_date=excluded.due_date,updated_at=excluded.updated_at", connection, transaction);
        Add(command, tenant.Value, entity, invoiceId, partyName.Trim(), settlement, regulatory, Db(dueDate));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<InvoiceComplianceSnapshotContract> BuildComplianceSnapshotAsync(ITransactionContext context, TenantId tenant,
        Guid legalEntityId, Guid invoiceId, string direction, string documentType, string documentNumber, InvoiceFinalizationData data, CancellationToken ct)
    {
        await using var header = new NpgsqlCommand("select document_date,posting_date,tax_date,supply_date,currency_code,price_mode from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
        Add(header, tenant.Value, legalEntityId, invoiceId);
        DateOnly documentDate; DateOnly postingDate; DateOnly taxDate; DateOnly? supplyDate; string currency; string priceMode;
        await using (var reader = await header.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found while building its immutable compliance snapshot.");
            documentDate = reader.GetFieldValue<DateOnly>(0); postingDate = reader.GetFieldValue<DateOnly>(1); taxDate = reader.GetFieldValue<DateOnly>(2);
            supplyDate = NDate(reader, 3); currency = reader.GetString(4).Trim(); priceMode = reader.GetString(5);
        }
        var lineFacts = new List<(Guid Id, int Sequence, Guid? Product, string Description, decimal Quantity, Guid? Unit, string? UnitCode, string? SourceLineReference)>();
        await using (var lines = new NpgsqlCommand("select id,sequence,product_variant_id,description,quantity,unit_of_measure_id,unit_code_snapshot,source_line_reference from business_invoicing.invoice_line where tenant_id=$1 and legal_entity_id=$2 and invoice_id=$3 order by sequence", context.Connection, context.Transaction))
        {
            Add(lines, tenant.Value, legalEntityId, invoiceId);
            await using var reader = await lines.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) lineFacts.Add((reader.GetGuid(0), reader.GetInt32(1), NGuid(reader, 2), reader.GetString(3), reader.GetDecimal(4), NGuid(reader, 5), NText(reader, 6), NText(reader, 7)));
        }
        var prices = data.Prices.ToDictionary(item => item.LineId);
        var amounts = data.LineAmounts.ToDictionary(item => item.LineId);
        var linesContract = lineFacts.Select(line =>
        {
            var price = prices[line.Id]; var amount = amounts[line.Id];
            return new InvoiceFrozenLineContract(line.Id, line.Sequence, line.Product, line.Description, D(line.Quantity), line.Unit, line.UnitCode,
                price.ResolvedUnitPrice, price.FinalUnitPrice, price.EvidenceJson, amount.NetAmount, amount.TaxAmount, amount.GrossAmount,
                data.DiscountAllocations.Where(item => item.LineId == line.Id).Select(item => new InvoiceFrozenDiscountContract(item.DiscountType, item.SourceAmount, item.AllocatedAmount)).ToArray(),
                data.Taxes.Where(item => item.LineId == line.Id).Select(item => new InvoiceFrozenTaxComponentContract(item.TaxCode, item.TaxTypeCode, item.Treatment, item.TaxableBase, item.TaxAmount, item.EvidenceJson)).ToArray(), line.SourceLineReference);
        }).ToArray();
        var charges = data.Charges.Select(charge => new InvoiceFrozenChargeContract(charge.Code, charge.Description, charge.Amount, charge.TaxTreatment,
            data.ChargeAllocations.Where(item => item.ChargeId == charge.Id).Select(item => new InvoiceFrozenChargeAllocationContract(item.LineId, item.Amount)).ToArray())).ToArray();
        var addressEvidence = data.AddressEvidenceJson;
        return new(2, invoiceId, legalEntityId, direction, documentType, documentNumber, documentDate, postingDate, taxDate, supplyDate,
            currency, priceMode, data.PartyEvidenceJson, addressEvidence, data.PaymentTermEvidenceJson, data.Totals.NetAmount, data.Totals.TaxableAmount,
            data.Totals.DiscountAmount, data.Totals.ChargeAmount, data.Totals.TaxAmount, data.Totals.GrossAmount, linesContract, charges,
            data.Schedule.Select(item => new InvoiceScheduleContract(item.Sequence, item.DueDate, item.Amount, item.CurrencyCode)).ToArray(),
            ToFrozenFinancialContext(data.FinancialContext));
    }

    private static InvoiceFrozenFinancialContextContract? ToFrozenFinancialContext(Shared.Contracts.FinanceBase.FinancialCalculationContextSnapshotContract? context)
    {
        if (context is null) return null;
        var exchangeRate = context.ExchangeRate is { } rate
            ? new InvoiceFrozenExchangeRateContract(rate.ApprovedRateId, rate.FromCurrency, rate.ToCurrency, rate.Rate, rate.Purpose.ToString(),
                rate.EffectiveDate, rate.Source, rate.Version, rate.ManualOverride, rate.CapturedAt)
            : null;
        return new(context.BaseCurrencyCode, context.TransactionCurrencyCode, context.Purpose.ToString(), context.BusinessDate,
            context.FinancialPolicy.Version, context.FinancialPolicy.ExchangeRateMaxAgeDays, context.FinancialPolicy.ManualRateOverrideAllowed,
            context.RoundingPolicy.Version, context.RoundingPolicy.MoneyScale, context.RoundingPolicy.CashIncrement,
            context.RoundingPolicy.LineDocumentStrategy.ToString(), context.RoundingPolicy.ContextRules.Select(rule =>
                new InvoiceFrozenRoundingRuleContract(rule.Context.ToString(), rule.Mode.ToString())).ToArray(), exchangeRate, context.CapturedAt);
    }

    private static InvoiceLineDraft[] ParseLines(IReadOnlyList<InvoiceLineInput> input)
    {
        if (input is null || input.Count is < 1 or > 1000) throw new InvoiceException("invoicing.lines_invalid", "An invoice must contain between one and 1000 lines.");
        return input.Select((line, index) => new InvoiceLineDraft(Guid.CreateVersion7(), index + 1, line.ProductVariantId, line.UnitOfMeasureId, line.UnitCode,
            line.Description, ParseRequired(line.Quantity), line.UnitPrice, ParseOptional(line.DiscountAmount) ?? 0m, line.SourceLineReference, line.PriceOverrideReason, line.BusinessCategoryCode)).ToArray();
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        scope.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await scope.ExecuteNonQueryAsync(ct);
    }
    private static async Task<string> EnsureInvoiceExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId,
        Guid invoiceId, CancellationToken ct)
    {
        var lifecycle = await ScalarAsync<string>(connection, transaction,
            "select lifecycle_state from business_invoicing.invoice where tenant_id=$1 and legal_entity_id=$2 and id=$3 for key share", ct,
            tenant.Value, legalEntityId, invoiceId);
        return lifecycle ?? throw new InvoiceException("invoicing.invoice_not_found", "Invoice was not found.");
    }
    private static async Task LockSourceIdentityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId,
        InvoiceSourceReferenceContract source, CancellationToken ct)
    {
        await using var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1 || ':' || $2::text || ':' || $3 || ':' || $4, 0))", connection, transaction);
        Add(lockCommand, tenant.Value.ToString("D"), legalEntityId, source.SourceModule, $"{source.SourceDocumentId:D}:{source.Purpose}");
        await lockCommand.ExecuteNonQueryAsync(ct);
    }
    private static async Task<T?> ScalarAsync<T>(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params object[] values) where T : class
    {
        await using var cmd = new NpgsqlCommand(sql, c, t); foreach (var value in values) cmd.Parameters.AddWithValue(value);
        return await cmd.ExecuteScalarAsync(ct) as T;
    }
    private static void Add(NpgsqlCommand command, params object?[] values)
    {
        foreach (var value in values)
        {
            if (value is NpgsqlParameter parameter) command.Parameters.Add(parameter);
            else command.Parameters.AddWithValue(value ?? DBNull.Value);
        }
    }
    private static object Db(object? value) => value ?? DBNull.Value;
    private static Guid? NGuid(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetGuid(index);
    private static string? NText(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
    private static DateOnly? NDate(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetFieldValue<DateOnly>(index);
    private static decimal? NDecimal(NpgsqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetDecimal(index);
    private static decimal ParseRequired(string amount) => decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw new InvoiceException("invoicing.amount_invalid", "A decimal amount is invalid.");
    private static decimal? ParseOptional(string? amount) => string.IsNullOrWhiteSpace(amount) ? null : ParseRequired(amount);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeSupplierNumber(string? value) => string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
