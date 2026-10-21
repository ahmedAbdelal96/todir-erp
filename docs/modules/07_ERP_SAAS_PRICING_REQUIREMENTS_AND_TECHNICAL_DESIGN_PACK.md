# 07 — ERP SaaS Pricing Requirements & Technical Design Pack

**File:** `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Status:** PRODUCT + COMMERCIAL INTEGRITY + DOMAIN + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-05  
**Audience:** Product Owner, Senior Product Designer, Product Architect, Sales/Purchasing Leads, Backend/Frontend Leads, Performance Engineer, QA, Implementation Agent  
**Risk Class:** HIGH — affects revenue, margin, customer trust, sales speed, purchasing cost, quotations, orders, invoices, future POS and e-commerce

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Product Decision

Pricing answers:

> **What is the correct commercial unit price for this transaction, for this party, product, quantity, UOM, currency, date, entity and commercial context — and why?**

Locked principles:

> **Pricing is the only owner of reusable price definitions and price-resolution rules.**

> **Transactions snapshot the price actually applied/agreed.**

> **Catalog never becomes a second source of truth for price.**

> **The user sees a simple number; the backend resolves complexity deterministically.**

---

# 1. Why Pricing Is a Foundation Domain

The same pricing truth may be consumed by:

- Sales quotations.
- Sales orders.
- Invoicing.
- POS.
- E-commerce.
- CRM quoting.
- Purchasing.
- Supplier agreements.
- Subscriptions later.
- Projects later.
- Integrations/APIs.

If every module implements its own lookup, the ERP produces inconsistent commercial answers.

---

# 2. Ownership

Pricing owns:

```text
PriceList
PriceListVersion
PriceEntry
PriceRule
PriceScope
PriceCondition
PriceResolution
PricingPolicy
PriceGroup
```

Pricing does NOT own:

```text
Product Identity        → Catalog
Party Identity          → Directory
Currency / FX           → FinBase
Tax Calculation         → Tax
Inventory Cost          → Inventory
Sales Order             → Sales
Purchase Order          → Purchasing
Approval Workflow       → Approvals
Promotional Rewards     → Promotions/Loyalty later
```

---

# 3. Single Source of Truth

Forbidden:

```text
product.sale_price
sales.default_price
invoice.item_price_master
pos.default_price_master
customer.special_price_column
ecommerce.product_price_master
```

as independent reusable truths.

Correct:

```text
Pricing → reusable master/rules

Transaction
→ immutable price snapshot
```

---

# 4. Simple Product "Selling Price"

The Product Workspace may show:

```text
Selling Price
1,250.00 EGP
```

But it is only a UI editor/projection over:

```text
Default Selling Price List
```

No `product.sale_price` canonical column.

---

# 5. Small Company UX

A small company should be able to work as if products simply have prices:

```text
Product A → 100 EGP
Product B → 250 EGP
```

without learning Price Lists.

The system silently maintains the default list.

---

# 6. Advanced Company UX

Advanced tenant can enable:

```text
Retail
Wholesale
Distributor
Government
Export USD
Customer Contracts
Supplier Prices
Quantity Tiers
```

Same engine, richer interface.

---

# 7. Business Scenario — Retail vs Wholesale

```text
Product A

Retail       = 100
Wholesale    = 90
Distributor  = 82
```

---

# 8. Business Scenario — Customer Contract

```text
Wholesale default = 100
Customer ABC = 92
```

ABC gets 92; others remain 100.

---

# 9. Business Scenario — Quantity Tiers

```text
Qty 1–9   = 100
Qty 10–49 = 95
Qty 50+   = 87
```

Qty 60 → 87.

---

# 10. Business Scenario — UOM Pricing

Catalog:

```text
Piece = 1
Pack = 6 Pieces
Carton = 24 Pieces
```

Commercial pricing:

```text
Piece  = 10 EGP
Pack   = 57 EGP
Carton = 210 EGP
```

Commercial price does not have to equal UOM factor × piece price.

---

# 11. Business Scenario — Validity

```text
Normal price = 1,000
December campaign price = 850
Valid 2026-12-01 → 2026-12-31
```

---

# 12. Business Scenario — Currency

```text
Egypt List = EGP
Export List = USD
UAE List = AED
```

Currency truth comes from FinBase.

---

# 13. Business Scenario — Manual Negotiation

Resolved:

```text
90 EGP
```

Salesperson negotiates:

```text
88 EGP
```

If authorized, transaction stores:

```text
Resolved = 90
Final = 88
Override = true
Reason
Actor
```

The Price List itself is unchanged.

---

# 14. Business Scenario — Buying

Supplier A:

```text
Variant X
Carton
1–99 = 10 USD
100+ = 9 USD
```

Pricing supports Selling and Buying directions.

---

# 15. Competitor Evidence

Current Odoo supports pricelist rules by product/category, quantity, dates, currency and customer context; it resolves overlaps using specificity and other tie-breakers.

Business Central supports price agreements by customer/customer groups, item, UOM, quantity, validity and currency.

ERPNext supports Selling/Buying Price Lists, UOM-specific item prices, customer/supplier-specific rates and conditional Pricing Rules.

Daftra supports wholesale/retail lists, customer-specific list assignment and quantity pricing that automatically updates invoices/POS.

These validate the business requirements. We do not copy their architecture blindly.

---

# 16. Important Divergence — No Accidental Tie-Break

We explicitly reject a last-resort rule such as:

```text
newest row wins
database row wins
highest ID wins
```

If two equally specific active rules conflict, activation must fail or runtime must surface a configuration conflict.

---

# 17. Pricing Direction

```text
Selling
Buying
```

Direction is explicit.

---

# 18. Pricing Context

Concept:

```text
PricingContext
{
  TenantId
  LegalEntityId

  Direction

  PartyId?
  PriceGroupId?

  ProductVariantId
  ProductTemplateId
  ProductCategoryId?
  BrandId?

  Quantity
  UomId

  TransactionCurrency
  PricingDate

  Channel?
  RequestedPriceListId?
}
```

---

# 19. Explicit Context Rule

Resolver does not read hidden static/global "current customer/entity/date".

Everything needed is explicit.

---

# 20. Price Resolution

Concept:

```text
PriceResolution
{
  FinalUnitPrice
  Currency
  UomId

  BaseUnitPrice?
  DiscountPercent?
  DiscountAmount?

  PriceListId
  PriceListVersionId
  RuleId?
  RuleVersion?

  ResolutionReason
  Explanation

  CalculationVersion
}
```

---

# 21. Price Snapshot

Consuming transaction stores:

```text
PriceSnapshot
{
  FinalUnitPrice
  Currency
  UomId

  PriceListId?
  PriceListVersion?
  RuleId?
  RuleVersion?

  BaseUnitPrice?
  DiscountPercent?
  DiscountAmount?

  ManualOverride
  OverridePrice?
  OverrideReason?
  OverrideActor?

  PricingDate
  CalculationVersion
}
```

Master changes do not rewrite history.

---

# 22. Price List

Stable commercial identity.

Examples:

```text
Standard Selling
Retail
Wholesale
Distributor
Export USD
Standard Buying
```

---

# 23. Price List Fields

Conceptually:

```text
id
tenant_id
code
name

direction
currency_code

entity_scope
status
is_default

