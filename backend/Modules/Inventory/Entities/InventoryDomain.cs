namespace Modules.Inventory.Entities;

public enum InventoryState { NonStocked, StockTracked }
public enum TrackingMode { Quantity, Lot, Serial }
public enum ValuationMode { NonValuated, Perpetual }
public enum CostingMethod { MovingAverage, FIFO }
public enum NegativeStockPolicy { Disallow, AllowWithWarningAndPermission, Allow }
public enum LocationTrackingMode { WarehouseOnly, Locations, HierarchicalLocations }
public enum WarehouseStatus { Active, Suspended, Archived }
public enum StockStatus { Available, QualityHold, Quarantine, Damaged, ReturnsInspection, Blocked, ScrapPending, InTransit }
public enum MovementKind { Receipt, Issue, Transfer, Return, Adjustment, Opening, Reversal, StatusChange }
public enum OperationStatus { Draft, Confirmed, Reversed }
public enum ReservationMode { Disabled, Advisory, Enforced }
public enum ReservationStatus { Active, Released, Consumed, Expired, Cancelled }
public enum TransferMode { Instant, InTransit }
public enum PhysicalCountMode { Simple, Full, Cycle, Blind }
public enum AdjustmentDirection { Increase, Decrease }
public enum AdjustmentReason { CountDifference, Damage, Loss, FoundStock, DataCorrection, Scrap, UomCorrection, OtherApproved }
public enum AdjustmentApprovalMode { DirectConfirm, ApprovalRequired }
public enum AdjustmentRequestStatus { Draft, PendingApproval, Approved, Posted, Rejected, Cancelled }

public sealed record InventoryProfile(
    Guid TenantId, Guid LegalEntityId, Guid ProductVariantId, InventoryState State,
    TrackingMode Tracking, ValuationMode Valuation, CostingMethod? Costing,
    NegativeStockPolicy NegativeStock, Guid? DefaultWarehouseId,
    ReservationMode ReservationMode = ReservationMode.Disabled, long Version = 1)
{
    public void Validate()
    {
        if (TenantId == Guid.Empty || LegalEntityId == Guid.Empty || ProductVariantId == Guid.Empty)
            throw new InventoryException("inventory.profile_identity_invalid");
        if (State == InventoryState.NonStocked && (Tracking != TrackingMode.Quantity || Valuation != ValuationMode.NonValuated || Costing is not null))
            throw new InventoryException("inventory.non_stocked_profile_invalid");
        if (Valuation == ValuationMode.Perpetual && Costing is null)
            throw new InventoryException("inventory.costing_method_required");
        if (Valuation == ValuationMode.NonValuated && Costing is not null)
            throw new InventoryException("inventory.costing_method_not_applicable");
    }
}

public sealed record Warehouse(Guid Id, Guid TenantId, Guid LegalEntityId, string Code, string Name,
    LocationTrackingMode LocationMode, Guid DefaultLocationId, WarehouseStatus Status, Guid? BranchId = null);

public sealed record InventoryLocation(Guid Id, Guid TenantId, Guid LegalEntityId, Guid WarehouseId,
    Guid? ParentLocationId, string Code, string Name, bool Hidden, bool CanHoldStock, bool CanReceiveInto,
    bool CanPickFrom, bool Active);

public sealed record StockPosition(Guid TenantId, Guid LegalEntityId, Guid ProductVariantId, Guid WarehouseId,
    Guid LocationId, StockStatus Status, Guid? LotId = null, Guid? SerialId = null);

public sealed record StockBalance(StockPosition Position, decimal OnHandBaseQuantity,
    decimal ReservedBaseQuantity, long Version, long LastMovementSequence)
{
    public decimal AvailableBaseQuantity => Math.Max(0m, OnHandBaseQuantity - ReservedBaseQuantity);
}

