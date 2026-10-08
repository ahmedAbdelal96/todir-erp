# 20 — TODIR ERP SaaS POS / Retail Checkout / Cashier Session Requirements & Technical Design Pack

**File:** `20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + RETAIL BUSINESS WORKFLOW + POS ORCHESTRATION + CASHIER OPERATIONS + OFFLINE RESILIENCE + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Retail Operations Lead, Store Manager, Cashier Lead, Finance/Accounting Lead, Inventory Lead, Payments Lead, Tax/Compliance Lead, Backend/Frontend Leads, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — MONEY + STOCK + TAX + CUSTOMER EXPERIENCE + HIGH-CONCURRENCY EDGE OPERATIONS**

---

## Canonical dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:
- Catalog owns Variant/UOM/barcode identity and preserves entered UOM/factor snapshots.
- Pricing owns reusable prices/discount resolution; missing price is never silently zero.
- Tax owns deterministic tax calculation and versioned tax snapshots.
- Inventory owns every physical stock movement, quantity truth and valuation.
- Payments owns actual money movement and provider state/recovery.
- Invoicing owns final invoice/legal financial document lifecycle.
- Ledger owns Journal Entries/account determination.
- POS orchestrates the retail checkout; it does not duplicate those truths.

---

# 0. Executive Decision

TODIR POS is the **retail transaction orchestrator and cashier operating system**.

It is not a second Sales module.

It is not a second Inventory.

It is not a second Payments engine.

It is not a second Invoicing engine.

It owns the retail checkout context and coordinates the domains needed to complete a real sale safely and quickly.

The core business question is:

> **How can a cashier complete a real-world sale in seconds while TODIR preserves correct product/UOM, price, tax, payment, stock, legal document, accounting and recovery behavior—even under retries, partial failures, returns, multiple terminals and intermittent connectivity?**

---

# 1. Business Correctness Comes Before Screen Design

This file is intentionally ordered:

```text
Business model
→ Domain ownership
→ State/lifecycle
→ Real retail workflows
→ Failure/recovery
→ Data model
→ Contracts/APIs
→ Security
→ Performance
→ Tests
→ Acceptance
→ UI implementation guidance
```

The screen can be redesigned later.

The retail transaction semantics must be correct from Day 1.

---

# 2. Central POS Principle

> **One fast cashier action may orchestrate many domains; it must never collapse their ownership or state machines.**

Example:

```text
[Complete Sale]
```

may coordinate:

```text
POS
+ Pricing
+ Tax
+ Payments
+ Inventory
+ Invoicing / Receipt
+ Compliance
+ Ledger
+ Audit
+ Outbox
```

while each domain remains canonical for its own truth.

---

# 3. Core Distinctions

Absolute:

```text
POS Sale != Payment
POS Sale != Invoice
POS Sale != Stock Movement
POS Sale != Fiscal Receipt
POS Sale != Accounting Entry

Return != Refund
POS Session Cash != Ledger Cash
Available Stock != Reserved/Guaranteed Stock
Displayed Price != Final Authoritative Checkout Price
```

---

# 4. Retail Business Scope

This pack targets general Retail POS:

```text
supermarket
fashion
electronics
mobile/accessories
parts
pharmacy-like inventory patterns
wholesale/retail counter
small shop
multi-branch retail chain
service counter
```

without hardcoding a single industry.

---

# 5. Restaurant Boundary

Do not force restaurant concepts into Retail Core.

Future optional capability/module can add:

```text
Tables
Floors
Kitchen routing
Modifiers
Courses
Guest count
Tips
Split bills by guest
KDS
```

Retail cashier does not see them.

---

# 6. POS Owns

POS owns:

```text
POS Terminal
POS Register
POS Session
POS Cart / Suspended Cart
POS Sale
POS Sale Line snapshots/orchestration references
POS Checkout attempt
POS Tender allocation intent
POS Return orchestration
POS Exchange orchestration
POS Cash Movement operational records
POS Session reconciliation
POS Offline transaction envelope/sync state
POS local configuration/read packages
POS operational reports/read models
```

---

# 7. POS Does NOT Own

```text
Product / Variant / UOM / Barcode      → Catalog
Reusable Price / Price List            → Pricing
Tax Rules / Tax math                    → Tax
On Hand / Stock Movement / Cost         → Inventory
Actual Payment movement/provider truth  → Payments
Invoice/Credit Note legal document      → Invoicing
AR/Open Item                             → Subledger
Journal Entry                            → Ledger
Customer identity                       → Directory
Country fiscal/eReceipt rules            → Country/Compliance Provider
```

---

# 8. Normal Retail Flow

Default:

```text
Open Session
→ Build Cart
→ Checkout
→ Pay
→ Complete Sale
→ Receipt
```

No Quotation.

No Sales Order.

No separate warehouse screen.

No separate payment-entry screen.

No accounting screen.

---

# 9. Stocked Retail Flow

Internally:

```text
Cart
→ authoritative price/tax validation
→ payment completion
→ Inventory Issue
→ document/compliance
→ Ledger/Outbox
→ Completed
```

Exact transaction orchestration is defined later by payment type and failure model.

---

# 10. Service Retail Flow

```text
Cart
→ price/tax
→ payment
→ receipt/invoice
```

No Inventory call.

---

# 11. Customer Is Optional

Default walk-in retail sale:

```text
Customer = none / anonymous walk-in context
```

Do not create Party per sale.

---

# 12. Known Customer Flow

Optional:

```text
Select existing Party(Customer Role)
or
Quick Create minimal customer
```

needed for:

```text
named invoice
credit sale
customer-specific price
loyalty later
purchase history
```

---

# 13. Anonymous Customer Is Not Fake Party

Do not create:

```text
Party: Walk-In Customer
```

as a shared mutable customer identity if not needed.

POS can use explicit anonymous/walk-in transaction context.

---

# 14. Legal Entity / Branch

Every POS sale belongs exactly one:

```text
Tenant
Legal Entity
Branch
```

Cross-entity sale is forbidden.

---

# 15. Terminal / Register / Session / Cashier Are Different Concepts

Do not collapse them.

```text
Terminal
= physical/logical device

Register
= store selling station/configuration

Session
= operational cashier/register working period

