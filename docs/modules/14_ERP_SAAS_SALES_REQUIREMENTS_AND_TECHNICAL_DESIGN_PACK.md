# 14 — TODIR ERP SaaS Sales / Quotation / Commercial Order Requirements & Technical Design Pack

**File:** `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + COMMERCIAL SALES + QUOTATION + OPTIONAL ORDER/FULFILLMENT + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Sales Lead, Finance Lead, Inventory Lead, Backend/Frontend Leads, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **HIGH** — Sales mistakes affect customer commitments, pricing, stock promises, credit exposure, delivery, invoicing and revenue operations.

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
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR Sales owns the **commercial selling workflow**.

It answers:

```text
What did we offer the customer?
At what agreed commercial price?
For which products/services and quantities?
Was the quotation accepted?
Did it become an invoice directly?
Did it become a Sales Order because fulfillment tracking was needed?
What remains commercially open?
What was cancelled or revised?
Which invoice/delivery came from this sale?
```

The core product rule is:

> **The shortest correct workflow is the default. Advanced sales workflow is optional.**

---

# 1. Default TODIR Sales Flow

For a normal small/simple company:

```text
Quotation
→ Edit
→ Accept / Approve
→ Convert to Invoice
```

No mandatory Sales Order.

No mandatory Reservation.

No mandatory Picking.

No mandatory Delivery document.

No mandatory Customer Portal.

---

# 2. Advanced Flow Is Optional

A company that needs operational fulfillment can enable:

```text
Quotation
→ Sales Order
→ Availability
→ Reservation
→ Delivery/Fulfillment
→ Invoice
```

Only when required by its business.

---

# 3. User Experience Rule

> **Minimum steps for the user; full correctness inside the system.**

One button may orchestrate multiple domains.

It must not collapse their ownership boundaries.

---

# 4. Example — Simple Service Company

```text
Quotation
→ Accepted
→ Invoice
```

No Inventory interaction.

---

# 5. Example — Small Trading Company

```text
Quotation
→ Accepted
→ Invoice
   ☑ Deliver / deduct stock now
```

Internally:

```text
Sales conversion
+
Invoicing finalization
+
Inventory Issue
+
Subledger
+
Ledger
```

according to the configured atomicity/legal policy.

---

# 6. Example — Large Distributor

```text
Quotation
→ Accepted
→ Sales Order
→ Reserve
→ Pick
→ Deliver partially
→ Backorder remainder
→ Invoice delivered quantity
```

Same Sales Core, more capabilities enabled.

---

# 7. No Workflow Copying for Its Own Sake

TODIR does not force:

```text
Quotation
→ Order
→ Picking
→ Packing
→ Delivery
→ Invoice
```

merely because another ERP does.

Each stage must exist only if the company needs that business fact/workflow.

---

# 8. Integrity vs Workflow Complexity

Optional:

```text
Internal approval
Sales Order
Credit approval
Reservation
Picking
Delivery document
Backorder
Customer Portal
```

Non-optional integrity:

```text
Correct customer
Correct price evidence
Correct quote/order history
Correct conversion source
No duplicate invoice conversion
No direct stock mutation
No fake payment state
Tenant/entity isolation
Permission enforcement
```

---

# 9. Central Business Question

> **How does TODIR let a company quote, agree and invoice a sale in the fewest possible steps while still supporting optional order, credit, reservation and fulfillment workflows for larger companies without duplicating Pricing, Inventory, Invoicing, Subledger or Payments?**

---

# 10. Sales Owns

Sales owns:

```text
Quotation
Quotation Revision
Quotation Acceptance
Quotation Commercial Terms
Sales Order (optional workflow)
Sales Order Line
Sales Order Amendment
Sales Customer Profile
Sales Channel
Salesperson/Team Assignment
Commercial Fulfillment Intent
Commercial Cancellation
Invoice Eligibility / Source Handoff
Return Commercial Request / orchestration
Sales document history
```

---

# 11. Sales Does NOT Own

```text
Party identity               → Directory
Product/UOM identity         → Catalog
Price Lists/Rules            → Pricing
Tax rules/legal calculation  → Tax
Payment terms definition     → FinBase
Party financial profile      → Subledger
Credit exposure truth        → Subledger
Physical stock               → Inventory
Stock reservation truth      → Inventory
Invoice                      → Invoicing
Open Item/customer balance   → Subledger
Payment movement             → Payments
Journal Entry                → Ledger
Regulatory invoice state     → Compliance/Country Pack
```

---

# 12. Sales Is an Orchestrator

Sales combines decisions from specialized domains.

It does not become their source of truth.

---

# 13. Canonical Simple Architecture

```text
Directory
   ↓
Quotation
   ↓
Pricing + Tax Preview
   ↓
Acceptance
   ↓
Invoicing CreateFromSalesSource
   ↓
Invoice Finalization
   ↓
Subledger + Ledger + Compliance
```

Inventory enters only if the sale includes physical fulfillment.

---

# 14. Canonical Advanced Architecture

```text
Quotation
   ↓
Sales Order
   ├────► Inventory Availability
   ├────► Inventory Reservation
   ├────► Credit Check
   ↓
Fulfillment Intent
   ↓
Inventory Issue
   ↓
Invoice Eligibility
   ↓
