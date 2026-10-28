using System.Security.Cryptography;
using System.Text;
using Modules.Catalog.Repositories;
using Modules.Catalog.Services;
using Modules.Ledger.Repositories;
using Modules.Ledger.Services;
using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Modules.Inventory.Repositories;
using Modules.Inventory.Services;
using Core.Services;
using Todir.Inventory.Contracts;
using Core.Transactions;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Inventory.Tests;

public sealed class InventoryPostgresIntegrationTests
{
    [Fact]
    public async Task ReceiptReplayIssueAndBalanceUseRealPostgresAndCatalogConversions()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_INVENTORY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_INVENTORY_TEST_CONNECTION to a migrated and seeded disposable PostgreSQL database.");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var tenantId = Stable("tenant:atlas");
        var tenant = new TenantId(tenantId);
        var entityId = Stable($"{tenantId:D}:legal-entity");
        var productVariantId = Stable($"{tenantId:D}:variant:0002:default");
        var warehouseId = Stable($"{tenantId:D}:inventory:warehouse-structured");
        var binId = Stable($"{tenantId:D}:inventory:location-bin-a-01");
        var actorId = Stable($"{tenantId:D}:identity:admin");
        var catalog = new CatalogCapabilities(new CatalogCapabilityRepository(dataSource));
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var ledgerRepository = new LedgerRepository(dataSource, coordinator,
            new NpgsqlLegalEntityFinancialReadStore(dataSource), TimeProvider.System);
        var ledger = new LedgerTransactionCapability(ledgerRepository);
        var repository = new InventoryRepository(dataSource, coordinator, TimeProvider.System, catalog, catalog, ledger);
        var capabilities = new InventoryService(repository, ledgerRepository, ledgerRepository, catalog, TimeProvider.System);
        var suffix = Guid.NewGuid().ToString("N");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await AssertTenantRlsAsync(dataSource, tenantId, productVariantId);

        var lot = await repository.CreateLotAsync(tenant, actorId, entityId,
            new CreateLotRequest(productVariantId, $"PG-{suffix}", today.AddDays(-10), today.AddDays(365), "integration"),
            Guid.CreateVersion7(), default);
        Assert.Equal("Active", lot.Status);

