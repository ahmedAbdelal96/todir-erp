# 15 — TODIR ERP SaaS Purchasing / Procurement / Purchase Order Requirements & Technical Design Pack

**File:** `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + PURCHASING + PROCUREMENT + SUPPLIER COMMERCIAL WORKFLOW + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Purchasing Lead, Warehouse Lead, Finance/AP Lead, Backend/Frontend Leads, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **HIGH** — Purchasing mistakes affect supplier commitments, stock receipts, supplier bills, AP balances, tax, cash planning, inventory valuation, landed cost and financial reporting.

**Primary dependencies**
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

TODIR Purchasing owns the **commercial buying workflow** from supplier selection/commitment through Purchase Order management and optional procurement controls.

It answers:

```text
What are we buying?
From which supplier?
At what agreed buy price?
In what quantity/UOM?
For which Legal Entity/Branch/Warehouse?
When is it expected?
Was the Purchase Order approved?
What has been received?
What remains open?
What Supplier Invoice came from it?
What variance exists?
What was returned/cancelled?
```

The primary product rule is:

> **The shortest correct purchasing workflow is the default. Advanced procurement controls are optional.**

---

# 1. Default TODIR Purchasing Flow

For a normal small/simple company:

```text
Supplier
→ Purchase Order
→ Edit / Approve
→ Convert to Supplier Invoice
```

No mandatory Purchase Request.

No mandatory RFQ.

No mandatory Goods Receipt document.

No mandatory 3-way matching.

No mandatory approval matrix.

---

# 2. Simple Stock Purchase

A small trading company may use:

```text
Purchase Order
→ Approve
→ Supplier Invoice
   ☑ Receive stock now
