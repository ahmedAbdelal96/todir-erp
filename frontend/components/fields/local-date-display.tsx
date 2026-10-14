import React from "react";
import { formatDate } from "../formatting/date-formatter.ts";
import clsx from "clsx";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";
import { useTranslation } from "react-i18next";
import { BidiText } from "../primitives/bidi-text.tsx";

export interface LocalDateDisplayProps {
  readonly value: string | Date | null | undefined;
  readonly locale?: string | undefined;
  readonly dateStyle?: "full" | "long" | "medium" | "short" | undefined;
  readonly className?: string | undefined;
}

export function LocalDateDisplay({
  value,
  locale = currentIntlLocale(),
  dateStyle,
  className
}: LocalDateDisplayProps): React.JSX.Element {
  const { i18n } = useTranslation();
  const resolvedLocale = locale ?? currentIntlLocale();
  const language = (locale ?? i18n.resolvedLanguage ?? "ar").toLowerCase().startsWith("ar") ? "ar" : "en";
  const formatted = formatDate(value, { locale: resolvedLocale, dateStyle: dateStyle ?? (language === "ar" ? "long" : "medium") });
  const machineValue = value instanceof Date
    ? Number.isNaN(value.getTime()) ? undefined : value.toISOString()
    : typeof value === "string" && !Number.isNaN(Date.parse(value)) ? value : undefined;

  return (
    <time
      className={clsx("ui-local-date-display", className)}
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