Cashier
= authorized User acting in session
```

---

# 16. POS Terminal

Concept:

```text
PosTerminal
{
    Id
    TenantId
    BranchId

    Code
    Name
    DeviceKey?
    RegisterId?

    DeviceProfile
    Status
    LastSeenAt?
}
```

---

# 17. POS Register

Concept:

```text
PosRegister
{
    Id
    LegalEntityId
    BranchId

    Code
    Name

    DefaultWarehouseId?
    DefaultLocationId?

    PriceContextRef?
    TaxContextRef?
    PaymentMethodPolicyRef?
    ReceiptPolicyRef?

    CashDrawerMode
    OfflinePolicyRef?

    Status
}
```

---

# 18. Terminal Can Change

Hardware replacement must not destroy register/session history.

---

# 19. Register Is Operational Context

Register determines defaults like:

```text
Branch
Warehouse
Price channel
Tax context
Allowed payment methods
Receipt behavior
Cash policy
Offline capability
```

---

# 20. POS Session

Concept:

```text
PosSession
{
    Id
    RegisterId
    TerminalId?
    CashierUserId

    OpenedAt
    ClosedAt?

    OpeningFloat?
    Currency

    State
    ExpectedCashProjection
    CountedCash?
    CashDifference?

    RowVersion
}
```

---

# 21. Session State

Minimal:

```text
Open
Closing
Closed
```

Optional:

```text
Suspended
NeedsReview
```

only if business requires.

---

# 22. Session Is Not Calendar Day

A session may:

```text
cross midnight
run 24/7
end at cashier handover
end before store day close
```

Do not bind Session = Date.

---

# 23. Session Open

Small company:

```text
Cashier
→ Open Register
→ optional Opening Float
→ Sell
```

---

# 24. Card-Only Register

May have:

```text
OpeningFloat = not applicable
CashDrawer = disabled
```

No forced cash workflow.

---

# 25. Shared Cash Drawer

Advanced policy.

Do not assume one cashier always owns one physical drawer.

If enabled, session/drawer accountability must be explicit.

---

# 26. Cashier Handover

Possible flow:

```text
Cashier A closes/counts
→ Manager confirms
→ Cashier B opens new session
```

Do not just replace UserId on an open session.

---

# 27. POS Cart

Cart is editable pre-sale workspace.

Concept:

```text
PosCart
{
    Id
    SessionId
    CustomerPartyId?
    Currency
    State
    PriceContextVersion
    TaxPreviewContextVersion
    CreatedAt
    UpdatedAt
    RowVersion
}
```

---

# 28. Cart Is Not Financial History

Before completion:

```text
no completed Payment
no confirmed stock issue
no final invoice/receipt
no final Ledger fact
```

unless a specific payment reservation/authorization flow explicitly exists.

---

# 29. Cart States

```text
Active
Held
Cancelled
CheckoutInProgress
ConvertedToSale
```

---

# 30. Hold / Resume

Hold means:

```text
cart preserved
no sale completed
no stock issue
no final financial effect
```

---

# 31. Held Cart Expiry

Configurable cleanup.

If cart contains payment authorization/reservation in future, release behavior must be coordinated.

Baseline held cart has no money/stock effect.

---

# 32. Cancel Cart

Pre-checkout cancel is not return/refund.

It simply closes editable cart.

---

# 33. Sale Is Final Commercial Retail Event

`PosSale` represents the completed/committed retail transaction context.

It snapshots what the customer bought and how checkout resolved.

---

# 34. POS Sale Concept

```text
PosSale
{
    Id
    TenantId
    LegalEntityId
    BranchId

    RegisterId
    SessionId
    CashierUserId

    CustomerPartyId?

    Currency
    SaleDateTime
    BusinessDate?

    CommercialState

    Subtotal
    DiscountTotal
    TaxTotal
    GrandTotal

    PriceSnapshotVersion
    TaxSnapshotRef

    InventoryState
    PaymentState
    DocumentState
    RegulatoryState
    AccountingState

    SourceCartId
    IdempotencyKey

    CreatedAt
}
```

---

# 35. Separate State Dimensions

Do not create one mega enum:

```text
PaidAndIssuedAndStockedAndPosted
```

Use:

```text
CommercialState
PaymentState
InventoryState
DocumentState
RegulatoryState
AccountingState
```

---

# 36. Commercial State

Recommended:

```text
PendingCompletion
Completed
CorrectedByReturn
CancelledBeforeCompletion
RecoveryRequired
```

---

# 37. Payment State Authority

Payments.

POS stores/refers to projection:

```text
Unpaid
PartiallyPaid
Paid
PaymentPending
PaymentFailed
RefundedPartially
Refunded
```

---

# 38. Inventory State Authority

Inventory.

Projection:

```text
NotApplicable
Pending
Issued
PartiallyReturned
Returned
Exception
```

---

# 39. Document State Authority

Invoicing/receipt capability.

Projection:

```text
NotRequired
Pending
Finalized
Corrected
```

---

# 40. Regulatory State Authority

Country/Compliance provider:

```text
NotRequired
Pending
Submitted
Accepted/Cleared
Rejected
RecoveryRequired
```

exact names provider-specific.

---

# 41. Accounting State Authority

Ledger:

```text
NotPosted
Posted
PostingFailed
Corrected
```

---

# 42. Sale Line

Concept:

```text
PosSaleLine
{
    Id
    PosSaleId

    ProductVariantId
    ProductSnapshot
    EnteredUomId
    EnteredQuantity
    BaseQuantity
    UomFactorSnapshot

    ResolvedUnitPrice
    ManualPriceOverride?
    DiscountSnapshot

    TaxSnapshotLineRef
    LineSubtotal
    LineTax
    LineTotal

    InventoryProfileSnapshot?
    LotSerialRequirementSnapshot?
}
```

---

# 43. Product Identity Rule

Final transaction reference:

```text
ProductVariantId
```

even simple product.

---

# 44. UOM Rule

Preserve:

```text
Entered UOM
Entered Quantity
Base UOM
Conversion Factor snapshot
Base Quantity
```

Never store only base quantity and lose what cashier sold.

---

# 45. POS Display Rule For UOM

Where ambiguity exists, cashier/customer must see:

```text
2 BOX × 480 / BOX
```

not:

```text
2 × 480
```

---

# 46. Barcode Ownership

Catalog owns barcode.

POS resolves:

```text
Barcode
→ Variant
→ UOM if barcode/UOM-specific
```

---

# 47. Barcode With UOM

Example:

```text
barcode A → 1 PCS
barcode B → 1 BOX (24 PCS)
```

Scanning must add correct UOM.

---

# 48. Scanner Repetition

Scanning same item multiple times can:

```text
increment quantity
```

according to product/serial policy.

---

# 49. Serial-Controlled Product Scan

A serial barcode may represent:

```text
specific serial unit
```

POS must not simply increment quantity.

Inventory validates serial eligibility.

---

# 50. Unknown Barcode

Explicit:

```text
Barcode not found
```

No silent ignore.

Authorized user may:

```text
Search Product
```

Product creation belongs Catalog, not POS.

---

# 51. Quick Product Create

Not normal cashier capability.

If company needs it, use Catalog Quick Create permission and readiness rules.

---

# 52. Product Search

Search by:

```text
Barcode
SKU
Arabic Name
English Name
Alias
Variant attributes
```

using lightweight Catalog projection.

---

# 53. Product Tiles

Optional speed layer.

They are shortcuts to Catalog products, not duplicate product master.

---

# 54. Variant Visibility

For fashion/electronics:

```text
T-Shirt
Black / XL
SKU
UOM
Price
```

must be clear.

---

# 55. Service Product

No stock operation.

---

# 56. Non-Stock Good

Can sell without physical Inventory movement according to Catalog/Inventory profile.

---

# 57. Stocked Good

Checkout requests Inventory Issue.

POS never updates stock table.

---

# 58. Inventory Availability In Cart

Can display:

```text
Available now
```

as informational.

---

# 59. Availability Is Not Guarantee

Checkout must revalidate authoritative Inventory availability unless reserved/committed policy exists.

---

# 60. POS Reservation

Not baseline for walk-in retail.

Normal retail checkout should directly issue at completion.

Optional reservation for special flows requires Inventory-owned Reservation.

---

# 61. Negative Stock

Inventory policy owns:

```text
Disallow
Warn
Allow
```

POS cannot bypass.

---

# 62. Warehouse

Register defaults Warehouse.

Inventory remains authority.

---

# 63. Location

Register can default POS selling location.

Detailed location hidden unless required.

---

# 64. Lot-Controlled Product

Checkout must provide valid lot when Inventory profile requires.

---

# 65. Serial-Controlled Product

Checkout must provide each serial exactly once.

---

# 66. FEFO / Lot Suggestion

Inventory can suggest eligible lot/FEFO.

POS may auto-select only when policy guarantees correctness and user can verify where required.

---

# 67. Weighted / Decimal Product

Catalog/UOM supports decimal quantity.

Examples:

```text
2.500 KG
1.750 METER
0.500 LITER
```

---

# 68. Scale Integration

Future/adapter capability can supply measured quantity.

POS still validates UOM/precision.

---

# 69. Quantity Precision

Use Catalog UOM precision policy.

No float.

---

# 70. Quantity Change

Pre-checkout editable.

Final sale quantity immutable; correction via Return/Exchange.

---

# 71. Price Ownership

Pricing owns reusable price resolution.

POS requests:

```text
Direction = Sell
Channel = POS
LegalEntity
Branch
Customer?
Variant
UOM
Qty
DateTime
Currency
```

---

# 72. Price Snapshot

POS stores resolved commercial result:

```text
PriceList/version
BasePrice
Discount facts
FinalUnitPrice
Manual Override?
Reason/actor
```

---

# 73. Missing Price

Never become zero automatically.

POS shows explicit blocking:

```text
No valid price
```

Authorized override can set transaction price with reason.

---

# 74. Manual Price Override

Does not modify Price List.

Stores:

```text
resolved price
override price
deviation
reason
actor
time
manager approval?
```

---

# 75. Discount Types

Core POS can support:

```text
Line Discount
Cart Discount
Manual Discount
Pricing-resolved Discount
Promotion Result
```

with clear source.

---

# 76. Promotion Boundary

Pricing pack intentionally did not own:

```text
Buy-X-Get-Y
Coupons
Loyalty
Free gifts
```

POS design must leave a typed `PromotionEngine` seam.

Do not embed ad-hoc promotional logic into checkout handlers.

---

# 77. Day-1 Promotion Strategy

Build only needed launch promotions.

Recommended minimum:

```text
manual line/cart discount
Pricing resolved discounts
```

Advanced retail promotion engine can be later capability.

---

# 78. Discount Permission

Examples:

```text
Cashier <= 5%
Supervisor <= 15%
Above requires manager override
```

thresholds configurable.

---

# 79. Inline Manager Override

POS-sensitive operations should support fast inline approval:

```text
Manager PIN / step-up
```

rather than long approval workflow, where security policy permits.

---

# 80. Override Audit

Capture:

```text
cashier
approver
requested discount/price
allowed threshold
reason
timestamp
```



# 81. Price Change During Cart

Default:

```text
cart preview uses current known price
checkout performs authoritative price validation
```

---

# 82. Checkout Repricing

If authoritative price changed:

```text
Old
New
Reason/version
```

must be visible before commitment when policy requires.

---

# 83. Price Lock Policy

Possible:

```text
RepriceAtCheckout
LockAtFirstScanForSessionWindow
ManagerCanHonorOldPrice
```

Default should favor current authoritative price with explicit exception.

---

# 84. Tax Ownership

Tax Engine owns tax calculation.

POS supplies retail context.

---

# 85. POS Tax Context

Includes:

```text
Legal Entity
Branch/Jurisdiction
Customer tax profile if known
Product tax classification
Qty/UOM
Resolved price
Tax inclusive/exclusive mode
Tax date/time
Document type/context
Country Pack version
```

---

# 86. Tax Preview

Cart displays Tax preview.

---

# 87. Final Tax

Checkout revalidates/finalizes exact Tax snapshot through authoritative Tax/Invoicing/Compliance flow.

---

# 88. Tax-Inclusive Retail

Common retail price can be tax-inclusive.

Pricing returns commercial price mode.

Tax extracts/calculates.

---

# 89. Tax-Exclusive Retail

Supported where business/market uses it.

Display must clearly communicate final payable total.

---

# 90. Tax Rule Change

Old finalized sales remain on old Tax snapshot.

Future checkouts use active rule version.

---

# 91. Offline Tax

Offline taxation requires explicit synced Country/Tax package version.

Never run incomplete guessed tax logic offline.

---

# 92. Offline Tax Package

Potential package includes:

```text
Tax rule set version
Product tax classifications
Entity registration
Jurisdiction assumptions allowed offline
Effective dates
Expiry/revalidation timestamp
Signature/hash
```

---

# 93. Tax Offline Validity

If package expired or context cannot be resolved:

```text
offline sale may be blocked
```

depending country policy.

---

# 94. Receipt / Invoice Boundary

POS sale is not automatically the legal invoice entity.

---

# 95. Retail Document Types

Depending country/company:

```text
simple POS receipt
simplified tax invoice
full tax invoice
electronic receipt
fiscal receipt
```

Country/Compliance/Invoicing decides legal semantics.

---

# 96. Walk-In Receipt

For simple cash retail:

```text
PosSale
→ retail receipt artifact/document
```

according to configured legal policy.

---

# 97. Named Tax Invoice

Known customer may require:

```text
Directory Party
→ Invoicing Customer Invoice
```

from POS sale source.

---

# 98. One Sale, Correct Document

Do not duplicate economic sale by creating unrelated receipt + invoice if legal process says invoice supersedes/represents same sale.

Use typed source relationship.

---

# 99. Invoicing Snapshot

Final legal document freezes:

```text
Party/legal snapshot
Product/UOM
price
discount
tax
totals
dates
```

per Invoicing rules.

---

# 100. Document Numbering

Use Platform/Invoicing/Compliance numbering.

POS does not invent legal numbers.

---

# 101. Receipt Number

Operational POS receipt identifier may be separate from statutory document number if law/provider requires.

---

# 102. Fiscal / eReceipt Boundary

Country Pack/Compliance provider owns:

```text
payload format
device/fiscal identity
signing
submission
clearance/reporting
QR rules
statutory response codes
```

---

# 103. POS Does Not Hardcode Country Fiscal Logic

Forbidden:

```text
if country == Egypt ...
if country == Saudi ...
```

inside generic POS checkout.

---

# 104. Fiscal State Separate

A sale may be commercially complete and payment/stock correct while:

```text
RegulatoryState = Pending/RecoveryRequired
```

only if local law/provider allows deferred reporting.

Country provider decides.

---

# 105. Hard Fiscal Gate

If market requires authority/fiscal device success before customer sale can legally complete:

provider defines that gate.

POS workflow must honor it.

---

# 106. Compliance Network Call

Do not hold long DB transaction open during external fiscal API call.

Use:

```text
prepare
commit internal provisional state if legally allowed
external call
verify
final state/recovery
```

or provider-specific safe protocol.

---

# 107. Business Model Must Support Both

```text
Synchronous fiscal gate
Deferred/asynchronous fiscal reporting
```

without changing core ownership.

---

# 108. Payment Is First-Class Separate State

Checkout can include one or more tenders.

---

# 109. Tender

Concept:

```text
PosTenderIntent
{
    Id
    CheckoutId
    PaymentMethod
    RequestedAmount
    Currency
    PaymentRequestRef?
    PaymentRef?
    State
}
```

---

# 110. Tender Methods

Configured from Payments:

```text
Cash
Card
Wallet
Bank
StoreCredit later
GiftCard later
Other allowed method
```

---

# 111. Split Tender

Example:

```text
Total 1,000
Cash 400
Card 600
```

---

# 112. Split Tender Invariant

Before fully paid sale completion:

```text
sum confirmed tender amounts
>= required amount
```

with change logic only for eligible cash-like methods.

---

# 113. Over-Tender Cash

Cash can exceed due to change:

```text
Due 450
Cash Received 500
Change 50
```

Canonical Payment movement records net/gross according to Payments design; POS cash movement must reconcile.

---

# 114. Card Over-Tender

Normally not allowed unless provider/business policy supports.

---

# 115. Partial Payment

Normal immediate-retail checkout can remain:

```text
PaymentState = PartiallyPaid
```

during checkout.

Commercial completion usually blocked until amount due is satisfied unless Credit Sale flow enabled.

---

# 116. Failed Tender

Failed Card does not destroy Cart.

Cashier can:

```text
retry safely
choose another method
remove failed tender
```

---

# 117. Provider Unknown State

Critical:

```text
Provider timeout != Failed
```

Use Payments:

```text
Unknown/Pending
→ status verification
→ recovery
```

---

# 118. Lost Client Response After Provider Success

Flow:

```text
Payment provider succeeds
network/client response lost
cashier retries
```

must return/recover original payment, never charge twice.

---

# 119. Checkout Payment Idempotency

Stable source reference:

```text
PosCheckoutId + TenderId
```

used with Payments idempotency.

---

# 120. Same Key Different Amount

Reject.

---

# 121. Cash Payment

No external provider.

Payments/Cashbox records actual cash movement.

POS drawer projection consumes source.

---

# 122. Card Payment

Payments owns:

```text
PaymentRequest
Attempt
ProviderState
Completed Payment
Fee
Settlement seam
```

POS only observes completion.

---

# 123. Payment Terminal Integration

Adapter through Payments/provider.

POS should not embed terminal SDK in domain layer.

---

# 124. Terminal Timeout

Do not let cashier immediately start duplicate payment without checking provider/terminal state.

---

# 125. Payment Cancel

Before provider completion:

request cancel/abandon attempt according to adapter.

A completed Payment cannot be "cancelled" by deleting it.

---

# 126. Refund

Payments owns outgoing refund movement.

Original payment remains.

---

# 127. Cumulative Refund Limit

Payments enforces:

```text
Cumulative refunds <= refundable amount
```

unless explicit special correction flow.

---

# 128. Payment Reversal

Different from Refund.

Use Payments semantics/provider capabilities.

---

# 129. Credit Sale

Optional.

Known Party only by default.

Flow:

```text
POS Sale
→ Invoice/Open Item
→ full/partial/no immediate payment
```

according to company policy.

---

# 130. Credit Sale Is Not Split Tender

Distinguish:

```text
Split Tender
= multiple immediate payment methods