```

Internally TODIR can orchestrate:

```text
Purchasing
+
Supplier Invoice
+
Inventory Receipt
+
Subledger
+
Ledger
```

without forcing the user through multiple modules/screens.

---

# 3. Advanced Purchasing Flow

A company that needs stronger controls can enable:

```text
Purchase Request
→ Approval
→ RFQ / Supplier Comparison
→ Purchase Order
→ Goods Receipt
→ Supplier Invoice
→ 2-Way / 3-Way Matching
→ Payment
```

Only the capabilities the company needs are active.

---

# 4. One Module, One File

This pack contains the complete Purchasing business module:

```text
Supplier Purchase Profile
Purchase Request
RFQ / Supplier Quotation comparison
Purchase Order
Approvals
Goods Receipt orchestration
Partial Receipt
Over/Under Receipt
Supplier Invoice conversion
2-Way / 3-Way Matching
Purchase Returns
Landed Cost integration
History
Reporting
Permissions
APIs
Schema
Performance
Tests
Implementation Plan
```

No separate design pack is required for each subfeature.

---

# 5. Simple Outside, Powerful Inside

A 5-person company can use:

```text
PO
→ Supplier Invoice
```

A large distributor can use:

```text
Request
→ RFQ
→ Approval
→ PO
→ Multiple Receipts
→ Matching
→ Supplier Invoice
→ Return
→ Landed Cost
```

The source-of-truth boundaries remain the same.

---

# 6. Central Business Question

> **How does TODIR let a company buy from suppliers in the fewest correct steps, while supporting optional procurement, receipt, matching, return and landed-cost controls for larger companies without Purchasing duplicating Supplier identity, Pricing, Inventory, Invoicing, Subledger, Payments or Ledger?**

---

# 7. Purchasing Owns

Purchasing owns:

```text
Supplier Purchase Profile
Purchase Request (optional)
Request Line
RFQ / Request for Quotation (optional)
Supplier Quote Response / Comparison (optional)
Purchase Order
Purchase Order Line
Purchase Order Amendment
Commercial Purchase Approval Context
Expected Receipt Intent
Purchase Cancellation
Matching Policy / Match Result orchestration
Supplier Return Commercial Request
Purchase History
Buyer / Procurement Team / Purchase Channel dimensions
```

---

# 8. Purchasing Does NOT Own

```text
Supplier identity              → Directory
Product/UOM identity           → Catalog
Reusable buy-price rules       → Pricing
Tax rules                      → Tax
Payment term definitions       → FinBase
Supplier payable balance       → Subledger
Supplier Invoice / Vendor Bill → Invoicing
Physical stock / receipts      → Inventory
Payment movement               → Payments
Journal Entries                → Ledger
Bank reconciliation            → Banking/Reconciliation
```

---

# 9. Source-of-Truth Matrix

```text
Supplier identity             → Directory
Product / UOM                 → Catalog
Buy price rules               → Pricing
Purchase commitment           → Purchasing
Physical receipt              → Inventory
Supplier Invoice              → Invoicing
Supplier payable balance      → Subledger
Supplier payment              → Payments
Accounting                    → Ledger
Tax                           → Tax
```

---

# 10. Supplier Is a Party, Not a Duplicate Master

Purchasing consumes:

```text
Party(Supplier Role)
Supplier contacts
Supplier addresses
Tax/legal identity
```

from Directory.

No separate duplicate supplier identity database.

---

# 11. Quick Create Supplier

From Purchase Order:

```text
Search Supplier
→ Not found
→ Quick Create Party
→ Supplier Role
→ Minimum required data
→ Continue Purchase Order
```

---

# 12. Same Party Can Be Customer and Supplier

Do not create duplicate Party.

Add Supplier Role and module-specific profile.

---

# 13. Supplier Purchase Profile

Purchasing owns commercial/operational defaults.

Concept:

```text
SupplierPurchaseProfile
{
    TenantId
    LegalEntityId
    PartyId

    DefaultBuyerId?
    DefaultPurchasePriceListId?
    PurchasePriceGroupId?

    DefaultCurrencyCode?
    DefaultWarehouseId?
    DefaultDeliveryAddressRef?

    DefaultLeadTimeDays?
    DefaultIncoterm?
    DefaultShippingMethodRef?

    PurchaseOrderRequired?
    SupplierReferenceRequired?

    Status
    Version
}
```

---

# 14. Financial Defaults Boundary

Payment term definitions belong FinBase.

Supplier payable/financial defaults should remain in the approved supplier/payables financial profile.

Purchasing consumes the resolved default and snapshots the selected term on the purchasing/invoice workflow where required.

---

# 15. Supplier Bank Details Boundary

Sensitive supplier beneficiary/bank details are not a generic Purchasing text field.

When payment beneficiary management is implemented:

```text
masked display
strong permission
change approval
audit
fraud controls
```

must apply.

Payments/Treasury owns money movement.

---

# 16. Supplier Status

A blocked/archived Supplier Party/profile cannot receive new Purchase Orders according to policy.

Historical documents remain queryable.

---

# 17. Buyer

Optional operational owner.

Examples:

```text
Owner
Purchasing Officer
Procurement Specialist
Category Buyer
Purchasing Manager
```

---

# 18. Purchase Team

Optional.

Small company can ignore teams entirely.

---

# 19. Procurement Channel

Optional dimension:

```text
Direct
Local Supplier
Import
Marketplace
Contract
Tender
Other
```

No separate purchasing engine per channel.

---

# 20. Purchase Order Is the Core Document

The Purchase Order is TODIR's primary purchasing commitment document.

---

# 21. Purchase Order Purpose

A PO says:

```text
We intend/commit to buy
these items/services
from this Supplier
at these agreed commercial terms
for these quantities
with this expected delivery context.
```

---

# 22. Purchase Order Is Not Supplier Invoice

A PO does not create:

```text
AP Open Item
Supplier payable
Journal Entry
Legal supplier bill
```

---

# 23. Purchase Order Is Not Physical Receipt

A PO does not increase On Hand.

---

# 24. Purchase Order Is Not Payment

No cash/bank movement.

---

# 25. Purchase Order Header

Concept:

```text
PurchaseOrder
{
    Id
    TenantId
    LegalEntityId
    BranchId?

    Number
    SupplierPartyId

    OrderDate
    CurrencyCode

    BuyerId?
    PurchaseTeamId?
    PurchaseChannelId?

    ExpectedDeliveryDate?
    DestinationWarehouseId?

    SupplierReference?
    SupplierQuoteReference?

    CommercialState
    ReceiptStateProjection
    InvoicingStateProjection
    MatchingStateProjection

    ApprovalState?

    CreatedBy
    CreatedAt
    RowVersion
}
```

---

# 26. Purchase Order Line

Concept:

```text
PurchaseOrderLine
{
    Id
    PurchaseOrderId
    Sequence

    ProductVariantId?
    Description

    OrderedQuantity
    PurchaseUomCode

    PricingSnapshot
    AgreedUnitPrice
    DiscountSnapshot?

    TaxPreviewSnapshot?

    ExpectedDeliveryDate?
    DestinationWarehouseId?

    CancelledQuantity

    ReceivedQuantityProjection
    InvoiceEligibleQuantityProjection
    InvoicedQuantityProjection

    RowVersion
}
```

---

# 27. Service Purchase

A Purchase Order line may be service/non-stock.

No Inventory Receipt is required unless the business explicitly models service acceptance separately.

---

# 28. Goods Purchase

Stock-tracked goods can integrate with Inventory Receipt.

---

# 29. Mixed Purchase Order

One PO can contain:

```text
stocked goods
non-stock goods
services
```

with line-level downstream handling.

---

# 30. Ad-Hoc Line

Authorized ad-hoc service/expense description may be allowed.

Supplier Invoice/Tax still validates required financial semantics.

---

# 31. Purchase UOM

Catalog owns UOM.

Purchasing stores:

```text
ordered quantity
purchase UOM
commercial conversion evidence/snapshot as needed
```

Inventory normalizes physical receipt to base UOM.

---

# 32. Supplier Product Reference

Purchasing may display supplier-specific SKU/reference from Catalog/reference contracts.

Do not duplicate that master.

---

# 33. PO Numbering

Via Platform Numbering.

No `MAX(number)+1`.

---

# 34. PO Draft

Editable workspace.

No financial/stock truth is created.

---

# 35. Draft Autosave

Allowed.

Must not confirm/approve automatically.

---

# 36. Draft Concurrency

Optimistic concurrency using row version / ETag.

---

# 37. Purchase Order Commercial States

Recommended compact baseline:

```text
Draft
PendingApproval
Approved
Sent/Open
PartiallyCompleted
Completed
Cancelled
Closed
```

Exact UX can hide unused states.

---

# 38. Approval State Separate Where Useful

For advanced companies:

```text
NotRequired
Pending
Approved
Rejected
```

Do not overload physical receipt/invoice state.

---

# 39. Receipt State Separate

Projection:

```text
NotRequired
NotReceived
PartiallyReceived
Received
OverReceivedException
Closed
```

Physical authority remains Inventory.

---

# 40. Invoicing State Separate

Projection:

```text
NotInvoiced
PartiallyInvoiced
Invoiced
```

Supplier Invoice authority remains Invoicing.

---

# 41. Matching State Separate

Optional projection:

```text
NotRequired
Unmatched
PartiallyMatched
Matched
Variance
Blocked
ApprovedException
```

---

# 42. Why Separate States

A valid PO can be:

```text
Commercial = Approved
Receipt = PartiallyReceived
Invoicing = Invoiced
Matching = Variance
```

Do not use one overloaded `Status`.

---

# 43. Default Small-Company Workflow

Recommended:

```text
Draft
→ Approved
→ Convert to Supplier Invoice
→ Completed/Closed according to conversion policy
```

No Goods Receipt required unless selected.

---

# 44. Internal Approval Optional

A small owner-managed business can approve/confirm directly.

---

# 45. Approval Modes

Potential:

```text
Disabled
SimpleConfirm
RiskBased
AlwaysRequired
```

---

# 46. Risk-Based Approval

Can consider:

```text
PO total
Supplier
Product category
Price deviation
Budget hook later
New supplier
Payment terms exception
Imported goods
```

---

# 47. Approval Matrix

Advanced capability.

Can route by:

```text
amount threshold
department
branch
category
legal entity
```

but is not required for MVP-simple flow.

---

# 48. Separation of Duties

Large company may require:

```text
Requester != Approver
Buyer != Final Approver
```

configurable.

---

# 49. Simple Approve UX

```text
[Approve & Continue]
```

No approval inbox if not configured.

---

# 50. PO Revision / Amendment

Before approval/commitment:

normal draft edits.

After meaningful approval/sending:

material changes create controlled amendment/reapproval as policy requires.

---

# 51. Material Purchase Change

Examples:

```text
Supplier
Product
Quantity
Price
Currency
Delivery date
Warehouse
Payment terms
```

---

# 52. Approved PO Price Change

Can require reapproval if policy threshold exceeded.

---

# 53. Approved PO Quantity Increase

Can require:

```text
reapproval
updated price
updated tax
updated expected receipt
```

as configured.

---

# 54. Approved PO Quantity Decrease

Cannot reduce below already irreversible:

```text
received quantity
invoiced quantity
```

without correction/return flow.

---

# 55. Cancel Remaining Quantity

Example:

```text
Ordered    100
Received    40
Cancelled   60
```

Valid.

---

# 56. Do Not Delete Received History

Cancellation applies to open commitment only.

---

# 57. Purchase Order Close

Close only when open commercial quantity has been fulfilled/cancelled and company policy allows.

---

# 58. Supplier Communication

PO can be rendered/sent by:

```text
PDF
Email
WhatsApp
Print
External integration
```

Communication does not change accounting truth.

---

# 59. "Sent" Is Not "Received"

Supplier communication state and inventory receipt state are independent.

---

# 60. Purchase Order PDF

Rendering is presentation artifact.

Structured PO data remains source truth.

---

# 61. Supplier Acceptance

Optional.

Many companies consider approved/sent PO sufficient.

Do not force supplier portal/acceptance workflow.

---

# 62. Supplier Portal Not Required

Supplier never needs a TODIR account for core Purchasing.

---

# 63. Supplier Confirmation Optional

If company wants it:

```text
Confirmed by phone/email
Supplier acknowledgment
Expected date confirmed
```

record as optional commercial metadata/history.

---

# 64. Purchase Request Is Optional

A Purchase Request represents internal demand before a supplier commitment.

---

# 65. Default Fresh Tenant

Purchase Request OFF.

User can create PO directly.

---

# 66. When Purchase Request Is Useful

Examples:

```text
Departments request supplies
Requester cannot choose supplier
Central purchasing controls buying
Budget/approval process required
```

---

# 67. Purchase Request Header

Concept:

```text
PurchaseRequest
{
    Id
    LegalEntityId
    BranchId?
    RequesterUserId
    DepartmentRef?
    RequiredByDate?
    Priority?
    State
}
```

---

# 68. Purchase Request Line

```text
ProductVariantId?
Description
RequestedQty
Uom
PreferredSupplierId?
Reason?
```

---

# 69. Request Is Not PO

No supplier commitment until converted/used to create PO.

---

# 70. One Request to Multiple POs

Advanced procurement may split lines across suppliers.

---

# 71. Multiple Requests to One PO

Advanced consolidation possible.

Build only when required.

---

# 72. Purchase Request Approval

Optional independent internal workflow.

---

# 73. RFQ Is Optional

Request for Quotation is advanced procurement capability.

---

# 74. RFQ Purpose

Ask multiple suppliers for commercial offers before PO.

---

# 75. RFQ Does Not Create Supplier Invoice

No AP/stock effect.

---

# 76. RFQ Supplier Responses

Can capture:

```text
Supplier
Quoted price
Currency
Lead time
Validity
Minimum order
Delivery terms
Notes
```

---

# 77. Supplier Quote Comparison

Optional view:

```text
Supplier A   Price 100  Lead 3 days
Supplier B   Price 95   Lead 7 days
Supplier C   Price 103  Lead 2 days
```

---

# 78. Lowest Price Is Not Always Winner

Comparison may include:

```text
lead time
quality rating later
payment terms
delivery reliability
currency
commercial conditions
```

---

# 79. Supplier Selection Decision

Store decision/evidence if RFQ workflow used.

---

# 80. RFQ → PO

Selected response can create Purchase Order with agreed snapshot.

---

# 81. Pricing Ownership

Pricing is reusable price source of truth for both sell and buy directions.

Purchasing consumes:

```text
Direction = Buy
```

---

# 82. No Purchasing Price Rule Engine

Do not copy supplier-specific Pricing logic into Purchasing.

---

# 83. Purchase Pricing Context

Purchasing supplies:

```text
Supplier Party
Product Variant
Qty
UOM
Currency
Pricing Date
Purchase Price List?
Purchase Group?
Legal Entity
Commercial Context
```

---

# 84. Pricing Batch

Many-line PO uses batch price resolution.

No N+1.

---

# 85. Agreed PO Price Snapshot

PO stores the commercial agreed result/evidence.

---

# 86. Supplier Price Changes Later

Do not rewrite approved/committed PO.

---

# 87. Manual Purchase Price Override

Allowed with permission/policy.

---

# 88. Price Override Evidence

Store:

```text
resolved price
agreed/override price
deviation
reason
actor
approval ref if required
```

---

# 89. Purchase Discount

Can be supplier line/header discount according to commercial rules.

Tax Engine consumes resulting bases.

---

# 90. Tax Preview

PO may preview purchase tax using Tax Engine.

Supplier Invoice finalization remains legal/financial tax authority.

---

# 91. No Purchasing Tax Formula

Forbidden.

---

# 92. Price/Tax Difference at Invoice

Matching detects and explains variance.

It does not silently change original PO history.

---

# 93. PO Currency

One document currency baseline.

---

# 94. FX Ownership

FinBase/Pricing/Invoicing.

Purchasing does not maintain FX tables.

---

# 95. Money Precision

Use approved FinBase decimal/rounding rules.

---

# 96. Expected Delivery Date

Commercial expectation only.

Does not mean goods received.

---

# 97. Destination Warehouse

Optional.

Only relevant for stock/warehouse flows.

---

# 98. Service Company

Can use Purchasing without Inventory active.

---

# 99. Inventory Optional Capability

A service company can:

```text
PO
→ Supplier Invoice
```

with no Warehouse/Receipt setup.

---

# 100. Purchase Order Does Not Increase Stock

Locked.

---

# 101. Supplier Invoice Does Not Automatically Increase Stock

Locked.

Physical receipt is separate Inventory truth.

---

# 102. Goods Receipt Is Optional Workflow

If company needs physical receipt tracking:

```text
PO
→ Goods Receipt
→ Supplier Invoice
```

---

# 103. Inventory Owns Goods Receipt

Purchasing requests/links receipt.

Inventory owns:

```text
physical quantity
warehouse
location
lot
serial
movement
valuation
```

---

# 104. Purchasing Does Not Write Stock

Never.

---

# 105. Simple Receive UX

From PO:

```text
[Receive]
```

TODIR opens/prefills Inventory receipt.

---

# 106. Receive & Bill One Action

For simple company:

```text
[Receive & Create Supplier Invoice]
```

can orchestrate both domains.

---

# 107. PO → Supplier Invoice Without Receipt

Valid when:

```text
service purchase
non-stock goods
invoice before receipt
company simple workflow
legal/business policy permits
```

---

# 108. PO → Supplier Invoice + Receive Now

Valid for simple stocked-goods purchase.

One user action can create:

```text
Supplier Invoice
Inventory Receipt
```

with correct internal boundaries.

---

# 109. Partial Receipt

First-class advanced/basic capability when Inventory active.

Example:

```text
Ordered 100
Received 60
Remaining 40
```

---

# 110. Multiple Receipts

Valid:

```text
60
20
20
```

against one PO line.

---

# 111. Receipt Quantity Projection

Purchasing can store strongly consistent/rebuildable projection/ref.

Inventory remains authority.

---

# 112. Receipt Source Identity

Inventory receipt links:

```text
PurchaseOrderId
PurchaseOrderLineId
ReceiptPurpose
CommandId
```

for idempotency.

---

# 113. Over-Receipt

Policy:

```text
Disallow
AllowWithinTolerance
RequireApproval
Allow
```

---

# 114. Default Over-Receipt Policy

Recommended:

```text
Disallow
```

unless explicitly configured.

---

# 115. Over-Receipt Tolerance

Can be:

```text
absolute qty
percentage
```

per company/supplier/category policy.

---

# 116. Under-Receipt

Can leave remainder open or allow closing short with reason.

---

# 117. Close Short

Example:

```text
Ordered 100
Received 98
Close remaining 2
Reason: supplier short shipment
```

history retained.

---

# 118. Receipt Date vs PO Date

Separate.

---

# 119. Receipt Does Not Automatically Mean Invoice Received

Critical.

---

# 120. GRNI / Received-not-Invoiced Seam

When perpetual accounting requires it:

Inventory/Ledger can account for receipt before supplier bill.

Purchasing supplies source linkage.

Exact accounting belongs Inventory/Ledger.


# 121. Supplier Invoice Is Owned by Invoicing

Purchasing never creates/updates supplier invoice tables directly.

---

# 122. Purchasing-Origin Supplier Invoice

Purchasing can request:

```text
Create Supplier Invoice
from:
- Purchase Order
- Goods Receipt
- selected PO/Receipt lines
```

Invoicing creates the actual financial document.

---

# 123. Direct Supplier Invoice Still Exists

Invoicing must support:

```text
Supplier Invoice without PO
```

for:

```text
rent
utilities
professional services
ad-hoc expenses
legacy suppliers
small companies
```

Purchasing is not required for every supplier bill.

---

# 124. PO to Supplier Invoice — Default

Simple baseline:

```text
Approved PO
→ Convert to Supplier Invoice
```

No extra matching screen unless policy requires it.

---

# 125. Conversion Is Not Type Mutation

Do not change:

```text
PurchaseOrder.type = SupplierInvoice
```

Create a new Invoicing document and preserve typed source relation.

---

# 126. Source Link

```text
Supplier Invoice
OriginatesFrom
Purchase Order
```

or:

```text
Supplier Invoice
OriginatesFrom
Goods Receipt
```

where applicable.

---

# 127. PO Remains Historical

After conversion:

```text
PO-100
→ Supplier Invoice BILL-900
```

both remain queryable.

---

# 128. Conversion Idempotency

Double click/retry cannot create duplicate Supplier Invoice.

---

# 129. Conversion Source Identity

Use:

```text
SourceModule = Purchasing
SourceType = PurchaseOrder / Receipt
SourceDocumentId
SourceLineId?
InvoicePurpose
```

with uniqueness rules.

---

# 130. Supplier External Invoice Number

Supplier Invoice can store supplier's own document number.

Invoicing owns the canonical field/duplicate detection.

Purchasing may capture/pass it during conversion UX.

---

# 131. Duplicate Supplier Invoice Warning

Before finalization, detect:

```text
Same Supplier
Same external invoice number
Same Legal Entity
```

according to Invoicing policy.

Purchasing must not duplicate this validation.

---

# 132. Supplier Invoice Dates

Invoicing owns:

```text
document date
posting date
tax date
issue date if relevant
```

Purchasing may suggest source dates, not own final financial semantics.

---

# 133. PO Payment Terms

Selected PO terms are commercial intent/snapshot.

Supplier Invoice finalization freezes payment schedule via FinBase/Subledger rules.

---

# 134. Quantity to Invoice

When no receipt-based control:

```text
Invoice against PO ordered quantity
```

according to company policy.

---

# 135. Receipt-Based Billing

When configured:

```text
Invoice only received quantity
```

or:

```text
Invoice up to received quantity
```

---

# 136. Prepayment / Invoice Before Receipt

Valid for suppliers requiring advance/prepayment where business/legal process supports it.

Do not force receipt-first.

---

# 137. Prepayment Boundary

Supplier advance/payment is Payments/Subledger truth.

Purchasing can reference procurement source.

---

# 138. Invoice Before PO Approval

Default block.

Can exist only through explicit exception policy.

---

# 139. Partial Supplier Invoice

Supported when PO workflow active.

Example:

```text
Ordered 100
Invoiced 60
Remaining invoiceable 40
```

---

# 140. Multiple Supplier Invoices

One PO can create multiple Supplier Invoices.

---

# 141. One Supplier Invoice Across Multiple POs

Possible advanced capability.

Not mandatory MVP.

---

# 142. Invoice Eligibility

Purchasing owns commercial/source eligibility calculation.

Invoicing owns actual invoice.

---

# 143. Invoice Eligibility Is Not Payable

No payable exists until Supplier Invoice is finalized.

---

# 144. Supplier Invoice Finalization

Follows Invoicing pack:

```text
Supplier Invoice
→ Subledger Payable/Open Item
→ Ledger
→ Tax/Compliance where relevant
```

---

# 145. Purchasing Never Creates AP Balance

Subledger owns payable truth.

---

# 146. Purchasing Never Sets Invoice Paid

Payments/Subledger own settlement.

---

# 147. Supplier Balance Display

Purchasing UI can show:

```text
Outstanding
Overdue
Unapplied advance
```

via Subledger read contract, permissioned.

Read-only.

---

# 148. Payment Shortcut

PO/Supplier Invoice screen may expose:

```text
Pay Supplier
```

which delegates to Payments.

Purchasing never owns money movement.

---

# 149. Matching Overview

Matching is optional control between commercial order, physical receipt and supplier bill.

---

# 150. Matching Modes

Recommended:

```text
Disabled
2Way
3Way
```

---

# 151. Matching Disabled

Simple company:

```text
PO
→ Supplier Invoice
```

No match workflow.

---

# 152. 2-Way Match

Compare:

```text
Purchase Order
↔ Supplier Invoice
```

---

# 153. 3-Way Match

Compare:

```text
Purchase Order
↔ Goods Receipt
↔ Supplier Invoice
```

---

# 154. Matching Does Not Own Source Documents

It evaluates refs/snapshots from Purchasing, Inventory and Invoicing.

---

# 155. Match Dimensions

Typical:

```text
Supplier
Product/Line
Quantity
Unit price
Discount
Tax basis
Currency
UOM
```

---

# 156. Quantity Variance

Example:

```text
PO Qty       100
Received      98
Invoice Qty  100
```

System identifies quantity variance.

---

# 157. Price Variance

Example:

```text
PO Price      100
Invoice Price 105
Variance        5
```

---

# 158. Tax Variance

PO tax is preview.

Supplier Invoice tax is final invoice-side calculation.

Matching can explain difference.

---

# 159. UOM Variance

Compare on normalized/comparable quantities while preserving source UOM evidence.

---

# 160. Currency Variance

If supplier invoice currency differs from PO:

default block/warn according to policy.

Never silently convert as though same commercial agreement.

---

# 161. Matching Tolerances

Configurable:

```text
Quantity tolerance
Price absolute tolerance
Price percentage tolerance
Tax tolerance if legally appropriate
```

---

# 162. Match Result

Concept:

```text
PurchaseMatchResult
{
    PurchaseOrderId
    SupplierInvoiceId
    ReceiptRefs[]

    QuantityVariance
    PriceVariance
    TaxVariance

    Status
    TolerancePolicyVersion
    Decision
    DecidedBy?
    Reason?
}
```

---

# 163. Matching Status

```text
Matched
WithinTolerance
Variance
Blocked
ApprovedException
```

---

# 164. Matching Does Not Edit Invoice Automatically

If invoice differs:

```text
block
approve exception
correct invoice draft
request supplier correction
```

according to workflow.

---

# 165. Matching Exception Approval

Optional.

High variance can require Purchasing/Finance approval.

---

# 166. Small Company Does Not See Matching

When disabled:

no matching menu/status clutter.

---

# 167. Match Before Finalization

Preferred for controlled AP workflow:

```text
Supplier Invoice Draft
→ Match
→ Finalize
```

---

# 168. Match After Finalization

Possible only where business process requires retrospective reconciliation.

Correction must use Invoicing credit/debit/correction mechanisms.

---

# 169. Matching History

Preserve:

```text
original variance
tolerance
decision
approver
reason
source versions
```

---

# 170. Supplier Return

Commercial process for returning goods/services to supplier.

---

# 171. Supplier Return Is Multi-Domain

Potential effects:

```text
Purchasing Return Request
Inventory Return Issue
Invoicing Supplier Credit/Vendor Credit
Payments Refund/Offset if money moves
```

---

# 172. Purchasing Owns Return Commercial Intent

Purchasing records:

```text
what is being returned
to which supplier
from which original source
why
expected resolution
```

---

# 173. Inventory Owns Physical Return

Stock leaves via Inventory ReturnOut/Issue.

---

# 174. Invoicing Owns Supplier Credit

Financial/legal credit document remains Invoicing-owned.

---

# 175. Payment Refund Boundary

Payments owns cash returned from supplier.

---

# 176. Return Without Physical Stock

Valid for:

```text
service credit
price adjustment
billing correction
short service
```

---

# 177. Physical Return Before Credit

Valid:

```text
Return goods
→ wait for supplier credit note
```

---

# 178. Supplier Credit Before Physical Return

Possible under controlled workflow.

States remain separate.

---

# 179. Return Eligibility

Cannot return more than eligible received/on-hand quantity tied to source without exception flow.

---

# 180. Return Reason

Typed:

```text
Damaged
WrongItem
QualityIssue
OverDelivery
SupplierRecall
CommercialAgreement
BillingCorrection
OtherApproved
```

---

# 181. Supplier Return History

Original PO/Receipt/Invoice remain unchanged.

Append typed return/correction links.

---

# 182. Supplier Replacement

Replacement is:

```text
Return old goods
+
new receipt / replacement source
```

Do not mutate original receipt.

---

# 183. Landed Cost Overview

Landed Cost is optional.

It allocates additional acquisition costs into inventory value when accounting policy requires.

---

# 184. Typical Landed Cost Components

```text
Freight
Customs
Insurance
Brokerage
Handling
Non-recoverable tax
Port charges
```

---

# 185. Purchasing Does Not Own Inventory Valuation

Purchasing provides/source-links cost components.

Inventory owns valuation allocation/result.

Ledger posts accounting facts.

---

# 186. Landed Cost Source

Can come from:

```text
Supplier Invoice
Freight provider bill
Customs document
Manual approved charge
```

---

# 187. Landed Cost Allocation

Inventory pack supports bases such as:

```text
Quantity
Weight
Volume
Value
Manual approved allocation
```

Purchasing can orchestrate but not duplicate valuation engine.

---

# 188. Landed Cost Timing

Can be known:

```text
before receipt
at receipt
after receipt
after some stock already sold
```

Valuation adjustment must remain explicit.

---

# 189. Late Landed Cost

Does not rewrite original receipt movement.

Inventory creates valuation adjustment.

---

# 190. Landed Cost Optional UX

Small local buyer never sees landed cost fields.

---

# 191. Supplier Performance

Useful but not core source-of-truth requirement for first implementation.

Potential metrics:

```text
On-time delivery
Price variance
Quantity accuracy
Return rate
Quality issue rate
Lead time reliability
```

---

# 192. Supplier Performance Is Derived

Do not store manually maintained "performance score" as financial truth.

---

# 193. Vendor Rating Later

Can use normalized metrics + reviewed qualitative score.

---

# 194. Reorder/Planning Boundary

Inventory/Planning may suggest demand.

Purchasing owns converting approved demand into procurement commitment.

---

# 195. Reorder Suggestion

Potential future flow:

```text
Inventory Reorder Suggestion
→ Purchase Request
or
→ Draft Purchase Order
```

never auto-confirm without configured authority.

---

# 196. MRP Boundary

Manufacturing/Planning can create purchase demand.

Purchasing executes supplier commitment.

---

# 197. Project Purchase Boundary

Projects can provide cost center/project reference.

Purchasing retains PO ownership.

---

# 198. Expense Purchase Boundary

Expense/AP can create direct Supplier Invoice without PO where policy permits.

---

# 199. Asset Purchase Boundary

Purchase can later link to Asset acquisition process.

Purchasing does not own Fixed Asset register.

---

# 200. Import Purchase Boundary

Import/customs workflows may extend Purchase Order/shipment context later.

Do not hardcode every trade document into core PO.

---

# 201. Purchase Request vs Requisition

Use one normalized concept in TODIR terminology.

Recommended UI name:

```text
Purchase Request
```

---

# 202. Supplier RFQ vs Supplier Quotation

TODIR sends RFQ.

Supplier response is Supplier Quote Response.

Avoid confusing with Sales Quotation.

---

# 203. No Supplier Portal Requirement

RFQ responses can be recorded manually from:

```text
email
PDF
phone
WhatsApp
external file
```

---

# 204. Attachment Support

Via Platform Files.

Examples:

```text
Supplier quotation PDF
Purchase contract
Delivery note
Quality certificate
Customs document
Supplier invoice copy
Return evidence
```

---

# 205. Attachment Does Not Create Business State

Uploading quote PDF does not automatically approve PO.

---

# 206. Internal Notes vs Supplier Notes

Separate.

Never send internal note accidentally.

---

# 207. Terms Snapshot

Approved PO should preserve customer/supplier-facing terms/version.

---

# 208. Incoterm

Optional.

Useful for import/international procurement.

Not required for local small company.

---

# 209. Shipping Method

Optional reference.

Full logistics provider execution belongs future Logistics.

---

# 210. Expected Receipt Schedule

Simple PO can have one expected date.

Advanced can later support line/schedule quantities.

---

# 211. Multiple Delivery Schedules

Future/optional:

```text
40 on date A
60 on date B
```

Do not overbuild unless needed.

---

# 212. PO Line Open Quantity

Concept:

```text
OpenQty
=
Ordered
- Cancelled
- ClosedShort
```

Physical/invoice dimensions remain separate.

---

# 213. Receipt Remaining

Projection:

```text
ReceiptRemaining
=
Ordered
- Cancelled
- Received
- ClosedShort
```

subject to over-receipt policy.

---

# 214. Invoice Remaining

Depends configured billing basis:

```text
PO-based
Receipt-based
Manual
```

---

# 215. Completion Rule

PO may be complete when:

```text
all commercial quantities fulfilled/cancelled
and
required invoice/match conditions satisfied
```

Company policy decides.

---

# 216. Auto-Close

Optional.

Default can auto-close simple PO after full conversion if no receipt workflow remains.

---

# 217. Manual Close

Allowed for authorized user with reason when residual quantity/value remains.

---

# 218. Reopen Closed PO

Controlled.

Do not casually reopen if downstream financial/stock history exists.

---

# 219. Purchase History Principle

> **Every material procurement decision must remain explainable without rewriting the past.**

---

# 220. Typed History

Use:

```text
PurchaseOrderAmendment
ApprovalDecision
PurchaseStateHistory
SupplierQuoteSelection
ReceiptLink
InvoiceLink
MatchResult
ReturnLink
Audit
```

---

# 221. PO History Must Answer

```text
Who created?
Who approved?
What was ordered?
At what agreed price?
What changed?
What was received?
What was invoiced?
What variance occurred?
What was returned?
What was cancelled?
```

---

# 222. Approved PO Is Historical Commercial Evidence

Master price/product/supplier profile changes do not rewrite it.

---

# 223. Supplier Rename

Historical PO remains understandable via Party refs + necessary snapshots.

---

# 224. Product Rename

Historical line description/SKU snapshot preserved as required.

---

# 225. Price Snapshot

Stores relevant Pricing evidence, not full rule graph.

---

# 226. Tax Preview Snapshot

Stores preview evidence.

Supplier Invoice remains financial/legal authority.

---

# 227. UOM Snapshot

Preserve agreed purchase UOM/context.

---

# 228. No Generic History Text as Source

UI can show one timeline, but backend histories remain typed.

---

# 229. Purchasing Search

Search by:

```text
PO Number
Supplier
Supplier Reference
Product
Buyer
Branch
State
Date
Warehouse
```

server-side.

---

# 230. PO List

Default columns:

```text
Number
Supplier
Date
Total
State
Expected Date
Buyer
```

---

# 231. Optional Columns

Only when enabled:

```text
Receipt State
Invoicing State
Matching State
Approval
Warehouse
```

---

# 232. Small Company Menu

Possible:

```text
Purchasing
├── Purchase Orders
└── Purchase History
```

No empty enterprise menus.

---

# 233. Advanced Menu

Can add:

```text
Purchase Requests
RFQs
Receipts
Matching
Returns
Supplier Performance
```

when capabilities active.

---

# 234. Supplier Workspace

Can compose:

```text
Open POs
Recent Bills
Outstanding Payables
Recent Receipts
Returns
```

using cross-module reads.

---

# 235. No Giant Supplier Aggregate

Load tabs/read models lazily.

---

# 236. Purchasing Dashboard — Simple

```text
Draft POs
Approved/Open POs
Due/expected deliveries
Recent supplier invoices
```

---

# 237. Purchasing Dashboard — Advanced

Add:

```text
Pending approvals
Late receipts
Matching exceptions
Open returns
Supplier performance
```

---

# 238. Reporting Ownership

Purchasing reports procurement activity.

Inventory reports stock.

Finance reports expense/AP/accounting.

---

# 239. Core Purchasing Reports

```text
Purchase Orders
Open Purchase Orders
Purchases by Supplier
Purchases by Product
Purchases by Buyer
Expected Receipts
PO vs Receipt
PO vs Invoice
Price Variance
Quantity Variance
Purchase Returns
```

---

# 240. PO Value Is Not Expense Automatically

Do not label PO value as accounting expense.

It is commercial commitment/order book.


# 241. Commitment Reporting

If company wants procurement commitments:

```text
PO open value
Expected cash requirement
Expected receipt value
```

label as forecast/commitment, not booked liability.

---

# 242. AP Liability Starts at Supplier Invoice

Canonical payable truth belongs Subledger after invoice finalization.

---

# 243. Purchase Tax Recoverability

Tax Engine determines tax treatment/recoverability context.

Purchasing only previews/source-links.

---

# 244. Non-Recoverable Tax

Can contribute to inventory cost/expense according to Tax/Inventory/accounting policy.

Purchasing does not hardcode this allocation.

---

# 245. Withholding

If supplier payment/invoice requires withholding, Tax/Invoicing/Payments/Country Pack own legal treatment.

Purchasing should only carry relevant source context.

---

# 246. Multi-Currency Supplier

Supplier can transact in different currencies by Legal Entity/context.

Do not force one Directory currency.

---

# 247. Supplier Default Currency

Commercial convenience in Purchasing profile.

PO explicitly stores its own currency.

---

# 248. Supplier Lead Time

Can default from profile/product-supplier sourcing data.

It is planning input, not immutable delivery truth.

---

# 249. Supplier Minimum Order

Optional sourcing rule.

Can warn/block if contract requires.

---

# 250. Supplier Minimum Quantity

Optional Product×Supplier purchasing context.

Do not force every supplier/product combination setup.

---

# 251. Preferred Supplier

Catalog/Planning/Purchasing may reference preferred supplier relation.

Preference is suggestion, not exclusive purchasing truth.

---

# 252. Multiple Suppliers per Product

Required.

TODIR must not model one Product → one Supplier.

---

# 253. Supplier-Specific UOM

Catalog/reference mapping can support supplier purchase UOM/reference.

Purchasing consumes it.

---

# 254. Buyer Override

Authorized Buyer may select alternate Supplier/UOM/price with evidence.

---

# 255. Contract Price Boundary

Pricing can store supplier contract price/rule.

Purchasing snapshots resolved result.

---

# 256. Supplier Contract Boundary

Future Contract module can own contract legal document/terms.

Purchasing references contract ID/version.

---

# 257. Budget Boundary

Budgeting can later provide:

```text
CheckBudget
→ Allow / Warn / Block / RequireApproval
```

Purchasing must not duplicate Budget engine.

---

# 258. Budget Is Optional

No Budget module means no purchasing blocker.

---

# 259. Department/Cost Center

PO can carry accounting dimension intent if Ledger/organization policy supports it.

Ledger final posting determines accounting dimension use.

---

# 260. Project Dimension

Optional source context.

---

# 261. Purchase Expense Allocation

Supplier Invoice can allocate expense/accounting through Ledger rules.

Purchasing does not store hardcoded GL Account IDs in commercial PO logic.

---

# 262. No GL Account IDs in Core PO Business Logic

Critical.

---

# 263. Accounting Preview

Optional finance users can inspect expected accounting through Ledger preview contracts.

Not required for Buyer UX.

---

# 264. Warehouse User Experience

If Goods Receipt enabled:

Warehouse user sees:

```text
Supplier
PO
Products
Expected Qty
Received Qty
Warehouse/Location
Lot/Serial if needed
```

No supplier cost if permission disallows.

---

# 265. Buyer User Experience

Buyer sees:

```text
Supplier
Product
Qty
Purchase Price
Expected Date
Receipt progress
Invoice progress
```

---

# 266. Finance/AP User Experience

Finance sees:

```text
Supplier Invoice
Match status
Price/Qty variance
Payable
Due date
Payment status
```

through appropriate module/read composition.

---

# 267. Cost Visibility

Purchase price is commercially sensitive.

Permission can separate:

```text
purchasing.price.read
purchasing.price.override
```

Warehouse receiver does not automatically see price.

---

# 268. Approval Visibility

Requester can see status without seeing sensitive approval comments if restricted.

---

# 269. Progressive Disclosure

Small company PO form:

```text
Supplier
Product
Qty
Price
Expected Date
[Approve & Create Bill]
```

Advanced fields collapsed.

---

# 270. Advanced PO Details

Expandable:

```text
Warehouse
Buyer
Payment Terms
Incoterm
Supplier Reference
Approval
RFQ Source
Matching
Cost Center
Attachments
```

---

# 271. No Empty Complexity

If Matching disabled, no Match tab.

If Inventory disabled, no Receipt tab.

If Purchase Request disabled, no Request menu.

---

# 272. Smart Defaults

Examples:

```text
default supplier currency
default buyer
last/default warehouse
default purchase UOM
default price context
default payment term from financial profile
default expected date from lead time
```

---

# 273. User Override

Authorized users can override suggestions.

Suggestion is not source authority.

---

# 274. No Re-Entry Across Modules

When PO already knows:

```text
Supplier
Product
Qty
Price
Warehouse
```

Goods Receipt/Supplier Invoice creation should be prefilled.

---

# 275. PO → Receipt Prefill

Inventory receives exact source lines and quantities.

User confirms actual received quantities only.

---

# 276. PO → Supplier Invoice Prefill

Invoicing receives supplier/source/commercial line snapshots.

User enters supplier invoice number/date and verifies differences as needed.

---

# 277. Supplier Invoice OCR Future

AI/OCR may extract supplier bill into Draft.

User verifies.

OCR never auto-posts/finalizes.

---

# 278. AI Opportunities

AI may:

```text
suggest supplier
compare quotes
summarize supplier history
detect unusual price increase
suggest PO from reorder demand
extract supplier invoice draft
explain matching variance
```

---

# 279. AI Cannot

```text
approve PO without authority
create payment
change supplier bank details
bypass match block
directly receive stock
post supplier invoice
change tax
write database
```

---

# 280. AI Path

```text
AI Tool Layer
→ Permissions
→ Purchasing Application Command/Query
→ Domain
→ Audit
```

---

# 281. Notifications

Optional:

```text
PO awaiting approval
Supplier delivery overdue
Partial receipt outstanding
Invoice match exception
Return awaiting action
```

---

# 282. Notification Is Not Transaction Dependency

External send failure does not corrupt PO.

---

# 283. Scheduled Follow-Up

Jobs can detect overdue expected receipts.

---

# 284. PO Due/Overdue

Expected delivery date comparison only.

Does not fabricate receipt state.

---

# 285. Schema

Recommended:

```text
business_purchasing
```

---

# 286. Purchasing DbContext

Owns Purchasing tables only.

No Inventory/Invoicing/Subledger/Ledger EF navigation.

---

# 287. Core Tables

Potential:

```text
supplier_purchase_profile

