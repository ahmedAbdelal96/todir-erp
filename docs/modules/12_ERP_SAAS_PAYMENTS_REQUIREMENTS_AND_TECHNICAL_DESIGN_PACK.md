# 12 — TODIR ERP SaaS Payments / Money Movement Requirements & Technical Design Pack

**File:** `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + MONEY MOVEMENT + PAYMENT PROVIDER + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Product Architect, Payments Lead, Accounting Lead, Treasury/Finance Reviewer, Security Lead, Backend/Frontend Leads, Integration Lead, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL** — duplicate, missing or incorrectly reversed payments directly affect cash/bank balances, AR/AP, customer/supplier balances, refunds, provider settlement and accounting.

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR Payments owns the **business record of money moving in, out or between operational money accounts**.

The core separation is:

```text
Payment
→ Did money move?

Subledger
→ Which obligation/credit did that money settle?

Ledger
→ What accounting entry represents the movement?

Bank Reconciliation
→ Does TODIR's recorded movement match the external bank statement?

Provider Adapter
→ What did the external payment provider report?
```

Non-negotiable rule:

> **Payment movement and invoice settlement are different concepts.**

A payment can be completed and still remain completely unapplied.

---

# 1. Central Business Question

Payments must answer:

> **Did money move, in which direction, for how much, in what currency, through which operational account/method/provider, with what external evidence, and has that movement been safely recorded exactly once?**

---

# 2. Payments Ownership

Payments owns:

```text
Payment
Payment Direction
Payment Method
Payment Account
Cashbox
Bank Payment Endpoint
Provider/Gateway Configuration
Provider Operation / Attempt
Payment References
Payment Fees
Refunds
Payment Reversals
Internal Transfers
Payment Links / Requests foundation
Provider Webhook Inbox
Provider Recovery / Status Sync
Payment Receipt data contract
```

---

# 3. Payments Does NOT Own

```text
Invoice                    → Invoicing
Open Item / Allocation     → Subledger
Customer/Supplier balance  → Subledger
Journal Entry              → Ledger
Chart of Accounts          → Ledger
Tax rules                  → Tax
Bank Statement             → Banking/Reconciliation
Bank reconciliation match  → Banking/Reconciliation
Pricing                    → Pricing
Collections                → Collections
```

---

# 4. The Four Truths

A payment-related workflow can have four independent truths:

```text
1. Business payment truth
2. Provider truth
3. Subledger application truth
4. Bank reconciliation truth
```

Example:

```text
Business Payment      = Completed
Provider              = Captured
Subledger Application = Partially Applied
Bank Reconciliation   = Not Yet Matched
```

This is valid.

---

# 5. Payment Is Not "Invoice Paid"

Forbidden:

```text
PaymentCompleted
→ invoice.paid = true
```

Invoice settlement state comes from `10 Subledger`.

---

# 6. Payment Can Exist Without an Invoice

Examples:

```text
Customer advance
Supplier advance
Security deposit
Refund
Internal transfer
Standalone receipt
Standalone disbursement
```

---

# 7. Payment Can Exist Before Allocation

Example:

```text
Incoming Customer Payment = 10,000 EGP
Applied = 0
Unapplied = 10,000
```

The money movement is still real.

---

# 8. Why This Separation Matters

Without separation, systems fail on:

- payments with no invoice reference;
- one payment covering many invoices;
- advances;
- overpayments;
- bank imports;
- incorrect allocations;
- provider retries;
- refunds;
- chargebacks.

---

# 9. Primary Payment Directions

```text
Incoming
Outgoing
InternalTransfer
```

---

# 10. Incoming Payment

Money enters an operational TODIR-controlled destination.

Examples:

```text
Customer payment
Cash receipt
Bank transfer received
Card/gateway collection
Supplier refund
Other approved receipt
```

---

# 11. Outgoing Payment

Money leaves an operational TODIR-controlled source.

Examples:

```text
Supplier payment
Customer refund
Cash disbursement
Bank payment
Expense payment
Other approved disbursement
```

---

# 12. Internal Transfer

Money moves between operational accounts of the same Legal Entity.

Examples:

```text
Bank A → Bank B
Cashbox → Bank
Bank → Cashbox
Gateway clearing → Bank
```

---

# 13. Internal Transfer Is Not Incoming + Outgoing Party Payment

It has no external customer/supplier obligation by default.

---

# 14. Legal Entity Boundary

A Payment belongs to exactly one Legal Entity.

---

# 15. No Cross-Entity Payment

One Payment cannot move money between two Legal Entities.

Intercompany settlement creates explicit documents/entries for each Entity.

---

# 16. Branch

Payment can carry Branch context for:

- cashbox;
- receipt point;
- operational reporting;
- dimensions.

Branch belongs Organization.

---

# 17. Party

Party is optional depending on payment type.

Required for normal:

```text
Customer receipt
Supplier payment
Customer refund
Supplier refund
```

Not required for:

```text
Internal bank transfer
Bank fee-only transaction
```

---

# 18. Party Ownership

Directory owns Party identity.

Payments stores PartyId + minimal transaction evidence/reference.

---

# 19. Payment Aggregate

Concept:

```text
Payment
 ├── Identity
 ├── Direction
 ├── Amount/Currency
 ├── Party?
 ├── Payment Method
 ├── Source/Destination Payment Account
 ├── Dates
 ├── References
 ├── Fees
 ├── Provider Context?
 ├── Source Context
 ├── Lifecycle State
 └── Reversal/Refund Relationships