created_at
version
```

---

# 24. Price List Currency

A Price List always has explicit currency.

No price amount without Currency.

---

# 25. Entity Scope

Support:

```text
SingleLegalEntity
SelectedLegalEntities
TenantShared
```

TenantShared only when commercial/currency rules allow it.

---

# 26. Default Price Lists

Recommended:

```text
Default Selling Price List per Legal Entity
Default Buying Price List per Legal Entity
```

or per explicit commercial context.

No ambiguous global default across unrelated entities.

---

# 27. Price List Versioning

A Price List identity can have versions:

```text
Draft
Scheduled
Active
Expired
Archived
```

This allows future price updates without editing live history.

---

# 28. Active Versions Should Be Immutable

Preferred rule:

> Once activated, material rule content is immutable.

Change → new version.

Benefits:

- audit;
- rollback;
- historical explanation;
- predictable caching.

---

# 29. Scheduled Price Version

Admin can prepare:

```text
2027 Prices
Effective 2027-01-01
```

without affecting 2026.

---

# 30. Price Entry

Simple explicit price:

```text
Price List = Wholesale
Variant = X
UOM = Carton
Price = 210 EGP
```

---

# 31. Price Rule

Conditional pricing:

```text
Wholesale
Variant X
Min Qty = 100
Fixed Price = 180
```

---

# 32. Rule Result Types — MVP

```text
FixedUnitPrice
PercentageDiscount
FixedDiscountPerUnit
```

Design support for:

```text
PercentageMarkup
FixedMarkupPerUnit
```

Build if Buying/cost-plus requirements need them.

---

# 33. No Arbitrary Formula Language Day 1

Do not allow tenant-supplied:

```text
JavaScript
SQL
C#
arbitrary expression eval
```

in pricing.

Use typed structured rules.

---

# 34. Referenced Price List

Advanced structured derived list may support:

```text
Distributor = Wholesale - 5%
```

If implemented, dependencies form a DAG.

---

# 35. No Price List Cycles

Forbidden:

```text
A → B
B → A
```

Activation validator detects cycles.

---

# 36. Dependency Depth

Price derivation chain should remain short and bounded.

Exact limit decided after performance/product validation.

---

# 37. Product Scope

Rules can apply to:

```text
Specific Variant
Product Template
Product Category
Brand
All Products
```

---

# 38. Party Scope

Rules can apply to:

```text
Specific Party
Price Group
All Parties
```

---

# 39. Other Hard Conditions

```text
Tenant
Legal Entity
Direction
Price List
Currency policy
Date
UOM
Quantity
Status
```

---

# 40. Product Specificity

Recommended strongest → weakest:

```text
Variant
Template
Deepest Category
Parent Category
Brand
All Products
```

Brand/category exact relationship must be locked and documented.

---

# 41. Party Specificity

Recommended:

```text
Specific Party
Price Group
All Parties
```

---

# 42. Variant Beats Template

Specific variant price beats product-template general price.

---

# 43. Deep Category Beats Parent

Specific subcategory rule beats broad parent category rule.

---

# 44. UOM Specificity

Exact UOM price beats derived/no-UOM price.

---

# 45. Quantity Tier

Within otherwise equivalent scope, highest qualifying minimum quantity wins.

---

# 46. Quantity Range

Support:

```text
min_quantity
max_quantity?
```

Boundaries must be explicit and tested.

---

# 47. Quantity Basis

Initial concepts:

```text
LineQuantity
DocumentVariantTotal
```

Default = line quantity unless consuming flow requests document aggregation.

---

# 48. Split-Line Quantity

Some businesses split same Product across lines but expect quantity pricing based on total quantity.

Support as explicit context policy, not an accidental behavior.

---

# 49. Mixed Product Quantity

"Buy any 10 products in category" is promotion/document-level logic.

Not core line Price Resolver MVP.

---

# 50. Cumulative Purchase Rules

"Spend 100k this quarter" depends on transaction history.

Belongs Promotions/Loyalty/Contract Rebate later.

---

# 51. Pricing Date

Every resolver request includes explicit:

```text
PricingDate
```

No `DateTime.Now` hidden inside.

---

# 52. Validity

Rules:

```text
valid_from?
valid_to?
```

Eligibility based on PricingDate.

---

# 53. Future Rules

Future prices can be scheduled safely.

---

# 54. UOM-Specific Price

Price key can include:

```text
Variant + UOM
```

---

# 55. UOM-Derived Price

If policy allows, system may derive alternate UOM price using Catalog conversion.

Example:

```text
Piece = 10
Carton 24 → derived 240
```

---

# 56. Explicit UOM Price Wins

If Carton explicit price = 210:

```text
210
```

wins over derived 240.

---

# 57. UOM Validation

Pricing consumes Catalog:

```text
ValidateVariantUom
```

No duplicate conversion table.

---

# 58. Currency Ownership

Pricing consumes FinBase.

No currency master or FX table here.

---

# 59. Matching Currency

If Price List and transaction currencies match:

no conversion.

---

# 60. Different Transaction Currency

Configurable policy:

```text
Require exact-currency Price List
or
Allow FinBase conversion
```

---

# 61. Recommended Default

Simple SMEs may allow conversion fallback.

Contract/export scenarios should favor explicit currency prices.

---

# 62. Commercial FX

If Pricing converts a price, result preserves the FinBase rate snapshot/purpose used.

Accounting posting rate may be a different explicit FinBase purpose.

---

# 63. Unit Price Precision

Use FinBase high-precision `UnitPrice`.

Do not prematurely round to Currency minor unit.

---

# 64. Final Money Rounding

Consuming document calculation performs final money rounding according to `06`.

Pricing returns high-precision commercial UnitPrice.

---

# 65. Discount Structure

Resolution can preserve:

```text
Base Price
Discount %
Discount Amount
Final Unit Price
```

---

# 66. Fixed Price

Direct final unit price.

---

# 67. Percentage Discount

Requires a known base price.

---

# 68. Fixed Per-Unit Discount

Example:

```text
100 - 5 = 95
```

---

# 69. Zero Price

Can be valid, but must be explicit.

Examples:

```text
sample
warranty service
free item line from another domain
```

Missing price must never silently become zero.

---

# 70. Missing Price

Resolver returns:

```text
PriceMissing
```

not `0`.

---

# 71. Missing Price UX

Sales user sees:

> لا يوجد سعر صالح لهذا المنتج في هذا السياق.

If authorized:

```text
Enter manual price
```

otherwise manager/configuration flow.

---

# 72. Manual Override

Override is transaction-level.

It never updates reusable Pricing master.

---

# 73. Override Data

Store:

```text
resolved price
override price
deviation
reason
actor
time
approval reference?
```

---

# 74. Override Permission

Consuming domain is final authority:

```text
sales.price.override
purchase.price.override
```

Pricing provides deviation/policy data.

---

# 75. Maximum Discount

Commercial policy can define thresholds:

```text
Salesperson <= 5%
Manager <= 15%
Above → approval
```

Exact enforcement belongs Sales + Approvals.

---

# 76. Floor Price

Potential floor:

```text
Explicit Minimum
Cost + Margin
Contractual Minimum
```

---

# 77. Cost Ownership

Inventory owns cost.

Pricing may consume optional:

```text
ICostProvider
```

No hard dependency.

---

# 78. Pricing Without Inventory

Service businesses work normally.

Cost/margin controls are optional capabilities.

---

# 79. Cost-Based Margin Protection

If enabled:

```text
Cost = 70
Minimum margin = 20%
Minimum price = configured result
```

Exact margin formula must be explicit.

Do not mix markup and margin terminology.

---

# 80. Markup vs Margin

They are mathematically different.

Pricing UI/domain must name them correctly.

Example:

```text
Markup = (Price - Cost) / Cost
Margin = (Price - Cost) / Price
```

No ambiguous "profit %" field.

---

# 81. Tax-Inclusive/Exclusive Boundary

Price List can carry commercial tax mode:

```text
TaxExclusive
TaxInclusive
```

Tax module owns actual VAT/tax extraction/calculation.

---

# 82. Pricing Does Not Compute VAT

It provides price basis and mode/context.

---

# 83. Price Group

If grouping exists only for pricing:

```text
PriceGroup
```

can be Pricing-owned.

If it is broad commercial segmentation:

Sales/CRM owns segment and Pricing consumes it.

---

# 84. Party-Specific Price

Rule references Directory `PartyId`.

No separate Customer master.

---

# 85. Supplier-Specific Price

Same Party identity, Buying direction.

---

# 86. Default Price List Assignment

Customer-specific default lives in:

```text
SalesCustomerProfile
```

not Party core.

Supplier default Buying list belongs Purchasing profile.

---

# 87. Requested Price List

Document may explicitly select a Price List.

Resolver validates:

- active;
- correct direction;
- entity scope;
- currency behavior;
- party eligibility.

---

# 88. Automatic Price List Selection

If document does not select:

1. Party/Profile assigned list.
2. Price Group list.
3. Entity default list.

Deterministic.

---

# 89. No UI Order as Pricing Logic

Admin drag/drop visual order cannot silently determine price unless it writes explicit priority.

---

# 90. Explicit Priority

Rules may have explicit priority for advanced cases.

But priority is not allowed to hide invalid same-scope conflicts.

---

# 91. Resolver Hard Filter

First filter candidates by:

```text
Tenant
Entity
Direction
Price List
Active Version
Date
Party eligibility
Product eligibility
UOM
Quantity
Currency policy
```

---

# 92. Resolver Rank

Then rank by explicit deterministic tuple.

Example:

```text
party_specificity
product_specificity
uom_specificity
quantity_specificity
explicit_priority
```

The final tuple order is versioned and contract-tested.

---

# 93. No Magic Score

Avoid one opaque integer score if it makes explanation impossible.

Use named rank components.

---

# 94. Conflict Rule

If two candidates have the same effective rank and overlapping conditions but different results:

```text
PricingConfigurationConflict
```

---

# 95. Activation Conflict Detection

Price List Version activation validates overlaps before becoming live.

---

# 96. Runtime Conflict Defense

If invalid data reaches runtime:

- fail safely;
- log;
- alert;
- return actionable error.

Never pick random candidate.

---

# 97. Soft Overlap

Broad and specific rules can overlap intentionally.

Specific rank resolves them.

---

# 98. Hard Conflict

Two identical scopes/periods/quantity/priorities with different prices block activation.

---

# 99. Stacking Philosophy

MVP:

> one winning base price + explicitly designed adjustment layers.

No arbitrary stack of unlimited rules.

---

# 100. Why Arbitrary Stacking Is Dangerous

```text
-10%
then -5%
then -20 EGP
```

Order changes result.

Hard to explain and test.

---

# 101. Promotions Boundary

Core Pricing does NOT own:

```text
Buy 2 Get 1
Coupon
Free Gift
Loyalty Points
Cashback
Cheapest Item Free
Basket Mix-and-Match
```

---

# 102. Simple Campaign Price

A simple time-limited fixed price/percentage price adjustment can remain Pricing.

---

# 103. Promotion Boundary Test

If question is:

> What is this line's unit price based on line/business context?

Pricing.

If question needs:

> What else is in basket or what customer bought historically?

Promotions/Loyalty.

---

# 104. Quote Price Lock

Pricing supplies snapshots.

Sales owns when quotation price is:

```text
Provisional
Resolved
Locked
Expired
Overridden
```

---

# 105. Repricing

Pricing contract supports comparing:

```text
Previous Snapshot
Current Resolution
Difference
Reason
```

---

# 106. No Silent Repricing

Editing a Price List must not silently mutate existing negotiated documents.

---

# 107. Sales Cycle Consistency

Order→Delivery→Invoice snapshot transfer rules belong Sales/Invoicing.

Pricing only resolves when explicitly called.

---

# 108. Buying Cycle Consistency

PO→Receipt→Vendor Bill preservation belongs Purchasing.

---

# 109. Explainability

Every result has an explanation model.

Example:

```text
List: Wholesale EGP
Rule: Variant X / Qty 50+
Party group: Distributor
UOM: Carton
Price: 87
```

---

# 110. Salesperson Experience

Normally:

```text
Unit Price: 87 EGP
```

Optional small badge:

```text
Bulk price
```

---

# 111. Admin "Why This Price?"

Admin can inspect matching context and winning rule.

---

# 112. Support Resolution Trace

On-demand trace includes:

```text
Selected List
Candidate Rules
Rejected Reasons
Winner
Computation
Warnings
```

Not persisted for every normal request.

---

# 113. Rules Are Data, Not Code

No arbitrary SQL/JS/C# tenant formulas.

---

# 114. Rule Input Registry

Typed approved inputs:

```text
PartyId
PriceGroupId
VariantId
TemplateId
CategoryId
BrandId
Quantity
UomId
PricingDate
Channel
```

---

# 115. Custom Fields

Custom fields are not automatically allowed as pricing predicates.

Critical recurring need must become an approved typed input.

---

# 116. Channel Context

Future-safe:

```text
ERP
POS
Web
Marketplace
Mobile
```

No hard dependency on those modules.

---

# 117. Territory / Warehouse / Batch

Existing ERPs support these contexts, but they are NOT automatically MVP.

Add only after Sales/Inventory market validation.

---

# 118. Price Approval

SME default: simple.

Enterprise can enable:

```text
Draft
Approval
Scheduled
Active
```

via Approval hooks.

---

# 119. Future Effective Price Workflow

Preferred:

```text
Create New Version
→ Edit
→ Validate
→ Preview Impact
→ Approve if required
→ Schedule
→ Activate
```

---

# 120. Emergency Price Change

Authorized admin can publish immediately effective version with audit.

---

# 121. Bulk Price Change

Need a safe bulk workflow.

Examples:

```text
Increase category 5%
Set new fixed prices from Excel
Copy list and adjust
```

Never run unreviewed UPDATE statements.

---

# 122. Bulk Preview

Before activation show:

```text
Products affected
Old price
New price
Delta
Percentage change
Conflicts
Missing values
```

---

# 123. Price Import

Pricing import is a first-class onboarding/operations feature.

Support:

- Price Lists.
- Explicit Product/UOM prices.
- customer/supplier prices.
- quantity tiers.
- validity.
- currencies.

---

# 124. Import Template — Simple

```text
Price List
SKU
UOM
Price
Currency
```

---

# 125. Import Template — Advanced

```text
Price List
Direction
Party/Group
SKU/Variant
UOM
Min Qty
Max Qty
Valid From
Valid To
Rule Type
Value
Priority
```

---

# 126. Import Mapping

Resolve:

- Variant via Catalog SKU/external refs.
- Party via Directory.
- UOM via Catalog.
- Currency via FinBase.

No duplicated masters.

---

# 127. Import Preview

Show:

```text
Create
Update Draft
Conflict
Unknown SKU
Unknown UOM
Unknown Party
Currency mismatch
Overlapping rule
```

---

# 128. Active Price Import

Do not directly mutate active immutable version.

Import into Draft/New Version.

---

# 129. Export

Allow exporting price lists/rules to Excel/CSV.

Large export async.

---

# 130. Price Worksheet

Useful business feature:

a working draft table for mass changes before activation.

Business Central has a price worksheet concept; our design can implement it as Draft Version + bulk editor rather than a separate truth.

---

# 131. Preview Impact

Admin can simulate:

```text
Product X
Customer ABC
Qty 10
Date ...
```

to see resolved price before activation.

---

# 132. Test Cases Attached to Version

Advanced future:

Admin saves sample pricing scenarios expected to resolve to specific values.

Activation runs them.

Useful for enterprise contracts.

---

# 133. Pricing Simulator

Admin UX inputs:

```text
Customer
Product
Qty
UOM
Date
Currency
Entity
```

Output:

```text
Resolved Price
Explanation
Candidates
Warnings
```

---

# 134. Architecture Schema

Use:

```text
foundation_pricing
```

---

# 135. Core Tables

```text
price_list
price_list_version

