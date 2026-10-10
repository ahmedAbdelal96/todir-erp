import type { QuantityValue } from "../contracts/domain-values.ts";
import { DEFAULT_QUANTITY_PRECISION } from "../contracts/precision-policy.ts";
import { formatDecimalNumber } from "./decimal-parser.ts";

export interface FormatQuantityOptions {
  readonly locale?: string | undefined;
  readonly decimals?: number | undefined;
  readonly useGrouping?: boolean | undefined;
  readonly hideUnit?: boolean | undefined;
}

/**
 * Formats a QuantityValue with exact decimal representation and unit of measure.
 */
export function formatQuantity(
  value: QuantityValue | null | undefined,
  options: FormatQuantityOptions = {}
): string {
  if (!value || !value.value) return "—";

  const {
    locale = "en-US",
    decimals = DEFAULT_QUANTITY_PRECISION.defaultDecimals,
    useGrouping = true,
    hideUnit = false
  } = options;

  const formattedValue = formatDecimalNumber(value.value, {
    locale,
    minFractionDigits: decimals,
    maxFractionDigits: decimals,
    useGrouping
  });

  if (hideUnit || !value.unitOfMeasure) {
    return formattedValue;
  }

  return `${formattedValue} ${value.unitOfMeasure}`;
}

export interface FormatPercentageOptions {
  readonly locale?: string | undefined;
  readonly decimals?: number | undefined;
  readonly useGrouping?: boolean | undefined;
}

/**
 * Formats a rate into a percentage string (e.g. "14.00 %").
 */
export function formatPercentage(
  value: string | number | null | undefined,
  options: FormatPercentageOptions = {}
): string {
  if (value === null || value === undefined || value === "") return "—";

  const {
    locale = "en-US",
    decimals = 2,
    useGrouping = false
  } = options;

  const strValue = typeof value === "number" ? value.toFixed(decimals) : String(value);
  const formatted = formatDecimalNumber(strValue, {
    locale,
    minFractionDigits: decimals,
    maxFractionDigits: decimals,
    useGrouping
  });

  return `${formatted}%`;
}
