import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { BANKING_DESTINATIONS } from "../../app/router/destination-registry.ts";
const BankingPage = lazyNamed(() => import("./pages/banking-page.tsx"), "BankingPage");
const BankingAccountsPage = lazyNamed(() => import("./pages/banking-accounts-page.tsx"), "BankingAccountsPage");
const BankingStatementsPage = lazyNamed(() => import("./pages/banking-statements-page.tsx"), "BankingStatementsPage");
const BankingReconciliationsPage = lazyNamed(() => import("./pages/banking-reconciliations-page.tsx"), "BankingReconciliationsPage");
const ReconciliationWorkbenchPage = lazyNamed(() => import("./pages/reconciliation-workbench-page.tsx"), "ReconciliationWorkbenchPage");
const BankingCashPage = lazyNamed(() => import("./pages/banking-cash-page.tsx"), "BankingCashPage");
const BankingTransfersPage = lazyNamed(() => import("./pages/banking-transfers-page.tsx"), "BankingTransfersPage");
const BankingForecastPage = lazyNamed(() => import("./pages/banking-forecast-page.tsx"), "BankingForecastPage");
const BankingReportsPage = lazyNamed(() => import("./pages/banking-reports-page.tsx"), "BankingReportsPage");

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

const bankingNavigation = {
  id: "banking" as const,
  moduleIds: ["banking"] as const,
  path: "/banking",
  navigationKey: "banking" as const,
  category: "finance" as const,
  destinations: BANKING_DESTINATIONS,
  permissions: bankingModulePermissions,
  icon: "banking"
};

export const bankingRoutes: ModuleRouteRegistration = {
  navigation: bankingNavigation,
  routes: [
    {
      path: "banking",
      element: (
        <ModuleRootRoute
          module={bankingNavigation}
          overviewPermission={BANKING_DESTINATIONS[0]?.permission}
          defaultElement={<BankingPage />}
        />
      )
    },
    {
      path: "banking/accounts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.accounts.read", "banking.accounts.create", "banking.accounts.edit"] }} moduleId="banking" mode="mixed">
          <BankingAccountsPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/statements",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.statements.read", "banking.statements.import", "banking.statements.validate"] }} moduleId="banking" mode="mixed">
          <BankingStatementsPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking" mode="mixed">
          <BankingReconciliationsPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations/:sessionId",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking" mode="mixed">
          <ReconciliationWorkbenchPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations/:sessionId/workbench",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] }} moduleId="banking" mode="mixed">
          <ReconciliationWorkbenchPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] }} moduleId="banking" mode="mixed">
          <BankingCashPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash-counts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] }} moduleId="banking" mode="mixed">
          <BankingCashPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/transfers",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.transfers.read", "banking.transfers.initiate"] }} moduleId="banking" mode="mixed">
          <BankingTransfersPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/forecast",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash_forecast.read", "banking.cash_forecast.manage"] }} moduleId="banking" mode="read">
          <BankingForecastPage />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reports",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reports.read", "banking.audit.read"] }} moduleId="banking" mode="read">
          <BankingReportsPage />
        </PermissionGuard>
      )
    }
  ]
};
