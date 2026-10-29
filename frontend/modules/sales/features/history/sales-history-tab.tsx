import React from "react";
import { useTranslation } from "react-i18next";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import type { QuotationListItem } from "../../types.ts";

export interface SalesHistoryTabProps {
  readonly quotations: readonly QuotationListItem[];
  readonly isLoading: boolean;
  readonly onSelectQuotation: (id: string) => void;
}

export function SalesHistoryTab({
  quotations,
  isLoading,
  onSelectQuotation
}: SalesHistoryTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <div>
        <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700 }}>
          {t("history.title")}
        </h3>
        <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("history.subtitle")}
        </p>
      </div>

      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.25rem 1.5rem",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
        }}
      >
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : quotations.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("history.emptyTimeline")}
          </div>
        ) : (
          <SalesDataTable minWidth="800px">
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("quotations.number")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("quotations.customer")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("history.effectiveDate")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("history.commercialState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "end" }}>{t("quotations.total")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "center" }}>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {quotations.map((q) => (
                  <tr key={q.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, fontFamily: "monospace", fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
                      {q.number}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesCustomerDisplay partyId={q.partyId} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                      {q.quoteDate}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesStatusBadge kind="quotationState" status={q.state} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem" }}>
                      {q.grossTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} {q.currencyCode}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <button
                        type="button"
                        onClick={() => onSelectQuotation(q.id)}
                        style={{
                          background: "transparent",
                          border: "1px solid var(--color-border-default)",
                          padding: "0.375rem 0.75rem",
                          borderRadius: "var(--radius-sm)",
                          cursor: "pointer",
                          fontSize: "0.8125rem"
                        }}
                      >
                        {t("common.details")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </SalesDataTable>
        )}
      </div>
    </div>
  );
}