price_entry
price_rule

price_group
price_list_assignment

pricing_policy

pricing_rule_scope
pricing_rule_condition
```

Keep schema structured, not generic EAV if avoidable.

---

# 136. `price_list`

Concept:

```text
id
tenant_id
code
name
direction
currency_code
entity_scope_type
status
created_at
version
```

---

# 137. `price_list_version`

```text
id
tenant_id
price_list_id
version_no

status
effective_from
effective_to?

created_by
approved_by?
activated_at?
```

---

# 138. `price_entry`

Simple explicit entry:

```text
id
tenant_id
price_list_version_id

variant_id
uom_id

party_id?
price_group_id?

min_qty?
max_qty?

unit_price

valid_from?
valid_to?

priority
status
version
```

---

# 139. `price_rule`

For adjustments/structured conditions:

```text
id
tenant_id
price_list_version_id

scope fields
result_type
result_value

base_source
min_qty
max_qty
valid dates
priority

status
version
```

---

# 140. Schema Simplicity Rule

Do not prematurely create one generic `condition(key,op,value_json)` engine for all cases.

Critical hot-path fields deserve typed columns/indexes.

---

# 141. Typed Columns for Hot Dimensions

Likely:

```text
variant_id
template_id
category_id
party_id
price_group_id
uom_id
min_qty
max_qty
valid_from
valid_to
priority
```

---

# 142. Price Group Table

If Pricing-owned:

```text
id
tenant_id
code
name
status
```

Assignments may be Sales-profile owned rather than generic Directory.

---

# 143. RLS

All tenant-owned pricing tables:

```text
ENABLE RLS
FORCE RLS
```

Runtime app role non-owner.

---

# 144. Entity Isolation

Price resolution always includes LegalEntity.

No price leakage between group companies.

---

# 145. Catalog References

Stable IDs:

```text
ProductVariantId
TemplateId
CategoryId
UomId
```

No Catalog DbContext navigation.

---

# 146. Directory References

Stable:

```text
PartyId
```

and/or PriceGroup context.

No Directory table joins through EF navigation.

---

# 147. FinBase References

Currency code and conversion contracts.

No FinBase DB direct access outside contracts.

---

# 148. Index Strategy

Critical candidate indexes around:

```text
tenant_id
legal_entity scope
price_list_version_id
variant_id
party_id
price_group_id
uom_id
validity
min_qty
status
```

Exact composite indexes follow measured resolver query shapes.

---

# 149. Do Not Build One Monster Index

Resolver can use staged candidate queries/read models.

Measure with realistic datasets.

---

# 150. Price Resolution Projection

Hot-path optimized projection is allowed:

```text
pricing_resolution_index
```

It contains only active/versioned rule fields needed for resolution.

Source remains Price List/Rules.

---

# 151. Projection Purpose

Avoid expensive normalization joins for every product selection.

---

# 152. Projection Update

Active version activation builds/updates projection atomically/through safe staged swap.

---

# 153. Activation Strategy

Preferred:

```text
Validate Draft
Build Resolution Projection
Run Conflict Scan
Run Sample Tests
Activate version pointer
Publish event
```

---

# 154. Active Version Pointer

Resolver should quickly know:

```text
PriceList → ActiveVersionId
```

without scanning versions.

---

# 155. In-Memory Cache

Cache immutable active Pricing versions/rule segments.

Key includes:

```text
Tenant
Legal Entity
Price List
Version
```

---

# 156. Cache Is Not Truth

DB/version remains authoritative.

Cache invalidated on activation.

---

# 157. No Redis Day 1

Postgres + immutable version cache + optimized queries first.

Redis only after measured need.

---

# 158. Resolver Performance Target

Pricing is in the critical sales hot path.

Initial engineering target:

```text
single local resolution p95 <= 20–30 ms
```

after context masters are already resolved/cached, under realistic load.

End-to-end UI has separate budget.

Target must be load-tested.

---

# 159. Batch Resolution Is Mandatory

API/contract:

```text
ResolvePricesBatch(contexts)
```

for quotations/orders with many lines.

---

# 160. No N+1

100-line order must not mean 100 DB rule queries.

---

# 161. Batch Algorithm

Concept:

1. group contexts by Entity/List/Currency/Date;
2. fetch candidate rule segments in batches;
3. resolve in memory;
4. call FinBase conversion in batch where required;
5. return ordered results.

---

# 162. Product Picker Pricing

Sales Product Picker may request prices for 20–50 visible variants.

Use batch pricing.

---

# 163. Pricing Search vs Resolution

Pricing doesn't need text search.

Catalog searches products; Pricing resolves their prices.

---

# 164. Large Price Lists

A tenant may have:

```text
100k variants
multiple lists
customer contracts
tiers
```

Resolver must not load entire tenant price database for one request.

---

# 165. Cache Segmentation

Cache per active Price List Version and hot rule scopes.

Avoid unbounded all-customer price cache.

---

# 166. Party-Specific Price Scale

If millions of customer-specific entries exist, use indexed DB/projection lookup and selective cache.

---

# 167. Cache Key Safety

Always Tenant + Entity scoped.

Never:

```text
SKU → price
```

globally.

---

# 168. Price Changes and Cache

Version activation is perfect invalidation boundary.

Old in-flight requests can finish on old immutable version; new requests use new version after atomic pointer change.

---

# 169. Determinism

Same:

```text
PricingContext
Price List Version
FinBase snapshot/policy
Resolver version
```

must produce same result.

---

# 170. Resolver Version

Track:

```text
pricing_resolver_v1
```

Breaking precedence change creates new version/ADR.

---



# 171. Resolver Purity

After candidate data/context is loaded, rule resolution should be pure in-memory logic.

No random DB call during comparator execution.

---

# 172. Resolver Stages

```text
Resolve List
→ Resolve Active Version
→ Gather Eligible Candidate Set
→ Hard Filter
→ Rank
→ Detect Conflict
→ Compute Price
→ Apply Optional Conversion
→ Return Explainable Result
```

---

# 173. Candidate Query Strategy

Use the strongest known dimensions first:

```text
PriceListVersion
Variant/Template/Category
Party/Group
Date
UOM
Quantity
```

Do not query all rules then filter in application.

---

# 174. Category Candidate Expansion

Catalog can provide category ancestry:

```text
Water
Beverages
Food
```

Resolver receives/loads stable IDs once.

No recursive Catalog DB joins inside Pricing hot path.

---

# 175. Brand Candidate

Brand supplied by Catalog summary/context.

No repeated fetch per rule.

---

# 176. Product Context DTO

Catalog may expose:

```text
PricingProductContext
{
  VariantId
  TemplateId
  CategoryPathIds
  BrandId?
  ValidUomIds
}
```

batch-capable.

---

# 177. Party Pricing Context DTO

Sales/Purchasing supplies:

```text
PartyId
PriceGroupId?
AssignedPriceListId?
```

Avoid Pricing fetching unrelated Party detail.

---

# 178. Rule Comparator Is Publicly Documented

Admin/support documentation should describe precedence in business language.

No hidden algorithm.

---

# 179. Rule Comparator Unit Tests

Golden vectors for:

```text
party vs group
variant vs template
subcategory vs category
exact UOM vs derived
quantity thresholds
priority
conflict
```

---

# 180. Conflict Scanner

Activation scan detects:

- exact scope overlap;
- quantity overlap;
- date overlap;
- same priority/effective rank;
- contradictory result.

---

# 181. Overlap Date Logic

Two periods overlap when intervals intersect.

Open-ended dates handled explicitly.

---

# 182. Overlap Quantity Logic

Ranges:

```text
[min,max]
```

with max optional infinity.

Boundary inclusivity explicit.

---

# 183. Conflict Scanner Performance

Activation can be heavier than runtime.

Use SQL set-based overlap detection + domain confirmation.

Price List activation is not hot path.

---

# 184. Conflict Severity

```text
Blocking
Warning
Info
```

Blocking:

identical effective rank contradictory price.

Warning:

broad rules that may surprise but comparator resolves.

---

# 185. Preview Before Activation

Show:

```text
Rules
Conflicts
Warnings
Products affected
Scheduled date
```

---

# 186. Price Change Impact Preview

For changed entries, calculate:

```text
old price
new price
absolute delta
percentage delta
```

for direct entries.

---

# 187. Customer Contract Impact

If changing group/default list, show approximate number of assigned customers/parties.

Do not necessarily enumerate millions synchronously.

---

# 188. Approval Hook

Before activation:

```text
BeforeActivatePriceListVersion
→ Allow / Deny / RequireApproval
```

uses Platform/Approvals hooks.

---

# 189. Price Activation Event

Publish:

```text
PriceListVersionActivatedV1
```

with:

```text
PriceListId
VersionId
EffectiveFrom
```

No need to publish every entry.

---

# 190. Price Entry Change Event

Draft edits don't need broad integration events.

Active version switch is meaningful boundary.

---

# 191. Price Snapshot Contract

Downstream modules never need live Price Rule after transaction is finalized.

Snapshot carries enough evidence.

---

# 192. Snapshot Does Not Copy Full Rule

Store relevant identifiers + commercial result/inputs.

Avoid embedding huge rule JSON.

---

# 193. Snapshot Explainability

At least store:

```text
list/version
rule/version
resolved unit price
base price/discount if meaningful
UOM
currency
pricing date
manual override
resolver version
```

---

# 194. Price Validity at Transaction Time

If a document is backdated, PricingDate decides rule eligibility.

Sales/Accounting domains decide whether backdating is allowed.

---

# 195. Quote Future Delivery

Do not use delivery date as PricingDate unless Sales policy explicitly says so.

No implicit date assumptions.

---

# 196. Price Expiration

Quotation may remain legally/commercially valid after master rule expiration because it has a snapshot.

Sales `ValidUntil` governs quote validity.

---

# 197. Price Revalidation

Sales can request:

```text
ValidateSnapshotStillCurrent
```

for drafts/open quotes.

Pricing returns:

```text
Same
Changed
NoLongerEligible
Conflict
```

---

# 198. Order Conversion

Quotation→Order can:

```text
KeepQuotedPrice
or
Reprice
```

based on Sales policy.

Pricing supports both; Sales owns choice.

---

# 199. Invoice From Order

Normally Invoice should inherit agreed/order price snapshot rather than re-query current master.

Invoicing/Sales pack locks exact flow.

---

# 200. Direct Invoice

Direct invoice may resolve Pricing if no source document.

---

# 201. POS

POS can use Pricing batch/local read model later.

Offline POS needs separate synchronized price snapshot package.

Not built here.

---

# 202. E-Commerce

Website/channel can request appropriate Price List/context.

Do not create separate e-commerce price master.

---

# 203. Marketplace

Channel mapping may expose different commercial list.

External marketplace-specific fees are not Pricing product price unless explicitly modeled.

---

# 204. Purchase RFQ

Supplier quotation may provide one-off offered price.

That transaction offer snapshot can override/respect Buying Pricing without changing master automatically.

---

# 205. Supplier Master Price

Repeated supplier pricing may be imported/maintained as Buying Price List entries.

---

# 206. Supplier Lead Time

Not Pricing truth.

Purchasing profile owns lead time.

ERPNext may place lead time near Item Price, but our ownership remains separated.

---

# 207. Minimum Order Quantity

Supplier MOQ belongs Purchasing/commercial supplier-product profile.

Quantity price threshold in Pricing is only about price eligibility.

Do not conflate.

---

# 208. Purchase Contract

A supplier contract can reference a Buying Price List/Version.

Contract ownership separate.

---

# 209. Discounts vs Price

We distinguish:

```text
Resolved base/list price
Automatic pricing discount
Manual discount
Final commercial unit price
```

for explanation.

---

# 210. Header/Document Discount

A discount on whole order/invoice is not line price master.

Sales/Invoicing calculation owns allocation/application.

Core Pricing can advise line prices only.

---

# 211. Line Manual Discount

Sales may allow user to enter discount after Pricing resolution.

Store separately in line snapshot/calculation.

Do not mutate Price List.

---

# 212. Automatic Pricing Discount

If Pricing rule is percentage discount, resolution exposes it.

Sales line can present it.

---

# 213. Discount Stacking Policy

MVP default:

automatic Pricing discount + manual discount stacking must be explicitly allowed/defined by Sales policy.

Avoid accidental double discount.

---

# 214. "Ignore Pricing Rule"

Some ERPs expose this.

Our system should prefer:

```text
Manual Override / explicit list selection
```

with audit, rather than generic "ignore all rules" unless advanced permission needs it.

---

# 215. Ignore Rules Permission

If implemented:

```text
sales.pricing.ignore_rules
```

and reason required.

---

# 216. Price Visibility Permission

Some users can sell but should not see:

- cost;
- margin;
- internal floor;
- contract metadata.

DTO/UX hides sensitive fields.

---

# 217. Price List Admin Permission

Initial:

```text
pricing.read
pricing.list.manage
pricing.version.create
pricing.rule.manage
pricing.version.activate
pricing.simulate
pricing.import
pricing.export
pricing.audit.view
```

---

# 218. Price Override Permissions

Owned by Sales/Purchase domains:

```text
sales.price.override
sales.discount.override
purchase.price.override
```

---

# 219. Cost/Margin Permissions

Future:

```text
pricing.margin.view
inventory.cost.view
```

Need both if result reveals cost-sensitive data.

---

# 220. Data Leakage Rule

Pricing resolver for salesperson must not return cost simply because it used cost floor internally.

Return:

```text
floor status
```

not raw cost unless permitted.

---

# 221. Margin Warning UX

Sales user might see:

```text
Manager approval required
```

not:

```text
Cost = 71.3421
```

if cost confidential.

---

# 222. Currency Conversion Fallback UX

If list USD but document EGP and conversion allowed:

user sees final EGP price.

Advanced details can show:

```text
Source list USD
Commercial FX snapshot
```

---

# 223. Price Display Precision

UI may format according to business/product settings.

But final underlying UnitPrice retains high precision.

---

# 224. Small Price Products

Products such as bulk commodities may require 4–6+ unit-price decimals.

Do not truncate early.

---

# 225. Large Unit Prices

Numeric types from FinBase support large values.

Business validation prevents absurd inputs.

---

# 226. Price Entry Validation

On save:

- Variant exists/visible.
- UOM valid.
- Currency matches list.
- amount >= 0 (default).
- quantity range valid.
- dates valid.
- scope references same tenant/entity.
- no invalid overlap in draft validation.

---

# 227. Rule Validation

- required result value;
- base source valid;
- no recursion;
- no impossible scope;
- quantity min <= max;
- valid_from <= valid_to.

---

# 228. Version Activation Validation

Must pass:

- rule validation;
- conflict scan;
- dependency DAG;
- currency/entity consistency;
- required default coverage policy if configured;
- optional simulations/tests.

---

# 229. Coverage Validation

Optional business policy can warn when active Catalog sellable products have no default selling price.

Do not force every Product if not sold.

---

# 230. Coverage Report

Examples:

```text
Sellable products missing price
Buying products missing supplier price
Products with only expired prices
Products with conflicting draft rules
```

---

# 231. Pricing Data Quality Dashboard

Later / useful:

- missing prices;
- zero prices;
- outlier changes;
- conflicting rules;
- expired contracts;
- unused Price Lists;
- customer-specific prices not used.

---

# 232. Price Outlier Detection

Large price change warning:

```text
+300%
-80%
```

can prompt review.

Not automatic rejection unless policy.

---

# 233. AI Use

AI can:

- explain rule conflict;
- map Excel columns;
- suggest group assignment;
- flag unusual price changes;
- draft rules from plain-language request.

AI cannot:

- activate prices;
- choose winner in ambiguous conflict;
- override floor automatically;
- invent cost;
- change live price without human confirmation.

---

# 234. Plain-Language Draft

Future AI:

> "Distributors get 10% off beverages above 50 cartons in December."

AI produces Draft structured rule.

User reviews.

---

# 235. No AI in Hot Resolver

Runtime price resolution is deterministic code, not LLM call.

---

# 236. Import from Excel

Real businesses often manage pricing in spreadsheets.

Importer is critical.

---

# 237. Excel Round-Trip

Export active/draft list.

User edits permitted fields.

Re-import into new Draft Version.

Do not update live active version directly.

---

# 238. Import IDs

Include stable hidden/reference IDs where possible:

```text
PriceListCode
SKU
PartyCode/ExternalRef
UOM Code
```

---

# 239. Duplicate Import Rows

Same effective scope appears twice with conflicting values:

block preview.

---

# 240. Decimal Import

Parse using invariant/declared locale carefully.

Do not let `1,25` become `125`.

---

# 241. Currency Import

Explicit Price List currency.

Don't infer from symbol `$`.

---

# 242. Performance — Product Picker

Catalog returns variants.

Pricing batch enriches visible rows only.

---

# 243. Performance — Quote 500 Lines

One batch call/request context.

No per-line network/API/DB calls.

---

# 244. Performance — Batch Grouping

Group by:

```text
Entity
Direction
Price List
Party/Group
PricingDate
Currency
```

Then resolve product-specific candidates efficiently.

---

# 245. Resolver Data Access

Potential pattern:

```text
Load exact party/variant matches
Load group/template/category matches
Load general fallback
```

in bounded queries.

---

# 246. Rule Projection Design

Potential flat row:

```text
tenant_id
entity_id
price_list_version_id
direction
party_id?
price_group_id?
variant_id?
template_id?
category_id?
brand_id?
uom_id?
min_qty
max_qty
valid_from
valid_to
priority
result_type
result_value
rank metadata
```

---

# 247. Projection Indexes

Measured composite/partial indexes for active segments.

Examples:

```text
(version, variant, party)
(version, variant, price_group)
(version, template, party)
(version, category, price_group)
(version, variant, min_qty)
```

Avoid blindly adding every combination.

---

# 248. Partial Indexes

Active projection/version design can reduce need for status filtering.

---

# 249. Active Version Isolation

Only one active version per effective period/list context.

Database exclusion/validation as appropriate.

---

# 250. Scheduled Version Overlap

Do not allow two versions of same list to claim same effective period without explicit transition semantics.

---

# 251. Version Effective Cutover

At effective time/date:

new version becomes resolver target.

Current transaction snapshots remain old.

---

# 252. Timezone

Effective dates interpreted using Legal Entity business timezone/date semantics.

Use NodaTime.

---

# 253. Daily vs Timestamp Pricing

MVP use LocalDate validity for most commercial pricing.

Intraday timestamp versioning only if real market need.

---

# 254. Cache Warmup

On version activation, optionally precompile/cache important resolver structures.

---

# 255. Compiled Rule Set

Immutable in-memory representation can precompute:

- specificity rank;
- normalized quantity bounds;
- category depth;
- result type.

Speeds resolver.

---

# 256. Memory Safety

Do not load millions of customer-specific rules into every app instance.

Segment/hybrid cache.

---

# 257. Hot/Cold Strategy

Cache:

- default/general lists;
- frequently used group rules.

DB indexed lookup for cold party-specific entries.

---

# 258. Multi-Instance Consistency

Activation event/version pointer invalidates app caches across instances.

RabbitMQ event can notify all nodes.

---

# 259. Cache Stale Safety

Cache key includes immutable Version ID.

Even if invalidation is delayed, new document should resolve active pointer correctly.

Active pointer cache must have safe short TTL/event invalidation.

---

# 260. Atomic Version Activation

Activation transaction updates:

```text
validated version state
active version reference
audit
outbox
```

atomically where feasible.

---

# 261. Scheduled Activation Worker

Quartz decides when.

Command performs transactional activation.

RabbitMQ can notify downstream caches after commit.

---

# 262. Broker Outage

Active price version still commits locally with Outbox.

Cache invalidation event sends when broker returns.

Local request path verifies active version pointer.

---

# 263. Pricing Availability

Pricing is critical for Sales.

If Pricing DB/app unavailable in Modular Monolith, Sales cannot safely resolve new price.

Do not silently use stale arbitrary client cache.

---

# 264. Offline POS Later

POS will need signed/versioned synchronized price package and conflict policy.

Separate design.

---

# 265. API Surface

Base admin:

```text
/api/v1/pricing
```

Examples:

```text
GET/POST /price-lists
GET /price-lists/{id}
POST /price-lists/{id}/versions
GET /price-list-versions/{id}

