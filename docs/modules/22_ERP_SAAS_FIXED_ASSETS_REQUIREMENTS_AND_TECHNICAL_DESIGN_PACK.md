# 22 — TODIR ERP SaaS Fixed Assets / Enterprise Asset Register / Custody / Depreciation Requirements & Technical Design Pack

**File:** `22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + ENTERPRISE ASSET REGISTER + FINANCIAL ASSET LIFECYCLE + CUSTODY / LOCATION CONTROL + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Finance/Accounting Lead, Fixed Asset Accountant, IT/Asset Custodian, Operations/Facilities Lead, HR Lead, Purchasing Lead, Inventory Lead, Tax/Compliance Lead, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — BALANCE-SHEET VALUE + LONG-LIVED HISTORY + CUSTODY + PHYSICAL CONTROL + DEPRECIATION + DISPOSAL**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream boundaries:
- Inventory already states warranty/service data can belong to an Asset capability while Inventory keeps Serial movement history.
- HR already states an Asset can be assigned/responsible to Employee while Fixed Assets owns Asset history.
- Accounting already states Fixed Assets owns Asset Register/depreciation calculation and Accounting displays/posting/reconciliation.
- Platform Files owns binary documents.
- Ledger owns Journal Entries and account determination.
- Fixed Assets must therefore own the canonical Asset identity/lifecycle without duplicating Product, Inventory, Employee, Journal or Supplier Invoice truth.

---

# 0. Executive Decision

TODIR Fixed Assets is the company's **canonical enterprise asset register and financial/operational lifecycle system for long-lived controlled assets**.

Its central question is:

> **What assets does the company own or control, what kind of assets are they, where did they come from, where are they now, who is responsible for them, what is their financial carrying value, what happened throughout their life, and how will TODIR prove every change years later?**

The module must serve all of these realities correctly:

```text
Land
Building
Leasehold Improvement
Vehicle
Laptop
Phone
Production Machine
Generator
Furniture
Office Equipment
Tool
Asset Under Construction / CIP
Capitalized Software / Intangible Asset
Grouped low-value homogeneous assets
Componentized high-value assets
Opening legacy assets
```

It must not assume:

```text
every asset depreciates
every asset has a serial
every asset is a Product
every asset is held by an Employee
every asset came from Inventory
every asset came from one Invoice line
every asset has one accounting book
every asset has one physical component
```

---

# 1. Business Correctness Before Screens

This pack is intentionally ordered:

```text
Real-world asset concepts
→ ownership boundaries
→ asset types/natures
→ acquisition/capitalization
→ custody/location/responsibility
→ financial books/depreciation
→ additions/adjustments
→ verification
→ disposal
→ migration
→ data model
→ APIs
→ security/performance
→ tests
→ implementation roadmap
→ UI guidance
```

The Asset screen can change later.

Wrong asset semantics are expensive to repair after years of depreciation, transfers and audits.

---

# 2. Absolute Domain Distinctions

```text
Fixed Asset != Catalog Product
Fixed Asset != Inventory Stock
Fixed Asset != Inventory Serial
Fixed Asset != Purchase Order
Fixed Asset != Supplier Invoice
Fixed Asset != Expense Claim
Fixed Asset != Journal Entry
Fixed Asset != Maintenance Work Order
Fixed Asset != Employee
Fixed Asset != Location
Fixed Asset != Depreciation Schedule Row
```

Also:

```text
Ownership != Custody
Custody != Responsibility
Responsibility != Cost Owner
Cost Owner != Physical Location
Acquisition != Capitalization
Capitalization != Payment
Capitalization != Placed In Service
Disposal != Delete
Asset Sale != Payment
Book Value != Editable Field
```

---

# 3. Fixed Asset Is Separate From Product

Locked:

```text
foundation_catalog.ProductVariant
!=
business_assets.FixedAsset
```

No shared table.

No inheritance from Product.

No assumption that every Asset has `ProductVariantId`.

---

# 4. Why Product And Asset Must Be Separate

Catalog answers:

```text
What product/service is this?
What SKU?
What Variant?
What UOM?
What Barcode?
```

Assets answers:

```text
Which specific company-controlled asset is this?
What did it cost?
When was it capitalized?
Where is it?
Who has custody?
How is it depreciated?
What is its carrying value?
What happened to it?
```

---

# 5. Optional Product Relationship

For assets originating from purchased/stocked products:

```text
FixedAsset
→ optional ProductVariant reference
→ optional Inventory Serial reference
```

as source/integration references.

Neither is required.

---

# 6. Land Proves Product Link Cannot Be Required

A parcel of land may have:

```text
no SKU
no ProductVariant
no Inventory Serial
```

but is still a valid Fixed Asset.

---

# 7. Laptop Proves Product And Asset Can Coexist

Example:

```text
Catalog:
Dell Latitude 7450
SKU DELL-7450

Inventory:
Serial A001

Assets:
LAP-000145
Source ProductVariant = DELL-7450
Source Serial = A001
Custodian = Ahmed
Book Value = ...
```

Three separate truths.

---

# 8. Asset Definition

A Fixed Asset represents:

> **A uniquely controlled or grouped long-lived economic/operational resource whose identity, custody/location, financial value and lifecycle must be tracked independently from ordinary stock.**

---

# 9. Asset Nature

Recommended generic nature:

```text
Tangible
Intangible
CIP
RightOfUseLater
```

with Category defining business detail.

---

# 10. Tangible Examples

```text
Land
Buildings
Vehicles
Machinery
IT Equipment
Phones
Furniture
Office Equipment
Tools
Leasehold Improvements
```

---

# 11. Intangible Examples

Possible:

```text
Capitalized Software
Patents
Licenses/Rights
Development Cost where applicable
```

Do not assume every software subscription is an Asset.

---

# 12. CIP

`Construction In Progress / Assets Under Construction` is first-class.

CIP collects capitalizable costs before the asset is ready for use.

---

# 13. Land

Land is a Fixed Asset even if:

```text
Depreciation = disabled
Custodian Employee = none
Serial = none
Product = none
```

It still requires:

```text
legal entity
cost
ownership/source
location/property details
documents
history
book value
```

---

# 14. Land vs Building

If one purchase contains both:

```text
Land
+
Building
```

TODIR must allow cost allocation into separate assets because their depreciation treatment can differ.

---

# 15. Land Improvement

Examples:

```text
Fencing
Paving
Site preparation
Irrigation infrastructure
External lighting
```

may be capitalized separately from Land according to policy.

---

# 16. Buildings

Building can be:

```text
single asset
or
parent asset with significant components
```

depending company/accounting policy.

---

# 17. Component Accounting

Example:

```text
Building BLD-001
├ Structure
├ Elevators
├ HVAC
└ Generator
```

Each component may have different useful life.

---

# 18. Component Is Not Always Required

Small company can track Building as one Asset if policy permits.

Progressive complexity.

---

# 19. Vehicles

Vehicle can require:

```text
VIN
Plate
Engine/Chassis references
registration documents
custodian/pool assignment
location
insurance
warranty
```

but these do not belong in every Asset row.

---

# 20. IT Assets

Laptop/Phone often require:

```text
Asset Tag
Serial/IMEI
Employee Custody
Accessories
Warranty
Return Condition
```

---

# 21. Machinery

Machine often requires:

```text
Plant/Location
Responsible Department
Serial
Operational Responsible
Maintenance integration
component relationships
```

---

# 22. Furniture

Can be:

```text
Individual
or
Grouped
```

depending value/control need.

---

# 23. Grouped Assets

Example:

```text
200 identical office chairs
```

may be one Grouped Asset when individual custody/serial tracking adds no value.

---

# 24. Individual Assets

Use when:

```text
unique serial
employee custody
high value
maintenance individually
regulatory identity
individual disposal
```

matters.

---

# 25. Tracking Granularity

Recommended:

```text
Individual
Grouped
```

Category can suggest default.

---

# 26. Grouped Asset Units

Grouped Asset tracks:

```text
units
financial value
distribution/location if needed
```

without pretending each unit has individual serial/custody.

---

# 27. Split Grouped Asset

If part of grouped population later requires separate lifecycle:

```text
Split Asset
```

creates child/new asset(s) with allocated book basis and history.

No silent quantity edit.

---

# 28. Ownership Dimension

Asset Legal Owner is the Legal Entity whose books/control own the Asset.

---

# 29. Legal Owner Is Not Employee Custodian

Example laptop:

```text
Legal Owner = TODIR Egypt LLC
Custodian = Ahmed
```

Ahmed does not own the laptop legally.

---

# 30. Five Different Responsibility Concepts

TODIR must distinguish:

```text
1. Legal Owner
2. Custodian
3. Operational Responsible
4. Maintenance Responsible
5. Cost Owner
```

and also:

```text
6. Physical Location
```

---

# 31. Legal Owner

Always Legal Entity for company books.

---

# 32. Custodian

Person/entity physically accountable for possession.

Can be:

```text
Employee
Department/Pool
None
```

depending Asset Category.

---

# 33. Operational Responsible

Who is responsible for use/operation.

Could be:

```text
Employee
Department
Operations team
```

---

# 34. Maintenance Responsible

Belongs to Maintenance capability/team/technician when Maintenance active.

Fixed Assets can retain current reference/projection.

---

# 35. Cost Owner

Accounting/management dimension:

```text
Department
Cost Center
Project?
```

may differ from Custodian.

---

# 36. Physical Location

Where the Asset physically exists.

Examples:

```text
Cairo HQ / Floor 3 / IT Room
Alexandria Branch
Factory / Line 4
Parcel address
Employee remote custody
```

---

# 37. Custody Is Optional

Land:

```text
Custodian = none
```

valid.

---

# 38. Location Can Be Required Without Custodian

Land/Building/Machine commonly require physical location even with no Employee custody.

---

# 39. Employee Custody Is Valid

Locked HR boundary:

```text
Fixed Assets owns Asset/history.
HR owns Employee.
```

Assets references `EmployeeId` through HR contract.

---

# 40. Employee User Account Not Required

Employee can hold Asset without TODIR login.

---

# 41. Custody Mode By Category

Recommended configuration:

```text
None
Employee
Department
Pool
Flexible
```

---

# 42. Example Category Behavior Matrix

```text
Land:
  Custody=None
  Location=Required
  Depreciation=UsuallyOff

Laptop:
  Custody=Employee/Flexible
  Location=Optional/Derived
  Serial=Recommended
  Depreciation=On

Machine:
  Custody=Department/Flexible
  Location=Required
  Serial=Recommended
  Depreciation=On

CIP:
  Custody=None
  Location=Optional/ProjectSite
  Depreciation=Off until capitalization
