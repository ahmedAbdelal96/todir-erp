# 25 — TODIR ERP SaaS Banking & Cash Management Requirements & Technical Design Pack

**File:** `25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + TREASURY ACCOUNT MANAGEMENT + BANK/CASH/WALLET + RECONCILIATION + CASH POSITION + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, CFO, Treasurer, Cash Manager, Accountant, Bank Reconciliation Specialist, Cash Custodian, POS Lead, Payments Lead, Accounting Lead, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — COMPANY CASH + BANK MASTER DATA + RECONCILIATION + LIQUIDITY + FINANCIAL INTEGRITY**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:

- Ledger owns Chart of Accounts, Journal Entries, posting rules, account determination and period locks.
- Subledger owns Customer/Supplier open items, allocation and settlement.
- Payments owns incoming/outgoing Payment lifecycle, provider operations, refunds and internal money-movement execution.
- Accounting already states that canonical bank statement/matching belongs to a dedicated Banking/Reconciliation capability.
- POS owns cashier Register/Session/Drawer operational accountability and session cash reconciliation.
- Banking must not duplicate POS Session cash.
- Banking must not duplicate Payment records.
- Banking must not create a second Ledger.
- Legal Entity is the legal owner of treasury accounts.
- FinBase owns Money, currency and FX snapshots.

---

# 0. Executive Decision

TODIR Banking & Cash Management is the **canonical treasury-account + external funds truth + reconciliation + cash-position capability**.

Its central question is:

> **Where is the company's money, what does each external source say the balance is, what do TODIR's books say, what remains unmatched or in transit, why does any difference exist, and what cash is expected to move in or out soon?**

This module manages:

```text
Company bank accounts
Cash safes / treasury cash accounts
Digital wallet balances
Provider/gateway settlement accounts
External statements / transaction feeds
Cash counts
Bank/cash/wallet reconciliation
Internal-transfer reconciliation
Cash position
Short-term cash forecast
Treasury account governance
```

It does **not** own:

```text
Customer/Supplier Invoice
Payment lifecycle
AR/AP open balance
POS cashier session
Journal Entry
Payroll calculation
```

---

# 1. Product Principle

> **One clear treasury view outside. Strictly separated financial truths inside.**

---

# 2. Core Distinctions

Absolute:

```text
Treasury Account != GL Account
Treasury Account != Payment
Bank Statement Line != Payment
Bank Statement Line != Journal Entry

Bank Balance != Book Balance
Bank Balance != Available Balance
Book Balance != Reconciled Balance

Imported != Matched
Matched != Reconciled

Actual Cash != Forecast Cash
Cash Position != Cash Forecast

Internal Transfer != Expense
Cross-Entity Transfer != Internal Transfer
```

---

# 3. Why `BankAccount` Alone Is Too Narrow

A real company may hold or control money through:

```text
Bank accounts
Physical safes
Petty cash
Electronic wallets
Payment gateway balances
Merchant settlement balances
Cash-in-transit
```

TODIR must not assume:

```text
all company money = bank account
```

---

# 4. Canonical Abstraction

Recommended business abstraction:

```text
TreasuryAccount
```

Meaning:

> **A company-owned or company-controlled monetary account/location whose balances, movements or settlement state must be visible and reconcilable.**

---

# 5. Treasury Account Types

Initial:

```text
BankAccount
CashAccount
DigitalWallet
SettlementAccount
```

Design seam:

```text
RestrictedCashAccount
InvestmentLiquidityAccount later
```

---

# 6. TreasuryAccount Is Not A Ledger Account

Example:

```text
Treasury Account:
CIB EGP Operating ****4582

Ledger:
Cash at Bank — CIB EGP
```

Different identities.

---

# 7. Why Separation Is Mandatory

Changing:

```text
GL mapping
accounting account structure
reporting dimension
```

must not change the real-world bank account identity.

---

# 8. Existing `12 Payments` Normalization

`12 Payments` currently defines an operational `PaymentAccount` abstraction with:

```text
Bank
Cashbox
ProviderClearing
Wallet
InternalClearing
```

When `25 Banking & Cash Management` becomes active, the architecture is normalized as follows.

---

# 9. Canonical Ownership After `25`

For real company money locations/balances:

```text
Bank                → TreasuryAccount
Cashbox/Safe        → TreasuryAccount
Wallet              → TreasuryAccount
Provider Settlement → TreasuryAccount
```

owned by `25 Banking & Cash Management`.

---

# 10. `PaymentAccount` Compatibility Rule

`PaymentAccount` in Payments becomes:

```text
an operational endpoint/reference/view of an authorized TreasuryAccount
```

not a second independent master record.

---

# 11. No Duplicate Account Masters

Forbidden:

```text
Payments.BankPaymentAccount CIB-001
Banking.TreasuryAccount CIB-001

