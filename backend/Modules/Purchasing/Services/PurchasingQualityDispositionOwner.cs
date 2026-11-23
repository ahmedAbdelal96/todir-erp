using Modules.Purchasing.DTOs;
using Modules.Purchasing.Repositories;
using Shared.Contracts.Inventory;
using Shared.Contracts.Quality;
using Shared.Primitives.Identifiers;

namespace Modules.Purchasing.Services;

public sealed class PurchasingQualityDispositionOwner(PurchasingRepository repository, IInventoryPurchasingCapabilities inventory) : IPurchasingQualityDispositionCapabilities
{
    public async Task<QualityDispositionOwnerResult> ExecuteSupplierReturnAsync(Guid tenantId, Guid actorId,
        QualitySupplierReturnRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = new TenantId(tenantId);
        var order = await repository.GetAsync(tenant, request.LegalEntityId, request.PurchaseOrderId, false, cancellationToken)
            ?? throw new InvalidOperationException("quality.supplier_return_purchase_order_not_found");
        var line = order.Lines.FirstOrDefault(item => item.ProductVariantId == request.ProductVariantId && item.UnitOfMeasureId == request.UnitOfMeasureId)
            ?? throw new InvalidOperationException("quality.supplier_return_line_not_found");
        var idempotencyKey = $"quality-disposition:{request.DispositionId:D}";
        var returnId = await repository.CreateReturnAsync(tenant, actorId, request.LegalEntityId, request.PurchaseOrderId,
            new CreateSupplierReturnRequest(order.Version, "PhysicalReturn", $"quality-disposition:{request.DispositionId:D}",
                [new SupplierReturnLineRequest(line.Id, request.Quantity, request.ProductVariantId, request.UnitOfMeasureId,
                    request.WarehouseId, request.LocationId, request.LotId, request.SerialId)], IdempotencyKey: idempotencyKey), cancellationToken);
        var current = await repository.GetReturnAsync(tenant, request.LegalEntityId, returnId, cancellationToken)
            ?? throw new InvalidOperationException("quality.supplier_return_not_found");
        if (current.State == "Executed")
        {
            var existingOperations = await repository.GetReturnOperationsAsync(tenant, request.LegalEntityId, returnId, cancellationToken);
            if (existingOperations.Count > 0) return new(existingOperations[0], "Confirmed", true);
        }
        if (current.State == "Draft") _ = await repository.ApproveReturnAsync(tenant, actorId, request.LegalEntityId, returnId, current.Version, cancellationToken);
        var lines = await repository.GetReturnLinesAsync(tenant, request.LegalEntityId, returnId, cancellationToken);
        var operations = await inventory.ReturnPurchaseAsync(tenantId, actorId, new PurchaseReturnRequest(request.LegalEntityId, returnId,
            idempotencyKey, request.OperationalDate, request.PostingDate, lines.Select(item => new PurchaseReturnLine(item.PurchaseOrderLineId,
                item.ProductVariantId ?? request.ProductVariantId, item.WarehouseId ?? request.WarehouseId, item.LocationId ?? request.LocationId,
                item.UnitOfMeasureId ?? request.UnitOfMeasureId, item.Quantity, item.LotId, item.SerialId)).ToArray()), cancellationToken);
        await repository.CompleteReturnAsync(tenant, request.LegalEntityId, returnId, operations, null, cancellationToken);
        return new(operations[0], "Confirmed", false);
    }
}