```

---

# 43. Asset Assignment Is Historical Event

Do not only mutate:

```text
asset.employee_id
asset.location_id
```

and lose prior history.

---

# 44. Asset Assignment

Concept:

```text
AssetAssignment
{
    Id
    AssetId

    CustodianEmployeeId?
    ResponsibleDepartmentId?
    LocationId?
    CostCenterRef?

    EffectiveFrom
    EffectiveTo?

    Reason
    SourceRef?
    ActorId
}
```

---

# 45. Current Assignment Projection

For speed Asset can expose:

```text
CurrentCustodian
CurrentLocation
CurrentDepartment
CurrentCostCenter
```

as rebuildable/current projection.

History remains authority.

---

# 46. Transfer

Same Legal Entity operational transfer:

```text
Old Assignment ends
→ New Assignment begins
```

with one semantic command.

---

# 47. Employee-to-Employee Transfer

Example:

```text
Ahmed
→ Return/Handover
→ Mohamed
```

preserve both acknowledgements if required.

---

# 48. Department Pool

Vehicle/tool can be:

```text
Custodian = Sales Vehicle Pool
```

without assigning one employee permanently.

---

# 49. Handover

Optional but important for individually controlled assets.

---

# 50. Asset Handover Record

Can capture:

```text
From
To
Asset
Date/Time
Condition
Accessories
Notes
Acknowledgement
File evidence
```

---

# 51. Receipt Acknowledgement

Employee can:

```text
Confirm Received
```

through Self-Service.

No login → authorized administrator records paper/electronic evidence.

---

# 52. Return Acknowledgement

Capture:

```text
ReturnedAt
ReturnCondition
MissingAccessories?
DamageNotes?
ReceivedBy
```

---

# 53. Return Condition Does Not Automatically Create Financial Loss

Damage may create:

```text
maintenance request
impairment review
employee accountability process
```

separately.

---

# 54. HR Employee Workspace

HR may compose read-only Asset section:

```text
Assigned Company Assets
```

from Fixed Assets.

HR does not copy asset records.

---

# 55. HR Offboarding Integration

Offboarding can query:

```text
Assets currently assigned to Employee
```

and show exceptions.

---

# 56. Offboarding Policy

Company can configure:

```text
Warn
RequireReturn
RequireExceptionApproval
```

before HR completes selected offboarding gate.

---

# 57. HR Cannot Return Asset By Direct DB Mutation

HR action delegates to Assets command.

---

# 58. Asset Location Ownership

Physical Asset Location taxonomy may be Assets-owned or consume Organization/Inventory location reference depending type.

Do not reuse Inventory bin semantics blindly for Land/Building.

---

# 59. Asset Location Model

Recommended Assets-specific hierarchical operational location:

```text
Country/Site
Building
Floor
Room/Area
Other
```

with optional references:

```text
Organization Branch
Inventory Warehouse/Location
```

when meaningful.

---

# 60. Asset Location Is Not Inventory Location

They can reference/match, but semantics differ.

A parcel of land has no warehouse bin.

---

# 61. Real Estate Profile

Typed optional profile for:

```text
Land
Building
```

---

# 62. Real Estate Profile Fields

Potential:

```text
Property/Parcel reference
Address reference/text
Area
Area UOM
Title/Deed reference
Ownership percentage
Legal description
Registration reference
```

country-specific registry fields via Country Pack/Custom Fields where possible.

---

# 63. Vehicle Profile

Potential:

```text
VIN
Plate
Engine/Chassis reference
Make/Model descriptive refs
Registration expiry
```

Keep only asset-control fields, not fleet-management engine.

---

# 64. Equipment Profile

Potential:

```text
Manufacturer Serial
Model
Commissioning reference
Operational criticality?
```

Maintenance owns maintenance-specific detail later.

---

# 65. Intangible Profile

Potential:

```text
Right/License reference
Effective dates
Renewal/end date
Legal document refs
```

---

# 66. Avoid Giant Sparse Asset Table

Do not create:

```text
land_area
vehicle_plate
engine_number
phone_imei
software_license_key
building_floors
...
```

all in `fixed_asset`.

Use core + typed profiles/custom fields.

---

# 67. Asset Category

First-class configuration.

Examples:

```text
Land
Buildings
Vehicles
Machinery
Computers
Phones
Furniture
Office Equipment
Tools
Leasehold Improvements
CIP
Capitalized Software
```

---

# 68. Category Defaults

Can define:

```text
AssetNature
TrackingGranularity
CustodyMode
LocationRequirement
SerialPolicy
AssetTagPolicy
DefaultBooks
DefaultUsefulLife by Book
DefaultDepreciationMethod
ResidualValuePolicy
CapitalizationPolicy
AccountingRoleSet
VerificationPolicy
```

---

# 69. Category Defaults Are Defaults

Asset accountant can override permitted fields according to policy.

---

# 70. Category Does Not Equal GL Account

Store semantic roles.

Ledger account determination owns account IDs.

---

# 71. Capitalization Policy

Defines when spend becomes Asset rather than ordinary Expense.

---

# 72. Capitalization Threshold

Can use:

```text
Minimum amount
Category
Currency
Effective date
```

plus company/accounting judgment.

---

# 73. Threshold Is Not Automatic Truth

A 500 item could still require controlled asset tracking operationally.

A high-value spend could still be Expense if criteria not met.

Policy assists; accountant approves.

---

# 74. Asset Recognition vs Operational Tracking

TODIR may support:

```text
FinancialAsset
OperationalTrackedAsset
Both
```

carefully.

Avoid forcing every tracked laptop into financial depreciation if company policy expenses low-value equipment.

---

# 75. Operational Asset Without Asset Book

Possible future/optional:

```text
tracked custody item
financially expensed already
```

Still useful for IT/equipment control.

---

# 76. Financial Asset

Has one or more Asset Books.

---

# 77. Acquisition vs Capitalization

Acquisition source proves cost/source.

Capitalization creates/recognizes Asset financial basis.

Not identical.

---

# 78. Placed In Service

Separate date:

```text
AcquisitionDate
CapitalizationDate
PlacedInServiceDate
```

because depreciation may begin based on availability/use rule.

---

# 79. Acquisition Sources

Asset can originate from:

```text
Supplier Invoice
Purchase Receipt
Inventory capitalization/issue
Expense/Capital Spend
CIP
Opening Migration
Internal Construction
Intercompany Transfer
Donation/Other authorized source
```

---

# 80. Acquisition Source Is Typed

No free-text-only source.



# 81. Acquisition Source Allocation

Do not assume:

```text
one Invoice Line
→ one Asset
```

or the reverse.

---

# 82. Many Source Lines → One Asset

Example:

```text
Machine             1,000,000
Freight                50,000
Installation          100,000
Engineering            50,000
--------------------------------
Capitalized Asset    1,200,000
```

One Asset can receive cost from several source lines.

---

# 83. One Source Line → Many Assets

Example:

```text
10 Laptops × 50,000
```

can create:

```text
10 Individual Assets
```

with allocated cost.

---

# 84. Many-to-Many Source Model

Concept:

```text
AssetSourceAllocation
{
    Id
    AssetId
    SourceType
    SourceId
    SourceLineId?
    OriginalAmount
    CapitalizedAmount
    Currency
    FxSnapshot?
    AllocationReason
}
```

---

# 85. Source Allocation Invariant

For selected source amount:

```text
SUM(AssetSourceAllocation)
<=
EligibleCapitalizableAmount
```

unless controlled correction/reclassification exists.

---

# 86. Capitalization Queue

TODIR should provide:

```text
Potential Capitalizable Spend
```

workbench.

Sources can include:

```text
Supplier Invoice lines
Purchase receipts
Approved capital Expenses
CIP costs
Inventory issues/transfers for internal-use items
Opening import
```

---

# 87. Queue Is Review, Not Automatic Asset Creation

Suggested action:

```text
Expense
Asset
Add to Existing Asset
CIP
Ignore/Noncapitalizable
Needs Review
```

---

# 88. Queue Candidate

Concept:

```text
CapitalizationCandidate
{
    SourceType
    SourceId
    SourceLineId?
    Description
    Date
    Supplier?
    Amount
    Currency
    SuggestedCategory?
    SuggestedQuantity?
    SuggestedProductVariant?
    SuggestedSerials?
    State
}
```

---

# 89. Automatic Suggestion

Can use:

```text
Purchase category
Product profile
Amount threshold
Supplier/category
Inventory receipt serials
```

to suggest.

Never silently capitalize merely because amount exceeds threshold.

---

# 90. Capitalization Decision

Authorized accountant chooses:

```text
Create Asset(s)
Add Cost To Existing Asset
Create/Increase CIP
Expense/Not Asset
Defer
```

---

# 91. Capitalization Of Supplier Invoice

Purchasing/Invoicing owns the supplier invoice.

Assets consumes finalized source.

---

# 92. Supplier Invoice Financial Truth

Do not duplicate:

```text
Supplier
Invoice number
Tax
Payable
Payment
```

inside Assets as truth.

Store source refs/snapshots required for audit.

---

# 93. Purchase Receipt Before Invoice

Some companies place asset in service on receipt.

Architecture can support:

```text
Receipt source
→ estimated acquisition basis
→ later Invoice adjustments
```

only through explicit policy.

---

# 94. Invoice Variance After Receipt Capitalization

Potential:

```text
Estimated Cost = 100,000
Final Invoice = 102,500
```

difference becomes:

```text
Asset Cost Adjustment
```

when policy says capitalizable.

No silent rewrite.

---

# 95. Freight / Installation / Direct Costs

Can be capitalizable additions if policy/accounting rules allow.

---

# 96. Tax Treatment

Recoverable tax usually not capitalized; non-recoverable tax may be.

Tax Engine/Country rules supply facts.

Assets does not guess.

---

# 97. Foreign Currency Acquisition

Preserve:

```text
Source Currency
Source Amount
Capitalization/Base Currency Amount
FX Snapshot
Purpose/Rate Date
```

via FinBase.

---

# 98. Payment Is Irrelevant To Asset Recognition Timing By Default

Supplier being paid or unpaid does not define whether Asset exists/capitalizes.

Payments owns settlement.

---

# 99. Inventory-Origin Asset

Example:

```text
Laptop received into stock
→ later issued for internal use
→ Asset created/capitalized
```

---

# 100. Inventory Integration

Inventory owns:

```text
Serial
stock movement
cost/valuation
location history while stock
```

Assets owns Asset after/for internal-control lifecycle.

---

# 101. Inventory To Asset Conversion Is Not Identity Merge

Keep links:

```text
AssetId
ProductVariantId?
InventorySerialId?
Source InventoryOperationId?
```

No shared row.

---

# 102. Inventory Serial Uniqueness

If Asset references serialized stock:

```text
one active Asset should not claim same Inventory Serial twice
```

within valid scope.

---

# 103. Inventory Cost Source

Inventory can provide cost basis for internal-use stock capitalization.

Assets stores capitalization snapshot.

---

# 104. Asset Returned To Stock

Not ordinary field mutation.

Potential controlled process:

```text
De-capitalize/transfer operational ownership
→ Inventory receipt if business meaning valid
```

needs accounting/history.

Not MVP default.

---

# 105. Expense-Origin Asset

`21 Expenses` may identify spend that should be capitalized.

Use:

```text
CapitalizationCandidate
```

or explicit approved Capital Expense handoff.

Do not turn an already-finalized ordinary Expense into Asset invisibly.

---

# 106. Expense Reclassification

If Expense was incorrectly finalized as operating expense:

```text
correct/reverse Expense accounting
→ capitalize Asset
```

through linked correction.

---

# 107. Internal Construction / Self-Built Asset

Collect authorized cost sources through CIP.

---

# 108. CIP Mission

CIP answers:

> **Which capital project/asset is still under construction, what qualifying costs have accumulated, and when is it ready to become a depreciable asset?**

---

# 109. CIP Does Not Depreciate

Default:

```text
Depreciation = Off
```

until placed in service/capitalized.

---

# 110. CIP Asset

Concept can use same Asset identity with:

```text
AssetNature = CIP
LifecycleState = UnderConstruction
```

or separate controlled subtype.

Recommended same canonical Asset registry.

---

# 111. CIP Cost Sources

Can include:

```text
Supplier invoices
Contractor costs
Materials
Internal capitalizable labor later
Engineering
Permits
Installation
Freight
Other directly attributable costs
```

according to policy.

---

# 112. CIP Cost Ledger

Use typed additions:

```text
CipCostAddition
```

not one mutable `cip_total`.

---

# 113. CIP Total

Rebuildable:

```text
SUM(valid capitalizable CIP additions)
```

---

# 114. CIP Removal/Correction

Reverse/correct source allocation.

No direct total edit.

---

# 115. CIP To Asset Capitalization

When ready:

```text
CIP
→ Available For Use
→ Capitalize
→ one or many final Assets
```

---

# 116. CIP Split

One CIP project can become:

```text
Land improvement
Building
Machine
Equipment
```

multiple assets through allocation.

---

# 117. Multiple CIP → One Asset

Possible with controlled allocations.

---

# 118. Placed In Service Gate

Before financial depreciation starts validate:

```text
Final category
Asset book(s)
Cost basis
Useful life/method
Location
Required documents
Accounting readiness
PlacedInServiceDate
```

---

# 119. Capitalization Does Not Always Equal Placed In Service

Possible:

```text
financial recognition prepared
but not yet available for use
```

depending accounting policy.

Keep dates separate.

---

# 120. Opening Assets / Migration

A company adopting TODIR must not create fake purchase invoices.

---

# 121. Opening Asset Mode

Explicit:

```text
SourceType = OpeningMigration
```

---

# 122. Opening Asset Minimum

```text
Asset Number/Legacy Ref
Category
Legal Entity
Description
Acquisition Date
Placed In Service Date
Original Cost
Accumulated Depreciation
Net Book Value
Remaining Life or method state
Current Location
Current Custodian if relevant
Books
```

---

# 123. Opening Financial Invariant

Per book:

```text
Opening Cost
- Accumulated Depreciation
- Impairment
± Revaluation
=
Opening Net Book Value
```

subject to book semantics.

---

# 124. Opening Assets Do Not Fabricate History

If legacy system lacks:

```text
old employee assignments
old transfers
old monthly depreciation detail
```

mark opening context.

Do not invent.

---

# 125. Opening Depreciation Detail

Options:

```text
OpeningAggregateOnly
DetailedImportedHistory
```

Explicit.

---

# 126. Opening Reconciliation

Before go-live:

```text
Asset Register Cost
Accumulated Depreciation
Net Book Value
```

must reconcile to approved GL/control balances per Book.

---

# 127. Asset Lifecycle

Keep lifecycle simple and separate from other dimensions.

Recommended:

```text
Draft
Active
UnderConstruction
HeldForDisposal?
Disposed
ArchivedHistorical
```

Exact states can be refined.

---

# 128. Lifecycle Is Not Depreciation State

Separate.

---

# 129. Lifecycle Is Not Custody State

Separate.

---

# 130. Lifecycle Is Not Accounting State

Separate.

---

# 131. Asset Financial State Dimensions

Potential:

```text
AssetLifecycleState
CapitalizationState
DepreciationState per Book
CustodyState
VerificationState
DisposalState
AccountingState
```

---

# 132. Active Asset

Can be:

```text
In Use
Temporarily Idle
Under Repair
Stored
```

operational condition without changing legal/financial lifecycle.

---

# 133. Operational Condition

Separate optional field/history:

```text
Good
NeedsRepair
UnderRepair
Damaged
Lost
Stolen
```

depending category.

---

# 134. Condition Changes

History event.

Not automatically accounting impairment.

---

# 135. Asset Tag

Independent human/scan identifier.

---

# 136. Asset Number vs Asset Tag

```text
AssetNumber
= business/register identifier