```

---

# 20. Payment Identity

Internal:

```text
UUIDv7 PaymentId
```

Human reference:

```text
PAY-2026-000123
```

through Platform Numbering when desired.

---

# 21. Payment Number Is Not Provider Reference

Keep separate:

```text
TODIR Payment Number
Provider Transaction ID
Provider Event ID
Bank Reference
Cheque Number
Merchant Reference
Customer Reference
```

---

# 22. Payment Lifecycle

Canonical business lifecycle should stay compact:

```text
Draft
Pending
Completed
Failed
Cancelled
Reversed
```

Not every method uses every state.

---

# 23. Draft

Manual payment being prepared.

No final money-movement truth yet.

---

# 24. Pending

Operation initiated but financial completion is not yet confirmed.

Common for providers/bank workflows.

---

# 25. Completed

TODIR has sufficient authoritative evidence that the money movement occurred and internal financial effects can be recorded.

---

# 26. Failed

Operation did not complete.

No completed financial movement should be produced.

---

# 27. Cancelled

Pending/draft operation terminated before completion.

---

# 28. Reversed

A previously completed movement has been economically reversed through explicit reversal operation/evidence.

Original remains immutable.

---

# 29. Completed Payment Is Financial History

After `Completed`:

do not edit:

```text
Amount
Currency
Direction
Operational money account
Party
Provider transaction identity
Core dates
```

Corrections use reversal/refund/new payment.

---

# 30. Provider State Is Separate

A provider can expose states like:

```text
Created
RequiresAction
Authorized
Captured
Succeeded
Failed
Cancelled
Refunded
Disputed
```

Payments Core maps provider state into canonical business state.

---

# 31. Provider State Must Not Pollute Core Enum

Provider-specific values are stored separately.

---

# 32. Authorization Is Not Always Completion

A card authorization may reserve funds without final capture.

Provider adapter defines the event/state that represents confirmed financial completion.

---

# 33. Provider Financial Completion Policy

Each Provider Configuration declares:

```text
CompletionTrigger
RefundTrigger
ReversalTrigger
```

reviewed/tested per provider.

---

# 34. Payment Method

Canonical method/category:

```text
Cash
BankTransfer
Cheque
Card
PaymentGateway
Wallet
DirectDebit
Other
```

Country/provider extensions can refine display subtype.

---

# 35. Payment Method Is Not Payment Account

Example:

```text
Method = BankTransfer
Account = CIB Operating EGP
```

---

# 36. Operational Payment Account

Payments needs a business abstraction representing where money moves.

Recommended:

```text
PaymentAccount
```

---

# 37. Payment Account Types

Initial:

```text
Bank
Cashbox
ProviderClearing
Wallet
InternalClearing
```

---

# 38. Payment Account Is Not a GL Account

Payments refers to operational money endpoint.

Ledger maps its accounting semantic role/configuration to actual GL accounts.

---

# 39. Why Avoid Direct GL Account Ownership

Changing accounting mapping should not require changing payment business identity.

---

# 40. Payment Account Core Fields

Concept:

```text
PaymentAccount
{
    Id
    TenantId
    LegalEntityId

    Type
    Code
    Name
    CurrencyPolicy

    BranchId?
    BankDetails?
    ProviderConfigId?

    Status
    Version
}
```

---

# 41. Bank Payment Account

Represents operational bank account used in money movement.

Possible attributes:

```text
Bank name
Account holder
IBAN/account identifier
Currency
Country
Branch
External integration ID?
```

---

# 42. Sensitive Bank Data

Protect/mask based on permission.

Do not log full account numbers.

---

# 43. Cashbox

Operational cash location.

Examples:

```text
Main Cash
Cairo Branch Cash
POS Register 1
Petty Cash
```

---

# 44. Cashbox Scope

Can be Branch/user scoped.

---

# 45. Cashbox Does Not Mean Cash Closing Engine

Full cashier opening/count/closing is a later Cash Management/POS capability.

---

# 46. Provider Clearing Account

Operational representation of funds held/processed by gateway before bank payout.

This is distinct from bank account.

---

# 47. Why Provider Clearing Matters

A gateway can accept customer payment today and transfer net payout later.

Payment success and bank settlement are separate events.

---

# 48. Wallet

Operational digital wallet/provider balance where required.

---

# 49. Payment Account Currency Policy

Possible:

```text
SingleCurrency
MultiCurrency
```

Default simple operational account can be single-currency.

---

# 50. Payment Amount

Use FinBase `Money`.

---

# 51. No Float/Double

C# `decimal`, PostgreSQL `numeric`.

---

# 52. Incoming Manual Payment Example

```text
Direction = Incoming
Party = Customer ABC
Amount = 10,000 EGP
Method = BankTransfer
Destination = CIB-EGP
Reference = TRX123
```

---

# 53. Outgoing Supplier Payment Example

```text
Direction = Outgoing
Party = Supplier XYZ
Amount = 7,500 EGP
Method = BankTransfer
Source = QNB-EGP
Reference = ...
```

---

# 54. Cash Payment Example

```text
Direction = Incoming
Amount = 2,000 EGP
Method = Cash
Destination = Cairo Cashbox
```

---

# 55. Internal Transfer Example

```text
From = CIB EGP
To = QNB EGP
Amount = 50,000 EGP
```

---

# 56. Internal Transfer Across Currency

Example:

```text
From USD Account
To EGP Account
```

requires explicit FX execution/context.

Do not model as same-currency transfer.

---

# 57. Internal Transfer State

For instant internal book transfer:

```text
Completed
```

can be atomic.

For external inter-bank movement:

```text
Initiated
InTransit
Completed
```

may be needed at application layer.

---

# 58. Transfer In Transit

Accounting may require a semantic:

```text
FundsInTransit
```

Ledger maps account.

Payments does not choose GL AccountId.

---

# 59. Payment Dates

Keep distinct:

```text
PaymentDate
PostingDate
ValueDate?
ProviderEventTime?
CreatedAt
CompletedAt
```

---

# 60. Payment Date

Business/payment instrument date.

---

# 61. Posting Date

Ledger accounting date.

---

# 62. Value Date

Bank value date if known.

---

# 63. Provider Event Time

External event timestamp.

Not automatically trusted over our received/verified time.

---

# 64. Date Timezone

Use NodaTime.

Business dates use Legal Entity timezone/context.

External provider timestamps use `Instant` where appropriate.

---

# 65. References

Typed reference object:

```text
BankReference
ChequeNumber
ProviderReference
MerchantReference
CustomerReference
SupplierReference
RemittanceReference
```

---

# 66. Raw Reference

Preserve raw provider/bank reference.

---

# 67. Normalized Reference

Optional normalized value for duplicate matching/search.

Never overwrite raw evidence.

---

# 68. Source Context

Payment can originate from:

```text
Invoice
Customer Portal
Sales Order Advance
Supplier Payment Run
Manual Finance Entry
Gateway
Bank Import
API Integration
POS
```

---

# 69. Source Does Not Own Payment State

Upstream creates/requests Payment through Payments contracts.

---

# 70. Party Payment and Subledger

On completed party payment:

Payments calls Subledger to register corresponding party credit/debit Open Item.

---

# 71. Incoming Customer Payment

Typical Subledger representation:

```text
AR Credit Open Item
```

available for application.

---

# 72. Outgoing Supplier Payment

Typical Subledger representation:

```text
AP Debit Open Item
```

available for application.

---

# 73. Customer Advance

Payment completed before Invoice.

Subledger credit remains unapplied.

---

# 74. Supplier Advance

Symmetric.

---

# 75. Payment Allocation

Payments does NOT own application logic.

---

# 76. UX Can Combine Recording and Application

User can enter:

```text
Receive 10,000
Apply to INV-001
```

as one screen.

Internally:

```text
Payment
+
Subledger Application
```

remain separate domain operations.

---

# 77. "Receive and Apply" Atomic Path

For local/manual payment where business operation requires one command:

```text
Payment Completed
+ Subledger Payment Open Item
+ Open Item Application
+ Ledger Payment Fact
+ Settlement FX/write-off Fact if needed
+ Audit
+ Outbox
```

can use Transaction Coordinator.

---

# 78. Payment Without Application

Must remain first-class.

---

# 79. Payment With Partial Application

Example:

```text
Payment = 10,000
Applied = 7,000
Unapplied = 3,000
```

Payments read model may show summary from Subledger.

Authority is Subledger.

---

# 80. Payment Applied Amount Is Projection

Do not make Payments independently calculate canonical:

```text
AppliedAmount
```

from invoice links.

---

# 81. Payment Read Model Can Cache

```text
Applied
Unapplied
```

from Subledger events/queries for UX speed.

---

# 82. Payment + Invoice Relationship

No direct mutable:

```text
payment.invoice_id
```

as the universal settlement model.

One payment can settle many invoices.

---

# 83. Subledger Application Link

Canonical many-to-many relationship lives in `10`.

---

# 84. Ledger Integration

Payments emits semantic Accounting Facts.

---

# 85. Example Facts

```text
PaymentReceived
PaymentMade
PaymentReversed
RefundPaid
InternalTransferCompleted
PaymentFeeRecognized
ProviderSettlementReceived
```

Exact catalog reviewed with `09`.

---

# 86. Payment Fact Does Not Contain GL Account IDs

It contains:

```text
PaymentAccountRef
Party
Amount
Currency
Direction
Payment type
Fees
Dimensions
Source
```

Ledger determines accounts.

---

# 87. Incoming Customer Payment Accounting

Typical accounting meaning can be:

```text
Increase money account
Reduce/credit customer receivable position
```

or use explicit unapplied/clearing model depending accounting configuration.

Payments expresses semantic fact, not debit/credit IDs.

---

# 88. Outgoing Supplier Payment Accounting

Semantic:

```text
Reduce supplier payable position
Decrease money account
```

---

# 89. Advance Accounting

Country/accounting policy may map advance differently from ordinary receivable/payable.

Subledger + Ledger config decide.

---

# 90. Unapplied Receipt Accounting

TODIR must support accounting policy where unapplied receipt is:

- within AR control with party credit; or
- mapped through explicit unapplied-receipt/advance role.

Do not hardcode one accounting treatment globally.

---

# 91. Payment Fee

Fee is explicit component.

---

# 92. Fee Types

```text
BankFee
ProviderFee
CardProcessingFee
FXFee
TransferFee
OtherApprovedFee
```

---

# 93. Gross / Fee / Net

Example:

```text
Customer Payment Gross = 1,000
Provider Fee           = 20
Provider Net           = 980
```

Preserve all three concepts.

---

# 94. Fee Timing

Fee can be recognized:

- per payment;
- at provider payout;
- at bank settlement.

Provider adapter/policy decides.

---

# 95. No Double Fee Recognition

If provider fee is only known at payout, do not also estimate/post same fee at individual Payment unless clearly provisional and reversed.

---

# 96. Fee Tax

If fee tax applies:

Tax Engine/Country Pack calculates/validates tax treatment.

Payments does not hardcode tax.

---

# 97. Refund

Refund is an explicit outgoing movement related to an earlier incoming payment or credit position.

---

# 98. Refund Is Not Editing Original Payment

Original:

```text
Incoming = 10,000
```

Refund:

```text
Outgoing Refund = 2,000
```

Both remain.

---

# 99. Partial Refund

Supported.

---

# 100. Refund Relationship

Typed:

```text
RefundOfPaymentId
```

plus provider refund reference.

---

# 101. Refund Limit

Provider/business validation:

```text
Cumulative refunded amount
<= refundable amount
```

unless special adjustment flow.

---

# 102. Concurrent Refund Protection

Two refund commands cannot over-refund.

Transaction + provider idempotency.

---

# 103. Refund and Subledger

Refund may consume customer available credit or reopen/reclassify settlement context.

Subledger owns party application effect.

---

# 104. Refund and Ledger

Payments emits semantic Refund Fact.

---

# 105. Payment Reversal

Use when original Payment record itself was erroneous or economically reversed according to method/provider.

---

# 106. Refund vs Reversal

Do not treat as identical.

Conceptually:

```text
Refund
→ new outgoing return of funds

