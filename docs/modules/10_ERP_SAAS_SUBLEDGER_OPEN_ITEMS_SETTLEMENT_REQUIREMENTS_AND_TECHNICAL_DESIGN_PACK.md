# 10 — TODIR ERP SaaS Subledger / Open Items / Settlement Requirements & Technical Design Pack

**File:** `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + AR/AP INTEGRITY + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Product Architect, Accounting Lead, AR/AP Lead, Senior Accountant Reviewer, Backend/Frontend Leads, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL** — Subledger errors directly affect customer/supplier balances, payment application, aging, credit exposure, FX settlement, collections and GL reconciliation.

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

The TODIR Subledger is the canonical operational financial record for:

```text
Who owes whom?
For which document?
How much was originally due?
How much has been settled?
How much remains?
When is it due?
Which payments/credits/advances were applied?
What is still unapplied?
```

The core rule is:

> **Ledger owns account-level accounting truth. Subledger owns party/open-item/settlement truth.**

And:

> **A payment movement and a payment allocation are different concepts.**

The bank/cash movement can already be posted while the amount remains unapplied.

---

# 1. Central Business Question

The Subledger must answer:

> **For a Party within a Legal Entity, what receivable/payable obligations exist, how were they settled over time, what remains open, and does the result reconcile exactly to the corresponding General Ledger control accounts?**

---

# 2. General Ledger vs Subledger

General Ledger:

```text
Accounts Receivable Control = 5,000,000 EGP
```

Subledger:

```text
Customer A = 100,000
Customer B = 350,000
Customer C = 42,500
...
```

The numbers must reconcile, but the models have different purposes.

---

# 3. What Subledger Owns

```text
PartyFinancialProfile
OpenItem
OpenItemInstallment / Due Item
Settlement
Allocation
Application / Unapplication History
Advance / Unapplied Party Amount
Credit Balance
WriteOff Request/Result
AR/AP Exposure Core
Aging
Party Statement read models
Control Reconciliation
Settlement FX context
```

---

# 4. What Subledger Does NOT Own

```text
Invoice document             → Invoicing
Vendor Bill document         → Invoicing/Purchasing
Payment movement             → Payments
Bank transaction             → Payments/Banking
GL Journal Entry             → Ledger
Tax calculation              → Tax
Price calculation            → Pricing
Party identity               → Directory
Payment Term definition      → FinBase
Collections cases/dunning    → Collections
```

---

# 5. AR and AP in One Domain

The engine should support:

```text
Receivable
Payable
```

with shared open-item/settlement mechanics.

Do not implement two entirely separate engines.

---

# 6. Receivable vs Payable Direction

Every Open Item has explicit direction:

```text
Receivable
Payable
```

Do not infer from sign alone.

---

# 7. Open Item

An Open Item represents a financial obligation or credit associated with a Party and source.

Examples:

```text
Customer Invoice
Supplier Bill
Customer Credit Note
Supplier Credit Note
Advance
Debit/Credit Adjustment
Opening Outstanding Item
```

---

# 8. Open Item Is Not the Invoice

Invoice owns:

- legal/commercial document;
- lines;
- tax;
- pricing;
- status.

Subledger owns:

- outstanding financial obligation derived from it.

---

# 9. Open Item Creation Flow

Example:

```text
Invoice Finalized
    ↓
Payment Schedule Snapshot
    ↓
Subledger Open Items
    ↓
Accounting Fact
    ↓