AssetTag
= physical label/QR/barcode
```

can be same by config, but not assumed.

---

# 137. Asset Numbering

Use Platform Numbering.

No `MAX+1`.

---

# 138. Asset Tag Uniqueness

Tenant/entity policy.

---

# 139. QR / Barcode

Can encode safe Asset Tag/reference.

Do not expose sensitive asset/book data in raw QR.

---

# 140. Scan Asset

Returns authorized Asset workspace.

---

# 141. Asset Tag Replacement

Lost/damaged label can be reissued.

History records old/new tag.

---

# 142. Serial Number

Manufacturer serial is not Asset Number.

---

# 143. Serial Optional

Land/Building no serial.

---

# 144. Duplicate Serial

If Category requires uniqueness, enforce normalized uniqueness in defined scope.

---

# 145. Serial Reuse

Do not globally block valid reuse across different manufacturers/categories unless policy requires.

---

# 146. Documents

Asset may have:

```text
Acquisition Invoice link
Title/Deed
Warranty
Insurance
Registration
License
Inspection Certificate
Handover Form
Photo
Manual
Disposal Evidence
Valuation Report
Other
```

---

# 147. Platform Files

Binary stored through Platform Files.

Assets stores FileRef + typed metadata.

---

# 148. Document Categories

Configurable/typed.

---

# 149. Required Documents

Category/Country policy can require:

```text
Title for Land
Registration for Vehicle
Warranty optional
```

without burdening laptop categories.

---

# 150. Expiry Documents

Examples:

```text
Vehicle registration
Insurance
License
Inspection
```

with reminders.

---

# 151. Warranty

Optional operational metadata:

```text
Provider
Start
End
Reference
FileRef
```

---

# 152. Warranty Is Not Maintenance

Maintenance may consume warranty context.

---

# 153. Insurance

Basic registry metadata:

```text
Provider Party?
Policy Reference
Coverage Start/End
FileRef
Insured Value?
```

Full insurance claims/premium management is separate future capability.

---

# 154. Ownership Documents

Land/building may require sensitive legal docs with stricter permission.

---

# 155. Document Permission

Viewing Asset does not automatically grant all documents.

---

# 156. Asset Notes

Internal notes allowed.

Never substitute typed lifecycle events with free text.

---

# 157. Asset History Timeline

Compose:

```text
Created
Capitalized
Placed In Service
Assigned
Transferred
Returned
Cost Addition
Depreciation Posted
Estimate Changed
Impaired
Revalued
Verified
Lost/Stolen Reported
Disposed/Sold
Corrected
```

---

# 158. Timeline Is Explainable

Every event links:

```text
Actor
Date
Reason
Source
Financial effect if any
Documents
```

---

# 159. Asset Books

Financial Asset can have one or more independent Books.

---

# 160. Why Books Exist

Different purposes may require different:

```text
Depreciation method
Useful life
Convention
Tax treatment
Posting behavior
Currency/reporting basis
```

---

# 161. Book Examples

```text
Corporate / Accounting Book
Tax Book
Management Book later
```

---

# 162. Small Company Default

One:

```text
Corporate Book
```

visible.

---

# 163. Tax Book Optional

Only when company/country requires separate tax depreciation.

---

# 164. AssetBook

Concept:

```text
AssetBook
{
    Id
    AssetId
    BookDefinitionId

    AcquisitionCost
    ResidualValue
    DepreciableBasis

    DepreciationMethod
    Convention
    UsefulLife
    DepreciationStartDate?

    CalculateDepreciation
    PostToLedger

    Currency
    State
    Version
}
```

---

# 165. Book Definition

Legal-Entity-level configuration:

```text
BookDefinition
{
    Code
    Purpose
    PostToLedger
    Currency
    CountryProviderRef?
    Status
}
```

---

# 166. One Active Book Configuration State At A Time

Changes should be effective-dated/versioned.

---

# 167. Book Transactions

Do not calculate carrying value from editable summary fields only.

Use transaction/history.

---

# 168. Asset Book Transaction Types

Potential:

```text
Acquisition
CostAddition
CostReduction
Depreciation
Impairment
ImpairmentReversal if allowed
RevaluationIncrease
RevaluationDecrease
Disposal
PartialDisposal
TransferBookAdjustment
OpeningBalance
Correction/Reversal
```

---

# 169. Book Value Formula

Conceptually:

```text
Cost Basis
+ Capital Additions
- Cost Reductions
- Accumulated Depreciation
- Impairment
± Revaluation
- Retired Basis
=
Net Book Value
```

exact book rules apply.

---

# 170. Book Value Is Derived

Never ordinary editable field.

Projection allowed.

---

# 171. Accumulated Depreciation Is Derived

From posted depreciation/book transactions + opening balance.

---

# 172. Remaining Life Is Derived/Policy-Aware

Can change through estimate change.

---

# 173. Asset Book Summary Projection

Allowed:

```text
Gross Carrying Amount
Accumulated Depreciation
Accumulated Impairment
Revaluation Reserve ref/summary
Net Book Value
Remaining Life
Next Depreciation Date
```

rebuildable.

---

# 174. Depreciable vs Non-Depreciable

Each Book can:

```text
CalculateDepreciation = true/false
```

---

# 175. Land Default

Usually:

```text
CalculateDepreciation = false
```

but TODIR does not hardcode accounting law globally.

Category/Book/Country Policy defines.

---

# 176. CIP Book

```text
CalculateDepreciation = false
```

until placed in service.

---

# 177. Depreciation Method

Initial generic methods:

```text
StraightLine
DecliningBalance
NoDepreciation
UnitsOfProductionLater
CountryProviderMethod
```

---

# 178. Do Not Overbuild Methods Day 1

Implement launch-needed verified methods with extensible interface.

---

# 179. Straight-Line

Must define exact:

```text
depreciable basis
useful life
frequency
convention
rounding
start/end rules
```

---

# 180. Declining Balance

Versioned parameters.

No ambiguous percentage field.



# 181. Depreciation Convention

Separate from method.

Possible:

```text
PlacedInServiceDate
StartOfMonth
NextMonth
MidMonth
HalfYear
CountrySpecific
```

Only enable verified needs.

---

# 182. Convention Is Per Book

Corporate and Tax books may differ.

---

# 183. Useful Life

Stored in explicit unit:

```text
Months
Years
UnitsOfProduction later
```

Internally normalize periods as needed.

---

# 184. Useful Life Precision

Avoid ambiguous decimal years where exact months matter.

---

# 185. Residual Value

Can be:

```text
FixedAmount
Percentage
Zero
CountryProviderDerived
```

depending policy.

---

# 186. Depreciable Basis

Explicit formula per Book:

```text
Gross Carrying Amount
- Residual Value
- non-depreciable components if applicable
```

plus impairment/revaluation treatment.

---

# 187. Depreciation Schedule

Generated/rebuildable projection from:

```text
Book configuration
Posted transactions
Estimate changes
Period/calendar
```

---

# 188. Schedule Is Not Posted Truth

Future schedule rows are forecast.

Posted depreciation uses actual Book Transactions/Ledger Facts.

---

# 189. Depreciation Schedule Row

Can show:

```text
Period
Opening NBV
Depreciation
Closing NBV
Method/Version
Posted?
```

---

# 190. Depreciation Frequency

Common:

```text
Monthly
Period-based
Annual
```

depending Book.

---

# 191. Depreciation Run

A company can:

```text
Prepare
Review Exceptions
Post
```

---

# 192. Small Company Depreciation

Can enable:

```text
Auto-prepare monthly
```

with one review/post action.

---

# 193. Fully Automatic Posting

Optional only after accounting readiness and company approval.

---

# 194. Depreciation Run Scope

```text
Legal Entity
Book
Period
Asset Category?
```

---

# 195. Depreciation Candidate

System determines eligible Assets.

Do not ask user to hand-pick every asset.

---

# 196. Depreciation Exceptions

Examples:

```text
Missing book
Missing useful life
Missing account mapping
Invalid placed-in-service date
Closed accounting period
Disposed asset
Book suspended
Country rule missing
```

---

# 197. Exception-Driven Review

Healthy Assets auto-calculate.

Accountant reviews exceptions and totals.

---

# 198. Depreciation Preview

Use same calculation engine that will post.

No duplicate preview math.

---

# 199. Depreciation Fact

Assets emits semantic:

```text
AssetDepreciationFact
```

per asset/book/period or approved aggregate with detailed trace.

---

# 200. Ledger Owns Journal

Assets never selects final GL Account IDs.

---

# 201. Posting Granularity

Ledger may post:

```text
per Asset
per Category/Dimension aggregate
```

according to accounting design, while Assets preserves asset-level detail.

---

# 202. Reconciliation

Aggregate Asset Book depreciation for period must equal Ledger posting/control totals.

---

# 203. Depreciation Retry

Idempotent by:

```text
Asset
Book
Period
Run/Fact key
```

---

# 204. Double Post

Impossible.

---

# 205. Closed Period

Ledger period lock blocks posting.

Do not silently move to current period unless explicit accounting policy.

---

# 206. Backdated Capitalization

Requires recalculation/catch-up policy.

---

# 207. Catch-Up Depreciation

If asset placed in service earlier but entered later:

system calculates missed periods according to Book policy.

---

# 208. Catch-Up Posting

Can:

```text
post current period catch-up
or
post historical periods if open/allowed
```

Accounting policy.

---

# 209. Depreciation Suspension

Some companies/rules may suspend in defined circumstances.

Not automatic from `OperationalCondition=UnderRepair`.

---

# 210. Suspension Policy

Explicit per Book/Country rules.

---

# 211. Temporary Idle

Does not automatically stop depreciation.

---

# 212. Estimate Change

Useful life or residual value can change prospectively.

---

# 213. Estimate Change Record

Concept:

```text
AssetBookEstimateChange
{
    AssetBookId
    EffectiveDate
    OldUsefulLife
    NewUsefulLife
    OldResidualValue
    NewResidualValue
    Reason
    ApprovedBy?
}
```

---

# 214. Estimate Change Does Not Rewrite Posted Depreciation

Future schedule recalculates from carrying value.

---

# 215. Depreciation Method Change

Controlled Book change.

May be prospective or Country-provider specific.

Never overwrite history.

---

# 216. Book Configuration History

Store version/effective history sufficient to reproduce past depreciation.

---

# 217. Capital Addition

A later qualifying cost increases carrying amount.

---

# 218. Capital Addition Source

Examples:

```text
major improvement
significant replacement
capital project completion
qualifying installation
```

---

# 219. Repair vs Capital Addition

Important business decision:

```text
Repair/Maintenance
→ Expense/Maintenance cost

