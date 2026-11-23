using Modules.Inventory.Entities;

namespace Modules.Inventory.DTOs;

public sealed record CreateWarehouseRequest(string Code, string Name, LocationTrackingMode LocationMode, Guid? BranchId);
public sealed record ChangeWarehouseStatusRequest(WarehouseStatus Status);
public sealed record CreateLocationRequest(Guid WarehouseId, Guid? ParentLocationId, string Code, string Name,
    string LocationType, string? Barcode, bool CanHoldStock, bool CanReceiveInto, bool CanPickFrom);
public sealed record MoveLocationRequest(Guid? ParentLocationId);
public sealed record CreateInventoryProfileRequest(Guid ProductVariantId, InventoryState State, TrackingMode Tracking,
    ValuationMode Valuation, CostingMethod? Costing, NegativeStockPolicy NegativeStock, Guid? DefaultWarehouseId,
    ReservationMode ReservationMode);
public sealed record ConfirmStockMovementRequest(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal EnteredQuantity, DateOnly OperationalDate,
    DateOnly PostingDate, string IdempotencyKey, string? SourceModule, Guid? SourceDocumentId,
    StockStatus Status = StockStatus.Available, Guid? LotId = null, Guid? SerialId = null,
    decimal? UnitCost = null, string? CurrencyCode = null, Guid? ProjectId = null, Guid? TaskId = null, string? CostCategory = null, Guid? SupplierPartyId = null);
public sealed record ChangeStockStatusRequest(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal EnteredQuantity, StockStatus FromStatus, StockStatus ToStatus,
    DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey, Guid? LotId = null, Guid? SerialId = null);
public sealed record AdjustStockRequest(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal Quantity, AdjustmentDirection Direction, AdjustmentReason Reason,
    DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey, StockStatus Status = StockStatus.Available,
    Guid? LotId = null, Guid? SerialId = null, decimal? UnitCost = null, string? CurrencyCode = null);
public sealed record UpdateInventoryPolicyRequest(AdjustmentApprovalMode AdjustmentApprovalMode = AdjustmentApprovalMode.DirectConfirm,
    bool SeparationOfDuties = true, DateOnly? OperationalCutoffDate = null);
public sealed record InventoryPolicyView(Guid LegalEntityId, AdjustmentApprovalMode AdjustmentApprovalMode,
    bool SeparationOfDuties, DateOnly? OperationalCutoffDate, long Version);
public sealed record CreateAdjustmentRequest(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId,
    Guid EnteredUomId, decimal Quantity, AdjustmentDirection Direction, AdjustmentReason Reason,
    DateOnly OperationalDate, DateOnly PostingDate, string IdempotencyKey, StockStatus Status = StockStatus.Available,
    Guid? LotId = null, Guid? SerialId = null, decimal? UnitCost = null, string? CurrencyCode = null);
public sealed record AdjustmentRequestView(Guid Id, Guid LegalEntityId, Guid ProductVariantId, Guid WarehouseId,
    Guid? LocationId, decimal Quantity, AdjustmentDirection Direction, AdjustmentReason Reason,
    DateOnly OperationalDate, DateOnly PostingDate, AdjustmentApprovalMode ApprovalMode,
    AdjustmentRequestStatus Status, Guid CreatedBy, Guid? ApprovedBy, Guid? PostedOperationId);
public sealed record ReverseInventoryOperationRequest(Guid OperationId, DateOnly OperationalDate, DateOnly PostingDate,
    string IdempotencyKey);
public sealed record ResolveInventoryScanRequest(string Code);
public sealed record InventoryOperationResult(Guid OperationId, string OperationNumber, string Status,
    IReadOnlyList<Guid> MovementIds, bool IdempotentReplay);
public sealed record InventoryBalanceView(Guid ProductVariantId, Guid WarehouseId, Guid LocationId,
    string StockStatus, Guid? LotId, Guid? SerialId, decimal OnHandBaseQuantity,
    decimal ReservedBaseQuantity, decimal AvailableBaseQuantity, long Version, long LastMovementSequence,
    string? ProductDisplayName = null, string? ProductSku = null);