POST /price-list-versions/{id}/validate
POST /price-list-versions/{id}/activate
POST /price-list-versions/{id}/schedule

POST /resolve
POST /resolve-batch
POST /simulate

POST /imports
GET /coverage
```

---

# 266. Hot Path Internal Contract

Inside Modular Monolith, Sales does NOT call Pricing over HTTP.

Use in-process contract/service.

---

# 267. `IPricingResolver`

Concept:

```csharp
PriceResolution Resolve(PricingContext context);
```

plus async data boundary where appropriate.

---

# 268. `IPricingBatchResolver`

```csharp
ResolveBatch(IReadOnlyList<PricingContext>)
```

mandatory.

---

# 269. `IPriceListSelector`

Resolves assigned/default list based on explicit context.

---

# 270. `IPricingSimulator`

Admin/debug.

---

# 271. `IPricePolicyProvider`

Returns pricing/override/fallback policies for Entity/context.

---

# 272. No God `PricingService`

Keep resolution, version management, import, simulation separated.

---

# 273. Commands

```text
CreatePriceList
CreatePriceListVersion
AddPriceEntry
AddPriceRule
ValidatePriceListVersion
SchedulePriceListVersion
ActivatePriceListVersion
ArchivePriceList
ImportPricingDraft
```

---

# 274. Queries

```text
GetPriceList
ListPriceLists
GetVersion
ResolvePrice
ResolvePricesBatch
SimulatePrice
GetCoverageReport
ExplainResolution
```

---

# 275. Domain Events

Meaningful:

```text
PriceListCreated
PriceListVersionValidated
PriceListVersionActivated
PriceListArchived
```

---

# 276. Integration Events

Potential:

```text
pricing.price-list-version-activated.v1
pricing.price-list-archived.v1
```

Do not emit event per every price row activation if version event is enough.

---

# 277. External Integration

E-commerce/marketplace connectors may request/export current price snapshot/version.

They do not read Pricing tables directly.

---

# 278. Webhooks

Integration Hub later can publish:

```text
PriceListVersionActivated
```

or product-price change feed derived from version diff.

---

# 279. Version Diff

Store/compute diff:

```text
added
changed
removed
```

between versions.

Useful for export/integration.

---

# 280. Massive Version Diff

Compute async if huge.

---

# 281. Audit

Must audit:

- list creation/archive;
- draft edits;
- import;
- conflict override? (ideally no override for hard conflict);
- activation;
- scheduling;
- emergency change;
- manual transaction override belongs consuming module.

---

# 282. Audit Does Not Mean Every Resolution

Do not append Audit event for every product price lookup.

That would be huge/noisy.

Transaction snapshot provides evidence.

---

# 283. Observability Metrics

```text
pricing_resolve_duration
pricing_batch_duration
pricing_batch_size
pricing_missing_total
pricing_conflict_total
pricing_cache_hit_ratio
pricing_version_activation_duration
pricing_import_errors_total
pricing_override_deviation_bucket
```

No tenant/product IDs as metric labels.

---

# 284. Tracing

Trace:

```text
Sales → Pricing
version selected
candidate fetch
candidate count
resolve
FinBase conversion if used
```

No sensitive cost unless authorized/debug.

---

# 285. Slow Resolution Logging

Sample slow calls with:

- version;
- candidate counts;
- dimensions;
- query timing.

Avoid logging entire customer contract details.

---

# 286. Performance Gate

Benchmark:

```text
simple default price
customer-specific price
group/category price
quantity tier
UOM explicit
FX conversion
batch 50
batch 500
```

---

# 287. Dataset Scale

Test at least:

```text
100k variants
5 price lists
500k direct entries
100k conditional rules/contracts in stress scenario
```

and larger synthetic enterprise tests.

---

# 288. Multi-Tenant Load

Simulate many small tenants plus several large tenants.

Ensure one large list does not poison all caches/queries.

---

# 289. Query Plan Review

Capture PostgreSQL `EXPLAIN ANALYZE` for hot candidate queries.

---

# 290. No Full-Table Scan in Resolver

Hot production resolver query must use selective index/partitioned projection strategy.

---

# 291. Partitioning

Do not partition Pricing Day 1 merely due fear of scale.

Measure.

Cell architecture handles SaaS growth across tenants.

---

# 292. Price Table Growth

Master pricing changes slower than transaction ledgers.

Postgres can handle large datasets with correct indexing.

---

# 293. Cell Readiness

Pricing data moves with Tenant/Cell.

No global cross-cell Price List truth.

---

# 294. RLS Performance

RLS remains mandatory.

Indexes include Tenant key appropriately.

Never remove isolation for speed.

---

# 295. DB Foreign Keys

Within Pricing strong composite tenant FKs.

Cross-foundation stable IDs according to architecture contracts.

---

# 296. No Cross-Module ORM Navigation

No:

```text
PriceEntry.Product.Navigation
PriceEntry.Party.Navigation
```

across DbContexts.

IDs + contracts/read context.

---

# 297. `PricingDbContext`

Only Pricing-owned entities.

---

# 298. Frontend Structure

```text
modules/pricing/
├── pages/
│   ├── PriceListPage/
│   ├── PriceListVersionEditor/
│   ├── PricingSimulator/
│   ├── CoveragePage/
│   └── ImportPage/
├── features/
│   ├── create-price-list/
│   ├── bulk-edit-prices/
│   ├── manage-tiers/
│   ├── validate-version/
│   ├── activate-version/
│   └── simulate-price/
├── components/
│   ├── PriceInput/
│   ├── QuantityTierEditor/
│   ├── PricingExplanation/
│   ├── ConflictPanel/
│   └── PriceChangePreview/
└── ...
```

---

# 299. Product Workspace Integration

Simple:

```text
Selling Price
```

Pricing-owned frontend component embedded in Catalog page.

Catalog module does not implement save logic itself.

---

# 300. Modular UI Contribution

Pricing can contribute section/widget to Product Workspace when active.

Clean module boundary.

---

# 301. Small Business Pricing Screen

Could simply show:

```text
Default Prices
Import Prices
```

Advanced Price Lists hidden under:

```text
Advanced Pricing
```

---

# 302. Advanced Pricing Screen

Tabs:

```text
Price Lists
Customer/Group Prices
Quantity Tiers
Scheduled Prices
Simulator
Imports
```

---

# 303. Price List Editor UX

Columns:

```text
Product/SKU
UOM
Min Qty
Price
Valid From
Valid To
Scope
```

Not 30 fields by default.

---

# 304. Inline Editing

Draft version can allow spreadsheet-like inline editing.

Active version read-only.

---

# 305. Keyboard/Excel-like UX

Pricing admins often work with many rows.

Support:

- keyboard;
- paste ranges later;
- bulk fill;
- filters;
- export/import.

---

# 306. Virtualization/Pagination

Do not render 100k price rows.

Server-side filtering + virtualized grid.

---

# 307. Price Filter

```text
SKU/name
Category
Customer/group
UOM
Changed only
Missing
Conflict
Effective date
```

---

# 308. Search

Catalog product search service supplies product identity results.

Pricing joins its own direct entries by IDs.

---

# 309. Product Name Snapshot in Admin Grid

Read projection can denormalize Product display name/SKU for admin speed.

Catalog remains source.

---

# 310. Projection Update on Product Rename

Catalog event updates Pricing admin read model asynchronously.

Price resolution uses IDs, so rename does not affect correctness.

---

# 311. Category Move Impact

If Category-based Pricing Rule exists and Product moves category, future resolved price may change.

Important.

---

# 312. Category Change Warning

Catalog may ask Pricing capability:

```text
Will category change affect active pricing?
```

and warn user.

Do not hard-block unless configured.

---

# 313. Brand Change Impact

Same for Brand-based rules.

---

# 314. Product Archive

Archived Product remains in old pricing history.

Future resolution normally unavailable because Catalog product inactive.

---

# 315. UOM Change Impact

Removing UOM referenced by active Price List must be blocked/validated through Catalog lifecycle contributor.

---

# 316. Party Merge Impact

Directory Party merge:

party-specific Pricing references must resolve/migrate to canonical Party through Party merge contributor/event.

---

# 317. Party Archive

Price rules may remain historical but no new sales selection for archived party.

---

# 318. Entity Deactivation

Pricing lists/rules retained read-only.

---

# 319. Price List Archive

Cannot be default/assigned to new docs.

Historical snapshots unaffected.

---

# 320. Price Version Rollback

Do not mutate history.

Rollback means create/reactivate an allowed version according to effective-date policy, preferably new version referencing previous content.

Audit.

---

# 321. Retroactive Price Change

Changing past effective master prices does not alter posted transactions.

If business needs retroactive rebates/credits, separate adjustment process.

---

# 322. Rebate

Volume rebate after period end is not ordinary Unit Price resolution.

Future Rebates/Promotions/Settlement domain.

---

# 323. Contract Settlement

Similarly not core pricing.

---

# 324. Sales Commission

Not Pricing.

Commission module may consume final transaction prices.

---

# 325. Landed Cost

Not Pricing.

Inventory/Purchasing.

---

# 326. Supplier Discounts on Invoice

If recurring predictable unit-price rule → Buying Pricing.

If document-level settlement discount → Purchasing/Invoice terms.

---

# 327. Payment Discount

Not Pricing.

FinBase Payment Terms/Subledger.

---

# 328. Tax Discount Interaction

Tax Pack defines whether tax is computed before/after permitted discount and legal behavior.

Pricing returns commercial discount facts.

---

# 329. Promotion + Tax

Future Promotions must emit structured commercial adjustments for Tax.

No opaque final total.

---

# 330. Pricing and Accounting

Ledger does not re-run Pricing.

It consumes transaction/accounting facts based on snapshotted commercial values.

---

# 331. Pricing and Invoicing

Invoice created from Sales should normally inherit agreed snapshot.

Direct invoice can resolve pricing if permitted.

---

# 332. Pricing and Returns

Return/credit flows should reference original price snapshot by default, not current price.

Sales/Invoicing pack locks exceptions.

---

# 333. Pricing and Exchange Rate Changes

Original transaction snapshot remains.

Repricing in new currency/context resolves new result explicitly.

---

# 334. Pricing and Catalog Variant Merge

Product merge isn't MVP; if later, Pricing must be contributor.

---

# 335. Pricing and Product Replacement

Replacement Product gets its own current price.

Do not transfer automatically without explicit business rule.

---

# 336. Contract Tests — Catalog

Pricing verifies:

- Variant summary.
- Category path.
- valid UOM.
- lifecycle.

---

# 337. Contract Tests — Directory

Pricing verifies Party reference/merge behavior.

---

# 338. Contract Tests — FinBase

Pricing verifies:

- CurrencyCode.
- UnitPrice precision.
- conversion snapshot.
- no hidden rounding.

---

# 339. Contract Tests — Sales Sample

Sample consumer proves:

```text
Customer + Product + Qty → price
```

without DB coupling.

---

# 340. Unit Test Matrix

- default list;
- explicit list;
- party assignment;
- group assignment;
- variant rule;
- template rule;
- category rule;
- brand rule;
- exact UOM;
- derived UOM;
- quantity boundary;
- date boundary;
- currency conversion;
- missing price;
- zero price;
- conflict;
- manual override comparison.

---

# 341. Boundary Quantity Tests

If rule 10+:

```text
9.999999
10
10.000001
```

according to quantity precision/UOM.

---

# 342. Date Boundary Tests

```text
day before valid_from
valid_from
valid_to
day after
```

---

# 343. Category Hierarchy Tests

Variant in deep subcategory; verify deepest intended rule.

---

# 344. Conflict Property Tests

Generate overlapping same-rank rules.

Activation must not pass.

---

# 345. Determinism Property Test

Random candidate input ordering must not change winning result.

Critical.

---

# 346. DB Row Order Test

Shuffle insertion order/IDs.

Same result.

---

# 347. Version Determinism

Same active version/context always same result independent of later draft edits.

---

# 348. Cache Consistency Test

Cold vs warm cache returns identical PriceResolution.

---

# 349. Batch vs Single Equivalence

For same contexts:

```text
ResolveBatch[i] == Resolve(context[i])
```

---

# 350. RLS Tests

Tenant A cannot:

- see B lists;
- use B list ID;
- resolve B price;
- import into B.

---

# 351. Entity Scope Tests

Same tenant, Entity A list not usable in Entity B unless explicitly shared.

---

# 352. Permission Tests

Salesperson cannot activate Price List.

Pricing admin cannot automatically see Inventory cost without cost permission.

---

# 353. Import Tests

100k+ price rows.

Validate throughput/memory/duplicate conflict reporting.

---

# 354. Performance Tests

At minimum:

```text
1 rule simple
100 candidates
party-specific
group/category
quantity tiers
batch 50
batch 500
many concurrent tenants
```

---

# 355. Performance Regression

Nightly/scheduled performance suite records p50/p95/p99.

---

# 356. Product Acceptance Scenario — Small SME

Owner opens Product:

```text
Selling Price = 100 EGP
```

saves.

Later quotation automatically resolves 100.

Owner never configures Price List manually.

---

# 357. Product Acceptance Scenario — Wholesale

Customer assigned Wholesale list.

Product selected → 90 instead of Retail 100.

---

# 358. Product Acceptance Scenario — Specific Customer

Customer ABC explicit 92 beats Wholesale 100.

Explain mode shows why.

---

# 359. Product Acceptance Scenario — Quantity Tier

Qty:

```text
49 → 95
50 → 87
```

boundaries exact.

---

# 360. Product Acceptance Scenario — UOM

Piece 10.

Carton explicit 210.

Selecting Carton returns 210, not 240.

---

# 361. Product Acceptance Scenario — FX

USD Price List, EGP transaction, conversion fallback allowed.

FinBase handles rate.

Resolution records commercial FX context.

---

# 362. Product Acceptance Scenario — No Price

No matching rule.

System never inserts zero silently.

---

# 363. Product Acceptance Scenario — Override

Resolver 90.

Authorized user enters 88.

Snapshot records both and reason.

---

# 364. Product Acceptance Scenario — Unauthorized Override

Field/action disabled in UI and backend rejects direct API attempt.

---

# 365. Product Acceptance Scenario — Conflicting Rules

Admin creates two identical active-target rules with different prices.

Activation blocked with understandable conflict.

---

# 366. Product Acceptance Scenario — Future Version

2027 Version scheduled.

2026-12-31 uses old.

2027-01-01 uses new according to Legal Entity business date.

---

# 367. Product Acceptance Scenario — Existing Order

Price List changes.

Existing confirmed Order keeps old snapshot.

---

# 368. Product Acceptance Scenario — Reprice Draft

User requests Reprice.

System shows old/new difference before update.

---

# 369. Product Acceptance Scenario — Customer Group

Distributor Group price applies.

Changing customer to Retail triggers re-resolution in draft.

---

# 370. Product Acceptance Scenario — Buying

Supplier-specific Buying price resolves separately from Selling prices.

---

# 371. Product Acceptance Scenario — Product Category Move

Product moved into another category.

Future price resolution changes according to rule.

Existing transactions unchanged.

---

# 372. Product Acceptance Scenario — Party Merge

Customer duplicate merged.

Party-specific price follows canonical Party safely without duplication.

---

# 373. Red-Team — Product Has Duplicate Price Column

Architecture review rejects `Catalog.Product.SalePrice` as master truth.

Only UI projection is allowed.

---

# 374. Red-Team — Newest Rule Wins

Not allowed.

Hard same-rank conflict blocks activation.

---

# 375. Red-Team — Price Missing Becomes Zero

Not allowed.

Explicit `PriceMissing`.

---

# 376. Red-Team — Frontend Calculates Final Price Alone

Not authoritative.

Backend resolver returns canonical result.

---

# 377. Red-Team — 100 Lines Cause 100 Queries

Performance test fails.

Batch resolver required.

---

# 378. Red-Team — Customer A Cache Used for Customer B

Cache keys/segmentation prevent.

---

# 379. Red-Team — Price List Currency Hidden Conversion

Not allowed.

FinBase conversion is explicit and snapshotted.

---

# 380. Red-Team — Rule Uses Inventory Cost Without Permission

Resolver may use floor internally but does not expose cost.

---

# 381. Red-Team — Active Version Edited

Blocked.

Create new Draft Version.

---

# 382. Red-Team — Backdated Document Uses Today

PricingDate explicit; test prevents.

---

# 383. Red-Team — Category + Brand Ambiguity

Comparator documented or activation conflict surfaced.

No accidental DB order.

---

# 384. Red-Team — Decimal Precision Loss

High-precision UnitPrice round-trip tests.

---

# 385. Red-Team — Rule Cycle

Price List DAG validation blocks.

---

# 386. Red-Team — Archived Price List Selected

Backend rejects new use.

---

# 387. Red-Team — Tenant Leakage

RLS + contract tests.

---

# 388. Pricing Product Success Criteria

- Simple company can price Product in seconds.
- Salesperson gets correct price without thinking about rules.
- Manager can explain any price.
- Bulk prices work.
- UOM prices work.
- Customer contracts work.
- No duplicate price truth.
- Historical transactions never change.
- Advanced users can manage versions safely.
- Pricing remains fast at catalog scale.

---

# 389. Pricing Performance Success Criteria

- Hot single resolve p95 target measured.
- Product picker uses batch.
- 500-line docs avoid N+1.
- version activation can precompile safely.
- cache behavior deterministic.
- no cross-tenant/entity leakage.

---

# 390. Business KPIs

Potential:

```text
Manual override rate
Missing price incidents
Average discount deviation
Price-list utilization
Expired contract prices
Conflict count
Quote repricing frequency
```

Do not expose margin/cost KPI to unauthorized users.

---

# 391. Product Analytics

Events:

```text
pricing_missing_shown
manual_price_override
pricing_explanation_opened
draft_price_version_activated
pricing_conflict_detected
```

No sensitive price detail in generic analytics unless governed.

---

# 392. Runbook — Wrong Price Reported

1. Capture transaction/context.
2. Open Explain Resolution.
3. Identify snapshot/version.
4. Check Party/Product/UOM/Quantity/Date.
5. Check rule conflict/history.
6. Correct future Price Version.
7. Never rewrite posted transaction silently.

---

# 393. Runbook — Missing Prices After Activation

- validate active version pointer;
- coverage report;
- projection/cache version;
- rule validity;
- UOM/currency;
- rollback/new version if severe.

---

# 394. Runbook — Cache Incident

Resolver can fall back to canonical DB/projection.

Cache never required for correctness.

---

# 395. Runbook — Import Error

Import batch has reconciliation/error report.

Fix draft then revalidate.

---

# 396. Build Now — MUST

- Price Lists.
- immutable/versioned Price List Versions.
- default Selling/Buying lists.
- Product default-price UX projection.
- explicit Variant/UOM price.
- party/group price.
- quantity tiers.
- effective dates.
- currency-aware pricing using FinBase.
- deterministic specificity.
- conflict detection.
- missing-price semantics.
- manual override contract.
- snapshots.
- batch resolver.
- simulation/explainability.
- import/export.
- RLS.
- performance tests.
- activation events/cache invalidation.

---

# 397. Design Now / Build Later

- referenced-list formulas/markup if not launch-needed.
- cost-floor provider.
- price approvals.
- channel-specific pricing.
- territory.
- warehouse/batch conditions.
- offline POS package.
- contract test suites saved by tenant.
- advanced pricing analytics.

---

# 398. Do Not Build Here

- coupons;
- loyalty;
- Buy-X-Get-Y;
- free gift engine;
- rebates;
- cumulative spend campaigns;
- commissions;
- tax engine;
- Inventory cost calculation;
- approval workflow engine;
- arbitrary formula DSL.

---

# 399. Implementation Order

```text
PR00 Pricing ADRs + Ownership
PR01 Schema + RLS
PR02 PriceList + Version Lifecycle
PR03 Default Selling/Buying Lists
PR04 Explicit Variant/UOM Entries
PR05 Price Groups + Party Scope
PR06 Quantity Tiers
PR07 Effective Dates
PR08 Currency / FinBase Integration
PR09 Deterministic Resolver
PR10 Conflict Validator
PR11 Batch Resolver
PR12 Price Snapshot Contract
PR13 Manual Override / Deviation Contract
PR14 Product Selling Price UX
PR15 Admin Price List Editor
PR16 Simulator / Explainability
PR17 Import / Export / Bulk Change
PR18 Cache + Active Version Projection
PR19 Events / Outbox / Multi-instance Invalidation
PR20 Security / Load / Red-Team
PR21 Product Acceptance
```

---

# 400. PR00 Exit Gate

ADR approval for:

- price source of truth;
- Product price UX;
- specificity comparator;
- conflict behavior;
- versioning;
- Price vs Promotion boundary.

---

# 401. PR01 Exit Gate

- `foundation_pricing`.
- RLS forced.
- runtime role non-owner.
- FK/tenant integrity.

---

# 402. PR02 Exit Gate

- Draft/Scheduled/Active/Archived.
- active version immutable.
- scheduled effective behavior.

---

# 403. PR03 Exit Gate

- simple SME gets default list automatically.
- Product Selling Price UI writes Pricing only.

---

# 404. PR04 Exit Gate

- Variant/UOM explicit prices.
- Catalog validation.
- high precision.

---

# 405. PR05 Exit Gate

- customer/group price.
- no Party duplication.
- profile assignment.

---

# 406. PR06 Exit Gate

Boundary tests for quantity tiers.

---

# 407. PR07 Exit Gate

Date eligibility/backdated context deterministic.

---

# 408. PR08 Exit Gate

- FinBase currency.
- no Pricing FX table.
- conversion snapshot when used.

---

# 409. PR09 Exit Gate

- deterministic result.
- insertion order independent.
- explanation output.

---

# 410. PR10 Exit Gate

Hard conflicts cannot activate.

---

# 411. PR11 Exit Gate

50/500 line batches no N+1.

---

# 412. PR12 Exit Gate

Transaction snapshot contract sufficient for history.

---

# 413. PR13 Exit Gate

Override captures resolved/final/deviation/reason.

---

# 414. PR14 Exit Gate

Small-company Product form remains simple.

---

# 415. PR15 Exit Gate

Admin can manage large Draft lists efficiently.

---

# 416. PR16 Exit Gate

"Why this price?" and simulator work.

---

# 417. PR17 Exit Gate

Large Excel price import with preview/conflict scan.

---

# 418. PR18 Exit Gate

Cache/version pointer deterministic under multi-instance load.

---

# 419. PR19 Exit Gate

Outbox event invalidates downstream caches after activation.

---

# 420. PR20 Exit Gate

- RLS.
- permission.
- cache isolation.
- load tests.
- red-team scenarios.

---

# 421. PR21 Product Acceptance Demo

Must demonstrate:

1. Simple Product Selling Price.
2. Retail/Wholesale.
3. Customer-specific.
4. Quantity 1/10/50 boundary.
5. Piece/Carton explicit prices.
6. USD/EGP conversion case.
7. Missing Price.
8. Manual override.
9. Conflicting Rule activation block.
10. Future Version cutover.
11. Existing order unchanged.
12. Buying supplier price.
13. Batch 100 lines with fast resolution.

---

# 422. Definition of Done Before Tax/Ledger/Sales

- [ ] Only Pricing owns reusable prices.
- [ ] Product price is only Pricing-backed UX.
- [ ] Default Selling/Buying list.
- [ ] Versioning/effective dates.
- [ ] Party/group scopes.
- [ ] Product/category scopes.
- [ ] UOM prices.
- [ ] Quantity tiers.
- [ ] Currency/FinBase integration.
- [ ] deterministic comparator.
- [ ] no creation-date/random tie-break.
- [ ] conflict blocking.
- [ ] missing-price error.
- [ ] snapshot.
- [ ] batch resolver.
- [ ] explainability.
- [ ] import/export.
- [ ] RLS.
- [ ] load test.
- [ ] architecture tests.

---

# 423. Senior Product Rule

The simple user should think:

```text
"This product costs 100."
```

The advanced user may think:

```text
"Distributor price for 50+ cartons in December is 82."
```

Both are powered by the same Pricing truth.

---

# 424. Senior Architecture Rule

> **One owner for reusable pricing. Many consumers. Historical transactions store snapshots, not live links as truth.**

---

# 425. Senior Engineering Rule

> **Speed comes from immutable versions, batch resolution, indexed candidate sets and safe caching — not from copying price fields into every module.**

---

# 426. Senior UX Rule

> **Do not expose Pricing Engine complexity unless the user's job requires it.**

---

# 427. Senior Commercial Integrity Rule

> **If the system cannot determine one price unambiguously, it must surface the configuration problem instead of guessing.**

---

# 428. Source Register — Current Market Review 2026-10-05

## Odoo 20 — Pricelists

Official:
https://www.odoo.com/documentation/20.0/applications/sales/sales/products_prices/prices/pricing.html

Observed:
- currency;
- multi-company/country applicability;
- Product/Category rules;
- fixed price/discount/formula;
- minimum quantity;
- rule precedence by specificity/quantity/category and final creation-date tie.

Our design uses explicit deterministic specificity but rejects creation date as hidden final business rule.

## Microsoft Dynamics 365 Business Central — Customer Price Groups

Official:
https://learn.microsoft.com/en-us/dynamics365/business-central/sales-how-to-set-up-customer-price-groups

Observed:
- customer price groups;
- item;
- UOM;
- unit price;
- variant;
- minimum quantity;
- dates;
- currency.

## Microsoft Dynamics 365 Business Central — Special Sales Prices & Discounts

Official:
https://learn.microsoft.com/dynamics365/business-central/sales-how-record-sales-price-discount-payment-agreements

Observed:
- party/group eligibility;
- date;
- UOM;
- minimum quantity;
- currency checks;
- automatic application to transaction lines.

## ERPNext — Pricing Rule

Official:
https://docs.frappe.io/erpnext/pricing-rule

Observed:
- Selling/Buying;
- item/group/brand;
- party/group;
- quantity/value/date;
- company/currency;
- priority;
- rate/discount/margin outcomes;
- explicit warning about overlapping rules.

## ERPNext — Price Lists / Item Price

Official:
https://docs.frappe.io/erpnext/price-lists
https://docs.frappe.io/erpnext/item-price

Observed:
- Selling and Buying Price Lists;
- multiple item prices;
- Price List currency;
- UOM-specific price;
- Customer/Supplier-specific price;
- minimum quantity;
- validity.

## ERPNext — Different UOM

Official:
https://docs.frappe.io/erpnext/Selling-in-different-UOM

Observed:
- explicit UOM-specific prices;
- optional UOM-derived pricing;
- deliberately priced Carton can differ from Unit × conversion.

## ERPNext — Promotional Scheme

Official:
https://docs.frappe.io/erpnext/promotional-scheme

Observed:
- multiple discount slabs;
- mixed/cumulative conditions;
- separate higher-level campaign concept.

This supports our boundary between core Pricing and Promotions.

## Daftra — Quantity Pricing

Official:
https://docs.daftra.com/en/tutorial/quantity-based-pricing-in-price-lists/

Observed:
- quantity-based Price Lists;
- unit price changes automatically by quantity;
- invoice and POS usage.

## Daftra — Customer Price List

Official:
https://docs.daftra.com/user_manual/تعيين-قائمة-أسعار-للعملاء/

Observed:
- customer-specific Price List assignment;
- automatic price application on invoice.

## Daftra — Wholesale/Retail Price Lists

Official:
https://docs.daftra.com/en/user_manual/wholesale-and-retail-price-list/

Observed:
- separate retail/wholesale pricing for the same Products.

---

# 429. Final Product / Architecture Verdict

Pricing is not "a price field on Product."

It is:

> **The canonical commercial price-decision layer of the ERP.**

The final model is:

```text
Catalog
    └── What product/UOM is this?

