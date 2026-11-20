import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams, useNavigate, useLocation } from "react-router-dom";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import type { EffectiveAccess } from "../../../core/runtime/effective-access.ts";
import type { PermissionSet } from "../../../core/runtime/permissions.ts";
import { hasPermissionInAccess } from "../../../core/runtime/effective-access.ts";
import { DefaultAccessDeniedView } from "../../../core/runtime/permission-guard.tsx";
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

const TAB_PERMISSIONS: Record<BankingTabId, readonly string[]> = {
  overview: ["banking.accounts.read", "banking.statement.read", "banking.reconciliation.read", "banking.reports.read"],
  accounts: ["banking.accounts.read", "banking.accounts.manage"],
  statements: ["banking.statement.read", "banking.statement.import", "banking.statement.validate"],
  reconciliation: ["banking.reconciliation.read", "banking.reconciliation.manage", "banking.reconciliation.finalize"],
  cash: ["banking.cash.count", "banking.cash.transfer"],
  transfers: ["banking.cash.transfer", "banking.accounts.manage", "payments.read"],
  forecast: ["banking.cash_forecast.manage", "banking.accounts.read"],
  reports: ["banking.reports.read", "banking.accounts.read"]
};

function isTabAllowed(tabId: BankingTabId, access?: EffectiveAccess, perms?: PermissionSet): boolean {
  if (access) {
    if (access.isSuper) return true;
    const required = TAB_PERMISSIONS[tabId];
    return required.some(p => hasPermissionInAccess(access, p));
  }
  if (perms) {
    const required = TAB_PERMISSIONS[tabId];
    return required.some(p => perms.hasPermission(p));
  }
  return true;
}

export interface BankingPageProps {
  readonly initialTab?: BankingTabId | undefined;
}

export function BankingPage({ initialTab }: BankingPageProps = {}): React.JSX.Element {
  const { t } = useTranslation("banking");
  const navigate = useNavigate();
  const location = useLocation();

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const effectiveAccess = outletCtx?.effectiveAccess;
  const permissions = outletCtx?.permissions;

  const [searchParams, setSearchParams] = useSearchParams();
  const rawParamTab = searchParams.get("tab") as BankingTabId | null;

  const allTabs: readonly { id: BankingTabId; label: string; icon: React.ReactNode; routePath: string }[] = [
    { id: "overview", label: t("tabs.overview"), icon: <LandmarkIcon />, routePath: "/banking" },
    { id: "accounts", label: t("tabs.accounts"), icon: <LandmarkIcon />, routePath: "/banking/accounts" },
    { id: "statements", label: t("tabs.statements"), icon: <StatementIcon />, routePath: "/banking/statements" },
    { id: "reconciliation", label: t("tabs.reconciliation"), icon: <ReconcileIcon />, routePath: "/banking/reconciliations" },
    { id: "cash", label: t("tabs.cash"), icon: <VaultIcon />, routePath: "/banking/cash" },
    { id: "transfers", label: t("tabs.transfers"), icon: <TransferIcon />, routePath: "/banking/transfers" },
    { id: "forecast", label: t("tabs.forecast"), icon: <ForecastIcon />, routePath: "/banking/forecast" },
    { id: "reports", label: t("tabs.reports"), icon: <ReportsIcon />, routePath: "/banking/reports" }
  ];

  const permittedTabs = allTabs.filter(tab => isTabAllowed(tab.id, effectiveAccess, permissions));

  let activeTab: BankingTabId;
  const isExplicitRequest = Boolean(initialTab || rawParamTab);
  if (rawParamTab) {
    activeTab = rawParamTab;
  } else if (initialTab) {
    activeTab = initialTab;
  } else if (permittedTabs.some(tab => tab.id === "overview")) {
    activeTab = "overview";
  } else if (permittedTabs.length > 0) {
    activeTab = permittedTabs[0]!.id;
  } else {
    activeTab = "overview";
  }

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  const handleTabChange = (newTab: BankingTabId) => {
    const target = allTabs.find(t => t.id === newTab);
    if (target && location.pathname !== "/banking") {
      navigate(target.routePath);
    } else {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (newTab === "overview") {
          next.delete("tab");
        } else {
          next.set("tab", newTab);
        }
        return next;
      });
    }
  };

  const isCurrentTabPermitted = isTabAllowed(activeTab, effectiveAccess, permissions);
  if (isExplicitRequest && !isCurrentTabPermitted) {
    return <DefaultAccessDeniedView />;
  }

  const displayedTabs = permittedTabs.length > 0 ? permittedTabs : allTabs;

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
        {displayedTabs.map((tab) => {
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