purchase_request
purchase_request_line

purchase_rfq
purchase_rfq_supplier
purchase_rfq_response
purchase_rfq_response_line
supplier_selection_decision

purchase_order
purchase_order_line
purchase_order_amendment
purchase_order_amendment_line

purchase_approval_decision
purchase_state_history

purchase_receipt_link
purchase_invoice_link

purchase_match_result
purchase_match_line_result

supplier_return_request
supplier_return_request_line
purchase_return_link

purchase_external_source_ref

purchase_order_list_projection
purchase_exception_projection
```

Do not build every optional table before capability is implemented.

---

# 288. Purchase Order Table

Stores current operational header/state.

---

# 289. PO Line Table

Stores commercial commitment lines.

---

# 290. Amendment Table

Append-only approved/confirmed change evidence where required.

---

# 291. Receipt Link

Typed reference to Inventory receipt operation.

Not duplicate stock data.

---

# 292. Invoice Link

Typed reference to Supplier Invoice.

---

# 293. Match Result Table

Stores matching evaluation/decision, not copied invoice/receipt source truth.

---

# 294. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 295. Runtime Role

No ownership/BYPASSRLS.

---

# 296. Tenant Context

Explicit transaction with:

```text
SET LOCAL app.tenant_id = ...
```

following Platform Kernel rules.

---

# 297. Legal Entity Safety

PO belongs one Legal Entity.

Receipt/Invoice targets must belong same entity unless explicit intercompany flow.

---

# 298. Branch Scope

Authorization can restrict users by Branch.

---

# 299. Supplier Party Scope

Validate Party visibility/role.

---

# 300. Composite/Tenant-Aware FKs

Prevent cross-tenant/entity links.

---

# 301. UUIDv7

Use for IDs.

---

# 302. Row Version

Mutable Draft/PO uses optimistic concurrency.

---

# 303. Numeric Types

C# `decimal` + PostgreSQL `numeric`.

---

# 304. JSONB

Use only for bounded snapshots/rare metadata.

Do not hide hot/search fields.

---

# 305. Purchasing Contracts

Locked naming:

```text
Todir.Purchasing.Contracts
```

---

# 306. Hard/Optional Dependencies

Likely hard:

```text
Platform
Directory
Catalog
Pricing
FinBase
Invoicing
```

Optional/capability-driven:

```text
Inventory
Approvals
Subledger read/financial defaults
Payments read/quick action
Budgeting later
Projects later
```

Exact manifest follows dependency DAG.

---

# 307. Inventory Optional

Critical.

Purchasing works for service/expense procurement without Inventory.

---

# 308. Matching Optional

No dependency cost when disabled.

---

# 309. Request/RFQ Optional

No menu/workflow cost when disabled.

---

# 310. API Base

```text
/api/v1/purchasing
```

---

# 311. Purchase Order APIs

Representative:

```text
GET  /purchase-orders
POST /purchase-orders
GET  /purchase-orders/{id}
PUT  /purchase-orders/{id}

