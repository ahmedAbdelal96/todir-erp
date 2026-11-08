import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as posAr } from "../i18n/ar.ts";
import { en as posEn } from "../i18n/en.ts";
import {
  toScaledBigInt,
  fromScaledBigInt,
  calculateExactChange,
  calculateExactSplitRemaining,
} from "../utils/exact-money-math.ts";

describe("POS Module 20 Slice Contracts & Domain Invariants", () => {
  test("Exact Money Decimal Arithmetic eliminates IEEE 754 float drift", () => {
    // 0.1 + 0.2 in standard JS float is 0.30000000000000004
    const a = toScaledBigInt("0.1");
    const b = toScaledBigInt("0.2");
    const sum = a + b;
    assert.equal(fromScaledBigInt(sum, 2), "0.30");

    // Exact cash change
    const change1 = calculateExactChange("100.00", "99.95");
    assert.equal(change1.isSufficient, true);
    assert.equal(change1.changeAmount, 0.05);
    assert.equal(change1.changeStr, "0.05");

    // Exact fractional cash change with Eastern Arabic digits
    const change2 = calculateExactChange("٢٠٠.٥٠", "150.25");
    assert.equal(change2.isSufficient, true);
    assert.equal(change2.changeAmount, 50.25);
    assert.equal(change2.changeStr, "50.25");

    // Underpayment detection
    const under = calculateExactChange("99.99", "100.00");
    assert.equal(under.isSufficient, false);
    assert.equal(under.changeAmount, 0);

    // Exact split remaining with three fractional payments
    const split = calculateExactSplitRemaining("100.00", [
      { amount: "33.33" },
      { amount: "33.33" },
      { amount: "33.34" },
    ]);
    assert.equal(split.isComplete, true);
    assert.equal(split.remainingAmount, 0);
    assert.equal(split.paidTotalStr, "100.00");

    // Incomplete split
    const splitIncomplete = calculateExactSplitRemaining("100.00", [
      { amount: "50.00" },
      { amount: "49.99" },
    ]);
    assert.equal(splitIncomplete.isComplete, false);
    assert.equal(splitIncomplete.remainingAmount, 0.01);
    assert.equal(splitIncomplete.remainingStr, "0.01");
  });
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
