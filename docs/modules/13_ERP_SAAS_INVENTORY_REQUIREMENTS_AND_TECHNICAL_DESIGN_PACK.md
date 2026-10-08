# 13 — TODIR ERP SaaS Inventory / Warehouse / Stock Movement / Valuation Requirements & Technical Design Pack

**File:** `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + INVENTORY INTEGRITY + WAREHOUSE + VALUATION + TECHNICAL BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Risk Class:** **CRITICAL**

**Depends on**
- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

---

# 0. Executive Decision

TODIR Inventory is the canonical operational truth for quantity, warehouse/location, reservation, traceability and inventory value.

```text
What stock exists?
Where exactly is it?
What is On Hand?
What is Reserved?
What is Available?
What is In Transit?
Which lot/serial owns the quantity?
Who moved it, when and why?
What did it cost?
What is the carrying value and COGS effect?
```

> **No business module directly changes stock quantity. Inventory owns every confirmed physical stock movement and every inventory valuation effect.**

Sales, Purchasing, Invoicing, POS, Manufacturing and integrations request inventory operations through `Todir.Inventory.Contracts`.

---

# 1. Product Principle — Simple Outside, Powerful Inside

TODIR Inventory must serve a shop with one store room and a distributor with many advanced warehouses using the same Core.

A small company must be able to start with:

```text
Main Warehouse
Product
Quantity
```

A large company can enable:

```text
Locations
Rooms / Zones / Aisles / Racks / Shelves / Bins
Reservations
Putaway
Picking
Replenishment
Lots
Serials
Expiry
Cycle Counts
Advanced costing
```

without needing a different product.

---

# 2. Non-Negotiable Flexibility Rule

Advanced warehouse features are capabilities, not mandatory steps.

If a capability is disabled:

- its fields disappear from normal UX;
- backend uses a safe default;
- the basic workflow remains valid;
- the company is not forced through screens it does not need.

---

# 3. One Core, No Small/Big Fork

TODIR must not implement:

```text
SimpleInventoryModule
AdvancedInventoryModule
```

as separate sources of truth.

There is one Inventory domain with optional capabilities.

---

# 4. Minimum Setup

When Inventory is activated, minimum operational setup can be:

```text
Legal Entity
Main Warehouse
Stocked Products
Opening Stock if any
```

TODIR may auto-create `Main Warehouse` and its hidden default logical Location to avoid unnecessary setup blockers.

---

# 5. Warehouse Required, Detailed Location Optional

Inventory stock must belong to a Warehouse.

Detailed location tracking is optional **per Warehouse**.

Example:

```text
Warehouse A → WarehouseOnly
Warehouse B → Locations
Warehouse C → HierarchicalLocations
```

---

# 6. Hidden Default Logical Location

When a Warehouse does not use detailed Locations:

```text
Warehouse
└── DEFAULT (hidden)
```

All stock logic continues through the same movement/balance model, while the user sees only the Warehouse.

---

# 7. Capability Presets

TODIR can offer setup presets:

```text
Simple
Standard
Advanced Warehouse
```

These are UX presets only.

Internally they map to explicit settings/capabilities.

---

# 8. Critical Integrity vs Optional Complexity

Optional complexity can be disabled.

Critical integrity cannot be silently bypassed.

Critical rules include:

```text
Tenant/Entity isolation
No direct stock mutation
No duplicate serial issue
No unexplained confirmed movement
Correct UOM conversion
No silent deletion of confirmed history
No accidental oversell beyond configured policy
Valuation and Ledger reconciliation
```

---

# 9. Central Business Question

> **How does TODIR preserve exact quantity, location, reservation, traceability and value history for every stock-affecting operation while allowing each company to enable only the warehouse complexity it needs?**

---

# 10. Inventory Owns

```text
Warehouse
Inventory Location
Stock Status
Product Inventory Profile
Stock Movement Ledger
Current Stock Balance
Reservation
Inventory Transfer
Receipt / Issue primitives
Lots
Serial Numbers
Physical Count
Inventory Adjustment
Opening Inventory
Inventory Valuation
Cost Layers / Cost State
Inventory Revaluation
Availability
Inventory Traceability
```

---

# 11. Inventory Does Not Own

```text
Product identity/UOM master   → Catalog
Sales Order                   → Sales
Purchase Order                → Purchasing
Invoice                       → Invoicing
Price List                    → Pricing
Tax rules                     → Tax
Journal Entry                 → Ledger
Party identity                → Directory
Shipping carrier              → Logistics
BOM/Production order          → Manufacturing
```

---

# 12. Catalog vs Inventory

Catalog answers:

```text
What is the product?
What is the Variant?
What is the UOM?
What is the barcode?
```

Inventory answers:

```text
How much exists?
Where?
In what state?
At what inventory cost?
```

---

# 13. No Product Stock or Inventory Cost Truth

Forbidden as canonical Catalog data:

```text
Product.stock_qty
Product.inventory_cost
```

Current stock and valuation belong Inventory.

Selling/buying commercial prices remain Pricing-owned.

---

# 14. Goods vs Stocked

`Nature = Goods` does not automatically mean stock-tracked.

Inventory profile decides:

```text
NonStocked
StockTracked
```

Services do not produce physical stock movements.

---

# 15. Product Inventory Profile

Concept:

```text
ProductInventoryProfile
{
    TenantId
    LegalEntityId
    ProductVariantId

    InventoryState
    TrackingMode
    ValuationMode
    CostingMethod
    NegativeStockPolicy

    DefaultWarehouseId?
    DefaultPutawayPolicy?
    DefaultRemovalPolicy?

    Status
    Version
}
```

---

# 16. Tracking Modes

```text
Quantity
Lot
Serial
```

Changing tracking mode after transactional history requires controlled migration.

---

# 17. Warehouse

A Warehouse is a physical/logical inventory facility owned by one Legal Entity.

```text
Warehouse
{
    Id
    TenantId
    LegalEntityId
    BranchId?

    Code
    Name
    LocationTrackingMode
    DefaultLocationId

    Status
    Version
}
```

---

# 18. Warehouse Is Not Branch

A Branch can own/use multiple Warehouses.

A central Warehouse can exist without a Branch.

---

# 19. Warehouse Lifecycle

```text
Active
Suspended
Archived
```

Warehouses with history are archived, not deleted.

---

# 20. Location Tracking Modes

```text
WarehouseOnly
Locations
HierarchicalLocations
```

No separate engine per mode.

---

# 21. Inventory Location

Locations are first-class entities, never product text.

```text
InventoryLocation
{
    Id
    WarehouseId
    ParentLocationId?

    Code
    Name
    Type
    Barcode?

    CanHoldStock
    CanReceiveInto
    CanPickFrom

    Status
}
```

---

# 22. Flexible Hierarchy

TODIR does not hardcode a mandatory depth.

A company can model:

```text
Warehouse
→ Room
→ Aisle
→ Rack
→ Shelf
→ Bin / Drawer
```

or only:

```text
Warehouse
→ Bin
```

---

# 23. Location Types

Initial vocabulary may include:

```text
Default
Zone
Room
Aisle
Rack
Shelf
Bin
Drawer
Receiving
Picking
Packing
Dispatch
Returns
QualityHold
Quarantine
Damaged
Scrap
TransitStaging
```

---

# 24. Human and Scanner Friendly Location

Example:

```text
Code: R2-B-R07-S03-B12

Path:
Main Warehouse
/ Room 2
/ Aisle B
/ Rack 07
/ Shelf 03
/ Bin 12
```

---

# 25. Product Can Exist in Multiple Locations

Example:

```text
Product P-100

Bin A = 30
Bin B = 20
Bin C = 50

Total = 100
```

No one-location-per-product restriction.

---

# 26. Stock Position Dimensions

Canonical position includes:

```text
Legal Entity
Product Variant
Warehouse
Location
Stock Status
Lot?
Serial?
```

---

# 27. Stock Status

Physical stock can exist but be unavailable.

Examples:

```text
Available
QualityHold
Quarantine
Damaged
ReturnsInspection
Blocked
ScrapPending
```

A simple company can use only `Available` and never see this complexity.

---

# 28. Quantity Concepts

TODIR keeps separate:

```text
On Hand
Reserved
Available
In Transit
Expected
Blocked/Non-available
```

These are not synonyms.

---

# 29. On Hand

Confirmed physical quantity in Inventory-controlled stock nodes.

---

# 30. Reserved

Quantity committed to demand but not yet physically issued.

Reservation does **not** reduce On Hand.

---

# 31. Available

Baseline:

```text
Available
=
Eligible On Hand
-
Active Reservations
```

with stock-status/policy filtering.

---

# 32. In Transit

Stock that has left source custody and has not yet been received at destination.

---

# 33. Expected

Planned incoming quantity from upstream documents.

Expected is not On Hand.

---

# 34. Availability Example

```text
Product X — Cairo

On Hand      100
Reserved      25
Blocked        5
Available     70
In Transit    20
Expected      50
```

---

# 35. Reservation Capability Optional

Reservation modes:

```text
Disabled
Advisory
Enforced
```

A company that does not need reservations is not forced through reservation workflows.

---

# 36. Reservation Aggregate

```text
StockReservation
{
    Id
    TenantId
    LegalEntityId

    ProductVariantId
    WarehouseId
    LocationId?
    LotId?
    SerialId?

    RequestedQuantity
    ReservedQuantity

    SourceModule
    SourceDocumentId
    SourceLineId

    Status
    ExpiresAt?
}
```

---

# 37. Reservation Lifecycle

```text
Active
PartiallyConsumed
Consumed
Released
Expired
Cancelled
```

---

# 38. Partial Reservation

If demand is 100 and availability is 60:

```text
Reserved = 60
Unfulfilled = 40
```

Sales owns backorder/business decision; Inventory owns reservation truth.

---

# 39. Reservation Concurrency

Two users cannot reserve the same last stock beyond configured policy.

Use transactional validation and fine-grained stock-position concurrency control.

---

# 40. Stock Movement Ledger

Every confirmed physical quantity change creates immutable Stock Movement entries.

> **No confirmed stock delta exists without a Movement.**

---

# 41. Inventory Operation vs Stock Movement

Separate:

```text
InventoryOperation
→ business/user operation

