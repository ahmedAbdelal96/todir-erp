import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { BANKING_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { BankingPage } from "./pages/banking-page.tsx";
import { ReconciliationWorkbenchPage } from "./pages/reconciliation-workbench-page.tsx";

const bankingModulePermissions: PermissionCondition = {
  anyOf: [
    "banking.accounts.read",
    "banking.accounts.create",
    "banking.accounts.edit",
    "banking.statements.read",
    "banking.statements.import",
    "banking.statements.validate",
    "banking.reconciliation.read",
    "banking.reconciliation.match",
    "banking.reconciliation.finalize",
    "banking.cash.read",
    "banking.cash.count",
    "banking.cash.difference.resolve",
    "banking.transfers.read",
    "banking.transfers.initiate",
    "banking.cash_position.read",
    "banking.cash_forecast.read",
    "banking.cash_forecast.manage",
    "banking.reports.read",
    "banking.audit.read"
  ]
};

export const bankingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "banking",
    moduleIds: ["banking"],
    path: "/banking",
    navigationKey: "banking",
    category: "finance",
    destinations: BANKING_DESTINATIONS,
    permissions: bankingModulePermissions,
    icon: "banking"
  },
  routes: [
    {
      path: "banking",
      element: (
        <PermissionGuard permission={bankingModulePermissions} moduleId="banking">
          <BankingPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/accounts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.accounts.read", "banking.accounts.create", "banking.accounts.edit"] }} moduleId="banking">
          <BankingPage initialTab="accounts" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/statements",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.statements.read", "banking.statements.import", "banking.statements.validate"] }} moduleId="banking">
          <BankingPage initialTab="statements" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking">
          <BankingPage initialTab="reconciliation" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations/:sessionId/workbench",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking">
          <ReconciliationWorkbenchPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] }} moduleId="banking">
          <BankingPage initialTab="cash" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash-counts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] }} moduleId="banking">
          <BankingPage initialTab="cash" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/transfers",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.transfers.read", "banking.transfers.initiate"] }} moduleId="banking">
          <BankingPage initialTab="transfers" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/forecast",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash_forecast.read", "banking.cash_forecast.manage"] }} moduleId="banking">
          <BankingPage initialTab="forecast" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reports",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reports.read", "banking.audit.read"] }} moduleId="banking">
          <BankingPage initialTab="reports" />
        </PermissionGuard>
      )
    }
  ]
};
