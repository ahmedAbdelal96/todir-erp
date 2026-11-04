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
        await using var c = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("select id,legal_entity_id,code,name,frequency,pay_day,currency_code,status from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 order by code", c);
        cmd.Parameters.AddWithValue(tenant.Value); cmd.Parameters.AddWithValue(legalEntityId);
        await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollGroupDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt16(5), r.GetString(6).Trim(), r.GetString(7)));
        return result;
    }

    public async Task<PayrollGroupDto> CreateGroupAsync(TenantId tenant, CreatePayrollGroupRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var frequency = request.Frequency.Trim();
        if (frequency is not ("Monthly" or "Weekly" or "Biweekly" or "SemiMonthly")) throw new PayrollRepositoryException("payroll.frequency_invalid");
        await using var c = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_group(tenant_id,legal_entity_id,id,code,name,frequency,pay_day,currency_code,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$9) returning status", c);
        Add(cmd, tenant.Value, request.LegalEntityId, id, request.Code.Trim(), request.Name.Trim(), frequency, request.PayDay, request.CurrencyCode.Trim().ToUpperInvariant(), now);
        var status = (string)(await cmd.ExecuteScalarAsync(ct) ?? "Active"); return new(id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), frequency, request.PayDay, request.CurrencyCode.Trim().ToUpperInvariant(), status);
    }

    public async Task<PayrollPeriodDto> CreatePeriodAsync(TenantId tenant, Guid legalEntityId, CreatePayrollPeriodRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await using var c = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_period(tenant_id,legal_entity_id,id,payroll_group_id,period_start,period_end,input_cutoff,calculation_date,planned_pay_date,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$10)", c);
        Add(cmd, tenant.Value, legalEntityId, id, request.PayrollGroupId, request.PeriodStart, request.PeriodEnd, request.InputCutoff, request.CalculationDate, request.PlannedPayDate, now); await cmd.ExecuteNonQueryAsync(ct);
        return new(id, request.PayrollGroupId, request.PeriodStart, request.PeriodEnd, request.InputCutoff, request.PlannedPayDate, "Open");
    }

    public async Task<IReadOnlyList<PayrollPeriodDto>> ListPeriodsAsync(TenantId tenant, Guid legalEntityId, Guid groupId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_start,period_end,input_cutoff,planned_pay_date,status from business_payroll.payroll_period where tenant_id=$1 and legal_entity_id=$2 and payroll_group_id=$3 order by period_start desc", c);
        Add(cmd, tenant.Value, legalEntityId, groupId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollPeriodDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.GetFieldValue<DateOnly>(3), r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateOnly>(5), r.GetString(6))); return result;
    }

    public async Task<IReadOnlyList<PayrollRunDto>> ListRunsAsync(TenantId tenant, Guid legalEntityId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); await using var c = await OpenAsync(tenant, ct);
        await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_id,run_number,run_type,calculation_state,finalization_state,payment_state,total_gross,total_deductions,total_net,employee_count from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 order by created_at desc offset $3 limit $4", c);
        Add(cmd, tenant.Value, legalEntityId, (page - 1) * pageSize, pageSize); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollRunDto>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetInt32(11))); return result;
    }

    public async Task<PayrollRunDto> CreateRunAsync(TenantId tenant, Guid legalEntityId, CreatePayrollRunRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var runType = request.RunType.Trim(); if (runType is not ("Regular" or "OffCycle" or "Retro" or "FinalSettlement")) throw new PayrollRepositoryException("payroll.run_type_invalid");
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_run(tenant_id,legal_entity_id,id,payroll_group_id,period_id,run_number,run_type,currency_code,created_at,updated_at) select $1,$2,$3,$4,$5,'PR-'||to_char(current_date,'YYYYMMDD')||'-'||right($3::text,6),$6,currency_code,$7,$7 from business_payroll.payroll_group where tenant_id=$1 and legal_entity_id=$2 and id=$4 returning run_number,currency_code", c);
        Add(cmd, tenant.Value, legalEntityId, id, request.PayrollGroupId, Db(request.PeriodId), runType, now); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.group_not_found"); var number = r.GetString(0); var currency = r.GetString(1).Trim();
        return new(id, request.PayrollGroupId, request.PeriodId, number, runType, "Draft", "Open", "NotReady", 0, 0, 0, 0);
    }

    public async Task<PayrollRunDto?> GetRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,payroll_group_id,period_id,run_number,run_type,calculation_state,finalization_state,payment_state,total_gross,total_deductions,total_net,employee_count from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 and id=$3", c); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0),r.GetGuid(1),r.IsDBNull(2)?null:r.GetGuid(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetDecimal(8),r.GetDecimal(9),r.GetDecimal(10),r.GetInt32(11)) : null;
    }

    public async Task<(PayrollRunDto Run, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly InputCutoff)?> GetRunContextAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select r.id,r.payroll_group_id,r.period_id,r.run_number,r.run_type,r.calculation_state,r.finalization_state,r.payment_state,r.total_gross,r.total_deductions,r.total_net,r.employee_count,p.period_start,p.period_end,p.input_cutoff from business_payroll.payroll_run r left join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3", c); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; var run = new PayrollRunDto(r.GetGuid(0),r.GetGuid(1),r.IsDBNull(2)?null:r.GetGuid(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetDecimal(8),r.GetDecimal(9),r.GetDecimal(10),r.GetInt32(11)); if (r.IsDBNull(12) || r.IsDBNull(13) || r.IsDBNull(14)) throw new PayrollRepositoryException("payroll.period_required"); return (run,r.GetFieldValue<DateOnly>(12),r.GetFieldValue<DateOnly>(13),r.GetFieldValue<DateOnly>(14));
    }

    public async Task<PayrollSnapshotDto> SaveHrSnapshotAsync(TenantId tenant, Guid legalEntityId, Guid runId, HrPayrollPeriodSnapshotContract snapshot, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(snapshot, JsonOptions); var id = Guid.CreateVersion7(); await using var c = await OpenAsync(tenant, ct); await using var tx = await c.BeginTransactionAsync(ct);
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
        await using var update = new NpgsqlCommand("update business_payroll.payroll_run set hr_snapshot_id=$4,calculation_state=case when calculation_state='Calculated' then 'Captured' else calculation_state end,updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and finalization_state='Open'", c, tx); Add(update, tenant.Value, legalEntityId, runId, id); if (await update.ExecuteNonQueryAsync(ct) != 1) throw new PayrollRepositoryException("payroll.run_not_open"); await tx.CommitAsync(ct); return new(id,runId,snapshot.PeriodStart,snapshot.PeriodEnd,snapshot.CutoffAt,snapshot.PayloadSha256,snapshot.Employments.Count);
    }

    public async Task<IReadOnlyList<PayrollEmployeeDto>> ListRunEmployeesAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select e.employment_id,e.status,e.gross,e.deductions,e.net,e.employer_cost,e.exception_code from business_payroll.payroll_run_employee e join business_payroll.payroll_run r on r.tenant_id=e.tenant_id and r.id=e.run_id where e.tenant_id=$1 and r.legal_entity_id=$2 and e.run_id=$3 order by e.employment_id", c); Add(cmd, tenant.Value, legalEntityId, runId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayrollEmployeeDto>(); while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0),r.GetString(1),r.GetDecimal(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5),r.IsDBNull(6)?null:r.GetString(6))); return result;
    }

    public async Task CalculateRunAsync(TenantId tenant, Guid legalEntityId, Guid runId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using var check = new NpgsqlCommand("select currency_code,finalization_state,hr_snapshot_id from business_payroll.payroll_run where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", c, tx); Add(check, tenant.Value, legalEntityId, runId); await using var cr = await check.ExecuteReaderAsync(ct); if (!await cr.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.run_not_found"); if (cr.GetString(1) != "Open") throw new PayrollRepositoryException("payroll.run_already_finalized"); var snapshotId = cr.IsDBNull(2) ? (Guid?)null : cr.GetGuid(2); await cr.CloseAsync(); if (snapshotId is null) throw new PayrollRepositoryException("payroll.hr_snapshot_missing");
        await using var clearLines = new NpgsqlCommand("delete from business_payroll.payroll_calculation_line where tenant_id=$1 and run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2)", c, tx); Add(clearLines, tenant.Value, runId); await clearLines.ExecuteNonQueryAsync(ct);
        await using var clearEmployees = new NpgsqlCommand("delete from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2", c, tx); Add(clearEmployees, tenant.Value, runId); await clearEmployees.ExecuteNonQueryAsync(ct);
        await using var insert = new NpgsqlCommand("insert into business_payroll.payroll_run_employee(tenant_id,id,run_id,employment_id,hr_snapshot_id,snapshot_employment_number,status,gross,deductions,net,employer_cost,calculated_at) select $1,gen_random_uuid(),r.id,(h.item->>'employmentId')::uuid,s.id,h.item->>'employmentNumber','Pending',0,0,0,0,now() from business_payroll.payroll_run r join business_payroll.payroll_hr_snapshot s on s.tenant_id=r.tenant_id and s.payroll_run_id=r.id and s.id=r.hr_snapshot_id cross join lateral jsonb_array_elements(s.payload->'employments') h(item) where r.tenant_id=$1 and r.id=$2 and r.legal_entity_id=$3", c, tx); Add(insert, tenant.Value, runId, legalEntityId); await insert.ExecuteNonQueryAsync(ct);
        var snapshotPayload = await ReadSnapshotPayloadAsync(c, tx, tenant, snapshotId.Value, ct);
        var statutoryModes = await ReadStatutoryModesAsync(c, tx, tenant, legalEntityId, snapshotPayload.PeriodEnd, ct);
        var components = new Dictionary<string, (Guid Id, string Category, string InputMode, string? ProviderCode)>(StringComparer.OrdinalIgnoreCase);
        await using (var componentCommand = new NpgsqlCommand("select c.id,c.code,c.category,c.input_mode,(select v.provider_code from business_payroll.payroll_component_version v where v.tenant_id=c.tenant_id and v.component_id=c.id and v.status='Published' order by v.effective_from desc limit 1) from business_payroll.payroll_component c where c.tenant_id=$1 and c.status='Active'", c, tx))
        { Add(componentCommand, tenant.Value); await using var reader = await componentCommand.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) components[reader.GetString(1)] = (reader.GetGuid(0), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)); await reader.CloseAsync(); }
        if (!components.ContainsKey("BASE")) throw new PayrollRepositoryException("payroll.component_invalid");
        var providerModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in components.Where(pair => string.Equals(pair.Value.InputMode, "Provider", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var domain = StatutoryDomain(provider.Key);
            var mode = domain == "Tax" ? statutoryModes.TaxMode : domain == "Insurance" ? statutoryModes.InsuranceMode : "Automatic";
            providerModes[provider.Key] = mode;
            if (mode == "NotManaged") components.Remove(provider.Key);
            else if (mode == "Automatic" && (string.IsNullOrWhiteSpace(provider.Value.ProviderCode) || countryProviders.Find(provider.Value.ProviderCode) is not { LegallyCertified: true }))
                throw new PayrollRepositoryException($"payroll.country_configuration_missing:{domain}");
        }
        var approvedInputs = new Dictionary<(Guid EmploymentId, string Code), decimal>();
        await using (var inputCommand = new NpgsqlCommand("select input.employment_id,component.code,sum(input.amount) from business_payroll.payroll_input input join business_payroll.payroll_component component on component.tenant_id=input.tenant_id and component.id=input.component_id where input.tenant_id=$1 and input.status='Approved' and input.input_date between $2 and $3 group by input.employment_id,component.code", c, tx))
        { Add(inputCommand, tenant.Value, snapshotPayload.PeriodStart, snapshotPayload.PeriodEnd); await using var reader = await inputCommand.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) approvedInputs[(reader.GetGuid(0), reader.GetString(1))] = reader.GetDecimal(2); await reader.CloseAsync(); }
        await using var employees = new NpgsqlCommand("select id,employment_id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 order by employment_id", c, tx); Add(employees, tenant.Value, runId); await using var employeeReader = await employees.ExecuteReaderAsync(ct); var employeeRows = new List<(Guid Id, Guid EmploymentId)>(); while (await employeeReader.ReadAsync(ct)) employeeRows.Add((employeeReader.GetGuid(0), employeeReader.GetGuid(1))); await employeeReader.CloseAsync();
        var rules = components.Where(pair => !string.Equals(pair.Key, "BASE", StringComparison.OrdinalIgnoreCase)).Select(pair => new PayrollComponentRule(pair.Key, pair.Value.Category)).ToArray();
        foreach (var row in employeeRows)
        {
            var employment = snapshotPayload.Employments.FirstOrDefault(item => item.EmploymentId == row.EmploymentId) ?? throw new PayrollRepositoryException("payroll.snapshot_population_mismatch");
            var compensation = employment.Compensation.Where(item => item.EffectiveFrom <= snapshotPayload.PeriodEnd && (item.EffectiveTo is null || item.EffectiveTo >= snapshotPayload.PeriodStart)).OrderBy(item => item.EffectiveFrom).ThenBy(item => item.Version).LastOrDefault();
            var inputs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in components.Keys.Where(code => !string.Equals(code, "BASE", StringComparison.OrdinalIgnoreCase)))
            {
                if (providerModes.TryGetValue(component, out var providerMode) && providerMode == "Manual" && !approvedInputs.ContainsKey((row.EmploymentId, component)))
                    throw new PayrollRepositoryException($"payroll.manual_statutory_input_missing:{component}");
                approvedInputs.TryGetValue((row.EmploymentId, component), out var amount);
                amount += employment.ApprovedAdjustments.Where(item => string.Equals(item.TypeCode, component, StringComparison.OrdinalIgnoreCase) && item.EffectiveFrom <= snapshotPayload.PeriodEnd && (item.EffectiveTo is null || item.EffectiveTo >= snapshotPayload.PeriodStart)).Sum(item => item.Amount);
                inputs[component] = amount;
            }
            try
            {
                var result = PayrollCalculationEngine.Calculate(new PayrollCalculationInput(compensation?.Amount ?? 0m, inputs, new Dictionary<string, decimal>()), rules);
                await using var updateEmployee = new NpgsqlCommand("update business_payroll.payroll_run_employee set status='Calculated',gross=$3,deductions=$4,net=$5,employer_cost=$6,exception_code=null,calculated_at=now() where tenant_id=$1 and id=$2", c, tx); Add(updateEmployee, tenant.Value, row.Id, result.Gross, result.EmployeeDeductions, result.Net, result.EmployerCost); await updateEmployee.ExecuteNonQueryAsync(ct);
                foreach (var line in result.Lines.Where(line => line.Amount != 0m || line.Category == "Earning"))
                {
                    if (!components.TryGetValue(line.Code, out var component)) throw new PayrollRepositoryException("payroll.component_invalid");
                    await using var lineCommand = new NpgsqlCommand("insert into business_payroll.payroll_calculation_line(tenant_id,id,run_employee_id,component_id,quantity,amount,source,trace) values($1,gen_random_uuid(),$2,$3,1,$4,$5,$6::jsonb)", c, tx); Add(lineCommand, tenant.Value, row.Id, component.Id, line.Amount, line.Source, JsonSerializer.Serialize(new { kind = "PayrollCalculation", line.Code, line.Explanation }, JsonOptions)); await lineCommand.ExecuteNonQueryAsync(ct);
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
        await using var c = await OpenAsync(tenant, ct); await using var tx = await c.BeginTransactionAsync(ct); await using var check = new NpgsqlCommand("select r.run_number,r.calculation_state,r.finalization_state,r.currency_code,r.hr_snapshot_id from business_payroll.payroll_run r where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3 for update", c, tx); Add(check, tenant.Value, legalEntityId, runId); await using var rr = await check.ExecuteReaderAsync(ct); if (!await rr.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.run_not_found"); var number=rr.GetString(0); var state=rr.GetString(1); var final=rr.GetString(2); var currency=rr.GetString(3).Trim(); var snapshotId=rr.IsDBNull(4)?(Guid?)null:rr.GetGuid(4); await rr.CloseAsync(); if (final != "Open") throw new PayrollRepositoryException("payroll.run_already_finalized"); if (state != "Calculated") throw new PayrollRepositoryException("payroll.run_not_ready"); if (snapshotId is null) throw new PayrollRepositoryException("payroll.hr_snapshot_missing");
        await using var population = new NpgsqlCommand("select (select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2),coalesce(jsonb_array_length((select payload from business_payroll.payroll_hr_snapshot where tenant_id=$1 and id=$3)->'employments'),0)", c, tx); Add(population, tenant.Value, runId, snapshotId.Value); await using var populationReader = await population.ExecuteReaderAsync(ct); await populationReader.ReadAsync(ct); var calculatedEmployees = populationReader.GetInt64(0); var snapshotEmployees = populationReader.GetInt32(1); await populationReader.CloseAsync(); if (calculatedEmployees != snapshotEmployees) throw new PayrollRepositoryException("payroll.snapshot_population_mismatch");
        await using var exceptions = new NpgsqlCommand("select count(*) from business_payroll.payroll_exception where tenant_id=$1 and run_id=$2 and severity='Blocking' and is_resolved=false union all select count(*) from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$2 and status='Exception'", c, tx); Add(exceptions, tenant.Value, runId); await using var exReader = await exceptions.ExecuteReaderAsync(ct); long blocking = 0; while (await exReader.ReadAsync(ct)) blocking += exReader.GetInt64(0); await exReader.CloseAsync(); if (blocking > 0) throw new PayrollRepositoryException("payroll.run_blocking_exceptions");
        await using var slip = new NpgsqlCommand("insert into business_payroll.payslip(tenant_id,legal_entity_id,id,run_employee_id,payslip_number,employment_id,gross,deductions,net,employer_cost,status,finalized_at,calculation_hash) select $1,$2,gen_random_uuid(),e.id,$3||'-'||right(e.id::text,8),e.employment_id,e.gross,e.deductions,e.net,e.employer_cost,'Finalized',now(),encode(sha256((e.gross::text||':'||e.deductions::text||':'||e.net::text||':'||$4::text)::bytea),'hex') from business_payroll.payroll_run_employee e where e.tenant_id=$1 and e.run_id=$4", c, tx); Add(slip, tenant.Value, legalEntityId, number, runId); await slip.ExecuteNonQueryAsync(ct);
        await using var lines = new NpgsqlCommand("insert into business_payroll.payslip_line(tenant_id,id,payslip_id,component_id,code,name,category,amount,trace) select $1,gen_random_uuid(),p.id,cl.component_id,pc.code,pc.name,pc.category,cl.amount,cl.trace from business_payroll.payslip p join business_payroll.payroll_calculation_line cl on cl.tenant_id=p.tenant_id and cl.run_employee_id=p.run_employee_id join business_payroll.payroll_component pc on pc.tenant_id=cl.tenant_id and pc.id=cl.component_id where p.tenant_id=$1 and p.legal_entity_id=$2 and p.status='Finalized' and p.run_employee_id in (select id from business_payroll.payroll_run_employee where tenant_id=$1 and run_id=$3)", c, tx); Add(lines, tenant.Value, legalEntityId, runId); await lines.ExecuteNonQueryAsync(ct);
        await using var pi = new NpgsqlCommand("insert into business_payroll.payment_instruction(tenant_id,legal_entity_id,id,run_id,employment_id,amount,currency_code,created_at) select $1,$2,gen_random_uuid(),e.run_id,e.employment_id,e.net,$3,now() from business_payroll.payroll_run_employee e where e.tenant_id=$1 and e.run_id=$4 on conflict (tenant_id,run_id,employment_id) do nothing", c, tx); Add(pi, tenant.Value, legalEntityId, currency, runId); await pi.ExecuteNonQueryAsync(ct);
        await using var consume = new NpgsqlCommand("update business_payroll.payroll_input input set status='Consumed',consumed_run_id=$2 where input.tenant_id=$1 and input.status='Approved' and exists(select 1 from business_payroll.payroll_run_employee e join business_payroll.payroll_run r on r.tenant_id=e.tenant_id and r.id=e.run_id join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where e.tenant_id=$1 and e.run_id=$2 and e.employment_id=input.employment_id and input.input_date between p.period_start and p.period_end)", c, tx); Add(consume, tenant.Value, runId); await consume.ExecuteNonQueryAsync(ct);
        var accountingFactId = Guid.CreateVersion7();
        await using var fact = new NpgsqlCommand("insert into business_payroll.accounting_fact(tenant_id,legal_entity_id,id,run_id,fact_type,total_amount,currency_code,idempotency_key,payload,created_at) select $1,$2,$3,$4,'PayrollPosted',total_net,$5,$6,jsonb_build_object('gross',total_gross,'deductions',total_deductions,'payable',total_net,'currencyCode',$5),now() from business_payroll.payroll_run where tenant_id=$1 and id=$4 on conflict (tenant_id,idempotency_key) do nothing", c, tx); Add(fact, tenant.Value, legalEntityId, accountingFactId, runId, currency, $"payroll:{tenant.Value:D}:{runId:D}"); await fact.ExecuteNonQueryAsync(ct);
        await using var finalizedEvent = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status) values($1,'PayrollFinalizedV1',jsonb_build_object('runId',$2,'legalEntityId',$3,'accountingFactId',$4,'currencyCode',$5),$6,now(),$2,$2,'payroll',null,'Pending') on conflict do nothing", c, tx); Add(finalizedEvent, Guid.CreateVersion7(), runId, legalEntityId, accountingFactId, currency, tenant.Value); await finalizedEvent.ExecuteNonQueryAsync(ct);
        await using var paymentEvent = new NpgsqlCommand("insert into platform.outbox_message(id,message_type,payload,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,status) values($1,'PayrollPaymentRequestedV1',jsonb_build_object('runId',$2,'legalEntityId',$3,'currencyCode',$4),$5,now(),$2,$2,'payroll',null,'Pending') on conflict do nothing", c, tx); Add(paymentEvent, Guid.CreateVersion7(), runId, legalEntityId, currency, tenant.Value); await paymentEvent.ExecuteNonQueryAsync(ct);
        await using var markFinalized = new NpgsqlCommand("update business_payroll.payroll_run set finalization_state='Finalized',payment_state='Ready',accounting_state='Ready',updated_at=now() where tenant_id=$1 and id=$2", c, tx); Add(markFinalized, tenant.Value, runId); await markFinalized.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<PayslipDto>> ListPayslipsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select id,payslip_number,employment_id,gross,deductions,net,employer_cost,status,finalized_at from business_payroll.payslip where tenant_id=$1 and legal_entity_id=$2 order by finalized_at desc nulls last limit 100", c); Add(cmd, tenant.Value, legalEntityId); await using var r = await cmd.ExecuteReaderAsync(ct); var result = new List<PayslipDto>(); while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0),r.GetString(1),r.GetGuid(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDecimal(6),r.GetString(7),r.IsDBNull(8)?null:r.GetFieldValue<DateTimeOffset>(8))); return result;
    }

    public async Task<PayrollInputDto> CreateInputAsync(TenantId tenant, CreatePayrollInputRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_payroll.payroll_input(tenant_id,legal_entity_id,id,employment_id,component_id,input_date,amount,currency_code,source,status,idempotency_key,notes,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,$11,$12) returning status", c);
        Add(cmd, tenant.Value, request.LegalEntityId, id, request.EmploymentId, request.ComponentId, request.InputDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.Source.Trim(), DbValue(request.IdempotencyKey?.Trim()), DbValue(request.Notes?.Trim()), now); var status = (string)(await cmd.ExecuteScalarAsync(ct) ?? "Draft"); return new(id, request.EmploymentId, request.ComponentId, request.InputDate, request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.Source.Trim(), status, request.Notes?.Trim());
    }

    public async Task<PayrollInputDto> ApproveInputAsync(TenantId tenant, Guid legalEntityId, Guid inputId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_payroll.payroll_input set status='Approved',approved_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning employment_id,component_id,input_date,amount,currency_code,source,status,notes", c); Add(cmd, tenant.Value, legalEntityId, inputId); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.input_not_found_or_not_draft"); return new(inputId,r.GetGuid(0),r.GetGuid(1),r.GetFieldValue<DateOnly>(2),r.GetDecimal(3),r.GetString(4).Trim(),r.GetString(5),r.GetString(6),r.IsDBNull(7)?null:r.GetString(7));
    }

    public async Task<PayrollInputDto?> GetInputAsync(TenantId tenant, Guid legalEntityId, Guid inputId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("select employment_id,component_id,input_date,amount,currency_code,source,status,notes from business_payroll.payroll_input where tenant_id=$1 and legal_entity_id=$2 and id=$3", c); Add(cmd, tenant.Value, legalEntityId, inputId); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(inputId,r.GetGuid(0),r.GetGuid(1),r.GetFieldValue<DateOnly>(2),r.GetDecimal(3),r.GetString(4).Trim(),r.GetString(5),r.GetString(6),r.IsDBNull(7)?null:r.GetString(7)) : null;
    }

    public async Task<EmployeeReceivableDto> CreateReceivableAsync(TenantId tenant, CreateReceivableRequest request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; var type = request.Type.Trim(); if (type is not ("Advance" or "Loan")) throw new PayrollRepositoryException("payroll.receivable_type_invalid");
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("insert into business_payroll.employee_receivable(tenant_id,legal_entity_id,id,employment_id,type,principal,outstanding_principal,currency_code,status,installment_amount,installment_count,created_at) values($1,$2,$3,$4,$5,$6,$6,$7,'Draft',$8,$9,$10)", c); Add(cmd, tenant.Value, request.LegalEntityId, id, request.EmploymentId, type, request.Principal, request.CurrencyCode.Trim().ToUpperInvariant(), DbValue(request.InstallmentAmount), DbValue(request.InstallmentCount), now); await cmd.ExecuteNonQueryAsync(ct); return new(id,request.EmploymentId,type,request.Principal,request.Principal,request.CurrencyCode.Trim().ToUpperInvariant(),"Draft");
    }

    public async Task<EmployeeReceivableDto> ApproveReceivableAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var cmd = new NpgsqlCommand("update business_payroll.employee_receivable set status='Approved',approved_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Draft' returning employment_id,type,principal,outstanding_principal,currency_code,status", c); Add(cmd, tenant.Value, legalEntityId, id); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new PayrollRepositoryException("payroll.receivable_not_found_or_not_draft"); return new(id,r.GetGuid(0),r.GetString(1),r.GetDecimal(2),r.GetDecimal(3),r.GetString(4).Trim(),r.GetString(5));
    }

    public async Task<ReceivableDisbursementDto> RequestReceivableDisbursementAsync(TenantId tenant, Guid legalEntityId, Guid id, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct);
        await using var tx = await c.BeginTransactionAsync(ct);
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

    public async Task<PayrollStatutorySettingsDto> GetStatutorySettingsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,tax_mode,insurance_mode,effective_from,version,created_at from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 and effective_from<=current_date order by effective_from desc,version desc limit 1", c);
        Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadStatutorySettings(reader) : new(Guid.Empty, legalEntityId, "NotManaged", "NotManaged", DateOnly.MinValue, 0, DateTimeOffset.UnixEpoch);
    }

    public async Task<IReadOnlyList<PayrollStatutorySettingsDto>> ListStatutorySettingsHistoryAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct)
    {
        await using var c = await OpenAsync(tenant, ct); await using var command = new NpgsqlCommand("select id,legal_entity_id,tax_mode,insurance_mode,effective_from,version,created_at from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 order by effective_from desc,version desc limit 100", c); Add(command, tenant.Value, legalEntityId); await using var reader = await command.ExecuteReaderAsync(ct); var result = new List<PayrollStatutorySettingsDto>(); while (await reader.ReadAsync(ct)) result.Add(ReadStatutorySettings(reader)); return result;
    }

    public async Task<PayrollStatutorySettingsDto> UpdateStatutorySettingsAsync(TenantId tenant, Guid legalEntityId, Guid actor, UpdatePayrollStatutorySettingsRequest request, CancellationToken ct)
    {
        var taxMode = NormalizeStatutoryMode(request.TaxMode); var insuranceMode = NormalizeStatutoryMode(request.InsuranceMode); if (request.EffectiveFrom == default) throw new PayrollRepositoryException("payroll.settings_effective_date_required");
        await using var c = await OpenAsync(tenant, ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using var current = new NpgsqlCommand("select coalesce(max(version),0) from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2", c, tx); Add(current, tenant.Value, legalEntityId); var version = Convert.ToInt32(await current.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); if (version != request.ExpectedVersion) throw new PayrollRepositoryException("payroll.settings_version_conflict");
        await using var finalized = new NpgsqlCommand("select exists(select 1 from business_payroll.payroll_run r join business_payroll.payroll_period p on p.tenant_id=r.tenant_id and p.id=r.period_id where r.tenant_id=$1 and r.legal_entity_id=$2 and r.finalization_state='Finalized' and p.period_end >= $3)", c, tx); Add(finalized, tenant.Value, legalEntityId, request.EffectiveFrom); if (request.EffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow.Date) && (bool)(await finalized.ExecuteScalarAsync(ct) ?? false)) throw new PayrollRepositoryException("payroll.settings_historical_change_blocked");
        var id = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow; await using var insert = new NpgsqlCommand("insert into business_payroll.payroll_statutory_setting(tenant_id,legal_entity_id,id,tax_mode,insurance_mode,effective_from,version,actor_id,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", c, tx); Add(insert, tenant.Value, legalEntityId, id, taxMode, insuranceMode, request.EffectiveFrom, version + 1, actor, now); await insert.ExecuteNonQueryAsync(ct);
        await NpgsqlAuditWriter.WriteAsync(c, tx, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, null, "payroll", "payroll.statutory_settings.updated", "payroll_statutory_setting", id, null, JsonSerializer.Serialize(new { taxMode, insuranceMode, request.EffectiveFrom, version = version + 1 }), null, "payroll-api", Guid.CreateVersion7(), null, now, null), ct);
        await tx.CommitAsync(ct); return new(id, legalEntityId, taxMode, insuranceMode, request.EffectiveFrom, version + 1, now);
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
            command.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await command.ExecuteNonQueryAsync(ct);
            return connection;
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

    private static async Task<(string TaxMode, string InsuranceMode)> ReadStatutoryModesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, DateOnly asOf, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select tax_mode,insurance_mode from business_payroll.payroll_statutory_setting where tenant_id=$1 and legal_entity_id=$2 and effective_from<=$3 order by effective_from desc,version desc limit 1", connection, transaction); Add(command, tenant.Value, legalEntityId, asOf); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetString(1)) : ("NotManaged", "NotManaged");
    }

    private static PayrollStatutorySettingsDto ReadStatutorySettings(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6));
    private static string NormalizeStatutoryMode(string value) => value.Trim() is "NotManaged" or "Manual" or "Automatic" ? value.Trim() : throw new PayrollRepositoryException("payroll.settings_mode_invalid");
    private static string StatutoryDomain(string code) => code.Contains("INSURANCE", StringComparison.OrdinalIgnoreCase) || code.Contains("SOCIAL", StringComparison.OrdinalIgnoreCase) ? "Insurance" : code.Contains("TAX", StringComparison.OrdinalIgnoreCase) ? "Tax" : "Tax";

    private static object Db(Guid? value) => value is null ? DBNull.Value : value.Value;
    private static object DbValue(object? value) => value ?? DBNull.Value;
    private static void Add(NpgsqlCommand c, params object[] values) { foreach (var value in values) c.Parameters.AddWithValue(value); }
}

public sealed class PayrollRepositoryException(string code) : Exception(code) { public string Code { get; } = code; }
