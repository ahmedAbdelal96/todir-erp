import React from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract";
import { CrmSellerHomeTab } from "../features/home/crm-seller-home-tab";
import { CrmLeadsTab } from "../features/leads/crm-leads-tab";
import { CrmOpportunitiesTab } from "../features/opportunities/crm-opportunities-tab";
import { CrmActivitiesTab } from "../features/activities/crm-activities-tab";
import { CrmManagementTab } from "../features/management/crm-management-tab";
import {
  Users,
  Target,
  Kanban,
  CheckSquare,
  BarChart2,
  Building2,
  CalendarCheck,
} from "../components/crm-icons.tsx";

export type CrmTabId = "home" | "leads" | "opportunities" | "activities" | "management";

export function CrmPage(): React.JSX.Element {
  const { t } = useTranslation("crm");

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = useOptionalPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as CrmTabId | null) ?? "home";

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  // Check if user has management permissions (can view all team data and manage pipelines/teams)
  const canManage = permissions
    ? permissions.hasPermission("crm.team.manage") ||
      permissions.hasPermission("crm.pipeline.manage") ||
      permissions.hasPermission("crm.all.view") ||
      permissions.hasPermission("admin")
    : true; // Default to true in dev if permissive

  const handleTabChange = (newTab: CrmTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  if (!legalEntityId) {
    return (
      <div
        role="alert"
        style={{
          margin: "1.5rem",
          padding: "2rem",
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
          textAlign: "center"
        }}
      >
        <Building2 style={{ margin: "0 auto 0.75rem", height: "3rem", width: "3rem", color: "var(--color-content-muted, #64748b)" }} />
        <h2 style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)", marginBottom: "0.25rem" }}>
          {t("page.contextRequiredTitle", "يلزم اختيار الكيان القانوني")}
        </h2>
        <p style={{ fontSize: "0.875rem", color: "var(--color-content-muted, #64748b)", margin: 0 }}>
          {t("page.contextRequiredDescription", "يرجى تحديد الشركة أو الكيان القانوني من شريط التنقل للوصول إلى إدارة علاقات العملاء.")}
        </p>
      </div>
    );
  }

  const tabs: Array<{ id: CrmTabId; label: string; icon: React.ReactNode }> = [
    { id: "home", label: t("tabs.home", "مبيعاتي اليوم"), icon: <CalendarCheck style={{ width: "1rem", height: "1rem" }} /> },
    { id: "leads", label: t("tabs.leads", "العملاء المحتملين"), icon: <Users style={{ width: "1rem", height: "1rem" }} /> },
    { id: "opportunities", label: t("tabs.opportunities", "الفرص والمسار البيعي"), icon: <Kanban style={{ width: "1rem", height: "1rem" }} /> },
    { id: "activities", label: t("tabs.activities", "الأنشطة والمتابعات"), icon: <CheckSquare style={{ width: "1rem", height: "1rem" }} /> },
    { id: "management", label: t("tabs.management", "إدارة المبيعات والتقارير"), icon: <BarChart2 style={{ width: "1rem", height: "1rem" }} /> },
  ];

  return (
    <div
      data-testid="crm-page"
      style={{
        minHeight: "100%",
        display: "flex",
        flexDirection: "column",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        paddingBottom: "3rem"
      }}
    >
      {/* Top Header Anchor - High Contrast Oxford Navy */}
      <header
        style={{
          backgroundColor: "var(--color-brand-primary, #102a43)",
          color: "#ffffff",
          borderBottom: "1px solid #0c1f31",
          boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)",
          padding: "1.25rem 1.5rem 0",
          boxSizing: "border-box"
        }}
      >
        <div style={{ maxWidth: "1400px", margin: "0 auto" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "1rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
              <div
                style={{
                  padding: "0.625rem",
                  backgroundColor: "rgba(255, 255, 255, 0.12)",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  border: "1px solid rgba(255, 255, 255, 0.2)"
                }}
              >
                <Target style={{ height: "1.5rem", width: "1.5rem", color: "#60a5fa" }} />
              </div>
              <div>
                <h1 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 800, color: "#ffffff", letterSpacing: "-0.01em" }}>
                  {t("page.title", "إدارة علاقات العملاء (CRM)")}
                </h1>
                <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#94a3b8" }}>
                  {t("page.subtitle", "متابعة العملاء المحتملين، مسارات الصفقات، والأنشطة البيعية")}
                </p>
              </div>
            </div>

            {/* Entity Context Badge */}
            <div
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                fontSize: "0.8125rem",
                color: "#e2e8f0",
                backgroundColor: "rgba(255, 255, 255, 0.08)",
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid rgba(255, 255, 255, 0.15)"
              }}
            >
              <Building2 style={{ width: "1rem", height: "1rem", color: "#38bdf8" }} />
              <span style={{ fontWeight: 600 }}>{runtimeContext?.name || t("page.currentEntity", "الكيان النشط")}</span>
            </div>
          </div>

          {/* Navigation Tab Bar */}
          <nav
            style={{
              display: "flex",
              gap: "0.5rem",
              overflowX: "auto",
              paddingBottom: 0,
              marginTop: "1.25rem",
              borderBottom: "1px solid rgba(255, 255, 255, 0.12)"
            }}
            aria-label={t("page.tabsNavAria", "تبويبات إدارة علاقات العملاء")}
          >
            {tabs.map((tab) => {
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => handleTabChange(tab.id)}
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.5rem",
                    padding: "0.625rem 1rem",
                    background: isActive ? "rgba(255, 255, 255, 0.15)" : "transparent",
                    color: isActive ? "#ffffff" : "#94a3b8",
                    fontWeight: isActive ? 700 : 500,
                    fontSize: "0.875rem",
                    border: "none",
                    borderBottom: `2px solid ${isActive ? "#38bdf8" : "transparent"}`,
                    borderRadius: "var(--radius-md, 0.5rem) var(--radius-md, 0.5rem) 0 0",
                    cursor: "pointer",
                    whiteSpace: "nowrap",
                    transition: "all 0.15s ease"
                  }}
                >
                  {tab.icon}
                  <span>{tab.label}</span>
                </button>
              );
            })}
          </nav>
        </div>
      </header>

      {/* Main Tab Content Canvas */}
      <main
        style={{
          width: "100%",
          maxWidth: "1400px",
          margin: "0 auto",
          padding: "1.5rem",
          boxSizing: "border-box"
        }}
      >
        {activeTab === "home" && (
          <CrmSellerHomeTab
            legalEntityId={legalEntityId}
            onNavigateToLeads={() => handleTabChange("leads")}
            onNavigateToOpportunities={() => handleTabChange("opportunities")}
            onNavigateToActivities={() => handleTabChange("activities")}
          />
        )}

        {activeTab === "leads" && (
          <CrmLeadsTab legalEntityId={legalEntityId} canManage={canManage} />
        )}

        {activeTab === "opportunities" && (
          <CrmOpportunitiesTab legalEntityId={legalEntityId} canManage={canManage} />
        )}

        {activeTab === "activities" && (
          <CrmActivitiesTab legalEntityId={legalEntityId} canManage={canManage} />
        )}

        {activeTab === "management" && (
          <CrmManagementTab legalEntityId={legalEntityId} />
        )}
      </main>
    </div>
  );
}

export default CrmPage;
