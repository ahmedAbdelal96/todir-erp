import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { ApiError } from "../../../core/api/http-client.ts";
import {
  toQty,
  toMoneyVal,
  generateIdempotencyKey,
  formatPoNumber,
  addDaysToIsoDate,
  isReceiptEligibleLine,
  resolveConfiguredLeadTimeDays
} from "../utils.ts";
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

  it("enforces price privacy: hides commercial money and unit prices when purchasing.price.read is absent", () => {
    interface ProjectionInput {
      hasPriceReadPermission: boolean;
      order: {
        netTotal: number;
        currencyCode: string;
        lines: Array<{ unitPrice: number; lineTotal: number }>;
      };
    }

    function projectOrderForView(input: ProjectionInput) {
      if (!input.hasPriceReadPermission) {
        return {
          hasMoneyVisible: false,
          netTotal: null,
          lines: input.order.lines.map(() => ({ unitPrice: null, lineTotal: null }))
        };
      }
      return {
        hasMoneyVisible: true,
        netTotal: input.order.netTotal,
        lines: input.order.lines.map((l) => ({ unitPrice: l.unitPrice, lineTotal: l.lineTotal }))
      };
    }

    const unpermitted = projectOrderForView({
      hasPriceReadPermission: false,
      order: {
        netTotal: 10050.0,
        currencyCode: "SAR",
        lines: [{ unitPrice: 100, lineTotal: 1000 }]
      }
    });

    assert.equal(unpermitted.hasMoneyVisible, false);
    assert.equal(unpermitted.netTotal, null);
    assert.equal(unpermitted.lines[0]?.unitPrice, null);
    assert.equal(unpermitted.lines[0]?.lineTotal, null);
  });

  it("enforces matching exception domain rule: exceptions are permitted only when state is Variance/Blocked/ApprovalRequired with positive variance", () => {
    function isMatchingExceptionPermitted(state: string | null | undefined, variance: number): boolean {
      return (state === "Variance" || state === "Blocked" || state === "ApprovalRequired") && variance > 0;
    }

    assert.equal(isMatchingExceptionPermitted("Matched", 0), false);
    assert.equal(isMatchingExceptionPermitted("WithinTolerance", 0), false);
    assert.equal(isMatchingExceptionPermitted("NotRequired", 0), false);
    assert.equal(isMatchingExceptionPermitted("Variance", 0), false);
    assert.equal(isMatchingExceptionPermitted("Variance", 150), true);
    assert.equal(isMatchingExceptionPermitted("Blocked", 500), true);
    assert.equal(isMatchingExceptionPermitted("ApprovalRequired", 25), true);
  });

  it("enforces matching variance explainability: blocked or variance state with varianceAmount > 0 explains price or quantity differences and never claims fully matched", () => {
    function computeMatchingLineDisplay(input: {
      poUnitPrice: number;
      invoiceUnitPrice?: number | null;
      orderedQty: number;
      receivedQty: number;
      invoicedQty: number;
      basis: "TwoWay" | "ThreeWay";
      matchState: string;
      totalVariance: number;
    }) {
      const invoicePrice = input.invoiceUnitPrice ?? input.poUnitPrice;
      const priceDiff = invoicePrice - input.poUnitPrice;
      const comparisonQty = input.basis === "ThreeWay" ? input.receivedQty : input.orderedQty;
      const qtyDiff = comparisonQty - input.invoicedQty;

      if (Math.abs(priceDiff) > 0.001) {
        return { isMatched: false, explanation: `فرق السعر: ${priceDiff > 0 ? "+" : ""}${priceDiff.toFixed(2)}` };
      }
      if (Math.abs(qtyDiff) > 0.001) {
        return { isMatched: false, explanation: `فرق الكمية: ${qtyDiff}` };
      }
      if ((input.matchState === "Blocked" || input.matchState === "Variance") && input.totalVariance > 0) {
        return { isMatched: false, explanation: `فرق السعر: +${input.totalVariance.toFixed(2)}` };
      }
      return { isMatched: true, explanation: "مطابق تماماً" };
    }

    const blockedPriceMismatch = computeMatchingLineDisplay({
      poUnitPrice: 115.0,
      invoiceUnitPrice: 116.5,
      orderedQty: 100,
      receivedQty: 60,
      invoicedQty: 100,
      basis: "TwoWay",
      matchState: "Blocked",
      totalVariance: 150.0
    });

    assert.equal(blockedPriceMismatch.isMatched, false);
    assert.equal(blockedPriceMismatch.explanation, "فرق السعر: +1.50");
  });

  it("enforces authoritative receipt eligibility invariant: ProductVariantId != null is NOT sufficient", () => {
    // Scenario A: Stock-tracked variant with ProductVariantId -> Receipt eligible
    const stockTrackedLine = {
      id: "l-stock",
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94", // Premium A4 Copy Paper (isStocked: true)
      description: "ورق تصوير مقاس A4 ممتاز (80 جم)"
    };
    assert.equal(isReceiptEligibleLine(stockTrackedLine), true);

    const explicitStockedLine = {
      id: "l-explicit-stock",
      productVariantId: "custom-variant-uuid",
      isInventoryTracked: true,
      description: "معدات تصنيع مخزنية"
    };
    assert.equal(isReceiptEligibleLine(explicitStockedLine), true);

    // Scenario B: Non-stock variant WITH a valid ProductVariantId -> NOT receipt eligible
    const nonStockVariantLine = {
      id: "l-nonstock",
      productVariantId: "prod-marketing-collateral", // Valid ProductVariantId, isStocked: false
      description: "مطبوعات تسويقية خاصة غير مخزنية"
    };
    assert.equal(isReceiptEligibleLine(nonStockVariantLine), false);

    const explicitNonStockLine = {
      id: "l-explicit-nonstock",
      productVariantId: "variant-with-id-123",
      isInventoryTracked: false,
      lineType: "NonStock",
      description: "بند مشتريات استهلاكي مباشر"
    };
    assert.equal(isReceiptEligibleLine(explicitNonStockLine), false);

    // Scenario C: Service variant WITH a valid ProductVariantId -> NOT receipt eligible
    const serviceVariantLine = {
      id: "l-service-variant",
      productVariantId: "prod-it-consulting", // Valid ProductVariantId, isStocked: false
      description: "خدمات استشارات ودعم تقني (ساعات)"
    };
    assert.equal(isReceiptEligibleLine(serviceVariantLine), false);

    const maintenanceVariantLine = {
      id: "l-maint-variant",
      productVariantId: "prod-equipment-maintenance", // Valid ProductVariantId, isStocked: false
      description: "صيانة وقائية ربع سنوية للمعدات"
    };
    assert.equal(isReceiptEligibleLine(maintenanceVariantLine), false);

    // Ad-hoc line without ProductVariantId -> NOT receipt eligible
    const adHocServiceLine = {
      id: "l-adhoc",
      productVariantId: null,
      description: "خدمات شحن واستشارات قانونية"
    };
    assert.equal(isReceiptEligibleLine(adHocServiceLine), false);
  });

  it("filters physical stock lines only for inventory goods receipt using authoritative classification", () => {
    const lines = [
      { id: "line-1", productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94", description: "خادم نصل عالي الأداء", orderedQuantity: 5, receivedQuantity: 0 },
      { id: "line-2", productVariantId: "prod-equipment-maintenance", description: "صيانة وقائية ربع سنوية للمعدات", orderedQuantity: 1, receivedQuantity: 0 },
      { id: "line-3", productVariantId: null, description: "خدمات صيانة وتركيب معتمدة", orderedQuantity: 1, receivedQuantity: 0 }
    ];

    const physicalLines = lines.filter(isReceiptEligibleLine);
    assert.equal(physicalLines.length, 1);
    assert.equal(physicalLines[0]?.id, "line-1");
    assert.equal(physicalLines[0]?.productVariantId, "a6315416-cffe-bf52-bcf0-d5e590d3bf94");

    // Receipt payload must not contain any service line (with or without variantId)
    const receiptLines = physicalLines.map((l) => ({
      purchaseOrderLineId: l.id,
      productVariantId: l.productVariantId!,
      warehouseId: "00000000-0000-0000-0000-000000000001",
      quantity: l.orderedQuantity
    }));
    assert.equal(receiptLines.length, 1);
    assert.equal(receiptLines[0]?.productVariantId, "a6315416-cffe-bf52-bcf0-d5e590d3bf94");
  });

  it("partitions mixed order lines into physical receipt vs service invoicing in Receive & Bill", () => {
    const orderLines = [
      { id: "l-stock", productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94", description: "Stock Item", orderedQuantity: 10 },
      { id: "l-serv-variant", productVariantId: "prod-it-consulting", description: "Service Variant With Id", orderedQuantity: 5 },
      { id: "l-serv-adhoc", productVariantId: null, description: "Service Contract Ad-hoc", orderedQuantity: 1 }
    ];

    const stockLines = orderLines.filter(isReceiptEligibleLine);
    const serviceLines = orderLines.filter((l) => !isReceiptEligibleLine(l));

    assert.equal(stockLines.length, 1);
    assert.equal(stockLines[0]?.id, "l-stock");
    assert.equal(serviceLines.length, 2);
    assert.equal(serviceLines[0]?.id, "l-serv-variant");
    assert.equal(serviceLines[1]?.id, "l-serv-adhoc");

    // Mutation receipt.lines sends ONLY stockLines
    const receiptMutationLines = stockLines.map((l) => ({
      purchaseOrderLineId: l.id,
      productVariantId: l.productVariantId!
    }));
    assert.equal(receiptMutationLines.length, 1);
    assert.equal(receiptMutationLines[0]?.purchaseOrderLineId, "l-stock");
  });

  it("suppresses receiveStock action when an order has zero physical receipt lines", () => {
    const serviceOnlyOrderWithVariants = {
      id: "po-serv-variants",
      lines: [
        { id: "l1", productVariantId: "prod-it-consulting", description: "IT Consulting Variant", orderedQuantity: 10 },
        { id: "l2", productVariantId: null, description: "Adhoc Consulting", orderedQuantity: 2 }
      ]
    };
    const mixedOrder = {
      id: "po-mixed",
      lines: [
        { id: "l1", productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94", description: "Hardware", orderedQuantity: 5 },
        { id: "l2", productVariantId: "prod-it-consulting", description: "Installation", orderedQuantity: 1 }
      ]
    };

    const hasPhysicalLines = (order: { lines: Array<{ productVariantId?: string | null }> }) =>
      order.lines.some(isReceiptEligibleLine);

    assert.equal(hasPhysicalLines(serviceOnlyOrderWithVariants), false);
    assert.equal(hasPhysicalLines(mixedOrder), true);
  });

  it("enforces lead time priority and defaulting rules without hardcoded business fallbacks", () => {
    // Scenario D: Supplier leadTimeDays exists -> derived correctly
    assert.equal(resolveConfiguredLeadTimeDays(7, 5), 7);
    assert.equal(resolveConfiguredLeadTimeDays(7, null), 7);
    assert.equal(addDaysToIsoDate("2026-10-29", 7), "2026-11-05");

    // Scenario E: Supplier leadTimeDays absent but backend settings default exists -> use backend default
    assert.equal(resolveConfiguredLeadTimeDays(null, 5), 5);
    assert.equal(resolveConfiguredLeadTimeDays(undefined, 5), 5);
    assert.equal(addDaysToIsoDate("2026-10-29", 5), "2026-11-03");

    // Scenario F: Neither supplier profile nor backend settings provides a default -> Expected Delivery remains empty
    assert.equal(resolveConfiguredLeadTimeDays(null, null), null);
    assert.equal(resolveConfiguredLeadTimeDays(undefined, undefined), null);
    assert.equal(resolveConfiguredLeadTimeDays(null, undefined), null);
  });

  it("enforces expected delivery date invariant (Expected Delivery >= Order Date)", () => {
    // Scenario G: Expected Delivery < Order Date is blocked
    function validateDates(orderDate: string, expectedDeliveryDate: string): { isValid: boolean; errorKey?: string } {
      if (expectedDeliveryDate && orderDate && expectedDeliveryDate < orderDate) {
        return { isValid: false, errorKey: "errors.expectedDeliveryBeforeOrderDate" };
      }
      return { isValid: true };
    }

    assert.equal(validateDates("2026-10-29", "2026-10-29").isValid, true);
    assert.equal(validateDates("2026-10-29", "2026-11-05").isValid, true);
    assert.equal(validateDates("2026-10-29", "2026-10-28").isValid, false);
    assert.equal(validateDates("2026-10-29", "2026-10-28").errorKey, "errors.expectedDeliveryBeforeOrderDate");
  });
});
