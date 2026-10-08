# 26 — TODIR ERP SaaS Manufacturing / MRP Requirements & Technical Design Pack

**File:** `26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + MANUFACTURING DEFINITION + MRP + PRODUCTION EXECUTION + CAPACITY + COST + PROFITABILITY + TRACEABILITY + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-07  
**Risk Class:** **CRITICAL — MATERIALS + PRODUCTION + INVENTORY + COST + MARGIN + WIP + TRACEABILITY + ACCOUNTING**

## Canonical dependencies

- `05` Catalog owns Product/Variant and UOM.
- `07` Pricing owns reusable selling/buying price logic.
- `09` Ledger owns accounting entries and WIP/variance account mapping.
- `13` Inventory owns physical stock, reservations, lots/serials and valuation.
- `14` Sales owns customer commercial demand.
- `15` Purchasing owns supplier commercial commitments and Purchase Orders.
- `17` HR owns Employee/Employment.
- `22` Fixed Assets owns company-owned machine/asset financial identity.
- `24` Service & Maintenance owns maintenance/downtime work.
- `27` Quality will own inspection/NCR/CAPA truth.
- No direct cross-module database writes.

---

# 0. Executive Decision

TODIR Manufacturing / MRP answers:

> **What should we make, how is it made, which materials and operations are required, what can be substituted, when should we make it, what is available, what is short, what capacity exists, what was actually consumed/produced, what did it cost, and what margin does that imply at the current selling price?**

It supports:

```text
Discrete Manufacturing
Process / Batch Manufacturing
BOM / Formula
Versioning
Material Substitution
Routing / Operations
Work Centers / Capacity
MRP / Planned Orders
Production Orders
Shop Floor
WIP
Scrap / Yield
Cost Roll-up
Profitability
Lots / Serials / Genealogy
Subcontracting
Co-products / By-products
```

---

# 1. Product Principle

> **Define how the product is manufactured once; reuse it automatically every time.**

For an already configured product:

```text
Product: Chair A
Quantity: 100

[Produce]
```

TODIR automatically resolves the correct production definition, components, quantities, substitutes, operations, availability and cost.

---

# 2. Absolute UX Rule

The user must **not** re-enter:

```text
wood
foam
fabric
screws
glue
```

every time they manufacture the same chair.

---

# 3. Optional Complexity, Mandatory Integrity

Small company:

```text
Product
→ Quantity
→ Produce
→ Consume Materials
→ Receive Finished Goods
→ Done
```

Advanced plant:

```text
Demand
→ MRP
→ Planned Production
→ Firm
→ Reserve Material
→ Schedule Capacity
→ Release
→ Shop Floor
→ Quality
→ Partial Output
→ WIP
→ Cost / Variance
→ Close
```

Same domain core.

---

# 4. Locked Distinctions

```text
Product != BOM
BOM != Routing
BOM Version != Product Version
Production Definition != Production Order
Planned Production Order != Production Order

Permanent Component Change != Temporary Substitute
Preferred Material != Approved Substitute

Material Requirement != Reservation
Reservation != Issue
Issue != Consumption

Expected Consumption != Actual Consumption

Work Center != Machine
Machine != Fixed Asset identity

Production Time != HR Attendance

Material Ready != Capacity Ready

Expected Yield != Actual Yield
Good Output != Scrap

Production Completed != Production Closed

Operational WIP != Finished Inventory
Manufacturing Cost != Inventory Valuation

Subcontract Operation != Purchase Order
Quality Gate != Quality Result owner
Machine Downtime != Production Operation

MRP Suggestion != Authorized Purchase
MRP Suggestion != Authorized Production
```

---

# 5. Manufacturing Modes

Foundation supports:

```text
Discrete
ProcessBatch
```

Discrete example:

```text
Chair A
= 4 legs + 1 seat + 1 back + screws + fabric + foam
```

Process example:

```text
Paint Batch 1000 L
= resin + solvent + pigment + additives
```

Mixed-mode must be possible.

---

# 6. Manufacturing Product Profile

Manufacturing settings do not belong in Catalog core.

```text
ManufacturingProductProfile
{
    ProductVariantId
    LegalEntityId
    ManufacturingEnabled
    DefaultMode
    MakeBuyPolicy
    DefaultProductionDefinitionId?
    DefaultPlantId?
}
```

Make/Buy:

```text
Make
Buy
MakeOrBuy
Subcontract
```

---

# 7. Plant

A Manufacturing Plant is production context linked to:

```text
Legal Entity
Branch/Site
Inventory Warehouses
Work Centers
Maintenance Site
```

Plant is not Warehouse.

---

# 8. Production Definition

Canonical higher-level master:

```text
ProductionDefinition
{
    Id
    LegalEntityId
    PlantId
    ProductVariantId
    ManufacturingMode
    BomOrFormulaVersionId
    RoutingVersionId?
    ValidFrom
    ValidTo?
    MinQty?
    MaxQty?
    Priority
    State
    ExpectedYield?
}
```

Purpose:

> **Select the approved production technique for a product by plant, date and quantity range.**

Never use one `Product.BomId`.

---

# 9. BOM

BOM answers:

> **Which components and quantities are normally required?**

```text
Bom
{
    Id
    ProductVariantId
    Name
}
```

Version:

```text
BomVersion
{
    Id
    BomId
    Revision
    State
    ValidFrom
    ValidTo?
    BaseOutputQty
    OutputUom
}
```

States:

```text
Draft
UnderReview
Approved
Active
Superseded
Obsolete
```

---

# 10. BOM Line

```text
BomComponentLine
{
    BomVersionId
    ComponentProductVariantId
    RequiredQty
    UomSnapshot
    ScrapFactor?
    ConsumptionMethod
    OperationRef?
    EffectiveFrom?
    EffectiveTo?
    SubstituteGroupRef?
}
```

BOM stores component/quantity/UOM, **not editable master cost**.

---

# 11. Revision Rule

Permanent material/process change:

```text
BOM v7 → Superseded
BOM v8 → Active
```

Old production history remains on v7.

Draft orders may explicitly update to latest revision.

Released/In-Progress orders never silently refresh master data.

---

# 12. Formula / Recipe

Process manufacturing uses Formula/Recipe with:

```text
Ingredients
Output basis
Expected yield
Expected loss
Co-products
By-products
Scaling policy
```

Co-product, by-product and scrap are separate facts.

---

# 13. Material Substitution — Core Rule

> **A temporary approved substitute must not require changing the reusable BOM.**

Example:

```text
Preferred: Foam A
Alternatives:
Foam B
Foam C
```

---

# 14. Approved Substitute Group

```text
MaterialSubstitutionGroup
{
    Id
    Code
    Name
    Policy
}
```

Membership:

```text
MaterialSubstitute
{
    GroupId
    ProductVariantId
    Priority
    ValidFrom
    ValidTo?
    ReplacementRatio
    Uom
    AutoApplyAllowed
    MaxCostIncreasePct?
    QualityApprovalRequired?
}
```

---

# 15. Substitute Selection

TODIR supports:

```text
By Date
During Planning
During Production
```

Preferred item normally priority 1.

---

# 16. Replacement Ratio

Never assume 1:1.

Example:

```text
Foam A = 2.0kg/chair
Foam B = 1.8kg/chair
```

MRP, cost and consumption use the chosen substitute quantity.

---

# 17. Substitute UX

Example:

```text
Foam A
Required      200 KG
Available      80 KG
Short         120 KG

Approved substitute:
Foam B
Available     500 KG

Cost impact:
+11 / chair

[Use Foam B]
[Purchase Foam A]
[Produce Partial]
```

---

# 18. Substitute Policies

Default:

```text
Auto Suggest = ON
Auto Apply   = OFF
```

Advanced Auto Apply may require:

```text
approved substitute
AND available
AND cost increase <= threshold
AND no quality/customer restriction
```

AI never autonomously substitutes a regulated/materially different component.

---

# 19. Permanent Change vs Temporary Substitute

Permanent:

```text
Foam A → Foam B from now on
```

→ New BOM revision.

Temporary:

```text
PO-100 uses Foam B because Foam A unavailable
```

→ Production-order substitution record.

---

# 20. Substitution Audit

Store:

```text
Original component
Substitute
Required/used quantities
Cost impact
Reason
Actor
Approval
Timestamp
```

Reasons:

```text
OutOfStock
LeadTime
QualityImprovement
CostOptimization
SupplierChange
EngineeringChange
CustomerApproved
OtherApproved
```

---

# 21. Product Cost Roll-Up

Core mission:

> **Show what it would cost to manufacture the product now, using current source facts, without editing the BOM whenever supplier prices change.**

Components:

```text
Material
Labor
Machine
Overhead
Subcontract
Expected Scrap/Yield
```

---

# 22. Cost Source Ownership

Material cost source:

```text
Inventory Valuation
```

via an approved cost provider.

Selling price source:

```text
Pricing
```

Manufacturing composes production economics; it does not own either master.

---

# 23. Latest Purchase Price != Inventory Cost

Locked.

Possible cost bases:

```text
Current Inventory Valuation Cost
Latest Purchase Price
Expected Purchase Cost
Standard Cost
Recent Actual Production Cost
30-Day Average Actual Production Cost
```

Each must be labeled.

---

# 24. Three Cost Truths

```text
Current Estimated Cost
Production Order Planned Cost Snapshot
Production Order Actual Cost
```

Never collapse them.

---

# 25. Current Estimated Cost

Dynamic/rebuildable and may change when:

```text
Inventory cost changes
BOM revision changes
Labor rate changes
Machine rate changes
Overhead changes
Subcontract cost changes
```

---

# 26. Planned Cost Snapshot

Captured at Release/defined planning point.

Historical order does not later adopt new master costs silently.

---

# 27. Actual Production Cost

Built from actual:

```text
Consumed materials
Actual inventory valuation
Labor time
Machine time
Subcontract cost
Scrap
Overhead
```

---

# 28. Cost Breakdown

Example:

```text
Wood        440
Foam        160
Fabric      175
Screws       20
Glue         25
Labor        80
Machine      25
Overhead     35
----------------
Unit Cost   960
```

---

# 29. Cost Change Explanation

Required:

```text
Yesterday 925
Today     960
Change    +35

Drivers:
Wood +40
Fabric -5
```

---

# 30. Reverse Component Usage Index

From a material:

```text
Wood-001

Used by:
Chair A
Chair B
Desk A
Table X
```

This is required both for cost refresh and engineering impact.

---

# 31. Incremental Cost Recalculation

When Inventory cost changes:

```text
Inventory Cost Changed
→ identify affected Production Definitions
→ invalidate/recalculate affected cost projections
```

Never recalculate the entire product catalog blindly.

---

# 32. Production Cost Projection

```text
ProductionCostProjection
{
    ProductVariantId
    ProductionDefinitionId
    CostBasis
    AsOf
    Currency
    MaterialCost
    LaborCost
    MachineCost
    OverheadCost
    SubcontractCost
    ExpectedLossCost
    TotalUnitCost
    SourceVersionHash
}
```

Rebuildable only.

---

# 33. Profitability Workspace

For a manufactured product show:

```text
Selling Price                  1,300
Current Estimated Cost           925
Estimated Profit                 375
Estimated Margin               28.85%
Estimated Markup               40.54%

Last Actual Production Cost      948
Margin at Last Actual Cost     27.08%
30-Day Avg Actual Cost           940
Margin at 30-Day Avg           27.69%
```

---

# 34. Margin vs Markup

Use Pricing definitions:

```text
Markup = (Price - Cost) / Cost
Margin = (Price - Cost) / Price
```

No ambiguous `Profit %`.

---

# 35. Margin Alerts

Company may configure:

```text
Target Margin
Minimum Margin
```

If material costs push margin below threshold:

```text
⚠ Margin Alert

Main cause:
Wood cost +22%

Actions:
Review Selling Price
Review BOM
View Approved Alternatives
Review Supplier
```

TODIR never silently changes selling price.

---

# 36. Change Impact Preview

Before permanent component change:

```text
Old material cost/unit     400
New material cost/unit     460
Difference                  +60

Current unit cost           925
New unit cost               985

Selling price             1,300
Old Margin                28.85%
New Margin                24.23%

For 1,000 units:
Additional cost           60,000
```

Required.

---

# 37. Availability Impact Preview

Also show:

```text
On Hand
Available
Reserved
Shortage
Expected receipts
Lead time
```

for old/new components.

---

# 38. Max Producible Quantity

Product workspace can calculate:

```text
Wood allows      500 chairs
Foam allows      320 chairs
Fabric allows    700 chairs
Screws allow   1,000 chairs

Bottleneck:
Foam

Max producible now:
320 chairs
```

With substitute:

```text
Using Foam B:
500 chairs
```

---

# 39. Produce-N Shortage Preview

For requested quantity:

```text
Produce 1,000 Chair A

Material      Required   Available   Short
Wood          4,000m      3,500m      500m
Foam          2,000kg       800kg    1,200kg
Fabric        1,500m      1,600m        0

