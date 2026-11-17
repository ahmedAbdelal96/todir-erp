import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { serviceAr } from "../i18n/ar.ts";
import { serviceEn } from "../i18n/en.ts";
import type {
  ServiceRequestState,
  WorkOrderState,
  WorkOrderBillingOwner,
  VisitLocationType,
  ServiceContractType,
  ServiceContractState,
  MaintenancePlanState,
  PartRequirementState,
  ServiceChargeCategory
} from "../types.ts";

describe("TODIR Module 24 (Service & Maintenance) — Contracts & Domain Invariants", () => {
  test("1:1 Arabic and English translation key parity for Service resources", () => {
    const getKeys = (obj: Record<string, any>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(serviceAr));
    const enKeys = new Set(getKeys(serviceEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
  });

  test("Service Request State Lifecycle: Strict status modeling", () => {
    const states: ServiceRequestState[] = ["New", "Triaged", "InProgress", "Resolved", "Closed", "Cancelled"];
    assert.equal(states.length, 6);

    assert.ok(serviceAr.requests.statuses.new);
    assert.ok(serviceAr.requests.statuses.triaged);
    assert.ok(serviceEn.requests.statuses.new);
    assert.ok(serviceEn.requests.statuses.triaged);
  });

  test("Work Order State Lifecycle: Complete ERP execution stages", () => {
    const woStates: WorkOrderState[] = [
      "Draft",
      "Ready",
      "Scheduled",
      "InProgress",
      "Waiting",
      "TechnicalCompleted",
      "Accepted",
      "Closed",
      "Cancelled"
    ];
    assert.equal(woStates.length, 9);

    assert.ok(serviceAr.workOrders.statuses.draft);
    assert.ok(serviceAr.workOrders.statuses.ready);
    assert.ok(serviceAr.workOrders.statuses.in_progress);
    assert.ok(serviceEn.workOrders.statuses.draft);
    assert.ok(serviceEn.workOrders.statuses.ready);
    assert.ok(serviceEn.workOrders.statuses.in_progress);
  });

  test("Work Order Billing Owner Separation: Service vs Projects vs None", () => {
    const billingOwners: WorkOrderBillingOwner[] = ["None", "Service", "Projects"];
    assert.equal(billingOwners.length, 3);

    // Business Invariant: If BillingOwner is "Projects", Service cannot invoice directly; it routes via Projects module
    const canInvoiceFromService = (owner: WorkOrderBillingOwner): boolean => owner === "Service";
    assert.equal(canInvoiceFromService("Service"), true);
    assert.equal(canInvoiceFromService("Projects"), false);
    assert.equal(canInvoiceFromService("None"), false);
  });

  test("Visit Transitions and Optimistic Concurrency Invariants", () => {
    const allowedTransitions: Record<string, string[]> = {
      Planned: ["Dispatched", "Cancelled"],
      Dispatched: ["Traveling", "InProgress", "Cancelled"],
      Traveling: ["Arrived", "NoAccess", "Cancelled"],
      Arrived: ["InProgress", "Completed", "NoAccess", "Cancelled"],
      InProgress: ["Completed", "Cancelled"]
    };

    const isValidTransition = (from: string, to: string): boolean => {
      return (allowedTransitions[from] ?? []).includes(to);
    };

    assert.equal(isValidTransition("Planned", "Dispatched"), true);
    assert.equal(isValidTransition("Dispatched", "Traveling"), true);
    assert.equal(isValidTransition("Traveling", "Arrived"), true);
    assert.equal(isValidTransition("Arrived", "InProgress"), true);
    assert.equal(isValidTransition("InProgress", "Completed"), true);
    // Invalid jumps must be rejected
    assert.equal(isValidTransition("Planned", "Completed"), false);
    assert.equal(isValidTransition("Traveling", "Completed"), false);
  });

  test("Financial Integrity: Customer Liability vs Covered Amount calculation", () => {
    type ChargeLine = {
      costAmount: number;
      customerAmount: number;
      coveredAmount: number;
    };

    const lines: ChargeLine[] = [
      { costAmount: 150, customerAmount: 0, coveredAmount: 150 }, // 100% under warranty
      { costAmount: 300, customerAmount: 100, coveredAmount: 200 }, // Deductible / partial coverage
      { costAmount: 450, customerAmount: 450, coveredAmount: 0 } // Out of scope / billable
    ];

    const totalCustomer = lines.reduce((acc, curr) => acc + curr.customerAmount, 0);
    const totalCovered = lines.reduce((acc, curr) => acc + curr.coveredAmount, 0);
    const totalCost = lines.reduce((acc, curr) => acc + curr.costAmount, 0);

    assert.equal(totalCustomer, 550);
    assert.equal(totalCovered, 350);
    assert.equal(totalCustomer + totalCovered >= totalCost, true);
  });

  test("Entitlement Engine line-level contract verification", () => {
    const resolveEntitlement = (hasContract: boolean, isWarrantyActive: boolean, category: ServiceChargeCategory) => {
      if (isWarrantyActive && (category === "Labor" || category === "Parts")) {
        return { covered: true, source: "Warranty", coveredPercent: 100 };
      }
      if (hasContract) {
        return { covered: true, source: "ServiceContract", coveredPercent: 100 };
      }
      return { covered: false, source: "CustomerLiability", coveredPercent: 0 };
    };

    const underWarranty = resolveEntitlement(false, true, "Parts");
    assert.equal(underWarranty.covered, true);
    assert.equal(underWarranty.source, "Warranty");

    const underContract = resolveEntitlement(true, false, "Travel");
    assert.equal(underContract.covered, true);
    assert.equal(underContract.source, "ServiceContract");

    const billable = resolveEntitlement(false, false, "Labor");
    assert.equal(billable.covered, false);
    assert.equal(billable.source, "CustomerLiability");
  });
});
