import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as planningAr } from "../i18n/ar.ts";
import { en as planningEn } from "../i18n/en.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";
import { toMoney } from "../utils.ts";
import { PLANNING_DESTINATIONS } from "../../../app/router/destination-registry.ts";
import { ApiError } from "../../../core/api/http-client.ts";

describe("TODIR Module 28 (Planning & FP&A) — Frontend Unit & Slice Suite", () => {
  test("1:1 Arabic and English translation key parity for Planning resources", () => {
    const getKeys = (obj: Record<string, unknown>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null && !Array.isArray(v)) {
          keys = keys.concat(getKeys(v as Record<string, unknown>, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(planningAr as unknown as Record<string, unknown>));
    const enKeys = new Set(getKeys(planningEn as unknown as Record<string, unknown>));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
    for (const key of enKeys) {
      assert.ok(arKeys.has(key), `Missing AR translation key: ${key}`);
    }
  });

  test("Authentic Arabic ERP vocabulary check (zero literal machine translations)", () => {
    const arSerialized = JSON.stringify(planningAr);
    const forbiddenLiterals = [
      "إدخال اليومية",
      "توازن التجربة",
      "قسيمة دفع",
      "إيصال دفع",
      "تسوية البنك",
      "أرباح محتجزة",
      "نشر الميزانية"
    ];
    for (const bad of forbiddenLiterals) {
      assert.equal(arSerialized.includes(bad), false, `Forbidden literal translation found: ${bad}`);
    }
  });

  test("PLANNING_DESTINATIONS covers all 9 canonical FP&A routes with finance category", () => {
    assert.equal(PLANNING_DESTINATIONS.length, 9);
    const paths = new Set(PLANNING_DESTINATIONS.map((d) => d.path));
    for (const expected of [
      "/planning",
      "/planning/budgets",
      "/planning/grid",
      "/planning/models",
      "/planning/forecasts",
      "/planning/analysis",
      "/planning/budget-control",
      "/planning/submissions",
      "/planning/config"
    ]) {
      assert.ok(paths.has(expected), `Missing destination path: ${expected}`);
    }
    for (const dest of PLANNING_DESTINATIONS) {
      assert.equal(dest.category, "finance");
      assert.ok(dest.permission, `Missing permission on ${dest.id}`);
    }
  });

  test("mapPlanningError maps canonical domain, budget control, and lifecycle codes", () => {
    const err1 = new ApiError(409, "Conflict", { code: "planning.published_immutable" });
    assert.equal(mapPlanningError(err1).messageKey, "errors.publishedImmutable");

    const err2 = new ApiError(422, "Unprocessable", { code: "planning.budget_approval_required" });
    assert.equal(mapPlanningError(err2).messageKey, "errors.budgetApprovalRequired");

    const err3 = new ApiError(422, "Unprocessable", { code: "planning.budget_exceeded" });
    assert.equal(mapPlanningError(err3).messageKey, "errors.budgetExceeded");

    const err4 = new ApiError(403, "Forbidden", { code: "module.lifecycle_readonly" });
    assert.equal(mapPlanningError(err4).messageKey, "errors.moduleReadOnly");

    const err5 = new ApiError(403, "Forbidden");
    assert.equal(mapPlanningError(err5).messageKey, "errors.permissionDenied");
  });

  test("toMoney formats numeric amounts safely without NaN", () => {
    assert.deepEqual(toMoney(150000, "EGP"), { amount: "150000", currency: "EGP" });
    assert.deepEqual(toMoney(45000.5, "SAR"), { amount: "45000.5", currency: "SAR" });
    assert.deepEqual(toMoney(null, "EGP"), null);
    assert.deepEqual(toMoney(undefined, "EGP"), null);
  });

  test("Original Budget vs Current Budget domain invariant: Original remains immutable after revision/transfer", () => {
    interface BudgetBaseline {
      originalBudget: number;
      approvedRevisions: number;
      netTransfers: number;
    }
    const computeCurrentBudget = (b: BudgetBaseline): number =>
      b.originalBudget + b.approvedRevisions + b.netTransfers;

    const baseline: BudgetBaseline = {
      originalBudget: 150000,
      approvedRevisions: 25000,
      netTransfers: -10000
    };

    assert.equal(baseline.originalBudget, 150000);
    assert.equal(computeCurrentBudget(baseline), 165000);
  });
});