StockMovementEntry
→ immutable quantity effect
```

Examples of Operations:

```text
Goods Receipt
Stock Issue
Warehouse Transfer
Inventory Adjustment
Physical Count Posting
Opening Inventory
Return Receipt
Return Issue
```

---

# 42. Movement Types

```text
Receipt
Issue
TransferOut
TransferIn
ReturnIn
ReturnOut
AdjustmentIncrease
AdjustmentDecrease
Opening
Reversal
```

Manufacturing adds its own semantic source later through contracts.

---

# 43. Receipt Example

```text
Receipt RCPT-1001
Product X
Qty 10
```

Movement:

```text
Cairo / DEFAULT / Available
+10
```

---

# 44. Issue Example

```text
Issue ISS-500
Product X
Qty 3
```

Movement:

```text
Cairo / Bin A / Available
-3
```

---

# 45. Location Transfer Example

```text
Bin A -10
Bin B +10
```

inside one confirmed atomic operation.

---

# 46. Warehouse Transfer — In Transit

Shipment:

```text
Cairo Available -10
Transit          +10
```

Receipt:

```text
Transit          -10
Alex Available   +10
```

---

# 47. Confirmed Movement Immutable

Confirmed movements cannot be edited/deleted.

Correction uses:

```text
Reversal
+
Correct Movement
```

---

# 48. Movement Idempotency

Retry of source command cannot duplicate stock.

Stable source identity includes:

```text
SourceModule
SourceDocumentId
SourceLineId
Purpose
SourceVersion
```

where appropriate.

---

# 49. Current Stock Balance

`StockBalance` is a high-performance current projection.

Concept:

```text
StockBalance
{
    TenantId
    LegalEntityId
    ProductVariantId
    WarehouseId
    LocationId
    StockStatus
    LotId?
    SerialId?

    OnHandBaseQty
    ReservedBaseQty

    Version
    LastMovementSequence
}
```

---

# 50. StockBalance Is Rebuildable

Movement Ledger + reservation state remain explainable source history.

A reconciliation process verifies current projection integrity.

---

# 51. Strong Consistency

Movement commit and affected current balances update in the same transaction.

A user must not receive success and still see an old physical quantity because of avoidable projection lag.

---

# 52. Permanent History Rule

> **Every confirmed inventory operation leaves permanent, queryable and explainable history.**

---

# 53. History Is Not One Generic Table

TODIR separates:

```text
InventoryOperation
StockMovementEntry
OperationStateHistory
InventoryValuationEntry
Audit
```

Each has a different job.

---

# 54. Inventory Operation History

Answers:

```text
What operation happened?
Who initiated it?
Why?
Which source document?
What lifecycle did it pass through?
```

---

# 55. Stock Movement History

Answers:

```text
What product?
How much?
From where?
To where?
Which Lot/Serial?
At what effective time?
```

---

# 56. Operation State History

Example:

```text
09:10 Created
09:15 Submitted
09:25 Approved
09:40 Transfer Out
14:30 Destination Receipt
```

Actor/time retained.

---

# 57. Valuation History

Answers:

```text
What value was added/removed?
Which cost method/version?
Which cost layers were consumed?
What COGS/value effect resulted?
```

---

# 58. Audit History

Security/configuration/high-risk actions.

It does not replace Movement or Valuation ledgers.

---

# 59. History Queries

Must support:

```text
Product Movement History
Warehouse History
Location History
Lot History
Serial History
Document History
User Operation History
Stock As-Of
Valuation As-Of
```

---

# 60. Stock As-Of

TODIR must answer historical quantity at a date/time from movement history, not only today's balance.

---

# 61. UOM Integration

Catalog owns UOM.

Inventory normalizes quantity to Product Base UOM while preserving:

```text
EnteredQuantity
EnteredUom
ConversionFactorSnapshot
BaseQuantity
```

---

# 62. Historical UOM Safety

Future Catalog conversion changes must not rewrite old movement quantities.

---

# 63. Warehouse Receipt

Purchasing may request receipt, but Inventory owns physical receipt truth.

Purchase Order does not increase On Hand.

Supplier Invoice does not automatically increase On Hand.

---

# 64. Simple Receipt UX

```text
Product
Qty
Warehouse
[Confirm]
```

If locations/lots are disabled, no extra fields appear.

---

# 65. Advanced Receipt UX

```text
PO/Source
Scan Product
Qty
Lot/Serial
Receiving Location
Putaway Suggestion
Confirm
```

only when enabled/required.

---

# 66. Stock Issue

Inventory owns physical stock leaving.

Sales Order does not reduce On Hand.

Customer Invoice does not reduce On Hand.

---

# 67. Direct Stock Issue

Allowed by policy for:

```text
Internal consumption
Department use
Project use
Samples
Damage/Scrap
Authorized manual issue
```

with typed reason/source.

---

# 68. Returns

Customer Return physical receipt and Supplier Return physical issue are Inventory movements.

Returned stock can enter inspection/hold instead of immediately becoming Available.

---

# 69. Warehouse Transfer Modes

```text
Instant
InTransit
```

Instant for logical/nearby moves.

InTransit for journeys requiring shipment and receipt.

---

# 70. Partial Transfer Receipt

Supported:

```text
Sent       100
Received    90
In Transit  10
```

Discrepancy remains explicit.

---

# 71. Cross-Legal-Entity Movement

Not a simple Warehouse Transfer.

Intercompany workflow must create separate entity-side operations/accounting.

---

# 72. Putaway

Optional capability that chooses/suggests storage location after receipt.

If disabled, receipt goes directly to default/selected location.

---

# 73. Putaway Rules

Can consider typed data such as:

```text
Product
Category
Warehouse zone
Preferred bin
Capacity
Lot/expiry
Handling class
```

No arbitrary tenant code required.

---

# 74. Preferred Location

A product can have preferred storage/picking location per Warehouse.

It is a preference, not the only place product may exist.

---

# 75. Picking

Optional operational workflow.

If disabled, authorized Issue can confirm directly.

If enabled, system produces pick instructions.

---

# 76. Picking Example

```text
Product X — Qty 8

Take 5
Room 2 / Rack 07 / Shelf 03 / Bin 12

Take 3
Room 2 / Rack 07 / Shelf 04 / Bin 01
```

---

# 77. Removal Strategy

Potential suggestion strategies:

```text
FIFO by receipt date
FEFO by expiry
Preferred/nearest Bin
Specific Lot
Manual
```

---

# 78. Warehouse FIFO vs Cost FIFO

Do not confuse physical removal strategy with accounting valuation method.

They are separate policies.



# 79. Replenishment

Replenishment is an optional advanced warehouse capability.

Example:

```text
Picking Bin
Capacity = 20
Current = 3
Min = 5

Bulk Storage = 200
```

TODIR can suggest:

```text
Move 17
Bulk Storage
→ Picking Bin
```

---

# 80. Replenishment Disabled

No replenishment tasks/screens are shown.

Normal receipt/issue/transfer workflows continue.

---

# 81. Replenishment Policy

Potential:

```text
Min/Max
Fixed Reorder Quantity
Top-up to Capacity
Demand-driven Pick Replenishment
```

Build only what launch customers need.

---

# 82. Bin Capacity

Optional.

Can use:

```text
Max Quantity
Weight
Volume
Unit Capacity
```

only if company configures it.

No capacity setup blocker for small warehouses.

---

# 83. Barcode Scanning

Inventory APIs and UX must support:

```text
Scan Location
Scan Product
Scan Lot/Serial
Enter/Scan Qty
Confirm
```

---

# 84. Barcode Ownership

Catalog owns product barcode identity.

Inventory consumes it.

Inventory Location can own its own location barcode.

---

# 85. Scanner Workflow

Example receive:

```text
Scan Receiving Bin
Scan Product
Scan Lot
Qty
Confirm
```

Example pick:

```text
Scan Pick Location
Scan Product
Scan Serial
Confirm
```

---

# 86. Scan Validation

Backend validates current operation context.

A barcode scan cannot bypass:

- wrong Warehouse;
- invalid Lot;
- duplicate Serial;
- insufficient stock;
- unauthorized Location.

---

# 87. Mobile Warehouse UX

Design Inventory commands to be mobile/scanner friendly from Day 1 even if full dedicated handheld UI ships later.

---

# 88. Lot / Batch

A Lot represents a group of units sharing traceability identity.

```text
Lot
{
    Id
    ProductVariantId
    LotNumber
    ManufacturedDate?
    ExpiryDate?
    SupplierLotReference?
    Status
}
```

---

# 89. Lot Identity

Scope must prevent ambiguous duplicate Lot references according to product/entity policy.

---

# 90. Lot Quantity

Derived per stock position from Movement Ledger.

Do not store one global mutable `lot.quantity`.

---

# 91. Lot History

Must show:

```text
Receipt source
Supplier
Received date
Locations
Transfers
Reservations
Issues
Returns
Adjustments
Remaining quantities
```

subject to permission/source boundaries.

---

# 92. Lot Expiry

Optional per product.

Expired quantity is not silently deleted.

Availability/removal policy decides whether it can be reserved/issued.

---

# 93. FEFO

If enabled, earliest expiry is suggested first.

User override according to permissions/policy.

---

# 94. Serial Number

Represents one uniquely tracked physical unit.

```text
SerialNumber
{
    Id
    ProductVariantId
    Serial
    Status
    InitialReceiptRef
}
```

---

# 95. Serial Uniqueness

Must prevent the same serial unit from existing twice in applicable inventory scope.

---

# 96. Serial Position

At any point one active serial should resolve to one valid physical/current state.

---

# 97. Serial History

Example:

```text
Received
→ Room 1 / Bin 2
→ Transferred
→ Bin 5
→ Reserved
→ Issued
→ Customer Return
→ Quality Hold
→ Released
```

Permanent and queryable.

---

# 98. Serial Race Protection

The same Serial cannot be reserved/issued by two concurrent operations.

---

# 99. Serial Return

Returned Serial is linked to original Serial identity.

Do not create duplicate Serial identity.

---

# 100. Serial Status

Operational summary can be:

```text
OnHand
Reserved
Issued
Returned
QualityHold
Scrapped
```

but Movement history remains authority.

---

# 101. Stock Status Transition

Moving quantity from `Available` to `QualityHold` is an inventory state/location operation with history.

Do not simply mutate status on a balance row with no movement/evidence.

---

# 102. Quality Management Boundary

Full inspection/testing workflow belongs future Quality capability.

Inventory provides:

```text
Hold
Release
Disposition
```

integration points.

---

# 103. Damaged Stock

Damaged does not automatically mean quantity leaves warehouse.

It can remain On Hand but unavailable.

---

# 104. Scrap

Confirmed scrap is a physical/value Issue or status-to-scrap operation according to policy, with reason and accounting fact if valued.

---

# 105. Inventory Adjustment

An Adjustment represents a controlled correction of physical quantity/value when reality differs from system truth.

---

# 106. Adjustment Is Not Direct Edit

Wrong:

```text
balance.qty = countedQty
```

Correct:

```text
Adjustment
Expected 100
Actual 96
Difference -4
→ confirmed Movement -4
```

---

# 107. Adjustment Reasons

Typed:

```text
CountDifference
Damage
Loss
FoundStock
DataCorrection
Scrap
UomCorrection
OtherApproved
```

---

# 108. Adjustment Approval

Configurable.

Small company:

```text
Authorized user confirms directly
```

Large company:

```text
Prepare
→ Approve
→ Post
```

No mandatory approval for all companies.

---

# 109. Adjustment Value Threshold

Approval can become mandatory only above quantity/value/risk threshold.

---

# 110. Physical Count

Inventory owns physical count sessions.

---

# 111. Count Session

```text
PhysicalCount
{
    Id
    WarehouseId
    LocationScope
    ProductScope
    SnapshotAt
    CountMode
    Status
    CreatedBy
}
```

---

# 112. Count Modes

Potential:

```text
Simple Count
Full Count
Cycle Count
Blind Count
Spot Count
Lot/Serial Count
```

---

# 113. Simple Count

For small company:

```text
Select Warehouse
Enter actual quantities
Review differences
Post
```

No complex task management.

---

# 114. Full Count

Covers selected Warehouse/location scope.

---

# 115. Cycle Count

Counts subsets periodically.

Useful for high-volume warehouses.

---

# 116. Blind Count

Counter does not see expected quantity.

Optional control for larger operations.

---

# 117. Count Snapshot

Capture system expected state at count start/snapshot point.

Need strategy for movements occurring during count.

---

# 118. Count Concurrency Modes

Potential:

```text
FreezeScope
MovementCutoff
LiveCountWithReconciliation
```

Do not universally freeze company operations.

---

# 119. No "Company Must Stop for Count" Default

Critical user requirement:

> **Inventory counting must not require the entire company to stop operating unless the company explicitly chooses a freeze policy.**

---

# 120. Localized Count Freeze

If strict freeze is used, scope it narrowly:

```text
Warehouse
Location
Product subset
```

rather than whole tenant.

---

# 121. Live Count Strategy

For large companies, count can reconcile movements that occurred after snapshot using Movement sequence/time.

---

# 122. Count Variance

```text
Expected
Counted
Difference
```

stored with actor/time.

---

# 123. Recount

Policy can require recount when variance exceeds threshold.

---

# 124. Count Approval

Large variance can require approval.

---

# 125. Posting Count

Approved differences create explicit Adjustment movements.

---

# 126. Count History

Must preserve:

```text
Session
Scope
Expected snapshot
Counted quantity
Counter
Recount
Approver
Variance
Adjustment document
```

---

# 127. Opening Inventory

Used for company migration/go-live.

---

# 128. Opening Stock Is Not Fake Purchasing

Do not create fake Purchase Orders/Receipts to represent legacy opening stock unless actual historical migration requires it.

---

# 129. Opening Inventory Input

```text
Product Variant
Warehouse
Location if enabled
Stock Status
Lot/Serial if required
Quantity
Base UOM
Inventory Value / Unit Cost
Legacy Reference
```

---

# 130. Opening Inventory Batch

Import/validate before posting.

---

# 131. Opening Idempotency

Batch/row external key prevents duplicate rerun.

---

# 132. Opening Reconciliation

For perpetual valuation:

```text
Opening Inventory Value
==
Ledger Opening Inventory Asset
```

by required accounting scope.

---

# 133. Inventory Go-Live

Per Legal Entity:

```text
InventoryGoLiveDate
InitialDataStrategy
HistoricalBoundary
```

---

# 134. Pre-Go-Live Draft Corrections

Import batch can be corrected before approval/posting.

---

# 135. Post-Go-Live Opening Correction

Explicit corrective Adjustment/Revaluation.

No silent historical overwrite.

---

# 136. Inventory Valuation

Inventory owns the business calculation of stock carrying cost and issue cost.

Ledger owns accounting posting.

---

# 137. Valuation Is Separate From Pricing

Example:

```text
Inventory Cost = 700
Selling Price  = 1,000
```

Different domains.

---

# 138. Valuation Modes

Initial architecture:

```text
NonValuated
Perpetual
```

`Periodic` can be added later if required by customer/accounting model.

---

# 139. NonValuated

Track quantity only.

Useful for:

- low-value supplies;
- informational stock;
- company policy cases.

No automatic inventory asset/COGS facts.

---

# 140. Perpetual

Quantity and value update with relevant confirmed movements.

---

# 141. Costing Methods

Recommended supported foundation:

```text
MovingAverage
FIFO
```

Design seam for:

```text
StandardCost
SpecificIdentification
```

as future/market-driven capabilities.

---

# 142. Why Not Support Every Method on Day 1

Each costing method creates deep behavior in:

- backdating;
- revaluation;
- returns;
- landed cost;
- COGS;
- performance.

Build the methods actually required, correctly.

---

# 143. Costing Method Scope

Default at Legal Entity/category/profile level, resolved to Product Inventory Profile.

Final active method for a product/entity is explicit.

---

# 144. Costing Method Change

Once material valuation history exists, method change is controlled migration.

No ordinary Settings toggle.

---

# 145. Moving Average Example

```text
10 units @ 100 = 1,000
10 units @ 120 = 1,200

