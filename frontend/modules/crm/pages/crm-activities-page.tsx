import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { hasPermissionInAccess } from "../../../core/runtime/effective-access.ts";
import { CrmActivitiesTab } from "../features/activities/crm-activities-tab.tsx";

export function CrmActivitiesPage(): React.JSX.Element {
  const { t } = useTranslation("crm");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const effectiveAccess = outletCtx?.effectiveAccess;
  const permissions = useOptionalPermissions() || outletCtx?.permissions;
  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";

  const canManage = effectiveAccess?.isSuper ||
    hasPermissionInAccess(effectiveAccess, "crm.team.manage") ||
    permissions?.hasPermission("crm.team.manage") || false;

  return (
    <div
      data-testid="crm-activities-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        minWidth: 0
      }}
    >
      <header
        style={{
          marginBottom: "1.25rem",
          paddingBottom: "1rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <nav  style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
          <Link to="/crm" style={{ color: "inherit", textDecoration: "none" }}>
            {t("tabs.home", "إدارة علاقات العملاء")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.activities", "الأنشطة والمتابعات")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.activities", "الأنشطة والمتابعات")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("activities.subtitle", "جدولة المكالمات والاجتماعات والمهام ومتابعة تنفيذها مع العملاء")}
        </p>
      </header>

      <main>
        <CrmActivitiesTab legalEntityId={legalEntityId} canManage={canManage} />
      </main>
    </div>
  );
}
