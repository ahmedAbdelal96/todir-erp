# 11 — TODIR ERP SaaS Invoicing Requirements & Technical Design Pack

**File:** `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + BUSINESS DOCUMENT + FINANCIAL INTEGRATION + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Product Architect, Invoicing Lead, Accounting Lead, Tax Lead, Compliance Lead, Backend/Frontend Leads, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL** — invoices are financial/legal business documents that drive receivables/payables, accounting, tax, compliance, customer/supplier statements and collections.

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
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR Invoicing owns the **financial document lifecycle and immutable invoice snapshot**.

It does **not** own Pricing, Tax, Currency/FX, AR/AP settlement, Ledger posting or government submission logic.

Canonical flow:

```text
Directory / Catalog
        ↓
Pricing
        ↓
Tax
        ↓
FinBase
        ↓
Invoicing Finalization
        ↓
Immutable Invoice Snapshot
      ┌───────────┬───────────┐
      ▼           ▼           ▼
Subledger       Ledger     Compliance
Open Items       Facts      Submission
```

Core rule:

> **Invoicing orchestrates the financial document. Each specialized domain remains the source of truth for its own decision.**

---

# 1. Central Business Question

The Invoicing domain must answer:

> **How does TODIR create, calculate, validate, finalize, issue, correct and track a legally meaningful invoice while preserving exact historical prices, tax, party data, payment terms and accounting links without duplicating the logic of other domains?**

---

# 2. Product Promise

For a small company, creating an invoice should feel like:

```text
Customer
Items
Qty
Price
Tax
Total
Due
[Finalize]
```

Behind the scenes TODIR preserves:

- party snapshot;
- product/UOM snapshot;
- pricing evidence;
- tax evidence;
- currency/FX context;
- payment schedule;
- accounting fact;
- Subledger obligations;
- regulatory state;
- audit.

---

# 3. Invoicing Ownership

Invoicing owns:

```text
Invoice
Invoice Line
Invoice Header
Invoice Document Type
Invoice Lifecycle
Document Number
Document Relationships
Party/Address Snapshot
Product/UOM Display Snapshot
Final Commercial Amount Snapshot
Final Tax Snapshot Embedding
Payment Schedule Snapshot
Invoice Totals
Invoice Corrections
Credit/Debit Notes
Attachments References
Invoice Delivery Metadata
Document Rendering Input Contract
```

---

# 4. Invoicing Does NOT Own

```text
Party identity               → Directory
Product master               → Catalog
Current price                → Pricing
Tax rules                    → Tax
Currency/FX master           → FinBase
Payment movement             → Payments
Open items / paid balance    → Subledger
Journal Entry                → Ledger
Government submission        → Compliance
Email/SMS/WhatsApp transport → Notifications/Delivery
File bytes                    → Platform Files
```

---

# 5. Why Invoicing Must Not Become a God Module

A common ERP failure is placing inside Invoice:

```text
pricing logic
tax calculation
customer balance
payment application
GL account mapping
government API integration
```

This creates:

- duplicated logic;
- contradictory totals;
- impossible upgrades;
- country forks;
- slow screens;
- accounting drift.

TODIR explicitly prevents this.

---

# 6. Canonical Invoice Directions

Initial financial directions:

```text
Receivable
Payable
```

Business-facing labels:

```text
Customer Invoice
Supplier Invoice / Vendor Bill
```

---

# 7. Shared Foundation, Different Workflows

Customer and supplier invoices can share:

- monetary model;
- lines;
- tax snapshots;
- payment schedules;
- lifecycle primitives;
- correction references.

But upstream workflows differ.

Sales owns selling workflow.

Purchasing owns buying/matching workflow.

---

# 8. Customer Invoice

Represents an amount owed by a customer to a Legal Entity.

Subledger effect:

```text
AR Debit Open Item(s)
```

according to `10`.

---

# 9. Supplier Invoice / Vendor Bill

Represents an amount owed by a Legal Entity to a supplier.

Subledger effect:

```text
AP Credit Open Item(s)
```

according to `10`.

---

# 10. Invoice Is a Financial Document, Not a Sales Order

A Sales Order can exist without accounting liability/receivable.

An Invoice is a financial document that normally creates accounting/subledger consequences when finalized/posted according to policy.

---

# 11. Invoice Is Not a Payment

Invoice does not move cash/bank.

---

# 12. Invoice Is Not a Settlement

Paid/remaining status comes from Subledger.

---

# 13. Invoice Is Not a Tax Engine

It consumes `TaxResult` and freezes the result.

---

# 14. Invoice Is Not a Pricing Engine

It consumes `PriceResolution`/override result and freezes it.

---

# 15. Invoice Aggregate

Core aggregate:

```text
Invoice
 ├── Header
 ├── Lines
 ├── PartySnapshot
 ├── AddressSnapshots
 ├── PricingSnapshots
 ├── TaxSnapshots
 ├── Charges/Discount Allocations
 ├── Totals
 ├── PaymentScheduleSnapshot
 ├── Relationships
 └── Lifecycle State
