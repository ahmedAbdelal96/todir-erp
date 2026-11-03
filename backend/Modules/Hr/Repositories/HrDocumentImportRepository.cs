using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Platform;

namespace Modules.Hr.Repositories;

public sealed class HrDocumentImportRepository(ITransactionCoordinator transactions, IPlatformFileReferenceCapability files, TimeProvider clock)
{
    public Task<HrDocumentImportSummary> PreviewAsync(TenantId tenant, HrDocumentImportRequest request, CancellationToken ct) => RunAsync(tenant, Guid.Empty, Guid.Empty, Guid.Empty, request, false, ct);
    public Task<HrDocumentImportSummary> CommitAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrDocumentImportRequest request, CancellationToken ct) =>
        RunAsync(tenant, actor, membership, correlation, request, true, ct);

    private async Task<HrDocumentImportSummary> RunAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrDocumentImportRequest request, bool commit, CancellationToken ct)
    {
        ValidateEnvelope(request);
        var results = new List<HrDocumentImportRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var issues = ValidateRow(row);
            if (issues.Count != 0) { results.Add(new(row.RowReference, "Rejected", null, issues)); continue; }
            var employment = await ResolveEmployeeAsync(tenant, request.SourceSystem.Trim(), row.LegacyEmployeeId.Trim(), row.LegalEntityId, ct);
            if (employment.EmployeeId is null || employment.EmploymentId is null) { results.Add(new(row.RowReference, "Rejected", null, ["hr.import_employee_not_found"])); continue; }
            if (!employment.HasLegalEntity) { results.Add(new(row.RowReference, "Rejected", null, ["hr.import_legal_entity_mismatch"])); continue; }
            var file = await files.VerifyReadyReferenceAsync(tenant, row.FileRef, "hr", "employee", employment.EmployeeId.Value, ct);
            if (file is null || !HrRepository.FileClassificationAllows(file.Classification, row.Sensitivity))
            {
                results.Add(new(row.RowReference, "Rejected", null, ["hr.document_file_reference_invalid"])); continue;
            }
            try
            {
                results.Add(await transactions.ExecuteAsync<HrDocumentImportRowResult>(tenant, async (context, token) =>
                {
                    var hash = Hash(row, file.Sha256);
                    await using (var existing = new NpgsqlCommand("select document_id,row_payload_sha256 from business_hr.employee_document_import_reference where tenant_id=$1 and source_system=$2 and legacy_document_id=$3", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(row.LegacyDocumentId.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token)) return string.Equals(reader.GetString(1), hash, StringComparison.Ordinal)
                            ? new(row.RowReference, "AlreadyImported", reader.GetGuid(0), []) : new(row.RowReference, "Rejected", null, ["hr.import_row_conflict"]);
                    }
                    var goLiveConfiguration = await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, commit, token);
                    if (goLiveConfiguration is null) return new(row.RowReference, "Rejected", null, ["hr.go_live_configuration_required"]);
                    if (goLiveConfiguration.DocumentMigrationStrategy == "None") return new(row.RowReference, "Rejected", null, ["hr.document_migration_disabled"]);
                    if (!commit) return new(row.RowReference, "Ready", null, []);
                    var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employee_document(id,tenant_id,employee_id,employment_id,legal_entity_id,scope_status,category,sensitivity,file_ref,sha256,content_type,size_bytes,expiry_date,verification_status,status,supersedes_document_id,created_by,created_at,version) values($1,$2,$3,$4,$5,'Resolved',$6,$7,$8,$9,$10,$11,$12,'Pending','Active',null,$13,$14,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employment.EmployeeId.Value); insert.Parameters.AddWithValue(employment.EmploymentId.Value); insert.Parameters.AddWithValue(row.LegalEntityId); insert.Parameters.AddWithValue(row.Category.Trim()); insert.Parameters.AddWithValue(row.Sensitivity.Trim());
                        insert.Parameters.AddWithValue(file.FileRef); insert.Parameters.AddWithValue(file.Sha256.ToLowerInvariant()); insert.Parameters.AddWithValue(file.ContentType); insert.Parameters.AddWithValue(file.SizeBytes);
                        insert.Parameters.AddWithValue((object?)row.ExpiryDate ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now);
                        if (await insert.ExecuteNonQueryAsync(token) != 1) return new(row.RowReference, "Rejected", null, ["hr.employee_not_found_or_outside_entity"]);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employee_document_import_reference(tenant_id,id,source_system,legacy_document_id,batch_reference,file_sha256,row_reference,row_payload_sha256,employee_id,document_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyDocumentId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); reference.Parameters.AddWithValue(row.RowReference.Trim()); reference.Parameters.AddWithValue(hash);
                        reference.Parameters.AddWithValue(employment.EmployeeId.Value); reference.Parameters.AddWithValue(id); reference.Parameters.AddWithValue(row.DataQuality); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor); await reference.ExecuteNonQueryAsync(token);
                    }
                    var payload = JsonSerializer.Serialize(new { id, row.Category, row.Sensitivity, fileRef = file.FileRef, file.Sha256, row.ExpiryDate, row.DataQuality });
                    var eventId = Guid.CreateVersion7();
                    await using (var eventInsert = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,'EmployeeDocumentImported',$4,$5,$6::jsonb,$7,$8)", context.Connection, context.Transaction))
                    {
                        eventInsert.Parameters.AddWithValue(tenant.Value); eventInsert.Parameters.AddWithValue(eventId); eventInsert.Parameters.AddWithValue(employment.EmployeeId.Value); eventInsert.Parameters.AddWithValue(row.ExpiryDate ?? DateOnly.FromDateTime(now.UtcDateTime));
                        eventInsert.Parameters.AddWithValue(actor); eventInsert.Parameters.AddWithValue(payload); eventInsert.Parameters.AddWithValue(correlation); eventInsert.Parameters.AddWithValue(now); await eventInsert.ExecuteNonQueryAsync(token);
                    }
                    await using (var projection = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,'EmployeeDocumentImported',$4,$5,$6::jsonb)", context.Connection, context.Transaction))
                    {
                        projection.Parameters.AddWithValue(tenant.Value); projection.Parameters.AddWithValue(eventId); projection.Parameters.AddWithValue(employment.EmployeeId.Value); projection.Parameters.AddWithValue(row.ExpiryDate ?? DateOnly.FromDateTime(now.UtcDateTime)); projection.Parameters.AddWithValue(now); projection.Parameters.AddWithValue(payload); await projection.ExecuteNonQueryAsync(token);
                    }
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, null, "Human", actor, membership, "hr", "hr.document.history.imported", "employee_document", id, null, payload, null, "hr-api", correlation, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    return new(row.RowReference, "Imported", id, []);
                }, ct));
            }
            catch (Exception exception) when (commit && exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" })
            {
                results.Add(new(row.RowReference, "Rejected", null, [exception is HrException hr ? hr.Code : "hr.import_row_invalid"]));
            }
        }
        var imported = results.Count(item => item.Status == "Imported"); var already = results.Count(item => item.Status == "AlreadyImported"); var rejected = results.Count(item => item.Status == "Rejected");
        return new(results.Count, imported, already, rejected, results);
    }

    private static void ValidateEnvelope(HrDocumentImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 100 || request.Rows.Any(row => row is null)) throw new HrException("hr.import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160) throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        if (request.Rows.Select(row => row.RowReference?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count || request.Rows.Select(row => row.LegacyDocumentId?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count) throw new HrException("hr.import_row_reference_invalid");
    }

    private static List<string> ValidateRow(HrDocumentImportRow row)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160 || string.IsNullOrWhiteSpace(row.LegacyDocumentId) || row.LegacyDocumentId.Trim().Length > 160) issues.Add("hr.import_row_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || row.LegalEntityId == Guid.Empty) issues.Add("hr.import_employee_reference_invalid");
        if (row.Category?.Trim().Length is < 1 or > 80 || row.Sensitivity is not ("Normal" or "Confidential" or "Medical" or "Restricted") || row.FileRef == Guid.Empty) issues.Add("hr.document_metadata_invalid");
        if (row.DataQuality is not ("Complete" or "Partial" or "UnknownHistory")) issues.Add("hr.import_data_quality_invalid");
        return issues;
    }

    private async Task<(Guid? EmployeeId, Guid? EmploymentId, bool HasLegalEntity)> ResolveEmployeeAsync(TenantId tenant, string source, string legacyEmployeeId, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<(Guid?, Guid?, bool)>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select e.employee_id,e.id,e.legal_entity_id=$4 from business_hr.employee_import_reference r join business_hr.employment e on e.tenant_id=r.tenant_id and e.id=r.employment_id where r.tenant_id=$1 and r.source_system=$2 and r.legacy_employee_id=$3", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(source); command.Parameters.AddWithValue(legacyEmployeeId);
            command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? (reader.GetGuid(0), reader.GetGuid(1), reader.GetBoolean(2)) : ((Guid?)null, (Guid?)null, false);
        }, ct);

    private static string Hash(HrDocumentImportRow row, string sha256) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { row.RowReference, legacyDocumentId = row.LegacyDocumentId.Trim(), legacyEmployeeId = row.LegacyEmployeeId.Trim(), row.LegalEntityId, category = row.Category.Trim(), sensitivity = row.Sensitivity.Trim(), row.FileRef, sha256 = sha256.ToLowerInvariant(), row.ExpiryDate, row.DataQuality })))).ToLowerInvariant();
}
