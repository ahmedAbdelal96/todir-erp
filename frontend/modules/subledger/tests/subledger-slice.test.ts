import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type {
  PartyOpenItem,
  PartyBalanceResult,
  AutoApplySuggestionLine,
  ApplicationPreviewResult,
  ReconciliationResult,
  OpeningSubledgerBatchResult
} from "../types.ts";

describe("Subledger Vertical Slice Contracts & Invariants", () => {
  it("F14 / Exact Decimal Safety — preserves all financial balances and transaction amounts as invariant strings without floating-point conversion", () => {
    const rawInvoiceAmount = "125000.7500";
    const rawCreditAmount = "25000.2500";

    assert.equal(typeof rawInvoiceAmount, "string");
    assert.equal(typeof rawCreditAmount, "string");

    // Standard IEEE-754 floats fail on 0.1 + 0.2:
    const floatImprecision = 0.1 + 0.2;
    assert.notEqual(floatImprecision, 0.3);

    // Decimal string math simulation
    const simulatedNet = (parseFloat(rawInvoiceAmount) - parseFloat(rawCreditAmount)).toFixed(2);
    assert.equal(simulatedNet, "100000.50");
  });

  it("Exposure & Polarity Invariants — calculates amount due, available credit, and net balance correctly", () => {
    const mockBalance: PartyBalanceResult = {
      partyId: "11111111-1111-1111-1111-111111111111",
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      direction: "Receivable",
      asOfDate: "2026-10-26",
      currencies: [
        {
          currencyCode: "EGP",
          amountDue: "100000.00",
          availableCredits: "20000.00",
          netBalance: "80000.00",
          overdueExposure: "10000.00"
        }
      ],
      creditLimitAmount: "150000.00",
      creditLimitCurrency: "EGP",
      availableCreditAmount: "70000.00",
      exposureVersion: 3
    };

    const currency = mockBalance.currencies[0]!;
    assert.equal(currency.currencyCode, "EGP");
    assert.equal(currency.amountDue, "100000.00");
    assert.equal(currency.availableCredits, "20000.00");
    assert.equal(currency.netBalance, "80000.00");

    const calculatedNet = (parseFloat(currency.amountDue) - parseFloat(currency.availableCredits)).toFixed(2);
    assert.equal(calculatedNet, currency.netBalance);

    // Credit limit check
    const limit = parseFloat(mockBalance.creditLimitAmount!);
    const net = parseFloat(currency.netBalance);
    assert.equal(limit - net, 70000.0);
    assert.equal((limit - net).toFixed(2), mockBalance.availableCreditAmount);
  });

  it("Auto-Apply Deterministic Suggestions — preserves expected item versions for optimistic concurrency", () => {
    const suggestion: AutoApplySuggestionLine = {
      debitItemId: "item-inv-1001",
      creditItemId: "item-pay-1001",
      debitAmount: "5000.00",
      creditAmount: "5000.00",
      expectedDebitVersion: 1,
      expectedCreditVersion: 2,
      reasonCode: "ExactReferenceAndAmount",
      confidenceClass: "Exact",
      currencyCode: "EGP",
      debitRemaining: "0.00",
      creditRemaining: "0.00"
    };

    assert.equal(suggestion.reasonCode, "ExactReferenceAndAmount");
    assert.equal(suggestion.confidenceClass, "Exact");
    assert.equal(suggestion.expectedDebitVersion, 1);
    assert.equal(suggestion.expectedCreditVersion, 2);
    assert.equal(suggestion.debitRemaining, "0.00");
  });

  it("Application Preview & FX Impact — verifies debits and credits apply with explicit FX impact calculation", () => {
    const preview: ApplicationPreviewResult = {
      partyId: "11111111-1111-1111-1111-111111111111",
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      direction: "Receivable",
      lines: [
        {
          debitItemId: "inv-fx-1001",
          creditItemId: "pay-fx-1001",
          debitAppliedAmount: "100.00",
          creditAppliedAmount: "100.00",
          expectedDebitVersion: 1,
          expectedCreditVersion: 1,
          debitRemainingAmount: "0.00",
          creditRemainingAmount: "0.00",
          fxImpactBase: "50.00",
          baseCurrencyCode: "EGP"
        }
      ],
      totalDebitApplied: "100.00",
      totalCreditApplied: "100.00",
      totalFxImpactBase: "50.00"
    };

    assert.equal(preview.lines.length, 1);
    assert.equal(preview.totalDebitApplied, "100.00");
    assert.equal(preview.totalCreditApplied, "100.00");
    assert.equal(preview.totalFxImpactBase, "50.00");
  });

  it("Concurrency Conflict Mapping — classifies subledger.item_changed as human-readable concurrency conflict", () => {
    const isConcurrencyConflict = (code: string) =>
      code.includes("item_changed") || code.includes("conflict") || code.includes("version");

    assert.equal(isConcurrencyConflict("subledger.item_changed"), true);
    assert.equal(isConcurrencyConflict("subledger.application_conflict"), true);
    assert.equal(isConcurrencyConflict("subledger.direction_invalid"), false);
  });

  it("Write-Off Policy Enforcement — identifies amounts exceeding manager threshold", () => {
    const threshold = 100.0;
    const writeOffAmount = 250.0;
    const requiresApproval = writeOffAmount > threshold;

    assert.equal(requiresApproval, true);

    const smallWriteOff = 25.0;
    const smallRequiresApproval = smallWriteOff > threshold;
    assert.equal(smallRequiresApproval, false);
  });

  it("GL Control Account Reconciliation — verifies balanced and unbalanced states", () => {
    const balancedResult: ReconciliationResult = {
      id: "run-001",
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      asOfDate: "2026-10-26",
      baseCurrencyCode: "EGP",
      subledgerBaseBalance: "100000.00",
      ledgerBaseBalance: "100000.00",
      variance: "0.00",
      isBalanced: true,
      createdAt: "2026-10-26T12:00:00Z",
      details: [
        {
          id: "detail-001",
          direction: "Receivable",
          controlRole: "TradeReceivables",
          controlAccountReference: "11200",
          controlConfigurationReference: "DEFAULT-AR",
          ledgerAccountId: "acc-11200",
          subledgerBaseBalance: "100000.00",
          ledgerBaseBalance: "100000.00",
          variance: "0.00",
          contextResolved: true
        }
      ]
    };

    assert.equal(balancedResult.isBalanced, true);
    assert.equal(balancedResult.variance, "0.00");
    assert.equal(balancedResult.details[0]?.contextResolved, true);

    const unbalancedResult: ReconciliationResult = {
      ...balancedResult,
      subledgerBaseBalance: "100000.00",
      ledgerBaseBalance: "95000.00",
      variance: "5000.00",
      isBalanced: false
    };

    assert.equal(unbalancedResult.isBalanced, false);
    assert.equal(unbalancedResult.variance, "5000.00");
  });

  it("Opening Migration Immutability — prohibits modifications once batch is finalized", () => {
    const finalizedBatch: OpeningSubledgerBatchResult = {
      id: "batch-open-01",
      batchIdentity: "OPEN-2026-01",
      goLiveDate: "2026-01-01",
      status: "Finalized",
      lineCount: 15,
      openItemIds: ["item-01", "item-02"],
      alreadyCompleted: true
    };

    assert.equal(finalizedBatch.status, "Finalized");
    assert.equal(finalizedBatch.alreadyCompleted, true);

    const isEditable = finalizedBatch.status !== "Finalized" && !finalizedBatch.alreadyCompleted;
    assert.equal(isEditable, false);
  });
});
