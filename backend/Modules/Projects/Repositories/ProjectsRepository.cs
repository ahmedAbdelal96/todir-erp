using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using Core.Modules;
using Core.Services;
using Core.Transactions;
using Npgsql;
using Modules.Projects.DTOs;
using Modules.Projects.Entities;
using Shared.Contracts.Projects;
using Shared.Primitives.Identifiers;

namespace Modules.Projects.Repositories;

public sealed class ProjectsRepository(ITransactionCoordinator coordinator, ITransactionalSequenceAllocator sequences)
{
    public Task<bool> CanAccessProjectAsync(TenantId tenant, Guid projectId, Guid actor, Guid membership, string permission, CancellationToken ct) => coordinator.ExecuteAsync<bool>(tenant, async (context, token) =>
    {
        const string sql = "select exists(select 1 from business_projects.project p where p.tenant_id=$1 and p.id=$2 and (exists(select 1 from business_projects.project_team_member tm where tm.tenant_id=p.tenant_id and tm.project_id=p.id and tm.user_id=$3 and tm.active_from <= current_date and (tm.active_to is null or tm.active_to >= current_date)) or exists(select 1 from platform.membership_role_assignment a join platform.role_permission rp on rp.tenant_id=a.tenant_id and rp.role_id=a.role_id where a.tenant_id=p.tenant_id and a.membership_id=$4 and a.scope_type='Tenant' and rp.permission_code=$5) or exists(select 1 from platform.membership_role_assignment a join platform.role_permission rp on rp.tenant_id=a.tenant_id and rp.role_id=a.role_id where a.tenant_id=p.tenant_id and a.membership_id=$4 and a.scope_type='LegalEntity' and a.scope_id=p.legal_entity_id and rp.permission_code=$5)))";
        return Convert.ToBoolean(await ScalarAsync(context, sql, token, tenant.Value, projectId, actor, membership, permission), CultureInfo.InvariantCulture);
    }, ct);

