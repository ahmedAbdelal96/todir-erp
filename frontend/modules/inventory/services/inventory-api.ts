import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AdjustmentRequest,
  AdjustStockRequest,
  ChangeStockStatusRequest,
  ChangeWarehouseStatusRequest,
  ConfirmStockMovementRequest,
  CreateAdjustmentRequest,
  CreateInventoryProfileRequest,
  CreateLocationRequest,
  CreateLotRequest,
  CreatePhysicalCountRequest,
  CreateSerialRequest,
  CreateTransferRequest,
  CreateWarehouseRequest,
  EnterCountLineRequest,
  AddCountLineRequest,
  InventoryAsOf,
  InventoryLocation,
  InventoryLocationSuggestion,
  InventoryMovementPage,
  InventoryOperationResult,
  InventoryPolicy,
  InventoryProfile,
  InventoryReconciliationSnapshot,
  InventoryScanResult,
  InventoryValuationSnapshot,
  MoveLocationRequest,
  NegativeStockCondition,
  PhysicalCount,
  PhysicalCountsPage,
  PhysicalCountLine,
  PhysicalCountLinesPage,
  ProductLocatorItem,
  ReceiveTransferRequest,
  ReservationResult,
  ReserveStockRequest,
  ResolveInventoryScanRequest,
  ReverseInventoryOperationRequest,
  StockBalance,
  TraceabilityRecord,
  TransferResult,
  UpdateInventoryPolicyRequest,
  Warehouse
} from "../types.ts";

const BASE = "/api/v1/inventory";

function entityPath(entityId: string, path: string): string {
  return `${BASE}/legal-entities/${encodeURIComponent(entityId)}${path}`;
}