Qty   = 20
Value = 2,200
Average = 110
```

Issue 5:

```text
COGS = 550
Remaining Qty = 15
Remaining Value = 1,650
```

subject to exact policy/rounding.

---

# 146. FIFO Example

```text
Layer 1: 10 @ 100
Layer 2: 10 @ 120
```

Issue 12:

```text
10 @ 100
2  @ 120

Issue Value = 1,240
Remaining = 8 @ 120
```

---

# 147. Cost Layer

FIFO requires immutable/reconcilable layer state.

```text
InventoryCostLayer
{
    ProductVariantId
    CostScope
    SourceReceiptRef
    OriginalQty
    RemainingQty
    UnitCost
    Currency/BaseValue
    EffectiveDate
}
```

---

# 148. Moving Average State

Requires current valuation state per configured costing scope.

---

# 149. Costing Scope ADR

Must explicitly decide whether valuation state is per:

```text
Legal Entity + Product
```

or:

```text
Legal Entity + Warehouse + Product
```

depending accounting policy.

Recommended default for TODIR MVP:

> **Costing policy is resolved at Legal Entity + Product, with Warehouse/location quantities tracked separately, unless a business/customer requirement proves warehouse-specific valuation necessary.**

This avoids artificial profit changes from moving stock between warehouses in the same Legal Entity.

---

# 150. Same-Entity Warehouse Transfer and Cost

A transfer between Warehouses should normally carry existing cost, not create new commercial purchase cost.

---

# 151. Inventory Valuation Entry

Append-only valuation history linked to movement.

```text
InventoryValuationEntry
{
    MovementEntryId
    ProductVariantId
    QuantityEffect
    ValueEffect
    Unit/Layer Cost Context
    CostingMethod
    CostingVersion
    Currency/BaseCurrency
    SourceCostContext
}
```

---

# 152. Quantity Movement vs Value Movement

Some operations change both:

```text
Receipt
Issue
Adjustment
```

Some can change value without quantity:

```text
Revaluation
Landed Cost Adjustment
Purchase Cost Correction
```

Keep explicit.

---

# 153. Inventory Value

At an accounting scope:

```text
Inventory Value
=
sum active carrying value / layers
```

must reconcile to Ledger inventory control accounts at defined points.

---

# 154. COGS

Inventory calculates issue valuation/COGS business amount.

Ledger maps semantic role/accounts.

---

# 155. Invoice Does Not Calculate COGS

Critical.

Delivery/physical issue timing can differ from Invoice timing.

---

# 156. COGS Accounting Fact

Example semantic Fact:

```text
InventoryIssueValued
{
    ProductVariantId
    Quantity
    Value
    CostingMethod
    Source
    Dimensions
}
```

No GL Account IDs.

---

# 157. Inventory Receipt Fact

Example:

```text
InventoryReceiptValued
```

The exact debit/credit/account determination belongs Ledger.

---

# 158. Inventory Adjustment Fact

Semantic quantity/value correction.

---

# 159. Inventory Revaluation Fact

Value-only adjustment.

---

# 160. Landed Cost

Architecture supports later allocation of costs such as:

```text
Freight
Customs
Insurance
Handling
Non-recoverable tax
```

into inventory value when accounting policy requires.

---

# 161. Landed Cost Ownership

Purchasing/Logistics/Tax provide cost components.

Inventory owns valuation allocation/result.

Ledger posts the accounting Fact.

---

# 162. Landed Cost Allocation Bases

Potential:

```text
Quantity
Weight
Volume
Value
Manual Approved Allocation
```

Constrained, explainable policies.

---

# 163. Cost Correction After Receipt

Example:

```text
Receipt estimated unit cost = 100
Supplier invoice final cost = 105
```

Do not rewrite original movement.

Create valuation adjustment linked to receipt/source correction.

---

# 164. Sold Stock Cost Correction

If stock was partly issued before cost correction:

valuation logic may split adjustment between:

```text
Remaining Inventory
Prior COGS / variance
```

according to costing/accounting policy.

Must be explicit and test-heavy.

---

# 165. Backdated Movements

Backdating can change FIFO layers or Moving Average chain.

High-risk.

---

# 166. Backdating Policy

Options:

```text
Disallow before cutoff
Allow within open period
Allow with controlled recalculation
```

Never unlimited silent backdating.

---

# 167. Inventory Period Lock

Integrates with Ledger/fiscal controls.

Can prevent stock operations/revaluation before lock date.

---

# 168. Cost Recalculation

If allowed, deterministic recalculation/reposting process must:

- identify affected future valuation entries;
- recompute in order;
- preserve old evidence/version;
- generate Ledger corrections as required;
- run safely at scale.

---

# 169. No "Recalculate Everything" During User Save

Large historical revaluation/repost must use controlled background process where correctness permits.

---

# 170. Current Cost vs Historical Cost

Do not display today's average cost as if it were historical issue cost.

Movement/valuation entry preserves historical value.

---

# 171. Returns and Cost

Customer return valuation should reference original issue cost when business/accounting policy requires, not blindly current average.

---

# 172. Supplier Return Cost

Should reverse/consume appropriate carrying value according to original receipt/cost context and method.

---

# 173. Serial Cost

For serialized assets/products, future `SpecificIdentification` can use unit-level cost.

Architecture must preserve per-serial source/valuation references.

---

# 174. Lot Cost

Lot traceability and costing are separate; lot may carry cost context but costing method remains explicit.

---

# 175. Negative Stock

Negative stock is a policy, never an accidental result.

---

# 176. Negative Stock Policies

Recommended:

```text
Disallow
AllowWithWarningAndPermission
Allow
```

with strict scope.

---

# 177. Default

For normal stock-tracked goods:

```text
Disallow
```

unless company deliberately changes policy.

---

# 178. Why Negative Stock Is Dangerous

It can make:

- availability misleading;
- costing provisional;
- FIFO impossible/ambiguous;
- COGS require later correction.

---

# 179. Controlled Negative Stock

If allowed for operational necessity, system must mark:

```text
NegativeQuantityCondition
ProvisionalValuation?
RequiresReconciliation
```

and resolve when receipt arrives.

---

# 180. Negative Stock Permission

Can require special role for manual Issue beyond stock.

---

# 181. Overselling vs Negative Physical Stock

Sales may accept backorder without creating negative On Hand.

Prefer:

```text
Demand > Available
→ Backorder
```

rather than fake physical issue.

---

# 182. Availability API

Other modules call:

```text
IInventoryAvailability
```

---

# 183. Batch Availability

Mandatory:

```text
GetAvailabilityBatch(...)
```

for orders/documents with many lines.

---

# 184. Availability Request Context

Potential:

```text
LegalEntity
ProductVariant
Warehouse?
Channel?
Quantity
RequestedDate?
IncludeTransit?
IncludeExpected?
```

---

# 185. Availability Response

```text
OnHand
Reserved
Available
InTransit
Expected
CanFulfill
EarliestKnownAvailability?
Version/AsOf
```

---

# 186. Sales Does Not Read Balance Tables

Contracts only.

---

# 187. Reservation Contracts

Potential:

```text
ReserveStock
ReleaseReservation
ConsumeReservation
GetReservation
```

---

# 188. Movement Contracts

Potential:

```text
ReceiveStock
IssueStock
TransferStock
ReverseMovement
```

but public business APIs should be operation-specific and permissioned.

---

# 189. Purchasing Integration

Purchasing can:

```text
RequestReceipt
RequestSupplierReturnIssue
QueryExpectedReceipts
```

No direct Inventory writes.

---

# 190. Sales Integration

Sales/Fulfillment can:

```text
QueryAvailability
Reserve
Release
RequestIssue/Delivery
RequestReturnReceipt
```

---

# 191. Invoicing Integration

Invoicing normally does not mutate physical stock.

A "direct invoice with stock update" UX, if offered for simple businesses, must orchestrate an Inventory Issue/Receipt command explicitly rather than update stock itself.

---

# 192. Simple Direct Invoice + Stock Option

For small companies TODIR may offer:

```text
Finalize Invoice
☑ Update Stock / Deliver Now
```

Internally:

```text
Invoice Finalization
+
Inventory Issue Operation
```

via Transaction Coordinator where appropriate.

This preserves simplicity without breaking ownership.

---

# 193. Direct Purchase Invoice + Receive Option

Likewise small-company UX can offer:

```text
Record Supplier Invoice
☑ Goods Received Now
```

Internally it creates a proper Inventory Receipt.

---

# 194. No Workflow Penalty for Simple Company

The user can experience one screen/action even when backend creates separate domain operations.

---

# 195. POS Integration

POS can request immediate stock Issue per completed sale or batch policy.

Inventory remains source of truth.

---

# 196. Manufacturing Integration

Future Manufacturing requests:

```text
Material Reservation
Material Issue
Production Receipt
Scrap
Byproduct Receipt
```

Inventory owns physical movement.

---

# 197. Project/Service Consumption

Project/Field Service can request internal Issue to cost center/project.

---

# 198. Stock Ownership Future Seam

Future support can add:

```text
CompanyOwned
ConsignmentSupplierOwned
CustomerOwned
```

without redesigning movement identity.

Do not build full consignment Day 1 without need.

---

# 199. Batch/Serial Optional Per Product

Warehouse can support lot/serial globally, but user only enters lot/serial for products whose Inventory Profile requires it.

---

# 200. Location Optional Per Warehouse

Even advanced tenant can have mixed simple and advanced warehouses.

---

# 201. Reservations Optional Per Flow

A company may reserve Sales stock but not internal issues, or disable reservations completely.

---

# 202. Approvals Optional Per Risk

Receipts/transfers may post directly while high-value adjustments require approval.

---

# 203. Cost Visibility Optional By Role

Warehouse user:

```text
Qty
Location
Lot/Serial
```

Finance user:

```text
Valuation
Cost
COGS
Reconciliation
```

---

# 204. Progressive Disclosure

Normal Stock screen should not expose every internal dimension.

Small-company stock card:

```text
On Hand
Reserved (if enabled)
Available
Warehouse
```

Advanced expandable details:

```text
Locations
Lots
Serials
Valuation
Movement history
```

---

# 205. Stock Card

Product inventory view should aggregate across Warehouses with drill-down.

---

# 206. Example Stock Card

```text
Product X

