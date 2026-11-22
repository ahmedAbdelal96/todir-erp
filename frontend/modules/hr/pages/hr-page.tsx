import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useOutletContext, useSearchParams } from "react-router-dom";
import { HrTabNav, type HrTabId, type HrTabDef } from "../components/hr-tab-nav.tsx";
import { HrHomeTab } from "../features/home/hr-home-tab.tsx";
import { EmployeeDirectoryTab } from "../features/directory/employee-directory-tab.tsx";
import { EmployeeWorkspace } from "../features/workspace/employee-workspace.tsx";
import { AttendanceTab } from "../features/attendance/attendance-tab.tsx";
import { LeaveTab } from "../features/leave/leave-tab.tsx";
import { OrganizationTab } from "../features/organization/organization-tab.tsx";
import { SelfServiceTab } from "../features/selfservice/self-service-tab.tsx";
import { ManagerTab } from "../features/manager/manager-tab.tsx";
import { ReportsTab } from "../features/reports/reports-tab.tsx";
import { ImportsTab } from "../features/imports/imports-tab.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { HR_DESTINATIONS, isDestinationPermitted } from "../../../app/router/destination-registry.ts";
import { buildCanonicalTaskPath } from "../../../app/router/legacy-task-navigation.ts";
import { hasAnyPermissionInAccess } from "../../../core/runtime/effective-access.ts";

export interface HrPageProps {
  readonly initialTab?: HrTabId | undefined;
}

export function HrPage({ initialTab }: HrPageProps = {}): React.JSX.Element {
  const { t } = useTranslation("hr");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId;
  } catch {
    // Outside RuntimeProvider
  }

  const legalEntityId = outletCtx?.legalEntityId || contextEntityId || null;

  // Tab & Employee State derived from searchParams (Single Source of Truth)
  const activeTab = initialTab ?? (searchParams.get("tab") as HrTabId | null) ?? "home";
  const canReadEmployeeDetails = hasAnyPermissionInAccess(
    outletCtx?.effectiveAccess,
    "hr.employee.read_basic",
    "hr.employee.manage"
  );
  const canOpenEmployeeWorkspace = activeTab === "directory" && canReadEmployeeDetails;
  const selectedEmployeeId = canOpenEmployeeWorkspace ? searchParams.get("employeeId") : null;

  useEffect(() => {
    if (!canOpenEmployeeWorkspace && searchParams.has("employeeId")) {
      const next = new URLSearchParams(searchParams);
      next.delete("employeeId");
      setSearchParams(next, { replace: true });
    }
  }, [canOpenEmployeeWorkspace, searchParams, setSearchParams]);

  // Help Modal State
  const [isHelpOpen, setIsHelpOpen] = useState(false);

  // Modal Control from Home
  const [isNewEmployeeOpen, setIsNewEmployeeOpen] = useState(false);

  const handleTabChange = (tabId: HrTabId) => {
    const target = buildCanonicalTaskPath(HR_DESTINATIONS, "/hr", tabId, searchParams.toString());
    if (target) {
      navigate(target);
    }
  };

  const handleSelectEmployee = (id: string) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("employeeId", id);
      return next;
    });
  };

  const handleBackToDirectory = () => {
    navigate("/hr/directory");
  };

  const tabs: readonly HrTabDef[] = [
    { id: "home", label: t("tabs.home") },
    { id: "directory", label: t("tabs.directory") },
    { id: "attendance", label: t("tabs.attendance") },
    { id: "leave", label: t("tabs.leave") },
    { id: "organization", label: t("tabs.organization") },
    { id: "selfservice", label: t("tabs.selfservice") },
    { id: "manager", label: t("tabs.manager") },
    { id: "reports", label: t("tabs.reports") },
    { id: "imports", label: t("tabs.imports") }
  ];

  const visibleTabs = tabs.filter(tab => {
    const destination = HR_DESTINATIONS.find(item => item.path.endsWith(`/${tab.id === "selfservice" ? "self-service" : tab.id}`) || (tab.id === "home" && item.path === "/hr"));
    return Boolean(destination && outletCtx?.effectiveAccess && isDestinationPermitted(destination, outletCtx.effectiveAccess));
  });

  if (!legalEntityId) {
    return <div role="alert" style={{ margin: "1.5rem", padding: "2rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", background: "var(--color-surface-card)", color: "var(--color-content-primary)" }}>{t("page.contextRequiredDescription", "اختر الكيان القانوني للمتابعة.")}</div>;
  }

  return (
    <div data-testid="hr-page" style={{ padding: "1.5rem", maxWidth: "1400px", margin: "0 auto", display: "flex", flexDirection: "column", gap: "1rem" }}>
      {/* Top Header Bar with Contextual Help */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h1 style={{ margin: 0, fontSize: "1.5rem", color: "var(--color-content-primary)", fontWeight: 700 }}>
            {t("title")}
          </h1>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("subtitle")}
          </p>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <Button
            variant="secondary"
            size="compact"
            onPress={() => setIsHelpOpen(true)}
            aria-label={t("helpButton")}
          >
            ? {t("helpButton")}
          </Button>
        </div>
      </div>

      {/* Primary Tab Navigation */}
      {!selectedEmployeeId && (
        <HrTabNav
          activeTab={activeTab}
          onTabChange={handleTabChange}
          tabs={visibleTabs}
        />
      )}

      {/* Main Tab Views or 360-Degree Workspace */}
      {selectedEmployeeId ? (
        <EmployeeWorkspace
          employeeId={selectedEmployeeId}
          legalEntityId={legalEntityId}
          onBack={handleBackToDirectory}
        />
      ) : (
        <>
          {activeTab === "home" && (
            <HrHomeTab
              legalEntityId={legalEntityId}
              onNavigateTab={(tab) => handleTabChange(tab)}
              onOpenNewEmployee={() => {
                handleTabChange("directory");
                setIsNewEmployeeOpen(true);
              }}
            />
          )}

          {activeTab === "directory" && (
            <EmployeeDirectoryTab
              legalEntityId={legalEntityId}
              onSelectEmployee={handleSelectEmployee}
              isNewModalOpen={isNewEmployeeOpen}
              onOpenNewModal={() => setIsNewEmployeeOpen(true)}
              onCloseNewModal={() => setIsNewEmployeeOpen(false)}
            />
          )}

          {activeTab === "attendance" && (
            <AttendanceTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "leave" && (
            <LeaveTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "organization" && (
            <OrganizationTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "selfservice" && (
            <SelfServiceTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "manager" && (
            <ManagerTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "reports" && (
            <ReportsTab legalEntityId={legalEntityId} />
          )}

          {activeTab === "imports" && (
            <ImportsTab legalEntityId={legalEntityId} />
          )}
        </>
      )}

      {/* Contextual Help Modal Dialog */}
      <Dialog isOpen={isHelpOpen} onOpenChange={setIsHelpOpen} title={t("helpTitle")} size="medium">
        <p style={{ margin: 0, fontSize: "0.875rem", lineHeight: 1.6, color: "var(--color-content-muted)" }}>
          {t("helpDescription")}
        </p>
        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "1.25rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
          <Button variant="primary" onPress={() => setIsHelpOpen(false)}>
            {t("actions.close")}
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