Approved substitute available:
Foam B 1,500kg
```

Actions:

```text
Use Substitute
Create Planned Purchase
Produce Available Qty
```

---

# 40. Competitor Design Basis

Current mature patterns validated from:

```text
Odoo 19 Manufacturing
Microsoft Dynamics 365 Supply Chain Management
Oracle Fusion Manufacturing
SAP S/4HANA Production Planning
NetSuite Manufacturing
Acumatica Manufacturing
```

TODIR target:

> **Odoo/Acumatica-like operational simplicity + Oracle/SAP/Dynamics-grade manufacturing correctness.**



# 41. Production Demand Sources

Manufacturing demand can come from:

```text
Sales demand
Make-to-stock policy
Master Production Schedule
MRP
Manual production plan
Project demand
Service spare/subassembly replenishment where appropriate
Safety stock replenishment
```

Each source remains traceable.

---

# 42. Demand Is Not Production Order

Demand says:

> We need output.

Production Order says:

> We authorized manufacturing execution.

---

# 43. Demand Link

Production Order can carry typed source refs:

```text
SalesOrder
Forecast/MPS
StockReplenishment
Project
Manual
OtherApproved
```

---

# 44. Make To Stock

Flow:

```text
Stock / forecast need
→ Planned Production
→ Production
→ Finished Goods Stock
```

---

# 45. Make To Order

Flow:

```text
Accepted Sales demand
→ specific supply requirement
→ Production Order
→ optional order reservation/link
```

Sales remains customer-commercial owner.

---

# 46. Assemble To Order Seam

Future/optional:

```text
Configured options
→ production definition resolution
```

Do not make Day 1 configuration engine mandatory.

---

# 47. Engineer To Order Seam

Projects/Sales/Manufacturing can later orchestrate engineered production.

Engineering-change depth is optional.

---

# 48. Master Production Schedule

MPS is a planning commitment/plan:

```text
Product A
Week 1 1000
Week 2 1200
Week 3 1500
```

It is not actual production.

---

# 49. MPS Versioning

Planning changes should preserve:

```text
Version
Scenario
CreatedAt
ApprovedBy?
```

when organization uses formal planning.

---

# 50. Small Company

MPS can be hidden entirely.

---

# 51. MRP Mission

> **Turn demand into time-phased net material/supply requirements, considering stock, scheduled supply, lead times, BOM levels, substitutes and policy.**

---

# 52. MRP Inputs

Core:

```text
Demand
Active/selected Production Definition
BOM/Formula
On Hand
Available
Reserved
Scheduled Receipts
Open Purchase Orders
Open Production Orders
Transfer supply
Lead Times
Safety Stock
Order multiples/min/max
Substitutes
Calendars
```

---

# 53. Gross Requirement

Raw demand before available supply.

---

# 54. Net Requirement

After considering eligible supply.

Example:

```text
Need                400
On Hand            -250
Confirmed Incoming -100
------------------------
Net Shortage          50
```

---

# 55. Planned Supply

MRP may recommend:

```text
Planned Production
Planned Purchase
Planned Transfer
```

according to Make/Buy and supply policy.

---

# 56. Planned Supply Is Not Execution

A plan recommendation does not change stock or supplier commitment.

---

# 57. Planned Production Order

Concept:

```text
PlannedProductionOrder
{
    ProductVariantId
    PlantId
    Qty
    NeedDate
    SuggestedStart
    ProductionDefinitionId
    SourceDemandRefs
    PlanningRunId
    State
}
```

---

# 58. Planned Production States

```text
Suggested
Reviewed
Firmed
Cancelled
Superseded
```

---

# 59. Firming

```text
Planned Production
→ Production Order
```

with controlled snapshot.

---

# 60. Planned Purchase

Manufacturing/MRP recommends requirement.

Purchasing owns purchase commitment.

---

# 61. Planned Transfer

Manufacturing/MRP recommends stock movement.

Inventory owns actual transfer.

---

# 62. MRP Multi-Level Explosion

Example:

```text
Car
└── Engine
    └── Piston
        └── Raw Material
```

Demand propagates through all manufactured levels.

---

# 63. Multi-Level Readiness

TODIR must show shortage impact back to parent product.

---

# 64. Delay Propagation

If a lower-level material arrives late:

```text
Raw material late
→ Subassembly late
→ Finished product risk
→ Sales/customer-date risk
```

surface chain.

---

# 65. Phantom Assembly

Support logical grouping that explodes to components without stocking assembly as ordinary finished stock.

---

# 66. Phantom Rule

Phantom is planning/structure semantics.

Inventory stock identity remains normal Product/Variant rules if physically stocked.

---

# 67. Safety Stock

Planning input.

Inventory/Planning policy owns threshold configuration according to final architecture.

---

# 68. Minimum Order Quantity

MRP can respect:

```text
Min order
Order multiple
Max order
Lot size
```

---

# 69. Lot Sizing

Potential policies:

```text
ExactRequirement
FixedLot
MinMax
PeriodLot
EconomicLater
```

Start simple.

---

# 70. Lead Time

Separate:

```text
Purchase Lead Time
Manufacturing Lead Time
Queue Time
Setup Time
Run Time
Move Time
Safety Time
```

where required.

---

# 71. Static vs Capacity-Based Lead Time

Planned suggestion may use static lead time.

Firmed schedule can use detailed routing/capacity.

---

# 72. Need Date

Demand-required completion date.

---

# 73. Suggested Start Date

Calculated backward/forward based on planning policy.

---

# 74. Planning Calendar

Must respect:

```text
plant working days
work-center calendars
holidays
maintenance downtime
```

when detailed scheduling enabled.

---

# 75. MRP Planning Run

Concept:

```text
PlanningRun
{
    Id
    LegalEntityId
    PlantScope
    HorizonStart
    HorizonEnd
    AsOf
    PolicyVersion
    State
}
```

---

# 76. Planning Run Snapshot

Results must be traceable to:

```text
demand snapshot
inventory snapshot/watermark
definitions
lead-time policies
```

---

# 77. Replanning

New plan does not silently rewrite firmed Production/Purchase Orders.

---

# 78. Firming Fence

Optional advanced concept:

```text
within next N days
do not auto-change firmed plan
```

---

# 79. Planning Exceptions

Must show:

```text
Shortage
Late supply
Excess supply
Capacity overload
Invalid definition
Missing BOM
Missing supplier/source
Substitute used
Expired component rule
```

---

# 80. MRP UX

Planner sees exceptions first, not millions of generated lines.

---

# 81. Production Order

Canonical execution authorization.

---

# 82. Production Order Concept

```text
ProductionOrder
{
    Id
    Number
    LegalEntityId
    PlantId
    ProductVariantId

    OrderedQty
    Uom

    NeedDate
    PlannedStart
    PlannedEnd

    ProductionDefinitionSnapshotId

    State
    MaterialState
    OperationState
    QualityState
    CostState

    SourceDemandRefs
}
```

---

# 83. Production Numbering

Platform Numbering.

---

# 84. Production Order State

Recommended:

```text
Draft
Released
InProgress
Completed
Closed
Cancelled
```

Optional:

```text
OnHold
```

---

# 85. Release

Means:

> Execution is authorized.

---

# 86. Start

Means actual production activity began.

---

# 87. Completed

Means technical production output for the intended order is complete under workflow rules.

---

# 88. Closed

Means:

```text
materials resolved
outputs resolved
scrap resolved
cost resolved
variances calculated
required quality resolved
```

according to policy.

---

# 89. Mega Status Forbidden

Do not encode every dimension in one giant enum.

---

# 90. Separate Material State

Potential:

```text
NotPlanned
Short
PartiallyReady
Ready
Issued
Resolved
```

---

# 91. Operation State

Potential:

```text
NotRequired
NotStarted
InProgress
PartiallyComplete
Complete
Blocked
```

---

# 92. Cost State

Potential:

```text
Uncosted
Planned
Accumulating
ReadyToClose
Finalized
Corrected
```

---

# 93. Production Definition Snapshot

At Release capture:

```text
BOM/Formula revision
Routing revision
selected substitutes if already resolved
expected quantities
operations
resource assumptions
cost snapshot
yield assumptions
```

---

# 94. Snapshot Immutability

Master revisions can evolve.

Released order snapshot remains.

---

# 95. Change After Release

Use explicit:

```text
ProductionOrderChange
MaterialSubstitution
QtyChange
OperationChange
```

with permissions/history.

---

# 96. Order Quantity Change

Draft: simple edit.

Released/InProgress: controlled change with impact.

---

# 97. Increase Quantity Impact

Recalculate:

```text
remaining requirements
reservations
capacity
expected cost
need date
```

---

# 98. Decrease Quantity Impact

Release unused reservations/requirements safely.

Do not delete consumed history.

---

# 99. Cancel Production Order

Resolve:

```text
reservations
issued but unused material
WIP
partial output
subcontract commitments
```

before final cancellation.

---

# 100. Partial Production

Mandatory.

Example:

```text
Target     1000
Good        600
Remaining   400
```

Finished 600 may be received according to policy.

---

# 101. Production Backorder / Remainder

Remaining quantity can stay same order or create linked follow-up split according to policy.

---

# 102. Split Production Order

Support advanced:

```text
PO 1000
→ PO-A 600
→ PO-B 400
```

history linkage.

---

# 103. Merge Planned Orders

Planning-level optimization only.

Do not merge active production history blindly.

---

# 104. Material Requirement

Derived from order snapshot.

```text
ProductionMaterialRequirement
{
    ProductionOrderId
    BomLineRef
    ProductVariantId
    RequiredQty
    Uom
    NeedBy
    OperationRef?
    PreferredMaterialRef
    SubstituteGroupRef?
}
```

---

# 105. Material Requirement Is Planning Demand

No physical stock change.

---

# 106. Reservation

Inventory-owned.

Manufacturing requests.

---

# 107. Issue

Inventory-owned physical move.

---

# 108. Consumption

Business statement that material was consumed by production.

Inventory owns quantity/value movement; Manufacturing owns production context.

---

# 109. Material State Projection

```text
Required
Available
Reserved
Issued
Consumed
Returned
Short
Substituted
```

built from Inventory contracts.

---

# 110. Required != Reserved != Issued != Consumed

Absolute architecture test.

---

# 111. Issue To Production

If plant uses staging:

```text
Raw Warehouse
→ Production Staging/WIP location
```

Inventory movement.

---

# 112. Direct Consumption

Simple company can skip visible staging.

Internally Inventory still records correct movement semantics.

---

# 113. Backflush

Policy:

```text
When output reported
→ automatically calculate/confirm expected material consumption
```

---

# 114. Backflush Is Optional

Not appropriate for every industry.

---

# 115. Actual Consumption

Can differ from BOM.

Record actual.

---

# 116. Material Usage Variance

```text
Actual Qty
-
Expected Qty for actual good output
```

---

# 117. Consumption Variance Reasons

Potential:

```text
Scrap
ProcessLoss
OperatorAdjustment
Substitute
QualityIssue
MachineIssue
MeasurementVariance
OtherApproved
```

---

# 118. Unused Issued Material

Can:

```text
Return to Inventory
Carry forward to next order if controlled
Scrap
```

Never silently consumed.

---

# 119. Material Return

Inventory-owned movement linked to Production Order.

---

# 120. Material Scrap

Inventory movement/status according to Inventory.

Manufacturing records production reason/context.

---

# 121. Good Output

Inventory receipt from Production.

---

# 122. Finished Goods Receipt

Inventory owns physical receipt and valuation.

Manufacturing supplies:

```text
ProductionOrderRef
OutputProduct
Qty
Lot/Serial context
Cost source context
```

---

# 123. Output Receipt != Production Completion

Partial output supported.

---

# 124. Scrap Output

Separate from good output.

---

# 125. Rework Output

May create:

```text
Rework Production Order
```

or rework operation according to model.

---

# 126. Unbuild / Disassembly

Future/optional but architecture seam:

```text
Finished good
→ components/recovered materials
```

Inventory effects explicit.

---

# 127. Lot / Serial Input

Inventory owns lot/serial identity.

Manufacturing records which input lots were consumed.

---

# 128. Output Lot / Serial

Inventory creates/validates.

Manufacturing links to production execution.

---

# 129. Genealogy

Must answer:

```text
Which input lots produced Finished Lot FG-900?
```

and reverse:

```text
Where was Raw Lot RM-100 used?
```

---

# 130. Genealogy Is Critical For

```text
Food
Pharma
Medical
Automotive
Electronics
Chemicals
```

---

# 131. Production Genealogy Projection

Rebuildable from:

```text
Production Order
Inventory consumption movements
Inventory output receipts
Lot/Serial links
```

---

# 132. Expiry / Shelf Life

Inventory owns lot expiry.

MRP/substitution can consume shelf-life availability through contract.

---

# 133. FEFO

Inventory allocation policy.

Manufacturing does not duplicate lot allocation engine.

---

# 134. Work Center

Represents production capacity grouping.

Examples:

```text
Cutting Area
CNC Department
Paint Line
Assembly Line
Packaging Line
```

---

# 135. Work Center != Fixed Asset

Work Center can contain/use multiple assets/resources.

---

# 136. Work Center Concept

```text
WorkCenter
{
    Id
    PlantId
    Code
    Name
    CapacityModel
    CalendarRef
    DefaultEfficiency
    CostPolicyRef?
    State
}
```

---

# 137. Machine Resource

Can reference:

```text
FixedAssetId
```

if company-owned tracked asset.

Manufacturing owns production-capacity role only.

---

# 138. Machine Financial Identity

Fixed Assets-owned.

---

# 139. Machine Maintenance

Service & Maintenance-owned.

---

# 140. Machine Availability

Manufacturing consumes:

```text
planned downtime
breakdown downtime
availability
```

from Service/Maintenance where enabled.

---

# 141. Labor Resource

References HR Employee/Employment or resource group.

No duplicate employee master.

---

# 142. Production Resource

Generic concept can represent:

```text
Labor
Machine
Tool
WorkCenter capacity
External resource
```

---

# 143. Skill Requirement

Operation may require:

```text
WelderLevel2
CNCOperator
FoodSafetyCertified
```

HR provides qualification/availability.

---

# 144. Operation

Defines a manufacturing step.

Example:

```text
10 Cutting
20 Welding
30 Painting
40 Assembly
50 Packing
```

---

# 145. Routing

Ordered/graph set of Operations.

---

# 146. Routing Version

Versioned/effective-dated like BOM.

---

# 147. Operation Definition

```text
RoutingOperation
{
    Sequence
    Name
    WorkCenterRef
    SetupTime
    RunTimeModel
    QueueTime?
    MoveTime?
    OverlapPolicy?
    RequiredResources[]
    InputMaterialAssignments[]
    OutputAssignments[]
}
```

---

# 148. Setup Time

Fixed per setup/order/batch according to rule.

---

# 149. Run Time

Variable by:

```text
quantity
batch
cycle
```

---

# 150. Setup Time != Run Time

Example:

```text
Setup 30 min
Run 2 min/unit
100 units

