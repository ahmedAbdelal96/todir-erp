# TODIR Financial & Accounting Domain Pack

Canonical specifications:
- `docs/modules/16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- Modules: `accounting`, `ledger`, `invoicing`, `subledger`, `payments`, `tax`

---

## Core Entities & Workflows

1. **General Ledger & Journal Entries (`قيد يومية`)**:
   - Double-entry balance invariant: `Total Debits == Total Credits`.
   - Balanced journals can be submitted for approval and posted.
   - Once posted (`POSTED`), a journal is immutable. The only way to correct it is via **Reversal (`عكس القيد`)** with a mandatory reason.
2. **Customer & Supplier Invoices (`فاتورة عميل / فاتورة مورد`)**:
   - Creation flow: Centered dialog for quick invoice, or dedicated route for multi-line invoice.
   - States: `DRAFT -> FINALIZED -> PAID / PARTIALLY_PAID / CANCELLED`.
   - Finalized invoices generate automated General Ledger and Subledger postings.
   - Cancellations after finalization strictly require Credit Notes (`إشعار دائن`) or Debit Notes (`إشعار مدين`).
3. **FX Revaluation (`إعادة تقييم فروق العملة`)**:
   - Three-step workflow: **Preview -> Post -> Reverse**.
   - Foreign currency monetary accounts revalued against legal entity base currency.
4. **Bank & Cashbox Reconciliation (`مطابقة الحسابات البنكية والخزينة`)**:
   - Compares external bank statement transactions against subledger payment entries.
   - Balanced reconciliation confirms matched items and flags un-reconciled variances.
