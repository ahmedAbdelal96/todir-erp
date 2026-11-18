export type InventoryState = "NonStocked" | "StockTracked";
export type TrackingMode = "Quantity" | "Lot" | "Serial";
export type ValuationMode = "NonValuated" | "Perpetual";
export type CostingMethod = "MovingAverage" | "FIFO";
export type NegativeStockPolicy = "Disallow" | "AllowWithWarningAndPermission" | "Allow";
export type LocationTrackingMode = "WarehouseOnly" | "Locations" | "HierarchicalLocations";
export type WarehouseStatus = "Active" | "Suspended" | "Archived";
export type StockStatus =
  | "Available"
  | "QualityHold"
  | "Quarantine"
  | "Damaged"
  | "ReturnsInspection"
  | "Blocked"
  | "ScrapPending"
  | "InTransit";
export type MovementKind =
  | "Receipt"
  | "Issue"
  | "Transfer"
  | "Return"
  | "Adjustment"
  | "Opening"
  | "Reversal"
  | "StatusChange";
export type OperationStatus = "Draft" | "Confirmed" | "Reversed";
export type ReservationMode = "Disabled" | "Advisory" | "Enforced";
export type ReservationStatus = "Active" | "Released" | "Consumed" | "Expired" | "Cancelled";
export type TransferMode = "Instant" | "InTransit";
export type PhysicalCountMode = "Simple" | "Full" | "Cycle" | "Blind";
export type AdjustmentDirection = "Increase" | "Decrease";
export type AdjustmentReason =
  | "CountDifference"
  | "Damage"
  | "Loss"
  | "FoundStock"
  | "DataCorrection"
  | "Scrap"
  | "UomCorrection"
  | "OtherApproved";
export type AdjustmentApprovalMode = "DirectConfirm" | "ApprovalRequired";
export type AdjustmentRequestStatus =
  | "Draft"
  | "PendingApproval"
  | "Approved"
  | "Posted"
  | "Rejected"
  | "Cancelled";

export interface Warehouse {
  readonly id: string;
  readonly tenantId: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly locationMode: LocationTrackingMode;
  readonly defaultLocationId: string;
  readonly status: WarehouseStatus;
  readonly branchId?: string | null;
}

export interface InventoryLocation {
  readonly id: string;
  readonly tenantId: string;
  readonly legalEntityId: string;
  readonly warehouseId: string;
  readonly parentLocationId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly locationType?: string;
  readonly barcode?: string | null;
  readonly hidden: boolean;
  readonly canHoldStock: boolean;
  readonly canReceiveInto: boolean;
  readonly canPickFrom: boolean;
  readonly active: boolean;
}

export interface InventoryProfile {
  readonly tenantId: string;
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly state: InventoryState;
  readonly tracking: TrackingMode;
  readonly valuation: ValuationMode;
  readonly costing?: CostingMethod | null;
  readonly negativeStock: NegativeStockPolicy;
  readonly defaultWarehouseId?: string | null;
  readonly reservationMode: ReservationMode;
  readonly version: number;
}

export interface StockBalance {
  readonly productVariantId: string;
  readonly productDisplayName?: string | null;
  readonly productSku?: string | null;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly onHandBaseQuantity: number;
  readonly reservedBaseQuantity: number;
  readonly availableBaseQuantity: number;
  readonly version: number;
  readonly lastMovementSequence: number;
}

export interface InventoryMovement {
  readonly id: string;
  readonly operationId: string;
  readonly sequence: number;
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly enteredQuantity: number;
  readonly enteredUomId: string;
  readonly conversionFactorSnapshot: number;
  readonly baseQuantity: number;
  readonly signedBaseQuantity: number;
  readonly movementType: string;
  readonly sourceModule: string;
  readonly sourceDocumentId?: string | null;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly effectiveAt: string;
}

export interface InventoryMovementPage {
  readonly items: readonly InventoryMovement[];
  readonly hasMore: boolean;
}

