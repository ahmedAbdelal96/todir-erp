import type { QuantityValue, MoneyValue } from "../../components/contracts/domain-values.ts";
import type { StockBalance } from "./types.ts";

export interface StockAvailabilitySummary {
  readonly onHandBaseQuantity: string;
  readonly reservedBaseQuantity: string;
  readonly availableBaseQuantity: string;
}

interface DecimalParts {
  readonly sign: bigint;
  readonly digits: bigint;
  readonly scale: number;
}

function decimalParts(value: number | string | null | undefined): DecimalParts {
  const text = String(value ?? "0").trim();
  const negative = text.startsWith("-");
  const unsigned = negative || text.startsWith("+") ? text.slice(1) : text;
  const [integerPart = "0", fractionPart = ""] = unsigned.split(".");
  const digitsText = `${integerPart || "0"}${fractionPart}`.replace(/^0+(?=\d)/, "") || "0";

  return {
    sign: negative ? -1n : 1n,
    digits: BigInt(digitsText),
    scale: fractionPart.length
  };
}

function formatDecimal(digits: bigint, scale: number): string {
  if (digits === 0n) return "0";
  const negative = digits < 0n;
  const unsigned = (negative ? -digits : digits).toString().padStart(scale + 1, "0");
  const integerPart = scale > 0 ? unsigned.slice(0, -scale) || "0" : unsigned;
  const fractionPart = scale > 0 ? unsigned.slice(-scale).replace(/0+$/, "") : "";
  return `${negative ? "-" : ""}${integerPart}${fractionPart ? `.${fractionPart}` : ""}`;
}

export function sumQuantities(values: readonly (number | string | null | undefined)[]): string {
  const parts = values.map(decimalParts);
  const scale = parts.reduce((maximum, part) => Math.max(maximum, part.scale), 0);
  const total = parts.reduce(
    (sum, part) => sum + (part.sign * part.digits * (10n ** BigInt(scale - part.scale))),
    0n
  );
  return formatDecimal(total, scale);
}

export function compareQuantities(left: number | string, right: number | string): number {
  const leftPart = decimalParts(left);
  const rightPart = decimalParts(right);
  const scale = Math.max(leftPart.scale, rightPart.scale);
  const leftScaled = leftPart.sign * leftPart.digits * (10n ** BigInt(scale - leftPart.scale));
  const rightScaled = rightPart.sign * rightPart.digits * (10n ** BigInt(scale - rightPart.scale));
  return leftScaled > rightScaled ? 1 : leftScaled < rightScaled ? -1 : 0;
}

export function summarizeStockBalances(balances: readonly StockBalance[]): StockAvailabilitySummary {
  return {
    onHandBaseQuantity: sumQuantities(balances.map((balance) => balance.onHandBaseQuantity)),
    reservedBaseQuantity: sumQuantities(balances.map((balance) => balance.reservedBaseQuantity)),
    availableBaseQuantity: sumQuantities(balances.map((balance) => balance.availableBaseQuantity))
  };
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
