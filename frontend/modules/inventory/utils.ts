import type { QuantityValue, MoneyValue } from "../../components/contracts/domain-values.ts";

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
