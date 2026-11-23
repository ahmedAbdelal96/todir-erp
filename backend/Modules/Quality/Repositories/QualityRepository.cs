using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using Modules.Quality.DTOs;
using Modules.Quality.Entities;
using Npgsql;
using Shared.Contracts.Quality;
using Shared.Primitives.Identifiers;

namespace Modules.Quality.Repositories;

public sealed class QualityRepository(ITransactionCoordinator coordinator) : IQualityDecisionCapabilities, IQualityReceivingCapabilities
{
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

    public Task<IReadOnlyList<object>> ListPlansAsync(TenantId tenant, Guid legalEntityId, string? eventType, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var result = new List<object>();
        await using var command = Command(context, "select id,code,name,event_type,mandatory,status,row_version from business_quality.quality_plan where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or event_type=$3) order by code limit 500", tenant.Value, legalEntityId, Db(eventType));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new { id = reader.GetGuid(0), code = reader.GetString(1), name = reader.GetString(2), eventType = reader.GetString(3), mandatory = reader.GetBoolean(4), status = reader.GetString(5), rowVersion = reader.GetInt64(6) });
        return (IReadOnlyList<object>)result;
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
        var planId = await ScalarAsync<Guid?>(context, "select plan_id from business_quality.quality_plan_version where tenant_id=$1 and id=$2", token, tenant.Value, versionId) ?? throw new QualityException("quality.plan_version_not_found");
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
        await ExecuteAsync(context, "insert into business_quality.quality_plan_characteristic(tenant_id,id,plan_version_id,characteristic_id,specification_version_id,minimum,maximum,target,allowed_values,required) values($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10)", token, tenant.Value, id, planVersionId, request.CharacteristicId, Db(request.SpecificationVersionId), Db(request.Minimum), Db(request.Maximum), Db(request.Target), Db(request.AllowedValuesJson), request.Required);
        return (object)new { id, planVersionId, request.CharacteristicId, request.Minimum, request.Maximum, request.Required };
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
        var planVersionId = request.PlanVersionId ?? await ResolvePlanVersionAsync(context, tenant.Value, request, token);
        var samplingId = request.SamplingPlanVersionId;
        var sampleSize = samplingId is null ? (int)Math.Ceiling(request.LotQuantity) : await CalculateSampleSizeInContextAsync(context, tenant.Value, samplingId.Value, request.LotQuantity, token);
        var id = Guid.CreateVersion7(); var number = $"QI-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.inspection(tenant_id,id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,customer_party_id,plan_version_id,sampling_plan_version_id,lot_quantity,sample_size,lot_reference,source_fingerprint,created_by) values($1,$2,$3,$4,$5,'InProgress','Pending',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18)", token, tenant.Value, id, request.LegalEntityId, number, request.EventType, Db(request.ProductVariantId), Db(request.SourceDocumentId), Db(request.ProductionOrderId), Db(request.ProductionOperationId), Db(request.SupplierPartyId), Db(request.CustomerPartyId), Db(planVersionId), Db(samplingId), request.LotQuantity, sampleSize, Db(request.LotReference), Db(request.SourceDocumentId?.ToString("D")), actor);
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

    public Task<IReadOnlyList<InspectionListItem>> ListInspectionsAsync(TenantId tenant, Guid legalEntityId, string? state, string? eventType, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var items = new List<InspectionListItem>();
        await using var command = Command(context, "select id,number,event_type,state,decision_state,product_variant_id,source_document_id,lot_quantity,sample_size,created_at from business_quality.inspection where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::text is null or event_type=$4) order by created_at desc,id desc limit 500", tenant.Value, legalEntityId, Db(state), Db(eventType));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetDecimal(7), reader.GetInt32(8), reader.GetFieldValue<DateTimeOffset>(9)));
        return (IReadOnlyList<InspectionListItem>)items;
    }, ct);

    public Task<InspectionView?> GetInspectionAsync(TenantId tenant, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadInspectionAsync(context, tenant.Value, id, token), ct);

    public Task<InspectionResultView> AddResultAsync(TenantId tenant, Guid actor, Guid inspectionId, AddInspectionResultRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var inspection = await ReadSingleAsync(context, "select plan_version_id,state from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, inspectionId) ?? throw new QualityException("quality.inspection_not_found");
        if ((string)inspection[1] is not ("InProgress" or "Draft")) throw new QualityException("quality.inspection_immutable");
        var rule = await ReadSingleAsync(context, "select c.data_type,c.required,pc.minimum,pc.maximum,pc.allowed_values from business_quality.quality_characteristic c left join business_quality.quality_plan_characteristic pc on pc.tenant_id=c.tenant_id and pc.characteristic_id=c.id and pc.plan_version_id=$3 where c.tenant_id=$1 and c.id=$2", token, tenant.Value, request.CharacteristicId, inspection[0] is DBNull ? null : inspection[0]);
        if (rule is null) throw new QualityException("quality.characteristic_not_found");
        var numeric = request.NumericValue; var text = request.TextValue; var boolean = request.BooleanValue;
        if ((numeric is not null ? 1 : 0) + (text is not null ? 1 : 0) + (boolean is not null ? 1 : 0) != 1) throw new QualityException("quality.result_value_required");
        var passed = rule[0] switch { "Numeric" => QualityRules.EvaluateNumeric(numeric, rule[2] as decimal?, rule[3] as decimal?, Convert.ToBoolean(rule[1], CultureInfo.InvariantCulture)), "Categorical" => QualityRules.EvaluateCategorical(text, ParseAllowed(rule[4]), Convert.ToBoolean(rule[1], CultureInfo.InvariantCulture)), "Boolean" => boolean == true, _ => !string.IsNullOrWhiteSpace(text) };
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.inspection_result(tenant_id,id,inspection_id,characteristic_id,numeric_value,text_value,boolean_value,uom_code,passed,evidence_reference,recorded_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, inspectionId, request.CharacteristicId, Db(numeric), Db(text), Db(boolean), Db(request.UomCode), passed, Db(request.EvidenceReference), actor);
        return new InspectionResultView(id, request.CharacteristicId, numeric, text, boolean, passed, request.UomCode, request.EvidenceReference);
    }, ct);

    public Task<InspectionView> CompleteInspectionAsync(TenantId tenant, Guid actor, Guid inspectionId, CompleteInspectionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var existing = await ReadSingleAsync(context, "select plan_version_id,state,production_order_id,production_operation_id from business_quality.inspection where tenant_id=$1 and id=$2 for update", token, tenant.Value, inspectionId) ?? throw new QualityException("quality.inspection_not_found");
        if ((string)existing[1] is not ("InProgress" or "Draft")) throw new QualityException("quality.inspection_immutable");
        var failed = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2 and passed=false)", token, tenant.Value, inspectionId), CultureInfo.InvariantCulture);
        var missing = existing[0] is not DBNull && Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.quality_plan_characteristic pc where pc.tenant_id=$1 and pc.plan_version_id=$3 and pc.required and not exists(select 1 from business_quality.inspection_result r where r.tenant_id=$1 and r.inspection_id=$2 and r.characteristic_id=pc.characteristic_id))", token, tenant.Value, inspectionId, existing[0]), CultureInfo.InvariantCulture);
        var decision = failed || missing ? "Failed" : "Passed"; var evidence = $"inspection:{inspectionId:D}:evaluation";
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
        var request2 = new CreateInspectionRequest((Guid)source[0], (string)source[1], source[2] is DBNull ? null : (Guid)source[2], (decimal)source[8], source[3] is DBNull ? null : (Guid)source[3], source[4] is DBNull ? null : (Guid)source[4], source[5] is DBNull ? null : (Guid)source[5], source[6] is DBNull ? null : (Guid)source[6], source[7] is DBNull ? null : (Guid)source[7], source[9] is DBNull ? null : (Guid)source[9], source[10] is DBNull ? null : (Guid)source[10], $"reinspection:{inspectionId:D}:{request.Reason.Trim()}");
        return await CreateInspectionInContextAsync(context, tenant, actor, request2, token);
    }, ct);

    public Task<QualityRestrictionView> CreateRestrictionAsync(TenantId tenant, Guid actor, CreateRestrictionRequest request, Guid legalEntityId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.RestrictionType is not ("QualityHold" or "Quarantine" or "Blocked" or "ReturnsInspection")) throw new QualityException("quality.restriction_type_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_restriction(tenant_id,id,legal_entity_id,restriction_type,reason_code,product_variant_id,warehouse_id,location_id,lot_id,serial_id,inspection_id,quantity,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", token, tenant.Value, id, legalEntityId, request.RestrictionType, request.ReasonCode.Trim(), Db(request.ProductVariantId), Db(request.WarehouseId), Db(request.LocationId), Db(request.LotId), Db(request.SerialId), Db(request.InspectionId), Db(request.Quantity), actor);
        await AuditAsync(context, tenant.Value, "QualityRestriction", id, "created", actor, request, token);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionCreatedV1", JsonSerializer.Serialize(new { restrictionId = id, legalEntityId, request.ProductVariantId, request.WarehouseId, request.LocationId, request.LotId, request.SerialId, request.Quantity, request.RestrictionType }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return new QualityRestrictionView(id, request.RestrictionType, "Active", request.ReasonCode, request.ProductVariantId, request.LotId, request.Quantity, request.InspectionId, DateTimeOffset.UtcNow, null);
    }, ct);

    public Task<QualityRestrictionView> ReleaseRestrictionAsync(TenantId tenant, Guid actor, Guid id, ReleaseRestrictionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new QualityException("quality.release_reason_required");
        var updated = await ExecuteAsync(context, "update business_quality.quality_restriction set status='Released',released_by=$3,released_at=now(),release_reason=$4 where tenant_id=$1 and id=$2 and status='Active'", token, tenant.Value, id, actor, request.Reason.Trim());
        if (updated != 1) throw new QualityException("quality.restriction_not_active");
        var row = await ReadSingleAsync(context, "select restriction_type,status,reason_code,product_variant_id,lot_id,quantity,inspection_id,created_at,released_at from business_quality.quality_restriction where tenant_id=$1 and id=$2", token, tenant.Value, id) ?? throw new QualityException("quality.restriction_not_found");
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityRestrictionReleasedV1", JsonSerializer.Serialize(new { restrictionId = id, reason = request.Reason }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return new QualityRestrictionView(id, (string)row[0], (string)row[1], (string)row[2], row[3] is DBNull ? null : (Guid)row[3], row[4] is DBNull ? null : (Guid)row[4], row[5] is DBNull ? null : (decimal)row[5], row[6] is DBNull ? null : (Guid)row[6], (DateTimeOffset)row[7], row[8] is DBNull ? null : (DateTimeOffset)row[8]);
    }, ct);

    public Task<QualityDispositionView> CreateDispositionAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateDispositionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Decision is not ("Accept" or "Reject" or "UseAsIs" or "Concession" or "Return" or "Scrap" or "Rework" or "Partial")) throw new QualityException("quality.disposition_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_quality.quality_disposition(tenant_id,id,legal_entity_id,inspection_id,decision,quantity,reason,external_reference,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, legalEntityId, Db(request.InspectionId), request.Decision, Db(request.Quantity), request.Reason.Trim(), Db(request.ExternalReference), actor);
        return new QualityDispositionView(id, request.InspectionId, request.Decision, "Draft", request.Quantity, request.Reason.Trim(), DateTimeOffset.UtcNow);
    }, ct);

    public Task<QualityDispositionView> ExecuteDispositionAsync(TenantId tenant, Guid actor, Guid id, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select inspection_id,decision,quantity,reason,created_at from business_quality.quality_disposition where tenant_id=$1 and id=$2", token, tenant.Value, id) ?? throw new QualityException("quality.disposition_not_found");
        if (await ExecuteAsync(context, "update business_quality.quality_disposition set state='Executed',executed_by=$3,executed_at=now() where tenant_id=$1 and id=$2 and state in ('Draft','Approved')", token, tenant.Value, id, actor) != 1) throw new QualityException("quality.disposition_not_executable");
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityDispositionExecutedV1", JsonSerializer.Serialize(new { dispositionId = id, decision = (string)row[1] }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return new QualityDispositionView(id, row[0] is DBNull ? null : (Guid)row[0], (string)row[1], "Executed", row[2] is DBNull ? null : (decimal)row[2], (string)row[3], (DateTimeOffset)row[4]);
    }, ct);

    public Task<QualityIssueView> CreateIssueAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateIssueRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.quality_issue(tenant_id,id,legal_entity_id,issue_type,title,description,severity,inspection_id,supplier_party_id,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)", token, tenant.Value, id, legalEntityId, request.IssueType, request.Title.Trim(), request.Description.Trim(), request.Severity, Db(request.InspectionId), Db(request.SupplierPartyId), actor);
        return new QualityIssueView(id, request.IssueType, request.Title.Trim(), request.Severity, "Open", request.InspectionId, request.SupplierPartyId, DateTimeOffset.UtcNow);
    }, ct);

    public Task<object> CreateNcrAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateNcrRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); var number = $"NCR-{id.ToString("N")[..12].ToUpperInvariant()}";
        await ExecuteAsync(context, "insert into business_quality.nonconformance(tenant_id,id,number,legal_entity_id,issue_id,title,description,severity,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, Db(request.IssueId), request.Title.Trim(), request.Description.Trim(), request.Severity, actor);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityNcrCreatedV1", JsonSerializer.Serialize(new { nonconformanceId = id, number }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality"));
        return (object)new { id, number, request.Title, state = "Open" };
    }, ct);

    public Task<object> AddContainmentAsync(TenantId tenant, Guid actor, Guid ncrId, CreateContainmentRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.quality_containment_action(tenant_id,id,nonconformance_id,action_type,description,product_variant_id,lot_id,quantity,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, ncrId, request.ActionType, request.Description.Trim(), Db(request.ProductVariantId), Db(request.LotId), Db(request.Quantity), actor); await ExecuteAsync(context, "update business_quality.nonconformance set state='Contained' where tenant_id=$1 and id=$2", token, tenant.Value, ncrId); return (object)new { id, ncrId, request.ActionType, state = "Open" }; }, ct);

    public Task<CapaView> CreateCapaAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateCapaRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var number = $"CAPA-{id.ToString("N")[..12].ToUpperInvariant()}"; await ExecuteAsync(context, "insert into business_quality.capa(tenant_id,id,number,legal_entity_id,type,title,problem_statement,ncr_id,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, legalEntityId, request.Type, request.Title.Trim(), request.ProblemStatement.Trim(), Db(request.NcrId), actor); return new CapaView(id, number, request.Type, request.Title.Trim(), "Open", request.NcrId, DateTimeOffset.UtcNow); }, ct);
    public Task<object> AddCapaActionAsync(TenantId tenant, Guid actor, Guid capaId, CreateCapaActionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.capa_action(tenant_id,id,capa_id,action_type,description,owner_id,due_date) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, capaId, request.ActionType, request.Description.Trim(), request.OwnerId, request.DueDate); await ExecuteAsync(context, "update business_quality.capa set state='ActionPending' where tenant_id=$1 and id=$2", token, tenant.Value, capaId); return (object)new { id, capaId, request.ActionType, state = "Open" }; }, ct);
    public Task<object> RecordEffectivenessAsync(TenantId tenant, Guid actor, Guid capaId, EffectivenessCheckRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.capa_effectiveness_check(tenant_id,id,capa_id,effective,evidence_reference,notes,checked_by) values($1,$2,$3,$4,$5,$6,$7)", token, tenant.Value, id, capaId, request.Effective, request.EvidenceReference.Trim(), request.Notes.Trim(), actor); await ExecuteAsync(context, "update business_quality.capa set state=$3,closed_at=case when $3='Closed' then now() else null end where tenant_id=$1 and id=$2", token, tenant.Value, capaId, request.Effective ? "Closed" : "Investigating"); return (object)new { id, capaId, request.Effective, state = request.Effective ? "Closed" : "Investigating" }; }, ct);

    public Task<object> SaveSupplierProfileAsync(TenantId tenant, Guid actor, CreateSupplierProfileRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = await ScalarAsync<Guid?>(context, "select id from business_quality.supplier_quality_profile where tenant_id=$1 and legal_entity_id=$2 and supplier_party_id=$3", token, tenant.Value, request.LegalEntityId, request.SupplierPartyId) ?? Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_quality.supplier_quality_profile(tenant_id,id,legal_entity_id,supplier_party_id,risk_level,target_ppm,receiving_inspection_required) values($1,$2,$3,$4,$5,$6,$7) on conflict(tenant_id,legal_entity_id,supplier_party_id) do update set risk_level=excluded.risk_level,target_ppm=excluded.target_ppm,receiving_inspection_required=excluded.receiving_inspection_required,row_version=business_quality.supplier_quality_profile.row_version+1", token, tenant.Value, id, request.LegalEntityId, request.SupplierPartyId, request.RiskLevel, request.TargetPpm, request.ReceivingInspectionRequired); return (object)new { id, request.SupplierPartyId, request.RiskLevel, request.ReceivingInspectionRequired }; }, ct);
    public Task<object> CreateScarAsync(TenantId tenant, Guid actor, CreateScarRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var id = Guid.CreateVersion7(); var number = $"SCAR-{id.ToString("N")[..12].ToUpperInvariant()}"; await ExecuteAsync(context, "insert into business_quality.supplier_corrective_action_request(tenant_id,id,number,legal_entity_id,supplier_party_id,title,description,ncr_id,due_date) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, id, number, request.LegalEntityId, request.SupplierPartyId, request.Title.Trim(), request.Description.Trim(), Db(request.NcrId), Db(request.DueDate)); return (object)new { id, number, request.SupplierPartyId, state = "Open" }; }, ct);
    public Task<QualityCertificateView> IssueCertificateAsync(TenantId tenant, Guid actor, IssueCertificateRequest request, Guid legalEntityId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) => { var inspection = await ReadSingleAsync(context, "select decision_state from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant.Value, request.InspectionId) ?? throw new QualityException("quality.inspection_not_found"); if ((string)inspection[0] != "Passed") throw new QualityException("quality.certificate_requires_passed_inspection"); var id = Guid.CreateVersion7(); var number = $"{request.CertificateType}-{id.ToString("N")[..12].ToUpperInvariant()}"; await ExecuteAsync(context, "insert into business_quality.quality_certificate(tenant_id,id,legal_entity_id,number,certificate_type,inspection_id,customer_reference,state,created_by) values($1,$2,$3,$4,$5,$6,$7,'Issued',$8)", token, tenant.Value, id, legalEntityId, number, request.CertificateType, request.InspectionId, Db(request.CustomerReference), actor); await ExecuteAsync(context, "insert into business_quality.quality_certificate_version(tenant_id,id,certificate_id,version_no,snapshot,created_by) values($1,$2,$3,1,$4::jsonb,$5)", token, tenant.Value, Guid.CreateVersion7(), id, JsonSerializer.Serialize(new { request.InspectionId, request.CertificateType }), actor); context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "QualityCertificateIssuedV1", JsonSerializer.Serialize(new { certificateId = id, number, request.InspectionId }), tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), id, "quality")); return new QualityCertificateView(id, number, request.CertificateType, "Issued", request.InspectionId, request.CustomerReference, DateTimeOffset.UtcNow); }, ct);

    public async Task<QualityReceiptResolution> ResolvePurchaseReceiptAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid purchaseOrderId, Guid supplierPartyId, string idempotencyKey, IReadOnlyList<QualityReceiptLine> lines, CancellationToken cancellationToken = default)
    {
        return await coordinator.ExecuteAsync(new TenantId(tenantId), async (context, token) =>
        {
            var required = Convert.ToBoolean(await ScalarAsync(context, "select exists(select 1 from business_quality.supplier_quality_profile where tenant_id=$1 and legal_entity_id=$2 and supplier_party_id=$3 and receiving_inspection_required) or exists(select 1 from business_quality.quality_plan p join business_quality.quality_plan_applicability a on a.tenant_id=p.tenant_id and a.plan_id=p.id where p.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type='Receiving' and p.status='Active' and p.mandatory and (a.supplier_party_id=$3 or a.product_variant_id = any($4::uuid[])))", token, tenantId, legalEntityId, supplierPartyId, lines.Select(x => x.ProductVariantId).ToArray()), CultureInfo.InvariantCulture);
            var inspections = new List<Guid>();
            if (required)
                foreach (var line in lines)
                {
                    var sourceFingerprint = $"receiving:{purchaseOrderId:D}:{idempotencyKey}:{line.PurchaseOrderLineId:D}";
                    var existingInspectionId = await ScalarAsync<Guid?>(context, "select id from business_quality.inspection where tenant_id=$1 and event_type='Receiving' and source_fingerprint=$2 order by created_at desc,id desc limit 1", token, tenantId, sourceFingerprint);
                    if (existingInspectionId is { } existingId)
                        inspections.Add(existingId);
                    else
                    {
                        var inspection = await CreateInspectionInContextAsync(context, new TenantId(tenantId), actorId, new CreateInspectionRequest(legalEntityId, "Receiving", line.ProductVariantId, line.Quantity, purchaseOrderId, SupplierPartyId: supplierPartyId, LotReference: line.LotId?.ToString("D"), SourceFingerprint: sourceFingerprint), token);
                        inspections.Add(inspection.Id);
                    }
                }
            return new QualityReceiptResolution(purchaseOrderId, legalEntityId, supplierPartyId, lines, required, required ? "QualityHold" : "Available", required ? "Mandatory receiving inspection" : null, inspections);
        }, cancellationToken);
    }

    public Task<QualityDecisionSnapshot?> GetProductionDecisionAsync(TenantId tenantId, Guid legalEntityId, Guid productionOrderId, Guid? productionOperationId, CancellationToken cancellationToken = default) => coordinator.ExecuteAsync(tenantId, async (context, token) =>
    {
        var row = await ReadSingleAsync(context, "select i.id,i.decision_state from business_quality.inspection i where i.tenant_id=$1 and i.legal_entity_id=$2 and i.production_order_id=$3 and i.production_operation_id is not distinct from $4::uuid and i.state='Completed' order by i.completed_at desc,i.id desc limit 1", token, tenantId.Value, legalEntityId, productionOrderId, Db(productionOperationId));
        return row is null ? null : new QualityDecisionSnapshot(productionOrderId, productionOperationId, true, (string)row[1], $"inspection:{(Guid)row[0]:D}:evaluation");
    }, cancellationToken);

    private static async Task<InspectionView> CreateInspectionInContextAsync(ITransactionContext context, TenantId tenant, Guid actor, CreateInspectionRequest request, CancellationToken token)
    {
        var planVersionId = request.PlanVersionId ?? await ResolvePlanVersionAsync(context, tenant.Value, request, token);
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
        return await ScalarAsync<Guid?>(context, "select v.id from business_quality.quality_plan_version v join business_quality.quality_plan p on p.tenant_id=v.tenant_id and p.id=v.plan_id left join business_quality.quality_plan_applicability a on a.tenant_id=p.tenant_id and a.plan_id=p.id where v.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type=$3 and p.status='Active' and v.status='Active' and (a.product_variant_id is null or a.product_variant_id=$4) and (a.supplier_party_id is null or a.supplier_party_id=$5) order by (a.product_variant_id is not null) desc,(a.supplier_party_id is not null) desc,v.version_no desc limit 1", token, tenant, request.LegalEntityId, request.EventType, Db(request.ProductVariantId), Db(request.SupplierPartyId));
    }
    private static async Task<int> CalculateSampleSizeInContextAsync(ITransactionContext context, Guid tenant, Guid samplingId, decimal lot, CancellationToken token) { var row = await ReadSingleAsync(context, "select p.method,p.fixed_quantity,p.percentage from business_quality.sampling_plan_version v join business_quality.sampling_plan p on p.tenant_id=v.tenant_id and p.id=v.sampling_plan_id where v.tenant_id=$1 and v.id=$2", token, tenant, samplingId) ?? throw new QualityException("quality.sampling_plan_not_found"); return QualityRules.CalculateSampleSize((string)row[0] == "Aql" ? "Full" : (string)row[0], lot, row[1] is DBNull ? null : (decimal?)row[1], row[2] is DBNull ? null : (decimal?)row[2]); }
    private static async Task<string> ReadPlanSnapshotAsync(ITransactionContext context, Guid tenant, Guid planVersionId, CancellationToken token) { var rows = new List<object>(); await using var command = Command(context, "select c.code,c.name,c.data_type,c.base_uom_code,pc.minimum,pc.maximum,pc.target,pc.allowed_values,pc.required from business_quality.quality_plan_characteristic pc join business_quality.quality_characteristic c on c.tenant_id=pc.tenant_id and c.id=pc.characteristic_id where pc.tenant_id=$1 and pc.plan_version_id=$2 order by pc.sequence_no", tenant, planVersionId); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) rows.Add(new { code = reader.GetString(0), name = reader.GetString(1), dataType = reader.GetString(2), uom = reader.IsDBNull(3) ? null : reader.GetString(3), minimum = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4), maximum = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5), target = reader.IsDBNull(6) ? null : reader.GetString(6), allowedValues = reader.IsDBNull(7) ? (JsonElement?)null : reader.GetFieldValue<JsonElement>(7), required = reader.GetBoolean(8) }); return JsonSerializer.Serialize(new { planVersionId, characteristics = rows }); }
    private static async Task<InspectionView?> ReadInspectionAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { var row = await ReadSingleAsync(context, "select id,legal_entity_id,number,event_type,state,decision_state,product_variant_id,source_document_id,production_order_id,production_operation_id,supplier_party_id,lot_quantity,sample_size,lot_reference,row_version from business_quality.inspection where tenant_id=$1 and id=$2", token, tenant, id); if (row is null) return null; var results = new List<InspectionResultView>(); await using var command = Command(context, "select id,characteristic_id,numeric_value,text_value,boolean_value,passed,uom_code,evidence_reference from business_quality.inspection_result where tenant_id=$1 and inspection_id=$2 order by recorded_at,id", tenant, id); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) results.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7))); return new InspectionView((Guid)row[0], (Guid)row[1], (string)row[2], (string)row[3], (string)row[4], (string)row[5], row[6] is DBNull ? null : (Guid)row[6], row[7] is DBNull ? null : (Guid)row[7], row[8] is DBNull ? null : (Guid)row[8], row[9] is DBNull ? null : (Guid)row[9], row[10] is DBNull ? null : (Guid)row[10], (decimal)row[11], (int)row[12], row[13] is DBNull ? null : (string)row[13], results, (long)row[14]); }
    private static HashSet<string> ParseAllowed(object? json) { if (json is null or DBNull) return new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { return JsonSerializer.Deserialize<HashSet<string>>((string)json) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase); } catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); } }
    private static async Task<object[]?> ReadSingleAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray() : null; }
    private static async Task<T?> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { var value = await ScalarAsync(context, sql, token, values); return value is null or DBNull ? default : (T)value; }
    private static async Task<object?> ScalarAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteScalarAsync(token); }
    private static async Task<int> ExecuteAsync(ITransactionContext context, string sql, CancellationToken token, params object?[] values) { await using var command = Command(context, sql, values); return await command.ExecuteNonQueryAsync(token); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object?[] values) { var command = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i] ?? DBNull.Value); return command; }
    private static object Db(object? value) => value ?? DBNull.Value;
    private static void ValidateText(string value, int max) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new QualityException("quality.text_invalid"); }
    private static Task<int> AuditAsync(ITransactionContext context, Guid tenant, string type, Guid id, string action, Guid actor, object? after, CancellationToken token) => ExecuteAsync(context, "insert into business_quality.quality_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,after_json) values($1,$2,$3,$4,$5,$6,$7::jsonb)", token, tenant, Guid.CreateVersion7(), type, id, action, actor, after is null ? null : JsonSerializer.Serialize(after));
}
