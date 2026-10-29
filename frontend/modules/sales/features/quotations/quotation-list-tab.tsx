import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import type { QuotationListItem } from "../../types.ts";

export interface QuotationListTabProps {
  readonly quotations: readonly QuotationListItem[];
  readonly isLoading: boolean;
  readonly totalCount: number;
  readonly page: number;
  readonly onPageChange: (newPage: number) => void;
  readonly onSelectQuotation: (id: string) => void;
  readonly onNewQuotation: () => void;
  readonly stateFilter: string | null;
  readonly onStateFilterChange: (state: string | null) => void;
}

export function QuotationListTab({
  quotations,
  isLoading,
  totalCount,
  page,
  onPageChange,
  onSelectQuotation,
  onNewQuotation,
  stateFilter,
  onStateFilterChange
}: QuotationListTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const [searchQuery, setSearchQuery] = useState("");

  const filteredQuotes = quotations.filter((q) => {
    if (stateFilter && q.state !== stateFilter) return false;
    if (!searchQuery.trim()) return true;
    const qTerm = searchQuery.toLowerCase();
    return q.number.toLowerCase().includes(qTerm) || q.partyId.toLowerCase().includes(qTerm);
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header with Search and State Filter Tabs */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem"
        }}
      >
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ width: "260px" }}>
            <SearchField
              value={searchQuery}
              onChange={setSearchQuery}
              placeholder={t("quotations.searchPlaceholder")}
              ariaLabel={t("quotations.title")}
            />
          </div>

          <div style={{ display: "flex", gap: "0.25rem", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", padding: "0.25rem", borderRadius: "var(--radius-md)" }}>
            <button
              type="button"
              onClick={() => onStateFilterChange(null)}
              style={{
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-sm)",
                border: "none",
                background: stateFilter === null ? "#ffffff" : "transparent",
                fontWeight: stateFilter === null ? 600 : 500,
                color: stateFilter === null ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                cursor: "pointer",
                fontSize: "0.8125rem",
                boxShadow: stateFilter === null ? "0 1px 2px rgba(0,0,0,0.05)" : "none"
              }}
            >
              {t("common.all")}
            </button>
            <button
              type="button"
              onClick={() => onStateFilterChange("Draft")}
              style={{
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-sm)",
                border: "none",
                background: stateFilter === "Draft" ? "#ffffff" : "transparent",
                fontWeight: stateFilter === "Draft" ? 600 : 500,
                color: stateFilter === "Draft" ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                cursor: "pointer",
                fontSize: "0.8125rem",
                boxShadow: stateFilter === "Draft" ? "0 1px 2px rgba(0,0,0,0.05)" : "none"
              }}
            >
              {t("status.draft")}
            </button>
            <button
              type="button"
              onClick={() => onStateFilterChange("Open")}
              style={{
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-sm)",
                border: "none",
                background: stateFilter === "Open" ? "#ffffff" : "transparent",
                fontWeight: stateFilter === "Open" ? 600 : 500,
                color: stateFilter === "Open" ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                cursor: "pointer",
                fontSize: "0.8125rem",
                boxShadow: stateFilter === "Open" ? "0 1px 2px rgba(0,0,0,0.05)" : "none"
              }}
            >
              {t("status.open")}
            </button>
            <button
              type="button"
              onClick={() => onStateFilterChange("Accepted")}
              style={{
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-sm)",
                border: "none",
                background: stateFilter === "Accepted" ? "#ffffff" : "transparent",
                fontWeight: stateFilter === "Accepted" ? 600 : 500,
                color: stateFilter === "Accepted" ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                cursor: "pointer",
                fontSize: "0.8125rem",
                boxShadow: stateFilter === "Accepted" ? "0 1px 2px rgba(0,0,0,0.05)" : "none"
              }}
            >
              {t("status.accepted")}
            </button>
          </div>
        </div>

        <Button
          variant="primary"
          onPress={onNewQuotation}
          type="button"
        >
          {t("quotations.newQuotation")}
        </Button>
      </div>

      {/* Main Table Card */}
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
        ) : filteredQuotes.length === 0 ? (
          <div style={{ padding: "3rem 1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            <p style={{ margin: "0 0 1rem", fontSize: "0.9375rem" }}>{t("quotations.noQuotationsFound")}</p>
            <Button variant="secondary" onPress={onNewQuotation} type="button">
              {t("quotations.newQuotation")}
            </Button>
          </div>
        ) : (
          <>
            <SalesDataTable minWidth="900px">
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
                      {t("quotations.validUntil")}
                    </th>
                    <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)", textAlign: "center" }}>
                      {t("quotations.revision")}
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
                  {filteredQuotes.map((q) => (
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
                        {q.quoteDate}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                        {q.validUntil ?? "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        <span style={{ backgroundColor: "var(--color-surface-subtle)", padding: "0.125rem 0.375rem", borderRadius: "var(--radius-sm)", fontSize: "0.75rem", fontWeight: 600 }}>
                          {t("common.revisionPrefix", { version: q.currentRevisionNumber })}
                        </span>
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
                          {t("common.manage")}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </SalesDataTable>

            {/* Pagination Strip */}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", paddingTop: "0.5rem" }}>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("common.page")} {page} · {totalCount} {t("quotations.title")}
              </span>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <Button
                  variant="secondary"
                  onPress={() => onPageChange(page - 1)}
                  isDisabled={page <= 1}
                  type="button"
                >
                  {t("common.previous")}
                </Button>
                <Button
                  variant="secondary"
                  onPress={() => onPageChange(page + 1)}
                  isDisabled={filteredQuotes.length < 50}
                  type="button"
                >
                  {t("common.next")}
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