Total = 30 + 200 = 230 min
```



# 151. Capacity

Work Center capacity may be:

```text
Hours
Units
Cycles
Batches
Parallel Resources
```

---

# 152. Capacity Calendar

Uses:

```text
working hours
shifts
holidays
planned downtime
```

without duplicating HR Attendance.

---

# 153. Effective Capacity

Concept:

```text
Calendar Capacity
× Efficiency
- Downtime
= Available Capacity
```

---

# 154. Capacity Requirement

Derived from Routing + Production Qty.

---

# 155. Capacity Conflict

Example:

```text
Available = 16h
Required  = 18h

Overload = 2h
```

Show actionable warning.

---

# 156. Material Readiness != Capacity Readiness

Order can be:

```text
Materials Ready
Capacity Blocked
```

or inverse.

---

# 157. Production Readiness

Composite, explainable:

```text
Definition Ready
Material Ready
Capacity Ready
Quality Ready
Subcontract Ready
```

---

# 158. Scheduling Levels

Simple:

```text
Planned Start / End
```

Advanced:

```text
Operation scheduling
finite capacity
dependencies
overlap
```

---

# 159. Infinite Capacity Planning

Useful baseline.

---

# 160. Finite Capacity

Advanced optional.

Do not overpromise before algorithm/performance validation.

---

# 161. Forward Scheduling

Start date fixed → calculate end.

---

# 162. Backward Scheduling

Need date fixed → calculate latest start.

---

# 163. Operation Dependencies

Default:

```text
FinishToStart
```

Advanced:

```text
overlap
lag
parallel operation
```

---

# 164. Work Center Alternative

Operation may allow:

```text
Primary Work Center
Approved Alternate Work Centers
```

---

# 165. Machine Alternative

Same principle with capacity/quality constraints.

---

# 166. Tooling

Operation may require:

```text
Mold
Die
Fixture
Special Tool
```

Tool availability seam.

Do not force full Tool Management Day 1.

---

# 167. Shop Floor Product Goal

Worker sees only executable work.

---

# 168. Shop Floor Queue

Example:

```text
My Work Center

PO-1024
Chair A
Assembly
Qty Remaining 40

PO-1030
Desk B
Assembly
Qty Remaining 10
```

---

# 169. Worker Actions

Minimal:

```text
Start
Pause
Report Good Qty
Report Scrap
Use Material
Report Problem
Complete Operation
```

---

# 170. Shop Floor Must Not Be ERP Desktop Shrunk

Absolute.

---

# 171. Barcode / QR

Can open:

```text
Production Order
Operation
Material
Lot
Output
```

through existing barcode foundations.

---

# 172. Operator Time

Production execution time.

Not HR Attendance.

---

# 173. Labor Cost Rate

Privacy-safe rate/fact.

No salary exposure to production supervisor by default.

---

# 174. Labor Setup Cost

Can be:

```text
Setup time × resource count × rate
```

---

# 175. Labor Run Cost

Can be:

```text
Actual run hours × rate
```

or approved rate model.

---

# 176. Machine Cost

Potential:

```text
machine run hours × machine cost rate
```

---

# 177. Overhead

Potential:

```text
per hour
per unit
per batch
percentage
fixed operation charge
```

Constrained rule model.

---

# 178. Cost Template

Optional reusable:

```text
ManufacturingCostTemplate
```

for labor/machine/overhead classes.

---

# 179. No Arbitrary Formula Code

No tenant C#/SQL/JS.

Use typed formulas.

---

# 180. Production Execution Event

Potential:

```text
OperationStarted
OperationPaused
OperationCompleted
GoodQtyReported
ScrapReported
MaterialConsumed
OutputReceived
```

---

# 181. Pause Reasons

Typed:

```text
MaterialShortage
MachineBreakdown
QualityHold
OperatorUnavailable
WaitingInspection
PlannedBreak
Other
```

---

# 182. Machine Breakdown

Can create/reference Service Maintenance request/work order.

Manufacturing order remains linked.

---

# 183. Quality Hold

Future `27` can block:

```text
material
operation
output
lot
```

according to Quality policy.

---

# 184. Quality Gate

Manufacturing defines where inspection is required.

Quality owns plan/result/NCR.

---

# 185. Operation Completion Gate

Can require:

```text
QualityPass
MeasurementRecorded
SupervisorApproval
```

only when configured.

---

# 186. Simple Company

No quality gate screens if Quality module inactive.

---

# 187. Scrap

Typed manufacturing fact linked to Inventory movement.

---

# 188. Scrap Categories

Potential:

```text
MaterialScrap
ProcessScrap
RejectedOutput
SetupScrap
QualityReject
NormalLoss
```

---

# 189. Scrap Reason

Required above configurable threshold.

---

# 190. Scrap Cost

Derived from canonical consumed/output valuation.

---

# 191. Scrap Rate

```text
Scrap Qty / Relevant Input or Output basis
```

definition must be explicit.

---

# 192. Expected Scrap

BOM/operation can carry normal expected factor.

---

# 193. Abnormal Scrap

Difference beyond expected.

---

# 194. Yield Variance

Required for process/batch and useful in discrete.

---

# 195. Rework

May use:

```text
Rework Operation
Rework Production Order
```

depending process.

---

# 196. Rework Is Not New Good Production Automatically

Trace original rejected output.

---

# 197. WIP Mission

> **Explain value/quantity currently committed to production but not yet finalized as finished goods.**

---

# 198. Operational WIP

Manufacturing owns order/operation progress.

---

# 199. Inventory WIP Movement

Inventory owns physical movements/valuation.

---

# 200. Accounting WIP

Ledger owns postings/account mapping.

---

# 201. WIP Stages

Potential:

```text
Material Issued
Operation In Progress
Partial Output
Awaiting Quality
Awaiting Close
```

---

# 202. WIP Cost Accumulation

Production Order cost accumulates from source facts.

---

# 203. WIP Cost Categories

```text
Material
Labor
Machine
Overhead
Subcontract
```

---

# 204. Production Order Planned Cost

At release snapshot:

```text
Planned Material
Planned Labor
Planned Machine
Planned Overhead
Planned Subcontract
```

---

# 205. Actual Cost Accumulation

As execution happens.

---

# 206. Cost Variance

Required categories:

```text
Material Usage
Material Price/Valuation
Labor Efficiency
Labor Rate
Machine Efficiency
Overhead
Scrap/Yield
Subcontract
Other
```

Do not show only one unexplained variance number.

---

# 207. Material Usage Variance

Actual qty vs standard/planned qty for actual output.

---

# 208. Material Price/Valuation Variance

Cost basis difference.

Exact accounting treatment depends costing policy.

---

# 209. Labor Efficiency Variance

Actual time vs expected time.

---

# 210. Machine Efficiency Variance

Actual machine hours/cycles vs expected.

---

# 211. Yield Variance

Input/output performance.

---

# 212. Overhead Variance

Applied vs actual/expected according to cost policy.

---

# 213. Close Production Order

Before close:

```text
all material issues resolved
unused material returned or explained
required outputs received
scrap resolved
required quality gates resolved
operations resolved
subcontract receipts resolved
costs complete enough per policy
```

---

# 214. Cost Close

Separate dimension if accounting requires.

---

# 215. Reopen Closed Production Order

High-impact controlled.

Prefer correction/rework over reopening.

---

# 216. Cost Correction After Close

Use semantic adjustment/revaluation/correction through owning domains.

No silent rewrite.

---

# 217. Inventory Valuation Boundary

`13 Inventory` remains canonical for:

```text
Moving Average
FIFO
Cost layers
Valuation entries
Landed cost
Purchase cost correction
```

Manufacturing consumes those values.

---

# 218. Manufacturing Cost Does Not Become Inventory Valuation Engine

Absolute.

---

# 219. Finished Goods Valuation

Manufacturing supplies accumulated production cost context.

Inventory applies configured valuation policy.

---

# 220. Standard Cost Seam

If Inventory later enables StandardCost, Manufacturing can supply standard roll-up inputs.

---

# 221. FIFO/Moving Average

Manufacturing actual material cost must consume actual Inventory issue valuation.

---

# 222. Landed Cost

If raw-material valuation increases due to freight/customs, current manufacturing estimate updates from Inventory cost.

---

# 223. Purchase Price Correction

Supplier final invoice may adjust Inventory valuation.

Manufacturing current cost projection updates; old closed production history remains historical.

---

# 224. Historical Profitability

Do not recompute old actual production margin using today's cost.

---

# 225. Current Product Economics

Can use current cost + current selling price.

Clearly labeled.

---

# 226. Actual Production Economics

Uses historical actual production cost.

---

# 227. Profitability Basis Label

Every profitability number shows:

```text
Selling price basis
Cost basis
Currency
AsOf
Production definition
```

---

# 228. Product Economics Workspace

Suggested tabs:

```text
Current Cost
Components
Alternatives
Production History
Margin
What Changed
Used By / Where Used
```

---

# 229. What Changed

Show:

```text
BOM revision
component substitution
component cost
labor rate
machine rate
overhead
selling price
```

with dates.

---

# 230. Cost Trend

Optional projection:

```text
7d / 30d / 90d estimated cost
actual production cost trend
```

---

# 231. Margin Trend

Useful management view.

Not accounting P&L.

---

# 232. Production Definition Cost Simulation

Before activating revision:

```text
Simulate 1
Simulate 100
Simulate 1000
```

---

# 233. Quantity-Dependent Setup Cost

Simulation must allocate fixed setup cost by batch/order quantity correctly.

---

# 234. Cost Per Unit

Not always linear.

---

# 235. Batch Size Economics

Large batch can reduce setup cost/unit.

Show when relevant.

---

# 236. Minimum Economic Run

Optional analytic.

Not Day 1 control.

---

# 237. Subassembly Cost Roll-Up

Multi-level cost:

```text
Raw Material
→ Subassembly
→ Finished Good
```

must avoid double counting.

---

# 238. Cost Explosion

Can show:

```text
Chair A
├ Seat Assembly 200
│ ├ Wood 120
│ └ Foam 80
├ Fabric 175
└ ...
```

---

# 239. Circular BOM

Forbidden.

---

# 240. BOM Cycle Detection

Architecture/test mandatory.

---

# 241. Deep BOM

Support practical multi-level depth without recursive N+1.

---

# 242. BOM Explosion Performance

Use set-based/CTE/materialized planning structures as measured.

---

# 243. Revision Selection

ProductionDefinition resolution uses:

```text
Legal Entity
Plant
Product
Date
Quantity
State
Priority
```

deterministically.

---

# 244. Ambiguous Production Definition

Block with clear configuration error.

Never choose randomly.

---

# 245. Missing Production Definition

Show:

```text
Product is manufacturable but no active production definition applies.
```

Action:

```text
Create/activate definition
```

---

# 246. Process Batch Scaling

For formula basis:

```text
Base Batch = 1000 L
Requested = 5000 L
```

scale according to formula policy.

---

# 247. Minimum/Maximum Batch

Can constrain process equipment.

---

# 248. Batch Rounding

Explicit policy.

No silent rounding.

---

# 249. Fixed Ingredients

Some ingredients may not scale linearly.

Support typed policy later.

---

# 250. Potency / Concentration

Advanced process-manufacturing capability.

Architecture seam only until market evidence.

---

# 251. Catch Weight

Advanced.

Not Day 1 unless target industry needs it.

---

# 252. Shelf-Life Planning

Planning can use Inventory lot expiry through contract.

---

# 253. Batch Attributes

Quality/Inventory ownership depending attribute.

Manufacturing references.

---

# 254. Subcontract Manufacturing

First-class manufacturing mode/operation.

---

# 255. Subcontract Types

Support:

```text
Entire Product Outsourced
Specific Operation Outsourced
Company Supplies Components
Supplier Supplies Components
Dropship Components To Subcontractor
```

---

# 256. Subcontract Scope

Manufacturing owns:

```text
what production/operation is outsourced
expected output
required component responsibility
technical definition
```

---

# 257. Purchasing Boundary

Purchasing owns:

```text
supplier
Purchase Order
commercial price
commitment
```

---

# 258. Inventory Boundary

Inventory owns:

```text
components sent
subcontractor location stock
finished goods receipt
returns
```

---

# 259. Supplier Invoice

Invoicing/AP owns final supplier invoice.

---

# 260. Subcontract Cost

Manufacturing actual cost consumes supplier source cost allocation.

---

# 261. No Mini Purchase Order Inside Manufacturing

Absolute.

---

# 262. Subcontract Lead Time

Feeds planning.

---

# 263. Subcontract Quality

Quality module can inspect received subcontracted output.

---

# 264. Subcontract Traceability

Track components sent and finished outputs when company supplies material.

---

# 265. External Operation

Routing Operation can be:

```text
Internal
Subcontract
```

---

# 266. Outsourced Operation PO Trigger

Manufacturing request:

```text
Need external painting for PO-100
```

Purchasing creates/owns procurement.

---

# 267. Production Order ↔ Purchase Order Link

Typed external reference only.

---

# 268. Maintenance Integration

Work Center/Machine availability consumes `24`.

---

# 269. Planned Maintenance

Capacity schedule should optionally exclude known planned maintenance.

---

# 270. Breakdown

When machine breaks during operation:

```text
Pause operation
→ Maintenance request/work order
→ Reschedule/alternate resource
```

---

# 271. Manufacturing Never Closes Maintenance Work

Boundary.

---

# 272. OEE

Overall Equipment Effectiveness can be future/reporting capability.

Needs precise definitions/data quality.

---

# 273. OEE Components

Typical:

```text
Availability
Performance
Quality
```

Do not display without documented basis.

---

# 274. Quality Integration

Manufacturing can require:

```text
Incoming material quality release
In-process inspection
Final inspection
```

Quality owns inspection records.

---

# 275. Quality Hold

Manufacturing sees hold status but cannot bypass without quality permission/workflow.

---

# 276. NCR

Future `27` owns.

---

# 277. Quality Rework

Quality disposition can request Manufacturing rework.

---

# 278. Quality Scrap

Quality approves disposition; Inventory/Manufacturing execute relevant scrap movement/context.

---

# 279. Sales Integration

Sales demand can create production requirement.

---

# 280. Manufacturing Does Not Own Customer Promise

Sales owns customer commercial promise.

Manufacturing supplies availability/date/capacity insights.

---

# 281. Available-To-Promise Seam

Future ATP/CTP may combine:

```text
On Hand
Scheduled Supply
Production Capacity
Lead Time
```

---

# 282. Purchasing Integration

MRP can issue:

```text
Planned Purchase Requirement
```

Purchasing converts to procurement.

---

# 283. Supplier Price

Purchasing/Pricing source.

Not BOM field.

---

# 284. Supplier Lead Time

Planning consumes supplier context.

---

# 285. Purchase Order Change

MRP plan refresh can respond.

It cannot mutate approved PO silently.

---

# 286. Inventory Integration

Mandatory contracts:

```text
GetAvailability
Reserve
ReleaseReservation
IssueToProduction
Consume
ReturnUnused
ReceiveProductionOutput
Scrap
GetIssueValuation
GetLotSerial
GetExpectedReceipts
```

exact names implementation-defined.

---

# 287. Ledger Integration

Manufacturing emits semantic facts, e.g.:

```text
ProductionStarted
MaterialConsumedForProduction
ProductionOutputCompleted
ManufacturingVarianceFinalized
WipClosed
```

No GL IDs.

---

# 288. Accounting Post Timing

Depends approved accounting policy and Inventory/Ledger orchestration.

---

# 289. WIP Accounting

Ledger owns debit/credit mapping.

---

# 290. Manufacturing Variance Accounting

Ledger owns roles/accounts.

---

# 291. HR Integration

Consume:

```text
Employee
Availability
Skill/Qualification
```

only as needed.

---

# 292. Payroll Boundary

Production labor cost rate is not employee salary.

---

# 293. Privacy

Shop-floor supervisors do not see payroll salary.

---

# 294. Fixed Assets Integration

Machine resource may reference Fixed Asset.

---

# 295. Asset Depreciation

Not Manufacturing cost source by direct DB access.

If overhead policy includes depreciation, use approved accounting/cost-provider abstraction.

---

# 296. Energy Cost

Can be overhead/resource cost if business needs.

---

# 297. Projects Integration

Project-specific manufacturing can link:

```text
ProjectId
TaskId?
```

for cost/demand context.

---

# 298. Project Cost Ownership

Avoid duplicate production/project cost.

Manufacturing source facts can feed Projects.

---

# 299. Product Change Management

Manufacturing revision management supports controlled changes.

---

# 300. Full PLM

Not required Day 1.

Possible future separate capability if evidence.



# 301. Data Model — Recommended Schema

```text
business_manufacturing
```

---

# 302. ManufacturingDbContext

Owns Manufacturing tables only.

---

# 303. Core Tables — Product / Definitions

Potential:

```text
manufacturing_product_profile
plant