Improvement extending benefit/capacity
→ possible Capital Addition
```

policy/accountant decides.

---

# 220. Addition To Existing Asset

Capitalization Queue supports:

```text
Add to Existing Asset
```

with source allocation.

---

# 221. Addition Effective Date

May affect depreciation from specified date.

---

# 222. Addition Depreciation

Book recalculates future depreciation according to policy.

---

# 223. Separate Component Alternative

Instead of adding cost to parent, significant component can become child Asset.

---

# 224. Component Replacement

Example:

```text
Old elevator component disposed
New elevator component capitalized
```

without rewriting parent Building cost history.

---

# 225. Cost Reduction

Controlled correction/credit can reduce basis.

---

# 226. Supplier Credit Against Asset

Invoicing/Purchasing source:

```text
Supplier Credit Note
→ Asset Cost Reduction candidate
```

accountant reviews.

---

# 227. Cost Reduction Cannot Make Invalid Book

Validate:

```text
Cost >= accumulated depreciation + other constraints
```

according to book rules.

---

# 228. Impairment

Separate financial transaction.

---

# 229. Impairment Trigger

Can be initiated after:

```text
damage
obsolescence
market/value review
operational event
```

but accounting decision explicit.

---

# 230. Impairment Amount

Not a mutable NBV field.

Creates Book Transaction.

---

# 231. Impairment Evidence

Can require:

```text
valuation report
reason
approval
supporting file
```

---

# 232. Impairment Reversal

Only if applicable rules permit.

Country/Accounting policy.

---

# 233. Revaluation

Advanced capability.

Do not mix with ordinary manual value edit.

---

# 234. Revaluation Record

Potential:

```text
ValuationDate
OldCarryingValue
NewFairValue
ValuationMethod
Valuer
Evidence
Book
```

---

# 235. Revaluation Accounting

Ledger owns reserve/gain/loss mapping.

---

# 236. Revaluation Effect On Future Depreciation

Book engine recalculates prospectively according to policy.

---

# 237. Revaluation Build Priority

Design now; build only when launch/accounting market requires.

---

# 238. Physical Verification

Financial register must match physical reality.

---

# 239. Verification Campaign

Concept:

```text
AssetVerificationCampaign
{
    Id
    LegalEntityId
    Scope
    PlannedDate
    State
    CreatedBy
}
```

---

# 240. Verification Scope

Can be:

```text
Branch
Location
Department
Category
Custodian
Selected Assets
All
```

---

# 241. Expected Snapshot

Campaign freezes expected:

```text
Asset
Location
Custodian
Status
Tag
```

at campaign start or planned cutoff.

---

# 242. Scan Verification

Auditor:

```text
Scan QR/Barcode
→ Confirm Found
→ Capture actual location/custodian/condition
```

---

# 243. Verification Result

Per Asset:

```text
FoundCorrect
FoundWrongLocation
FoundWrongCustodian
Damaged
TagMissing
NotFound
UnexpectedAsset
NeedsReview
```

---

# 244. Verification Does Not Auto-Transfer

A mismatch creates:

```text
VerificationException
```

Then authorized transfer/correction.

---

# 245. Unexpected Asset

Can capture:

```text
Tag/Serial
Location
Photo
Notes
```

for review.

Do not auto-create financial asset.

---

# 246. Missing Asset

NotFound is operational exception.

Not automatic disposal.

---

# 247. Lost/Stolen Workflow

After investigation/approval:

```text
Report Lost/Stolen
→ operational state
→ financial disposal/impairment decision
```

separate.

---

# 248. Verification Evidence

Store:

```text
Verifier
Timestamp
Device?
Photo?
Coordinates only if explicitly allowed/useful
Condition
```

Avoid mandatory location tracking.

---

# 249. Verification Report

```text
Expected
Verified
Missing
Mismatch
Damaged
Unverified
```

---

# 250. Verification Frequency

Category/company policy:

```text
Annual
Quarterly
Ad hoc
None
```

---

# 251. Asset Custody Verification

Employee self-service can optionally confirm:

```text
I still hold these assets
```

with manager/IT audit support.

---

# 252. Mass Operations

Large company needs safe bulk actions.

---

# 253. Mass Transfer

Examples:

```text
300 laptops → New HQ
500 chairs → Warehouse
```

---

# 254. Mass Change

Can update permitted operational metadata through preview.

Financial/book changes require stronger controls.

---

# 255. Mass Retirement/Disposal

Possible with:

```text
preview
selection
reason
approval
validation
```

---

# 256. Bulk Operation Preview

Before commit show:

```text
Affected assets
Current values
Proposed values
Financial effects
Errors
```

---

# 257. Bulk Partial Failure

Choose explicit semantics:

```text
all-or-nothing batch
or
per-asset result
```

depending operation.

Financial operations should avoid hidden partial success.

---

# 258. Transfer Within Legal Entity

Operational transfer can change:

```text
Location
Custodian
Responsible Department
Cost Center
```

with effective date.

---

# 259. Cost Center Transfer

Can influence future depreciation expense dimension.

Past depreciation remains historical.

---

# 260. Location Transfer

Does not change Book value by itself.

---

# 261. Employee Transfer

No financial value effect by default.

---

# 262. Cross-Legal-Entity Transfer

Not ordinary assignment update.

---

# 263. Cross-Entity Rule

```text
Asset.LegalEntityId
```

cannot simply be changed.

---

# 264. Intercompany Asset Transfer

Future/advanced process may require:

```text
retirement/disposal in source
acquisition in destination
intercompany sale/transfer pricing
related-party invoice
book mapping
accumulated depreciation mapping
tax/compliance
```

---

# 265. Cross-Entity MVP Default

```text
Disallow ordinary transfer
```

until Intercompany capability approved.

---

# 266. Country Transfer Document

Country Pack can require document/fiscal process for moving certain assets.

---

# 267. Transfer Compliance Seam

```text
IAssetTransferComplianceProvider
```

validates/creates required regulatory context.

---

# 268. Ownership Change

Legal owner change is not custody transfer.

Could imply sale/intercompany/legal transfer.

---

# 269. Asset Sale

Asset disposal with proceeds.

---

# 270. Asset Sale Components

```text
Disposal of Asset carrying value
+ Customer/Sales/Invoicing source
+ Payment
+ Gain/Loss
```

separate truths.

---

# 271. Assets Owns Retirement/Disposal Fact

Sales/Invoicing owns customer commercial/legal document.

Payments owns proceeds.

Ledger owns accounting.

---

# 272. Asset Sale Flow

```text
Select Asset
→ validate disposable
→ determine sale/disposal date
→ optional customer/sale document
→ finalize retirement
→ Ledger gain/loss
→ payment settles separately
```

---

# 273. Sale Price

May come from Sales/Invoicing document.

Assets stores disposal proceeds reference/snapshot needed for gain/loss explanation.

---

# 274. Gain/Loss

Derived from:

```text
Net proceeds
- Book carrying value disposed
- eligible disposal costs
```

according to Book/accounting rules.

Ledger posts.

---

# 275. Sale Payment Can Be Later

Asset disposed/sold does not mean cash received.

---

# 276. Scrap

```text
Proceeds = zero or scrap proceeds
Reason = Scrap
```

with disposal evidence.

---

# 277. Donation

Disposal reason with its own accounting/tax treatment.

---

# 278. Lost

Disposal/financial treatment after approved loss process.

---

# 279. Stolen

Same distinction; may involve insurance claim later.

---

# 280. Destroyed

Separate reason.

---

# 281. Disposal Reason

Typed:

```text
Sold
Scrapped
Donated
Lost
Stolen
Destroyed
IntercompanyTransfer
OtherApproved
```

---

# 282. Disposal Is Irreversible Financial Event

Ordinary edit/delete not allowed.

Correction/reinstatement process only.

---

# 283. Reinstatement

If disposal posted incorrectly and accounting rules allow:

```text
Reinstate Asset
```

with reversal/history.

---

# 284. Partial Disposal

Support architecture.

Example:

```text
Grouped Asset 100 units
→ dispose 20 units
```

or component disposal.

---

# 285. Partial Disposal Basis

Allocate:

```text
Cost
Accumulated depreciation
Impairment/revaluation
```

according to Book policy.

---

# 286. Disposal Depreciation Through Date

Book convention decides final depreciation.

---

# 287. Fully Depreciated Asset

Can remain:

```text
Active / In Use
NBV = residual/zero
```

until actual disposal.

Do not auto-dispose.

---

# 288. Zero Book Value Does Not Mean No Asset

Laptop can still be physically assigned.

---

# 289. Disposal Stops Future Depreciation

From effective disposal rules.

---

# 290. Disposal Documents

Can require:

```text
sale invoice
scrap approval
committee report
police report
insurance report
handover evidence
```

category/reason/country policy.

---

# 291. Maintenance Boundary

Fixed Assets does not own:

```text
Maintenance Request
Preventive Schedule
Work Order
Technician Dispatch
Spare Parts Consumption
Downtime Work Order
```

---

# 292. Maintenance Integration

Future Maintenance:

```text
MaintenanceAssetRef = AssetId
```

Canonical Asset identity remains Fixed Assets.

---

# 293. Avoid Duplicate Equipment Identity

Do not create:

```text
FinancialAsset LAP-001
MaintenanceEquipment LAP-001
```

as independent master identities.

Maintenance uses AssetId or explicit linked OperationalAsset profile.

---

# 294. Operational-Only Equipment

If company tracks equipment not financial assets, canonical Asset Registry can support operational tracking without Book where configured.

---

# 295. Maintenance Cost

Ordinary repair expense remains Expenses/Purchasing/Accounting source.

---

# 296. Capitalizable Maintenance Improvement

Can become Capitalization Candidate/Add Cost/Component.

Accountant decides.

---

# 297. Warranty Integration

Maintenance can see warranty period.

---

# 298. Insurance Claim Boundary

Full claims handling future.

Assets provides Asset/ref/value/history.

---

# 299. Lease Boundary

Right-of-Use/Lease accounting is complex.

Do not fake by ordinary owned Asset workflow.

---

# 300. Right-of-Use Asset Seam

Design optional `AssetNature = RightOfUse` only when Lease module/accounting design exists.

Build later.



# 301. Accounting Mission

Fixed Assets owns:

```text
Asset identity
Book transaction meaning
Depreciation calculation
Carrying-value history
```

Ledger owns:

```text
Journal Entry
Account Determination
Period locks
Accounting dimensions validation
```

---

# 302. Accounting Facts

Potential semantic facts:

```text
AssetCapitalized
AssetCostAdded
AssetCostReduced
AssetDepreciated
AssetImpaired
AssetImpairmentReversed
AssetRevalued
AssetDisposed
AssetPartialDisposed
AssetReinstated
AssetOpeningImported
```

---

# 303. No GL Account IDs In Asset Commands

Forbidden:

```text
DebitAccountId
CreditAccountId
AccumulatedDepreciationAccountId
```

inside ordinary Asset domain commands.

Use semantic roles.

---

# 304. Account Roles

Possible:

```text
FixedAssetCost
AccumulatedDepreciation
DepreciationExpense
ImpairmentExpense
RevaluationReserve
DisposalGain
DisposalLoss
CIP
AssetClearing
```

mapped by Ledger/account determination.

---

# 305. Category Accounting Profile

Category can reference semantic role profile.

No hardcoded chart numbers.

---

# 306. Dimensions

Asset Book financial facts can carry:

```text
Legal Entity
Branch
Department
Cost Center
Project?
Asset Category
```

where accounting design permits.

---

# 307. Depreciation Expense Dimension

Can follow:

```text
Current Cost Owner
Fixed book assignment
Historical assignment as of depreciation period
```

company policy.

Must be explicit.

---

# 308. Assignment Change Mid-Period

Policy defines whether depreciation splits across dimensions in same period.

Do not silently choose.

---

# 309. Financial Posting Date

Separate from:

```text
Asset Transaction Date
Effective Date
Physical Transfer Date
```

when needed.

---

# 310. Ledger Period Lock

All posting validates Ledger periods.

---

# 311. Accounting Preview

Accountant can use real Ledger Posting Preview for:

```text
Capitalization
Depreciation
Impairment
Disposal
```

---

# 312. Asset/GL Reconciliation

Must reconcile per Book/Legal Entity:

```text
Gross Asset Cost
Accumulated Depreciation
Impairment
Net Book Value
CIP
Disposals
```

to corresponding Ledger control accounts.

---

# 313. Reconciliation Exception

Show:

```text
Asset subledger amount
GL amount
Difference
Likely source
Unposted facts?
Mapping issue?
Opening mismatch?
```

---

# 314. Accounting Health

Accounting dashboard can consume:

```text
Assets Healthy
Depreciation Pending
Unposted Facts
GL Difference
```

without owning asset calculations.

---

# 315. Asset Book Posting

Book can:

```text
PostToLedger = true
```

or be non-posting tax/management book.

---

# 316. Non-Posting Book

Maintains separate valuation/depreciation schedule but no GL Journal.

---

# 317. Derived Book Concept

Can be designed for future:

```text
Primary Book transaction
→ copy selected acquisition/disposal events to secondary Book
```

but no need to overbuild Day 1.

---

# 318. Country/Tax Book

Country provider may supply:

```text
asset tax categories
tax depreciation methods
rates
special allowances
disposal rules
required registers/forms
```

---

# 319. Country Rules Are Versioned

Historical calculations reproduce old provider version.

---

# 320. Core Does Not Hardcode Country Tax Rates

No:

```text
if Egypt land...
if Saudi vehicle...
```

inside generic Assets.

---

# 321. Book Currency

Usually Legal Entity base/reporting currency.

Architecture supports explicit currency.

---

# 322. Secondary/Reporting Currency

Design seam through FinBase/Ledger.

Do not duplicate full multi-ledger accounting inside Assets.

---

# 323. Acquisition FX vs Accounting FX

Preserve source acquisition FX snapshot when relevant.

Ledger may use explicit accounting FX purpose.

---

# 324. Asset Data Model — Core

Recommended schema:

```text
business_assets
```

---

# 325. AssetsDbContext

Own only Assets tables.

No direct cross-module DbContext navigation.

---

# 326. Core Asset Table

Concept:

```text
fixed_asset
{
    id
    tenant_id
    legal_entity_id

    asset_number
    asset_tag?
    name
    description?

    category_id
    asset_nature
    tracking_granularity

    lifecycle_state
    operational_condition?

    acquisition_date?
    capitalization_date?
    placed_in_service_date?

    parent_asset_id?

    source_origin_type?
    product_variant_ref?
    inventory_serial_ref?

    row_version
    created_at
}
```

---

# 327. Product Link Optional

Do not require.

Use stable contract/reference, not ORM FK across business module if architecture forbids.

---

# 328. Inventory Serial Link Optional

Same.

---

# 329. Asset Category Tables

```text
asset_category
asset_category_version
asset_category_book_default
asset_category_policy
```

---

# 330. Asset Assignment Tables

```text
asset_assignment
asset_assignment_history/projection
asset_handover
```

History can be append-style effective-dated records.

---

# 331. Location Tables

```text
asset_location
asset_location_hierarchy
```

if Assets owns general physical sites beyond Organization references.

---

# 332. Profile Tables

Optional:

```text
asset_real_estate_profile
asset_vehicle_profile
asset_equipment_profile
asset_intangible_profile
```

Only when typed fields justify dedicated model.

---

# 333. Asset Book Tables

```text
asset_book_definition
asset_book
asset_book_transaction
asset_book_estimate_change
asset_depreciation_run
asset_depreciation_run_line
asset_depreciation_schedule_projection
asset_book_balance_projection
```

---

# 334. Source Tables

```text
asset_capitalization_candidate
asset_source_allocation
asset_cost_adjustment
```

---

# 335. CIP Tables

Can use Asset + Book Transactions, plus:

```text
asset_cip_cost_source
asset_cip_capitalization_allocation
```

if needed.

---

# 336. Document Tables

```text
asset_document
asset_document_requirement
```

File bytes external.

---

# 337. Verification Tables

```text
asset_verification_campaign
asset_verification_item
asset_verification_exception
```

---

# 338. Disposal Tables

```text
asset_disposal
asset_disposal_book_result
asset_disposal_source_link
```

---

# 339. Revaluation/Impairment

Potential:

```text
asset_impairment
asset_revaluation
```

or typed Book Transaction + supporting aggregate.

---

# 340. External Refs

```text
asset_external_reference
```

for legacy/other systems.

---

# 341. Projections

```text
asset_register_projection
asset_current_assignment_projection
asset_book_balance_projection
asset_verification_summary_projection
asset_depreciation_due_projection
asset_gl_reconciliation_projection
```

Rebuildable.

---

# 342. Tables Assets Must Not Own

Do not create:

```text
product
inventory_serial
employee
supplier_invoice
purchase_order
payment
journal_entry
project
maintenance_work_order
```

---

# 343. Tenant Isolation

Every tenant-owned table:

```text
tenant_id
ENABLE RLS
FORCE RLS
```

---

# 344. Runtime DB Role

No ownership/BYPASSRLS.

---

# 345. UUIDv7

Use for IDs.

---

# 346. Money Precision

FinBase exact decimal.

No float/double.

---

# 347. Quantity For Grouped Assets

Exact decimal/whole number policy based on category.

---

# 348. Area UOM

Use explicit area UOM.

Do not store unlabelled `area = 1000`.

---

# 349. Dates

Use NodaTime:

```text
LocalDate
Instant
DateTimeZone
```

according to semantic meaning.

---

# 350. Effective Dating

Assignments/book config/rate changes use effective dates.

---

# 351. Optimistic Concurrency

Mutable Draft/config assets use row version.

---

# 352. Posted Financial Transactions Immutable

Corrections/reversals only.

---

# 353. Asset Number Uniqueness

Within configured Tenant/Legal Entity scope.

---

# 354. Asset Tag Uniqueness

Within appropriate operational scope.

---

# 355. Asset Name Not Unique

Do not use name as identity.

---

# 356. Asset Hierarchy

Parent/child relation supports:

```text
Building → Components
Machine → Components
Compound Asset → Member Assets
```

---

# 357. Hierarchy Cycle Prevention

No asset can be ancestor of itself.

---

# 358. Parent Disposal

Policy:

```text
Require child review
Cascade only with explicit approval
Partial allowed
```

No blind cascade.

---

# 359. Parent/Child Book Independence

Children can have separate Books/useful lives.

---

# 360. Component Cost Allocation

Parent acquisition source can allocate cost to children.

---

# 361. Group vs Parent/Child

Distinct:

```text
Grouped Asset = homogeneous units treated together
Parent/Child = structural relationship between separate Assets
```

---

# 362. Source-of-Truth Matrix

```text
Asset identity/lifecycle          → Fixed Assets
Asset Category                    → Fixed Assets
Asset Custody/Assignment          → Fixed Assets
Asset Physical Location           → Fixed Assets
Asset Book/depreciation           → Fixed Assets
Product identity                  → Catalog
Inventory Serial/movement         → Inventory
Employee identity                 → HR
Supplier Invoice                  → Invoicing
Purchase workflow                 → Purchasing
Payment                           → Payments
Tax                               → Tax/Country
Journal Entry                     → Ledger
Maintenance Work Order            → Maintenance future
```

---

# 363. Commands — Asset Core

Representative:

```text
CreateAssetDraft
ActivateOperationalAsset
ArchiveUnusedDraftAsset
UpdateNonFinancialAssetDetails
AssignAssetTag
ReplaceAssetTag
```

---

# 364. Commands — Capitalization

```text
CreateCapitalizationCandidate
ReviewCapitalizationCandidate
CreateAssetsFromSource
AddSourceCostToAsset
CreateCipFromSources
CapitalizeCip
```

---

# 365. Commands — Assignment

```text
AssignAssetToEmployee
AssignAssetToDepartment
TransferAsset
ReturnAssetToPool
ConfirmAssetReceipt
ConfirmAssetReturn
ChangeAssetCostOwner
```

---

# 366. Commands — Books

```text
AddAssetBook
SetBookDepreciationPolicy
PlaceAssetInService
ChangeAssetEstimate
SuspendDepreciationIfPolicyAllows
ResumeDepreciation
```

---

# 367. Commands — Depreciation

```text
PrepareDepreciationRun
ReviewDepreciationRun
PostDepreciationRun
ReverseDepreciationRun
```

---

# 368. Commands — Value Changes

```text
AddCapitalCost
ReduceAssetCost
RecordImpairment
ReverseImpairment
RecordRevaluation
```

---

# 369. Commands — Verification

```text
CreateVerificationCampaign
StartVerification
VerifyAsset
ReportVerificationMismatch
ResolveVerificationException
CloseVerificationCampaign
```

---

# 370. Commands — Disposal

```text
PrepareAssetDisposal
DisposeAsset
SellAsset
ScrapAsset
ReportLostAsset
ReportStolenAsset
PartialDisposeAsset
ReinstateAsset
```

---

# 371. No Generic `SetBookValue`

Forbidden.

---

# 372. No Generic `SetAccumulatedDepreciation`

Forbidden after opening/import setup.

---

# 373. No Generic `SetAssetOwner`

Use legal transfer process.

---

# 374. No Generic `SetCustodian`

Use assignment/transfer command.

---

# 375. No Generic `DeleteDisposedAsset`

Forbidden.

---

# 376. Contracts

Recommended:

```text
Todir.Assets.Contracts
```

---

# 377. Backend Namespaces

```text
Todir.Modules.Assets.Domain
Todir.Modules.Assets.Application
Todir.Modules.Assets.Infrastructure
Todir.Assets.Contracts
```

---

# 378. Internal Areas

Potential:

```text
Registry/
Categories/
Capitalization/
Assignments/
Books/
Depreciation/
CIP/
Verification/
Disposals/
Documents/
Reporting/
```

---

# 379. API Base

Recommended:

```text
/api/v1/assets
```

---

# 380. Asset Register APIs

```text
GET  /assets
POST /assets
GET  /assets/{id}
PUT  /assets/{id}
GET  /assets/{id}/timeline
GET  /assets/{id}/documents
```

---

# 381. Category APIs

```text
GET  /categories
POST /categories
POST /categories/{id}/versions
POST /category-versions/{id}/activate
```

---

# 382. Capitalization APIs

```text
GET  /capitalization/candidates
POST /capitalization/candidates/{id}/review
POST /capitalization/create-assets
POST /capitalization/add-to-asset
POST /capitalization/create-cip
```

---

# 383. Assignment APIs

```text
GET  /assets/{id}/assignments
POST /assets/{id}/assign
POST /assets/{id}/transfer
POST /assets/{id}/return
POST /assets/{id}/handover/confirm
```

---

# 384. Book APIs

```text
GET  /assets/{id}/books
POST /assets/{id}/books
GET  /assets/{id}/books/{bookId}
POST /assets/{id}/place-in-service
POST /asset-books/{id}/estimate-change
```

---

# 385. Depreciation APIs

```text
GET  /depreciation/runs
POST /depreciation/runs
POST /depreciation/runs/{id}/prepare
POST /depreciation/runs/{id}/post
POST /depreciation/runs/{id}/reverse
GET  /assets/{id}/depreciation-schedule
```

---

# 386. Verification APIs

```text
GET  /verification/campaigns
POST /verification/campaigns
POST /verification/campaigns/{id}/start
POST /verification/campaigns/{id}/items/{assetId}/verify
POST /verification/campaigns/{id}/close
```

---

# 387. Disposal APIs

```text
POST /assets/{id}/disposal/prepare
POST /assets/{id}/dispose
POST /assets/{id}/sell
POST /assets/{id}/scrap
POST /assets/{id}/partial-disposal
POST /assets/{id}/reinstate
```

---

# 388. Search

Search by:

```text
Asset Number
Asset Tag
Name
Serial
Category
Location
Custodian Employee
Responsible Department
Legacy Ref
VIN/Plate where authorized
Property Ref
```

---

# 389. Lightweight Asset Picker

Never return full Books/documents/history per row.

---

# 390. Employee Asset Query Contract

HR can call:

```text
IEmployeeAssetsQuery
```

returning safe summary:

```text
AssetId
AssetNumber
Name
Category
Tag
CurrentAssignment
ReturnRequired?
```

No book values unless permission/source explicitly permits.

---

# 391. Inventory Asset Query Contract

Inventory can validate/link serialized internal-use conversion through stable contract.

---

# 392. Accounting Query Contract

Accounting consumes:

```text
AssetBook balances
Depreciation due/posting health
GL reconciliation
```

read-only.

---

# 393. Maintenance Contract

Future:

```text
IAssetOperationalInfo
```

for maintenance context.

---

# 394. Idempotency

Mandatory for:

```text
capitalization
bulk creation
depreciation posting
asset transfer
handover confirmation
CIP capitalization
cost addition
impairment
revaluation
disposal
opening import
verification import
```

where retries occur.

---

# 395. Same Key Different Payload

Conflict.

---

# 396. Concurrency — Asset Transfer

Two simultaneous transfers:

```text
one current assignment version wins
one receives concurrency conflict
```

---

# 397. Concurrency — Depreciation

Two runs cannot post same Asset/Book/Period twice.

---

# 398. Concurrency — Disposal

Disposed Asset cannot be disposed twice.

---

# 399. Concurrency — Capitalization Source

Same source amount cannot be over-allocated to Assets.

---

# 400. Concurrency — Inventory Serial

Same serial cannot become two active individual Assets through duplicate process.



# 401. Security Principle

Seeing an Asset operationally does not automatically grant financial valuation, legal documents or disposal authority.

---

# 402. Permission Vocabulary

Initial:

```text
assets.read
assets.read_financials
assets.read_legal_documents