Total On Hand       250
Total Reserved       40
Total Available     210

Cairo               150
Alex                 100
```

Locations shown only if enabled.

---

# 207. Warehouse Worker Home

Actions:

```text
Receive
Issue
Transfer
Pick
Count
Adjust
```

only enabled capabilities/actions visible.

---

# 208. Finance Inventory Home

```text
Inventory Value
Valuation Changes
COGS
Unposted/Failed Accounting Facts
Stock-to-GL Reconciliation
Cost Adjustments
```

---

# 209. Inventory Reconciliation to Ledger

Critical invariant for perpetual inventory:

```text
Inventory Valuation Total
==
GL Inventory Control Balance
```

for defined entity/account/scope/date.

---

# 210. Reconciliation Service

Potential:

```text
IInventoryLedgerReconciliation
```

returns:

```text
Inventory Value
GL Value
Difference
Status
Diagnostic breakdown
```

---

# 211. Difference Must Be Explainable

Diagnostics can identify:

```text
Valued movement without Ledger posting
Ledger inventory posting without matching Inventory fact
Opening mismatch
Manual control-account entry
Revaluation mismatch
```

---

# 212. Control Account Protection

Ledger should restrict unsafe manual posting to Inventory control accounts.

---

# 213. Reconciliation Timing

Run:

```text
On demand
After opening migration
Before/at period close
Nightly/monitoring as appropriate
```

---

# 214. Inventory Module Activation

Hard dependencies:

```text
Platform
Catalog
FinBase
Ledger (when perpetual valuation enabled)
```

Exact manifest follows module governance.

---

# 215. Quantity-Only Inventory

If valuation disabled, Ledger dependency can remain capability-driven rather than requiring accounting actions for every movement.

---

# 216. Module Lifecycle

```text
Entitled
Active
WindDown
ReadOnly
Disabled
```

History never deleted.

---

# 217. Deactivation

Cannot disable Inventory while dependent active business flows require stock truth.

WindDown/read-only preferred.

---

# 218. Warehouse Deactivation

No new movements.

Existing stock must be zero/transferred or explicitly governed before archive.

---

# 219. Location Deactivation

No new movement into inactive Location.

Existing quantity must be moved or policy blocks archive.

---

# 220. Lot/Serial Master Lifecycle

Cannot delete referenced traceability identity.

---

# 221. Inventory Operation Lifecycle

Recommended minimal:

```text
Draft
Confirmed
Reversed
```

Approval workflow can sit separately.

Transfer with transit has operational shipment/receipt state.

---

# 222. Approval State Separate

Do not overload operation lifecycle with every workflow state.

---

# 223. Transfer Lifecycle

Possible:

```text
Draft
Ready
Shipped
PartiallyReceived
Received
CancelledBeforeShip
Reversed/Corrected
```

exact model refined by transfer aggregate.

---

# 224. Movement Confirmation

Only confirmation creates physical Movement entries.

Draft edits do not affect On Hand.

---

# 225. Movement Numbering

Human references via Platform Numbering:

```text
RCPT-
ISS-
TRF-
ADJ-
CNT-
```

company can use simpler unified series if desired.

---

# 226. Numbering Optional UX Complexity

Small company does not configure many series unless needed; sensible defaults auto-created.

---

# 227. Inventory Operation Source

Typed:

```text
PurchaseOrder
SalesOrder
Delivery
CustomerReturn
SupplierReturn
Internal
PhysicalCount
OpeningMigration
Manufacturing
Project
API
```

---

# 228. Operation Reason

Manual operations require semantic reason.

---

# 229. Attachments

Adjustment/count/receipt can reference files/photos via Platform Files.

Examples:

```text
Damage photo
Supplier delivery note
Count sheet
Transfer proof
```

---

# 230. Notes

Separate:

```text
Internal Note
Warehouse Instruction
Audit/Reason text
```

---

# 231. Inventory Schema

Recommended:

```text
business_inventory
```

---

# 232. Core Tables

Potential:

```text
warehouse
inventory_location
inventory_profile

inventory_operation
inventory_operation_line
inventory_operation_state_history

stock_movement_entry
stock_balance

stock_reservation
stock_reservation_line

inventory_lot
inventory_serial

inventory_valuation_entry
inventory_cost_layer
inventory_cost_state

inventory_transfer
inventory_transfer_line

physical_count
physical_count_line

inventory_adjustment

opening_inventory_batch
opening_inventory_line

inventory_reconciliation_run

inventory_list_projection
```

Exact tables are implementation detail; domain boundaries/invariants are mandatory.

---

# 233. `stock_movement_entry`

Concept:

```text
id
tenant_id
legal_entity_id

operation_id
operation_line_id
sequence

product_variant_id

warehouse_id
location_id
stock_status

lot_id?
serial_id?

quantity_delta_base
base_uom_code

effective_at
confirmed_at

source_type
source_ref
reversal_of_id?

created_by
```

---

# 234. Signed Movement Quantity

Movement ledger can use signed quantity delta internally:

```text
+ receipt
- issue
```

while business operation retains semantic direction.

---

# 235. Transfer Entries

Transfer creates balanced quantity effects between stock nodes, not one mutable "from/to current location" field only.

---

# 236. `stock_balance`

Current projection keyed by stock position dimensions.

Use exact numeric quantity precision.

---

# 237. `inventory_valuation_entry`

Stores immutable value delta linked to quantity/value operation.

---

# 238. `inventory_cost_layer`

Used only for methods requiring layers.

Moving Average should not fake FIFO layers unnecessarily.

---

# 239. JSONB Usage

Use for immutable/versioned snapshots or rare provider/config metadata.

Do not hide hot quantity/location/search fields in JSONB.

---

# 240. UUIDv7

Use for IDs.

---

# 241. RLS

All tenant-owned Inventory tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 242. Runtime Role

No ownership/BYPASSRLS.

---

# 243. Tenant-Aware FKs

Prevent cross-tenant Warehouse/Product/Location references.

---

# 244. Legal Entity Scope

Warehouse and stock cannot cross Legal Entity accidentally.

---

# 245. Branch Authorization

Application permissions can restrict users to assigned Branch/Warehouses.

---

# 246. Location Authorization

Optional advanced restriction for zones/locations.

Do not require for simple companies.

---

# 247. Inventory DbContext

Only Inventory-owned tables.

No Sales/Purchasing/Ledger EF navigation.

---

# 248. Contracts Project

Use locked naming:

```text
Todir.Inventory.Contracts
```

---

# 249. Core Query Contracts

Potential:

```text
IInventoryAvailability
IInventoryHistoryQuery
IInventoryValuationQuery
IStockTraceabilityQuery
```

---

# 250. Core Command Contracts

Potential:

```text
IInventoryReceiptService
IInventoryIssueService
IInventoryTransferService
IInventoryReservationService
IInventoryAdjustmentService
```

Avoid a single God `InventoryService`.



# 251. Availability API

Representative endpoint:

```text
GET /api/v1/inventory/availability
```

but hot-path module-to-module calls should use in-process Contracts.

---

# 252. Batch Availability Contract

Mandatory for Sales/Purchasing/Planning:

```text
GetAvailabilityBatch(
    LegalEntityId,
    WarehouseScope,
    ProductVariantIds,
    QuantityContext,
    RequestedDate?
)
```

No N+1 product queries.

---

# 253. Reservation API

Representative commands:

```text
POST /api/v1/inventory/reservations
POST /api/v1/inventory/reservations/{id}/release
POST /api/v1/inventory/reservations/{id}/consume
```

---

# 254. Receipt API

```text
POST /api/v1/inventory/receipts
POST /api/v1/inventory/receipts/{id}/confirm
POST /api/v1/inventory/receipts/{id}/reverse
```

---

# 255. Issue API

```text
POST /api/v1/inventory/issues
POST /api/v1/inventory/issues/{id}/confirm
POST /api/v1/inventory/issues/{id}/reverse
```

---

# 256. Transfer API

```text
POST /api/v1/inventory/transfers
POST /api/v1/inventory/transfers/{id}/ship
POST /api/v1/inventory/transfers/{id}/receive
POST /api/v1/inventory/transfers/{id}/cancel
```

only actions valid for configured transfer mode.

---

# 257. Count API

```text
POST /api/v1/inventory/counts
POST /api/v1/inventory/counts/{id}/lines
POST /api/v1/inventory/counts/{id}/submit
POST /api/v1/inventory/counts/{id}/approve
POST /api/v1/inventory/counts/{id}/post
```

---

# 258. Adjustment API

```text
POST /api/v1/inventory/adjustments
POST /api/v1/inventory/adjustments/{id}/confirm
POST /api/v1/inventory/adjustments/{id}/reverse
```

---

# 259. History API

```text
GET /api/v1/inventory/history/products/{variantId}
GET /api/v1/inventory/history/warehouses/{warehouseId}
GET /api/v1/inventory/history/locations/{locationId}
GET /api/v1/inventory/history/lots/{lotId}
GET /api/v1/inventory/history/serials/{serialId}
```

server-paged.

---

# 260. Valuation API

Role-restricted:

```text
GET /api/v1/inventory/valuation
GET /api/v1/inventory/valuation/as-of
GET /api/v1/inventory/reconciliation
```

---

# 261. No Public `SetStock`

Forbidden:

```text
PUT /product/{id}/stock
POST /inventory/set-quantity
```

except controlled count/adjustment commands with history.

---

# 262. No Public `SetCost`

Cost changes require Receipt/Valuation/Revaluation commands.

---

# 263. Command Idempotency

All stock-changing commands require stable idempotency/source identity.

Double click/retry must not duplicate movements.

---

# 264. Error Codes

Stable examples:

```text
inventory.product_not_stocked
inventory.warehouse_not_found
inventory.warehouse_inactive
inventory.location_not_found
inventory.location_inactive
inventory.location_not_in_warehouse

inventory.insufficient_available
inventory.negative_stock_not_allowed
inventory.reservation_conflict
inventory.reservation_changed

inventory.lot_required
inventory.lot_invalid
inventory.lot_expired
inventory.serial_required
inventory.serial_duplicate
inventory.serial_not_available

inventory.uom_invalid
inventory.quantity_invalid

inventory.transfer_same_location
inventory.transfer_not_shipped
inventory.transfer_over_received

inventory.count_scope_locked
inventory.count_changed
inventory.adjustment_not_allowed

inventory.costing_not_ready
inventory.valuation_conflict
inventory.backdate_not_allowed
inventory.period_locked

