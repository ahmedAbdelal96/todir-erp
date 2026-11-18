import React from "react";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";

interface Props {
  readonly amount?: number | null | undefined;
  readonly currency?: string | null | undefined;
  readonly size?: "small" | "medium" | "large" | undefined;
}

export function BankingMoney({ amount, currency, size = "medium" }: Props): React.JSX.Element {
  if (amount == null) return <span>—</span>;
  const safeCurrency = currency || "EGP";
  return (
    <MoneyDisplay
      value={{ amount: amount.toFixed(2), currency: safeCurrency }}
      displayCurrencyCode
      size={size}
    />
  );
}
