# 27 — TODIR ERP SaaS Quality Management Requirements & Technical Design Pack

**File:** `27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + QUALITY PLANNING + INSPECTION + SAMPLING + NONCONFORMANCE + DISPOSITION + CAPA + SUPPLIER QUALITY + COA + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-07  
**Audience:** Product Owner, Product Architect, Quality Manager, Quality Engineer, Quality Inspector, Supplier Quality Engineer, Manufacturing Manager, Production Planner, Warehouse/Receiving Lead, Purchasing Lead, Inventory Lead, Service/Maintenance Lead, Sales/CRM Lead, Cost Accountant, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — PRODUCT CONFORMANCE + STOCK RELEASE + SUPPLIER QUALITY + MANUFACTURING GATES + TRACEABILITY + CUSTOMER SAFETY + COMPLIANCE**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:

- Catalog owns Product/Variant and UOM identity.
- Inventory owns stock quantity, availability, lot/serial identity, material status, warehouse/location, reservations, physical movements and valuation.
- Purchasing owns supplier commercial commitments, Purchase Orders and supplier return commercial orchestration.
- Manufacturing owns Production Definitions, Production Orders, operations, material requirements, output and production execution.
- Manufacturing explicitly delegates inspection/NCR/CAPA truth to Quality.
- Sales/CRM/Helpdesk own commercial/customer interaction intake; Quality owns formal quality investigation only after linked handoff.
- Fixed Assets owns company Asset identity.
- Service & Maintenance owns maintenance/calibration work and equipment service history.
- Ledger owns accounting.
- Platform owns Files, Audit, Approvals, Numbering, Notifications and Jobs.
- No Quality code may directly mutate Inventory, Manufacturing, Purchasing, Service, Sales or Ledger tables.

---

# 0. Executive Decision

TODIR Quality Management is the canonical business capability that answers:

> **What quality requirement applies, what must be inspected, how much should be sampled, what was actually measured, did the result conform, what quantity must be held or released, what defect/nonconformance exists, what disposition is authorized, why did the problem happen, what corrective/preventive action is required, and did that action actually prevent recurrence?**

The module must serve:

```text
Incoming supplier quality
Inventory quality
In-process manufacturing quality
Final production quality
Outgoing quality
Ad-hoc quality inspection
Supplier nonconformance
Customer-linked quality issues
Formal asset/service quality inspections where required
CAPA
COA / quality evidence
Supplier quality analytics
SPC-ready measurement history
```

---

# 1. Product Principle

> **Define the quality requirement once; trigger the right inspection automatically where the real work happens.**

The receiver, operator or inspector should not rebuild inspection criteria every transaction.

---

# 2. Workflow Principle

> **Domain separation must never become workflow fragmentation.**

A Receiving user may see:

```text
Receive
→ Inspect
→ Release / Hold
```

inside one guided experience even though:

```text
Purchasing owns PO
Inventory owns receipt/stock
Quality owns inspection/disposition
```

---

# 3. Simplicity Principle

Small company:

```text
Product
When: Receive
Check:
- Dimension 10 ± .05 mm
- Surface Pass/Fail
Sample: 10%

If Fail:
Hold + Notify Quality
```

Enough.

Large organization can enable:

```text
AQL
Critical/Major/Minor
Control Plans
Sampling Schemes
Supplier-specific rules
MRB
CAPA
Electronic signatures
Instrument trace
SPC
COA
```

without replacing the core.

---

# 4. Core Distinctions

Absolute:

```text
Quality Plan != Inspection
Inspection != Sample
Inspection != Result
Result != Evaluation
Evaluation != Disposition

Defect != Nonconformance
Quality Issue != Confirmed NCR

Containment != Correction
Correction != Corrective Action
Corrective Action != Preventive Action
Action Completed != Action Effective

Quality Hold != Inventory Location
Quality Disposition != Inventory Movement

Produced Quantity != Quality Released Quantity

Failed Inspection != Reinspection

Supplier Return != Supplier NCR
Customer Return != Quality NCR

Specification Limit != Control Limit

Inspection Instrument != Inspection Characteristic

Inspection Result != Certificate Of Analysis

Quality Cost != Journal Entry

Routine Service Checklist != Formal QMS Inspection
```

---

# 5. Quality Is Cross-Functional

Quality is not a Manufacturing subfeature only.

Supported contexts:

```text
Receiving
Inventory
Manufacturing WIP
Manufacturing Output
Outgoing Delivery
Customer Return
Ad Hoc Inventory
Formal Asset / Maintenance inspection
Supplier issue
Customer complaint-linked quality investigation
```

---

# 6. Quality Ownership

Quality owns:

```text
Quality Plan
Quality Characteristic
Quality Specification
Sampling Plan
Inspection
Sample
Inspection Result
Evaluation
Quality Issue
Defect
Nonconformance / NCR
Containment
Disposition decision
Root Cause
CAPA
Effectiveness Check
Supplier Quality Case
Quality Certificate / COA
Quality analytics/read models
```

---

# 7. Quality Does Not Own

```text
Product → Catalog
Supplier/Customer → Directory
Stock/lot/location/movement → Inventory
Purchase Order/return commercial flow → Purchasing
Production Order/operation/output → Manufacturing
Customer Return → Sales/Inventory
Maintenance Work Order → Service & Maintenance
Asset identity → Fixed Assets
Journal Entry → Ledger
```

---

# 8. Quality Plan

A `QualityPlan` answers:

> **When this context occurs, what must be checked, how, using what sampling/evidence rules, and what should happen when results are outside requirements?**

---

# 9. Quality Plan Concept

```text
QualityPlan
{
    Id
    LegalEntityId
    Code
    Name
    PlanType
    State
    CurrentVersionId?
}
```

---

# 10. Quality Plan Types

Initial:

```text
Receiving
Inventory
ManufacturingInProcess
ManufacturingFinal
Outgoing
ReturnInspection
AdHoc
FormalAssetInspection
```

---

# 11. Routine Service Boundary

A routine maintenance checklist remains `24 Service & Maintenance`.

Only when the organization requires a formal QMS-controlled inspection with:

```text
quality specification
formal evaluation
quality disposition
NCR/CAPA
```

does `27` own the formal inspection.

---

# 12. Quality Plan Version

```text
QualityPlanVersion
{
    Id
    QualityPlanId
    Revision
    State
    ValidFrom
    ValidTo?
    SamplingPlanVersionId?
    ApprovalState?
}
```

---

# 13. Quality Plan Version States

Recommended:

```text
Draft
UnderReview
Approved
Active
Superseded
Obsolete
```

Small companies can hide review/approval.

---

# 14. Historical Rule

Once an Inspection is created from a plan version, later plan changes must not alter the inspection requirements or limits historically applied.

---

# 15. Plan Snapshot

Inspection stores exact snapshot/version references for:

```text
characteristics
specifications
sampling
action rules
evidence requirements
```

---

# 16. Effective Dating

Example:

```text
Plan v3
Valid through 31 Oct

