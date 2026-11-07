// POS Module 20 TanStack Query Hooks & Key Factories

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { posApi } from "../services/pos-api.ts";
import type {
  CreateRegisterRequest,
  UpdateRegisterRequest,
  RegisterTerminalRequest,
  OpenSessionRequest,
  CloseSessionRequest,
  CashMovementRequest,
  CreateCartRequest,
  AddCartLineRequest,
  ScanCartLineRequest,
  UpdateCartLineRequest,
  DiscountCartRequest,
  SetCartCustomerRequest,
  CheckoutRequest,
  CheckoutForCartRequest,
  CreateReturnRequest,
  ReturnLineRequest,
  RefundReturnRequest,
  ExchangeRequest,
  CompleteExchangeRequest,
  ManagerOverrideRequest,
  OfflineEnvelopeRequest,
  OfflineConflictResolutionRequest,
  RegisterResponse,
  TerminalResponse,
  SessionResponse,
  CartResponse,
  CartDetailsResponse,
  CheckoutResponse,
  ExchangeResponse,
  CatalogSearchResult,
  PosPaymentMixReport,
  PosDiscountSummaryReport,
} from "../types.ts";

export const posQueryKeys = {
  all: ["pos"] as const,
  registers: (entityId: string) => [...posQueryKeys.all, "registers", entityId] as const,
  register: (registerId: string) => [...posQueryKeys.all, "register", registerId] as const,
  terminals: (registerId: string) => [...posQueryKeys.all, "terminals", registerId] as const,
  currentSession: (registerId: string) => [...posQueryKeys.all, "session", "current", registerId] as const,
  session: (sessionId: string) => [...posQueryKeys.all, "session", sessionId] as const,
  cart: (cartId: string) => [...posQueryKeys.all, "cart", cartId] as const,
  checkout: (checkoutId: string) => [...posQueryKeys.all, "checkout", checkoutId] as const,
  reports: {
    all: () => [...posQueryKeys.all, "reports"] as const,
    sales: (entityId: string, from?: string, to?: string, page = 1) =>
      [...posQueryKeys.reports.all(), "sales", entityId, from, to, page] as const,
    returns: (entityId: string, from?: string, to?: string, page = 1) =>
      [...posQueryKeys.reports.all(), "returns", entityId, from, to, page] as const,
    paymentMix: (entityId: string, from?: string, to?: string) =>
      [...posQueryKeys.reports.all(), "paymentMix", entityId, from, to] as const,
    cashDifferences: (entityId: string, page = 1) =>
      [...posQueryKeys.reports.all(), "cashDifferences", entityId, page] as const,
    discounts: (entityId: string, from?: string, to?: string) =>
      [...posQueryKeys.reports.all(), "discounts", entityId, from, to] as const,
    sessionSummary: (entityId: string, sessionId: string) =>
      [...posQueryKeys.reports.all(), "sessionSummary", entityId, sessionId] as const,
  },
  catalog: {
    search: (query: string, entityId: string, page = 1) =>
      [...posQueryKeys.all, "catalog", "search", query, entityId, page] as const,
    barcode: (barcode: string, entityId: string) =>
      [...posQueryKeys.all, "catalog", "barcode", barcode, entityId] as const,
  },
};

// Query Hooks
export function useRegisters(entityId: string) {
  return useQuery<readonly RegisterResponse[]>({
    queryKey: posQueryKeys.registers(entityId),
    queryFn: () => posApi.listRegisters(entityId),
    enabled: Boolean(entityId),
    staleTime: 30_000,
  });
}
export const useRegistersQuery = useRegisters;

export function useRegister(registerId: string) {
  return useQuery<RegisterResponse>({
    queryKey: posQueryKeys.register(registerId),
    queryFn: () => posApi.getRegister(registerId),
    enabled: Boolean(registerId),
    staleTime: 30_000,
  });
}
export const useRegisterQuery = useRegister;

export function useCurrentSession(registerId?: string) {
  return useQuery<SessionResponse | null>({
    queryKey: posQueryKeys.currentSession(registerId || ""),
    queryFn: async () => {
      if (!registerId) return null;
      try {
        return await posApi.getCurrentSession(registerId);
      } catch {
        return null;
      }
    },
    enabled: Boolean(registerId),
    staleTime: 10_000,
    retry: false,
  });
}
export const useCurrentSessionQuery = useCurrentSession;

