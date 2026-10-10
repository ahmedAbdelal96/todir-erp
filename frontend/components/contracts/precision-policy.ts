export interface CurrencyPrecisionRule {
  readonly standardFractionDigits: number;
  readonly minFractionDigits: number;
  readonly maxFractionDigits: number;
}

const DEFAULT_CURRENCY_PRECISIONS: Readonly<Record<string, CurrencyPrecisionRule>> = {
  EGP: { standardFractionDigits: 2, minFractionDigits: 2, maxFractionDigits: 2 },
  SAR: { standardFractionDigits: 2, minFractionDigits: 2, maxFractionDigits: 2 },
  AED: { standardFractionDigits: 2, minFractionDigits: 2, maxFractionDigits: 2 },
  USD: { standardFractionDigits: 2, minFractionDigits: 2, maxFractionDigits: 2 },
  EUR: { standardFractionDigits: 2, minFractionDigits: 2, maxFractionDigits: 2 },
  KWD: { standardFractionDigits: 3, minFractionDigits: 3, maxFractionDigits: 3 },
  BHD: { standardFractionDigits: 3, minFractionDigits: 3, maxFractionDigits: 3 },
  OMR: { standardFractionDigits: 3, minFractionDigits: 3, maxFractionDigits: 3 },
  JOD: { standardFractionDigits: 3, minFractionDigits: 3, maxFractionDigits: 3 },
  JPY: { standardFractionDigits: 0, minFractionDigits: 0, maxFractionDigits: 0 }
};

export const DEFAULT_FALLBACK_CURRENCY_PRECISION: CurrencyPrecisionRule = {
  standardFractionDigits: 2,
  minFractionDigits: 2,
  maxFractionDigits: 2
};

export function getCurrencyPrecision(currencyCode: string): CurrencyPrecisionRule {
  const normalized = currencyCode.trim().toUpperCase();
  return DEFAULT_CURRENCY_PRECISIONS[normalized] ?? DEFAULT_FALLBACK_CURRENCY_PRECISION;
}

export interface QuantityPrecisionRule {
  readonly defaultDecimals: number;
  readonly maxDecimals: number;
}

export const DEFAULT_QUANTITY_PRECISION: QuantityPrecisionRule = {
  defaultDecimals: 2,
  maxDecimals: 4
};
