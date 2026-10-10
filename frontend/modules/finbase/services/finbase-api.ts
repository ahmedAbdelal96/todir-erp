import { httpClient } from "../../../core/api/http-client.ts";
import type { Currency, PaymentScheduleLine, PaymentTermRule } from "../types.ts";

export const finbaseApi = {
  currencies: () => httpClient<readonly Currency[]>("/api/v1/finbase/currencies"),
  previewSchedule: (totalAmount: string, basisDate: string, rules: readonly PaymentTermRule[]) => httpClient<readonly PaymentScheduleLine[]>("/api/v1/finbase/payment-schedule/preview", { method: "POST", body: JSON.stringify({ totalAmount, basisDate, scale: 2, rules }) }),
};
