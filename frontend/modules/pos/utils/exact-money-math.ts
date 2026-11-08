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
  const baseFrac = fracPart.slice(0, 4).padEnd(4, "0");
  let fracBigInt = BigInt(baseFrac);
  if (fracPart.length > 4) {
    const fifthDigit = Number(fracPart[4]);
    if (!isNaN(fifthDigit) && fifthDigit >= 5) {
      fracBigInt += 1n;
    }
  }
  const total = BigInt(intPart || "0") * POS_MONEY_SCALE + fracBigInt;
  return isNegative ? -total : total;
}

/**
 * Formats a scaled BigInt back to a standard decimal string ("12.50").
 * Employs exact integer half-up rounding without floating-point representation drift or lost carry.
 */
export function fromScaledBigInt(scaled: bigint, decimals: number = 2): string {
  const isNegative = scaled < 0n;
  const abs = isNegative ? -scaled : scaled;
  const safeDecimals = Math.max(0, Math.min(4, decimals));
  const divisorExp = 4 - safeDecimals;
  const factor = 10n ** BigInt(divisorExp);
  const half = factor > 1n ? factor / 2n : 0n;
  const rounded = (abs + half) / factor;

  if (safeDecimals === 0) {
    return `${isNegative && rounded !== 0n ? "-" : ""}${rounded.toString()}`;
  }

  const outputFactor = 10n ** BigInt(safeDecimals);
  const whole = rounded / outputFactor;
  const fraction = rounded % outputFactor;
  const fracStr = fraction.toString().padStart(safeDecimals, "0");
  return `${isNegative && rounded !== 0n ? "-" : ""}${whole.toString()}.${fracStr}`;
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
