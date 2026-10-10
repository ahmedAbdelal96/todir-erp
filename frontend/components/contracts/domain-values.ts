/**
 * Exact numeric string representing a decimal value without floating-point precision loss.
 * Example: "1234.56"
 */
export type DecimalString = string;

/**
 * ISO-4217 Currency Code representation (e.g. 'SAR', 'USD', 'EUR').
 */
export type CurrencyCode = string;

/**
 * Monetary value with exact decimal amount and ISO-4217 currency code.
 */
export interface MoneyValue {
  readonly amount: DecimalString;
  readonly currency: CurrencyCode;
}

/**
 * Quantity value with exact decimal value and unit of measure code.
 */
export interface QuantityValue {
  readonly value: DecimalString;
  readonly unitOfMeasure: string;
}

/**
 * Percentage value represented as exact decimal (e.g. "14.00" for 14%).
 */
export interface PercentageValue {
  readonly rate: DecimalString;
}

/**
 * ISO-8601 Calendar date string: YYYY-MM-DD.
 */
export type LocalDateString = string;

/**
 * ISO-8601 UTC timestamp string: YYYY-MM-DDTHH:mm:ss.sssZ.
 */
export type IsoInstantString = string;

/**
 * Entity reference identifier wrapper.
 */
export interface EntityReference {
  readonly id: string;
  readonly code?: string;
  readonly name: string;
  readonly type?: string;
}
