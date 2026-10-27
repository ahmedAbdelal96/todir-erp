import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRuntimeContext, usePermissions } from "../../../core/runtime/runtime-provider.tsx";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { paymentApi } from "../services/payment-api.ts";
import type {
  PaymentListQuery,
  CreatePaymentRequest,
  CreateInternalTransferRequest,
  CreateRefundRequest,
  ReversePaymentRequest,
  CreatePaymentFeeRequest,
  ApplyPaymentRequest,
  CreatePaymentAccountRequest,
  UpdatePaymentAccountStatusRequest,
  CreatePaymentIntentRequest,
  PaymentRequestListQuery,
  CreatePaymentLinkRequest
} from "../types.ts";

export interface PaymentsContextScope {
  tenantId?: string | undefined;
  legalEntityId?: string | undefined;
  branchId?: string | undefined;
}

export function usePaymentsScope(explicit?: PaymentsContextScope): {
  tenantId: string;
  legalEntityId: string;
  branchId: string;
} {
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  let runtimeEntityId = "";
  let runtimeTenantId = "";
  let runtimeBranchId = "";

  try {
    const runtime = useRuntimeContext();
    runtimeEntityId = (runtime as unknown as { legalEntity?: { legalEntityId?: string } }).legalEntity?.legalEntityId || "";
    runtimeTenantId = (runtime as unknown as { tenant?: { tenantId?: string } }).tenant?.tenantId || "";
    runtimeBranchId = (runtime as unknown as { branch?: { branchId?: string } }).branch?.branchId || "";
  } catch {
    // Graceful fallback outside provider
  }

  let queryEntityId = "";
  let queryBranchId = "";
  if (typeof window !== "undefined" && window.location.search) {
    try {
      const sp = new URLSearchParams(window.location.search);
      queryEntityId = sp.get("legalEntityId") || "";
      queryBranchId = sp.get("branchId") || "";
    } catch {
      // ignore
    }
  }

  const session = sessionQuery.data as any;
  const tenantId =
    explicit?.tenantId ||
    session?.tenantId ||
    session?.activeTenant?.id ||
    runtimeTenantId ||
    "";

  let legalEntityId =
    explicit?.legalEntityId ||
    queryEntityId ||
    session?.context?.legalEntities?.[0]?.id ||
    runtimeEntityId ||
    "";

  let branchId =
    explicit?.branchId ||
    queryBranchId ||
    session?.context?.branches?.[0]?.id ||
    runtimeBranchId ||
    "";

  if (!legalEntityId && typeof window !== "undefined") {
    legalEntityId = window.sessionStorage.getItem("erp.current.legalEntityId") || "";
  }
  if (!branchId && typeof window !== "undefined") {
    branchId = window.sessionStorage.getItem("erp.current.branchId") || "";
  }

  return { tenantId, legalEntityId, branchId };
}

export function usePaymentsPermissions() {
  let perms: { hasPermission: (perm: string) => boolean } | null = null;
  try {
    perms = usePermissions();
  } catch {
    // Allow fallback
  }

  return {
    canReadPayments: perms ? perms.hasPermission("payments.read") : true,
    canCreateIncoming: perms ? perms.hasPermission("payments.incoming.create") : true,
    canCreateOutgoing: perms ? perms.hasPermission("payments.outgoing.create") : true,
    canCreateTransfer: perms ? perms.hasPermission("payments.internal_transfer.create") : true,
    canCompleteManual: perms ? perms.hasPermission("payments.complete_manual") : true,
    canCancelPending: perms ? perms.hasPermission("payments.cancel_pending") : true,
    canReverse: perms ? perms.hasPermission("payments.reverse") : true,
    canCreateRefund: perms ? perms.hasPermission("payments.refund.create") : true,
    canApproveRefund: perms ? perms.hasPermission("payments.refund.approve") : true,
    canReadAccounts: perms ? perms.hasPermission("payments.account.read") : true,
    canManageAccounts: perms ? perms.hasPermission("payments.account.manage") : true,
    canReadProviders: perms ? perms.hasPermission("payments.provider.read") : true,
    canManageProviders: perms ? perms.hasPermission("payments.provider.manage") : true,
    canRetryProviders: perms ? perms.hasPermission("payments.provider.retry") : true,
    canReconcileProviders: perms ? perms.hasPermission("payments.provider.reconcile") : true,
    canReadCashbox: perms ? perms.hasPermission("payments.cashbox.read") : true,
    canUseCashbox: perms ? perms.hasPermission("payments.cashbox.use") : true,
    canManageCashbox: perms ? perms.hasPermission("payments.cashbox.manage") : true,
    canOverrideFee: perms ? perms.hasPermission("payments.fee.override") : true,
    canOverrideFx: perms ? perms.hasPermission("payments.fx.override") : true,
    canOverrideDate: perms ? perms.hasPermission("payments.date.override") : true,
    canViewAudit: perms ? perms.hasPermission("payments.audit.view") : true
  };
}