production_definition
production_definition_snapshot

bom
bom_version
bom_component_line

formula
formula_version
formula_ingredient_line
formula_output_line

routing
routing_version
routing_operation

material_substitution_group
material_substitute
```

---

# 304. Core Tables — Planning

Potential:

```text
planning_run
planning_demand
planned_production_order
planned_purchase_requirement
planned_transfer_requirement
planning_exception
mrp_pegging_link
mps_version
mps_line
```

---

# 305. Core Tables — Execution

Potential:

```text
production_order
production_order_change
production_material_requirement
production_material_substitution
production_operation
production_operation_resource
production_output
production_scrap
production_pause
production_external_reference
```

---

# 306. Core Tables — Resource / Capacity

Potential:

```text
work_center
work_center_calendar
work_center_capacity
manufacturing_resource
resource_assignment
capacity_requirement
```

---

# 307. Core Tables — Cost / Read Models

Potential:

```text
production_planned_cost_snapshot
production_actual_cost_fact
production_cost_projection
production_variance
product_profitability_projection
component_usage_projection
material_readiness_projection
capacity_readiness_projection
production_order_summary_projection
shop_floor_queue_projection
production_genealogy_projection
```

---

# 308. Tables Manufacturing Must Not Own

Forbidden duplicate truth:

```text
product
uom
stock_balance
inventory_movement
inventory_cost_layer
purchase_order
supplier_invoice
employee
fixed_asset
maintenance_work_order
quality_inspection
journal_entry
sales_order
selling_price
```

---

# 309. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 310. Runtime DB Role

No owner/BYPASSRLS.

---

# 311. IDs

UUIDv7.

---

# 312. Money

FinBase Money / high precision decimal.

No float.

---

# 313. Quantity

High precision decimal.

---

# 314. Time

NodaTime.

Use correct semantics:

```text
LocalDate
Instant
Duration
DateTimeZone
```

---

# 315. Versioning

Required for:

```text
BOM
Formula
Routing
Production Definition
Cost/overhead policy where material
Substitution rule
Planning policy
```

---

# 316. Effective Dating

Master definitions use:

```text
ValidFrom
ValidTo
```

where business requires.

---

# 317. Immutable Historical Snapshot

Released Production Order stores immutable manufacturing-definition snapshot.

---

# 318. Optimistic Concurrency

Use for mutable:

```text
Draft BOM
Production Order
Planning result review
Work Center
```

---

# 319. BOM Cycle Protection

No product can become ancestor of itself in active explosion graph unless explicit special recyclable process approved by separate architecture.

---

# 320. Substitution Cycle

Avoid:

```text
A substitutes B
B substitutes A
```

causing infinite planning recursion.

Priority resolution bounded.

---

# 321. Multi-Level Explosion Cache

Allowed as rebuildable planning structure.

---

# 322. Read Models

Allowed for performance.

Never independent truth.

---

# 323. Production Definition Resolver API

Representative:

```text
POST /api/v1/manufacturing/production-definitions/resolve
```

Inputs:

```text
Product
Plant
Qty
Date
```

---

# 324. Product Manufacturing APIs

```text
GET  /manufacturing/products/{productId}
GET  /manufacturing/products/{productId}/cost
GET  /manufacturing/products/{productId}/profitability
GET  /manufacturing/products/{productId}/where-used
GET  /manufacturing/products/{productId}/availability-to-produce
```

---

# 325. BOM APIs

```text
GET  /manufacturing/boms
POST /manufacturing/boms
GET  /manufacturing/boms/{id}
POST /manufacturing/boms/{id}/versions
GET  /manufacturing/bom-versions/{id}
POST /manufacturing/bom-versions/{id}/approve
POST /manufacturing/bom-versions/{id}/activate
POST /manufacturing/bom-versions/{id}/supersede
```

---

# 326. Component Replace Preview API

```text
POST /manufacturing/bom-versions/{id}/component-change-preview
```

Returns:

```text
cost impact
margin impact
availability
lead time
affected orders
affected products
```

---

# 327. Substitute APIs

```text
GET  /manufacturing/substitution-groups
POST /manufacturing/substitution-groups
POST /manufacturing/substitution-groups/{id}/members
POST /manufacturing/production-orders/{id}/substitutions/preview
POST /manufacturing/production-orders/{id}/substitutions/apply
```

---

# 328. Routing APIs

```text
GET  /manufacturing/routings
POST /manufacturing/routings
POST /manufacturing/routings/{id}/versions
POST /manufacturing/routing-versions/{id}/activate
```

---

# 329. Work Center APIs

```text
GET  /manufacturing/work-centers
POST /manufacturing/work-centers
GET  /manufacturing/work-centers/{id}
GET  /manufacturing/work-centers/{id}/capacity
GET  /manufacturing/work-centers/{id}/schedule
```

---

# 330. Planning APIs

```text
POST /manufacturing/planning-runs
GET  /manufacturing/planning-runs/{id}
GET  /manufacturing/planning-runs/{id}/exceptions
GET  /manufacturing/planned-production
POST /manufacturing/planned-production/{id}/firm
POST /manufacturing/planned-production/{id}/cancel
```

---

# 331. Production Order APIs

```text
GET  /manufacturing/production-orders
POST /manufacturing/production-orders
GET  /manufacturing/production-orders/{id}
PUT  /manufacturing/production-orders/{id}
POST /manufacturing/production-orders/{id}/release
POST /manufacturing/production-orders/{id}/start
POST /manufacturing/production-orders/{id}/hold
POST /manufacturing/production-orders/{id}/complete
POST /manufacturing/production-orders/{id}/close
POST /manufacturing/production-orders/{id}/cancel
```

---

# 332. Material APIs

```text
GET  /manufacturing/production-orders/{id}/materials
POST /manufacturing/production-orders/{id}/materials/reserve
POST /manufacturing/production-orders/{id}/materials/issue
POST /manufacturing/production-orders/{id}/materials/consume
POST /manufacturing/production-orders/{id}/materials/return
```

These orchestrate Inventory; Manufacturing never writes Inventory tables.

---

# 333. Output APIs

```text
POST /manufacturing/production-orders/{id}/outputs
POST /manufacturing/production-orders/{id}/scrap
```

Inventory executes physical effect.

---

# 334. Operation APIs

```text
GET  /manufacturing/production-orders/{id}/operations
POST /manufacturing/operations/{id}/start
POST /manufacturing/operations/{id}/pause
POST /manufacturing/operations/{id}/resume
POST /manufacturing/operations/{id}/report
POST /manufacturing/operations/{id}/complete
```

---

# 335. Shop Floor APIs

Dedicated compact read model:

```text
GET  /manufacturing/shop-floor/my-work
GET  /manufacturing/shop-floor/work-centers/{id}
GET  /manufacturing/shop-floor/operations/{id}
POST /manufacturing/shop-floor/commands
```

---

# 336. Cost APIs

```text
GET  /manufacturing/products/{id}/cost-rollup
POST /manufacturing/products/{id}/cost-simulations
GET  /manufacturing/production-orders/{id}/planned-cost
GET  /manufacturing/production-orders/{id}/actual-cost
GET  /manufacturing/production-orders/{id}/variance
```

---

# 337. Availability-To-Produce API

```text
POST /manufacturing/products/{id}/availability-to-produce
```

Input:

```text
Plant
Qty?
Date?
UseApprovedSubstitutes?
```

---

# 338. Response Example

```text
MaxProducible = 320
Bottleneck = Foam A
WithSubstitute = 500
```

---

# 339. Commands — Master

Representative:

```text
CreateBom
CreateBomRevision
ApproveBomRevision
ActivateBomRevision
SupersedeBomRevision

CreateFormula
CreateFormulaRevision

CreateRouting
CreateRoutingRevision

CreateProductionDefinition
ActivateProductionDefinition
```

---

# 340. Commands — Substitution

```text
CreateSubstitutionGroup
AddApprovedSubstitute
ChangeSubstitutePriority
ExpireSubstitute
PreviewProductionSubstitute
ApplyProductionSubstitute
```

---

# 341. Commands — Planning

```text
RunMrp
CreateMpsVersion
PublishMpsVersion
FirmPlannedProduction
RequestPlannedPurchase
RequestPlannedTransfer
```

---

# 342. Commands — Production

```text
CreateProductionOrder
ReleaseProductionOrder
StartProductionOrder
ChangeProductionOrderQty
HoldProductionOrder
ResumeProductionOrder
CompleteProductionOrder
CloseProductionOrder
CancelProductionOrder
SplitProductionOrder
```

---

# 343. Commands — Materials

```text
RequestMaterialReservation
RequestMaterialIssue
ConfirmMaterialConsumption
RequestUnusedMaterialReturn
RecordMaterialVarianceReason
```

---

# 344. Commands — Operations

```text
StartProductionOperation
PauseProductionOperation
ResumeProductionOperation
ReportOperationOutput
CompleteProductionOperation
```

---

# 345. Commands — Output

```text
ReportGoodOutput
ReportScrap
ReportCoProduct
ReportByProduct
```

---

# 346. No Generic `SetCost`

Forbidden.

---

# 347. No Generic `SetStockConsumed`

Forbidden.

---

# 348. No Generic `SetBomId` On Released Order

Use snapshot/change command.

---

# 349. No Generic `SetMaterial`

Use substitution/change semantics.

---

# 350. No Generic `SetFinished=true`

Use lifecycle command with validations.

---

# 351. No Generic `SetVariance`

Variance derived from source facts.

---

# 352. Domain Contracts

Recommended:

```text
Todir.Manufacturing.Contracts
```

---

# 353. Backend Namespaces

```text
Todir.Modules.Manufacturing.Domain
Todir.Modules.Manufacturing.Application
Todir.Modules.Manufacturing.Infrastructure
Todir.Manufacturing.Contracts
```

---

# 354. Internal Areas

Potential:

```text
Products/
Plants/
Definitions/
Bom/
Formula/
Routing/
Substitution/
Planning/
ProductionOrders/
Materials/
Operations/
Resources/
ShopFloor/
Costing/
Traceability/
Subcontracting/
Reporting/
```

---

# 355. Event Examples Emitted

Potential:

```text
ProductionDefinitionActivatedV1
ProductionOrderReleasedV1
ProductionOrderStartedV1
ProductionOrderCompletedV1
ProductionOrderClosedV1
ProductionMaterialSubstitutedV1
ProductionOutputReportedV1
ManufacturingVarianceFinalizedV1
```

---

# 356. Events Consumed

Potential:

```text
InventoryCostChangedV1
InventoryReceiptConfirmedV1
InventoryAvailabilityChangedV1
PurchaseOrderChangedV1
MaintenanceDowntimeChangedV1
SalesDemandChangedV1
```

Do not subscribe to noisy events without need.

---

# 357. Cost Projection Update Strategy

Prefer:

```text
source event
→ affected component index
→ targeted invalidation/rebuild
```

---

# 358. MRP Execution Strategy

MRP is batch/planning compute.

Do not run full MRP synchronously during every Sales screen request.

---

# 359. Shop Floor Consistency

Execution commands require strong transactional validation.

---

# 360. Inventory Transaction Boundary

Where immediate integrity matters:

```text
Manufacturing command
→ Inventory application contract
```

may participate in Platform Transaction Coordinator if approved.

Do not replace with eventual event when stock invariant requires synchronous confirmation.

---

# 361. Outbox

Use Platform outbox for async projections/integrations.

---

# 362. RabbitMQ

Async integration only.

Not replacement for production/inventory ACID invariants.

---

# 363. Idempotency

Mandatory for:

```text
Material consume
Output receipt
Scrap
Operation report
Subcontract receipt
Planning firm
```

---

# 364. Client Retry

Never double-consume material or double-receive output.

---

# 365. Production Output Idempotency Key

Stable per user/device/report operation.

---

# 366. Concurrency — Material Consumption

Two operators cannot consume same reserved quantity beyond allowed amount.

---

# 367. Concurrency — Production Output

Protect cumulative target/overproduction policy.

---

# 368. Concurrency — BOM Activation

Cannot have ambiguous overlapping active definitions without explicit priority.

---

# 369. Concurrency — Firm Planned Order

One planned supply converts once.

---

# 370. Permissions

Initial vocabulary:

```text
manufacturing.product.read
manufacturing.definition.read
manufacturing.definition.manage

