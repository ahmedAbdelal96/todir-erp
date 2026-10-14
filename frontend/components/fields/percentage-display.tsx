import React from "react";
import { formatPercentage } from "../formatting/quantity-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface PercentageDisplayProps {
  readonly value: string | number | null | undefined;
  readonly locale?: string | undefined;
  readonly decimals?: number | undefined;
  readonly className?: string | undefined;
}

export function PercentageDisplay({
  value,
  locale = currentIntlLocale(),
  decimals = 2,
  className
}: PercentageDisplayProps): React.JSX.Element {
  const formatted = formatPercentage(value, { locale, decimals });

  return (
    <span
      className={clsx("ui-percentage-display", className)}
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