Invoicing
```

---

# 15. Customer Is Not a System User

A Customer is:

```text
Directory Party
with Customer Role
```

It is not automatically a TODIR Login.

---

# 16. Customer Portal Is NOT Required

Sales must work completely without Customer Portal.

---

# 17. Customer Acceptance Methods

Quotation acceptance can be recorded from:

```text
Phone
In-person
Email
WhatsApp
Signed paper/PDF
External e-signature
Portal later
Other documented method
```

No customer login required.

---

# 18. Customer Portal Future Boundary

Optional later:

```text
Customer Portal
→ view quotation
→ accept quotation
→ view order
→ view invoice
```

Portal never exposes internal ERP Sales/Inventory/Finance screens.

---

# 19. Internal User Is the Primary Sales Actor

Examples:

```text
Owner
Salesperson
Sales Manager
Branch Manager
Sales Admin
```

---

# 20. Directory Integration

Sales consumes:

```text
Party(Customer Role)
Contacts
Bill-to Address
Ship-to Address
```

Directory remains canonical identity owner.

---

# 21. No Sales Customer Identity Table

Forbidden:

```text
sales_customer.name
sales_customer.tax_id
sales_customer.address as duplicated identity truth
```

Sales stores PartyId + commercial profile + transaction snapshots where required.

---

# 22. Sales Customer Profile

Sales owns commercial/operational defaults only.

Concept:

```text
SalesCustomerProfile
{
    TenantId
    LegalEntityId
    PartyId

    DefaultSalespersonId?
    DefaultSalesTeamId?
    DefaultSalesChannelId?

    DefaultWarehouseId?
    PreferredFulfillmentMode?

    AssignedSalesPriceListId?
    SalesPriceGroupId?

    DefaultQuoteValidityDays?
    CustomerPoRequired?

    Status
    Version
}
```

---

# 23. Financial Defaults Do Not Move Into Sales Profile

Examples owned elsewhere:

```text
Credit Limit       → Subledger
Credit Hold        → Subledger
Payment Term       → Subledger/FinBase
Open Balance       → Subledger
```

Sales consumes them.

---

# 24. Customer Sales Readiness

Sales can ask Directory/other providers:

```text
CanQuote?
CanInvoice?
CanShip?
```

depending action.

---

# 25. Quick Customer Create

Sales can launch Directory Quick Create.

Ask only what current action needs.

---

# 26. Quick Create Does Not Duplicate Directory

The resulting Party remains Directory-owned.

---

# 27. Customer Search Before Create

Sales Party Picker uses Directory search to reduce duplicates.

---

# 28. Customer Block

A blocked/archived customer cannot enter new commercial commitments according to policy.

Historical documents remain visible.

---

# 29. Quotation Is the Primary Sales Document

For TODIR's default workflow, Quotation is the main commercial workspace before invoice.

---

# 30. Quotation Purpose

A Quotation says:

```text
We offer Customer X
these items/services
at these commercial prices/discounts
under these terms
valid until this date if configured
```

It does not mean money is owed.

---

# 31. Quotation Is Not an Invoice

It creates no canonical:

```text
AR Open Item
Journal Entry
Payment obligation
Legal invoice number
```

---

# 32. Quotation Is Not Stock Movement

It does not reduce On Hand.

---

# 33. Quotation Is Not Reservation by Default

Showing availability is not reservation.

Reservation is optional and explicit.

---

# 34. Quotation Header

Concept:

```text
Quotation
{
    Id
    TenantId
    LegalEntityId
    BranchId?

    Number
    PartyId

    QuoteDate
    ValidUntil?

    CurrencyCode

    SalespersonId?
    SalesTeamId?
    SalesChannelId?

    BillToAddressRef?
    ShipToAddressRef?

    RequestedDeliveryDate?
    CustomerReference?

    CommercialState
    CurrentRevisionNumber

    CreatedBy
    CreatedAt
    RowVersion
}
```

---

# 35. Quotation Line

Concept:

```text
QuotationLine
{
    Id
    QuotationId
    Sequence

    ProductVariantId?
    Description

    Quantity
    UomCode

    PricingSnapshot
    AgreedUnitPrice

    DiscountSnapshot?
    TaxPreviewSnapshot?

    RequestedDeliveryDate?
    WarehousePreferenceId?

    SourceContext?
}
```

---

# 36. Ad-Hoc Service Line

Quotation can allow an authorized free-text/ad-hoc line according to policy.

Invoicing finalization still requires proper financial/tax semantics.

---

# 37. Product Line

Uses canonical Catalog Variant + UOM.

No copied Product master.

---

# 38. Quote Description Snapshot

Quotation line can preserve customer-facing description at that revision.

Product rename later does not silently change an already issued/accepted quote.

---

# 39. Quotation Number

Generated by Platform Numbering.

No `MAX(number)+1`.

---

# 40. Quotation Number Is Not Legal Invoice Number

Converting to Invoice creates a separate Invoice number through Invoicing.

---

# 41. Quotation Draft

Draft is editable.

No financial/legal commitment is created merely by autosave.

---

# 42. Draft Autosave

Allowed.

Must not accidentally accept/convert.

---

# 43. Draft Concurrency

Optimistic concurrency:

```text
row_version / ETag
```

Prevents silent overwrite between sales users.

---

# 44. Draft Fields

Normal editable fields:

```text
Customer
Products
Qty
UOM
Commercial price/override
Discount
Validity
Notes
Delivery request
Warehouse preference
```

Advanced fields hidden unless relevant.

---

# 45. Quotation Commercial States

Recommended compact model:

```text
Draft
Open
Accepted
Declined
Expired
Cancelled
Converted
```

---

# 46. No Mandatory `Sent` Lifecycle Gate

A quotation can be accepted from a phone call.

Communication status is separate.

---

# 47. Communication State Separate

Optional projection/history:

```text
NeverSent
Sent
Delivered
Viewed (future portal)
```

It does not block commercial state unless company policy explicitly requires.

---

# 48. Open Quotation

`Open` means ready for customer decision.

It can be shared as PDF/email/WhatsApp externally.

---

# 49. Accepted Quotation

Represents recorded customer agreement to one exact commercial revision.

---

# 50. Declined Quotation

No conversion without reopen/revision.

---

# 51. Expired Quotation

Validity passed.

History remains.

---

# 52. Cancelled Quotation

Internally withdrawn.

---

# 53. Converted Quotation

Accepted commercial source has produced its target:

```text
Invoice
or
Sales Order
```

according to selected workflow.

---

# 54. Quotation Validity Optional

A company may configure:

```text
No Expiry
7 days
15 days
30 days
custom
```

No forced validity date for every business.

---

# 55. Quote Expiry Does Not Delete

Expired quote remains queryable.

---

# 56. Convert After Expiry

Policy:

```text
Block
Warn
Require Authorized Override
Create New Revision
```

Recommended safe default: require revision/authorized confirmation.

---

# 57. Quote Revision

Material edits after an externally meaningful version should preserve history.

---

# 58. Draft Edits vs Revision

Do not create a revision for every keystroke.

Draft remains mutable.

A revision boundary occurs when configured meaningful state is reached.

---

# 59. Recommended Revision Boundary

Once a quote is:

```text
Open/shared
or
Accepted
```

material commercial edits create a new revision.

---

# 60. Revision Content

Freeze enough to answer:

```text
What exactly was offered?
What exactly was accepted?
```

---

# 61. Quotation Revision

Concept:

```text
QuotationRevision
{
    QuotationId
    RevisionNumber

    PartySnapshotRef
    LineSnapshots
    CommercialTotals
    PricingEvidence
    TaxPreviewEvidence
    TermsSnapshot

    CreatedAt
    CreatedBy
}
```

---

# 62. Accepted Revision Immutable

Do not silently edit an accepted revision.

---

# 63. Revise Accepted Quote

UX can still be simple:

```text
[Edit Quote]
```

Internally:

```text
Create Revision N+1
→ previous acceptance remains historical
→ new revision needs acceptance
```

if change is material.

---

# 64. Material Change

Examples:

```text
Product
Qty
Price
Discount
Currency
Commercial terms
Major delivery terms
```

---

# 65. Non-Material Internal Metadata

Internal tags/notes may change without new commercial revision if they do not alter customer agreement.

---

# 66. Acceptance Record

Concept:

```text
QuotationAcceptance
{
    QuotationId
    RevisionNumber

    AcceptedAt
    AcceptanceMethod
    CustomerContactId?
    RecordedByUserId

    EvidenceFileRef?
    ExternalReference?
    Note?
}
```

---

# 67. Acceptance Evidence Optional By Policy

Small company:

```text
Record Accepted
```

Large/controlled company:

```text
Acceptance method/evidence required
```

---

# 68. Acceptance Does Not Require Portal

Critical.

---

# 69. Acceptance Does Not Create Invoice Automatically Unless Configured

Company can choose one-click conversion or separate confirm/convert.

---

# 70. One-Click Accept & Invoice

TODIR can provide:

```text
[Accept & Create Invoice]
```

for authorized users.

Internally it executes explicit acceptance + conversion.

---

# 71. One-Click Accept & Invoice Idempotency

Double-click/retry must not create two invoices.

---

# 72. Conversion Source Identity

Use:

```text
SourceModule = Sales
SourceType = Quotation
SourceDocumentId
SourceRevision
InvoicePurpose
```

with uniqueness where applicable.

---

# 73. Quotation to Invoice — Default

Core TODIR path:

```text
Accepted Quotation
→ Create Invoice Request
→ Invoicing Draft/Finalization
```

No Sales Order required.

---

# 74. Full Conversion Default

Simple direct quote-to-invoice conversion normally converts the agreed commercial revision in full.

---

# 75. Partial/Repeated Fulfillment

If company needs:

```text
partial delivery
partial invoicing
backorders
multiple shipments
```

use optional Sales Order/Fulfillment workflow.

Do not overload simple Quotation conversion with enterprise fulfillment state.

---

# 76. Quotation Conversion Is Not Type Mutation

Do not change:

```text
Quotation.type = Invoice
```

Create a new Invoice owned by Invoicing and preserve typed relationship.

---

# 77. Typed Source Relationship

```text
Invoice OriginatesFrom Quotation Revision
```

---

# 78. Quote Remains Historical After Conversion

Users can navigate:

```text
Quotation QT-100
→ Invoice INV-900
```

---

# 79. Conversion Does Not Copy Live Master Data Blindly

Invoicing finalization resolves required current legal/tax context while preserving agreed commercial source evidence.

---

# 80. Pricing Ownership

Pricing remains the only reusable commercial price resolver.

---

# 81. Sales Pricing Input

Sales supplies:

```text
Party
Variant
Qty
UOM
Currency
PricingDate
Assigned Price List?
Price Group?
Channel
Legal Entity
```

---

# 82. Pricing Response

Sales stores relevant Pricing Snapshot/evidence on quote revision.

---

# 83. No `Sales.PriceRule`

Sales does not copy Pricing rule logic.

---

# 84. Pricing Batch

Quotation with many lines uses:

```text
ResolvePricesBatch
```

No one call per line.

---

# 85. Customer Change

Changing customer on draft triggers batch repricing if policy/context differs.

---

# 86. Quantity Change

Reprice affected line through Pricing.

---

# 87. Currency Change

Reprice/recompute with explicit Pricing/FinBase context.

No hidden conversion.

---

# 88. Manual Price Override

Allowed only with permission/policy.

---

# 89. Price Override Evidence

Store:

```text
ResolvedPrice
OverridePrice
Deviation
Reason?
Actor
ApprovalRef?
```

as required.

---

# 90. Discount

Commercial discount belongs Sales document input/accepted agreement.

Pricing may recommend/resolve discount.

Tax/Invoicing uses final commercial result.

---

# 91. Discount Approval Optional

Policy can:

```text
Allow
Warn
RequireApproval
Block
```

based on deviation/threshold.

No approval step when policy does not require it.

---

# 92. Internal Approval Is Optional

Sales must work with approval capability disabled.

---

# 93. Approval Guard

Optional integration:

```text
IApprovalGuard
```

can evaluate:

```text
Large discount
Price below allowed floor
High quote value
Credit exception
Special terms
```

---

# 94. Approval State Separate From Customer Acceptance

Do not confuse:

```text
InternalApproved
CustomerAccepted
```

---

# 95. Default Small-Company Approval

None unless explicitly configured.

---

# 96. Price Lock Policy

Quotation conversion policy:

```text
HonorAcceptedQuote
RepriceAtConversion
WarnIfCurrentPriceChanged
RequireApprovalIfDifference
```

---

# 97. Recommended Default

For accepted valid quote:

> **Honor the accepted commercial price snapshot unless policy/law explicitly requires another treatment.**

---

# 98. Price List Change After Acceptance

Does not silently reprice accepted quote.

---

# 99. Expired Quote Reprice

Can require new Pricing resolution/revision.

---

# 100. Pricing Snapshot Explainability

Must answer:

```text
Which price list/version?
Which rule/version?
What base/resolved price?
Which discount?
Was there manual override?
Which Pricing resolver version?
```

---

# 101. Quote Future Delivery Date

Does not automatically become PricingDate.

Sales policy explicitly controls pricing date semantics.

---

# 102. Tax Preview

Quotation can show tax-inclusive/exclusive estimated totals using Tax Engine.

---

# 103. Tax Preview Is Not Legal Invoice Tax Truth

Invoice finalization remains authoritative legal/financial tax calculation.

---

# 104. Quote Tax Evidence

Store Tax Preview snapshot/version for explaining what customer saw.

---

# 105. Tax Change Between Quote and Invoice

TODIR detects material difference.

Policy/UI must make it visible.

---

# 106. No Sales VAT Formula

Sales never calculates VAT independently.

---

# 107. Commercial Total vs Invoice Final Total

Usually expected to match, but legal/tax/date changes can create controlled differences.

TODIR must explain them.

---

# 108. Quote Total Calculation

Uses:

```text
Pricing
Discount/Charge policy
Tax Preview
FinBase precision/rounding
```

server-authoritative.

---

# 109. Draft Frontend Calculation

Frontend can preview.

Backend remains authoritative for Open/Accepted revision.

---

# 110. Quote Commercial Currency

One document currency initially.

Mixed line currency forbidden unless future explicit design.

---

# 111. Money Types

C# decimal/PostgreSQL numeric.

Financial decimals serialized safely.

No JavaScript Number as authoritative financial calculation.

---

# 112. Delivery Address

Sales references Directory Ship-to.

Quote revision can snapshot customer-facing delivery address/text where necessary.

---

# 113. Billing Address

Referenced/snapshotted for quote presentation.

Invoice finalization obtains appropriate legal billing snapshot through Invoicing/Directory rules.

---

# 114. Requested Delivery Date

Commercial request only.

It does not mean inventory is reserved or shipped.

---

# 115. Customer Reference

Support:

```text
Customer PO Number
Email Reference
Contract Reference
Tender Reference
```

without making it mandatory.

---

# 116. Salesperson

Optional assignment for ownership/reporting.

---

# 117. Sales Team

Optional.

Small company can ignore teams.

---

# 118. Sales Channel

Optional dimension:

```text
Direct
Wholesale
Retail
Phone
Ecommerce
Marketplace
POS
Other
```

---

# 119. Channel Does Not Create Separate Sales Engine

Same Quotation/Sales Core.

---

# 120. Branch

Sales document can be scoped to Branch/Legal Entity according to organization rules.

---

# 121. Legal Entity

Every financial sale belongs one Legal Entity.

Cross-entity sale cannot be a single invoice/order fact.

---

# 122. Multi-Branch

Branch can influence:

```text
Salesperson
Warehouse default
Number series
Channel
Permissions
```

without owning accounting identity.

---

# 123. Service Sale

No Inventory calls needed.

---

# 124. Non-Stock Goods

Can be invoiced without stock movement according to Inventory Profile.

---

# 125. Stocked Goods

Sales can query Inventory.

---

# 126. Availability Is Optional UX for Quote

A quote can show availability when Inventory active.

Quote does not require availability check for service/non-stock lines.

---

# 127. Availability Is Not Promise Unless Policy Says So

Display:

```text
Available now: 6
```

is informational unless reserved/committed.

---

# 128. Availability Refresh

Old quote preview cannot be trusted forever.

Refresh at commitment/delivery decision point.

---

# 129. Inventory Availability Contract

Use:

```text
Todir.Inventory.Contracts
IInventoryAvailability
```

batch-capable.

---

# 130. No Sales Stock Table Read

Forbidden.

---

# 131. Availability Batch

100 lines → one/bounded batch call, not 100 independent calls.

---

# 132. Availability Display

Simple:

```text
Available 24
```

Advanced expand:

```text
Warehouse A 10
Warehouse B 14
In Transit 5
Expected 20
```

---

# 133. Inventory Not Active

Sales does not show stock fields.

Quotation/invoice flow remains fully usable.

---

# 134. Default Warehouse

Sales Profile/Branch can suggest Warehouse.

Inventory remains authority.

---

# 135. Warehouse Selection Optional

Service company never sees it.

---

# 136. Reservation Is Optional

Reservation modes come from Inventory policy:

```text
Disabled
Advisory
Enforced
```

---

# 137. Quotation Does Not Reserve By Default

A company may enable quote-level hold later, but not baseline.

---

# 138. Sales Order Can Request Reservation

Inventory creates/resolves Reservation.

Sales stores reference/projection only.

---

# 139. Reservation Does Not Reduce On Hand

Locked Inventory invariant.

---

# 140. Release Reservation

Sales cancellation/amendment requests release.

Inventory owns release state.



# 141. Sales Order Is Optional

TODIR does not require Sales Order for every sale.

---

# 142. When Sales Order Adds Real Value

Use Sales Order when company needs:

```text
Commercial commitment separate from invoice
Partial fulfillment
Backorder
Multiple deliveries
Reservation
Delivery scheduling
Order amendment
Credit approval
Order-level tracking
```

---

# 143. When Sales Order Adds No Value

If company workflow is simply:

```text
Quote
→ Invoice
```

do not insert an Order screen.

---

# 144. Direct Sales Order

A company can create Sales Order without Quotation.

Useful for repeat/known customers.

---

# 145. Quotation to Sales Order

Creates a new Sales Order from accepted revision.

Does not mutate Quotation type.

---

# 146. Sales Order Header

Concept:

```text
SalesOrder
{
    Id
    TenantId
    LegalEntityId
    BranchId?

    Number
    PartyId

    OrderDate
    CurrencyCode

    SalespersonId?
    SalesTeamId?
    SalesChannelId?

    CustomerReference?

    CommercialState
    FulfillmentState
    InvoicingStateProjection
    CreditStateProjection

    SourceQuotationId?
    SourceQuotationRevision?

    RowVersion
}
```

---

# 147. Sales Order Line

Concept:

```text
SalesOrderLine
{
    Id
    SalesOrderId
    Sequence

    ProductVariantId?
    Description

    OrderedQuantity
    UomCode

    AgreedPricingSnapshot

    RequestedDeliveryDate?
    WarehousePreferenceId?

    CancelledQuantity
    FulfilledQuantityProjection
    InvoiceEligibleQuantityProjection
    InvoicedQuantityProjection

    RowVersion
}
```

---

# 148. Commercial Ordered Quantity Is Sales Truth

Sales owns:

```text
Ordered Qty
Cancelled Qty
Commercial Remaining Qty
```

---

# 149. Physical Fulfillment Quantity Comes From Inventory

Sales can maintain a strongly consistent projection/reference.

Inventory remains physical source truth.

---

# 150. Invoice Quantity Comes From Invoicing

Sales can maintain conversion/invoiced projection.

Invoice remains Invoicing truth.

---

# 151. Order Commercial State

Recommended:

```text
Draft
Confirmed
PartiallyCompleted
Completed
Cancelled
Closed
```

Keep compact.

---

# 152. Fulfillment State Separate

Example:

```text
NotRequired
NotStarted
PartiallyFulfilled
Fulfilled
Blocked
```

---

# 153. Invoicing State Projection Separate

Example:

```text
NotInvoiced
PartiallyInvoiced
Invoiced
```

Projection/reference only.

---

# 154. Settlement Does Not Belong Sales Order State

Do not mark Sales Order itself as authoritative `Paid`.

Payment/settlement belongs Subledger/Payments.

Sales UI may display payment summary projection.

---

# 155. Credit State Separate

Example projection:

```text
NotChecked
Allowed
Warning
ApprovalRequired
Blocked
ApprovedException
```

Source decision comes Subledger/Credit policy.

---

# 156. Why Separate States

A valid order can be:

```text
Commercial = Confirmed
Fulfillment = PartiallyFulfilled
Invoicing   = PartiallyInvoiced
Settlement  = PartiallyPaid (external projection)
```

No overloaded `Status`.

---

# 157. Confirm Order

Commercial confirmation means:

> Company accepted the customer order commitment.

It does not itself mean stock moved.

---

# 158. Confirm Order Does Not Reduce On Hand

Inventory invariant.

---

# 159. Confirm Order Can Request Reservation

If policy enabled.

---

# 160. Confirm Order Can Run Credit Check

If credit policy enabled.

---

# 161. Confirm Order Without Inventory

Service order can confirm normally.

---

# 162. Confirm Order Without Credit Control

Cash/simple company can confirm without unused credit workflow.

---

# 163. Confirmation Validation Is Contextual

Validate only capabilities required for this order.

Do not block service sale because no Warehouse exists.

---

# 164. Order Amendment

After confirmation, material changes use controlled amendment.

---

# 165. Amendment Purpose

Preserve:

```text
Original commitment
Change
Reason
Actor
Effects on reservation/fulfillment/invoicing
```

---

# 166. Simple Amendment UX

User can click:

```text
[Edit Order]
```

TODIR creates controlled amendment internally where history requires.

---

# 167. Draft Order Edit

Before confirmation, normal edits.

No amendment object needed for every keystroke.

---

# 168. Confirmed Order Qty Increase

May require:

```text
Repricing
Credit recheck
Availability recheck
Reservation increase
Approval
```

only as configured.

---

# 169. Confirmed Order Qty Decrease

Must respect already:

```text
Fulfilled Qty
Invoiced Qty
```

Cannot reduce below irreversible committed result without correction workflow.

---

# 170. Cancel Remaining Quantity

Example:

```text
Ordered   100
Fulfilled  40
Cancelled  60
```

Valid.

---

# 171. Do Not Cancel Fulfilled History

Cancellation normally applies remaining commitment.

---

# 172. Cancellation Releases Reservation

Sales requests Inventory release for unconsumed reservation.

---

# 173. Cancellation After Invoice

Requires invoice correction/credit rules.

Sales cannot silently change invoice truth.

---

# 174. Cancellation Reason

Typed/required according to policy.

---

# 175. Backorder

Backorder is remaining commercial demand not currently fulfilled.

Sales owns the business interpretation.

---

# 176. Inventory Does Not Own Backorder

Inventory returns availability/reservation/fulfillment facts.

Sales decides how remaining demand is handled.

---

# 177. Partial Fulfillment

First-class in Sales Order workflow.

---

# 178. Fulfillment Request

Sales requests Inventory to issue/deliver approved quantities.

---

# 179. Fulfillment Source

Potential:

```text
SalesOrderId
SalesOrderLineId
RequestedQty
Warehouse
Delivery Context
```

---

# 180. Inventory Issue Result

Sales receives:

```text
Movement/Operation Ref
Fulfilled Quantity
Lot/Serial references if needed for trace
Effective Date
```

---

# 181. No Duplicate Fulfillment

Source idempotency prevents same order line fulfillment command from issuing twice.

---

# 182. Fulfillment Quantity Rule

For line:

```text
Fulfilled
+
Cancelled
<=
Ordered
```

unless explicit over-delivery policy.

---

# 183. Over-Delivery

Optional controlled policy.

Default:

```text
Disallow
```

---

# 184. Over-Delivery Tolerance

Large companies may configure:

```text
Allow up to X%
Require Approval
```

Sales/Inventory coordinate exact quantity.

---

# 185. Fulfillment Does Not Require Invoice First

Valid:

```text
Deliver
→ Invoice later
```

if company workflow/legal policy permits.

---

# 186. Invoice Does Not Require Fulfillment First

Valid:

```text
Invoice
→ Deliver later
```

for businesses where allowed.

---

# 187. Billing Basis

Company/order policy can be:

```text
ImmediateFromQuote
OrderConfirmedQuantity
DeliveredQuantity
ManualSelectedQuantity
```

Milestone/progress billing later where needed.

---

# 188. Default TODIR Billing Basis

For simple quote workflow:

```text
ImmediateFromQuote
```

---

# 189. Advanced Distribution Default

Likely:

```text
DeliveredQuantity
```

when company explicitly enables delivery-based invoicing.

---

# 190. Invoice Eligibility

Sales calculates commercial/source eligibility from its own commitment + fulfillment facts.

Invoicing creates the Invoice.

---

# 191. Invoice Eligibility Is Not Invoice

Do not create fake invoice records inside Sales.

---

# 192. Create Invoice Request

Concept:

```text
CreateSalesInvoiceRequest
{
    SourceType
    SourceId
    SourceRevision?

    PartyId
    LegalEntityId

    SourceLines[]
    QuantityToInvoice

    CommercialPricingSnapshots
    CustomerReference
    DeliveryRefs?
}
```

Invoicing resolves/finalizes authoritative document.

---

# 193. Quote-to-Invoice Request

Uses accepted Quotation Revision.

---

# 194. Order-to-Invoice Request

Uses eligible Sales Order lines/quantities.

---

# 195. Idempotent Invoice Creation

Same source + same purpose/quantity slice cannot accidentally create duplicate Invoice.

---

# 196. Partial Order Invoicing

Supported if Sales Order workflow enabled.

Example:

```text
Ordered 100
Delivered 60
Invoice 60
Remaining eligible later 40
```

---

# 197. Multiple Invoices

One Sales Order can relate to multiple Invoices.

---

# 198. One Invoice Across Multiple Sales Orders

Possible later/optional consolidation policy.

Do not force MVP complexity unless required.

---

# 199. Quote Direct Invoice Usually One-to-One Commercially

Simple baseline:

```text
Accepted Quote Revision
→ one Customer Invoice
```

Corrections happen through Invoicing.

---

# 200. Quotation Conversion Marker

After successful source conversion:

```text
ConvertedTo = Invoice/Order
TargetRef
ConvertedAt
ConvertedBy
```

---

# 201. Conversion Rollback

If target creation fails, Quotation must not become falsely Converted.

---

# 202. Atomic Internal Conversion

Where modules share Transaction Coordinator, source conversion marker + target source identity commit atomically when feasible.

---

# 203. External Compliance Not Inside Conversion Transaction

Country authority calls remain asynchronous/state-separated.

---

# 204. Quote & Deliver Now

For stocked item small-company UX:

```text
[Accept & Invoice]
☑ Deliver stock now
```

---

# 205. Deliver-Now Composite Action

Internally:

```text
Record quote acceptance
Create/finalize Invoice
Create Inventory Issue
Create required Subledger/Ledger facts
Record source links
Outbox
```

according to configured legal/atomic workflow.

---

# 206. Deliver-Now Failure

If physical issue is required by the user's chosen action and cannot occur:

do not report complete success.

---

# 207. Stock Shortage During Deliver Now

UX:

```text
Requested 10
Available 6
```

Offer only policy-valid options:

```text
Invoice 6
Invoice all without delivery if allowed
Create Order/Backorder
Choose Warehouse
Cancel
```

---

# 208. Do Not Auto-Negative-Stock to Keep UX Fast

Speed never means corrupt inventory.

---

# 209. Quote & Invoice Without Stock Movement

For:

```text
Service
Non-stock item
Future delivery
Company policy
```

valid.

---

# 210. Direct Invoice Without Quotation Still Exists

Invoicing supports it independently.

Sales module is not mandatory for ad-hoc invoicing.

---

# 211. Why Sales Still Matters

Sales adds:

```text
Quotation history
Commercial acceptance
Salesperson ownership
Order/fulfillment workflow
Customer commercial reporting
Optional credit/availability orchestration
```

---

# 212. Credit Control Is Optional

Sales should not force a credit screen for every company.

---

# 213. Credit Truth Owner

Subledger owns:

```text
Financial Exposure
Credit Limit
Credit Hold Policy
```

---

# 214. Credit Check Contract

Canonical:

```text
CheckCredit(
    party,
    legalEntity,
    proposedExposure
)
→ Allow | Warn | Block | RequireApproval
```

---

# 215. Sales Does Not Compute Customer Balance

Forbidden:

```text
sum(invoices) - sum(payments)
```

inside Sales.

---

# 216. Proposed Exposure

Sales/Invoicing provides proposed commercial amount/context.

Subledger evaluates against current financial exposure/policy.

---

# 217. When Credit Check Runs

Configurable decision points:

```text
Quotation acceptance
Sales Order confirmation
Invoice conversion
Delivery
```

Do not run every point unless business requires.

---

# 218. Recommended Default

If credit control enabled:

run at the point where company creates material credit commitment.

---

# 219. Cash Sale

Can bypass credit exposure according to payment/credit policy.

---

# 220. Credit Warn

User can proceed with permission.

---

# 221. Credit Block

Cannot proceed unless policy provides exception route.

---

# 222. Credit Approval

Optional.

Can create approval request without duplicating credit data.

---

# 223. Credit Exception Evidence

Store:

```text
Decision
Exposure snapshot/ref
Reason
Approver
Timestamp
```

in Sales decision history where needed.

---

# 224. Credit State Is Time-Sensitive

Old quote check is not permanent truth.

Refresh at commitment decision point.

---

# 225. Customer Payment Summary in Sales

Sales UI may display:

```text
Outstanding
Overdue
Credit Limit
Available Credit
```

from Subledger read contract.

Read-only projection.

---

# 226. Sales Never Sets Paid

Forbidden.

---

# 227. Sales Never Creates Payment Movement

Payments owns money movement.

---

# 228. Quick "Receive Payment" UX

A Sales/Invoice screen may deep-link/orchestrate Payments command.

Sales does not own resulting payment.

---

# 229. Payment Status on Sales Screens

Projection/reference only.

---

# 230. Returns

Sales can own commercial return request/orchestration from original sale.

---

# 231. Simple Return UX

From original sale:

```text
[Return]
→ choose item/qty
→ reason
→ receive stock? yes/no by item/workflow
→ credit/refund path
```

---

# 232. Return Is Not One Database Mutation

Potential domains:

```text
Sales Return Request
Inventory Return Receipt
Invoicing Credit Note
Payments Refund
```

---

# 233. One UX, Multiple Correct Domains

User should not manually recreate all four documents if workflow can safely orchestrate them.

---

# 234. Return Without Stock

Valid for:

```text
Service credit
Price adjustment
Non-returnable/disposed goods
Financial-only correction
```

---

# 235. Stock Return Without Immediate Credit

Can be valid for:

```text
Inspection pending
Warranty assessment
Quality review
```

advanced workflow.

---

# 236. Sales Return Request

Concept:

```text
SalesReturnRequest
{
    OriginalSalesSource
    PartyId
    Lines
    RequestedQty
    Reason
    DesiredResolution
    Status
}
```

---

# 237. Return Quantity Validation

Cannot return more than eligible delivered/sold quantity without explicit exceptional process.

---

# 238. Return History

Original sale remains unchanged.

Return/correction links append history.

---

# 239. Refund

Payments owns money movement.

---

# 240. Credit Note

Invoicing owns financial/legal correction.

---

# 241. Restock

Inventory owns physical return.

---

# 242. Sales Exchange

Future flow:

```text
Return old
+
new sale
```

Do not hide both truths in one mutable document.

---

# 243. Sales Document Attachments

Via Platform Files.

Examples:

```text
Customer PO
Signed quote
Tender document
Technical scope
Acceptance email export
```

---

# 244. Attachment Is Not Acceptance by Itself

Acceptance state requires explicit command/record.

---

# 245. Customer-Facing Notes

Separate from internal notes.

---

# 246. Internal Notes

Never copied to invoice/customer document unless explicitly mapped.

---

# 247. Terms Text

Can reference templates/snippets.

Accepted revision freezes customer-facing terms text/version.

---

# 248. Quote PDF

Rendering is presentation artifact.

Quotation structured data remains source truth.

---

# 249. PDF Regeneration

Historical accepted revision should render from frozen revision data/template version policy.

---

# 250. Email / WhatsApp Sending

Communication service/integration.

Sales records delivery metadata/ref where useful.

External API calls never inside Sales DB transaction.

---

# 251. Communication Failure

Does not corrupt Quotation.

Can retry.

---

# 252. "Sent" Is Not "Accepted"

Never infer acceptance merely from successful delivery.

---

# 253. Acceptance by Employee Recording

Authorized salesperson can record external acceptance.

---

# 254. Acceptance Permission

Separate from quote editing if company requires.

---

# 255. Quote Approval vs Customer Acceptance

Independent:

```text
Internal approval
Customer acceptance
```

A small company may use neither internal approval nor portal.

---

# 256. Salesperson Assignment

Can default from:

```text
Customer Sales Profile
Branch
Current user
Sales team routing
```

---

# 257. Sales Ownership

Salesperson assignment is operational ownership, not security by itself.

Permissions/scopes determine access.

---

# 258. Sales Team

Can group users for routing/reporting.

Do not duplicate IAM roles.

---

# 259. Territory/Region

Optional reporting/routing dimension.

Not required for MVP if no customer need.

---

# 260. Commission Boundary

Future Commission capability consumes finalized sales/payment facts.

Sales does not hardcode commission accounting.

---

# 261. CRM Boundary

CRM owns:

```text
Lead
Opportunity
Pipeline
Activities
```

Sales owns:

```text
Quotation
Sales Order
```

---

# 262. CRM Is Optional

TODIR Sales works without CRM.

---

# 263. Opportunity to Quotation

Future CRM can create Quotation with source ref.

---

# 264. Quotation Without Opportunity

Default/common.

---

# 265. Subscription Boundary

Recurring contracts/subscriptions can later request Invoices/Sales context.

Do not make Sales Quote engine a subscription engine.

---

# 266. POS Boundary

POS owns rapid retail checkout/session.

Can use Pricing/Inventory/Invoicing/Payments.

Do not force POS through Quotation.

---

# 267. Ecommerce Boundary

Channel orders can create Sales Order/Invoice requests through stable APIs.

No separate stock/pricing truth.

---

# 268. Marketplace Boundary

External order ID stored as source reference.

Idempotent import mandatory.

---

# 269. Import Sales Orders

Possible for migration/integration.

Must use source external key.

---

# 270. Import Quotations

Possible but lower priority.

---

# 271. Duplicate External Order

Same external source key must not create second Sales Order.

---

# 272. Sales Channel Mapping

External channel maps to internal:

```text
Legal Entity
Branch
Price Context
Warehouse preference
Tax context
```

through configuration.

---

# 273. Source-of-Truth Rule

External platform order can be source event, but once imported TODIR Sales owns internal commercial order state.

---

# 274. Sales History Principle

> **Every material commercial decision must remain explainable without rewriting the past.**

---

# 275. History Is Typed

Use:

```text
QuotationRevision
QuotationAcceptance
SalesOrderAmendment
SalesDocumentStateHistory
ConversionLink
FulfillmentLink
InvoiceLink
ApprovalDecision
Audit
```

not one generic free-text history table.

---

# 276. Quote History Must Answer

```text
Who created?
What was offered?
What changed?
Which revision was accepted?
How was acceptance recorded?
When did it convert?
To which Invoice/Order?
```

---

# 277. Sales Order History Must Answer

```text
Original order
Amendments
Confirmed quantity
Cancelled quantity
Fulfilled quantity
Reservations
Invoices
Returns
Approvals
Actors/timestamps
```

---

# 278. No History Rewrite

Customer/product/profile changes do not rewrite old accepted commercial documents.

---

# 279. Party Snapshot Scope

Quote revision stores only customer-facing/historical fields needed.

Do not copy entire Directory object.

---

# 280. Product Snapshot Scope

Store:

```text
VariantId
SKU snapshot
Name/description snapshot
UOM snapshot
```

as necessary for historical quote.

---

# 281. Pricing Snapshot Scope

Store relevant result/evidence, not full Pricing rules.

---

# 282. Tax Preview Snapshot Scope

Store calculated preview summary/version/evidence.

Invoice remains legal authority.

---

# 283. Conversion Link

Typed:

```text
QuotationRevision → Invoice
QuotationRevision → SalesOrder
SalesOrderLine → InvoiceLine
SalesOrderLine → Inventory Operation
SalesReturn → CreditNote/ReturnReceipt/Refund
```

---

# 284. No Generic `related_document_text`

Use typed references + external display reference separately.

---

# 285. Commercial State History

Append-only transitions:

```text
Draft → Open → Accepted → Converted
```

or other valid path.

---

# 286. State Transition Commands

No public generic `SetStatus`.

---

# 287. Valid Transition Example

```text
Open → Accepted
```

requires current revision and policy checks.

---

# 288. Invalid Transition Example

```text
Converted → Draft
```

forbidden.

Use new/corrective workflow.

---

# 289. Reopen Declined Quote

Creates new Open revision/explicit command.

---

# 290. Reopen Expired Quote

Requires new validity/revision depending policy.

---

# 291. Quote Clone

Convenience action:

```text
Duplicate as New Draft
```

Creates new Quotation ID/number.

---

# 292. Clone Does Not Copy Acceptance

Never.

---

# 293. Repeat Customer Sale

Can clone previous quote/order as draft.

Reprice/revalidate according to policy.

---

# 294. "Use Same Prices" Option

Allowed only as explicit commercial override/policy, preserving evidence.

---

# 295. Sales Search

Search by:

```text
Quotation/Order Number
Customer
Customer Reference
Product
Salesperson
Branch
Status
Date
```

server-side.

---

# 296. Quotation List

Default columns:

```text
Number
Customer
Date
Total
State
Salesperson
```

Advanced columns hidden.

---

# 297. Sales Order List

Only shown if Sales Order capability enabled.

---

# 298. Simple Company Menu

Could be:

```text
Sales
├── Quotations
└── Sales History
```

Invoices remain in Invoicing application or linked view.

No empty complex menus.

---

# 299. Advanced Company Menu

Could add:

```text
Sales Orders
Fulfillment
Backorders
Approvals
Returns
```

according to capabilities.

---

# 300. Product Workspace Shortcut

`Create Quote` from customer/product context.

---

# 301. Customer Workspace

Can show:

```text
Open Quotations
Recent Orders
Invoices summary
Outstanding summary
```

through cross-module read composition.

---

# 302. Cross-Module UI Composition

Frontend composes stable query contracts/read models.

It does not grant backend data ownership to Sales.

---

# 303. No Giant Customer Aggregate Load

Do not load full:

```text
Party + 5 years orders + invoices + payments + stock
```

on page load.

---

# 304. Lazy Tabs

Load:

```text
Quotes
Orders
Invoices
Payments
History
```

on demand.

---

# 305. Quote Editor Performance

Use:

```text
Product search server-side
Pricing batch
Tax preview batch
Availability batch
```

---

# 306. No N+1

100-line quote cannot trigger independent per-line:

```text
Catalog
Pricing
Tax
Inventory
```

requests.

---

# 307. Product Picker

Lightweight Catalog projection.

---

# 308. Pricing on Visible Products

Only batch resolve where UI actually needs customer price display.

---

# 309. Availability on Quote Lines

Batch after selected products, not every Catalog search result by default.

---

# 310. Tax Preview

Debounced/batched after relevant commercial changes.

---

# 311. Autosave

Do not autosave on every keystroke without debounce/batching.

---

# 312. Optimistic UI

Can improve perceived speed for draft edits.

Server result remains authoritative.

---

# 313. Conversion Performance

Quote→Invoice should avoid serial repeated lookups.

Resolve contexts once/batch.

---

# 314. Typical Quote Save Target

Initial measured target:

```text
p95 <= 150–250ms
```

for normal 1–20 line draft save excluding external communication.

---

# 315. Quote Preview Target

Initial measured target:

```text
p95 <= 200–350ms
```

for normal Pricing+Tax preview with warm reference caches.

Benchmark, do not promise blindly.

---

# 316. Quote-to-Invoice Conversion Target

Internal create/finalize path target measured separately from external compliance.

Correctness first.

---

# 317. External Email/WhatsApp Latency

Never counted as DB transaction duration.

Async after commit.

---

# 318. Large Quote

500 lines supported without N+1.

---

# 319. Very Large Quote UX

Virtualized grid/server validation.

Do not render thousands of heavy controls at once.

---

# 320. Quote Import

Excel import may populate lines for large tenders.

Validate before commit.



# 321. Quote Import Does Not Bypass Pricing Policy

Imported explicit price is treated as:

```text
Requested/Override Price
```

and validated/recorded according to Sales/Pricing policy.

---

# 322. Sales Export

Authorized export:

```text
Quotations
Sales Orders
Sales Lines
Sales History
```

server-generated for large datasets.

---

# 323. Reporting Ownership

Sales reports commercial activity.

Finance reports accounting/revenue.

Inventory reports stock.

Do not rebuild finance/stock truth in Sales.

---

# 324. Core Sales Reports

```text
Quotation List
Quotation Conversion
Accepted/Declined Quotes
Sales by Customer
Sales by Product
Sales by Salesperson
Sales by Branch
Sales by Channel
Open Sales Orders
Backorders
Cancelled Order Qty
Order Fulfillment
Return Requests
```

with correct source boundaries.

---

# 325. Quotation Conversion Metric

Possible:

```text
Accepted or Converted Quotations / Opened Quotations
```

Business definition must be explicit.

---

# 326. Revenue Is Not Quotation Total

Do not report quote/order value as accounting revenue.

---

# 327. Order Book Value

Can report outstanding commercial committed value.

Label clearly as order book, not revenue.

---

# 328. Sales Dashboard — Simple

```text
Quotes awaiting decision
Accepted this month
Converted to invoice
Recent customers
```

---

# 329. Sales Dashboard — Advanced

Can add:

```text
Open orders
Backorders
Credit blocks
Fulfillment delays
Order conversion
```

---

# 330. Sales Schema

Recommended:

```text
business_sales
```

---

# 331. Sales DbContext

Owns Sales tables only.

No cross-module EF navigation.

---

# 332. Core Tables

Potential:

```text
sales_customer_profile

