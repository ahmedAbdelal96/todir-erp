import React, { useState } from "react";
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
  const { t, i18n } = useTranslation("crm");
  const isRtl = i18n.dir() === "rtl";

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
      <div className="m-6 p-8 bg-white border border-slate-200 rounded-lg shadow-sm text-center">
        <Building2 className="mx-auto h-12 w-12 text-slate-400 mb-3" />
        <h2 className="text-lg font-bold text-slate-900 mb-1">
          {t("page.contextRequiredTitle", "يلزم اختيار الكيان القانوني")}
        </h2>
        <p className="text-sm text-slate-500">
          {t("page.contextRequiredDescription", "يرجى تحديد الشركة أو الكيان القانوني من شريط التنقل للوصول إلى إدارة علاقات العملاء.")}
        </p>
      </div>
    );
  }

  return (
    <div data-testid="crm-page" className="min-h-screen bg-[#f0f4f8] pb-12">
      {/* Top Header Anchor - High Contrast Oxford Navy */}
      <div className="bg-[#102a43] text-white border-b border-slate-800 shadow-md">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-5">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-primary-600 rounded-lg shadow-inner">
                <Target className="h-6 w-6 text-white" />
              </div>
              <div>
                <h1 className="text-xl font-bold text-white tracking-tight">
                  {t("page.title", "إدارة علاقات العملاء (CRM)")}
                </h1>
                <p className="text-xs text-slate-300 mt-0.5">
                  {t("page.subtitle", "متابعة العملاء المحتملين، مسارات الصفقات، والأنشطة البيعية")}
                </p>
              </div>
            </div>

            {/* Quick stats or context indicator */}
            <div className="flex items-center gap-2 text-xs text-slate-300 bg-slate-800/80 px-3 py-1.5 rounded-md border border-slate-700">
              <Building2 className="h-4 w-4 text-primary-400" />
              <span>{runtimeContext?.name || t("page.currentEntity", "الكيان النشط")}</span>
            </div>
          </div>

          {/* Navigation Tab Bar */}
          <div className="mt-6 border-b border-slate-700/60">
            <nav className="flex space-x-6 rtl:space-x-reverse overflow-x-auto pb-px" aria-label={t("page.tabsNavAria", "تبويبات إدارة علاقات العملاء")}>
              {/* Tab 1: Home / My Sales Today */}
              <button
                type="button"
                onClick={() => handleTabChange("home")}
                className={`pb-3 px-1 border-b-2 font-semibold text-sm inline-flex items-center gap-2 whitespace-nowrap transition-colors cursor-pointer ${
                  activeTab === "home"
                    ? "border-primary-400 text-primary-300"
                    : "border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-500"
                }`}
              >
                <CalendarCheck className="h-4 w-4" />
                <span>{t("tabs.home", "مبيعاتي اليوم")}</span>
              </button>

              {/* Tab 2: Leads Inbox */}
              <button
                type="button"
                onClick={() => handleTabChange("leads")}
                className={`pb-3 px-1 border-b-2 font-semibold text-sm inline-flex items-center gap-2 whitespace-nowrap transition-colors cursor-pointer ${
                  activeTab === "leads"
                    ? "border-primary-400 text-primary-300"
                    : "border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-500"
                }`}
              >
                <Users className="h-4 w-4" />
                <span>{t("tabs.leads", "العملاء المحتملين")}</span>
              </button>

              {/* Tab 3: Opportunities & Pipeline */}
              <button
                type="button"
                onClick={() => handleTabChange("opportunities")}
                className={`pb-3 px-1 border-b-2 font-semibold text-sm inline-flex items-center gap-2 whitespace-nowrap transition-colors cursor-pointer ${
                  activeTab === "opportunities"
                    ? "border-primary-400 text-primary-300"
                    : "border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-500"
                }`}
              >
                <Kanban className="h-4 w-4" />
                <span>{t("tabs.opportunities", "الفرص والمسار البيعي")}</span>
              </button>

              {/* Tab 4: Activities */}
              <button
                type="button"
                onClick={() => handleTabChange("activities")}
                className={`pb-3 px-1 border-b-2 font-semibold text-sm inline-flex items-center gap-2 whitespace-nowrap transition-colors cursor-pointer ${
                  activeTab === "activities"
                    ? "border-primary-400 text-primary-300"
                    : "border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-500"
                }`}
              >
                <CheckSquare className="h-4 w-4" />
                <span>{t("tabs.activities", "الأنشطة والمتابعات")}</span>
              </button>

              {/* Tab 5: Sales Management */}
              <button
                type="button"
                onClick={() => handleTabChange("management")}
                className={`pb-3 px-1 border-b-2 font-semibold text-sm inline-flex items-center gap-2 whitespace-nowrap transition-colors cursor-pointer ${
                  activeTab === "management"
                    ? "border-primary-400 text-primary-300"
                    : "border-transparent text-slate-400 hover:text-slate-200 hover:border-slate-500"
                }`}
              >
                <BarChart2 className="h-4 w-4" />
                <span>{t("tabs.management", "إدارة المبيعات والتقارير")}</span>
              </button>
            </nav>
          </div>
        </div>
      </div>

      {/* Main Tab Content Canvas */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-6">
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
