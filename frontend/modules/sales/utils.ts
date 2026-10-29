import type { QuantityValue, MoneyValue } from "../../components/contracts/domain-values.ts";

export function computeQuotationValidUntil(
  quoteDateStr: string,
  validityDays?: number | null
): string {
  if (validityDays == null || validityDays <= 0) return "";

  try {
    const date = new Date(quoteDateStr);
    if (!Number.isNaN(date.getTime())) {
      date.setDate(date.getDate() + validityDays);
      return date.toISOString().slice(0, 10);
    }
  } catch {
    // Invalid dates leave validity unset for the user or backend policy to resolve.
  }

  return "";
}

export function toQty(value: number | string | null | undefined, unitOfMeasure = "EA"): QuantityValue {
  return {
    value: value != null ? String(value) : "0",
    unitOfMeasure
  };
}

export function toMoneyVal(amount: number | string | null | undefined, currency = "SAR"): MoneyValue {
  return {
    amount: amount != null ? Number(amount).toFixed(2) : "0.00",
    currency
  };
}
