using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Core.Messaging;
using Core.Services;
using Core.Transactions;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Npgsql;
using Shared.Contracts.Manufacturing;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Repositories;

public sealed partial class ManufacturingRepository(ITransactionCoordinator coordinator, ITransactionalSequenceAllocator sequences) : IManufacturingCostReader
{
    public Task<PlantResponse> CreatePlantAsync(TenantId tenant, Guid actor, CreatePlantRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ManufacturingException("manufacturing.plant_identity_required");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.plant(tenant_id,id,legal_entity_id,code,name,branch_id,time_zone,created_by) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), Db(request.BranchId), request.TimeZone.Trim(), actor);
        await AuditAsync(context, tenant.Value, "Plant", id, "created", actor, null, new { request.Code, request.Name }, token);
        return new PlantResponse(id, request.LegalEntityId, request.Code.Trim(), request.Name.Trim(), request.TimeZone.Trim(), "Active");
    }, ct);

    public Task<ManufacturingProfileResponse> SaveProfileAsync(TenantId tenant, Guid actor, CreateManufacturingProfileRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.DefaultMode is not ("Discrete" or "ProcessBatch") || request.MakeBuyPolicy is not ("Make" or "Buy" or "MakeOrBuy" or "Subcontract")) throw new ManufacturingException("manufacturing.profile_policy_invalid");
        var id = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.manufacturing_product_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", token, tenant.Value, request.LegalEntityId, request.ProductVariantId) ?? Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.manufacturing_product_profile(tenant_id,id,legal_entity_id,product_variant_id,default_mode,make_buy_policy,default_plant_id,created_by) values($1,$2,$3,$4,$5,$6,$7,$8) on conflict (tenant_id,legal_entity_id,product_variant_id) do update set default_mode=excluded.default_mode,make_buy_policy=excluded.make_buy_policy,default_plant_id=excluded.default_plant_id,updated_at=now(),row_version=business_manufacturing.manufacturing_product_profile.row_version+1", token, tenant.Value, id, request.LegalEntityId, request.ProductVariantId, request.DefaultMode, request.MakeBuyPolicy, Db(request.DefaultPlantId), actor);
        return new ManufacturingProfileResponse(id, request.LegalEntityId, request.ProductVariantId, true, request.DefaultMode, request.MakeBuyPolicy, request.DefaultPlantId);
    }, ct);

    public Task<BomResponse> CreateBomAsync(TenantId tenant, Guid actor, CreateBomRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.bom(tenant_id,id,product_variant_id,code,name,created_by) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, id, request.ProductVariantId, request.Code.Trim(), request.Name.Trim(), actor);
        return new BomResponse(id, request.ProductVariantId, request.Code.Trim(), request.Name.Trim(), "Active");
    }, ct);

    public Task<BomVersionResponse> CreateBomVersionAsync(TenantId tenant, Guid actor, Guid bomId, CreateBomVersionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ManufacturingRules.ValidateBomLines(request.Lines);
        var product = await ScalarAsync<Guid?>(context, "select product_variant_id from business_manufacturing.bom where tenant_id=$1 and id=$2", token, tenant.Value, bomId) ?? throw new ManufacturingException("manufacturing.bom_not_found");
        var revision = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(revision),0)+1 from business_manufacturing.bom_version where tenant_id=$1 and bom_id=$2", token, tenant.Value, bomId), CultureInfo.InvariantCulture);
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.bom_version(tenant_id,id,bom_id,revision,valid_from,valid_to,base_output_qty,output_uom_code,expected_yield,expected_scrap,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, bomId, revision, request.ValidFrom, Db(request.ValidTo), request.BaseOutputQty, request.OutputUomCode, request.ExpectedYield, request.ExpectedScrap, actor);
        var lines = new List<BomLineResponse>(); var lineNo = 1;
        foreach (var line in request.Lines)
        {
            var lineId = Guid.CreateVersion7();
            await ExecuteAsync(context, "insert into business_manufacturing.bom_component_line(tenant_id,id,bom_version_id,line_no,component_product_variant_id,required_qty,uom_code,scrap_factor,consumption_method,operation_sequence,substitute_group_id,phantom) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, lineId, id, lineNo, line.ComponentProductVariantId, line.RequiredQty, line.UomCode, line.ScrapFactor, line.ConsumptionMethod, Db(line.OperationSequence), Db(line.SubstituteGroupId), line.Phantom);
            lines.Add(new(lineId, lineNo++, line.ComponentProductVariantId, line.RequiredQty, line.UomCode, line.ScrapFactor, line.ConsumptionMethod, line.SubstituteGroupId, line.Phantom));
        }
        await AuditAsync(context, tenant.Value, "BomVersion", id, "created", actor, null, new { bomId, revision }, token);
        return new BomVersionResponse(id, bomId, revision, "Draft", request.ValidFrom, request.ValidTo, request.BaseOutputQty, request.OutputUomCode, lines);
    }, ct);

    public Task<BomVersionResponse?> GetBomVersionAsync(TenantId tenant, Guid versionId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadBomVersionAsync(context, tenant.Value, versionId, token), ct);

    public Task<BomVersionResponse> ChangeBomVersionStateAsync(TenantId tenant, Guid actor, Guid versionId, string action, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var row = await ReadBomStateAsync(context, tenant.Value, versionId, token) ?? throw new ManufacturingException("manufacturing.bom_version_not_found");
        var next = (row.State, action) switch { ("Draft", "approve") => "Approved", ("Approved", "activate") => "Active", ("Active", "supersede") => "Superseded", _ => throw new ManufacturingException("manufacturing.invalid_bom_transition") };
        await ExecuteAsync(context, "update business_manufacturing.bom_version set state=$1,approved_by=case when $1='Approved' then $2 else approved_by end,approved_at=case when $1='Approved' then now() else approved_at end,activated_at=case when $1='Active' then now() else activated_at end,row_version=row_version+1 where tenant_id=$3 and id=$4", token, next, actor, tenant.Value, versionId);
        if (next == "Active") await ExecuteAsync(context, "update business_manufacturing.bom_version set state='Superseded',row_version=row_version+1 where tenant_id=$1 and bom_id=$2 and id<>$3 and state='Active'", token, tenant.Value, row.BomId, versionId);
        return await ReadBomVersionAsync(context, tenant.Value, versionId, token) ?? throw new ManufacturingException("manufacturing.bom_version_not_found");
    }, ct);

    public Task<FormulaResponse> CreateFormulaAsync(TenantId tenant, Guid actor, CreateFormulaRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.formula(tenant_id,id,product_variant_id,code,name,state) values($1,$2,$3,$4,$5,'Active')", token, tenant.Value, id, request.ProductVariantId, request.Code.Trim(), request.Name.Trim());
        return new FormulaResponse(id, request.ProductVariantId, request.Code.Trim(), request.Name.Trim(), "Active");
    }, ct);

    public Task<FormulaVersionResponse> CreateFormulaVersionAsync(TenantId tenant, Guid actor, Guid formulaId, CreateFormulaVersionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.BaseBatchQty <= 0 || request.ExpectedYield <= 0 || request.ExpectedYield > 1 || request.Ingredients.Count == 0 || request.Outputs.Count == 0) throw new ManufacturingException("manufacturing.formula_version_invalid");
        var exists = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.formula where tenant_id=$1 and id=$2)", token, tenant.Value, formulaId); if (!exists) throw new ManufacturingException("manufacturing.formula_not_found");
        var revision = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(revision),0)+1 from business_manufacturing.formula_version where tenant_id=$1 and formula_id=$2", token, tenant.Value, formulaId), CultureInfo.InvariantCulture);
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.formula_version(tenant_id,id,formula_id,revision,state,valid_from,valid_to,base_batch_qty,output_uom_code,expected_yield) values($1,$2,$3,$4,'Draft',$5,$6,$7,$8,$9)", token, tenant.Value, id, formulaId, revision, request.ValidFrom, Db(request.ValidTo), request.BaseBatchQty, request.OutputUomCode, request.ExpectedYield);
        var ingredients = new List<FormulaIngredientResponse>(); var lineNo = 1;
        foreach (var ingredient in request.Ingredients)
        {
            var ingredientId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.formula_ingredient_line(tenant_id,id,formula_version_id,line_no,product_variant_id,quantity,uom_code,fixed_quantity) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, ingredientId, id, lineNo, ingredient.ProductVariantId, ingredient.Quantity, ingredient.UomCode, ingredient.FixedQuantity);
            ingredients.Add(new(ingredientId, lineNo++, ingredient.ProductVariantId, ingredient.Quantity, ingredient.UomCode, ingredient.FixedQuantity));
        }
        var outputs = new List<FormulaOutputResponse>(); lineNo = 1;
        foreach (var output in request.Outputs)
        {
            if (output.OutputType is not ("Primary" or "CoProduct" or "ByProduct" or "Loss")) throw new ManufacturingException("manufacturing.formula_output_invalid");
            var outputId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.formula_output_line(tenant_id,id,formula_version_id,line_no,product_variant_id,output_type,quantity,uom_code,allocation_basis) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, outputId, id, lineNo, output.ProductVariantId, output.OutputType, output.Quantity, output.UomCode, output.AllocationBasis);
            outputs.Add(new(outputId, lineNo++, output.ProductVariantId, output.OutputType, output.Quantity, output.UomCode, output.AllocationBasis));
        }
        return new FormulaVersionResponse(id, formulaId, revision, "Draft", request.ValidFrom, request.ValidTo, request.BaseBatchQty, request.OutputUomCode, request.ExpectedYield, ingredients, outputs);
    }, ct);

    public Task<RoutingResponse> CreateRoutingAsync(TenantId tenant, Guid actor, CreateRoutingRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.routing(tenant_id,id,product_variant_id,code,name,state) values($1,$2,$3,$4,$5,'Active')", token, tenant.Value, id, Db(request.ProductVariantId), request.Code.Trim(), request.Name.Trim()); return new RoutingResponse(id, request.ProductVariantId, request.Code.Trim(), request.Name.Trim(), "Active");
    }, ct);

    public Task<RoutingVersionResponse> CreateRoutingVersionAsync(TenantId tenant, Guid actor, Guid routingId, CreateRoutingVersionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Operations.Count == 0 || request.Operations.Any(operation => operation.SequenceNo <= 0 || operation.SetupMinutes < 0 || operation.RunMinutesPerUnit < 0 || operation.QueueMinutes < 0 || operation.MoveMinutes < 0)) throw new ManufacturingException("manufacturing.routing_version_invalid");
        var exists = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.routing where tenant_id=$1 and id=$2)", token, tenant.Value, routingId); if (!exists) throw new ManufacturingException("manufacturing.routing_not_found");
        var revision = Convert.ToInt32(await ScalarAsync(context, "select coalesce(max(revision),0)+1 from business_manufacturing.routing_version where tenant_id=$1 and routing_id=$2", token, tenant.Value, routingId), CultureInfo.InvariantCulture); var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.routing_version(tenant_id,id,routing_id,revision,state,valid_from,valid_to) values($1,$2,$3,$4,'Draft',$5,$6)", token, tenant.Value, id, routingId, revision, request.ValidFrom, Db(request.ValidTo));
        var operations = new List<RoutingOperationResponse>(); foreach (var operation in request.Operations)
        {
            var operationId = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.routing_operation(tenant_id,id,routing_version_id,sequence_no,code,name,work_center_id,setup_minutes,run_minutes_per_unit,queue_minutes,move_minutes,operation_type,depends_on_sequence,quality_gate_required) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, operationId, id, operation.SequenceNo, operation.Code.Trim(), operation.Name.Trim(), Db(operation.WorkCenterId), operation.SetupMinutes, operation.RunMinutesPerUnit, operation.QueueMinutes, operation.MoveMinutes, operation.OperationType, Db(operation.DependsOnSequence), operation.QualityGateRequired);
            operations.Add(new(operationId, operation.SequenceNo, operation.Code.Trim(), operation.Name.Trim(), operation.WorkCenterId, operation.SetupMinutes, operation.RunMinutesPerUnit, operation.QueueMinutes, operation.MoveMinutes, operation.OperationType, operation.DependsOnSequence, operation.QualityGateRequired));
        }
        return new RoutingVersionResponse(id, routingId, revision, "Draft", request.ValidFrom, request.ValidTo, operations);
    }, ct);

    public Task<bool> CreateWorkCenterCalendarAsync(TenantId tenant, Guid actor, Guid workCenterId, CreateWorkCenterCalendarRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Weekday is < 0 or > 6 || request.ShiftEnd <= request.ShiftStart || request.BreakMinutes < 0) throw new ManufacturingException("manufacturing.calendar_invalid");
        await ExecuteAsync(context, "insert into business_manufacturing.work_center_calendar(tenant_id,id,work_center_id,weekday,shift_start,shift_end,effective_from,effective_to,break_minutes) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, Guid.CreateVersion7(), workCenterId, request.Weekday, request.ShiftStart, request.ShiftEnd, request.EffectiveFrom, Db(request.EffectiveTo), request.BreakMinutes); return true;
    }, ct);

    public Task<ProductionDefinitionResponse> CreateDefinitionAsync(TenantId tenant, Guid actor, CreateProductionDefinitionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        ManufacturingRules.ValidateMode(request.ManufacturingMode, request.BomVersionId, request.FormulaVersionId);
        if (request.ValidTo is not null && request.ValidTo < request.ValidFrom || request.MinQty is <= 0 || request.MaxQty is <= 0 || request.MaxQty is not null && request.MinQty is not null && request.MaxQty < request.MinQty) throw new ManufacturingException("manufacturing.definition_range_invalid");
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.production_definition(tenant_id,id,legal_entity_id,plant_id,product_variant_id,manufacturing_mode,bom_version_id,formula_version_id,routing_version_id,valid_from,valid_to,min_qty,max_qty,priority,state,expected_yield,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,'Approved',$15,$16)", token, tenant.Value, id, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.ManufacturingMode, Db(request.BomVersionId), Db(request.FormulaVersionId), Db(request.RoutingVersionId), request.ValidFrom, Db(request.ValidTo), Db(request.MinQty), Db(request.MaxQty), request.Priority, request.ExpectedYield, actor);
        return new ProductionDefinitionResponse(id, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.ManufacturingMode, request.BomVersionId, request.FormulaVersionId, request.RoutingVersionId, request.ValidFrom, request.ValidTo, request.MinQty, request.MaxQty, request.Priority, "Approved");
    }, ct);

    public Task<ProductionDefinitionResponse?> ResolveDefinitionAsync(TenantId tenant, ResolveDefinitionRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = Command(context, "select id,legal_entity_id,plant_id,product_variant_id,manufacturing_mode,bom_version_id,formula_version_id,routing_version_id,valid_from,valid_to,min_qty,max_qty,priority,state from business_manufacturing.production_definition where tenant_id=$1 and legal_entity_id=$2 and plant_id=$3 and product_variant_id=$4 and state in ('Approved','Active') and valid_from <= $5 and (valid_to is null or valid_to >= $5) and (min_qty is null or min_qty <= $6) and (max_qty is null or max_qty >= $6) order by priority asc,valid_from desc,id asc", tenant.Value, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.AsOfDate, request.Quantity);
        await using var reader = await command.ExecuteReaderAsync(token); var rows = new List<ProductionDefinitionResponse>();
        while (await reader.ReadAsync(token)) rows.Add(ReadDefinition(reader));
        if (rows.Count > 1 && rows[0].Priority == rows[1].Priority) throw new ManufacturingException("manufacturing.definition_ambiguous");
        return rows.FirstOrDefault();
    }, ct);

    public Task<SubstitutionGroupResponse> CreateSubstitutionGroupAsync(TenantId tenant, Guid actor, CreateSubstitutionGroupRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.material_substitution_group(tenant_id,id,code,name,policy) values($1,$2,$3,$4,$5)", token, tenant.Value, id, request.Code.Trim(), request.Name.Trim(), request.Policy); return new SubstitutionGroupResponse(id, request.Code.Trim(), request.Name.Trim(), request.Policy);
    }, ct);

    public Task<SubstituteResponse> AddSubstituteAsync(TenantId tenant, Guid actor, Guid groupId, AddSubstituteRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.material_substitute(tenant_id,id,group_id,product_variant_id,priority,valid_from,valid_to,replacement_ratio,uom_code,auto_apply_allowed,max_cost_increase_pct,quality_approval_required) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", token, tenant.Value, id, groupId, request.ProductVariantId, request.Priority, request.ValidFrom, Db(request.ValidTo), request.ReplacementRatio, request.UomCode, request.AutoApplyAllowed, Db(request.MaxCostIncreasePct), request.QualityApprovalRequired); return new SubstituteResponse(id, groupId, request.ProductVariantId, request.Priority, request.ReplacementRatio, request.UomCode, request.ValidFrom, request.ValidTo);
    }, ct);

    public Task<WorkCenterResponse> CreateWorkCenterAsync(TenantId tenant, Guid actor, CreateWorkCenterRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.work_center(tenant_id,id,plant_id,code,name,capacity_model,time_zone,default_efficiency) values($1,$2,$3,$4,$5,$6,$7,$8)", token, tenant.Value, id, request.PlantId, request.Code.Trim(), request.Name.Trim(), request.CapacityModel, request.TimeZone, request.DefaultEfficiency); return new WorkCenterResponse(id, request.PlantId, request.Code.Trim(), request.Name.Trim(), request.CapacityModel, request.DefaultEfficiency, "Active");
    }, ct);

    public Task<ProductionOrderResponse> CreateProductionOrderAsync(TenantId tenant, Guid actor, CreateProductionOrderRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => CreateProductionOrderInContextAsync(context, tenant, actor, request, correlationId, token), ct);

    public Task<ProductionOrderResponse?> GetProductionOrderAsync(TenantId tenant, Guid orderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadOrderAsync(context, tenant.Value, orderId, token), ct);
    public Task<IReadOnlyList<ProductionOrderResponse>> ListProductionOrdersAsync(TenantId tenant, Guid legalEntityId, string? state, Guid? productVariantId, int limit, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = Command(context, "select id,number,legal_entity_id,plant_id,product_variant_id,ordered_qty,uom_code,need_date,state,material_state,operation_state,quality_state,cost_state,completed_qty,scrap_qty,row_version from business_manufacturing.production_order where tenant_id=$1 and legal_entity_id=$2 and ($3::text is null or state=$3) and ($4::uuid is null or product_variant_id=$4) order by need_date,id limit $5", tenant.Value, legalEntityId, Db(state), Db(productVariantId), Math.Clamp(limit, 1, 200));
        await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ProductionOrderResponse>(); while (await reader.ReadAsync(token)) result.Add(ReadOrder(reader)); return (IReadOnlyList<ProductionOrderResponse>)result;
    }, ct);

    public Task<ProductionOrderResponse> TransitionProductionOrderAsync(TenantId tenant, Guid actor, Guid orderId, string action, long expectedVersion, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        if (order.RowVersion != expectedVersion) throw new ManufacturingException("manufacturing.concurrency_conflict");
        if (action == "close")
        {
            if (order.CompletedQty < order.OrderedQty || await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_material_requirement where tenant_id=$1 and production_order_id=$2 and consumed_qty+returned_qty+scrapped_qty < issued_qty)", token, tenant.Value, orderId)) throw new ManufacturingException("manufacturing.close_blocked");
            if (await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_output where tenant_id=$1 and production_order_id=$2 and inventory_receipt_status <> 'Committed')", token, tenant.Value, orderId)) throw new ManufacturingException("manufacturing.close_output_unreconciled");
            if (order.QualityState is "Blocked" or "Unknown" or "Pending") throw new ManufacturingException("manufacturing.close_quality_unreconciled");
            if (order.CostState is "Unknown" or "Pending") throw new ManufacturingException("manufacturing.close_cost_unreconciled");
            if (await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_operation where tenant_id=$1 and production_order_id=$2 and state <> 'Complete')", token, tenant.Value, orderId)) throw new ManufacturingException("manufacturing.close_operations_incomplete");
            await ExecuteAsync(context, "insert into business_manufacturing.production_close_reconciliation(tenant_id,id,production_order_id,material_issued_qty,material_accounted_qty,good_output_qty,scrap_qty,operation_completed,quality_state,cost_state,wip_amount,state,idempotency_key,closed_at) select $1,$3,$2,coalesce(sum(r.issued_qty),0),coalesce(sum(r.consumed_qty+r.returned_qty+r.scrapped_qty),0),o.completed_qty,o.scrap_qty,true,o.quality_state,o.cost_state,greatest(coalesce((select sum(f.amount) from business_manufacturing.production_actual_cost_fact f where f.tenant_id=o.tenant_id and f.production_order_id=o.id),0)-coalesce((select sum(p.final_value) from business_manufacturing.production_output p where p.tenant_id=o.tenant_id and p.production_order_id=o.id and p.inventory_receipt_status='Committed'),0),0),'Ready',$4,now() from business_manufacturing.production_order o left join business_manufacturing.production_material_requirement r on r.tenant_id=o.tenant_id and r.production_order_id=o.id where o.tenant_id=$1 and o.id=$2 group by o.id,o.completed_qty,o.scrap_qty,o.quality_state,o.cost_state on conflict (tenant_id,production_order_id) do update set state='Ready',closed_at=excluded.closed_at,idempotency_key=excluded.idempotency_key", token, tenant.Value, orderId, Guid.CreateVersion7(), $"close:{orderId:D}:{expectedVersion}");
        }
        var state = ManufacturingRules.NextOrderState(order.State, action);
        await ExecuteAsync(context, "update business_manufacturing.production_order set state=$1,released_at=case when $1='Released' then now() else released_at end,updated_at=now(),row_version=row_version+1 where tenant_id=$2 and id=$3 and row_version=$4", token, state, tenant.Value, orderId, expectedVersion);
        var eventType = state switch { "Released" => "ProductionOrderReleasedV1", "InProgress" => "ProductionOrderStartedV1", "Closed" => "ProductionOrderClosedV1", _ => null };
        if (eventType is not null) context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), eventType, JsonSerializer.Serialize(new { productionOrderId = orderId, state }), tenant, DateTimeOffset.UtcNow, correlationId, orderId, "manufacturing"));
        return await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
    }, ct);

    public Task<IReadOnlyList<ProductionMaterialResponse>> ListMaterialsAsync(TenantId tenant, Guid orderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = Command(context, "select id,product_variant_id,preferred_product_variant_id,required_qty,reserved_qty,issued_qty,consumed_qty,returned_qty,scrapped_qty,uom_code,state from business_manufacturing.production_material_requirement where tenant_id=$1 and production_order_id=$2 order by id", tenant.Value, orderId);
        await using var reader = await command.ExecuteReaderAsync(token); var result = new List<ProductionMaterialResponse>(); while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9), reader.GetString(10))); return (IReadOnlyList<ProductionMaterialResponse>)result;
    }, ct);

    public Task<ProductionMaterialResponse> RecordMaterialAsync(TenantId tenant, Guid actor, Guid orderId, MaterialActionRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.material_event_invalid");
        if (request.EventType is not ("Reserved" or "Issued" or "Consumed" or "Returned" or "Scrapped")) throw new ManufacturingException("manufacturing.material_event_invalid");
        var material = await ReadMaterialAsync(context, tenant.Value, request.RequirementId, token) ?? throw new ManufacturingException("manufacturing.material_requirement_not_found");
        var orderExists = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_order where tenant_id=$1 and id=$2 and state in ('Released','InProgress','OnHold'))", token, tenant.Value, orderId); if (!orderExists) throw new ManufacturingException("manufacturing.production_not_executable");
        var belongsToOrder = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_material_requirement where tenant_id=$1 and id=$2 and production_order_id=$3)", token, tenant.Value, request.RequirementId, orderId); if (!belongsToOrder) throw new ManufacturingException("manufacturing.material_order_mismatch");
        var replayRequirement = await ScalarAsync<Guid?>(context, "select requirement_id from business_manufacturing.production_material_event where tenant_id=$1 and source_key=$2", token, tenant.Value, request.IdempotencyKey);
        if (replayRequirement is { } replay)
        {
            if (replay != request.RequirementId) throw new ManufacturingException("manufacturing.idempotency_conflict");
            return await ReadMaterialAsync(context, tenant.Value, request.RequirementId, token) ?? throw new ManufacturingException("manufacturing.material_requirement_not_found");
        }
        await ExecuteAsync(context, "insert into business_manufacturing.production_material_event(tenant_id,id,requirement_id,event_type,quantity,uom_code,product_variant_id,inventory_operation_id,inventory_reservation_id,source_key,substitute_product_variant_id,unit_cost,currency_code,actor_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, Guid.CreateVersion7(), request.RequirementId, request.EventType, request.Quantity, request.UomCode, request.SubstituteProductVariantId ?? material.PreferredProductVariantId, Db(request.InventoryOperationId), Db(request.InventoryReservationId), request.IdempotencyKey, Db(request.SubstituteProductVariantId), Db(request.UnitCost), Db(request.CurrencyCode), actor);
        var column = request.EventType switch { "Reserved" => "reserved_qty", "Issued" => "issued_qty", "Consumed" => "consumed_qty", "Returned" => "returned_qty", "Scrapped" => "scrapped_qty", _ => throw new ManufacturingException("manufacturing.material_event_invalid") };
        var issued = request.EventType == "Issued" ? material.IssuedQty + request.Quantity : material.IssuedQty; var consumed = request.EventType == "Consumed" ? material.ConsumedQty + request.Quantity : material.ConsumedQty; var returned = request.EventType == "Returned" ? material.ReturnedQty + request.Quantity : material.ReturnedQty; var scrapped = request.EventType == "Scrapped" ? material.ScrappedQty + request.Quantity : material.ScrappedQty;
        ManufacturingRules.ValidateMaterialConservation(issued, consumed, returned, scrapped);
        await ExecuteAsync(context, $"update business_manufacturing.production_material_requirement set {column}={column}+$1,state=case when issued_qty + case when $2='Issued' then $1 else 0 end >= required_qty and consumed_qty + case when $2='Consumed' then $1 else 0 end + returned_qty + case when $2='Returned' then $1 else 0 end + scrapped_qty + case when $2='Scrapped' then $1 else 0 end >= issued_qty + case when $2='Issued' then $1 else 0 end then 'Resolved' when consumed_qty + case when $2='Consumed' then $1 else 0 end > 0 then 'PartiallyConsumed' when issued_qty + case when $2='Issued' then $1 else 0 end > 0 then 'PartiallyIssued' when reserved_qty + case when $2='Reserved' then $1 else 0 end > 0 then 'Reserved' else state end where tenant_id=$3 and id=$4", token, request.Quantity, request.EventType, tenant.Value, request.RequirementId);
        await ExecuteAsync(context, "update business_manufacturing.production_order o set material_state=case when exists(select 1 from business_manufacturing.production_material_requirement r where r.tenant_id=o.tenant_id and r.production_order_id=o.id and r.state in ('Required','Short')) then 'Short' when exists(select 1 from business_manufacturing.production_material_requirement r where r.tenant_id=o.tenant_id and r.production_order_id=o.id and r.state not in ('Consumed','Resolved')) then 'PartiallyReady' else 'Resolved' end,updated_at=now(),row_version=row_version+1 where o.tenant_id=$1 and o.id=$2", token, tenant.Value, orderId);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "MaterialConsumedForProductionV1", JsonSerializer.Serialize(new { productionOrderId = orderId, requirementId = request.RequirementId, eventType = request.EventType, quantity = request.Quantity, inventoryOperationId = request.InventoryOperationId, inventoryReservationId = request.InventoryReservationId }), tenant, DateTimeOffset.UtcNow, correlationId, orderId, "manufacturing"));
        return await ReadMaterialAsync(context, tenant.Value, request.RequirementId, token) ?? throw new ManufacturingException("manufacturing.material_requirement_not_found");
    }, ct);

    public Task<ProductionOutputResponse> ReportOutputAsync(TenantId tenant, Guid actor, Guid orderId, OutputReportRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.idempotency_required");
        var existing = await ReadOutputByKeyAsync(context, tenant.Value, request.IdempotencyKey, token); if (existing is not null) return existing;
        var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        ManufacturingRules.ValidateOutput(order.OrderedQty, order.CompletedQty, request.Quantity);
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.production_output(tenant_id,id,production_order_id,operation_id,product_variant_id,output_type,quantity,uom_code,inventory_operation_id,inventory_receipt_status,lot_id,serial_id,provisional_value,final_value,currency_code,idempotency_key,actor_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Committed',$10,$11,$12,$12,$13,$14,$15)", token, tenant.Value, id, orderId, Db(request.OperationId), order.ProductVariantId, request.OutputType, request.Quantity, Db(request.InventoryOperationId), Db(request.LotId), Db(request.SerialId), Db(request.ProvisionalValue), Db(request.CurrencyCode), request.IdempotencyKey, actor);
        if (request.OutputType == "Good") await ExecuteAsync(context, "update business_manufacturing.production_order set completed_qty=completed_qty+$1,state=case when completed_qty+$1 >= ordered_qty then 'Completed' else state end,updated_at=now(),row_version=row_version+1 where tenant_id=$2 and id=$3", token, request.Quantity, tenant.Value, orderId);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProductionOutputReportedV1", JsonSerializer.Serialize(new { productionOrderId = orderId, outputId = id, quantity = request.Quantity, outputType = request.OutputType }), tenant, DateTimeOffset.UtcNow, correlationId, orderId, "manufacturing"));
        return new ProductionOutputResponse(id, orderId, order.ProductVariantId, request.Quantity, request.OutputType, "Committed", request.IdempotencyKey);
    }, ct);

    public Task<ProductionOutputResponse?> GetOutputByKeyAsync(TenantId tenant, string key, CancellationToken ct) => coordinator.ExecuteAsync(tenant, (context, token) => ReadOutputByKeyAsync(context, tenant.Value, key, token), ct);

    public Task<PlanningRunResponse> CreatePlanningRunAsync(TenantId tenant, Guid actor, CreatePlanningRunRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.planning_run(tenant_id,id,legal_entity_id,plant_id,horizon_start,horizon_end,as_of,policy_version,source_watermark,state,created_by) values($1,$2,$3,$4,$5,$6,now(),$7,jsonb_build_object('createdAt',now()),'Draft',$8)", token, tenant.Value, id, request.LegalEntityId, Db(request.PlantId), request.HorizonStart, request.HorizonEnd, request.PolicyVersion, actor); return new PlanningRunResponse(id, request.LegalEntityId, request.PlantId, request.HorizonStart, request.HorizonEnd, "Draft", 0, 0, 0);
    }, ct);

    public async Task AddPlanningDemandAsync(TenantId tenant, Guid runId, PlanningDemandRequest request, CancellationToken ct)
    {
        await coordinator.ExecuteAsync(tenant, async (context, token) =>
        {
            var state = await ScalarAsync<string?>(context, "select state from business_manufacturing.planning_run where tenant_id=$1 and id=$2", token, tenant.Value, runId) ?? throw new ManufacturingException("manufacturing.planning_run_not_found"); if (state != "Draft") throw new ManufacturingException("manufacturing.planning_run_immutable");
            await ExecuteAsync(context, "insert into business_manufacturing.planning_demand(tenant_id,id,planning_run_id,product_variant_id,plant_id,source_type,source_ref,need_date,quantity,uom_code,consumed_forecast_qty) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, Guid.CreateVersion7(), runId, request.ProductVariantId, Db(request.PlantId), request.SourceType, request.SourceRef, request.NeedDate, request.Quantity, request.UomCode, request.ConsumedForecastQty);
            return true;
        }, ct);
    }

    public Task<PlanningRunResponse> RunPlanningAsync(TenantId tenant, Guid actor, Guid runId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var run = await ReadPlanningRunAsync(context, tenant.Value, runId, token) ?? throw new ManufacturingException("manufacturing.planning_run_not_found"); if (run.State is not ("Draft" or "Completed")) throw new ManufacturingException("manufacturing.planning_run_not_runnable");
        await ExecuteAsync(context, "update business_manufacturing.planning_run set state='Running',started_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, runId);
        await ExecuteAsync(context, "delete from business_manufacturing.planned_supply where tenant_id=$1 and planning_run_id=$2 and state='Suggested'; delete from business_manufacturing.planning_exception where tenant_id=$1 and planning_run_id=$2", token, tenant.Value, runId);
        await using var demandCommand = Command(context, "select id,product_variant_id,plant_id,need_date,quantity,uom_code,source_type,consumed_forecast_qty from business_manufacturing.planning_demand where tenant_id=$1 and planning_run_id=$2 order by need_date,id", tenant.Value, runId); await using var reader = await demandCommand.ExecuteReaderAsync(token); var demands = new List<(Guid Id, Guid ProductVariantId, Guid? PlantId, DateOnly NeedDate, decimal Quantity, string UomCode, string SourceType, decimal ConsumedForecastQty)>(); while (await reader.ReadAsync(token)) demands.Add((reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7))); await reader.DisposeAsync();
        foreach (var demandGroup in demands.GroupBy(demand => new { demand.ProductVariantId, demand.PlantId, demand.NeedDate, demand.UomCode }))
        {
            var forecast = demandGroup.Where(demand => string.Equals(demand.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)).Sum(demand => demand.Quantity);
            var acceptedDemand = demandGroup.Where(demand => !string.Equals(demand.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)).Sum(demand => demand.Quantity);
            var consumedForecast = demandGroup.Sum(demand => demand.ConsumedForecastQty);
            var grossRequirement = Math.Max(forecast, acceptedDemand);
            var committedSupply = await ScalarAsync<decimal>(context, "select coalesce(sum(quantity),0) from business_manufacturing.planned_supply where tenant_id=$1 and product_variant_id=$2 and (($3::uuid is null and plant_id is null) or plant_id=$3) and need_date=$4 and state='Firmed'", token, tenant.Value, demandGroup.Key.ProductVariantId, Db(demandGroup.Key.PlantId), demandGroup.Key.NeedDate);
            var residual = Math.Max(0m, grossRequirement - consumedForecast - committedSupply);
            if (residual <= 0) continue;
            var demand = demandGroup.FirstOrDefault(item => string.Equals(item.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)) is var forecastDemand && forecastDemand.Id != Guid.Empty ? forecastDemand : demandGroup.First();
            var supplyId = Guid.CreateVersion7(); var start = demandGroup.Key.NeedDate.AddDays(-1); await ExecuteAsync(context, "insert into business_manufacturing.planned_supply(tenant_id,id,planning_run_id,product_variant_id,plant_id,supply_type,source_ref,need_date,suggested_start,quantity,uom_code,source_watermark) values($1,$2,$3,$4,$5,'Production',$6,$7,$8,$9,$10,jsonb_build_object('demandId',$2::text,'grossRequirement',$11,'consumedForecast',$12,'firmedSupply',$13))", token, tenant.Value, supplyId, runId, demandGroup.Key.ProductVariantId, Db(demandGroup.Key.PlantId), $"Demand:{demand.Id:D}", demandGroup.Key.NeedDate, start, residual, demandGroup.Key.UomCode, grossRequirement, consumedForecast, committedSupply);
            await ExecuteAsync(context, "insert into business_manufacturing.mrp_pegging_link(tenant_id,id,planning_run_id,demand_id,planned_supply_id,allocated_qty) values($1,$2,$3,$4,$5,$6)", token, tenant.Value, Guid.CreateVersion7(), runId, demand.Id, supplyId, residual);
        }
        await ExecuteAsync(context, "update business_manufacturing.planning_run set state='Completed',completed_at=now() where tenant_id=$1 and id=$2", token, tenant.Value, runId); return await ReadPlanningRunAsync(context, tenant.Value, runId, token) ?? throw new ManufacturingException("manufacturing.planning_run_not_found");
    }, ct);

    public Task<IReadOnlyList<PlannedSupplyResponse>> ListPlannedSupplyAsync(TenantId tenant, Guid runId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = Command(context, "select id,planning_run_id,product_variant_id,supply_type,need_date,suggested_start,quantity,uom_code,state,firmed_order_id from business_manufacturing.planned_supply where tenant_id=$1 and planning_run_id=$2 order by need_date,id", tenant.Value, runId); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<PlannedSupplyResponse>(); while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetFieldValue<DateOnly>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.GetDecimal(6), reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetGuid(9))); return (IReadOnlyList<PlannedSupplyResponse>)result;
    }, ct);

    public Task<ProductionOrderResponse> FirmProductionAsync(TenantId tenant, Guid actor, Guid plannedSupplyId, Guid legalEntityId, Guid plantId, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        var supply = await ReadSupplyAsync(context, tenant.Value, plannedSupplyId, token) ?? throw new ManufacturingException("manufacturing.planned_supply_not_found"); if (supply.State != "Suggested") throw new ManufacturingException("manufacturing.planned_supply_stale");
        await ExecuteAsync(context, "update business_manufacturing.planned_supply set state='Firmed' where tenant_id=$1 and id=$2 and state='Suggested'", token, tenant.Value, plannedSupplyId);
        var order = await CreateProductionOrderInContextAsync(context, tenant, actor, new CreateProductionOrderRequest(legalEntityId, plantId, supply.ProductVariantId, supply.Quantity, supply.UomCode, supply.NeedDate, supply.SuggestedStart, supply.NeedDate, "MRP", plannedSupplyId.ToString("D")), correlationId, token);
        await ExecuteAsync(context, "update business_manufacturing.planned_supply set firmed_order_id=$1 where tenant_id=$2 and id=$3", token, order.Id, tenant.Value, plannedSupplyId); return order;
    }, ct);

    public Task<ProductionOrderResponse> ReportScrapAsync(TenantId tenant, Guid actor, Guid orderId, ScrapReportRequest request, Guid correlationId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.scrap_invalid");
        var order = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        var existing = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_scrap where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey);
        if (existing is not null) return order;
        var id = Guid.CreateVersion7(); await ExecuteAsync(context, "insert into business_manufacturing.production_scrap(tenant_id,id,production_order_id,operation_id,product_variant_id,quantity,uom_code,category,reason,idempotency_key,actor_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, id, orderId, Db(request.OperationId), order.ProductVariantId, request.Quantity, request.UomCode, request.Category, Db(request.Reason), request.IdempotencyKey, actor);
        await ExecuteAsync(context, "update business_manufacturing.production_order set scrap_qty=scrap_qty+$1,updated_at=now(),row_version=row_version+1 where tenant_id=$2 and id=$3", token, request.Quantity, tenant.Value, orderId);
        context.AddOutbox(new OutboxMessage(Guid.CreateVersion7(), "ProductionScrapReportedV1", JsonSerializer.Serialize(new { productionOrderId = orderId, scrapId = id, request.Quantity, request.Category }), tenant, DateTimeOffset.UtcNow, correlationId, orderId, "manufacturing"));
        return await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
    }, ct);

    public Task<ProductionCostResponse> RecordCostFactAsync(TenantId tenant, Guid actor, Guid orderId, RecordCostFactRequest request, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        if (request.Amount < 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.cost_fact_invalid");
        _ = await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        var existing = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and idempotency_key=$2", token, tenant.Value, request.IdempotencyKey);
        if (existing is null) await ExecuteAsync(context, "insert into business_manufacturing.production_actual_cost_fact(tenant_id,id,production_order_id,source_type,source_id,cost_category,amount,currency_code,quantity,source_date,idempotency_key) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", token, tenant.Value, Guid.CreateVersion7(), orderId, request.SourceType, request.SourceId, request.CostCategory, request.Amount, request.CurrencyCode, Db(request.Quantity), request.SourceDate, request.IdempotencyKey);
        await ExecuteAsync(context, "update business_manufacturing.production_order set cost_state='Accumulating',updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2", token, tenant.Value, orderId);
        return await GetProductionCostInContextAsync(context, tenant.Value, orderId, token);
    }, ct);

    public Task<ProductionCostResponse> GetProductionCostAsync(TenantId tenant, Guid orderId, CancellationToken ct) => coordinator.ExecuteAsync(tenant, async (context, token) =>
    {
        return await GetProductionCostInContextAsync(context, tenant.Value, orderId, token);
    }, ct);

    public Task<ManufacturingProductionFact?> GetProductionFactAsync(Guid tenantId, Guid productionOrderId, CancellationToken cancellationToken = default) => coordinator.ExecuteAsync(new TenantId(tenantId), async (context, token) =>
    {
        await using var command = Command(context, "select legal_entity_id,plant_id,number,product_variant_id,completed_qty,coalesce((select sum(amount) from business_manufacturing.production_actual_cost_fact f where f.tenant_id=o.tenant_id and f.production_order_id=o.id),0),need_date from business_manufacturing.production_order o where tenant_id=$1 and id=$2", tenantId, productionOrderId); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) return null; return new ManufacturingProductionFact(tenantId, reader.GetGuid(0), reader.GetGuid(1), productionOrderId, reader.GetString(2), reader.GetGuid(3), reader.GetDecimal(4), reader.GetDecimal(5), "EGP", reader.GetFieldValue<DateOnly>(6));
    }, cancellationToken);

    private static async Task<ProductionCostResponse> GetProductionCostInContextAsync(ITransactionContext context, Guid tenant, Guid orderId, CancellationToken token)
    {
        var order = await ReadOrderAsync(context, tenant, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        await using var command = Command(context, "select coalesce(sum(amount) filter(where cost_category='Material'),0),coalesce(sum(amount) filter(where cost_category='Labor'),0),coalesce(sum(amount) filter(where cost_category='Machine'),0),coalesce(sum(amount) filter(where cost_category='Overhead'),0),coalesce(sum(amount) filter(where cost_category='Subcontract'),0),coalesce(sum(amount),0) from business_manufacturing.production_actual_cost_fact where tenant_id=$1 and production_order_id=$2 and source_type <> 'Correction'", tenant, orderId); await using var reader = await command.ExecuteReaderAsync(token); await reader.ReadAsync(token); var material = reader.GetDecimal(0); var labor = reader.GetDecimal(1); var machine = reader.GetDecimal(2); var overhead = reader.GetDecimal(3); var subcontract = reader.GetDecimal(4); var total = reader.GetDecimal(5); return new ProductionCostResponse(orderId, "EGP", material, labor, machine, overhead, subcontract, total, order.CompletedQty == 0 ? 0 : total / order.CompletedQty);
    }

    private async Task<ProductionOrderResponse> CreateProductionOrderInContextAsync(ITransactionContext context, TenantId tenant, Guid actor, CreateProductionOrderRequest request, Guid correlationId, CancellationToken token)
    {
        var definition = request.ProductionDefinitionId is { } definitionId
            ? await ReadDefinitionByIdAsync(context, tenant.Value, definitionId, token)
            : await ResolveDefinitionInternalAsync(context, tenant.Value, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.OrderedQty, request.NeedDate, token);
        if (definition is null) throw new ManufacturingException("manufacturing.definition_missing");
        var orderId = Guid.CreateVersion7(); var number = await sequences.AllocateAsync(context, new SequenceDefinition("manufacturing.production-order", tenant, request.LegalEntityId, null, null, null, "MO-", 8, 1), token);
        await ExecuteAsync(context, "insert into business_manufacturing.production_order(tenant_id,id,number,legal_entity_id,plant_id,product_variant_id,ordered_qty,uom_code,need_date,planned_start,planned_end,source_type,source_ref,created_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)", token, tenant.Value, orderId, number, request.LegalEntityId, request.PlantId, request.ProductVariantId, request.OrderedQty, request.UomCode, request.NeedDate, Db(request.PlannedStart), Db(request.PlannedEnd), request.SourceType, Db(request.SourceRef), actor);
        var snapshot = new { definition.Id, definition.BomVersionId, definition.FormulaVersionId, definition.RoutingVersionId }; await ExecuteAsync(context, "insert into business_manufacturing.production_definition_snapshot(tenant_id,id,production_order_id,production_definition_id,bom_version_id,formula_version_id,routing_version_id,snapshot,source_hash) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9)", token, tenant.Value, Guid.CreateVersion7(), orderId, definition.Id, Db(definition.BomVersionId), Db(definition.FormulaVersionId), Db(definition.RoutingVersionId), JsonSerializer.Serialize(snapshot), Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant());
        if (definition.BomVersionId is { } bomVersionId)
        {
            await using var lines = Command(context, "select l.id,l.component_product_variant_id,l.required_qty,l.uom_code,l.substitute_group_id,v.base_output_qty from business_manufacturing.bom_component_line l join business_manufacturing.bom_version v on v.tenant_id=l.tenant_id and v.id=l.bom_version_id where l.tenant_id=$1 and l.bom_version_id=$2 order by l.line_no", tenant.Value, bomVersionId);
            await using var reader = await lines.ExecuteReaderAsync(token);
            var requirements = new List<(Guid LineId, Guid ProductVariantId, decimal RequiredQty, string UomCode, Guid? SubstituteGroupId, decimal BaseOutputQty)>();
            while (await reader.ReadAsync(token)) requirements.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetDecimal(5)));
            await reader.DisposeAsync();
            foreach (var line in requirements)
            {
                var requiredQty = line.RequiredQty * request.OrderedQty / line.BaseOutputQty;
                await ExecuteAsync(context, "insert into business_manufacturing.production_material_requirement(tenant_id,id,production_order_id,line_ref,product_variant_id,required_qty,uom_code,need_by,preferred_product_variant_id,substitute_group_id) values($1,$2,$3,$4,$5,$6,$7,$8,$5,$9)", token, tenant.Value, Guid.CreateVersion7(), orderId, line.LineId, line.ProductVariantId, requiredQty, line.UomCode, request.NeedDate, Db(line.SubstituteGroupId));
            }
        }
        if (definition.RoutingVersionId is { } routingVersionId)
        {
            await using var operations = Command(context, "select sequence_no,name,work_center_id,setup_minutes,run_minutes_per_unit,quality_gate_required from business_manufacturing.routing_operation where tenant_id=$1 and routing_version_id=$2 order by sequence_no", tenant.Value, routingVersionId);
            await using var operationReader = await operations.ExecuteReaderAsync(token);
            var routingOperations = new List<(int SequenceNo, string Name, Guid? WorkCenterId, decimal SetupMinutes, decimal RunMinutesPerUnit, bool QualityGateRequired)>();
            while (await operationReader.ReadAsync(token)) routingOperations.Add((operationReader.GetInt32(0), operationReader.GetString(1), operationReader.IsDBNull(2) ? null : operationReader.GetGuid(2), operationReader.GetDecimal(3), operationReader.GetDecimal(4), operationReader.GetBoolean(5)));
            await operationReader.DisposeAsync();
            foreach (var operation in routingOperations)
                await ExecuteAsync(context, "insert into business_manufacturing.production_operation(tenant_id,id,production_order_id,sequence_no,name,work_center_id,setup_minutes,run_minutes_per_unit,quality_gate_required) values($1,$2,$3,$4,$5,$6,$7,$8,$9)", token, tenant.Value, Guid.CreateVersion7(), orderId, operation.SequenceNo, operation.Name, Db(operation.WorkCenterId), operation.SetupMinutes, operation.RunMinutesPerUnit, operation.QualityGateRequired);
        }
        return await ReadOrderAsync(context, tenant.Value, orderId, token) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
    }

    private static async Task<ProductionDefinitionResponse?> ResolveDefinitionInternalAsync(ITransactionContext context, Guid tenant, Guid legalEntityId, Guid plantId, Guid productVariantId, decimal qty, DateOnly date, CancellationToken token)
    {
        await using var command = Command(context, "select id,legal_entity_id,plant_id,product_variant_id,manufacturing_mode,bom_version_id,formula_version_id,routing_version_id,valid_from,valid_to,min_qty,max_qty,priority,state from business_manufacturing.production_definition where tenant_id=$1 and legal_entity_id=$2 and plant_id=$3 and product_variant_id=$4 and state in ('Approved','Active') and valid_from <= $5 and (valid_to is null or valid_to >= $5) and (min_qty is null or min_qty <= $6) and (max_qty is null or max_qty >= $6) order by priority,valid_from desc,id", tenant, legalEntityId, plantId, productVariantId, date, qty); await using var reader = await command.ExecuteReaderAsync(token); var rows = new List<ProductionDefinitionResponse>(); while (await reader.ReadAsync(token)) rows.Add(ReadDefinition(reader)); if (rows.Count > 1 && rows[0].Priority == rows[1].Priority) throw new ManufacturingException("manufacturing.definition_ambiguous"); return rows.FirstOrDefault();
    }

    private static async Task<BomVersionResponse?> ReadBomVersionAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token)
    {
        await using var command = Command(context, "select bom_id,revision,state,valid_from,valid_to,base_output_qty,output_uom_code from business_manufacturing.bom_version where tenant_id=$1 and id=$2", tenant, id); await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) return null; var bomId = reader.GetGuid(0); var response = new BomVersionResponse(id, bomId, reader.GetInt32(1), reader.GetString(2), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetDecimal(5), reader.GetString(6), []); await reader.DisposeAsync(); await using var lines = Command(context, "select id,line_no,component_product_variant_id,required_qty,uom_code,scrap_factor,consumption_method,substitute_group_id,phantom from business_manufacturing.bom_component_line where tenant_id=$1 and bom_version_id=$2 order by line_no", tenant, id); await using var lineReader = await lines.ExecuteReaderAsync(token); var items = new List<BomLineResponse>(); while (await lineReader.ReadAsync(token)) items.Add(new(lineReader.GetGuid(0), lineReader.GetInt32(1), lineReader.GetGuid(2), lineReader.GetDecimal(3), lineReader.GetString(4), lineReader.GetDecimal(5), lineReader.GetString(6), lineReader.IsDBNull(7) ? null : lineReader.GetGuid(7), lineReader.GetBoolean(8))); return response with { Lines = items };
    }
    private static async Task<(Guid BomId, string State)?> ReadBomStateAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select bom_id,state from business_manufacturing.bom_version where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? (r.GetGuid(0), r.GetString(1)) : null; }
    private static async Task<ProductionDefinitionResponse?> ReadDefinitionByIdAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select id,legal_entity_id,plant_id,product_variant_id,manufacturing_mode,bom_version_id,formula_version_id,routing_version_id,valid_from,valid_to,min_qty,max_qty,priority,state from business_manufacturing.production_definition where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? ReadDefinition(r) : null; }
    private static ProductionDefinitionResponse ReadDefinition(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.GetFieldValue<DateOnly>(8), r.IsDBNull(9) ? null : r.GetFieldValue<DateOnly>(9), r.IsDBNull(10) ? null : r.GetDecimal(10), r.IsDBNull(11) ? null : r.GetDecimal(11), r.GetInt32(12), r.GetString(13));
    private static async Task<ProductionOrderResponse?> ReadOrderAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select id,number,legal_entity_id,plant_id,product_variant_id,ordered_qty,uom_code,need_date,state,material_state,operation_state,quality_state,cost_state,completed_qty,scrap_qty,row_version from business_manufacturing.production_order where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? ReadOrder(r) : null; }
    private static ProductionOrderResponse ReadOrder(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetGuid(2), r.GetGuid(3), r.GetGuid(4), r.GetDecimal(5), r.GetString(6), r.GetFieldValue<DateOnly>(7), r.GetString(8), r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetDecimal(13), r.GetDecimal(14), r.GetInt64(15));
    private static async Task<ProductionMaterialResponse?> ReadMaterialAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select id,product_variant_id,preferred_product_variant_id,required_qty,reserved_qty,issued_qty,consumed_qty,returned_qty,scrapped_qty,uom_code,state from business_manufacturing.production_material_requirement where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetString(9), r.GetString(10)) : null; }
    private static async Task<ProductionOutputResponse?> ReadOutputByKeyAsync(ITransactionContext context, Guid tenant, string key, CancellationToken token) { await using var c = Command(context, "select id,production_order_id,product_variant_id,quantity,output_type,inventory_receipt_status,idempotency_key from business_manufacturing.production_output where tenant_id=$1 and idempotency_key=$2", tenant, key); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetString(4), r.GetString(5), r.GetString(6)) : null; }
    private static async Task<PlanningRunResponse?> ReadPlanningRunAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select id,legal_entity_id,plant_id,horizon_start,horizon_end,state,(select count(*) from business_manufacturing.planning_demand where tenant_id=$1 and planning_run_id=$2),(select count(*) from business_manufacturing.planned_supply where tenant_id=$1 and planning_run_id=$2),(select count(*) from business_manufacturing.planning_exception where tenant_id=$1 and planning_run_id=$2) from business_manufacturing.planning_run where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetFieldValue<DateOnly>(4), r.GetString(5), r.GetInt32(6), r.GetInt32(7), r.GetInt32(8)) : null; }
    private static async Task<(Guid Id, Guid ProductVariantId, DateOnly NeedDate, DateOnly? SuggestedStart, decimal Quantity, string UomCode, string State)?> ReadSupplyAsync(ITransactionContext context, Guid tenant, Guid id, CancellationToken token) { await using var c = Command(context, "select id,product_variant_id,need_date,suggested_start,quantity,uom_code,state from business_manufacturing.planned_supply where tenant_id=$1 and id=$2", tenant, id); await using var r = await c.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? (r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.IsDBNull(3) ? null : r.GetFieldValue<DateOnly>(3), r.GetDecimal(4), r.GetString(5), r.GetString(6)) : null; }
    private static async Task<T?> ScalarAsync<T>(ITransactionContext context, string sql, CancellationToken token, params object[] values) { var value = await ScalarAsync(context, sql, token, values); return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture); }
    private static async Task<object?> ScalarAsync(ITransactionContext context, string sql, CancellationToken token, params object[] values) { await using var c = Command(context, sql, values); return await c.ExecuteScalarAsync(token); }
    private static async Task ExecuteAsync(ITransactionContext context, string sql, CancellationToken token, params object[] values) { await using var c = Command(context, sql, values); await c.ExecuteNonQueryAsync(token); }
    private static NpgsqlCommand Command(ITransactionContext context, string sql, params object[] values) { var c = new NpgsqlCommand(sql, context.Connection, context.Transaction); for (var i = 0; i < values.Length; i++) c.Parameters.AddWithValue("p" + i, values[i] is null ? DBNull.Value : values[i]); return c; }
    private static object Db(object? value) => value ?? DBNull.Value;
    private static async Task AuditAsync(ITransactionContext context, Guid tenant, string entityType, Guid entityId, string action, Guid actor, object? before, object? after, CancellationToken token) => await ExecuteAsync(context, "insert into business_manufacturing.manufacturing_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,before_json,after_json) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb)", token, tenant, Guid.CreateVersion7(), entityType, entityId, action, actor, Db(before is null ? null : JsonSerializer.Serialize(before)), Db(after is null ? null : JsonSerializer.Serialize(after)));
}

public sealed record SubstitutionGroupResponse(Guid Id, string Code, string Name, string Policy);
public sealed record SubstituteResponse(Guid Id, Guid GroupId, Guid ProductVariantId, int Priority, decimal ReplacementRatio, string UomCode, DateOnly ValidFrom, DateOnly? ValidTo);
public sealed record WorkCenterResponse(Guid Id, Guid PlantId, string Code, string Name, string CapacityModel, decimal DefaultEfficiency, string State);
