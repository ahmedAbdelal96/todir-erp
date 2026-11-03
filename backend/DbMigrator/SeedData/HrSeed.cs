using Modules.Hr.Entities;
using Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Security.Cryptography;

namespace DbMigrator.SeedData;

public static class HrSeed
{
    private static readonly string[] GivenNamesAr = ["مريم", "أحمد", "سلمى", "محمود", "نور", "يوسف", "ندى", "عمر", "آية", "مصطفى", "ياسمين", "كريم"];
    private static readonly string[] FamilyNamesAr = ["حسن", "علي", "إبراهيم", "عبدالله", "محمود", "السيد", "منصور", "عبدالرحمن"];
    private static readonly string[] GivenNamesEn = ["Mariam", "Ahmed", "Salma", "Mahmoud", "Nour", "Youssef", "Nada", "Omar", "Aya", "Mostafa", "Yasmin", "Karim"];
    private static readonly string[] FamilyNamesEn = ["Hassan", "Ali", "Ibrahim", "Abdallah", "Mahmoud", "Elsayed", "Mansour", "Abdelrahman"];
    private static readonly (string Code, string Ar, string En)[] Departments =
    [
        ("EXEC", "الإدارة", "Management"), ("SALES", "المبيعات", "Sales"),
        ("FIN", "المالية", "Finance"), ("OPS", "العمليات", "Operations"),
        ("HR", "الموارد البشرية", "Human Resources"), ("PROC", "المشتريات", "Purchasing")
    ];
    private static readonly (string Code, string Ar, string En)[] Positions =
    [
        ("MGR", "مدير", "Manager"), ("SALES-REP", "مندوب مبيعات", "Sales Representative"),
        ("ACCOUNTANT", "محاسب", "Accountant"), ("STOREKEEPER", "أمين مخزن", "Storekeeper"),
        ("HR-SPECIALIST", "أخصائي موارد بشرية", "HR Specialist"), ("BUYER", "مسؤول مشتريات", "Purchasing Officer")
    ];

