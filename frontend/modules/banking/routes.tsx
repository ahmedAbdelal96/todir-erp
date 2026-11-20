import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { BANKING_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { BankingPage } from "./pages/banking-page.tsx";
import { BankingAccountsPage } from "./pages/banking-accounts-page.tsx";
import { BankingStatementsPage } from "./pages/banking-statements-page.tsx";
import { BankingReconciliationsPage } from "./pages/banking-reconciliations-page.tsx";
import { ReconciliationWorkbenchPage } from "./pages/reconciliation-workbench-page.tsx";
import { BankingCashPage } from "./pages/banking-cash-page.tsx";
import { BankingTransfersPage } from "./pages/banking-transfers-page.tsx";
import { BankingForecastPage } from "./pages/banking-forecast-page.tsx";
import { BankingReportsPage } from "./pages/banking-reports-page.tsx";

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
          <BankingAccountsPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/statements",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.statements.read", "banking.statements.import", "banking.statements.validate"] }} moduleId="banking">
          <BankingStatementsPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking">
          <BankingReconciliationsPage />
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
          <BankingCashPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash-counts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] }} moduleId="banking">
          <BankingCashPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/transfers",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.transfers.read", "banking.transfers.initiate"] }} moduleId="banking">
          <BankingTransfersPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/forecast",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash_forecast.read", "banking.cash_forecast.manage"] }} moduleId="banking">
          <BankingForecastPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reports",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reports.read", "banking.audit.read"] }} moduleId="banking">
          <BankingReportsPage />
        </PermissionGuard>
      )
    }
  ]
};