POST /purchase-orders/{id}/approve
POST /purchase-orders/{id}/reject
POST /purchase-orders/{id}/amend
POST /purchase-orders/{id}/cancel-remaining
POST /purchase-orders/{id}/close

POST /purchase-orders/{id}/create-supplier-invoice
POST /purchase-orders/{id}/receive
POST /purchase-orders/{id}/receive-and-bill
```

---

# 312. Purchase Request APIs

Only if capability enabled:

```text
GET  /purchase-requests
POST /purchase-requests
POST /purchase-requests/{id}/approve
POST /purchase-requests/{id}/convert-to-po
```

---

# 313. RFQ APIs

Optional:

```text
POST /rfqs
POST /rfqs/{id}/send
POST /rfqs/{id}/responses
POST /rfqs/{id}/select-supplier
POST /rfqs/{id}/convert-to-po
```

---

# 314. Match APIs

Optional:

```text
POST /matches/preview
POST /matches/{id}/approve-exception
POST /matches/{id}/reject
```

---

# 315. Return APIs

```text
POST /supplier-returns
GET  /supplier-returns/{id}
POST /supplier-returns/{id}/approve
POST /supplier-returns/{id}/execute
```

---

# 316. No Generic `SetStatus`

State changes only through business commands.

---

# 317. No `SetReceived`

Receipt state comes from Inventory result.

---

# 318. No `SetInvoiced`

Invoicing link/result updates projection.

---

# 319. No `SetPaid`

Forbidden.

---

# 320. No `SetStock`

Forbidden.

---

# 321. No `SetJournalEntry`

Forbidden.

---

# 322. PO Create Command

Accept user intent only.

No authoritative:

```text
stock balance
invoice state
tax result
payment status
GL accounts
```

from client.

---

# 323. Approve PO Command

Concept:

```text
ApprovePurchaseOrder
{
    PurchaseOrderId
    ExpectedVersion
    DecisionReason?
    IdempotencyKey
}
```

---

# 324. Create Supplier Invoice Command

Concept:

```text
CreateSupplierInvoiceFromPO
{
    PurchaseOrderId
    SelectedLines[]
    InvoiceQuantity[]
    SupplierExternalInvoiceNumber?
    SupplierInvoiceDate?
    ReceiveNow?
    IdempotencyKey
}
```

Invoicing remains authority.

---

# 325. Receive Command

Concept:

```text
ReceivePurchaseOrder
{
    PurchaseOrderId
    Lines[]
    ActualReceivedQty
    WarehouseId
    Location/Lot/Serial context as required
    IdempotencyKey
}
```

Inventory validates physical details.

---

# 326. Receive-And-Bill Composite

Can orchestrate:

```text
Inventory Receipt
+
Supplier Invoice
+
source links
+
audit/outbox
```

through Transaction Coordinator where all-or-nothing is required.

---

# 327. Client Does Not Supply Final Receipt State

Backend computes from Inventory result.

---

# 328. Client Does Not Supply Final Match Status

Backend matching engine computes.

---

# 329. Command Idempotency

Mandatory for:

```text
approve
convert to invoice
receive
receive-and-bill
cancel remaining
close
RFQ conversion
return execution
matching exception decision
external import
```

---

# 330. Same Idempotency Key Different Payload

Conflict.

---

# 331. Double Approve

Returns same approved result/no duplicate side effect.

---

# 332. Double Convert

One Supplier Invoice target only for same source purpose.

---

# 333. Double Receive

Cannot duplicate stock receipt.

Inventory source idempotency protects.

---

# 334. Approve vs Edit Race

Stale version rejected.

---

# 335. Receive vs Cancel Race

Cannot cancel quantity already received.

---

# 336. Invoice vs Amendment Race

Amendment cannot reduce commercial line below already invoiced/received facts.

---

# 337. Match vs Invoice Edit Race

Match preview/result uses source version and becomes stale if invoice draft changes.

---

# 338. Error Codes

Stable examples:

```text
purchasing.supplier_not_ready
purchasing.supplier_blocked
purchasing.po_not_found
purchasing.po_stale
purchasing.po_not_approvable
purchasing.po_not_approved
purchasing.po_already_closed
purchasing.po_conversion_conflict

