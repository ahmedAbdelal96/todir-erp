using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

/// <summary>Applies scheduled HR termination facts only when their effective date arrives.</summary>
public sealed class HrOffboardingLifecycleRepository(ITransactionCoordinator transactions, TimeProvider clock)
{
    public Task<int> ApplyDueTerminationsAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (legalEntityId == Guid.Empty || asOfDate == default) throw new HrException("hr.offboarding_scope_invalid");
            var due = new List<ScheduledTermination>();
            await using (var select = new NpgsqlCommand("select c.id,c.employee_id,c.employment_id,c.target_status,c.effective_end_date,c.reason_code,c.created_by,e.status,e.version from business_hr.offboarding_case c join business_hr.employment e on e.tenant_id=c.tenant_id and e.id=c.employment_id and e.legal_entity_id=c.legal_entity_id where c.tenant_id=$1 and c.legal_entity_id=$2 and c.termination_status='Scheduled' and c.effective_end_date<=$3 order by c.effective_end_date,c.id for update of c,e skip locked", context.Connection, context.Transaction))
            {
                select.Parameters.AddWithValue(tenant.Value); select.Parameters.AddWithValue(legalEntityId); select.Parameters.AddWithValue(asOfDate);
                await using var reader = await select.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) due.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.GetGuid(6), reader.GetString(7), reader.GetInt64(8)));
            }

            var applied = 0;
            foreach (var termination in due)
            {
                if (termination.CurrentStatus is not ("PreHire" or "Active" or "Suspended" or "NoticePeriod"))
                    throw new HrException("hr.scheduled_termination_status_conflict");

                await using (var updateEmployment = new NpgsqlCommand("update business_hr.employment set status=$1,status_reason_code=$2,version=version+1 where tenant_id=$3 and id=$4 and legal_entity_id=$5 and status=$6 and version=$7 and start_date<=$8 and end_date=$8", context.Connection, context.Transaction))
                {
                    updateEmployment.Parameters.AddWithValue(termination.TargetStatus); updateEmployment.Parameters.AddWithValue(termination.ReasonCode); updateEmployment.Parameters.AddWithValue(tenant.Value); updateEmployment.Parameters.AddWithValue(termination.EmploymentId); updateEmployment.Parameters.AddWithValue(legalEntityId); updateEmployment.Parameters.AddWithValue(termination.CurrentStatus); updateEmployment.Parameters.AddWithValue(termination.EmploymentVersion); updateEmployment.Parameters.AddWithValue(termination.EffectiveOn);
                    if (await updateEmployment.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.scheduled_termination_concurrency_conflict");
                }

                var affectedContracts = new List<Guid>();
                await using (var endContracts = new NpgsqlCommand("update business_hr.employment_contract c set status=case when c.start_date>$1 then 'CancelledBeforeStart' else 'Ended' end,end_date=case when c.start_date>$1 then c.end_date when c.end_date is null or c.end_date>$1 then $1 else c.end_date end,version=version+1 where c.tenant_id=$2 and c.employment_id=$3 and c.status in ('Draft','Active') and (c.end_date is null or c.end_date>=$1) returning c.id", context.Connection, context.Transaction))
                {
                    endContracts.Parameters.AddWithValue(termination.EffectiveOn); endContracts.Parameters.AddWithValue(tenant.Value); endContracts.Parameters.AddWithValue(termination.EmploymentId);
                    await using var reader = await endContracts.ExecuteReaderAsync(token);
                    while (await reader.ReadAsync(token)) affectedContracts.Add(reader.GetGuid(0));
                }

                var occurredAt = clock.GetUtcNow();
                var correlationId = Guid.CreateVersion7();
                var payload = JsonSerializer.Serialize(new { previousStatus = termination.CurrentStatus, status = termination.TargetStatus, termination.EffectiveOn, termination.ReasonCode, affectedContracts, scheduled = true });
                var eventId = Guid.CreateVersion7();
                await using (var employmentEvent = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'EmploymentTerminated',$5,$6,$7::jsonb,$8,$9)", context.Connection, context.Transaction))
                {
                    employmentEvent.Parameters.AddWithValue(tenant.Value); employmentEvent.Parameters.AddWithValue(eventId); employmentEvent.Parameters.AddWithValue(termination.EmployeeId); employmentEvent.Parameters.AddWithValue(termination.EmploymentId); employmentEvent.Parameters.AddWithValue(termination.EffectiveOn); employmentEvent.Parameters.AddWithValue(termination.CreatedBy); employmentEvent.Parameters.AddWithValue(payload); employmentEvent.Parameters.AddWithValue(correlationId); employmentEvent.Parameters.AddWithValue(occurredAt);
                    await employmentEvent.ExecuteNonQueryAsync(token);
                }
                await using (var timeline = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'EmploymentTerminated',$5,$6,$7::jsonb)", context.Connection, context.Transaction))
                {
                    timeline.Parameters.AddWithValue(tenant.Value); timeline.Parameters.AddWithValue(eventId); timeline.Parameters.AddWithValue(termination.EmployeeId); timeline.Parameters.AddWithValue(termination.EmploymentId); timeline.Parameters.AddWithValue(termination.EffectiveOn); timeline.Parameters.AddWithValue(occurredAt); timeline.Parameters.AddWithValue(payload);
                    await timeline.ExecuteNonQueryAsync(token);
                }
                await using (var updateCase = new NpgsqlCommand("update business_hr.offboarding_case set termination_status='Effective',termination_applied_at=$1,version=version+1 where tenant_id=$2 and id=$3 and termination_status='Scheduled'", context.Connection, context.Transaction))
                {
                    updateCase.Parameters.AddWithValue(occurredAt); updateCase.Parameters.AddWithValue(tenant.Value); updateCase.Parameters.AddWithValue(termination.CaseId);
                    if (await updateCase.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.scheduled_termination_concurrency_conflict");
                }
                await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                    new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "System", null, null, "hr", "employment.termination_effective", "employment", termination.EmploymentId,
                        JsonSerializer.Serialize(new { status = termination.CurrentStatus, version = termination.EmploymentVersion }), payload, null, "hr-scheduler", correlationId, null, occurredAt, null), token);
                applied++;
            }
            return applied;
        }, ct);

    private sealed record ScheduledTermination(Guid CaseId, Guid EmployeeId, Guid EmploymentId, string TargetStatus, DateOnly EffectiveOn,
        string ReasonCode, Guid CreatedBy, string CurrentStatus, long EmploymentVersion);
}