export const paymentKeys = {
  all: ["payments"] as const,
  lists: () => [...paymentKeys.all, "list"] as const,
  list: (entityId: string, params: Omit<PaymentListQuery, "legalEntityId">) =>
    [...paymentKeys.lists(), entityId, params] as const,
  details: () => [...paymentKeys.all, "detail"] as const,
  detail: (entityId: string, paymentId: string) =>
    [...paymentKeys.details(), entityId, paymentId] as const,
  accounts: (entityId: string, branchId?: string) =>
    [...paymentKeys.all, "accounts", entityId, branchId || "all"] as const,
  fees: (entityId: string, paymentId: string) =>
    [...paymentKeys.all, "fees", entityId, paymentId] as const,
  requests: (entityId: string, params?: Omit<PaymentRequestListQuery, "legalEntityId">) =>
    [...paymentKeys.all, "requests", entityId, params || {}] as const,
  requestDetail: (entityId: string, requestId: string) =>
    [...paymentKeys.all, "requestDetail", entityId, requestId] as const
};

export function usePaymentList(params: PaymentListQuery, enabled = true) {
  const { legalEntityId, ...rest } = params;
  return useQuery({
    queryKey: paymentKeys.list(legalEntityId, rest),
    queryFn: () => paymentApi.listPayments(params),
    enabled: Boolean(legalEntityId) && enabled,
    staleTime: 30_000
  });
}

export function usePaymentRecord(legalEntityId: string, paymentId: string, branchId?: string, enabled = true) {
  return useQuery({
    queryKey: paymentKeys.detail(legalEntityId, paymentId),
    queryFn: () => paymentApi.getPayment(legalEntityId, paymentId, branchId),
    enabled: Boolean(legalEntityId && paymentId) && enabled,
    staleTime: 30_000
  });
}

export function usePaymentAccounts(legalEntityId: string, branchId?: string, enabled = true) {
  return useQuery({
    queryKey: paymentKeys.accounts(legalEntityId, branchId),
    queryFn: () => paymentApi.listAccounts(legalEntityId, branchId),
    enabled: Boolean(legalEntityId) && enabled,
    staleTime: 60_000
  });
}

export function usePaymentFees(legalEntityId: string, paymentId: string, branchId?: string, enabled = true) {
  return useQuery({
    queryKey: paymentKeys.fees(legalEntityId, paymentId),
    queryFn: () => paymentApi.listFees(legalEntityId, paymentId, branchId),
    enabled: Boolean(legalEntityId && paymentId) && enabled,
    staleTime: 30_000
  });
}

export function usePaymentRequests(params: PaymentRequestListQuery, enabled = true) {
  const { legalEntityId, ...rest } = params;
  return useQuery({
    queryKey: paymentKeys.requests(legalEntityId, rest),
    queryFn: () => paymentApi.listPaymentRequests(params),
    enabled: Boolean(legalEntityId) && enabled,
    staleTime: 30_000
  });
}

export function usePaymentRequestDetails(legalEntityId: string, requestId: string, enabled = true) {
  return useQuery({
    queryKey: paymentKeys.requestDetail(legalEntityId, requestId),
    queryFn: () => paymentApi.getPaymentRequestDetails(legalEntityId, requestId),
    enabled: Boolean(legalEntityId && requestId) && enabled,
    staleTime: 30_000
  });
}

export function useCreateIncomingPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request, idempotencyKey }: { legalEntityId: string; request: CreatePaymentRequest; idempotencyKey?: string | undefined }) =>
      paymentApi.createIncomingPayment(legalEntityId, request, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useCreateOutgoingPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request, idempotencyKey }: { legalEntityId: string; request: CreatePaymentRequest; idempotencyKey?: string | undefined }) =>
      paymentApi.createOutgoingPayment(legalEntityId, request, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useCompleteManualPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, branchId, idempotencyKey }: { legalEntityId: string; paymentId: string; branchId?: string | undefined; idempotencyKey?: string | undefined }) =>
      paymentApi.completeManualPayment(legalEntityId, paymentId, branchId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useCancelPendingPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, branchId, idempotencyKey }: { legalEntityId: string; paymentId: string; branchId?: string | undefined; idempotencyKey?: string | undefined }) =>
      paymentApi.cancelPendingPayment(legalEntityId, paymentId, branchId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
    }
  });
}