Reversal
→ negate/cancel original completed movement
```

Provider semantics vary.

---

# 107. Chargeback

Provider-enforced reversal/dispute.

Separate from merchant-initiated Refund.

---

# 108. Chargeback State

Can produce:

```text
DisputeOpened
ChargebackDebited
ChargebackWon
ChargebackLost
Fee
```

Provider-specific extension.

---

# 109. Chargeback MVP

Design extension points now.

Build full dispute management only for provider/market need.

---

# 110. Returned/Bounced Payment

Example:

```text
Cheque bounced
Direct debit returned
Bank transfer reversed
```

creates reversal/corrective flow.

---

# 111. Returned Payment and Subledger

If original payment was applied:

Subledger applications may need reversal/reopening.

This is coordinated explicitly.

---

# 112. No Silent Unpay

Do not set Invoice from `Paid` to `Unpaid` manually.

Reverse payment/application history.

---

# 113. Cheque

Cheque is a payment instrument with its own lifecycle.

---

# 114. Cheque Lifecycle Seam

Potential:

```text
Received
Deposited
Cleared
Bounced
Cancelled
```

---

# 115. Post-Dated Cheque

Architecture should permit:

```text
InstrumentDate > ReceivedDate
```

without counting it as cleared cash automatically.

---

# 116. Cheque Build Decision

Design now.

Build advanced cheque management according to launch-market customer need.

---

# 117. Card / Gateway Payments

Use provider adapter.

TODIR Core never assumes one provider.

---

# 118. Provider Adapter Contract

Concept:

```text
IPaymentProvider
{
    CreatePaymentRequest
    GetPaymentStatus
    Capture
    Cancel
    Refund
    VerifyWebhook
}
```

Capabilities exposed explicitly.

---

# 119. Provider Capability Matrix

Provider config declares support for:

```text
Authorize
Capture
PartialCapture
Refund
PartialRefund
Cancel
PaymentLink
SavedPaymentMethod
Payouts
Webhooks
Disputes
```

---

# 120. No Unsupported Call

Core does not call `Capture` if provider is immediate-capture only.

---

# 121. Provider Configuration

Per Legal Entity / merchant account:

```text
Provider
Environment
Merchant reference
Webhook endpoint identity
Supported currencies
Payment methods
Operational PaymentAccount
Secret reference
API version
Status
```

---

# 122. Provider Secrets

Never store API secret/credentials as normal plaintext DB configuration.

Use platform secret management.

---

# 123. Provider Public Configuration

Safe public keys/config IDs can be stored separately.

---

# 124. Provider API Version

Pin/version intentionally.

Do not auto-upgrade provider API semantics.

---

# 125. Payment Request / Intent

External provider payment often begins before money movement is completed.

Use separate concept:

```text
PaymentRequest / PaymentIntent
```

not a fake Completed Payment.

---

# 126. Provider Attempt

One logical Payment Request can have multiple attempts.

---

# 127. Provider Attempt Fields

```text
AttemptId
Provider
ProviderObjectId
State
Amount
Currency
CreatedAt
LastSyncedAt
FailureCode?
FailureMessage?
```

---

# 128. Completed Payment Creation

A canonical financial Payment is completed exactly once when verified provider state reaches configured financial completion.

---

# 129. Provider Idempotency Key

Every money-changing provider POST should use stable operation-level idempotency where provider supports it.

---

# 130. TODIR Idempotency Is Still Required

Do not rely solely on provider idempotency.

TODIR DB uniqueness/command idempotency protects internal state.

---

# 131. Idempotency Scope

Examples:

```text
Create Payment Request
Capture
Refund
Cancel
Provider event processing
Complete internal Payment
```

each has its own stable key.

---

# 132. Idempotency Key Reuse Protection

Same key with different command payload:

```text
PaymentIdempotencyConflict
```

---

# 133. Provider Webhook

Webhooks are external input, not trusted business command by default.

Pipeline:

```text
Receive
→ preserve raw body as needed
→ verify signature
→ idempotency/inbox
→ parse version
→ map provider event
→ transition provider state
→ complete/reverse Payment if warranted
→ outbox
```

---

# 134. Verify Before Processing

Invalid signature:

reject.

---

# 135. Never Trust Browser Redirect as Financial Truth

A user returning to `success_url` is not proof money moved.

---

# 136. Webhook Event Idempotency

Unique:

```text
Provider + MerchantConfig + ProviderEventId
```

where provider supplies stable Event ID.

---

# 137. Event Without Stable ID

Use provider-defined unique transaction/version or safely derived dedup key.

Provider adapter owns strategy.

---

# 138. Out-of-Order Provider Events

State machine must handle:

```text
capture arrives before authorization event
duplicate success
late failure from old attempt
```

---

# 139. Provider Event Ordering

Compare:

- provider object;
- attempt;
- event version/time;
- legal transition rules.

Never blindly apply last received event.

---

# 140. Webhook Replay

Verified duplicate returns success/no duplicate financial effect.

---

# 141. Provider Sync

Periodic/status-on-demand reconciliation checks provider state for unresolved attempts.

---

# 142. Provider Success + Internal Failure

Critical scenario:

```text
Provider captured money
TODIR DB operation failed
```

Recovery worker must query provider and complete internal Payment idempotently.

---

# 143. Internal Completed + Response Lost

Client retry returns same Payment.

---

# 144. Provider Refund Success + Internal Failure

Recovery must complete internal Refund exactly once.

---

# 145. Provider Timeout

Timeout is `Unknown/Pending`, not automatically `Failed`.

Query provider/idempotently retry according to operation semantics.

---

# 146. Provider Recovery State

Operations can be:

```text
PendingVerification
RequiresRecovery
```

at integration layer without corrupting Payment business state.

---

# 147. Provider Error Storage

Store normalized:

```text
ProviderCode
Category
Retryable
MessageSafeForUser
RequestId?
```

Do not expose raw secret/error internals to normal user.

---

# 148. Exponential Backoff

Retry transient provider/network failures with bounded backoff/jitter.

---

# 149. Do Not Retry Business Declines Blindly

Card declined is not the same as HTTP 503.

Provider adapter classifies.

---

# 150. Payment Link

Provider-backed request allowing payer to complete payment.

---

# 151. Payment Link Lifecycle

Application-level:

```text
Active
Completed
Expired
Cancelled
```

backed by Provider Request.

---

# 152. Payment Link Does Not Mean Payment Exists

Payment is created/completed only after verified success.

---

# 153. Invoice Payment Link

Invoicing can request a link referencing Invoice/Party/Amount.

Payments owns provider operation.

---

# 154. Link Amount Policy

Can be:

```text
Fixed
PartialAllowed
CustomerEnteredWithinLimits
```

provider/product policy.

---

# 155. Payment Link Expiry

Explicit.

---

# 156. Saved Payment Method

Future capability.

Store provider token/reference only.

---

# 157. Raw Card Data

TODIR application MUST NOT store raw PAN/CVV.

Use hosted/redirect/tokenized provider patterns.

---

# 158. PCI Scope Rule

Minimize cardholder-data environment aggressively.

Provider-hosted/tokenized integration preferred.

---

# 159. PCI DSS Baseline

As of this pack date, PCI Security Standards Council lists PCI DSS v4.0.1 as the current PCI DSS document in its official library.

Provider/card architecture must be reviewed against current PCI responsibilities before production.

---

# 160. CVV

Never store.

---

# 161. PAN

Avoid receiving/storing wherever possible.

If product later directly handles cardholder data, security/compliance architecture must be separately approved before implementation.

---

# 162. Payment Account Secrets vs Card Tokens

Do not mix:

- Provider secret credential.
- Customer payment method token.
- Bank identifier.

Each has separate security classification.

---

# 163. Wallet / Mobile Payment

Treat as provider/method capability.

---

# 164. Direct Debit

Requires mandate/reference lifecycle.

Design provider extension.

Build when required.

---

# 165. Mandate

Separate object/reference, not generic Payment note.

---

# 166. Cash Payment

No provider lifecycle.

Can complete in local transaction after authorization/validation.

---

# 167. Manual Bank Transfer

Can be recorded as completed when finance has evidence, or pending until bank confirmation depending company policy.

---

# 168. Bank Import Origin

Future bank statement import can propose/create Payment movement.

Banking/Reconciliation owns external statement line.

---

# 169. Bank Reconciliation Boundary

Payments:

```text
"We recorded payment PAY-1001."
```

Banking:

```text
"Statement line XYZ matches PAY-1001."
```

---

# 170. Reconciliation State Projection

Payment UI may show:

```text
Unmatched
Matched
Reconciled
```

but future Banking/Reconciliation is authority.



# 171. Bank Statement Is Not Payment

One statement line may:

- match one Payment;
- match many internal movements;
- include fee;
- include unknown transfer.

Do not make `bank_statement_line` a Payment row.

---

# 172. Provider Payout Is Not Customer Payment

Gateway can settle many customer Payments into one bank payout.

Need separate:

```text
ProviderPayout
```

or future Provider Settlement capability.

---

# 173. Why Provider Payout Matters

Example:

```text
Customer Payments Gross = 100,000
Provider Fees           = 2,500
Payout to Bank          = 97,500
```

If we treat payout as new customer payment, revenue/AR/cash accounting duplicates.

---

# 174. Provider Settlement / Payout

Design seam now:

```text
ProviderSettlement
{
  Provider
  Period/Batch
  Gross
  Fees
  Adjustments
  NetPayout
  Currency
  BankDestination
  ProviderReference
}
```

---

# 175. Provider Settlement Ownership

Can remain inside Payments provider integration initially.

Full reconciliation with bank statement belongs Banking/Treasury later.

---

# 176. Provider Balance

Do not infer provider receivable/clearing balance solely from one payout row.

Ledger/Provider settlement projections determine.

---

# 177. Internal Transfer Pair

One business Internal Transfer may create:

```text
Transfer Operation
From movement
To movement
```

but expose one user transaction.

---

# 178. Same-Currency Internal Transfer

Conceptual amount conservation:

```text
Amount Out == Amount In
```

except explicit bank fees.

---

# 179. Cross-Currency Transfer

Store:

```text
Source Money
Destination Money
FX Snapshot
Fee(s)
```

---

# 180. FX Ownership

FinBase owns FX records/resolution.

Payments owns actual movement snapshot.

---

# 181. Payment Movement FX vs Settlement FX

Example:

```text
Customer owes 1,000 USD
Customer pays 52,000 EGP
```

Payments records 52,000 EGP incoming.

Subledger resolves how much USD debt that settles and realized FX.

Do not conflate.

---

# 182. Provider FX

If provider processes:

```text
Charge USD
Settle EGP
```

Payments stores provider conversion evidence.

Ledger/provider clearing accounting consumes facts.

---

# 183. Provider FX Rate Is Historical Evidence

Do not recalculate later using FinBase current rate.

---

# 184. Multi-Currency Payment Account

If operational bank account is foreign currency, Payment stores its actual currency.

---

# 185. Base Currency Conversion

Ledger receives required FinBase/accounting snapshot.

Payments itself does not maintain base ledger balances.

---

# 186. Fees in Different Currency

Allowed only if explicit.

Store Fee Money separately.

---

# 187. Rounding

Use FinBase only.

---

# 188. No Implicit Conversion

Currency mismatch must be resolved by explicit FX context.

---

# 189. Dimensions

Payment may carry:

```text
Branch
Project
Cost Center
Department
Channel
```

for Ledger fact.

---

# 190. Dimensions Are Not Payment Accounts

Keep analytical context separate.

---

# 191. Payment Source Document

Typed source reference:

```text
Invoice
SalesOrder
PurchaseOrder
Expense
Payroll
RefundRequest
Manual
API
```

---

# 192. Source Reference Is Traceability

It does not create settlement automatically.

---

# 193. Payment Against Invoice UX

UI can show Invoice due amount and prefill:

```text
Party
Amount
Currency
Reference
```

but final allocation belongs Subledger.

---

# 194. Overpayment UX

Example:

```text
Invoice Due = 8,000
Payment     = 10,000
```

show:

```text
Apply to invoice = 8,000
Available credit = 2,000
```

---

# 195. Underpayment UX

Apply available amount and leave Open Item remaining.

---

# 196. Customer Advance UX

Simple action:

```text
Receive Advance
```

or Receive Payment without selecting invoice.

---

# 197. Supplier Advance UX

```text
Pay Advance
```

---

# 198. Payment Receipt

Payments provides structured receipt data.

Rendering capability creates printable/PDF receipt.

---

# 199. Payment Receipt Numbering

Can use distinct numbering series.

Not necessarily same as Payment internal number.

---

# 200. Receipt Legal Meaning

Country Pack may define receipt/e-receipt compliance separately.

Do not assume a payment receipt is a tax invoice.

---

# 201. Payment Proof Attachments

Examples:

```text
Bank transfer slip
Cheque scan
Provider receipt
Approval letter
```

Platform Files owns bytes.

---

# 202. Payment Notes

Separate:

```text
Internal Note
Payer-visible Note
Bank Remittance Text
```

---

# 203. Remittance Advice

Outgoing supplier payment may generate remittance advice.

Rendering/Delivery handles document transport.

---

# 204. Supplier Payment Run Boundary

Future AP Payment Run:

```text
select due supplier Open Items
→ propose payments
→ approvals
→ bank file/API
→ Payments created
```

Payment execution stays Payments.

---

# 205. Payment Proposal Is Not Completed Payment

Do not mark cash/bank moved at proposal approval.

---

# 206. Approval Workflow

Outgoing payment can support:

```text
Draft
Submitted
Approved
Execution Pending
Completed
```

through Workflow layer around Payment Request.

Canonical completed Payment remains money truth.

---

# 207. Small Business Mode

User may directly:

```text
Record Payment
```

without multi-step approval.

---

# 208. Enterprise Maker-Checker

Configurable:

```text
Preparer
Approver
Executor
```

---

# 209. Approval Does Not Mean Provider Success

Approved Payment Request may still fail bank/provider execution.

---

# 210. Payment Request Aggregate

Recommended for outgoing/provider workflows:

```text
PaymentRequest
```

editable/approvable before actual completed Payment.

---

# 211. Completed Payment vs Request

Do not use one mutable Payment row from "idea" to permanent history if workflow complexity obscures financial truth.

Recommended:

```text
PaymentRequest / Intent
→ attempts/provider execution
→ Completed Payment
```

for advanced external flows.

Manual immediate payments may create and complete in one command.

---

# 212. Request Lifecycle

Potential:

```text
Draft
Submitted
Approved
ExecutionPending
Succeeded
Failed
Cancelled
```

Application-layer workflow.

---

# 213. Completed Payment Lifecycle Remains Immutable

Separate from request workflow.

---

# 214. Why This Separation Helps

Provider retries can create many attempts without creating multiple financial Payments.

---

# 215. Payment Attempt

Every execution try is operational integration history.

---

# 216. Attempt Is Not Payment

Failed attempt:

no completed money movement.

---

# 217. Attempt Retry

New attempt or idempotent repeat depending provider operation.

---

# 218. Attempt Failure Reason

Typed:

```text
Declined
InsufficientFunds
AuthenticationRequired
ProviderUnavailable
InvalidAccount
TimeoutUnknown
Cancelled
Other
```

provider maps.

---

# 219. Error Safety

User-facing error should not expose:

- API keys;
- provider stack traces;
- card data.

---

# 220. Cash Authorization

Cash Payment completion can require user/cashbox permission only.

---

# 221. Cashbox Balance

Payments can provide operational movement projection.

Ledger remains accounting truth.

---

# 222. Cashbox Expected Balance

Potential derived:

```text
Opening operational count
+ completed incoming cash
- completed outgoing cash
```

Cash Management later owns counted/closing process.

---

# 223. Do Not Let Cashbox Projection Replace GL Cash Account

Different control purposes.

---

# 224. Cash Difference

Counted vs expected difference later creates explicit adjustment Fact.

---

# 225. Payment Account Status

```text
Active
Suspended
Archived
```

Archived account cannot accept new Payments.

History retained.

---

# 226. Payment Method Enablement

Per Legal Entity/account/channel:

```text
Cash enabled?
Bank transfer enabled?
Gateway X enabled?
```

---

# 227. Customer-Facing Method Availability

Can differ from finance manual methods.

---

# 228. Channel Context

Example:

```text
Back Office
POS
Customer Portal
E-Commerce
Mobile
```

Payment method policy can restrict.

---

# 229. Branch Context

Cashbox/payment account access scoped to Branch.

---

# 230. Payment Limits

Policy can define:

```text
Min/Max amount
Currency
Method
User role
Approval threshold
```

---

# 231. Cash Limit

Can require approval above configured cash amount.

---

# 232. Refund Limit

Cumulative refundable amount from provider/payment.

---

# 233. User Amount Limit

Optional risk policy.

---

# 234. Payment Method Fee

Can be customer-borne or company-borne depending business/legal policy.

Do not hardcode.

---

# 235. Customer-Borne Surcharge

Can have legal/tax restrictions.

Model as explicit charge, Tax-reviewed.

Not arbitrary provider fee pass-through.

---

# 236. Provider Fee vs Customer Surcharge

Different concepts.

---

# 237. Payment Tax Boundary

Payment itself usually does not recalculate invoice tax.

Payment-related service fees may have their own tax treatment.

---

# 238. Withholding Boundary

If payment triggers withholding tax:

Subledger/Tax withholding capability coordinates.

Payments provides payment context.

Do not put withholding formulas in Payments.

---

# 239. Settlement Discount Boundary

Early-payment discount belongs Subledger settlement context.

Payments only records actual movement.

---

# 240. Payment Allocation Policy

No allocation engine in Payments.

---

# 241. Bank Reference Duplicate Detection

Manual bank transfer can warn/block based on:

```text
LegalEntity
PaymentAccount
Reference
Amount
Date
```

depending policy.

---

# 242. Duplicate Detection Is Not Idempotency

Manual duplicate similarity warning differs from command/provider hard idempotency.

---

# 243. Provider Transaction Uniqueness

Hard unique:

```text
ProviderConfigId + ProviderTransactionId
```

for financial object type where stable.

---

# 244. Provider Refund Uniqueness

Separate ProviderRefundId unique.

---

# 245. Provider Event Uniqueness

Separate EventId unique.

---

# 246. Merchant Reference

TODIR-generated reference sent to provider.

Useful recovery key.

---

# 247. Provider Metadata

Always include stable TODIR identifiers where provider supports:

```text
Tenant-safe external correlation token
LegalEntity
PaymentRequestId
```

without leaking sensitive internal data.

---

# 248. Recovery by Merchant Reference

If response lost and webhook delayed, query provider using stable external reference if supported.

---

# 249. Payment Number Allocation

For manual completed payments:

allocate at completion.

For provider request:

PaymentRequest can have request reference; final Payment gets final number when completed.

---

# 250. No Number on Failed Payment Needed

Unless business audit wants attempt reference.

---

# 251. Provider Raw Payload Retention

If stored:

- encrypted/access-controlled;
- retention-limited;
- secrets/card data redacted;
- schema/version tagged.

---

# 252. Webhook Raw Body

Signature verification may require exact raw bytes.

Capture only as needed before parsing.

---

# 253. Webhook Endpoint Design

Per provider or routed by provider/config.

Never trust tenant/provider IDs solely from user-supplied URL parameters when signature/config lookup can establish context.

---

# 254. Webhook Tenant Resolution

Resolve merchant/provider account → Legal Entity securely.

Do not accept arbitrary TenantId from payload without validated mapping.

---

# 255. Webhook Inbox

Schema concept:

```text
provider_webhook_inbox
{
  Id
  ProviderConfigId
  ProviderEventId
  ReceivedAt
  SignatureVerified
  EventType
  ProcessingState
  RetryCount
  PayloadRef/RedactedPayload
  ErrorCode?
}
```

---

# 256. Webhook Processing State

```text
Received
Verified
Processed
IgnoredDuplicate
RetryPending
DeadLetter/ManualReview
Rejected
```

---

# 257. Unknown Event Type

Store/ack according to provider contract; do not crash global handler.

Alert if financially relevant unknown version.

---

# 258. Provider API Version Mismatch

Fail safe and alert.

---

# 259. Provider Adapter Contract Tests

Use recorded/synthetic fixtures.

No production credentials in CI.

---

# 260. Provider Sandbox

Use provider test/sandbox environment where available.

---

# 261. WireMock.Net

Use for deterministic provider timeout/retry/out-of-order/error integration tests.

---

# 262. Provider Certification Tests

Per adapter:

```text
success
decline
timeout
duplicate webhook
out-of-order webhook
refund
partial refund
signature invalid
provider 500
recovery
```

---

# 263. Security — PCI

Default integration strategy:

```text
Hosted Checkout / Redirect / Hosted Fields / Tokenization
```

to minimize handling of cardholder data.

---

# 264. Security — Secrets

Provider secrets stored in approved secret manager.

Rotation supported.

---

# 265. Security — Encryption

Sensitive bank/provider identifiers encrypted at rest where classification requires, in addition to platform/database protection.

---

# 266. Security — Masking

UI displays:

```text
****1234
```

where full account/reference not needed.

---

# 267. Security — Authorization

Payment operations are backend-enforced.

Hidden buttons are not security.

---

# 268. Permissions

Initial:

```text
payments.read

