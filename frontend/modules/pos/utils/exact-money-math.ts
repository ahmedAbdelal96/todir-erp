// Exact Fixed-Scale Decimal Arithmetic for POS Financial Calculations
// Eliminates IEEE 754 binary floating-point representation drift (e.g., 0.1 + 0.2 != 0.3)

import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";

/** Fixed scale factor for 4 decimal places (standard financial precision) */
export const POS_MONEY_SCALE = 10000n;

/**
 * Parses any string or number into a scaled BigInt representation (value * 10,000).
 * Handles Eastern Arabic digits, commas, and negative signs.
 */
export function toScaledBigInt(val: string | number | undefined | null): bigint {
  if (val === undefined || val === null || val === "") return 0n;
  const str = normalizeDecimalInput(String(val)).trim();
  if (!str) return 0n;
  const isNegative = str.startsWith("-");
  const abs = isNegative ? str.slice(1) : str;
  const [intPart = "0", fracPart = ""] = abs.split(".");
  const paddedFrac = (fracPart + "0000").slice(0, 4);
  const total = BigInt(intPart) * POS_MONEY_SCALE + BigInt(paddedFrac);
  return isNegative ? -total : total;
}

/**
 * Formats a scaled BigInt back to a standard decimal string ("12.50").
 */
export function fromScaledBigInt(scaled: bigint, decimals: number = 2): string {
  const isNegative = scaled < 0n;
  const abs = isNegative ? -scaled : scaled;
  const intPart = abs / POS_MONEY_SCALE;
  const fracPart = abs % POS_MONEY_SCALE;
  const fracStr = fracPart.toString().padStart(4, "0");
  const rounded = Number(`0.${fracStr}`).toFixed(decimals).slice(2);
  return `${isNegative ? "-" : ""}${intPart}.${rounded}`;
}

/**
 * Converts a scaled BigInt to a safe JS number for API requests.
 */
export function scaledToNumber(scaled: bigint): number {
  return Number(scaled) / 10000;
}

/**
 * Computes exact cash change: max(0, cashTendered - totalDue).
 */
export function calculateExactChange(cashTendered: string | number, totalDue: string | number): {
  changeAmount: number;
  changeStr: string;
  isSufficient: boolean;
} {
  const tenderedScaled = toScaledBigInt(cashTendered);
  const dueScaled = toScaledBigInt(totalDue);
  const diff = tenderedScaled - dueScaled;
  const isSufficient = diff >= 0n;
  const changeScaled = diff > 0n ? diff : 0n;
  return {
    changeAmount: scaledToNumber(changeScaled),
    changeStr: fromScaledBigInt(changeScaled, 2),
    isSufficient,
  };
}

/**
 * Computes exact split remaining amount: max(0, totalDue - sum(splitTenders)).
 */
export function calculateExactSplitRemaining(
  totalDue: string | number,
  tenders: ReadonlyArray<{ amount: number | string }>
): {
  paidTotal: number;
  paidTotalStr: string;
  remainingAmount: number;
  remainingStr: string;
  isComplete: boolean;
} {
  const dueScaled = toScaledBigInt(totalDue);
  const paidScaled = tenders.reduce(
    (sum, t) => sum + toScaledBigInt(t.amount),
    0n
  );
  const diff = dueScaled - paidScaled;
  const isComplete = paidScaled >= dueScaled;
  const remainingScaled = diff > 0n ? diff : 0n;
  return {
    paidTotal: scaledToNumber(paidScaled),
    paidTotalStr: fromScaledBigInt(paidScaled, 2),
    remainingAmount: scaledToNumber(remainingScaled),
    remainingStr: fromScaledBigInt(remainingScaled, 2),
    isComplete,
  };
}
