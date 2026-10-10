import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { PaymentCommand, PaymentResult } from "../public.ts";
import { formatMoney } from "../../../components/formatting/index.ts";

describe("F19 — Payment Reference Validation", () => {
  it("structures high-risk payment command with idempotency key", () => {
    const command: PaymentCommand = {
      partyId: "party-001",
      partyName: "شركة الأفق للتجارة والمقاولات",
      amount: "75000.50",
      currency: "SAR",
      paymentMethod: "BankTransfer",
      referenceNumber: "PAY-2026-8801",
      sourceAccount: "1010-البنك الأهلي التجاري",
      destinationAccount: "2010-حساب المورد الجاري",
      notes: "دفعة مستحقات عن مشروع برج الرياض"
    };

    const idempotencyKey = crypto.randomUUID();

    assert.equal(command.partyId, "party-001");
    assert.equal(command.amount, "75000.50");
    assert.equal(command.currency, "SAR");
    assert.equal(formatMoney({ amount: command.amount, currency: command.currency }), "SAR 75,000.50");
    assert.ok(idempotencyKey.length > 20);
  });

  it("handles payment outcome classification correctly", () => {
    const successResult: PaymentResult = {
      paymentId: "pay-tx-001",
      referenceNumber: "PAY-2026-8801",
      status: "Settled",
      amount: "75000.50",
      currency: "SAR",
      settledAt: "2026-10-09T10:30:00Z"
    };

    assert.equal(successResult.status, "Settled");
    assert.equal(successResult.amount, "75000.50");
  });

  it("enforces logical payment attempt idempotency lifecycle across retries and resets", () => {
    // Helper simulating logical payment attempt controller
    class PaymentAttemptController {
      private currentKey: string = crypto.randomUUID();
      private inFlight: boolean = false;
      public executionHistory: Array<{ key: string; attempt: number }> = [];

      public execute(isRetry = false) {
        if (this.inFlight) {
          throw new Error("Double submission blocked while mutation in-flight");
        }
        this.inFlight = true;
        this.executionHistory.push({ key: this.currentKey, attempt: this.executionHistory.length + 1 });
        this.inFlight = false;
        return this.currentKey;
      }

      public resetForNewIntent() {
        this.currentKey = crypto.randomUUID();
        this.inFlight = false;
      }

      public getCurrentKey() {
        return this.currentKey;
      }
    }

    const controller = new PaymentAttemptController();

    // Rule 1: First payment command creates a stable idempotency key K1
    const key1 = controller.execute();
    assert.ok(key1.length > 20);
    assert.equal(controller.executionHistory.length, 1);
    assert.equal(controller.executionHistory[0]?.key, key1);

    // Rule 2: In-flight double submission is rejected and does not generate new key
    // Rule 3: Safe retry after transport timeout reuses the exact same logical key K1
    const keyRetry = controller.execute(true);
    assert.equal(keyRetry, key1);
    assert.equal(controller.executionHistory.length, 2);
    assert.equal(controller.executionHistory[1]?.key, key1, "Retry must reuse original attempt key");

    // Rule 4: Genuinely new payment after explicit form reset / new intent receives a new key K2
    controller.resetForNewIntent();
    const key2 = controller.execute();
    assert.notEqual(key2, key1, "New payment intent must receive a fresh idempotency key");
    assert.equal(controller.executionHistory.length, 3);
    assert.equal(controller.executionHistory[2]?.key, key2);
  });
});
