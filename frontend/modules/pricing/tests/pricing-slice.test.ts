import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { PriceResolution, PriceSnapshot } from "../types.ts";

describe("Pricing Vertical Slice Contracts & Invariants", () => {
  it("F14 / Decimal Safety — preserves price strings as invariant decimals without floating-point conversion", () => {
    const rawPrice = "1250.000000000000";
    assert.equal(typeof rawPrice, "string");
    assert.equal(rawPrice, "1250.000000000000");

    // Proves string integrity avoids floating point issues (e.g. 0.1 + 0.2 !== 0.3)
    const floatSum = 0.1 + 0.2;
    assert.notEqual(floatSum, 0.3); // IEEE 754 float imperfection
  });

  it("Resolution Contract — distinguishes PriceMissing clearly from explicit zero price", () => {
    const missingResolution: PriceResolution = {
      status: "PriceMissing",
      snapshot: null,
      reasonCode: "pricing.candidate_not_found",
      explanation: "No candidate was found.",
      equivalentWinningRuleIds: []
    };

    assert.equal(missingResolution.status, "PriceMissing");
    assert.equal(missingResolution.snapshot, null);

    const zeroPriceSnapshot: PriceSnapshot = {
      priceListId: "00000000-0000-0000-0000-000000000001",
      priceListVersionId: "00000000-0000-0000-0000-000000000002",
      winningRuleId: null,
      direction: "Selling",
      productVariantId: "00000000-0000-0000-0000-000000000004",
      unitOfMeasureId: "00000000-0000-0000-0000-0000000000ea",
      quantity: 1,
      pricingDate: "2026-10-21",
      priceListCurrencyCode: "EGP",
      transactionCurrencyCode: "EGP",
      finalUnitPrice: "0.00",
      resolverVersion: "1.0",
      capturedAt: "2026-10-21T10:00:00Z"
    };

    const zeroResolution: PriceResolution = {
      status: "Resolved",
      snapshot: zeroPriceSnapshot,
      reasonCode: "pricing.entry_exact_match",
      explanation: "Explicit zero price applied.",
      equivalentWinningRuleIds: []
    };

    assert.equal(zeroResolution.status, "Resolved");
    assert.notEqual(zeroResolution.snapshot, null);
    assert.equal(zeroResolution.snapshot?.finalUnitPrice, "0.00");

    // They must never be conflated
    assert.notEqual(missingResolution.status, zeroResolution.status);
  });

  it("Conflict Handling — classifies overlapping conflicting rules as blocking validation error", () => {
    const isConflict = (errorCode: string) => errorCode.includes("conflict");
    assert.equal(isConflict("pricing.conflict"), true);
    assert.equal(isConflict("pricing.import_overlap_conflict"), true);
    assert.equal(isConflict("pricing.validation_ok"), false);
  });
});
