import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useOptionalPermissions, useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { PermissionSet } from "../../../core/runtime/permissions.ts";
import { isPermissionEffective, canPerformAction } from "../../../core/runtime/effective-access.ts";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useOutletContext } from "react-router-dom";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { inventoryApi } from "../services/inventory-api.ts";
import type {
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
  MoveLocationRequest,
  ReceiveTransferRequest,
  ReserveStockRequest,
  ReverseInventoryOperationRequest,
  UpdateInventoryPolicyRequest
} from "../types.ts";

export interface InventoryScopeOptions {
  readonly legalEntityId?: string | undefined;
}

export function useInventoryScope(options?: InventoryScopeOptions) {
  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Rendered outside RuntimeProvider
  }
  const legalEntityId = options?.legalEntityId ?? contextEntityId ?? "";
  const today = new Date().toISOString().slice(0, 10);
  return {
    legalEntityId,
    today
  };
}

export function useInventoryPermissions() {
  let effectiveAccess: import("../../../core/runtime/effective-access.ts").EffectiveAccess | undefined;
  try {
    const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
    effectiveAccess = outletCtx?.effectiveAccess;
  } catch {
    // outside Router context
  }

  const permissions = useOptionalPermissions();
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  const sessionPerms = (sessionQuery.data as { permissions?: readonly string[] } | undefined)?.permissions;
  const permSet = permissions ?? (sessionPerms ? new PermissionSet(sessionPerms) : undefined);

  if (effectiveAccess) {
    return {
      canReadStock: isPermissionEffective(effectiveAccess, "inventory.stock.read", true),
      canReadHistory: isPermissionEffective(effectiveAccess, "inventory.history.read", true),
      canReadCost: isPermissionEffective(effectiveAccess, "inventory.cost.read", true),
      canReadValuation: isPermissionEffective(effectiveAccess, "inventory.valuation.read", true),
      canReadReconciliation: isPermissionEffective(effectiveAccess, "inventory.reconciliation.read", true),
      canConfirmReceipt: canPerformAction(effectiveAccess, "inventory.receipt.confirm", "inventory"),
      canConfirmIssue: canPerformAction(effectiveAccess, "inventory.issue.confirm", "inventory"),
      canCreateTransfer: canPerformAction(effectiveAccess, "inventory.transfer.create", "inventory"),
      canShipTransfer: canPerformAction(effectiveAccess, "inventory.transfer.ship", "inventory"),
      canReceiveTransfer: canPerformAction(effectiveAccess, "inventory.transfer.receive", "inventory"),
      canCreateReservation: canPerformAction(effectiveAccess, "inventory.reservation.create", "inventory"),
      canReleaseReservation: canPerformAction(effectiveAccess, "inventory.reservation.release", "inventory"),
      canCreateCount: canPerformAction(effectiveAccess, "inventory.count.create", "inventory"),
      canEnterCount: canPerformAction(effectiveAccess, "inventory.count.enter", "inventory"),
      canPostCount: canPerformAction(effectiveAccess, "inventory.count.post", "inventory"),
      canCreateAdjustment: canPerformAction(effectiveAccess, "inventory.adjustment.create", "inventory"),
      canApproveAdjustment: canPerformAction(effectiveAccess, "inventory.adjustment.approve", "inventory"),
      canPostAdjustment: canPerformAction(effectiveAccess, "inventory.adjustment.post", "inventory"),
      canManageWarehouses: canPerformAction(effectiveAccess, "inventory.warehouse.manage", "inventory"),
      canManageLocations: canPerformAction(effectiveAccess, "inventory.location.manage", "inventory"),
      canManageProfiles: canPerformAction(effectiveAccess, "inventory.profile.manage", "inventory"),
      canManagePolicy: canPerformAction(effectiveAccess, "inventory.policy.manage", "inventory"),
      canManageTraceability: canPerformAction(effectiveAccess, "inventory.traceability.manage", "inventory"),
      canPostOpening: canPerformAction(effectiveAccess, "inventory.opening.post", "inventory"),
      canChangeStockStatus: canPerformAction(effectiveAccess, "inventory.stock.status.change", "inventory"),
      canReverseOperation: canPerformAction(effectiveAccess, "inventory.operation.reverse", "inventory"),
      canOverrideNegative: canPerformAction(effectiveAccess, "inventory.stock.negative.override", "inventory")
    };
  }

  return {
    canReadStock: permSet ? permSet.hasPermission("inventory.stock.read") : true,
    canReadHistory: permSet ? permSet.hasPermission("inventory.history.read") : true,
    canReadCost: permSet ? permSet.hasPermission("inventory.cost.read") : true,
    canReadValuation: permSet ? permSet.hasPermission("inventory.valuation.read") : true,
    canReadReconciliation: permSet ? permSet.hasPermission("inventory.reconciliation.read") : true,
    canConfirmReceipt: permSet ? permSet.hasPermission("inventory.receipt.confirm") : true,
    canConfirmIssue: permSet ? permSet.hasPermission("inventory.issue.confirm") : true,
    canCreateTransfer: permSet ? permSet.hasPermission("inventory.transfer.create") : true,
    canShipTransfer: permSet ? permSet.hasPermission("inventory.transfer.ship") : true,
    canReceiveTransfer: permSet ? permSet.hasPermission("inventory.transfer.receive") : true,
    canCreateReservation: permSet ? permSet.hasPermission("inventory.reservation.create") : true,
    canReleaseReservation: permSet ? permSet.hasPermission("inventory.reservation.release") : true,
    canCreateCount: permSet ? permSet.hasPermission("inventory.count.create") : true,
    canEnterCount: permSet ? permSet.hasPermission("inventory.count.enter") : true,
    canPostCount: permSet ? permSet.hasPermission("inventory.count.post") : true,
    canCreateAdjustment: permSet ? permSet.hasPermission("inventory.adjustment.create") : true,
    canApproveAdjustment: permSet ? permSet.hasPermission("inventory.adjustment.approve") : true,
    canPostAdjustment: permSet ? permSet.hasPermission("inventory.adjustment.post") : true,
    canManageWarehouses: permSet ? permSet.hasPermission("inventory.warehouse.manage") : true,
    canManageLocations: permSet ? permSet.hasPermission("inventory.location.manage") : true,
    canManageProfiles: permSet ? permSet.hasPermission("inventory.profile.manage") : true,
    canManagePolicy: permSet ? permSet.hasPermission("inventory.policy.manage") : true,
    canManageTraceability: permSet ? permSet.hasPermission("inventory.traceability.manage") : true,
    canPostOpening: permSet ? permSet.hasPermission("inventory.opening.post") : true,
    canChangeStockStatus: permSet ? permSet.hasPermission("inventory.stock.status.change") : true,
    canReverseOperation: permSet ? permSet.hasPermission("inventory.operation.reverse") : true,
    canOverrideNegative: permSet ? permSet.hasPermission("inventory.stock.negative.override") : false
  };
}

