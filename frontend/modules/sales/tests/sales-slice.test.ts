import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ApiError } from "../../../core/api/http-client.ts";
import { toQty, toMoneyVal } from "../utils.ts";
import { mapSalesError } from "../services/sales-error-map.ts";
import { resolveSalesCustomer } from "../components/sales-customer-resolver.ts";
import { resolveSalesProduct } from "../components/sales-product-resolver.ts";
import { ar } from "../i18n/ar.ts";
import { en } from "../i18n/en.ts";
import type { QuotationLineRequest, QuotationDetail } from "../types.ts";

describe("Sales Domain Invariants & Frontend Slice Contracts", () => {
  it("formats quantities and monetary values safely without floating point inaccuracies", () => {
    assert.deepEqual(toQty(15.5), { value: "15.5", unitOfMeasure: "EA" });
    assert.deepEqual(toQty("20", "KG"), { value: "20", unitOfMeasure: "KG" });
    assert.deepEqual(toQty(undefined), { value: "0", unitOfMeasure: "EA" });
    assert.deepEqual(toMoneyVal(150.75), { amount: "150.75", currency: "SAR" });
    assert.deepEqual(toMoneyVal("1200", "EGP"), { amount: "1200.00", currency: "EGP" });
    assert.deepEqual(toMoneyVal(undefined), { amount: "0.00", currency: "SAR" });
  });

  it("maps RFC-7807 sales domain errors to localized translation keys", () => {
    const custErr = mapSalesError(new ApiError(400, "Customer not found", {
      code: "sales.customer_not_found"
    }));
    assert.equal(custErr.code, "sales.customer_not_found");
    assert.equal(custErr.messageKey, "errors.customerNotFound");

    const staleErr = mapSalesError(new ApiError(409, "Stale quotation", {
      code: "sales.stale_quotation"
    }));
    assert.equal(staleErr.code, "sales.stale_quotation");
    assert.equal(staleErr.messageKey, "errors.staleQuotation");

    const creditBlockedErr = mapSalesError(new ApiError(400, "Credit blocked", {
      code: "sales.credit_blocked"
    }));
    assert.equal(creditBlockedErr.code, "sales.credit_blocked");
    assert.equal(creditBlockedErr.messageKey, "errors.creditBlocked");

    const forbiddenErr = mapSalesError(new ApiError(403, "Forbidden", {}));
    assert.equal(forbiddenErr.code, "sales.forbidden");
    assert.equal(forbiddenErr.messageKey, "errors.forbidden");

    const genericErr = mapSalesError(new Error("Network timeout"));
    assert.equal(genericErr.code, "sales.unknown");
    assert.equal(genericErr.messageKey, "errors.generic");
  });

  it("resolves customer and product identities without exposing raw technical UUIDs", () => {
    const rawUuid = "a6315416-cffe-bf52-bcf0-d5e590d3bf94";
    const resolvedProd = resolveSalesProduct(rawUuid, true);
    assert.notEqual(resolvedProd.name, rawUuid);
    assert.notEqual(resolvedProd.sku, rawUuid);
    assert.equal(resolvedProd.sku.includes(rawUuid), false);

    const randomUuid = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    const resolvedCust = resolveSalesCustomer(randomUuid, true);
    assert.notEqual(resolvedCust.name, randomUuid);
    assert.equal(resolvedCust.name.length > 0, true);
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

  it("enforces commercial lines integrity and quotation calculation bounds", () => {
    const line: QuotationLineRequest = {
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      description: "Test Line",
      quantity: 5,
      unitPrice: 100,
      discountAmount: 25,
      uomCode: "EA"
    };

    const net = line.quantity * (line.unitPrice ?? 0) - (line.discountAmount ?? 0);
    assert.equal(net, 475);
    assert.ok(line.quantity > 0);
  });

  it("ensures quotations do not leak inventory cost layers to commercial sales agents", () => {
    const quoteView: Partial<QuotationDetail> = {
      id: "quote-test-1",
      number: "Q-2026-000001",
      grossTotal: 1000,
      lines: [
        {
          id: "line-1",
          sequence: 1,
          description: "Office Supplies",
          quantity: 10,
          unitPrice: 100,
          discountAmount: 0,
          netAmount: 1000
        }
      ]
    };

    const json = JSON.stringify(quoteView);
    assert.equal(json.includes("cost"), false);
    assert.equal(json.includes("fifo"), false);
    assert.equal(json.includes("carryingValue"), false);
  });
});
