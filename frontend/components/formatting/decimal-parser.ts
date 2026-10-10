import type { DecimalString } from "../contracts/domain-values.ts";

const EASTERN_ARABIC_DIGITS: Readonly<Record<string, string>> = {
  "٠": "0", "١": "1", "٢": "2", "٣": "3", "٤": "4",
  "٥": "5", "٦": "6", "٧": "7", "٨": "8", "٩": "9"
};

const PERSIAN_DIGITS: Readonly<Record<string, string>> = {
  "۰": "0", "۱": "1", "۲": "2", "۳": "3", "۴": "4",
  "۵": "5", "۶": "6", "۷": "7", "۸": "8", "۹": "9"
};

/**
 * Normalizes user numeric input into a standard Western-digit decimal string ("1234.56").
 * Replaces Arabic/Persian digits, handles Arabic decimal separators (٫), and strips thousands separators.
 */
export function normalizeDecimalInput(input: string): string {
  if (!input) return "";

  let converted = "";
  for (const char of input.trim()) {
    if (EASTERN_ARABIC_DIGITS[char]) {
      converted += EASTERN_ARABIC_DIGITS[char];
    } else if (PERSIAN_DIGITS[char]) {
      converted += PERSIAN_DIGITS[char];
    } else {
      converted += char;
    }
  }

  // Remove Arabic thousands separator
  converted = converted.replace(/٬/g, "");

  // If Arabic decimal separator is present
  if (converted.includes("٫")) {
    converted = converted.replace(/,/g, "").replace(/٫/g, ".");
    return converted;
  }

  // If both comma and dot are present, determine which is decimal
  if (converted.includes(",") && converted.includes(".")) {
    const lastComma = converted.lastIndexOf(",");
    const lastDot = converted.lastIndexOf(".");
    if (lastComma > lastDot) {
      // European format: 1.234,56
      converted = converted.replace(/\./g, "").replace(",", ".");
    } else {
      // Standard format: 1,234.56
      converted = converted.replace(/,/g, "");
    }
    return converted;
  }

  // If only comma is present and looks like a decimal separator (e.g. 1234,56)
  if (converted.includes(",")) {
    converted = converted.replace(",", ".");
  }

  return converted;
}

/**
 * Checks if the given string is a syntactically valid decimal number.
 */
export function isValidDecimal(input: string): boolean {
  const normalized = normalizeDecimalInput(input);
  if (!normalized) return false;
  return /^-?\d+(\.\d+)?$/.test(normalized);
}

/**
 * Parses and validates an input string into a canonical DecimalString ("123.45").
 * Returns null if the input is invalid.
 */
export function parseDecimalInput(input: string): DecimalString | null {
  const normalized = normalizeDecimalInput(input);
  if (!isValidDecimal(normalized)) {
    return null;
  }
  return normalized;
}

/**
 * Formats a canonical DecimalString with thousands grouping and exact decimal places.
 */
export function formatDecimalNumber(
  value: DecimalString,
  options: {
    locale?: string;
    minFractionDigits?: number;
    maxFractionDigits?: number;
    useGrouping?: boolean;
  } = {}
): string {
  if (!isValidDecimal(value)) {
    return value;
  }

  const {
    locale = "en-US",
    minFractionDigits = 2,
    maxFractionDigits = 2,
    useGrouping = true
  } = options;

  const isNegative = value.startsWith("-");
  const absValue = isNegative ? value.slice(1) : value;
  const parts = absValue.split(".");
  const integerPart = parts[0] ?? "0";
  let fractionPart = parts[1] ?? "";

  // Pad or trim fraction part to match precision rules
  if (fractionPart.length < minFractionDigits) {
    fractionPart = fractionPart.padEnd(minFractionDigits, "0");
  } else if (fractionPart.length > maxFractionDigits) {
    // Truncate / round if necessary
    fractionPart = fractionPart.slice(0, maxFractionDigits);
  }

  // Format integer part with grouping if requested
  let formattedInteger = integerPart;
  if (useGrouping) {
    const intNum = BigInt(integerPart);
    formattedInteger = new Intl.NumberFormat(locale, { useGrouping: true }).format(intNum);
  }

  const sign = isNegative ? "-" : "";
  if (maxFractionDigits === 0 || fractionPart.length === 0) {
    return `${sign}${formattedInteger}`;
  }

  // Determine localized decimal separator
  const sampleFormatted = new Intl.NumberFormat(locale, { minimumFractionDigits: 1 }).format(1.1);
  const decimalSeparator = sampleFormatted.includes("٫") ? "٫" : sampleFormatted.includes(",") ? "," : ".";

  return `${sign}${formattedInteger}${decimalSeparator}${fractionPart}`;
}