assets.create
assets.update_nonfinancial
assets.capitalize
assets.add_cost

assets.assign
assets.transfer
assets.confirm_handover

assets.book.read
assets.book.manage
assets.depreciation.prepare
assets.depreciation.post
assets.depreciation.reverse

assets.impair
assets.revalue

assets.dispose
assets.sell
assets.scrap
assets.reinstate

assets.verification.run
assets.verification.resolve

assets.opening.import
assets.category.manage
assets.location.manage
assets.document.manage
assets.audit.read
assets.export
assets.settings.manage
```

---

# 403. Scope Model

Combine with:

```text
SelfAssigned
Department
Branch
Location
LegalEntity
AllAuthorizedEntities
```

---

# 404. Employee Self-Service Scope

Employee can see Assets currently assigned to self and permitted history.

Server resolves User→Employee.

---

# 405. Employee Cannot Forge EmployeeId

IDOR tests mandatory.

---

# 406. Employee Financial Visibility

Default:

```text
Asset number
name
tag
serial
assignment date
condition
return action
```

Not:

```text
cost
accumulated depreciation
NBV
gain/loss
```

---

# 407. Manager Visibility

Manager may see team custody assets if company enables.

No automatic full Book visibility.

---

# 408. IT/Operations Role

Can manage:

```text
tags
assignments
locations
condition
documents operational
verification
```

without posting depreciation.

---

# 409. Fixed Asset Accountant

Can manage financial Books/depreciation/capitalization.

May not need employee sensitive HR data beyond safe identity.

---

# 410. Finance Viewer

Read financial valuation/reconciliation.

---

# 411. Disposal Authority

High risk.

Separate from ordinary edit.

---

# 412. Revaluation/Impairment Authority

High risk.

Can require Approval/SoD.

---

# 413. Category/Book Config Authority

High risk because future financial calculations change.

---

# 414. RLS

All tenant-owned Assets tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 415. Legal Entity Scope

Financial Assets always entity-scoped.

---

# 416. Cross-Entity Asset Read

Only explicit authorized multi-entity roles.

---

# 417. Sensitive Real Estate Documents

Title/deed documents can require stricter field/document permissions.

---

# 418. Vehicle Personal Data

Avoid storing unnecessary driver personal data in Asset profile.

Custodian is Employee reference.

---

# 419. Audit

Critical actions:

```text
Asset create/capitalize
Source allocation
Book setup/change
Placed in service
Assignment/transfer/return
Tag replacement
Cost addition/reduction
Estimate change
Depreciation post/reverse
Impairment/revaluation
Verification result/resolution
Disposal/reinstatement
Opening import
Category/policy change
Mass operations
Document upload/remove
Large export
```

---

# 420. Audit vs Business History

Both.

Business history explains Asset life.

Audit explains actor/config/security action.

---

# 421. Hard Delete

Only unused Draft never referenced/financially posted.

---

# 422. Active/Posted Asset Cannot Hard Delete

Archive after disposal/history retention.

---

# 423. Data Retention

Financial/legal records retained per country/company policy.

---

# 424. Asset Documents Retention

Can exceed ordinary operational history.

Country Pack may define.

---

# 425. Performance Mission

Asset Register must remain fast with:

```text
hundreds
thousands
hundreds of thousands
millions
```

of assets depending tenant size.

---

# 426. Asset Register Target

Indexed paged query:

```text
p95 <= 100-250ms
```

on realistic dataset.

---

# 427. Asset Workspace Target

Core overview:

```text
p95 <= 150-350ms
```

without loading full timeline/books/documents.

---

# 428. Lazy Tabs

Load separately:

```text
Financial Books
Assignments
Documents
Depreciation
Timeline
Verification
Maintenance link
```

---

# 429. Search Target

Asset number/tag/serial:

```text
p95 <= 100-200ms
```

with indexes.

---

# 430. Employee Assigned Assets

```text
p95 <= 100-250ms
```

for employee summary.

---

# 431. Depreciation Run Scale

Test:

```text
10k
100k
1M AssetBooks
```

architecture benchmark.

---

# 432. Batch Depreciation

Use set-based/batch calculations where possible.

No one HTTP/module call per Asset.

---

# 433. Parallelization

Worker partition by:

```text
Legal Entity
Book
asset ID ranges/categories
```

only after deterministic concurrency design.

---

# 434. Depreciation Calculation Determinism

Same:

```text
Book state
transaction history
period
policy/provider versions
```

must reproduce same result.

---

# 435. Schedule Projection

Do not recalculate entire life history on every Asset open.

Persist rebuildable current schedule projection.

---

# 436. Book Balance Projection

Transactional/rebuildable.

---

# 437. No N+1

Asset list cannot query per row:

```text
Category
Custodian
Location
NBV
Book
Documents
```

separately.

---

# 438. Batch Enrichment

Use bounded composition/read projections.

---

# 439. Verification Campaign Scale

Test:

```text
100k assets campaign
mobile/scan bursts
```

---

# 440. Capitalization Queue Scale

Large AP import can create many candidates.

Use bulk processing.

---

# 441. Opening Asset Import Scale

Batch/chunk.

No one request per row.

---

# 442. Asset Hierarchy Query

Recursive hierarchy queries must be bounded/indexed.

---

# 443. Search Indexes

Likely:

```text
tenant/entity/asset_number unique
tenant/entity/asset_tag
tenant/entity/category/state
tenant/current_location/state
tenant/current_custodian/state
tenant/serial normalized
tenant/product_variant_ref
tenant/inventory_serial_ref
tenant/parent_asset
```

---

# 444. Book Indexes

```text
tenant/entity/book_definition/state
asset_id/book_definition unique active
book/next_depreciation_date/state
```

---

# 445. Transaction Indexes

```text
asset_book_id/effective_date
asset_book_id/transaction_type
source_type/source_id/source_line
```

---

# 446. Assignment Indexes

```text
asset_id/effective_from desc
custodian_employee_id/effective_to
location_id/effective_to
department_id/effective_to
```

---

# 447. Verification Indexes

```text
campaign/status
campaign/asset
asset/latest verification
```

---

# 448. Disposal Indexes

```text
tenant/entity/disposal_date
reason
source sale ref
```

---

# 449. PostgreSQL First

No OpenSearch required Day 1.

---

# 450. Redis

Not required Day 1.

---

# 451. Partitioning

Do not partition Day 1.

Measure high-volume transaction/depreciation history first.

---

# 452. Read Replica

Reporting later.

Authoritative depreciation/capitalization never depends on stale replica.

---

# 453. Background Jobs

Quartz/Worker can handle:

```text
Depreciation prepare
Document expiry reminders
Warranty/insurance reminders
Verification reminders
Projection rebuilds
Large imports/exports
Capitalization candidate ingestion
```

---

# 454. Notification Failure

Never rolls back financial Asset truth.

---

# 455. Observability Metrics

Potential:

```text
assets_register_query_duration
assets_search_duration
assets_capitalization_candidate_count
assets_capitalization_failure_total
assets_depreciation_prepare_duration
assets_depreciation_post_duration
assets_depreciation_exception_count
assets_assignment_transfer_total
assets_verification_mismatch_total
assets_disposal_total
assets_gl_reconciliation_difference
assets_opening_import_error_total
```

No AssetId as metric label.

---

# 456. Tracing — Capitalization

Spans:

```text
Load Source
Validate Eligibility
Resolve Category
Allocate Source Cost
Create Asset
Create Book
Ledger Fact
Audit
Outbox
Commit
```

---

# 457. Tracing — Depreciation

```text
Load Book Version
Load Balance
Calculate
Validate Period
Create Fact
Ledger Posting
Update Projection
Audit
Commit
```

---

# 458. Tracing — Transfer

```text
Load Current Assignment
Authorize
End Prior Assignment
Create New Assignment
Compliance if needed
Audit
Commit
```

---

# 459. Logging

Do not dump legal documents or employee sensitive details.

---

# 460. Operational Reports

Core:

```text
Asset Register
Assets By Category
Assets By Location
Assets By Branch
Assets By Department
Assets By Custodian
Unassigned Assets
Assets Without Location
Assets By Condition
Warranty Expiring
Insurance Expiring
Verification Exceptions
```

---

# 461. Financial Reports

```text
Asset Cost
Accumulated Depreciation
Net Book Value
Additions
Capital Additions
Depreciation By Period
Disposals
Gain/Loss
CIP
Impairment
Revaluation if enabled
```

---

# 462. Asset Register Columns

Permission-dependent:

```text
Asset Number
Tag
Name
Category
Nature
Legal Entity
Location
Custodian
Placed In Service
Cost
Accumulated Depreciation
NBV
Lifecycle
```

---

# 463. Nonfinancial User Asset Register

Hide financial columns.

---

# 464. Assets By Employee

Shows current custody, not legal ownership.

---

# 465. Custody Aging

Optional:

```text
How long assigned
Return overdue?
```

---

# 466. Unassigned Assets

Only relevant for categories requiring custody.

Do not flag Land.

---

# 467. Missing Location Report

Only categories with required location.

---

# 468. Verification Exceptions Report

```text
Not Found
Wrong Location
Wrong Custodian
Damaged
Tag Missing
Unexpected
```

---

# 469. Depreciation Report

Per Book:

```text
Opening NBV
Additions
Depreciation
Adjustments
Disposals
Closing NBV
```

---

# 470. Fixed Asset Statement

Detailed Book transaction history.

---

# 471. CIP Report

```text
CIP Asset
Source costs
Age
Project/site
Total
Ready-for-capitalization?
```

---

# 472. CIP Aging

Flag old CIP requiring review.

---

# 473. Asset GL Reconciliation

Per:

```text
Book
Asset Category
Legal Entity
Control Role
```

---

# 474. Reconciliation Drilldown

Difference → Asset transaction / Ledger posting refs.

---

# 475. Disposal Report

```text
Asset
Reason
Date
Book Value
Proceeds
Gain/Loss
Source document
```

---

# 476. Asset Movement Report

Assignment/location changes.

---

# 477. Audit Asset Trail

One Asset:

```text
creation
sources
assignments
book transactions
documents
verification
disposal
```

---

# 478. Export

Excel/CSV for registers/reconciliation.

PDF for formal reports where useful.

---

# 479. Large Export

Background secure file.

---

# 480. Asset Import

Mandatory for ERP adoption.

---

# 481. Import — Master/Open Assets

Fields can include:

```text
Legacy Asset ID
Asset Number
Name
Category
Nature
Tracking Mode
Acquisition Date
Placed In Service
Cost
Accumulated Depreciation
NBV
Useful Life
Remaining Life
Method
Residual Value
Location
Custodian Employee Number
Serial
Tag
Parent Legacy Asset
```

---

# 482. Import — Multiple Books

Separate sheet/rows:

```text
Asset Legacy ID
Book
Cost
Accumulated Depreciation
Method
Useful Life
Residual Value
```

---

# 483. Import — Assignments

Current assignment required if known.

Historical assignments optional.

---

# 484. Import — Documents

Metadata/files through Platform Files.

---

# 485. Import Matching

Employee by:

```text
Employee Number
approved external ref
```

No fuzzy auto assignment.

---

# 486. Product/Serial Matching

Use exact external/SKU/serial refs.

No fuzzy auto link.

---

# 487. Opening Balance Reconciliation

Import blocked from final go-live if financial Books do not reconcile within explicit approved tolerance/adjustment process.

---

# 488. Migration Provenance

Every opening Asset stores:

```text
SourceSystem
LegacyAssetId
ImportBatchId
ImportedAt
OpeningAsOfDate
```

---

# 489. Migration Does Not Create Fake Invoice

Absolute.

---

# 490. Migration Does Not Fabricate Depreciation Months

Aggregate opening reserve valid when detailed history absent.

---

# 491. Go-Live Date

Per Legal Entity/Book.

---

# 492. Historical Boundary

Transactions before go-live may be Opening/Migrated.

After go-live normal semantic commands only.

---

# 493. Configuration Readiness

Before Assets financial activation:

```text
Categories
Books
Depreciation methods
Account role mappings
Numbering
Locations where needed
Country Provider where needed
Period readiness
Opening reconciliation
```

---

# 494. Operational-Only Asset Readiness

Can activate without financial Books when company uses asset custody registry only, if product packaging allows.

---

# 495. Financial Activation

Requires Ledger/account mappings.

---

# 496. Setup Wizard

Recommended:

```text
1. Legal Entity
2. Define Asset Categories
3. Define Corporate Book
4. Configure depreciation defaults
5. Map accounting roles
6. Configure locations/custody policies
7. Import opening assets or start fresh
8. Reconcile opening balances
9. Enable source capitalization
10. Go Live
```

---

# 497. Small Company Defaults

```text
One Corporate Book
Straight Line
Simple Categories
No Tax Book
No component accounting unless needed
Employee custody only for selected categories
Simple locations
Manual capitalization review
Monthly depreciation
No revaluation
No impairment automation
No advanced verification campaign
```

---

# 498. Large Company Capabilities

Can enable:

```text
Multiple Books
Tax Book
CIP
Component Assets
Grouped Assets
Mass Additions
Mass Transfers
Verification Campaigns
Advanced approvals
Impairment
Revaluation
Intercompany transfer later
Country fiscal movement rules
```

---

# 499. UI Principle

Operational users and accountants should not see the same density by default.

---

# 500. Asset Workspace Composition

Tabs may be:

```text
Overview
Assignment / Location
Financial Books
Depreciation
Documents
Components
Verification
Timeline
Maintenance link
```

but screens are not domain boundaries.



# 501. UI — Operational Overview

Show first:

```text
Asset Number
Tag
Name
Category
Current Location
Custodian
Condition
Key Documents/Expiry
```

Financial numbers only if authorized.

---

# 502. UI — Accountant Overview

Show:

```text
Cost
Book
Accumulated Depreciation
NBV
Placed In Service
Next Depreciation
Accounting Health
```

---

# 503. UI — Land

Do not show irrelevant:

```text
Employee Custodian required
Serial required
Depreciation schedule required
```

unless configured.

---

# 504. UI — Laptop

Show:

```text
Tag
Serial
Employee
Location
Accessories
Warranty
```

with financial tab permissioned.

---

# 505. UI — CIP

Focus:

```text
Accumulated Sources
Cost
Project/Site
Ready for Use?
Capitalization blockers
```

not depreciation.

---

# 506. Asset Scan UX

Scan QR/tag:

```text
→ exact Asset
→ authorized quick actions
```

such as:

```text
Transfer
Verify
Return
Report Issue
```

according to permission.

---

# 507. Bulk UX

Always:

```text
Preview
Validate
Confirm
Result
```

for high-impact mass operations.

---

# 508. Business Scenario 1 — Land Purchase

Example:

```text
Supplier/contract source total = 20,000,000
Category = Land
Depreciation = Off
Location/property profile required
Custodian = None
```

---

# 509. Land Acceptance

System must allow Asset with:

```text
no Product
no Serial
no Employee
no depreciation
```

while financial/legal register remains complete.

---

# 510. Business Scenario 2 — Land + Building

Source:

```text
Combined purchase = 30,000,000
```

Allocate:

```text
Land      18,000,000
Building  12,000,000
```

Land non-depreciating, Building depreciating.

---

# 511. Land/Building Acceptance

One source can create multiple Asset categories/books with independent behavior.

---

# 512. Business Scenario 3 — Employee Laptop

Flow:

```text
Purchase/Receipt
→ Inventory serial A001
→ Internal use
→ Capitalize LAP-001
→ Assign Ahmed
→ Employee acknowledges
→ Later Return
→ Assign Mohamed
```

---

# 513. Laptop Acceptance

History answers:

```text
Which product?
Which serial?
Who held it on date X?
Where was it?
What condition at return?
What is financial NBV?
```

---

# 514. Business Scenario 4 — Pool Vehicle

```text
Vehicle owned by Entity
Location = Cairo HQ
Custody = Sales Vehicle Pool
Responsible Dept = Sales
Maintenance Responsible = Fleet Team later
```

No forced permanent employee.

---

# 515. Vehicle Acceptance

Custody and Maintenance responsibility separate.

---

# 516. Business Scenario 5 — Factory Machine

```text
Machine
→ Factory Line 4
→ Production Department
→ Serial
→ Book
→ Maintenance ref later
```

---

# 517. Machine Acceptance

Operational location, cost owner and maintenance responsible can differ.

---

# 518. Business Scenario 6 — Grouped Furniture

```text
200 chairs
→ Grouped Asset
→ HQ Floor 2
```

No artificial 200 serials.

---

# 519. Grouped Furniture Split

Move 40 chairs to Alexandria:

```text
Split/Distribution
```

preserves allocated basis/history according to policy.

---

# 520. Business Scenario 7 — 10 Laptops One Invoice

```text
Invoice line 500,000
Qty 10
→ create 10 assets
→ each 50,000 allocation
```

or exact allocation if freight/tax differs.

---

# 521. Multi-Asset Acceptance

Source amount cannot be over-allocated.

---

# 522. Business Scenario 8 — One Machine Many Costs

Sources:

```text
Machine Invoice
Freight
Installation
Engineering
```

→ one machine Asset.

---

# 523. Many-Source Acceptance

Each capitalized cost traceable to original source.

---

# 524. Business Scenario 9 — CIP Factory

```text
CIP opens
→ contractor costs
→ materials
→ engineering
→ permits
→ ready for use
→ split into Building + Machinery
```

---

# 525. CIP Acceptance

No depreciation before placed in service.

Every source cost allocated/remaining explained.

---

# 526. Business Scenario 10 — Opening Assets

Legacy data:

```text
Cost 1,000,000
Accum Dep 400,000
NBV 600,000
```

imports without fake invoices.

---

# 527. Opening Acceptance

Asset subledger reconciles to GL opening control.

---

# 528. Test — Land No Depreciation

Land remains active and financial with no depreciation run lines.

---

# 529. Test — Building Depreciation

Correct method/convention/life.

---

# 530. Test — Land + Building Allocation

Source total exactly allocated.

---

# 531. Test — Asset Without Product

Valid.

---

# 532. Test — Asset Without Employee

Valid for category.

---

# 533. Test — Laptop Requires Custody

Category rule enforces assignment if configured.

---

# 534. Test — Assignment History

Transfer preserves old custodian/date.

---

# 535. Test — Employee Rehire

Asset history references stable HR Employee, not obsolete User.

---

# 536. Test — Offboarding Query

Returns outstanding assigned Assets accurately.

---

# 537. Test — Handover Confirmation

Employee acknowledgement tied to exact assignment/version.

---

# 538. Test — Return Damage

Condition recorded; financial impairment not automatic.

---

# 539. Test — Same Serial Duplicate Asset

Blocked when exact Inventory Serial already linked active.

---

# 540. Test — Grouped Asset

No per-unit serial required.

---

# 541. Test — Split Group

Basis/units reconcile.

---

# 542. Test — Parent Child Cycle

Blocked.

---

# 543. Test — Component Disposal

Child disposed without corrupting unrelated parent/children.

---

# 544. Test — Capitalization Source Retry

One Asset/source allocation.

---

# 545. Test — Source Over-Allocation

Blocked.

---

# 546. Test — One Source Many Assets

Sum allocations exact.

---

# 547. Test — Many Sources One Asset

All traceable.

---

# 548. Test — Supplier Credit

Cost reduction via controlled source.

---

# 549. Test — Inventory Receipt Capitalization

Correct Serial/Product refs.

---

# 550. Test — Inventory Cost Variance Later

Creates cost adjustment, not silent cost rewrite.

---

# 551. Test — Expense Reclassification

Explicit correction path.

---

# 552. Test — CIP No Depreciation

Until capitalized.

---

# 553. Test — CIP Cost Addition

Rebuildable total.

---

# 554. Test — CIP Split Capitalization

New assets allocation exact.

---

# 555. Test — Placed In Service

Depreciation start follows Book convention.

---

# 556. Test — Acquisition Date Different From Service Date

Correct.

---

# 557. Test — Book Non-Depreciable

Excluded from depreciation run.

---

# 558. Test — Multiple Books

Corporate and Tax schedules independent.

---

# 559. Test — Non-Posting Book

No Ledger fact.

---

# 560. Test — Straight-Line Golden Vector

Exact rounding/life/convention.

---

# 561. Test — Declining Balance Golden Vector

Exact.

---

# 562. Test — Mid-Month/Convention

When enabled, verified golden dates.

---

# 563. Test — Fully Depreciated Still Active

No disposal.

---

# 564. Test — Residual Value

NBV never depreciates below policy residual absent allowed adjustment.

---

# 565. Test — Estimate Change

Prior depreciation unchanged; future recalculated.

---

# 566. Test — Backdated Asset

Catch-up exact.

---

# 567. Test — Closed Period

Posting blocked/handled by approved policy.

---

# 568. Test — Double Depreciation Post

One financial effect.

---

# 569. Test — Depreciation Retry

Idempotent.

---

# 570. Test — Cost Addition

NBV/schedule updated prospectively.

---

# 571. Test — Impairment

Book transaction + Ledger fact exact.

---

# 572. Test — Revaluation

If enabled, evidence/history/future depreciation exact.

---

# 573. Test — Operational Condition Change

Does not post Ledger by itself.

---

# 574. Test — Verification Found Correct

Closes item with no transfer.

---

# 575. Test — Verification Wrong Location

Creates exception, not auto-transfer.

---

# 576. Test — Verification Not Found

Does not auto-dispose.

---

# 577. Test — Verification Unexpected Asset

Review only.

---

# 578. Test — Mass Transfer

All selected assets validated, history exact.

---

# 579. Test — Transfer Concurrent Update

No lost assignment.

---

# 580. Test — Cross Entity Ordinary Transfer

Blocked.

---

# 581. Test — Asset Sale

Disposal fact links Sales/Invoicing source, Payment independent.

---

# 582. Test — Sale Before Payment

Asset can be disposed per legal/accounting workflow while receivable remains open.

---

# 583. Test — Scrap

Zero/controlled proceeds and disposal history.

---

# 584. Test — Lost/Stolen

No silent deletion.

---

# 585. Test — Partial Disposal

Basis and accumulated depreciation allocation exact.

---

# 586. Test — Reinstatement

Reverses incorrect disposal under approved conditions.

---

# 587. Test — Opening Asset

No fake invoice/payment.

---

# 588. Test — Opening Reconciliation

Cost/accumulated dep/NBV exactly reconcile.

---

# 589. Test — Legacy Missing History

Marked opening context, no invented events.

---

# 590. Test — Document Permission

IT user cannot read title deed without permission.

---

# 591. Test — Employee Self Service

Employee sees own assigned asset only.

---

# 592. Test — Financial Field Redaction

Employee/ops user receives no NBV if unauthorized.

---

# 593. Test — Cross Tenant RLS

Tenant A cannot read Tenant B Asset/tag/document.

---

# 594. Test — Cross Entity

Scope exact.

---

# 595. Test — Asset Tag Search

Fast and exact.

---

# 596. Test — 1M Asset Register

Measured pagination/search.

---

# 597. Test — 100k Depreciation Books

Batch calculation no N+1.

---

# 598. Test — 100k Verification Campaign

Scales.

---

# 599. Test — Large Opening Import

Chunked/reconcilable/idempotent.

---

# 600. Property Test — Book Value

For supported transaction set:

```text
Opening/Acquisition
+ Additions
- Reductions
- Depreciation
- Impairment
± Revaluation
- Disposed Basis
=
Closing NBV
```

according to book policy.

---

# 601. Property Test — Source Allocation

```text
allocated amount <= eligible source amount
```

---

# 602. Property Test — Group Units

```text
active units + disposed/split units reconcile to historical units
```

---

# 603. Property Test — Assignment

At most one primary current custody assignment per individual Asset when policy requires.

---

# 604. Property Test — Depreciation

No period double-post.

---

# 605. Property Test — Disposal

Disposed quantity/basis cannot exceed active basis/units.

---

# 606. Architecture Test — No Product Inheritance

Assets does not reference Catalog implementation assembly/table.

---

# 607. Architecture Test — No Inventory Table Read

Only Contracts.

---

# 608. Architecture Test — No HR DbContext

Only Employee contract/query.

---

# 609. Architecture Test — No Ledger Journal Direct Creation

Semantic facts only.

---

# 610. Architecture Test — No Supplier Invoice Duplication

Source refs only.

---

# 611. Architecture Test — No Maintenance Duplicate Master

Maintenance references Asset.

---

# 612. Red-Team — Asset = Product

Reject.

---

# 613. Red-Team — Land Requires SKU

Reject.

---

# 614. Red-Team — Every Asset Needs Employee

Reject.

---

# 615. Red-Team — Every Asset Depreciates

Reject.

---

# 616. Red-Team — Current Custodian Field Only

Reject without history.

---

# 617. Red-Team — Location Equals Custodian

Reject.

---

# 618. Red-Team — Employee Equals Owner

Reject.

---

# 619. Red-Team — Book Value Editable

Reject.

---

# 620. Red-Team — Depreciation Is Manual Journal

Reject.

---

# 621. Red-Team — Asset Disposal Deletes Row

Reject.

---

# 622. Red-Team — Zero NBV Auto-Dispose

Reject.

---

# 623. Red-Team — One Invoice Line Per Asset

Reject architecture assumption.

---

# 624. Red-Team — One Source Only

Reject.

---

# 625. Red-Team — CIP Is Ordinary Active Asset

Reject.

---

# 626. Red-Team — Repair Always Capitalized

Reject.

---

# 627. Red-Team — Operational Damage Auto-Impairment

Reject.

---

# 628. Red-Team — Cross Entity Update LegalEntityId

Reject.

---

# 629. Red-Team — Verification Mismatch Auto-Transfer

Reject.

---

# 630. Red-Team — Employee Offboarding Deletes Custody History

Reject.

---

# 631. Red-Team — Maintenance Creates Duplicate Equipment Master

Reject.

---

# 632. Red-Team — Tax Depreciation Reuses Corporate Schedule Blindly

Reject.

---

# 633. Red-Team — Opening Assets Require Fake Purchase Invoice

Reject.

---

# 634. Red-Team — Asset Location Uses Inventory Bin For Everything

Reject.

---

# 635. Red-Team — 100 Chairs Always 100 Asset Rows

Reject universal rule.

---

# 636. Red-Team — Grouped Asset Cannot Split

Reject architecture limitation.

---

# 637. Red-Team — Category Change Rewrites History

Reject.

---

# 638. Red-Team — Current Policy Recalculates Historical Book Without Version

Reject.

---

# 639. Agent Implementation Philosophy

Implement business truth first, not screens.

---

# 640. Agent Precedence

If conflict:

```text
22 Fixed Assets pack
→ owning domain packs (Catalog/Inventory/HR/Purchasing/Invoicing/Tax/Ledger/Accounting)
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ Country Provider
→ approved ADR
```

---

# 641. Agent Must Read

At minimum:

```text
01
02
03
04
05
06
08
09
11
12
13
15
16
17
21
22
all AGENTS.md
approved ADRs
actual public contracts
```

---

# 642. Agent Baseline Report

Before coding map:

```text
Organization LegalEntity/Branch
Catalog ProductVariant
Inventory Serial/cost/issue
HR Employee
Invoicing source
Purchasing source
Tax
FinBase
Ledger Facts
Platform Files
Approvals
Numbering
Audit
Outbox
Jobs
RLS
Transaction Coordinator
```

---

# 643. Agent Must Not Invent Duplicate Dependencies

If Inventory lacks required capitalization contract:

extend through approved contract/ADR.

Do not read Inventory tables directly.

---

# 644. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL
Real asset-control/accounting problem.

REAL-WORLD WORKFLOW

SOURCE OF TRUTH

STATE / INVARIANTS

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

DATABASE REQUIREMENTS

ACCOUNTING REQUIREMENTS

COUNTRY/COMPLIANCE REQUIREMENTS

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
- accounting reconciliation
- depreciation vectors
- performance evidence
- security evidence
- unresolved decisions
- risks
```

