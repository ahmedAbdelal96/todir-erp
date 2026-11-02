using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class AttendanceRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<AttendancePunchResult> RecordPunchAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateAttendancePunchRequest request, Guid correlationId, CancellationToken ct)
    {
        var direction = request.Direction?.Trim();
        var sourceType = request.SourceType?.Trim();
        var key = request.IdempotencyKey?.Trim();
        var sourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? null : request.SourceReference.Trim();
        if (request.OccurredAt == default || request.OccurredAt > clock.GetUtcNow().AddMinutes(5) || direction is not ("In" or "Out" or "Unknown")
            || sourceType is not ("Manual" or "Web" or "Imported" or "API" or "Device") || string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 120 || sourceReference?.Length > 200)
            throw new HrException("hr.attendance_punch_invalid");

        return await transactions.ExecuteAsync<AttendancePunchResult>(tenant, async (context, token) =>
        {
            await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':attendance:' || $2::text,0))", context.Connection, context.Transaction))
            {
                lockCommand.Parameters.AddWithValue(tenant.Value); lockCommand.Parameters.AddWithValue(employmentId); await lockCommand.ExecuteNonQueryAsync(token);
            }

            Guid employeeId;
            await using (var employee = new NpgsqlCommand("select employee_id from business_hr.employment where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status in ('Active','Suspended','NoticePeriod')", context.Connection, context.Transaction))
            {
                employee.Parameters.AddWithValue(tenant.Value); employee.Parameters.AddWithValue(employmentId); employee.Parameters.AddWithValue(request.LegalEntityId);
                employeeId = (Guid?)await employee.ExecuteScalarAsync(token) ?? throw new HrException("hr.employment_not_found");
            }

            Guid calendarId; string timezone;
            await using (var assignment = new NpgsqlCommand("select a.work_calendar_id,wc.time_zone_id from business_hr.employment_assignment a join business_hr.work_calendar wc on wc.tenant_id=a.tenant_id and wc.id=a.work_calendar_id where a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 and a.work_calendar_id is not null and a.effective_from<=($4::timestamptz at time zone wc.time_zone_id)::date and (a.effective_to is null or a.effective_to>($4::timestamptz at time zone wc.time_zone_id)::date) and wc.status='Active' order by a.effective_from desc,a.id desc limit 1", context.Connection, context.Transaction))
            {
                assignment.Parameters.AddWithValue(tenant.Value); assignment.Parameters.AddWithValue(employmentId); assignment.Parameters.AddWithValue(request.LegalEntityId); assignment.Parameters.AddWithValue(request.OccurredAt);
                await using var reader = await assignment.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.work_calendar_not_assigned");
                calendarId = reader.GetGuid(0); timezone = reader.GetString(1);
            }
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
            catch (TimeZoneNotFoundException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
            catch (InvalidTimeZoneException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
            var local = TimeZoneInfo.ConvertTime(request.OccurredAt, zone);
            var workDate = DateOnly.FromDateTime(local.DateTime);
            Guid? shiftAssignmentId = null; TimeOnly? shiftStart = null; TimeOnly? shiftEnd = null; short shiftBreak = 0; short shiftGrace = 0;
            await using (var shift = new NpgsqlCommand("with candidate_dates(work_date,is_previous) as (values($4::date,false),($4::date-1,true)) select a.id,d.work_date,s.start_local,s.end_local,s.break_minutes,s.grace_minutes from candidate_dates d join business_hr.shift_assignment a on a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 and a.effective_from<=d.work_date and (a.effective_to is null or a.effective_to>d.work_date) join business_hr.shift_definition s on s.tenant_id=a.tenant_id and s.id=a.shift_definition_id and s.status='Active' where not d.is_previous or ($5='Out' and s.is_overnight and $6::time<=s.end_local+make_interval(mins=>s.grace_minutes)) order by case when d.is_previous then 0 else 1 end,a.effective_from desc,a.id desc limit 1", context.Connection, context.Transaction))
            {
                shift.Parameters.AddWithValue(tenant.Value); shift.Parameters.AddWithValue(employmentId); shift.Parameters.AddWithValue(request.LegalEntityId); shift.Parameters.AddWithValue(workDate); shift.Parameters.AddWithValue(direction); shift.Parameters.AddWithValue(TimeOnly.FromTimeSpan(local.TimeOfDay));
                await using var reader = await shift.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    shiftAssignmentId = reader.GetGuid(0); workDate = reader.GetFieldValue<DateOnly>(1); shiftStart = reader.GetFieldValue<TimeOnly>(2); shiftEnd = reader.GetFieldValue<TimeOnly>(3); shiftBreak = reader.GetInt16(4); shiftGrace = reader.GetInt16(5);
                }
            }

            bool isWorkingDay; TimeOnly? plannedStart; TimeOnly? plannedEnd; short breakMinutes; string? holidayName;
            await using (var pattern = new NpgsqlCommand("select p.is_working_day,p.local_start,p.local_end,p.break_minutes,h.name_ar from business_hr.work_calendar_weekly_pattern p left join business_hr.work_calendar_holiday h on h.tenant_id=p.tenant_id and h.calendar_id=p.calendar_id and h.holiday_date=$3 and h.status='Active' where p.tenant_id=$1 and p.calendar_id=$2 and p.day_of_week=$4", context.Connection, context.Transaction))
            {
                pattern.Parameters.AddWithValue(tenant.Value); pattern.Parameters.AddWithValue(calendarId); pattern.Parameters.AddWithValue(workDate); pattern.Parameters.AddWithValue((short)workDate.DayOfWeek);
                await using var reader = await pattern.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.work_calendar_pattern_missing");
                isWorkingDay = reader.GetBoolean(0); plannedStart = reader.IsDBNull(1) ? null : reader.GetFieldValue<TimeOnly>(1); plannedEnd = reader.IsDBNull(2) ? null : reader.GetFieldValue<TimeOnly>(2); breakMinutes = reader.GetInt16(3); holidayName = reader.IsDBNull(4) ? null : reader.GetString(4);
                if (holidayName is not null) isWorkingDay = false;
            }
            if (shiftAssignmentId is not null) { plannedStart = shiftStart; plannedEnd = shiftEnd; breakMinutes = shiftBreak; isWorkingDay = holidayName is null; }

            var punchId = Guid.CreateVersion7();
            bool duplicateSubmission;
            await using (var insert = new NpgsqlCommand("insert into business_hr.attendance_punch(id,tenant_id,employee_id,employment_id,legal_entity_id,occurred_at,local_time_snapshot,time_zone_id,direction,source_type,source_reference,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14) on conflict do nothing returning id", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(punchId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.OccurredAt.ToUniversalTime()); insert.Parameters.AddWithValue(local.DateTime); insert.Parameters.AddWithValue(timezone); insert.Parameters.AddWithValue(direction); insert.Parameters.AddWithValue(sourceType); insert.Parameters.AddWithValue((object?)sourceReference ?? DBNull.Value); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow());
                if (await insert.ExecuteScalarAsync(token) is null)
                {
                    await using var existingPunch = new NpgsqlCommand("select id,occurred_at,direction,source_type,source_reference from business_hr.attendance_punch where tenant_id=$1 and employment_id=$2 and (idempotency_key=$3 or ($4::text is not null and source_reference=$4 and source_type=$5)) order by created_at desc limit 1", context.Connection, context.Transaction);
                    existingPunch.Parameters.AddWithValue(tenant.Value); existingPunch.Parameters.AddWithValue(employmentId); existingPunch.Parameters.AddWithValue(key); existingPunch.Parameters.AddWithValue((object?)sourceReference ?? DBNull.Value); existingPunch.Parameters.AddWithValue(sourceType);
                    await using var existingReader = await existingPunch.ExecuteReaderAsync(token);
                    if (!await existingReader.ReadAsync(token)) throw new HrException("hr.attendance_idempotency_conflict");
                    var existingId = existingReader.GetGuid(0); var existingAt = existingReader.GetFieldValue<DateTimeOffset>(1); var existingDirection = existingReader.GetString(2); var existingSource = existingReader.GetString(3); var existingRef = existingReader.IsDBNull(4) ? null : existingReader.GetString(4);
                    duplicateSubmission = existingAt == request.OccurredAt.ToUniversalTime() && existingDirection == direction && existingSource == sourceType && existingRef == sourceReference;
                    if (!duplicateSubmission) throw new HrException("hr.attendance_idempotency_conflict");
                    await existingReader.DisposeAsync();
                    var record = await FindRecordAsync(context.Connection, context.Transaction, tenant, employmentId, workDate, token);
                    if (record is null) throw new HrException("hr.attendance_record_missing");
                    return new(existingId, true, false, record);
                }
                duplicateSubmission = false;
            }

            var nearDuplicate = false;
            await using (var findDuplicate = new NpgsqlCommand("select exists(select 1 from business_hr.attendance_punch where tenant_id=$1 and employment_id=$2 and id<>$3 and direction=$4 and source_type=$5 and occurred_at between $6::timestamptz-interval '30 seconds' and $6::timestamptz+interval '30 seconds')", context.Connection, context.Transaction))
            {
                findDuplicate.Parameters.AddWithValue(tenant.Value); findDuplicate.Parameters.AddWithValue(employmentId); findDuplicate.Parameters.AddWithValue(punchId); findDuplicate.Parameters.AddWithValue(direction); findDuplicate.Parameters.AddWithValue(sourceType); findDuplicate.Parameters.AddWithValue(request.OccurredAt.ToUniversalTime());
                nearDuplicate = (bool)(await findDuplicate.ExecuteScalarAsync(token) ?? false);
            }

            var current = await FindRecordAsync(context.Connection, context.Transaction, tenant, employmentId, workDate, token, forUpdate: true);
            if (current?.LockState == "LockedForPayroll")
            {
                await AddPunchExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, punchId, "OutOfShiftPunch", "Raw punch arrived after attendance was consumed by Payroll; the consumed record was preserved.", token);
                await WritePunchAuditAsync(context.Connection, context.Transaction, tenant, actor, membershipId, request, punchId, "attendance.late_after_payroll_lock", correlationId, token);
                return new(punchId, false, true, current);
            }

            var firstIn = current?.FirstIn;
            var lastOut = current?.LastOut;
            if (direction == "In" && (firstIn is null || request.OccurredAt < firstIn)) firstIn = request.OccurredAt.ToUniversalTime();
            if (direction == "Out" && (lastOut is null || request.OccurredAt > lastOut)) lastOut = request.OccurredAt.ToUniversalTime();
            var outOfOrder = firstIn is { } first && lastOut is { } last && last < first;
            var status = !isWorkingDay ? (holidayName is null ? "RestDay" : "Holiday") : outOfOrder || direction == "Unknown" ? "Exception" : firstIn is not null && lastOut is not null ? "Present" : "Partial";
            var normalizedOut = lastOut is { } overnightOut && firstIn is { } overnightIn && overnightOut < overnightIn && shiftAssignmentId is not null ? overnightOut.AddDays(1) : lastOut;
            var workedMinutes = firstIn is { } actualIn && normalizedOut is { } actualOut && actualOut >= actualIn
                ? Math.Max(0, (int)(actualOut - actualIn).TotalMinutes - breakMinutes) : 0;
            var localIn = firstIn is { } firstInstant ? TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(firstInstant, zone).TimeOfDay) : (TimeOnly?)null;
            var localOut = lastOut is { } lastInstant ? TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(lastInstant, zone).TimeOfDay) : (TimeOnly?)null;
            var lateMinutes = plannedStart is { } start && localIn is { } inTime ? Math.Max(0, (int)(inTime - start).TotalMinutes) : 0;
            var earlyMinutes = plannedEnd is { } end && localOut is { } outTime ? Math.Max(0, (int)(end - outTime).TotalMinutes) : 0;
            var overtimeMinutes = plannedEnd is { } plannedEndTime && localOut is { } actualOutTime ? Math.Max(0, (int)(actualOutTime - plannedEndTime).TotalMinutes) : 0;
            if (shiftAssignmentId is not null) lateMinutes = Math.Max(0, lateMinutes - shiftGrace);
            var recordId = current?.Id ?? Guid.CreateVersion7();
            var version = current is null ? 1 : current.Version + 1;
            var lockState = "Open";
            await using (var save = new NpgsqlCommand("insert into business_hr.attendance_record(id,tenant_id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,normalization_version,lock_state,payroll_reference,version,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,1,'Open',null,1,$21) on conflict(tenant_id,employment_id,work_date) do update set work_calendar_id=excluded.work_calendar_id,shift_assignment_id=excluded.shift_assignment_id,time_zone_id=excluded.time_zone_id,planned_start_local=excluded.planned_start_local,planned_end_local=excluded.planned_end_local,planned_break_minutes=excluded.planned_break_minutes,grace_minutes=excluded.grace_minutes,actual_first_in=excluded.actual_first_in,actual_last_out=excluded.actual_last_out,worked_minutes=excluded.worked_minutes,late_minutes=excluded.late_minutes,early_leave_minutes=excluded.early_leave_minutes,overtime_eligible_minutes=excluded.overtime_eligible_minutes,attendance_status=excluded.attendance_status,normalization_version=business_hr.attendance_record.normalization_version+1,version=business_hr.attendance_record.version+1,updated_at=excluded.updated_at returning id,version", context.Connection, context.Transaction))
            {
                save.Parameters.AddWithValue(recordId); save.Parameters.AddWithValue(tenant.Value); save.Parameters.AddWithValue(employeeId); save.Parameters.AddWithValue(employmentId); save.Parameters.AddWithValue(request.LegalEntityId); save.Parameters.AddWithValue(workDate); save.Parameters.AddWithValue(calendarId); save.Parameters.AddWithValue((object?)shiftAssignmentId ?? DBNull.Value); save.Parameters.AddWithValue(timezone); save.Parameters.AddWithValue((object?)plannedStart ?? DBNull.Value); save.Parameters.AddWithValue((object?)plannedEnd ?? DBNull.Value); save.Parameters.AddWithValue(breakMinutes); save.Parameters.AddWithValue(shiftGrace); save.Parameters.AddWithValue((object?)firstIn ?? DBNull.Value); save.Parameters.AddWithValue((object?)lastOut ?? DBNull.Value); save.Parameters.AddWithValue(workedMinutes); save.Parameters.AddWithValue(lateMinutes); save.Parameters.AddWithValue(earlyMinutes); save.Parameters.AddWithValue(overtimeMinutes); save.Parameters.AddWithValue(status); save.Parameters.AddWithValue(clock.GetUtcNow());
                await using var saved = await save.ExecuteReaderAsync(token);
                if (!await saved.ReadAsync(token)) throw new HrException("hr.attendance_record_save_failed");
                recordId = saved.GetGuid(0); version = saved.GetInt64(1);
            }
            if (nearDuplicate) await AddPunchExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, punchId, "DuplicatePunch", "A similar punch was recorded for the same direction and source within 30 seconds.", token);
            if (direction == "In" && lastOut is null) await AddRecordExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, recordId, "MissingCheckOut", "Check-out has not been recorded yet.", token);
            if (direction == "Out" && firstIn is null) await AddRecordExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, recordId, "MissingCheckIn", "Check-in has not been recorded.", token);
            if (!isWorkingDay) await AddPunchExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, punchId, "PunchOnNonWorkingDay", "A punch was recorded on a non-working day.", token);
            if (direction == "Unknown" || outOfOrder) await AddPunchExceptionAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, request.LegalEntityId, punchId, "OutOfShiftPunch", "Punch order or direction requires review.", token);
            if (firstIn is not null && lastOut is not null && lastOut >= firstIn) await ResolveMissingPunchExceptionsAsync(context.Connection, context.Transaction, tenant, recordId, actor, token);
            await WritePunchAuditAsync(context.Connection, context.Transaction, tenant, actor, membershipId, request, punchId, "attendance.punch.recorded", correlationId, token);
            var result = current is null
                ? new AttendanceRecordView(recordId, employeeId, employmentId, request.LegalEntityId, workDate, calendarId, shiftAssignmentId, timezone, plannedStart, plannedEnd, breakMinutes, shiftGrace, firstIn, lastOut, workedMinutes, lateMinutes, earlyMinutes, overtimeMinutes, status, lockState, null, version)
                : current with { WorkCalendarId = calendarId, ShiftAssignmentId = shiftAssignmentId, TimeZoneId = timezone, PlannedStart = plannedStart, PlannedEnd = plannedEnd, PlannedBreakMinutes = breakMinutes, GraceMinutes = shiftGrace, FirstIn = firstIn, LastOut = lastOut, WorkedMinutes = workedMinutes, LateMinutes = lateMinutes, EarlyLeaveMinutes = earlyMinutes, OvertimeEligibleMinutes = overtimeMinutes, Status = status, Version = version };
            return new(punchId, duplicateSubmission, false, result);
        }, ct);
    }

    public async Task<AttendancePage> ListAsync(TenantId tenant, AttendancePageRequest request, CancellationToken ct)
    {
        if (request.From == default || request.To < request.From || request.To.DayNumber - request.From.DayNumber > 366) throw new HrException("hr.attendance_date_range_invalid");
        if (request.Status is not null && !new[] { "Present", "Partial", "Absent", "OnApprovedLeave", "RestDay", "Holiday", "Exception" }.Contains(request.Status, StringComparer.Ordinal)) throw new HrException("hr.attendance_status_filter_invalid");
        var page = Math.Clamp(request.Page, 1, 100_000); var pageSize = Math.Clamp(request.PageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tx, tenant, ct);
        const string filter = "tenant_id=$1 and legal_entity_id=$2 and work_date between $3 and $4 and ($5::uuid is null or employment_id=$5) and ($6::text is null or attendance_status=$6)";
        await using var countCommand = new NpgsqlCommand($"select count(*) from business_hr.attendance_record where {filter}", connection, tx);
        countCommand.Parameters.AddWithValue(tenant.Value); countCommand.Parameters.AddWithValue(request.LegalEntityId); countCommand.Parameters.AddWithValue(request.From); countCommand.Parameters.AddWithValue(request.To); countCommand.Parameters.AddWithValue((object?)request.EmploymentId ?? DBNull.Value); countCommand.Parameters.AddWithValue((object?)request.Status ?? DBNull.Value);
        var total = (long)(await countCommand.ExecuteScalarAsync(ct) ?? 0L);
        var rows = new List<AttendanceRecordView>(pageSize);
        await using var command = new NpgsqlCommand($"select id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,lock_state,payroll_reference,version from business_hr.attendance_record where {filter} order by work_date desc,employee_id,id limit $7 offset $8", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(request.From); command.Parameters.AddWithValue(request.To); command.Parameters.AddWithValue((object?)request.EmploymentId ?? DBNull.Value); command.Parameters.AddWithValue((object?)request.Status ?? DBNull.Value); command.Parameters.AddWithValue(pageSize); command.Parameters.AddWithValue(checked((page - 1) * pageSize));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadRecord(reader));
        await reader.CloseAsync(); await tx.CommitAsync(ct);
        return new(rows, page, pageSize, total);
    }

    public async Task<IReadOnlyList<AttendanceExceptionView>> ListExceptionsAsync(TenantId tenant, Guid legalEntityId, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (status is not null && status is not ("Open" or "Resolved")) throw new HrException("hr.attendance_exception_status_invalid");
        page = Math.Clamp(page, 1, 100_000); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,employment_id,attendance_record_id,attendance_punch_id,exception_code,status,reason,created_at from business_hr.attendance_exception where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) order by created_at desc,id limit $4 offset $5", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue((object?)status ?? DBNull.Value); command.Parameters.AddWithValue(pageSize); command.Parameters.AddWithValue(checked((page - 1) * pageSize));
        var rows = new List<AttendanceExceptionView>(pageSize);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        await reader.CloseAsync(); await tx.CommitAsync(ct);
        return rows;
    }

    public async Task<AttendanceCorrectionView> CorrectAsync(TenantId tenant, Guid actor, Guid membershipId, Guid recordId, Guid legalEntityId, AttendanceCorrectionRequest request, Guid correlationId, CancellationToken ct)
    {
        var reasonCode = request.ReasonCode?.Trim();
        var reason = request.Reason?.Trim();
        if (request.ExpectedVersion <= 0 || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 80 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new HrException("hr.attendance_correction_invalid");
        if (request.FirstIn is { } requestedIn && request.LastOut is { } requestedOut && requestedOut < requestedIn)
            throw new HrException("hr.attendance_correction_range_invalid");

        return await transactions.ExecuteAsync<AttendanceCorrectionView>(tenant, async (context, token) =>
        {
            var current = await FindRecordByIdAsync(context.Connection, context.Transaction, tenant, recordId, legalEntityId, token, forUpdate: true)
                ?? throw new HrException("hr.attendance_record_not_found");
            if (current.Version != request.ExpectedVersion) throw new HrException("hr.attendance_correction_concurrency_conflict");
            var firstIn = request.FirstIn?.ToUniversalTime();
            var lastOut = request.LastOut?.ToUniversalTime();
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(current.TimeZoneId); }
            catch (TimeZoneNotFoundException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
            catch (InvalidTimeZoneException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
            var checkoutDate = current.PlannedStart is { } scheduledStart && current.PlannedEnd is { } scheduledEnd && scheduledEnd < scheduledStart ? current.WorkDate.AddDays(1) : current.WorkDate;
            if ((firstIn is { } first && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(first, zone).DateTime) != current.WorkDate)
                || (lastOut is { } last && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(last, zone).DateTime) != checkoutDate))
                throw new HrException("hr.attendance_correction_work_date_mismatch");

            var breakMinutes = current.PlannedBreakMinutes;
            var workedMinutes = firstIn is { } start && lastOut is { } end
                ? Math.Max(0, (int)(end - start).TotalMinutes - breakMinutes) : 0;
            var localIn = firstIn is { } inInstant ? TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(inInstant, zone).TimeOfDay) : (TimeOnly?)null;
            var localOut = lastOut is { } outInstant ? TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(outInstant, zone).TimeOfDay) : (TimeOnly?)null;
            var lateMinutes = current.PlannedStart is { } plannedStart && localIn is { } actualIn ? Math.Max(0, (int)(actualIn - plannedStart).TotalMinutes - current.GraceMinutes) : 0;
            var earlyMinutes = current.PlannedEnd is { } plannedEnd && localOut is { } actualOut ? Math.Max(0, (int)(plannedEnd - actualOut).TotalMinutes) : 0;
            var overtimeMinutes = current.PlannedEnd is { } endPlan && localOut is { } finalOut ? Math.Max(0, (int)(finalOut - endPlan).TotalMinutes) : 0;
            await using (var approvedOvertime = new NpgsqlCommand("select coalesce(sum(requested_minutes),0) from business_hr.attendance_overtime_request where tenant_id=$1 and attendance_record_id=$2 and status='Approved'", context.Connection, context.Transaction))
            {
                approvedOvertime.Parameters.AddWithValue(tenant.Value);
                approvedOvertime.Parameters.AddWithValue(recordId);
                var approvedMinutes = Convert.ToInt32(await approvedOvertime.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
                if (overtimeMinutes < approvedMinutes) throw new HrException("hr.attendance_correction_below_approved_overtime");
            }
            var status = current.Status is "Holiday" or "RestDay" ? current.Status : firstIn is not null && lastOut is not null ? "Present" : "Partial";
            var before = JsonSerializer.Serialize(current);
            var updated = current with { FirstIn = firstIn, LastOut = lastOut, WorkedMinutes = workedMinutes, LateMinutes = lateMinutes, EarlyLeaveMinutes = earlyMinutes, OvertimeEligibleMinutes = overtimeMinutes, Status = status, Version = current.Version + 1 };
            Guid? payrollSnapshotId = null;
            if (current.LockState == "LockedForPayroll")
            {
                if (string.IsNullOrWhiteSpace(current.PayrollReference)) throw new HrException("hr.attendance_payroll_lock_reference_missing");
                await using var lockQuery = new NpgsqlCommand("select payroll_snapshot_id from business_hr.attendance_payroll_lock where tenant_id=$1 and legal_entity_id=$2 and payroll_reference=$3 and period_start<=$4 and period_end>=$4 order by locked_at desc,id limit 1", context.Connection, context.Transaction);
                lockQuery.Parameters.AddWithValue(tenant.Value); lockQuery.Parameters.AddWithValue(legalEntityId); lockQuery.Parameters.AddWithValue(current.PayrollReference); lockQuery.Parameters.AddWithValue(current.WorkDate);
                payrollSnapshotId = (Guid?)await lockQuery.ExecuteScalarAsync(token) ?? throw new HrException("hr.payroll_snapshot_not_found");
            }
            else
            {
                await using var update = new NpgsqlCommand("update business_hr.attendance_record set actual_first_in=$4,actual_last_out=$5,worked_minutes=$6,late_minutes=$7,early_leave_minutes=$8,overtime_eligible_minutes=$9,attendance_status=$10,version=version+1,updated_at=$11 where tenant_id=$1 and id=$2 and version=$3 and lock_state='Open' returning version", context.Connection, context.Transaction);
                update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(recordId); update.Parameters.AddWithValue(request.ExpectedVersion); update.Parameters.AddWithValue((object?)firstIn ?? DBNull.Value); update.Parameters.AddWithValue((object?)lastOut ?? DBNull.Value); update.Parameters.AddWithValue(workedMinutes); update.Parameters.AddWithValue(lateMinutes); update.Parameters.AddWithValue(earlyMinutes); update.Parameters.AddWithValue(overtimeMinutes); update.Parameters.AddWithValue(status); update.Parameters.AddWithValue(clock.GetUtcNow());
                if (await update.ExecuteScalarAsync(token) is null) throw new HrException("hr.attendance_correction_concurrency_conflict");
                if (firstIn is null) await AddRecordExceptionAsync(context.Connection, context.Transaction, tenant, current.EmployeeId, current.EmploymentId, current.LegalEntityId, recordId, "MissingCheckIn", "Check-in is missing after an authorized correction.", token);
                if (lastOut is null) await AddRecordExceptionAsync(context.Connection, context.Transaction, tenant, current.EmployeeId, current.EmploymentId, current.LegalEntityId, recordId, "MissingCheckOut", "Check-out is missing after an authorized correction.", token);
                if (firstIn is not null && lastOut is not null) await ResolveMissingPunchExceptionsAsync(context.Connection, context.Transaction, tenant, recordId, actor, token);
            }
            var correctionId = Guid.CreateVersion7();
            var createdAt = clock.GetUtcNow();
            var correctionStatus = payrollSnapshotId is null ? "Applied" : "PayrollDeltaPending";
            await using (var insert = new NpgsqlCommand("insert into business_hr.attendance_correction(id,tenant_id,attendance_record_id,employment_id,legal_entity_id,before_snapshot,after_snapshot,reason_code,reason,status,corrected_by,created_at) values($1,$2,$3,$4,$5,$6::jsonb,$7::jsonb,$8,$9,$10,$11,$12)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(correctionId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(recordId); insert.Parameters.AddWithValue(current.EmploymentId); insert.Parameters.AddWithValue(current.LegalEntityId); insert.Parameters.AddWithValue(before); insert.Parameters.AddWithValue(JsonSerializer.Serialize(updated)); insert.Parameters.AddWithValue(reasonCode); insert.Parameters.AddWithValue(reason); insert.Parameters.AddWithValue(correctionStatus); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(createdAt); await insert.ExecuteNonQueryAsync(token);
            }
            if (payrollSnapshotId is { } snapshotId)
            {
                await using var delta = new NpgsqlCommand("insert into business_hr.attendance_payroll_correction_delta(id,tenant_id,legal_entity_id,employment_id,attendance_record_id,correction_id,payroll_snapshot_id,payroll_reference,before_snapshot,proposed_snapshot,reason_code,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10::jsonb,$11,$12,$13)", context.Connection, context.Transaction);
                delta.Parameters.AddWithValue(Guid.CreateVersion7()); delta.Parameters.AddWithValue(tenant.Value); delta.Parameters.AddWithValue(legalEntityId); delta.Parameters.AddWithValue(current.EmploymentId); delta.Parameters.AddWithValue(recordId); delta.Parameters.AddWithValue(correctionId); delta.Parameters.AddWithValue(snapshotId); delta.Parameters.AddWithValue(current.PayrollReference!); delta.Parameters.AddWithValue(before); delta.Parameters.AddWithValue(JsonSerializer.Serialize(updated)); delta.Parameters.AddWithValue(reasonCode); delta.Parameters.AddWithValue(actor); delta.Parameters.AddWithValue(createdAt); await delta.ExecuteNonQueryAsync(token);
            }
            var action = payrollSnapshotId is null ? "attendance.corrected" : "attendance.payroll_delta_created";
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, current.LegalEntityId, null, "Human", actor, membershipId, "hr", action, "attendance_record", recordId, null, JsonSerializer.Serialize(new { correctionId, payrollSnapshotId, reasonCode, reason, request.ExpectedVersion, updated.Version }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), createdAt, null), token);
            return new(correctionId, recordId, correctionStatus, reasonCode, reason, actor, createdAt);
        }, ct);
    }

    public async Task<IReadOnlyList<AttendanceCorrectionView>> ListCorrectionsAsync(TenantId tenant, Guid recordId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select c.id,c.attendance_record_id,c.status,c.reason_code,c.reason,c.corrected_by,c.created_at from business_hr.attendance_correction c join business_hr.attendance_record r on r.tenant_id=c.tenant_id and r.id=c.attendance_record_id where c.tenant_id=$1 and c.attendance_record_id=$2 and c.legal_entity_id=$3 and r.legal_entity_id=$3 order by c.created_at desc,c.id desc limit 100", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(recordId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<AttendanceCorrectionView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetGuid(5), reader.GetFieldValue<DateTimeOffset>(6)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    private static async Task<AttendanceRecordView?> FindRecordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, DateOnly workDate, CancellationToken ct, bool forUpdate = false)
    {
        await using var command = new NpgsqlCommand("select id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,lock_state,payroll_reference,version from business_hr.attendance_record where tenant_id=$1 and employment_id=$2 and work_date=$3" + (forUpdate ? " for update" : string.Empty), connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(workDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRecord(reader) : null;
    }

    private static async Task<AttendanceRecordView?> FindRecordByIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid recordId, Guid legalEntityId, CancellationToken ct, bool forUpdate = false)
    {
        await using var command = new NpgsqlCommand("select id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,lock_state,payroll_reference,version from business_hr.attendance_record where tenant_id=$1 and id=$2 and legal_entity_id=$3" + (forUpdate ? " for update" : string.Empty), connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(recordId); command.Parameters.AddWithValue(legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRecord(reader) : null;
    }

    private static AttendanceRecordView ReadRecord(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<TimeOnly>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<TimeOnly>(9), reader.GetInt16(10), reader.GetInt16(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12), reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13), reader.GetInt32(14), reader.GetInt32(15), reader.GetInt32(16), reader.GetInt32(17), reader.GetString(18), reader.GetString(19), reader.IsDBNull(20) ? null : reader.GetString(20), reader.GetInt64(21));

    private static async Task AddPunchExceptionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employeeId, Guid employmentId, Guid entityId, Guid punchId, string code, string reason, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_hr.attendance_exception(id,tenant_id,employee_id,employment_id,legal_entity_id,attendance_record_id,attendance_punch_id,exception_code,status,reason,created_at) values($1,$2,$3,$4,$5,null,$6,$7,'Open',$8,now()) on conflict do nothing", connection, transaction);
        command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(punchId); command.Parameters.AddWithValue(code); command.Parameters.AddWithValue(reason); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task AddRecordExceptionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employeeId, Guid employmentId, Guid entityId, Guid recordId, string code, string reason, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_hr.attendance_exception(id,tenant_id,employee_id,employment_id,legal_entity_id,attendance_record_id,attendance_punch_id,exception_code,status,reason,created_at) values($1,$2,$3,$4,$5,$6,null,$7,'Open',$8,now()) on conflict do nothing", connection, transaction);
        command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(recordId); command.Parameters.AddWithValue(code); command.Parameters.AddWithValue(reason); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ResolveMissingPunchExceptionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid recordId, Guid actor, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("update business_hr.attendance_exception set status='Resolved',resolved_by=$3,resolved_at=now() where tenant_id=$1 and attendance_record_id=$2 and status='Open' and exception_code in ('MissingCheckIn','MissingCheckOut')", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(recordId); command.Parameters.AddWithValue(actor); await command.ExecuteNonQueryAsync(ct);
    }

    private async Task WritePunchAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid actor, Guid membershipId, CreateAttendancePunchRequest request, Guid punchId, string action, Guid correlationId, CancellationToken ct)
    {
        await NpgsqlAuditWriter.WriteAsync(connection, transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", action, "attendance_punch", punchId, null, JsonSerializer.Serialize(new { request.OccurredAt, direction = request.Direction, sourceType = request.SourceType }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), ct);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct);
    }
}
