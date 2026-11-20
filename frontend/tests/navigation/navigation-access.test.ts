import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import {
  evaluatePermissionCondition,
  isModuleOperational,
  hasPermissionInAccess,
  hasAnyPermissionInAccess,
  hasAllPermissionsInAccess
} from "../../core/runtime/effective-access.ts";
import {
  isDestinationPermitted,
  isModulePermitted,
  getPermittedDestinationsForModule,
  BANKING_DESTINATIONS,
  CRM_DESTINATIONS,
  NAVIGATION_CATEGORIES
} from "../../app/router/destination-registry.ts";
import type { ModuleNavigationRegistration } from "../../app/router/route-contract.ts";
import { buildCommandPaletteItems } from "../../app/shell/command-palette-registry.ts";

describe("Permission-Driven Effective Access & Destination Architecture", () => {
  const emptyAccess: EffectiveAccess = {
    tenantId: "tenant-1",
    membershipId: "member-1",
    legalEntityId: "entity-1",
    branchId: "branch-1",
    isSuper: false,
    permissions: [],
    entitledModules: ["banking", "crm", "invoicing", "sales", "catalog"],
    moduleStatuses: {},
    version: "v1"
  };

  const cashierAccess: EffectiveAccess = {
    ...emptyAccess,
    permissions: ["banking.cash.count"]
  };

  const accountantAccess: EffectiveAccess = {
    ...emptyAccess,
    permissions: [
      "banking.accounts.read",
      "banking.statement.read",
      "banking.statement.import",
      "banking.statement.validate",
      "banking.reconciliation.read",
      "banking.reconciliation.manage",
      "banking.reconciliation.finalize"
    ]
  };

  const salespersonAccess: EffectiveAccess = {
    ...emptyAccess,
    permissions: [
      "crm.home.read",
      "crm.lead.read",
      "crm.lead.create",
      "crm.opportunity.read",
      "crm.activity.read"
    ]
  };

  const salesManagerAccess: EffectiveAccess = {
    ...emptyAccess,
    permissions: [
      "crm.home.read",
      "crm.lead.read",
      "crm.lead.create",
      "crm.opportunity.read",
      "crm.activity.read",
      "crm.team.manage",
      "crm.pipeline.manage"
    ]
  };

  const superAdminAccess: EffectiveAccess = {
    ...emptyAccess,
    isSuper: true,
    permissions: ["*"]
  };

  const sampleBankingRegistration: ModuleNavigationRegistration = {
    id: "banking",
    moduleIds: ["banking"],
    path: "/banking",
    navigationKey: "banking",
    category: "finance",
    destinations: BANKING_DESTINATIONS,
    permissions: {
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
    },
    icon: "banking"
  };

  const sampleCrmRegistration: ModuleNavigationRegistration = {
    id: "crm",
    moduleIds: ["crm"],
    path: "/crm",
    navigationKey: "crm",
    category: "sales",
    destinations: CRM_DESTINATIONS,
    permissions: {
      anyOf: [
        "crm.home.read",
        "crm.lead.read",
        "crm.lead.create",
        "crm.lead.edit",
        "crm.opportunity.read",
        "crm.opportunity.create",
        "crm.opportunity.edit",
        "crm.activity.read",
        "crm.activity.create",
        "crm.activity.edit",
        "crm.team.read",
        "crm.team.manage",
        "crm.pipeline.manage"
      ]
    },
    icon: "crm"
  };

  it("evaluates permission condition with string, anyOf, allOf, and super-admin correctly", () => {
    assert.equal(evaluatePermissionCondition("banking.cash.count", cashierAccess), true);
    assert.equal(evaluatePermissionCondition("banking.accounts.read", cashierAccess), false);
    assert.equal(evaluatePermissionCondition({ anyOf: ["banking.cash.count", "banking.accounts.read"] }, cashierAccess), true);
    assert.equal(evaluatePermissionCondition({ allOf: ["banking.cash.count", "banking.accounts.read"] }, cashierAccess), false);

    // Super admin bypasses all permission checks
    assert.equal(evaluatePermissionCondition("banking.accounts.read", superAdminAccess), true);
    assert.equal(evaluatePermissionCondition("platform.system.admin", superAdminAccess), true);
  });

  it("enforces module operational status and unentitled check", () => {
    const unentitledAccess: EffectiveAccess = {
      ...cashierAccess,
      entitledModules: ["invoicing"] // Banking not entitled
    };
    assert.equal(isModuleOperational(unentitledAccess, "banking"), false);
    assert.equal(isModuleOperational(unentitledAccess, "invoicing"), true);

    const deactivatedAccess: EffectiveAccess = {
      ...cashierAccess,
      moduleStatuses: { banking: "Deactivated" }
    };
    assert.equal(isModuleOperational(deactivatedAccess, "banking"), false);
  });

  it("permits Banking module when cashier has only banking.cash.count (child destination rule)", () => {
    // Cashier has no banking.accounts.read, but has banking.cash.count
    assert.equal(isModulePermitted(sampleBankingRegistration, cashierAccess), true);

    // Completely empty user has no access to Banking
    assert.equal(isModulePermitted(sampleBankingRegistration, emptyAccess), false);
  });

  it("filters Banking destinations to only allowed tasks for cashier vs accountant", () => {
    // Cashier: only cash destination is visible
    const cashierDests = getPermittedDestinationsForModule(sampleBankingRegistration, cashierAccess);
    assert.equal(cashierDests.length, 1);
    assert.equal(cashierDests[0]?.id, "banking-cash");
    assert.equal(cashierDests[0]?.path, "/banking/cash");

    // Accountant: accounts, statements, reconciliations are visible; cash counts are not
    const accountantDests = getPermittedDestinationsForModule(sampleBankingRegistration, accountantAccess);
    const destIds = accountantDests.map(d => d.id);
    assert.ok(destIds.includes("banking-accounts"));
    assert.ok(destIds.includes("banking-statements"));
    assert.ok(destIds.includes("banking-reconciliations"));
    assert.ok(!destIds.includes("banking-cash"), "Accountant without banking.cash.count should not see cash counts");
  });

  it("filters CRM destinations: salesperson cannot see management, while manager can", () => {
    const repDests = getPermittedDestinationsForModule(sampleCrmRegistration, salespersonAccess);
    const repDestIds = repDests.map(d => d.id);
    assert.ok(repDestIds.includes("crm-home"));
    assert.ok(repDestIds.includes("crm-leads"));
    assert.ok(repDestIds.includes("crm-opportunities"));
    assert.ok(repDestIds.includes("crm-activities"));
    assert.ok(!repDestIds.includes("crm-management"), "Salesperson should not see crm-management");

    const mgrDests = getPermittedDestinationsForModule(sampleCrmRegistration, salesManagerAccess);
    const mgrDestIds = mgrDests.map(d => d.id);
    assert.ok(mgrDestIds.includes("crm-management"), "Sales manager should see crm-management");
  });

  it("defines the 7 required shallow navigation categories", () => {
    const categoryIds = NAVIGATION_CATEGORIES.map(c => c.id);
    assert.deepEqual(categoryIds, [
      "workspace",
      "sales",
      "purchasing",
      "inventory",
      "finance",
      "hr",
      "admin"
    ]);
  });

  it("protects Command Palette items: hides unauthorized quick actions and surfaces permitted destinations", () => {
    const t = (key: string, fallback?: string) => fallback ?? key;
    const navigate = () => {};

    // For cashier: invoice creation action must NOT be present
    const cashierPalette = buildCommandPaletteItems({
      modules: [sampleBankingRegistration],
      effectiveAccess: cashierAccess,
      navigate,
      t: t as any
    });

    const cashierItemIds = cashierPalette.map(item => item.id);
    assert.ok(!cashierItemIds.includes("action-new-customer-invoice"), "Cashier must not see new customer invoice action");
    assert.ok(!cashierItemIds.includes("action-new-supplier-invoice"), "Cashier must not see new supplier invoice action");
    assert.ok(!cashierItemIds.includes("action-new-product"), "Cashier must not see new product action");
    // But cashier can find the cash count task destination in command palette search!
    assert.ok(cashierItemIds.includes("nav-banking-cash"), "Cashier should be able to search cash count destination via Ctrl+K");

    // For invoice clerk
    const invoiceClerkAccess: EffectiveAccess = {
      ...emptyAccess,
      permissions: ["invoicing.customer_invoice.create", "invoicing.customer_invoice.read"]
    };
    const invoicePalette = buildCommandPaletteItems({
      modules: [{ id: "invoicing", moduleIds: ["invoicing"], path: "/invoicing", navigationKey: "invoicing", icon: "invoicing" }],
      effectiveAccess: invoiceClerkAccess,
      navigate,
      t: t as any
    });
    const invoiceItemIds = invoicePalette.map(item => item.id);
    assert.ok(invoiceItemIds.includes("action-new-customer-invoice"), "Invoicing clerk should see new customer invoice action");
  });
});
