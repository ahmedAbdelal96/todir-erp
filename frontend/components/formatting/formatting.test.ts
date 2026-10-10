import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  normalizeDecimalInput,
  isValidDecimal,
  parseDecimalInput,
  formatDecimalNumber
} from "./decimal-parser.ts";
import { formatMoney } from "./money-formatter.ts";
import { formatQuantity, formatPercentage } from "./quantity-formatter.ts";
import { formatDate, formatDateTime } from "./date-formatter.ts";

describe("Formatting & Domain Value Contracts", () => {
  it("normalizes Eastern Arabic and Persian digits to Western digits", () => {
    assert.equal(normalizeDecimalInput("١٢٣٤٫٥٦"), "1234.56");
    assert.equal(normalizeDecimalInput("۱۲۳۴.۵۶"), "1234.56");
    assert.equal(normalizeDecimalInput("1,234.56"), "1234.56");
  });

  it("handles all required precision edge cases (0.1, 0.2, 0.3, 0.000001, large numbers)", () => {
    assert.equal(parseDecimalInput("0.1"), "0.1");
    assert.equal(parseDecimalInput("0.2"), "0.2");
    assert.equal(parseDecimalInput("0.3"), "0.3");
    assert.equal(parseDecimalInput("0.000001"), "0.000001");
    assert.equal(parseDecimalInput("999999999999.99"), "999999999999.99");
    assert.equal(parseDecimalInput("-1250.75"), "-1250.75");
    assert.equal(parseDecimalInput("0"), "0");
    assert.equal(parseDecimalInput("0.00"), "0.00");
  });

  it("normalizes Arabic decimal and thousands grouping separators", () => {
    assert.equal(normalizeDecimalInput("١٬٢٣٤٫٥٦"), "1234.56");
    assert.equal(normalizeDecimalInput("1٬000٫00"), "1000.00");
    assert.equal(normalizeDecimalInput("1.234,56"), "1234.56"); // European
    assert.equal(normalizeDecimalInput("1,234.56"), "1234.56"); // Standard
  });

  it("strictly rejects malformed and ambiguous numeric inputs", () => {
    assert.equal(isValidDecimal(""), false);
    assert.equal(isValidDecimal("   "), false);
    assert.equal(isValidDecimal("abc"), false);
    assert.equal(isValidDecimal("12.34.56"), false);
    assert.equal(isValidDecimal("123..45"), false);
    assert.equal(isValidDecimal("--50"), false);
    assert.equal(isValidDecimal("12-34"), false);
    assert.equal(isValidDecimal("12.34a"), false);
    assert.equal(parseDecimalInput(""), null);
    assert.equal(parseDecimalInput("malformed"), null);
  });

  it("formats large numbers without precision truncation or floating point artifacts", () => {
    const formatted = formatDecimalNumber("999999999999.99", {
      locale: "en-US",
      minFractionDigits: 2,
      maxFractionDigits: 2
    });
    assert.equal(formatted, "999,999,999,999.99");
  });

  it("formats money values with exact precision rules and negative amounts", () => {
    const egp = formatMoney(
      { amount: "1500.50", currency: "EGP" },
      { locale: "en-US", displayCurrencyCode: true }
    );
    assert.equal(egp, "EGP 1,500.50");

    const negativeMoney = formatMoney(
      { amount: "-500.25", currency: "USD" },
      { locale: "en-US", displayCurrencyCode: true }
    );
    assert.equal(negativeMoney, "USD -500.25");

    const kwd = formatMoney(
      { amount: "25.125", currency: "KWD" },
      { locale: "en-US", displayCurrencyCode: true }
    );
    assert.equal(kwd, "KWD 25.125");
  });

  it("formats quantity and percentage values with exact strings", () => {
    const qty = formatQuantity(
      { value: "45.50", unitOfMeasure: "KG" },
      { locale: "en-US" }
    );
    assert.equal(qty, "45.50 KG");

    const microQty = formatQuantity(
      { value: "0.0005", unitOfMeasure: "MG" },
      { locale: "en-US", decimals: 4 }
    );
    assert.equal(microQty, "0.0005 MG");

    const vat = formatPercentage("14.00", { locale: "en-US" });
    assert.equal(vat, "14.00%");
  });

  it("formats local calendar dates and datetimes safely", () => {
    const formattedDate = formatDate("2026-05-15", { locale: "en-US" });
    assert.ok(formattedDate.includes("2026"));
    assert.ok(formattedDate.includes("15") || formattedDate.includes("May"));

    const formattedTime = formatDateTime("2026-05-15T14:30:00Z", { locale: "en-US" });
    assert.ok(formattedTime.includes("2026"));
  });
});