export const inventoryApi = {
  // Warehouses & Locations
  listWarehouses: (entityId: string): Promise<readonly Warehouse[]> =>
    httpClient<readonly Warehouse[]>(entityPath(entityId, "/warehouses")),

  createWarehouse: (entityId: string, request: CreateWarehouseRequest): Promise<Warehouse> =>
    httpClient<Warehouse>(entityPath(entityId, "/warehouses"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  changeWarehouseStatus: (entityId: string, warehouseId: string, request: ChangeWarehouseStatusRequest): Promise<Warehouse> =>
    httpClient<Warehouse>(entityPath(entityId, `/warehouses/${encodeURIComponent(warehouseId)}/status`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  listLocations: (entityId: string, warehouseId: string): Promise<readonly InventoryLocation[]> =>
    httpClient<readonly InventoryLocation[]>(entityPath(entityId, `/warehouses/${encodeURIComponent(warehouseId)}/locations`)),

  createLocation: (entityId: string, request: CreateLocationRequest): Promise<InventoryLocation> =>
    httpClient<InventoryLocation>(entityPath(entityId, "/locations"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  moveLocation: (entityId: string, locationId: string, request: MoveLocationRequest): Promise<InventoryLocation> =>
    httpClient<InventoryLocation>(entityPath(entityId, `/locations/${encodeURIComponent(locationId)}/parent`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  archiveLocation: (entityId: string, locationId: string): Promise<boolean> =>
    httpClient<boolean>(entityPath(entityId, `/locations/${encodeURIComponent(locationId)}`), {
      method: "DELETE"
    }),

  // Policy
  getPolicy: (entityId: string): Promise<InventoryPolicy> =>
    httpClient<InventoryPolicy>(entityPath(entityId, "/policy")),

  updatePolicy: (entityId: string, request: UpdateInventoryPolicyRequest): Promise<InventoryPolicy> =>
    httpClient<InventoryPolicy>(entityPath(entityId, "/policy"), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  getNegativeStockConditions: (
    entityId: string,
    params?: { status?: string; beforeOpenedAt?: string; beforeId?: string; limit?: number }
  ): Promise<readonly NegativeStockCondition[]> => {
    const q = new URLSearchParams();
    if (params?.status) q.set("status", params.status);
    if (params?.beforeOpenedAt) q.set("beforeOpenedAt", params.beforeOpenedAt);
    if (params?.beforeId) q.set("beforeId", params.beforeId);
    if (params?.limit) q.set("limit", String(params.limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<readonly NegativeStockCondition[]>(entityPath(entityId, `/negative-stock-conditions${suffix}`));
  },

  // Profiles
  saveProfile: (entityId: string, productVariantId: string, request: CreateInventoryProfileRequest): Promise<InventoryProfile> =>
    httpClient<InventoryProfile>(entityPath(entityId, `/profiles/${encodeURIComponent(productVariantId)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  // Traceability
  createLot: (entityId: string, request: CreateLotRequest): Promise<TraceabilityRecord> =>
    httpClient<TraceabilityRecord>(entityPath(entityId, "/lots"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createSerial: (entityId: string, request: CreateSerialRequest): Promise<TraceabilityRecord> =>
    httpClient<TraceabilityRecord>(entityPath(entityId, "/serials"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Balances & History
  getBalances: (
    entityId: string,
    params?: { productVariantId?: string; warehouseId?: string; limit?: number }
  ): Promise<readonly StockBalance[]> => {
    const q = new URLSearchParams();
    if (params?.productVariantId) q.set("productVariantId", params.productVariantId);
    if (params?.warehouseId) q.set("warehouseId", params.warehouseId);
    if (params?.limit) q.set("limit", String(params.limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<readonly StockBalance[]>(entityPath(entityId, `/balances${suffix}`));
  },

  getHistory: (
    entityId: string,
    params?: {
      productVariantId?: string;
      warehouseId?: string;
      locationId?: string;
      lotId?: string;
      serialId?: string;
      sourceDocumentId?: string;
      beforeEffectiveAt?: string;
      beforeSequence?: number;
      beforeId?: string;
      limit?: number;
    }
  ): Promise<InventoryMovementPage> => {
    const q = new URLSearchParams();
    if (params?.productVariantId) q.set("productVariantId", params.productVariantId);
    if (params?.warehouseId) q.set("warehouseId", params.warehouseId);
    if (params?.locationId) q.set("locationId", params.locationId);
    if (params?.lotId) q.set("lotId", params.lotId);
    if (params?.serialId) q.set("serialId", params.serialId);
    if (params?.sourceDocumentId) q.set("sourceDocumentId", params.sourceDocumentId);
    if (params?.beforeEffectiveAt) q.set("beforeEffectiveAt", params.beforeEffectiveAt);
    if (params?.beforeSequence) q.set("beforeSequence", String(params.beforeSequence));
    if (params?.beforeId) q.set("beforeId", params.beforeId);
    if (params?.limit) q.set("limit", String(params.limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<InventoryMovementPage>(entityPath(entityId, `/history${suffix}`));
  },

  getStockAsOf: (
    entityId: string,
    asOf: string,
    params?: { productVariantId?: string; warehouseId?: string; limit?: number }
  ): Promise<readonly InventoryAsOf[]> => {
    const q = new URLSearchParams({ asOf });
    if (params?.productVariantId) q.set("productVariantId", params.productVariantId);
    if (params?.warehouseId) q.set("warehouseId", params.warehouseId);
    if (params?.limit) q.set("limit", String(params.limit));
    return httpClient<readonly InventoryAsOf[]>(entityPath(entityId, `/stock-as-of?${q.toString()}`));
  },

  // Valuation & Reconciliation (Guarded by inventory.cost.read)
  getReconciliation: (entityId: string, asOfDate: string): Promise<InventoryReconciliationSnapshot> => {
    const q = new URLSearchParams({ asOfDate });
    return httpClient<InventoryReconciliationSnapshot>(entityPath(entityId, `/reconciliation?${q.toString()}`));
  },

  getValuation: (entityId: string, productVariantId: string, asOfDate: string): Promise<InventoryValuationSnapshot> => {
    const q = new URLSearchParams({ asOfDate });
    return httpClient<InventoryValuationSnapshot>(entityPath(entityId, `/products/${encodeURIComponent(productVariantId)}/valuation?${q.toString()}`));
  },

  // Suggestions & Locator
  locateProduct: (entityId: string, productVariantId: string, limit?: number): Promise<readonly ProductLocatorItem[]> => {
    const q = new URLSearchParams();
    if (limit) q.set("limit", String(limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<readonly ProductLocatorItem[]>(entityPath(entityId, `/products/${encodeURIComponent(productVariantId)}/locator${suffix}`));
  },

  suggestPutaway: (entityId: string, productVariantId: string, warehouseId?: string): Promise<InventoryLocationSuggestion | null> => {
    const q = new URLSearchParams();
    if (warehouseId) q.set("warehouseId", warehouseId);
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<InventoryLocationSuggestion | null>(entityPath(entityId, `/products/${encodeURIComponent(productVariantId)}/putaway-suggestion${suffix}`));
  },

  suggestPicking: (entityId: string, productVariantId: string, warehouseId?: string, limit?: number): Promise<readonly InventoryLocationSuggestion[]> => {
    const q = new URLSearchParams();
    if (warehouseId) q.set("warehouseId", warehouseId);
    if (limit) q.set("limit", String(limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<readonly InventoryLocationSuggestion[]>(entityPath(entityId, `/products/${encodeURIComponent(productVariantId)}/picking-suggestions${suffix}`));
  },

  resolveScan: (entityId: string, request: ResolveInventoryScanRequest): Promise<InventoryScanResult | null> =>
    httpClient<InventoryScanResult | null>(entityPath(entityId, "/scan/resolve"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Operations
  receiveStock: (entityId: string, request: ConfirmStockMovementRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/receipts/confirm"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  issueStock: (entityId: string, request: ConfirmStockMovementRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/issues/confirm"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  postOpeningStock: (entityId: string, request: ConfirmStockMovementRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/opening-stock/post"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  changeStockStatus: (entityId: string, request: ChangeStockStatusRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/stock-status/change"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  reverseOperation: (entityId: string, request: ReverseInventoryOperationRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/operations/reverse"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Adjustments & Requests
  adjustStock: (entityId: string, request: AdjustStockRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, "/adjustments"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createAdjustmentRequest: (entityId: string, request: CreateAdjustmentRequest): Promise<AdjustmentRequest> =>
    httpClient<AdjustmentRequest>(entityPath(entityId, "/adjustment-requests"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveAdjustmentRequest: (entityId: string, requestId: string): Promise<AdjustmentRequest> =>
    httpClient<AdjustmentRequest>(entityPath(entityId, `/adjustment-requests/${encodeURIComponent(requestId)}/approve`), {
      method: "POST"
    }),

  postAdjustmentRequest: (entityId: string, requestId: string): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, `/adjustment-requests/${encodeURIComponent(requestId)}/post`), {
      method: "POST"
    }),

  // Transfers
  createTransfer: (entityId: string, request: CreateTransferRequest): Promise<TransferResult> =>
    httpClient<TransferResult>(entityPath(entityId, "/transfers"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  shipTransfer: (entityId: string, transferId: string): Promise<TransferResult> =>
    httpClient<TransferResult>(entityPath(entityId, `/transfers/${encodeURIComponent(transferId)}/ship`), {
      method: "POST"
    }),

  receiveTransfer: (entityId: string, transferId: string, request: ReceiveTransferRequest): Promise<TransferResult> =>
    httpClient<TransferResult>(entityPath(entityId, `/transfers/${encodeURIComponent(transferId)}/receipts`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Reservations
  reserveStock: (entityId: string, request: ReserveStockRequest): Promise<ReservationResult> =>
    httpClient<ReservationResult>(entityPath(entityId, "/reservations"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  releaseReservation: (entityId: string, reservationId: string): Promise<boolean> =>
    httpClient<boolean>(entityPath(entityId, `/reservations/${encodeURIComponent(reservationId)}/release`), {
      method: "POST"
    }),

  cancelReservation: (entityId: string, reservationId: string): Promise<boolean> =>
    httpClient<boolean>(entityPath(entityId, `/reservations/${encodeURIComponent(reservationId)}/cancel`), {
      method: "POST"
    }),

  expireReservations: (entityId: string, limit?: number): Promise<number> => {
    const q = new URLSearchParams();
    if (limit) q.set("limit", String(limit));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<number>(entityPath(entityId, `/reservations/expire${suffix}`), {
      method: "POST"
    });
  },

  consumeReservation: (entityId: string, reservationId: string, request: ConfirmStockMovementRequest): Promise<InventoryOperationResult> =>
    httpClient<InventoryOperationResult>(entityPath(entityId, `/reservations/${encodeURIComponent(reservationId)}/consume`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Physical Counts
  createPhysicalCount: (entityId: string, request: CreatePhysicalCountRequest): Promise<PhysicalCount> =>
    httpClient<PhysicalCount>(entityPath(entityId, "/counts"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  listPhysicalCounts: (entityId: string, params?: { warehouseId?: string; status?: string; page?: number; pageSize?: number }): Promise<PhysicalCountsPage> => {
    const q = new URLSearchParams();
    if (params?.warehouseId) q.set("warehouseId", params.warehouseId);
    if (params?.status) q.set("status", params.status);
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    return httpClient<PhysicalCountsPage>(entityPath(entityId, `/counts${suffix}`));
  },

  getPhysicalCountLines: async (entityId: string, countId: string, params?: { page?: number; pageSize?: number; search?: string }): Promise<PhysicalCountLinesPage> => {
    const q = new URLSearchParams();
    if (params?.search?.trim()) q.set("search", params.search.trim());
    if (params?.page) q.set("page", String(params.page));
    if (params?.pageSize) q.set("pageSize", String(params.pageSize));
    const suffix = q.toString() ? `?${q.toString()}` : "";
    const response = await httpClient<PhysicalCountLinesPage | readonly PhysicalCountLine[]>(entityPath(entityId, `/counts/${encodeURIComponent(countId)}/lines${suffix}`));
    if (Array.isArray(response)) {
      return { items: response as readonly PhysicalCountLine[], totalCount: response.length, page: params?.page ?? 1, pageSize: params?.pageSize ?? response.length };
    }
    return response as PhysicalCountLinesPage;
  },

  addPhysicalCountLine: (entityId: string, countId: string, request: AddCountLineRequest): Promise<PhysicalCountLine> =>
    httpClient<PhysicalCountLine>(entityPath(entityId, `/counts/${encodeURIComponent(countId)}/lines`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  enterPhysicalCountLine: (entityId: string, countId: string, lineId: string, request: EnterCountLineRequest): Promise<PhysicalCount> =>
    httpClient<PhysicalCount>(entityPath(entityId, `/counts/${encodeURIComponent(countId)}/lines/${encodeURIComponent(lineId)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  postPhysicalCount: (entityId: string, countId: string, idempotencyKey?: string): Promise<PhysicalCount> =>
    httpClient<PhysicalCount>(entityPath(entityId, `/counts/${encodeURIComponent(countId)}/post`), {
      method: "POST",
      ...(idempotencyKey ? { headers: { "Idempotency-Key": idempotencyKey } } : {})
    })
};