Plan v4
Valid from 01 Nov
```

Old inspections remain on v3.

---

# 17. Plan Applicability

Plan applicability can be based on:

```text
Product
Product Category
Supplier
Supplier Site
Plant
Warehouse
Location/Subinventory
Manufacturing Operation
Work Center
Customer
Delivery type
Return reason
Asset category/context
Country/Legal Entity
```

Only fields relevant to PlanType are available.

---

# 18. Quality Trigger Rule

```text
QualityTriggerRule
{
    PlanVersionId
    TriggerType
    Criteria
    FrequencyPolicy
    Priority
}
```

---

# 19. Trigger Types

Potential:

```text
PurchaseReceipt
InventoryMovement
InventoryAdHoc
ProductionOperationStart
ProductionOperationComplete
ProductionOutput
DeliveryBeforeShip
CustomerReturnReceipt
MaintenanceFormalInspection
Manual
ScheduledReinspection
```

---

# 20. Trigger Resolution

Given source transaction/context TODIR must deterministically answer:

```text
Which Quality Plan applies?
Is inspection mandatory?
How many inspections/samples?
Does source quantity need hold?
```

---

# 21. Ambiguous Plans

If two equally applicable mandatory plans conflict:

```text
block and show configuration error
```

unless explicit merge/priority rule exists.

Never choose randomly.

---

# 22. Multiple Compatible Plans

Can coexist when they represent different required checks:

```text
Regulatory plan
Customer-specific plan
Internal control plan
```

Duplicate characteristic handling must be explicit.

---

# 23. Quality Characteristic

A `QualityCharacteristic` defines:

> **What property or evidence are we evaluating?**

Examples:

```text
Diameter
Length
Weight
Hardness
pH
Moisture
Color
Surface condition
Packaging integrity
Label correct
Certificate present
Seal intact
```

---

# 24. Characteristic Library

```text
QualityCharacteristic
{
    Id
    Code
    Name
    ResultType
    DefaultUom?
    Category?
    State
}
```

---

# 25. Characteristic Result Types

Initial:

```text
Measurement
PassFail
Boolean
Categorical
DefectCount
Text
PhotoEvidence
DocumentVerification
SignatureApproval
Calculated
```

---

# 26. Measurement

Stores exact value and UOM.

Example:

```text
10.02 mm
```

never just:

```text
10.02
```

---

# 27. Measurement UOM

Must use compatible Catalog/UOM dimension.

---

# 28. Document Verification

Example:

```text
Supplier Certificate Of Analysis present?
Certificate number?
Expiry?
```

Use Platform Files for documents.

---

# 29. Photo Evidence

Can require:

```text
At least one photo
Label photo
Defect photo
Before/after
```

through Platform Files.

---

# 30. Quality Specification

Specification says:

> **What values/outcomes are acceptable for this characteristic in this context?**

---

# 31. Specification Version

```text
QualitySpecificationVersion
{
    Id
    CharacteristicId
    Revision
    ResultType
    Target?
    LowerSpecLimit?
    UpperSpecLimit?
    Uom?
    AllowedValues?
    SeverityOnFailure?
    ValidFrom
    ValidTo?
}
```

---

# 32. Numeric Specification

Example:

```text
Diameter
LSL = 9.95 mm
USL = 10.05 mm
Target = 10.00 mm
```

---

# 33. Tolerance Convenience

UI may accept:

```text
10.00 ± 0.05
```

but domain stores exact lower/upper limits.

---

# 34. Specification Limit != Control Limit

Specification limits define acceptance.

Control limits belong to statistical process stability.

Never collapse them.

---

# 35. Characteristic Group

Reusable bundle:

```text
Incoming Steel Checks
Final Paint Checks
Packaging Checks
```

---

# 36. Quality Plan Line

```text
QualityPlanCharacteristic
{
    PlanVersionId
    CharacteristicId
    SpecificationVersionId
    Sequence
    Required
    EvidenceRequirement?
    FailureActionProfile?
}
```

---

# 37. Required vs Optional

Required result blocks completion if missing.

Optional result can be omitted.

---

# 38. Inspection Method

Reusable method reference:

```text
Internal SOP Q-101
Visual Inspection WI-22
ASTM / ISO method reference
```

Historical inspection retains method version/reference.

---

# 39. Sampling Plan

Sampling answers:

> **How much of the lot/population must be inspected and how is acceptance decided?**

---

# 40. Sampling Modes

```text
FullInspection
FixedQuantity
Percentage
EveryNthUnit
PerLot
PerBatch
TimeBased
AcceptanceSamplingAQL
ManualDefined
```

---

# 41. Full Inspection

```text
100%
```

---

# 42. Fixed Quantity

Example:

```text
Inspect 20 units per receipt
```

---

# 43. Percentage

Example:

```text
Inspect 10%
```

with explicit rounding.

---

# 44. Every Nth Unit

Example:

```text
Every 100th output
```

---

# 45. Time-Based Sampling

Example:

```text
One sample every 2 hours
```

---

# 46. AQL Acceptance Sampling

Advanced support:

```text
Lot Size
Inspection Level
AQL
Sample Size
Acceptance Number
Rejection Number
```

---

# 47. Defect Classes

```text
Critical
Major
Minor
```

with organization-defined meanings.

---

# 48. Sampling Plan Version

```text
SamplingPlanVersion
{
    Id
    Revision
    Mode
    Parameters
    AcceptancePolicy
    State
    ValidFrom
    ValidTo?
}
```

---

# 49. Frequency != Sample Size

Example:

```text
Every receipt
```

is frequency.

```text
20 pieces
```

is sample size.

---

# 50. Dynamic Sampling Foundation

Future/advanced:

```text
Reduced
Normal
Tightened
Skip-Lot
```

based on supplier/process quality history.

Do not make it hidden Day-1 automation.

---

# 51. Inspection

An `Inspection` is the actual quality-control event.

---

# 52. Inspection Concept

```text
Inspection
{
    Id
    Number
    LegalEntityId
    PlanVersionId?
    PlanSnapshotId?

    ContextType
    SourceRef
    SubjectType
    SubjectRef

    PopulationQty?
    Uom?

    State
    EvaluationState
    DispositionState

    RequiredBy?
    AssignedTo?
    CreatedAt
    DueAt?
}
```

---

# 53. Inspection Context Types

```text
Receiving
Inventory
ProductionOperation
ProductionOutput
OutgoingDelivery
CustomerReturn
AdHocInventory
FormalAssetInspection
```

---

# 54. Subject Scope

Can target:

```text
Product
Lot
Serial
Quantity segment
Production Order
Operation
Inventory location
Asset
```

---

# 55. Inspection State

```text
Pending
Ready
InProgress
Completed
Cancelled
Superseded
```

---

# 56. Evaluation State

Separate:

```text
NotEvaluated
Pass
Fail
Conditional
Inconclusive
```

---

# 57. Disposition State

Separate:

```text
NotRequired
Pending
PartiallyDecided
Decided
Executed
```

---

# 58. Inspection Completed != Passed

Absolute.

---

# 59. Inspection Passed != Inventory Released

Absolute.

Inventory release is a downstream physical/status action.

---

# 60. Auto-Created Inspection

Normal path:

```text
Source event
→ resolve Quality Plan
→ calculate sampling
→ create Inspection
→ optional Quality Hold
```


# 61. Manual Inspection

Authorized user can:

```text
Inspect Now
```

for an ad-hoc context.

---

# 62. Ad-Hoc Inspection

Does not require a parent transaction.

Potential subjects:

```text
Inventory lot
Inventory serial
Product sample
Production resource/context
Asset
Formal service subject
```

---

# 63. Ad-Hoc Physical Action Rule

If no inventory/transaction context exists, Quality can record:

```text
results
evaluation
issue
```

but cannot invent:

```text
scrap
stock release
material movement
```

without an owning Inventory subject/action.

---

# 64. Reinspection

A reinspection is a new Inspection.

```text
ReinspectionOfInspectionId
```

---

# 65. Failed History Rule

Never convert:

```text
Inspection 1 = Fail
```

into Pass because the material was reworked.

Correct:

```text
Inspection 1 = Fail
→ Rework
→ Inspection 2 = Pass
```

---

# 66. Quality Sample

A sample represents the actual inspected subset.

---

# 67. Sample Concept

```text
QualitySample
{
    Id
    InspectionId
    SampleNumber
    Qty
    Uom
    LotRef?
    SerialRefs?
    CollectedAt?
    CollectedBy?
    SampleState
}
```

---

# 68. Sample States

Advanced:

```text
Planned
Collected
InTesting
Tested
Archived
Disposed
```

---

# 69. Simple Sampling

Small organizations can use one logical sample set without managing physical sample custody.

---

# 70. Physical Sample Management

Advanced regulated industries can enable:

```text
Sample label
Chain of custody
Storage condition
Retention
Archive
Disposal
```

---

# 71. Sample Label

Platform barcode/label capability.

---

# 72. Chain Of Custody

If enabled:

```text
CollectedBy
CollectedAt
TransferredTo
ReceivedBy
StorageLocation
Timestamps
```

---

# 73. Retained Sample

Optional policy:

```text
Retain 90 days
Retain until shelf-life + N days
Retain per regulation
```

---

# 74. Sample Disposal

Explicit event with:

```text
reason
actor
timestamp
approval if required
```

---

# 75. Inspection Result

Each captured observation is a result.

---

# 76. Result Concept

```text
InspectionResult
{
    Id
    InspectionId
    SampleId?
    CharacteristicSnapshotId

    ResultType

    NumericValue?
    Uom?
    BooleanValue?
    CategoricalValue?
    TextValue?
    FileRef?

    RecordedAt
    RecordedBy

    Evaluation
    FailureReason?
}
```

---

# 77. Numeric Result

Example:

```text
10.02 mm
```

server evaluated.

---

# 78. Qualitative Result

Example:

```text
Surface = Rust
```

mapped against specification.

---

# 79. PassFail Result

User records direct:

```text
Pass
Fail
```

only for characteristics where direct evaluation is the requirement.

---

# 80. Defect Count Result

Example:

```text
Critical = 0
Major = 1
Minor = 3
```

for sampling acceptance.

---

# 81. Photo Result

Evidence via Platform Files.

---

# 82. Document Result

Verify:

```text
Supplier COA
Certificate
Packing certificate
Calibration document
```

without duplicating files.

---

# 83. Result Evaluation

Characteristic-level:

```text
Pass
Fail
Warning
NotEvaluated
```

---

# 84. Automatic Evaluation

Measurement and categorical specs should evaluate server-side.

---

# 85. Manual Evaluation Override

If allowed:

```text
permission
reason
approval
audit
```

required.

---

# 86. Result Correction

Completed result is not silently edited.

Use:

```text
CorrectionOfResultId
CorrectionReason
```

---

# 87. Corrected Result

Old value remains visible in audit/history.

---

# 88. Result Timestamp

Capture actual measurement time, not only database save time when relevant.

---

# 89. Backdated Result

Restricted.

Requires:

```text
permission
reason
actual measured-at
recorded-at
```

---

# 90. Instrument Requirement

A characteristic may require a specified measurement instrument class/type.

---

# 91. Instrument Use

Record:

```text
InstrumentRef
CalibrationStatusAtUse
CalibrationDueAt?
InstrumentSerial?
```

as permitted.

---

# 92. Instrument Identity Boundary

Quality references canonical identity where available:

```text
FixedAsset
Inventory Serial
Service Equipment reference
```

No duplicate financial/maintenance master.

---

# 93. Calibration Boundary

`24 Service & Maintenance` owns calibration/maintenance work.

Quality consumes calibration validity.

---

# 94. Calibration Check

Before result:

```text
Is instrument calibration valid at measurement time?
```

---

# 95. Expired Calibration Policy

Possible:

```text
Block
Warn + Supervisor Approval
Allow only informational result
```

plan-specific.

---

# 96. Calibration Impact Analysis

If an instrument is later found out of tolerance:

```text
Find all inspections/results measured by instrument during affected interval
```

mandatory architecture capability when instrument trace is enabled.

---

# 97. Measurement Method

Characteristic may require:

```text
Specific method
Specific instrument family
Sample conditioning
Measurement location
```

---

# 98. Inspector Qualification

A plan/characteristic may require:

```text
Qualified Inspector
Certification
Skill
```

HR owns qualification identity/source.

Quality enforces requirement.

---

# 99. Inspector Qualification Expiry

Check at inspection time.

---

# 100. Electronic Signature

Optional high-regulation capability.

May require:

```text
Inspector signature
Reviewer signature
Disposition approval signature
CAPA approval signature
```

---

# 101. E-Signature Meaning

Signature records:

```text
who
when
what exact record/version
meaning/purpose
```

not just an image.

---

# 102. Reviewer Workflow

Optional:

```text
Inspector records results
→ Reviewer verifies
→ Inspection evaluation finalized
```

---

# 103. Small Company Default

Reviewer step OFF unless configured.

---

# 104. Inspection Instructions

Inspector sees:

```text
sample size
what to inspect
method
limits
instrument
evidence required
```

---

# 105. Inspector Mobile UX

Minimal:

```text
Pending
Scan
Measure
Photo
Defect
Complete
```

---

# 106. Barcode First

Scan can resolve:

```text
Receipt
Lot
Serial
Production Order
Operation
Inspection
Sample
```

---

# 107. Offline-Ready Inspection

Architecture supports client-generated command IDs and retry-safe result submission.

Full offline sync is optional/evidence-based.

---

# 108. Offline Critical Rule

Never silently overwrite central:

```text
Disposition
Hold release
NCR approval
```

from stale offline state.

---

# 109. Inspection Evaluation

Inspection-level evaluation aggregates characteristic/sample results according to plan policy.

---

# 110. Evaluation Inputs

Potential:

```text
Required characteristic results
Sampling acceptance
Critical-defect policy
Mandatory document presence
Reviewer approval
```

---

# 111. Inspection Pass

Means:

> Results satisfy evaluation policy.

It does **not** itself mean stock has physically moved/released.

---

# 112. Inspection Fail

Means:

> One or more requirements fail according to the plan/evaluation rule.

---

# 113. Conditional Evaluation

Used only for controlled scenarios.

Example:

```text
Technically outside target but within authorized conditional range
```

Needs explicit policy.

---

# 114. Inconclusive

Used when:

```text
sample compromised
instrument failure
insufficient sample
missing required evidence
```

May require new sample/reinspection.

---

# 115. Evaluation Rule Version

Historical evaluation must identify rule/version used.

---

# 116. Quality Action Rule

Allows plan-driven response to results.

---

# 117. Action Rule Concept

```text
QualityActionRule
{
    PlanVersionId
    Conditions
    Actions
    Priority
    State
}
```

Constrained predicates/actions only.

---

# 118. Action Rule Conditions

Examples:

```text
Characteristic failed
Critical defect count > 0
Measurement > USL
Inspection failed
Supplier = X
Product category = Y
```

---

# 119. Action Rule Actions

Potential:

```text
Display Instruction
Notify Role/User
Request Quality Hold
Create Quality Issue
Create NCR Candidate
Require Supervisor Review
Create Maintenance Request
Require Reinspection
```

---

# 120. No Arbitrary Script Action

No tenant:

```text
SQL
C#
JavaScript
Python
```

inside Quality rules.

---

# 121. Maintenance Action Rule

Formal asset inspection can request:

```text
Create Maintenance Request
```

through `24`.

Quality does not create Maintenance DB rows directly.

---

# 122. Inventory Hold Action

Quality can request:

```text
Hold quantity / lot / serial
```

through Inventory contract.

---

# 123. Quality Hold

A Quality Hold is a QMS decision/restriction request.

---

# 124. Quality Hold != Inventory Location

Locked.

A quantity may remain physically in the same location but have unavailable status.

---

# 125. Quality Hold Concept

```text
QualityRestriction
{
    Id
    InspectionId?
    NCRId?
    SubjectType
    SubjectRef
    Qty?
    Uom?
    RestrictionType
    Reason
    State
}
```

---

# 126. Restriction Types

Potential:

```text
HoldForInspection
Quarantine
DoNotConsume
DoNotShip
DoNotUseInProduction
RestrictedUse
```

---

# 127. Inventory Execution

Inventory owns actual:

```text
Material Status
Quarantine Location
Availability
Movement
Reservation eligibility
```

---

# 128. Hold Request Idempotency

Retry must not create duplicate restrictions/movements.

---

# 129. Partial Quantity Hold

Supported.

Example:

```text
Received 1000
Hold 200
Release 800
```

when quality/disposition permits.

---

# 130. Lot Hold

Can hold entire lot.

---

# 131. Serial Hold

Can hold exact serial.

---

# 132. Scope Escalation

If defect indicates systemic risk, authorized Quality user may expand:

```text
one sample
→ entire lot
→ related lots
→ related production batch
```

with explicit evidence/reason.

---

# 133. Hold Release

Requires authorized Quality disposition/evaluation.

Inventory executes status release.

---

# 134. Hold Release Audit

Capture:

```text
who
why
which quantity
which source decision
when
```

---

# 135. Disposition

Disposition answers:

> **What are we authorized to do with the affected quantity/material/product?**

---

# 136. Disposition != Evaluation

A failed inspection still needs a business disposition.

---

# 137. Disposition Types

Foundation:

```text
Accept
Release
UseAsIs
Concession
Rework
Repair
SortScreen
Reinspect
ReturnToSupplier
Scrap
DowngradeRegrade
Hold
OtherApproved
```

---

# 138. Use As Is

Allows nonconforming material/product under authorized deviation/concession.

High-control action.

---

# 139. Concession

Formal authorization to use/release nonconforming output under defined limits.

May require:

```text
Quality Manager
Engineering
Customer
Regulatory approval
```

depending context.

---

# 140. Planned Deviation != Concession

Planned Deviation:

```text
approved exception before/during execution
```

Concession:

```text
acceptance after actual nonconformance
```

Keep separate.

---

# 141. Rework

Bring item/product back into conformance through approved work.

---

# 142. Repair

Make usable/functionally acceptable without necessarily restoring original specification.

Industry policy decides whether allowed.

---

# 143. Sort / Screen

Inspect affected population more broadly to separate conforming/nonconforming items.

---

# 144. Reinspect

Create new inspection after correction/rework.

---

# 145. Return To Supplier

Quality decides disposition.

Purchasing + Inventory execute commercial/physical return.

---

# 146. Scrap

Quality decides disposition.

Inventory owns stock scrap movement.

Ledger owns accounting.

---

# 147. Downgrade / Regrade

Change intended quality grade/category where product model supports it.

Catalog identity change rules must be respected.

---

# 148. Disposition Quantity Lines

One NCR/inspection can split quantity:

```text
300 Rework
100 Use As Is
50 Return Supplier
50 Scrap
```

---

# 149. Disposition Line Concept

```text
QualityDispositionLine
{
    Id
    SourceCaseId
    DispositionType
    Qty
    Uom
    Reason
    ApprovalState
    ExecutionRef?
}
```

---

# 150. Disposition Conservation

For affected quantity:

```text
Sum(final disposition quantities)
=
Quantity under decision
```

except explicitly open/pending remainder.


# 151. Quality Issue

A Quality Issue means:

> **A reported or detected condition that requires investigation and may or may not become a confirmed nonconformance.**

---

# 152. Quality Issue Sources

Potential:

```text
Inspection
Receiving
Manufacturing
Warehouse
Customer complaint
Service
Supplier
Audit
Employee report
Trend/SPC signal
```

---

# 153. Quality Issue != NCR

Example:

```text
"Paint looks unusual"
```

is an issue.

After measurement:

```text
Color ΔE outside specification
```

becomes a confirmed NCR.

---

# 154. Quality Issue Concept

```text
QualityIssue
{
    Id
    Number
    LegalEntityId
    SourceType
    SourceRef?
    SubjectType
    SubjectRef?
    Title
    Description
    Severity
    State
    ReportedBy
    ReportedAt
    Owner?
}
```

---

# 155. Quality Issue States

Recommended:

```text
New
Triaged
Investigating
ConfirmedNCR
ResolvedNoNCR
Cancelled
Closed
```

---

# 156. Triage

Triage determines:

```text
valid quality concern?
severity?
scope?
immediate containment?
NCR required?
```

---

# 157. Immediate Containment

A critical issue may trigger hold before root cause is known.

---

# 158. Defect

A Defect is an observed undesirable condition.

Examples:

```text
Scratch
Crack
Wrong Dimension
Wrong Color
Contamination
Missing Label
Loose Fastener
Leak
```

---

# 159. Defect != Nonconformance

A defect can be recorded before exact requirement violation is confirmed.

---

# 160. Defect Classification

Controlled dictionaries:

```text
DefectCategory
DefectType
Severity
Location
OccurrencePoint
```

---

# 161. Severity

Initial:

```text
Critical
Major
Minor
Observation
```

organization may configure wording.

---

# 162. Critical Severity

Can auto-trigger:

```text
full hold
manager escalation
shipment block request
CAPA requirement
```

according to rule.

---

# 163. Defect Quantity

Track:

```text
AffectedQty
DefectCount
SampleQty
PopulationQty
```

separately.

---

# 164. Defect Location

Examples:

```text
Surface
Edge
Weld
Thread
Packaging
Label
Seal
```

useful for Pareto.

---

# 165. Defect Attachment

Photo/evidence through Files.

---

# 166. Nonconformance

A Nonconformance means:

> **A defined requirement was not met.**

---

# 167. NCR Concept

```text
Nonconformance
{
    Id
    Number
    LegalEntityId

    SourceType
    SourceRef?

    SubjectType
    SubjectRef

    RequirementRef?
    CharacteristicRef?
    SpecificationSnapshot?

    AffectedQty?
    Uom?

    Severity
    Origin
    State

    Owner
    DetectedAt
    ConfirmedAt?
}
```

---

# 168. NCR Origins

Potential:

```text
Supplier
Incoming
Inventory
ManufacturingProcess
ManufacturingOutput
Packaging
Warehouse
Delivery
Customer
Service
DesignEngineering
Unknown
```

---

# 169. NCR States

Recommended:

```text
Draft
UnderReview
Confirmed
Contained
DispositionPending
ActionInProgress
AwaitingVerification
Closed
Cancelled
```

Do not overload with CAPA states.

---

# 170. NCR Confirmation

Requires:

```text
requirement violated
evidence/result
affected subject/quantity
severity
```

unless explicitly manual management case.

---

# 171. NCR Numbering

Platform Numbering.

---

# 172. NCR From Inspection

Failed result can create:

```text
NCR Candidate
```

or direct NCR according to plan.

---

# 173. Automatic NCR Safety

Do not create thousands of duplicate NCRs for one systemic event.

Support grouping policy by:

```text
Inspection
Lot
Production Order
Defect type
```

---

# 174. One Inspection Many NCRs

Allowed if independent nonconformances need separate ownership/disposition.

---

# 175. One NCR Many Defects

Allowed.

---

# 176. NCR Scope

Can target:

```text
specific quantity
lot
serial
batch
operation
shipment
supplier receipt
```

---

# 177. NCR Scope Expansion

Authorized action can expand affected population when investigation identifies broader risk.

---

# 178. NCR Linking

Support:

```text
DuplicateOf
RelatedTo
CausedBy
Parent/Child
RepeatOf
```

without merging historical records.

---

# 179. Repeat NCR

Derived/flagged based on:

```text
same product
same defect/cause
same supplier/process
within configured period
```

---

# 180. NCR Recurrence

Important CAPA input.

---

# 181. Containment

Containment means:

> **Immediate action to prevent further escape/use while root cause and permanent action are investigated.**

---

# 182. Containment Examples

```text
Hold current lot
Stop shipment
Stop production line
Sort existing stock
Notify customer
Block supplier lot
100% inspect next receipt
```

---

# 183. Containment != Corrective Action

Locked.

Holding stock stops impact now.

It does not prevent cause from recurring.

---

# 184. Containment Action

Concept:

```text
QualityContainmentAction
{
    NCRId
    ActionType
    Description
    Owner
    DueAt?
    State
    ExecutionRef?
}
```

---

# 185. Stop Production Request

Quality can request production hold through Manufacturing.

Manufacturing owns execution state.

---

# 186. Stop Shipment Request

Quality can request delivery/stock restriction through Inventory/Sales orchestration.

---

# 187. Supplier Block Recommendation

Quality can recommend supplier/material restriction.

Purchasing owns commercial sourcing controls.

---

# 188. Material Review Board — MRB

Advanced disposition approval pattern.

---

# 189. MRB Mission

Bring authorized functions together for high-impact nonconformance:

```text
Quality
Engineering
Manufacturing
Purchasing
Customer representative
Finance if cost material
```

as configured.

---

# 190. MRB Is Optional

Small companies can use:

```text
Quality Manager approves disposition
```

without formal board workflow.

---

# 191. MRB Review

Potential decisions:

```text
Use As Is
Rework
Repair
Return Supplier
Scrap
Regrade
Request Customer Concession
```

---

# 192. MRB Approval

Use Platform Approvals.

No custom approval engine.

---

# 193. Approval Snapshot

Disposition approval records exact:

```text
NCR version
affected qty
decision
conditions
```

---

# 194. Concession Approval

May require external customer approval reference.

---

# 195. Customer Concession Evidence

Store:

```text
customer response/ref
approved quantity
conditions
expiry
```

through Files/reference.

---

# 196. Planned Deviation

A temporary pre-approved departure from normal requirement.

---

# 197. Deviation Use Cases

Examples:

```text
Temporary alternate material
Temporary process parameter
Temporary inspection method
Temporary packaging method
```

---

# 198. Deviation Concept

```text
QualityDeviation
{
    Id
    Scope
    RequirementRef
    ProposedException
    Reason
    ValidFrom
    ValidTo
    QuantityLimit?
    ApprovalState
}
```

---

# 199. Deviation Must Be Bounded

By one or more:

```text
date
quantity
lot
production order
customer
supplier
plant
```

---

# 200. Deviation Expiry

Expired deviation cannot silently continue.

---

# 201. Deviation != Engineering Revision

Permanent changes belong owning master domain, e.g.:

```text
Manufacturing BOM/route revision
Quality Plan revision
```

---

# 202. Correction

Correction fixes the currently affected product/material/problem.

Examples:

```text
Rework quantity
Replace wrong label
Sort stock
Repair unit
```

---

# 203. Corrective Action

Corrective Action removes/reduces root cause to prevent recurrence.

---

# 204. Preventive Action

Addresses potential/systemic risk before occurrence or broader recurrence.

---

# 205. CAPA

CAPA is structured improvement workflow.

---

# 206. CAPA Mission

> **Move from problem containment to verified systemic improvement.**

---

# 207. CAPA Flow

```text
Issue / NCR
→ Containment
→ Investigation
→ Root Cause
→ Action Plan
→ Implement
→ Effectiveness Check
→ Close
```

---

# 208. CAPA Concept

```text
Capa
{
    Id
    Number
    LegalEntityId
    SourceType
    SourceRef
    Severity
    State
    Owner
    OpenedAt
    DueAt?
    ClosedAt?
}
```

---

# 209. CAPA States

Recommended:

```text
Draft
Approved
Investigation
ActionPlanning
Implementation
EffectivenessPending
Effective
Ineffective
Closed
Cancelled
```

---

# 210. CAPA != NCR

One NCR may not require CAPA.

Multiple NCRs may feed one systemic CAPA.

---

# 211. CAPA Trigger Rules

Examples:

```text
Critical NCR
Repeated Major NCR
Customer safety complaint
Supplier repeated failure
Regulatory failure
Trend threshold
Management decision
```

---

# 212. CAPA Numbering

Platform Numbering.

---

# 213. Root Cause Analysis

Separate from defect symptom.

---

# 214. Symptom != Root Cause

Example:

```text
Defect:
Scratch

