using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Npgsql;
using Modules.Service.Domain;
using Modules.Service.DTOs;
using Shared.Contracts.Inventory;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Projects;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Modules.Service.Services;

public sealed class ServiceService(
    ITransactionCoordinator coordinator,
    IInventoryCapabilities? inventory = null,
    IInvoicingSourceCapabilities? invoicing = null,
    IServiceInvoiceReadCapability? invoiceReader = null,
    IProjectSourceIntegrationCapabilities? projects = null,
    IPlatformFileReferenceCapability? fileReferences = null)
{
    private static readonly JsonSerializerOptions OfflineJsonOptions = new(JsonSerializerDefaults.Web);
    public Task<Guid?> GetWorkOrderLegalEntityAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.work_order where tenant_id=$1 and id=$2", token, tenant.Value, workOrderId), ct);
    public Task<Guid?> GetVisitLegalEntityAsync(TenantId tenant, Guid visitId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.service_visit v join business_service.work_order w on w.tenant_id=v.tenant_id and w.id=v.work_order_id where v.tenant_id=$1 and v.id=$2", token, tenant.Value, visitId), ct);
    public Task<Guid?> GetPartLegalEntityAsync(TenantId tenant, Guid partId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select coalesce(p.legal_entity_id,w.legal_entity_id) from business_service.work_part_requirement p join business_service.work_order w on w.tenant_id=p.tenant_id and w.id=p.work_order_id where p.tenant_id=$1 and p.id=$2", token, tenant.Value, partId), ct);
    public Task<Guid?> GetChargeLegalEntityAsync(TenantId tenant, Guid chargeId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.service_charge c join business_service.work_order w on w.tenant_id=c.tenant_id and w.id=c.work_order_id where c.tenant_id=$1 and c.id=$2", token, tenant.Value, chargeId), ct);
    public Task<Guid?> GetRequestLegalEntityAsync(TenantId tenant, Guid requestId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.service_request where tenant_id=$1 and id=$2", token, tenant.Value, requestId), ct);
    public Task<Guid?> GetAssignmentLegalEntityAsync(TenantId tenant, Guid assignmentId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.work_assignment a join business_service.service_visit v on v.tenant_id=a.tenant_id and v.id=a.visit_id join business_service.work_order w on w.tenant_id=v.tenant_id and w.id=v.work_order_id where a.tenant_id=$1 and a.id=$2", token, tenant.Value, assignmentId), ct);
    public Task<Guid?> GetInstalledBaseLegalEntityAsync(TenantId tenant, Guid itemId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.installed_base_item where tenant_id=$1 and id=$2", token, tenant.Value, itemId), ct);
    public Task<Guid?> GetChecklistLegalEntityAsync(TenantId tenant, Guid answerId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.work_checklist_answer a join business_service.work_order w on w.tenant_id=a.tenant_id and w.id=a.work_order_id where a.tenant_id=$1 and a.id=$2", token, tenant.Value, answerId), ct);
    public Task<Guid?> GetPlanLegalEntityAsync(TenantId tenant, Guid planId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.maintenance_plan where tenant_id=$1 and id=$2", token, tenant.Value, planId), ct);
    public Task<Guid?> GetDueLegalEntityAsync(TenantId tenant, Guid dueId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select p.legal_entity_id from business_service.maintenance_due d join business_service.maintenance_plan p on p.tenant_id=d.tenant_id and p.id=d.plan_id where d.tenant_id=$1 and d.id=$2", token, tenant.Value, dueId), ct);
    public Task<Guid?> GetRoundLegalEntityAsync(TenantId tenant, Guid roundId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.maintenance_round where tenant_id=$1 and id=$2", token, tenant.Value, roundId), ct);
    public Task<Guid?> GetRoundSubjectLegalEntityAsync(TenantId tenant, Guid subjectId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select r.legal_entity_id from business_service.maintenance_round_subject s join business_service.maintenance_round r on r.tenant_id=s.tenant_id and r.id=s.round_id where s.tenant_id=$1 and s.id=$2", token, tenant.Value, subjectId), ct);
    public Task<Guid?> GetContractLegalEntityAsync(TenantId tenant, Guid contractId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.service_contract where tenant_id=$1 and id=$2", token, tenant.Value, contractId), ct);
    public Task<Guid?> GetWarrantyLegalEntityAsync(TenantId tenant, Guid warrantyId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.warranty where tenant_id=$1 and id=$2", token, tenant.Value, warrantyId), ct);
    public Task<Guid?> GetSupplierRecoveryLegalEntityAsync(TenantId tenant, Guid recoveryId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.service_supplier_recovery r join business_service.work_order w on w.tenant_id=r.tenant_id and w.id=r.work_order_id where r.tenant_id=$1 and r.id=$2", token, tenant.Value, recoveryId), ct);
    public Task<Guid?> GetDowntimeLegalEntityAsync(TenantId tenant, Guid downtimeId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select coalesce(w.legal_entity_id,i.legal_entity_id) from business_service.asset_downtime d left join business_service.work_order w on w.tenant_id=d.tenant_id and w.id=d.work_order_id left join business_service.installed_base_item i on i.tenant_id=d.tenant_id and i.id=d.subject_id where d.tenant_id=$1 and d.id=$2", token, tenant.Value, downtimeId), ct);
    public Task<Guid?> GetReportLegalEntityAsync(TenantId tenant, Guid reportId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.service_report r join business_service.work_order w on w.tenant_id=r.tenant_id and w.id=r.work_order_id where r.tenant_id=$1 and r.id=$2", token, tenant.Value, reportId), ct);
    public Task<Guid?> GetServiceEntityLegalEntityAsync(TenantId tenant, string entityType, Guid entityId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.work_order where tenant_id=$1 and id=$2 and $3 in ('WorkOrder','work_order') union all select legal_entity_id from business_service.service_request where tenant_id=$1 and id=$2 and $3 in ('ServiceRequest','service_request') union all select w.legal_entity_id from business_service.service_visit v join business_service.work_order w on w.tenant_id=v.tenant_id and w.id=v.work_order_id where v.tenant_id=$1 and v.id=$2 and $3 in ('Visit','service_visit') union all select legal_entity_id from business_service.installed_base_item where tenant_id=$1 and id=$2 and $3 in ('InstalledBase','installed_base') limit 1", token, tenant.Value, entityId, entityType), ct);
    public Task<Guid?> GetFixedAssetHistoryLegalEntityAsync(TenantId tenant, Guid fixedAssetId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context, "select w.legal_entity_id from business_service.service_fixed_asset_history h join business_service.work_order w on w.tenant_id=h.tenant_id and w.id=h.work_order_id where h.tenant_id=$1 and h.fixed_asset_id=$2 order by h.occurred_at desc limit 1", token, tenant.Value, fixedAssetId), ct);
    public Task<bool> IsActorAssignedAsync(TenantId tenant, Guid actor, Guid? workOrderId, Guid? visitId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => IsActorAssignedAsync(context, tenant, actor, workOrderId, visitId, token), ct);
    private static async Task<bool> IsActorAssignedAsync(ITransactionContext context, TenantId tenant, Guid actor, Guid? workOrderId, Guid? visitId, CancellationToken ct) => Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_assignment a join business_service.service_visit v on v.tenant_id=a.tenant_id and v.id=a.visit_id where a.tenant_id=$1 and a.user_id=$2 and a.state='Assigned' and ($3::uuid is null or v.id=$3) and ($4::uuid is null or v.work_order_id=$4)", ct, tenant.Value, actor, Db(visitId), Db(workOrderId)), CultureInfo.InvariantCulture) > 0;
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
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 240) throw new ServiceException("service.work_order_title_invalid");
        if (request.BillingOwner == "Projects" && request.ProjectId is null) throw new ServiceException("service.project_billing_owner_required");
        if ((request.TemplateId is null) != (request.TemplateVersion is null) || request.TemplateVersion is <= 0) throw new ServiceException("service.template_snapshot_invalid");
        if (request.TemplateId is { } templateId && !await ExistsAsync(context, "business_service.service_work_template_version", tenant.Value, templateId, request.TemplateVersion, token)) throw new ServiceException("service.template_missing");
        if (request.ServiceRequestId is { } requestId && !await ExistsAsync(context, "business_service.service_request", tenant.Value, requestId, token)) throw new ServiceException("service.request_not_found");
        if (request.InstalledBaseId is { } installedBase && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, installedBase, token)) throw new ServiceException("service.installed_base_not_found");
        var id = Guid.CreateVersion7(); var number = Number("WO", id);
        await ExecuteAsync(context, "insert into business_service.work_order(tenant_id,id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,service_request_id,billing_owner,work_type,priority,template_id,template_version,state,planned_start,planned_end,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,'Draft',$18,$19,$20)", token, tenant.Value, id, request.LegalEntityId, number, request.Context, request.Title.Trim(), Db(request.Description), Db(request.CustomerPartyId), Db(request.InstalledBaseId), Db(request.FixedAssetId), Db(request.ProjectId), Db(request.ServiceRequestId), request.BillingOwner, request.WorkType, request.Priority, Db(request.TemplateId), Db(request.TemplateVersion), Db(request.PlannedStart), Db(request.PlannedEnd), actor);
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
        await using var command = Command(context, "select id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,billing_owner,work_type,priority,state,planned_start,planned_end,technical_completed_at,accepted_at,closed_at,row_version,template_id,template_version from business_service.work_order where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) and ($4::text is null or context=$4) order by updated_at desc,id desc limit $5 offset $6", tenant.Value, Db(legalEntityId), Db(state), Db(contextName), pageSize + 1, offset);
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
        if (next is "TechnicalCompleted" or "Accepted" or "Closed")
        {
            await ExecuteAsync(context, """
                insert into business_service.service_fixed_asset_history
                    (tenant_id,id,fixed_asset_id,work_order_id,event_type,cost_amount,currency_code,snapshot)
                select w.tenant_id,gen_random_uuid(),w.fixed_asset_id,w.id,$3,
                       coalesce((select sum(amount) from business_service.service_cost_fact f where f.tenant_id=w.tenant_id and f.work_order_id=w.id and f.state='Finalized'),0),
                       'EGP',jsonb_build_object('workOrderNumber',w.work_order_number,'state',w.state,'context',w.context)
                from business_service.work_order w
                where w.tenant_id=$1 and w.id=$2 and w.fixed_asset_id is not null
                on conflict (tenant_id,fixed_asset_id,work_order_id,event_type) do nothing
                """, token, tenant.Value, id, next);
        }
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

    public Task<ResourceRequirementView> AddResourceRequirementAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateResourceRequirementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Quantity <= 0 || request.RequiredTo is not null && request.RequiredFrom is not null && request.RequiredTo <= request.RequiredFrom) throw new ServiceException("service.resource_requirement_invalid");
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, workOrderId, token)) throw new ServiceException("service.work_order_not_found");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_resource_requirement(tenant_id,id,work_order_id,skill_code,quantity,required_from,required_to) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, workOrderId, Db(request.SkillCode), request.Quantity, Db(request.RequiredFrom), Db(request.RequiredTo));
        await AuditAsync(context, tenant, "ServiceResourceRequirement", id, actor, "resource_requirement_created", null, new { workOrderId, request.SkillCode, request.Quantity }, token);
        return await ReadResourceRequirementAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.resource_requirement_not_found");
    }, ct);

    public Task<IReadOnlyList<ResourceRequirementView>> ListResourceRequirementsAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var rows = new List<ResourceRequirementView>();
        await using var command = Command(context, "select id,work_order_id,skill_code,quantity,required_from,required_to,state from business_service.service_resource_requirement where tenant_id=$1 and work_order_id=$2 order by id", tenant.Value, workOrderId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(ReadResourceRequirement(reader));
        return (IReadOnlyList<ResourceRequirementView>)rows;
    }, ct);

    public Task<CrewView> CreateCrewAsync(TenantId tenant, Guid actor, CreateCrewRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ServiceException("service.crew_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_crew(tenant_id,id,legal_entity_id,code,name) values($1,$2,$3,$4,$5)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim());
        return await ReadCrewAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.crew_not_found");
    }, ct);

    public Task<IReadOnlyList<CrewView>> ListCrewsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var rows = new List<CrewView>();
        await using var command = Command(context, "select id,legal_entity_id,code,name,active from business_service.service_crew where tenant_id=$1 and legal_entity_id=$2 and active order by code", tenant.Value, legalEntityId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
        return (IReadOnlyList<CrewView>)rows;
    }, ct);

    public Task<SkillView> CreateSkillAsync(TenantId tenant, Guid actor, CreateSkillRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ServiceException("service.skill_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_skill(tenant_id,id,code,name) values($1,$2,$3,$4)", token, tenant.Value, id, request.Code.Trim(), request.Name.Trim());
        return await ReadSkillAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.skill_not_found");
    }, ct);

    public Task<IReadOnlyList<SkillView>> ListSkillsAsync(TenantId tenant, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var rows = new List<SkillView>();
        await using var command = Command(context, "select id,code,name,active from business_service.service_skill where tenant_id=$1 and active order by code", tenant.Value);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        return (IReadOnlyList<SkillView>)rows;
    }, ct);

    public Task<TerritoryView> CreateTerritoryAsync(TenantId tenant, Guid actor, CreateTerritoryRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ServiceException("service.territory_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_territory(tenant_id,id,code,name) values($1,$2,$3,$4)", token, tenant.Value, id, request.Code.Trim(), request.Name.Trim());
        return await ReadTerritoryAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.territory_not_found");
    }, ct);

    public Task<IReadOnlyList<TerritoryView>> ListTerritoriesAsync(TenantId tenant, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var rows = new List<TerritoryView>();
        await using var command = Command(context, "select id,code,name,active from business_service.service_territory where tenant_id=$1 and active order by code", tenant.Value);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        return (IReadOnlyList<TerritoryView>)rows;
    }, ct);

    public Task<ExternalReferenceView> CreateExternalReferenceAsync(TenantId tenant, Guid actor, CreateExternalReferenceRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.EntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.EntityType) || string.IsNullOrWhiteSpace(request.SourceModule) || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceId)) throw new ServiceException("service.external_reference_invalid");
        var ownerExists = Convert.ToInt32(await ScalarAsync(context, "select count(*) from (select id from business_service.work_order where tenant_id=$1 and id=$2 and $3 in ('WorkOrder','work_order') union all select id from business_service.service_request where tenant_id=$1 and id=$2 and $3 in ('ServiceRequest','service_request') union all select id from business_service.service_visit where tenant_id=$1 and id=$2 and $3 in ('Visit','service_visit') union all select id from business_service.installed_base_item where tenant_id=$1 and id=$2 and $3 in ('InstalledBase','installed_base')) owner", token, tenant.Value, request.EntityId, request.EntityType), CultureInfo.InvariantCulture);
        if (ownerExists == 0) throw new ServiceException("service.external_reference_owner_not_found");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_external_reference(tenant_id,id,entity_type,entity_id,source_module,source_type,source_id,snapshot) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb) on conflict(tenant_id,source_module,source_type,source_id) do update set snapshot=excluded.snapshot", token, tenant.Value, id, request.EntityType.Trim(), request.EntityId, request.SourceModule.Trim(), request.SourceType.Trim(), request.SourceId.Trim(), Db(request.SnapshotJson));
        var actual = await ScalarAsync<Guid>(context, "select id from business_service.service_external_reference where tenant_id=$1 and source_module=$2 and source_type=$3 and source_id=$4", token, tenant.Value, request.SourceModule.Trim(), request.SourceType.Trim(), request.SourceId.Trim());
        await AuditAsync(context, tenant, "ServiceExternalReference", actual, actor, "external_reference_upserted", null, new { request.EntityType, request.EntityId, request.SourceModule, request.SourceType, request.SourceId }, token);
        return await ReadExternalReferenceAsync(context, tenant.Value, actual, token) ?? throw new ServiceException("service.external_reference_not_found");
    }, ct);

    public Task<IReadOnlyList<ExternalReferenceView>> ListExternalReferencesAsync(TenantId tenant, Guid entityId, string entityType, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var rows = new List<ExternalReferenceView>();
        await using var command = Command(context, "select id,entity_id,entity_type,source_module,source_type,source_id,snapshot::text from business_service.service_external_reference where tenant_id=$1 and entity_id=$2 and entity_type=$3 order by created_at,id", tenant.Value, entityId, entityType);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        return (IReadOnlyList<ExternalReferenceView>)rows;
    }, ct);

    public Task<PagedResult<InstalledBaseView>> ListInstalledBaseAsync(TenantId tenant, Guid? legalEntityId, Guid? customerPartyId, string? search, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.installed_base_item where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::uuid is null or customer_party_id=$3) and ($4::text is null or name ilike '%'||$4||'%' or serial_normalized ilike '%'||lower($4)||'%')", token, tenant.Value, Db(legalEntityId), Db(customerPartyId), Db(search)), CultureInfo.InvariantCulture);
        var rows = new List<InstalledBaseView>(); await using var command = Command(context, "select id,legal_entity_id,name,serial_number,model_number,product_id,customer_party_id,owner_party_id,parent_id,state,site_name,installed_date,row_version from business_service.installed_base_item where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::uuid is null or customer_party_id=$3) and ($4::text is null or name ilike '%'||$4||'%' or serial_normalized ilike '%'||lower($4)||'%') order by updated_at desc,id desc limit $5 offset $6", tenant.Value, Db(legalEntityId), Db(customerPartyId), Db(search), pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadInstalledBase(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<InstalledBaseView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<InstalledBaseView> CreateInstalledBaseAsync(TenantId tenant, Guid actor, CreateInstalledBaseRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ParentId == Guid.Empty) throw new ServiceException("service.parent_invalid");
        if (request.ParentId is { } parent && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, parent, token)) throw new ServiceException("service.installed_base_not_found");
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

    public Task<PagedResult<MaintenancePlanView>> ListPlansAsync(TenantId tenant, Guid? legalEntityId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.maintenance_plan where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2)", token, tenant.Value, Db(legalEntityId)), CultureInfo.InvariantCulture); var rows = new List<MaintenancePlanView>(); await using var command = Command(context, "select id,legal_entity_id,name,subject_type,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code,state from business_service.maintenance_plan where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) order by name,id limit $3 offset $4", tenant.Value, Db(legalEntityId), pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadPlan(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<MaintenancePlanView>(rows, page, pageSize, hasNext, total);
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

    public Task<DueGenerationView> GenerateDueAsync(TenantId tenant, Guid actor, GenerateDueRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ToDate < request.FromDate || request.Limit is < 1 or > 5000) throw new ServiceException("service.maintenance_horizon_invalid");
        var plans = new List<(Guid Id, Guid SubjectId, string Trigger, DateOnly? Start, int? Interval, decimal? MeterThreshold, string? MeterCode)>();
        await using (var command = Command(context, "select id,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code from business_service.maintenance_plan where tenant_id=$1 and state='Active' order by id", tenant.Value))
        await using (var reader = await command.ExecuteReaderAsync(token)) while (await reader.ReadAsync(token)) plans.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        var created = 0;
        foreach (var plan in plans)
        {
            if (created >= request.Limit) break;
            var dates = new List<DateOnly>();
            if (plan.Trigger == "Meter")
            {
                var value = await ScalarAsync<decimal?>(context, "select value from business_service.meter_reading where tenant_id=$1 and subject_id=$2 and meter_code=$3 and reading_at::date <= $4 order by reading_at desc limit 1", token, tenant.Value, plan.SubjectId, plan.MeterCode!, request.ToDate);
                if (value >= plan.MeterThreshold) dates.Add(request.FromDate);
            }
            else if (plan.Start is { } start && plan.Interval is { } interval)
            {
                var due = start; while (due < request.FromDate) due = due.AddDays(interval);
                while (due <= request.ToDate && dates.Count + created < request.Limit) { dates.Add(due); due = due.AddDays(interval); }
            }
            foreach (var dueDate in dates)
            {
                var key = $"{plan.Id:D}:{dueDate:yyyyMMdd}"; var inserted = await ExecuteAsync(context, "insert into business_service.maintenance_due(tenant_id,id,plan_id,subject_id,due_date,state,generated_key,reason) values($1,$2,$3,$4,$5,'Due',$6,'engine') on conflict(tenant_id,generated_key) do nothing", token, tenant.Value, Guid.CreateVersion7(), plan.Id, plan.SubjectId, dueDate, key); if (inserted > 0) created++;
                if (created >= request.Limit) break;
            }
        }
        return new DueGenerationView(plans.Count, created);
    }, ct);

    public Task<MaintenanceDueView> UpdateDueAsync(TenantId tenant, Guid actor, Guid dueId, MaintenanceDueUpdateRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.State is not ("Due" or "Postponed" or "Skipped")) throw new ServiceException("service.maintenance_due_state_invalid");
        var current = await ReadDueAsync(context, tenant.Value, dueId, token) ?? throw new ServiceException("service.maintenance_due_not_found");
        if (current.Version != request.ExpectedVersion) throw new ServiceException("service.concurrent_change");
        await ExecuteAsync(context, "update business_service.maintenance_due set state=$3,reason=$4,row_version=row_version+1 where tenant_id=$1 and id=$2 and row_version=$5", token, tenant.Value, dueId, request.State, Db(request.Reason), request.ExpectedVersion);
        await AuditAsync(context, tenant, "MaintenanceDue", dueId, actor, $"due_{request.State.ToLowerInvariant()}", new { current.State }, new { request.State, request.Reason }, token);
        return await ReadDueAsync(context, tenant.Value, dueId, token) ?? throw new ServiceException("service.maintenance_due_not_found");
    }, ct);

    public Task<PagedResult<MaintenanceDueView>> ListDueAsync(TenantId tenant, Guid? legalEntityId, string? state, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.maintenance_due d join business_service.maintenance_plan p on p.tenant_id=d.tenant_id and p.id=d.plan_id where d.tenant_id=$1 and ($2::uuid is null or p.legal_entity_id=$2) and ($3::text is null or d.state=$3) and ($4::date is null or d.due_date >= $4) and ($5::date is null or d.due_date <= $5)", token, tenant.Value, Db(legalEntityId), Db(state), Db(from), Db(to)), CultureInfo.InvariantCulture);
        var rows = new List<MaintenanceDueView>(); await using var command = Command(context, "select d.id,d.plan_id,d.subject_id,d.due_date,d.state,d.work_order_id,d.reason,d.row_version from business_service.maintenance_due d join business_service.maintenance_plan p on p.tenant_id=d.tenant_id and p.id=d.plan_id where d.tenant_id=$1 and ($2::uuid is null or p.legal_entity_id=$2) and ($3::text is null or d.state=$3) and ($4::date is null or d.due_date >= $4) and ($5::date is null or d.due_date <= $5) order by d.due_date,d.id limit $6 offset $7", tenant.Value, Db(legalEntityId), Db(state), Db(from), Db(to), pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt64(7))); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<MaintenanceDueView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<MaintenanceConversionView> ConvertDueAsync(TenantId tenant, Guid actor, Guid dueId, ConvertDueRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var due = await ScalarAsync<Guid?>(context, "select d.id from business_service.maintenance_due d join business_service.maintenance_plan p on p.tenant_id=d.tenant_id and p.id=d.plan_id where d.tenant_id=$1 and d.id=$2 and d.state in ('Due','Forecast','Postponed') and p.legal_entity_id=$3", token, tenant.Value, dueId, request.LegalEntityId) ?? throw new ServiceException("service.maintenance_due_not_found");
        var subject = await ScalarAsync<Guid?>(context, "select subject_id from business_service.maintenance_due where tenant_id=$1 and id=$2", token, tenant.Value, dueId); ValidateContext(request.Context, request.CustomerPartyId, subject); if (request.BillingOwner == "Projects" && request.ProjectId is null) throw new ServiceException("service.project_billing_owner_required");
        var orderId = Guid.CreateVersion7(); var number = Number("WO", orderId); await ExecuteAsync(context, "insert into business_service.work_order(tenant_id,id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,project_id,maintenance_due_id,billing_owner,work_type,priority,state,created_by) select $1,$2,$3,$4,$5,$6,$7,$8,subject_id,$9,$10,$11,$12,$13,'Draft',$14 from business_service.maintenance_due where tenant_id=$1 and id=$15", token, tenant.Value, orderId, request.LegalEntityId, number, request.Context, request.Title.Trim(), Db(request.Description), Db(request.CustomerPartyId), Db(request.ProjectId), dueId, request.BillingOwner, request.WorkType, request.Priority, actor, dueId);
        await ExecuteAsync(context, "update business_service.maintenance_due set state='Converted',work_order_id=$3,row_version=row_version+1 where tenant_id=$1 and id=$2 and state in ('Due','Forecast','Postponed')", token, tenant.Value, dueId, orderId);
        await ExecuteAsync(context, """
            insert into business_service.maintenance_due(tenant_id,id,plan_id,subject_id,due_date,state,generated_key,reason)
            select d.tenant_id,gen_random_uuid(),d.plan_id,d.subject_id,d.due_date+p.interval_days,'Forecast',d.plan_id::text||':'||(d.due_date+p.interval_days)::text,'next_after_conversion'
            from business_service.maintenance_due d
            join business_service.maintenance_plan p on p.tenant_id=d.tenant_id and p.id=d.plan_id
            where d.tenant_id=$1 and d.id=$2 and p.trigger_type='CalendarFixed' and p.interval_days is not null
            on conflict (tenant_id,generated_key) do nothing
            """, token, tenant.Value, dueId);
        var work = await ReadWorkOrderAsync(context, tenant.Value, orderId, token) ?? throw new ServiceException("service.work_order_not_found"); var updated = await ReadDueAsync(context, tenant.Value, dueId, token) ?? throw new ServiceException("service.maintenance_due_not_found"); return new MaintenanceConversionView(updated, work);
    }, ct);

    public Task<SlaProfileView> CreateSlaProfileAsync(TenantId tenant, Guid actor, CreateSlaProfileRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.ResponseMinutes <= 0 || request.ResolutionMinutes <= 0 || request.ArrivalMinutes is <= 0 || request.WorkingWeekdays is < 1 or > 127 || TimeOnly.TryParse(request.WorkingStart, out var start) == false || TimeOnly.TryParse(request.WorkingEnd, out var end) == false || end <= start) throw new ServiceException("service.sla_profile_invalid"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.sla_profile(tenant_id,id,legal_entity_id,code,response_minutes,arrival_minutes,resolution_minutes,working_weekdays,working_start,working_end,time_zone_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.ResponseMinutes, Db(request.ArrivalMinutes), request.ResolutionMinutes, request.WorkingWeekdays, start, end, request.TimeZoneId); return await ReadSlaProfileAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.sla_profile_not_found");
    }, ct);

    public Task<SlaClockView> StartSlaClockAsync(TenantId tenant, Guid actor, StartSlaClockRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var profile = await ReadSlaProfileAsync(context, tenant.Value, request.ProfileId, token) ?? throw new ServiceException("service.sla_profile_not_found"); var minutes = request.ClockType switch { "Response" => profile.ResponseMinutes, "Arrival" when profile.ArrivalMinutes is not null => profile.ArrivalMinutes.Value, "Resolution" => profile.ResolutionMinutes, _ => throw new ServiceException("service.sla_clock_invalid") }; var dueAt = BusinessDue(request.StartedAt, minutes, profile); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.sla_clock(tenant_id,id,work_order_id,clock_type,started_at,due_at) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,work_order_id,clock_type) do update set started_at=excluded.started_at,due_at=excluded.due_at,state='Running',paused_minutes=0", token, tenant.Value, id, request.WorkOrderId, request.ClockType, request.StartedAt, dueAt); return await ReadSlaClockAsync(context, tenant.Value, request.WorkOrderId, request.ClockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
    }, ct);

    public Task<SlaClockView> PauseSlaClockAsync(TenantId tenant, Guid actor, Guid workOrderId, string clockType, SlaClockActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var clock = await ReadSlaClockAsync(context, tenant.Value, workOrderId, clockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
        if (clock.State != "Running") throw new ServiceException("service.sla_clock_state_invalid");
        var at = request.OccurredAt ?? DateTimeOffset.UtcNow; await ExecuteAsync(context, "update business_service.sla_clock set state='Paused' where tenant_id=$1 and id=$2 and state='Running'", token, tenant.Value, clock.Id); await ExecuteAsync(context, "insert into business_service.sla_clock_event(tenant_id,id,clock_id,event_type,occurred_at,reason,actor_id) values($1,$2,$3,'Paused',$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), clock.Id, at, Db(request.Reason), actor); return await ReadSlaClockAsync(context, tenant.Value, workOrderId, clockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
    }, ct);

    public Task<SlaClockView> ResumeSlaClockAsync(TenantId tenant, Guid actor, Guid workOrderId, string clockType, SlaClockActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var clock = await ReadSlaClockAsync(context, tenant.Value, workOrderId, clockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
        if (clock.State != "Paused") throw new ServiceException("service.sla_clock_state_invalid");
        var at = request.OccurredAt ?? DateTimeOffset.UtcNow; var pausedAt = await ScalarDateTimeOffsetAsync(context, "select occurred_at from business_service.sla_clock_event where tenant_id=$1 and clock_id=$2 and event_type='Paused' order by occurred_at desc limit 1", token, tenant.Value, clock.Id) ?? at; var minutes = Math.Max(0, (int)(at - pausedAt).TotalMinutes); await ExecuteAsync(context, "update business_service.sla_clock set state='Running',paused_minutes=paused_minutes+$3,due_at=due_at+$3 * interval '1 minute' where tenant_id=$1 and id=$2", token, tenant.Value, clock.Id, minutes); await ExecuteAsync(context, "insert into business_service.sla_clock_event(tenant_id,id,clock_id,event_type,occurred_at,reason,actor_id) values($1,$2,$3,'Resumed',$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), clock.Id, at, Db(request.Reason), actor); return await ReadSlaClockAsync(context, tenant.Value, workOrderId, clockType, token) ?? throw new ServiceException("service.sla_clock_not_found");
    }, ct);

    public Task<SlaMonitorView> MonitorSlaAsync(TenantId tenant, Guid actor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var ids = new List<Guid>(); await using (var command = Command(context, "select id from business_service.sla_clock where tenant_id=$1 and state in ('Running','Paused') and due_at < now()", tenant.Value)) await using (var reader = await command.ExecuteReaderAsync(token)) while (await reader.ReadAsync(token)) ids.Add(reader.GetGuid(0));
        foreach (var id in ids) { await ExecuteAsync(context, "update business_service.sla_clock set state='Breached' where tenant_id=$1 and id=$2 and state in ('Running','Paused')", token, tenant.Value, id); await ExecuteAsync(context, "insert into business_service.sla_clock_event(tenant_id,id,clock_id,event_type,occurred_at,actor_id) values($1,$2,$3,'Breached',now(),$4)", token, tenant.Value, Guid.CreateVersion7(), id, actor); }
        return new SlaMonitorView(ids.Count);
    }, ct);

    public Task<MeterReadingView> RecordMeterAsync(TenantId tenant, Guid actor, RecordMeterReadingRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var subjectEntity = await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.installed_base_item where tenant_id=$1 and id=$2", token, tenant.Value, request.SubjectId) ?? throw new ServiceException("service.subject_not_found");
        if (subjectEntity != request.LegalEntityId) throw new ServiceException("service.legal_entity_mismatch");
        var definitionId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.meter_definition(tenant_id,id,subject_id,meter_code,uom) values($1,$2,$3,$4,$5) on conflict(tenant_id,subject_id,meter_code) do update set uom=excluded.uom", token, tenant.Value, definitionId, request.SubjectId, request.MeterCode.Trim(), request.Uom); var actualDefinition = await ScalarAsync<Guid>(context, "select id from business_service.meter_definition where tenant_id=$1 and subject_id=$2 and meter_code=$3", token, tenant.Value, request.SubjectId, request.MeterCode.Trim()); var duplicate = await ScalarAsync<Guid?>(context, "select id from business_service.meter_reading where tenant_id=$1 and subject_id=$2 and meter_code=$3 and reading_at=$4", token, tenant.Value, request.SubjectId, request.MeterCode.Trim(), request.ReadingAt); if (duplicate is { } existing) return await ReadMeterAsync(context, tenant.Value, existing, token) ?? throw new ServiceException("service.meter_reading_invalid"); var prior = await ScalarAsync<decimal?>(context, "select value from business_service.meter_reading where tenant_id=$1 and subject_id=$2 and meter_code=$3 and reading_at <= $4 order by reading_at desc limit 1", token, tenant.Value, request.SubjectId, request.MeterCode.Trim(), request.ReadingAt); var rollover = await ScalarAsync<decimal?>(context, "select rollover_value from business_service.meter_definition where tenant_id=$1 and id=$2", token, tenant.Value, actualDefinition); if (prior is { } old && request.Value < old && !request.IsReset && (rollover is null || old - request.Value > rollover)) throw new ServiceException("service.meter_decreased_unexpectedly"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.meter_reading(tenant_id,id,subject_id,meter_definition_id,meter_code,value,uom,reading_at,source,is_reset) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, request.SubjectId, actualDefinition, request.MeterCode.Trim(), request.Value, request.Uom, request.ReadingAt, request.Source, request.IsReset); return await ReadMeterAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.meter_reading_invalid");
    }, ct);

    public Task<ServiceContractView> CreateContractAsync(TenantId tenant, Guid actor, CreateContractRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.service_contract(tenant_id,id,legal_entity_id,contract_number,contract_holder_party_id,bill_to_party_id,contract_type,start_date,end_date,currency_code,value,state,scope) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'Draft',$12::jsonb)", token, tenant.Value, id, request.LegalEntityId, Number("CTR", id), request.ContractHolderPartyId, request.BillToPartyId, request.ContractType, request.StartDate, request.EndDate, request.CurrencyCode, Db(request.Value), request.ScopeJson ?? "{}"); return await ReadContractAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.contract_not_found");
    }, ct);

    public Task<ServiceContractView> ActivateContractAsync(TenantId tenant, Guid actor, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.service_contract set state='Active' where tenant_id=$1 and id=$2 and state in ('Draft','Suspended')", token, tenant.Value, id); return await ReadContractAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.contract_not_found"); }, ct);

    public Task<ServiceContractView> RecordContractUsageAsync(TenantId tenant, Guid actor, Guid contractId, ContractUsageRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.UsageType) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ServiceException("service.contract_usage_invalid");
        var contract = await ReadContractAsync(context, tenant.Value, contractId, token) ?? throw new ServiceException("service.contract_not_found");
        if (contract.State != "Active" || request.WorkOrderId == Guid.Empty) throw new ServiceException("service.contract_state_invalid");
        var prior = await ScalarAsync<Guid?>(context, "select id from business_service.service_contract_usage where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey.Trim()); if (prior is not null) return contract;
        _ = await ScalarAsync<Guid>(context, "select id from business_service.service_contract where tenant_id=$1 and id=$2 for update", token, tenant.Value, contractId);
        var current = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(quantity),0) from business_service.service_contract_usage where tenant_id=$1 and contract_id=$2 and usage_type=$3", token, tenant.Value, contractId, request.UsageType), CultureInfo.InvariantCulture);
        var limit = ScopeLimit(contract.ScopeJson, request.UsageType, decimal.MaxValue); if (limit < decimal.MaxValue && current + request.Quantity > limit) throw new ServiceException("service.contract_usage_limit_exceeded");
        await ExecuteAsync(context, "insert into business_service.service_contract_usage(tenant_id,id,contract_id,work_order_id,usage_type,quantity,idempotency_key) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, Guid.CreateVersion7(), contractId, request.WorkOrderId, request.UsageType.Trim(), request.Quantity, request.IdempotencyKey.Trim());
        return contract;
    }, ct);

    public Task<EntitlementView> ResolveEntitlementAsync(TenantId tenant, Guid actor, EntitlementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        Guid? contract = null; string? contractScope = null;
        if (request.CustomerPartyId is { } customer)
        {
            contract = await ScalarAsync<Guid?>(context, "select id from business_service.service_contract where tenant_id=$1 and legal_entity_id=$2 and contract_holder_party_id=$3 and state='Active' and $4 between start_date and end_date order by end_date desc limit 1", token, tenant.Value, request.LegalEntityId, customer, request.AsOfDate);
            if (contract is { } contractId) contractScope = await ScalarAsync<string?>(context, "select scope::text from business_service.service_contract where tenant_id=$1 and id=$2", token, tenant.Value, contractId);
        }
        Guid? warranty = null; string? warrantyScope = null;
        if (request.InstalledBaseId is { } subject)
        {
            warranty = await ScalarAsync<Guid?>(context, "select id from business_service.warranty where tenant_id=$1 and legal_entity_id=$2 and subject_id=$3 and (customer_party_id=$4 or warranty_type='Supplier') and state='Active' and $5 between start_date and end_date order by end_date desc limit 1", token, tenant.Value, request.LegalEntityId, subject, Db(request.CustomerPartyId), request.AsOfDate);
            if (warranty is { } warrantyId) warrantyScope = await ScalarAsync<string?>(context, "select scope::text from business_service.warranty where tenant_id=$1 and id=$2", token, tenant.Value, warrantyId);
        }
        var requested = request.Lines is { Count: > 0 } ? request.Lines : [new EntitlementLineRequest("Labor", 0m)];
        var resolved = new List<CoverageResult>(requested.Count);
        foreach (var line in requested)
        {
            var category = line.ChargeCategory.Trim(); var amount = Math.Max(0m, line.Amount); var source = "Paid"; var covered = 0m; var explanation = "No active coverage";
            var warrantyCovers = warranty is not null && ScopeCovers(warrantyScope, category);
            var contractCovers = contract is not null && ScopeCovers(contractScope, category);
            if (warrantyCovers) { source = "CustomerWarranty"; covered = amount; explanation = "Covered by active customer warranty"; }
            else if (contractCovers)
            {
                source = "ServiceContract"; covered = Math.Min(amount, ScopeLimit(contractScope, category, amount));
                explanation = covered < amount ? "Service contract limit applied" : "Covered by active service contract";
            }
            var result = new CoverageResult(category, source, covered > 0m || amount == 0m && source != "Paid", covered, amount - covered, explanation); resolved.Add(result);
            if (request.WorkOrderId is { } workOrder)
                await ExecuteAsync(context, "insert into business_service.coverage_decision(tenant_id,id,work_order_id,charge_category,source_type,source_id,covered,covered_amount,customer_amount,explanation,decided_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) on conflict(tenant_id,work_order_id,charge_category) do update set source_type=excluded.source_type,source_id=excluded.source_id,covered=excluded.covered,covered_amount=excluded.covered_amount,customer_amount=excluded.customer_amount,explanation=excluded.explanation,decided_at=now(),decided_by=excluded.decided_by", token, tenant.Value, Guid.CreateVersion7(), workOrder, category, source, Db(source == "ServiceContract" ? contract : warranty), result.Covered, result.CoveredAmount, result.CustomerAmount, result.Explanation, actor);
        }
        return new EntitlementView(resolved, contract is null ? "NoContract" : "Active", contract, warranty);
    }, ct);

    public async Task<ServiceChargeView> CreateChargeAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateChargeRequest request, CancellationToken ct)
    {
        var charge = await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var order = await ReadWorkOrderAsync(context, tenant.Value, workOrderId, token) ?? throw new ServiceException("service.work_order_not_found");
            if (request.CorrectionOfId is { } correctionId)
            {
                var correction = await ReadChargeAsync(context, tenant.Value, correctionId, token);
                if (correction is null || correction.WorkOrderId != workOrderId) throw new ServiceException("service.charge_correction_invalid");
            }
            var id = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_service.service_charge(tenant_id,id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,source_type,source_id,correction_of_id,state,invoice_integration_state) values($1,$2,$3,$4,$5,$6,0,$7,$8,$9,$10,$11,'Reviewed','NotRequested') on conflict(tenant_id,work_order_id,category,source_type,source_id) do update set customer_amount=excluded.customer_amount,cost_amount=excluded.cost_amount,correction_of_id=excluded.correction_of_id", token, tenant.Value, id, workOrderId, request.Category, request.Description, request.CostAmount, request.CustomerAmount, request.CurrencyCode, Db(request.SourceType), Db(request.SourceId), Db(request.CorrectionOfId));
            var persisted = await ReadChargeAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.charge_not_found");
            return (Order: order, Charge: persisted);
        }, ct);
        if (charge.Order.BillingOwner == "Projects")
        {
            if (charge.Order.ProjectId is null || projects is null) throw new ServiceException("service.project_integration_unavailable");
            try
            {
                var fact = await projects.RecordCostAsync(new ProjectCostFactIngress(tenant, actor, charge.Order.ProjectId.Value, null,
                    "service", "ServiceCharge", charge.Charge.Id.ToString("D"), null, request.Category, request.CurrencyCode,
                    request.CostAmount, request.CurrencyCode, request.CostAmount, 1m, DateOnly.FromDateTime(DateTime.UtcNow),
                    $"service-charge:{charge.Charge.Id:D}", LegalEntityId: charge.Order.LegalEntityId), Guid.CreateVersion7(), ct);
                await coordinator.ExecuteAsync(tenant, async (context, token) =>
                {
                    await ExecuteAsync(context, "update business_service.service_charge set project_cost_fact_id=$3 where tenant_id=$1 and id=$2", token, tenant.Value, charge.Charge.Id, fact.FactId);
                    return 0;
                }, ct);
            }
            catch (Exception ex) when (ex is not ServiceException)
            {
                throw new ServiceException("service.project_cost_integration_retryable");
            }
        }
        return charge.Charge;
    }

    public Task<PagedResult<ServiceChargeView>> ListChargesAsync(TenantId tenant, Guid workOrderId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize; var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_charge where tenant_id=$1 and work_order_id=$2", token, tenant.Value, workOrderId), CultureInfo.InvariantCulture); var rows = new List<ServiceChargeView>(); await using var command = Command(context, "select id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,state,invoice_id,correction_of_id from business_service.service_charge where tenant_id=$1 and work_order_id=$2 order by id limit $3 offset $4", tenant.Value, workOrderId, pageSize + 1, offset); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadCharge(reader)); var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<ServiceChargeView>(rows, page, pageSize, hasNext, total); }, ct);

    public Task<PagedResult<ServiceChargeView>> ListAllChargesAsync(TenantId tenant, Guid? legalEntityId, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize);
        var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_charge c join business_service.work_order w on w.tenant_id=c.tenant_id and w.id=c.work_order_id where c.tenant_id=$1 and ($2::uuid is null or w.legal_entity_id=$2) and ($3::text is null or c.state=$3)", token, tenant.Value, Db(legalEntityId), Db(state)), CultureInfo.InvariantCulture);
        var rows = new List<ServiceChargeView>();
        await using var command = Command(context, "select c.id,c.work_order_id,c.category,c.description,c.cost_amount,c.covered_amount,c.customer_amount,c.currency_code,c.state,c.invoice_id,c.correction_of_id from business_service.service_charge c join business_service.work_order w on w.tenant_id=c.tenant_id and w.id=c.work_order_id where c.tenant_id=$1 and ($2::uuid is null or w.legal_entity_id=$2) and ($3::text is null or c.state=$3) order by c.id limit $4 offset $5", tenant.Value, Db(legalEntityId), Db(state), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) rows.Add(ReadCharge(reader));
        var hasNext = rows.Count > pageSize;
        if (hasNext) rows.RemoveAt(rows.Count - 1);
        return new PagedResult<ServiceChargeView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<PartRequirementView> AddPartAsync(TenantId tenant, Guid actor, Guid workOrderId, CreatePartRequirementRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { if (request.Quantity <= 0) throw new ServiceException("service.part_quantity_invalid"); var orderEntity = await ScalarAsync<Guid?>(context, "select legal_entity_id from business_service.work_order where tenant_id=$1 and id=$2", token, tenant.Value, workOrderId) ?? throw new ServiceException("service.work_order_not_found"); if (request.LegalEntityId is { } requestedEntity && requestedEntity != orderEntity) throw new ServiceException("service.legal_entity_mismatch"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.work_part_requirement(tenant_id,id,work_order_id,product_id,quantity,uom_code,description,legal_entity_id,warehouse_id,location_id,unit_of_measure_id,lot_id,serial_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", token, tenant.Value, id, workOrderId, request.ProductId, request.Quantity, request.UomCode, Db(request.Description), orderEntity, Db(request.WarehouseId), Db(request.LocationId), Db(request.UnitOfMeasureId), Db(request.LotId), Db(request.SerialId)); return await ReadPartAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.part_requirement_not_found"); }, ct);

    public Task<LaborView> RecordLaborAsync(TenantId tenant, Guid actor, RecordLaborRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { if (request.EndedAt <= request.StartedAt) throw new ServiceException("service.labor_time_invalid"); var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"labor:{request.VisitId:D}:{request.StartedAt:O}" : request.IdempotencyKey.Trim(); var prior = await ScalarAsync<Guid?>(context, "select id from business_service.service_labor_entry where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, key); if (prior is { } existing) return await ReadLaborAsync(context, tenant.Value, existing, token) ?? throw new ServiceException("service.labor_not_found"); var id = Guid.CreateVersion7(); var hours = (decimal)(request.EndedAt - request.StartedAt).TotalHours; var amount = request.CostRate is null ? null : request.CostRate * hours; await ExecuteAsync(context, "insert into business_service.service_labor_entry(tenant_id,id,visit_id,employee_id,started_at,ended_at,time_category,cost_rate,currency_code,cost_amount,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, request.VisitId, Db(request.EmployeeId), request.StartedAt, request.EndedAt, request.TimeCategory, Db(request.CostRate), request.CurrencyCode, Db(amount), key); if (amount is > 0m) await ExecuteAsync(context, "insert into business_service.service_cost_fact(tenant_id,id,work_order_id,source_module,source_type,source_id,cost_category,currency_code,amount,fact_date,idempotency_key) select $1,$2,v.work_order_id,'service','Labor', $3,'Labor',$4,$5,current_date,$6 from business_service.service_visit v where v.tenant_id=$1 and v.id=$7", token, tenant.Value, Guid.CreateVersion7(), id.ToString("D"), request.CurrencyCode, amount.Value, $"labor-cost:{key}", request.VisitId); return await ReadLaborAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.labor_not_found"); }, ct);

    public async Task<OfflineCommandView> SubmitOfflineAsync(TenantId tenant, Guid actor, Guid correlationId, OfflineCommandRequest request, CancellationToken ct)
    {
        var pending = await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var prior = await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token); if (prior is not null) return (View: prior, New: false);
            if (request.WorkOrderId is { } work && request.ExpectedVersion is { } version)
            {
                var current = await ScalarAsync<long?>(context, "select row_version from business_service.work_order where tenant_id=$1 and id=$2", token, tenant.Value, work); if (current is null) throw new ServiceException("service.work_order_not_found");
                if (current != version) { var conflictId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.offline_command(tenant_id,id,client_command_id,command_type,work_order_id,visit_id,expected_version,payload,state,conflict_code,processed_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,'Conflict','service.concurrent_change',now())", token, tenant.Value, conflictId, request.ClientCommandId, request.CommandType, Db(request.WorkOrderId), Db(request.VisitId), Db(request.ExpectedVersion), request.PayloadJson); return (View: await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token) ?? throw new ServiceException("service.offline_command_not_found"), New: false); }
            }
            var assignmentRequired = request.CommandType.StartsWith("visit.", StringComparison.Ordinal) || request.CommandType.StartsWith("work.", StringComparison.Ordinal) || request.CommandType is "checklist.answer" or "meter.record" or "part.consume";
            if (assignmentRequired && !await IsActorAssignedAsync(context, tenant, actor, request.WorkOrderId, request.VisitId, token)) throw new ServiceException("service.offline_assignment_required");
            var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.offline_command(tenant_id,id,client_command_id,command_type,work_order_id,visit_id,expected_version,payload,state) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,'Pending')", token, tenant.Value, id, request.ClientCommandId, request.CommandType, Db(request.WorkOrderId), Db(request.VisitId), Db(request.ExpectedVersion), request.PayloadJson);
            return (View: await ReadOfflineAsync(context, tenant.Value, request.ClientCommandId, token) ?? throw new ServiceException("service.offline_command_not_found"), New: true);
        }, ct);
        if (!pending.New) return pending.View;
        try
        {
            object? result = request.CommandType switch
            {
                "visit.arrive" or "visit.start" or "visit.complete" or "visit.no-access" => await TransitionVisitAsync(tenant, actor, request.VisitId ?? throw new ServiceException("service.visit_required"), request.CommandType[6..], new TransitionRequest(request.ExpectedVersion ?? 0), ct),
                "work.start" or "work.technical-complete" => await TransitionWorkOrderAsync(tenant, actor, request.WorkOrderId ?? throw new ServiceException("service.work_order_required"), request.CommandType[5..], new TransitionRequest(request.ExpectedVersion ?? 0), ct),
                "checklist.answer" => JsonSerializer.Deserialize<ChecklistAnswerRequest>(request.PayloadJson, OfflineJsonOptions) is { } answer ? await SaveChecklistAnswerAsync(tenant, actor, answer, ct) : throw new ServiceException("service.offline_payload_invalid"),
                "meter.record" => JsonSerializer.Deserialize<RecordMeterReadingRequest>(request.PayloadJson, OfflineJsonOptions) is { } meter ? await RecordMeterAsync(tenant, actor, meter, ct) : throw new ServiceException("service.offline_payload_invalid"),
                "part.consume" => JsonSerializer.Deserialize<OfflinePartCommand>(request.PayloadJson, OfflineJsonOptions) is { } part ? await ApplyPartActionAsync(tenant, actor, part.PartId, "consume", part.Request, ct) : throw new ServiceException("service.offline_payload_invalid"),
                _ => throw new ServiceException("service.offline_command_type_invalid")
            };
            return await MarkOfflineAsync(tenant, request.ClientCommandId, "Processed", null, JsonSerializer.Serialize(result), ct);
        }
        catch (ServiceException ex)
        {
            var state = ex.Code.Contains("concurrent", StringComparison.Ordinal) || ex.Code.Contains("conflict", StringComparison.Ordinal) ? "Conflict" : ex.Code.Contains("retryable", StringComparison.Ordinal) || ex.Code.Contains("in_progress", StringComparison.Ordinal) ? "Retryable" : "Rejected";
            return await MarkOfflineAsync(tenant, request.ClientCommandId, state, ex.Code, null, ct);
        }
    }

    private async Task<OfflineCommandView> MarkOfflineAsync(TenantId tenant, Guid clientCommandId, string state, string? code, string? result, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.offline_command set state=$3,conflict_code=$4,result=$5::jsonb,processed_at=now() where tenant_id=$1 and client_command_id=$2", token, tenant.Value, clientCommandId, state, Db(code), Db(result ?? "null")); return await ReadOfflineAsync(context, tenant.Value, clientCommandId, token) ?? throw new ServiceException("service.offline_command_not_found"); }, ct);
    private sealed record OfflinePartCommand(Guid PartId, PartActionRequest Request);

    public Task<MobileDayView> GetMobileDayAsync(TenantId tenant, Guid? userId, Guid? employeeId, DateOnly date, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var visits = new List<VisitView>(); await using var command = Command(context, "select v.id,v.work_order_id,v.planned_start,v.planned_end,v.location_type,v.state,v.site_instructions,v.actual_start,v.actual_end,v.row_version from business_service.service_visit v join business_service.work_assignment a on a.tenant_id=v.tenant_id and a.visit_id=v.id and a.state='Assigned' where v.tenant_id=$1 and v.planned_start::date=$2 and ($3::uuid is null or a.user_id=$3) order by v.planned_start,v.id limit 200", tenant.Value, date, Db(userId)); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) visits.Add(ReadVisit(reader)); var orders = new List<WorkOrderView>(); foreach (var visit in visits) { var row = await ReadWorkOrderAsync(context, tenant.Value, visit.WorkOrderId, token); if (row is not null) orders.Add(row); } return new MobileDayView(visits, orders, date); }, ct);

    public Task<PagedResult<TemplateView>> ListTemplatesAsync(TenantId tenant, Guid? legalEntityId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_work_template where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2)", token, tenant.Value, Db(legalEntityId)), CultureInfo.InvariantCulture);
        var rows = new List<TemplateView>();
        await using var command = Command(context, "select t.id,t.legal_entity_id,t.code,t.name,v.version_no,v.expected_duration_hours,v.definition::text,t.state from business_service.service_work_template t join lateral (select version_no,expected_duration_hours,definition from business_service.service_work_template_version v where v.tenant_id=t.tenant_id and v.template_id=t.id order by version_no desc limit 1) v on true where t.tenant_id=$1 and ($2::uuid is null or t.legal_entity_id=$2) order by t.code,t.id limit $3 offset $4", tenant.Value, Db(legalEntityId), pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(ReadTemplate(reader));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<TemplateView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<ChecklistAnswerView> SaveChecklistAnswerAsync(TenantId tenant, Guid actor, ChecklistAnswerRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ValidateChecklistAnswer(request);
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
        if (request.ReplacedComponentId is { } replaced && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, replaced, token)) throw new ServiceException("service.installed_base_not_found");
        if (request.EventType is "Installed" or "Replaced")
        {
            if (await HasDescendantAsync(context, tenant.Value, request.ComponentItemId, request.ItemId, token)) throw new ServiceException("service.component_cycle");
            await ExecuteAsync(context, "update business_service.installed_base_item set parent_id=$3,row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, request.ComponentItemId, request.ItemId);
            if (request.EventType == "Replaced" && request.ReplacedComponentId is { } oldComponent)
                await ExecuteAsync(context, "update business_service.installed_base_item set parent_id=null,row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and parent_id=$3", token, tenant.Value, oldComponent, request.ItemId);
        }
        else
        {
            await ExecuteAsync(context, "update business_service.installed_base_item set parent_id=null,row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and parent_id=$3", token, tenant.Value, request.ComponentItemId, request.ItemId);
        }
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.installed_base_component_event(tenant_id,id,item_id,component_item_id,event_type,occurred_at,replaced_component_id,work_order_id,notes) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, request.ItemId, request.ComponentItemId, request.EventType, request.OccurredAt, Db(request.ReplacedComponentId), Db(request.WorkOrderId), Db(request.Notes));
        return await ReadComponentEventAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.component_event_not_found");
    }, ct);

    public Task<WarrantyView> CreateWarrantyAsync(TenantId tenant, Guid actor, CreateWarrantyRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.WarrantyType is not ("Customer" or "Supplier") || request.EndDate < request.StartDate) throw new ServiceException("service.warranty_invalid");
        if (request.WarrantyType == "Customer" && request.CustomerPartyId is null) throw new ServiceException("service.warranty_customer_required");
        if (request.WarrantyType == "Supplier" && request.ProviderPartyId is null) throw new ServiceException("service.warranty_supplier_required");
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_service.warranty(tenant_id,id,legal_entity_id,warranty_type,provider_party_id,customer_party_id,subject_id,start_date,end_date,state,scope) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10::jsonb)", token, tenant.Value, id, request.LegalEntityId, request.WarrantyType, Db(request.ProviderPartyId), Db(request.CustomerPartyId), request.SubjectId, request.StartDate, request.EndDate, request.ScopeJson ?? "{}");
        return await ReadWarrantyAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.warranty_not_found");
    }, ct);

    public async Task<PartRequirementView> ApplyPartActionAsync(TenantId tenant, Guid actor, Guid partId, string action, PartActionRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0 || action is not ("reserve" or "issue" or "consume" or "return" or "reverse")) throw new ServiceException("service.part_action_invalid");
        var execution = await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var part = await ReadPartAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.part_requirement_not_found");
            var details = await ReadPartIntegrationAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.inventory_context_required");
            var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"service-part:{partId:D}:{action}:{request.Quantity:0.######}:{request.WarehouseId:D}" : request.IdempotencyKey.Trim();
            Guid? originalMovementId = null;
            Guid? originalOperationId = null;
            if (action == "reverse")
            {
                if (request.InventoryOperationId is null) throw new ServiceException("service.inventory_operation_required");
                await using var original = Command(context, "select id,inventory_operation_id,quantity from business_service.work_part_movement where tenant_id=$1 and part_requirement_id=$2 and action='issue' and state='Processed' and inventory_operation_id=$3 for update", tenant.Value, partId, request.InventoryOperationId.Value);
                await using var originalReader = await original.ExecuteReaderAsync(token);
                if (!await originalReader.ReadAsync(token)) throw new ServiceException("service.inventory_operation_not_found");
                originalMovementId = originalReader.GetGuid(0);
                originalOperationId = originalReader.GetGuid(1);
                if (originalReader.GetDecimal(2) != request.Quantity) throw new ServiceException("service.part_reversal_must_match_issue");
            }
            var prior = await ReadPartMovementAsync(context, tenant.Value, key, token);
            if (prior is { State: "Processed" }) return (Part: part, Details: details, Key: key, Movement: prior, Existing: true);
            if (prior is { State: "Pending" }) throw new ServiceException("service.inventory_operation_in_progress");
            try { ServiceRules.ApplyPartMovement(action, request.Quantity, part.Quantity, part.ReservedQuantity, part.IssuedQuantity, part.ConsumedQuantity, part.ReturnedQuantity); }
            catch (ArgumentOutOfRangeException) { throw new ServiceException("service.part_quantity_invalid"); }
            catch (InvalidOperationException) { throw new ServiceException(action is "consume" or "return" ? "service.part_quantity_exceeds_issue" : "service.part_quantity_exceeds_required"); }
            var movementId = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_service.work_part_movement(tenant_id,id,part_requirement_id,action,quantity,idempotency_key,actor_id) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, movementId, partId, action, request.Quantity, key, actor);
            return (Part: part, Details: details, Key: key, Movement: new PartMovement(movementId, action, request.Quantity, "Pending", originalMovementId, originalOperationId), Existing: false);
        }, ct);
        if (execution.Existing) return execution.Part;
        if (inventory is null) throw new ServiceException("service.inventory_integration_unavailable");
        Guid? reservationId = request.InventoryReservationId;
        Guid? operationId = null;
        try
        {
            if (action == "reserve")
            {
                reservationId = await inventory.ReserveAsync(tenant.Value, actor, new InventoryReservationRequest(execution.Part.ProductId,
                    execution.Details.LegalEntityId, execution.Details.WarehouseId, execution.Details.LocationId, request.Quantity,
                    "service", execution.Part.WorkOrderId, partId, execution.Key, null, request.LotId ?? execution.Details.LotId, request.SerialId ?? execution.Details.SerialId), ct);
            }
            else if (action == "issue")
            {
                reservationId ??= await coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context,
                    "select inventory_reservation_id from business_service.work_part_movement where tenant_id=$1 and part_requirement_id=$2 and action='reserve' and state='Processed' order by created_at desc limit 1", token, tenant.Value, partId), ct);
                if (reservationId is null) throw new ServiceException("service.inventory_reservation_required");
                operationId = await inventory.ConsumeReservationAsync(tenant.Value, actor, reservationId.Value, new InventoryMovementRequest(execution.Part.ProductId,
                    execution.Details.LegalEntityId, execution.Details.WarehouseId, execution.Details.LocationId, request.Quantity,
                    execution.Details.UnitOfMeasureId, 1m, "service", execution.Part.WorkOrderId, execution.Key, DateOnly.FromDateTime(DateTime.UtcNow),
                    DateOnly.FromDateTime(DateTime.UtcNow), "ServicePart", request.LotId ?? execution.Details.LotId, request.SerialId ?? execution.Details.SerialId), ct);
            }
            else if (action == "return")
            {
                operationId = await inventory.ReceiveReturnAsync(tenant.Value, actor, new InventoryReturnRequest(execution.Part.ProductId,
                    execution.Details.LegalEntityId, execution.Details.WarehouseId, execution.Details.LocationId, execution.Details.UnitOfMeasureId,
                    request.Quantity, "service", execution.Part.WorkOrderId, partId, execution.Key, DateOnly.FromDateTime(DateTime.UtcNow),
                    DateOnly.FromDateTime(DateTime.UtcNow), "Available", request.LotId ?? execution.Details.LotId, request.SerialId ?? execution.Details.SerialId), CancellationToken.None);
            }
            else if (action == "reverse")
            {
                if (execution.Movement.OriginalOperationId is null) throw new ServiceException("service.inventory_operation_required");
                operationId = await inventory.ReverseOperationAsync(tenant.Value, actor, execution.Details.LegalEntityId, execution.Movement.OriginalOperationId!.Value,
                    DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), execution.Key, ct);
            }
            else
            {
                operationId = await coordinator.ExecuteAsync(tenant, async (context, token) => await ScalarAsync<Guid?>(context,
                    "select inventory_operation_id from business_service.work_part_movement where tenant_id=$1 and part_requirement_id=$2 and action='issue' and state='Processed' order by created_at desc limit 1", token, tenant.Value, partId), ct);
                if (operationId is null) throw new ServiceException("service.inventory_issue_required");
                var evidence = await inventory.GetMovementHistoryAsync(tenant.Value,
                    new InventoryMovementHistoryRequest(execution.Part.ProductId, execution.Details.LegalEntityId,
                        execution.Details.WarehouseId, execution.Details.LocationId, execution.Part.WorkOrderId, 100), ct);
                if (!evidence.Any(item => item.OperationId == operationId.Value &&
                    item.SourceModule == "service" && item.SourceDocumentId == execution.Part.WorkOrderId &&
                    item.SignedBaseQuantity < 0m))
                    throw new ServiceException("service.inventory_consumption_evidence_required");
            }
        }
        catch (ServiceException) { await MarkPartMovementAsync(tenant, execution.Movement.Id, "Retryable", null, null, ct); throw; }
        catch (Exception) { await MarkPartMovementAsync(tenant, execution.Movement.Id, "Retryable", null, "service.inventory_operation_retryable", ct); throw new ServiceException("service.inventory_operation_retryable"); }
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var current = await ReadPartAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.part_requirement_not_found");
            var column = action switch { "reserve" => "reserved_quantity", "issue" => "issued_quantity", "consume" => "consumed_quantity", "reverse" => "issued_quantity", _ => "returned_quantity" };
            var amount = action == "reverse" ? -request.Quantity : request.Quantity;
            var state = action switch { "reserve" => "Reserved", "issue" => "Issued", "consume" => "Consumed", "reverse" => "Issued", _ => "Returned" };
            await ExecuteAsync(context, $"update business_service.work_part_requirement set {column}={column}+$3,state=$4,inventory_reference=coalesce($5,inventory_reference) where tenant_id=$1 and id=$2", token, tenant.Value, partId, amount, state, Db(request.InventoryReference ?? operationId?.ToString("D")));
            if (action == "reverse" && execution.Movement.OriginalMovementId is { } reversedMovement)
                await ExecuteAsync(context, "update business_service.work_part_movement set state='Reversed',processed_at=now() where tenant_id=$1 and id=$2 and state='Processed'", token, tenant.Value, reversedMovement);
            await ExecuteAsync(context, "update business_service.work_part_movement set state='Processed',inventory_reservation_id=$3,inventory_operation_id=$4,inventory_reference=$5,processed_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, execution.Movement.Id, Db(reservationId), Db(operationId), Db(request.InventoryReference ?? operationId?.ToString("D")));
            return await ReadPartAsync(context, tenant.Value, partId, token) ?? throw new ServiceException("service.part_requirement_not_found");
        }, ct);
    }

    private async Task MarkPartMovementAsync(TenantId tenant, Guid movementId, string state, Guid? operationId, string? error, CancellationToken ct) => await coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.work_part_movement set state=$3,inventory_operation_id=$4,error_code=$5 where tenant_id=$1 and id=$2", token, tenant.Value, movementId, state, Db(operationId), Db(error)); return 0; }, ct);

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

    public Task<MaintenanceRoundView> CreateMaintenanceRoundAsync(TenantId tenant, Guid actor, CreateMaintenanceRoundRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ServiceException("service.round_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.maintenance_round(tenant_id,id,legal_entity_id,code,name,scheduled_date,created_by) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.ScheduledDate, actor);
        return await ReadRoundAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.round_not_found");
    }, ct);

    public Task<RoundSubjectView> AddRoundSubjectAsync(TenantId tenant, Guid actor, Guid roundId, AddRoundSubjectRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.SubjectType is not ("InstalledBase" or "FixedAsset") || request.SequenceNo < 1) throw new ServiceException("service.round_subject_invalid");
        if (!await ExistsAsync(context, "business_service.maintenance_round", tenant.Value, roundId, token)) throw new ServiceException("service.round_not_found");
        if (request.SubjectType == "InstalledBase" && !await ExistsAsync(context, "business_service.installed_base_item", tenant.Value, request.SubjectId, token)) throw new ServiceException("service.subject_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.maintenance_round_subject(tenant_id,id,round_id,subject_type,subject_id,sequence_no) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, roundId, request.SubjectType, request.SubjectId, request.SequenceNo);
        return await ReadRoundSubjectAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.round_subject_not_found");
    }, ct);

    public Task<MaintenanceRoundView> TransitionRoundAsync(TenantId tenant, Guid actor, Guid roundId, string action, TransitionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var current = await ReadRoundAsync(context, tenant.Value, roundId, token) ?? throw new ServiceException("service.round_not_found");
        if (current.Version != request.Version) throw new ServiceException("service.concurrent_change");
        var next = action switch
        {
            "plan" when current.State == "Draft" => "Planned",
            "start" when current.State == "Planned" => "InProgress",
            "complete" when current.State == "InProgress" => "Completed",
            "cancel" when current.State is not ("Completed" or "Cancelled") => "Cancelled",
            _ => throw new ServiceException("service.round_transition_invalid")
        };
        await ExecuteAsync(context, "update business_service.maintenance_round set state=$3,row_version=row_version+1,updated_at=now() where tenant_id=$1 and id=$2 and row_version=$4", token, tenant.Value, roundId, next, current.Version);
        await AuditAsync(context, tenant, "MaintenanceRound", roundId, actor, $"round_{action}", new { current.State }, new { next }, token);
        return await ReadRoundAsync(context, tenant.Value, roundId, token) ?? throw new ServiceException("service.round_not_found");
    }, ct);

    public Task<RoundSubjectView> RecordRoundSubjectAsync(TenantId tenant, Guid actor, Guid subjectId, RecordRoundSubjectRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.State is not ("Open" or "Passed" or "Failed" or "Skipped") || string.IsNullOrWhiteSpace(request.MeasurementsJson)) throw new ServiceException("service.round_subject_invalid");
        var row = await ReadRoundSubjectAsync(context, tenant.Value, subjectId, token) ?? throw new ServiceException("service.round_subject_not_found");
        await ExecuteAsync(context, "update business_service.maintenance_round_subject set state=$3,result=$4,measurements=$5::jsonb,exceptions=$6,follow_up=$7,recorded_by=$8,recorded_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, subjectId, request.State, Db(request.Result), request.MeasurementsJson, Db(request.Exceptions), Db(request.FollowUp), actor);
        return await ReadRoundSubjectAsync(context, tenant.Value, subjectId, token) ?? throw new ServiceException("service.round_subject_not_found");
    }, ct);

    public Task<SupplierRecoveryView> CreateSupplierRecoveryAsync(TenantId tenant, Guid actor, CreateSupplierRecoveryRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Amount <= 0 || request.SupplierPartyId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceReference)) throw new ServiceException("service.supplier_recovery_invalid");
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"supplier-recovery:{request.WorkOrderId:D}:{request.SourceReference.Trim()}" : request.IdempotencyKey.Trim();
        var existing = await ScalarAsync<Guid?>(context, "select id from business_service.service_supplier_recovery where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, key);
        if (existing is { } prior) return await ReadSupplierRecoveryAsync(context, tenant.Value, prior, token) ?? throw new ServiceException("service.supplier_recovery_not_found");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_service.service_supplier_recovery(tenant_id,id,work_order_id,supplier_party_id,source_reference,amount,currency_code,reason,idempotency_key,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, request.WorkOrderId, request.SupplierPartyId, request.SourceReference.Trim(), request.Amount, request.CurrencyCode, Db(request.Reason), key, actor);
        return await ReadSupplierRecoveryAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.supplier_recovery_not_found");
    }, ct);

    public Task<SupplierRecoveryView> TransitionSupplierRecoveryAsync(TenantId tenant, Guid actor, Guid id, string state, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (state is not ("Requested" or "Recovered" or "Rejected" or "Cancelled")) throw new ServiceException("service.supplier_recovery_state_invalid");
        if (await ExecuteAsync(context, "update business_service.service_supplier_recovery set state=$3 where tenant_id=$1 and id=$2 and state in ('Draft','Requested')", token, tenant.Value, id, state) != 1) throw new ServiceException("service.supplier_recovery_not_found");
        return await ReadSupplierRecoveryAsync(context, tenant.Value, id, token) ?? throw new ServiceException("service.supplier_recovery_not_found");
    }, ct);

    public Task<PagedResult<FixedAssetServiceHistoryView>> ListFixedAssetHistoryAsync(TenantId tenant, Guid fixedAssetId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_fixed_asset_history where tenant_id=$1 and fixed_asset_id=$2", token, tenant.Value, fixedAssetId), CultureInfo.InvariantCulture);
        var rows = new List<FixedAssetServiceHistoryView>();
        await using var command = Command(context, "select id,fixed_asset_id,work_order_id,event_type,cost_amount,currency_code,snapshot::text,occurred_at from business_service.service_fixed_asset_history where tenant_id=$1 and fixed_asset_id=$2 order by occurred_at desc,id desc limit $3 offset $4", tenant.Value, fixedAssetId, pageSize + 1, offset);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1); return new PagedResult<FixedAssetServiceHistoryView>(rows, page, pageSize, hasNext, total);
    }, ct);

    public Task<ServiceReportView> SaveReportAsync(TenantId tenant, Guid actor, Guid workOrderId, CreateReportRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (!await ExistsAsync(context, "business_service.work_order", tenant.Value, workOrderId, token)) throw new ServiceException("service.work_order_not_found");
        if (request.SignedFileId is { } signedFileId)
        {
            var verified = fileReferences is null
                ? null
                : await fileReferences.VerifyReadyReferenceAsync(tenant, signedFileId, "service", "work_order", workOrderId, token);
            if (verified is null || verified.Classification is "medical" or "restricted") throw new ServiceException("service.report_file_reference_invalid");
        }
        var currentState = await ScalarAsync<string?>(context, "select state from business_service.service_report where tenant_id=$1 and work_order_id=$2", token, tenant.Value, workOrderId);
        if (currentState == "Accepted") throw new ServiceException("service.report_immutable");
        var report = await ScalarAsync<Guid?>(context, "select id from business_service.service_report where tenant_id=$1 and work_order_id=$2", token, tenant.Value, workOrderId) ?? Guid.CreateVersion7();
        var version = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_service.service_report_version where tenant_id=$1 and report_id=$2", token, tenant.Value, report), CultureInfo.InvariantCulture);
        await ExecuteAsync(context, "insert into business_service.service_report(tenant_id,id,work_order_id,current_version,state) values($1,$2,$3,$4,'Draft') on conflict(tenant_id,work_order_id) do update set current_version=excluded.current_version,state='Draft'", token, tenant.Value, report, workOrderId, version);
        await ExecuteAsync(context, "insert into business_service.service_report_version(tenant_id,id,report_id,version_no,content,signed_file_id,created_by) values($1,$2,$3,$4,$5::jsonb,$6,$7)", token, tenant.Value, Guid.CreateVersion7(), report, version, request.ContentJson, Db(request.SignedFileId), actor);
        return await ReadReportAsync(context, tenant.Value, report, version, token) ?? throw new ServiceException("service.report_not_found");
    }, ct);

    public Task<ServiceReportView> AcceptReportAsync(TenantId tenant, Guid actor, Guid reportId, int version, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (version < 1 || await ScalarAsync<Guid?>(context, "select id from business_service.service_report_version where tenant_id=$1 and report_id=$2 and version_no=$3", token, tenant.Value, reportId, version) is null) throw new ServiceException("service.report_version_not_found");
        var workOrderId = await ScalarAsync<Guid?>(context, "select work_order_id from business_service.service_report where tenant_id=$1 and id=$2", token, tenant.Value, reportId) ?? throw new ServiceException("service.report_not_found");
        var signedFileId = await ScalarAsync<Guid?>(context, "select signed_file_id from business_service.service_report_version where tenant_id=$1 and report_id=$2 and version_no=$3", token, tenant.Value, reportId, version);
        if (signedFileId is { } fileId)
        {
            var verified = fileReferences is null
                ? null
                : await fileReferences.VerifyReadyReferenceAsync(tenant, fileId, "service", "work_order", workOrderId, token);
            if (verified is null || verified.Classification is "medical" or "restricted") throw new ServiceException("service.report_file_reference_invalid");
        }
        if (await ExecuteAsync(context, "update business_service.service_report set state='Accepted' where tenant_id=$1 and id=$2 and state='Draft' and current_version=$3", token, tenant.Value, reportId, version) != 1) throw new ServiceException("service.report_immutable");
        await ExecuteAsync(context, "update business_service.service_report_version set accepted_by=$3,accepted_at=now() where tenant_id=$1 and report_id=$2 and version_no=$4 and accepted_at is null", token, tenant.Value, reportId, actor, version);
        return await ReadReportAsync(context, tenant.Value, reportId, version, token) ?? throw new ServiceException("service.report_not_found");
    }, ct);

    public Task<ServiceRequestView?> GetRequestAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadRequestAsync(context, tenant.Value, id, token), ct);
    public Task<InstalledBaseView?> GetInstalledBaseItemAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadInstalledBaseAsync(context, tenant.Value, id, token), ct);
    public Task<IReadOnlyList<ComponentEventView>> ListComponentEventsAsync(TenantId tenant, Guid itemId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<ComponentEventView>();
        await using var cmd = Command(context, "select id,item_id,component_item_id,event_type,occurred_at,replaced_component_id,work_order_id,notes from business_service.installed_base_component_event where tenant_id=$1 and item_id=$2 order by occurred_at desc", tenant.Value, itemId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetString(7)));
        return (IReadOnlyList<ComponentEventView>)result;
    }, ct);
    public Task<TemplateView?> GetTemplateAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var cmd = Command(context, "select t.id,t.legal_entity_id,t.code,t.name,coalesce(v.version_no,1),v.expected_duration_hours,coalesce(v.definition::text,'{}'),t.state from business_service.service_work_template t left join lateral (select version_no,expected_duration_hours,definition from business_service.service_work_template_version where tenant_id=t.tenant_id and template_id=t.id order by version_no desc limit 1) v on true where t.tenant_id=$1 and t.id=$2", tenant.Value, id);
        await using var r = await cmd.ExecuteReaderAsync(token);
        return await r.ReadAsync(token) ? ReadTemplate(r) : null;
    }, ct);
    public Task<IReadOnlyList<ChecklistAnswerView>> ListChecklistAnswersAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<ChecklistAnswerView>();
        await using var cmd = Command(context, "select id,work_order_id,question_key,question_type,answer_json::text,required,answered_at from business_service.work_checklist_answer where tenant_id=$1 and work_order_id=$2 order by question_key", tenant.Value, workOrderId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetBoolean(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6)));
        return (IReadOnlyList<ChecklistAnswerView>)result;
    }, ct);
    public Task<IReadOnlyList<DiagnosisView>> ListDiagnosesAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<DiagnosisView>();
        await using var cmd = Command(context, "select id,work_order_id,reported_symptom,diagnosis,failure_mode_code,root_cause_code,resolution_code,notes,recorded_by,recorded_at from business_service.work_diagnosis where tenant_id=$1 and work_order_id=$2 order by recorded_at desc", tenant.Value, workOrderId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetGuid(8), r.GetFieldValue<DateTimeOffset>(9)));
        return (IReadOnlyList<DiagnosisView>)result;
    }, ct);
    public Task<IReadOnlyList<PartRequirementView>> ListPartsAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<PartRequirementView>();
        await using var cmd = Command(context, "select id,work_order_id,product_id,quantity,uom_code,state,reserved_quantity,issued_quantity,consumed_quantity,returned_quantity from business_service.work_part_requirement where tenant_id=$1 and work_order_id=$2 order by id", tenant.Value, workOrderId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9)));
        return (IReadOnlyList<PartRequirementView>)result;
    }, ct);
    public Task<IReadOnlyList<LaborView>> ListLaborAsync(TenantId tenant, Guid? workOrderId, Guid? visitId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<LaborView>();
        await using var cmd = Command(context, "select l.id,l.visit_id,l.employee_id,l.started_at,l.ended_at,l.time_category,extract(epoch from (l.ended_at-l.started_at))/3600,l.cost_amount,l.currency_code,l.state from business_service.service_labor_entry l join business_service.service_visit v on v.tenant_id=l.tenant_id and v.id=l.visit_id where l.tenant_id=$1 and ($2::uuid is null or v.work_order_id=$2) and ($3::uuid is null or l.visit_id=$3) order by l.started_at desc", tenant.Value, Db(workOrderId), Db(visitId));
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetDecimal(6), r.IsDBNull(7) ? null : r.GetDecimal(7), r.GetString(8), r.GetString(9)));
        return (IReadOnlyList<LaborView>)result;
    }, ct);
    public Task<IReadOnlyList<ServiceReportView>> ListReportsAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<ServiceReportView>();
        await using var cmd = Command(context, "select r.id,r.work_order_id,v.version_no,r.state,v.content::text,v.created_at,v.signed_file_id from business_service.service_report r join business_service.service_report_version v on v.tenant_id=r.tenant_id and v.report_id=r.id where r.tenant_id=$1 and r.work_order_id=$2 order by v.version_no desc", tenant.Value, workOrderId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetGuid(6)));
        return (IReadOnlyList<ServiceReportView>)result;
    }, ct);
    public Task<IReadOnlyList<SlaClockView>> ListSlaClocksAsync(TenantId tenant, Guid workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<SlaClockView>();
        await using var cmd = Command(context, "select id,work_order_id,clock_type,started_at,due_at,state,paused_minutes from business_service.sla_clock where tenant_id=$1 and work_order_id=$2 order by started_at", tenant.Value, workOrderId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetInt32(6)));
        return (IReadOnlyList<SlaClockView>)result;
    }, ct);
    public Task<IReadOnlyList<SupplierRecoveryView>> ListSupplierRecoveriesAsync(TenantId tenant, Guid? workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<SupplierRecoveryView>();
        await using var cmd = Command(context, "select id,work_order_id,supplier_party_id,source_reference,amount,currency_code,state,reason from business_service.service_supplier_recovery where tenant_id=$1 and ($2::uuid is null or work_order_id=$2) order by id desc", tenant.Value, Db(workOrderId));
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)));
        return (IReadOnlyList<SupplierRecoveryView>)result;
    }, ct);
    public Task<IReadOnlyList<DowntimeView>> ListDowntimeAsync(TenantId tenant, Guid? subjectId, Guid? workOrderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<DowntimeView>();
        await using var cmd = Command(context, "select id,subject_id,work_order_id,downtime_type,started_at,ended_at,reason from business_service.asset_downtime where tenant_id=$1 and ($2::uuid is null or subject_id=$2) and ($3::uuid is null or work_order_id=$3) order by started_at desc", tenant.Value, Db(subjectId), Db(workOrderId));
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetString(6)));
        return (IReadOnlyList<DowntimeView>)result;
    }, ct);
    public Task<VisitView?> GetVisitAsync(TenantId tenant, Guid visitId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadVisitAsync(context, tenant.Value, visitId, token), ct);
    public Task<IReadOnlyList<AssignmentView>> ListAssignmentsAsync(TenantId tenant, Guid visitId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<AssignmentView>();
        await using var cmd = Command(context, "select id,visit_id,employee_id,user_id,resource_type,display_name,state from business_service.work_assignment where tenant_id=$1 and visit_id=$2 order by assigned_at", tenant.Value, visitId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6)));
        return (IReadOnlyList<AssignmentView>)result;
    }, ct);
    public Task<MaintenancePlanView?> GetPlanAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadPlanAsync(context, tenant.Value, id, token), ct);
    public Task<MaintenanceDueView?> GetDueAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadDueAsync(context, tenant.Value, id, token), ct);
    public Task<PagedResult<MaintenanceRoundView>> ListRoundsAsync(TenantId tenant, Guid? legalEntityId, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.maintenance_round where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3)", token, tenant.Value, Db(legalEntityId), Db(state)), CultureInfo.InvariantCulture);
        var rows = new List<MaintenanceRoundView>();
        await using var cmd = Command(context, "select r.id,r.legal_entity_id,r.code,r.name,r.scheduled_date,r.state,r.row_version,(select count(*) from business_service.maintenance_round_subject s where s.tenant_id=r.tenant_id and s.round_id=r.id) from business_service.maintenance_round r where r.tenant_id=$1 and ($2::uuid is null or r.legal_entity_id=$2) and ($3::text is null or r.state=$3) order by r.scheduled_date desc,r.id desc limit $4 offset $5", tenant.Value, Db(legalEntityId), Db(state), pageSize + 1, offset);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) rows.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetString(5), r.GetInt64(6), r.GetInt32(7)));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1);
        return new PagedResult<MaintenanceRoundView>(rows, page, pageSize, hasNext, total);
    }, ct);
    public Task<MaintenanceRoundView?> GetRoundAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadRoundAsync(context, tenant.Value, id, token), ct);
    public Task<IReadOnlyList<RoundSubjectView>> ListRoundSubjectsAsync(TenantId tenant, Guid roundId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<RoundSubjectView>();
        await using var cmd = Command(context, "select id,round_id,subject_type,subject_id,sequence_no,state,result,measurements::text,exceptions,follow_up from business_service.maintenance_round_subject where tenant_id=$1 and round_id=$2 order by sequence_no,id", tenant.Value, roundId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetGuid(3), r.GetInt32(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
        return (IReadOnlyList<RoundSubjectView>)result;
    }, ct);
    public Task<PagedResult<ServiceContractView>> ListContractsAsync(TenantId tenant, Guid? legalEntityId, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_contract where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3)", token, tenant.Value, Db(legalEntityId), Db(state)), CultureInfo.InvariantCulture);
        var rows = new List<ServiceContractView>();
        await using var cmd = Command(context, "select id,legal_entity_id,contract_holder_party_id,bill_to_party_id,start_date,end_date,contract_type,currency_code,value,state,scope::text from business_service.service_contract where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::text is null or state=$3) order by start_date desc,id desc limit $4 offset $5", tenant.Value, Db(legalEntityId), Db(state), pageSize + 1, offset);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) rows.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateOnly>(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10)));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1);
        return new PagedResult<ServiceContractView>(rows, page, pageSize, hasNext, total);
    }, ct);
    public Task<ServiceContractView?> GetContractAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadContractAsync(context, tenant.Value, id, token), ct);
    public Task<PagedResult<WarrantyView>> ListWarrantiesAsync(TenantId tenant, Guid? legalEntityId, Guid? subjectId, string? state, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.warranty where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::uuid is null or subject_id=$3) and ($4::text is null or state=$4)", token, tenant.Value, Db(legalEntityId), Db(subjectId), Db(state)), CultureInfo.InvariantCulture);
        var rows = new List<WarrantyView>();
        await using var cmd = Command(context, "select id,legal_entity_id,warranty_type,provider_party_id,customer_party_id,subject_id,start_date,end_date,state,scope::text from business_service.warranty where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) and ($3::uuid is null or subject_id=$3) and ($4::text is null or state=$4) order by start_date desc,id desc limit $5 offset $6", tenant.Value, Db(legalEntityId), Db(subjectId), Db(state), pageSize + 1, offset);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) rows.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.GetFieldValue<DateOnly>(6), r.GetFieldValue<DateOnly>(7), r.GetString(8), r.GetString(9)));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1);
        return new PagedResult<WarrantyView>(rows, page, pageSize, hasNext, total);
    }, ct);
    public Task<WarrantyView?> GetWarrantyAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadWarrantyAsync(context, tenant.Value, id, token), ct);
    public Task<PagedResult<SlaProfileView>> ListSlaProfilesAsync(TenantId tenant, Guid? legalEntityId, int page, int pageSize, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        (page, pageSize) = Page(page, pageSize); var offset = (page - 1) * pageSize;
        var total = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.sla_profile where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2)", token, tenant.Value, Db(legalEntityId)), CultureInfo.InvariantCulture);
        var rows = new List<SlaProfileView>();
        await using var cmd = Command(context, "select id,legal_entity_id,code,response_minutes,arrival_minutes,resolution_minutes,state,working_weekdays,working_start::text,working_end::text,time_zone_id from business_service.sla_profile where tenant_id=$1 and ($2::uuid is null or legal_entity_id=$2) order by code limit $3 offset $4", tenant.Value, Db(legalEntityId), pageSize + 1, offset);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) rows.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.GetInt32(5), r.GetString(6), r.GetInt16(7), r.GetString(8), r.GetString(9), r.GetString(10)));
        var hasNext = rows.Count > pageSize; if (hasNext) rows.RemoveAt(rows.Count - 1);
        return new PagedResult<SlaProfileView>(rows, page, pageSize, hasNext, total);
    }, ct);
    public Task<IReadOnlyList<MeterReadingView>> ListMeterReadingsAsync(TenantId tenant, Guid subjectId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<MeterReadingView>();
        await using var cmd = Command(context, "select id,subject_id,meter_code,value,uom,reading_at,source,is_reset from business_service.meter_reading where tenant_id=$1 and subject_id=$2 order by reading_at desc", tenant.Value, subjectId);
        await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) result.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.GetString(6), r.GetBoolean(7)));
        return (IReadOnlyList<MeterReadingView>)result;
    }, ct);

    public Task<ServiceKpiView> GetKpiAsync(TenantId tenant, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var openRequests = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_request where tenant_id=$1 and state not in ('Closed','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture);
        var openWorkOrders = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_order where tenant_id=$1 and state not in ('Closed','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture);
        var visitsToday = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.service_visit where tenant_id=$1 and planned_start::date=current_date and state not in ('Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture);
        var openCharges = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(customer_amount),0) from business_service.service_charge where tenant_id=$1 and state not in ('Invoiced','WrittenOff','Cancelled')", token, tenant.Value), CultureInfo.InvariantCulture);
        var downtimeHours = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(extract(epoch from (coalesce(ended_at,now())-started_at))/3600),0) from business_service.asset_downtime where tenant_id=$1", token, tenant.Value), CultureInfo.InvariantCulture);
        var failureCount = Convert.ToInt32(await ScalarAsync(context, "select count(*) from business_service.work_order where tenant_id=$1 and work_type='Corrective' and state <> 'Cancelled'", token, tenant.Value), CultureInfo.InvariantCulture);
        var mttrHours = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(avg(extract(epoch from (l.ended_at-l.started_at))/3600),0) from business_service.service_labor_entry l join business_service.service_visit v on v.tenant_id=l.tenant_id and v.id=l.visit_id join business_service.work_order w on w.tenant_id=v.tenant_id and w.id=v.work_order_id where l.tenant_id=$1 and w.work_type='Corrective' and l.time_category in ('OnSiteWork','RemoteWork')", token, tenant.Value), CultureInfo.InvariantCulture);
        var observedHours = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(extract(epoch from (current_timestamp-min(started_at)))/3600,0) from business_service.asset_downtime where tenant_id=$1", token, tenant.Value), CultureInfo.InvariantCulture);
        var mtbfHours = failureCount == 0 ? 0m : Math.Max(0m, (observedHours - downtimeHours) / failureCount);
        var availability = observedHours <= 0m ? 100m : Math.Clamp((observedHours - downtimeHours) / observedHours * 100m, 0m, 100m);
        return new ServiceKpiView(openRequests, openWorkOrders, visitsToday, openCharges, downtimeHours, failureCount, mtbfHours, mttrHours, availability);
    }, ct);

    public async Task<ServiceChargeView> RequestInvoiceAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid chargeId, CancellationToken ct)
    {
        var pending = await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var charge = await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
            var order = await ReadWorkOrderAsync(context, tenant.Value, charge.WorkOrderId, token) ?? throw new ServiceException("service.work_order_not_found");
            if (order.BillingOwner == "Projects") throw new ServiceException("service.project_billing_owned_by_projects");
            if (order.CustomerPartyId is null) throw new ServiceException("service.invoice_customer_required");
            if (charge.CustomerAmount <= 0) throw new ServiceException("service.invoice_amount_invalid");
            if (charge.State is "Invoiced" or "WrittenOff" or "Cancelled") throw new ServiceException("service.charge_state_invalid");
            var key = $"service-charge-invoice:{chargeId:D}";
            await ExecuteAsync(context, "update business_service.service_charge set state='Requested',invoice_integration_state='Pending',invoice_request_key=$3,integration_error=null where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, key);
            return (Charge: charge, Order: order, Key: key);
        }, ct);
        if (invoicing is null) throw new ServiceException("service.invoicing_integration_unavailable");
        Guid invoiceId;
        try
        {
            invoiceId = await invoicing.CreateDraftFromSourceAsync(tenant, actor, membership,
                new InvoiceSourceDocumentContract("service", pending.Order.Id, "ServiceWorkOrder", pending.Key, "Receivable", pending.Order.LegalEntityId,
                    null, pending.Order.CustomerPartyId!.Value, null, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow),
                    DateOnly.FromDateTime(DateTime.UtcNow), null, "EGP", "Exclusive", null, null, null,
                    [new InvoiceSourceLineContract(null, null, "EA", pending.Charge.Description, "1", pending.Charge.CustomerAmount.ToString(CultureInfo.InvariantCulture),
                        "Agreed service charge", pending.Charge.Category, pending.Charge.Id.ToString("D"))]), correlationId, ct);
            if (invoiceReader is null) throw new ServiceException("service.invoice_reader_unavailable");
            var state = await invoiceReader.GetServiceInvoiceStateAsync(tenant, pending.Order.LegalEntityId, invoiceId, ct);
            if (state is null || state.SourceModule != "service" || state.SourceDocumentId != pending.Order.Id || state.PartyId != pending.Order.CustomerPartyId || state.Direction != "Receivable") throw new ServiceException("service.invoice_source_mismatch");
        }
        catch (ServiceException)
        {
            await coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.service_charge set invoice_integration_state='Retryable',integration_error=$3 where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, "service.invoice_source_mismatch"); return 0; }, ct);
            throw;
        }
        catch (Exception)
        {
            await coordinator.ExecuteAsync(tenant, async (context, token) => { await ExecuteAsync(context, "update business_service.service_charge set invoice_integration_state='Retryable',integration_error=$3 where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, "service.invoicing_request_retryable"); return 0; }, ct);
            throw new ServiceException("service.invoicing_request_retryable");
        }
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            await ExecuteAsync(context, "update business_service.service_charge set invoice_id=$3,invoice_integration_state='DraftCreated' where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, invoiceId);
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ServiceInvoiceRequestedV1", JsonSerializer.Serialize(new { chargeId, workOrderId = pending.Order.Id, invoiceId, amount = pending.Charge.CustomerAmount }), tenant, DateTimeOffset.UtcNow, correlationId, chargeId, "service"));
            return await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
        }, ct);
    }

    public async Task<ServiceChargeView> AllocateInvoiceAsync(TenantId tenant, Guid actor, Guid chargeId, InvoiceAllocationRequest request, CancellationToken ct)
    {
        if (request.AllocatedAmount <= 0) throw new ServiceException("service.invoice_allocation_invalid");
        var current = await coordinator.ExecuteAsync(tenant, async (context, token) => (Charge: await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found"), Order: await ReadWorkOrderAsync(context, tenant.Value, (await ReadChargeAsync(context, tenant.Value, chargeId, token))!.WorkOrderId, token) ?? throw new ServiceException("service.work_order_not_found")), ct);
        if (invoiceReader is null) throw new ServiceException("service.invoice_reader_unavailable");
        var invoice = await invoiceReader.GetServiceInvoiceStateAsync(tenant, current.Order.LegalEntityId, request.InvoiceId, ct);
        if (invoice is null || invoice.SourceModule != "service" || invoice.SourceDocumentId != current.Order.Id || invoice.PartyId != current.Order.CustomerPartyId || invoice.CurrencyCode != current.Charge.CurrencyCode || invoice.Direction != "Receivable") throw new ServiceException("service.invoice_source_mismatch");
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var lockedAmount = Convert.ToDecimal(await ScalarAsync(context, "select customer_amount from business_service.service_charge where tenant_id=$1 and id=$2 for update", token, tenant.Value, chargeId), CultureInfo.InvariantCulture);
            var already = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(allocated_amount),0) from business_service.service_invoice_allocation where tenant_id=$1 and charge_id=$2 and invoice_id<>$3", token, tenant.Value, chargeId, request.InvoiceId), CultureInfo.InvariantCulture);
            if (already + request.AllocatedAmount > lockedAmount) throw new ServiceException("service.invoice_allocation_exceeds_charge");
            await ExecuteAsync(context, "insert into business_service.service_invoice_allocation(tenant_id,id,charge_id,invoice_id,invoice_line_id,allocated_amount) values($1,$2,$3,$4,$5,$6) on conflict(tenant_id,charge_id,invoice_id) do update set invoice_line_id=excluded.invoice_line_id,allocated_amount=excluded.allocated_amount", token, tenant.Value, Guid.CreateVersion7(), chargeId, request.InvoiceId, Db(request.InvoiceLineId), request.AllocatedAmount);
            var total = already + request.AllocatedAmount;
            await ExecuteAsync(context, "update business_service.service_charge set state=case when $3>=customer_amount then 'Invoiced' else 'Requested' end,invoice_id=$4,invoice_integration_state='Allocated' where tenant_id=$1 and id=$2", token, tenant.Value, chargeId, total, request.InvoiceId);
            return await ReadChargeAsync(context, tenant.Value, chargeId, token) ?? throw new ServiceException("service.charge_not_found");
        }, ct);
    }

    private static void ValidateChecklistAnswer(ChecklistAnswerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionKey) || request.QuestionType is not ("Text" or "Boolean" or "Number" or "Choice" or "Measurement" or "Photo")) throw new ServiceException("service.checklist_invalid");
        if (string.IsNullOrWhiteSpace(request.AnswerJson))
        {
            if (request.Required) throw new ServiceException("service.checklist_required");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(request.AnswerJson);
            var value = document.RootElement;
            var valid = request.QuestionType switch
            {
                "Text" or "Choice" => value.ValueKind == JsonValueKind.String,
                "Boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "Number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
                "Measurement" => value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("value", out var measurementValue)
                    && measurementValue.ValueKind == JsonValueKind.Number
                    && measurementValue.TryGetDecimal(out _)
                    && value.TryGetProperty("uom", out var measurementUom)
                    && measurementUom.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(measurementUom.GetString()),
                "Photo" => value.ValueKind is JsonValueKind.String or JsonValueKind.Object,
                _ => false
            };
            if (!valid) throw new ServiceException(request.QuestionType == "Measurement" ? "service.measurement_out_of_range" : "service.checklist_invalid");
        }
        catch (JsonException) { throw new ServiceException("service.checklist_invalid"); }
    }

    private static void ValidateContext(string context, Guid? customer, Guid? installedBase) { try { ServiceRules.ValidateContext(context, customer, installedBase); } catch (ArgumentException ex) { throw new ServiceException(ex.ParamName == "customerPartyId" ? "service.customer_required" : ex.ParamName == "installedBaseId" ? "service.subject_invalid" : "service.context_invalid"); } }
    private static string Number(string prefix, Guid id) => $"{prefix}-{id.ToString("N", CultureInfo.InvariantCulture)[..12].ToUpperInvariant()}";
    private static (int Page, int Size) Page(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 200));
    private static object Db(object? value) => value ?? DBNull.Value;
    private static bool ScopeCovers(string? scopeJson, string category)
    {
        if (string.IsNullOrWhiteSpace(scopeJson)) return true;
        try
        {
            using var document = JsonDocument.Parse(scopeJson);
            if (!document.RootElement.TryGetProperty("coveredCategories", out var categories) || categories.ValueKind != JsonValueKind.Array) return true;
            return categories.EnumerateArray().Any(item => string.Equals(item.GetString(), category, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }
    private static decimal ScopeLimit(string? scopeJson, string category, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(scopeJson)) return amount;
        try
        {
            using var document = JsonDocument.Parse(scopeJson);
            var property = category.Contains("Visit", StringComparison.OrdinalIgnoreCase) ? "includedVisits" : category.Equals("Parts", StringComparison.OrdinalIgnoreCase) ? "partsCap" : category.Equals("Labor", StringComparison.OrdinalIgnoreCase) ? "includedLaborAmount" : "includedAmount";
            return document.RootElement.TryGetProperty(property, out var value) && value.TryGetDecimal(out var limit) ? Math.Max(0m, Math.Min(amount, limit)) : amount;
        }
        catch (JsonException) { return 0m; }
    }
    private static DateTimeOffset BusinessDue(DateTimeOffset start, int minutes, SlaProfileView profile)
    {
        var cursor = start; var remaining = minutes; var open = TimeOnly.Parse(profile.WorkingStart, CultureInfo.InvariantCulture); var close = TimeOnly.Parse(profile.WorkingEnd, CultureInfo.InvariantCulture);
        while (remaining > 0)
        {
            var local = TimeZoneInfo.ConvertTime(cursor, TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId));
            var dayBit = 1 << (int)local.DayOfWeek;
            if ((profile.WorkingWeekdays & dayBit) == 0 || local.TimeOfDay >= close.ToTimeSpan()) { cursor = local.Date.AddDays(1).Add(open.ToTimeSpan()).ToUniversalTime(); continue; }
            if (local.TimeOfDay < open.ToTimeSpan()) cursor = local.Date.Add(open.ToTimeSpan()).ToUniversalTime();
            var available = (int)(close.ToTimeSpan() - TimeZoneInfo.ConvertTime(cursor, TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId)).TimeOfDay).TotalMinutes;
            var take = Math.Min(remaining, Math.Max(1, available)); cursor = cursor.AddMinutes(take); remaining -= take;
        }
        return cursor;
    }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); return command; }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteNonQueryAsync(ct); }
    private static async Task<object?> ScalarAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { await using var command = Command(context, sql, values); var value = await command.ExecuteScalarAsync(ct); return value is DBNull ? null : value; }
    private static async Task<T> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken ct, params object?[] values) { var value = await ScalarAsync(context, sql, ct, values); if (value is null) return default!; return (T)value; }
    private static async Task<DateTimeOffset?> ScalarDateTimeOffsetAsync(ITransactionContext context, string sql, CancellationToken ct, params object?[] values)
    {
        var value = await ScalarAsync(context, sql, ct, values);
        return value switch
        {
            null => null,
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidCastException($"Expected a timestamp value, received {value.GetType().Name}.")
        };
    }
    private static async Task<bool> ExistsAsync(ITransactionContext context, string table, Guid tenant, Guid id, CancellationToken ct) => Convert.ToBoolean(await ScalarAsync<bool>(context, $"select exists(select 1 from {table} where tenant_id=$1 and id=$2)", ct, tenant, id), CultureInfo.InvariantCulture);
    private static async Task<bool> ExistsAsync(ITransactionContext context, string table, Guid tenant, Guid id, int? version, CancellationToken ct) => Convert.ToBoolean(await ScalarAsync<bool>(context, $"select exists(select 1 from {table} where tenant_id=$1 and template_id=$2 and version_no=$3)", ct, tenant, id, version ?? 0), CultureInfo.InvariantCulture);
    private static async Task<bool> HasDescendantAsync(ITransactionContext context, Guid tenant, Guid rootId, Guid candidateId, CancellationToken ct) => Convert.ToBoolean(await ScalarAsync<bool>(context, "with recursive tree as (select id,parent_id from business_service.installed_base_item where tenant_id=$1 and id=$2 union all select i.id,i.parent_id from business_service.installed_base_item i join tree t on i.parent_id=t.id where i.tenant_id=$1) select exists(select 1 from tree where id=$3)", ct, tenant, rootId, candidateId), CultureInfo.InvariantCulture);
    private static async Task AuditAsync(ITransactionContext context, TenantId tenant, string type, Guid id, Guid actor, string action, object? before, object? after, CancellationToken ct) => await ExecuteAsync(context, "insert into business_service.service_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,before_json,after_json) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb)", ct, tenant.Value, Guid.CreateVersion7(), type, id, action, actor, before is null ? DBNull.Value : JsonSerializer.Serialize(before), after is null ? DBNull.Value : JsonSerializer.Serialize(after));
    private static async Task<ServiceRequestView?> ReadRequestAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,request_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,state,priority,requested_at,row_version from business_service.service_request where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadRequest(r) : null; }
    private static ServiceRequestView ReadRequest(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetString(9), r.GetString(10), r.GetFieldValue<DateTimeOffset>(11), r.GetInt64(12));
    private static async Task<WorkOrderView?> ReadWorkOrderAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,work_order_number,context,title,description,customer_party_id,installed_base_id,fixed_asset_id,project_id,billing_owner,work_type,priority,state,planned_start,planned_end,technical_completed_at,accepted_at,closed_at,row_version,template_id,template_version from business_service.work_order where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadWorkOrder(r) : null; }
    private static WorkOrderView ReadWorkOrder(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.IsDBNull(9) ? null : r.GetGuid(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetString(13), r.IsDBNull(14) ? null : r.GetFieldValue<DateTimeOffset>(14), r.IsDBNull(15) ? null : r.GetFieldValue<DateTimeOffset>(15), r.IsDBNull(16) ? null : r.GetFieldValue<DateTimeOffset>(16), r.IsDBNull(17) ? null : r.GetFieldValue<DateTimeOffset>(17), r.IsDBNull(18) ? null : r.GetFieldValue<DateTimeOffset>(18), r.GetInt64(19), r.IsDBNull(20) ? null : r.GetGuid(20), r.IsDBNull(21) ? null : r.GetInt32(21));
    private static async Task<VisitView?> ReadVisitAsync(ITransactionContext c, Guid tenant, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,planned_start,planned_end,location_type,state,site_instructions,actual_start,actual_end,row_version from business_service.service_visit where tenant_id=$1 and id=$2", tenant, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadVisit(r) : null; }
    private static VisitView ReadVisit(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateTimeOffset>(2), r.GetFieldValue<DateTimeOffset>(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8), r.GetInt64(9));
    private static async Task<AssignmentView?> ReadAssignmentAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,visit_id,employee_id,user_id,resource_type,display_name,state from business_service.work_assignment where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetString(6)) : null; }
    private static async Task<ResourceRequirementView?> ReadResourceRequirementAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,skill_code,quantity,required_from,required_to,state from business_service.service_resource_requirement where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadResourceRequirement(r) : null; }
    private static ResourceRequirementView ReadResourceRequirement(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetDecimal(3), r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.GetString(6));
    private static async Task<CrewView?> ReadCrewAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,code,name,active from business_service.service_crew where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetBoolean(4)) : null; }
    private static async Task<SkillView?> ReadSkillAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,code,name,active from business_service.service_skill where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3)) : null; }
    private static async Task<TerritoryView?> ReadTerritoryAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,code,name,active from business_service.service_territory where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3)) : null; }
    private static async Task<ExternalReferenceView?> ReadExternalReferenceAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,entity_id,entity_type,source_module,source_type,source_id,snapshot::text from business_service.service_external_reference where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)) : null; }
    private static InstalledBaseView ReadInstalledBase(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10), r.IsDBNull(11) ? null : r.GetFieldValue<DateOnly>(11), r.GetInt64(12));
    private static async Task<InstalledBaseView?> ReadInstalledBaseAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,name,serial_number,model_number,product_id,customer_party_id,owner_party_id,parent_id,state,site_name,installed_date,row_version from business_service.installed_base_item where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadInstalledBase(r) : null; }
    private static TemplateView ReadTemplate(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetString(6), r.GetString(7));
    private static async Task<ChecklistAnswerView?> ReadChecklistAsync(ITransactionContext c, Guid t, Guid workOrderId, string key, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,question_key,question_type,answer_json::text,required,answered_at from business_service.work_checklist_answer where tenant_id=$1 and work_order_id=$2 and question_key=$3", t, workOrderId, key); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetBoolean(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6)) : null; }
    private static async Task<DiagnosisView?> ReadDiagnosisAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,reported_symptom,diagnosis,failure_mode_code,root_cause_code,resolution_code,notes,recorded_by,recorded_at from business_service.work_diagnosis where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.GetGuid(8), r.GetFieldValue<DateTimeOffset>(9)) : null; }
    private static async Task<ComponentEventView?> ReadComponentEventAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,item_id,component_item_id,event_type,occurred_at,replaced_component_id,work_order_id,notes from business_service.installed_base_component_event where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetString(7)) : null; }
    private static async Task<TemplateView?> ReadTemplateAsync(ITransactionContext c, Guid t, Guid templateId, int version, CancellationToken ct) { await using var cmd = Command(c, "select t.id,t.legal_entity_id,t.code,t.name,v.version_no,v.expected_duration_hours,v.definition::text,t.state from business_service.service_work_template t join business_service.service_work_template_version v on v.tenant_id=t.tenant_id and v.template_id=t.id where t.tenant_id=$1 and t.id=$2 and v.version_no=$3", t, templateId, version); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.IsDBNull(5) ? null : r.GetDecimal(5), r.GetString(6), r.GetString(7)) : null; }
    private static MaintenancePlanView ReadPlan(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetGuid(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateOnly>(6), r.IsDBNull(7) ? null : r.GetInt32(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.IsDBNull(9) ? null : r.GetString(9), r.GetString(10));
    private static async Task<MaintenancePlanView?> ReadPlanAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,name,subject_type,subject_id,trigger_type,start_date,interval_days,meter_threshold,meter_code,state from business_service.maintenance_plan where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadPlan(r) : null; }
    private static async Task<MaintenanceDueView?> ReadDueByKeyAsync(ITransactionContext c, Guid t, string key, CancellationToken ct) { await using var cmd = Command(c, "select id,plan_id,subject_id,due_date,state,work_order_id,reason,row_version from business_service.maintenance_due where tenant_id=$1 and generated_key=$2", t, key); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt64(7)) : null; }
    private static async Task<MaintenanceDueView?> ReadDueAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,plan_id,subject_id,due_date,state,work_order_id,reason,row_version from business_service.maintenance_due where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt64(7)) : null; }
    private static async Task<SlaProfileView?> ReadSlaProfileAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,code,response_minutes,arrival_minutes,resolution_minutes,state,working_weekdays,working_start::text,working_end::text,time_zone_id from business_service.sla_profile where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4), r.GetInt32(5), r.GetString(6), r.GetInt16(7), r.GetString(8), r.GetString(9), r.GetString(10)) : null; }
    private static async Task<SlaClockView?> ReadSlaClockAsync(ITransactionContext c, Guid t, Guid workOrderId, string clockType, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,clock_type,started_at,due_at,state,paused_minutes from business_service.sla_clock where tenant_id=$1 and work_order_id=$2 and clock_type=$3", t, workOrderId, clockType); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetInt32(6)) : null; }
    private static async Task<MeterReadingView?> ReadMeterAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,subject_id,meter_code,value,uom,reading_at,source,is_reset from business_service.meter_reading where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetDecimal(3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5), r.GetString(6), r.GetBoolean(7)) : null; }
    private static async Task<ServiceContractView?> ReadContractAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,contract_holder_party_id,bill_to_party_id,start_date,end_date,contract_type,currency_code,value,state,scope::text from business_service.service_contract where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetFieldValue<DateOnly>(4), r.GetFieldValue<DateOnly>(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetDecimal(8), r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10)) : null; }
    private static async Task<ServiceChargeView?> ReadChargeAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,category,description,cost_amount,covered_amount,customer_amount,currency_code,state,invoice_id,correction_of_id from business_service.service_charge where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadCharge(r) : null; }
    private static ServiceChargeView ReadCharge(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetString(7), r.GetString(8), r.IsDBNull(9) ? null : r.GetGuid(9), r.IsDBNull(10) ? null : r.GetGuid(10));
    private static async Task<PartRequirementView?> ReadPartAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,product_id,quantity,uom_code,state,reserved_quantity,issued_quantity,consumed_quantity,returned_quantity from business_service.work_part_requirement where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9)) : null; }
    private static async Task<PartIntegration?> ReadPartIntegrationAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select legal_entity_id,warehouse_id,location_id,unit_of_measure_id,lot_id,serial_id from business_service.work_part_requirement where tenant_id=$1 and id=$2 and legal_entity_id is not null and warehouse_id is not null and location_id is not null and unit_of_measure_id is not null", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetGuid(5)) : null; }
    private static async Task<PartMovement?> ReadPartMovementAsync(ITransactionContext c, Guid t, string key, CancellationToken ct) { await using var cmd = Command(c, "select id,action,quantity,state,inventory_reservation_id,inventory_operation_id from business_service.work_part_movement where tenant_id=$1 and idempotency_key=$2", t, key); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetString(1), r.GetDecimal(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetGuid(5)) : null; }
    private sealed record PartIntegration(Guid LegalEntityId, Guid WarehouseId, Guid LocationId, Guid UnitOfMeasureId, Guid? LotId, Guid? SerialId);
    private sealed record PartMovement(Guid Id, string Action, decimal Quantity, string State, Guid? OriginalMovementId, Guid? OriginalOperationId);
    private static async Task<WarrantyView?> ReadWarrantyAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,legal_entity_id,warranty_type,provider_party_id,customer_party_id,subject_id,start_date,end_date,state,scope::text from business_service.warranty where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.IsDBNull(4) ? null : r.GetGuid(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.GetFieldValue<DateOnly>(6), r.GetFieldValue<DateOnly>(7), r.GetString(8), r.GetString(9)) : null; }
    private static async Task<LaborView?> ReadLaborAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,visit_id,employee_id,started_at,ended_at,time_category,extract(epoch from (ended_at-started_at))/3600,cost_amount,currency_code,state from business_service.service_labor_entry where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetString(5), r.GetDecimal(6), r.IsDBNull(7) ? null : r.GetDecimal(7), r.GetString(8), r.GetString(9)) : null; }
    private static async Task<OfflineCommandView?> ReadOfflineAsync(ITransactionContext c, Guid t, Guid clientId, CancellationToken ct) { await using var cmd = Command(c, "select id,client_command_id,command_type,state,conflict_code,result::text from business_service.offline_command where tenant_id=$1 and client_command_id=$2", t, clientId); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)) : null; }
    private static async Task<DowntimeView?> ReadDowntimeAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,subject_id,work_order_id,downtime_type,started_at,ended_at,reason from business_service.asset_downtime where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetString(6)) : null; }
    private static async Task<MaintenanceRoundView?> ReadRoundAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select r.id,r.legal_entity_id,r.code,r.name,r.scheduled_date,r.state,r.row_version,(select count(*) from business_service.maintenance_round_subject s where s.tenant_id=r.tenant_id and s.round_id=r.id) from business_service.maintenance_round r where r.tenant_id=$1 and r.id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetFieldValue<DateOnly>(4), r.GetString(5), r.GetInt64(6), r.GetInt32(7)) : null; }
    private static async Task<RoundSubjectView?> ReadRoundSubjectAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,round_id,subject_type,subject_id,sequence_no,state,result,measurements::text,exceptions,follow_up from business_service.maintenance_round_subject where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetGuid(3), r.GetInt32(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)) : null; }
    private static async Task<SupplierRecoveryView?> ReadSupplierRecoveryAsync(ITransactionContext c, Guid t, Guid id, CancellationToken ct) { await using var cmd = Command(c, "select id,work_order_id,supplier_party_id,source_reference,amount,currency_code,state,reason from business_service.service_supplier_recovery where tenant_id=$1 and id=$2", t, id); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetDecimal(4), r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7)) : null; }
    private static async Task<ServiceReportView?> ReadReportAsync(ITransactionContext c, Guid t, Guid reportId, int version, CancellationToken ct) { await using var cmd = Command(c, "select r.id,r.work_order_id,v.version_no, r.state,v.content::text,v.created_at,v.signed_file_id from business_service.service_report r join business_service.service_report_version v on v.tenant_id=r.tenant_id and v.report_id=r.id where r.tenant_id=$1 and r.id=$2 and v.version_no=$3", t, reportId, version); await using var row = await cmd.ExecuteReaderAsync(ct); return await row.ReadAsync(ct) ? new(row.GetGuid(0), row.GetGuid(1), row.GetInt32(2), row.GetString(3), row.GetString(4), row.GetFieldValue<DateTimeOffset>(5), row.IsDBNull(6) ? null : row.GetGuid(6)) : null; }
}
