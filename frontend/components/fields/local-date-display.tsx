import React from "react";
import { formatDate } from "../formatting/date-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface LocalDateDisplayProps {
  readonly value: string | Date | null | undefined;
  readonly locale?: string | undefined;
  readonly dateStyle?: "full" | "long" | "medium" | "short" | undefined;
  readonly className?: string | undefined;
}

export function LocalDateDisplay({
  value,
  locale = currentIntlLocale(),
  dateStyle = "medium",
  className
}: LocalDateDisplayProps): React.JSX.Element {
  const formatted = formatDate(value, { locale, dateStyle });

  return (
    <span
      className={clsx("ui-local-date-display", className)}
      style={{
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        whiteSpace: "nowrap"
      }}
    >
      {formatted}
    </span>
  );
}
