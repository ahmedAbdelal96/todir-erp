import React from "react";
import { formatDate, formatDateTime } from "../formatting/date-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";
import { useTranslation } from "react-i18next";
import { BidiText } from "../primitives/bidi-text.tsx";

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
  dateStyle,
  timeStyle = "short",
  className
}: DateTimeDisplayProps): React.JSX.Element {
  const { i18n } = useTranslation();
  const language = (locale ?? i18n.resolvedLanguage ?? "ar").toLowerCase().startsWith("ar") ? "ar" : "en";
  const formatted =
    type === "datetime"
      ? formatDateTime(value, { locale: locale ?? currentIntlLocale(), dateStyle: dateStyle ?? (language === "ar" ? "long" : "medium"), timeStyle })
      : formatDate(value, { locale: locale ?? currentIntlLocale(), dateStyle: dateStyle ?? (language === "ar" ? "long" : "medium") });
  const machineValue = value instanceof Date
    ? Number.isNaN(value.getTime()) ? undefined : value.toISOString()
    : typeof value === "string" && !Number.isNaN(Date.parse(value)) ? value : undefined;

  return (
    <time
      className={clsx("ui-datetime-display", className)}
      {...(machineValue && formatted !== "—" ? { dateTime: machineValue } : {})}
      style={{
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        whiteSpace: "nowrap",
        unicodeBidi: "isolate"
      }}
    >
      <BidiText lang={language} direction={language === "ar" ? "rtl" : "ltr"}>{formatted}</BidiText>
    </time>
  );
}