export function useSession(sessionId: string) {
  return useQuery<SessionResponse>({
    queryKey: posQueryKeys.session(sessionId),
    queryFn: () => posApi.getSession(sessionId),
    enabled: Boolean(sessionId),
  });
}
export const useSessionQuery = useSession;

export function useCartDetails(cartId: string | null) {
  return useQuery<CartDetailsResponse | null>({
    queryKey: posQueryKeys.cart(cartId || ""),
    queryFn: async () => {
      if (!cartId) return null;
      return posApi.getCartDetails(cartId);
    },
    enabled: Boolean(cartId),
    refetchInterval: 5_000,
  });
}
export const useCartDetailsQuery = useCartDetails;

export function useCheckout(checkoutId: string) {
  return useQuery<CheckoutResponse>({
    queryKey: posQueryKeys.checkout(checkoutId),
    queryFn: () => posApi.getCheckout(checkoutId),
    enabled: Boolean(checkoutId),
  });
}
export const useCheckoutQuery = useCheckout;

export function useSalesReport(entityId: string, from?: string, to?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: posQueryKeys.reports.sales(entityId, from, to, page),
    queryFn: () => posApi.listSales(entityId, from, to, page, pageSize),
    enabled: Boolean(entityId),
  });
}
export const useSalesReportQuery = useSalesReport;

export function useReturnsReport(entityId: string, from?: string, to?: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: posQueryKeys.reports.returns(entityId, from, to, page),
    queryFn: () => posApi.listReturns(entityId, from, to, page, pageSize),
    enabled: Boolean(entityId),
  });
}
export const useReturnsReportQuery = useReturnsReport;

export function usePaymentMixReport(entityId: string, from?: string, to?: string) {
  return useQuery<PosPaymentMixReport>({
    queryKey: posQueryKeys.reports.paymentMix(entityId, from, to),
    queryFn: () => posApi.getPaymentMix(entityId, from, to),
    enabled: Boolean(entityId),
  });
}
export const usePaymentMixReportQuery = usePaymentMixReport;

export function useCashDifferencesReport(entityId: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: posQueryKeys.reports.cashDifferences(entityId, page),
    queryFn: () => posApi.listCashDifferences(entityId, page, pageSize),
    enabled: Boolean(entityId),
  });
}
export const useCashDifferencesReportQuery = useCashDifferencesReport;

export function useDiscountSummaryReport(entityId: string, from?: string, to?: string) {
  return useQuery<PosDiscountSummaryReport>({
    queryKey: posQueryKeys.reports.discounts(entityId, from, to),
    queryFn: () => posApi.getDiscountSummary(entityId, from, to),
    enabled: Boolean(entityId),
  });
}
export const useDiscountSummaryReportQuery = useDiscountSummaryReport;

export function useCatalogSearch(entityId: string, query: string, page = 1, pageSize = 25) {
  return useQuery<CatalogSearchResult>({
    queryKey: posQueryKeys.catalog.search(query, entityId, page),
    queryFn: () => posApi.searchCatalog(query, entityId, page, pageSize),
    enabled: Boolean(entityId) && query.trim().length >= 2,
    staleTime: 10_000,
  });
}
export const useCatalogSearchQuery = useCatalogSearch;

export function useDownloadOfflinePackageQuery(registerId: string, deviceIdentity: string, enabled = true) {
  return useQuery({
    queryKey: [...posQueryKeys.all, "offlinePackage", registerId, deviceIdentity],
    queryFn: () => posApi.downloadOfflinePackage(registerId, deviceIdentity),
    enabled: Boolean(registerId) && enabled,
    staleTime: 300_000,
  });
}

// Individual Mutation Hooks
export function useCreateRegisterMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateRegisterRequest) => posApi.createRegister(entityId, req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.registers(entityId) }),
  });
}

export function useActivateRegisterMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (registerId: string) => posApi.activateRegister(registerId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.all }),
  });
}

export function useDeactivateRegisterMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (registerId: string) => posApi.deactivateRegister(registerId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.all }),
  });
}