Ledger
```

---

# 10. One Invoice Can Create Multiple Open Items

Invoice:

```text
100,000 EGP
```

Payment Term:

```text
30% immediate
40% after 30 days
30% after 60 days
```

Subledger creates:

```text
30,000 due today
40,000 due +30
30,000 due +60
```

Each due item is independently ageable/settleable.

---

# 11. Why Installment-Level Open Items Matter

Without them:

- Aging is wrong.
- Collection priority is wrong.
- Overdue amount is wrong.
- Partial payment application is ambiguous.

---

# 12. Open Item Core Fields

Concept:

```text
OpenItem
{
    Id
    TenantId
    LegalEntityId

    Direction
    PartyId

    SourceDocumentRef
    SourceDocumentType
    SourceLine/ScheduleRef?

    OriginalTransactionAmount
    TransactionCurrency

    OriginalBaseAmount
    BaseCurrency

    DueDate
    PostingDate
    DocumentDate?

    ControlAccountRole/Ref
    Status

    CreatedAt
    Version
}
```

---

# 13. Open Item Amount Semantics

Maintain distinct:

```text
Original Amount
Settled Amount
Open Amount
```

But Open Amount must be explainable from settlement history.

---

# 14. Open Amount Source of Truth

Recommended:

```text
Original Amount
+
immutable Allocations / Reversals
```

are the historical truth.

A cached `open_amount` may be maintained transactionally for speed.

---

# 15. Cached Open Amount

Allowed:

```text
open_transaction_amount
open_base_amount
```

because it is a performance projection maintained in the same transaction.

It must be reconcilable/rebuildable.

---

# 16. No Hidden Mutable Balance

Never trust a single mutable `customer_balance` without underlying open-item/allocation history.

---

# 17. Open Item Status

Canonical status should be derived/minimal:

```text
Open
PartiallySettled
Settled
Cancelled/Reversed
```

---

# 18. Overdue Is Derived

`Overdue` is not a permanent state.

Derived:

```text
OpenAmount != 0
and DueDate < AsOfDate
```

---

# 19. Aging Is Derived

Likewise:

```text
Current
1–30
31–60
61–90
90+
```

are reporting buckets.

Not OpenItem status.

---

# 20. Dispute Is Separate

Dispute/case management is Collections.

Open Item may carry a lightweight:

```text
CollectionHold / DisputeRef
```

if required.

---

# 21. Settlement

Settlement is the business operation that matches available credit/value against one or more open obligations.

---

# 22. Allocation

An Allocation is one settlement relationship:

```text
Source available amount
→ Target Open Item
→ Allocated Amount
```

---

# 23. Example — Partial Payment

Invoice:

```text
10,000
```

Payment:

```text
4,000
```

Allocation:

```text
PAY-001 → INV-001 = 4,000
```

Result:

```text
Original 10,000
Settled   4,000
Open      6,000
```

---

# 24. Example — One Payment, Multiple Invoices

Payment:

```text
10,000
```

Invoices:

```text
INV-A = 3,000
INV-B = 4,000
INV-C = 8,000
```

Allocation:

```text
3,000 → INV-A
4,000 → INV-B
3,000 → INV-C
```

Remaining on INV-C:

```text
5,000
```

---

# 25. Example — Multiple Payments, One Invoice

Invoice:

```text
10,000
```

Applications:

```text
PAY-1 → 3,000
PAY-2 → 2,000
PAY-3 → 1,500
```

Open:

```text
3,500
```

---

# 26. Payment Movement vs Allocation

Critical:

```text
Payment
```

means money moved.

```text
Allocation
```

means that amount was associated with an obligation.

These are not the same transaction.

---

# 27. Existing ERP Pattern

Current ERPNext reconciliation documentation explicitly separates already-recorded payment movement from later reconciliation/allocation: allocating an existing payment reduces invoice outstanding/unallocated amounts without creating another bank movement.

TODIR locks this as a domain boundary.

---

# 28. Payment Can Be Unapplied

Customer sends 10,000 with no invoice reference.

Payments module records money.

Subledger records available party credit:

```text
Unapplied = 10,000
```

---

# 29. Unapplied Amount

A payment/credit can have:

```text
OriginalAvailable
Allocated
RemainingUnapplied
```

---

# 30. Advance

Advance/prepayment is first-class.

Example:

```text
Customer pays 20,000 before Invoice exists.
```

Subledger keeps:

```text
Customer Advance / Available Credit = 20,000
```

---

# 31. Advance Is Not Revenue

Subledger tracks party credit.

Ledger posting determines accounting effect through Fact/account mapping.

Do not automatically recognize revenue.

---

# 32. Apply Advance Later

Invoice:

```text
15,000
```

Apply:

```text
15,000 from Advance
```

Remaining:

```text
5,000 Advance
```

---

# 33. Supplier Advance

Symmetric AP behavior.

---

# 34. Credit Note

Credit Note creates available credit/open credit item according to source flow.

It can settle invoice obligations without cash movement.

---

# 35. Credit Allocation

```text
Credit Note 2,000
→ Invoice 10,000
```

Remaining invoice:

```text
8,000
```

---

# 36. Debit Note / Supplier Credits

Direction and semantics are explicit.

Do not use signs only.

---

# 37. Available Credit Item

Concept:

```text
SettlementSource
```

can represent:

- Payment.
- Advance.
- Credit Note.
- Adjustment.
- Opening credit.

---

# 38. Settlement Source Is a Stable Reference

Subledger stores:

```text
SourceType
SourceRef
OriginalAvailableAmount
Currency
```

not Payments implementation navigation.

---

# 39. Overpayment

Invoice:

```text
8,000
```

Payment:

```text
10,000
```

Apply:

```text
8,000
```

Remaining:

```text
2,000 Unapplied Credit
```

---

# 40. Underpayment

Normal partial settlement.

No special hack.

---

# 41. Write-Off

Write-off is an explicit financial operation.

Example:

```text
Open = 0.03
Approved write-off = 0.03
```

---

# 42. Write-Off Is Not Rounding

Do not silently clear small balances.

Write-off requires:

```text
Policy
Permission
Reason
Accounting Fact
Audit
```

---

# 43. Write-Off Policy

Potential:

```text
Max automatic suggestion amount
Allowed currency
Role limit
Reason code
Account role
Approval threshold
```

---

# 44. Auto Write-Off Default

Disabled by default.

Can be configured later.

---

# 45. Payment Tolerance

Tolerance is related but separate.

Example:

```text
Invoice 100.03
Payment 100.00
```

Policy can allow settlement with:

```text
0.03 tolerance/write-off
```

but the financial difference remains explicit.

---

# 46. Settlement Source vs Target

Generic model:

```text
SettlementSource
→ Allocation
→ OpenItem
```

---

# 47. Allocation Core Fields

```text
Allocation
{
  Id
  TenantId
  LegalEntityId

  SettlementId
  SourceRef
  TargetOpenItemId

  TransactionAmount
  TransactionCurrency

  TargetCurrencyAmount?

  BaseAmount

  SettlementDate
  ApplicationDate

  FxSnapshot?
  Status

  CreatedBy
  CreatedAt
  ReversalOfAllocationId?
}
```

---

# 48. Settlement Header

```text
Settlement
{
  Id
  TenantId
  LegalEntityId

  PartyId
  Direction

  SettlementDate
  PostingDate?

  SourceType
  SourceRef

  Status
  PolicyVersion

  CreatedBy
  CreatedAt
}
```

---

# 49. One Settlement, Many Allocations

Payment can create one Settlement with many Allocation lines.

---

# 50. Allocation Invariant

For one source:

```text
SUM(active allocations)
<= available source amount
```

---

# 51. Target Invariant

For one Open Item:

```text
SUM(active applied amounts)
<= amount available to settle
```

subject to signed credit/debit semantics.

---

# 52. No Over-Allocation

Must fail transactionally.

---

# 53. Concurrency Problem

Two finance users can attempt to settle same invoice simultaneously.

This must not create:

```text
negative Open Amount
```

---

# 54. Concurrency Control

Use:

- optimistic concurrency/version;
- row locking where necessary;
- transaction;
- re-validation before commit.

For hot allocation:

```text
SELECT ... FOR UPDATE
```

or equivalent can be justified.

---

# 55. Settlement Atomicity

All allocations in one confirmed settlement either:

```text
commit
```

or:

```text
rollback
```

as the operation requires.

---

# 56. Source Availability Concurrency

Lock/validate source available balance and target open balances consistently to avoid double application.

---

# 57. Deadlock Strategy

Acquire locks in deterministic order:

```text
Source IDs sorted
then Target OpenItem IDs sorted
```

where possible.

Retry transient deadlocks safely.

---

# 58. Allocation Idempotency

Settlement command has idempotency key.

Double click/retry cannot allocate twice.

---

# 59. Allocation Mutation

Posted/confirmed allocation is not silently edited.

---

# 60. Unapply / Reverse Allocation

Wrong allocation:

```text
Unapply / Reverse Allocation
```

creates explicit reversal history.

---

# 61. Allocation Reversal

Concept:

```text
Original Allocation
+
Reversal Allocation
```

or immutable application event model.

Do not delete original evidence.

---

# 62. Why Unapply Must Preserve History

Finance needs to answer:

```text
Who applied this payment?
Who later reversed it?
Why?
When?
```

---

# 63. Current ERP Pattern

Business Central and ERPNext both expose apply/unapply or reconcile/unreconcile operations, validating application history as a separate business operation. citeturn496724search6turn244730search3

---

# 64. Allocation Date

Keep explicit:

```text
PaymentDate
SettlementDate
ApplicationDate
PostingDate
```

Do not merge all into one `Date`.

---

# 65. Settlement Date

Economic matching date.

---

# 66. Posting Date

Ledger accounting date if the settlement creates accounting effects such as:

- FX;
- write-off;
- tolerance.

---

# 67. Payment Date

Owned by Payments document.

---

# 68. Application Date

Can be used for reconciliation/history when allocation performed later.

---

# 69. Party Boundary

Allocation normally occurs within same:

```text
Legal Entity
Party
Direction/control context
```

unless explicit netting process.

---

# 70. Customer/Supplier Same Party

One Party can have:

```text
Customer role
Supplier role
```

but AR and AP do NOT automatically net.

---

# 71. Why No Automatic Netting

Netting can have:

- contractual implications;
- tax implications;
- legal implications;
- accounting requirements.

Requires explicit process.

---

# 72. Future Set-Off / Netting

Design seam:

```text
PartyNettingTransaction
```

can settle AR and AP with explicit authorization/accounting facts.

Not MVP automatic behavior.

---

# 73. Cross-Party Allocation

Default prohibited.

---

# 74. Cross-Entity Allocation

Prohibited.

Intercompany is separate process.

---

# 75. Party Merge

Directory Party merge must safely redirect/reference Subledger items.

Do not rewrite historical source party evidence blindly.

---

# 76. Party Merge Strategy

Future/implementation:

- canonical resolver;
- `PartyMerged` event;
- active operational search uses canonical Party;
- historical snapshot preserves original reference where needed.

---

# 77. PartyFinancialProfile

Subledger owns core financial relationship profile:

```text
PartyFinancialProfile
{
  TenantId
  LegalEntityId
  PartyId

  DefaultPaymentTermId?
  CreditLimit?
  CreditPolicy?
  ReceivablePolicy?
  PayablePolicy?

  StatementPreference?
  DunningIntegrationPolicy?

  Status
  Version
}
```

---

# 78. Why Key = Party × Legal Entity

Same Party can have different:

- payment terms;
- credit limits;
- control policies;
- commercial relationship

per Legal Entity.

---

# 79. Default Payment Term

Definition is FinBase-owned.

Profile references it.

---

# 80. Invoice Snapshot Wins

Changing Customer default from Net 30 to Net 15 does not change existing Open Items.

Subledger uses Invoice payment-schedule snapshot.

---

# 81. Supplier Default Payment Term

Same model on AP side.

---

# 82. Credit Limit

Profile can contain:

```text
CreditLimit
Currency / base policy
```

Exact currency semantics explicit.

---

# 83. Credit Limit Is Not Customer Balance

Balance/exposure is dynamic.

Limit is policy/config.

---

# 84. Credit Exposure

Subledger owns core posted financial exposure:

```text
Open Receivables
minus eligible unapplied credits
plus/minus configured components
```

---

# 85. Sales Exposure Extension

Sales may add:

```text
Confirmed Orders
Unbilled Deliveries
```

to projected exposure.

Subledger should provide core exposure, not own Sales commitments.

---

# 86. Exposure Contract

```text
IPartyFinancialExposure
```

returns:

```text
PostedOpenExposure
AvailableCredits
OverdueExposure
CreditLimit
AvailableCredit
```

---

# 87. Credit Decision Ownership

Sales decides:

```text
Allow
Warn
Block
Require Approval
```

based on exposure + Sales policy.

---

# 88. Subledger Does Not Block Sales Directly

It provides authoritative financial facts.

---

# 89. Exposure Currency

For one Legal Entity, credit policy usually compares in Base Currency.

Transaction-currency breakdown remains available.

---

# 90. Multi-Currency Open Item

Store:

```text
OriginalTransactionAmount
TransactionCurrency
OriginalBaseAmount
HistoricRateSnapshot
```

---

# 91. Open Transaction Amount

Settlement preserves remaining amount in transaction currency where applicable.

---

# 92. Open Base Amount

Historical base amount can reduce according to proportional/application accounting method.

Needs explicit settlement algorithm.

---

# 93. Multi-Currency Settlement

Invoice:

```text
1,000 USD
```

Payment:

```text
EGP
```

Allowed only with explicit settlement policy and FinBase conversion.

---

# 94. FinBase Ownership

Subledger requests:

```text
Settlement FX Rate
```

from FinBase.

No Subledger FX master.

---

# 95. FX at Settlement

Need to distinguish:

```text
Historic Base Amount of settled portion
Settlement Base Equivalent
Difference
```

---

# 96. Realized FX

Formula concept:

```text
Realized FX
=
Settlement Base Value
-
Historic Base Carrying Value of settled portion
```

sign interpretation by direction/accounting Fact.

---

# 97. Subledger Owns Settlement FX Meaning

Because Subledger knows:

- which Open Item;
- how much was settled;
- historic carrying amount;
- settlement context.

Ledger does not guess allocation.

---

# 98. Ledger Owns FX Posting

Subledger produces:

```text
SettlementFxDifference Fact
```

Ledger maps to Gain/Loss accounts.

---

# 99. Partial FX Settlement

Invoice 1,000 USD.

Settle 400 USD equivalent.

Historic base carrying portion must be deterministic.

Remaining 600 USD keeps residual carrying base.

---

# 100. Carrying Base Allocation

Use explicit proportional/rounding algorithm.

Final settlement absorbs residual so total historic base closes exactly.

---

# 101. FX Rounding Residual

Use FinBase rounding.

Never leave 0.01 ghost balance because proportional base allocation rounded independently.

---

# 102. Final Settlement Rule

When Open Transaction Amount reaches zero:

Open Base Amount must also reach zero, with legitimate difference captured as FX/rounding/write-off fact.

---

# 103. Unrealized FX

Period-end revaluation is not a Settlement.

Ledger/Revaluation reads eligible open foreign items/carrying values.

---

# 104. Revaluation Must Not Change Original Transaction Amount

It changes accounting carrying value via explicit Ledger entries.

Subledger can expose valuation basis/read context.

---

# 105. Revaluation Adjustment State

Need contract so subsequent realized FX uses correct carrying amount according to accounting policy.

Detailed integration finalized with Ledger revaluation implementation.

---

# 106. Do Not Hide Revaluation Complexity in OpenAmount

Transaction OpenAmount remains transactional obligation.

Accounting carrying base can have revaluation layers.

---

# 107. Aging

Aging works on due Open Items.

---

# 108. Aging As-Of Date

Explicit:

```text
AsOfDate
```

No hidden Today in historical reports.

---

# 109. Aging Buckets

Default:

```text
Current
1–30
31–60
61–90
91+
```

Configurable report definition.

---

# 110. Aging By Due Date

Default.

Optional by Document Date for analysis, clearly labeled.

---

# 111. Aging Currency

Reports can show:

- Base currency.
- Transaction currency groups.
- Original currency.

Do not aggregate unlike currencies without conversion/base amount.

---

# 112. Aging Snapshot

No need persist daily aging.

Compute from Open Item due dates/balance read models.

---

# 113. Historical Aging

"AR Aging as of 31 Dec last year" requires historical application state as-of date.

This is critical.

---

# 114. Historical As-Of Requirement

If we only store current `open_amount`, historical aging becomes impossible.

Therefore immutable allocation dates/history are mandatory.

---

# 115. Historical Open Amount Formula

At `AsOfDate`:

```text
Original amount
- allocations effective on/before AsOfDate
+ allocation reversals effective on/before AsOfDate
```

---

# 116. Historical Performance

May require period snapshots/read model later.

Do not sacrifice history correctness.

---

# 117. Party Statement

Customer/Supplier statement includes:

```text
Opening balance
Invoices/Bills
Credit/Debit notes
Payments
Allocations/Applications as appropriate
Adjustments
Closing balance
```

---

# 118. Statement vs Ledger

Statement is party operational financial report.

Do not expose raw GL control-account lines as substitute.

---

# 119. Statement Period

Explicit date range + opening balance as of start.

---

# 120. Statement Currency

Can be:

- Base Currency.
- One transaction currency.
- Separate sections per currency.

---

# 121. Statement Reconciliation

Closing Statement balance should reconcile to Party Subledger balance for same scope/date.

---

# 122. Account Reconciliation

Critical invariant:

```text
AR Subledger Control Total
==
GL AR Control Account
```

and:

```text
AP Subledger Control Total
==
GL AP Control Account
```

for defined scope/as-of.

---

# 123. What Exactly Reconciles

Subledger accounting carrying values, not merely transaction currency open values.

---

# 124. Reconciliation Dimensions

May include:

```text
Legal Entity
Control Account
Currency/Book context
AsOfDate
```

---

# 125. Control Account Reference

Open Item stores/control context needed to reconcile to correct GL control account.

Do not hardcode one AR account globally.

---

# 126. Control Account Changes

If customer/accounting mapping changes:

old items remain associated with historical control account/accounting fact context unless migration/reclassification performed.

---

# 127. New Items Use New Mapping

Account determination version applies at source posting.

---

# 128. Reconciliation Service

```text
ISubledgerControlReconciliation
```

returns:

```text
GL Balance
Subledger Balance
Difference
Status
Diagnostic breakdown
```

---

# 129. Reconciliation Difference Is Critical

No hiding with rounding.

---

# 130. Explainable Difference

Diagnostics can identify:

- Open Item without expected Fact.
- Ledger posting without Subledger item.
- Reversed source mismatch.
- migration imbalance.
- manual control-account posting.

---

# 131. Control Account Guard

`09` blocks unsafe manual GL posting to control accounts by default.

This protects reconciliation.

---

# 132. Reconciliation Schedule

Run:

- on-demand.
- after migration.
- period close.
- nightly/continuous lightweight check where feasible.

---

# 133. Reconciliation Alert

Finance dashboard:

```text
AR Control: Reconciled
AP Control: Reconciled
```

or critical alert.

---

# 134. Collections Boundary

Subledger supplies:

```text
Open items
Due dates
Overdue days
Amounts
Party exposure
Dispute/hold marker
```

Collections owns:

```text
Dunning
Reminder sequences
Promise to Pay
Collection cases
Collector assignment
Contact attempts
```

---

# 135. Promise to Pay

Not an Open Item settlement.

It is Collections commitment until money/credit actually settles.

---

# 136. Dispute

A disputed invoice remains financially open unless legal/accounting process says otherwise.

Collections flag does not make amount zero.

---

# 137. Payment Hold

AP may mark supplier invoice "on hold" for payment proposal.

It remains payable/open.

---

# 138. Payment Proposal Boundary

AP Payment Run later consumes open payable items.

Subledger does not execute bank payment.

---

# 139. Due Supplier Payments

Query:

```text
open AP items
due by date
not on hold
eligible for payment
```

Payments/AP application uses results.

---

# 140. Settlement Policy

Per Legal Entity/direction:

```text
Manual
AutoOldestDue
AutoOldestDocument
ReferenceMatchFirst
```

---

# 141. Auto Allocation

Must be deterministic.

---

# 142. Recommended Default

If user selects auto:

```text
Exact explicit reference
→ exact amount match
→ oldest due
```

but policy should be configurable.

---

# 143. Same Currency First

For multi-currency Party, default matching should prefer same currency to avoid unnecessary FX.

---

# 144. Auto Allocation Constraints

Never allocate:

- across Party;
- across Legal Entity;
- incompatible control context;
- blocked/disputed items if policy forbids;
- beyond amounts.

---

# 145. Auto Match Confidence

Bank/reconciliation automation can suggest based on:

```text
Invoice reference
Amount
Party
Bank memo
Dates
```

---

# 146. AI/ML Match

AI may rank candidates.

It does not become financial truth.

---

# 147. Confirmation Policies

Possible:

```text
High-confidence auto apply
Suggest for review
Manual only
```

configured by risk.

---

# 148. Reference Match

Exact invoice/reference number is strongest candidate.

Normalize safely.

---

# 149. Amount Match

Exact amount alone is not enough in large customer accounts.

Use multiple signals.

---

# 150. Oldest Due

Fallback strategy, not always correct.

User can override if authorized.

---

# 151. Manual Allocation UX

Finance user sees source amount and eligible target items.

---

# 152. Allocation Preview

Before commit:

```text
Source available
Selected invoices
Applied amounts
Remaining source
Remaining open balances
FX impact
Write-off/tolerance impact
```

---

# 153. Allocation Validation Server-Side

Frontend amounts are not authoritative.

Backend revalidates current balances at commit.

---

# 154. Stale Allocation

If another user settled invoice while screen open:

backend returns:

```text
OpenItemChanged
```

UI refreshes.

---

# 155. Allocation Batch

One user can apply a payment to many items.

One transaction if logically one settlement.

---

# 156. Massive Reconciliation

For thousands of entries:

background/batch process with deterministic chunks.

---

# 157. Current ERP Pattern

ERPNext supports queued reconciliation of many eligible entries; this validates need for batch processing rather than only interactive one-row application. citeturn244730search8

---

# 158. Batch Safety

Each settlement unit idempotent.

Progress/resume supported.

---

# 159. Batch Failure

One bad Party/item should not necessarily rollback thousands of unrelated reconciliations.

Chunk by Party/control context.

---

# 160. Opening Subledger Migration

At cutover, import only outstanding items/available credits needed operationally.

---

# 161. Do Not Recreate Full History by Fake Invoices

If historical detail is not required operationally, use opening outstanding items with legacy references.

---

# 162. Opening Customer Item

Fields:

```text
Party
Legacy Document Number
Document Date
Due Date
Currency
Historic Rate
Outstanding Transaction Amount
Outstanding Base Amount
Control Account
Legacy Ref
```

---

# 163. Opening Supplier Item

Same AP equivalent.

---

# 164. Opening Credits/Advances

Import separately as available settlement sources/open credits.

---

# 165. Opening Reconciliation

Before go-live:

```text
Sum opening AR items
==
Opening AR control GL
```

and AP equivalent.

---

# 166. Current ERP Pattern

ERPNext's current opening-balance guidance distinguishes outstanding customer/supplier invoices, unallocated payments/advances, stock, and residual GL balances, and warns against duplicating control-account balances. This validates our migration split. citeturn244730search6turn244730search7

---

# 167. Migration Idempotency

Each imported row/batch has external key/hash.

Rerun does not duplicate.

---

# 168. Legacy Closed History

Can be stored in archive/reporting import later.

Not required in operational Subledger.

---

# 169. Go-Live Boundary

New native transactions start after defined Subledger GoLiveDate.

---

# 170. Opening Item Editing

Before migration approval: Draft import can be corrected.

After approval/posting: use adjustment/reversal, not direct mutation.

---

# 171. Subledger Accounting Fact

Subledger can produce semantic Facts such as:

```text
SettlementFxDifference
SettlementWriteOff
SettlementTolerance
AdvanceApplied if accounting effect required
ControlReclassification
```

---

# 172. Not Every Allocation Creates GL Entry

If payment cash/AR effect was already posted and allocation only links that existing receivable credit to invoice debit:

allocation can be Subledger-only.

---

# 173. Why This Is Important

Do NOT create second:

```text
Dr/Cr Bank
```

just because payment was later reconciled.

---

# 174. Allocation Accounting Effect Cases

Potential effects:

```text
None
Realized FX
Write-off
Cash discount
Tax adjustment
Control reclassification
```

Only those effects create new Accounting Facts.

---

# 175. Source Payment Accounting

Payments owns initial money movement and Fact.

Subledger owns later allocation.

---

# 176. Cash Discount

Early-payment discount qualification belongs settlement context.

FinBase Term definition can contain eligibility seam.

Tax Pack determines tax adjustment.

Ledger posts semantic Facts.

---

# 177. Cash Discount Not MVP Full

Design seam now.

Build full country-specific accounting when required.

---

# 178. Settlement Tax Adjustment

Some discounts/write-offs can change tax.

Subledger asks Tax/consuming correction flow; does not invent tax.

---

# 179. Settlement Write-Off Fact

```text
SettlementWriteOff
{
  Party
  OpenItem
  Amount
  Currency
  Reason
  WriteOffType
  Dimensions
}
```

Ledger maps write-off account.

---

# 180. No Write-Off Account ID in Subledger

Same architecture rule.



# 181. Payment Ledger Pattern

The industry pattern is clear:

```text
Payment exists
Outstanding invoice exists
Reconciliation links them
```

ERPNext explicitly states reconciliation updates references/outstanding balances while leaving the original bank/cash posting intact. citeturn244730search0turn244730search2

TODIR adopts the principle while using its own domain architecture.

---

# 182. Subledger Event Sourcing?

Do NOT adopt full Event Sourcing architecture merely because allocations are historical.

Canonical relational model + immutable allocation rows is sufficient.

---

# 183. Allocation History Model

Prefer:

```text
Settlement
Allocation
AllocationReversal
```

with immutable confirmed rows.

---

# 184. Recomputed State

Current balances can be transactionally cached.

Historical state remains derivable.

---

# 185. Open Item Current State Columns

Potential:

```text
open_transaction_amount
open_base_carrying_amount
settled_transaction_amount
last_settlement_date
is_open
```

maintained atomically.

---

# 186. Why Store Current Open Amount

Hot screens repeatedly need:

```text
Open invoices for Customer X
```

Re-summing millions of Allocation rows each time is unnecessary.

---

# 187. Integrity Check

For each Open Item:

```text
Original
-
Net Active Allocations
==
Cached Open Amount
```

subject to currency/carrying-value semantics.

---

# 188. Background Integrity Scan

Periodically verify sampled/all items as appropriate.

Mismatch = Critical incident.

---

# 189. Balance Projection

Party-level:

```text
PartySubledgerBalance
{
  TenantId
  LegalEntityId
  PartyId
  Direction
  BaseOpenBalance
  BaseAvailableCredit
  OverdueBalance
  OldestDueDate
  CurrencyBreakdown?
}
```

Derived/rebuildable.

---

# 190. Strong Consistency of Party Balance

After settlement commit, balance UI must update immediately.

Update projection in same transaction or derive from updated Open Items.

---

# 191. No Eventually Consistent Credit Limit by Default

Sales credit decision cannot rely on a projection lagging minutes behind if it can cause over-credit.

Use strongly consistent core exposure.

---

# 192. Exposure Read Optimization

Maintain transactionally updated Party financial balance if benchmark justifies.

---

# 193. Exposure Version

Return:

```text
ExposureVersion / AsOfInstant
```

so Sales can detect stale approval calculations.

---

# 194. Credit Reservation

Future Sales can reserve exposure for confirmed Orders.

Do not implement inside Subledger core now.

---

# 195. Credit Limit Currency

Recommended default:

```text
Legal Entity Base Currency
```

because exposure across foreign currencies needs a comparable unit.

---

# 196. Foreign Exposure Conversion

Two concepts:

```text
Accounting carrying exposure
Current estimated exposure
```

Do not mix.

Credit control may choose policy.

---

# 197. Accounting Carrying Exposure

Based on posted carrying/base values.

Strong accounting truth.

---

# 198. Current Market Exposure

Could use current FX for commercial risk.

This is a derived risk view, not accounting balance.

---

# 199. Credit Policy

Potential:

```text
UseAccountingExposure
UseCurrentFxExposure
IncludeOrders
IncludeOverdueWeighting
```

owned by Sales/Credit Control application with Subledger inputs.

---

# 200. Party Statement Read Model

Can optimize:

```text
PartyStatementEntry
```

containing timeline-friendly source references.

Source remains Open Items/Payments/Allocations.

---

# 201. Statement Entry Types

```text
Invoice
Bill
Credit
Payment
Advance
Allocation
WriteOff
Adjustment
```

Presentation may hide allocations if simpler statement convention requires.

---

# 202. Statement Ordering

Stable:

```text
Effective Date
Posting Date
Source Sequence
ID
```

---

# 203. Statement Running Balance

Use SQL window/read model, not application loop over millions.

---

# 204. Statement PDF/Export

Reporting/Template engine handles rendering/export.

Subledger provides data.

---

# 205. Aging Read Model

Potential materialized/derived:

```text
OpenItemAgingProjection
```

But bucket boundaries vary, so avoid storing only fixed buckets.

Store due/open fields; calculate requested buckets efficiently.

---

# 206. Partial Index for Open Items

Highly valuable candidate:

```sql
WHERE is_open = true
```

for:

```text
tenant + entity + party + direction + due_date
```

---

# 207. Closed Items

Historical queries use separate indexes/read paths.

---

# 208. Data Volume

A large tenant can accumulate:

```text
millions of invoices
millions of allocation rows
```

Open-item hot set is usually much smaller.

Architecture must exploit this.

---

# 209. Partitioning

Do not partition Day 1 without measured need.

Potential future partitioning by:

```text
Tenant/Cell
Legal Entity
Time for closed history
```

---

# 210. Archive Strategy

Do NOT move posted history to cold tables casually.

Use database/storage strategy later if measured.

---

# 211. Subledger Schema

Recommended:

```text
finance_subledger
```

---

# 212. Core Tables

```text
party_financial_profile