```

---

# 16. Invoice Identity

Internal:

```text
UUIDv7 InvoiceId
```

Human/legal:

```text
DocumentNumber
```

These are separate.

---

# 17. Draft Identity

Draft can exist without legal Document Number.

Use internal InvoiceId plus optional Draft Reference.

---

# 18. Legal Number Assignment

Legal/customer-facing sequence is assigned at configured finalization/issue point.

Do not use legal number as database PK.

---

# 19. Document Numbering

Use Platform Numbering.

Never:

```text
MAX(invoice_no) + 1
```

---

# 20. Numbering Scope

Country Pack/Entity policy may define sequence scope:

```text
Legal Entity
Document Type
Branch
Fiscal Year
Series
```

---

# 21. Numbering Idempotency

A retry/double click of finalization cannot allocate a second legal number.

---

# 22. Number Gaps

Whether a legal sequence permits gaps is jurisdiction/policy dependent.

TODIR must record reservation/cancellation semantics rather than silently reuse numbers.

---

# 23. Number Reservation State

Potential:

```text
Reserved
Assigned
Voided
```

owned by Platform Numbering with Invoicing reference.

---

# 24. Invoice Document Types

Generic initial types:

```text
CustomerInvoice
SupplierInvoice
CustomerCreditNote
SupplierCreditNote
CustomerDebitNote
SupplierDebitNote
```

---

# 25. Non-Posting Commercial Documents

Potential:

```text
ProForma
PaymentRequest
```

must be explicitly marked non-accounting if supported.

Do not reuse "Invoice" semantics casually.

---

# 26. Tax Invoice / Simplified Invoice

These are Country Pack/Tax/Compliance classifications.

Core Invoicing stores `RegulatoryDocumentType` snapshot/code.

---

# 27. Invoice Lifecycle

Recommended minimal document lifecycle:

```text
Draft
Finalized
CorrectedOrCancelled
```

Detailed regulatory/accounting/settlement states remain separate.

---

# 28. Why Keep Lifecycle Small

Avoid one status enum containing:

```text
Draft
Posted
Paid
Sent
Cleared
Overdue
Rejected
```

These belong to different state machines.

---

# 29. State Dimensions

Invoice UI composes:

```text
DocumentLifecycle
AccountingState
RegulatoryState
SettlementState
DeliveryState
```

---

# 30. Example

```text
Document      = Finalized
Accounting    = Posted
Regulatory    = Cleared
Settlement    = PartiallyPaid
Delivery      = Sent
```

---

# 31. Accounting State Authority

Ledger.

Invoice stores/refers to summary projection for UX only.

---

# 32. Settlement State Authority

Subledger.

---

# 33. Regulatory State Authority

Compliance.

---

# 34. Delivery State Authority

Notification/Document Delivery system.

---

# 35. Draft

Draft is editable business workspace.

---

# 36. Draft Can Change

Subject to permissions:

- Party.
- lines.
- quantities.
- descriptions.
- payment term.
- addresses.
- commercial discounts.
- price overrides.
- dates.
- notes.

---

# 37. Draft Is Not Financial History

No canonical AR/AP open items.

No immutable Ledger entry.

No legal final snapshot.

---

# 38. Draft Preview

Can call:

```text
Pricing
Tax
Payment Term calculator
Ledger Posting Preview
```

without committing final financial truth.

---

# 39. Draft Autosave

Allowed.

Must not trigger finalization.

---

# 40. Draft Concurrency

Optimistic concurrency:

```text
row_version / ETag
```

prevents silent overwrite.

---

# 41. Finalized Invoice

Finalization freezes all legally/financially relevant values.

---

# 42. Financial Immutability

After finalization, these cannot be edited in place:

```text
Party legal snapshot
Currency
Lines
Qty
UOM snapshot
Price snapshots
Discount allocation
Tax snapshots
Taxable bases
Tax amounts
Totals
Payment schedule
Core dates where legally/accounting relevant
```

---

# 43. Metadata Mutability

Some non-financial metadata may remain editable if safe:

```text
internal tags
internal note
collection owner
```

but not document facts.

---

# 44. Finalized Does Not Automatically Mean Regulatory Issued

Country Pack can require compliance clearance/reporting.

---

# 45. Finalized Does Not Automatically Mean Paid

Subledger.

---

# 46. Finalized Does Not Automatically Mean Sent

Delivery.

---

# 47. Finalization Idempotency

Command:

```text
FinalizeInvoice(commandId)
```

must safely return existing finalization result on retry.

---

# 48. Finalization Lock

Concurrent finalization:

only one succeeds/returns same result.

No duplicate:

- numbers;
- Open Items;
- Journal postings;
- compliance jobs.

---

# 49. Source Idempotency

When creating Invoice from Sales/Purchasing/API, use upstream source identity.

Example:

```text
SourceModule
SourceDocumentId
InvoicePurpose
```

unique where business semantics demand.

---

# 50. Direct Invoice

TODIR supports:

```text
Create Invoice
```

without Sales Order.

Critical for:

- service companies;
- accountants;
- ad-hoc billing;
- small businesses.

---

# 51. Sales-Origin Invoice

Sales can request:

```text
CreateInvoiceFromSalesOrder
```

Invoicing creates Invoice using explicit source lines/snapshots.

---

# 52. Purchasing-Origin Invoice

Purchasing can request Supplier Invoice / Bill creation from:

- Purchase Order.
- Receipt.
- external supplier bill.

---

# 53. POS-Origin Invoice

POS can request Invoice from completed sale.

POS keeps POS workflow ownership.

---

# 54. Subscription/Contract-Origin Invoice

Future upstream modules can produce Invoice requests using stable contract.

---

# 55. Invoice Relationship

Typed relationship:

```text
OriginatesFrom
Corrects
Credits
Debits
Replaces
RelatedToDelivery
RelatedToReceipt
```

---

# 56. No Generic Relationship Text

Use typed links + external reference text separately.

---

# 57. Header Core Fields

Concept:

```text
Invoice
{
    Id
    TenantId
    LegalEntityId
    BranchId?

    Direction
    DocumentType
    RegulatoryDocumentType?

    Number?
    Series?

    PartyId
    PartySnapshot

    DocumentDate
    IssueDate?
    PostingDate
    TaxDate
    SupplyDate?

    Currency
    BaseCurrencyContext?

    PriceMode

    PaymentTermSnapshot
    PaymentScheduleSnapshot

    Totals

    LifecycleState

    SourceContext

    CreatedBy
    FinalizedBy?
    FinalizedAt?

    RowVersion
}
```

---

# 58. Date Model

Potential business dates:

```text
DocumentDate
IssueDate
PostingDate
TaxDate
SupplyDate
DueDate(s)
```

Do not collapse them.

---

# 59. Document Date

Business date shown/recorded on document.

---

# 60. Posting Date

Accounting date consumed by Ledger.

---

# 61. Tax Date

Tax Engine/Country Pack.

---

# 62. Supply Date

Needed by some regimes/workflows.

---

# 63. Issue Date

Legal issuance timestamp/date can differ from Draft creation.

---

# 64. Due Date

Derived from frozen Payment Schedule.

---

# 65. Default Dates

UI defaults intelligently.

Advanced roles can edit allowed dates.

---

# 66. Date Validation

Country Pack + Ledger Period + Tax rules can reject invalid combinations.

---

# 67. No Server Time Assumption

Use Legal Entity timezone + NodaTime.

---

# 68. Party

Invoice references canonical Directory `PartyId`.

---

# 69. Party Snapshot

At finalization store needed document/legal fields:

```text
Display/Legal Name
Trade Name if required
Tax Registration Number(s)
Address
Country
Contact/attention if printed
Identifiers required by Country Pack
```

---

# 70. Why Snapshot Party Data

Directory changes must not rewrite historical invoice.

---

# 71. Party Snapshot Is Minimal

Do not copy whole Party entity.

Store only document-relevant immutable evidence.

---

# 72. Billing Address

Selected from Directory or entered permitted override.

Snapshot on finalization.

---

# 73. Shipping/Supply Address

Only when transaction/document needs it.

---

# 74. Legal Address

Country Pack decides required address form.

---

# 75. Party Address Override

If user edits for this Invoice:

- does not silently update Directory;
- can optionally offer "save back" separately.

---

# 76. Party Tax Data

Tax Engine/Tax Profile is authority.

Invoice stores result/snapshot needed for document.

---

# 77. Supplier External Invoice Number

Payable Invoice requires supplier's own number/reference where policy dictates.

---

# 78. Supplier External Number Uniqueness

Duplicate detection key can include:

```text
LegalEntity
Party
SupplierInvoiceNumber
```

with normalization.

---

# 79. Duplicate Supplier Bill Detection

Hard/soft checks may also compare:

- date;
- currency;
- amount;
- tax ID;
- PO reference.

Do not auto-reject solely on fuzzy similarity.

---

# 80. External Reference

Keep separate:

```text
Supplier Invoice No.
Customer PO No.
Customer Reference
Payment Reference
External Source Ref.
```

---

# 81. Product Reference

Line can reference Catalog Variant.

---

# 82. Product Snapshot

Final line captures:

```text
Product/Variant ID
SKU
Barcode only if document requires
Name/Description
UOM code/name
Relevant legal product code/classification references
```

---

# 83. Current Product Changes

Rename/category/UOM display changes do not rewrite old Invoice.

---

# 84. Product Variant

Use `VariantId` from `05`.

Template context can also be snapshotted.

---

# 85. UOM

Line keeps entered UOM snapshot and quantity.

Catalog owns conversion.

---

# 86. Quantity

Exact decimal type.

No double.

---

# 87. Quantity Sign

Normal Invoice lines use positive quantity with document/correction type defining economic direction.

Avoid negative-line hacks where possible.

---

# 88. Returns

Credit Note/return semantics are explicit.

Do not model correction solely by negative qty in original Invoice.

---

# 89. Ad-Hoc Line

TODIR may allow a line without Product master for service/ad-hoc invoicing.

---

# 90. Ad-Hoc Line Requirements

Must still have:

```text
Description
Quantity
UOM or unit context
Price
Tax classification/treatment where tax active
Accounting semantic/category where needed
```

---

# 91. Ad-Hoc Line Restrictions

Cannot be used where:

- Inventory movement required;
- regulated product code required and unavailable;
- accounting mapping cannot resolve;
- Country Pack forbids.

---

# 92. Quick Create Product

Preferred for recurring items.

Minimal Catalog create + readiness.

---

# 93. Invoice Line

Concept:

```text
InvoiceLine
{
    Id
    InvoiceId
    Sequence

    ProductVariantId?
    ProductSnapshot

    Description

    Quantity
    UomSnapshot

    PriceSnapshot
    DiscountSnapshot

    NetBeforeTax
    TaxSnapshot
    TaxAmount
    GrossAmount

    Dimensions
}
```

---

# 94. Sequence

Stable line order.

Reordering Draft allowed.

Final sequence frozen.

---

# 95. Description

Can start from Catalog description and be overridden per Draft if allowed.

Final text snapshot.

---

# 96. Line Comments

Use dedicated non-financial line-note type if needed.

Do not fake zero-value product line unless required.

---

# 97. Pricing Integration

Invoicing constructs Pricing Context and calls `07`.

---

# 98. Pricing Batch

For multi-line document:

```text
ResolvePricesBatch
```

No per-line network/DB N+1.

---

# 99. Price Snapshot

Final line stores:

```text
Resolved Price
Final Agreed Price
Price Currency
Price UOM
Price List/Version
Rule/Version
Manual Override
Override Reason
Pricing Resolver Version
```

compactly.

---

# 100. Price Snapshot Is Historical Truth

Do not call Pricing later to reconstruct old Invoice.

---

# 101. Price Override

Requires permission/policy.

---

# 102. Price Override Audit

Store:

```text
Resolved
Final
Delta / deviation
Actor
Reason
```

---

# 103. Price Missing

Draft cannot finalize if required line price unresolved.

Never default zero.

---

# 104. Explicit Zero Price

Allowed only as explicit commercial decision.

---

# 105. Discounts

Distinguish:

```text
Line Discount
Document Discount
Commercial adjustment
Promotion-derived result
```

---

# 106. Pricing Ownership of Commercial Rule

Pricing decides eligible price/discount logic.

Invoicing freezes applied result.

---

# 107. Manual Line Discount

Transaction override may be permitted.

Snapshot reason/policy.

---

# 108. Document Discount

If allowed, allocate deterministically across eligible lines before final Tax calculation.

---

# 109. Discount Allocation

Store allocation per line so:

- Tax is reproducible;
- Ledger facts are reproducible;
- Credit Note can reproduce original basis.

---

# 110. Discount Residual

Explicit FinBase rounding strategy.

Final eligible line can absorb deterministic residual.

---

# 111. Charges

Document can contain typed charges:

```text
Shipping
Handling
Insurance
Service Fee
Other approved charge type
```

---

# 112. No Generic Unexplained "Other"

Every charge must have a semantic code/category.

---

# 113. Charge Ownership

Upstream module can propose charge.

Invoice stores final commercial charge snapshot.

Tax classifies.

Ledger maps accounting role.

---

# 114. Charge Line vs Header Charge

Architecture supports both, but one canonical calculation representation is required.

Recommended: normalize to calculation components allocated/represented consistently.

---

# 115. Tax Integration

Invoicing calls `08 Tax Engine`.

---

# 116. Tax Context

Per line/document includes:

- Legal Entity;
- Party tax context;
- product tax context;
- dates;
- jurisdiction;
- net/gross basis;
- discounts/charges;
- currency;
- document type.

---

# 117. Tax Batch

Resolve document-level context once.

Product tax profiles batch.

---

# 118. Tax Disabled Legal Entity

Fast path.

Invoice shows no fake VAT 0%.

---

# 119. Tax Snapshot

Final line/document stores:

```text
Treatment
Tax Code
Rate/Method
Taxable Base
Tax Amount
Jurisdiction
Rule Version
Rounding Policy Version
Reason Code
Calculation Version
```

---

# 120. Multiple Tax Components

Line stores `TaxLines[]`.

Do not only store one TaxAmount field as full truth.

---

# 121. Tax Summary

Document summary derives from frozen line/document tax components.

---

# 122. Tax Inclusive Price

Invoice stores final Net/Tax/Gross result from Tax Engine.

Does not reverse-engineer later.

---

# 123. Tax Exclusive Price

Same.

---

# 124. Tax Recalculation on Draft

Allowed.

---

# 125. Tax Recalculation After Finalization

Forbidden except correction/new legal document.

---

# 126. Country Pack Requirements

Country Pack can require:

- party fields;
- product codes;
- tax IDs;
- supply date;
- legal text;
- currency presentation;
- document type;
- compliance route.

---

# 127. Currency

Transaction Currency from FinBase.

---

# 128. Base Currency

Canonical Legal Entity Base Currency from Organization/FinBase context.

Invoice does not duplicate Entity base currency config.

---

# 129. Foreign Currency Invoice

Stores:

```text
TransactionCurrency
FX Snapshot where required
```

Accounting base amounts go into Ledger Fact.

---

# 130. FX Purpose

Invoice accounting conversion uses explicit approved FinBase purpose.

Tax can have separate Tax FX purpose.

---

# 131. No Live FX During Historical View

Snapshot.

---

# 132. FX Override

If allowed:

- permission;
- reason;
- rate;
- source;
- audit.

---

# 133. Money Precision

Use `Money`, `UnitPrice`, FinBase rules.

No JavaScript Number authority.

---

# 134. Totals

Canonical document outputs:

```text
Line Net Total
Discount Total
Charge Total
Taxable Total(s)
Tax Total
Gross / Invoice Total
Rounding Adjustment if explicit
```

---

# 135. Totals Pipeline

Example conceptual order:

```text
Pricing per line
→ line/document commercial adjustments
→ allocated charge/discount bases
→ Tax Engine
→ FinBase rounding
→ Document totals
```

Exact Country Pack deviations explicit.

---

# 136. No Independent Tax Formula in Totals Service

Totals orchestrator consumes Tax Result.

---

# 137. Total Invariant

```text
InvoiceTotal
=
sum(final line/charge gross components)
+ explicit permitted document-level components
```

---

# 138. Payment Term

Default can come from PartyFinancialProfile.

Definition owned by FinBase.

---

# 139. Payment Term Override

Draft user with permission can select another term.

---

# 140. Payment Schedule Snapshot

Finalization stores resolved schedule:

```text
Due Date
Amount
Currency
Sequence
Term/Version
```

---

# 141. Payment Schedule Invariant

```text
SUM(schedule amount) == Invoice Total
```

exactly after approved residual allocation.

---

# 142. Single Due Date

Term can resolve to one installment.

---

# 143. Multi-Installment

Subledger creates one Party Open Item per schedule installment or equivalent schedule-aware items.

---

# 144. Changing Payment Term Master

Does not change final Invoice.

---

# 145. Payment Term Description on PDF

Can display friendly label/schedule.

---

# 146. Subledger Integration

On financial finalization:

```text
CreateReceivableOpenItems
```

or:

```text
CreatePayableOpenItems
```

---

# 147. Subledger Input

Uses frozen:

```text
Party
DocumentRef
Original amount
Payment Schedule
Currency
Historic base context
Due dates
Control context
```

---

# 148. Invoicing Does Not Maintain Outstanding Amount

No authoritative mutable:

```text
invoice.outstanding_amount
```

owned by Invoicing.

---

# 149. Settlement Summary Projection

Invoice read model can cache:

```text
Paid
Remaining
SettlementStatus
```

from Subledger.

---

# 150. No `invoice.paid = true` Manual Field

Settlement authority remains Subledger.

---

# 151. Ledger Integration

Invoice produces semantic Fact:

```text
CustomerInvoicePosted@1
VendorBillPosted@1
CustomerCreditNotePosted@1
...
```

---

# 152. Fact Contains Business Meaning

Example:

```text
Receivable amount
Revenue components
Tax components
Charges
Discount effects
Party
Currency
Dimensions
Source Invoice
```

---

# 153. Fact Does Not Contain GL Account IDs

`09` Account Determination.

---

# 154. Ledger Posting Preview

Finance-authorized user can preview accounting before finalization.

---

# 155. Posting Preview Is Same Ledger Engine

No duplicate posting logic.

---

# 156. Ledger Posting Result

Invoice can retain:

```text
JournalEntryId
```

as reference/projection.

Ledger remains authority.

---

# 157. Financial Atomicity

For normal financial invoice, preferred:

```text
Invoice Finalization
+
Subledger Open Items
+
Ledger Posting
+
Audit
+
Outbox
```

in one PostgreSQL transaction via Transaction Coordinator.

---

# 158. Why Atomic

Never allow:

```text
Invoice finalized
but no AR/AP
```

or:

```text
Invoice finalized
but Ledger failed
```

for paths designated immediate financial invariant.

---

# 159. Compliance Is Different

External government call must NOT be inside long DB transaction.

---

# 160. Prepare vs Issue

For countries requiring clearance before legal issue:

```text
Prepare immutable candidate
→ commit internal state
→ submit to Compliance
→ authority response
→ legally Issue/Clear
```

exact lifecycle defined by Country Pack.

---

# 161. No DB Transaction Waiting for Authority API

Critical performance/reliability rule.

---

# 162. Compliance Boundary

Invoicing provides canonical immutable document payload/snapshot.

Compliance transforms/signs/submits according to Country Pack.

---

# 163. Compliance Can Add Regulatory Metadata

Examples:

```text
Authority UUID
Submission ID
Clearance ID
Hash
QR data
Authority timestamp
Error codes
```

not owned by Invoice calculation core.

---

# 164. Regulatory State Projection

Invoice UI shows Compliance state.

---

# 165. Current Saudi Pattern

Official ZATCA technical guidance distinguishes real-time clearance for standard tax invoices and reporting flows for applicable documents.

Therefore TODIR must keep `RegulatoryState` separate from accounting/document lifecycle and Country Pack-driven.

---

# 166. Current Egypt Pattern

Egyptian Tax Authority maintains separate e-Invoice onboarding/integration, coding and electronic-signature guidance.

Therefore ETA integration belongs Compliance/Country Pack, not generic Invoice aggregate.

---

# 167. Current UAE Direction

The UAE FTA currently describes e-Invoicing as structured electronic invoice exchange/reporting; unstructured PDF/Word/image/email is not considered the e-Invoice itself.

Therefore rendering a PDF and creating a structured regulatory e-Invoice must remain separate capabilities.

---

# 168. Rendering

Invoice domain exposes:

```text
InvoiceRenderModel
```

from final snapshot.

---

# 169. Template Engine

Separate document/template capability renders:

- PDF;
- print;
- email attachment;
- bilingual layout.

---

# 170. Template Version

Final issued document should record template/version if exact reproduction is needed.

---

# 171. PDF Is Not the Canonical Invoice Data

Canonical Invoice snapshot remains structured data.

---

# 172. Rendered Artifact Retention

For legal/audit needs, store rendered version FileRef + hash where Country Pack/policy requires.

---

# 173. Re-rendering Historical Invoice

If template changed, user may:

- view original rendered artifact;
- optionally render a "current template copy" clearly labeled if policy permits.

Do not silently replace original.

---

# 174. Arabic / English

Invoice can render:

```text
Arabic
English
Bilingual
```

without changing data truth.

---

# 175. RTL

Templates/design system must support RTL.

---

# 176. Localized Numbers/Dates

Presentation formatting only.

Canonical values remain locale-neutral.

---

# 177. Legal Text

Country Pack can supply versioned legal wording.

Final document snapshots or references exact version where required.

---

# 178. Internal Note

Never print unless template explicitly maps it.

---

# 179. Customer/Supplier Visible Note

Separate field.

---

# 180. Legal Note

Separate Country Pack-controlled field/content.



# 181. Attachments

Invoice can reference Platform Files:

```text
Purchase Order
Contract
Delivery Note
Supporting Document
Supplier PDF
```

---

# 182. File Ownership

Platform Files owns storage/version/security.

Invoicing stores FileRef + attachment role.

---

# 183. Attachment Roles

Typed:

```text
CustomerPO
SupplierOriginalInvoice
Contract
DeliveryProof
TaxEvidence
OtherSupporting
```

---

# 184. No Raw File Blob in Invoice Tables

Store object/file reference.

---

# 185. Document Delivery

Invoice can be:

```text
Sent by email
Downloaded
Shared via Portal
Printed
```

Delivery subsystem owns transport/logging.

---

# 186. Delivery State

Potential projection:

```text
NotSent
Queued
Sent
Delivered?
Failed
```

Transport capabilities vary.

---

# 187. Sending Does Not Finalize

Draft cannot be accidentally turned final just because user clicked email.

---

# 188. Final Document Delivery

Default: only final/issued copy sent as legal invoice.

Draft/pro forma clearly labeled.

---

# 189. Payment Instructions

Render model can include:

- Bank account.
- Payment link.
- Payment reference.
- QR payment data.

Source comes from Payments/Settings.

---

# 190. Payment Link

Payment provider creates link.

Invoice stores/reference metadata only.

---

# 191. Receive Payment Action

Invoice UI can expose:

```text
Receive Payment
```

which starts Payments flow.

Invoicing does not create cash/bank movement itself.

---

# 192. Payment History

UI queries Subledger/Payments.

Do not store duplicate canonical payment list inside Invoice.

---

# 193. Refund Action

Starts Payment/Refund workflow.

---

# 194. Paid Invoice Correction

Correction rules must consider settlement.

---

# 195. Correction Is Not Edit

Finalized Invoice amounts are never edited directly.

---

# 196. Credit Note

A Credit Note represents explicit reduction/reversal of original financial obligation.

---

# 197. Debit Note

A Debit Note represents explicit increase/adjustment where legally/business applicable.

---

# 198. Credit Note Relationship

Prefer:

```text
CreditNote.CreditsInvoiceId
```

or typed relation.

---

# 199. Partial Credit

Can reference subset of lines/amounts.

---

# 200. Full Reversal

Credit Note can reverse entire original Invoice.

---

# 201. Correct and Replace

Pattern:

```text
Original Invoice
→ Full Corrective Credit Note
→ New Corrected Invoice
```

where business/country workflow supports.

---

# 202. Current ERP Pattern

Business Central and Odoo both use posted credit documents/corrective flows rather than silently overwriting posted invoices.

TODIR adopts immutable-document correction as architecture rule.

---

# 203. Cancellation

For Draft:

```text
Delete / Cancel Draft
```

depending audit policy.

For Finalized:

```text
Correction/Reversal document
```

plus lifecycle marker.

---

# 204. Hard Delete

Finalized Invoice hard delete is forbidden.

---

# 205. Draft Deletion

Allowed by permission if no legal number/financial consequences.

Audit minimal metadata if required.

---

# 206. Regulatory Cancellation

Some authorities require cancellation/credit-note process.

Compliance/Country Pack decides.

---

# 207. Accounting Reversal

Credit/correction Fact causes Ledger reversal/corrective posting.

Invoicing never edits Journal Entry.

---

# 208. Subledger Correction

Correction document creates opposite/open credit items/applications according to Subledger contract.

---

# 209. Correcting Paid Invoice

May require:

- Credit Note.
- Refund/available credit.
- allocation reversal or credit application.

Subledger/Payments coordinate.

---

# 210. Return of Goods

Inventory return is a separate Inventory/Sales workflow.

Credit Note references return context but does not move stock itself.

---

# 211. Service Refund

No Inventory movement.

---

# 212. Correct Tax

Tax Engine calculates correction document using original/reference rules as required by Country Pack.

---

# 213. Historical Tax Link

Correction snapshot can reference original Tax Snapshot.

---

# 214. Correct Currency

Correction usually uses original document currency unless policy permits otherwise.

Country Pack/business rules explicit.

---

# 215. Correct Payment Schedule

Credit Note creates credit/open item semantics; it does not simply edit old due dates.

---

# 216. Invoice Relationships

Typed graph must prevent cycles where inappropriate.

---

# 217. One Original, Multiple Credits

Supported.

Track cumulative credited quantities/amounts to prevent accidental over-credit if policy requires.

---

# 218. Over-Credit Protection

Before confirming Credit Note linked to original:

```text
Requested correction
<= remaining creditable basis
```

unless explicit exceptional policy.

---

# 219. Creditability by Line

Track credited quantity/net/tax basis from correction documents.

---

# 220. Correction Explainability

Invoice detail should show:

```text
Original
Credit Note(s)
Debit Note(s)
Replacement Invoice
Net effect
```

---

# 221. Supplier Credit Note

Same immutable correction principle.

---

# 222. Supplier Invoice Duplicate Controls

Supplier invoice number can be mandatory by policy.

---

# 223. Exact Duplicate Rule

Strong candidate:

```text
LegalEntity + Supplier Party + Normalized Supplier Invoice Number
```

---

# 224. Similar Duplicate Warning

Optional:

```text
same supplier
similar amount
near date
same PO/reference
```

warn, don't auto-block without policy.

---

# 225. Supplier Invoice OCR

Future/document digitization can populate Draft.

AI/OCR result is suggestion.

---

# 226. Supplier PDF Import

Original supplier file attaches to Draft.

---

# 227. OCR Boundary

OCR/AI does not finalize invoice automatically unless explicit high-confidence automation policy exists and all validation passes.

---

# 228. PO Matching Boundary

Purchasing owns:

```text
PO ↔ Receipt ↔ Supplier Invoice
```

matching.

---

# 229. Invoicing Supports Matching References

Supplier Invoice lines expose stable line IDs/amounts/qty for Purchasing matching.

---

# 230. Three-Way Match

Not implemented inside core Invoicing.

---

# 231. Matching Result Snapshot

Purchasing may attach approved match result/reference before finalization.

---

# 232. Direct Supplier Bill

Must still be possible for:

- utilities;
- rent;
- professional services;
- ad-hoc expenses.

---

# 233. Expense Invoice

Can use ad-hoc line/accounting category through controlled expense profile.

Do not make user create stock Product unnecessarily.

---

# 234. Accounting Category for Ad-Hoc Lines

Accounting-owned semantic category/reference supports Ledger mapping.

Not direct GL AccountId from normal user.

---

# 235. Revenue/Expense Recognition Boundary

Invoice accounting usually recognizes revenue/expense, but advanced deferral/accrual can separate timing.

---

# 236. Deferred Revenue

Future capability/module creates accounting schedule/facts.

Do not make Invoice line itself own deferral engine.

---

# 237. Deferred Expense

Same.

---

# 238. Prepayments / Down Payments

Need explicit concepts.

---

# 239. Customer Advance

Payment before final Invoice belongs Payments/Subledger.

---

# 240. Down-Payment Invoice

Some jurisdictions/businesses issue tax/legal document for advance.

Country Pack + Sales/Invoicing extension defines exact document flow.

---

# 241. Final Invoice Against Advance

Subledger applies advance credit to final Invoice; tax treatment may require Country Pack adjustments.

---

# 242. Pro Forma

Pro Forma must not automatically create:

- AR/AP.
- Ledger posting.
- tax reporting.

unless Country Pack defines a different legal document.

---

# 243. Quote vs Pro Forma

Sales owns Quote.

Invoicing may render Pro Forma from order/document context if product requires.

---

# 244. Recurring Invoice

Future scheduling capability can generate Draft Invoices periodically.

---

# 245. Recurrence Is Not Invoice State

A recurring template/schedule is a separate aggregate.

---

# 246. Subscription Billing

Subscription owns billing schedule/entitlement.

Invoicing creates actual Invoice.

---

# 247. Consolidated Invoicing

Future Sales can combine eligible source lines/orders into one Invoice.

Eligibility determined upstream/policy.

---

# 248. Split Invoicing

Source order can create multiple Invoices.

Invoicing stores source-line quantities/relationships.

---

# 249. Billing Quantity

Sales/Purchasing supplies billable quantity.

Invoicing does not infer delivery state by querying Inventory tables directly.

---

# 250. Source Snapshot

Invoice stores source references/line refs sufficient for traceability.

---

# 251. Dimensions

Invoice lines/header can carry analytical dimension context.

---

# 252. Dimension Ownership

Ledger defines accounting dimension rules.

Business source can provide dimension values.

---

# 253. Dimension Snapshot

Final Invoice retains relevant dimension references for Fact generation/audit.

---

# 254. Required Dimensions

Ledger posting preview/finalization validates.

---

# 255. Branch

Invoice can have Branch context.

Branch belongs Organization.

---

# 256. Legal Entity

Invoice belongs exactly one Legal Entity.

---

# 257. No Cross-Entity Invoice

A single Invoice cannot span multiple Legal Entities.

---

# 258. Intercompany

Future intercompany process creates separate documents per entity.

---

# 259. Tenant

RLS.

---

# 260. Country

Derived from Legal Entity; invoice can involve cross-border counterparty but issuing Entity country is canonical.

---

# 261. Document Currency

One transaction currency per Invoice initially.

---

# 262. Mixed Currency Lines

Forbidden.

---

# 263. Line Tax Currency

Normally Invoice currency; tax reporting currency can be separate snapshot through Tax.

---

# 264. Base Amount Storage

Invoice may store base totals needed for audit/facts, but Ledger is accounting base authority.

---

# 265. Calculation Version

Final Invoice stores:

```text
InvoiceCalculationVersion
PricingResolverVersion
TaxCalculationVersion
FinancialPolicyVersion
```

where useful.

---

# 266. Calculation Fingerprint

Draft can hold hash/version fingerprint to detect stale preview.

---

# 267. Finalization Recalculation

Always authoritative server-side.

---

# 268. Finalization Pipeline

Detailed:

```text
1. Load Draft with concurrency token
2. Authorize actor/action
3. Validate Legal Entity/Branch
4. Validate Party readiness
5. Validate Product/UOM readiness
6. Resolve currency/financial policy
7. Resolve Pricing in batch
8. Apply authorized commercial overrides
9. Allocate document discounts/charges
10. Resolve Tax in batch
11. Calculate document totals
12. Resolve Payment Schedule
13. Validate Country Pack requirements
14. Validate dates/period
15. Validate duplicate supplier number
16. Preview/validate Ledger accounting
17. Validate Subledger open-item creation
18. Allocate legal number when appropriate
19. Freeze Invoice snapshots
20. Create Subledger items
21. Post Ledger Fact
22. Persist audit/outbox
23. Commit
24. Trigger Compliance/Delivery workflows after commit
```

---

# 269. Pipeline Ordering Is Intentional

Pricing precedes Tax.

Payment schedule uses final Invoice total.

Ledger uses frozen final amounts.

Compliance receives committed immutable data.

---

# 270. No External HTTP in Transaction

No:

```text
ZATCA
ETA
FTA/e-Invoice provider
Email
Payment Gateway
```

call inside core DB transaction.

---

# 271. Finalization Dry Run

Support:

```text
Validate / Preview Finalization
```

without number allocation/persistence.

---

# 272. Preview Result

Can include:

- totals;
- tax breakdown;
- payment schedule;
- accounting preview;
- blockers;
- warnings;
- regulatory readiness.

---

# 273. Preview Is Not Authority

Finalization re-validates.

---

# 274. Warnings vs Blockers

Typed:

```text
Warning
RequiresConfirmation
Blocker
```

---

# 275. Examples of Blockers

```text
Missing Price
Missing Tax Classification
Invalid Tax Registration
Closed Posting Period
Missing Account Mapping
Duplicate Supplier Invoice Number
Unbalanced Payment Schedule
Required Regulatory Field Missing
```

---

# 276. Examples of Warnings

```text
Customer over credit limit
Unusual price override
Near duplicate supplier bill
Long overdue customer
Future date
```

Exact policy defines block/warn.

---

# 277. Credit Limit Integration

Before customer Invoice finalization, optional credit-control check can consume Subledger exposure.

---

# 278. Credit Control Ownership

Sales/Credit Policy decides block/approval.

Invoicing executes approved outcome.

---

# 279. Approval Workflow

Optional:

```text
Draft
→ Submit for Approval
→ Approved
→ Finalize
```

via Workflow capability.

---

# 280. Approval Is Separate From Lifecycle Truth

Approval state should not pollute financial lifecycle enum.

---

# 281. Price Override Approval

Workflow can trigger if deviation exceeds policy.

---

# 282. Credit Limit Approval

Same.

---

# 283. High-Value Invoice Approval

Optional rule.

---

# 284. Small Company

No approval complexity by default.

---

# 285. Document Number Timing

Two broad patterns:

```text
Assign at financial finalization
Assign at legal issue
```

Country Pack/sequence policy decides.

---

# 286. Clearance-Required Jurisdiction

If legal Invoice number/data must be fixed before authority clearance:

prepare immutable candidate + assigned number, then Compliance submits.

---

# 287. Regulatory Rejection

Do not reuse identity blindly.

Country Pack determines correction/resubmission rules.

---

# 288. Rejection State

Compliance state only.

Invoice financial/accounting treatment follows Country Pack workflow.

---

# 289. Regulatory Retry

Idempotent using immutable document identity.

---

# 290. Structured E-Invoice Payload

Generated by Compliance from Invoice snapshot + Country Pack mappings.

---

# 291. PDF vs Structured E-Invoice

Different artifacts.

---

# 292. UAE Current Direction

UAE FTA explicitly states PDFs/images/emails are not themselves structured e-Invoices under the e-Invoicing model.

Architecture separation is mandatory.

---

# 293. Egypt Integration

ETA currently publishes e-Invoice integration/coding/signature guidance.

Compliance adapter consumes Invoice snapshot.

---

# 294. KSA Integration

ZATCA FATOORA API clearance/reporting belongs Compliance.

---

# 295. Regulatory Provider Abstraction

Potential:

```text
IInvoiceComplianceProvider
```

behind Compliance module, not Invoicing.

---

# 296. Country Pack Capability

```text
RequiresEInvoice
RequiresClearance
RequiresReporting
RequiresSignature
RequiresQR
RequiresProductCode
```

versioned/effective.

---

# 297. Country Pack Document Validation

Invoicing asks:

```text
ValidateInvoiceDocumentRequirements(snapshot candidate)
```

before issue.

---

# 298. Legal Text Versioning

Store exact Country Pack content version.

---

# 299. Printed Invoice Requirements

Country Pack contributes required fields; Template cannot hide legally mandatory fields.

---

# 300. Template Safety

Template editor should expose protected legal blocks.

Cannot remove required tax number/document number if Country Pack says mandatory.

---

# 301. Rendering Contract

```text
InvoiceRenderModel
```

must contain all values already resolved.

Template engine must not query Pricing/Tax.

---

# 302. Rendering Determinism

Same snapshot + same template version → same business content.

---

# 303. Original Document Hash

Optional artifact hash for audit.

---

# 304. QR

QR payload generation belongs Compliance/Country Pack if regulatory.

Generic QR for payment/link belongs Delivery/Payments.

---

# 305. Digital Signature

Regulatory signing belongs Compliance/provider.

---

# 306. Search/List UX

Core columns:

```text
Number
Party
Date
Total
Currency
Document Status
Settlement Status
Regulatory Status
Due
```

---

# 307. Supplier List

Adds:

```text
Supplier Invoice No.
PO/Reference
```

---

# 308. Filters

```text
Entity
Branch
Customer/Supplier
Date
Due
Status
Currency
Amount range
Overdue
Regulatory status
Source
```

---

# 309. Saved Views

Platform/user preference.

---

# 310. Invoice List Projection

Recommended read model:

```text
invoice_list_projection
```

with denormalized:

- party display;
- number;
- totals;
- current settlement summary;
- regulatory summary;
- source ref.

---

# 311. Read Projection Is Not Authority

Can rebuild.

---

# 312. List N+1 Forbidden

Do not query Subledger/Compliance separately per row.

---

# 313. Projection Update

Relevant events update settlement/regulatory summaries.

---

# 314. Immediate Settlement Display

After payment/application, Invoice detail can query Subledger directly or strongly updated projection.

---

# 315. Detail Screen

Sections:

```text
Header
Lines
Totals
Payment Schedule
Payments/Settlement
Accounting
Regulatory
Attachments
History
```

role-aware.

---

# 316. Progressive Disclosure

Sales user sees minimal.

Finance sees accounting/payment schedule.

Tax/Compliance admin sees regulatory detail.

---

# 317. Mobile/Small Screen

Header + totals first.

Line table responsive.

---

# 318. Large Line Count

Support 500+ lines without catastrophic rerender/query behavior.

---

# 319. Frontend Line State

React Hook Form can manage edited row state, but avoid a single giant rerender on each keystroke.

---

# 320. Virtualization

Use for large line grids if UX testing shows need.

---

# 321. Calculation Debounce

Draft edits can debounce backend recalculation.

---

# 322. Optimistic UI

Can show tentative arithmetic, but final server result is authoritative.

---

# 323. Batch Pricing

Mandatory.

---

# 324. Batch Tax

Mandatory.

---

# 325. Product Context

Batch fetch product/UOM snapshots/read data.

---

# 326. Party Context

Resolve once.

---

# 327. Financial Context

Resolve once.

---

# 328. Avoid Repeated Country Pack Resolution

Resolve Pack/version once per document.

---

# 329. Draft Save Performance

Do not persist a new audit/version row for every keystroke.

Save meaningful state.

---

# 330. Finalization Performance Target

Typical 10–50 line Invoice, local dependencies only:

```text
p95 <= 200–300ms
```

initial goal excluding external compliance.

Measure under realistic load.

---

# 331. Simple 1–10 Line Target

Aim materially lower, but do not weaken integrity for benchmark.

---

# 332. 500-Line Finalization

No N+1.

Bounded batch processing.

---

# 333. Compliance Latency

Not part of core DB finalization p95 if asynchronous/state-machine integration.

If clearance required before legal issue, UX shows progress without holding DB transaction.

---

# 334. Concurrency

Two users edit Draft:

optimistic conflict.

---

# 335. Concurrent Finalize

Unique idempotency + state guard.

---

# 336. Concurrent Credit Note

Protect against over-credit using transaction/locking/current credited basis.

---

# 337. Supplier Duplicate Race

Unique normalized external-number constraint where policy hard-enforces.

---

# 338. Normalization

Supplier invoice number normalization must preserve raw original string and derived comparison key.

---

# 339. Case/Whitespace

Comparison normalization can:

- trim;
- normalize case;
- normalize safe whitespace.

Do not remove meaningful digits/punctuation blindly.

---

# 340. Invoice Schema

Recommended:

```text
business_invoicing
```

---

# 341. Core Tables

```text
invoice
invoice_line

