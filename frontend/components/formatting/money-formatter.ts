import type { MoneyValue } from "../contracts/domain-values.ts";
import { getCurrencyPrecision } from "../contracts/precision-policy.ts";
import { formatDecimalNumber } from "./decimal-parser.ts";

export interface FormatMoneyOptions {
  readonly locale?: string;
  readonly displayCurrencyCode?: boolean;
  readonly useGrouping?: boolean;
  readonly customDecimals?: number;
}

const ARABIC_CURRENCY_SYMBOLS: Readonly<Record<string, string>> = {
  EGP: "ج.م",
  SAR: "ر.س",
  AED: "د.إ",
  KWD: "د.ك",
  BHD: "د.ب",
  OMR: "ر.ع",
  JOD: "د.أ",
  USD: "$",
  EUR: "€"
};

/**
 * Formats a MoneyValue into a localized string preserving exact decimal semantics.
 */
export function formatMoney(
  value: MoneyValue | null | undefined,
  options: FormatMoneyOptions = {}
): string {
  if (!value || !value.amount) return "—";

  const {
    locale = "en-US",
    displayCurrencyCode = false,
    useGrouping = true,
    customDecimals
  } = options;

  const currency = value.currency.toUpperCase();
  const precisionRule = getCurrencyPrecision(currency);
  const fractionDigits = customDecimals ?? precisionRule.standardFractionDigits;

  const formattedAmount = formatDecimalNumber(value.amount, {
    locale,
    minFractionDigits: fractionDigits,
    maxFractionDigits: fractionDigits,
    useGrouping
  });

  const isArabicLocale = locale.startsWith("ar");
  const currencySymbol = isArabicLocale
    ? ARABIC_CURRENCY_SYMBOLS[currency] ?? currency
    : currency;

  if (displayCurrencyCode) {
    return isArabicLocale
      ? `${formattedAmount} ${currency}`
      : `${currency} ${formattedAmount}`;
  }

  return isArabicLocale
    ? `${formattedAmount} ${currencySymbol}`
    : `${currencySymbol} ${formattedAmount}`;
}