inventory.idempotency_conflict
inventory.history_immutable
inventory.ledger_reconciliation_failed
```

---

# 265. Business-Friendly Error

Instead of:

```text
InventoryConcurrencyException
```

show:

> "الكمية المتاحة اتغيرت أثناء تنفيذ العملية. حدّث الرصيد وحاول مرة أخرى."

---

# 266. Insufficient Stock UX

Show:

```text
Requested: 10
Available: 6
Reserved: 4
```

and allowed next action based on workflow:

```text
Reduce quantity
Backorder
Choose another Warehouse
Request transfer
Authorized override
```

---

# 267. No Company Block From Optional Missing Setup

Examples:

- no Locations configured → use hidden Default;
- no Picking enabled → direct Issue;
- no Lot tracking on product → no Lot requirement;
- no Reservation enabled → don't require Reservation;
- no approval policy → direct confirm;
- no barcode scanner → manual Product/Location entry.

---

# 268. Required Setup Must Be Contextual

If a Product is Serial-tracked, missing Serial **is** a blocker.

If it is Quantity-tracked, Serial UI does not appear.

---

# 269. Operational Readiness

Before movement confirmation Inventory validates only relevant capabilities.

---

# 270. Warehouse Readiness

Examples:

```text
Warehouse active?
Required location available?
Required receiving/picking node configured if feature enabled?
```

---

# 271. Product Readiness

Examples:

```text
Inventory Profile active?
UOM valid?
Lot/Serial requirement satisfied?
Cost method available if valuated?
```

---

# 272. Accounting Readiness

Only required for movements with accounting/valuation consequence.

Quantity-only setup must not be blocked by irrelevant Ledger mapping.

---

# 273. Performance Is First-Class

Inventory is an append-heavy, high-volume domain.

Do not calculate current stock by summing full Movement history on every request.

---

# 274. Performance Architecture

Use:

```text
Immutable Movement Ledger
+
Transactionally maintained StockBalance
+
Valuation state/layers
+
Read projections
```

---

# 275. Availability Hot Path

Initial measured goal:

```text
p95 50–100ms
```

for normal batch availability requests under realistic load.

Not a contractual number until benchmarked.

---

# 276. Single Product Stock Card

Initial measured goal:

```text
p95 <= 100ms
```

for current balance summary with realistic indexes/cache.

---

# 277. Receipt/Issue Commit

Typical 1–20 line local operation target:

```text
p95 <= 150–250ms
```

excluding unrelated external integrations.

Correctness has priority.

---

# 278. Large Operations

500+ lines use optimized batches.

Do not perform one DB round trip per line.

---

# 279. StockBalance Index

Candidate:

```text
(tenant_id, legal_entity_id, product_variant_id, warehouse_id, location_id, stock_status)
```

with Lot/Serial variants as needed.

---

# 280. Availability Index

Hot product + warehouse + eligible status.

---

# 281. Movement Indexes

Likely:

```text
tenant/entity/product/effective_at
tenant/entity/warehouse/effective_at
tenant/entity/location/effective_at
operation_id
source_type/source_ref
lot_id/effective_at
serial_id/effective_at
```

---

# 282. Reservation Indexes

```text
tenant/entity/product/warehouse/status
source_module/source_document/source_line
expires_at where Active
```

---

# 283. Serial Index

Exact unique serial lookup must be fast.

---

# 284. Lot Index

Product + lot number + status.

---

# 285. Valuation Index

Product/cost-scope/effective sequence/date.

---

# 286. Partial Indexes

Useful for:

```text
Active Reservations
Non-zero Stock Balances
Open Transfers
Active Lots
```

based on measured query plans.

---

# 287. Keyset Pagination

Movement/history screens use stable keyset/cursor pagination.

Avoid deep OFFSET.

---

# 288. Stable History Order

Recommended:

```text
EffectiveAt
MovementSequence
Id
```

---

# 289. Inventory Sequence

Use deterministic per-entity/cost-scope ordering mechanism for valuation-sensitive movement order.

Human operation number is separate.

---

# 290. Sequence vs Timestamp

Do not rely on timestamp alone for exact movement ordering.

Concurrent operations can share time precision.

---

# 291. Costing Serialization

For the same costing scope/Product, valuation-sensitive operations may require serialized/locked sequencing.

Avoid global Inventory locks.

---

# 292. Hot SKU

Popular product can have high reservation/issue concurrency.

Need specific benchmark.

---

# 293. Hot SKU Reservation Strategy

Options include:

- row-level StockBalance lock;
- optimistic version + atomic conditional update;
- reservation counter transaction.

Select based on benchmark.

---

# 294. Atomic Conditional Availability

Example concept:

```sql
UPDATE stock_balance
SET reserved_qty = reserved_qty + @q
WHERE ...
AND available_qty >= @q
```

with correct derived/status semantics if this approach is chosen.

No database shortcut may bypass domain policy.

---

# 295. Deadlock Prevention

When operation touches multiple stock positions, lock in deterministic order:

```text
Product
Warehouse
Location
Lot/Serial
```

stable IDs.

---

# 296. Retry

Only safe transient transaction retries.

Idempotency protects duplicate effects.

---

# 297. Massive Physical Count

Count sessions can process/import large datasets in chunks before final posting.

---

# 298. Massive Opening Import

Validate asynchronously/chunked, post in controlled batches while preserving batch-level reconciliation.

---

# 299. Batch Failure

One bad import row should be diagnosable.

Do not hide behind generic "Import Failed".

---

# 300. Projection Rebuild

Admin/runbook tools:

```text
Rebuild Stock Balance
Rebuild Availability Projection
Rebuild Valuation Projection
Verify Movement/Balance
```

must never fabricate source Movement history.

---

# 301. No Redis Day 1

PostgreSQL indexes + local immutable/config cache first.

Add distributed cache only from measured need and ADR.

---

# 302. Cacheable Data

Safe examples:

```text
Warehouse configuration
Location hierarchy
Inventory Profiles
Policy/version
```

---

# 303. Do Not Cache Current Quantity as Loose Unversioned Memory Truth

Stock availability must remain transactionally current.

---

# 304. Multi-Instance Invalidation

Configuration changes emit Outbox event for local cache invalidation.

---

# 305. Inventory Events

Meaningful integration events:

```text
StockReceivedV1
StockIssuedV1
StockTransferredV1
StockTransferReceivedV1
StockAdjustedV1
ReservationCreatedV1
ReservationReleasedV1
InventoryRevaluedV1
PhysicalCountPostedV1
InventoryProfileChangedV1
```

---

# 306. Avoid Event Per Internal Cost Layer Step

Events express business facts, not every implementation detail.

---

# 307. Outbox

Integration events via Transactional Outbox.

---

# 308. Synchronous vs Async

Immediate physical/inventory invariants use synchronous transaction.

RabbitMQ never decides if stock exists.

---

# 309. Sales Reservation

Should return success/failure synchronously.

---

# 310. Sales Delivery / Issue

If order completion cannot be valid without stock Issue, coordinate transaction according to owning workflow and Transaction Coordinator.

---

# 311. Purchasing Receipt

Receipt confirmation + Inventory movement + Ledger Fact should be atomic where accounting consequence is immediate.

---

# 312. Broker Outage

Movement still commits with Outbox.

Downstream asynchronous notifications/read consumers catch up.

---

# 313. Ledger Integration

Inventory sends semantic Accounting Facts.

---

# 314. Example Fact — InventoryReceiptValued

Contains:

```text
LegalEntity
Product/Category accounting semantics
Quantity
Inventory Value
Source
Dimensions
Valuation method/version
```

---

# 315. Example Fact — InventoryIssueValued

Contains:

```text
Issue Value
COGS semantic component
Inventory carrying value decrease
Source
Dimensions
```

---

# 316. No GL IDs

Inventory does not own:

```text
InventoryAssetAccountId
COGSAccountId
StockAdjustmentAccountId
```

as business hardcoding.

Ledger Account Determination owns accounts.

---

# 317. Transaction Coordinator

Critical valued receipt/issue can atomically include:

```text
Inventory Operation
Stock Movement
StockBalance
Valuation Entry
Accounting Fact / Ledger Posting
Audit
Outbox
```

---

# 318. Ledger Failure

For a flow requiring immediate perpetual accounting:

whole operation rolls back.

No "stock moved but posting disappeared".

---

# 319. Quantity-Only Mode

If explicit non-valuated profile, no Ledger posting is expected.

This is not a failure.

---

# 320. Reversal Accounting

Inventory reversal emits corresponding semantic corrective Fact.

Ledger reverses/corrects; Inventory never edits Journal Entry.

---

# 321. Inventory vs GL Period Locks

Before confirmed valued movement:

Ledger posting date/period lock must be validated.

---

# 322. Inventory Operational Date vs Posting Date

Separate fields when needed.

Do not silently substitute today's date.

---

# 323. History Immutability and Legal Audit

Confirmed history should remain queryable after:

- Product rename;
- Warehouse rename;
- Location archive;
- User deactivation.

References/snapshots preserve context.

---

# 324. User Identity History

Store immutable ActorId/reference; display current user name plus historical identifier where needed.

---

# 325. Location Rename

Movement points to LocationId; optional code/name snapshot preserves exact historical display if required.

---

# 326. Product Rename

Movement points to VariantId; display/history can retain SKU/name snapshot to preserve old documents without duplicating Catalog truth unnecessarily.

---

# 327. Traceability Chain

For a Lot/Serial, query should traverse:

```text
Receipt source
→ Transfers
→ Locations
→ Reservations
→ Issues
→ Returns
→ Adjustments
```

---

# 328. Forward Trace

"Where did Lot L go?"

---

# 329. Backward Trace

"Which receipt/supplier/source did this issued Serial/Lot come from?"

---

# 330. Recall Readiness

Future recall feature can consume Lot traceability without redesign.

---

# 331. Expiry Reporting

Read model:

```text
Expired
Expiring in N days
Lot
Qty
Warehouse/Location
```

---

# 332. Serial Warranty Boundary

Warranty/service data belongs Sales/Service/Asset capability.

Inventory provides Serial movement history.

---

# 333. Stock Aging

Future report can use receipt/cost layer age.

Do not conflate with financial valuation method.

---

# 334. Slow Moving Inventory

Derived analytics/reporting.

Not Inventory source state.

---

# 335. Reorder Point Boundary

Basic Inventory can expose stock metrics.

Full replenishment/planning may later own:

```text
Min/Max
Reorder Point
Safety Stock
Lead Time
MRP
```

---

# 336. Basic Reorder Alert

Can be built later as read/notification feature without changing Movement core.

---

# 337. Multi-Warehouse Availability

Sales can request:

```text
Preferred Warehouse
Fallback Warehouses
All authorized Warehouses
```

Inventory returns exact availability.

Sales decides fulfillment business policy.

---

# 338. Automatic Warehouse Selection

Future Allocation/Fulfillment policy can consider:

- availability;
- customer location;
- shipping cost;
- priority;
- promised date.

Inventory supplies stock facts, not commercial routing.

---

# 339. Location Capacity Optional

Capacity data cannot block companies that never configure it.

If feature enabled, putaway validates capacity.

---

# 340. Location Restrictions Optional

Examples:

```text
Hazardous only
Cold storage
No mixed Lot
Product category
```

Advanced policy only.

---

# 341. Empty Location Search

Useful for advanced putaway.

---

# 342. Location Contents

Fast query:

```text
What is in Bin B12?
```

---

# 343. Product Locator

Fast query:

```text
Where can I find Product X?
```

Returns:

```text
Warehouse
Location path
Qty
Lot/Serial if relevant
Status
```

This directly supports the storekeeper use case.

---

# 344. Storekeeper "Find Item" UX

Search/scan SKU or barcode.

TODIR shows:

```text
Main Warehouse
Room 2
Aisle B
Rack 07
Shelf 03
Bin 12
Qty Available 5

Room 2
Aisle B
Rack 07
Shelf 04
Bin 01
Qty Available 20
```

No accounting data unless role permits.

---

# 345. Map/Floorplan

Future optional visualization.

Do not make warehouse map required for location hierarchy.

---

# 346. Location Path Generation

Can maintain Materialized Path/cache for fast display/search while parent-child relation remains controlled truth.

---

# 347. Prevent Location Cycles

DB/domain validates hierarchy cannot cycle.

---

# 348. Move Location Node

Changing parent of a Location with stock/history is controlled.

Current hierarchy can change, but historical movements retain Location ID and relevant path snapshot if exact historical layout matters.

---

# 349. Delete Location

Forbidden with history.

Archive.

---

# 350. Location Merge

Do not merge bins by rewriting movement history.

Move current stock then archive old Location.

---

# 351. Product Default Location Change

Affects future suggestions only.

Existing stock stays where it physically exists.

---

# 352. Putaway Suggestion Is Not Movement

No stock changes until user/system confirms actual movement.

---

# 353. Pick Suggestion Is Not Issue

No On Hand decrement until confirmed physical take/issue according to workflow.

---

# 354. Reservation vs Pick

Reservation = commitment.

Pick = physical preparation.

Issue = confirmed stock leaving/control transition.

Keep separate.

---

# 355. Picking Staging

Advanced warehouse can move picked stock to Packing/Dispatch location before Issue.

---

# 356. Simple Warehouse

Can skip staging completely.

---

# 357. Operation Complexity by Capability

Example simple Sale:

```text
Sales
→ Issue
```

Advanced:

```text
Sales
→ Reserve
→ Pick
→ Packing
→ Dispatch
→ Issue
```

Same Inventory truth underneath.

---

# 358. Operation Complexity Must Not Leak to Everyone

Menus/actions adapt to active Warehouse capabilities and user role.

---

# 359. Configuration Hierarchy

Resolve policy with explicit precedence, for example:

```text
Product + Warehouse override
→ Product/Category policy
→ Warehouse policy
→ Legal Entity Inventory policy
→ System safe default
```

Exact rules versioned.

---

# 360. No "Newest Rule Wins"

Ambiguous same-priority configuration blocks activation/config change.

---

# 361. Configuration Versioning

Cost/negative stock/tracking policies with financial impact are versioned/effective where required.

---

# 362. Configuration Audit

High-risk:

```text
Costing method
Negative-stock policy
Tracking mode
Valuation mode
Warehouse location mode
Inventory period lock
```

---

# 363. Activation Validation

Turning on advanced feature validates only what it needs.

Example:

Enable `HierarchicalLocations`:

- creates/ensures Default Location;
- does not force user to immediately create 500 bins;
- new movements can still use Default until company config says detailed location mandatory.

---

# 364. Location Mandatory Policy

Separate from enabling Location capability.

Possible:

```text
OptionalLocation
MandatoryForSpecificOperations
MandatoryForAllStockMovements
```

Advanced company decides.

---

# 365. Lot Mandatory

Product Inventory Profile decides.

Not warehouse-wide global requirement.

---

# 366. Serial Mandatory

Per Product Profile.

---

# 367. Reservation Mandatory

Per workflow/policy, not universal.

---

# 368. Count Approval Mandatory

Risk policy, not universal.

---

# 369. Inventory Roles / Experience

Examples:

```text
Storekeeper
Warehouse Supervisor
Inventory Controller
Inventory Accountant
Sales Viewer
Purchasing Receiver
Auditor
```

Roles are permission bundles, not hard UI personas.

---

# 370. Permissions

Initial permission vocabulary:

```text
inventory.stock.read
inventory.history.read
inventory.cost.read
inventory.valuation.read