invoice_party_snapshot
invoice_address_snapshot

invoice_price_snapshot
invoice_tax_component
invoice_payment_schedule

invoice_charge
invoice_discount_allocation

invoice_relationship
invoice_attachment

invoice_finalization_record
invoice_number_reference

invoice_list_projection
```

Exact normalization refined in implementation.

---

# 342. Header Snapshot Strategy

Some snapshots can be embedded JSONB if immutable/versioned and not frequently queried.

---

# 343. Queryable Fields Stay Typed

Do not hide hot/filter fields in JSON:

```text
number
party_id
date
total
currency
state
supplier_external_number
```

---

# 344. Tax Components

Normalized child table useful for reporting/compliance.

---

# 345. Price Snapshot

Can be line columns + compact snapshot JSON/version IDs.

Avoid excessive join explosion.

---

# 346. Party Snapshot

JSONB can be appropriate because immutable document evidence varies by Country Pack.

Schema version required.

---

# 347. Snapshot Version

Each serialized snapshot:

```text
snapshot_schema_version
```

---

# 348. Invoice Table Concept

```text
id
tenant_id
legal_entity_id
branch_id

direction
document_type

document_number
series_id
supplier_external_number?

party_id

document_date
posting_date
tax_date
issue_date?

currency_code

lifecycle_state

