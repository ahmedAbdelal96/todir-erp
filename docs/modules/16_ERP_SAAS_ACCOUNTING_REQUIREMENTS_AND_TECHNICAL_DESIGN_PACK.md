# 16 — TODIR ERP SaaS Accounting / Finance Workspace / Financial Control Requirements & Technical Design Pack

**File:** `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + ACCOUNTANT EXPERIENCE + OWNER FINANCIAL VISIBILITY + ACCOUNTING CONTROL + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Senior Accountant/Controller, Finance Manager, Implementation Consultant, Backend/Frontend Leads, Data Architect, Performance Engineer, QA, Security, Coding Agent  
**Risk Class:** **CRITICAL / TRUST-DEFINING**

This is not an ordinary feature pack.

If Accounting is confusing, slow, internally inconsistent, difficult to reconcile, or produces unexplained differences, TODIR loses business trust even if every other module is good.

---

## Canonical dependencies

This pack **does not redesign** the financial kernels already approved.

It consumes and exposes them.

Primary sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR Accounting is the **accountant-facing and owner-facing finance application layer** over the existing financial truth.

Architecture:

```text
Sales / Purchasing / Invoicing / Payments / Inventory / Payroll / Assets / Expenses
                                ↓
                        Semantic Accounting Facts
                                ↓
                      Hidden Ledger + Subledger
                                ↓
                          TODIR Accounting
                                ↓
              Accountant / Finance Manager / Owner
```

Accounting does **not** become a second Ledger.

Accounting does **not** create a second AR/AP engine.

Accounting does **not** invent another financial truth.

It exposes, controls, reconciles, explains and reports the canonical truth already owned by Ledger/Subledger and other specialist modules.

---

# 1. Product Mission

TODIR Accounting must let a real accountant answer, quickly and confidently:

```text
Where is the company's money?
How much cash/bank do we have?
How much do customers owe us?
How much do we owe suppliers?
What is the inventory worth?
What did the company earn?
What did it spend?
What is the profit/loss?
Which numbers are not reconciled?
Where did this number come from?
Can I close this month safely?
```

---

# 2. Owner Mission

The owner does not buy an ERP to admire journals.

The owner wants financial truth.

The owner experience must answer:

```text
Cash & Bank
Receivables
Payables
Inventory Value
Revenue
Expenses
Profit/Loss
Overdue Customer Amount
Supplier Bills Due
Financial Problems Needing Attention
```

with clear date/scope/source context.

---

# 3. Accountant Is a Purchase-Decision Persona

TODIR must assume that many companies will ask their accountant:

> "جرّب النظام وقولنا ينفع ولا لأ."

Therefore Accountant Acceptance is a first-class commercial requirement.

Accounting must feel:

```text
Familiar
Fast
Traceable
Reconciled
Predictable
Not over-engineered
```

within the accountant's first evaluation session.

---

# 4. Trust Equation

TODIR Accounting succeeds when:

```text
Every Number
    ↓
Can Be Explained
    ↓
Can Be Traced
    ↓
Can Be Reconciled
    ↓
Cannot Be Silently Rewritten
```

---

# 5. Simple Outside, Strict Inside

Small-company default:

```text
Automatic postings
Simple Chart of Accounts
Simple Journal Entry
Trial Balance
P&L
Balance Sheet
Customer/Supplier balances
Month Close
```

Advanced controls only when enabled:

```text
Preparer/Approver/Poster separation
Advanced dimension policies
Close checklist
Approval workflows
Revaluation
Advanced reconciliations
Multiple accounting roles
Complex reporting mappings
```

---

# 6. No Workflow for Workflow's Sake

Default manual journal workflow:

```text
Draft
→ Post
```

not:

```text
Draft
→ Submit
→ Review
→ Approve
→ Authorize
→ Queue
→ Post
```

unless company policy requires it.

---

# 7. Exception-Driven Accounting

If accounting is correct:

```text
Do not create unnecessary work.
```

Accountants should primarily investigate:

```text
Unreconciled balances
Failed postings
Missing mappings
Period issues
Unallocated items
Unexpected variances
Manual high-risk adjustments
```

---

# 8. Accounting Owns the Experience, Not the Kernel Truth

Accounting owns or coordinates the application experience for:

```text
Accounting Home / Health
Owner Financial View
Chart of Accounts administration UX
Journal administration UX
Manual Journal Draft workflow
General Ledger exploration
Trial Balance
Financial Statements
Financial Statement Mapping/Presentation
Period/Close Workspace
Reconciliation Workbench
Opening Balance / Go-Live Workspace
FX Revaluation workspace
Accounting Configuration health
Account Mapping administration UX
Dimension administration UX
Financial drill-down
Finance reporting/export
Accountant setup/onboarding
```

---

# 9. Accounting Does NOT Own

```text
Journal Entry canonical storage      → Ledger
Posting Engine                       → Ledger
Chart of Accounts canonical model    → Ledger
Fiscal Period canonical model        → Ledger
Accounting Facts                     → Ledger Contracts
AR/AP Open Items                     → Subledger
Settlement/Allocation                → Subledger
Customer/Supplier balance truth      → Subledger
Invoice                              → Invoicing
Inventory valuation                  → Inventory
Payment movement                     → Payments
Exchange-rate source/resolution      → FinBase
Tax calculation                      → Tax
Bank statement reconciliation truth  → Banking/Reconciliation capability
```

---

# 10. Critical Anti-Duplication Rule

Forbidden:

```text
accounting_customer_balance
accounting_supplier_balance
accounting_stock_value
accounting_journal_entry_copy
accounting_invoice_balance
```

as independent sources of truth.

Accounting can have rebuildable read models/projections only.

---

# 11. Accountant Seat — Daily Work

Typical accountant day:

```text
Open Accounting Home
→ Review exceptions
→ Check cash/bank and due items
→ Review/post needed manual journals
→ Inspect customer/supplier aging
→ Reconcile control accounts
→ Investigate differences
→ Run reports
→ Close/review period when due
```

The system should optimize for this.

---

# 12. Accountant Home

Default finance workspace:

```text
Accounting Health

Ledger Balanced                 ✓
AR Reconciled                   ✓
AP Reconciled                   ✓
Inventory Reconciled            ✓
Failed Postings                 0
Unmapped Accounting Facts       0
Open Prior Periods              1
Items Needing Review            3
```

---

# 13. Owner Financial View

Recommended owner summary:

```text
Cash & Bank
Receivables
Payables
Inventory Value

Revenue MTD
Expenses MTD
Profit/Loss MTD

Overdue Receivables
Bills Due Soon

Financial Health
```

No debit/credit terminology unless user expands details.

---

# 14. Owner View Must Not Lie

Every owner card must display or carry:

```text
AsOfDate
LegalEntity / consolidated scope
Currency
Source status
Reconciliation health
```

where relevant.

---

# 15. Cash Is Not Profit

TODIR must never visually imply:

```text
Cash balance == Profit
```

Owner dashboard separates:

```text
Liquidity
Profitability
Working Capital
```

---

# 16. GL Cash vs Operational Bank Balance

Ledger account balance and bank/payment operational balance are related but not always identical until reconciliation.

Owner UI must not silently choose one and label it "Bank" without source context.

---

# 17. Receivables Truth

Owner/customer balance:

```text
Subledger AR Open Items
```

not a sum performed independently by Accounting.

---

# 18. Payables Truth

Supplier payable:

```text
Subledger AP Open Items
```

---

# 19. Inventory Value Truth

Inventory module valuation.

Accounting compares it to GL Inventory Control.

---

# 20. Revenue/Expense/Profit Truth

Ledger posted base amounts mapped/classified into financial statement definitions.

---

# 21. Financial Health

Accounting Health is not decorative.

It is a control surface for:

```text
reconciliation
posting configuration
period state
failed financial operations
migration/opening integrity
```

---

# 22. Health Severity

Recommended:

```text
Healthy
Attention
Blocking
Critical
```

---

# 23. Blocking Health Example

```text
AR Subledger != GL AR Control
```

is not a cosmetic warning.

It blocks period close by default unless explicit high-risk exception policy exists.

---

# 24. No Hidden Differences

Never hide reconciliation differences through:

```text
rounding away
silent write-off
automatic suspense
"ignore difference" without audit
```

---

# 25. Ledger Core Invariants Reused

Accounting UI must respect Ledger invariants:

```text
Every posted Journal Entry is balanced.
Posted history is immutable.
A source fact cannot post twice for the same fact version.
Account determination is centralized/versioned.
Corrections use reversal/adjustment.
Posting preserves source/accounting context.
Cross-tenant/entity isolation is mandatory.
```

---

# 26. Debit = Credit

Absolute invariant:

```text
SUM(Debit Base Amount)
==
SUM(Credit Base Amount)
```

for every posted Journal Entry.

If not:

```text
PostingRejected
```

No auto-fix-later.

---

# 27. Posted Entry Immutable

After posting, no in-place edits to:

```text
Account
Debit/Credit amount
Posting date
Currency context
Source
Dimensions
Rule version
```

---

# 28. Corrections

Correct with:

```text
Reversal
+
Correct entry/source correction
```

not delete/update.

---

# 29. Traceability Requirement

Every posted line must lead to:

```text
Journal Entry
Source Fact
Source Module
Source Document / Operation
Posting Rule
Account Determination
Dimensions
Actor / System context
```

---

# 30. "Why This Account?"

For any automatically selected account:

```text
Account 410100 — Electronics Revenue

Because:
Role = SalesRevenue
Product Category = Electronics
Legal Entity = Egypt
Mapping = REV-ELECTRONICS v3
```

must be obtainable.

---

# 31. "Why This Amount?"

Accounting drill-down must explain semantic amount source:

```text
Invoice net
Tax
Inventory issue value
Payment amount
FX difference
Adjustment
```

---

# 32. Posting Preview

Accounting exposes:

```text
Preview Accounting
```

for supported draft/manual/configuration scenarios.

---

# 33. Preview Uses Same Posting Engine

Forbidden:

```text
UI preview calculator
+
different real posting calculator
```

Use Ledger engine dry-run.

---

# 34. Preview Result

Show:

```text
Posting Date
Journal
Account
Debit
Credit
Currency
Dimensions
Rule/Mapping Explanation
Warnings
```

---

# 35. Preview Is Not Posting

No persisted Journal Entry/Open Item effects.

---

# 36. Automatic Posting Is Default

Normal business user should not manually make journal entries for:

```text
Customer Invoice
Supplier Invoice
Payment
Stock Valuation
Inventory Issue
Payroll
Asset Depreciation
```

when owning module exists.

---

# 37. Source Module Responsibility

Business module emits semantic Fact.

Ledger determines accounts/posts.

Accounting displays/explains.

---

# 38. No Duplicate Approval

If source Invoice was validly finalized/approved:

do not force a second generic Ledger approval merely because a posting exists.

Manual Journals can have their own approval policy.

---

# 39. Chart of Accounts — Product Goal

A small company should start with sensible seeded accounts.

A large company should be able to manage its own controlled hierarchy.

---

# 40. Chart of Accounts Source

Canonical CoA lives Ledger.

Accounting exposes management UX/API application services.

---

# 41. Account Core Fields

Conceptual display/admin:

```text
Code
Arabic Name
English Name
Account Type
Parent
Posting/Header
Normal Balance / semantic classification
Control Account Policy
Manual Posting Allowed?
Active / Archived
Reporting Classification
Currency restriction? optional
Dimension requirements
```

---

# 42. Account Types

Initial accounting types should be stable and minimal:

```text
Asset
Liability
Equity
Revenue
Expense
```

Subclassification/report grouping is separate.

---

# 43. Do Not Encode Reporting Only in Account Number

Forbidden assumption:

```text
1000-1999 always Assets
```

Companies use different codes.

---

# 44. Reporting Classification

Financial statements use semantic/report mappings over accounts/types/categories.

---

# 45. Header vs Posting Account

Header/group accounts organize hierarchy.

Only posting accounts accept Journal Lines.

---

# 46. Account Code

Unique within Legal Entity/book scope per policy.

---

# 47. Account Rename

Allowed with audit if safe.

Historical Journal Lines reference AccountId.

---

# 48. Account Type Change

High risk after history.

Ordinary UI change should be blocked once used if it changes financial semantics.

Use controlled migration/reclassification.

---

# 49. Account Archive

Used account is archived, not deleted.

---

# 50. Account With Balance

Cannot disappear from reports/history because archived.

---

# 51. Control Accounts

Examples:

```text
AR Control
AP Control
Inventory Control
Tax Control
Bank/Cash control where policy requires
```

---

# 52. Manual Posting to Control Accounts

Default:

```text
AllowManualPosting = false
```

for controlled accounts.

---

# 53. Why Control Account Guard Exists

Prevent:

```text
GL AR != Subledger AR
GL AP != Subledger AP
GL Inventory != Inventory Valuation
```

---

# 54. Control Account Exception

If company genuinely requires manual control-account posting:

```text
special permission
reason
approval/step-up optional
diagnostic impact warning
audit
reconciliation issue visibility
```

No ordinary accountant shortcut.

---

# 55. Account Determination UX

Finance admin configures semantic roles, not scattered GL IDs in business modules.

---

# 56. Semantic Account Roles

Examples:

```text
AccountsReceivableControl
AccountsPayableControl
SalesRevenue
PurchaseExpense
InventoryAsset
COGS
GRNI
OutputTaxPayable
InputTaxRecoverable
Cash
Bank
FXGain
FXLoss
RetainedEarnings
```

---

# 57. Account Mapping Scope

Typed dimensions can include:

```text
Legal Entity
Account Role
Product Category
Party Accounting Group
Tax Code
Journal
Inventory Group
```

---

# 58. Mapping Determinism

More specific valid mapping wins.

Same-rank conflict:

```text
AccountingConfigurationConflict
```

---

# 59. No "Newest Wins"

Insertion order must never determine accounting result.

---

# 60. Missing Mapping

Posting blocks.

Never silently use random suspense.

---

# 61. Explicit Suspense Only

Suspense can exist for explicit migration/reconciliation workflows.

Must be policy-controlled and visible.

---

# 62. Accounting Configuration Health

Before go-live, validate:

```text
required Account Roles mapped
Journals configured
Base Currency configured
Fiscal Period exists
Retained Earnings mapped
Tax mappings valid
AR/AP control mappings valid
Inventory/GRNI mappings if Inventory active
```

---

# 63. Mapping Simulator

Finance admin can choose:

```text
Fact Type
Product Category
Party/Group
Tax context
Journal
Dimensions
```

and see:

```text
which accounts would be selected and why
```

without posting.

---

# 64. Journal Definitions

Canonical Journal definition lives Ledger.

Accounting UX can manage:

```text
General Journal
Sales Journal
Purchase Journal
Cash/Bank Journal
Inventory Journal
Payroll Journal
Opening/Migration Journal
```

as needed.

---

# 65. Journal Is Not Source Module

Journal classifies/accounting stream.

It does not move ownership of Invoice/Payment/Inventory.

---

# 66. Manual General Journal

Manual Journal is first-class for accountants.

Use cases:

```text
Accrual
Adjustment
Correction
Reclassification
Opening
Authorized transfer
Year-end adjustment
```

---

# 67. Manual Journal Is Not a Replacement for Business Documents

Do not use General Journal to bypass:

```text
Invoice
Payment
Stock Movement
Payroll
Asset transaction
```

where specialist module exists.

---

# 68. Manual Journal UX — Small Company

Minimum:

```text
Date
Description
Account
Debit/Credit
Amount
[Post]
```

with smart balancing UI.

---

# 69. Manual Journal UX — Large Company

Optional:

```text
Draft
→ Review
→ Approve
→ Post
```

plus dimensions/attachments/reference.

---

# 70. Manual Journal Draft

Accounting application works with Ledger's:

```text
GeneralJournalDraft
GeneralJournalDraftLine
```

or approved equivalent.

---

# 71. Draft Is Editable

Until posted/approved policy boundary.

---

# 72. Draft Total Panel

Always show:

```text
Total Debits
Total Credits
Difference
```

live.

---

# 73. Post Button

Disabled/blocked if:

```text
Debit != Credit
Period invalid
Account inactive
Control account restricted
Dimension invalid
Currency invalid
Mapping/policy invalid
```

---

# 74. Account Picker

Search:

```text
Code
Arabic Name
English Name
Alias
```

fast server-side.

---

# 75. Entry Templates

Optional convenience later:

```text
Monthly rent accrual
Recurring adjustment
Standard allocation
```

Templates create Drafts, not posted truth automatically without policy.

---

# 76. Attachments

Manual Journal can reference Platform Files:

```text
supporting schedule
approval document
calculation sheet
external evidence
```

---

# 77. Journal Description

Required/meaningful for manual entries.

Avoid "adjustment" as only explanation for high-risk postings.

---

# 78. External Reference

Optional:

```text
Bank advice
Auditor reference
Legacy voucher
Contract reference
```

---

# 79. Manual Journal Reversal

Authorized user:

```text
Open posted JE
→ Reverse
→ choose valid reversal date
→ reason
→ Post reversal
```

---

# 80. No Delete-to-Unpost

Forbidden.

---

# 81. Automatic Reversal

Useful for:

```text
Accrual
Period-end temporary adjustment
FX revaluation
```

metadata can schedule reverse in next period.

---

# 82. Reversal Date

Must respect open period/lock policy.

---

# 83. Fiscal Year

Canonical Ledger entity.

Can differ from calendar year.

---

# 84. Accounting Period

Canonical Ledger entity.

Typical monthly periods, but architecture does not hardcode month-only if legal/company setup differs.

---

# 85. Period UX — Small Company

Simple:

```text
Jan 2027     Open
Feb 2027     Open
Dec 2026     Closed
```

Actions:

```text
Close
Reopen (permission)
```

---

# 86. Period UX — Large Company

Show:

```text
Operational Close
Finance Close
Hard Lock
Close checklist
exceptions
approvals
```

only if configured.

---

# 87. Period States

Keep canonical states minimal in Ledger.

Accounting may present friendly states such as:

```text
Open
Soft Closed
Closed/Locked
```

mapped to actual lock policies.

---

# 88. Posting Date Validation

Every post:

```text
Posting Date
→ Fiscal Period
→ Lock rules
```

before Ledger persistence.

---

# 89. Closed Period

Normal posting blocked.

---

# 90. Reopen Period

High-risk command:

```text
permission
reason
audit
optional approval/step-up
```

---

# 91. No Silent Backdating

Historical posting into closed/locked period cannot occur from another module silently.

---

# 92. Current-Period Correction

If original period locked:

correction may require current open-period reversal/adjustment according to policy.

---

# 93. Close Is a Finance Control, Not Just Status

Before close, Accounting evaluates health.

---

# 94. Close Workbench — Small Company

Example:

```text
December 2026