inventory.receipt.create
inventory.receipt.confirm
inventory.receipt.reverse

inventory.issue.create
inventory.issue.confirm
inventory.issue.reverse

inventory.transfer.create
inventory.transfer.ship
inventory.transfer.receive
inventory.transfer.reverse

inventory.reservation.create
inventory.reservation.release
inventory.reservation.override

inventory.count.create
inventory.count.enter
inventory.count.approve
inventory.count.post

inventory.adjustment.create
inventory.adjustment.approve
inventory.adjustment.post

inventory.revaluation.create
inventory.revaluation.approve

inventory.warehouse.manage
inventory.location.manage
inventory.profile.manage
inventory.policy.manage

inventory.opening.manage
inventory.reconciliation.read
inventory.reconciliation.run
inventory.audit.view
```

---

# 371. Cost Visibility Permission

`inventory.cost.read` is separate.

Warehouse staff can work without seeing margins/cost.

---

# 372. Serial/Lot History Permission

Normal storekeeper can see operational trace.

Sensitive source/customer details follow cross-module permissions.

---

# 373. Adjustment Separation of Duties

High-value adjustment can require:

```text
Creator != Approver
```

configurable.

---

# 374. Physical Count Separation

Blind count may hide expected quantity from counter.

Approver can see variance.

---

# 375. Step-Up Authentication

Optional for:

- large write-down/revaluation;
- reopening locked inventory date;
- major adjustment;
- destructive configuration migration.

---

# 376. AI

AI can:

```text
Suggest putaway
Suggest replenishment
Detect unusual movement
Suggest count priorities
Explain stock difference
Suggest transfer
```

---

# 377. AI Cannot

```text
Directly change stock
Confirm high-risk adjustment
Change valuation method
Invent Lot/Serial
Bypass permissions
Edit movement history
```

AI passes through Application Commands.

---

# 378. Observability

Metrics:

```text
inventory_availability_duration
inventory_movement_commit_duration
inventory_reservation_conflict_total
inventory_negative_stock_attempt_total
inventory_transfer_in_transit_age
inventory_count_variance_total
inventory_balance_reconciliation_difference
inventory_gl_reconciliation_difference
inventory_valuation_duration
inventory_cost_recalculation_duration
```

Avoid Tenant/Product IDs as metric labels.

---

# 379. Tracing

Movement trace can include:

```text
Load profile/policy
Resolve UOM
Validate source/warehouse/location
Acquire stock locks
Validate availability
Create movement
Update balance
Value movement
Post Ledger fact
Outbox
Commit
```

---

# 380. Logging

No full inventory datasets in logs.

Use IDs/correlation.

---

# 381. Inventory Alerts

Potential:

```text
Negative stock condition
Long in-transit transfer
Count variance above threshold
Valuation/GL reconciliation difference
Expired lot
Failed accounting posting
```

---

# 382. Admin Support

Support role can inspect:

- movement correlation;
- source;
- projection integrity;
- Ledger Fact ref;
- outbox status;

without direct DB edits.

---

# 383. No Manual Database Stock Fix

Operational runbook must use supported adjustment/rebuild commands.

---

# 384. Schema Migration Safety

Inventory migrations must consider very large tables.

Use expand/migrate/contract patterns.

Avoid long table locks in production.

---

# 385. High Availability

Inventory does not depend on RabbitMQ for immediate stock correctness.

PostgreSQL transaction is critical path.

---

# 386. Broker Outage Behavior

Stock operation succeeds if its required DB/Ledger transaction succeeds; integration events remain in Outbox.

---

# 387. No External API Critical Path

Warehouse Issue/Receipt should not call carrier/provider APIs inside stock transaction.

Shipping integration occurs outside core movement transaction.

---

# 388. File/Photo Upload

Damage photo upload should not hold stock DB transaction open.

Store FileRef before/after according to workflow.

---

# 389. Import

First-class inventory imports:

```text
Opening stock
Warehouse/location master
Lot/serial opening data
Physical count lines
```

---

# 390. Generic "Import Stock Movement History"

Not default.

Historical movement migration needs controlled migration design to avoid corrupting opening truth.

---

# 391. Bulk Adjustment Import

Validate all rows and permission/risk policy.

Use batch ID/idempotency.

---

# 392. Export

Authorized exports:

```text
Stock balance
Movement history
Count variance
Valuation report
Lot/serial trace
```

server-generated/paged.

---

# 393. Reporting

Inventory reports consume read models/source ledger.

Do not introduce separate shadow inventory truth.

---

# 394. Key Reports

```text
Stock Balance
Stock by Warehouse
Stock by Location
Available vs Reserved
Movement Ledger
Stock As-Of
Inventory Valuation
Valuation As-Of
Transfer In Transit
Physical Count Variance
Lot Expiry
Serial Trace
Stock-to-GL Reconciliation
```

---

# 395. Stock Balance Report

Default simple company sees:

```text
Product
Warehouse
On Hand
Available
```

Advanced columns appear only when relevant.

---

# 396. Movement Ledger Report

Includes:

```text
Date
Operation
Product
Warehouse/Location
Qty In
Qty Out
Balance
UOM
Lot/Serial
Source
Actor
```

Cost columns permission-controlled.

---

# 397. Historical Running Quantity

Use SQL/read projection efficiently, not application loops over unbounded data.

---

# 398. Valuation Report

Includes:

```text
Product
Qty
Cost Method
Carrying Value
Warehouse breakdown
AsOfDate
```

within selected costing scope.

---

# 399. Warehouse Availability Report

Supports large Warehouse filtering and server pagination.

---

# 400. Product Locator Report

Optimized for storekeeper:

```text
Product → Locations with non-zero eligible quantity
```

---

# 401. Test Strategy — Simple Warehouse

No Locations capability.

```text
Receipt +10
Issue -3
Current 7
History exact
```

No location setup asked from user.

---

# 402. Test — Mixed Warehouse Modes

Same company:

```text
Warehouse A WarehouseOnly
Warehouse B Hierarchical
```

Both work simultaneously.

---

# 403. Test — Location Hierarchy

Create:

```text
Room → Rack → Shelf → Bin
```

Product locator returns exact path.

---

# 404. Test — Move Between Locations

Source decreases, destination increases atomically.

---

# 405. Test — Warehouse Transfer Instant

No duplicated/lost quantity.

---

# 406. Test — Warehouse Transfer In Transit

Shipment/partial receipt/current transit exact.

---

# 407. Test — Reservation

On Hand unchanged.

Reserved/Available change atomically.

---

# 408. Test — Reservation Disabled

Sales can operate through availability/issue without reservation records.

---

# 409. Test — Reservation Race

Two concurrent reservations against last stock respect policy.

---

# 410. Test — Release Reservation

Available restored; On Hand unchanged.

---

# 411. Test — Consume Reservation

Reservation decreases while physical Issue decreases On Hand.

No double subtraction from Available.

---

# 412. Test — Negative Stock Disallowed

Issue blocked.

---

# 413. Test — Negative Stock Authorized

If policy allows, condition/history/valuation handling explicit.

---

# 414. Test — UOM Conversion

Receive in Case, issue in Piece.

Base quantity exact using snapshots.

---

# 415. Test — UOM Conversion Changes Later

Historical movement unchanged.

---

# 416. Test — Lot Receipt/Issue

Lot quantities exact and traceable.

---

# 417. Test — Expired Lot

Reservation/issue follows configured block/warn policy.

---

# 418. Test — FEFO

Suggestion selects earliest expiry.

Override policy tested.

---

# 419. Test — Serial Receipt

One serial = one active unit.

---

# 420. Test — Duplicate Serial

Blocked under uniqueness policy.

---

# 421. Test — Serial Concurrent Issue

Only one succeeds.

---

# 422. Test — Serial Return

Original identity reused; history extends.

---

# 423. Test — Physical Count

Expected 100, counted 96 → posted Adjustment -4.

History preserved.

---

# 424. Test — Live Count

Movements after count snapshot handled correctly; company operations do not need global stop.

---

# 425. Test — Blind Count

Counter cannot see expected quantity.

---

# 426. Test — Opening Stock

Quantity/value/lot/serial import and idempotent rerun.

---

# 427. Test — Opening GL Reconciliation

Valuated opening equals Ledger Inventory Asset opening.

---

# 428. Test — Moving Average

Known vectors produce exact expected cost/value.

---

# 429. Test — FIFO

Known layer vectors produce exact issue and remaining values.

---

# 430. Test — Return Cost

Uses correct original/carrying context.

---

# 431. Test — Cost Correction

Receipt cost adjustment updates remaining inventory/COGS according to policy.

---

# 432. Test — Backdated Movement

Blocked or recalculated exactly based on configured policy.

---

# 433. Test — Period Lock

No unauthorized backdated stock/value changes.

---

# 434. Test — Adjustment Approval

Threshold policy enforced.

---

# 435. Test — Reversal

Original Movement remains; inverse/corrective Movement restores quantity/value.

---

# 436. Test — Double Confirm

Idempotent; one Movement set only.

---

# 437. Test — Ledger Failure

Valued stock operation transaction rolls back when immediate accounting invariant required.

---

# 438. Test — Broker Down

Movement valid + Outbox pending, no duplicated movement later.

---

# 439. Test — Product Rename

History still points correctly and old evidence remains understandable.

---

# 440. Test — Location Archive

Existing history query still works.

---

# 441. Test — Location Cycle

Hierarchy cycle rejected.

---

# 442. Test — Location With Stock Archive

Blocked until stock moved/controlled process.

---

# 443. Test — Multi-Tenant RLS

Tenant A cannot read/move/reserve Tenant B stock.

---

# 444. Test — Same Tenant, Wrong Entity

Entity A cannot use Entity B Warehouse.

---

# 445. Test — Branch Authorization

Storekeeper sees only authorized Warehouses.

---

# 446. Test — Cost Permission

Storekeeper cannot retrieve valuation/cost.

---

# 447. Test — Million Movement Dataset

Stock balance/availability/history query plans remain healthy.

---

# 448. Test — Hot SKU

Concurrent reservation/issue throughput and lock contention measured.

---

# 449. Test — 500-Line Receipt

No N+1; exact all-or-nothing operation behavior.

---

# 450. Test — Large Count Import

Chunk validation, exact posting, useful errors.



# 451. Property Test — Quantity Conservation

For an instant same-entity transfer:

```text
Total quantity before
==
Total quantity after
```

excluding explicit adjustment/scrap semantics.

---

# 452. Property Test — Reservation Conservation

For each reservation:

```text
Reserved
+
Consumed
+
Released/Expired
==
Originally Reserved
```

according to lifecycle semantics.

---

# 453. Property Test — Balance Rebuild

Rebuilt `StockBalance` equals transactionally maintained current projection.

---

# 454. Property Test — Movement Reversal

Valid Movement + full Reversal returns quantity to prior state while preserving both records.

---

# 455. Property Test — Valuation Conservation

Receipt/issue/revaluation sequences preserve exact carrying-value invariants under selected cost method.

---

# 456. Property Test — FIFO Layer Sum

```text
Sum(RemainingLayerQty)
==
Current valued quantity
```

and:

```text
Sum(RemainingLayerValue)
==
Current inventory carrying value
```

within approved decimal/rounding rules.

---

# 457. Property Test — Moving Average

Generated positive receipt/issue sequence matches independently computed reference model.

---

# 458. Property Test — Serial Uniqueness

At most one active stock position exists for a Serial at any point.

---

# 459. Property Test — As-Of Determinism

Same immutable movement/valuation history + same AsOf point → same quantity/value.

---

# 460. Architecture Test — No Direct Stock Writes

Projects outside Inventory cannot reference/write `StockBalance`/Movement implementation entities.

---

# 461. Architecture Test — No Inventory Quantity in Catalog

Catalog cannot become canonical stock owner.

---

# 462. Architecture Test — No GL Entry Construction

Inventory depends only on Ledger Contracts for Accounting Facts.

---

# 463. Architecture Test — No Sales/Purchasing DbContext Coupling

No cross-module EF navigation.

---

# 464. Architecture Test — No Random Cost Rounding

Financial rounding follows FinBase policy.

---

# 465. Architecture Test — No Arbitrary History Delete

Confirmed Movement/Valuation delete/update paths absent.

---

# 466. Red-Team — Product Stock Field

Reject:

```text
product.stock = ...
```

---

# 467. Red-Team — Invoice Directly Decrements Stock

Reject.

Invoice can orchestrate an Inventory Issue command only.

---

# 468. Red-Team — Purchase Invoice Directly Adds Stock

Reject.

---

# 469. Red-Team — Sales Order Reduces On Hand

Reject.

Reservation is separate.

---

# 470. Red-Team — Reservation Reduces On Hand

Reject.

---

# 471. Red-Team — Transfer Changes Current Warehouse Field

Reject.

Transfer is immutable stock movement between nodes.

---

# 472. Red-Team — Count Replaces Balance

Reject.

Count variance creates Adjustment movement.

---

# 473. Red-Team — Confirmed Movement Edited

Reject.

---

# 474. Red-Team — History Table With Free-Text Only

Reject.

Use typed Movement/Operation/State/Valuation history.

---

# 475. Red-Team — Location Stored as String on Product

Reject.

---

# 476. Red-Team — Locations Mandatory for Every Company

Reject.

Detailed location tracking is optional per Warehouse.

---

# 477. Red-Team — Advanced Picking Mandatory

Reject.

Simple direct Issue remains valid when capability is disabled.

---

# 478. Red-Team — Lot/Serial Fields Shown to Every User/Product

Reject.

Only relevant profiles/workflows show them.

---

# 479. Red-Team — Whole Company Stops for Physical Count

Reject as default.

Scope/freeze/live-count policies must support continued operation.

---

# 480. Red-Team — Warehouse Transfer Makes Stock Visible in Both Warehouses During Transit

Reject.

Use explicit In Transit state.

---

# 481. Red-Team — Same Serial in Two Bins

Reject.

---

# 482. Red-Team — Current Average Cost Rewrites Old COGS

Reject.

Historical valuation entry is immutable.

---

# 483. Red-Team — Costing Method Changed With One Toggle

Reject after transaction history.

---

# 484. Red-Team — Negative Stock Appears Accidentally

Reject.

Must be explicit policy.

---

# 485. Red-Team — Missing Cost Mapping Posts to Random/Suspense Account

Reject unless an explicitly governed accounting policy exists; never silent fallback.

---

# 486. Red-Team — StockBalance Becomes Only Truth

Reject.

Movement history must explain/rebuild it.

---

# 487. Red-Team — RabbitMQ Reservation

Reject.

Immediate availability/reservation invariant is DB transactional.

---

# 488. Red-Team — External Shipping API Inside Stock Transaction

Reject.

---

# 489. Red-Team — Full Movement History Loaded Into Product Screen

Reject.

Use current projections + paginated history.

---

# 490. Product Acceptance — Micro Business

Company has:

```text
1 Warehouse
No locations
No reservations
No lots
No serials
```

User can:

```text
Receive
Issue
Transfer? (if second warehouse later)
Count
Adjust
```

with simple screens.

---

# 491. Product Acceptance — Small Distributor

```text
3 Warehouses
Basic locations
Reservations optional
Barcode product scan
Moving Average
```

Works without advanced WMS setup.

---

# 492. Product Acceptance — Large Warehouse

Can configure:

```text
Room / Zone / Aisle / Rack / Shelf / Bin
Location barcodes
Putaway
Picking
Replenishment
Cycle count
Lot/Serial
```

on the same Inventory Core.

---

# 493. Product Acceptance — Mixed Complexity Company

Warehouse A simple.

Warehouse B advanced.

Users do not see irrelevant complexity when working in A.

---

# 494. Product Acceptance — Find Product

Storekeeper scans SKU and TODIR tells exact location(s) and available qty.

---

# 495. Product Acceptance — Receive

Simple user can complete in very few fields.

Advanced user can scan locations/lots/serials.

---

# 496. Product Acceptance — Issue

System prevents accidental duplicate/insufficient issue according to policy.

---

# 497. Product Acceptance — Transfer

Warehouse-to-warehouse and location-to-location history exact.

---

# 498. Product Acceptance — Count

No unexplained stock overwrite.

Variance and approval/history visible.

---

# 499. Product Acceptance — Operations Continue During Count

Configured live/scoped count does not freeze unrelated company work.

---

# 500. Product Acceptance — History

For any current quantity, authorized user can drill into movement explanation.

---

# 501. Product Acceptance — Serial Trace

User can reconstruct complete physical lifecycle.

---

# 502. Product Acceptance — Finance

Inventory valuation and GL reconciliation are explainable.

---

# 503. Product Acceptance — Sales

Batch availability is fast and never requires direct Inventory DB access.

---

# 504. Product Acceptance — Purchasing

Goods can be received independently from supplier invoice timing.

---

# 505. Product Acceptance — Direct Invoice Simplicity

Small company can use "Deliver/Update Stock now" UX while backend still creates proper Inventory Movement.

---

# 506. Product Acceptance — No Cost Leak

Warehouse worker can work normally without access to inventory cost.

---

# 507. Product Acceptance — Opening Migration

Company can go live with opening stock instead of recreating historical Purchase Orders.

---

# 508. Product Acceptance — Optional Features

Turning off an optional feature removes workflow burden without corrupting Inventory truth.

---

# 509. Implementation Order

```text
IN00 ADRs + Domain Boundaries
IN01 Schema + RLS
IN02 Warehouse + Default Logical Location
IN03 Location Hierarchy + Capability Modes
IN04 Product Inventory Profile
IN05 Stock Movement Ledger
IN06 StockBalance Projection
IN07 UOM Snapshot Integration
IN08 Receipts
IN09 Issues
IN10 Transfers + In Transit
IN11 Reservations + Concurrency
IN12 History / Traceability Queries
IN13 Lots
IN14 Serials
IN15 Stock Status / Hold / Release
IN16 Physical Count
IN17 Adjustments
IN18 Opening Inventory
IN19 Valuation Foundation
IN20 Moving Average
IN21 FIFO Layers
IN22 COGS / Ledger Accounting Facts
IN23 Inventory-to-GL Reconciliation
IN24 Cost Corrections / Revaluation
IN25 Backdating / Period Locks
IN26 Putaway / Preferred Locations
IN27 Picking
IN28 Barcode / Scanner Workflows
IN29 Replenishment Foundation
IN30 Sales / Purchasing / Invoicing Contracts
IN31 Read Models / Reports
IN32 Permissions / Audit / SoD
IN33 Performance / Concurrency / Chaos
IN34 Migration / Runbooks / Projection Rebuild
IN35 Product + Warehouse + Finance Acceptance
```

---

# 510. IN00 Exit Gate

ADRs approved:

- Inventory ownership.
- optional-capability architecture.
- hidden Default Location.
- Movement vs Balance.
- Reservation vs On Hand.
- costing scope.
- valuation/accounting boundary.
- permanent history model.

---

# 511. IN01 Exit Gate

- `business_inventory`.
- FORCE RLS.
- tenant/entity-safe FKs.
- exact decimals.
- UUIDv7.
- Testcontainers isolation.

---

# 512. IN02 Exit Gate

Inventory activation auto-supports a simple `Main Warehouse`.

Small company can transact without configuring Locations.

---

# 513. IN03 Exit Gate

Per-Warehouse modes:

```text
WarehouseOnly
Locations
HierarchicalLocations
```

work concurrently in one tenant.

---

# 514. IN04 Exit Gate

Per-Product Variant Inventory Profile controls:

- Stocked/non-stocked.
- Quantity/Lot/Serial.
- valuation.
- costing.
- negative-stock policy.

---

# 515. IN05 Exit Gate

Every confirmed quantity change creates immutable movement.

No external direct write.

---

# 516. IN06 Exit Gate

Current balance is strongly consistent and rebuildable.

---

# 517. IN07 Exit Gate

Base-UOM normalization + entered UOM/factor snapshot.

---

# 518. IN08 Exit Gate

Simple and advanced receipt workflows both operate on same movement truth.

---

# 519. IN09 Exit Gate

Issue handles:

- delivery;
- internal use;
- authorized manual issue;
- returns/scrap sources

without Invoicing/Sales owning stock.

---

# 520. IN10 Exit Gate

Instant and In-Transit transfers preserve quantity and exact history.

---

# 521. IN11 Exit Gate

Reservation modes optional; concurrency prevents accidental over-reservation.

---

# 522. IN12 Exit Gate

Queryable:

```text
Product
Warehouse
Location
Lot
Serial
Document
As-Of
```

history.

---

# 523. IN13 Exit Gate

Lot quantities/expiry/traceability exact.

---

# 524. IN14 Exit Gate

One Serial cannot exist/issue twice; full lifecycle queryable.

---

# 525. IN15 Exit Gate

Available/Hold/Damaged statuses do not require separate systems or corrupt On Hand.

---

# 526. IN16 Exit Gate

Physical Count supports simple, scoped and live-count operation.

No global company freeze requirement.

---

# 527. IN17 Exit Gate

Adjustments always leave movement/reason/approval history.

---

# 528. IN18 Exit Gate

Opening quantity/value and traceability imports are idempotent.

---

# 529. IN19 Exit Gate

Valuation entries separate quantity and value truth correctly.

---

# 530. IN20 Exit Gate

Moving Average golden vectors and property tests pass.

---

# 531. IN21 Exit Gate

FIFO layer golden vectors/property tests pass at scale.

---

# 532. IN22 Exit Gate

Inventory emits semantic Facts; Ledger creates Journal Entries.

---

# 533. IN23 Exit Gate

Inventory carrying value reconciles to GL control balance.

---

# 534. IN24 Exit Gate

Cost correction/revaluation never silently rewrites original Movement.

---

# 535. IN25 Exit Gate

Backdating/period locks deterministic and controlled.

---

# 536. IN26 Exit Gate

Putaway optional; disabled mode adds no workflow burden.

---

# 537. IN27 Exit Gate

Picking optional; Storekeeper receives clear source location instructions.

---

# 538. IN28 Exit Gate

Product/location/lot/serial scan flows validated.

---

# 539. IN29 Exit Gate

Replenishment suggestions do not mutate stock until confirmed.

---

# 540. IN30 Exit Gate

Sales/Purchasing/Invoicing/POS integrate through Contracts only.

---

# 541. IN31 Exit Gate

No N+1; Stock Balance/History/Valuation reports paginated and indexed.

---

# 542. IN32 Exit Gate

Warehouse/Cost/Adjustment/Count permissions and audit pass.

---

# 543. IN33 Exit Gate

Benchmarks include:

```text
Millions of movements
Hot SKU concurrency
500-line operations
Reservation races
Serial races
Large count
Projection rebuild
Broker outage
Ledger rollback
```

---

# 544. IN34 Exit Gate

Runbooks exist for:

```text
Projection mismatch
Valuation mismatch
Stuck transfer
Opening import failure
Cost recalculation
Rebuild
RLS incident
```

No manual SQL stock correction runbook.

---

# 545. IN35 Acceptance

Signoff required from:

```text
Product Architect
Warehouse/Inventory Lead
Senior Accountant
Backend Lead
Frontend Lead
Performance Engineer
QA/Security
```

before pilot.

---

# 546. Build Now — MUST

```text
Warehouse
Default logical Location
Optional hierarchical Locations
Product Inventory Profile
Stock Movement Ledger
StockBalance
Receipts
Issues
Transfers
In Transit
Reservations
Permanent history
UOM snapshots
Lot foundation
Serial foundation
Stock status
Physical count
Adjustments
Opening Inventory
Moving Average
FIFO
Valuation Entries
COGS/accounting facts
Inventory-to-GL reconciliation
Negative-stock policy
Backdating/locks
Basic putaway/preferred location
Basic picking/location suggestions
Barcode-ready APIs
Read models/reports
RLS/security/audit/performance
```

---

# 547. Build Later / Per Customer Need

```text
Advanced directed WMS waves
Complex bin capacity optimization
Advanced replenishment engine
Warehouse floor maps
Automated robotics/conveyors
Consignment ownership
Customer-owned inventory
Advanced quality inspection
Recall management UI
Advanced landed-cost automation
Standard Cost
Specific Identification
Advanced stock planning/MRP
Carrier/shipping execution
Cross-company/intercompany transfer automation
Complex PDC/warehouse devices
AI optimization
```

The data/contracts should leave seams for these without forcing them into MVP.

---

# 548. Product Configuration Matrix

A company should be free to choose, independently:

```text
Warehouse quantity tracking     ON
Detailed Locations              OFF/ON per Warehouse
Hierarchical Locations          OFF/ON per Warehouse
Reservations                    OFF/Advisory/Enforced
Lot Tracking                    per Product
Serial Tracking                 per Product
Expiry                          per Product/Lot
Putaway                         OFF/ON per Warehouse
Picking                         OFF/ON per Warehouse
Replenishment                   OFF/ON per Warehouse
Barcode Scanning                optional
Count Approval                  optional/risk-based
Negative Stock                  policy
Valuation                       per Product/Entity policy
Cost Method                     controlled policy
```

---

# 549. Company Freedom Rule

> **TODIR must never require an advanced warehouse workflow merely because the Inventory module is active.**

---

# 550. Integrity Rule

> **TODIR must never remove a necessary integrity check merely to make the workflow feel simple. Simplicity comes from defaults, hidden optional fields, orchestration and automation — not from corrupting stock truth.**

---

# 551. Defaulting Strategy

When optional information is absent, use explicit safe defaults such as:

```text
Default Warehouse
Default Logical Location
Available Stock Status
Default Inventory Policy
Default Costing Policy
```

Never invent Lot/Serial or financial facts.

---

# 552. Smart Defaults

Examples:

- last/preferred Warehouse for user;
- preferred Product Location;
- branch default Warehouse;
- default Receipt destination;
- default Issue source;
- automatically hidden Location field in WarehouseOnly mode.

---

# 553. User Override

Authorized users can override suggestions where policy allows.

Suggestion is not authority.

---

# 554. No Manual Re-Entry Across Modules

If Sales already knows Product/Qty/Warehouse, Inventory receives them in command.

User should not retype the same information.

---

# 555. Source-Driven Warehouse Operations

From PO:

```text
Receive
```

screen is prefilled.

From Sales Order:

```text
Pick/Deliver
```

screen is prefilled.

---

# 556. Direct Operations Still Available

Company not using Sales/Purchasing can still use Inventory independently.

---

# 557. Product Locator Is Core UX

For large warehouse use case, finding physical location must be fast and obvious.

The Inventory pack treats `Where is this item?` as a first-class business question, not a report afterthought.

---

# 558. Permanent History Is Core UX

For every operation, users with permission can open:

```text
History
```

and see its lifecycle, movements, reversal/correction and source.

---

# 559. History Table Presentation

UI may present a combined timeline, but backend sources remain typed:

```text
Operation State
Movement
Valuation
Audit
```

Do not collapse database truth into one generic table just because UI shows one timeline.

---

# 560. Current ERP Pattern Validation — Business Central

Microsoft Business Central currently supports both simple and advanced warehouse setup.

Its official documentation describes Locations as warehouses/storage places, optional Bins, Zones and advanced directed put-away/pick policies.

This validates TODIR's key product decision:

> **Warehouse complexity should be configurable per location/warehouse, rather than forced globally.**

Business Central also uses bins to suggest where items should be placed/picked and supports advanced picking based on bin policies/ranking.

---

# 561. Current ERP Pattern Validation — Odoo 20

Odoo 20 Inventory documentation describes:

- warehouses and internal locations;
- structured operation types;
- receipts;
- putaway;
- internal transfers;
- picking/delivery;
- lots/serial numbers;
- removal strategies such as FIFO/FEFO/LIFO.

This validates first-class stock locations and warehouse operation history.

TODIR intentionally keeps these capabilities optional and role-aware.

---

# 562. Current ERP Pattern Validation — ERPNext

ERPNext's current Stock Ledger documentation treats stock movements as a detailed quantity/value history linked to source vouchers and warehouses.

Its current valuation documentation supports FIFO and Moving Average and explains that backdated entries can require forward revaluation/reposting.

This validates TODIR's:

- immutable movement ledger;
- current StockBalance projection;
- explicit valuation engine;
- high-risk backdating controls.

---

# 563. Product Pattern vs TODIR Architecture

Competitor behavior is reference evidence, not source-of-truth architecture.

TODIR's decisions remain:

```text
Inventory owns physical movement
Catalog owns product/UOM
Pricing owns commercial price
Ledger owns Journal Entries
Sales/Purchasing own demand/source workflows
```

---

# 564. Source Register — Checked 2026-10-06

## Microsoft Business Central — Set Up Locations

https://learn.microsoft.com/dynamics365/business-central/inventory-how-setup-locations

Relevant patterns:

- simple and complex locations;
- optional bins;
- zones;
- transfers;
- warehouse policies.

## Microsoft Business Central — Set Up Locations to Use Bins

https://learn.microsoft.com/en-us/dynamics365/business-central/warehouse-how-to-set-up-locations-to-use-bins

Relevant patterns:

- bin functionality enabled for locations;
- default bins;
- receiving/shipping/putaway context.

## Microsoft Business Central — Pick Items for Warehouse Shipment

https://learn.microsoft.com/en-us/dynamics365/business-central/warehouse-how-to-pick-items-for-warehouse-shipment

Relevant patterns:

- pick instructions;
- bin suggestions;
- directed picking.

## Odoo 20 — Operation Types

https://www.odoo.com/documentation/20.0/applications/inventory_and_mrp/inventory/warehouses_storage/inventory_management/operation_type.html

Relevant patterns:

- receiving;
- putaway;
- internal transfers;
- picking/delivery;
- structured stock operations.

## Odoo 20 — FIFO Removal

https://www.odoo.com/documentation/20.0/applications/inventory_and_mrp/inventory/shipping_receiving/removal_strategies/fifo.html

Relevant patterns:

- location/product removal strategy;
- lot/serial-aware picking.

## ERPNext — Stock Ledger

https://docs.frappe.io/erpnext/stock-ledger

Relevant patterns:

- detailed stock-movement history;
- quantity/value;
- source voucher;
- warehouse.

## ERPNext — FIFO and Moving Average

https://docs.frappe.io/erpnext/fifo-and-moving-average

Relevant patterns:

- FIFO layers;
- Moving Average;
- perpetual valuation;
- backdated reposting implications.

These sources validate product patterns only. Accounting/statutory treatment still follows TODIR Ledger/Country Packs and qualified review.

---

# 565. Final Domain Model

```text
Catalog Product Variant
        │
        ▼
