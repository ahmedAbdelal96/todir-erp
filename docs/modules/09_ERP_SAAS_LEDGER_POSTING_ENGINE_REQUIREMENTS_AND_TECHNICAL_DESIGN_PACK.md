# 09 — TODIR ERP SaaS Ledger & Posting Engine Requirements and Technical Design Pack

**File:** `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + ACCOUNTING INTEGRITY + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Product Architect, Accounting Domain Lead, Senior Accountant Reviewer, Backend/Frontend Leads, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL** — the Ledger is the permanent financial record of TODIR.

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

The Ledger is TODIR's **single canonical general-accounting record**.

The platform rule is:

> **Business modules describe what happened. The Ledger decides how that business fact becomes accounting entries.**

Business modules MUST NOT create Journal Entries directly.

Canonical flow:

```text
Business Module
   ↓
Accounting Fact
   ↓
Ledger Posting Engine
   ↓
Account Determination
   ↓
Balanced Journal Entry
   ↓
Immutable General Ledger
```

The Ledger owns accounting consequences.

The business module owns the business transaction.

---

# 1. Central Business Question

The Ledger must answer:

> **How does TODIR convert an immutable business event into a correct, balanced, auditable, explainable, reversible and idempotent accounting entry?**

---

# 2. Why the Ledger Is a Separate Domain

An invoice knows:

```text
Customer
Lines
Prices
Tax
Total
```

A payment knows:

```text
Payer
Amount
Currency
Bank/Cash method
```

An inventory transaction knows:

```text
Product
Quantity
Location
Valuation
```

None of these domains should need to understand the Chart of Accounts.

The Ledger converts their semantic financial facts into accounting.

---

# 3. Non-Negotiable Invariants

1. Every posted Journal Entry is balanced.
2. Posted accounting history is immutable.
3. A business fact can be posted at most once for one fact version.
4. No business module writes Ledger tables directly.
5. Account determination is centralized and versioned.
6. Posted entries preserve their source and accounting context.
7. Corrections are reversal/adjustment, not silent edit.
8. Posting never depends on current mutable master data after snapshot/fact creation.
9. Base Currency comes from canonical Legal Entity configuration.
10. Cross-tenant/entity isolation is mandatory.
11. General Ledger and Subledger are distinct responsibilities.
12. Ledger reads are optimized without duplicating accounting truth.

---

# 4. Debit = Credit

For every Journal Entry:

```text
SUM(Debit Base Amount)
==
SUM(Credit Base Amount)
```

exactly according to approved financial rounding semantics.

If not:

```text
PostingRejected
```

No "auto-fix later."

---

# 5. Transaction Currency Balance

If one Entry carries a transaction currency, currency-specific lines must obey the posting model defined for that Fact.

Do not assume all transaction-currency debit/credit lines must be populated identically.

Base currency balance is the mandatory accounting invariant.

---

# 6. Ledger Ownership

Ledger owns:

```text
Chart of Accounts
Account
Account Hierarchy
Journal Definition
Journal Entry
Journal Entry Line
Fiscal Year
Accounting Period
Posting Date Policy
Financial Locks
Accounting Fact Contract/Catalog
Posting Rule
Account Determination
Accounting Dimensions
Ledger Mode
Opening General Ledger Entries
Posting/Reversal Engine
General Ledger Read Models
```

---

# 7. Ledger Does NOT Own

```text
Invoice                 → Invoicing
Customer/Supplier Open Items → Subledger
Payment transaction     → Payments
Product                 → Catalog
Price                   → Pricing
Tax calculation         → Tax
Inventory valuation calculation → Inventory
Payroll calculation     → Payroll
Bank reconciliation     → Bank Reconciliation
```

---

# 8. General Ledger vs Subledger

This separation is critical.

General Ledger answers:

```text
What is the total Accounts Receivable balance?
What is total revenue?
What is VAT payable?
What is bank/cash balance in accounting?
```

Subledger answers:

```text
Which customer owes what?
Which invoice remains open?
Which supplier invoice is overdue?
How was a payment allocated?
```

---

# 9. Receivable Example

Ledger:

```text
Accounts Receivable Control = 5,000,000 EGP
```

Subledger:

```text
Customer A = 100,000
Customer B = 350,000
...
```

The two must reconcile, but they are not the same storage model.

---

# 10. No Party-Level Explosion in Chart of Accounts

Do NOT create:

```text
Customer A Receivable Account
Customer B Receivable Account
Customer C Receivable Account
```

for every customer by default.

Use:

```text
AR Control Account
+
Subledger Party/Open Items
```

---

# 11. Why This Matters

Benefits:

- scalable Chart of Accounts;
- cleaner reporting;
- easier customer/vendor lifecycle;
- faster account determination;
- standard accounting architecture.

---

# 12. Ledger Modes

TODIR supports:

```text
Managed Ledger
Full Accounting
```

Same canonical Ledger data.

Different UX/exposure.

---

# 13. Managed Ledger

For a small business that does not want accounting complexity:

TODIR automatically:

- seeds an appropriate Chart of Accounts;
- configures default journals;
- configures account determination;
- opens periods;
- posts business facts.

User may never see debit/credit.

---

# 14. Managed Ledger Does Not Mean Simplified Accounting

Internally, correctness requirements remain identical.

Managed Mode hides complexity.

It does not bypass accounting.

---

# 15. Full Accounting Mode

Finance/accounting users can access:

```text
Chart of Accounts
Journals
Journal Entries
General Ledger
Trial Balance
Periods
Locks
Adjustments
Dimensions
Opening Balances
Posting Configuration
```

---

# 16. Managed → Full Transition

No data migration.

Switching mode reveals/configures richer UX over the same Ledger.

---

# 17. Accounting Module vs Ledger

`ledger` is the hidden financial kernel.

`accounting` later is the full product/application layer that exposes advanced accounting UX.

This separation is intentional.

---

# 18. Chart of Accounts Per Legal Entity

Each Legal Entity has its own Chart of Accounts.

```text
Tenant
├── Egypt Entity → CoA A
└── UAE Entity   → CoA B
```

---

# 19. Country Pack and CoA

Country Pack may provide:

```text
Default CoA Template
Account categories
Recommended posting mappings
Tax account templates
```

Tenant can customize within allowed rules.

Country Pack template is not runtime accounting truth after initialization.

---

# 20. Account Identity

Concept:

```text
Account
{
    Id
    TenantId
    LegalEntityId

    Code
    Name
    NameAr?
    NameEn?

    Type
    NormalBalance
    ParentAccountId?

    AllowDirectPosting
    CurrencyPolicy?
    Status

    Effective dates?
    Version
}
```

---

# 21. Account Types

Initial canonical types:

```text
Asset
Liability
Equity
Revenue
Expense
```

Optional operational subtypes:

```text
ReceivableControl
PayableControl
Cash
Bank
Inventory
Tax
RetainedEarnings
COGS
Other
```

Subtypes assist validation and defaults.

---

# 22. Normal Balance

Account category defines expected normal balance:

```text
Assets / Expenses → Debit-normal
Liabilities / Equity / Revenue → Credit-normal
```

This helps presentation/validation but does not forbid legitimate opposite balances.

---

# 23. Account Code

Human-friendly stable code per Legal Entity.

Example:

```text
110100
210100
410100
```

Do not use code as internal PK.

---

# 24. Account Code Uniqueness

Unique:

```text
tenant + legal_entity + account_code
```

among active/historical according to archive rules.

---

# 25. Account Hierarchy

Tree:

```text
Assets
 ├── Current Assets
 │    ├── Cash
 │    ├── Bank
 │    └── Accounts Receivable
 └── Non-current Assets
```

---

# 26. Parent Account Posting

Default:

summary/group accounts cannot receive direct posting.

Only leaf/postable accounts.

---

# 27. `AllowDirectPosting`

Explicit flag.

Posting Engine rejects posting to non-postable account.

---

# 28. Account Archive

Used account is never hard-deleted.

Archive:

- blocks new posting;
- preserves history;
- keeps reporting.

---

# 29. Account Replacement

Changing account mappings affects future postings.

Historical Journal Entry lines keep original AccountId.

---

# 30. Account Merge

Do not implement arbitrary account merge MVP.

Accounting migrations/reclassification are high risk.

Use controlled transfer/adjusting entry if needed.

---

# 31. Currency Policy on Account

Some accounts may require/allow specific foreign currency tracking.

Ledger multi-currency model must support:

```text
Base Currency
Transaction Currency
Account Currency policy
```

without duplicating FinBase truth.

---

# 32. Journal Definition

A Journal groups entries by accounting purpose.

Examples:

```text
Sales
Purchases
Bank
Cash
Inventory
Payroll
General
Opening
FX
Tax
```

---

# 33. Journal Is Not the Source Document

A Sales Invoice belongs Invoicing.

The resulting Journal Entry may be in Sales Journal.

---

# 34. Journal Fields

```text
Journal
{
  Id
  TenantId
  LegalEntityId
  Code
  Name
  JournalType
  Status
  DefaultAccountMappings?
  NumberingPolicyRef?
}
```

---

# 35. Journal Numbering

Journal Entry internal ID is UUIDv7.

Human voucher/posting sequence can use Platform Numbering according to legal/accounting requirements.

---

# 36. Journal Entry

Canonical accounting posting container:

```text
JournalEntry
{
  Id
  TenantId
  LegalEntityId

  JournalId

  PostingDate
  AccountingPeriodId

  SourceFactId
  SourceReference
  SourceModule
  SourceDocumentRef

  FactType
  FactVersion

  Status
  Description

  ReversalOfEntryId?
  ReversedByEntryId?

  BaseCurrency

  PostingRuleVersion
  AccountDeterminationVersion

  PostedAt
  PostedByActor

  CorrelationId
  Version
}
```

---

# 37. Journal Entry Status

Simple:

```text
Posted
Reversed
```

Draft General Journal input is a separate application entity/process before posting.

Canonical Journal Entry itself should represent posted accounting truth.

---

# 38. Why No Editable Draft Journal Entry

Avoid using canonical accounting record as user worksheet.

Use:

```text
GeneralJournalDraft / JournalBatch
```

then Post → immutable JournalEntry.

---

# 39. Journal Entry Line

Concept:

```text
JournalEntryLine
{
  Id
  TenantId
  LegalEntityId
  JournalEntryId

  Sequence

  AccountId

  DebitBaseAmount
  CreditBaseAmount

  TransactionCurrency?
  TransactionDebitAmount?
  TransactionCreditAmount?
  ExchangeRateSnapshot?

  PartyRef?
  TaxRef?
  ProductRef?

  Dimensions

  Description
}
```

---

# 40. Debit/Credit Storage

Recommended explicit columns:

```text
debit_base_amount
credit_base_amount
```

with invariant:

- both >= 0;
- not both positive;
- at least one is positive except explicitly supported zero line (normally forbidden).

---

# 41. No Signed Single Amount as Only Model

Signed amounts can be useful internally.

But canonical GL line benefits from explicit Debit/Credit for accounting clarity and reporting.

---

# 42. Journal Entry Balance Constraint

Application validation + DB defense.

Database trigger/check strategy can validate line-level properties.

Entry-level balance is validated transactionally by Posting Engine.

---

# 43. Database Balance Enforcement

A row-level CHECK cannot validate total entry balance.

Options:

- Posting Engine transaction;
- deferred constraint trigger if justified;
- immutable posting stored procedure boundary if later proven useful.

Initial design:

Application Posting Engine + integration tests + no external DB writes.

---

# 44. No Direct Table Writes

Only Ledger application commands/Posting Engine role can insert Journal Entries.

DB roles should prevent generic tools/services from casual writes in production.

---

# 45. Accounting Fact

An Accounting Fact is a semantic statement about business financial meaning.

It is not a Journal Entry.

Example:

```text
CustomerInvoicePosted
{
  ReceivableAmount
  RevenueComponents
  TaxComponents
  Party
  Currency
  Dimensions
  SourceDocument
}
```

---

# 46. Why Facts Instead of Journal Lines

If Invoicing emits:

```text
Debit Account 110100
Credit Account 410100
```

then Invoicing owns accounting mapping.

Wrong boundary.

Fact should say:

```text
CustomerReceivable
ProductRevenue
OutputTax
```

Ledger maps these semantics to accounts.

---

# 47. Accounting Fact Catalog

Ledger owns a versioned Fact Catalog defining stable fact types and payload contracts.

---

# 48. Initial Fact Types

Design now for:

```text
CustomerInvoicePosted
CustomerCreditNotePosted