---

# 645. Implementation Sequence

```text
FA00 Repository / Dependency Verification
FA01 Module Boundary + Schema + RLS
FA02 Asset Category / Behavior Policy
FA03 Canonical Asset Registry
FA04 Asset Locations
FA05 Assignment / Custody / Handover
FA06 Product / Inventory Serial Linking
FA07 Documents / Tags / QR
FA08 Asset Book Definitions
FA09 Asset Book Core / Opening Value
FA10 Capitalization Candidate Queue
FA11 Supplier Invoice / Purchasing Source
FA12 Inventory Internal-Use Capitalization
FA13 Many-to-Many Source Allocation
FA14 CIP
FA15 Placed-In-Service
FA16 Straight-Line Depreciation
FA17 Additional Depreciation Methods / Conventions Needed At Launch
FA18 Depreciation Run + Ledger Posting
FA19 Book Balance / Schedule Projections
FA20 Cost Additions / Reductions
FA21 Estimate Changes
FA22 Impairment
FA23 Revaluation Seam / Implementation If Required
FA24 Grouped Assets / Split
FA25 Parent / Component Assets
FA26 Physical Verification
FA27 Mass Transfers / Operations
FA28 Disposal / Scrap / Loss
FA29 Asset Sale Integration
FA30 Partial Disposal / Reinstatement
FA31 HR Offboarding Integration
FA32 Maintenance Seam
FA33 Country / Tax Book Provider
FA34 Reports / Reconciliation
FA35 Opening Migration
FA36 Security / Permissions / Audit
FA37 Performance / Load
FA38 Runbooks / Recovery
FA39 Operational Asset Acceptance
FA40 Fixed Asset Accountant Acceptance
FA41 HR Custody Acceptance
FA42 Finance / GL Reconciliation Acceptance
FA43 Country / Tax Acceptance
FA44 Cross-Module End-to-End Acceptance
FA45 Release Gate
```