Credit Sale
= amount remains receivable/open item
```

---

# 131. Credit Control

If enabled:

```text
Subledger.CheckCredit
```

before allowing customer credit exposure.

POS does not calculate credit exposure.

---

# 132. Anonymous Credit Sale

Disallowed by default.

Need Party/customer identity.

---

# 133. Deposit / Layaway

Not baseline.

Future capability can model reservation/partial-pay workflow.

Do not fake with incomplete POS sale.

---

# 134. Checkout Is A Saga-Like Business Orchestration, Not Distributed Transaction

External payment/fiscal provider cannot participate in PostgreSQL ACID transaction.

Therefore checkout needs explicit recoverable state.

---

# 135. Checkout Attempt

Concept:

```text
PosCheckout
{
    Id
    CartId
    SaleId?

    AttemptNumber
    IdempotencyKey

    PriceValidationVersion
    TaxValidationVersion

    CheckoutState
    StartedAt
    CompletedAt?

    FailureCode?
    RecoveryState?
}
```

---

# 136. Checkout State

Recommended:

```text
Started
Validated
PaymentInProgress
PaymentSatisfied
InternalCompletionInProgress
Completed
NeedsRecovery
FailedBeforePayment
CancelledBeforePayment
```

Avoid meaningless generic states.

---

# 137. Pre-Payment Validation

Before external irreversible money action:

```text
Cart not empty
Session open
Register active
Products active/allowed
UOM valid
Current price resolved
Discount authorized
Tax valid
Stock precheck where applicable
Customer/credit policy valid
Required serial/lot information captured or resolvable
Compliance precheck
```

---

# 138. Why Validate Before Payment

Reduce scenario:

```text
customer charged
then obvious local validation fails
```

---

# 139. Precheck Is Not Final Stock Lock Forever

Concurrent sales can still change Inventory.

Final inventory operation must be concurrency-safe.

---

# 140. Cash Checkout Atomic Path

For cash/no external provider, prefer one coordinated internal transaction where feasible:

```text
POS Sale
+ Payment Cash
+ Inventory Issue
+ Invoice/financial fact as required
+ Audit
+ Outbox
```

through Transaction Coordinator and module contracts.

---

# 141. External Card Checkout Path

External call breaks ACID.

Safe sequence must avoid double charge and incomplete untracked state.

Reference pattern:

```text
1. Validate cart
2. Create durable Checkout + PaymentRequest intent
3. Commit
4. Call provider/terminal
5. Verify result
6. On success, run idempotent internal completion transaction
7. If internal completion fails, mark NeedsRecovery
8. Recovery re-runs internal completion exactly once
```

---

# 142. Provider Success / Internal Completion Failure

Must become:

```text
Payment confirmed
Sale completion pending recovery
```

not "Payment Failed".

---

# 143. Recovery Worker

Idempotently completes:

```text
POS Sale
Inventory
Document
Ledger fact
Audit
Outbox
```

from verified payment source.

---

# 144. Cashier Recovery UX

Show:

```text
Payment received — completing sale
Do not charge again
```

and provide safe status refresh.

---

# 145. Customer Communication During Recovery

Receipt may be delayed depending legal state.

Provide clear provisional/final receipt policy.

---

# 146. Internal Completion Must Be Idempotent

Repeated recovery must not create:

```text
second stock issue
second invoice
second Ledger posting
second Sale
```

---

# 147. Source Idempotency Keys

Typed unique relationships:

```text
Checkout → Sale
Sale → InventoryOperation
Sale → Invoice/Receipt
Sale → Accounting Fact
Tender → Payment
```

---

# 148. Double Click Checkout

One sale.

---

# 149. Browser Reload During Checkout

Status recovers from CheckoutId/idempotency.

No fresh payment by default.

---

# 150. Terminal Reconnect

Same.

---

# 151. Inventory Completion

For stocked lines, POS requests Inventory Issue.

---

# 152. Inventory Issue Source

Typed:

```text
SourceType = POS
SourceId = PosSaleId
```

---

# 153. Inventory Quantity

Use BaseQuantity + entered UOM snapshot.

---

# 154. Inventory Failure Before External Payment

Block checkout safely.

---

# 155. Inventory Failure After External Payment

Rare concurrency/recovery scenario.

Must not disappear.

Policy options:

```text
Retry issue if valid
Manager resolution/substitute
Refund payment
controlled negative stock only if Inventory policy allows
```

No silent stock corruption.

---

# 156. Inventory Compensation Is Not Automatic Fake Stock

Do not create adjustment just to make checkout pass.

---

# 157. Stock Issue Atomicity

For internal cash sale and same DB, use Transaction Coordinator where required.

---

# 158. Lot / Serial Final Validation

Inventory locks/validates selected serial/lot at Issue.

---

# 159. Serial Race

Same serial cannot be sold by two terminals.

One succeeds; other receives clear conflict.

---

# 160. Product Deactivated During Cart

Checkout revalidation blocks new sale unless explicit honor-existing-cart policy.

History remains.

---

# 161. Price Deactivated During Cart

Revalidate.

---

# 162. Tax Rule Changed During Cart

Revalidate final Tax.

---

# 163. Session Closed During Cart

Cart cannot complete against closed session.

Manager can transfer/reopen through controlled flow if policy allows.

---

# 164. Register Disabled

No new sale.

Existing recovery can still complete safely through backend.

---

# 165. Sale Business Date

May differ from UTC date and session opening date.

Use Branch timezone/business-day policy.

---

# 166. 24/7 Store

Business date can roll at configured cutoff while session remains open if operational policy permits.

---

# 167. Store Day

Optional operational concept:

```text
StoreBusinessDay
```

separate from cashier session.

Do not require Day Close for small companies unless needed.

---

# 168. Cash Drawer Mission

Track operational cash accountability.

---

# 169. Cash Movement Types

```text
OpeningFloat
CashSaleReceipt
CashRefund
CashIn
CashOut
CashDrop
CashPickup
ClosingAdjustment
```

typed.

---

# 170. No Mutable Drawer Balance Truth

Expected cash derives from typed movements.

Projection allowed.

---

# 171. Manual Cash In

Examples:

```text
add float
owner deposit
approved cash correction
```

requires reason.

---

# 172. Manual Cash Out

Examples:

```text
petty cash transfer
cash pickup
safe drop
```

must not become Expenses module replacement.

Use reference/reason; material expenses should use proper domain.

---

# 173. Cash Drop

Optional:

```text
drawer → safe
```

operationally reduces drawer expected cash without meaning company cash disappeared.

Payments/accounting treatment separately modeled.

---

# 174. Opening Float

Operational starting drawer cash.

Finance/accounting mapping depends company process.

---

# 175. Expected Closing Cash

Concept:

```text
Opening Float
+ Cash Sales
+ Approved Cash In
- Cash Refunds
- Cash Out
- Cash Drops
= Expected Drawer Cash
```

exact categories depend configured drawer policy.

---

# 176. Counted Cash

Human physical count.

---

# 177. Cash Difference

```text
Counted - Expected
```

---

# 178. Difference Policy

```text
WithinTolerance → close
OutsideTolerance → reason
HighDifference → manager approval
```

---

# 179. Cash Difference Is Not Auto Write-Off

Accounting decision explicit.

POS records operational difference and source fact.

---

# 180. Close Session

Process:

```text
Stop new checkout
→ Resolve pending checkout/recovery
→ Count cash
→ Compare expected
→ Resolve/approve difference
→ Close
```

---

# 181. Pending Provider Payment At Close

Cannot silently close as if final.

Policy:

```text
block close
or
close with tracked pending tender/recovery
```

depending provider/operation design.

---

# 182. Held Carts At Close

Options:

```text
cancel
transfer to new session
keep register-level held cart
```

policy.

Default cancel/explicit transfer; no hidden orphan carts.

---

# 183. Session Reopen

Closed session should not casually reopen.

If correction needed:

```text
controlled adjustment
manager permission
audit
```

---

# 184. Store Summary

Across sessions/registers:

```text
Sales
Payment mix
Refunds
Discounts
Cash expected/count/difference
Open recovery items
```

---

# 185. Operational Store Close

Not same as statutory fiscal Z close.

---

# 186. Fiscal Close

Country provider owns if applicable.

---

# 187. Returns Mission

Correct original sale without rewriting it.

---

# 188. Return Is New Business Event

Original sale remains immutable.

---

# 189. Return Types

```text
Full
Partial lines
Partial quantity
With original receipt
Without receipt
Exchange-related
Damaged/quality hold
```

---

# 190. Return With Receipt

Preferred flow:

```text
Find original Sale
→ select eligible lines/qty
→ validate return policy
→ confirm physical disposition
→ create return
→ Inventory Return if physical
→ Credit document if required
→ Refund/credit if money returned
```

---

# 191. Return Quantity Invariant

Cumulative returned quantity:

```text
<= originally sold quantity
```

for the original source line, after prior returns.

---

# 192. Concurrent Return Protection

Two terminals cannot over-return same sold quantity.

---

# 193. Return Window

Configurable policy:

```text
N days
product/category-specific
customer-specific later
no expiry
country consumer law override
```

Country Pack may impose minimum rights.

---

# 194. Return Reason

Typed:

```text
WrongItem
Defective
CustomerChangedMind
SizeExchange
Damaged
Expired
Other
```

configurable.

---

# 195. Physical Disposition

Returned stocked item can go to:

```text
Available
QualityHold
Damaged
Scrap flow later
```

Inventory owns stock state/location.

---

# 196. Return Does Not Always Mean Available Stock

Critical.

Damaged return must not inflate sellable availability.

---

# 197. Refund Is Separate

Return can have:

```text
NoRefund
PartialRefund
FullRefund
StoreCredit later
ExchangeValue
```

according to policy.

---

# 198. Refund Without Return

Possible for:

```text
service failure
price correction
customer goodwill
```

requires authorization/reason.

No Inventory movement.

---

# 199. Return Without Refund

Possible:

```text
warranty replacement
exchange process
company policy
```

must be explicit.

---

# 200. No-Receipt Return

Policy:

```text
Disallow
ManagerApproval
AllowByCustomerHistory
AllowWithinAmount
```

---

# 201. No-Receipt Product Identity

Still requires exact Product Variant/UOM/quantity.

If no source sale link, stronger controls and lower refund assumptions.

---

# 202. Refund Method

Policy can:

```text
OriginalMethodPreferred
CashOnlyIfOriginalCash
ManagerOverride
StoreCredit later
```

Payments/provider restrictions apply.

---

# 203. Card Refund

Payments/provider handles.

POS does not create cash refund just because card provider is temporarily unavailable unless policy explicitly permits alternative.

---

# 204. Refund Pending

Return may be physically complete while Refund pending.

Separate states.

---

# 205. Credit Note

Invoicing owns legal correction document.

---

# 206. Tax On Return

Tax/Invoicing/Country Pack computes corrective tax.

POS never negates tax naïvely.

---

# 207. Exchange

Exchange is orchestration:

```text
Return old item/value
+
New sale
```

---

# 208. Exchange Is Not Editing Original Sale

Original remains.

---

# 209. Exchange Difference

Example:

```text
Returned item value = 500
New item = 650
Customer pays = 150
```

or:

```text
Returned = 650
New = 500
Refund/credit = 150
```

---

# 210. Exchange Atomic UX, Separate Facts

One cashier flow can create:

```text
Return
New Sale
Refund/Additional Payment
Inventory return + issue
Documents
```

with linked transaction group.

---

# 211. Exchange Failure Recovery

If return completed but new payment fails:

system must retain coherent return and allow continuation/correction.

Do not roll back external refund/provider action blindly.

---

# 212. Return Of Serial Item

Must return same Serial identity where original source available.

Inventory validates.

---

# 213. Return Of Lot Item

Maintain lot relation when possible/required.

---

# 214. Service Return

No physical stock return.

Use credit/refund/correction only.

---

# 215. Credit Sale Return

Subledger open item correction coordinated through Invoicing/Payments.

---

# 216. Customer Refund Over Original

Blocked.

---

# 217. Cash Refund Drawer Effect

Reduces operational drawer cash.

---

# 218. Different Branch Return

Policy:

```text
Disallow
AllowIfSharedEntity
AllowSelectedBranches
```

Inventory destination and payment policy explicit.

---

# 219. Different Legal Entity Return

Do not treat as simple same sale.

Requires cross-entity legal/accounting handling; default disallow.

---

# 220. Gift Receipt / Hidden Price Return

Future capability.

Source value remains known internally.



# 221. Customer / Directory Integration

Known customer selection uses Directory.

POS can display permitted:

```text
Name
Phone
Customer Code
Commercial labels
```

---

# 222. Quick Customer Create

Target:

```text
Name
Phone
```

plus minimum country/company requirements.

Do not block a normal retail sale on tax/legal fields unless a named legal invoice requires them.

---

# 223. Customer Duplicate Check

Directory handles search-before-create.

---

# 224. Customer-Specific Pricing

Pricing receives Party context.

---

# 225. Customer Credit

Subledger controls exposure/eligibility.

---

# 226. Customer History In POS

Optional lightweight view:

```text
Recent purchases
Returns
Outstanding balance if permitted
```

from source modules.

No POS duplicate history truth.

---

# 227. Loyalty Boundary

Not Core Day 1.

Design seam for:

```text
Points
Rewards
Membership
Tier
```

---

# 228. Gift Card Boundary

Not ordinary Payment method until dedicated stored-value liability capability exists.

Do not model gift card as free cash.

---

# 229. Store Credit Boundary

Requires typed customer credit liability/open-item design.

Not a generic POS discount.

---

# 230. Coupons Boundary

Future Promotion capability.

---

# 231. Product Bundles / Kits

Catalog currently treats advanced kits/bundles as later design.

POS must not invent BOM semantics.

Future sellable bundle capability integrates through Catalog/Pricing/Inventory.

---

# 232. Retail Promotions Architecture Seam

Conceptual contract:

```text
IRetailPromotionResolver
```

Input:

```text
Cart context
Customer
Branch
Date/time
Products/UOM/qty
Current base pricing
Coupon/reward context
```

Output:

```text
AppliedPromotion
Discount allocations
Free item entitlements
Explanation
Version
```

---

# 233. Promotion Snapshot

Final sale must preserve applied promotion/version/allocation.

---

# 234. Promotion Revalidation

Checkout authoritatively revalidates.

---

# 235. Conflicting Promotions

Resolver has deterministic stacking/exclusion policy.

No insertion-order behavior.

---

# 236. Free Item

Must be explicit:

```text
Qty
Unit price basis
Discount = full amount / promotion allocation
Tax treatment
Inventory issue
```

Never simply add unpriced stock line.

---

# 237. Price / Promotion / Tax Ordering

Must be explicit and tested:

```text
Base price
→ commercial discount/promotion allocation
→ taxable basis
→ tax
→ total
```

or country-specific sequence.

No duplicate calculators.

---

# 238. POS Does Not Calculate VAT Itself

Tax Engine only.

---

# 239. Rounding

FinBase rules.

---

# 240. Cart Total

Derived from line money/tax snapshots.

No float.

---

# 241. Currency

Normal register configured for one transaction currency.

Multi-currency cash acceptance can be future capability with explicit FX/payment handling.

---

# 242. Currency Mismatch

Do not silently convert tender.

---

# 243. Tip

Not Retail Core.

Restaurant/service gratuity capability later.

---

# 244. Sales Channel

POS sale contributes:

```text
SalesChannel = POS
```

for reporting/source context.

Sales module remains owner of quotation/order workflow when used.

---

# 245. POS Direct Sale Does Not Need Sales Order

Retail sale is its own direct transaction/orchestration.

Do not create hidden Sales Order for every checkout.

---

# 246. POS Quotation Boundary

If customer needs quotation before sale:

use Sales.

Later accepted quotation can be payable/fulfilled at POS only through explicit source-conversion capability.

Not baseline.

---

# 247. Ecommerce Boundary

Future ecommerce orders may be fulfilled/collected at store.

Do not mix online order source with walk-in sale without source type.

---

# 248. Click & Collect Seam

Future:

```text
External/Sales Order
→ POS pickup/payment
```

with Sales/Inventory ownership.

---

# 249. Return From Ecommerce At POS

Future cross-channel return policy.

Requires source system/order/document linkage.

---

# 250. Hardware Integration Principles

Hardware is adapter-driven:

```text
Barcode scanner
Receipt printer
Cash drawer
Customer display
Scale
Payment terminal
Fiscal device
```

---

# 251. No Hardware SDK In Domain

Adapters/infrastructure only.

---

# 252. Barcode Scanner

Typically keyboard-wedge/HID supported without special domain integration.

Dedicated adapter optional.

---

# 253. Receipt Printer

Rendering/print adapter.

Printing failure does not duplicate sale.

---

# 254. Reprint Receipt

Allowed with:

```text
Reprint marker / audit
```

according to policy.

No new sale/document number.

---

# 255. Cash Drawer Open

Can be triggered by:

```text
cash payment
authorized no-sale open
cash movement
```

---

# 256. No-Sale Drawer Open

Sensitive action:

```text
permission
reason optional
audit
```

---

# 257. Customer Display

Read-only projection from current cart/sale.

---

# 258. Customer Display Truth

Shows:

```text
Product
Variant
UOM
Qty
Price
Discount
Tax/Total according to market
```

same commercial data as cashier.

---

# 259. Scale

Reads quantity only.

Product/UOM mapping still Catalog.

---

# 260. Device Health

POS can surface:

```text
Printer Offline
Payment Terminal Offline
Scale disconnected
Fiscal device unavailable
```

without confusing business state.

---

# 261. Offline Mission

Retail operation should tolerate connectivity loss **only where correctness, payment and local law allow**.

---

# 262. Offline Is Not "Everything Works"

Every capability must be classified.

---

# 263. Offline Capability Matrix

Each operation declares:

```text
OnlineOnly
OfflineSafe
OfflineWithLimit
OfflineWithManagerApproval
CountryProviderControlled
```

---

# 264. Recommended Baseline Matrix

```text
Cached product search       → OfflineSafe
Cached price                → OfflineWithValidity
Cached tax                  → ProviderControlled
Cash sale                   → OfflineWithPolicy
Online card payment         → OnlineOnly/provider dependent
New legal customer          → Usually OnlineOnly
Simple anonymous cash sale  → Potential OfflineSafe
Return                      → Restricted
Refund                      → Online/payment dependent
Real-time stock check       → OnlineOnly
Offline stock estimate      → Allowed but clearly stale
Receipt print               → Offline local if document policy permits
Fiscal submission           → CountryProviderControlled
```

---

# 265. Offline Policy Is Per Register/Legal Entity

No universal global switch.

---

# 266. POS Local Resilience Package

Downloaded/signed versioned data:

```text
Register config
Terminal identity
Allowed cashier/session context
Product lightweight projection
Barcode mappings
UOM mappings
Price package/version
Tax offline package/version
Promotion package if supported
Payment method offline capabilities
Receipt template/config
Inventory local hint/last-known availability if allowed
```

---

# 267. Local Package Is Not Source Of Truth

It is an authorized offline snapshot.

Server remains canonical.

---

# 268. Package Version

Every offline sale records:

```text
ConfigPackageVersion
CatalogProjectionVersion
PricingVersion
TaxVersion
PromotionVersion?
DownloadedAt
ValidUntil
```

---

# 269. Expired Package

If safety/legal requirements say expired:

```text
Offline sale blocked
```

or limited to approved modes.

---

# 270. Offline Product Changes

An archived/repriced product may still exist in old package.

Sync conflict policy must detect.

---

# 271. Offline Pricing Risk

Cached price must show:

```text
offline/cached status
package timestamp
```

to authorized cashier/manager where relevant.

---

# 272. Offline Stock

Never claim real-time.

Can display:

```text
Last known 5
Offline — current stock unknown
```

---

# 273. Offline Negative Stock

Server replay may discover insufficient stock.

Policy must define:

```text
allow offline oversell within explicit policy
block stocked offline sales
use local decremented availability estimate
```

per business.

---

# 274. Local Stock Estimate

If used:

```text
last synced availability
- local unsynced sales
+ local unsynced returns if policy
```

only a resilience estimate.

Inventory remains authority.

---

# 275. Offline Transaction Identity

Each locally completed transaction has:

```text
Tenant
LegalEntity
Branch
Register
TerminalDeviceId
OfflineSessionId
OfflineSequence
ClientTransactionId
CreatedLocalInstant
BusinessDate
ConfigVersions
PayloadHash
```

---

# 276. Offline Idempotency

Server unique key prevents duplicate replay.

---

# 277. Offline Sequence

Useful for device-local ordering/evidence.

Not automatically legal fiscal numbering.

---

# 278. Offline Clock Risk

Device time cannot be blindly trusted.

Store:

```text
device time
last trusted server time
monotonic/order metadata
sync time
```

Country fiscal rules may impose stricter behavior.

---

# 279. Offline User Authentication

Need bounded cached authorization/session policy.

Do not store reusable plaintext credentials.

---

# 280. Offline Permissions

Cache signed permission snapshot with expiry.

High-risk actions can remain OnlineOnly.

---

# 281. Offline Manager Override

Only if secure local step-up mechanism exists.

Otherwise restricted.

---

# 282. Offline Cash Sale

Potential flow:

```text
validated cached config
→ local deterministic price/tax package
→ cash tender
→ local sale envelope
→ local receipt
→ queue
→ sync
```

only where legal/provider rules permit.

---

# 283. Offline Card Payment

Provider-specific.

Do not assume.

If provider supports offline authorization/store-and-forward, explicit risk limits and compliance review required.

---

# 284. Offline Returns

Recommended baseline:

```text
OnlineOnly
```

or very restricted.

Reason:

```text
original sale eligibility
prior returns
refund amount
serial history
payment provider
```

can be stale.

---

# 285. Offline Customer Creation

Avoid full legal Party create offline baseline.

Can capture temporary customer details for later reconciliation only if business/legal requirements allow.

---

# 286. Offline Fiscalization

Country Provider decides.

Possible:

```text
local fiscal device
deferred reporting
blocked
```

---

# 287. Sync Queue

Persistent local queue.

Never browser-memory only.

---

# 288. Local Persistence

Use robust local storage appropriate to client technology:

```text
IndexedDB / desktop local DB
```

not volatile React state.

---

# 289. Offline Transaction Encryption

Sensitive local data encrypted where platform/device capability permits.

---

# 290. Sync States

```text
Queued
Uploading
Accepted
Conflict
NeedsReview
Rejected
Synced
```

---

# 291. Server Acceptance

Replay passes:

```text
idempotency
signature/device auth
tenant/entity/register validation
config version validation
price/tax policy
Inventory policy
document/compliance policy
```

---

# 292. Sync Conflict Is Visible

Never silently drop sale.

---

# 293. Conflict Examples

```text
product archived
price package too old
tax package invalid
serial already sold
stock insufficient
session invalid
legal fiscal window expired
```

---

# 294. Conflict Resolution

Must distinguish:

```text
auto-recoverable
manager decision
finance correction
refund/customer resolution
support/legal escalation
```

---

# 295. Offline Sale Already Given To Customer

Cannot simply reject and forget.

Recovery workflow must preserve customer/business reality.

---

# 296. Offline Sync Compensation

If a sale must be corrected server-side:

```text
create explicit correction/return/refund/accounting action
```

not delete local history.

---

# 297. Multi-Device Sync

Each device uses unique identity/sequence.

No global local sequence dependency.

---

# 298. Local Package Update

Online:

```text
download new package
validate hash/signature
activate atomically
retain prior package for unsynced transaction replay/explanation
```

---

# 299. Offline Package Rollback

Unsynced sale retains versions it used.

Do not reinterpret with new package.

---

# 300. Browser Close

Unsynced queue survives.

---

# 301. Terminal Replacement

Old device unsynced queue must be recovered/closed explicitly before identity retired where possible.

---

# 302. Connectivity Indicator

Business requirement:

```text
Online
Degraded
Offline
Syncing
Needs Attention
```

always clear to cashier.

---

# 303. Offline Is Part Of Audit

Sale indicates if created offline.

---

# 304. Store Manager Offline Dashboard

Shows:

```text
Unsynced sales
Oldest unsynced age
Conflict count
Terminals offline
```

---

# 305. Checkout Recovery Queue

Separate from offline queue but unified operations UX can show both.

---

# 306. Concurrency Mission

Multiple terminals/cashiers must sell concurrently without double stock/payment/refund effects.

---

# 307. Cart Concurrency

One active cart typically edited by one session/client.

If resume on another terminal, use optimistic ownership/version.

---

# 308. Sale Finalization Concurrency

Unique Checkout→Sale constraint.

---

# 309. Stock Concurrency

Inventory locks/atomic update.

---

# 310. Refund Concurrency

Payments + POS source return locks prevent over-refund/over-return.

---

# 311. Coupon/Promotion Concurrency

Future usage limits must be transactional in Promotion engine.

---

# 312. Customer Credit Concurrency

Subledger credit check/exposure must account for concurrent commitments according to contract.

---

# 313. Session Close Concurrency

Closing prevents new Checkout atomically.

---

# 314. Manager Override Concurrency

Override tied to exact action/request version.

Cannot reuse PIN approval for changed amount.

---

# 315. POS Numbering

Operational sale reference can use Platform Numbering or scalable device/register-safe scheme.

Legal document numbering remains Invoicing/Compliance.

---

# 316. High-Throughput Numbering

Avoid global table lock/sequence bottleneck where business does not require gapless global number.

---

# 317. Legal Gapless Numbering

Only where country policy requires, through approved Numbering/Compliance mechanism.

---

# 318. Cashier Corrections Before Pay

Editable:

```text
qty
remove line
customer
discount if allowed
```

---

# 319. After Pay But Before Internal Completion

Do not let cashier mutate cart.

Recovery uses frozen checkout snapshot.

---

# 320. Completed Sale Immutable

No line edits.

Return/correction only.



# 321. POS Cashier Roles / Experience

Examples:

```text
Cashier
Senior Cashier
Supervisor
Store Manager
POS Admin
Retail Auditor
Finance Viewer
```

Roles are permission bundles, not hardcoded identities.

---

# 322. Permission Model

Initial vocabulary:

```text
pos.sale.read
pos.sale.create
pos.sale.complete