VendorBillPosted
VendorCreditNotePosted

PaymentReceived
PaymentMade

InventoryReceiptValued
InventoryIssueValued
InventoryCostAdjustment

ExpenseRecognized
PayrollPosted

AssetCapitalized
AssetDepreciationPosted
AssetDisposed

TaxAdjustment

SettlementFxDifference
UnrealizedFxRevaluation

OpeningBalancePosted

ManualGeneralJournalPosted
```

Not all implemented immediately.

---

# 49. Fact Type Versioning

Example:

```text
CustomerInvoicePosted@1
```

Breaking payload change:

```text
@2
```

Old facts remain replay/audit compatible.

---

# 50. Fact ID

Globally unique UUIDv7.

---

# 51. Fact Idempotency Key

Canonical:

```text
(SourceModule, SourceReference, FactType, FactVersion)
```

or explicit source idempotency key.

Unique at Ledger boundary.

---

# 52. Idempotent Posting

If same Fact arrives twice:

second attempt returns existing posting result.

It does NOT create second Journal Entry.

---

# 53. Fact Persistence

For synchronous posting, Fact can be persisted in Ledger inbox/fact table as part of same transaction.

For async integrations, Inbox/idempotency applies.

---

# 54. `accounting_fact`

Concept:

```text
id
tenant_id
legal_entity_id

fact_type
fact_version

source_module
source_ref
idempotency_key

business_date
posting_date

payload_jsonb? / typed normalized contract snapshot

status
posted_entry_id?