public sealed record MovementDraft(Guid Id, Guid TenantId, Guid LegalEntityId, Guid ProductVariantId,
    Guid WarehouseId, Guid LocationId, decimal EnteredQuantity, Guid EnteredUomId,
    decimal ConversionFactorSnapshot, decimal BaseQuantity, string SourceModule, Guid? SourceDocumentId,
    string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate, MovementKind Kind,
    StockStatus Status = StockStatus.Available, Guid? LotId = null, Guid? SerialId = null,
    decimal? UnitCost = null, string? CurrencyCode = null)
{
    public void Validate(InventoryProfile profile)
    {
        profile.Validate();
        if (profile.TenantId != TenantId || profile.LegalEntityId != LegalEntityId || profile.ProductVariantId != ProductVariantId)
            throw new InventoryException("inventory.profile_scope_mismatch");
        if (profile.State != InventoryState.StockTracked) throw new InventoryException("inventory.product_not_stock_tracked");
        if (Id == Guid.Empty || WarehouseId == Guid.Empty || LocationId == Guid.Empty || EnteredUomId == Guid.Empty)
            throw new InventoryException("inventory.movement_identity_invalid");
        if (EnteredQuantity <= 0m || ConversionFactorSnapshot <= 0m || BaseQuantity <= 0m
            || BaseQuantity != EnteredQuantity * ConversionFactorSnapshot)
            throw new InventoryException("inventory.quantity_invalid");
        if (string.IsNullOrWhiteSpace(IdempotencyKey) || IdempotencyKey.Length > 200)
            throw new InventoryException("inventory.idempotency_key_invalid");
        if (profile.Tracking == TrackingMode.Lot && LotId is null) throw new InventoryException("inventory.lot_required");
        if (profile.Tracking == TrackingMode.Serial && SerialId is null) throw new InventoryException("inventory.serial_required");
        if (profile.Tracking == TrackingMode.Quantity && (LotId is not null || SerialId is not null))
            throw new InventoryException("inventory.traceability_not_enabled");
        if (SerialId is not null && BaseQuantity != 1m) throw new InventoryException("inventory.serial_quantity_must_be_one");
        if (profile.Valuation == ValuationMode.Perpetual && Kind is MovementKind.Receipt or MovementKind.Return or MovementKind.Opening
            && (UnitCost is null or < 0m || string.IsNullOrWhiteSpace(CurrencyCode)))
            throw new InventoryException("inventory.receipt_cost_required");
        if (profile.Valuation == ValuationMode.Perpetual && Kind == MovementKind.Issue && UnitCost is not null)
            throw new InventoryException("inventory.issue_cost_is_calculated");
        if (profile.Valuation == ValuationMode.NonValuated && UnitCost is not null)
            throw new InventoryException("inventory.cost_not_allowed");
        if (PostingDate < OperationalDate) throw new InventoryException("inventory.posting_before_operation_invalid");
    }
}

public sealed record ValuationState(decimal Quantity, decimal CarryingValue, decimal AverageUnitCost,
    IReadOnlyList<CostLayer> Layers);
public sealed record CostLayer(Guid Id, decimal RemainingQuantity, decimal UnitCost, DateOnly EffectiveDate);
public sealed record CostConsumption(decimal Quantity, decimal Value, IReadOnlyList<CostLayer> RemainingLayers);
public sealed record MovementProjection(StockPosition Position, decimal SignedBaseQuantity, long Sequence, DateTimeOffset EffectiveAt, Guid Id);
public sealed record TransferLeg(StockPosition Position, decimal SignedBaseQuantity, string Stage);

public static class InventoryCosting
{
    public static ValuationState ReceiveMovingAverage(ValuationState state, decimal quantity, decimal unitCost)
    {
        ValidateReceipt(quantity, unitCost);
        var newQuantity = checked(state.Quantity + quantity);
        var newValue = checked(state.CarryingValue + quantity * unitCost);
        return state with { Quantity = newQuantity, CarryingValue = newValue,
            AverageUnitCost = newQuantity == 0m ? 0m : newValue / newQuantity };
    }

    public static (ValuationState State, CostConsumption Consumption) IssueMovingAverage(ValuationState state, decimal quantity)
    {
        ValidateIssue(state.Quantity, quantity);
        var value = decimal.Round(quantity * state.AverageUnitCost, 6, MidpointRounding.ToEven);
        var remainingQuantity = state.Quantity - quantity;
        var remainingValue = state.CarryingValue - value;
        if (remainingQuantity == 0m) remainingValue = 0m;
        var updated = state with { Quantity = remainingQuantity, CarryingValue = remainingValue,
            AverageUnitCost = remainingQuantity == 0m ? 0m : remainingValue / remainingQuantity };
        return (updated, new CostConsumption(quantity, value, state.Layers));
    }

    public static ValuationState ReceiveFifo(ValuationState state, Guid layerId, decimal quantity, decimal unitCost, DateOnly date)
    {
        ValidateReceipt(quantity, unitCost);
        var layers = state.Layers.Append(new CostLayer(layerId, quantity, unitCost, date))
            .OrderBy(layer => layer.EffectiveDate).ThenBy(layer => layer.Id).ToArray();
        var totalQuantity = checked(state.Quantity + quantity);
        var totalValue = checked(state.CarryingValue + quantity * unitCost);
        return state with { Quantity = totalQuantity, CarryingValue = totalValue,
            AverageUnitCost = totalQuantity == 0m ? 0m : totalValue / totalQuantity, Layers = layers };
    }