with independently editable IBAN/currency/status
```

There must be one canonical account master.

---

# 12. Payments Consumes Banking Contract

Recommended:

```text
Todir.Banking.Contracts
```

provides:

```text
TreasuryAccountReference
AllowedPaymentUses
Currency support
LegalEntity
Status
Operational availability
```

---

# 13. Provider Configuration Boundary

Payment gateway:

```text
API credentials
merchant configuration
webhooks
payment attempts
```

remain Payments/provider-integration owned.

---

# 14. Provider Settlement Account Boundary

If provider holds money pending payout:

```text
Provider settlement balance
```

is represented in Banking as:

```text
SettlementAccount
```

while provider API/payment execution remains Payments.

---

# 15. Pure Accounting Clearing Boundary

If an `InternalClearing` concept is only an accounting transit account with no real external balance:

```text
Ledger owns the accounting account
```

Do not create TreasuryAccount merely because a GL clearing account exists.

---

# 16. TreasuryAccount Core

Concept:

```text
TreasuryAccount
{
    Id
    TenantId
    LegalEntityId

    Type
    Code
    Name

    CurrencyMode
    PrimaryCurrency?

    BranchId?
    Purpose

    LifecycleState
    OperationalState

    LiquidityClass

    OpenedOn?
    ClosingStartedOn?
    ClosedOn?

    Version
    RowVersion
}
```

---

# 17. Legal Entity Ownership

Every TreasuryAccount belongs exactly one:

```text
LegalEntity
```

---

# 18. Cross-Legal-Entity Movement

Not ordinary internal transfer.

Requires:

```text
intercompany accounting
two legal-entity movements/documents
explicit due-to/due-from treatment
```

future/intercompany capability.

---

# 19. Treasury Account Purpose

Examples:

```text
GeneralOperations
Collections
SupplierPayments
Payroll
Tax
PettyCash
POSSettlement
GatewaySettlement
Refunds
RestrictedFunds
Other
```

---

# 20. Allowed Uses

Potential:

```text
ReceiveCustomerPayment
PaySupplier
PayrollDisbursement
TaxPayment
Refund
InternalTransfer
CashDeposit
CashWithdrawal
POSSettlement
ProviderPayout
```

---

# 21. Account Currency Mode

Recommended:

```text
SingleCurrency
MultiCurrencyBalance
```

---

# 22. Multi-Currency Balance Pocket

Concept:

```text
TreasuryBalancePocket
{
    TreasuryAccountId
    Currency
    ExternalIdentifier?
    State
}
```

---

# 23. Bank Account Profile

Concept:

```text
BankAccountProfile
{
    TreasuryAccountId
    BankName
    BankCode?
    BranchName?
    BranchCode?
    AccountHolderName
    AccountNumberMasked
    AccountIdentifierEncrypted?
    IBANEncrypted?
    SwiftBic?
    CountryCode
    BankConnectionRef?
    StatementImportProfileRef?
}
```

---

# 24. Sensitive Bank Data

Mask by default and never log full account identifiers.

---

# 25. Bank Credentials

TODIR must not store ordinary Internet Banking passwords.

Use:

```text
BankConnectionRef
Secret reference
OAuth/provider token vault
```

---

# 26. Cash Account Profile

Concept:

```text
CashAccountProfile
{
    TreasuryAccountId
    BranchId?
    PhysicalLocation?
    CustodianEmployeeRef?
    CustodianUserRef?
    CashCountPolicy
    DenominationCountEnabled
    MaxCashLimit?
}
```

---

# 27. Cash Account Examples

```text
Head Office Safe
Alexandria Branch Safe
Factory Petty Cash
Main Treasury Cash
```

---

# 28. POS Drawer Boundary

POS Session cash remains POS-owned.

After session close:

```text
POS Session Cash
→ settlement/handover
→ Treasury Cash Account
```

---

# 29. Digital Wallet Profile

Concept:

```text
DigitalWalletProfile
{
    TreasuryAccountId
    Provider
    WalletAccountRef
    WalletIdentifierMasked
    ExternalConnectionRef?
    StatementCapability
    PayoutCapability
}
```

---

# 30. Wallet Is Not Payment Method

Example:

```text
Payment Method = Wallet
Treasury Account = Company Wallet EGP
```

---

# 31. Settlement Account

Represents funds held by:

```text
payment gateway
merchant acquirer
marketplace
payment service provider
```

pending payout/settlement.

---

# 32. Provider Payout Is Not Customer Payment

Customer payment already happened.

Payout moves provider-held funds to bank.

---

# 33. Liquidity Class

Potential:

```text
Available
Restricted
InTransit
PendingSettlement
Unavailable
```

---

# 34. Lifecycle State

Recommended:

```text
Draft
PendingApproval
Active
Suspended
Closing
Closed
```

---

# 35. Closed Account Is Never Deleted

Historical statements/reconciliation/payments remain.

---

# 36. Maker-Checker

Sensitive account master changes can require:

```text
Maker
→ Proposed Change
→ Checker
→ Approved / Rejected
```

---

# 37. Account Activation Readiness

Depending type:

```text
Legal Entity
Currency
Purpose
Accounting readiness
Permissions
Connection/statement setup if used
```

---

# 38. Account Closing Review

Before close:

```text
Open Payments
Transfers In Transit
Unreconciled Statement Lines
Provider Settlements
Non-zero Balance
Pending Cash Count
```

---

# 39. Ledger Mapping Ownership

Ledger owns account determination/mapping.

Banking supplies Treasury identity and semantic context.

---

# 40. No GL IDs In Banking Commands

Forbidden:

```text
DebitAccountId
CreditAccountId
```

in ordinary treasury operations.



# 41. Accounting Readiness Contract

Banking can query:

```text
IsTreasuryAccountAccountingReady(accountId)
```

from Ledger/account-determination capability.

---

# 42. Semantic Roles

Potential accounting roles:

```text
CashAtBank
CashOnHand
DigitalWalletFunds
ProviderSettlementFunds
CashInTransit
CashOverShort
BankFees
BankInterestIncome
BankInterestExpense
```

Exact GL mapping is Ledger-owned.

---

# 43. Balance Types

TODIR must keep distinct:

```text
BookBalance
ExternalReportedBalance
AvailableBalance
StatementClosingBalance
LastReconciledBalance
CountedCashBalance
```

---

# 44. Book Balance

Derived from Ledger.

Never manually typed.

---

# 45. External Reported Balance

From:

```text
bank feed
statement
wallet/provider API
settlement statement
```

with timestamp/source.

---

# 46. Available Balance

May differ from reported balance due to:

```text
holds
pending card authorizations
overdraft
restricted amount
provider rules
```

---

# 47. Statement Closing Balance

Belongs to one statement.

---

# 48. Last Reconciled Balance

Result of finalized reconciliation.

---

# 49. Cash Counted Balance

Physical count snapshot for CashAccount.

---

# 50. Never One Ambiguous Balance Field

UI must label:

```text
Book Balance
Bank Reported Balance
Available Balance
Counted Cash
```

explicitly.

---

# 51. Balance Timestamp

Every external balance shows:

```text
AsOf
Source
Freshness
```

---

# 52. Stale Balance

Clearly marked.

Never present yesterday's feed as "live".

---

# 53. Bank Statement

Canonical external bank document.

---

# 54. Bank Statement Is Not Payment

Locked.

---

# 55. Bank Statement Is Not Journal Entry

Locked.

---

# 56. Bank Statement Import Sources

Adapter-driven:

```text
Bank API
Open Banking / AISP
Direct Bank Feed
ISO 20022 CAMT.053
CAMT.054
MT940
BAI2
OFX
CSV
XLSX
Manual statement
Other provider/country format
```

---

# 57. Import Adapter

Recommended:

```text
IBankStatementImporter
```

---

# 58. Normalizer

Recommended:

```text
IBankStatementNormalizer
```

converts source format to canonical statement model.

---

# 59. Raw Source Preservation

Store original imported file/message through Platform Files or immutable external-payload storage reference.

---

# 60. Import Provenance

Store:

```text
SourceProvider
SourceFormat
ParserVersion
ImportedAt
ImportedBy/System
RawFileRef?
ContentHash
ExternalStatementId?
```

---

# 61. Duplicate Statement Protection

Use stable combination:

```text
TreasuryAccount
ExternalStatementId?
StatementNumber?
Date range
Opening/Closing balance
Content hash
```

according to provider reliability.

---

# 62. Duplicate Import Must Be Idempotent

Same statement must not create new external truth twice.

---

# 63. Statement Concept

```text
BankStatement
{
    Id
    TreasuryAccountId
    Currency

    StatementNumber?
    ExternalStatementId?

    PeriodStart?
    PeriodEnd?

    OpeningBalance?
    ClosingBalance?

    StatementDate
    ImportedAt

    ProcessingState
    ReconciliationState

    RawFileRef?
    ContentHash
}
```

---

# 64. Statement Processing States

Recommended:

```text
Received
Parsed
ValidationFailed
Validated
ReadyForMatching
Processed
Superseded
Rejected
```

---

# 65. Reconciliation State Separate

```text
NotStarted
InProgress
PartiallyReconciled
FullyReconciled
Closed
```

---

# 66. Statement Validation

Check:

```text
Account identity
Currency
Date range
Opening balance continuity if provided
Closing balance math if provider supports
Duplicate lines
Required identifiers
Unsupported codes
```

---

# 67. Statement Opening Balance Continuity

Warn/block when:

```text
previous closing balance
!=
new opening balance
```

unless provider statement semantics explain difference.

---

# 68. Missing Statements

Track expected statement cadence where configured.

---

# 69. Statement Cadence

Examples:

```text
Daily
Weekly
Monthly
AdHoc
FeedOnly
```

---

# 70. Bank Statement Line

Canonical external movement line.

---

# 71. Bank Statement Line Concept

```text
BankStatementLine
{
    Id
    StatementId
    TreasuryAccountId

    ExternalLineId?
    Sequence?

    BookingDate
    ValueDate?
    TransactionDate?

    Direction
    Amount
    Currency

    RunningBalance?

    TransactionCode?
    BankTransactionType?

    CounterpartyName?
    CounterpartyAccountMasked?

    EndToEndId?
    PaymentReference?
    RemittanceInfo?
    BankReference?

    IsReversal
    ReversalOfExternalLineId?

    ReconciliationState
    RawPayloadHash?
}
```

---

# 72. Booking Date != Value Date

Keep separate.

---

# 73. Transaction Date != Booking Date

Keep separate if source provides.

---

# 74. Direction

Use explicit:

```text
Credit
Debit
```

or MoneyFlow semantics.

Do not infer solely from signed amount.

---

# 75. Transaction Code

Provider/bank code.

Map to canonical bank transaction type.

---

# 76. Canonical Bank Transaction Types

Initial:

```text
CustomerReceipt
SupplierPayment
InternalTransfer
CashDeposit
CashWithdrawal
BankFee
InterestIncome
InterestCharge
DirectDebit
Cheque
ProviderPayout
Refund
TaxDebit
LoanDebit
UnknownCredit
UnknownDebit
Other
```

---

# 77. Bank Transaction Type Is Classification

It does not create accounting by itself.

---

# 78. Transaction Code Mapping

Per bank/account/provider:

```text
ExternalCode
→ CanonicalBankTransactionType
```

---

# 79. Version Transaction Code Mapping

Historical interpretation must remain reproducible.

---

# 80. Unknown Code

Import line anyway as:

```text
Unknown
```

and route to review.

---

# 81. Counterparty Data

Sensitive.

Display/minimize according to permissions and banking privacy policy.

---

# 82. Reversal Line

Do not delete original statement line.

Link explicit reversal.

---

# 83. Corrected Statement

If bank republishes/corrects:

```text
Original statement remains
Correction/Superseding statement links
```

---

# 84. No Silent Raw Data Mutation

Absolute.

---

# 85. External Transaction Feed Without Formal Statement

Some APIs return transactions continuously.

Normalize into:

```text
ExternalTreasuryTransaction
```

and later group/reference official statement when available.

---

# 86. External Treasury Transaction

Can share line model but maintain source distinction:

```text
FeedTransaction
StatementLine
```

---

# 87. Official Statement Authority

Where bank supplies official statement, statement is period reconciliation evidence.

---

# 88. Matching Mission

> **Explain which TODIR-side money movements correspond to which external transactions.**

---

# 89. Match Candidate Sources

Potential system-side items:

```text
Payment
Internal Transfer movement
Provider Settlement/Payout
POS-to-cash settlement
Cash deposit/withdrawal
Bank-originated created movement
Cheque clearing
Approved Treasury adjustment
Eligible Ledger-side cash movement where contract allows
```

---

# 90. Do Not Match Directly To Invoice By Default

The money movement is:

```text
Bank Line ↔ Payment
```

Then:

```text
Payment ↔ Subledger Open Items
```

---

# 91. Bank-First Receipt Exception

If no Payment exists:

```text
Bank Line
→ propose/create Payment
→ allocate Payment to invoices
→ reconcile
```

through explicit orchestration.

---

# 92. Matching Cardinalities

Mandatory:

```text
1 : 1
1 : Many
Many : 1
Many : Many
```

---

# 93. One-to-One

Example:

```text
Bank Line -100,000
↔
Supplier Payment PAY-102
```

---

# 94. One-to-Many

Example:

```text
Bank Credit +100,000
↔
Customer Payment allocations/payment batch
60,000 + 40,000
```

The exact system-side object may still be one Payment with many allocations; matching model must not assume one invoice.

---

# 95. Many-to-One

Example:

```text
two bank debit lines
↔ one batch/system movement
```

when bank/provider splits execution.

---

# 96. Many-to-Many

Required for complex statement/batch settlement cases.

---

# 97. Partial Matching

Support:

```text
External amount 100,000
Matched 80,000
Remaining 20,000
```

only when business semantics allow.

---

# 98. Matching Does Not Mean Accounting Completion

A match is evidence relation.

If missing accounting/payment movement exists, create it explicitly first.

---

# 99. Reconciliation

Reconciliation confirms:

```text
external treasury truth
and
TODIR system-side money truth
```

for specified items/period.

---

# 100. Matched != Reconciled

Locked.

---

# 101. Why Separate Match/Reconcile

Allows:

```text
suggest
review
correct
approve
finalize
```

without immediately locking questionable matches.

---

# 102. Reconciliation Session

Concept:

```text
ReconciliationSession
{
    Id
    TreasuryAccountId
    Currency

    CutoffDate
    StatementIds[]

    State

    OpeningExternalBalance?
    ClosingExternalBalance?

    BookBalanceAtCutoff
    Difference

    StartedBy
    FinalizedBy?
    FinalizedAt?
}
```

---

# 103. Reconciliation Session State

```text
Draft
Matching
Review
ReadyToFinalize
Finalized
ReopenedForCorrection
Superseded
```

---

# 104. Overlapping Reconciliation

Do not allow two finalized reconciliation sessions to claim same statement line/system movement amount.

---

# 105. Reconciliation Match

Concept:

```text
ReconciliationMatch
{
    Id
    SessionId
    MatchType
    RuleVersionId?
    Confidence?
    CreatedBySystem

    State
    ReviewedBy?
}
```

---

# 106. Match Items

```text
ReconciliationMatchItem
{
    MatchId
    SideType
    SourceType
    SourceId
    Amount
    Currency
}
```

---

# 107. Match Amount Conservation

Within allowed tolerance/adjustment:

```text
External matched amount
=
System matched amount
+
explicit difference items
```

---

# 108. No Hidden Difference

Bank fee/FX/tolerance/write-off must be typed.

---

# 109. Matching Rules

Versioned deterministic rules.

---

# 110. Matching Rule Examples

```text
Exact EndToEndId + Amount
Exact Payment Reference + Amount
Exact Provider Payout ID
Exact Cheque Number + Amount
Amount + Date Window + Counterparty
Amount + Invoice/Payment Reference in remittance
Batch Reference + Total
```

---

# 111. Matching Rule Set

Ordered collection of rules.

---

# 112. Rule Priority

Specific/high-confidence first.

---

# 113. Auto-Match Policy

Only deterministic high-confidence rules may auto-match.

---

# 114. Ambiguous Match

If multiple plausible candidates:

```text
Needs Review
```

---

# 115. AI Matching

Optional suggestion layer.

Never authoritative financial truth.

---

# 116. AI May Suggest

```text
likely customer
likely invoice/payment
likely transaction type
similar description pattern
```

---

# 117. AI Must Not

```text
finalize reconciliation
create financial adjustment
apply customer payment
write off difference
```

without approved deterministic/human action.

---

# 118. Match Confidence

If used:

```text
RuleBasedExact
RuleBasedStrong
Suggested
Manual
```

Prefer explainable categories over opaque percentage only.

---

# 119. Match Explanation

Show:

```text
same amount
reference matched
date within 1 day
counterparty matched
```

---

# 120. Reconciliation Workbench

Exception-driven:

```text
Auto/Exact Matches
Suggestions
Needs Review
Unmatched External
Unmatched System
Differences
```

---

# 121. User Should Not Review Every Line

Healthy deterministic matches can be batch-approved/finalized under policy.

---

# 122. Unknown Incoming Receipt

Bank credit with no known Payment.

---

# 123. Unknown Receipt Workflow

```text
Statement Line
→ Search customer/reference/open invoices
→ Suggest/create Incoming Payment
→ Optional allocate to Subledger
→ Reconcile
```

---

# 124. Unknown Receipt Can Remain Unallocated

Money exists even if customer/invoice unknown.

Payments/Subledger supports unapplied amount.

---

# 125. Do Not Invent Customer

No fake customer just to reconcile.

---

# 126. Unknown Outgoing Debit

Could be:

```text
Bank fee
direct debit
loan deduction
tax
supplier debit
fraud/error
```

route to classification/review.

---

# 127. Bank Fee

External bank-originated charge.

---

# 128. Bank Fee Workflow

```text
Statement Line
→ classify Bank Fee
→ create approved bank-originated movement/financial event
→ Ledger semantic posting
→ reconcile
```

Use Payments/Ledger contracts; do not write Journal directly.

---

# 129. Interest Income

Typed bank-originated event.

---

# 130. Interest Charge

Typed bank-originated event.

---

# 131. Direct Debit

Can match:

```text
existing scheduled Payment
```

or create approved bank-originated outgoing movement.

---

# 132. Bank-Originated Event Candidate

Concept:

```text
BankOriginatedEventCandidate
{
    StatementLineId
    SuggestedType
    SuggestedParty?
    SuggestedAmount
    SuggestedAccountingRole?
    State
}
```

---

# 133. Candidate Is Not Posted Truth

Requires configured automation/approval.

---

# 134. Auto-Create Rule

Can be enabled for highly deterministic recurring lines, e.g.:

```text
monthly bank fee
```

with strict account-specific rule and audit.

---

# 135. Auto-Create Rule Versioning

Required.

---

# 136. Auto-Create Safety

Rule must constrain:

```text
TreasuryAccount
Transaction code/description
Direction
Amount range?
Counterparty?
Currency
```

---

# 137. No Arbitrary SQL Rule Engine

Use constrained predicates/actions.

---

# 138. Bank Fee Tolerance

Tolerance is not same as fee.

---

# 139. Match Tolerance

Company/account policy can allow minor:

```text
date
amount
```

differences.

---

# 140. Amount Difference Must Remain Explainable

If amount tolerance is accepted:

```text
difference reason
posting treatment
```

must be explicit.

---

# 141. FX Difference

Cross-currency system movement vs bank settlement can create explicit FX/fee difference.

FinBase + Ledger rules.

---

# 142. Cash Discount

Subledger settlement owns customer/supplier discount application.

Banking should not infer AR/AP discount itself.

---

# 143. Reconciliation Finalization

Validates:

```text
all selected matches balanced
difference explained
no duplicate claimed items
period/cutoff valid
required adjustments posted/confirmed
```

---

# 144. Finalized Reconciliation Is Immutable

Corrections use:

```text
Unreconcile / Reopen with permission
Correction session
Reversal linkage
```

not silent edit.

---

# 145. Unreconcile

High-impact audited action.

---

# 146. Reopen Conditions

Potential:

```text
not in locked accounting period
or
approved corrective process
```

with Ledger coordination.

---

# 147. Reconciliation Date

Separate from statement date and accounting date.

---

# 148. Last Reconciled Date

Account-level projection.

---

# 149. Reconciliation Difference

Show:

```text
External Closing Balance
Book Balance
Unmatched External
Unmatched System
Explicit Adjustments
Difference
```

---

# 150. Cash-to-GL Reconciliation

Banking must support drilldown from Treasury Account to:

```text
external balance
book balance
unreconciled statement lines
unreconciled book-side movements
```



# 151. Book-Side Movement Sources

Potential:

```text
Payments
Internal Transfers
Approved Treasury adjustments
Provider Payout
Cash settlement
Cheque clearing
Eligible Ledger cash postings
```

depending contract.

---

# 152. Payments Reconciliation Boundary

Payments says:

```text
PAY-1001 completed / pending / reversed
```

Banking says:

```text
Statement line XYZ matches PAY-1001
```

---

# 153. Payment Can Be Completed While Unreconciled

Normal.

---

# 154. Reconciliation Does Not Set Invoice Paid

Subledger handles payment allocation to invoice.

---

# 155. Bank Line Can Arrive Before Payment Record

Banking can orchestrate Payment creation.

---

# 156. Bank Line Can Arrive After Payment Record

Normal match.

---

# 157. Payment Reversal

Payments owns reversal.

Banking consumes resulting system movement/reconciliation correction.

---

# 158. Refund

Payments owns refund.

Banking matches external debit/credit evidence.

---

# 159. Provider Payout

Payments/provider integration may own payout operation.

Banking owns external settlement/bank reconciliation.

---

# 160. Provider Payout Reconciliation

Example:

```text
Gateway gross     100,000
Fees                2,500
Net payout         97,500
```

Banking must explain:

```text
Settlement Account decrease 100,000
Bank increase               97,500
Provider fee                 2,500
```

without duplicating customer payments.

---

# 161. Provider Settlement Batch

Concept:

```text
ProviderSettlementBatch
{
    ProviderRef
    SettlementAccountId
    Period
    Currency
    Gross
    Fees
    Adjustments
    NetPayout
    ExternalRef
}
```

May be owned by Payments provider integration and projected into Banking.

---

# 162. Settlement Account Reconciliation

Needs:

```text
provider payment events
refunds
chargebacks later
fees
payouts
external provider statement
```

---

# 163. Payment Gateway Balance Is Not Bank Balance

Label separately.

---

# 164. Wallet Statement

Digital wallet can provide transaction feed/statement similar to bank.

---

# 165. Wallet Reconciliation

Can reuse generic external-transaction matching engine with wallet-specific adapters.

---

# 166. Wallet Transfer To Bank

Operational transfer executed by Payments/provider.

Banking reconciles both sides.

---

# 167. Wallet Cash-In

Customer receipt into business wallet:

```text
Payment destination = DigitalWallet TreasuryAccount
```

---

# 168. Wallet Fee

Typed provider/bank-originated event.

---

# 169. Settlement Delay

Provider-held funds may be:

```text
PendingSettlement
```

until payout.

---

# 170. Cash Account Mission

Cash Account represents company cash held physically outside POS Session abstraction.

---

# 171. Cash Expected Balance

Derived from:

```text
opening authoritative balance
+
cash receipts
-
cash payments
+
transfers in
-
transfers out
+
approved cash differences
```

through canonical money/accounting movements.

---

# 172. Cash Expected Balance Is Not Counted Cash

Locked.

---

# 173. Cash Count

Physical verification of cash.

---

# 174. Cash Count Concept

```text
CashCount
{
    Id
    TreasuryAccountId
    CountedAt
    CountedBy
    Currency

    ExpectedBalance
    CountedBalance
    Difference

    State
    WitnessedBy?
    Reason?
}
```

---

# 175. Cash Count States

```text
Draft
Counted
NeedsReview
Approved
Rejected
Finalized
```

---

# 176. Denomination Counting

Optional:

```text
200 × 100 notes
100 × 50 notes
...
```

---

# 177. Denomination Line

Concept:

```text
CashCountDenomination
{
    CashCountId
    Denomination
    Quantity
    Total
}
```

---

# 178. Denomination Configuration

Country/currency-aware.

---

# 179. Counted Balance

Derived from denomination lines when enabled.

---

# 180. Cash Difference

```text
Counted - Expected
```

---

# 181. Cash Over

Positive difference.

---

# 182. Cash Short

Negative difference.

---

# 183. Cash Difference Is Not Balance Edit

Locked.

---

# 184. Cash Difference Resolution

Concept:

```text
CashDifferenceResolution
{
    CashCountId
    DifferenceType
    ReasonCode
    Explanation
    ApprovalState
    AccountingFactRef?
}
```

---

# 185. Cash Difference Reasons

Examples:

```text
CountingError
UnrecordedReceipt
UnrecordedPayment
CashierOverShort
Exchange/Rounding
Unknown
```

---

# 186. Unknown Cash Difference

Can remain review exception.

Do not auto-post large unknown shortages.

---

# 187. Cash Difference Approval

Threshold-based.

---

# 188. Petty Cash

Petty cash is:

```text
CashAccount
```

with additional policy such as:

```text
max balance
custodian
allowed uses
replenishment threshold
```

---

# 189. Petty Cash Expense

Expense truth remains Expenses.

Money movement:

```text
Cash Payment
```

via Payments.

---

# 190. Petty Cash Replenishment

Typical:

```text
Bank
→ Cash Account
```

internal transfer.

---

# 191. Petty Cash Float

Can have target:

```text
20,000 EGP
```

for replenishment.

---

# 192. Imprest Model

Optional policy:

```text
Replenish to fixed target
```

---

# 193. Non-Imprest Model

Allowed.

Do not force one petty-cash accounting model globally.

---

# 194. Cash Deposit To Bank

Operationally:

```text
CashAccount
→ BankAccount
```

same Legal Entity.

---

# 195. Cash Withdrawal From Bank

```text
BankAccount
→ CashAccount
```

---

# 196. Cash Deposit Can Be In Transit

Physical cash leaves safe before bank statement credit appears.

---

# 197. Cash Withdrawal Can Be In Transit

Bank withdrawal may clear before cash receipt count/hand-over.

---

# 198. Internal Transfer Execution Ownership

`12 Payments` owns actual internal-transfer movement lifecycle.

Banking can initiate/orchestrate from its UI.

---

# 199. Funds Transfer UI

User sees:

```text
From
To
Amount
Date
Reference
[Transfer]
```

Banking calls Payments.

---

# 200. Internal Transfer Is One Business Operation

Even if internally represented by:

```text
source movement
destination movement
cash-in-transit
```

---

# 201. Same-Currency Transfer

Concept:

```text
Amount out = Amount in
```

except explicit fees.

---

# 202. Cross-Currency Transfer

Stores:

```text
Source Money
Destination Money
FX Snapshot
Fee(s)
```

through Payments + FinBase.

---

# 203. Cross-Currency Transfer Does Not Infer FX

Explicit.

---

# 204. Transfer In Transit

First-class state/projection.

---

# 205. In-Transit Example

```text
CIB debited 500,000
NBE not yet credited
```

TODIR shows:

```text
500,000 In Transit
```

not expense/loss.

---

# 206. Transfer Reconciliation

Each external side matched separately.

---

# 207. Bank A → Bank B

Flow:

```text
Create Internal Transfer
→ source system movement
→ destination expected movement
→ Bank A statement match
→ Bank B statement match
→ Transfer fully reconciled
```

---

# 208. Cash → Bank

Flow:

```text
Cash handover/deposit
→ internal transfer
→ cash expected balance decreases
→ bank statement credit
→ reconcile destination
```

---

# 209. Bank → Cash

Flow:

```text
bank withdrawal
→ internal transfer
→ bank statement debit
→ cash receipt/count
```

---

# 210. Wallet → Bank

Provider transfer.

---

# 211. Gateway Settlement → Bank

Provider payout/settlement, not customer payment.

---

# 212. Cross-Entity Attempt

Block ordinary internal-transfer command.

---

# 213. Intercompany Seam

Future dedicated contract.

---

# 214. Cash Position Mission

> **Show the company's actual/near-actual liquidity now, by legal entity, account, currency and liquidity class.**

---

# 215. Cash Position Is Not General Ledger Trial Balance

It is treasury view.

---

# 216. Cash Position Sources

Current:

```text
Bank reported balances
Book balances
Cash expected/count balances
Wallet balances
Settlement balances
In-transit amounts
```

---

# 217. Cash Position Basis

User can choose:

```text
External Reported
Book
Reconciled
Available
```

where meaningful.

---

# 218. Default Owner View

Recommended:

```text
Available/Reported actual liquidity
```

with clear freshness/source.

---

# 219. Accountant View

Shows:

```text
External
Book
Difference
Reconciliation state
```

---

# 220. Treasury Cash Position Projection

Concept:

```text
CashPositionProjection
{
    AsOf
    LegalEntity
    TreasuryAccount
    Currency

    ExternalBalance?
    AvailableBalance?
    BookBalance
    LastReconciledBalance?
    InTransitAmount

    LiquidityClass
    Freshness
}
```

---

# 221. Group View

Authorized CFO can aggregate across entities.

---

# 222. Group View Does Not Merge Legal Truth

Each row retains Legal Entity.

---

# 223. Currency Aggregation

Do not sum unlike currencies without conversion.

---

# 224. Group Reporting Currency

FinBase conversion with:

```text
AsOf
Rate Purpose
Rate Snapshot
```

---

# 225. Actual Cash Position vs Converted View

Original currency remains visible.

---

# 226. Restricted Cash

May be shown but excluded from:

```text
Available Liquidity
```

---

# 227. Pending Settlement

May be included in:

```text
Near-term funds
```

not immediate available cash.

---

# 228. Overdraft

Bank account may have:

```text
OverdraftLimit
```

metadata.

---

# 229. Overdraft Limit Is Not Balance

Separate.

---

# 230. Available Liquidity With Overdraft

Can optionally display:

```text
cash available + unused overdraft
```

as separate metric, never pretend it's owned cash.

---

# 231. Credit Facility Detail

Full loan/debt management is future Treasury capability.

---

# 232. Cash Forecast Mission

> **Estimate future inflows/outflows and expected liquidity without changing actual accounting truth.**

---

# 233. Cash Position != Cash Forecast

Locked.

---

# 234. Forecast Horizons

Examples:

```text
Today
7 Days
14 Days
30 Days
60 Days
90 Days
```

---

# 235. Forecast Sources

Potential:

```text
Subledger receivables due
Subledger payables due
Scheduled Payments
Payroll planned payment dates
Tax obligations
Recurring service/subscription flows later
Purchase commitments where policy applies
Project expected cash flows later
Manual treasury forecast items
```

---

# 236. Forecast Source Ownership

Banking reads projected cash events.

It does not duplicate AR/AP/Payroll.

---

# 237. Forecast Inflow

Example:

```text
Customer invoice due 500,000
Expected probability/time adjustment optional
```

---

# 238. Forecast Outflow

Example:

```text
Supplier payable due 300,000
Payroll 1,200,000
Tax 200,000
```

---

# 239. Manual Forecast Item

Allowed for planning-only known event not represented elsewhere.

---

# 240. Manual Forecast Item Must Be Clearly Planning Data

Never post accounting.

---

# 241. Manual Forecast Item Concept

```text
CashForecastItem
{
    LegalEntityId
    Direction
    ExpectedDate
    Amount
    Currency
    Category
    Probability?
    Source = Manual
    Owner
    Notes
}
```

---

# 242. Forecast Version

Optional snapshot for comparison.

---

# 243. Cash Forecast Version

```text
CashForecastVersion
{
    AsOf
    Horizon
    Scenario
    CreatedAt
}
```

---

# 244. Forecast Scenario

Potential:

```text
Base
Conservative
Optimistic
```

later/optional.

---

# 245. Do Not Build Full FP&A Here

Company-wide budgeting/planning belongs `28 Budgeting & Financial Planning`.

---

# 246. Treasury Forecast Is Short-Term Liquidity

Not annual P&L budget.

---

# 247. Forecast Accuracy

Can compare:

```text
forecasted inflow/outflow
vs
actual external/payment movement
```

later.

---

# 248. Overdue AR

Can appear as:

```text
expected but uncertain inflow
```

according to policy.

---

# 249. AP Payment Proposal

Future Payment Runs can feed scheduled outflows.

---

# 250. Payroll

Payroll planned pay date can feed outflow forecast.

No employee salary detail needed in Banking.

---

# 251. Tax

Tax module/country capability can feed expected obligations.

---

# 252. Project Cash Forecast

Projects may later supply:

```text
expected milestone collection
expected project payments
```

but Banking does not infer from project revenue.

---

# 253. Forecast Confidence

Optional per source.

---

# 254. Forecast Drilldown

Every number explains source.

---

# 255. Liquidity Gap

Show future date where:

```text
forecast available cash < threshold
```

---

# 256. Target Balance

Account/legal-entity policy can define:

```text
minimum desired balance
```

---

# 257. At-Risk Cash Position

Flag:

```text
forecast/available below target
```

---

# 258. Cash Concentration

Future enterprise capability:

```text
sweep
zero-balance account
cash pool
```

design seam.

---

# 259. Cash Pool

Do not implement Day 1 without need.

---

# 260. Sweep

Future transfer automation with strong approvals/reconciliation.

---

# 261. Treasury Dashboard

Owner/CFO view should answer:

```text
How much cash is available?
Where is it?
Which accounts are stale?
What is unreconciled?
What is in transit?
What is due in/out next 7/30 days?
Where is liquidity risk?
```

---

# 262. Banking Home — Example

```text
Available Cash          8.2M
Book Cash               8.35M
Unreconciled Difference 0.15M

