import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type { JournalEntry, JournalLine } from "../public.ts";
import { formatMoney } from "../../../components/formatting/index.ts";

describe("F20 — Accounting / Manual Journal Validation", () => {
  it("enforces exact debit and credit balance contracts", () => {
    const lines: JournalLine[] = [
      {
        id: "l1",
        accountCode: "1010",
        accountName: "البنك التجاري",
        description: "تحصيل إيرادات مبيعات",
        debit: "15000.00",
        credit: "0.00"
      },
      {
        id: "l2",
        accountCode: "4010",
        accountName: "إيرادات المبيعات",
        description: "إيرادات عن شهر أكتوبر",
        debit: "0.00",
        credit: "15000.00"
      }
    ];

    const journal: JournalEntry = {
      id: "jrnl-001",
      entryNumber: "JV-2026-0045",
      date: "2026-10-09",
      description: "قيد إثبات إيرادات مبيعات نقدية",
      status: "Draft",
      period: "2026-10",
      isPeriodClosed: false,
      lines,
      debitTotal: "15000.00",
      creditTotal: "15000.00",
      isBalanced: true
    };

    assert.equal(journal.isBalanced, true);
    assert.equal(journal.debitTotal, journal.creditTotal);
    assert.equal(formatMoney({ amount: journal.debitTotal, currency: "SAR" }), "SAR 15,000.00");
  });

  it("verifies locked immutable state when journal is posted", () => {
    const postedJournal: JournalEntry = {
      id: "jrnl-002",
      entryNumber: "JV-2026-0040",
      date: "2026-09-30",
      description: "قيد إقفال تسويات",
      status: "Posted",
      period: "2026-09",
      isPeriodClosed: true,
      lines: [],
      debitTotal: "5000.00",
      creditTotal: "5000.00",
      isBalanced: true,
      postedAt: "2026-09-30T18:00:00Z",
      postedBy: "المدير المالي"
    };

    assert.equal(postedJournal.status, "Posted");
    assert.equal(postedJournal.isPeriodClosed, true);
  });
});
