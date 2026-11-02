using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class AttendanceImportRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public Task<AttendanceImportPlan> PrepareAsync(TenantId tenant, HrAttendanceImportRequest request, CancellationToken ct)
    {
        var sourceSystem = NormalizeSourceSystem(request.SourceSystem);
        var batchReference = request.BatchReference?.Trim() ?? string.Empty;
        var fileSha256 = request.FileSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        ValidateRequest(request, sourceSystem, batchReference, fileSha256);

        return transactions.ExecuteAsync<AttendanceImportPlan>(tenant, async (context, token) =>
        {
            var goLive = await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(
                context.Connection, context.Transaction, tenant, request.LegalEntityId, false, token);
            var prepared = new List<PreparedAttendanceImportRow>(request.Rows.Count);
            var seenBatchSource = new HashSet<string>(StringComparer.Ordinal);
            var seenRowReferences = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rawRow in request.Rows)
            {
                var row = NormalizeRow(rawRow);
                var issues = new List<string>();
                var warnings = new List<string>();
                var rowReference = row.RowReference;
                var duplicateRowReference = !seenRowReferences.Add(rowReference);
                if (duplicateRowReference) issues.Add("hr.attendance_import_duplicate_row_reference");

                if (!ValidateRow(row, clock.GetUtcNow(), issues))
                {
                    prepared.Add(Rejected(row, null, null, issues, warnings));
                    continue;
                }

                var sourceKey = SourceKey(sourceSystem, row.SourceReference);
                if (!seenBatchSource.Add(sourceKey))
                {
                    issues.Add("hr.attendance_import_duplicate_source_in_batch");
                    prepared.Add(Rejected(row, null, null, issues, warnings));
                    continue;
                }

                var employment = await ResolveEmploymentAsync(context.Connection, context.Transaction,
                    tenant, request.LegalEntityId, row.EmploymentNumber, row.OccurredAt, token);
                if (employment is null)
                {
                    issues.Add("hr.employment_not_found_for_attendance_import");
                    prepared.Add(Rejected(row, null, null, issues, warnings));
                    continue;
                }
                if (employment.TimeZoneId is null)
                {
                    issues.Add("hr.work_calendar_not_assigned");
                    prepared.Add(Rejected(row, employment.Id, null, issues, warnings));
                    continue;
                }

                TimeZoneInfo zone;
                try { zone = TimeZoneInfo.FindSystemTimeZoneById(employment.TimeZoneId); }
                catch (TimeZoneNotFoundException) { issues.Add("hr.work_calendar_timezone_invalid"); prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings)); continue; }
                catch (InvalidTimeZoneException) { issues.Add("hr.work_calendar_timezone_invalid"); prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings)); continue; }

                var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(row.OccurredAt, zone).DateTime);
                if (workDate < employment.StartDate || employment.EndDate is { } endDate && workDate > endDate)
                {
                    issues.Add("hr.attendance_import_outside_employment_dates");
                    prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings, workDate));
                    continue;
                }

                if (goLive is null && workDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime)
                    || goLive is not null && workDate < goLive.GoLiveDate
                    && (goLive.AttendanceHistoryBoundary is null || workDate > goLive.AttendanceHistoryBoundary.Value))
                {
                    issues.Add("hr.attendance_history_boundary_not_allowed");
                    prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings, workDate));
                    continue;
                }

                var payloadHash = HashPayload(employment.Id, row);
                var imported = await ReadImportReferenceAsync(context.Connection, context.Transaction,
                    tenant, sourceSystem, row.SourceReference, batchReference, row.RowReference, token);
                if (imported is not null)
                {
                    if (imported.MultipleMatches || imported.SourceReference != row.SourceReference || imported.PayloadHash != payloadHash)
                    {
                        issues.Add("hr.attendance_import_source_conflict");
                        prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings, workDate, payloadHash));
                    }
                    else
                    {
                        prepared.Add(new(row, employment.Id, workDate, employment.TimeZoneId, "AlreadyImported", [], warnings,
                            payloadHash, PunchSourceReference(sourceKey), PunchIdempotencyKey(sourceKey)));
                    }
                    continue;
                }

                var existingPunch = await ReadExistingPunchAsync(context.Connection, context.Transaction,
                    tenant, employment.Id, PunchSourceReference(sourceKey), token);
                if (existingPunch is not null)
                {
                    if (existingPunch.OccurredAt == row.OccurredAt.ToUniversalTime() && existingPunch.Direction == row.Direction)
                        prepared.Add(new(row, employment.Id, workDate, employment.TimeZoneId, "AlreadyImported", [], warnings,
                            payloadHash, PunchSourceReference(sourceKey), PunchIdempotencyKey(sourceKey)));
                    else
                    {
                        issues.Add("hr.attendance_import_source_conflict");
                        prepared.Add(Rejected(row, employment.Id, employment.TimeZoneId, issues, warnings, workDate, payloadHash));
                    }
                    continue;
                }

                if (await HasNearDuplicateAsync(context.Connection, context.Transaction, tenant, employment.Id, row, token))
                    warnings.Add("hr.attendance_import_near_duplicate_review");

                prepared.Add(new(row, employment.Id, workDate, employment.TimeZoneId, "Ready", issues, warnings,
                    payloadHash, PunchSourceReference(sourceKey), PunchIdempotencyKey(sourceKey)));
            }

            var summary = Summarize(prepared);
            return new(sourceSystem, batchReference, fileSha256, request.LegalEntityId, summary, prepared);
        }, ct);
    }

    private static string NormalizeSourceSystem(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is < 1 or > 80 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-')))
            throw new HrException("hr.attendance_import_source_system_invalid");
        return normalized;
    }

    private static void ValidateRequest(HrAttendanceImportRequest request, string sourceSystem, string batchReference, string fileSha256)
    {
        if (request.LegalEntityId == Guid.Empty || sourceSystem.Length == 0 || batchReference.Length is < 1 or > 160
            || fileSha256.Length != 64 || fileSha256.Any(character => !Uri.IsHexDigit(character))
            || request.Rows is null || request.Rows.Count is < 1 or > 500)
            throw new HrException("hr.attendance_import_request_invalid");
    }

    private static bool ValidateRow(HrAttendanceImportRow row, DateTimeOffset now, List<string> issues)
    {
        if (row.RowReference.Length is < 1 or > 160) issues.Add("hr.attendance_import_row_reference_invalid");
        if (row.EmploymentNumber.Length is < 1 or > 80) issues.Add("hr.attendance_import_employment_number_invalid");
        if (row.SourceReference.Length is < 1 or > 160) issues.Add("hr.attendance_import_source_reference_invalid");
        if (row.OccurredAt == default || row.OccurredAt > now.AddMinutes(5)) issues.Add("hr.attendance_punch_invalid");
        if (row.Direction is not ("In" or "Out" or "Unknown")) issues.Add("hr.attendance_punch_invalid");
        return issues.Count == 0;
    }

    private static HrAttendanceImportRow NormalizeRow(HrAttendanceImportRow row) => row with
    {
        RowReference = row.RowReference?.Trim() ?? string.Empty,
        EmploymentNumber = row.EmploymentNumber?.Trim() ?? string.Empty,
        Direction = row.Direction?.Trim() ?? string.Empty,
        SourceReference = row.SourceReference?.Trim() ?? string.Empty
    };

    private static PreparedAttendanceImportRow Rejected(HrAttendanceImportRow row, Guid? employmentId, string? timeZoneId,
        IReadOnlyList<string> issues, IReadOnlyList<string> warnings, DateOnly? workDate = null, string? payloadHash = null) =>
        new(row, employmentId, workDate, timeZoneId, "Rejected", issues, warnings, payloadHash ?? string.Empty, string.Empty, string.Empty);

    private static async Task<ResolvedEmployment?> ResolveEmploymentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantId tenant, Guid legalEntityId, string employmentNumber, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select e.id,e.start_date,e.end_date,calendar.time_zone_id
            from business_hr.employment e
            left join lateral (
                select wc.time_zone_id
                from business_hr.employment_assignment a
                join business_hr.work_calendar wc on wc.tenant_id=a.tenant_id and wc.id=a.work_calendar_id and wc.status='Active'
                where a.tenant_id=e.tenant_id and a.employment_id=e.id and a.legal_entity_id=e.legal_entity_id
                  and a.work_calendar_id is not null
                  and a.effective_from<=(($4::timestamptz at time zone wc.time_zone_id)::date)
                  and (a.effective_to is null or a.effective_to>(($4::timestamptz at time zone wc.time_zone_id)::date))
                order by a.effective_from desc,a.id desc limit 1
            ) calendar on true
            where e.tenant_id=$1 and e.legal_entity_id=$2 and e.employment_number=$3
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(employmentNumber);
        command.Parameters.AddWithValue(occurredAt.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.IsDBNull(3) ? null : reader.GetString(3))
            : null;
    }

    private static async Task<ImportReference?> ReadImportReferenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantId tenant, string sourceSystem, string sourceReference, string batchReference, string rowReference, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select source_reference,payload_sha256 from business_hr.attendance_import_reference where tenant_id=$1 and source_system=$2 and (source_reference=$3 or (batch_reference=$4 and row_reference=$5))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sourceSystem); command.Parameters.AddWithValue(sourceReference);
        command.Parameters.AddWithValue(batchReference); command.Parameters.AddWithValue(rowReference);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var result = new ImportReference(reader.GetString(0), reader.GetString(1), false);
        return await reader.ReadAsync(ct) ? result with { MultipleMatches = true } : result;
    }

    private static async Task<ExistingPunch?> ReadExistingPunchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantId tenant, Guid employmentId, string sourceReference, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select occurred_at,direction from business_hr.attendance_punch where tenant_id=$1 and employment_id=$2 and source_type='Imported' and source_reference=$3", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(sourceReference);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1)) : null;
    }

    private static async Task<bool> HasNearDuplicateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantId tenant, Guid employmentId, HrAttendanceImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.attendance_punch where tenant_id=$1 and employment_id=$2 and direction=$3 and source_type='Imported' and occurred_at between $4::timestamptz-interval '30 seconds' and $4::timestamptz+interval '30 seconds')", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(row.Direction); command.Parameters.AddWithValue(row.OccurredAt.ToUniversalTime());
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static string HashPayload(Guid employmentId, HrAttendanceImportRow row)
    {
        var canonical = string.Join('\n', employmentId.ToString("D"), row.OccurredAt.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), row.Direction, row.EmploymentNumber, row.SourceReference);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string SourceKey(string sourceSystem, string sourceReference) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceSystem}\n{sourceReference}"))).ToLowerInvariant();

    public static string PunchSourceReference(string sourceKey) => $"attendance-import:{sourceKey}";
    public static string PunchIdempotencyKey(string sourceKey) => $"attendance-import:{sourceKey}";

    private static HrAttendanceImportSummary Summarize(List<PreparedAttendanceImportRow> rows) =>
        new(rows.Count, rows.Count(row => row.Status == "Ready"), 0, rows.Count(row => row.Status == "AlreadyImported"),
            rows.Count(row => row.Status == "Rejected"), rows.Select(row => row.ToView()).ToArray());

    private sealed record ResolvedEmployment(Guid Id, DateOnly StartDate, DateOnly? EndDate, string? TimeZoneId);
    private sealed record ImportReference(string SourceReference, string PayloadHash, bool MultipleMatches);
    private sealed record ExistingPunch(DateTimeOffset OccurredAt, string Direction);
}

public sealed record AttendanceImportPlan(string SourceSystem, string BatchReference, string FileSha256, Guid LegalEntityId,
    HrAttendanceImportSummary Summary, IReadOnlyList<PreparedAttendanceImportRow> Rows);

public sealed record AttendanceImportEvidence(string SourceSystem, string SourceReference, string BatchReference,
    string RowReference, string FileSha256, string PayloadSha256);

public sealed record PreparedAttendanceImportRow(HrAttendanceImportRow Row, Guid? EmploymentId, DateOnly? WorkDate,
    string? TimeZoneId, string Status, IReadOnlyList<string> Issues, IReadOnlyList<string> Warnings,
    string PayloadHash, string PunchSourceReference, string IdempotencyKey)
{
    public HrAttendanceImportRowResult ToView() => new(Row.RowReference, Status, Row.EmploymentNumber, WorkDate, TimeZoneId, Issues, Warnings);
}
