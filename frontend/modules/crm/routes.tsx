import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { CRM_DESTINATIONS } from "../../app/router/destination-registry.ts";
const CrmPage = lazyNamed(() => import("./pages/crm-page.tsx"), "CrmPage");
const CrmLeadsPage = lazyNamed(() => import("./pages/crm-leads-page.tsx"), "CrmLeadsPage");
const CrmOpportunitiesPage = lazyNamed(() => import("./pages/crm-opportunities-page.tsx"), "CrmOpportunitiesPage");
const CrmActivitiesPage = lazyNamed(() => import("./pages/crm-activities-page.tsx"), "CrmActivitiesPage");
const CrmManagementPage = lazyNamed(() => import("./pages/crm-management-page.tsx"), "CrmManagementPage");

const crmModulePermissions: PermissionCondition = {
  anyOf: [
    "crm.home.read",
    "crm.lead.read",
    "crm.lead.create",
    "crm.lead.edit",
    "crm.opportunity.read",
    "crm.opportunity.create",
    "crm.opportunity.edit",
    "crm.activity.read",
    "crm.activity.create",
    "crm.activity.edit",
    "crm.team.read",
    "crm.team.manage",
    "crm.pipeline.manage"
  ]
};

const crmNavigation = {
  id: "crm" as const,
  moduleIds: ["crm"] as const,
  path: "/crm",
  navigationKey: "crm" as const,
  category: "sales" as const,
  destinations: CRM_DESTINATIONS,
  permissions: crmModulePermissions,
  icon: "crm"
};

export const crmRoutes: ModuleRouteRegistration = {
  navigation: crmNavigation,
  routes: [
    {
      path: "crm",
      element: (
        <ModuleRootRoute
          module={crmNavigation}
          overviewPermission={CRM_DESTINATIONS[0]?.permission}
          defaultElement={<CrmPage />}
        />
      )
    },
    {
      path: "crm/leads",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.lead.read", "crm.lead.create", "crm.lead.edit"] }} moduleId="crm" mode="mixed">
          <CrmLeadsPage />
        </PermissionGuard>
      )
    },
    {
      path: "crm/opportunities",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.opportunity.read", "crm.opportunity.create", "crm.opportunity.edit"] }} moduleId="crm" mode="mixed">
          <CrmOpportunitiesPage />
        </PermissionGuard>
      )
    },
    {
      path: "crm/activities",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.activity.read", "crm.activity.create", "crm.activity.edit"] }} moduleId="crm" mode="mixed">
          <CrmActivitiesPage />
        </PermissionGuard>
      )
    },
    {
      path: "crm/management",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.team.manage", "crm.pipeline.manage", "crm.team.read", "crm.pipeline.read", "crm.forecast.read"] }} moduleId="crm" mode="mixed">
          <CrmManagementPage />
        </PermissionGuard>
      )
    }
  ]
};