Ledger Balanced        ✓
AR Reconciled          ✓
AP Reconciled          ✓
Inventory Reconciled   ✓
Failed Postings        0
Unmapped Facts         0

[Close Month]
```

---

# 95. Close Workbench — Large Company

Can add:

```text
Bank reconciliation
Tax review
Accrual checklist
Prepayments
Fixed assets
Payroll
Intercompany later
Manual journal review
Approval
```

---

# 96. Close Checklist Is Capability-Based

Do not show Inventory check if company does not use Inventory.

Do not show Payroll check if module inactive.

---

# 97. Close Blockers

Default hard blockers:

```text
Ledger unbalanced/corrupt
AR reconciliation difference
AP reconciliation difference
Inventory GL difference when Inventory active
failed required postings
missing/unresolved accounting facts affecting period
migration/opening integrity issue
```

---

# 98. Close Warnings

Potential non-blocking based on policy:

```text
unallocated customer payment
long-open payable
open draft journal
unreconciled bank items if banking not close-mandatory
```

---

# 99. Close Exception

If company permits closing with known issue:

```text
special permission
explicit reason
issue snapshot
approver
audit
```

Difference must remain visible after close.

---

# 100. Reopen Impact

Show affected:

```text
financial statements
tax/reporting
reconciliations
later periods
```

before confirmation.

---

# 101. Reconciliation Is Core Product, Not Afterthought

Accounting must continuously prove:

```text
Subledger
Inventory
Payments/Bank
Ledger
```

agree where they should.

---

# 102. AR Reconciliation

Invariant:

```text
AR Subledger Control Total
==
GL AR Control Account
```

for same:

```text
Legal Entity
Control Account
Currency/Book context
AsOfDate
```

---

# 103. AP Reconciliation

Invariant:

```text
AP Subledger Control Total
==
GL AP Control Account
```

---

# 104. Inventory Reconciliation

Invariant:

```text
Inventory Valuation
==
GL Inventory Control
```

for defined scope/date.

---

# 105. Reconciliation Uses Carrying Values

Not just transaction-currency open values.

---

# 106. Reconciliation Result

Common contract/result shape:

```text
Source Balance
GL Balance
Difference
Status
AsOf
Diagnostic Breakdown
```

---

# 107. Explainable Difference

Potential diagnostics:

```text
Open Item without expected Ledger Fact
Ledger posting without Subledger item
Manual posting to control account
Reversal mismatch
Migration mismatch
Inventory valued movement without posting
Ledger stock posting without Inventory source
```

---

# 108. "Difference = 0" Is Not Enough

Accounting should also expose:

```text
Last checked
AsOf
Scope
Source versions/status
```

---

# 109. Reconciliation Schedule

Support:

```text
On demand
After migration
At period close
Nightly/lightweight check where feasible
```

---

# 110. Reconciliation History

Store run/result metadata as Accounting-owned application evidence where useful.

Do not duplicate source balances as new truth.

---

# 111. Reconciliation Workbench

One screen:

```text
AR        Reconciled
AP        Reconciled
Inventory Difference 4,500
Bank      12 unmatched
```

click any row to diagnostics.

---

# 112. Accountant Should Not Hunt Across Modules

From reconciliation problem, deep-link directly to:

```text
Open Item
Journal Entry
Invoice
Payment
Inventory Movement
Opening Batch
```

---

# 113. AR Workspace

Accounting composes Subledger AR.

Show:

```text
Total Receivable
Current
Overdue
Aging
Customer balances
Open Items
Credits/Advances
```

---

# 114. AP Workspace

Show:

```text
Total Payable
Due soon
Overdue
Supplier balances
Open Items
Credits/Advances
Payment holds
```

---

# 115. AR/AP Engine Is Shared Subledger

Accounting must not build separate Receivable/Payable engines.

---

# 116. Aging

Derived from Open Items.

Typical:

```text
Current
1-30
31-60
61-90
90+
```

---

# 117. Historical Aging

Must support:

```text
Aging as of prior date
```

using Subledger immutable allocation history.

---

# 118. Party Statement

Customer/Supplier statement:

```text
Opening Balance
Invoices/Bills
Credits
Payments
Allocations
Adjustments
Closing Balance
```

---

# 119. Statement Must Reconcile

Closing balance equals Subledger Party balance for same scope/date.

---

# 120. Statement Is Not Raw GL

Do not expose GL control lines as a substitute for party statement.

---

# 121. Party Statement UX

Inputs:

```text
Party
Date Range
Currency mode
Include settled? optional
```

Outputs:

```text
screen
PDF
Excel
```

---

# 122. Arabic/English Statements

Rendering supports bilingual/RTL templates.

---

# 123. Open Item Drill-Down

From balance:

```text
Customer
→ Open Items
→ Invoice
→ Payment allocations
→ Settlement/reversal/FX
```

---

# 124. Partial Settlement

First-class.

Accounting UI must clearly show:

```text
Invoice 10,000
Paid     4,000
Remaining 6,000
```

---

# 125. Advances/Unapplied Payments

First-class.

Do not hide under negative invoice hacks.

---

# 126. Allocation/Reconciliation

Accounting can expose finance UX for Subledger settlement allocation.

Subledger remains canonical engine.

---

# 127. Manual Allocation UX

Show:

```text
Source available amount
Eligible open items
Applied amounts
Remaining source
Remaining target balance
FX impact
Write-off impact
```

---

# 128. Allocation Commit

Backend revalidates source/target current balances.

Frontend values not authoritative.

---

# 129. Allocation Concurrency

Two finance users cannot over-allocate same payment/open item.

---

# 130. Write-Off

Explicit, permissioned and fact-producing when required.

Never silently zero small balances.



# 131. Cash Discount / Settlement Tolerance

If supported, explicit settlement policy + Ledger fact.

Do not hide as rounding.

---

# 132. Multi-Currency Accounting

Accounting uses FinBase + Ledger/Subledger.

It does not implement its own rate table.

---

# 133. Base Currency

Canonical:

```text
Organization.LegalEntity.base_currency_code
```

One Legal Entity has one functional/base currency at a time.

---

# 134. Transaction Currency Evidence

Journal/financial source can retain:

```text
TransactionCurrency
TransactionAmount
BaseAmount
ExchangeRateSnapshot
RatePurpose
```

---

# 135. Historical FX Stability

Today's rate never rewrites historical posted base amounts.

---

# 136. Settlement FX

Subledger/Payments determine settlement context.

Ledger posts:

```text
SettlementFxDifference
```

to mapped FX Gain/Loss.

---

# 137. Realized FX

Triggered by settlement difference.

Traceable to:

```text
original open item
payment/credit
rate snapshots
settlement
journal entry
```

---

# 138. Unrealized FX

Period-end revaluation is explicit accounting process.

It does not rewrite original invoices/open items.

---

# 139. FX Revaluation Workspace

For eligible foreign monetary balances:

```text
AsOf Date
Currency
Original carrying amount
Current eligible rate
Revalued amount
Difference
Proposed posting
```

---

# 140. Revaluation Preview

Uses real Ledger posting rules in preview mode.

---

# 141. Revaluation Post

Creates explicit Journal Entry/Facts.

---

# 142. Revaluation Reversal

Configurable:

```text
Auto reverse next period
Keep until next revaluation
```

according to accounting policy.

---

# 143. Single-Currency SME

If Legal Entity only uses EGP:

do not show FX complexity.

---

# 144. Multi-Currency Company

Expose FX workbench only when foreign monetary balances exist/configured.

---

# 145. General Ledger Explorer

Accountant can query:

```text
Account
Date Range
Branch/Dimensions
Journal
Source Type
Currency
Reference
```

---

# 146. GL Row

Typical:

```text
Posting Date
JE Number
Account
Description
Debit
Credit
Running Balance
Source
Dimensions
```

---

# 147. Running Balance

Must be mathematically correct for selected account/scope/order.

---

# 148. Opening Balance in GL View

Derived from postings before period start.

Do not store independent manually maintained period opening for every period.

---

# 149. Drill-Down From GL

```text
Account Line
→ Journal Entry
→ Source Fact
→ Source Document
```

---

# 150. Source Badges

Examples:

```text
Invoice
Supplier Bill
Payment
Inventory
Payroll
Manual Journal
Opening
FX Revaluation
```

---

# 151. General Ledger Pagination

Keyset/cursor for large data.

No deep OFFSET on millions of lines.

---

# 152. Trial Balance Is Core

Trial Balance is an integrity tool, not optional reporting decoration.

---

# 153. Trial Balance Default

Show:

```text
Account Code
Account Name
Opening Debit
Opening Credit
Period Debit
Period Credit
Closing Debit
Closing Credit
```

---

# 154. Trial Balance Scope

Inputs:

```text
Legal Entity
AsOf / Date Range
Account range/filter
Dimension filter optional
Posted only default
```

---

# 155. Trial Balance Invariant

Across all accounts for same scope:

```text
Total Debit Balance
==
Total Credit Balance
```

according to presentation convention.

---

# 156. Trial Balance Drill-Down

```text
Trial Balance
→ Account
→ GL lines
→ Journal Entry
→ Source Document
```

---

# 157. Trial Balance Performance

Must not sum entire Journal Line history naively for every request at scale.

Use Ledger read models/balance projections while preserving source correctness.

---

# 158. Balance Projection

Allowed as rebuildable performance structure:

```text
AccountPeriodBalance
```

or equivalent.

Canonical Journal Lines remain truth.

---

# 159. Projection Rebuild

Must be possible from immutable Ledger.

---

# 160. Projection Reconciliation

Regular integrity check:

```text
Projection
==
Canonical Ledger aggregation
```

on sampled/controlled basis.

---

# 161. Financial Statements

Accounting owns statement definition/presentation layer.

Ledger supplies accounting balances.

---

# 162. Must-Have Statements

Initial production Accounting:

```text
Balance Sheet
Profit & Loss
Trial Balance
General Ledger
```

---

# 163. Highly Valuable Owner Report

```text
Cash / Liquidity Summary
```

with clear source/reconciliation context.

---

# 164. Cash Flow Statement

Design support from start.

Formal statement implementation may follow once cash-flow classification/mappings are validated.

Do not fake cash flow by simply subtracting profit.

---

# 165. Balance Sheet

Derived from classified:

```text
Assets
Liabilities
Equity
```

as of date.

---

# 166. Profit & Loss

Derived from:

```text
Revenue
Expenses
```

over period.

---

# 167. Statement Mapping

Do not hardcode account code ranges globally.

Use:

```text
FinancialStatementDefinition
Section
Line
Account/Classification Mapping
```

---

# 168. Reporting Classification vs Account Type

Account Type is fundamental.

Reporting classification can create:

```text
Current Assets
Non-current Assets
Operating Revenue
Other Income
Operating Expenses
Finance Costs
```

without changing account type.

---

# 169. Statement Definition Versioning

Material statement layout/mapping changes are versioned/effective where historical reproducibility matters.

---

# 170. Statement Line Formula

Prefer constrained typed formulas:

```text
Sum mapped accounts
Subtotal child lines
Derived arithmetic between statement lines
```

No arbitrary SQL.

---

# 171. Statement Mapping Conflict

One account can map to multiple valid report views only if definition explicitly supports it.

Avoid accidental duplicate inclusion in same statement.

---

# 172. Unmapped Account Check

Financial statement health must detect posting accounts not mapped where statement requires mapping.

---

# 173. Statement Total Integrity

```text
Assets = Liabilities + Equity
```

must be checked for Balance Sheet.

If not, show critical diagnostic.

---

# 174. P&L Period Semantics

P&L uses period activity, not closing balance copied blindly.

---

# 175. Current-Year Profit

Balance Sheet equity presentation can include current-year result according to statement mapping/year-close rules.

---

# 176. Retained Earnings

Ledger configuration owns retained earnings account role/mapping.

Accounting close/year-end UI exposes process.

---

# 177. Year-End Close

Purpose:

```text
close P&L accounts
carry Balance Sheet accounts
transfer result according to policy
```

through explicit Ledger process.

---

# 178. No Destructive Year Close

Historical P&L transactions remain.

Year close creates controlled accounting entries/process state.

---

# 179. Year-End Preview

Show:

```text
Net result
Target retained earnings
Proposed close entries
Open issues
```

---

# 180. Year-End Posting

Idempotent.

Cannot accidentally close same year twice.

---

# 181. Prior-Year Adjustment

Handled through explicit adjustment/open-period policy.

Never rewrite old entries.

---

# 182. Financial Statements — Owner View

Owner should see clean labels, not account IDs by default.

---

# 183. Financial Statements — Accountant View

Expandable:

```text
Statement line
→ Accounts
→ Journal Lines
→ Sources
```

---

# 184. Comparative Columns

Useful:

```text
This Month
Previous Month
YTD
Previous Year
```

once statement engine stable.

---

# 185. Comparative Scope

Periods must be comparable and clearly labeled.

---

# 186. Branch/Dimension P&L

Allowed when dimension coverage is complete.

Do not show "Branch Profit" if critical lines lack Branch dimension and result would mislead.

---

# 187. Dimension Completeness Warning

If filtered statement excludes untagged lines unexpectedly:

show warning/coverage.

---

# 188. Dimensions Product Goal

Analyze business without exploding CoA.

---

# 189. Built-In Dimensions

Potential:

```text
Branch
Department
Cost Center
Project
Profit Center
Sales Channel
Region
```

only as needed.

---

# 190. Small Company Dimensions

Default:

```text
Branch only if multi-branch
```

No forced Cost Centers/Projects.

---

# 191. Dimension Definition

Canonical Ledger configuration.

Accounting exposes admin UX.

---

# 192. Dimension Requirement Policy

Can be:

```text
Required
Optional
Forbidden
```

by:

```text
Account
Journal
Fact Type
```

---

# 193. Dimension Defaults

Can come from:

```text
Branch
Source document
User context
Project
Product
Account mapping
```

deterministically.

---

# 194. Final Posted Dimension Immutable

Correction/reversal only.

---

# 195. No Every-Custom-Field-as-Dimension

Custom field becomes accounting dimension only through explicit approved typed promotion.

---

# 196. Dimension Picker UX

Only show dimensions relevant/required for selected account/journal.

---

# 197. Missing Required Dimension

Posting blocks with friendly message.

---

# 198. Dimension Reports

Accountant can analyze:

```text
P&L by Branch
Expense by Department
Revenue by Channel
Cost by Project
```

without duplicated accounts.

---

# 199. Branch Is Not Separate Ledger by Default

Branch is dimension/context.

Legal Entity owns balanced books.

---

# 200. Journal Entry Cannot Span Legal Entities

Absolute domain/DB constraint.

---

# 201. Legal Entity Accounting Context

Each entity owns:

```text
Base Currency
CoA/config
Fiscal Years
Periods
Journals
Posting mappings
Financial statements
```

---

# 202. Entity Switch UX

Must clearly show active Legal Entity.

Clear/reload:

```text
CoA
period
currency
statement definitions
account mappings
```

as scoped.

---

# 203. Never Mix Entity A and B Balances

No cross-entity cache reuse.

---

# 204. Group Tenant

A user can access multiple entities, but each entity remains independently balanced.

---

# 205. Consolidation

Future/advanced capability.

Do not make core Accounting cross-entity.

---

# 206. Consolidation Seam

Design later for:

```text
entity mapping
eliminations
reporting currency
ownership
intercompany
```

without weakening entity-level books.

---

# 207. Intercompany

Future workflows create independent entries in each Legal Entity.

Never one JE across two entities.

---

# 208. Opening Balances Mission

Go-live must begin with trustworthy numbers.

---

# 209. Opening Is Migration Truth, Not Fake History

Do not create fake historical invoices/purchases merely to populate GL.

---

# 210. Opening General Ledger

Ledger receives:

```text
OpeningBalancePosted
```

with:

```text
Account
Debit/Credit
Dimensions
Currency context
Legacy reference
```

---

# 211. Opening AR/AP

If Subledger active, GL control balance alone is insufficient.

Need:

```text
GL AR/AP control opening
+
Subledger opening open items
```

with reconciliation.

---

# 212. Opening Inventory

Inventory owns:

```text
Opening qty
Opening valuation
Lots/Serials/Locations
```

and emits accounting facts.

Accounting does not post unexplained inventory GL amount if Inventory active.

---

# 213. Opening Cash/Bank

Coordinate Ledger/Payments/Banking migration policy.

---

# 214. Financial Go-Live Boundary

Explicit:

```text
FinancialGoLiveDate
OpeningPostingDate
HistoricalBoundary
```

---

# 215. Opening Import Preview

Before post:

```text
Account
Debit
Credit
Dimensions
Currency
Validation
Legacy Ref
```

plus:

```text
Total Debits
Total Credits
Difference
```

---

# 216. Unbalanced Opening

Block.

---

# 217. Explicit Suspense During Migration

If legacy books genuinely have unresolved difference:

only explicit approved migration suspense policy.

Never hidden.

---

# 218. Migration Source Evidence

Store:

```text
Legacy System
Legacy Account Code
Batch ID
File Hash
External Reference
Imported By
Imported At
```

---

# 219. Opening Batch Idempotency

Same batch cannot post twice.

---

# 220. Opening Reconciliation Report

Before go-live:

```text
Legacy Trial Balance
TODIR Opening Trial Balance
Difference by Account
AR reconciliation
AP reconciliation
Inventory reconciliation
Cash/Bank reconciliation status
```

---

# 221. Go-Live Approval

Once accepted:

Opening batch locked.

Corrections use controlled adjustment.

---

# 222. No Editing Opening After Go-Live

Critical.

---

# 223. Managed Ledger Mode

For small businesses:

TODIR automatically:

```text
Seeds CoA
Configures Journals
Configures Account Determination
Opens Periods
Posts Business Facts
```

User may never need debit/credit screens.

---

# 224. Managed Does Not Mean Weak Accounting

Internal correctness is identical.

Only complexity is hidden.

---

# 225. Full Accounting Mode

Expose:

```text
Chart of Accounts
Journals
Manual Journals
General Ledger
Trial Balance
Periods
Locks
Dimensions
Opening
Posting Configuration
Reconciliation
Financial Statements
```

---

# 226. Managed → Full Transition

No data migration.

Same Ledger.

Richer Accounting UX becomes visible.

---

# 227. Full → Managed

Hiding advanced UX must not delete/change accounting truth.

---

# 228. Accounting Mode Per Legal Entity

Possible if tenant has different entities at different maturity levels.

---

# 229. Small Company Setup Wizard

Goal: start accounting safely in minutes, not days.

---

# 230. Setup Wizard Inputs

Minimum:

```text
Legal Entity
Base Currency (already org-owned)
Country Pack
Business type/template
Financial Go-Live Date
Opening strategy
```

---

# 231. Seeded Chart of Accounts

Use Country/industry-ready templates where legally/product-verified.

Allow later customization.

---

# 232. Template Is Starting Point

Not hardcoded forever.

---

# 233. Setup Health Before Go-Live

Show:

```text
CoA              Ready
Journals          Ready
AR/AP             Ready
Tax mappings      Ready
Inventory mapping Ready/Not applicable
Periods           Ready
Opening           Ready/Pending
```

---

# 234. Accountant Setup Test

A competent accountant should understand the seeded structure without developer assistance.

---

# 235. Accounting Home — Small Company

Suggested:

```text
Money
- Cash & Bank
- Customers Owe Us
- We Owe Suppliers

