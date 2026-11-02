using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrAdjustmentRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock, IPlatformFileReferenceCapability files)
{
    private static readonly HashSet<string> AllowanceTypes = ["Transport", "Housing", "Meal", "Communication", "Responsibility", "TemporaryAssignment", "Other"];
    private static readonly HashSet<string> DisciplinaryTypes = ["Warning", "WrittenWarning", "AttendancePenalty", "Suspension", "FinancialPenalty", "Other"];

    public async Task<HrAdjustmentView> CreateAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateHrAdjustmentRequest request, Guid correlationId, CancellationToken ct)
    {
        var kind = request.Kind?.Trim(); var type = request.AdjustmentType?.Trim(); var recurrence = request.Recurrence?.Trim(); var impact = request.PayrollImpact?.Trim();
        var reasonCode = request.ReasonCode?.Trim(); var reason = request.Reason?.Trim(); var currency = string.IsNullOrWhiteSpace(request.CurrencyCode) ? null : request.CurrencyCode.Trim().ToUpperInvariant();
        if (kind is not ("Allowance" or "DisciplinaryAction") || string.IsNullOrWhiteSpace(type)
            || (kind == "Allowance" ? !AllowanceTypes.Contains(type) : !DisciplinaryTypes.Contains(type))
            || request.EffectiveFrom == default || request.EffectiveTo is { } effectiveTo && effectiveTo < request.EffectiveFrom
            || recurrence is not ("OneTime" or "Recurring" or "Informational") || request.Amount is < 0 || currency is not null && (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            || string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 80 || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new HrException("hr.adjustment_invalid");
        HrAdjustmentRules.ValidatePayrollImpact(kind!, impact!, recurrence!, request.Amount, currency);
        if (request.EvidenceFileRef is { } evidence && await files.VerifyReadyReferenceAsync(tenant, evidence, "hr", "employee", employeeId, ct) is null)
            throw new HrException("hr.adjustment_evidence_not_ready");

        return await transactions.ExecuteAsync<HrAdjustmentView>(tenant, async (context, token) =>
        {
            await using (var employment = new NpgsqlCommand("select 1 from business_hr.employment where tenant_id=$1 and id=$2 and employee_id=$3 and legal_entity_id=$4", context.Connection, context.Transaction))
            {
                employment.Parameters.AddWithValue(tenant.Value); employment.Parameters.AddWithValue(employmentId); employment.Parameters.AddWithValue(employeeId); employment.Parameters.AddWithValue(request.LegalEntityId);
                if (await employment.ExecuteScalarAsync(token) is null) throw new HrException("hr.employment_not_found");
            }
            var id = Guid.CreateVersion7(); var createdAt = clock.GetUtcNow();
            await using (var insert = new NpgsqlCommand("insert into business_hr.hr_adjustment(id,tenant_id,employee_id,employment_id,legal_entity_id,kind,adjustment_type,effective_from,effective_to,recurrence,payroll_impact,amount,currency_code,reason_code,reason,evidence_file_ref,status,version,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,'Draft',1,$17,$18)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(kind!); insert.Parameters.AddWithValue(type!); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(recurrence!); insert.Parameters.AddWithValue(impact!); insert.Parameters.AddWithValue((object?)request.Amount ?? DBNull.Value); insert.Parameters.AddWithValue((object?)currency ?? DBNull.Value); insert.Parameters.AddWithValue(reasonCode!); insert.Parameters.AddWithValue(reason!); insert.Parameters.AddWithValue((object?)request.EvidenceFileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(createdAt); await insert.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.adjustment.created", "hr_adjustment", id, null, JsonSerializer.Serialize(new { employeeId, employmentId, kind, type, request.EffectiveFrom, request.EffectiveTo, recurrence, impact, request.Amount, currency, reasonCode, evidenceFileRef = request.EvidenceFileRef }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), createdAt, null), token);
            return new(id, employeeId, employmentId, kind!, type!, request.EffectiveFrom, request.EffectiveTo, recurrence!, impact!, request.Amount, currency, reasonCode!, reason!, request.EvidenceFileRef, "Draft", 1, createdAt, null, null, null);
        }, ct);
    }

    public async Task<IReadOnlyList<HrAdjustmentView>> ListAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,employee_id,employment_id,kind,adjustment_type,effective_from,effective_to,recurrence,payroll_impact,amount,currency_code,reason_code,reason,evidence_file_ref,status,version,created_at,approved_by,approved_at,decision_reason from business_hr.hr_adjustment where tenant_id=$1 and employment_id=$2 and legal_entity_id=$3 order by effective_from desc,id desc limit 500", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<HrAdjustmentView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public Task<HrAdjustmentView> SubmitAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, Guid correlationId, CancellationToken ct) => TransitionAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, "Submitted", null, correlationId, ct);
    public Task<HrAdjustmentView> ApproveAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, Guid correlationId, CancellationToken ct) => TransitionAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, "Approved", null, correlationId, ct);
    public Task<HrAdjustmentView> RejectAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, string reason, Guid correlationId, CancellationToken ct) => TransitionAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, "Rejected", reason, correlationId, ct);
    public Task<HrAdjustmentView> CancelAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, string reason, Guid correlationId, CancellationToken ct) => TransitionAsync(tenant, actor, membershipId, id, legalEntityId, expectedVersion, "Cancelled", reason, correlationId, ct);

    private async Task<HrAdjustmentView> TransitionAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, long expectedVersion, string target, string? decisionReason, Guid correlationId, CancellationToken ct)
    {
        var normalizedReason = decisionReason?.Trim();
        if (expectedVersion <= 0 || target is "Rejected" or "Cancelled" && (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)) throw new HrException("hr.adjustment_decision_invalid");
        return await transactions.ExecuteAsync<HrAdjustmentView>(tenant, async (context, token) =>
        {
            HrAdjustmentView current;
            await using (var read = new NpgsqlCommand("select id,employee_id,employment_id,kind,adjustment_type,effective_from,effective_to,recurrence,payroll_impact,amount,currency_code,reason_code,reason,evidence_file_ref,status,version,created_at,approved_by,approved_at,decision_reason from business_hr.hr_adjustment where tenant_id=$1 and id=$2 and legal_entity_id=$3 for update", context.Connection, context.Transaction))
            {
                read.Parameters.AddWithValue(tenant.Value); read.Parameters.AddWithValue(id); read.Parameters.AddWithValue(legalEntityId); await using var reader = await read.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.adjustment_not_found"); current = Read(reader);
            }
            if (current.Version != expectedVersion) throw new HrException("hr.adjustment_concurrency_conflict");
            if (target == "Submitted" && current.Status != "Draft" || target is "Approved" or "Rejected" && current.Status != "Submitted" || target == "Cancelled" && current.Status is not ("Draft" or "Submitted")) throw new HrException("hr.adjustment_transition_invalid");
            if (target == "Approved" && current.PayrollImpact == "SendToPayroll" && current.Kind != "Allowance") throw new HrException("hr.disciplinary_payroll_impact_not_configured");
            if (target == "Approved" && current.PayrollImpact == "SendToPayroll" && current.CreatedAt is { } && current.Status == "Submitted")
            {
                await using var submitter = new NpgsqlCommand("select submitted_by from business_hr.hr_adjustment where tenant_id=$1 and id=$2 and legal_entity_id=$3", context.Connection, context.Transaction);
                submitter.Parameters.AddWithValue(tenant.Value); submitter.Parameters.AddWithValue(id); submitter.Parameters.AddWithValue(legalEntityId);
                if ((Guid?)await submitter.ExecuteScalarAsync(token) == actor) throw new HrException("hr.adjustment_separation_of_duties_required");
            }
            var now = clock.GetUtcNow(); var approvedBy = target is "Approved" or "Rejected" ? (object)actor : DBNull.Value; var approvedAt = target is "Approved" or "Rejected" ? (object)now : DBNull.Value;
            await using (var update = new NpgsqlCommand("update business_hr.hr_adjustment set status=$5,version=version+1,submitted_by=case when $5='Submitted' then $6 else submitted_by end,submitted_at=case when $5='Submitted' then $7 else submitted_at end,approved_by=$8,approved_at=$9,decision_reason=$10 where tenant_id=$1 and id=$2 and legal_entity_id=$3 and version=$4", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(legalEntityId); update.Parameters.AddWithValue(expectedVersion); update.Parameters.AddWithValue(target); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(now); update.Parameters.AddWithValue(approvedBy); update.Parameters.AddWithValue(approvedAt); update.Parameters.AddWithValue((object?)normalizedReason ?? DBNull.Value);
                if (await update.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.adjustment_concurrency_conflict");
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", $"hr.adjustment.{target.ToLowerInvariant()}", "hr_adjustment", id, null, JsonSerializer.Serialize(new { expectedVersion, target, decisionReason = normalizedReason }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return current with { Status = target, Version = current.Version + 1, ApprovedBy = target is "Approved" or "Rejected" ? actor : null, ApprovedAt = target is "Approved" or "Rejected" ? now : null, DecisionReason = normalizedReason };
        }, ct);
    }

    private static HrAdjustmentView Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetDecimal(9), reader.IsDBNull(10) ? null : reader.GetString(10)?.Trim(), reader.GetString(11), reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetGuid(13), reader.GetString(14), reader.GetInt64(15), reader.GetFieldValue<DateTimeOffset>(16), reader.IsDBNull(17) ? null : reader.GetGuid(17), reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18), reader.IsDBNull(19) ? null : reader.GetString(19));

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct);
    }
}
