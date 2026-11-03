using Npgsql;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrReminderRepository(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<IReadOnlyList<(TenantId Tenant, Guid LegalEntityId)>> ListScheduledScopesAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("select tenant_id,legal_entity_id from business_hr.list_hr_reminder_scopes() order by tenant_id,legal_entity_id", connection);
        var scopes = new List<(TenantId, Guid)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) scopes.Add((new TenantId(reader.GetGuid(0)), reader.GetGuid(1)));
        return scopes;
    }

    public Task<int> ProcessDueAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        ProcessDueAsync(tenant, legalEntityId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);

    public async Task<int> ProcessDueAsync(TenantId tenant, Guid legalEntityId, DateOnly asOf, CancellationToken ct)
    {
        if (asOf == default) throw new HrException("hr.reminder_date_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("""
            with due as materialized (
                select 'ContractExpiry'::text as reminder_type,'employment_contract'::text as source_type,c.id as source_id,e.employee_id,e.id as employment_id,c.end_date as due_date
                from business_hr.employment_contract c join business_hr.employment e on e.tenant_id=c.tenant_id and e.id=c.employment_id and e.legal_entity_id=c.legal_entity_id
                where c.tenant_id=$1 and c.legal_entity_id=$2 and c.status='Active' and e.status in ('PreHire','Active','Suspended','NoticePeriod') and c.end_date between $3 and $4
                union all
                select 'ProbationEnd','employment_contract',c.id,e.employee_id,e.id,c.probation_end
                from business_hr.employment_contract c join business_hr.employment e on e.tenant_id=c.tenant_id and e.id=c.employment_id and e.legal_entity_id=c.legal_entity_id
                where c.tenant_id=$1 and c.legal_entity_id=$2 and c.status='Active' and e.status in ('PreHire','Active','Suspended','NoticePeriod') and c.probation_end between $3 and $4
                union all
                select 'DocumentExpiry','employee_document',d.id,d.employee_id,coalesce(d.employment_id,em.id),d.expiry_date
                from business_hr.employee_document d join business_hr.employment em on em.tenant_id=d.tenant_id and em.employee_id=d.employee_id and em.legal_entity_id=$2
                where d.tenant_id=$1 and d.status='Active' and d.scope_status='Resolved' and d.expiry_date between $3 and $4 and em.status in ('PreHire','Active','Suspended','NoticePeriod')
                    and (d.legal_entity_id is null or d.legal_entity_id=$2)
                    and (d.employment_id is null or d.employment_id=em.id)
                union all
                select 'CredentialExpiry','employee_credential',c.id,c.employee_id,em.id,c.expiry_date
                from business_hr.employee_credential c join business_hr.employment em on em.tenant_id=c.tenant_id and em.employee_id=c.employee_id and em.legal_entity_id=$2
                where c.tenant_id=$1 and c.status='Active' and c.expiry_date between $3 and $4 and em.status in ('PreHire','Active','Suspended','NoticePeriod')
                union all
                select 'InsuranceExpiry','employee_insurance_enrollment',i.id,e.employee_id,e.id,i.effective_to
                from business_hr.employee_insurance_enrollment i join business_hr.employment e on e.tenant_id=i.tenant_id and e.id=i.employment_id and e.legal_entity_id=i.legal_entity_id
                where i.tenant_id=$1 and i.legal_entity_id=$2 and i.status='Active' and e.status in ('PreHire','Active','Suspended','NoticePeriod') and i.effective_to between $3 and $4
                union all
                select 'OnboardingTaskDue','onboarding_task',t.id,c.employee_id,c.employment_id,t.due_date
                from business_hr.onboarding_task t join business_hr.onboarding_case c on c.tenant_id=t.tenant_id and c.id=t.case_id
                where t.tenant_id=$1 and t.legal_entity_id=$2 and t.status='Pending' and c.status='Open' and t.due_date between $5 and $4
                union all
                select 'OffboardingTaskDue','offboarding_task',t.id,c.employee_id,c.employment_id,t.due_date
                from business_hr.offboarding_task t join business_hr.offboarding_case c on c.tenant_id=t.tenant_id and c.id=t.case_id
                where t.tenant_id=$1 and t.legal_entity_id=$2 and t.status='Pending' and c.status='Open' and t.due_date between $5 and $4
                union all
                select 'LeaveBalanceExpiry','leave_balance_movement',lot.id,e.employee_id,e.id,lot.expires_on
                from business_hr.leave_balance_movement lot
                join business_hr.employment e on e.tenant_id=lot.tenant_id and e.id=lot.employment_id and e.legal_entity_id=lot.legal_entity_id
                left join business_hr.leave_balance_allocation allocation on allocation.tenant_id=lot.tenant_id and allocation.entitlement_movement_id=lot.id
                left join business_hr.leave_balance_movement consumption on consumption.tenant_id=allocation.tenant_id and consumption.id=allocation.movement_id
                where lot.tenant_id=$1 and lot.legal_entity_id=$2 and lot.movement_type='CarryOver' and lot.quantity>0
                    and lot.expires_on between $3 and $4 and e.status in ('PreHire','Active','Suspended','NoticePeriod')
                group by lot.id,e.employee_id,e.id,lot.expires_on,lot.quantity
                having lot.quantity+coalesce(sum(consumption.quantity) filter(where consumption.effective_date<=$3 or (consumption.source_type='LeaveRequest' and consumption.created_at::date<=$3)),0)>0
                order by due_date,reminder_type,source_id
                limit 500
            )
            insert into business_hr.hr_reminder(id,tenant_id,legal_entity_id,employee_id,employment_id,reminder_type,source_type,source_id,due_date,created_at)
            select gen_random_uuid(),$1,$2,employee_id,employment_id,reminder_type,source_type,source_id,due_date,now() from due
            on conflict (tenant_id,legal_entity_id,reminder_type,source_id,due_date) do nothing
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOf); command.Parameters.AddWithValue(asOf.AddDays(30)); command.Parameters.AddWithValue(asOf.AddDays(-30));
        var inserted = await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return inserted;
    }

    public async Task<HrReminderPage> ListAsync(TenantId tenant, Guid legalEntityId, DateOnly asOf, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) throw new HrException("hr.pagination_invalid");
        pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        int total;
        await using (var count = new NpgsqlCommand("select count(*)::integer from business_hr.hr_reminder where tenant_id=$1 and legal_entity_id=$2 and due_date between $3 and $4", connection, transaction))
        {
            count.Parameters.AddWithValue(tenant.Value); count.Parameters.AddWithValue(legalEntityId); count.Parameters.AddWithValue(asOf.AddDays(-30)); count.Parameters.AddWithValue(asOf.AddDays(30));
            total = (int)(await count.ExecuteScalarAsync(ct) ?? 0);
        }
        var rows = new List<HrReminderView>();
        await using (var command = new NpgsqlCommand("select r.id,r.reminder_type,r.source_id,r.employee_id,e.display_name,em.employment_number,r.due_date,(r.due_date-$3)::integer,r.created_at from business_hr.hr_reminder r join business_hr.employee e on e.tenant_id=r.tenant_id and e.id=r.employee_id join business_hr.employment em on em.tenant_id=r.tenant_id and em.id=r.employment_id and em.legal_entity_id=r.legal_entity_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.due_date between $4 and $5 order by r.due_date,r.reminder_type,r.id offset $6 limit $7", connection, transaction))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOf); command.Parameters.AddWithValue(asOf.AddDays(-30)); command.Parameters.AddWithValue(asOf.AddDays(30)); command.Parameters.AddWithValue((page - 1) * pageSize); command.Parameters.AddWithValue(pageSize);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateOnly>(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8)));
        }
        await transaction.CommitAsync(ct);
        return new(rows, page, pageSize, total, asOf);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(ct);
    }
}
