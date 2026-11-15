import React from "react";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";

export interface ProjectMoneyProps {
  readonly amount: number | string | null | undefined;
  readonly currency?: string | null | undefined;
  readonly coloredSign?: boolean | undefined;
  readonly size?: "small" | "medium" | "large" | undefined;
}

export function ProjectMoney({
  amount,
  currency = "EGP",
  coloredSign = false,
  size = "medium"
}: ProjectMoneyProps): React.JSX.Element {
  let decimalAmount = "0.00";
  if (typeof amount === "number") {
    decimalAmount = Number.isFinite(amount) ? amount.toFixed(2) : "0.00";
  } else if (typeof amount === "string" && amount.trim() !== "") {
    decimalAmount = amount;
  }

  return (
    <MoneyDisplay
      value={{
        amount: decimalAmount,
        currency: currency || "EGP"
      }}
      displayCurrencyCode
      coloredSign={coloredSign}
      size={size}
    />
  );
}