received_at
posted_at
```

---

# 55. Typed Contract vs JSON

Application uses strongly typed Fact contracts.

Persisted payload may be JSONB for audit/replay if schema versioned.

Do NOT use untyped JSON dictionaries in domain logic.

---

# 56. Fact Payload Integrity

Persist content hash.

Useful for detecting duplicate idempotency key with different payload.

---

# 57. Duplicate Key Different Payload

Critical error:

```text
FactIdempotencyConflict
```

Do not return existing result silently.

---

# 58. Synchronous vs Asynchronous Posting

Not one answer for all flows.

---

# 59. Immediate Accounting Invariant

If business operation is not valid without its accounting consequence, post synchronously within shared PostgreSQL transaction.

Example:

finalizing a financial Invoice may require:

```text
Invoice Finalized
+
Fact Accepted
+
Journal Entry Posted
+
Outbox
```

atomically.

---

# 60. Asynchronous Facts

Non-critical derived/reclassification/reporting jobs may post asynchronously where business invariants allow.

Decision belongs Fact Contract.

---

# 61. No Eventual Consistency for Core Financial Truth by Default

Do not finalize a financial transaction and "hope" Ledger catches up later unless explicit architecture says the business state can safely exist unposted.

---

# 62. Transaction Coordinator

For synchronous cross-module flow:

```text
One NpgsqlConnection
One PostgreSQL Transaction
Module DbContexts
Ledger DbContext
Outbox
```

Commit together.

---

# 63. Broker Is Not Posting Transaction Coordinator

RabbitMQ is for asynchronous integration.

Do not replace ACID posting with broker choreography for immediate financial invariant.

---

# 64. Posting Engine

Canonical service:

```text
ILedgerPostingEngine
```

Concept:

```text
Post(AccountingFact fact, PostingContext context)
```

---

# 65. Posting Pipeline

```text
1. Validate Fact identity/version
2. Validate Tenant/Entity
3. Validate posting date
4. Resolve Fiscal Period
5. Check locks
6. Resolve Base Currency / Financial Context
7. Resolve Posting Rule
8. Resolve Accounts
9. Validate Dimensions
10. Generate Journal Entry Lines
11. Validate Debit = Credit
12. Validate currencies/amounts
13. Persist Fact + Entry + Lines
14. Audit
15. Outbox
16. Return Posting Result
```

---

# 66. Posting Preview

Finance users/admins should be able to preview posting for supported draft documents:

```text
Preview Accounting
```

without persistence.

Business Central's current product supports posting previews, validating this UX pattern.

---

# 67. Preview Must Use Same Engine

Do not maintain:

```text
Preview calculation
Posting calculation
```

separately.

Use same deterministic engine in dry-run mode.

---

# 68. Preview Result

```text
Journal
Posting Date
Accounts
Debits
Credits
Dimensions
Rule explanations
Warnings
```

---

# 69. Posting Rule

A Posting Rule transforms a Fact semantic structure into required accounting legs.

Example:

```text
CustomerInvoicePosted@1
→ AR Control
→ Revenue
→ Output Tax
```

---

# 70. Posting Rule Is Not Account Mapping Alone

It defines:

- required legs;
- semantic amounts;
- sign logic;
- dimension propagation;
- account role required.

---

# 71. Posting Rule Versioning

Rules are versioned/effective.

A posted Entry stores Rule version.

---

# 72. Rule Mutability

Active/used Posting Rule immutable.

Change → new version.

---

# 73. Posting Rule Catalog

Examples:

```text
CustomerInvoicePostingRule
VendorBillPostingRule
PaymentReceiptPostingRule
InventoryIssuePostingRule
PayrollPostingRule
```

---

# 74. Semantic Account Role

Rules request roles:

```text
AccountsReceivableControl
SalesRevenue
OutputTaxPayable
InventoryAsset
COGS
Cash
Bank
PayrollPayable
FXGain
FXLoss
```

Then Account Determination resolves actual AccountId.

---

# 75. Account Determination

Contract:

```text
IAccountDetermination
```

Input:

```text
AccountRole
LegalEntity
FactType
Party/Profile?
Product/Category?
TaxCode?
Journal?
Dimensions?
BusinessDate
```

Output:

```text
AccountId
DeterminationRuleId
Version
Explanation
```

---

# 76. Account Determination Is Centralized

No business module stores GL AccountId as its canonical business behavior unless that field is explicitly an Accounting-owned profile/configuration reference.

---

# 77. Determination Hierarchy

Example revenue role:

```text
Explicit Accounting Override for Product
→ Product Category Accounting Mapping
→ Business/Party Accounting Position if applicable
→ Journal-specific mapping
→ Legal Entity Default Revenue Account
```

Exact hierarchy is role-specific.

---

# 78. Tax Account Determination

Tax Fact may include:

```text
TaxCode
Treatment
Recoverability
```

Ledger resolves:

```text
Output VAT Account
Input Recoverable VAT
Non-Recoverable Tax destination
```

Tax Engine does not own these AccountIds.

---

# 79. Customer Receivable Account

Subledger/Finance profile may select a Receivable Control policy/account class.

Ledger determination returns actual Account.

---

# 80. Supplier Payable Account

Same concept.

---

# 81. Inventory Accounts

Inventory produces valuation semantics.

Ledger determination maps:

```text
Inventory Asset
COGS
GRNI / Accrual
Variance
```

according to configured accounting model.

---

# 82. Account Determination Tables

Concept:

```text
account_role
account_mapping
account_mapping_rule
account_mapping_version
```

---

# 83. Mapping Scope

Typed dimensions such as:

```text
Legal Entity
Account Role
Product Category?
Party Accounting Group?
Tax Code?
Journal?
Location/Inventory Group?
```

Avoid arbitrary expression engine.

---

# 84. Mapping Specificity

Deterministic.

More specific mapping wins over general.

Same-rank conflicting mappings:

```text
AccountingConfigurationConflict
```

---

# 85. No "Newest Mapping Wins"

Same policy as Pricing/Tax.

Accounting cannot depend on insertion order.

---

# 86. "Why This Account?"

Every determination returns explanation.

Example:

```text
Account 410100 — Electronics Revenue
Because:
Role = SalesRevenue
Product Category = Electronics
Legal Entity = Egypt
Mapping = REV-ELECTRONICS v3
```

---

# 87. Missing Account Mapping

Posting blocks.

Never use random suspense account by default.

---

# 88. Suspense Account

Suspense can be legitimate for explicit workflows/import/reconciliation.

It must be intentional:

```text
UseSuspense = explicit policy
```

not hidden fallback for broken configuration.

---

# 89. Posting Configuration Health

Before financial go-live, system validates:

- required account roles mapped;
- journals configured;
- base currency;
- fiscal period;
- retained earnings;
- tax mappings;
- receivable/payable control accounts.

---

# 90. Account Mapping Preview

Finance admin can simulate:

```text
Fact Type + Product Category + Party + Tax
```

and see accounts that would be selected.

---

# 91. Fiscal Year

Concept:

```text
FiscalYear
{
  Id
  LegalEntityId
  StartDate
  EndDate
  Status
}
```

Fiscal year need not equal calendar year.

---

# 92. Accounting Period

Concept:

```text
AccountingPeriod
{
  Id
  FiscalYearId
  StartDate
  EndDate
  Sequence
  Status
}
```

---

# 93. Period Types

Usually monthly.

Architecture permits:

- 12 months;
- 13 periods;
- custom fiscal calendar.

Do not assume Jan-Dec.

---

# 94. Period Lookup

PostingDate resolves exactly one AccountingPeriod.

No overlap.

---

# 95. Period Lifecycle

Potential:

```text
Open
SoftClosed
Closed
```

with explicit lock policies.

---

# 96. Why Soft Close

Month-end team may want:

- normal users blocked;
- finance adjustments allowed.

---

# 97. Hard Close

Closed period blocks normal posting.

Reopen requires elevated permission and audit if jurisdiction/company policy permits.

---

# 98. Financial Locks

Separate concepts can include:

```text
General Posting Lock
Tax Lock
Advisor/Audit Lock
Module-specific Lock
```

---

# 99. Lock Hierarchy

Most restrictive applicable lock wins.

---

# 100. Posting Date Validation

Posting Engine validates:

```text
PostingDate
User allowed posting range
Period status
Financial locks
Go-live boundary
```

---

# 101. Backdated Posting

Allowed only according to lock/policy.

Never silently change PostingDate to today.

---

# 102. Future Posting

May be restricted by policy.

Do not globally assume future dates valid/invalid.

---

# 103. Document Date vs Posting Date

Explicit:

```text
DocumentDate
PostingDate
TaxDate
SupplyDate
```

can differ.

Ledger owns PostingDate/fiscal interpretation.

---

# 104. Closing a Period

Closing is a controlled state transition.

Before close, optional checks:

- unposted critical documents;
- reconciliation statuses;
- tax readiness;
- inventory valuation completed;
- depreciation/payroll jobs;
- trial balance validity.

---

# 105. Close Does Not Rewrite Entries

Period close changes allowed posting behavior/reporting process.

---

# 106. Year Close

Year-end can require transfer of P&L to Retained Earnings according to local/accounting policy.

Design a dedicated closing operation, not manual row mutation.

---

# 107. Retained Earnings

Legal Entity setup requires retained earnings / closing account strategy.

Country Pack can seed default.

---

# 108. Reopen Period

Highly privileged and audited.

System records:

```text
who
when
reason
previous state
```

---

# 109. Posting Window Permission

Finance role can have permitted posting date range.

This is authorization/policy in addition to period locks.

---

# 110. Immutable Ledger

Posted Journal Entry/Lines cannot be edited/deleted.

---

# 111. Correction

Approved methods:

```text
Reversal
Correcting Journal
Credit/Debit document from source module
```

---

# 112. Reversal

Creates new Journal Entry with opposite debits/credits.

Original remains.

---

# 113. Reversal Link

```text
original_entry_id
reversal_entry_id
reason
actor
date
```

---

# 114. Same-Date vs Current-Date Reversal

Policy/period status determines valid reversal date.

If original period locked:

may require current open period adjustment.

---

# 115. Automatic Reversal

Useful for accruals/revaluation:

```text
Post on 31 Dec
Auto reverse on 1 Jan
```

Ledger supports scheduled reversal metadata.

---

# 116. No "Unpost by Delete"

Forbidden.

---

# 117. Source Document Cancellation

If business document cancellation is allowed:

source module emits reversal/cancellation Accounting Fact.

Ledger creates reversal.

---

# 118. Journal Entry Manual Corrections

Manual General Journal is allowed for authorized accounting users.

It still goes through Posting Engine.

---

# 119. General Journal Draft

Application entity:

```text
GeneralJournalDraft
GeneralJournalDraftLine
```

editable until posted.

---

# 120. General Journal Use Cases

Examples:

- accrual;
- correction;
- transfer;
- adjustment;
- opening;
- reclassification.

Not a replacement for specialized documents when a business module exists.

---

# 121. Manual Journal Restrictions

Optionally restrict direct posting to control accounts:

```text
AR Control
AP Control
Inventory Control
Tax Control
```

to prevent Subledger/Inventory reconciliation breaks.

---

# 122. Control Account Policy

Account:

```text
AllowManualPosting = false
```

or allowed only via specific Fact types.

---

# 123. Why Control Account Guard Matters

A random manual Debit to AR Control can make:

```text
GL AR != Sum(Customer Open Items)
```

---

# 124. Subledger Reconciliation Invariant

For controlled subledger accounts:

```text
GL Control Account Balance
==
Subledger Control Total
```

at defined reconciliation points.

`10 Subledger` locks exact mechanics.

---

# 125. Inventory Reconciliation Invariant

Inventory Asset GL should reconcile with Inventory valuation read model/facts at period boundaries.

Inventory Pack locks.

---

# 126. Dimensions

Dimensions provide analytical context without exploding CoA.

Examples:

```text
Branch
Department
Cost Center
Project
Profit Center
Sales Channel
Region
```

---

# 127. Account vs Dimension

Account answers:

> What financial nature is this?

Dimension answers:

> Where/why/for whom operationally?

---

# 128. Avoid Account Explosion

Bad:

```text
Sales-Cairo
Sales-Alexandria
Sales-Riyadh
```

when Branch dimension solves it.

---

# 129. Dimension Definition

```text
DimensionDefinition
{
  Id
  LegalEntityId
  Code
  Name
  ValueSource
  Status
}
```

---

# 130. Dimension Value

Can be sourced from:

```text
Branch
Department
Project
Custom approved dimension catalog
```

---

# 131. Built-in Branch Dimension

Branch is Organization-owned.

Ledger references BranchId as dimension.

---

# 132. Dimension Snapshot

Journal line stores Dimension Value IDs/references.

If display name changes, history still resolves.

---

# 133. Dimension Requirements

Policies can specify:

```text
Required for Account Type
Required for Journal
Required for Fact Type
Optional
Forbidden
```

---

# 134. Dimension Defaults

Can come from:

```text
Source document
User context
Branch
Product
Project
Account mapping
```

but must be deterministic.

---

# 135. Dimension Override

Authorized user/domain can override before posting if policy allows.

Final posted dimension immutable.

---

# 136. Line-Level Dimensions

One Journal Entry can have different dimensions per line.

Necessary for multi-department/project documents.

---

# 137. Dimension Balancing

Some analytical systems require dimension-level balancing.

Do not enable automatically.

If required, explicit balancing policy and offset accounts.

---

# 138. Dimensions and Custom Fields

Do not make every Custom Field automatically an accounting dimension.

Finance admin explicitly promotes approved typed source.

---

# 139. Multi-Currency Ledger

Legal Entity has Base Currency from `06`.

Journal Entry stores BaseCurrency snapshot/code.

---

# 140. Foreign Transaction Line

Where relevant:

```text
TransactionCurrency
TransactionAmount
BaseAmount
ExchangeRateSnapshot
```

---

# 141. Base Currency Is Accounting Truth

Financial statements primarily aggregate Base Amounts.

Transaction amounts retain foreign-currency evidence.

---

# 142. No Current FX Recalculation of Historical Entry

Posted line base amount never changes because today's rate changed.

---

# 143. Payment Settlement FX

Subledger/Payments compute settlement context.

Ledger posts Fact:

```text
SettlementFxDifference
```

to FX Gain/Loss accounts.

---

# 144. Unrealized FX

Period-end revaluation is Ledger process over eligible foreign monetary balances supplied by Ledger/Subledger model.

---

# 145. Revaluation Entry

Creates explicit Journal Entry.

Does not rewrite original entry.

---

# 146. Revaluation Reversal

Typically configurable auto-reversal next period according to accounting policy.

---

# 147. Additional Reporting Currency

Design later.

Do not confuse with Base Currency.

---

# 148. Account Currency

Optional account-level currency restrictions later.

Not needed for every account.

---

# 149. Posting Amount Precision

Use FinBase exact numeric standards.

No double/float.

---

# 150. Rounding

Posting Engine consumes already resolved business/tax values and applies only accounting-required rounding through FinBase policies.

Do not independently re-price or re-tax.

---

# 151. Rounding Difference

If legal calculation legitimately yields small accounting rounding leg:

use explicit:

```text
RoundingDifference
```

semantic/account role.

Never silently adjust arbitrary revenue line.

---

# 152. Rounding Account

Configured through Account Determination.

---

# 153. Posting Reconciliation

Generated Entry must match Fact component totals.

Example Customer Invoice:

```text
Receivable
=
Revenue + Tax + Other recognized components
```

according to Fact semantics.

---

# 154. Source Fact Validation

Posting Rule validates internal Fact invariants before accounting.

---

# 155. Ledger Does Not Recalculate Invoice

It does not:

- re-price product;
- re-run Tax Engine;
- recompute discounts.

It trusts immutable semantic Fact values after validating accounting consistency.

---

# 156. Tax Fact Consumption

Tax Snapshot/Fact contains:

```text
Tax Type
Amount
Recoverability
Treatment
```

Ledger determines accounts.

---

# 157. Inventory Fact Consumption

Inventory Fact contains valuation semantics/amounts.

Ledger determines Inventory/COGS/variance accounts.

---

# 158. Payroll Fact Consumption

Payroll emits:

```text
Gross Expense
Employer Contributions
Employee Payable
Tax/Insurance Payables
```

not account IDs.

---

# 159. Fact Dimensions

Business modules include semantic dimensions/context.

Ledger validates/maps them.

---

# 160. Fact Evolution

New modules add new Fact Types via Ledger-owned catalog/contract review.

Do not let each module invent arbitrary JSON events.

---

# 161. Accounting Fact Registration

Module Manifest can declare facts it produces.

CI verifies Fact Contract exists.

---

# 162. Architecture Test

Business module cannot reference:

```text
Ledger.JournalEntry implementation
Ledger.Account entity
LedgerDbContext
```

Only Ledger Contracts.

---

# 163. Posting Rule Registration

Ledger knows supported Fact Type → Posting Rule version.

Unknown Fact:

```text
UnsupportedAccountingFact
```

---

# 164. No Generic "DebitCreditFact"

Avoid business modules bypassing semantics by sending arbitrary debits/credits disguised as Fact.

Only Manual General Journal may intentionally provide account-level journal instructions, and it remains Ledger-owned.

---

# 165. Ledger Read Models

Canonical Journal Entry/Lines are source of truth.

Read optimization can include:

```text
AccountPeriodBalance
TrialBalanceProjection
GeneralLedgerSearchProjection
DimensionBalanceProjection
```

---

# 166. Balance Read Model

Potential:

```text
AccountPeriodBalance
{
  Tenant
  Entity
  Account
  FiscalYear
  Period
  OpeningDebit
  OpeningCredit
  PeriodDebit
  PeriodCredit
  ClosingBalance
}
```

Derived.

---

# 167. Why Balance Projection

Financial statements should not scan billions of Journal Lines on every request.

---

# 168. Projection Correctness

Derived balance can always be rebuilt from Journal Lines.

---

# 169. Projection Update Strategy

For synchronous financial screens, update period balance in posting transaction OR use safe materialized aggregation depending on benchmark.

Decision based on performance/consistency tests.

---

# 170. Strong Consistency for Trial Balance

After successful posting, accountant expects Trial Balance to reflect it.

Therefore core balance read model should prefer immediate consistency if feasible.

---



# 171. General Ledger Search Projection

Useful read model:

```text
GeneralLedgerSearchProjection
{
  TenantId
  LegalEntityId
  PostingDate
  EntryNumber
  AccountId
  AccountCode
  AccountName
  Debit
  Credit
  BaseCurrency
  SourceModule
  SourceRef
  PartyRef?
  JournalId
  DimensionKeys
}
```

This is optimized for accountant search/listing.

---

# 172. Projection Is Not Editable

No accounting screen edits projection rows.

Corrections always go through source/reversal/Journal workflow.

---

# 173. Projection Rebuild

Must support:

```text
Rebuild by Legal Entity
Rebuild by Fiscal Year
Rebuild by Period
```

with reconciliation checks.

---

# 174. Projection Drift Detection

Periodic/background integrity check:

```text
SUM(JournalLines)
vs
PeriodBalance Projection
```

alerts on mismatch.

---

# 175. Trial Balance

Trial Balance is derived from posted Journal Lines / strongly consistent balance projection.

Core invariant:

```text
Total Debits == Total Credits
```

for selected accounting scope.

---

# 176. Trial Balance Scope

Filter by:

```text
Legal Entity
Date/Period
Account ranges
Dimensions
Currency display context
```

---

# 177. General Ledger Report

Shows detailed posted lines in accounting order.

Supports:

- date;
- account;
- journal;
- source;
- party;
- dimension;
- entry number.

Server-side pagination required.

---

# 178. Running Balance

Compute using efficient indexed window/read model approach.

Do not load all historical rows into application memory.

---

# 179. Pagination

Prefer keyset/cursor pagination for deep high-volume General Ledger browsing.

Avoid deep OFFSET at millions of lines.

---

# 180. Sort Key

Stable accounting browse order:

```text
PostingDate
JournalEntry sequence/number
JournalEntryLine sequence
Id
```

Exact key locked in implementation.

---

# 181. Posting Sequence

Accounting sequence can provide deterministic ordering/audit.

Human Entry Number may be Journal/Entity/Fiscal-year scoped.

---

# 182. Entry Number vs Posting Date

Do not assume sequence perfectly follows document date under concurrent posting/backdating.

Both are retained.

---

# 183. High-Concurrency Numbering

Use Platform Numbering transactionally.

No `MAX(number)+1`.

---

# 184. Entry Posting Atomicity

Journal Entry header + all lines + Fact link + audit/outbox are one transaction.

No partially posted Entry.

---

# 185. Posting Failure

Any validation/DB failure rolls back all posting effects.

---

# 186. Retry

Client/network retry uses idempotency key.

Returns same posted result.

---

# 187. Posting Result

```text
PostingResult
{
  FactId
  JournalEntryId
  EntryNumber
  PostingDate
  Status
  ExistingPosting = true/false
}
```

---

# 188. Posting Command Concurrency

Use DB uniqueness/idempotency to protect against two nodes posting same Fact simultaneously.

Application locks alone are insufficient.

---

# 189. Unique Fact Constraint

Example:

```text
UNIQUE (
 tenant_id,
 legal_entity_id,
 source_module,
 source_ref,
 fact_type,
 fact_version
)
```

or normalized idempotency key.

---

# 190. Posting Rules and Concurrency

Posting Rule version resolved once before line generation and recorded.

---

# 191. Configuration Version Snapshot

Posted Entry stores:

```text
PostingRuleVersion
AccountDeterminationVersion/Mapping IDs
FinancialPolicyVersion
```

where relevant.

---

# 192. Account Mapping Changes

Future posting uses new mapping.

Old Entry does not change account.

---

# 193. Reclassification After Mapping Error

If historical posting was wrong:

- reversal + corrected repost;
- or adjusting Journal.

Never remap old lines in place.

---

# 194. Posting Explanation

Finance user can open:

```text
Why was this posted here?
```

Response includes:

```text
Source document
Fact type
Posting Rule
Account Role
Account Mapping
Dimensions
Amounts
FX snapshot
```

---

# 195. Source Navigation

From Journal Entry:

```text
View Source
```

routes to source Invoice/Payment/etc if authorized.

---

# 196. Reverse Navigation

From source document:

```text
Accounting
→ Journal Entry
```

for finance-authorized users.

---

# 197. Source Deletion

A source document with posted Entry cannot be hard deleted.

Archive/correction according to domain.

---

# 198. Ledger Retention

Posted accounting data retained according to statutory/company retention.

No normal hard-delete.

---

# 199. GDPR/Privacy Boundary

Ledger should reference Party via stable IDs and snapshots only where needed.

Do not duplicate unnecessary personal detail into Journal Lines.

---

# 200. Audit Actor

PostedBy supports actors:

```text
Human
System
Automation
AI Tool mediated action
Service
Migration
```

consistent with Platform actor model.

---

# 201. System Posting

Automatic posting records:

```text
ActorType = System/Automation
InitiatedBy? = human/source command
```

for traceability.

---

# 202. General Journal Approval

Optional/enterprise:

```text
Draft
Submitted
Approved
Posted
```

application workflow before canonical Journal Entry.

Not required for every SME.

---

# 203. Segregation of Duties

High-risk optional policies:

```text
Preparer != Approver
Approver != Poster
```

especially Manual Journals.

---

# 204. Automated Source Document Posting

Invoice posting normally does not require separate Ledger approval if source workflow already approved.

Avoid duplicate approvals.

---

# 205. Opening Balances

Opening balances are migration/go-live facts.

Do not create fake historical invoices merely to populate GL.

---

# 206. Opening General Ledger Fact

```text
OpeningBalancePosted
```

contains account opening amounts/dimensions/currency context.

---

# 207. Opening AR/AP

General opening control balance alone is insufficient if Subledger is active.

Need:

```text
GL opening control balance
+
Subledger open items
```

with reconciliation.

`10 Subledger` defines detail.

---

# 208. Opening Inventory

Inventory module owns opening quantity/value detail and emits Accounting Facts.

Do not post inventory opening solely as unexplained GL amount if Inventory module is active.

---

# 209. Opening Bank/Cash

Can be imported as Ledger/Payments opening according to Payments design.

---

# 210. Opening Date

Use explicit Financial Go-Live boundary.

Example:

```text
Go-live: 2027-01-01
Opening posting date: 2027-01-01 or prior closing date per migration policy
```

No arbitrary mixing.

---

# 211. Opening Import Preview

Finance user sees:

```text
Account
Debit
Credit
Dimensions
Currency
Validation
```

and total balance check before posting.

---

# 212. Opening Balance Invariant

Total opening debits = total opening credits.

---

# 213. Unbalanced Legacy Trial Balance

Import blocks.

If business decides to use Suspense, it must be explicit and approved.

---

# 214. Legacy Source References

Opening/import entries can preserve:

```text
legacy system
legacy account code
batch ID
migration file hash
```

---

# 215. Migration Idempotency

Re-running same migration batch cannot duplicate opening postings.

---

# 216. Migration Reconciliation Report

Shows:

```text
Legacy Trial Balance
TODIR Opening Trial Balance
Differences
```

before go-live approval.

---

# 217. Go-Live Lock

After migration approval:

Opening batch locked.

Correction through controlled adjustment.

---

# 218. Period Opening Balance

Derived from prior periods.

Do not store manually independent opening for every period.

---

# 219. Year Opening

Balance Sheet accounts carry forward.

P&L close according to year-close process.

---

# 220. Financial Statements

Ledger provides accounting data.

Full statement definitions/report presentation may live Accounting/Reporting layer.

---

# 221. Balance Sheet

Derived from classified accounts and balances.

---

# 222. Profit & Loss

Derived from Revenue/Expense accounts over period.

---

# 223. Trial Balance Is Core

Must exist early as integrity/reporting tool.

---

# 224. Statement Configuration

Account categories/subcategories can support flexible reports without changing account types.

---

# 225. Reporting Currency

Additional presentation currency later.

Base accounting remains Legal Entity Base Currency.

---

# 226. Consolidation

Group consolidation later.

Do not make Ledger cross-entity by default.

Each Legal Entity has independent balanced books.

---

# 227. Intercompany

Intercompany business processes later create entries in each Legal Entity.

Never one Journal Entry spanning two Legal Entities.

---

# 228. Intercompany Invariant

Each Entity entry independently:

```text
Debit = Credit
```

Intercompany reconciliation is separate.

---

# 229. Journal Entry Cannot Span Legal Entities

DB/domain constraint.

---

# 230. Branch

Branch can be dimension/context, not separate Ledger unless legally required.

---

# 231. Multiple Books / GAAP Bases

Some enterprises require multiple accounting books.

Do NOT implement Day 1.

Design `BookId` seam only if it does not complicate MVP excessively.

Initial:

```text
one Primary Book per Legal Entity
```

---

# 232. Ledger Book Concept

Potential future:

```text
LedgerBook
{
  Primary
  LocalGAAP
  IFRSAdjustment
  Management
}
```

Not built until requirement proven.

---

# 233. Current Decision

No speculative multi-book complexity in initial implementation.

---

# 234. Journal Templates / Recurring Journals

Useful accounting capability.

General Journal application can support:

```text
Standard Journal Template
Recurring Journal
```

later after core Posting Engine stable.

---

# 235. Accruals

Manual/automated accrual posting can schedule reversal.

---

# 236. Deferred Revenue/Expense

Later dedicated schedules/module/application.

Do not bake into generic Ledger Posting Rule.

---

# 237. Fixed Assets

Asset module calculates depreciation.

Ledger posts Facts.

---

# 238. Payroll

Payroll calculates payroll.

Ledger posts Facts.

---

# 239. Expenses

Expense module validates business expense.

Ledger posts Facts.

---

# 240. Inventory

Inventory calculates valuation.

Ledger posts Facts.

---

# 241. Ledger Must Not Become Business Logic Dump

If Posting Rule starts calculating:

```text
stock quantity
payroll taxes
invoice discounts
```

architecture is broken.

---

# 242. Posting Rule Responsibility Test

Posting Rule may answer:

> Which accounting legs correspond to this already-calculated business fact?

It must not answer:

> What is the business transaction amount?

---

# 243. Accounting Fact Completeness

Fact must contain enough semantic financial information to post without re-reading mutable business tables.

---

# 244. Fact Source Snapshot

Fact can reference source snapshot IDs/version.

Posting should not fetch current Product/Party values to reconstruct historical amount.

---

# 245. Fact Validation Against Source

Within synchronous transaction, source module can guarantee Fact matches source state.

Ledger does not duplicate business validation.

---

# 246. Fact Immutability

Once accepted/posted:

Fact payload immutable.

---

# 247. Fact Correction

New corrective/reversal Fact.

---

# 248. Fact Status

```text
Received
Validated
Posted
Rejected
Reversed? // usually Entry state/link rather than Fact mutation
```

---

# 249. Rejected Fact

For synchronous financial finalization:

whole transaction rolls back, so persistent rejected Fact may not exist.

For async processing:

Inbox can store error state.

---

# 250. Posting Errors

Stable codes:

```text
ledger.fact_unsupported
ledger.fact_duplicate_conflict