quotation
quotation_revision
quotation_revision_line
quotation_acceptance

sales_order
sales_order_line
sales_order_amendment
sales_order_amendment_line

sales_document_state_history
sales_conversion_link

sales_return_request
sales_return_request_line

sales_channel
sales_team
sales_team_member_ref

sales_source_external_ref
sales_list_projection
sales_order_list_projection
```

Exact table split can be refined without changing ownership rules.

---

# 333. Quotation Table

Stores current operational header and pointer to current revision.

---

# 334. Quotation Revision Table

Immutable once sealed/opened according to policy.

---

# 335. Quotation Revision Line

Historical commercial lines.

Do not overwrite accepted line facts.

---

# 336. Acceptance Table

Explicit evidence of accepted revision.

---

# 337. Sales Order Table

Exists only for tenants/workflows using order capability.

Schema still supported in one Sales module.

---

# 338. Sales Conversion Link

Typed relationship:

```text
source_type
source_id
source_revision?
target_module
target_type
target_id
purpose
created_at
```

---

# 339. Source Idempotency

Unique constraints where business semantics require one target.

---

# 340. External Source Reference

For ecommerce/marketplace/import:

```text
source_system
external_id
external_version?
```

unique in configured scope.

---

# 341. Tenant Denormalization

Child rows include Tenant/LegalEntity where useful for RLS/performance.

---

# 342. Legal Entity FK Safety

A Sales document cannot reference Party/Warehouse context from an unauthorized entity scope.

---

# 343. RLS

All tenant-owned Sales tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 344. Runtime Role

No ownership/BYPASSRLS.

---

# 345. Transaction Tenant Context

Explicit transaction:

```text
SET LOCAL app.tenant_id = ...
```

according to Platform Kernel rules.

---

# 346. UUIDv7

Use for Sales IDs.

---

# 347. Numeric Types

```text
C# decimal
PostgreSQL numeric
```

No floating point for prices/totals.

---

# 348. Timestamps

NodaTime / UTC storage + Legal Entity timezone for business date semantics.

---

# 349. Row Version

Optimistic concurrency on mutable Draft/current aggregate.

---

# 350. JSONB

Allowed for bounded versioned snapshots/evidence.

Do not bury:

```text
customer
status
date
total
salesperson
source ids
```

inside JSONB.

---

# 351. Sales Contract Assembly

Locked naming:

```text
Todir.Sales.Contracts
```

---

# 352. No Implementation Dependency

Other modules depend on Sales Contracts, not Sales implementation assembly.

---

# 353. Sales Consumes Contracts

Examples:

```text
Todir.Directory.Contracts
Todir.Catalog.Contracts
Todir.Pricing.Contracts
Todir.Tax.Contracts
Todir.FinBase.Contracts
Todir.Subledger.Contracts
Todir.Invoicing.Contracts
Todir.Inventory.Contracts
Todir.Payments.Contracts (read/action integration where needed)
```

---

# 354. Contract Dependency Direction

Keep hard dependency DAG.

No synchronous re-entrant cycles.

---

# 355. Sales API Base

```text
/api/v1/sales
```

---

# 356. Quotation APIs

Representative:

```text
GET  /quotations
POST /quotations
GET  /quotations/{id}
PUT  /quotations/{id}

