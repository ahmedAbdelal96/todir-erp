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
    <div>
      {/* Desktop Table */}
      <div className="ui-responsive-financial-desktop" style={{ overflowX: "auto" }}>
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

      {/* Mobile 390px View Cards */}
      <div className="ui-responsive-financial-mobile">
        {schedule.map((item) => (
          <div
            key={item.sequence}
            style={{
              backgroundColor: "var(--color-surface-card, #ffffff)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              borderRadius: "var(--radius-md, 0.5rem)",
              padding: "0.75rem",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              boxShadow: "var(--shadow-sm, 0 1px 2px rgba(0,0,0,0.05))"
            }}
          >
            <div>
              <div style={{ fontWeight: 700, fontSize: "0.8125rem", color: "var(--color-brand-primary, #102a43)" }}>
                {t("preview.installmentSequence")} #{item.sequence}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", marginTop: "0.125rem" }}>
                <LocalDateDisplay value={item.dueDate} />
              </div>
            </div>
            <div style={{ fontWeight: 800, fontSize: "0.9375rem", color: "var(--color-brand-primary, #102a43)" }}>
              <MoneyDisplay value={{ amount: item.amount, currency: item.currencyCode }} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