    public static (ValuationState State, CostConsumption Consumption) IssueFifo(ValuationState state, decimal quantity)
    {
        ValidateIssue(state.Quantity, quantity);
        var remaining = quantity;
        var consumedValue = 0m;
        var layers = new List<CostLayer>(state.Layers.Count);
        foreach (var layer in state.Layers.OrderBy(item => item.EffectiveDate).ThenBy(item => item.Id))
        {
            var take = Math.Min(remaining, layer.RemainingQuantity);
            consumedValue = checked(consumedValue + take * layer.UnitCost);
            remaining -= take;
            if (layer.RemainingQuantity > take) layers.Add(layer with { RemainingQuantity = layer.RemainingQuantity - take });
        }
        if (remaining != 0m) throw new InventoryException("inventory.cost_layers_inconsistent");
        var newQuantity = state.Quantity - quantity;
        var newValue = state.CarryingValue - consumedValue;
        if (newQuantity == 0m) newValue = 0m;
        var updated = state with { Quantity = newQuantity, CarryingValue = newValue,
            AverageUnitCost = newQuantity == 0m ? 0m : newValue / newQuantity, Layers = layers };
        return (updated, new CostConsumption(quantity, consumedValue, layers));
    }

    private static void ValidateReceipt(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0m) throw new InventoryException("inventory.quantity_invalid");
        if (unitCost < 0m) throw new InventoryException("inventory.unit_cost_invalid");
    }

    private static void ValidateIssue(decimal onHand, decimal quantity)
    {
        if (quantity <= 0m) throw new InventoryException("inventory.quantity_invalid");
        if (quantity > onHand) throw new InventoryException("inventory.insufficient_stock");
    }
}

public static class InventoryInvariants
{
    public static IReadOnlyList<TransferLeg> CreateTransfer(StockPosition source, StockPosition destination, decimal quantity, bool inTransit)
    {
        if (quantity <= 0m) throw new InventoryException("inventory.quantity_invalid");
        if (source.TenantId != destination.TenantId || source.LegalEntityId != destination.LegalEntityId)
            throw new InventoryException("inventory.cross_entity_transfer_not_supported");
        if (source.ProductVariantId != destination.ProductVariantId || source.LotId != destination.LotId || source.SerialId != destination.SerialId)
            throw new InventoryException("inventory.transfer_position_mismatch");
        var legs = inTransit
            ? new[] { new TransferLeg(source, -quantity, "Ship"), new TransferLeg(destination with { Status = StockStatus.InTransit }, quantity, "Ship") }
            : new[] { new TransferLeg(source, -quantity, "Complete"), new TransferLeg(destination, quantity, "Complete") };
        if (legs.Sum(leg => leg.SignedBaseQuantity) != 0m) throw new InventoryException("inventory.transfer_not_conserved");
        return legs;
    }

    public static decimal Available(decimal onHand, decimal reserved, StockStatus status) =>
        status == StockStatus.Available ? Math.Max(0m, onHand - reserved) : 0m;

    public static void EnsureReservationAllowed(ReservationMode mode, decimal onHand, decimal alreadyReserved, decimal requested, bool overrideAuthorized = false)
    {
        if (mode == ReservationMode.Disabled) throw new InventoryException("inventory.reservations_disabled");
        if (requested <= 0m) throw new InventoryException("inventory.quantity_invalid");
        if (mode == ReservationMode.Enforced && requested > Math.Max(0m, onHand - alreadyReserved) && !overrideAuthorized)
            throw new InventoryException("inventory.insufficient_available_stock");
    }

    public static void EnsureOperationalDateOpen(DateOnly operationalDate, DateOnly? earliestAllowedDate)
    {
        if (earliestAllowedDate is { } cutoff && operationalDate < cutoff)
            throw new InventoryException("inventory.period_locked");
    }

    public static void EnsureLocationMoveIsAcyclic(Guid movedLocation, Guid? newParent, IReadOnlyDictionary<Guid, Guid?> parentByLocation)
    {
        var visited = new HashSet<Guid>();
        var current = newParent;
        while (current.HasValue)
        {
            if (current.Value == movedLocation || !visited.Add(current.Value))
                throw new InventoryException("inventory.location_cycle");
            current = parentByLocation.GetValueOrDefault(current.Value);
        }
    }

    public static IReadOnlyDictionary<StockPosition, decimal> RebuildBalances(IEnumerable<MovementProjection> history)
    {
        var balances = new Dictionary<StockPosition, decimal>();
        foreach (var movement in history.OrderBy(row => row.EffectiveAt).ThenBy(row => row.Sequence).ThenBy(row => row.Id))
            balances[movement.Position] = checked(balances.GetValueOrDefault(movement.Position) + movement.SignedBaseQuantity);
        return balances;
    }
}

public sealed class InventoryException(string code) : Exception(code)
{
    public string Code => Message;
}