    public Task<ProjectDetail> CreateAsync(TenantId tenant, Guid actor, Guid membership, CreateProjectRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ProjectRules.ValidateCreate(request.ProjectType, request.CustomerPartyId, request.BillingModel, request.InitialBudget, request.StartDate, request.TargetEndDate);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200) throw new ProjectException("projects.name_invalid");
        var id = Guid.CreateVersion7();
        var number = await sequences.AllocateAsync(context, new SequenceDefinition("projects.project-number", tenant, request.LegalEntityId, null, null, null, "PRJ-", 7, 1), token);
        const string sql = "insert into business_projects.project(tenant_id,id,legal_entity_id,project_number,name,description,project_type,customer_party_id,manager_employee_id,start_date,target_end_date,billing_model,currency_code,source_type,source_ref,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16)";
        await ExecuteAsync(context, sql, token, tenant.Value, id, request.LegalEntityId, number, request.Name.Trim(), Db(request.Description), request.ProjectType, Db(request.CustomerPartyId), Db(request.ProjectManagerEmployeeId), request.StartDate, Db(request.TargetEndDate), request.BillingModel, Db(NormalizeCurrency(request.CurrencyCode)), Db(request.SourceType), Db(request.SourceRef), actor);
        if (request.InitialBudget is { } amount)
        {
            var budgetId = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_projects.project_budget(tenant_id,id,project_id,name,currency_code,control_mode) values($1,$2,$3,'Initial budget',$4,'Warn')", token, tenant.Value, budgetId, id, NormalizeCurrency(request.CurrencyCode) ?? "EGP");
            await ExecuteAsync(context, "insert into business_projects.project_budget_version(tenant_id,id,budget_id,version_no,state,original_amount,current_amount,approved_by,approved_at,created_by) values($1,$2,$3,1,'Approved',$4,$4,$5,now(),$5)", token, tenant.Value, versionId, budgetId, amount, actor);
        }
        await AuditAsync(context, tenant, id, actor, "project_created", null, new { number, request.Name, request.ProjectType }, token);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProjectCreatedV1", JsonSerializer.Serialize(new { projectId = id, legalEntityId = request.LegalEntityId, projectNumber = number, projectType = request.ProjectType }), tenant, DateTimeOffset.UtcNow, correlationId, id, "projects"));
        return await GetDetailAsync(context, tenant, id, token) ?? throw new ProjectException("projects.project_not_found");
    }, ct);

    public Task<PagedResult<ProjectListItem>> ListAsync(TenantId tenant, Guid legalEntityId, string? search, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync<PagedResult<ProjectListItem>>(tenant, async (context, token) =>
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); var offset = (page - 1) * pageSize;
        const string sql = "select id,legal_entity_id,project_number,name,project_type,customer_party_id,lifecycle_state,execution_health,billing_model,start_date,target_end_date,row_version from business_projects.project where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or name ilike '%'||$3||'%' or project_number ilike '%'||$3||'%') and ($4::text is null or lifecycle_state=$4) order by updated_at desc,id desc limit $5 offset $6";
        var items = new List<ProjectListItem>();
        await using var command = Command(context, sql, tenant.Value, legalEntityId, Db(search), Db(state), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateOnly>(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10), reader.GetInt64(11)));
        var hasNext = items.Count > pageSize; if (hasNext) items.RemoveAt(items.Count - 1); return new(items, page, pageSize, hasNext);
    }, ct);

    public Task<ProjectDetail?> GetAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => GetDetailAsync(context, tenant, projectId, token), ct);

    public Task<ProjectDetail> UpdateAsync(TenantId tenant, Guid actor, Guid projectId, UpdateProjectRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ProjectException("projects.name_invalid");
        var updated = await ExecuteAsync(context, "update business_projects.project set name=$4,description=$5,target_end_date=$6,execution_health=coalesce($7,execution_health),row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and row_version=$3", token, tenant.Value, projectId, request.Version, request.Name.Trim(), Db(request.Description), Db(request.TargetEndDate), Db(request.ExecutionHealth));
        if (updated == 0) throw new ProjectException("projects.concurrency_conflict");
        await AuditAsync(context, tenant, projectId, actor, "project_updated", null, request, token);
        return await GetDetailAsync(context, tenant, projectId, token) ?? throw new ProjectException("projects.project_not_found");
    }, ct);

    public Task<ProjectDetail> TransitionAsync(TenantId tenant, Guid actor, Guid projectId, string nextState, long version, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await StateAsync(context, tenant.Value, projectId, token); var aggregate = new ProjectAggregate(projectId, current); aggregate.TransitionTo(nextState);
        if (nextState == ProjectStates.Closed) await EnsureCloseReadyAsync(context, tenant.Value, projectId, token);
        var count = await ExecuteAsync(context, "update business_projects.project set lifecycle_state=$4,actual_end_date=case when $4='ExecutionCompleted' then current_date else actual_end_date end,row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and row_version=$3", token, tenant.Value, projectId, version, nextState);
        if (count == 0) throw new ProjectException("projects.concurrency_conflict");
        await AuditAsync(context, tenant, projectId, actor, "project_state_changed", new { current }, new { nextState }, token);
        return await GetDetailAsync(context, tenant, projectId, token) ?? throw new ProjectException("projects.project_not_found");
    }, ct);

    public Task<ProjectTaskView> AddTaskAsync(TenantId tenant, Guid actor, Guid projectId, CreateProjectTaskRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token)); if (string.IsNullOrWhiteSpace(request.Title)) throw new ProjectException("projects.task_title_required");
        var id = Guid.CreateVersion7(); var sequence = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(sequence_no),0)+1 from business_projects.project_task where tenant_id=$1 and project_id=$2", token, tenant.Value, projectId), CultureInfo.InvariantCulture);
        await ExecuteAsync(context, "insert into business_projects.project_task(tenant_id,id,project_id,work_node_id,title,description,priority,assigned_employee_id,planned_start,planned_end,sequence_no,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, id, projectId, Db(request.WorkNodeId), request.Title.Trim(), Db(request.Description), request.Priority, Db(request.AssignedEmployeeId), Db(request.PlannedStart), Db(request.PlannedEnd), sequence, actor);
        return await ReadTaskAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.task_not_found");
    }, ct);

    public Task<IReadOnlyList<ProjectTaskView>> ListTasksAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<ProjectTaskView>>(tenant, async (context, token) =>
    {
        var result = new List<ProjectTaskView>(); await using var command = Command(context, "select id,project_id,title,description,status,priority,assigned_employee_id,planned_start,planned_end,progress_percent,sequence_no,row_version from business_projects.project_task where tenant_id=$1 and project_id=$2 order by sequence_no,id", tenant.Value, projectId); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadTask(reader)); return result;
    }, ct);

    public Task<MilestoneView> AddMilestoneAsync(TenantId tenant, Guid actor, Guid projectId, CreateMilestoneRequest request, CancellationToken ct) => coordinator.ExecuteAsync<MilestoneView>(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ProjectException("projects.milestone_name_required");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_projects.project_milestone(tenant_id,id,project_id,task_id,name,due_date) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, projectId, Db(request.TaskId), request.Name.Trim(), Db(request.DueDate));
        await AuditAsync(context, tenant, projectId, actor, "milestone_created", null, request, token);
        return await ReadMilestoneAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.milestone_not_found");
    }, ct);

    public Task<TaskDependencyView> AddDependencyAsync(TenantId tenant, Guid actor, Guid projectId, CreateTaskDependencyRequest request, CancellationToken ct) => coordinator.ExecuteAsync<TaskDependencyView>(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token));
        if (request.PredecessorTaskId == request.SuccessorTaskId || request.LagDays < 0 || request.DependencyType is not ("FinishToStart" or "StartToStart" or "FinishToFinish" or "StartToFinish")) throw new ProjectException("projects.task_dependency_invalid");
        var tasks = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_projects.project_task where tenant_id=$1 and project_id=$2 and id in ($3,$4)", token, tenant.Value, projectId, request.PredecessorTaskId, request.SuccessorTaskId), CultureInfo.InvariantCulture); if (tasks != 2) throw new ProjectException("projects.task_not_found");
        var cycle = Convert.ToBoolean(await ScalarAsync(context, "with recursive reachable(id) as (select $3::uuid union all select d.successor_task_id from business_projects.project_dependency d join reachable r on r.id=d.predecessor_task_id where d.tenant_id=$1 and d.project_id=$2) select exists(select 1 from reachable where id=$4)", token, tenant.Value, projectId, request.SuccessorTaskId, request.PredecessorTaskId), CultureInfo.InvariantCulture); if (cycle) throw new ProjectException("projects.task_dependency_cycle");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_dependency(tenant_id,id,project_id,predecessor_task_id,successor_task_id,dependency_type,lag_days) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, projectId, request.PredecessorTaskId, request.SuccessorTaskId, request.DependencyType, request.LagDays); await AuditAsync(context, tenant, projectId, actor, "task_dependency_created", null, request, token); return new(id, projectId, request.PredecessorTaskId, request.SuccessorTaskId, request.DependencyType, request.LagDays);
    }, ct);

    public Task<IReadOnlyList<MilestoneView>> ListMilestonesAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<MilestoneView>>(tenant, async (context, token) =>
    {
        var result = new List<MilestoneView>();
        await using var command = Command(context, "select id,project_id,task_id,name,due_date,reached_at,accepted_at,acceptance_reference,status from business_projects.project_milestone where tenant_id=$1 and project_id=$2 order by due_date nulls last,id", tenant.Value, projectId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadMilestone(reader));
        return result;
    }, ct);

    public Task<MilestoneView> MilestoneActionAsync(TenantId tenant, Guid actor, Guid milestoneId, string action, MilestoneActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync<MilestoneView>(tenant, async (context, token) =>
    {
        var row = await ReadMilestoneAsync(context, tenant.Value, milestoneId, token) ?? throw new ProjectException("projects.milestone_not_found");
        var (next, reached, accepted) = action switch
        {
            "reach" when row.Status == "Open" => ("Reached", true, false),
            "accept" when row.Status == "Reached" && !string.IsNullOrWhiteSpace(request.AcceptanceReference) => ("Accepted", false, true),
            "reject" when row.Status == "Reached" => ("Open", false, false),
            _ => throw new ProjectException("projects.milestone_transition_invalid")
        };
        await ExecuteAsync(context, "update business_projects.project_milestone set status=$3,reached_at=case when $3='Reached' then coalesce(reached_at,now()) when $3='Accepted' then coalesce(reached_at,now()) else null end,accepted_at=case when $3='Accepted' then now() else null end,acceptance_reference=case when $3='Accepted' then $4 else null end where tenant_id=$1 and id=$2", token, tenant.Value, milestoneId, next, Db(request.AcceptanceReference));
        await AuditAsync(context, tenant, row.ProjectId, actor, $"milestone_{action}", row, new { next, request.AcceptanceReference }, token);
        return await ReadMilestoneAsync(context, tenant.Value, milestoneId, token) ?? throw new ProjectException("projects.milestone_not_found");
    }, ct);

    public Task<ProjectTeamMemberView> AddTeamMemberAsync(TenantId tenant, Guid actor, Guid projectId, AddProjectTeamMemberRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token)); if (request.EmployeeId is null && request.UserId is null) throw new ProjectException("projects.team_member_required");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_team_member(tenant_id,id,project_id,employee_id,user_id,role_code,allocation_percent,active_from,active_to,is_manager) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, projectId, Db(request.EmployeeId), Db(request.UserId), request.RoleCode.Trim(), request.AllocationPercent, request.ActiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow), Db(request.ActiveTo), request.IsManager); return await ReadTeamAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.team_member_not_found");
    }, ct);

    public Task<IReadOnlyList<ProjectTeamMemberView>> ListTeamAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<ProjectTeamMemberView>>(tenant, async (context, token) =>
    {
        var result = new List<ProjectTeamMemberView>(); await using var command = Command(context, "select id,employee_id,user_id,role_code,allocation_percent,active_from,active_to,is_manager from business_projects.project_team_member where tenant_id=$1 and project_id=$2 order by active_from,id", tenant.Value, projectId); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) result.Add(ReadTeam(reader)); return result;
    }, ct);

    public Task<TimeEntryView> CreateTimeAsync(TenantId tenant, Guid actor, CreateTimeEntryRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, request.ProjectId, token)); if (request.DurationMinutes is <= 0 or > 1440) throw new ProjectException("projects.time_duration_invalid");
        var id = Guid.CreateVersion7(); var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"time:{id:D}" : request.IdempotencyKey.Trim();
        var existing = await ScalarAsync(context, "select id from business_projects.project_time_entry where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, key);
        if (existing is Guid prior) return await ReadTimeAsync(context, tenant.Value, prior, token) ?? throw new ProjectException("projects.time_retry_incomplete");
        await ExecuteAsync(context, "insert into business_projects.project_time_entry(tenant_id,id,project_id,task_id,employee_id,work_date,duration_minutes,description,billable,state,idempotency_key,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Draft',$10,$11)", token, tenant.Value, id, request.ProjectId, Db(request.TaskId), request.EmployeeId, request.WorkDate, request.DurationMinutes, Db(request.Description), request.Billable, key, actor);
        return await ReadTimeAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.time_not_found");
    }, ct);

    public Task<TimeEntryView> TimeActionAsync(TenantId tenant, Guid actor, Guid timeId, string action, long version, string? reason, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadTimeStateAsync(context, tenant.Value, timeId, token); var expected = action switch { "submit" when current == "Draft" => "Submitted", "approve" when current == "Submitted" => "Approved", "reject" when current is "Submitted" or "Approved" => "Rejected", "finalize" when current == "Approved" => "Finalized", "correct" when current == "Finalized" && !string.IsNullOrWhiteSpace(reason) => "Corrected", _ => throw new ProjectException("projects.time_transition_invalid") };
        if (expected == "Finalized")
        {
            var source = await ReadTimeFinalizationSourceAsync(context, tenant.Value, timeId, token) ?? throw new ProjectException("projects.time_not_found");
            var rates = await ResolveRatesAsync(context, tenant.Value, timeId, token);
            if (rates is null || rates.Value.CostRate is null) throw new ProjectException("projects.cost_rate_missing");
            if (source.Billable && rates.Value.BillingRate is null) throw new ProjectException("projects.billing_rate_missing");
            var costAmount = Math.Round(source.DurationMinutes / 60m * rates.Value.CostRate.Value, 4, MidpointRounding.AwayFromZero);
            var billingAmount = source.Billable ? Math.Round(source.DurationMinutes / 60m * rates.Value.BillingRate!.Value, 4, MidpointRounding.AwayFromZero) : (decimal?)null;
            await ExecuteAsync(context, "update business_projects.project_time_entry set state='Finalized',cost_rate=$4,cost_currency=$5,cost_amount=$6,billing_rate=$7,billing_currency=$8,billing_amount=$9,finalized_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2 and row_version=$3", token, tenant.Value, timeId, version, rates.Value.CurrencyCode, rates.Value.CostRate.Value, costAmount, Db(billingAmount is null ? null : rates.Value.BillingRate), Db(billingAmount is null ? null : rates.Value.CurrencyCode), Db(billingAmount));
            await ExecuteAsync(context, "insert into business_projects.project_cost_fact(tenant_id,id,project_id,task_id,source_module,source_type,source_id,cost_category,original_currency,original_amount,base_currency,base_amount,exchange_rate,fact_date,idempotency_key) values($1,$2,$3,$4,'projects','ProjectTimeEntry',$5,'Labor',$6,$7,$6,$7,1,$8,$9) on conflict (tenant_id,idempotency_key) do nothing", token, tenant.Value, Guid.CreateVersion7(), source.ProjectId, Db(source.TaskId), timeId.ToString("D"), rates.Value.CurrencyCode, costAmount, source.WorkDate, $"time-cost:{timeId:D}");
            if (source.Billable)
                await ExecuteAsync(context, "insert into business_projects.project_billable_charge(tenant_id,id,project_id,source_type,source_id,charge_date,description,currency_code,cost_amount,billable_amount,state,idempotency_key) values($1,$2,$3,'TimeEntry',$4,$5,'Approved project time',$6,$7,$8,'Eligible',$9) on conflict (tenant_id,idempotency_key) do nothing", token, tenant.Value, Guid.CreateVersion7(), source.ProjectId, timeId.ToString("D"), source.WorkDate, rates.Value.CurrencyCode, costAmount, billingAmount!.Value, $"time-charge:{timeId:D}");
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProjectTimeFinalizedV1", JsonSerializer.Serialize(new { timeId, projectId = source.ProjectId, costAmount, billingAmount }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), source.ProjectId, "projects"));
        }
        else
        {
            var count = await ExecuteAsync(context, "update business_projects.project_time_entry set state=$4,row_version=row_version+1 where tenant_id=$1 and id=$2 and row_version=$3", token, tenant.Value, timeId, version, expected); if (count == 0) throw new ProjectException("projects.concurrency_conflict");
        }
        await ExecuteAsync(context, "insert into business_projects.project_time_approval(tenant_id,id,time_entry_id,action,actor_id,reason) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), timeId, expected, actor, Db(reason)); return await ReadTimeAsync(context, tenant.Value, timeId, token) ?? throw new ProjectException("projects.time_not_found");
    }, ct);

    public Task<RateCardView> CreateRateCardAsync(TenantId tenant, Guid actor, CreateRateCardRequest request, CancellationToken ct) => coordinator.ExecuteAsync<RateCardView>(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || request.Lines.Count == 0) throw new ProjectException("projects.rate_card_invalid");
        if (request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom) throw new ProjectException("projects.rate_card_dates_invalid");
        var id = Guid.CreateVersion7(); var currency = NormalizeCurrency(request.CurrencyCode) ?? throw new ProjectException("projects.currency_required");
        await ExecuteAsync(context, "insert into business_projects.project_rate_card(tenant_id,id,legal_entity_id,code,name,currency_code,effective_from,effective_to,status) values($1,$2,$3,$4,$5,$6,$7,$8,'Active')", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), currency, request.EffectiveFrom, Db(request.EffectiveTo));
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.RoleCode) || line.CostRate is < 0 || line.BillingRate is < 0 || line.CostRate is null && line.BillingRate is null) throw new ProjectException("projects.rate_line_invalid");
            await ExecuteAsync(context, "insert into business_projects.project_rate_card_line(tenant_id,id,rate_card_id,role_code,employee_id,cost_rate,billing_rate) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, Guid.CreateVersion7(), id, line.RoleCode.Trim(), Db(line.EmployeeId), Db(line.CostRate), Db(line.BillingRate));
        }
        return new(id, request.LegalEntityId, null, request.Code.Trim(), request.Name.Trim(), currency, request.EffectiveFrom, request.EffectiveTo, "Active");
    }, ct);

    public Task<IReadOnlyList<TimeEntryView>> ListTimeAsync(TenantId tenant, Guid projectId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<TimeEntryView>>(tenant, async (context, token) =>
    {
        var result = new List<TimeEntryView>(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); await using var command = Command(context, "select id,project_id,task_id,employee_id,work_date,duration_minutes,billable,state,cost_amount,billing_amount,row_version from business_projects.project_time_entry where tenant_id=$1 and project_id=$2 order by work_date desc,id desc limit $3 offset $4", tenant.Value, projectId, pageSize, (page - 1) * pageSize); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) result.Add(ReadTime(reader)); return result;
    }, ct);

    public Task<BudgetView> CreateBudgetAsync(TenantId tenant, Guid actor, Guid projectId, CreateBudgetRequest request, CancellationToken ct) => coordinator.ExecuteAsync<BudgetView>(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token)); if (request.OriginalAmount < 0) throw new ProjectException("projects.budget_invalid"); var id = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7(); var currency = NormalizeCurrency(request.CurrencyCode) ?? throw new ProjectException("projects.currency_required");
        await ExecuteAsync(context, "insert into business_projects.project_budget(tenant_id,id,project_id,name,currency_code,control_mode) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, projectId, request.Name.Trim(), currency, request.ControlMode); await ExecuteAsync(context, "insert into business_projects.project_budget_version(tenant_id,id,budget_id,version_no,state,original_amount,current_amount,approved_by,approved_at,created_by) values($1,$2,$3,1,'Approved',$4,$4,$5,now(),$5)", token, tenant.Value, versionId, id, request.OriginalAmount, actor);
        foreach (var line in request.Lines ?? []) await ExecuteAsync(context, "insert into business_projects.project_budget_line(tenant_id,id,budget_version_id,category_code,description,amount) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), versionId, line.CategoryCode.Trim(), Db(line.Description), line.Amount);
        return new(id, projectId, request.Name.Trim(), currency, request.ControlMode, request.OriginalAmount, request.OriginalAmount, "Approved", 1);
    }, ct);

    public Task<BudgetVersionView> CreateBudgetRevisionAsync(TenantId tenant, Guid actor, Guid projectId, Guid budgetId, CreateBudgetRevisionRequest request, CancellationToken ct) => coordinator.ExecuteAsync<BudgetVersionView>(tenant, async (context, token) =>
    {
        ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token));
        if (request.CurrentAmount < 0) throw new ProjectException("projects.budget_invalid");
        var budgetProject = await ScalarAsync(context, "select project_id from business_projects.project_budget where tenant_id=$1 and id=$2", token, tenant.Value, budgetId); if (budgetProject is not Guid linkedProject || linkedProject != projectId) throw new ProjectException("projects.budget_not_found");
        var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_projects.project_budget_version where tenant_id=$1 and budget_id=$2", token, tenant.Value, budgetId), CultureInfo.InvariantCulture); var id = Guid.CreateVersion7();
        var original = await DecimalAsync(context, "select original_amount from business_projects.project_budget_version where tenant_id=$1 and budget_id=$2 and state='Approved' order by version_no desc limit 1", token, tenant.Value, budgetId);
        await ExecuteAsync(context, "insert into business_projects.project_budget_version(tenant_id,id,budget_id,version_no,state,original_amount,current_amount,created_by) values($1,$2,$3,$4,'Draft',$5,$6,$7)", token, tenant.Value, id, budgetId, version, original, request.CurrentAmount, actor);
        foreach (var line in request.Lines ?? []) await ExecuteAsync(context, "insert into business_projects.project_budget_line(tenant_id,id,budget_version_id,category_code,description,amount) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), id, line.CategoryCode.Trim(), Db(line.Description), line.Amount);
        await AuditAsync(context, tenant, projectId, actor, "budget_revision_created", null, new { budgetId, version, request.CurrentAmount, request.Reason }, token);
        return new(id, budgetId, version, "Draft", original, request.CurrentAmount);
    }, ct);

    public Task<BudgetVersionView> ApproveBudgetVersionAsync(TenantId tenant, Guid actor, Guid budgetVersionId, CancellationToken ct) => coordinator.ExecuteAsync<BudgetVersionView>(tenant, async (context, token) =>
    {
        await using var command = Command(context, "select v.id,v.budget_id,v.version_no,v.state,v.original_amount,v.current_amount,b.project_id from business_projects.project_budget_version v join business_projects.project_budget b on b.tenant_id=v.tenant_id and b.id=v.budget_id where v.tenant_id=$1 and v.id=$2", tenant.Value, budgetVersionId); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ProjectException("projects.budget_version_not_found"); var version = new BudgetVersionView(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5)); var projectId = reader.GetGuid(6); await reader.CloseAsync();
        if (version.State != "Draft") return version;
        await ExecuteAsync(context, "update business_projects.project_budget_version set state='Superseded' where tenant_id=$1 and budget_id=$2 and state='Approved'", token, tenant.Value, version.BudgetId);
        await ExecuteAsync(context, "update business_projects.project_budget_version set state='Approved',approved_by=$3,approved_at=now() where tenant_id=$1 and id=$2 and state='Draft'", token, tenant.Value, budgetVersionId, actor);
        await AuditAsync(context, tenant, projectId, actor, "budget_version_approved", null, version, token);
        return version with { State = "Approved" };
    }, ct);

    public Task<ForecastView> CreateForecastAsync(TenantId tenant, Guid actor, Guid projectId, CreateForecastRequest request, CancellationToken ct) => coordinator.ExecuteAsync<ForecastView>(tenant, async (context, token) =>
    {
        var actual = await DecimalAsync(context, "select coalesce(sum(base_amount),0) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and state='Finalized'", token, tenant.Value, projectId); var commitment = await DecimalAsync(context, "select coalesce(sum(open_amount),0) from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and state in ('Open','PartiallyActualized')", token, tenant.Value, projectId); var eac = request.IncludesOpenCommitments ? actual + commitment + request.EtcAmount : actual + request.EtcAmount; var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_projects.project_forecast where tenant_id=$1 and project_id=$2", token, tenant.Value, projectId), CultureInfo.InvariantCulture); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_forecast(tenant_id,id,project_id,version_no,state,actual_amount,open_commitment_amount,etc_amount,eac_amount,includes_open_commitments,assumptions,created_by) values($1,$2,$3,$4,'Approved',$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, projectId, version, actual, commitment, request.EtcAmount, eac, request.IncludesOpenCommitments, Db(request.Assumptions), actor); return new(id, projectId, version, "Approved", actual, commitment, request.EtcAmount, eac, request.IncludesOpenCommitments, request.Assumptions);
    }, ct);

    public Task<CostFactView> RecordCostFactAsync(TenantId tenant, Guid actor, RecordCostFactRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync<CostFactView>(tenant, async (context, token) =>
    {
        var existing = await ReadCostByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (existing is not null) return existing;
        await EnsureProjectLegalEntityAsync(context, tenant.Value, request.ProjectId, request.LegalEntityId, token);
        if (request.OriginalAmount < 0 || request.BaseAmount < 0) throw new ProjectException("projects.cost_amount_invalid");
        if (request.CorrectionOfId is { } correctionId)
        {
            var corrected = await ExecuteAsync(context, "update business_projects.project_cost_fact set state='Corrected' where tenant_id=$1 and id=$2 and project_id=$3 and state='Finalized'", token, tenant.Value, correctionId, request.ProjectId);
            if (corrected == 0) throw new ProjectException("projects.correction_source_invalid");
        }
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_cost_fact(tenant_id,id,project_id,task_id,source_module,source_type,source_id,source_line_ref,cost_category,original_currency,original_amount,base_currency,base_amount,exchange_rate,fact_date,correction_of_id,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)", token, tenant.Value, id, request.ProjectId, Db(request.TaskId), request.SourceModule, request.SourceType, request.SourceId, Db(request.SourceLineRef), request.CostCategory, request.OriginalCurrency.ToUpperInvariant(), request.OriginalAmount, request.BaseCurrency.ToUpperInvariant(), request.BaseAmount, Db(request.ExchangeRate), request.FactDate, Db(request.CorrectionOfId), request.IdempotencyKey); await AuditAsync(context, tenant, request.ProjectId, actor, "cost_fact_recorded", null, request, token); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProjectCostFactRecordedV1", JsonSerializer.Serialize(new { projectId = request.ProjectId, costFactId = id, request.SourceModule, request.SourceType, request.SourceId }), tenant, DateTimeOffset.UtcNow, correlationId, request.ProjectId, "projects")); return await ReadCostAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.cost_fact_not_found");
    }, ct);

    public Task<CommitmentView> UpsertCommitmentAsync(TenantId tenant, RecordCommitmentRequest request, CancellationToken ct) => coordinator.ExecuteAsync<CommitmentView>(tenant, async (context, token) =>
    {
        var current = await ReadCommitmentByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (current is not null) return current;
        await EnsureProjectLegalEntityAsync(context, tenant.Value, request.ProjectId, request.LegalEntityId, token);
        var existingId = await ScalarAsync(context, "select id from business_projects.project_commitment where tenant_id=$1 and source_module=$2 and source_id=$3", token, tenant.Value, request.SourceModule, request.SourceId); var id = existingId is Guid sourceId ? sourceId : Guid.CreateVersion7();
        var open = Math.Max(0, request.CommittedAmount - request.ActualizedAmount); var state = open == 0 ? "Actualized" : request.ActualizedAmount > 0 ? "PartiallyActualized" : "Open";
        await ExecuteAsync(context, "insert into business_projects.project_commitment(tenant_id,id,project_id,source_module,source_id,commitment_date,currency_code,committed_amount,actualized_amount,open_amount,state,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) on conflict (tenant_id,source_module,source_id) do update set project_id=excluded.project_id,commitment_date=excluded.commitment_date,currency_code=excluded.currency_code,committed_amount=excluded.committed_amount,actualized_amount=excluded.actualized_amount,open_amount=excluded.open_amount,state=excluded.state,idempotency_key=excluded.idempotency_key,updated_at=now()", token, tenant.Value, id, request.ProjectId, request.SourceModule, request.SourceId, request.CommitmentDate, request.CurrencyCode.ToUpperInvariant(), request.CommittedAmount, request.ActualizedAmount, open, state, request.IdempotencyKey);
        return await ReadCommitmentAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.commitment_not_found");
    }, ct);

    public Task<CostFactView?> RecordReversalAsync(TenantId tenant, Guid actor, Guid projectId, string sourceModule, string sourceId, decimal amount, string currencyCode, DateOnly factDate, string idempotencyKey, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync<CostFactView?>(tenant, async (context, token) =>
    {
        var original = await ReadCostBySourceAsync(context, tenant.Value, projectId, sourceModule, sourceId, token);
        if (original is null) return null;
        return await RecordCostFactAsync(tenant, actor, new RecordCostFactRequest(projectId, null, sourceModule, "Reversal", sourceId, null, original.CostCategory, currencyCode, amount, currencyCode, amount, null, factDate, idempotencyKey, original.Id), correlationId, token);
    }, ct);

    public Task CancelCommitmentAsync(TenantId tenant, Guid projectId, string sourceModule, string sourceId, CancellationToken ct) => coordinator.ExecuteAsync<object?>(tenant, async (context, token) =>
    {
        await ExecuteAsync(context, "update business_projects.project_commitment set state='Cancelled',open_amount=0,updated_at=now() where tenant_id=$1 and project_id=$2 and source_module=$3 and source_id=$4", token, tenant.Value, projectId, sourceModule, sourceId);
        return null;
    }, ct);

    public Task<FinancialSummary> SummaryAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<FinancialSummary>(tenant, async (context, token) =>
    {
        var budget = await DecimalAsync(context, "select coalesce(sum(current_amount),0) from business_projects.project_budget_version v join business_projects.project_budget b on b.tenant_id=v.tenant_id and b.id=v.budget_id where v.tenant_id=$1 and b.project_id=$2 and v.state='Approved'", token, tenant.Value, projectId); var actual = await DecimalAsync(context, "select coalesce(sum(base_amount),0) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and state='Finalized'", token, tenant.Value, projectId); var commitment = await DecimalAsync(context, "select coalesce(sum(open_amount),0) from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and state in ('Open','PartiallyActualized')", token, tenant.Value, projectId); var etc = await DecimalAsync(context, "select coalesce(etc_amount,0) from business_projects.project_forecast where tenant_id=$1 and project_id=$2 order by version_no desc limit 1", token, tenant.Value, projectId); var eac = await DecimalAsync(context, "select coalesce(eac_amount,0) from business_projects.project_forecast where tenant_id=$1 and project_id=$2 order by version_no desc limit 1", token, tenant.Value, projectId); var eligible = await DecimalAsync(context, "select coalesce(sum(billable_amount),0) from business_projects.project_billable_charge where tenant_id=$1 and project_id=$2 and state in ('Eligible','Requested')", token, tenant.Value, projectId); var invoiced = await DecimalAsync(context, "select coalesce(sum(allocated_amount),0) from business_projects.project_invoice_allocation a join business_projects.project_invoice_request r on r.tenant_id=a.tenant_id and r.id=a.invoice_request_id where r.tenant_id=$1 and r.project_id=$2", token, tenant.Value, projectId); return new(budget, actual, commitment, etc, eac, eligible, invoiced, 0, invoiced, 0 - actual);
    }, ct);

    public Task<IReadOnlyList<(Guid InvoiceId, Guid LegalEntityId)>> ListInvoiceSourcesAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<(Guid InvoiceId, Guid LegalEntityId)>>(tenant, async (context, token) =>
    {
        var result = new List<(Guid, Guid)>();
        await using var command = Command(context, "select distinct a.invoice_id,p.legal_entity_id from business_projects.project_invoice_allocation a join business_projects.project_invoice_request r on r.tenant_id=a.tenant_id and r.id=a.invoice_request_id join business_projects.project p on p.tenant_id=r.tenant_id and p.id=r.project_id where a.tenant_id=$1 and r.project_id=$2 and a.invoice_id is not null and a.allocated_amount>0", tenant.Value, projectId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add((reader.GetGuid(0), reader.GetGuid(1)));
        return result;
    }, ct);

    public Task<BillingRuleView> CreateBillingRuleAsync(TenantId tenant, Guid actor, Guid projectId, CreateBillingRuleRequest request, CancellationToken ct) => coordinator.ExecuteAsync<BillingRuleView>(tenant, async (context, token) =>
    {
        var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_projects.project_billing_rule where tenant_id=$1 and project_id=$2", token, tenant.Value, projectId), CultureInfo.InvariantCulture); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_billing_rule(tenant_id,id,project_id,version_no,model,status,contract_value,currency_code,markup_percent,billing_cap,effective_from) values($1,$2,$3,$4,$5,'Active',$6,$7,$8,$9,$10)", token, tenant.Value, id, projectId, version, request.Model, Db(request.ContractValue), Db(NormalizeCurrency(request.CurrencyCode)), request.MarkupPercent, Db(request.BillingCap), request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow)); return new(id, projectId, version, request.Model, "Active", request.ContractValue, NormalizeCurrency(request.CurrencyCode), request.MarkupPercent, request.BillingCap);
    }, ct);

    public Task<BillableChargeView> CreateChargeAsync(TenantId tenant, Guid actor, CreateBillableChargeRequest request, CancellationToken ct) => coordinator.ExecuteAsync<BillableChargeView>(tenant, async (context, token) =>
    {
        var existing = await ReadChargeByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (existing is not null) return existing; var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_billable_charge(tenant_id,id,project_id,source_type,source_id,charge_date,description,currency_code,cost_amount,billable_amount,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, request.ProjectId, request.SourceType, request.SourceId, request.ChargeDate, request.Description, request.CurrencyCode.ToUpperInvariant(), request.CostAmount, request.BillableAmount, request.IdempotencyKey); return await ReadChargeAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.charge_not_found");
    }, ct);

    public Task<IReadOnlyList<ProjectInvoiceChargeSource>> ListInvoiceChargeSourcesAsync(TenantId tenant, Guid projectId, IReadOnlyList<Guid> chargeIds, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<ProjectInvoiceChargeSource>>(tenant, async (context, token) =>
    {
        var result = new List<ProjectInvoiceChargeSource>();
        foreach (var chargeId in chargeIds)
        {
            await using var command = Command(context, "select id,description,billable_amount-invoiced_amount,trim(currency_code) from business_projects.project_billable_charge where tenant_id=$1 and project_id=$2 and id=$3 and state in ('Eligible','Requested','PartiallyInvoiced') and billable_amount > invoiced_amount", tenant.Value, projectId, chargeId);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ProjectException("projects.charge_not_eligible"); result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3)));
        }
        return result;
    }, ct);

    public Task<InvoiceRequestView> RequestInvoiceAsync(TenantId tenant, Guid actor, RequestInvoiceRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync<InvoiceRequestView>(tenant, async (context, token) =>
    {
        var existing = await ReadInvoiceRequestByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (existing is not null) return existing;
        if (request.ChargeIds.Count == 0 || request.ChargeIds.Count != request.ChargeIds.Distinct().Count() || request.RequestedAmount < 0) throw new ProjectException("projects.invoice_request_invalid");
        var total = 0m; var currency = request.CurrencyCode.ToUpperInvariant();
        foreach (var charge in request.ChargeIds)
        {
            await using var chargeCommand = Command(context, "select billable_amount,trim(currency_code) from business_projects.project_billable_charge where tenant_id=$1 and id=$2 and project_id=$3 and state='Eligible'", tenant.Value, charge, request.ProjectId);
            await using var chargeReader = await chargeCommand.ExecuteReaderAsync(token);
            if (!await chargeReader.ReadAsync(token)) throw new ProjectException("projects.charge_not_eligible");
            if (!string.Equals(currency, chargeReader.GetString(1), StringComparison.OrdinalIgnoreCase)) throw new ProjectException("projects.invoice_currency_mismatch");
            total += chargeReader.GetDecimal(0);
        }
        if (request.RequestedAmount != total) throw new ProjectException("projects.invoice_amount_mismatch");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_projects.project_invoice_request(tenant_id,id,project_id,idempotency_key,requested_amount,currency_code) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, request.ProjectId, request.IdempotencyKey, request.RequestedAmount, currency);
        foreach (var charge in request.ChargeIds) { await ExecuteAsync(context, "insert into business_projects.project_invoice_allocation(tenant_id,id,invoice_request_id,charge_id,invoice_id,allocated_amount) select $1,$2,$3,id,null,billable_amount-invoiced_amount from business_projects.project_billable_charge where tenant_id=$1 and id=$4", token, tenant.Value, Guid.CreateVersion7(), id, charge); await ExecuteAsync(context, "update business_projects.project_billable_charge set state='Requested',invoice_request_id=$3 where tenant_id=$1 and id=$2", token, tenant.Value, charge, id); }
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProjectInvoiceRequestedV1", JsonSerializer.Serialize(new { projectId = request.ProjectId, invoiceRequestId = id, request.RequestedAmount }), tenant, DateTimeOffset.UtcNow, correlationId, request.ProjectId, "projects")); return await ReadInvoiceRequestAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.invoice_request_not_found");
    }, ct);

    public Task<InvoiceRequestView> ApplyInvoiceResultAsync(TenantId tenant, Guid actor, Guid invoiceRequestId, ApplyInvoiceResultRequest request, CancellationToken ct) => coordinator.ExecuteAsync<InvoiceRequestView>(tenant, async (context, token) =>
    {
        if (request.State is not ("Accepted" or "Rejected" or "Unknown" or "Completed" or "Cancelled")) throw new ProjectException("projects.invoice_result_invalid");
        var existing = await ReadInvoiceRequestAsync(context, tenant.Value, invoiceRequestId, token) ?? throw new ProjectException("projects.invoice_request_not_found");
        if (request.State is "Accepted" or "Completed" && request.InvoiceId is null) throw new ProjectException("projects.invoice_id_required");
        if (existing.State is "Completed" or "Rejected" or "Cancelled")
        {
            if (existing.State != request.State || existing.InvoiceId != request.InvoiceId) throw new ProjectException("projects.invoice_result_conflict");
            return existing;
        }
        var amount = request.InvoicedAmount ?? existing.RequestedAmount;
        if (amount < 0 || amount > existing.RequestedAmount) throw new ProjectException("projects.invoice_amount_invalid");
        await ExecuteAsync(context, "update business_projects.project_invoice_request set state=$3,invoice_id=$4,failure_code=$5,completed_at=case when $3 in ('Completed','Rejected','Cancelled') then now() else completed_at end where tenant_id=$1 and id=$2", token, tenant.Value, invoiceRequestId, request.State, Db(request.InvoiceId), Db(request.FailureCode));
        if (request.State == "Completed")
        {
            var remaining = amount;
            await using var allocations = Command(context, "select a.id,a.charge_id,a.allocated_amount,c.billable_amount,c.invoiced_amount from business_projects.project_invoice_allocation a join business_projects.project_billable_charge c on c.tenant_id=a.tenant_id and c.id=a.charge_id where a.tenant_id=$1 and a.invoice_request_id=$2 order by a.id for update", tenant.Value, invoiceRequestId);
            await using var allocationReader = await allocations.ExecuteReaderAsync(token);
            var rows = new List<(Guid AllocationId, Guid ChargeId, decimal Requested, decimal Billable, decimal Invoiced)>();
            while (await allocationReader.ReadAsync(token)) rows.Add((allocationReader.GetGuid(0), allocationReader.GetGuid(1), allocationReader.GetDecimal(2), allocationReader.GetDecimal(3), allocationReader.GetDecimal(4)));
            await allocationReader.CloseAsync();
            foreach (var row in rows)
            {
                var applied = Math.Min(row.Requested, Math.Max(0m, remaining));
                remaining -= applied;
                await ExecuteAsync(context, "update business_projects.project_invoice_allocation set allocated_amount=$3,invoice_id=$4 where tenant_id=$1 and id=$2", token, tenant.Value, row.AllocationId, applied, request.InvoiceId!.Value);
                var newInvoiced = row.Invoiced + applied;
                var state = newInvoiced >= row.Billable ? "Invoiced" : applied > 0 ? "PartiallyInvoiced" : "Eligible";
                await ExecuteAsync(context, "update business_projects.project_billable_charge set invoiced_amount=$3,state=$4,invoice_request_id=case when $4='Eligible' then null else $5 end where tenant_id=$1 and id=$2", token, tenant.Value, row.ChargeId, newInvoiced, state, applied > 0 ? invoiceRequestId : (object)DBNull.Value);
            }
            if (remaining != 0) throw new ProjectException("projects.invoice_allocation_mismatch");
        }
        else if (request.State == "Rejected" || request.State == "Cancelled")
        {
            await ExecuteAsync(context, "update business_projects.project_invoice_allocation set allocated_amount=0,invoice_id=null where tenant_id=$1 and invoice_request_id=$2", token, tenant.Value, invoiceRequestId);
            await ExecuteAsync(context, "update business_projects.project_billable_charge c set state=case when c.invoiced_amount=0 then 'Eligible' else 'PartiallyInvoiced' end,invoice_request_id=null where c.tenant_id=$1 and c.invoice_request_id=$2", token, tenant.Value, invoiceRequestId);
        }
        await AuditAsync(context, tenant, existing.ProjectId, actor, "invoice_result_applied", existing, request, token);
        return await ReadInvoiceRequestAsync(context, tenant.Value, invoiceRequestId, token) ?? throw new ProjectException("projects.invoice_request_not_found");
    }, ct);

    public Task<ChangeRequestView> CreateChangeAsync(TenantId tenant, Guid actor, Guid projectId, CreateChangeRequest request, CancellationToken ct) => coordinator.ExecuteAsync<ChangeRequestView>(tenant, async (context, token) => { ProjectRules.EnsureMutable(await StateAsync(context, tenant.Value, projectId, token)); var legalEntityId = await ScalarAsync<Guid>(context, "select legal_entity_id from business_projects.project where tenant_id=$1 and id=$2", token, tenant.Value, projectId); var id = Guid.CreateVersion7(); var number = await sequences.AllocateAsync(context, new SequenceDefinition("projects.change-request", tenant, legalEntityId, null, null, null, "CR-", 7, 1), token); await ExecuteAsync(context, "insert into business_projects.project_change_request(tenant_id,id,project_id,request_number,title,reason,contract_delta,budget_delta,schedule_delta_days,requested_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, projectId, number, request.Title.Trim(), Db(request.Reason), request.ContractDelta, request.BudgetDelta, request.ScheduleDeltaDays, actor); return new(id, projectId, number, request.Title.Trim(), "Draft", request.ContractDelta, request.BudgetDelta, request.ScheduleDeltaDays); }, ct);

    public Task<ChangeRequestView> DecideChangeAsync(TenantId tenant, Guid actor, Guid projectId, Guid changeId, ChangeDecisionRequest request, CancellationToken ct) => coordinator.ExecuteAsync<ChangeRequestView>(tenant, async (context, token) => { if (request.Decision is not ("approve" or "reject")) throw new ProjectException("projects.change_decision_invalid"); var status = request.Decision == "approve" ? "Approved" : "Rejected"; var row = await ReadChangeAsync(context, tenant.Value, changeId, token) ?? throw new ProjectException("projects.change_not_found"); await ExecuteAsync(context, "update business_projects.project_change_request set status=$3 where tenant_id=$1 and id=$2 and status in ('Draft','Submitted')", token, tenant.Value, changeId, status); if (status == "Approved") { var legalEntityId = await ScalarAsync<Guid>(context, "select legal_entity_id from business_projects.project where tenant_id=$1 and id=$2", token, tenant.Value, projectId); var orderNumber = await sequences.AllocateAsync(context, new SequenceDefinition("projects.change-order", tenant, legalEntityId, null, null, null, "CO-", 7, 1), token); await ExecuteAsync(context, "insert into business_projects.project_change_order(tenant_id,id,project_id,change_request_id,order_number,contract_delta,budget_delta,schedule_delta_days,approved_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, Guid.CreateVersion7(), projectId, changeId, orderNumber, row.ContractDelta, row.BudgetDelta, row.ScheduleDeltaDays, actor); } return row with { Status = status }; }, ct);

    public Task<CloseView> CloseReadinessAsync(TenantId tenant, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<CloseView>(tenant, async (context, token) => { await StateAsync(context, tenant.Value, projectId, token); var blockers = await CloseBlockersAsync(context, tenant.Value, projectId, token); var state = await ScalarAsync<string>(context, "select state from business_projects.project_close where tenant_id=$1 and project_id=$2", token, tenant.Value, projectId) ?? "Open"; return new(projectId, state, blockers); }, ct);
    public Task<CloseView> CloseAsync(TenantId tenant, Guid actor, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<CloseView>(tenant, async (context, token) => { var current = await StateAsync(context, tenant.Value, projectId, token); new ProjectAggregate(projectId, current).TransitionTo(ProjectStates.Closed); var blockers = await CloseBlockersAsync(context, tenant.Value, projectId, token); if (blockers.Count > 0) throw new ProjectException("projects.close_blocked"); await ExecuteAsync(context, "insert into business_projects.project_close(tenant_id,id,project_id,state,requested_by,requested_at,closed_by,closed_at) values($1,$2,$3,'Closed',$4,now(),$4,now()) on conflict (tenant_id,project_id) do update set state='Closed',closed_by=$4,closed_at=now()", token, tenant.Value, Guid.CreateVersion7(), projectId, actor); var updated = await ExecuteAsync(context, "update business_projects.project set lifecycle_state='Closed',row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and lifecycle_state='FinancialClosing'", token, tenant.Value, projectId); if (updated == 0) throw new ProjectException("projects.concurrency_conflict"); return new(projectId, "Closed", Array.Empty<string>()); }, ct);
    public Task<CloseView> ReopenAsync(TenantId tenant, Guid actor, Guid projectId, CancellationToken ct) => coordinator.ExecuteAsync<CloseView>(tenant, async (context, token) => { var current = await StateAsync(context, tenant.Value, projectId, token); if (current != ProjectStates.Closed) throw new ProjectException("projects.project_not_closed"); await ExecuteAsync(context, "update business_projects.project_close set state='Reopened',reopened_by=$3,reopened_at=now() where tenant_id=$1 and project_id=$2", token, tenant.Value, projectId, actor); var updated = await ExecuteAsync(context, "update business_projects.project set lifecycle_state='FinancialClosing',row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and lifecycle_state='Closed'", token, tenant.Value, projectId); if (updated == 0) throw new ProjectException("projects.concurrency_conflict"); return new(projectId, "Reopened", Array.Empty<string>()); }, ct);

    public Task<CapitalHandoffView> CreateCapitalHandoffAsync(TenantId tenant, Guid actor, Guid projectId, CapitalHandoffRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync<CapitalHandoffView>(tenant, async (context, token) => { var existing = await ReadHandoffByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (existing is not null) return existing; var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_projects.project_capital_handoff(tenant_id,id,project_id,source_cost_fact_id,amount,currency_code,idempotency_key) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, projectId, request.CostFactId, request.Amount, request.CurrencyCode.ToUpperInvariant(), request.IdempotencyKey); var legalEntityId = await ScalarAsync<Guid>(context, "select legal_entity_id from business_projects.project where tenant_id=$1 and id=$2", token, tenant.Value, projectId); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProjectCapitalHandoffRequestedV1", JsonSerializer.Serialize(new { projectId, legalEntityId, handoffId = id, costFactId = request.CostFactId, amount = request.Amount, currencyCode = request.CurrencyCode.ToUpperInvariant() }), tenant, DateTimeOffset.UtcNow, correlationId, projectId, "projects")); return await ReadHandoffAsync(context, tenant.Value, id, token) ?? throw new ProjectException("projects.capital_handoff_not_found"); }, ct);

    public Task ApplyCapitalHandoffResultAsync(TenantId tenant, Guid handoffId, string state, string? externalReference, CancellationToken ct) => coordinator.ExecuteAsync<object?>(tenant, async (context, token) =>
    {
        if (state is not ("Accepted" or "Rejected" or "Unknown")) throw new ProjectException("projects.capital_handoff_state_invalid");
        await ExecuteAsync(context, "update business_projects.project_capital_handoff set state=$3,external_reference=$4,resolved_at=now() where tenant_id=$1 and id=$2 and state in ('Requested','Unknown')", token, tenant.Value, handoffId, state, Db(externalReference));
        return null;
    }, ct);

    public Task<IReadOnlyList<ProjectCostFactContract>> ListCostFactsAsync(TenantId tenant, Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct) => coordinator.ExecuteAsync<IReadOnlyList<ProjectCostFactContract>>(tenant, async (context, token) =>
    {
        var result = new List<ProjectCostFactContract>();
        await using var command = Command(context, "select id,source_module,source_type,source_id,cost_category,base_amount,base_currency,fact_date from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and ($3::date is null or fact_date >= $3) and ($4::date is null or fact_date <= $4) order by fact_date,id", tenant.Value, projectId, Db(from), Db(to));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new(projectId, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6).Trim(), reader.GetFieldValue<DateOnly>(7), reader.GetGuid(0)));
        return result;
    }, ct);

    private static async Task<ProjectDetail?> GetDetailAsync(ITransactionContext context, TenantId tenant, Guid id, CancellationToken ct)
    {
        const string sql = "select p.id,p.legal_entity_id,p.project_number,p.name,p.description,p.project_type,p.customer_party_id,p.manager_employee_id,p.start_date,p.target_end_date,p.actual_end_date,p.lifecycle_state,p.execution_health,p.billing_model,p.currency_code,p.row_version,coalesce((select sum(v.current_amount) from business_projects.project_budget_version v join business_projects.project_budget b on b.tenant_id=v.tenant_id and b.id=v.budget_id where v.tenant_id=p.tenant_id and b.project_id=p.id and v.state='Approved'),0),coalesce((select sum(base_amount) from business_projects.project_cost_fact where tenant_id=p.tenant_id and project_id=p.id and state='Finalized'),0),coalesce((select sum(open_amount) from business_projects.project_commitment where tenant_id=p.tenant_id and project_id=p.id and state in ('Open','PartiallyActualized')),0),coalesce((select etc_amount from business_projects.project_forecast where tenant_id=p.tenant_id and project_id=p.id order by version_no desc limit 1),0),coalesce((select eac_amount from business_projects.project_forecast where tenant_id=p.tenant_id and project_id=p.id order by version_no desc limit 1),0),coalesce((select sum(billable_amount) from business_projects.project_billable_charge where tenant_id=p.tenant_id and project_id=p.id and state in ('Eligible','Requested')),0),coalesce((select sum(allocated_amount) from business_projects.project_invoice_allocation a join business_projects.project_invoice_request r on r.tenant_id=a.tenant_id and r.id=a.invoice_request_id where r.tenant_id=p.tenant_id and r.project_id=p.id),0) from business_projects.project p where p.tenant_id=$1 and p.id=$2";
        await using var command = Command(context, sql, tenant.Value, id); await using var reader = await command.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null; var budget = reader.GetDecimal(16); var actual = reader.GetDecimal(17); var commitment = reader.GetDecimal(18); var etc = reader.GetDecimal(19); var eac = reader.GetDecimal(20); var eligible = reader.GetDecimal(21); var invoiced = reader.GetDecimal(22); return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetFieldValue<DateOnly>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetString(14), reader.GetInt64(15), budget, actual, commitment, etc, eac, eligible, invoiced, 0, invoiced);
    }
    private static async Task<string> StateAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) => (string?)await ScalarAsync(c, "select lifecycle_state from business_projects.project where tenant_id=$1 and id=$2", ct, tenant, id) ?? throw new ProjectException("projects.project_not_found");
    private static async Task EnsureCloseReadyAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { var blockers = await CloseBlockersAsync(c, tenant, id, ct); if (blockers.Count > 0) throw new ProjectException("projects.close_blocked"); }
    private static async Task<IReadOnlyList<string>> CloseBlockersAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { var blockers = new List<string>(); if (Convert.ToInt64(await ScalarAsync(c, "select count(*) from business_projects.project_time_entry where tenant_id=$1 and project_id=$2 and state in ('Draft','Submitted','Approved')", ct, tenant, id), CultureInfo.InvariantCulture) > 0) blockers.Add("unresolved_time"); if (Convert.ToDecimal(await ScalarAsync(c, "select coalesce(sum(open_amount),0) from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and open_amount>0", ct, tenant, id), CultureInfo.InvariantCulture) > 0) blockers.Add("open_commitment"); if (Convert.ToInt64(await ScalarAsync(c, "select count(*) from business_projects.project_billable_charge where tenant_id=$1 and project_id=$2 and state in ('Eligible','Requested')", ct, tenant, id), CultureInfo.InvariantCulture) > 0) blockers.Add("unresolved_charge"); if (Convert.ToInt64(await ScalarAsync(c, "select count(*) from business_projects.project_capital_handoff where tenant_id=$1 and project_id=$2 and state in ('Requested','Unknown')", ct, tenant, id), CultureInfo.InvariantCulture) > 0) blockers.Add("unresolved_capital_handoff"); return blockers; }
    private static async Task<T?> ScalarAsync<T>(ITransactionContext c, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(c, sql, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? default : (T)value; }
    private static async Task<object?> ScalarAsync(ITransactionContext c, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(c, sql, values); var value = await command.ExecuteScalarAsync(ct); return value is null or DBNull ? null : value; }
    private static async Task<decimal> DecimalAsync(ITransactionContext c, string sql, CancellationToken ct, params object?[] values) => Convert.ToDecimal(await ScalarAsync(c, sql, ct, values), CultureInfo.InvariantCulture);
    private static async Task<int> ExecuteAsync(ITransactionContext c, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(c, sql, values); return await command.ExecuteNonQueryAsync(ct); }
    private static NpgsqlCommand Command(ITransactionContext c, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, c.Connection, c.Transaction); for (var i=0;i<values.Length;i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); return command; }
    private static object Db(object? value) => value ?? DBNull.Value;
    private static string? NormalizeCurrency(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static async Task AuditAsync(ITransactionContext c, TenantId tenant, Guid projectId, Guid actor, string action, object? before, object? after, CancellationToken ct) => await ExecuteAsync(c, "insert into business_projects.project_audit_event(tenant_id,id,project_id,action,actor_id,before_json,after_json) values($1,$2,$3,$4,$5,$6::jsonb,$7::jsonb)", ct, tenant.Value, Guid.CreateVersion7(), projectId, action, actor, Db(before is null ? null : JsonSerializer.Serialize(before)), Db(after is null ? null : JsonSerializer.Serialize(after)));
    private static ProjectTaskView ReadTask(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateOnly>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateOnly>(8), r.GetDecimal(9), r.GetInt32(10), r.GetInt64(11));
    private static async Task<ProjectTaskView?> ReadTaskAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,title,description,status,priority,assigned_employee_id,planned_start,planned_end,progress_percent,sequence_no,row_version from business_projects.project_task where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadTask(r) : null; }
    private static MilestoneView ReadMilestone(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetString(8));
    private static async Task<MilestoneView?> ReadMilestoneAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,task_id,name,due_date,reached_at,accepted_at,acceptance_reference,status from business_projects.project_milestone where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadMilestone(r) : null; }
    private static ProjectTeamMemberView ReadTeam(NpgsqlDataReader r) => new(r.GetGuid(0), r.IsDBNull(1)?null:r.GetGuid(1), r.IsDBNull(2)?null:r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetFieldValue<DateOnly>(5), r.IsDBNull(6)?null:r.GetFieldValue<DateOnly>(6), r.GetBoolean(7));
    private static async Task<ProjectTeamMemberView?> ReadTeamAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,employee_id,user_id,role_code,allocation_percent,active_from,active_to,is_manager from business_projects.project_team_member where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadTeam(r) : null; }
    private sealed record TimeFinalizationSource(Guid ProjectId, Guid? TaskId, Guid EmployeeId, DateOnly WorkDate, int DurationMinutes, bool Billable);
    private readonly record struct ResolvedRates(decimal? CostRate, decimal? BillingRate, string CurrencyCode);
    private static async Task<TimeFinalizationSource?> ReadTimeFinalizationSourceAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct)
    {
        await using var cmd = Command(c, "select project_id,task_id,employee_id,work_date,duration_minutes,billable from business_projects.project_time_entry where tenant_id=$1 and id=$2", tenant, id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.IsDBNull(1) ? null : r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetInt32(4), r.GetBoolean(5)) : null;
    }
    private static async Task<ResolvedRates?> ResolveRatesAsync(ITransactionContext c, Guid tenant, Guid timeId, CancellationToken ct)
    {
        const string sql = "select l.cost_rate,l.billing_rate,trim(rc.currency_code) from business_projects.project_time_entry t join business_projects.project p on p.tenant_id=t.tenant_id and p.id=t.project_id join business_projects.project_team_member tm on tm.tenant_id=t.tenant_id and tm.project_id=t.project_id and tm.employee_id=t.employee_id and tm.active_from <= t.work_date and (tm.active_to is null or tm.active_to >= t.work_date) join business_projects.project_rate_card rc on rc.tenant_id=p.tenant_id and rc.legal_entity_id=p.legal_entity_id and rc.status='Active' and rc.effective_from <= t.work_date and (rc.effective_to is null or rc.effective_to >= t.work_date) join business_projects.project_rate_card_line l on l.tenant_id=rc.tenant_id and l.rate_card_id=rc.id and l.role_code=tm.role_code and (l.employee_id=t.employee_id or l.employee_id is null) where t.tenant_id=$1 and t.id=$2 order by case when rc.project_id=t.project_id then 0 else 1 end,case when l.employee_id=t.employee_id then 0 else 1 end,rc.effective_from desc,rc.id desc limit 1";
        await using var command = Command(c, sql, tenant, timeId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.IsDBNull(0) ? null : reader.GetDecimal(0), reader.IsDBNull(1) ? null : reader.GetDecimal(1), reader.GetString(2)) : null;
    }
    private static TimeEntryView ReadTime(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.IsDBNull(2)?null:r.GetGuid(2),r.GetGuid(3),r.GetFieldValue<DateOnly>(4),r.GetInt32(5),r.GetBoolean(6),r.GetString(7),r.IsDBNull(8)?null:r.GetDecimal(8),r.IsDBNull(9)?null:r.GetDecimal(9),r.GetInt64(10));
    private static async Task<TimeEntryView?> ReadTimeAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,task_id,employee_id,work_date,duration_minutes,billable,state,cost_amount,billing_amount,row_version from business_projects.project_time_entry where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadTime(r) : null; }
    private static async Task<string> ReadTimeStateAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) => (string?)await ScalarAsync(c,"select state from business_projects.project_time_entry where tenant_id=$1 and id=$2",ct,tenant,id) ?? throw new ProjectException("projects.time_not_found");
    private static CostFactView ReadCost(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetDecimal(6),r.GetString(7).Trim(),r.GetDecimal(8),r.GetString(9).Trim(),r.GetString(10),r.IsDBNull(11)?null:r.GetGuid(11));
    private static async Task<CostFactView?> ReadCostAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,source_module,source_type,source_id,cost_category,original_amount,original_currency,base_amount,base_currency,state,correction_of_id from business_projects.project_cost_fact where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCost(r) : null; }
    private static async Task<CostFactView?> ReadCostByKeyAsync(ITransactionContext c, Guid tenant, string key, CancellationToken ct) { var id=await ScalarAsync(c,"select id from business_projects.project_cost_fact where tenant_id=$1 and idempotency_key=$2",ct,tenant,key); return id is Guid value ? await ReadCostAsync(c,tenant,value,ct) : null; }
    private static async Task<CostFactView?> ReadCostBySourceAsync(ITransactionContext c, Guid tenant, Guid projectId, string sourceModule, string sourceId, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,source_module,source_type,source_id,cost_category,original_amount,original_currency,base_amount,base_currency,state,correction_of_id from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module=$3 and source_id=$4 and state='Finalized' order by created_at desc,id desc limit 1",tenant,projectId,sourceModule,sourceId); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCost(r) : null; }
    private static async Task EnsureProjectLegalEntityAsync(ITransactionContext c, Guid tenant, Guid projectId, Guid? legalEntityId, CancellationToken ct) { var value = await ScalarAsync(c, "select legal_entity_id from business_projects.project where tenant_id=$1 and id=$2 and lifecycle_state not in ('Closed','Cancelled')", ct, tenant, projectId); if (value is null) throw new ProjectException("projects.project_not_found"); if (legalEntityId is { } expected && value is Guid actual && actual != expected) throw new ProjectException("projects.legal_entity_mismatch"); }
    private static CommitmentView ReadCommitment(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDecimal(6),r.GetString(7),r.GetString(8).Trim());
    private static async Task<CommitmentView?> ReadCommitmentAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,source_module,source_id,committed_amount,actualized_amount,open_amount,state,currency_code from business_projects.project_commitment where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCommitment(r) : null; }
    private static async Task<CommitmentView?> ReadCommitmentByKeyAsync(ITransactionContext c, Guid tenant, string key, CancellationToken ct) { var id=await ScalarAsync(c,"select id from business_projects.project_commitment where tenant_id=$1 and idempotency_key=$2",ct,tenant,key); return id is Guid value ? await ReadCommitmentAsync(c,tenant,value,ct) : null; }
    private static BillableChargeView ReadCharge(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetDecimal(4),r.GetDecimal(5),r.GetString(6).Trim(),r.GetString(7),r.IsDBNull(8)?null:r.GetGuid(8));
    private static async Task<BillableChargeView?> ReadChargeAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,source_type,source_id,cost_amount,billable_amount,currency_code,state,invoice_request_id from business_projects.project_billable_charge where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCharge(r) : null; }
    private static async Task<BillableChargeView?> ReadChargeByKeyAsync(ITransactionContext c, Guid tenant, string key, CancellationToken ct) { var id=await ScalarAsync(c,"select id from business_projects.project_billable_charge where tenant_id=$1 and idempotency_key=$2",ct,tenant,key); return id is Guid value ? await ReadChargeAsync(c,tenant,value,ct) : null; }
    private static async Task<InvoiceRequestView?> ReadInvoiceRequestAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,requested_amount,currency_code,state,invoice_id from business_projects.project_invoice_request where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0),r.GetGuid(1),r.GetDecimal(2),r.GetString(3).Trim(),r.GetString(4),r.IsDBNull(5)?null:r.GetGuid(5)) : null; }
    private static async Task<InvoiceRequestView?> ReadInvoiceRequestByKeyAsync(ITransactionContext c, Guid tenant, string key, CancellationToken ct) { var id=await ScalarAsync(c,"select id from business_projects.project_invoice_request where tenant_id=$1 and idempotency_key=$2",ct,tenant,key); return id is Guid value ? await ReadInvoiceRequestAsync(c,tenant,value,ct) : null; }
    private static async Task<ChangeRequestView?> ReadChangeAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,request_number,title,status,contract_delta,budget_delta,schedule_delta_days from business_projects.project_change_request where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetDecimal(5),r.GetDecimal(6),r.GetInt32(7)) : null; }
    private static async Task<CapitalHandoffView?> ReadHandoffAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd=Command(c,"select id,project_id,source_cost_fact_id,amount,currency_code,state,external_reference from business_projects.project_capital_handoff where tenant_id=$1 and id=$2",tenant,id); await using var r=await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetDecimal(3),r.GetString(4).Trim(),r.GetString(5),r.IsDBNull(6)?null:r.GetString(6)) : null; }
    private static async Task<CapitalHandoffView?> ReadHandoffByKeyAsync(ITransactionContext c, Guid tenant, string key, CancellationToken ct) { var id=await ScalarAsync(c,"select id from business_projects.project_capital_handoff where tenant_id=$1 and idempotency_key=$2",ct,tenant,key); return id is Guid value ? await ReadHandoffAsync(c,tenant,value,ct) : null; }
}
