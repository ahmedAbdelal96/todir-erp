using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrHomeRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<HrHomeDashboardView> GetSummaryAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty || asOfDate == default || asOfDate > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1))
            throw new HrException("hr.dashboard_request_invalid");

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantContext.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync(ct);
        }

        await using var command = new NpgsqlCommand("""
            select
                (select count(*)::integer from business_hr.employment e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.start_date<=$3 and (e.end_date is null or e.end_date>=$3) and e.status in ('Active','Suspended','NoticePeriod')),
                (select count(*)::integer from business_hr.attendance_record a where a.tenant_id=$1 and a.legal_entity_id=$2 and a.work_date=$3),
                (select count(*)::integer from business_hr.attendance_record a where a.tenant_id=$1 and a.legal_entity_id=$2 and a.work_date=$3 and a.attendance_status='Present'),
                (select count(*)::integer from business_hr.attendance_record a where a.tenant_id=$1 and a.legal_entity_id=$2 and a.work_date=$3 and a.attendance_status='Absent'),
                (select count(*)::integer from business_hr.attendance_exception x where x.tenant_id=$1 and x.legal_entity_id=$2 and x.status='Open'),
                (select count(*)::integer from business_hr.leave_request l where l.tenant_id=$1 and l.legal_entity_id=$2 and l.status='Pending'),
                (select count(*)::integer from business_hr.onboarding_task t where t.tenant_id=$1 and t.legal_entity_id=$2 and t.status='Pending' and t.due_date<=$3+7),
                (select count(*)::integer from business_hr.employment_contract c where c.tenant_id=$1 and c.legal_entity_id=$2 and c.status='Active' and c.end_date between $3 and $3+30),
                (select count(*)::integer from business_hr.employee_document d where d.tenant_id=$1 and d.status='Active' and d.scope_status='Resolved' and d.expiry_date between $3 and $3+30
                    and exists(select 1 from business_hr.employment e where e.tenant_id=d.tenant_id and e.employee_id=d.employee_id and e.legal_entity_id=$2)
                    and (d.legal_entity_id is null or d.legal_entity_id=$2)
                    and (d.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=d.tenant_id and scoped.id=d.employment_id and scoped.legal_entity_id=$2))),
                (select count(*)::integer from business_hr.employment e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.start_date between $3-29 and $3 and e.status<>'PreHire'),
                (select count(*)::integer from business_hr.employment e where e.tenant_id=$1 and e.legal_entity_id=$2 and e.status='NoticePeriod'),
                (select count(*)::integer from business_hr.attendance_overtime_request o where o.tenant_id=$1 and o.legal_entity_id=$2 and o.status='Pending'),
                (select count(*)::integer from business_hr.attendance_exception x where x.tenant_id=$1 and x.legal_entity_id=$2 and x.status='Open')
                    +(select count(*)::integer from business_hr.attendance_record a where a.tenant_id=$1 and a.legal_entity_id=$2 and a.work_date between $3-7 and $3 and a.attendance_status in ('Partial','Exception','Absent'))
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(legalEntityId);
        command.Parameters.AddWithValue(asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("The HR dashboard summary query returned no row.");
        var result = new HrHomeDashboardView(asOfDate,
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7),
            reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12));
        await reader.CloseAsync();
        await transaction.CommitAsync(ct);
        return result;
    }
}
