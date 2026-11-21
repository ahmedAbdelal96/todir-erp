using Modules.Manufacturing.DTOs;
using Modules.Manufacturing.Entities;
using Modules.Manufacturing.Repositories;
using Shared.Contracts.Inventory;
using Shared.Contracts.Projects;
using Shared.Primitives.Identifiers;

namespace Modules.Manufacturing.Services;

public sealed class ManufacturingExecutionService(ManufacturingRepository repository, IInventoryCapabilities inventory, IProjectSourceIntegrationCapabilities? projects = null)
{
    public async Task<ProductionCostResponse> RecordLaborAsync(TenantId tenant, Guid actor, Guid orderId, RecordLaborFactRequest request, Guid correlationId, CancellationToken ct)
    {
        var result = await repository.RecordLaborFactAsync(tenant, actor, orderId, request, ct);
        if (projects is not null)
        {
            var project = await repository.GetProjectContextAsync(tenant, orderId, ct);
            if (project?.ProjectId is { } projectId)
            {
                var amount = request.Minutes * request.RateAmount / 60m;
                await projects.RecordCostAsync(new ProjectCostFactIngress(tenant, actor, projectId, project.ProjectTaskId,
                    "manufacturing", "LaborFact", request.SourceRef, null, "Labor", request.CurrencyCode.ToUpperInvariant(), amount,
                    request.CurrencyCode.ToUpperInvariant(), amount, 1m, request.WorkDate,
                    $"manufacturing-labor:{request.IdempotencyKey}", LegalEntityId: project.LegalEntityId), correlationId, ct);
            }
        }
        return result;
    }

    public async Task<SubcontractCommitmentResponse> CreateSubcontractCommitmentAsync(TenantId tenant, Guid actor, Guid orderId, CreateSubcontractCommitmentRequest request, Guid correlationId, CancellationToken ct)
    {
        var result = await repository.CreateSubcontractCommitmentAsync(tenant, actor, orderId, request, ct);
        if (projects is not null && request.CommittedAmount > 0m && request.CurrencyCode is not null)
        {
            var project = await repository.GetProjectContextAsync(tenant, orderId, ct);
            if (project?.ProjectId is { } projectId)
                await projects.RecordCostAsync(new ProjectCostFactIngress(tenant, actor, projectId, project.ProjectTaskId,
                    "manufacturing", "SubcontractCommitment", request.SourceDocumentId, null, "Subcontract", request.CurrencyCode.ToUpperInvariant(), request.CommittedAmount,
                    request.CurrencyCode.ToUpperInvariant(), request.CommittedAmount, 1m, request.ExpectedDate,
                    $"manufacturing-subcontract:{request.IdempotencyKey}", LegalEntityId: project.LegalEntityId), correlationId, ct);
        }
        return result;
    }

