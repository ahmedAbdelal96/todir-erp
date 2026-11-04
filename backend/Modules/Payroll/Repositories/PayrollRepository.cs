using Modules.Payroll.DTOs;
using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Hr;
using Modules.Payroll.Services;
using Core.Services;
using System.Text.Json;

namespace Modules.Payroll.Repositories;

public sealed class PayrollRepository(NpgsqlDataSource dataSource, IPayrollCountryProviderRegistry countryProviders)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<PayrollGroupDto>> ListGroupsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var cmd = new NpgsqlCommand("select id,legal_entity_id,code,name,frequency,pay_day,currency_code,status,holiday_adjustment from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 order by code", c, tx);
        cmd.Parameters.AddWithValue(tenant.Value); cmd.Parameters.AddWithValue(legalEntityId);
        await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollGroupDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt16(5), r.GetString(6).Trim(), r.GetString(7), r.GetString(8))); await r.CloseAsync(); await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PayrollGroupDto> CreateGroupAsync(TenantId tenant, CreatePayrollGroupRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var frequency = request.Frequency.Trim(); var holidayAdjustment = NormalizeHolidayAdjustment(request.HolidayAdjustment);
        if (frequency is not ("Monthly" or "Weekly" or "Biweekly" or "SemiMonthly")) throw new PayrollRepositoryException("payroll.frequency_invalid");
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_group(tenant_id,legal_entity_id,id,code,name,frequency,pay_day,holiday_adjustment,currency_code,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$10) returning status", c, tx);
        Add(cmd, tenant.Value, request.LegalEntityId, id, request.Code.Trim(), request.Name.Trim(), frequency, request.PayDay, holidayAdjustment, request.CurrencyCode.Trim().ToUpperInvariant(), now);
        var status = (string)(await cmd.ExecuteScalarAsync(ct) ?? "Active"); await tx.CommitAsync(ct); return new(id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), frequency, request.PayDay, request.CurrencyCode.Trim().ToUpperInvariant(), status, holidayAdjustment);
    }

    public async Task<PayrollPeriodDto> CreatePeriodAsync(TenantId tenant, Guid legalEntityId, CreatePayrollPeriodRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var group = new NpgsqlCommand("select holiday_adjustment from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 and id=$3", c, tx); Add(group, tenant.Value, legalEntityId, request.PayrollGroupId); var holidayAdjustment = (string?)await group.ExecuteScalarAsync(ct) ?? throw new PayrollRepositoryException("payroll.group_not_found"); var plannedPayDate = AdjustPayDate(request.PlannedPayDate, holidayAdjustment);
        await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_period(tenant_id,legal_entity_id,id,payroll_group_id,period_start,period_end,input_cutoff,calculation_date,planned_pay_date,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$10)", c, tx);
        Add(cmd, tenant.Value, legalEntityId, id, request.PayrollGroupId, request.PeriodStart, request.PeriodEnd, request.InputCutoff, request.CalculationDate, plannedPayDate, now); await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
        return new(id, request.PayrollGroupId, request.PeriodStart, request.PeriodEnd, request.InputCutoff, plannedPayDate, "Open");
    }

    public async Task<IReadOnlyList<PayrollPeriodDto>> ListPeriodsAsync(TenantId tenant, Guid legalEntityId, Guid groupId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_start,period_end,input_cutoff,planned_pay_date,status from business_payroll.payroll_period where tenant_id=$1 and legal_entity_id=$2 and payroll_group_id=$3 order by period_start desc", c, tx);
        Add(cmd, tenant.Value, legalEntityId, groupId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollPeriodDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.GetFieldValue<DateOnly>(3), r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateOnly>(5), r.GetString(6))); await r.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<IReadOnlyList<PayrollRunDto>> ListRunsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_id,run_number,run_type,calculation_state,finalization_state,payment_state,total_gross,total_deductions,total_net,employee_count from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 order by created_at desc offset $3 limit $4", c, tx);
        Add(cmd, tenant.Value, legalEntityId, (page - 1) * pageSize, pageSize); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollRunDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetInt32(11))); await r.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<PayrollRunDto> CreateRunAsync(TenantId tenant, Guid legalEntityId, CreatePayrollRunRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var runType = request.RunType.Trim(); if (runType is not ("Regular" or "OffCycle" or "Retro" or "FinalSettlement" or "Adjustment" or "BonusOnly" or "Retroactive")) throw new PayrollRepositoryException("payroll.run_type_invalid");
        if (runType is "Retro" or "Retroactive" && request.SourceRunId is null) throw new PayrollRepositoryException("payroll.retro_source_run_required");
        if (runType == "FinalSettlement" && request.SettlementEmploymentId is null) throw new PayrollRepositoryException("payroll.settlement_employee_required");
        if (runType == "Regular" && (request.SourceRunId is not null || request.SettlementEmploymentId is not null)) throw new PayrollRepositoryException("payroll.regular_lineage_invalid");
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_run(tenant_id,legal_entity_id,id,payroll_group_id,period_id,run_number,run_type,currency_code,created_at,updated_at) select $1,$2,$3,$4,$5,'PR-'||to_char(current_date,'YYYYMMDD')||'-'||right($3::text,6),$6,currency_code,$7,$7 from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 and id=$4 returning run_number,currency_code", c, tx);
        var sourcePeriodId = request.SourcePeriodId;
        if (request.SourceRunId is not null)
        {
            await using var source = new NpgsqlCommand("select legal_entity_id,period_id,finalization_state from business_payroll.payroll_run where tenant_id=$1 and id=$2", c, tx); Add(source, tenant.Value, request.SourceRunId.Value); await using var sourceReader = await source.ExecuteReaderAsync(ct); if (!await sourceReader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.source_run_not_found"); if (sourceReader.GetGuid(0) != legalEntityId || sourceReader.GetString(2) != "Finalized") throw new PayrollRepositoryException("payroll.source_run_not_finalized"); sourcePeriodId ??= sourceReader.IsDBNull(1) ? null : sourceReader.GetGuid(1); await sourceReader.CloseAsync();
        }
        Add(cmd, tenant.Value, legalEntityId, id, request.PayrollGroupId, Db(request.PeriodId), runType, now); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.group_not_found"); var number = r.GetString(0); var currency = r.GetString(1).Trim(); await r.CloseAsync();
        await using var lineage = new NpgsqlCommand("update business_payroll.payroll_run set source_run_id=$4,source_period_id=$5,settlement_employment_id=$6,effective_date=$7,reason_code=$8,notes=$9 where tenant_id=$1 and legal_entity_id=$2 and id=$3", c, tx);
        Add(lineage, tenant.Value, legalEntityId, id, Db(request.SourceRunId), Db(sourcePeriodId), Db(request.SettlementEmploymentId), Db(request.EffectiveDate), DbValue(request.ReasonCode?.Trim()), DbValue(request.Notes?.Trim())); await lineage.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
        return new(id, request.PayrollGroupId, request.PeriodId, number, runType, "Draft", "Open", "NotReady", 0, 0, 0, 0);
    }

    public async Task<PayrollRunDto?> GetRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_id,run_number,run_type,calculation_state,finalization_state,payment_state,total_gross,total_deductions,total_net,employee_count from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", c, tx); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); var found = await r.ReadAsync(ct) ? new PayrollRunDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetInt32(11)) : null; await r.CloseAsync(); await tx.CommitAsync(ct); return found;
    }

    public async Task<PayrollRunDto> ApproveRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var command = new NpgsqlCommand("update business_payroll.payroll_run set approval_state='Approved',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and calculation_state='Calculated' and finalization_state='Open' and approval_state in ('Pending','NotRequired')", c, tx);
        Add(command, tenant.Value, legalEntityId, runId);
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.run_not_ready_for_approval");
        await tx.CommitAsync(ct);
        return await GetRunAsync(tenant, legalEntityId, runId, ct) ?? throw new PayrollRepositoryException("payroll.run_not_found");
    }

    public async Task<(PayrollRunDto Run, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly InputCutoff)?> GetRunContextAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select r.id,r.payroll_group_id,r.period_id,r.run_number,r.run_type,r.calculation_state,r.finalization_state,r.payment_state,r.total_gross,r.total_deductions,r.total_net,r.employee_count,p.period_start,p.period_end,p.input_cutoff from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3", c, tx); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) { await r.CloseAsync(); await tx.CommitAsync(ct); return null; }
        var run = new PayrollRunDto(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetInt32(11)); if (r.IsDBNull(12) || r.IsDBNull(13) || r.IsDBNull(14)) throw new PayrollRepositoryException("payroll.period_required"); var context = (run, r.GetFieldValue<DateOnly>(12), r.GetFieldValue<DateOnly>(13), r.GetFieldValue<DateOnly>(14)); await r.CloseAsync(); await tx.CommitAsync(ct); return context;
    }

    public async Task<PayrollSnapshotDto> SaveHrSnapshotAsync(TenantId tenant, Guid legalEntityId, Guid runId, HrPayrollPeriodSnapshotContract snapshot, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(snapshot, JsonOptions); var id = Guid.CreateVersion7(); await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var existing = new NpgsqlCommand("select id,payload_sha256 from business_payroll.payroll_hr_snapshot where tenant_id=$1 and payroll_run_id=$2 for update", c, tx); Add(existing, tenant.Value, runId); await using var existingReader = await existing.ExecuteReaderAsync(ct);
        Guid? existingId = null; string? existingHash = null;
        if (await existingReader.ReadAsync(ct)) { existingId = existingReader.GetGuid(0); existingHash = existingReader.GetString(1).Trim(); }
        await existingReader.CloseAsync();
        if (existingHash is not null && !string.Equals(existingHash, snapshot.PayloadSha256, StringComparison.OrdinalIgnoreCase)) throw new PayrollRepositoryException("payroll.hr_snapshot_immutable");
        if (existingHash is null)
        {
            await using var insert = new NpgsqlCommand("insert into business_payroll.payroll_hr_snapshot(tenant_id,id,legal_entity_id,payroll_run_id,contract_version,period_start,period_end,cutoff_at,payload_sha256,payload,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10::jsonb,$11)", c, tx); Add(insert, tenant.Value, id, legalEntityId, runId, snapshot.ContractVersion, snapshot.PeriodStart, snapshot.PeriodEnd, snapshot.CutoffAt, snapshot.PayloadSha256, payload, snapshot.CreatedAt); await insert.ExecuteNonQueryAsync(ct);
        }
        else id = existingId!.Value;
        await using var update = new NpgsqlCommand("update business_payroll.payroll_run set hr_snapshot_id=$4,calculation_state=case when calculation_state='Calculated' then 'Captured' else calculation_state end,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and finalization_state='Open'", c, tx); Add(update, tenant.Value, legalEntityId, runId, id); if (await update.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.run_not_open"); await tx.CommitAsync(ct); return new(id, runId, snapshot.PeriodStart, snapshot.PeriodEnd, snapshot.CutoffAt, snapshot.PayloadSha256, snapshot.Employments.Count);
    }

    public async Task<IReadOnlyList<PayrollEmployeeDto>> ListRunEmployeesAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select e.employment_id,e.status,e.gross,e.deductions,e.net,e.employer_cost,e.exception_code from business_payroll.payroll_run_employee e join business_payroll.payroll_run r on r.tenant_id=e.tenant_id and r.id=e.run_id where e.tenant_id=$1 and r.legal_entity_id=$2 and e.run_id=$3 order by e.employment_id", c, tx); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollEmployeeDto>(); while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.IsDBNull(6) ? null : r.GetString(6))); await r.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task CalculateRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var check = new NpgsqlCommand("select currency_code,finalization_state,hr_snapshot_id,run_type,settlement_employment_id,source_run_id from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", c, tx); Add(check, tenant.Value, legalEntityId, runId); await using var cr = await check.ExecuteReaderAsync(ct); if (!await cr.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.run_not_found"); if (cr.GetString(1) != "Open") throw new PayrollRepositoryException("payroll.run_already_finalized"); var snapshotId = cr.IsDBNull(2) ? (Guid?)null : cr.GetGuid(2); var runType = cr.GetString(3); var settlementEmploymentId = cr.IsDBNull(4) ? (Guid?)null : cr.GetGuid(4); var sourceRunId = cr.IsDBNull(5) ? (Guid?)null : cr.GetGuid(5); await cr.CloseAsync(); if (snapshotId is null) throw new PayrollRepositoryException("payroll.hr_snapshot_missing");
        await using var clearLines = new NpgsqlCommand("delete from business_payroll.payroll_calculation_line where tenant_id=$1 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2)", c, tx); Add(clearLines, tenant.Value, runId); await clearLines.ExecuteNonQueryAsync(ct);
        await using var clearEmployees = new NpgsqlCommand("delete from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", c, tx); Add(clearEmployees, tenant.Value, runId); await clearEmployees.ExecuteNonQueryAsync(ct);
        await using var insert = new NpgsqlCommand("insert into business_payroll.payroll_run_employee(tenant_id,id,run_id,employment_id,hr_snapshot_id,snapshot_employment_number,status,gross,deductions,net,employer_cost,calculated_at) select $1,gen_random_uuid(),r.id,(h.item->>'employmentId')::uuid,s.id,h.item->>'employmentNumber','Pending',0,0,0,0,now() from business_payroll.payroll_run r join business_payroll.payroll_hr_snapshot s on s.tenant_id=r.tenant_id and s.payroll_run_id=r.id and s.id=r.hr_snapshot_id cross join lateral jsonb_array_elements(s.payload->'employments') h(item) where r.tenant_id=$1 and r.id=$2 and r.legal_entity_id=$3 and ($4 <> 'FinalSettlement' or (h.item->>'employmentId')::uuid=$5)", c, tx); Add(insert, tenant.Value, runId, legalEntityId, runType, Db(settlementEmploymentId)); await insert.ExecuteNonQueryAsync(ct);
        var snapshotPayload = await ReadSnapshotPayloadAsync(c, tx, tenant, snapshotId.Value, ct);
        await using var runGroupCommand = new NpgsqlCommand("select payroll_group_id,rtrim(currency_code) from business_payroll.payroll_run where tenant_id=$1 and id=$2", c, tx); Add(runGroupCommand, tenant.Value, runId); await using var runGroupReader = await runGroupCommand.ExecuteReaderAsync(ct); if (!await runGroupReader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.group_not_found"); var runGroupId = runGroupReader.GetGuid(0); var runCurrency = runGroupReader.GetString(1); await runGroupReader.CloseAsync();
        var statutoryModes = await ReadStatutoryModesAsync(c, tx, tenant, legalEntityId, snapshotPayload.PeriodEnd, ct);
        var components = new Dictionary<string, (Guid Id, string Category, string InputMode, Guid? VersionId, string? ProviderCode, string? StatutoryDomain, string Formula, int Scale)>(StringComparer.OrdinalIgnoreCase);
        await using (var componentCommand = new NpgsqlCommand("select c.id,c.code,c.category,c.input_mode,v.id,v.provider_code,v.statutory_domain,coalesce(v.formula,'{}'::jsonb)::text,coalesce(v.rounding_scale,2) from business_payroll.payroll_component c left join lateral (select id,provider_code,statutory_domain,formula,rounding_scale from business_payroll.payroll_component_version where tenant_id=c.tenant_id and component_id=c.id and status='Published' and effective_from<=$2 and (effective_to is null or effective_to>=$3) order by effective_from desc limit 1) v on true where c.tenant_id=$1 and c.status='Active'", c, tx))
        { Add(componentCommand, tenant.Value, snapshotPayload.PeriodEnd, snapshotPayload.PeriodStart); await using var reader = await componentCommand.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) components[reader.GetString(1)] = (reader.GetGuid(0), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.GetInt32(8)); await reader.CloseAsync(); }
        if (!components.ContainsKey("BASE")) throw new PayrollRepositoryException("payroll.component_invalid");
        var frozenComponentVersions = components.ToDictionary(item => item.Key, item => item.Value.VersionId, StringComparer.OrdinalIgnoreCase);
        var providerModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in components.Where(pair => string.Equals(pair.Value.InputMode, "Provider", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var domain = provider.Value.StatutoryDomain;
            if (domain is not ("Tax" or "Insurance")) throw new PayrollRepositoryException($"payroll.statutory_domain_missing:{provider.Key}");
            var mode = domain == "Tax" ? statutoryModes.TaxMode : domain == "Insurance" ? statutoryModes.InsuranceMode : "Automatic";
            providerModes[provider.Key] = mode;
            if (mode == "NotManaged") components.Remove(provider.Key);
        }
        var approvedInputs = new Dictionary<(Guid EmploymentId, string Code), (decimal Amount, List<Guid> InputIds)>();
        await using (var inputCommand = new NpgsqlCommand("select input.id,input.employment_id,component.code,input.amount from business_payroll.payroll_input input join business_payroll.payroll_component component on component.tenant_id=input.tenant_id and component.id=input.component_id where input.tenant_id=$1 and input.status='Approved' and input.input_date between $2 and $3 order by input.id", c, tx))
        { Add(inputCommand, tenant.Value, snapshotPayload.PeriodStart, snapshotPayload.PeriodEnd); await using var reader = await inputCommand.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) { var key = (reader.GetGuid(1), reader.GetString(2)); if (approvedInputs.TryGetValue(key, out var current)) { current.Amount += reader.GetDecimal(3); current.InputIds.Add(reader.GetGuid(0)); approvedInputs[key] = current; } else approvedInputs[key] = (reader.GetDecimal(3), [reader.GetGuid(0)]); } await reader.CloseAsync(); }
        var scheduledRecovery = new Dictionary<Guid, decimal>();
        await using (var recoveryCommand = new NpgsqlCommand("select r.employment_id,coalesce(sum(least(i.amount-i.applied_amount,r.outstanding_principal)),0) from business_payroll.employee_receivable r join business_payroll.recovery_installment i on i.tenant_id=r.tenant_id and i.receivable_id=r.id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.status='Disbursed' and i.status='Scheduled' and ($4='FinalSettlement' or i.due_date <= $3) and i.applied_amount < i.amount group by r.employment_id", c, tx))
        {
            Add(recoveryCommand, tenant.Value, legalEntityId, snapshotPayload.PeriodEnd, runType);
            await using var recoveryReader = await recoveryCommand.ExecuteReaderAsync(ct);
            while (await recoveryReader.ReadAsync(ct)) scheduledRecovery[recoveryReader.GetGuid(0)] = recoveryReader.GetDecimal(1);
            await recoveryReader.CloseAsync();
        }
        await using var employees = new NpgsqlCommand("select id,employment_id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 order by employment_id", c, tx); Add(employees, tenant.Value, runId); await using var employeeReader = await employees.ExecuteReaderAsync(ct); var employeeRows = new List<(Guid Id, Guid EmploymentId)>(); while (await employeeReader.ReadAsync(ct)) employeeRows.Add((employeeReader.GetGuid(0), employeeReader.GetGuid(1))); await employeeReader.CloseAsync();
        var assignments = new Dictionary<Guid, (decimal BaseSalary, string CurrencyCode, Guid StructureVersionId)>();
        await using (var assignmentCommand = new NpgsqlCommand("select distinct on (a.employment_id) a.employment_id,a.base_salary,rtrim(a.currency_code),a.salary_structure_version_id from business_payroll.employee_payroll_assignment a join business_payroll.salary_structure_version sv on sv.tenant_id=a.tenant_id and sv.id=a.salary_structure_version_id and sv.status='Published' where a.tenant_id=$1 and a.legal_entity_id=$2 and a.payroll_group_id=$3 and a.effective_from<=$4 and (a.effective_to is null or a.effective_to>=$5) and sv.effective_from<=$4 and (sv.effective_to is null or sv.effective_to>=$5) order by a.employment_id,a.effective_from desc,a.created_at desc", c, tx))
        { Add(assignmentCommand, tenant.Value, legalEntityId, runGroupId, snapshotPayload.PeriodEnd, snapshotPayload.PeriodStart); await using var assignmentReader = await assignmentCommand.ExecuteReaderAsync(ct); while (await assignmentReader.ReadAsync(ct)) assignments[assignmentReader.GetGuid(0)] = (assignmentReader.GetDecimal(1), assignmentReader.GetString(2), assignmentReader.GetGuid(3)); await assignmentReader.CloseAsync(); }
        var structureComponents = new Dictionary<Guid, Dictionary<string, (Guid VersionId, string? ProviderCode, string? StatutoryDomain, string Formula, int Scale)>>();
        await using (var structureCommand = new NpgsqlCommand("select sc.structure_version_id,pc.code,sc.component_version_id,cv.provider_code,cv.statutory_domain,coalesce(cv.formula,'{}'::jsonb)::text,cv.rounding_scale from business_payroll.salary_structure_component sc join business_payroll.payroll_component pc on pc.tenant_id=sc.tenant_id and pc.id=sc.component_id join business_payroll.payroll_component_version cv on cv.tenant_id=sc.tenant_id and cv.id=sc.component_version_id and cv.status='Published' where sc.tenant_id=$1", c, tx))
        { Add(structureCommand, tenant.Value); await using var structureReader = await structureCommand.ExecuteReaderAsync(ct); while (await structureReader.ReadAsync(ct)) { var structureId = structureReader.GetGuid(0); var code = structureReader.GetString(1); if (!structureComponents.TryGetValue(structureId, out var definitions)) { definitions = new Dictionary<string, (Guid, string?, string?, string, int)>(StringComparer.OrdinalIgnoreCase); structureComponents[structureId] = definitions; } definitions[code] = (structureReader.GetGuid(2), structureReader.IsDBNull(3) ? null : structureReader.GetString(3), structureReader.IsDBNull(4) ? null : structureReader.GetString(4), structureReader.GetString(5), structureReader.GetInt32(6)); } await structureReader.CloseAsync(); }
        foreach (var row in employeeRows)
        {
            var employment = snapshotPayload.Employments.FirstOrDefault(item => item.EmploymentId == row.EmploymentId) ?? throw new PayrollRepositoryException("payroll.snapshot_population_mismatch");
            if (!assignments.TryGetValue(row.EmploymentId, out var assignment)) throw new PayrollRepositoryException($"payroll.assignment_missing:{row.EmploymentId}");
            if (!string.Equals(assignment.CurrencyCode, runCurrency, StringComparison.OrdinalIgnoreCase)) throw new PayrollRepositoryException("payroll.assignment_currency_mismatch");
            if (!structureComponents.TryGetValue(assignment.StructureVersionId, out var assignedDefinitions)) throw new PayrollRepositoryException($"payroll.salary_structure_missing:{row.EmploymentId}");
            var applicableComponents = components.Where(item => string.Equals(item.Key, "BASE", StringComparison.OrdinalIgnoreCase) || assignedDefinitions.ContainsKey(item.Key)).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var definition in assignedDefinitions)
                if (applicableComponents.TryGetValue(definition.Key, out var component))
                    applicableComponents[definition.Key] = (component.Id, component.Category, component.InputMode, definition.Value.VersionId, definition.Value.ProviderCode, definition.Value.StatutoryDomain, definition.Value.Formula, definition.Value.Scale);
            var rules = applicableComponents.Where(pair => !string.Equals(pair.Key, "BASE", StringComparison.OrdinalIgnoreCase)).Select(pair => RuleFromVersion(pair.Key, pair.Value.Category, pair.Value.Formula, pair.Value.Scale)).ToArray();
            var proratedBaseSalary = ProrateBaseSalary(assignment.BaseSalary, employment, snapshotPayload.PeriodStart, snapshotPayload.PeriodEnd);
            var inputs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in applicableComponents.Keys.Where(code => !string.Equals(code, "BASE", StringComparison.OrdinalIgnoreCase)))
            {
                if (providerModes.TryGetValue(component, out var providerMode) && providerMode == "Manual" && !approvedInputs.ContainsKey((row.EmploymentId, component)) && statutoryModes.ManualInputPolicy == "Block")
                    throw new PayrollRepositoryException($"payroll.manual_statutory_input_missing:{component}");
                if (providerModes.TryGetValue(component, out providerMode) && providerMode == "Automatic" && IsCountryProviderFormula(applicableComponents[component].Formula) && (applicableComponents[component].ProviderCode is not { Length: > 0 } providerCode || countryProviders.Find(providerCode) is not { LegallyCertified: true }))
                    throw new PayrollRepositoryException($"payroll.country_configuration_missing:{applicableComponents[component].StatutoryDomain}");
                approvedInputs.TryGetValue((row.EmploymentId, component), out var approved);
                var amount = approved.Amount;
                amount += employment.ApprovedAdjustments.Where(item => string.Equals(item.TypeCode, component, StringComparison.OrdinalIgnoreCase) && item.EffectiveFrom <= snapshotPayload.PeriodEnd && (item.EffectiveTo is null || item.EffectiveTo >= snapshotPayload.PeriodStart)).Sum(item => item.Amount);
                if (string.Equals(component, "LOAN-RECOVERY", StringComparison.OrdinalIgnoreCase) && scheduledRecovery.TryGetValue(row.EmploymentId, out var recoveryAmount)) amount += recoveryAmount;
                inputs[component] = amount;
            }
            try
            {
                var result = PayrollCalculationEngine.Calculate(new PayrollCalculationInput(proratedBaseSalary, inputs, new Dictionary<string, decimal>()), rules);
                await using var updateEmployee = new NpgsqlCommand("update business_payroll.payroll_run_employee set status='Calculated',gross=$3,deductions=$4,net=$5,employer_cost=$6,calculation_hash=$7,exception_code=null,calculated_at=now() where tenant_id=$1 and id=$2", c, tx); Add(updateEmployee, tenant.Value, row.Id, result.Gross, result.EmployeeDeductions, result.Net, result.EmployerCost, result.Fingerprint); await updateEmployee.ExecuteNonQueryAsync(ct);
                var evidence = JsonSerializer.Serialize(new
                {
                    settingsVersion = statutoryModes.Version,
                    taxMode = statutoryModes.TaxMode,
                    insuranceMode = statutoryModes.InsuranceMode,
                    manualInputPolicy = statutoryModes.ManualInputPolicy,
                    componentVersions = frozenComponentVersions,
                    rules = rules.Select(rule => new { rule.Code, rule.Category, rule.FixedAmount, rule.PercentageOf, rule.Percentage, rule.Priority, rule.Scale }),
                    approvedInputIds = approvedInputs.Where(item => item.Key.EmploymentId == row.EmploymentId).ToDictionary(item => item.Key.Code, item => item.Value.InputIds)
                }, JsonOptions);
                await using var evidenceCommand = new NpgsqlCommand("update business_payroll.payroll_run_employee set statutory_settings_version=$3,statutory_tax_mode=$4,statutory_insurance_mode=$5,calculation_evidence=$6::jsonb where tenant_id=$1 and id=$2", c, tx); Add(evidenceCommand, tenant.Value, row.Id, statutoryModes.Version, statutoryModes.TaxMode, statutoryModes.InsuranceMode, evidence); await evidenceCommand.ExecuteNonQueryAsync(ct);
                foreach (var line in result.Lines.Where(line => line.Amount != 0m || line.Category == "Earning"))
                {
                    if (!applicableComponents.TryGetValue(line.Code, out var component)) throw new PayrollRepositoryException("payroll.component_invalid");
                    await using var lineCommand = new NpgsqlCommand("insert into business_payroll.payroll_calculation_line(tenant_id,id,run_employee_id,component_id,component_version_id,quantity,amount,source,trace) values($1,gen_random_uuid(),$2,$3,$4,1,$5,$6,$7::jsonb)", c, tx); Add(lineCommand, tenant.Value, row.Id, component.Id, component.VersionId is null ? DBNull.Value : component.VersionId.Value, line.Amount, line.Source, JsonSerializer.Serialize(new { kind = "PayrollCalculation", line.Code, line.Explanation, componentVersionId = component.VersionId }, JsonOptions)); await lineCommand.ExecuteNonQueryAsync(ct);
                }
                if (runType is "Retro" or "Retroactive")
                {
                    if (sourceRunId is null) throw new PayrollRepositoryException("payroll.retro_source_run_required");
                    await using var retroLines = new NpgsqlCommand("update business_payroll.payroll_calculation_line current_line set amount=current_line.amount-coalesce((select sum(previous_line.amount) from business_payroll.payroll_calculation_line previous_line join business_payroll.payroll_run_employee previous_employee on previous_employee.tenant_id=previous_line.tenant_id and previous_employee.id=previous_line.run_employee_id where previous_line.tenant_id=current_line.tenant_id and previous_employee.run_id=$3 and previous_employee.employment_id=(select employment_id from business_payroll.payroll_run_employee where tenant_id=$1 and id=$2) and previous_line.component_id=current_line.component_id),0) where current_line.tenant_id=$1 and current_line.run_employee_id=$2", c, tx);
                    Add(retroLines, tenant.Value, row.Id, sourceRunId.Value); await retroLines.ExecuteNonQueryAsync(ct);
                    await using var retroTotals = new NpgsqlCommand("update business_payroll.payroll_run_employee e set gross=x.gross,deductions=x.deductions,net=x.net,employer_cost=x.gross+x.employer from (select l.run_employee_id,coalesce(sum(case when pc.category='Earning' then l.amount else 0 end),0) gross,coalesce(sum(case when pc.category in ('EmployeeDeduction','Recovery') then l.amount else 0 end),0) deductions,coalesce(sum(case when pc.category='EmployerContribution' then l.amount else 0 end),0) employer,coalesce(sum(case when pc.category='Earning' then l.amount else 0 end),0)-coalesce(sum(case when pc.category in ('EmployeeDeduction','Recovery') then l.amount else 0 end),0) net from business_payroll.payroll_calculation_line l join business_payroll.payroll_component pc on pc.tenant_id=l.tenant_id and pc.id=l.component_id where l.tenant_id=$1 and l.run_employee_id=$2 group by l.run_employee_id) x where e.tenant_id=$1 and e.id=x.run_employee_id", c, tx);
                    Add(retroTotals, tenant.Value, row.Id); await retroTotals.ExecuteNonQueryAsync(ct);
                }
            }
            catch (PayrollCalculationException exception)
            {
                await using var updateEmployee = new NpgsqlCommand("update business_payroll.payroll_run_employee set status='Exception',gross=0,deductions=0,net=0,employer_cost=0,exception_code=$3,calculated_at=now() where tenant_id=$1 and id=$2", c, tx); Add(updateEmployee, tenant.Value, row.Id, exception.Code); await updateEmployee.ExecuteNonQueryAsync(ct);
            }
        }
        await using var exceptions = new NpgsqlCommand("insert into business_payroll.payroll_exception(tenant_id,id,run_id,employment_id,code,severity,explanation,required_action,created_at) select $1,gen_random_uuid(),e.run_id,e.employment_id,e.exception_code,'Blocking','فشل احتساب مكون أو قاعدة رواتب لهذه الوظيفة.','راجع إعداد المكون أو القاعدة ثم أعد الاحتساب.',now() from business_payroll.payroll_run_employee e where e.tenant_id=$1 and e.run_id=$2 and e.exception_code is not null on conflict (tenant_id,run_id,employment_id,code) do update set is_resolved=false,resolved_at=null", c, tx); Add(exceptions, tenant.Value, runId); await exceptions.ExecuteNonQueryAsync(ct);
        await using var update = new NpgsqlCommand("update business_payroll.payroll_run r set calculation_state='Calculated',total_gross=coalesce(x.gross,0),total_deductions=coalesce(x.deductions,0),total_net=coalesce(x.net,0),employee_count=coalesce(x.count,0),updated_at=now() from (select run_id,sum(gross) gross,sum(deductions) deductions,sum(net) net,count(*) count from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 group by run_id) x where r.tenant_id=$1 and r.id=$2", c, tx); Add(update, tenant.Value, runId); await update.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task FinalizeRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var approvalCheck = new NpgsqlCommand("select approval_state from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", c, tx); Add(approvalCheck, tenant.Value, legalEntityId, runId); var approvalState = (string?)await approvalCheck.ExecuteScalarAsync(ct); if (approvalState is null) throw new PayrollRepositoryException("payroll.run_not_found"); if (approvalState is "Pending" or "Rejected") throw new PayrollRepositoryException("payroll.run_approval_required");
        await using var check = new NpgsqlCommand("select r.run_number,r.calculation_state,r.finalization_state,r.currency_code,r.hr_snapshot_id,r.run_type,r.effective_date,p.planned_pay_date from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3 for update", c, tx); Add(check, tenant.Value, legalEntityId, runId); await using var rr = await check.ExecuteReaderAsync(ct); if (!await rr.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.run_not_found"); var number = rr.GetString(0); var state = rr.GetString(1); var final = rr.GetString(2); var currency = rr.GetString(3).Trim(); var snapshotId = rr.IsDBNull(4) ? (Guid?)null : rr.GetGuid(4); var runType = rr.GetString(5); var postingDate = rr.IsDBNull(6) ? (rr.IsDBNull(7) ? DateOnly.FromDateTime(DateTime.UtcNow.Date) : rr.GetFieldValue<DateOnly>(7)) : rr.GetFieldValue<DateOnly>(6); await rr.CloseAsync(); if (final != "Open") throw new PayrollRepositoryException("payroll.run_already_finalized"); if (state != "Calculated") throw new PayrollRepositoryException("payroll.run_not_ready"); if (snapshotId is null) throw new PayrollRepositoryException("payroll.hr_snapshot_missing");
        await using var population = new NpgsqlCommand("select (select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2),case when $4='FinalSettlement' then 1 else coalesce(jsonb_array_length((select payload from business_payroll.payroll_hr_snapshot where tenant_id=$1 and id=$3)->'employments'),0) end", c, tx); Add(population, tenant.Value, runId, snapshotId.Value, runType); await using var populationReader = await population.ExecuteReaderAsync(ct); await populationReader.ReadAsync(ct); var calculatedEmployees = populationReader.GetInt64(0); var snapshotEmployees = populationReader.GetInt32(1); await populationReader.CloseAsync(); if (calculatedEmployees != snapshotEmployees) throw new PayrollRepositoryException("payroll.snapshot_population_mismatch");
        await using var periodEndCommand = new NpgsqlCommand("select coalesce(p.period_end,(select (payload->>'periodEnd')::date from business_payroll.payroll_hr_snapshot where tenant_id=$1 and id=$3)) from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.id=$2", c, tx); Add(periodEndCommand, tenant.Value, runId, snapshotId.Value); var periodEnd = (DateOnly?)await periodEndCommand.ExecuteScalarAsync(ct) ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await using var exceptions = new NpgsqlCommand("select count(*) from business_payroll.payroll_exception where tenant_id=$1 and run_id=$2 and severity='Blocking' and is_resolved=false union all select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 and status='Exception'", c, tx); Add(exceptions, tenant.Value, runId); await using var exReader = await exceptions.ExecuteReaderAsync(ct); long blocking = 0; while (await exReader.ReadAsync(ct)) blocking += exReader.GetInt64(0); await exReader.CloseAsync(); if (blocking > 0) throw new PayrollRepositoryException("payroll.run_blocking_exceptions");
        await using var slip = new NpgsqlCommand("insert into business_payroll.payslip(tenant_id,legal_entity_id,id,run_employee_id,payslip_number,employment_id,gross,deductions,net,employer_cost,status,finalized_at,calculation_hash) select $1,$2,gen_random_uuid(),e.id,$3||'-'||right(e.id::text,8),e.employment_id,e.gross,e.deductions,e.net,e.employer_cost,'Finalized',now(),encode(sha256((e.gross::text||':'||e.deductions::text||':'||e.net::text||':'||$4::text)::bytea),'hex') from business_payroll.payroll_run_employee e where e.tenant_id=$1 and e.run_id=$4", c, tx); Add(slip, tenant.Value, legalEntityId, number, runId); await slip.ExecuteNonQueryAsync(ct);
        await using var lines = new NpgsqlCommand("insert into business_payroll.payslip_line(tenant_id,id,payslip_id,component_id,code,name,category,amount,trace) select $1,gen_random_uuid(),p.id,cl.component_id,pc.code,pc.name,pc.category,cl.amount,cl.trace from business_payroll.payslip p join business_payroll.payroll_calculation_line cl on cl.tenant_id=p.tenant_id and cl.run_employee_id=p.run_employee_id join business_payroll.payroll_component pc on pc.tenant_id=cl.tenant_id and pc.id=cl.component_id where p.tenant_id=$1 and p.legal_entity_id=$2 and p.status='Finalized' and p.run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$3)", c, tx); Add(lines, tenant.Value, legalEntityId, runId); await lines.ExecuteNonQueryAsync(ct);
        await ApplyRecoveryApplicationsAsync(c, tx, tenant, legalEntityId, runId, periodEnd, ct);
        await using var pi = new NpgsqlCommand("insert into business_payroll.payment_instruction(tenant_id,legal_entity_id,id,run_id,employment_id,amount,currency_code,created_at) select $1,$2,gen_random_uuid(),e.run_id,e.employment_id,e.net,$3,now() from business_payroll.payroll_run_employee e where e.tenant_id=$1 and e.run_id=$4 on conflict (tenant_id,run_id,employment_id) do nothing", c, tx); Add(pi, tenant.Value, legalEntityId, currency, runId); await pi.ExecuteNonQueryAsync(ct);
        await using var consume = new NpgsqlCommand("update business_payroll.payroll_input input set status='Consumed',consumed_run_id=$2 where input.tenant_id=$1 and input.status='Approved' and exists(select 1 from business_payroll.payroll_run_employee e join business_payroll.payroll_run r on r.tenant_id=e.tenant_id and r.id=e.run_id join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where e.tenant_id=$1 and e.run_id=$2 and e.employment_id=input.employment_id and input.input_date between p.period_start and p.period_end)", c, tx); Add(consume, tenant.Value, runId); await consume.ExecuteNonQueryAsync(ct);
        var accountingFactId = Guid.CreateVersion7();
        await using var classification = new NpgsqlCommand("select coalesce(sum(case when cv.statutory_domain='Tax' then l.amount else 0 end),0),coalesce(sum(case when cv.statutory_domain='Insurance' then l.amount else 0 end),0),coalesce(sum(case when pc.category='Recovery' then l.amount else 0 end),0) from business_payroll.payroll_calculation_line l join business_payroll.payroll_run_employee e on e.tenant_id=l.tenant_id and e.id=l.run_employee_id join business_payroll.payroll_component pc on pc.tenant_id=l.tenant_id and pc.id=l.component_id left join business_payroll.payroll_component_version cv on cv.tenant_id=l.tenant_id and cv.id=l.component_version_id where l.tenant_id=$1 and e.run_id=$2", c, tx); Add(classification, tenant.Value, runId); await using var classReader = await classification.ExecuteReaderAsync(ct); await classReader.ReadAsync(ct); var taxAmount = classReader.GetDecimal(0); var insuranceAmount = classReader.GetDecimal(1); var recoveryAmount = classReader.GetDecimal(2); await classReader.CloseAsync();
        await using var fact = new NpgsqlCommand("insert into business_payroll.accounting_fact(tenant_id,legal_entity_id,id,run_id,fact_type,total_amount,currency_code,idempotency_key,payload,created_at) select $1,$2,$3,$4,'PayrollPosted',total_net,$5,$6,jsonb_build_object('gross',total_gross,'deductions',total_deductions,'payable',total_net,'tax', $7,'insurance',$8,'recovery',$9,'currencyCode',$5,'postingDate',$10),now() from business_payroll.payroll_run where tenant_id=$1 and id=$4 on conflict (tenant_id,idempotency_key) do nothing", c, tx); Add(fact, tenant.Value, legalEntityId, accountingFactId, runId, currency, $"payroll:{tenant.Value:D}:{runId:D}", taxAmount, insuranceAmount, recoveryAmount, postingDate); await fact.ExecuteNonQueryAsync(ct);
        await using var finalizedEvent = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status) values($1,'PayrollFinalizedV1',jsonb_build_object('runId',$2,'legalEntityId',$3,'accountingFactId',$4,'currencyCode',$5,'postingDate',$6),$7,now(),$2,$2,'payroll',null,'Pending') on conflict do nothing", c, tx); Add(finalizedEvent, Guid.CreateVersion7(), runId, legalEntityId, accountingFactId, currency, postingDate, tenant.Value); await finalizedEvent.ExecuteNonQueryAsync(ct);
        await using var paymentEvent = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status) values($1,'PayrollPaymentRequestedV1',jsonb_build_object('runId',$2,'legalEntityId',$3,'currencyCode',$4),$5,now(),$2,$2,'payroll',null,'Pending') on conflict do nothing", c, tx); Add(paymentEvent, Guid.CreateVersion7(), runId, legalEntityId, currency, tenant.Value); await paymentEvent.ExecuteNonQueryAsync(ct);
        await using var markFinalized = new NpgsqlCommand("update business_payroll.payroll_run set finalization_state='Finalized',payment_state='Ready',accounting_state='Ready',updated_at=now() where tenant_id=$1 and id=$2", c, tx); Add(markFinalized, tenant.Value, runId); await markFinalized.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }

    private static async Task ApplyRecoveryApplicationsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, Guid runId, DateOnly periodEnd, CancellationToken ct)
    {
        var recoveryRows = new List<(Guid RunEmployeeId, Guid EmploymentId, decimal Amount)>();
        await using (var lines = new NpgsqlCommand("select e.id,e.employment_id,sum(cl.amount) from business_payroll.payroll_run_employee e join business_payroll.payroll_calculation_line cl on cl.tenant_id=e.tenant_id and cl.run_employee_id=e.id join business_payroll.payroll_component pc on pc.tenant_id=cl.tenant_id and pc.id=cl.component_id and pc.category='Recovery' where e.tenant_id=$1 and e.run_id=$2 group by e.id,e.employment_id having sum(cl.amount)>0", connection, transaction))
        {
            Add(lines, tenant.Value, runId); await using var reader = await lines.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) recoveryRows.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2)));
            await reader.CloseAsync();
        }
        foreach (var row in recoveryRows)
        {
            var remaining = row.Amount;
            await using var installments = new NpgsqlCommand("select i.id,i.receivable_id,i.amount-i.applied_amount,r.outstanding_principal from business_payroll.recovery_installment i join business_payroll.employee_receivable r on r.tenant_id=i.tenant_id and r.id=i.receivable_id where i.tenant_id=$1 and r.legal_entity_id=$2 and r.employment_id=$3 and r.status='Disbursed' and i.status='Scheduled' and i.due_date<=$4 and i.applied_amount<i.amount and r.outstanding_principal>0 order by i.due_date,i.installment_number for update of i,r", connection, transaction);
            Add(installments, tenant.Value, legalEntityId, row.EmploymentId, periodEnd); await using var reader = await installments.ExecuteReaderAsync(ct);
            var candidates = new List<(Guid InstallmentId, Guid ReceivableId, decimal Available, decimal Outstanding)>();
            while (await reader.ReadAsync(ct)) candidates.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3)));
            await reader.CloseAsync();
            foreach (var candidate in candidates)
            {
                if (remaining <= 0m) break;
                var amount = Math.Min(remaining, Math.Min(candidate.Available, candidate.Outstanding));
                if (amount <= 0m) continue;
                await using (var application = new NpgsqlCommand("insert into business_payroll.recovery_application(tenant_id,legal_entity_id,id,receivable_id,installment_id,run_id,run_employee_id,amount,created_at) values($1,$2,gen_random_uuid(),$3,$4,$5,$6,$7,now()) on conflict (tenant_id,installment_id,run_id) do nothing", connection, transaction))
                { Add(application, tenant.Value, legalEntityId, candidate.ReceivableId, candidate.InstallmentId, runId, row.RunEmployeeId, amount); if (await application.ExecuteNonQueryAsync(ct) == 0) continue; }
                await using (var installment = new NpgsqlCommand("update business_payroll.recovery_installment set applied_amount=applied_amount+$3,status=case when applied_amount+$3>=amount then 'Applied' else status end where tenant_id=$1 and id=$2 and status='Scheduled' and applied_amount+$3<=amount", connection, transaction))
                { Add(installment, tenant.Value, candidate.InstallmentId, amount); if (await installment.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.recovery_concurrency_conflict"); }
                await using (var receivable = new NpgsqlCommand("update business_payroll.employee_receivable set outstanding_principal=greatest(0,outstanding_principal-$3),status=case when outstanding_principal-$3<=0 then 'Settled' else status end where tenant_id=$1 and id=$2", connection, transaction))
                { Add(receivable, tenant.Value, candidate.ReceivableId, amount); await receivable.ExecuteNonQueryAsync(ct); }
                remaining -= amount;
            }
        }
    }

    public async Task<IReadOnlyList<PayslipDto>> ListPayslipsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select id,payslip_number,employment_id,gross,deductions,net,employer_cost,status,finalized_at from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 order by finalized_at desc nulls last limit 100", c, tx); Add(cmd, tenant.Value, legalEntityId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayslipDto>(); while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8))); await r.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<IReadOnlyList<PayslipDto>> ListSelfPayslipsAsync(TenantId tenant, Guid legalEntityId, Guid employmentId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var cmd = new NpgsqlCommand("select id,payslip_number,employment_id,gross,deductions,net,employer_cost,status,finalized_at from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 and employment_id=$3 order by finalized_at desc nulls last limit 100", c, tx); Add(cmd, tenant.Value, legalEntityId, employmentId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayslipDto>(); while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8))); await r.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<PayrollInputDto> CreateInputAsync(TenantId tenant, CreatePayrollInputRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_input(tenant_id,legal_entity_id,id,employment_id,component_id,input_date,amount,currency_code,source,status,idempotency_key,notes,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,$11,$12) returning status", c, tx);
        Add(cmd, tenant.Value, request.LegalEntityId, id, request.EmploymentId, request.ComponentId, request.InputDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.Source.Trim(), DbValue(request.IdempotencyKey?.Trim()), DbValue(request.Notes?.Trim()), now); var status = (string)(await cmd.ExecuteScalarAsync(ct) ?? "Draft"); await tx.CommitAsync(ct); return new(id, request.EmploymentId, request.ComponentId, request.InputDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.Source.Trim(), status, request.Notes?.Trim());
    }

    public async Task<PayrollInputDto> ApproveInputAsync(TenantId tenant, Guid legalEntityId, Guid inputId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("update business_payroll.payroll_input set status='Approved',approved_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning employment_id,component_id,input_date,amount,currency_code,source,status,notes", c, tx); Add(cmd, tenant.Value, legalEntityId, inputId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.input_not_found_or_not_draft"); var input = new PayrollInputDto(inputId, r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.GetDecimal(3), r.GetString(4).Trim(), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)); await r.CloseAsync(); await tx.CommitAsync(ct); return input;
    }

    public async Task<PayrollInputDto?> GetInputAsync(TenantId tenant, Guid legalEntityId, Guid inputId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("select employment_id,component_id,input_date,amount,currency_code,source,status,notes from business_payroll.payroll_input where tenant_id=$1 and legal_entity_id=$2 and id=$3", c, tx); Add(cmd, tenant.Value, legalEntityId, inputId); await using var r = await cmd.ExecuteReaderAsync(ct); var input = await r.ReadAsync(ct) ? new PayrollInputDto(inputId, r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.GetDecimal(3), r.GetString(4).Trim(), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)) : null; await r.CloseAsync(); await tx.CommitAsync(ct); return input;
    }

    public async Task<PayrollReportSummaryDto> GetReportAsync(TenantId tenant, Guid legalEntityId, DateOnly? from, DateOnly? to, string? runType, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        const string filter = " where r.tenant_id=$1 and r.legal_entity_id=$2 and ($3::date is null or p.period_end >= $3) and ($4::date is null or p.period_end <= $4) and ($5::text is null or r.run_type=$5)";
        await using var summary = new NpgsqlCommand("select count(*),coalesce(sum(r.total_gross),0),coalesce(sum(r.total_deductions),0),coalesce(sum(r.total_net),0) from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id" + filter, c, tx); Add(summary, tenant.Value, legalEntityId, Db(from), Db(to), DbValue(string.IsNullOrWhiteSpace(runType) ? null : runType.Trim())); await using var sr = await summary.ExecuteReaderAsync(ct); await sr.ReadAsync(ct); var totalCount = sr.GetInt32(0); var totalGross = sr.GetDecimal(1); var totalDeductions = sr.GetDecimal(2); var totalNet = sr.GetDecimal(3); await sr.CloseAsync();
        await using var rows = new NpgsqlCommand("select r.id,r.run_number,r.run_type,p.period_end,r.finalization_state,r.payment_state,r.total_gross,r.total_deductions,r.total_net,r.employee_count from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id" + filter + " order by coalesce(p.period_end,'0001-01-01') desc,r.created_at desc offset $6 limit $7", c, tx); Add(rows, tenant.Value, legalEntityId, Db(from), Db(to), DbValue(string.IsNullOrWhiteSpace(runType) ? null : runType.Trim()), (page - 1) * pageSize, pageSize); await using var reader = await rows.ExecuteReaderAsync(ct); var items = new List<PayrollReportItem>(); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetInt32(9))); await reader.CloseAsync(); await tx.CommitAsync(ct); return new(items, page, pageSize, totalCount, totalGross, totalDeductions, totalNet);
    }

    public async Task<PayrollImportResult> ImportInputsAsync(TenantId tenant, Guid legalEntityId, Guid actorId, PayrollImportRequest request, bool commit, CancellationToken ct)
    {
        if (request.Rows.Count > 5000) throw new PayrollRepositoryException("payroll.import_too_large");
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; var batchId = Guid.CreateVersion7();
        await using var find = new NpgsqlCommand("select id,status from business_payroll.payroll_import_batch where tenant_id=$1 and legal_entity_id=$2 and import_type=$3 and source_system=$4 and batch_reference=$5 and file_sha256=$6", c, tx); Add(find, tenant.Value, legalEntityId, request.ImportType.Trim(), request.SourceSystem.Trim(), request.BatchReference.Trim(), request.FileSha256.Trim()); await using var fr = await find.ExecuteReaderAsync(ct); string? priorStatus = null; if (await fr.ReadAsync(ct)) { batchId = fr.GetGuid(0); priorStatus = fr.GetString(1); }
        await fr.CloseAsync();
        if (priorStatus == "Committed") { await tx.CommitAsync(ct); return new(batchId, priorStatus, request.Rows.Count, request.Rows.Select(row => new PayrollImportRowResult(row.RowNumber, row.RowKey, "Imported", null, null)).ToArray()); }
        if (priorStatus is null)
        {
            await using var insertBatch = new NpgsqlCommand("insert into business_payroll.payroll_import_batch(tenant_id,legal_entity_id,id,import_type,source_system,batch_reference,file_sha256,status,row_count,actor_id,created_at) values($1,$2,$3,$4,$5,$6,$7,'Previewed',$8,$9,now())", c, tx); Add(insertBatch, tenant.Value, legalEntityId, batchId, request.ImportType.Trim(), request.SourceSystem.Trim(), request.BatchReference.Trim(), request.FileSha256.Trim(), request.Rows.Count, actorId); await insertBatch.ExecuteNonQueryAsync(ct);
        }
        var results = new List<PayrollImportRowResult>();
        foreach (var row in request.Rows.OrderBy(item => item.RowNumber))
        {
            Guid? inputId = null; string? issue = null; var rowStatus = "Pending";
            await using var component = new NpgsqlCommand("select id from business_payroll.payroll_component where tenant_id=$1 and code=$2 and status='Active'", c, tx); Add(component, tenant.Value, row.ComponentCode.Trim()); var componentScalar = await component.ExecuteScalarAsync(ct); var componentId = componentScalar is Guid componentGuidValue ? componentGuidValue : (Guid?)null;
            if (componentId is null) issue = "payroll.import_component_not_found"; else if (row.Amount < 0m) issue = "payroll.import_amount_invalid";
            if (issue is null && commit)
            {
                var componentGuid = componentId ?? throw new PayrollRepositoryException("payroll.import_component_not_found"); inputId = Guid.CreateVersion7(); await using var input = new NpgsqlCommand("insert into business_payroll.payroll_input(tenant_id,legal_entity_id,id,employment_id,component_id,input_date,amount,currency_code,source,status,idempotency_key,notes,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Approved',$10,$11,now()) on conflict (tenant_id,idempotency_key) do nothing", c, tx); Add(input, tenant.Value, legalEntityId, inputId.Value, row.EmploymentId, componentGuid, row.InputDate, row.Amount, row.CurrencyCode.Trim().ToUpperInvariant(), "PayrollImport", $"payroll-import:{batchId:D}:{row.RowKey}", DbValue(row.Notes)); await input.ExecuteNonQueryAsync(ct); rowStatus = "Imported";
            }
            else if (issue is null) rowStatus = "Ready"; else rowStatus = "Rejected";
            await using var rowCommand = new NpgsqlCommand("insert into business_payroll.payroll_import_row(tenant_id,id,batch_id,row_number,row_key,row_data,status,issue_code,created_at) values($1,gen_random_uuid(),$2,$3,$4,$5::jsonb,$6,$7,now()) on conflict (tenant_id,batch_id,row_number) do update set row_data=excluded.row_data,status=excluded.status,issue_code=excluded.issue_code", c, tx); Add(rowCommand, tenant.Value, batchId, row.RowNumber, row.RowKey, JsonSerializer.Serialize(row), rowStatus == "Imported" ? "Imported" : rowStatus == "Rejected" ? "Rejected" : "Pending", DbValue(issue)); await rowCommand.ExecuteNonQueryAsync(ct); results.Add(new(row.RowNumber, row.RowKey, rowStatus, issue, inputId));
        }
        if (commit && results.Any(item => item.Status == "Rejected")) throw new PayrollRepositoryException("payroll.import_validation_failed");
        if (commit) await ExecuteNonQueryAsync(c, tx, "update business_payroll.payroll_import_batch set status='Committed',committed_at=now() where tenant_id=$1 and id=$2", ct, tenant.Value, batchId);
        await tx.CommitAsync(ct); return new(batchId, commit ? "Committed" : "Previewed", results.Count, results);
    }

    public async Task<PayrollOpeningBalanceDto> CreateOpeningBalanceAsync(TenantId tenant, Guid legalEntityId, Guid actorId, PayrollOpeningBalanceRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0m || request.BalanceType.Trim() is not ("NetPay" or "Tax" or "Insurance" or "Loan" or "Advance" or "Other")) throw new PayrollRepositoryException("payroll.opening_balance_invalid");
        var id = Guid.CreateVersion7(); await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var command = new NpgsqlCommand("insert into business_payroll.payroll_opening_balance(tenant_id,legal_entity_id,id,employment_id,balance_type,amount,currency_code,as_of_date,source_reference,actor_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,now()) returning status", c, tx); Add(command, tenant.Value, legalEntityId, id, request.EmploymentId, request.BalanceType.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.AsOfDate, request.SourceReference.Trim(), actorId); var status = (string)(await command.ExecuteScalarAsync(ct) ?? "Draft"); await tx.CommitAsync(ct); return new(id, request.EmploymentId, request.BalanceType.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.AsOfDate, request.SourceReference.Trim(), status);
    }

    public async Task<IReadOnlyList<PayrollOpeningBalanceDto>> ListOpeningBalancesAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var command = new NpgsqlCommand("select id,employment_id,balance_type,amount,currency_code,as_of_date,source_reference,status from business_payroll.payroll_opening_balance where tenant_id=$1 and legal_entity_id=$2 order by as_of_date desc,created_at desc limit 500", c, tx); Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PayrollOpeningBalanceDto>(); while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetFieldValue<DateOnly>(5), reader.GetString(6), reader.GetString(7))); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<PayrollOpeningBalanceDto> ApproveOpeningBalanceAsync(TenantId tenant, Guid legalEntityId, Guid openingBalanceId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var command = new NpgsqlCommand("update business_payroll.payroll_opening_balance set status='Approved' where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning employment_id,balance_type,amount,currency_code,as_of_date,source_reference,status", c, tx); Add(command, tenant.Value, legalEntityId, openingBalanceId); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.opening_balance_not_found_or_not_draft"); var result = new PayrollOpeningBalanceDto(openingBalanceId, reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.GetString(6)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<EmployeeReceivableDto> CreateReceivableAsync(TenantId tenant, CreateReceivableRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var type = request.Type.Trim(); if (type is not ("Advance" or "Loan")) throw new PayrollRepositoryException("payroll.receivable_type_invalid");
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("insert into business_payroll.employee_receivable(tenant_id,legal_entity_id,id,employment_id,type,principal,outstanding_principal,currency_code,status,installment_amount,installment_count,created_at) values($1,$2,$3,$4,$5,$6,$6,$7,'Draft',$8,$9,$10)", c, tx); Add(cmd, tenant.Value, request.LegalEntityId, id, request.EmploymentId, type, request.Principal, request.CurrencyCode.Trim().ToUpperInvariant(), DbValue(request.InstallmentAmount), DbValue(request.InstallmentCount), now); await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return new(id, request.EmploymentId, type, request.Principal, request.Principal, request.CurrencyCode.Trim().ToUpperInvariant(), "Draft");
    }

    public async Task<EmployeeReceivableDto> ApproveReceivableAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var cmd = new NpgsqlCommand("update business_payroll.employee_receivable set status='Approved',approved_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning employment_id,type,principal,outstanding_principal,currency_code,status", c, tx); Add(cmd, tenant.Value, legalEntityId, id); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.receivable_not_found_or_not_draft"); var receivable = new EmployeeReceivableDto(id, r.GetGuid(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetString(4).Trim(), r.GetString(5)); await r.CloseAsync(); await tx.CommitAsync(ct); return receivable;
    }

    public async Task<ReceivableDisbursementDto> RequestReceivableDisbursementAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var command = new NpgsqlCommand("""
            insert into business_payroll.receivable_disbursement
                (tenant_id,legal_entity_id,id,receivable_id,amount,currency_code,idempotency_key,status,requested_at,created_at,updated_at)
            select $1,$2,gen_random_uuid(),r.id,r.principal,r.currency_code,$4,'Requested',now(),now(),now()
            from business_payroll.employee_receivable r
            where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3 and r.status='Approved'
            on conflict (tenant_id,receivable_id,idempotency_key) do update set updated_at=now()
            returning id,receivable_id,amount,currency_code,status,provider_reference
            """, c, tx);
        Add(command, tenant.Value, legalEntityId, id, $"payroll:receivable-disbursement:{tenant.Value:D}:{id:D}");
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.receivable_not_approved");
        var result = new ReceivableDisbursementDto(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5));
        await reader.CloseAsync();
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ReceivableDisbursementDto> CompleteReceivableDisbursementAsync(TenantId tenant, Guid legalEntityId, Guid disbursementId, string? providerReference, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        decimal principal; decimal? installmentAmount; int? installmentCount; Guid receivableId;
        await using (var load = new NpgsqlCommand("select d.receivable_id,d.amount,d.currency_code,d.status,r.principal,r.installment_amount,r.installment_count from business_payroll.receivable_disbursement d join business_payroll.employee_receivable r on r.tenant_id=d.tenant_id and r.legal_entity_id=d.legal_entity_id and r.id=d.receivable_id where d.tenant_id=$1 and d.legal_entity_id=$2 and d.id=$3 for update", c, tx))
        {
            Add(load, tenant.Value, legalEntityId, disbursementId); await using var reader = await load.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.disbursement_not_found");
            receivableId = reader.GetGuid(0); var status = reader.GetString(3); principal = reader.GetDecimal(4); installmentAmount = reader.IsDBNull(5) ? null : reader.GetDecimal(5); installmentCount = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            if (status == "Succeeded") { await reader.CloseAsync(); return await GetDisbursementAsync(c, tx, tenant, legalEntityId, disbursementId, ct); }
            if (status is not ("Requested" or "Pending")) throw new PayrollRepositoryException("payroll.disbursement_not_completable");
        }
        await using (var complete = new NpgsqlCommand("update business_payroll.receivable_disbursement set status='Succeeded',provider_reference=$4,completed_at=now(),updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Requested','Pending')", c, tx))
        { Add(complete, tenant.Value, legalEntityId, disbursementId, DbValue(providerReference)); await complete.ExecuteNonQueryAsync(ct); }
        await using (var mark = new NpgsqlCommand("update business_payroll.employee_receivable set status='Disbursed',disbursed_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Approved'", c, tx))
        { Add(mark, tenant.Value, legalEntityId, receivableId); if (await mark.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.receivable_not_approvable"); }
        var count = installmentCount.GetValueOrDefault(1); var scheduledAmount = installmentAmount.GetValueOrDefault(principal); var remaining = principal;
        for (var number = 1; number <= count && remaining > 0m; number++)
        {
            var amount = number == count ? remaining : Math.Min(scheduledAmount, remaining);
            await using var installment = new NpgsqlCommand("insert into business_payroll.recovery_installment(tenant_id,id,receivable_id,installment_number,due_date,amount,applied_amount,status) values($1,gen_random_uuid(),$2,$3,$4,$5,0,'Scheduled') on conflict (tenant_id,receivable_id,installment_number) do nothing", c, tx);
            Add(installment, tenant.Value, receivableId, number, DateOnly.FromDateTime(DateTime.UtcNow.Date).AddMonths(number), amount); await installment.ExecuteNonQueryAsync(ct); remaining -= amount;
        }
        await tx.CommitAsync(ct); return await GetDisbursementAfterCommitAsync(tenant, legalEntityId, disbursementId, ct);
    }

    public async Task<ReceivableDisbursementDto> FailReceivableDisbursementAsync(TenantId tenant, Guid legalEntityId, Guid disbursementId, string? failureCode, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var command = new NpgsqlCommand("update business_payroll.receivable_disbursement set status='Failed',failure_code=$4,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status in ('Requested','Pending')", c, tx); Add(command, tenant.Value, legalEntityId, disbursementId, DbValue(failureCode));
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.disbursement_not_failible");
        await tx.CommitAsync(ct); return await GetDisbursementAfterCommitAsync(tenant, legalEntityId, disbursementId, ct);
    }

    private async Task<ReceivableDisbursementDto> GetDisbursementAfterCommitAsync(TenantId tenant, Guid legalEntityId, Guid disbursementId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct);
        var result = await GetDisbursementAsync(scope.Connection, scope.Transaction, tenant, legalEntityId, disbursementId, ct);
        await scope.Transaction.CommitAsync(ct);
        return result;
    }

    public async Task<PayrollLoanActionDto> ApplyLoanActionAsync(TenantId tenant, Guid legalEntityId, Guid receivableId, Guid actorId, PayrollLoanActionRequest request, CancellationToken ct)
    {
        var action = request.Action.Trim();
        if (action is not ("Pause" or "Resume" or "Reschedule" or "ExternalRepayment" or "EarlySettlement" or "WriteOff")) throw new PayrollRepositoryException("payroll.loan_action_invalid");
        var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"payroll:loan-action:{tenant.Value:D}:{receivableId:D}:{action}:{effectiveDate:yyyyMMdd}" : request.IdempotencyKey.Trim();
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using (var existing = new NpgsqlCommand("select id,action,amount,effective_date,external_reference,status from business_payroll.payroll_loan_action where tenant_id=$1 and idempotency_key=$2", c, tx))
        {
            Add(existing, tenant.Value, key); await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) { var result = new PayrollLoanActionDto(reader.GetGuid(0), receivableId, reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result; }
        }
        decimal outstanding; string status; decimal? installmentAmount;
        await using (var load = new NpgsqlCommand("select outstanding_principal,status,installment_amount from business_payroll.employee_receivable where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", c, tx))
        { Add(load, tenant.Value, legalEntityId, receivableId); await using var reader = await load.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.receivable_not_found"); outstanding = reader.GetDecimal(0); status = reader.GetString(1); installmentAmount = reader.IsDBNull(2) ? null : reader.GetDecimal(2); await reader.CloseAsync(); }
        var amount = request.Amount;
        if (action == "Pause")
        {
            if (status != "Disbursed") throw new PayrollRepositoryException("payroll.loan_not_active");
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.employee_receivable set status='Paused' where tenant_id=$1 and id=$2", ct, tenant.Value, receivableId);
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.recovery_installment set status='Paused' where tenant_id=$1 and receivable_id=$2 and status='Scheduled'", ct, tenant.Value, receivableId);
        }
        else if (action == "Resume")
        {
            if (status != "Paused") throw new PayrollRepositoryException("payroll.loan_not_paused");
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.employee_receivable set status='Disbursed' where tenant_id=$1 and id=$2", ct, tenant.Value, receivableId);
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.recovery_installment set status='Scheduled' where tenant_id=$1 and receivable_id=$2 and status='Paused'", ct, tenant.Value, receivableId);
        }
        else if (action == "Reschedule")
        {
            if (status is not ("Disbursed" or "Paused")) throw new PayrollRepositoryException("payroll.loan_not_reschedulable");
            if (request.NewDueDate is null && request.NewInstallmentAmount is null) throw new PayrollRepositoryException("payroll.loan_reschedule_details_required");
            if (request.NewInstallmentAmount is not null) installmentAmount = request.NewInstallmentAmount;
            await using var reschedule = new NpgsqlCommand("update business_payroll.recovery_installment set due_date=coalesce($4,due_date),amount=coalesce($5,amount) where tenant_id=$1 and receivable_id=$2 and status in ('Scheduled','Paused') and installment_number=(select min(installment_number) from business_payroll.recovery_installment where tenant_id=$1 and receivable_id=$2 and status in ('Scheduled','Paused'))", c, tx);
            Add(reschedule, tenant.Value, receivableId, request.NewDueDate is null ? DBNull.Value : request.NewDueDate.Value, request.NewInstallmentAmount is null ? DBNull.Value : request.NewInstallmentAmount.Value); if (await reschedule.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.loan_schedule_missing");
            if (request.NewInstallmentAmount is not null) await ExecuteNonQueryAsync(c, tx, "update business_payroll.employee_receivable set installment_amount=$3 where tenant_id=$1 and id=$2", ct, tenant.Value, receivableId, request.NewInstallmentAmount.Value);
        }
        else if (action is "ExternalRepayment" or "EarlySettlement")
        {
            amount ??= outstanding;
            if (amount <= 0m || amount > outstanding) throw new PayrollRepositoryException("payroll.loan_repayment_amount_invalid");
            var remaining = amount.Value;
            await using var installments = new NpgsqlCommand("select id,amount-applied_amount from business_payroll.recovery_installment where tenant_id=$1 and receivable_id=$2 and status='Scheduled' and applied_amount<amount order by due_date,installment_number for update", c, tx);
            Add(installments, tenant.Value, receivableId); await using var reader = await installments.ExecuteReaderAsync(ct); var rows = new List<(Guid Id, decimal Available)>(); while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0), reader.GetDecimal(1))); await reader.CloseAsync();
            foreach (var row in rows) { if (remaining <= 0m) break; var applied = Math.Min(remaining, row.Available); await ExecuteNonQueryAsync(c, tx, "update business_payroll.recovery_installment set applied_amount=applied_amount+$3,status=case when applied_amount+$3>=amount then 'Applied' else status end where tenant_id=$1 and id=$2", ct, tenant.Value, row.Id, applied); remaining -= applied; }
            if (remaining > 0m) throw new PayrollRepositoryException("payroll.loan_schedule_insufficient");
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.employee_receivable set outstanding_principal=greatest(0,outstanding_principal-$3),status=case when outstanding_principal-$3<=0 then 'Settled' else status end where tenant_id=$1 and id=$2", ct, tenant.Value, receivableId, amount.Value);
            if (action == "EarlySettlement") await ExecuteNonQueryAsync(c, tx, "update business_payroll.recovery_installment set status='Cancelled' where tenant_id=$1 and receivable_id=$2 and status in ('Scheduled','Paused')", ct, tenant.Value, receivableId);
        }
        else if (action == "WriteOff")
        {
            amount ??= outstanding; if (amount != outstanding) throw new PayrollRepositoryException("payroll.loan_writeoff_must_close_balance");
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.employee_receivable set outstanding_principal=0,status='WrittenOff' where tenant_id=$1 and id=$2 and status in ('Disbursed','Paused')", ct, tenant.Value, receivableId);
            await ExecuteNonQueryAsync(c, tx, "update business_payroll.recovery_installment set status='Cancelled' where tenant_id=$1 and receivable_id=$2 and status in ('Scheduled','Paused')", ct, tenant.Value, receivableId);
        }
        var id = Guid.CreateVersion7();
        await using (var insert = new NpgsqlCommand("insert into business_payroll.payroll_loan_action(tenant_id,legal_entity_id,id,receivable_id,action,amount,effective_date,new_installment_amount,new_due_date,external_reference,reason,actor_id,idempotency_key,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,now())", c, tx))
        { Add(insert, tenant.Value, legalEntityId, id, receivableId, action, DbValue(amount), effectiveDate, DbValue(request.NewInstallmentAmount), DbValue(request.NewDueDate), DbValue(request.ExternalReference), DbValue(request.Reason), actorId, key); await insert.ExecuteNonQueryAsync(ct); }
        await tx.CommitAsync(ct); return new(id, receivableId, action, amount, effectiveDate, request.ExternalReference, "Applied");
    }

    private static async Task<ReceivableDisbursementDto> GetDisbursementAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select receivable_id,amount,currency_code,status,provider_reference from business_payroll.receivable_disbursement where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction); Add(command, tenant.Value, legalEntityId, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.disbursement_not_found"); return new(id, reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2).Trim(), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    public async Task<PayrollStatutorySettingsDto> GetStatutorySettingsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var command = new NpgsqlCommand("select id,legal_entity_id,tax_mode,insurance_mode,effective_from,version,created_at,manual_input_policy from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 and effective_from<=current_date order by effective_from desc,version desc limit 1", c, tx);
        Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(ct);
        var settings = await reader.ReadAsync(ct) ? ReadStatutorySettings(reader) : new(Guid.Empty, legalEntityId, "NotManaged", "NotManaged", DateOnly.MinValue, 0, DateTimeOffset.UnixEpoch, "Block"); await reader.CloseAsync(); await tx.CommitAsync(ct); return settings;
    }

    public async Task<IReadOnlyList<PayrollStatutorySettingsDto>> ListStatutorySettingsHistoryAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction; await using var command = new NpgsqlCommand("select id,legal_entity_id,tax_mode,insurance_mode,effective_from,version,created_at,manual_input_policy from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 order by effective_from desc,version desc limit 100", c, tx); Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PayrollStatutorySettingsDto>(); while (await reader.ReadAsync(ct)) result.Add(ReadStatutorySettings(reader)); await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<PayrollStatutorySettingsDto> UpdateStatutorySettingsAsync(TenantId tenant, Guid legalEntityId, Guid actor, UpdatePayrollStatutorySettingsRequest request, CancellationToken ct)
    {
        var taxMode = NormalizeStatutoryMode(request.TaxMode); var insuranceMode = NormalizeStatutoryMode(request.InsuranceMode); var manualInputPolicy = NormalizeManualInputPolicy(request.ManualInputPolicy); if (request.EffectiveFrom == default) throw new PayrollRepositoryException("payroll.settings_effective_date_required");
        await using var scope = await OpenTransactionAsync(tenant, ct); var c = scope.Connection; await using var tx = scope.Transaction;
        await using var current = new NpgsqlCommand("select coalesce(max(version),0) from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2", c, tx); Add(current, tenant.Value, legalEntityId); var version = Convert.ToInt32(await current.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); if (version != request.ExpectedVersion) throw new PayrollRepositoryException("payroll.settings_version_conflict");
        await using var finalized = new NpgsqlCommand("select exists(select 1 from business_payroll.payroll_run r join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.finalization_state='Finalized' and p.period_end >= $3)", c, tx); Add(finalized, tenant.Value, legalEntityId, request.EffectiveFrom); if (request.EffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow.Date) && (bool)(await finalized.ExecuteScalarAsync(ct) ?? false)) throw new PayrollRepositoryException("payroll.settings_historical_change_blocked");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; await using var insert = new NpgsqlCommand("insert into business_payroll.payroll_statutory_setting(tenant_id,legal_entity_id,id,tax_mode,insurance_mode,effective_from,version,manual_input_policy,actor_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", c, tx); Add(insert, tenant.Value, legalEntityId, id, taxMode, insuranceMode, request.EffectiveFrom, version + 1, manualInputPolicy, actor, now); await insert.ExecuteNonQueryAsync(ct);
        await NpgsqlAuditWriter.WriteAsync(c, tx, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, null, "payroll", "payroll.statutory_settings.updated", "payroll_statutory_setting", id, null, JsonSerializer.Serialize(new { taxMode, insuranceMode, request.EffectiveFrom, version = version + 1 }), null, "payroll-api", Guid.CreateVersion7(), null, now, null), ct);
        await tx.CommitAsync(ct); return new(id, legalEntityId, taxMode, insuranceMode, request.EffectiveFrom, version + 1, now, manualInputPolicy);
    }

    private async Task<TenantTransaction> OpenTransactionAsync(TenantId tenant, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            var transaction = await connection.BeginTransactionAsync(ct);
            await SetLocalTenantAsync(connection, transaction, tenant, ct);
            return new TenantTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<HrPayrollPeriodSnapshotContract> ReadSnapshotPayloadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid snapshotId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select payload::text from business_payroll.payroll_hr_snapshot where tenant_id=$1 and id=$2", connection, transaction); Add(command, tenant.Value, snapshotId);
        var payload = (string?)await command.ExecuteScalarAsync(ct) ?? throw new PayrollRepositoryException("payroll.hr_snapshot_missing");
        return JsonSerializer.Deserialize<HrPayrollPeriodSnapshotContract>(payload, JsonOptions) ?? throw new PayrollRepositoryException("payroll.hr_snapshot_invalid");
    }

    private static async Task<(string TaxMode, string InsuranceMode, int Version, string ManualInputPolicy)> ReadStatutoryModesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, DateOnly asOf, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select tax_mode,insurance_mode,version,manual_input_policy from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 and effective_from<=$3 order by effective_from desc,version desc limit 1", connection, transaction); Add(command, tenant.Value, legalEntityId, asOf); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3)) : ("NotManaged", "NotManaged", 0, "Block");
    }

    private static PayrollStatutorySettingsDto ReadStatutorySettings(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? "Block" : reader.GetString(7));
    private static string NormalizeStatutoryMode(string value) => value.Trim() is "NotManaged" or "Manual" or "Automatic" ? value.Trim() : throw new PayrollRepositoryException("payroll.settings_mode_invalid");
    private static string NormalizeManualInputPolicy(string value) => value.Trim() is "Block" or "Skip" ? value.Trim() : throw new PayrollRepositoryException("payroll.settings_manual_input_policy_invalid");
    private static string NormalizeHolidayAdjustment(string? value)
    {
        var normalized = value?.Trim();
        return normalized is "NextWorkingDay" or "PreviousWorkingDay" or "None" ? normalized : "NextWorkingDay";
    }

    private static DateOnly AdjustPayDate(DateOnly planned, string rule)
    {
        if (rule == "None") return planned;
        var result = planned;
        while (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            result = result.AddDays(rule == "PreviousWorkingDay" ? -1 : 1);
        return result;
    }

    private static PayrollComponentRule RuleFromVersion(string code, string category, string formula, int scale)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(formula) ? "{}" : formula);
        var root = document.RootElement;
        var kind = root.TryGetProperty("kind", out var kindProperty) ? kindProperty.GetString() : null;
        return kind switch
        {
            "Fixed" => new PayrollComponentRule(code, category, FixedAmount: root.GetProperty("amount").GetDecimal(), Scale: scale),
            "PercentageOf" => new PayrollComponentRule(code, category, PercentageOf: root.GetProperty("base").GetString() ?? "BASE", Percentage: root.GetProperty("percentage").GetDecimal(), Scale: scale),
            "Input" or "CountryProvider" or "Recovery" or null => new PayrollComponentRule(code, category, Scale: scale),
            _ => throw new PayrollRepositoryException($"payroll.component_formula_kind_invalid:{code}")
        };
    }

    private static bool IsCountryProviderFormula(string formula)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(formula) ? "{}" : formula);
        return document.RootElement.TryGetProperty("kind", out var kind) && string.Equals(kind.GetString(), "CountryProvider", StringComparison.Ordinal);
    }

    private static decimal ProrateBaseSalary(decimal assignmentBaseSalary, HrPayrollEmploymentInput employment, DateOnly periodStart, DateOnly periodEnd)
    {
        var days = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var compensation = employment.Compensation
            .Where(item => item.EffectiveFrom <= periodEnd && (item.EffectiveTo is null || item.EffectiveTo >= periodStart))
            .OrderBy(item => item.EffectiveFrom).ThenBy(item => item.Version).ToArray();
        var lastWorkingDate = employment.FinalExit?.EffectiveEndDate;
        decimal total = 0m;
        for (var offset = 0; offset < days; offset++)
        {
            var date = periodStart.AddDays(offset);
            if (lastWorkingDate is not null && date > lastWorkingDate.Value) continue;
            var applicable = compensation.LastOrDefault(item => item.EffectiveFrom <= date && (item.EffectiveTo is null || item.EffectiveTo >= date));
            total += (applicable?.Amount ?? assignmentBaseSalary) / days;
        }
        var unpaidDays = employment.Absence.UnexcusedAbsenceDays + employment.Leave
            .Where(item => item.PayrollTreatment.Contains("Unpaid", StringComparison.OrdinalIgnoreCase) || item.PayrollTreatment.Contains("NoPay", StringComparison.OrdinalIgnoreCase))
            .Sum(item => (int)Math.Ceiling(item.ChargedQuantityInPeriod));
        if (unpaidDays > 0) total -= total * Math.Min(unpaidDays, days) / days;
        return decimal.Round(Math.Max(0m, total), 2, MidpointRounding.ToEven);
    }

    private static async Task SetLocalTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static object Db(Guid? value) => value is null ? DBNull.Value : value.Value;
    private static object Db(DateOnly? value) => value is null ? DBNull.Value : value.Value;
    private static object DbValue(object? value) => value ?? DBNull.Value;
    private static void Add(NpgsqlCommand c, params object[] values) { foreach (var value in values) c.Parameters.AddWithValue(value); }
    private static async Task<int> ExecuteNonQueryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction); Add(command, values); return await command.ExecuteNonQueryAsync(ct);
    }

    private sealed class TenantTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; } = connection;
        public NpgsqlTransaction Transaction { get; } = transaction;
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}

public sealed class PayrollRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
