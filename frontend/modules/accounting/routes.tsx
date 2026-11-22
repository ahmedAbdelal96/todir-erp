import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { ACCOUNTING_DESTINATIONS } from "../../app/router/destination-registry.ts";
const AccountingPage = lazyNamed(() => import("./pages/accounting-page.tsx"), "AccountingPage");
const AccountingJournalsPage = lazyNamed(() => import("./pages/accounting-journals-page.tsx"), "AccountingJournalsPage");
const AccountingLedgerPage = lazyNamed(() => import("./pages/accounting-ledger-page.tsx"), "AccountingLedgerPage");
const AccountingTrialBalancePage = lazyNamed(() => import("./pages/accounting-trial-balance-page.tsx"), "AccountingTrialBalancePage");
const AccountingStatementsPage = lazyNamed(() => import("./pages/accounting-statements-page.tsx"), "AccountingStatementsPage");
const AccountingPeriodsPage = lazyNamed(() => import("./pages/accounting-periods-page.tsx"), "AccountingPeriodsPage");
const AccountingReconciliationPage = lazyNamed(() => import("./pages/accounting-reconciliation-page.tsx"), "AccountingReconciliationPage");
const AccountingRevaluationPage = lazyNamed(() => import("./pages/accounting-revaluation-page.tsx"), "AccountingRevaluationPage");

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

const accountingNavigation = {
  id: "accounting" as const,
  moduleIds: ["accounting", "ledger"] as const,
  path: "/accounting",
  navigationKey: "accounting" as const,
  category: "finance" as const,
  destinations: ACCOUNTING_DESTINATIONS,
  permissions: accountingModulePermissions,
  icon: "accounting"
};

export const accountingRoutes: ModuleRouteRegistration = {
  navigation: accountingNavigation,
  routes: [
    {
      path: "accounting",
      element: (
        <ModuleRootRoute
          module={accountingNavigation}
          overviewPermission={ACCOUNTING_DESTINATIONS[0]?.permission}
          defaultElement={<AccountingPage />}
        />
      )
    },
    {
      path: "accounting/journals",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["ledger.journal.read", "ledger.journal.manage", "ledger.general_journal.post"] }}
          moduleId="ledger"
          mode="mixed"
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
          mode="read"
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
          mode="read"
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
          mode="mixed"
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
          moduleId="ledger"
          mode="mixed"
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
          mode="mixed"
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
          mode="mixed"
        >
          <AccountingRevaluationPage />
        </PermissionGuard>
      )
    }
  ]
};
