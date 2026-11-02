import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { purchasingApi } from "../services/purchasing-api.ts";
import type {
  CreatePurchaseOrderRequest,
  UpdatePurchaseOrderRequest,
  PurchaseOrderCommandRequest,
  CreateSupplierInvoiceRequest,
  SupplierPurchaseProfileRequest,
  PurchaseSettingsRequest,
  ReceivePurchaseOrderRequest,
  MatchPurchaseOrderRequest,
  CreateSupplierReturnRequest,
  ApproveAndBillRequest,
  ReceiveAndBillRequest
} from "../types.ts";

export const purchasingKeys = {
  all: ["purchasing"] as const,
  orders: (entityId: string) => [...purchasingKeys.all, "orders", entityId] as const,
  orderList: (entityId: string, params?: { state?: string | null; search?: string | null; page?: number | null; pageSize?: number | null }) =>
    [...purchasingKeys.orders(entityId), params] as const,
  orderDetail: (entityId: string, id: string) => [...purchasingKeys.all, "order", entityId, id] as const,
  supplierProfile: (entityId: string, supplierId: string) => [...purchasingKeys.all, "supplier-profile", entityId, supplierId] as const,
  settings: (entityId: string) => [...purchasingKeys.all, "settings", entityId] as const,
  returns: (entityId: string) => [...purchasingKeys.all, "returns", entityId] as const,
  returnDetail: (entityId: string, id: string) => [...purchasingKeys.all, "return", entityId, id] as const
};

export function usePurchaseOrders(
  entityId: string,
  params?: { state?: string | null; search?: string | null; page?: number | null; pageSize?: number | null }
) {
  return useQuery({
    queryKey: purchasingKeys.orderList(entityId, params),
    queryFn: () => purchasingApi.listPurchaseOrders(entityId, params),
    enabled: Boolean(entityId)
  });
}

export function usePurchaseOrderDetail(entityId: string, id: string) {
  return useQuery({
    queryKey: purchasingKeys.orderDetail(entityId, id),
    queryFn: () => purchasingApi.getPurchaseOrder(entityId, id),
    enabled: Boolean(entityId && id)
  });
}

export function useCreatePurchaseOrderMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePurchaseOrderRequest) => purchasingApi.createPurchaseOrder(entityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
    }
  });
}

export function useUpdatePurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePurchaseOrderRequest) => purchasingApi.updatePurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useAmendPurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePurchaseOrderRequest) => purchasingApi.amendPurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useApprovePurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseOrderCommandRequest) => purchasingApi.approvePurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useRejectPurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseOrderCommandRequest) => purchasingApi.rejectPurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useCancelRemainingPurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseOrderCommandRequest) => purchasingApi.cancelRemainingPurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useCloseShortPurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseOrderCommandRequest) => purchasingApi.closeShortPurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useReceivePurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReceivePurchaseOrderRequest) => purchasingApi.receivePurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useCreateSupplierInvoiceMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSupplierInvoiceRequest) => purchasingApi.createSupplierInvoice(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useApproveAndBillMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ApproveAndBillRequest) => purchasingApi.approveAndBill(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useReceiveAndBillMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReceiveAndBillRequest) => purchasingApi.receiveAndBill(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useMatchPurchaseOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: MatchPurchaseOrderRequest) => purchasingApi.matchPurchaseOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orders(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, id) });
    }
  });
}

export function useSupplierProfile(entityId: string, supplierId: string) {
  return useQuery({
    queryKey: purchasingKeys.supplierProfile(entityId, supplierId),
    queryFn: () => purchasingApi.getSupplierProfile(entityId, supplierId),
    enabled: Boolean(entityId && supplierId)
  });
}

export function useSaveSupplierProfileMutation(entityId: string, supplierId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SupplierPurchaseProfileRequest) => purchasingApi.saveSupplierProfile(entityId, supplierId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.supplierProfile(entityId, supplierId) });
    }
  });
}

export function usePurchasingSettings(entityId: string) {
  return useQuery({
    queryKey: purchasingKeys.settings(entityId),
    queryFn: () => purchasingApi.getSettings(entityId),
    enabled: Boolean(entityId)
  });
}

export function useSavePurchasingSettingsMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseSettingsRequest) => purchasingApi.saveSettings(entityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.settings(entityId) });
    }
  });
}

export function useCreateSupplierReturnMutation(entityId: string, orderId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSupplierReturnRequest) => purchasingApi.createReturn(entityId, orderId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.returns(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.orderDetail(entityId, orderId) });
    }
  });
}

export function useSupplierReturnDetail(entityId: string, id: string) {
  return useQuery({
    queryKey: purchasingKeys.returnDetail(entityId, id),
    queryFn: () => purchasingApi.getReturn(entityId, id),
    enabled: Boolean(entityId && id)
  });
}

export function useApproveSupplierReturnMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: PurchaseOrderCommandRequest) => purchasingApi.approveReturn(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.returns(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.returnDetail(entityId, id) });
    }
  });
}

export function useExecuteSupplierReturnMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => purchasingApi.executeReturn(entityId, id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: purchasingKeys.returns(entityId) });
      queryClient.invalidateQueries({ queryKey: purchasingKeys.returnDetail(entityId, id) });
    }
  });
}
