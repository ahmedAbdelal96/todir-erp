using Core.Transactions;
using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Npgsql;

namespace Modules.Manufacturing.Repositories;

/// <summary>
/// Planning helpers kept outside the command repository so the planning algorithm
/// remains auditable. These helpers only read Manufacturing snapshots and write
/// Manufacturing planning rows; owner modules are accessed through capabilities.
/// </summary>
public sealed partial class ManufacturingRepository
{
    private sealed record PlanningDefinition(Guid ProductVariantId, string Mode, Guid? BomVersionId, Guid? FormulaVersionId, decimal ExpectedYield);
    private sealed record BomExpansionLine(Guid ComponentProductVariantId, decimal RequiredQty, string UomCode, decimal ScrapFactor, bool Phantom);
    private sealed record FormulaExpansionLine(Guid ProductVariantId, decimal Quantity, string UomCode, bool FixedQuantity);
    private sealed record ExecutionRequirement(Guid LineId, Guid ProductVariantId, decimal RequiredQty, string UomCode, Guid? SubstituteGroupId);

    private static async Task<IReadOnlyList<ExecutionRequirement>> ExpandExecutionBomAsync(ITransactionContext context, Guid tenant, Guid bomVersionId, decimal orderedQuantity, CancellationToken token)
    {
        var requirements = new List<ExecutionRequirement>();
        await ExpandExecutionBomVersionAsync(context, tenant, bomVersionId, orderedQuantity, new HashSet<Guid>(), requirements, token);
        return requirements;
    }

    private static async Task ExpandExecutionBomVersionAsync(ITransactionContext context, Guid tenant, Guid bomVersionId, decimal outputQuantity, HashSet<Guid> path, List<ExecutionRequirement> requirements, CancellationToken token)
    {
        if (!path.Add(bomVersionId)) throw new ManufacturingException("manufacturing.bom_cycle_detected");
        decimal baseOutput;
        decimal expectedYield;
        await using (var header = Command(context, "select base_output_qty,expected_yield from business_manufacturing.bom_version where tenant_id=$1 and id=$2", tenant, bomVersionId))
        await using (var reader = await header.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) throw new ManufacturingException("manufacturing.bom_version_not_found");
            baseOutput = reader.GetDecimal(0);
            expectedYield = reader.GetDecimal(1);
        }