Accounts Updated        6 / 7
Unmatched Lines         19
Transfers In Transit    0.5M

7-Day Inflows           3.5M
7-Day Outflows          5.0M
7-Day Forecast Cash     6.7M
```

---

# 263. Account Card

Show:

```text
Name
Type
Currency
Legal Entity
External Balance
Book Balance
Difference
Freshness
Last Reconciled
Unmatched count
```

---

# 264. Cash Account Card

Show:

```text
Expected Cash
Last Counted Cash
Last Count Date
Difference
Custodian
```

---

# 265. Wallet Card

Show:

```text
Provider balance
Book balance
Pending payout
Last sync
```

---

# 266. Settlement Account Card

Show:

```text
Gross pending
Fees pending
Net expected payout
Aging
Last payout
```

---

# 267. Reconciliation Inbox

Exception-driven.

---

# 268. Reconciliation Inbox Groups

```text
Exact Matches
Suggested Matches
Needs Review
Unknown Credits
Unknown Debits
Differences
Missing System Movements
```

---

# 269. Missing Statement Alert

If expected daily feed missing:

```text
CIB EGP — no statement/feed since yesterday
```

---

# 270. Bank Connection Health

Separate operational status:

```text
Healthy
AuthenticationRequired
ProviderDown
RateLimited
Stale
Disconnected
```

---

# 271. Connection Health Does Not Change Account Lifecycle

Bank account can remain Active while feed is down.

---

# 272. Connection Credentials

Use Plugin/connector/provider secret vault.

---

# 273. Reconnect/MFA

Operational action with audit.

---

# 274. Statement Import Manually

Allowed via file upload.

---

# 275. Manual Statement

Finance can enter opening/closing and lines if no electronic format.

Strong validation.

---

# 276. Manual Statement Is Not Manual Balance Edit

Every line/evidence preserved.

---

# 277. Cash Account "Statement"

Cash does not need bank statement.

Use:

```text
Cash Movements
Cash Count
Cash Reconciliation
```

---

# 278. Cash Reconciliation

Compare:

```text
Expected Cash
vs
Counted Cash
```

then resolve explicit difference.

---

# 279. Wallet/Settlement Reconciliation

Compare:

```text
Provider external transaction feed
vs
TODIR Payments/provider settlements
```

---

# 280. Reconciliation Engine Reuse

Shared matching core can support:

```text
Bank
Wallet
Settlement provider
```

with source-specific adapters.

---

# 281. External Account Connection

One TreasuryAccount can have:

```text
zero
one
multiple historical connection profiles
```

---

# 282. Connection Versioning

When provider connection changes, historical imported data keeps source profile/version.

---

# 283. Account Number Change

Usually new real bank account identity or controlled master change.

Do not casually overwrite if bank issued new account.

---

# 284. IBAN Change Review

High risk.

---

# 285. Currency Change

Normally cannot mutate active account currency if historical transactions exist.

Create new balance pocket/account as appropriate.

---

# 286. Legal Entity Change

Normally forbidden after activation/history.

Create new account/ownership process.

---

# 287. Branch Assignment

Operational dimension can change with history if needed.

---

# 288. Custodian Change For Cash Safe

Effective-dated/handover.

---

# 289. Cash Custodian Handover

Flow:

```text
Outgoing Custodian Count
→ Manager/Witness confirm
→ Incoming Custodian accepts
→ Custodian changes
```

optional policy.

---

# 290. Cash Safe Physical Location Change

History.

---

# 291. Max Cash Limit

Alert/control.

---

# 292. Exceeding Max Cash

Can warn:

```text
deposit excess to bank
```

not silently block legitimate receipt unless policy.

---

# 293. Minimum Cash Float

Optional.

---

# 294. Safe Replenishment

Can suggest internal transfer.

---

# 295. Cash Forecast For Safe

Not same as corporate cash forecast; optional.

---

# 296. Cheque Boundary

Payments owns Cheque as Payment Method/instrument lifecycle.

Banking consumes clearing/reconciliation state.

---

# 297. Issued Cheque

Can appear:

```text
Payment completed/issued
but bank not yet cleared
```

---

# 298. Received Cheque

Can affect cash forecast/collection state according to Payments/Subledger policy.

Do not treat as cleared bank cash until appropriate.

---

# 299. Post-Dated Cheque

Important regional seam.

Payments owns instrument schedule; Banking includes expected clearing in forecast.

---

# 300. Returned Cheque

Bank statement/Payment correction updates status and Subledger consequence through owning domains.



# 301. Cheque Clearing

Bank Statement can match cheque reference/number/amount.

---

# 302. Cheque In Transit

Show in:

```text
Uncleared / In Transit
```

without treating as unexplained difference.

---

# 303. Bank Fees On Transfer

Explicit:

```text
Transfer Amount
Bank Fee
Net Destination
```

---

# 304. Bank Fee Paid Separately

Can be separate statement line.

Reconciliation supports.

---

# 305. Cash In Transit

First-class treasury concept/projection.

---

# 306. Cash In Transit Sources

Examples:

```text
Bank-to-bank transfer
Cash deposit pending bank credit
Provider payout pending
Cheque deposited not cleared
```

---

# 307. In-Transit Is Not Expense

Locked.

---

# 308. In-Transit Aging

Report:

```text
0-1 days
2-3 days
4-7 days
8+ days
```

to detect stuck transfers.

---

# 309. Settlement Aging

Provider-held funds aging:

```text
Expected payout date
Days pending
```

---

# 310. Unexpected Long Settlement

Exception.

---

# 311. Treasury Account Data Model

Recommended schema:

```text
finance_banking
```

or

```text
finance_treasury
```

Decision:

> Use `finance_banking` for consistency with product name, while code/domain can use Treasury terminology.

---

# 312. BankingDbContext

One module-owned context:

```text
BankingDbContext
```

---

# 313. Core Tables

Potential:

```text
treasury_account
treasury_account_balance_pocket
bank_account_profile
cash_account_profile
digital_wallet_profile
settlement_account_profile

treasury_account_change_request
treasury_account_usage_policy
treasury_account_connection

external_statement
external_statement_line
external_feed_transaction
statement_import_batch
statement_transaction_code_mapping

reconciliation_rule_set
reconciliation_rule_version
reconciliation_session
reconciliation_match
reconciliation_match_item
reconciliation_adjustment

cash_count
cash_count_denomination
cash_difference_resolution
cash_custodian_history