ledger.posting_date_invalid
ledger.period_not_found
ledger.period_locked

ledger.posting_rule_missing
ledger.account_mapping_missing
ledger.account_mapping_conflict
ledger.account_not_postable
ledger.account_archived

ledger.dimension_missing
ledger.dimension_invalid

ledger.entry_unbalanced
ledger.currency_mismatch
ledger.fx_snapshot_missing

ledger.control_account_manual_post_blocked

ledger.reversal_not_allowed
ledger.already_reversed

ledger.opening_unbalanced
```

---

# 251. Business-Friendly Error

Instead of:

```text
AccountMappingNotFoundException
```

show:

> "تعذر ترحيل المستند لأن حساب إيرادات فئة الإلكترونيات غير مُحدد."

with link for authorized Finance Admin.

---

# 252. Managed Mode Auto-Healing

Managed Mode may auto-seed missing standard mapping if safe and previously approved configuration template supports it.

Do not invent account mapping at runtime.

---

# 253. Posting Configuration Setup Wizard

For Full Mode:

```text
Chart of Accounts
Default Journals
AR/AP Control
Sales/Purchase mappings
Tax mappings
Inventory mappings if enabled
Retained Earnings
FX Gain/Loss
Rounding
```

---

# 254. Managed Mode Setup

Country Pack + system defaults configure most items automatically.

User sees minimal setup.

---

# 255. Accounting Readiness

Per Legal Entity:

```text
CanPostInvoices
CanPostPayments
CanPostInventory
CanClosePeriod
```

based on mappings/periods/modules.

---

# 256. Module Activation Readiness

When Invoicing activates:

Ledger validates all required Invoice Fact account roles mapped.

---

# 257. Inventory Activation Readiness

Inventory validates required asset/COGS/variance roles.

---

# 258. Payroll Activation Readiness

Payroll validates expense/payable roles.

---

# 259. Module Deactivation

Ledger history remains.

Mappings can become inactive for future use only after dependents/wind-down.

---

# 260. Chart of Accounts Import

First-class migration/admin feature.

Fields:

```text
Code
Name
Type
Parent
Allow Direct Posting
Subtype
Currency policy?
```

---

# 261. CoA Import Validation

- unique codes;
- valid tree;
- no cycles;
- valid types;
- no posting under invalid parent;
- required root/categories.

---

# 262. CoA Tree Cycle

Block activation/import.

---

# 263. CoA Export

Excel/CSV.

---

# 264. Account Mapping Import

Advanced Finance setup can import mappings into Draft version.

Preview conflicts before activation.

---

# 265. Mapping Version Activation

Same safe pattern:

```text
Draft
Validate
Activate
```

Future postings use new mapping.

---

# 266. No Live Mass Update of Mapping Rows

Avoid partial configuration state.

---

# 267. Accounting Configuration Version

Could group related mappings:

```text
AccountingConfigurationVersion
```

so posting gets one coherent set.

Recommended.

---

# 268. Configuration Atomicity

A new Accounting Configuration becomes active atomically.

---

# 269. Posting Context Uses Active Config Version

Resolve once at command start.

Store on Entry.

---

# 270. Concurrent Config Activation

In-flight posting can finish with old immutable config version.

New posting uses new version.

Safe.

---

# 271. Data Model Schema

Use:

```text
finance_ledger
```

or project naming convention equivalent.

Keep separate from `foundation_finbase`.

---

# 272. Core Tables

```text
ledger_book
account
account_category

