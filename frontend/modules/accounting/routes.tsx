import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ACCOUNTING_DESTINATIONS } from "../../app/router/destination-registry.ts";
import { AccountingPage } from "./pages/accounting-page.tsx";
import { AccountingJournalsPage } from "./pages/accounting-journals-page.tsx";
import { AccountingLedgerPage } from "./pages/accounting-ledger-page.tsx";
import { AccountingTrialBalancePage } from "./pages/accounting-trial-balance-page.tsx";
import { AccountingStatementsPage } from "./pages/accounting-statements-page.tsx";
import { AccountingPeriodsPage } from "./pages/accounting-periods-page.tsx";
import { AccountingReconciliationPage } from "./pages/accounting-reconciliation-page.tsx";
import { AccountingRevaluationPage } from "./pages/accounting-revaluation-page.tsx";

const accountingModulePermissions: PermissionCondition = {
  anyOf: [
    "ledger.account.read",
    "ledger.account.manage",
    "ledger.journal.read",
    "ledger.journal.manage",
    "ledger.general_journal.post",
    "ledger.reports.read",
    "ledger.period.read",
    "ledger.period.manage",
    "accounting.home.read",
    "accounting.health.read",
    "accounting.statement.read",
    "accounting.statement.manage",
    "accounting.reconciliation.read",
    "accounting.reconciliation.run",
    "accounting.close.execute",
    "accounting.fx-revaluation.read",
    "accounting.fx-revaluation.post"
  ]
};

export const accountingRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "accounting",
    moduleIds: ["accounting", "ledger"],
    path: "/accounting",
    navigationKey: "accounting",
    category: "finance",
    destinations: ACCOUNTING_DESTINATIONS,
    permissions: accountingModulePermissions,
    icon: "accounting"
  },
  routes: [
    {
      path: "accounting",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["ledger.account.read", "ledger.journal.read", "accounting.home.read"] }}
          moduleIds={["accounting", "ledger"]}
        >
          <AccountingPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/journals",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["ledger.journal.read", "ledger.journal.manage", "ledger.general_journal.post"] }}
          moduleId="ledger"
        >
          <AccountingJournalsPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/ledger",
      element: (
        <PermissionGuard
          permission="ledger.reports.read"
          moduleId="ledger"
        >
          <AccountingLedgerPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/trial-balance",
      element: (
        <PermissionGuard
          permission="ledger.reports.read"
          moduleId="ledger"
        >
          <AccountingTrialBalancePage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/statements",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["accounting.statement.read", "accounting.statement.manage"] }}
          moduleId="accounting"
        >
          <AccountingStatementsPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/periods",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["ledger.period.read", "ledger.period.manage", "accounting.close.execute"] }}
          moduleIds={["accounting", "ledger"]}
        >
          <AccountingPeriodsPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/reconciliation",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["accounting.reconciliation.read", "accounting.reconciliation.run"] }}
          moduleId="accounting"
        >
          <AccountingReconciliationPage />
        </PermissionGuard>
      )
    },
    {
      path: "accounting/revaluation",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["accounting.fx-revaluation.read", "accounting.fx-revaluation.post"] }}
          moduleId="accounting"
        >
          <AccountingRevaluationPage />
        </PermissionGuard>
      )
    }
  ]
};