net_total
tax_total
charge_total
discount_total
gross_total

source_module?
source_ref?

finalized_at?
finalized_by?

row_version
created_at
updated_at
```

---

# 349. Invoice Line Concept

```text
id
tenant_id
legal_entity_id
invoice_id

sequence

product_variant_id?
description

quantity
uom_code_snapshot

unit_price
line_discount
net_amount
tax_amount
gross_amount

pricing_snapshot_ref/json
tax_summary_ref/json

source_line_ref?
```

---

# 350. Tenant/Entity Denormalization

Include Tenant/Entity on child rows where useful for RLS/performance/partition readiness.

---

# 351. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 352. Runtime Role

No BYPASSRLS/ownership.

---

# 353. Entity Scope

Application authorization.

---

# 354. Composite FKs

Prevent cross-tenant/entity child links.

---

# 355. Unique Invoice Number

Unique according to series/entity/document-type scope.

---

# 356. Supplier External Number Index

Conditional unique per configured scope if hard policy.

---

# 357. Source Idempotency Index

Unique for upstream source/purpose where configured.

---

# 358. Invoice List Indexes

Likely:

```text
tenant + entity + document_date desc
tenant + entity + party + document_date desc
tenant + entity + lifecycle + document_date
tenant + entity + document_number
```

---

# 359. Due/Settlement Filtering

Due/settlement summary may live projection.

Index projection.

---

# 360. Regulatory Filtering

Projection index on regulatory state.



# 361. Partitioning

Do not partition Day 1 by habit.

Design for future time/entity partitioning if measured.

---

# 362. UUIDv7

Use for Invoice and Line IDs.

---

# 363. Numeric Types

C# `decimal` + PostgreSQL `numeric`.

No floating point.

---

# 364. API Base

```text
/api/v1/invoicing
```

---

# 365. Customer Invoice APIs

```text
GET  /customer-invoices
POST /customer-invoices
GET  /customer-invoices/{id}
PUT  /customer-invoices/{id}
POST /customer-invoices/{id}/preview
POST /customer-invoices/{id}/finalize
POST /customer-invoices/{id}/credit-notes
POST /customer-invoices/{id}/debit-notes
```

---

# 366. Supplier Invoice APIs

```text
GET  /supplier-invoices
POST /supplier-invoices
GET  /supplier-invoices/{id}
PUT  /supplier-invoices/{id}
POST /supplier-invoices/{id}/preview
POST /supplier-invoices/{id}/finalize
```

---

# 367. Generic Internal Commands

Application layer can share:

```text
CreateInvoiceDraft
UpdateInvoiceDraft
PreviewInvoice
FinalizeInvoice
CreateCorrectiveDocument
CancelDraft
```

---

# 368. No Generic Public `SetStatus`

State transitions only through business commands.

---

# 369. No Public `SetPaid`

Forbidden.

---

# 370. No Public `SetTaxAmount`

Forbidden.

---

# 371. No Public `SetJournalEntry`

Forbidden.

---

# 372. Draft Update DTO

Accept user-editable inputs only.

Server ignores/rejects calculated/final fields.

---

# 373. Mass Assignment Defense

Never let client post:

```text
lifecycle_state = Finalized
tax_total = ...
paid = true
regulatory_state = Cleared
```

---

# 374. Preview Endpoint

Returns:

```text
Resolved lines
Price explanations
Tax summary
Totals
Payment Schedule
Warnings/blockers
Accounting preview if authorized
```

---

# 375. Finalize Endpoint

Requires:

- invoice version;
- idempotency key;
- explicit command.

---

# 376. Finalize Response

```text
InvoiceId
DocumentNumber
Lifecycle
Totals
PaymentSchedule
AccountingRef
SubledgerRef summary
RegulatoryNextAction
```

---

# 377. Corrections API

Explicit actions.

No update-in-place.

---

# 378. PDF Endpoint

```text
GET /invoices/{id}/rendered-document
```

uses authorized rendering/file service.

---

# 379. Original vs Current Render

API distinguishes:

```text
original-issued-artifact
render-preview
```

---

# 380. Email Send API

Invoicing Application command requests Delivery service.

---

# 381. Send Idempotency

Avoid duplicate email on retry where provider permits.

---

# 382. Internal Contracts Project

```text
Erp.Business.Invoicing.Contracts
```

or repository naming convention equivalent.

---

# 383. Key Input Contracts

```text
InvoiceSourceContext
InvoicePartyContext
InvoiceLineInput
InvoiceCommercialContext
InvoiceFinalizationRequest
```

---

# 384. Key Output Contracts

```text
FinalizedInvoiceReference
InvoiceSnapshotContract
InvoiceAccountingFact
InvoiceOpenItemSchedule
InvoiceComplianceSnapshot
```

---

# 385. Sales Contract

Sales can request Invoice creation from approved source data without accessing Invoicing implementation entities.

---

# 386. Purchasing Contract

Same.

---

# 387. Compliance Contract

Compliance receives immutable:

```text
InvoiceComplianceSnapshot
```

---

# 388. Snapshot Must Be Sufficient

Compliance must not query mutable Pricing/Tax/Catalog to rebuild final document.

---

# 389. Ledger Contract

Typed Accounting Fact per `09`.

---

# 390. Subledger Contract

Typed Open Item creation per `10`.

---

# 391. Events

Meaningful integration events:

```text
InvoiceDraftCreatedV1
InvoiceFinalizedV1
InvoiceCorrectedV1
InvoiceCancelledByCorrectionV1
InvoiceRegulatoryStateChangedV1  // probably originated Compliance
InvoiceSettlementSummaryChangedV1 // probably originated Subledger
```

---

# 392. Source of Event

Do not have Invoicing impersonate Compliance/Subledger state changes.

Those domains publish their own events.

---

# 393. Outbox

All durable events via Transactional Outbox.

---

# 394. Event Payload Size

Do not publish full 1000-line Invoice to broker by default.

Publish summary/reference/version.

Consumer fetches snapshot through contract if authorized.

---

# 395. Compliance Work Command

After commit:

```text
Prepare/Submit Invoice Compliance
```

queued/idempotent.

---

# 396. Delivery Work Command

After successful legal issue/appropriate state.

---

# 397. Workflow Hooks

Potential:

```text
BeforeInvoiceFinalize
AfterInvoiceFinalize
BeforeCreditNoteFinalize
```

constrained workflow engine.

---

# 398. Custom Fields

Invoice can support approved custom fields through Platform Custom Fields.

---

# 399. Custom Field Snapshot

Final custom field values can be frozen if part of document.

---

# 400. Custom Fields and Tax/Accounting

Arbitrary custom fields cannot affect critical Tax/Ledger logic unless promoted to typed approved Rule Inputs.

---

# 401. Extensions

External developer can:

- add custom fields/views;
- create upstream source integration;
- consume APIs/events;
- add approved Country Pack/extension.

Cannot replace core finalization with arbitrary tenant JavaScript.

---

# 402. Security Permissions

Initial:

```text
invoicing.customer_invoice.read
invoicing.customer_invoice.create
invoicing.customer_invoice.edit
invoicing.customer_invoice.finalize
invoicing.customer_invoice.cancel_draft