export function useCreateRefund() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, request, branchId, idempotencyKey }: { legalEntityId: string; paymentId: string; request: CreateRefundRequest; branchId?: string | undefined; idempotencyKey?: string | undefined }) =>
      paymentApi.createRefund(legalEntityId, paymentId, request, branchId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
    }
  });
}

export function useReversePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, request, branchId, idempotencyKey }: { legalEntityId: string; paymentId: string; request: ReversePaymentRequest; branchId?: string | undefined; idempotencyKey?: string | undefined }) =>
      paymentApi.reversePayment(legalEntityId, paymentId, request, branchId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
    }
  });
}

export function useAddPaymentFee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, request, idempotencyKey }: { legalEntityId: string; paymentId: string; request: CreatePaymentFeeRequest; idempotencyKey?: string | undefined }) =>
      paymentApi.addFee(legalEntityId, paymentId, request, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.fees(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
    }
  });
}

export function useApplyPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, paymentId, request, branchId, idempotencyKey }: { legalEntityId: string; paymentId: string; request: ApplyPaymentRequest; branchId?: string | undefined; idempotencyKey?: string | undefined }) =>
      paymentApi.applyPayment(legalEntityId, paymentId, request, branchId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.detail(vars.legalEntityId, vars.paymentId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
    }
  });
}

export function useCreateInternalTransfer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request, idempotencyKey }: { legalEntityId: string; request: CreateInternalTransferRequest; idempotencyKey?: string | undefined }) =>
      paymentApi.createInternalTransfer(legalEntityId, request, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useCreatePaymentAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request }: { legalEntityId: string; request: CreatePaymentAccountRequest }) =>
      paymentApi.createAccount(legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useUpdatePaymentAccountStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, accountId, request }: { legalEntityId: string; accountId: string; request: UpdatePaymentAccountStatusRequest }) =>
      paymentApi.updateAccountStatus(legalEntityId, accountId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.accounts(vars.legalEntityId) });
    }
  });
}

export function useCreatePaymentRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, request, idempotencyKey }: { legalEntityId: string; request: CreatePaymentIntentRequest; idempotencyKey?: string }) =>
      paymentApi.createPaymentRequest(legalEntityId, request, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requests(vars.legalEntityId) });
    }
  });
}

export function useRetryPaymentRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, requestId, idempotencyKey }: { legalEntityId: string; requestId: string; idempotencyKey?: string }) =>
      paymentApi.retryPaymentRequest(legalEntityId, requestId, idempotencyKey),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requestDetail(vars.legalEntityId, vars.requestId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requests(vars.legalEntityId) });
    }
  });
}

export function useSyncPaymentRequestStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, requestId }: { legalEntityId: string; requestId: string }) =>
      paymentApi.syncPaymentRequestStatus(legalEntityId, requestId),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requestDetail(vars.legalEntityId, vars.requestId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requests(vars.legalEntityId) });
    }
  });
}

export function useCancelPaymentRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, requestId }: { legalEntityId: string; requestId: string }) =>
      paymentApi.cancelPaymentRequest(legalEntityId, requestId),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requestDetail(vars.legalEntityId, vars.requestId) });
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requests(vars.legalEntityId) });
    }
  });
}

export function useCreatePaymentLink() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, requestId, request }: { legalEntityId: string; requestId: string; request: CreatePaymentLinkRequest }) =>
      paymentApi.createPaymentLink(legalEntityId, requestId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requestDetail(vars.legalEntityId, vars.requestId) });
    }
  });
}

export function useCancelPaymentLink() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ legalEntityId, requestId, linkId }: { legalEntityId: string; requestId: string; linkId: string }) =>
      paymentApi.cancelPaymentLink(legalEntityId, requestId, linkId),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.requestDetail(vars.legalEntityId, vars.requestId) });
    }
  });
}

export function useSubmitPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ command, idempotencyKey, tenantId }: { command: any; idempotencyKey: string; tenantId?: string | undefined }) =>
      paymentApi.submitPayment(command, idempotencyKey, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: paymentKeys.lists() });
    }
  });
}