export function useTerminalHeartbeatMutation() {
  return useMutation({
    mutationFn: (terminalId: string) => posApi.heartbeatTerminal(terminalId),
  });
}

export function useOpenSessionMutation(entityId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: OpenSessionRequest) => posApi.openSession(entityId, req),
    onSuccess: (session, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.currentSession(vars.registerId) });
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(session.id) });
    },
  });
}

export function useStartSessionCloseMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => posApi.startSessionClose(sessionId),
    onSuccess: (_, sessionId) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(sessionId) });
    },
  });
}

export function useRecordCashCountMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ sessionId, req }: { sessionId: string; req: CloseSessionRequest }) =>
      posApi.recordCashCount(sessionId, req),
    onSuccess: (session) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(session.id) });
      queryClient.invalidateQueries({ queryKey: posQueryKeys.currentSession(session.registerId) });
    },
  });
}

export function useCloseSessionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ sessionId, req }: { sessionId: string; req: CloseSessionRequest }) =>
      posApi.closeSession(sessionId, req),
    onSuccess: (session) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(session.id) });
      queryClient.invalidateQueries({ queryKey: posQueryKeys.currentSession(session.registerId) });
    },
  });
}

export function useAddCashMovementMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ sessionId, request }: { sessionId: string; request: CashMovementRequest }) =>
      posApi.addCashMovement(sessionId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(vars.sessionId) });
    },
  });
}

export function useApproveVarianceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => posApi.approveSessionVariance(sessionId),
    onSuccess: (session) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.session(session.id) });
      queryClient.invalidateQueries({ queryKey: posQueryKeys.currentSession(session.registerId) });
    },
  });
}

export function useCreateCartMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateCartRequest) => posApi.createCart(req),
    onSuccess: (cart) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(cart.id) });
    },
  });
}

export function useScanCartLineMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, request }: { cartId: string; request: ScanCartLineRequest }) =>
      posApi.scanCartLine(cartId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useAddCartLineMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, request }: { cartId: string; request: AddCartLineRequest }) =>
      posApi.addCartLine(cartId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useUpdateCartLineMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, lineId, request }: { cartId: string; lineId: string; request: UpdateCartLineRequest }) =>
      posApi.updateCartLine(cartId, lineId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useRemoveCartLineMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, lineId, expectedVersion }: { cartId: string; lineId: string; expectedVersion: number }) =>
      posApi.removeCartLine(cartId, lineId, expectedVersion),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useSetCartCustomerMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, request }: { cartId: string; request: SetCartCustomerRequest }) =>
      posApi.setCartCustomer(cartId, request.customerPartyId),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useDiscountCartLineMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, request }: { cartId: string; request: DiscountCartRequest }) =>
      posApi.applyCartLineDiscount(cartId, request),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
    },
  });
}

export function useHoldCartMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (cartId: string) => posApi.holdCart(cartId),
    onSuccess: (_, cartId) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(cartId) });
    },
  });
}

export function useResumeCartMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (cartId: string) => posApi.resumeCart(cartId),
    onSuccess: (_, cartId) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(cartId) });
    },
  });
}

export function useCancelCartMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (cartId: string) => posApi.cancelCart(cartId),
    onSuccess: (_, cartId) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(cartId) });
    },
  });
}

export function useCheckoutMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ cartId, request }: { cartId: string; request: CheckoutForCartRequest }) =>
      posApi.checkoutForCart(cartId, request),
    onSuccess: (checkout, vars) => {
      queryClient.invalidateQueries({ queryKey: posQueryKeys.cart(vars.cartId) });
      queryClient.invalidateQueries({ queryKey: posQueryKeys.checkout(checkout.checkoutId) });
    },
  });
}

export function useCreateReturnMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateReturnRequest) => posApi.createReturn(req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.reports.all() }),
  });
}

export function useRefundReturnMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ returnId, request }: { returnId: string; request: RefundReturnRequest }) =>
      posApi.refundReturn(returnId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.reports.all() }),
  });
}

export function useReturnCreditNoteMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (returnId: string) => posApi.createReturnCreditNote(returnId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.reports.all() }),
  });
}

export function useCompleteReturnMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (returnId: string) => posApi.completeReturn(returnId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: posQueryKeys.reports.all() }),
  });
}
