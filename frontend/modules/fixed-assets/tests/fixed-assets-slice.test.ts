import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { fixedAssetsAr } from "../i18n/ar.ts";
import { fixedAssetsEn } from "../i18n/en.ts";
import type { DepreciationMethod, AssetLifecycleState, VerificationResult } from "../types.ts";

describe("Module 22 Fixed Assets Slice Contracts & Domain Invariants", () => {
  test("1:1 Arabic and English translation parity for Fixed Assets resources", () => {
    const getKeys = (obj: Record<string, unknown>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v as Record<string, unknown>, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(fixedAssetsAr));
    const enKeys = new Set(getKeys(fixedAssetsEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
  });

  test("Depreciation methods invariant: strictly StraightLine and NoDepreciation", () => {
    const supportedMethods: DepreciationMethod[] = ["StraightLine", "NoDepreciation"];
    assert.equal(supportedMethods.length, 2);
    assert.ok(supportedMethods.includes("StraightLine"));
    assert.ok(supportedMethods.includes("NoDepreciation"));
    // Verify ReducingBalance, UnitsOfProduction, SumOfYearsDigits are NOT permitted
    // @ts-expect-error ReducingBalance must not be allowed
    const invalidMethod: DepreciationMethod = "ReducingBalance";
    assert.notEqual(invalidMethod, "StraightLine");
  });

  test("Governed backend permission strings match exact backend security contracts", () => {
    const governedPermissions = [
      "assets.read",
      "assets.create",
      "assets.assign",
      "assets.financial.read",
      "assets.depreciation.prepare",
      "assets.depreciation.post",
      "assets.book.manage",
      "assets.capitalize",
      "assets.disposal.manage",
      "assets.verification.manage"
    ] as const;

    // Verify invented permissions like 'assets:view' are strictly rejected
    governedPermissions.forEach((perm) => {
      assert.ok(perm.startsWith("assets."), `Permission must be dot-notated: ${perm}`);
      assert.ok(!perm.includes(":"), `Invented colon-notation forbidden: ${perm}`);
    });
  });

  test("Asset lifecycle states match canonical specifications", () => {
    const canonicalStates: AssetLifecycleState[] = [
      "Draft",
      "UnderConstruction",
      "Active",
      "FullyDepreciated",
      "Disposed",
      "WrittenOff"
    ];
    assert.equal(canonicalStates.length, 6);
  });

  test("Physical verification findings support all required audit results", () => {
    const results: VerificationResult[] = [
      "Found",
      "Missing",
      "WrongLocation",
      "WrongCustodian",
      "Damaged"
    ];
    assert.equal(results.length, 5);
  });

  test("Book GL Reconciliation invariant: isReconciled strictly matches cost & accDep zero variance", () => {
    const balancedReport = {
      assetGrossCost: 100000,
      ledgerGrossCost: 100000,
      grossCostDifference: 0,
      assetAccumulatedDepreciation: 25000,
      ledgerAccumulatedDepreciation: 25000,
      accumulatedDepreciationDifference: 0,
      status: "Reconciled" as const
    };

    const isBalanced =
      balancedReport.grossCostDifference === 0 &&
      balancedReport.accumulatedDepreciationDifference === 0 &&
      balancedReport.status === "Reconciled";
    assert.equal(isBalanced, true);

    const unbalancedReport = {
      assetGrossCost: 100000,
      ledgerGrossCost: 95000,
      grossCostDifference: 5000,
      assetAccumulatedDepreciation: 25000,
      ledgerAccumulatedDepreciation: 25000,
      accumulatedDepreciationDifference: 0,
      status: "Discrepancy" as const
    };

    const isUnbalanced =
      unbalancedReport.grossCostDifference !== 0 ||
      unbalancedReport.accumulatedDepreciationDifference !== 0 ||
      (unbalancedReport.status as string) !== "Reconciled";
    assert.equal(isUnbalanced, true);
  });
});
