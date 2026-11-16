using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Npgsql;
using Modules.Service.Domain;
using Modules.Service.DTOs;
using Shared.Primitives.Identifiers;

namespace Modules.Service.Services;

public sealed class ServiceService(ITransactionCoordinator coordinator)
{
    public Task<ServiceRequestView> CreateRequestAsync(TenantId tenant, Guid actor, Guid correlationId, CreateServiceRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ValidateContext(request.Context, request.CustomerPartyId, request.InstalledBaseId);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 240) throw new ServiceException("service.request_title_invalid");
        if (request.InstalledBaseId is { } installedBase && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, installedBase, token)) throw new ServiceException("service.installed_base_not_found");
        var id = Guid.CreateVersion7();
        var number = Number("REQ", id);
        await ExecuteAsync(context, "insert into business_service.service_request(tenant_id,id,legal_entity_id,request_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,state,priority,requested_at,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'New',$11,coalesce($12,now()),$13)", token, tenant.Value, id, request.LegalEntityId, number, request.Context, request.Title.Trim(), Db(request.Description), Db(request.CustomerPartyId), Db(request.InstalledBaseId), Db(request.FixedAssetId), request.Priority, Db(request.RequestedAt), actor);
        await AuditAsync(context, tenant, "ServiceRequest", id, actor, "request_created", null, new { number, request.Context, request.Title }, token);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ServiceRequestCreatedV1", JsonSerializer.Serialize(new { requestId = id, requestNumber = number, request.LegalEntityId }), tenant, DateTimeOffset.UtcNow, correlationId, id, "service"));
        return await ReadRequestAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.request_not_found");
    }, ct);

    public Task<PagedResult<ServiceRequestView>> ListRequestsAsync(TenantId tenant, Guid? legalEntityId, string? state, string? search, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize);
        var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_request where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) and ($4::text is null or title ilike '%'||$4||'%' or request_number ilike '%'||$4||'%')", token, tenant.Value, Db(legalEntityId), Db(state), Db(search)), CultureInfo.InvariantCulture);
        var result = new List<ServiceRequestView>();
        await using var command = Command(context, "select id,legal_entity_id,request_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,state,priority,requested_at,row_version from business_service.service_request where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) and ($4::text is null or title ilike '%'||$4||'%' or request_number ilike '%'||$4||'%') order by requested_at desc,id desc limit $5 offset $6", tenant.Value, Db(legalEntityId), Db(state), Db(search), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadRequest(reader));
        var hasNext = result.Count > pageSize; if (hasNext) result.RemoveAt(result.Count - 1);
        return new PagedResult<ServiceRequestView>(result, page, pageSize, hasNext, total);
    }, ct);

    public Task<ServiceRequestView> TransitionRequestAsync(TenantId tenant, Guid actor, Guid requestId, string action, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadRequestAsync(context, tenant.Value, requestId, token) ?? throw new ServiceException("service.request_not_found");
        var next = action switch
        {
            "triage" when current.State == "New" => "Triaged",
            "resolve" when current.State is "Triaged" or "InProgress" => "Resolved",
            "close" when current.State == "Resolved" => "Closed",
            "cancel" when current.State is not "Closed" and not "Cancelled" => "Cancelled",
            _ => throw new ServiceException("service.request_transition_invalid")
        };
        var column = next switch { "Triaged" => "triaged_at", "Resolved" => "resolved_at", "Closed" => "closed_at", _ => null };
        var timestamp = column is null ? string.Empty : $",{column}=now()";
        await ExecuteAsync(context, $"update business_service.service_request set state=$3,row_version=row_version+1{timestamp} where tenant_id=$1 and id=$2 and row_version=$4", token, tenant.Value, requestId, next, current.Version);
        await AuditAsync(context, tenant, "ServiceRequest", requestId, actor, $"request_{action}", new { current.State }, new { next }, token);
        return await ReadRequestAsync(context, tenant.Value, requestId, token) ?? throw new ServiceException("service.request_not_found");
    }, ct);

    public Task<WorkOrderView> CreateWorkOrderAsync(TenantId tenant, Guid actor, Guid correlationId, CreateWorkOrderRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ValidateContext(request.Context, request.CustomerPartyId, request.InstalledBaseId);
        if (request.BillingOwner == "Projects" && request.ProjectId is null) throw new ServiceException("service.project_billing_owner_required");
        if (request.ServiceRequestId is { } requestId && !await ExistsAsync(context, "business_service.service_request", tenant.Value, requestId, token)) throw new ServiceException("service.request_not_found");
        if (request.InstalledBaseId is { } installedBase && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, installedBase, token)) throw new ServiceException("service.installed_base_not_found");
        var id = Guid.CreateVersion7(); var number = Number("WO", id);
        await ExecuteAsync(context, "insert into business_service.work_order(tenant_id,id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,service_request_id,billing_owner,work_type,priority,state,planned_start,planned_end,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,'Draft',$16,$17,$18)", token, tenant.Value, id, request.LegalEntityId, number, request.Context, request.Title.Trim(), Db(request.Description), Db(request.CustomerPartyId), Db(request.InstalledBaseId), Db(request.FixedAssetId), Db(request.ProjectId), Db(request.ServiceRequestId), request.BillingOwner, request.WorkType, request.Priority, Db(request.PlannedStart), Db(request.PlannedEnd), actor);
        if (request.InstalledBaseId is { } subject) await ExecuteAsync(context, "insert into business_service.work_order_subject(tenant_id,id,work_order_id,subject_type,subject_id) values($1,$2,$3,'InstalledBase',$4)", token, tenant.Value, Guid.CreateVersion7(), id, subject);
        if (request.FixedAssetId is { } asset) await ExecuteAsync(context, "insert into business_service.work_order_subject(tenant_id,id,work_order_id,subject_type,subject_id) values($1,$2,$3,'FixedAsset',$4)", token, tenant.Value, Guid.CreateVersion7(), id, asset);
        if (request.ServiceRequestId is { } linked) await ExecuteAsync(context, "insert into business_service.work_order_request_link(tenant_id,id,work_order_id,request_id) values($1,$2,$3,$4)", token, tenant.Value, Guid.CreateVersion7(), id, linked);
        await AuditAsync(context, tenant, "WorkOrder", id, actor, "work_order_created", null, new { number, request.Context, request.BillingOwner }, token);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "WorkOrderCreatedV1", JsonSerializer.Serialize(new { workOrderId = id, workOrderNumber = number, request.LegalEntityId }), tenant, DateTimeOffset.UtcNow, correlationId, id, "service"));
        return await ReadWorkOrderAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.work_order_not_found");
    }, ct);

    public Task<PagedResult<WorkOrderView>> ListWorkOrdersAsync(TenantId tenant, Guid? legalEntityId, string? state, string? contextName, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_order where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) and ($4::text is null or context=$4)", token, tenant.Value, Db(legalEntityId), Db(state), Db(contextName)), CultureInfo.InvariantCulture);
        var rows = new List<WorkOrderView>();
        await using var command = Command(context, "select id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,billing_owner,work_type,priority,state,planned_start,planned_end,technical_completed_at,accepted_at,closed_at,row_version from business_service.work_order where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) and ($4::text is null or context=$4) order by updated_at desc,id desc limit $5 offset $6", tenant.Value, Db(legalEntityId), Db(state), Db(contextName), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadWorkOrder(reader));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<WorkOrderView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<WorkOrderView?> GetWorkOrderAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadWorkOrderAsync(context, tenant.Value, id, token), ct);

    public Task<WorkOrderView> TransitionWorkOrderAsync(TenantId tenant, Guid actor, Guid id, string action, TransitionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadWorkOrderAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.work_order_not_found");
        if (current.Version != request.Version) throw new ServiceException("service.concurrent_change");
        var next = action switch
        {
            "ready" when current.State == "Draft" => "Ready",
            "schedule" when current.State == "Ready" => "Scheduled",
            "start" when current.State is "Scheduled" or "Ready" => "InProgress",
            "waiting" when current.State is "InProgress" or "Scheduled" => "Waiting",
            "technical-complete" when current.State is "InProgress" or "Waiting" => "TechnicalCompleted",
            "accept" when current.State == "TechnicalCompleted" => "Accepted",
            "close" when current.State == "TechnicalCompleted" && current.Context == "InternalMaintenance" => "Closed",
            "close" when current.State == "Accepted" => "Closed",
            "cancel" when current.State is not "Closed" and not "Cancelled" => "Cancelled",
            "reopen" when current.State == "Closed" => "Ready",
            _ => throw new ServiceException("service.work_order_transition_invalid")
        };
        if (next == "TechnicalCompleted")
        {
            var missing = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_checklist_answer where tenant_id=$1 and work_order_id=$2 and required and answer_json is null", token, tenant.Value, id), CultureInfo.InvariantCulture);
            if (missing > 0) throw new ServiceException("service.checklist_required");
            var pending = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_part_requirement where tenant_id=$1 and work_order_id=$2 and state in ('Required','Reserved','PartiallyReserved','Issued','Unavailable','Ordered')", token, tenant.Value, id), CultureInfo.InvariantCulture);
            if (pending > 0) throw new ServiceException("service.part_consumption_conflict");
        }
        var technical = next == "TechnicalCompleted" ? ",technical_completed_at=now()" : string.Empty;
        var accepted = next == "Accepted" ? ",accepted_at=now()" : string.Empty;
        var closed = next == "Closed" ? ",closed_at=now()" : string.Empty;
        var cancelled = next == "Cancelled" ? ",cancelled_at=now()" : string.Empty;
        await ExecuteAsync(context, $"update business_service.work_order set state=$3,waiting_reason=case when $3='Waiting' then $4 else null end,row_version=row_version+1,updated_at=now(){technical}{accepted}{closed}{cancelled} where tenant_id=$1 and id=$2 and row_version=$5", token, tenant.Value, id, next, Db(request.Reason), current.Version);
        await AuditAsync(context, tenant, "WorkOrder", id, actor, $"work_order_{action}", new { current.State }, new { next, request.Reason }, token);
        return await ReadWorkOrderAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.work_order_not_found");
    }, ct);

    public Task<VisitView> CreateVisitAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateVisitRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.PlannedEnd <= request.PlannedStart) throw new ServiceException("service.visit_time_invalid");
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, workOrderId, token)) throw new ServiceException("service.work_order_not_found");
        var conflict = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_visit where tenant_id=$1 and work_order_id=$2 and state not in ('Cancelled','Completed','NoAccess') and planned_start < $4 and planned_end > $3", token, tenant.Value, workOrderId, request.PlannedStart, request.PlannedEnd), CultureInfo.InvariantCulture);
        if (conflict > 0) throw new ServiceException("service.visit_schedule_conflict");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.service_visit(tenant_id,id,work_order_id,planned_start,planned_end,location_type,site_instructions,created_by) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, workOrderId, request.PlannedStart, request.PlannedEnd, request.LocationType, Db(request.SiteInstructions), actor);
        await ExecuteAsync(context, "update business_service.work_order set state=case when state='Ready' then 'Scheduled' else state end,updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, workOrderId);
        return await ReadVisitAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.visit_not_found");
    }, ct);

    public Task<PagedResult<VisitView>> ListVisitsAsync(TenantId tenant, Guid? workOrderId, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_visit where tenant_id=$1 and ($2::uuid is null or work_order_id=$2) and ($3::text is null or state=$3)", token, tenant.Value, Db(workOrderId), Db(state)), CultureInfo.InvariantCulture);
        var rows = new List<VisitView>(); await using var command = Command(context, "select id,work_order_id,planned_start,planned_end,location_type,state,site_instructions,actual_start,actual_end,row_version from business_service.service_visit where tenant_id=$1 and ($2::uuid is null or work_order_id=$2) and ($3::text is null or state=$3) order by planned_start,id limit $4 offset $5", tenant.Value, Db(workOrderId), Db(state), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadVisit(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<VisitView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<VisitView> TransitionVisitAsync(TenantId tenant, Guid actor, Guid id, string action, TransitionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadVisitAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.visit_not_found"); if (current.Version != request.Version) throw new ServiceException("service.concurrent_change");
        var next = action switch { "dispatch" when current.State == "Planned" => "Dispatched", "travel" when current.State == "Dispatched" => "Traveling", "arrive" when current.State == "Traveling" => "Arrived", "start" when current.State is "Arrived" or "Dispatched" => "InProgress", "complete" when current.State is "InProgress" or "Arrived" => "Completed", "no-access" when current.State is "Traveling" or "Arrived" => "NoAccess", "cancel" when current.State is not "Completed" => "Cancelled", _ => throw new ServiceException("service.visit_transition_invalid") };
        var actual = next == "InProgress" ? ",actual_start=coalesce(actual_start,now())" : next is "Completed" or "NoAccess" ? ",actual_end=coalesce(actual_end,now())" : string.Empty;
        await ExecuteAsync(context, $"update business_service.service_visit set state=$3,row_version=row_version+1{actual} where tenant_id=$1 and id=$2 and row_version=$4", token, tenant.Value, id, next, current.Version);
        await AuditAsync(context, tenant, "ServiceVisit", id, actor, $"visit_{action}", new { current.State }, new { next }, token);
        return await ReadVisitAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.visit_not_found");
    }, ct);

    public Task<AssignmentView> AssignAsync(TenantId tenant, Guid actor, Guid visitId, CreateAssignmentRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (!await ExistsAsync(context, "business_service.service_visit", tenant.Value, visitId, token)) throw new ServiceException("service.visit_not_found");
        if (request.EmployeeId is null && request.UserId is null && string.IsNullOrWhiteSpace(request.DisplayName)) throw new ServiceException("service.resource_required");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.work_assignment(tenant_id,id,visit_id,employee_id,user_id,resource_type,display_name) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, visitId, Db(request.EmployeeId), Db(request.UserId), request.ResourceType, Db(request.DisplayName));
        await AuditAsync(context, tenant, "WorkAssignment", id, actor, "assignment_created", null, new { visitId, request.EmployeeId, request.UserId }, token); return await ReadAssignmentAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.assignment_not_found");
    }, ct);

    public Task<PagedResult<InstalledBaseView>> ListInstalledBaseAsync(TenantId tenant, Guid? customerPartyId, string? search, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.installed_base_item where tenant_id=$1 and ($2::uuid is null or customer_party_id=$2) and ($3::text is null or name ilike '%'||$3||'%' or serial_normalized ilike '%'||lower($3)||'%')", token, tenant.Value, Db(customerPartyId), Db(search)), CultureInfo.InvariantCulture);
        var rows = new List<InstalledBaseView>(); await using var command = Command(context, "select id,legal_entity_id,name,serial_number,model_number,product_id,customer_party_id,owner_party_id,parent_id,state,site_name,installed_date,row_version from business_service.installed_base_item where tenant_id=$1 and ($2::uuid is null or customer_party_id=$2) and ($3::text is null or name ilike '%'||$3||'%' or serial_normalized ilike '%'||lower($3)||'%') order by updated_at desc,id desc limit $4 offset $5", tenant.Value, Db(customerPartyId), Db(search), pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadInstalledBase(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<InstalledBaseView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<InstalledBaseView> CreateInstalledBaseAsync(TenantId tenant, Guid actor, CreateInstalledBaseRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ParentId == Guid.Empty) throw new ServiceException("service.parent_invalid");
        if (request.ParentId is { } parent && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, parent, token)) throw new ServiceException("service.installed_base_not_found");
        if (request.ParentId is { } parentId && await HasDescendantAsync(context, tenant.Value, parentId, token)) throw new ServiceException("service.component_cycle");
        var id = Guid.CreateVersion7(); var serial = string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim(); var normalized = serial?.ToLowerInvariant();
        await ExecuteAsync(context, "insert into business_service.installed_base_item(tenant_id,id,legal_entity_id,name,serial_number,serial_normalized,model_number,product_id,customer_party_id,owner_party_id,parent_id,site_name,installed_date,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, id, request.LegalEntityId, request.Name.Trim(), Db(serial), Db(normalized), Db(request.ModelNumber), Db(request.ProductId), Db(request.CustomerPartyId), Db(request.OwnerPartyId), Db(request.ParentId), Db(request.SiteName), Db(request.InstalledDate), actor);
        await AuditAsync(context, tenant, "InstalledBase", id, actor, "installed_base_created", null, new { request.Name, request.SerialNumber }, token); return await ReadInstalledBaseAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.installed_base_not_found");
    }, ct);

    public Task<TemplateView> CreateTemplateAsync(TenantId tenant, Guid actor, CreateTemplateRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var templateId = Guid.CreateVersion7(); var versionId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.service_work_template(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5) on conflict(tenant_id,legal_entity_id,code) do update set name=excluded.name returning id", token, tenant.Value, templateId, request.LegalEntityId, request.Code.Trim(), request.Name.Trim());
        var actualId = await ScalarAsync<Guid>(context, "select id from business_service.service_work_template where tenant_id=$1 and legal_entity_id=$2 and code=$3", token, tenant.Value, request.LegalEntityId, request.Code.Trim());
        await ExecuteAsync(context, "insert into business_service.service_work_template_version(tenant_id,id,template_id,version_no,effective_from,expected_duration_hours,definition) values($1,$2,$3,$4,current_date,$5,$6::jsonb) on conflict(tenant_id,template_id,version_no) do update set definition=excluded.definition,expected_duration_hours=excluded.expected_duration_hours", token, tenant.Value, versionId, actualId, request.Version, Db(request.ExpectedDurationHours), request.DefinitionJson); return await ReadTemplateAsync(context, tenant.Value, actualId, request.Version, token) ?? throw new ServiceException("service.template_missing");
    }, ct);

    public Task<PagedResult<MaintenancePlanView>> ListPlansAsync(TenantId tenant, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.maintenance_plan where tenant_id=$1", token, tenant.Value), CultureInfo.InvariantCulture); var rows = new List<MaintenancePlanView>(); await using var command = Command(context, "select id,legal_entity_id,name,subject_type,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code,state from business_service.maintenance_plan where tenant_id=$1 order by name,id limit $2 offset $3", tenant.Value, pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadPlan(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<MaintenancePlanView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<MaintenancePlanView> CreatePlanAsync(TenantId tenant, Guid actor, CreateMaintenancePlanRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.TriggerType == "CalendarFixed" && request.StartDate is null) throw new ServiceException("service.maintenance_plan_invalid"); if (request.TriggerType == "Meter" && (request.MeterThreshold is null || string.IsNullOrWhiteSpace(request.MeterCode))) throw new ServiceException("service.maintenance_plan_invalid");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.maintenance_plan(tenant_id,id,legal_entity_id,name,subject_type,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, id, request.LegalEntityId, request.Name.Trim(), request.SubjectType, request.SubjectId, request.TriggerType, Db(request.StartDate), Db(request.IntervalDays), Db(request.MeterThreshold), Db(request.MeterCode), actor); return await ReadPlanAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.maintenance_plan_not_found");
    }, ct);

    public Task<MaintenanceDueView> CreateDueAsync(TenantId tenant, Guid actor, Guid planId, DateOnly dueDate, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var plan = await ReadPlanAsync(context, tenant.Value, planId, token) ?? throw new ServiceException("service.maintenance_plan_not_found"); var key = $"{plan.Id:D}:{dueDate:yyyyMMdd}"; var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.maintenance_due(tenant_id,id,plan_id,subject_id,due_date,state,generated_key,reason) values($1,$2,$3,$4,$5,'Due',$6,'scheduled') on conflict(tenant_id,generated_key) do nothing", token, tenant.Value, id, planId, plan.SubjectId, dueDate, key); var result = await ReadDueByKeyAsync(context, tenant.Value, key, token); return result ?? throw new ServiceException("service.maintenance_due_not_found");
    }, ct);

    public Task<PagedResult<MaintenanceDueView>> ListDueAsync(TenantId tenant, string? state, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.maintenance_due where tenant_id=$1 and ($2::text is null or state=$2) and ($3::date is null or due_date >= $3) and ($4::date is null or due_date <= $4)", token, tenant.Value, Db(state), Db(from), Db(to)), CultureInfo.InvariantCulture);
        var rows = new List<MaintenanceDueView>(); await using var command = Command(context, "select id,plan_id,subject_id,due_date,state,work_order_id,reason from business_service.maintenance_due where tenant_id=$1 and ($2::text is null or state=$2) and ($3::date is null or due_date >= $3) and ($4::date is null or due_date <= $4) order by due_date,id limit $5 offset $6", tenant.Value, Db(state), Db(from), Db(to), pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetString(6))); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<MaintenanceDueView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<MaintenanceConversionView> ConvertDueAsync(TenantId tenant, Guid actor, Guid dueId, ConvertDueRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var due = await ScalarAsync<Guid?>(context, "select id from business_service.maintenance_due where tenant_id=$1 and id=$2 and state in ('Due','Forecast','Postponed')", token, tenant.Value, dueId) ?? throw new ServiceException("service.maintenance_due_not_found");
        ValidateContext(request.Context, request.CustomerPartyId, null); if (request.BillingOwner == "Projects" && request.ProjectId is null) throw new ServiceException("service.project_billing_owner_required");
        var orderId = Guid.CreateVersion7(); var number = Number("WO", orderId); await ExecuteAsync(context, "insert into business_service.work_order(tenant_id,id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,project_id,billing_owner,work_type,priority,state,created_by) select $1,$2,$3,$4,$5,$6,$7,$8,subject_id,$9,$10,$11,$12,'Draft',$13 from business_service.maintenance_due where tenant_id=$1 and id=$14", token, tenant.Value, orderId, request.LegalEntityId, number, request.Context, request.Title.Trim(), Db(request.Description), Db(request.CustomerPartyId), Db(request.ProjectId), request.BillingOwner, request.WorkType, request.Priority, actor, dueId);
        await ExecuteAsync(context, "update business_service.maintenance_due set state='Converted',work_order_id=$3 where tenant_id=$1 and id=$2", token, tenant.Value, dueId, orderId);
        var work = await ReadWorkOrderAsync(context, tenant.Value, orderId, token) ?? throw new ServiceException("service.work_order_not_found"); var updated = await ReadDueAsync(context, tenant.Value, dueId, token) ?? throw new ServiceException("service.maintenance_due_not_found"); return new MaintenanceConversionView(updated, work);
    }, ct);

    public Task<SlaProfileView> CreateSlaProfileAsync(TenantId tenant, Guid actor, CreateSlaProfileRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ResponseMinutes <= 0 || request.ResolutionMinutes <= 0 || request.ArrivalMinutes is <= 0) throw new ServiceException("service.sla_profile_invalid"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.sla_profile(tenant_id,id,legal_entity_id,code,response_minutes,arrival_minutes,resolution_minutes) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.ResponseMinutes, Db(request.ArrivalMinutes), request.ResolutionMinutes); return await ReadSlaProfileAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.sla_profile_not_found");
    }, ct);

    public Task<SlaClockView> StartSlaClockAsync(TenantId tenant, Guid actor, StartSlaClockRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var profile = await ReadSlaProfileAsync(context, tenant.Value, request.ProfileId, token) ?? throw new ServiceException("service.sla_profile_not_found"); var minutes = request.ClockType switch { "Response" => profile.ResponseMinutes, "Arrival" when profile.ArrivalMinutes is not null => profile.ArrivalMinutes.Value, "Resolution" => profile.ResolutionMinutes, _ => throw new ServiceException("service.sla_clock_invalid") }; var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.sla_clock(tenant_id,id,work_order_id,clock_type,started_at,due_at) values($1,$2,$3,$4,$5,$5 + make_interval(mins => $6)) on conflict(tenant_id,work_order_id,clock_type) do update set started_at=excluded.started_at,due_at=excluded.due_at,state='Running'", token, tenant.Value, id, request.WorkOrderId, request.ClockType, request.StartedAt, minutes); return await ReadSlaClockAsync(context, tenant.Value, request.WorkOrderId, request.ClockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
    }, ct);

    public Task<MeterReadingView> RecordMeterAsync(TenantId tenant, Guid actor, RecordMeterReadingRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var definitionId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.meter_definition(tenant_id,id,subject_id,meter_code,uom) values($1,$2,$3,$4,$5) on conflict(tenant_id,subject_id,meter_code) do update set uom=excluded.uom", token, tenant.Value, definitionId, request.SubjectId, request.MeterCode.Trim(), request.Uom); var actualDefinition = await ScalarAsync<Guid>(context, "select id from business_service.meter_definition where tenant_id=$1 and subject_id=$2 and meter_code=$3", token, tenant.Value, request.SubjectId, request.MeterCode.Trim()); var prior = await ScalarAsync<decimal?>(context, "select value from business_service.meter_reading where tenant_id=$1 and subject_id=$2 and meter_code=$3 order by reading_at desc limit 1", token, tenant.Value, request.SubjectId, request.MeterCode.Trim()); if (prior is { } old && request.Value < old && !request.IsReset) throw new ServiceException("service.meter_decreased_unexpectedly"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.meter_reading(tenant_id,id,subject_id,meter_definition_id,meter_code,value,uom,reading_at,source,is_reset) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, request.SubjectId, actualDefinition, request.MeterCode.Trim(), request.Value, request.Uom, request.ReadingAt, request.Source, request.IsReset); return await ReadMeterAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.meter_reading_invalid");
    }, ct);

    public Task<ServiceContractView> CreateContractAsync(TenantId tenant, Guid actor, CreateContractRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.service_contract(tenant_id,id,legal_entity_id,contract_number,contract_holder_party_id,bill_to_party_id,contract_type,start_date,end_date,currency_code,value,state,scope) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,'Draft',$13::jsonb)", token, tenant.Value, id, request.LegalEntityId, Number("CTR", id), request.ContractHolderPartyId, request.BillToPartyId, request.ContractType, request.StartDate, request.EndDate, request.CurrencyCode, Db(request.Value), request.ScopeJson ?? "{}"); return await ReadContractAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.contract_not_found");
    }, ct);

    public Task<ServiceContractView> ActivateContractAsync(TenantId tenant, Guid actor, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.service_contract set state='Active' where tenant_id=$1 and id=$2 and state in ('Draft','Suspended')", token, tenant.Value, id); return await ReadContractAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.contract_not_found"); }, ct);

    public Task<EntitlementView> ResolveEntitlementAsync(TenantId tenant, Guid actor, EntitlementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        Guid? contract = null; if (request.CustomerPartyId is { } customer) contract = await ScalarAsync<Guid?>(context, "select id from business_service.service_contract where tenant_id=$1 and contract_holder_party_id=$2 and state='Active' and $3 between start_date and end_date order by end_date desc limit 1", token, tenant.Value, customer, request.AsOfDate);
        Guid? warranty = request.InstalledBaseId is { } subject ? await ScalarAsync<Guid?>(context, "select id from business_service.warranty where tenant_id=$1 and subject_id=$2 and state='Active' and $3 between start_date and end_date order by end_date desc limit 1", token, tenant.Value, subject, request.AsOfDate) : null;
        var lines = new[] { new CoverageResult("Labor", contract is not null ? "ServiceContract" : warranty is not null ? "CustomerWarranty" : "Paid", contract is not null || warranty is not null, 0m, contract is not null || warranty is not null ? 0m : 0m, contract is not null ? "Covered by active service contract" : warranty is not null ? "Covered by active warranty" : "No active coverage") };
        return new EntitlementView(lines, contract is null ? "NoContract" : "Active", contract, warranty);
    }, ct);

    public Task<ServiceChargeView> CreateChargeAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateChargeRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, workOrderId, token)) throw new ServiceException("service.work_order_not_found"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.service_charge(tenant_id,id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,source_type,source_id,state) values($1,$2,$3,$4,$5,$6,0,$7,$8,$9,$10,'Reviewed') on conflict(tenant_id,work_order_id,category,source_type,source_id) do update set customer_amount=excluded.customer_amount", token, tenant.Value, id, workOrderId, request.Category, request.Description, request.CostAmount, request.CustomerAmount, request.CurrencyCode, Db(request.SourceType), Db(request.SourceId)); return await ReadChargeAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.charge_not_found");
    }, ct);

    public Task<PagedResult<ServiceChargeView>> ListChargesAsync(TenantId tenant, Guid workOrderId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_charge where tenant_id=$1 and work_order_id=$2", token, tenant.Value, workOrderId), CultureInfo.InvariantCulture); var rows = new List<ServiceChargeView>(); await using var command = Command(context, "select id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,state,invoice_id from business_service.service_charge where tenant_id=$1 and work_order_id=$2 order by id limit $3 offset $4", tenant.Value, workOrderId, pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadCharge(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<ServiceChargeView>(rows, page, pageSize, hasNext, total); }, ct);

    public Task<PartRequirementView> AddPartAsync(TenantId tenant, Guid actor, Guid workOrderId, CreatePartRequirementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { if (request.Quantity <= 0) throw new ServiceException("service.part_quantity_invalid"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.work_part_requirement(tenant_id,id,work_order_id,product_id,quantity,uom_code,description) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, workOrderId, request.ProductId, request.Quantity, request.UomCode, Db(request.Description)); return await ReadPartAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.part_requirement_not_found"); }, ct);

    public Task<LaborView> RecordLaborAsync(TenantId tenant, Guid actor, RecordLaborRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { if (request.EndedAt <= request.StartedAt) throw new ServiceException("service.labor_time_invalid"); var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"labor:{request.VisitId:D}:{request.StartedAt:O}" : request.IdempotencyKey.Trim(); var prior = await ScalarAsync<Guid?>(context, "select id from business_service.service_labor_entry where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, key); if (prior is { } existing) return await ReadLaborAsync(context, tenant.Value, existing, token) ?? throw new ServiceException("service.labor_not_found"); var id = Guid.CreateVersion7(); var hours = (decimal)(request.EndedAt - request.StartedAt).TotalHours; var amount = request.CostRate is null ? null : request.CostRate * hours; await ExecuteAsync(context, "insert into business_service.service_labor_entry(tenant_id,id,visit_id,employee_id,started_at,ended_at,time_category,cost_rate,currency_code,cost_amount,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, request.VisitId, Db(request.EmployeeId), request.StartedAt, request.EndedAt, request.TimeCategory, Db(request.CostRate), request.CurrencyCode, Db(amount), key); return await ReadLaborAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.labor_not_found"); }, ct);

    public Task<OfflineCommandView> SubmitOfflineAsync(TenantId tenant, Guid actor, Guid correlationId, OfflineCommandRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var prior = await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token); if (prior is not null) return prior;
        if (request.WorkOrderId is { } work && request.ExpectedVersion is { } version) { var current = await ScalarAsync<long?>(context, "select row_version from business_service.work_order where tenant_id=$1 and id=$2", token, tenant.Value, work); if (current is null) throw new ServiceException("service.work_order_not_found"); if (current != version) { var conflictId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.offline_command(tenant_id,id,client_command_id,command_type,work_order_id,visit_id,expected_version,payload,state,conflict_code) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,'Conflict','service.concurrent_change')", token, tenant.Value, conflictId, request.ClientCommandId, request.CommandType, Db(request.WorkOrderId), Db(request.VisitId), Db(request.ExpectedVersion), request.PayloadJson); return await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token) ?? throw new ServiceException("service.idempotency_conflict"); } }
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.offline_command(tenant_id,id,client_command_id,command_type,work_order_id,visit_id,expected_version,payload,state,processed_at,result) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,'Processed',now(),$9::jsonb)", token, tenant.Value, id, request.ClientCommandId, request.CommandType, Db(request.WorkOrderId), Db(request.VisitId), Db(request.ExpectedVersion), request.PayloadJson, "{\"accepted\":true}"); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ServiceOfflineCommandProcessedV1", JsonSerializer.Serialize(new { commandId = request.ClientCommandId, request.CommandType }), tenant, DateTimeOffset.UtcNow, correlationId, id, "service")); return await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token) ?? throw new ServiceException("service.offline_command_not_found");
    }, ct);

    public Task<MobileDayView> GetMobileDayAsync(TenantId tenant, Guid? userId, Guid? employeeId, DateOnly date, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var visits = new List<VisitView>(); await using var command = Command(context, "select v.id,v.work_order_id,v.planned_start,v.planned_end,v.location_type,v.state,v.site_instructions,v.actual_start,v.actual_end,v.row_version from business_service.service_visit v join business_service.work_assignment a on a.tenant_id=v.tenant_id and a.visit_id=v.id and a.state='Assigned' where v.tenant_id=$1 and v.planned_start::date=$2 and ($3::uuid is null or a.user_id=$3) and ($4::uuid is null or a.employee_id=$4) order by v.planned_start,v.id limit 200", tenant.Value, date, Db(userId), Db(employeeId)); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) visits.Add(ReadVisit(reader)); var orders = new List<WorkOrderView>(); foreach (var visit in visits) { var row = await ReadWorkOrderAsync(context, tenant.Value, visit.WorkOrderId, token); if (row is not null) orders.Add(row); } return new MobileDayView(visits, orders, date); }, ct);

    public Task<PagedResult<TemplateView>> ListTemplatesAsync(TenantId tenant, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_work_template where tenant_id=$1", token, tenant.Value), CultureInfo.InvariantCulture);
        var rows = new List<TemplateView>();
        await using var command = Command(context, "select t.id,t.legal_entity_id,t.code,t.name,v.version_no,v.expected_duration_hours,v.definition::text,t.state from business_service.service_work_template t join lateral (select version_no,expected_duration_hours,definition from business_service.service_work_template_version v where v.tenant_id=t.tenant_id and v.template_id=t.id order by version_no desc limit 1) v on true where t.tenant_id=$1 order by t.code,t.id limit $2 offset $3", tenant.Value, pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadTemplate(reader));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<TemplateView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<ChecklistAnswerView> SaveChecklistAnswerAsync(TenantId tenant, Guid actor, ChecklistAnswerRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.QuestionKey) || request.QuestionType is not ("Text" or "Boolean" or "Number" or "Choice" or "Measurement" or "Photo")) throw new ServiceException("service.checklist_invalid");
        await ExecuteAsync(context, "insert into business_service.work_checklist_answer(tenant_id,id,work_order_id,question_key,question_type,answer_json,required,answered_at) values($1,coalesce((select id from business_service.work_checklist_answer where tenant_id=$1 and work_order_id=$2 and question_key=$3),$4),$2,$3,$5,$6::jsonb,$7,case when $6::jsonb is null then null else now() end) on conflict(tenant_id,work_order_id,question_key) do update set question_type=excluded.question_type,answer_json=excluded.answer_json,required=excluded.required,answered_at=excluded.answered_at", token, tenant.Value, request.WorkOrderId, request.QuestionKey.Trim(), Guid.CreateVersion7(), request.QuestionType, Db(request.AnswerJson), request.Required);
        return await ReadChecklistAsync(context, tenant.Value, request.WorkOrderId, request.QuestionKey.Trim(), token) ?? throw new ServiceException("service.checklist_not_found");
    }, ct);

    public Task<DiagnosisView> RecordDiagnosisAsync(TenantId tenant, Guid actor, DiagnosisRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, request.WorkOrderId, token)) throw new ServiceException("service.work_order_not_found");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.work_diagnosis(tenant_id,id,work_order_id,reported_symptom,diagnosis,failure_mode_code,root_cause_code,resolution_code,notes,recorded_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, request.WorkOrderId, Db(request.ReportedSymptom), Db(request.Diagnosis), Db(request.FailureModeCode), Db(request.RootCauseCode), Db(request.ResolutionCode), Db(request.Notes), actor);
        return await ReadDiagnosisAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.diagnosis_not_found");
    }, ct);

    public Task<ComponentEventView> RecordComponentEventAsync(TenantId tenant, Guid actor, ComponentEventRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.EventType is not ("Installed" or "Removed" or "Replaced") || request.ItemId == request.ComponentItemId) throw new ServiceException("service.component_event_invalid");
        if (!await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, request.ItemId, token) || !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, request.ComponentItemId, token)) throw new ServiceException("service.installed_base_not_found");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.installed_base_component_event(tenant_id,id,item_id,component_item_id,event_type,occurred_at,replaced_component_id,work_order_id,notes) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, request.ItemId, request.ComponentItemId, request.EventType, request.OccurredAt, Db(request.ReplacedComponentId), Db(request.WorkOrderId), Db(request.Notes));
        return await ReadComponentEventAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.component_event_not_found");
    }, ct);

    public Task<WarrantyView> CreateWarrantyAsync(TenantId tenant, Guid actor, CreateWarrantyRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.WarrantyType is not ("Customer" or "Supplier") || request.EndDate < request.StartDate) throw new ServiceException("service.warranty_invalid");
        if (request.WarrantyType == "Customer" && request.CustomerPartyId is null) throw new ServiceException("service.warranty_customer_required");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.warranty(tenant_id,id,legal_entity_id,warranty_type,provider_party_id,customer_party_id,subject_id,start_date,end_date,state,scope) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10::jsonb)", token, tenant.Value, id, request.LegalEntityId, request.WarrantyType, Db(request.ProviderPartyId), Db(request.CustomerPartyId), request.SubjectId, request.StartDate, request.EndDate, request.ScopeJson ?? "{}");
        return await ReadWarrantyAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.warranty_not_found");
    }, ct);

    public Task<PartRequirementView> ApplyPartActionAsync(TenantId tenant, Guid actor, Guid partId, string action, PartActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Quantity <= 0 || action is not ("reserve" or "issue" or "consume" or "return")) throw new ServiceException("service.part_action_invalid");
        var part = await ReadPartAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.part_requirement_not_found");
        var column = action switch { "reserve" => "reserved_quantity", "issue" => "issued_quantity", "consume" => "consumed_quantity", _ => "returned_quantity" };
        var state = action switch { "reserve" => "Reserved", "issue" => "Issued", "consume" => "Consumed", _ => "Returned" };
        try { ServiceRules.ApplyPartMovement(action, request.Quantity, part.Quantity, part.ReservedQuantity, part.IssuedQuantity, part.ConsumedQuantity, part.ReturnedQuantity); } catch (ArgumentOutOfRangeException) { throw new ServiceException("service.part_quantity_invalid"); } catch (InvalidOperationException) { throw new ServiceException(action is "consume" or "return" ? "service.part_quantity_exceeds_issue" : "service.part_quantity_exceeds_required"); }
        await ExecuteAsync(context, $"update business_service.work_part_requirement set {column}={column}+$3,state=$4,inventory_reference=coalesce($5,inventory_reference) where tenant_id=$1 and id=$2", token, tenant.Value, partId, request.Quantity, state, Db(request.InventoryReference));
        return await ReadPartAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.part_requirement_not_found");
    }, ct);

    public Task<DowntimeView> StartDowntimeAsync(TenantId tenant, Guid actor, DowntimeRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.DowntimeType is not ("Planned" or "Unplanned")) throw new ServiceException("service.downtime_invalid");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.asset_downtime(tenant_id,id,subject_id,work_order_id,downtime_type,started_at,reason) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, request.SubjectId, Db(request.WorkOrderId), request.DowntimeType, request.StartedAt, Db(request.Reason));
        return await ReadDowntimeAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.downtime_not_found");
    }, ct);

    public Task<DowntimeView> EndDowntimeAsync(TenantId tenant, Guid actor, Guid downtimeId, DateTimeOffset endedAt, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadDowntimeAsync(context, tenant.Value, downtimeId, token) ?? throw new ServiceException("service.downtime_not_found");
        if (endedAt <= current.StartedAt) throw new ServiceException("service.downtime_invalid");
        await ExecuteAsync(context, "update business_service.asset_downtime set ended_at=$3 where tenant_id=$1 and id=$2 and ended_at is null", token, tenant.Value, downtimeId, endedAt);
        return await ReadDowntimeAsync(context, tenant.Value, downtimeId, token) ?? throw new ServiceException("service.downtime_not_found");
    }, ct);

    public Task<ServiceReportView> SaveReportAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateReportRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, workOrderId, token)) throw new ServiceException("service.work_order_not_found");
        var report = await ScalarAsync<Guid?>(context, "select id from business_service.service_report where tenant_id=$1 and work_order_id=$2", token, tenant.Value, workOrderId) ?? Guid.CreateVersion7();
        var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_service.service_report_version where tenant_id=$1 and report_id=$2", token, tenant.Value, report), CultureInfo.InvariantCulture);
        await ExecuteAsync(context, "insert into business_service.service_report(tenant_id,id,work_order_id,current_version,state) values($1,$2,$3,$4,'Draft') on conflict(tenant_id,work_order_id) do update set current_version=excluded.current_version,state='Draft'", token, tenant.Value, report, workOrderId, version);
        await ExecuteAsync(context, "insert into business_service.service_report_version(tenant_id,id,report_id,version_no,content,created_by) values($1,$2,$3,$4,$5::jsonb,$6)", token, tenant.Value, Guid.CreateVersion7(), report, version, request.ContentJson, actor);
        return await ReadReportAsync(context, tenant.Value, report, version, token) ?? throw new ServiceException("service.report_not_found");
    }, ct);

    public Task<ServiceReportView> AcceptReportAsync(TenantId tenant, Guid actor, Guid reportId, int version, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await ExecuteAsync(context, "update business_service.service_report set state='Accepted' where tenant_id=$1 and id=$2", token, tenant.Value, reportId);
        await ExecuteAsync(context, "update business_service.service_report_version set accepted_by=$3,accepted_at=now() where tenant_id=$1 and report_id=$2 and version_no=$4", token, tenant.Value, reportId, actor, version);
        return await ReadReportAsync(context, tenant.Value, reportId, version, token) ?? throw new ServiceException("service.report_not_found");
    }, ct);

    public Task<ServiceKpiView> GetKpiAsync(TenantId tenant, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => new ServiceKpiView(
        Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_request where tenant_id=$1 and state not in ('Closed','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture),
        Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_order where tenant_id=$1 and state not in ('Closed','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture),
        Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_visit where tenant_id=$1 and planned_start::date=current_date and state not in ('Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture),
        Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(customer_amount),0) from business_service.service_charge where tenant_id=$1 and state not in ('Invoiced','WrittenOff','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture),
        Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(extract(epoch from (coalesce(ended_at,now())-started_at))/3600),0) from business_service.asset_downtime where tenant_id=$1", token, tenant.Value), CultureInfo.InvariantCulture)), ct);

    public Task<ServiceChargeView> RequestInvoiceAsync(TenantId tenant, Guid actor, Guid chargeId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var charge = await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
        if (charge.State is "Invoiced" or "WrittenOff" or "Cancelled") throw new ServiceException("service.charge_state_invalid");
        await ExecuteAsync(context, "update business_service.service_charge set state='Requested' where tenant_id=$1 and id=$2", token, tenant.Value, chargeId);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ServiceInvoiceRequestedV1", JsonSerializer.Serialize(new { chargeId, workOrderId = charge.WorkOrderId, amount = charge.CustomerAmount }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), chargeId, "service"));
        return await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
    }, ct);

    public Task<ServiceChargeView> AllocateInvoiceAsync(TenantId tenant, Guid actor, Guid chargeId, InvoiceAllocationRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.AllocatedAmount <= 0) throw new ServiceException("service.invoice_allocation_invalid");
        var charge = await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
        if (request.AllocatedAmount > charge.CustomerAmount) throw new ServiceException("service.invoice_allocation_exceeds_charge");
        await ExecuteAsync(context, "insert into business_service.service_invoice_allocation(tenant_id,id,charge_id,invoice_id,allocated_amount) values($1,$2,$3,$4,$5) on conflict(tenant_id,charge_id,invoice_id) do update set allocated_amount=excluded.allocated_amount", token, tenant.Value, Guid.CreateVersion7(), chargeId, request.InvoiceId, request.AllocatedAmount);
        await ExecuteAsync(context, "update business_service.service_charge set state='Invoiced',invoice_id=$3 where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, request.InvoiceId);
        return await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
    }, ct);

    private static void ValidateContext(string context, Guid? customer, Guid? installedBase) { try { ServiceRules.ValidateContext(context, customer, installedBase); } catch (ArgumentException ex) { throw new ServiceException(ex.ParamName == "customerPartyId" ? "service.customer_required" : ex.ParamName == "installedBaseId" ? "service.subject_invalid" : "service.context_invalid"); } }
    private static string Number(string prefix, Guid id) => $"{prefix}-{id.ToString("N", CultureInfo.InvariantCulture)[..12].ToUpperInvariant()}";
    private static (int Page, int Size) Page(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 200));
    private static object Db(object? value) => value ?? DBNull.Value;
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return command; }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<object?> ScalarAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); var value = await command.ExecuteScalarAsync(ct); return value is DBNull ? null : value; }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { var value = await ScalarAsync(context, sql, ct, values); if (value is null) return default!; return (T)value; }
    private static async Task<bool> ExistsAsync(ITransactionContext context, string table, Guid tenant, Guid id, CancellationToken ct) => Convert.ToBoolean(await ScalarAsync<bool>(context, $"select exists(select 1 from {table} where tenant_id=$1 and id=$2)", ct, tenant, id), CultureInfo.InvariantCulture);
    private static async Task<bool> HasDescendantAsync(ITransactionContext context, Guid tenant, Guid parentId, CancellationToken ct) => Convert.ToBoolean(await ScalarAsync<bool>(context, "with recursive tree as (select id,parent_id from business_service.installed_base_item where tenant_id=$1 and id=$2 union all select i.id,i.parent_id from business_service.installed_base_item i join tree t on i.parent_id=t.id where i.tenant_id=$1) select exists(select 1 from tree where parent_id=$2)", ct, tenant, parentId), CultureInfo.InvariantCulture);
    private static async Task AuditAsync(ITransactionContext context, TenantId tenant, string type, Guid id, Guid actor, string action, object? before, object? after, CancellationToken ct) => await ExecuteAsync(context, "insert into business_service.service_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,before_json,after_json) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb)", ct, tenant.Value, Guid.CreateVersion7(), type, id, action, actor, before is null ? DBNull.Value : JsonSerializer.Serialize(before), after is null ? DBNull.Value : JsonSerializer.Serialize(after));
    private static async Task<ServiceRequestView?> ReadRequestAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,request_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,state,priority,requested_at,row_version from business_service.service_request where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadRequest(r) : null; }
    private static ServiceRequestView ReadRequest(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetString(9), r.GetString(10), r.GetFieldValue<DateTimeOffset>(11), r.GetInt64(12));
    private static async Task<WorkOrderView?> ReadWorkOrderAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,billing_owner,work_type,priority,state,planned_start,planned_end,technical_completed_at,accepted_at,closed_at,row_version from business_service.work_order where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadWorkOrder(r) : null; }
    private static WorkOrderView ReadWorkOrder(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.IsDBNull(9) ? null : r.GetGuid(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetString(13), r.IsDBNull(14) ? null : r.GetFieldValue<DateTimeOffset>(14), r.IsDBNull(15) ? null : r.GetFieldValue<DateTimeOffset>(15), r.IsDBNull(16) ? null : r.GetFieldValue<DateTimeOffset>(16), r.IsDBNull(17) ? null : r.GetFieldValue<DateTimeOffset>(17), r.IsDBNull(18) ? null : r.GetFieldValue<DateTimeOffset>(18), r.GetInt64(19));
    private static async Task<VisitView?> ReadVisitAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,planned_start,planned_end,location_type,state,site_instructions,actual_start,actual_end,row_version from business_service.service_visit where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadVisit(r) : null; }
    private static VisitView ReadVisit(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateTimeOffset>(2), r.GetFieldValue<DateTimeOffset>(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8), r.GetInt64(9));
    private static async Task<AssignmentView?> ReadAssignmentAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,visit_id,employee_id,user_id,resource_type,display_name,state from business_service.work_assignment where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6)) : null; }
    private static InstalledBaseView ReadInstalledBase(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10), r.IsDBNull(11) ? null : r.GetFieldValue<DateOnly>(11), r.GetInt64(12));
    private static async Task<InstalledBaseView?> ReadInstalledBaseAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,name,serial_number,model_number,product_id,customer_party_id,owner_party_id,parent_id,state,site_name,installed_date,row_version from business_service.installed_base_item where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadInstalledBase(r) : null; }
    private static TemplateView ReadTemplate(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetString(6), r.GetString(7));
    private static async Task<ChecklistAnswerView?> ReadChecklistAsync(ITransactionContext c, Guid t, Guid workOrderId, string key, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,question_key,question_type,answer_json::text,required,answered_at from business_service.work_checklist_answer where tenant_id=$1 and work_order_id=$2 and question_key=$3", t, workOrderId, key); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetBoolean(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6)) : null; }
    private static async Task<DiagnosisView?> ReadDiagnosisAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,reported_symptom,diagnosis,failure_mode_code,root_cause_code,resolution_code,notes,recorded_by,recorded_at from business_service.work_diagnosis where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetGuid(8), r.GetFieldValue<DateTimeOffset>(9)) : null; }
    private static async Task<ComponentEventView?> ReadComponentEventAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,item_id,component_item_id,event_type,occurred_at,replaced_component_id,work_order_id,notes from business_service.installed_base_component_event where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetString(7)) : null; }
    private static async Task<TemplateView?> ReadTemplateAsync(ITransactionContext c, Guid t, Guid templateId, int version, CancellationToken ct) { await using var cmd = Command(c, "select t.id,t.legal_entity_id,t.code,t.name,v.version_no,v.expected_duration_hours,v.definition::text,t.state from business_service.service_work_template t join business_service.service_work_template_version v on v.tenant_id=t.tenant_id and v.template_id=t.id where t.tenant_id=$1 and t.id=$2 and v.version_no=$3", t, templateId, version); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetString(6), r.GetString(7)) : null; }
    private static MaintenancePlanView ReadPlan(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetInt32(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.IsDBNull(9) ? null : r.GetString(9), r.GetString(10));
    private static async Task<MaintenancePlanView?> ReadPlanAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,name,subject_type,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code,state from business_service.maintenance_plan where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadPlan(r) : null; }
    private static async Task<MaintenanceDueView?> ReadDueByKeyAsync(ITransactionContext c, Guid t, string key, CancellationToken ct) { await using var cmd = Command(c, "select id,plan_id,subject_id,due_date,state,work_order_id,reason from business_service.maintenance_due where tenant_id=$1 and generated_key=$2", t, key); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetString(6)) : null; }
    private static async Task<MaintenanceDueView?> ReadDueAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,plan_id,subject_id,due_date,state,work_order_id,reason from business_service.maintenance_due where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetString(6)) : null; }
    private static async Task<SlaProfileView?> ReadSlaProfileAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,code,response_minutes,arrival_minutes,resolution_minutes,state from business_service.sla_profile where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.GetInt32(5), r.GetString(6)) : null; }
    private static async Task<SlaClockView?> ReadSlaClockAsync(ITransactionContext c, Guid t, Guid workOrderId, string clockType, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,clock_type,started_at,due_at,state,paused_minutes from business_service.sla_clock where tenant_id=$1 and work_order_id=$2 and clock_type=$3", t, workOrderId, clockType); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetInt32(6)) : null; }
    private static async Task<MeterReadingView?> ReadMeterAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,subject_id,meter_code,value,uom,reading_at,source,is_reset from business_service.meter_reading where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.GetString(6), r.GetBoolean(7)) : null; }
    private static async Task<ServiceContractView?> ReadContractAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,contract_holder_party_id,bill_to_party_id,start_date,end_date,contract_type,currency_code,value,state,scope::text from business_service.service_contract where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateOnly>(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10)) : null; }
    private static async Task<ServiceChargeView?> ReadChargeAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,state,invoice_id from business_service.service_charge where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCharge(r) : null; }
    private static ServiceChargeView ReadCharge(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetString(7), r.GetString(8), r.IsDBNull(9) ? null : r.GetGuid(9));
    private static async Task<PartRequirementView?> ReadPartAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,product_id,quantity,uom_code,state,reserved_quantity,issued_quantity,consumed_quantity,returned_quantity from business_service.work_part_requirement where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9)) : null; }
    private static async Task<WarrantyView?> ReadWarrantyAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,warranty_type,provider_party_id,customer_party_id,subject_id,start_date,end_date,state,scope::text from business_service.warranty where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.GetFieldValue<DateOnly>(6), r.GetFieldValue<DateOnly>(7), r.GetString(8), r.GetString(9)) : null; }
    private static async Task<LaborView?> ReadLaborAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,visit_id,employee_id,started_at,ended_at,time_category,extract(epoch from (ended_at-started_at))/3600,cost_amount,currency_code,state from business_service.service_labor_entry where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetDecimal(6), r.IsDBNull(7) ? null : r.GetDecimal(7), r.GetString(8), r.GetString(9)) : null; }
    private static async Task<OfflineCommandView?> ReadOfflineAsync(ITransactionContext c, Guid t, Guid clientId, CancellationToken ct) { await using var cmd = Command(c, "select id,client_command_id,command_type,state,conflict_code,result::text from business_service.offline_command where tenant_id=$1 and client_command_id=$2", t, clientId); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)) : null; }
    private static async Task<DowntimeView?> ReadDowntimeAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,subject_id,work_order_id,downtime_type,started_at,ended_at,reason from business_service.asset_downtime where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetString(6)) : null; }
    private static async Task<ServiceReportView?> ReadReportAsync(ITransactionContext c, Guid t, Guid reportId, int version, CancellationToken ct) { await using var cmd = Command(c, "select r.id,r.work_order_id,v.version_no, r.state,v.content::text,v.created_at from business_service.service_report r join business_service.service_report_version v on v.tenant_id=r.tenant_id and v.report_id=r.id where r.tenant_id=$1 and r.id=$2 and v.version_no=$3", t, reportId, version); await using var row = await cmd.ExecuteReaderAsync(ct); return await row.ReadAsync(ct) ? new(row.GetGuid(0), row.GetGuid(1), row.GetInt32(2), row.GetString(3), row.GetString(4), row.GetFieldValue<DateTimeOffset>(5)) : null; }
}