bank_originated_event_candidate

cash_position_projection
cash_forecast_version
cash_forecast_item
cash_forecast_projection

treasury_account_balance_projection
treasury_activity_projection
transfer_reconciliation_projection
settlement_account_projection
```

---

# 314. Tables Banking Must Not Own

Forbidden duplicate tables:

```text
payment
payment_attempt
customer_open_item
supplier_open_item
journal_entry
pos_session
pos_sale
employee_payslip
invoice
```

---

# 315. Treasury Activity Projection

Can combine:

```text
Payments
External statement lines
Transfers
Bank-originated events
Provider settlements
```

for one timeline.

Projection only.

---

# 316. Treasury Activity Is Not New Transaction Source Of Truth

Locked.

---

# 317. Account Balance Projection

Can persist rebuildable:

```text
BookBalance
ExternalBalance
AvailableBalance
UnreconciledDifference
LastReconciledAt
```

---

# 318. Projection Source Watermarks

Store source versions/timestamps for rebuild/health.

---

# 319. RLS

Every tenant-owned table:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 320. Runtime DB Role

No ownership/BYPASSRLS.

---

# 321. Legal Entity Authorization

Application authorization always validates entity scope.

---

# 322. UUIDv7

Use for IDs.

---

# 323. Money

FinBase `Money`.

No float/double.

---

# 324. Statement Amount Precision

Use exact source currency decimal scale.

---

# 325. External Raw Amount

Preserve original decimal string/value if parser format needs audit.

---

# 326. FX

FinBase snapshots.

---

# 327. Dates

Use NodaTime semantics:

```text
LocalDate for statement/value dates
Instant for import/audit timestamps
DateTimeZone for feed/calendar operations
```

---

# 328. Row Version

Mutable account master/rules/draft reconciliation.

---

# 329. Finalized Statements

External imported truth not ordinarily edited.

Corrections/supersession only.

---

# 330. Finalized Reconciliation

Immutable/corrective.

---

# 331. Versioned Matching Rules

Mandatory.

---

# 332. Versioned Transaction Code Mapping

Mandatory where changes matter.

---

# 333. Connection Profile Version

Track provider/format changes.

---

# 334. Unique Treasury Account Code

Within:

```text
Tenant + Legal Entity
```

---

# 335. Bank Identifier Uniqueness

Do not blindly enforce global IBAN uniqueness across tenants.

Within authorized tenant/entity, detect duplicate real account candidate.

---

# 336. Cash Account Code Uniqueness

Entity scope.

---

# 337. Wallet Identifier Uniqueness

Provider + legal entity + wallet reference policy.

---

# 338. Statement Line Unique External ID

When reliable:

```text
TreasuryAccount + Provider + ExternalLineId
```

unique.

---

# 339. Content Hash Fallback

For providers without stable IDs.

---

# 340. API Base

Recommended:

```text
/api/v1/banking
```

---

# 341. Treasury Account APIs

```text
GET  /treasury-accounts
POST /treasury-accounts
GET  /treasury-accounts/{id}
PUT  /treasury-accounts/{id}
POST /treasury-accounts/{id}/activate
POST /treasury-accounts/{id}/suspend
POST /treasury-accounts/{id}/close-request
POST /treasury-accounts/{id}/reopen
```

---

# 342. Sensitive Change APIs

```text
POST /treasury-accounts/{id}/change-requests
POST /treasury-account-change-requests/{id}/approve
POST /treasury-account-change-requests/{id}/reject
```

---

# 343. Statement APIs

```text
GET  /treasury-accounts/{id}/statements
POST /treasury-accounts/{id}/statements/import
GET  /statements/{id}
POST /statements/{id}/validate
POST /statements/{id}/process
```

---

# 344. Feed APIs

Infrastructure adapters can use internal endpoints/jobs.

Public API only when safe.

---

# 345. Statement Line APIs

```text
GET /statements/{id}/lines
GET /statement-lines/{id}
```

Read-only external truth except classification metadata.

---

# 346. Reconciliation APIs

```text
GET  /reconciliations
POST /treasury-accounts/{id}/reconciliations
GET  /reconciliations/{id}
POST /reconciliations/{id}/run-rules
POST /reconciliations/{id}/match
POST /reconciliations/{id}/unmatch
POST /reconciliations/{id}/finalize
POST /reconciliations/{id}/reopen
```

---

# 347. Match Suggestion API

```text
GET /reconciliations/{id}/suggestions
```

---

# 348. Unknown Receipt API

```text
POST /statement-lines/{id}/create-payment-proposal
```

or orchestration command.

---

# 349. Bank-Originated Event API

```text
POST /statement-lines/{id}/classify
POST /bank-originated-candidates/{id}/approve
```

---

# 350. Cash Count APIs

```text
GET  /cash-accounts/{id}/counts
POST /cash-accounts/{id}/counts
POST /cash-counts/{id}/submit
POST /cash-counts/{id}/approve
POST /cash-counts/{id}/finalize
```

---

# 351. Internal Transfer UI API

Can proxy/orchestrate:

```text
POST /transfers
```

but implementation delegates movement lifecycle to Payments.

---

# 352. Cash Position APIs

```text
GET /cash-position
GET /cash-position/by-account
GET /cash-position/by-entity
GET /cash-position/by-currency
```

---

# 353. Cash Forecast APIs

```text
GET  /cash-forecast
POST /cash-forecast/manual-items
POST /cash-forecast/versions
GET  /cash-forecast/versions/{id}
```

---

# 354. Connection APIs

```text
GET  /treasury-accounts/{id}/connections
POST /treasury-accounts/{id}/connections
POST /connections/{id}/refresh
POST /connections/{id}/disconnect
```

secrets handled by connector infrastructure.

---

# 355. Commands — Account Master

Representative:

```text
CreateTreasuryAccount
UpdateTreasuryAccountDraft
RequestTreasuryAccountActivation
ActivateTreasuryAccount
SuspendTreasuryAccount
RequestTreasuryAccountChange
ApproveTreasuryAccountChange
StartTreasuryAccountClosing
CloseTreasuryAccount
ReopenTreasuryAccount
```

---

# 356. Commands — Statements

```text
ImportExternalStatement
ValidateExternalStatement
AcceptExternalStatement
SupersedeExternalStatement
RecordExternalFeedTransaction
```

---

# 357. Commands — Matching

```text
CreateReconciliationSession
RunMatchingRuleSet
CreateManualMatch
AcceptSuggestedMatch
RejectSuggestedMatch
UnmatchDraftMatch
FinalizeReconciliation
ReopenReconciliation
```

---

# 358. Commands — Cash

```text
CreateCashCount
RecordDenominationCount
SubmitCashCount
ResolveCashDifference
FinalizeCashCount
TransferCashCustody
```

---

# 359. Commands — Forecast

```text
CreateManualCashForecastItem
UpdateManualCashForecastItem
CreateCashForecastSnapshot
```

---

# 360. No Generic `SetBalance`

Forbidden.

---

# 361. No Generic `SetBookBalance`

Forbidden.

---

# 362. No Generic `SetBankBalance`

Forbidden.

---

# 363. No Generic `SetReconciled=true`

Use reconciliation commands.

---

# 364. No Generic `CreateJournal`

Banking emits semantic facts/contracts only.

---

# 365. No Generic `MarkInvoicePaid`

Forbidden.

---

# 366. Contracts

Recommended:

```text
Todir.Banking.Contracts
```

---

# 367. Backend Namespaces

```text
Todir.Modules.Banking.Domain
Todir.Modules.Banking.Application
Todir.Modules.Banking.Infrastructure
Todir.Banking.Contracts
```

---

# 368. Internal Areas

Potential:

```text
Accounts/
BankAccounts/
CashAccounts/
Wallets/
Settlement/
Statements/
Matching/
Reconciliation/
CashCounts/
Transfers/
CashPosition/
Forecast/
Connections/
Reporting/
```

---

# 369. Payments Contract Consumed

Need:

```text
GetPaymentMovement
SearchPaymentMovementsForMatch
CreateIncomingPaymentFromBankEvidence
CreateOutgoingMovementFromBankEvidence
CreateInternalTransfer
GetTransferState
GetProviderSettlement
```

exact shape implementation-approved.

---

# 370. Ledger Contract Consumed

Need:

```text
GetTreasuryBookBalance
CheckTreasuryAccountingReadiness
PreviewSemanticAdjustment
PostBankingAccountingFact
GetCashPostingRefs
```

---

# 371. Subledger Contract Consumed

For unknown receipt application:

```text
SearchOpenItems
ApplyPayment
```

through Payments/Subledger orchestration.

---

# 372. POS Contract Consumed

Need settlement handoff:

```text
GetClosedSessionCashSettlement
ConfirmTreasuryCashReceipt
```

without Banking owning session.

---

# 373. Payroll Contract Consumed

Forecast only:

```text
GetPlannedPayrollCashOutflow
```

privacy-safe aggregate.

---

# 374. Tax/Country Contract Consumed

Forecast/regulatory bank format/compliance where needed.

---

# 375. Event Contracts

Potential Banking events:

```text
TreasuryAccountActivatedV1
TreasuryAccountSuspendedV1
TreasuryAccountClosedV1

ExternalStatementImportedV1
ExternalStatementValidatedV1

ReconciliationMatchCreatedV1
ReconciliationFinalizedV1
ReconciliationReopenedV1

CashCountFinalizedV1
CashDifferenceResolvedV1

CashPositionChangedV1?  // use carefully; projections may not need event
```

---

# 376. Events Consumed

Potential:

```text
PaymentCompletedV1
PaymentReversedV1
PaymentRefundedV1
InternalTransferCompletedV1
ProviderSettlementImportedV1

PosSessionClosedV1
PosCashSettlementPreparedV1

PayrollFinalizedV1 / payment instruction projection

OpenItemDueChanged
```

only if contracts actually provide.

---

# 377. Sync Invariants

Immediate matching/finalization uses strongly consistent Banking data.

Cross-module state queried synchronously where finalization invariant requires.

---

# 378. Async Projections

Cash position/forecast/read models can use events where freshness semantics explicit.

---

# 379. Outbox/Inbox

Use Platform Kernel.

---

# 380. Idempotency — Statement Import

Mandatory.

---

# 381. Idempotency — Feed Transaction

Mandatory.

---

# 382. Idempotency — Reconciliation Match

Same items/amount cannot be claimed twice.

---

# 383. Idempotency — Bank-Originated Movement Creation

Mandatory.

---

# 384. Idempotency — Internal Transfer Initiation

Payments key.

---

# 385. Idempotency — Cash Count Finalization

Mandatory.

---

# 386. Concurrency — Reconciliation

Two users cannot reconcile same amount/item simultaneously.

---

# 387. Concurrency — Account Master Change

Optimistic concurrency + approval version.

---

# 388. Concurrency — Cash Count

Only one active authoritative count for same CashAccount/cutoff where policy requires.

---

# 389. Concurrency — Statement Import

Duplicate feed jobs safe.

---

# 390. Concurrency — Connection Refresh

Deduplicate external pull window.

---

# 391. Permissions

Initial vocabulary:

```text
banking.accounts.read
banking.accounts.create
banking.accounts.edit
banking.accounts.activate
banking.accounts.suspend
banking.accounts.close
banking.accounts.sensitive_data.read
banking.accounts.change.approve

banking.connections.read
banking.connections.manage

banking.statements.read
banking.statements.import
banking.statements.validate

banking.reconciliation.read
banking.reconciliation.match
banking.reconciliation.finalize
banking.reconciliation.reopen

banking.bank_originated.review
banking.bank_originated.approve

banking.cash.read
banking.cash.count
banking.cash.count.approve
banking.cash.difference.resolve

banking.transfers.read
banking.transfers.initiate

banking.cash_position.read
banking.cash_forecast.read
banking.cash_forecast.manage