open_item
open_item_schedule_metadata?

settlement_source
settlement
allocation
allocation_reversal

write_off
subledger_policy

party_balance_projection
control_reconciliation_run
control_reconciliation_detail

opening_subledger_batch
opening_subledger_item
```

---

# 213. `party_financial_profile`

Fields conceptually:

```text
id
tenant_id
legal_entity_id
party_id

default_receivable_payment_term_id?
default_payable_payment_term_id?

credit_limit_amount?
credit_limit_currency?

receivable_policy_code?
payable_policy_code?

status
version
created_at
updated_at
```

---

# 214. Customer/Supplier Code

Do not duplicate customer/supplier identity.

If operational customer/vendor code belongs Sales/Purchasing profile, Subledger references Party.

---

# 215. `open_item`

Concept:

```text
id
tenant_id
legal_entity_id

party_id
direction

source_type
source_ref
source_schedule_ref?

document_date
posting_date
due_date

transaction_currency
original_transaction_amount
open_transaction_amount

base_currency
original_base_amount
open_base_carrying_amount

control_account_ref / role snapshot

historic_fx_snapshot?

status
is_open

source_version
row_version

created_at
closed_at?
```

---

# 216. Open Item Uniqueness

A source schedule line should not create duplicate Open Items.

Unique:

```text
tenant + entity + source_type + source_ref + source_schedule_ref
```

---

# 217. Source Schedule Reference

Invoice installment:

```text
ScheduleLineId / Sequence
```

stable.

---

# 218. `settlement_source`

Concept:

```text
id
tenant_id
legal_entity_id

