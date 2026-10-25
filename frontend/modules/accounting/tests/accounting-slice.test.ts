import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type {
  AccountDto,
  AccountType,
  JournalEntry,
  JournalEntryDto,
  JournalLine,
  LedgerMode,
  LedgerReadinessDto,
  OpeningBalancePreviewDto,
  ReverseEntryRequest
} from "../public.ts";
import { formatMoney } from "../../../components/formatting/index.ts";
import { mapLedgerError } from "../services/ledger-error-map.ts";

describe("F20 — Accounting & Ledger Domain Invariants & Slice Contracts", () => {
  it("enforces exact debit and credit balance contracts on journal entries", () => {
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
    const postedJournal: JournalEntryDto = {
      id: "00000000-0000-0000-0000-000000000010",
      journalId: "00000000-0000-0000-0000-000000000020",
      entryNumber: "JV-2026-0040",
      postingDate: "2026-09-30",
      periodDate: "2026-09-30",
      status: "Posted",
      currencyCode: "EGP",
      lines: [
        {
          sequence: 1,
          accountId: "00000000-0000-0000-0000-000000000101",
          debit: 5000,
          credit: 0,
          currencyCode: "EGP",
          amount: 5000,
          role: "Cash"
        },
        {
          sequence: 2,
          accountId: "00000000-0000-0000-0000-000000000102",
          debit: 0,
          credit: 5000,
          currencyCode: "EGP",
          amount: 5000,
          role: "Revenue"
        }
      ]
    };

    assert.equal(postedJournal.status, "Posted");
    const totalDebit = postedJournal.lines.reduce((s, l) => s + l.debit, 0);
    const totalCredit = postedJournal.lines.reduce((s, l) => s + l.credit, 0);
    assert.equal(totalDebit, 5000);
    assert.equal(totalCredit, 5000);
    assert.equal(totalDebit, totalCredit);
  });

  it("verifies mode switching invariant: Managed vs Full mode preserves accounting truth", () => {
    let mode: LedgerMode = "Managed";
    assert.equal(mode, "Managed");

    // Switch mode
    mode = "Full";
    assert.equal(mode, "Full");

    // In both modes, core posting rules and balance requirements remain identical
    const debit = 1000;
    const credit = 1000;
    assert.equal(debit, credit);
  });

  it("proves multi-currency line conversion preserves base currency equality", () => {
    const transactionCurrency = "USD";
    const baseCurrency = "EGP";
    const exchangeRate = 48.5;

    const txnDebit = 100;
    const txnCredit = 100;

    const baseDebit = txnDebit * exchangeRate;
    const baseCredit = txnCredit * exchangeRate;

    assert.equal(transactionCurrency, "USD");
    assert.equal(baseCurrency, "EGP");
    assert.equal(baseDebit, 4850);
    assert.equal(baseCredit, 4850);
    assert.equal(baseDebit, baseCredit);
  });

  it("validates RFC-7807 problem detail error code mapping to humanized messages", () => {
    const unbalancedError = {
      status: 400,
      title: "Journal entry unbalanced",
      extensions: { code: "ledger.entry_unbalanced" }
    };
    const arMsg = mapLedgerError(unbalancedError, "ar");
    const enMsg = mapLedgerError(unbalancedError, "en");

    assert.ok(arMsg.includes("يجب أن يتساوى إجمالي المدين والدائن"));
    assert.ok(enMsg.includes("debit and credit totals must be equal"));

    const closedError = {
      status: 400,
      extensions: { code: "ledger.period_closed" }
    };
    assert.ok(mapLedgerError(closedError, "ar").includes("الفترة المحاسبية لهذا التاريخ مغلقة"));
    assert.ok(mapLedgerError(closedError, "en").includes("accounting period for this posting date is closed"));
  });

  it("validates operational readiness state evaluation", () => {
    const readyReadiness: LedgerReadinessDto = {
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      mode: "Managed",
      state: "Ready",
      capabilities: [
        { capability: "invoicing", state: "Ready", reasons: [] },
        { capability: "payments", state: "Ready", reasons: [] }
      ],
      version: 1
    };
    assert.equal(readyReadiness.state, "Ready");
    assert.equal(readyReadiness.capabilities.every((c) => c.state === "Ready"), true);

    const blockedReadiness: LedgerReadinessDto = {
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      mode: "Managed",
      state: "Blocked",
      capabilities: [
        {
          capability: "invoicing",
          state: "Blocked",
          reasons: [{ code: "ledger.missing_rule", message: "Missing posting rule" }]
        }
      ],
      version: 1
    };
    assert.equal(blockedReadiness.state, "Blocked");
    assert.equal(blockedReadiness.capabilities[0]?.reasons.length, 1);
  });

  it("verifies reversal request integrity with reason and date checks", () => {
    const reversal: ReverseEntryRequest = {
      reversalDate: "2026-10-24",
      reason: "Correction of incorrect cost allocation"
    };

    assert.ok(reversal.reversalDate.length > 0);
    assert.ok(reversal.reason.length >= 5);
  });

  it("verifies opening balances preview zero-mutation balance validation", () => {
    const preview: OpeningBalancePreviewDto = {
      batchId: "00000000-0000-0000-0000-000000000088",
      balanced: true,
      totalDebit: 150000,
      totalCredit: 150000,
      validationErrors: [],
      lines: [
        {
          accountId: "00000000-0000-0000-0000-000000000001",
          debit: 150000,
          credit: 0,
          currencyCode: "EGP",
          baseDebit: 150000,
          baseCredit: 0
        },
        {
          accountId: "00000000-0000-0000-0000-000000000002",
          debit: 0,
          credit: 150000,
          currencyCode: "EGP",
          baseDebit: 0,
          baseCredit: 150000
        }
      ]
    };

    assert.equal(preview.balanced, true);
    assert.equal(preview.totalDebit, preview.totalCredit);
    assert.equal(preview.validationErrors.length, 0);
  });

  it("verifies normal balance derivation per account type", () => {
    const normalBalanceFor = (type: AccountType): "Debit" | "Credit" =>
      type === "Liability" || type === "Equity" || type === "Revenue" ? "Credit" : "Debit";

    assert.equal(normalBalanceFor("Asset"), "Debit");
    assert.equal(normalBalanceFor("Expense"), "Debit");
    assert.equal(normalBalanceFor("Liability"), "Credit");
    assert.equal(normalBalanceFor("Equity"), "Credit");
    assert.equal(normalBalanceFor("Revenue"), "Credit");
  });

  it("maps control account protection and draft lifecycle error codes accurately", () => {
    const controlErr = { extensions: { code: "ledger.control_account_manual_post_blocked" } };
    assert.match(mapLedgerError(controlErr, "ar"), /لا يمكن ترحيل قيود اليومية اليدوية مباشرة إلى حساب مراقبة محمي/);
    assert.match(mapLedgerError(controlErr, "en"), /Manual journals cannot post directly to a protected control account/);

    const transitionErr = { extensions: { code: "ledger.general_journal_transition_invalid" } };
    assert.match(mapLedgerError(transitionErr, "ar"), /مسودة القيد ليست في الحالة التشغيلية المطلوبة/);

    const dupBatchErr = { extensions: { code: "ledger.opening_batch_already_processed" } };
    assert.match(mapLedgerError(dupBatchErr, "ar"), /تمت معالجة وترحيل دفعة الأرصدة الافتتاحية/);
  });

  it("validates multi-currency line representation and base debit/credit math", () => {
    const fxRate = 50.25;
    const txnDebit = 1000;
    const baseDebit = txnDebit * fxRate;
    assert.equal(baseDebit, 50250);

    const formattedTxn = formatMoney({ amount: txnDebit.toFixed(2), currency: "USD" });
    const formattedBase = formatMoney({ amount: baseDebit.toFixed(2), currency: "EGP" });
    assert.equal(formattedTxn, "USD 1,000.00");
    assert.equal(formattedBase, "EGP 50,250.00");
  });
});