journal

fiscal_year
accounting_period
financial_lock

accounting_fact

posting_rule
posting_rule_version

account_role
account_mapping
account_mapping_version
accounting_configuration_version

journal_entry
journal_entry_line

dimension_definition
dimension_value
journal_line_dimension

general_journal_draft
general_journal_draft_line

account_period_balance
```

Some tables can be simplified after implementation review.

---

# 273. `account`

Key indexes:

```text
(tenant_id, legal_entity_id, code)
(parent_account_id)
(type/status)
```

---

# 274. `journal_entry`

Key fields/indexes:

```text
tenant_id
legal_entity_id
posting_date
journal_id
entry_number
source_module
source_ref
fact_type
fact_version
reversal_of
```

---

# 275. `journal_entry_line`

Indexes:

```text
(tenant_id, legal_entity_id, account_id, posting_date/entry relation)
(journal_entry_id, sequence)
(party_ref where useful)
```

Avoid redundant indexes until query plans measured.

---

# 276. Denormalized Posting Date on Line?

Potential performance optimization:

Store:

```text
posting_date
```

also on Journal Line to avoid header join for GL queries.

This is acceptable controlled denormalization if enforced from Entry and immutable.

Recommended if benchmark confirms.

---

# 277. Legal Entity on Line?

Likewise can denormalize tenant/entity for:

- RLS;
- indexes;
- partition readiness;
- query speed.

Recommended.

---

# 278. Composite Tenant FKs

Use tenant-aware references to prevent cross-tenant accidental links.

---

# 279. RLS

All tenant-owned Ledger tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 280. Runtime DB Role

No ownership, no BYPASSRLS.

---

# 281. `SET LOCAL`

Every DB transaction sets tenant context as Platform Kernel defined.

---

# 282. Legal Entity Authorization

RLS handles Tenant.

Application authorization also enforces user Entity scope.

---

# 283. RLS Performance

Indexes lead/include TenantId appropriately.

Never disable RLS for Ledger speed.

---

# 284. Table Partitioning

Do NOT partition Day 1 by reflex.

Ledger can reach large volumes, but partitioning adds operational complexity.

Benchmark first.

---

# 285. Partition Readiness

Schema/query design should permit future partitioning by:

```text
Cell / Tenant cohort
or LegalEntity/time
```

without semantic redesign.

---

# 286. UUIDv7

Use UUIDv7 for primary IDs where approved.

Improves insertion locality compared with random UUIDv4.

---

# 287. Numeric Types

Per `06`:

```text
PostgreSQL numeric
C# decimal
```

No floating point.

---

# 288. Base Amount Columns

Use approved money storage precision.

Do not let DB scale silently define financial rounding.

---

# 289. Append-Heavy Design

Journal Entries/Lines are append-heavy/immutable.

This is favorable for PostgreSQL and audit.

---

# 290. Update Patterns

Updates primarily metadata/state links:

```text
reversed_by
status
```

even these should be minimal/audited.

Original amounts/accounts immutable.

---

# 291. Reversal Status

Original Entry can set:

```text
IsReversed / ReversedBy
```

without changing financial content.

---

# 292. Immutable Enforcement

Application + DB permissions/triggers where useful.

Consider DB trigger rejecting UPDATE/DELETE of financial columns after insert.

ADR after testing migration/admin implications.

---

# 293. Production DBA Access

Emergency DBA can technically modify DB, but process/audit controls treat this as break-glass incident.

Application should not rely on direct edits.

---

# 294. Posting Performance Target

Typical simple 3–10 line financial posting should be very fast locally.

Initial backend target:

```text
p95 core posting <= 50–100ms
```

excluding unrelated external integrations, under realistic concurrency.

Validate with tests.

---

# 295. Batch Posting

Support:

```text
PostFactsBatch
```

for migration/background batch where semantics allow.

Each Fact remains idempotent/atomic.

---

# 296. Bulk Invoice Posting

Business layer may post many documents.

Do not create one giant transaction for thousands of independent invoices.

Chunk safely.

---

# 297. Posting Query Count Budget

Posting simple Invoice should not query Account mappings per generated line independently.

Resolve mapping context/batch.

---

# 298. Mapping Cache

Active immutable Accounting Configuration can be cached in memory.

Key:

```text
Tenant
LegalEntity
ConfigVersion
```

---

# 299. Account Cache

Postable Account metadata can be cached by immutable/config version.

---

# 300. Period Cache

Open Fiscal Period/locks cacheable with version/event invalidation.

---

# 301. Cache Correctness

Posting checks authoritative version/state on critical close/config transitions as needed.

No stale cache posting into closed period.

---

# 302. Period Close Cache Invalidation

Period close command invalidates/broadcasts immediately.

---

# 303. Multi-Instance

Outbox/RabbitMQ event:

```text
LedgerConfigurationChanged
AccountingPeriodStatusChanged
```

invalidates process caches.

---

# 304. Broker Outage

Critical close/config transaction succeeds in DB + Outbox.

Other app nodes must have safe TTL/version check so stale cache cannot violate lock for long.

For period close, consider DB/version check in posting hot path even with cache.

Correctness beats microseconds.

---

# 305. Lock Check Performance

Indexed single-row/small policy lookup + local cache/version.

No complex history scan per posting.

---

# 306. Account Determination Performance

Compile active mappings into decision structure.

No arbitrary SQL per role.

---

# 307. Posting Rule Performance

Strongly typed code/compiled rule data.

No dynamic reflection-heavy rule engine in hot path.

---

# 308. JSON Fact Performance

Deserialize once into typed contract.

Do not repeatedly parse JSON for each posting leg.

---

# 309. Ledger Write Amplification

A 10-line entry produces predictable rows.

Audit/outbox kept compact.

Do not emit event per Journal Line.

---

# 310. Integration Event

Meaningful:

```text
JournalEntryPostedV1
JournalEntryReversedV1
PeriodClosedV1
PeriodReopenedV1
AccountingConfigurationActivatedV1
```

---

# 311. Journal Entry Posted Event

Carries summary/reference, not every line unless consumer contract genuinely needs lines.

Consumers can query authorized Ledger read API.

---

# 312. Audit

Audit configuration/commands:

- account create/archive;
- mapping activation;
- journal change;
- period close/reopen;
- manual journal submit/approve/post;
- reversal;
- opening import.

---

# 313. Posted Entry Itself Is Audit Evidence

Do not duplicate every line into generic Audit table.

---

# 314. Observability Metrics

```text
ledger_post_duration
ledger_post_lines
ledger_post_failures_total
ledger_unbalanced_rejections_total
ledger_mapping_missing_total
ledger_period_locked_total
ledger_idempotency_hits_total
ledger_reversal_total
ledger_projection_lag
ledger_projection_reconciliation_errors
```

No Tenant ID high-cardinality labels.

---

# 315. Tracing

Posting trace:

```text
Fact received
Period resolved
Config version
Posting rule
Account determination timing
Line generation
Balance validation
DB write
```

---

# 316. Sensitive Logging

Do not log complete financial payload by default.

Use IDs, amounts only according to log data policy.

---

# 317. General Ledger API

Base:

```text
/api/v1/ledger
```

---

# 318. Account APIs

```text
GET /accounts
POST /accounts
GET /accounts/{id}
PUT /accounts/{id}
POST /accounts/{id}/archive
```

mutability rules enforced.

---

# 319. Journal APIs

```text
GET /journals
POST /journals
```

---

# 320. Period APIs

```text
GET /fiscal-years
GET /periods
POST /periods/{id}/soft-close
POST /periods/{id}/close
POST /periods/{id}/reopen
```

---

# 321. Entry APIs

```text
GET /entries
GET /entries/{id}
POST /entries/{id}/reverse
GET /entries/{id}/explanation
```

No generic PUT Entry.

---

# 322. Posting Internal API

Hot path is in-process:

```text
ILedgerPostingEngine
```

not HTTP inside Modular Monolith.

---

# 323. Posting Preview Contract

```text
PreviewPosting(AccountingFact)
```

returns generated Entry DTO without persistence.

---

# 324. General Journal APIs

```text
POST /general-journals
PUT /general-journals/{id}
POST /general-journals/{id}/submit
POST /general-journals/{id}/approve
POST /general-journals/{id}/post
```

depending on configured workflow.

---

# 325. Trial Balance API

```text
GET /reports/trial-balance
```

server-side filtered/read model.

---

# 326. General Ledger Report API

```text
GET /reports/general-ledger
```

cursor pagination.

---

# 327. Account Balance API

```text
GET /accounts/{id}/balance
```

period/date/dimension filters.

---

# 328. Contracts Project

```text
Todir.Finance.Ledger.Contracts
```

---

# 329. Key Contracts

```text
AccountingFact
AccountingFactEnvelope
PostingResult
PostingPreview
JournalEntrySummary
AccountReference
DimensionContext
AccountingFactType
```

---

# 330. Fact Producers Depend on Contracts Only

No implementation references.

---

# 331. Ledger Module Manifest

Concept:

```yaml
module:
  id: ledger
  layer: finance
  type: business-foundation/managed-financial-kernel
  purchasable: false_directly