payments.incoming.create
payments.outgoing.create
payments.internal_transfer.create

payments.complete_manual
payments.cancel_pending
payments.reverse

payments.refund.create
payments.refund.approve

payments.account.read
payments.account.manage

payments.provider.read
payments.provider.manage
payments.provider.retry
payments.provider.reconcile

payments.cashbox.read
payments.cashbox.use
payments.cashbox.manage

payments.fee.override
payments.fx.override
payments.date.override

payments.audit.view
```

---

# 269. Provider Secret Permission

Even `payments.provider.manage` should not display secret plaintext.

Use replace/rotate operation.

---

# 270. Segregation of Duties

Optional:

```text
payment creator != approver
refund creator != approver
provider config editor != payout approver
```

---

# 271. Step-Up Authentication

Recommended option for:

- large outgoing payment;
- refund;
- provider config change;
- bank account change;
- reversal.

---

# 272. Payment Account Change Audit

Mandatory.

---

# 273. Bank Details Change Risk

Changing payout bank details is high-risk.

Require:

- elevated permission;
- audit;
- possible dual approval/step-up.

---

# 274. Payment Fraud Considerations

TODIR core should support risk hooks but not invent a fraud engine Day 1.

---

# 275. Risk Hook

Potential:

```text
BeforePaymentExecution
BeforeRefund
BeforePayoutAccountChange
```

---

# 276. Fraud/Anomaly Signals

Future:

- unusual amount;
- new beneficiary;
- new bank account;
- velocity;
- repeated refunds.

---

# 277. AI Restriction

AI can:

- explain Payment history;
- suggest invoice match;
- summarize provider failure;
- suggest duplicate.

AI cannot:

- execute outgoing payment;
- approve refund;
- change bank account;
- reveal secrets;
- mark provider success.

without explicit authorized Application Command/workflow.

---

# 278. Audit

Mandatory for:

```text
Manual Payment completion
Outgoing Payment approval/execution
Refund
Reversal
Payment Account change
Provider config change
Manual FX override
Fee override
Manual reference override
Recovery/manual provider state intervention
```

---

# 279. Payment Record Is Primary Evidence

Do not duplicate all immutable Payment fields into generic audit event.

Audit the action/override/config changes.

---

# 280. Payment Correction

Never overwrite completed amount.

---

# 281. Wrong Manual Payment

If money did not actually move and user accidentally recorded it completed:

create explicit reversal/cancellation accounting workflow with reason.

---

# 282. Wrong Reference Only

Non-financial reference correction may be allowed under controlled metadata amendment if it does not alter external/legal evidence.

Audit.

---

# 283. Wrong Party

If completed financial Payment tied to wrong Party:

do not silently edit Party after Subledger/Ledger effects.

Use correction/reclassification workflow.

---

# 284. Payment Reclassification

Potential future command:

```text
ReclassifyPartyPayment
```

implemented through Subledger/Ledger correcting facts.

Not a row edit.

---

# 285. Wrong Payment Account

Likewise requires reversal/correction, not direct edit after completion.

---

# 286. Provider Dispute

Original Payment remains.

Dispute/chargeback creates separate operational/financial history.

---

# 287. Payment Search

Search by:

```text
Payment Number
Party
Amount
Date
Reference
Provider Transaction
Bank Reference
Payment Account
Method
Status
Source Document
```

---

# 288. Payment List Filters

```text
Legal Entity
Branch
Direction
Party
Method
Payment Account
Currency
Date
Status
Provider
Applied/Unapplied
Reconciliation status
```

---

# 289. Payment List Projection

Recommended:

```text
payment_list_projection
```

with:

```text
Number
Party display
Direction
Amount/Currency
Method
Payment account display
Completed date
Business state
Provider summary
Subledger applied summary
Reconciliation summary
```

---

# 290. Projection Authorities

```text
Payment State      → Payments
Applied Summary    → Subledger
Reconciliation     → Banking
Provider Summary   → Provider Integration
```

---

# 291. No N+1

Payment list must not call Subledger/Banking per row.

---

# 292. Detail Screen

Role-aware sections:

```text
Overview
Money Movement
Application
Provider
Accounting
Reconciliation
Fees
Refunds/Reversals
Attachments
Audit
```

---

# 293. Small User UX

Customer payment:

```text
Customer
Amount
Method
Bank/Cash
Reference
[Receive Payment]
```

Optionally:

```text
Apply to invoice
```

without exposing Subledger terminology.

---

# 294. Finance UX

Can inspect:

```text
Completed movement
Available/unapplied
Applications
Provider refs
Ledger Entry
Bank match
Fees
FX
```

---

# 295. Provider UX

Normal user should not see raw webhook event JSON.

Admin/support-only diagnostic.

---

# 296. Failed Provider Payment UX

Show clear:

```text
Failed / action required / retry possible
```

with safe provider message.

---

# 297. Pending/Unknown UX

Do not show "Failed" just because request timed out.

Show:

```text
Checking payment status...
```

---

# 298. Duplicate UX

If same provider transaction already recorded:

return existing Payment, not scary duplicate error for idempotent retry.

---

# 299. Manual Duplicate Warning UX

Show candidate existing payments for review.

---

# 300. Internal Transfer UX

Simple:

```text
From
To
Amount
Date
Reference
Fee?
[Transfer]
```

---

# 301. Cross-Currency Transfer UX

Finance-only advanced fields:

```text
Source amount
Destination amount
Rate
Fee
```

server validation.

---

# 302. Cashbox UX

Only accessible cashboxes shown.

---

# 303. Provider Payment Link UX

Invoice action:

```text
Create Payment Link
```

shows:

```text
Amount
Expiry
Status
Copy/Send
```

---

# 304. Refund UX

Show:

```text
Original Payment
Refundable
Already Refunded
Refund Amount
Reason
Provider Fee implications
```

---

# 305. Refund Reason

Typed reason + optional text.

---

# 306. Provider Refund Preview

Before provider call:

validate internal and provider constraints.

---

# 307. Provider Refund Execution

Do not hold DB transaction during external call.

Workflow:

```text
Create Refund Request
commit
call Provider idempotently
receive/verify result
complete internal Refund transaction
```

---

# 308. Manual Cash Refund

Can complete locally with permission.

---

# 309. Payment Performance

Manual/local completed Payment path should be fast.

Initial target:

```text
p95 <= 150–200ms
```

for core internal DB operation under realistic load.

Measure.

---

# 310. Payment + Apply Performance

Interactive typical one/few invoice application:

initial:

```text
p95 <= 250–350ms
```

excluding external provider, subject to Subledger/Ledger targets.

---

# 311. Provider Latency

Tracked separately:

```text
provider_api_duration
provider_webhook_delay
internal_completion_duration
```

---

# 312. No External Network in Core Financial Transaction

Critical.

---

# 313. DB Connection Pool

Provider wait never occupies long PostgreSQL transaction/connection unnecessarily.

---

# 314. Provider Worker Concurrency

Bound per provider/config to avoid rate limits.

---

# 315. Provider Rate Limits

Adapter implements provider-specific backoff/throttling.

---

# 316. Webhook Throughput

Webhook HTTP endpoint:

- verify quickly;
- Inbox persist;
- acknowledge according to provider requirements;
- process heavy work asynchronously if safe.

---

# 317. Financial Completion from Webhook

Inbox processing must be idempotent and transactional internally.

---

# 318. Webhook Response

Do not wait for downstream email/read model work.

---

# 319. Provider Cache

Avoid caching financial transaction status as authoritative.

Provider config metadata can cache.

---

# 320. Payment Account Cache

Active PaymentAccount metadata cacheable by version.

---

# 321. Payment Method Cache

Safe config cache.

---

# 322. No Redis Day 1 Requirement

In-memory/versioned caches + PostgreSQL first.

---

# 323. Schema

Recommended:

```text
finance_payments
```

---

# 324. Core Tables

```text
payment
payment_account
payment_method_config