manufacturing.bom.read
manufacturing.bom.create
manufacturing.bom.edit
manufacturing.bom.approve
manufacturing.bom.activate

manufacturing.routing.read
manufacturing.routing.manage

manufacturing.substitute.read
manufacturing.substitute.manage
manufacturing.substitute.apply

manufacturing.planning.read
manufacturing.planning.run
manufacturing.planning.firm

manufacturing.production.read
manufacturing.production.create
manufacturing.production.release
manufacturing.production.execute
manufacturing.production.change
manufacturing.production.complete
manufacturing.production.close
manufacturing.production.cancel

manufacturing.material.read
manufacturing.material.reserve
manufacturing.material.issue
manufacturing.material.consume
manufacturing.material.return

manufacturing.shopfloor.use

manufacturing.cost.read
manufacturing.cost.sensitive.read
manufacturing.margin.read

manufacturing.workcenter.read
manufacturing.workcenter.manage

manufacturing.reports.read
manufacturing.settings.manage
manufacturing.audit.read
```

---

# 371. Scope Model

Combine with:

```text
Plant
Branch
LegalEntity
WorkCenter
AssignedOperation
ProductGroup
AllAuthorized
```

---

# 372. Operator Scope

Normally:

```text
assigned/current Work Center operations
required materials
instructions
quality prompts
```

not full cost/margin.

---

# 373. Planner Scope

Planning/material/capacity.

---

# 374. Manufacturing Engineer

BOM/Formula/Routing/Definition.

---

# 375. Cost Accountant

Cost/variance with appropriate financial access.

---

# 376. Plant Manager

Broad plant execution/analytics.

---

# 377. Cost Privacy

Worker does not see:

```text
supplier contract prices
salary
selling margin
```

unless authorized.

---

# 378. Engineering Change Approval

Optional approval for BOM/routing activation.

---

# 379. High-Risk Changes

Can require approval:

```text
component replacement
yield change
routing change
cost policy change
substitute auto-apply
```

---

# 380. Audit Critical Actions

Capture:

```text
BOM revision activation
formula change
routing change
Production Definition activation
substitute creation/application
Production Order release/change/cancel
material override
manual consumption
scrap
output correction
order close/reopen
cost policy change
MRP firming
```

---

# 381. Security — Cross Tenant

RLS mandatory.

---

# 382. Security — Cross Plant

Application authorization.

---

# 383. Security — Forged Product/Material Ref

Validate Catalog and entity visibility.

---

# 384. Security — Forged Inventory Location

Inventory authorization.

---

# 385. Security — Cost Leakage

Dedicated cost/margin permission.

---

# 386. Security — Mass Export

Audit.

---

# 387. Performance Mission

Fast product economics + scalable planning + fast shop floor.

---

# 388. Product Manufacturing Workspace Target

```text
p95 <= 150-350ms
```

for core summary/read projection.

---

# 389. Cost Roll-Up Target

Cached/current projection:

```text
p95 <= 150-400ms
```

for normal product.

Fresh deep simulation may be slower but bounded.

---

# 390. Shop Floor Queue Target

```text
p95 <= 100-250ms
```

---

# 391. Production Order Open Target

```text
p95 <= 150-350ms
```

without loading entire genealogy.

---

# 392. MRP

Batch workload.

Must scale independently of interactive APIs.

---

# 393. MRP Set-Based Processing

Avoid per-demand recursive database calls.

---

# 394. BOM Explosion Query

Use optimized recursive/set-based strategy.

---

# 395. No N+1

Production list cannot query each order separately for:

```text
product
material readiness
capacity
cost
status
```

---

# 396. Reverse Usage Index

Mandatory at scale.

---

# 397. Cost Projection Batch Update

Debounce/coalesce high-frequency cost updates.

---

# 398. Inventory Cost Event Storm

If 10,000 product costs revalue:

```text
batch affected-definition refresh
```

not one heavyweight job per event.

---

# 399. Planning Indexes

Likely:

```text
product/plant/date
demand/date/state
planned_supply/date/state
component/need_date
```

---

# 400. BOM Indexes

```text
product/state/valid_from
bom_version/bom
component_product
substitution_group
```

---

# 401. Production Order Indexes

```text
tenant/entity/plant/state/date
product/state
need_date/state
source_ref
```

---

# 402. Operation Indexes

```text
work_center/state/planned_start
production_order/sequence
resource/state
```

---

# 403. Cost Indexes

```text
product/definition/cost_basis/as_of
production_order/source_type
```

---

# 404. Partitioning

Do not partition Day 1.

Measure high-volume execution facts first.

---

# 405. PostgreSQL First

No separate planning database Day 1.

---

# 406. Redis

Not canonical and not required Day 1.

---

# 407. OpenSearch

Not required Day 1.

---

# 408. Large Scale Test Targets

Architecture rehearsal:

```text
1M Product Variants
500k active Manufacturing Profiles
1M BOM/Formula versions
20M BOM lines
5M Production Orders
50M material requirement/execution rows
10M operations
large multi-level MRP run
```

Scale scenarios adjusted to actual target customers.

---

# 409. Background Jobs

Potential:

```text
Cost projection refresh
MRP planning run
MPS expansion
Planning exception refresh
Capacity projection
Production analytics refresh
Large export
Genealogy projection rebuild
```

---

# 410. Observability Metrics

Potential:

```text
manufacturing_production_order_cycle_time
manufacturing_material_shortage_count
manufacturing_substitution_total
manufacturing_scrap_rate
manufacturing_yield_variance
manufacturing_cost_rollup_duration
manufacturing_cost_projection_stale_count
manufacturing_mrp_run_duration
manufacturing_planned_order_count
manufacturing_capacity_overload_count
manufacturing_shopfloor_command_duration
manufacturing_close_blocker_count
```

No high-cardinality IDs as labels.

---

# 411. Tracing — Cost Roll-Up

```text
Resolve Definition
Explode Components
Load Cost Snapshot
Load Routing Cost
Apply Yield/Scrap
Aggregate
Resolve Selling Price if requested
Build Profitability
```

---

# 412. Tracing — Production Release

```text
Validate Definition
Snapshot BOM/Formula
Snapshot Routing
Resolve Materials
Cost Snapshot
Validate Plant
Create Requirements
Audit
Commit
```

---

# 413. Tracing — MRP

```text
Load Demand
Resolve Definitions
Explode BOM
Net Supply
Substitution
Lead Time
Planned Supply
Exceptions
Persist Results
```

---

# 414. Tracing — Output

```text
Validate Order
Validate Qty
Validate Operation/Quality gates
Inventory Receipt
Cost Context
Update Progress
Audit
```

---

# 415. Logging

Never dump:

```text
entire BOM graphs
supplier confidential prices
salary data
customer-sensitive engineering data
```

unnecessarily.

---

# 416. Reports — Production

```text
Open Production Orders
Released / In Progress
Completed / Closed
Late Orders
Production By Product
Production By Plant
Production By Work Center
```

---

# 417. Reports — Materials

```text
Material Requirements
Material Shortages
Substitutions Used
Consumption Variance
Unused Issued Material
Scrap
```

---

# 418. Reports — Planning

```text
MRP Exceptions
Planned Production
Planned Purchase
Planned Transfer
Demand/Supply Pegging
Late Supply
```

---

# 419. Reports — Capacity

```text
Work Center Load
Capacity Overload
Idle Capacity
Planned Maintenance Impact
```

---

# 420. Reports — Cost

```text
Current Estimated Product Cost
Actual Production Cost
Planned vs Actual
Cost Variance
Cost Change Drivers
Component Cost Exposure
```

---

# 421. Reports — Profitability

```text
Current Estimated Margin
Last Actual Margin
Margin Below Threshold
Margin Change
Cost vs Selling Price
```

Operational profitability, clearly labeled.

---

# 422. Reports — Quality/Execution

After `27`:

```text
Yield
Scrap
Rework
Quality Holds
NCR-linked Production
```

---

# 423. Reports — Traceability

```text
Input Lot → Output Lots
Output Lot → Input Lots
Production Order Genealogy
```

---

# 424. Drilldown Rule

Every KPI drills to source Production/Inventory/Cost facts.

---

# 425. Import — BOM

Support:

```text
Product
Revision
Component
Qty
UOM
Validity
Operation
Substitute group
```

---

# 426. Import — Routing

```text
Operations
Sequence
Work Center
Setup
Run
Resources
```

---

# 427. Import — Production Definitions

Preserve:

```text
plant
validity
quantity range
BOM revision
routing revision
priority
```

---

# 428. Import — Open Production Orders

Must preserve:

```text
state
remaining qty
materials issued/consumed
partial outputs
operations
cost context
```

---

# 429. Import — Historical Production

Options:

```text
Detailed
Summary
None
```

---

# 430. Migration Provenance

```text
SourceSystem
LegacyId
BatchId
ImportedAt
AsOfDate
```

---

# 431. No Fake Inventory Movements

If legacy history lacks movement detail, do not synthesize false stock transactions.

---

# 432. Go-Live Boundary

After date, normal TODIR Manufacturing + Inventory movements.

---

# 433. Small Manufacturer Setup Wizard

```text
1. Select products to manufacture
2. Create/import BOM
3. Optional routing
4. Choose source warehouse/output warehouse
5. Review current cost
6. Test Produce 1
7. Go Live
```

---

# 434. Advanced Plant Setup Wizard

```text
1. Plant
2. Product profiles
3. BOM/Formula versions
4. Routings
5. Work Centers
6. Calendars/capacity
7. Substitutions
8. Cost policies
9. MRP policies
10. Opening open orders
11. Integration tests
12. Go Live
```

---

# 435. No Irrelevant Setup Blockers

No Work Centers? Simple product can still manufacture.

No MRP? Direct Production Order works.

No Quality module? Quality screens hidden.

No Maintenance? Capacity uses configured calendars only.

---

# 436. Small Company Defaults

```text
BOM                       ON
Production Definition     auto-created/simple
Routing                   optional
Work Centers              optional
MRP                       optional
Backflush                 configurable
Manual Material Issue     optional
Substitution Suggestions  ON
Advanced Approval         OFF
Cost Roll-Up              ON
Profitability             ON
```

---

# 437. Medium/Large Defaults

Enable as needed:

```text
Version approvals
MRP
MPS
Work Centers
Capacity
Operations
Shop Floor
Lots/Serial genealogy
Subcontracting
Quality integration
Maintenance downtime
Detailed variance
```

---

# 438. Enterprise Later

Possible:

```text
Finite scheduling optimizer
Advanced APS
Constraint-based scheduling
Advanced PLM
Potency
Catch Weight
Advanced Process Control
MES machine integration
IoT/OEE
Advanced labor standards
Advanced costing
```

---

# 439. AI Allowed

AI can:

```text
explain cost increase
suggest approved substitute
summarize shortages
highlight abnormal scrap
predict likely delay
suggest planning exception priority
```

---

# 440. AI Not Allowed

AI cannot autonomously:

```text
change BOM
activate revision
consume stock
release production
approve substitute outside policy
post accounting
override quality hold
```

---

# 441. Product Workspace UX

Suggested summary:

```text
Chair A

Current Cost       925
Selling Price    1,300
Margin           28.85%

Can Produce Now    320
With Alternatives  500

Active Definition
BOM v8
Routing v4

Shortage:
Foam A

