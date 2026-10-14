import React from "react";
import type { QuantityValue } from "../contracts/domain-values.ts";
import { formatQuantity } from "../formatting/quantity-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface QuantityDisplayProps {
  readonly value: QuantityValue | null | undefined;
  readonly locale?: string;
  readonly decimals?: number;
  readonly hideUnit?: boolean;
  readonly className?: string;
}

export function QuantityDisplay({
  value,
  locale = currentIntlLocale(),
  decimals,
  hideUnit = false,
  className
}: QuantityDisplayProps): React.JSX.Element {
  const formatted = formatQuantity(value, {
    locale,
    decimals,
    hideUnit
  });

  return (
    <span
      className={clsx("ui-quantity-display", className)}
      style={{
        fontFamily: "var(--font-mono, monospace)",
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        whiteSpace: "nowrap"
      }}
    >
      {formatted}
    </span>
  );
}