pos.cart.hold
pos.cart.resume
pos.cart.cancel

pos.price.override
pos.discount.apply
pos.discount.override

pos.customer.select
pos.customer.quick_create

pos.return.create
pos.return.without_receipt
pos.refund.request
pos.exchange.create

pos.session.open
pos.session.close
pos.session.view_other

pos.cash.open_drawer
pos.cash.in
pos.cash.out
pos.cash.drop
pos.cash.count
pos.cash.difference.approve

pos.manager.override

pos.offline.sell
pos.offline.override

pos.report.read
pos.audit.read
pos.settings.manage
pos.terminal.manage
pos.register.manage
```

---

# 323. Inventory Cost Visibility

Separate:

```text
inventory.cost.read
```

Cashier normally does not see product cost/margin.

---

# 324. Customer Financial Visibility

Outstanding/credit requires appropriate Subledger/Sales permissions.

---

# 325. Manager Override

Must validate manager's real permission.

A PIN is authentication mechanism, not authorization source.

---

# 326. Override Scope

Approval tied to:

```text
specific cashier
specific session
specific action
specific amount/discount
specific cart version
short expiry
```

---

# 327. Override Reuse

Do not allow one manager PIN entry to authorize unlimited later actions.

---

# 328. Step-Up Authentication

Optional for:

```text
large discount
price below floor
return without receipt
large refund
large cash difference
manual drawer open
offline high-value sale
```

---

# 329. RLS

All tenant-owned POS tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 330. Runtime DB Role

No ownership/BYPASSRLS.

---

# 331. Branch Scope

Cashier access restricted to authorized Branch/Register.

---

# 332. Register Binding

A user cannot forge RegisterId from another branch.

---

# 333. Terminal Identity

For offline/sensitive operations, terminal registration can use:

```text
device credential
rotatable key/certificate
signed token
```

not a predictable terminal code alone.

---

# 334. Device Revocation

Lost/stolen terminal can be revoked.

Server rejects new sync/auth while preserving old audit.

---

# 335. Secrets

Payment/fiscal provider secrets never in browser/local package.

Use backend/secure terminal integration.

---

# 336. Card Data

No raw PAN/CVV storage/logging.

Payments architecture/PCI review applies.

---

# 337. Local Offline Sensitive Data

Minimize:

```text
customer personal data
payment tokens
tax identifiers
```

offline.

Encrypt and expire packages.

---

# 338. Receipt Customer Data

Only legally/operationally necessary fields.

---

# 339. Audit Critical POS Actions

Capture:

```text
Session open/close
Opening float
Manual price override
Manual discount override
Manager override
Cart cancellation after sensitive event
Return
No-receipt return
Refund request
Cash In/Out/Drop
Drawer no-sale open
Cash difference
Offline sale/sync conflict
Terminal registration/revocation
Settings changes
```

---

# 340. Business History vs Audit

Business history:

```text
what sale/return/payment happened
```

Audit:

```text
who performed/approved/configured it
```

Both.

---

# 341. POS Schema

Recommended:

```text
business_pos
```

---

# 342. PosDbContext

Own only POS tables.

No direct Catalog/Inventory/Payments/Invoicing DbContext access.

---

# 343. Core Tables

Potential:

```text
pos_register
pos_terminal
pos_terminal_registration