public sealed record InventoryMovementView(Guid Id, Guid OperationId, long Sequence, Guid ProductVariantId, Guid WarehouseId,
    Guid LocationId, string StockStatus, Guid? LotId, Guid? SerialId, decimal EnteredQuantity, Guid EnteredUomId,
    decimal ConversionFactorSnapshot, decimal BaseQuantity, decimal SignedBaseQuantity, string MovementType,
    string SourceModule, Guid? SourceDocumentId, DateOnly OperationalDate, DateOnly PostingDate, DateTimeOffset EffectiveAt);
public sealed record InventoryMovementPage(IReadOnlyList<InventoryMovementView> Items, bool HasMore);
public sealed record InventoryAsOfView(Guid ProductVariantId, Guid WarehouseId, Guid LocationId, string StockStatus,
    Guid? LotId, Guid? SerialId, decimal OnHandBaseQuantity);
public sealed record NegativeStockConditionView(Guid Id, Guid ProductVariantId, Guid WarehouseId, Guid LocationId,
    string StockStatus, Guid? LotId, Guid? SerialId, string Status, decimal ShortfallBaseQuantity,
    Guid OpenedByMovementId, Guid LastSeenMovementId, Guid? ResolvedByMovementId, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt);
public sealed record ProductLocatorView(Guid ProductVariantId, Guid WarehouseId, string WarehouseCode, Guid LocationId,
    string LocationPath, string StockStatus, Guid? LotId, Guid? SerialId, decimal OnHand, decimal Reserved, decimal Available);
public sealed record CreatePhysicalCountRequest(Guid WarehouseId, string Mode = "Full");
public sealed record EnterCountLineRequest(decimal CountedBaseQuantity, decimal? CountedUnitCost = null);
public sealed record AddCountLineRequest(Guid ProductVariantId, Guid LocationId, string StockStatus = "Available", Guid? LotId = null, Guid? SerialId = null);
public sealed record PhysicalCountLineView(Guid Id, Guid ProductVariantId, Guid WarehouseId, Guid LocationId, string StockStatus,
    Guid? LotId, Guid? SerialId, decimal Expected, decimal? Counted, decimal? Difference, Guid? EnteredBy, DateTimeOffset? EnteredAt,
    string? ProductDisplayName = null, string? ProductSku = null, string? BaseUomCode = null);
public sealed record PhysicalCountLinesPage(IReadOnlyList<PhysicalCountLineView> Items, int TotalCount, int Page, int PageSize);
public sealed record PhysicalCountView(Guid Id, string Number, string Status, int Lines, int CountedLines, decimal VarianceQuantity);
public sealed record PhysicalCountListView(Guid Id, string Number, Guid WarehouseId, string WarehouseName, string Mode,
    string Status, int Lines, int CountedLines, decimal VarianceQuantity);
public sealed record PhysicalCountsPage(IReadOnlyList<PhysicalCountListView> Items, int TotalCount, int Page, int PageSize);
public sealed record CreateLotRequest(Guid ProductVariantId, string LotNumber, DateOnly? ManufacturedOn, DateOnly? ExpiresOn, string? SupplierReference);
public sealed record CreateSerialRequest(Guid ProductVariantId, string SerialNumber);
public sealed record TraceabilityRecord(Guid Id, Guid ProductVariantId, string Reference, string Status);
public sealed record CreateTransferRequest(Guid ProductVariantId, Guid SourceWarehouseId, Guid? SourceLocationId,
    Guid DestinationWarehouseId, Guid? DestinationLocationId, Guid EnteredUomId, decimal EnteredQuantity,
    string Mode, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate,
    Guid? SourceDocumentId = null, Guid? LotId = null, Guid? SerialId = null);
public sealed record ReceiveTransferRequest(decimal Quantity, string IdempotencyKey, DateOnly OperationalDate, DateOnly PostingDate);
public sealed record ReserveStockRequest(Guid ProductVariantId, Guid WarehouseId, Guid? LocationId, decimal Quantity,
    string SourceModule, Guid SourceDocumentId, Guid? SourceLineId, string IdempotencyKey, DateTimeOffset? ExpiresAt = null,
    Guid? LotId = null, Guid? SerialId = null);
public sealed record ReservationResult(Guid ReservationId, Guid ProductVariantId, Guid WarehouseId, Guid LocationId,
    decimal RequestedQuantity, decimal ReservedQuantity, string Status, bool IdempotentReplay);
public sealed record TransferResult(Guid TransferId, string Status, decimal RequestedQuantity,
    decimal ShippedQuantity, decimal ReceivedQuantity, bool IdempotentReplay);