banking.reports.read
banking.audit.read
banking.export
banking.settings.manage
```

---

# 392. Scope Model

Combine permission with:

```text
Branch
LegalEntity
SpecificTreasuryAccount
AccountGroup
AllAuthorizedEntities
```

---

# 393. Account-Level Authorization

Some treasury accounts may be restricted even within same Legal Entity.

---

# 394. Payroll Account

Can be restricted to Treasury/CFO.

---

# 395. Tax Account

May have specific authorized roles.

---

# 396. Cash Custodian

Can:

```text
see own CashAccount
record count
request transfer
```

not manage bank account IBAN.

---

# 397. Reconciler

Can match/reconcile authorized accounts.

---

# 398. Treasury Admin

Can manage account setup but not necessarily approve sensitive changes.

---

# 399. Approver

Can approve account changes/transfers according to platform approval policy.

---

# 400. CFO / Cash Manager

Cross-account position/forecast views.

---

# 401. Segregation Of Duties

Optional controls:

```text
Account Maker != Account Checker
Payment Maker != Payment Approver
Reconciliation Adjuster != Reconciliation Finalizer
Cash Custodian != Cash Count Approver
```

---

# 402. Do Not Hardcode SoD For Tiny Companies

Configurable.

---

# 403. Sensitive Data Classes

Protect:

```text
full IBAN
full account number
bank connection identifiers
cash safe location details
provider merchant references
counterparty bank account
```

---

# 404. Bank Statement Data Privacy

Imported bank files can contain personal data.

Access tightly permissioned.

---

# 405. Raw Statement File Access

Separate permission if needed.

---

# 406. Audit Critical Actions

Capture:

```text
Account create/change/close
IBAN/account-number access/change
Connection change
Statement import/reject/supersede
Manual line classification
Match/unmatch
Reconciliation finalize/reopen
Bank-originated adjustment approve
Cash count
Cash difference resolution
Custodian handover
Internal transfer initiation
Forecast manual item
Large export
```

---

# 407. Audit Match Source

Record:

```text
System rule
User
AI suggestion accepted
Auto-create rule
```

---

# 408. Matching Rule Audit

Who changed rule and version.

---

# 409. Security — Connector

Never expose bank tokens to browser/client.

---

# 410. Security — Webhook/Feed

Verify provider authenticity where supported.

---

# 411. Security — Imported Files

Virus/content scanning through Platform Files policy where appropriate.

---

# 412. Security — CSV Formula Injection

Sanitize exports/import previews.

---

# 413. Security — Statement Parser

Treat external files as untrusted.

---

# 414. Security — Account Enumeration

APIs must not reveal hidden account existence.

---

# 415. Security — IDOR

Entity/account scope server-side.

---

# 416. Security — Amount Tampering

Finalization recomputes totals server-side.

---

# 417. Performance Mission

Banking workbench must stay fast under:

```text
many accounts
millions of statement lines
high payment volume
large provider settlement feeds
```

---

# 418. Treasury Account List Target

```text
p95 <= 100-250ms
```

---

# 419. Account Workspace Target

Core summary:

```text
p95 <= 150-350ms
```

without loading all transactions.

---

# 420. Reconciliation Inbox Target

Paged:

```text
p95 <= 150-400ms
```

for normal indexed filters.

---

# 421. Match Search Target

Candidate lookup:

```text
p95 <= 150-400ms
```

for bounded amount/date/account window.

---

# 422. Cash Position Target

```text
p95 <= 200-500ms
```

from projections.

---

# 423. Forecast Target

Interactive horizon:

```text
p95 <= 300-800ms
```

depending source composition.

---

# 424. Large Statement Import

Batch/chunk parse and validate.

---

# 425. No One Transaction Per API Call

Bulk statement lines inserted efficiently.

---

# 426. Matching Batch

Set-based candidate search where possible.

---

# 427. No N+1

Account list cannot query separately:

```text
BookBalance
ExternalBalance
Unmatched
LastReconciled
```

per account.

---

# 428. Candidate Indexes

Likely:

```text
statement_line(account, reconciliation_state, booking_date)
statement_line(account, amount, booking_date)
statement_line(end_to_end_id)
statement_line(payment_reference)
statement_line(bank_reference)
```

---

# 429. Payment-Side Matching Projection

Create/search optimized Banking-side reference projection if direct contract query is too expensive.

Projection only.

---

# 430. Reconciliation Match Indexes

```text
match(session, state)
match_item(source_type, source_id)
```

with claimed-amount protection.

---

# 431. Treasury Account Indexes

```text
tenant/entity/type/state
tenant/entity/code unique
connection/status
```

---

# 432. Cash Count Indexes

```text
cash_account/counted_at desc
state
```

---

# 433. Forecast Indexes

By:

```text
entity/date/direction/source
```

---

# 434. Statement Partitioning

Do not partition Day 1.

Measure growth.

---

# 435. Archive/Retention

Statement data may require long legal retention.

Country Pack determines.

---

# 436. Read Replica

Historical reporting can use replica later.

Final reconciliation cannot depend on stale replica.

---

# 437. Redis

Not required Day 1.

---

# 438. OpenSearch

Not required Day 1.

PostgreSQL indexes/text search first.

---

# 439. Background Jobs

Quartz/Worker:

```text
Bank feed pull
Statement parse
Matching run
SLA-like feed freshness checks
Cash forecast refresh
Projection rebuild
Large export
Connection health
```

---

# 440. Feed Pull Frequency

Provider-specific and rate-limit aware.

---

# 441. Do Not Poll Every Account Every Minute Blindly

Use provider schedule/webhook capability.

---

# 442. Retry

Bounded exponential retry.

---

# 443. Provider Outage

Show stale state.

Do not fabricate zero balance.

---

# 444. Partial Feed Failure

One account failure does not block others.

---

# 445. Observability Metrics

Potential:

```text
banking_statement_import_duration
banking_statement_import_failure_total
banking_duplicate_statement_total
banking_match_rate
banking_unmatched_line_count
banking_reconciliation_duration
banking_cash_difference_total
banking_connection_stale_count
banking_transfer_in_transit_count
banking_cash_position_refresh_duration
banking_forecast_refresh_duration
```

No account IDs as high-cardinality metric labels.

---

# 446. Matching Quality Metrics

```text
exact auto-match %
suggested accepted %
manual %
unmatched %
reopened %
```

---

# 447. Trace — Statement Import

Spans:

```text
Receive
Hash
Parse
Normalize
Validate
Persist
Project
Audit
```

---

# 448. Trace — Matching

```text
Load rule set
Candidate search
Rule evaluation
Conflict check
Match create
Projection update
```

---

# 449. Trace — Reconciliation Finalize

```text
Validate statement
Validate matches
Validate claimed amounts
Validate adjustments
Read Book Balance
Compute difference
Finalize
Audit
Outbox
```

---

# 450. Trace — Cash Count

```text
Load expected cash
Record denominations
Compute difference
Approval
Adjustment if needed
Finalize
```

---

# 451. Logging

Do not log:

```text
full account numbers
full IBAN
bank credentials
raw statement content
counterparty sensitive data
```

unless secure diagnostic policy explicitly allows masked samples.

---

# 452. Reports — Treasury Accounts

```text
Treasury Account Register
Accounts By Legal Entity
Accounts By Type
Accounts By Currency
Accounts By Purpose
Active/Suspended/Closing
Connection Health
```

---

# 453. Reports — Balances

```text
External Balance
Available Balance
Book Balance
Reconciled Balance
Unreconciled Difference
```

---

# 454. Reports — Reconciliation

```text
Unmatched Statement Lines
Unmatched System Movements
Reconciliation Summary
Reconciliation Aging
Match Rate
Reopened Reconciliations
```

---

# 455. Reports — Cash

```text
Cash Count History
Cash Over/Short
Cash By Branch
Petty Cash Balance
Custodian History
Cash Transfers
```

---

# 456. Reports — Transfers

```text
Internal Transfers
Transfers In Transit
Aged In Transit
Cross-Currency Transfers
Transfer Fees
```

---

# 457. Reports — Provider Settlement

```text
Gross Collected
Fees
Adjustments
Net Payout
Pending Settlement
Settlement Aging
Bank Payout Match
```

---

# 458. Reports — Bank Fees/Interest

```text
Fees By Bank/Account/Period
Interest Income
Interest Charges
```

---

# 459. Reports — Cash Position

By:

```text
Legal Entity
Account
Type
Currency
Liquidity Class
```

---

# 460. Reports — Cash Forecast

```text
7-day
30-day
90-day
Inflows
Outflows
Liquidity Gap
Source Category
```

---

# 461. Reports — Cash-to-GL

Required:

```text
Treasury external
Book
Unmatched external
Unmatched book
Difference
```

---

# 462. Reports — Missing Statements

Expected vs received.

---

# 463. Reports — Stale Connections

Operational.

---

# 464. Reports — Sensitive Master Changes

Audit/approval.

---

# 465. Export

CSV/XLSX/PDF as appropriate.

---

# 466. Large Export

Background secure file.

---

# 467. Migration Mission

Move existing bank/cash/wallet accounts and reconciliation state without fake payments.

---

# 468. Import — Treasury Account Master

Fields:

```text
Legal Entity
Type
Code
Name
Currency
Purpose
Bank/Wallet/Provider identifiers
Status
Opened date
Branch
Legacy ref
```

---

# 469. Import — Bank Accounts

Protect sensitive values.

---

# 470. Import — Cash Accounts

Include:

```text
opening cash
custodian
branch
```

with opening reconciliation.

---

# 471. Import — Wallet/Settlement

Provider IDs + opening external/book balances.

---

# 472. Opening Treasury Balance

Do not directly set arbitrary current balance.

Use approved opening/migration process tied to Ledger opening state.

---

# 473. Opening External Balance

Can be imported:

```text
AsOfDate
Reported Balance
Source
```

---

# 474. Opening Reconciliation

Need to establish:

```text
Book opening balance
External opening balance
Outstanding uncleared items
```

---

# 475. Outstanding Cheques/Transfers

Import as opening in-transit items with source/provenance.

---

# 476. Historical Statements

Optional:

```text
Full
RecentOnly
SummaryOnly
None
```

explicit strategy.

---

# 477. Reconciliation History Import

Only if reliable.

Otherwise begin at go-live opening reconciliation.

---

# 478. Migration Provenance

Store:

```text
SourceSystem
LegacyAccountId
BatchId
ImportedAt
OpeningAsOfDate
```

---

# 479. No Fake Payment

Absolute.

---

# 480. No Fake Statement Line

Do not synthesize detail if only opening balance exists.

---

# 481. Go-Live Boundary

After date:

```text
normal statement import/payment/reconciliation
```

---

# 482. Readiness Wizard — Small Company

Recommended:

```text
1. Create Legal Entity treasury accounts
2. Add Bank/Cash/Wallet
3. Choose currency/purpose
4. Check Ledger mapping readiness
5. Configure statement import/manual mode
6. Import opening balances/statements
7. Reconcile opening
8. Go Live
```

---

# 483. Small Company Defaults

```text
One/few Bank Accounts
One Cash Safe optional
Manual/CSV statement import
Simple exact matching
Manual review
No cash pooling
Simple 7/30-day forecast
No dual-control unless wanted
```

---

# 484. Medium/Large Defaults

Can enable:

```text
Bank feeds
Matching rule sets
Maker-checker
Multiple entities
Wallet/settlement accounts
Cash position
Cash forecast
Provider settlement reconciliation
Cash counts
Cheque/in-transit reporting
```

---

# 485. Enterprise Capabilities Later

```text
Cash pooling
Automated sweeps
Central treasury
Intercompany liquidity
Debt management
Investments
Advanced bank fee analysis
Bank relationship analytics
Payment factory
Treasury risk management
```

---

# 486. UX Principle — Owner

Show:

```text
money available
where
difference
risk
```

not reconciliation jargon first.

---

# 487. UX Principle — Accountant

Show:

```text
external
book
match
difference
drilldown
```

---

# 488. UX Principle — Cash Custodian

Show:

```text
expected
count
difference
transfers
```

---

# 489. UX Principle — Treasurer

Show:

```text
position
forecast
in transit
settlement
liquidity risk
```

---

# 490. Account Workspace Tabs

Potential:

```text
Overview
Activity
Statements
Reconciliation
Transfers
Forecast
Account Details
Controls
History
```

---

# 491. Cash Account Workspace

Potential:

```text
Overview
Movements
Counts
Transfers
Custody
History
```

---

# 492. Wallet Workspace

Potential:

```text
Overview
Provider Transactions
Reconciliation
Payouts
Fees
History
```

---

# 493. Reconciliation UI

Two-sided working area:

```text
External side
↔
TODIR side
```

plus suggested matches.

---

# 494. One-Click Exact Match

Safe when candidate unique and authorized.

---

# 495. Bulk Accept Exact Matches

Allowed with preview/audit.

---

# 496. Suggested Match Must Show Why

No opaque suggestion.

---

# 497. Unknown Line Quick Actions

Examples:

```text
Find Payment
Create Receipt
Classify Bank Fee
Classify Interest
Create Transfer Match
Mark Needs Investigation
```

---

# 498. Investigation Queue

Unknown/suspicious lines assigned to user/team.

---

# 499. Investigation Is Not Reconciliation

Line remains unreconciled until resolved.

---

# 500. Fraud/Error Signal

Unexpected debit/credit can be flagged:

```text
Suspicious
Bank Error
Possible Fraud
```

without auto-accounting.



# 501. Business Scenario 1 — Multiple Bank Accounts

Company:

```text
TODIR Egypt LLC