pos_session
pos_session_cash_movement
pos_session_count

pos_cart
pos_cart_line

pos_checkout
pos_checkout_tender

pos_sale
pos_sale_line
pos_sale_source_link

pos_return
pos_return_line
pos_exchange_group

pos_manager_override

pos_offline_package_manifest
pos_offline_transaction
pos_sync_attempt
pos_sync_conflict

pos_receipt_projection
pos_register_summary_projection
pos_session_summary_projection
pos_store_daily_projection
```

---

# 344. Tables POS Must Not Own

```text
product
product_variant
uom
price_list
tax_rule
stock_balance
stock_movement
payment
invoice
open_item
journal_entry
party
```

---

# 345. UUIDv7

Use for IDs.

Offline client-generated UUIDv7/approved sortable UUID strategy must avoid collision and preserve idempotency.

---

# 346. Money Precision

FinBase numeric/decimal rules.

---

# 347. Quantity Precision

Catalog UOM decimal rules.

---

# 348. Timestamp Semantics

Use NodaTime:

```text
Instant
LocalDate
DateTimeZone
```

Store branch timezone/business date context where needed.

---

# 349. Row Version

Mutable carts/sessions/checkouts use optimistic concurrency.

---

# 350. Append History

Completed sale/payment/return effects not mutable.

---

# 351. POS Contracts

Locked naming:

```text
Todir.POS.Contracts
```

Use repository-approved capitalization consistently.

---

# 352. Backend Namespaces

Recommended:

```text
Todir.Modules.POS.Domain
Todir.Modules.POS.Application
Todir.Modules.POS.Infrastructure
Todir.POS.Contracts
```

---

# 353. Internal Areas

Potential:

```text
Registers/
Terminals/
Sessions/
Carts/
Checkout/
Sales/
Returns/
Cash/
Offline/
Reporting/
Hardware/
CountryProviders/
```

---

# 354. Cross-Module Contracts

POS consumes:

```text
Todir.Catalog.Contracts
Todir.Pricing.Contracts
Todir.Tax.Contracts
Todir.Inventory.Contracts
Todir.Payments.Contracts
Todir.Invoicing.Contracts
Todir.Directory.Contracts
Todir.Subledger.Contracts
Todir.Ledger.Contracts
```

Only stable public contracts.

---

# 355. Transaction Coordinator

Use for same-database cross-module atomic internal operations where approved.

Do not bypass source module APIs.

---

# 356. No Cross-Module ORM Navigation

Forbidden.

---

# 357. Register Commands

```text
CreatePosRegister
UpdatePosRegisterPolicy
ActivatePosRegister
DeactivatePosRegister
AssignTerminal
```

---

# 358. Terminal Commands

```text
RegisterTerminal
RotateTerminalCredential
RevokeTerminal
HeartbeatTerminal
```

---

# 359. Session Commands

```text
OpenPosSession
RecordOpeningFloat
StartSessionClosing
RecordCashCount
ApproveCashDifference
ClosePosSession
```

---

# 360. Cart Commands

```text
CreateCart
AddCartLine
ScanBarcode
ChangeCartLineQuantity
ChangeCartLineUom
RemoveCartLine
SelectCustomer
ApplyManualDiscount
OverridePrice
HoldCart
ResumeCart
CancelCart
```

---

# 361. Checkout Commands

```text
StartCheckout
ValidateCheckout
AddTender
RemoveUnconfirmedTender
CompleteCashTender
RequestCardTender
RefreshPaymentStatus
CompletePosSale
RecoverPosCheckout
```

---

# 362. Return Commands

```text
StartReturnFromSale
AddReturnLine
SetReturnDisposition
CompleteReturn
RequestRefund
CreateNoReceiptReturn
```

---

# 363. Exchange Command

```text
StartExchange
CompleteExchange
```

application orchestration over Return + new Cart/Sale.

---

# 364. Cash Commands

```text
RecordCashIn
RecordCashOut
RecordCashDrop
OpenDrawerNoSale
```

---

# 365. Offline Commands

```text
DownloadPosOfflinePackage
RegisterOfflineTransaction
SyncOfflineTransactions
ResolveOfflineConflict
```

---

# 366. No Generic `SetSaleStatus`

Use semantic commands.

---

# 367. No Generic `SetStock`

Forbidden.

---

# 368. No Generic `SetPaymentPaid`

Forbidden.

---

# 369. No Generic `SetCashBalance`

Forbidden.

---

# 370. API Base

Recommended:

```text
/api/v1/pos
```

---

# 371. Register APIs

```text
GET  /registers
POST /registers
GET  /registers/{id}
PUT  /registers/{id}
POST /registers/{id}/activate
POST /registers/{id}/deactivate
```

---

# 372. Terminal APIs

```text
POST /terminals/register
POST /terminals/{id}/rotate-credential
POST /terminals/{id}/revoke
POST /terminals/{id}/heartbeat
```

---

# 373. Session APIs

```text
POST /sessions/open
GET  /sessions/current
POST /sessions/{id}/start-close
POST /sessions/{id}/cash-count
POST /sessions/{id}/approve-difference
POST /sessions/{id}/close
```

---

# 374. Cart APIs

```text
POST /carts
GET  /carts/{id}
POST /carts/{id}/scan
POST /carts/{id}/lines
PUT  /carts/{id}/lines/{lineId}
DELETE /carts/{id}/lines/{lineId}
POST /carts/{id}/customer
POST /carts/{id}/discount
POST /carts/{id}/hold
POST /carts/{id}/resume
POST /carts/{id}/cancel
```

---

# 375. Product Lookup API

Prefer lightweight POS composition/query:

```text
GET /catalog/search
GET /catalog/barcode/{barcode}
```

through contract/facade.

Do not return full Product aggregate.

---

# 376. Checkout APIs

```text
POST /carts/{id}/checkout
GET  /checkouts/{id}
POST /checkouts/{id}/tenders
POST /checkouts/{id}/payment-status/refresh
POST /checkouts/{id}/complete
POST /checkouts/{id}/recover
```

---

# 377. Return APIs

```text
POST /sales/{saleId}/returns
POST /returns/{id}/lines
POST /returns/{id}/complete
POST /returns/{id}/refund
```

---

# 378. No-Receipt Return API

Separate high-risk endpoint/command with permissions.

---

# 379. Exchange APIs

May expose one workflow facade, internally preserving separate facts.

---

# 380. Cash APIs

```text
POST /sessions/{id}/cash-in
POST /sessions/{id}/cash-out
POST /sessions/{id}/cash-drop
POST /sessions/{id}/drawer-open
```

---

# 381. Offline APIs

```text
GET  /offline/package
POST /offline/sync
GET  /offline/sync/{batchId}
POST /offline/conflicts/{id}/resolve
```

---

# 382. Reports APIs

```text
GET /reports/session-summary
GET /reports/store-summary
GET /reports/sales
GET /reports/returns
GET /reports/payment-mix
GET /reports/discounts
GET /reports/cash-differences
```

---

# 383. API Idempotency

Mandatory for:

```text
Open Session
Start Checkout
Create Payment Tender
Complete Sale
Recover Checkout
Return
Refund
Exchange
Cash movement imports
Offline Sync
Session Close
```

where retries can occur.

---

# 384. Stable Error Codes

Examples:

```text
pos.session_not_open
pos.session_closing
pos.register_inactive
pos.terminal_revoked

pos.product_not_found
pos.barcode_not_found
pos.uom_invalid
pos.serial_required
pos.serial_unavailable
pos.lot_required

pos.price_missing
pos.price_changed
pos.price_override_forbidden
pos.discount_limit_exceeded
pos.manager_override_required

pos.tax_context_invalid
pos.tax_package_expired

pos.stock_insufficient
pos.stock_conflict

pos.checkout_stale
pos.checkout_already_completed
pos.checkout_recovery_required

pos.payment_pending
pos.payment_failed
pos.payment_amount_mismatch
pos.payment_duplicate_risk

pos.return_quantity_exceeded
pos.return_window_expired
pos.return_without_receipt_forbidden
pos.refund_limit_exceeded

pos.cash_difference_requires_approval