        var lines = new List<(Guid Id, Guid ProductVariantId, decimal RequiredQty, string UomCode, decimal ScrapFactor, Guid? SubstituteGroupId, bool Phantom)>();
        await using (var command = Command(context, "select id,component_product_variant_id,required_qty,uom_code,scrap_factor,substitute_group_id,phantom from business_manufacturing.bom_component_line where tenant_id=$1 and bom_version_id=$2 order by line_no", tenant, bomVersionId))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
                lines.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetBoolean(6)));
        }

        foreach (var line in lines)
        {
            var requiredQuantity = outputQuantity * line.RequiredQty / baseOutput * (1m + line.ScrapFactor) / expectedYield;
            if (requiredQuantity <= 0) continue;
            if (!line.Phantom)
            {
                requirements.Add(new ExecutionRequirement(line.Id, line.ProductVariantId, requiredQuantity, line.UomCode, line.SubstituteGroupId));
                continue;
            }

            var childBomVersionId = await ScalarAsync<Guid?>(context, "select v.id from business_manufacturing.bom b join business_manufacturing.bom_version v on v.tenant_id=b.tenant_id and v.bom_id=b.id where b.tenant_id=$1 and b.product_variant_id=$2 and v.state in ('Active','Approved') order by v.revision desc,v.id limit 1", token, tenant, line.ProductVariantId);
            if (childBomVersionId is null) throw new ManufacturingException("manufacturing.phantom_definition_missing");
            await ExpandExecutionBomVersionAsync(context, tenant, childBomVersionId.Value, requiredQuantity, new HashSet<Guid>(path), requirements, token);
        }
    }

    private static async Task<List<(Guid Id, Guid ProductVariantId, Guid? PlantId, DateOnly NeedDate, decimal Quantity, string UomCode, string SourceType, decimal ConsumedForecastQty, Guid? ParentDemandId, decimal StockAllocatedQty, decimal UncoveredQty)>> ReadPlanningDemandsAsync(ITransactionContext context, Guid tenant, Guid runId, CancellationToken token)
    {
        var demands = new List<(Guid, Guid, Guid?, DateOnly, decimal, string, string, decimal, Guid?, decimal, decimal)>();
        await using var command = Command(context, "select id,product_variant_id,plant_id,need_date,quantity,uom_code,source_type,consumed_forecast_qty,parent_demand_id,stock_allocated_qty,uncovered_qty from business_manufacturing.planning_demand where tenant_id=$1 and planning_run_id=$2 order by need_date,demand_level,id", tenant, runId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) demands.Add((reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetDecimal(9), reader.GetDecimal(10)));
        return demands;
    }

    private async Task ExpandDependentDemandsAsync(ITransactionContext context, Guid tenant, PlanningRunResponse run, CancellationToken token)
    {
        var roots = new List<(Guid Id, Guid ProductVariantId, Guid? PlantId, DateOnly NeedDate, decimal Quantity, string UomCode, string SourceType, decimal ConsumedForecastQty)>();
        await using (var command = Command(context, "select id,product_variant_id,plant_id,need_date,quantity,uom_code,source_type,consumed_forecast_qty from business_manufacturing.planning_demand where tenant_id=$1 and planning_run_id=$2 and parent_demand_id is null order by need_date,id", tenant, run.Id))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
                roots.Add((reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetFieldValue<DateOnly>(3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7)));
        }

        var stockRemaining = new Dictionary<(Guid ProductVariantId, Guid? PlantId, string UomCode, Guid WarehouseId), decimal>();
        foreach (var group in roots.GroupBy(root => new { root.ProductVariantId, root.PlantId, root.NeedDate, root.UomCode }).OrderBy(group => group.Key.NeedDate))
        {
            var forecast = group.Where(root => string.Equals(root.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)).Sum(root => root.Quantity);
            var accepted = group.Where(root => !string.Equals(root.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)).Sum(root => root.Quantity);
            var explicitConsumed = group.Where(root => string.Equals(root.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase)).Sum(root => root.ConsumedForecastQty);
            var consumed = Math.Min(forecast, Math.Max(explicitConsumed, accepted));
            var remainingForecast = Math.Max(0m, forecast - consumed);
            var gross = accepted + remainingForecast;
            var allocatedStock = (await AllocatePlanningStockAsync(context, tenant, run.LegalEntityId, group.Key.ProductVariantId, group.Key.PlantId, group.Key.UomCode, gross, stockRemaining, token)).Stock;
            var uncovered = Math.Max(0m, gross - allocatedStock);
            var remainingGross = gross;
            var remainingStock = allocatedStock;
            var remainingConsumed = consumed;
            foreach (var root in group.OrderBy(root => string.Equals(root.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(root => root.Id))
            {
                var rowGross = Math.Min(root.Quantity, remainingGross);
                var rowStock = Math.Min(rowGross, remainingStock);
                var rowUncovered = Math.Max(0m, rowGross - rowStock);
                var rowConsumed = string.Equals(root.SourceType, "Forecast", StringComparison.OrdinalIgnoreCase) ? Math.Min(root.Quantity, remainingConsumed) : root.ConsumedForecastQty;
                await ExecuteAsync(context, "update business_manufacturing.planning_demand set consumed_forecast_qty=$1,stock_allocated_qty=$2,uncovered_qty=$3 where tenant_id=$4 and id=$5", token, rowConsumed, rowStock, rowUncovered, tenant, root.Id);
                remainingGross -= rowGross;
                remainingStock -= rowStock;
                remainingConsumed -= rowConsumed;
                if (rowUncovered > 0m)
                    await ExpandNodeAsync(context, tenant, run, root.Id, root.ProductVariantId, root.PlantId, root.NeedDate, rowUncovered, root.UomCode, 0, new HashSet<Guid>(), stockRemaining, token);
            }
        }
    }

    private async Task ExpandNodeAsync(ITransactionContext context, Guid tenant, PlanningRunResponse run, Guid parentDemandId,
        Guid productVariantId, Guid? plantId, DateOnly needDate, decimal quantity, string parentUom, int level,
        HashSet<Guid> path, Dictionary<(Guid ProductVariantId, Guid? PlantId, string UomCode, Guid WarehouseId), decimal> stockRemaining, CancellationToken token)
    {
        if (level >= 20) throw new ManufacturingException("manufacturing.bom_depth_exceeded");
        if (path.Contains(productVariantId))
        {
            await ExecuteAsync(context, "insert into business_manufacturing.planning_exception(tenant_id,id,planning_run_id,product_variant_id,exception_type,severity,need_date,quantity,message) values($1,$2,$3,$4,'BomCycle','Error',$5,$6,$7)", token, tenant, Guid.CreateVersion7(), run.Id, productVariantId, needDate, quantity, "The active BOM/formula graph contains a cycle.");
            throw new ManufacturingException("manufacturing.bom_cycle_detected");
        }

        var definition = await ReadPlanningDefinitionAsync(context, tenant, run, productVariantId, plantId, needDate, quantity, token);
        if (definition is null) return;
        var nextPath = new HashSet<Guid>(path) { productVariantId };
        if (definition.BomVersionId is { } bomVersionId)
        {
            var bom = await ReadBomExpansionAsync(context, tenant, bomVersionId, token);
            foreach (var line in bom.Lines)
            {
                var childQuantity = quantity * line.RequiredQty / bom.BaseOutputQty * (1m + line.ScrapFactor) / bom.ExpectedYield / definition.ExpectedYield;
                if (childQuantity <= 0) continue;
                var childAllocation = await AllocatePlanningStockAsync(context, tenant, run.LegalEntityId, line.ComponentProductVariantId, plantId, line.UomCode, childQuantity, stockRemaining, token);
                if (childAllocation.Uncovered <= 0m) continue;
                if (line.Phantom)
                {
                    await ExpandNodeAsync(context, tenant, run, parentDemandId, line.ComponentProductVariantId, plantId, needDate, childAllocation.Uncovered, line.UomCode, level + 1, nextPath, stockRemaining, token);
                    continue;
                }

                var childId = await EnsureDependentDemandAsync(context, tenant, run.Id, parentDemandId, line.ComponentProductVariantId, plantId, needDate, childQuantity, line.UomCode, level + 1, $"Dependent:{parentDemandId:D}:BOM:{line.ComponentProductVariantId:D}", token);
                await ExecuteAsync(context, "update business_manufacturing.planning_demand set stock_allocated_qty=$1,uncovered_qty=$2 where tenant_id=$3 and id=$4", token, childAllocation.Stock, childAllocation.Uncovered, tenant, childId);
                await ExpandNodeAsync(context, tenant, run, childId, line.ComponentProductVariantId, plantId, needDate, childAllocation.Uncovered, line.UomCode, level + 1, nextPath, stockRemaining, token);
            }
        }
        else if (definition.FormulaVersionId is { } formulaVersionId)
        {
            var formula = await ReadFormulaExpansionAsync(context, tenant, formulaVersionId, token);
            foreach (var line in formula.Lines)
            {
                var childQuantity = line.FixedQuantity ? line.Quantity : line.Quantity * quantity / formula.BaseBatchQty / formula.ExpectedYield;
                if (childQuantity <= 0) continue;
                var childAllocation = await AllocatePlanningStockAsync(context, tenant, run.LegalEntityId, line.ProductVariantId, plantId, line.UomCode, childQuantity, stockRemaining, token);
                if (childAllocation.Uncovered <= 0m) continue;
                var childId = await EnsureDependentDemandAsync(context, tenant, run.Id, parentDemandId, line.ProductVariantId, plantId, needDate, childQuantity, line.UomCode, level + 1, $"Dependent:{parentDemandId:D}:FORMULA:{line.ProductVariantId:D}", token);
                await ExecuteAsync(context, "update business_manufacturing.planning_demand set stock_allocated_qty=$1,uncovered_qty=$2 where tenant_id=$3 and id=$4", token, childAllocation.Stock, childAllocation.Uncovered, tenant, childId);
                await ExpandNodeAsync(context, tenant, run, childId, line.ProductVariantId, plantId, needDate, childAllocation.Uncovered, line.UomCode, level + 1, nextPath, stockRemaining, token);
            }
        }
    }

    private async Task<(decimal Stock, decimal Uncovered)> AllocatePlanningStockAsync(ITransactionContext context, Guid tenant, Guid legalEntityId,
        Guid productVariantId, Guid? plantId, string uomCode, decimal quantity, Dictionary<(Guid ProductVariantId, Guid? PlantId, string UomCode, Guid WarehouseId), decimal> stockRemaining, CancellationToken token)
    {
        if (quantity <= 0m || inventoryCapabilities is null) return (0m, Math.Max(0m, quantity));
        var scope = await ReadPlantWarehouseScopeAsync(context, tenant, plantId, token);
        var availability = await inventoryCapabilities.GetAvailabilityAsync(tenant, legalEntityId, productVariantId, true, token);
        var allowedUoms = catalogCapabilities is null ? [] : await catalogCapabilities.GetAllowedUomsAsync(new Shared.Primitives.Identifiers.TenantId(tenant), productVariantId, DateTimeOffset.UtcNow, legalEntityId, token);
        var allocated = 0m;
        foreach (var row in availability.Where(row => !scope.HasScope || scope.WarehouseIds.Contains(row.WarehouseId)))
        {
            var factorToBase = string.Equals(row.BaseUnitCode, uomCode, StringComparison.OrdinalIgnoreCase)
                ? 1m
                : allowedUoms.FirstOrDefault(item => string.Equals(item.UnitCode, uomCode, StringComparison.OrdinalIgnoreCase))?.FactorToBase ?? 0m;
            if (factorToBase <= 0m) continue;
            var key = (productVariantId, plantId, uomCode, row.WarehouseId);
            if (!stockRemaining.TryGetValue(key, out var remaining)) remaining = Math.Max(0m, row.Available / factorToBase);
            var take = Math.Min(remaining, Math.Max(0m, quantity - allocated));
            stockRemaining[key] = remaining - take;
            allocated += take;
            if (allocated >= quantity) break;
        }
        return (allocated, Math.Max(0m, quantity - allocated));
    }

    private static async Task<Guid> EnsureDependentDemandAsync(ITransactionContext context, Guid tenant, Guid runId, Guid parentDemandId,
        Guid productVariantId, Guid? plantId, DateOnly needDate, decimal quantity, string uomCode, int level, string sourceRef, CancellationToken token)
    {
        var existing = await ScalarAsync<Guid?>(context, "select id from business_manufacturing.planning_demand where tenant_id=$1 and planning_run_id=$2 and source_type='Dependent' and source_ref=$3", token, tenant, runId, sourceRef);
        if (existing is { } existingId)
        {
            await ExecuteAsync(context, "update business_manufacturing.planning_demand set parent_demand_id=$1,product_variant_id=$2,plant_id=$3,need_date=$4,quantity=$5,uom_code=$6,demand_level=$7 where tenant_id=$8 and id=$9", token, parentDemandId, productVariantId, Db(plantId), needDate, quantity, uomCode, level, tenant, existingId);
            return existingId;
        }
        var id = Guid.CreateVersion7();
        await ExecuteAsync(context, "insert into business_manufacturing.planning_demand(tenant_id,id,planning_run_id,product_variant_id,plant_id,source_type,source_ref,need_date,quantity,uom_code,consumed_forecast_qty,parent_demand_id,demand_level,source_version) values($1,$2,$3,$4,$5,'Dependent',$6,$7,$8,$9,0,$10,$11,'manufacturing-bom-snapshot')", token, tenant, id, runId, productVariantId, Db(plantId), sourceRef, needDate, quantity, uomCode, parentDemandId, level);
        return id;
    }

    private static async Task<PlanningDefinition?> ReadPlanningDefinitionAsync(ITransactionContext context, Guid tenant, PlanningRunResponse run,
        Guid productVariantId, Guid? plantId, DateOnly needDate, decimal quantity, CancellationToken token)
    {
        await using var command = Command(context, "select product_variant_id,manufacturing_mode,bom_version_id,formula_version_id,expected_yield from business_manufacturing.production_definition where tenant_id=$1 and legal_entity_id=$2 and plant_id=coalesce($3,plant_id) and product_variant_id=$4 and state in ('Approved','Active') and valid_from <= $5 and (valid_to is null or valid_to >= $5) and (min_qty is null or min_qty <= $6) and (max_qty is null or max_qty >= $6) order by priority,valid_from desc,id limit 1", tenant, run.LegalEntityId, Db(plantId), productVariantId, needDate, quantity);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new PlanningDefinition(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetDecimal(4))
            : null;
    }

    private static async Task<(decimal BaseOutputQty, decimal ExpectedYield, IReadOnlyList<BomExpansionLine> Lines)> ReadBomExpansionAsync(ITransactionContext context, Guid tenant, Guid bomVersionId, CancellationToken token)
    {
        decimal baseOutput;
        decimal expectedYield;
        await using (var command = Command(context, "select base_output_qty,expected_yield from business_manufacturing.bom_version where tenant_id=$1 and id=$2", tenant, bomVersionId))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) throw new ManufacturingException("manufacturing.bom_version_not_found");
            baseOutput = reader.GetDecimal(0); expectedYield = reader.GetDecimal(1);
        }
        var lines = new List<BomExpansionLine>();
        await using var lineCommand = Command(context, "select component_product_variant_id,required_qty,uom_code,scrap_factor,phantom from business_manufacturing.bom_component_line where tenant_id=$1 and bom_version_id=$2 order by line_no", tenant, bomVersionId);
        await using var lineReader = await lineCommand.ExecuteReaderAsync(token);
        while (await lineReader.ReadAsync(token)) lines.Add(new(lineReader.GetGuid(0), lineReader.GetDecimal(1), lineReader.GetString(2), lineReader.GetDecimal(3), lineReader.GetBoolean(4)));
        return (baseOutput, expectedYield, lines);
    }

    private static async Task<(decimal BaseBatchQty, decimal ExpectedYield, IReadOnlyList<FormulaExpansionLine> Lines)> ReadFormulaExpansionAsync(ITransactionContext context, Guid tenant, Guid formulaVersionId, CancellationToken token)
    {
        decimal baseBatch;
        decimal expectedYield;
        await using (var command = Command(context, "select base_batch_qty,expected_yield from business_manufacturing.formula_version where tenant_id=$1 and id=$2", tenant, formulaVersionId))
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) throw new ManufacturingException("manufacturing.formula_version_not_found");
            baseBatch = reader.GetDecimal(0); expectedYield = reader.GetDecimal(1);
        }
        var lines = new List<FormulaExpansionLine>();
        await using var lineCommand = Command(context, "select product_variant_id,quantity,uom_code,fixed_quantity from business_manufacturing.formula_ingredient_line where tenant_id=$1 and formula_version_id=$2 order by line_no", tenant, formulaVersionId);
        await using var lineReader = await lineCommand.ExecuteReaderAsync(token);
        while (await lineReader.ReadAsync(token)) lines.Add(new(lineReader.GetGuid(0), lineReader.GetDecimal(1), lineReader.GetString(2), lineReader.GetBoolean(3)));
        return (baseBatch, expectedYield, lines);
    }

    private static async Task<bool> HasBomCycleAsync(ITransactionContext context, Guid tenant, Guid rootProductVariantId, IReadOnlyList<BomLineRequest> proposedLines, CancellationToken token)
    {
        var visited = new HashSet<Guid>();
        async Task<bool> Visit(Guid product, HashSet<Guid> path, IReadOnlyList<BomLineRequest>? proposed = null)
        {
            if (!path.Add(product)) return true;
            var lines = proposed ?? await ReadActiveBomLinesForProductAsync(context, tenant, product, token);
            foreach (var line in lines)
            {
                if (line.ComponentProductVariantId == rootProductVariantId || await Visit(line.ComponentProductVariantId, new HashSet<Guid>(path), null)) return true;
            }
            visited.Add(product);
            return false;
        }
        return await Visit(rootProductVariantId, new HashSet<Guid>(), proposedLines);
    }

    private static async Task<IReadOnlyList<BomLineRequest>> ReadActiveBomLinesForProductAsync(ITransactionContext context, Guid tenant, Guid productVariantId, CancellationToken token)
    {
        var result = new List<BomLineRequest>();
        await using var command = Command(context, "select l.component_product_variant_id,l.required_qty,l.uom_code,l.scrap_factor,l.consumption_method,l.operation_sequence,l.substitute_group_id,l.phantom from business_manufacturing.bom b join business_manufacturing.bom_version v on v.tenant_id=b.tenant_id and v.bom_id=b.id and v.state='Active' join business_manufacturing.bom_component_line l on l.tenant_id=v.tenant_id and l.bom_version_id=v.id where b.tenant_id=$1 and b.product_variant_id=$2 order by b.id,v.revision,l.line_no", tenant, productVariantId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetBoolean(7)));
        return result;
    }

    private static async Task<(string Policy, int PurchaseLead, int ManufacturingLead, decimal SafetyStock, decimal MinimumOrder, decimal Multiple, decimal? MaximumOrder, string LotPolicy)> ReadPlanningPolicyAsync(ITransactionContext context, Guid tenant, Guid legalEntityId, Guid productVariantId, CancellationToken token)
    {
        await using var command = Command(context, "select coalesce(make_buy_policy,'Buy'),purchase_lead_time_days,manufacturing_lead_time_days,safety_stock_qty,minimum_order_qty,order_multiple,maximum_order_qty,lot_sizing_policy from business_manufacturing.manufacturing_product_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", tenant, legalEntityId, productVariantId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6), reader.GetString(7))
            : ("Buy", 0, 0, 0m, 0m, 1m, null, "ExactRequirement");
    }

    private static decimal ApplyLotSizing(decimal quantity, decimal safetyStock, decimal minimumOrder, decimal multiple, decimal? maximumOrder, string lotPolicy)
    {
        var target = quantity + safetyStock;
        if (target <= 0) return 0;
        if (lotPolicy == "FixedLot" && minimumOrder > 0) target = Math.Max(target, minimumOrder);
        else if (lotPolicy == "MinMax") target = Math.Max(target, minimumOrder);
        if (multiple > 1) target = Math.Ceiling(target / multiple) * multiple;
        if (maximumOrder is > 0) target = Math.Min(target, maximumOrder.Value);
        return target;
    }

    private static async Task<string> ResolvePlanningSupplyTypeAsync(ITransactionContext context, Guid tenant, Guid legalEntityId, Guid productVariantId, DateOnly needDate, decimal quantity, CancellationToken token)
    {
        var policy = await ReadPlanningPolicyAsync(context, tenant, legalEntityId, productVariantId, token);
        if (policy.Policy == "Subcontract" || policy.Policy == "Buy") return "Purchase";
        if (policy.Policy == "MakeOrBuy")
        {
            var hasDefinition = await ScalarAsync<bool>(context, "select exists(select 1 from business_manufacturing.production_definition where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and state in ('Approved','Active') and valid_from <= $4 and (valid_to is null or valid_to >= $4) and (min_qty is null or min_qty <= $5) and (max_qty is null or max_qty >= $5))", token, tenant, legalEntityId, productVariantId, needDate, quantity);
            return hasDefinition ? "Production" : "Purchase";
        }
        return "Production";
    }

    private async Task<decimal> GetPlanningAvailableAsync(ITransactionContext context, Guid tenant, Guid legalEntityId, Guid productVariantId, Guid? plantId, string uomCode, CancellationToken token)
    {
        if (inventoryCapabilities is null) return 0m;
        var scope = await ReadPlantWarehouseScopeAsync(context, tenant, plantId, token);
        var availability = await inventoryCapabilities.GetAvailabilityAsync(tenant, legalEntityId, productVariantId, true, token);
        return availability.Where(row => (!scope.HasScope || scope.WarehouseIds.Contains(row.WarehouseId)) && string.Equals(row.BaseUnitCode, uomCode, StringComparison.OrdinalIgnoreCase)).Sum(row => Math.Max(0m, row.Available));
    }

    private static async Task<(bool HasScope, HashSet<Guid> WarehouseIds)> ReadPlantWarehouseScopeAsync(ITransactionContext context, Guid tenant, Guid? plantId, CancellationToken token)
    {
        if (plantId is null) return (false, []);
        var ids = new HashSet<Guid>();
        await using var command = Command(context, "select warehouse_id from business_manufacturing.plant_warehouse_scope where tenant_id=$1 and plant_id=$2 and active", tenant, plantId.Value);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) ids.Add(reader.GetGuid(0));
        return (true, ids);
    }

    private static async Task<HashSet<Guid>> ReadOtherPlantWarehousesAsync(ITransactionContext context, Guid tenant, Guid? plantId, CancellationToken token)
    {
        var ids = new HashSet<Guid>();
        await using var command = Command(context, "select warehouse_id from business_manufacturing.plant_warehouse_scope where tenant_id=$1 and active and ($2::uuid is null or plant_id<>$2)", tenant, Db(plantId));
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) ids.Add(reader.GetGuid(0));
        return ids;
    }
}