    public static async Task<HrSeedSummary> SeedAsync(SeedContext context, CancellationToken ct)
    {
        await SeedDocumentRequirementsAsync(context, ct);
        var count = context.Tenant.Id == SeedIds.AtlasTenant ? 80 : 40;
        var compensationRecords = 0;
        var insuranceDrafts = 0;
        var qualificationRecords = 0;
        var credentialRecords = 0;
        foreach (var (department, index) in Departments.Select((value, index) => (value, index)))
        {
            await context.ExecuteAsync("insert into business_hr.department(tenant_id,id,legal_entity_id,code,name_ar,name_en,parent_id,status,version,created_at) values($1,$2,$3,$4,$5,$6,null,'Active',1,$7) on conflict(tenant_id,id) do update set code=excluded.code,name_ar=excluded.name_ar,name_en=excluded.name_en,status='Active',version=business_hr.department.version+case when (business_hr.department.code,business_hr.department.name_ar,business_hr.department.name_en,business_hr.department.status) is distinct from (excluded.code,excluded.name_ar,excluded.name_en,excluded.status) then 1 else 0 end", ct,
                context.Tenant.Id, DepartmentId(context.Tenant.Id, index), context.EntityId, department.Code, department.Ar, department.En, context.AsOf);
        }
        foreach (var (position, index) in Positions.Select((value, index) => (value, index)))
        {
            await context.ExecuteAsync("insert into business_hr.position(tenant_id,id,legal_entity_id,code,title_ar,title_en,status,version,created_at) values($1,$2,$3,$4,$5,$6,'Active',1,$7) on conflict(tenant_id,id) do update set code=excluded.code,title_ar=excluded.title_ar,title_en=excluded.title_en,status='Active',version=business_hr.position.version+case when (business_hr.position.code,business_hr.position.title_ar,business_hr.position.title_en,business_hr.position.status) is distinct from (excluded.code,excluded.title_ar,excluded.title_en,excluded.status) then 1 else 0 end", ct,
                context.Tenant.Id, PositionId(context.Tenant.Id, index), context.EntityId, position.Code, position.Ar, position.En, context.AsOf);
        }
        var calendarId = CalendarId(context.Tenant.Id);
        await context.ExecuteAsync("insert into business_hr.work_calendar(tenant_id,id,legal_entity_id,code,name_ar,name_en,time_zone_id,status,version,created_by,created_at) values($1,$2,$3,'OFFICE','جدول العمل المعتاد','Standard office schedule','Africa/Cairo','Active',1,$4,$5) on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, calendarId, context.EntityId, context.AdministratorId, context.AsOf);
        for (short day = 0; day < 7; day++)
        {
            var working = day <= 4;
            await context.ExecuteAsync("insert into business_hr.work_calendar_weekly_pattern(tenant_id,calendar_id,day_of_week,is_working_day,local_start,local_end,break_minutes) values($1,$2,$3,$4,$5,$6,$7) on conflict(tenant_id,calendar_id,day_of_week) do nothing", ct,
                context.Tenant.Id, calendarId, day, working, working ? new TimeOnly(9, 0) : (object)DBNull.Value, working ? new TimeOnly(17, 0) : (object)DBNull.Value, working ? (short)60 : (short)0);
        }
        var dayShiftId = ShiftId(context.Tenant.Id, "day"); var nightShiftId = ShiftId(context.Tenant.Id, "night");
        await context.ExecuteAsync("insert into business_hr.shift_definition(tenant_id,id,legal_entity_id,code,name_ar,name_en,start_local,end_local,is_overnight,break_minutes,grace_minutes,status,version,created_by,created_at) values($1,$2,$3,'DAY','وردية صباحية','Day shift','09:00','17:00',false,60,10,'Active',1,$4,$5),($1,$6,$3,'NIGHT','وردية ليلية','Night shift','22:00','06:00',true,45,5,'Active',1,$4,$5) on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, dayShiftId, context.EntityId, context.AdministratorId, context.AsOf, nightShiftId);
        var annualLeaveTypeId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-type:company-annual");
        var annualLeavePolicyId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-policy:company-annual:v1");
        await context.ExecuteAsync("insert into business_hr.leave_type(id,tenant_id,legal_entity_id,code,name_ar,name_en,category,unit,country_code,country_provider_code,status,created_by,created_at) values($1,$2,$3,'COMPANY-ANNUAL','إجازة سنوية حسب سياسة الشركة','Company annual leave policy','Annual','Days',null,null,'Active',$4,$5) on conflict(tenant_id,id) do nothing", ct,
            annualLeaveTypeId, context.Tenant.Id, context.EntityId, context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_hr.leave_policy(id,tenant_id,legal_entity_id,leave_type_id,version,effective_from,effective_to,counting_method,approval_required,partial_day_allowed,negative_balance_policy,negative_balance_limit,payroll_treatment,accrual_rule,carryover_rule,eligibility_rules,country_provider_code,created_by,created_at) values($1,$2,$3,$4,1,'2025-01-01',null,'WorkingDays',true,true,'Disallow',0,'Paid','{\"mode\":\"Monthly\",\"amount\":1.75,\"frequency\":\"Monthly\"}'::jsonb,'{\"quantity\":5,\"expiryMonths\":6}'::jsonb,'{\"employmentTypes\":[]}'::jsonb,null,$5,$6) on conflict(tenant_id,id) do nothing", ct,
            annualLeavePolicyId, context.Tenant.Id, context.EntityId, annualLeaveTypeId, context.AdministratorId, context.AsOf);

        for (var index = 1; index <= count; index++)
        {
            var person = Person(context.Tenant.Id, index);
            await context.ExecuteAsync("insert into business_hr.employee(tenant_id,id,arabic_name,english_name,preferred_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,membership_id,created_by,created_at,version) values($1,$2,$3,$4,null,$3,$5,$6,$7,'Active',$8,$9,$10,1) on conflict(tenant_id,id) do update set arabic_name=excluded.arabic_name,english_name=excluded.english_name,display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,arabic_name_normalized=excluded.arabic_name_normalized,english_name_normalized=excluded.english_name_normalized,membership_id=case when business_hr.employee.membership_id is null and not exists(select 1 from business_hr.employee linked where linked.tenant_id=excluded.tenant_id and linked.membership_id=excluded.membership_id and linked.id<>excluded.id) then excluded.membership_id else business_hr.employee.membership_id end,status='Active',version=business_hr.employee.version+case when (business_hr.employee.arabic_name,business_hr.employee.english_name,business_hr.employee.status) is distinct from (excluded.arabic_name,excluded.english_name,excluded.status) or business_hr.employee.membership_id is null and not exists(select 1 from business_hr.employee linked where linked.tenant_id=excluded.tenant_id and linked.membership_id=excluded.membership_id and linked.id<>excluded.id) and excluded.membership_id is not null then 1 else 0 end", ct,
                context.Tenant.Id, person.EmployeeId, person.ArabicName, person.EnglishName, Normalize(person.ArabicName), Normalize(person.ArabicName), Normalize(person.EnglishName), index == 1 ? SeedIds.Membership(context.Tenant.Id, "hr-manager") : null, context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("insert into business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at) values($1,$2,$3,$4,$5,$6,$7,'Active',$8) on conflict(tenant_id,employee_id) do update set display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,arabic_name_normalized=excluded.arabic_name_normalized,english_name=excluded.english_name,english_name_normalized=excluded.english_name_normalized,status='Active',projected_at=excluded.projected_at", ct,
                context.Tenant.Id, person.EmployeeId, person.ArabicName, Normalize(person.ArabicName), Normalize(person.ArabicName), person.EnglishName, Normalize(person.EnglishName), context.AsOf);

            var isRehireScenario = index == count;
            var employmentId = EmploymentId(context.Tenant.Id, index, isRehireScenario ? 2 : 1);
            var employmentNumber = $"EMP-{(isRehireScenario ? count + 1 : index):000000}";
            var startDate = new DateOnly(2021 + index % 5, 1 + index % 12, 1 + index % 20);
            var effectiveDate = startDate < new DateOnly(2025, 1, 1) ? new DateOnly(2025, 1, 1) : startDate;
            if (isRehireScenario)
            {
                var formerEmploymentId = EmploymentId(context.Tenant.Id, index, 1);
                var formerStartDate = new DateOnly(2021, 3, 1);
                var formerEndDate = new DateOnly(2024, 12, 31);
                await UpsertEmploymentAsync(context, person.EmployeeId, formerEmploymentId, $"EMP-{count:000000}", "Terminated", formerStartDate, formerEndDate, ct);
                await AddEventAsync(context, index, person.EmployeeId, formerEmploymentId, "EmploymentCreated", formerStartDate, new { employmentNumber = $"EMP-{count:000000}", status = "Terminated" }, ct);
                await UpsertContractAsync(context, formerEmploymentId, ContractId(context.Tenant.Id, index, 1), "Expired", formerStartDate, formerEndDate, null, ct);
                startDate = new DateOnly(2025, 2, 1);
                await UpsertEmploymentAsync(context, person.EmployeeId, employmentId, employmentNumber, "Active", startDate, null, ct);
                await AddEventAsync(context, 3000 + index, person.EmployeeId, employmentId, "Rehired", startDate, new { priorEmploymentId = formerEmploymentId, employmentNumber }, ct);
                await UpsertContractAsync(context, employmentId, ContractId(context.Tenant.Id, index, 2), "Active", startDate, null, null, ct);
            }
            else
            {
                await UpsertEmploymentAsync(context, person.EmployeeId, employmentId, employmentNumber, "Active", startDate, null, ct);
                await AddEventAsync(context, index, person.EmployeeId, employmentId, "EmploymentCreated", startDate, new { employmentNumber, status = "Active" }, ct);
                if (index == 2)
                {
                    var firstContractId = ContractId(context.Tenant.Id, index, 1);
                    var firstContractEnd = startDate.AddYears(1).AddDays(-1);
                    var renewalStart = firstContractEnd.AddDays(1);
                    await UpsertContractAsync(context, employmentId, firstContractId, "Superseded", startDate, firstContractEnd, null, ct);
                    await UpsertContractAsync(context, employmentId, ContractId(context.Tenant.Id, index, 2), "Active", renewalStart, null, firstContractId, ct);
                    await AddEventAsync(context, 4000 + index, person.EmployeeId, employmentId, "ContractRenewed", renewalStart, new { previousContractId = firstContractId }, ct);
                }
                else await UpsertContractAsync(context, employmentId, ContractId(context.Tenant.Id, index, 1), "Active", startDate, null, null, ct);
            }

            await context.ExecuteAsync("insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,reason,created_by,created_at) values($1,$2,$3,$4,$5,'2025-01-01','Opening',12,'DevelopmentSeed','opening-balance',$6,1,'Opening balance configured for the development company policy.',$7,$8) on conflict(tenant_id,idempotency_key) do nothing", ct,
                SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-opening:{employmentId:D}"), context.Tenant.Id, context.EntityId, employmentId, annualLeaveTypeId, $"seed-hr-leave-opening:{employmentId:D}", context.AdministratorId, context.AsOf);

            var compensationStart = startDate < new DateOnly(2025, 1, 1) ? new DateOnly(2025, 1, 1) : startDate;
            if (compensationStart > startDate)
            {
                await UpsertCompensationAsync(context, employmentId, index, startDate, compensationStart.AddDays(-1), "Superseded", ct);
                await UpsertCompensationAsync(context, employmentId, index, compensationStart, null, "Active", ct);
                compensationRecords += 2;
            }
            else { await UpsertCompensationAsync(context, employmentId, index, compensationStart, null, "Active", ct); compensationRecords++; }
            if (index % 5 == 0)
            { await UpsertInsuranceAsync(context, employmentId, index, compensationStart, ct); insuranceDrafts++; }
            await UpsertQualificationAsync(context, person.EmployeeId, index, ct); qualificationRecords++;
            if (index % 2 == 0) { await UpsertCredentialAsync(context, person.EmployeeId, index, ct); credentialRecords++; }
            if (index == 1)
            {
                await context.ExecuteAsync("insert into business_hr.hr_adjustment(id,tenant_id,employee_id,employment_id,legal_entity_id,kind,adjustment_type,effective_from,effective_to,recurrence,payroll_impact,amount,currency_code,reason_code,reason,evidence_file_ref,status,version,created_by,created_at,submitted_by,submitted_at,approved_by,approved_at,decision_reason) values($1,$2,$3,$4,$5,'Allowance','Transport',$6,null,'Recurring','SendToPayroll',1500,'EGP','ROLE_ALLOWANCE','بدل انتقالات شهري معتمد ضمن سياسة الشركة.',null,'Approved',1,$7,$8,$7,$8,$7,$8,null) on conflict(tenant_id,id) do nothing", ct,
                    HrAdjustmentId(context.Tenant.Id, index, "transport"), context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, new DateOnly(2025, 1, 1), context.AdministratorId, context.AsOf);
                await context.ExecuteAsync("insert into business_hr.hr_adjustment(id,tenant_id,employee_id,employment_id,legal_entity_id,kind,adjustment_type,effective_from,effective_to,recurrence,payroll_impact,amount,currency_code,reason_code,reason,evidence_file_ref,status,version,created_by,created_at,submitted_by,submitted_at,approved_by,approved_at,decision_reason) values($1,$2,$3,$4,$5,'DisciplinaryAction','WrittenWarning',$6,null,'Informational','None',null,null,'ATTENDANCE_POLICY','إنذار كتابي موثق لمخالفة سياسة الحضور.',null,'Approved',1,$7,$8,$7,$8,$7,$8,null) on conflict(tenant_id,id) do nothing", ct,
                    HrAdjustmentId(context.Tenant.Id, index, "warning"), context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, new DateOnly(2026, 2, 15), context.AdministratorId, context.AsOf);
            }

            if (index % 8 == 0 && !isRehireScenario)
            {
                var historicalId = AssignmentId(context.Tenant.Id, index, 1);
                var historicalFrom = startDate.AddMonths(-12);
                await UpsertAssignmentAsync(context, historicalId, employmentId, DepartmentId(context.Tenant.Id, index % Departments.Length), PositionId(context.Tenant.Id, index % Positions.Length), calendarId, null,
                    historicalFrom, startDate, "InitialHire", context.AdministratorId, ct);
                await AddEventAsync(context, 1000 + index, person.EmployeeId, employmentId, "AssignmentChanged", historicalFrom, new { change = "initial_assignment" }, ct);
            }

            Guid? managerEmploymentId = index is > 1 and <= 8 ? EmploymentId(context.Tenant.Id, 1, 1) : null;
            var currentAssignmentId = AssignmentId(context.Tenant.Id, index, 2);
            var assignmentBoundary = await ReadAssignmentBoundaryAsync(context, employmentId, currentAssignmentId, effectiveDate, ct);
            var seededCurrentAssignment = !assignmentBoundary.HasAssignmentAtStart;
            if (seededCurrentAssignment)
            {
                // A scheduled future transfer owns its effective date; end the seed baseline at that exclusive boundary.
                await UpsertAssignmentAsync(context, currentAssignmentId, employmentId, DepartmentId(context.Tenant.Id, index % Departments.Length), PositionId(context.Tenant.Id, index % Positions.Length), calendarId, managerEmploymentId,
                    effectiveDate, assignmentBoundary.NextEffectiveFrom, "InitialHire", context.AdministratorId, ct);
            }
            if (index <= 10 || index is >= 11 and <= 14)
            {
                var shiftId = index <= 10 ? dayShiftId : nightShiftId;
                await context.ExecuteAsync("insert into business_hr.shift_assignment(tenant_id,id,legal_entity_id,employment_id,shift_definition_id,effective_from,effective_to,created_by,created_at) values($1,$2,$3,$4,$5,$6,null,$7,$8) on conflict(tenant_id,id) do nothing", ct,
                    context.Tenant.Id, ShiftAssignmentId(context.Tenant.Id, index), context.EntityId, employmentId, shiftId, effectiveDate, context.AdministratorId, context.AsOf);
            }
            if (seededCurrentAssignment)
                await AddEventAsync(context, 2000 + index, person.EmployeeId, employmentId, "AssignmentChanged", effectiveDate, new { department = Departments[index % Departments.Length].Code, position = Positions[index % Positions.Length].Code }, ct);
        }

        var pendingEmploymentId = EmploymentId(context.Tenant.Id, 3, 1);
        var pendingEmployee = Person(context.Tenant.Id, 3);
        var pendingFrom = new DateOnly(2026, 12, 7);
        var pendingRequestId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-request:pending-demo");
        await context.ExecuteAsync("insert into business_hr.leave_request(id,tenant_id,legal_entity_id,employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,reason,status,version,idempotency_key,submitted_at,decided_by,decided_at,decision_reason,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,2,'موعد عائلي','Pending',1,$10,$11,null,null,null,$12,$11,$11) on conflict(tenant_id,id) do nothing", ct,
            pendingRequestId, context.Tenant.Id, context.EntityId, pendingEmployee.EmployeeId, pendingEmploymentId, annualLeaveTypeId, annualLeavePolicyId, pendingFrom, pendingFrom.AddDays(1), $"seed-hr-leave-request:{pendingRequestId:D}", context.AsOf, context.AdministratorId);
        await context.ExecuteAsync("insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,reason,created_by,created_at) values($1,$2,$3,$4,$5,$6,'RequestReservation',-2,'LeaveRequest',$7,$8,1,'Reserved for a seeded approval-queue scenario.',$9,$10) on conflict(tenant_id,idempotency_key) do nothing", ct,
            SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-pending-reservation"), context.Tenant.Id, context.EntityId, pendingEmploymentId, annualLeaveTypeId, pendingFrom, pendingRequestId.ToString("D"), $"seed-hr-leave-pending:{pendingRequestId:D}", context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_hr.leave_request_event(id,tenant_id,legal_entity_id,leave_request_id,event_type,from_status,to_status,actor_id,reason,occurred_at) values($1,$2,$3,$4,'Created',null,'Pending',$5,'Development seed approval queue.',$6),($7,$2,$3,$4,'Submitted','Draft','Pending',$5,null,$6) on conflict(tenant_id,id) do nothing", ct,
            SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-pending-event-created"), context.Tenant.Id, context.EntityId, pendingRequestId, context.AdministratorId, context.AsOf, SeedIds.Stable($"{context.Tenant.Id:D}:hr:leave-pending-event-submitted"));

        var prehire = Person(context.Tenant.Id, count + 1);
        var prehireEmploymentId = EmploymentId(context.Tenant.Id, count + 1, 1);
        var prehireStart = new DateOnly(2026, 12, 1);
        await context.ExecuteAsync("insert into business_hr.employee(tenant_id,id,arabic_name,english_name,preferred_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,membership_id,created_by,created_at,version) values($1,$2,$3,$4,null,$3,$5,$6,$7,'Active',null,$8,$9,1) on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, prehire.EmployeeId, prehire.ArabicName, prehire.EnglishName, Normalize(prehire.ArabicName), Normalize(prehire.ArabicName), Normalize(prehire.EnglishName), context.AdministratorId, context.AsOf);
        await context.ExecuteAsync("insert into business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at) values($1,$2,$3,$4,$5,$6,$7,'Active',$8) on conflict(tenant_id,employee_id) do nothing", ct,
            context.Tenant.Id, prehire.EmployeeId, prehire.ArabicName, Normalize(prehire.ArabicName), Normalize(prehire.ArabicName), prehire.EnglishName, Normalize(prehire.EnglishName), context.AsOf);
        await context.ExecuteAsync("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version) values($1,$2,$3,$4,$5,'FullTime','PreHire',$6,null,$7,$8,1) on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, prehireEmploymentId, prehire.EmployeeId, context.EntityId, $"EMP-{count + 2:000000}", prehireStart, context.AdministratorId, context.AsOf);
        await UpsertContractAsync(context, prehireEmploymentId, ContractId(context.Tenant.Id, count + 1, 1), "Draft", prehireStart, null, null, ct);
        await AddEventAsync(context, 9000 + count, prehire.EmployeeId, prehireEmploymentId, "EmploymentCreated", prehireStart, new { employmentNumber = $"EMP-{count + 2:000000}", status = "PreHire" }, ct);

        var onboardingTemplateId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:onboarding-template:standard:v1");
        var onboardingCaseId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:onboarding-case:{prehireEmploymentId:D}");
        await context.ExecuteAsync("insert into business_hr.onboarding_template(id,tenant_id,legal_entity_id,code,name_ar,name_en,version,status,created_by,created_at) values($1,$2,$3,'STANDARD','تهيئة الموظف الجديد','Standard new employee setup',1,'Active',$4,$5) on conflict(tenant_id,id) do nothing", ct,
            onboardingTemplateId, context.Tenant.Id, context.EntityId, context.AdministratorId, context.AsOf);
        var onboardingItems = new (string Code, string Ar, string En, bool Required, int? DueDays)[]
        {
            ("IDENTITY_CHECK", "مراجعة مستندات الهوية", "Review identity documents", true, -3),
            ("WORKSTATION", "تجهيز مكان العمل", "Prepare workstation", true, 0),
            ("TEAM_INTRO", "التعريف بالفريق", "Team introduction", false, 3)
        };
        foreach (var (item, order) in onboardingItems.Select((value, index) => (value, index)))
        {
            await context.ExecuteAsync("insert into business_hr.onboarding_template_task(id,tenant_id,template_id,code,title_ar,title_en,is_required,due_days_from_start,sort_order) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict(tenant_id,id) do nothing", ct,
                SeedIds.Stable($"{context.Tenant.Id:D}:hr:onboarding-template-task:{item.Code}"), context.Tenant.Id, onboardingTemplateId, item.Code, item.Ar, item.En, item.Required, (object?)item.DueDays ?? DBNull.Value, (short)order);
        }
        await context.ExecuteAsync("insert into business_hr.onboarding_case(id,tenant_id,legal_entity_id,employee_id,employment_id,template_id,target_start_date,status,created_by,created_at,completed_at) values($1,$2,$3,$4,$5,$6,$7,'Open',$8,$9,null) on conflict(tenant_id,id) do nothing", ct,
            onboardingCaseId, context.Tenant.Id, context.EntityId, prehire.EmployeeId, prehireEmploymentId, onboardingTemplateId, prehireStart, context.AdministratorId, context.AsOf);
        foreach (var item in onboardingItems)
        {
            await context.ExecuteAsync("insert into business_hr.onboarding_task(id,tenant_id,legal_entity_id,case_id,code,title_ar,title_en,is_required,due_date,status) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Pending') on conflict(tenant_id,id) do nothing", ct,
                SeedIds.Stable($"{context.Tenant.Id:D}:hr:onboarding-task:{item.Code}"), context.Tenant.Id, context.EntityId, onboardingCaseId, item.Code, item.Ar, item.En, item.Required, (object?)(item.DueDays is { } days ? prehireStart.AddDays(days) : null) ?? DBNull.Value);
        }
        await SeedAttendanceHistoryAsync(context, count, ct);
        var employeeDocuments = await SeedEmployeeDocumentsAsync(context, ct);

        var nextNumber = count + 3;
        await PlatformSeed.EnsureSequenceAsync(context, "hr.employee-number", context.EntityId, "EMP-", 6, nextNumber, ct);

        return new(count + 1, Departments.Length, Positions.Length, count, 1, count + 3, 1, compensationRecords, insuranceDrafts, qualificationRecords, credentialRecords, employeeDocuments);
    }

    private static async Task<int> SeedEmployeeDocumentsAsync(SeedContext context, CancellationToken ct)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        if (string.IsNullOrWhiteSpace(configuration["TODIR_FILES_BUCKET"])) return 0;

        using var storage = new S3PlatformFileStorage(configuration, NullLogger<S3PlatformFileStorage>.Instance);
        var scanner = new ClamAvFileScanner(configuration);
        await storage.EnsurePrivateBucketAsync(ct);
        var asOf = DateOnly.FromDateTime(context.AsOf.UtcDateTime);
        var scenarios = new[]
        {
            new SeedDocument(1, "National ID", 1, "Restricted", "restricted", "Verified", "Active", asOf.AddYears(2), "national-id-current.txt", "بيانات هوية تجريبية محمية — ليست بيانات شخص حقيقي."),
            new SeedDocument(1, "Employment Contract", 1, "Normal", "internal", "Verified", "Superseded", asOf.AddDays(-400), "employment-contract-previous.txt", "نسخة تاريخية تجريبية من عقد العمل."),
            new SeedDocument(1, "Employment Contract", 2, "Normal", "internal", "Verified", "Active", asOf.AddDays(365), "employment-contract-current.txt", "نسخة حالية تجريبية من عقد العمل."),
            new SeedDocument(3, "National ID", 1, "Confidential", "confidential", "Pending", "Active", asOf.AddDays(20), "national-id-pending.txt", "مستند هوية تجريبي بانتظار المراجعة."),
            new SeedDocument(4, "Employment Contract", 1, "Normal", "internal", "Verified", "Active", asOf.AddDays(-30), "employment-contract-expired.txt", "عقد تجريبي منتهي يحتاج إلى متابعة.")
        };

        foreach (var scenario in scenarios)
        {
            var employeeId = EmployeeId(context.Tenant.Id, scenario.EmployeeIndex);
            var employmentId = EmploymentId(context.Tenant.Id, scenario.EmployeeIndex, 1);
            var documentId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:document:{scenario.EmployeeIndex}:{scenario.Category}:{scenario.Revision}");
            var priorId = scenario.Revision > 1 ? SeedIds.Stable($"{context.Tenant.Id:D}:hr:document:{scenario.EmployeeIndex}:{scenario.Category}:{scenario.Revision - 1}") : (Guid?)null;
            var fileRef = SeedIds.Stable($"{context.Tenant.Id:D}:hr:document-file:{scenario.EmployeeIndex}:{scenario.Category}:{scenario.Revision}");
            var content = System.Text.Encoding.UTF8.GetBytes(scenario.Content);
            if (!await scanner.IsCleanAsync(content, ct)) throw new InvalidOperationException("Development HR seed document was rejected by the malware scanner.");
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var objectKey = $"tenant/{context.Tenant.Id:D}/hr/employee/{employeeId:D}/{fileRef:D}";
            await storage.StoreReadyObjectAsync(objectKey, "text/plain", content, ct);

            await context.ExecuteAsync("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'hr','employee',$3,$4,$5,'text/plain',$6,$7,'Ready',$8,$9,$10) on conflict(id) do nothing", ct,
                fileRef, context.Tenant.Id, employeeId, objectKey, scenario.FileName, content.Length, sha256, scenario.FileClassification, context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("insert into business_hr.employee_document(id,tenant_id,employee_id,employment_id,legal_entity_id,scope_status,category,sensitivity,file_ref,sha256,content_type,size_bytes,expiry_date,verification_status,status,supersedes_document_id,created_by,created_at,version) values($1,$2,$3,$4,$5,'Resolved',$6,$7,$8,$9,'text/plain',$10,$11,$12,$13,$14,$15,$16,1) on conflict(tenant_id,id) do update set employment_id=excluded.employment_id,legal_entity_id=excluded.legal_entity_id,scope_status='Resolved',sensitivity=excluded.sensitivity,file_ref=excluded.file_ref,sha256=excluded.sha256,size_bytes=excluded.size_bytes,expiry_date=excluded.expiry_date,verification_status=excluded.verification_status,status=excluded.status,supersedes_document_id=excluded.supersedes_document_id", ct,
                documentId, context.Tenant.Id, employeeId, employmentId, context.EntityId, scenario.Category, scenario.Sensitivity, fileRef, sha256, content.Length, scenario.ExpiryDate, scenario.VerificationStatus, scenario.Status, priorId, context.AdministratorId, context.AsOf);
        }

        return scenarios.Length;
    }

    private sealed record SeedDocument(int EmployeeIndex, string Category, int Revision, string Sensitivity, string FileClassification, string VerificationStatus, string Status, DateOnly? ExpiryDate, string FileName, string Content);

    private static async Task SeedDocumentRequirementsAsync(SeedContext context, CancellationToken ct)
    {
        foreach (var (code, category) in new[] { ("NATIONAL-ID", "National ID"), ("EMPLOYMENT-CONTRACT", "Employment Contract") })
        {
            var id = SeedIds.Stable($"{context.Tenant.Id:D}:hr:document-requirement:{code}");
            await context.ExecuteAsync("insert into business_hr.employee_document_requirement(id,tenant_id,legal_entity_id,category,category_key,enabled,created_by,updated_by,updated_at,version) values($1,$2,$3,$4,upper($4),true,$5,$5,$6,1) on conflict(tenant_id,legal_entity_id,category_key) do update set category=excluded.category,enabled=true,updated_by=excluded.updated_by,updated_at=excluded.updated_at,version=business_hr.employee_document_requirement.version+case when (business_hr.employee_document_requirement.category,business_hr.employee_document_requirement.enabled) is distinct from (excluded.category,excluded.enabled) then 1 else 0 end", ct,
                id, context.Tenant.Id, context.EntityId, category, context.AdministratorId, context.AsOf);
        }
    }

    private static async Task UpsertEmploymentAsync(SeedContext context, Guid employeeId, Guid employmentId, string number, string status, DateOnly start, DateOnly? end, CancellationToken ct) =>
        await context.ExecuteAsync("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version) values($1,$2,$3,$4,$5,'FullTime',$6,$7,$8,$9,$10,1) on conflict(tenant_id,id) do update set employment_number=excluded.employment_number,status=excluded.status,start_date=excluded.start_date,end_date=excluded.end_date,version=business_hr.employment.version+case when (business_hr.employment.employment_number,business_hr.employment.status,business_hr.employment.start_date,business_hr.employment.end_date) is distinct from (excluded.employment_number,excluded.status,excluded.start_date,excluded.end_date) then 1 else 0 end", ct,
            context.Tenant.Id, employmentId, employeeId, context.EntityId, number, status, start, end, context.AdministratorId, context.AsOf);

    private static async Task SeedAttendanceHistoryAsync(SeedContext context, int employeeCount, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.AsOf, zone).DateTime);
        var workDates = new List<DateOnly>();
        for (var date = localToday.AddDays(-1); workDates.Count < 5; date = date.AddDays(-1))
            if (date.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday)) workDates.Add(date);
        var sampleEmployees = Math.Min(10, employeeCount - 1);
        for (var index = 1; index <= sampleEmployees; index++)
        {
            var person = Person(context.Tenant.Id, index);
            var employmentId = EmploymentId(context.Tenant.Id, index, 1);
            var assignmentId = ShiftAssignmentId(context.Tenant.Id, index);
            foreach (var (workDate, dayIndex) in workDates.Select((date, index) => (date, index)))
            {
                var actualIn = workDate.ToDateTime(new TimeOnly(9, 0));
                if (index == 1 && dayIndex == 0) actualIn = actualIn.AddMinutes(18);
                var actualOut = workDate.ToDateTime(new TimeOnly(17, 0));
                var isMissingOut = index == 2 && dayIndex == 1;
                var firstIn = ToInstant(actualIn, zone); var lastOut = isMissingOut ? (DateTimeOffset?)null : ToInstant(actualOut, zone);
                var recordId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:attendance:{index:0000}:{workDate:yyyyMMdd}");
                var inPunchId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:punch:{index:0000}:{workDate:yyyyMMdd}:in");
                await context.ExecuteAsync("insert into business_hr.attendance_punch(id,tenant_id,employee_id,employment_id,legal_entity_id,occurred_at,local_time_snapshot,time_zone_id,direction,source_type,source_reference,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,'Africa/Cairo','In','Web',$8,$9,$10,$11) on conflict do nothing", ct,
                    inPunchId, context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, firstIn, actualIn, $"seed-in-{index:0000}-{workDate:yyyyMMdd}", $"seed-hr-in-{index:0000}-{workDate:yyyyMMdd}", context.AdministratorId, context.AsOf);
                if (lastOut is { } outInstant)
                {
                    var outPunchId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:punch:{index:0000}:{workDate:yyyyMMdd}:out");
                    await context.ExecuteAsync("insert into business_hr.attendance_punch(id,tenant_id,employee_id,employment_id,legal_entity_id,occurred_at,local_time_snapshot,time_zone_id,direction,source_type,source_reference,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,'Africa/Cairo','Out','Web',$8,$9,$10,$11) on conflict do nothing", ct,
                        outPunchId, context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, outInstant, actualOut, $"seed-out-{index:0000}-{workDate:yyyyMMdd}", $"seed-hr-out-{index:0000}-{workDate:yyyyMMdd}", context.AdministratorId, context.AsOf);
                }
                var lateMinutes = Math.Max(0, (int)(TimeOnly.FromDateTime(actualIn) - new TimeOnly(9, 0)).TotalMinutes - 10);
                var status = isMissingOut ? "Partial" : "Present";
                await context.ExecuteAsync("insert into business_hr.attendance_record(id,tenant_id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,normalization_version,lock_state,payroll_reference,version,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Africa/Cairo','09:00','17:00',60,10,$9,$10,$11,$12,0,0,$13,1,'Open',null,1,$14) on conflict(tenant_id,employment_id,work_date) do nothing", ct,
                    recordId, context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, workDate, CalendarId(context.Tenant.Id), assignmentId, firstIn, (object?)lastOut ?? DBNull.Value, isMissingOut ? 0 : Math.Max(0, (int)(actualOut - actualIn).TotalMinutes - 60), lateMinutes, status, context.AsOf);
                if (isMissingOut)
                    await context.ExecuteAsync("insert into business_hr.attendance_exception(id,tenant_id,employee_id,employment_id,legal_entity_id,attendance_record_id,attendance_punch_id,exception_code,status,reason,created_at) values($1,$2,$3,$4,$5,$6,null,'MissingCheckOut','Open','Check-out has not been recorded yet.',$7) on conflict do nothing", ct,
                        SeedIds.Stable($"{context.Tenant.Id:D}:hr:attendance-exception:{index:0000}:{workDate:yyyyMMdd}"), context.Tenant.Id, person.EmployeeId, employmentId, context.EntityId, recordId, context.AsOf);
            }
        }
    }

    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo zone) => new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static async Task UpsertContractAsync(SeedContext context, Guid employmentId, Guid contractId, string status, DateOnly start, DateOnly? end, Guid? renewalOf, CancellationToken ct) =>
        await context.ExecuteAsync("insert into business_hr.employment_contract(id,tenant_id,legal_entity_id,employment_id,contract_type,start_date,end_date,status,renewal_of_contract_id,created_by,created_at,version) values($1,$2,$3,$4,'EmploymentAgreement',$5,$6,$7,$8,$9,$10,1) on conflict(tenant_id,id) do update set status=excluded.status,start_date=excluded.start_date,end_date=excluded.end_date,renewal_of_contract_id=excluded.renewal_of_contract_id,version=business_hr.employment_contract.version+case when (business_hr.employment_contract.status,business_hr.employment_contract.start_date,business_hr.employment_contract.end_date,business_hr.employment_contract.renewal_of_contract_id) is distinct from (excluded.status,excluded.start_date,excluded.end_date,excluded.renewal_of_contract_id) then 1 else 0 end", ct,
            contractId, context.Tenant.Id, context.EntityId, employmentId, start, end, status, renewalOf, context.AdministratorId, context.AsOf);

    private static async Task UpsertCompensationAsync(SeedContext context, Guid employmentId, int index, DateOnly from, DateOnly? to, string status, CancellationToken ct)
    {
        var id = SeedIds.Stable($"{context.Tenant.Id:D}:hr:compensation:{index:0000}:{from:yyyyMMdd}");
        var amount = 12000m + ((index * 137) % 28000);
        await context.ExecuteAsync("insert into business_hr.employment_compensation_agreement(id,tenant_id,employment_id,legal_entity_id,currency_code,amount,pay_frequency,effective_from,effective_to,status,contract_id,reason,evidence_file_ref,created_by,created_at,version) values($1,$2,$3,$4,'EGP',$5,'Monthly',$6,$7,$8,null,'Development seed compensation history',null,$9,$10,1) on conflict(tenant_id,id) do update set amount=excluded.amount,effective_to=excluded.effective_to,status=excluded.status,version=business_hr.employment_compensation_agreement.version+case when (business_hr.employment_compensation_agreement.amount,business_hr.employment_compensation_agreement.effective_to,business_hr.employment_compensation_agreement.status) is distinct from (excluded.amount,excluded.effective_to,excluded.status) then 1 else 0 end", ct,
            id, context.Tenant.Id, employmentId, context.EntityId, amount, from, (object?)to ?? DBNull.Value, status, context.AdministratorId, context.AsOf);
    }

    private static async Task UpsertInsuranceAsync(SeedContext context, Guid employmentId, int index, DateOnly from, CancellationToken ct)
    {
        var id = SeedIds.Stable($"{context.Tenant.Id:D}:hr:insurance:{index:0000}:health");
        await context.ExecuteAsync("insert into business_hr.employee_insurance_enrollment(id,tenant_id,employment_id,legal_entity_id,scheme_code,provider_code,encrypted_member_number,masked_member_number,effective_from,effective_to,status,country_provider_version,evidence_file_ref,created_by,created_at,version) values($1,$2,$3,$4,'HealthCoverage',null,null,null,$5,null,'Active',null,null,$6,$7,1) on conflict(tenant_id,id) do nothing", ct,
            id, context.Tenant.Id, employmentId, context.EntityId, from, context.AdministratorId, context.AsOf);
    }

    private static async Task UpsertQualificationAsync(SeedContext context, Guid employeeId, int index, CancellationToken ct)
    {
        var id = SeedIds.Stable($"{context.Tenant.Id:D}:hr:qualification:{index:0000}");
        var course = index % 3 == 0 ? "بكالوريوس إدارة الأعمال" : index % 3 == 1 ? "بكالوريوس التجارة" : "دبلوم مهني";
        await context.ExecuteAsync("insert into business_hr.employee_qualification(id,tenant_id,employee_id,qualification_type,name,institution,field_of_study,level,completed_on,file_ref,status,created_by,created_at) values($1,$2,$3,'Degree',$4,'مؤهل تجريبي للتطوير',$5,$6,$7,null,'Active',$8,$9) on conflict(tenant_id,id) do update set name=excluded.name,field_of_study=excluded.field_of_study,level=excluded.level,completed_on=excluded.completed_on", ct,
            id, context.Tenant.Id, employeeId, course, index % 2 == 0 ? "إدارة أعمال" : "محاسبة", index % 3 == 2 ? "دبلوم" : "بكالوريوس", new DateOnly(2012 + index % 12, 6, 30), context.AdministratorId, context.AsOf);
    }

    private static async Task UpsertCredentialAsync(SeedContext context, Guid employeeId, int index, CancellationToken ct)
    {
        var id = SeedIds.Stable($"{context.Tenant.Id:D}:hr:credential:{index:0000}");
        var asOf = DateOnly.FromDateTime(context.AsOf.UtcDateTime);
        var expires = index % 4 == 0 ? asOf.AddDays(-45) : asOf.AddDays(180 + index % 360);
        await context.ExecuteAsync("insert into business_hr.employee_credential(id,tenant_id,employee_id,credential_type,name,issuer,country_code,issued_on,expiry_date,file_ref,verification_status,status,created_by,created_at,version) values($1,$2,$3,'ProfessionalCredential','شهادة مهنية تجريبية','جهة إصدار تجريبية',null,$4,$5,null,'Pending','Active',$6,$7,1) on conflict(tenant_id,id) do update set expiry_date=excluded.expiry_date", ct,
            id, context.Tenant.Id, employeeId, asOf.AddYears(-2), expires, context.AdministratorId, context.AsOf);
    }

    private static async Task UpsertAssignmentAsync(SeedContext context, Guid id, Guid employmentId, Guid departmentId, Guid positionId, Guid calendarId, Guid? managerEmploymentId, DateOnly from, DateOnly? to, string reason, Guid actor, CancellationToken ct) =>
        await context.ExecuteAsync("insert into business_hr.employment_assignment(tenant_id,id,legal_entity_id,employment_id,branch_id,department_id,position_id,manager_employment_id,work_calendar_id,effective_from,effective_to,reason_code,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,1) on conflict(tenant_id,id) do update set branch_id=excluded.branch_id,department_id=excluded.department_id,position_id=excluded.position_id,manager_employment_id=excluded.manager_employment_id,work_calendar_id=excluded.work_calendar_id,effective_from=excluded.effective_from,effective_to=excluded.effective_to,reason_code=excluded.reason_code,version=business_hr.employment_assignment.version+case when (business_hr.employment_assignment.department_id,business_hr.employment_assignment.position_id,business_hr.employment_assignment.manager_employment_id,business_hr.employment_assignment.work_calendar_id,business_hr.employment_assignment.effective_from,business_hr.employment_assignment.effective_to) is distinct from (excluded.department_id,excluded.position_id,excluded.manager_employment_id,excluded.work_calendar_id,excluded.effective_from,excluded.effective_to) then 1 else 0 end", ct,
            context.Tenant.Id, id, context.EntityId, employmentId, context.BranchId, departmentId, positionId, managerEmploymentId, calendarId, from, to, reason, actor, context.AsOf);

    private static async Task<(DateOnly? NextEffectiveFrom, bool HasAssignmentAtStart)> ReadAssignmentBoundaryAsync(SeedContext context, Guid employmentId, Guid currentAssignmentId, DateOnly effectiveFrom, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            select
                min(effective_from) filter (where effective_from > $4),
                coalesce(bool_or(effective_from <= $4 and coalesce(effective_to, 'infinity'::date) > $4), false)
            from business_hr.employment_assignment
            where tenant_id = $1 and employment_id = $2 and id <> $3
            """, context.Connection, context.Transaction);
        command.Parameters.AddWithValue(context.Tenant.Id);
        command.Parameters.AddWithValue(employmentId);
        command.Parameters.AddWithValue(currentAssignmentId);
        command.Parameters.AddWithValue(effectiveFrom);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.IsDBNull(0) ? null : reader.GetFieldValue<DateOnly>(0), reader.GetBoolean(1));
    }

    private static async Task AddEventAsync(SeedContext context, int suffix, Guid employeeId, Guid? employmentId, string eventType, DateOnly effectiveOn, object payload, CancellationToken ct)
    {
        var eventId = SeedIds.Stable($"{context.Tenant.Id:D}:hr:event:{suffix}");
        await context.ExecuteAsync("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10) on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, eventId, employeeId, employmentId, eventType, effectiveOn, context.AdministratorId,
            System.Text.Json.JsonSerializer.Serialize(payload), SeedIds.Stable($"{context.Tenant.Id:D}:hr:event-correlation:{suffix}"), context.AsOf);
        await context.ExecuteAsync("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) select tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload from business_hr.employment_event where tenant_id=$1 and id=$2 on conflict(tenant_id,id) do nothing", ct,
            context.Tenant.Id, eventId);
    }

    private static (Guid EmployeeId, string ArabicName, string EnglishName) Person(Guid tenant, int index)
    {
        var givenIndex = (index - 1) % GivenNamesAr.Length;
        var familyIndex = ((index - 1) / GivenNamesAr.Length) % FamilyNamesAr.Length;
        return (EmployeeId(tenant, index), $"{GivenNamesAr[givenIndex]} {FamilyNamesAr[familyIndex]}", $"{GivenNamesEn[givenIndex]} {FamilyNamesEn[familyIndex]}");
    }
    private static Guid EmployeeId(Guid tenant, int index) => SeedIds.Stable($"{tenant:D}:hr:employee:{index:0000}");
    private static Guid EmploymentId(Guid tenant, int index, int rehire) => SeedIds.Stable($"{tenant:D}:hr:employment:{index:0000}:{rehire}");
    private static Guid DepartmentId(Guid tenant, int index) => SeedIds.Stable($"{tenant:D}:hr:department:{index:0000}");
    private static Guid PositionId(Guid tenant, int index) => SeedIds.Stable($"{tenant:D}:hr:position:{index:0000}");
    private static Guid AssignmentId(Guid tenant, int index, int revision) => SeedIds.Stable($"{tenant:D}:hr:assignment:{index:0000}:{revision}");
    private static Guid ContractId(Guid tenant, int index, int revision) => SeedIds.Stable($"{tenant:D}:hr:contract:{index:0000}:{revision}");
    private static Guid CalendarId(Guid tenant) => SeedIds.Stable($"{tenant:D}:hr:calendar:office");
    private static Guid ShiftId(Guid tenant, string code) => SeedIds.Stable($"{tenant:D}:hr:shift:{code}");
    private static Guid ShiftAssignmentId(Guid tenant, int index) => SeedIds.Stable($"{tenant:D}:hr:shift-assignment:{index:0000}");
    private static Guid HrAdjustmentId(Guid tenant, int index, string code) => SeedIds.Stable($"{tenant:D}:hr:adjustment:{index:0000}:{code}");
    private static string Normalize(string value) => HrSearchNormalization.Normalize(value);
}

public sealed record HrSeedSummary(int Employees, int Departments, int Positions, int ActiveEmployments, int TerminatedEmployments, int Contracts, int Renewals, int CompensationRecords, int InsuranceDrafts, int Qualifications, int Credentials, int EmployeeDocuments);