[Produce]
[Simulate 100]
[Edit Manufacturing]
[View Alternatives]
```

---

# 442. Manufacturing Edit UX

Tabs:

```text
Components
Operations
Alternatives
Cost
Versions
Where Used
History
```

---

# 443. User Changes Component

Workflow:

```text
Select line
→ Replace
→ pick approved/new material
→ impact preview
→ Save New Revision
```

---

# 444. User Changes Qty

Impact preview updates:

```text
Cost
Availability
Expected Yield
MRP
```

before revision activation.

---

# 445. Fast Inline Editing

BOM grid supports:

```text
Product picker
Qty
UOM
Operation
Substitute group
```

keyboard-friendly.

---

# 446. Bulk Edit

Authorized manufacturing engineer can bulk-adjust components with validation.

---

# 447. Spreadsheet Import

Supported for large BOM setup.

Preview/diff before commit.

---

# 448. BOM Diff

Revision comparison:

```text
Added
Removed
Qty Changed
UOM Changed
Substitute Changed
Operation Changed
```

---

# 449. Cost Diff

Revision comparison includes:

```text
Old Estimated Cost
New Estimated Cost
Delta
Margin Impact
```

---

# 450. Where-Used Impact

Before component obsolescence show all affected products/definitions/orders.



# 451. Business Scenario — Repeated Chair Production

Configured once:

```text
Chair A
Active Definition
BOM v8
Routing v4
```

Daily use:

```text
Produce 100
```

TODIR loads everything automatically.

---

# 452. Acceptance — Repeated Production

User does not reselect normal components.

---

# 453. Business Scenario — Permanent Material Change

```text
Foam A → Foam B
```

because B is better.

System previews:

```text
cost
margin
availability
affected definitions
```

and creates new revision.

---

# 454. Acceptance — Permanent Change

Old released/history orders unchanged.

---

# 455. Business Scenario — Temporary Stockout

Foam A short.

TODIR suggests approved Foam B.

---

# 456. Acceptance — Temporary Stockout

BOM master unchanged.

Order stores substitution history.

---

# 457. Business Scenario — Supplier Price Increase

Wood valuation increases.

TODIR recalculates affected current product-cost projections.

---

# 458. Acceptance — Price Increase

No BOM edit required.

---

# 459. Business Scenario — Supplier Invoice Correction

Receipt estimated cost 100.

Final supplier cost 105.

Inventory posts valuation adjustment.

Manufacturing current estimate updates.

Old closed production actual history remains historical.

---

# 460. Business Scenario — Margin Drop

Current cost increase causes:

```text
Margin 26% → 18%
```

System warns and explains driver.

No auto selling-price change.

---

# 461. Business Scenario — Maximum Producible

User opens Chair A.

TODIR calculates current max output from material bottleneck.

---

# 462. Business Scenario — Max With Substitutes

TODIR also shows max producible if approved alternatives are used.

---

# 463. Business Scenario — MRP Shortage

Demand 1000 Tables.

MRP nets stock + open supply and creates planned purchase/production recommendations only for remaining shortage.

---

# 464. Business Scenario — Multi-Level BOM

Finished demand explodes through subassemblies.

No double counting.

---

# 465. Business Scenario — Capacity Overload

Two orders require 18h on 16h Paint Line.

System flags conflict.

---

# 466. Business Scenario — Machine Maintenance

Maintenance blocks 4h.

Capacity projection updates; production order remains Manufacturing-owned.

---

# 467. Business Scenario — Partial Production

Target 1000.

Produce 600 good.

Remaining 400 stays visible.

---

# 468. Business Scenario — Scrap

Report:

```text
Good 600
Scrap 20
```

Inventory/output/cost reflect separate facts.

---

# 469. Business Scenario — Backflush

Simple factory reports 100 good units.

TODIR consumes expected materials according to policy and creates canonical Inventory movements.

---

# 470. Business Scenario — Actual Consumption

Controlled plant records actual raw material above BOM standard.

Variance appears.

---

# 471. Business Scenario — Returned Material

Unused issued material returned to stock.

Not treated as consumed.

---

# 472. Business Scenario — Lot Genealogy

Raw lots A+B create finished lot C.

Trace both directions.

---

# 473. Business Scenario — Process Batch

Formula produces:

```text
950 L main
30 L by-product
20 L normal loss
```

all explicit.

---

# 474. Business Scenario — Subcontract Entire Product

Purchasing owns PO to subcontractor.

Manufacturing owns technical production definition/context.

Inventory owns receipt.

---

# 475. Business Scenario — Outsourced Operation

External painting sits between internal cutting and assembly.

Manufacturing orchestrates; Purchasing procures service.

---

# 476. Business Scenario — Quality Hold

Output cannot proceed/close when configured Quality gate blocked.

Manufacturing does not create fake pass.

---

# 477. Business Scenario — Rework

Rejected quantity goes through linked rework path with full cost/history.

---

# 478. Test — BOM Auto Resolution

Product/plant/date/qty resolves deterministic Production Definition.

---

# 479. Test — Ambiguous Definition

Two equally applicable definitions without explicit priority → block.

---

# 480. Test — BOM Revision History

Old order keeps old revision.

---

# 481. Test — Draft Update To Latest

Explicit action works.

---

# 482. Test — Released Order Master Change

No silent change.

---

# 483. Test — In-Progress Master Change

No silent change.

---

# 484. Test — Permanent Component Change

Creates revision.

---

# 485. Test — Temporary Substitute

No master BOM change.

---

# 486. Test — Substitute Ratio

Uses correct equivalent qty.

---

# 487. Test — Substitute Date

Expired alternative not selectable.

---

# 488. Test — Substitute Priority

Preferred available → preferred selected.

---

# 489. Test — Preferred Unavailable

Next approved available substitute suggested.

---

# 490. Test — No Approved Substitute

No arbitrary product choice without engineering override permission.

---

# 491. Test — Substitute Cost Preview

Matches canonical cost basis.

---

# 492. Test — Substitute Margin Preview

Margin/markup formulas correct.

---

# 493. Test — Supplier Price Change

No BOM mutation.

---

# 494. Test — Inventory Cost Change

Affected cost projection refreshes.

---

# 495. Test — Unaffected Product

Not unnecessarily recalculated.

---

# 496. Test — Latest Purchase vs Valuation

UI labels both and does not substitute one for the other.

---

# 497. Test — Current vs Planned vs Actual Cost

All distinct.

---

# 498. Test — Historical Actual Cost

Not recomputed from current prices.

---

# 499. Test — Cost Breakdown

Drills to component/source facts.

---

# 500. Test — Margin Alert

Triggered only by configured basis/threshold.

---

# 501. Test — Margin vs Markup

Exact formulas.

---

# 502. Test — Multi-Level Cost Roll-Up

Subassembly cost included once.

---

# 503. Test — BOM Cycle

Rejected.

---

# 504. Test — Substitute Cycle

No infinite resolution.

---

# 505. Test — Max Producible

Correct bottleneck with UOM conversion.

---

# 506. Test — Max With Substitute

Correct equivalent ratios.

---

# 507. Test — MRP Netting

On-hand/open supply correctly reduces net requirement.

---

# 508. Test — MRP Multi-Level

Lower-level demand correct.

---

# 509. Test — MRP Planned Purchase

Does not create PO automatically unless explicit approved automation.

---

# 510. Test — MRP Planned Production

Does not create Released Production directly by default.

---

# 511. Test — Firming

Exactly one Production Order created per planned-order conversion.

---

# 512. Test — Planned Order Retry

Idempotent.

---

# 513. Test — Delay Propagation

Late lower-level supply affects parent risk correctly.

---

# 514. Test — Safety Stock

Planning preserves configured minimum.

---

# 515. Test — Order Multiple

Planned qty rounds according to explicit policy.

---

# 516. Test — Lead Time

Suggested start date correct.

---

# 517. Test — Material Requirement

No stock change.

---

# 518. Test — Reservation

No physical movement.

---

# 519. Test — Issue

Inventory movement once.

---

# 520. Test — Consumption

Canonical Inventory movement/value once.

---

# 521. Test — Backflush Retry

No double consumption.

---

# 522. Test — Partial Consumption

Remaining requirement correct.

---

# 523. Test — Material Return

Reduces/returns correct physical stock.

---

# 524. Test — Scrap

Good output unaffected except explicit quantities/cost.

---

# 525. Test — Partial Output

Inventory receives only reported good quantity.

---

# 526. Test — Overproduction

Policy controls allowed tolerance/approval.

---

# 527. Test — Output Retry

No duplicate receipt.

---

# 528. Test — Lot Genealogy

Inputs/outputs exact.

---

# 529. Test — Serial Genealogy

One-to-many/many-to-one relationships as business permits.

---

# 530. Test — Setup Time

Applied once per setup basis.

---

# 531. Test — Run Time

Scales by correct quantity basis.

---

# 532. Test — Capacity

Resource demand vs available calendar correct.

---

# 533. Test — Maintenance Downtime

Removes capacity only within configured window.

---

# 534. Test — Operator Time

Does not alter HR Attendance.

---

# 535. Test — Labor Privacy

No salary leak.

---

# 536. Test — Production Close

Blocked with unresolved material/output/quality/cost issues.

---

# 537. Test — Closed Order

Ordinary mutation rejected.

---

# 538. Test — Cost Correction

Semantic correction, no historical overwrite.

---

# 539. Test — Process Scaling

Correct proportional quantities.

---

# 540. Test — Co-Products

All outputs trace/cost allocation basis explicit.

---

# 541. Test — By-Products

Separate from scrap.

---

# 542. Test — Yield

Expected/actual/variance correct.

---

# 543. Test — Subcontract

No mini Purchasing table.

---

# 544. Test — Outsourced Operation

Production progress waits on correct procurement/receipt state.

---

# 545. Test — Quality Hold

Cannot bypass by ordinary manufacturing permission.

---

# 546. Test — Cross-Tenant RLS

No leakage.

---

# 547. Test — Cross-Entity Product/Plant

Authorization enforced.

---

# 548. Test — Forged Inventory Ref

Rejected by owning contract.

---

# 549. Test — Forged Work Center

Rejected by plant scope.

---

# 550. Test — Cost Permission

Unauthorized operator cannot read margin.

---

# 551. Test — 20M BOM Lines

Explosion/where-used indexes measured.

---

# 552. Test — 5M Production Orders

Operational list remains performant.

---

# 553. Test — Large MRP Run

Time/memory measured with representative multi-level graph.

---

# 554. Property Test — Material Conservation

For each production source:

```text
Issued
=
Consumed + Returned + Remaining Staged + Scrap disposition
```

according to workflow/state.

---

# 555. Property Test — Output

```text
Reported output
=
Good + Scrap + Rework/other explicitly classified output
```

for configured production basis.

---

# 556. Property Test — Cost

Every actual production cost amount traces to canonical source fact.

---

# 557. Property Test — Substitution

No substitute quantity used without approved/recorded substitution semantics.

---

# 558. Property Test — Revision

Released order snapshot cannot change because master revision changed.

---

# 559. Property Test — Planned Supply

Firmed quantity cannot be double-firmed.

---

# 560. Architecture Test — No Stock Mutation

Manufacturing cannot write Inventory tables.

---

# 561. Architecture Test — No Product Duplicate

Manufacturing references Catalog.

---

# 562. Architecture Test — No PO Duplicate

Manufacturing cannot own Purchase Order.

---

# 563. Architecture Test — No Employee Duplicate

HR remains owner.

---

# 564. Architecture Test — No Asset Duplicate

Fixed Assets remains owner.

---

# 565. Architecture Test — No Maintenance Duplicate

Service/Maintenance remains owner.

---

# 566. Architecture Test — No Quality Truth

Future Quality remains owner.

---

# 567. Architecture Test — No Journal Entry

Ledger remains owner.

---

# 568. Architecture Test — No Selling Price Master

Pricing remains owner.

---

# 569. Red-Team — Select Components Every Time

Reject UX.

---

# 570. Red-Team — BOM Stores Current Supplier Price

Reject.

---

# 571. Red-Team — Last Purchase Price = Cost

Reject universal assumption.

---

# 572. Red-Team — Change Raw Material Price → Edit BOM

Reject.

---

# 573. Red-Team — Temporary Substitute Changes Master BOM

Reject.

---

# 574. Red-Team — Any Product Can Substitute

Reject.

---

# 575. Red-Team — Substitute Always 1:1

Reject.

---

# 576. Red-Team — Auto Substitute Regardless Of Quality

Reject.

---

# 577. Red-Team — Active BOM Editable After Use

Reject destructive edit.

---

# 578. Red-Team — Current Cost Rewrites Old Production Cost

Reject.

---

# 579. Red-Team — Current Margin Called Accounting Profit

Reject.

---

# 580. Red-Team — Material Requirement Decrements Stock

Reject.

---

# 581. Red-Team — Reservation = Consumption

Reject.

---

# 582. Red-Team — Output = Order Closed

Reject.

---

# 583. Red-Team — Work Center = Machine

Reject.

---

# 584. Red-Team — Machine = Fixed Asset Copy

Reject.

---

# 585. Red-Team — Production Time = Attendance

Reject.

---

# 586. Red-Team — MRP Creates PO Without Commercial Owner

Reject.

---

# 587. Red-Team — MRP Recommendation Mutates Firm Order

Reject.

---

# 588. Red-Team — Quality Notes In Manufacturing Replace Quality Module

Reject.

---

# 589. Red-Team — Maintenance Downtime Stored As Production Maintenance Work

Reject.

---

# 590. Red-Team — Manufacturing Calculates Inventory FIFO Itself

Reject.

---

# 591. Red-Team — One Cost Field

Reject.

---

# 592. Red-Team — One Profit Percent

Reject.

---

# 593. Red-Team — Full MRP On Every Product Page Open

Reject performance design.

---

# 594. Red-Team — Cost Change Recalculates Entire Tenant Synchronously

Reject.

---

# 595. Competitive Benchmark — Odoo 19

Useful current patterns:

```text
Manufacturing Orders
BOMs
Work Centers
Shop Floor
MPS
Multi-level BOMs
Scrap
Lots/Serials
By-products
Subcontracting
WIP costs
Manufacturing order cost
```

TODIR adopts operational simplicity and worker-focused execution.

Official reference:

`https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/manufacturing.html`

---

# 596. Odoo Simplicity Lesson

Repeated production should feel like:

```text
Product + Quantity → Produce
```

once master data exists.

---

# 597. Odoo Cost Lesson

Expected manufacturing-order cost and real cost can differ due to:

```text
actual material
actual quantities
actual operation time
component cost
```

TODIR preserves this distinction explicitly.

---

# 598. Odoo Subcontracting Lesson

Mature workflows include:

```text
basic subcontract
company-resupplied subcontractor
dropship components to subcontractor
```

TODIR keeps manufacturing technical ownership while Purchasing/Inventory own their truth.

---

# 599. Competitive Benchmark — Dynamics 365

Useful current patterns:

```text
Master Planning
Planned Production Orders
BOM requirements
BOM version selection by validity/quantity
firming
delay propagation
material substitution
```

Official references:

`https://learn.microsoft.com/en-us/dynamics365/supply-chain/master-planning/planning-optimization/production-planning`