pos.offline_not_allowed
pos.offline_package_expired
pos.offline_sync_conflict
pos.permission_denied
pos.idempotency_conflict
```

---

# 385. Friendly Error — Unknown Payment

> "لم يتم تأكيد نتيجة الدفع بعد. لا تُعد المحاولة الآن؛ TODIR يتحقق من حالة العملية."

---

# 386. Friendly Error — Stock Race

> "الكمية لم تعد متاحة عند تأكيد البيع. راجع الكمية أو اختر بديلًا."

---

# 387. Friendly Error — Price Changed

Show:

```text
Old price
Current price
reason/version if known
```

---

# 388. Friendly Error — Serial Sold

> "تم استخدام هذا الرقم التسلسلي في عملية أخرى."

---

# 389. Friendly Error — Offline Restriction

Explain:

```text
operation requires connection
why
safe next action
```

---

# 390. Read Models

Allowed rebuildable POS projections:

```text
CurrentSessionSummary
RegisterDailySummary
SaleSearchProjection
ReceiptProjection
CashierPerformanceOperationalSummary
OfflineSyncHealth
```

---

# 391. Read Model Is Not Accounting Truth

POS sales report does not replace Ledger revenue.

---

# 392. POS Sales Reporting

Operational:

```text
Gross sales
Discounts
Returns
Net retail sales
Transaction count
Average basket
Items sold
```

clearly defined.

---

# 393. Revenue Terminology

Avoid labeling operational POS sales as accounting revenue where timing/accounting may differ.

Use:

```text
POS Net Sales
```

or precise name.

---

# 394. Payment Mix

```text
Cash
Card
Wallet
Other
```

from Payment/ tender projections.

---

# 395. Sales By Cashier

Operational attribution.

Not employee compensation/commission engine.

---

# 396. Sales By Hour

Useful store operations metric.

---

# 397. Top Products

Uses POS sale lines/Catalog references.

---

# 398. UOM-Aware Quantity Reporting

Do not sum:

```text
2 BOX + 5 PCS
```

as `7`.

Normalize to Base UOM or report entered UOM separately.

---

# 399. Return Rate

Define:

```text
Returned quantity/value
relative to eligible POS sales
```

with period/source clarity.

---

# 400. Discount Report

Show:

```text
automatic/pricing
manual cashier
manager override
promotion
```

separately.

---

# 401. Cash Difference Report

By:

```text
session
cashier
register
branch
```

authorized users only.

---

# 402. Store Manager Dashboard

Focus:

```text
Sales Today
Transactions
Average Basket
Payment Mix
Returns
Discounts
Open Registers
Cash Differences
Offline/Recovery Issues
```

---

# 403. Owner Dashboard Boundary

Accounting/BI can consume POS operational sales.

POS does not duplicate owner financial statements.

---

# 404. Migration

POS onboarding may migrate:

```text
Registers
Terminal mappings
Payment method configuration
Quick product tile layout
Cash opening policy
Historical POS sales summary optional
Open store credits later
```

Product/prices/stock/customer migrate in owning modules.

---

# 405. Historical POS Sale Migration

Not required to fake Inventory/Payment history.

Strategy:

```text
SummaryOnly
DetailedReferenceHistory
NoHistoricalTransactions
```

explicit.

---

# 406. Open Returns During Migration

Need explicit transition policy.

Do not allow return against unverifiable legacy sale without rule.

---

# 407. Existing Gift Cards / Store Credit

If migrated, require dedicated future liability design.

Do not stuff into discount table.

---

# 408. Register Go-Live

Per Register:

```text
GoLiveDate
OpeningSessionPolicy
OfflinePolicy
Receipt/FiscalPolicy
PaymentMethodReadiness
InventoryReadiness
```

---

# 409. POS Readiness

Before activation validate:

```text
Legal Entity / Branch
Register
Warehouse for stocked products
Product Catalog availability
Price context
Tax context
Payment methods
Receipt/compliance
Cash policy
Terminal identity
Permissions
Offline package if enabled
```

---

# 410. Readiness Is Actionable

Show exactly what's missing.

---

# 411. Service-Only Register

No Inventory readiness blocker.

---

# 412. Cash-Only Register

No card terminal blocker.

---

# 413. Non-Tax Company

Tax disabled fast path.

---

# 414. Performance Mission

POS interaction must feel immediate.

---

# 415. Performance Classes

Separate:

```text
Local interaction latency
Server lookup latency
Checkout latency
Provider latency
Sync latency
```

---

# 416. Scan/Add-To-Cart Target

When product/price package is locally available:

```text
perceived response < 50-100ms target
```

measured on target hardware.

---

# 417. Online Barcode Lookup Target

```text
p95 <= 100-200ms
```

under realistic branch load.

---

# 418. Product Search Target

```text
p95 <= 100-250ms
```

server query/page.

---

# 419. Cart Recalculation

Local deterministic preview where safe.

Do not roundtrip server for every `+1` if approved package/context allows local calculation.

---

# 420. Checkout Validation Target

Internal validation:

```text
p95 <= 200-400ms
```

excluding external payment/fiscal providers.

---

# 421. Cash Checkout Target

Aim:

```text
p95 <= 300-700ms internal completion
```

for normal small cart, benchmark-dependent.

---

# 422. Card Checkout

External provider latency separately shown/measured.

Do not hide provider delay as POS calculation delay.

---

# 423. Large Cart

Load test:

```text
100
500
1000 lines
```

for wholesale/counter edge use.

UI may optimize large carts.

---

# 424. No N+1

Checkout cannot call:

```text
Pricing once per line
Tax once per line
Inventory once per line
Catalog once per line
```

Use batch contracts.

---

# 425. Batch Contracts

Required:

```text
Catalog resolve batch
Pricing resolve batch
Tax calculate batch
Inventory availability/issue batch
```

where domain contracts permit.

---

# 426. Local Product Projection

POS read package contains only:

```text
VariantId
SKU
Barcode/UOM
Localized display name
Variant label
UOM metadata
sellability flags
image thumbnail ref optional
```

not full Catalog graph.

---

# 427. Local Price Projection

Versioned POS-relevant Pricing entries/compiled package.

---

# 428. Local Tax Projection

Versioned Country/Tax package.

---

# 429. Cache Invalidation

Online POS receives version change/event and refreshes package safely.

---

# 430. Redis

Not required Day 1.

Local terminal/client package + PostgreSQL/read model first.

---

# 431. Server-Side Cache

Immutable config/version cache allowed.

Never source of truth.

---

# 432. Product Search Scale

Test:

```text
100k
1M variants
```

for large retailer architecture.

---

# 433. Terminal Concurrency Scale

Test many concurrent registers per branch/tenant.

---

# 434. Payment Provider Burst

Test retry/webhook/status checks under peak checkout.

---

# 435. Offline Sync Burst

Store reconnect after hours offline:

```text
thousands of queued sales
```

sync without duplicate effects or API overload.

---

# 436. Sync Throttling

Batch/chunk with backpressure.

---

# 437. Database Indexes

Likely POS-owned:

```text
tenant/branch/register/status
session/register/state/opened_at
sale/register/sale_datetime
sale/session
sale/idempotency_key unique
checkout/cart/idempotency
return/original_sale
cash_movement/session/time
offline/device/sequence unique
sync/state/created_at
```

---

# 438. Source Unique Constraints

Typed unique references to downstream operation IDs where applicable.

---

# 439. Partitioning

Do not partition Day 1.

Measure sale-line growth and retention first.

---

# 440. PostgreSQL First

Operational POS truth remains PostgreSQL.

---

# 441. Read Replica

Reporting may use replica later if freshness semantics clear.

Checkout never depends on replica for authoritative state.

---

# 442. Observability Metrics

Potential:

```text
pos_barcode_lookup_duration
pos_product_search_duration
pos_checkout_validation_duration
pos_cash_checkout_duration
pos_checkout_recovery_count
pos_payment_pending_count
pos_stock_conflict_total
pos_return_duration
pos_cash_difference_total
pos_offline_transaction_count
pos_offline_sync_lag
pos_sync_conflict_count
pos_package_age
```

No customer/product IDs as metric labels.

---

# 443. Checkout Trace

Spans:

```text
Load Cart
Validate Session
Resolve Products/UOM
Resolve Pricing
Resolve Tax
Inventory Precheck
Create Payment Intent
Provider Attempt if any
Internal Completion
Inventory Issue
Document/Compliance
Ledger Fact
Audit
Outbox
Commit
```

---

# 444. Recovery Trace

Must preserve original correlation across provider callback/recovery worker.

---

# 445. Offline Sync Trace

Per batch + transaction correlation.

---

# 446. Logging

Do not log:

```text
card data
full customer personal data
full fiscal payload secrets
entire offline package
```

---

# 447. Alerts

Potential:

```text
Payment success / internal completion pending
High checkout recovery backlog
Offline terminal too long
Offline package expired
Sync conflicts
Large cash difference
Fiscal provider rejection spike
Inventory issue failure spike
```

---

# 448. Admin Support

Support can inspect:

```text
Checkout correlation
Payment refs/state
Inventory operation ref
Document ref
Fiscal ref
Outbox
Offline transaction versions
```

without direct business data edits.

---

# 449. No Direct Database Fix

Never ordinary runbook:

```text
UPDATE pos_sale SET paid=true
UPDATE stock_balance
UPDATE payment
UPDATE cash_balance
```

---

# 450. Recovery Commands Only

Supported semantic recovery.



# 451. Test Strategy — Business First

POS tests must prove retail truth, not just controllers/screens.

---

# 452. Test — Simple Cash Sale

```text
1 stocked product
cash payment
stock issue
receipt
accounting
```

exactly once.

---

# 453. Test — Simple Service Sale

No Inventory call.

---

# 454. Test — Barcode/UOM

Piece and Box barcodes add correct UOM and BaseQuantity.

---

# 455. Test — Variant

Exact color/size variant sold.

---

# 456. Test — Unknown Barcode

No accidental product selection.

---

# 457. Test — Missing Price

Blocked, not zero.

---

# 458. Test — Manual Price Override

Permission/reason/audit exact.

---

# 459. Test — Discount Threshold

Cashier within threshold succeeds.

Above threshold needs manager.

---

# 460. Test — Manager Override Scope

Approval cannot be reused after cart amount/action changes.

---

# 461. Test — Tax Inclusive

Golden math exact.

---

# 462. Test — Tax Exclusive

Golden math exact.

---

# 463. Test — Tax Rule Change During Cart

Checkout revalidates.

---

# 464. Test — Price Change During Cart

Checkout follows configured policy.

---

# 465. Test — Product Deactivated During Cart

Correct block/honor policy.

---

# 466. Test — Inventory Race

Two terminals compete for last unit:

```text
one succeeds
one receives stock conflict
```

---

# 467. Test — Serial Race

One serial cannot sell twice.

---

# 468. Test — Lot Required

Cannot complete without valid lot.

---

# 469. Test — Negative Stock Disallowed

POS cannot bypass Inventory.

---

# 470. Test — Split Tender

```text
400 cash + 600 card = 1000
```

one sale.

---

# 471. Test — Cash Change

Expected drawer movement correct.

---

# 472. Test — Failed Card Then Cash

Only successful cash/card movements count.

---

# 473. Test — Provider Timeout

No duplicate charge; state Pending/Unknown.

---

# 474. Test — Provider Success Response Lost

Retry/recovery returns same Payment and one Sale.

---

# 475. Test — Provider Success / Internal Failure

Checkout becomes RecoveryRequired.

Recovery creates exactly one:

```text
Sale
Inventory Issue
Document
Accounting effect
```

---

# 476. Test — Double Checkout Click

One sale.

---

# 477. Test — Browser Reload During Pay

No second tender.

---

# 478. Test — Cash Sale Transaction Rollback

Force Inventory/Ledger failure in internal transaction.

No partial completed cash sale/payment/stock effect.

---

# 479. Test — Held Cart

No stock/payment effect.

---

# 480. Test — Cancel Cart

No downstream effects.

---

# 481. Test — Session Close During Cart

Checkout blocked/transferred per policy.

---

# 482. Test — Session Cross Midnight

Correct business date/session behavior.

---

# 483. Test — Opening Float

Expected cash exact.

---

# 484. Test — Cash In/Out

Typed movement + reason/audit.

---

# 485. Test — Cash Drop

Drawer expected decreases without fake sale/refund.

---

# 486. Test — Close Session

Expected vs counted exact.

---

# 487. Test — Difference Tolerance

Policy exact.

---

# 488. Test — High Difference Approval

Manager required.

---

# 489. Test — Full Return

Original immutable; stock/payment/document corrections correct.

---

# 490. Test — Partial Return

Cumulative eligible qty exact.

---

# 491. Test — Concurrent Returns

Cannot over-return.

---

# 492. Test — Damaged Return

Returns to QualityHold/Damaged, not Available.

---

# 493. Test — Return Without Refund

No Payment outgoing movement.

---

# 494. Test — Refund Without Physical Return

No Inventory movement.

---

# 495. Test — Card Refund

Original Payment remains; Refund new movement.

---

# 496. Test — Refund Over Limit

Blocked.

---

# 497. Test — No-Receipt Return

Permission/policy enforced.

---

# 498. Test — Exchange Higher Value

Return + new sale + difference payment correct.

---

# 499. Test — Exchange Lower Value

Return + new sale + refund/credit correct.

---

# 500. Test — Serial Return

Same serial identity returns.

---

# 501. Test — Credit Sale

Known customer; correct Invoice/Open Item; no fake immediate payment.

---

# 502. Test — Anonymous Credit

Blocked by default.

---

# 503. Test — Offline Cash Sale

Uses valid package; queued; local receipt policy correct.

---

# 504. Test — Offline Package Expired

Blocked/restricted as policy.

---

# 505. Test — Offline Replay Duplicate

One server sale.

---

# 506. Test — Offline Product Archived

Conflict/recovery visible.

---

# 507. Test — Offline Price Stale

Policy exact.

---

# 508. Test — Offline Tax Invalid

Country policy enforced.

---

# 509. Test — Offline Stock Conflict

No silent drop; recovery case created.

---

# 510. Test — Offline Serial Conflict

NeedsReview; no duplicate serial issue.

---

# 511. Test — Queue Survives App Restart

Unsynced transaction remains.

---

# 512. Test — Device Revoked

New sync rejected with safe operations path.

---

# 513. Test — Cross-Tenant RLS

Tenant A cannot read/sync Tenant B sale.

---

# 514. Test — Cross-Branch

Cashier cannot use unauthorized register.

---

# 515. Test — Cost Visibility

Cashier without inventory cost permission gets no cost data in API.

---

# 516. Test — Customer PII

Anonymous sale stores none unnecessarily.

---

# 517. Test — Payment Data

No PAN/CVV persisted/logged.

---

# 518. Test — Fiscal Required Gate

Country Provider golden flow.

---

# 519. Test — Fiscal Deferred Reporting

If provider permits, sale/recovery semantics exact.

---

# 520. Test — Fiscal Rejection

No silent Accepted state.

---

# 521. Test — Receipt Reprint

Same transaction/document, audited reprint.

---

# 522. Test — 1M Product Search

Measured.

---

# 523. Test — Scan Burst

Scanner can add hundreds of scans with no UI backlog/error.

---

# 524. Test — 500-Line Checkout

No N+1.

---

# 525. Test — Concurrent Terminals

Load branch peak.

---

# 526. Test — Provider Burst

Idempotency/recovery.

---

# 527. Test — Offline Reconnect Burst

Thousands of transactions replay safely.

---

# 528. Property Test — Sale Totals

```text
GrandTotal
=
Subtotal
- Discounts
+ Tax
```

according to Tax/Pricing inclusion semantics.

---

# 529. Property Test — UOM

```text
BaseQuantity
=
EnteredQuantity × UomFactorSnapshot
```

within precision policy.

---

# 530. Property Test — Payment Satisfaction

Completed immediate-payment sale cannot have confirmed tender total below required amount.

---

# 531. Property Test — Return

```text
CumulativeReturnedQty <= SoldQty
```

---

# 532. Property Test — Refund

```text
CumulativeRefund <= RefundableAmount
```

---

# 533. Property Test — Cash Session

Expected cash rebuilds from movements.

---

# 534. Property Test — Idempotency

Repeated same command has one business effect.

---

# 535. Architecture Test — No Catalog Duplication

No Product/UOM master tables in POS.

---

# 536. Architecture Test — No Stock Mutation

No direct Inventory table dependency.

---

# 537. Architecture Test — No Payment Completion Flag Mutation

Use Payments contract.

---

# 538. Architecture Test — No Invoice Duplication

No POS-owned legal invoice engine.

---

# 539. Architecture Test — No Country Fiscal Branching

Provider interface only.

---

# 540. Architecture Test — No Arbitrary Offline Bypass

Offline command must pass explicit capability policy.

---

# 541. Red-Team — Sale Is Just Invoice

Reject.

POS has retail checkout/session/recovery concerns distinct from Invoicing.

---

# 542. Red-Team — Sale Is Just Payment

Reject.

---

# 543. Red-Team — Stock Deducted In POS Table

Reject.

---

# 544. Red-Team — Single `status` Field

Reject.

---

# 545. Red-Team — Payment Success Then Generic Error

Reject if it invites duplicate charge.

Show recovery state.

---

# 546. Red-Team — Retry Creates New Payment

Reject.

---

# 547. Red-Team — Return Deletes Original Sale

Reject.

---

# 548. Red-Team — Refund Edits Original Payment

Reject.

---

# 549. Red-Team — Exchange Edits Original Sale Lines

Reject.

---

# 550. Red-Team — Return Always Adds Available Stock

Reject.

---

# 551. Red-Team — Session = Calendar Date

Reject.

---

# 552. Red-Team — Drawer Balance Mutable Number

Reject.

---

# 553. Red-Team — Offline Means Full Functionality

Reject.

---

# 554. Red-Team — Offline Stock Shown As Real-Time

Reject.

---

# 555. Red-Team — Cached Tax Without Version/Expiry

Reject.

---

# 556. Red-Team — Customer Required For Every Sale

Reject.

---

# 557. Red-Team — Hidden UOM

Reject where ambiguity exists.

---

# 558. Red-Team — Barcode Changes Price Directly

Reject.

Barcode resolves Catalog identity/UOM; Pricing resolves money.

---

# 559. Red-Team — POS Hardcodes Promotions Into Checkout Handler

Reject.

---

# 560. Red-Team — Cashier Sees Product Cost By Default

Reject.

---

# 561. Red-Team — Manager PIN Equals Universal Access

Reject.

---

# 562. Red-Team — Fiscal Provider Failure Deletes Sale

Reject.

Use provider-specific state/recovery.

---

# 563. UI Guidance Comes After Business Model

UI requirements are implementation guidance, not domain ownership.

---

# 564. Input-Mode Requirement

Same POS must support:

```text
Touch
Mouse
Keyboard
Barcode scanner
Mixed use
```

---

# 565. No Hover-Only Action

Primary functionality available without hover.

---

# 566. Touch Targets

Frequent actions sized for touch.

Exact pixels belong Design System/device testing.

---

# 567. Adaptive Density

Touch mode:

```text
larger controls
less dense
```

Desktop:

```text
more visible data
compact optional
```

Same workflow.

---

# 568. Barcode-First Flow

Normal heavy-scan cashier:

```text
Scan
Scan
Scan
Pay
```

without repeated mouse clicks.

---

# 569. Focus Management

After scan/add:

scanner/search input remains ready where workflow allows.

---

# 570. Keyboard Shortcuts

Configurable examples:

```text
Search
Customer
Hold
Discount
Payment
Confirm
Back
Qty
```

Do not make keyboard required.

---

# 571. Cart Visibility

During sale, always easy to see:

```text
Product
Variant
Qty
UOM
Unit price
Discount
Line total
Subtotal
Tax
Grand Total
```

---

# 572. Product Line Clarity

Never display only:

```text
Product A    2 × 20
```

when Variant/UOM ambiguity exists.

---

# 573. Checkout Visibility

Before payment:

```text
Total due
Payment methods
Tender status
Remaining due
Change
```

obvious.

---

# 574. Offline Visibility

Always prominent:

```text
Offline
Cached prices as of...
Unsynced transactions...
```

without blocking unrelated safe actions.

---

# 575. Recovery Visibility

Payment-success recovery must be impossible to mistake for failed unpaid cart.

---

# 576. Manager Override UX

Inline fast step-up.

---

# 577. Customer Display

Optional separate display.

Same authoritative cart values.

---

# 578. Accessibility

Keyboard navigation, focus states, readable contrast, scalable text and non-color-only status cues.

---

# 579. Arabic / RTL

Full RTL POS experience.

Numeric/price columns remain readable and aligned.

---

# 580. Bilingual Product Names

Display configured preferred language; quick alternate-language search.

---

# 581. Cashier Acceptance Test — Touch Only

User must complete:

```text
open session
scan/search products
change qty
select UOM
discount with approval
cash payment
card payment
split payment
hold/resume
return
close session
```

without mouse/keyboard.

---

# 582. Cashier Acceptance Test — Mouse Only

Same core tasks efficiently.

---

# 583. Cashier Acceptance Test — Keyboard + Scanner

Power user completes normal sales primarily by scan/shortcuts.

---

# 584. Cashier Acceptance Test — Mixed

Switching input modes never corrupts focus/cart.

---

# 585. Store Manager Acceptance

Manager can:

```text
see open sessions
approve override
review returns
see cash differences
see offline/recovery issues
close/reconcile operations
```

without developer support.

---

# 586. Finance Acceptance

Finance can reconcile:

```text
POS payment totals
Payments
Cash session movement
Invoices/credit notes
Ledger postings
```

with no unexplained differences.

---

# 587. Inventory Acceptance

Inventory can reconcile:

```text
POS sold quantities
Inventory Issues
Returns
Serial/Lot history
```

exactly.

---

# 588. Compliance Acceptance

Country specialist validates:

```text
document type
tax
receipt/fiscal lifecycle
offline behavior
correction/return
```

for enabled market.

---

# 589. Agent Implementation Philosophy

The Agent must implement POS as **business slices**, not screen slices.

Wrong:

```text
build cashier screen
then wire backend later
```

Correct:

```text
prove one end-to-end business sale
then optimize UI
```

---

# 590. Agent Precedence

If conflict:

```text
20 POS pack
→ owning domain pack (Catalog/Pricing/Tax/Payments/Inventory/Invoicing/Ledger)
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ Country Pack
→ approved ADR
```

POS cannot override owning-domain invariants for speed.

---

# 591. Agent Must Read Before POS

At minimum:

```text
01
02
03
04
05
06
07
08
09
10
11
12
13
14
20
all AGENTS.md
approved ADRs
```

---

# 592. Agent Baseline Report

Before implementation report:

```text
Catalog barcode/UOM contracts
Pricing batch resolver
Tax batch contract
Inventory issue/return contracts
Payments request/attempt/recovery
Invoicing source finalization
Ledger fact integration
Transaction Coordinator
RLS
Files
Audit
Outbox
Country Pack/Compliance
```

---

# 593. Agent Stop Rule

Escalate only unresolved architecture/owner decision.

Continue unaffected work.

---

# 594. Agent Task Template

Every implementation phase:

```text
TASK