party_id
direction / credit-side semantics

source_type
source_ref

currency
original_available_amount
remaining_unapplied_amount

base carrying data
historic/settlement context

status
row_version
```

---

# 219. Payment as Settlement Source

Payment module creates/declares source availability through Subledger contract.

Subledger does not own bank account movement.

---

# 220. Credit Note as Source

Invoicing creates credit-source/open-credit semantics.

---

# 221. Advance Source

Payment before invoice creates available amount.

---

# 222. `settlement`

Header:

```text
id
tenant_id
legal_entity_id
party_id
direction

settlement_date
application_date

policy_version
idempotency_key

status

created_by
created_at
confirmed_at
```

---

# 223. Settlement Status

```text
Draft
Confirmed
Reversed
```

Draft can be frontend/application workspace.

Confirmed allocations immutable.

---

# 224. `allocation`

```text
id
tenant_id
legal_entity_id

settlement_id
source_id
open_item_id

source_currency
target_currency

source_amount
target_amount
base_settlement_amount
historic_base_closed_amount

fx_difference_base
rounding_difference_base

settlement_fx_snapshot?

status
sequence

created_at
```

---

# 225. Cross-Currency Allocation Needs Two Amounts

If currencies differ:

```text
Source Amount
Target Amount
```

are distinct.

Do not overload one decimal.

---

# 226. Same Currency Allocation

Usually:

```text
source_amount == target_amount
```

within same currency.

---

# 227. Base Amounts

Need enough data to explain:

```text
historic carrying value closed
current settlement base value
FX difference
```

---

# 228. Allocation Reversal Model

Possible:

```text
reversal_of_allocation_id
```

on inverse row.

Prefer append-only history.

---

# 229. `write_off`

Can be modeled as settlement adjustment/source or dedicated record.

Recommended dedicated command/result referencing allocation/open item.

---

# 230. Write-Off Fields

```text
amount
currency
base_amount
reason_code
reason_text
approval_ref?
account_role
actor
date
```

---

# 231. DB Constraints — Amounts

No negative "available" values unless explicitly represented by semantic direction.

Prefer positive amounts + direction/type over sign ambiguity.

---

# 232. Signed vs Directional Model

Canonical recommendation:

```text
direction/type describes economic sign
amount columns non-negative
```

This improves reasoning.

---

# 233. Credit Item Direction

Credit Note may be modeled as settlement source rather than negative receivable Open Item for application simplicity.

But reporting may still need open debit/credit entries.

Final model should support both via explicit entry polarity.

---

# 234. Open Item Polarity

Recommended field:

```text
Polarity = Debit / Credit
```

in addition to Receivable/Payable domain direction.

---

# 235. Why Polarity Matters

AR can contain:

```text
Invoice = Debit
Customer Credit = Credit
```

AP can contain reverse patterns.

---

# 236. Generic Application Engine

Application matches opposite polarity within same Party/control context.

---

# 237. Open Debit / Open Credit

This model supports:

- invoices;
- credits;
- payments;
- advances

uniformly.

---

# 238. Settlement Source Table vs Open Credit Item

Architecture decision:

Prefer treating all settleable party ledger entries as a common `PartyLedgerItem` abstraction internally, with `OpenItem` terminology in product.

However, do NOT collapse Payments ownership.

Subledger can have references/projections of settlement sources.

---

# 239. Canonical Model Recommendation

Use:

```text
PartyOpenItem
```

for debit/credit obligations/credits, including available payment credits.

Source document remains external.

This gives one application engine.

---

# 240. Payment Credit Item

A submitted payment can create:

```text
PartyOpenItem
Direction = Receivable
Polarity = Credit
Source = Payment
```

Then allocation closes it against Invoice debit item.

---

# 241. Why This Is Elegant

All applications become:

```text
Open Credit Item
↔
Open Debit Item
```

with same settlement mechanics.

---

# 242. But Cash Movement Still Separate

Payment source document/accounting fact is still Payments-owned.

Subledger item is only its party-settlement representation.

---

# 243. Final Naming

Implementation may use:

```text
PartyOpenItem
OpenItemApplication
ApplicationBatch
```

rather than separate settlement source table if unified model proves cleaner.

---

# 244. Domain Decision

**Preferred canonical model:**

```text
PartyOpenItem
ApplicationBatch
OpenItemApplication
```

All party obligations/credits are Open Items with:

```text
AR/AP Direction
Debit/Credit Polarity
```

This reduces duplicated settlement logic.

---

# 245. Examples

Customer Invoice:

```text
Direction = AR
Polarity = Debit
Amount = 10,000
```

Customer Payment:

```text
Direction = AR
Polarity = Credit
Amount = 4,000
```

Customer Credit Note:

```text
Direction = AR
Polarity = Credit
Amount = 2,000
```

---

# 246. Supplier Bill

```text
Direction = AP
Polarity = Credit
```

depending on accounting-style party ledger representation.

Supplier Payment:

```text
Direction = AP
Polarity = Debit
```

---

# 247. Avoid User Exposure of Debit/Credit Polarity

Normal user sees:

```text
Invoice
Payment
Credit
Remaining
```

not internal polarity.

---

# 248. Application Rule

Only economically opposite items can apply.

---

# 249. Application Sum

For each item:

```text
Original Amount
-
Active Applied Amount
=
Remaining Amount
```

---

# 250. Application History

Each application links two Open Items.

---

# 251. Application Batch

One user action can create many links.

---

# 252. Application Data Model

```text
OpenItemApplication
{
  Id
  BatchId

  DebitItemId
  CreditItemId

  DebitCurrency
  CreditCurrency

  DebitAppliedAmount
  CreditAppliedAmount

  HistoricBaseAmountClosed
  SettlementBaseAmount

  FxDifference
  RoundingDifference

  ApplicationDate
  SettlementDate

  FxSnapshot?
  Status
  ReversalOf?
}
```

---

# 253. Same Currency Simplicity

DebitAppliedAmount == CreditAppliedAmount.

---

# 254. Cross-Currency

Amounts differ according to explicit conversion.

---

# 255. Which Currency Drives?

User may specify payment-side or invoice-side applied amount.

Engine resolves the other side deterministically.

---

# 256. Cross-Currency UX

Show:

```text
Invoice remaining: 1,000 USD
Payment available: 52,500 EGP
Rate: ...
Apply: 1,000 USD
Consumes: 52,500 EGP
FX impact: ...
```

finance role only.

---

# 257. Cross-Currency Permissions

May be restricted to finance roles.

---

# 258. Current Business Central Pattern

Business Central explicitly supports applying customer/vendor entries in different currencies when configured, validating cross-currency application as a real requirement rather than edge speculation. citeturn496724search0

---

# 259. Application Policy

```text
allow_cross_currency
fx_purpose
max_rate_age
allow_manual_rate
```

via FinBase/financial policy.

---

# 260. Application Reversal and FX

Reversing a cross-currency allocation may require reversing settlement FX accounting effect.

Subledger emits inverse/reversal Fact.

---

# 261. Write-Off Application

Represented as explicit adjustment item/application.

Avoid magic reduction of target RemainingAmount.

---

# 262. Tolerance Item

Likewise explicit.

---

# 263. Cash Discount Item

If implemented, explicit settlement adjustment item.

---

# 264. Party Ledger Timeline

Unified Open Items + applications naturally produce a clear party timeline.

---

# 265. Customer Balance Formula

At a point in time:

```text
AR Debit open/carrying amounts
-
AR Credit open/carrying amounts
```

according to base/transaction currency reporting.

---

# 266. Supplier Balance Formula

Equivalent AP semantics.

---

# 267. Negative Customer Balance

Can legitimately mean customer credit/advance.

Do not force zero.

---

# 268. Negative Supplier Balance

Can represent supplier advance/credit.

---

# 269. Balance Presentation

Display business-friendly:

```text
Amount Due
Available Credit
Net Balance
```

rather than confusing signs.

---

# 270. Current Open Items Screen

Filters:

```text
Party
Direction
Document type
Due range
Overdue only
Currency
Amount
Branch
Dispute/Hold
```

---

# 271. Open Item Search

Search by:

```text
Document number
Payment reference
External reference
Party
```

using read projection.

---

# 272. Avoid Joining Invoicing Tables for Every List Row

Subledger read model can denormalize source number/display data.

Source document remains canonical.

---

# 273. Source Display Snapshot

Store:

```text
source_display_number
source_date
```

as immutable reference/display data where useful.

---

# 274. Source Rename/Number Immutability

Final document number should already be immutable.

---

# 275. Party Display Name

Read projection can cache current display name.

Statement legal historical name may come from source document snapshot.

---

# 276. Application Screen Performance

Query only open opposite-polarity items for Party/Entity/control.

Partial index.

---

# 277. Default Result Limit

Do not load 50k invoices.

Server-side search/pagination.

---

# 278. Payment Application UI

Recommended:

```text
Payment Available: 10,000 EGP