purchasing.price_override_not_allowed
purchasing.approval_required

purchasing.inventory_not_available
purchasing.receipt_not_allowed
purchasing.over_receipt_not_allowed
purchasing.receipt_quantity_conflict

purchasing.invoice_quantity_exceeded
purchasing.invoice_source_conflict
purchasing.supplier_invoice_duplicate

purchasing.match_variance
purchasing.match_blocked
purchasing.match_stale

purchasing.return_quantity_exceeded
purchasing.return_not_allowed

purchasing.idempotency_conflict
purchasing.permission_denied
```

---

# 339. Business-Friendly Stale PO Message

> "تم تعديل أمر الشراء بواسطة مستخدم آخر. حدّث الأمر قبل الاعتماد."

---

# 340. Over-Receipt Message

Show:

```text
Ordered: 100
Already received: 90
Attempted: 15
Allowed remaining: 10
```

with policy-valid actions.

---

# 341. Matching Variance UX

Show side-by-side:

```text
PO
Receipt
Supplier Invoice
Variance
Tolerance
Decision needed?
```

---

# 342. Price Variance UX

Example:

```text
PO Unit Price        100
Supplier Invoice     105
Variance               5 (5%)
Tolerance              2%
Status                Approval Required
```

---

# 343. Security — Mass Assignment

Never bind API request directly to entities.

---

# 344. Security — Supplier Scope

Validate Party tenant/entity visibility and Supplier Role.

---

# 345. Security — Purchase Price

Permissions enforce API access.

Hidden UI is insufficient.

---

# 346. Security — Supplier Bank Details

Strong permission and audit.

Purchasing should preferably only display masked/ref data.

---

# 347. Security — Approval

Server-enforced.

---

# 348. Security — Matching Override

Requires explicit permission + reason + audit.

---

# 349. Security — Receipt

Purchasing user cannot fake Inventory receipt without Inventory authorization.

---

# 350. Permissions

Initial vocabulary:

```text
purchasing.po.read
purchasing.po.create
purchasing.po.edit
purchasing.po.approve
purchasing.po.reject
purchasing.po.amend
purchasing.po.cancel
purchasing.po.close

