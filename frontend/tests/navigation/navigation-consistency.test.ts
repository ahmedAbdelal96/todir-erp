import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import {
  ACCOUNTING_DESTINATIONS,
  BANKING_DESTINATIONS,
  CRM_DESTINATIONS,
  EXPENSES_DESTINATIONS,
  FIXED_ASSETS_DESTINATIONS,
  HR_DESTINATIONS,
  INVENTORY_DESTINATIONS,
  INVOICING_DESTINATIONS,
  MANUFACTURING_DESTINATIONS,
  PAYMENTS_DESTINATIONS,
  PAYROLL_DESTINATIONS,
  POS_DESTINATIONS,
  PROJECTS_DESTINATIONS,
  PURCHASING_DESTINATIONS,
  SALES_DESTINATIONS,
  SERVICE_DESTINATIONS,
  isDestinationPermitted
} from "../../app/router/destination-registry.ts";
import type { ModuleNavigationRegistration } from "../../app/router/route-contract.ts";
import {
  buildCanonicalTaskPath,
  resolveLegacyTaskDestination
} from "../../app/router/legacy-task-navigation.ts";

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const routesByModule = new Map<string, string>();
for (const entry of fs.readdirSync(path.join(rootDir, "frontend/modules"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  const routeFile = path.join(rootDir, "frontend/modules", entry.name, "routes.tsx");
  if (fs.existsSync(routeFile)) routesByModule.set(entry.name, fs.readFileSync(routeFile, "utf8"));
}

const destinationSets = [
  ACCOUNTING_DESTINATIONS,
  BANKING_DESTINATIONS,
  CRM_DESTINATIONS,
  EXPENSES_DESTINATIONS,
  FIXED_ASSETS_DESTINATIONS,
  HR_DESTINATIONS,
  INVENTORY_DESTINATIONS,
  INVOICING_DESTINATIONS,
  MANUFACTURING_DESTINATIONS,
  PAYMENTS_DESTINATIONS,
  PAYROLL_DESTINATIONS,
  POS_DESTINATIONS,
  PROJECTS_DESTINATIONS,
  PURCHASING_DESTINATIONS,
  SALES_DESTINATIONS,
  SERVICE_DESTINATIONS
];

const emptyAccess: EffectiveAccess = {
  tenantId: "tenant-1",
  membershipId: "membership-1",
  legalEntityId: "entity-1",
  branchId: "branch-1",
  isSuper: false,
  permissions: [],
  entitledModules: destinationSets.flatMap(set => set.length ? [set[0]!.moduleId] : []),
  moduleStatuses: {},
  version: "test"
};

describe("canonical task navigation contract", () => {
  it("keeps every registered destination backed by a route", () => {
    const paths = new Set<string>();
    for (const destinations of destinationSets) {
      for (const destination of destinations) {
        assert.equal(paths.has(destination.path), false, `duplicate destination path: ${destination.path}`);
        paths.add(destination.path);
        const routeModule = destination.path.split("/").filter(Boolean)[0];
        const routeText = routesByModule.get(routeModule);
        assert.ok(routeText, `missing route file for ${destination.path}`);
        const routePath = destination.path.slice(1);
        assert.match(routeText!, new RegExp(`path:\\s*["']${routePath.replace(/[.*+?^${}()|[\\]\\]/g, "\\$&")}["']`), `destination has no canonical route: ${destination.path}`);
      }
    }
  });

  it("resolves legacy tabs without bypassing the destination permission", () => {
    const module: ModuleNavigationRegistration = {
      id: "hr",
      moduleIds: ["hr"],
      path: "/hr",
      navigationKey: "hr",
      destinations: HR_DESTINATIONS,
      icon: "users"
    };
    const leaveAccess: EffectiveAccess = { ...emptyAccess, permissions: ["hr.leave.read"], entitledModules: ["hr"] };
    const leave = resolveLegacyTaskDestination(module, "leave");
    const directory = resolveLegacyTaskDestination(module, "directory");
    assert.equal(leave?.path, "/hr/leave");
    assert.equal(directory?.path, "/hr/directory");
    assert.equal(isDestinationPermitted(leave!, leaveAccess), true);
    assert.equal(isDestinationPermitted(directory!, leaveAccess), false);
  });

  it("preserves unrelated query parameters while removing only the legacy tab", () => {
    assert.equal(
      buildCanonicalTaskPath(HR_DESTINATIONS, "/hr", "leave", "tab=leave&employeeId=emp-1&view=compact"),
      "/hr/leave?employeeId=emp-1&view=compact"
    );
  });

  it("prevents protected root pages from letting query tabs override canonical task routes", () => {
    const guardedRootPages = [
      "frontend/modules/expenses/pages/expenses-page.tsx",
      "frontend/modules/fixed-assets/pages/fixed-assets-page.tsx",
      "frontend/modules/hr/pages/hr-page.tsx",
      "frontend/modules/payroll/pages/payroll-page.tsx",
      "frontend/modules/projects/pages/projects-page.tsx",
      "frontend/modules/service/pages/service-page.tsx"
    ];
    for (const relativePath of guardedRootPages) {
      const source = fs.readFileSync(path.join(rootDir, relativePath), "utf8");
      assert.doesNotMatch(source, /searchParams\.get\("tab"\)[\s\S]{0,100}\?\? initialTab/, relativePath);
      assert.doesNotMatch(source, /!isPermissionsLoaded/, relativePath);
    }
  });

  it("does not retain fabricated legal-entity fallbacks in user-facing module scope", () => {
    const sourceFiles = [
      "frontend/modules/accounting/hooks/use-ledger-queries.ts",
      "frontend/modules/inventory/hooks/use-inventory-queries.ts",
      "frontend/modules/hr/pages/hr-page.tsx",
      "frontend/modules/sales/pages/sales-page.tsx",
      "frontend/modules/service/pages/service-page.tsx",
      "frontend/modules/service/hooks/use-service-queries.ts"
    ];
    for (const relativePath of sourceFiles) {
      const source = fs.readFileSync(path.join(rootDir, relativePath), "utf8");
      assert.doesNotMatch(source, /legalEntityId[^\n]*00000000-0000-0000-0000-000000000001/, relativePath);
    }
  });

  it("keeps Projects resource-load permission and data loading aligned", () => {
    const destination = PROJECTS_DESTINATIONS.find((item) => item.id === "projects-resource-load");
    assert.ok(destination);
    const operatorAccess: EffectiveAccess = { ...emptyAccess, permissions: ["projects.manage_team"], entitledModules: ["projects"] };
    assert.equal(isDestinationPermitted(destination!, operatorAccess), true);

    const pageSource = fs.readFileSync(path.join(rootDir, "frontend/modules/projects/pages/projects-page.tsx"), "utf8");
    assert.match(pageSource, /"projects\.manage_team"/);
    assert.match(pageSource, /useResourceLoad\(legalEntityId, \{ enabled: canViewResourceLoad \}\)/);
    assert.match(pageSource, /useProjectsList\(legalEntityId, queryParams, \{ enabled: canViewPortfolio \}\)/);
  });

  it("keeps HR employee identity details bound to the directory task", () => {
    const pageSource = fs.readFileSync(path.join(rootDir, "frontend/modules/hr/pages/hr-page.tsx"), "utf8");
    assert.match(pageSource, /activeTab === "directory"/);
    assert.match(pageSource, /hr\.employee\.read_basic/);
    assert.match(pageSource, /next\.delete\("employeeId"\)/);
  });
});