---

# 646. FA00 Goal

Map actual repository dependencies.

---

# 647. FA00 Exit Gate

No duplicate Product, Serial, Employee, Journal, Files or Numbering service planned.

---

# 648. FA01 Goal

Create one isolated Assets module.

---

# 649. FA01 Build

```text
Todir.Modules.Assets.Domain
Todir.Modules.Assets.Application
Todir.Modules.Assets.Infrastructure
Todir.Assets.Contracts
business_assets
AssetsDbContext
module manifest
RLS
architecture tests
```

---

# 650. FA01 Exit Gate

Cross-module boundaries enforced.

---

# 651. FA02 Goal

Model Asset categories/behavior before individual assets.

---

# 652. FA02 Build

```text
Category
Nature
Tracking Granularity
Custody Mode
Location requirement
Tag/Serial policy
Book defaults
Capitalization policy
```

---

# 653. FA02 Acceptance Matrix

Must include at least:

```text
Land
Building
Laptop
Vehicle
Machine
Furniture
CIP
Intangible
```

---

# 654. FA02 Exit Gate

No single behavior assumed for all Asset classes.

---

# 655. FA03 Goal

Canonical Asset Registry.

---

# 656. FA03 Build

```text
Asset Number
Tag
Category
Nature
Lifecycle
Parent/child foundation
source refs
```

---

# 657. FA03 Exit Gate

Land can exist correctly with no Product/Serial/Employee.

---

# 658. FA04 Goal

Real physical location model.

---

# 659. FA04 Exit Gate

Land/Building/Machine locations work without Inventory bin assumptions.

---

# 660. FA05 Goal

Custody/responsibility history.

---

# 661. FA05 Build

```text
Assignment
Transfer
Return
Handover
Employee/Department/Pool
Current projection
History
```

---

# 662. FA05 Exit Gate

Historical holder as-of-date reproducible.

---

# 663. FA06 Goal

Optional Catalog/Inventory integration.

---

# 664. FA06 Exit Gate

Serialized laptop source links without merging identities.

---

# 665. FA07 Goal

Physical control/evidence.

---

# 666. FA07 Build

```text
Asset Tag
QR
Documents
Warranty
Insurance basics
```

---

# 667. FA07 Exit Gate

Secure scan/search/document permissions.

---

# 668. FA08 Goal

Book definitions.

---

# 669. FA08 Build

```text
Corporate Book
Tax Book seam
PostToLedger
method profiles
```

---

# 670. FA08 Exit Gate

One simple Corporate Book works; multiple books possible.

---

# 671. FA09 Goal

Book transaction/balance foundation.

---

# 672. FA09 Exit Gate

NBV derived/rebuildable; no editable BookValue.

---

# 673. FA10 Goal

Capitalization Queue.

---

# 674. FA10 Exit Gate

Potential spend can be classified without immediate auto-capitalization.

---

# 675. FA11 Goal

Supplier source integration.

---

# 676. FA11 Exit Gate

Finalized Supplier Invoice can create candidate/assets without duplication.

---

# 677. FA12 Goal

Inventory internal-use conversion.

---

# 678. FA12 Exit Gate

Product/Serial/cost trace preserved.

---

# 679. FA13 Goal

Many-to-many cost allocation.

---

# 680. FA13 Exit Gate

10 laptops / one machine with many costs scenarios both pass.



# 681. FA14 Goal

CIP / Assets Under Construction.

---

# 682. FA14 Build

```text
CIP Asset
Cost additions
Source allocations
Ready-for-use gate
Capitalization split
```

---

# 683. FA14 Exit Gate

No depreciation before placed in service.

---

# 684. FA15 Goal

Placed-In-Service transition.

---

# 685. FA15 Exit Gate

Book/depreciation start dates follow approved policy, not acquisition-date assumption.

---

# 686. FA16 Goal

First production depreciation method.

---

# 687. FA16 Build

Straight-Line with:

```text
Useful life
Residual
Convention
Frequency
Rounding
Schedule
Golden vectors
```

---

# 688. FA16 Exit Gate

Reproducible exact results.

---

# 689. FA17 Goal

Launch-required additional methods/conventions.

---

# 690. FA17 Rule

Build only validated market/company needs.

---

# 691. FA17 Exit Gate

Every enabled method has golden-vector tests and versioned configuration.

---

# 692. FA18 Goal

Production depreciation run and Ledger integration.

---

# 693. FA18 Build

```text
Prepare
Exceptions
Preview
Post
Retry
Reverse
```

---

# 694. FA18 Exit Gate

No duplicate period/asset postings; Ledger reconciles.

---

# 695. FA19 Goal

Fast book balances/schedules.

---

# 696. FA19 Exit Gate

Projections rebuild exactly from Book transactions.

---

# 697. FA20 Goal

Capital additions/reductions.

---

# 698. FA20 Exit Gate

Source-linked value changes update future depreciation without rewriting prior posted history.

---

# 699. FA21 Goal

Accounting estimate changes.

---

# 700. FA21 Exit Gate

Useful-life/residual changes are prospective and historically explainable.

---

# 701. FA22 Goal

Impairment.

---

# 702. FA22 Exit Gate

Operational damage alone does not create impairment; approved financial event does.

---

# 703. FA23 Goal

Revaluation seam / build if market requires.

---

# 704. FA23 Exit Gate

No arbitrary Book value edit.

---

# 705. FA24 Goal

Grouped Asset controls.

---

# 706. FA24 Build

```text
Units
Distributions
Split
Partial disposal
```

---

# 707. FA24 Exit Gate

Grouped furniture scenario works without artificial individual serials.

---

# 708. FA25 Goal

Parent/component Assets.

---

# 709. FA25 Exit Gate

Building/component useful lives independent and no hierarchy cycles.

---

# 710. FA26 Goal

Physical verification.

---

# 711. FA26 Build

```text
Campaign
Expected snapshot
Scan
Mismatch
Resolution
```

---

# 712. FA26 Exit Gate

Mismatch never silently mutates assignment/location.

---

# 713. FA27 Goal

Safe high-volume operations.

---

# 714. FA27 Build

Mass transfer / permitted mass update with preview.

---

# 715. FA27 Exit Gate

Bulk operation result fully auditable and retry-safe.

---

# 716. FA28 Goal

Non-sale disposal.

---

# 717. FA28 Build

```text
Scrap
Lost
Stolen
Destroyed
Donation
```

---

# 718. FA28 Exit Gate

Original Asset remains historical; financial effect exact.

---

# 719. FA29 Goal

Asset Sale integration.

---

# 720. FA29 Exit Gate

Sale/Invoice/Payment/Disposal remain distinct and linked.

---

# 721. FA30 Goal

Partial disposal and reinstatement.

---

# 722. FA30 Exit Gate

Basis/accumulated depreciation reconcile before and after.

---

# 723. FA31 Goal

HR custody/offboarding integration.

---

# 724. FA31 Build

```text
Employee assigned-assets query
Return warning/gate
Self-service acknowledgement
```

---

# 725. FA31 Exit Gate

HR never owns Asset history.

---

# 726. FA32 Goal

Maintenance integration seam.

---

# 727. FA32 Exit Gate

Maintenance references canonical AssetId and does not create duplicate Equipment identity.

---

# 728. FA33 Goal

Country/Tax Book integration.

---

# 729. FA33 Build

Provider contracts for:

```text
tax book defaults
depreciation rules
special allowances
disposal rules
movement compliance
statutory reports later
```

---

# 730. FA33 Exit Gate

No country-specific branching spread through Assets Core.

---

# 731. FA34 Goal

Operational + financial reporting/reconciliation.

---

# 732. FA34 Exit Gate

Asset Book and GL differences drill to source.

---

# 733. FA35 Goal

Opening migration.

---

# 734. FA35 Build

```text
Opening Asset
Books
Assignments
Hierarchy
Documents
Legacy refs
Reconciliation
```

---

# 735. FA35 Exit Gate

Opening register matches approved legacy/GL totals.

---

# 736. FA36 Goal

Security/permissions/audit.

---

# 737. FA36 Attack Cases

```text
Cross-tenant Asset read
Cross-entity Book value read
Employee self IDOR
Ops user financial leakage
Forged EmployeeId
Forged LocationId
Forged FileRef
Unauthorized disposal
Unauthorized revaluation
Unauthorized depreciation post
Unauthorized mass transfer
Opening import misuse
```

---

# 738. FA36 Exit Gate

RLS + app authorization + field/document restrictions pass.

---

# 739. FA37 Goal

Performance and scale.

---

# 740. FA37 Load Tests

```text
1M Assets
100k+ depreciation lines/run
Large Capitalization Queue
100k verification campaign
Employee asset query burst
Asset tag scan burst
Large opening import
Large reconciliation
```

---

# 741. FA37 Query Plan Review

Real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot queries.

---

# 742. FA37 Exit Gate

Targets pass or evidence-based optimization ADR.

---

# 743. FA38 Goal

Operational recovery without DB surgery.

---

# 744. FA38 Runbooks

Required:

```text
Capitalization source mismatch
Duplicate asset candidate
Inventory serial conflict
Book setup missing
Depreciation calculation exception
Depreciation posting failure
Ledger outage
Incorrect assignment
Verification mismatch
Asset not found
Wrong disposal
Reinstatement
CIP allocation mismatch
Opening import mismatch
Projection rebuild
DR restore validation
```

---

# 745. FA38 Forbidden Runbook

Never normal fix:

```text
UPDATE fixed_asset SET employee_id = ...
UPDATE asset_book SET book_value = ...
DELETE asset_book_transaction ...
UPDATE accumulated_depreciation = ...
DELETE disposed_asset ...
```

---

# 746. FA38 Exit Gate

Critical failures have semantic commands/recovery.

---

# 747. FA39 Operational Asset Acceptance

Reviewer persona:

```text
IT/Facilities/Operations Asset Controller
```

---

# 748. FA39 Scenario — Laptop

```text
Create/receive
tag
assign
handover
transfer
return
verify
```

without accounting expertise.

---

# 749. FA39 Scenario — Land

Find:

```text
property
location
legal documents
cost summary permissioned
```

without irrelevant Employee/Serial fields.

---

# 750. FA39 Scenario — Vehicle Pool

Department/pool custody works.

---

# 751. FA39 Scenario — Verification

Scan campaign identifies wrong location and resolves through transfer.

---

# 752. FA39 Pass Criteria

```text
Asset identity trustworthy
Current holder/location obvious
History complete
No duplicate Product/Equipment master
No irrelevant workflow
```

---

# 753. FA40 Fixed Asset Accountant Acceptance

