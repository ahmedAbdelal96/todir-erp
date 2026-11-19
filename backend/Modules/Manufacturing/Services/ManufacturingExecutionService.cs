using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Modules.Manufacturing.Repositories;
using Shared.Contracts.Inventory;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Services;

public sealed class ManufacturingExecutionService(ManufacturingRepository repository, IInventoryCapabilities inventory)
{
    public async Task<ProductionMaterialResponse> RecordMaterialAsync(TenantId tenant, Guid actor, Guid orderId, MaterialActionRequest request, Guid correlationId, CancellationToken ct)
    {
        var order = await repository.GetProductionOrderAsync(tenant, orderId, ct) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        var material = (await repository.ListMaterialsAsync(tenant, orderId, ct)).SingleOrDefault(item => item.Id == request.RequirementId) ?? throw new ManufacturingException("manufacturing.material_requirement_not_found");
        var inventoryProductVariantId = request.SubstituteProductVariantId ?? material.PreferredProductVariantId;
        var operationId = request.InventoryOperationId;
        var reservationId = request.InventoryReservationId;

        if (request.EventType is "Reserved" or "Issued" or "Returned")
        {
            if (request.WarehouseId is null || request.EnteredUomId is null) throw new ManufacturingException("manufacturing.inventory_context_required");
            if (request.EventType == "Reserved")
            {
                reservationId = await inventory.ReserveAsync(tenant.Value, actor, new InventoryReservationRequest(inventoryProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.Quantity, "manufacturing", orderId, request.RequirementId, request.IdempotencyKey), ct);
            }
            else if (request.EventType == "Issued")
            {
                if (reservationId is null) throw new ManufacturingException("manufacturing.inventory_reservation_required");
                operationId = await inventory.ConsumeReservationAsync(tenant.Value, actor, reservationId.Value, new InventoryMovementRequest(inventoryProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.Quantity, request.EnteredUomId.Value, request.ConversionFactorSnapshot, "manufacturing", orderId, request.IdempotencyKey, order.NeedDate, order.NeedDate, "ProductionIssue"), ct);
            }
            else
            {
                operationId = await inventory.ReceiveReturnAsync(tenant.Value, actor, new InventoryReturnRequest(inventoryProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.EnteredUomId.Value, request.Quantity, "manufacturing", orderId, request.RequirementId, request.IdempotencyKey, order.NeedDate, order.NeedDate), ct);
            }
        }

        return await repository.RecordMaterialAsync(tenant, actor, orderId, request with { InventoryOperationId = operationId, InventoryReservationId = reservationId }, correlationId, ct);
    }

    public async Task<ProductionOutputResponse> ReportOutputAsync(TenantId tenant, Guid actor, Guid orderId, OutputReportRequest request, Guid correlationId, CancellationToken ct)
    {
        var order = await repository.GetProductionOrderAsync(tenant, orderId, ct) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        if (request.WarehouseId is null || request.EnteredUomId is null) throw new ManufacturingException("manufacturing.inventory_context_required");
        var operationId = await inventory.ReceiveAsync(tenant.Value, actor, new InventoryMovementRequest(order.ProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.Quantity, request.EnteredUomId.Value, request.ConversionFactorSnapshot, "manufacturing", orderId, request.IdempotencyKey, order.NeedDate, order.NeedDate, "ProductionOutput", request.LotId, request.SerialId), ct);
        return await repository.ReportOutputAsync(tenant, actor, orderId, request with { InventoryOperationId = operationId }, correlationId, ct);
    }
}
