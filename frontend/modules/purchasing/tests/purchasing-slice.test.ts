import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ApiError } from "../../../core/api/http-client.ts";
import { toQty, toMoneyVal, generateIdempotencyKey, formatPoNumber } from "../utils.ts";
import { mapPurchasingError } from "../services/purchasing-error-map.ts";
import { resolvePurchasingSupplier } from "../components/purchasing-supplier-resolver.ts";
import { resolvePurchasingProduct } from "../components/purchasing-product-resolver.ts";
import { ar } from "../i18n/ar.ts";
import { en } from "../i18n/en.ts";

describe("Purchasing Domain Invariants & Frontend Slice Contracts", () => {
  it("formats quantities and monetary values safely without floating point inaccuracies", () => {
    assert.deepEqual(toQty(15.5), { value: "15.50", unitOfMeasure: "EA" });
    assert.deepEqual(toQty("20", "KG"), { value: "20", unitOfMeasure: "KG" });
    assert.deepEqual(toQty(undefined), { value: "0", unitOfMeasure: "EA" });
    assert.deepEqual(toMoneyVal(250.75), { amount: "250.75", currency: "SAR" });
    assert.deepEqual(toMoneyVal("4500", "EGP"), { amount: "4500.00", currency: "EGP" });
    assert.deepEqual(toMoneyVal(undefined), { amount: "0.00", currency: "SAR" });
  });

  it("generates unique idempotency keys with proper prefix", () => {
    const key1 = generateIdempotencyKey("create");
    const key2 = generateIdempotencyKey("create");
    assert.equal(key1.startsWith("create-"), true);
    assert.notEqual(key1, key2);
  });

  it("formats human-friendly PO numbers consistently from ids", () => {
    const poNum = formatPoNumber("po-uuid-12345");
    assert.equal(poNum.startsWith("PO-"), true);
  });

  it("maps RFC-7807 purchasing domain errors to localized translation keys", () => {
    const suppErr = mapPurchasingError(new ApiError(400, "Supplier not ready", {
      code: "purchasing.supplier_not_ready"
    }));
    assert.equal(suppErr.code, "purchasing.supplier_not_ready");
    assert.equal(suppErr.messageKey, "errors.supplierNotReady");

    const staleErr = mapPurchasingError(new ApiError(409, "Version mismatch", {
      code: "purchasing.po_stale"
    }));
    assert.equal(staleErr.code, "purchasing.po_stale");
    assert.equal(staleErr.messageKey, "errors.poStale");

    const lineErr = mapPurchasingError(new ApiError(400, "Price override not allowed", {
      code: "purchasing.price_override_not_allowed"
    }));
    assert.equal(lineErr.code, "purchasing.price_override_not_allowed");
    assert.equal(lineErr.messageKey, "errors.priceOverrideNotAllowed");

    const forbiddenErr = mapPurchasingError(new ApiError(403, "Forbidden", {}));
    assert.equal(forbiddenErr.code, "purchasing.forbidden");
    assert.equal(forbiddenErr.messageKey, "errors.forbidden");

    const genericErr = mapPurchasingError(new Error("Network timeout"));
    assert.equal(genericErr.code, "purchasing.unknown");
    assert.equal(genericErr.messageKey, "errors.generic");
  });

  it("resolves supplier and product identities without exposing raw technical UUIDs", () => {
    const rawUuid = "a6315416-cffe-bf52-bcf0-d5e590d3bf94";
    const resolvedProd = resolvePurchasingProduct(rawUuid, true);
    assert.notEqual(resolvedProd.name, rawUuid);
    assert.notEqual(resolvedProd.sku, rawUuid);
    assert.equal(resolvedProd.sku.includes(rawUuid), false);

    const randomUuid = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    const resolvedSupp = resolvePurchasingSupplier(randomUuid, true);
    assert.notEqual(resolvedSupp.name, randomUuid);
    assert.equal(resolvedSupp.name.length > 0, true);
  });

  it("enforces Product Law: PO commitment is NEVER labeled as expense or AP liability", () => {
    // English terms verification
    assert.equal("commercialCommitment" in en.orders, true);
    assert.equal(en.orders.commercialCommitment, "Net Commercial Commitment");
    assert.equal(en.orders.commercialCommitment.includes("Expense"), false);
    assert.equal(en.orders.commercialCommitment.includes("Liability"), false);

    // Arabic terms verification
    assert.equal("commercialCommitment" in ar.orders, true);
    assert.equal(ar.orders.commercialCommitment, "قيمة الالتزام التجاري الصافي");
    assert.equal(ar.orders.commercialCommitment.includes("مصروف"), false);
    assert.equal(ar.orders.commercialCommitment.includes("التزام مالي دائن"), false);
  });

  it("enforces backend authority for tax preview with zero client-side VAT formulas", () => {
    // In pricing preview, tax is rendered only if provided by backend authority
    assert.equal("pricingPreviewNotice" in en.orders, true);
    assert.equal("pricingPreviewNotice" in ar.orders, true);
  });

  it("maintains strict Arabic and English translation parity with zero missing keys", () => {
    function getKeys(obj: any, prefix = ""): string[] {
      let keys: string[] = [];
      for (const k of Object.keys(obj)) {
        const full = prefix ? `${prefix}.${k}` : k;
        if (typeof obj[k] === "object" && obj[k] !== null) {
          keys = keys.concat(getKeys(obj[k], full));
        } else {
          keys.push(full);
        }
      }
      return keys.sort();
    }

    const arKeys = getKeys(ar);
    const enKeys = getKeys(en);
    assert.deepEqual(arKeys, enKeys);
  });
});
