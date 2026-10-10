import React from "react";
import type { MoneyValue } from "../contracts/domain-values.ts";
import { formatMoney } from "../formatting/money-formatter.ts";
import clsx from "clsx";

export interface MoneyDisplayProps {
  readonly value: MoneyValue | null | undefined;
  readonly locale?: string | undefined;
  readonly displayCurrencyCode?: boolean | undefined;
  readonly coloredSign?: boolean | undefined;
  readonly size?: "small" | "medium" | "large" | undefined;
  readonly className?: string | undefined;
}

export function MoneyDisplay({
  value,
  locale = "en-US",
  displayCurrencyCode = false,
  coloredSign = false,
  size = "medium",
  className
}: MoneyDisplayProps): React.JSX.Element {
  const formatted = formatMoney(value, { locale, displayCurrencyCode });
  const isNegative = value?.amount?.startsWith("-");
  const isZero = value?.amount === "0" || value?.amount === "0.00";

  const getFontSize = () => {
    switch (size) {
      case "small":
        return "0.8125rem";
      case "large":
        return "1.25rem";
      case "medium":
      default:
        return "0.875rem";
    }
  };

  const getColor = () => {
    if (!coloredSign) return "inherit";
    if (isNegative) return "var(--color-status-danger)";
    if (isZero) return "var(--color-content-muted)";
    return "var(--color-status-success)";
  };

  return (
    <span
      className={clsx("ui-money-display", className)}
      style={{
        fontFamily: "var(--font-mono, monospace)",
        fontSize: getFontSize(),
        fontWeight: size === "large" ? 600 : 500,
        color: getColor(),
        direction: "ltr",
        display: "inline-block",
        whiteSpace: "nowrap"
      }}
    >
      {formatted}
    </span>
  );
}