Performance
- Revenue
- Expenses
- Profit

Health
- Books Balanced
- Reconciliation
- Things to Review
```

---

# 236. Accounting Home — Large Company

Can add:

```text
Period Close
Manual Journal Queue
Reconciliation Queue
Unmapped Facts
FX Revaluation
Entity switch
Advanced reports
```

---

# 237. Quick Actions

Small company:

```text
New Journal Entry
Trial Balance
P&L
Balance Sheet
Customer Statement
Supplier Statement
Close Month
```

---

# 238. Avoid Dashboard Overload

Do not show 40 finance KPIs.

Prioritize money and trust.

---

# 239. Owner Visibility Test

Owner must understand core financial state without knowing accounting terminology.

---

# 240. Owner Card — Receivables

Display:

```text
Customers owe us: 830,000 EGP
Overdue: 210,000 EGP
As of: Today
```

source: Subledger.

---

# 241. Owner Card — Payables

```text
We owe suppliers: 410,000 EGP
Due next 7 days: 95,000 EGP
```

---

# 242. Owner Card — Inventory Value

```text
Inventory: 1,900,000 EGP
Reconciled to GL: Yes
```

if Inventory active.

---

# 243. Owner Card — Profit

```text
Profit this month: 750,000 EGP
```

derived from posted P&L.

---

# 244. Profit Card Must State Basis

Example:

```text
Posted accounting through 2026-10-06
```

not imply unposted drafts included.

---

# 245. Cash Card

If bank reconciliation capability not fully available:

label source precisely, e.g.:

```text
Book Cash & Bank Balance
```

rather than implying bank-confirmed available cash.

---

# 246. Owner Alerts

Only material:

```text
Overdue receivables
Bills due soon
Reconciliation difference
Failed accounting posting
Negative/low cash warning later
Period not closed
```

---

# 247. Owner Drill-Down

Owner can drill:

```text
Receivables
→ Top customers / aging
```

without being forced into raw Journal Lines.

---

# 248. Accountant Drill-Down

Accountant can continue to:

```text
Open Item
→ Invoice
→ Journal Entry
```

---

# 249. User Experience Separation

Same truth, different depth:

```text
Owner        → financial meaning
Accountant   → financial control/detail
Business User→ business document
Auditor      → evidence/history
```

---

# 250. Accountant Acceptance Test — Goal

A new accountant evaluating TODIR should be able to perform core work with minimal explanation.

---

# 251. Accountant Acceptance Tasks

Must be able to:

```text
Open Trial Balance
View GL for an account
Find customer balance
Find supplier balance
View aging
Open a Journal Entry
Trace it to source
Create/post a manual Journal
Reverse a wrong Journal
Run P&L
Run Balance Sheet
Reconcile AR/AP/Inventory
Close a month
Explain a difference
```

---

# 252. Acceptance UX Rule

No task above should require understanding internal architecture names such as:

```text
AccountingFact
Outbox
RLS
PostingRuleResolver
SubledgerAggregate
```

---

# 253. Owner Financial Visibility Test — Goal

A non-accountant owner must answer:

```text
What do I have?
What am I owed?
What do I owe?
Am I profitable?
Where are the problems?
```

in a few minutes.

---

# 254. Owner Test Must Distinguish

```text
Cash
Profit
Receivables
Inventory
Payables
```

clearly.

---

# 255. Financial Terminology

Use accountant-standard terms in Accounting.

Use business-friendly labels where owner view benefits.

---

# 256. Arabic Terminology

Arabic localization must use professional accounting language consistently.

Do not literal-translate technical identifiers.

---

# 257. Bilingual Names

Accounts/report lines can support Arabic/English names.

---

# 258. RTL

Accounting tables/forms/reports must work correctly RTL.

Numeric columns remain easy to compare.

---

# 259. Decimal Alignment

Financial tables right-align numeric amounts and decimal places consistently.

---

# 260. Negative Amount Display

Configurable presentation:

```text
-1,000.00
(1,000.00)
```

without changing underlying sign.



# 261. Zero Values

Show `0.00` or dash consistently by report preference.

Do not hide missing data as zero.

---

# 262. Missing Data vs Zero

Critical UI distinction:

```text
0
```

means confirmed zero.

```text
Not available / Not reconciled
```

means source is unavailable/incomplete.

---

# 263. Report As-Of

Every accounting report clearly displays:

```text
Legal Entity
Date / Period
Currency
Posted/Draft scope
Generated At
```

---

# 264. Posted Default

Financial statements/Trial Balance default to Posted truth.

Draft/manual preview shown only if explicitly selected.

---

# 265. Draft Impact Preview

Optional:

```text
Posted Actual
+
Selected Draft Adjustments
=
Pro Forma
```

must be clearly labeled non-posted.

---

# 266. Comparative Reports

Never compare periods with hidden different entity/currency scopes.

---

# 267. Export

Core finance reports:

```text
PDF
Excel
CSV where useful
```

with permissions.

---

# 268. Excel Is Export, Not Source of Financial Truth

Edits to downloaded report do not sync back.

Imports use controlled workflows.

---

# 269. Report Numbers Must Be Reproducible

Same:

```text
AsOf
Entity
Posted ledger state/version
Definition version
```

produces same result.

---

# 270. Report Snapshot / Evidence

For audit/board/tax workflows, system can store:

```text
Report definition version
AsOf
GeneratedAt
GeneratedBy
FileRef/hash
```

without making the PDF canonical accounting truth.

---

# 271. Reporting Cutoff

Transactions posted after report AsOf must not appear in historical report merely because generated later.

---

# 272. Posting Date vs Document Date

Financial statements/GL default accounting period based on Posting Date.

Document Date can be filter/display context.

---

# 273. Tax Date

Tax-specific reporting belongs Tax/Country Pack.

Do not substitute Posting Date blindly.

---

# 274. Business Date Semantics

Ledger/FinBase definitions apply.

Accounting UI labels date types explicitly.

---

# 275. Journals List

Display:

```text
Code
Name
Type
Active
Manual Posting Allowed
Numbering
Default Dimensions?
```

---

# 276. Journal Entry Header

Display:

```text
Entry Number
Posting Date
Journal
Source
Description
Base Currency
Transaction Currency context
Status
Posted At
Posted By/System
Reversal Link
```

---

# 277. Journal Entry Lines

Display:

```text
Account
Description
Debit
Credit
Transaction Amount
Base Amount
Dimensions
Source Line ref
```

---

# 278. Posted Entry Status

Keep simple:

```text
Posted
Reversed / Reversing relation
```

Do not create unnecessary state machine for immutable entry.

---

# 279. Draft Journal Status

Application workflow can be:

```text
Draft
PendingApproval
Approved
Posted
Rejected
Cancelled
```

only capabilities enabled by company.

---

# 280. Journal Approval Policy

Potential:

```text
Disabled
AboveAmount
ByJournal
ByAccount
Always
```

---

# 281. Small Company Journal Policy

Default:

```text
Draft → Post
```

---

# 282. Large Company Journal Policy

Can require:

```text
Preparer != Approver
Approver != Poster
```

---

# 283. Automated Postings Do Not Enter Manual Approval Queue

Unless explicit accounting governance requires.

Avoid making every invoice wait for accountant approval.

---

# 284. Source Posting Failure

If a business transaction requires accounting atomically:

the source transaction must not report final success while required Ledger posting failed.

---

# 285. Posting Failure Queue

Accounting Health can expose genuinely recoverable/integration failures.

It must distinguish:

```text
Transaction rolled back — nothing posted
vs
Committed source + asynchronous downstream issue
```

---

# 286. No Generic "Unposted Fact" Without Explanation

Show:

```text
Source
Fact Type
Why not posted
Retryability
Required action
```

---

# 287. Mapping Missing Error

Business-friendly:

> "لا يمكن ترحيل العملية لأن حساب مبيعات هذه الفئة غير محدد."

Action:

```text
[Open Account Mapping]
```

---

# 288. Period Closed Error

> "الفترة المحاسبية مغلقة. اختر تاريخًا في فترة مفتوحة أو اطلب إعادة فتح الفترة."

---

# 289. Dimension Missing Error

> "مركز التكلفة مطلوب لهذا الحساب."

---

# 290. Unbalanced Manual Journal Error

Show:

```text
Debits  = 25,000
Credits = 24,500
Difference = 500
```

---

# 291. Control Account Error

Explain:

> "لا يمكن الترحيل اليدوي مباشرة إلى حساب العملاء لأنه حساب رقابي مرتبط بأرصدة العملاء."

---

# 292. Duplicate Source Posting

Return existing Journal Entry / source result where idempotent.

Do not show mysterious database unique error.

---

# 293. Posting Health Drilldown

From a problem:

```text
Configuration
→ Mapping
→ Preview
→ Fix
→ Retry source where allowed
```

---

# 294. Retry Policy

Only retry operations whose source and idempotency guarantees make retry safe.

---

# 295. No Manual Database Repair

Finance support runbooks use controlled application commands.

---

# 296. Accounting Application Schema

Accounting must not duplicate Ledger/Subledger canonical tables.

Recommended application-owned schema:

```text
business_accounting
```

or approved finance application schema.

---

# 297. What Accounting Schema May Own

Application-specific entities such as:

```text
accounting_workspace_preference
close_run
close_check_result
reconciliation_run
reconciliation_issue
financial_statement_definition
financial_statement_section
financial_statement_line
financial_statement_mapping
report_snapshot_ref
accounting_setup_progress
accounting_exception_note
```

---

# 298. What Accounting Schema Must NOT Own

```text
account
journal_entry
journal_entry_line
open_item
settlement
allocation
invoice
payment
stock_balance
inventory_valuation
```

---

# 299. Ledger DbContext Ownership

Ledger-owned tables remain in Ledger DbContext/schema.

Accounting consumes Ledger application/contracts/read models.

---

# 300. Subledger DbContext Ownership

No direct EF navigation from Accounting.

Use contracts/read queries.

---

# 301. Read Composition

Accounting can compose:

```text
Ledger read
+
Subledger read
+
Inventory valuation read
+
Payments/Bank read
```

into accountant dashboards.

---

# 302. Cross-Module Read Model

If performance requires a finance dashboard projection, it is:

```text
rebuildable
source-versioned
not independently editable
```

---

# 303. Accounting Contracts

Locked naming:

```text
Todir.Accounting.Contracts
```

for Accounting application capabilities.

---

# 304. Ledger Contracts

Accounting uses canonical Ledger contracts rather than exposing Ledger DB.

---

# 305. Subledger Contracts

Accounting uses:

```text
Party balances
Aging
Statements
Control reconciliation
Open Items
Settlement queries/actions
```

---

# 306. Inventory Contracts

Accounting uses:

```text
Valuation summary
Inventory-to-GL reconciliation
Valuation As-Of
```

---

# 307. Payments/Bank Contracts

Accounting uses operational cash/bank/reconciliation reads where connected.

---

# 308. Dependency Direction

Accounting may depend on stable financial contracts.

Specialist business modules must not depend on Accounting UI implementation.

---

# 309. API Base

Recommended:

```text
/api/v1/accounting
```

---

# 310. Accounting Home APIs

Representative:

```text
GET /home
GET /health
GET /owner-summary
GET /exceptions
```

---

# 311. Chart of Accounts APIs

Accounting façade/application endpoints can expose:

```text
GET  /chart-of-accounts
POST /accounts
PUT  /accounts/{id}
POST /accounts/{id}/archive
GET  /accounts/{id}/history
```

with commands delegated to Ledger ownership.

---

# 312. Mapping APIs

```text
GET  /account-mappings
POST /account-mappings
POST /account-mappings/preview
POST /account-mappings/{id}/activate
```

through Ledger-owned configuration services.

---

# 313. Journal Draft APIs

```text
GET  /journal-drafts
POST /journal-drafts
PUT  /journal-drafts/{id}
POST /journal-drafts/{id}/preview
POST /journal-drafts/{id}/approve
POST /journal-drafts/{id}/post
POST /journal-drafts/{id}/cancel
```

---

# 314. Posted Journal APIs

```text
GET  /journal-entries
GET  /journal-entries/{id}
POST /journal-entries/{id}/reverse
```

No edit/delete endpoint.

---

# 315. Trial Balance API

```text
GET /reports/trial-balance
```

with server-side scope/filter.

---

# 316. General Ledger API

```text
GET /reports/general-ledger
```

keyset-paged.

---

# 317. Financial Statement APIs

```text
GET /reports/profit-loss
GET /reports/balance-sheet
GET /financial-statements/{definitionId}
```

---

# 318. Aging APIs

Accounting façade may expose:

```text
GET /receivables/aging
GET /payables/aging
```

delegating to Subledger.

---

# 319. Statement APIs

```text
GET /parties/{partyId}/statement
```

from Subledger truth.

---

# 320. Period APIs

```text
GET  /periods
POST /periods/{id}/close-preview
POST /periods/{id}/close
POST /periods/{id}/reopen
```

---

# 321. Close Run APIs

```text
GET  /close-runs/{periodId}
POST /close-runs/{periodId}/refresh
GET  /close-runs/{periodId}/issues
```

---

# 322. Reconciliation APIs

```text
GET  /reconciliation
POST /reconciliation/run
GET  /reconciliation/runs/{id}
GET  /reconciliation/runs/{id}/issues
```

---

# 323. Opening APIs

```text
POST /opening/import
POST /opening/validate
POST /opening/preview
POST /opening/post
GET  /opening/reconciliation
POST /opening/approve-go-live
```

---

# 324. FX Revaluation APIs

```text
POST /fx-revaluation/preview
POST /fx-revaluation/post
GET  /fx-revaluation/runs
```

---

# 325. No Generic Set Balance

Forbidden:

```text
POST /accounting/set-account-balance
POST /customer/set-balance
POST /inventory/set-value
```

---

# 326. No Generic Set Posted Entry

Forbidden:

```text
PUT /journal-entry/{id}
```

for posted entry.

---

# 327. No Direct "Fix Reconciliation"

A difference must be corrected at its actual source/corrective entry.

Do not expose:

```text
[Make Difference Zero]
```

---

# 328. Command Idempotency

Mandatory for:

```text
post manual journal
reverse journal
close period
reopen period
opening post
opening approval
FX revaluation post
year close
reconciliation corrective command where applicable
```

---

# 329. Double Post Manual Journal

One Journal Entry only.

Retry returns existing result.

---

# 330. Double Period Close

Idempotent/current-state response.

---

# 331. Close vs Posting Race

Period close transaction/locking policy must prevent a new posting from slipping into a period after close checks but before lock.

---

# 332. Close Race Strategy

Final close:

```text
acquire period close lock
re-run critical blockers
set canonical lock
commit
```

within valid Ledger transaction.

---

# 333. Reconciliation vs Concurrent Posting

Reconciliation result carries:

```text
AsOf
Source cutoff/version
```

so accountant knows scope.

---

# 334. Manual Journal Concurrency

Optimistic row version on draft.

Only one post result.

---

# 335. Mapping Change Concurrency

Versioned/effective mapping activation.

Do not update active mapping in place if used.

---

# 336. Period Config Concurrency

Version checks for open/close/reopen.

---

# 337. RLS

Accounting-owned tenant data:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 338. Runtime DB Role

No table ownership/BYPASSRLS.

---

# 339. Legal Entity Scope

Every Accounting operation requires explicit allowed Legal Entity context.

---

# 340. Cross-Entity Reports

Only explicit multi-entity read/report capability.

Never mix by accidental missing filter.

---

# 341. Financial Permissions

Initial vocabulary:

```text
accounting.home.read
accounting.owner_summary.read