export function useWarehouses(legalEntityId: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "warehouses"],
    queryFn: () => inventoryApi.listWarehouses(legalEntityId),
    staleTime: 30_000
  });
}

export function useLocations(legalEntityId: string, warehouseId?: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "warehouses", warehouseId, "locations"],
    queryFn: () => warehouseId ? inventoryApi.listLocations(legalEntityId, warehouseId) : Promise.resolve([]),
    enabled: Boolean(warehouseId),
    staleTime: 30_000
  });
}

export function useInventoryPolicy(legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "policy"],
    queryFn: () => inventoryApi.getPolicy(legalEntityId),
    enabled: enabled && Boolean(legalEntityId),
    staleTime: 60_000
  });
}

export function useNegativeStockConditions(legalEntityId: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "negative-stock-conditions"],
    queryFn: () => inventoryApi.getNegativeStockConditions(legalEntityId),
    staleTime: 30_000
  });
}

export function useStockBalances(
  legalEntityId: string,
  params?: { productVariantId?: string; warehouseId?: string; limit?: number },
  enabled = true
) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "balances", params?.productVariantId, params?.warehouseId, params?.limit],
    queryFn: () => inventoryApi.getBalances(legalEntityId, params),
    enabled,
    staleTime: 10_000
  });
}

export function useMovementHistory(
  legalEntityId: string,
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
) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "history", params],
    queryFn: () => inventoryApi.getHistory(legalEntityId, params),
    staleTime: 10_000
  });
}

export function useStockAsOf(
  legalEntityId: string,
  asOf: string,
  params?: { productVariantId?: string; warehouseId?: string; limit?: number },
  enabled = true
) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "stock-as-of", asOf, params?.productVariantId, params?.warehouseId],
    queryFn: () => inventoryApi.getStockAsOf(legalEntityId, asOf, params),
    enabled: Boolean(asOf) && enabled,
    staleTime: 60_000
  });
}

export function useProductLocator(legalEntityId: string, productVariantId?: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "locator", productVariantId],
    queryFn: () => productVariantId ? inventoryApi.locateProduct(legalEntityId, productVariantId) : Promise.resolve([]),
    enabled: Boolean(productVariantId),
    staleTime: 10_000
  });
}

