import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { BankingOverviewTab } from "../features/overview/banking-overview-tab.tsx";
import { TreasuryAccountsTab } from "../features/accounts/treasury-accounts-tab.tsx";
import { StatementsTab } from "../features/statements/statements-tab.tsx";
import { ReconciliationTab } from "../features/reconciliation/reconciliation-tab.tsx";
import { CashManagementTab } from "../features/cash/cash-management-tab.tsx";
import { TransfersTab } from "../features/transfers/transfers-tab.tsx";
import { CashForecastTab } from "../features/forecast/cash-forecast-tab.tsx";
import { BankingReportsTab } from "../features/reports/banking-reports-tab.tsx";
import {
  LandmarkIcon,
  VaultIcon,
  StatementIcon,
  ReconcileIcon,
  TransferIcon,
  ForecastIcon,
  ReportsIcon
} from "../components/banking-icons.tsx";

export type BankingTabId =
  | "overview"
  | "accounts"
  | "statements"
  | "reconciliation"
  | "cash"
  | "transfers"
  | "forecast"
  | "reports";

export function BankingPage(): React.JSX.Element {
  const { t } = useTranslation("banking");

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as BankingTabId | null) ?? "overview";

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  const handleTabChange = (newTab: BankingTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  const tabs: readonly { id: BankingTabId; label: string; icon: React.ReactNode }[] = [
    { id: "overview", label: t("tabs.overview"), icon: <LandmarkIcon /> },
    { id: "accounts", label: t("tabs.accounts"), icon: <LandmarkIcon /> },
    { id: "statements", label: t("tabs.statements"), icon: <StatementIcon /> },
    { id: "reconciliation", label: t("tabs.reconciliation"), icon: <ReconcileIcon /> },
    { id: "cash", label: t("tabs.cash"), icon: <VaultIcon /> },
    { id: "transfers", label: t("tabs.transfers"), icon: <TransferIcon /> },
    { id: "forecast", label: t("tabs.forecast"), icon: <ForecastIcon /> },
    { id: "reports", label: t("tabs.reports"), icon: <ReportsIcon /> }
  ];

  return (
    <div data-testid="banking-page" style={{ display: "flex", flexDirection: "column", minHeight: "100%", gap: "1.5rem" }}>
      {/* Top Banner Ribbon */}
      <div
        style={{
          background: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-lg)",
          padding: "1.25rem 1.5rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          boxShadow: "var(--shadow-sm)"
        }}
      >
        <div>
          <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
            {t("moduleName")}
          </h1>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("moduleDescription")}
          </p>
        </div>
      </div>

      {/* Main Tab Bar */}
      <div
        role="tablist"
        aria-label={t("moduleName")}
        style={{
          display: "flex",
          gap: "0.5rem",
          overflowX: "auto",
          paddingBottom: "0.25rem",
          borderBottom: "2px solid var(--color-border-subtle)"
        }}
      >
        {tabs.map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              role="tab"
              data-testid={`banking-tab-${tab.id}`}
              aria-selected={isActive}
              type="button"
              onClick={() => handleTabChange(tab.id)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.625rem 1rem",
                borderRadius: "var(--radius-md) var(--radius-md) 0 0",
                fontSize: "0.875rem",
                fontWeight: isActive ? 600 : 500,
                color: isActive ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                backgroundColor: isActive ? "var(--color-surface-card)" : "transparent",
                border: "none",
                borderBottom: isActive ? "2px solid var(--color-brand-primary)" : "2px solid transparent",
                marginBottom: "-2px",
                cursor: "pointer",
                whiteSpace: "nowrap",
                transition: "all 0.15s ease"
              }}
            >
              {tab.icon}
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab Panels */}
      <div role="tabpanel">
        {activeTab === "overview" && <BankingOverviewTab legalEntityId={legalEntityId} />}
        {activeTab === "accounts" && <TreasuryAccountsTab legalEntityId={legalEntityId} />}
        {activeTab === "statements" && <StatementsTab legalEntityId={legalEntityId} />}
        {activeTab === "reconciliation" && <ReconciliationTab legalEntityId={legalEntityId} />}
        {activeTab === "cash" && <CashManagementTab legalEntityId={legalEntityId} />}
        {activeTab === "transfers" && <TransfersTab legalEntityId={legalEntityId} />}
        {activeTab === "forecast" && <CashForecastTab legalEntityId={legalEntityId} />}
        {activeTab === "reports" && <BankingReportsTab legalEntityId={legalEntityId} />}
      </div>
    </div>
  );
}