Root Cause:
Worn fixture alignment
```

---

# 215. Root Cause Methods

Foundation can support:

```text
5 Why
Cause Codes
Fishbone categories
Structured narrative
```

---

# 216. Fishbone Categories

Optional:

```text
Man
Machine
Method
Material
Measurement
Environment
```

customizable.

---

# 217. 5 Why

Store structured chain if used.

---

# 218. Root Cause Confidence

Optional:

```text
Suspected
Probable
Confirmed
```

---

# 219. Root Cause Evidence

Files/results/data links.

---

# 220. CAPA Action

```text
CapaAction
{
    CapaId
    ActionType
    Description
    Owner
    DueAt
    State
    CompletionEvidence?
}
```

---

# 221. CAPA Action Types

```text
Correction
CorrectiveAction
PreventiveAction
VerificationAction
Training
ProcedureChange
SupplierAction
MaintenanceAction
EngineeringChange
InspectionChange
```

---

# 222. Training Action Boundary

HR/Learning capability may own training record.

Quality owns requirement/action link.

---

# 223. Maintenance Action Boundary

Service & Maintenance owns work order.

CAPA links execution result.

---

# 224. Engineering Change Boundary

Manufacturing/Catalog owner changes master.

Quality CAPA links approved change.

---

# 225. Inspection Change Boundary

Quality owns Plan revision.

---

# 226. Supplier Action Boundary

Supplier corrective action request remains Quality case; Purchasing/Directory own supplier identity/commercial control.

---

# 227. CAPA Due Dates

Overdue actions visible/escalated.

---

# 228. Action Completion

Action owner marks work completed with evidence.

---

# 229. Action Completed != Effective

Absolute.

---

# 230. Effectiveness Check

Tests whether CAPA reduced/eliminated recurrence.

---

# 231. Effectiveness Check Concept

```text
CapaEffectivenessCheck
{
    CapaId
    PlannedAt
    PerformedAt?
    EvaluationCriteria
    Result
    EvidenceRef?
    PerformedBy?
}
```

---

# 232. Effectiveness Result

```text
Effective
PartiallyEffective
Ineffective
Inconclusive
```

---

# 233. Ineffective CAPA

Can:

```text
reopen investigation
create new actions
extend monitoring
escalate
```

---

# 234. CAPA Close Gate

Cannot close only because tasks are marked done.

Require configured effectiveness evidence.

---

# 235. CAPA Effectiveness Examples

```text
Defect rate below 0.5% for 3 batches
No recurrence for 60 days
Process capability Cpk >= threshold
Supplier passes next 5 receipts
```

---

# 236. CAPA Recurrence Window

Configurable.

---

# 237. CAPA Audit

Critical history:

```text
who approved
root cause changes
actions
due date changes
effectiveness
close/reopen
```

---

# 238. CAPA E-Signature

Optional for regulated processes.

---

# 239. CAPA Cost

Can aggregate:

```text
rework
scrap
labor
external service
inspection
downtime
```

from canonical sources.

---

# 240. Quality Cost Fact

Quality analytics can normalize source-linked cost facts.

---

# 241. Quality Cost Categories

Potential:

```text
Prevention
Appraisal
InternalFailure
ExternalFailure
```

---

# 242. Internal Failure Cost

Examples:

```text
Scrap
Rework
Sort
Retest
Downtime due quality
```

---

# 243. External Failure Cost

Examples:

```text
Customer return
Warranty
Field correction
Replacement
Freight due defect
```

through source integrations.

---

# 244. Quality Cost != Accounting Ledger

Locked.

Quality analytics references source facts.

---

# 245. Cost Attribution

Every cost line should trace to:

```text
Inventory
Manufacturing
Expenses
Service
Purchasing
Payments
Ledger read model where appropriate
```

not duplicated manual accounting.

---

# 246. Cost Of Poor Quality

Useful KPI:

```text
Internal Failure + External Failure
```

with explicitly documented basis.

---

# 247. Prevention Cost

Optional advanced.

Examples:

```text
training
process improvement
preventive quality activities
```

---

# 248. Appraisal Cost

Examples:

```text
inspection
testing
external lab
```

---

# 249. Quality Economics

Owner view should show:

```text
Scrap/Rework cost
Supplier-related quality cost
Customer-related quality cost
Trend
```

without calling it accounting profit.

---

# 250. NCR Close Gate

Before close:

```text
affected quantity dispositioned
containment resolved
required actions complete
required CAPA linked/handled
inventory/manufacturing execution reconciled
```

according to policy.

# 251. Supplier Quality

Quality evaluates supplier quality without becoming supplier identity/commercial owner.

---

# 252. Supplier Identity

Directory owns canonical Supplier Party/role.

---

# 253. Supplier Commercial Status

Purchasing owns sourcing/commercial use.

Quality provides:

```text
quality status
score
restrictions/recommendations
```

through contract.

---

# 254. Supplier Quality Profile

```text
SupplierQualityProfile
{
    SupplierPartyId
    LegalEntityId
    QualityStatus
    RiskLevel
    DefaultSamplingProfile?
    LastReviewAt?
}
```

---

# 255. Supplier Quality Status

Potential:

```text
Qualified
Conditional
UnderReview
SuspendedForQuality
NotQualified
```

Purchasing decides whether that blocks sourcing according to policy.

---

# 256. Quality Status != Supplier Block

Locked.

Quality emits recommendation/eligibility context.

Purchasing owns commercial block execution.

---

# 257. Supplier-Specific Plan

Example:

```text
Product X
Supplier A → sample 5%
Supplier B → sample 20%
Supplier C → 100% inspection
```

---

# 258. Supplier Quality Performance

Metrics may include:

```text
Received Qty
Inspected Qty
Accepted Qty
Rejected Qty
Supplier NCR Count
Critical Defects
Defect PPM
Return Rate
Corrective Action Timeliness
```

---

# 259. Supplier Pass Rate

Definition documented.

Example:

```text
Accepted inspected lots / inspected lots
```

or quantity-based variant.

Never mix silently.

---

# 260. Supplier Defect PPM

If used:

```text
defective units / received units × 1,000,000
```

definition must be explicit.

---

# 261. Supplier Score

Can combine:

```text
Quality
Delivery
Commercial
```

only in a cross-functional supplier scorecard.

`27` owns Quality component only.

---

# 262. Supplier NCR

Formal NCR where origin = Supplier.

---

# 263. Supplier Corrective Action Request — SCAR

A Supplier NCR can create:

```text
SCAR
```

requesting supplier:

```text
containment
root cause
corrective action
due date
evidence
```

---

# 264. SCAR Concept

```text
SupplierCorrectiveActionRequest
{
    Id
    NCRId
    SupplierPartyId
    SentAt?
    DueAt
    State
    SupplierResponseRef?
    VerificationResult?
}
```

---

# 265. SCAR States

Potential:

```text
Draft
Sent
Acknowledged
ResponseReceived
UnderReview
Accepted
Rejected
Closed
Overdue
```

---

# 266. External Supplier Access

Supplier portal may later allow direct response.

Day 1 can use:

```text
email/export/upload
```

without supplier login.

---

# 267. Supplier Response

Store structured:

```text
Containment
Root Cause
Corrective Action
Implementation Date
Evidence
```

---

# 268. Supplier Response Acceptance

Quality Manager decides:

```text
Accept
Request Revision
Reject
```

---

# 269. Supplier CAPA Effectiveness

Can verify through:

```text
next N receipts
defect recurrence
audit
```

---

# 270. Dynamic Supplier Sampling

Advanced:

```text
good history → reduced
failure trend → tightened
critical event → 100%
```

must be explainable and versioned.

---

# 271. Supplier Quality Review

Periodic review can update:

```text
risk
sampling
qualification recommendation
```

---

# 272. Supplier Quality Evidence

Examples:

```text
Certificates
Audit report
SCAR
Inspection trends
Approved process evidence
```

Files/reference.

---

# 273. Supplier Return

Quality disposition can request:

```text
ReturnToSupplier
```

Purchasing owns commercial return/credit orchestration.

Inventory owns physical movement.

---

# 274. Supplier Replacement

Purchasing owns replacement commitment.

Quality can link outcome.

---

# 275. Incoming Quality

Canonical flow:

```text
Purchase Receipt
→ Resolve Quality Plan
→ Hold if required
→ Inspection
→ Sample
→ Results
→ Evaluation
→ Disposition
→ Inventory Release / Return / Other action
```

---

# 276. Receiving Without Quality Requirement

Normal receipt follows Inventory/Purchasing.

No Quality UI blocker.

---

# 277. Receiving With Quality Required

Receiver sees inline:

```text
Quality Inspection Required
[Inspect]
```

---

# 278. Auto-Hold On Receipt

Configurable by plan:

```text
Hold full receipt
Hold sampled quantity only
Allow restricted staging
No hold; inspection informational
```

---

# 279. Default Safety

For mandatory acceptance inspection:

```text
not available for ordinary use
```

until decision.

---

# 280. Partial Receipt Inspection

Inspection population uses actual received quantity.

---

# 281. Multiple Receipts

Each receipt can create independent Inspection according to frequency policy.

---

# 282. Supplier + Product Criteria

Plan can specialize:

```text
Product X from Supplier A
```

without duplicating Product or Supplier data.

---

# 283. Incoming Failure

Possible actions:

```text
Hold
Supplier NCR
Sort
Return
Rework
UseAsIs
Reinspect
```

---

# 284. Receiving Release

Quality authorizes.

Inventory makes stock usable.

---

# 285. Receiving Pass Does Not Post Invoice

No AP effect.

---

# 286. Receipt Quantity != Accepted Quantity

Example:

```text
Received 1000
Accepted 950
Rejected 50
```

must remain separate.

---

# 287. Purchase Order Received Quantity

Purchasing/Inventory rules remain canonical.

Quality does not rewrite PO received quantity silently.

---

# 288. Accepted Quantity Projection

Can be shown for Quality analysis.

---

# 289. Inventory Quality

Quality can inspect stock already in inventory.

---

# 290. Inventory Inspection Triggers

Potential:

```text
Scheduled reinspection
Shelf-life milestone
Temperature excursion signal
Ad-hoc concern
Customer requirement
Lot aging
Quality trend
```

---

# 291. Periodic Reinspection

Example:

```text
Chemical lot
→ reinspect every 90 days
```

---

# 292. Reinspection Due

Quality scheduling owns due task.

Inventory owns lot expiry/availability.

---

# 293. Expired Lot

Inventory shelf-life policy.

Quality reinspection cannot silently extend legal expiry unless approved owning policy allows.

---

# 294. Inventory Hold

Formal Quality restriction linked to stock subject.

---

# 295. Quarantine Location

Inventory-owned location.

Quality may request move if company process uses physical quarantine.

---

# 296. Hold Without Move

Supported.

Material status alone may block use.

---

# 297. Inventory Quality Release

Can release:

```text
full lot
partial quantity
specific serials
```

subject to Inventory capabilities.

---

# 298. Inventory Regrade

If disposition changes grade/product classification, exact Catalog/Inventory transformation must be explicit.

No simple text status update.

---

# 299. Manufacturing Quality

Integration with `26 Manufacturing`.

---

# 300. Manufacturing Quality Trigger Points

Potential:

```text
Before Operation
During Operation
Operation Completion
Production Output
Batch Completion
Final Release
```

---

# 301. Operation Quality Gate

Manufacturing routing can reference:

```text
QualityPlan / QualityRequirementRef
```

---

# 302. Manufacturing Owns Operation

Quality does not duplicate Routing/Operation master.

---

# 303. Inline Inspection

Shop-floor operator can complete allowed Quality checks in same execution screen.

---

# 304. Inspector-Only Check

Some characteristics require independent Quality Inspector.

---

# 305. Production Continue Policy

After gate:

```text
Pass → Continue
Fail → Block / Hold / ContinueWithApproval
```

plan-specific.

---

# 306. Production Gate != Production Status

Manufacturing consumes Quality gate result and owns production state.

---

# 307. In-Process Measurement

Examples:

```text
Temperature
Torque
Pressure
Thickness
Weld measurement
pH
```

---

# 308. Process Parameter vs Product Characteristic

Keep typed if useful:

```text
ProcessParameter
ProductCharacteristic
```

for analytics.

---

# 309. Production Output Inspection

Final output can require formal acceptance before Inventory status becomes usable.

---

# 310. Produced != Quality Released

Example:

```text
Produced 1000
Quality Released 950
Rework 30
Scrap 20
```

---

# 311. Production Partial Quality

Each partial output lot can be inspected independently.

---

# 312. Production Lot Sampling

Sampling population can be:

```text
batch
lot
partial completion
shift output
```

---

# 313. Quality-Driven Rework

Disposition:

```text
Rework
```

requests Manufacturing rework process/order.

Manufacturing owns execution.

---

# 314. Rework Reinspection

After Manufacturing completes rework:

```text
new Inspection
```

not mutation of failed result.

---

# 315. Quality-Driven Scrap

Quality authorizes.

Manufacturing/Inventory perform physical production/stock effect.

---

# 316. Manufacturing Hold

Quality can request:

```text
Hold Production Order
Hold Operation
Hold Output Lot
```

through Manufacturing contract.

---

# 317. Manufacturing Stop Escalation

Critical defect can trigger:

```text
stop current operation
hold related output
notify supervisor
create NCR
```

according to rule.

---

# 318. Maintenance Link

If root cause suspected machine:

```text
NCR/CAPA
→ Maintenance Request
```

Service/Maintenance owns work.

---

# 319. Machine Return-To-Service

Quality does not mark machine operational.

Service/Maintenance does.

Quality may require verification inspection afterward.

---

# 320. Outgoing Quality

Optional pre-shipment inspection.

---

# 321. Outgoing Trigger

Potential:

```text
Customer
Product
Country
Delivery type
Lot
Contract/customer specification
```

---

# 322. Outgoing Checks

Examples:

```text
Correct product
Correct quantity
Packaging
Label
Seal
Appearance
Documents
COA attached
```

---

# 323. Shipment Block

Failed required outgoing inspection requests:

```text
DoNotShip
```

Inventory/Sales execution owner.

---

# 324. Shipment Release

Quality authorizes release.

---

# 325. Customer-Specific Specification

Plan can apply tighter requirement for a specific customer.

---

# 326. Customer Spec Version

Historical shipment inspection retains exact customer-specific limits.

---

# 327. Customer Return Quality

Return commercial/logistics transaction remains external owner.

Quality can inspect returned material.

---

# 328. Return Reasons

Not every return is quality-related.

Examples:

```text
Wrong item ordered
Customer changed mind
Transport damage
Quality defect
```

---

# 329. Quality Return Trigger

Only quality-relevant returns create formal Quality case/inspection automatically according to policy.

---

# 330. Customer Complaint Intake

Complaint may originate in:

```text
CRM
Helpdesk
Sales
Service
```

Quality references source.

---

# 331. Complaint != NCR

Complaint is customer-reported concern.

Formal Quality investigation determines:

```text
Confirmed quality issue?
NCR?
No defect found?
Misuse?
Transport issue?
```

---

# 332. Customer Quality Case

Can link:

```text
Customer
Product
Lot/Serial
Invoice/Delivery
Complaint source
Inspection
NCR
CAPA
```

without duplicating Sales/CRM record.

---

# 333. Customer Notification

Quality can request/send approved communication through Platform/CRM/Helpdesk.

---

# 334. Customer Containment

High-risk issue may require:

```text
Stop ship
Notify affected customers
Trace lots
Recall evaluation
```

---

# 335. Recall Foundation

Architecture should support trace-based affected population identification.

Full regulated Recall Management can be advanced/evidence-based.

---

# 336. Recall Candidate

Quality issue can identify:

```text
affected lots/serials
customers/deliveries
inventory remaining
```

through Inventory/Sales trace.

---

# 337. Recall Decision

High-impact approval workflow.

---

# 338. Product Recall Execution

Cross-module orchestration.

Quality owns quality case/decision, not every logistics/payment action.

---

# 339. Certificate Of Analysis — COA

Quality-owned generated evidence document.

---

# 340. COA Mission

> **Certify the approved inspection results/specifications for a particular lot/batch/product context.**

---

# 341. COA != Inspection Result

COA is a controlled customer/regulatory-facing document derived from approved results.

---

# 342. COA Concept

```text
CertificateOfAnalysis
{
    Id
    Number
    LegalEntityId
    ProductRef
    LotRef?
    BatchRef?
    CustomerRef?
    InspectionRefs[]
    TemplateVersionRef
    State
    IssuedAt?
    IssuedBy?
}
```

---

# 343. COA Contents

Potential:

```text
Product
Batch/Lot
Manufacture date
Expiry if applicable
Characteristic
Specification
Result
UOM
Pass/Conformance statement
Approver/signature
```

---

# 344. COA Template

Versioned.

---

# 345. COA Generation Gate

Only approved/final eligible results.

---

# 346. COA State

```text
Draft
Generated
Approved
Issued
Superseded
Revoked
```

---

# 347. COA Correction

New version/superseding document.

Never silently replace issued certificate.

---

# 348. Certificate Of Conformance — CoC

Foundation can support a simpler conformance certificate:

```text
conforms to specified requirements
```

without publishing every measurement.

---

# 349. COA / CoC Delivery

Can be linked to:

```text
Shipment
Customer Portal
Invoice/Delivery docs
Email
```

through integration.

---

# 350. COA Security

Customer receives only allowed characteristics/results.

Internal notes/root cause/cost are never exposed.

# 351. SPC Foundation

Statistical Process Control is architecture-ready.

---

# 352. SPC Mission

> **Detect process instability and drift before specification failures become widespread.**

---

# 353. SPC Is Not Pass/Fail Inspection

A process can be:

```text
within specification
but statistically unstable
```

or:

```text
statistically stable
but centered outside customer specification
```

---

# 354. Control Limits

Derived/statistical:

```text
UCL
Center Line
LCL
```

not the same as Specification Limits.

---

# 355. Specification Limits

Engineering/customer acceptance:

```text
USL
Target
LSL
```

---

# 356. Measurement Series

Inspection results must preserve enough sequence/context for SPC:

```text
MeasuredAt
Product
Process/Operation
WorkCenter
Machine/Resource
Shift? optional
Lot/Batch
Characteristic
```

---

# 357. Control Chart Types — Future Foundation

Potential:

```text
Xbar-R
Xbar-S
Individuals-MR
p
np
c
u
```

Build only when user/customer evidence requires.

---

# 358. Control Chart Definition

Future:

```text
SpcChartDefinition
{
    Characteristic
    ProcessContext
    ChartType
    SubgroupSize
    BaselineWindow
    RuleSet
}
```

---

# 359. SPC Rule Violations

Potential:

```text
Point outside control limit
Run on one side
Trend
Too many near limit
Other configured Western Electric/Nelson-style rule
```

No copyrighted rule table should be hardcoded from proprietary source without review.

---

# 360. SPC Signal

A signal can create:

```text
Quality Issue
Notification
Additional inspection
Process hold recommendation
```

according to policy.

---

# 361. SPC Signal != NCR

Out-of-control does not automatically prove specification nonconformance.

---

# 362. Process Capability

Advanced analytics can calculate:

```text
Cp
Cpk
Pp
Ppk
```

when statistical assumptions/data sufficiency are satisfied.

---

# 363. Capability Warning

Never show capability index without:

```text
sample count
spec limits
time range
calculation basis
```

---

# 364. Measurement Drift

Quality dashboard can flag approaching spec/control boundary before failure.

---

# 365. Quality Analytics Mission

Turn inspection/NCR/CAPA history into actionable quality improvement.

---

# 366. Core Inspection KPIs

Potential:

```text
Pending Inspections
Overdue Inspections
Pass Rate
Fail Rate
Reinspection Rate
Average Inspection Time
```

---

# 367. Core Nonconformance KPIs

```text
Open NCRs
Critical NCRs
NCR Aging
Repeat NCRs
Disposition Aging
```

---

# 368. CAPA KPIs

```text
Open CAPA
Overdue Actions
Effectiveness Pending
Ineffective CAPA
Average Closure Time
```

---

# 369. Manufacturing Quality KPIs

Potential:

```text
First Pass Yield
Scrap Rate
Rework Rate
Defects per Unit
Quality Holds
Production Release Delay
```

---

# 370. Incoming Quality KPIs

```text
Supplier Lot Pass Rate
Supplier Reject Rate
Defect PPM
Incoming Hold Aging
SCAR Aging
```

---

# 371. Customer Quality KPIs

Potential:

```text
Quality Complaints
Confirmed Customer NCR
Return due to quality
External failure cost
Repeat complaint
```

---

# 372. First Pass Yield

Example definition:

```text
Qty passed without rework
/
Qty entering quality gate
```

Exact denominator is configured/documented.

---

# 373. First Pass Yield != Final Yield

Final yield may include reworked units.

---

# 374. Scrap Rate

Must specify denominator:

```text
input qty
produced qty
material value
```

Do not publish ambiguous KPI.

---

# 375. Defect Rate

Can be:

```text
defects / units inspected
```

not same as defective-unit rate.

---

# 376. Defects Per Unit

Useful where one unit may contain multiple defects.

---

# 377. Pareto Analysis

Rank by:

```text
Defect Type
Root Cause
Supplier
Product
Process
Work Center
```

---

# 378. Pareto Drilldown

Every bar drills to exact NCR/Inspection source.

---

# 379. Quality Trend

Show time series:

```text
Pass rate
Defect count
PPM
Scrap
Rework
```

---

# 380. Quality Heatmap

Potential:

```text
Product × Defect
Supplier × Defect
Operation × Defect
```

read model only.

---

# 381. Cost Of Poor Quality Dashboard

Potential:

```text
Scrap Cost
Rework Cost
Supplier Failure Cost
Customer Failure Cost
Inspection/Containment Cost
```

basis labeled.

---

# 382. Quality Cost Drilldown

Every amount links to canonical source.

---

# 383. No Quality P&L

Accounting profitability remains Finance/Accounting.

---

# 384. Quality Scorecards

Possible:

```text
Supplier
Product
Plant
Work Center
Production Line
```

---

# 385. Quality KPI Definition Versioning

For materially changing KPI definition, store/report definition version/effective date.

---

# 386. Management Home

Suggested:

```text
Pending Inspections
Lots On Hold
Failed Today
Critical NCR
CAPA Overdue
Supplier Risk
FPY
Scrap Cost
```

---

# 387. Inspector Home

Suggested:

```text
My Pending
Due Today
Overdue
Waiting Samples
Reinspection
```

---

# 388. Quality Manager Home

Suggested:

```text
Holds
Critical Issues
NCR Aging
Disposition Waiting
CAPA
Supplier Quality
Trends
```

---

# 389. Receiving User Experience

Receiver should not open Quality module manually.

Flow:

```text
Receive
→ Quality Required
→ Inspect / Send to Quality
```

---

# 390. Manufacturing Operator Experience

Inline quality step appears in Shop Floor only when required.

---

# 391. Warehouse Experience

Stock card/status can show:

```text
Quality Hold
Pending Inspection
Released
Restricted
```

from Quality/Inventory composition.

---

# 392. Supplier Buyer Experience

Purchasing can see:

```text
Supplier Quality Status
Open Supplier NCR
Recent Reject Rate
```

without owning Quality details.

---

# 393. Product Workspace Quality Tab

Could show:

```text
Active Quality Plans
Recent Inspections
Pass Rate
Defects
Open NCR
COA templates
```

Catalog remains owner.

---

# 394. Lot Quality Workspace

Composition:

```text
Lot Identity
Current Inventory Status
Inspections
Results
Holds
Disposition
NCR
Production genealogy
Shipments if authorized
```

---

# 395. Supplier Workspace Quality Tab

Composition:

```text
Quality status
Pass rate
Defect PPM
Open SCAR
Recent NCR
Sampling mode
```

---

# 396. Production Order Quality Tab

Composition:

```text
Required gates
Pending checks
Failed checks
Holds
NCR
Released qty
```

---

# 397. Quality Plan UX

Setup should read like business language:

```text
When receiving Product X from Supplier A
inspect 10%
check Diameter + Surface
if any critical failure → hold lot + create NCR
```

not a rule-engine programming screen.

---

# 398. Quality Plan Wizard

Simple flow:

```text
1. Where/when to inspect
2. What to inspect
3. How much to sample
4. What counts as pass
5. What to do on failure
6. Activate
```

---

# 399. Template Library

Can provide optional starters:

```text
Incoming Dimensional
Visual Packaging
Final Production
Food Batch Basic
Customer Shipment
```

templates are copied/versioned, not hidden system magic.

---

# 400. Fast Characteristic Entry

Grid supports:

```text
Name
Type
Target
Lower
Upper
UOM
Required
```

keyboard friendly.

---

# 401. Bulk Characteristic Import

CSV/XLSX import with preview/diff.

---

# 402. Plan Revision Diff

Show:

```text
Characteristic added/removed
Limit changed
Sampling changed
Failure action changed
Method changed
```

---

# 403. Revision Impact Preview

Before activating plan revision show:

```text
Affected products
Suppliers
Operations
Open/future inspections
```

---

# 404. Open Inspection Revision Rule

Existing started/created Inspection retains snapshot.

New inspections use new active version according to effective date.

---

# 405. Draft Inspection Refresh

Only explicit controlled action if organization chooses.

---

# 406. Inspection Queue

Primary filters:

```text
Due
Overdue
Context
Plant/Warehouse
Supplier
Product
Inspector
Severity/Risk
```

---

# 407. Inspection Priority

Can derive from:

```text
critical product
production blockage
shipment deadline
supplier risk
customer priority
```

without changing requirement truth.

---

# 408. Inspection SLA

Optional operational target:

```text
Inspect within 2h of receipt
```

Not same as Quality spec.

---

# 409. Inspection Assignment

Can assign:

```text
Inspector
Team
Lab
```

---

# 410. Quality Team

Operational team identity can be Quality-owned configuration referencing Users/Employees.

---

# 411. External Lab

Inspection characteristic may require outsourced lab test.

---

# 412. External Lab Procurement

Purchasing owns external service procurement if paid.

Quality owns test requirement/result/evidence.

---

# 413. Lab Result Import

Can import:

```text
CSV
PDF evidence
API later
```

with validation.

---

# 414. Lab Result Manual Entry

Requires:

```text
lab reference
result
method
report file
actor
```

where policy requires.

---

# 415. Environmental Conditions

Inspection/sample may record:

```text
Temperature
Humidity
Clean-room condition
```

if required by method.

---

# 416. Environmental Reading Source

Can be manual or IoT later.

Not required Day 1.

---

# 417. Quality Evidence Package

For an Inspection/NCR, compose:

```text
Results
Photos
Certificates
Methods
Signatures
Disposition
Audit
```

without duplicating source files.

---

# 418. Evidence Retention

Country/industry policy may specify retention period.

---

# 419. Legal Hold

Platform future/shared capability may override normal retention.

---

# 420. Quality Audit Foundation

Internal/supplier process audits are useful but not the first operational release priority.

Architecture may include a future `QualityAudit` bounded area inside `27` unless evidence justifies a separate QMS module.

---

# 421. Audit Types

Future:

```text
Internal Process Audit
Supplier Audit
Product Audit
Layered Process Audit
```

---

# 422. Audit Finding

Can create:

```text
Quality Issue
NCR
CAPA
```

---

# 423. Audit Checklist

Versioned and similar to Quality Plan characteristics, but audit semantics remain distinct from product inspection.

---

# 424. No Audit Engine Duplication Day 1

Use shared checklist/approval building blocks where appropriate.

---

# 425. Document Control Boundary

Full controlled-document management is not implemented inside Quality Day 1.

Quality references approved procedures/files/versions.

---

# 426. Procedure Approval

Use Platform Files/DMS capability when available.

---

# 427. Training Acknowledgement Boundary

HR/Learning future capability owns training completion.

Quality can require valid training/qualification.

---

# 428. Regulatory Requirement Context

Quality Plan/Characteristic can carry:

```text
RegulatoryRef
StandardRef
CustomerSpecRef
InternalSpecRef
```

as metadata/evidence.

---

# 429. Regulatory Reference Is Not Legal Advice

Country Pack/industry configuration supplies approved requirements.

---

# 430. Country Pack Quality

May add:

```text
mandatory certificate fields
retention rules
signature requirements
regulated disposition restrictions
```

without forking core.

---

# 431. Customer-Specific Quality Requirement

Can be activated only for customer/product combination.

---

# 432. Supplier COA Verification

Receiving Plan can require supplier COA and validate key metadata.

---

# 433. Supplier COA != TODIR COA

Supplier COA is incoming evidence.

TODIR COA is company-issued evidence from its own approved inspection truth.

---

# 434. Quality Alert Notifications

Examples:

```text
Critical fail
Lot hold
NCR assigned
Disposition overdue
CAPA overdue
SCAR overdue
Inspection overdue
```

through Platform Notifications.

---

# 435. Notification Failure

Must not change Quality state.

---

# 436. Escalation

Can escalate overdue/critical case to role/manager.

---

# 437. Escalation != Severity

Severity describes quality impact.

Escalation describes attention/workflow.

---

# 438. Quality Calendar

Inspection due dates can use business calendar if required.

---

# 439. Quality Shift Context

Optional production analytics can capture shift reference from Manufacturing/HR schedule.

Quality does not own shift master.

---

# 440. Repeat Defect Detection

System can flag recurrence by:

```text
Product + Defect
Supplier + Defect
Operation + Defect
Machine + Defect
Root Cause
```

within time window.

---

# 441. Repeat Defect != Same NCR

New occurrence remains distinct source case.

---

# 442. Similarity Suggestions

AI/search can suggest related prior NCR/CAPA.

Human decides relation.

---

# 443. AI Allowed

AI may:

```text
suggest inspection plan characteristics
summarize NCR history
suggest similar past causes/actions
classify free-text defect
extract values from certificate/photo
highlight quality trends
```

---

# 444. AI Must Not

AI cannot autonomously:

```text
release held stock
approve UseAsIs
close NCR
approve CAPA effectiveness
change specification
change sampling to reduced mode
issue regulatory certificate
```

without approved deterministic/human control.

---

# 445. OCR Measurement Suggestion

Photo/document extraction may suggest value.

Inspector confirms material results.

---

# 446. AI Confidence

Display:

```text
suggested
source
confidence/context
```

where useful.

---

# 447. No Direct AI Database Access

AI Tool Layer → permissions → Application Commands → Domain → Audit.

---

# 448. Search

Quality search supports:

```text
Inspection number
NCR number
CAPA number
Product
Lot/Serial
Supplier
Customer
Production Order
Purchase Receipt
Defect
Root Cause
```

---

# 449. Arabic/English

Quality names, instructions and controlled descriptions support Arabic/English localization while codes remain stable.

---

# 450. Measurement Locale

UI accepts locale formatting carefully but stores canonical decimal values/UOM.

# 451. Recommended Schema

```text
business_quality
```

---

# 452. QualityDbContext

One module-owned context:

```text
QualityDbContext
```

---

# 453. Core Tables — Planning

Potential:

```text
quality_plan
quality_plan_version
quality_plan_applicability
quality_trigger_rule
quality_characteristic
quality_characteristic_group
quality_specification_version
quality_plan_characteristic
inspection_method
sampling_plan
sampling_plan_version
quality_action_rule
```

---

# 454. Core Tables — Inspection

Potential:

```text
inspection
inspection_plan_snapshot
quality_sample
inspection_result
inspection_result_correction
inspection_instrument_use
inspection_review
inspection_evaluation
```

---

# 455. Core Tables — Holds / Disposition

Potential:

```text
quality_restriction
quality_disposition
quality_disposition_line
quality_deviation
quality_concession
```

---

# 456. Core Tables — Issues / NCR

Potential:

```text
quality_issue
quality_defect
nonconformance
nonconformance_defect
quality_containment_action
nonconformance_relation
```

---

# 457. Core Tables — CAPA

Potential:

```text
capa
capa_source_link
capa_root_cause
capa_action
capa_effectiveness_check
```

---

# 458. Core Tables — Supplier Quality

Potential:

```text
supplier_quality_profile
supplier_corrective_action_request
supplier_quality_review
```

---

# 459. Core Tables — Certificates

Potential:

```text
quality_certificate
quality_certificate_version
quality_certificate_template
quality_certificate_result_line
```

---

# 460. Core Tables — Read Models

Potential:

```text
inspection_queue_projection
lot_quality_projection
product_quality_projection
supplier_quality_projection
production_quality_projection
quality_kpi_projection
quality_cost_projection
quality_hold_projection
capa_overdue_projection
```

---

# 461. Future/Advanced Tables

Only when feature enabled:

```text
spc_chart_definition
spc_control_limit_version
spc_signal
quality_physical_sample_custody
quality_audit
quality_audit_finding
```

---

# 462. Tables Quality Must Not Own

Forbidden duplicate truth:

```text
product
supplier
customer
purchase_order
inventory_balance
inventory_movement
lot
serial
production_order
routing_operation
fixed_asset
maintenance_work_order
sales_order
customer_return
journal_entry
```

---

# 463. Typed External References

Use explicit reference type + ID/contracts.

No cross-module EF navigation.

---

# 464. Tenant Columns

All tenant-owned Quality tables:

```text
tenant_id
```

---

# 465. RLS

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 466. Runtime Role

No ownership/BYPASSRLS.

---

# 467. Legal Entity

Every operational Quality case/plan is scoped to Legal Entity where legally/financially relevant.

---

# 468. Shared Quality Plan Across Entities

Do not share one mutable operational record across legal entities.

Use templates/copied controlled versions if group standardization is needed.

---

# 469. IDs

UUIDv7.

---

# 470. Quantity

High-precision decimal + UOM.

---

# 471. Measurement Value

Exact decimal, never float/double.

---

# 472. Dates/Time

NodaTime semantics:

```text
LocalDate
Instant
Duration
DateTimeZone
```

according to meaning.

---

# 473. Result UOM Snapshot

Preserve exact UOM/version context used at inspection.

---

# 474. State History

High-impact transitions preserved through domain history/audit.

---

# 475. Row Version

Use optimistic concurrency for:

```text
Draft Plan
Inspection in progress
NCR
Disposition draft
CAPA
```

---

# 476. Finalized Result Immutability

Completed/approved results corrected semantically only.

---

# 477. Finalized Disposition Immutability

Changes require:

```text
reversal/superseding disposition
```

not overwrite.

---

# 478. Effective-Dated Master Data

Required where history matters:

```text
Plan
Specification
Sampling
Action Rule
COA Template
```

---

# 479. Unique Codes

Within Legal Entity or configured group scope:

```text
Plan Code
Characteristic Code
NCR Number
CAPA Number
Certificate Number
```

---

# 480. Inspection Result Indexes

Likely:

```text
inspection_id
characteristic_snapshot_id
sample_id
recorded_at
```

---

# 481. Inspection Queue Indexes

Likely:

```text
tenant/entity/state/due_at
assigned_to/state/due_at
context_type/state
subject_type/subject_ref
source_type/source_ref
```

---

# 482. NCR Indexes

```text
tenant/entity/state/severity
origin/state
subject_type/subject_ref
owner/state/due
```

---

# 483. CAPA Indexes

```text
state/due_at
owner/state
source_type/source_ref
```

---

# 484. Supplier Quality Indexes

```text
supplier/entity
quality_status
```

---

# 485. Lot Quality Indexes

Reference projection by:

```text
LotRef
ProductRef
State
```

---

# 486. Measurement Analytics Indexes

For SPC/trends:

```text
characteristic/product/process/time
```

only after measured workload analysis.

---

# 487. Partitioning

Do not partition Day 1.

Measure result volume first.

---

# 488. High-Volume Measurements

If IoT/high-frequency measurement becomes huge, do not dump unlimited telemetry into core InspectionResult tables.

Store actionable/quality records; use specialized telemetry capability if justified.

---

# 489. API Base

Recommended:

```text
/api/v1/quality
```

---

# 490. Plan APIs

```text
GET  /plans
POST /plans
GET  /plans/{id}
POST /plans/{id}/versions
GET  /plan-versions/{id}
POST /plan-versions/{id}/approve
POST /plan-versions/{id}/activate
POST /plan-versions/{id}/supersede
```

---

# 491. Characteristic APIs

```text
GET  /characteristics
POST /characteristics
GET  /characteristics/{id}
POST /characteristics/{id}/specifications
```

---

# 492. Sampling APIs

```text
GET  /sampling-plans
POST /sampling-plans
POST /sampling-plans/{id}/versions
POST /sampling/calculate
```

---

# 493. Trigger Resolution API

Internal/application contract:

```text
ResolveQualityRequirements(context)
```

returns:

```text
plans
mandatory checks
sampling
hold requirement
```

---

# 494. Inspection APIs

```text
GET  /inspections
POST /inspections
GET  /inspections/{id}
POST /inspections/{id}/start
POST /inspections/{id}/results
POST /inspections/{id}/complete
POST /inspections/{id}/review
POST /inspections/{id}/cancel
```

---

# 495. Ad-Hoc Inspection API

```text
POST /inspections/ad-hoc
```

---

# 496. Reinspection API

```text
POST /inspections/{id}/reinspect
```

---

# 497. Result Correction API

```text
POST /inspection-results/{id}/correct
```

---

# 498. Hold APIs

```text
POST /restrictions
POST /restrictions/{id}/release-request
GET  /restrictions
```

Inventory execution via contract.

---

# 499. Disposition APIs

```text
POST /inspections/{id}/dispositions
POST /nonconformances/{id}/dispositions
POST /dispositions/{id}/submit
POST /dispositions/{id}/approve
POST /dispositions/{id}/execute
```

---

# 500. Quality Issue APIs

```text
GET  /issues
POST /issues
GET  /issues/{id}
POST /issues/{id}/triage
POST /issues/{id}/confirm-ncr
POST /issues/{id}/resolve
```

---

# 501. NCR APIs

```text
GET  /nonconformances
POST /nonconformances
GET  /nonconformances/{id}
POST /nonconformances/{id}/confirm
POST /nonconformances/{id}/containments
POST /nonconformances/{id}/close
POST /nonconformances/{id}/reopen
```

---

# 502. CAPA APIs

```text
GET  /capas
POST /capas
GET  /capas/{id}
POST /capas/{id}/root-causes
POST /capas/{id}/actions
POST /capa-actions/{id}/complete
POST /capas/{id}/effectiveness-checks
POST /capas/{id}/close
```

---

# 503. Supplier Quality APIs

```text
GET  /suppliers/{supplierId}/quality
GET  /supplier-quality
POST /supplier-quality/{supplierId}/reviews
POST /nonconformances/{id}/scar
```

---

# 504. Certificate APIs

```text
GET  /certificates
POST /certificates/generate
GET  /certificates/{id}
POST /certificates/{id}/approve
POST /certificates/{id}/issue
POST /certificates/{id}/supersede
```

---

# 505. Analytics APIs

```text
GET /dashboard
GET /analytics/defects
GET /analytics/suppliers
GET /analytics/products
GET /analytics/cost-of-poor-quality
```

---

# 506. Commands — Planning

Representative:

```text
CreateQualityPlan
CreateQualityPlanVersion
AddPlanCharacteristic
SetPlanApplicability
SetSamplingPlan
AddQualityActionRule
ApproveQualityPlanVersion
ActivateQualityPlanVersion
SupersedeQualityPlanVersion
```

---

# 507. Commands — Inspection

```text
CreateInspection
CreateAdHocInspection
StartInspection
CollectSample
RecordInspectionResult
CorrectInspectionResult
CompleteInspection
ReviewInspection
CreateReinspection
CancelInspection
```

---

# 508. Commands — Restriction / Disposition

```text
RequestQualityHold
RequestQualityRelease
CreateDisposition
AddDispositionLine
SubmitDisposition
ApproveDisposition
ExecuteDisposition
CreateDeviation
ApproveDeviation
```

---

# 509. Commands — NCR

```text
ReportQualityIssue
TriageQualityIssue
CreateNonconformance
ConfirmNonconformance
AddDefect
ExpandNonconformanceScope
CreateContainmentAction
CloseNonconformance
ReopenNonconformance
```

---

# 510. Commands — CAPA

```text
CreateCapa
RecordRootCause
CreateCapaAction
CompleteCapaAction
ScheduleEffectivenessCheck
RecordEffectivenessResult
CloseCapa
ReopenCapa
```

---

# 511. Commands — Supplier

```text
CreateScar
SendScar
RecordSupplierResponse
AcceptSupplierResponse
RejectSupplierResponse
ReviewSupplierQuality
```

---

# 512. Commands — Certificates

```text
GenerateCoa
ApproveQualityCertificate
IssueQualityCertificate
SupersedeQualityCertificate
RevokeQualityCertificate
```

---

# 513. No Generic `SetPass=true`

Use result/evaluation command.

---

# 514. No Generic `SetInventoryHold=true`

Quality requests restriction; Inventory owns stock status.

---

# 515. No Generic `DeleteFailedResult`

Forbidden.

---

# 516. No Generic `SetNcrClosed=true`

Use close gate.

---

# 517. No Generic `SetCapaEffective=true`

Require effectiveness-check evidence.

---

# 518. No Generic `MoveToQuarantine`

Inventory command through contract.

---

# 519. Domain Contracts

Recommended:

```text
Todir.Quality.Contracts
```

---

# 520. Backend Namespaces

```text
Todir.Modules.Quality.Domain
Todir.Modules.Quality.Application
Todir.Modules.Quality.Infrastructure
Todir.Quality.Contracts
```

---

# 521. Internal Areas

Potential:

```text
Planning/
Characteristics/
Sampling/
Inspections/
Samples/
Results/
Restrictions/
Disposition/
Issues/
Nonconformance/
Capa/
SupplierQuality/
Certificates/
Spc/
Reporting/
```

---

# 522. Inventory Contracts Consumed

Need concepts such as:

```text
GetStockSubject
GetLotSerialContext
RequestQualityRestriction
ReleaseQualityRestriction
MoveToQuarantine if configured
ExecuteScrapDisposition
ExecuteSupplierReturnMovement
GetTraceability
```

exact names implementation-approved.

---

# 523. Manufacturing Contracts Consumed

Need:

```text
GetProductionContext
GetOperationContext
ApplyQualityGateResult
RequestProductionHold
RequestRework
GetOutputContext
```

---

# 524. Purchasing Contracts Consumed

Need:

```text
GetSupplierReceiptContext
RequestSupplierReturn
GetSupplierContext
ApplyQualityEligibility/Restriction
```

---

# 525. Sales / CRM Contracts Consumed

Need:

```text
GetDeliveryContext
GetCustomerReturnContext
GetComplaintContext
RequestShipmentHold
```

only where actual contracts exist.

---

# 526. Service Contracts Consumed

Need:

```text
GetInstrumentCalibrationStatus
CreateMaintenanceRequest
GetFormalAssetInspectionContext
```

---

# 527. HR Contracts Consumed

Need optional:

```text
GetInspectorQualification
GetEmployeeRef
```

---

# 528. Ledger Contract

Quality normally does not post.

Quality-cost analytics read source costs; semantic accounting facts only for rare approved cases through owning financial module.

---

# 529. Events Emitted

Potential:

```text
QualityInspectionCreatedV1
QualityInspectionCompletedV1
QualityInspectionFailedV1
QualityRestrictionRequestedV1
QualityDispositionApprovedV1
NonconformanceConfirmedV1
CapaOpenedV1
CapaClosedV1
QualityCertificateIssuedV1
```

---

# 530. Events Consumed

Potential:

```text
PurchaseReceiptConfirmedV1
ProductionOperationReadyV1
ProductionOutputReportedV1
InventoryLotStatusChangedV1
CustomerReturnReceivedV1
MaintenanceCalibrationCompletedV1
```

only if event contract is actually approved.

---

# 531. Sync vs Async Rule

If Quality result is a hard gate for stock/production release:

```text
use synchronous application contract / transaction coordination as required
```

Do not rely on eventual event only.

---

# 532. Async Rule

Use async events for:

```text
analytics
notifications
trend projections
search projections
```

---

# 533. Outbox / Inbox

Platform Kernel.

---

# 534. Idempotency

Mandatory for:

```text
Inspection creation from trigger
Result submission
Hold request
Disposition execution
NCR auto-creation
CAPA action completion
Certificate generation
```

---

# 535. Trigger Idempotency

Same Receipt/Operation event cannot create duplicate mandatory Inspection unexpectedly.

---

# 536. Result Idempotency

Mobile/API retry cannot duplicate one measurement.

---

# 537. Disposition Execution Idempotency

Retry cannot scrap/return/rework quantity twice.

---

# 538. Concurrency — Inspection

Two inspectors cannot finalize conflicting results without version/conflict handling.

---

# 539. Concurrency — Disposition

Allocated disposition quantities cannot exceed affected quantity.

---

# 540. Concurrency — Hold Release

Only one effective release/action per restriction version.

---

# 541. Concurrency — CAPA

Action/effectiveness workflow uses row/version checks.

---

# 542. Permissions

Initial vocabulary:

```text
quality.plan.read
quality.plan.create
quality.plan.edit
quality.plan.approve
quality.plan.activate