invoicing.supplier_invoice.read
invoicing.supplier_invoice.create
invoicing.supplier_invoice.edit
invoicing.supplier_invoice.finalize

invoicing.credit_note.create
invoicing.debit_note.create

invoicing.price_override
invoicing.payment_term_override
invoicing.date_override

invoicing.accounting_preview
invoicing.regulatory_details

invoicing.attachment.manage
invoicing.delivery.send

invoicing.audit.view
```

---

# 403. Direction-Specific Roles

Sales users need not see supplier invoices.

AP users need not see all customer commercial detail.

---

# 404. Price Visibility

Some users can create quantity/product lines while unit margin/cost remains hidden.

Pricing output visible according to business need.

---

# 405. Cost Visibility

Invoicing should not expose Inventory Cost unless explicit permission.

---

# 406. Supplier Sensitive Data

Restrict vendor financial documents by role/entity.

---

# 407. Step-Up / Approval

Optional for:

- very high-value invoice;
- large price override;
- unusual backdate;
- correction of legally issued document.

---

# 408. Audit — Draft

Do not log every keystroke.

Log meaningful saves/overrides if needed.

---

# 409. Audit — Finalization

Mandatory:

```text
Actor
Time
Invoice ID
Document Number
Source
Pricing versions
Tax versions
Payment term version
Financial policy version
Posting result
```

---

# 410. Audit — Corrections

Mandatory reason + link.

---

# 411. Audit — Manual Overrides

Price/date/payment term/tax-related user inputs.

---

# 412. Tax Manual Override

Prefer Tax domain command/policy.

Invoice cannot directly set arbitrary tax.

---

# 413. Performance Metrics

```text
invoice_preview_duration
invoice_finalize_duration
invoice_finalize_lines
invoice_pricing_batch_duration
invoice_tax_batch_duration
invoice_subledger_create_duration
invoice_ledger_post_duration
invoice_duplicate_supplier_warning_total
invoice_finalize_failure_total
invoice_list_query_duration
```

---

# 414. Metrics Cardinality

No Invoice/Tenant IDs as metric labels.

---

# 415. Distributed Trace

Finalization spans:

```text
Invoicing
Pricing
Tax
FinBase
Subledger
Ledger
Outbox
```

single correlation trace.

---

# 416. Compliance Trace

Separate async trace linked by InvoiceId/correlation.

---

# 417. Logging

Do not dump full Invoice with customer tax IDs/addresses to logs.

---

# 418. Error Codes

Stable:

```text
invoicing.invoice_not_found
invoicing.draft_changed
invoicing.already_finalized
invoicing.finalization_conflict