purchasing.price.read
purchasing.price.override

purchasing.request.read
purchasing.request.create
purchasing.request.approve

purchasing.rfq.read
purchasing.rfq.create
purchasing.rfq.compare
purchasing.rfq.select

purchasing.receipt.request
purchasing.receipt.view

purchasing.invoice.create_from_po
purchasing.invoice.view_link

purchasing.match.read
purchasing.match.override

purchasing.return.create
purchasing.return.approve
purchasing.return.execute

purchasing.supplier_profile.manage
purchasing.team.manage
purchasing.settings.manage

purchasing.history.read
purchasing.audit.read
```

---

# 351. Cost/Price Permission

Warehouse receipt users need not see purchase cost.

---

# 352. Approval SoD

Can require different approver for high-value PO.

---

# 353. Matching SoD

AP can review match while Buyer owns commercial PO.

Configurable.

---

# 354. Return SoD

High-value return can require manager approval.

---

# 355. Audit Must Capture

```text
PO approval/rejection
price override
supplier change
quantity increase
amendment
cancel remaining
close short
matching override
return approval
sensitive supplier profile change
```

---

# 356. Audit Does Not Replace Business History

Both retained.

---

# 357. Integration Events

Potential:

```text
PurchaseOrderApprovedV1
PurchaseOrderAmendedV1
PurchaseOrderCancelledV1
PurchaseOrderClosedV1

PurchaseReceiptRequestedV1
PurchaseReceiptLinkedV1

PurchaseInvoiceRequestedV1
PurchaseInvoiceLinkedV1

PurchaseMatchVarianceDetectedV1
PurchaseMatchApprovedV1

