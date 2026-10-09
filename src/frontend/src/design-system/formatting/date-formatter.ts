export interface FormatDateOptions {
  readonly locale?: string;
  readonly dateStyle?: "full" | "long" | "medium" | "short";
}

export interface FormatDateTimeOptions {
  readonly locale?: string;
  readonly dateStyle?: "full" | "long" | "medium" | "short";
  readonly timeStyle?: "full" | "long" | "medium" | "short";
  readonly timeZone?: string;
}

/**
 * Formats a calendar date (LocalDate or ISO string) safely without unexpected timezone shifts.
 */
export function formatDate(
  value: string | Date | null | undefined,
  options: FormatDateOptions = {}
): string {
  if (!value) return "—";

  const { locale = "en-US", dateStyle = "medium" } = options;

  let date: Date;
  if (typeof value === "string") {
    // If format is YYYY-MM-DD, parse as local calendar date to avoid UTC midnight offset shifts
    if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
      const [year, month, day] = value.split("-").map(Number);
      date = new Date(year!, month! - 1, day!);
    } else {
      date = new Date(value);
    }
  } else {
    date = value;
  }

  if (isNaN(date.getTime())) return "—";

  return new Intl.DateTimeFormat(locale, { dateStyle }).format(date);
}

/**
 * Formats a timestamp (Instant or ISO string) into localized date and time.
 */
export function formatDateTime(
  value: string | Date | null | undefined,
  options: FormatDateTimeOptions = {}
): string {
  if (!value) return "—";

  const {
    locale = "en-US",
    dateStyle = "medium",
    timeStyle = "short",
    timeZone
  } = options;

  const date = typeof value === "string" ? new Date(value) : value;
  if (isNaN(date.getTime())) return "—";

  return new Intl.DateTimeFormat(locale, {
    dateStyle,
    timeStyle,
    ...(timeZone ? { timeZone } : {})
  }).format(date);
}
