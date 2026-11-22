import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { SERVICE_DESTINATIONS } from "../../app/router/destination-registry.ts";
const ServicePage = lazyNamed(() => import("./pages/service-page.tsx"), "ServicePage");

const serviceNavigation = {
  id: "service",
  moduleIds: ["service"],
  path: "/service",
  navigationKey: "service",
  permissions: { anyOf: ["service.request.read", "service.work.read", "service.dispatch.read", "service.installed_base.read"] },
  destinations: SERVICE_DESTINATIONS,
  icon: "service"
} as const;

export const serviceRoutes: ModuleRouteRegistration = {
  navigation: serviceNavigation,
  routes: [
    { path: "service", element: <ModuleRootRoute module={serviceNavigation} overviewPermission={{ anyOf: ["service.request.read", "service.work.read", "service.dispatch.read", "service.installed_base.read"] }} defaultElement={<ServicePage />} /> },
    { path: "service/requests", element: <PermissionGuard permission={{ anyOf: ["service.request.read", "service.request.create", "service.request.triage", "service.request.resolve"] }} moduleId="service" mode="mixed"><ServicePage initialTab="requests" /></PermissionGuard> },
    { path: "service/work-orders", element: <PermissionGuard permission={{ anyOf: ["service.work.read", "service.work.create", "service.work.plan", "service.work.schedule", "service.work.execute", "service.work.complete", "service.work.close"] }} moduleId="service" mode="mixed"><ServicePage initialTab="work-orders" /></PermissionGuard> },
    { path: "service/dispatch", element: <PermissionGuard permission={{ anyOf: ["service.dispatch.read", "service.dispatch.manage"] }} moduleId="service" mode="mixed"><ServicePage initialTab="dispatch" /></PermissionGuard> },
    { path: "service/my-day", element: <PermissionGuard permission={{ anyOf: ["service.work.read", "service.work.execute", "service.dispatch.read"] }} moduleId="service" mode="read"><ServicePage initialTab="my-day" /></PermissionGuard> },
    { path: "service/installed-base", element: <PermissionGuard permission={{ anyOf: ["service.installed_base.read", "service.installed_base.manage"] }} moduleId="service" mode="mixed"><ServicePage initialTab="installed-base" /></PermissionGuard> },
    { path: "service/preventive", element: <PermissionGuard permission={{ anyOf: ["service.maintenance_plan.read", "service.maintenance_plan.manage"] }} moduleId="service" mode="mixed"><ServicePage initialTab="preventive" /></PermissionGuard> },
    { path: "service/contracts-warranty", element: <PermissionGuard permission={{ anyOf: ["service.contract.read", "service.contract.manage", "service.warranty.read", "service.warranty.manage"] }} moduleId="service" mode="mixed"><ServicePage initialTab="contracts-warranty" /></PermissionGuard> },
    { path: "service/billing", element: <PermissionGuard permission={{ anyOf: ["service.billing.read", "service.billing.manage", "service.billing.writeoff", "service.billing.request_invoice"] }} moduleId="service" mode="mixed"><ServicePage initialTab="billing" /></PermissionGuard> }
  ]
};