export interface InventoryAsOf {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly onHandBaseQuantity: number;
}

export interface NegativeStockCondition {
  readonly id: string;
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly status: string;
  readonly shortfallBaseQuantity: number;
  readonly openedByMovementId: string;
  readonly lastSeenMovementId: string;
  readonly resolvedByMovementId?: string | null;
  readonly openedAt: string;
  readonly resolvedAt?: string | null;
}

export interface ProductLocatorItem {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly warehouseCode: string;
  readonly locationId: string;
  readonly locationPath: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly onHand: number;
  readonly reserved: number;
  readonly available: number;
}

export interface PhysicalCount {
  readonly id: string;
  readonly number: string;
  readonly status: string;
  readonly lines: number;
  readonly countedLines: number;
  readonly varianceQuantity: number;
}

export interface PhysicalCountLine {
  readonly id: string;
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly stockStatus: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly expected: number;
  readonly counted?: number | null;
  readonly difference?: number | null;
  readonly enteredBy?: string | null;
  readonly enteredAt?: string | null;
}

export interface TraceabilityRecord {
  readonly id: string;
  readonly productVariantId: string;
  readonly reference: string;
  readonly status: string;
}

export interface AdjustmentRequest {
  readonly id: string;
  readonly legalEntityId: string;
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly quantity: number;
  readonly direction: AdjustmentDirection;
  readonly reason: AdjustmentReason;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly approvalMode: AdjustmentApprovalMode;
  readonly status: AdjustmentRequestStatus;
  readonly createdBy: string;
  readonly approvedBy?: string | null;
  readonly postedOperationId?: string | null;
}

export interface InventoryPolicy {
  readonly legalEntityId: string;
  readonly adjustmentApprovalMode: AdjustmentApprovalMode;
  readonly separationOfDuties: boolean;
  readonly operationalCutoffDate?: string | null;
  readonly version: number;
}

export interface InventoryOperationResult {
  readonly operationId: string;
  readonly operationNumber: string;
  readonly status: string;
  readonly movementIds: readonly string[];
  readonly idempotentReplay: boolean;
}

export interface ReservationResult {
  readonly reservationId: string;
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId: string;
  readonly requestedQuantity: number;
  readonly reservedQuantity: number;
  readonly status: string;
  readonly idempotentReplay: boolean;
}

export interface TransferResult {
  readonly transferId: string;
  readonly status: string;
  readonly requestedQuantity: number;
  readonly shippedQuantity: number;
  readonly receivedQuantity: number;
  readonly idempotentReplay: boolean;
}

export interface InventoryValuationSnapshot {
  readonly productVariantId: string;
  readonly quantity: number;
  readonly carryingValue: number;
  readonly currencyCode: string;
  readonly costingMethod: string;
  readonly asOfDate: string;
}

export interface InventoryReconciliationSnapshot {
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly inventoryValue: number;
  readonly ledgerValue: number;
  readonly difference: number;
  readonly currencyCode: string;
  readonly status: string;
  readonly diagnostics: readonly string[];
}

export interface InventoryLocationSuggestion {
  readonly warehouseId: string;
  readonly locationId: string;
  readonly locationCode: string;
  readonly availableQuantity?: number | null;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly reason: string;
}

export interface InventoryScanResult {
  readonly kind: string;
  readonly recordId: string;
  readonly productVariantId?: string | null;
  readonly warehouseId?: string | null;
  readonly reference: string;
  readonly status: string;
}

export interface CreateWarehouseRequest {
  readonly code: string;
  readonly name: string;
  readonly locationMode: LocationTrackingMode;
  readonly branchId?: string | null;
}

export interface ChangeWarehouseStatusRequest {
  readonly status: WarehouseStatus;
}

export interface CreateLocationRequest {
  readonly warehouseId: string;
  readonly parentLocationId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly locationType: string;
  readonly barcode?: string | null;
  readonly canHoldStock: boolean;
  readonly canReceiveInto: boolean;
  readonly canPickFrom: boolean;
}