BUSINESS GOAL
Which real store problem this solves.

REAL-WORLD WORKFLOW
Happy path + exceptions.

SOURCE OF TRUTH
Which domain owns each fact.

STATE / INVARIANTS
What can/cannot happen.

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

DATABASE REQUIREMENTS

INTEGRATION REQUIREMENTS

OFFLINE REQUIREMENTS

SECURITY REQUIREMENTS

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
- performance evidence
- failure/recovery evidence
- security evidence
- unresolved decisions
- risks
```

---

# 595. Implementation Sequence

```text
PS00 Repository / POS Dependency Verification
PS01 POS Module Boundary + RLS
PS02 Register / Terminal Model
PS03 Session / Cash Drawer Foundation
PS04 Cart Domain
PS05 Catalog / Barcode / UOM Integration
PS06 Pricing Integration
PS07 Tax Integration
PS08 Checkout State Machine
PS09 Cash Payment E2E
PS10 Inventory Issue E2E
PS11 Receipt / Invoicing E2E
PS12 Ledger / Accounting E2E
PS13 Card / Provider Payment E2E
PS14 Checkout Recovery / Idempotency
PS15 Split Tender
PS16 Cash Movements + Session Reconciliation
PS17 Returns
PS18 Refunds
PS19 Exchanges
PS20 Lot / Serial / Weighted Goods
PS21 Known Customer + Quick Customer
PS22 Credit Sale
PS23 Manager Override / Discount Controls
PS24 Country Fiscal / eReceipt Provider
PS25 Hardware Adapter Layer
PS26 Offline Package Architecture
PS27 Offline Cash Sale
PS28 Offline Sync + Conflict Recovery
PS29 Multi-Terminal Concurrency
PS30 Operational Reporting
PS31 Security / Privacy / PCI
PS32 Performance / Load
PS33 Migration / Go-Live
PS34 Runbooks / Recovery
PS35 Touch Cashier Acceptance
PS36 Mouse/Keyboard/Scanner Acceptance
PS37 Store Manager Acceptance
PS38 Finance/Inventory Reconciliation Acceptance
PS39 Country Compliance Acceptance
PS40 Cross-Module End-to-End Acceptance
PS41 Release Gate
```

---

# 596. PS00 — Goal

Prove existing domain contracts before writing POS business logic.

---

# 597. PS00 Exit Gate

Exact dependency/gap matrix.

No duplicated source of truth planned.

---

# 598. PS01 — Goal

Create one POS module with architecture boundaries.

---

# 599. PS01 Implement

```text
Todir.Modules.POS.Domain
Todir.Modules.POS.Application
Todir.Modules.POS.Infrastructure
Todir.POS.Contracts
business_pos
PosDbContext
manifest
RLS
architecture tests
```

---

# 600. PS01 Exit Gate

Module cannot directly read/write owning domain DbContexts.

---

# 601. PS02 — Goal

Represent real store hardware/operation context.

---

# 602. PS02 Implement

Register, Terminal, device registration, branch/default policies.

---

# 603. PS02 Exit Gate

Hardware replacement does not rewrite transaction history.

---

# 604. PS03 — Goal

Correct cashier session/cash accountability.

---

# 605. PS03 Implement

```text
Open Session
Opening Float
Cash movements
Closing count
Expected cash
Difference
Close
```

---

# 606. PS03 Exit Gate

Expected cash rebuilds from movements; session not tied to date.

---

# 607. PS04 — Goal

Editable, holdable retail Cart with no financial side effects.

---

# 608. PS04 Exit Gate

Hold/cancel produces no Payment/Inventory/Ledger effect.

---

# 609. PS05 — Goal

Fast exact Product/Variant/UOM/Barcode behavior.

---

# 610. PS05 Exit Gate

Piece/Box/Variant/Serial scenarios pass.

---

# 611. PS06 — Goal

Use canonical Pricing.

---

# 612. PS06 Exit Gate

Missing price/override/discount/version snapshot correct.

---

# 613. PS07 — Goal

Use canonical Tax.

---

# 614. PS07 Exit Gate

Inclusive/exclusive golden vectors + version snapshot.

---

# 615. PS08 — Goal

Create recoverable Checkout state machine before provider integration.

---

# 616. PS08 Exit Gate

Business states separate and restart-safe.

---

# 617. PS09 — Goal

First real end-to-end Cash Sale.

---

# 618. PS09 Required

```text
Cart
→ validation
→ cash Payment
→ POS Sale
```

with idempotency.

---

# 619. PS09 Exit Gate

Double-click creates one Payment/Sale.

---

# 620. PS10 — Goal

Add Inventory Issue correctly.

---

# 621. PS10 Exit Gate

Stock movement source-linked exactly once; service line bypasses.

---

# 622. PS11 — Goal

Correct legal receipt/invoice integration.

---

# 623. PS11 Exit Gate

Sale document is source-linked and immutable; POS not duplicate invoice engine.

---

# 624. PS12 — Goal

Accounting integration.

---

# 625. PS12 Exit Gate

Ledger postings reconcile to POS economic facts through owning modules.

---

# 626. PS13 — Goal

Provider/card payment without distributed transaction assumptions.

---

# 627. PS13 Exit Gate

Timeout/success/failure states handled without duplicate charge.

---

# 628. PS14 — Goal

Make payment-success/internal-failure safe.

---

# 629. PS14 Build

Recovery queue + idempotent completion.

---

# 630. PS14 Exit Gate

Forced internal failure after provider success recovers exactly once.

---

# 631. PS15 — Goal

Multiple tenders.

---

# 632. PS15 Exit Gate

Cash/card split, failed tender replacement, change all correct.

---

# 633. PS16 — Goal

Production cash drawer/session reconciliation.

---

# 634. PS16 Exit Gate

Cash movements/count/difference/approval reconcile.

---

# 635. PS17 — Goal

Physical/commercial returns.

---

# 636. PS17 Exit Gate

Partial/full/damaged/serial return exact.

---

# 637. PS18 — Goal

Refund integration.

---

# 638. PS18 Exit Gate

Refund separate from return and cannot over-refund.

---

# 639. PS19 — Goal

One simple Exchange UX over correct facts.

---

# 640. PS19 Exit Gate

Original sale unchanged; return/new sale/payment difference traceable.

---

# 641. PS20 — Goal

Advanced product traceability without burdening normal goods.

---

# 642. PS20 Exit Gate

Lot/Serial/weight requirements appear only when product requires.

---

# 643. PS21 — Goal

Optional known customer.

---

# 644. PS21 Exit Gate

Walk-in still fastest path; Directory duplicates prevented.

---

# 645. PS22 — Goal

Optional customer credit.

---

# 646. PS22 Exit Gate

Known Party/Open Item/Credit Check exact; anonymous credit blocked.

---

# 647. PS23 — Goal

Fast risk controls.

---

# 648. PS23 Build

```text
discount thresholds
price override
return without receipt
cash difference
inline manager authorization
```

---

# 649. PS23 Exit Gate

No long workflow; no security bypass.

---

# 650. PS24 — Goal

Country fiscal/eReceipt seam.

---

# 651. PS24 Exit Gate

At least one test provider proves synchronous/deferred strategy without country branching in Core.

---

# 652. PS25 — Goal

Hardware abstraction.

---

# 653. PS25 Exit Gate

Printer/scanner/drawer/customer display/payment terminal adapters cannot alter domain truth directly.

---

# 654. PS26 — Goal

Build safe versioned offline package.

---

# 655. PS26 Exit Gate

Package signed/versioned/expiring; no live secrets.

---

# 656. PS27 — Goal

First controlled Offline Cash Sale.

---

# 657. PS27 Exit Gate

App restart preserves queue and customer can receive allowed local receipt.

---

# 658. PS28 — Goal

Sync/recover offline transactions.

---

# 659. PS28 Exit Gate

Duplicate replay safe; all conflict classes visible.

---

# 660. PS29 — Goal

Prove store concurrency.

---

# 661. PS29 Exit Gate

Last-stock/serial/return/session races tested.

---

# 662. PS30 — Goal

Operational POS reports.

---

# 663. PS30 Exit Gate

Reports reconcile to POS sale/payment projections and use precise terminology.

---

# 664. PS31 — Goal

Security/privacy/PCI hardening.

---

# 665. PS31 Exit Gate

RLS, branch scope, manager auth, card-data tests pass.

---

# 666. PS32 — Goal

Prove speed.

---

# 667. PS32 Benchmarks

```text
scan/add latency
product search
cash checkout
large carts
many terminals
provider burst
offline sync burst
```

---

# 668. PS32 Exit Gate

Measured targets pass or evidence-based optimization ADR.

---

# 669. PS33 — Goal

Safe store migration/go-live.

---

# 670. PS33 Exit Gate

Register/payment/inventory/tax/fiscal readiness approved before first live sale.

---

# 671. PS34 — Goal

Operational recovery without direct DB manipulation.

---

# 672. PS34 Runbooks

Required:

```text
Unknown payment
Payment success / internal failure
Inventory conflict
Fiscal rejection
Printer failure
Stuck checkout
Duplicate scan/source
Cash difference
Session cannot close
Offline package expired
Offline conflict
Device lost/revoked
Return/refund disagreement
Projection rebuild
DR restore validation
```

---

# 673. PS34 Exit Gate

Every critical scenario has semantic recovery.

---

# 674. PS35 — Touch Cashier Acceptance

Independent cashier uses touch-only device.

---

# 675. PS35 Pass

Normal sale can be completed quickly without training on ERP internals.

---

# 676. PS36 — Mouse / Keyboard / Scanner Acceptance

Power cashier proves:

```text
scan-heavy
keyboard-heavy
mouse-heavy
mixed
```

flows.

---

# 677. PS36 Pass

No input mode is second-class.

---

# 678. PS37 — Store Manager Acceptance

Manager handles:

```text
sessions
overrides
returns
cash differences
recovery
offline status
```

without developer.

---

# 679. PS38 — Finance / Inventory Acceptance

Reconcile:

```text
POS sales
Payments
Inventory
Invoices/Credit Notes
Ledger
Cash Sessions
```

zero unexplained difference.

---

# 680. PS39 — Country Compliance Acceptance

Country specialist validates enabled POS fiscal/tax behavior.

---

# 681. PS40 — Cross-Module E2E

Scenarios:

```text
Cash sale
Card sale
Split tender
Credit sale
Return/refund
Exchange
Serial sale/return
Offline sale/sync
Fiscal recovery
```

real PostgreSQL integration tests.

---

# 682. PS41 — Release Gate

POS ships only after business trust and operational speed are proven.

---

# 683. Required Sign-Off

```text
Product Architect
Retail Operations Lead
Store Manager/Cashier Reviewer
Payments Lead
Inventory Lead
Tax/Compliance Specialist
Finance/Accounting Lead
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 684. Mandatory Evidence