accounting.coa.read
accounting.coa.manage

accounting.mapping.read
accounting.mapping.manage
accounting.mapping.preview

accounting.journal.read
accounting.journal.create
accounting.journal.edit
accounting.journal.approve
accounting.journal.post
accounting.journal.reverse

accounting.gl.read
accounting.trial_balance.read
accounting.financial_statements.read

accounting.receivables.read
accounting.payables.read
accounting.statement.read
accounting.settlement.manage

accounting.reconciliation.read
accounting.reconciliation.run
accounting.reconciliation.resolve

accounting.period.read
accounting.period.close
accounting.period.reopen

accounting.opening.read
accounting.opening.manage
accounting.opening.approve

accounting.fx_revaluation.read
accounting.fx_revaluation.post

accounting.settings.manage
accounting.audit.read
```

---

# 342. Owner Permission

Owner view can be a dedicated permission bundle.

Do not automatically grant Journal posting.

---

# 343. Accountant Permission

Can manage day-to-day finance without system/admin permissions.

---

# 344. Auditor Permission

Read-only:

```text
GL
Journal history
Statements
Reconciliation
Audit evidence
```

no mutation.

---

# 345. Accountant vs Finance Manager

Role bundles can differ:

```text
Accountant
Senior Accountant
Finance Manager
Controller
Owner
Auditor
```

Permissions remain authority.

---

# 346. SoD Optional

Small company can have one accountant.

Large company can enforce:

```text
Preparer != Approver
Approver != Poster
Period closer != Journal preparer? optional
```

---

# 347. Step-Up Authentication

High-risk optional:

```text
Reopen closed period
Post very large manual Journal
Override control-account guard
Approve opening/go-live
Year close
Change critical account mappings
```

---

# 348. Audit — Critical Actions

Capture:

```text
CoA structural change
Account mapping change
Manual Journal create/approve/post
Journal reversal
Control-account exception
Period close/reopen
Opening import/post/approval
FX revaluation
Year close
Financial statement mapping change
Reconciliation exception override
```

---

# 349. Audit Fields

At minimum:

```text
Actor
Timestamp
Tenant
Legal Entity
Action
Entity/Command
Reason if required
Correlation ID
Before/After for mutable config
Source evidence
```

---

# 350. Audit Does Not Replace Ledger History

Journal entries/reversals remain financial truth.

Audit explains actions around them.

---

# 351. Error Codes

Stable examples:

```text
accounting.period_closed
accounting.period_lock_conflict
accounting.account_inactive
accounting.account_not_postable
accounting.control_account_manual_posting_forbidden
accounting.journal_unbalanced
accounting.dimension_required
accounting.dimension_forbidden
accounting.mapping_missing
accounting.mapping_conflict
accounting.posting_duplicate
accounting.journal_stale
accounting.reversal_not_allowed
accounting.reconciliation_difference
accounting.close_blocked
accounting.opening_unbalanced
accounting.opening_already_posted
accounting.fx_rate_missing
accounting.fx_revaluation_conflict
accounting.financial_statement_unmapped_account
accounting.permission_denied
accounting.idempotency_conflict
```

---

# 352. Accountant-Friendly Error Principle

Errors describe:

```text
What is wrong?
Why does it matter?
What can I do next?
```

---

# 353. No Stack Trace UX

Never show internal exception type as primary user message.

---

# 354. Financial Health Error Links

Every fixable health issue should offer:

```text
Open source
Open mapping
Open period
Open reconciliation
Open failed operation
```

---

# 355. Security — Financial Data Sensitivity

Accounting data is sensitive.

Least privilege.

---

# 356. Security — Owner Dashboard

A user who can view owner money summary may not have permission to open detailed employee/account notes.

---

# 357. Security — Export

Financial export requires dedicated permission where appropriate.

---

# 358. Security — Manual Journal

Server validates:

```text
accounts
period
dimensions
amounts
entity
control policy
approval
```

regardless of frontend.

---

# 359. Security — Report Scope

Never trust client-supplied LegalEntityId without authorization check.

---

# 360. Security — Mapping Admin

Mapping configuration can change future accounting.

Treat as high-risk financial configuration.

---

# 361. Security — RLS Tests

Cross-tenant accounting read/write isolation mandatory.

---

# 362. Performance Mission

Accountants judge ERP quality heavily by report responsiveness.

Trial Balance and GL cannot feel like batch jobs for normal scopes.

---

# 363. Performance Dataset Assumption

Design/load-test for:

```text
Millions of Journal Entries/Lines
Millions of Open Items/Allocations
Years of financial history
Many accounts
Multiple legal entities
High invoice/payment throughput
```

---

# 364. Normal Trial Balance Target

Initial measured product target:

```text
p95 <= 300-500ms
```

for ordinary entity/period scope with balance projection/read model.

Benchmark before promise.

---

# 365. GL Page Target

```text
p95 <= 150-300ms
```

for indexed account/date cursor page.

---

# 366. Owner Home Target

```text
p95 <= 300-600ms
```

with composed cached/read projections and health summaries.

Must not sacrifice freshness semantics.

---

# 367. Journal Post Target

Normal manual Journal:

```text
p95 <= 150-300ms
```

excluding external integrations.

Correctness first.

---

# 368. Reconciliation Target

Normal control reconciliation interactive.

Large historical/full rebuild can be background.

---

# 369. Financial Statement Target

Common P&L/Balance Sheet period views should be interactive.

Use account-period balance projections.

---

# 370. No Full-Ledger Scan Per Dashboard Card

Forbidden.

---

# 371. Account Period Balance Projection

Recommended performance seam:

```text
LegalEntity
Period
Account
Dimension summary where justified
Debit Activity
Credit Activity
Closing Balance
```

exact design belongs Ledger read model.

---

# 372. Projection Update

Strongly consistent or transactionally derived where needed for posted ledger reporting.

If async read model chosen, UI must expose freshness; prefer same-DB transactional/read-optimized model for core accounting.

---

# 373. Projection Rebuildability

Non-negotiable.

---

# 374. Statement Query

Set-based SQL.

No application loop over accounts/lines.

---

# 375. N+1 Forbidden

Examples:

```text
100 statement lines → 100 DB queries
100 accounts → 100 balance queries
100 customers → 100 aging queries
```

forbidden.

---

# 376. Indexes

Likely Ledger/read indexes:

```text
tenant/entity/posting_date
tenant/entity/account/posting_date
tenant/entity/journal/posting_date
source_type/source_id
period/account
reversal_of
fact_id/version
```

owned/validated in Ledger.

---

# 377. Accounting-Owned Indexes

For:

```text
close run
reconciliation run
statement mapping
exception queue
```

scope by tenant/entity/status/date.

---

# 378. Keyset Pagination

GL, Journal lists, exception lists.

---

# 379. Materialized Views

Only if evidence shows need and refresh correctness is controlled.

Do not use blindly for canonical live balances.

---

# 380. PostgreSQL First

No ClickHouse/OpenSearch for core accounting Day 1.

---

# 381. Redis

No financial truth in Redis.

Optional cache only for safe config/report metadata after measured need.

---

# 382. Local Cache

Cache stable:

```text
statement definitions
account mapping metadata
period metadata
```

with version/event invalidation.

---

# 383. Posting Does Not Depend on Cache Correctness

Cache miss/stale protection resolves canonical version.

---

# 384. Background Jobs

Use for:

```text
large reconciliation
large export
scheduled FX revaluation preview? as policy
report snapshot generation
health scans
projection rebuild
```

not for immediate Debit=Credit invariant.

---

# 385. Broker Outage

Core Ledger posting/financial transaction correctness must not require RabbitMQ.

Outbox catches integration events.

---

# 386. Observability Metrics

Potential:

```text
accounting_journal_post_duration
accounting_trial_balance_duration
accounting_gl_query_duration
accounting_statement_duration
accounting_reconciliation_duration
accounting_reconciliation_difference_count
accounting_close_blocker_count
accounting_mapping_missing_total
accounting_posting_failure_total
accounting_opening_difference_total
accounting_fx_revaluation_duration
```

No tenant/account IDs as metric labels.

---

# 387. Tracing — Manual Journal Post

Span stages:

```text
Validate Draft
Validate Period
Validate Accounts
Validate Dimensions
Build Fact
Posting Engine
Balance Validation
Persist
Audit
Outbox
Commit
```

---

# 388. Tracing — Report

```text
Resolve Entity/Period
Load Statement Definition
Load Balance Projection
Apply Mappings
Calculate Lines
Validate Totals
Return
```

---

# 389. Logging

Never dump:

```text
full Trial Balance
all Journal Lines
customer statements
bank details
```

into logs.

---

# 390. Health Monitoring

Technical health + accounting health are separate.

Database healthy does not mean books reconciled.

---

# 391. Accounting Health Scan

Can periodically validate:

```text
AR = GL
AP = GL
Inventory = GL
No failed postings
No unresolved mapping failures
Balance projections reconcile
Periods consistent
```

---

# 392. Alert Noise

Do not alert accountant for every successful post.

Exception-driven.

---

# 393. Source Trace UI

Every automatic Journal Entry should show:

```text
Created automatically from:
Invoice INV-...
Payment PAY-...
Inventory Movement ...
```

---

# 394. Rule Trace UI

Show:

```text
Posting Rule
Rule Version
Account Mapping
Mapping Version
```

on expandable details.

---

# 395. Technical Details Progressive Disclosure

Normal accountant sees meaning first.

Advanced/audit panel exposes implementation evidence.

---

# 396. Accountant Search

Global finance search can find:

```text
Journal Entry number
Account code/name
Invoice number
Payment reference
Customer/Supplier
Amount
External reference
```

within permission.

---

# 397. Amount Search

Potential exact/near search for accountant investigation.

Must be scoped/date-filtered to avoid expensive unbounded scans.

---

# 398. Saved Views

Accountants can save filters for:

```text
Open manual journals
Large adjustments
Specific accounts
Branch expenses
Unreconciled issues
```

---

# 399. Favorites

Optional UI convenience.

No domain impact.

---

# 400. Keyboard Efficiency

Accounting grids should support efficient keyboard entry/navigation.

Critical for accountant adoption.

---

# 401. Journal Line Entry

Support:

```text
Tab navigation
Account search by code/name
Copy line
Insert line
Debit/Credit shortcuts
Balanced totals visible
```

---

# 402. Paste From Spreadsheet

Optional useful capability:

Paste structured Journal Draft lines.

Validate server-side before post.

---

# 403. Import Manual Journals

Controlled:

```text
Download template
Upload
Validate
Preview
Fix errors
Post batch
```

---

# 404. Import Never Posts Blindly

Default requires validation/preview.

---

# 405. Import Idempotency

Batch key/hash prevents accidental duplicate posting.

---

# 406. Import Error Report

Row-specific:

```text
Account invalid
Period closed
Dimension missing
Amount invalid
Unbalanced batch
```

---

# 407. Bulk Journal Posting

Can post validated batches with deterministic transaction strategy.

Do not hold one enormous transaction unnecessarily if batches are independent.

---

# 408. Bulk Posting Atomicity

User must understand whether:

```text
whole batch all-or-nothing
```

or:

```text
each journal independent
```

Default finance import should prefer predictable grouped atomic units.

---

# 409. Financial Statement Export

PDF layout professional enough to present to management/accountant.

---

# 410. Trial Balance Excel

Preserve numeric cells as numbers, not formatted strings only.

---

# 411. Report Locale

Formatting follows locale, but underlying API decimals remain canonical strings/numbers per FinBase contract.

---

# 412. Print Header

Include:

```text
Company/Legal Entity
Report
Period/AsOf
Currency
Generated At
```

---

# 413. Audit Export

Authorized auditor can export evidence references.

---

# 414. Data Retention

Posted financial history retained according to legal/company policy.

No casual purge.

---

# 415. Draft Cleanup

Old unused Draft Journals can be archived/deleted according to policy.

Posted truth unaffected.

---

# 416. Legal Retention

Country Pack/legal review determines mandatory retention.

Accounting Core does not hardcode one country's retention globally.

---

# 417. Privacy

Party financial details are sensitive.

Statement/aging access permissioned.

---

# 418. Multi-Tenant Isolation

No cross-tenant report aggregation in ordinary Accounting.

---

# 419. Backup/Restore Financial Integrity

Operational runbook must include post-restore:

```text
Ledger balance validation
AR/AP reconciliation
Inventory reconciliation
Outbox consistency
projection health
```

---

# 420. Disaster Recovery Acceptance

Recovery is not complete merely because database starts.

Financial invariants must pass.



# 421. Banking/Reconciliation Boundary

Accounting displays bank/cash health and can host a finance-facing workbench.

Canonical bank statement/matching implementation belongs dedicated Banking/Reconciliation capability when implemented.

---

# 422. Book Balance vs Bank Statement Balance

Keep separate:

```text
Book Bank Balance
Bank Statement Balance
Unreconciled Difference
```

Never collapse into one ambiguous "Bank Balance".

---

# 423. Cash Account

Ledger supplies accounting cash balance.

Payments owns operational money movements.

---

# 424. Bank Reconciliation Summary

When capability active:

```text
Book Balance
Statement Balance
Unmatched Statement Items
Unmatched Book Items
Difference
Last Reconciled Date
```

---

# 425. Bank Reconciliation Not Mandatory For Simple Accounting Activation

If Banking capability inactive:

Accounting still works.

Owner card should label balance accordingly.

---

# 426. Collections Boundary

Accounting can expose AR aging/open items.

Collections owns:

```text
Dunning
Promise to Pay
Collection Cases
Collector workflow
```

Do not duplicate.

---

# 427. Payment Proposal Boundary

Accounting/AP can expose due supplier items.

Payment Run/Payments executes money movement.

---

# 428. Tax Workspace Boundary

Accounting can show tax control balances and links.

Tax returns/statutory filing belongs Tax/Country Pack/Compliance.

---

# 429. Payroll Boundary

Payroll produces finalized payroll accounting facts.

Accounting displays resulting liabilities/expenses.

No payroll calculation in Accounting.

---

# 430. Assets Boundary

Fixed Assets module owns asset register/depreciation calculation.

Accounting displays/posting/reconciliation.

---

# 431. Expenses Boundary

Expense module owns expense claims/workflow.

Accounting consumes facts.

---

# 432. Accounting Health By Module

Show only active integrations:

```text
Invoicing     Healthy
Payments      Healthy
Inventory     Healthy
Payroll       Not active
Assets        Not active
```

---

# 433. Module Activation Does Not Require Accountant Reconfiguration Everywhere

New module activation should:

```text
validate required Account Roles
suggest/default mappings
show missing configuration
```

not silently post to random accounts.

---

# 434. New Module Accounting Readiness

Before module financial go-live:

```text
Required Fact types known
Posting rules installed
Account mappings healthy
Dimensions resolvable
Periods available
```

---

# 435. Accounting Configuration Wizard for New Module

Example Inventory:

```text
Inventory Asset
COGS
GRNI
Inventory Variance
```

mapped with sensible template/defaults.

---

# 436. Template Defaults Need Accountant Visibility

Default mappings should be viewable/explainable.

No invisible magic.

---

# 437. Configuration Change Preview

Before activating new mapping:

show sample Facts / affected roles.

---

# 438. Configuration Effective Date

Financial configuration changes should have controlled effective date/version where needed.

---

# 439. Historical Entries Preserve Old Mapping

New mapping affects eligible new postings.

Old Journal Entries remain unchanged.

---

# 440. Accounting Setup Version Evidence

Posted Entry stores rule/mapping versions sufficient to reproduce explanation.

---

# 441. Module Manifest

Concept:

```text
id: accounting
type: business-application
purchasable: true or bundle-policy
scope: legal-entity/tenant UX
```

Hard dependency:

```text
Ledger
```

Common dependencies/capabilities:

```text
Subledger
FinBase
Directory
Invoicing
Payments
Inventory
Tax
```

depending enabled modules.

---

# 442. Accounting Can Be Exposed While Ledger Is Hidden

Managed Ledger may be hidden technical capability.

Accounting is the user-facing finance app.

---

# 443. Activation — Small Company

Seed:

```text
Accounting mode = Managed
CoA template
Journals
Periods
Basic statements
Mappings
```

---

# 444. Activation — Full Accounting

Expose admin/accountant workspaces.

No data migration.

---

# 445. Accounting Deactivation

Financial history cannot be deleted.

At most:

```text
WindDown
ReadOnly
Hidden UX
```

depending subscription/lifecycle.

---

# 446. Ledger Cannot Be Physically Removed While Financial History Exists

Hidden kernel remains.

---

# 447. Period Close Does Not Deactivate Modules

Separate concerns.

---

# 448. Accounting Readiness Status

Per Legal Entity:

```text
NotConfigured
Configuring
Ready
GoLiveApproved
ReadOnly
```

application projection only.

---

# 449. Go-Live Gate

Cannot mark Accounting ready until critical setup/reconciliation checks pass.

---

# 450. Accountant Onboarding

First-time sequence:

```text
1. Select company/entity
2. Review base currency
3. Review Chart of Accounts template
4. Review key control accounts
5. Review tax mappings
6. Review opening strategy
7. Import/enter openings if needed
8. Reconcile
9. Approve go-live
10. Open Accounting Home
```

---

# 451. Small Company Onboarding Must Be Short

If starting fresh with no legacy data:

```text
choose template
confirm
start
```

with generated periods/mappings.

---

# 452. Existing Company Migration Is More Controlled

Legacy balances require preview/reconciliation.

---

# 453. Fresh Company Opening

Can start all balances zero.

No fake opening batch required unless policy/audit wants it.

---

# 454. Accounting Health — First Day

Fresh company:

```text
Ledger Balanced ✓
AR Reconciled ✓ (0=0)
AP Reconciled ✓
Inventory Reconciled ✓ if active
```

---

# 455. Red Flag — False Healthy

Do not mark "Reconciled" if source subsystem has not completed migration/readiness.

Use:

```text
Not Ready
```

not green zero.

---

# 456. Account Hierarchy Validation

Prevent cycles.

Parent must belong same Legal Entity/CoA context.

---

# 457. Posting Account With Children

Policy should be explicit.

Recommended:

```text
Header/group accounts non-posting
Leaf accounts posting
```

for clarity.

---

# 458. Account Merge

Do not rewrite historical Journal Lines.

Prefer:

```text
stop future use
reclassify balance if required
archive old account
map new future postings
```

---

# 459. Account Split

Same: future mapping + controlled reclassification.

---

# 460. CoA Import

Support controlled Excel import for large companies:

```text
Code
Name AR
Name EN
Type
Parent
Posting?
Classification
```

---

# 461. CoA Import Validation

Check:

```text
duplicate codes
invalid parent
cycles
invalid type
unknown classifications
control-account conflicts
```

before commit.

---

# 462. CoA Import Existing Used Account

Do not silently change type/semantic role.

---

# 463. Financial Statement Mapping Import

Optional controlled template/import.

Preview totals/unmapped accounts.

---

# 464. Journal Numbering

Canonical Ledger numbering.

Accounting displays stable human reference.

---

# 465. Journal Entry Source Reference

Human-friendly source:

```text
INV-1002
PAY-550
ADJ-80
```

plus internal typed IDs.

---

# 466. Source Document Deleted/Archived

Posted JE still retains historical source reference/evidence.

---

# 467. Source Navigation Authorization

Accountant may see Journal but not sensitive source detail.

UI shows permitted summary.

---

# 468. No Authorization Escalation Through Drilldown

Deep link enforces target module permissions.

---

# 469. Accountant Need-to-Know Summary

Even if source page restricted, Accounting can show approved financial snapshot:

```text
Source Type
Reference
Posting Date
Amount
Party/semantic context if permitted
```

---

# 470. Trial Balance By Currency

Base currency is accounting truth.

Transaction currency views can be supplemental.

---

# 471. Do Not Aggregate Unlike Transaction Currencies

Unless converted/base amounts used.

---

# 472. Foreign Currency Account Detail

Show:

```text
Transaction amount/currency
Base amount
Historical rate
```

where relevant.

---

# 473. Exchange Rate Explanation

Link to FinBase rate snapshot/source/approval.

---

# 474. Missing Accounting Rate

Posting blocks according to FinBase policy.

No live uncontrolled external API call during posting.

---

# 475. Rate Override

Only via approved FinBase process.

Accounting does not provide hidden manual rate field bypass.

---

# 476. Monetary Precision

Use shared FinBase golden vectors.

No float/double.

---

# 477. Financial API Serialization

Amounts use stable decimal representation.

No localized commas in API.

---

# 478. Rounding

Accounting statements aggregate posted exact base amounts.

Do not re-round each line differently from Ledger.

---

# 479. Display Rounding

Presentation can round/display according to currency/report setting.

Underlying totals remain exact.

---

# 480. Rounding Difference Diagnosis

If displayed subtotals appear off due to display precision:

UI can expose exact amount/rounding note.

Do not alter ledger to make display prettier.

---

# 481. Financial Statement Sign Presentation

Can display expenses/contra accounts as preferred reporting sign.

Mapping/presentation layer controls it.

Ledger debit/credit remains unchanged.

---

# 482. Contra Accounts

Support through classification/mapping.

Do not infer solely from negative amount.

---

# 483. Normal Balance

Can assist presentation/validation.

Do not use as substitute for debit/credit math.

---

# 484. Account Balance Formula

Use account type/normal balance presentation consistently.

---

# 485. Zero-Balance Accounts

Configurable report display:

```text
Hide
Show
```

---

# 486. Inactive Accounts

Historical balances appear if non-zero/history requested.

---

# 487. Financial Statements With Drafts

Default excludes Draft.

Pro-forma mode explicitly labeled.

---

# 488. Audit Trail From Statement

Every statement amount must be traceable to account balances.

---

# 489. Audit Trail From Account

Every account balance traceable to immutable Journal Lines.

---

# 490. Audit Trail From Journal

Every automatic Journal traceable to Fact/source.

---

# 491. End-to-End Financial Trace Example — Customer Invoice

```text
P&L Revenue
→ Revenue Account
→ JE
→ CustomerInvoicePosted Fact
→ Invoice INV-1001
→ Customer
```

---

# 492. End-to-End Financial Trace Example — Inventory COGS

```text
P&L COGS
→ COGS Account
→ JE
→ InventoryIssueValued Fact
→ Stock Movement
→ Product/Warehouse/Lot
```

---

# 493. End-to-End Financial Trace Example — Supplier Payment

```text
Bank/Payable
→ JE
→ PaymentSent Fact
→ Payment
→ Settlement
→ Supplier Open Items
```

---

# 494. End-to-End Financial Trace Example — FX

```text
FX Gain/Loss
→ JE
→ SettlementFxDifference
→ Settlement
→ Invoice + Payment rate snapshots
```

---

# 495. Accounting Quality Is Cross-Module Quality

Accounting acceptance tests must include source modules.

A Ledger-only unit test is insufficient.

---

# 496. Test — Every Posted JE Balanced

Property test across all Fact types.

---

# 497. Test — Posted JE Immutable

No update/delete path after post.

---

# 498. Test — Source Idempotency

Same Fact/version posts once.

---

# 499. Test — Missing Mapping

Posting blocks.

No suspense fallback unless explicit.

---

# 500. Test — Mapping Conflict

Same-rank mappings block configuration/post.

---

# 501. Test — Why This Account

Every generated line can explain selected mapping/version.

---

# 502. Test — Preview Equals Post

Same input/context:

```text
Preview lines
==
Actual posted semantic lines
```

except IDs/timestamps.

---

# 503. Test — Manual Journal Simple Flow

```text
Create Draft
Balance
Post
```

works without approval capability.

---

# 504. Test — Manual Journal Approval Flow

When enabled:

unauthorized preparer cannot self-approve/post contrary to SoD.

---

# 505. Test — Unbalanced Manual Journal

Blocked.

---

# 506. Test — Control Account Manual Post

Blocked by default.

---

# 507. Test — Control Account Exception

If enabled:

permission/reason/audit/reconciliation warning all present.

---

# 508. Test — Closed Period

Posting blocked from:

```text
Manual Journal
Invoice
Payment
Inventory
```

according to Ledger policy.

---

# 509. Test — Close vs Concurrent Posting

No posting slips into closed period after final blocker check.

---

# 510. Test — Reopen Period

Permission/reason/audit enforced.

---

# 511. Test — Reversal

Original remains; inverse/corrective entry links exact.

---

# 512. Test — Reversal Into Locked Original Period

Uses allowed current/open period policy.

---

# 513. Test — Auto Reversal

Scheduled next-period reversal occurs exactly once.

---

# 514. Test — Trial Balance

Known fixture produces exact opening/activity/closing totals.

---

# 515. Test — Trial Balance Balance

Total Debit = Total Credit.

---

# 516. Test — GL Running Balance

Known account transactions produce exact running balance.

---

# 517. Test — Historical GL

Later master-data changes do not alter amounts/context.

---

# 518. Test — Balance Sheet Equation

```text
Assets = Liabilities + Equity
```

for known fixture.

---

# 519. Test — P&L

Revenue/expense period activity exact.

---

# 520. Test — Statement Mapping Change

New version changes eligible future/report definition use without mutating historical posted entries.

---

# 521. Test — Unmapped Statement Account

Health warning/error visible.

---

# 522. Test — Dimension Requirement

Required dimension blocks post.

---

# 523. Test — Dimension Default

Deterministic default applied.

---

# 524. Test — Dimension Override

Authorized only; posted dimension immutable.

---

# 525. Test — Branch P&L

Correct lines grouped by Branch dimension.

---

# 526. Test — AR Reconciliation

Known fixture:

```text
Subledger AR
=
GL AR
```

---

# 527. Test — AR Manual Control Difference

Intentional unsafe test creates detectable difference in controlled test scenario.

Diagnostics identify cause.

---

# 528. Test — AP Reconciliation

Exact.

---

# 529. Test — Inventory Reconciliation

Inventory valuation = GL Inventory control.

---

# 530. Test — Reconciliation As-Of

Historical date uses matching scope/carrying values.

---

# 531. Test — Historical Aging

Prior-period aging remains reproducible after later settlements.

---

# 532. Test — Party Statement

Opening + movements = closing; equals Subledger.

---

# 533. Test — Partial Payment

Invoice 10,000 / payment 4,000 / open 6,000.

---

# 534. Test — Multiple Payment Allocation

No over-allocation.

---

# 535. Test — Concurrent Allocation

No negative open amount.

---

# 536. Test — Foreign Invoice

Historical base amount uses original approved rate snapshot.

---

# 537. Test — Later Rate Change

Does not rewrite invoice/JE.

---

# 538. Test — Realized FX

Settlement difference posts exact gain/loss.

---

# 539. Test — Unrealized FX Revaluation

Explicit JE + optional reversal.

Original item unchanged.

---

# 540. Test — Single Currency

No unnecessary FX UI/setup.

---

# 541. Test — Opening GL

Balanced batch required.

---

# 542. Test — Opening AR/AP

Control balances reconcile to opening Open Items.

---

# 543. Test — Opening Inventory

Inventory opening valuation reconciles to GL.

---

# 544. Test — Opening Duplicate Import

Idempotency prevents double post.

---

# 545. Test — Go-Live Lock

Opening cannot be edited after approval.

---

# 546. Test — Managed Mode

Business transactions post correctly while debit/credit admin UI hidden.

---

# 547. Test — Managed to Full

Same Ledger history appears; no migration.

---

# 548. Test — Owner Summary

Receivables from Subledger, Inventory from Inventory, P&L from Ledger; no independent totals.

---

# 549. Test — Cash vs Profit

UI never conflates.

---

# 550. Test — Owner As-Of/Freshness

Cards expose proper scope/date/source status.

---

# 551. Test — Accountant Acceptance

A test persona completes defined core tasks without architecture knowledge.

---

# 552. Test — Arabic/RTL

Account codes/names/numeric alignment and reports usable RTL.

---

# 553. Test — English/Arabic Account Names

Search/display works.

---

# 554. Test — Million Journal Lines

Trial Balance/GL targets measured.

---

# 555. Test — Large Aging

Subledger batch/read model remains responsive.

---

# 556. Test — Large Statement

No N+1.

---

# 557. Test — Cross Tenant RLS

Tenant A cannot view/post Tenant B finance.

---

# 558. Test — Wrong Entity

Posting/report blocked.

---

# 559. Test — Auditor Read-Only

No mutation APIs authorized.

---

# 560. Test — Owner Bundle

Can view summary without Journal posting.



# 561. Architecture Red-Team — Accounting Creates Second Ledger

Reject.

Accounting is application/UX over Ledger.

---

# 562. Red-Team — Customer Balance Stored in Accounting

Reject.

Subledger only.

---

# 563. Red-Team — Inventory Value Recalculated in Accounting

Reject.

Inventory only.

---

# 564. Red-Team — Journal Entry Editable After Post

Reject.

---

# 565. Red-Team — "Unpost" Deletes Entry

Reject.

---

# 566. Red-Team — Missing Mapping Uses Suspense Automatically

Reject.

---

# 567. Red-Team — Manual AR/AP Control Posting Allowed to Everyone

Reject.

---

# 568. Red-Team — Period Close Is Just UI Flag

Reject.

Canonical Ledger lock must enforce posting.

---

# 569. Red-Team — Financial Statement Uses Hardcoded Account Number Ranges

Reject.

Use semantic/classification mapping.

---

# 570. Red-Team — Profit Calculated From Invoices Minus Expenses UI

Reject.

Profit is Ledger P&L truth.

---

# 571. Red-Team — Cash = Profit

Reject.

---

# 572. Red-Team — Owner Dashboard Uses Stale Unknown Data

Reject.

Freshness/AsOf/source semantics required.

---

# 573. Red-Team — Trial Balance Runs Full History Scan Every Time

Reject for production-scale design.

---

# 574. Red-Team — Reconciliation Difference Hidden

Reject.

---

# 575. Red-Team — "Fix Difference" Button Writes Balance

Reject.

Correct source/corrective entry.

---

# 576. Red-Team — Every Invoice Needs Accountant Approval

Reject by default.

Source workflow approval is sufficient unless policy explicitly adds finance review.

---

# 577. Red-Team — Small Company Forced Into SoD

Reject.

Optional.

---

# 578. Red-Team — Large Company Cannot Add SoD

Reject.

Must support advanced controls.

---

# 579. Red-Team — Every Company Must Configure CoA From Scratch

Reject.

Managed templates/defaults.

---

# 580. Red-Team — Accounting Mode Change Migrates Data

Reject.

Managed and Full use same Ledger.

---

# 581. Red-Team — Bank GL Shown As Reconciled Bank Balance

Reject unless reconciliation/source proves it.

---

# 582. Red-Team — Draft Journals Included In Official Financial Statements

Reject by default.

---

# 583. Red-Team — Current FX Revalues Old Posted Entries

Reject.

---

# 584. Red-Team — Opening Balances Entered Only In GL While Active Subledgers Differ

Reject.

---

# 585. Red-Team — Fake Historical Invoices For Migration

Reject unless explicit historical migration strategy truly requires them.

---

# 586. Red-Team — One JE Spans Two Legal Entities

Reject.

---

# 587. Red-Team — AI Directly Posts Journals

Reject.

---

# 588. AI Opportunities

AI may:

```text
Explain a reconciliation difference
Summarize monthly financial movement
Suggest likely mapping for review
Explain why an account was chosen
Suggest journal description
Detect unusual manual journal
Highlight anomalies
Answer natural-language questions through approved financial queries
```

---

# 589. AI Cannot

```text
change account mapping without authorization
post Journal directly outside command path
reopen period
override reconciliation
invent balance
change historical entry
change exchange rate
directly query/write DB bypassing permissions
```

---

# 590. AI Financial Answer Rule

When answering "How much profit?" AI must query approved Accounting report/read APIs.

No LLM arithmetic over arbitrary documents as financial truth.

---

# 591. AI Tool Path

```text
AI Tool Layer
→ Permissions
→ Accounting Query/Command
→ Ledger/Subledger/Other Domain
→ Audit
```

---

# 592. Natural Language Finance

Future:

```text
"مين أكتر 10 عملاء متأخرين؟"
"ليه المصروفات زادت الشهر ده؟"
"إيه سبب فرق المخزون؟"
```

answers from approved queries + traceable sources.

---

# 593. No Autonomous Financial Posting

Not Day 1.

---

# 594. Frontend Module Structure

Recommended:

```text
frontend/modules/accounting/

