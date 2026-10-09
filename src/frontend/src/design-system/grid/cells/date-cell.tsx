import React from "react";
import { DateTimeDisplay } from "../../fields/date-time-display.tsx";

export interface DateCellProps {
  readonly value: string | Date | null | undefined;
  readonly type?: "date" | "datetime" | undefined;
  readonly locale?: string | undefined;
}

export function DateCell({ value, type = "date", locale }: DateCellProps): React.JSX.Element {
  return <DateTimeDisplay value={value} type={type} locale={locale} />;
}