```text
Automated test report
Payment idempotency/recovery evidence
Inventory concurrency evidence
Return/refund reconciliation
Cash session reconciliation
Offline replay/conflict evidence
Country compliance vectors
RLS/security/PCI report
Performance benchmark
Cashier acceptance
Store manager acceptance
Finance/inventory reconciliation
Known limitations
```

---

# 685. Build Now — MUST

```text
Register
Terminal
Session
Cash drawer
Cart
Barcode
Variant/UOM
Pricing
Tax
Cash checkout
Card/provider checkout
Split tender
Inventory issue
Receipt/invoice handoff
Accounting integration
Hold/resume
Returns
Refunds
Exchange
Lot/serial/decimal quantity
Known customer optional
Cash reconciliation
Manager overrides
Recovery/idempotency
Offline architecture
Offline cash capability where allowed
Sync/conflicts
Permissions
Audit
Reports
Performance
Runbooks
Acceptance gates
```

---

# 686. Build Later / Evidence-Based

```text
Restaurant tables/KDS
Advanced promotion engine
Loyalty
Gift cards
Store credit
Coupons
Self-checkout
Kiosk
Click & Collect
Cross-channel returns
Advanced scales
Advanced fiscal hardware markets
Offline card store-and-forward
Complex layaway
Advanced commissions
Retail workforce scheduling
AI cashier assistant
```

---

# 687. Fresh Small Store Defaults

Recommended:

```text
One Register
One Main Warehouse
Simple Session
Opening Float optional
Anonymous Walk-In ON
Known Customer optional
Cash + configured Card
Simple Pricing
Tax automatic
Manual discount limited
Manager override inline
Hold/Resume ON
Returns with receipt ON
No-receipt return OFF
Offline OFF until explicitly configured/validated
Advanced Promotions OFF
Loyalty OFF
Credit Sale OFF
```

---

# 688. Larger Retailer Capabilities

Enable as needed:

```text
Multiple registers
Multiple terminals
Multiple cashiers
Advanced manager controls
Offline packages
Multiple payment methods
Complex returns
Customer credit
Promotion engine
Customer display
Fiscal devices
Large-scale reporting
```

---

# 689. Final Source-of-Truth Matrix

```text
Product / Variant / UOM / Barcode   → Catalog
Price / commercial discount         → Pricing
Promotion                            → Retail Promotion capability
Tax                                  → Tax
Stock / Lot / Serial / Cost          → Inventory
Customer identity                    → Directory
POS Cart / Checkout / Session        → POS
Actual Payment                       → Payments
Invoice / Credit Note                → Invoicing
Open Receivable                      → Subledger
Fiscal/eReceipt state                → Country/Compliance
Journal Entry                        → Ledger
```

---

# 690. Final Normal Sale Flow

```text
Open Session
→ Scan / Search
→ Cart
→ Validate Price / Tax / Stock
→ Payment
→ Complete Sale
→ Inventory / Document / Accounting
→ Receipt
```

---

# 691. Final External Payment Recovery Flow

```text
Checkout intent
→ Payment provider
→ Provider success
→ Internal completion
   ├ success → Completed
   └ failure → RecoveryRequired
                ↓
          Idempotent recovery
                ↓
            Completed
```

Never charge twice.

---

# 692. Final Return Flow

```text
Original immutable Sale
→ Select return qty
→ Validate policy
→ Physical disposition
→ Inventory Return if applicable
→ Credit correction if required
→ Refund if applicable
```

---

# 693. Final Exchange Flow

```text
Original Sale
→ Return
+
New Sale
→ payment/refund difference
```

One easy UX, correct separate facts.

---

# 694. Final Cash Session Flow

```text
Opening Float
+ Cash Sales
+ Cash In
- Cash Refunds
- Cash Out/Drops
= Expected Cash

Counted Cash
- Expected Cash
= Difference
```

Operational truth; not GL balance.

---

# 695. Final Offline Flow

```text
Versioned Offline Package
→ Allowed local sale
→ Durable local queue
→ Idempotent server sync
→ Conflict/recovery if needed
```

Never silently discard.

---

# 696. Final Business Rule

> **POS speed is achieved through orchestration, cached/versioned read packages, fast input and resilient recovery—not by duplicating or bypassing Catalog, Pricing, Tax, Inventory, Payments, Invoicing or Ledger.**

---

# 697. Final UOM Rule

> **The cashier and customer must never be left guessing whether a quantity means Piece, Box, Kg, Meter or another UOM; entered UOM and conversion snapshot are permanent sale facts.**

---

# 698. Final Payment Rule

> **A payment provider timeout is not a failed payment, and a retry must never create a second charge.**

---

# 699. Final Stock Rule

> **POS never changes stock directly; every physical sale/return reaches Inventory through a typed, idempotent operation.**

---

# 700. Final Return Rule

> **A Return changes product disposition; a Refund moves money. They may occur together, but they are not the same fact.**

---

# 701. Final Session Rule

> **Cashier session operational cash is rebuilt from movements and counts; it is never treated as the company's Ledger cash balance.**

---

# 702. Final Offline Rule

> **Offline is an explicitly bounded business capability, not a promise that every online function remains valid without connectivity.**

---

# 703. Final Country Rule

> **Retail fiscal/eReceipt requirements come through versioned Country/Compliance providers; POS Core does not guess local law.**

---

# 704. Final UX Rule

> **The POS screen is the shortest safe path through an already-correct business model. It must never define the business model itself.**

---

# 705. Final Product Verdict

TODIR POS is approved only when:

```text
Walk-in sale does not require customer master.
Barcode resolves exact Variant/UOM.
Entered UOM is visible and historically preserved.
Missing price never becomes zero.
Tax comes only from Tax Engine.
Stock moves only through Inventory.
Payment truth comes only from Payments.
Invoices/credit notes remain Invoicing-owned.
Fiscal state is separate from commercial/payment state.
One checkout retry cannot duplicate charge/sale/stock/document.
Provider success/internal failure is recoverable.
Cash/session balances rebuild from movements.
Returns never rewrite original sale.
Returns and refunds remain separate.
Exchange preserves return + new sale truth.
Serial/Lot concurrency is safe.
Offline sales use explicit versioned packages and durable replay.
Offline stale stock is never presented as real-time.
Small stores are not forced through advanced workflows.
Large retailers can enable controls without replacing POS Core.
Touch, mouse, keyboard and barcode users can all operate efficiently.
Finance, Inventory and Payments reconcile with zero unexplained differences.
```

---

# 706. Final Agent Rule

For every POS implementation decision, the Agent must answer:

> **What happened in the real store, which domain owns each resulting fact, what must be atomic, what can fail independently, how is a retry made safe, and how does the system recover if the cashier/customer already experienced an irreversible real-world action?**

If those answers are unclear, the POS implementation is incomplete.

---

# 707. No Separate POS Subfeature Packs

Do **not** create separate architecture packs for:

```text
Cashier Session
Cash Drawer
Checkout
Returns
Refunds
Exchange
Offline POS
POS Hardware
POS Reports
```

They are all part of `20 POS`.

Restaurant-specific functionality may later be a clearly scoped capability because its business model is materially different.

---

# 708. Next Planning Rule

Continue one comprehensive file per actual business module.

After POS, choose the next module based on MVP/business priority rather than creating documents only to complete a list.

---

**END OF FILE — 20_ERP_SAAS_POS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