payment_request
payment_attempt

payment_fee

payment_refund
payment_reversal
payment_relationship

internal_transfer

provider_config
provider_operation
provider_webhook_inbox

payment_attachment_ref

payment_list_projection
```

Can simplify after domain implementation review.

---

# 325. `payment`

Concept:

```text
id
tenant_id
legal_entity_id
branch_id?

payment_number?

direction
payment_type

party_id?

amount
currency_code

payment_method
payment_account_id
counter_account_ref? // second operational account for internal transfer handled separately

payment_date
posting_date
value_date?

business_state

source_module?
source_ref?

provider_config_id?
provider_transaction_id?

completed_at?
completed_by?

reversal_of_payment_id?

version
created_at
```

---

# 326. `payment_account`

```text
id
tenant_id
legal_entity_id
branch_id?

code
name
type
currency_policy
currency_code?

masked_bank_identifier?
bank_metadata?
provider_config_id?

status
version
```

---

# 327. Bank Secret/Data Split

Public/operational bank metadata in DB.

Sensitive credentials in secret manager.

---

# 328. `payment_request`

```text
id
tenant_id
legal_entity_id

direction
party_id?
amount
currency

method
payment_account/context

workflow_state
source_ref
idempotency_key

created_by
approved_by?
created_at
```

---

# 329. `payment_attempt`

```text
id
payment_request_id

provider_config_id?
provider_operation_type

provider_object_id?
merchant_reference

state
failure_category?
provider_request_id?

created_at
updated_at
```

---

# 330. `payment_fee`

```text
payment_id / provider_settlement_ref
fee_type
amount
currency
tax_context_ref?
provider_fee_ref?
```

---

# 331. `payment_refund`

```text
id
original_payment_id
amount
currency
reason
provider_ref?
state
idempotency_key
requested_by
completed_at?
```

---

# 332. Refund Is Its Own Aggregate/Financial Operation

Do not store only `refunded_amount` on Payment.

That can be derived/cached.

---

# 333. `provider_config`

```text
id
tenant_id
legal_entity_id
provider_code
merchant_reference
environment
api_version
secret_ref
supported_capabilities
payment_account_id
status
version
```

---

# 334. No Secret Value Column

`secret_ref` only.

---

# 335. `provider_operation`

Tracks:

```text
Create
Authorize
Capture
Refund
Cancel
StatusSync
```

idempotency and result metadata.

---

# 336. `provider_webhook_inbox`

As defined above.

---

# 337. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 338. Provider Webhook Context and RLS

Webhook processor establishes validated tenant context after resolving ProviderConfig from trusted merchant mapping.

---

# 339. Runtime DB Role

No BYPASSRLS/ownership.

---

# 340. Composite Tenant FKs

Payment → PaymentAccount, ProviderConfig, Refund references tenant-aware.

---

# 341. Entity Scope

Application authorization ensures actor Legal Entity/Branch access.

---

# 342. Unique Constraints

Examples:

```text
payment number scope
provider_config + provider_transaction_id
provider_config + provider_event_id
idempotency key scope
refund provider id
merchant reference where guaranteed unique
```

---

# 343. Provider IDs Need Type Scope

Provider can reuse ID namespaces across object types.

Unique key includes object/operation type where required.

---

# 344. Manual Bank Reference Index

Non-unique searchable by default.

Optional unique policy, because real banks can duplicate formats.

---

# 345. Payment Search Indexes

Likely:

```text
tenant + entity + payment_date desc
tenant + entity + party + payment_date desc
tenant + entity + status + payment_date
tenant + entity + payment_account + payment_date
tenant + entity + provider_transaction_id
tenant + entity + normalized_reference
```

---

# 346. Provider Inbox Indexes

```text
provider_config + event_id UNIQUE
processing_state + received_at
```

---

# 347. Payment Request Indexes

```text
workflow_state
source_ref
idempotency_key
```

---

# 348. Payment Projection Indexes

Applied/reconciliation/provider filters.

---

# 349. Partitioning

Do not partition Day 1.

Provider webhook history/payment history can grow; keep partition-ready if measured.

---

# 350. Retention

Financial Payment rows retained according to legal/company policy.

Raw provider payload retention can be shorter/security-driven.

---

# 351. Immutable Completed Payment

Application-level guard.

Consider DB protection against UPDATE of core completed fields.

---

# 352. Refund/Reversal Link Mutation

Only state/link metadata changes; original amount remains immutable.

---

# 353. Payment DbContext

Owns only Payments schema.

No Subledger/Ledger entities.

---

# 354. Cross-Module Contracts

Use:

```text
Erp.Finance.Payments.Contracts
```

---

# 355. Core Contracts

```text
RecordIncomingPayment
RecordOutgoingPayment
CreatePaymentRequest
CompletePayment
ReversePayment
CreateRefund
CreateInternalTransfer

