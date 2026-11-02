import React, { useState } from "react";
import { useOwnerFinancialSummary } from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { useAccountingI18n } from "../../i18n/index.ts";

export interface OwnerFinancialViewProps {
  readonly legalEntityId?: string;
  readonly onNavigateTab?: (tab: string) => void;
}

export const OwnerFinancialView: React.FC<OwnerFinancialViewProps> = ({ legalEntityId, onNavigateTab }) => {
  const { t } = useAccountingI18n();
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));

  const { data: summary, isLoading, isError, refetch } = useOwnerFinancialSummary(asOfDate, legalEntityId);

  const getHealthBadge = (health?: string) => {
    switch (health) {
      case "Healthy":
        return <StatusBadge intent="success" label={t("owner.healthHealthy")} />;
      case "Attention":
        return <StatusBadge intent="warning" label={t("owner.healthAttention")} />;
      case "Blocking":
      default:
        return <StatusBadge intent="danger" label={t("owner.healthBlocking")} />;
    }
  };

  const kpiCardStyle: React.CSSProperties = {
    backgroundColor: "var(--color-surface-card, #ffffff)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    borderRadius: "var(--radius-lg, 0.75rem)",
    boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
    padding: "1.25rem",
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
    minWidth: 0,
    transition: "box-shadow 150ms ease, transform 150ms ease"
  };

  const quickActionCardStyle: React.CSSProperties = {
    backgroundColor: "var(--color-surface-subtle, #f8fafc)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    borderRadius: "var(--radius-md, 0.5rem)",
    padding: "1rem",
    textAlign: "start",
    cursor: "pointer",
    display: "flex",
    flexDirection: "column",
    gap: "0.25rem",
    transition: "background-color 150ms ease, border-color 150ms ease, transform 150ms ease"
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="owner-financial-summary">
      {/* Header and Filter Control Bar */}
      <Surface
        variant="card"
        style={{
          padding: "1.25rem 1.5rem"
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "1rem"
          }}
        >
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {t("owner.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("owner.subtitle")}
            </p>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            <label htmlFor="owner-as-of-date" style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-secondary, #334155)" }}>
              {t("owner.asOfDateLabel")}:
            </label>
            <input
              id="owner-as-of-date"
              type="date"
              value={asOfDate}
              onChange={(e) => setAsOfDate(e.target.value)}
              style={{
                fontSize: "0.875rem",
                padding: "0.375rem 0.625rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                color: "var(--color-content-primary, #0f172a)"
              }}
            />
            <Button variant="secondary" size="compact" onPress={() => void refetch()}>
              {t("owner.refresh")}
            </Button>
          </div>
        </div>
      </Surface>

      {/* Loading / Error / Content */}
      {isLoading ? (
        <Surface variant="card" style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
          <p>{t("owner.loading")}</p>
        </Surface>
      ) : isError || !summary ? (
        <Surface variant="card" style={{ padding: "1.5rem", border: "1px solid var(--color-status-danger, #dc2626)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)" }}>
          <p style={{ margin: 0, color: "var(--color-status-danger, #dc2626)", fontWeight: 600 }}>{t("owner.errorLoading")}</p>
        </Surface>
      ) : (
        <>
          {/* Health Summary Banner */}
          <Surface
            variant="card"
            style={{
              padding: "1rem 1.5rem",
              backgroundColor: summary.health === "Blocking" ? "var(--color-status-danger-subtle, #fef2f2)" : summary.health === "Attention" ? "var(--color-status-warning-subtle, #fffbeb)" : "var(--color-surface-subtle, #f8fafc)",
              border: summary.health === "Blocking" ? "1px solid var(--color-status-danger, #ef4444)" : summary.health === "Attention" ? "1px solid var(--color-status-warning, #f59e0b)" : "1px solid var(--color-border-default, #cbd5e1)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: "1rem"
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                {t("owner.integrityStatus")}:
              </span>
              {getHealthBadge(summary.health)}
              {summary.health !== "Healthy" && onNavigateTab && (
                <Button variant="ghost" size="compact" onPress={() => onNavigateTab("health")}>
                  عرض تفاصيل العوائق في مركز السلامة المالية ←
                </Button>
              )}
            </div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", fontFamily: "var(--font-mono, monospace)" }}>
              {t("owner.lastUpdated")}: {summary.asOfDate} ({summary.currencyCode})
            </span>
          </Surface>

          {/* 5 Executive KPI Cards Grid */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
              gap: "1.25rem",
              width: "100%",
              boxSizing: "border-box"
            }}
          >
            {/* 1. Cash & Bank */}
            <div style={kpiCardStyle} data-testid="owner-kpi-cash">
              <div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                    {t("owner.cashBook")}
                  </span>
                  <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", color: "var(--color-status-info, #0284c7)", fontSize: "0.75rem", fontWeight: 700 }}>
                    1102
                  </span>
                </div>
                <div style={{ margin: "0.5rem 0" }}>
                  <MoneyDisplay
                    value={{ amount: summary.cashBookValue.toFixed(2), currency: summary.currencyCode }}
                    size="large"
                  />
                </div>
              </div>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("owner.cashBookDesc")}
              </p>
            </div>

            {/* 2. Receivables */}
            <div style={kpiCardStyle} data-testid="owner-kpi-receivables">
              <div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                    {t("owner.receivables")}
                  </span>
                  <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", color: "var(--color-status-info, #0284c7)", fontSize: "0.75rem", fontWeight: 700 }}>
                    AR
                  </span>
                </div>
                <div style={{ margin: "0.5rem 0" }}>
                  <MoneyDisplay
                    value={{ amount: summary.receivables.toFixed(2), currency: summary.currencyCode }}
                    size="large"
                  />
                </div>
              </div>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("owner.receivablesDesc")}
              </p>
            </div>

            {/* 3. Payables */}
            <div style={kpiCardStyle} data-testid="owner-kpi-payables">
              <div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                    {t("owner.payables")}
                  </span>
                  <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-warning-subtle, #fffbeb)", color: "var(--color-status-warning, #d97706)", fontSize: "0.75rem", fontWeight: 700 }}>
                    AP
                  </span>
                </div>
                <div style={{ margin: "0.5rem 0" }}>
                  <MoneyDisplay
                    value={{ amount: summary.payables.toFixed(2), currency: summary.currencyCode }}
                    size="large"
                  />
                </div>
              </div>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("owner.payablesDesc")}
              </p>
            </div>

            {/* 4. Inventory */}
            <div style={kpiCardStyle} data-testid="owner-kpi-inventory">
              <div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                    {t("owner.inventoryValue")}
                  </span>
                  <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", color: "var(--color-status-info, #0284c7)", fontSize: "0.75rem", fontWeight: 700 }}>
                    1104
                  </span>
                </div>
                <div style={{ margin: "0.5rem 0" }}>
                  {summary.inventoryValue !== null && summary.inventoryValue !== undefined ? (
                    <MoneyDisplay
                      value={{ amount: summary.inventoryValue.toFixed(2), currency: summary.currencyCode }}
                      size="large"
                    />
                  ) : (
                    <span style={{ fontSize: "1.125rem", color: "var(--color-content-muted, #64748b)" }}>
                      {t("owner.notAvailable")}
                    </span>
                  )}
                </div>
              </div>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("owner.inventoryValueDesc")}
              </p>
            </div>

            {/* 5. Profit */}
            <div style={kpiCardStyle} data-testid="owner-kpi-profit">
              <div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                  <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                    {t("owner.profit")}
                  </span>
                  <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-success-subtle, #ecfdf5)", color: "var(--color-status-success, #059669)", fontSize: "0.75rem", fontWeight: 700 }}>
                    P&L
                  </span>
                </div>
                <div style={{ margin: "0.5rem 0" }}>
                  <MoneyDisplay
                    value={{ amount: summary.profit.toFixed(2), currency: summary.currencyCode }}
                    size="large"
                    coloredSign
                  />
                </div>
              </div>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("owner.profitDesc")}
              </p>
            </div>
          </div>

          {/* Quick Deep Drilldown Navigation Links */}
          <Surface
            variant="card"
            title={t("owner.quickLinksTitle")}
            style={{ padding: "1.25rem 1.5rem" }}
          >
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                gap: "1rem"
              }}
            >
              <button
                type="button"
                style={quickActionCardStyle}
                onClick={() => onNavigateTab?.("financialStatements")}
              >
                <strong style={{ fontSize: "0.9375rem", color: "var(--color-brand-primary, #102a43)" }}>
                  {t("owner.linkIncomeStatement")} →
                </strong>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("owner.linkIncomeStatementDesc")}
                </span>
              </button>

              <button
                type="button"
                style={quickActionCardStyle}
                onClick={() => onNavigateTab?.("financialStatements")}
              >
                <strong style={{ fontSize: "0.9375rem", color: "var(--color-brand-primary, #102a43)" }}>
                  {t("owner.linkBalanceSheet")} →
                </strong>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("owner.linkBalanceSheetDesc")}
                </span>
              </button>

              <button
                type="button"
                style={quickActionCardStyle}
                onClick={() => onNavigateTab?.("controlReconciliation")}
              >
                <strong style={{ fontSize: "0.9375rem", color: "var(--color-brand-primary, #102a43)" }}>
                  {t("owner.linkReconciliation")} →
                </strong>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("owner.linkReconciliationDesc")}
                </span>
              </button>

              <button
                type="button"
                style={quickActionCardStyle}
                onClick={() => onNavigateTab?.("entries")}
              >
                <strong style={{ fontSize: "0.9375rem", color: "var(--color-brand-primary, #102a43)" }}>
                  {t("owner.linkEntries")} →
                </strong>
                <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("owner.linkEntriesDesc")}
                </span>
              </button>
            </div>
          </Surface>
        </>
      )}
    </div>
  );
};
