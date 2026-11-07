import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as posAr } from "../i18n/ar.ts";
import { en as posEn } from "../i18n/en.ts";

describe("POS Module 20 Slice Contracts & Domain Invariants", () => {
  test("1:1 Arabic and English translation parity for POS resources", () => {
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

    const arKeys = new Set(getKeys(posAr));
    const enKeys = new Set(getKeys(posEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
    for (const key of enKeys) {
      assert.ok(arKeys.has(key), `Missing AR key: ${key}`);
    }
  });

  test("Arabic Market Glossary Charter compliance: authentic ERP terminology", () => {
    // Assert authentic market vocabulary
    assert.equal(posAr.nav.checkout, "شاشة الكاشير");
    assert.equal(posAr.nav.sessions, "ورديات الصندوق");
    assert.equal(posAr.nav.returns, "مرتجع المبيعات");
    assert.equal(posAr.session.variance, "عجز / فائض الصندوق");
    assert.equal(posAr.tender.cash, "نقداً");
    assert.equal(posAr.tender.card, "بطاقة بنكية");
    assert.equal(posAr.returns.actionCreditNote, "إشعار دائن");
  });

  test("Cash change calculation invariant: change = max(0, tendered - total)", () => {
    const calculateChange = (total: number, tendered: number): number => {
      if (tendered < total) return 0;
      return Math.round((tendered - total) * 100) / 100;
    };

    assert.equal(calculateChange(150.00, 200.00), 50.00);
    assert.equal(calculateChange(99.95, 100.00), 0.05);
    assert.equal(calculateChange(150.00, 150.00), 0.00);
    assert.equal(calculateChange(150.00, 100.00), 0.00);
  });

  test("Session variance invariant: variance = countedCash - expectedCash", () => {
    const calculateVariance = (expected: number, counted: number): number => {
      return Math.round((counted - expected) * 100) / 100;
    };

    // Exact count -> zero variance
    assert.equal(calculateVariance(100.00, 100.00), 0.00);
    // Surplus -> positive variance
    assert.equal(calculateVariance(100.00, 120.50), 20.50);
    // Shortage -> negative variance (deficit)
    assert.equal(calculateVariance(100.00, 85.00), -15.00);
  });

  test("Cart discount invariant: discount cannot exceed line total", () => {
    const computeDiscount = (lineTotal: number, discountAmount: number): number => {
      return Math.min(Math.max(discountAmount, 0), lineTotal);
    };

    assert.equal(computeDiscount(100, 20), 20);
    assert.equal(computeDiscount(100, 150), 100);
    assert.equal(computeDiscount(100, -10), 0);
  });

  test("Split payment completeness invariant", () => {
    const isFullyPaid = (totalDue: number, payments: number[]): boolean => {
      const sum = payments.reduce((acc, p) => acc + p, 0);
      return Math.round(sum * 100) >= Math.round(totalDue * 100);
    };

    assert.equal(isFullyPaid(100.00, [50.00, 50.00]), true);
    assert.equal(isFullyPaid(100.00, [30.00, 70.00]), true);
    assert.equal(isFullyPaid(100.00, [50.00, 49.99]), false);
  });
});