CIB EGP
NBE EGP
CIB USD
QNB Payroll
```

Each account is a separate TreasuryAccount.

---

# 502. Scenario 1 Acceptance

User can see:

```text
per account balance
currency
purpose
status
last reconciliation
```

and aggregate authorized position.

---

# 503. Business Scenario 2 — Cash Safe

```text
Head Office Safe
Expected = 100,000
Counted  = 99,950
Difference = -50
```

---

# 504. Scenario 2 Acceptance

No balance edit.

Requires count/difference resolution.

---

# 505. Business Scenario 3 — Petty Cash

```text
Factory Petty Cash
Target Float = 20,000
Expenses paid = 7,500
Remaining expected = 12,500
```

Replenishment:

```text
Bank → Petty Cash 7,500
```

---

# 506. Scenario 3 Acceptance

Expense remains Expenses-owned; cash movement remains Payment/Treasury movement.

---

# 507. Business Scenario 4 — Digital Wallet

```text
Company Wallet EGP
Customer receipt +10,000
Wallet fee -50
Transfer to bank -9,950
```

---

# 508. Scenario 4 Acceptance

Wallet balance/reconciliation explain all three events.

---

# 509. Business Scenario 5 — Provider Settlement

```text
Customer Payments 100,000
Provider Fee         2,500
Net Payout          97,500
```

---

# 510. Scenario 5 Acceptance

No duplicate customer payment.

Gateway/settlement account clears to bank.

---

# 511. Business Scenario 6 — Payment Before Bank

```text
Supplier Payment recorded 50,000
Bank not cleared yet
```

TODIR shows:

```text
Book movement exists
External unmatched pending
```

---

# 512. Scenario 6 Acceptance

Payment can be Completed but Reconciliation Unmatched.

---

# 513. Business Scenario 7 — Bank Before Payment

```text
Bank Credit 100,000
Reference XYZ
```

No Payment exists.

---

# 514. Scenario 7 Flow

```text
Statement Line
→ find Customer XYZ
→ create incoming Payment
→ allocate if known
→ reconcile
```

---

# 515. Business Scenario 8 — One Receipt Pays Two Invoices

```text
Bank Credit 100,000
Invoice A 60,000
Invoice B 40,000
```

---

# 516. Scenario 8 Acceptance

One money movement can settle many open items.

Banking does not create invoice settlement itself.

---

# 517. Business Scenario 9 — Bank Fee

```text
Statement Debit 250
Description Bank Fee
```

---

# 518. Scenario 9 Acceptance

Typed bank-originated event created/approved.

Ledger posting explainable.

---

# 519. Business Scenario 10 — Internal Bank Transfer

```text
CIB EGP → NBE EGP
500,000
```

---

# 520. Scenario 10 Flow

```text
Create transfer
CIB clears
→ In Transit
NBE clears
→ Fully Reconciled
```

---

# 521. Business Scenario 11 — Cash Deposit

```text
Main Safe → CIB EGP
100,000
```

---

# 522. Scenario 11 Acceptance

Cash expected balance decreases; bank credit later matches transfer.

---

# 523. Business Scenario 12 — Bank Withdrawal To Safe

```text
CIB EGP → Main Safe
50,000
```

---

# 524. Scenario 12 Acceptance

Bank debit and cash receipt/count both explain movement.

---

# 525. Business Scenario 13 — POS Cash Handover

```text
POS Session Counted Cash 15,000
→ Main Store Safe
```

---

# 526. Scenario 13 Acceptance

POS Session remains POS-owned.

Treasury Safe receives settlement once.

---

# 527. Business Scenario 14 — POS Cash Difference

```text
POS expected 15,000
POS counted 14,950
Difference -50
```

POS owns session difference.

Banking receives only approved settlement amount/context.

---

# 528. Business Scenario 15 — Safe Cash Difference

```text
Safe expected 100,000
Counted 99,500
Difference -500
```

Banking owns cash count difference workflow.

---

# 529. Business Scenario 16 — Cross-Currency Transfer

```text
USD Bank → EGP Bank
1,000 USD → 52,000 EGP
Fee 100 EGP
```

---

# 530. Scenario 16 Acceptance

Source/destination Money + FX snapshot + fee explicit.

---

# 531. Business Scenario 17 — Cross-Entity Attempt

```text
Egypt CIB → Saudi Al Rajhi
```

ordinary transfer blocked.

---

# 532. Business Scenario 18 — Statement Duplicate

Same CAMT statement imported twice.

Second import idempotent/rejected duplicate.

---

# 533. Business Scenario 19 — Corrected Statement

Bank publishes correction/reversal.

Original remains linked.

---

# 534. Business Scenario 20 — Cheque In Transit

Issued cheque appears in Payments but not yet bank-cleared.

Shown as pending/unreconciled.

---

# 535. Business Scenario 21 — Returned Cheque

Bank returns cheque.

Payment/Subledger correction coordinated.

---

# 536. Business Scenario 22 — Missing Daily Feed

Bank connection stale.

TODIR marks balance stale, not zero.

---

# 537. Business Scenario 23 — Cash Position

```text
Bank Available     3,500,000
Cash Safes           200,000
Wallet               100,000
Pending Settlement   500,000
Restricted           300,000
```

---

# 538. Scenario 23 Acceptance

Dashboard can separately show:

```text
Immediately Available
Pending Settlement
Restricted
```

---

# 539. Business Scenario 24 — 7-Day Forecast

```text
Current Available      3.8M
Expected Receipts     +2.0M
Supplier Payments     -1.1M
Payroll               -1.2M
Tax                   -0.3M
Forecast               3.2M
```

---

# 540. Scenario 24 Acceptance

Every forecast line drills to source.

---

# 541. Business Scenario 25 — Overdraft

```text
Bank Balance     -100,000
Overdraft Limit   500,000
Available Facility 400,000
```

---

# 542. Scenario 25 Acceptance

Debt/available facility not mislabeled as owned cash.

---

# 543. Test — Treasury Account Not GL

Change Ledger mapping; Treasury identity unchanged.

---

# 544. Test — Payment Account Normalization

Payments references Banking TreasuryAccount; no duplicate editable master.

---

# 545. Test — Bank Account Entity Scope

Entity B cannot use Entity A account.

---

# 546. Test — Cash Account

Valid without IBAN/bank profile.

---

# 547. Test — Wallet

Valid without bank fields.

---

# 548. Test — Settlement Account

Pending provider funds tracked separately from Bank.

---

# 549. Test — Multi-Currency Pocket

Balances never aggregated without currency.

---

# 550. Test — Sensitive Masking

Unauthorized API returns masked account identifier only.

---

# 551. Test — Account Close

Blocked with open/in-transit/unreconciled items according to policy.

---

# 552. Test — Closed Account

Historical reconciliation remains searchable.

---

# 553. Test — Account Change Approval

Sensitive change not active before approval.

---

# 554. Test — Legal Entity Change

Active historical account cannot silently switch entity.

---

# 555. Test — Statement Import

Valid lines/data preserved.

---

# 556. Test — Statement Duplicate

Idempotent.

---

# 557. Test — Statement Currency Mismatch

Rejected/exception.

---

# 558. Test — Statement Balance Continuity

Mismatch surfaced.

---

# 559. Test — Feed Transaction Duplicate

No duplicate external line.

---

# 560. Test — Reversal Statement Line

Original preserved.

---

# 561. Test — 1:1 Match

Exact amount/reference.

---

# 562. Test — 1:Many Match

One external line matched to multiple eligible system items.

---

# 563. Test — Many:1 Match

Correct sum and uniqueness.

---

# 564. Test — Many:Many Match

Balanced and no claimed item reuse.

---

# 565. Test — Partial Match

Remaining amount explicit.

---

# 566. Test — Match Conflict

Two users cannot claim same statement amount simultaneously.

---

# 567. Test — Suggested Match

Suggestion does not reconcile until accepted/finalized.

---

# 568. Test — Ambiguous Match

No auto-match when two candidates equal.

---

# 569. Test — Bank Fee

Creates explicit event/fact and reconciles.

---

# 570. Test — Interest Income

Typed and posted correctly.

---

# 571. Test — Unknown Receipt

Can become unapplied customer Payment.

---

# 572. Test — Fake Customer

System never creates one to force match.

---

# 573. Test — Unknown Debit

Can remain investigation exception.

---

# 574. Test — Tolerance

Difference handling explicit.

---

# 575. Test — FX Difference

No silent rounding/FX loss.

---

# 576. Test — Finalized Reconciliation

Ordinary edits rejected.

---

# 577. Test — Reopen Reconciliation

Permission/audit required.

---

# 578. Test — Payment Completed Unmatched

Supported.

---

# 579. Test — Bank Line Before Payment

Payment creation idempotent.

---

# 580. Test — Payment Before Bank

External match later.

---

# 581. Test — Provider Payout

No duplicate AR/customer receipt.

---

# 582. Test — Provider Fee

Gross/fee/net reconcile.

---

# 583. Test — Wallet Payout

Wallet decreases and Bank increases through one transfer/settlement flow.

---

# 584. Test — Cash Count

Expected/count/difference exact.

---

# 585. Test — Denomination Count

Sum equals counted balance.

---

# 586. Test — Cash Short

No direct balance overwrite.

---

# 587. Test — Cash Difference Approval

Threshold enforced.

---

# 588. Test — Custodian Handover

Count + acceptance + history.

---

# 589. Test — POS Settlement

One Treasury receipt from closed session.

---

# 590. Test — POS Session Remains POS-Owned

Banking cannot alter Session cash.

---

# 591. Test — Internal Transfer

No customer/supplier Open Item.

---

# 592. Test — Source Cleared First

Transfer In Transit.

---

# 593. Test — Destination Cleared

Transfer fully reconciled.

---

# 594. Test — Cross-Entity Transfer

Blocked.

---

# 595. Test — Cross-Currency Transfer

Source/destination/FX/fee exact.

---

# 596. Test — Stale External Balance

UI marks freshness.

---

# 597. Test — Provider Outage

No balance set to zero.

---

# 598. Test — Cash Position

Original currencies preserved.

---

# 599. Test — Group Position

Authorized only.

---

# 600. Test — Restricted Cash

Excluded from immediately available metric if policy.

---

# 601. Test — Forecast Does Not Post

No Ledger entry.

---

# 602. Test — Forecast Source Correction

Forecast updates/rebuilds, actual history unchanged.

---

# 603. Test — Manual Forecast Item

Clearly planning-only.

---

# 604. Test — Payroll Forecast

Only aggregate outflow, no salary detail.

---

# 605. Test — Forecast Currency

Explicit conversion basis.

---

# 606. Test — Cheque In Transit

Not false unexplained difference.

---

# 607. Test — Returned Cheque

Owning domains corrected.

---

# 608. Test — Cross Tenant RLS

Tenant A cannot read B account/statement.

---

# 609. Test — Cross Entity Authorization

Entity scope enforced.

---

# 610. Test — Full IBAN Permission

Masked unless authorized.

---

# 611. Test — Reconciler SoD

Configured maker/checker enforced.

---

# 612. Test — Account Change Audit

Protected old/new values recorded appropriately.

---

# 613. Test — Connector Secret

Never returned to client.

---

# 614. Test — Parser Security

Malformed file safely rejected.

---

# 615. Test — 10M Statement Lines

Hot workbench indexes measured.

---

# 616. Test — Large Matching Batch

No N+1/per-line external calls.

---

# 617. Test — Cash Position Many Accounts

Projection scale.

---

# 618. Test — Feed Burst

Idempotent import under retries.

---

# 619. Property Test — Match Conservation

For every finalized match:

```text
External amount
=
System amount
+
Explicit differences
```

within currency/rule semantics.

---

# 620. Property Test — No Double Claim

One source amount cannot be reconciled twice.

---

# 621. Property Test — Cash Count

```text
Difference = Counted - Expected
```

---

# 622. Property Test — Same-Currency Transfer

```text
Source Amount
=
Destination Amount + explicit fee effects
```

according to fee model.

---

# 623. Property Test — Cash Position

Every displayed amount has:

```text
Currency
AsOf
Source/Basis
```

---

# 624. Architecture Test — No Journal Direct Write

Banking cannot create Ledger rows.

---

# 625. Architecture Test — No Payment Duplicate

Banking does not own `payment` table.

---

# 626. Architecture Test — No AR/AP Settlement

Banking cannot mutate Subledger tables.

---

# 627. Architecture Test — No POS Session Mutation

Forbidden.

---

# 628. Architecture Test — TreasuryAccount Canonical

Payments cannot maintain separate Bank/Cash/Wallet master.

---

# 629. Red-Team — Bank Account = GL Account

Reject.

---

# 630. Red-Team — Editable Current Balance

Reject.

---

# 631. Red-Team — Bank Statement Line = Payment

Reject.

---

# 632. Red-Team — Imported = Reconciled

Reject.

---

# 633. Red-Team — Match = Reconciled

Reject.

---

# 634. Red-Team — Bank Balance = Book Balance

Reject.

---

# 635. Red-Team — Stale Balance Shown As Live

Reject.

---

# 636. Red-Team — Unknown Receipt Creates Fake Customer

Reject.

---

# 637. Red-Team — Difference Disappears Into Tolerance

Reject.

---

# 638. Red-Team — Provider Payout Creates New Customer Revenue

Reject.

---

# 639. Red-Team — Cash Count Edits Balance

Reject.

---

# 640. Red-Team — POS Drawer Duplicated As Treasury Safe

Reject unless explicitly separate physical account.

---

# 641. Red-Team — Cross-Entity Internal Transfer

Reject.

---

# 642. Red-Team — Forecast Treated As Cash

Reject.

---

# 643. Red-Team — Overdraft Treated As Owned Cash

Reject.

---

# 644. Red-Team — Wallet = Payment Method

Reject.

---

# 645. Red-Team — Gateway Settlement = Bank Account

Reject.

---

# 646. Red-Team — Internet Banking Password Stored

Reject.

---

# 647. Red-Team — AI Auto-Reconciles Ambiguous Match

Reject.

---

# 648. Red-Team — Final Reconciliation Silently Edited

Reject.

---

# 649. Red-Team — Manual Bank Fee Writes Journal Directly

Reject.

---

# 650. Red-Team — Ledger Clearing Account Always Treasury Account

Reject.



# 651. Competitive Benchmark — Microsoft Dynamics 365 Finance

Current Dynamics Cash and bank management patterns validate:

```text
Legal-entity bank account masters
Advanced bank reconciliation
Electronic bank statement import
Matching rules / rule sets
Bank transaction types
Bank account workspaces
Pending/bridged amounts
```

Official references:

- https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/cash-bank-management
- https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/advanced-bank-reconciliation-overview
- https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/configure-advanced-bank-reconciliation
- https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/bank-management-workspace

TODIR adopts:

- explicit bank-account master;
- statement import + reconciliation;
- versioned matching rules;
- bank transaction classification;
- pending/in-transit visibility.

TODIR extends the canonical abstraction beyond banks to:

```text
Cash
Wallet
Settlement accounts
```

---

# 652. Competitive Benchmark — SAP S/4HANA Cash & Liquidity Management

Current SAP Bank Relationship Management validates:

```text
bank/house-bank account management
bank account lifecycle
dual control / workflow
payment approvers
account groups
cash pools / concentration
```

Official reference:

- https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/ac319d8fa4ea4624b40a58d23e3c4627/59d5a052c3ad224fe10000000a445394.html

TODIR adopts:

- sensitive account governance;
- maker-checker;
- account lifecycle;
- explicit payment-use/approval context;
- future cash-pool seam.

TODIR does not require enterprise treasury workflow for small companies by default.

---

# 653. Competitive Benchmark — Oracle Fusion Cash Management

Current Oracle patterns validate:

```text
banks / branches / accounts
cash management dashboard
unreconciled cash
cash at risk
cash positioning
short-term cash forecast
cash-in-transit
cash-to-GL reconciliation
```

Official references:

- https://docs.oracle.com/en/cloud/saas/financials/26b/facsf/how-you-implement-cash-management.html
- https://docs.oracle.com/en/cloud/saas/financials/26b/faofc/overview-of-the-cash-management-dashboard.html
- https://docs.oracle.com/en/cloud/saas/financials/26b/fappp/oracle-fusion-cash-management-predefined-reports.html
- https://docs.oracle.com/en/cloud/saas/financials/26b/fappp/cash-to-general-ledger-reconciliation-report.html

TODIR adopts:

- balance/reconciliation dashboard;
- cash-position + forecast distinction;
- cash-in-transit;
- book-to-bank reconciliation;
- source drilldown.

---

# 654. Competitive Benchmark — Oracle Matching Cardinality

Oracle reconciliation rule documentation validates:

```text
1:1
1:Many
Many:1
Many:Many
```

Official reference:

- https://docs.oracle.com/en/cloud/saas/financials/26d/fairp/reconciliation-matching-rules.html

TODIR requires all four cardinalities in architecture.

---

# 655. Competitive Benchmark — NetSuite Banking

Current NetSuite 2026.2 patterns validate:

```text
Bank Feeds
Match Bank Data
Rule-based matching
Match Suggestions
Manual review
Auto-create rules
System notes / reconciliation audit
```

Official references:

- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/subsect_0330111247.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/article_32131451402.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N1551275.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_90174746588.html

TODIR adopts:

- imported feed workflow;
- deterministic matching rules;
- explicit suggestions;
- auto-create rules for narrow cases;
- audit of system vs user matches.

---

# 656. Competitive Benchmark — Odoo 19

Odoo 19 provides a strong simplicity reference for:

```text
bank/cash accounts
reconciliation models
bank fees
internal transfers
```

Official references:

- https://www.odoo.com/documentation/19.0/applications/finance/accounting/bank/reconciliation_models.html
- https://www.odoo.com/documentation/19.0/applications/finance/accounting/bank/internal_transfers.html

TODIR adopts:

- simple reconciliation UX;
- explicit internal transfers;
- easy bank-fee classification.

TODIR adds stronger:

```text
TreasuryAccount abstraction
Cash/Wallet/Settlement ownership
external vs book balance separation
cash position
forecast
maker-checker
complex matching
provider-settlement reconciliation
```

---

# 657. Competitive Product Rule

Take:

```text
SAP/Oracle treasury correctness
Dynamics reconciliation structure
NetSuite matching assistance
Odoo simplicity
```

without copying their UI/setup burden.

---

# 658. Competitive Product Target

> **A small accountant should reconcile today's bank in minutes; a larger treasury team should be able to control many accounts/entities without changing the domain core.**

---

# 659. Agent Implementation Philosophy

Implement treasury truth and reconciliation first.

Do not begin with a dashboard full of balances whose provenance is unclear.

---

# 660. Agent Precedence

If conflict:

```text
25 Banking & Cash Management
→ owning domain pack for source truth
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ approved ADR
```

---

# 661. Critical Upstream Ownership

When conflict concerns:

```text
Payment lifecycle       → 12 Payments
Open Item settlement    → 10 Subledger
Journal/account mapping → 09 Ledger
POS Session cash        → 20 POS
Payroll                 → 18 Payroll
```

those packs retain source ownership.

`25` normalizes account master ownership as explicitly documented in this pack.

---

# 662. Agent Must Read

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
16
18
20
21
23
24
25
all AGENTS.md
approved ADRs
actual public contracts
```

---

# 663. Agent Baseline Report

Before code, map:

```text
PaymentAccount current implementation
Payments internal transfer
Provider settlement
Ledger cash mappings
Subledger payment allocation
POS session cash
Payroll planned payment date
Platform Files
Approvals
Audit
Numbering
Outbox/Inbox
Jobs
RLS
Connections/plugins
```

