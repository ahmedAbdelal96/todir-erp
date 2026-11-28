import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PLANNING_DESTINATIONS } from "../../app/router/destination-registry.ts";

const PlanningOverviewPage = lazyNamed(() => import("./pages/planning-overview-page.tsx"), "PlanningOverviewPage");
const PlanningBudgetsPage = lazyNamed(() => import("./pages/planning-budgets-page.tsx"), "PlanningBudgetsPage");
const PlanningGridPage = lazyNamed(() => import("./pages/planning-grid-page.tsx"), "PlanningGridPage");
const PlanningModelsPage = lazyNamed(() => import("./pages/planning-models-page.tsx"), "PlanningModelsPage");
const PlanningForecastsPage = lazyNamed(() => import("./pages/planning-forecasts-page.tsx"), "PlanningForecastsPage");
const PlanningAnalysisPage = lazyNamed(() => import("./pages/planning-analysis-page.tsx"), "PlanningAnalysisPage");
const PlanningBudgetControlPage = lazyNamed(() => import("./pages/planning-budget-control-page.tsx"), "PlanningBudgetControlPage");
const PlanningSubmissionsPage = lazyNamed(() => import("./pages/planning-submissions-page.tsx"), "PlanningSubmissionsPage");
const PlanningConfigPage = lazyNamed(() => import("./pages/planning-config-page.tsx"), "PlanningConfigPage");

const planningModulePermissions: PermissionCondition = {
  anyOf: [
    "planning.cycles.read",
    "planning.cycles.manage",
    "planning.models.read",
    "planning.models.manage",
    "planning.versions.read",
    "planning.versions.edit",
    "planning.versions.submit",
    "planning.versions.approve",
    "planning.versions.publish",
    "planning.scenarios.create",
    "planning.scenarios.share",
    "planning.assumptions.manage",
    "planning.budget.read",
    "planning.budget.manage",
    "planning.budget.revision",
    "planning.budget.transfer",
    "planning.budget.transfer.approve",
    "planning.budget_control.read",
    "planning.budget_control.override",
    "planning.budget_control.override.approve",
    "planning.workforce.read",
    "planning.workforce.sensitive.read",
    "planning.capex.read",
    "planning.capex.manage",
    "planning.variance.read",
    "planning.actuals.refresh",
    "planning.export",
    "planning.audit.read"
  ]
};

const planningNavigation = {
  id: "planning" as const,
  moduleIds: ["planning"] as const,
  path: "/planning",
  navigationKey: "planning" as const,
  category: "finance" as const,
  destinations: PLANNING_DESTINATIONS,
  permissions: planningModulePermissions,
  icon: "account_balance_wallet"
};

export const planningRoutes: ModuleRouteRegistration = {
  navigation: planningNavigation,
  routes: [
    {
      path: "planning",
      element: (
        <ModuleRootRoute
          module={planningNavigation}
          overviewPermission={{ anyOf: ["planning.versions.read", "planning.budget.read", "planning.variance.read", "planning.cycles.read", "planning.models.read", "planning.budget_control.read", "planning.workforce.read", "planning.capex.read"] }}
          defaultElement={<PlanningOverviewPage />}
        />
      )
    },
    {
      path: "planning/overview",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.versions.read", "planning.budget.read", "planning.variance.read", "planning.cycles.read", "planning.models.read", "planning.budget_control.read", "planning.workforce.read", "planning.capex.read"] }}
          moduleId="planning"
          mode="read"
        >
          <PlanningOverviewPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/budgets",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.budget.read", "planning.budget.manage", "planning.budget.revision", "planning.budget.transfer", "planning.cycles.read", "planning.versions.read"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningBudgetsPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/grid",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.versions.read", "planning.versions.edit", "planning.export"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningGridPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/models",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.versions.read", "planning.versions.edit", "planning.workforce.read", "planning.capex.read", "planning.assumptions.manage"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningModelsPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/forecasts",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.versions.read", "planning.scenarios.create", "planning.scenarios.share", "planning.versions.edit"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningForecastsPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/analysis",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.variance.read", "planning.versions.read", "planning.actuals.refresh"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningAnalysisPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/budget-control",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.budget_control.read", "planning.budget.manage", "planning.budget_control.override", "planning.budget_control.override.approve"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningBudgetControlPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/submissions",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.versions.read", "planning.versions.edit", "planning.versions.submit", "planning.versions.approve", "planning.budget.manage"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningSubmissionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "planning/config",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["planning.models.read", "planning.models.manage", "planning.cycles.manage"] }}
          moduleId="planning"
          mode="mixed"
        >
          <PlanningConfigPage />
        </PermissionGuard>
      )
    }
  ]
};
