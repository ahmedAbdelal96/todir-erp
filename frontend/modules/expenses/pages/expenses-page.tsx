import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
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

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = useOptionalPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as ExpensesTabId | null) ?? "self-service";

  const legalEntityId =
    outletCtx?.legalEntityId ||
    runtimeContext?.legalEntityId ||
    "00000000-0000-0000-0000-000000000001";

  // Check elevated / finance permissions
  const canManage = permissions
    ? permissions.hasPermission("expenses.approve") ||
      permissions.hasPermission("expenses.finalize") ||
      permissions.hasPermission("expenses.reimbursement.manage") ||
      permissions.hasPermission("expenses.category.manage") ||
      permissions.hasPermission("admin")
    : true;

  const handleTabChange = (newTab: ExpensesTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  const tabs: readonly { id: ExpensesTabId; label: string; icon: React.ReactNode; requiresManager?: boolean }[] = [
    {
      id: "self-service",
      label: t("tabs.selfService"),
      icon: <UserIcon size={16} />
    },
    {
      id: "review",
      label: t("tabs.review"),
      icon: <CheckCircleIcon size={16} />,
      requiresManager: true
    },
    {
      id: "advances",
      label: t("tabs.advances"),
      icon: <BanknoteIcon size={16} />,
      requiresManager: true
    },
    {
      id: "corporate-cards",
      label: t("tabs.corporateCards"),
      icon: <CreditCardIcon size={16} />,
      requiresManager: true
    },
    {
      id: "reimbursements",
      label: t("tabs.reimbursements"),
      icon: <ArrowRightLeftIcon size={16} />,
      requiresManager: true
    },
    {
      id: "reports",
      label: t("tabs.reports"),
      icon: <TrendingUpIcon size={16} />,
      requiresManager: true
    },
    {
      id: "settings",
      label: t("tabs.settings"),
      icon: <SettingsIcon size={16} />,
      requiresManager: true
    }
  ];

  const visibleTabs = tabs.filter((tab) => !tab.requiresManager || canManage);

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
          padding: "1.25rem 2rem 0",
          borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem",
            marginBottom: "1rem"
          }}
        >
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: "1.5rem",
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
                  padding: "0.2rem 0.625rem",
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
                margin: "0.375rem 0 0",
                fontSize: "0.875rem",
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
                  padding: "0.75rem 1rem",
                  fontSize: "0.875rem",
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
          padding: "1.5rem 2rem",
          maxWidth: "1600px",
          width: "100%",
          margin: "0 auto",
          boxSizing: "border-box"
        }}
      >
        {activeTab === "self-service" && <ExpensesSelfServiceTab legalEntityId={legalEntityId} />}
        {activeTab === "review" && <ExpensesReviewTab legalEntityId={legalEntityId} />}
        {activeTab === "advances" && <ExpensesAdvancesTab legalEntityId={legalEntityId} />}
        {activeTab === "corporate-cards" && <ExpensesCorporateCardsTab legalEntityId={legalEntityId} />}
        {activeTab === "reimbursements" && <ExpensesReimbursementsTab legalEntityId={legalEntityId} />}
        {activeTab === "reports" && <ExpensesReportsTab legalEntityId={legalEntityId} />}
        {activeTab === "settings" && <ExpensesSettingsTab legalEntityId={legalEntityId} />}
      </main>
    </div>
  );
}
