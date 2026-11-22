import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  BANKING_DESTINATIONS,
  CRM_DESTINATIONS,
  ACCOUNTING_DESTINATIONS,
  INVENTORY_DESTINATIONS,
  MANUFACTURING_DESTINATIONS,
  SALES_DESTINATIONS,
  PURCHASING_DESTINATIONS,
  POS_DESTINATIONS,
  FIXED_ASSETS_DESTINATIONS,
  EXPENSES_DESTINATIONS,
  INVOICING_DESTINATIONS,
  PAYMENTS_DESTINATIONS,
  HR_DESTINATIONS,
  PAYROLL_DESTINATIONS,
  PROJECTS_DESTINATIONS,
  SERVICE_DESTINATIONS
} from "../../app/router/destination-registry.ts";
import type { PermissionCondition } from "../../app/router/route-contract.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, "../../..");
const modulesDir = path.resolve(rootDir, "backend/Modules");
const frontendModulesDir = path.resolve(rootDir, "frontend/modules");
const commandPaletteFile = path.resolve(rootDir, "frontend/app/shell/command-palette-registry.ts");

// Authoritative Platform Kernel permissions (PlatformHostingExtensions.cs:146)
const PLATFORM_KERNEL_PERMISSIONS = [
  "platform.role.manage",
  "platform.module.view",
  "platform.module.activate",
  "platform.module.deactivate",
  "platform.settings.manage",
  "platform.settings.read",
  "platform.audit.view",
  "platform.support_access.request",
  "platform.support_access.approve",
  "platform.message.replay",
  "platform.organization.read",
  "platform.identity.read",
  "platform.identity.manage",
  "platform.tenancy.read",
  "platform.tenancy.manage",
  "platform.migration.execute"
];

function loadAllManifestPermissions(): Set<string> {
  const allPermissions = new Set<string>(PLATFORM_KERNEL_PERMISSIONS);
  const entries = fs.readdirSync(modulesDir, { withFileTypes: true });

  for (const entry of entries) {
    if (entry.isDirectory()) {
      const manifestPath = path.join(modulesDir, entry.name, "module.manifest.json");
      if (fs.existsSync(manifestPath)) {
        const content = fs.readFileSync(manifestPath, "utf-8");
        const json = JSON.parse(content);
        if (Array.isArray(json.permissions)) {
          for (const perm of json.permissions) {
            allPermissions.add(perm);
          }
        }
      }
    }
  }

  return allPermissions;
}

function extractPermissions(condition: PermissionCondition | undefined): string[] {
  if (!condition) return [];
  if (typeof condition === "string") return [condition];
  const result: string[] = [];
  if (condition.anyOf) result.push(...condition.anyOf);
  if (condition.allOf) result.push(...condition.allOf);
  return result;
}