---

# 664. PaymentAccount Migration Decision

Agent must explicitly report:

```text
Does repo already persist PaymentAccount?
Which types?
Which modules reference it?
Can canonical ownership move to Banking without breaking IDs?
```

---

# 665. Recommended Migration Strategy

If `PaymentAccount` already exists in Payments:

```text
1. Preserve stable IDs where feasible.
2. Introduce TreasuryAccount canonical record.
3. Backfill TreasuryAccount from real money endpoint PaymentAccounts.
4. Add Payments reference contract.
5. Deprecate independent master mutation in Payments.
6. Migrate callers.
7. Remove duplicate source-of-truth path only after tests.
```

---

# 666. Do Not Big-Bang Rename Blindly

Protect migrations/history/foreign refs.

Use ADR if stable ID strategy differs.

---

# 667. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL

REAL-WORLD WORKFLOW

SOURCE OF TRUTH

STATE / INVARIANTS

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

DATA MIGRATION REQUIREMENTS

DATABASE REQUIREMENTS

INTEGRATION REQUIREMENTS

ACCOUNTING REQUIREMENTS

SECURITY / SOD REQUIREMENTS

PERFORMANCE REQUIREMENTS

UX REQUIREMENTS

TESTS REQUIRED

ACCEPTANCE CRITERIA

EXIT GATE

REPORT BACK WITH
- files changed
- migrations
- contracts used/added
- tests/results
- reconciliation evidence
- security evidence
- performance evidence
- unresolved decisions
- risks
```

---

# 668. Implementation Sequence

Recommended:

```text
BK00 Repository / Dependency Verification
BK01 PaymentAccount Normalization ADR
BK02 Module Boundary + Schema + RLS
BK03 TreasuryAccount Core
BK04 Bank Account Profile
BK05 Cash Account / Safe
BK06 Digital Wallet
BK07 Settlement Account
BK08 Currency Pockets / Liquidity Class
BK09 Ledger Mapping Readiness
BK10 Account Lifecycle / Maker-Checker
BK11 Bank Connection Foundation
BK12 Statement Import Batch
BK13 Statement Canonical Normalization
BK14 Duplicate / Continuity Validation
BK15 Transaction Code Mapping
BK16 Matching Rule Engine
BK17 Reconciliation Session / Worksheet
BK18 1:1 Matching
BK19 1:Many / Many:1 / Many:Many / Partial
BK20 Payments Reconciliation Integration
BK21 Unknown Receipt / Bank-First Payment
BK22 Bank-Originated Fees / Interest / Direct Debit
BK23 Provider Settlement Reconciliation
BK24 Digital Wallet Reconciliation
BK25 Internal Transfer Reconciliation
BK26 Cash Count
BK27 Cash Difference / Custody
BK28 POS Cash Handover
BK29 Cheque / In-Transit Projection
BK30 Multi-Currency / FX
BK31 Cash Position
BK32 Cash Forecast
BK33 Group / Multi-Entity Read Views
BK34 Account / Connection Health
BK35 Reports / Cash-to-GL
BK36 Migration / Opening Reconciliation
BK37 Permissions / SoD / Security
BK38 Performance / Scale
BK39 Observability / Operations
BK40 Runbooks / Recovery
BK41 Accountant Reconciliation Acceptance
BK42 Cash Custodian Acceptance
BK43 Treasurer / CFO Acceptance
BK44 Payments / POS / Ledger Cross-Module Reconciliation
BK45 Cross-Module E2E
BK46 Release Gate
```

---

# 669. BK00 Goal

Inspect repository truth before implementation.

---

# 670. BK00 Exit Gate

No assumptions about current PaymentAccount schema remain.

---

# 671. BK01 Goal

Resolve canonical account ownership.

---

# 672. BK01 Deliverable

Approved ADR covering:

```text
PaymentAccount compatibility
TreasuryAccount canonical IDs
migration path
Payments references
provider clearing handling
pure Ledger clearing handling
```

---

# 673. BK01 Exit Gate

Exactly one editable master for each real treasury account.

---

# 674. BK02 Goal

Create module boundary.

---

# 675. BK02 Build

```text
Todir.Modules.Banking.Domain
Todir.Modules.Banking.Application
Todir.Modules.Banking.Infrastructure
Todir.Banking.Contracts
BankingDbContext
finance_banking
module manifest
RLS
architecture tests
```

---

# 676. BK02 Exit Gate

No direct cross-module table access.

---

# 677. BK03 Goal

TreasuryAccount core.

---

# 678. BK03 Build

```text
types
legal entity
currency mode
purpose
allowed use
lifecycle
liquidity
```

---

# 679. BK03 Exit Gate

One company can create many accounts of different types.

---

# 680. BK04 Goal

Bank profile.

---

# 681. BK04 Exit Gate

Sensitive identifiers masked/encrypted, lifecycle correct.

---

# 682. BK05 Goal

Cash safes/petty cash.

---

# 683. BK05 Exit Gate

Cash account works without bank fields and supports custodian/count policy.

---

# 684. BK06 Goal

Digital Wallet.

---

# 685. BK06 Exit Gate

Wallet holds/reconciles balance separately from Payment Method.

---

# 686. BK07 Goal

Provider Settlement Account.

---

# 687. BK07 Exit Gate

Gateway pending funds distinguishable from Bank and Customer Payment.

---

# 688. BK08 Goal

Currency/liquidity foundation.

---

# 689. BK08 Exit Gate

Multi-currency external account does not create ambiguous balance.

---

# 690. BK09 Goal

Ledger readiness.

---

# 691. BK09 Exit Gate

Banking knows accounting readiness without owning GL IDs.

---

# 692. BK10 Goal

Account governance.

---

# 693. BK10 Build

```text
Draft
Approval
Activate
Suspend
Close
Sensitive change request
```

---

# 694. BK10 Exit Gate

IBAN/account master change is audited and policy-controlled.

---

# 695. BK11 Goal

Secure Bank/Provider connection abstraction.

---

# 696. BK11 Exit Gate

No credentials in domain/browser logs.

---

# 697. BK12 Goal

Import raw statements safely.

---

# 698. BK12 Exit Gate

Bulk/idempotent import with provenance.

---

# 699. BK13 Goal

Canonical statement/line model.

---

# 700. BK13 Exit Gate

CAMT/MT940/CSV etc can normalize without source-specific domain branches.

---

# 701. BK14 Goal

Validation.

---

# 702. BK14 Build

```text
duplicates
currency
account
balance continuity
external IDs
```

---

# 703. BK14 Exit Gate

Bad statement cannot silently enter reconciliation.

---

# 704. BK15 Goal

Bank transaction code mapping.

---

# 705. BK15 Exit Gate

Unknown codes reviewable; historical versions reproducible.

---

# 706. BK16 Goal

Deterministic matching rules.

---

# 707. BK16 Exit Gate

Rules are versioned/explainable/constrained.

---

# 708. BK17 Goal

Reconciliation workbench.

---

# 709. BK17 Exit Gate

External and system sides visible without creating duplicate truth.

---

# 710. BK18 Goal

Reliable exact 1:1.

---

# 711. BK18 Exit Gate

Unique exact reference/amount matches retry safely.

---

# 712. BK19 Goal

Complex cardinality.

---

# 713. BK19 Exit Gate

1:M / M:1 / M:M / partial amounts reconcile without double claim.

---

# 714. BK20 Goal

Payments integration.

---

# 715. BK20 Exit Gate

Completed Payment can match/unmatch independently; no Payment row owned by Banking.

---

# 716. BK21 Goal

Bank-first receipt workflow.

---

# 717. BK21 Exit Gate

Statement can create/propose Payment once and optionally allocate through Subledger.

---

# 718. BK22 Goal

Bank-originated events.

---

# 719. BK22 Build

```text
fee
interest
direct debit
unknown debit/credit
```

---

# 720. BK22 Exit Gate

No direct journal write; accounting remains semantic/traceable.

---

# 721. BK23 Goal

Provider settlement.

---

# 722. BK23 Exit Gate

Gross/fees/net payout reconcile without duplicate customer cash.

---

# 723. BK24 Goal

Digital Wallet.

---

# 724. BK24 Exit Gate

Wallet transaction/feed + payout reconcile.

---

# 725. BK25 Goal

Internal transfers.

---

# 726. BK25 Exit Gate

Source/destination external evidence resolves one business transfer with In-Transit state.

---

# 727. BK26 Goal

Cash counts.

---

# 728. BK26 Build

```text
expected
denominations
counted
difference
```

---

# 729. BK26 Exit Gate

No editable cash balance.

---

# 730. BK27 Goal

Cash difference/custody.

---

# 731. BK27 Exit Gate

Differences and handovers are approved/history-preserving.

---

# 732. BK28 Goal

POS settlement.

---

# 733. BK28 Exit Gate

POS session remains POS-owned; one settlement reaches treasury safe.

---

# 734. BK29 Goal

Cheques / in-transit visibility.

---

# 735. BK29 Exit Gate

Issued/received uncleared items explain book-vs-bank gap.

---

# 736. BK30 Goal

Multi-currency/FX.

---

# 737. BK30 Exit Gate

All source/destination amounts/currencies/snapshots explicit.

---

# 738. BK31 Goal

Cash Position.

---

# 739. BK31 Exit Gate

Every balance basis/source/freshness explicit.

---

# 740. BK32 Goal

Short-term cash forecast.

---

# 741. BK32 Exit Gate

Forecast combines source projections without posting accounting.

---

# 742. BK33 Goal

Authorized group views.

---

# 743. BK33 Exit Gate

Group aggregation never breaks Legal Entity ownership.

---

# 744. BK34 Goal

Operational account/feed health.

---

# 745. BK34 Exit Gate

Stale connection/balance visible and actionable.

---

# 746. BK35 Goal

Reports/reconciliation drilldown.

---

# 747. BK35 Exit Gate

Cash-to-GL difference explainable.

---

# 748. BK36 Goal

Migration/opening reconciliation.

---

# 749. BK36 Exit Gate

Opening external/book/in-transit states reconcile without fake payments.

---

# 750. BK37 Goal

Security/SoD red-team.

---

# 751. BK37 Attack Cases

```text
Cross-tenant statement read
Cross-entity account use
Full IBAN leakage
Unauthorized account master change
Forged TreasuryAccountId in Payment
Forged reconciliation match
Duplicate match claim
Unauthorized cash difference write-off
Connector secret leak
Malicious statement file
Duplicate import replay
Unauthorized reconciliation reopen
AI suggestion auto-accepted without policy
```

---

# 752. BK37 Exit Gate

RLS + app auth + field masking + SoD pass.

---

# 753. BK38 Goal

Performance.

---

# 754. BK38 Load Tests

```text
100k Treasury Accounts across platform
10M statement lines
large reconciliation month-end
large provider settlement batches
large cash-position refresh
many concurrent accountants
```

tenant-specific realistic subsets.

---

# 755. BK38 Query Plan Review

Real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot queries.

---

# 756. BK38 Exit Gate

Targets pass or ADR.

---

# 757. BK39 Goal

Operational visibility.

---

# 758. BK39 Exit Gate

Imports/jobs/feeds/matches have metrics/traces/run states.

---

# 759. BK40 Goal

Semantic recovery without DB surgery.

---

# 760. BK40 Runbooks

Required:

```text
Duplicate statement
Bad statement balance
Wrong account import
Missing statement
Feed connection failure
Duplicate external transaction
Wrong suggested match
Finalized wrong reconciliation
Unknown receipt
Unknown debit
Bank fee misclassification
Provider payout mismatch
Cash count difference
Cash handover dispute
Transfer stuck in transit
Wallet balance mismatch
POS settlement mismatch
Book-vs-bank difference
Projection rebuild
DR restore validation
```

---

# 761. BK40 Forbidden Runbook

Never normal fix:

```text
UPDATE treasury_account SET balance=...
DELETE bank_statement_line...
UPDATE reconciliation_match SET amount=...
UPDATE cash_count SET difference=0...
UPDATE payment SET bank_account_id=...
```

outside semantic migration/correction tooling.

---

# 762. BK40 Exit Gate

Critical failures have documented semantic correction.

---

# 763. BK41 Accountant Reconciliation Acceptance

Reviewer:

```text
Senior Accountant / Bank Reconciliation Specialist
```

---

# 764. BK41 Scenario

```text
Import statement
run rules
review suggestions
classify bank fee
create unknown receipt Payment
match transfer
finalize reconciliation
```

---

# 765. BK41 Pass Criteria

```text
fast
explainable
no spreadsheet
no hidden difference
full source trace
```

---

# 766. BK42 Cash Custodian Acceptance

Reviewer:

```text
Treasury Cashier / Cash Custodian
```

---

# 767. BK42 Scenario

```text
Open safe
receive/transfer cash
count denominations
explain difference
handover custody
```

---

# 768. BK42 Pass Criteria

No access to irrelevant bank/admin secrets.

---

# 769. BK43 Treasurer/CFO Acceptance

Reviewer:

```text
Treasurer / CFO
```

---

# 770. BK43 Scenario

Can answer:

```text
Where is cash now?
How much is available?
Which accounts are stale?
What's in transit?
What is unreconciled?
What happens in 7/30 days?
Where is liquidity below target?
```

---

# 771. BK43 Pass Criteria

No manual consolidation spreadsheet for normal cash position.

---

# 772. BK44 Cross-Module Reconciliation

Validate:

```text
Banking matched Payment movements
= Payments canonical movements

Payment allocations
= Subledger canonical settlement

Book balance
= Ledger mapped cash balances

POS cash settlement
= POS closed-session handoff

Provider settlement
= Payments provider settlement source

Cash difference accounting
= Ledger Banking fact postings
```

---

# 773. BK44 Exit Gate

Zero unexplained differences.

---

# 774. BK45 E2E — Customer Receipt

```text
Invoice/Open Item
→ Customer Payment
→ Bank Statement
→ Match
→ Reconcile
→ Cash Position
```

---

# 775. BK45 E2E — Supplier Payment

```text
Supplier Open Item
→ Payment
→ Bank debit
→ Match
→ Reconcile
```

---

# 776. BK45 E2E — Bank-First Receipt

```text
Bank credit
→ Payment creation
→ Subledger allocation
→ reconcile
```

---

# 777. BK45 E2E — Internal Transfer

```text
CIB
→ Payment Internal Transfer
→ In Transit
→ NBE
→ both statements
→ reconciled
```

---

# 778. BK45 E2E — Cash Deposit

```text
Safe
→ Transfer
→ Bank
→ Statement
→ Reconcile
```

---

# 779. BK45 E2E — POS Cash

```text
POS Session
→ Close/Count
→ Safe handoff
→ Treasury receipt
→ Cash count
```

---

# 780. BK45 E2E — Wallet

```text
Customer Payment
→ Wallet
→ wallet statement
→ transfer
→ Bank statement
→ reconcile
```

---

# 781. BK45 E2E — Gateway

```text
Customer card payments
→ Provider settlement
→ fees
→ net payout
→ Bank
→ reconcile
```

---

# 782. BK45 E2E — Cash Forecast

```text
Bank/Cash position
+ AR due
- AP due
- Payroll
- Tax
→ 30-day liquidity
```

---

# 783. BK45 Exit Gate

Real PostgreSQL integration tests pass ownership/reconciliation invariants.

---

# 784. BK46 Release Gate

Banking does not ship because accounts can be created.

It ships when company cash can be trusted, explained and reconciled.

---

# 785. BK46 Required Sign-Off

```text
Product Architect
CFO/Treasurer reviewer
Senior Accountant
Bank Reconciliation reviewer
Cash Custodian reviewer
Payments Lead
Ledger/Accounting Lead
POS Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 786. BK46 Mandatory Evidence

