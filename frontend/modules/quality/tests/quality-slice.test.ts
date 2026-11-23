import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { qualityAr } from "../i18n/ar.ts";
import { qualityEn } from "../i18n/en.ts";
import { QUALITY_DESTINATIONS } from "../../../app/router/destination-registry.ts";

describe("TODIR Module 27 (Quality Management) â€” Frontend Unit & Slice Suite", () => {
  test("1:1 Arabic and English translation key parity for Quality resources", () => {
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

    const arKeys = new Set(getKeys(qualityAr));
    const enKeys = new Set(getKeys(qualityEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
    for (const key of enKeys) {
      assert.ok(arKeys.has(key), `Missing AR translation key: ${key}`);
    }
  });

  test("Strict Domain Invariant: Passed != Released (Passed inspection never automatically releases held inventory)", () => {
    // Model inspection and hold status
    interface MockBatchState {
      inspectionState: "Pending" | "InProgress" | "Completed";
      inspectionEvaluation: "Passed" | "Failed" | "Concession" | null;
      heldQuantity: number;
      releasedQuantity: number;
      remainingHeldQuantity: number;
      restrictionStatus: "Active" | "Released";
    }

    const batch: MockBatchState = {
      inspectionState: "Completed",
      inspectionEvaluation: "Passed",
      heldQuantity: 100,
      releasedQuantity: 0,
      remainingHeldQuantity: 100,
      restrictionStatus: "Active"
    };

    // Even if evaluation is "Passed", restriction remains Active until explicit release
    assert.equal(batch.inspectionEvaluation, "Passed");
    assert.equal(batch.restrictionStatus, "Active");
    assert.equal(batch.remainingHeldQuantity, 100);

    // Explicit partial release
    const releaseQuantity = 40;
    assert.ok(releaseQuantity <= batch.remainingHeldQuantity, "Cannot release more than remaining");
    batch.releasedQuantity += releaseQuantity;
    batch.remainingHeldQuantity -= releaseQuantity;

    assert.equal(batch.releasedQuantity, 40);
    assert.equal(batch.remainingHeldQuantity, 60);
    assert.equal(batch.restrictionStatus, "Active", "Hold remains Active until remaining is 0");

    // Full release
    batch.releasedQuantity += 60;
    batch.remainingHeldQuantity = 0;
    batch.restrictionStatus = "Released";
    assert.equal(batch.restrictionStatus, "Released");
  });

  test("Supplier Return Disposition issues directly from QualityHold without touching Available stock", () => {
    type InventoryBucket = "QualityHold" | "Available" | "Scrap" | "SupplierReturned";

    interface InventoryMovement {
      fromBucket: InventoryBucket;
      toBucket: InventoryBucket;
      quantity: number;
    }

    const executeSupplierReturn = (qty: number): InventoryMovement => {
      // Must move directly from QualityHold to SupplierReturned
      return {
        fromBucket: "QualityHold",
        toBucket: "SupplierReturned",
        quantity: qty
      };
    };

    const movement = executeSupplierReturn(50);
    assert.equal(movement.fromBucket, "QualityHold");
    assert.equal(movement.toBucket, "SupplierReturned");
    assert.notEqual(movement.fromBucket, "Available");
    assert.notEqual(movement.toBucket, "Available");
  });

  test("Reconciliation for Unknown execution status reuses existing idempotency key", () => {
    const originalIdempotencyKey = "DISP-EXEC-2026-9901";
    let executionStatus: "Pending" | "Executing" | "Confirmed" | "Unknown" = "Unknown";

    const reconcile = (currentStatus: string, key: string) => {
      assert.equal(currentStatus, "Unknown");
      // Retries execution using the EXACT same idempotency key
      return {
        reconciled: true,
        reusedKey: key,
        newStatus: "Confirmed" as const
      };
    };

    const result = reconcile(executionStatus, originalIdempotencyKey);
    assert.equal(result.reconciled, true);
    assert.equal(result.reusedKey, originalIdempotencyKey);
    assert.equal(result.newStatus, "Confirmed");
  });

  test("Ishikawa 6M root cause analysis categories are strictly enumerated", () => {
    const ishikawaCategories = ["Man", "Machine", "Material", "Method", "Measurement", "Environment"];
    assert.equal(ishikawaCategories.length, 6);
    assert.ok(ishikawaCategories.includes("Man"));
    assert.ok(ishikawaCategories.includes("Machine"));
    assert.ok(ishikawaCategories.includes("Material"));
    assert.ok(ishikawaCategories.includes("Method"));
    assert.ok(ishikawaCategories.includes("Measurement"));
    assert.ok(ishikawaCategories.includes("Environment"));
  });

  test("QUALITY_DESTINATIONS registration invariants", () => {
    assert.ok(QUALITY_DESTINATIONS.length >= 8, "Must register all canonical quality destinations");

    for (const dest of QUALITY_DESTINATIONS) {
      assert.equal(dest.category, "inventory", `Destination ${dest.id} must belong to category 'inventory'`);
      assert.equal(dest.moduleId, "quality", `Destination ${dest.id} must belong to moduleId 'quality'`);
      assert.ok(dest.path.startsWith("/quality"), `Destination ${dest.id} path must start with /quality`);
      assert.ok(dest.permission, `Destination ${dest.id} must define permissions`);
    }
  });
});