    public async Task<ProductionMaterialResponse> RecordMaterialAsync(TenantId tenant, Guid actor, Guid orderId, MaterialActionRequest request, Guid correlationId, CancellationToken ct)
    {
        var order = await repository.GetProductionOrderAsync(tenant, orderId, ct) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        if (order.State is not ("Released" or "InProgress" or "OnHold")) throw new ManufacturingException("manufacturing.production_not_executable");
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.material_event_invalid");
        var material = (await repository.ListMaterialsAsync(tenant, orderId, ct)).SingleOrDefault(item => item.Id == request.RequirementId) ?? throw new ManufacturingException("manufacturing.material_requirement_not_found");
        var inventoryProductVariantId = request.SubstituteProductVariantId ?? material.PreferredProductVariantId;
        var operationId = request.InventoryOperationId;
        var reservationId = request.InventoryReservationId;

        InventoryOperationSnapshot? inventoryEvidence = null;
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
                operationId = await ResolveInventoryOperationAsync(tenant.Value, order.LegalEntityId, orderId, request.IdempotencyKey,
                    () => inventory.ConsumeReservationAsync(tenant.Value, actor, reservationId.Value, new InventoryMovementRequest(inventoryProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.Quantity, request.EnteredUomId.Value, request.ConversionFactorSnapshot, "manufacturing", orderId, request.IdempotencyKey, order.NeedDate, order.NeedDate, "ProductionIssue"), ct), ct);
                inventoryEvidence = await inventory.GetOperationSnapshotAsync(tenant.Value, order.LegalEntityId, operationId!.Value, ct)
                    ?? throw new ManufacturingException("manufacturing.inventory_operation_unknown");
                if (!string.Equals(inventoryEvidence.Status, "Confirmed", StringComparison.Ordinal)) throw new ManufacturingException("manufacturing.inventory_operation_not_confirmed");
            }
            else
            {
                operationId = await ResolveInventoryOperationAsync(tenant.Value, order.LegalEntityId, orderId, request.IdempotencyKey,
                    () => inventory.ReceiveReturnAsync(tenant.Value, actor, new InventoryReturnRequest(inventoryProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.EnteredUomId.Value, request.Quantity, "manufacturing", orderId, request.RequirementId, request.IdempotencyKey, order.NeedDate, order.NeedDate), ct), ct);
                inventoryEvidence = await inventory.GetOperationSnapshotAsync(tenant.Value, order.LegalEntityId, operationId!.Value, ct)
                    ?? throw new ManufacturingException("manufacturing.inventory_operation_unknown");
                if (!string.Equals(inventoryEvidence.Status, "Confirmed", StringComparison.Ordinal)) throw new ManufacturingException("manufacturing.inventory_operation_not_confirmed");
            }
        }

        var response = await repository.RecordMaterialAsync(tenant, actor, orderId, request with { InventoryOperationId = operationId, InventoryReservationId = reservationId }, correlationId, ct, inventoryEvidence);
        if (request.EventType is "Issued" or "Returned" && inventoryEvidence is { ValuationAmount: not 0, CurrencyCode: not null } && projects is not null)
        {
            var project = await repository.GetProjectContextAsync(tenant, orderId, ct);
            if (project?.ProjectId is { } projectId)
            {
                var amount = request.EventType == "Returned" ? -Math.Abs(inventoryEvidence.ValuationAmount) : Math.Abs(inventoryEvidence.ValuationAmount);
                await projects.RecordCostAsync(new ProjectCostFactIngress(tenant, actor, projectId, project.ProjectTaskId,
                    "manufacturing", request.EventType == "Returned" ? "MaterialReturn" : "MaterialIssue",
                    inventoryEvidence.OperationId.ToString("D"), request.RequirementId.ToString("D"), "Material", inventoryEvidence.CurrencyCode,
                    amount, inventoryEvidence.CurrencyCode, amount, 1m, project.FactDate,
                    $"manufacturing-material:{inventoryEvidence.OperationId:D}", LegalEntityId: project.LegalEntityId), correlationId, ct);
            }
        }
        return response;
    }

    public async Task<ProductionOutputResponse> ReportOutputAsync(TenantId tenant, Guid actor, Guid orderId, OutputReportRequest request, Guid correlationId, CancellationToken ct)
    {
        var order = await repository.GetProductionOrderAsync(tenant, orderId, ct) ?? throw new ManufacturingException("manufacturing.production_order_not_found");
        if (request.WarehouseId is null || request.EnteredUomId is null) throw new ManufacturingException("manufacturing.inventory_context_required");
        if (order.State is not ("Released" or "InProgress" or "OnHold")) throw new ManufacturingException("manufacturing.production_not_executable");
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ManufacturingException("manufacturing.output_invalid");
        if (request.OutputType is not ("Good" or "CoProduct" or "ByProduct")) throw new ManufacturingException("manufacturing.output_type_invalid");
        var outputProductVariantId = request.ProductVariantId ?? order.ProductVariantId;
        await repository.ValidateOutputRequestAsync(tenant, orderId, outputProductVariantId, request.Quantity, request.OutputType, ct);
        var operationId = await ResolveInventoryOperationAsync(tenant.Value, order.LegalEntityId, orderId, request.IdempotencyKey,
            () => inventory.ReceiveAsync(tenant.Value, actor, new InventoryMovementRequest(outputProductVariantId, order.LegalEntityId, request.WarehouseId.Value, request.LocationId, request.Quantity, request.EnteredUomId.Value, request.ConversionFactorSnapshot, "manufacturing", orderId, request.IdempotencyKey, order.NeedDate, order.NeedDate, "ProductionOutput", request.LotId, request.SerialId), ct), ct);
        var evidence = await inventory.GetOperationSnapshotAsync(tenant.Value, order.LegalEntityId, operationId, ct)
            ?? throw new ManufacturingException("manufacturing.inventory_operation_unknown");
        if (!string.Equals(evidence.Status, "Confirmed", StringComparison.Ordinal)) throw new ManufacturingException("manufacturing.inventory_operation_not_confirmed");
        return await repository.ReportOutputAsync(tenant, actor, orderId, request with { InventoryOperationId = operationId, ProductVariantId = outputProductVariantId, ProvisionalValue = evidence.ValuationAmount, CurrencyCode = evidence.CurrencyCode }, correlationId, ct, evidence);
    }

    private async Task<Guid> ResolveInventoryOperationAsync(Guid tenantId, Guid legalEntityId, Guid sourceDocumentId, string idempotencyKey,
        Func<Task<Guid>> execute, CancellationToken ct)
    {
        try
        {
            return await execute();
        }
        catch when (!ct.IsCancellationRequested)
        {
            var recovered = await inventory.FindOperationBySourceAsync(tenantId, legalEntityId, "manufacturing", sourceDocumentId, idempotencyKey, ct);
            if (recovered is not null) return recovered.OperationId;
            throw;
        }
    }
}
