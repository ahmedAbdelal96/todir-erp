using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrReportsRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<HrAttendanceReportPage> GetAttendanceReportAsync(TenantId tenant, Guid legalEntityId, DateOnly from, DateOnly to, int page, int pageSize, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty || from == default || to < from || to.DayNumber - from.DayNumber > 366)
            throw new HrException("hr.attendance_report_range_invalid");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with attendance_facts as materialized (
                select attendance.tenant_id, attendance.employment_id,
                    count(*)::integer as record_count,
                    count(*) filter (where attendance.attendance_status='Present')::integer as present_days,
                    count(*) filter (where attendance.attendance_status='Absent')::integer as absent_days,
                    count(*) filter (where attendance.attendance_status='Partial')::integer as partial_days,
                    count(*) filter (where attendance.attendance_status='OnApprovedLeave')::integer as approved_leave_days,
                    coalesce(sum(attendance.worked_minutes),0)::integer as worked_minutes,
                    coalesce(sum(attendance.late_minutes),0)::integer as late_minutes,
                    coalesce(sum(attendance.overtime_eligible_minutes),0)::integer as overtime_eligible_minutes
                from business_hr.attendance_record attendance
                where attendance.tenant_id=$1 and attendance.legal_entity_id=$2 and attendance.work_date between $3 and $4
                group by attendance.tenant_id,attendance.employment_id
            ), exception_facts as materialized (
                select exception.tenant_id,exception.employment_id,count(*)::integer as open_exception_count
                from business_hr.attendance_exception exception
                join business_hr.attendance_record attendance on attendance.tenant_id=exception.tenant_id and attendance.id=exception.attendance_record_id
                where exception.tenant_id=$1 and exception.legal_entity_id=$2 and exception.status='Open' and attendance.work_date between $3 and $4
                group by exception.tenant_id,exception.employment_id
            ), overtime_facts as materialized (
                select overtime.tenant_id,overtime.employment_id,sum(overtime.requested_minutes)::integer as approved_overtime_minutes
                from business_hr.attendance_overtime_request overtime
                join business_hr.attendance_record attendance on attendance.tenant_id=overtime.tenant_id and attendance.id=overtime.attendance_record_id
                where overtime.tenant_id=$1 and overtime.legal_entity_id=$2 and overtime.status='Approved' and attendance.work_date between $3 and $4
                group by overtime.tenant_id,overtime.employment_id
            ), report_rows as materialized (
                select employment.id as employment_id, employee.id as employee_id, employee.display_name, employment.employment_number,
                    coalesce(attendance.record_count,0) as record_count,
                    coalesce(attendance.present_days,0) as present_days,
                    coalesce(attendance.absent_days,0) as absent_days,
                    coalesce(attendance.partial_days,0) as partial_days,
                    coalesce(attendance.approved_leave_days,0) as approved_leave_days,
                    coalesce(attendance.worked_minutes,0) as worked_minutes,
                    coalesce(attendance.late_minutes,0) as late_minutes,
                    coalesce(attendance.overtime_eligible_minutes,0) as overtime_eligible_minutes,
                    coalesce(overtime.approved_overtime_minutes,0) as approved_overtime_minutes,
                    coalesce(exception.open_exception_count,0) as open_exception_count
                from business_hr.employment employment
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id and employee.status<>'Archived'
                left join attendance_facts attendance on attendance.tenant_id=employment.tenant_id and attendance.employment_id=employment.id
                left join exception_facts exception on exception.tenant_id=employment.tenant_id and exception.employment_id=employment.id
                left join overtime_facts overtime on overtime.tenant_id=employment.tenant_id and overtime.employment_id=employment.id
                where employment.tenant_id=$1 and employment.legal_entity_id=$2 and employment.start_date<=$4 and (employment.end_date is null or employment.end_date>=$3)
            ), paged_rows as (
                select * from report_rows order by employment_number,employment_id offset $5 limit $6
            )
            select (select count(*)::integer from report_rows), paged_rows.*
            from (values(1)) anchor(value)
            left join paged_rows on true
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        command.Parameters.AddWithValue(pageSize);
        var rows = new List<HrAttendanceReportItem>();
        var total = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                total = reader.GetInt32(0);
                if (reader.IsDBNull(1)) continue;
                rows.Add(new(reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5),
                    reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10),
                    reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13), reader.GetInt32(14)));
            }
        }
        await transaction.CommitAsync(ct);
        return new(rows, page, pageSize, total, from, to);
    }

    public async Task<HrExpiryReportPage> GetExpiryReportAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty || asOfDate == default || asOfDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1))
            throw new HrException("hr.expiry_report_date_invalid");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var from = asOfDate.AddDays(-30);
        var to = asOfDate.AddDays(90);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with report_items as materialized (
                select employment.employee_id, employee.display_name, employment.id as employment_id, employment.employment_number,
                    'Contract'::text as item_type, coalesce(contract.contract_number,contract.contract_type) as item_label, contract.end_date as expiry_date
                from business_hr.employment_contract contract
                join business_hr.employment employment on employment.tenant_id=contract.tenant_id and employment.id=contract.employment_id and employment.legal_entity_id=contract.legal_entity_id
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id
                where contract.tenant_id=$1 and contract.legal_entity_id=$2 and contract.status in ('Active','Expired') and contract.end_date between $3 and $4
                union all
                select document.employee_id, employee.display_name, null::uuid, null::text,
                    'Document'::text, 'Employee document'::text, document.expiry_date
                from business_hr.employee_document document
                join business_hr.employee employee on employee.tenant_id=document.tenant_id and employee.id=document.employee_id
                where document.tenant_id=$1 and document.status='Active' and document.expiry_date between $3 and $4
                    and document.sensitivity not in ('Medical','Restricted')
                    and exists(select 1 from business_hr.employment employment where employment.tenant_id=document.tenant_id and employment.employee_id=document.employee_id
                        and employment.legal_entity_id=$2 and employment.status in ('Active','Suspended','NoticePeriod'))
                union all
                select credential.employee_id, employee.display_name, null::uuid, null::text,
                    'Credential'::text, credential.credential_type, credential.expiry_date
                from business_hr.employee_credential credential
                join business_hr.employee employee on employee.tenant_id=credential.tenant_id and employee.id=credential.employee_id
                where credential.tenant_id=$1 and credential.status='Active' and credential.expiry_date between $3 and $4
                    and exists(select 1 from business_hr.employment employment where employment.tenant_id=credential.tenant_id and employment.employee_id=credential.employee_id
                        and employment.legal_entity_id=$2 and employment.status in ('Active','Suspended','NoticePeriod'))
                union all
                select employment.employee_id, employee.display_name, employment.id, employment.employment_number,
                    'Insurance'::text, insurance.scheme_code, insurance.effective_to
                from business_hr.employee_insurance_enrollment insurance
                join business_hr.employment employment on employment.tenant_id=insurance.tenant_id and employment.id=insurance.employment_id and employment.legal_entity_id=insurance.legal_entity_id
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id
                where insurance.tenant_id=$1 and insurance.legal_entity_id=$2 and insurance.status='Active' and insurance.effective_to between $3 and $4
                    and employment.status in ('Active','Suspended','NoticePeriod')
            ), paged_items as (
                select * from report_items order by expiry_date, employee_id, item_type offset $5 limit $6
            )
            select (select count(*)::integer from report_items), paged_items.* from (values(1)) anchor(value) left join paged_items on true
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        command.Parameters.AddWithValue(pageSize);
        var rows = new List<HrExpiryReportItem>();
        var total = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                total = reader.GetInt32(0);
                if (reader.IsDBNull(1)) continue;
                var expiry = reader.GetFieldValue<DateOnly>(7);
                rows.Add(new(reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), expiry, expiry.DayNumber - asOfDate.DayNumber));
            }
        }
        await transaction.CommitAsync(ct);
        return new(rows, page, pageSize, total, asOfDate);
    }

    public async Task<HrLeaveBalanceReportPage> GetLeaveBalanceReportAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty || asOfDate == default || asOfDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1))
            throw new HrException("hr.leave_report_date_invalid");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with report_rows as materialized (
                select employment.id as employment_id, employee.id as employee_id, employee.display_name, employment.employment_number,
                    leave_type.id as leave_type_id, leave_type.code as leave_type_code, leave_type.name_ar as leave_type_name_ar,
                    leave_type.unit, coalesce(sum(movement.quantity) filter (where movement.effective_date<=$3),0) as balance
                from business_hr.employment employment
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id and employee.status<>'Archived'
                join business_hr.leave_type leave_type on leave_type.tenant_id=employment.tenant_id and leave_type.legal_entity_id=employment.legal_entity_id
                left join business_hr.leave_balance_movement movement on movement.tenant_id=employment.tenant_id and movement.employment_id=employment.id
                    and movement.legal_entity_id=employment.legal_entity_id and movement.leave_type_id=leave_type.id and movement.effective_date<=$3
                where employment.tenant_id=$1 and employment.legal_entity_id=$2 and employment.start_date<=$3
                    and (employment.end_date is null or employment.end_date>=$3) and employment.status in ('Active','Suspended','NoticePeriod')
                group by employment.id,employee.id,employee.display_name,employment.employment_number,leave_type.id,leave_type.code,leave_type.name_ar,leave_type.unit
            ), paged_rows as (
                select * from report_rows order by employment_number,leave_type_code,employment_id,leave_type_id offset $4 limit $5
            )
            select (select count(*)::integer from report_rows), paged_rows.* from (values(1)) anchor(value) left join paged_rows on true
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(asOfDate);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        command.Parameters.AddWithValue(pageSize);
        var rows = new List<HrLeaveBalanceReportItem>();
        var total = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                total = reader.GetInt32(0);
                if (reader.IsDBNull(1)) continue;
                rows.Add(new(reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
                    reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetDecimal(9)));
            }
        }
        await transaction.CommitAsync(ct);
        return new(rows, page, pageSize, total, asOfDate);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(ct);
    }
}
