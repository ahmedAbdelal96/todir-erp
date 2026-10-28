using Modules.Inventory.Entities;
using Xunit;

namespace Modules.Inventory.Tests;

public sealed class InventoryCostingTests
{
    private static ValuationState Empty => new(0m, 0m, 0m, []);

    [Fact]
    public void MovingAverageReceiptAndIssueMatchGoldenVector()
    {
        var state = InventoryCosting.ReceiveMovingAverage(Empty, 10m, 100m);
        state = InventoryCosting.ReceiveMovingAverage(state, 10m, 120m);

        Assert.Equal(20m, state.Quantity);
        Assert.Equal(2200m, state.CarryingValue);
        Assert.Equal(110m, state.AverageUnitCost);

        var (afterIssue, consumption) = InventoryCosting.IssueMovingAverage(state, 5m);
        Assert.Equal(550m, consumption.Value);
        Assert.Equal(15m, afterIssue.Quantity);
        Assert.Equal(1650m, afterIssue.CarryingValue);
        Assert.Equal(110m, afterIssue.AverageUnitCost);
    }

    [Fact]
    public void FifoConsumesOldestLayersAndLeavesExactRemainder()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var state = InventoryCosting.ReceiveFifo(Empty, first, 10m, 100m, new DateOnly(2026, 1, 1));
        state = InventoryCosting.ReceiveFifo(state, second, 10m, 120m, new DateOnly(2026, 1, 2));

        var (afterIssue, consumption) = InventoryCosting.IssueFifo(state, 12m);

        Assert.Equal(1240m, consumption.Value);
        Assert.Equal(8m, afterIssue.Quantity);
        Assert.Equal(960m, afterIssue.CarryingValue);
        Assert.Single(afterIssue.Layers);
        Assert.Equal(8m, afterIssue.Layers[0].RemainingQuantity);
        Assert.Equal(120m, afterIssue.Layers[0].UnitCost);
    }

    [Fact]
    public void IssueCannotCreateAccidentalNegativeStock()
    {
        var error = Assert.Throws<InventoryException>(() => InventoryCosting.IssueMovingAverage(Empty, 1m));
        Assert.Equal("inventory.insufficient_stock", error.Code);
    }

    [Fact]
    public void ProfileRejectsCostingForNonValuatedStock()
    {
        var profile = new InventoryProfile(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            InventoryState.StockTracked, TrackingMode.Quantity, ValuationMode.NonValuated, CostingMethod.FIFO,
            NegativeStockPolicy.Disallow, null);

        var error = Assert.Throws<InventoryException>(profile.Validate);
        Assert.Equal("inventory.costing_method_not_applicable", error.Code);
    }

    [Fact]
    public void InTransitTransferMovesQuantityOutOfSourceAndIntoTransitWithoutChangingTotal()
    {
        var tenant = Guid.CreateVersion7(); var entity = Guid.CreateVersion7(); var product = Guid.CreateVersion7();
        var source = new StockPosition(tenant, entity, product, Guid.CreateVersion7(), Guid.CreateVersion7(), StockStatus.Available);
        var transitPosition = new StockPosition(tenant, entity, product, Guid.CreateVersion7(), Guid.CreateVersion7(), StockStatus.Available);

        var legs = InventoryInvariants.CreateTransfer(source, transitPosition, 12.5m, inTransit: true);

        Assert.Equal(0m, legs.Sum(leg => leg.SignedBaseQuantity));
        Assert.Equal(-12.5m, legs[0].SignedBaseQuantity);
        Assert.Equal(12.5m, legs[1].SignedBaseQuantity);
        Assert.Equal(StockStatus.InTransit, legs[1].Position.Status);
    }

    [Fact]
    public void EnforcedReservationProtectsAvailableQuantityAndDoesNotChangeOnHand()
    {
        const decimal onHand = 10m;
        Assert.Throws<InventoryException>(() => InventoryInvariants.EnsureReservationAllowed(ReservationMode.Enforced, onHand, 7m, 4m));
        InventoryInvariants.EnsureReservationAllowed(ReservationMode.Enforced, onHand, 7m, 3m);
        Assert.Equal(10m, onHand);
    }

    [Fact]
    public void LocationHierarchyRejectsCycleAndDatePolicyRejectsClosedPeriod()
    {
        var root = Guid.CreateVersion7(); var child = Guid.CreateVersion7();
        var parent = new Dictionary<Guid, Guid?> { [root] = null, [child] = root };
        Assert.Throws<InventoryException>(() => InventoryInvariants.EnsureLocationMoveIsAcyclic(root, child, parent));
        var dateError = Assert.Throws<InventoryException>(() => InventoryInvariants.EnsureOperationalDateOpen(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1)));
        Assert.Equal("inventory.period_locked", dateError.Code);
    }

    [Fact]
    public void StockBalanceCanBeRebuiltFromStableOrderedImmutableMovementHistory()
    {
        var position = new StockPosition(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), StockStatus.Available);
        var at = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new[]
        {
            new MovementProjection(position, -3m, 2, at.AddMinutes(1), Guid.CreateVersion7()),
            new MovementProjection(position, 10m, 1, at, Guid.CreateVersion7())
        };

        Assert.Equal(7m, InventoryInvariants.RebuildBalances(history)[position]);
    }
}
