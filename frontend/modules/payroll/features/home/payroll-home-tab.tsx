import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PayrollKpiCard } from "../../components/payroll-kpi-card.tsx";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { formatMoneyAmount } from "../../utils/payroll-formatters.ts";
import { usePayrollGroups, usePayrollRuns, usePayrollReport, usePayrollStatutorySettings } from "../../hooks/use-payroll-queries.ts";
import { NewPayrollRunModal } from "../runs/new-payroll-run-modal.tsx";
import { NewPayrollInputModal } from "../inputs/new-payroll-input-modal.tsx";
import { NewLoanModal } from "../loans/new-loan-modal.tsx";
import type { PayrollRun } from "../../types.ts";

export interface PayrollHomeTabProps {
  readonly legalEntityId: string;
  readonly onOpenRun: (runId: string) => void;
  readonly onNavigateTab: (tabId: "runs" | "setup" | "settings" | "inputs" | "loans" | "payslips" | "reports") => void;
}

export function PayrollHomeTab({ legalEntityId, onOpenRun, onNavigateTab }: PayrollHomeTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");

  const [isNewRunOpen, setIsNewRunOpen] = useState(false);
  const [isNewInputOpen, setIsNewInputOpen] = useState(false);
  const [isNewLoanOpen, setIsNewLoanOpen] = useState(false);

  const groupsQuery = usePayrollGroups(legalEntityId);
  const runsQuery = usePayrollRuns(legalEntityId, { pageSize: 5 });
  const reportQuery = usePayrollReport(legalEntityId, { pageSize: 1 });
  const statutoryQuery = usePayrollStatutorySettings(legalEntityId);

  const groups = groupsQuery.data ?? [];
  const runs = runsQuery.data ?? [];
  const report = reportQuery.data;
  const statutory = statutoryQuery.data;

  // Active / Latest Run
  const latestRun: PayrollRun | undefined = runs[0];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Statutory Mode Banner / Information */}
      {statutory && (statutory.taxMode === "NotManaged" || statutory.insuranceMode === "NotManaged") && (
        <div
          role="region"
          aria-label={t("statutoryBanner.title")}
          style={{
            padding: "0.875rem 1.25rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            background: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ flexShrink: 0, color: "var(--color-brand-primary)" }}>
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="16" x2="12" y2="12" />
              <line x1="12" y1="8" x2="12.01" y2="8" />
            </svg>
            <div>
              <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
                {t("statutoryBanner.title")}
              </span>
              <p style={{ margin: "0.125rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("statutoryBanner.description")}
              </p>
            </div>
          </div>
          <Button variant="secondary" size="compact" onPress={() => onNavigateTab("settings")}>
            {t("statutoryBanner.manageSettings")}
          </Button>
        </div>
      )}

      {/* KPI Cards Row */}
      <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
        <PayrollKpiCard
          title={t("reports.totalNetPayroll")}
          value={
            <bdi>
              {formatMoneyAmount(
                latestRun ? latestRun.totalNet : report?.totalNet ?? 0,
                "EGP"
              )}
            </bdi>
          }
          subtitle={latestRun ? latestRun.runNumber : t("reports.totalNetPayroll")}
          tone="primary"
        />

        <PayrollKpiCard
          title={t("reports.totalGrossPayroll")}
          value={
            <bdi>
              {formatMoneyAmount(
                latestRun ? latestRun.totalGross : report?.totalGross ?? 0,
                "EGP"
              )}
            </bdi>
          }
          subtitle={t("reports.gross")}
          tone="default"
        />

        <PayrollKpiCard
          title={t("reports.totalDeductions")}
          value={
            <bdi>
              {formatMoneyAmount(
                (() => {
                  const ded = latestRun ? latestRun.totalDeductions : report?.totalDeductions ?? 0;
                  return ded > 0 ? -ded : ded;
                })(),
                "EGP"
              )}
            </bdi>
          }
          subtitle={t("reports.totalDeductions")}
          tone="warning"
        />

        <PayrollKpiCard
          title={t("reports.employees")}
          value={latestRun ? latestRun.employeeCount : groups.length > 0 ? "80" : "0"}
          subtitle={t("reports.employees")}
          tone="success"
        />
      </div>

      {/* Primary Actions & Quick Links Surface */}
      <Surface style={{ padding: "1.25rem", display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("home.quickActions")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("home.subtitle")}
          </p>
        </div>

        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button variant="secondary" onPress={() => setIsNewInputOpen(true)}>
            {t("home.newInputAction")}
          </Button>

          <Button variant="secondary" onPress={() => setIsNewLoanOpen(true)}>
            {t("home.requestLoanAction")}
          </Button>

          <Button variant="primary" onPress={() => setIsNewRunOpen(true)}>
            {t("home.newRunAction")}
          </Button>
        </div>
      </Surface>

      {/* Latest Runs Surface */}
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("home.recentRunsTitle")}
          </h2>
          <Button variant="secondary" size="compact" onPress={() => onNavigateTab("runs")}>
            {t("home.viewAllRuns")}
          </Button>
        </div>

        {runs.length === 0 ? (
          <div style={{ textAlign: "center", padding: "2.5rem 1rem", color: "var(--color-content-muted)" }}>
            <p style={{ fontSize: "0.9375rem", fontWeight: 500 }}>{t("runs.emptyTitle")}</p>
            <Button variant="primary" size="compact" onPress={() => setIsNewRunOpen(true)} style={{ marginTop: "0.75rem" }}>
              {t("home.newRunAction")}
            </Button>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)", background: "var(--color-surface-subtle, #f8fafc)" }}>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.type")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("reports.employees")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.gross")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("reports.totalDeductions")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.net")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.calcState")}</th>
                  <th style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>{t("runs.actionsTitle")}</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r) => (
                  <tr key={r.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 600, color: "var(--color-brand-primary, #102a43)" }}>
                      {r.runType}
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>{r.employeeCount}</td>
                    <td style={{ padding: "0.875rem 1rem" }}><bdi>{(r.totalGross ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem" }}><bdi>-{(r.totalDeductions ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem", fontWeight: 700 }}><bdi>{(r.totalNet ?? 0).toLocaleString()} EGP</bdi></td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap" }}>
                        <PayrollStatusBadge status={r.calculationState} type="calculation" />
                        <PayrollStatusBadge status={r.finalizationState} type="finalization" />
                      </div>
                    </td>
                    <td style={{ padding: "0.875rem 1rem" }}>
                      <Button variant="secondary" size="compact" onPress={() => onOpenRun(r.id)}>
                        {t("home.viewWorkspaceAction")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Modals */}
      <NewPayrollRunModal
        isOpen={isNewRunOpen}
        onClose={() => setIsNewRunOpen(false)}
        legalEntityId={legalEntityId}
        onCreated={(runId) => {
          setIsNewRunOpen(false);
          onOpenRun(runId);
        }}
      />

      <NewPayrollInputModal
        isOpen={isNewInputOpen}
        onClose={() => setIsNewInputOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewLoanModal
        isOpen={isNewLoanOpen}
        onClose={() => setIsNewLoanOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