Product Inventory Profile
        │
        ├──────────────► Warehouse / Location Structure
        │
        ├──────────────► Reservation
        │
        ▼
Inventory Operation
        │
        ▼
Immutable Stock Movement Ledger
        │
        ├──────────────► StockBalance
        │                 Current fast projection
        │
        ├──────────────► Lot / Serial Traceability
        │
        └──────────────► Valuation Engine
                              │
                              ▼
                      Inventory Accounting Fact
                              │
                              ▼
                            Ledger
```

---

# 566. Simple Company Flow

```text
Receive Product X +10
        ↓
Inventory Movement +10
        ↓
On Hand = 10
```

No bin, lot, reservation or picking screen unless needed.

---

# 567. Advanced Company Flow

```text
Purchase Receipt
→ Receiving Area
→ Lot/Serial Capture
→ Putaway
→ Storage Bin
→ Reservation
→ Pick
→ Packing/Staging
→ Delivery Issue
→ COGS
```

Same core movement/valuation truth.

---

# 568. Transfer Flow

```text
Warehouse A / Bin A
        ↓ Ship
      Transit
        ↓ Receive
Warehouse B / Bin B
```

Every stage remains queryable.

---

# 569. Physical Count Flow

```text
Create Count
→ Snapshot/Scope
→ Count
→ Reconcile concurrent movements if policy
→ Variance
→ Approval if needed
→ Adjustment Movement
→ Permanent History
```

---

# 570. History Flow

```text
Operation
   │
   ├── State History
   │
   ├── Movement Entries
   │
   ├── Valuation Entries
   │
   └── Audit / Reversal links
