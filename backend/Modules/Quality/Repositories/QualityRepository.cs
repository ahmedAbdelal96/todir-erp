using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Modules.Quality.DTOs;
using Modules.Quality.Entities;
using Npgsql;
using Shared.Contracts.Quality;
using Shared.Primitives.Identifiers;

namespace Modules.Quality.Repositories;

public sealed class QualityRepository(ITransactionCoordinator coordinator, Shared.Contracts.Quality.IInventoryQualityCapabilities? inventory = null,
    IInventoryQualityDispositionCapabilities? inventoryDisposition = null,
    IPurchasingQualityDispositionCapabilities? purchasingDisposition = null,
    IManufacturingQualityDispositionCapabilities? manufacturingDisposition = null) : IQualityDecisionCapabilities, IQualityReceivingCapabilities
{
    private readonly Shared.Contracts.Quality.IInventoryQualityCapabilities? inventoryCapability = inventory;
    private readonly IInventoryQualityDispositionCapabilities? inventoryDispositionCapability = inventoryDisposition;
    private readonly IPurchasingQualityDispositionCapabilities? purchasingDispositionCapability = purchasingDisposition;
    private readonly IManufacturingQualityDispositionCapabilities? manufacturingDispositionCapability = manufacturingDisposition;
    private static readonly HashSet<string> SupportedIntegrationEvents = new(StringComparer.Ordinal)
    {
        "StockReceivedV1",
        "ProductionOutputReportedV1",
        "ProductionOperationCompletedV1",
        "SalesReturnReceivedV1",
        "CalibrationStatusChangedV1"
    };

    public async Task RecordIntegrationEventAsync(OutboxPublishedMessage message, CancellationToken ct = default)
    {
        if (!SupportedIntegrationEvents.Contains(message.MessageType)) return;

        await coordinator.ExecuteAsync(message.TenantId, async (context, token) =>
        {
            await ExecuteAsync(context, "insert into business_quality.quality_integration_event(tenant_id,message_id,source_module,message_type,status,payload,occurred_at,processed_at) values($1,$2,$3,$4,'Applied',$5::jsonb,$6,now()) on conflict (tenant_id,message_id) do nothing", token, message.TenantId.Value, message.MessageId, message.SourceModule, message.MessageType, message.Payload, message.OccurredAt);
            return true;
        }, ct);
    }

    public async Task ApplyIntegrationConsequencesAsync(OutboxPublishedMessage message, CancellationToken ct = default)
    {
        if (!SupportedIntegrationEvents.Contains(message.MessageType)) return;
        await coordinator.ExecuteAsync(message.TenantId, async (context, token) =>
        {
            if (Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_integration_event where tenant_id=$1 and message_id=$2)", token, message.TenantId.Value, message.MessageId), CultureInfo.InvariantCulture)) return true;
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            var legalEntityId = ReadGuid(root, "legalEntityId");
            var productVariantId = ReadGuid(root, "productVariantId");
            var quantity = root.TryGetProperty("quantity", out var quantityElement) && quantityElement.TryGetDecimal(out var parsedQuantity) ? parsedQuantity : 0m;
            var eventType = message.MessageType switch
            {
                "StockReceivedV1" => "Receiving",
                "ProductionOutputReportedV1" or "ProductionOperationCompletedV1" => "Final",
                "SalesReturnReceivedV1" => "Return",
                _ => null
            };
            var status = "Ignored";
            if (eventType is not null && legalEntityId is { } entity && productVariantId is { } product && quantity > 0m)
            {
                var request = new CreateInspectionRequest(entity, eventType, product, quantity, SourceDocumentId: ReadGuid(root, "operationId") ?? ReadGuid(root, "outputId"), SupplierPartyId: ReadGuid(root, "supplierPartyId"), SourceFingerprint: $"integration:{message.MessageId:D}");
                if (await ResolvePlanVersionAsync(context, message.TenantId.Value, request, token) is { } planVersion)
                {
                    var exists = await ScalarAsync<Guid?>(context, "select id from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2 limit 1", token, message.TenantId.Value, request.SourceFingerprint);
                    if (exists is null) _ = await CreateInspectionInContextAsync(context, message.TenantId, Guid.Empty, request with { PlanVersionId = planVersion }, token);
                    status = "Applied";
                }
            }
            await ExecuteAsync(context, "insert into business_quality.quality_integration_event(tenant_id,message_id,source_module,message_type,status,payload,occurred_at,processed_at) values($1,$2,$3,$4,$5,$6::jsonb,$7,now())", token, message.TenantId.Value, message.MessageId, message.SourceModule, message.MessageType, status, message.Payload, message.OccurredAt);
            return true;
        }, ct);
    }

    private static Guid? ReadGuid(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;

    public Task<QualityPage<QualityPlanListItem>> ListPlansAsync(TenantId tenant, Guid legalEntityId, string? eventType, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<QualityPlanListItem>();
        var boundedSize = Math.Clamp(pageSize ?? 50, 1, 100);
        var decoded = DecodePlanCursor(cursor);
        await using var command = Command(context, "select id,code,name,event_type,mandatory,status,row_version from business_quality.quality_plan where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or event_type=$3) and ($4::text is null or code>$4 or (code=$4 and id>$5::uuid)) order by code,id limit $6", tenant.Value, legalEntityId, Db(eventType), Db(decoded?.Code), Db(decoded?.Id), boundedSize + 1);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token) && result.Count <= boundedSize) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetString(5), reader.GetInt64(6)));
        var hasMore = result.Count > boundedSize;
        if (hasMore) result.RemoveAt(result.Count - 1);
        return new QualityPage<QualityPlanListItem>(result, hasMore ? EncodePlanCursor(result[^1]) : null);
    }, ct);

    public Task<QualityPage<QualityRestrictionListItem>> ListRestrictionsAsync(TenantId tenant, Guid legalEntityId, string? status, string? restrictionType, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityRestrictionListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,restriction_type,status,reason_code,product_variant_id,lot_id,quantity,released_quantity,owner_execution_status,created_at from business_quality.quality_restriction where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or status=$3) and ($4::text is null or restriction_type=$4) and ($5::text is null or reason_code ilike '%'||$5||'%') and ($6::timestamptz is null or (created_at,id)<($6::timestamptz,$7::uuid)) order by created_at desc,id desc limit $8", tenant.Value, legalEntityId, Db(status), Db(restrictionType), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9), Timestamp(reader[10])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityRestrictionListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualitySamplingPlanListItem>> ListSamplingPlansAsync(TenantId tenant, Guid legalEntityId, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualitySamplingPlanListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodePlanCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,code,name,method,fixed_quantity,percentage from business_quality.sampling_plan where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or code ilike '%'||$3||'%' or name ilike '%'||$3||'%') and ($4::text is null or code>$4 or (code=$4 and id>$5::uuid)) order by code,id limit $6", tenant.Value, legalEntityId, Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.Code), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualitySamplingPlanListItem>(items, more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{items[^1].Code}\n{items[^1].Id:D}")) : null);
    }, ct);

    public Task<QualityPage<QualityDispositionListItem>> ListDispositionsAsync(TenantId tenant, Guid legalEntityId, string? state, string? decision, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityDispositionListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,inspection_id,decision,state,quantity,reason,owner_execution_status,created_at from business_quality.quality_disposition where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or decision=$4) and ($5::text is null or reason ilike '%'||$5||'%') and ($6::timestamptz is null or (created_at,id)<($6::timestamptz,$7::uuid)) order by created_at desc,id desc limit $8", tenant.Value, legalEntityId, Db(state), Db(decision), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), Timestamp(reader[8])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityDispositionListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualityIssueListItem>> ListIssuesAsync(TenantId tenant, Guid legalEntityId, string? state, string? severity, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityIssueListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,issue_type,title,severity,state,inspection_id,created_at from business_quality.quality_issue where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or severity=$4) and ($5::text is null or title ilike '%'||$5||'%') and ($6::timestamptz is null or (created_at,id)<($6::timestamptz,$7::uuid)) order by created_at desc,id desc limit $8", tenant.Value, legalEntityId, Db(state), Db(severity), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), Timestamp(reader[7])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityIssueListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualityNcrListItem>> ListNcrsAsync(TenantId tenant, Guid legalEntityId, string? state, string? severity, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityNcrListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,number,title,severity,state,issue_id,created_at from business_quality.nonconformance where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or severity=$4) and ($5::text is null or title ilike '%'||$5||'%') and ($6::timestamptz is null or (created_at,id)<($6::timestamptz,$7::uuid)) order by created_at desc,id desc limit $8", tenant.Value, legalEntityId, Db(state), Db(severity), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), Timestamp(reader[7])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityNcrListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualityCapaListItem>> ListCapasAsync(TenantId tenant, Guid legalEntityId, string? state, bool overdueOnly, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityCapaListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select c.id,c.legal_entity_id,c.number,c.type,c.title,c.state,c.ncr_id,(select count(*) from business_quality.capa_action a where a.tenant_id=c.tenant_id and a.capa_id=c.id and a.state='Open') as open_actions,c.created_at from business_quality.capa c where c.tenant_id=$1 and c.legal_entity_id=$2 and ($3::text is null or c.state=$3) and ($4::boolean=false or exists(select 1 from business_quality.capa_action a where a.tenant_id=c.tenant_id and a.capa_id=c.id and a.state='Open' and a.due_date<current_date)) and ($5::text is null or c.title ilike '%'||$5||'%') and ($6::timestamptz is null or (c.created_at,c.id)<($6::timestamptz,$7::uuid)) order by c.created_at desc,c.id desc limit $8", tenant.Value, legalEntityId, Db(state), overdueOnly, Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), Convert.ToInt32(reader.GetInt64(7), CultureInfo.InvariantCulture), Timestamp(reader[8])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityCapaListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualitySupplierProfileListItem>> ListSupplierProfilesAsync(TenantId tenant, Guid legalEntityId, string? riskLevel, string? status, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualitySupplierProfileListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodePlanCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,supplier_party_id,risk_level,target_ppm,receiving_inspection_required,status,row_version from business_quality.supplier_quality_profile where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or risk_level=$3) and ($4::text is null or status=$4) and ($5::uuid is null or (id>$5::uuid)) order by id limit $6", tenant.Value, legalEntityId, Db(riskLevel), Db(status), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetBoolean(5), reader.GetString(6), reader.GetInt64(7)));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualitySupplierProfileListItem>(items, more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"x\n{items[^1].Id:D}")) : null);
    }, ct);

    public Task<QualityPage<QualityScarListItem>> ListScarsAsync(TenantId tenant, Guid legalEntityId, string? state, bool overdueOnly, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityScarListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodePlanCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,number,supplier_party_id,title,state,due_date,ncr_id from business_quality.supplier_corrective_action_request where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::boolean=false or (due_date is not null and due_date<current_date and state not in ('Accepted','Closed'))) and ($5::text is null or title ilike '%'||$5||'%') and ($6::uuid is null or id>$6::uuid) order by id limit $7", tenant.Value, legalEntityId, Db(state), overdueOnly, Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetGuid(7)));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityScarListItem>(items, more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"x\n{items[^1].Id:D}")) : null);
    }, ct);

    public Task<QualityPage<QualityCertificateListItem>> ListCertificatesAsync(TenantId tenant, Guid legalEntityId, string? state, string? certificateType, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityCertificateListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodeDateCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,number,certificate_type,state,inspection_id,customer_reference,created_at from business_quality.quality_certificate where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or certificate_type=$4) and ($5::text is null or number ilike '%'||$5||'%' or customer_reference ilike '%'||$5||'%') and ($6::timestamptz is null or (created_at,id)<($6::timestamptz,$7::uuid)) order by created_at desc,id desc limit $8", tenant.Value, legalEntityId, Db(state), Db(certificateType), Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.CreatedAt), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetString(6), Timestamp(reader[7])));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityCertificateListItem>(items, more ? EncodeDateCursor(items[^1].CreatedAt, items[^1].Id) : null);
    }, ct);

    public Task<QualityPage<QualityCharacteristicListItem>> ListCharacteristicsAsync(TenantId tenant, Guid legalEntityId, string? search, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<QualityCharacteristicListItem>(); var size = Math.Clamp(pageSize ?? 50, 1, 100); var decoded = DecodePlanCursor(cursor);
        await using var command = Command(context, "select id,legal_entity_id,code,name,data_type,base_uom_code,required from business_quality.quality_characteristic where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or code ilike '%'||$3||'%' or name ilike '%'||$3||'%') and ($4::uuid is null or id>$4::uuid) order by id limit $5", tenant.Value, legalEntityId, Db(string.IsNullOrWhiteSpace(search) ? null : search.Trim()), Db(decoded?.Id), size + 1);
        await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token) && items.Count <= size) items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6)));
        var more = items.Count > size; if (more) items.RemoveAt(items.Count - 1); return new QualityPage<QualityCharacteristicListItem>(items, more ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"x\n{items[^1].Id:D}")) : null);
    }, ct);

    public Task<object> CreatePlanAsync(TenantId tenant, Guid actor, CreateQualityPlanRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ValidateText(request.Code, 80); ValidateText(request.Name, 200);
        if (request.EventType is not ("Receiving" or "InProcess" or "Final" or "Outgoing" or "Return" or "AdHoc")) throw new QualityException("quality.event_type_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_plan(tenant_id,id,legal_entity_id,code,name,event_type,mandatory,created_by) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.EventType, request.Mandatory, actor);
        await AuditAsync(context, tenant.Value, "QualityPlan", id, "created", actor, request, token);
        return (object)new { id, request.LegalEntityId, code = request.Code.Trim(), name = request.Name.Trim(), request.EventType, request.Mandatory, status = "Active" };
    }, ct);

    public Task<object> CreatePlanVersionAsync(TenantId tenant, Guid actor, Guid planId, CreateQualityPlanVersionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var next = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(version_no),0)+1 from business_quality.quality_plan_version where tenant_id=$1 and plan_id=$2", token, tenant.Value, planId), CultureInfo.InvariantCulture);
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_plan_version(tenant_id,id,plan_id,version_no,effective_from,effective_to,notes,created_by) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, planId, next, request.EffectiveFrom, Db(request.EffectiveTo), Db(request.Notes), actor);
        return (object)new { id, planId, versionNo = next, status = "Draft", request.EffectiveFrom, request.EffectiveTo };
    }, ct);

    public Task<object> ActivatePlanVersionAsync(TenantId tenant, Guid actor, Guid versionId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var planRow = await ReadSingleAsync(context, "select plan_id,effective_from,effective_to from business_quality.quality_plan_version where tenant_id=$1 and id=$2", token, tenant.Value, versionId) ?? throw new QualityException("quality.plan_version_not_found");
        var planId = (Guid)planRow[0];
        if (Convert.ToInt64(await ScalarAsync(context, "select count(*) from business_quality.quality_plan_characteristic where tenant_id=$1 and plan_version_id=$2", token, tenant.Value, versionId), CultureInfo.InvariantCulture) == 0) throw new QualityException("quality.plan_version_requires_characteristic");
        var overlap = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_plan_version where tenant_id=$1 and plan_id=$2 and id<>$3 and status='Active' and effective_from<=coalesce($5::date,'9999-12-31'::date) and coalesce(effective_to,'9999-12-31'::date)>=$4::date)", token, tenant.Value, planId, versionId, planRow[1], Db(planRow[2] is DBNull ? null : planRow[2])), CultureInfo.InvariantCulture);
        if (overlap) throw new QualityException("quality.plan_version_effective_conflict");
        await ExecuteAsync(context, "update business_quality.quality_plan_version set status='Superseded' where tenant_id=$1 and plan_id=$2 and status='Active'", token, tenant.Value, planId);
        if (await ExecuteAsync(context, "update business_quality.quality_plan_version set status='Active' where tenant_id=$1 and id=$2", token, tenant.Value, versionId) != 1) throw new QualityException("quality.plan_version_not_found");
        await AuditAsync(context, tenant.Value, "QualityPlanVersion", versionId, "activated", actor, null, token);
        return (object)new { id = versionId, planId, status = "Active" };
    }, ct);

    public Task<object> CreateApplicabilityAsync(TenantId tenant, Guid actor, Guid planId, CreateQualityApplicabilityRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_plan_applicability(tenant_id,id,plan_id,product_variant_id,supplier_party_id,plant_id,customer_party_id,event_type) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict do nothing", token, tenant.Value, id, planId, Db(request.ProductVariantId), Db(request.SupplierPartyId), Db(request.PlantId), Db(request.CustomerPartyId), Db(request.EventType));
        return (object)new { id, planId, request.ProductVariantId, request.SupplierPartyId, request.EventType };
    }, ct);

    public Task<object> CreateCharacteristicAsync(TenantId tenant, Guid actor, CreateCharacteristicRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ValidateText(request.Code, 80); ValidateText(request.Name, 200);
        if (request.DataType is not ("Numeric" or "Categorical" or "Text" or "Boolean")) throw new QualityException("quality.characteristic_type_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_characteristic(tenant_id,id,legal_entity_id,code,name,data_type,base_uom_code,required,allowed_values,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.DataType, Db(request.BaseUomCode), request.Required, Db(request.AllowedValuesJson ?? (request.DataType == "Categorical" ? "[]" : null)), actor);
        return (object)new { id, request.LegalEntityId, code = request.Code.Trim(), name = request.Name.Trim(), request.DataType, request.Required };
    }, ct);

    public Task<object> CreateSpecificationAsync(TenantId tenant, Guid actor, CreateSpecificationRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_specification_version(tenant_id,id,legal_entity_id,code,name,effective_from,effective_to,created_by) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.EffectiveFrom, Db(request.EffectiveTo), actor);
        return (object)new { id, request.Code, request.Name, versionNo = 1, status = "Active" };
    }, ct);

    public Task<object> AddPlanCharacteristicAsync(TenantId tenant, Guid actor, Guid planVersionId, CreatePlanCharacteristicRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_plan_characteristic(tenant_id,id,plan_version_id,characteristic_id,specification_version_id,minimum,maximum,target,allowed_values,required,evidence_required) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10,$11)", token, tenant.Value, id, planVersionId, request.CharacteristicId, Db(request.SpecificationVersionId), Db(request.Minimum), Db(request.Maximum), Db(request.Target), Db(request.AllowedValuesJson), request.Required, request.EvidenceRequired);
        return (object)new { id, planVersionId, request.CharacteristicId, request.Minimum, request.Maximum, request.Required, request.EvidenceRequired };
    }, ct);

    public Task<object> CreateSamplingPlanAsync(TenantId tenant, Guid actor, CreateSamplingPlanRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        _ = QualityRules.CalculateSampleSize(request.Method == "Aql" ? "Full" : request.Method, 1m, request.FixedQuantity, request.Percentage);
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.sampling_plan(tenant_id,id,legal_entity_id,code,name,method,fixed_quantity,percentage,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.Method, Db(request.FixedQuantity), Db(request.Percentage), actor);
        var versionId = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.sampling_plan_version(tenant_id,id,sampling_plan_id,version_no,effective_from) values($1,$2,$3,1,current_date)", token, tenant.Value, versionId, id);
        return (object)new { id, versionId, request.Code, request.Method };
    }, ct);

    public Task<SamplingCalculationView> CalculateSamplingAsync(TenantId tenant, Guid samplingPlanVersionId, decimal lotQuantity, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select p.method,p.fixed_quantity,p.percentage from business_quality.sampling_plan_version v join business_quality.sampling_plan p on p.tenant_id=v.tenant_id and p.id=v.sampling_plan_id where v.tenant_id=$1 and v.id=$2", token, tenant.Value, samplingPlanVersionId);
        if (row is null) throw new QualityException("quality.sampling_plan_not_found");
        var size = QualityRules.CalculateSampleSize((string)row[0], lotQuantity, row[1] as decimal?, row[2] as decimal?);
        return new SamplingCalculationView((string)row[0], lotQuantity, size, (string)row[0] == "Full");
    }, ct);

    public Task<InspectionView> CreateInspectionAsync(TenantId tenant, Guid actor, CreateInspectionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.LotQuantity <= 0m) throw new QualityException("quality.lot_quantity_invalid");
        if (!string.IsNullOrWhiteSpace(request.SourceFingerprint))
        {
            var existingId = await ScalarAsync<Guid?>(context, "select id from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2 limit 1", token, tenant.Value, request.SourceFingerprint);
            if (existingId is { } idempotentId) return await ReadInspectionAsync(context, tenant.Value, idempotentId, token) ?? throw new QualityException("quality.inspection_not_found");
        }
        var planVersionId = request.PlanVersionId is { } explicitPlan
            ? await ValidateExplicitPlanVersionAsync(context, tenant.Value, explicitPlan, request, token)
            : await ResolvePlanVersionAsync(context, tenant.Value, request, token);
        if (planVersionId is null && request.EventType is not "AdHoc") throw new QualityException("quality.plan_not_configured");
        var samplingId = request.SamplingPlanVersionId;
        var sampleSize = samplingId is null ? (int)Math.Ceiling(request.LotQuantity) : await CalculateSampleSizeInContextAsync(context, tenant.Value, samplingId.Value, request.LotQuantity, token);
        var id = Guid.CreateVersion7(); var number = $"QI-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.inspection(tenant_id,id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,customer_party_id,plan_version_id,sampling_plan_version_id,lot_quantity,sample_size,lot_reference,source_fingerprint,created_by) values($1,$2,$3,$4,$5,'InProgress','Pending',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18)", token, tenant.Value, id, request.LegalEntityId, number, request.EventType, Db(request.ProductVariantId), Db(request.SourceDocumentId), Db(request.ProductionOrderId), Db(request.ProductionOperationId), Db(request.SupplierPartyId), Db(request.CustomerPartyId), Db(planVersionId), Db(samplingId), request.LotQuantity, sampleSize, Db(request.LotReference), Db(request.SourceFingerprint ?? request.SourceDocumentId?.ToString("D")), actor);
        if (planVersionId is { } pvid)
        {
            var snapshot = await ReadPlanSnapshotAsync(context, tenant.Value, pvid, token);
            await ExecuteAsync(context, "insert into business_quality.inspection_plan_snapshot(tenant_id,id,inspection_id,plan_id,plan_version_id,snapshot) select $1,$2, $3, plan_id, $4, $5::jsonb from business_quality.quality_plan_version where tenant_id=$1 and id=$4", token, tenant.Value, Guid.CreateVersion7(), id, pvid, snapshot);
        }
        var quantity = sampleSize == 0 ? request.LotQuantity : Math.Min(request.LotQuantity, request.LotQuantity / Math.Max(1, sampleSize));
        for (var i = 1; i <= Math.Max(1, sampleSize); i++) await ExecuteAsync(context, "insert into business_quality.quality_sample(tenant_id,id,inspection_id,sample_no,quantity) values($1,$2,$3,$4,$5)", token, tenant.Value, Guid.CreateVersion7(), id, i, quantity);
        await AuditAsync(context, tenant.Value, "Inspection", id, "created", actor, request, token);
        return await ReadInspectionAsync(context, tenant.Value, id, token) ?? throw new QualityException("quality.inspection_not_found");
    }, ct);

    public Task<QualityPage<InspectionListItem>> ListInspectionsAsync(TenantId tenant, Guid legalEntityId, string? state, string? eventType, int? pageSize, string? cursor, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<InspectionListItem>();
        var boundedSize = Math.Clamp(pageSize ?? 50, 1, 100);
        var decoded = DecodeInspectionCursor(cursor);
        await using var command = Command(context, "select id,number,event_type,state,decision_state,product_variant_id,source_document_id,lot_quantity,sample_size,created_at from business_quality.inspection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or event_type=$4) and ($5::timestamptz is null or (created_at,id)<($5::timestamptz,$6::uuid)) order by created_at desc,id desc limit $7", tenant.Value, legalEntityId, Db(state), Db(eventType), Db(decoded?.CreatedAt), Db(decoded?.Id), boundedSize + 1);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token) && items.Count <= boundedSize) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetDecimal(7), reader.GetInt32(8), reader.GetFieldValue<DateTimeOffset>(9)));
        var hasMore = items.Count > boundedSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new QualityPage<InspectionListItem>(items, hasMore ? EncodeInspectionCursor(items[^1]) : null);
    }, ct);

    public Task<InspectionView?> GetInspectionAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadInspectionAsync(context, tenant.Value, id, token), ct);

    public Task<QualityRestrictionView?> GetRestrictionAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,restriction_type,status,reason_code,product_variant_id,lot_id,quantity,inspection_id,created_at,released_at,released_quantity,owner_execution_status,inventory_operation_id from business_quality.quality_restriction where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityRestrictionView((Guid)row[0], (string)row[1], (string)row[2], (string)row[3], row[4] is DBNull ? null : (Guid)row[4], row[5] is DBNull ? null : (Guid)row[5], row[6] is DBNull ? null : (decimal)row[6], row[7] is DBNull ? null : (Guid)row[7], Timestamp(row[8]), row[9] is DBNull ? null : Timestamp(row[9]), (decimal)row[10], (string)row[11], row[12] is DBNull ? null : (Guid)row[12]);
    }, ct);

    public Task<QualityDispositionView?> GetDispositionAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,inspection_id,decision,state,quantity,reason,created_at,owner_execution_status,owner_operation_id from business_quality.quality_disposition where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityDispositionView((Guid)row[0], row[1] is DBNull ? null : (Guid)row[1], (string)row[2], (string)row[3], row[4] is DBNull ? null : (decimal)row[4], (string)row[5], Timestamp(row[6]), (string)row[7], row[8] is DBNull ? null : (Guid)row[8]);
    }, ct);

    public Task<QualityIssueView?> GetIssueAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,issue_type,title,severity,state,inspection_id,supplier_party_id,created_at from business_quality.quality_issue where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityIssueView((Guid)row[0], (string)row[1], (string)row[2], (string)row[3], (string)row[4], row[5] is DBNull ? null : (Guid)row[5], row[6] is DBNull ? null : (Guid)row[6], Timestamp(row[7]));
    }, ct);

    public Task<QualityNcrView?> GetNcrAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,number,legal_entity_id,title,severity,state,issue_id,created_at from business_quality.nonconformance where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityNcrView((Guid)row[0], (string)row[1], (Guid)row[2], (string)row[3], (string)row[4], (string)row[5], row[6] is DBNull ? null : (Guid)row[6], Timestamp(row[7]));
    }, ct);

    public Task<CapaView?> GetCapaAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,number,type,title,state,ncr_id,created_at from business_quality.capa where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new CapaView((Guid)row[0], (string)row[1], (string)row[2], (string)row[3], (string)row[4], row[5] is DBNull ? null : (Guid)row[5], Timestamp(row[6]));
    }, ct);

    public Task<QualityScarView?> GetScarAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,number,legal_entity_id,supplier_party_id,title,state,ncr_id,due_date from business_quality.supplier_corrective_action_request where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityScarView((Guid)row[0], (string)row[1], (Guid)row[2], (Guid)row[3], (string)row[4], (string)row[5], row[6] is DBNull ? null : (Guid)row[6], row[7] is DBNull ? null : (DateOnly)row[7]);
    }, ct);

    public Task<QualityCertificateView?> GetCertificateAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select id,number,certificate_type,state,inspection_id,customer_reference,created_at from business_quality.quality_certificate where tenant_id=$1 and id=$2", token, tenant.Value, id);
        return row is null ? null : new QualityCertificateView((Guid)row[0], (string)row[1], (string)row[2], (string)row[3], (Guid)row[4], row[5] is DBNull ? null : (string)row[5], Timestamp(row[6]));
    }, ct);

    public Task<Guid?> GetLegalEntityForResourceAsync(TenantId tenant, string resourceType, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (resourceType == "capa-action") return await ScalarAsync<Guid?>(context, "select c.legal_entity_id from business_quality.capa_action a join business_quality.capa c on c.tenant_id=a.tenant_id and c.id=a.capa_id where a.tenant_id=$1 and a.id=$2", token, tenant.Value, id);
        var table = resourceType switch
        {
            "inspection" => "inspection",
            "restriction" => "quality_restriction",
            "disposition" => "quality_disposition",
            "issue" => "quality_issue",
            "ncr" => "nonconformance",
            "capa" => "capa",
            "scar" => "supplier_corrective_action_request",
            "certificate" => "quality_certificate",
            _ => throw new QualityException("quality.resource_type_invalid")
        };
        return await ScalarAsync<Guid?>(context, $"select legal_entity_id from business_quality.{table} where tenant_id=$1 and id=$2", token, tenant.Value, id);
    }, ct);

    public Task<InspectionResultView> AddResultAsync(TenantId tenant, Guid actor, Guid inspectionId, AddInspectionResultRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var inspection = await ReadSingleAsync(context, "select plan_version_id,state from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, inspectionId) ?? throw new QualityException("quality.inspection_not_found");
        if ((string)inspection[1] is not ("InProgress" or "Draft")) throw new QualityException("quality.inspection_immutable");
        var rule = await ReadSingleAsync(context, "select c.data_type,c.required,c.base_uom_code,pc.minimum,pc.maximum,pc.allowed_values,pc.required,pc.evidence_required from business_quality.quality_characteristic c join business_quality.quality_plan_characteristic pc on pc.tenant_id=c.tenant_id and pc.characteristic_id=c.id and pc.plan_version_id=$3 where c.tenant_id=$1 and c.id=$2", token, tenant.Value, request.CharacteristicId, inspection[0] is DBNull ? null : inspection[0]);
        if (rule is null) throw new QualityException("quality.characteristic_not_in_plan");
        var numeric = request.NumericValue; var text = request.TextValue; var boolean = request.BooleanValue;
        if ((numeric is not null ? 1 : 0) + (text is not null ? 1 : 0) + (boolean is not null ? 1 : 0) != 1) throw new QualityException("quality.result_value_required");
        if (request.InstrumentReference is not null && request.CalibrationStatus is not ("Valid" or "NotRequired")) throw new QualityException("quality.instrument_not_calibrated");
        if (rule[2] is not DBNull && !string.Equals((string)rule[2], request.UomCode, StringComparison.OrdinalIgnoreCase)) throw new QualityException("quality.uom_mismatch");
        if (request.InstrumentReference is not null && request.CalibrationDueOn is { } dueOn && dueOn < DateOnly.FromDateTime(DateTime.UtcNow.Date)) throw new QualityException("quality.instrument_calibration_expired");
        if (request.SampleId is { } sampleId && !Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_sample where tenant_id=$1 and id=$2 and inspection_id=$3)", token, tenant.Value, sampleId, inspectionId), CultureInfo.InvariantCulture)) throw new QualityException("quality.sample_not_in_inspection");
        if (Convert.ToBoolean(rule[7], CultureInfo.InvariantCulture) && string.IsNullOrWhiteSpace(request.EvidenceReference)) throw new QualityException("quality.evidence_required");
        var duplicate = await ScalarAsync(context, "select exists(select 1 from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2 and characteristic_id=$3 and (($4::uuid is null and sample_id is null) or sample_id=$4::uuid))", token, tenant.Value, inspectionId, request.CharacteristicId, Db(request.SampleId));
        if (Convert.ToBoolean(duplicate, CultureInfo.InvariantCulture)) throw new QualityException("quality.result_duplicate");
        var passed = rule[0] switch { "Numeric" => QualityRules.EvaluateNumeric(numeric, rule[3] is DBNull ? null : (decimal?)rule[3], rule[4] is DBNull ? null : (decimal?)rule[4], Convert.ToBoolean(rule[6], CultureInfo.InvariantCulture)), "Categorical" => QualityRules.EvaluateCategorical(text, ParseAllowed(rule[5]), Convert.ToBoolean(rule[6], CultureInfo.InvariantCulture)), "Boolean" => boolean == true, _ => !string.IsNullOrWhiteSpace(text) };
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.inspection_result(tenant_id,id,inspection_id,sample_id,characteristic_id,numeric_value,text_value,boolean_value,uom_code,passed,evidence_reference,recorded_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, id, inspectionId, Db(request.SampleId), request.CharacteristicId, Db(numeric), Db(text), Db(boolean), Db(request.UomCode), passed, Db(request.EvidenceReference), actor);
        if (request.InstrumentReference is not null) await ExecuteAsync(context, "insert into business_quality.inspection_instrument_use(tenant_id,id,inspection_id,instrument_ref,calibration_status,calibration_due_on,calibration_snapshot) values($1,$2,$3,$4,$5,$6,$7::jsonb)", token, tenant.Value, Guid.CreateVersion7(), inspectionId, request.InstrumentReference, request.CalibrationStatus!, Db(request.CalibrationDueOn), JsonSerializer.Serialize(new { request.InstrumentReference, request.CalibrationStatus, request.CalibrationDueOn }));
        return new InspectionResultView(id, request.CharacteristicId, numeric, text, boolean, passed, request.UomCode, request.EvidenceReference);
    }, ct);

    public Task<InspectionView> CompleteInspectionAsync(TenantId tenant, Guid actor, Guid inspectionId, CompleteInspectionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var existing = await ReadSingleAsync(context, "select plan_version_id,state,production_order_id,production_operation_id from business_quality.inspection where tenant_id=$1 and id=$2 for update", token, tenant.Value, inspectionId) ?? throw new QualityException("quality.inspection_not_found");
        if ((string)existing[1] is not ("InProgress" or "Draft")) throw new QualityException("quality.inspection_immutable");
        var failed = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2 and passed=false)", token, tenant.Value, inspectionId), CultureInfo.InvariantCulture);
        var missing = existing[0] is not DBNull && Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_plan_characteristic pc where pc.tenant_id=$1 and pc.plan_version_id=$3 and pc.required and not exists(select 1 from business_quality.inspection_result r where r.tenant_id=$1 and r.inspection_id=$2 and r.characteristic_id=pc.characteristic_id and r.passed is not null))", token, tenant.Value, inspectionId, existing[0]), CultureInfo.InvariantCulture);
        var noResults = existing[0] is not DBNull && Convert.ToBoolean(await ScalarAsync(context, "select not exists(select 1 from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2)", token, tenant.Value, inspectionId), CultureInfo.InvariantCulture);
        var evidenceMissing = existing[0] is not DBNull && Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_plan_characteristic pc where pc.tenant_id=$1 and pc.plan_version_id=$3 and pc.evidence_required and not exists(select 1 from business_quality.inspection_result r where r.tenant_id=$1 and r.inspection_id=$2 and r.characteristic_id=pc.characteristic_id and nullif(trim(r.evidence_reference),'') is not null))", token, tenant.Value, inspectionId, existing[0]), CultureInfo.InvariantCulture);
        var decision = failed || missing || noResults || evidenceMissing ? "Failed" : "Passed"; var evidence = $"inspection:{inspectionId:D}:evaluation";
        await ExecuteAsync(context, "insert into business_quality.inspection_evaluation(tenant_id,id,inspection_id,decision,evaluated_by,evidence_reference,reason) values($1,$2,$3,$4,$5,$6,$7) on conflict(tenant_id,inspection_id) do update set decision=excluded.decision,evaluated_by=excluded.evaluated_by,evaluated_at=now(),evidence_reference=excluded.evidence_reference,reason=excluded.reason", token, tenant.Value, Guid.CreateVersion7(), inspectionId, decision, actor, evidence, Db(request.ReviewNotes));
        await ExecuteAsync(context, "update business_quality.inspection set state='Completed',decision_state=$3,completed_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, inspectionId, decision);
        await AuditAsync(context, tenant.Value, "Inspection", inspectionId, "completed", actor, new { decision }, token);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityInspectionCompletedV1", JsonSerializer.Serialize(new { inspectionId, decision, productionOrderId = existing[2] is DBNull ? (Guid?)null : (Guid)existing[2], productionOperationId = existing[3] is DBNull ? (Guid?)null : (Guid)existing[3], evidenceReference = evidence }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), inspectionId, "quality"));
        return await ReadInspectionAsync(context, tenant.Value, inspectionId, token) ?? throw new QualityException("quality.inspection_not_found");
    }, ct);

    public Task<InspectionView> ReinspectAsync(TenantId tenant, Guid actor, Guid inspectionId, ReinspectionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var source = await ReadSingleAsync(context, "select legal_entity_id,event_type,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,customer_party_id,lot_quantity,plan_version_id,sampling_plan_version_id,lot_reference from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, inspectionId) ?? throw new QualityException("quality.inspection_not_found");
        await ExecuteAsync(context, "update business_quality.inspection set state='Superseded',row_version=row_version+1 where tenant_id=$1 and id=$2 and state='Completed'", token, tenant.Value, inspectionId);
        var request2 = new CreateInspectionRequest((Guid)source[0], (string)source[1], source[2] is DBNull ? null : (Guid)source[2], (decimal)source[8], source[3] is DBNull ? null : (Guid)source[3], source[4] is DBNull ? null : (Guid)source[4], source[5] is DBNull ? null : (Guid)source[5], source[6] is DBNull ? null : (Guid)source[6], source[7] is DBNull ? null : (Guid)source[7], source[9] is DBNull ? null : (Guid)source[9], source[10] is DBNull ? null : (Guid)source[10], source[11] is DBNull ? null : (string)source[11], SourceFingerprint: $"reinspection:{inspectionId:D}:{request.Reason.Trim()}");
        return await CreateInspectionInContextAsync(context, tenant, actor, request2, token);
    }, ct);

    public async Task<QualityRestrictionView> CreateRestrictionAsync(TenantId tenant, Guid actor, CreateRestrictionRequest request, Guid legalEntityId, CancellationToken ct)
    {
        if (request.RestrictionType is not ("QualityHold" or "Quarantine" or "Blocked" or "ReturnsInspection")) throw new QualityException("quality.restriction_type_invalid");
        var hasOwnerScope = request.ProductVariantId is not null || request.WarehouseId is not null || request.Quantity is not null;
        if (hasOwnerScope && (request.ProductVariantId is null || request.WarehouseId is null || request.Quantity is not > 0m || request.UnitOfMeasureId is null))
            throw new QualityException("quality.owner_scope_incomplete");
        if (hasOwnerScope && inventoryCapability is null) throw new QualityException("quality.inventory_capability_unavailable");
        var id = Guid.CreateVersion7();
        var ownerKey = hasOwnerScope ? $"quality-restriction:{id:D}:create" : null;
        await coordinator.ExecuteAsync<bool>(tenant, async (context, token) =>
        {
            await ExecuteAsync(context, "insert into business_quality.quality_restriction(tenant_id,id,legal_entity_id,restriction_type,reason_code,product_variant_id,warehouse_id,location_id,lot_id,serial_id,inspection_id,quantity,owner_execution_status,owner_idempotency_key,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)", token, tenant.Value, id, legalEntityId, request.RestrictionType, request.ReasonCode.Trim(), Db(request.ProductVariantId), Db(request.WarehouseId), Db(request.LocationId), Db(request.LotId), Db(request.SerialId), Db(request.InspectionId), Db(request.Quantity), hasOwnerScope ? "Pending" : "NotRequired", Db(ownerKey), actor);
            await AuditAsync(context, tenant.Value, "QualityRestriction", id, "created", actor, request, token);
            return true;
        }, ct);
        if (hasOwnerScope)
        {
            try
            {
                var ownerResult = await inventoryCapability!.ChangeQualityStatusAsync(tenant.Value, actor, new InventoryQualityStatusRequest(legalEntityId, request.ProductVariantId!.Value, request.WarehouseId!.Value, request.LocationId, request.UnitOfMeasureId!.Value, request.Quantity!.Value, "Available", InventoryStatusForRestriction(request.RestrictionType), request.OperationalDate ?? DateOnly.FromDateTime(DateTime.UtcNow), request.PostingDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ownerKey!, request.LotId, request.SerialId), ct);
                await coordinator.ExecuteAsync<bool>(tenant, async (context, token) =>
                {
                    await ExecuteAsync(context, "update business_quality.quality_restriction set owner_execution_status='Confirmed',inventory_operation_id=$3 where tenant_id=$1 and id=$2", token, tenant.Value, id, ownerResult.OperationId);
                    context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionCreatedV1", JsonSerializer.Serialize(new { restrictionId = id, legalEntityId, request.ProductVariantId, request.WarehouseId, request.LocationId, request.LotId, request.SerialId, request.Quantity, request.RestrictionType, ownerOperationId = ownerResult.OperationId }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
                    return true;
                }, ct);
            }
            catch
            {
                await coordinator.ExecuteAsync<int>(tenant, (context, token) => ExecuteAsync(context, "update business_quality.quality_restriction set owner_execution_status='Failed' where tenant_id=$1 and id=$2", token, tenant.Value, id), ct);
                throw new QualityException("quality.inventory_owner_execution_failed");
            }
        }
        else
        {
            await coordinator.ExecuteAsync<bool>(tenant, async (context, token) =>
            {
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionCreatedV1", JsonSerializer.Serialize(new { restrictionId = id, legalEntityId, request.ProductVariantId, request.WarehouseId, request.LocationId, request.LotId, request.SerialId, request.Quantity, request.RestrictionType }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
                return true;
            }, ct);
        }
        return new QualityRestrictionView(id, request.RestrictionType, "Active", request.ReasonCode, request.ProductVariantId, request.LotId, request.Quantity, request.InspectionId, DateTimeOffset.UtcNow, null, 0m, hasOwnerScope ? "Confirmed" : "NotRequired");
    }

    public async Task<QualityRestrictionView> ReleaseRestrictionAsync(TenantId tenant, Guid actor, Guid id, ReleaseRestrictionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new QualityException("quality.release_reason_required");
        if (request.IdempotencyKey is { Length: > 180 }) throw new QualityException("quality.release_idempotency_invalid");
        var logicalKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? $"release:{Guid.CreateVersion7():D}" : request.IdempotencyKey.Trim();
        var claim = await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var row = await ReadSingleAsync(context, "select legal_entity_id,restriction_type,status,reason_code,product_variant_id,warehouse_id,location_id,lot_id,serial_id,inspection_id,quantity,created_at,released_at,owner_execution_status,inventory_operation_id,released_quantity from business_quality.quality_restriction where tenant_id=$1 and id=$2 for update", token, tenant.Value, id) ?? throw new QualityException("quality.restriction_not_found");
            var hasOwnerScope = row[4] is not DBNull || row[5] is not DBNull || row[10] is not DBNull;
            object[]? existing = null;
            if (hasOwnerScope)
            {
                existing = await ReadSingleAsync(context, "select id,request_hash,state,quantity,unit_of_measure_id,operational_date,posting_date,reason,owner_operation_id from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and idempotency_key=$3 for update", token, tenant.Value, id, logicalKey);
                if (existing is not null && (string)existing[2] == "Confirmed")
                {
                    var replayHash = ReleaseRequestHash(id, request.Reason.Trim(), request.Quantity ?? (decimal)existing[3], request.UnitOfMeasureId ?? (Guid)existing[4], request.OperationalDate ?? (DateOnly)existing[5], request.PostingDate ?? (DateOnly)existing[6]);
                    if (!string.Equals((string)existing[1], replayHash, StringComparison.Ordinal)) throw new QualityException("quality.release_idempotency_conflict");
                    return ReleaseClaim.Confirmed(row, (Guid)existing[0], existing[8] is DBNull ? null : (Guid)existing[8]);
                }
                if (existing is not null && (string)existing[2] == "Executing") throw new QualityException("quality.release_operation_in_progress");
            }
            if ((string)row[2] != "Active") throw new QualityException("quality.restriction_not_active");
            if (!hasOwnerScope)
            {
                await ExecuteAsync(context, "update business_quality.quality_restriction set status='Released',released_by=$3,released_at=now(),release_reason=$4 where tenant_id=$1 and id=$2 and status='Active'", token, tenant.Value, id, actor, request.Reason.Trim());
                context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionReleasedV1", JsonSerializer.Serialize(new { restrictionId = id, reason = request.Reason }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
                return ReleaseClaim.NonOwner(row);
            }
            if ((string)row[13] is not ("Confirmed" or "Releasing") || inventoryCapability is null) throw new QualityException("quality.inventory_owner_not_confirmed");
            if (row[4] is DBNull || row[5] is DBNull || row[10] is DBNull || request.UnitOfMeasureId is null) throw new QualityException("quality.owner_scope_incomplete");
            var pending = Convert.ToDecimal(await ScalarAsync(context, "select coalesce(sum(quantity) filter (where state='Executing'),0) from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2", token, tenant.Value, id), CultureInfo.InvariantCulture);
            var available = (decimal)row[10] - (decimal)row[15] - pending;
            var quantity = request.Quantity ?? (existing is null ? available : (decimal)existing[3]);
            if (quantity <= 0m || quantity > available) throw new QualityException("quality.release_quantity_invalid");
            var operationalDate = request.OperationalDate ?? (existing is null ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)existing[5]);
            var postingDate = request.PostingDate ?? (existing is null ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)existing[6]);
            if (postingDate < operationalDate) throw new QualityException("quality.release_date_invalid");
            var hash = ReleaseRequestHash(id, request.Reason.Trim(), quantity, request.UnitOfMeasureId.Value, operationalDate, postingDate);
            Guid operationId;
            if (existing is not null)
            {
                if (!string.Equals((string)existing[1], hash, StringComparison.Ordinal)) throw new QualityException("quality.release_idempotency_conflict");
                operationId = (Guid)existing[0];
                if ((string)existing[2] == "Confirmed") return ReleaseClaim.Confirmed(row, operationId, existing[8] is DBNull ? null : (Guid)existing[8]);
                await ExecuteAsync(context, "update business_quality.quality_restriction_release_operation set state='Executing',error_code=null,updated_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, operationId);
            }
            else
            {
                operationId = Guid.CreateVersion7();
                await ExecuteAsync(context, "insert into business_quality.quality_restriction_release_operation(tenant_id,id,restriction_id,idempotency_key,request_hash,quantity,unit_of_measure_id,reason,operational_date,posting_date,state,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'Executing',$11)", token, tenant.Value, operationId, id, logicalKey, hash, quantity, request.UnitOfMeasureId.Value, request.Reason.Trim(), operationalDate, postingDate, actor);
            }
            await ExecuteAsync(context, "update business_quality.quality_restriction set owner_execution_status='Releasing' where tenant_id=$1 and id=$2 and status='Active'", token, tenant.Value, id);
            return ReleaseClaim.Owner(row, operationId, request.Reason.Trim(), quantity, request.UnitOfMeasureId.Value, operationalDate, postingDate);
        }, ct);
        if (!claim.RequiresOwner || claim.AlreadyConfirmed) return await GetRestrictionAsync(tenant, id, ct) ?? throw new QualityException("quality.restriction_not_found");
        try
        {
            var ownerResult = await inventoryCapability!.ChangeQualityStatusAsync(tenant.Value, actor, new InventoryQualityStatusRequest(claim.LegalEntityId, claim.ProductVariantId, claim.WarehouseId, claim.LocationId, claim.UnitOfMeasureId, claim.Quantity, InventoryStatusForRestriction(claim.RestrictionType), "Available", claim.OperationalDate, claim.PostingDate, $"quality-release:{claim.OperationId:D}", claim.LotId, claim.SerialId), ct);
            return await coordinator.ExecuteAsync(tenant, async (context, token) =>
            {
                var current = await ReadSingleAsync(context, "select restriction_type,reason_code,product_variant_id,lot_id,inspection_id,quantity,created_at,released_quantity,status from business_quality.quality_restriction where tenant_id=$1 and id=$2 for update", token, tenant.Value, id) ?? throw new QualityException("quality.restriction_not_found");
                var op = await ReadSingleAsync(context, "select state,quantity,owner_operation_id from business_quality.quality_restriction_release_operation where tenant_id=$1 and id=$2 for update", token, tenant.Value, claim.OperationId) ?? throw new QualityException("quality.release_operation_not_found");
                if ((string)op[0] == "Confirmed") return await ReadRestrictionAsync(context, tenant.Value, id, token) ?? throw new QualityException("quality.restriction_not_found");
                var newReleased = (decimal)current[7] + (decimal)op[1];
                if (newReleased > (decimal)current[5]) throw new QualityException("quality.release_quantity_conflict");
                var newStatus = newReleased >= (decimal)current[5] ? "Released" : "Active";
                await ExecuteAsync(context, "update business_quality.quality_restriction_release_operation set state='Confirmed',owner_operation_id=$3,updated_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, claim.OperationId, ownerResult.OperationId);
                await ExecuteAsync(context, "update business_quality.quality_restriction set status=$3,released_quantity=$4,released_by=$5,released_at=case when $3='Released' then now() else released_at end,release_reason=$6,owner_execution_status='Confirmed',inventory_operation_id=$7 where tenant_id=$1 and id=$2", token, tenant.Value, id, newStatus, newReleased, actor, claim.Reason, ownerResult.OperationId);
                if (newStatus == "Released") context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionReleasedV1", JsonSerializer.Serialize(new { restrictionId = id, reason = claim.Reason, ownerOperationId = ownerResult.OperationId }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
                return await ReadRestrictionAsync(context, tenant.Value, id, token) ?? throw new QualityException("quality.restriction_not_found");
            }, ct);
        }
        catch (Exception exception)
        {
            await coordinator.ExecuteAsync(tenant, (context, token) => ExecuteAsync(context, "update business_quality.quality_restriction_release_operation set state='Unknown',error_code=$3,updated_at=now() where tenant_id=$1 and id=$2 and state='Executing'", token, tenant.Value, claim.OperationId, exception.GetType().Name), CancellationToken.None);
            throw exception is QualityException qualityException ? qualityException : new QualityException("quality.inventory_owner_execution_unknown");
        }
    }

    private static string InventoryStatusForRestriction(string type) => type switch
    {
        "Quarantine" => "Quarantine",
        "Blocked" => "Blocked",
        "ReturnsInspection" => "ReturnsInspection",
        _ => "QualityHold"
    };

    public Task<QualityDispositionView> CreateDispositionAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateDispositionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Decision is not ("Accept" or "Reject" or "UseAsIs" or "Concession" or "Return" or "Scrap" or "Rework" or "Partial")) throw new QualityException("quality.disposition_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_disposition(tenant_id,id,legal_entity_id,inspection_id,decision,quantity,reason,external_reference,unit_of_measure_id,operational_date,posting_date,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, id, legalEntityId, Db(request.InspectionId), request.Decision, Db(request.Quantity), request.Reason.Trim(), Db(request.ExternalReference), Db(request.UnitOfMeasureId), Db(request.OperationalDate), Db(request.PostingDate), actor);
        return new QualityDispositionView(id, request.InspectionId, request.Decision, "Draft", request.Quantity, request.Reason.Trim(), DateTimeOffset.UtcNow);
    }, ct);

    public Task<QualityDispositionView> ApproveDispositionAsync(TenantId tenant, Guid actor, Guid id, ApproveDispositionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select inspection_id,decision,quantity,reason,created_at from business_quality.quality_disposition where tenant_id=$1 and id=$2", token, tenant.Value, id) ?? throw new QualityException("quality.disposition_not_found");
        if (await ExecuteAsync(context, "update business_quality.quality_disposition set state='Approved',approval_required=true,approved_by=$3,approved_at=now() where tenant_id=$1 and id=$2 and state='Draft'", token, tenant.Value, id, actor) != 1) throw new QualityException("quality.disposition_not_approvable");
        await AuditAsync(context, tenant.Value, "QualityDisposition", id, "approved", actor, request, token);
        return new QualityDispositionView(id, row[0] is DBNull ? null : (Guid)row[0], (string)row[1], "Approved", row[2] is DBNull ? null : (decimal)row[2], (string)row[3], Timestamp(row[4]));
    }, ct);

    public async Task<QualityDispositionView> ExecuteDispositionAsync(TenantId tenant, Guid actor, Guid id, CancellationToken ct)
    {
        var row = await coordinator.ExecuteAsync(tenant, (context, token) => ReadSingleAsync(context, "select legal_entity_id,inspection_id,decision,quantity,reason,created_at,state,unit_of_measure_id,operational_date,posting_date,owner_execution_status,owner_operation_id from business_quality.quality_disposition where tenant_id=$1 and id=$2", token, tenant.Value, id), ct) ?? throw new QualityException("quality.disposition_not_found");
        if ((string)row[6] == "Executed") return new QualityDispositionView(id, row[1] is DBNull ? null : (Guid)row[1], (string)row[2], "Executed", row[3] is DBNull ? null : (decimal)row[3], (string)row[4], Timestamp(row[5]), (string)row[10], row[11] is DBNull ? null : (Guid)row[11]);
        if ((string)row[6] != "Approved") throw new QualityException("quality.disposition_requires_approval");
        if ((string)row[10] is "Executing" or "Unknown") throw new QualityException("quality.disposition_execution_in_progress");
        if (await coordinator.ExecuteAsync(tenant, (context, token) => ExecuteAsync(context, "update business_quality.quality_disposition set owner_execution_status='Executing',owner_idempotency_key=$3,execution_error_code=null where tenant_id=$1 and id=$2 and state='Approved' and owner_execution_status in ('NotRequired','Failed')", token, tenant.Value, id, $"quality-disposition:{id:D}"), ct) != 1)
            throw new QualityException("quality.disposition_execution_in_progress");
        Guid? ownerOperationId = null;
        try
        {
            var decision = (string)row[2];
            var inspectionId = row[1] is DBNull ? (Guid?)null : (Guid)row[1];
            var inspection = inspectionId is null ? null : await coordinator.ExecuteAsync(tenant, (context, token) => ReadSingleAsync(context, "select source_document_id,production_order_id,production_operation_id,product_variant_id from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, inspectionId), ct);
            var restriction = inspectionId is null ? null : await coordinator.ExecuteAsync(tenant, (context, token) => ReadSingleAsync(context, "select id,product_variant_id,warehouse_id,location_id,lot_id,serial_id,quantity,released_quantity from business_quality.quality_restriction where tenant_id=$1 and inspection_id=$2 and status='Active' order by created_at desc,id desc limit 1", token, tenant.Value, inspectionId), ct);
            var quantity = row[3] is DBNull ? restriction is null ? 0m : (decimal)restriction[6] - (decimal)restriction[7] : (decimal)row[3];
            if (quantity <= 0m) throw new QualityException("quality.disposition_quantity_required");
            if (decision is "Accept" or "UseAsIs" or "Concession" or "Partial")
            {
                if (restriction is null || row[7] is DBNull) throw new QualityException("quality.disposition_owner_data_required");
                var released = await ReleaseRestrictionAsync(tenant, actor, (Guid)restriction[0], new ReleaseRestrictionRequest((string)row[4], quantity, (Guid)row[7], row[8] is DBNull ? null : (DateOnly)row[8], row[9] is DBNull ? null : (DateOnly)row[9], $"quality-disposition:{id:D}:release"), ct);
                ownerOperationId = released.OwnerOperationId;
            }
            else if (decision == "Scrap")
            {
                if (restriction is null || row[7] is DBNull || inventoryDispositionCapability is null) throw new QualityException("quality.inventory_scrap_capability_unavailable");
                var result = await inventoryDispositionCapability.ExecuteScrapAsync(tenant.Value, actor, new QualityInventoryScrapRequest((Guid)row[0], (Guid)restriction[1], (Guid)restriction[2], restriction[3] is DBNull ? null : (Guid)restriction[3], (Guid)row[7], quantity, id, row[8] is DBNull ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)row[8], row[9] is DBNull ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)row[9], $"quality-disposition:{id:D}:scrap", restriction[4] is DBNull ? null : (Guid)restriction[4], restriction[5] is DBNull ? null : (Guid)restriction[5]), ct);
                ownerOperationId = result.OperationId;
                await MarkRestrictionDisposedAsync(tenant, actor, (Guid)restriction[0], quantity, (string)row[4], result.OperationId, ct);
            }
            else if (decision == "Return")
            {
                if (restriction is null || inspection is null || inspection[0] is DBNull || row[7] is DBNull || purchasingDispositionCapability is null) throw new QualityException("quality.purchasing_return_capability_unavailable");
                await ReleaseRestrictionAsync(tenant, actor, (Guid)restriction[0], new ReleaseRestrictionRequest((string)row[4], quantity, (Guid)row[7], row[8] is DBNull ? null : (DateOnly)row[8], row[9] is DBNull ? null : (DateOnly)row[9], $"quality-disposition:{id:D}:release"), ct);
                var result = await purchasingDispositionCapability.ExecuteSupplierReturnAsync(tenant.Value, actor, new QualitySupplierReturnRequest((Guid)row[0], (Guid)inspection[0], (Guid)restriction[1], (Guid)restriction[2], restriction[3] is DBNull ? null : (Guid)restriction[3], (Guid)row[7], quantity, id, row[8] is DBNull ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)row[8], row[9] is DBNull ? DateOnly.FromDateTime(DateTime.UtcNow) : (DateOnly)row[9], restriction[4] is DBNull ? null : (Guid)restriction[4], restriction[5] is DBNull ? null : (Guid)restriction[5]), ct);
                ownerOperationId = result.OperationId;
            }
            else if (decision == "Rework")
            {
                if (inspection is null || inspection[1] is DBNull || manufacturingDispositionCapability is null) throw new QualityException("quality.manufacturing_rework_capability_unavailable");
                var result = await manufacturingDispositionCapability.ExecuteReworkAsync(tenant.Value, actor, new QualityReworkRequest((Guid)row[0], (Guid)inspection[1], inspection[2] is DBNull ? null : (Guid)inspection[2], quantity, id, (string)row[4]), ct);
                ownerOperationId = result.OperationId;
            }
            else if (decision is "Reject")
            {
                if (restriction is not null) throw new QualityException("quality.reject_requires_owner_disposition");
            }
            else throw new QualityException("quality.disposition_owner_capability_unavailable");
        }
        catch (Exception exception)
        {
            await coordinator.ExecuteAsync(tenant, (context, token) => ExecuteAsync(context, "update business_quality.quality_disposition set owner_execution_status='Unknown',execution_error_code=$3 where tenant_id=$1 and id=$2 and state='Approved'", token, tenant.Value, id, exception.GetType().Name), CancellationToken.None);
            throw exception is QualityException qualityException ? qualityException : new QualityException("quality.disposition_owner_execution_unknown");
        }
        return await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            if (await ExecuteAsync(context, "update business_quality.quality_disposition set state='Executed',executed_by=$3,executed_at=now(),owner_execution_status=$4,owner_operation_id=$5,row_version=row_version+1 where tenant_id=$1 and id=$2 and state='Approved' and owner_execution_status='Executing'", token, tenant.Value, id, actor, ownerOperationId is null ? "NotRequired" : "Confirmed", Db(ownerOperationId)) != 1) throw new QualityException("quality.disposition_requires_approval");
            context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityDispositionExecutedV1", JsonSerializer.Serialize(new { dispositionId = id, decision = (string)row[2], ownerOperationId }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
            return new QualityDispositionView(id, row[1] is DBNull ? null : (Guid)row[1], (string)row[2], "Executed", row[3] is DBNull ? null : (decimal)row[3], (string)row[4], Timestamp(row[5]), ownerOperationId is null ? "NotRequired" : "Confirmed", ownerOperationId);
        }, ct);
    }

    public Task<QualityIssueView> CreateIssueAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateIssueRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.quality_issue(tenant_id,id,legal_entity_id,issue_type,title,description,severity,inspection_id,supplier_party_id,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, legalEntityId, request.IssueType, request.Title.Trim(), request.Description.Trim(), request.Severity, Db(request.InspectionId), Db(request.SupplierPartyId), actor);
        return new QualityIssueView(id, request.IssueType, request.Title.Trim(), request.Severity, "Open", request.InspectionId, request.SupplierPartyId, DateTimeOffset.UtcNow);
    }, ct);

    public Task<QualityNcrView> CreateNcrAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateNcrRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); var number = $"NCR-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.nonconformance(tenant_id,id,number,legal_entity_id,issue_id,title,description,severity,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, legalEntityId, Db(request.IssueId), request.Title.Trim(), request.Description.Trim(), request.Severity, actor);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityNcrCreatedV1", JsonSerializer.Serialize(new { nonconformanceId = id, number }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return new QualityNcrView(id, number, legalEntityId, request.Title.Trim(), request.Severity, "Open", request.IssueId, DateTimeOffset.UtcNow);
    }, ct);

    public Task<object> AddContainmentAsync(TenantId tenant, Guid actor, Guid ncrId, CreateContainmentRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.quality_containment_action(tenant_id,id,nonconformance_id,action_type,description,product_variant_id,lot_id,quantity,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, ncrId, request.ActionType, request.Description.Trim(), Db(request.ProductVariantId), Db(request.LotId), Db(request.Quantity), actor); await ExecuteAsync(context, "update business_quality.nonconformance set state='Contained' where tenant_id=$1 and id=$2", token, tenant.Value, ncrId); return (object)new { id, ncrId, request.ActionType, state = "Open" }; }, ct);

    public Task<CapaView> CreateCapaAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateCapaRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var number = $"CAPA-{id.ToString("N")[..12].ToUpperInvariant()}"; await ExecuteAsync(context, "insert into business_quality.capa(tenant_id,id,number,legal_entity_id,type,title,problem_statement,ncr_id,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, legalEntityId, request.Type, request.Title.Trim(), request.ProblemStatement.Trim(), Db(request.NcrId), actor); return new CapaView(id, number, request.Type, request.Title.Trim(), "Open", request.NcrId, DateTimeOffset.UtcNow); }, ct);
    public Task<object> AddCapaRootCauseAsync(TenantId tenant, Guid actor, Guid capaId, CreateCapaRootCauseRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.capa_root_cause(tenant_id,id,capa_id,category,description,verified) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, capaId, request.Category.Trim(), request.Description.Trim(), request.Verified); await ExecuteAsync(context, "update business_quality.capa set state='Investigating' where tenant_id=$1 and id=$2 and state='Open'", token, tenant.Value, capaId); return (object)new { id, capaId, state = "Investigating" }; }, ct);
    public Task<CapaActionView> AddCapaActionAsync(TenantId tenant, Guid actor, Guid capaId, CreateCapaActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.capa_action(tenant_id,id,capa_id,action_type,description,owner_id,due_date) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, capaId, request.ActionType, request.Description.Trim(), request.OwnerId, request.DueDate); await ExecuteAsync(context, "update business_quality.capa set state='ActionPending' where tenant_id=$1 and id=$2", token, tenant.Value, capaId); return new CapaActionView(id, capaId, request.ActionType, "Open"); }, ct);
    public Task<CapaActionView> CompleteCapaActionAsync(TenantId tenant, Guid actor, Guid actionId, CompleteCapaActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var capaId = await ScalarAsync<Guid?>(context, "select capa_id from business_quality.capa_action where tenant_id=$1 and id=$2", token, tenant.Value, actionId) ?? throw new QualityException("quality.capa_action_not_found"); var actionType = await ScalarAsync<string?>(context, "select action_type from business_quality.capa_action where tenant_id=$1 and id=$2", token, tenant.Value, actionId) ?? throw new QualityException("quality.capa_action_not_found"); if (await ExecuteAsync(context, "update business_quality.capa_action set state='Completed',completed_at=now() where tenant_id=$1 and id=$2 and state='Open'", token, tenant.Value, actionId) != 1) throw new QualityException("quality.capa_action_not_open"); await ExecuteAsync(context, "update business_quality.capa set state='EffectivenessPending' where tenant_id=$1 and id=$2 and not exists(select 1 from business_quality.capa_action where tenant_id=$1 and capa_id=$2 and state='Open')", token, tenant.Value, capaId); return new CapaActionView(actionId, capaId, actionType, "Completed"); }, ct);
    public Task<object> RecordEffectivenessAsync(TenantId tenant, Guid actor, Guid capaId, EffectivenessCheckRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var state = await ScalarAsync<string?>(context, "select state from business_quality.capa where tenant_id=$1 and id=$2", token, tenant.Value, capaId) ?? throw new QualityException("quality.capa_not_found"); if (state != "EffectivenessPending" && request.Effective) throw new QualityException("quality.capa_actions_incomplete"); var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.capa_effectiveness_check(tenant_id,id,capa_id,effective,evidence_reference,notes,checked_by) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, capaId, request.Effective, request.EvidenceReference.Trim(), request.Notes.Trim(), actor); await ExecuteAsync(context, "update business_quality.capa set state=$3,closed_at=case when $3='Closed' then now() else null end where tenant_id=$1 and id=$2", token, tenant.Value, capaId, request.Effective ? "Closed" : "Investigating"); return (object)new { id, capaId, request.Effective, state = request.Effective ? "Closed" : "Investigating" }; }, ct);

    public Task<object> SaveSupplierProfileAsync(TenantId tenant, Guid actor, CreateSupplierProfileRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = await ScalarAsync<Guid?>(context, "select id from business_quality.supplier_quality_profile where tenant_id=$1 and legal_entity_id=$2 and supplier_party_id=$3", token, tenant.Value, request.LegalEntityId, request.SupplierPartyId) ?? Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.supplier_quality_profile(tenant_id,id,legal_entity_id,supplier_party_id,risk_level,target_ppm,receiving_inspection_required) values($1,$2,$3,$4,$5,$6,$7) on conflict(tenant_id,legal_entity_id,supplier_party_id) do update set risk_level=excluded.risk_level,target_ppm=excluded.target_ppm,receiving_inspection_required=excluded.receiving_inspection_required,row_version=business_quality.supplier_quality_profile.row_version+1", token, tenant.Value, id, request.LegalEntityId, request.SupplierPartyId, request.RiskLevel, request.TargetPpm, request.ReceivingInspectionRequired); return (object)new { id, request.SupplierPartyId, request.RiskLevel, request.ReceivingInspectionRequired }; }, ct);
    public Task<QualityScarView> CreateScarAsync(TenantId tenant, Guid actor, CreateScarRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var number = $"SCAR-{id.ToString("N")[..12].ToUpperInvariant()}"; await ExecuteAsync(context, "insert into business_quality.supplier_corrective_action_request(tenant_id,id,number,legal_entity_id,supplier_party_id,title,description,ncr_id,due_date) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, request.LegalEntityId, request.SupplierPartyId, request.Title.Trim(), request.Description.Trim(), Db(request.NcrId), Db(request.DueDate)); return new QualityScarView(id, number, request.LegalEntityId, request.SupplierPartyId, request.Title.Trim(), "Open", request.NcrId, request.DueDate); }, ct);
    public Task<QualityScarView> SubmitScarResponseAsync(TenantId tenant, Guid actor, Guid scarId, SupplierScarResponseRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { if (string.IsNullOrWhiteSpace(request.Response) || string.IsNullOrWhiteSpace(request.EvidenceReference)) throw new QualityException("quality.scar_response_required"); var row = await ReadSingleAsync(context, "select number,legal_entity_id,supplier_party_id,title,ncr_id,due_date,state from business_quality.supplier_corrective_action_request where tenant_id=$1 and id=$2", token, tenant.Value, scarId) ?? throw new QualityException("quality.scar_not_found"); if ((string)row[6] is not ("Open" or "Rejected")) throw new QualityException("quality.scar_response_not_allowed"); await ExecuteAsync(context, "update business_quality.supplier_corrective_action_request set state='Submitted',supplier_response=$3,response_evidence_reference=$4,responded_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, scarId, request.Response.Trim(), request.EvidenceReference.Trim()); return new QualityScarView(scarId, (string)row[0], (Guid)row[1], (Guid)row[2], (string)row[3], "Submitted", row[4] is DBNull ? null : (Guid)row[4], row[5] is DBNull ? null : (DateOnly)row[5]); }, ct);
    public Task<QualityScarView> ReviewScarAsync(TenantId tenant, Guid actor, Guid scarId, SupplierScarReviewRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var state = request.Decision is "Accept" or "Close" ? "Accepted" : request.Decision == "Reject" ? "Rejected" : throw new QualityException("quality.scar_review_invalid"); var row = await ReadSingleAsync(context, "select number,legal_entity_id,supplier_party_id,title,ncr_id,due_date,state from business_quality.supplier_corrective_action_request where tenant_id=$1 and id=$2", token, tenant.Value, scarId) ?? throw new QualityException("quality.scar_not_found"); if ((string)row[6] != "Submitted") throw new QualityException("quality.scar_review_not_allowed"); await ExecuteAsync(context, "update business_quality.supplier_corrective_action_request set state=$3,reviewed_by=$4,reviewed_at=now(),review_notes=$5 where tenant_id=$1 and id=$2", token, tenant.Value, scarId, state, actor, request.Notes.Trim()); return new QualityScarView(scarId, (string)row[0], (Guid)row[1], (Guid)row[2], (string)row[3], state, row[4] is DBNull ? null : (Guid)row[4], row[5] is DBNull ? null : (DateOnly)row[5]); }, ct);
    public Task<QualityCertificateView> IssueCertificateAsync(TenantId tenant, Guid actor, IssueCertificateRequest request, Guid legalEntityId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var inspection = await ReadSingleAsync(context, "select legal_entity_id,decision_state from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, request.InspectionId) ?? throw new QualityException("quality.inspection_not_found");
        if ((Guid)inspection[0] != legalEntityId) throw new QualityException("quality.inspection_legal_entity_mismatch");
        if ((string)inspection[1] != "Passed") throw new QualityException("quality.certificate_requires_passed_inspection");
        var inspectionView = await ReadInspectionAsync(context, tenant.Value, request.InspectionId, token) ?? throw new QualityException("quality.inspection_not_found");
        var snapshot = JsonSerializer.Serialize(new { request.InspectionId, request.CertificateType, inspection = inspectionView, capturedAt = DateTimeOffset.UtcNow });
        var id = Guid.CreateVersion7(); var number = $"{request.CertificateType}-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.quality_certificate(tenant_id,id,legal_entity_id,number,certificate_type,inspection_id,customer_reference,state,created_by,issued_at) values($1,$2,$3,$4,$5,$6,$7,'Issued',$8,now())", token, tenant.Value, id, legalEntityId, number, request.CertificateType, request.InspectionId, Db(request.CustomerReference), actor);
        await ExecuteAsync(context, "insert into business_quality.quality_certificate_version(tenant_id,id,certificate_id,version_no,snapshot,created_by) values($1,$2,$3,1,$4::jsonb,$5)", token, tenant.Value, Guid.CreateVersion7(), id, snapshot, actor);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityCertificateIssuedV1", JsonSerializer.Serialize(new { certificateId = id, number, request.InspectionId }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return new QualityCertificateView(id, number, request.CertificateType, "Issued", request.InspectionId, request.CustomerReference, DateTimeOffset.UtcNow);
    }, ct);

    public async Task<QualityReceiptResolution> ResolvePurchaseReceiptAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid purchaseOrderId, Guid supplierPartyId, string idempotencyKey, IReadOnlyList<QualityReceiptLine> lines, CancellationToken cancellationToken = default)
    {
        return await coordinator.ExecuteAsync(new TenantId(tenantId), async (context, token) =>
        {
            if (lines.Count == 0 || lines.Any(x => x.Quantity <= 0m) || lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != lines.Count) throw new QualityException("quality.receipt_lines_invalid");
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) throw new QualityException("quality.idempotency_key_invalid");
            var initialStatuses = new Dictionary<Guid, string>();
            var reasons = new List<string>();
            var inspections = new List<Guid>();
            foreach (var line in lines)
            {
                var required = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.supplier_quality_profile where tenant_id=$1 and legal_entity_id=$2 and supplier_party_id=$3 and receiving_inspection_required) or exists(select 1 from business_quality.quality_plan p join business_quality.quality_plan_applicability a on a.tenant_id=p.tenant_id and a.plan_id=p.id where p.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type='Receiving' and p.status='Active' and p.mandatory and (a.supplier_party_id=$3 or a.product_variant_id=$4))", token, tenantId, legalEntityId, supplierPartyId, line.ProductVariantId), CultureInfo.InvariantCulture);
                initialStatuses[line.PurchaseOrderLineId] = required ? "QualityHold" : "Available";
                if (!required) continue;
                reasons.Add($"line:{line.PurchaseOrderLineId:D}");
                var sourceFingerprint = $"receiving:{purchaseOrderId:D}:{idempotencyKey}:{line.PurchaseOrderLineId:D}";
                var existingInspectionId = await ScalarAsync<Guid?>(context, "select id from business_quality.inspection where tenant_id=$1 and event_type='Receiving' and source_fingerprint=$2 order by created_at desc,id desc limit 1", token, tenantId, sourceFingerprint);
                if (existingInspectionId is { } existingId) inspections.Add(existingId);
                else
                {
                    var inspection = await CreateInspectionInContextAsync(context, new TenantId(tenantId), actorId, new CreateInspectionRequest(legalEntityId, "Receiving", line.ProductVariantId, line.Quantity, purchaseOrderId, SupplierPartyId: supplierPartyId, LotReference: line.LotId?.ToString("D"), SourceFingerprint: sourceFingerprint), token);
                    inspections.Add(inspection.Id);
                }
            }
            var anyRequired = initialStatuses.Values.Any(x => x == "QualityHold");
            return new QualityReceiptResolution(purchaseOrderId, legalEntityId, supplierPartyId, lines, anyRequired, anyRequired ? "QualityHold" : "Available", anyRequired ? $"Mandatory receiving inspection ({string.Join(',', reasons)})" : null, inspections, initialStatuses);
        }, cancellationToken);
    }

    public Task<QualityDecisionSnapshot?> GetProductionDecisionAsync(TenantId tenantId, Guid legalEntityId, Guid productionOrderId, Guid? productionOperationId, CancellationToken cancellationToken = default) => coordinator.ExecuteAsync(tenantId, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select i.id,i.decision_state from business_quality.inspection i where i.tenant_id=$1 and i.legal_entity_id=$2 and i.production_order_id=$3 and i.production_operation_id is not distinct from $4::uuid and i.state='Completed' order by i.completed_at desc,i.id desc limit 1", token, tenantId.Value, legalEntityId, productionOrderId, Db(productionOperationId));
        return row is null ? null : new QualityDecisionSnapshot(productionOrderId, productionOperationId, true, (string)row[1], $"inspection:{(Guid)row[0]:D}:evaluation");
    }, cancellationToken);

    private static async Task<InspectionView> CreateInspectionInContextAsync(ITransactionContext context, TenantId tenant, Guid actor, CreateInspectionRequest request, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceFingerprint))
        {
            var existingId = await ScalarAsync<Guid?>(context, "select id from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2 limit 1", token, tenant.Value, request.SourceFingerprint);
            if (existingId is { } idempotentId) return await ReadInspectionAsync(context, tenant.Value, idempotentId, token) ?? throw new QualityException("quality.inspection_not_found");
        }
        var planVersionId = request.PlanVersionId is { } explicitPlan
            ? await ValidateExplicitPlanVersionAsync(context, tenant.Value, explicitPlan, request, token)
            : await ResolvePlanVersionAsync(context, tenant.Value, request, token);
        if (planVersionId is null && request.EventType is not "AdHoc") throw new QualityException("quality.plan_not_configured");
        var samplingId = request.SamplingPlanVersionId; var sampleSize = samplingId is null ? (int)Math.Ceiling(request.LotQuantity) : await CalculateSampleSizeInContextAsync(context, tenant.Value, samplingId.Value, request.LotQuantity, token);
        var id = Guid.CreateVersion7(); var number = $"QI-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.inspection(tenant_id,id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,customer_party_id,plan_version_id,sampling_plan_version_id,lot_quantity,sample_size,lot_reference,source_fingerprint,created_by) values($1,$2,$3,$4,$5,'InProgress','Pending',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18)", token, tenant.Value, id, request.LegalEntityId, number, request.EventType, Db(request.ProductVariantId), Db(request.SourceDocumentId), Db(request.ProductionOrderId), Db(request.ProductionOperationId), Db(request.SupplierPartyId), Db(request.CustomerPartyId), Db(planVersionId), Db(samplingId), request.LotQuantity, sampleSize, Db(request.LotReference), Db(request.SourceFingerprint ?? request.SourceDocumentId?.ToString("D")), actor);
        if (planVersionId is { } pvid) await ExecuteAsync(context, "insert into business_quality.inspection_plan_snapshot(tenant_id,id,inspection_id,plan_id,plan_version_id,snapshot) select $1,$2,$3,plan_id,$4,$5::jsonb from business_quality.quality_plan_version where tenant_id=$1 and id=$4", token, tenant.Value, Guid.CreateVersion7(), id, pvid, await ReadPlanSnapshotAsync(context, tenant.Value, pvid, token));
        var quantity = sampleSize == 0 ? request.LotQuantity : Math.Min(request.LotQuantity, request.LotQuantity / Math.Max(1, sampleSize));
        for (var i = 1; i <= Math.Max(1, sampleSize); i++) await ExecuteAsync(context, "insert into business_quality.quality_sample(tenant_id,id,inspection_id,sample_no,quantity) values($1,$2,$3,$4,$5)", token, tenant.Value, Guid.CreateVersion7(), id, i, quantity);
        return await ReadInspectionAsync(context, tenant.Value, id, token) ?? throw new QualityException("quality.inspection_not_found");
    }

    private static async Task<Guid?> ResolvePlanVersionAsync(ITransactionContext context, Guid tenant, CreateInspectionRequest request, CancellationToken token)
    {
        var candidates = new List<(Guid Id, int Specificity)>();
        await using var command = Command(context, "select distinct v.id,(case when a.product_variant_id is not null then 1 else 0 end + case when a.supplier_party_id is not null then 1 else 0 end) as specificity from business_quality.quality_plan_version v join business_quality.quality_plan p on p.tenant_id=v.tenant_id and p.id=v.plan_id left join business_quality.quality_plan_applicability a on a.tenant_id=p.tenant_id and a.plan_id=p.id where v.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type=$3 and p.status='Active' and v.status='Active' and v.effective_from<=current_date and (v.effective_to is null or v.effective_to>=current_date) and (a.product_variant_id is null or a.product_variant_id=$4) and (a.supplier_party_id is null or a.supplier_party_id=$5) order by specificity desc,v.id", tenant, request.LegalEntityId, request.EventType, Db(request.ProductVariantId), Db(request.SupplierPartyId));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) candidates.Add((reader.GetGuid(0), reader.GetInt32(1)));
        if (candidates.Count > 1 && candidates[0].Specificity == candidates[1].Specificity) throw new QualityException("quality.plan_conflict");
        return candidates.Count == 0 ? null : candidates[0].Id;
    }
    private static async Task<Guid> ValidateExplicitPlanVersionAsync(ITransactionContext context, Guid tenant, Guid versionId, CreateInspectionRequest request, CancellationToken token)
    {
        var valid = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_plan_version v join business_quality.quality_plan p on p.tenant_id=v.tenant_id and p.id=v.plan_id where v.tenant_id=$1 and v.id=$2 and p.legal_entity_id=$3 and p.event_type=$4 and p.status='Active' and v.status='Active' and v.effective_from<=current_date and (v.effective_to is null or v.effective_to>=current_date))", token, tenant, versionId, request.LegalEntityId, request.EventType), CultureInfo.InvariantCulture);
        if (!valid) throw new QualityException("quality.plan_version_not_applicable");
        return versionId;
    }
    private static async Task<int> CalculateSampleSizeInContextAsync(ITransactionContext context, Guid tenant, Guid samplingId, decimal lot, CancellationToken token) { var row = await ReadSingleAsync(context, "select p.method,p.fixed_quantity,p.percentage from business_quality.sampling_plan_version v join business_quality.sampling_plan p on p.tenant_id=v.tenant_id and p.id=v.sampling_plan_id where v.tenant_id=$1 and v.id=$2", token, tenant, samplingId) ?? throw new QualityException("quality.sampling_plan_not_found"); return QualityRules.CalculateSampleSize((string)row[0] == "Aql" ? "Full" : (string)row[0], lot, row[1] is DBNull ? null : (decimal?)row[1], row[2] is DBNull ? null : (decimal?)row[2]); }
    private static async Task<string> ReadPlanSnapshotAsync(ITransactionContext context, Guid tenant, Guid planVersionId, CancellationToken token) { var rows = new List<object>(); await using var command = Command(context, "select c.code,c.name,c.data_type,c.base_uom_code,pc.minimum,pc.maximum,pc.target,pc.allowed_values,pc.required,pc.evidence_required from business_quality.quality_plan_characteristic pc join business_quality.quality_characteristic c on c.tenant_id=pc.tenant_id and c.id=pc.characteristic_id where pc.tenant_id=$1 and pc.plan_version_id=$2 order by pc.sequence_no", tenant, planVersionId); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(new { code = reader.GetString(0), name = reader.GetString(1), dataType = reader.GetString(2), uom = reader.IsDBNull(3) ? null : reader.GetString(3), minimum = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4), maximum = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5), target = reader.IsDBNull(6) ? null : reader.GetString(6), allowedValues = reader.IsDBNull(7) ? (JsonElement?)null : reader.GetFieldValue<JsonElement>(7), required = reader.GetBoolean(8), evidenceRequired = reader.GetBoolean(9) }); return JsonSerializer.Serialize(new { planVersionId, characteristics = rows }); }
    private static async Task<InspectionView?> ReadInspectionAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { var row = await ReadSingleAsync(context, "select id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,lot_quantity,sample_size,lot_reference,row_version from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant, id); if (row is null) return null; var results = new List<InspectionResultView>(); await using var command = Command(context, "select id,characteristic_id,numeric_value,text_value,boolean_value,passed,uom_code,evidence_reference from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2 order by recorded_at,id", tenant, id); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) results.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7))); return new InspectionView((Guid)row[0], (Guid)row[1], (string)row[2], (string)row[3], (string)row[4], (string)row[5], row[6] is DBNull ? null : (Guid)row[6], row[7] is DBNull ? null : (Guid)row[7], row[8] is DBNull ? null : (Guid)row[8], row[9] is DBNull ? null : (Guid)row[9], row[10] is DBNull ? null : (Guid)row[10], (decimal)row[11], (int)row[12], row[13] is DBNull ? null : (string)row[13], results, (long)row[14]); }
    private static HashSet<string> ParseAllowed(object? json) { if (json is null or DBNull) return new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { var text = json is JsonElement element ? element.GetRawText() : json.ToString(); return JsonSerializer.Deserialize<HashSet<string>>(text!) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase); } catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); } }
    private static async Task<object[]?> ReadSingleAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray() : null; }
    private static async Task<T?> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { var value = await ScalarAsync(context, sql, token, values); return value is null or DBNull ? default : (T)value; }
    private static async Task<object?> ScalarAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteScalarAsync(token); }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteNonQueryAsync(token); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); return command; }
    private static object Db(object? value) => value ?? DBNull.Value;
    private static string ReleaseRequestHash(Guid restrictionId, string reason, decimal quantity, Guid unitOfMeasureId, DateOnly operationalDate, DateOnly postingDate)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { restrictionId, reason, quantity, unitOfMeasureId, operationalDate, postingDate })))).ToLowerInvariant();
    private sealed record ReleaseClaim(bool RequiresOwner, bool AlreadyConfirmed, Guid OperationId, Guid LegalEntityId, string RestrictionType, string Reason, Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, Guid? LotId, Guid? SerialId, Guid UnitOfMeasureId, decimal Quantity, DateOnly OperationalDate, DateOnly PostingDate)
    {
        public static ReleaseClaim NonOwner(object[] row) => new(false, false, Guid.Empty, (Guid)row[0], (string)row[1], string.Empty, Guid.Empty, Guid.Empty, null, null, null, Guid.Empty, 0m, default, default);
        public static ReleaseClaim Confirmed(object[] row, Guid operationId, Guid? ownerOperationId) => new(true, true, operationId, (Guid)row[0], (string)row[1], string.Empty, (Guid)row[4], (Guid)row[5], row[6] is DBNull ? null : (Guid)row[6], row[7] is DBNull ? null : (Guid)row[7], row[8] is DBNull ? null : (Guid)row[8], Guid.Empty, 0m, default, default);
        public static ReleaseClaim Owner(object[] row, Guid operationId, string reason, decimal quantity, Guid unitOfMeasureId, DateOnly operationalDate, DateOnly postingDate)
            => new(true, false, operationId, (Guid)row[0], (string)row[1], reason, (Guid)row[4], (Guid)row[5], row[6] is DBNull ? null : (Guid)row[6], row[7] is DBNull ? null : (Guid)row[7], row[8] is DBNull ? null : (Guid)row[8], unitOfMeasureId, quantity, operationalDate, postingDate);
    }
    private static async Task<QualityRestrictionView?> ReadRestrictionAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        var row = await ReadSingleAsync(context, "select r.id,r.restriction_type,r.status,r.reason_code,r.product_variant_id,r.lot_id,r.quantity,r.inspection_id,r.created_at,r.released_at,r.released_quantity,r.owner_execution_status,r.inventory_operation_id,(select o.id from business_quality.quality_restriction_release_operation o where o.tenant_id=r.tenant_id and o.restriction_id=r.id order by o.created_at desc,o.id desc limit 1) from business_quality.quality_restriction r where r.tenant_id=$1 and r.id=$2", token, tenant, id);
        return row is null ? null : new QualityRestrictionView((Guid)row[0], (string)row[1], (string)row[2], (string)row[3], row[4] is DBNull ? null : (Guid)row[4], row[5] is DBNull ? null : (Guid)row[5], row[6] is DBNull ? null : (decimal)row[6], row[7] is DBNull ? null : (Guid)row[7], Timestamp(row[8]), row[9] is DBNull ? null : Timestamp(row[9]), (decimal)row[10], (string)row[11], row[12] is DBNull ? null : (Guid)row[12], row[13] is DBNull ? null : (Guid)row[13]);
    }
    private Task<bool> MarkRestrictionDisposedAsync(TenantId tenant, Guid actor, Guid restrictionId, decimal quantity, string reason, Guid ownerOperationId, CancellationToken token)
        => coordinator.ExecuteAsync(tenant, async (context, ct) =>
        {
            var row = await ReadSingleAsync(context, "select quantity,released_quantity,status from business_quality.quality_restriction where tenant_id=$1 and id=$2 for update", ct, tenant.Value, restrictionId) ?? throw new QualityException("quality.restriction_not_found");
            if ((string)row[2] != "Active") return true;
            var total = (decimal)row[1] + quantity;
            if (total > (decimal)row[0]) throw new QualityException("quality.disposition_quantity_conflict");
            await ExecuteAsync(context, "update business_quality.quality_restriction set status=case when released_quantity+$3>=quantity then 'Released' else status end,released_quantity=released_quantity+$3,released_by=$4,released_at=case when released_quantity+$3>=quantity then now() else released_at end,release_reason=$5,owner_execution_status='Confirmed',inventory_operation_id=$6 where tenant_id=$1 and id=$2", ct, tenant.Value, restrictionId, quantity, actor, reason, ownerOperationId);
            return true;
        }, token);
    private static DateTimeOffset Timestamp(object value) => value switch { DateTimeOffset timestamp => timestamp, DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)), _ => throw new InvalidCastException($"Unsupported timestamp type {value.GetType().Name}.") };
    private static string EncodePlanCursor(QualityPlanListItem item) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{item.Code}\n{item.Id:D}"));
    private static (string Code, Guid Id)? DecodePlanCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('\n');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return (parts[0], id);
        }
        catch (FormatException) { throw new QualityException("quality.cursor_invalid"); }
    }
    private static string EncodeInspectionCursor(InspectionListItem item) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{item.CreatedAt:O}\n{item.Id:D}"));
    private static string EncodeDateCursor(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt:O}\n{id:D}"));
    private static (DateTimeOffset CreatedAt, Guid Id)? DecodeDateCursor(string? cursor) => DecodeInspectionCursor(cursor);
    private static (DateTimeOffset CreatedAt, Guid Id)? DecodeInspectionCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('\n');
            if (parts.Length != 2 || !DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt) || !Guid.TryParse(parts[1], out var id)) throw new FormatException();
            return (createdAt, id);
        }
        catch (FormatException) { throw new QualityException("quality.cursor_invalid"); }
    }
    private static void ValidateText(string value, int max) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new QualityException("quality.text_invalid"); }
    private static Task<int> AuditAsync(ITransactionContext context, Guid tenant, string type, Guid id, string action, Guid actor, object? after, CancellationToken token) => ExecuteAsync(context, "insert into business_quality.quality_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,after_json) values($1,$2,$3,$4,$5,$6,$7::jsonb)", token, tenant, Guid.CreateVersion7(), type, id, action, actor, after is null ? null : JsonSerializer.Serialize(after));
}
