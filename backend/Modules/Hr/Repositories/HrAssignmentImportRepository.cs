using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrAssignmentImportRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public Task<HrAssignmentImportSummary> PreviewAsync(TenantId tenant, HrAssignmentImportRequest request, CancellationToken ct) => RunAsync(tenant, Guid.Empty, Guid.Empty, Guid.Empty, request, false, ct);

    public Task<HrAssignmentImportSummary> CommitAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrAssignmentImportRequest request, CancellationToken ct) =>
        RunAsync(tenant, actor, membershipId, correlationId, request, true, ct);

    private async Task<HrAssignmentImportSummary> RunAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId, HrAssignmentImportRequest request, bool commit, CancellationToken ct)
    {
        ValidateEnvelope(request);
        var results = new List<HrAssignmentImportRowResult>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var issues = ValidateRow(row);
            if (issues.Count > 0)
            {
                results.Add(new(row.RowReference, "Rejected", null, issues));
                continue;
            }
            try
            {
                var result = await transactions.ExecuteAsync(tenant, async (context, token) =>
                {
                    var payloadHash = PayloadHash(row);
                    await using (var existing = new NpgsqlCommand("select r.assignment_id,r.row_payload_sha256 from business_hr.employment_assignment_import_reference r where r.tenant_id=$1 and r.source_system=$2 and r.legacy_assignment_id=$3", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(row.LegacyAssignmentId.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token))
                            return string.Equals(reader.GetString(1), payloadHash, StringComparison.Ordinal)
                                ? new HrAssignmentImportRowResult(row.RowReference, "AlreadyImported", reader.GetGuid(0), [])
                                : new HrAssignmentImportRowResult(row.RowReference, "Rejected", null, ["hr.import_row_conflict"]);
                    }

                    var references = await ResolveReferencesAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                    if (references.EmploymentId is null) return new(row.RowReference, "Rejected", null, ["hr.import_employee_not_found"]);
                    if (references.LegalEntityId != row.LegalEntityId) return new(row.RowReference, "Rejected", null, ["hr.import_legal_entity_mismatch"]);
                    if (await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, commit, token) is null)
                        return new(row.RowReference, "Rejected", null, ["hr.go_live_configuration_required"]);
                    if (references.EmploymentStart is { } employmentStart && employmentStart > row.EffectiveFrom
                        || references.EmploymentEnd is { } employmentEnd && employmentEnd < row.EffectiveFrom)
                        return new(row.RowReference, "Rejected", null, ["hr.import_assignment_outside_employment"]);
                    var missing = MissingReferences(row, references);
                    if (missing.Count > 0) return new(row.RowReference, "Rejected", null, missing);
                    var employmentBoundary = references.EmploymentEnd?.AddDays(1);
                    if (employmentBoundary is { } boundary && row.EffectiveTo is { } requestedTo && requestedTo > boundary)
                        return new(row.RowReference, "Rejected", null, ["hr.import_assignment_outside_employment"]);
                    var effectiveTo = row.EffectiveTo ?? employmentBoundary;
                    await using (var successor = new NpgsqlCommand("select effective_from from business_hr.employment_assignment where tenant_id=$1 and employment_id=$2 and effective_from>$3 order by effective_from,id limit 1", context.Connection, context.Transaction))
                    {
                        successor.Parameters.AddWithValue(tenant.Value); successor.Parameters.AddWithValue(references.EmploymentId.Value); successor.Parameters.AddWithValue(row.EffectiveFrom);
                        if (await successor.ExecuteScalarAsync(token) is DateOnly next)
                        {
                            if (effectiveTo is { } requestedEnd && requestedEnd > next) return new(row.RowReference, "Rejected", null, ["hr.assignment_effective_dates_overlap"]);
                            effectiveTo ??= next;
                        }
                    }
                    if (effectiveTo <= row.EffectiveFrom) return new(row.RowReference, "Rejected", null, ["hr.import_assignment_invalid"]);
                    if (!commit) return new(row.RowReference, "Ready", null, []);

                    await using (var predecessor = new NpgsqlCommand("select id,version from business_hr.employment_assignment where tenant_id=$1 and employment_id=$2 and effective_from<$3 and (effective_to is null or effective_to>$3) order by effective_from desc,id desc limit 1 for update", context.Connection, context.Transaction))
                    {
                        predecessor.Parameters.AddWithValue(tenant.Value); predecessor.Parameters.AddWithValue(references.EmploymentId.Value); predecessor.Parameters.AddWithValue(row.EffectiveFrom);
                        await using var reader = await predecessor.ExecuteReaderAsync(token);
                        if (await reader.ReadAsync(token))
                        {
                            var priorId = reader.GetGuid(0); var priorVersion = reader.GetInt64(1); await reader.DisposeAsync();
                            await using var close = new NpgsqlCommand("update business_hr.employment_assignment set effective_to=$1,version=version+1 where tenant_id=$2 and id=$3 and version=$4", context.Connection, context.Transaction);
                            close.Parameters.AddWithValue(row.EffectiveFrom); close.Parameters.AddWithValue(tenant.Value); close.Parameters.AddWithValue(priorId); close.Parameters.AddWithValue(priorVersion);
                            if (await close.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.assignment_concurrency_conflict");
                        }
                    }

                    var assignmentId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employment_assignment(tenant_id,id,legal_entity_id,employment_id,branch_id,department_id,position_id,manager_employment_id,work_calendar_id,effective_from,effective_to,reason_code,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(assignmentId); insert.Parameters.AddWithValue(row.LegalEntityId); insert.Parameters.AddWithValue(references.EmploymentId.Value);
                        insert.Parameters.AddWithValue((object?)references.BranchId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)references.DepartmentId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)references.PositionId ?? DBNull.Value);
                        insert.Parameters.AddWithValue((object?)references.ManagerEmploymentId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)references.WorkCalendarId ?? DBNull.Value); insert.Parameters.AddWithValue(row.EffectiveFrom);
                        insert.Parameters.AddWithValue((object?)effectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(row.ReasonCode.Trim()); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now);
                        await insert.ExecuteNonQueryAsync(token);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employment_assignment_import_reference(tenant_id,id,source_system,legacy_assignment_id,batch_reference,file_sha256,row_reference,row_payload_sha256,assignment_id,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyAssignmentId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); reference.Parameters.AddWithValue(row.RowReference.Trim()); reference.Parameters.AddWithValue(payloadHash);
                        reference.Parameters.AddWithValue(assignmentId); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor); await reference.ExecuteNonQueryAsync(token);
                    }
                    await AppendEventAsync(context.Connection, context.Transaction, tenant, references.EmployeeId, references.EmploymentId.Value, actor, correlationId, request.SourceSystem.Trim(), row, assignmentId, now, token);
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                        new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, references.BranchId, "Human", actor, membershipId, "hr", "assignment.imported", "employment_assignment", assignmentId,
                            null, System.Text.Json.JsonSerializer.Serialize(new { row.LegacyAssignmentId, row.LegacyEmployeeId, row.EffectiveFrom, effectiveTo }), row.ReasonCode, "hr-api", correlationId,
                            System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    return new HrAssignmentImportRowResult(row.RowReference, "Imported", assignmentId, []);
                }, ct);
                results.Add(result);
            }
            catch (Exception exception) when (commit && exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" or "23P01" })
            {
                results.Add(new(row.RowReference, "Rejected", null, [exception is PostgresException { MessageText: "hr.manager_hierarchy_cycle" } ? "hr.import_manager_cycle" : "hr.import_row_invalid"]));
            }
        }
        var imported = results.Count(row => row.Status == "Imported"); var already = results.Count(row => row.Status == "AlreadyImported"); var rejected = results.Count(row => row.Status == "Rejected");
        return new(results.Count, imported, already, rejected, results);
    }

    private static void ValidateEnvelope(HrAssignmentImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 100 || request.Rows.Any(row => row is null)) throw new HrException("hr.import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160)
            throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        if (request.Rows.Select(row => row.RowReference?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count
            || request.Rows.Select(row => row.LegacyAssignmentId?.Trim()).Distinct(StringComparer.Ordinal).Count() != request.Rows.Count)
            throw new HrException("hr.import_row_reference_invalid");
    }

    private static List<string> ValidateRow(HrAssignmentImportRow row)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160) issues.Add("hr.import_row_reference_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyAssignmentId) || row.LegacyAssignmentId.Trim().Length > 160) issues.Add("hr.import_legacy_assignment_id_invalid");
        if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || row.LegalEntityId == Guid.Empty || row.EffectiveFrom == default || row.EffectiveFrom == DateOnly.MaxValue
            || row.EffectiveTo is { } end && (end <= row.EffectiveFrom || end == DateOnly.MaxValue)) issues.Add("hr.import_assignment_invalid");
        if (string.IsNullOrWhiteSpace(row.ReasonCode) || row.ReasonCode.Trim().Length > 80) issues.Add("hr.import_assignment_reason_invalid");
        if (row.BranchCode?.Trim().Length > 40 || row.DepartmentCode?.Trim().Length > 40 || row.PositionCode?.Trim().Length > 40
            || row.ManagerLegacyEmployeeId?.Trim().Length > 160 || row.WorkCalendarCode?.Trim().Length > 40) issues.Add("hr.import_assignment_reference_invalid");
        return issues;
    }

    private static async Task<AssignmentReferences> ResolveReferencesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string sourceSystem, HrAssignmentImportRow row, CancellationToken ct)
    {
        const string sql = "select e.id,e.employee_id,e.legal_entity_id,e.start_date,e.end_date,(select id from platform.branch where tenant_id=$1 and legal_entity_id=$3 and code=$5 limit 1),(select id from business_hr.department where tenant_id=$1 and legal_entity_id=$3 and code=$6 limit 1),(select id from business_hr.position where tenant_id=$1 and legal_entity_id=$3 and code=$7 limit 1),(select m.id from business_hr.employee_import_reference r join business_hr.employment m on m.tenant_id=r.tenant_id and m.id=r.employment_id where r.tenant_id=$1 and r.source_system=$4 and r.legacy_employee_id=$8 and m.legal_entity_id=$3 and m.start_date<=$9 and (m.end_date is null or m.end_date>=$9) limit 1),(select id from business_hr.work_calendar where tenant_id=$1 and legal_entity_id=$3 and code=$10 limit 1) from business_hr.employee_import_reference r join business_hr.employment e on e.tenant_id=r.tenant_id and e.id=r.employment_id where r.tenant_id=$1 and r.source_system=$4 and r.legacy_employee_id=$2 and e.legal_entity_id=$3";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(row.LegacyEmployeeId.Trim()); command.Parameters.AddWithValue(row.LegalEntityId); command.Parameters.AddWithValue(sourceSystem);
        command.Parameters.AddWithValue((object?)row.BranchCode?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue((object?)row.DepartmentCode?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)row.PositionCode?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue((object?)row.ManagerLegacyEmployeeId?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue(row.EffectiveFrom); command.Parameters.AddWithValue((object?)row.WorkCalendarCode?.Trim() ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new(null, Guid.Empty, null, null, null, null, null, null, null, null);
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetGuid(9));
    }

    private static List<string> MissingReferences(HrAssignmentImportRow row, AssignmentReferences refs)
    {
        var issues = new List<string>();
        if (row.BranchCode is not null && refs.BranchId is null) issues.Add("hr.import_branch_not_found");
        if (row.DepartmentCode is not null && refs.DepartmentId is null) issues.Add("hr.import_department_not_found");
        if (row.PositionCode is not null && refs.PositionId is null) issues.Add("hr.import_position_not_found");
        if (row.ManagerLegacyEmployeeId is not null && refs.ManagerEmploymentId is null) issues.Add("hr.import_manager_not_found");
        if (row.WorkCalendarCode is not null && refs.WorkCalendarId is null) issues.Add("hr.import_calendar_not_found");
        return issues;
    }

    private static async Task AppendEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employeeId, Guid employmentId, Guid actor, Guid correlationId, string sourceSystem, HrAssignmentImportRow row, Guid assignmentId, DateTimeOffset now, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { assignmentId, row.LegacyAssignmentId, row.EffectiveFrom, row.EffectiveTo, sourceSystem });
        await using (var command = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'AssignmentChanged',$5,$6,$7::jsonb,$8,$9)", connection, transaction))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId);
            command.Parameters.AddWithValue(row.EffectiveFrom); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(payload); command.Parameters.AddWithValue(correlationId); command.Parameters.AddWithValue(now);
            await command.ExecuteNonQueryAsync(ct);
        }
        await using var timeline = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'AssignmentChanged',$5,$6,$7::jsonb)", connection, transaction);
        timeline.Parameters.AddWithValue(tenant.Value); timeline.Parameters.AddWithValue(id); timeline.Parameters.AddWithValue(employeeId); timeline.Parameters.AddWithValue(employmentId);
        timeline.Parameters.AddWithValue(row.EffectiveFrom); timeline.Parameters.AddWithValue(now); timeline.Parameters.AddWithValue(payload); await timeline.ExecuteNonQueryAsync(ct);
    }

    private static string PayloadHash(HrAssignmentImportRow row)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { row.RowReference, row.LegacyAssignmentId, row.LegacyEmployeeId, row.LegalEntityId, row.EffectiveFrom, row.EffectiveTo, row.BranchCode, row.DepartmentCode, row.PositionCode, row.ManagerLegacyEmployeeId, row.ReasonCode, row.WorkCalendarCode });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private sealed record AssignmentReferences(Guid? EmploymentId, Guid EmployeeId, Guid? LegalEntityId, DateOnly? EmploymentStart, DateOnly? EmploymentEnd, Guid? BranchId, Guid? DepartmentId, Guid? PositionId, Guid? ManagerEmploymentId, Guid? WorkCalendarId);
}