SupplierReturnRequestedV1
SupplierReturnCompletedV1
```

---

# 358. Draft Events

Do not publish every edit globally.

---

# 359. Outbox

Post-commit events through Transactional Outbox.

---

# 360. RabbitMQ Is Not Purchasing Invariant

PO approval/source conversion uses local DB/contract transactions.

---

# 361. Approve PO Transaction

Typical:

```text
load current version
validate supplier/profile
validate pricing/approval policy
set approved state
history
audit
outbox
commit
```

---

# 362. PO → Invoice Transaction

Where supported:

```text
validate source
validate idempotency
create Invoicing request/draft/finalize
create typed link
update PO projection
audit
outbox
commit
```

---

# 363. Receipt Transaction

Inventory controls:

```text
movement
balance
valuation
Ledger fact if applicable
audit
outbox
```

Purchasing source projection/link can join Transaction Coordinator if immediate consistency required.

---

# 364. Receive-And-Bill Atomicity

If UX promises one all-or-nothing action:

```text
Inventory Receipt
Supplier Invoice
Purchasing links/projections
Subledger/Ledger effects
Audit/Outbox
```

must commit consistently.

External APIs never participate.

---

# 365. Supplier Invoice Compliance/External Calls

Remain outside DB transaction.

---

# 366. Supplier Email/Communication

Outside DB transaction.

---

# 367. Payment Provider/Bank Calls

Never inside Purchase transaction.

---

# 368. Projection Rebuild

Receipt/invoice state projections must be reconstructable from typed links/source modules.

---

# 369. Reconciliation

Admin/support can verify:

```text
PO linked receipts
PO linked invoices
matching results
open quantities
```

without changing source truth.

---

# 370. Performance Principle

Purchasing document screens compose several domains.

Must use bounded batch calls.

---

# 371. PO Editor Hot Path

Typical:

```text
Supplier context once
Catalog batch/search
Pricing batch
Tax preview batch
Inventory/warehouse references once
Financial defaults once
```

---

# 372. No N+1

100 lines must not trigger 100 independent:

```text
Pricing
Catalog
Tax
Supplier profile
```

calls.

---

# 373. Draft Save Target

Initial measured target:

```text
p95 <= 150–250ms
```

for normal 1–20 line PO save, excluding external communications.

---

# 374. Preview Target

Initial measured target:

```text
p95 <= 200–350ms
```

for normal price/tax preview.

Benchmark, do not promise blindly.

---

# 375. PO List Target

Current page/filter:

```text
p95 <= 100–200ms
```

with realistic indexes and dataset.

---

# 376. Conversion Performance

PO→Invoice should resolve contexts in bounded batches.

---

# 377. Large PO

500+ lines supported.

Use virtualized UI and server-side validation.

---

# 378. Large Receipt

Inventory handles batch receipt efficiently.

Purchasing should not loop one line at a time across module boundary.

---

# 379. Large Matching

Set-based comparison.

Do not issue one DB query per PO/receipt/invoice line.

---

# 380. Read Models

Use projections for:

```text
PO list
Open PO lines
Expected receipts
Supplier purchase summary
Matching exception queue
```

---

# 381. Read Model Is Not Source Truth

Rebuildable.

---

# 382. Indexes

Likely:

```text
tenant/entity/order_date desc
tenant/entity/supplier/order_date desc
tenant/entity/state/order_date
tenant/entity/buyer/order_date
tenant/entity/number
tenant/entity/expected_delivery_date
```

---

# 383. Open PO Line Index

Partial/open-state index for outstanding quantities.

---

# 384. Matching Exception Index

Only active exceptions.

---

# 385. External Source Index

For imports/integrations:

```text
tenant
source_system
external_id
```

unique as configured.

---

# 386. Keyset Pagination

PO/history lists use cursor/keyset for deep pages.

---

# 387. PostgreSQL First

No OpenSearch Day 1.

---

# 388. No Redis Day 1

Reference/config cache only if measured.

Current PO/source truth remains PostgreSQL.

---

# 389. Cacheable Data

Examples:

```text
purchase settings
supplier profile stable metadata
approval policy metadata
```

---

# 390. No Loose Cache of Matching/Receipt Truth

Use source contracts/read projections.

---

# 391. Observability

Metrics:

```text
purchasing_po_save_duration
purchasing_po_approve_duration
purchasing_po_convert_duration
purchasing_receipt_request_duration
purchasing_match_duration
purchasing_match_variance_total
purchasing_over_receipt_attempt_total
purchasing_po_conversion_conflict_total
purchasing_external_import_duplicate_total
```

---

# 392. Tracing

PO→Invoice trace:

```text
Load PO
Validate state/version
Resolve supplier context
Resolve source quantities
Optional match preview
Create Invoicing request
Create source link
Update projection
Outbox
Commit
```

---

# 393. Logging

No full supplier documents/prices/bank data dumps.

Use safe IDs/correlation.

---

# 394. Alerts

Potential:

```text
stuck PO conversion
overdue receipts
high matching exception rate
outbox backlog
supplier invoice duplicate attempts
projection mismatch
```

---

# 395. Import

Purchasing imports can include:

```text
Open POs
Supplier price agreements via Pricing import
Legacy purchase history reference
RFQ response bulk input
```

---

# 396. Open PO Migration

Must reconcile:

```text
Ordered
Received
Invoiced
Cancelled
Open
```

against Inventory/Invoicing migration.

---

# 397. Migration Idempotency

Legacy source key unique.

---

# 398. Do Not Recreate Historical Supplier Bills as POs

Historical bills belong Invoicing/AP migration.

---

# 399. Purchasing Go-Live

Per Legal Entity:

```text
PurchasingGoLiveDate
InitialDocumentStrategy
HistoricalBoundary
```

---

# 400. Export

Permissioned:

```text
POs
Open quantities
Expected receipts
Matching variances
Returns
Supplier purchasing history
```

Large exports async.



# 401. Test Strategy — Default Simple Flow

```text
Create PO
Edit
Approve
Convert to Supplier Invoice
```

must work without:

```text
Purchase Request
RFQ
Goods Receipt
Matching
Supplier Portal
```

---

# 402. Test — Service Company

Inventory inactive.

PO→Supplier Invoice works correctly.

---

# 403. Test — Supplier Quick Create

Purchasing creates/links Directory Party with Supplier Role and continues PO.

No duplicate supplier identity.

---

# 404. Test — Existing Customer Becomes Supplier

Add Supplier Role to same Party.

No duplicate Party.

---

# 405. Test — Draft PO

Editable.

No stock/payable/accounting effect.

---

# 406. Test — Approve PO

Commercial state changes exactly once.

No stock/payable effect.

---

# 407. Test — Double Approve

Idempotent.

---

# 408. Test — Edit vs Approve Race

Stale version rejected.

---

# 409. Test — Approved PO Amendment

Material change creates controlled amendment/reapproval when required.

---

# 410. Test — Reduce Below Received

Blocked.

---

# 411. Test — Reduce Below Invoiced

Blocked/corrective flow required.

---

# 412. Test — Cancel Remaining

Only open commitment cancelled.

Receipt/invoice history retained.

---

# 413. Test — Price List Change After Approval

Approved PO price remains historical agreement.

---

# 414. Test — Buy Pricing

`Direction=Buy` resolves supplier price correctly.

---

# 415. Test — Manual Price Override

Permission/reason/approval evidence enforced.

---

# 416. Test — Tax Preview

Tax Engine only.

No Purchasing tax formula.

---

# 417. Test — PO to Supplier Invoice

Exactly one source-linked invoice.

---

# 418. Test — Double Convert

No duplicate supplier invoice.

---

# 419. Test — Direct Supplier Invoice

Invoicing can work without PO.

Purchasing does not block it.

---

# 420. Test — Supplier External Invoice Duplicate

Invoicing duplicate rule triggers.

---

# 421. Test — PO Conversion Failure

PO not falsely marked invoiced/converted.

---

# 422. Test — PO With Stock, No Receipt

Supplier Invoice can be created if policy permits; On Hand unchanged.

---

# 423. Test — Receive Now

Inventory Receipt increases stock exactly once.

---

# 424. Test — Receive And Bill

Both source links correct; no duplicate receipt/invoice.

---

# 425. Test — Partial Receipt

```text
Ordered 100
Received 60
Remaining 40
```

exact.

---

# 426. Test — Multiple Receipts

60+20+20 = 100.

---

# 427. Test — Over-Receipt Block

Configured Disallow policy enforced.

---

# 428. Test — Over-Receipt Tolerance

Within tolerance allowed; outside requires configured action.

---

# 429. Test — Close Short

Remaining quantity closes with reason/history.

---

# 430. Test — Receipt Before Invoice

Inventory truth and GRNI/accounting seam valid.

---

# 431. Test — Invoice Before Receipt

Allowed/blocked according to policy.

No false stock receipt.

---

# 432. Test — Inventory Disabled

No Warehouse/Receipt setup required.

---

# 433. Test — Mixed Goods + Services

Inventory Receipt affects only stock-tracked goods.

---

# 434. Test — Lot/Serial Receipt

Inventory requirements respected.

Purchasing does not bypass them.

---

# 435. Test — 2-Way Match

PO vs Supplier Invoice quantity/price variance exact.

---

# 436. Test — 3-Way Match

PO vs Receipt vs Invoice comparison exact.

---

# 437. Test — Match Disabled

No matching workflow blocker.

---

# 438. Test — Match Within Tolerance

Passes with recorded policy/version.

---

# 439. Test — Match Variance

Blocks/warns/requires approval exactly as configured.

---

# 440. Test — Match Stale Invoice Draft

Recompute required.

---

# 441. Test — Supplier Return

Original source remains; physical return/credit links correct.

---

# 442. Test — Return More Than Received

Blocked.

---

# 443. Test — Service Credit

No Inventory movement required.

---

# 444. Test — Physical Return Without Credit Yet

Inventory state changes; AP remains until credit/correction.

---

# 445. Test — Landed Cost Late

Inventory valuation adjustment, not movement rewrite.

---

# 446. Test — Payment Shortcut

Creates Payments operation only through Payments module.

Purchasing never sets paid.

---

# 447. Test — Supplier Balance Display

Matches Subledger.

---

# 448. Test — RLS Cross Tenant

Tenant A cannot read/mutate Tenant B Purchasing data.

---

# 449. Test — Wrong Legal Entity

Blocked.

---

# 450. Test — Branch Scope

Backend authorization enforced.

---

# 451. Test — Purchase Price Permission

Receiver without price permission cannot retrieve it through Purchasing API.

---

# 452. Test — Approval Permission

Hidden button does not bypass server rule.

---

# 453. Test — Matching Override Permission

Reason + permission + audit required.

---

# 454. Test — Supplier Bank Data

Unauthorized user cannot access sensitive details.

---

# 455. Test — 500-Line PO

No N+1.

---

# 456. Test — Million PO Dataset

List/search stays indexed/paged.

---

# 457. Test — Large Supplier History

Supplier workspace lazy-loads.

---

# 458. Test — Broker Down

PO approval/conversion commits with Outbox when required DB invariants succeed.

---

# 459. Test — Email Provider Down

PO remains valid; communication retries.

---

# 460. Test — Inventory Failure During Receive-And-Bill

Composite operation does not report false complete success.

---

# 461. Test — Invoicing Failure During Receive-And-Bill

Atomic policy respected; no orphaned Purchasing projection.

---

# 462. Test — Projection Rebuild

Receipt/invoice/match projections reconstruct from typed links/source modules.

---

# 463. Property Test — Purchase Quantity

For each PO line:

```text
CancelledQty >= 0
ReceivedQty >= 0
InvoicedQty >= 0
```

and:

```text
CancelledQty + ClosedShortQty <= OrderedQty
```

---

# 464. Property Test — Receipt Limit

Unless approved over-receipt:

```text
ReceivedQty
<=
OrderedQty - CancelledQty + AllowedTolerance
```

---

# 465. Property Test — Invoice Limit

Invoice source quantity cannot exceed configured invoiceable quantity without explicit exception.

---

# 466. Property Test — Source Idempotency

Same source/purpose/idempotency key → same downstream target.

---

# 467. Property Test — Approved PO Stability

Master/profile/price changes do not mutate approved commercial snapshot.

---

# 468. Architecture Test — No Inventory DbContext

Purchasing cannot reference Inventory implementation entities/DbContext.

---

# 469. Architecture Test — No Invoicing DbContext

Purchasing uses Invoicing Contracts.

---

# 470. Architecture Test — No Subledger Balance Calculation

No local AP computation from bills/payments.

---

# 471. Architecture Test — No Tax Formula

Tax Engine only.

---

# 472. Architecture Test — No Buy Price Rule Duplication

Pricing only.

---

# 473. Architecture Test — No Payment Mutation

Payments only.

---

# 474. Architecture Test — No Journal Entry Creation

Ledger only.

---

# 475. Red-Team — Mandatory Purchase Request

Reject.

---

# 476. Red-Team — Mandatory RFQ

Reject.

---

# 477. Red-Team — Mandatory Goods Receipt

Reject for every company/document.

---

# 478. Red-Team — Mandatory 3-Way Matching

Reject.

---

# 479. Red-Team — Mandatory Supplier Portal

Reject.

---

# 480. Red-Team — PO Increases Stock

Reject.

---

# 481. Red-Team — Supplier Invoice Increases Stock Directly

Reject.

---

# 482. Red-Team — Purchasing Stores Supplier Balance

Reject.

---

# 483. Red-Team — Purchasing Creates Supplier Invoice Rows Directly

Reject.

---

# 484. Red-Team — Purchasing Marks Bill Paid

Reject.

---

# 485. Red-Team — Approved PO Edited Silently

Reject.

---

# 486. Red-Team — Receipt Quantity Written Into PO as Source Truth

Reject.

Use Inventory truth + projection.

---

# 487. Red-Team — Match Edits Supplier Invoice Automatically

Reject.

---

# 488. Red-Team — Supplier Return Deletes Original Receipt

Reject.

---

# 489. Red-Team — Landed Cost Rewrites Original Stock Movement

Reject.

---

# 490. Red-Team — Lowest RFQ Price Auto-Wins

Reject.

Business decision may consider other terms.

---

# 491. Red-Team — Supplier Bank Change by Buyer Without Control

Reject.

---

# 492. Red-Team — 100 PO Lines = 100 Pricing Calls

Reject.

---

# 493. Red-Team — One Giant Purchase Status

Reject.

Commercial/receipt/invoice/matching states separate.

---

# 494. Red-Team — Advanced Procurement Menus for Every Tenant

Reject.

---

# 495. Product Acceptance — Small Company

User sees:

```text
Supplier
Product
Qty
Price
Expected Date

