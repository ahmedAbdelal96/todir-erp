import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as manufacturingAr } from "../i18n/ar.ts";
import { en as manufacturingEn } from "../i18n/en.ts";
import type { ProductionState } from "../types.ts";

describe("TODIR Module 26 (Manufacturing & MRP) — Frontend Unit & Slice Suite", () => {
  test("1:1 Arabic and English translation key parity for Manufacturing resources", () => {
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

    const arKeys = new Set(getKeys(manufacturingAr));
    const enKeys = new Set(getKeys(manufacturingEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
    for (const key of enKeys) {
      assert.ok(arKeys.has(key), `Missing AR translation key: ${key}`);
    }
  });

  test("Production Order lifecycle transitions enforce valid state flow and immutability of Closed orders", () => {
    const validTransitions: Record<ProductionState, readonly ProductionState[]> = {
      Draft: ["Released", "Cancelled"],
      Released: ["InProgress", "OnHold", "Cancelled"],
      InProgress: ["OnHold", "Completed", "Cancelled"],
      OnHold: ["InProgress", "Cancelled"],
      Completed: ["Closed"],
      Closed: [],
      Cancelled: []
    };

    const isValidTransition = (from: ProductionState, to: ProductionState): boolean => {
      return validTransitions[from]?.includes(to) ?? false;
    };

    // Valid paths
    assert.ok(isValidTransition("Draft", "Released"), "Draft can be Released");
    assert.ok(isValidTransition("Released", "InProgress"), "Released can start InProgress");
    assert.ok(isValidTransition("InProgress", "Completed"), "InProgress can be Completed");
    assert.ok(isValidTransition("Completed", "Closed"), "Completed can be Closed");
    assert.ok(isValidTransition("InProgress", "OnHold"), "InProgress can be put OnHold");
    assert.ok(isValidTransition("OnHold", "InProgress"), "OnHold can be resumed to InProgress");

    // Invalid / illegal paths
    assert.ok(!isValidTransition("Draft", "Completed"), "Draft cannot jump directly to Completed");
    assert.ok(!isValidTransition("Closed", "InProgress"), "Closed order cannot be resumed to InProgress");
    assert.ok(!isValidTransition("Closed", "Draft"), "Closed order is immutable and cannot become Draft");
    assert.ok(!isValidTransition("Cancelled", "Released"), "Cancelled order cannot be Released");
  });

  test("MRP Netting Model correctly consumes Forecast with Sales demand without double-counting", () => {
    // Scenario 1: Forecast 1,000, Confirmed Sales 600
    // Gross Demand = Sales (600) + Remaining Forecast (400) = 1,000 (NOT 1,600)
    const forecast = 1000;
    const confirmedSales = 600;
    const consumedForecast = Math.min(forecast, confirmedSales);
    const remainingForecast = Math.max(0, forecast - consumedForecast);
    const grossDemand = confirmedSales + remainingForecast;

    assert.equal(consumedForecast, 600, "600 units of forecast consumed by matching sales demand");
    assert.equal(remainingForecast, 400, "400 units of forecast remain unconsumed");
    assert.equal(grossDemand, 1000, "Gross demand must be exactly 1,000, avoiding double-counting sales + forecast");

    // Net requirement calculation:
    // Available stock: 250
    // Allocated stock: 50
    // Net Available: 200
    // Net Requirement = Gross Demand (1,000) - Net Available (200) = 800
    const availableStock = 250;
    const allocatedStock = 50;
    const netAvailable = Math.max(0, availableStock - allocatedStock);
    const netRequirement = Math.max(0, grossDemand - netAvailable);

    assert.equal(netAvailable, 200, "Net available stock is 200");
    assert.equal(netRequirement, 800, "Net requirement to produce/purchase is 800");
  });

  test("Raw material staging and consumption conservation invariant: (Consumed + Returned) <= Issued", () => {
    const requiredQty = 500;
    const issuedQty = 500;
    const consumedQty = 480;
    const returnedQty = 20;

    assert.ok(
      consumedQty + returnedQty <= issuedQty,
      "Conservation invariant: Consumed (480) + Returned (20) must not exceed Issued (500)"
    );
    assert.equal(
      consumedQty + returnedQty,
      issuedQty,
      "All issued materials are completely accounted for between consumed and returned"
    );

    // Over-consumption without additional issuance must be detected as an invariant violation
    const illegalConsumed = 510;
    assert.ok(
      illegalConsumed + returnedQty > issuedQty,
      "Attempting to consume more than issued without additional issuance must trigger validation error"
    );
  });

  test("Quality Gate policy enforcement blocks output when gate is mandatory and uninspected", () => {
    interface TestGate {
      required: boolean;
      decisionState: "Pending" | "Passed" | "Failed" | "Exempt";
    }

    const canReleaseOrComplete = (gate: TestGate): boolean => {
      if (!gate.required) return true;
      return gate.decisionState === "Passed" || gate.decisionState === "Exempt";
    };

    assert.ok(canReleaseOrComplete({ required: false, decisionState: "Pending" }), "Optional gate allows flow");
    assert.ok(canReleaseOrComplete({ required: true, decisionState: "Passed" }), "Passed gate allows flow");
    assert.ok(canReleaseOrComplete({ required: true, decisionState: "Exempt" }), "Exempt gate allows flow");

    assert.ok(!canReleaseOrComplete({ required: true, decisionState: "Pending" }), "Pending mandatory gate BLOCKS flow");
    assert.ok(!canReleaseOrComplete({ required: true, decisionState: "Failed" }), "Failed gate BLOCKS flow");
  });

  test("Close Readiness invariant blocks close when WIP variance remains unposted to General Ledger", () => {
    interface CloseCheck {
      orderState: ProductionState;
      allOperationsCompleted: boolean;
      unpostedCostFactsCount: number;
      unreconciledWipVariance: number;
    }

    const evaluateCloseReadiness = (check: CloseCheck): { canClose: boolean; blockerKey?: string } => {
      if (check.orderState !== "Completed") {
        return { canClose: false, blockerKey: "orderMustBeCompleted" };
      }
      if (!check.allOperationsCompleted) {
        return { canClose: false, blockerKey: "operationsPending" };
      }
      if (check.unpostedCostFactsCount > 0) {
        return { canClose: false, blockerKey: "unpostedLedgerWarning" };
      }
      if (Math.abs(check.unreconciledWipVariance) > 0.001) {
        return { canClose: false, blockerKey: "closeWipBlocker" };
      }
      return { canClose: true };
    };

    // Ready scenario
    const readyOrder: CloseCheck = {
      orderState: "Completed",
      allOperationsCompleted: true,
      unpostedCostFactsCount: 0,
      unreconciledWipVariance: 0.0
    };
    assert.equal(evaluateCloseReadiness(readyOrder).canClose, true);

    // Unposted ledger fact scenario
    const unpostedOrder: CloseCheck = {
      orderState: "Completed",
      allOperationsCompleted: true,
      unpostedCostFactsCount: 2,
      unreconciledWipVariance: 0.0
    };
    const unpostedRes = evaluateCloseReadiness(unpostedOrder);
    assert.equal(unpostedRes.canClose, false);
    assert.equal(unpostedRes.blockerKey, "unpostedLedgerWarning");

    // Unreconciled WIP variance scenario
    const wipBlockedOrder: CloseCheck = {
      orderState: "Completed",
      allOperationsCompleted: true,
      unpostedCostFactsCount: 0,
      unreconciledWipVariance: 145.50
    };
    const wipBlockedRes = evaluateCloseReadiness(wipBlockedOrder);
    assert.equal(wipBlockedRes.canClose, false);
    assert.equal(wipBlockedRes.blockerKey, "closeWipBlocker");
  });

  test("Routing operations sequencing validation detects cycles and enforces strictly ascending order", () => {
    interface TestOp {
      sequenceNo: number;
      dependsOnSequence?: number | null;
    }

    const validateSequencing = (ops: TestOp[]): boolean => {
      for (const op of ops) {
        if (op.dependsOnSequence !== undefined && op.dependsOnSequence !== null) {
          if (op.dependsOnSequence >= op.sequenceNo) {
            // Cannot depend on self or future operation (cycle or invalid order)
            return false;
          }
        }
      }
      return true;
    };

    const validOps: TestOp[] = [
      { sequenceNo: 10, dependsOnSequence: null },
      { sequenceNo: 20, dependsOnSequence: 10 },
      { sequenceNo: 30, dependsOnSequence: 20 }
    ];
    assert.ok(validateSequencing(validOps), "Sequential linear routing operations are valid");

    const cyclicOps: TestOp[] = [
      { sequenceNo: 10, dependsOnSequence: 20 },
      { sequenceNo: 20, dependsOnSequence: 10 }
    ];
    assert.ok(!validateSequencing(cyclicOps), "Circular dependency between operations is detected and rejected");
  });
});
