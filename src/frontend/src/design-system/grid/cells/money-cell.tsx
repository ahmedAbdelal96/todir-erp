import React from "react";
import type { MoneyValue } from "../../contracts/domain-values.ts";
import { MoneyDisplay } from "../../fields/money-display.tsx";

export interface MoneyCellProps {
  readonly value: MoneyValue | string | null | undefined;
  readonly currency?: string | undefined;
  readonly locale?: string | undefined;
  readonly coloredSign?: boolean | undefined;
}

export function MoneyCell({
  value,
  currency = "EGP",
  locale,
  coloredSign
}: MoneyCellProps): React.JSX.Element {
  const moneyValue: MoneyValue | null =
    value === null || value === undefined
      ? null
      : typeof value === "string"
      ? { amount: value, currency }
      : value;

  return (
    <MoneyDisplay
      value={moneyValue}
      locale={locale}
      coloredSign={coloredSign}
      size="small"
    />
  );
}