depends:
  hard: [platform-kernel, finbase]
  soft: [directory, tax]
owns:
  schema: finance_ledger
```

Exact manifest follows `03`.

---

# 332. Activation

Ledger auto-activates in Managed Mode when financial module requiring posting activates.

---

# 333. Deactivation

Cannot deactivate while any financial dependent module remains active.

After full financial wind-down:

read-only retention.

---

# 334. Ledger Go-Live Date

Per Legal Entity.

Blocks normal business posting before migration/go-live boundary unless explicit opening/import.

---

# 335. Financial Go-Live Relationship

Coordinates with FinBase Base Currency lock.

Once Ledger financial history exists:

Base Currency normal edit blocked.

---

# 336. Account Determination Config Before Go-Live

Mandatory mapping health check.

---

# 337. Opening Balance Before Normal Posting

Migration strategy defines whether opening batch must be approved before transactions.

Recommended.

---

# 338. Security Permissions

Initial:

```text
ledger.account.read
ledger.account.manage

ledger.journal.read
ledger.journal.manage

ledger.entry.read
ledger.entry.reverse
ledger.entry.explain

ledger.general_journal.create
ledger.general_journal.edit
ledger.general_journal.submit
ledger.general_journal.approve
ledger.general_journal.post

ledger.period.read
ledger.period.soft_close
ledger.period.close
ledger.period.reopen

ledger.lock.read
ledger.lock.manage

ledger.dimension.read
ledger.dimension.manage

ledger.mapping.read
ledger.mapping.manage
ledger.mapping.activate

ledger.opening.read
ledger.opening.manage
ledger.opening.post

