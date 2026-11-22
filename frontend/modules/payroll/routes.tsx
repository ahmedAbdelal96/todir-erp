import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { PAYROLL_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { PayrollPage } from "./pages/payroll-page.tsx";

const payrollNavigation = {
  id: "payroll",
  moduleIds: ["payroll"],
  path: "/payroll",
  navigationKey: "payroll",
  permissions: { anyOf: ["payroll.home.read", "payroll.run.read", "payroll.payslip.read_self", "payroll.payslip.read", "payroll.input.read"] },
  destinations: PAYROLL_DESTINATIONS,
  icon: "banknotes"
} as const;

export const payrollRoutes: ModuleRouteRegistration = {
  navigation: payrollNavigation,
  routes: [
    { path: "payroll", element: <ModuleRootRoute module={payrollNavigation} overviewPermission={{ anyOf: ["payroll.home.read", "payroll.run.read", "payroll.payslip.read_self", "payroll.payslip.read"] }} defaultElement={<PayrollPage />} /> },
    { path: "payroll/runs", element: <PermissionGuard permission={{ anyOf: ["payroll.run.read", "payroll.run.create", "payroll.run.calculate", "payroll.run.review", "payroll.run.approve", "payroll.run.finalize"] }} moduleId="payroll" mode="mixed"><PayrollPage initialTab="runs" /></PermissionGuard> },
    { path: "payroll/inputs", element: <PermissionGuard permission={{ anyOf: ["payroll.input.read", "payroll.input.create", "payroll.input.approve"] }} moduleId="payroll" mode="mixed"><PayrollPage initialTab="inputs" /></PermissionGuard> },
    { path: "payroll/loans", element: <PermissionGuard permission={{ anyOf: ["payroll.loan.read", "payroll.loan.create", "payroll.loan.approve", "payroll.loan.reschedule", "payroll.loan.writeoff"] }} moduleId="payroll" mode="mixed"><PayrollPage initialTab="loans" /></PermissionGuard> },
    { path: "payroll/payslips", element: <PermissionGuard permission={{ anyOf: ["payroll.payslip.read", "payroll.payslip.read_self", "payroll.payslip.export"] }} moduleId="payroll" mode="read"><PayrollPage initialTab="payslips" /></PermissionGuard> },
    { path: "payroll/reports", element: <PermissionGuard permission={{ anyOf: ["payroll.compliance_output.read", "payroll.accounting_preview.read", "payroll.audit.read", "payroll.compliance_output.generate"] }} moduleId="payroll" mode="read"><PayrollPage initialTab="reports" /></PermissionGuard> },
    { path: "payroll/setup", element: <PermissionGuard permission={{ anyOf: ["payroll.group.manage", "payroll.structure.manage", "payroll.component.manage", "payroll.assignment.manage"] }} moduleId="payroll" mode="mutation"><PayrollPage initialTab="setup" /></PermissionGuard> },
    { path: "payroll/settings", element: <PermissionGuard permission={{ anyOf: ["payroll.settings.read", "payroll.settings.manage"] }} moduleId="payroll" mode="mixed"><PayrollPage initialTab="settings" /></PermissionGuard> },
    { path: "payroll/opening-balances", element: <PermissionGuard permission={{ anyOf: ["payroll.input.read", "payroll.input.create", "payroll.run.read"] }} moduleId="payroll" mode="mixed"><PayrollPage initialTab="openingBalances" /></PermissionGuard> }
  ]
};
