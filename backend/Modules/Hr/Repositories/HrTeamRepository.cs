using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrTeamRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<HrTeamPage> ListDirectReportsAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct)
    {
        if (asOfDate == default || asOfDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1)) throw new HrException("hr.team_query_date_invalid");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with manager as (
                select employment.id
                from business_hr.employee employee
                join business_hr.employment employment on employment.tenant_id=employee.tenant_id and employment.employee_id=employee.id
                where employee.tenant_id=$1 and employee.membership_id=$2 and employment.legal_entity_id=$3
                    and employment.status='Active' and employment.start_date<=$4 and (employment.end_date is null or employment.end_date>=$4)
            ), team as (
                select employee.id as employee_id, employee.arabic_name, employee.english_name,
                    employment.id as employment_id, employment.employment_number, employment.start_date,
                    assignment.department_id, department.name_ar as department_name, assignment.position_id, position.title_ar as position_title,
                    employee.arabic_name_normalized
                from manager
                join business_hr.employment_assignment assignment on assignment.tenant_id=$1 and assignment.legal_entity_id=$3
                    and assignment.manager_employment_id=manager.id and assignment.effective_from<=$4 and (assignment.effective_to is null or assignment.effective_to>$4)
                join business_hr.employment employment on employment.tenant_id=assignment.tenant_id and employment.id=assignment.employment_id
                    and employment.legal_entity_id=$3 and employment.status='Active' and employment.start_date<=$4 and (employment.end_date is null or employment.end_date>=$4)
                join business_hr.employee employee on employee.tenant_id=employment.tenant_id and employee.id=employment.employee_id and employee.status<>'Archived'
                left join business_hr.department department on department.tenant_id=assignment.tenant_id and department.id=assignment.department_id and department.legal_entity_id=$3
                left join business_hr.position position on position.tenant_id=assignment.tenant_id and position.id=assignment.position_id and position.legal_entity_id=$3
            )
            select totals.total_count, team.employee_id, team.arabic_name, team.english_name,
                team.employment_id, team.employment_number, team.start_date, team.department_id,
                team.department_name, team.position_id, team.position_title
            from (select count(*)::integer as total_count from team) totals
            left join team on true
            order by team.arabic_name_normalized, team.employment_number, team.employee_id
            offset $5 limit $6
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(membershipId);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(asOfDate);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        command.Parameters.AddWithValue(pageSize);
        var rows = new List<HrTeamMemberView>();
        var total = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                total = reader.GetInt32(0);
                if (reader.IsDBNull(1)) continue;
                rows.Add(new(reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetGuid(4), reader.GetString(5), reader.GetFieldValue<DateOnly>(6),
                    reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetString(10)));
            }
        }
        await transaction.CommitAsync(ct);
        return new(rows, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ManagerLeaveApprovalView>> ListPendingLeaveApprovalsAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, int page, int pageSize, CancellationToken ct)
    {
        if (asOfDate == default || asOfDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1)) throw new HrException("hr.team_query_date_invalid");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with manager as (
                select employment.id
                from business_hr.employee employee
                join business_hr.employment employment on employment.tenant_id=employee.tenant_id and employment.employee_id=employee.id
                where employee.tenant_id=$1 and employee.membership_id=$2 and employment.legal_entity_id=$3
                    and employment.status='Active' and employment.start_date<=$4 and (employment.end_date is null or employment.end_date>=$4)
            )
            select request.id, employee.id, employee.display_name, employment.id, employment.employment_number,
                leave_type.id, leave_type.code, leave_type.name_ar, request.requested_from, request.requested_to,
                request.charged_quantity, request.status, request.version, request.submitted_at
            from manager
            join business_hr.employment_assignment assignment on assignment.tenant_id=$1 and assignment.legal_entity_id=$3
                and assignment.manager_employment_id=manager.id and assignment.effective_from<=$4 and (assignment.effective_to is null or assignment.effective_to>$4)
            join business_hr.leave_request request on request.tenant_id=assignment.tenant_id and request.employment_id=assignment.employment_id
                and request.legal_entity_id=$3 and request.status='Pending'
            join business_hr.employment employment on employment.tenant_id=request.tenant_id and employment.id=request.employment_id
                and employment.status='Active' and employment.start_date<=$4 and (employment.end_date is null or employment.end_date>=$4)
            join business_hr.employee employee on employee.tenant_id=request.tenant_id and employee.id=request.employee_id
            join business_hr.leave_type leave_type on leave_type.tenant_id=request.tenant_id and leave_type.id=request.leave_type_id and leave_type.legal_entity_id=$3
            order by request.submitted_at, request.id
            offset $5 limit $6
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(membershipId);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(asOfDate);
        command.Parameters.AddWithValue((page - 1) * pageSize);
        command.Parameters.AddWithValue(pageSize);
        var rows = new List<ManagerLeaveApprovalView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4),
                reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateOnly>(8),
                reader.GetFieldValue<DateOnly>(9), reader.GetDecimal(10), reader.GetString(11), reader.GetInt64(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13)));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return rows;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(ct);
    }
}
