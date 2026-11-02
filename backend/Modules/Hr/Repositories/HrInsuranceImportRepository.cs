using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Services;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrInsuranceImportRepository(ITransactionCoordinator transactions, HrIdentifierCrypto crypto, TimeProvider clock)
{
    public Task<HrInsuranceImportSummary> PreviewAsync(TenantId tenant, HrInsuranceImportRequest request, CancellationToken ct) => RunAsync(tenant, Guid.Empty, Guid.Empty, Guid.Empty, request, false, ct);
    public Task<HrInsuranceImportSummary> CommitAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrInsuranceImportRequest request, CancellationToken ct) =>
        RunAsync(tenant, actor, membership, correlation, request, true, ct);

    private async Task<HrInsuranceImportSummary> RunAsync(TenantId tenant, Guid actor, Guid membership, Guid correlation, HrInsuranceImportRequest request, bool commit, CancellationToken ct)
    {
        ValidateEnvelope(request);
        var results = new List<HrInsuranceImportRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var issues = ValidateRow(row);
            if (row.Status == "Active" && request.Rows.Any(other => !ReferenceEquals(row, other)
                && string.Equals(row.LegacyEmployeeId?.Trim(), other.LegacyEmployeeId?.Trim(), StringComparison.Ordinal)
                && string.Equals(row.SchemeCode?.Trim(), other.SchemeCode?.Trim(), StringComparison.Ordinal)
                && other.Status == "Active" && RangesOverlap(row.EffectiveFrom, row.EffectiveTo, other.EffectiveFrom, other.EffectiveTo)))
                issues.Add("hr.insurance_dates_overlap");
            if (issues.Count != 0) { results.Add(new(row.RowReference, "Rejected", null, issues)); continue; }
            try
            {
                results.Add(await transactions.ExecuteAsync<HrInsuranceImportRowResult>(tenant, async (context, token) =>
                {
                    var payloadHash = Hash(row);
                    await using (var existing = new NpgsqlCommand("select r.enrollment_id,r.row_payload_sha256,i.encrypted_member_number from business_hr.employee_insurance_import_reference r join business_hr.employee_insurance_enrollment i on i.tenant_id=r.tenant_id and i.id=r.enrollment_id where r.tenant_id=$1 and r.source_system=$2 and r.legacy_enrollment_id=$3", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(row.LegacyEnrollmentId.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token))
                        {
                            var sameSensitiveValue = row.MemberNumber is null ? reader.IsDBNull(2) : !reader.IsDBNull(2) && string.Equals(crypto.UnprotectInsuranceNumber(reader.GetString(2)), HrIdentifierCrypto.Normalize(row.MemberNumber), StringComparison.Ordinal);
                            return string.Equals(reader.GetString(1), payloadHash, StringComparison.Ordinal) && sameSensitiveValue
                                ? new(row.RowReference, "AlreadyImported", reader.GetGuid(0), []) : new(row.RowReference, "Rejected", null, ["hr.import_row_conflict"]);
                        }
                    }
                    var employment = await ResolveEmploymentAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                    if (employment.Id is null) return new(row.RowReference, "Rejected", null, ["hr.import_employee_not_found"]);
                    if (employment.LegalEntityId != row.LegalEntityId) return new(row.RowReference, "Rejected", null, ["hr.import_legal_entity_mismatch"]);
                    if (await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, commit, token) is null)
                        return new(row.RowReference, "Rejected", null, ["hr.go_live_configuration_required"]);
                    if (row.Status == "Active" && await HasOverlapAsync(context.Connection, context.Transaction, tenant, employment.Id.Value, row, token))
                        return new(row.RowReference, "Rejected", null, ["hr.insurance_dates_overlap"]);
                    if (!commit) return new(row.RowReference, "Ready", null, []);
                    var now = clock.GetUtcNow(); var id = Guid.CreateVersion7();
                    var encrypted = string.IsNullOrWhiteSpace(row.MemberNumber) ? null : crypto.ProtectInsuranceNumber(HrIdentifierCrypto.Normalize(row.MemberNumber));
                    var masked = string.IsNullOrWhiteSpace(row.MemberNumber) ? null : HrIdentifierCrypto.Mask(row.MemberNumber.Trim());
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employee_insurance_enrollment(id,tenant_id,employment_id,legal_entity_id,scheme_code,provider_code,encrypted_member_number,masked_member_number,effective_from,effective_to,status,country_provider_version,evidence_file_ref,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,null,$13,$14,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employment.Id.Value); insert.Parameters.AddWithValue(row.LegalEntityId);
                        insert.Parameters.AddWithValue(row.SchemeCode.Trim()); insert.Parameters.AddWithValue((object?)row.ProviderCode?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue((object?)encrypted ?? DBNull.Value); insert.Parameters.AddWithValue((object?)masked ?? DBNull.Value);
                        insert.Parameters.AddWithValue(row.EffectiveFrom); insert.Parameters.AddWithValue((object?)row.EffectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(row.Status);
                        insert.Parameters.AddWithValue((object?)row.CountryProviderVersion?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employee_insurance_import_reference(tenant_id,id,source_system,legacy_enrollment_id,batch_reference,file_sha256,row_reference,row_payload_sha256,employment_id,enrollment_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyEnrollmentId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); reference.Parameters.AddWithValue(row.RowReference.Trim()); reference.Parameters.AddWithValue(payloadHash);
                        reference.Parameters.AddWithValue(employment.Id.Value); reference.Parameters.AddWithValue(id); reference.Parameters.AddWithValue(row.DataQuality); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor); await reference.ExecuteNonQueryAsync(token);
                    }
                    var payload = JsonSerializer.Serialize(new { id, row.LegacyEnrollmentId, row.SchemeCode, row.ProviderCode, maskedMemberNumber = masked, row.EffectiveFrom, row.EffectiveTo, row.Status, row.DataQuality });
                    var eventId = Guid.CreateVersion7();
                    await using (var eventInsert = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'InsuranceHistoryImported',$5,$6,$7::jsonb,$8,$9)", context.Connection, context.Transaction))
                    {
                        eventInsert.Parameters.AddWithValue(tenant.Value); eventInsert.Parameters.AddWithValue(eventId); eventInsert.Parameters.AddWithValue(employment.EmployeeId); eventInsert.Parameters.AddWithValue(employment.Id.Value);
                        eventInsert.Parameters.AddWithValue(row.EffectiveFrom); eventInsert.Parameters.AddWithValue(actor); eventInsert.Parameters.AddWithValue(payload); eventInsert.Parameters.AddWithValue(correlation); eventInsert.Parameters.AddWithValue(now); await eventInsert.ExecuteNonQueryAsync(token);
                    }
                    await using (var projection = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'InsuranceHistoryImported',$5,$6,$7::jsonb)", context.Connection, context.Transaction))
                    {
                        projection.Parameters.AddWithValue(tenant.Value); projection.Parameters.AddWithValue(eventId); projection.Parameters.AddWithValue(employment.EmployeeId); projection.Parameters.AddWithValue(employment.Id.Value);
                        projection.Parameters.AddWithValue(row.EffectiveFrom); projection.Parameters.AddWithValue(now); projection.Parameters.AddWithValue(payload); await projection.ExecuteNonQueryAsync(token);
                    }
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, null, "Human", actor, membership, "hr", "hr.insurance.history.imported", "employee_insurance_enrollment", id, null, payload, null, "hr-api", correlation, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    return new(row.RowReference, "Imported", id, []);
                }, ct));
            }
            catch (Exception exception) when (commit && exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" or "23P01" })
            {
                results.Add(new(row.RowReference, "Rejected", null, [exception is PostgresException { MessageText: "hr.insurance_dates_overlap" } ? "hr.insurance_dates_overlap" : "hr.import_row_invalid"]));
            }
        }
        var imported = results.Count(item => item.Status == "Imported"); var already = results.Count(item => item.Status == "AlreadyImported"); var rejected = results.Count(item => item.Status == "Rejected");
        return new(results.Count, imported, already, rejected, results);
    }

    private static void ValidateEnvelope(HrInsuranceImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 100 || request.Rows.Any(row => row is null)) throw new HrException("hr.import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160) throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        if (request.Rows.Select(row => row.RowReference?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count || request.Rows.Select(row => row.LegacyEnrollmentId?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count) throw new HrException("hr.import_row_reference_invalid");
    }

    private List<string> ValidateRow(HrInsuranceImportRow row)
    {
        var issues = new List<string>(); var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160 || string.IsNullOrWhiteSpace(row.LegacyEnrollmentId) || row.LegacyEnrollmentId.Trim().Length > 160) issues.Add("hr.import_row_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || row.LegalEntityId == Guid.Empty) issues.Add("hr.import_employee_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.SchemeCode) || row.SchemeCode.Trim().Length > 80 || row.ProviderCode?.Length > 80 || row.MemberNumber?.Length > 160 || row.CountryProviderVersion?.Length > 120 || row.EffectiveFrom == default || row.EffectiveTo < row.EffectiveFrom || row.Status is not ("Draft" or "Active" or "Ended" or "Rejected")) issues.Add("hr.insurance_import_terms_invalid");
        if (row.Status == "Active" && (row.EffectiveFrom > today || row.EffectiveTo is { } end && end < today) || row.Status == "Ended" && (row.EffectiveTo is not { } ended || ended > today)) issues.Add("hr.insurance_import_status_invalid");
        if (row.DataQuality is not ("Complete" or "Partial" or "UnknownHistory")) issues.Add("hr.import_data_quality_invalid");
        return issues;
    }

    private static async Task<(Guid? Id, Guid EmployeeId, Guid? LegalEntityId)> ResolveEmploymentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string source, HrInsuranceImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select e.id,e.employee_id,e.legal_entity_id from business_hr.employee_import_reference r join business_hr.employment e on e.tenant_id=r.tenant_id and e.id=r.employment_id where r.tenant_id=$1 and r.source_system=$2 and r.legacy_employee_id=$3", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(source); command.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)) : (null, Guid.Empty, null);
    }

    private static async Task<bool> HasOverlapAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, HrInsuranceImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.employee_insurance_enrollment i where i.tenant_id=$1 and i.employment_id=$2 and i.scheme_code=$3 and i.status='Active' and daterange(i.effective_from,i.effective_to,'[]') && daterange($4::date,$5::date,'[]'))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(row.SchemeCode.Trim()); command.Parameters.AddWithValue(row.EffectiveFrom); command.Parameters.AddWithValue((object?)row.EffectiveTo ?? DBNull.Value);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static bool RangesOverlap(DateOnly leftFrom, DateOnly? leftTo, DateOnly rightFrom, DateOnly? rightTo) => leftFrom <= (rightTo ?? DateOnly.MaxValue) && rightFrom <= (leftTo ?? DateOnly.MaxValue);
    private static string Hash(HrInsuranceImportRow row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { row.RowReference, legacyEnrollmentId = row.LegacyEnrollmentId.Trim(), legacyEmployeeId = row.LegacyEmployeeId.Trim(), row.LegalEntityId, schemeCode = row.SchemeCode.Trim(), providerCode = row.ProviderCode?.Trim(), row.EffectiveFrom, row.EffectiveTo, row.Status, row.DataQuality, countryProviderVersion = row.CountryProviderVersion?.Trim() })))).ToLowerInvariant();
}
