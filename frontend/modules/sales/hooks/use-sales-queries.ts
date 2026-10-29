import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { salesApi } from "../services/sales-api.ts";
import type {
  AcceptAndInvoiceQuotationRequest,
  AcceptQuotationRequest,
  ConvertQuotationRequest,
  ConvertQuotationToOrderRequest,
  CreateOrderRequest,
  CreateQuotationRequest,
  CreateReturnRequest,
  FulfillOrderRequest,
  OrderCommandRequest,
  ReturnCommandRequest,
  SalesCustomerProfileRequest,
  TransitionQuotationRequest,
  UpdateOrderRequest,
  UpdateQuotationRequest
} from "../types.ts";

export const salesKeys = {
  all: ["sales"] as const,
  quotations: (entityId: string) => [...salesKeys.all, "quotations", entityId] as const,
  quotationList: (entityId: string, params?: { state?: string | null; partyId?: string | null; page?: number | null; pageSize?: number | null }) =>
    [...salesKeys.quotations(entityId), params] as const,
  quotationDetail: (entityId: string, id: string) => [...salesKeys.all, "quotation", entityId, id] as const,
  orders: (entityId: string) => [...salesKeys.all, "orders", entityId] as const,
  orderList: (entityId: string, params?: { page?: number | null; pageSize?: number | null }) =>
    [...salesKeys.orders(entityId), params] as const,
  orderDetail: (entityId: string, id: string) => [...salesKeys.all, "order", entityId, id] as const,
  returnDetail: (entityId: string, id: string) => [...salesKeys.all, "return", entityId, id] as const
};

export function useSalesQuotations(
  entityId: string,
  params?: { state?: string | null; partyId?: string | null; page?: number | null; pageSize?: number | null }
) {
  return useQuery({
    queryKey: salesKeys.quotationList(entityId, params),
    queryFn: () => salesApi.listQuotations(entityId, params),
    enabled: Boolean(entityId)
  });
}

export function useSalesQuotation(entityId: string, id: string) {
  return useQuery({
    queryKey: salesKeys.quotationDetail(entityId, id),
    queryFn: () => salesApi.getQuotation(entityId, id),
    enabled: Boolean(entityId && id)
  });
}

export function useCreateQuotationMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateQuotationRequest) => salesApi.createQuotation(entityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useUpdateQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateQuotationRequest) => salesApi.updateQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useOpenQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransitionQuotationRequest) => salesApi.openQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useAcceptQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AcceptQuotationRequest) => salesApi.acceptQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useDeclineQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransitionQuotationRequest) => salesApi.declineQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useCancelQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TransitionQuotationRequest) => salesApi.cancelQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useReviseQuotationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateQuotationRequest) => salesApi.reviseQuotation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useConvertToInvoiceMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ConvertQuotationRequest) => salesApi.convertToInvoice(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useAcceptAndInvoiceMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AcceptAndInvoiceQuotationRequest) => salesApi.acceptAndInvoice(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
    }
  });
}

export function useConvertToOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ConvertQuotationToOrderRequest) => salesApi.convertToOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.quotationDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.quotations(entityId) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

// Orders hooks
export function useSalesOrders(
  entityId: string,
  params?: { page?: number | null; pageSize?: number | null }
) {
  return useQuery({
    queryKey: salesKeys.orderList(entityId, params),
    queryFn: () => salesApi.listOrders(entityId, params),
    enabled: Boolean(entityId)
  });
}

export function useSalesOrder(entityId: string, id: string) {
  return useQuery({
    queryKey: salesKeys.orderDetail(entityId, id),
    queryFn: () => salesApi.getOrder(entityId, id),
    enabled: Boolean(entityId && id)
  });
}

export function useCreateOrderMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateOrderRequest) => salesApi.createOrder(entityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useUpdateOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateOrderRequest) => salesApi.updateOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useConfirmOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OrderCommandRequest) => salesApi.confirmOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useReleaseReservationMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OrderCommandRequest) => salesApi.releaseReservation(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useCancelOrderRemainingMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OrderCommandRequest) => salesApi.cancelOrderRemaining(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useFulfillOrderMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: FulfillOrderRequest) => salesApi.fulfillOrder(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

export function useCreateOrderInvoiceMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: OrderCommandRequest) => salesApi.createOrderInvoice(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.orderDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.orders(entityId) });
    }
  });
}

// Returns
export function useCreateReturnMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateReturnRequest) => salesApi.createReturn(entityId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.all });
    }
  });
}

export function useSalesReturn(entityId: string, id: string) {
  return useQuery({
    queryKey: salesKeys.returnDetail(entityId, id),
    queryFn: () => salesApi.getReturn(entityId, id),
    enabled: Boolean(entityId && id)
  });
}

export function useApproveReturnMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReturnCommandRequest) => salesApi.approveReturn(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.returnDetail(entityId, id) });
    }
  });
}

export function useExecuteReturnMutation(entityId: string, id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ReturnCommandRequest) => salesApi.executeReturn(entityId, id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.returnDetail(entityId, id) });
      queryClient.invalidateQueries({ queryKey: salesKeys.all });
    }
  });
}

// Customer Profile
export function useSaveCustomerProfileMutation(entityId: string, partyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SalesCustomerProfileRequest) => salesApi.saveCustomerProfile(entityId, partyId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: salesKeys.all });
    }
  });
}