function extractPermissionsFromSource(source: string): string[] {
  const permissions: string[] = [];
  // Match single permission: permission: "..." or permission="crm.read"
  const singleRegex = /permission(?:s)?\s*[:=]\s*["']([a-zA-Z0-9_]+\.[a-zA-Z0-9_.]+)["']/g;
  let match;
  while ((match = singleRegex.exec(source)) !== null) {
    if (match[1]) permissions.push(match[1]);
  }

  // Match array permissions: ["...", "..."]
  const arrayRegex = /(?:anyOf|allOf)\s*:\s*\[([^\]]+)\]/g;
  while ((match = arrayRegex.exec(source)) !== null) {
    const inner = match[1] ?? "";
    const strRegex = /["']([a-zA-Z0-9_]+\.[a-zA-Z0-9_.]+)["']/g;
    let strMatch;
    while ((strMatch = strRegex.exec(inner)) !== null) {
      if (strMatch[1]) permissions.push(strMatch[1]);
    }
  }

  // Match canPerform("...", "...") in command palette
  const canPerformRegex = /canPerform\s*\(\s*["']([a-zA-Z0-9_]+\.[a-zA-Z0-9_.]+)["']/g;
  while ((match = canPerformRegex.exec(source)) !== null) {
    if (match[1]) permissions.push(match[1]);
  }

  return Array.from(new Set(permissions));
}

describe("Permission Manifest Authoritative Audit", () => {
  const authoritativeCatalog = loadAllManifestPermissions();

  it("loads authoritative catalog containing 24 modules and platform kernel", () => {
    assert.ok(authoritativeCatalog.size > 150, `Catalog must have >150 permissions, found ${authoritativeCatalog.size}`);
    assert.ok(authoritativeCatalog.has("banking.statements.read"));
    assert.ok(authoritativeCatalog.has("banking.reconciliation.match"));
    assert.ok(authoritativeCatalog.has("crm.pipeline.manage"));
    assert.ok(authoritativeCatalog.has("subledger.read"));
    assert.ok(authoritativeCatalog.has("platform.identity.read"));
  });

  it("verifies 100% of BANKING_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of BANKING_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All BANKING_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of CRM_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of CRM_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All CRM_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of ACCOUNTING_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of ACCOUNTING_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All ACCOUNTING_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of INVENTORY_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of INVENTORY_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All INVENTORY_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of MANUFACTURING_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of MANUFACTURING_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All MANUFACTURING_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of SALES_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of SALES_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All SALES_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of PURCHASING_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of PURCHASING_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All PURCHASING_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of POS_DESTINATIONS permissions exist in manifest catalog", () => {
    const invalidCodes: string[] = [];
    for (const dest of POS_DESTINATIONS) {
      const codes = extractPermissions(dest.permission);
      for (const code of codes) {
        if (!authoritativeCatalog.has(code)) {
          invalidCodes.push(`${dest.id} -> ${code}`);
        }
      }
    }
    assert.deepEqual(invalidCodes, [], `All POS_DESTINATIONS permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of P3 Wave 3–5 destination permissions exist in manifest catalog", () => {
    const waveDestinations = [
      ...FIXED_ASSETS_DESTINATIONS,
      ...EXPENSES_DESTINATIONS,
      ...INVOICING_DESTINATIONS,
      ...PAYMENTS_DESTINATIONS,
      ...HR_DESTINATIONS,
      ...PAYROLL_DESTINATIONS,
      ...PROJECTS_DESTINATIONS,
      ...SERVICE_DESTINATIONS
    ];
    const invalidCodes: string[] = [];
    for (const dest of waveDestinations) {
      for (const code of extractPermissions(dest.permission)) {
        if (!authoritativeCatalog.has(code)) invalidCodes.push(`${dest.id} -> ${code}`);
      }
    }
    assert.deepEqual(invalidCodes, [], `All Wave 3–5 destination permissions must be valid manifests. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of permissions in all module routes.tsx files match manifest catalog", () => {
    const moduleDirs = fs.readdirSync(frontendModulesDir, { withFileTypes: true });
    const invalidCodes: string[] = [];

    for (const dir of moduleDirs) {
      if (dir.isDirectory()) {
        const routesPath = path.join(frontendModulesDir, dir.name, "routes.tsx");
        if (fs.existsSync(routesPath)) {
          const source = fs.readFileSync(routesPath, "utf-8");
          const permissions = extractPermissionsFromSource(source);
          for (const perm of permissions) {
            if (!authoritativeCatalog.has(perm)) {
              invalidCodes.push(`${dir.name}/routes.tsx -> ${perm}`);
            }
          }
        }
      }
    }

    assert.deepEqual(invalidCodes, [], `All permissions in routes.tsx must exist in authoritative catalog. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("verifies 100% of permissions referenced in Command Palette exist in manifest catalog", () => {
    assert.ok(fs.existsSync(commandPaletteFile), "Command palette registry file must exist");
    const source = fs.readFileSync(commandPaletteFile, "utf-8");
    const permissions = extractPermissionsFromSource(source);
    const invalidCodes: string[] = [];

    for (const perm of permissions) {
      if (!authoritativeCatalog.has(perm)) {
        invalidCodes.push(`command-palette-registry -> ${perm}`);
      }
    }

    assert.deepEqual(invalidCodes, [], `All permissions in command palette must exist in authoritative catalog. Found invalid: ${invalidCodes.join(", ")}`);
  });

  it("catches known legacy invented permission codes as invalid", () => {
    const rejectedInventedCodes = [
      "banking.statement.read", // singular instead of banking.statements.read
      "banking.statement.import", // singular instead of banking.statements.import
      "banking.cash.transfer", // instead of banking.transfers.initiate
      "banking.reconciliation.manage", // instead of banking.reconciliation.match
      "banking.accounts.manage", // invented
      "crm.all.view", // invented
      "subledger.openitems.read" // instead of subledger.read
    ];

    for (const code of rejectedInventedCodes) {
      assert.equal(
        authoritativeCatalog.has(code),
        false,
        `Known invented permission '${code}' must NOT exist in the authoritative manifest catalog`
      );
    }
  });
});
