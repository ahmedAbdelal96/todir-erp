import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { PayrollTabNav, type PayrollTabId, type PayrollTabDef } from "../components/payroll-tab-nav.tsx";
import { PayrollHomeTab } from "../features/home/payroll-home-tab.tsx";
import { PayrollRunsTab } from "../features/runs/payroll-runs-tab.tsx";
import { PayrollRunWorkspace } from "../features/runs/payroll-run-workspace.tsx";
import { PayrollSetupTab } from "../features/setup/payroll-setup-tab.tsx";
import { StatutorySettingsTab } from "../features/settings/statutory-settings-tab.tsx";
import { PayrollInputsTab } from "../features/inputs/payroll-inputs-tab.tsx";
import { PayrollLoansTab } from "../features/loans/payroll-loans-tab.tsx";
import { PayslipsTab } from "../features/payslips/payslips-tab.tsx";
import { PayrollReportsTab } from "../features/reports/payroll-reports-tab.tsx";
import { OpeningBalancesTab } from "../features/opening-balances/opening-balances-tab.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";

export function PayrollPage(): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const [searchParams, setSearchParams] = useSearchParams();

  const runtimeContext = useOptionalLegalEntityContext();
  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  if (!legalEntityId) {
    return (
      <div role="alert" style={{ margin: "1.5rem", padding: "2rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", background: "var(--color-surface-card)", color: "var(--color-content-primary)" }}>
        <h1 style={{ margin: "0 0 0.5rem", fontSize: "1.25rem" }}>{t("page.contextRequiredTitle")}</h1>
        <p style={{ margin: 0, color: "var(--color-content-secondary)" }}>{t("page.contextRequiredDescription")}</p>
      </div>
    );
  }

  // Active tab & Run Workspace selection derived from searchParams
  const activeTab = (searchParams.get("tab") as PayrollTabId | null) ?? "home";
  const selectedRunId = searchParams.get("runId");

  // Help Modal State
  const [isHelpOpen, setIsHelpOpen] = useState(false);

  const handleTabChange = (tabId: PayrollTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", tabId);
      if (tabId !== "runs") {
        next.delete("runId");
      }
      return next;
    });
  };

  const handleOpenRun = (runId: string) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", "runs");
      next.set("runId", runId);
      return next;
    });
  };

  const handleBackToRuns = () => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("runId");
      return next;
    });
  };

  const tabs: readonly PayrollTabDef[] = [
    { id: "home", label: t("tabs.home") },
    { id: "runs", label: t("tabs.runs") },
    { id: "inputs", label: t("tabs.inputs") },
    { id: "loans", label: t("tabs.loans") },
    { id: "payslips", label: t("tabs.payslips") },
    { id: "reports", label: t("tabs.reports") },
    { id: "setup", label: t("tabs.setup") },
    { id: "settings", label: t("tabs.settings") },
    { id: "openingBalances", label: t("tabs.openingBalances") }
  ];

  return (
    <div
      data-testid="payroll-page"
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100%",
        padding: "1.5rem",
        boxSizing: "border-box",
        backgroundColor: "var(--color-surface-page, #f0f4f8)"
      }}
    >
      {/* Page Header with Help Guide Button */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1.25rem",
          flexWrap: "wrap",
          gap: "1rem"
        }}
      >
        <div>
          <h1
            style={{
              margin: 0,
              fontSize: "1.5rem",
              fontWeight: 800,
              color: "var(--color-content-primary, #0f172a)",
              letterSpacing: "-0.025em"
            }}
          >
            {t("page.title")}
          </h1>
          <p
            style={{
              margin: "0.25rem 0 0 0",
              fontSize: "0.875rem",
              color: "var(--color-content-muted, #64748b)"
            }}
          >
            {t("page.description")}
          </p>
        </div>

        <Button
          variant="secondary"
          size="comfortable"
          onPress={() => setIsHelpOpen(true)}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.5rem",
            fontSize: "0.875rem",
            fontWeight: 600,
            borderRadius: "var(--radius-md)"
          }}
        >
          <span
            style={{
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              width: "20px",
              height: "20px",
              borderRadius: "50%",
              background: "var(--color-brand-primary-subtle, #102a4314)",
              color: "var(--color-brand-primary, #102a43)",
              fontWeight: 700,
              fontSize: "0.75rem"
            }}
          >
            ?
          </span>
          {t("page.helpButton")}
        </Button>
      </div>

      {/* Tabs Navigation */}
      <PayrollTabNav activeTab={activeTab} onTabChange={handleTabChange} tabs={tabs} />

      {/* Tab Contents */}
      <div style={{ flex: 1 }}>
        {activeTab === "home" && (
          <PayrollHomeTab
            legalEntityId={legalEntityId}
            onOpenRun={handleOpenRun}
            onNavigateTab={handleTabChange}
          />
        )}

        {activeTab === "runs" && (
          selectedRunId ? (
            <PayrollRunWorkspace
              legalEntityId={legalEntityId}
              runId={selectedRunId}
              onBack={handleBackToRuns}
            />
          ) : (
            <PayrollRunsTab
              legalEntityId={legalEntityId}
              onOpenRun={handleOpenRun}
            />
          )
        )}

        {activeTab === "inputs" && (
          <PayrollInputsTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "loans" && (
          <PayrollLoansTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "payslips" && (
          <PayslipsTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "reports" && (
          <PayrollReportsTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "setup" && (
          <PayrollSetupTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "settings" && (
          <StatutorySettingsTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "openingBalances" && (
          <OpeningBalancesTab legalEntityId={legalEntityId} />
        )}
      </div>

      {/* Help & Workflow Guide Modal */}
      <Dialog
        isOpen={isHelpOpen}
        onOpenChange={setIsHelpOpen}
        title={t("help.title")}
        description={t("help.subtitle")}
        size="large"
        footer={
          <Button variant="primary" onPress={() => setIsHelpOpen(false)}>
            {t("actions.understandAndClose")}
          </Button>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", lineHeight: 1.6 }}>
          <div
            style={{
              padding: "1rem 1.25rem",
              background: "var(--color-surface-subtle)",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-default)",
              fontSize: "0.875rem",
              color: "var(--color-content-secondary)"
            }}
          >
            {t("help.overview")}
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            <h4 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("help.monthlyWorkflowTitle")}
            </h4>
            <ol style={{ margin: 0, paddingInlineStart: "1.5rem", display: "flex", flexDirection: "column", gap: "0.625rem", fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
              <li><strong>{t("help.step1Title")}:</strong> {t("help.step1Desc")}</li>
              <li><strong>{t("help.step2Title")}:</strong> {t("help.step2Desc")}</li>
              <li><strong>{t("help.step3Title")}:</strong> {t("help.step3Desc")}</li>
              <li><strong>{t("help.step4Title")}:</strong> {t("help.step4Desc")}</li>
              <li><strong>{t("help.step5Title")}:</strong> {t("help.step5Desc")}</li>
              <li><strong>{t("help.step6Title")}:</strong> {t("help.step6Desc")}</li>
              <li><strong>{t("help.step7Title")}:</strong> {t("help.step7Desc")}</li>
            </ol>
          </div>

          <div
            style={{
              padding: "0.875rem 1rem",
              borderRadius: "var(--radius-md)",
              background: "var(--color-brand-primary-subtle)",
              border: "1px solid var(--color-brand-primary)",
              fontSize: "0.8125rem",
              color: "var(--color-brand-primary)",
              fontWeight: 600
            }}
          >
            {t("help.goldenRuleNotice")}
          </div>
        </div>
      </Dialog>
    </div>
  );
}
