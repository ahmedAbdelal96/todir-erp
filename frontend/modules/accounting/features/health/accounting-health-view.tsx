import React, { useState } from "react";
import { useAccountingHealth } from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { useAccountingI18n } from "../../i18n/index.ts";
import type { AccountingHealthItem } from "../../types.ts";
import {
  formatHealthCode,
  formatSeverity,
  formatStatus,
  formatDirection,
  formatDiagnosticMessage
} from "../../utils/diagnostic-translations.ts";

export interface AccountingHealthViewProps {
  readonly legalEntityId?: string;
  readonly onNavigateTab?: (tab: string) => void;
}

export const AccountingHealthViewComponent: React.FC<AccountingHealthViewProps> = ({ legalEntityId, onNavigateTab }) => {
  const { t } = useAccountingI18n();
  const [filterSeverity, setFilterSeverity] = useState<string>("ALL");

  const { data: health, isLoading, isError, refetch } = useAccountingHealth(undefined, legalEntityId);

  const getSeverityBadge = (severity: string) => {
    switch (severity) {
      case "Blocking":
        return <StatusBadge intent="danger" label={formatSeverity("Blocking")} size="small" />;
      case "Attention":
        return <StatusBadge intent="warning" label={formatSeverity("Attention")} size="small" />;
      case "Info":
      default:
        return <StatusBadge intent="success" label={formatSeverity("Info")} size="small" />;
    }
  };

  const getOverallStatusBadge = (status?: string) => {
    switch (status) {
      case "Healthy":
        return <StatusBadge intent="success" label={formatStatus("Healthy")} size="medium" />;
      case "Attention":
        return <StatusBadge intent="warning" label={formatSeverity("Attention")} size="medium" />;
      case "Blocking":
      default:
        return <StatusBadge intent="danger" label={formatSeverity("Blocking")} size="medium" />;
    }
  };

  const statBoxStyle: React.CSSProperties = {
    backgroundColor: "var(--color-surface-card, #ffffff)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    borderRadius: "var(--radius-lg, 0.75rem)",
    boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
    padding: "1.25rem",
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: "1rem"
  };

  const filterBtnStyle = (active: boolean): React.CSSProperties => ({
    padding: "0.375rem 0.875rem",
    borderRadius: "var(--radius-md, 0.5rem)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    fontSize: "0.8125rem",
    fontWeight: 600,
    cursor: "pointer",
    backgroundColor: active ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-card, #ffffff)",
    color: active ? "#ffffff" : "var(--color-content-primary, #0f172a)",
    transition: "background-color 150ms ease, color 150ms ease"
  });

  const filteredItems = (health?.items ?? []).filter((item: AccountingHealthItem) => {
    if (filterSeverity === "ALL") return true;
    return item.severity === filterSeverity;
  });

  const blockingCount = (health?.items ?? []).filter((i: AccountingHealthItem) => i.severity === "Blocking").length;
  const attentionCount = (health?.items ?? []).filter((i: AccountingHealthItem) => i.severity === "Attention").length;
  const readyCount = (health?.items ?? []).filter((i: AccountingHealthItem) => i.severity === "Info").length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="accounting-health-center">
      {/* Header and Re-check Banner */}
      <Surface variant="card" style={{ padding: "1.25rem 1.5rem" }}>
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
              {t("health.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("health.subtitle")}
            </p>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "1rem", flexWrap: "wrap" }}>
            {getOverallStatusBadge(health?.status)}
            <Button variant="secondary" size="compact" onPress={() => void refetch()}>
              {t("health.recheck")}
            </Button>
          </div>
        </div>
      </Surface>

      {/* Loading / Error States */}
      {isLoading ? (
        <Surface variant="card" style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
          <p>{t("health.loading")}</p>
        </Surface>
      ) : isError || !health ? (
        <Surface variant="card" style={{ padding: "1.5rem", border: "1px solid var(--color-status-danger, #dc2626)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)" }}>
          <p style={{ margin: 0, color: "var(--color-status-danger, #dc2626)", fontWeight: 600 }}>{t("health.errorLoading")}</p>
        </Surface>
      ) : (
        <>
          {/* Status Metrics Cards Grid */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
              gap: "1.25rem"
            }}
          >
            {/* Blocking Count */}
            <div style={statBoxStyle}>
              <div>
                <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                  {t("health.countBlocking")}
                </span>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("health.blockingDesc")}
                </p>
              </div>
              <span
                style={{
                  fontSize: "1.5rem",
                  fontWeight: 800,
                  fontFamily: "var(--font-mono, monospace)",
                  color: blockingCount > 0 ? "var(--color-status-danger, #dc2626)" : "var(--color-content-muted, #64748b)",
                  padding: "0.25rem 0.75rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: blockingCount > 0 ? "var(--color-status-danger-subtle, #fef2f2)" : "var(--color-surface-subtle, #f8fafc)",
                  border: `1px solid ${blockingCount > 0 ? "var(--color-status-danger, #dc2626)" : "var(--color-border-subtle, #e2e8f0)"}`
                }}
              >
                {blockingCount}
              </span>
            </div>

            {/* Attention Count */}
            <div style={statBoxStyle}>
              <div>
                <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                  {t("health.countAttention")}
                </span>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("health.attentionDesc")}
                </p>
              </div>
              <span
                style={{
                  fontSize: "1.5rem",
                  fontWeight: 800,
                  fontFamily: "var(--font-mono, monospace)",
                  color: attentionCount > 0 ? "var(--color-status-warning, #d97706)" : "var(--color-content-muted, #64748b)",
                  padding: "0.25rem 0.75rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: attentionCount > 0 ? "var(--color-status-warning-subtle, #fffbeb)" : "var(--color-surface-subtle, #f8fafc)",
                  border: `1px solid ${attentionCount > 0 ? "var(--color-status-warning, #d97706)" : "var(--color-border-subtle, #e2e8f0)"}`
                }}
              >
                {attentionCount}
              </span>
            </div>

            {/* Ready Count */}
            <div style={statBoxStyle}>
              <div>
                <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)" }}>
                  {t("health.countReady")}
                </span>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                  {t("health.readyDesc")}
                </p>
              </div>
              <span
                style={{
                  fontSize: "1.5rem",
                  fontWeight: 800,
                  fontFamily: "var(--font-mono, monospace)",
                  color: "var(--color-status-success, #059669)",
                  padding: "0.25rem 0.75rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
                  border: "1px solid var(--color-status-success, #059669)"
                }}
              >
                {readyCount}
              </span>
            </div>
          </div>

          {/* Diagnostic Invariants List */}
          <Surface
            variant="card"
            title={t("health.diagnosticItemsTitle")}
            headerActions={
              <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                <button
                  type="button"
                  style={filterBtnStyle(filterSeverity === "ALL")}
                  onClick={() => setFilterSeverity("ALL")}
                >
                  {t("health.filterAll")} ({health.items.length})
                </button>
                <button
                  type="button"
                  style={filterBtnStyle(filterSeverity === "Blocking")}
                  onClick={() => setFilterSeverity("Blocking")}
                >
                  {t("health.filterBlocking")} ({blockingCount})
                </button>
                <button
                  type="button"
                  style={filterBtnStyle(filterSeverity === "Attention")}
                  onClick={() => setFilterSeverity("Attention")}
                >
                  {t("health.filterAttention")} ({attentionCount})
                </button>
                <button
                  type="button"
                  style={filterBtnStyle(filterSeverity === "Info")}
                  onClick={() => setFilterSeverity("Info")}
                >
                  {t("health.filterInfo")} ({readyCount})
                </button>
              </div>
            }
            style={{ padding: "0" }}
          >
            {filteredItems.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                <p style={{ margin: 0 }}>{t("health.noItemsFound")}</p>
              </div>
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                  <thead>
                    <tr
                      style={{
                        backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                        borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                        color: "var(--color-content-secondary, #334155)",
                        fontSize: "0.8125rem",
                        fontWeight: 700
                      }}
                    >
                      <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>مستوى الأهمية</th>
                      <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>الحالة التشغيلية</th>
                      <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>الفحص والتحقق الرقابي</th>
                      <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>البيان والتفاصيل التشغيلية</th>
                      <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredItems.map((item: AccountingHealthItem, idx: number) => (
                      <tr
                        key={item.code}
                        style={{
                          borderBottom: idx < filteredItems.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                          backgroundColor: idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)"
                        }}
                      >
                        <td style={{ padding: "0.75rem 1rem", whiteSpace: "nowrap" }}>
                          {getSeverityBadge(item.severity)}
                        </td>
                        <td style={{ padding: "0.75rem 1rem", whiteSpace: "nowrap", fontSize: "0.8125rem", fontWeight: 600 }}>
                          {formatStatus(item.status)}
                        </td>
                        <td style={{ padding: "0.75rem 1rem" }}>
                          <span style={{ fontSize: "0.8125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)", display: "block" }}>
                            {formatHealthCode(item.code)}
                          </span>
                          <span style={{ fontFamily: "var(--font-mono, monospace)", fontSize: "0.6875rem", color: "var(--color-content-muted, #64748b)" }}>
                            <bdi>{item.code}</bdi>{item.source ? ` • ${formatDirection(item.source)}` : ""}
                          </span>
                        </td>
                        <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", color: "var(--color-content-primary, #0f172a)", lineHeight: 1.5 }}>
                          {formatDiagnosticMessage(item.message)}
                        </td>
                        <td style={{ padding: "0.75rem 1rem", textAlign: "end", whiteSpace: "nowrap" }}>
                          {item.code.includes("reconciliation") ? (
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => onNavigateTab?.("controlReconciliation")}
                            >
                              {t("health.actionReconcile")}
                            </Button>
                          ) : item.code.includes("trial_balance") || item.code.includes("statement") ? (
                            <Button
                              variant="secondary"
                              size="compact"
                              onPress={() => onNavigateTab?.("financialStatements")}
                            >
                              {t("health.actionViewStatements")}
                            </Button>
                          ) : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Surface>
        </>
      )}
    </div>
  );
};
