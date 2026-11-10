import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useIsMobile } from "../hooks/use-is-mobile.ts";
import { ExpensesSelfServiceTab } from "../features/self-service/expenses-self-service-tab.tsx";
import { ExpensesReviewTab } from "../features/review/expenses-review-tab.tsx";
import { ExpensesAdvancesTab } from "../features/advances/expenses-advances-tab.tsx";
import { ExpensesCorporateCardsTab } from "../features/corporate-cards/expenses-corporate-cards-tab.tsx";
import { ExpensesReimbursementsTab } from "../features/reimbursements/expenses-reimbursements-tab.tsx";
import { ExpensesReportsTab } from "../features/reports/expenses-reports-tab.tsx";
import { ExpensesSettingsTab } from "../features/settings/expenses-settings-tab.tsx";
import {
  UserIcon,
  CheckCircleIcon,
  BanknoteIcon,
  CreditCardIcon,
  ArrowRightLeftIcon,
  TrendingUpIcon,
  SettingsIcon
} from "../components/expenses-icons.tsx";

export type ExpensesTabId =
  | "self-service"
  | "review"
  | "advances"
  | "corporate-cards"
  | "reimbursements"
  | "reports"
  | "settings";

export function ExpensesPage(): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const isMobile = useIsMobile(768);

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = outletCtx?.permissions ?? useOptionalPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as ExpensesTabId | null) ?? "self-service";

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || null;

  // Check elevated / finance permissions (fail closed if context is missing)
  const canManage = Boolean(
    permissions &&
      (permissions.hasPermission("expenses.approve") ||
        permissions.hasPermission("expenses.finalize") ||
        permissions.hasPermission("expenses.reimbursement.manage") ||
        permissions.hasPermission("expenses.category.manage") ||
        permissions.hasPermission("expenses.audit.read") ||
        permissions.hasPermission("admin") ||
        permissions.hasPermission("*"))
  );

  const handleTabChange = (newTab: ExpensesTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  const tabs: readonly {
    id: ExpensesTabId;
    label: string;
    icon: React.ReactNode;
    requiresManager?: boolean;
    isPermitted?: () => boolean;
  }[] = [
    {
      id: "self-service",
      label: t("tabs.selfService"),
      icon: <UserIcon size={16} />
    },
    {
      id: "review",
      label: t("tabs.review"),
      icon: <CheckCircleIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.approve") ||
            permissions?.hasPermission("expenses.finalize") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    },
    {
      id: "advances",
      label: t("tabs.advances"),
      icon: <BanknoteIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.reimbursement.manage") ||
            permissions?.hasPermission("expenses.finalize") ||
            permissions?.hasPermission("expenses.approve") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    },
    {
      id: "corporate-cards",
      label: t("tabs.corporateCards"),
      icon: <CreditCardIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.finalize") ||
            permissions?.hasPermission("expenses.approve") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    },
    {
      id: "reimbursements",
      label: t("tabs.reimbursements"),
      icon: <ArrowRightLeftIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.reimbursement.manage") ||
            permissions?.hasPermission("expenses.finalize") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    },
    {
      id: "reports",
      label: t("tabs.reports"),
      icon: <TrendingUpIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.audit.read") ||
            permissions?.hasPermission("expenses.approve") ||
            permissions?.hasPermission("expenses.finalize") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    },
    {
      id: "settings",
      label: t("tabs.settings"),
      icon: <SettingsIcon size={16} />,
      requiresManager: true,
      isPermitted: () =>
        Boolean(
          permissions?.hasPermission("expenses.category.manage") ||
            permissions?.hasPermission("admin") ||
            permissions?.hasPermission("*")
        )
    }
  ];

  const visibleTabs = tabs.filter((tab) => !tab.requiresManager || (canManage && (tab.isPermitted ? tab.isPermitted() : true)));
  const currentTab = visibleTabs.some((t) => t.id === activeTab) ? activeTab : "self-service";

  if (!legalEntityId) {
    return (
      <div
        data-testid="expenses-page"
        style={{
          display: "flex",
          flexDirection: "column",
          minHeight: "100%",
          backgroundColor: "var(--color-surface-page, #f0f4f8)"
        }}
      >
        <header
          style={{
            backgroundColor: "#102a43",
            color: "#ffffff",
            padding: "1.25rem 2rem",
            borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
          }}
        >
          <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "#ffffff" }}>
            {t("title")}
          </h1>
          <p style={{ margin: "0.375rem 0 0", fontSize: "0.875rem", color: "#94a3b8" }}>
            {t("subtitle")}
          </p>
        </header>
        <main style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
          <div
            style={{
              maxWidth: "480px",
              margin: "2rem auto",
              padding: "2rem",
              borderRadius: "var(--radius-lg, 0.75rem)",
              backgroundColor: "#ffffff",
              border: "1px solid var(--color-border-default, #cbd5e1)"
            }}
          >
            <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("common.noLegalEntitySelected")}
            </h3>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>
              {t("common.selectLegalEntityNotice")}
            </p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div
      data-testid="expenses-page"
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100%",
        backgroundColor: "var(--color-surface-page, #f0f4f8)"
      }}
    >
      {/* Dark Oxford Navy Application Header */}
      <header
        style={{
          backgroundColor: "#102a43",
          color: "#ffffff",
          padding: isMobile ? "1rem 1rem 0" : "1.25rem 2rem 0",
          borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem",
            marginBottom: "1rem"
          }}
        >
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: isMobile ? "1.25rem" : "1.5rem",
                  fontWeight: 700,
                  color: "#ffffff",
                  letterSpacing: "-0.02em"
                }}
              >
                {t("title")}
              </h1>
              <span
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  padding: "0.15rem 0.5rem",
                  borderRadius: "9999px",
                  fontSize: "0.75rem",
                  fontWeight: 600,
                  backgroundColor: "rgba(255, 255, 255, 0.15)",
                  color: "#e2e8f0"
                }}
              >
                {t("badge")}
              </span>
            </div>
            <p
              style={{
                margin: "0.25rem 0 0",
                fontSize: isMobile ? "0.8125rem" : "0.875rem",
                color: "#94a3b8"
              }}
            >
              {t("subtitle")}
            </p>
          </div>

          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              padding: "0.375rem 0.75rem",
              borderRadius: "0.375rem",
              backgroundColor: "rgba(255, 255, 255, 0.08)",
              fontSize: "0.8125rem",
              color: "#cbd5e1"
            }}
          >
            <span style={{ color: "#94a3b8" }}>{t("activeEntity")}:</span>
            <strong style={{ color: "#ffffff", direction: "ltr" }}>
              {runtimeContext?.name ?? t("defaultEntity")}
            </strong>
          </div>
        </div>

        {/* Navigation Tabs Bar */}
        <nav
          aria-label={t("title")}
          style={{
            display: "flex",
            gap: "0.25rem",
            overflowX: "auto",
            scrollbarWidth: "none"
          }}
        >
          {visibleTabs.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                aria-selected={isActive}
                onClick={() => handleTabChange(tab.id)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.5rem",
                  padding: isMobile ? "0.625rem 0.75rem" : "0.75rem 1rem",
                  fontSize: isMobile ? "0.8125rem" : "0.875rem",
                  fontWeight: isActive ? 600 : 500,
                  color: isActive ? "#ffffff" : "#94a3b8",
                  backgroundColor: "transparent",
                  border: "none",
                  borderBottom: isActive ? "3px solid #38bdf8" : "3px solid transparent",
                  cursor: "pointer",
                  whiteSpace: "nowrap",
                  transition: "all 0.15s ease-in-out"
                }}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            );
          })}
        </nav>
      </header>

      {/* Main Canvas Area */}
      <main
        style={{
          flex: 1,
          padding: isMobile ? "1rem" : "1.5rem 2rem",
          maxWidth: "1600px",
          width: "100%",
          margin: "0 auto",
          boxSizing: "border-box"
        }}
      >
        {currentTab === "self-service" && <ExpensesSelfServiceTab legalEntityId={legalEntityId} />}
        {currentTab === "review" && <ExpensesReviewTab legalEntityId={legalEntityId} />}
        {currentTab === "advances" && <ExpensesAdvancesTab legalEntityId={legalEntityId} />}
        {currentTab === "corporate-cards" && <ExpensesCorporateCardsTab legalEntityId={legalEntityId} />}
        {currentTab === "reimbursements" && <ExpensesReimbursementsTab legalEntityId={legalEntityId} />}
        {currentTab === "reports" && <ExpensesReportsTab legalEntityId={legalEntityId} />}
        {currentTab === "settings" && <ExpensesSettingsTab legalEntityId={legalEntityId} />}
      </main>
    </div>
  );
}
