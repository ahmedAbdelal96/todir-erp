import React from "react";
import { useTranslation } from "react-i18next";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import type { InvoiceScheduleLine } from "../types.ts";

export interface InvoiceScheduleTableProps {
  readonly schedule: readonly InvoiceScheduleLine[];
}

export function InvoiceScheduleTable({ schedule }: InvoiceScheduleTableProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  if (!schedule || schedule.length === 0) {
    return (
      <div style={{ padding: "1rem", textAlign: "center", color: "var(--color-content-muted, #94a3b8)", fontSize: "0.875rem" }}>
        {t("preview.noScheduleNotice")}
      </div>
    );
  }

  return (
    <div style={{ overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
        <thead>
          <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default, #cbd5e1)" }}>
            <th style={{ padding: "0.5rem 0.75rem", fontWeight: 700, color: "var(--color-content-secondary, #475569)" }}>
              {t("preview.installmentSequence")}
            </th>
            <th style={{ padding: "0.5rem 0.75rem", fontWeight: 700, color: "var(--color-content-secondary, #475569)" }}>
              {t("preview.dueDate")}
            </th>
            <th style={{ padding: "0.5rem 0.75rem", fontWeight: 700, color: "var(--color-content-secondary, #475569)", textAlign: "end" }}>
              {t("preview.installmentAmount")}
            </th>
          </tr>
        </thead>
        <tbody>
          {schedule.map((item) => (
            <tr key={item.sequence} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
              <td style={{ padding: "0.5rem 0.75rem", fontWeight: 600 }}>
                {t("preview.installmentSequence")} #{item.sequence}
              </td>
              <td style={{ padding: "0.5rem 0.75rem" }}>
                <LocalDateDisplay value={item.dueDate} />
              </td>
              <td style={{ padding: "0.5rem 0.75rem", textAlign: "end", fontWeight: 700 }}>
                <MoneyDisplay value={{ amount: item.amount, currency: item.currencyCode }} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
