using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Contracts.Hr;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class PayrollIntegrationRepository(ITransactionCoordinator transactions, TimeProvider clock) : IHumanResourcesPayrollCapability
{
    private const int ContractVersion = 5;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<HrPayrollPeriodSnapshotContract> CreateSnapshotAsync(TenantId tenant, HrPayrollPeriodSnapshotRequest request, CancellationToken ct)
    {
        var cutoff = request.CutoffAt.ToUniversalTime();
        if (request.LegalEntityId == Guid.Empty || request.PeriodStart == default || request.PeriodEnd < request.PeriodStart
            || request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber > 366 || cutoff > clock.GetUtcNow().AddSeconds(5))
            throw new HrException("hr.payroll_snapshot_request_invalid");

        return await transactions.ExecuteAsync<HrPayrollPeriodSnapshotContract>(tenant, async (context, token) =>
        {
            await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':hr-payroll-snapshot:' || $2::text || ':' || $3::text || ':' || $4::text || ':' || $5::text,0))", context.Connection, context.Transaction))
            {
                lockCommand.Parameters.AddWithValue(tenant.Value); lockCommand.Parameters.AddWithValue(request.LegalEntityId); lockCommand.Parameters.AddWithValue(request.PeriodStart); lockCommand.Parameters.AddWithValue(request.PeriodEnd); lockCommand.Parameters.AddWithValue(cutoff); await lockCommand.ExecuteNonQueryAsync(token);
            }
            await using (var existing = new NpgsqlCommand("select id,contract_version,legal_entity_id,period_start,period_end,cutoff_at,created_at,payload_sha256,payload::text from business_hr.payroll_period_snapshot where tenant_id=$1 and legal_entity_id=$2 and period_start=$3 and period_end=$4 and cutoff_at=$5", context.Connection, context.Transaction))
            {
                existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.LegalEntityId); existing.Parameters.AddWithValue(request.PeriodStart); existing.Parameters.AddWithValue(request.PeriodEnd); existing.Parameters.AddWithValue(cutoff);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var snapshot = ReadSnapshot(reader); await reader.CloseAsync(); return snapshot;
                }
            }

            var employments = new List<HrPayrollEmploymentInput>();
            await using (var query = new NpgsqlCommand("""
                with employment_scope as materialized (
                    select e.tenant_id,e.id,e.employee_id,e.legal_entity_id,e.employment_number,e.status
                    from business_hr.employment e
                    where e.tenant_id=$1 and e.legal_entity_id=$2 and e.created_at<=$5 and e.start_date<=$4
                        and (e.end_date is null or e.end_date>=$3) and e.status in ('Active','Suspended','NoticePeriod','Terminated','Ended')
                ), contract_context as (
                    select c.tenant_id,c.employment_id,c.legal_entity_id,
                        jsonb_agg(jsonb_build_object('contractId',c.id,'contractType',c.contract_type,'startDate',c.start_date,'endDate',c.end_date,'version',c.version) order by c.start_date,c.id) as items
                    from business_hr.employment_contract c join employment_scope e on e.tenant_id=c.tenant_id and e.id=c.employment_id and e.legal_entity_id=c.legal_entity_id
                    where (c.start_date is null or c.start_date<=$4) and (c.end_date is null or c.end_date>=$3) and c.created_at<=$5 and c.status in ('Active','Expired','Ended','Superseded')
                    group by c.tenant_id,c.employment_id,c.legal_entity_id
                ), compensation_context as (
                    select c.tenant_id,c.employment_id,c.legal_entity_id,
                        jsonb_agg(jsonb_build_object('agreementId',c.id,'currencyCode',rtrim(c.currency_code),'amount',c.amount,'payFrequency',c.pay_frequency,'effectiveFrom',c.effective_from,'effectiveTo',c.effective_to,'version',c.version) order by c.effective_from,c.id) as items
                    from business_hr.employment_compensation_agreement c join employment_scope e on e.tenant_id=c.tenant_id and e.id=c.employment_id and e.legal_entity_id=c.legal_entity_id
                    where c.effective_from<=$4 and (c.effective_to is null or c.effective_to>=$3) and c.created_at<=$5 and c.status in ('Active','Superseded','Ended')
                    group by c.tenant_id,c.employment_id,c.legal_entity_id
                ), adjustment_context as (
                    select a.tenant_id,a.employment_id,a.legal_entity_id,
                        jsonb_agg(jsonb_build_object('adjustmentId',a.id,'typeCode',a.adjustment_type,'amount',a.amount,'currencyCode',rtrim(a.currency_code),'recurrence',a.recurrence,'effectiveFrom',a.effective_from,'effectiveTo',a.effective_to) order by a.effective_from,a.id) as items
                    from business_hr.hr_adjustment a join employment_scope e on e.tenant_id=a.tenant_id and e.id=a.employment_id and e.legal_entity_id=a.legal_entity_id
                    where a.status='Approved' and a.payroll_impact='SendToPayroll' and a.created_at<=$5 and a.approved_at<=$5
                        and a.effective_from<=$4 and (a.effective_to is null or a.effective_to>=$3)
                    group by a.tenant_id,a.employment_id,a.legal_entity_id
                ), attendance_context as (
                    select ar.tenant_id,ar.employment_id,ar.legal_entity_id,count(*)::integer as record_count,
                        coalesce(sum(ar.worked_minutes),0)::integer as worked_minutes,
                        coalesce(sum(ar.late_minutes),0)::integer as late_minutes,
                        coalesce(sum(ar.early_leave_minutes),0)::integer as early_leave_minutes,
                        coalesce(sum(ar.overtime_eligible_minutes),0)::integer as overtime_eligible_minutes,
                        count(*) filter (where ar.attendance_status='Absent')::integer as absent_days,
                        count(*) filter (where ar.attendance_status='OnApprovedLeave')::integer as approved_leave_days
                    from business_hr.attendance_record ar join employment_scope e on e.tenant_id=ar.tenant_id and e.id=ar.employment_id and e.legal_entity_id=ar.legal_entity_id
                    where ar.work_date between $3 and $4
                    group by ar.tenant_id,ar.employment_id,ar.legal_entity_id
                ), overtime_context as (
                    select ov.tenant_id,ov.employment_id,ov.legal_entity_id,sum(ov.requested_minutes)::integer as approved_overtime_minutes
                    from business_hr.attendance_overtime_request ov
                    join business_hr.attendance_record ar on ar.tenant_id=ov.tenant_id and ar.id=ov.attendance_record_id and ar.legal_entity_id=ov.legal_entity_id
                    join employment_scope e on e.tenant_id=ov.tenant_id and e.id=ov.employment_id and e.legal_entity_id=ov.legal_entity_id
                    where ov.status='Approved' and ov.created_at<=$5 and ov.decided_at<=$5 and ar.work_date between $3 and $4
                    group by ov.tenant_id,ov.employment_id,ov.legal_entity_id
                ), exception_context as (
                    select ax.tenant_id,ax.employment_id,ax.legal_entity_id,count(*)::integer as exception_count
                    from business_hr.attendance_exception ax
                    join business_hr.attendance_record ar on ar.tenant_id=ax.tenant_id and ar.id=ax.attendance_record_id and ar.legal_entity_id=ax.legal_entity_id
                    join employment_scope e on e.tenant_id=ax.tenant_id and e.id=ax.employment_id and e.legal_entity_id=ax.legal_entity_id
                    where ar.work_date between $3 and $4
                    group by ax.tenant_id,ax.employment_id,ax.legal_entity_id
                ), insurance_context as (
                    select i.tenant_id,i.employment_id,i.legal_entity_id,
                        jsonb_agg(jsonb_build_object('schemeCode',i.scheme_code,'providerCode',i.provider_code,'status',i.status,'effectiveFrom',i.effective_from,'effectiveTo',i.effective_to,'countryProviderVersion',i.country_provider_version) order by i.effective_from,i.id) as items
                    from business_hr.employee_insurance_enrollment i join employment_scope e on e.tenant_id=i.tenant_id and e.id=i.employment_id and e.legal_entity_id=i.legal_entity_id
                    where i.effective_from<=$4 and (i.effective_to is null or i.effective_to>=$3) and i.created_at<=$5
                    group by i.tenant_id,i.employment_id,i.legal_entity_id
                ), leave_request_context as (
                    select lr.tenant_id,lr.employment_id,lr.legal_entity_id,lr.id,lr.leave_type_id,lt.code as leave_type_code,
                        lr.requested_from,lr.requested_to,lp.payroll_treatment,lp.version as policy_version,
                        coalesce(sum(ch.quantity),0)::numeric as charged_quantity,
                        coalesce(max(ch.unit),'Days') as unit,state.to_status as status_at_cutoff
                    from business_hr.leave_request lr
                    join employment_scope e on e.tenant_id=lr.tenant_id and e.id=lr.employment_id and e.legal_entity_id=lr.legal_entity_id
                    join business_hr.leave_policy lp on lp.tenant_id=lr.tenant_id and lp.id=lr.policy_id
                    join business_hr.leave_type lt on lt.tenant_id=lr.tenant_id and lt.id=lr.leave_type_id
                    left join business_hr.leave_request_charge ch on ch.tenant_id=lr.tenant_id and ch.leave_request_id=lr.id and ch.charge_date between $3 and $4
                    join lateral (select ev.to_status from business_hr.leave_request_event ev where ev.tenant_id=lr.tenant_id and ev.leave_request_id=lr.id and ev.occurred_at<=$5 order by ev.occurred_at desc,ev.id desc limit 1) state on true
                    where lr.created_at<=$5 and lr.requested_from<=$4 and lr.requested_to>=$3
                    group by lr.tenant_id,lr.employment_id,lr.legal_entity_id,lr.id,lr.leave_type_id,lt.code,lr.requested_from,lr.requested_to,lp.payroll_treatment,lp.version,state.to_status
                ), leave_context as (
                    select tenant_id,employment_id,legal_entity_id,
                        jsonb_agg(jsonb_build_object('leaveRequestId',id,'leaveTypeId',leave_type_id,'leaveTypeCode',leave_type_code,'statusAtCutoff',status_at_cutoff,'payrollTreatment',payroll_treatment,'policyVersion',policy_version,'requestedFrom',requested_from,'requestedTo',requested_to,'chargedQuantityInPeriod',charged_quantity,'unit',unit) order by requested_from,id) as items
                    from leave_request_context where status_at_cutoff in ('Approved','Taken') and charged_quantity>0
                    group by tenant_id,employment_id,legal_entity_id
                )
                select e.id,e.employee_id,e.employment_number,e.status,
                    coalesce(contract.items,'[]'::jsonb)::text,
                    coalesce(compensation.items,'[]'::jsonb)::text,
                    coalesce(adjustment.items,'[]'::jsonb)::text,
                    coalesce(attendance.record_count,0),coalesce(attendance.worked_minutes,0),coalesce(attendance.late_minutes,0),
                    coalesce(attendance.early_leave_minutes,0),coalesce(attendance.overtime_eligible_minutes,0),
                    coalesce(overtime.approved_overtime_minutes,0),coalesce(exception.exception_count,0),
                    coalesce(insurance.items,'[]'::jsonb)::text,coalesce(leave_context.items,'[]'::jsonb)::text,
                    coalesce(attendance.absent_days,0),coalesce(attendance.approved_leave_days,0),
                    coalesce((select jsonb_build_object(
                        'offboardingCaseId',oc.id,'reasonType',oc.reason_type,'reasonCode',oc.reason_code,'noticeDate',oc.notice_date,
                        'lastWorkingDate',oc.last_working_date,'effectiveEndDate',oc.effective_end_date,'finalPayrollRequired',oc.final_payroll_required,
                        'unusedLeaveBalances',coalesce((select jsonb_agg(jsonb_build_object('leaveTypeId',balance.leave_type_id,'leaveTypeCode',balance.code,'balance',balance.quantity,'unit',balance.unit) order by balance.code,balance.leave_type_id)
                            from (select lt.id as leave_type_id,lt.code,lt.unit, sum(m.quantity)::numeric as quantity
                                from business_hr.leave_balance_movement m join business_hr.leave_type lt on lt.tenant_id=m.tenant_id and lt.id=m.leave_type_id and lt.legal_entity_id=m.legal_entity_id
                                where m.tenant_id=oc.tenant_id and m.employment_id=oc.employment_id and m.legal_entity_id=oc.legal_entity_id
                                    and m.effective_date<=oc.effective_end_date and m.created_at<=$5
                                group by lt.id,lt.code,lt.unit having sum(m.quantity)<>0) balance), '[]'::jsonb)
                    )::text from business_hr.offboarding_case oc where oc.tenant_id=e.tenant_id and oc.employment_id=e.id and oc.legal_entity_id=e.legal_entity_id and oc.termination_status='Effective' and oc.effective_end_date<=$4 and oc.created_at<=$5), 'null')
                from employment_scope e
                left join contract_context contract on contract.tenant_id=e.tenant_id and contract.employment_id=e.id and contract.legal_entity_id=e.legal_entity_id
                left join compensation_context compensation on compensation.tenant_id=e.tenant_id and compensation.employment_id=e.id and compensation.legal_entity_id=e.legal_entity_id
                left join adjustment_context adjustment on adjustment.tenant_id=e.tenant_id and adjustment.employment_id=e.id and adjustment.legal_entity_id=e.legal_entity_id
                left join attendance_context attendance on attendance.tenant_id=e.tenant_id and attendance.employment_id=e.id and attendance.legal_entity_id=e.legal_entity_id
                left join overtime_context overtime on overtime.tenant_id=e.tenant_id and overtime.employment_id=e.id and overtime.legal_entity_id=e.legal_entity_id
                left join exception_context exception on exception.tenant_id=e.tenant_id and exception.employment_id=e.id and exception.legal_entity_id=e.legal_entity_id
                left join insurance_context insurance on insurance.tenant_id=e.tenant_id and insurance.employment_id=e.id and insurance.legal_entity_id=e.legal_entity_id
                left join leave_context on leave_context.tenant_id=e.tenant_id and leave_context.employment_id=e.id and leave_context.legal_entity_id=e.legal_entity_id
                order by e.employment_number,e.id
                """, context.Connection, context.Transaction))
            {
                query.Parameters.AddWithValue(tenant.Value); query.Parameters.AddWithValue(request.LegalEntityId); query.Parameters.AddWithValue(request.PeriodStart); query.Parameters.AddWithValue(request.PeriodEnd); query.Parameters.AddWithValue(cutoff);
                await using var reader = await query.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    var contracts = JsonSerializer.Deserialize<HrPayrollContractContext[]>(reader.GetString(4), JsonOptions) ?? [];
                    var compensation = JsonSerializer.Deserialize<HrPayrollCompensationInput[]>(reader.GetString(5), JsonOptions) ?? [];
                    var adjustments = JsonSerializer.Deserialize<HrPayrollAdjustmentInput[]>(reader.GetString(6), JsonOptions) ?? [];
                    var insurance = (JsonSerializer.Deserialize<HrPayrollInsuranceContext[]>(reader.GetString(14), JsonOptions) ?? [])
                        .Where(static item => item.Status is "Active" or "Ended").ToArray();
                    var leave = JsonSerializer.Deserialize<HrPayrollLeaveContext[]>(reader.GetString(15), JsonOptions) ?? [];
                    var finalExit = JsonSerializer.Deserialize<HrPayrollFinalExitContext?>(reader.GetString(18), JsonOptions);
                    employments.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), contracts, compensation, adjustments,
                        new(reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13)),
                        leave, new(reader.GetInt32(16), reader.GetInt32(17)), insurance, finalExit));
                }
            }
            var snapshotId = Guid.CreateVersion7(); var createdAt = clock.GetUtcNow();
            var payload = JsonSerializer.Serialize(employments, JsonOptions);
            var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            var result = new HrPayrollPeriodSnapshotContract(snapshotId, ContractVersion, request.LegalEntityId, request.PeriodStart, request.PeriodEnd, cutoff, createdAt, checksum, employments);
            await using (var insert = new NpgsqlCommand("insert into business_hr.payroll_period_snapshot(id,tenant_id,legal_entity_id,period_start,period_end,cutoff_at,created_at,contract_version,payload_sha256,payload) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10::jsonb)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(snapshotId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.PeriodStart); insert.Parameters.AddWithValue(request.PeriodEnd); insert.Parameters.AddWithValue(cutoff); insert.Parameters.AddWithValue(createdAt); insert.Parameters.AddWithValue(ContractVersion); insert.Parameters.AddWithValue(checksum); insert.Parameters.AddWithValue(payload); await insert.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Service", null, null, "hr", "hr.payroll_snapshot.created", "payroll_period_snapshot", snapshotId, null, JsonSerializer.Serialize(new { request.PeriodStart, request.PeriodEnd, cutoff, checksum, employmentCount = employments.Count }), null, "payroll-capability", snapshotId, System.Diagnostics.Activity.Current?.TraceId.ToString(), createdAt, null), token);
            return result;
        }, ct);
    }

    public async Task<HrAttendancePeriodLockContract> LockAttendancePeriodAsync(TenantId tenant, HrAttendancePeriodLockRequest request, CancellationToken ct)
    {
        var reference = request.PayrollReference?.Trim();
        if (request.LegalEntityId == Guid.Empty || request.PayrollRunId == Guid.Empty || request.SnapshotId == Guid.Empty
            || string.IsNullOrWhiteSpace(reference) || reference.Length > 120 || request.PeriodStart == default || request.PeriodEnd < request.PeriodStart)
            throw new HrException("hr.attendance_payroll_lock_request_invalid");

        return await transactions.ExecuteAsync<HrAttendancePeriodLockContract>(tenant, async (context, token) =>
        {
            await using (var lockPeriod = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':hr-attendance-lock:' || $2::text || ':' || $3::text || ':' || $4::text,0))", context.Connection, context.Transaction))
            {
                lockPeriod.Parameters.AddWithValue(tenant.Value); lockPeriod.Parameters.AddWithValue(request.LegalEntityId); lockPeriod.Parameters.AddWithValue(request.PeriodStart); lockPeriod.Parameters.AddWithValue(request.PeriodEnd); await lockPeriod.ExecuteNonQueryAsync(token);
            }
            await using (var existing = new NpgsqlCommand("select payroll_snapshot_id,legal_entity_id,period_start,period_end,payroll_reference,locked_record_count,locked_at from business_hr.attendance_payroll_lock where tenant_id=$1 and legal_entity_id=$2 and period_start=$3 and period_end=$4 order by locked_at desc limit 1", context.Connection, context.Transaction))
            {
                existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.LegalEntityId); existing.Parameters.AddWithValue(request.PeriodStart); existing.Parameters.AddWithValue(request.PeriodEnd); await using var reader = await existing.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    if (reader.GetGuid(0) != request.SnapshotId) throw new HrException("hr.attendance_payroll_lock_idempotency_conflict");
                    var result = new HrAttendancePeriodLockContract(request.PayrollRunId, reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6));
                    await reader.CloseAsync(); return result;
                }
            }
            await using (var snapshot = new NpgsqlCommand("select 1 from business_hr.payroll_period_snapshot where tenant_id=$1 and id=$2 and legal_entity_id=$3 and period_start=$4 and period_end=$5", context.Connection, context.Transaction))
            {
                snapshot.Parameters.AddWithValue(tenant.Value); snapshot.Parameters.AddWithValue(request.SnapshotId); snapshot.Parameters.AddWithValue(request.LegalEntityId); snapshot.Parameters.AddWithValue(request.PeriodStart); snapshot.Parameters.AddWithValue(request.PeriodEnd);
                if (await snapshot.ExecuteScalarAsync(token) is null) throw new HrException("hr.payroll_snapshot_not_found");
            }
            await using (var employmentLocks = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':attendance:' || e.id::text,0)) from (select id from business_hr.employment where tenant_id=$1 and legal_entity_id=$2 and start_date<=$4 and (end_date is null or end_date>=$3) order by id) e", context.Connection, context.Transaction))
            {
                employmentLocks.Parameters.AddWithValue(tenant.Value); employmentLocks.Parameters.AddWithValue(request.LegalEntityId); employmentLocks.Parameters.AddWithValue(request.PeriodStart); employmentLocks.Parameters.AddWithValue(request.PeriodEnd);
                await using var reader = await employmentLocks.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) { }
            }
            await using (var pendingOvertime = new NpgsqlCommand("select exists(select 1 from business_hr.attendance_overtime_request o join business_hr.attendance_record a on a.tenant_id=o.tenant_id and a.id=o.attendance_record_id where o.tenant_id=$1 and o.legal_entity_id=$2 and a.work_date between $3 and $4 and o.status='Pending')", context.Connection, context.Transaction))
            {
                pendingOvertime.Parameters.AddWithValue(tenant.Value); pendingOvertime.Parameters.AddWithValue(request.LegalEntityId); pendingOvertime.Parameters.AddWithValue(request.PeriodStart); pendingOvertime.Parameters.AddWithValue(request.PeriodEnd);
                if ((bool)(await pendingOvertime.ExecuteScalarAsync(token) ?? false)) throw new HrException("hr.overtime_approvals_pending");
            }
            var lockedAt = clock.GetUtcNow(); var batchId = Guid.CreateVersion7(); int lockedCount;
            await using (var update = new NpgsqlCommand("update business_hr.attendance_record set lock_state='LockedForPayroll',payroll_reference=$5,version=version+1,updated_at=$6 where tenant_id=$1 and legal_entity_id=$2 and work_date between $3 and $4 and lock_state='Open'", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(request.LegalEntityId); update.Parameters.AddWithValue(request.PeriodStart); update.Parameters.AddWithValue(request.PeriodEnd); update.Parameters.AddWithValue(reference); update.Parameters.AddWithValue(lockedAt); lockedCount = await update.ExecuteNonQueryAsync(token);
            }
            await using (var insert = new NpgsqlCommand("insert into business_hr.attendance_payroll_lock(id,tenant_id,legal_entity_id,payroll_run_id,payroll_snapshot_id,payroll_reference,period_start,period_end,locked_record_count,locked_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(batchId); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(request.PayrollRunId); insert.Parameters.AddWithValue(request.SnapshotId); insert.Parameters.AddWithValue(reference); insert.Parameters.AddWithValue(request.PeriodStart); insert.Parameters.AddWithValue(request.PeriodEnd); insert.Parameters.AddWithValue(lockedCount); insert.Parameters.AddWithValue(lockedAt); await insert.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Service", null, null, "hr", "hr.attendance.period_locked_for_payroll", "attendance_payroll_lock", batchId, null, JsonSerializer.Serialize(new { request.PayrollRunId, request.SnapshotId, reference, request.PeriodStart, request.PeriodEnd, lockedCount }), null, "payroll-capability", request.PayrollRunId, System.Diagnostics.Activity.Current?.TraceId.ToString(), lockedAt, null), token);
            return new(request.PayrollRunId, request.SnapshotId, request.LegalEntityId, request.PeriodStart, request.PeriodEnd, reference, lockedCount, lockedAt);
        }, ct);
    }

    Task<HrPayrollPeriodSnapshotContract> IHumanResourcesPayrollCapability.CreatePeriodSnapshotAsync(TenantId tenantId, HrPayrollPeriodSnapshotRequest request, CancellationToken cancellationToken) =>
        CreateSnapshotAsync(tenantId, request, cancellationToken);

    Task<HrAttendancePeriodLockContract> IHumanResourcesPayrollCapability.LockAttendancePeriodAsync(TenantId tenantId, HrAttendancePeriodLockRequest request, CancellationToken cancellationToken) =>
        LockAttendancePeriodAsync(tenantId, request, cancellationToken);

    public Task<IReadOnlyList<HrPayrollAttendanceCorrectionDelta>> ListAttendanceCorrectionDeltasAsync(TenantId tenantId, Guid snapshotId, CancellationToken cancellationToken)
    {
        if (snapshotId == Guid.Empty) throw new HrException("hr.payroll_snapshot_request_invalid");
        return transactions.ExecuteAsync<IReadOnlyList<HrPayrollAttendanceCorrectionDelta>>(tenantId, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select delta.id,delta.payroll_snapshot_id,delta.employment_id,delta.attendance_record_id,delta.correction_id,delta.payroll_reference,delta.reason_code,delta.created_at,delta.before_snapshot::text,delta.proposed_snapshot::text from business_hr.attendance_payroll_correction_delta delta join business_hr.payroll_period_snapshot snapshot on snapshot.tenant_id=delta.tenant_id and snapshot.id=delta.payroll_snapshot_id and snapshot.legal_entity_id=delta.legal_entity_id where delta.tenant_id=$1 and delta.payroll_snapshot_id=$2 order by delta.created_at,delta.id", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(snapshotId);
            var result = new List<HrPayrollAttendanceCorrectionDelta>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                using var before = JsonDocument.Parse(reader.GetString(8));
                using var proposed = JsonDocument.Parse(reader.GetString(9));
                result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), before.RootElement.Clone(), proposed.RootElement.Clone()));
            }
            return result;
        }, cancellationToken);
    }

    public Task<Guid?> GetSelfEmploymentAsync(TenantId tenantId, Guid membershipId, Guid legalEntityId, CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync<Guid?>(tenantId, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select em.id from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.membership_id=$2 and e.status<>'Archived' and em.legal_entity_id=$3 and em.status in ('Active','Suspended','NoticePeriod') order by em.start_date desc,em.id limit 1", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(membershipId); command.Parameters.AddWithValue(legalEntityId);
            var value = await command.ExecuteScalarAsync(token);
            return value is Guid id ? id : null;
        }, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, (string DisplayName, string EmploymentNumber)>> ResolveEmploymentIdentitiesAsync(TenantId tenantId, Guid legalEntityId, IReadOnlyList<Guid> employmentIds, CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync<IReadOnlyDictionary<Guid, (string DisplayName, string EmploymentNumber)>>(tenantId, async (context, token) =>
        {
            if (employmentIds == null || employmentIds.Count == 0) return new Dictionary<Guid, (string, string)>();
            await using var command = new NpgsqlCommand("select em.id, e.display_name, em.employment_number from business_hr.employment em join business_hr.employee e on e.tenant_id=em.tenant_id and e.id=em.employee_id where em.tenant_id=$1 and em.legal_entity_id=$2 and em.id = any($3)", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(legalEntityId);
            command.Parameters.AddWithValue(employmentIds.ToArray());
            var map = new Dictionary<Guid, (string, string)>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                map[reader.GetGuid(0)] = (reader.GetString(1), reader.GetString(2));
            }
            return map;
        }, cancellationToken);

    private static HrPayrollPeriodSnapshotContract ReadSnapshot(NpgsqlDataReader reader)
    {
        var employments = JsonSerializer.Deserialize<HrPayrollEmploymentInput[]>(reader.GetString(8), JsonOptions) ?? [];
        return new(reader.GetGuid(0), reader.GetInt32(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetFieldValue<DateOnly>(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7), employments);
    }
}