export function usePutawaySuggestion(legalEntityId: string, productVariantId?: string, warehouseId?: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "putaway", productVariantId, warehouseId],
    queryFn: () => productVariantId ? inventoryApi.suggestPutaway(legalEntityId, productVariantId, warehouseId) : Promise.resolve(null),
    enabled: Boolean(productVariantId),
    staleTime: 15_000
  });
}

export function usePickingSuggestions(legalEntityId: string, productVariantId?: string, warehouseId?: string) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "picking", productVariantId, warehouseId],
    queryFn: () => productVariantId ? inventoryApi.suggestPicking(legalEntityId, productVariantId, warehouseId) : Promise.resolve([]),
    enabled: Boolean(productVariantId),
    staleTime: 15_000
  });
}

export function usePhysicalCountLines(legalEntityId: string, countId?: string, params?: { page?: number; pageSize?: number; search?: string }) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "counts", countId, "lines", params],
    queryFn: () => countId ? inventoryApi.getPhysicalCountLines(legalEntityId, countId, params) : Promise.resolve({ items: [], totalCount: 0, page: params?.page ?? 1, pageSize: params?.pageSize ?? 50 }),
    enabled: Boolean(countId),
    staleTime: 10_000
  });
}

export function usePhysicalCounts(legalEntityId: string, params?: { warehouseId?: string; status?: string; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "counts", "list", params],
    queryFn: () => inventoryApi.listPhysicalCounts(legalEntityId, params),
    staleTime: 10_000
  });
}

export function useValuation(legalEntityId: string, productVariantId?: string, asOfDate?: string, canReadCost = true) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "valuation", productVariantId, asOfDate],
    queryFn: () => (productVariantId && asOfDate) ? inventoryApi.getValuation(legalEntityId, productVariantId, asOfDate) : Promise.resolve(null),
    enabled: Boolean(productVariantId && asOfDate && canReadCost),
    staleTime: 60_000
  });
}

export function useReconciliation(legalEntityId: string, asOfDate?: string, canReadCost = true) {
  return useQuery({
    queryKey: ["inventory", legalEntityId, "reconciliation", asOfDate],
    queryFn: () => asOfDate ? inventoryApi.getReconciliation(legalEntityId, asOfDate) : Promise.resolve(null),
    enabled: Boolean(asOfDate && canReadCost),
    staleTime: 60_000
  });
}