quality.inspection.read
quality.inspection.create
quality.inspection.execute
quality.inspection.review
quality.inspection.correct

quality.result.read
quality.result.sensitive.read

quality.hold.read
quality.hold.request
quality.hold.release

quality.disposition.read
quality.disposition.create
quality.disposition.approve
quality.disposition.execute

quality.issue.read
quality.issue.create
quality.issue.triage

quality.ncr.read
quality.ncr.create
quality.ncr.confirm
quality.ncr.close
quality.ncr.reopen

quality.capa.read
quality.capa.create
quality.capa.manage
quality.capa.approve
quality.capa.effectiveness

quality.supplier.read
quality.supplier.manage
quality.scar.manage

quality.certificate.read
quality.certificate.generate
quality.certificate.approve
quality.certificate.issue

quality.analytics.read
quality.cost.read
quality.audit.read
quality.settings.manage
quality.export
```

---

# 543. Scope Model

Combine permissions with:

```text
Plant
Warehouse
Branch
LegalEntity
QualityTeam
SupplierGroup
ProductGroup
AssignedInspection
AllAuthorized
```

---

# 544. Inspector Scope

Normally sees:

```text
assigned/pending inspections
required source context
spec limits
method
sample
```

not supplier price, margin or unrelated customer data.

---

# 545. Receiver Scope

Can see/execute simple incoming quality steps if authorized.

Cannot approve UseAsIs unless granted.

---

# 546. Operator Scope

Can perform inline manufacturing checks assigned to operation.

Cannot alter plan/specification.

---

# 547. Quality Engineer

Can manage plans/specifications/sampling according to approval policy.

---

# 548. Quality Manager

Can manage NCR/disposition/CAPA and critical release according to scope.

---

# 549. Supplier Quality Engineer

Supplier-related NCR/SCAR/read scope.

---

# 550. Separation Of Duties

Optional controls:

```text
Inspector != Disposition Approver
Plan Author != Plan Approver
NCR Owner != UseAsIs Approver
CAPA Action Owner != Effectiveness Verifier
```

---

# 551. Do Not Force SoD On Tiny Companies

Configurable.

---

# 552. Sensitive Quality Data

Potential:

```text
Customer proprietary specification
Supplier confidential certificate
Regulatory test results
Root-cause investigation
Complaint personal data
```

field/file permissions apply.

---

# 553. File Security

Short-lived signed access.

---

# 554. Customer-Facing Certificate Security

Only issued certificate snapshot is exposed externally.

---

# 555. Audit Critical Actions

Capture:

```text
Plan/specification activation
Sampling change
Result correction
Evaluation override
Hold/release
UseAsIs/Concession
Scrap/Return disposition
NCR confirmation/close/reopen
Root cause change
CAPA effectiveness/close
Supplier status change
COA issue/revoke
Large export
```

---

# 556. Audit vs Quality History

Quality history explains business quality case.

Audit explains actor/system/permission change.

Both required.

---

# 557. Security — IDOR

Server validates every SourceRef/SubjectRef against tenant/entity/user scope.

---

# 558. Security — Quantity Tampering

Disposition execution revalidates current affected quantity/source state.

---

# 559. Security — Hold Bypass

Inventory/Manufacturing must enforce quality restrictions server-side, not only hide buttons.

---

# 560. Security — Result Tampering

Completed signed/final result cannot be ordinary PUT-updated.

# 561. Security — Certificate Issue

Only authorized role can issue/revoke customer-facing certificate.

---

# 562. Security — Supplier File

Supplier evidence access scoped.

---

# 563. Security — Customer Complaint PII

Only quality-relevant minimal data exposed.

---

# 564. Performance Mission

Fast inspection execution + scalable historical analytics.

---

# 565. Inspector Queue Target

```text
p95 <= 100-250ms
```

for indexed normal queue.

---

# 566. Inspection Open Target

Core plan/results context:

```text
p95 <= 150-350ms
```

without loading all history/files.

---

# 567. Result Save Target

Interactive:

```text
p95 <= 100-250ms
```

excluding external file upload.

---

# 568. Lot Quality Summary Target

```text
p95 <= 150-350ms
```

from projection.

---

# 569. Supplier Quality Summary Target

```text
p95 <= 200-500ms
```

from projection.

---

# 570. NCR List Target

```text
p95 <= 150-350ms
```

paged/indexed.

---

# 571. CAPA List Target

```text
p95 <= 150-350ms
```

paged/indexed.

---

# 572. Measurement History

Keyset pagination for high-volume result history.

---

# 573. No N+1

Inspection queue cannot query each row separately for:

```text
Product
Lot
Supplier
Plan
Assigned inspector
Hold status
```

---

# 574. Trigger Resolution Performance

Quality requirement resolution must be indexed and deterministic.

Do not scan all plans per receipt/operation.

---

# 575. Applicability Index

Precompute/index searchable criteria by:

```text
Plan Type
Product/Category
Supplier
Plant
Operation
Legal Entity
Effective Date
```

as implementation requires.

---

# 576. Plan Snapshot

Create once per Inspection, avoid rebuilding full master graph on every result save.

---

# 577. Bulk Inspection Creation

Large receipt/output batches use set/batch processing.

---

# 578. Large Sampling

Do not create one database row for every uninspected unit.

Represent population/sample efficiently.

---

# 579. 100% Serial Inspection

When serialized quantity genuinely requires per-serial result, explicit rows are valid.

---

# 580. Large Lot Sampling

Store sample/results, not one row per entire lot unit.

---

# 581. Analytics Projections

Incremental/rebuildable.

---

# 582. SPC Analytics

Can run batch/background over selected measurement series.

---

# 583. No Redis Requirement Day 1

PostgreSQL-first.

---

# 584. No OpenSearch Requirement Day 1

PostgreSQL indexes/search first.

---

# 585. Background Jobs

Potential:

```text
Scheduled reinspection creation
Inspection overdue reminders
CAPA/SCAR overdue checks
Supplier-quality projection refresh
Quality KPI projection refresh
COA generation
SPC calculation later
Large export
Projection rebuild
Retention jobs
```

---

# 586. Reinspection Scheduler

Idempotent.

---

# 587. Overdue Job

Never changes inspection result; only status/projection/notification.

---

# 588. COA Generation

Background for large documents; issue only after successful generation/approval.

---

# 589. Observability Metrics

Potential:

```text
quality_inspection_created_total
quality_inspection_duration
quality_inspection_fail_rate
quality_hold_count
quality_ncr_open_count
quality_ncr_cycle_duration
quality_capa_overdue_count
quality_supplier_reject_rate
quality_result_save_duration
quality_plan_resolution_duration
quality_coa_generation_failure_total
```

No high-cardinality IDs as labels.

---

# 590. Tracing — Trigger

```text
Source Context
Resolve Applicability
Resolve Plan Version
Calculate Sampling
Create Inspection
Request Hold if required
Audit
Commit
```

---

# 591. Tracing — Result

```text
Load Snapshot
Validate Inspector/Instrument
Validate Value/UOM
Evaluate Characteristic
Persist Result
Run Action Rules
Update Inspection Projection
```

---

# 592. Tracing — Disposition

```text
Validate Affected Qty
Validate Approval
Execute Cross-Module Action
Record Execution Ref
Update Restriction
Audit
```

---

# 593. Tracing — CAPA

```text
Load Sources
Root Cause
Actions
Effectiveness
Close Gate
```

---

# 594. Logging

Never dump unnecessarily:

```text
full customer complaint data
confidential supplier certificate
full regulated test report
signature payload
```

---

# 595. Reports — Inspections

```text
Pending
Overdue
Completed
Pass/Fail
By Product
By Supplier
By Plant
By Inspector
By Plan
```

---

# 596. Reports — Defects

```text
Defects By Type
Defects By Severity
Defects By Product
Defects By Supplier
Defects By Process
Pareto
Trend
```

---

# 597. Reports — NCR

```text
Open NCR
Aging
By Origin
By Severity
Repeat NCR
Disposition Aging
```

---

# 598. Reports — CAPA

```text
Open CAPA
Overdue Actions
Effectiveness Pending
Effective/Ineffective
Cycle Time
```

---

# 599. Reports — Supplier Quality

```text
Lot Pass Rate
Reject Rate
PPM
Open SCAR
SCAR Aging
Critical Defects
Quality Status
```

---

# 600. Reports — Manufacturing Quality

```text
FPY
Rework Rate
Scrap Rate
Quality Hold Time
Defect By Operation
Defect By Work Center
```

---

# 601. Reports — Cost

```text
Cost Of Poor Quality
Scrap Cost
Rework Cost
External Failure Cost
Supplier Quality Cost
```

---

# 602. Reports — Certificates

```text
Issued COA
Pending COA
Revoked/Superseded
By Customer/Product/Lot
```

---

# 603. Reports — Instrument Trace

```text
Results By Instrument
Calibration-at-use status
Affected results by calibration issue
```

---

# 604. Reports — Sampling

```text
Sampling Mode
Sample Size
Acceptance/Reject
Reduced/Tightened later
```

---

# 605. Reports — Reinspection

```text
First Fail → Reinspection outcome
Rework effectiveness
```

---

# 606. KPI Drilldown

Every aggregate drills to exact source.

---

# 607. Export

CSV/XLSX/PDF as appropriate.

---

# 608. Large Export

Background secure file.

---

# 609. Migration Mission

Migrate controlled quality masters + open quality cases + required history without fabricating inspections/results.

---

# 610. Import — Characteristics

Fields:

```text
Code
Name
Type
UOM
Category
```

---

# 611. Import — Specifications

```text
Characteristic
Target
Lower
Upper
Allowed Values
Validity
```

---

# 612. Import — Quality Plans

```text
Plan
Revision
Applicability
Characteristics
Sampling
Actions
Validity
```

---

# 613. Import — Sampling Plans

Preserve mode/parameters/version.

---

# 614. Import — Open Inspections

Preserve:

```text
source
plan snapshot
sample
completed results
pending characteristics
hold state
```

where reliable.

---

# 615. Import — Open NCR

Preserve:

```text
source
subject
qty
defects
containment
disposition
owner
state
```

---

# 616. Import — Open CAPA

Preserve:

```text
root cause
actions
due dates
effectiveness state
```

---

# 617. Historical Inspection Migration

Options:

```text
Detailed
Summary
RecentOnly
None
```

---

# 618. Historical Result Integrity

Do not synthesize measurement detail from aggregate pass-rate data.

---

# 619. Migration Provenance

```text
SourceSystem
LegacyId
BatchId
ImportedAt
AsOfDate
```

---

# 620. Opening Holds

Must reconcile to Inventory current restricted quantities/lots/serials.

---

# 621. Opening Supplier Quality Status

Import with evidence/as-of date.

---

# 622. Opening COA History

Existing issued files can be imported as legacy certificates if provenance known.

Do not recreate historical measurements.

---

# 623. Go-Live Boundary

After date:

```text
Quality Plan triggers
Inspections
Holds
NCR/CAPA
Certificates
```

use TODIR normal semantics.

---

# 624. Small Company Setup Wizard

```text
1. Pick Products/Processes requiring inspection
2. Define simple checks
3. Choose sample percentage/fixed qty
4. Choose failure action
5. Test one receipt/production output
6. Go Live
```

---

# 625. Manufacturing Quality Setup

```text
1. Choose Production operation/output gates
2. Attach Quality Plan
3. Define inspector/operator responsibility
4. Define fail behavior
5. Test Shop Floor flow
```

---

# 626. Supplier Quality Setup

```text
1. Select risky/critical suppliers/products
2. Set incoming plan
3. Set sampling
4. Configure NCR/SCAR workflow
```

---

# 627. No Irrelevant Setup Blocker

No AQL? Use simple sample.

No CAPA? Simple NCR/disposition works.

No instrument trace? Hide it.

No SPC? Hide it.

No COA? No certificate setup.

---

# 628. Small Company Defaults

```text
Simple Quality Plans       ON
Pass/Fail + Measurement    ON
Percentage/Fixed Sampling  ON
Incoming + Production      ON
NCR                        Simple
CAPA                       Optional
MRB                        OFF
AQL                        OFF
SPC                        OFF
E-Signature                OFF
Instrument Trace           Optional
```

---

# 629. Enterprise Capabilities

Enable:

```text
AQL
Sampling tables
Critical/Major/Minor
Supplier-specific plans
MRB
CAPA
Electronic signatures
Instrument trace
COA
SPC
Dynamic sampling
Quality audits
```

---

# 630. Test — Plan Resolution

Correct Plan selected by Product/Supplier/context/date.

---

# 631. Test — Ambiguous Plan

Blocked when no deterministic priority/merge rule.

---

# 632. Test — Plan Revision

Old Inspection retains old limits.

---

# 633. Test — New Revision

New source event uses correct active version.

---

# 634. Test — Numeric Pass

Value within spec → Pass.

---

# 635. Test — Numeric Fail

Outside spec → Fail.

---

# 636. Test — UOM Conversion

Compatible UOM evaluates correctly.

---

# 637. Test — UOM Incompatible

Rejected.

---

# 638. Test — Categorical Result

Allowed/rejected mapping exact.

---

# 639. Test — Required Result

Cannot complete if missing.

---

# 640. Test — Optional Result

Can complete if omitted.

---

# 641. Test — Result Correction

Old value preserved.

---

# 642. Test — Instrument Calibration Valid

Result allowed.

---

# 643. Test — Instrument Calibration Expired

Plan policy block/warn exact.

---

# 644. Test — Instrument Impact Search

Affected historical results found for interval.

---

# 645. Test — Fixed Sample

Correct size.

---

# 646. Test — Percentage Sample

Correct rounding/minimum.

---

# 647. Test — Full Inspection

100% requirement.

---

# 648. Test — AQL

Correct sample/accept/reject based on configured table/version.

---

# 649. Test — Critical Defect

Critical rejection policy enforced.

---

# 650. Test — Frequency

Every-receipt/periodic/on-demand semantics exact.

---

# 651. Test — Auto Inspection Creation

Source retry does not duplicate Inspection.

---

# 652. Test — Ad-Hoc Inspection

Works without parent transaction.

---

# 653. Test — Reinspection

Original fail remains unchanged.

---

# 654. Test — Quality Hold

Inventory becomes unavailable exactly once through Inventory owner.

---

# 655. Test — Partial Hold

Only selected quantity restricted.

---

# 656. Test — Hold Release

Authorized decision releases correct scope.

---

# 657. Test — Pass Does Not Auto-Move

Inventory action still explicit/owned.

---

# 658. Test — Disposition Split

Disposition quantities conserve affected quantity.

---

# 659. Test — Use As Is

Requires configured authority/reason.

---

# 660. Test — Customer Concession

Required evidence/approval enforced.

---

# 661. Test — Return Supplier

Purchasing/Inventory own execution.

---

# 662. Test — Scrap

Inventory scrap occurs once.

---

# 663. Test — Rework

Manufacturing creates/executes rework; Quality links result.

---

# 664. Test — NCR From Failed Inspection

No duplicate NCR on retry.

---

# 665. Test — Quality Issue No NCR

Can resolve as not confirmed.

---

# 666. Test — NCR Scope Expansion

Authorized/audited.

---

# 667. Test — Repeat NCR

Flag does not merge records.

---

# 668. Test — Containment

Hold/stop request executed through owning module.

---

# 669. Test — CAPA Action Complete

Does not set CAPA effective.

---

# 670. Test — CAPA Effective

Configured effectiveness criteria/evidence required.

---

# 671. Test — CAPA Ineffective

Reopens/escalates according to policy.

---

# 672. Test — SCAR

Supplier response/review lifecycle exact.

---

# 673. Test — Supplier Sampling Tightened

If advanced policy enabled, change is explainable/versioned.

---

# 674. Test — Produced vs Released

Quality Released Qty independent from Produced Qty.

---

# 675. Test — Shop Floor Gate

Manufacturing cannot continue when blocking Quality gate failed.

---

# 676. Test — Outgoing Hold

Shipment cannot bypass server-side restriction.

---

# 677. Test — Customer Return Not Quality

No NCR forced for non-quality reason.

---

# 678. Test — COA

Certificate reflects approved result/spec snapshot only.

---

# 679. Test — COA Supersede

Old issued certificate remains historical.

---

# 680. Test — Certificate Security

External user cannot access internal root-cause/NCR/cost fields.

# 681. Test — Cross-Tenant RLS

Tenant A cannot read or mutate Tenant B Quality data.

---

# 682. Test — Wrong Legal Entity

Blocked.

---

# 683. Test — Cross-Plant Inspector

Authorization enforced.

---

# 684. Test — Cost Permission

Inspector cannot read sensitive quality-cost data without permission.

---

# 685. Test — Result Replay

Retry does not duplicate one result.

---

# 686. Test — Disposition Replay

No double scrap/return/rework execution.

---

# 687. Test — 10M Inspection Results

Queue/history remains performant.

---

# 688. Test — High-Volume Receiving

Large receipt batch creates required inspections without N+1.

---

# 689. Test — Large Manufacturing Output

Sampling does not create one row per uninspected unit.

---

# 690. Test — Supplier Dashboard Scale

Projection query measured.

---

# 691. Property Test — Disposition Conservation

For finalized disposition:

```text
Accepted/Released
+ Rework
+ Repair
+ Return
+ Scrap
+ Regrade
+ Other explicit final disposition
=
Affected quantity
```

subject to open remainder rules.

---

# 692. Property Test — Hold Conservation

Released/consumed quantity cannot exceed quantity not under effective restriction.

---

# 693. Property Test — Sampling

Calculated sample quantity obeys exact SamplingPlan version and population.

---

# 694. Property Test — Result Determinism

Same result + same Specification snapshot → same deterministic evaluation.

---

# 695. Property Test — Reinspection History

Original result remains immutable while new reinspection may pass/fail independently.

---

# 696. Property Test — CAPA Close

CAPA cannot reach Closed unless configured effectiveness gate is satisfied/approved.

---

# 697. Architecture Test — No Inventory Writes

Quality cannot reference Inventory persistence implementation for stock/status mutation.

---

# 698. Architecture Test — No Manufacturing Writes

Quality cannot update ProductionOrder/Operation tables directly.

---

# 699. Architecture Test — No Purchasing Return Table

Quality does not own supplier-return transaction.

---

# 700. Architecture Test — No Customer Return Duplicate

Quality references source return only.

---

# 701. Architecture Test — No Asset Duplicate

Instrument/asset reference only.

---

# 702. Architecture Test — No Maintenance Duplicate

Calibration work remains `24`.

---

# 703. Architecture Test — No Journal Entry

Quality never creates Ledger entity directly.

---

# 704. Architecture Test — Formal QMS Boundary

Routine Service checklist remains Service-owned; formal QMS inspection remains Quality-owned.

---

# 705. Red-Team — Pass/Fail Only Quality Module

Reject.

---

# 706. Red-Team — One Current Quality Specification

Reject destructive history.

---

# 707. Red-Team — Failed Inspection Edited To Pass

Reject.

---

# 708. Red-Team — Quality Hold = Quarantine Location

Reject.

---

# 709. Red-Team — Quality Changes Inventory Status Directly

Reject.

---

# 710. Red-Team — Produced = Released

Reject.

---

# 711. Red-Team — Defect = NCR

Reject universal assumption.

---

# 712. Red-Team — Customer Complaint = Confirmed NCR

Reject.

---

# 713. Red-Team — Supplier Return = Supplier NCR

Reject.

---

# 714. Red-Team — CAPA Closed When Tasks Done

Reject.

---

# 715. Red-Team — Correction = Corrective Action

Reject.

---

# 716. Red-Team — Specification Limit = Control Limit

Reject.

---

# 717. Red-Team — Sampling Frequency = Sample Size

Reject.

---

# 718. Red-Team — AQL Hardcoded Without Version

Reject.

---

# 719. Red-Team — UseAsIs Without Approval

Reject for configured controlled material.

---

# 720. Red-Team — Quality Module Owns Supplier Block

Reject commercial ownership leak.

---

# 721. Red-Team — Quality Module Owns Rework Production Order

Reject.

---

# 722. Red-Team — COA Generated From Current Spec Instead Of Historical Snapshot

Reject.

---

# 723. Red-Team — AI Releases Held Stock

Reject.

---

# 724. Competitive Benchmark — Odoo 19 Quality

Current official patterns validate:

```text
Quality Control Points
automatic Quality Checks
operation/product/quantity control scope
frequency rules
Pass-Fail
Measure with norm/tolerance
Photo/evidence checks
quality checks embedded in Manufacturing/Inventory
Failure locations
```

Official references:

- https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/quality/quality_management/quality_control_points.html
- https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/quality/quality_management/quality_checks.html
- https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/quality/quality_check_types/measure_check.html
- https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/quality/quality_management/failure_locations.html

TODIR adopts:

- simple automatic trigger experience;
- checks inside the operational workflow;
- measurement/photo/pass-fail simplicity.

TODIR adds stronger formal:

```text
Plan versioning
Sampling/AQL
NCR
Disposition
CAPA
Supplier Quality
COA
SPC foundation
```

---

# 725. Competitive Benchmark — Dynamics 365 Supply Chain Management

Current official patterns validate:

```text
Quality Orders
automatic trigger criteria
inventory blocking during inspection
fixed/percentage sampling
Acceptance Sampling / AQL
quantitative + qualitative tests
Nonconformance
Correction handling
nonconformance cost
supplier/customer/production source context
```

Official references:

- https://learn.microsoft.com/en-us/dynamics365/supply-chain/inventory/quality-management-processes
- https://learn.microsoft.com/en-us/dynamics365/supply-chain/inventory/quality-orders
- https://learn.microsoft.com/en-us/dynamics365/supply-chain/inventory/quality-item-sampling
- https://learn.microsoft.com/en-us/dynamics365/supply-chain/inventory/quality-acceptance-sampling

TODIR adopts:

- broad quality triggers;
- sampling + AQL foundation;
- material blocking;
- nonconformance/correction/cost concepts.

TODIR separates Correction from CAPA more explicitly.

---

# 726. Competitive Benchmark — Oracle Fusion Quality Management 26B

Current official patterns validate:

```text
Inspection Plans
Receiving criteria by supplier
Inventory criteria by location
WIP criteria by operation
Asset inspection context
Inspection Characteristics
Ad-Hoc Inspections
Action Rules
Change Material Status
Create Quality Issue
Create Maintenance Work Order from asset inspection
Electronic signatures for quality dispositions
```

Official references:

- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/index.html
- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/inspection-plan-criteria.html
- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/inspection-plan-specifications.html
- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/ad-hoc-inspections.html
- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/manage-action-rules.html
- https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/26b/fauqm/electronic-signatures-and-electronic-records-for-quality.html

TODIR adopts:

- cross-domain plan applicability;
- ad-hoc inspection;
- result-driven action rules;
- formal e-signature seam;
- maintenance integration without duplicating Maintenance ownership.

---

# 727. Competitive Benchmark — IFS Cloud Quality Management

Current official Control Plan patterns validate:

```text
Control Plans for manufactured/purchased/inventory parts
multiple revisions
phase-in/phase-out dates
routing operation / work center linkage
sample frequency schedules
Acceptance Sampling
SPC
capability indices
measurement tool groups
```

Official reference:

- https://docs.ifs.com/ifsclouddocs/26r2/QualityManagement/AboutControlPlan.htm

TODIR adopts:

- versioned Quality Plans;
- effective dating;
- operation linkage;
- sampling architecture;
- instrument traceability;
- SPC/capability foundation.

---

# 728. Competitive Benchmark — NetSuite Quality Management

Current official patterns validate:

```text
Quality Specifications
Inspections
Ad-Hoc inspection
Statistical Sampling
AQL/ISO-oriented sampling
Critical/Major/Minor categories
Certificate Of Analysis
quality tablet workflow
```

Official references:

- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/article_0221101910.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/article_0221103218.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/article_0715122351.html
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/chapter_1498594076.html

TODIR adopts:

- accessible statistical sampling;
- defect severity categories;
- COA;
- tablet-friendly inspection.

---

# 729. Competitive Benchmark — SAP S/4HANA Quality Management

SAP remains an important architectural reference for mature concepts such as:

```text
Inspection Lots
Inspection Characteristics
Sampling Procedures
Defects
Usage Decisions
Quality Notifications
follow-up actions
```

TODIR adopts the business depth but deliberately does **not** expose SAP terminology/setup density to small users.

The TODIR equivalent user-facing concepts remain:

```text
Quality Plan
Inspection
Result
Disposition
Quality Issue / NCR
CAPA
```

---

# 730. Competitive Product Rule

Competitor feature presence proves a real quality-management need.

TODIR does not copy competitor screens or setup burden.

---

# 731. Competitive Product Target

> **Odoo-like operational simplicity + SAP/Oracle-grade inspection/disposition correctness + Dynamics/NetSuite sampling depth + IFS-style Control Plan/SPC foundation.**

---

# 732. Agent Implementation Philosophy

Quality business correctness first.

Do not begin with a generic checklist CRUD screen.

---

# 733. Agent Central Question

For every feature answer:

> **What requirement applies, what exact population/sample is inspected, what result was observed, what rule evaluated it, what quantity is restricted, what disposition is authorized, what downstream owner executes that disposition, and how is history preserved?**

---

# 734. Agent Must Read

At minimum:

```text
01
02
03
04
05
06
09
13
14
15
17
22
24
26
27
all AGENTS.md
approved ADRs
actual public contracts
```

---

# 735. Implementation Sequence

Recommended:

```text
QM00 Repository / Dependency Verification
QM01 Module Boundary + Schema + RLS
QM02 Characteristic Library
QM03 Specification Versioning
QM04 Quality Plan Core
QM05 Plan Applicability / Trigger Resolver
QM06 Sampling Foundation
QM07 Inspection / Snapshot
QM08 Result Capture / Evaluation
QM09 Instrument / Calibration Integration
QM10 Receiving Quality
QM11 Inventory Hold / Release
QM12 Manufacturing Inline Quality
QM13 Production Output Quality
QM14 Outgoing / Return Quality
QM15 Ad-Hoc Inspection
QM16 Defect / Quality Issue
QM17 Nonconformance / NCR
QM18 Containment
QM19 Disposition
QM20 Rework / Scrap / Return Integrations
QM21 Planned Deviation / Concession
QM22 Root Cause
QM23 CAPA
QM24 Effectiveness Verification
QM25 Supplier Quality
QM26 SCAR
QM27 Quality Cost Facts
QM28 COA / CoC
QM29 Product/Lot Quality History
QM30 Customer Quality Integration
QM31 Dynamic Sampling Foundation
QM32 SPC Foundation
QM33 Analytics / Dashboards
QM34 Mobile / Tablet UX
QM35 Migration
QM36 Security / SoD / E-Signature
QM37 Performance / Scale
QM38 Observability
QM39 Runbooks / Recovery
QM40 Receiving Inspector Acceptance
QM41 Manufacturing Quality Acceptance
QM42 Quality Manager Acceptance
QM43 Supplier Quality Acceptance
QM44 Cross-Module Reconciliation
QM45 E2E
QM46 Release Gate
```

---

# 736. QM00 Goal

Inspect repository truth before coding.

---

# 737. QM00 Must Verify

```text
Inventory lot/serial/status/restriction contracts
Purchase Receipt contracts
Manufacturing operation/output gate contracts
Sales/return/complaint contracts
Service calibration contract
Catalog/UOM
Files
Approvals/e-signature seam
Numbering
Audit
Notifications
Jobs
Outbox/Inbox
RLS
```

---

# 738. QM00 Exit Gate

No duplicate Inventory/Manufacturing/Purchasing/Service truth planned.

---

# 739. QM01 Goal

Create:

```text
Todir.Modules.Quality.Domain
Todir.Modules.Quality.Application
Todir.Modules.Quality.Infrastructure
Todir.Quality.Contracts
QualityDbContext
business_quality
RLS
architecture tests
```

---

# 740. QM01 Exit Gate

Boundaries enforced.

---

# 741. QM02 Goal

Reusable Characteristic library.

---

# 742. QM02 Exit Gate

Measurement/pass-fail/categorical/document/photo types work with UOM/evidence.

---

# 743. QM03 Goal

Versioned Specifications.

---

# 744. QM03 Exit Gate

Historical result evaluation reproducible.

---

# 745. QM04 Goal

Quality Plan/version.

---

# 746. QM04 Exit Gate

User can define recurring inspection once.

---

# 747. QM05 Goal

Automatic applicability/trigger resolution.

---

# 748. QM05 Exit Gate

Receipt/operation/output resolves correct Plan without manual recreation.

---

# 749. QM06 Goal

Sampling.

---

# 750. QM06 Build

```text
Full
Fixed
Percentage
Nth
Time-based
AQL provider seam
```

---

# 751. QM06 Exit Gate

Frequency and sample size remain distinct and reproducible.

---

# 752. QM07 Goal

Inspection + Plan snapshot.

---

# 753. QM07 Exit Gate

Later plan edits cannot mutate created Inspection.

---

# 754. QM08 Goal

Result capture/evaluation.

---

# 755. QM08 Exit Gate

Automatic evaluation correct; corrections preserve history.

---

# 756. QM09 Goal

Instrument trace/calibration validity.

---

# 757. QM09 Exit Gate

No duplicate Asset/Maintenance master.

---

# 758. QM10 Goal

Receiving Quality.

---

# 759. QM10 Exit Gate

Receipt workflow is simple and source ownership correct.

---

# 760. QM11 Goal

Quality restriction with Inventory.

---

# 761. QM11 Exit Gate

Quality cannot directly change stock but physical availability is enforced.

---

# 762. QM12 Goal

Inline Manufacturing Quality.

---

# 763. QM12 Exit Gate

Operator sees check in Shop Floor; Quality remains owner of formal result.

---

# 764. QM13 Goal

Final Production Quality.

---

# 765. QM13 Exit Gate

Produced and Released quantities remain distinct.

---

# 766. QM14 Goal

Outgoing/Return Quality.

---

# 767. QM14 Exit Gate

Shipment/return workflows integrate without Quality owning Sales/Inventory transaction.

---

# 768. QM15 Goal

Ad-Hoc inspection.

---

# 769. QM15 Exit Gate

Can inspect lot/serial/asset context without fake parent transaction.

---

# 770. QM16 Goal

Issue/Defect.

---

# 771. QM16 Exit Gate

Reported concern and confirmed NCR remain separate.

---

# 772. QM17 Goal

NCR.

---

# 773. QM17 Exit Gate

Requirement/evidence/scope/severity trace complete.

---

# 774. QM18 Goal

Containment.

---

# 775. QM18 Exit Gate

Immediate risk control does not masquerade as corrective action.

---

# 776. QM19 Goal

Disposition.

---

# 777. QM19 Exit Gate

Split disposition quantities conserve affected quantity.

---

# 778. QM20 Goal

Cross-module execution.

---

# 779. QM20 Exit Gate

Rework/Scrap/Return execute exactly once through owner modules.

---

# 780. QM21 Goal

Deviation/Concession.

---

# 781. QM21 Exit Gate

Pre-approved exception and post-failure acceptance remain distinct.

---

# 782. QM22 Goal

Root Cause.

---

# 783. QM22 Exit Gate

Defect symptom and causal conclusion remain separate.

---

# 784. QM23 Goal

CAPA workflow.

---

# 785. QM23 Exit Gate

Actions linked to source issue/NCR with owners/due dates.

---

# 786. QM24 Goal

Effectiveness.

---

# 787. QM24 Exit Gate

CAPA cannot close just because tasks finished.

---

# 788. QM25 Goal

Supplier Quality.

---

# 789. QM25 Exit Gate

Quality status/metrics available without Quality owning supplier master/commercial PO.

---

# 790. QM26 Goal

SCAR.

---

# 791. QM26 Exit Gate

Supplier response + verification history complete.

---

# 792. QM27 Goal

Quality cost analytics.

---

# 793. QM27 Exit Gate

Every amount source-drillable; no second accounting engine.

---

# 794. QM28 Goal

COA / CoC.

---

# 795. QM28 Exit Gate

Issued certificate uses exact approved snapshot and is immutable/superseding.

---

# 796. QM29 Goal

Product/Lot history.

---

# 797. QM29 Exit Gate

One quality timeline composes source truth without copying it.

---

# 798. QM30 Goal

Customer quality integration.

---

# 799. QM30 Exit Gate

Complaint != NCR; confirmed quality issue can trace to lot/serial/delivery.

---

# 800. QM31 Goal

Dynamic sampling foundation.

---

# 801. QM31 Exit Gate

Reduced/tightened mode changes are explainable and versioned.

---

# 802. QM32 Goal

SPC foundation.

---

# 803. QM32 Exit Gate

Spec limits and control limits remain distinct; measurements support future charts.

---

# 804. QM33 Goal

Dashboards/reporting.

---

# 805. QM33 Exit Gate

Every KPI definition documented/drillable.

---

# 806. QM34 Goal

Mobile/tablet inspector UX.

---

# 807. QM34 Exit Gate

Inspector can scan, measure, photo, defect, complete with minimal taps.

---

# 808. QM35 Goal

Migration.

---

# 809. QM35 Exit Gate

Open holds/NCR/CAPA reconcile; no fabricated results.

---

# 810. QM36 Goal

Security/SoD/e-signature.

---

# 811. QM36 Attack Cases

```text
Cross-tenant inspection read
Forged Product/Lot/Serial
Forged Supplier
Unauthorized specification change
Unauthorized result correction
Hold bypass
UseAsIs without approval
Disposition quantity tampering
Double scrap/return replay
Unauthorized CAPA close
COA issued from unapproved results
Instrument calibration bypass
Signature replay
```

---

# 812. QM36 Exit Gate

RLS + authorization + audit + optional SoD/e-signature pass.

---

# 813. QM37 Goal

Performance.

---

# 814. QM37 Load Tests

```text
10M inspection results
1M inspections
large receiving batch
large supplier dashboard
large product/lot history
concurrent shop-floor checks
```

---

# 815. QM37 Exit Gate

Targets pass or ADR.

---

# 816. QM38 Goal

Observability.

---

# 817. QM38 Exit Gate

Trigger/result/disposition/CAPA/COA paths traceable.

---

# 818. QM39 Goal

Semantic recovery without DB surgery.

---

# 819. QM39 Runbooks

Required:

```text
Wrong Plan selected
Wrong Specification version
Duplicate Inspection
Incorrect measurement/result
Instrument later found out of calibration
Hold requested but Inventory unknown result
Disposition execution unknown result
Wrong UseAsIs approval
Duplicate NCR
Incorrect NCR scope
CAPA closed incorrectly
SCAR overdue/stuck
COA issued with wrong source
Projection rebuild
DR restore validation
```

---

# 820. QM39 Forbidden Runbook

Never normal fix:

```text
UPDATE inspection_result SET value=...
UPDATE inventory_stock SET quality_hold=false
DELETE nonconformance...
UPDATE capa SET state='Closed'
UPDATE certificate SET result=...
```

Use semantic correction/reopen/supersede.

---

# 821. QM40 Receiving Inspector Acceptance

Scenario:

```text
Receive supplier lot
Quality required
Sample calculated
Measure
Defect
Fail
Hold
Disposition
Return/Reinspect
```

---

# 822. QM40 Pass Criteria

```text
fast
clear sample
clear limits
no duplicate data entry
stock cannot escape hold
```

---

# 823. QM41 Manufacturing Quality Acceptance

Scenario:

```text
Production operation
inline measurement
out-of-spec
operation blocked
NCR
rework
reinspection pass
production continues
```

---

# 824. QM41 Pass Criteria

Operator workflow remains simple; failed history remains visible.

---

# 825. QM42 Quality Manager Acceptance

Can manage:

```text
critical issue
NCR
containment
MRB/disposition
root cause
CAPA
effectiveness
```

without spreadsheets.

---

# 826. QM43 Supplier Quality Acceptance

Scenario:

```text
Repeated supplier failures
supplier score changes
SCAR
response
next receipts verification
sampling tightened/reduced if enabled
```

---

# 827. QM44 Cross-Module Reconciliation

Validate:

```text
Quality restrictions
= Inventory effective restrictions