Open Invoices
---------------------------------
INV-1001 | Due ... | 3,000 | Apply [3,000]
INV-1002 | Due ... | 4,000 | Apply [4,000]
INV-1003 | Due ... | 8,000 | Apply [3,000]

Applied 10,000
Remaining 0
```

---

# 279. Keyboard Workflow

Finance teams process many payments.

Support:

- search by invoice number;
- keyboard selection;
- apply full amount;
- next row;
- batch confirmation.

---

# 280. Allocation Suggestions

Button:

```text
Auto Apply
```

with explanation:

```text
3 exact references
2 oldest due
```

---

# 281. Do Not Hide Auto Behavior

Before commit, show proposed applications.

---

# 282. Payment Reference Matching

Normalization:

- whitespace;
- common prefixes;
- case;
- safe punctuation.

Do not fuzzy-match too aggressively without confidence threshold.

---

# 283. Bank Memo Parsing

Payments/Banking domain provides memo/reference.

Subledger matching engine consumes.

---

# 284. AI Matching

Optional later.

Deterministic exact/rule matcher first.

---

# 285. Application Confirmation

Backend uses current row versions.

---

# 286. Draft Application

Can exist frontend-only or DB draft.

Prefer short-lived frontend draft unless collaboration/save-later needed.

---

# 287. Confirmed Application

Immutable.

---

# 288. Application Permissions

Initial:

```text
subledger.read

subledger.application.create
subledger.application.reverse
subledger.application.cross_currency

subledger.writeoff.create
subledger.writeoff.approve

subledger.profile.read
subledger.profile.manage

subledger.credit_limit.read
subledger.credit_limit.manage

subledger.opening.manage
subledger.opening.post

subledger.reconciliation.read
subledger.reconciliation.run