invoicing.party_not_ready
invoicing.product_not_ready
invoicing.price_missing
invoicing.price_override_not_allowed
invoicing.tax_not_ready
invoicing.total_mismatch
invoicing.payment_schedule_invalid

invoicing.posting_date_invalid
invoicing.accounting_not_ready
invoicing.subledger_not_ready

invoicing.numbering_failed
invoicing.duplicate_supplier_invoice

invoicing.correction_not_allowed
invoicing.over_credit
invoicing.country_requirement_missing
```

---

# 419. Business-Friendly Error Example

Instead of:

```text
TaxProfileNotFoundException
```

show:

> "لا يمكن إصدار الفاتورة لأن المنتج «X» يحتاج تصنيفًا ضريبيًا."

---

# 420. Business-Friendly Posting Error

> "لا يمكن ترحيل الفاتورة لأن حساب إيرادات فئة «الخدمات» غير مُحدد."

---

# 421. Readiness Summary

Draft can show:

```text
Ready to finalize
```

or blockers grouped:

```text
Customer
Tax
Accounting
Dates
Compliance
```

---

# 422. Test Strategy — Basic Customer Invoice

```text
Draft
Add lines
Pricing
Tax
Payment schedule
Finalize
Subledger open items
Ledger posting
```

---

# 423. Test — Tax Disabled

No Tax UI/result lines.

Total remains commercial total.

---

# 424. Test — Tax Enabled

Correct Tax snapshot.

---

# 425. Test — Inclusive Pricing

Correct net/tax/gross freeze.

---

# 426. Test — Multi-Installment

Schedule sums exactly to total and Subledger creates correct due items.

---

# 427. Test — Price Change During Draft

Preview old.

Pricing master changes.

Finalization revalidates and reports meaningful change.

---

# 428. Test — Tax Rule Change During Draft

Same.

---

# 429. Test — Party Rename After Finalization

Old Invoice snapshot unchanged.

---

# 430. Test — Product Rename After Finalization

Old Invoice unchanged.

---

# 431. Test — Payment Term Master Change

Old due schedule unchanged.

---

# 432. Test — Numbering Retry

One final number only.

---

# 433. Test — Double Finalize

One Invoice finalization, one Subledger effect, one Ledger Entry.

---

# 434. Test — Transaction Rollback

Force Ledger failure.

Invoice remains non-finalized/no Open Items/no partial effects.

---

# 435. Test — Subledger Failure

Same.

---

# 436. Test — Outbox Failure Before Commit

Whole transaction rollback.

---

# 437. Test — Broker Down After Commit

Invoice/financial posting valid; Outbox retries.

---

# 438. Test — Compliance Down

Core DB transaction unaffected.

Regulatory state pending/retry according to Country Pack.

---

# 439. Test — External Clearance Required

No legal "Issued/Cleared" state until authority success.

No long DB transaction.

---

# 440. Test — Credit Note Partial

Correct subset, tax, Open Items, Ledger Fact.

---

# 441. Test — Credit Note Full

Original remains; corrective doc reverses economic effect.

---

# 442. Test — Credit Over Original

Blocked.

---

# 443. Test — Correct Paid Invoice

Subledger/payment effects handled explicitly; no data loss.

---

# 444. Test — Supplier Duplicate

Exact duplicate detected.

Concurrent exact duplicate protected if hard rule active.

---

# 445. Test — Supplier External Number Raw/Normalized

Raw preserved; comparison key normalized.

---

# 446. Test — Ad-Hoc Service Line

Works when tax/accounting readiness valid.

---

# 447. Test — Ad-Hoc Stocked Item Attempt

Blocked if Inventory/product master required.

---

# 448. Test — 500 Lines

No N+1.

Totals exact.

---

# 449. Test — 1000 Lines

Stress only; UI/backend bounded.

---

# 450. Test — Multi-Currency

FX snapshot exact; Ledger base amounts reconcile.

---

# 451. Test — Closed Period

Finalization blocked or allowed according to PostingDate policy.

---

# 452. Test — Backdated Invoice

Tax Date/Posting Date rules independently validated.

---

# 453. Test — Branch Scope

User cannot invoice unauthorized Branch.

---

# 454. Test — Legal Entity Isolation

No cross-Entity lines/party/source mix.

---

# 455. Test — Tenant RLS

Tenant A cannot access B Invoice by ID.

---

# 456. Test — Source Idempotency

Same Sales source not invoiced twice beyond intended invoicing quantity/purpose.

---

# 457. Test — Split Invoicing

If upstream allows, multiple invoices track source quantities correctly.

---

# 458. Test — Rendering

Same snapshot/template version reproduces exact values.

---

# 459. Test — Original PDF Retention

Template later changes; original artifact remains.

---

# 460. Test — Payment Status

Invoice screen reflects Subledger; cannot be manually changed.

---

# 461. Test — Accounting State

Reflects Ledger result.

---

# 462. Test — Regulatory State

Reflects Compliance; no direct Invoice mutation endpoint.

---

# 463. Property Test — Total Integrity

For generated valid line sets:

```text
document total == sum authoritative final components
```

according to policy.

---

# 464. Property Test — Schedule Integrity

```text
sum(schedule) == total
```

---

# 465. Property Test — Snapshot Determinism

Same frozen inputs + versions → same final snapshot.

---

# 466. Property Test — Finalization Idempotency

N repeated commands → one financial effect.

---

# 467. Property Test — Credit Conservation

Original creditable amount cannot be exceeded by linked finalized corrections.

---

# 468. Architecture Test — No Pricing Logic in Invoicing

Ban independent price rule resolver.

---

# 469. Architecture Test — No Tax Formula

Ban independent tax calculator.

---

# 470. Architecture Test — No Journal Entry Construction

Invoicing cannot reference Ledger implementation.

---

# 471. Architecture Test — No Open Item Table Write

Use Subledger Contract.

---

# 472. Architecture Test — No Government SDK in Core Invoicing

Compliance adapter only.

---

# 473. Architecture Test — No Direct File Storage

Platform Files.

---

# 474. Architecture Test — No SMTP

Notification/Delivery.

---

# 475. Architecture Test — No Mutable Final Amount Endpoints

CI/API review.

---

# 476. Red-Team — `invoice.tax_rate`

As sole Tax truth: reject.

---

# 477. Red-Team — `invoice.paid`

Editable canonical boolean: reject.

---

# 478. Red-Team — `invoice.gl_account_id`

Hardcoded source accounting: reject.

---

# 479. Red-Team — Update Final Invoice Lines

Reject.

---

# 480. Red-Team — Delete Final Invoice

Reject.

---

# 481. Red-Team — Change Customer Name in Directory and old PDF changes

Reject.

---

# 482. Red-Team — Recalculate old tax using current law

Reject.

---

# 483. Red-Team — Recalculate old price using current Price List

Reject.

---

# 484. Red-Team — Government call inside DB transaction

Reject.

---

# 485. Red-Team — Retry creates duplicate number

Reject.

---

# 486. Red-Team — Retry creates duplicate Open Items

Reject.

---

# 487. Red-Team — Retry creates duplicate Journal Entry

Reject.

---

# 488. Red-Team — Correction edits original

Reject.

---

# 489. Red-Team — Payment creates `invoice.paid = true`

Reject.

---

# 490. Red-Team — Tax-disabled company shows VAT 0%

Reject.

---

# 491. Red-Team — Supplier invoice with duplicate external number silently posts

Reject/warn according to configured hard policy.

---

# 492. Red-Team — One Invoice spans Legal Entities

Reject.

---

# 493. Red-Team — Mixed transaction currencies

Reject initial architecture.

---

# 494. Red-Team — Missing account mapping posts to suspense silently

Reject unless explicit managed policy intentionally maps to a configured role, never accidental fallback.

---

# 495. Red-Team — Compliance rejection destroys Invoice

Reject.

Keep state/evidence.

---

# 496. Product Acceptance — Small Service Company

Flow:

```text
New Invoice
Customer
Service
Amount
Finalize
```

TODIR handles pricing/tax/accounting/open item without complex screen.

---

# 497. Product Acceptance — Tax-Disabled Company

No tax fields unless relevant.

---

# 498. Product Acceptance — Retail Tax-Inclusive

Gross price stays user-friendly; net/tax extraction exact.

---

# 499. Product Acceptance — B2B Customer

Tax/legal fields appear only as needed.

---

# 500. Product Acceptance — Supplier Bill

AP user records supplier invoice number, lines, due terms and finalizes.

Duplicate protection active.

---

# 501. Product Acceptance — Direct Expense Bill

No PO required when policy permits.

---

# 502. Product Acceptance — Sales-Origin Invoice

Trace back to Sales Order/source lines.

---

# 503. Product Acceptance — Purchasing-Origin Bill

Trace back to PO/Receipt matching context.

---

# 504. Product Acceptance — Partial Credit

Original invoice intact; linked credit visible.

---

# 505. Product Acceptance — Paid/Remaining

Always matches Subledger.

---

# 506. Product Acceptance — Accounting

Finance can open Journal Entry and "Why this account?"

---

# 507. Product Acceptance — Tax Explanation

"Why this tax?" available.

---

# 508. Product Acceptance — Price Explanation

"Why this price?" available.

---

# 509. Product Acceptance — Compliance

User sees pending/cleared/rejected without confusing it with payment/posting state.

---

# 510. Product Acceptance — Bilingual Invoice

Same snapshot, Arabic/English template.

---

# 511. Product Acceptance — Historical Fidelity

Years later Invoice shows exact original:

- party;
- product description;
- prices;
- tax;
- totals;
- schedule.

---

# 512. Product Acceptance — Large Invoice

500 lines workable.

---

# 513. Product Acceptance — Double Click

No duplicate.

---

# 514. Product Acceptance — Failed Government Service

Invoice work not corrupted; compliance retries/state shown.

---

# 515. Implementation Order

```text
IV00 ADRs + Domain Boundaries
IV01 Schema + RLS
IV02 Invoice Aggregate + Draft Lifecycle
IV03 Party / Address Snapshots
IV04 Product / UOM Line Snapshot
IV05 Pricing Integration + Snapshot
IV06 Discounts / Charges
IV07 Tax Integration + Snapshot
IV08 Totals + FinBase Rounding
IV09 Payment Term / Schedule Snapshot
IV10 Dates + Posting/Tax Date Integration
IV11 Numbering + Finalization Idempotency
IV12 Finalization Transaction Coordinator
IV13 Subledger Open Item Integration
IV14 Ledger Accounting Fact Integration
IV15 Correction / Credit / Debit Documents
IV16 Supplier Invoice External Number + Duplicate Detection
IV17 Country Pack Document Requirements
IV18 Compliance Snapshot + Async Handoff
IV19 Rendering / Template / Original Artifact
IV20 Invoice List/Detail Read Models
IV21 Sales/Purchasing Source Contracts
IV22 Attachments / Delivery Integration
IV23 Security / Permissions / Audit
IV24 Performance / Load / Concurrency
IV25 Product + Finance + Tax + Compliance Acceptance
```

---

# 516. IV00 Exit Gate

Approved ADRs:

- Invoicing ownership.
- state dimensions.
- immutable final documents.
- correction model.
- sync financial vs async compliance boundary.
- customer/supplier common model.

---

# 517. IV01 Exit Gate

- `business_invoicing`.
- FORCE RLS.
- tenant/entity constraints.
- UUIDv7.
- exact decimals.

---

# 518. IV02 Exit Gate

Draft editable with optimistic concurrency.

Final state guarded.

---

# 519. IV03 Exit Gate

Directory changes cannot alter final Invoice.

---

# 520. IV04 Exit Gate

Catalog changes cannot alter final Invoice.

---

# 521. IV05 Exit Gate

Pricing batch + immutable snapshot + override evidence.

---

# 522. IV06 Exit Gate

Discount/charge calculation deterministic and tax-ready.

---

# 523. IV07 Exit Gate

Tax disabled/enabled/multi-component cases correct.

---

# 524. IV08 Exit Gate

Totals invariant exact.

No random rounding.

---

# 525. IV09 Exit Gate

Schedule sums to total and is immutable.

---

# 526. IV10 Exit Gate

Document/Posting/Tax/Issue/Supply dates explicit and validated.

---

# 527. IV11 Exit Gate

Concurrent/retried finalization produces one number/financial effect.

---

# 528. IV12 Exit Gate

Invoice + Subledger + Ledger + Outbox all-or-nothing for immediate posting path.

---

# 529. IV13 Exit Gate

Open Items exactly reflect Payment Schedule.

---

# 530. IV14 Exit Gate

No GL Account IDs in Invoice.

Ledger Entry balanced and traceable.

---

# 531. IV15 Exit Gate

Final document never edited; credit/debit/correction workflows preserve history.

---

# 532. IV16 Exit Gate

Supplier external invoice number + duplicate race protection.

---

# 533. IV17 Exit Gate

Country Pack can declare required document fields/types/validation without forking core.

---

# 534. IV18 Exit Gate

No authority network call in DB transaction.

Regulatory workflow idempotent.

---

# 535. IV19 Exit Gate

Original render/artifact version preserved where required.

---

# 536. IV20 Exit Gate

No list N+1; server paging/search.

---

# 537. IV21 Exit Gate

Direct + Sales-origin + Purchasing-origin supported through Contracts.

---

# 538. IV22 Exit Gate

Files/notification boundaries respected.

---

# 539. IV23 Exit Gate

RLS/authz/overrides/audit pass.

---

# 540. IV24 Exit Gate

- 10–50-line p95 measured.
- 500-line no N+1.
- concurrent finalize.
- duplicate supplier race.
- query plans.

---

# 541. IV25 Acceptance

Required signoff:

```text
Product Architect
Senior Accountant
Tax Reviewer
Compliance/Country Pack Reviewer
Backend Lead
Frontend Lead
Performance Engineer
```

before financial pilot.

---

# 542. Build Now — MUST

- Customer Invoice.
- Supplier Invoice.
- Draft/final lifecycle.
- immutable final snapshot.
- Party/address snapshot.
- Product/UOM snapshot.
- Pricing snapshot.
- discounts/typed charges.
- Tax snapshot.
- totals.
- payment schedule.
- numbering.
- Subledger integration.
- Ledger Fact integration.
- Credit Note/essential Debit Note foundation.
- supplier external invoice number.
- duplicate detection.
- Country Pack validation contract.
- Compliance async handoff.
- rendering contract.
- original artifact reference.
- read models.
- performance/security/tests.

---

# 543. Design Now / Build Later

- OCR/AI supplier bill capture.
- recurring invoices.
- subscription billing.
- advanced down-payment flows.
- complex consolidated billing.
- sophisticated deferral/revenue recognition.
- customer portal.
- e-signatures beyond country compliance.
- advanced B2G procurement references.
- cross-company billing automation.

---

# 544. Explicitly NOT Invoicing

- Sales order management.
- Purchase order matching engine.
- Inventory delivery/receipt.
- Bank/cash movement.
- Payment allocation.
- Tax rule master.
- Price list master.
- Chart of Accounts.
- Journal Entry creation.
- Dunning.
- Government API implementation.
- Email transport.
- File blob storage.

---

# 545. Senior Product Rule

> **Creating a normal invoice must remain simple even though TODIR preserves full financial, tax, accounting and compliance integrity underneath.**

---

# 546. Senior Architecture Rule

> **Invoicing orchestrates final document truth; it never duplicates the specialist engines that provide price, tax, money, settlement, accounting or compliance.**

---

# 547. Senior History Rule

> **A finalized invoice is a historical snapshot, not a live view of today's customer, product, price or tax configuration.**

---

# 548. Senior Correction Rule

> **Corrections add linked documents and accounting history; they do not rewrite the original invoice.**

---

# 549. Senior State Rule

> **Document, accounting, regulatory, settlement and delivery states are independent dimensions.**

---

# 550. Senior Performance Rule

> **Resolve document context once, batch line pricing/tax, use read projections for lists, and never trade correctness for speed.**

---

# 551. Senior Compliance Rule

> **No external government call is allowed inside the financial database transaction. Country Pack/Compliance controls issuance/clearance/reporting requirements.**

---

# 552. Market Pattern Validation — Business Central

Current Microsoft Business Central documentation supports:

- sales and purchase invoices as business documents that post financial effects;
- posted invoice correction/cancellation through corrective credit documents;
- supplier/vendor external document numbers;
- separate customer/vendor payment application.

These patterns validate TODIR's immutable-final-document and separate settlement architecture.

TODIR differs by making boundaries explicit through Contracts and specialist kernels.

---

# 553. Market Pattern Validation — Odoo 20

Current Odoo 20 documentation supports:

- customer invoices/vendor bills;
- credit/debit notes for corrections;
- electronic invoicing/localizations;
- invoice rendering;
- tax/accounting integration.

Odoo documents credit/debit notes as the correction route for validated invoices.

TODIR adopts the immutable correction principle while intentionally avoiding a monolithic module owning all financial concerns.

---

# 554. Market Pattern Validation — ERPNext

Current ERPNext documentation exposes:

- Draft vs submitted invoices;
- outstanding/partly paid/paid status;
- Payment Ledger;
- Purchase Invoice integration;
- Return/Debit Note corrections.

TODIR separates settlement authority into `10 Subledger` rather than letting invoice status itself become financial truth.

---

# 555. Official Regulatory Validation — Egypt

Egyptian Tax Authority currently provides dedicated e-Invoice guidance covering:

- system onboarding;
- integration;
- coding;
- electronic signature;
- taxpayer guidance.

This validates a dedicated Egypt Compliance adapter/Country Pack rather than hardcoding ETA transport into Invoicing.

Official source register:

- Egyptian Tax Authority — E-Invoice Services
- Egyptian Tax Authority — Taxpayer E-Invoice Guides
- Egyptian Tax Authority — Electronic Signature Guidance

Checked 2026-10-05.

---

# 556. Official Regulatory Validation — Saudi Arabia

ZATCA official e-Invoicing technical guidance defines API-driven submission and distinguishes reporting/clearance behavior, including real-time clearance for applicable standard tax invoices.

This validates:

```text
Invoice financial/document lifecycle
!=
Regulatory clearance state
```

Official source:

- ZATCA — E-Invoicing Detailed Technical Guideline / FATOORA guidance.

Checked 2026-10-05.

---

# 557. Official Regulatory Validation — UAE

The UAE Federal Tax Authority currently describes e-Invoicing as structured electronic invoice data exchanged electronically and reported to the FTA; unstructured PDFs, Word files, images, scans and emails are not themselves e-Invoices.

FTA also publishes current VAT legislation and invoice-related clarifications.

This validates:

```text
Structured Compliance Document
!=
Rendered PDF
```

Official sources:

- UAE Federal Tax Authority — UAE e-Invoicing.
- UAE Federal Tax Authority — VAT legislation/public clarifications.

Checked 2026-10-05.

---

# 558. Source Register — Business Central

**Microsoft Learn — Process sales returns or cancellations**

Used for:

- corrective sales credit memo;
- preserving posted accounting history;
- partial/full return patterns.

**Microsoft Learn — Correct or cancel a posted sales invoice**

Used for:

- correction through posted credit memo.

**Microsoft Learn — External document numbers on purchase documents**

Used for:

- supplier/vendor invoice number handling and search.

**Microsoft Learn — Managing Receivables / Payables**

Used for:

- separation of Invoice from payment application.

---

# 559. Source Register — Odoo

**Odoo 20 — Credit notes and refunds**

Used for:

- credit/debit note correction;
- link to original Invoice/Bill;
- reversal pattern.

**Odoo 20 — Finance / Invoicing documentation**

Used for:

- invoice/electronic-invoice/localization product patterns.

---

# 560. Source Register — ERPNext

**ERPNext — Sales Invoice**

Used for:

- draft/submitted invoice pattern;
- outstanding/paid presentation.

**ERPNext — Purchase Invoice**

Used for:

- Supplier Invoice workflow;
- Payment Ledger integration;
- return/debit note.

**ERPNext — Payment Entry / Payment Reconciliation**

Used only as cross-check for settlement separation already owned by `10`.

---

# 561. Source Register URLs

```text
https://learn.microsoft.com/en-us/dynamics365/business-central/sales-how-process-sales-returns-cancellations
https://learn.microsoft.com/en-au/dynamics365/business-central/sales-how-correct-cancel-sales-invoice
https://learn.microsoft.com/en-us/dynamics365/business-central/purchasing-ext-doc-no

