using Modules.Purchasing.DTOs;
using Modules.Purchasing.Entities;
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
        var idempotencyKey = $"quality-disposition:{request.DispositionId:D}";
        Guid returnId;
        try
        {
            var order = await repository.GetAsync(tenant, request.LegalEntityId, request.PurchaseOrderId, false, cancellationToken)
                ?? throw new QualityDispositionOwnerException("quality.supplier_return_purchase_order_not_found", false);
            var line = order.Lines.FirstOrDefault(item => item.ProductVariantId == request.ProductVariantId && item.UnitOfMeasureId == request.UnitOfMeasureId)
                ?? throw new QualityDispositionOwnerException("quality.supplier_return_line_not_found", false);
            returnId = await repository.CreateReturnAsync(tenant, actorId, request.LegalEntityId, request.PurchaseOrderId,
                new CreateSupplierReturnRequest(order.Version, "PhysicalReturn", idempotencyKey,
                    [new SupplierReturnLineRequest(line.Id, request.Quantity, request.ProductVariantId, request.UnitOfMeasureId,
                        request.WarehouseId, request.LocationId, request.LotId, request.SerialId)], IdempotencyKey: idempotencyKey), cancellationToken);
            var current = await repository.GetReturnAsync(tenant, request.LegalEntityId, returnId, cancellationToken)
                ?? throw new QualityDispositionOwnerException("quality.supplier_return_not_found", false);
            if (current.State == "Executed")
            {
                var existingOperations = await repository.GetReturnOperationsAsync(tenant, request.LegalEntityId, returnId, cancellationToken);
                if (existingOperations.Count > 0) return new(existingOperations[0], "Confirmed", true);
                throw new QualityDispositionOwnerException("quality.supplier_return_operation_missing", true);
            }
            if (current.State == "Draft") _ = await repository.ApproveReturnAsync(tenant, actorId, request.LegalEntityId, returnId, current.Version, cancellationToken);
        }
        catch (QualityDispositionOwnerException)
        {
            throw;
        }
        catch (PurchasingException exception)
        {
            throw new QualityDispositionOwnerException(exception.Code, false, exception);
        }

        var lines = await repository.GetReturnLinesAsync(tenant, request.LegalEntityId, returnId, cancellationToken);
        IReadOnlyList<Guid> operations;
        try
        {
            operations = await inventory.ReturnPurchaseAsync(tenantId, actorId, new PurchaseReturnRequest(request.LegalEntityId, returnId,
                idempotencyKey, request.OperationalDate, request.PostingDate, lines.Select(item => new PurchaseReturnLine(item.PurchaseOrderLineId,
                    item.ProductVariantId ?? request.ProductVariantId, item.WarehouseId ?? request.WarehouseId, item.LocationId ?? request.LocationId,
                    item.UnitOfMeasureId ?? request.UnitOfMeasureId, item.Quantity, item.LotId, item.SerialId)).ToArray(), "QualityHold"), cancellationToken);
        }
        catch (Exception exception)
        {
            throw new QualityDispositionOwnerException("quality.supplier_return_inventory_unknown", true, exception);
        }

        try
        {
            await repository.CompleteReturnAsync(tenant, request.LegalEntityId, returnId, operations, null, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new QualityDispositionOwnerException("quality.supplier_return_completion_unknown", true, exception);
        }
        return new(operations[0], "Confirmed", false);
    }
}