├── api/
├── routes/
├── pages/
│   ├── AccountingHomePage/
│   ├── OwnerFinancePage/
│   ├── ChartOfAccountsPage/
│   ├── AccountWorkspacePage/
│   ├── JournalListPage/
│   ├── JournalDraftPage/
│   ├── JournalEntryPage/
│   ├── TrialBalancePage/
│   ├── GeneralLedgerPage/
│   ├── ProfitLossPage/
│   ├── BalanceSheetPage/
│   ├── ReceivablesPage/
│   ├── PayablesPage/
│   ├── ReconciliationPage/
│   ├── PeriodClosePage/
│   ├── OpeningBalancesPage/
│   ├── FxRevaluationPage/
│   └── AccountingSettingsPage/
├── features/
│   ├── post-journal/
│   ├── reverse-journal/
│   ├── close-period/
│   ├── reopen-period/
│   ├── run-reconciliation/
│   ├── import-opening/
│   └── mapping-preview/
├── components/
│   ├── MoneySummaryCard/
│   ├── AccountingHealthPanel/
│   ├── JournalLineGrid/
│   ├── SourceTrace/
│   ├── ReconciliationStatus/
│   ├── FinancialStatementTree/
│   └── PeriodStatusBadge/
├── queries/
├── mutations/
├── permissions/
├── schemas/
├── i18n/
└── public.ts
```

---

# 595. Progressive Frontend

Managed Mode routes can hide:

```text
Mappings
Dimensions
Advanced journals
Revaluation
Close checklist
```

until needed.

---

# 596. Backend Application Structure

Recommended:

```text
backend/Modules/Accounting/

