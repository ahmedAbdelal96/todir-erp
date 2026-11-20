import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { CRM_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { CrmPage } from "./pages/crm-page.tsx";

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

export const crmRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "crm",
    moduleIds: ["crm"],
    path: "/crm",
    navigationKey: "crm",
    category: "sales",
    destinations: CRM_DESTINATIONS,
    permissions: crmModulePermissions,
    icon: "crm"
  },
  routes: [
    {
      path: "crm",
      element: (
        <PermissionGuard permission={crmModulePermissions} moduleId="crm">
          <CrmPage />
        </PermissionGuard>
      )
    },
    {
      path: "crm/leads",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.lead.read", "crm.lead.create", "crm.lead.edit"] }} moduleId="crm">
          <CrmPage initialTab="leads" />
        </PermissionGuard>
      )
    },
    {
      path: "crm/opportunities",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.opportunity.read", "crm.opportunity.create", "crm.opportunity.edit"] }} moduleId="crm">
          <CrmPage initialTab="opportunities" />
        </PermissionGuard>
      )
    },
    {
      path: "crm/activities",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.activity.read", "crm.activity.create", "crm.activity.edit"] }} moduleId="crm">
          <CrmPage initialTab="activities" />
        </PermissionGuard>
      )
    },
    {
      path: "crm/management",
      element: (
        <PermissionGuard permission={{ anyOf: ["crm.team.manage", "crm.pipeline.manage", "crm.team.read", "crm.pipeline.read", "crm.forecast.read"] }} moduleId="crm">
          <CrmPage initialTab="management" />
        </PermissionGuard>
      )
    }
  ]
};