POST /quotations/{id}/open
POST /quotations/{id}/accept
POST /quotations/{id}/decline
POST /quotations/{id}/revise
POST /quotations/{id}/cancel

POST /quotations/{id}/convert-to-invoice
POST /quotations/{id}/convert-to-order
```

---

# 357. Quote Preview

```text
POST /quotations/{id}/preview
```

resolves server-authoritative commercial preview.

---

# 358. Quote Send

Optional:

```text
POST /quotations/{id}/send
```

delegates communication.

Not required before accept.

---

# 359. Sales Order APIs

Only if order capability active:

```text
GET  /orders
POST /orders
GET  /orders/{id}
PUT  /orders/{id}

POST /orders/{id}/confirm
POST /orders/{id}/amend
POST /orders/{id}/cancel-remaining
POST /orders/{id}/reserve
POST /orders/{id}/release-reservation
POST /orders/{id}/fulfill
POST /orders/{id}/create-invoice
```

---

# 360. Return APIs

```text
POST /returns
GET  /returns/{id}
POST /returns/{id}/approve
POST /returns/{id}/execute
```

only if Sales Return workflow enabled.

---

# 361. No Generic `SetStatus`

Forbidden.

---

# 362. No Generic `SetPaid`

Forbidden.

---

# 363. No Generic `SetDelivered`

Sales fulfillment state changes from Inventory result/valid command.

---

# 364. No Generic `SetStock`

Forbidden.

---

# 365. No Generic `SetPriceRule`

Forbidden.

---

# 366. Quotation Command DTO

Accept user-editable inputs only.

No client-supplied authoritative:

```text
resolved tax
credit result
stock balance
payment status
journal account
```

---

# 367. Server Authoritative Preview

Client sends intent.

Server resolves:

```text
Pricing
Tax preview
Commercial totals
Optional availability
Optional credit preview
```

---

# 368. Accept Command

Concept:

```text
AcceptQuotation
{
    QuotationId
    RevisionNumber
    AcceptanceMethod
    AcceptedAt?
    CustomerContactId?
    EvidenceRef?
    IdempotencyKey
}
```

---

# 369. Convert Quote to Invoice Command

Concept:

```text
ConvertQuotationToInvoice
{
    QuotationId
    AcceptedRevision
    ConversionMode
    DeliverNow?
    WarehouseId?
    IdempotencyKey
}
```

---

# 370. Conversion Modes

Potential:

```text
CreateInvoiceDraft
FinalizeInvoice
FinalizeInvoiceAndDeliver
```

Only expose modes permitted by company/legal policy.

---

# 371. Safe Default Conversion Mode

Company configurable.

For simple operations:

```text
FinalizeInvoice
```

can be default.

For strict review:

```text
CreateInvoiceDraft
```

---

# 372. Conversion Result

Return typed refs:

```text
QuotationId
InvoiceId
InvoiceNumber?
InventoryOperationId?
Warnings[]
```

---

# 373. Order Confirm Command

Includes expected row version + idempotency key.

---

# 374. Order Fulfill Command

Includes:

```text
OrderLine
Qty
Warehouse?
DeliveryDate
IdempotencyKey
```

Inventory validates actual stock.

---

# 375. No Trusting Client Availability

Backend refreshes Inventory at reservation/issue.

---

# 376. No Trusting Client Credit Result

Backend refreshes Subledger decision when required.

---

# 377. No Trusting Client Price Snapshot IDs Alone

Validate snapshot/context belongs current document/tenant/entity and is eligible.

---

# 378. Permission Model

Permissions = authority.

Role = bundle.

UX derives from effective permissions + capabilities.

---

# 379. Core Permissions

Potential:

```text
sales.quotation.read
sales.quotation.create
sales.quotation.edit
sales.quotation.open
sales.quotation.accept
sales.quotation.decline
sales.quotation.revise
sales.quotation.cancel
sales.quotation.convert_invoice
sales.quotation.convert_order