Quality scrap/return disposition execution
= Inventory/Purchasing source transaction

Quality rework request
= Manufacturing rework execution

Calibration validity
= Service/Maintenance source

Supplier identity
= Directory

Product/Lot/Serial
= Catalog/Inventory
```

---

# 828. QM44 Exit Gate

Zero unexplained differences.

---

# 829. QM45 E2E — Incoming Pass

```text
PO Receipt
→ Inspection
→ Sample
→ Pass
→ Release Stock
```

---

# 830. QM45 E2E — Incoming Fail

```text
Receipt
→ Hold
→ Fail
→ NCR
→ Supplier SCAR
→ Return / Reinspect
```

---

# 831. QM45 E2E — Manufacturing Gate

```text
Production Operation
→ Quality Check
→ Fail
→ Hold
→ Rework
→ Reinspection
→ Pass
→ Continue
```

---

# 832. QM45 E2E — Final Output

```text
Produced
→ Final Inspection
→ Partial Release
→ Rework/Scrap remainder
```

---

# 833. QM45 E2E — Customer Complaint

```text
Complaint source
→ Quality Issue
→ Trace Lot/Serial
→ Confirm NCR
→ Containment
→ CAPA
→ Effectiveness
```

---

# 834. QM45 E2E — COA

```text
Approved lot inspection
→ Generate COA
→ Approve
→ Issue
→ Link shipment/customer
```

---

# 835. QM45 Exit Gate

Real PostgreSQL/Testcontainers + canonical contracts pass end to end.

---

# 836. QM46 Release Gate

Quality does not ship because a checklist can be completed.

It ships when requirement, sampling, results, restrictions, disposition and improvement are trustworthy together.

---

# 837. QM46 Required Sign-Off

```text
Product Architect
Quality Manager
Quality Engineer
Receiving/Inspector Reviewer
Manufacturing Lead
Inventory Lead
Purchasing/Supplier Quality Lead
Service/Maintenance Reviewer
Security Reviewer
Performance Engineer
QA Lead
```

---

# 838. QM46 Mandatory Evidence

```text
Architecture tests
RLS/security report
Plan/version tests
Sampling vectors
AQL vectors when enabled
Result evaluation tests
Instrument/calibration tests
Hold/release reconciliation
Disposition conservation
NCR/CAPA tests
Supplier Quality/SCAR tests
COA snapshot tests
Manufacturing gate tests
Performance benchmarks
Acceptance reports
Runbooks
Known limitations
```

---

# 839. Build Now — MUST

```text
Quality Plan
Plan Versions
Applicability / Trigger Rules
Characteristic Library
Specification Versions
Sampling: Full/Fixed/Percentage
AQL provider foundation
Inspection
Plan Snapshot
Result Capture
Automatic Evaluation
Photo/Document Evidence
Ad-Hoc Inspection
Reinspection
Quality Hold/Release integration
Disposition
Defect
Quality Issue
NCR
Containment
Root Cause
CAPA
Effectiveness Check
Supplier Quality
SCAR
Receiving Quality
Inventory Quality
Manufacturing Inline/Final Quality
Outgoing/Return Quality foundation
COA/CoC foundation
Instrument trace/calibration integration
Quality Cost analytics
Product/Lot/Supplier history
Permissions/RLS/Audit
Migration
Performance
Runbooks
Acceptance gates
```

---

# 840. Build Later / Evidence-Based

```text
Advanced dynamic sampling
Full ISO sampling tables/content packs
SPC charts and automated signals
Cp/Cpk/Pp/Ppk dashboards
Full physical retained-sample laboratory management
LIMS integration
Advanced Quality Audits
Layered Process Audits
Full Recall Management
Advanced regulatory e-record controls
IoT continuous process quality
AI-assisted root-cause recommendations
```

---

# 841. Final Source-of-Truth Matrix

```text
Quality Plan / Specification     → Quality
Sampling Plan                    → Quality
Inspection / Sample / Result     → Quality
Quality Hold Decision            → Quality
Disposition Decision             → Quality
Quality Issue / NCR              → Quality
CAPA / Effectiveness             → Quality
Supplier Quality / SCAR          → Quality
COA / Quality Certificate        → Quality

