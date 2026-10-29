import type { QuantityValue, MoneyValue } from "../../components/contracts/domain-values.ts";

export function toMoneyVal(amount: number | string | undefined | null, currency = "SAR"): MoneyValue {
  if (amount == null) return { amount: "0.00", currency };
  const num = typeof amount === "number" ? amount : parseFloat(String(amount));
  if (isNaN(num)) return { amount: "0.00", currency };
  return {
    amount: num.toFixed(2),
    currency
  };
}

export function toQty(quantity: number | string | undefined | null, unitOfMeasure = "EA"): QuantityValue {
  if (quantity == null) return { value: "0", unitOfMeasure };
  const num = typeof quantity === "number" ? quantity : parseFloat(String(quantity));
  if (isNaN(num)) return { value: "0", unitOfMeasure };
  return {
    value: Number.isInteger(num) ? num.toString() : num.toFixed(2),
    unitOfMeasure
  };
}

export function generateIdempotencyKey(prefix = "purchasing"): string {
  const ts = Date.now().toString(36);
  const rnd = Math.random().toString(36).substring(2, 8);
  return `${prefix}-${ts}-${rnd}`;
}

export function formatPoNumber(id: string, sequenceNumber?: number): string {
  if (sequenceNumber != null) {
    return `PO-2026-${String(sequenceNumber).padStart(4, "0")}`;
  }
  const cleanId = id.replace(/-/g, "").toUpperCase();
  const shortCode = cleanId.substring(cleanId.length - 4);
  return `PO-2026-${shortCode}`;
}
