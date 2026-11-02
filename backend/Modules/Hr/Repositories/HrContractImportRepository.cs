using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrContractImportRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public Task<HrContractImportSummary> PreviewAsync(TenantId tenant, HrContractImportRequest request, CancellationToken ct) => RunAsync(tenant, Guid.Empty, Guid.Empty, Guid.Empty, request, false, ct);
    public Task<HrContractImportSummary> CommitAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrContractImportRequest request, CancellationToken ct) =>
        RunAsync(tenant, actor, membershipId, correlationId, request, true, ct);

    private async Task<HrContractImportSummary> RunAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrContractImportRequest request, bool commit, CancellationToken ct)
    {
        ValidateEnvelope(request);
        var results = new List<HrContractImportRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var issues = ValidateRow(row);
            if (row.Status == "Active" && request.Rows.Any(other => !ReferenceEquals(row, other)
                && string.Equals(row.LegacyEmployeeId?.Trim(), other.LegacyEmployeeId?.Trim(), StringComparison.Ordinal)
                && other.Status == "Active" && ContractRangesMayOverlap(row, other)))
                issues.Add("hr.contract_active_dates_overlap");
            if (issues.Count > 0) { results.Add(new(row.RowReference, "Rejected", null, issues)); continue; }
            try
            {
                var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
                {
                    var payloadHash = PayloadHash(row);
                    await using (var existing = new NpgsqlCommand("select contract_id,row_payload_sha256 from business_hr.employment_contract_import_reference where tenant_id=$1 and source_system=$2 and legacy_contract_id=$3", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(row.LegacyContractId.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token))
                            return string.Equals(reader.GetString(1), payloadHash, StringComparison.Ordinal)
                                ? new HrContractImportRowResult(row.RowReference, "AlreadyImported", reader.GetGuid(0), [])
                                : new HrContractImportRowResult(row.RowReference, "Rejected", null, ["hr.import_row_conflict"]);
                    }
                    var employment = await ResolveEmploymentAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                    if (employment.EmploymentId is null) return new(row.RowReference, "Rejected", null, ["hr.import_employee_not_found"]);
                    if (employment.LegalEntityId != row.LegalEntityId) return new(row.RowReference, "Rejected", null, ["hr.import_legal_entity_mismatch"]);
                    if (await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, commit, token) is null)
                        return new(row.RowReference, "Rejected", null, ["hr.go_live_configuration_required"]);
                    if (row.Status == "Active" && await HasActiveOverlapAsync(context.Connection, context.Transaction, tenant, employment.EmploymentId.Value, row, token))
                        return new(row.RowReference, "Rejected", null, ["hr.contract_active_dates_overlap"]);
                    if (!commit) return new(row.RowReference, "Ready", null, []);

                    var contractId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employment_contract(id,tenant_id,legal_entity_id,employment_id,contract_type,start_date,end_date,probation_start,probation_end,working_hours_basis,notice_period_terms,status,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(contractId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(row.LegalEntityId); insert.Parameters.AddWithValue(employment.EmploymentId.Value);
                        insert.Parameters.AddWithValue(row.ContractType.Trim()); insert.Parameters.AddWithValue((object?)row.StartDate ?? DBNull.Value); insert.Parameters.AddWithValue((object?)row.EndDate ?? DBNull.Value);
                        insert.Parameters.AddWithValue((object?)row.ProbationStart ?? DBNull.Value); insert.Parameters.AddWithValue((object?)row.ProbationEnd ?? DBNull.Value);
                        insert.Parameters.AddWithValue((object?)row.WorkingHoursBasis?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue((object?)row.NoticePeriodTerms?.Trim() ?? DBNull.Value);
                        insert.Parameters.AddWithValue(row.Status); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employment_contract_import_reference(tenant_id,id,source_system,legacy_contract_id,batch_reference,file_sha256,row_reference,row_payload_sha256,employment_id,contract_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyContractId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); reference.Parameters.AddWithValue(row.RowReference.Trim()); reference.Parameters.AddWithValue(payloadHash);
                        reference.Parameters.AddWithValue(employment.EmploymentId.Value); reference.Parameters.AddWithValue(contractId); reference.Parameters.AddWithValue(row.DataQuality); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor);
                        await reference.ExecuteNonQueryAsync(token);
                    }
                    var timelineDate = row.StartDate ?? row.EndDate ?? DateOnly.FromDateTime(now.UtcDateTime);
                    var eventPayload = System.Text.Json.JsonSerializer.Serialize(new { contractId, row.LegacyContractId, row.ContractType, row.StartDate, row.EndDate, row.Status, row.DataQuality, effectiveDateUnknown = row.StartDate is null, sourceSystem = request.SourceSystem.Trim() });
                    var eventId = Guid.CreateVersion7();
                    await using (var eventInsert = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'ContractHistoryImported',$5,$6,$7::jsonb,$8,$9)", context.Connection, context.Transaction))
                    {
                        eventInsert.Parameters.AddWithValue(tenant.Value); eventInsert.Parameters.AddWithValue(eventId); eventInsert.Parameters.AddWithValue(employment.EmployeeId); eventInsert.Parameters.AddWithValue(employment.EmploymentId.Value);
                        eventInsert.Parameters.AddWithValue(timelineDate); eventInsert.Parameters.AddWithValue(actor); eventInsert.Parameters.AddWithValue(eventPayload); eventInsert.Parameters.AddWithValue(correlationId); eventInsert.Parameters.AddWithValue(now);
                        await eventInsert.ExecuteNonQueryAsync(token);
                    }
                    await using (var timeline = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'ContractHistoryImported',$5,$6,$7::jsonb)", context.Connection, context.Transaction))
                    {
                        timeline.Parameters.AddWithValue(tenant.Value); timeline.Parameters.AddWithValue(eventId); timeline.Parameters.AddWithValue(employment.EmployeeId); timeline.Parameters.AddWithValue(employment.EmploymentId.Value);
                        timeline.Parameters.AddWithValue(timelineDate); timeline.Parameters.AddWithValue(now); timeline.Parameters.AddWithValue(eventPayload); await timeline.ExecuteNonQueryAsync(token);
                    }
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                        new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, null, "Human", actor, membershipId, "hr", "contract.imported", "employment_contract", contractId,
                            null, eventPayload, null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    return new HrContractImportRowResult(row.RowReference, "Imported", contractId, []);
                }, ct);
                results.Add(result);
            }
            catch (Exception exception) when (commit && exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" or "23P01" })
            {
                results.Add(new(row.RowReference, "Rejected", null, [exception is PostgresException { MessageText: "hr.contract_active_dates_overlap" } ? "hr.contract_active_dates_overlap" : "hr.import_row_invalid"]));
            }
        }
        var imported = results.Count(item => item.Status == "Imported"); var already = results.Count(item => item.Status == "AlreadyImported"); var rejected = results.Count(item => item.Status == "Rejected");
        return new(results.Count, imported, already, rejected, results);
    }

    private static void ValidateEnvelope(HrContractImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 100 || request.Rows.Any(row => row is null)) throw new HrException("hr.import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160) throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        if (request.Rows.Select(row => row.RowReference?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count
            || request.Rows.Select(row => row.LegacyContractId?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count) throw new HrException("hr.import_row_reference_invalid");
    }

    private List<string> ValidateRow(HrContractImportRow row)
    {
        var issues = new List<string>(); var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160) issues.Add("hr.import_row_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyContractId) || row.LegacyContractId.Trim().Length > 160) issues.Add("hr.import_legacy_contract_id_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || row.LegalEntityId == Guid.Empty) issues.Add("hr.import_employee_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.ContractType) || row.ContractType.Trim().Length > 60 || row.Status is not ("Active" or "Expired" or "Ended" or "Superseded")) issues.Add("hr.import_contract_terms_invalid");
        if (row.StartDate is { } start && row.EndDate is { } end && end < start) issues.Add("hr.import_contract_dates_invalid");
        if ((row.ProbationStart is null) != (row.ProbationEnd is null) || row.StartDate is null && row.ProbationStart is not null
            || row.ProbationStart is { } probationStart && (row.StartDate is { } knownStart && probationStart < knownStart || row.ProbationEnd < probationStart || row.EndDate is { } contractEnd && row.ProbationEnd > contractEnd))
            issues.Add("hr.import_contract_probation_invalid");
        if (row.StartDate is null && row.DataQuality == "Complete" || row.DataQuality is not ("Complete" or "Partial" or "UnknownHistory")) issues.Add("hr.import_data_quality_invalid");
        if (row.Status == "Active" && (row.StartDate is { } activeStart && activeStart > today || row.EndDate is { } activeEnd && activeEnd < today)
            || (row.Status is "Expired" or "Ended" or "Superseded") && (row.EndDate is not { } historicalEnd || historicalEnd > today))
            issues.Add("hr.import_contract_status_date_mismatch");
        if (row.WorkingHoursBasis?.Length > 1000 || row.NoticePeriodTerms?.Length > 1000) issues.Add("hr.import_contract_terms_invalid");
        return issues;
    }

    private static async Task<EmploymentReference> ResolveEmploymentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string sourceSystem, HrContractImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select e.id,e.employee_id,e.legal_entity_id from business_hr.employee_import_reference r join business_hr.employment e on e.tenant_id=r.tenant_id and e.id=r.employment_id where r.tenant_id=$1 and r.source_system=$2 and r.legacy_employee_id=$3", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sourceSystem); command.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)) : new(null, Guid.Empty, null);
    }

    private static async Task<bool> HasActiveOverlapAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, HrContractImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.employment_contract c where c.tenant_id=$1 and c.employment_id=$2 and c.status='Active' and ($3::date is null or c.start_date is null or ($3::date<=coalesce(c.end_date,'infinity'::date) and c.start_date<=coalesce($4::date,'infinity'::date))))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue((object?)row.StartDate ?? DBNull.Value); command.Parameters.AddWithValue((object?)row.EndDate ?? DBNull.Value);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static bool ContractRangesMayOverlap(HrContractImportRow left, HrContractImportRow right) =>
        left.StartDate is null || right.StartDate is null
        || left.StartDate <= (right.EndDate ?? DateOnly.MaxValue) && right.StartDate <= (left.EndDate ?? DateOnly.MaxValue);

    private static string PayloadHash(HrContractImportRow row)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { row.RowReference, legacyContractId = row.LegacyContractId.Trim(), legacyEmployeeId = row.LegacyEmployeeId.Trim(), row.LegalEntityId, contractType = row.ContractType.Trim(), row.StartDate, row.EndDate, row.ProbationStart, row.ProbationEnd, row.Status, row.DataQuality, workingHoursBasis = row.WorkingHoursBasis?.Trim(), noticePeriodTerms = row.NoticePeriodTerms?.Trim() });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private sealed record EmploymentReference(Guid? EmploymentId, Guid EmployeeId, Guid? LegalEntityId);
}