Product / UOM                    → Catalog
Supplier / Customer identity     → Directory
Stock / Lot / Serial             → Inventory
Stock Status / Movement          → Inventory
PO / Supplier Return Commercial  → Purchasing
Production Order / Operation     → Manufacturing
Rework Execution                 → Manufacturing
Maintenance / Calibration Work   → Service & Maintenance
Asset identity                   → Fixed Assets
Customer Commercial Return       → Sales/Inventory
Accounting                       → Ledger
```

---

# 842. Final Plan Rule

> **Quality requirements are defined once in versioned Quality Plans and automatically resolved in the operational workflow.**

---

# 843. Final Result Rule

> **An Inspection records what actually happened under a specific plan/specification snapshot; later master changes never rewrite that history.**

---

# 844. Final Sampling Rule

> **Frequency, population, sample size and acceptance criteria are separate, explicit and historically reproducible.**

---

# 845. Final Hold Rule

> **Quality owns the reason/decision to restrict material; Inventory owns stock status, location, availability and physical movement.**

---

# 846. Final Disposition Rule

> **Evaluation says whether requirements passed; Disposition says what the business is authorized to do with affected quantity.**

---

# 847. Final Reinspection Rule

> **Rework or correction never changes a failed Inspection to Pass; it creates new evidence through reinspection.**

---

# 848. Final NCR Rule

> **A Defect is an observed condition; an NCR is a confirmed requirement failure with scope, evidence and disposition.**

---

# 849. Final CAPA Rule

> **Containment controls current risk, Correction fixes current affected product, Corrective Action attacks root cause, and CAPA closes only after effectiveness is verified.**

---

# 850. Final Supplier Rule

> **Quality owns supplier-quality evidence and recommendations; Purchasing owns supplier commercial commitments and sourcing execution.**

# 851. Final Manufacturing Rule

> **Manufacturing can require a Quality gate, but Quality owns the formal result/NCR/disposition and Manufacturing owns production execution state.**

---

# 852. Final Produced-vs-Released Rule

> **Produced quantity and Quality Released quantity are separate facts and may legitimately differ.**

---

# 853. Final Instrument Rule

> **Quality records which instrument supported a result and validates calibration status, while Asset/Service domains keep canonical equipment and calibration-work ownership.**

---

# 854. Final COA Rule

> **A COA is an issued, versioned document derived from approved historical result/specification snapshots; it is never rebuilt from today's specification as if history changed.**

---

# 855. Final SPC Rule

> **Specification limits define acceptance; control limits describe statistical process behavior. They are never interchangeable.**

---

# 856. Final Customer Complaint Rule

> **A customer complaint starts an investigation; it does not become a confirmed Quality NCR until evidence proves a requirement failure.**

---

# 857. Final Customer Return Rule

> **A commercial return may have no quality defect; Quality is invoked only when the return requires formal inspection/investigation.**

---

# 858. Final Quality Cost Rule

> **Quality can explain Cost of Poor Quality by composing source-linked cost facts, but it never becomes a second Ledger or Inventory costing engine.**

---

# 859. Final History Rule

> **Failed results, previous specifications, dispositions, CAPA actions and issued certificates are historical evidence and are corrected by new records/reversals/supersession—not destructive edits.**

---

# 860. Final Small-Company Rule

> **A small company should be able to configure a two-check incoming inspection in minutes and process it directly during receipt without learning enterprise QMS terminology.**

---

# 861. Final Enterprise Rule

> **A larger organization can enable AQL, MRB, CAPA, supplier-quality controls, e-signatures, COA, instrument trace, dynamic sampling and SPC without replacing the Quality core.**

---

# 862. Final User-Speed Rule

Before adding another Quality screen/step, ask:

```text
Can TODIR infer the applicable plan, sample and failure action safely?
Can the quality step appear inside the operational workflow?
Does this decision truly require the user?
```

Prefer safe inference and exception-driven work.

---

# 863. Final Receiving UX Rule

Normal operator experience:

```text
Receive
→ system says Inspection Required
→ Scan / Measure / Pass-Fail
→ system handles Hold/Release workflow
```

not manual plan recreation.

---

# 864. Final Shop-Floor UX Rule

Normal operator experience:

```text
Operation
→ required Quality Check appears inline
→ record result
→ continue or see exact blocker
```

not navigating to a separate QMS menu.

---

# 865. Final Quality Manager UX Rule

Quality Manager should work primarily from exceptions:

```text
Failed Inspections
Held Lots
Critical NCR
Disposition Waiting
CAPA Overdue
Supplier Risk
```

not manually inspect every successful transaction.

---

# 866. Final Automation Rule

> **TODIR automates repeatable deterministic quality work, but irreversible/high-risk decisions such as UseAsIs, release of critical held material, CAPA effectiveness and certificate issue remain governed.**

---

# 867. Final Agent Rule

For every Quality implementation decision, the Agent must answer:

> **Which requirement/version applies, what is the population and sample, what was observed, how was it evaluated, what quantity/subject is restricted, what disposition is authorized, which module executes the physical/commercial consequence, what investigation/improvement follows, and how is all historical evidence preserved?**

If any answer is unclear, implementation is incomplete.

---

# 868. Agent Rule — Never Duplicate Physical Truth

Before creating a Quality field/table for:

```text
Stock Status
Lot
Serial
Production State
Supplier Return
Maintenance State
Customer Return
```

first confirm the owning module.

---

# 869. Agent Rule — Never Hide Evaluation Basis

Every Pass/Fail must be explainable by:

```text
Characteristic
Specification version
Result
UOM
Sampling/evaluation rule
Inspector/system
Timestamp
```

---

# 870. Agent Rule — Never Collapse Decision Layers

Do not implement:

```text
result.fail → inventory.scrap
```

as one hidden step.

Preserve:

```text
Result
→ Evaluation
→ NCR/Disposition if required
→ Authorized execution
```

---

# 871. Agent Rule — Cross-Module Failure Safety

If Quality approves a disposition but downstream Inventory/Manufacturing execution fails/returns unknown result:

```text
Do not mark disposition Executed falsely.
```

Use idempotent retry/reconciliation/runbook.

---

# 872. Agent Rule — Rebuildable Analytics

Dashboards and supplier/product quality scores are projections.

They never become primary inspection truth.

---

# 873. No Separate Quality Subfeature Packs

Do **not** create separate architecture packs for:

```text
Incoming Quality
Inspection Plans
Sampling
NCR
CAPA
Supplier Quality
COA
SPC Foundation
```

They are all part of `27 Quality Management`.

---

# 874. Future Separate Domain Decision

A future deeply specialized capability may become separate only with evidence, for example:

```text
Full Laboratory Information Management System (LIMS)
Enterprise PLM/QMS document-control suite
Regulated Recall Management
Advanced Audit Management
```

not by default.

---

# 875. Research Basis — Current Competitor Patterns

This design intentionally incorporates verified current official patterns from:

```text
Odoo 19 Quality
Microsoft Dynamics 365 Supply Chain Management Quality
Oracle Fusion Quality Management 26B
IFS Cloud Quality Management 26R2
NetSuite Quality Management
SAP S/4HANA Quality Management concepts
```

Competitor research validates business needs; TODIR keeps its own domain boundaries and UX.

---

# 876. Research Closure — Odoo Lesson

Keep quality checks close to:

```text
Receipt
Manufacturing
Delivery
```

so workers perform the required action without workflow fragmentation.

---

# 877. Research Closure — Dynamics / NetSuite Lesson

Sampling is a first-class quality decision, including:

```text
fixed
percentage
AQL/statistical
Critical/Major/Minor
```

not an afterthought.

---

# 878. Research Closure — Oracle / SAP Lesson

Mature Quality requires distinct:

```text
Plan/Requirement
Inspection
Result
Disposition
Issue/Nonconformance
Follow-up action
```

TODIR preserves those semantics with simpler names/workflows.

---

# 879. Research Closure — IFS Lesson

Quality measurement history should be rich enough for:

```text
versioned control plans
instrument trace
sampling
SPC
capability analysis
```

without forcing those advanced tools on every customer.

---

# 880. Architecture Closure

TODIR Quality Management must remain:

```text
Simple outside.
Powerful inside.
Plan once.
Inspect in workflow.
Sample explicitly.
Preserve evidence.
Separate result from disposition.
Hold safely.
Correct without rewriting history.
Investigate root cause.
Verify CAPA effectiveness.
Keep source ownership clean.
```

A system with beautiful Pass/Fail screens but weak sampling, destructive history, bypassable holds, ambiguous disposition or duplicated Inventory/Manufacturing truth fails this pack.

---

**END OF FILE — 27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
