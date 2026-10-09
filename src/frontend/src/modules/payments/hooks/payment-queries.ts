import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { paymentApi } from "../api/payment-api.ts";
import type { PaymentCommand, PaymentResult, PaymentStatusCheck } from "../types.ts";
import { queryKeyFactory } from "../../../platform/api/query-keys.ts";

export interface UsePaymentContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useSubmitPayment(context: UsePaymentContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<PaymentResult, Error, { command: PaymentCommand; idempotencyKey: string }>({
    mutationFn: ({ command, idempotencyKey }) =>
      paymentApi.submitPayment(command, idempotencyKey, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "payments", "transactions"),
      });
    },
  });
}

export function usePaymentStatus(paymentId: string | null, context: UsePaymentContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<PaymentStatusCheck>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "payments", "transactions", paymentId ?? ""),
    queryFn: () => paymentApi.checkStatus(paymentId!, tenantId),
    enabled: Boolean(paymentId),
    refetchInterval: (query) => {
      const data = query.state.data;
      if (data?.status === "Processing" || data?.status === "Unknown") {
        return 3_000;
      }
      return false;
    },
  });
}