https://www.odoo.com/documentation/20.0/applications/finance/accounting/customer_invoices/credit_notes.html

https://docs.frappe.io/erpnext/purchase-invoice

https://www.eta.gov.eg/ar/content/e-invoice-services
https://www.eta.gov.eg/ar/content/adlt-almmwlyn-lltaml-m-alfatwrt-alalktrwnyt
https://portal.eta.gov.eg/ar/content/altwqy-alalktrwny

https://zatca.gov.sa/en/E-Invoicing/Introduction/Guidelines/Documents/E-invoicing-Detailed-Technical-Guideline.pdf

https://www.tax.gov.ae/en/content/uae.einvoicing.aspx
https://tax.gov.ae/en/legislation/vat/vat-decree-law.aspx
```

Reference material validates product/regulatory patterns. Country Pack statutory implementation still requires current official legal review and specialist signoff.

---

# 562. Final Architecture

```text
                     ┌──────────────┐
                     │  Directory   │
                     └──────┬───────┘
                            │
                     ┌──────▼───────┐
                     │   Catalog    │
                     └──────┬───────┘
                            │
              ┌─────────────▼─────────────┐
              │       Invoice Draft        │
              └─────────────┬─────────────┘
                            │
           ┌────────────────┼────────────────┐
           ▼                ▼                ▼
       Pricing             Tax             FinBase
           │                │                │
           └────────────────┼────────────────┘
                            ▼
                 Authoritative Finalization
                            │
                            ▼
                  Immutable Invoice Snapshot
                  /          │          \
                 /           │           \
                ▼            ▼            ▼
          Subledger        Ledger      Compliance
          Open Items       Facts       Regulatory
                │            │            │
                └────────────┴────────────┘
                            │
                            ▼
                 Read Models / Rendering