        var unitId = await FindBaseUomAsync(dataSource, tenantId, productVariantId);
        var receiptRequest = new ConfirmStockMovementRequest(productVariantId, warehouseId, binId, unitId, 5m,
            today, today, $"inventory-pg-receipt-{suffix}", "inventory", null,
            LotId: lot.Id);
        var concurrentReceipts = await Task.WhenAll(
            repository.ReceiveAsync(tenant, actorId, entityId, receiptRequest, Guid.CreateVersion7(), null, default),
            repository.ReceiveAsync(tenant, actorId, entityId, receiptRequest, Guid.CreateVersion7(), null, default));
        Assert.Single(concurrentReceipts, result => !result.IdempotentReplay);
        Assert.Single(concurrentReceipts, result => result.IdempotentReplay);
        var receipt = concurrentReceipts.Single(result => !result.IdempotentReplay);
        Assert.Equal("Confirmed", receipt.Status);
        Assert.Single(receipt.MovementIds);
        Assert.Equal(receipt.OperationId, concurrentReceipts.Single(result => result.IdempotentReplay).OperationId);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, receipt.OperationId, "StockReceivedV1"));

        var issue = await repository.IssueAsync(tenant, actorId, entityId,
            receiptRequest with { EnteredQuantity = 2m, IdempotencyKey = $"inventory-pg-issue-{suffix}" },
            Guid.CreateVersion7(), null, default);
        Assert.Single(issue.MovementIds);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, issue.OperationId, "StockIssuedV1"));

        var balances = await repository.GetBalancesAsync(tenant, entityId, productVariantId, warehouseId, 100, default);
        var balance = Assert.Single(balances, item => item.LotId == lot.Id);
        Assert.Equal(3m, balance.OnHandBaseQuantity);
        Assert.Equal(0m, balance.ReservedBaseQuantity);
        Assert.Equal(3m, balance.AvailableBaseQuantity);

        var history = await repository.GetMovementHistoryAsync(tenant, entityId, productVariantId, warehouseId,
            binId, lot.Id, null, null, null, null, null, 100, default);
        Assert.Equal(2, history.Items.Count);
        Assert.Contains(history.Items, item => item.SignedBaseQuantity == 5m);
        Assert.Contains(history.Items, item => item.SignedBaseQuantity == -2m);

        var simpleWarehouseId = Stable($"{tenantId:D}:inventory:warehouse-main");
        var transfer = await repository.CreateTransferAsync(tenant, actorId, entityId,
            new CreateTransferRequest(productVariantId, warehouseId, binId, simpleWarehouseId, null, unitId, 1m,
                "InTransit", $"inventory-pg-transfer-{suffix}", today, today, LotId: lot.Id), Guid.CreateVersion7(), default);
        Assert.Equal("Draft", transfer.Status);
        var shipped = await repository.ShipTransferAsync(tenant, actorId, entityId, transfer.TransferId, Guid.CreateVersion7(), null, default);
        Assert.Equal("Shipped", shipped.Status);
        var received = await repository.ReceiveTransferAsync(tenant, actorId, entityId, transfer.TransferId,
            new ReceiveTransferRequest(0.5m, $"inventory-pg-transfer-receipt-{suffix}", today, today), Guid.CreateVersion7(), null, default);
        Assert.Equal(0.5m, received.ReceivedQuantity);
        var transferredBalances = await repository.GetBalancesAsync(tenant, entityId, productVariantId, null, 500, default);
        Assert.Equal(2m, Assert.Single(transferredBalances, row => row.WarehouseId == warehouseId && row.StockStatus == "Available" && row.LotId == lot.Id).OnHandBaseQuantity);
        Assert.Equal(0.5m, Assert.Single(transferredBalances, row => row.StockStatus == "InTransit" && row.LotId == lot.Id).OnHandBaseQuantity);
        Assert.Equal(0.5m, Assert.Single(transferredBalances, row => row.WarehouseId == simpleWarehouseId && row.StockStatus == "Available" && row.LotId == lot.Id).OnHandBaseQuantity);
        var availability = await capabilities.GetBatchAvailabilityAsync(tenantId,
            new InventoryAvailabilityRequest(productVariantId, entityId, WarehouseIds: [warehouseId, simpleWarehouseId], IncludeReservations: true), default);
        Assert.Equal(2, availability.Count);
        Assert.All(availability, line => Assert.Equal("EA", line.BaseUnitCode));
        Assert.Null(await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, productVariantId, today), default));
        var capabilityHistory = await capabilities.GetMovementHistoryAsync(tenantId,
            new InventoryMovementHistoryQuery(productVariantId, entityId, null, null, lot.Id, null,
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), null), default);
        Assert.True(capabilityHistory.Count >= 6);
        var firstHistoryPage = await capabilities.GetMovementHistoryAsync(tenantId,
            new InventoryMovementHistoryQuery(productVariantId, entityId, null, null, lot.Id, null,
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), null, 2), default);
        var cursorValue = $"{firstHistoryPage[^1].EffectiveAt:O}|{firstHistoryPage[^1].Sequence}|{firstHistoryPage[^1].Id:D}";
        var cursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(cursorValue));
        var secondHistoryPage = await capabilities.GetMovementHistoryAsync(tenantId,
            new InventoryMovementHistoryQuery(productVariantId, entityId, null, null, lot.Id, null,
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), cursor, 2), default);
        Assert.Equal(2, secondHistoryPage.Count);
        Assert.DoesNotContain(secondHistoryPage, row => firstHistoryPage.Any(first => first.Id == row.Id));

        var statusRequest = new ChangeStockStatusRequest(productVariantId, warehouseId, binId, unitId, 1m,
            StockStatus.Available, StockStatus.QualityHold, today, today, $"inventory-pg-status-{suffix}", LotId: lot.Id);
        var statusChange = await repository.ChangeStockStatusAsync(tenant, actorId, entityId, statusRequest, Guid.CreateVersion7(), null, default);
        Assert.Equal(2, statusChange.MovementIds.Count);
        var statusReplay = await repository.ChangeStockStatusAsync(tenant, actorId, entityId, statusRequest, Guid.CreateVersion7(), null, default);
        Assert.True(statusReplay.IdempotentReplay);
        var statusBalances = await repository.GetBalancesAsync(tenant, entityId, productVariantId, warehouseId, 500, default);
        Assert.Equal(1m, Assert.Single(statusBalances, row => row.StockStatus == "QualityHold" && row.LotId == lot.Id).OnHandBaseQuantity);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, statusChange.OperationId, "StockStatusChangedV1"));

        var reservedDocumentId = Guid.CreateVersion7();
        var reservedLineId = Guid.CreateVersion7();
        var reserveRequest = new ReserveStockRequest(productVariantId, warehouseId, binId, 1m, "inventory", reservedDocumentId,
            reservedLineId, $"inventory-pg-reserve-consume-{suffix}", LotId: lot.Id);
        var duplicateReservations = await Task.WhenAll(
            repository.ReserveAsync(tenant, actorId, entityId, reserveRequest, Guid.CreateVersion7(), default),
            repository.ReserveAsync(tenant, actorId, entityId, reserveRequest, Guid.CreateVersion7(), default));
        Assert.Single(duplicateReservations, item => !item.IdempotentReplay);
        Assert.Single(duplicateReservations, item => item.IdempotentReplay);
        var reservation = duplicateReservations[0];
        Assert.Equal("Active", reservation.Status);
        var consumedIssue = await capabilities.ConsumeReservationAsync(tenant, actorId, entityId, reservation.ReservationId,
            new ConfirmStockMovementRequest(productVariantId, warehouseId, binId, unitId, 1m, today, today,
                $"inventory-pg-consume-issue-{suffix}", "inventory", reservedDocumentId, LotId: lot.Id),
            Guid.CreateVersion7(), null, default);
        Assert.False(consumedIssue.IdempotentReplay);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, reservation.ReservationId, "ReservationConsumedV1"));
        Assert.Equal("Consumed", await GetReservationStatusAsync(dataSource, tenantId, reservation.ReservationId));

        var valuedVariantId = Stable($"{tenantId:D}:variant:0004:default");
        var valuedWarehouseId = Stable($"{tenantId:D}:inventory:warehouse-main");
        var valuedUnitId = await FindBaseUomAsync(dataSource, tenantId, valuedVariantId);
        var valuationBefore = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, valuedVariantId, today), default);
        var reconciliationBefore = await repository.GetValuationTotalAsync(tenant, entityId, today, default);
        var valuedReceipt = new ConfirmStockMovementRequest(valuedVariantId, valuedWarehouseId, null, valuedUnitId, 2m,
            today, today, $"inventory-pg-valued-receipt-{suffix}", "inventory", null,
            UnitCost: 100m, CurrencyCode: "EGP");
        var failingRepository = new InventoryRepository(dataSource, coordinator, TimeProvider.System, catalog, catalog,
            new FailingInventoryLedgerPostingCapability(new LedgerTransactionCapability(ledgerRepository)));
        var beforeFailedReceipt = await ReadInventoryEffectCountsAsync(dataSource, tenantId);
        await Assert.ThrowsAsync<InjectedLedgerFailureException>(() => failingRepository.ReceiveAsync(tenant, actorId, entityId,
            valuedReceipt, Guid.CreateVersion7(), null, default));
        Assert.Equal(beforeFailedReceipt, await ReadInventoryEffectCountsAsync(dataSource, tenantId));
        var valuedReceiptResult = await repository.ReceiveAsync(tenant, actorId, entityId, valuedReceipt, Guid.CreateVersion7(), null, default);
        Assert.True((await repository.ReceiveAsync(tenant, actorId, entityId, valuedReceipt, Guid.CreateVersion7(), null, default)).IdempotentReplay);
        var backdatedValuedMovement = valuedReceipt with
        {
            OperationalDate = today.AddDays(-1),
            IdempotencyKey = $"inventory-pg-valued-backdate-{suffix}"
        };
        var backdateError = await Assert.ThrowsAsync<InventoryException>(() => repository.ReceiveAsync(tenant, actorId,
            entityId, backdatedValuedMovement, Guid.CreateVersion7(), null, default));
        Assert.Equal("inventory.valued_backdating_requires_revaluation", backdateError.Code);
        var issueRequest = valuedReceipt with { EnteredQuantity = 1m, UnitCost = null, IdempotencyKey = $"inventory-pg-valued-issue-{suffix}" };
        var beforeFailedIssue = await ReadInventoryEffectCountsAsync(dataSource, tenantId);
        await Assert.ThrowsAsync<InjectedLedgerFailureException>(() => failingRepository.IssueAsync(tenant, actorId, entityId,
            issueRequest, Guid.CreateVersion7(), null, default));
        Assert.Equal(beforeFailedIssue, await ReadInventoryEffectCountsAsync(dataSource, tenantId));
        var valuedIssue = await repository.IssueAsync(tenant, actorId, entityId,
            issueRequest,
            Guid.CreateVersion7(), null, default);
        Assert.Single(valuedIssue.MovementIds);
        var valuation = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, valuedVariantId, today), default);
        Assert.NotNull(valuation);
        Assert.Equal((valuationBefore?.Quantity ?? 0m) + 1m, valuation.Quantity);
        Assert.Equal((valuationBefore?.CarryingValue ?? 0m) + 100m, valuation.CarryingValue);
        Assert.Equal("EGP", valuation.CurrencyCode);
        var receiptLedgerEvidence = await GetInventoryLedgerRolesAsync(dataSource, tenantId, entityId, valuedReceiptResult.MovementIds[0]);
        Assert.Equal("InventoryReceiptValued", receiptLedgerEvidence.FactType);
        Assert.Equal(2, receiptLedgerEvidence.Roles.Count);
        Assert.All(receiptLedgerEvidence.Roles, role => Assert.Equal(receiptLedgerEvidence.FactType, role));
        var issueLedgerEvidence = await GetInventoryLedgerRolesAsync(dataSource, tenantId, entityId, valuedIssue.MovementIds[0]);
        Assert.Equal("InventoryIssueValued", issueLedgerEvidence.FactType);
        Assert.Equal(2, issueLedgerEvidence.Roles.Count);
        Assert.All(issueLedgerEvidence.Roles, role => Assert.Equal(issueLedgerEvidence.FactType, role));
        var reconciliation = await capabilities.ReconcileAsync(tenantId, entityId, today, default);
        Assert.Equal("Reconciled", reconciliation.Status);
        Assert.Equal((reconciliationBefore?.CarryingValue ?? 0m) + 100m, reconciliation.InventoryValue);
        Assert.Equal(reconciliation.InventoryValue, reconciliation.LedgerValue);
        Assert.Equal(0m, reconciliation.Difference);
        var valuedAdjustmentRequest = new AdjustStockRequest(valuedVariantId, valuedWarehouseId, null, valuedUnitId, 1m,
            AdjustmentDirection.Increase, AdjustmentReason.FoundStock, today, today, $"inventory-pg-valued-adjustment-{suffix}");
        var beforeFailedAdjustment = await ReadInventoryEffectCountsAsync(dataSource, tenantId);
        await Assert.ThrowsAsync<InjectedLedgerFailureException>(() => failingRepository.AdjustAsync(tenant, actorId, entityId,
            valuedAdjustmentRequest, Guid.CreateVersion7(), null, default));
        Assert.Equal(beforeFailedAdjustment, await ReadInventoryEffectCountsAsync(dataSource, tenantId));
        var valuedAdjustment = await repository.AdjustAsync(tenant, actorId, entityId, valuedAdjustmentRequest,
            Guid.CreateVersion7(), null, default);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, valuedAdjustment.OperationId, "StockAdjustedV1"));
        var valuedAdjustmentLedgerEvidence = await GetInventoryLedgerRolesAsync(dataSource, tenantId, entityId, valuedAdjustment.MovementIds[0]);
        Assert.Equal("InventoryCostAdjustment", valuedAdjustmentLedgerEvidence.FactType);
        Assert.Equal(2, valuedAdjustmentLedgerEvidence.Roles.Count);
        Assert.All(valuedAdjustmentLedgerEvidence.Roles, role => Assert.Equal(valuedAdjustmentLedgerEvidence.FactType, role));

        var receiptReversalRequest = new ReverseInventoryOperationRequest(valuedReceiptResult.OperationId, today, today, $"inventory-pg-valued-receipt-reversal-{suffix}");
        var beforeFailedReversal = await ReadInventoryEffectCountsAsync(dataSource, tenantId);
        await Assert.ThrowsAsync<InjectedLedgerFailureException>(() => failingRepository.ReverseOperationAsync(tenant, actorId, entityId,
            receiptReversalRequest, Guid.CreateVersion7(), null, default));
        Assert.Equal(beforeFailedReversal, await ReadInventoryEffectCountsAsync(dataSource, tenantId));
        var valuedReceiptReversal = await repository.ReverseOperationAsync(tenant, actorId, entityId,
            receiptReversalRequest,
            Guid.CreateVersion7(), null, default);
        Assert.Single(valuedReceiptReversal.MovementIds);
        var afterReceiptReversal = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, valuedVariantId, today), default);
        Assert.NotNull(afterReceiptReversal);
        Assert.Equal(valuationBefore?.Quantity ?? 0m, afterReceiptReversal.Quantity);
        Assert.Equal(valuationBefore?.CarryingValue ?? 0m, afterReceiptReversal.CarryingValue);
        Assert.Equal("InventoryValuationReversal", (await GetInventoryLedgerRolesAsync(dataSource, tenantId, entityId, valuedReceiptReversal.MovementIds[0])).FactType);
        await Assert.ThrowsAsync<InventoryException>(() => repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(valuedReceiptResult.OperationId, today, today, $"inventory-pg-valued-receipt-double-reversal-{suffix}"),
            Guid.CreateVersion7(), null, default));
        var valuedIssueReversal = await repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(valuedIssue.OperationId, today, today, $"inventory-pg-valued-issue-reversal-{suffix}"),
            Guid.CreateVersion7(), null, default);
        var afterIssueReversal = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, valuedVariantId, today), default);
        Assert.NotNull(afterIssueReversal);
        Assert.Equal((valuationBefore?.Quantity ?? 0m) + 1m, afterIssueReversal.Quantity);
        Assert.Equal((valuationBefore?.CarryingValue ?? 0m) + 100m, afterIssueReversal.CarryingValue);
        var valuedAdjustmentReversal = await repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(valuedAdjustment.OperationId, today, today, $"inventory-pg-valued-adjustment-reversal-{suffix}"),
            Guid.CreateVersion7(), null, default);
        var afterAdjustmentReversal = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, valuedVariantId, today), default);
        Assert.NotNull(afterAdjustmentReversal);
        Assert.Equal(valuationBefore?.Quantity ?? 0m, afterAdjustmentReversal.Quantity);
        Assert.Equal(valuationBefore?.CarryingValue ?? 0m, afterAdjustmentReversal.CarryingValue);
        Assert.Equal(3, valuedReceiptReversal.MovementIds.Count + valuedIssueReversal.MovementIds.Count + valuedAdjustmentReversal.MovementIds.Count);

        var fifoVariantId = Stable($"{tenantId:D}:variant:0005:default");
        var fifoWarehouseId = Stable($"{tenantId:D}:inventory:warehouse-structured");
        var fifoLocationId = Stable($"{tenantId:D}:inventory:location-bin-a-01");
        var fifoUnitId = await FindBaseUomAsync(dataSource, tenantId, fifoVariantId);
        var fifoBefore = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        var fifoReceiptOne = new ConfirmStockMovementRequest(fifoVariantId, fifoWarehouseId, fifoLocationId, fifoUnitId, 10m,
            today, today, $"inventory-pg-fifo-r1-{suffix}", "inventory", null, UnitCost: 100m, CurrencyCode: "EGP");
        await repository.ReceiveAsync(tenant, actorId, entityId, fifoReceiptOne, Guid.CreateVersion7(), null, default);
        await repository.ReceiveAsync(tenant, actorId, entityId,
            fifoReceiptOne with { EnteredQuantity = 10m, UnitCost = 120m, IdempotencyKey = $"inventory-pg-fifo-r2-{suffix}" },
            Guid.CreateVersion7(), null, default);
        var fifoAfterReceipts = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        Assert.NotNull(fifoAfterReceipts);
        Assert.Equal((fifoBefore?.Quantity ?? 0m) + 20m, fifoAfterReceipts.Quantity);
        Assert.Equal((fifoBefore?.CarryingValue ?? 0m) + 2200m, fifoAfterReceipts.CarryingValue);
        var fifoIssue = await repository.IssueAsync(tenant, actorId, entityId,
            fifoReceiptOne with { EnteredQuantity = 12m, UnitCost = null, IdempotencyKey = $"inventory-pg-fifo-issue-{suffix}" },
            Guid.CreateVersion7(), null, default);
        var fifoAfterIssue = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        Assert.NotNull(fifoAfterIssue);
        Assert.Equal(fifoAfterReceipts.Quantity - 12m, fifoAfterIssue.Quantity);
        var (remainingLayerQuantity, remainingLayerValue) = await GetLayerTotalsAsync(dataSource, tenantId, entityId, fifoVariantId);
        Assert.Equal(fifoAfterIssue.Quantity, remainingLayerQuantity);
        Assert.Equal(fifoAfterIssue.CarryingValue, remainingLayerValue);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, fifoIssue.OperationId, "StockIssuedV1"));
        var fifoIssueReversal = await repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(fifoIssue.OperationId, today, today, $"inventory-pg-fifo-issue-reversal-{suffix}"),
            Guid.CreateVersion7(), null, default);
        Assert.Single(fifoIssueReversal.MovementIds);
        var fifoAfterReversal = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        Assert.NotNull(fifoAfterReversal);
        Assert.Equal(fifoAfterReceipts.Quantity, fifoAfterReversal.Quantity);
        Assert.Equal(fifoAfterReceipts.CarryingValue, fifoAfterReversal.CarryingValue);
        var (restoredLayerQuantity, restoredLayerValue) = await GetLayerTotalsAsync(dataSource, tenantId, entityId, fifoVariantId);
        Assert.Equal(fifoAfterReversal.Quantity, restoredLayerQuantity);
        Assert.Equal(fifoAfterReversal.CarryingValue, restoredLayerValue);
        var fifoBeforeConcurrentReceipts = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        await Task.WhenAll(
            repository.ReceiveAsync(tenant, actorId, entityId, fifoReceiptOne with { EnteredQuantity = 1m, UnitCost = 130m, IdempotencyKey = $"inventory-pg-fifo-race-a-{suffix}" }, Guid.CreateVersion7(), null, default),
            repository.ReceiveAsync(tenant, actorId, entityId, fifoReceiptOne with { EnteredQuantity = 1m, UnitCost = 140m, IdempotencyKey = $"inventory-pg-fifo-race-b-{suffix}" }, Guid.CreateVersion7(), null, default));
        var fifoAfterConcurrentReceipts = await capabilities.GetValuationAsync(tenantId, new InventoryValuationQuery(entityId, fifoVariantId, today), default);
        Assert.NotNull(fifoBeforeConcurrentReceipts);
        Assert.NotNull(fifoAfterConcurrentReceipts);
        Assert.Equal(fifoBeforeConcurrentReceipts.Quantity + 2m, fifoAfterConcurrentReceipts.Quantity);
        Assert.Equal(fifoBeforeConcurrentReceipts.CarryingValue + 270m, fifoAfterConcurrentReceipts.CarryingValue);

        var productScan = await capabilities.ResolveScanAsync(tenantId, entityId, "EG-1-0002", default);
        Assert.Equal("Product", productScan?.Kind);
        Assert.Equal(productVariantId, productScan?.ProductVariantId);
        var locationScan = await capabilities.ResolveScanAsync(tenantId, entityId, "6220000000001", default);
        Assert.Equal("Location", locationScan?.Kind);
        var lotScan = await capabilities.ResolveScanAsync(tenantId, entityId, lot.Reference, default);
        Assert.Equal("Lot", lotScan?.Kind);
        var serialVariantId = Stable($"{tenantId:D}:variant:0006:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, serialVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(serialVariantId, InventoryState.StockTracked, TrackingMode.Serial,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Disallow, Stable($"{tenantId:D}:inventory:warehouse-structured"), ReservationMode.Advisory),
                Guid.CreateVersion7(), default);
        var serial = await repository.CreateSerialAsync(tenant, actorId, entityId,
            new CreateSerialRequest(serialVariantId, $"SN-PG-{suffix}"), Guid.CreateVersion7(), default);
        var serialScan = await capabilities.ResolveScanAsync(tenantId, entityId, serial.Reference, default);
        Assert.Equal("Serial", serialScan?.Kind);
        var serialUomId = await FindBaseUomAsync(dataSource, tenantId, serialVariantId);
        var serialReceipt = await repository.ReceiveAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(serialVariantId, warehouseId, binId, serialUomId, 1m, today, today,
                $"inventory-pg-serial-receipt-{suffix}", "inventory", null, SerialId: serial.Id),
            Guid.CreateVersion7(), null, default);
        Assert.Single(serialReceipt.MovementIds);
        var serialReservation = await repository.ReserveAsync(tenant, actorId, entityId,
            new ReserveStockRequest(serialVariantId, warehouseId, binId, 1m, "inventory", Guid.CreateVersion7(),
                Guid.CreateVersion7(), $"inventory-pg-serial-reservation-{suffix}", SerialId: serial.Id),
            Guid.CreateVersion7(), default);
        var serialIssue = await capabilities.ConsumeReservationAsync(tenant, actorId, entityId, serialReservation.ReservationId,
            new ConfirmStockMovementRequest(serialVariantId, warehouseId, binId, serialUomId, 1m, today, today,
                $"inventory-pg-serial-issue-{suffix}", "inventory", null, SerialId: serial.Id), Guid.CreateVersion7(), null, default);
        Assert.Single(serialIssue.MovementIds);
        var serialRaceNumber = $"SN-RACE-{suffix}";
        var serialRace = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await repository.CreateSerialAsync(tenant, actorId, entityId,
                    new CreateSerialRequest(serialVariantId, serialRaceNumber), Guid.CreateVersion7(), default);
                return true;
            }
            catch (InventoryException) { return false; }
        }));
        Assert.Single(serialRace, created => created);
        Assert.Single(serialRace, created => !created);

        var serialCountWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"SCNT-{suffix[..8]}", "مخزن جرد الأرقام التسلسلية", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var foundSerial = await repository.CreateSerialAsync(tenant, actorId, entityId,
            new CreateSerialRequest(serialVariantId, $"SN-FOUND-{suffix}"), Guid.CreateVersion7(), default);
        var foundSerialCount = await repository.CreatePhysicalCountAsync(tenant, actorId, entityId,
            new CreatePhysicalCountRequest(serialCountWarehouse.Id, "Simple"), Guid.CreateVersion7(), default);
        var foundSerialLine = await repository.AddPhysicalCountLineAsync(tenant, actorId, entityId, foundSerialCount.Id,
            new AddCountLineRequest(serialVariantId, serialCountWarehouse.DefaultLocationId, SerialId: foundSerial.Id),
            Guid.CreateVersion7(), default);
        Assert.Equal(0m, foundSerialLine.Expected);
        await repository.EnterPhysicalCountLineAsync(tenant, actorId, entityId, foundSerialCount.Id, foundSerialLine.Id,
            new EnterCountLineRequest(1m), Guid.CreateVersion7(), default);
        await repository.PostPhysicalCountAsync(tenant, actorId, entityId, foundSerialCount.Id,
            $"inventory-pg-found-serial-count-{suffix}", Guid.CreateVersion7(), null, default);
        Assert.Equal(1m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, serialVariantId,
            serialCountWarehouse.Id, 100, default)).OnHandBaseQuantity);

        var missingSerialCount = await repository.CreatePhysicalCountAsync(tenant, actorId, entityId,
            new CreatePhysicalCountRequest(serialCountWarehouse.Id, "Simple"), Guid.CreateVersion7(), default);
        var missingSerialLine = Assert.Single(await repository.GetPhysicalCountLinesAsync(tenant, entityId, missingSerialCount.Id, 100, default));
        Assert.Equal(foundSerial.Id, missingSerialLine.SerialId);
        await repository.EnterPhysicalCountLineAsync(tenant, actorId, entityId, missingSerialCount.Id, missingSerialLine.Id,
            new EnterCountLineRequest(0m), Guid.CreateVersion7(), default);
        await repository.PostPhysicalCountAsync(tenant, actorId, entityId, missingSerialCount.Id,
            $"inventory-pg-missing-serial-count-{suffix}", Guid.CreateVersion7(), null, default);
        Assert.Empty(await repository.GetBalancesAsync(tenant, entityId, serialVariantId, serialCountWarehouse.Id, 100, default));

        var lifecycleWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"LIFE-{suffix[..8]}", "مخزن اختبار دورة الحياة", LocationTrackingMode.Locations, null),
            Guid.CreateVersion7(), default);
        var emptyLocation = await repository.CreateLocationAsync(tenant, actorId, entityId,
            new CreateLocationRequest(lifecycleWarehouse.Id, null, $"EMPTY-{suffix[..8]}", "موقع فارغ", "Bin", null, true, true, true),
            Guid.CreateVersion7(), default);
        await repository.ArchiveLocationAsync(tenant, actorId, entityId, emptyLocation.Id, Guid.CreateVersion7(), default);
        Assert.False(Assert.Single(await repository.ListLocationsAsync(tenant, entityId, lifecycleWarehouse.Id, default), item => item.Id == emptyLocation.Id).Active);
        var suspendedWarehouse = await repository.ChangeWarehouseStatusAsync(tenant, actorId, entityId, lifecycleWarehouse.Id,
            WarehouseStatus.Suspended, Guid.CreateVersion7(), default);
        Assert.Equal(WarehouseStatus.Suspended, suspendedWarehouse.Status);
        await Assert.ThrowsAsync<InventoryException>(() => repository.ReceiveAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(productVariantId, lifecycleWarehouse.Id, null, unitId, 1m, today, today,
                $"inventory-pg-suspended-receipt-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default));
        var activeWarehouse = await repository.ChangeWarehouseStatusAsync(tenant, actorId, entityId, lifecycleWarehouse.Id,
            WarehouseStatus.Active, Guid.CreateVersion7(), default);
        Assert.Equal(WarehouseStatus.Active, activeWarehouse.Status);
        var archivedWarehouse = await repository.ChangeWarehouseStatusAsync(tenant, actorId, entityId, lifecycleWarehouse.Id,
            WarehouseStatus.Archived, Guid.CreateVersion7(), default);
        Assert.Equal(WarehouseStatus.Archived, archivedWarehouse.Status);
        await Assert.ThrowsAsync<InventoryException>(() => repository.ChangeWarehouseStatusAsync(tenant, actorId, entityId,
            warehouseId, WarehouseStatus.Archived, Guid.CreateVersion7(), default));
        await Assert.ThrowsAsync<InventoryException>(() => repository.ArchiveLocationAsync(tenant, actorId, entityId,
            lifecycleWarehouse.DefaultLocationId, Guid.CreateVersion7(), default));

        var countVariantId = Stable($"{tenantId:D}:variant:0007:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, countVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(countVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Disallow, null, ReservationMode.Disabled),
                Guid.CreateVersion7(), default);
        var countWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"COUNT-{suffix[..8]}", "مخزن جرد", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var countUnitId = await FindBaseUomAsync(dataSource, tenantId, countVariantId);
        var openingRequest = new ConfirmStockMovementRequest(countVariantId, countWarehouse.Id, null, countUnitId, 100m,
            today, today, $"inventory-pg-opening-{suffix}", "inventory", null);
        var opening = await repository.OpenAsync(tenant, actorId, entityId, openingRequest, Guid.CreateVersion7(), null, default);
        var openingReplay = await repository.OpenAsync(tenant, actorId, entityId, openingRequest, Guid.CreateVersion7(), null, default);
        Assert.False(opening.IdempotentReplay);
        Assert.True(openingReplay.IdempotentReplay);
        var count = await repository.CreatePhysicalCountAsync(tenant, actorId, entityId,
            new CreatePhysicalCountRequest(countWarehouse.Id, "Simple"), Guid.CreateVersion7(), default);
        var countLine = Assert.Single(await repository.GetPhysicalCountLinesAsync(tenant, entityId, count.Id, 100, default));
        Assert.Equal(100m, countLine.Expected);
        await repository.EnterPhysicalCountLineAsync(tenant, actorId, entityId, count.Id, countLine.Id,
            new EnterCountLineRequest(96m), Guid.CreateVersion7(), default);
        var postedCount = await repository.PostPhysicalCountAsync(tenant, actorId, entityId, count.Id,
            $"inventory-pg-count-post-{suffix}", Guid.CreateVersion7(), null, default);
        Assert.Equal("Posted", postedCount.Status);
        Assert.Equal(-4m, postedCount.VarianceQuantity);
        Assert.Equal(96m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, countVariantId,
            countWarehouse.Id, 100, default)).OnHandBaseQuantity);

        var allowedNegativeVariantId = Stable($"{tenantId:D}:variant:0009:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, allowedNegativeVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(allowedNegativeVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Allow, null, ReservationMode.Disabled),
                Guid.CreateVersion7(), default);
        var allowedNegativeWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"NEG-{suffix[..8]}", "مخزن اختبار المخزون السالب", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var allowedNegativeUnit = await FindBaseUomAsync(dataSource, tenantId, allowedNegativeVariantId);
        var negativeIssue = await repository.IssueAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(allowedNegativeVariantId, allowedNegativeWarehouse.Id, null, allowedNegativeUnit, 1m,
                today, today, $"inventory-pg-negative-issue-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        Assert.Equal(-1m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, allowedNegativeVariantId,
            allowedNegativeWarehouse.Id, 100, default)).OnHandBaseQuantity);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, negativeIssue.MovementIds[0], "NegativeStockConditionRaisedV1"));
        var activeNegativeCondition = Assert.Single(await repository.GetNegativeStockConditionsAsync(tenant, entityId,
            "Active", null, null, 100, default), row => row.ProductVariantId == allowedNegativeVariantId);
        Assert.Equal(1m, activeNegativeCondition.ShortfallBaseQuantity);
        var resolvingReceipt = await repository.ReceiveAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(allowedNegativeVariantId, allowedNegativeWarehouse.Id, null, allowedNegativeUnit, 1m,
                today, today, $"inventory-pg-negative-receipt-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, resolvingReceipt.MovementIds[0], "NegativeStockConditionResolvedV1"));
        var resolvedNegativeCondition = Assert.Single(await repository.GetNegativeStockConditionsAsync(tenant, entityId,
            "Resolved", null, null, 100, default), row => row.Id == activeNegativeCondition.Id);
        Assert.Equal("Resolved", resolvedNegativeCondition.Status);

        var permissionNegativeVariantId = Stable($"{tenantId:D}:variant:0010:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, permissionNegativeVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(permissionNegativeVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.AllowWithWarningAndPermission, null, ReservationMode.Disabled),
                Guid.CreateVersion7(), default);
        var permissionNegativeWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"NGA-{suffix[..8]}", "مخزن اختبار صلاحية المخزون السالب", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var permissionNegativeUnit = await FindBaseUomAsync(dataSource, tenantId, permissionNegativeVariantId);
        var permissionIssue = new ConfirmStockMovementRequest(permissionNegativeVariantId, permissionNegativeWarehouse.Id, null,
            permissionNegativeUnit, 1m, today, today, $"inventory-pg-negative-permission-{suffix}", "inventory", null);
        await Assert.ThrowsAsync<InventoryException>(() => repository.IssueAsync(tenant, actorId, entityId, permissionIssue,
            Guid.CreateVersion7(), null, default));
        await repository.IssueAsync(tenant, actorId, entityId, permissionIssue, Guid.CreateVersion7(), null, default,
            canOverrideNegativeStock: true);
        Assert.Equal(-1m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, permissionNegativeVariantId,
            permissionNegativeWarehouse.Id, 100, default)).OnHandBaseQuantity);

        var issueRaceVariantId = Stable($"{tenantId:D}:variant:0011:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, issueRaceVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(issueRaceVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Disallow, null, ReservationMode.Disabled),
                Guid.CreateVersion7(), default);
        var issueRaceWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"IR-{suffix[..8]}", "مخزن اختبار تزامن الصرف", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var issueRaceUnit = await FindBaseUomAsync(dataSource, tenantId, issueRaceVariantId);
        await repository.OpenAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(issueRaceVariantId, issueRaceWarehouse.Id, null, issueRaceUnit, 1m,
                today, today, $"inventory-pg-issue-race-opening-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        var issueRace = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
        {
            try
            {
                await repository.IssueAsync(tenant, actorId, entityId,
                    new ConfirmStockMovementRequest(issueRaceVariantId, issueRaceWarehouse.Id, null, issueRaceUnit, 1m,
                        today, today, $"inventory-pg-issue-race-{index}-{suffix}", "inventory", null),
                    Guid.CreateVersion7(), null, default);
                return true;
            }
            catch (InventoryException) { return false; }
        }));
        Assert.Single(issueRace, succeeded => succeeded);
        Assert.Single(issueRace, succeeded => !succeeded);
        Assert.Empty(await repository.GetBalancesAsync(tenant, entityId, issueRaceVariantId, issueRaceWarehouse.Id, 100, default));

        var reservationRaceVariantId = Stable($"{tenantId:D}:variant:0012:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, reservationRaceVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(reservationRaceVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Disallow, null, ReservationMode.Enforced),
                Guid.CreateVersion7(), default);
        var reservationRaceWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"RR-{suffix[..8]}", "مخزن اختبار تزامن الحجز", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var reservationRaceUnit = await FindBaseUomAsync(dataSource, tenantId, reservationRaceVariantId);
        await repository.OpenAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(reservationRaceVariantId, reservationRaceWarehouse.Id, null, reservationRaceUnit, 1m,
                today, today, $"inventory-pg-reservation-race-opening-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        var competingReservations = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await repository.ReserveAsync(tenant, actorId, entityId,
                    new ReserveStockRequest(reservationRaceVariantId, reservationRaceWarehouse.Id, null, 1m, "inventory",
                        Guid.CreateVersion7(), Guid.CreateVersion7(), $"inventory-pg-reservation-race-{Guid.NewGuid():N}"),
                    Guid.CreateVersion7(), default);
                return true;
            }
            catch (InventoryException) { return false; }
        }));
        Assert.Single(competingReservations, succeeded => succeeded);
        Assert.Single(competingReservations, succeeded => !succeeded);
        var reservationRaceBalance = Assert.Single(await repository.GetBalancesAsync(tenant, entityId, reservationRaceVariantId,
            reservationRaceWarehouse.Id, 100, default));
        Assert.Equal(1m, reservationRaceBalance.OnHandBaseQuantity);
        Assert.Equal(1m, reservationRaceBalance.ReservedBaseQuantity);
        var adjustment = await repository.AdjustAsync(tenant, actorId, entityId,
            new AdjustStockRequest(countVariantId, countWarehouse.Id, null, countUnitId, 2m,
                AdjustmentDirection.Decrease, AdjustmentReason.Damage, today, today, $"inventory-pg-adjust-{suffix}"),
            Guid.CreateVersion7(), null, default);
        Assert.Single(adjustment.MovementIds);
        Assert.Equal(94m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, countVariantId,
            countWarehouse.Id, 100, default)).OnHandBaseQuantity);
        Assert.Equal(1L, await CountOutboxAsync(dataSource, tenantId, adjustment.OperationId, "StockAdjustedV1"));
        var adjustmentReversalRequest = new ReverseInventoryOperationRequest(adjustment.OperationId, today, today, $"inventory-pg-reverse-adjust-{suffix}");
        var adjustmentReversal = await repository.ReverseOperationAsync(tenant, actorId, entityId,
            adjustmentReversalRequest, Guid.CreateVersion7(), null, default);
        Assert.False(adjustmentReversal.IdempotentReplay);
        Assert.Equal(96m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, countVariantId,
            countWarehouse.Id, 100, default)).OnHandBaseQuantity);
        Assert.True((await repository.ReverseOperationAsync(tenant, actorId, entityId, adjustmentReversalRequest,
            Guid.CreateVersion7(), null, default)).IdempotentReplay);
        await Assert.ThrowsAsync<InventoryException>(() => repository.ReverseOperationAsync(tenant, actorId, entityId,
            adjustmentReversalRequest with { IdempotencyKey = $"inventory-pg-second-adjust-reversal-{suffix}" },
            Guid.CreateVersion7(), null, default));

        var reversalWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"REV-{suffix[..8]}", "مخزن اختبار العكس", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var receiptToReverse = await repository.ReceiveAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(countVariantId, reversalWarehouse.Id, null, countUnitId, 5m, today, today,
                $"inventory-pg-reversible-receipt-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        var issueToReverse = await repository.IssueAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(countVariantId, reversalWarehouse.Id, null, countUnitId, 2m, today, today,
                $"inventory-pg-reversible-issue-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        await repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(issueToReverse.OperationId, today, today, $"inventory-pg-reverse-issue-{suffix}"),
            Guid.CreateVersion7(), null, default);
        Assert.Equal(5m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, countVariantId,
            reversalWarehouse.Id, 100, default)).OnHandBaseQuantity);
        await repository.ReverseOperationAsync(tenant, actorId, entityId,
            new ReverseInventoryOperationRequest(receiptToReverse.OperationId, today, today, $"inventory-pg-reverse-receipt-{suffix}"),
            Guid.CreateVersion7(), null, default);
        Assert.Empty(await repository.GetBalancesAsync(tenant, entityId, countVariantId, reversalWarehouse.Id, 100, default));

        var expiryVariantId = Stable($"{tenantId:D}:variant:0008:default");
        if (!await HasInventoryProfileAsync(dataSource, tenantId, entityId, expiryVariantId))
            await repository.SaveProfileAsync(tenant, actorId, entityId,
                new CreateInventoryProfileRequest(expiryVariantId, InventoryState.StockTracked, TrackingMode.Quantity,
                    ValuationMode.NonValuated, null, NegativeStockPolicy.Disallow, null, ReservationMode.Advisory),
                Guid.CreateVersion7(), default);
        var expiryWarehouse = await repository.CreateWarehouseAsync(tenant, actorId, entityId,
            new CreateWarehouseRequest($"EXP-{suffix[..8]}", "مخزن انتهاء الحجز", LocationTrackingMode.WarehouseOnly, null),
            Guid.CreateVersion7(), default);
        var expiryUnitId = await FindBaseUomAsync(dataSource, tenantId, expiryVariantId);
        await repository.OpenAsync(tenant, actorId, entityId,
            new ConfirmStockMovementRequest(expiryVariantId, expiryWarehouse.Id, null, expiryUnitId, 3m, today, today,
                $"inventory-pg-expiry-opening-{suffix}", "inventory", null), Guid.CreateVersion7(), null, default);
        var cancelledReservation = await repository.ReserveAsync(tenant, actorId, entityId,
            new ReserveStockRequest(expiryVariantId, expiryWarehouse.Id, null, 1m, "inventory", Guid.CreateVersion7(),
                Guid.CreateVersion7(), $"inventory-pg-cancel-reservation-{suffix}"), Guid.CreateVersion7(), default);
        await repository.CancelReservationAsync(tenant, actorId, entityId, cancelledReservation.ReservationId, Guid.CreateVersion7(), default);
        await repository.CancelReservationAsync(tenant, actorId, entityId, cancelledReservation.ReservationId, Guid.CreateVersion7(), default);
        Assert.Equal("Cancelled", await GetReservationStatusAsync(dataSource, tenantId, cancelledReservation.ReservationId));
        var expiryReservation = await repository.ReserveAsync(tenant, actorId, entityId,
            new ReserveStockRequest(expiryVariantId, expiryWarehouse.Id, null, 1m, "inventory", Guid.CreateVersion7(),
                Guid.CreateVersion7(), $"inventory-pg-expiring-reservation-{suffix}", DateTimeOffset.UtcNow.AddMinutes(5)),
            Guid.CreateVersion7(), default);
        var expiryService = new InventoryService(repository);
        var expiryAsOf = DateTimeOffset.UtcNow.AddMinutes(10);
        var expiryRuns = await Task.WhenAll(
            expiryService.ExpireDueReservationsAsync(cancellationToken: default, asOf: expiryAsOf),
            expiryService.ExpireDueReservationsAsync(cancellationToken: default, asOf: expiryAsOf));
        Assert.True(expiryRuns.Sum() >= 1);
        Assert.Equal("Expired", await GetReservationStatusAsync(dataSource, tenantId, expiryReservation.ReservationId));
        Assert.Equal(1L, await CountReservationEventsAsync(dataSource, tenantId, expiryReservation.ReservationId, "Expired"));
        Assert.Equal(0, await expiryService.ExpireDueReservationsAsync(cancellationToken: default, asOf: expiryAsOf));
        var expiryBalance = Assert.Single(await repository.GetBalancesAsync(tenant, entityId, expiryVariantId, expiryWarehouse.Id, 100, default));
        Assert.Equal(3m, expiryBalance.OnHandBaseQuantity);
        Assert.Equal(0m, expiryBalance.ReservedBaseQuantity);

        var putaway = await repository.SuggestPutawayAsync(tenant, entityId, countVariantId, countWarehouse.Id, default);
        Assert.Equal(countWarehouse.DefaultLocationId, putaway?.LocationId);
        var pickSuggestions = await repository.SuggestPickingAsync(tenant, entityId, countVariantId, countWarehouse.Id, 10, default);
        var pick = Assert.Single(pickSuggestions);
        Assert.Equal(countWarehouse.DefaultLocationId, pick.LocationId);
        Assert.Equal(96m, pick.AvailableQuantity);
        Assert.Equal(96m, Assert.Single(await repository.GetBalancesAsync(tenant, entityId, countVariantId,
            countWarehouse.Id, 100, default)).OnHandBaseQuantity);
    }

    private static async Task<Guid> FindBaseUomAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid variantId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select uom_id from foundation_catalog.product_uom where tenant_id=$1 and variant_id=$2 and is_base and status='Active' and effective_from<=now() and (effective_to is null or effective_to>now()) order by effective_from desc limit 1", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(variantId);
        return await command.ExecuteScalarAsync() as Guid? ?? throw new InvalidOperationException("Seeded product has no active base unit.");
    }

    private static async Task AssertTenantRlsAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid variantId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var role = new NpgsqlCommand("select rolsuper or rolbypassrls from pg_roles where rolname=current_user", connection))
            Assert.False((bool)(await role.ExecuteScalarAsync() ?? true));
        var tenantTables = new[] { "warehouse", "movement_sequence", "location", "product_inventory_profile", "lot", "serial", "inventory_operation", "stock_movement_entry", "stock_balance", "reservation", "reservation_allocation", "reservation_event", "inventory_transfer", "transfer_receipt", "transfer_event", "valuation_entry", "cost_layer", "cost_layer_consumption", "cost_state", "physical_count", "count_line", "inventory_audit", "negative_stock_condition" };
        await using (var rls = new NpgsqlCommand("select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='business_inventory' and c.relname=any($1) and c.relrowsecurity and c.relforcerowsecurity", connection))
        {
            rls.Parameters.AddWithValue(tenantTables);
            Assert.Equal((long)tenantTables.Length, (long)(await rls.ExecuteScalarAsync() ?? -1L));
        }
        await using var command = new NpgsqlCommand("select count(*) from foundation_catalog.product_variant where id=$1", connection);
        command.Parameters.AddWithValue(variantId);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? -1L));
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(Stable("tenant:delta").ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? -1L));
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync() ?? -1L));
        await using (var inventoryRows = new NpgsqlCommand("select count(*) from business_inventory.product_inventory_profile where tenant_id=$1 and product_variant_id=$2", connection))
        {
            inventoryRows.Parameters.AddWithValue(tenantId); inventoryRows.Parameters.AddWithValue(variantId);
            Assert.Equal(1L, (long)(await inventoryRows.ExecuteScalarAsync() ?? -1L));
        }
        await using (var forbiddenWrite = new NpgsqlCommand("insert into business_inventory.inventory_audit(tenant_id,id,legal_entity_id,actor_id,action,entity_type,entity_id,occurred_at,correlation_id) values($1,$2,$3,$4,'rls_probe','InventoryOperation',$5,now(),$6)", connection))
        {
            forbiddenWrite.Parameters.AddWithValue(Stable("tenant:delta"));
            forbiddenWrite.Parameters.AddWithValue(Guid.CreateVersion7());
            forbiddenWrite.Parameters.AddWithValue(Stable("tenant:delta:entity"));
            forbiddenWrite.Parameters.AddWithValue(Stable("tenant:delta:actor"));
            forbiddenWrite.Parameters.AddWithValue(Guid.CreateVersion7());
            forbiddenWrite.Parameters.AddWithValue(Guid.CreateVersion7());
            var error = await Assert.ThrowsAsync<PostgresException>(() => forbiddenWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", error.SqlState);
        }
    }

    private static async Task<long> CountOutboxAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid operationId, string messageType)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(operationId);
        command.Parameters.AddWithValue(messageType);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string> GetReservationStatusAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid reservationId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select status from business_inventory.reservation where tenant_id=$1 and id=$2", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(reservationId);
        return (string?)await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Reservation disappeared after commit.");
    }

    private static async Task<long> CountReservationEventsAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid reservationId, string eventType)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select count(*) from business_inventory.reservation_event where tenant_id=$1 and reservation_id=$2 and event_type=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(reservationId);
        command.Parameters.AddWithValue(eventType);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<bool> HasInventoryProfileAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid entityId, Guid variantId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select exists(select 1 from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3)", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(entityId);
        command.Parameters.AddWithValue(variantId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<(decimal Quantity, decimal Value)> GetLayerTotalsAsync(NpgsqlDataSource dataSource, Guid tenantId, Guid entityId, Guid variantId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select coalesce(sum(remaining_quantity),0),coalesce(sum(remaining_quantity*unit_cost),0) from business_inventory.cost_layer where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(entityId);
        command.Parameters.AddWithValue(variantId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private static async Task<(long Operations, long Movements, long Valuations, long Audit, long Outbox, long Facts, long Entries)> ReadInventoryEffectCountsAsync(
        NpgsqlDataSource dataSource, Guid tenantId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select (select count(*) from business_inventory.inventory_operation where tenant_id=$1),(select count(*) from business_inventory.stock_movement_entry where tenant_id=$1),(select count(*) from business_inventory.valuation_entry where tenant_id=$1),(select count(*) from business_inventory.inventory_audit where tenant_id=$1),(select count(*) from platform.outbox_message where tenant_id=$1),(select count(*) from finance_ledger.accounting_fact where tenant_id=$1),(select count(*) from finance_ledger.journal_entry where tenant_id=$1)", connection);
        command.Parameters.AddWithValue(tenantId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
    }

    private static async Task<(string FactType, IReadOnlyList<string> Roles)> GetInventoryLedgerRolesAsync(
        NpgsqlDataSource dataSource, Guid tenantId, Guid entityId, Guid movementId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using (var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection))
        {
            tenantContext.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenantContext.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select f.fact_type,l.semantic_role from business_inventory.valuation_entry v join finance_ledger.accounting_fact f on f.tenant_id=v.tenant_id and f.legal_entity_id=v.legal_entity_id and f.id=v.accounting_fact_id join finance_ledger.journal_entry_line l on l.tenant_id=f.tenant_id and l.legal_entity_id=f.legal_entity_id and l.journal_entry_id=f.posted_entry_id where v.tenant_id=$1 and v.legal_entity_id=$2 and v.movement_id=$3 order by l.sequence_no", connection);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(movementId);
        string? factType = null; var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { factType ??= reader.GetString(0); roles.Add(reader.GetString(1)); }
        return (factType ?? throw new InvalidOperationException("Valuation did not create its expected Ledger fact."), roles);
    }

    private static Guid Stable(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"development-seed:2026-10-10:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }

    private sealed class InjectedLedgerFailureException() : Exception("Deterministic Inventory ledger failpoint.");

    private sealed class FailingInventoryLedgerPostingCapability(ITransactionalInventoryLedgerPostingCapability inner)
        : ITransactionalInventoryLedgerPostingCapability
    {
        public Task<string?> ValidateInventoryPostingDateAsync(ITransactionContext context, TenantId tenantId,
            LegalEntityId legalEntityId, DateOnly postingDate, CancellationToken cancellationToken = default) =>
            inner.ValidateInventoryPostingDateAsync(context, tenantId, legalEntityId, postingDate, cancellationToken);

        public async Task<LedgerPostingResult> PostInventoryValuationAsync(ITransactionContext context,
            AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationFactPayload payload,
            CancellationToken cancellationToken = default)
        {
            await inner.PostInventoryValuationAsync(context, fact, payload, cancellationToken);
            throw new InjectedLedgerFailureException();
        }

        public async Task<LedgerPostingResult> ReverseInventoryValuationAsync(ITransactionContext context,
            AccountingFactEnvelope<IAccountingFactPayload> fact, InventoryValuationReversalPayload payload,
            CancellationToken cancellationToken = default)
        {
            await inner.ReverseInventoryValuationAsync(context, fact, payload, cancellationToken);
            throw new InjectedLedgerFailureException();
        }
    }
}
