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
  currency,
  coloredSign = false,
  size = "medium"
}: ProjectMoneyProps): React.JSX.Element {
  if (amount === null || amount === undefined) {
    return <span style={{ color: "var(--color-content-muted, #64748b)" }}>—</span>;
  }

  let decimalAmount = "";
  if (typeof amount === "number") {
    if (!Number.isFinite(amount)) {
      return <span style={{ color: "var(--color-content-muted, #64748b)" }}>—</span>;
    }
    decimalAmount = amount.toFixed(2);
  } else if (typeof amount === "string" && amount.trim() !== "") {
    decimalAmount = amount.trim();
  } else {
    return <span style={{ color: "var(--color-content-muted, #64748b)" }}>—</span>;
  }

  const cleanCurrency = currency?.trim();
  if (!cleanCurrency) {
    return (
      <span
        style={{
          fontFamily: "var(--font-mono, monospace)",
          fontWeight: 600,
          color: coloredSign && decimalAmount.startsWith("-")
            ? "var(--color-status-danger, #ef4444)"
            : "inherit"
        }}
      >
        {decimalAmount}
      </span>
    );
  }

  return (
    <MoneyDisplay
      value={{
        amount: decimalAmount,
        currency: cleanCurrency
      }}
      displayCurrencyCode
      coloredSign={coloredSign}
      size={size}
    />
  );
}