subledger.statement.read
subledger.aging.read
subledger.audit.view
```

---

# 289. Sales User Permissions

May see:

```text
customer current balance
overdue summary
credit available
```

but not supplier/AP or detailed accounting history unless authorized.

---

# 290. Sensitive Supplier Data

Purchasing user may not automatically see full supplier financial statement.

Permissions separate.

---

# 291. Credit Limit Admin

Finance/Credit Manager.

---

# 292. Write-Off SoD

Large write-off can require:

```text
Creator != Approver
```

configurable.

---

# 293. Allocation Reversal Permission

Higher-risk than normal allocation.

---

# 294. Period Lock Interaction

Allocation can affect only Subledger relation or also accounting.

Policies:

- pure application of already-posted same-currency items may be allowed in current context;
- if FX/write-off accounting is produced, PostingDate must be open;
- historical application date restrictions apply.

---

# 295. Closed Period Application

Need explicit policy.

Do not let changing application history rewrite a closed-period GL effect.

---

# 296. Application Effective Date

If applying now to old invoice/payment:

application can be current date while source dates remain historical.

---

# 297. Historical Aging Impact

Use application effective date to determine when obligation became settled for historical as-of reporting.

---

# 298. Backdated Application

Finance can backdate only if policy/period permits.

---

# 299. Unapplication Date

Reversal has its own effective date.

Historical report before reversal still shows applied.

After reversal shows open again.

---

# 300. Application Audit

Record:

```text
Actor
Timestamp
Effective date
Source/targets
Amounts
Policy
FX
Reason
Reversal link
```

---

# 301. Payment Reconciliation Import

Bank statement matching belongs Payments/Banking.

Subledger exposes application service.

---

# 302. Payment Proposal

AP Payment Run later queries Subledger.

---

# 303. Customer Refund

Available customer credit can be:

```text
applied to invoice
or refunded
```

Refund movement owned Payments.

Subledger credit item closes against refund settlement semantics.

---

# 304. Supplier Refund

Symmetric.

---

# 305. Refund Does Not Create Fake Invoice

Use payment/refund document + open credit application.

---

# 306. Chargeback / Returned Payment

Payment reversal can reopen invoices.

Payments emits reversal context.

Subledger reverses relevant applications according to policy.

---

# 307. Returned Payment Flow

```text
Payment reversed
→ credit Open Item reversed/removed via corrective item
→ applications reversed
→ invoices reopen
→ Ledger reversal Facts
```

atomically where required.

---

# 308. Do Not Delete Payment Applications on Bounce

Preserve history.

---

# 309. Currency Difference on Reversal

Use original settlement FX reversal semantics unless accounting policy requires current-period correction.

Explicit.

---

# 310. Open Item Source Reversal

Credit Note/cancelled invoice flow:

source module tells Subledger to reverse/close original item via corrective open item/application semantics.

---

# 311. Invoicing Cannot Directly Set Outstanding Amount

It calls Subledger commands.

---

# 312. Payment Cannot Directly Set Invoice Paid Status

Invoice UI derives payment status from Subledger.

---

# 313. Invoice Settlement State

Invoicing can expose:

```text
Unpaid
PartiallyPaid
Paid
```

as projection from Subledger.

It is not canonical internal settlement engine.

---

# 314. Separate Document Lifecycle

Invoice:

```text
Draft / Finalized / Cancelled
```

separate from:

```text
Settlement State
```

already locked.

---

# 315. Settlement State Projection

Subledger publishes/query:

```text
OpenAmount
SettlementStatus
```

for source document.

---

# 316. Immediate Consistency

After application commit, source invoice screen should read updated status immediately.

Use synchronous query/read model update where practical.

---

# 317. Events

Meaningful:

```text
OpenItemCreatedV1
OpenItemSettledV1
OpenItemReopenedV1
ApplicationConfirmedV1
ApplicationReversedV1
PartyFinancialProfileChangedV1
CreditLimitChangedV1
```

---

# 318. Event Volume

Do not emit separate event for every internal balance calculation.

---

# 319. Outbox

Subledger integration events via Transactional Outbox.

---

# 320. Cross-Module Synchronous Contracts

Invoicing can synchronously create Open Items in shared transaction.

Payments can synchronously create payment credit item.

---

# 321. Transaction Coordinator Use Cases

Example invoice finalization:

```text
Invoice finalized
Tax snapshot
Subledger due Open Items
Ledger Fact/Posting
Audit
Outbox
```

single transaction where chosen.

---

# 322. Payment Receive Use Case

```text
Payment posted
Subledger credit item
Ledger payment Fact
Outbox
```

atomic.

---

# 323. Application Use Case

```text
Open Items locked
Applications inserted
Open amounts updated
FX/write-off Facts if needed
Ledger posting if needed
Audit
Outbox
```

atomic.

---

# 324. No Broker Between Application and Balance Update

Core settlement consistency is synchronous DB transaction.

---

# 325. API Surface

Base:

```text
/api/v1/subledger
```

---

# 326. Open Item APIs

```text
GET /open-items
GET /open-items/{id}
GET /parties/{partyId}/open-items
```

---

# 327. Application APIs

```text
POST /applications/preview
POST /applications
POST /applications/{id}/reverse
```

---

# 328. Auto Apply API

```text
POST /applications/suggest
```

returns suggestions only.

---

# 329. Profile APIs

```text
GET /parties/{partyId}/financial-profile
PUT /parties/{partyId}/financial-profile
```

scoped LegalEntity.

---

# 330. Exposure APIs

```text
GET /parties/{partyId}/exposure
```

---

# 331. Aging API

```text
GET /reports/aging
```

---

# 332. Statement API

```text
GET /parties/{partyId}/statement
```

---

# 333. Reconciliation API

```text
GET /reconciliation/control-accounts
POST /reconciliation/run
GET /reconciliation/runs/{id}
```

---

# 334. Opening APIs

```text
POST /opening/import
POST /opening/{batchId}/validate
POST /opening/{batchId}/post
```

---

# 335. Internal Contracts Project

```text
Erp.Finance.Subledger.Contracts
```

---

# 336. Key Contracts

```text
CreateReceivableOpenItems
CreatePayableOpenItems

RegisterPartyPaymentCredit
RegisterPartyCredit

ApplyOpenItems
ReverseApplication

GetPartyExposure
GetOpenItems
GetSettlementState

GetForeignOpenBalances
GetControlBalance
```

---

# 337. No Public Arbitrary Open Item Creation

Only registered modules/migration commands.

---

# 338. Source Authorization

Invoicing/Payments/Purchasing invoke trusted application contracts.

---

# 339. Domain Commands

```text
CreateOpenItemsFromSchedule
CreateOpeningOpenItem
RegisterPaymentCreditItem
RegisterCreditNoteItem

ApplyOpenItems
ReverseOpenItemApplication

WriteOffOpenItem

UpdatePartyFinancialProfile
SetCreditLimit