export interface MoveLocationRequest {
  readonly parentLocationId?: string | null;
}

export interface CreateInventoryProfileRequest {
  readonly productVariantId: string;
  readonly state: InventoryState;
  readonly tracking: TrackingMode;
  readonly valuation: ValuationMode;
  readonly costing?: CostingMethod | null;
  readonly negativeStock: NegativeStockPolicy;
  readonly defaultWarehouseId?: string | null;
  readonly reservationMode: ReservationMode;
}

export interface ConfirmStockMovementRequest {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly enteredUomId: string;
  readonly enteredQuantity: number;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly idempotencyKey: string;
  readonly sourceModule?: string | null;
  readonly sourceDocumentId?: string | null;
  readonly status?: StockStatus;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly unitCost?: number | null;
  readonly currencyCode?: string | null;
}

export interface ChangeStockStatusRequest {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly enteredUomId: string;
  readonly enteredQuantity: number;
  readonly fromStatus: StockStatus;
  readonly toStatus: StockStatus;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly idempotencyKey: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface AdjustStockRequest {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly enteredUomId: string;
  readonly quantity: number;
  readonly direction: AdjustmentDirection;
  readonly reason: AdjustmentReason;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly idempotencyKey: string;
  readonly status?: StockStatus;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly unitCost?: number | null;
  readonly currencyCode?: string | null;
}

export interface CreateAdjustmentRequest {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly enteredUomId: string;
  readonly quantity: number;
  readonly direction: AdjustmentDirection;
  readonly reason: AdjustmentReason;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly idempotencyKey: string;
  readonly status?: StockStatus;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
  readonly unitCost?: number | null;
  readonly currencyCode?: string | null;
}

export interface UpdateInventoryPolicyRequest {
  readonly adjustmentApprovalMode: AdjustmentApprovalMode;
  readonly separationOfDuties: boolean;
  readonly operationalCutoffDate?: string | null;
}

export interface ReverseInventoryOperationRequest {
  readonly operationId: string;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly idempotencyKey: string;
}

export interface ResolveInventoryScanRequest {
  readonly code: string;
}

export interface CreatePhysicalCountRequest {
  readonly warehouseId: string;
  readonly mode?: PhysicalCountMode | string;
}

export interface EnterCountLineRequest {
  readonly countedBaseQuantity: number;
  readonly countedUnitCost?: number | null;
}

export interface AddCountLineRequest {
  readonly productVariantId: string;
  readonly locationId: string;
  readonly stockStatus?: string;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface CreateLotRequest {
  readonly productVariantId: string;
  readonly lotNumber: string;
  readonly manufacturedOn?: string | null;
  readonly expiresOn?: string | null;
  readonly supplierReference?: string | null;
}

export interface CreateSerialRequest {
  readonly productVariantId: string;
  readonly serialNumber: string;
}

export interface CreateTransferRequest {
  readonly productVariantId: string;
  readonly sourceWarehouseId: string;
  readonly sourceLocationId?: string | null;
  readonly destinationWarehouseId: string;
  readonly destinationLocationId?: string | null;
  readonly enteredUomId: string;
  readonly enteredQuantity: number;
  readonly mode: TransferMode | string;
  readonly idempotencyKey: string;
  readonly operationalDate: string;
  readonly postingDate: string;
  readonly sourceDocumentId?: string | null;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}

export interface ReceiveTransferRequest {
  readonly quantity: number;
  readonly idempotencyKey: string;
  readonly operationalDate: string;
  readonly postingDate: string;
}

export interface ReserveStockRequest {
  readonly productVariantId: string;
  readonly warehouseId: string;
  readonly locationId?: string | null;
  readonly quantity: number;
  readonly sourceModule: string;
  readonly sourceDocumentId: string;
  readonly sourceLineId?: string | null;
  readonly idempotencyKey: string;
  readonly expiresAt?: string | null;
  readonly lotId?: string | null;
  readonly serialId?: string | null;
}