PaymentCompletedReference
PaymentMovementSnapshot
PaymentProviderContext
PaymentAccountReference
```

---

# 356. Subledger Contracts Consumed

```text
RegisterPartyPaymentCredit
ApplyOpenItems
ReverseApplicationsForPayment
```

through Subledger Contracts.

---

# 357. Ledger Contracts Consumed

```text
PaymentReceived Fact
PaymentMade Fact
Refund Fact
Transfer Fact
Fee Fact
```

through Ledger Contracts.

---

# 358. Tax Contract Consumed

Only for Payment-related fee/withholding/country treatment when explicitly needed.

---

# 359. Invoicing Contract

Invoicing can request payment link/payment action; no direct Payments table access.

---

# 360. Banking Contract Future

Banking/Reconciliation can query:

```text
Payment movements
Payment accounts
References
Amounts/Dates
```

through read Contracts/API.



# 361. API Base

```text
/api/v1/payments
```

---

# 362. Manual Incoming Payment APIs

```text
POST /incoming
GET  /{id}
GET  /
POST /{id}/complete
POST /{id}/reverse
```

Exact route conventions follow platform standards.

---

# 363. Outgoing Payment APIs

```text
POST /outgoing
POST /{id}/submit
POST /{id}/approve
POST /{id}/execute
POST /{id}/reverse
```

only where approval workflow enabled.

---

# 364. Internal Transfer APIs

```text
POST /internal-transfers
GET  /internal-transfers/{id}
```

---

# 365. Refund APIs

```text
POST /{paymentId}/refunds
GET  /refunds/{id}
POST /refunds/{id}/approve
```

---

# 366. Payment Link APIs

```text
POST /payment-links
GET  /payment-links/{id}
POST /payment-links/{id}/cancel
```

---

# 367. Provider Webhook Endpoint

Provider-specific:

```text
POST /webhooks/{provider}
```

or routed endpoint with secure configuration resolution.

No normal user auth token required; provider signature verification is mandatory.

---

# 368. Provider Admin APIs

```text
GET  /providers
POST /provider-configs
PUT  /provider-configs/{id}
POST /provider-configs/{id}/rotate-secret
POST /provider-configs/{id}/test-connection
```

Secret value never returned.

---

# 369. Payment Account APIs

```text
GET  /accounts
POST /accounts
PUT  /accounts/{id}
POST /accounts/{id}/archive
```

---

# 370. No Public `SetCompleted`

Completion only via trusted business/provider command.

---

# 371. No Public `SetProviderState`

Provider adapter/inbox only.

---

# 372. No Public `SetAppliedAmount`

Subledger authority.

---

# 373. No Public `SetReconciled`

Banking authority.

---

# 374. Command Idempotency Header

Financial POSTs require supported command idempotency strategy.

---

# 375. Idempotency Persistence

Store:

```text
Scope
Key
Request Hash
Result Reference
CreatedAt
```

with appropriate retention.

---

# 376. Same Key / Same Payload

Return same result.

---

# 377. Same Key / Different Payload

Reject.

---

# 378. Error Codes

Stable:

```text
payments.payment_not_found
payments.payment_already_completed
payments.payment_already_reversed
payments.payment_not_reversible

payments.account_not_found
payments.account_inactive
payments.account_currency_mismatch

payments.party_required
payments.party_scope_invalid

payments.amount_invalid
payments.currency_mismatch
payments.fx_rate_missing

payments.duplicate_reference_warning
payments.idempotency_conflict

payments.provider_unavailable
payments.provider_declined
payments.provider_timeout_unknown
payments.provider_signature_invalid
payments.provider_event_duplicate
payments.provider_event_out_of_order
payments.provider_state_conflict
payments.provider_recovery_required

payments.refund_not_allowed
payments.refund_exceeds_available
payments.refund_already_processed

payments.transfer_invalid
payments.same_source_destination

payments.permission_denied
payments.period_locked
```

---

# 379. Safe Provider Error Message

User-facing:

> "تعذر إتمام الدفع من مزود الخدمة. لم يتم تسجيل حركة مالية ناجحة."

not:

```text
NullReferenceException at ProviderAdapter...
```

---

# 380. Timeout Message

> "لم نتمكن من تأكيد حالة العملية بعد. TODIR يتحقق من مزود الدفع قبل اعتبارها فاشلة."

---

# 381. Duplicate Provider Event

Normal successful no-op.

---

# 382. Provider Recovery Alert

Support/Finance dashboard can show:

```text
Provider confirms success
Internal recording pending recovery
```

until repaired idempotently.

---

# 383. Integration Events

```text
PaymentCompletedV1
PaymentReversedV1
PaymentRefundedV1
PaymentFailedV1

InternalTransferCompletedV1

PaymentAccountChangedV1