Reviewer:

```text
Fixed Asset Accountant
```

---

# 754. FA40 Scenario — Capitalization

10-laptop invoice and machine-many-costs both processed.

---

# 755. FA40 Scenario — CIP

Costs accumulate and split to final assets.

---

# 756. FA40 Scenario — Depreciation

Prepare/review/post monthly run with exceptions.

---

# 757. FA40 Scenario — Addition/Estimate Change

Future depreciation correct.

---

# 758. FA40 Scenario — Disposal

Sale/scrap gain/loss understandable.

---

# 759. FA40 Pass Criteria

Accountant can explain every Book value without manual spreadsheet.

---

# 760. FA41 HR Custody Acceptance

Reviewer:

```text
HR/Offboarding Specialist
```

---

# 761. FA41 Scenario

Employee termination shows all outstanding Assets and lets authorized workflow request/record return.

---

# 762. FA41 Pass Criteria

No duplicate Asset data in HR.

---

# 763. FA42 Finance / GL Reconciliation Acceptance

Validate:

```text
Asset Cost
Accumulated Depreciation
CIP
Impairment/Revaluation
Disposals/Gain-Loss
```

against Ledger.

---

# 764. FA42 Exit Gate

Zero unexplained difference.

---

# 765. FA43 Country / Tax Acceptance

Country specialist validates enabled:

```text
Tax Book
Depreciation methods/rates
Special allowance
Disposal tax treatment
Asset movement documents
Retention/statutory register
```

---

# 766. FA43 Exit Gate

No unverified local rule in production.

---

# 767. FA44 Cross-Module End-to-End Acceptance

---

# 768. FA44 Scenario — Purchased Laptop

```text
Purchasing/Invoicing
→ Inventory Serial
→ Capitalization
→ Asset
→ Employee Custody
→ Depreciation
→ Return
→ Disposal
→ Ledger
```

---

# 769. FA44 Scenario — Land + Building

```text
One acquisition source
→ cost allocation
→ Land non-depreciating
→ Building depreciating
→ separate books
→ GL reconciliation
```

---

# 770. FA44 Scenario — Factory CIP

```text
Purchasing/Expenses
→ CIP
→ cost additions
→ placed in service
→ component assets
→ depreciation
```

---

# 771. FA44 Scenario — Asset Sale

```text
Asset
→ Sales/Invoicing
→ Disposal
→ Gain/Loss
→ Payment later
```

---

# 772. FA44 Scenario — Employee Offboarding

```text
HR
→ query Assets
→ return/handover
→ offboarding gate
```

---

# 773. FA44 Exit Gate

All ownership boundaries pass on real PostgreSQL integration tests.

---

# 774. FA45 Release Gate

Fixed Assets does not ship because an Asset form exists.

It ships when the company can trust the physical and financial Asset Register.

---

# 775. FA45 Required Sign-Off

```text
Product Architect
Fixed Asset Accountant
Finance/Accounting Lead
Operations/IT Asset Reviewer
HR Reviewer
Inventory/Purchasing Reviewer
Tax/Country Specialist
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 776. FA45 Mandatory Evidence

```text
Automated tests
RLS/authorization report
Field/document restriction tests
Capitalization source reconciliation
Depreciation golden vectors
CIP reconciliation
Assignment history tests
Verification campaign results
Disposal/gain-loss tests
Asset-to-GL reconciliation
Opening migration reconciliation
Performance benchmarks
Operational acceptance
Accountant acceptance
Country Pack acceptance
Known limitations
```

---

# 777. Build Now — MUST

```text
Canonical Asset Registry
Asset Categories
Land/Building support
Individual + Grouped tracking foundation
Asset Tags/Serial references
Locations
Custody/Assignment/Transfer/Handover
Documents
Product/Inventory optional refs
Capitalization Candidate Queue
Many-to-many source allocation
CIP
Corporate Book
Non-depreciable assets
Straight-Line depreciation
Launch-required conventions/methods
Depreciation Runs
Ledger Facts
Book Balance/Schedule
Capital Additions/Reductions
Estimate Changes
Parent/Component foundation
Physical Verification
Disposal/Scrap/Lost/Stolen
Asset Sale integration
Opening Migration
HR Offboarding seam
Maintenance seam
Permissions/RLS/Audit
Reports/Reconciliation
Performance
Runbooks
Acceptance Gates
```

---

# 778. Build Later / Evidence-Based

```text
Advanced Revaluation
Advanced Impairment reversal
Complex Units-of-Production depreciation
Right-of-Use / Lease accounting
Advanced insurance claims
Full Fleet Management
Full Maintenance module
Government property registry integrations
Advanced Intercompany Asset Transfer
Advanced mass automation
IoT telemetry
AI valuation
AI physical verification
```

---

# 779. Current Competitor Benchmark — Oracle Fusion Assets

Official Oracle material shows mature patterns TODIR should learn from:

```text
Asset distributions can include:
- employee
- location
- cost/accounting distribution

Mass additions can:
- originate from Payables/import
- be split
- be merged
- add cost to existing Assets

Inventory/receipt-origin assets can preserve:
- serial
- location
- receipt/source cost

Asset book history is effective/history-oriented rather than a single overwritten value row.
```

Reference examples:
- https://docs.oracle.com/en/cloud/saas/financials/26c/fabof/bo_oraErpCoreFixedAssets_AssetDistribution.html
- https://docs.oracle.com/en/cloud/saas/financials/26d/faalm/how-fixed-asset-mass-additions-import-data-is-processed.html
- https://docs.oracle.com/en/cloud/saas/financials/25d/faalm/overview-of-assets-added-upon-receipt.html
- https://docs.oracle.com/en/cloud/saas/financials/26a/oedmf/fabooks-14026.html

TODIR takes:
- assignment/history separation;
- flexible source capitalization;
- split/merge/add-to-existing concepts;
- explicit book history.

TODIR avoids:
- exposing accounting complexity to ordinary asset custodians;
- duplicating maintenance identity.

---

# 780. Current Competitor Benchmark — Microsoft Dynamics 365 Finance

Current Dynamics Fixed Assets uses:

```text
Fixed Asset Groups
Books
Depreciation Profiles
Depreciation Conventions
Posting profiles
```

Books can post or not post to GL, allowing corporate/tax-like parallel valuation.

Official references:
- https://learn.microsoft.com/en-us/dynamics365/finance/fixed-assets/set-up-fixed-assets
- https://learn.microsoft.com/en-us/dynamics365/finance/fixed-assets/fixed-asset-depreciation-conventions
- https://learn.microsoft.com/en-us/dynamics365/finance/fixed-assets/fixed-asset-management-workspace

Useful patterns:
- independent Books;
- asset grouping/defaults;
- dedicated depreciation conventions;
- current valuation with drill-to-transactions;
- search by number/group/name/location/responsible person.

TODIR adopts these business concepts while keeping simple-company setup lighter.

---

# 781. Current Competitor Benchmark — Business Central

Business Central supports:

```text
Fixed Asset cards
Classes/Departments/Locations
Main Assets + Components
Depreciation Books
Acquisition
Maintenance
Sale/Disposal
```

Reference:
- https://learn.microsoft.com/en-us/dynamics365/business-central/fa-manage

Useful pattern:

```text
Building / Production Equipment as main asset
→ component list
```

TODIR keeps parent/component capability.

---

# 782. Current Competitor Benchmark — Odoo

Odoo separates operational Equipment/Maintenance concepts from accounting Assets.

Its equipment setup can include:

```text
company
Used By employee/department
maintenance team
technician
used-in location
```

Reference:
- https://www.odoo.com/documentation/18.0/applications/inventory_and_mrp/maintenance/add_new_equipment.html

TODIR learns from the operational assignment model but chooses:

> **One canonical Asset Registry where possible, with Maintenance referencing AssetId, instead of requiring the business to maintain duplicate independent identities for the same laptop/machine.**

---

# 783. Competitive Product Rule — Do Not Copy Complexity

Competitor capability is evidence that a business problem exists.

It is not permission to copy every field/workflow.

TODIR rule:

```text
Simple outside.
Powerful inside.
```

---

# 784. Competitive Product Rule — Asset Assignment

Employee, Location and accounting/cost ownership are separate dimensions.

This is consistent with mature enterprise Asset systems and required by real operations.

---

# 785. Competitive Product Rule — Books

Book abstraction is required from architecture Day 1 even if most SME customers see only one Book.

---

# 786. Competitive Product Rule — Mass Additions

Source-to-Asset creation must support:

```text
split
merge/allocation
add-to-existing
multiple units
```

rather than one invoice line = one asset.

---

# 787. Competitive Product Rule — Operational + Financial Asset

TODIR must avoid choosing between:

```text
Accounting Asset only
or
Equipment tracking only
```

Canonical Asset Registry must support both perspectives where appropriate.

---

# 788. Competitive Product Rule — History

Mature systems preserve distributions/book history.

TODIR treats history as a core invariant, not an audit afterthought.

---

# 789. Final Asset Behavior Matrix

Minimum launch validation matrix:

| Category | Product Link | Employee Custody | Physical Location | Individual/Grouped | Depreciation | Typical Extra Profile |
|---|---|---|---|---|---|---|
| Land | Optional/No | No | Required | Individual | Usually No | Real Estate |
| Building | Optional/No | No | Required | Individual/Components | Yes | Real Estate |
| Leasehold Improvement | Optional | No | Required | Individual | Yes | Real Estate |
| Vehicle | Optional | Employee/Pool | Required | Individual | Yes | Vehicle |
| Laptop | Optional | Usually Employee | Optional/Useful | Individual | Yes | Equipment |
| Phone | Optional | Usually Employee | Optional | Individual | Yes | Equipment |
| Machine | Optional | Dept/Flexible | Required | Individual/Components | Yes | Equipment |
| Furniture | Optional | Optional | Required | Individual/Grouped | Yes | None |
| CIP | Source-driven | No | Site optional | Individual/Project-like | No until service | CIP |
| Software/Intangible | Usually No | No | N/A | Individual | Amortization/Policy | Intangible |

This is a product baseline, not legal/accounting rule hardcoding.

---

# 790. Final Land Rule

> **Land is a valid Fixed Asset even with no Product, no Serial, no Employee Custodian and no depreciation.**

---

# 791. Final Custody Rule

> **Employee custody is an optional effective-dated relationship. It never means the Employee owns the Asset and it never replaces Physical Location, Cost Owner or Legal Owner.**

---

# 792. Final Product Rule

> **Products describe what the business buys/sells; Assets describe specific long-lived resources the business controls. They are separate canonical tables/domains with optional references.**

---

# 793. Final Source Rule

> **One source can create many Assets, and many source costs can capitalize one Asset. Source allocation is first-class.**

---

# 794. Final CIP Rule

> **CIP accumulates qualifying capital costs without depreciation until the asset is available for use and explicitly capitalized.**

---

# 795. Final Book Rule

> **Book Value is reconstructed from immutable Book transactions and opening state; users never edit Book Value directly.**

---

# 796. Final Depreciation Rule

> **Depreciation is a deterministic, versioned Asset Book calculation that emits semantic Ledger facts; it is not a manual Journal shortcut.**

---

# 797. Final History Rule

> **Assignments, transfers, cost changes, depreciation, verification and disposals preserve history instead of overwriting the past.**

---

# 798. Final Operational Rule

> **A fully depreciated Asset can still be physically active, assigned and operationally important until it is actually disposed.**

---

# 799. Final Disposal Rule

> **Disposal is a business/financial event that preserves the Asset forever in history; it is never Delete.**

---

# 800. Final Verification Rule

> **Physical verification discovers reality; mismatches create review exceptions and never silently rewrite custody/location.**

---

# 801. Final Maintenance Rule

> **Maintenance can operate on the same canonical AssetId; TODIR should not create a second independent equipment master for the same physical asset.**

---

# 802. Final HR Rule

> **HR may show and gate employee asset returns, but Fixed Assets owns every custody/return event.**

---

# 803. Final Accounting Rule

> **Fixed Assets calculates and explains Asset financial lifecycle; Ledger owns every Journal Entry and account mapping.**

---

# 804. Final Country Rule

> **Tax depreciation, special allowances and statutory movement/disposal requirements come through Country Providers, not country `if` statements in Assets Core.**

---

# 805. Final Small-Company Rule

> **A small company should be able to create an Asset, assign it, depreciate it and dispose it without configuring tax books, component accounting, verification campaigns or enterprise mass processes.**

---

# 806. Final Enterprise Rule

> **A large company should be able to add Books, CIP, components, grouped assets, mass additions, verification campaigns and stricter controls without replacing the Asset core.**

---

# 807. Final Product Verdict

TODIR Fixed Assets is release-ready only when:

```text
Land works with no Product/Employee/Depreciation.
Buildings and land can be separated from one acquisition.
Laptop custody history is trustworthy.
Employee assignment is distinct from location and legal owner.
Grouped furniture does not require fake serials.
Product and FixedAsset remain separate canonical models.
Inventory Serial can link without becoming the Asset record.
One source can generate many Assets.
Many sources can capitalize one Asset.
CIP cannot depreciate before placed in service.
Corporate Book works simply.
Multiple Books remain possible.
Book Value is derived, not editable.
Depreciation is deterministic and retry-safe.
Posted history is immutable.
Capital additions and estimate changes preserve prior history.
Physical verification does not silently mutate truth.
Zero NBV does not auto-dispose an Asset.
Disposal never deletes Asset history.
Asset Sale remains distinct from Payment.
Cross-entity transfer cannot be a LegalEntityId update.
Opening migration needs no fake invoices.
Asset Register reconciles to Ledger.
HR offboarding can identify unreturned Assets.
Operational users do not see financial/legal data without permission.
Large datasets remain fast.
Accountant, Operations, HR and Country acceptance tests all pass.
```

---

# 808. Final Agent Rule

For every Fixed Assets implementation decision, the Agent must answer:

> **What real resource is being controlled, who legally owns it, where is it, who has custody/responsibility, what financial Book facts exist, which source created or changed its value, what history must remain immutable, and which other TODIR domain owns any truth that Fixed Assets is only referencing?**

If any answer is unclear, the implementation is incomplete.

---

# 809. No Separate Asset Subfeature Packs

Do **not** create separate architecture packs for:

```text
Asset Register
Asset Custody
Asset Locations
Depreciation
CIP
Asset Verification
Asset Disposal
Asset Tags
Asset Documents
Asset Books
```

They are all part of `22 Fixed Assets`.

A future full Maintenance module remains separate because Work Orders/maintenance planning are a materially different business domain.

---

# 810. Next Planning Rule

Continue one comprehensive file per actual business module.

The likely next candidate is `23 Projects`, but sequence remains an explicit product/architecture decision based on MVP priority.

---

**END OF FILE — 22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