Todir.Modules.Accounting.Domain
Todir.Modules.Accounting.Application
Todir.Modules.Accounting.Infrastructure
Todir.Accounting.Contracts
```

Accounting depends on Ledger/Subledger contracts, not implementation.

---

# 597. Accounting Domain Is Small

Do not recreate Ledger aggregate/domain in Accounting.

Accounting-owned domain focuses on:

```text
CloseRun
ReconciliationRun/Issue
FinancialStatementDefinition
AccountingSetup/Readiness
Application-specific policy/workspace
```

---

# 598. Application Services

Use explicit use cases:

```text
GetAccountingHome
GetOwnerFinancialSummary
CreateGeneralJournalDraft
PostGeneralJournal
ReverseJournalEntry
RunTrialBalance
RunFinancialStatement
RunReconciliation
PreviewPeriodClose
ClosePeriod
ReopenPeriod
ImportOpeningBalances
ApproveFinancialGoLive
PreviewFxRevaluation
PostFxRevaluation
```

---

# 599. Infrastructure

Contains only Accounting-owned persistence/read composition.

No Ledger table ownership.

---

# 600. Direct SQL

Dapper/raw SQL allowed for measured report/read hot paths.

Never bypass domain/posting rules for commands.

---

# 601. Agent Implementation Philosophy

The coding Agent must implement in **vertical, verifiable phases**.

Do not build all UI first and accounting later.

Do not build all tables before invariants.

Each phase must prove financial correctness before next high-risk phase.

---

# 602. Agent Precedence

If conflict:

```text
16 Accounting pack
→ 09 Ledger / 10 Subledger for financial kernel ownership/invariants
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ later approved ADR
```

Accounting cannot override a locked Ledger invariant merely for UX convenience.

---

# 603. Agent Files To Read Before AC00

At minimum:

```text
01
02
03
06
09
10
11
12
13
14
15
all relevant AGENTS.md
approved ADRs
existing Todir.* repository structure
```

---

# 604. Agent Must Not Assume Greenfield

Inspect repository first.

Reuse existing Ledger/Subledger contracts/entities/services.

Do not duplicate because name differs slightly.

---

# 605. Agent Must Report Existing Capability

Before implementing each phase:

```text
Already exists
Partially exists
Missing
Conflicts with pack
```

---

# 606. Agent Stop Condition

Do not stop for ordinary implementation decisions already determined here.

Escalate only unresolved owner/architecture decision with:

```text
Problem
Affected invariant
Options
Recommendation
Impact
```

Continue unaffected work.

---

# 607. Implementation Sequence

```text
AC00 Repository/Financial Baseline Verification
AC01 Accounting Module Boundary + Architecture Tests
AC02 Accountant/Owner Experience Shell
AC03 Accounting Readiness + Setup Health
AC04 Chart of Accounts Management UX
AC05 Journals + Manual Journal Draft
AC06 Posting Preview + Source Trace
AC07 Account Mapping Administration + Simulator
AC08 Periods + Locks UX
AC09 General Ledger Explorer
AC10 Trial Balance
AC11 Account Balance Projections / Performance
AC12 AR Workspace
AC13 AP Workspace
AC14 Party Statements + Aging
AC15 Control Account Reconciliation
AC16 Inventory-to-GL Reconciliation
AC17 Accounting Health Dashboard
AC18 Financial Statement Definition Engine
AC19 Profit & Loss
AC20 Balance Sheet
AC21 Owner Financial Visibility
AC22 Period Close Workbench
AC23 Journal Reversal + Auto-Reversal UX
AC24 Multi-Currency / FX Trace
AC25 FX Revaluation Workspace
AC26 Opening Balances / Migration
AC27 Managed Ledger Setup
AC28 Managed → Full Accounting
AC29 Permissions / SoD / Audit
AC30 Imports / Exports / Reporting UX
AC31 Performance / Load / Concurrency
AC32 Security / RLS / Red-Team
AC33 Runbooks / Recovery / Projection Rebuild
AC34 Accountant Acceptance Test
AC35 Owner Financial Visibility Test
AC36 End-to-End Cross-Module Financial Acceptance
AC37 Release Gate
```

---

# 608. AC00 — Goal

Prove repository financial baseline matches approved architecture before adding Accounting UI/application.

---

# 609. AC00 Agent Tasks

Inspect and verify:

```text
Ledger schema/DbContext
Subledger schema/DbContext
Posting Engine
Account Determination
Posting Rules
Journal Entries/Lines
Fiscal Periods/Locks
GeneralJournalDraft support
Open Items
Settlements/Allocations
FinBase integration
Transaction Coordinator
RLS
Outbox
```

---

# 610. AC00 Do Not

Do not rewrite working Ledger/Subledger into Accounting.

---

# 611. AC00 Required Tests

Run existing:

```text
Ledger unit/integration
Subledger integration
RLS
Posting balance
Idempotency
Transaction Coordinator
```

---

# 612. AC00 Exit Gate

A written implementation report states exact reused kernel capabilities and gaps.

No Accounting feature coding until financial baseline is understood.

---

# 613. AC01 — Goal

Create Accounting application boundary without violating kernel ownership.

---

# 614. AC01 Required

Create/verify:

```text
Todir.Accounting.Contracts
Todir.Modules.Accounting.Application
Todir.Modules.Accounting.Domain
Todir.Modules.Accounting.Infrastructure
frontend accounting module
module manifest
```

according to repository conventions.

---

# 615. AC01 Architecture Tests

Must fail if Accounting attempts:

```text
direct Ledger DbContext access from wrong layer
direct Subledger implementation dependency
business module implementation imports Accounting
duplicate JournalEntry entity
duplicate OpenItem entity
```

---

# 616. AC01 Exit Gate

One-way contract dependencies pass architecture tests.

---

# 617. AC02 — Goal

Build the accountant and owner application shell around real finance reads.

---

# 618. AC02 Required UI

Routes:

```text
Accounting Home
Owner Finance
Journals
GL
Trial Balance
Financial Statements
AR/AP
Reconciliation
Periods
Settings
```

capability/permission-aware.

---

# 619. AC02 No Fake Numbers

Do not use hardcoded/dashboard placeholder values in production implementation.

Mock only in isolated frontend tests/story fixtures.

---

# 620. AC02 Exit Gate

Active Legal Entity and permission/capability context correctly controls visible Accounting experience.

---

# 621. AC03 — Goal

Prevent financial go-live with broken configuration.

---

# 622. AC03 Implement

Accounting Readiness query:

```text
CoA
Journals
Periods
Base Currency
Required mappings
Tax mappings
Control accounts
Inventory mappings if active
Opening/go-live state
```

---

# 623. AC03 Exit Gate

Readiness explains every missing configuration with actionable link.

---

# 624. AC04 — Goal

Provide safe accountant-grade Chart of Accounts administration.

---

# 625. AC04 Must Support

```text
Tree/list
Search code/name
Create
Edit safe metadata
Archive
History
Classification
Control flags
Dimension policy navigation
```

---

# 626. AC04 Guard

Used account semantic type cannot be casually changed.

---

# 627. AC04 Tests

```text
cycle prevention
duplicate code
archive used account
historical visibility
control account guard
RLS
```

---

# 628. AC04 Exit Gate

Accountant can understand/manage CoA without touching database/config files.

---

# 629. AC05 — Goal

Production-grade Manual Journal.

---

# 630. AC05 Must Support

```text
Draft
Lines
Debit/Credit total
Account picker
Description
Reference
Dimensions
Attachments
Preview
Post
```

---

# 631. AC05 Small Default

```text
Draft → Post
```

---

# 632. AC05 Advanced Optional

```text
Draft → Approve → Post
```

---

# 633. AC05 Tests

```text
balance
period
account
control account
dimensions
concurrency
idempotency
permissions
SoD
```

---

# 634. AC05 Exit Gate

A real accountant can post an accrual/reclassification in minimal steps and trace result.

---

# 635. AC06 — Goal

Make automatic accounting explainable.

---

# 636. AC06 Build

```text
Posting Preview
Source Trace
Why This Account
Why This Amount
Rule/Mapping Version display
```

---

# 637. AC06 Exit Gate

For an Invoice-generated JE, accountant reaches Invoice and mapping explanation in a few clicks.

---

# 638. AC07 — Goal

Manage Account Determination safely.

---

# 639. AC07 Build

```text
Role mappings
Version/effective state
Specificity display
Conflict detection
Simulator
Configuration health
```

---

# 640. AC07 Exit Gate

Missing/conflicting mapping can be diagnosed before real transaction posting.

---

# 641. AC08 — Goal

Expose periods/locks simply but correctly.

---

# 642. AC08 Build

```text
Period list
Open/Closed state
Close preview entry point
Reopen
Lock explanation
Posting date validation feedback
```

---

# 643. AC08 Tests

Concurrent close/post race.

---

# 644. AC08 Exit Gate

Closed period is enforced in backend for every source module.

---

# 645. AC09 — Goal

Fast, traceable General Ledger.

---

# 646. AC09 Build

```text
Account/date filters
running balance
source refs
dimensions
currency evidence
cursor paging
drilldown
```

---

# 647. AC09 Exit Gate

Millions-line test meets measured target without full-table application processing.

---

# 648. AC10 — Goal

Accountant-grade Trial Balance.

---

# 649. AC10 Build

```text
Opening
Period Debit/Credit
Closing
Totals
Drilldown
Export
```

---

# 650. AC10 Integrity

Debit/Credit totals reconcile.

---

# 651. AC10 Exit Gate

Trial Balance accepted against known golden dataset.

---

# 652. AC11 — Goal

Make financial reports interactive at scale.

---

# 653. AC11 Build/Verify

Ledger balance projections/read model.

---

# 654. AC11 Rule

Projection is rebuildable, never canonical independent truth.

---

# 655. AC11 Exit Gate

Projection rebuild matches Ledger aggregation exactly.

---

# 656. AC12 — Goal

Accountant AR workspace using Subledger.

---

# 657. AC12 Build

```text
AR total
Aging
Customer balances
Open items
Overdue
Credits/advances
Drilldown
```

---

# 658. AC12 Exit Gate

No local Accounting customer-balance table.

---

# 659. AC13 — Goal

AP workspace using Subledger.

---

# 660. AC13 Build

```text
AP total
Supplier aging
Due soon
Overdue
Open items
Credits/advances
Payment holds
```

---

# 661. AC13 Exit Gate

No local Accounting supplier-balance truth.

---

# 662. AC14 — Goal

Statements and historical aging.

---

# 663. AC14 Build

```text
Customer statement
Supplier statement
As-of aging
PDF/Excel
Currency modes
```

---

# 664. AC14 Exit Gate

Statement closing equals Subledger balance.

---

# 665. AC15 — Goal

Prove AR/AP = GL.

---

# 666. AC15 Build

```text
AR reconciliation
AP reconciliation
diagnostic breakdown
history/run metadata
```

---

# 667. AC15 Exit Gate

Injected test differences are correctly identified, not merely flagged.

---

# 668. AC16 — Goal

Prove Inventory Valuation = GL Inventory.

---

# 669. AC16 Build

Inventory reconciliation composition + diagnostics.

---

# 670. AC16 Exit Gate

Known inventory valuation mismatch traces to source movement/posting.

---

# 671. AC17 — Goal

Accounting Health becomes accountant's exception queue.

---

# 672. AC17 Build

Cards/status:

```text
Ledger
AR
AP
Inventory
Posting failures
Mappings
Periods
```

---

# 673. AC17 Exit Gate

Healthy system shows minimal noise; broken system gives actionable diagnostics.

---

# 674. AC18 — Goal

Flexible financial statements without hardcoded account ranges.

---

# 675. AC18 Build

```text
Definition
Sections
Lines
Mappings
Versioning
Subtotals
Validation
Unmapped check
```

---

# 676. AC18 Exit Gate

Two different CoA numbering schemes produce same semantic statement structure via mapping.

---

# 677. AC19 — Goal

Production P&L.

---

# 678. AC19 Build

```text
Period activity
YTD
comparative optional
drilldown
dimension filter
export
```

---

# 679. AC19 Exit Gate

Golden company P&L matches expected result exactly.

---

# 680. AC20 — Goal

Production Balance Sheet.

---

# 681. AC20 Build

```text
As-of balances
classified sections
current result/equity presentation
drilldown
```

---

# 682. AC20 Exit Gate

Assets = Liabilities + Equity.

---

# 683. AC21 — Goal

Owner understands money without becoming accountant.

---

# 684. AC21 Build

```text
Cash/Book Bank
Receivables
Payables
Inventory
Revenue
Expenses
Profit
Attention items
```

---

# 685. AC21 Rule

Each card uses canonical source and correct AsOf/freshness semantics.

---

# 686. AC21 Exit Gate

Owner Financial Visibility Test passes.

---

# 687. AC22 — Goal

Month close becomes safe and simple.

---

# 688. AC22 Build

Capability-aware close checklist + blockers + close command.

---

# 689. AC22 Small Experience

```text
Everything reconciled
[Close Month]
```

---

# 690. AC22 Advanced Experience

Detailed checklist/approvals.

---

# 691. AC22 Exit Gate

No race permits posting into period after successful close.

---

# 692. AC23 — Goal

Safe corrections.

---

# 693. AC23 Build

```text
Reverse Journal
Reason
Date policy
Auto reversal metadata
Links
```

---

# 694. AC23 Exit Gate

Original remains immutable and history readable.

---

# 695. AC24 — Goal

Make multi-currency entries understandable.

---

# 696. AC24 Build

Transaction/base amounts, rate snapshot/source, FX trace.

---

# 697. AC24 Exit Gate

Historical rates remain stable.

---

# 698. AC25 — Goal

Controlled period-end FX revaluation.

---

# 699. AC25 Build

```text
Eligible items
Rate
Preview
Difference
Posting
Reversal policy
History
```

---

# 700. AC25 Exit Gate

Golden FX vectors pass and original transactions remain unchanged.



# 701. AC26 — Goal

Go live with correct opening books.

---

# 702. AC26 Build

```text
Opening import
Validation
Preview
Balanced check
AR/AP opening integration
Inventory opening integration
Migration reconciliation
Go-live approval
Lock
```

---

# 703. AC26 Exit Gate

Legacy/TODIR opening reconciliation shows zero unexplained difference or explicit approved migration exception.

---

# 704. AC27 — Goal

Small-company Managed Ledger works without accounting complexity.

---

# 705. AC27 Build

```text
CoA template selection
Journals
Mappings
Periods
Basic statements
Automatic accounting
Simple Accounting Home
```

---

# 706. AC27 Exit Gate

A new small company can invoice/pay/buy/stock and get correct reports without manually configuring debit/credit logic.

---

# 707. AC28 — Goal

Unlock Full Accounting without data migration.

---

# 708. AC28 Build

Mode/capability exposure:

```text
CoA admin
Mappings
Manual Journals
Dimensions
Close
Revaluation
Advanced reports
```

---

# 709. AC28 Exit Gate

Same historical Journal Entries/balances before and after mode change.

---

# 710. AC29 — Goal

Production authorization, SoD and audit.

---

# 711. AC29 Required Tests

```text
Owner summary permission
Accountant posting
Auditor read-only
Mapping admin
Period reopen
Manual Journal SoD
Control-account exception
Cross-entity scope
```

---

# 712. AC29 Exit Gate

No financial authority is controlled only by hidden frontend actions.

---

# 713. AC30 — Goal

Accountant-efficient import/export/report delivery.

---

# 714. AC30 Build

```text
CoA import
Manual Journal import
Trial Balance Excel
GL export
Financial statement PDF/Excel
Party statement PDF/Excel
Large export jobs
```

---

# 715. AC30 Exit Gate

Imports validate before posting and exports retain correct numeric data.

---

# 716. AC31 — Goal

Prove scale and concurrency.

---

# 717. AC31 Load Tests

At minimum:

```text
1M+ Journal Lines
large Trial Balance
large GL account
100k Open Items
historical aging
many concurrent source postings
concurrent Journal posting
close-vs-post race
large reconciliation
financial statement drilldowns
```

Scale upward to target customer evidence.

---

# 718. AC31 Query Plan Review

Capture real PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` evidence for hot report queries in performance environment.

