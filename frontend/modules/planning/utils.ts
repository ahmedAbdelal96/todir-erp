import type { MoneyValue } from "../../components/contracts/domain-values.ts";

export function toMoney(amount: number | null | undefined, currency: string): MoneyValue | null {
  if (amount === null || amount === undefined) return null;
  return { amount: String(amount), currency };
}