```

---

# 571. Final Product Verdict

For a small business, TODIR Inventory should feel like:

```text
Product X
On Hand 100

Receive
Issue
Transfer
Count
```

For a large warehouse it should answer:

```text
Which room?
Which aisle?
Which rack?
Which shelf?
Which bin?
Which lot?
Which serial?
Which status?
What is reserved?
What should I pick?
Where should I put it?
Who moved it?
When?
Why?
What was the cost?
```

without changing the Inventory source-of-truth model.

---

# 572. Final Engineering Verdict

Inventory is approved only when:

```text
No module directly mutates stock.
Every confirmed quantity change has immutable Movement history.
Every current balance is explainable/rebuildable.
Detailed Locations are optional per Warehouse.
Small companies can work without WMS complexity.
Advanced warehouses can track exact Room/Aisle/Rack/Shelf/Bin.
Reservations never fake physical movement.
On Hand / Reserved / Available / In Transit remain separate.
Lot/Serial traceability is exact when enabled.
Physical Count never silently overwrites stock.
Operations can continue during scoped/live count where configured.
Inventory Cost is separate from Selling Price.
COGS/valuation are Inventory-owned; Journal Entries are Ledger-owned.
Negative stock is explicit policy.
History is permanent, typed and queryable.
Valuation reconciles to GL when perpetual accounting is enabled.
Hot-path availability/reservation works correctly under concurrency.
```

---

# 573. Final Non-Negotiable User Experience Rule

> **No company should be forced to operate like a complex warehouse if it is not one.**

And simultaneously:

> **No large company should outgrow TODIR because the simple Inventory model cannot represent its real warehouse.**

TODIR achieves both through one strong Core plus optional, progressive capabilities.

---

# 574. Next Pack

After Inventory:

> **`14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Central question:

> **How does TODIR manage the full customer sales journey from opportunity/quotation/order through pricing, credit control, reservation, delivery, invoicing, payment visibility and returns — while Sales never owns Pricing, Inventory, Invoicing, Subledger or Ledger truth?**

The Sales pack should lock:

```text
Sales Order
Quotation
Customer commercial profile
Order lifecycle
Pricing integration
Credit exposure/approval
Availability
Reservation
Fulfillment
Partial delivery
Backorder
Invoice eligibility
Returns/cancellations
Salesperson/branch/channel
Order history
Performance
Simple vs advanced sales workflows
```

---

**END OF FILE — 13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
