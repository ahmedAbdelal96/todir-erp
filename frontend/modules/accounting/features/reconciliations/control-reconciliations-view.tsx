import React, { useState } from "react";
import {
  useReconciliationsList,
  useReconciliationIssues,
  useRunReconciliationMutation
} from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { useAccountingI18n } from "../../i18n/index.ts";
import type { SubledgerReconciliationView, ReconciliationIssueView } from "../../types.ts";

export interface ControlReconciliationsViewProps {
  readonly legalEntityId?: string;
}

export const ControlReconciliationsView: React.FC<ControlReconciliationsViewProps> = ({ legalEntityId }) => {
  const { t } = useAccountingI18n();

  // Modal state
  const [isRunModalOpen, setIsRunModalOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<"AR" | "AP" | "Inventory">("AR");
  const [asOfDate, setAsOfDate] = useState<string>(() => new Date().toISOString().slice(0, 10));

  // Queries
  const {
    data: reconciliations = [],
    isLoading: isListLoading,
    refetch: refetchList
  } = useReconciliationsList({ limit: 50 }, legalEntityId);

  const {
    data: issues = [],
    isLoading: isIssuesLoading,
    refetch: refetchIssues
  } = useReconciliationIssues({ limit: 50 }, legalEntityId);

  // Mutation
  const runMutation = useRunReconciliationMutation(legalEntityId);

  const handleOpenRunModal = (type: "AR" | "AP" | "Inventory") => {
    setSelectedType(type);
    setIsRunModalOpen(true);
  };

  const handleExecuteRun = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await runMutation.mutateAsync({ type: selectedType, asOf: asOfDate });
      setIsRunModalOpen(false);
      void refetchList();
      void refetchIssues();
    } catch {
      // Handled by runMutation.isError
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Reconciled":
        return <StatusBadge intent="success" label={t("reconciliation.statusReconciled")} size="small" />;
      case "Difference":
        return <StatusBadge intent="danger" label={t("reconciliation.statusDifference")} size="small" />;
      case "NotReady":
      default:
        return <StatusBadge intent="warning" label={t("reconciliation.statusNotReady")} size="small" />;
    }
  };

  const cardActionStyle: React.CSSProperties = {
    backgroundColor: "var(--color-surface-card, #ffffff)",
    border: "1px solid var(--color-border-default, #cbd5e1)",
    borderRadius: "var(--radius-lg, 0.75rem)",
    boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
    padding: "1.25rem",
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
    gap: "1rem"
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="control-reconciliations-view">
      {/* Top Header Card */}
      <Surface variant="card" style={{ padding: "1.25rem 1.5rem" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
              {t("reconciliation.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("reconciliation.subtitle")}
            </p>
          </div>

          <Button variant="secondary" size="compact" onPress={() => void refetchList()}>
            {t("reconciliation.refresh")}
          </Button>
        </div>
      </Surface>

      {/* 3 Operational Reconciliation Action Cards */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "1.25rem"
        }}
      >
        {/* AR Card */}
        <div style={cardActionStyle}>
          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
              <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                {t("reconciliation.runAr")}
              </span>
              <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", color: "var(--color-status-info, #0284c7)", fontSize: "0.75rem", fontWeight: 700 }}>
                110100
              </span>
            </div>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.5 }}>
              مطابقة حساب مراقبة الذمم المدينة مع سجلات وفواتير مبيعات العملاء.
            </p>
          </div>
          <div>
            <Button variant="primary" data-testid="btn-reconcile-ar" onPress={() => handleOpenRunModal("AR")} style={{ width: "100%" }}>
              تشغيل مطابقة العملاء (AR)
            </Button>
          </div>
        </div>

        {/* AP Card */}
        <div style={cardActionStyle}>
          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
              <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                {t("reconciliation.runAp")}
              </span>
              <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-warning-subtle, #fffbeb)", color: "var(--color-status-warning, #d97706)", fontSize: "0.75rem", fontWeight: 700 }}>
                210100
              </span>
            </div>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.5 }}>
              مطابقة حساب مراقبة الذمم الدائنة مع فواتير ومستحقات الموردين.
            </p>
          </div>
          <div>
            <Button variant="primary" onPress={() => handleOpenRunModal("AP")} style={{ width: "100%" }}>
              تشغيل مطابقة الموردين (AP)
            </Button>
          </div>
        </div>

        {/* Inventory Card */}
        <div style={cardActionStyle}>
          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.5rem" }}>
              <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                {t("reconciliation.runInventory")}
              </span>
              <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-info-subtle, #f0f9ff)", color: "var(--color-status-info, #0284c7)", fontSize: "0.75rem", fontWeight: 700 }}>
                110400
              </span>
            </div>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", lineHeight: 1.5 }}>
              مطابقة تقييم حركات المستودعات والبضاعة المتاحة مع حساب الأستاذ العام.
            </p>
          </div>
          <div>
            <Button variant="primary" onPress={() => handleOpenRunModal("Inventory")} style={{ width: "100%" }}>
              تشغيل مطابقة المخزون (Inventory)
            </Button>
          </div>
        </div>
      </div>

      {/* Historical Runs Register */}
      <Surface
        variant="card"
        title={t("reconciliation.runsHistoryTitle")}
        style={{ padding: "0" }}
      >
        {isListLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            <p>{t("common.loading")}</p>
          </div>
        ) : reconciliations.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            <p>{t("reconciliation.noRunsYet")}</p>
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
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.colType")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.colAsOfDate")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reconciliation.colSourceBalance")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reconciliation.colLedgerBalance")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reconciliation.colDifference")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("reconciliation.colStatus")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.colDiagnostics")}</th>
                </tr>
              </thead>
              <tbody>
                {reconciliations.map((run: SubledgerReconciliationView, idx: number) => {
                  const hasDiff = (run.difference ?? 0) !== 0;
                  return (
                    <tr
                      key={run.id}
                      style={{
                        borderBottom: idx < reconciliations.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                        backgroundColor: idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)"
                      }}
                      data-testid={`reconciliation-row-${run.id}`}
                    >
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                        <span style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-surface-subtle, #f1f5f9)", border: "1px solid var(--color-border-subtle, #e2e8f0)", fontSize: "0.75rem" }}>
                          {run.type}
                        </span>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem" }}>
                        <bdi>{run.asOfDate}</bdi>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {run.sourceBalance !== null && run.sourceBalance !== undefined ? (
                          <MoneyDisplay value={{ amount: run.sourceBalance.toFixed(2), currency: run.currencyCode ?? "EGP" }} />
                        ) : "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                        {run.ledgerBalance !== null && run.ledgerBalance !== undefined ? (
                          <MoneyDisplay value={{ amount: run.ledgerBalance.toFixed(2), currency: run.currencyCode ?? "EGP" }} />
                        ) : "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                        {run.difference !== null && run.difference !== undefined ? (
                          <MoneyDisplay
                            value={{ amount: run.difference.toFixed(2), currency: run.currencyCode ?? "EGP" }}
                            coloredSign
                          />
                        ) : "—"}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        {getStatusBadge(run.status)}
                      </td>
                      <td style={{ padding: "0.75rem 1rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                        {run.diagnostics && run.diagnostics.length > 0 ? (
                          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.25rem" }}>
                            {run.diagnostics.map((diag: string, i: number) => (
                              <span
                                key={i}
                                style={{
                                  padding: "0.125rem 0.375rem",
                                  borderRadius: "var(--radius-sm, 0.25rem)",
                                  backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                                  border: "1px solid var(--color-border-subtle, #e2e8f0)",
                                  fontSize: "0.6875rem"
                                }}
                              >
                                {diag}
                              </span>
                            ))}
                          </div>
                        ) : "—"}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Issues Register */}
      <Surface
        variant="card"
        title={t("reconciliation.issuesTitle")}
        description={t("reconciliation.issuesSubtitle")}
        style={{ padding: "0" }}
      >
        {isIssuesLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            <p>{t("common.loading")}</p>
          </div>
        ) : issues.length === 0 ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-status-success, #059669)", fontWeight: 600 }}>
            <p style={{ margin: 0 }}>✓ {t("reconciliation.noIssuesFound")}</p>
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
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.issueColCode")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.issueColSeverity")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.issueColStatus")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.issueColCreatedAt")}</th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("reconciliation.issueColResolvedAt")}</th>
                </tr>
              </thead>
              <tbody>
                {issues.map((issue: ReconciliationIssueView, idx: number) => (
                  <tr
                    key={issue.id}
                    style={{
                      borderBottom: idx < issues.length - 1 ? "1px solid var(--color-border-subtle, #e2e8f0)" : "none",
                      backgroundColor: idx % 2 === 0 ? "var(--color-surface-card, #ffffff)" : "var(--color-surface-subtle, #f8fafc)"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem", fontFamily: "var(--font-mono, monospace)", fontSize: "0.8125rem", fontWeight: 700 }}>
                      <bdi>{issue.code}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <StatusBadge
                        intent={issue.severity === "Blocking" ? "danger" : issue.severity === "Attention" ? "warning" : "info"}
                        label={issue.severity}
                        size="small"
                      />
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <StatusBadge
                        intent={issue.status === "Resolved" ? "success" : "danger"}
                        label={issue.status}
                        size="small"
                      />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                      {issue.createdAt.slice(0, 10)}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                      {issue.resolvedAt ? issue.resolvedAt.slice(0, 10) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* RUN RECONCILIATION MODAL (Using Approved Dialog Primitive) */}
      <Dialog
        isOpen={isRunModalOpen}
        onOpenChange={(open) => !open && setIsRunModalOpen(false)}
        title={`${t("reconciliation.modalRunTitle")} — ${selectedType}`}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsRunModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={runMutation.isPending}
              onPress={handleExecuteRun as unknown as () => void}
            >
              {t("reconciliation.modalSubmit")}
            </Button>
          </div>
        }
      >
        <form onSubmit={handleExecuteRun} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label htmlFor="rec-as-of" style={{ display: "block", marginBottom: "0.375rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("reconciliation.modalAsOfLabel")}
            </label>
            <input
              id="rec-as-of"
              type="date"
              value={asOfDate}
              onChange={(e) => setAsOfDate(e.target.value)}
              required
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                fontSize: "0.875rem"
              }}
            />
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("reconciliation.modalAsOfHelp")}
            </p>
          </div>

          {runMutation.isError && (
            <div style={{ padding: "0.75rem", borderRadius: "var(--radius-md, 0.5rem)", backgroundColor: "var(--color-status-danger-subtle, #fef2f2)", border: "1px solid var(--color-status-danger, #dc2626)", color: "var(--color-status-danger, #dc2626)", fontSize: "0.8125rem" }}>
              {t("reconciliation.modalRunError")}
            </div>
          )}
        </form>
      </Dialog>
    </div>
  );
};
