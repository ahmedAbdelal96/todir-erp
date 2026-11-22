import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { EXPENSES_DESTINATIONS } from "../../app/router/destination-registry.ts";
const ExpensesPage = lazyNamed(() => import("./pages/expenses-page.tsx"), "ExpensesPage");

const expensesNavigation = {
  id: "expenses",
  moduleIds: ["expenses"],
  path: "/expenses",
  navigationKey: "expenses",
  permissions: {
    anyOf: ["expenses.read", "expenses.create", "expenses.submit", "expenses.approve", "expenses.finalize", "expenses.reimbursement.read", "expenses.reimbursement.manage", "expenses.audit.read", "expenses.category.manage"]
  },
  destinations: EXPENSES_DESTINATIONS,
  icon: "expenses"
} as const;

export const expensesRoutes: ModuleRouteRegistration = {
  navigation: expensesNavigation,
  routes: [
    { path: "expenses", element: <ModuleRootRoute module={expensesNavigation} overviewPermission={{ anyOf: ["expenses.read", "expenses.create", "expenses.reimbursement.read"] }} defaultElement={<ExpensesPage />} /> },
    { path: "expenses/self-service", element: <PermissionGuard permission={{ anyOf: ["expenses.read", "expenses.create", "expenses.submit", "expenses.reimbursement.read"] }} moduleId="expenses" mode="mixed"><ExpensesPage initialTab="self-service" /></PermissionGuard> },
    { path: "expenses/review", element: <PermissionGuard permission={{ anyOf: ["expenses.approve", "expenses.finalize", "expenses.audit.read"] }} moduleId="expenses" mode="mixed"><ExpensesPage initialTab="review" /></PermissionGuard> },
    { path: "expenses/advances", element: <PermissionGuard permission={{ anyOf: ["expenses.reimbursement.manage", "expenses.approve", "expenses.finalize"] }} moduleId="expenses" mode="mixed"><ExpensesPage initialTab="advances" /></PermissionGuard> },
    { path: "expenses/corporate-cards", element: <PermissionGuard permission={{ anyOf: ["expenses.read", "expenses.approve", "expenses.finalize"] }} moduleId="expenses" mode="mixed"><ExpensesPage initialTab="corporate-cards" /></PermissionGuard> },
    { path: "expenses/reimbursements", element: <PermissionGuard permission={{ anyOf: ["expenses.reimbursement.read", "expenses.reimbursement.manage", "expenses.finalize"] }} moduleId="expenses" mode="mixed"><ExpensesPage initialTab="reimbursements" /></PermissionGuard> },
    { path: "expenses/reports", element: <PermissionGuard permission={{ anyOf: ["expenses.audit.read", "expenses.read"] }} moduleId="expenses" mode="read"><ExpensesPage initialTab="reports" /></PermissionGuard> },
    { path: "expenses/settings", element: <PermissionGuard permission="expenses.category.manage" moduleId="expenses" mode="mutation"><ExpensesPage initialTab="settings" /></PermissionGuard> }
  ]
};