sales.price_override
sales.discount_override
sales.acceptance.record
sales.approval.override

sales.order.read
sales.order.create
sales.order.edit
sales.order.confirm
sales.order.amend
sales.order.cancel
sales.order.fulfill
sales.order.invoice

sales.credit.view
sales.credit.override

sales.return.create
sales.return.approve
sales.return.execute

sales.customer_profile.manage
sales.channel.manage
sales.team.manage

sales.history.read
sales.audit.read
```

---

# 380. Cost Permission Is Not Sales Permission

Salespeople should not gain Inventory cost simply because they sell products.

---

# 381. Margin Visibility

If future margin display uses Inventory cost:

requires explicit permission + protected cost contract.

---

# 382. Price Override Permission

Separate from normal quotation edit.

---

# 383. Discount Override Permission

Can be threshold/policy based.

---

# 384. Acceptance Recording Permission

Can be same as salesperson in simple companies.

Can be restricted in controlled companies.

---

# 385. Credit Override

High-risk permission.

Require reason/audit and optional step-up.

---

# 386. Order Cancellation Permission

Separate for confirmed orders.

---

# 387. Return Approval Permission

Optional risk-based control.

---

# 388. Branch Scope

User can be limited to assigned Branch(es).

---

# 389. Legal Entity Scope

Mandatory.

---

# 390. Salesperson Ownership Filter

Can limit read/edit by assignment/team if company chooses.

Not a substitute for backend permission.

---

# 391. Customer Visibility

Sales can use Party visibility scope from Directory.

---

# 392. Warehouse Visibility

Sales may see only Inventory Warehouses allowed to user/branch.

---

# 393. Audit

High-value actions:

```text
Price override
Discount override
Acceptance
Conversion
Credit override
Order amendment
Cancellation
Return
```

audited.

---

# 394. Audit Does Not Replace Business History

Both exist.

---

# 395. Sales Integration Events

Potential:

```text
QuotationOpenedV1
QuotationAcceptedV1
QuotationDeclinedV1
QuotationConvertedV1
SalesOrderConfirmedV1
SalesOrderAmendedV1
SalesOrderCancelledV1
SalesFulfillmentRequestedV1
SalesOrderFulfilledV1
SalesReturnRequestedV1
```

Publish only meaningful boundaries.

---

# 396. Draft Edit Events

Do not publish every keystroke/edit globally.

---

# 397. Outbox

Post-commit integration events through Transactional Outbox.

---

# 398. Broker Is Not Commercial Invariant

Acceptance/conversion source truth does not depend on RabbitMQ being available.

---

# 399. Quote Acceptance Transaction

Typical:

```text
validate current revision
validate policy/approval
append acceptance/history
update commercial state
write audit
write outbox
commit
```

---

# 400. Convert-to-Invoice Transaction

Where supported:

```text
validate accepted source
validate idempotency
create Invoicing source request/draft/finalization
mark conversion link
audit
outbox
commit
```

using Transaction Coordinator for immediate internal invariants.

---

# 401. Finalized Invoice Atomicity

Follows Invoicing pack:

```text
Invoice
+ Subledger Open Item
+ Ledger posting/fact
+ Audit
+ Outbox
```

where immediate finalization is selected.

---

# 402. Deliver-Now Atomicity

If chosen as one business action and policy requires all-or-nothing:

```text
Sales Conversion
+ Invoice
+ Inventory Issue/Valuation
+ Ledger/Subledger effects
+ Audit/Outbox
```

coordinate within one database transaction where architecture supports.

---

# 403. No Distributed Transaction With External Services

Never include:

```text
Email
WhatsApp
Government e-invoice authority
Carrier API
External payment gateway
```

inside DB transaction.

---

# 404. Compliance Outcome

Can change invoice regulatory state later.

Sales conversion remains linked to Invoice.

---

# 405. Compliance Failure

Does not rewrite Quotation acceptance.

Country policy determines invoice/legal workflow.

---

# 406. Inventory Failure During Deliver-Now

If stock issue was selected as required part:

composite action fails/returns actionable conflict.

---

# 407. Optional Asynchronous Fulfillment

Advanced company can:

```text
Confirm Order
→ create fulfillment request
→ warehouse processes later
```

without holding Sales transaction.

---

# 408. Fulfillment Events

Inventory can emit confirmed result event/read ref.

Sales updates projection idempotently.

---

# 409. Strong Consistency When Sales Initiates Same-Process Fulfillment

If necessary for user action, Transaction Coordinator can update Sales projection + Inventory movement atomically.

---

# 410. Projection Repair

Sales fulfillment/invoice projections must be rebuildable from typed source links/events.

---

# 411. Idempotency

Mandatory for:

```text
Accept
Convert
Confirm order
Reserve
Fulfill
Create invoice
Cancel remaining
External import
Return execution
```

---

# 412. Same Key Different Payload

Conflict.

Never silently reuse idempotency key with different request.

---

# 413. Quotation Acceptance Race

Two users accepting same revision:

one acceptance truth; retries return existing result.

---

# 414. Quote Edit vs Accept Race

Optimistic version/revision check prevents accepting stale unseen edits.

---

# 415. Quote Conversion Race

Two users click Convert:

one Invoice target only.

---

# 416. Order Amend vs Fulfill Race

Use version + line-level invariants.

Cannot fulfill cancelled/reduced quantity beyond allowed amount.

---

# 417. Order Cancel vs Reserve Race

Coordinated commands ensure reservation and commercial state converge correctly.

---

# 418. Error Codes

Stable examples:

```text
sales.customer_not_ready
sales.customer_blocked
sales.quotation_not_found
sales.quotation_revision_stale
sales.quotation_not_open
sales.quotation_not_accepted
sales.quotation_expired
sales.quotation_already_converted
sales.quotation_conversion_conflict

sales.price_changed
sales.price_override_not_allowed
sales.discount_approval_required
sales.tax_preview_changed

sales.credit_warning
sales.credit_blocked
sales.credit_approval_required

sales.inventory_unavailable
sales.reservation_failed
sales.fulfillment_conflict

sales.order_not_confirmable
sales.order_amendment_conflict
sales.order_quantity_below_fulfilled
sales.order_already_closed

sales.return_quantity_exceeded
sales.return_not_allowed

sales.idempotency_conflict
sales.concurrent_change
sales.permission_denied
```

---

# 419. Business-Friendly Stale Quote Error

> "تم تعديل عرض السعر بواسطة مستخدم آخر. حدّث العرض قبل تسجيل الموافقة."

---

# 420. Expired Quote UX

Show:

```text
Expired on 05 Oct
```

Actions according to policy:

```text
Create Revision
Authorized Accept
Cancel
```

---

# 421. Price Change Warning

Example:

```text
Accepted quote price: 1,000
Current list price:   1,100
```

Then apply configured honor/reprice policy.

---

# 422. Tax Change Warning

Explain legal/tax preview difference before finalization where possible.

---

# 423. Credit Block UX

Show:

```text
Exposure
Limit
Proposed
Decision
```

without exposing irrelevant finance details beyond permission.

---

# 424. Inventory Shortage UX

Show:

```text
Requested
Available
Alternative Warehouse
Expected
```

if user has access.

---

# 425. Permission Error

Do not expose internal policy implementation details.

---

# 426. Security — Mass Assignment

Never bind API request directly to Sales entities.

---

# 427. Security — Customer Scope

Validate Party belongs tenant and allowed visibility/entity context.

---

# 428. Security — Source Reference

Validate all target Invoice/Inventory refs belong same tenant/entity/source relation.

---

# 429. Security — Price Override

Server enforces permission; hidden UI is insufficient.

---

# 430. Security — Credit Override

Server enforces + audit.

---

# 431. Security — Acceptance Evidence File

FileRef access validated.

---

# 432. Security — External Import

Signed/authenticated integration, scoped credentials and rate limits.

---

# 433. Security — Customer Portal Future

External identity has dedicated scope and must never inherit internal Sales permissions.

---

# 434. Privacy

Customer contact data comes from Directory permissions.

Do not duplicate/export unnecessarily.

---

# 435. Observability

Metrics:

```text
sales_quote_save_duration
sales_quote_preview_duration
sales_quote_convert_duration
sales_quote_conversion_conflict_total
sales_price_override_total
sales_credit_block_total
sales_inventory_shortage_total
sales_order_fulfillment_duration
sales_external_import_duplicate_total
```

No customer/tenant IDs as high-cardinality labels.

---

# 436. Tracing

Conversion trace can include:

```text
Load quote revision
Validate acceptance
Resolve conversion policy
Credit check if required
Inventory check if required
Create invoice request
Finalize invoice
Issue stock if selected
Write conversion link
Outbox
Commit
```

---

# 437. Logging

No full quote/invoice customer data dumps.

Use IDs/correlation and safe structured metadata.

---

# 438. Alerts

Potential:

```text
Repeated conversion conflicts
High failure rate converting quotes
Fulfillment projection mismatch
External order import failures
Outbox backlog
```

---

# 439. Support Tool

Support can inspect:

```text
Quote revision
Acceptance
Conversion target
Idempotency key/result
Credit decision ref
Inventory ref
Invoice ref
Outbox/correlation
```

without editing business truth.

---

# 440. No Direct DB Fix

Support must not mark quote converted/paid/delivered by SQL.

Use controlled repair/reconciliation commands.

---

# 441. Read Models

Use optimized projections for:

```text
Quotation List
Sales Order List
Customer Sales Summary
Open Order Lines
Backorder Summary
Sales History Timeline
```

---

# 442. Read Model Is Not Commercial Source

Rebuildable from Sales source tables/typed links.

---

# 443. List Indexes

Likely:

```text
tenant/entity/quote_date desc
tenant/entity/party/quote_date desc
tenant/entity/commercial_state/quote_date desc
tenant/entity/salesperson/quote_date desc
tenant/entity/number
```

---

# 444. Order Indexes

Likely:

```text
tenant/entity/order_date desc
tenant/entity/party/order_date desc
tenant/entity/commercial_state/order_date
tenant/entity/fulfillment_state/order_date
tenant/entity/number
```

---

# 445. External Source Unique Index

```text
tenant
+ source_system
+ external_id
```

scope refined by integration semantics.

---

# 446. Acceptance Index

Quick lookup accepted revision.

---

# 447. Conversion Unique Index

Protect duplicate target creation.

---

# 448. Deep Pagination

Use keyset/cursor.

---

# 449. Search Number

Exact/prefix indexed search.

---

# 450. Search Customer

Directory search/composed projection, not broad `%term%` scans over duplicated names.

---

# 451. Search Product in Quote

Catalog picker.

---

# 452. Performance Dataset

Test with:

```text
1m quotations
10m quotation lines
500k sales orders
5m order lines
large customer histories
large product catalog
```

adjust based on target customers.

---

# 453. Hot Customer

Large B2B customer with many quotations/orders must remain responsive.

---

# 454. Concurrent Quote Editing

Optimistic conflict behavior benchmarked.

---

# 455. 100-Line Quote Preview

Benchmark Pricing+Tax+Inventory batch composition.

---

# 456. 500-Line Conversion

No N+1.

---

# 457. One-Click Conversion

Must not create long DB locks due to external APIs.

---

# 458. Frontend Module Structure

Recommended:

```text
src/frontend/src/modules/sales/

