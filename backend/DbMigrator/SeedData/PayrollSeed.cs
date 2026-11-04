namespace DbMigrator.SeedData;

public static class PayrollSeed
{
    public static async Task<PayrollSeedSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var groupId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:group:monthly");
        await context.ExecuteAsync("insert into business_payroll.payroll_group(tenant_id,legal_entity_id,id,code,name,frequency,pay_day,cutoff_rule,holiday_adjustment,currency_code,status,version,created_at,updated_at) values($1,$2,$3,'MONTHLY-10','الرواتب الشهرية - يوم 10','Monthly',10,'{\"kind\":\"PeriodEnd\"}'::jsonb,'NextWorkingDay','EGP','Active',1,$4,$4) on conflict(tenant_id,id) do update set name=excluded.name,status='Active',updated_at=excluded.updated_at", ct, context.Tenant.Id, context.EntityId, groupId, context.AsOf);
        var settingsId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:statutory-settings:default");
        await context.ExecuteAsync("insert into business_payroll.payroll_statutory_setting(tenant_id,legal_entity_id,id,tax_mode,insurance_mode,effective_from,version,created_at) values($1,$2,$3,'NotManaged','NotManaged','2025-01-01',1,$4) on conflict(tenant_id,legal_entity_id,effective_from) do update set tax_mode='NotManaged',insurance_mode='NotManaged'", ct, context.Tenant.Id, context.EntityId, settingsId, context.AsOf);
        var currentPeriodId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:period:2026-10");
        await context.ExecuteAsync("insert into business_payroll.payroll_period(tenant_id,legal_entity_id,id,payroll_group_id,period_start,period_end,input_cutoff,calculation_date,planned_pay_date,status,created_at,updated_at) values($1,$2,$3,$4,'2026-10-01','2026-10-31','2026-10-31','2026-11-01','2026-11-10','Open',$5,$5) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, context.EntityId, currentPeriodId, groupId, context.AsOf);
        var priorPeriodId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:period:2026-09");
        await context.ExecuteAsync("insert into business_payroll.payroll_period(tenant_id,legal_entity_id,id,payroll_group_id,period_start,period_end,input_cutoff,calculation_date,planned_pay_date,status,created_at,updated_at) values($1,$2,$3,$4,'2026-09-01','2026-09-30','2026-09-30','2026-10-01','2026-10-10','Finalized',$5,$5) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, context.EntityId, priorPeriodId, groupId, context.AsOf);

        var baseComponent = Component(context.Tenant.Id, "BASE"); var transport = Component(context.Tenant.Id, "TRANSPORT"); var tax = Component(context.Tenant.Id, "TAX"); var recovery = Component(context.Tenant.Id, "LOAN-RECOVERY");
        await ComponentAsync(context, baseComponent, "BASE", "الراتب الأساسي", "Earning", "Credit", "Formula", ct);
        await ComponentAsync(context, transport, "TRANSPORT", "بدل انتقالات", "Earning", "Credit", "Manual", ct);
        await ComponentAsync(context, tax, "TAX", "ضريبة كسب العمل", "EmployeeDeduction", "Debit", "Provider", ct);
        await ComponentAsync(context, recovery, "LOAN-RECOVERY", "استرداد سلفة أو قرض", "Recovery", "Debit", "Formula", ct);
        var versionIds = new[] { baseComponent, transport, tax, recovery }.Select(id => ComponentVersion(context.Tenant.Id, id)).ToArray();
        for (var i = 0; i < versionIds.Length; i++) await context.ExecuteAsync("insert into business_payroll.payroll_component_version(tenant_id,id,component_id,effective_from,formula,rounding_scale,provider_code,status,created_at) values($1,$2,$3,'2025-01-01',$4::jsonb,2,$5,'Published',$6) on conflict(tenant_id,id) do update set provider_code=excluded.provider_code,formula=excluded.formula,status='Published'", ct, context.Tenant.Id, versionIds[i], new[] { baseComponent, transport, tax, recovery }[i], i == 0 ? "{\"kind\":\"BaseSalary\"}" : i == 1 ? "{\"kind\":\"Input\"}" : i == 2 ? "{\"kind\":\"CountryProvider\",\"provider\":\"EG\"}" : "{\"kind\":\"Recovery\"}", i == 2 ? "EG" : (object)DBNull.Value, context.AsOf);
        var structureId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:structure:standard"); var structureVersionId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:structure:standard:v1");
        await context.ExecuteAsync("insert into business_payroll.salary_structure(tenant_id,legal_entity_id,id,code,name,status,created_at,updated_at) values($1,$2,$3,'STANDARD-EG','هيكل الرواتب القياسي','Active',$4,$4) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, context.EntityId, structureId, context.AsOf);
        await context.ExecuteAsync("insert into business_payroll.salary_structure_version(tenant_id,id,salary_structure_id,version_number,effective_from,status,currency_code,created_at) values($1,$2,$3,1,'2025-01-01','Published','EGP',$4) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, structureVersionId, structureId, context.AsOf);
        foreach (var (component, version, priority) in new[] { (baseComponent, versionIds[0], (short)10), (transport, versionIds[1], (short)20), (tax, versionIds[2], (short)90), (recovery, versionIds[3], (short)95) })
            await context.ExecuteAsync("insert into business_payroll.salary_structure_component(tenant_id,id,structure_version_id,component_id,component_version_id,priority) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, SeedIds.Stable($"{context.Tenant.Id:D}:payroll:structure-component:{component:D}"), structureVersionId, component, version, priority);

        var count = context.Tenant.Id == SeedIds.AtlasTenant ? 80 : 40; var assignments = 0;
        for (var index = 1; index <= count; index++)
        {
            var employmentId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:employment:{index:0000}:{(index == count ? 2 : 1)}");
            var assignmentId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:assignment:{index:0000}"); var from = index == count ? new DateOnly(2025, 2, 1) : new DateOnly(2025, 1, 1); var salary = 15000m + index * 125m;
            await context.ExecuteAsync("insert into business_payroll.employee_payroll_assignment(tenant_id,legal_entity_id,id,employment_id,payroll_group_id,salary_structure_version_id,effective_from,base_salary,currency_code,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,'EGP',$9) on conflict(tenant_id,id) do update set base_salary=excluded.base_salary,effective_from=excluded.effective_from", ct, context.Tenant.Id, context.EntityId, assignmentId, employmentId, groupId, structureVersionId, from, salary, context.AsOf); assignments++;
        }
        var inputId = SeedIds.Stable($"{context.Tenant.Id:D}:payroll:input:transport:2026-10");
        var employeeOneEmployment = SeedIds.Stable($"{context.Tenant.Id:D}:hr:employment:0001:1");
        await context.ExecuteAsync("insert into business_payroll.payroll_input(tenant_id,legal_entity_id,id,employment_id,component_id,input_date,amount,currency_code,source,status,idempotency_key,notes,created_at,approved_at) values($1,$2,$3,$4,$5,'2026-10-15',1500,'EGP','HRAdjustment','Approved',$6,'بدل انتقالات معتمد للدورة الحالية.',$7,$7) on conflict(tenant_id,id) do nothing", ct, context.Tenant.Id, context.EntityId, inputId, employeeOneEmployment, transport, $"seed-payroll-input:{inputId:D}", context.AsOf);
        return new(1, 2, assignments, 4, 1);
    }

    private static async Task ComponentAsync(SeedContext context, Guid id, string code, string name, string category, string direction, string mode, CancellationToken ct) => await context.ExecuteAsync("insert into business_payroll.payroll_component(tenant_id,id,code,name,category,direction,input_mode,status,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,'Active',$8,$8) on conflict(tenant_id,id) do update set name=excluded.name,status='Active',updated_at=excluded.updated_at", ct, context.Tenant.Id, id, code, name, category, direction, mode, context.AsOf);
    private static Guid Component(Guid tenant, string code) => SeedIds.Stable($"{tenant:D}:payroll:component:{code}");
    private static Guid ComponentVersion(Guid tenant, Guid component) => SeedIds.Stable($"{tenant:D}:payroll:component-version:{component:D}:v1");
}

public sealed record PayrollSeedSummary(int Groups, int Periods, int Assignments, int Components, int Inputs);
