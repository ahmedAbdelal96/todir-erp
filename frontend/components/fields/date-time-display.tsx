import React from "react";
import { formatDate, formatDateTime } from "../formatting/date-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface DateTimeDisplayProps {
  readonly value: string | Date | null | undefined;
  readonly type?: "date" | "datetime" | undefined;
  readonly locale?: string | undefined;
  readonly dateStyle?: "full" | "long" | "medium" | "short" | undefined;
  readonly timeStyle?: "full" | "long" | "medium" | "short" | undefined;
  readonly className?: string | undefined;
}

export function DateTimeDisplay({
  value,
  type = "date",
  locale = currentIntlLocale(),
  dateStyle = "medium",
  timeStyle = "short",
  className
}: DateTimeDisplayProps): React.JSX.Element {
  const formatted =
    type === "datetime"
      ? formatDateTime(value, { locale, dateStyle, timeStyle })
      : formatDate(value, { locale, dateStyle });

  return (
    <span
      className={clsx("ui-datetime-display", className)}
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