---

# 719. AC31 No Premature Partitioning

Partition Journal tables only after measured evidence/ADR.

---

# 720. AC31 Exit Gate

Measured targets met or documented with evidence-based optimization ADR.

---

# 721. AC32 — Goal

Security and red-team acceptance.

---

# 722. AC32 Must Attack

```text
Cross-tenant reads
Cross-entity reads
Forged account IDs
Forged dimensions
Closed-period bypass
Direct control-account post
Double-post retry
Unauthorized reversal
Unauthorized period reopen
Unauthorized mapping change
Export leakage
```

---

# 723. AC32 Exit Gate

RLS + application authorization + architecture controls all pass.

---

# 724. AC33 — Goal

Operations team can recover without corrupting books.

---

# 725. AC33 Runbooks

Required:

```text
Missing account mapping
Failed financial operation
AR/AP reconciliation difference
Inventory reconciliation difference
Balance projection mismatch
Period accidentally closed
Approved reopen
Opening import failure
Opening reconciliation difference
FX revaluation issue
Outbox backlog
Restore/DR validation
```

---

# 726. AC33 Forbidden Runbook

Never:

```text
UPDATE journal_entry_line ...
UPDATE account_balance ...
UPDATE customer_balance ...
```

as ordinary support fix.

---

# 727. AC33 Exit Gate