RunControlReconciliation
```

---

# 340. Queries

```text
GetPartyBalance
GetPartyExposure
GetOpenItems
GetAvailableCredits
GetAging
GetStatement
GetApplicationHistory
GetControlReconciliation
```

---

# 341. Error Codes

```text
subledger.open_item_not_found
subledger.item_closed
subledger.item_changed
subledger.application_over_target
subledger.application_over_source
subledger.party_mismatch
subledger.entity_mismatch
subledger.direction_mismatch
subledger.currency_not_allowed
subledger.fx_rate_missing
subledger.application_duplicate
subledger.application_conflict
subledger.period_locked
subledger.writeoff_not_allowed
subledger.control_reconciliation_failed
subledger.credit_limit_invalid
subledger.opening_unbalanced
```

---

# 342. UX Error

Example:

> "الفاتورة اتغير رصيدها لأن مستخدمًا آخر سجّل دفعة. حدّث القائمة وحاول مرة أخرى."

instead of concurrency exception.

---

# 343. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 344. Entity Authorization

Application-level Legal Entity scope mandatory.

---

# 345. Composite Tenant FKs

Prevent cross-tenant item/application links.

---

# 346. Party Scope Validation

Party belongs/visible appropriately, but financial profile is Entity-scoped.

---

# 347. Indexes — Open Items

Likely hot:

```text
(tenant_id, legal_entity_id, party_id, direction, is_open, due_date)
```

partial `WHERE is_open`.

---

# 348. Indexes — Source

```text
(tenant_id, legal_entity_id, source_type, source_ref)
```

unique where appropriate.

---

# 349. Indexes — Applications

```text
(debit_item_id)
(credit_item_id)
(batch_id)
(application_date)
```

with Tenant/Entity keys as needed.

---

# 350. Indexes — Aging

Open items by:

```text
entity + direction + is_open + due_date
```

---

# 351. Indexes — Exposure

Party + open.

---

# 352. No Unbounded Include Graphs

Use explicit projections.

---

# 353. Subledger DbContext

Only Subledger-owned entities.

No Invoicing/Payments/Ledger implementation entities.

---

# 354. Read Model Cross-References

Stable DocumentRef/PartyId only.

---

# 355. Open Item Number

No need human sequence by default.

Source document number is user-facing.

UUIDv7 internal.

---

# 356. Settlement/Application Number

Optional audit-friendly number.

Can use Platform Numbering if finance wants reference.

---

# 357. Numeric Types

FinBase decimals/numeric.

No float/double.

---

# 358. Currency Mismatch

No implicit conversion.

---

# 359. Rounding

FinBase only.

---

# 360. FX Snapshot

FinBase immutable settlement snapshot.

---

# 361. Amount Validation

Application amounts > 0.

Zero allocation forbidden.

---

# 362. Tiny Residual

Never silently close due to DB precision.

Tolerance/write-off explicit.

---

# 363. Closed Item

`remaining_amount == 0` under canonical rounding.

---

# 364. Final Application Residual Rule

Final allocation can absorb approved proportional carrying-base rounding residual, not transaction amount discrepancy.

---

# 365. Performance Target — Party Open Items

Initial target:

```text
p95 <= 100–150ms
```

for typical Party open-item listing with pagination under realistic data.

Measure.

---

# 366. Performance Target — Application Commit

Interactive small allocation batch:

```text
p95 <= 100–200ms
```

excluding external integrations, under realistic concurrency.

Correctness first.

---

# 367. Performance Target — Exposure

Sales-critical exposure query:

```text
p95 <= 50–100ms
```

from strongly consistent summary.

---

# 368. Performance Target — Aging

Large company aging may run as optimized report/read model; target based on dataset and filters.

Do not promise instant million-row full report without design.

---

# 369. Batch Reconciliation

Background job for massive scopes.

---

# 370. Query Plan Tests

Capture `EXPLAIN ANALYZE` for:

- party open items;
- overdue list;
- exposure;
- available credits;
- aging;
- source lookup.

---

# 371. Stress Dataset

At least:

```text
1M Open/Closed Items
5M Applications
100k currently open
many parties
multiple currencies
many tenants
```

and larger synthetic where infrastructure allows.

---

# 372. Open Hot Set Test

Ensure partial indexes keep current workflow fast despite historical growth.

---

# 373. Concurrency Load

Multiple payments against same high-volume customer.

Test lock contention.

---

# 374. Hot Customer

One enterprise customer can have tens of thousands open invoices.

Pagination and targeted matching mandatory.

---

# 375. Lock Granularity

Lock only involved Open Items, not entire Party whenever possible.

---

# 376. Auto Apply Many Items

May lock selected targets in deterministic order.

---

# 377. Projection Update Cost

Avoid updating expensive aggregate tables once per application row if one batch update works.

---

# 378. Party Balance Projection

Update delta once per confirmed batch.

---

# 379. Control Balance Projection

Can be maintained by accounting facts/read model.

Reconciliation remains independent verification.

---

# 380. Rebuild Tools

Support:

```text
Rebuild Party Balance
Rebuild Open Item Cache
Rebuild Aging Support Projection
```

without altering source applications.

---

# 381. Observability Metrics

```text
subledger_application_duration
subledger_application_lines
subledger_open_items_query_duration
subledger_auto_match_duration
subledger_application_conflict_total
subledger_writeoff_total
subledger_fx_settlement_total
subledger_reconciliation_difference
subledger_exposure_query_duration
```

No Party/Tenant IDs as metric labels.

---

# 382. Tracing

Application trace:

```text
Source items loaded
Target items loaded
Locks acquired
Amounts validated
FX resolved
Applications calculated
Facts generated
DB commit
```

---

# 383. Logging

Avoid logging all invoice/payment financial data.

Use IDs/correlation.

---

# 384. Audit

Audit high-risk:

- credit-limit changes;
- write-offs;
- manual cross-currency applications;
- application reversals;
- opening imports;
- profile policy changes.

---

# 385. Allocation Rows Are Operational Audit Evidence

No need duplicate every application into generic Audit.

---

# 386. AI Restrictions

AI can:

- suggest payment→invoice matches;
- summarize overdue exposure;
- explain application history.

AI cannot:

- invent payment;
- write off amount;
- silently apply large payment;
- override FX;
- change credit limit

without approved tool command/permission/policy.

---

# 387. Test Matrix — Basic AR

```text
invoice creates open item
partial payment
full payment
two payments
one payment many invoices
credit note application
overpayment
unapplied payment
```

---

# 388. Test Matrix — AP

Same supplier equivalents.

---

# 389. Test Matrix — Installments

Invoice schedule:

```text
30/40/30
```

creates exact due Open Items and sums to total.

---

# 390. Test Matrix — Concurrency

Two users attempt to close same invoice.

Only valid combined amount committed.

No negative remaining.

---

# 391. Test Matrix — Idempotency

Double-submit application → one confirmed batch.

---

# 392. Test Matrix — Unapply

Apply → reverse → balances reopen exactly.

---

# 393. Test Matrix — Historical Aging

Apply on Jan 15.

Aging as Jan 10 shows open.

Aging as Jan 20 shows settled.

---

# 394. Test Matrix — Credit Limit

Exposure changes immediately after posting/settlement.

---

# 395. Test Matrix — Cross Currency

```text
USD invoice
EGP payment
partial application
final application
FX gain/loss
```

---

# 396. Test Matrix — FX Residual

Final settlement leaves:

```text
transaction open = 0
base carrying open = 0
```

with explicit FX/rounding effect.

---

# 397. Test Matrix — Write-Off

Permission, threshold, reason, Fact, audit.

---

# 398. Test Matrix — Control Reconciliation

GL and Subledger equal.

Injected test mismatch detected.

---

# 399. Test Matrix — Opening Migration

Outstanding AR/AP + advances.

Totals reconcile to Ledger opening.

---

# 400. Property Test — No Over-Allocation

Generated concurrent application sets never result in remaining below zero.

---

# 401. Property Test — Application Conservation

For same-currency simple case:

```text
source remaining + applied == source original
target remaining + applied == target original
```

---

# 402. Property Test — Apply + Reverse

Net operational effect returns to original state.

---

# 403. Property Test — Historical Determinism

Same item/application history + AsOfDate → same balance.

---

# 404. Property Test — Projection Rebuild

Rebuilt Party balance equals transactionally maintained projection.

---

# 405. Architecture Test

No module except Subledger writes Subledger tables.

---

# 406. Architecture Test — Outstanding Amount

Invoicing cannot own mutable canonical `outstanding_amount` logic.

It can cache/read projection, but Subledger is authority.

---

# 407. Architecture Test — Payment Allocation

Payments cannot directly mutate Invoice settlement status.

---

# 408. Architecture Test — GL

Subledger does not create Journal Entries directly.

Accounting Facts only.

---

# 409. Security Test

Tenant A cannot:

- view B open items;
- apply B payment;
- use B Party ID;
- manipulate B application.

---

# 410. Entity Test

Same Tenant, Entity A cannot settle Entity B items.

---

# 411. Party Test

Cannot allocate Customer A credit to Customer B Invoice.

---

# 412. Control Context Test

Cannot apply across incompatible control accounts/policies without explicit reclassification.

---

# 413. Performance Test

Open item query remains fast with millions closed.

---

# 414. Product Acceptance — Simple Customer

Invoice screen:

```text
Total 10,000
Paid 4,000
Remaining 6,000
Due 1 Nov
```

No Subledger jargon.

---

# 415. Product Acceptance — Finance Application

Payment 10,000.

User selects three invoices.

Preview correct.

Confirm once.

Balances update instantly.

---

# 416. Product Acceptance — Unapplied Payment

Payment received without reference.

System shows:

```text
Available Credit 10,000
```

until later application.

---

# 417. Product Acceptance — Advance

Advance applied to future invoice.

Remaining advance preserved.

---

# 418. Product Acceptance — Wrong Application

Reverse application.

Original history remains.

Reapply correctly.

---

# 419. Product Acceptance — Credit Note

Apply Credit Note to Invoice without creating new bank movement.

---

# 420. Product Acceptance — Write-Off

Small balance cleared through explicit approved write-off.

---

# 421. Product Acceptance — Customer Statement

Shows coherent opening/activity/closing.

---

# 422. Product Acceptance — Aging

Current + overdue buckets match open-item due dates.

---

# 423. Product Acceptance — Historical Aging

Prior-period report unaffected by later payment except according to AsOf date.

---

# 424. Product Acceptance — Credit Limit

Sales sees current credit availability within performance target.

---

# 425. Product Acceptance — Multi-Currency

Finance can settle USD invoice using EGP payment with explicit rate/FX difference.

---

# 426. Product Acceptance — AR/AP Same Party

System does NOT net automatically.

---

# 427. Product Acceptance — Reconciliation

Finance sees:

```text
AR Control Reconciled
AP Control Reconciled
```

and diagnostic if not.

---

# 428. Product Acceptance — Migration

Outstanding legacy items imported and reconcile exactly to opening control accounts.

---

# 429. Red-Team — Invoice Stores Independent Outstanding Truth

Reject.

---

# 430. Red-Team — Payment Allocation Deletes History

Reject.

---

# 431. Red-Team — Payment Allocation Reposts Bank

Reject unless a new actual payment movement exists.

---

# 432. Red-Team — Negative Remaining Amount

Reject.

---

# 433. Red-Team — Cross-Party Settlement

Reject.

---

# 434. Red-Team — Cross-Entity Settlement

Reject.

---

# 435. Red-Team — Auto Net AR/AP Same Party

Reject.

---

# 436. Red-Team — Current Payment Terms Recalculate Old Due Dates

Reject.

Open Items use source schedule snapshot.

---

# 437. Red-Team — Current FX Recalculates Historical Carrying Value

Reject.

---

# 438. Red-Team — Write-Off Hidden as Rounding

Reject.

---

# 439. Red-Team — Allocation Without Permission

Reject backend.

---

# 440. Red-Team — Current Balance Only, No History

Reject because historical aging impossible.

---

# 441. Red-Team — Credit Limit Uses Stale Async Projection

Reject for authoritative decision path unless freshness/policy explicitly allows.

---

# 442. Red-Team — Manual GL AR Posting Creates Difference

Blocked by Ledger control-account policy.

---

# 443. Implementation Order

```text
SL00 ADRs + AR/AP Domain Boundaries
SL01 Schema + RLS
SL02 PartyFinancialProfile
SL03 PartyOpenItem Model
SL04 Open Item Creation Contracts
SL05 Payment/Credit Item Registration
SL06 Application Batch + Application Model
SL07 Same-Currency Settlement Engine
SL08 Concurrency + Idempotency
SL09 Application Reversal
SL10 Payment Term Installments Integration
SL11 Advances / Unapplied Credits
SL12 Credit Notes / Adjustments
SL13 Write-Off / Tolerance Framework
SL14 Multi-Currency Settlement + Realized FX
SL15 Party Balance / Exposure
SL16 Aging / Historical As-Of
SL17 Customer/Supplier Statements
SL18 GL Control Reconciliation
SL19 Opening AR/AP Migration
SL20 Auto-Apply / Matching
SL21 APIs + Finance UX
SL22 Outbox / Cross-Module Events
SL23 Security / Permissions / Audit
SL24 Performance / Load / Projection Rebuild
SL25 Product + Accountant Acceptance
```

---

# 444. SL00 Exit Gate

ADRs approved:

- GL vs Subledger.
- unified PartyOpenItem model.
- Debit/Credit polarity semantics.
- payment movement vs application.
- immutable application history.
- strong consistency for exposure.
- settlement accounting Fact boundaries.

---

# 445. SL01 Exit Gate

- `finance_subledger`.
- FORCE RLS.
- tenant-aware constraints.
- Testcontainers isolation.

---

# 446. SL02 Exit Gate

- Party × Legal Entity financial profile.
- payment-term refs.
- credit policy.
- no Directory pollution.

---

# 447. SL03 Exit Gate

Unified Open Item supports:

- AR/AP.
- Debit/Credit.
- transaction/base amounts.
- due dates.
- source refs.
- current + historical state.

---

# 448. SL04 Exit Gate

Invoice/Bill schedule creates exact Open Items idempotently.

---

# 449. SL05 Exit Gate

Payment/Credit can exist unapplied.

No duplicate bank movement.

---

# 450. SL06 Exit Gate

Many-to-many applications.

No over-allocation.

---

# 451. SL07 Exit Gate

Partial/full same-currency settlement exact.

---

# 452. SL08 Exit Gate

Concurrent applications safe + idempotent.

---

# 453. SL09 Exit Gate

Reversal preserves history and restores remaining amounts exactly.

---

# 454. SL10 Exit Gate

FinBase schedule snapshots map exactly to Open Items.

---

# 455. SL11 Exit Gate

Advances/unapplied credit first-class.

---

# 456. SL12 Exit Gate

Credit Notes settle without fake payment.

---

# 457. SL13 Exit Gate

Write-offs explicit, permissioned, audited, ledger-fact driven.

---

# 458. SL14 Exit Gate

Cross-currency partial/full settlement with exact realized FX and no residual ghost balances.

---

# 459. SL15 Exit Gate

Sales-critical exposure is strongly consistent and performant.

---

# 460. SL16 Exit Gate

Current + historical aging correct.

---

# 461. SL17 Exit Gate

Party Statements reconcile.

---

# 462. SL18 Exit Gate

AR/AP Subledger control equals GL Control Account.

Mismatch diagnostic.

---

# 463. SL19 Exit Gate

Opening outstanding items + credits reconcile to Ledger opening.

---

# 464. SL20 Exit Gate

Auto-match deterministic/safe; suggestions explainable.

---

# 465. SL21 Exit Gate

Simple business UX + efficient finance allocation UX.

---

# 466. SL22 Exit Gate

Cross-module contracts/events without DbContext coupling.

---

# 467. SL23 Exit Gate

permissions/RLS/SoD/audit pass.

---

# 468. SL24 Exit Gate

- millions-history dataset;
- open partial index;
- concurrency load;
- projection rebuild;
- historical aging performance.

---

# 469. SL25 Acceptance

Senior Accountant + Product Architect approve:

- AR/AP.
- partial payments.
- advances.
- credits.
- FX.
- write-offs.
- aging.
- statements.
- reconciliation.
- migration.

---

# 470. Build Now — MUST

- PartyFinancialProfile.
- AR/AP unified Open Items.
- installment due items.
- payment/credit available items.
- partial/full application.
- many-to-many application.
- unapplied credit.
- advances.
- credit notes.
- application reversal.
- write-off framework.
- multi-currency settlement foundation.
- realized FX Fact.
- current balances.
- exposure.
- aging/current + historical.
- statements.
- GL control reconciliation.
- opening AR/AP import.
- batch/suggestion API.
- performance indexes/read models.
- RLS/security/audit.

---

# 471. Design Now / Build Later

- advanced cash discounts.
- AR/AP legal set-off/netting.
- sophisticated payment tolerances.
- AI bank matching.
- collections/dunning.
- promise-to-pay.
- payment runs.
- credit insurance.
- factoring.
- customer risk scoring.
- cross-company netting.
- advanced dispute management.

---

# 472. Explicitly NOT Owned Here

- Bank transaction creation.
- Bank reconciliation.
- Invoice legal content.
- Payment gateway.
- GL Journal creation.
- Tax recalculation.
- Collections workflow.
- Sales order credit approval workflow.
- Supplier payment execution.

---

# 473. Senior Product Rule

> **The ordinary user sees Paid / Remaining / Due. TODIR hides Open Item and application mechanics unless the user's role needs them.**

---

# 474. Senior Accounting Rule

> **Every outstanding amount must be explainable from the original obligation and immutable application history.**

---

# 475. Senior Architecture Rule

> **Payment movement, open-item creation, settlement allocation and General Ledger posting are separate responsibilities connected by explicit contracts.**

---

# 476. Senior Integrity Rule

> **AR/AP Subledger must reconcile exactly to the corresponding GL Control Accounts.**

---

# 477. Senior FX Rule

> **Realized FX comes from actual settlement of historical foreign-currency carrying values, not from re-running today's conversion on an old invoice.**

---

# 478. Senior Performance Rule

> **Optimize for the open working set using partial indexes and strongly consistent summaries while keeping full immutable history for as-of reporting.**

---

# 479. Senior UX Rule

> **A finance user can understand why a balance is open, how it was settled, and how to reverse a mistake without seeing database concepts.**

---

# 480. Current ERP Pattern Validation — Business Central

Microsoft Business Central currently supports:

- open customer/vendor ledger entries;
- application of payments to open entries;
- automatic payment reconciliation;
- manual application;
- unapplication;
- payment tolerances;
- cross-currency applications.

Its Customer Ledger Entry model exposes original and remaining amounts in transaction and local currency.

These patterns validate the need for first-class open-item/application semantics. citeturn496724search1turn496724search3turn496724search5

---

# 481. Current ERP Pattern Validation — ERPNext

ERPNext currently documents:

- Payment Entry for receipts/payments/advances;
- partial payment;
- one payment allocated to invoice references;
- standalone unallocated payments;
- Payment Reconciliation;
- Credit Note allocation;
- unreconciliation;
- Payment Ledger;
- opening outstanding invoice import.

This validates separation between cash movement, party outstanding items and allocation. citeturn244730search0turn244730search1turn244730search2turn244730search3turn244730search5

---

# 482. Source Register — Checked 2026-10-05

## Microsoft Business Central — Managing Receivables

https://learn.microsoft.com/en-us/dynamics365/business-central/receivables-manage-receivables

Used for:

- open customer entries;
- payment application;
- payment tolerances;
- receivables workflow.

## Business Central — Managing Payables

https://learn.microsoft.com/en-us/dynamics365/business-central/payables-manage-payables

Used for:

- outgoing payment application;
- vendor open entries.

## Business Central — Payment Reconciliation

https://learn.microsoft.com/en-us/dynamics365/business-central/receivables-how-reconcile-payments-auto-application

Used for:

- automatic matching;
- open-entry application;
- user override/review.

## Business Central — Cross-Currency Entry Application

https://learn.microsoft.com/en-us/dynamics365/business-central/finance-how-enable-application-ledger-entries-different-currencies

Used for:

- real-world cross-currency application requirement.

## Business Central — Customer Ledger Entry

https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.receivables.cust.-ledger-entry

Used for:

- original amount;
- remaining amount;
- original/local-currency amount concepts.

## ERPNext — Payment Entry

https://docs.frappe.io/erpnext/payment-entry

Used for:

- payment movement;
- advances;
- partial/multi-invoice payment;
- unallocated amount.

## ERPNext — Payment Reconciliation

https://docs.frappe.io/erpnext/payment-reconciliation

Used for:

- allocation of existing payment/credit;
- no new bank movement;
- outstanding/unallocated amount update.

## ERPNext — Allocating Credit Note and Payment

https://docs.frappe.io/erpnext/allocating-credit-note-and-payment

Used for:

- credit application;
- multiple allocations;
- reconciliation semantics.

## ERPNext — Unreconcile Payments

https://docs.frappe.io/erpnext/unreconciliation

Used for:

- preserving original vouchers while removing wrong allocation.

## ERPNext — Payment Ledger

https://docs.frappe.io/erpnext/payment_ledger

Used for:

- party payment/open reference patterns.

## ERPNext — Opening Balance

https://docs.frappe.io/erpnext/opening-balance

## ERPNext — Import Outstanding Invoices

https://docs.frappe.io/erpnext/opening-invoice-creation-tool

Used for:

- migration separation between outstanding party items, advances, stock and residual GL balances.

These are ERP product-pattern references, not accounting/legal authority.

---

# 483. Final Architecture Model

```text
Invoicing
   │
   │ payment schedule snapshot
   ▼
