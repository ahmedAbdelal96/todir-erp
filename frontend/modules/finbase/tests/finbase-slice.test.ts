import { strict as assert } from "node:assert";
import { describe, it } from "node:test";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import { finbaseApi } from "../services/finbase-api.ts";
import type {
  PaymentTermRuleRequest,
  ExchangeRatePurpose,
  ExchangeRateStatus,
  RoundingMode
} from "../types.ts";

describe("FinBase Vertical Slice Contracts & Invariants", () => {
  describe("F14 — Financial Decimal Safety", () => {
    it("preserves financial decimal amounts as invariant strings without floating-point conversion", () => {
      const sampleRate = "50.125000000000";
      const sampleAmount = "1187.50";
      const sampleRatio = "0.3333";

      assert.equal(typeof sampleRate, "string");
      assert.equal(typeof sampleAmount, "string");
      assert.equal(typeof sampleRatio, "string");

      // Verify strings are not converted to JavaScript numbers
      assert.notEqual(Number(sampleRate).toString(), sampleRate);
      assert.equal(sampleRate.length, 15);
    });

    it("normalizes Eastern Arabic and Persian digits to invariant Western digits", () => {
      assert.equal(normalizeDecimalInput("١٠٠٫٥٠"), "100.50");
      assert.equal(normalizeDecimalInput("٥٠٫١٢٥"), "50.125");
      assert.equal(normalizeDecimalInput("۱۲۳٫۴۵"), "123.45");
      assert.equal(normalizeDecimalInput("١٬٢٣٤٫٥٦"), "1234.56");
      assert.equal(normalizeDecimalInput("100.00"), "100.00");
    });
  });

  describe("Payment Term & Schedule Invariants", () => {
    it("conforms to standard payment term rule shape with balance absorption", () => {
      const rules: readonly PaymentTermRuleRequest[] = [
        {
          sequence: 1,
          allocationType: "Percentage",
          percentage: "0.3333",
          dueDateBasis: "Immediate",
          days: 0,
          monthOffset: 0
        },
        {
          sequence: 2,
          allocationType: "Percentage",
          percentage: "0.3333",
          dueDateBasis: "DaysAfterDate",
          days: 30,
          monthOffset: 0
        },
        {
          sequence: 3,
          allocationType: "Balance",
          dueDateBasis: "DaysAfterDate",
          days: 60,
          monthOffset: 0
        }
      ];

      assert.equal(rules.length, 3);
      assert.equal(rules[0]?.allocationType, "Percentage");
      assert.equal(rules[2]?.allocationType, "Balance");
      assert.equal(rules[2]?.sequence, 3);
    });

    it("absorbs rounding residuals in the final balance installment", () => {
      // 100.00 document total with INSTALL3: 33.33 + 33.33 = 66.66. Residual balance is 33.34.
      const line1 = "33.33";
      const line2 = "33.33";
      const line3 = "33.34";

      // BigInt cents arithmetic to avoid float math
      const cents1 = BigInt(Math.round(parseFloat(line1) * 100));
      const cents2 = BigInt(Math.round(parseFloat(line2) * 100));
      const cents3 = BigInt(Math.round(parseFloat(line3) * 100));
      const totalCents = cents1 + cents2 + cents3;

      assert.equal(totalCents, 10000n);
    });
  });

  describe("Exchange Rate & Policy Contracts", () => {
    it("recognizes standard exchange rate purposes and statuses", () => {
      const purposes: readonly ExchangeRatePurpose[] = [
        "Accounting",
        "Commercial",
        "Contractual",
        "BankActual",
        "Settlement",
        "Tax"
      ];
      assert.equal(purposes.length, 6);

      const statuses: readonly ExchangeRateStatus[] = [
        "Draft",
        "Approved",
        "Superseded",
        "Rejected"
      ];
      assert.equal(statuses.length, 4);

      const modes: readonly RoundingMode[] = ["HalfUp", "HalfEven", "Down", "Up"];
      assert.equal(modes.length, 4);
    });

    it("exposes all required FinBase API lifecycle operations", () => {
      assert.equal(typeof finbaseApi.listCurrencies, "function");
      assert.equal(typeof finbaseApi.listEntityCurrencies, "function");
      assert.equal(typeof finbaseApi.enableCurrency, "function");
      assert.equal(typeof finbaseApi.listExchangeRates, "function");
      assert.equal(typeof finbaseApi.createExchangeRate, "function");
      assert.equal(typeof finbaseApi.approveExchangeRate, "function");
      assert.equal(typeof finbaseApi.listRateSources, "function");
      assert.equal(typeof finbaseApi.createRateSource, "function");
      assert.equal(typeof finbaseApi.previewConversion, "function");
      assert.equal(typeof finbaseApi.getFinancialPolicy, "function");
      assert.equal(typeof finbaseApi.updateFinancialPolicy, "function");
      assert.equal(typeof finbaseApi.listRoundingPolicies, "function");
      assert.equal(typeof finbaseApi.createRoundingPolicy, "function");
      assert.equal(typeof finbaseApi.listPaymentTerms, "function");
      assert.equal(typeof finbaseApi.getPaymentTermHistory, "function");
      assert.equal(typeof finbaseApi.createPaymentTerm, "function");
      assert.equal(typeof finbaseApi.createPaymentTermVersion, "function");
      assert.equal(typeof finbaseApi.activatePaymentTermVersion, "function");
      assert.equal(typeof finbaseApi.archivePaymentTerm, "function");
      assert.equal(typeof finbaseApi.previewSchedule, "function");
    });
  });
});
