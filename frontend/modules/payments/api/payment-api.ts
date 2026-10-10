import { httpClient } from "../../../platform/api/http-client.ts";
import type { PaymentCommand, PaymentResult, PaymentStatusCheck } from "../types.ts";

export const paymentApi = {
  submitPayment: async (
    command: PaymentCommand,
    idempotencyKey: string,
    tenantId?: string
  ): Promise<PaymentResult> => {
    return httpClient<PaymentResult>("/api/v1/payments/dispatch", {
      method: "POST",
      idempotencyKey,
      body: JSON.stringify(command),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  checkStatus: async (
    paymentId: string,
    tenantId?: string
  ): Promise<PaymentStatusCheck> => {
    return httpClient<PaymentStatusCheck>(`/api/v1/payments/${paymentId}/status`, {
      method: "GET",
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};