// Mutations
export function useInventoryMutations(legalEntityId: string) {
  const queryClient = useQueryClient();

  const invalidateStock = () => {
    queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "balances"] });
    queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "history"] });
    queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "locator"] });
    queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "negative-stock-conditions"] });
  };

  const receiveStock = useMutation({
    mutationFn: (request: ConfirmStockMovementRequest) => inventoryApi.receiveStock(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const issueStock = useMutation({
    mutationFn: (request: ConfirmStockMovementRequest) => inventoryApi.issueStock(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const postOpeningStock = useMutation({
    mutationFn: (request: ConfirmStockMovementRequest) => inventoryApi.postOpeningStock(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const changeStockStatus = useMutation({
    mutationFn: (request: ChangeStockStatusRequest) => inventoryApi.changeStockStatus(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const adjustStock = useMutation({
    mutationFn: (request: AdjustStockRequest) => inventoryApi.adjustStock(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const createAdjustmentRequest = useMutation({
    mutationFn: (request: CreateAdjustmentRequest) => inventoryApi.createAdjustmentRequest(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "adjustments"] });
    }
  });

  const approveAdjustmentRequest = useMutation({
    mutationFn: (requestId: string) => inventoryApi.approveAdjustmentRequest(legalEntityId, requestId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "adjustments"] });
    }
  });

  const postAdjustmentRequest = useMutation({
    mutationFn: (requestId: string) => inventoryApi.postAdjustmentRequest(legalEntityId, requestId),
    onSuccess: () => {
      invalidateStock();
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "adjustments"] });
    }
  });

  const reverseOperation = useMutation({
    mutationFn: (request: ReverseInventoryOperationRequest) => inventoryApi.reverseOperation(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const createTransfer = useMutation({
    mutationFn: (request: CreateTransferRequest) => inventoryApi.createTransfer(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const shipTransfer = useMutation({
    mutationFn: (transferId: string) => inventoryApi.shipTransfer(legalEntityId, transferId),
    onSuccess: invalidateStock
  });

  const receiveTransfer = useMutation({
    mutationFn: ({ transferId, request }: { transferId: string; request: ReceiveTransferRequest }) =>
      inventoryApi.receiveTransfer(legalEntityId, transferId, request),
    onSuccess: invalidateStock
  });

  const reserveStock = useMutation({
    mutationFn: (request: ReserveStockRequest) => inventoryApi.reserveStock(legalEntityId, request),
    onSuccess: invalidateStock
  });

  const releaseReservation = useMutation({
    mutationFn: (reservationId: string) => inventoryApi.releaseReservation(legalEntityId, reservationId),
    onSuccess: invalidateStock
  });

  const cancelReservation = useMutation({
    mutationFn: (reservationId: string) => inventoryApi.cancelReservation(legalEntityId, reservationId),
    onSuccess: invalidateStock
  });

  const consumeReservation = useMutation({
    mutationFn: ({ reservationId, request }: { reservationId: string; request: ConfirmStockMovementRequest }) =>
      inventoryApi.consumeReservation(legalEntityId, reservationId, request),
    onSuccess: invalidateStock
  });

  const createWarehouse = useMutation({
    mutationFn: (request: CreateWarehouseRequest) => inventoryApi.createWarehouse(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "warehouses"] });
    }
  });

  const changeWarehouseStatus = useMutation({
    mutationFn: ({ warehouseId, request }: { warehouseId: string; request: ChangeWarehouseStatusRequest }) =>
      inventoryApi.changeWarehouseStatus(legalEntityId, warehouseId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "warehouses"] });
    }
  });

  const createLocation = useMutation({
    mutationFn: (request: CreateLocationRequest) => inventoryApi.createLocation(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "warehouses"] });
    }
  });

  const moveLocation = useMutation({
    mutationFn: ({ locationId, request }: { locationId: string; request: MoveLocationRequest }) =>
      inventoryApi.moveLocation(legalEntityId, locationId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "warehouses"] });
    }
  });

  const archiveLocation = useMutation({
    mutationFn: (locationId: string) => inventoryApi.archiveLocation(legalEntityId, locationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "warehouses"] });
    }
  });

  const saveProfile = useMutation({
    mutationFn: ({ productVariantId, request }: { productVariantId: string; request: CreateInventoryProfileRequest }) =>
      inventoryApi.saveProfile(legalEntityId, productVariantId, request),
    onSuccess: invalidateStock
  });

  const updatePolicy = useMutation({
    mutationFn: (request: UpdateInventoryPolicyRequest) => inventoryApi.updatePolicy(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "policy"] });
    }
  });

  const createLot = useMutation({
    mutationFn: (request: CreateLotRequest) => inventoryApi.createLot(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "lots"] });
    }
  });

  const createSerial = useMutation({
    mutationFn: (request: CreateSerialRequest) => inventoryApi.createSerial(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "serials"] });
    }
  });

  const createPhysicalCount = useMutation({
    mutationFn: (request: CreatePhysicalCountRequest) => inventoryApi.createPhysicalCount(legalEntityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "counts"] });
    }
  });

  const enterPhysicalCountLine = useMutation({
    mutationFn: ({ countId, lineId, request }: { countId: string; lineId: string; request: EnterCountLineRequest }) =>
      inventoryApi.enterPhysicalCountLine(legalEntityId, countId, lineId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "counts", variables.countId, "lines"] });
    }
  });

  const addPhysicalCountLine = useMutation({
    mutationFn: ({ countId, request }: { countId: string; request: AddCountLineRequest }) =>
      inventoryApi.addPhysicalCountLine(legalEntityId, countId, request),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "counts", variables.countId, "lines"] });
    }
  });

  const postPhysicalCount = useMutation({
    mutationFn: ({ countId, idempotencyKey }: { countId: string; idempotencyKey?: string }) =>
      inventoryApi.postPhysicalCount(legalEntityId, countId, idempotencyKey),
    onSuccess: () => {
      invalidateStock();
      queryClient.invalidateQueries({ queryKey: ["inventory", legalEntityId, "counts"] });
    }
  });

  return {
    receiveStock,
    issueStock,
    postOpeningStock,
    changeStockStatus,
    adjustStock,
    createAdjustmentRequest,
    approveAdjustmentRequest,
    postAdjustmentRequest,
    reverseOperation,
    createTransfer,
    shipTransfer,
    receiveTransfer,
    reserveStock,
    releaseReservation,
    cancelReservation,
    consumeReservation,
    createWarehouse,
    changeWarehouseStatus,
    createLocation,
    moveLocation,
    archiveLocation,
    saveProfile,
    updatePolicy,
    createLot,
    createSerial,
    createPhysicalCount,
    enterPhysicalCountLine,
    addPhysicalCountLine,
    postPhysicalCount
  };
}