`https://learn.microsoft.com/en-us/dynamics365/supply-chain/production-control/substitute-items-bom-lines`

---

# 600. Dynamics Substitution Lesson

Current Dynamics supports substitution:

```text
by date
during planning
during production
```

TODIR makes this a first-class simplified capability.

---

# 601. Competitive Benchmark — Oracle Fusion Manufacturing

Useful current patterns:

```text
Work Definitions
Operations
Materials
Resources
Outputs
Discrete
Process
Mixed-mode
```

Official references:

`https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/faumf/overview-of-production-process-design.html`

`https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/faumf/how-you-create-work-definitions.html`

---

# 602. Oracle Design Lesson

One coherent production definition should bring together:

```text
materials
operations
resources
outputs
```

without forcing user to navigate disconnected master data.

---

# 603. Competitive Benchmark — SAP S/4HANA

Useful pattern:

```text
Production Version
=
BOM alternative
+ Routing/Recipe
+ Validity
+ Lot-size range
```

Used in MRP, production/process orders and product costing.

Official reference:

`https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/21aead0c98bd4755abdacd91c99e3393/c234b8535c39b44ce10000000a174cb4-401.html`

---

# 604. SAP Design Lesson

TODIR ProductionDefinition adopts this business strength while providing simpler UX.

---

# 605. Competitive Benchmark — NetSuite

Useful patterns:

```text
Manufacturing Routing
Work Centers
Setup Time
Run Rate
Labor/Machine/Overhead
WIP
Cost Roll-up
```

Official references:

`https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N2345383.html`

`https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N2351557.html`

---

# 606. NetSuite Cost Lesson

Setup and run costs must have separate quantity/time bases.

---

# 607. Competitive Benchmark — Acumatica

Useful mid-market pattern:

```text
BOM
Routing
Operations
Work Centers
Materials
Tools
Overhead
Production Orders
```

TODIR targets comparable practical depth without forcing advanced controls on SMEs.

---

# 608. Competitive Product Rule

Competitor feature presence validates business needs.

TODIR does not copy screen complexity.

---

# 609. Competitive Product Target

> **Odoo/Acumatica usability + SAP/Oracle manufacturing-definition correctness + Dynamics-grade planning + TODIR domain ownership.**

---

# 610. Agent Implementation Philosophy

Business workflow first.

Do not begin with CRUD tables for BOM and Production Order only.

---

# 611. Agent Must Understand User Goal

The product owner expects:

> **Set manufacturing components once, then production is fast; price/cost changes flow automatically into cost/margin views; component changes are easy; temporary substitutes do not corrupt the master recipe.**

---

# 612. Agent Must Read

At minimum:

```text
01
02
03
05
06
07
09
13
14
15
16
17
22
24
25
26
all AGENTS.md
approved ADRs
actual contracts
```

Quality pack once created.

---

# 613. Implementation Sequence

Recommended:

```text
MF00 Repository / Dependency Verification
MF01 Manufacturing Module Boundary
MF02 Product Manufacturing Profile / Plant
MF03 BOM Core
MF04 BOM Versioning / Activation
MF05 Formula / Process Foundation
MF06 Routing / Operations
MF07 Production Definition Resolver
MF08 Substitute Groups
MF09 Substitute Impact Preview
MF10 Current Cost Provider Integration
MF11 Cost Roll-Up / Reverse Usage
MF12 Product Profitability
MF13 Production Order / Snapshot
MF14 Material Requirements
MF15 Inventory Reservation / Issue / Consumption / Return
MF16 Output / Partial Completion
MF17 Scrap / Yield
MF18 Work Centers / Resources
MF19 Capacity / Calendars
MF20 Shop Floor
MF21 Actual Labor / Machine / Overhead Cost
MF22 Planned vs Actual / Variance
MF23 WIP / Close
MF24 Lot / Serial Genealogy
MF25 MRP Demand / Netting
MF26 Planned Production
MF27 Planned Purchase / Transfer Integration
MF28 Multi-Level MRP / Pegging
MF29 Lead Time / Delay Propagation
MF30 MPS
MF31 Subcontracting
MF32 Maintenance Integration
MF33 Quality Integration Seam
MF34 Sales / Make-to-Order
MF35 Process Batch / Co-/By-Products
MF36 Migration
MF37 Security / Permissions / Audit
MF38 Performance / Scale
MF39 Observability
MF40 Runbooks / Recovery
MF41 Manufacturing Engineer Acceptance
MF42 Planner Acceptance
MF43 Shop Floor Acceptance
MF44 Cost Accountant / Profitability Acceptance
MF45 Inventory / Purchasing Reconciliation
MF46 Cross-Module E2E
MF47 Release Gate
```

---

# 614. MF00 Goal

Inspect actual repository contracts.

---

# 615. MF00 Must Verify

```text
Catalog ProductVariant/UOM
Inventory availability/reservation/movement/valuation
Pricing cost/selling-price contracts
Purchasing planned-demand seam
Sales demand contract
Ledger semantic facts
HR employee/resource
Fixed Asset resource ref
Service downtime
Platform approvals
Numbering
Files
Audit
Outbox/Inbox
Jobs
RLS
```

---

# 616. MF00 Exit Gate

No duplicate domain planned.

---

# 617. MF01 Goal

Create module:

```text
Todir.Modules.Manufacturing.*
Todir.Manufacturing.Contracts
business_manufacturing
ManufacturingDbContext
RLS
architecture tests
```

---

# 618. MF02 Goal

Plant/product manufacturing profile.

---

# 619. MF02 Exit Gate

Manufacturing settings do not pollute Catalog.

---

# 620. MF03 Goal

Fast BOM creation/edit.

---

# 621. MF03 Acceptance

User can define Chair once and never re-enter normal component list per order.

---

# 622. MF04 Goal

Revision/history.

---

# 623. MF04 Exit Gate

Used revision never destructively edited.

---

# 624. MF05 Goal

Process manufacturing foundation.

---

# 625. MF05 Exit Gate

Formula/yield/multiple output architecture works without affecting simple discrete UX.

---

# 626. MF06 Goal

Routing/operations foundation.

---

# 627. MF06 Exit Gate

Setup/run separate.

---

# 628. MF07 Goal

Deterministic Production Definition resolution.

---

# 629. MF07 Exit Gate

Product/plant/date/qty chooses exactly one valid technique or fails clearly.

---

# 630. MF08 Goal

Approved alternatives.

---

# 631. MF08 Exit Gate

Substitute priority/effective date/ratio represented.

---

# 632. MF09 Goal

Impact preview.

---

# 633. MF09 Exit Gate

Cost/margin/stock/lead-time impact available before apply.

---

# 634. MF10 Goal

Canonical cost basis.

---

# 635. MF10 Exit Gate

No BOM cost master; Inventory owns cost.

---

# 636. MF11 Goal

Incremental cost roll-up.

---

# 637. MF11 Exit Gate

Component cost change updates affected products quickly.

---

# 638. MF12 Goal

Profitability.

---

# 639. MF12 Exit Gate

Margin/markup correctly labeled and source-drillable.

---

# 640. MF13 Goal

Production Order + frozen definition snapshot.

---

# 641. MF13 Exit Gate

Master changes never mutate released order.

---

# 642. MF14 Goal

Material requirement layer.

---

# 643. MF14 Exit Gate

Requirement has no physical stock effect.

---

# 644. MF15 Goal

Inventory execution.

---

# 645. MF15 Exit Gate

Reservation/issue/consume/return distinct and idempotent.

---

# 646. MF16 Goal

Partial/good output.

---

# 647. MF16 Exit Gate

Partial completion and inventory receipts reconcile.

---

# 648. MF17 Goal

Scrap/yield.

---

# 649. MF17 Exit Gate

Good, scrap, loss, rework separate.

---

# 650. MF18 Goal

Work Centers/resources.

---

# 651. MF18 Exit Gate

Machine can reference Asset; no duplicate asset master.

---

# 652. MF19 Goal

Capacity.

---

# 653. MF19 Exit Gate

Material-ready order can still show capacity block correctly.

---

# 654. MF20 Goal

Shop Floor.

---

# 655. MF20 Exit Gate

Operator can execute work with minimal taps and without cost/accounting clutter.

---

# 656. MF21 Goal

Actual resource cost.

---

# 657. MF21 Exit Gate

No salary leakage.

---

# 658. MF22 Goal

Variance.

---

# 659. MF22 Exit Gate

Variance decomposed and explainable.

---

# 660. MF23 Goal

WIP/close.

---

# 661. MF23 Exit Gate

No unresolved material/output/cost closes silently.

---

# 662. MF24 Goal

Genealogy.

---

# 663. MF24 Exit Gate

Both trace directions work.

---

# 664. MF25 Goal

MRP net requirements.

---

# 665. MF25 Exit Gate

Stock/open supply/substitutes/lead-time net correctly.

---

# 666. MF26 Goal

Planned Production.

---

# 667. MF26 Exit Gate

Planned != authorized execution.

---

# 668. MF27 Goal

Purchasing/transfer suggestions.

---

# 669. MF27 Exit Gate

Manufacturing does not own PO or Inventory transfer.

---

# 670. MF28 Goal

Multi-level demand/pegging.

---

# 671. MF28 Exit Gate

Parent demand traces to child supply.

---

# 672. MF29 Goal

Delay propagation.

---

# 673. MF29 Exit Gate

Lower-level shortage impact visible to parent/customer risk.

---

# 674. MF30 Goal

MPS.

---

# 675. MF30 Exit Gate

Optional plan layer, no forced SME complexity.

---

# 676. MF31 Goal

Subcontracting.

---

# 677. MF31 Exit Gate

Purchasing/Inventory source ownership preserved.

---

# 678. MF32 Goal

Maintenance integration.

---

# 679. MF32 Exit Gate

Downtime affects capacity without Manufacturing owning maintenance.

---

# 680. MF33 Goal

Quality seam.

---

# 681. MF33 Exit Gate

Quality can gate production without duplicate inspection truth.

---

# 682. MF34 Goal

Sales/make-to-order.

---

# 683. MF34 Exit Gate

Customer demand traceable, commercial ownership remains Sales.

---

# 684. MF35 Goal

Process/co-/by-products.

---

# 685. MF35 Exit Gate

Discrete users unaffected.

---

# 686. MF36 Goal

Migration.

---

# 687. MF36 Exit Gate

BOM/open orders/material state reconcile.

---

# 688. MF37 Goal

Security.

---

# 689. MF37 Attack Cases

```text
Cross-tenant BOM read
Cross-plant Production release
Forged substitute product
Forged Inventory location
Unauthorized cost/margin read
Unauthorized BOM activation
Double consumption replay
Double output receipt
Close with unresolved stock
Bypass quality hold
Use machine under maintenance override without authority
```

---

# 690. MF37 Exit Gate

RLS + server auth + permissions pass.

---

# 691. MF38 Goal

Performance/scale.

---

# 692. MF38 Load Tests

```text
20M BOM lines
5M production orders
large MRP
large where-used
high-frequency cost changes
shop-floor concurrency
```

---

# 693. MF39 Goal

Observability.

---

# 694. MF40 Runbooks

Required:

```text
Wrong BOM revision
Wrong temporary substitute
Duplicate material consumption
Duplicate output receipt
Incorrect cost projection
MRP duplicate planned supply
Production stuck In Progress
Inventory consumption rejected
Output received but Production update failed
Production updated but Inventory unknown result
Quality hold mismatch
Machine downtime mismatch
Subcontract receipt mismatch
Projection rebuild
DR restore validation
```

---

# 695. Forbidden Runbook

Never normal fix:

```text
UPDATE production_order SET state='Closed'
UPDATE bom_line SET component_id=...
UPDATE production_cost SET total=...
DELETE material_consumption...
```

Use semantic correction.

---

# 696. MF41 Manufacturing Engineer Acceptance

Must validate:

```text
Create Chair BOM once
change one component permanently
compare revisions
create approved substitute
preview impact
activate revision
```

---

# 697. MF42 Planner Acceptance

Must validate:

```text
1000-unit demand
multi-level explosion
shortages
approved substitute
planned production
planned purchase
late supply
capacity warning
```

---

# 698. MF43 Shop Floor Acceptance

Worker:

```text
open queue
start
report material
report good qty
report scrap
pause for breakdown
resume
complete
```

No unnecessary ERP complexity.

---

# 699. MF44 Cost Accountant Acceptance

Can answer:

```text
What is current estimated unit cost?
Why did it change?
What did last batch actually cost?
What variance caused difference?
What is current margin?
```

---

# 700. MF45 Cross-Module Reconciliation

Validate:

```text
Manufacturing material consumption
= Inventory production consumption

Production output
= Inventory production receipts

Manufacturing material cost
= Inventory issue valuation

Planned purchases
= Purchasing accepted requirements

Subcontract cost
= Purchasing/Invoicing source allocation

Machine identity
= Fixed Assets ref

Downtime
= Service/Maintenance source

Accounting facts
= Ledger postings
```