├── api/
├── routes/
├── pages/
│   ├── QuotationListPage/
│   ├── QuotationWorkspacePage/
│   ├── SalesOrderListPage/
│   ├── SalesOrderWorkspacePage/
│   ├── SalesReturnPage/
│   └── SalesSettingsPage/
├── features/
│   ├── create-quotation/
│   ├── edit-quotation/
│   ├── accept-quotation/
│   ├── convert-quotation/
│   ├── revise-quotation/
│   ├── confirm-order/
│   ├── fulfill-order/
│   ├── cancel-order/
│   └── create-return/
├── components/
│   ├── SalesCustomerPicker/
│   ├── SalesLineGrid/
│   ├── CommercialTotals/
│   ├── AvailabilityBadge/
│   ├── CreditDecisionBadge/
│   ├── QuoteHistoryTimeline/
│   └── ConversionActions/
├── queries/
├── mutations/
├── schemas/
├── permissions/
├── i18n/
└── public.ts
```

---

# 459. Simple Capability Hides Advanced Routes

If Sales Order disabled:

do not show empty Sales Order pages/menu.

---

# 460. Backend Structure

Recommended:

```text
src/backend/modules/Sales/

Todir.Modules.Sales.Domain
Todir.Modules.Sales.Application
Todir.Modules.Sales.Infrastructure
Todir.Sales.Contracts
```

following final repository naming conventions.

---

# 461. Domain Folder Areas

Potential:

```text
Quotations/
Orders/
Returns/
CustomerProfiles/
Channels/
Teams/
Policies/
History/
```

---

# 462. Application Use Cases

Explicit commands/queries.

Avoid generic CRUD service.

---

# 463. Infrastructure

Contains:

```text
SalesDbContext
Mappings
Migrations
Read Queries
Outbox integration
External source adapters
```

---

# 464. API Layer

Thin endpoint/controller.

No domain rules in controller.

---

# 465. Tests

Located with appropriate project layers + integration suite.

---

# 466. Module Manifest

Concept:

```text
id: sales
type: business-module
purchasable: true
scope: tenant/entity
```

dependencies/capabilities explicit.

---

# 467. Hard Dependencies

Likely baseline:

```text
Platform Kernel
Directory
Catalog
Pricing
FinBase
Invoicing
```

Exact hard/optional relationship follows module DAG decisions.

---

# 468. Optional Capabilities

Potential:

```text
Inventory
Subledger credit check
Approvals
Payments quick action
CRM
Customer Portal
Communication providers
```

---

# 469. Important Dependency Note

Subledger may already be activated indirectly by Invoicing.

Sales should still consume credit capability only when configured.

---

# 470. Inventory Optional for Sales

Critical.

Service company can sell without Inventory module.

---

# 471. Sales Order Capability Is Inside Sales But Optional

No separate tenant code fork.

---

# 472. Module Activation

Seeds sensible defaults:

```text
Quotation number series
Simple conversion policy
Default quote validity
No mandatory approval
No mandatory Sales Order
```

---

# 473. Recommended Fresh Tenant Defaults

```text
Quotation enabled                 ON
Direct Quote → Invoice            ON
Sales Order required              OFF
Reservation required              OFF
Credit control in Sales           OFF unless configured
Internal approval                 OFF
Customer Portal                   OFF
Delivery document required        OFF
```

This matches TODIR simplicity goal.

---

# 474. Inventory-Aware Tenant Default

If Inventory active:

show availability.

Do not automatically require reservation/delivery.

---

# 475. Advanced Sales Preset

Can turn on:

```text
Sales Order
Reservation
Credit Check
Delivery-based invoicing
Backorder
Approvals
```

as a preset, still individually configurable.

---

# 476. No Setup Dead-End

A new company should be able to:

```text
Create Customer
Create Quote
Accept
Create Invoice
```

without configuring advanced Sales settings.

---

# 477. Sales Settings — Simple

Show only:

```text
Quotation numbering
Default validity
Default convert action
```

---

# 478. Sales Settings — Advanced

Expandable:

```text
Order workflow
Credit point
Reservation
Fulfillment
Invoice basis
Approvals
Returns
Channel defaults
```

---

# 479. Policy Versioning

Policies affecting accepted/confirmed documents need effective version/evidence where material.

---

# 480. Old Document Stability

Changing Sales settings does not retroactively alter accepted quote/order interpretation.

---

# 481. Policy Resolution

Potential precedence:

```text
Document override (authorized)
Customer Sales Profile
Branch/Channel policy
Legal Entity Sales policy
Tenant default
```

No ambiguous hidden tie-break.

---

# 482. Policy Conflict

Configuration conflict blocks activation/change rather than random behavior.

---

# 483. Feature Flag vs Business Capability

Use Module Capability/Settings for persistent business behavior.

Do not scatter temporary frontend flags as architecture.

---

# 484. Deactivation

Sales enters:

```text
WindDown
ReadOnly
Disabled
```

per platform lifecycle.

History retained.

---

# 485. WindDown

No new Quotes/Orders.

Existing open documents can be completed/cancelled according to policy.

---

# 486. ReadOnly

Historical access/reports.

No mutation.

---

# 487. Sales Order Capability Disable

Cannot simply hide unresolved open Orders.

Must wind down/close/migrate them first.

---

# 488. Customer Profile Deactivation

Does not delete Party or historical Sales.

---

# 489. Channel Deactivation

No new docs use it.

Old docs retain reference.

---

# 490. Salesperson Deactivation

Historical assignment retained.

Open docs can be reassigned by authorized action.

---

# 491. Time/Date Semantics

Separate:

```text
QuoteDate
ValidUntil
AcceptedAt
OrderDate
RequestedDeliveryDate
ConfirmedAt
FulfillmentDate
ConversionAt
```

---

# 492. No Server Local Time Assumption

Use Legal Entity timezone.

---

# 493. Backdated Quote

Can be allowed by policy.

Does not bypass PricingDate rules.

---

# 494. Backdated Order

Controlled.

Credit/availability checked at appropriate current/transaction decision context.

---

# 495. Future-Dated Quote

Supported.

Pricing policy defines PricingDate independently from delivery date.

---

# 496. Expiry Time

If date-only validity:

interpret in Legal Entity timezone.

---

# 497. International Customer

Customer location may differ; Sales document still belongs Legal Entity.

Tax Engine determines jurisdiction/treatment.

---

# 498. Multi-Currency

Sales uses document currency through Pricing/FinBase.

Invoice finalization freezes financial currency context.

---

# 499. FX

Sales should not maintain its own FX table.

---

# 500. Commercial Price Conversion

If Pricing resolves conversion, snapshot exact commercial FX evidence according to Pricing/FinBase policy.



# 501. Commercial Rounding

Sales does not invent rounding rules.

Use FinBase + Pricing/Tax outputs.

---

# 502. Quote Total Snapshot

Accepted revision stores customer-facing commercial totals and evidence.

---

# 503. Invoice Financial Snapshot Remains Separate

Quote totals explain agreement.

Invoice snapshot explains legal/financial document.

---

# 504. Country Pack Boundary

Country Pack can constrain:

```text
tax/legal invoice requirements
mandatory customer identifiers
document wording
specific regulated sales document needs
```

but Sales Core remains country-neutral.

---

# 505. Quotation Is Usually Not a Tax Invoice

Do not assign regulatory invoice semantics unless a specific Country Pack defines a distinct legal requirement.

---

# 506. Government API Boundary

Sales never submits Quotation to e-invoice authority merely because it converts later.

Invoicing/Compliance owns authority workflow.

---

# 507. Customer PO Requirement

Optional Sales Profile/policy:

```text
NotRequired
RequiredBeforeOrder
RequiredBeforeInvoice
```

for B2B customers.

---

# 508. Missing Customer PO

Does not block companies that do not use this policy.

---

# 509. Contract Reference

Optional.

Future Contracts module can provide richer commercial contract source.

---

# 510. Terms and Conditions

Versioned template can be selected on Quotation.

Accepted revision stores rendered/version reference.

---

# 511. Technical Scope Attachment

Useful for services/projects.

Does not turn Sales into project management.

---

# 512. Project Boundary

Future Project module can consume accepted sale/order.

Sales does not own project execution.

---

# 513. Sales of Services

Can carry:

```text
description
qty/hours/unit
price
tax context
```

without Inventory.

---

# 514. Sales of Goods

Can optionally use availability/fulfillment.

---

# 515. Mixed Quote

Goods + services in one quote supported.

Inventory logic applies only to stock-tracked lines.

---

# 516. Mixed Fulfillment

Service lines may become invoiceable immediately while goods depend on delivery, if advanced policy config explicitly supports it.

---

# 517. Simple Mixed Sale Default

Direct quote-to-invoice can invoice all lines.

Stock issue only applies to eligible stock-tracked lines if Deliver Now selected.

---

# 518. Line-Level Source Trace

Invoice line should retain source quote/order line ref.

---

# 519. Product Removed From Catalog

Historical quote remains.

New conversion requires policy/readiness validation.

---

# 520. Archived Product on Accepted Quote

Can still be invoiced if legal/commercial policy allows and Invoicing has sufficient snapshots; new stock fulfillment may be blocked if Inventory profile unavailable.

---

# 521. UOM Change After Quote

Accepted revision stores UOM/conversion commercial evidence.

Invoice/Inventory use valid authoritative conversion snapshots according to their domains.

---

# 522. Sales Does Not Change Catalog UOM

Never.

---

# 523. Customer Address Change After Quote

Accepted revision preserves what was offered.

Invoice finalization uses appropriate selected/legal address context and can warn on material difference.

---

# 524. Customer Tax ID Change

Directory/Tax/Invoicing validate current legal requirement.

Sales must not rewrite accepted commercial history.

---

# 525. Customer Merge

Directory canonical redirect handled through contract.

Historical Sales source still traceable.

---

# 526. Sales Profile Merge Handling

Move/repoint profile to canonical Party through controlled Directory merge integration.

---

# 527. Customer Both Buyer and Supplier

Same Party can have Customer + Supplier roles.

Sales only uses Customer role/profile.

---

# 528. Sales and Purchasing Profiles Separate

Commercial defaults are direction-specific.

---

# 529. Sales Price Group

Sell-side commercial assignment only.

Purchasing will use supplier/buy-side context separately.

---

# 530. Customer-Specific Product Reference

Catalog/Directory/Pricing contracts can expose customer item reference where available.

Sales may show it.

Do not duplicate reference master.

---

# 531. Customer-Facing Product Code

Quotation can snapshot customer reference code for display.

---

# 532. Bilingual Quote

Rendering supports Arabic/English according to template/Party preference.

Structured data remains language-neutral where possible.

---

# 533. RTL

Sales UI and rendered Arabic quotation support RTL.

---

# 534. Sales Document Templates

Template system controls layout.

Sales controls data contract, not arbitrary tenant React code.

---

# 535. Custom Fields

Platform Custom Fields can extend Sales documents.

---

# 536. Custom Fields Are Not Core Pricing/Tax Inputs by Default

Critical commercial/financial behavior cannot depend on arbitrary custom field unless promoted to approved typed Rule Input.

---

# 537. Custom Approval Fields

Can be displayed/recorded without bypassing core invariants.

---

# 538. Workflow Customization

Use constrained workflow hooks.

No arbitrary tenant code in confirmation/conversion.

---

# 539. Sales Automation

Future:

```text
Auto-follow-up quote
Expire quote notification
Notify manager on high discount
Reminder before validity expires
```

through platform jobs/notifications.

---

# 540. Automation Does Not Auto-Accept Customer

Never fabricate customer acceptance.

---

# 541. AI Opportunities

AI may:

```text
Draft quote description
Suggest products
Suggest follow-up text
Summarize customer history
Explain price/credit/availability warnings
Detect unusual discount
Suggest likely upsell
```

---

# 542. AI Does Not

```text
Accept quote on behalf of customer
Override price permission
Override credit block
Create fake acceptance evidence
Directly issue stock
Finalize invoice without authorized command
Record payment
```

---

# 543. AI Tool Path

```text
AI Tool Layer
→ Permissions
→ Sales Application Command
→ Domain
→ Audit
```

---

# 544. AI Customer Communication

Generated email/WhatsApp remains draft unless explicit approved send workflow.

---

# 545. Notifications

Potential:

```text
Quote opened internally
Quote nearing expiry
Quote accepted
Credit blocked
Order ready for fulfillment
Backorder aging
```

---

# 546. Notification Is Optional

Must not become a transaction dependency.

---

# 547. Scheduling

Quote expiry/reminder jobs through Platform Scheduler.

---

# 548. Expiry Job

Can mark/recompute `Expired` when validity passes.

Must be idempotent.

---

# 549. Expiry Race With Acceptance

Acceptance command checks authoritative current time/policy inside transaction.

---

# 550. Expiry Does Not Modify Accepted Quote

Accepted quote does not become Expired afterward as a commercial state.

Validity evidence remains.

---

# 551. Import/Export Security

Sensitive prices/customer data require permission.

---

# 552. Data Retention

Commercial history retained per company/legal policy.

No casual hard-delete of accepted/converted documents.

---

# 553. Draft Deletion

May be allowed for never-opened unused Draft according to retention/audit policy.

---

# 554. Accepted/Converted Delete

Forbidden.

Use cancellation/correction/history.

---

# 555. GDPR/Privacy Erasure Boundary

Legal retention can require pseudonymization/restriction rather than deleting commercial history.

Country/legal review required.

---

# 556. Migration

Legacy sales data strategies:

```text
Open Quotations
Open Sales Orders
Recent History
Reference-only Closed History
```

depending migration scope.

---

# 557. Do Not Recreate Old Invoices as Sales Orders

Historical invoice migration belongs Invoicing/finance migration.

---

# 558. Open Quote Migration

Preserve:

```text
Legacy ID
Customer
Lines
Price
Validity
State
```

with migration evidence.

---

# 559. Open Order Migration

Requires reconciliation of:

```text
Ordered
Delivered
Invoiced
Cancelled
Reserved
```

with Inventory/Invoicing.

---

# 560. Migration Idempotency

Legacy source key unique.

---

# 561. Migration Reconciliation

Totals/counts and source refs compared before go-live.

---

# 562. Sales Go-Live

Per Legal Entity:

```text
SalesGoLiveDate
InitialDocumentStrategy
HistoricalBoundary
```

---

# 563. Sales Module WindDown

Existing open docs can be:

```text
Complete
Cancel
Archive after closure
```

No new creation.

---

# 564. Runbook — Quote Converted But UI Missing Link

Rebuild/repair projection/link from idempotent source/target references.

No duplicate Invoice.

---

# 565. Runbook — Invoice Exists but Conversion Marker Failed

Use idempotency/source identity to reconcile.

Never click-create another Invoice blindly.

---

# 566. Runbook — Reservation Projection Mismatch

Ask Inventory source truth and repair Sales projection/reference.

---

# 567. Runbook — Fulfillment Projection Mismatch

Reconcile against Inventory operation links.

---

# 568. Runbook — Credit Decision Confusion

Re-query Subledger current exposure and inspect historical decision evidence.

---

# 569. Runbook — Stale Pricing Snapshot

Accepted quote remains commercial evidence; conversion policy decides whether refresh/revision required.

---

# 570. Runbook — External Import Duplicate

Resolve source key mapping, never merge two commercial orders silently.

---

# 571. Test Strategy — Default Simple Flow

```text
Create Quote
Edit
Accept
Convert to Invoice
```

No Sales Order/Reservation/Delivery required.

---

# 572. Test — Service Company

No Inventory active.

Quote→Invoice works fully.

---

# 573. Test — Customer Does Not Have Login

Quote can be accepted/converted using employee-recorded acceptance.

---

# 574. Test — Customer Portal Disabled

No missing dependency or blocked Sales workflow.

---

# 575. Test — Draft Quote Edit

Editable fields save; no financial Open Item/stock movement created.

---

# 576. Test — Quote Acceptance

Accepted revision frozen.

---

# 577. Test — Edit Accepted Quote

Creates controlled new revision for material change.

Previous accepted revision remains.

---

# 578. Test — Declined Quote

Cannot convert without valid reopen/revision path.

---

# 579. Test — Expired Quote

Configured block/warn/revise behavior enforced.

---

# 580. Test — No Expiry

Quote remains valid until explicit state change.

---

# 581. Test — Double Accept

Idempotent one acceptance result.

---

# 582. Test — Edit vs Accept Race

Stale revision cannot be accepted silently.

---

# 583. Test — Double Convert

Exactly one Invoice.

---

# 584. Test — Conversion Failure

Quote does not become falsely Converted.

---

# 585. Test — Price List Changes After Acceptance

Accepted quote commercial price remains per Honor policy.

---

# 586. Test — Reprice Policy

Current price difference shown/applied exactly by configured policy.

---

# 587. Test — Manual Price Override

Permission/reason/approval evidence enforced.

---

# 588. Test — Discount Threshold

Below threshold direct; above threshold approval if policy.

---

# 589. Test — Tax Preview Change

Sales shows/handles difference; Invoicing remains final authority.

---

# 590. Test — Direct Quote Invoice With Service

No Inventory call.

---

# 591. Test — Direct Quote Invoice With Stock, No Deliver

Invoice succeeds if workflow permits; On Hand unchanged.

---

# 592. Test — Direct Quote Invoice + Deliver Now

Invoice and Inventory Issue coordinated; no duplicate stock/invoice.

---

# 593. Test — Deliver Now Insufficient Stock

No false complete success.

---

# 594. Test — Negative Stock Disallowed

Sales cannot bypass Inventory policy.

---

# 595. Test — Inventory Disabled

No Warehouse/availability fields required.

---

# 596. Test — Stocked and Service Mixed Quote

Only stock lines call Inventory when fulfilling.

---

# 597. Test — Availability Preview

Informational preview does not reserve.

---

# 598. Test — Old Availability

Conversion/fulfillment refreshes authoritative Inventory state.

---

# 599. Test — Reservation Disabled

Sales Order confirms/fulfills according to policy without reservation record.

---

# 600. Test — Reservation Enforced

Order cannot fulfill unreserved stock if configured invariant requires it.

---

# 601. Test — Reservation Release on Cancellation

Remaining reservation released exactly once.

---

# 602. Test — Sales Order Optional

Tenant with capability OFF never needs Order API/UI for quote→invoice.

---

# 603. Test — Direct Sales Order

Order can be created without Quotation when capability ON.

---

# 604. Test — Quote→Order

Accepted revision creates one Order.

---

# 605. Test — Partial Fulfillment

```text
Ordered 100
Fulfilled 60
Remaining 40
```

exact.

---

# 606. Test — Multiple Deliveries

60 + 20 + 20 = 100.

No overfulfillment.

---

# 607. Test — Backorder

Remaining demand stays Sales-owned; Inventory quantity remains correct.

---

# 608. Test — Cancel Remaining

Fulfilled quantity remains historical.

Only open remainder cancelled.

---

# 609. Test — Amend Qty Below Fulfilled

Blocked.

---

# 610. Test — Order Partial Invoice

Invoice eligible/invoiced projections exact.

---

# 611. Test — Credit Control Disabled

No credit workflow blocker.

---

# 612. Test — Credit Allow

Order/conversion proceeds.

---

# 613. Test — Credit Warn

Permissioned proceed with evidence.

---

# 614. Test — Credit Block

Blocked.

---

# 615. Test — Credit Approval

Authorized exception recorded without changing Subledger truth.

---

# 616. Test — Credit Race

Current exposure rechecked at commitment point.

---

# 617. Test — Customer Outstanding Display

Matches Subledger read contract.

Sales cannot edit it.

---

# 618. Test — Return Full

Return request links correct Inventory/Credit Note/Refund operations according to policy.

---

# 619. Test — Return Partial

Eligible quantity enforced.

---

# 620. Test — Financial-Only Credit

No Inventory Receipt required.

---

# 621. Test — Stock Return Pending Inspection

Inventory status correct; no automatic sellable stock unless policy.

---

# 622. Test — Customer Rename

Historical accepted quote remains understandable.

---

# 623. Test — Product Rename

Historical accepted quote lines stable.

---

# 624. Test — Customer Merge

Canonical Party linkage works; history preserved.

---

# 625. Test — RLS Cross Tenant

Tenant A cannot read/create/convert Tenant B Sales docs.

---

# 626. Test — Wrong Legal Entity

Blocked.

---

# 627. Test — Branch Scope

User cannot access unauthorized Branch docs.

---

# 628. Test — Salesperson Scope

Configured team/ownership restrictions enforced backend.

---

# 629. Test — Price Override Hidden Button

API still blocks unauthorized caller.

---

# 630. Test — Cost Leakage

Sales user without cost permission cannot retrieve Inventory cost through Sales API.

---

# 631. Test — Large Quote

500 lines: bounded Catalog/Pricing/Tax/Inventory calls.

---

# 632. Test — Million Quote List

Indexed paging remains healthy.

---

# 633. Test — External Source Duplicate

Same marketplace order imported twice creates one internal Order.

---

# 634. Test — Broker Down

Acceptance/conversion commits with Outbox where DB invariants succeed.

---

# 635. Test — Email Provider Down

Quote remains valid; send can retry.

---

# 636. Test — Government Compliance Down

Sales source/Invoice internal state follows Invoicing compliance model; no duplicate conversion.

---

# 637. Test — Projection Rebuild

Sales list/fulfillment/invoice projections reconstruct from source links.

---

# 638. Property Test — Commercial Quantity

For Order line:

```text
CancelledQty >= 0
FulfilledQty >= 0

