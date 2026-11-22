import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { HR_DESTINATIONS } from "../../app/router/destination-registry.ts";
const HrPage = lazyNamed(() => import("./pages/hr-page.tsx"), "HrPage");

const hrNavigation = {
  id: "hr",
  moduleIds: ["hr"],
  path: "/hr",
  navigationKey: "hr",
  permissions: { anyOf: ["hr.home.read", "hr.employee.read_basic", "hr.employee.read_self", "hr.employee.read_team", "hr.attendance.read_self", "hr.leave.request_self"] },
  destinations: HR_DESTINATIONS,
  icon: "users"
} as const;

export const hrRoutes: ModuleRouteRegistration = {
  navigation: hrNavigation,
  routes: [
    { path: "hr", element: <ModuleRootRoute module={hrNavigation} overviewPermission={{ anyOf: ["hr.home.read", "hr.employee.read_basic", "hr.employee.read_self", "hr.employee.read_team"] }} defaultElement={<HrPage />} /> },
    { path: "hr/directory", element: <PermissionGuard permission={{ anyOf: ["hr.employee.read_basic", "hr.employee.create", "hr.employee.manage"] }} moduleId="hr" mode="mixed"><HrPage initialTab="directory" /></PermissionGuard> },
    { path: "hr/attendance", element: <PermissionGuard permission={{ anyOf: ["hr.attendance.read", "hr.attendance.read_self", "hr.attendance.manage", "hr.attendance.correct"] }} moduleId="hr" mode="mixed"><HrPage initialTab="attendance" /></PermissionGuard> },
    { path: "hr/leave", element: <PermissionGuard permission={{ anyOf: ["hr.leave.read", "hr.leave.read_self", "hr.leave.read_team", "hr.leave.request_self", "hr.leave.approve_team", "hr.leave.approve"] }} moduleId="hr" mode="mixed"><HrPage initialTab="leave" /></PermissionGuard> },
    { path: "hr/organization", element: <PermissionGuard permission={{ anyOf: ["hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.assignment.read", "hr.assignment.manage"] }} moduleId="hr" mode="mixed"><HrPage initialTab="organization" /></PermissionGuard> },
    { path: "hr/self-service", element: <PermissionGuard permission={{ anyOf: ["hr.employee.read_self", "hr.leave.read_self", "hr.leave.request_self", "hr.attendance.read_self", "hr.employee.document.read_self"] }} moduleId="hr" mode="mixed"><HrPage initialTab="selfservice" /></PermissionGuard> },
    { path: "hr/manager", element: <PermissionGuard permission={{ anyOf: ["hr.employee.read_team", "hr.leave.read_team", "hr.leave.approve_team", "hr.attendance.manage"] }} moduleId="hr" mode="mixed"><HrPage initialTab="manager" /></PermissionGuard> },
    { path: "hr/reports", element: <PermissionGuard permission={{ anyOf: ["hr.report.read", "hr.audit.read", "hr.export"] }} moduleId="hr" mode="read"><HrPage initialTab="reports" /></PermissionGuard> },
    { path: "hr/imports", element: <PermissionGuard permission="hr.employee.import" moduleId="hr" mode="mutation"><HrPage initialTab="imports" /></PermissionGuard> }
  ]
};
