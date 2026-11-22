import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import {
  evaluatePermissionCondition,
  evaluateEffectivePermissionCondition,
  isModuleOperational,
  hasPermissionInAccess,
  hasAnyPermissionInAccess,
  hasAllPermissionsInAccess,
  isModuleReadOnly,
  canPerformAction
} from "../../core/runtime/effective-access.ts";
import {
  isDestinationPermitted,
  isModulePermitted,
  getPermittedDestinationsForModule,
  BANKING_DESTINATIONS,
  CRM_DESTINATIONS,
  ACCOUNTING_DESTINATIONS,
  INVENTORY_DESTINATIONS,
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
  SERVICE_DESTINATIONS,
  NAVIGATION_CATEGORIES,
  resolveModuleLandingPath
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
      "banking.statements.read",
      "banking.statements.import",
      "banking.statements.validate",
      "banking.reconciliation.read",
      "banking.reconciliation.match",
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

  it("handles multi-module Accounting vs Ledger entitlements: non-purchasable core Ledger destinations remain accessible", () => {
    const accountingRegistration: ModuleNavigationRegistration = {
      id: "accounting",
      moduleIds: ["accounting", "ledger"],
      path: "/accounting",
      navigationKey: "accounting",
      category: "finance",
      destinations: ACCOUNTING_DESTINATIONS,
      icon: "accounting"
    };

    // Tenant has entitled "ledger" (core, non-purchasable) but NOT commercial "accounting"
    const ledgerOnlyAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["ledger"], // No commercial "accounting" entitlement
      permissions: ["ledger.account.read", "ledger.reports.read"]
    };

    // The module must be permitted because its ledger task destinations are operational
    assert.equal(isModulePermitted(accountingRegistration, ledgerOnlyAccess), true);

    const permittedDests = getPermittedDestinationsForModule(accountingRegistration, ledgerOnlyAccess);
    const destIds = permittedDests.map(d => d.id);

    // Ledger destinations are allowed
    assert.ok(destIds.includes("accounting-overview"), "Chart of Accounts must be accessible under Ledger");
    assert.ok(destIds.includes("accounting-ledger"), "General Ledger must be accessible under Ledger");
    assert.ok(destIds.includes("accounting-trial-balance"), "Trial Balance must be accessible under Ledger");

    // Commercial accounting destinations are NOT allowed
    assert.ok(!destIds.includes("accounting-statements"), "Financial statements requires commercial accounting module");
    assert.ok(!destIds.includes("accounting-reconciliation"), "Reconciliation requires commercial accounting module");
    assert.ok(!destIds.includes("accounting-revaluation"), "FX revaluation requires commercial accounting module");
  });

  it("filters Inventory destinations between warehouse operator and logistics manager", () => {
    const inventoryRegistration: ModuleNavigationRegistration = {
      id: "inventory",
      moduleIds: ["inventory"],
      path: "/inventory",
      navigationKey: "inventory",
      category: "inventory",
      destinations: INVENTORY_DESTINATIONS,
      icon: "inventory"
    };

    // Operator with only stock reading permission
    const operatorAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.stock.read", "inventory.history.read"]
    };

    const operatorDests = getPermittedDestinationsForModule(inventoryRegistration, operatorAccess);
    const operatorDestIds = operatorDests.map(d => d.id);
    assert.ok(operatorDestIds.includes("inventory-overview"));
    assert.ok(operatorDestIds.includes("inventory-movements"));
    assert.ok(!operatorDestIds.includes("inventory-transfers"), "Operator cannot perform transfers");
    assert.ok(!operatorDestIds.includes("inventory-adjustments"), "Operator cannot perform adjustments");
    assert.ok(!operatorDestIds.includes("inventory-counts"), "Operator cannot perform stock counts");

    // Logistics manager with operational permissions
    const managerAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: [
        "inventory.stock.read",
        "inventory.history.read",
        "inventory.transfer.create",
        "inventory.adjustment.create",
        "inventory.count.create"
      ]
    };

    const managerDests = getPermittedDestinationsForModule(inventoryRegistration, managerAccess);
    const managerDestIds = managerDests.map(d => d.id);
    assert.ok(managerDestIds.includes("inventory-transfers"));
    assert.ok(managerDestIds.includes("inventory-adjustments"));
    assert.ok(managerDestIds.includes("inventory-counts"));
  });

  it("resolves module landing path deterministically to first permitted task destination when root is forbidden", () => {
    const inventoryRegistration: ModuleNavigationRegistration = {
      id: "inventory",
      moduleIds: ["inventory"],
      path: "/inventory",
      navigationKey: "inventory",
      category: "inventory",
      destinations: INVENTORY_DESTINATIONS,
      icon: "inventory"
    };

    // User with only count creation permission
    const countClerkAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.count.create"]
    };

    // Must resolve to /inventory/counts instead of forbidden /inventory
    const clerkLandingPath = resolveModuleLandingPath(inventoryRegistration, countClerkAccess);
    assert.equal(clerkLandingPath, "/inventory/counts");

    // Cashier with only cash counting permission
    const cashierLandingPath = resolveModuleLandingPath(sampleBankingRegistration, cashierAccess);
    assert.equal(cashierLandingPath, "/banking/cash");

    // Full manager with overview permissions resolves to module root
    const fullAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.stock.read"]
    };
    const managerLandingPath = resolveModuleLandingPath(inventoryRegistration, fullAccess);
    assert.equal(managerLandingPath, "/inventory");
  });

  it("enforces ReadOnly and WindDown lifecycle statuses correctly", () => {
    const readOnlyAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.stock.read", "inventory.count.create"],
      moduleStatuses: { inventory: "ReadOnly" }
    };

    // Module is operational for historical reading (allowReadOnly = true)
    assert.equal(isModuleOperational(readOnlyAccess, "inventory", true), true);

    // Module is NOT operational for mutations (allowReadOnly = false)
    assert.equal(isModuleOperational(readOnlyAccess, "inventory", false), false);

    // Flagged as read-only
    assert.equal(isModuleReadOnly(readOnlyAccess, "inventory"), true);

    // WindDown status
    const windDownAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.stock.read"],
      moduleStatuses: { inventory: "WindDown" }
    };
    assert.equal(isModuleOperational(windDownAccess, "inventory", true), true);
    assert.equal(isModuleOperational(windDownAccess, "inventory", false), false);
    assert.equal(isModuleReadOnly(windDownAccess, "inventory"), true);

    // Active status
    const activeAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: ["inventory.stock.read"],
      moduleStatuses: { inventory: "Active" }
    };
    assert.equal(isModuleOperational(activeAccess, "inventory", true), true);
    assert.equal(isModuleOperational(activeAccess, "inventory", false), true);
    assert.equal(isModuleReadOnly(activeAccess, "inventory"), false);
  });

  it("filters destinations based on mode in ReadOnly and WindDown states", () => {
    const readOnlyAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: [
        "inventory.stock.read",
        "inventory.history.read",
        "inventory.count.create",
        "inventory.adjustment.create"
      ],
      moduleStatuses: { inventory: "ReadOnly" }
    };

    // INVENTORY_DESTINATIONS:
    // Movements is "read" mode -> permitted
    const movementsDest = INVENTORY_DESTINATIONS.find(d => d.id === "inventory-movements")!;
    assert.equal(isDestinationPermitted(movementsDest, readOnlyAccess), true);

    // Adjustments is "mutation" mode -> NOT permitted in ReadOnly
    const adjustmentsDest = INVENTORY_DESTINATIONS.find(d => d.id === "inventory-adjustments")!;
    assert.equal(isDestinationPermitted(adjustmentsDest, readOnlyAccess), false);

    // When module becomes Active, mutation destination is permitted
    const activeAccess: EffectiveAccess = {
      ...readOnlyAccess,
      moduleStatuses: { inventory: "Active" }
    };
    assert.equal(isDestinationPermitted(adjustmentsDest, activeAccess), true);
  });

  it("allows inventory-traceability (mode: read) in ReadOnly while canPerformAction disables mutations", () => {
    const readOnlyAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["inventory"],
      permissions: [
        "inventory.traceability.manage",
        "inventory.lot.create",
        "inventory.serial.create"
      ],
      moduleStatuses: { inventory: "ReadOnly" }
    };

    // Traceability destination has mode: 'read' and permission: 'inventory.traceability.manage'
    const traceabilityDest = INVENTORY_DESTINATIONS.find(d => d.id === "inventory-traceability")!;
    assert.ok(traceabilityDest);
    assert.equal(traceabilityDest.mode, "read");

    // In ReadOnly, traceability MUST remain accessible for historical tracking and lot/serial lookup
    assert.equal(isDestinationPermitted(traceabilityDest, readOnlyAccess), true);

    // Mutation actions within the page (e.g. creating lots or serials) must be prevented by canPerformAction
    assert.equal(canPerformAction(readOnlyAccess, "inventory.lot.create"), false);
    assert.equal(canPerformAction(readOnlyAccess, "inventory.serial.create"), false);

    // When module is Active, mutation actions become permitted
    const activeAccess: EffectiveAccess = {
      ...readOnlyAccess,
      moduleStatuses: { inventory: "Active" }
    };
    assert.equal(isDestinationPermitted(traceabilityDest, activeAccess), true);
    assert.equal(canPerformAction(activeAccess, "inventory.lot.create"), true);
    assert.equal(canPerformAction(activeAccess, "inventory.serial.create"), true);
  });

  it("enforces multi-module commercial entitlement isolation without cross-module bypass", () => {
    // User has permission 'accounting.close.execute' from Accounting module,
    // but the tenant is only entitled to 'ledger' (not 'accounting').
    const unentitledAccountingAccess: EffectiveAccess = {
      ...emptyAccess,
      entitledModules: ["ledger"], // accounting is missing!
      permissions: ["accounting.close.execute"],
      moduleStatuses: { ledger: "Active" }
    };

    const compositeCondition = {
      anyOf: ["ledger.period.read", "ledger.period.manage", "accounting.close.execute"]
    };

    // Condition must FAIL because owning module 'accounting' is unentitled
    assert.equal(evaluateEffectivePermissionCondition(compositeCondition, unentitledAccountingAccess, false), false);

    // If tenant purchases and activates 'accounting', condition PASSES
    const entitledAccountingAccess: EffectiveAccess = {
      ...unentitledAccountingAccess,
      entitledModules: ["ledger", "accounting"],
      moduleStatuses: { ledger: "Active", accounting: "Active" }
    };
    assert.equal(evaluateEffectivePermissionCondition(compositeCondition, entitledAccountingAccess, false), true);
  });

  describe("P3 Wave 2: Sales, Purchasing & POS Destination & Role Isolation", () => {
    it("isolates Sales quotations from order-only operators and guards settings", () => {
      const quoteOperatorAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["sales"],
        permissions: [
          "sales.quotation.read",
          "sales.quotation.create",
          "sales.quotation.edit"
        ],
        moduleStatuses: { sales: "Active" }
      };

      const orderOperatorAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["sales"],
        permissions: [
          "sales.order.read",
          "sales.order.fulfill"
        ],
        moduleStatuses: { sales: "Active" }
      };

      const salesOverviewDest = SALES_DESTINATIONS.find(d => d.id === "sales-overview")!;
      const quotationsDest = SALES_DESTINATIONS.find(d => d.id === "sales-quotations")!;
      const ordersDest = SALES_DESTINATIONS.find(d => d.id === "sales-orders")!;
      const returnsDest = SALES_DESTINATIONS.find(d => d.id === "sales-returns")!;
      const settingsDest = SALES_DESTINATIONS.find(d => d.id === "sales-settings")!;

      // Quote operator can see overview & quotations, but CANNOT see orders, returns, or settings
      assert.equal(isDestinationPermitted(salesOverviewDest, quoteOperatorAccess), true);
      assert.equal(isDestinationPermitted(quotationsDest, quoteOperatorAccess), true);
      assert.equal(isDestinationPermitted(ordersDest, quoteOperatorAccess), false);
      assert.equal(isDestinationPermitted(returnsDest, quoteOperatorAccess), false);
      assert.equal(isDestinationPermitted(settingsDest, quoteOperatorAccess), false);

      // Order operator can see overview & orders, but CANNOT see quotations or settings
      assert.equal(isDestinationPermitted(salesOverviewDest, orderOperatorAccess), true);
      assert.equal(isDestinationPermitted(quotationsDest, orderOperatorAccess), false);
      assert.equal(isDestinationPermitted(ordersDest, orderOperatorAccess), true);
      assert.equal(isDestinationPermitted(settingsDest, orderOperatorAccess), false);
    });

    it("isolates Purchasing roles: PO clerk vs Receiving clerk vs Matching clerk vs Settings", () => {
      const poClerkAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["purchasing"],
        permissions: ["purchasing.po.read", "purchasing.po.create"],
        moduleStatuses: { purchasing: "Active" }
      };

      const receivingClerkAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["purchasing"],
        permissions: ["purchasing.receipt.view", "purchasing.receipt.request"],
        moduleStatuses: { purchasing: "Active" }
      };

      const matchingClerkAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["purchasing"],
        permissions: ["purchasing.match.read"],
        moduleStatuses: { purchasing: "Active" }
      };

      const poOverviewDest = PURCHASING_DESTINATIONS.find(d => d.id === "purchasing-overview")!;
      const ordersDest = PURCHASING_DESTINATIONS.find(d => d.id === "purchasing-orders")!;
      const receiptsDest = PURCHASING_DESTINATIONS.find(d => d.id === "purchasing-receipts")!;
      const matchingDest = PURCHASING_DESTINATIONS.find(d => d.id === "purchasing-matching")!;
      const settingsDest = PURCHASING_DESTINATIONS.find(d => d.id === "purchasing-settings")!;

      // PO Clerk: sees overview and orders only
      assert.equal(isDestinationPermitted(poOverviewDest, poClerkAccess), true);
      assert.equal(isDestinationPermitted(ordersDest, poClerkAccess), true);
      assert.equal(isDestinationPermitted(receiptsDest, poClerkAccess), false);
      assert.equal(isDestinationPermitted(matchingDest, poClerkAccess), false);
      assert.equal(isDestinationPermitted(settingsDest, poClerkAccess), false);

      // Receiving Clerk: sees overview and receipts only
      assert.equal(isDestinationPermitted(poOverviewDest, receivingClerkAccess), true);
      assert.equal(isDestinationPermitted(ordersDest, receivingClerkAccess), false);
      assert.equal(isDestinationPermitted(receiptsDest, receivingClerkAccess), true);
      assert.equal(isDestinationPermitted(matchingDest, receivingClerkAccess), false);
      assert.equal(isDestinationPermitted(settingsDest, receivingClerkAccess), false);

      // Matching Clerk: sees matching only
      assert.equal(isDestinationPermitted(poOverviewDest, matchingClerkAccess), true);
      assert.equal(isDestinationPermitted(ordersDest, matchingClerkAccess), false);
      assert.equal(isDestinationPermitted(receiptsDest, matchingClerkAccess), false);
      assert.equal(isDestinationPermitted(matchingDest, matchingClerkAccess), true);
      assert.equal(isDestinationPermitted(settingsDest, matchingClerkAccess), false);
    });

    it("allows POS Cashier fast lane without administrative register read, while blocking unentitled tenants", () => {
      // Cashier has operational permissions only, NO pos.register.read or pos.register.manage
      const cashierAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["pos"],
        permissions: [
          "pos.cart.manage",
          "pos.checkout.create",
          "pos.session.open"
        ],
        moduleStatuses: { pos: "Active" }
      };

      const checkoutDest = POS_DESTINATIONS.find(d => d.id === "pos-checkout")!;
      const sessionsDest = POS_DESTINATIONS.find(d => d.id === "pos-sessions")!;
      const registersDest = POS_DESTINATIONS.find(d => d.id === "pos-registers")!;
      const reportsDest = POS_DESTINATIONS.find(d => d.id === "pos-reports")!;

      // Cashier can access checkout fast lane & sessions
      assert.equal(isDestinationPermitted(checkoutDest, cashierAccess), true);
      assert.equal(isDestinationPermitted(sessionsDest, cashierAccess), true);

      // Cashier CANNOT access administrative registers page or reports
      assert.equal(isDestinationPermitted(registersDest, cashierAccess), false);
      assert.equal(isDestinationPermitted(reportsDest, cashierAccess), false);

      // Commercial entitlement enforcement: if 'pos' is NOT entitled, all access is denied
      const unentitledCashierAccess: EffectiveAccess = {
        ...cashierAccess,
        entitledModules: ["sales"] // pos missing!
      };
      assert.equal(isModuleOperational(unentitledCashierAccess, "pos"), false);
      assert.equal(isDestinationPermitted(checkoutDest, unentitledCashierAccess), false);
    });
  });

  describe("P3 Waves 3–5: Finance, HR, Payroll, Projects & Service", () => {
    it("isolates Wave 3 finance tasks by role and resolves narrow landings", () => {
      const assetClerkAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["fixed-assets"],
        permissions: ["assets.depreciation.post"],
        moduleStatuses: { "fixed-assets": "Active" }
      };
      const expenseReviewerAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["expenses"],
        permissions: ["expenses.approve"],
        moduleStatuses: { expenses: "Active" }
      };
      const supplierInvoiceAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["invoicing"],
        permissions: ["invoicing.supplier_invoice.read"],
        moduleStatuses: { invoicing: "Active" }
      };
      const incomingPaymentAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["payments"],
        permissions: ["payments.incoming.create"],
        moduleStatuses: { payments: "Active" }
      };

      const assetRegistration: ModuleNavigationRegistration = {
        id: "fixed-assets", moduleIds: ["fixed-assets"], path: "/fixed-assets", navigationKey: "fixedAssets",
        destinations: FIXED_ASSETS_DESTINATIONS, icon: "fixed-assets"
      };
      const expenseRegistration: ModuleNavigationRegistration = {
        id: "expenses", moduleIds: ["expenses"], path: "/expenses", navigationKey: "expenses",
        destinations: EXPENSES_DESTINATIONS, icon: "expenses"
      };
      const invoicingRegistration: ModuleNavigationRegistration = {
        id: "invoicing", moduleIds: ["invoicing"], path: "/invoicing", navigationKey: "invoicing",
        destinations: INVOICING_DESTINATIONS, icon: "invoicing"
      };
      const paymentsRegistration: ModuleNavigationRegistration = {
        id: "payments", moduleIds: ["payments"], path: "/payments", navigationKey: "payments",
        destinations: PAYMENTS_DESTINATIONS, icon: "payments"
      };

      assert.equal(resolveModuleLandingPath(assetRegistration, assetClerkAccess), "/fixed-assets/depreciation");
      assert.equal(isDestinationPermitted(FIXED_ASSETS_DESTINATIONS.find(d => d.id === "fixed-assets-depreciation")!, assetClerkAccess), true);
      assert.equal(isDestinationPermitted(FIXED_ASSETS_DESTINATIONS.find(d => d.id === "fixed-assets-register")!, assetClerkAccess), false);
      assert.equal(isDestinationPermitted(EXPENSES_DESTINATIONS.find(d => d.id === "expenses-review")!, expenseReviewerAccess), true);
      assert.equal(isDestinationPermitted(EXPENSES_DESTINATIONS.find(d => d.id === "expenses-settings")!, expenseReviewerAccess), false);
      assert.equal(isDestinationPermitted(INVOICING_DESTINATIONS.find(d => d.id === "invoicing-supplier")!, supplierInvoiceAccess), true);
      assert.equal(isDestinationPermitted(INVOICING_DESTINATIONS.find(d => d.id === "invoicing-customer")!, supplierInvoiceAccess), false);
      assert.equal(isDestinationPermitted(PAYMENTS_DESTINATIONS.find(d => d.id === "payments-incoming")!, incomingPaymentAccess), true);
      assert.equal(isModulePermitted(paymentsRegistration, incomingPaymentAccess), true);
      assert.equal(isModulePermitted(expenseRegistration, expenseReviewerAccess), true);
    });

    it("keeps Wave 4 HR and payroll task destinations separate", () => {
      const employeeAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["hr"],
        permissions: ["hr.leave.request_self"],
        moduleStatuses: { hr: "Active" }
      };
      const payrollAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["payroll"],
        permissions: ["payroll.run.read"],
        moduleStatuses: { payroll: "Active" }
      };
      const hrRegistration: ModuleNavigationRegistration = {
        id: "hr", moduleIds: ["hr"], path: "/hr", navigationKey: "hr", destinations: HR_DESTINATIONS, icon: "users"
      };
      const payrollRegistration: ModuleNavigationRegistration = {
        id: "payroll", moduleIds: ["payroll"], path: "/payroll", navigationKey: "payroll", destinations: PAYROLL_DESTINATIONS, icon: "banknotes"
      };

      assert.equal(resolveModuleLandingPath(hrRegistration, employeeAccess), "/hr/leave");
      assert.equal(isDestinationPermitted(HR_DESTINATIONS.find(d => d.id === "hr-self-service")!, employeeAccess), true);
      assert.equal(isDestinationPermitted(HR_DESTINATIONS.find(d => d.id === "hr-directory")!, employeeAccess), false);
      assert.equal(resolveModuleLandingPath(payrollRegistration, payrollAccess), "/payroll");
      assert.equal(isDestinationPermitted(PAYROLL_DESTINATIONS.find(d => d.id === "payroll-runs")!, payrollAccess), true);
      assert.equal(isDestinationPermitted(PAYROLL_DESTINATIONS.find(d => d.id === "payroll-setup")!, payrollAccess), false);
    });

    it("protects Wave 5 Projects and Field Service destinations with lifecycle rules", () => {
      const projectPlannerAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["projects"],
        permissions: ["projects.manage_team"],
        moduleStatuses: { projects: "Active" }
      };
      const dispatcherAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["service"],
        permissions: ["service.dispatch.read"],
        moduleStatuses: { service: "Active" }
      };
      const readOnlyAssetAccess: EffectiveAccess = {
        ...emptyAccess,
        entitledModules: ["fixed-assets"],
        permissions: ["assets.depreciation.post", "assets.audit.read"],
        moduleStatuses: { "fixed-assets": "ReadOnly" }
      };

      assert.equal(isDestinationPermitted(PROJECTS_DESTINATIONS.find(d => d.id === "projects-resource-load")!, projectPlannerAccess), true);
      assert.equal(isDestinationPermitted(PROJECTS_DESTINATIONS.find(d => d.id === "projects-templates")!, projectPlannerAccess), false);
      assert.equal(resolveModuleLandingPath({ id: "service", moduleIds: ["service"], path: "/service", navigationKey: "service", destinations: SERVICE_DESTINATIONS, icon: "service" }, dispatcherAccess), "/service");
      assert.equal(isDestinationPermitted(SERVICE_DESTINATIONS.find(d => d.id === "service-dispatch")!, dispatcherAccess), true);
      assert.equal(isDestinationPermitted(SERVICE_DESTINATIONS.find(d => d.id === "service-billing")!, dispatcherAccess), false);
      assert.equal(isDestinationPermitted(FIXED_ASSETS_DESTINATIONS.find(d => d.id === "fixed-assets-depreciation")!, readOnlyAssetAccess), true);
      assert.equal(canPerformAction(readOnlyAssetAccess, "assets.depreciation.post"), false);
      assert.equal(isDestinationPermitted(FIXED_ASSETS_DESTINATIONS.find(d => d.id === "fixed-assets-reports")!, readOnlyAssetAccess), true);
    });
  });
});