CancelledQty + FulfilledQty
<= OrderedQty
```

unless explicit overdelivery feature.

---

# 639. Property Test — Invoiced Quantity

Cannot exceed configured eligible quantity without explicit advance/order billing policy.

---

# 640. Property Test — Quote Revision

At most one accepted commercial revision is current for a simple quote conversion context.

Historical acceptances remain traceable if later superseded/revised.

---

# 641. Property Test — Conversion Idempotency

Same source/purpose/key → same target.

---

# 642. Property Test — Accepted Revision Stability

Mutable master/config changes never mutate accepted revision bytes/semantic snapshot.

---

# 643. Architecture Test — No Direct Inventory Write

Sales implementation cannot reference Inventory entity/DbContext.

---

# 644. Architecture Test — No Direct Invoice Table Write

Sales calls Invoicing Contracts.

---

# 645. Architecture Test — No Subledger Calculation

No Sales implementation computes AR/open balance from invoices/payments.

---

# 646. Architecture Test — No Tax Formula

Tax Engine only.

---

# 647. Architecture Test — No Price Rule Logic

Pricing only.

---

# 648. Architecture Test — No Payment Mutation

Payments only.

---

# 649. Architecture Test — No Ledger Entry Creation

Sales never constructs Journal Entry.

---

# 650. Red-Team — Mandatory Sales Order for Every Quote

Reject.

Default simple flow must remain Quote→Invoice.

---

# 651. Red-Team — Mandatory Customer Portal

Reject.

---

# 652. Red-Team — Customer Must Click Accept Online

Reject as default.

Employee-recorded external acceptance is valid workflow.

---

# 653. Red-Team — Mandatory Reservation

Reject unless company enables it.

---

# 654. Red-Team — Mandatory Delivery Document

Reject unless company enables it.

---

# 655. Red-Team — Mandatory Internal Approval

Reject unless policy triggers.

---

# 656. Red-Team — Sales Stores Customer Balance

Reject.

---

# 657. Red-Team — Sales Stores Current Stock Truth

Reject.

---

# 658. Red-Team — Sales Creates Invoice Row Directly

Reject.

---

# 659. Red-Team — Quote Converted by Changing Document Type

Reject.

Create target and typed relation.

---

# 660. Red-Team — Accepted Quote Edited in Place

Reject.

Revision/correction.

---

# 661. Red-Team — Price List Change Rewrites Accepted Quote

Reject.

---

# 662. Red-Team — Sent Email Means Accepted

Reject.

---

# 663. Red-Team — Available Stock Means Reserved

Reject.

---

# 664. Red-Team — Confirm Order Means Delivered

Reject.

---

# 665. Red-Team — Invoice Means Stock Delivered

Reject.

---

# 666. Red-Team — Sales Order `Paid = true`

Reject.

---

# 667. Red-Team — Cancellation Deletes History

Reject.

---

# 668. Red-Team — Return Edits Original Sale

Reject.

Append return/correction links.

---

# 669. Red-Team — Credit Check Cached Forever

Reject.

Refresh at configured commitment point.

---

# 670. Red-Team — 100 Lines = 100 Pricing Calls

Reject.

---

# 671. Red-Team — One Generic Sales Status

Reject.

Commercial/Fulfillment/Invoicing/Credit dimensions separate where Order workflow exists.

---

# 672. Red-Team — Complex Settings Shown to Small Business

Reject.

Progressive disclosure.

---

# 673. Product Acceptance — Small Service Company

User sees:

```text
New Quote
Customer
Service
Qty
Price
Save
Accept
Create Invoice
```

No Warehouse/Order/Reservation screens.

---

# 674. Product Acceptance — Small Trading Company

User can:

```text
Quote
→ Accept
→ Invoice
→ Deliver Now
```

in one simple action where allowed.

---

# 675. Product Acceptance — Distributor

Can enable Order, Reservation, Partial Delivery, Backorder, Delivery-based Invoice.

---

# 676. Product Acceptance — Mixed Company

Service quotes stay simple even when Inventory capability exists for goods.

---

# 677. Product Acceptance — External Customer

Customer never logs in, yet full Sales workflow works.

---

# 678. Product Acceptance — Salesperson

Can create/convert quote without seeing accounting internals.

---

# 679. Product Acceptance — Finance

Invoice/Open Item/Ledger remain authoritative and traceable.

---

# 680. Product Acceptance — Warehouse

Only receives fulfillment/reservation requests when relevant.

---

# 681. Product Acceptance — History

From Quote user can navigate exact accepted revision → target Invoice/Order.

---

# 682. Product Acceptance — Speed

Normal simple sale requires minimal screens/clicks.

---

# 683. Product Acceptance — Correctness

Minimal UX never bypasses domain checks.

---

# 684. Implementation Order

```text
SA00 ADRs + Ownership + Simple-First Policy
SA01 Schema + RLS
SA02 Sales Customer Profile
SA03 Quotation Draft
SA04 Pricing Integration
SA05 Tax Preview
SA06 Quotation Revisions
SA07 Acceptance
SA08 Quote → Invoice
SA09 Simple One-Click Conversion
SA10 Communication/PDF Hooks
SA11 History + Typed Links
SA12 Optional Approval Guard
SA13 Optional Credit Check
SA14 Inventory Availability
SA15 Deliver-Now Orchestration
SA16 Sales Order Capability
SA17 Order Confirmation
SA18 Reservation Integration
SA19 Fulfillment Integration
SA20 Partial Delivery + Backorder
SA21 Order → Invoice / Partial Invoice
SA22 Amendments + Cancellation
SA23 Returns
SA24 Salesperson/Team/Channel
SA25 Customer Sales Workspace
SA26 Read Models + Reports
SA27 External Source Import
SA28 Permissions + Audit
SA29 Frontend Progressive Disclosure
SA30 Performance + Concurrency
SA31 Projection/Reconciliation Runbooks
SA32 Migration
SA33 Security / Red-Team
SA34 End-to-End Acceptance
SA35 Release Gate
```

---

# 685. SA00 Exit Gate

Locked ADRs:

```text
Quote→Invoice is default
Sales Order optional
Customer Portal optional
Sales owns commercial truth only
Accepted revision immutable
Conversion creates target, never type mutation
```

---

# 686. SA01 Exit Gate

```text
business_sales
FORCE RLS
UUIDv7
tenant/entity-safe FKs
row versions
```

---

# 687. SA02 Exit Gate

Sales Customer Profile contains no duplicate identity/credit/payment truth.

---

# 688. SA03 Exit Gate

User can create/edit Quote with minimal fields.

---

# 689. SA04 Exit Gate

Pricing batch/override evidence exact.

---

# 690. SA05 Exit Gate

Tax Preview uses Tax Engine; no Sales tax formula.

---

# 691. SA06 Exit Gate

Commercial revisions preserve meaningful history without revision-per-keystroke.

---

# 692. SA07 Exit Gate

Acceptance works without customer login/portal.

---

# 693. SA08 Exit Gate

Accepted Quote creates exactly one Invoice target through Invoicing Contract.

---

# 694. SA09 Exit Gate

One-click:

```text
Accept & Invoice
```

works idempotently.

---

# 695. SA10 Exit Gate

PDF/send integrations cannot corrupt Quote if external provider fails.

---

# 696. SA11 Exit Gate

Typed history answers exact source/revision/target.

---

# 697. SA12 Exit Gate

Approvals fully optional and risk-triggered.

---

# 698. SA13 Exit Gate

Credit control fully optional; when enabled uses Subledger truth.

---

# 699. SA14 Exit Gate

Inventory availability is batch/read-only from Sales.

---

# 700. SA15 Exit Gate

Deliver Now creates correct Inventory operation without direct stock writes.

---

# 701. SA16 Exit Gate

Sales Order can be enabled without changing Quotation Core.

---

# 702. SA17 Exit Gate

Order confirmation is commercial only, not physical delivery.

---

# 703. SA18 Exit Gate

Reservation truth remains Inventory-owned.

---

# 704. SA19 Exit Gate

Fulfillment requests/results linked idempotently.

---

# 705. SA20 Exit Gate

Partial fulfillment/backorder exact.

---

# 706. SA21 Exit Gate

Order invoicing/partial invoicing source quantities cannot duplicate.

---

# 707. SA22 Exit Gate

Amend/cancel preserves prior commitments and downstream facts.

---

# 708. SA23 Exit Gate

Returns orchestrate proper domains without rewriting original sale.

---

# 709. SA24 Exit Gate

Salesperson/team/channel optional and reportable.

---

# 710. SA25 Exit Gate

Customer workspace composes Sales/Invoice/Subledger summaries without giant aggregate/N+1.

---

# 711. SA26 Exit Gate

Lists/reports indexed/paginated.

---

# 712. SA27 Exit Gate

External order import idempotent.

---

# 713. SA28 Exit Gate

Permissions enforced backend; override/audit tested.

---

# 714. SA29 Exit Gate

Small tenant does not see advanced order/warehouse/approval complexity.

---

# 715. SA30 Exit Gate

Benchmarks include:

```text
100/500-line quote
double conversion
concurrent accept/edit
million-document list
large customer history
Inventory/Pricing/Tax batch calls
```

---

# 716. SA31 Exit Gate

Runbooks repair projections/links without creating duplicate financial/stock documents.

---

# 717. SA32 Exit Gate

Open quote/order migration idempotent and reconciled.

---

# 718. SA33 Exit Gate

Red-team/architecture tests prohibit cross-domain shortcuts.

---

# 719. SA34 End-to-End — Simplest Sale

```text
Create Quote
→ Accept
→ Create Invoice
```

must be production-correct.

---

# 720. SA34 End-to-End — Simple Goods Sale

```text
Quote
→ Accept & Invoice
→ Deliver Now
```

must create exact Inventory/financial facts.

---

# 721. SA34 End-to-End — Advanced Sale

```text
Quote
→ Order
→ Reserve
→ Partial Deliver
→ Backorder
→ Partial Invoice
→ Complete
```

must be exact.

---

# 722. SA35 Release Gate

Required signoff:

```text
Product Architect
Sales Domain Lead
Finance/Invoicing Lead
Inventory Lead
Backend Lead
Frontend Lead
Performance Engineer
QA/Security
```

---

# 723. Build Now — MUST

```text
Sales Customer Profile
Quotation
Quotation Lines
Draft editing/autosave
Pricing integration
Tax preview
Quote revision
External/manual acceptance recording
Quote→Invoice direct conversion
One-click Accept & Invoice
Typed history
Source idempotency
Salesperson/channel basics
Optional approval hook
Optional credit hook
Inventory availability
Optional Deliver Now
Sales Order foundation
Order confirmation
Reservation integration
Partial fulfillment/backorder
Order→Invoice
Amend/cancel
Basic returns
RLS/security/audit
Read models/reports
Performance tests
```

---

# 724. Build Later / Per Customer Need

```text
Customer Portal
Advanced CRM
Complex quotation configurator/CPQ
Advanced contracts
E-signature providers
Complex commissions
Territory optimization
Sales forecasting AI
Sales sequences/cadences
Advanced order allocation
Shipment planning/carrier routing
Complex promotions
Subscription billing
Project milestone billing
Marketplace-specific deep workflows
Advanced sales incentives
```

---

# 725. Configuration Matrix

Company can choose independently:

```text
Quotation                         ON
Direct Quote → Invoice            ON/OFF
Sales Order                       OFF/ON
Inventory availability            OFF/ON if Inventory active
Reservation                       OFF/Advisory/Enforced
Deliver Now                       OFF/ON
Credit check                      OFF/ON
Internal approval                 OFF/RiskBased/Always
Delivery-based invoice            OFF/ON
Backorder                         OFF/ON
Customer Portal                   OFF/ON later
Return workflow                   Simple/Advanced
```

---

# 726. Default Fresh Tenant Experience

Recommended:

```text
Quotation                         ON
Quote → Invoice                   ON
Sales Order                       OFF
Reservation                       OFF
Credit approval                   OFF
Internal approval                 OFF
Customer Portal                   OFF
Mandatory Delivery document       OFF
```

---

# 727. Why This Default

TODIR should not ask a small company to configure enterprise process before making its first sale.

---

# 728. Strong Core Still Exists

Even simple default uses:

```text
Directory Party
Pricing
Tax
Invoicing
Subledger
Ledger
Inventory only when relevant
```

behind the scenes.

---

# 729. Domain Separation Must Not Become UX Fragmentation

This is a core TODIR product law.

---

# 730. Example — One Screen

```text
Customer: ABC
Product: X
Qty: 5
Price: 1,000
Total: 5,000

