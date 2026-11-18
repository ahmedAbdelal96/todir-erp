using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Modules.Inventory.Repositories;
using Shared.Primitives.Identifiers;
using Todir.Inventory.Contracts;
using Shared.Contracts.Ledger;
using Shared.Contracts.Accounting;
using Shared.Contracts.Catalog;
using System.Globalization;
using System.Text;

namespace Modules.Inventory.Services;

/// <summary>Application boundary for Inventory workflows exposed to the API host.</summary>
public sealed class InventoryService(InventoryRepository repository, ILedgerPostingEngine? ledgerPosting = null,
    ILedgerControlBalanceRead? ledgerBalance = null, ICatalogCapabilities? catalog = null, TimeProvider? clock = null) : IInventoryCapabilities, Shared.Contracts.Inventory.IInventoryPurchasingCapabilities, Shared.Contracts.Inventory.IInventoryFixedAssetCapabilities, IInventoryAccountingRead
{
    public Task<IReadOnlyList<InventoryAvailabilityLine>> GetBatchAvailabilityAsync(Guid tenantId, InventoryAvailabilityRequest request, CancellationToken cancellationToken = default) => repository.GetBatchAvailabilityAsync(new TenantId(tenantId), request, cancellationToken);
    public async Task<Guid> ReserveAsync(Guid tenantId, Guid actorId, InventoryReservationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await repository.ReserveAsync(new TenantId(tenantId), actorId, request.LegalEntityId,
            new ReserveStockRequest(request.ProductVariantId, request.WarehouseId, request.LocationId, request.Quantity,
                request.SourceModule, request.SourceDocumentId, request.SourceLineId, request.IdempotencyKey, request.ExpiresAt,
                request.LotId, request.SerialId),
            Guid.CreateVersion7(), cancellationToken);
        return result.ReservationId;
    }
    public Task ReleaseReservationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId, CancellationToken cancellationToken = default) => repository.ReleaseReservationAsync(new TenantId(tenantId), actorId, legalEntityId, reservationId, Guid.CreateVersion7(), cancellationToken);
    public Task CancelReservationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid reservationId, CancellationToken cancellationToken = default) => repository.CancelReservationAsync(new TenantId(tenantId), actorId, legalEntityId, reservationId, Guid.CreateVersion7(), cancellationToken);
    public Task<int> ExpireReservationsAsync(Guid tenantId, Guid legalEntityId, int limit = 500, CancellationToken cancellationToken = default) => repository.ExpireReservationsAsync(new TenantId(tenantId), legalEntityId, limit, Guid.CreateVersion7(), cancellationToken);
    public async Task<int> ExpireDueReservationsAsync(int scopeLimit = 500, int perScopeLimit = 500,
        DateTimeOffset? asOf = null, CancellationToken cancellationToken = default)
    {
        if (perScopeLimit is < 1 or > 500) throw new InventoryException("inventory.page_size_invalid");
        var scopes = await repository.GetDueReservationScopesAsync(scopeLimit, asOf, cancellationToken);
        var expired = 0;
        foreach (var scope in scopes)
            expired += await repository.ExpireReservationsAsync(new TenantId(scope.TenantId), scope.LegalEntityId,
                perScopeLimit, Guid.CreateVersion7(), cancellationToken, asOf);
        return expired;
    }
    public async Task<Guid> ConsumeReservationAsync(Guid tenantId, Guid actorId, Guid reservationId, InventoryMovementRequest issue, CancellationToken cancellationToken = default)
    {
        var result = await repository.IssueAsync(new TenantId(tenantId), actorId, issue.LegalEntityId,
            new ConfirmStockMovementRequest(issue.ProductVariantId, issue.WarehouseId, issue.LocationId, issue.EnteredUomId,
                issue.Quantity, issue.OperationalDate, issue.PostingDate, issue.IdempotencyKey, issue.SourceModule,
                issue.SourceDocumentId, StockStatus.Available, issue.LotId, issue.SerialId), Guid.CreateVersion7(), null,
            cancellationToken, reservationId: reservationId);
        return result.OperationId;
    }
    public async Task<IReadOnlyList<InventoryMovementHistoryItem>> GetMovementHistoryAsync(Guid tenantId, InventoryMovementHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var cursor = ParseCursor(query.Cursor);
        var page = await repository.GetMovementHistoryAsync(new TenantId(tenantId), query.LegalEntityId, query.ProductVariantId,
            query.WarehouseId, query.LocationId, query.LotId, query.SerialId, null, cursor?.EffectiveAt,
            cursor?.Sequence, cursor?.Id, query.PageSize, cancellationToken, query.From, query.To);
        return page.Items.Select(item => new InventoryMovementHistoryItem(item.Id, item.OperationId, item.ProductVariantId,
            item.WarehouseId, item.LocationId, item.SignedBaseQuantity, item.EffectiveAt, item.Sequence,
            item.MovementType, item.SourceModule, item.SourceDocumentId)).ToArray();
    }
    public Task<InventoryValuationSnapshot?> GetValuationAsync(Guid tenantId, InventoryValuationQuery query, CancellationToken cancellationToken = default) => repository.GetValuationAsync(new TenantId(tenantId), query, cancellationToken);
    public async Task<Shared.Contracts.Inventory.FixedAssetInventorySourceSnapshot?> GetInternalUseCapitalizationSourceAsync(
        TenantId tenantId, Guid legalEntityId, Guid productVariantId, Guid? serialId, DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var valuation = await repository.GetValuationAsync(tenantId,
            new InventoryValuationQuery(legalEntityId, productVariantId, asOfDate), cancellationToken);
        if (valuation is null || valuation.Quantity <= 0m || valuation.CarryingValue <= 0m)
            return null;

        var history = await repository.GetMovementHistoryAsync(tenantId, legalEntityId, productVariantId,
            null, null, null, serialId, null, null, null, null, 100, cancellationToken);
        return new Shared.Contracts.Inventory.FixedAssetInventorySourceSnapshot(
            productVariantId, serialId, valuation.Quantity, valuation.CarryingValue,
            valuation.CurrencyCode, valuation.CostingMethod, asOfDate,
            history.Items.Select(item => item.Id).ToArray(),
            history.Items.Where(item => item.SourceDocumentId is not null)
                .Select(item => item.SourceDocumentId!.Value).Distinct().ToArray());
    }
    public async Task<IReadOnlyList<InventoryNegativeStockCondition>> GetNegativeStockConditionsAsync(Guid tenantId, Guid legalEntityId,
        string? status = "Active", DateTimeOffset? beforeOpenedAt = null, Guid? beforeId = null, int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        (await repository.GetNegativeStockConditionsAsync(new TenantId(tenantId), legalEntityId, status, beforeOpenedAt, beforeId, pageSize, cancellationToken))
            .Select(row => new InventoryNegativeStockCondition(row.Id, row.ProductVariantId, row.WarehouseId, row.LocationId,
                row.StockStatus, row.LotId, row.SerialId, row.Status, row.ShortfallBaseQuantity, row.OpenedByMovementId,
                row.LastSeenMovementId, row.ResolvedByMovementId, row.OpenedAt, row.ResolvedAt)).ToArray();
    public Task<InventoryLocationSuggestion?> SuggestPutawayAsync(Guid tenantId, Guid legalEntityId, Guid productVariantId, Guid? warehouseId, CancellationToken cancellationToken = default) => repository.SuggestPutawayAsync(new TenantId(tenantId), legalEntityId, productVariantId, warehouseId, cancellationToken);
    public Task<IReadOnlyList<InventoryLocationSuggestion>> SuggestPickingAsync(Guid tenantId, Guid legalEntityId, Guid productVariantId, Guid? warehouseId, int limit = 100, CancellationToken cancellationToken = default) => repository.SuggestPickingAsync(new TenantId(tenantId), legalEntityId, productVariantId, warehouseId, limit, cancellationToken);
    public async Task<InventoryReconciliationSnapshot> ReconcileAsync(Guid tenantId, Guid legalEntityId,
        DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        if (ledgerPosting is null || ledgerBalance is null) throw new InventoryException("inventory.ledger_read_capability_required");
        var tenant = new TenantId(tenantId);
        var entity = new LegalEntityId(legalEntityId);
        var configurationId = await ledgerPosting.GetActiveConfigurationVersionIdAsync(tenant, entity, cancellationToken);
        var ledger = await ledgerBalance.GetControlBalanceAsync(tenant, entity, "Inventory", configurationId, asOfDate, cancellationToken);
        var inventory = await repository.GetValuationTotalAsync(tenant, legalEntityId, asOfDate, cancellationToken);
        if (inventory is not null && !string.Equals(inventory.CurrencyCode, ledger.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InventoryException("inventory.valuation_currency_mismatch");
        var inventoryValue = inventory?.CarryingValue ?? 0m;
        var difference = inventoryValue - ledger.SignedBaseBalance;
        var diagnostics = new List<string>();
        if (inventory is null && ledger.SignedBaseBalance != 0m) diagnostics.Add("ledger_inventory_without_valuation");
        if (inventory is not null && ledger.SignedBaseBalance == 0m && inventoryValue != 0m) diagnostics.Add("valuation_without_ledger_balance");
        if (difference != 0m && diagnostics.Count == 0) diagnostics.Add("inventory_ledger_balance_difference");
        return new InventoryReconciliationSnapshot(legalEntityId, asOfDate, inventoryValue, ledger.SignedBaseBalance,
            difference, ledger.BaseCurrencyCode, difference == 0m ? "Reconciled" : "Difference", diagnostics);
    }
    public async Task<InventoryAccountingReconciliation> ReconcileInventoryAsync(TenantId tenantId, LegalEntityId legalEntityId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ReconcileAsync(tenantId.Value, legalEntityId.Value, asOfDate, cancellationToken);
            return new(result.LegalEntityId, result.AsOfDate, result.InventoryValue, result.LedgerValue, result.Difference,
                result.CurrencyCode, result.Status, result.Diagnostics, true);
        }
        catch (InventoryException exception) when (exception.Code is "inventory.ledger_read_capability_required" or "inventory.valuation_currency_mismatch")
        {
            return new(legalEntityId.Value, asOfDate, 0m, 0m, 0m, string.Empty, "NotReady", [exception.Code], false);
        }
    }
    public async Task<InventoryScanResult?> ResolveScanAsync(Guid tenantId, Guid legalEntityId, string code, CancellationToken cancellationToken = default)
    {
        if (catalog is null) throw new InventoryException("inventory.catalog_read_capability_required");
        var tenant = new TenantId(tenantId);
        var normalized = code?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 160) throw new InventoryException("inventory.scan_code_invalid");
        var product = await catalog.ResolveVariantAsync(tenant, normalized, (clock ?? TimeProvider.System).GetUtcNow(), legalEntityId, cancellationToken);
        if (product is not null)
            return new InventoryScanResult("Product", product.ProductVariantId, product.ProductVariantId, null,
                product.Sku ?? product.DisplayName, "Active");
        return await repository.ResolveOwnedScanAsync(tenant, legalEntityId, normalized, cancellationToken);
    }

    public Task<Warehouse> CreateWarehouseAsync(TenantId tenant, Guid actor, Guid entity, CreateWarehouseRequest request, Guid correlationId, CancellationToken ct) => repository.CreateWarehouseAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(TenantId tenant, Guid entity, CancellationToken ct) => repository.ListWarehousesAsync(tenant, entity, ct);
    public Task<Warehouse> ChangeWarehouseStatusAsync(TenantId tenant, Guid actor, Guid entity, Guid warehouseId, WarehouseStatus status, Guid correlationId, CancellationToken ct) => repository.ChangeWarehouseStatusAsync(tenant, actor, entity, warehouseId, status, correlationId, ct);
    public Task<InventoryLocation> CreateLocationAsync(TenantId tenant, Guid actor, Guid entity, CreateLocationRequest request, Guid correlationId, CancellationToken ct) => repository.CreateLocationAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<IReadOnlyList<InventoryLocation>> ListLocationsAsync(TenantId tenant, Guid entity, Guid warehouseId, CancellationToken ct) => repository.ListLocationsAsync(tenant, entity, warehouseId, ct);
    public Task<InventoryLocation> MoveLocationAsync(TenantId tenant, Guid actor, Guid entity, Guid locationId, MoveLocationRequest request, Guid correlationId, CancellationToken ct) => repository.MoveLocationAsync(tenant, actor, entity, locationId, request, correlationId, ct);
    public Task ArchiveLocationAsync(TenantId tenant, Guid actor, Guid entity, Guid locationId, Guid correlationId, CancellationToken ct) => repository.ArchiveLocationAsync(tenant, actor, entity, locationId, correlationId, ct);
    public Task<InventoryProfile> SaveProfileAsync(TenantId tenant, Guid actor, Guid entity, CreateInventoryProfileRequest request, Guid correlationId, CancellationToken ct) => repository.SaveProfileAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<TraceabilityRecord> CreateLotAsync(TenantId tenant, Guid actor, Guid entity, CreateLotRequest request, Guid correlationId, CancellationToken ct) => repository.CreateLotAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<TraceabilityRecord> CreateSerialAsync(TenantId tenant, Guid actor, Guid entity, CreateSerialRequest request, Guid correlationId, CancellationToken ct) => repository.CreateSerialAsync(tenant, actor, entity, request, correlationId, ct);
    public async Task<IReadOnlyList<InventoryBalanceView>> GetBalancesAsync(TenantId tenant, Guid entity, Guid? variantId, Guid? warehouseId, int limit, CancellationToken ct)
    {
        var balances = await repository.GetBalancesAsync(tenant, entity, variantId, warehouseId, limit, ct);
        if (balances.Count == 0 || catalog is null) return balances;

        var identities = await catalog.FindVariantsBatchAsync(tenant, balances.Select(balance => balance.ProductVariantId).Distinct().ToArray(), entity, ct);
        var templateIds = identities.Values.Select(identity => identity.ProductTemplateId).Distinct().ToArray();
        var templateReferences = templateIds.Length == 0
            ? new Dictionary<Guid, CatalogProductTemplateReferences>()
            : await catalog.GetProductTemplateReferencesBatchAsync(tenant, templateIds, entity, ct);
        return balances.Select(balance => identities.TryGetValue(balance.ProductVariantId, out var identity)
            ? balance with
            {
                ProductDisplayName = identity.DisplayName,
                ProductSku = identity.Sku ?? (templateReferences.TryGetValue(identity.ProductTemplateId, out var template) ? template.ProductTemplateCode : null)
            }
            : balance).ToArray();
    }
    public Task<InventoryMovementPage> GetMovementHistoryAsync(TenantId tenant, Guid entity, Guid? variantId, Guid? warehouseId, Guid? locationId, Guid? lotId, Guid? serialId, Guid? sourceDocumentId, DateTimeOffset? beforeEffectiveAt, long? beforeSequence, Guid? beforeId, int limit, CancellationToken ct) => repository.GetMovementHistoryAsync(tenant, entity, variantId, warehouseId, locationId, lotId, serialId, sourceDocumentId, beforeEffectiveAt, beforeSequence, beforeId, limit, ct);
    public Task<IReadOnlyList<NegativeStockConditionView>> GetNegativeStockConditionsAsync(TenantId tenant, Guid entity, string? status, DateTimeOffset? beforeOpenedAt, Guid? beforeId, int limit, CancellationToken ct) => repository.GetNegativeStockConditionsAsync(tenant, entity, status, beforeOpenedAt, beforeId, limit, ct);
    public Task<IReadOnlyList<InventoryAsOfView>> GetStockAsOfAsync(TenantId tenant, Guid entity, DateTimeOffset asOf, Guid? variantId, Guid? warehouseId, int limit, CancellationToken ct) => repository.GetStockAsOfAsync(tenant, entity, asOf, variantId, warehouseId, limit, ct);
    public Task<IReadOnlyList<ProductLocatorView>> LocateProductAsync(TenantId tenant, Guid entity, Guid variantId, int limit, CancellationToken ct) => repository.LocateProductAsync(tenant, entity, variantId, limit, ct);
    public Task<InventoryOperationResult> ReceiveAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ReceiveAsync(tenant, actor, entity, request, correlationId, traceParent, ct);
    public async Task<Shared.Contracts.Inventory.PurchaseReceiptResult> ReceivePurchaseAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.PurchaseReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var receipts = new List<Guid>();
        var lines = new List<Shared.Contracts.Inventory.PurchaseReceiptLineResult>();
        foreach (var line in request.Lines)
        {
            var result = await repository.ReceiveAsync(new TenantId(tenantId), actorId, request.LegalEntityId,
                new ConfirmStockMovementRequest(line.ProductVariantId, line.WarehouseId, line.LocationId, line.UnitOfMeasureId, line.Quantity,
                    request.OperationalDate, request.PostingDate, request.IdempotencyKey + ":" + line.PurchaseOrderLineId.ToString("D"), "purchasing", request.PurchaseOrderId,
                    StockStatus.Available, line.LotId, line.SerialId), Guid.CreateVersion7(), null, cancellationToken);
            receipts.Add(result.OperationId);
            lines.Add(new(line.PurchaseOrderLineId, result.OperationId, line.Quantity));
        }
        return new(receipts, lines, false);
    }

    public async Task<IReadOnlyList<Guid>> ReturnPurchaseAsync(Guid tenantId, Guid actorId, Shared.Contracts.Inventory.PurchaseReturnRequest request, CancellationToken cancellationToken = default)
    {
        var operations = new List<Guid>();
        foreach (var line in request.Lines)
        {
            var result = await repository.IssueAsync(new TenantId(tenantId), actorId, request.LegalEntityId,
                new ConfirmStockMovementRequest(line.ProductVariantId, line.WarehouseId, line.LocationId, line.UnitOfMeasureId, line.Quantity,
                    request.OperationalDate, request.PostingDate, request.IdempotencyKey + ":" + line.PurchaseOrderLineId.ToString("D"), "purchasing", request.ReturnRequestId,
                    StockStatus.Available, line.LotId, line.SerialId), Guid.CreateVersion7(), null, cancellationToken);
            operations.Add(result.OperationId);
        }
        return operations;
    }
    public Task<InventoryOperationResult> IssueAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false) => repository.IssueAsync(tenant, actor, entity, request, correlationId, traceParent, ct, canOverrideNegativeStock);
    public Task<InventoryOperationResult> ConsumeReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.IssueAsync(tenant, actor, entity, request, correlationId, traceParent, ct, reservationId: reservationId);
    public Task<InventoryOperationResult> OpenAsync(TenantId tenant, Guid actor, Guid entity, ConfirmStockMovementRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.OpenAsync(tenant, actor, entity, request, correlationId, traceParent, ct);
    public Task<InventoryOperationResult> ChangeStockStatusAsync(TenantId tenant, Guid actor, Guid entity, ChangeStockStatusRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ChangeStockStatusAsync(tenant, actor, entity, request, correlationId, traceParent, ct);
    public Task<InventoryOperationResult> AdjustAsync(TenantId tenant, Guid actor, Guid entity, AdjustStockRequest request, Guid correlationId, string? traceParent, CancellationToken ct, bool canOverrideNegativeStock = false) => repository.AdjustAsync(tenant, actor, entity, request, correlationId, traceParent, ct, canOverrideNegativeStock);
    public Task<InventoryOperationResult> ReverseOperationAsync(TenantId tenant, Guid actor, Guid entity, ReverseInventoryOperationRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ReverseOperationAsync(tenant, actor, entity, request, correlationId, traceParent, ct);
    public async Task<Guid> AdjustAsync(Guid tenantId, Guid actorId, InventoryAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AdjustmentDirection>(request.Direction, true, out var direction)
            || !Enum.TryParse<AdjustmentReason>(request.Reason, true, out var reason)
            || !Enum.TryParse<StockStatus>(request.StockStatus, true, out var status))
            throw new InventoryException("inventory.adjustment_request_invalid");
        var result = await repository.AdjustAsync(new TenantId(tenantId), actorId, request.LegalEntityId,
            new AdjustStockRequest(request.ProductVariantId, request.WarehouseId, request.LocationId, request.EnteredUomId,
                request.Quantity, direction, reason, request.OperationalDate, request.PostingDate, request.IdempotencyKey,
                status, request.LotId, UnitCost: request.UnitCost, CurrencyCode: request.CurrencyCode),
            Guid.CreateVersion7(), null, cancellationToken);
        return result.OperationId;
    }

    public async Task<Guid> ReceiveReturnAsync(Guid tenantId, Guid actorId, InventoryReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<StockStatus>(request.StockStatus, true, out var status))
            throw new InventoryException("inventory.return_stock_status_invalid");
        var result = await repository.ReceiveAsync(new TenantId(tenantId), actorId, request.LegalEntityId,
            new ConfirmStockMovementRequest(request.ProductVariantId, request.WarehouseId, request.LocationId,
                request.EnteredUomId, request.Quantity, request.OperationalDate, request.PostingDate,
                request.IdempotencyKey, request.SourceModule, request.SourceDocumentId, status,
                request.LotId, request.SerialId, request.UnitCost, request.CurrencyCode),
            Guid.CreateVersion7(), null, cancellationToken);
        return result.OperationId;
    }
    public async Task<Guid> ReverseOperationAsync(Guid tenantId, Guid actorId, Guid legalEntityId, Guid operationId,
        DateOnly operationalDate, DateOnly postingDate, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var result = await repository.ReverseOperationAsync(new TenantId(tenantId), actorId, legalEntityId,
            new ReverseInventoryOperationRequest(operationId, operationalDate, postingDate, idempotencyKey),
            Guid.CreateVersion7(), null, cancellationToken);
        return result.OperationId;
    }
    public Task<TransferResult> CreateTransferAsync(TenantId tenant, Guid actor, Guid entity, CreateTransferRequest request, Guid correlationId, CancellationToken ct) => repository.CreateTransferAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<TransferResult> ShipTransferAsync(TenantId tenant, Guid actor, Guid entity, Guid transferId, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ShipTransferAsync(tenant, actor, entity, transferId, correlationId, traceParent, ct);
    public Task<TransferResult> ReceiveTransferAsync(TenantId tenant, Guid actor, Guid entity, Guid transferId, ReceiveTransferRequest request, Guid correlationId, string? traceParent, CancellationToken ct) => repository.ReceiveTransferAsync(tenant, actor, entity, transferId, request, correlationId, traceParent, ct);
    public Task<ReservationResult> ReserveAsync(TenantId tenant, Guid actor, Guid entity, ReserveStockRequest request, Guid correlationId, CancellationToken ct) => repository.ReserveAsync(tenant, actor, entity, request, correlationId, ct);
    public Task ReleaseReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, Guid correlationId, CancellationToken ct) => repository.ReleaseReservationAsync(tenant, actor, entity, reservationId, correlationId, ct);
    public Task CancelReservationAsync(TenantId tenant, Guid actor, Guid entity, Guid reservationId, Guid correlationId, CancellationToken ct) => repository.CancelReservationAsync(tenant, actor, entity, reservationId, correlationId, ct);
    public Task<int> ExpireReservationsAsync(TenantId tenant, Guid entity, int limit, Guid correlationId, CancellationToken ct) => repository.ExpireReservationsAsync(tenant, entity, limit, correlationId, ct);
    public Task<PhysicalCountView> CreatePhysicalCountAsync(TenantId tenant, Guid actor, Guid entity, CreatePhysicalCountRequest request, Guid correlationId, CancellationToken ct) => repository.CreatePhysicalCountAsync(tenant, actor, entity, request, correlationId, ct);
    public Task<IReadOnlyList<PhysicalCountLineView>> GetPhysicalCountLinesAsync(TenantId tenant, Guid entity, Guid countId, int limit, CancellationToken ct) => repository.GetPhysicalCountLinesAsync(tenant, entity, countId, limit, ct);
    public Task<PhysicalCountLineView> AddPhysicalCountLineAsync(TenantId tenant, Guid actor, Guid entity, Guid countId, AddCountLineRequest request, Guid correlationId, CancellationToken ct) => repository.AddPhysicalCountLineAsync(tenant, actor, entity, countId, request, correlationId, ct);
    public Task<PhysicalCountView> EnterPhysicalCountLineAsync(TenantId tenant, Guid actor, Guid entity, Guid countId, Guid lineId, EnterCountLineRequest request, Guid correlationId, CancellationToken ct) => repository.EnterPhysicalCountLineAsync(tenant, actor, entity, countId, lineId, request, correlationId, ct);
    public Task<PhysicalCountView> PostPhysicalCountAsync(TenantId tenant, Guid actor, Guid entity, Guid countId, string idempotencyKey, Guid correlationId, string? traceParent, CancellationToken ct) => repository.PostPhysicalCountAsync(tenant, actor, entity, countId, idempotencyKey, correlationId, traceParent, ct);

    private static HistoryCursor? ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 3 || !DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var effectiveAt)
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                || !Guid.TryParse(parts[2], out var id)) throw new FormatException();
            return new HistoryCursor(effectiveAt, sequence, id);
        }
        catch (FormatException) { throw new InventoryException("inventory.cursor_invalid"); }
    }

    private sealed record HistoryCursor(DateTimeOffset EffectiveAt, long Sequence, Guid Id);
}