[Approve]
[Create Supplier Invoice]
```

---

# 496. Product Acceptance — Small Stock Company

User can:

```text
PO
→ Approve
→ Receive & Create Supplier Invoice
```

with one simple action.

---

# 497. Product Acceptance — Service Company

No warehouse complexity.

---

# 498. Product Acceptance — Distributor

Can enable:

```text
Requests
Approvals
Receipts
Partial receipts
3-way matching
Returns
Landed cost
```

without replacing Purchasing Core.

---

# 499. Product Acceptance — Supplier Does Not Log In

Core Purchasing fully works.

---

# 500. Product Acceptance — Buyer

Can manage commercial purchase without accounting internals.

---

# 501. Product Acceptance — Warehouse Receiver

Can receive without seeing sensitive purchase price if restricted.

---

# 502. Product Acceptance — AP

Can inspect source PO/receipt/match from Supplier Invoice.

---

# 503. Product Acceptance — History

From PO, user can navigate:

```text
Request/RFQ source
Receipts
Supplier Invoices
Returns
Matching decisions
```

---

# 504. Product Acceptance — Speed

Normal purchase completes in minimal screens/actions.

---

# 505. Product Acceptance — Correctness

Simple UX does not weaken Inventory/Invoicing/AP integrity.

---

# 506. Implementation Order

```text
PU00 ADRs + Ownership + Simple-First Policy
PU01 Schema + RLS
PU02 Supplier Purchase Profile
PU03 Purchase Order Draft
PU04 Catalog/UOM Integration
PU05 Buy Pricing Integration
PU06 Tax Preview
PU07 PO Approval/Confirmation
PU08 PO Amendments + Cancellation
PU09 PO → Supplier Invoice
PU10 One-Click Approve & Bill
PU11 History + Typed Links
PU12 Inventory Receipt Integration
PU13 Receive & Bill Composite
PU14 Partial Receipt + Over/Under Policy
PU15 Invoice Eligibility / Partial Invoice
PU16 2-Way Matching
PU17 3-Way Matching
PU18 Matching Exceptions
PU19 Supplier Returns
PU20 Landed Cost Integration Seam
PU21 Optional Purchase Request
PU22 Optional RFQ + Comparison
PU23 Buyer/Team/Channel
PU24 Supplier Workspace
PU25 Read Models + Reports
PU26 Permissions + Audit + SoD
PU27 Performance + Concurrency
PU28 Import/Migration
PU29 Projection/Reconciliation Runbooks
PU30 End-to-End Acceptance
PU31 Security / Red-Team
PU32 Release Gate
```

---

# 507. PU00 Exit Gate

Locked:

```text
PO→Supplier Invoice is default
Goods Receipt optional
Matching optional
Request/RFQ optional
Supplier Portal optional
Physical receipt Inventory-owned
Supplier Invoice Invoicing-owned
AP Subledger-owned
```

---

# 508. PU01 Exit Gate

```text
business_purchasing
FORCE RLS
UUIDv7
tenant/entity-safe FKs
row versions
```

---

# 509. PU02 Exit Gate

Supplier profile contains no duplicate identity/AP/payment truth.

---

# 510. PU03 Exit Gate

User can create/edit PO with minimum fields.

---

# 511. PU04 Exit Gate

Product/UOM refs correct.

---

# 512. PU05 Exit Gate

Buy-side Pricing batch + override evidence correct.

---

# 513. PU06 Exit Gate

Tax preview only through Tax Engine.

---

# 514. PU07 Exit Gate

Approval can be disabled; simple approve remains fast.

---

# 515. PU08 Exit Gate

Post-approval change/cancel preserves history/downstream facts.

---

# 516. PU09 Exit Gate

PO creates exactly one intended Supplier Invoice target per source slice/purpose.

---

# 517. PU10 Exit Gate

One-click:

```text
Approve & Create Supplier Invoice
```

idempotent.

---

# 518. PU11 Exit Gate

Typed history/references answer complete PO story.

---

# 519. PU12 Exit Gate

Purchasing requests Inventory Receipt; no direct stock write.

---

# 520. PU13 Exit Gate

```text
Receive & Bill
```

works correctly when enabled.

---

# 521. PU14 Exit Gate

Partial/over/under receipt policies exact.

---

# 522. PU15 Exit Gate

Partial Supplier Invoices cannot duplicate/exceed allowed source quantity.

---

# 523. PU16 Exit Gate

2-way matching compares PO/Invoice exactly.

---

# 524. PU17 Exit Gate

3-way matching compares PO/Receipt/Invoice exactly.

---

# 525. PU18 Exit Gate

Tolerance/exception approval audited and optional.

---

# 526. PU19 Exit Gate

Supplier return coordinates Inventory/Credit/Payment without rewriting original history.

---

# 527. PU20 Exit Gate

Landed-cost source links reach Inventory valuation engine without duplicate valuation logic.

---

# 528. PU21 Exit Gate

Purchase Request can be enabled without changing PO Core.

---

# 529. PU22 Exit Gate

RFQ/quote comparison optional and converts safely to PO.

---

# 530. PU23 Exit Gate

Buyer/team/channel optional and reportable.

---

# 531. PU24 Exit Gate

Supplier workspace composes cross-module summaries lazily.

---

# 532. PU25 Exit Gate

Lists/reports indexed/paginated/no shadow finance truth.

---

# 533. PU26 Exit Gate

Backend permissions/SoD/audit tested.

---

# 534. PU27 Exit Gate

Benchmarks include:

```text
100/500-line PO
concurrent edit/approve
double conversion
large supplier history
partial receipts
matching large documents
million-document list
```

---

# 535. PU28 Exit Gate

Open PO migration idempotent/reconciled.

---

# 536. PU29 Exit Gate

Projection/source-link repair does not create duplicate Invoice/Receipt.

---

# 537. PU30 E2E — Simplest Purchase

```text
Create PO
→ Approve
→ Create Supplier Invoice
```

production-correct.

---

# 538. PU30 E2E — Simple Stock Purchase

```text
PO
→ Approve
→ Receive & Bill
```

production-correct.

---

# 539. PU30 E2E — Advanced Purchase

```text
Request
→ RFQ
→ PO
→ Partial Receipt
→ 3-Way Match
→ Supplier Invoice
→ Return
```

production-correct.

---

# 540. PU31 Exit Gate

Architecture/red-team tests block cross-domain shortcuts.

---

# 541. PU32 Release Gate

Signoff:

```text
Product Architect
Purchasing Domain Lead
Inventory Lead
Finance/AP Lead
Backend Lead
Frontend Lead
Performance Engineer
QA/Security
```

---

# 542. Build Now — MUST

```text
Supplier Purchase Profile
Purchase Order
PO Lines
Draft edit/autosave
Buy Pricing integration
Tax preview
Approval/confirm (optional/simple)
Amend/cancel remaining
PO→Supplier Invoice
One-click Approve & Bill
Typed history
Inventory Receipt integration
Partial receipt
Over/under receipt policy
Partial invoicing
2-way matching foundation
3-way matching capability
Matching tolerance/exception
Supplier returns
Landed cost integration seam
RLS/security/audit
Read models/reports
Performance/concurrency tests
```

---

# 543. Build Later / Per Customer Need

```text
Advanced Purchase Requests
Complex RFQ tendering
Supplier Portal
E-sourcing
Auction/reverse auction
Advanced vendor scorecards
Contract lifecycle management
Full import shipment/logistics
Complex blanket purchase agreements
Automated replenishment procurement
Advanced Budget integration
Complex approval matrix designer
Supplier onboarding portal
AI autonomous procurement
Advanced spend analytics
```

---

# 544. Configuration Matrix

Company can choose independently:

```text
Purchase Order                  ON
Direct PO → Supplier Invoice    ON/OFF
Purchase Request                OFF/ON
RFQ                             OFF/ON
Approval                        OFF/Simple/RiskBased/Always
Goods Receipt                   OFF/ON
Receive & Bill                  OFF/ON
Partial Receipt                 OFF/ON
Over Receipt                    Disallow/Tolerance/Approval/Allow
2-Way Match                     OFF/ON
3-Way Match                     OFF/ON
Match Tolerances                Configurable
Supplier Return                 Simple/Advanced
Landed Cost                     OFF/ON
Supplier Portal                 OFF/ON later
```

---

# 545. Recommended Fresh Tenant Defaults

```text
Purchase Order                  ON
PO → Supplier Invoice           ON
Purchase Request                OFF
RFQ                             OFF
Approval                        Simple / owner confirm
Goods Receipt required          OFF
3-Way Matching                  OFF
Supplier Portal                 OFF
Advanced Vendor Rating          OFF
```

Inventory-active tenants can turn Receipt on without requiring advanced matching.

---

# 546. Default UX Law

> **If a company can buy correctly with PO → Supplier Invoice, TODIR must not force Request → RFQ → Receipt → Matching screens.**

---

# 547. Integrity Law

> **Short purchasing workflows must still create the correct supplier bill, stock movement, payable and accounting facts through their owning domains.**

---

# 548. Domain Separation Must Not Become UX Fragmentation

The user may see one action:

```text
[Receive & Bill]
```

while TODIR executes several correct domain commands internally.

---

# 549. Final Simple Flow

```text
Supplier
   ↓
Purchase Order
   ↓
Edit / Approve
   ↓
Supplier Invoice
```

---

# 550. Final Simple Stock Flow

```text
Purchase Order
   ↓
Approve
   ↓
Receive & Bill
   ├── Inventory Receipt
   └── Supplier Invoice
```

---

# 551. Final Advanced Flow

```text
Purchase Request
   ↓
RFQ / Compare
   ↓
Purchase Order
   ↓
Approval
   ↓
Partial/Full Goods Receipt
   ↓
3-Way Match
   ↓
Supplier Invoice
   ↓
Subledger Payable
   ↓
Payment
```

---

# 552. Final Return Flow

```text
Original Purchase
   ↓
Supplier Return Request
   ├── Inventory Return Out
   ├── Supplier Credit / Vendor Credit
   └── Supplier Refund/Offset
```

Each domain owns its truth.

---

# 553. Final Matching Flow

```text
PO Commercial Truth
        │
        ├──────────┐
        ▼          ▼
Receipt Truth   Supplier Invoice Truth
 Inventory          Invoicing
        \          /
         \        /
          Matching
             ↓
     Pass / Variance / Exception
```

---

# 554. Final Product Verdict

For most TODIR customers Purchasing should feel like:

```text
Create Purchase Order
Edit it
Approve it
Create Supplier Invoice
```

Everything else is optional capability.

---

# 555. Final Engineering Verdict

Purchasing is approved only when:

```text
PO→Supplier Invoice works without Goods Receipt.
Goods Receipt is optional and Inventory-owned.
Supplier login/Portal is not required.
Purchase Request/RFQ are optional.
Approval complexity is optional.
Approved PO history cannot be silently rewritten.
Buy price rules remain Pricing-owned.
Tax remains Tax-owned.
Supplier Invoice remains Invoicing-owned.
AP/payable balance remains Subledger-owned.
Stock/receipt remains Inventory-owned.
Payments remain Payments-owned.
Ledger remains accounting authority.
Retries/double-clicks cannot duplicate Invoice or Receipt.
Partial receipt/invoicing is exact.
2-way/3-way matching is optional and explainable.
Supplier returns preserve original history.
Landed cost does not duplicate Inventory valuation logic.
Small-company UX hides enterprise procurement complexity.
Large companies can enable controls without replacing Purchasing Core.
```

---

# 556. Final File Boundary

This file is the complete design baseline for the Purchasing business module.

Do **not** create separate architecture/design packs for:

```text
Purchase Request
RFQ
Goods Receipt
Matching
Supplier Return
Landed Cost
```

unless a future major architecture decision explicitly turns one into a cross-module shared platform/kernel.

---

# 557. Next Planning Rule

After Purchasing, continue with **one file per actual business module**.

Do not split subfeatures into extra documents.

Reuse the already-approved shared foundations.

---

# 558. Candidate Next Business Module

Recommended next high-value file:

> **`16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

It should describe the user-facing Accounting module over the already-designed Ledger/Subledger kernels, without rewriting `09` and `10`.

The file should focus on:

```text
Chart of Accounts management UX
Journals/manual journals
Accounting periods/close
Trial Balance
General Ledger views
Financial Statements
Reconciliation workbench
Inventory/AP/AR control reconciliation
Managed → Full Accounting UX
Adjusting entries
Audit
Reports
Permissions
Performance
Implementation plan
```

while reusing existing Ledger/Subledger architecture.

---

**END OF FILE — 15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