```

---

# 563. Final Business Verdict

For a normal user:

```text
Choose Customer
Add Items
Check Total
Finalize
```

TODIR handles the rest.

For Finance/Tax/Compliance:

Every number and state remains traceable:

```text
Why this price?
Why this tax?
Why this due date?
What accounting was posted?
What is still unpaid?
What did the authority return?
What correction changed the economic result?
```

---

# 564. Final Engineering Verdict

Invoicing is approved only when:

```text
Final financial values are immutable.
Draft calculation is server-authoritative at finalization.
Pricing/Tax/FinBase remain external sources of truth.
Open Items are created by Subledger.
Journal Entries are created by Ledger.
Compliance is asynchronous/state-separated.
Corrections preserve original history.
Numbering/finalization are idempotent.
Historical master-data changes cannot alter old invoices.
Lists/finalization perform under large documents and high concurrency.
```

---

# 565. Next Pack

After Invoicing:

> **`12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR safely record money moving in/out through cash, bank, cards, gateways and transfers; create party credits/advances; integrate with Subledger allocation and Ledger posting; remain idempotent with external providers; and never confuse payment movement with invoice settlement?**

The next pack should lock:

- incoming/outgoing payments;
- cash/bank accounts;
- payment methods;
- payment lifecycle;
- party payments;
- unapplied payments/advances;
- refunds;
- internal transfers;
- fees;
- provider/gateway idempotency;
- bank references;
- payment allocation handoff to Subledger;
- Ledger Facts;
- multicurrency payments;
- security;
- performance;
- future bank reconciliation boundary.

---

**END OF FILE — 11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