```text
Automated tests
Architecture boundary tests
RLS/security report
Sensitive-field masking tests
PaymentAccount normalization migration tests
Statement parser/idempotency tests
Matching cardinality tests
Reconciliation conservation tests
Cash count/difference tests
Transfer in-transit tests
Provider settlement reconciliation
POS settlement reconciliation
Cash-to-GL reconciliation
Forecast source tests
Performance benchmarks
Runbooks
Accountant acceptance
Treasurer acceptance
Known limitations
```

---

# 787. Build Now — MUST

```text
TreasuryAccount canonical master
Bank Accounts
Cash Accounts / Safes / Petty Cash
Digital Wallets
Provider Settlement Accounts
Currency pockets foundation
Legal Entity ownership
Purpose / Allowed Uses
Lifecycle / sensitive change governance
Payments PaymentAccount normalization
Ledger mapping readiness
Statement import
Canonical statement lines
Duplicate/continuity validation
Transaction code mapping
Matching rules
1:1 / 1:M / M:1 / M:M / partial matching
Reconciliation sessions
Unknown receipt workflow
Bank fees / interest / direct debit classification
Provider settlement reconciliation
Wallet reconciliation
Internal transfer reconciliation
Cash Count
Cash Difference
Custodian handover foundation
POS cash handoff
Cheque/in-transit visibility foundation
Multi-currency/FX
Cash Position
Short-term Cash Forecast
Permissions/RLS/SoD/Audit
Reports
Migration/opening reconciliation
Performance
Runbooks
Acceptance gates
```

---

# 788. Build Later / Evidence-Based

```text
Advanced Cash Pooling
Automated Sweeps
Central Payment Factory
Intercompany Treasury
Debt / Loan Management
Investment Management
FX Hedge / Treasury Risk
Bank Relationship Fee Analytics
Advanced Liquidity Scenarios
AI transaction classification beyond suggestions
Continuous open-banking auto-actions
Advanced Cheque lifecycle by country
```

---

# 789. Final Source-of-Truth Matrix

```text
Treasury Account Master           → Banking
Bank/Cash/Wallet external balance → Banking
Bank Statement                    → Banking
Statement Line                    → Banking
Cash Count                        → Banking
Reconciliation                    → Banking
Cash Position                     → Banking
Cash Forecast                     → Banking projection

Payment                           → Payments
Internal Transfer movement        → Payments
Provider payment attempt          → Payments
Provider settlement source        → Payments/provider integration

AR/AP Open Item                   → Subledger
Invoice                           → Invoicing
POS Session/Drawer                → POS
Payroll calculation               → Payroll
Expense                           → Expenses
Journal Entry / GL mapping        → Ledger
Currency / FX                     → FinBase
```

---

# 790. Final Account Model

```text
TreasuryAccount
│
├── BankAccountProfile
├── CashAccountProfile
├── DigitalWalletProfile
└── SettlementAccountProfile
```

---

# 791. Final Bank Rule

> **A bank account is a real treasury identity owned by one Legal Entity; it is never the same object as a GL account.**

---

# 792. Final Cash Rule

> **A safe/petty-cash location is a real TreasuryAccount with expected cash and physical counts; its balance is never corrected by editing a number.**

---

# 793. Final Wallet Rule

> **A company digital wallet can be a TreasuryAccount; a payment method called Wallet is a separate concept.**

---

# 794. Final Settlement Rule

> **A gateway/provider settlement balance is distinct from customer Payments and from the destination Bank Account.**

---

# 795. Final Payments Boundary

> **Payments owns money-movement lifecycle; Banking owns the canonical treasury-account master and external reconciliation evidence.**

---

# 796. Final Statement Rule

> **Bank Statement Lines are external evidence and remain immutable/corrective; they never become Payment rows by identity.**

---

# 797. Final Reconciliation Rule

> **Reconciliation explains how external money evidence corresponds to system-side movements; every difference must be explicit.**

---

# 798. Final Matching Rule

> **TODIR supports 1:1, 1:Many, Many:1, Many:Many and partial matching without double-claiming any amount.**

---

# 799. Final Balance Rule

> **Book Balance, External/Bank Balance, Available Balance and Reconciled Balance are separate facts with separate sources and timestamps.**

---

# 800. Final Transfer Rule

> **Movement between Treasury Accounts of the same Legal Entity is an Internal Transfer, not revenue or expense; cross-entity movement is not an ordinary Internal Transfer.**



# 801. Final Cash Count Rule

> **Expected Cash and Counted Cash are separate facts; every difference is explained, approved where required and historically traceable.**

---

# 802. Final POS Rule

> **POS owns cashier/session/drawer accountability; Banking owns company Treasury Cash Accounts and receives POS cash through an explicit settlement/handover.**

---

# 803. Final Provider Rule

> **Provider payment success, provider settlement and bank payout are separate events and must never duplicate customer cash or revenue.**

---

# 804. Final Forecast Rule

> **Cash Position describes actual/current liquidity; Cash Forecast describes expected future liquidity and never posts accounting.**

---

# 805. Final Overdraft Rule

> **An overdraft or credit facility can increase available liquidity but is never presented as company-owned cash.**

---

# 806. Final Security Rule

> **Treasury master data, bank identifiers, connections and reconciliation actions are high-risk financial data/actions and require field masking, server-side authorization, audit and optional segregation of duties.**

---

# 807. Final Small-Company Rule

> **A small company can create two bank accounts and one safe, import a CSV statement, match payments and reconcile without configuring enterprise treasury workflows.**

---

# 808. Final Enterprise Rule

> **A larger group can add feeds, wallets, provider settlement accounts, maker-checker, account restrictions, complex matching, cash position and short-term forecasting without replacing the TreasuryAccount core.**

---

# 809. Final Product Verdict

TODIR Banking & Cash Management is release-ready only when:

```text
A company can own many Treasury Accounts.
Bank, Cash, Wallet and Settlement accounts are distinct profiles.
Every Treasury Account belongs one Legal Entity.
TreasuryAccount is not a GL Account.
Payments no longer owns a duplicate editable account master.
Payment lifecycle remains Payments-owned.
POS Session cash remains POS-owned.
Bank Statements remain Banking-owned external evidence.
Statement Line is not Payment or Journal.
Book Balance and External Balance remain separate.
Available Balance and Reconciled Balance are clearly labeled.
No balance can be corrected by direct edit.
Statements import idempotently.
Duplicate/corrected statements are safe.
Matching rules are deterministic/versioned/explainable.
1:1, 1:M, M:1, M:M and partial matching work.
No source amount can be reconciled twice.
Unknown receipts can become real Payments without fake customers.
Bank fees/interest/direct debits are explicit events.
Provider payouts do not duplicate customer Payments.
Digital Wallets reconcile independently.
Cash Counts preserve expected/count/difference.
Cash differences cannot disappear silently.
Internal Transfers remain movements, not expenses.
In-transit funds remain visible.
Cross-entity transfer is blocked from ordinary internal-transfer flow.
Cash Position shows source/freshness/currency.
Cash Forecast is planning-only and source-drillable.
Cash-to-GL differences are explainable.
Sensitive bank data is masked.
Maker-checker/SoD can be enabled where required.
Performance works on large statement datasets.
Accountant/Cash Custodian/Treasurer acceptance tests pass.
```

---

# 810. Final Agent Rule

For every Banking implementation decision, the Agent must answer:

> **Which real treasury account owns or controls the money, which Legal Entity owns that account, what external evidence exists, what TODIR money movement exists, what the Ledger says, whether the item is matched or reconciled, whether anything is in transit, what balance basis is being shown, and which domain owns any accounting/payment/open-item truth Banking is only referencing?**

If any answer is unclear, the implementation is incomplete.

---

# 811. Agent Rule — Never Hide Provenance

Every displayed treasury number must answer:

```text
Source?
As of when?
Currency?
Legal Entity?
External or Book?
Reconciled or not?
```

---

# 812. Agent Rule — Never Duplicate Money Truth

Before creating a Banking table/entity, ask:

```text
Is this already owned by Payments?
Subledger?
Ledger?
POS?
Payroll?
```

If yes, reference/compose instead.

---

# 813. Agent Rule — Reconciliation Is Evidence

Do not implement reconciliation as:

```text
toggle is_reconciled = true
```

without match items, amounts and history.

---

# 814. Agent Rule — Corrections Preserve History

Wrong:

```text
delete wrong statement line
edit reconciled amount
```

Correct:

```text
reverse
unreconcile
supersede
correction
```

according to business event.

---

# 815. Agent Rule — Balance Is Derived

No endpoint:

```text
PUT /treasury-account/{id}/balance
```

ever.

---

# 816. Agent Rule — Cash Is Physical Reality

For Cash Accounts, physical count is evidence.

A cash shortage is an exception/event, not a number update.

---

# 817. Agent Rule — External Connectivity Is Optional

Banking must still work through:

```text
manual
CSV/XLSX
statement import
```

when direct bank APIs do not exist in target market.

---

# 818. Agent Rule — Country/Provider Adapters

Do not spread:

```text
if EgyptBankX...
if SaudiBankY...
```

through Core.

Use adapters/provider contracts.

---

# 819. Agent Rule — Arabic Market Reality

Architecture must handle common real-world combinations:

```text
multiple local bank accounts
multiple currencies
branch cash safes
petty cash
wallets
payment gateways
cheques
manual statements
limited/no bank API
mixed electronic/manual reconciliation
```

without making any single combination mandatory.

---

# 820. Agent Rule — Automation Earns Trust

Start with:

```text
exact matching
suggestions
human review
```

before aggressive auto-creation/auto-reconciliation.

---

# 821. Agent Rule — Exact Automation Can Be Fast

Once deterministic rules prove safe:

```text
batch auto-match
batch approve/finalize
```

is allowed with audit.

---

# 822. Agent Rule — AI Is Advisory

AI can:

```text
suggest transaction type
suggest candidate match
summarize unknown lines
```

but must not become unreviewed financial authority.

---

# 823. No Separate Banking Subfeature Packs

Do **not** create separate architecture packs for:

```text
Bank Accounts
Cash Safes
Digital Wallets
Bank Statements
Bank Reconciliation
Cash Counts
Cash Position
Cash Forecast
Provider Settlement Reconciliation
```

They are all part of `25 Banking & Cash Management`.

A future `Advanced Treasury` domain may cover:

```text
Debt
Investments
Hedging
Cash Pools
Intercompany Liquidity
Treasury Risk
```

only when evidence justifies a separate bounded context.

---

# 824. Current Competitor Research Basis

This pack intentionally incorporates current official patterns from:

```text
Microsoft Dynamics 365 Finance
SAP S/4HANA Cash & Liquidity Management
Oracle Fusion Cash Management
NetSuite Banking
Odoo 19 Accounting/Banking
```

Research is used to validate business problems and mature patterns, not to copy competitor screens.

---

# 825. Dynamics Validation Summary

Current Dynamics official documentation confirms mature support for:

```text
legal-entity bank accounts
electronic statement import
ISO 20022 / BAI2 / MT940
matching rules/rule sets
reconciliation worksheet
bank transaction types
bank management workspace
```

These validate TODIR's account + statement + matching + reconciliation separation.

---

# 826. SAP Validation Summary

Current SAP Bank Relationship Management confirms mature enterprise needs around:

```text
bank account master
account groups
payment approvers
dual control
open/modify/close/reopen workflow
cash pooling/concentration
```

TODIR adopts the governance foundation without making it mandatory for SMEs.

---

# 827. Oracle Validation Summary

Current Oracle Cash Management confirms mature patterns around:

```text
bank accounts
cash balance dashboard
unreconciled items
cash at risk
5-day forecast
cash-in-transit
cash-to-GL reconciliation
```

TODIR generalizes the treasury-account model to bank/cash/wallet/settlement accounts.

---

# 828. Oracle Matching Validation Summary

Current Oracle reconciliation rules explicitly validate:

```text
One-to-One
One-to-Many
Many-to-One
Many-to-Many
```

as real reconciliation needs.

TODIR treats those cardinalities as architecture requirements.

---

# 829. NetSuite Validation Summary

Current NetSuite banking confirms useful patterns:

```text
bank feeds
system/user matching rules
suggested transaction matches
manual review
auto-create rules
audit/system notes
```

TODIR uses suggestions but preserves explainability and explicit finalization.

---

# 830. Odoo Validation Summary

Current Odoo banking documentation confirms the usability value of:

```text
simple reconciliation models
bank-fee handling
internal transfers between bank/cash accounts
```

TODIR keeps that simplicity while adding stronger treasury and source-of-truth boundaries.

---

# 831. Official Research References

Current official reference set used by this design includes:

```text
Microsoft:
https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/cash-bank-management
https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/advanced-bank-reconciliation-overview
https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/configure-advanced-bank-reconciliation
https://learn.microsoft.com/en-us/dynamics365/finance/cash-bank-management/bank-management-workspace

SAP:
https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/ac319d8fa4ea4624b40a58d23e3c4627/59d5a052c3ad224fe10000000a445394.html

Oracle:
https://docs.oracle.com/en/cloud/saas/financials/26b/facsf/how-you-implement-cash-management.html
https://docs.oracle.com/en/cloud/saas/financials/26b/faofc/overview-of-the-cash-management-dashboard.html
https://docs.oracle.com/en/cloud/saas/financials/26b/fappp/cash-to-general-ledger-reconciliation-report.html
https://docs.oracle.com/en/cloud/saas/financials/26d/fairp/reconciliation-matching-rules.html

NetSuite:
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/subsect_0330111247.html
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/article_32131451402.html
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N1551275.html
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_90174746588.html

Odoo:
https://www.odoo.com/documentation/19.0/applications/finance/accounting/bank/reconciliation_models.html
https://www.odoo.com/documentation/19.0/applications/finance/accounting/bank/internal_transfers.html
```

---

# 832. Architecture Closure

The module must remain:

```text
Simple outside.
Powerful inside.
One canonical TreasuryAccount master.
External truth preserved.
Book truth preserved.
Every difference explainable.
No duplicate Payment/GL/POS truth.
Legal-Entity safe.
Reconciliation-first.
Liquidity-aware.
```

A beautiful bank dashboard with ambiguous balances, duplicated account masters or silent differences is a failed implementation.

---

**END OF FILE — 25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
