import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { BANKING_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { BankingPage } from "./pages/banking-page.tsx";

const bankingModulePermissions: PermissionCondition = {
  anyOf: [
    "banking.accounts.read",
    "banking.accounts.manage",
    "banking.statement.read",
    "banking.statement.import",
    "banking.statement.validate",
    "banking.reconciliation.read",
    "banking.reconciliation.manage",
    "banking.reconciliation.finalize",
    "banking.cash.count",
    "banking.cash.transfer",
    "banking.cash_forecast.manage",
    "banking.reports.read"
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
        <PermissionGuard permission={{ anyOf: ["banking.accounts.read", "banking.accounts.manage"] }} moduleId="banking">
          <BankingPage initialTab="accounts" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/statements",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.statement.read", "banking.statement.import", "banking.statement.validate"] }} moduleId="banking">
          <BankingPage initialTab="statements" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.read", "banking.reconciliation.manage", "banking.reconciliation.finalize"] }} moduleId="banking">
          <BankingPage initialTab="reconciliation" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reconciliations/:sessionId/workbench",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reconciliation.manage", "banking.reconciliation.finalize"] }} moduleId="banking">
          <BankingPage initialTab="reconciliation" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.count", "banking.cash.transfer"] }} moduleId="banking">
          <BankingPage initialTab="cash" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/cash-counts",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.count", "banking.cash.transfer"] }} moduleId="banking">
          <BankingPage initialTab="cash" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/transfers",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash.transfer", "banking.accounts.manage", "payments.read"] }} moduleId="banking">
          <BankingPage initialTab="transfers" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/forecast",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.cash_forecast.manage", "banking.accounts.read"] }} moduleId="banking">
          <BankingPage initialTab="forecast" />
        </PermissionGuard>
      )
    },
    {
      path: "banking/reports",
      element: (
        <PermissionGuard permission={{ anyOf: ["banking.reports.read", "banking.accounts.read"] }} moduleId="banking">
          <BankingPage initialTab="reports" />
        </PermissionGuard>
      )
    }
  ]
};
