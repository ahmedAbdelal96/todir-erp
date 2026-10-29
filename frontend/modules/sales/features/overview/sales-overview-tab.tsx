import React from "react";
import { useTranslation } from "react-i18next";
import { SalesKpiCard } from "../../components/sales-kpi-card.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import type { QuotationListItem } from "../../types.ts";

export interface SalesOverviewTabProps {
  readonly quotations: readonly QuotationListItem[];
  readonly isLoading: boolean;
  readonly onSelectQuotation: (id: string) => void;
  readonly onNewQuotation: () => void;
}

export function SalesOverviewTab({
  quotations,
  isLoading,
  onSelectQuotation,
  onNewQuotation
}: SalesOverviewTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  const openQuotes = quotations.filter((q) => q.state === "Open");
  const acceptedQuotes = quotations.filter((q) => q.state === "Accepted");
  const convertedQuotes = quotations.filter((q) => q.state === "Converted");
  const uniqueCustomersCount = new Set(quotations.map((q) => q.partyId)).size;

  const totalAcceptedValue = acceptedQuotes.reduce((acc, q) => acc + q.grossTotal, 0);

  const recentQuotations = quotations.slice(0, 8);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Primary Action Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-primary, #0f172a)" }}>
            {t("overview.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
            {t("overview.subtitle")}
          </p>
        </div>

        <Button
          variant="primary"
          onPress={onNewQuotation}
          type="button"
        >
          {t("quotations.newQuotation")}
        </Button>
      </div>

      {/* KPI Cards Strip */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <SalesKpiCard
          title={t("overview.awaitingDecision")}
          value={openQuotes.length}
          subtitle={t("overview.awaitingDecisionSubtitle")}
          tone="primary"
        />
        <SalesKpiCard
          title={t("overview.acceptedThisPeriod")}
          value={totalAcceptedValue > 0 ? `${totalAcceptedValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${quotations[0]?.currencyCode || "EGP"}` : String(acceptedQuotes.length)}
          subtitle={t("overview.acceptedThisPeriodSubtitle", { count: acceptedQuotes.length })}
          tone="success"
        />
        <SalesKpiCard
          title={t("overview.convertedToInvoices")}
          value={convertedQuotes.length}
          subtitle={t("overview.convertedToInvoicesSubtitle")}
          tone="default"
        />
        <SalesKpiCard
          title={t("overview.activeCustomers")}
          value={uniqueCustomersCount}
          subtitle={t("overview.activeCustomersSubtitle")}
          tone="default"
        />
      </div>

      {/* Recent Activity Table Card */}
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
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h3 style={{ fontSize: "1rem", fontWeight: 600, margin: 0, color: "var(--color-content-primary, #0f172a)" }}>
            {t("overview.recentQuotations")}
          </h3>
          <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
            {t("overview.showingCount", { count: recentQuotations.length })}
          </span>
        </div>

        {isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : recentQuotations.length === 0 ? (
          <div style={{ padding: "2.5rem 1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            <p style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem" }}>{t("overview.emptyState")}</p>
            <Button variant="secondary" onPress={onNewQuotation} type="button">
              {t("quotations.newQuotation")}
            </Button>
          </div>
        ) : (
          <SalesDataTable minWidth="800px">
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-default, #cbd5e1)", backgroundColor: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                    {t("quotations.number")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                    {t("quotations.customer")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                    {t("quotations.quoteDate")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
                    {t("quotations.status")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)", textAlign: "end" }}>
                    {t("quotations.total")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)", textAlign: "center" }}>
                    {t("common.actions")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {recentQuotations.map((q) => (
                  <tr
                    key={q.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle, #f1f5f9)",
                      transition: "background-color 0.15s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, fontFamily: "monospace", fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
                      {q.number}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesCustomerDisplay partyId={q.partyId} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                      <LocalDateDisplay value={q.quoteDate} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <SalesStatusBadge kind="quotationState" status={q.state} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600, fontSize: "0.875rem" }}>
                      {q.grossTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} {q.currencyCode}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <Button
                        variant="secondary"
                        onPress={() => onSelectQuotation(q.id)}
                        type="button"
                      >
                        {t("common.details")}
                      </Button>
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