ProviderPaymentNeedsReviewV1
ProviderSettlementImportedV1
```

Only meaningful events.

---

# 384. Event Payload

Small:

```text
PaymentId
LegalEntityId
Direction
Amount/Currency
PartyId?
SourceRef?
Version
```

Do not publish sensitive bank/provider payload.

---

# 385. Outbox

All durable platform events through Transactional Outbox.

---

# 386. Provider Webhooks Use Inbox

Exactly-once effect via at-least-once processing + idempotent Inbox/DB constraints.

---

# 387. Inbox + Outbox Combined Flow

```text
Webhook Inbox
→ verified transition
→ Payment Completion
→ Subledger
→ Ledger
→ Outbox
→ commit
```

one internal transaction where feasible.

---

# 388. No Distributed Transaction with Provider

Provider is external.

Use idempotent state machine/recovery.

---

# 389. Transaction Coordinator — Manual Payment

Potential atomic flow:

```text
Payment
+ Subledger party item
+ Ledger Fact/Entry
+ Audit
+ Outbox
```

---

# 390. Transaction Coordinator — Provider Success

After external success is verified:

same internal atomic flow.

---

# 391. Transaction Coordinator — Refund

Internal refund completion + Subledger/Ledger effects atomically after provider success confirmation.

---

# 392. Payment Reversal Atomicity

```text
Reversal record
+ Subledger application corrections
+ Ledger reversal/corrective fact
+ Outbox
```

where required.

---

# 393. Source Payment Applications

If payment is applied, reversal may first require reversing/unapplying affected Subledger Applications.

---

# 394. Reversal Policy

Cannot reverse a Payment with downstream settlements blindly.

Return actionable blocker:

```text
Reverse/unapply applications first
```

or coordinate one controlled command.

---

# 395. Controlled Cascade Reversal

Finance command can:

```text
Reverse Payment and related applications
```

transactionally if policy permits.

---

# 396. Current ERP Pattern Validation — ERPNext

ERPNext's current Payment Entry model records incoming/outgoing/internal-transfer money movement, supports party payments, advances, partial payments and multi-invoice references.

Its Payment Reconciliation model explicitly allows an already-recorded payment/credit to be allocated later to outstanding invoices without creating a second bank movement.

TODIR adopts the useful business distinction but separates Payment movement and Open Item application into explicit domain ownership.

---

# 397. Current ERP Pattern Validation — Business Central

Business Central's Payment Reconciliation Journal works from bank/payment-service transactions and can match/apply them to open customer/vendor entries.

This validates the need for separate:

```text
external bank/provider movement
payment record
open-item application
reconciliation
```

rather than a single invoice-paid flag.

---

# 398. Current Provider Pattern Validation — Idempotency

Stripe's current API documentation explicitly supports idempotency keys for safe retries of create/update operations so a repeated request does not accidentally perform the operation twice.

TODIR does not depend on Stripe specifically, but this validates provider-side idempotency as a standard integration capability.

TODIR still enforces its own internal idempotency.

---

# 399. Security Validation — PCI DSS

PCI Security Standards Council's official document library currently lists:

```text
PCI DSS v4.0.1
```

Payment-card implementation must minimize cardholder-data handling and be reviewed against current applicable PCI requirements before production.

---

# 400. Test Strategy — Manual Incoming Payment

```text
create
complete
Subledger item
Ledger posting
Outbox
```

---

# 401. Test — Manual Outgoing Payment

Same, correct direction and account scope.

---

# 402. Test — Internal Transfer

Source decreases / destination increases semantically.

No Party/Open Item accidentally created.

---

# 403. Test — Customer Advance

Completed incoming payment creates unapplied Subledger credit.

---

# 404. Test — Supplier Advance

Equivalent AP case.

---

# 405. Test — Receive and Apply

One user action produces one Payment and correct Subledger applications.

---

# 406. Test — Payment Without Apply

Remains fully unapplied.

---

# 407. Test — Overpayment

Only invoice amount applied; residual remains credit.

---

# 408. Test — Double Click

One Payment.

---

# 409. Test — Same Idempotency Key Different Amount

Reject.

---

# 410. Test — Network Response Lost

Retry returns original Payment.

---

# 411. Test — Ledger Failure

Manual completion transaction rolls back Payment completion/Subledger effects.

---

# 412. Test — Subledger Failure

Same.

---

# 413. Test — Broker Down

Completed internal financial transaction remains valid; Outbox retries.

---

# 414. Test — Provider Success

Verified event creates exactly one completed Payment.

---

# 415. Test — Duplicate Webhook

No duplicate Payment.

---

# 416. Test — Invalid Webhook Signature

No financial state change.

---

# 417. Test — Out-of-Order Webhooks

State machine remains valid.

---

# 418. Test — Browser Success Without Provider Confirmation

No completed Payment.

---

# 419. Test — Provider Timeout

State remains Pending/Unknown; no false failure/success.

---

# 420. Test — Provider Success + DB Failure

Recovery creates one Payment later.

---

# 421. Test — Provider Idempotent Retry

Provider receives same operation key; internal operation still one.

---

# 422. Test — Provider Decline

No financial Payment completion.

---

# 423. Test — Full Refund

Original Payment remains; Refund financial history created.

---

# 424. Test — Partial Refund

Cumulative refund tracking exact.

---

# 425. Test — Concurrent Refunds

Cannot exceed refundable amount.

---

# 426. Test — Refund Provider Success + Internal Failure

Recovery exact once.

---

# 427. Test — Payment Reversal

Original remains, correction history present.

---

# 428. Test — Applied Payment Reversal

Subledger applications reversed correctly or command blocked per policy.

---

# 429. Test — Chargeback

Provider extension produces explicit corrective history, no original edit.

---

# 430. Test — Payment Fee

Gross/fee/net preserved.

Ledger fact semantics correct.

---

# 431. Test — Fee at Payout

Not duplicated at Payment level.

---

# 432. Test — Same Currency Transfer

Exact amount conservation excluding explicit fee.

---

# 433. Test — Cross-Currency Transfer

Source/destination amount + FX snapshot exact.

---

# 434. Test — Currency Mismatch Without FX

Blocked.

---

# 435. Test — Cashbox Authorization

Unauthorized Branch/user blocked.

---

# 436. Test — Bank Account Scope

Entity/role restrictions enforced.

---

# 437. Test — Archived Payment Account

Cannot create new Payment.

---

# 438. Test — Supplier Payment

Correct AP Subledger credit/debit semantics and Ledger Fact.

---

# 439. Test — Payment Allocation Projection

Payments read model updates from Subledger; authority remains Subledger.

---

# 440. Test — Bank Reconciliation Projection

Does not let Payments mutate reconciliation truth.

---

# 441. Test — PCI Data

API/log/database tests ensure raw CVV/PAN not persisted by supported hosted/tokenized integration.

---

# 442. Test — Secret Redaction

Provider secret never returned/logged.

---

# 443. Test — Webhook Tenant Isolation

Merchant configuration resolves correct Tenant/Entity.

Forged Tenant reference cannot redirect event.

---

# 444. Test — RLS

Tenant A cannot:

- read B Payments;
- use B PaymentAccount;
- use B ProviderConfig;
- refund B Payment.

---

# 445. Test — Entity Isolation

Entity A user cannot pay from Entity B bank/cash account.

---

# 446. Test — Provider Config Rotation

New secret works; old invalidated according to provider workflow; no secret display.

---

# 447. Test — Provider API Version

Unexpected incompatible payload fails safely.

---

# 448. Test — Large Payment Search Dataset

Millions of historical payments; current list/filter remains indexed.

---

# 449. Test — Webhook Burst

High duplicate/event burst does not duplicate financial movement.

---

# 450. Test — Provider Rate Limit

Workers back off without losing operation state.

---

# 451. Property Test — Completed Payment Immutability

No allowed command changes core completed amount/currency/direction.

---

# 452. Property Test — Idempotency

N identical retries → one financial effect.

---

# 453. Property Test — Refund Conservation

```text
Net refundable
=
Original refundable
-
Completed refunds/reversals according to policy
```

never below zero.

---

# 454. Property Test — Internal Transfer

For same-currency no-fee case:

```text
source movement amount == destination movement amount
```

---

# 455. Architecture Test — No Invoice Paid Mutation

Payments project cannot update Invoicing tables.

---

# 456. Architecture Test — No Subledger Allocation Logic

Payments can call Subledger contract only.

---

# 457. Architecture Test — No Journal Entry Construction

Payments cannot reference Ledger implementation entities.

---

# 458. Architecture Test — No Provider Secret in Entity DTO

Static/config review.

---

# 459. Architecture Test — No Raw Card Fields

Ban fields:

```text
CVV
FullPAN
CardNumber
```

in Payments persistence/domain contracts.

---

# 460. Architecture Test — No External Provider Call Inside Transaction

Integration test/instrumentation review.

---

# 461. Red-Team — `payment.invoice_id` Universal Model

Reject.

---

# 462. Red-Team — Payment Sets Invoice Paid

Reject.

---

# 463. Red-Team — Provider Webhook Creates Duplicate Payment

Reject.

---

# 464. Red-Team — Success URL Marks Payment Completed

Reject.

---

# 465. Red-Team — Timeout Marks Failed Immediately

Reject.

---

# 466. Red-Team — Completed Payment Amount Edited

Reject.

---

# 467. Red-Team — Refund Changes Original Amount

Reject.

---

# 468. Red-Team — Payment Reversal Deletes Original

Reject.

---

# 469. Red-Team — Provider API Key Stored Plaintext

Reject.

---

# 470. Red-Team — CVV Stored

Reject.

---

# 471. Red-Team — Raw PAN Logged

Reject.

---

# 472. Red-Team — Bank Reconciliation Inside Payments Core

Reject.

---

# 473. Red-Team — Bank Statement Line Equals Payment

Reject.

---

# 474. Red-Team — Provider Payout Equals Customer Payment

Reject.

---

# 475. Red-Team — Fee Silently Reduces Customer Payment

Reject.

Store Gross/Fee/Net semantics.

---

# 476. Red-Team — One Payment Across Legal Entities

Reject.

---

# 477. Red-Team — Cross-Currency Implicit Conversion

Reject.

---

# 478. Red-Team — Missing Provider Event Retried Forever

Use bounded retry + manual review/dead-letter/recovery process.

---

# 479. Red-Team — Unknown Provider State Treated as Success

Reject.

---

# 480. Product Acceptance — Simple Customer Receipt

User:

```text
Customer
10,000 EGP
Bank
Reference
Receive
```

Payment completed, accounting/subledger correct.

---

# 481. Product Acceptance — Receive and Apply

User applies to one Invoice from same screen.

No duplicated domain logic.

---

# 482. Product Acceptance — Advance

Money received without Invoice.

Visible as available credit.

---

# 483. Product Acceptance — Supplier Payment

Finance pays supplier and sees remaining unapplied/application summary.

---

# 484. Product Acceptance — Cash

Branch employee records cash only in authorized Cashbox.

---

# 485. Product Acceptance — Internal Transfer

Finance moves money Bank A → Bank B without creating Party balance.

---

# 486. Product Acceptance — Gateway

Customer pays online.

Duplicate webhooks/retries still produce one Payment.

---

# 487. Product Acceptance — Payment Link

Invoice can create provider link without Invoicing owning provider logic.

---

# 488. Product Acceptance — Refund

Original Payment stays visible; refund linked and traceable.

---

# 489. Product Acceptance — Provider Failure

No false money movement.

---

# 490. Product Acceptance — Provider Unknown Timeout

UI shows pending verification, not duplicate retry chaos.

---

# 491. Product Acceptance — Fee

Finance can explain gross, fee, net and accounting.

---

# 492. Product Acceptance — Cross-Currency Payment

Actual movement and Subledger settlement FX remain separately explainable.

---

# 493. Product Acceptance — Bank Reconciliation

Payment can be Completed while Reconciliation remains Unmatched.

---

# 494. Product Acceptance — Accounting

Finance can open Ledger entry and see "Why this account?"

---

# 495. Product Acceptance — Audit

Can answer:

```text
Who recorded/approved/reversed?
What provider reference?
Which source document?
What was applied?
Which refund/reversal followed?
```

---

# 496. Implementation Order

```text
PM00 ADRs + Domain Boundaries
PM01 Schema + RLS
PM02 Payment Account Model
PM03 Payment Method / Policy
PM04 Manual Incoming Payment
PM05 Manual Outgoing Payment
PM06 Subledger Party Item Integration
PM07 Ledger Accounting Fact Integration
PM08 Idempotency Framework
PM09 Internal Transfers
PM10 Fees
PM11 Refunds
PM12 Payment Reversal
PM13 Multi-Currency / FX Movement
PM14 Payment Request / Attempt
PM15 Provider Adapter Contract
PM16 Provider Webhook Inbox
PM17 Provider Recovery / Status Sync
PM18 Payment Links
PM19 Provider Settlement/Payout Seam
PM20 Cashbox / Branch Controls
PM21 Read Models / Search
PM22 Banking Reconciliation Contract Boundary
PM23 Security / PCI / Secrets
PM24 Permissions / Approval / Audit
PM25 Performance / Concurrency / Load
PM26 Product + Finance + Security Acceptance
```

---

# 497. PM00 Exit Gate

ADRs approved:

- Payment movement vs Subledger application.
- Payment vs PaymentRequest/Attempt.
- PaymentAccount vs GL Account.
- provider lifecycle vs canonical lifecycle.
- refund vs reversal.
- Banking/Reconciliation boundary.

---

# 498. PM01 Exit Gate

- `finance_payments`.
- FORCE RLS.
- tenant-aware FKs.
- UUIDv7.
- exact decimals.
- no secret/plain card storage.

---

# 499. PM02 Exit Gate

Operational:

```text
Bank
Cashbox
ProviderClearing
Wallet
```

without direct GL ownership.

---

# 500. PM03 Exit Gate

Methods enabled/scoped by Entity/Branch/channel.

---

# 501. PM04 Exit Gate

Manual incoming Payment:

```text
Payment + Subledger + Ledger + Outbox
```

atomic/idempotent.

---

# 502. PM05 Exit Gate

Outgoing payment same integrity + optional approval.

---

# 503. PM06 Exit Gate

Party payment creates correct Subledger Open Item and can remain unapplied.

---

# 504. PM07 Exit Gate

No Journal Entry IDs selected by Payments; semantic Facts only.

---

# 505. PM08 Exit Gate

- command retry safe.
- same key/different payload conflict.
- concurrent duplicate safe.

---

# 506. PM09 Exit Gate

Same/cross-currency internal transfer semantics correct.

---

# 507. PM10 Exit Gate

Gross/Fee/Net explicit.

---

# 508. PM11 Exit Gate

Partial/full refunds cannot over-refund and preserve original.

---

# 509. PM12 Exit Gate

Reversal preserves history and coordinates downstream state.

---

# 510. PM13 Exit Gate

Movement FX and settlement FX clearly separate.

---

# 511. PM14 Exit Gate

Failed provider attempt does not create completed Payment.

---

# 512. PM15 Exit Gate

At least one reference provider adapter proves abstraction without leaking provider semantics into Core.

---

# 513. PM16 Exit Gate

Signature verification + Inbox + duplicate/out-of-order tests.

---

# 514. PM17 Exit Gate

Provider success/internal failure recovers exactly once.

---

# 515. PM18 Exit Gate

Payment Link lifecycle does not falsely imply Payment completion.

---

# 516. PM19 Exit Gate

Provider payout cannot duplicate customer payment.

---

# 517. PM20 Exit Gate

Cashbox Branch/user access tested.

---

# 518. PM21 Exit Gate

No N+1, indexed list/search, Subledger summary projection.

---

# 519. PM22 Exit Gate

Payments records movement; Banking matches statement separately.

---

# 520. PM23 Exit Gate

- secret manager.
- masking.
- no raw CVV/PAN.
- PCI architecture review.
- provider webhook security.

---

# 521. PM24 Exit Gate

permissions/approval/SoD/audit pass.

---

# 522. PM25 Exit Gate

- manual payment p95 measured.
- webhook burst.
- provider retry.
- concurrent refund.
- large payment history.
- provider rate-limit/recovery tests.

---

# 523. PM26 Acceptance

Required signoff:

```text
Product Architect
Finance/Accounting Lead
Payments/Integration Lead
Security Lead
Backend Lead
Frontend Lead
Performance Engineer
```

---

# 524. Build Now — MUST

- Incoming Payment.
- Outgoing Payment.
- Internal Transfer.
- PaymentAccount.
- Cash/Bank methods.
- Party payment/advance integration with Subledger.
- Ledger Facts.
- idempotency.
- refunds.
- reversal.
- fee model.
- multi-currency foundation.
- PaymentRequest/Attempt foundation.
- provider adapter contract.
- webhook Inbox.
- provider recovery.
- payment links foundation.
- payment read model.
- RLS/security/permissions/audit.
- no-card-data architecture.
- performance/concurrency tests.

---

# 525. Design Now / Build Later or Per Market

- advanced cheque/PDC lifecycle.
- direct debit mandates.
- provider disputes/chargebacks UI.
- provider payout reconciliation.
- cash drawer opening/count/closing.
- supplier payment runs.
- bank file generation.
- open banking/payment initiation.
- AI payment matching.
- fraud/risk engine.
- card-on-file/saved methods.
- recurring auto-pay.
- multi-provider smart routing.

---

# 526. Explicitly NOT Payments

- Invoice pricing/tax.
- Invoice outstanding truth.
- Open Item allocation engine.
- Chart of Accounts.
- Journal Entry construction.
- Bank statement ingestion/reconciliation.
- Customer collections.
- Supplier payment proposal selection.
- Cardholder-data vault.
- full Treasury/Cash forecasting.

---

# 527. Senior Product Rule

> **A user can record that money moved without being forced to know which invoice it belongs to at that moment.**

---

# 528. Senior Architecture Rule

> **Payments owns money movement; Subledger owns application; Ledger owns accounting; Banking owns reconciliation.**

---

# 529. Senior Integrity Rule

> **One real-world payment can create only one completed financial movement in TODIR, regardless of double clicks, retries, duplicate webhooks or recovery jobs.**

---

# 530. Senior Provider Rule

> **External provider success is verified evidence, not permission to duplicate internal financial effects.**

---

# 531. Senior History Rule

> **Completed payments are immutable; refunds, reversals and chargebacks add history instead of rewriting it.**

---

# 532. Senior Security Rule

> **TODIR minimizes payment-card data exposure, never stores CVV, avoids raw PAN handling, and keeps provider secrets out of ordinary application data.**

---

# 533. Senior Performance Rule

> **External-provider latency is isolated from TODIR's financial DB transactions; local financial completion remains short, atomic and idempotent.**

---

# 534. Senior UX Rule

> **The normal user sees Receive / Pay / Refund / Transfer. Provider attempts, accounting facts and application mechanics appear only to roles that need them.**

---

# 535. Source Register — Checked 2026-10-05

## ERPNext — Payment Entry

https://docs.frappe.io/erpnext/payment-entry

Used to validate real-world payment needs including:

- incoming/outgoing payments;
- internal transfers;
- advances;
- partial payments;
- multi-invoice references;
- bank/cash references;
- unallocated amounts.

## ERPNext — Payment Reconciliation

https://docs.frappe.io/erpnext/payment-reconciliation

Used to validate:

- allocation of a previously recorded Payment;
- no duplicate bank/cash movement during reconciliation;
- partial application;
- same Company/Party/control-account constraints.

## ERPNext — Unreconcile Payments

https://docs.frappe.io/erpnext/unreconciliation

Used to validate explicit reversal/unapplication rather than deleting original vouchers.

## ERPNext — Advance Payment Entry

https://docs.frappe.io/erpnext/advance-payment-entry

Used to validate advances before Invoice and later allocation.

---

# 536. Source Register — Business Central

## Microsoft Business Central — Payment Reconciliation Journal

https://learn.microsoft.com/en-us/dynamics365/business-central/receivables-how-reconcile-payments-auto-application

Used to validate:

- bank/payment-service transaction matching;
- automatic and manual application;
- separation of payment evidence from open-entry application;
- bank-account-specific reconciliation workflow.

---

# 537. Source Register — Provider Idempotency

## Stripe API — Idempotent Requests

https://docs.stripe.com/api/idempotent_requests

Used only as an example of a current provider pattern:

- money-changing API requests can be safely retried with idempotency keys;
- duplicate retry need not create a second provider object/effect.

TODIR provider abstraction is not Stripe-specific.

---

# 538. Source Register — PCI Security Standards Council

## PCI SSC Document Library

https://www.pcisecuritystandards.org/document_library/?category=pcidss

The official library currently lists PCI DSS v4.0.1.

## PCI SSC — PCI DSS v4.0.1 publication notice

https://blog.pcisecuritystandards.org/just-published-pci-dss-v4-0-1

Used to validate current PCI baseline reference.

Exact PCI scope/compliance obligations depend on TODIR's final payment architecture, providers, deployment and merchant responsibilities and require specialist review.

---

# 539. Final Architecture

```text
                 User / Portal / POS / API
                           │
                           ▼
                    Payment Request
                           │
              ┌────────────┴────────────┐
              │                         │
         Manual / Cash              Provider
              │                         │
              │                 Attempt / Webhook
              │                         │
              └────────────┬────────────┘
                           ▼
                  Completed Payment
                      /          \
                     /            \
                    ▼              ▼
              Subledger          Ledger
             Party Open Item   Accounting Fact
                    │
                    ▼
             Application Engine
                    │
                    ▼
           Invoice Outstanding