Directory / Sales / Purchasing Profile
    └── Who are we dealing with and what commercial context applies?

FinBase
    └── What currency/FX/precision applies?

Pricing
    ├── Which Price List?
    ├── Which version?
    ├── Which rule?
    ├── Which quantity tier?
    ├── Which UOM?
    ├── Which customer/group?
    └── What final high-precision UnitPrice?

Sales / Purchase Transaction
    └── Snapshot the agreed result

Tax
    └── Calculate tax on the commercial result

Ledger
    └── Post financial facts; never re-run Pricing
```

The Product UX promise:

> **A small company can enter "Selling Price" normally and never know there is a Pricing Engine.**

The commercial promise:

> **A large company can handle wholesale, distributor, contracts, UOMs, quantities, currencies and scheduled price changes without duplicating Product price truth.**

The correctness promise:

> **No ambiguous winner. If two rules are equally valid and contradictory, the system flags the configuration instead of guessing.**

The performance promise:

> **Pricing is resolved in batches, from immutable versioned rules, with indexed candidate data and safe caches; no N+1 on sales screens.**

---

# 430. Next File

After Pricing, the next file should be:

> **`08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Its central business question:

> **How does the ERP determine the legally correct tax treatment for a line/document based on legal entity, jurisdiction, party, product classification, date, document type and price mode — while keeping the user experience simple and preserving an immutable tax snapshot?**

Tax is high-risk and country-sensitive, so the pack must separate:

```text
Global deterministic Tax Engine
from
Country Pack tax rules/legal content
from
Compliance submission/e-invoicing
from
Ledger tax posting
```

---

## Product Owner Scope Decision — Large-Scale Performance Certification

The Product Owner explicitly defers large-scale Pricing benchmarking and certification to a dedicated Performance Hardening phase. This backend mission must still use bounded batch sizes and candidate reads, suitable indexes, and avoid obvious N+1 access. It must not claim large-scale performance certification. This decision does not defer ordinary functional, security, or correctness verification.

**END OF FILE — 07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
