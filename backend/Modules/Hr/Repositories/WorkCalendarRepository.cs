using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class WorkCalendarRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public async Task<WorkCalendarView> CreateAsync(TenantId tenant, Guid actor, Guid membershipId, CreateWorkCalendarRequest request, Guid correlationId, CancellationToken ct)
    {
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var nameAr = (request.NameAr ?? string.Empty).Trim();
        var timezone = (request.TimeZoneId ?? string.Empty).Trim();
        if (code.Length is < 1 or > 40 || !code.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') || nameAr.Length is < 1 or > 160 || request.NameEn?.Length > 160 || timezone.Length is < 1 or > 100 || request.WeeklyPattern is null || request.WeeklyPattern.Count != 7 || request.WeeklyPattern.Select(static day => day.DayOfWeek).Distinct().Count() != 7)
            throw new HrException("hr.work_calendar_invalid");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch (TimeZoneNotFoundException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
        catch (InvalidTimeZoneException) { throw new HrException("hr.work_calendar_timezone_invalid"); }
        foreach (var day in request.WeeklyPattern)
            if (day.DayOfWeek is < 0 or > 6 || day.BreakMinutes is < 0 or > 720
                || day.IsWorkingDay && (day.LocalStart is null || day.LocalEnd is null || day.LocalEnd <= day.LocalStart)
                || !day.IsWorkingDay && (day.LocalStart is not null || day.LocalEnd is not null || day.BreakMinutes != 0))
                throw new HrException("hr.work_calendar_weekly_pattern_invalid");

        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<WorkCalendarView>(tenant, async (context, token) =>
        {
            await using (var insert = new NpgsqlCommand("insert into business_hr.work_calendar(tenant_id,id,legal_entity_id,code,name_ar,name_en,time_zone_id,status,version,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,'Active',1,$8,$9)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(code); insert.Parameters.AddWithValue(nameAr); insert.Parameters.AddWithValue((object?)request.NameEn?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue(timezone); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow());
                await insert.ExecuteNonQueryAsync(token);
            }
            foreach (var day in request.WeeklyPattern.OrderBy(static value => value.DayOfWeek))
            {
                await using var pattern = new NpgsqlCommand("insert into business_hr.work_calendar_weekly_pattern(tenant_id,calendar_id,day_of_week,is_working_day,local_start,local_end,break_minutes) values($1,$2,$3,$4,$5,$6,$7)", context.Connection, context.Transaction);
                pattern.Parameters.AddWithValue(tenant.Value); pattern.Parameters.AddWithValue(id); pattern.Parameters.AddWithValue((short)day.DayOfWeek); pattern.Parameters.AddWithValue(day.IsWorkingDay); pattern.Parameters.AddWithValue((object?)day.LocalStart ?? DBNull.Value); pattern.Parameters.AddWithValue((object?)day.LocalEnd ?? DBNull.Value); pattern.Parameters.AddWithValue((short)day.BreakMinutes);
                await pattern.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.work_calendar.created", "work_calendar", id, null, JsonSerializer.Serialize(new { code, nameAr, request.NameEn, timezone, dayCount = request.WeeklyPattern.Count }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, request.LegalEntityId, code, nameAr, request.NameEn?.Trim(), timezone, "Active", 1, request.WeeklyPattern.OrderBy(static value => value.DayOfWeek).ToArray());
        }, ct);
    }

    public async Task<IReadOnlyList<WorkCalendarView>> ListAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<IReadOnlyList<WorkCalendarView>>(tenant, async (context, token) =>
        {
            var calendars = new Dictionary<Guid, CalendarRow>();
            await using var command = new NpgsqlCommand("with selected as (select * from business_hr.work_calendar where tenant_id=$1 and legal_entity_id=$2 order by code,id limit 200) select c.id,c.legal_entity_id,c.code,c.name_ar,c.name_en,c.time_zone_id,c.status,c.version,p.day_of_week,p.is_working_day,p.local_start,p.local_end,p.break_minutes from selected c left join business_hr.work_calendar_weekly_pattern p on p.tenant_id=$1 and p.calendar_id=c.id order by c.code,c.id,p.day_of_week", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var id = reader.GetGuid(0);
                if (!calendars.TryGetValue(id, out var row)) calendars[id] = row = new CalendarRow(id, reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7));
                if (!reader.IsDBNull(8)) row.Days.Add(new(reader.GetInt16(8), reader.GetBoolean(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<TimeOnly>(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<TimeOnly>(11), reader.GetInt16(12)));
            }
            return calendars.Values.Select(row => new WorkCalendarView(row.Id, row.LegalEntityId, row.Code, row.NameAr, row.NameEn, row.TimeZoneId, row.Status, row.Version, row.Days)).ToArray();
        }, ct);

    public async Task<WorkCalendarHolidayView> AddHolidayAsync(TenantId tenant, Guid actor, Guid membershipId, Guid calendarId, Guid legalEntityId, AddWorkCalendarHolidayRequest request, Guid correlationId, CancellationToken ct)
    {
        var name = (request.NameAr ?? string.Empty).Trim(); var source = (request.SourceCode ?? string.Empty).Trim();
        if (request.Date == default || name.Length is < 1 or > 160 || request.NameEn?.Length > 160 || source.Length is < 1 or > 80) throw new HrException("hr.work_calendar_holiday_invalid");
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<WorkCalendarHolidayView>(tenant, async (context, token) =>
        {
            await using var insert = new NpgsqlCommand("insert into business_hr.work_calendar_holiday(id,tenant_id,calendar_id,holiday_date,name_ar,name_en,source_code,created_by,created_at) select $1,$2,c.id,$5,$6,$7,$8,$9,$10 from business_hr.work_calendar c where c.tenant_id=$2 and c.id=$3 and c.legal_entity_id=$4 and c.status='Active'", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(calendarId); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(request.Date); insert.Parameters.AddWithValue(name); insert.Parameters.AddWithValue((object?)request.NameEn?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue(source); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow());
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.work_calendar_not_found");
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.work_calendar.holiday_added", "work_calendar_holiday", id, null, JsonSerializer.Serialize(new { calendarId, request.Date, name, request.NameEn, source }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, calendarId, request.Date, name, request.NameEn?.Trim(), source, "Active", 1);
        }, ct);
    }

    public async Task<IReadOnlyList<WorkCalendarHolidayView>> ListHolidaysAsync(TenantId tenant, Guid calendarId, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<IReadOnlyList<WorkCalendarHolidayView>>(tenant, async (context, token) =>
        {
            var rows = new List<WorkCalendarHolidayView>();
            await using var command = new NpgsqlCommand("select h.id,h.calendar_id,h.holiday_date,h.name_ar,h.name_en,h.source_code,h.status,h.version from business_hr.work_calendar_holiday h join business_hr.work_calendar c on c.tenant_id=h.tenant_id and c.id=h.calendar_id where h.tenant_id=$1 and h.calendar_id=$2 and c.legal_entity_id=$3 order by h.holiday_date desc,h.id limit 500", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(calendarId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7)));
            return rows;
        }, ct);

    public async Task<WorkCalendarHolidayView> CancelHolidayAsync(TenantId tenant, Guid actor, Guid membershipId, Guid holidayId, Guid legalEntityId, CancelWorkCalendarHolidayRequest request, Guid correlationId, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim();
        if (request.ExpectedVersion <= 0 || reason.Length is < 1 or > 500) throw new HrException("hr.work_calendar_holiday_cancel_invalid");
        return await transactions.ExecuteAsync<WorkCalendarHolidayView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update business_hr.work_calendar_holiday h set status='Cancelled',version=h.version+1,cancelled_by=$5,cancelled_at=$6,cancel_reason=$7 from business_hr.work_calendar c where h.tenant_id=$1 and h.id=$2 and h.version=$3 and h.status='Active' and c.tenant_id=h.tenant_id and c.id=h.calendar_id and c.legal_entity_id=$4 returning h.calendar_id,h.holiday_date,h.name_ar,h.name_en,h.source_code,h.version", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(holidayId); command.Parameters.AddWithValue(request.ExpectedVersion); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(clock.GetUtcNow()); command.Parameters.AddWithValue(reason);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.work_calendar_holiday_concurrency_conflict");
            var view = new WorkCalendarHolidayView(holidayId, reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), "Cancelled", reader.GetInt64(5));
            await reader.DisposeAsync();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.work_calendar.holiday_cancelled", "work_calendar_holiday", holidayId, null, JsonSerializer.Serialize(new { view.CalendarId, view.Date, reason, view.Version }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return view;
        }, ct);
    }

    public async Task<WorkDayResolution?> ResolveWorkDayAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, DateOnly date, CancellationToken ct) =>
        await transactions.ExecuteAsync<WorkDayResolution?>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select wc.id,wc.time_zone_id,(h.id is not null),coalesce(p.is_working_day,false) and h.id is null,p.local_start,p.local_end,coalesce(p.break_minutes,0),h.name_ar from business_hr.employment_assignment a join business_hr.work_calendar wc on wc.tenant_id=a.tenant_id and wc.id=a.work_calendar_id left join business_hr.work_calendar_weekly_pattern p on p.tenant_id=wc.tenant_id and p.calendar_id=wc.id and p.day_of_week=extract(dow from $4::date)::smallint left join business_hr.work_calendar_holiday h on h.tenant_id=wc.tenant_id and h.calendar_id=wc.id and h.holiday_date=$4 and h.status='Active' where a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 and a.effective_from<=$4 and (a.effective_to is null or a.effective_to>$4) and wc.status='Active' order by a.effective_from desc,a.id desc limit 1", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(date);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            return new(employmentId, reader.GetGuid(0), date, reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<TimeOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<TimeOnly>(5), reader.GetInt16(6), reader.IsDBNull(7) ? null : reader.GetString(7));
        }, ct);

    private sealed class CalendarRow(Guid id, Guid legalEntityId, string code, string nameAr, string? nameEn, string timeZoneId, string status, long version)
    {
        public Guid Id { get; } = id;
        public Guid LegalEntityId { get; } = legalEntityId;
        public string Code { get; } = code;
        public string NameAr { get; } = nameAr;
        public string? NameEn { get; } = nameEn;
        public string TimeZoneId { get; } = timeZoneId;
        public string Status { get; } = status;
        public long Version { get; } = version;
        public List<WorkCalendarDayRequest> Days { get; } = [];
    }
}