Zero unexplained differences.



# 701. MF46 E2E — Simple Production

```text
Product configured once
→ Produce 100
→ automatic components
→ availability check
→ consume
→ receive finished goods
→ current cost / actual cost visible
```

---

# 702. MF46 E2E — Permanent Change

```text
Foam A
→ engineering/material change
→ preview
→ new revision
→ future orders use new revision
→ old orders/history unchanged
```

---

# 703. MF46 E2E — Temporary Substitute

```text
Preferred short
→ approved substitute suggested
→ impact preview
→ apply to one Production Order
→ consume substitute
→ BOM master unchanged
```

---

# 704. MF46 E2E — Cost Change

```text
Purchase/Inventory cost changes
→ affected cost projections refresh
→ margin alert
→ Selling Price unchanged until commercial decision
```

---

# 705. MF46 E2E — MRP

```text
Sales/stock demand
→ MRP
→ BOM explosion
→ net stock/supply
→ substitution
→ planned production/purchase/transfer
→ planner review
→ firm
```

---

# 706. MF46 E2E — Multi-Level

```text
Finished Good
→ Subassembly
→ Raw Material
→ purchase/manufacture requirements
→ execution
→ final output
```

---

# 707. MF46 E2E — Capacity/Maintenance

```text
Production demand
→ routing capacity
→ Maintenance downtime
→ overload
→ reschedule/alternative resource
```

---

# 708. MF46 E2E — Process Batch

```text
Formula
→ scaled batch
→ ingredients
→ operations
→ primary output
→ by-product
→ loss
→ yield
→ cost
```

---

# 709. MF46 E2E — Subcontract

```text
Production need
→ outsourced operation/product
→ Purchasing PO
→ components via Inventory if needed
→ supplier execution
→ receipt
→ production cost
```

---

# 710. MF46 Exit Gate

Cross-module integration uses real PostgreSQL/Testcontainers and canonical contracts.

---

# 711. MF47 Release Gate

Manufacturing does not ship because BOM CRUD and Production Order CRUD exist.

It ships when:

```text
repeated production is fast
master definitions are versioned
materials are correct
substitutions are safe
MRP plans correctly
inventory effects reconcile
costs are explainable
profitability is current
history is reproducible
```

---

# 712. MF47 Required Sign-Off

```text
Product Architect
Manufacturing Manager
Manufacturing Engineer
Production Planner
Shop Floor Reviewer
Inventory Lead
Purchasing Lead
Cost Accountant
Maintenance Lead
Quality Lead once 27 exists
Security Reviewer
Performance Engineer
QA Lead
```

---

# 713. MF47 Mandatory Evidence

```text
Architecture boundary tests
RLS/security report
BOM revision tests
substitution tests
cost roll-up tests
margin tests
Inventory reconciliation
MRP netting tests
multi-level explosion tests
capacity tests
idempotency tests
shop-floor acceptance
process manufacturing tests
subcontract tests
performance benchmarks
runbooks
known limitations
```

---

# 714. Build Now — MUST

```text
Manufacturing Product Profile
Plant
Production Definition
BOM
BOM Versioning
Formula foundation
Routing foundation
Operations
Work Centers foundation
Approved Substitute Groups
Substitution Preview
Current Cost Roll-Up
Reverse Component Usage
Product Profitability
Production Orders
Definition Snapshot
Material Requirements
Inventory reservation/issue/consume/return integration
Partial Production
Output Receipt
Scrap / Yield
Planned vs Actual Cost
Variance foundation
Shop Floor
MRP
Planned Production
Planned Purchase
Planned Transfer
Multi-level explosion
Lead time / delay propagation
Lot/Serial genealogy
Subcontracting
Maintenance integration
Quality integration seam
Permissions/RLS/Audit
Migration
Performance
Runbooks
Acceptance gates
```

---

# 715. Build Later / Evidence-Based

```text
Advanced finite APS
Optimizer-based sequencing
Advanced MES integration
Machine PLC/IoT
OEE automation
Advanced PLM
Potency
Catch Weight
Advanced batch optimization
Advanced repetitive manufacturing
Kanban/Lean depth
Detailed tool/mold lifecycle
Advanced engineering configuration
Advanced labor standards
Advanced standard-cost accounting
```

---

# 716. Final Source-of-Truth Matrix

```text
Product / Variant / UOM               → Catalog
BOM / Formula / Routing               → Manufacturing
Production Definition                 → Manufacturing
Material Substitute Rules             → Manufacturing
Production Order                      → Manufacturing
Production Execution                  → Manufacturing
MRP / Planned Production              → Manufacturing
Production Cost Composition           → Manufacturing
Current Product Manufacturing Cost    → Manufacturing projection from canonical sources

Physical Stock                        → Inventory
Reservation                           → Inventory
Lot / Serial                          → Inventory
Inventory Valuation                   → Inventory
Inventory Issue Cost                  → Inventory

Selling Price                         → Pricing
Purchase Order / Supplier Commitment  → Purchasing
Sales Customer Demand                 → Sales
Employee                              → HR
Machine Fixed Asset                   → Fixed Assets
Maintenance Work / Downtime           → Service & Maintenance
Quality Inspection / NCR              → Quality
Journal Entry                         → Ledger
```

---

# 717. Final Repeated-Production Rule

> **Once the manufacturing definition is configured, creating a normal Production Order requires product + quantity + context, not manual component re-entry.**

---

# 718. Final BOM Rule

> **BOM/Formula defines quantities and relationships; changing component prices never requires editing the BOM.**

---

# 719. Final Revision Rule

> **Permanent manufacturing changes create versioned revisions; released and historical production never changes silently.**

---

# 720. Final Substitute Rule

> **Approved temporary substitutes belong to planning/execution context and never overwrite the reusable master definition.**

---

# 721. Final Substitute Decision Rule

> **A substitute decision considers availability, priority, replacement ratio, cost, lead time, quality and effective dates—not price alone.**

---

# 722. Final Cost Rule

> **Current estimated manufacturing cost is derived from canonical material/resource cost sources and is a rebuildable calculation, not manually maintained BOM data.**

---

# 723. Final Cost-Basis Rule

> **Latest Purchase Price, Inventory Valuation Cost, Standard Cost, Expected Purchase Cost and Actual Production Cost are distinct facts and must be labeled.**

---

# 724. Final Cost-History Rule

> **Today's source-cost change can change today's estimated manufacturing cost but never rewrites yesterday's closed Production Order actual cost.**

---

# 725. Final Profitability Rule

> **Every manufacturing profitability number identifies selling-price basis, cost basis, currency and date; Margin and Markup remain mathematically distinct.**

---

# 726. Final Cost-Change Rule

> **When a component's canonical cost changes, TODIR refreshes only affected manufacturing definitions through a reverse usage dependency index.**

---

# 727. Final Impact-Preview Rule

> **Before a material/revision change is activated, TODIR shows unit-cost, order-cost, margin, availability and lead-time impact.**

---

# 728. Final Availability Rule

> **TODIR should be able to answer both “How many can I produce now?” and “What am I missing to produce N units?”**

---

# 729. Final Requirement Rule

> **A Material Requirement expresses production need only; it never changes physical stock.**

---

# 730. Final Inventory Rule

> **Reservation, Issue, Consumption, Return, Scrap and Finished-Goods Receipt remain Inventory-owned physical truths.**

---

# 731. Final Backflush Rule

> **Backflush is a simplified execution policy over canonical Inventory movements; it is not an excuse to skip stock history.**

---

# 732. Final Production Snapshot Rule

> **Release freezes the chosen manufacturing definition context so later master-data edits cannot alter active execution invisibly.**

---

# 733. Final MRP Rule

> **MRP proposes time-phased net supply after considering demand, on-hand, open supply, multi-level requirements, substitutes and lead time; recommendations are not execution documents.**

---

# 734. Final Planned-Order Rule

> **A Planned Production Order becomes an executable Production Order only through firming/authorization.**

---

# 735. Final Purchasing Rule

> **MRP can request purchase supply; Purchasing owns the Purchase Order and supplier commitment.**

---

# 736. Final Transfer Rule

> **MRP can recommend a warehouse transfer; Inventory owns the physical transfer.**

---

# 737. Final Capacity Rule

> **Material availability and production capacity are separate readiness dimensions; having all materials does not prove the order can be produced on time.**

---

# 738. Final Work-Center Rule

> **Work Center represents manufacturing capacity; a company machine may be a Fixed Asset used by that Work Center but is not duplicated inside Manufacturing.**

---

# 739. Final Maintenance Rule

> **Maintenance/downtime comes from Service & Maintenance and may reduce production capacity; Manufacturing never creates a second maintenance engine.**

---

# 740. Final HR Rule

> **Production effort explains time spent on manufacturing; HR Attendance explains workforce attendance. They are never the same fact.**

---

# 741. Final Quality Rule

> **Manufacturing can require a quality gate, but Quality owns inspection result, NCR, disposition and CAPA truth.**

---

# 742. Final Yield Rule

> **Expected Yield and Actual Yield remain separate so process performance and material loss are measurable.**

---

# 743. Final Scrap Rule

> **Good output, normal loss, scrap, by-product, co-product and rework are separate classifications and never collapse into one Waste field.**

---

# 744. Final WIP Rule

> **Manufacturing explains operational WIP, Inventory explains physical value/movement, and Ledger owns accounting WIP postings.**

---

# 745. Final Traceability Rule

> **When lot/serial tracking is enabled, TODIR must answer input-to-output and output-to-input genealogy without reconstructing history manually.**

---

# 746. Final Subcontract Rule

> **Manufacturing owns the technical work requirement, Purchasing owns the supplier commitment, and Inventory owns components/output movements.**

---

# 747. Final Small-Company Rule

> **A small manufacturer should be able to define a product once, type a quantity, see shortages/cost/margin, press Produce, and finish the job without learning enterprise MRP terminology.**

---

# 748. Final Enterprise Rule

> **A larger manufacturer can enable MRP, routing, capacity, shop floor, substitutions, process batches, genealogy, subcontracting, maintenance and quality without replacing the production core.**

---

# 749. Final Agent Rule

For every implementation decision, the Agent must answer:

> **Which product are we making, which approved production definition applies, what exact materials/operations are required, which alternatives are permitted, what is available, what changed, what cost basis is being shown, which inventory movements actually occurred, what output was produced, what remains in WIP, and which owning module supplies each external truth?**

If any answer is unclear, implementation is incomplete.

---

# 750. Final User-Speed Rule

Before adding any extra production field or step, ask:

```text
Does the user need to choose this every production run?
Or can TODIR infer it safely from the approved definition?
```

Prefer safe inference.

---

# 751. Final “Change One Material” Rule

The product workspace must make this workflow first-class:

```text
Open Product
→ Manufacturing
→ Component
→ Replace
→ Preview Cost/Stock/Margin
→ Save New Revision
```

not require rebuilding the product.

---

# 752. Final “Price Changed” Rule

If supplier cost changes:

```text
Purchase/Inventory valuation changes
→ Manufacturing estimate refreshes
→ Profitability refreshes
```

No user edits BOM price fields.

---

# 753. Final “Temporary Shortage” Rule

If a preferred component is unavailable:

```text
TODIR shows approved alternatives
+ availability
+ replacement ratio
+ cost impact
+ lead time
```

and the user makes one clear decision.

---

# 754. Final “What Can I Produce?” Rule

The product workspace provides:

```text
Max producible with preferred materials
Max producible with approved substitutes
Current bottleneck
```

---

# 755. Final “Why Did Cost Change?” Rule

The cost workspace must answer:

```text
Which components/resources changed?
By how much?
Since when?
Which products are affected?
How did margin change?
```

---

# 756. Final Architecture Closure

The module must remain:

```text
Simple outside.
Powerful inside.
Definition once.
Automatic reuse.
Versioned change.
Approved substitution.
Canonical Inventory cost.
Fast targeted recalculation.
Explainable MRP.
Source-owned stock/accounting.
History preserving.
```

A Manufacturing module with a pretty Production Order screen but requiring repeated component entry, duplicating stock valuation, or hiding cost changes fails this pack.

---

# 757. Official Competitor Research References

Current patterns reviewed for this pack include:

```text
Odoo 19 Manufacturing:
https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/manufacturing.html
https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/manufacturing/subcontracting.html

Microsoft Dynamics 365 Supply Chain:
https://learn.microsoft.com/en-us/dynamics365/supply-chain/master-planning/planning-optimization/production-planning
https://learn.microsoft.com/en-us/dynamics365/supply-chain/production-control/substitute-items-bom-lines
https://learn.microsoft.com/en-us/dynamics365/supply-chain/master-planning/item-substitution

Oracle Fusion Manufacturing:
https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/faumf/overview-of-production-process-design.html
https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/faumf/how-you-create-work-definitions.html

SAP S/4HANA:
https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/21aead0c98bd4755abdacd91c99e3393/c234b8535c39b44ce10000000a174cb4-401.html

NetSuite:
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N2345383.html
https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N2351557.html
```

---

# 758. No Separate Manufacturing Subfeature Packs

Do **not** create separate architecture packs for:

```text
BOM
MRP
Production Orders
Work Centers
Shop Floor
Material Substitution
Manufacturing Cost
WIP
Subcontracting
Production Traceability
```

They are all part of `26 Manufacturing / MRP`.

Separate future modules only where a real bounded context exists, such as:

```text
27 Quality Management
Advanced PLM if proven
Advanced MES if proven
```

---

# 759. Release Summary

TODIR Manufacturing is acceptable only when the real daily experience is:

```text
Configure once
→ produce repeatedly
→ system knows components
→ system knows current cost
→ system knows shortages
→ system suggests approved alternatives
→ user sees impact immediately
→ history remains correct
```

---

**END OF FILE — 26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