Party Open Items (AR/AP)
   │
   │
Payments ──► Payment/Credit Open Item
   │                │
   │                ▼
   │         Application Engine
   │                │
   │        ┌───────┴────────┐
   │        ▼                ▼
   │   Open Amounts      FX/Write-off Facts
   │                         │
   │                         ▼
   └────────────────────── Ledger
                            │
                            ▼
                    GL Control Accounts
```

Then:

```text
Subledger Control Total
        ==
GL Control Account
```

---

# 484. Final Business Verdict

For a normal business user, TODIR should make settlement feel like:

```text
Invoice 10,000
Paid     4,000
Remaining 6,000
```

For Finance:

```text
Which payments?
Which credits?
Which due installment?
Which application?
Which FX?
Which reversal?
Which write-off?
Why is this balance open?
```

must all be answerable.

---

# 485. Final Engineering Verdict

## Development Seed is not reconciliation certification

Canonical Development Seed Open Items are demo fixtures for evaluating Subledger workflows. They are not expected to have matching Ledger postings and must not be used as evidence that AR/AP balances reconcile to Ledger control accounts. Reconciliation certification must use a dedicated integration scenario that creates the corresponding Subledger and Ledger accounting evidence through supported paths, verifies exact zero variance, and removes any deliberate mismatch before completion.

Subledger is approved only when:

```text
Partial settlement is first-class.
Unapplied payment is first-class.
Advances are first-class.
Application history is immutable.
No over-allocation is possible.
Concurrency cannot create negative remaining balances.
Historical aging is reproducible.
FX settlement is deterministic.
Write-offs are explicit.
Party balances are fast and strongly consistent.
AR/AP reconcile exactly to GL control accounts.
```

---

# 486. Next Pack

After Subledger:

> **`11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Its central question:

> **How does TODIR create, validate, finalize, issue, correct and track legally meaningful customer/supplier invoices while combining Pricing, Tax, Payment Terms, Subledger and Ledger without making Invoicing own any of those domains?**

The pack should lock:

- Invoice aggregate.
- customer/supplier invoices.
- draft/finalization.
- line model.
- pricing snapshots.
- tax snapshots.
- document totals.
- payment-term schedule snapshot.
- document/accounting/regulatory/settlement state separation.
- credit/debit notes.
- correction/cancellation.
- direct invoice vs Sales/Purchasing-origin invoice.
- numbering.
- printing/templates.
- attachments.
- e-invoicing compliance boundary.
- Subledger Open Items creation.
- Ledger Accounting Facts.
- performance.
- UX simplicity.

---

**END OF FILE — 10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