Meanwhile:

Provider Payout ──► Provider Settlement ──► Bank
                                          │
Bank Statement ───────────────────────────┘
                 Banking/Reconciliation
```

---

# 540. Final Business Verdict

For the business user:

```text
Receive 10,000
or
Pay 7,500
or
Refund 2,000
or
Transfer 50,000
```

should be simple.

For Finance/Security/Support, TODIR must still answer:

```text
Did money really move?
Through which account?
Was provider success verified?
Was it recorded more than once?
What remains unapplied?
Which invoices were settled?
What Ledger posting was created?
Was it refunded/reversed?
Did the bank statement reconcile it?
Which provider event/request caused it?
```

---

# 541. Final Engineering Verdict

Payments is approved only when:

```text
Payment movement is separate from settlement.
Completed payment is immutable.
Internal financial completion is idempotent.
Provider webhooks/retries cannot duplicate payment.
Timeout/unknown states cannot create false success/failure.
Subledger application remains separate.
Ledger posting remains separate.
Refund/reversal preserve original history.
Provider payout is not treated as new customer payment.
No external provider call holds the financial DB transaction.
Secrets/card data are protected by design.
Cash/Bank/Gateway flows perform under realistic concurrency.
```

---

# 542. Next Pack

After Payments:

> **`13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR know exactly what quantity exists, where it exists, what is reserved/available/in-transit, what each movement means, and what its financial cost/valuation is — while Sales, Purchasing and Invoicing never directly mutate stock?**

The Inventory pack should lock:

- Warehouses/Locations.
- Stock Items.
- On-hand / Reserved / Available / In-transit.
- Stock Movement ledger.
- Receipts / Issues / Transfers.
- Reservations.
- Inventory dimensions/lots/serials foundation.
- UOM integration.
- Negative-stock policy.
- costing/valuation ownership.
- COGS/Inventory Accounting Facts.
- physical count/adjustments.
- opening stock.
- performance at millions of movements.
- Sales/Purchasing contracts.

---

**END OF FILE — 12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
