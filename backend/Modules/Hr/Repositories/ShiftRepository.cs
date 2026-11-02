using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class ShiftRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<ShiftDefinitionView> CreateDefinitionAsync(TenantId tenant, Guid actor, Guid membershipId, CreateShiftDefinitionRequest request, Guid correlationId, CancellationToken ct)
    {
        var code = request.Code?.Trim().ToUpperInvariant(); var name = request.NameAr?.Trim(); var nameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40 || string.IsNullOrWhiteSpace(name) || name.Length > 160 || nameEn?.Length > 160)
            throw new HrException("hr.shift_definition_invalid");
        ShiftScheduleRules.ValidateAndGetDurationMinutes(request.StartLocal, request.EndLocal, request.IsOvernight, request.BreakMinutes, request.GraceMinutes);
        return await transactions.ExecuteAsync<ShiftDefinitionView>(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7();
            await using var command = new NpgsqlCommand("insert into business_hr.shift_definition(tenant_id,id,legal_entity_id,code,name_ar,name_en,start_local,end_local,is_overnight,break_minutes,grace_minutes,status,version,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Active',1,$12,$13)", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(request.LegalEntityId); command.Parameters.AddWithValue(code); command.Parameters.AddWithValue(name); command.Parameters.AddWithValue((object?)nameEn ?? DBNull.Value); command.Parameters.AddWithValue(request.StartLocal); command.Parameters.AddWithValue(request.EndLocal); command.Parameters.AddWithValue(request.IsOvernight); command.Parameters.AddWithValue(request.BreakMinutes); command.Parameters.AddWithValue(request.GraceMinutes); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(clock.GetUtcNow()); await command.ExecuteNonQueryAsync(token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.shift.created", "shift_definition", id, null, JsonSerializer.Serialize(new { code, name, nameEn, request.StartLocal, request.EndLocal, request.IsOvernight, request.BreakMinutes, request.GraceMinutes }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, request.LegalEntityId, code, name, nameEn, request.StartLocal, request.EndLocal, request.IsOvernight, request.BreakMinutes, request.GraceMinutes, "Active", 1);
        }, ct);
    }

    public async Task<IReadOnlyList<ShiftDefinitionView>> ListDefinitionsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,code,name_ar,name_en,start_local,end_local,is_overnight,break_minutes,grace_minutes,status,version from business_hr.shift_definition where tenant_id=$1 and legal_entity_id=$2 order by code,id limit 200", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<ShiftDefinitionView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<TimeOnly>(5), reader.GetFieldValue<TimeOnly>(6), reader.GetBoolean(7), reader.GetInt16(8), reader.GetInt16(9), reader.GetString(10), reader.GetInt64(11)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<ShiftAssignmentView> AssignAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, AssignShiftRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.EffectiveFrom == default || request.EffectiveTo is { } end && end < request.EffectiveFrom) throw new HrException("hr.shift_assignment_dates_invalid");
        return await transactions.ExecuteAsync<ShiftAssignmentView>(tenant, async (context, token) =>
        {
            await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || $2::text || ':shift',0))", context.Connection, context.Transaction))
            {
                lockCommand.Parameters.AddWithValue(tenant.Value); lockCommand.Parameters.AddWithValue(employmentId); await lockCommand.ExecuteNonQueryAsync(token);
            }
            string name; TimeOnly start; TimeOnly finish; bool overnight; short breakMinutes; short graceMinutes; DateOnly employmentStart; DateOnly? employmentEnd;
            await using (var shift = new NpgsqlCommand("select s.name_ar,s.start_local,s.end_local,s.is_overnight,s.break_minutes,s.grace_minutes,e.start_date,e.end_date from business_hr.shift_definition s join business_hr.employment e on e.tenant_id=s.tenant_id and e.legal_entity_id=s.legal_entity_id where s.tenant_id=$1 and s.id=$2 and s.legal_entity_id=$3 and s.status='Active' and e.id=$4 and e.status in ('Active','Suspended','NoticePeriod')", context.Connection, context.Transaction))
            {
                shift.Parameters.AddWithValue(tenant.Value); shift.Parameters.AddWithValue(request.ShiftDefinitionId); shift.Parameters.AddWithValue(request.LegalEntityId); shift.Parameters.AddWithValue(employmentId);
                await using var reader = await shift.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new HrException("hr.shift_or_employment_not_found");
                name = reader.GetString(0); start = reader.GetFieldValue<TimeOnly>(1); finish = reader.GetFieldValue<TimeOnly>(2); overnight = reader.GetBoolean(3); breakMinutes = reader.GetInt16(4); graceMinutes = reader.GetInt16(5); employmentStart = reader.GetFieldValue<DateOnly>(6); employmentEnd = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7);
            }
            if (request.EffectiveFrom < employmentStart || employmentEnd is { } finalDate && request.EffectiveFrom > finalDate || request.EffectiveTo is { } assignmentEnd && employmentEnd is { } employmentFinal && assignmentEnd > employmentFinal)
                throw new HrException("hr.shift_assignment_outside_employment");
            Guid? endedAssignmentId = null;
            await using (var closePrior = new NpgsqlCommand("update business_hr.shift_assignment set effective_to=$3 where tenant_id=$1 and employment_id=$2 and effective_from<$3 and effective_to is null returning id", context.Connection, context.Transaction))
            {
                closePrior.Parameters.AddWithValue(tenant.Value); closePrior.Parameters.AddWithValue(employmentId); closePrior.Parameters.AddWithValue(request.EffectiveFrom);
                endedAssignmentId = (Guid?)await closePrior.ExecuteScalarAsync(token);
            }
            await using (var overlap = new NpgsqlCommand("select exists(select 1 from business_hr.shift_assignment where tenant_id=$1 and employment_id=$2 and $3<coalesce(effective_to,'infinity'::date) and effective_from<coalesce($4::date,'infinity'::date))", context.Connection, context.Transaction))
            {
                overlap.Parameters.AddWithValue(tenant.Value); overlap.Parameters.AddWithValue(employmentId); overlap.Parameters.AddWithValue(request.EffectiveFrom); overlap.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
                if ((bool)(await overlap.ExecuteScalarAsync(token) ?? false)) throw new HrException("hr.shift_assignment_overlap");
            }
            var id = Guid.CreateVersion7();
            await using (var insert = new NpgsqlCommand("insert into business_hr.shift_assignment(tenant_id,id,legal_entity_id,employment_id,shift_definition_id,effective_from,effective_to,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(request.ShiftDefinitionId); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); await insert.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.shift.assigned", "shift_assignment", id, null, JsonSerializer.Serialize(new { employmentId, request.ShiftDefinitionId, request.EffectiveFrom, request.EffectiveTo, endedAssignmentId }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employmentId, request.ShiftDefinitionId, request.EffectiveFrom, request.EffectiveTo, name, start, finish, overnight, breakMinutes, graceMinutes);
        }, ct);
    }

    public async Task<IReadOnlyList<ShiftAssignmentView>> ListAssignmentsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select a.id,a.employment_id,a.shift_definition_id,a.effective_from,a.effective_to,s.name_ar,s.start_local,s.end_local,s.is_overnight,s.break_minutes,s.grace_minutes from business_hr.shift_assignment a join business_hr.shift_definition s on s.tenant_id=a.tenant_id and s.id=a.shift_definition_id and s.legal_entity_id=a.legal_entity_id where a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 order by a.effective_from desc,a.id limit 100", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<ShiftAssignmentView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.GetFieldValue<TimeOnly>(6), reader.GetFieldValue<TimeOnly>(7), reader.GetBoolean(8), reader.GetInt16(9), reader.GetInt16(10)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct);
    }
}
