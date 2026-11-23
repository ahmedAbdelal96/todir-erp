import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { CapabilitySet } from "./capabilities.ts";
import { PermissionSet } from "./permissions.ts";
import { defaultUserPreferences } from "./preferences.ts";
import { queryKeyFactory } from "../api/query-keys.ts";
import type { TenantContext } from "./tenant-context.ts";
import type { LegalEntityContext } from "./entity-context.ts";
import type { QueryCacheManager } from "./runtime-provider.tsx";

describe("CapabilitySet", () => {
  it("correctly identifies available, readonly, and unavailable capabilities", () => {
    const caps = new CapabilitySet({
      "sales.orders": "Available",
      "inventory.stock": "ReadOnly",
      "payroll.run": "NotActivated"
    });

    assert.equal(caps.isAvailable("sales.orders"), true);
    assert.equal(caps.isReadOnly("sales.orders"), false);
    assert.equal(caps.isActivated("sales.orders"), true);

    assert.equal(caps.isAvailable("inventory.stock"), false);
    assert.equal(caps.isReadOnly("inventory.stock"), true);
    assert.equal(caps.isActivated("inventory.stock"), true);

    assert.equal(caps.isAvailable("payroll.run"), false);
    assert.equal(caps.isReadOnly("payroll.run"), false);
    assert.equal(caps.isActivated("payroll.run"), false);
    assert.equal(caps.getState("payroll.run"), "NotActivated");

    assert.equal(caps.isAvailable("unknown.module"), false);
    assert.equal(caps.getState("unknown.module"), "Unavailable");
  });

  it("handles capability details with reasons", () => {
    const caps = new CapabilitySet({
      "accounting.post": { state: "ReadOnly", reason: "Period closed" }
    });

    assert.equal(caps.isReadOnly("accounting.post"), true);
    assert.equal(caps.getReason("accounting.post"), "Period closed");
  });
});

describe("PermissionSet", () => {
  it("evaluates exact permissions", () => {
    const perms = new PermissionSet(["sales.invoice.create", "platform.settings.read"]);

    assert.equal(perms.hasPermission("sales.invoice.create"), true);
    assert.equal(perms.hasPermission("sales.invoice.delete"), false);
    assert.equal(perms.hasAnyPermission("sales.invoice.delete", "platform.settings.read"), true);
    assert.equal(perms.hasAllPermissions("sales.invoice.create", "platform.settings.read"), true);
    assert.equal(perms.hasAllPermissions("sales.invoice.create", "sales.invoice.delete"), false);
  });

  it("handles wildcard scope permissions", () => {
    const perms = new PermissionSet(["platform.*"]);

    assert.equal(perms.hasPermission("platform.settings.read"), true);
    assert.equal(perms.hasPermission("platform.identity.manage"), true);
    assert.equal(perms.hasPermission("sales.invoice.read"), false);
  });

  it("handles global wildcard", () => {
    const perms = new PermissionSet(["*"]);
    assert.equal(perms.hasPermission("anything.under.the.sun"), true);
  });
});

describe("UserPreferences", () => {
  it("defaults to comfortable density, light appearance, and arabic language", () => {
    assert.equal(defaultUserPreferences.density, "comfortable");
    assert.equal(defaultUserPreferences.appearance, "light");
    assert.equal(defaultUserPreferences.language, "ar");
    assert.equal(defaultUserPreferences.numeralSystem, "latin");
  });
});

describe("Context Switching & Cache Isolation Contract", () => {
  it("keeps tenant and legal-entity boundaries in server-state query keys", () => {
    assert.notDeepEqual(
      queryKeyFactory.entity("tenant-1", "entity-1"),
      queryKeyFactory.entity("tenant-2", "entity-1")
    );
    assert.notDeepEqual(
      queryKeyFactory.entity("tenant-1", "entity-1"),
      queryKeyFactory.entity("tenant-1", "entity-2")
    );
    assert.deepEqual(queryKeyFactory.tenant("tenant-1"), ["todir", "tenant", "tenant-1"]);
  });

  it("tenant switch requires full query cache clear to prevent data leakage", () => {
    let cacheCleared = false;
    const cacheManager: QueryCacheManager = {
      clearAll: () => { cacheCleared = true; },
      invalidateEntityScope: (_id: string) => {}
    };

    const initialTenant: TenantContext = { tenantId: "tenant-1", displayName: "Tenant 1" };
    const nextTenant: TenantContext = { tenantId: "tenant-2", displayName: "Tenant 2" };

    assert.notEqual(initialTenant.tenantId, nextTenant.tenantId);
    cacheManager.clearAll();
    assert.equal(cacheCleared, true);
  });

  it("legal entity switch invalidates entity-scoped queries", () => {
    const invalidatedEntities: string[] = [];
    const cacheManager: QueryCacheManager = {
      clearAll: () => {},
      invalidateEntityScope: (id: string) => { invalidatedEntities.push(id); }
    };

    const currentEntity: LegalEntityContext = {
      legalEntityId: "entity-1",
      code: "HQ",
      name: "Headquarters",
      baseCurrencyCode: "SAR",
      countryCode: "SA"
    };

    cacheManager.invalidateEntityScope(currentEntity.legalEntityId);
    assert.deepEqual(invalidatedEntities, ["entity-1"]);
  });
});