ledger.audit.view
ledger.reports.read
```

---

# 339. No Generic `ledger.entry.edit`

By design.

---

# 340. Step-Up Authentication

Consider for:

- period reopen;
- large manual journal;
- configuration activation;
- reversal in closed-sensitive context.

Configurable risk policy.

---

# 341. AI Restrictions

AI can:

- explain an Entry;
- suggest an Account Mapping draft;
- identify suspicious imbalance in imported draft;
- suggest classification.

AI cannot:

- directly write Journal Entry;
- change posted Entry;
- reopen period;
- activate mapping;
- post manual journal without Application Command + permission/approval.

---

# 342. Manual Journal Amount Limits

Optional policy:

```text
User role can post up to X
Above requires approval
```

not hardcoded.

---

# 343. Source Document Posting Permissions

Source module permission controls source operation.

Ledger does not require user also possess generic manual `ledger.entry.post`.

System posting is authorized through source command + Ledger internal capability.

---

# 344. Defense Against Forged Facts

Only registered module/application code can submit Fact type.

Public REST clients cannot post arbitrary AccountingFact directly unless a tightly controlled integration contract exists.

---

# 345. Fact Authorization

Posting Context includes trusted source identity/module.

---

# 346. Integrity Hash

Optional persisted Fact payload hash.

Not blockchain.

No need cryptographic chain unless legal requirement.

---

# 347. ICV/Hash Chains

Belong document compliance/numbering where required, not generic Ledger gimmick.

---

# 348. Test Strategy — Posting Math

Unit tests:

```text
balanced simple entry
unbalanced rejection
zero line
negative source signs
multi-line revenue
tax legs
rounding difference
```

---

# 349. Test Strategy — Account Determination

```text
default mapping
category override
specific override
tax mapping
missing mapping
same-rank conflict
archived account
non-postable account
```

---

# 350. Test Strategy — Idempotency

```text
same fact same payload → same Entry
same key different payload → conflict
concurrent duplicate → one Entry
retry after response timeout → one Entry
```

---

# 351. Test Strategy — Periods

```text
open
soft closed normal user
soft closed finance adjustment
closed
reopen
backdated
future
fiscal year boundary
```

---

# 352. Test Strategy — Reversal

```text
simple
already reversed
closed original period
auto reversal
reversal balances
source links
```

---

# 353. Test Strategy — Multi-Currency

```text
base currency
foreign invoice
FX snapshot
settlement gain
settlement loss
revaluation
rounding
```

---

# 354. Test Strategy — Dimensions

```text
required
optional
invalid
different line values
archived dimension value
Branch scope
```

---

# 355. Test Strategy — RLS

Tenant A cannot:

- query B accounts;
- query B entries;
- submit Fact to B;
- reference B account IDs.

---

# 356. Test Strategy — Entity Scope

Same Tenant user scoped Entity A cannot read/post Entity B without authorization.

Ledger tables still tenant RLS; application Entity scope tested separately.

---

# 357. Test Strategy — Transaction Coordinator

Critical integration:

```text
Invoice save
Ledger fact
Journal Entry
Audit
Outbox
```

forced failure at each step → all rollback.

---

# 358. Test Strategy — Broker Outage

Synchronous posting commits DB.

Outbox remains.

No duplicate Journal Entry after broker recovery.

---

# 359. Test Strategy — Projection

Post → Trial Balance updates.

Rebuild projection → same balances.

Corrupt projection in test → reconciliation detects.

---

# 360. Test Strategy — Opening Migration

```text
balanced batch
unbalanced batch
duplicate import
AR control mismatch
legacy refs
re-run
```

---

# 361. Property Test — Every Posted Entry Balanced

Generate many valid Fact inputs.

Invariant always holds.

---

# 362. Property Test — Reversal Net Zero

For an Entry + exact reversal:

```text
combined financial effect = 0
```

by account/dimension/currency semantics.

---

# 363. Property Test — Candidate Ordering

Randomize Account Mapping insertion order.

Same Determination result.

---

# 364. Property Test — Fact Determinism

Same Fact + Config Version → same generated accounting lines.

---

# 365. Architecture Test — No Journal Entry Creation Outside Ledger

Static project dependency test.

---

# 366. Architecture Test — No Ledger DbContext Outside Module

Fail CI.

---

# 367. Architecture Test — No `double` Financial Fields

Fail public/domain Ledger types.

---

# 368. Architecture Test — No Mutable Entry Endpoint

API contract inspection.

---

# 369. Performance Test Dataset

At least:

```text
1M Journal Lines
10M Journal Lines synthetic
multiple accounts
multiple periods
dimensions
many tenants
```

as environment permits.

---

# 370. Posting Load Test

Concurrent invoices/payments across tenants/entities.

Measure:

```text
p50
p95
p99
DB locks
connection pool
WAL
index overhead
```

---

# 371. General Ledger Query Load

Account with millions of lines.

Cursor pagination.

No table scan.

---

# 372. Trial Balance Load

Hundreds/thousands of accounts and dimensions.

Use balance projection.

---

# 373. Period Close Load

Close should not scan every raw line unnecessarily if readiness checks can use summaries.

---

# 374. Mapping Cache Load

Large category mappings.

Ensure resolution bounded.

---

# 375. Red-Team — Invoice Creates Journal Entry

Reject architecture.

Invoice emits Fact.

---

# 376. Red-Team — Business Module Stores GL Account IDs

Reject unless explicitly an Accounting-owned profile/reference field through contract.

---

# 377. Red-Team — Entry Not Balanced by 0.01

Reject posting.

Do not silently "fix."

---

# 378. Red-Team — Edit Posted Amount

Reject.

---

# 379. Red-Team — Delete Posted Entry

Reject.

---

# 380. Red-Team — Duplicate RabbitMQ Message Creates Entry

Must not happen.

---

# 381. Red-Team — Retry HTTP Creates Entry Twice

Must not happen.

---

# 382. Red-Team — Current Account Mapping Changes Old Report

Journal line retains AccountId; history unchanged.

---

# 383. Red-Team — Current FX Changes Old Entry

History unchanged.

---

# 384. Red-Team — Period Closed but Cache Stale

Posting still blocked.

---

# 385. Red-Team — Manual Journal to AR Control

Blocked unless explicit authorized reconciliation-safe workflow.

---

# 386. Red-Team — One Entry Spans Entities

Blocked.

---

# 387. Red-Team — Tenant A Account ID in Tenant B Fact

RLS/scope/FK reject.

---

# 388. Red-Team — Missing Revenue Mapping Uses Suspense Automatically

Reject unless explicit policy.

---

# 389. Red-Team — Projection Becomes Accounting Truth

Reject.

Projection is rebuildable.

---

# 390. Red-Team — General Ledger Screen Loads All Lines

Reject.

Server-side pagination.

---

# 391. Red-Team — Account Tree Cycle

Block.

---

# 392. Red-Team — Archived Account Posting

Block.

---

# 393. Red-Team — Non-Postable Parent Account Posting

Block.

---

# 394. Red-Team — Posting Before Go-Live

Block except approved opening/migration operation.

---

# 395. Red-Team — Reversal Deletes Original

Reject.

---

# 396. Red-Team — Accounting Fact Contains arbitrary debit/credit from Sales

Reject Fact contract.

---

# 397. Business Acceptance — Small Company

User posts Invoice.

User sees:

```text
Invoice Posted
```

No debit/credit complexity.

TODIR internally posts balanced entry.

---

# 398. Business Acceptance — Accountant

Opens Invoice → Accounting:

```text
Dr Accounts Receivable  1,140
Cr Sales Revenue        1,000
Cr VAT Payable            140
```

---

# 399. Business Acceptance — Why This Account?

Accountant opens Revenue line.

TODIR explains mapping.

---

# 400. Business Acceptance — Mapping Change

Finance changes next-period Revenue mapping.

Old invoices remain old account.

New invoices use new account.

---

# 401. Business Acceptance — Closed Period

Salesperson tries backdated post into closed month.

Clear business message; no posting.

---

# 402. Business Acceptance — Finance Adjustment

Authorized Accountant creates General Journal draft, reviews/approves/posts.

Immutable Entry created.

---

# 403. Business Acceptance — Error Correction

Posted Entry wrong.

Reversal + corrected posting.

Audit trail visible.

---

# 404. Business Acceptance — Duplicate Request

User double-clicks Post / network retries.

One Journal Entry.

---

# 405. Business Acceptance — Multi-Currency Invoice

USD invoice.

Ledger shows USD context + EGP base amount from immutable snapshot.

---

# 406. Business Acceptance — FX Settlement

Later settlement produces explicit FX Gain/Loss posting.

Original invoice unchanged.

---

# 407. Business Acceptance — Opening Migration

Legacy Trial Balance imported.

Balanced preview.

Approval.

Opening Entry/Entries posted idempotently.

---

# 408. Business Acceptance — Dimensions

Sales posted to:

```text
Revenue
Branch = Cairo
Department = Retail
```

Reports can filter without separate Revenue accounts.

---

# 409. Business Acceptance — Managed Mode

Small business never sees CoA setup after onboarding but financial statements remain valid.

---

# 410. Business Acceptance — Upgrade to Full Accounting

Enable Accounting subscription/experience.

Existing entries appear immediately.

No migration.

---

# 411. Business Acceptance — Trial Balance

After postings:

```text
Total Debit = Total Credit
```

and values reconcile with Journal Entries.

---

# 412. Business Acceptance — Subledger Control

After `10` implementation:

AR/AP control balances reconcile with Open Items.

---

# 413. Product UX — Entry Detail

Recommended sections:

```text
Header
Source
Lines
Dimensions
Accounting Explanation
Audit/Reversal
```

---

# 414. Product UX — Account Workspace

Tabs:

```text
Overview
Activity
Balance by Period
Dimensions
Configuration
Audit
```

permission-aware.

---

# 415. Product UX — Chart of Accounts

Tree + flat searchable view.

Avoid forcing accountant to navigate huge tree for every action.

---

# 416. CoA Search

Search:

```text
code
Arabic name
English name
```

PostgreSQL/indexed.

---

# 417. Product UX — Period Close

Checklist-style:

```text
Ready checks
Warnings
Blockers
Close action
```

not a mysterious button.

---

# 418. Product UX — Posting Preview

Before sensitive manual source finalization:

```text
Preview accounting
```

for accountant roles.

Normal salesperson never needs it.

---

# 419. Product UX — Managed Accounting

Hide:

```text
Debits
Credits
Posting rules
Mappings
```

unless role/Accounting experience requires.

---

# 420. Implementation Order

```text
LG00 Ledger ADRs + Domain Boundaries
LG01 Schema + RLS
LG02 Account / CoA
LG03 Journal Definitions
LG04 Fiscal Years / Periods / Locks
LG05 Accounting Fact Catalog
LG06 Fact Inbox / Idempotency
LG07 Posting Rule Model
LG08 Account Roles + Determination
LG09 Deterministic Posting Engine
LG10 Balance Validation + Persistence
LG11 Transaction Coordinator Integration
LG12 Reversal Engine
LG13 Dimensions
LG14 Multi-Currency Posting
LG15 Manual General Journal
LG16 Opening Balances / Migration
LG17 Balance / GL Read Models
LG18 Trial Balance + General Ledger Queries
LG19 Managed Ledger Setup
LG20 Full Accounting UX Contracts
LG21 Outbox / Events / Cache Invalidation
LG22 Security / Permissions / Audit
LG23 Performance / Projection Reconciliation
LG24 Cross-Module Fact Contract Tests
LG25 Product + Accountant Acceptance
```

---

# 421. LG00 Exit Gate

ADRs approved:

- Facts vs Journal Entries.
- General Ledger vs Subledger.
- immutable Ledger.
- synchronous posting boundaries.
- account determination.
- Managed vs Full mode.

---

# 422. LG01 Exit Gate

- `finance_ledger`.
- FORCE RLS.
- runtime role no bypass.
- tenant-aware FKs.
- Testcontainers isolation tests.

---

# 423. LG02 Exit Gate

- CoA tree.
- account types.
- postable leaf behavior.
- archive.
- no cycles.

---

# 424. LG03 Exit Gate

- journals per Entity.
- stable codes.
- numbering integration.

---

# 425. LG04 Exit Gate

- custom fiscal year.
- periods.
- soft/hard locks.
- backdate tests.

---

# 426. LG05 Exit Gate

Versioned Fact contracts for current + foreseeable modules.

No arbitrary debit/credit Fact escape hatch.

---

# 427. LG06 Exit Gate

- unique idempotency.
- duplicate same payload safe.
- different payload conflict.
- concurrency test.

---

# 428. LG07 Exit Gate

Posting Rule versioning/immutability.

---

# 429. LG08 Exit Gate

- role-based account mapping.
- deterministic specificity.
- missing/conflict behavior.
- "Why this account?"

---

# 430. LG09 Exit Gate

Pure deterministic line generation from Fact + config.

---

# 431. LG10 Exit Gate

- every Entry balanced.
- atomic header/lines/fact.
- immutable amounts.

---

# 432. LG11 Exit Gate

Cross-DbContext invoice-style POC:

all commit/all rollback.

---

# 433. LG12 Exit Gate

Reversal leaves original and creates exact inverse accounting effect.

---

# 434. LG13 Exit Gate

Dimensions validated/propagated without CoA explosion.

---

# 435. LG14 Exit Gate

Base + transaction currency snapshots exact.

---

# 436. LG15 Exit Gate

Manual Journal Draft/approval/posting separate from canonical Entry.

Control account policy.

---

# 437. LG16 Exit Gate

Balanced opening import + reconciliation + idempotency.

---

# 438. LG17 Exit Gate

Rebuildable balance/read projections.

---

# 439. LG18 Exit Gate

Trial Balance and GL performant at stress dataset.

---

# 440. LG19 Exit Gate

SME can activate financial modules without manual CoA complexity.

---

# 441. LG20 Exit Gate

Accountant can inspect/manage advanced accounting safely.

---

# 442. LG21 Exit Gate

Outbox events and multi-instance cache invalidation.

---

# 443. LG22 Exit Gate

Permissions/SoD/audit/RLS/security pass.

---

# 444. LG23 Exit Gate

- posting p95 measured;
- GL query plans;
- projection reconciliation;
- no N+1.

---

# 445. LG24 Exit Gate

Contract samples for:

```text
Invoicing
Payments
Inventory
Payroll
Tax
```

prove Ledger API can support future modules without implementation coupling.

---

# 446. LG25 Acceptance

Senior Accountant + Product Architect approve:

- postings;
- reversal;
- periods;
- trial balance;
- managed mode;
- full mode;
- explanations;
- opening migration.

---

# 447. Build Now — MUST

- Account/CoA.
- Journal.
- Fiscal year/period/locks.
- Accounting Fact Catalog.
- idempotency.
- Posting Rules.
- Account Roles/Determination.
- Posting Engine.
- balance validation.
- immutable Journal Entries.
- reversal.
- Dimensions base.
- multi-currency base/transaction amounts.
- General Journal.
- Opening GL balances.
- Trial Balance.
- General Ledger report.
- balance projection/read model.
- Managed Mode.
- RLS/security/audit.
- performance tests.

---

# 448. Design Now / Build Later

- multi-book accounting.
- consolidation.
- advanced intercompany eliminations.
- recurring journals.
- deferred revenue/expense schedules.
- budgets.
- sophisticated allocations.
- additional reporting currencies.
- automated period-close orchestration.
- advanced statutory financial statement formats.

---

# 449. Explicitly NOT Ledger

- Customer collection.
- Supplier payment allocation.
- Bank reconciliation.
- Invoice tax calculation.
- Product pricing.
- Inventory cost algorithm.
- Payroll calculation.
- e-Invoice compliance submission.
- CRM/Sales business workflow.

---

# 450. Senior Product Rule

> **A non-accountant can run the business without seeing debit/credit, while TODIR still keeps correct books underneath.**

---

# 451. Senior Accounting Rule

> **A posted entry is permanent evidence. Correction adds history; it never erases history.**

---

# 452. Senior Architecture Rule

> **Business modules emit semantic Accounting Facts. Only the Ledger Posting Engine creates Journal Entries.**

---

# 453. Senior Data Rule

> **Journal Entries/Lines are accounting truth. Balance tables and report projections are derived and rebuildable.**

---

# 454. Senior Integrity Rule

> **No posted Entry exists unless it is balanced, mapped, period-valid, tenant-safe and idempotent.**

---

# 455. Senior Performance Rule

> **Speed comes from immutable mappings, cached configuration, batch resolution, append-only writes and balance projections — never from weakening accounting controls.**

---

# 456. Senior UX Rule

> **"Why this account?" must be answerable without reading source code.**

---

# 457. Current ERP Pattern Validation — Business Central

Microsoft Business Central currently documents Posting Groups as the mechanism that maps business entities/context such as customers, vendors, items and sales/purchase documents to General Ledger accounts.

This validates the business requirement for Account Determination separate from operational documents.

TODIR's model goes further by formalizing this as:

```text
Accounting Fact
→ Account Role
→ Versioned Account Determination
→ Journal Entry
```

rather than allowing source modules to own GL accounts.

---

# 458. Current ERP Pattern Validation — Posting Workflow

Business Central documents posting as the accounting action that records business documents/journals into ledgers and supports Posting Preview.

TODIR adopts the same important product expectation:

- business user acts on a document;
- accounting consequence is generated;
- finance can preview/explain where needed.

---

# 459. Current ERP Pattern Validation — Immutable Ledger

ERPNext's current Ledger documentation states that submitted ledger rows cannot be edited directly and corrections go through cancellation/amendment or Journal Entry.

Its Immutable Ledger keeps original entries and creates reversal entries rather than silently rewriting history.

TODIR locks this principle at the architecture level.

---

# 460. Current ERP Pattern Validation — General vs Subledger

ERPNext's current General Ledger documentation explicitly notes that GL detail does not replace AR/AP reports when the question is which invoices remain outstanding.

This validates TODIR's separation:

```text
Ledger → control/general accounting
Subledger → Party/Open Item detail
```

---

# 461. Current ERP Pattern Validation — Dimensions

ERPNext documents Accounting Dimensions as a way to analyze business context without creating separate GL Accounts for each region/product/department.

TODIR adopts this pattern but keeps dimensions as Ledger-owned typed analytical context rather than arbitrary dynamic fields.

---

# 462. Source Register — Checked 2026-10-05

## Microsoft Dynamics 365 Business Central — Posting Groups

Official:

https://learn.microsoft.com/dynamics365/business-central/finance-posting-groups

Used to validate:

- operational context → GL account mapping;
- business/product posting groups;
- audit/configuration importance.

## Microsoft Dynamics 365 Business Central — Posting Documents and Journals

Official:

https://learn.microsoft.com/en-gb/dynamics365/business-central/ui-post-documents-journals

Used to validate:

- business documents producing accounting ledger effects;
- posting preview pattern.

## Microsoft Dynamics 365 Business Central — General Ledger / Chart of Accounts

Official:

https://learn.microsoft.com/en-us/dynamics365/business-central/finance-general-ledger

Used to validate:

- Chart of Accounts;
- Posting setup relationship.

## Microsoft Dynamics 365 Business Central — General Journals

Official:

https://learn.microsoft.com/en-us/dynamics365/business-central/ui-work-general-journals

Used to validate:

- draft/editable journal input before posting;
- posted entries not being edited like worksheet rows;
- reversing/correcting workflows.

## Microsoft Dynamics 365 Business Central — Accounting Periods

Official:

https://learn.microsoft.com/en-gb/dynamics365/business-central/finance-accounting-periods-and-fiscal-years

Used to validate:

- fiscal years can differ from calendar years;
- accounting period/closing concepts.

## ERPNext — General Ledger

Official:

https://docs.frappe.io/erpnext/general-ledger

Used to validate:

- General Ledger as detailed audit trail;
- source voucher linking;
- GL vs AR/AP distinction;
- direct ledger rows not editable.

## ERPNext — Immutable Ledger

Official:

https://docs.frappe.io/erpnext/immutable-ledger-in-erpnext

Used to validate:

- corrections through reversals;
- preserving original accounting history.

## ERPNext — Accounting Entries

Official:

https://docs.frappe.io/erpnext/accounting-entries

Used to validate:

- operational transactions producing balanced accounting entries.

## ERPNext — Accounting Dimensions

Official:

https://docs.frappe.io/erpnext/accounting-dimensions

Used to validate:

- dimensions for analytical accounting instead of account proliferation.

These sources validate ERP product patterns. Final accounting/tax/statutory behavior for a launch country still requires qualified accounting review and applicable Country Pack requirements.

---

# 463. Final Architecture Model

```text
Invoicing / Payments / Inventory / Payroll / Expenses
                      │
                      │ semantic financial meaning
                      ▼
                Accounting Facts
                      │
                      ▼
              Ledger Posting Engine
                      │
          ┌───────────┴───────────┐
          ▼                       ▼
   Posting Rule            Account Determination
          │                       │
          └───────────┬───────────┘
                      ▼
               Journal Entry
                      │
             Debit == Credit
                      │
                      ▼
            Immutable GL Lines
                      │
        ┌─────────────┴─────────────┐
        ▼                           ▼
 Rebuildable Read Models        Audit/Source Links
        │
        ▼
