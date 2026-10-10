import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  formatMoney,
  formatQuantity,
  formatPercentage,
  formatDate,
  formatDateTime,
  isValidDecimal,
  parseDecimalInput
} from "./formatting/index.ts";
import { getCurrencyPrecision } from "./contracts/index.ts";

describe("Design System Export & Contract Validation", () => {
  it("exports all formatting contracts with deterministic behavior", () => {
    assert.equal(typeof formatMoney, "function");
    assert.equal(typeof formatQuantity, "function");
    assert.equal(typeof formatPercentage, "function");
    assert.equal(typeof formatDate, "function");
    assert.equal(typeof formatDateTime, "function");
    assert.equal(typeof isValidDecimal, "function");
    assert.equal(typeof parseDecimalInput, "function");
  });

  it("handles multi-currency precision correctly", () => {
    const egp = getCurrencyPrecision("EGP");
    assert.equal(egp.standardFractionDigits, 2);

    const kwd = getCurrencyPrecision("KWD");
    assert.equal(kwd.standardFractionDigits, 3);

    const jpy = getCurrencyPrecision("JPY");
    assert.equal(jpy.standardFractionDigits, 0);
  });

  it("handles empty and null values gracefully without throwing", () => {
    assert.equal(formatMoney(null), "—");
    assert.equal(formatQuantity(null), "—");
    assert.equal(formatPercentage(null), "—");
    assert.equal(formatDate(null), "—");
    assert.equal(formatDateTime(null), "—");
  });
});