[Save Quote]
[Accept & Invoice]
```

Advanced details collapsed.

---

# 731. Example — Advanced Expansion

User can expand:

```text
Warehouse
Availability
Credit
Price explanation
Tax preview
Delivery schedule
Approval
Customer PO
```

only when relevant.

---

# 732. Customer Does Not Need to Understand TODIR Workflow

Customer interaction remains external unless Portal chosen.

---

# 733. Employee Does Not Need to Understand Internal Domains

Salesperson should not need to know:

```text
Subledger
Posting Engine
RLS
Outbox
Accounting Fact
```

to sell correctly.

---

# 734. System Handles Cross-Domain Work

Product UX orchestrates it.

---

# 735. Final Source-of-Truth Matrix

```text
Customer identity       → Directory
Product/UOM             → Catalog
Commercial price rules  → Pricing
Quote/agreement         → Sales
Stock                   → Inventory
Invoice                 → Invoicing
Customer balance        → Subledger
Payment                 → Payments
Accounting              → Ledger
Tax rules               → Tax
```

---

# 736. Final Simple Flow

```text
Customer
   ↓
Quotation
   ↓
Edit until correct
   ↓
Accepted
   ↓
Invoice
```

---

# 737. Final Goods Simple Flow

```text
Quotation
   ↓
Accepted
   ↓
Invoice
   ↓
Deliver Now? ──► Inventory Issue
```

UX can combine Invoice + Issue into one authorized action.

---

# 738. Final Advanced Flow

```text
Quotation
   ↓
Accepted
   ↓
Sales Order
   ├── Credit Check
   ├── Availability
   └── Reservation
   ↓
Partial/Full Fulfillment
   ↓
Inventory Issue
   ↓
Invoice Eligibility
   ↓
Invoicing
```

---

# 739. Final Quotation History Flow

```text
Draft
  ↓
Open Revision 1
  ↓
Accepted Revision 1
  ↓
Converted
  └── Invoice INV-...
```

If changed after acceptance:

```text
Accepted Revision 1
  ↓
Revise
  ↓
Open Revision 2
  ↓
New Acceptance
```

Original evidence remains.

---

# 740. Final Return Flow

```text
Original Sale
   ↓
Return Request
   ├── Inventory Return (if physical)
   ├── Credit Note (if financial correction)
   └── Refund (if money moves)
```

Sales orchestrates; each domain owns its truth.

---

# 741. Final Product Verdict

For most TODIR customers, Sales should feel like:

```text
Create Quotation
Edit it
Record acceptance
Create Invoice
```

Everything else is optional.

---

# 742. Final Engineering Verdict

Sales is approved only when:

```text
Quote→Invoice works without Sales Order.
Customer login/Portal is not required.
Accepted quotation revision cannot be silently changed.
Price rules remain Pricing-owned.
Tax remains Tax-owned.
Credit exposure remains Subledger-owned.
Stock/reservations remain Inventory-owned.
Invoice remains Invoicing-owned.
Payments remain Payments-owned.
Sales Order/Reservation/Delivery are optional capabilities.
One-click UX remains idempotent and transactionally correct.
No duplicate Invoice can result from retries/double clicks.
Partial fulfillment/invoicing is exact when advanced workflow is enabled.
Commercial history is permanent and explainable.
Small-company UX does not expose unnecessary enterprise workflow.
Large-company workflow can expand without replacing the Sales Core.
```

---

# 743. Final UX Law

> **If the user can complete the sale correctly in one or two actions, TODIR must not force five screens.**

---

# 744. Final Integrity Law

> **Fewer screens must never mean fewer domain controls. TODIR removes user friction through orchestration, defaults and progressive disclosure—not by weakening correctness.**

---

# 745. Next Pack

After Sales:

> **`15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR let a company create or approve a Purchase Order and convert it directly to a Supplier Invoice/Purchase Invoice in the shortest correct workflow, while making Goods Receipt, partial receipt, supplier matching, landed cost, approvals and advanced procurement optional capabilities—and while Purchasing never owns Supplier identity, Pricing, Inventory, Invoicing, Subledger or Ledger truth?**

The default Purchasing philosophy should mirror Sales:

```text
Purchase Order
→ Edit / Approve
→ Supplier Invoice
```

and only when the company needs it:

```text
Purchase Order
→ Goods Receipt
→ Supplier Invoice
→ Matching
```

without forcing the advanced path on every company.

---

**END OF FILE — 14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