Every critical failure has supported diagnostic/corrective path.

---

# 728. AC34 — Accountant Acceptance Test

This is a formal Product Release Gate.

Use a finance/accounting reviewer who did not build the feature.

---

# 729. AC34 Scenario A — Understand the Company

Reviewer opens Accounting and identifies within minutes:

```text
Cash/Bank book balance
AR
AP
Inventory value if active
P&L
financial health
```

---

# 730. AC34 Scenario B — Trace Revenue

```text
P&L
→ Revenue line
→ Account
→ JE
→ Invoice
```

Reviewer explains source without developer help.

---

# 731. AC34 Scenario C — Trace Supplier Payable

```text
AP
→ Supplier
→ Open Item
→ Supplier Invoice
→ JE
```

---

# 732. AC34 Scenario D — Manual Accrual

Accountant creates balanced accrual Journal and posts it in minimal steps.

---

# 733. AC34 Scenario E — Correction

Accountant reverses incorrect manual Journal correctly without editing posted history.

---

# 734. AC34 Scenario F — Reconciliation

Reviewer runs:

```text
AR
AP
Inventory
```

reconciliations and diagnoses seeded discrepancy.

---

# 735. AC34 Scenario G — Month Close

Accountant sees blocker, fixes/understands it, and closes month.

---

# 736. AC34 Scenario H — Reports

Reviewer produces:

```text
Trial Balance
P&L
Balance Sheet
GL
Customer Statement
Supplier Statement
```

without developer support.

---

# 737. AC34 Pass Criteria

Reviewer confirms:

```text
numbers understandable
navigation predictable
no unnecessary workflow
source trace obvious
errors actionable
reports trusted
```

Any trust-breaking confusion is release-blocking until reviewed.

---

# 738. AC35 — Owner Financial Visibility Test

Use non-accountant business-owner persona.

---

# 739. AC35 Questions

Owner must answer:

```text
How much do customers owe me?
How much do I owe suppliers?
How much inventory value do I have?
What is my profit this period?
What is my book cash/bank position?
What needs my attention?
```

---

# 740. AC35 Pass Criteria

No Chart of Accounts knowledge required.

No debit/credit knowledge required.

---

# 741. AC35 Truth Criteria

Each card must reconcile to canonical domain source.

---

# 742. AC36 — Cross-Module End-to-End Financial Acceptance

This is the most important technical/business gate.

---

# 743. AC36 Scenario — Sale

```text
Quotation
→ Customer Invoice
→ AR Open Item
→ Ledger
→ Payment
→ Settlement
```

Verify:

```text
Revenue
Tax
AR
Bank/Cash
Open amount
GL/Subledger reconciliation
```

---

# 744. AC36 Scenario — Purchase

```text
Purchase Order
→ Supplier Invoice
→ AP Open Item
→ Ledger
→ Payment
→ Settlement
```

Verify AP and expense/inventory accounting.

---

# 745. AC36 Scenario — Stock Purchase/Issue

```text
Receipt
→ Inventory Value
→ GL Inventory/GRNI
→ Issue
→ COGS
```

Verify Inventory = GL.

---

# 746. AC36 Scenario — Return/Credit

Verify original source remains and corrective facts reverse correct amounts.

---

# 747. AC36 Scenario — Multi-Currency

```text
Foreign Invoice
→ Historical rate
→ Payment later rate
→ Realized FX
```

exact.

---

# 748. AC36 Scenario — Closed Period

Source modules cannot bypass period lock.

---

# 749. AC36 Scenario — High Concurrency

Duplicate source requests cannot duplicate postings/open items/payments.

---

# 750. AC36 Exit Gate

All cross-module financial invariants pass in Testcontainers real PostgreSQL integration tests.

---

# 751. AC37 — Release Gate

Accounting cannot ship because UI "looks complete."

It ships after trust evidence.

---

# 752. AC37 Required Sign-Off

```text
Product Architect
Senior Accountant / Finance Domain Reviewer
Backend Lead
Frontend Lead
Ledger/Subledger Owner
Inventory Finance Owner
Security Reviewer
Performance Engineer
QA Lead
```

---

# 753. AC37 Mandatory Evidence

```text
Automated test report
RLS/security report
Reconciliation report
Golden financial vectors
Performance benchmark report
Accountant Acceptance results
Owner Visibility results
Migration/opening test
DR/recovery financial validation
Known limitations
```

---

# 754. Implementation Agent Task Template

Every AC phase should be handed to the coding Agent using:

```text
TASK

BUSINESS GOAL
Why this matters to the accountant/owner.

CONTEXT
Which existing TODIR modules/contracts already own the truth.

ARCHITECTURE RULES
Non-negotiable boundaries/invariants.

FILES TO READ
Exact AGENTS.md / canonical docs / affected source files.

FILES ALLOWED TO MODIFY

DO NOT MODIFY
Especially Ledger/Subledger implementation unless phase explicitly requires an approved kernel gap.

IMPLEMENTATION REQUIREMENTS

DATABASE REQUIREMENTS

SECURITY REQUIREMENTS

PERFORMANCE REQUIREMENTS

UX REQUIREMENTS

TESTS REQUIRED

ACCEPTANCE CRITERIA

EXIT GATE

REPORT BACK WITH
- files changed
- migrations
- tests/results
- performance evidence
- unresolved decisions
- risks
```

---

# 755. Agent Report Quality

"Implemented Accounting screen" is not acceptable.

Agent must report:

```text
which canonical source each number uses
which invariant tests prove correctness
which permissions enforce actions
which query plans support performance
```

---

# 756. Agent Must Use Real PostgreSQL Integration Tests

Financial database behavior must not be validated only with in-memory providers.

Use Testcontainers PostgreSQL.

---

# 757. Agent Must Test RLS Under Pooled Connections

Tenant isolation cannot rely on assumptions.

---

# 758. Agent Must Use Shared Transaction Coordinator

For cross-module atomic financial paths already approved.

---

# 759. Agent Must Not Introduce Second Transaction Strategy

No broker choreography for immediate financial invariants.

---

# 760. Agent Must Not Introduce Redis To "Speed Accounting"

Not without measured evidence/ADR.

---

# 761. Agent Must Not Introduce New Accounting Math

Reuse FinBase.

---

# 762. Agent Must Not Introduce Generic Rule DSL

Account mappings/report definitions remain typed/constrained.

---

# 763. Agent Must Not "Fix" Governance Casually

If canonical docs conflict:

identify conflict and propose ADR.

Do not silently redesign finance.

---

# 764. Agent Can Fix Clear Implementation Defects

Examples:

```text
missing RLS policy
missing architecture test
N+1 query
incorrect permission check
broken idempotency
```

when solution follows locked architecture.

---

# 765. Agent Must Preserve Naming

All new technical names use:

```text
Todir.*
```

No new `Erp.*`.

---

# 766. Accounting Namespace Examples

```text
Todir.Modules.Accounting.Domain
Todir.Modules.Accounting.Application
Todir.Modules.Accounting.Infrastructure
Todir.Accounting.Contracts
```

---

# 767. Build Now — MUST

```text
Accounting Home / Health
Owner Financial View
CoA management UX
Manual Journal
Posting Preview
Source Trace
Account Mapping administration
Periods/Locks
General Ledger
Trial Balance
AR/AP workspaces
Party Statements
Historical Aging
AR/AP Reconciliation
Inventory Reconciliation
P&L
Balance Sheet
Financial Statement Mapping
Period Close
Reversal
Multi-Currency trace
FX Revaluation
Opening Balances
Managed Ledger setup
Managed→Full Accounting
Permissions/SoD
Audit
Import/Export
Performance
RLS/Security
Runbooks
Accountant Acceptance
Owner Visibility Acceptance
Cross-module financial E2E
```

---

# 768. Build Later / Evidence-Based

```text
Group Consolidation
Complex Intercompany Elimination
Multiple GAAP books
Additional Reporting Currency
Full formal Cash Flow statement if not release-MVP
Advanced Budgeting
Advanced Treasury
Automated Bank APIs
Advanced Financial Planning
Complex allocation engines
Advanced management accounting
AI finance copilot
Regulatory multi-book reporting
```

Design seams now; do not delay trustworthy core.

---

# 769. Product Configuration Matrix

Company can choose:

```text
Accounting Mode         Managed / Full
Manual Journal Approval Disabled / RiskBased / Always
SoD                     Disabled / Enabled
Dimensions              Minimal / Selected / Advanced
Close Checklist         Simple / Advanced
FX                      Hidden if unused / Enabled
Reconciliation          Automatic health + advanced workbench
Owner View              Enabled by permission
```

---

# 770. Recommended Small Company Defaults

```text
Managed Ledger          ON
Full CoA Admin          hidden
Automatic postings      ON
Manual Journal          simple
Approval                OFF
SoD                     OFF
Dimensions              minimal
Reconciliation health   ON
P&L / Balance Sheet     ON
Owner Financial View    ON
Month Close             simple
```

---

# 771. Recommended Large Company Capabilities

Enable as needed:

```text
Full Accounting
Advanced mappings
Multiple journals
Dimensions
Manual Journal approval
SoD
Close checklist
FX revaluation
Advanced reconciliation
Auditor role
```

---

# 772. No Separate Files For Accounting Subfeatures

Do **not** create separate architecture packs for:

```text
Chart of Accounts
Manual Journals
Trial Balance
General Ledger
Financial Statements
Period Close
Reconciliation
Opening Balances
FX Revaluation
```

They are all part of `16 Accounting`.

---

# 773. Final Source-of-Truth Matrix

```text
Financial primitives / FX / rounding     → FinBase
General Ledger / CoA / JE / Period       → Ledger
AR/AP / Open Items / Settlement          → Subledger
Invoice                                  → Invoicing
Payment                                  → Payments
Inventory quantity/value                 → Inventory
Tax calculation                          → Tax
Accounting UX / controls / reports       → Accounting
```

---

# 774. Final Accountant Flow — Daily

```text
Accounting Home
→ Review exceptions
→ Journal only when necessary
→ Reconcile
→ Analyze
→ Report
```

Automatic business postings require no duplicate manual action.

---

# 775. Final Accountant Flow — Month End Small Company

```text
Accounting Health
→ Everything reconciled
→ Review P&L / Balance Sheet
→ Close Month
```

---

# 776. Final Accountant Flow — Month End Large Company

```text
Close Workbench
→ AR
→ AP
→ Inventory
→ Bank
→ Tax
→ Accruals
→ FX
→ Manual Journals
→ Review Statements
→ Approve
→ Close
```

Only enabled checks appear.

---

# 777. Final Owner Flow

```text
Owner Finance
→ Cash/Bank
→ Receivables
→ Payables
→ Inventory
→ Profit
→ Attention
```

No accounting workflow required.

---

# 778. Final Trust Requirement

If Accountant finds a number that cannot be traced/explained/reconciled:

that is a product defect, not "normal ERP complexity."

---

# 779. Final Simplicity Requirement

If a small-company accountant needs five approvals/screens for a normal task that can safely be done in one action:

that is a product defect.

---

# 780. Final Control Requirement

If a large-company finance team cannot add control/SoD/reconciliation without changing the accounting core:

that is an architecture defect.

---

# 781. Final Financial Integrity Requirement

TODIR must never trade financial correctness for apparent workflow speed.

Speed comes from:

```text
automation
defaults
orchestration
progressive disclosure
batching
read models
good UX
```

not bypassing accounting controls.

---

# 782. Final Product Verdict

TODIR Accounting is not primarily a "journal screen."

It is the product layer that proves to the accountant and owner:

```text
The company's money is understood.
The books are balanced.
The subledgers reconcile.
The inventory reconciles.
The reports come from the same truth.
Every number can be traced.
Errors cannot be silently hidden.
Small companies are not punished with complexity.
Large companies are not blocked by simplicity.
```

---

# 783. Final Commercial Verdict

The Accounting module should be strong enough that an accountant evaluating TODIR can recommend buying the ERP because the financial core feels trustworthy, familiar, fast and explainable.

That is a formal product objective, not marketing copy.

---

# 784. Final Engineering Verdict

Accounting is release-ready only when:

```text
No duplicate accounting truth exists.
Ledger remains canonical GL.
Subledger remains canonical AR/AP.
Every posted JE balances.
Posted history is immutable.
Source idempotency prevents duplicate posting.
Account determination is deterministic/versioned.
Missing mappings block instead of hiding errors.
Control accounts are protected.
Periods are enforced backend-wide.
Trial Balance is exact and fast.
P&L/Balance Sheet derive from Ledger mappings.
AR/AP reconcile to GL.
Inventory reconciles to GL when active.
FX history is reproducible.
Opening migration reconciles before go-live.
Owner dashboard uses canonical sources.
Accountant can trace every material number.
Simple-company workflow stays simple.
Enterprise controls remain optional but available.
Accountant Acceptance Test passes.
Owner Financial Visibility Test passes.
Cross-module financial E2E passes.
```

---

# 785. Final Agent Rule

The Agent should always ask of every implementation decision:

> **Which financial truth does this come from, what invariant protects it, how will the accountant understand it, and how will we prove it is correct?**

If the code cannot answer those four questions, the implementation is not complete.

---

# 786. Next Module Planning Rule

After this file, continue one comprehensive file per actual business module.

Do not split accounting into further packs.

Do not rewrite Ledger/Subledger foundations in later modules.

---

**END OF FILE — 16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
