using System.Text.Json;
using System.Globalization;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class LeaveRequestRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<LeaveRequestView> CreateAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateLeaveRequest request, Guid correlationId, CancellationToken ct) =>
        await transactions.ExecuteAsync<LeaveRequestView>(tenant, async (context, token) =>
        {
            var idempotencyKey = request.IdempotencyKey?.Trim();
            if (request.From == default || request.To < request.From || request.To.DayNumber - request.From.DayNumber > 366 || request.Reason?.Length > 1000 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 160)
                throw new HrException("hr.leave_request_invalid");
            await LockIdempotencyKeyAsync(context.Connection, context.Transaction, tenant, idempotencyKey, token);

            await using (var duplicate = new NpgsqlCommand("select id,employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,status,version,reason,created_at,submitted_at,decided_by,decided_at,decision_reason from business_hr.leave_request where tenant_id=$1 and idempotency_key=$2", context.Connection, context.Transaction))
            {
                duplicate.Parameters.AddWithValue(tenant.Value); duplicate.Parameters.AddWithValue(idempotencyKey);
                await using var reader = await duplicate.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var existing = ReadRequest(reader);
                    if (existing.EmploymentId != employmentId || existing.LeaveTypeId != request.LeaveTypeId || existing.From != request.From || existing.To != request.To || request.RequestedQuantity is { } amount && amount != existing.ChargedQuantity || !string.Equals(existing.Reason, request.Reason, StringComparison.Ordinal))
                        throw new HrException("hr.leave_request_idempotency_conflict");
                    return existing;
                }
            }

            var now = clock.GetUtcNow();
            Guid employeeId; Guid policyId; int policyVersion; string unit; string counting; bool approvalRequired; bool partialDay; string negativePolicy; decimal negativeLimit;
            await using (var policy = new NpgsqlCommand("""
                select e.employee_id,p.id,p.version,t.unit,p.counting_method,p.approval_required,p.partial_day_allowed,p.negative_balance_policy,p.negative_balance_limit,p.accrual_rule->>'mode'
                from business_hr.employment e
                join business_hr.leave_type t on t.tenant_id=e.tenant_id and t.legal_entity_id=e.legal_entity_id and t.id=$3 and t.status='Active'
                join business_hr.leave_policy p on p.tenant_id=t.tenant_id and p.legal_entity_id=t.legal_entity_id and p.leave_type_id=t.id and $4 between p.effective_from and coalesce(p.effective_to,'infinity'::date) and $6<=coalesce(p.effective_to,'infinity'::date)
                where e.tenant_id=$1 and e.id=$2 and e.legal_entity_id=$5 and e.status='Active' and
                (jsonb_array_length(coalesce(p.eligibility_rules->'employmentTypes','[]'::jsonb))=0 or p.eligibility_rules->'employmentTypes' ? e.employment_type::text)
                order by p.version desc limit 1
                """, context.Connection, context.Transaction))
            {
                policy.Parameters.AddWithValue(tenant.Value); policy.Parameters.AddWithValue(employmentId); policy.Parameters.AddWithValue(request.LeaveTypeId); policy.Parameters.AddWithValue(request.From); policy.Parameters.AddWithValue(request.LegalEntityId); policy.Parameters.AddWithValue(request.To);
                await using var reader = await policy.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.leave_policy_not_found");
                employeeId = reader.GetGuid(0); policyId = reader.GetGuid(1); policyVersion = reader.GetInt32(2); unit = reader.GetString(3); counting = reader.GetString(4); approvalRequired = reader.GetBoolean(5); partialDay = reader.GetBoolean(6); negativePolicy = reader.GetString(7); negativeLimit = reader.GetDecimal(8);
            }

            var chargeDates = unit == "Hours" ? new List<DateOnly> { request.From } : await GetChargeDatesAsync(context.Connection, context.Transaction, tenant, employmentId, request.LegalEntityId, request.From, request.To, counting, token);
            if (unit == "Hours" && request.From != request.To) throw new HrException("hr.hourly_leave_must_be_single_day");
            var fullQuantity = unit == "Hours" ? (request.RequestedQuantity ?? 0m) : chargeDates.Count;
            var requested = LeavePolicyRules.ValidateRequestedQuantity(unit, partialDay, request.RequestedQuantity, fullQuantity);
            if (unit == "Days" && requested != fullQuantity && chargeDates.Count != 1)
                throw new HrException("hr.partial_leave_must_be_single_day");

            await LockBalanceAsync(context.Connection, context.Transaction, tenant, employmentId, request.LeaveTypeId, token);
            var available = await GetBalanceAsync(context.Connection, context.Transaction, tenant, employmentId, request.LeaveTypeId, request.From, true, token);
            LeavePolicyRules.EnsureBalanceAllowsRequest(negativePolicy, negativeLimit, available, requested);
            if (negativePolicy == "RequireApproval" && available < requested && !approvalRequired) throw new HrException("hr.leave_approval_required_for_negative_balance");

            var id = Guid.CreateVersion7(); var status = approvalRequired ? "Pending" : "Approved"; var version = 1L;
            await using (var insert = new NpgsqlCommand("insert into business_hr.leave_request(id,tenant_id,legal_entity_id,employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,reason,status,version,idempotency_key,submitted_at,decided_by,decided_at,decision_reason,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$20)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(request.LeaveTypeId); insert.Parameters.AddWithValue(policyId); insert.Parameters.AddWithValue(request.From); insert.Parameters.AddWithValue(request.To); insert.Parameters.AddWithValue(requested); insert.Parameters.AddWithValue((object?)request.Reason ?? DBNull.Value); insert.Parameters.AddWithValue(status); insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(idempotencyKey); insert.Parameters.AddWithValue(now); insert.Parameters.AddWithValue(approvalRequired ? DBNull.Value : actor); insert.Parameters.AddWithValue(approvalRequired ? DBNull.Value : now); insert.Parameters.AddWithValue(approvalRequired ? DBNull.Value : "Policy does not require approval"); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
            }
            foreach (var date in chargeDates)
            {
                var chargedQuantity = unit == "Hours" ? requested : requested == fullQuantity ? 1m : requested;
                await using var charge = new NpgsqlCommand("insert into business_hr.leave_request_charge(id,tenant_id,legal_entity_id,leave_request_id,employment_id,leave_type_id,charge_date,quantity,unit,policy_version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", context.Connection, context.Transaction);
                charge.Parameters.AddWithValue(Guid.CreateVersion7()); charge.Parameters.AddWithValue(tenant.Value); charge.Parameters.AddWithValue(request.LegalEntityId); charge.Parameters.AddWithValue(id); charge.Parameters.AddWithValue(employmentId); charge.Parameters.AddWithValue(request.LeaveTypeId); charge.Parameters.AddWithValue(date); charge.Parameters.AddWithValue(chargedQuantity); charge.Parameters.AddWithValue(unit); charge.Parameters.AddWithValue(policyVersion); await charge.ExecuteNonQueryAsync(token);
            }
            if (approvalRequired)
                await InsertAllocatedConsumptionAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, employmentId, request.LeaveTypeId, request.From, requested, "RequestReservation", "LeaveRequest", id.ToString("D"), $"leave-request:{id:D}:reserve", policyVersion, "Leave request reservation", actor, now, token);
            else
                await InsertAllocatedConsumptionAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, employmentId, request.LeaveTypeId, request.From, requested, "LeaveTaken", "LeaveRequest", id.ToString("D"), $"leave-request:{id:D}:taken", policyVersion, "Approved by policy without a separate approval step", actor, now, token);
            await InsertEventAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, id, "Created", null, status, actor, request.Reason, now, token);
            if (status == "Pending") await InsertEventAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, id, "Submitted", "Draft", status, actor, null, now, token);
            else await InsertEventAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, id, "Approved", "Draft", status, actor, "Policy does not require approval", now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.leave_request.created", "leave_request", id, null, JsonSerializer.Serialize(new { request.LeaveTypeId, request.From, request.To, quantity = requested, status }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return new(id, employeeId, employmentId, request.LeaveTypeId, policyId, request.From, request.To, requested, status, version, request.Reason, now, now, status == "Approved" ? actor : null, status == "Approved" ? now : null, status == "Approved" ? "Policy does not require approval" : null);
        }, ct);

    public async Task<LeaveRequestView> DecideAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, LeaveRequestDecision decision, bool approve, Guid correlationId, CancellationToken ct, Guid? managerMembershipId = null) =>
        await transactions.ExecuteAsync<LeaveRequestView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("select employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,status,version,reason,created_at,submitted_at from business_hr.leave_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(id);
            Guid employeeId; Guid employmentId; Guid leaveTypeId; Guid policyId; DateOnly from; DateOnly to; decimal quantity; string status; long version; string? reason; DateTimeOffset createdAt; DateTimeOffset? submittedAt;
            await using (var reader = await command.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new HrException("hr.leave_request_not_found");
                employeeId = reader.GetGuid(0); employmentId = reader.GetGuid(1); leaveTypeId = reader.GetGuid(2); policyId = reader.GetGuid(3); from = reader.GetFieldValue<DateOnly>(4); to = reader.GetFieldValue<DateOnly>(5); quantity = reader.GetDecimal(6); status = reader.GetString(7); version = reader.GetInt64(8); reason = reader.IsDBNull(9) ? null : reader.GetString(9); createdAt = reader.GetFieldValue<DateTimeOffset>(10); submittedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11);
            }
            if (version != decision.ExpectedVersion) throw new HrException("hr.leave_request_concurrency");
            var now = clock.GetUtcNow();
            if (managerMembershipId is { } managerMembership)
            {
                await using (var hierarchyLock = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || $2::text || ':manager-hierarchy',0))", context.Connection, context.Transaction))
                {
                    hierarchyLock.Parameters.AddWithValue(tenant.Value); hierarchyLock.Parameters.AddWithValue(legalEntityId); await hierarchyLock.ExecuteNonQueryAsync(token);
                }
                await using var managerScope = new NpgsqlCommand("""
                    select exists(
                        select 1
                        from business_hr.employee manager_employee
                        join business_hr.employment manager_employment on manager_employment.tenant_id=manager_employee.tenant_id and manager_employment.employee_id=manager_employee.id
                        join business_hr.employment_assignment assignment on assignment.tenant_id=manager_employment.tenant_id and assignment.manager_employment_id=manager_employment.id and assignment.legal_entity_id=$3
                        join business_hr.employment report_employment on report_employment.tenant_id=assignment.tenant_id and report_employment.id=assignment.employment_id
                        where manager_employee.tenant_id=$1 and manager_employee.membership_id=$2 and manager_employment.legal_entity_id=$3
                            and manager_employment.status='Active' and manager_employment.start_date<=$4 and (manager_employment.end_date is null or manager_employment.end_date>=$4)
                            and report_employment.id=$5 and report_employment.status='Active'
                            and assignment.effective_from<=$4 and (assignment.effective_to is null or assignment.effective_to>$4)
                    )
                    """, context.Connection, context.Transaction);
                managerScope.Parameters.AddWithValue(tenant.Value); managerScope.Parameters.AddWithValue(managerMembership); managerScope.Parameters.AddWithValue(legalEntityId); managerScope.Parameters.AddWithValue(DateOnly.FromDateTime(now.UtcDateTime)); managerScope.Parameters.AddWithValue(employmentId);
                if ((bool)(await managerScope.ExecuteScalarAsync(token) ?? false) is false) throw new HrException("hr.manager_relationship_required");
            }
            string next; string eventType;
            if (approve && status == "Pending") { next = "Approved"; eventType = "Approved"; }
            else if (!approve && status == "Pending") { next = "Rejected"; eventType = "Rejected"; }
            else if (!approve && status is "Approved" or "Taken") { next = "Cancelled"; eventType = "Cancelled"; }
            else throw new HrException("hr.leave_request_transition_invalid");

            await LockBalanceAsync(context.Connection, context.Transaction, tenant, employmentId, leaveTypeId, token);
            if (status == "Pending")
            {
                var reservations = await ReadAllocatedMovementsAsync(context.Connection, context.Transaction, tenant, id, "RequestReservation", token);
                if (reservations.Count == 0)
                    await InsertMovementAsync(context.Connection, context.Transaction, tenant, legalEntityId, employmentId, leaveTypeId, from, "CancellationRestore", quantity, "LeaveRequest", id.ToString("D"), $"leave-request:{id:D}:reservation-release", null, decision.Reason ?? (approve ? "Reservation converted to approved leave" : "Rejected leave request reservation released"), actor, now, token);
                else
                    foreach (var reservation in reservations)
                        await InsertAllocatedReversalAsync(context.Connection, context.Transaction, tenant, legalEntityId, employmentId, leaveTypeId, from, reservation, $"leave-request:{id:D}:reservation-release", decision.Reason ?? (approve ? "Reservation converted to approved leave" : "Rejected leave request reservation released"), actor, now, token);
                if (approve) await InsertAllocatedConsumptionAsync(context.Connection, context.Transaction, tenant, legalEntityId, employmentId, leaveTypeId, from, quantity, "LeaveTaken", "LeaveRequest", id.ToString("D"), $"leave-request:{id:D}:taken", null, "Approved leave consumed", actor, now, token);
            }
            else
            {
                var taken = await ReadAllocatedMovementsAsync(context.Connection, context.Transaction, tenant, id, "LeaveTaken", token);
                if (taken.Count == 0)
                    await InsertMovementAsync(context.Connection, context.Transaction, tenant, legalEntityId, employmentId, leaveTypeId, from, "CancellationRestore", quantity, "LeaveRequest", id.ToString("D"), $"leave-request:{id:D}:cancel-restore", null, decision.Reason ?? "Approved leave cancelled", actor, now, token);
                else
                    foreach (var consumption in taken)
                        await InsertAllocatedReversalAsync(context.Connection, context.Transaction, tenant, legalEntityId, employmentId, leaveTypeId, from, consumption, $"leave-request:{id:D}:cancel-restore", decision.Reason ?? "Approved leave cancelled", actor, now, token);
            }
            await using (var update = new NpgsqlCommand("update business_hr.leave_request set status=$1,version=version+1,decided_by=$2,decided_at=$3,decision_reason=$4,updated_at=$3 where tenant_id=$5 and id=$6 and version=$7", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(next); update.Parameters.AddWithValue(actor); update.Parameters.AddWithValue(now); update.Parameters.AddWithValue((object?)decision.Reason ?? DBNull.Value); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(id); update.Parameters.AddWithValue(version); if (await update.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.leave_request_concurrency");
            }
            await InsertEventAsync(context.Connection, context.Transaction, tenant, legalEntityId, id, eventType, status, next, actor, decision.Reason, now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", $"hr.leave_request.{eventType.ToLowerInvariant()}", "leave_request", id, JsonSerializer.Serialize(new { status, version }), JsonSerializer.Serialize(new { status = next, version = version + 1, decision.Reason }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return new(id, employeeId, employmentId, leaveTypeId, policyId, from, to, quantity, next, version + 1, reason, createdAt, submittedAt, actor, now, decision.Reason);
        }, ct);

    public async Task<IReadOnlyList<LeaveRequestView>> ListRequestsAsync(TenantId tenant, Guid legalEntityId, Guid? employmentId, string? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select id,employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,status,version,reason,created_at,submitted_at,decided_by,decided_at,decision_reason from business_hr.leave_request where tenant_id=$1 and legal_entity_id=$2 and ($3::uuid is null or employment_id=$3) and ($4::text is null or status=$4) order by submitted_at desc nulls last,id desc offset $5 limit $6", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue((object?)employmentId ?? DBNull.Value); command.Parameters.AddWithValue((object?)status ?? DBNull.Value); command.Parameters.AddWithValue((page - 1) * pageSize); command.Parameters.AddWithValue(pageSize);
        var rows = new List<LeaveRequestView>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) rows.Add(ReadRequest(reader)); await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<LeaveBalanceView> GetBalanceAsync(TenantId tenant, Guid employmentId, Guid leaveTypeId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        var balance = await GetBalanceAsync(connection, tx, tenant, employmentId, leaveTypeId, null, false, ct);
        await using var command = new NpgsqlCommand("select id,effective_date,movement_type,quantity,source_type,source_reference,reason,created_at,expires_on from business_hr.leave_balance_movement where tenant_id=$1 and legal_entity_id=$2 and employment_id=$3 and leave_type_id=$4 order by effective_date desc,id desc limit 50", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(leaveTypeId); var movements = new List<LeaveBalanceMovementView>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) movements.Add(new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8))); await reader.CloseAsync(); await tx.CommitAsync(ct); return new(employmentId, leaveTypeId, balance, movements);
    }

    public async Task<HrLeaveOpeningImportSummary> PreviewLeaveOpeningsAsync(TenantId tenant, HrLeaveOpeningImportRequest request, CancellationToken ct)
    {
        var validation = ValidateLeaveOpeningImport(request);
        if (validation.Values.All(issues => issues.Count > 0)) return RejectedLeaveOpeningImport(request, validation);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var results = new List<HrLeaveOpeningImportRowResult>(request.Rows.Count);
            foreach (var row in request.Rows)
            {
                var rowRef = row.RowReference ?? string.Empty;
                var issues = validation.GetValueOrDefault(rowRef, []).ToList();
                if (issues.Count > 0)
                {
                    results.Add(new(rowRef, "Rejected", null, issues));
                    continue;
                }

                var rowHash = LeaveOpeningRowHash(row);
                await using (var existing = new NpgsqlCommand("select movement_id,row_payload_sha256 from business_hr.leave_import_reference where tenant_id=$1 and source_system=$2 and batch_reference=$3 and row_reference=$4", context.Connection, context.Transaction))
                {
                    existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(request.BatchReference.Trim()); existing.Parameters.AddWithValue(rowRef.Trim());
                    await using var reader = await existing.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
                    if (await reader.ReadAsync(token))
                    {
                        var movementId = reader.GetGuid(0); var existingHash = reader.GetString(1);
                        results.Add(existingHash == rowHash ? new(rowRef, "AlreadyImported", movementId, []) : new(rowRef, "Rejected", null, ["hr.leave_import_row_conflict"]));
                        continue;
                    }
                }

                var goLiveConfiguration = await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, false, token);
                if (goLiveConfiguration is null) issues.Add("hr.go_live_configuration_required");
                else if (goLiveConfiguration.LeaveOpeningBoundary is null) issues.Add("hr.leave_opening_boundary_not_configured");
                else if (goLiveConfiguration.LeaveOpeningBoundary != row.EffectiveDate) issues.Add("hr.leave_opening_boundary_mismatch");
                if (issues.Count != 0)
                {
                    results.Add(new(rowRef, "Rejected", null, issues));
                    continue;
                }

                var references = await ResolveLeaveOpeningReferencesAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                if (references.EmploymentId is null) issues.Add("hr.import_employment_not_found");
                if (references.LeaveTypeId is null) issues.Add("hr.import_leave_type_not_found");
                if (references.EmploymentId is { } employmentId && references.LeaveTypeId is { } leaveTypeId)
                {
                    await using var opening = new NpgsqlCommand("select exists(select 1 from business_hr.leave_balance_movement where tenant_id=$1 and employment_id=$2 and leave_type_id=$3 and movement_type='Opening')", context.Connection, context.Transaction);
                    opening.Parameters.AddWithValue(tenant.Value); opening.Parameters.AddWithValue(employmentId); opening.Parameters.AddWithValue(leaveTypeId);
                    if ((bool)(await opening.ExecuteScalarAsync(token) ?? false)) issues.Add("hr.leave_opening_already_exists");
                }
                results.Add(issues.Count == 0 ? new(rowRef, "Ready", null, []) : new(rowRef, "Rejected", null, issues));
            }
            return SummarizeLeaveOpeningImport(results);
        }, ct);
    }

    public async Task<HrLeaveOpeningImportSummary> CommitLeaveOpeningsAsync(TenantId tenant, Guid actor, Guid membershipId, Guid correlationId,
        HrLeaveOpeningImportRequest request, CancellationToken ct)
    {
        var validation = ValidateLeaveOpeningImport(request);
        if (validation.Values.All(issues => issues.Count > 0)) return RejectedLeaveOpeningImport(request, validation);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var results = new List<HrLeaveOpeningImportRowResult>(request.Rows.Count);
            var now = clock.GetUtcNow();
            foreach (var row in request.Rows)
            {
                var rowRef = row.RowReference ?? string.Empty;
                var issues = validation.GetValueOrDefault(rowRef, []).ToList();
                if (issues.Count > 0)
                {
                    results.Add(new(rowRef, "Rejected", null, issues));
                    continue;
                }

                await context.Transaction.SaveAsync("hr_leave_opening_row", token);
                try
                {
                    var rowHash = LeaveOpeningRowHash(row);
                    // Import provenance is immutable; the unique key arbitrates concurrent commits without requiring UPDATE privileges.
                    await using (var existing = new NpgsqlCommand("select movement_id,row_payload_sha256 from business_hr.leave_import_reference where tenant_id=$1 and source_system=$2 and batch_reference=$3 and row_reference=$4", context.Connection, context.Transaction))
                    {
                        existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(request.SourceSystem.Trim()); existing.Parameters.AddWithValue(request.BatchReference.Trim()); existing.Parameters.AddWithValue(rowRef.Trim());
                        await using var reader = await existing.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
                        if (await reader.ReadAsync(token))
                        {
                            var movementId = reader.GetGuid(0); var existingHash = reader.GetString(1);
                            if (existingHash != rowHash) results.Add(new(rowRef, "Rejected", null, ["hr.leave_import_row_conflict"]));
                            else results.Add(new(rowRef, "AlreadyImported", movementId, []));
                            await reader.DisposeAsync();
                            await context.Transaction.ReleaseAsync("hr_leave_opening_row", token);
                            continue;
                        }
                    }

                    var goLiveConfiguration = await HrGoLiveConfigurationRepository.GetWithinTransactionAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, true, token)
                        ?? throw new HrException("hr.go_live_configuration_required");
                    if (goLiveConfiguration.LeaveOpeningBoundary is null) throw new HrException("hr.leave_opening_boundary_not_configured");
                    if (goLiveConfiguration.LeaveOpeningBoundary != row.EffectiveDate) throw new HrException("hr.leave_opening_boundary_mismatch");

                    var references = await ResolveLeaveOpeningReferencesAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                    var employmentId = references.EmploymentId ?? throw new HrException("hr.import_employment_not_found");
                    var leaveTypeId = references.LeaveTypeId ?? throw new HrException("hr.import_leave_type_not_found");
                    await LockBalanceAsync(context.Connection, context.Transaction, tenant, employmentId, leaveTypeId, token);
                    await using (var existingOpening = new NpgsqlCommand("select 1 from business_hr.leave_balance_movement where tenant_id=$1 and employment_id=$2 and leave_type_id=$3 and movement_type='Opening'", context.Connection, context.Transaction))
                    {
                        existingOpening.Parameters.AddWithValue(tenant.Value); existingOpening.Parameters.AddWithValue(employmentId); existingOpening.Parameters.AddWithValue(leaveTypeId);
                        if (await existingOpening.ExecuteScalarAsync(token) is not null) throw new HrException("hr.leave_opening_already_exists");
                    }
                    var idempotencyMaterial = $"{request.SourceSystem.Trim()}|{request.BatchReference.Trim()}|{rowRef.Trim()}";
                    var idempotencyKey = "hr-opening:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyMaterial))).ToLowerInvariant();
                    var sourceReference = rowRef.Trim();
                    var movement = await InsertMovementAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, employmentId, leaveTypeId,
                        row.EffectiveDate, "Opening", row.Quantity, "LegacyLeaveOpening", sourceReference, idempotencyKey, null, row.Reason.Trim(), actor, now, token);
                    await using (var provenance = new NpgsqlCommand("insert into business_hr.leave_import_reference(id,tenant_id,source_system,batch_reference,file_sha256,row_reference,row_payload_sha256,legal_entity_id,employment_id,leave_type_id,movement_id,imported_by,imported_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        provenance.Parameters.AddWithValue(Guid.CreateVersion7()); provenance.Parameters.AddWithValue(tenant.Value); provenance.Parameters.AddWithValue(request.SourceSystem.Trim()); provenance.Parameters.AddWithValue(request.BatchReference.Trim());
                        provenance.Parameters.AddWithValue(request.FileSha256.ToLowerInvariant()); provenance.Parameters.AddWithValue(rowRef.Trim()); provenance.Parameters.AddWithValue(rowHash); provenance.Parameters.AddWithValue(row.LegalEntityId); provenance.Parameters.AddWithValue(employmentId); provenance.Parameters.AddWithValue(leaveTypeId);
                        provenance.Parameters.AddWithValue(movement.Id); provenance.Parameters.AddWithValue(actor); provenance.Parameters.AddWithValue(now); await provenance.ExecuteNonQueryAsync(token);
                    }
                    results.Add(new(rowRef, "Imported", movement.Id, []));
                    await context.Transaction.ReleaseAsync("hr_leave_opening_row", token);
                }
                catch (Exception exception) when (exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" })
                {
                    await context.Transaction.RollbackAsync("hr_leave_opening_row", token);
                    await context.Transaction.ReleaseAsync("hr_leave_opening_row", token);
                    var code = exception is HrException hr ? hr.Code : exception is PostgresException { ConstraintName: "ux_hr_leave_opening_balance" } ? "hr.leave_opening_already_exists" : "hr.leave_import_row_conflict";
                    results.Add(new(rowRef, "Rejected", null, [code]));
                }
            }
            var summary = SummarizeLeaveOpeningImport(results);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "Human", actor, membershipId, "hr", "hr.leave.opening_imported", "leave_import_batch", Guid.CreateVersion7(), null,
                    JsonSerializer.Serialize(new { request.SourceSystem, request.BatchReference, fileSha256 = request.FileSha256.ToLowerInvariant(), summary.ImportedRows, summary.AlreadyImportedRows, summary.RejectedRows }), null,
                    "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return summary;
        }, ct);
    }

    public async Task<LeaveBalanceMovementView> AdjustBalanceAsync(TenantId tenant, Guid actor, Guid membershipId, AdjustLeaveBalanceRequest request, Guid correlationId, CancellationToken ct) =>
        await transactions.ExecuteAsync<LeaveBalanceMovementView>(tenant, async (context, token) =>
        {
            var idempotencyKey = request.IdempotencyKey?.Trim();
            if (request.Quantity == 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 160) throw new HrException("hr.leave_adjustment_invalid");
            await LockIdempotencyKeyAsync(context.Connection, context.Transaction, tenant, idempotencyKey, token);
            await LockBalanceAsync(context.Connection, context.Transaction, tenant, Guid.Empty, request.LeaveTypeId, token, request.EmploymentId);
            var now = clock.GetUtcNow(); var movement = await InsertMovementAsync(context.Connection, context.Transaction, tenant, request.LegalEntityId, request.EmploymentId, request.LeaveTypeId, request.EffectiveDate, "Adjustment", request.Quantity, "ManualAdjustment", idempotencyKey, idempotencyKey, null, request.Reason, actor, now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.leave_balance.adjusted", "leave_balance_movement", movement.Id, null, JsonSerializer.Serialize(new { request.EmploymentId, request.LeaveTypeId, request.Quantity, request.EffectiveDate, request.Reason }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return movement;
        }, ct);

    public async Task<int> AccrueAsync(TenantId tenant, Guid actor, Guid membershipId, Guid legalEntityId, AccrueLeaveRequest request, Guid correlationId, CancellationToken ct) =>
        await transactions.ExecuteAsync<int>(tenant, async (context, token) =>
        {
            var runKey = request.IdempotencyRunKey?.Trim();
            if (request.AsOfDate == default || string.IsNullOrWhiteSpace(runKey) || runKey.Length is < 8 or > 120) throw new HrException("hr.leave_accrual_request_invalid");
            var now = clock.GetUtcNow();
            await using (var locks = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || e.id::text || ':' || t.id::text || ':leave-balance',0)) from business_hr.employment e join business_hr.leave_type t on t.tenant_id=e.tenant_id and t.legal_entity_id=e.legal_entity_id and t.status='Active' join business_hr.leave_policy p on p.tenant_id=t.tenant_id and p.legal_entity_id=t.legal_entity_id and p.leave_type_id=t.id and $3 between p.effective_from and coalesce(p.effective_to,'infinity'::date) where e.tenant_id=$1 and e.legal_entity_id=$2 and e.status='Active' and e.start_date<=$3 and p.accrual_rule->>'mode' in ('Monthly','Annual') order by e.id,t.id", context.Connection, context.Transaction))
            { locks.Parameters.AddWithValue(tenant.Value); locks.Parameters.AddWithValue(legalEntityId); locks.Parameters.AddWithValue(request.AsOfDate); await using var reader = await locks.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) { } }
            await using var insert = new NpgsqlCommand("""
                insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,reason,created_by,created_at)
                select gen_random_uuid(),e.tenant_id,e.legal_entity_id,e.id,t.id,$3,'Accrual',(p.accrual_rule->>'amount')::numeric,'LeaveAccrual',to_char($3,'YYYY-MM'),
                    'leave-accrual:'||e.id::text||':'||t.id::text||':'||p.version::text||':'||to_char($3,'YYYY-MM'),p.version,'Configured leave policy accrual', $4,$5
                from business_hr.employment e join business_hr.leave_type t on t.tenant_id=e.tenant_id and t.legal_entity_id=e.legal_entity_id and t.status='Active'
                join business_hr.leave_policy p on p.tenant_id=t.tenant_id and p.legal_entity_id=t.legal_entity_id and p.leave_type_id=t.id and $3 between p.effective_from and coalesce(p.effective_to,'infinity'::date)
                where e.tenant_id=$1 and e.legal_entity_id=$2 and e.status='Active' and e.start_date<=$3
                    and (jsonb_array_length(coalesce(p.eligibility_rules->'employmentTypes','[]'::jsonb))=0 or p.eligibility_rules->'employmentTypes' ? e.employment_type)
                    and ((p.accrual_rule->>'mode'='Monthly' and $3=(date_trunc('month',$3::date)+interval '1 month - 1 day')::date)
                        or (p.accrual_rule->>'mode'='Annual' and extract(month from $3)=extract(month from p.effective_from) and extract(day from $3)=least(extract(day from p.effective_from),extract(day from (date_trunc('month',$3::date)+interval '1 month - 1 day')::date))))
                on conflict(tenant_id,idempotency_key) do nothing
                """, context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(request.AsOfDate); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); var count = await insert.ExecuteNonQueryAsync(token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.leave.accrual_processed", "leave_balance_movement", Guid.CreateVersion7(), null, JsonSerializer.Serialize(new { request.AsOfDate, runKey, insertedMovementCount = count }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return count;
        }, ct);

    public async Task<int> AccrueScheduledPeriodsAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct) =>
        await transactions.ExecuteAsync<int>(tenant, async (context, token) =>
        {
            if (legalEntityId == Guid.Empty || asOfDate == default) throw new HrException("hr.leave_accrual_request_invalid");
            var now = clock.GetUtcNow();
            var runId = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("""
                with employment_policy as materialized (
                    select e.tenant_id,e.legal_entity_id,e.id as employment_id,e.employment_type,e.start_date,e.end_date,
                        t.id as leave_type_id,p.id as policy_id,p.version,p.effective_from,p.effective_to,p.accrual_rule,p.eligibility_rules
                    from business_hr.employment e
                    join business_hr.leave_type t on t.tenant_id=e.tenant_id and t.legal_entity_id=e.legal_entity_id and t.status='Active'
                    join business_hr.leave_policy p on p.tenant_id=t.tenant_id and p.legal_entity_id=t.legal_entity_id and p.leave_type_id=t.id
                    where e.tenant_id=$1 and e.legal_entity_id=$2 and e.status in ('Active','Suspended','NoticePeriod','Terminated','Ended')
                        and p.accrual_rule->>'mode' in ('Monthly','Annual')
                ), scheduled_periods as materialized (
                    select policy.*, schedule.effective_date
                    from employment_policy policy
                    cross join lateral (
                        select (date_trunc('month',period.month_start)::date + interval '1 month - 1 day')::date as effective_date
                        from generate_series(
                            date_trunc('month',greatest(policy.start_date,policy.effective_from)::timestamp),
                            date_trunc('month',$3::date::timestamp), interval '1 month') period(month_start)
                        where policy.accrual_rule->>'mode'='Monthly'
                        union all
                        select make_date(years.year,extract(month from policy.effective_from)::integer,
                            least(extract(day from policy.effective_from)::integer,
                                extract(day from (date_trunc('month',make_date(years.year,extract(month from policy.effective_from)::integer,1)::timestamp)+interval '1 month - 1 day'))::integer))
                        from generate_series(extract(year from greatest(policy.start_date,policy.effective_from))::integer,
                            extract(year from $3::date)::integer) years(year)
                        where policy.accrual_rule->>'mode'='Annual'
                    ) schedule
                    where schedule.effective_date >= greatest(policy.start_date,policy.effective_from)
                        and schedule.effective_date <= $3
                        and (policy.end_date is null or schedule.effective_date <= policy.end_date)
                        and (policy.effective_to is null or schedule.effective_date <= policy.effective_to)
                        and (jsonb_array_length(coalesce(policy.eligibility_rules->'employmentTypes','[]'::jsonb))=0
                            or policy.eligibility_rules->'employmentTypes' ? policy.employment_type)
                        and not exists (
                            select 1 from business_hr.leave_balance_movement prior
                            where prior.tenant_id=policy.tenant_id and prior.idempotency_key=
                                'leave-accrual:'||policy.employment_id::text||':'||policy.leave_type_id::text||':'||policy.version::text||':'||to_char(schedule.effective_date,'YYYY-MM'))
                    order by policy.employment_id,schedule.effective_date,policy.leave_type_id
                    limit 5000
                )
                insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,reason,created_by,created_at)
                select gen_random_uuid(),tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,'Accrual',(accrual_rule->>'amount')::numeric,'LeaveAccrual',to_char(effective_date,'YYYY-MM'),
                    'leave-accrual:'||employment_id::text||':'||leave_type_id::text||':'||version::text||':'||to_char(effective_date,'YYYY-MM'),version,
                    'Scheduled policy accrual',$4,$5
                from scheduled_periods
                on conflict(tenant_id,idempotency_key) do nothing
                """, context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(asOfDate); insert.Parameters.AddWithValue(Guid.Empty); insert.Parameters.AddWithValue(now);
            var count = await insert.ExecuteNonQueryAsync(token);
            if (count > 0)
                await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                    new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "System", null, null, "hr", "hr.leave.scheduled_accrual_processed", "leave_accrual_batch", runId,
                        null, JsonSerializer.Serialize(new { asOfDate, insertedMovementCount = count }), null, "hr-scheduler", runId, null, now, null), token);
            return count;
        }, ct);

    public Task<int> ProcessScheduledCarryoversAsync(TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct) =>
        transactions.ExecuteAsync<int>(tenant, async (context, token) =>
        {
            if (legalEntityId == Guid.Empty || asOfDate == default) throw new HrException("hr.leave_carryover_scope_invalid");
            var now = clock.GetUtcNow(); var inserted = 0; var runId = Guid.CreateVersion7();
            var firstPolicyYear = await ReadFirstPolicyYearAsync(context.Connection, context.Transaction, tenant, legalEntityId, token);
            if (firstPolicyYear is not null)
            {
                for (var year = firstPolicyYear.Value + 1; year <= asOfDate.Year && inserted < 5000; year++)
                {
                    var boundary = new DateOnly(year, 1, 1);
                    if (boundary > asOfDate) break;
                    inserted += await ExpireDueCarryoversAsync(context.Connection, context.Transaction, tenant, legalEntityId, boundary, now, 5000 - inserted, token);
                    var cycleEnd = boundary.AddDays(-1);
                    var policies = await ReadCarryoverPoliciesAsync(context.Connection, context.Transaction, tenant, legalEntityId, cycleEnd, token);
                    foreach (var policy in policies)
                    {
                        if (inserted >= 5000) break;
                        await LockBalanceAsync(context.Connection, context.Transaction, tenant, policy.EmploymentId, policy.LeaveTypeId, token);
                        if (await WasYearBoundaryProcessedAsync(context.Connection, context.Transaction, tenant, policy.EmploymentId, policy.LeaveTypeId, boundary, token)) continue;
                        var lots = await GetEntitlementLotsAsync(context.Connection, context.Transaction, tenant, policy.EmploymentId, policy.LeaveTypeId, cycleEnd, true, false, token);
                        var eligible = lots.Where(lot => lot.ExpiresOn is null || lot.ExpiresOn > cycleEnd).ToArray();
                        var available = eligible.Sum(lot => lot.Remaining);
                        if (available <= 0) continue;
                        var carry = Math.Min(available, policy.CarryoverLimit);
                        foreach (var lot in eligible)
                        {
                            if (inserted >= 5000) break;
                            var expiry = await InsertMovementAsync(context.Connection, context.Transaction, tenant, legalEntityId, policy.EmploymentId, policy.LeaveTypeId, boundary, "Expiry", -lot.Remaining, "LeaveYearBoundary", boundary.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), AllocatedIdempotencyKey($"leave-year-close:{boundary:yyyy-MM-dd}", lot.Id), policy.PolicyVersion, "Unused leave closed at the annual carryover boundary", Guid.Empty, now, token);
                            await InsertAllocationAsync(context.Connection, context.Transaction, tenant, expiry.Id, lot.Id, token); inserted++;
                        }
                        if (carry > 0 && inserted < 5000)
                        {
                            var expiryDate = boundary.AddMonths(policy.ExpiryMonths!.Value);
                            await InsertMovementAsync(context.Connection, context.Transaction, tenant, legalEntityId, policy.EmploymentId, policy.LeaveTypeId, boundary, "CarryOver", carry, "LeaveCarryover", boundary.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"leave-carryover:{policy.EmploymentId:D}:{policy.LeaveTypeId:D}:{year}", policy.PolicyVersion, "Unused leave carried forward under the effective policy", Guid.Empty, now, token, expiryDate);
                            inserted++;
                        }
                    }
                }
            }
            if (inserted < 5000) inserted += await ExpireDueCarryoversAsync(context.Connection, context.Transaction, tenant, legalEntityId, asOfDate, now, 5000 - inserted, token);
            if (inserted > 0)
                await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "System", null, null, "hr", "hr.leave.carryover_lifecycle_processed", "leave_lifecycle_batch", runId, null, JsonSerializer.Serialize(new { asOfDate, insertedMovementCount = inserted }), null, "hr-scheduler", runId, null, now, null), token);
            return inserted;
        }, ct);

    private static async Task<int?> ReadFirstPolicyYearAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select min(extract(year from greatest(policy.effective_from,employment.start_date)))::integer from business_hr.leave_policy policy join business_hr.leave_type type on type.tenant_id=policy.tenant_id and type.legal_entity_id=policy.legal_entity_id and type.id=policy.leave_type_id join business_hr.employment employment on employment.tenant_id=type.tenant_id and employment.legal_entity_id=type.legal_entity_id where policy.tenant_id=$1 and policy.legal_entity_id=$2 and type.status='Active'", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<LeaveCarryoverPolicy>> ReadCarryoverPoliciesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, DateOnly cycleEnd, CancellationToken ct)
    {
        var policies = new List<LeaveCarryoverPolicy>();
        await using var command = new NpgsqlCommand("select employment.id,policy.leave_type_id,policy.version,policy.carryover_rule from business_hr.employment employment join business_hr.leave_type type on type.tenant_id=employment.tenant_id and type.legal_entity_id=employment.legal_entity_id join business_hr.leave_policy policy on policy.tenant_id=type.tenant_id and policy.legal_entity_id=type.legal_entity_id and policy.leave_type_id=type.id where employment.tenant_id=$1 and employment.legal_entity_id=$2 and employment.start_date<=$3 and (employment.end_date is null or employment.end_date>=$3) and type.status='Active' and policy.effective_from<=$3 and (policy.effective_to is null or policy.effective_to>=$3) order by employment.id,policy.leave_type_id,policy.version desc", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(cycleEnd);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            using var rule = JsonDocument.Parse(reader.GetString(3));
            var root = rule.RootElement;
            var limit = root.TryGetProperty("quantity", out var quantity) && quantity.ValueKind == JsonValueKind.Number ? quantity.GetDecimal() : 0m;
            var months = root.TryGetProperty("expiryMonths", out var duration) && duration.ValueKind == JsonValueKind.Number ? duration.GetInt32() : (int?)null;
            if (limit < 0 || limit > 0 && months is null or < 1 or > 120) throw new HrException("hr.leave_carryover_policy_invalid");
            policies.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), limit, months));
        }
        return policies.GroupBy(item => (item.EmploymentId, item.LeaveTypeId)).Select(group => group.First()).ToArray();
    }

    private static async Task<bool> WasYearBoundaryProcessedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid leaveTypeId, DateOnly boundary, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.leave_balance_movement where tenant_id=$1 and employment_id=$2 and leave_type_id=$3 and movement_type='Expiry' and source_type='LeaveYearBoundary' and source_reference=$4)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(leaveTypeId); command.Parameters.AddWithValue(boundary.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<int> ExpireDueCarryoversAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, DateOnly asOfDate, DateTimeOffset now, int remainingCapacity, CancellationToken ct)
    {
        if (remainingCapacity <= 0) return 0;
        var sources = new List<(Guid EmploymentId, Guid LeaveTypeId)>();
        await using (var find = new NpgsqlCommand("""
            select distinct carry.employment_id,carry.leave_type_id
            from business_hr.leave_balance_movement carry
            where carry.tenant_id=$1 and carry.legal_entity_id=$2 and carry.movement_type='CarryOver' and carry.quantity>0 and carry.expires_on<=$3
              and not exists(
                select 1 from business_hr.leave_balance_allocation allocation
                join business_hr.leave_balance_movement expiry on expiry.tenant_id=allocation.tenant_id and expiry.id=allocation.movement_id
                where allocation.tenant_id=carry.tenant_id and allocation.entitlement_movement_id=carry.id
                  and expiry.movement_type='Expiry' and expiry.source_type='LeaveEntitlementExpiry'
              )
            order by carry.employment_id,carry.leave_type_id
            limit $4
            """, connection, transaction))
        {
            find.Parameters.AddWithValue(tenant.Value); find.Parameters.AddWithValue(legalEntityId); find.Parameters.AddWithValue(asOfDate); find.Parameters.AddWithValue(remainingCapacity);
            await using var reader = await find.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) sources.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }
        var inserted = 0;
        var recordedExpiries = await ReadRecordedExpiryEntitlementsAsync(connection, transaction, tenant, legalEntityId, asOfDate, ct);
        foreach (var source in sources)
        {
            await LockBalanceAsync(connection, transaction, tenant, source.EmploymentId, source.LeaveTypeId, ct);
            var expired = (await GetEntitlementLotsAsync(connection, transaction, tenant, source.EmploymentId, source.LeaveTypeId, asOfDate, true, false, ct)).Where(lot => lot.ExpiresOn is { } expires && expires <= asOfDate).ToArray();
            foreach (var lot in expired)
            {
                if (inserted >= remainingCapacity) return inserted;
                if (recordedExpiries.Contains(lot.Id)) continue;
                var movement = await InsertMovementAsync(connection, transaction, tenant, legalEntityId, source.EmploymentId, source.LeaveTypeId, lot.ExpiresOn!.Value, "Expiry", -lot.Remaining, "LeaveEntitlementExpiry", lot.Id.ToString("D"), $"leave-expiry:{lot.Id:D}", null, "Carried leave expired under the effective policy", Guid.Empty, now, ct);
                await InsertAllocationAsync(connection, transaction, tenant, movement.Id, lot.Id, ct); inserted++;
            }
        }
        return inserted;
    }

    private static async Task<HashSet<Guid>> ReadRecordedExpiryEntitlementsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, DateOnly asOfDate, CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        await using var command = new NpgsqlCommand("select allocation.entitlement_movement_id from business_hr.leave_balance_movement entitlement join business_hr.leave_balance_allocation allocation on allocation.tenant_id=entitlement.tenant_id and allocation.entitlement_movement_id=entitlement.id join business_hr.leave_balance_movement expiry on expiry.tenant_id=allocation.tenant_id and expiry.id=allocation.movement_id where entitlement.tenant_id=$1 and entitlement.legal_entity_id=$2 and entitlement.movement_type='CarryOver' and entitlement.quantity>0 and entitlement.expires_on<=$3 and expiry.movement_type='Expiry' and expiry.source_type='LeaveEntitlementExpiry'", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(asOfDate);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetGuid(0));
        return result;
    }

    private sealed record LeaveCarryoverPolicy(Guid EmploymentId, Guid LeaveTypeId, int PolicyVersion, decimal CarryoverLimit, int? ExpiryMonths);

    private static async Task InsertAllocatedConsumptionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid employmentId, Guid leaveTypeId, DateOnly effectiveDate, decimal quantity, string movementType, string sourceType, string sourceReference, string idempotencyBase, int? policyVersion, string reason, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var remaining = quantity;
        var lots = await GetEntitlementLotsAsync(connection, transaction, tenant, employmentId, leaveTypeId, effectiveDate, false, true, ct);
        foreach (var lot in lots)
        {
            if (remaining <= 0) break;
            var allocated = Math.Min(lot.Remaining, remaining);
            var movement = await InsertMovementAsync(connection, transaction, tenant, entity, employmentId, leaveTypeId, effectiveDate, movementType, -allocated, sourceType, sourceReference, AllocatedIdempotencyKey(idempotencyBase, lot.Id), policyVersion, reason, actor, now, ct);
            await InsertAllocationAsync(connection, transaction, tenant, movement.Id, lot.Id, ct);
            remaining -= allocated;
        }
        if (remaining > 0)
            await InsertMovementAsync(connection, transaction, tenant, entity, employmentId, leaveTypeId, effectiveDate, movementType, -remaining, sourceType, sourceReference, UnallocatedIdempotencyKey(idempotencyBase), policyVersion, reason, actor, now, ct);
    }

    private static async Task<IReadOnlyList<AllocatedLeaveMovement>> ReadAllocatedMovementsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid leaveRequestId, string movementType, CancellationToken ct)
    {
        var movements = new List<AllocatedLeaveMovement>();
        await using var command = new NpgsqlCommand("select movement.id,movement.quantity,allocation.entitlement_movement_id from business_hr.leave_balance_movement movement left join business_hr.leave_balance_allocation allocation on allocation.tenant_id=movement.tenant_id and allocation.movement_id=movement.id where movement.tenant_id=$1 and movement.source_type='LeaveRequest' and movement.source_reference=$2 and movement.movement_type=$3", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(leaveRequestId.ToString("D")); command.Parameters.AddWithValue(movementType);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) movements.Add(new(reader.GetGuid(0), reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        return movements;
    }

    private static async Task InsertAllocatedReversalAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid employmentId, Guid leaveTypeId, DateOnly effectiveDate, AllocatedLeaveMovement original, string idempotencyBase, string reason, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var quantity = -original.Quantity;
        var sourceLot = original.EntitlementMovementId;
        var key = AllocatedIdempotencyKey(idempotencyBase, original.MovementId);
        var movement = await InsertMovementAsync(connection, transaction, tenant, entity, employmentId, leaveTypeId, effectiveDate, "CancellationRestore", quantity, "LeaveRequest", original.MovementId.ToString("D"), key, null, reason, actor, now, ct);
        if (sourceLot is { } entitlementId) await InsertAllocationAsync(connection, transaction, tenant, movement.Id, entitlementId, ct);
    }

    private static async Task InsertAllocationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid movementId, Guid entitlementMovementId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_hr.leave_balance_allocation(tenant_id,movement_id,entitlement_movement_id) values($1,$2,$3) on conflict(tenant_id,movement_id) do nothing", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(movementId); command.Parameters.AddWithValue(entitlementMovementId); await command.ExecuteNonQueryAsync(ct);
        await using var verify = new NpgsqlCommand("select entitlement_movement_id from business_hr.leave_balance_allocation where tenant_id=$1 and movement_id=$2", connection, transaction);
        verify.Parameters.AddWithValue(tenant.Value); verify.Parameters.AddWithValue(movementId);
        if ((Guid?)await verify.ExecuteScalarAsync(ct) != entitlementMovementId) throw new HrException("hr.leave_allocation_idempotency_conflict");
    }

    private static string AllocatedIdempotencyKey(string basis, Guid entitlementMovementId) => "leave-allocated:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(basis))).ToLowerInvariant() + ":" + entitlementMovementId.ToString("N");
    private static string UnallocatedIdempotencyKey(string basis) => "leave-unallocated:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(basis))).ToLowerInvariant();
    private sealed record AllocatedLeaveMovement(Guid MovementId, decimal Quantity, Guid? EntitlementMovementId);

    private static Dictionary<string, IReadOnlyList<string>> ValidateLeaveOpeningImport(HrLeaveOpeningImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 500) throw new HrException("hr.leave_import_row_count_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160)
            throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");

        var issues = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var references = new HashSet<string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in request.Rows)
        {
            var rowRef = row.RowReference ?? string.Empty;
            var rowIssues = new List<string>();
            if (string.IsNullOrWhiteSpace(rowRef) || rowRef.Trim().Length > 120 || !references.Add(rowRef.Trim())) rowIssues.Add("hr.import_row_reference_invalid");
            if (row.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160
                || string.IsNullOrWhiteSpace(row.LeaveTypeCode) || row.LeaveTypeCode.Trim().Length > 60 || row.EffectiveDate == default
                || row.Quantity <= 0 || row.Quantity >= 100_000_000m || decimal.Round(row.Quantity, 4) != row.Quantity)
                rowIssues.Add("hr.leave_opening_row_invalid");
            if (string.IsNullOrWhiteSpace(row.Reason) || row.Reason.Trim().Length > 500) rowIssues.Add("hr.leave_opening_reason_invalid");
            issues[rowRef] = rowIssues;

            if (row.LegalEntityId != Guid.Empty && !string.IsNullOrWhiteSpace(row.LegacyEmployeeId) && !string.IsNullOrWhiteSpace(row.LeaveTypeCode))
            {
                var target = $"{row.LegalEntityId:D}|{row.LegacyEmployeeId.Trim()}|{row.LeaveTypeCode.Trim().ToUpperInvariant()}";
                if (!targets.TryGetValue(target, out var targetRows)) targets[target] = targetRows = [];
                targetRows.Add(rowRef);
            }
        }
        foreach (var duplicateRows in targets.Values.Where(rows => rows.Count > 1))
            foreach (var rowRef in duplicateRows) issues[rowRef].Add("hr.leave_opening_duplicate_target");
        return issues.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal);
    }

    private static async Task<(Guid? EmploymentId, Guid? LeaveTypeId)> ResolveLeaveOpeningReferencesAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantId tenant, string sourceSystem, HrLeaveOpeningImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select
                (select e.id from business_hr.employee_import_reference reference
                 join business_hr.employment e on e.tenant_id=reference.tenant_id and e.id=reference.employment_id
                 where reference.tenant_id=$1 and reference.source_system=$2 and reference.legacy_employee_id=$3
                   and e.legal_entity_id=$4 and e.start_date<=$5 and (e.end_date is null or e.end_date>=$5)
                 order by e.start_date desc,e.id limit 1),
                (select t.id from business_hr.leave_type t where t.tenant_id=$1 and t.legal_entity_id=$4 and upper(t.code)=upper($6) and t.status='Active' limit 1)
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(sourceSystem); command.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
        command.Parameters.AddWithValue(row.LegalEntityId); command.Parameters.AddWithValue(row.EffectiveDate); command.Parameters.AddWithValue(row.LeaveTypeCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct)) return (null, null);
        return (reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1));
    }

    private static string LeaveOpeningRowHash(HrLeaveOpeningImportRow row)
    {
        var normalized = row with
        {
            RowReference = row.RowReference.Trim(),
            LegacyEmployeeId = row.LegacyEmployeeId.Trim(),
            LeaveTypeCode = row.LeaveTypeCode.Trim().ToUpperInvariant(),
            Reason = row.Reason.Trim()
        };
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized)))).ToLowerInvariant();
    }

    private static HrLeaveOpeningImportSummary RejectedLeaveOpeningImport(HrLeaveOpeningImportRequest request, IReadOnlyDictionary<string, IReadOnlyList<string>> validation)
    {
        var rows = request.Rows.Select(row => new HrLeaveOpeningImportRowResult(row.RowReference ?? string.Empty, "Rejected", null, validation.GetValueOrDefault(row.RowReference ?? string.Empty, []))).ToArray();
        return SummarizeLeaveOpeningImport(rows);
    }

    private static HrLeaveOpeningImportSummary SummarizeLeaveOpeningImport(IReadOnlyList<HrLeaveOpeningImportRowResult> rows) =>
        new(rows.Count, rows.Count(row => row.Status == "Imported"), rows.Count(row => row.Status == "AlreadyImported"), rows.Count(row => row.Status == "Rejected"), rows);

    private static async Task<List<DateOnly>> GetChargeDatesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid legalEntityId, DateOnly from, DateOnly to, string counting, CancellationToken ct)
    {
        if (counting == "CalendarDays") return Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(offset => from.AddDays(offset)).ToList();
        await using var command = new NpgsqlCommand("""
            with days as (select d::date as day from generate_series($4::date,$5::date,interval '1 day') d), calendar as (
                select a.work_calendar_id from business_hr.employment_assignment a where a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 and a.effective_from<=$5 and (a.effective_to is null or a.effective_to>=$4) order by a.effective_from desc limit 1
            ) select d.day from days d join business_hr.work_calendar_weekly_pattern w on w.tenant_id=$1 and w.calendar_id=(select work_calendar_id from calendar) and w.day_of_week=extract(dow from d.day)::int and w.is_working_day
            where not exists(select 1 from business_hr.work_calendar_holiday h where h.tenant_id=$1 and h.calendar_id=(select work_calendar_id from calendar) and h.holiday_date=d.day and h.status='Active')
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(from); command.Parameters.AddWithValue(to); var dates = new List<DateOnly>(); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) dates.Add(reader.GetFieldValue<DateOnly>(0)); return dates;
    }

    private static async Task LockBalanceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid leaveTypeId, CancellationToken ct, Guid? alternativeEmployment = null)
    {
        await using var command = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || $2::text || ':' || $3::text || ':leave-balance',0))", connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(alternativeEmployment ?? employmentId); command.Parameters.AddWithValue(leaveTypeId); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task LockIdempotencyKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, string key, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended($1::text || ':' || $2::text || ':hr-idempotency',0))", connection, transaction); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(key); await command.ExecuteNonQueryAsync(ct); }

    private async Task<decimal> GetBalanceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid leaveTypeId, DateOnly? requestedAsOf, bool includeFutureLeaveRequests, CancellationToken ct)
    {
        var asOf = requestedAsOf ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await using var totalCommand = new NpgsqlCommand("select coalesce(sum(quantity),0) from business_hr.leave_balance_movement where tenant_id=$1 and employment_id=$2 and leave_type_id=$3 and (effective_date<=$4 or (source_type='LeaveRequest' and ($5 or created_at::date<=$4)))", connection, transaction);
        totalCommand.Parameters.AddWithValue(tenant.Value); totalCommand.Parameters.AddWithValue(employmentId); totalCommand.Parameters.AddWithValue(leaveTypeId); totalCommand.Parameters.AddWithValue(asOf); totalCommand.Parameters.AddWithValue(includeFutureLeaveRequests);
        var total = Convert.ToDecimal(await totalCommand.ExecuteScalarAsync(ct) ?? 0, CultureInfo.InvariantCulture);
        var expired = (await GetEntitlementLotsAsync(connection, transaction, tenant, employmentId, leaveTypeId, asOf, true, includeFutureLeaveRequests, ct)).Where(lot => lot.ExpiresOn is { } expires && expires <= asOf).Sum(lot => lot.Remaining);
        return total - expired;
    }

    private static async Task<IReadOnlyList<LeaveEntitlementLot>> GetEntitlementLotsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid leaveTypeId, DateOnly asOfDate, bool includeExpired, bool includeFutureLeaveRequests, CancellationToken ct)
    {
        var lots = new List<LeaveEntitlementLot>();
        await using (var command = new NpgsqlCommand("""
            select entitlement.id,entitlement.effective_date,entitlement.expires_on,
                entitlement.quantity+coalesce(sum(consumption.quantity) filter(where consumption.effective_date<=$5 or (consumption.source_type='LeaveRequest' and ($6 or consumption.created_at::date<=$5))),0) as remaining
            from business_hr.leave_balance_movement entitlement
            left join business_hr.leave_balance_allocation allocation
              on allocation.tenant_id=entitlement.tenant_id and allocation.entitlement_movement_id=entitlement.id
            left join business_hr.leave_balance_movement consumption
              on consumption.tenant_id=allocation.tenant_id and consumption.id=allocation.movement_id
            where entitlement.tenant_id=$1 and entitlement.employment_id=$2 and entitlement.leave_type_id=$3
              and entitlement.quantity>0
              and not exists(select 1 from business_hr.leave_balance_allocation origin where origin.tenant_id=entitlement.tenant_id and origin.movement_id=entitlement.id)
              and entitlement.effective_date<=$5
              and ($4 or entitlement.expires_on is null or entitlement.expires_on>$5)
            group by entitlement.id,entitlement.effective_date,entitlement.expires_on,entitlement.quantity
            having entitlement.quantity+coalesce(sum(consumption.quantity) filter(where consumption.effective_date<=$5 or (consumption.source_type='LeaveRequest' and ($6 or consumption.created_at::date<=$5))),0)>0
            order by entitlement.expires_on nulls last,entitlement.effective_date,entitlement.id
            """, connection, transaction))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(leaveTypeId); command.Parameters.AddWithValue(includeExpired); command.Parameters.AddWithValue(asOfDate); command.Parameters.AddWithValue(includeFutureLeaveRequests);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) lots.Add(new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetDecimal(3)));
        }
        var historicalDebt = await ReadUnallocatedDebtAsync(connection, transaction, tenant, employmentId, leaveTypeId, asOfDate, false, includeFutureLeaveRequests, ct);
        ApplyDebt(lots, historicalDebt, lots.Select((lot, index) => (lot, index)).OrderBy(item => item.lot.EffectiveDate).ThenBy(item => item.lot.Id).Select(item => item.index));
        var adjustmentDebt = await ReadUnallocatedDebtAsync(connection, transaction, tenant, employmentId, leaveTypeId, asOfDate, true, includeFutureLeaveRequests, ct);
        ApplyDebt(lots, adjustmentDebt, Enumerable.Range(0, lots.Count));
        return lots.Where(lot => lot.Remaining > 0).ToArray();
    }

    private static async Task<decimal> ReadUnallocatedDebtAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, Guid leaveTypeId, DateOnly asOfDate, bool manualAdjustments, bool includeFutureLeaveRequests, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select coalesce(sum(-movement.quantity),0) from business_hr.leave_balance_movement movement where movement.tenant_id=$1 and movement.employment_id=$2 and movement.leave_type_id=$3 and movement.quantity<0 and (movement.effective_date<=$4 or (movement.source_type='LeaveRequest' and ($6 or movement.created_at::date<=$4))) and (movement.source_type='ManualAdjustment')=$5 and not exists(select 1 from business_hr.leave_balance_allocation allocation where allocation.tenant_id=movement.tenant_id and allocation.movement_id=movement.id)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(leaveTypeId); command.Parameters.AddWithValue(asOfDate); command.Parameters.AddWithValue(manualAdjustments); command.Parameters.AddWithValue(includeFutureLeaveRequests);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(ct) ?? 0, CultureInfo.InvariantCulture);
    }

    private static void ApplyDebt(List<LeaveEntitlementLot> lots, decimal debt, IEnumerable<int> indexes)
    {
        foreach (var index in indexes)
        {
            if (debt <= 0) break;
            var consumed = Math.Min(lots[index].Remaining, debt); lots[index] = lots[index] with { Remaining = lots[index].Remaining - consumed }; debt -= consumed;
        }
    }

    private sealed record LeaveEntitlementLot(Guid Id, DateOnly EffectiveDate, DateOnly? ExpiresOn, decimal Remaining);

    private static async Task<LeaveBalanceMovementView> InsertMovementAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid employmentId, Guid leaveTypeId, DateOnly effectiveDate, string type, decimal quantity, string sourceType, string sourceReference, string key, int? policyVersion, string reason, Guid actor, DateTimeOffset now, CancellationToken ct, DateOnly? expiresOn = null)
    {
        await using (var existing = new NpgsqlCommand("select id,effective_date,movement_type,quantity,source_type,source_reference,reason,created_at,legal_entity_id,employment_id,leave_type_id,policy_version,expires_on from business_hr.leave_balance_movement where tenant_id=$1 and idempotency_key=$2", connection, transaction))
        {
            existing.Parameters.AddWithValue(tenant.Value); existing.Parameters.AddWithValue(key); await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var existingExpiry = reader.IsDBNull(12) ? null : reader.GetFieldValue<DateOnly?>(12);
                var same = reader.GetGuid(8) == entity && reader.GetGuid(9) == employmentId && reader.GetGuid(10) == leaveTypeId && reader.GetFieldValue<DateOnly>(1) == effectiveDate && reader.GetString(2) == type && reader.GetDecimal(3) == quantity && reader.GetString(4) == sourceType && reader.GetString(5) == sourceReference && reader.GetString(6) == reason && (reader.IsDBNull(11) ? null : reader.GetInt32(11)) == policyVersion && existingExpiry == expiresOn;
                if (!same) throw new HrException("hr.leave_movement_idempotency_conflict");
                return new(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), existingExpiry);
            }
        }
        var id = Guid.CreateVersion7(); await using var insert = new NpgsqlCommand("insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,expires_on,reason,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16)", connection, transaction); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(entity); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(leaveTypeId); insert.Parameters.AddWithValue(effectiveDate); insert.Parameters.AddWithValue(type); insert.Parameters.AddWithValue(quantity); insert.Parameters.AddWithValue(sourceType); insert.Parameters.AddWithValue(sourceReference); insert.Parameters.AddWithValue(key); insert.Parameters.AddWithValue((object?)policyVersion ?? DBNull.Value); insert.Parameters.AddWithValue((object?)expiresOn ?? DBNull.Value); insert.Parameters.AddWithValue(reason); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(ct); return new(id, effectiveDate, type, quantity, sourceType, sourceReference, reason, now, expiresOn);
    }

    private static async Task InsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid id, string type, string? from, string to, Guid actor, string? reason, DateTimeOffset now, CancellationToken ct)
    { await using var command = new NpgsqlCommand("insert into business_hr.leave_request_event(id,tenant_id,legal_entity_id,leave_request_id,event_type,from_status,to_status,actor_id,reason,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", connection, transaction); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(type); command.Parameters.AddWithValue((object?)from ?? DBNull.Value); command.Parameters.AddWithValue(to); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue((object?)reason ?? DBNull.Value); command.Parameters.AddWithValue(now); await command.ExecuteNonQueryAsync(ct); }

    private static LeaveRequestView ReadRequest(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetGuid(4), r.GetFieldValue<DateOnly>(5), r.GetFieldValue<DateOnly>(6), r.GetDecimal(7), r.GetString(8), r.GetInt64(9), r.IsDBNull(10) ? null : r.GetString(10), r.GetFieldValue<DateTimeOffset>(11), r.IsDBNull(12) ? null : r.GetFieldValue<DateTimeOffset>(12), r.IsDBNull(13) ? null : r.GetGuid(13), r.IsDBNull(14) ? null : r.GetFieldValue<DateTimeOffset>(14), r.IsDBNull(15) ? null : r.GetString(15));
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct) { await using var cmd = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); cmd.Parameters.AddWithValue(tenant.Value.ToString("D")); await cmd.ExecuteNonQueryAsync(ct); }
}
