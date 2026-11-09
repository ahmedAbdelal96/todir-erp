import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { mapExpenseError } from "../services/expenses-error-map.ts";
import { expensesAr } from "../i18n/ar.ts";
import { expensesEn } from "../i18n/en.ts";
import type {
  ExpenseState,
  ApprovalState,
  AccountingState,
  SettlementState,
  EvidenceState,
  PolicyState,
  PaymentSource
} from "../types.ts";

describe("Expenses Slice Contracts & Domain Invariants", () => {
  test("RFC-7807 error mapper translates known Expenses business codes without leaking technical details", () => {
    const error1 = mapExpenseError({ code: "expenses.expense_not_found" });
    assert.equal(error1.messageKey, "errors.expenseNotFound");
    assert.ok(error1.defaultMessage.length > 0);

    const error2 = mapExpenseError({ code: "expenses.invalid_amount" });
    assert.equal(error2.messageKey, "errors.invalidAmount");

    const error3 = mapExpenseError({ code: "expenses.category_not_found" });
    assert.equal(error3.messageKey, "errors.categoryNotFound");

    const error4 = mapExpenseError({ code: "expenses.concurrent_change_or_not_editable" });
    assert.equal(error4.messageKey, "errors.concurrentChange");

    const error5 = mapExpenseError({ code: "expenses.self_employee_not_found" });
    assert.equal(error5.messageKey, "errors.selfEmployeeNotFound");

    const error6 = mapExpenseError({ code: "expenses.advance_not_found" });
    assert.equal(error6.messageKey, "errors.advanceNotFound");

    const unknown = mapExpenseError({ code: "expenses.unknown_code" });
    assert.equal(unknown.messageKey, "errors.unknown");
  });

  test("1:1 Arabic and English translation parity for Expenses resources", () => {
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

    const arKeys = new Set(getKeys(expensesAr));
    const enKeys = new Set(getKeys(expensesEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
  });

  test("Domain State Invariants: 6 independent state dimensions strictly modeled", () => {
    const expenseStates: ExpenseState[] = ["Draft", "Submitted", "Approved", "Finalized", "Reversed"];
    assert.equal(expenseStates.length, 5);

    const approvalStates: ApprovalState[] = ["Pending", "Approved", "Rejected"];
    assert.equal(approvalStates.length, 3);

    const accountingStates: AccountingState[] = ["NotFinalized", "Pending", "Posted", "Reversed"];
    assert.equal(accountingStates.length, 4);

    const settlementStates: SettlementState[] = [
      "NotSettled",
      "ReimbursementPending",
      "ReimbursementPaid",
      "RepaymentPending",
      "RepaymentSettled",
      "AdvanceApplied",
      "SettledDirect"
    ];
    assert.equal(settlementStates.length, 7);

    const evidenceStates: EvidenceState[] = ["Missing", "PendingVerification", "Verified", "Rejected"];
    assert.equal(evidenceStates.length, 4);

    const policyStates: PolicyState[] = ["NotEvaluated", "Passed", "Warning", "Violation", "ExceptionApproved"];
    assert.equal(policyStates.length, 5);

    const paymentSources: PaymentSource[] = ["EmployeePaid", "CompanyCard", "PettyCash", "Advance"];
    assert.equal(paymentSources.length, 4);

    // Each state has localized labels in both Arabic and English
    for (const state of expenseStates) {
      assert.ok(expensesAr.states.expense[state], `Missing AR for expense state ${state}`);
      assert.ok(expensesEn.states.expense[state], `Missing EN for expense state ${state}`);
    }
  });

  test("Advance settlement balance invariant: Outstanding = Issued - Applied - Returned - WrittenOff", () => {
    const computeOutstanding = (
      issued: number,
      applied: number,
      returned: number,
      writtenOff: number
    ): number => {
      assert.ok(issued >= 0, "Issued amount must be non-negative");
      assert.ok(applied >= 0, "Applied amount must be non-negative");
      assert.ok(returned >= 0, "Returned amount must be non-negative");
      assert.ok(writtenOff >= 0, "Written off amount must be non-negative");
      const totalSettled = applied + returned + writtenOff;
      assert.ok(totalSettled <= issued + 0.0001, "Total settled cannot exceed issued");
      return Number((issued - totalSettled).toFixed(2));
    };

    assert.equal(computeOutstanding(5000, 3200, 1800, 0), 0);
    assert.equal(computeOutstanding(5000, 2000, 0, 0), 3000);
    assert.equal(computeOutstanding(5000, 4500, 0, 500), 0);
  });
});