Trial Balance / GL / Statements
```

---

# 464. Final Business Verdict

TODIR must support two experiences over one accounting truth:

### Business user

```text
Post Invoice
Receive Payment
Ship Goods
Run Payroll
```

TODIR handles accounting automatically.

### Accountant

Can inspect:

```text
What was posted?
Why this account?
Which rule/mapping?
Which period?
Which dimensions?
What source caused it?
How was it reversed?
```

without compromising the original history.

---

# 465. Final Engineering Verdict

The Ledger is approved only when:

```text
No module writes Journal Entries directly.
Every posted Entry is balanced.
Every posting is idempotent.
Every Account is deterministically determined.
Every Entry has source traceability.
Posted history cannot be silently changed.
Period locks are enforced.
Multi-currency snapshots are immutable.
Read models can be rebuilt.
Performance is proven under large Ledger datasets.
```

---

# 466. Next Pack

After Ledger is approved:

> **`10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Its central question:

> **How does TODIR know exactly which Customer/Supplier owes what, which invoice is still open, how partial payments/advances/credits are allocated, and how settlements reconcile perfectly to the General Ledger without putting customer-level detail into the Chart of Accounts?**

The next pack will lock:

- PartyFinancialProfile.
- AR/AP Open Items.
- due schedules.
- settlements/allocations.
- partial payments.
- advances/unapplied cash.
- credit/debit application.
- write-offs.
- realized FX at settlement.
- aging.
- customer/vendor statements.
- control-account reconciliation.
- exposure/credit context.
- performance at millions of open/closed items.

---

**END OF FILE — 09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
