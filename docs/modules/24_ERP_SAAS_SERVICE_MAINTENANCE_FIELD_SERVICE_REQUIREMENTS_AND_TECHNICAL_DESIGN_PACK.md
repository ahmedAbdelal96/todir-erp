# 24 — TODIR ERP SaaS Service / Maintenance / Field Service Requirements & Technical Design Pack

**File:** `24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + INTERNAL MAINTENANCE + CUSTOMER FIELD SERVICE + INSTALLED BASE + WORK EXECUTION + SERVICE BILLING + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Service Manager, Maintenance Manager, Dispatcher, Field Technician, Asset Controller, Customer Service Lead, Inventory Lead, Purchasing Lead, Finance/Accounting Lead, Sales/CRM Lead, HR Lead, Projects Lead, Backend/Frontend Leads, Mobile Lead, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — EQUIPMENT UPTIME + FIELD EXECUTION + CUSTOMER SLA + PARTS + WARRANTY + BILLING + OFFLINE OPERATIONS**

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
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:

- Directory owns Customer/Party/Contact identity.
- Catalog owns Product/Variant/UOM.
- Inventory owns stock, reservations, issues, returns, valuation, warehouses and technician/van stock locations.
- Purchasing owns Purchase Requests/POs and external service procurement.
- HR owns Employee/Employment, work calendars and qualifications source.
- HR Attendance is not technician service time.
- Expenses owns employee expense truth.
- Fixed Assets owns company-owned Asset identity, financial books, depreciation and asset lifecycle.
- Projects owns Project/Project Cost/Billable Charges when Projects is the billing owner.
- Invoicing owns legal customer/supplier invoices.
- Subledger owns receivables/open items.
- Payments owns actual money movement.
- Ledger owns Journal Entries/account determination.
- Platform Files owns file bytes.
- Service must therefore own service demand, installed customer equipment, work execution, entitlement/SLA logic and service-billing eligibility without duplicating those truths.

---

# 0. Executive Decision

TODIR `24 Service & Maintenance` is one actual business module with:

```text
Shared Work Execution Core
+
Internal Maintenance
+
Customer Field Service
```

The shared core exists because both internal maintenance and customer service need:

```text
Work Request/Demand
Work Order
Work Scope
Work Template
Technician/Crew
Scheduling
Visit/Assignment
Checklists
Parts
Time
Diagnosis
Completion
Documents
Cost
```

But they are **not one identical workflow**.

Internal Maintenance works on:

```text
Company Fixed Assets / operational equipment
```

Customer Field Service works on:

```text
Customer / third-party Installed Base
```

and can involve:

```text
Warranty
Service Contract
SLA
Customer billing
Customer acceptance
```

---

# 1. Central Product Question

> **What service or maintenance is required, on which equipment/site, why, by when, who should execute it, what parts/time were used, what was found and fixed, what coverage applies, who pays, what did it cost, and can TODIR prove the full service history afterward?**

---

# 2. Product Law — Simplicity First

A small company must be able to run:

```text
Problem
→ Work Order
→ Technician
→ Do Work
→ Parts
→ Complete
```

without configuring:

```text
SLA matrix
resource optimization
warranty entitlements
service contract caps
crew scheduling
meter-based maintenance
offline sync conflict rules
```

unless needed.

---

# 3. Product Law — Correct Business Under The Simplicity

Internally TODIR still distinguishes:

```text
Request
Work Order
Visit
Work Assignment
Technical Completion
Customer Acceptance
Billing Eligibility
Invoice
Payment
```

even if a simple workflow collapses several actions into one guided screen.

---

# 4. Product Law — Domain Separation Must Not Fragment UX

A technician can press:

```text
[Use Part]
```

and TODIR can orchestrate Inventory.

The technician does not need to open Inventory manually.

A dispatcher can press:

```text
[Create Purchase Request]
```

for a missing part.

Purchasing remains owner.

---

# 5. Absolute Domain Distinctions

```text
Service Request != Work Order
Work Order != Visit
Visit != Technician
Work Order != Project
Work Order != Invoice
Work Order != Inventory Movement
Work Order != Fixed Asset
```

---

# 6. Equipment Distinctions

```text
Company Fixed Asset
!=
Customer Installed Base Item
```

---

# 7. Commercial Distinctions

```text
Warranty
!=
Service Contract
!=
SLA
!=
Billing Rule
```

---

# 8. Execution Distinctions

```text
Required Part
!=
Reserved Part
!=
Issued Part
!=
Consumed Part
!=
Returned Part
```

---

# 9. Time Distinctions

```text
Travel Time
!=
Work Time
!=
Waiting Time
!=
Break Time
!=
HR Attendance
```

---

# 10. Completion Distinctions

```text
Technically Completed
!=
Customer Accepted
!=
Financially Closed
!=
Invoiced
!=
Paid
```

---

# 11. Maintenance Value Distinction

```text
Maintenance Cost
!=
Fixed Asset Capital Addition
```

---

# 12. Billing Ownership Distinction

A Work Order linked to Project must have one billing owner:

```text
Service
or
Projects
```

Never both.

---

# 13. Offline Rule

Field Service architecture must be offline-ready from Day 1 even if first UI release is online-first.

---

# 14. Service Contexts

Recommended:

```text
InternalMaintenance
CustomerFieldService
```

with shared execution engine.

---

# 15. Internal Maintenance

Examples:

```text
Factory machine repair
Generator preventive maintenance
Vehicle inspection
Office equipment maintenance
Asset calibration
Building equipment check
```

---

# 16. Customer Field Service

Examples:

```text
HVAC service
Elevator maintenance
Generator repair
Medical equipment service
IT onsite support
Security system maintenance
Solar installation/service
Industrial machine support
Appliance service
Telecom field work
```

---

# 17. Internal Subject

Usually:

```text
FixedAssetRef
```

from `22 Fixed Assets`.

Can also support:

```text
OperationalTrackedAsset
WorkCenter/FunctionalLocation later
```

without duplicate financial asset identity.

---

# 18. Customer Subject

Usually:

```text
InstalledBaseItem
```

owned by Service.

---

# 19. Installed Base Definition

An Installed Base Item is:

> **A specific product/equipment/component installed at or associated with a customer/site that TODIR services but does not necessarily own.**

---

# 20. Installed Base Is Not Fixed Asset

Customer-owned generator is not our company Fixed Asset.

---

# 21. Installed Base Is Not Product

Product says:

```text
what model/item this is
```

Installed Base says:

```text
which exact serialized/installed instance this customer has
```

---

# 22. Installed Base Can Reference Product

Optional:

```text
ProductVariantRef
Serial
Model
```

---

# 23. Installed Base Can Exist Without Catalog Product

Legacy equipment or third-party asset may be serviced without TODIR Catalog master.

---

# 24. Installed Base Ownership

Can capture:

```text
EquipmentOwnerParty
ServiceCustomerParty
ContractHolderParty
BillToParty
ServiceLocation
RequesterContact
```

with sensible defaults.

---

# 25. Do Not Collapse Parties Into CustomerId

Real-world case:

```text
Owner       = ABC Holding
Service Site = ABC Factory Alexandria
Requester   = Plant Maintenance Manager
Contract    = ABC Holding
Bill-To     = ABC Finance Co.
```

---

# 26. Installed Base Core

Concept:

```text
InstalledBaseItem
{
    Id
    TenantId
    LegalEntityId

    OwnerPartyId?
    ServiceCustomerPartyId?
    ProductVariantRef?
    SerialNumber?
    ModelText?

    ParentInstalledBaseItemId?

    ServiceLocationId
    InstalledAt?
    CommissionedAt?

    LifecycleState
    ServiceStatus

    WarrantyContext?
    ExternalRef?

    RowVersion
}
```

---

# 27. Installed Base Hierarchy

Support:

```text
Production Line
├ Machine
│  ├ Motor
│  ├ Controller
│  └ Sensor
└ Packaging Unit
```

---

# 28. Installed Base Component Replacement

When serialized component is replaced:

```text
Old component removed
New component installed
```

history preserved.

---

# 29. Component Replacement Is Not Product Edit

It is Installed Base lifecycle event.

---

# 30. Installed Base Lifecycle

Recommended:

```text
Draft
Installed
Active
OutOfService
Removed
Replaced
Retired
```

---

# 31. Installed Base Service Status

Separate from ownership/lifecycle:

```text
Serviceable
Suspended
ContractExpired
WarrantyOnly
NoCoverage
```

projection/context, not one mega status.

---

# 32. Installed Base Site Transfer

Customer equipment can move between service locations.

History preserved.

---

# 33. Company Asset Service Subject

Internal Work Order references:

```text
FixedAssetId
```

through `Todir.Assets.Contracts`.

---

# 34. No Duplicate Maintenance Equipment Master

Do not create independent copy of Fixed Asset just because Maintenance needs it.

---

# 35. Fixed Asset History Composition

Fixed Assets may display:

```text
Maintenance History
Last Service
Next Service
Downtime
Maintenance Cost Summary
```

from `24`.

Fixed Assets does not own Work Orders.

---

# 36. Service Demand Sources

Work can begin from:

```text
Service Request
Maintenance Request
Preventive Schedule
Inspection finding
Meter trigger
Customer Contract
Project
Sales installation
Asset condition
Manual Work Order
External API
IoT/Condition signal later
```

---

# 37. Service Request

Captures reported need/problem.

---

# 38. Service Request Is Not Execution Plan

Request says:

```text
what was reported / requested
```

Work Order says:

```text
what TODIR decided to execute
```

---

# 39. Service Request Concept

```text
ServiceRequest
{
    Id
    LegalEntityId
    ContextType

    CustomerPartyId?
    RequesterContactId?
    ServiceLocationId?

    SubjectType
    SubjectRef?

    ReportedProblem
    RequestedServiceType?
    Priority

    RequestedAt
    PreferredWindow?

    State
    TriageState
    SLAContext?

    Source
    RowVersion
}
```

---

# 40. Request States

Simple:

```text
New
Triaged
Planned
Resolved
Closed
Cancelled
```

---

# 41. Request Can Create One Work Order

Common.

---

# 42. Request Can Create Multiple Work Orders

Example:

```text
Electrical
Mechanical
Calibration
```

for same request.

---

# 43. Multiple Requests Can Merge Into One Work Order

Possible when dispatcher decides same visit/scope handles them.

Preserve links.

---

# 44. Work Order

Canonical execution container.

---

# 45. Work Order Mission

> **Defines authorized work scope, service subject/site, requirements, parts/services/checklists and execution outcome.**

---

# 46. Work Order Concept

```text
WorkOrder
{
    Id
    LegalEntityId
    Number

    ContextType
    RequestId?

    CustomerPartyId?
    ServiceLocationId?

    SubjectType
    SubjectRef?

    WorkTypeId
    Priority
    Criticality?

    BillingOwner
    ProjectRef?

    PlannedStart?
    PlannedEnd?

    LifecycleState
    TechnicalState
    CoverageState
    BillingState

    RowVersion
}
```

---

# 47. Work Order Number

Platform Numbering.

---

# 48. Work Order Context

```text
InternalMaintenance
CustomerService
ProjectService
Installation
Inspection
Calibration
WorkshopRepair
```

can be typed/configurable.

---

# 49. Work Order States

Recommended simple lifecycle:

```text
Draft
ReadyToSchedule
Scheduled
InProgress
Waiting
TechnicallyCompleted
Closed
Cancelled
```

---

# 50. Waiting Reason

Typed:

```text
WaitingForPart
WaitingForCustomer
WaitingForApproval
WaitingForVendor
WaitingForAccess
WaitingForWeather
Other
```

---

# 51. Waiting Reason Is Not SLA Pause Automatically

SLA policy decides clock behavior.

---

# 52. Work Order Can Have Multiple Visits

Core requirement.

---

# 53. Work Order Can Have Multiple Technicians

Core requirement.

---

# 54. Work Order Can Have Multiple Service Subjects

Support carefully for:

```text
maintenance rounds
inspection routes
site-wide service
```

while retaining per-subject results.

---

# 55. Visit / Work Assignment

Represents one scheduled execution occurrence.

---

# 56. Visit Concept

```text
ServiceVisit
{
    Id
    WorkOrderId

    PlannedStart
    PlannedEnd

    ServiceLocationId

    State

    ActualTravelStart?
    ActualArrival?
    ActualWorkStart?
    ActualWorkEnd?
    ActualDeparture?

    CompletionReason?
    FollowUpRequired?
}
```

---

# 57. Visit States

```text
Unscheduled
Scheduled
Dispatched
Traveling
Arrived
InProgress
Waiting
Completed
Cancelled
NoAccess
```

---

# 58. Visit != Work Order

One Work Order may need:

```text
Visit 1 diagnose
Visit 2 install ordered part
```

---

# 59. Work Assignment

Assignment of resource/crew to Visit.

---

# 60. Work Assignment Concept

```text
WorkAssignment
{
    VisitId
    ResourceType
    ResourceRef
    Role
    State
    PlannedDuration?
    ActualWorkDuration?
}
```

---

# 61. Resource Types

```text
EmployeeTechnician
ExternalContractor
Crew
PartnerServiceCenter
```

---

# 62. Technician Is Not Always Employee

Required.

---

# 63. Employee Technician

References HR Employee.

---

# 64. External Contractor

References approved external resource/Party relationship.

---

# 65. Crew

Group of service resources.

---

# 66. Crew Assignment

Can be scheduled as unit while preserving member execution where needed.

---

# 67. Service Territory

Optional geographic/business scheduling unit.

---

# 68. Territory Is Not Branch

Can overlap business branches.

---

# 69. Resource Requirements

Work Order/Visit may require:

```text
Skills
Certificates
Resource Type
Crew Size
Special Tool
Vehicle
```

---

# 70. Resource Skill Source

HR Qualifications/skills or service-specific certification references.

---

# 71. Skills Do Not Need Full Workforce Planning Day 1

Simple mode:

```text
choose technician
```

Advanced scheduling uses skills.

---

# 72. Scheduling Mission

Find feasible resource/time for work.

---

# 73. Scheduling Inputs

Potential:

```text
Availability
Work Calendar
Leave
Skill
Certification
Territory
Location
Travel
Priority
SLA
Parts readiness
Customer window
Existing workload
```

---

# 74. Manual Scheduling First

Core supports:

```text
Choose Technician
Choose Date/Time
```

without optimizer.

---

# 75. Scheduling Assistant Later/Optional

Suggest best candidates.

---

# 76. Optimization Is Not Day 1 Requirement

Do not build heavy route optimizer without evidence.

---

# 77. But Data Model Must Support Optimization

Resource Requirements + Visits + locations + calendars must be explicit.

---

# 78. Travel Time

Separate tracked duration.

---

# 79. Work Time

Actual service execution.

---

# 80. HR Attendance

Remains HR truth.



# 81. Service Time Categories

Optional:

```text
Travel
OnSiteWork
RemoteWork
Waiting
Break
Administrative
```

---

# 82. Small Mobile Mode

Technician can use only:

```text
Travel
Arrive
Start
Complete
```

TODIR derives durations.

---

# 83. Time Correction

After service cost/billing use:

```text
Correction
```

not silent edit.

---

# 84. Work Template

First-class versioned template for repeat service.

---

# 85. Work Template Mission

> **Turn recurring knowledge into repeatable, safe, fast work execution.**

---

# 86. Work Template Can Define

```text
Work Type
Expected Duration
Required Skills
Checklist
Operations
Suggested Parts
Suggested Services
Required Measurements
Required Photos
Safety Instructions
Completion Criteria
Default Billing/Coverage classification hints
```

---

# 87. Template Is Versioned

Historical Work Orders keep applied template version.

---

# 88. Template Update

Does not rewrite old work.

---

# 89. Template Examples

```text
Generator 500h PM
Elevator Quarterly Inspection
AC Compressor Replacement
Laptop Preventive Check
Fire Alarm Inspection
Solar Inverter Service
```

---

# 90. Incident / Problem Type

Can suggest Work Template.

---

# 91. Reported Problem != Diagnosis

Locked.

---

# 92. Diagnosis != Root Cause

Locked.

---

# 93. Root Cause != Resolution

Locked.

---

# 94. Failure Taxonomy

Recommended typed dictionaries:

```text
ReportedSymptom
FailureMode
RootCause
ResolutionCode
```

---

# 95. Free Text Still Allowed

But typed classification enables analytics.

---

# 96. Example Failure Chain

```text
Reported:
Generator does not start

Failure Mode:
Starter motor failure

Root Cause:
Bearing wear

Resolution:
Replace starter motor
```

---

# 97. Repeat Failure

System can flag:

```text
Same Subject
Same Failure Mode
within N days
```

---

# 98. Repeat Failure Is Not Auto-Reopen

Company policy decides:

```text
new Work Order
follow-up
warranty rework
```

---

# 99. Rework

Explicit relation:

```text
ReworkOfWorkOrderId
```

---

# 100. First-Time Fix

Derived metric:

```text
Resolved without follow-up visit/rework
```

definition configurable.

---

# 101. Inspection

Inspection is typed execution with questions/measurements.

---

# 102. Checklist Question Types

```text
PassFail
YesNo
Number
MeasurementWithUom
Text
SingleChoice
MultiChoice
Date
Photo
Signature
```

---

# 103. Measurement Must Include UOM

No naked numeric field.

---

# 104. Measurement Limits

Template can define:

```text
Min
Max
Target
Tolerance
```

---

# 105. Out-of-Range Result

Can:

```text
Warn
Require Comment
Create Follow-up
Block Completion
```

according to rule.

---

# 106. Checklist Snapshot

Work Order freezes template version/questions.

---

# 107. Checklist Completion

Required questions enforced.

---

# 108. Photos

Platform Files.

---

# 109. Photo Requirement

Can require:

```text
Before
After
Serial Plate
Failure Evidence
Meter Reading
```

---

# 110. Customer Signature

Optional completion/acceptance evidence.

---

# 111. Signature Is Not Authentication

Record:

```text
Signer Name
Role?
Timestamp
Context
Signature File/DataRef
```

Do not infer legal identity beyond workflow.

---

# 112. Service Report

Generated from canonical Work Order/Visit data.

---

# 113. Service Report Contents

Potential:

```text
Customer
Site
Equipment
Reported Problem
Diagnosis
Work Performed
Parts
Measurements
Technicians
Time
Photos
Recommendations
Customer Acceptance
Signature
```

---

# 114. Report Is Snapshot

Generated version preserved if customer signed.

---

# 115. Service Report Correction

New version/annotation, not silent mutation.

---

# 116. Preventive Maintenance

First-class.

---

# 117. Preventive Maintenance Plan

Concept:

```text
MaintenancePlan
{
    Id
    ContextType
    SubjectRef or SubjectGroup

    TemplateVersionId

    TriggerType
    ScheduleDefinition

    EffectiveFrom
    EffectiveTo?

    PlanningHorizon
    AutoGeneratePolicy

    State
}
```

---

# 118. Preventive Trigger Types

```text
CalendarFixed
FromLastCompletion
MeterThreshold
UsageThreshold
ConditionBasedLater
```

---

# 119. Fixed Calendar

Example:

```text
Every 3 months on planned cadence
```

Late completion does not shift next date unless policy says.

---

# 120. From Last Completion

Example:

```text
30 days after actual completion
```

floating schedule.

---

# 121. Meter Trigger

Examples:

```text
Every 10,000 KM
Every 500 Run Hours
Every 100,000 Cycles
```

---

# 122. Meter Definition

First-class:

```text
MeterDefinition
{
    Code
    Name
    Uom
    MonotonicPolicy
    RolloverPolicy?
}
```

---

# 123. Meter Reading

```text
MeterReading
{
    SubjectRef
    MeterDefinitionId
    Value
    ReadingAt
    Source
    Evidence?
}
```

---

# 124. Meter Sources

```text
TechnicianManual
CustomerReported
Imported
IoTLater
VehicleIntegrationLater
```

---

# 125. Meter Validation

Detect:

```text
unexpected decrease
duplicate reading
future timestamp
impossible jump
```

---

# 126. Meter Reset

Explicit event.

---

# 127. Maintenance Due

Schedule entry represents future/due work before Work Order exists.

---

# 128. Maintenance Due != Work Order

Locked.

---

# 129. Maintenance Schedule Entry

Concept:

```text
MaintenanceDue
{
    PlanId
    SubjectRef
    DueDate?
    DueMeterValue?
    DueWindowStart?
    DueWindowEnd?
    State
}
```

---

# 130. Due States

```text
Forecast
DueSoon
Due
Overdue
ConvertedToWork
Skipped
Superseded
```

---

# 131. Planning Horizon

Generate forecast for:

```text
30 / 60 / 90 days
```

configurable.

---

# 132. Overlapping Preventive Work

Policy can consolidate compatible due items.

---

# 133. Maintenance Round

One execution grouping over multiple subjects.

---

# 134. Round Example

```text
Monthly Safety Round
→ 20 machines
```

---

# 135. Round Result

Each subject has:

```text
Result
Measurements
Exceptions
Follow-up
```

---

# 136. Round Work Order

Can contain multiple subject lines.

---

# 137. Inspection Route

Similar to round but ordered route.

---

# 138. Preventive Auto-Generation

Options:

```text
ForecastOnly
AutoCreateWorkOrder
AutoCreateAndScheduleLater
```

---

# 139. No Auto-Generate Infinite Work Orders

Use planning horizon.

---

# 140. Preventive Plan Change

Version/effective-date.

Do not rewrite completed history.

---

# 141. Maintenance Skip

Requires:

```text
reason
actor
next due recalculation policy
```

---

# 142. Maintenance Postpone

Separate from Skip.

---

# 143. Grace Window

Plan can define:

```text
Due - 7 days
Due + 3 days
```

---

# 144. Preventive Compliance

Measure:

```text
Completed within window
Late
Missed
```

---

# 145. Internal Downtime

First-class for company assets.

---

# 146. Downtime Definition

```text
Asset unavailable or impaired for operation
```

not Work Order duration.

---

# 147. Downtime Record

```text
AssetRef
Start
End?
Reason
Planned/Unplanned
ProductionImpact?
WorkOrderRef?
```

---

# 148. Work Can Happen Without Downtime

Inspection while machine remains operational.

---

# 149. Downtime Can Exist Before Work Order

Failure detected, machine stopped, work order later.

---

# 150. Availability Metrics

Potential:

```text
Downtime Hours
Availability %
Failure Count
MTBF
MTTR
```

---

# 151. MTBF

Definition documented by company metric policy.

---

# 152. MTTR

Definition documented; avoid mixing calendar elapsed vs active repair time.

---

# 153. Failure Count

Based on qualifying corrective failure Work Orders.

---

# 154. Planned vs Unplanned Maintenance

Important reporting dimension.

---

# 155. Corrective Maintenance

Triggered by failure/problem.

---

# 156. Preventive Maintenance

Triggered by plan before failure.

---

# 157. Inspection

May result in no repair or follow-up.

---

# 158. Calibration

Typed service type with measurements/certificate seam.

---

# 159. Installation

Customer equipment installation/commissioning service.

---

# 160. Commissioning

Can activate Installed Base and warranty/service start context.

---

# 161. Decommissioning

Can remove Installed Base item from active service.

---

# 162. Workshop / Depot Repair

Architecture must support off-site repair.

---

# 163. Work Location Type

```text
CustomerSite
CompanySite
CompanyWorkshop
Remote
VendorSite
Other
```

---

# 164. Depot Repair Flow

Potential:

```text
Customer Equipment
→ Receive Device/Component
→ Workshop Work Order
→ Parts/Labor
→ Test
→ Return
```

---

# 165. Depot Receipt

Inventory/Service Logistics integration decides physical custody.

Do not build duplicate stock ledger.

---

# 166. Removed Component

Can become:

```text
ReturnToWarehouse
ReturnToVendor
WorkshopRepair
Scrap
WarrantyClaim
```

---

# 167. Removed Component != Automatically Scrapped

Locked.

---

# 168. Service Parts Requirement

Work Order can state expected required part.

---

# 169. Required Part

Planning demand only.

---

# 170. Reservation

Inventory-owned.

---

# 171. Issue

Inventory-owned physical movement.

---

# 172. Consumption

Confirmed used in work.

Inventory-owned stock effect; Service owns work relationship.

---

# 173. Return

Unused/removed stock returns through Inventory.

---

# 174. Service Part Requirement Concept

```text
WorkPartRequirement
{
    WorkOrderId
    ProductVariantRef
    RequiredQty
    Uom
    NeedBy
    PreferredSourceLocation?
    StateProjection
}
```

---

# 175. Part State Projection

```text
Required
Reserved
PartiallyReserved
Issued
Consumed
Returned
Unavailable
Ordered
```

from Inventory/Purchasing truth.

---

# 176. Technician Van

Model as Inventory Location/Warehouse configuration.

---

# 177. Van Stock

Inventory truth.

---

# 178. Technician Quick View

Service mobile can show:

```text
My Van Stock
```

through Inventory safe query.

---

# 179. Transfer To Van

Inventory Transfer.

---

# 180. Consume From Van

Inventory Issue/Consumption linked to Work Order.

---

# 181. Parts Availability Before Dispatch

Scheduling can consider:

```text
ready
partial
unavailable
```

but company can dispatch for diagnosis without all parts.

---

# 182. Missing Part

Can create:

```text
Purchase Request
Stock Transfer Request
Backorder/Follow-up Visit
```

---

# 183. Purchasing Boundary

Service requests procurement; Purchasing owns PO.

---

# 184. External Vendor Service

Work Order may need subcontract repair/service.

---

# 185. Vendor Service Requirement

Can generate Purchase Request with:

```text
WorkOrderRef
Service description
NeedBy
Budget/cost context
```

---

# 186. External Vendor Invoice

Invoicing AP owns actual supplier invoice.

Service can consume cost allocation.

---

# 187. Service Expense

Technician travel/hotel/fuel/parking through Expenses.

---

# 188. Expense Work Order Link

Expense can reference:

```text
WorkOrderId
VisitId?
ProjectId?
```

where relevant.

---

# 189. Do Not Create Mini Expense Inside Service

Locked.

---

# 190. Service Labor

Work execution time can produce service cost/billing source.

---

# 191. Service Labor Cost

Use privacy-safe cost rate/fact.

Salary remains Payroll/HR private.

---

# 192. Service Billing Rate

Separate from labor cost rate.

---

# 193. Labor Rate Snapshot

Historical work keeps exact applied rate/version.

---

# 194. Travel Billing

Can be:

```text
Included
Actual time
Fixed travel fee
Mileage
Distance band
Nonbillable
```

contract rule.

---

# 195. Mileage

Can use measured distance/provider later.

No hidden global pricing rule.

---

# 196. Service Entitlement

First-class resolver.

---

# 197. Entitlement Mission

> **Given this customer, equipment, service type, date, contract and warranty context, what service is covered, by whom, to what limit, and what remains billable?**

---

# 198. Entitlement Sources

Potential:

```text
Customer Warranty
Supplier Warranty
Service Contract
Goodwill Approval
Project Contract
Paid Ad-hoc Service
No Coverage
```

---

# 199. Coverage Is Line-Level

A Work Order can have:

```text
Labor covered
Part covered
Travel billable
Consumable excluded
```

---

# 200. Coverage Result

Concept:

```text
CoverageDecision
{
    WorkOrderId
    SourceType
    SourceRef?
    ChargeCategory
    CoverageType
    CoveredQty/Amount?
    CustomerLiability
    RecoverableFromThirdParty?
    RuleVersion
    Reason
}
```

---

# 201. Warranty

Product/equipment coverage.

---

# 202. Customer Warranty

Reduces customer liability according to terms.

---

# 203. Supplier Warranty

Potential recovery from supplier/manufacturer.

---

# 204. Customer Warranty != Supplier Warranty

Both can apply simultaneously.

---

# 205. Warranty Core Fields

```text
SubjectRef
Provider/Obligor
Start
End
Coverage Profile
Evidence
State
```

---

# 206. Warranty Start

Could derive from:

```text
sale
delivery
installation
commissioning
manual approved date
```

policy.

---

# 207. Warranty Coverage Profile

Can define:

```text
Labor
Parts
Travel
Consumables
Specific components
Excluded failure causes
Caps
```

---

# 208. Warranty Claim

Supplier recovery can become separate claim process.

Architecture seam now; full Claims capability later if needed.

---

# 209. Warranty Does Not Mean Free

Resolver decides exact covered lines.

---

# 210. Customer-Induced Damage

May be excluded by warranty.

Failure cause can affect coverage.

---

# 211. Warranty Rework

Repair itself may have service warranty.

Support relation/version later.

---

# 212. Service Contract

Commercial agreement for service coverage/recurrence/billing.

---

# 213. Service Contract Is Not Sales Invoice

It defines service entitlement/commercial terms.

Invoicing owns invoices.

---

# 214. Service Contract Concept

```text
ServiceContract
{
    Id
    LegalEntityId
    ContractHolderPartyId
    BillToPartyId?

    StartDate
    EndDate?

    State
    Currency

    CoveragePlanVersion
    SLAProfileVersion?
    BillingArrangementRef?

    SourceSalesRef?
}
```

---

# 215. Contract Covered Scope

Can target:

```text
Customer
Site
Installed Base Items
Product families
Service types
```

---

# 216. Contract Coverage

Examples:

```text
4 preventive visits/year
Unlimited labor
Parts up to cap
Emergency visits included
Travel excluded
Business-hours support
```

---

# 217. Contract Usage

Track:

```text
Included
Consumed
Remaining
Overage
```

where terms require.

---

# 218. Contract Usage Is Not Inventory

It is service entitlement consumption.

---

# 219. Service Contract Recurrence

Can generate Maintenance Dues/Work Orders.

---

# 220. Contract Billing Arrangement

Potential:

```text
UpfrontFixed
PeriodicFixed
PerVisit
Usage/Overage
AtCompletion
Mixed
```

Invoicing executes legal billing.



# 221. SLA

SLA defines service timing commitments.

---

# 222. SLA != Priority

Priority is urgency/order of work.

SLA is contractual/operational promise.

---

# 223. SLA != Asset Criticality

Criticality describes impact of asset failure.

---

# 224. SLA Clock Types

Potential:

```text
Response
Dispatch
Arrival
Resolution
RestoreService
```

Enable only needed clocks.

---

# 225. SLA Profile

Concept:

```text
SlaProfileVersion
{
    Priority
    Criticality?
    ServiceType?
    BusinessCalendarId

    ResponseDuration?
    ArrivalDuration?
    ResolutionDuration?

    CutoffPolicy
    PausePolicy
}
```

---

# 226. Business Calendar

SLA must respect:

```text
working hours
weekends
holidays
24x7 contracts
```

from appropriate calendar source.

---

# 227. SLA Start

Can be:

```text
Request Created
Request Triaged
Work Order Created
Customer Contact Confirmed
```

according to contract/profile.

---

# 228. SLA Pause

Explicit.

---

# 229. Pause Reasons

Possible:

```text
WaitingForCustomer
NoSiteAccess
CustomerRequestedDelay
ForceMajeure
WaitingForThirdParty
```

Not all waiting states pause.

---

# 230. SLA Resume

Recorded event.

---

# 231. SLA Breach

Derived when clock deadline exceeded without qualifying completion.

---

# 232. SLA Breach History

Never disappears because work later completed.

---

# 233. SLA Warning

Examples:

```text
75%
90%
Due Soon
Breached
```

---

# 234. SLA Recalculation

Contract/profile change does not rewrite historical SLA clock after work starts unless explicit approved rule.

---

# 235. Priority

Simple values:

```text
Low
Normal
High
Urgent
Emergency
```

configurable.

---

# 236. Criticality

Examples:

```text
Low
Operational
Critical
SafetyCritical
```

subject/category specific.

---

# 237. Priority Can Be Raised

History preserved.

---

# 238. SLA Can Be Contract-Derived

No need for user to select manually.

---

# 239. Service Location

First-class operational site.

---

# 240. Service Location Is Not Inventory Location

Different semantics.

---

# 241. Service Location Can Reference

```text
Directory address
Customer site
Branch
Fixed Asset location
Installed Base location
```

---

# 242. Site Access Instructions

Can include:

```text
Gate contact
Working hours
PPE
Parking
Access code reference
Safety requirements
```

sensitive fields permissioned.

---

# 243. Site Contact

Directory Contact reference.

---

# 244. Site Time Zone

Important for schedules/SLA.

---

# 245. Functional Location

For internal maintenance, future/optional hierarchy:

```text
Plant
Area
Line
Cell
Equipment Position
```

---

# 246. Functional Location != Fixed Asset

Position/place can exist without current asset.

---

# 247. Asset Move Within Functional Location

Fixed Assets owns asset location history; Service can reference operational context.

---

# 248. Safety

Work Template can require safety checklist.

---

# 249. Safety Permit Seam

Future:

```text
PermitToWork
LockoutTagout
ConfinedSpace
HotWork
```

Do not implement full EHS module here.

---

# 250. Safety Blocker

Work cannot start if required permit not satisfied.

---

# 251. Tool Requirement

Work can require special tool/equipment.

---

# 252. Tool Is Not Consumed Part

Separate resource requirement.

---

# 253. Calibration Tool Certification

Future seam.

---

# 254. Parts Cost

Inventory valuation is cost source.

---

# 255. Labor Cost

Privacy-safe labor rate/fact.

---

# 256. External Service Cost

Supplier invoice / Purchasing source.

---

# 257. Expense Cost

Expenses source.

---

# 258. Service Cost Fact

Service can own normalized execution-cost fact/reference for analytics.

---

# 259. Service Cost Fact Concept

```text
ServiceCostFact
{
    WorkOrderId
    VisitId?
    SourceType
    SourceId
    SourceLineId?

    CostCategory
    Quantity?
    Uom?

    OriginalAmount
    OriginalCurrency
    BaseAmount

    CostDate
    State
    CorrectionOf?
}
```

---

# 260. Cost Categories

```text
Labor
Travel
Part
ExternalService
Expense
OtherApproved
```

---

# 261. Cost Fact Is Not Journal Entry

Ledger/source module already accounts where relevant.

---

# 262. Avoid Double Accounting

Service analytics consumes cost; does not re-post Inventory/Expense/Supplier Invoice cost.

---

# 263. Maintenance Cost By Asset

Aggregate Work Order ServiceCostFacts for company asset.

---

# 264. Internal Maintenance Capital Improvement Candidate

When work materially improves asset:

```text
Maintenance Work
→ Capital Improvement Candidate
→ Fixed Assets Review
```

---

# 265. Service Cannot Increase Asset Book Value

Only Fixed Assets.

---

# 266. Customer Service Charge

First-class commercial object.

---

# 267. Service Charge Mission

Separates:

```text
work performed
```

from:

```text
legal invoice
```

---

# 268. Service Charge Sources

```text
Labor
Travel
Mileage
Part
External Vendor
Expense
Callout Fee
Fixed Service Fee
Contract Overage
Manual Approved Adjustment
```

---

# 269. Service Charge Concept

```text
ServiceCharge
{
    Id
    WorkOrderId
    VisitId?

    SourceType
    SourceId?
    SourceLineId?

    ChargeCategory

    Quantity?
    Uom?
    UnitPrice?
    BaseAmount

    CoverageDecisionRef
    CustomerBillableAmount
    RecoverableAmount?
    Currency

    BillingState
    InvoiceRef?
    CorrectionOf?
}
```

---

# 270. Charge Coverage States

```text
CustomerBillable
WarrantyCovered
ContractIncluded
SupplierRecoverable
Goodwill
InternalNonbillable
ProjectBillingOwned
```

---

# 271. Coverage Resolver Before Billing

Every charge line receives coverage decision.

---

# 272. Service Charge != Invoice

Locked.

---

# 273. Billing Owner

Work Order:

```text
Service
Projects
None
```

---

# 274. Service Billing Owner

`24` generates Service Charges and Invoice Request.

---

# 275. Projects Billing Owner

`24` sends execution/cost/billing-source facts to `23`.

`23` generates Project Billable Charge.

---

# 276. No Double Billing

Architecture/invariant test mandatory.

---

# 277. Internal Maintenance Billing Owner

Usually:

```text
None
```

---

# 278. Warranty Work

Customer billable can be zero while company cost remains nonzero.

---

# 279. Supplier Recoverable

Recovery process separate from customer invoice.

---

# 280. Goodwill

Authorized noncontractual customer waiver.

Requires reason/approval according to threshold.

---

# 281. Call-Out Fee

Can be fixed per visit/work order.

---

# 282. Minimum Charge

Contract/rate rule can apply.

---

# 283. Labor Billing

Possible:

```text
TimeAtRate
FlatRate
Included
Free
```

---

# 284. Part Billing

Possible:

```text
PriceList
ActualCostPlusMarkup
Included
Warranty
```

Pricing/Sales strategy may supply price.

---

# 285. Travel Billing

Separate category.

---

# 286. Expense Rebilling

Can be at cost/markup/fixed/nonbillable.

---

# 287. External Vendor Rebilling

Rule-controlled.

---

# 288. Service Billing Cap

Contract can limit:

```text
per Work Order
per Visit
per Contract Period
per Category
```

---

# 289. Overage

Beyond included/capped service can become billable.

---

# 290. Billing Cap != Cost Budget

Separate.

---

# 291. Billing Review

Optional:

```text
Technical Completion
→ Coverage Resolution
→ Charges Review
→ Invoice Request
```

---

# 292. Automatic Invoice Request

Can enable for trusted simple service types.

---

# 293. Invoicing Request

Send:

```text
BillTo
Customer
LegalEntity
Currency
Service Charges
WorkOrder/Visit refs
Service Report ref
```

---

# 294. Invoicing Owns

```text
Invoice Number
Tax
Legal snapshot
Posting
Credit Note
```

---

# 295. Service Tax

Tax/Invoicing calculates.

---

# 296. Invoice Allocation Back

Store:

```text
ChargeId
InvoiceId
InvoiceLineId
Amount
```

---

# 297. Partial Invoicing

Supported only if commercial rule allows.

---

# 298. Billing Correction

Use corrective charge + Invoicing correction.

---

# 299. Collection

Subledger/Payments.

---

# 300. Service Contract Revenue

Do not equate contract periodic invoice with one Work Order revenue automatically.

---

# 301. Contract Prepaid/Periodic Service

Coverage and billing arrangement may be decoupled from work execution.

---

# 302. Contract Utilization Reporting

Show:

```text
Visits Included/Used
Labor Included/Used
Parts Cap Used
Overages
```

where contract defines.

---

# 303. Customer Acceptance

Optional according to work type/contract.

---

# 304. Technical Completion

Technician says work execution complete.

---

# 305. Customer Acceptance

Customer confirms result/evidence.

---

# 306. Financial Close

Coverage/charges/costs resolved.

---

# 307. Work Order Close

Requires configured checklist.

---

# 308. Close Checklist

Potential:

```text
Technical work complete
Required checklist complete
Required photos present
Parts consumed/returned resolved
Time complete
Customer acceptance if required
Coverage resolved
Charges resolved
Follow-up created if required
```

---

# 309. Close With Exception

Authorized reason if company allows.

---

# 310. Closed Work Order

No ordinary mutation.

Corrections/follow-up semantic commands only.

---

# 311. Reopen

High-impact controlled.

---

# 312. Follow-Up Work Order

New linked Work Order.

Do not reopen all history by default.

---

# 313. Cancellation

Reason required.

Resolve:

```text
reservations
visits
parts requests
customer commitments
```

---

# 314. No-Access Visit

Visit can fail without cancelling Work Order.

---

# 315. Customer No-Show

Typed Visit completion reason.

---

# 316. Work Not Completed

Visit completes as:

```text
FollowUpRequired
```

Work Order stays open.

---

# 317. Parts Not Available

Visit may diagnose and create follow-up.

---

# 318. Service Recommendation

Technician can recommend:

```text
additional repair
replacement
upgrade
preventive plan
quotation
```

---

# 319. Recommendation != Approved Work

Can create:

```text
Service Request
Sales Opportunity/Quotation suggestion
Follow-up Work Order pending approval
```

---

# 320. Replacement Recommendation

Can feed Sales/CRM.

---

# 321. Product Sale → Installed Base

When Sales/Inventory confirms serialized product delivered/installed:

```text
Installed Base candidate
```

---

# 322. Installed Base Creation Policy

Options:

```text
Auto from serialized delivery
Create on installation/commissioning
Manual/import
```

---

# 323. Installation Work Order

Can activate Installed Base.

---

# 324. Warranty Activation

May trigger on installation/commissioning according to policy.

---

# 325. Installed Base Product Replacement

Old item:

```text
Replaced
```

new item:

```text
Installed/Active
```

with relation.

---

# 326. Service History

Per Installed Base / Fixed Asset:

```text
Requests
Work Orders
Visits
Failures
Parts
Measurements
Warranty actions
Costs
Service reports
```

---

# 327. Service History Is Composed

Do not copy source details into giant history blob.

---

# 328. Customer 360

CRM/Directory can show:

```text
Installed Base
Open Requests
Open Work Orders
Active Contracts
Warranty
Upcoming PM
Service History
```

from Service.

---

# 329. CRM Does Not Own Service

Read-only composition / actions through Service commands.

---

# 330. Sales Integration

Can create:

```text
Installation Work
Service Contract
Warranty context
Paid service order
```

from accepted sale.

---

# 331. Service Quote

If new paid work needs price approval:

```text
Service Recommendation
→ Sales Quotation
→ Accepted
→ Work Order authorized
```

---

# 332. Work Authorization

Can require:

```text
customer approval
internal approval
quotation acceptance
contract entitlement
```

---

# 333. Emergency Work

Policy can allow start before quotation with approval.

---

# 334. Project Integration

Work Order may execute part of Project.

---

# 335. Project Link

Can reference:

```text
ProjectId
TaskId?
```

---

# 336. Project Time Ownership

Avoid duplicate Service + Project time facts.

---

# 337. Time Ownership Rule

If Project-linked and Project is cost/billing owner:

```text
Service execution time
→ Projects Time/Cost integration
```

with one canonical finalized time source.

---

# 338. Standalone Service

Service owns finalized service labor time for cost/charges.

---

# 339. Project Billing Owner

Explicit field/policy.

---

# 340. Project Cost Integration

Service Parts/Labor/Expenses can feed Project cost via source facts.

---

# 341. Maintenance Project

Large overhaul can be Project + Work Orders.

Project handles plan/budget.

Service handles field/work execution.

---

# 342. Asset Maintenance History

Fixed Assets can query Work Orders.

---

# 343. Fixed Asset Disposal Decision

Service can recommend:

```text
Beyond Economic Repair
```

but Fixed Assets owns disposal.

---

# 344. Repair vs Replace

Service analytics can show:

```text
maintenance cost
downtime
failure frequency
asset NBV reference
```

to assist decision.

---

# 345. AI Recommendation

May suggest repair/replace.

Human/finance decides.

---

# 346. Mobile Product Goal

Technician sees only today's executable work.

---

# 347. Mobile Today View

```text
09:00 Generator Repair
11:30 AC Inspection
14:00 Elevator PM
```

---

# 348. Mobile Work View

Show:

```text
Customer/Site
Equipment
Problem
History summary
Instructions
Checklist
Parts
Contact
Navigation link
SLA/priority
```

---

# 349. Mobile Quick Actions

```text
Travel
Arrive
Start
Pause
Use Part
Add Photo
Record Reading
Complete
Signature
```

---

# 350. Mobile Must Not Be ERP Desktop Shrunk

Absolute.

---

# 351. Offline-Ready Architecture

Commands must support client-generated IDs.

---

# 352. Offline Command Queue

Potential client operations:

```text
Arrive
Start
Add Note
Checklist Answer
Meter Reading
Photo metadata
Part Consumption Intent
Complete
Signature
```

queued safely.

---

# 353. Offline Sync State

Each local operation:

```text
Pending
Synced
Conflict
Rejected
Retrying
```

---

# 354. Idempotency Offline

Every command stable key.

---

# 355. Client-Generated UUID

UUIDv7 acceptable.

---

# 356. Conflict Detection

Use row/version/operation semantics.

---

# 357. Do Not Last-Write-Wins Financial/Inventory Facts

Critical.

---

# 358. Part Consumption Offline

Can record local intent.

Inventory validates/reserves/posts on sync.

---

# 359. Offline Part Conflict

If stock unavailable centrally:

```text
Conflict/Exception
```

do not silently create negative stock unless Inventory policy allows.

---

# 360. Offline Completion

Can be locally completed pending sync.

Central Close happens after validation.

---

# 361. Offline Photos

Queued uploads with hashes/retry.

---

# 362. Offline Signature

Stored securely and synced with completion context.

---

# 363. Selective Sync

Technician device receives:

```text
assigned visits
necessary customer/site
equipment summary
templates/checklists
required parts
relevant history summary
```

not entire tenant database.

---

# 364. Sync Security

Revoked assignment/user must lose future access; local cached data retention policy required.

---

# 365. Offline Data Encryption

Mobile/local storage encrypted using platform/mobile standards.

---

# 366. Offline Scope

Full offline PWA/native decision can be implementation ADR.

Domain/API semantics support it Day 1.

---

# 367. Dispatch Board

Dispatcher view:

```text
Unscheduled Work
Technicians
Calendar
Map optional
SLA risks
Parts readiness
```

---

# 368. Simple Dispatcher

List + calendar enough Day 1.

---

# 369. Map

Helpful but not canonical.

---

# 370. Route Optimization

Later/evidence-driven.

---

# 371. Geolocation

Use only when business legitimately needs it.

No unnecessary continuous tracking.

---

# 372. Technician Location

If enabled:

```text
opt-in/company policy
work context
retention limits
access control
```

---

# 373. ETA

Can be calculated later from route provider.

---

# 374. Customer Notification

Examples:

```text
Visit scheduled
Technician on the way
Arrival window
Work completed
Service report ready
```

---

# 375. Notification Failure

Does not change Work Order state.

---

# 376. Reschedule

Preserve prior appointment history.

---

# 377. Customer Reschedule

Typed reason.

---

# 378. Technician Absence

Dispatcher can reassign.

HR Leave informs availability.

---

# 379. Resource Reassignment

Does not rewrite prior Visit assignment history.

---

# 380. Crew Change

History.

---

# 381. Work Order Priority Change

Audit/history.

---

# 382. Escalation

Can escalate:

```text
priority
manager notification
specialist assignment
customer communication
```

---

# 383. Escalation != SLA Breach

Can happen before breach.

---

# 384. Service Knowledge

Root Cause/Resolution history can later power Knowledge Base.

---

# 385. Knowledge Article

Future/shared knowledge capability.

Do not put wiki engine inside Service now.

---

# 386. Recommendations From History

AI/search can suggest:

```text
similar failures
likely parts
prior resolutions
```

with confidence.

---

# 387. AI Must Not Autonomously

```text
approve warranty coverage
waive customer charges
consume inventory
close work
sign customer acceptance
change SLA
create binding invoice
```

---

# 388. AI Can

```text
summarize history
extract serial/reading from photo
suggest failure code
suggest parts
draft service report
suggest technician
flag repeat failure
```

---

# 389. Photo OCR

Can suggest:

```text
serial
meter reading
model
```

human confirms if material.

---

# 390. Predictive Maintenance

Future.

Uses sensor/condition data.

Not required Day 1.

---

# 391. Condition Signal

Future source can create:

```text
Maintenance Due
Service Request
Inspection recommendation
```

not direct Work Order close/open without policy.

---

# 392. Service Analytics

Operational, financial and reliability views.

---

# 393. Core Operational KPIs

Potential:

```text
Open Requests
Open Work Orders
Unscheduled
Overdue
SLA At Risk
SLA Breached
First-Time Fix
Repeat Failure
Average Response
Average Resolution
Visit Completion
```

---

# 394. Maintenance KPIs

```text
Planned vs Unplanned
Preventive Compliance
Downtime
MTBF
MTTR
Maintenance Cost by Asset
Maintenance Cost by Category
```

---

# 395. Field Service KPIs

```text
Visits per Technician
Travel vs Work Time
First-Time Fix
Billable Utilization
Customer Acceptance
Service Revenue/Cost/Margin
```

---

# 396. KPI Definitions Must Be Versioned/Documented

Avoid ambiguous business dashboards.

---

# 397. First-Time Fix Definition

Company can choose:

```text
one Visit
no Rework within N days
```

with clear denominator.

---

# 398. MTTR Definition

Clarify:

```text
calendar elapsed
active repair duration
```

which one dashboard uses.

---

# 399. SLA Compliance Definition

By specific clock.

---

# 400. Customer Satisfaction Seam

Future simple CSAT after work.

---

# 401. Maintenance Cost Summary

Service cost only.

Asset financial value remains Fixed Assets.

---

# 402. Service Profitability

Standalone customer service:

```text
Service Charges / invoiced basis
-
Service Cost
```

label basis.

---

# 403. Contract Profitability

Potential:

```text
Contract billed/revenue basis
-
covered service cost
```

but accounting/revenue recognition basis must be explicit.

---

# 404. Do Not Call Contract Invoice Revenue Automatically

Accounting authority.

---

# 405. Service Margin View

Operational commercial margin only, labeled.

---

# 406. Cost/Revenue Currency

FinBase.

---

# 407. Multi-Currency

Preserve original and base/service currency snapshots.

---

# 408. Coverage Currency

Contract/warranty caps can have currency.

---

# 409. Tax

Invoicing/Tax owns customer tax.

---

# 410. Inventory Tax/Cost

Source module owns.

---

# 411. Schema

Recommended:

```text
business_service
```

---

# 412. ServiceDbContext

Own Service tables only.

---

# 413. Core Tables

Potential:

```text
service_request
work_order
work_order_request_link
work_order_subject

service_visit
work_assignment
service_resource_requirement

service_work_type
service_work_template
service_work_template_version
work_operation
work_checklist
work_checklist_answer

installed_base_item
installed_base_relation
installed_base_location_history
installed_base_component_event

failure_symptom
failure_mode
root_cause
resolution_code
work_diagnosis

maintenance_plan
maintenance_plan_version
maintenance_due
meter_definition
meter_reading

asset_downtime

work_part_requirement
work_part_projection

warranty
warranty_coverage_version
coverage_decision

service_contract
service_contract_version
service_contract_scope
service_contract_usage
sla_profile
sla_profile_version
sla_clock
sla_clock_event

service_cost_fact
service_charge
service_invoice_allocation

service_document
service_report
service_report_version

service_external_reference

service_request_projection
dispatch_projection
technician_today_projection
installed_base_service_history_projection
maintenance_due_projection
service_financial_summary_projection
```

---

# 414. Tables Service Must Not Own

```text
fixed_asset
product
inventory_balance
inventory_movement
employee
attendance
purchase_order
supplier_invoice
expense
project
invoice
payment
journal_entry
```

---

# 415. Installed Base Table Is Valid

Because customer/third-party service equipment is Service-owned, not company Fixed Asset.

---

# 416. Fixed Asset Link

Reference/contract only.

No ORM cross-module navigation.

---

# 417. Tenant Columns

All tenant-owned Service tables:

```text
tenant_id
```

---

# 418. RLS

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 419. UUIDv7

IDs.

---

# 420. Money

FinBase decimal, never float.



# 421. Quantity / UOM

Use Catalog/UOM semantics.

---

# 422. Duration

Exact duration/time type.

---

# 423. Dates

NodaTime.

Use:

```text
LocalDate
Instant
DateTimeZone
```

by semantic need.

---

# 424. Row Version

Mutable Work Orders/Visits/contracts use optimistic concurrency.

---

# 425. Finalized Facts

Closed Work Order cost/charge/history corrected semantically.

---

# 426. Effective Dating

Templates/contracts/warranty/SLA/coverage profiles versioned/effective-dated.

---

# 427. Hierarchy Cycle

Installed Base hierarchy cycle blocked.

---

# 428. Service Request Indexes

Likely:

```text
tenant/entity/state/requested_at
tenant/customer/state
tenant/subject/state
tenant/priority/state
```

---

# 429. Work Order Indexes

```text
tenant/entity/state/planned_start
tenant/customer/state
tenant/subject/state
tenant/work_type/state
tenant/project_ref
```

---

# 430. Visit Indexes

```text
tenant/resource/planned_start
tenant/work_order
tenant/state/planned_start
```

---

# 431. Installed Base Indexes

```text
tenant/customer/site
tenant/serial normalized
tenant/product_ref
tenant/parent
tenant/state
```

---

# 432. Meter Indexes

```text
subject/meter/reading_at desc
```

---

# 433. Maintenance Due Indexes

```text
tenant/due_date/state
tenant/subject/state
tenant/plan
```

---

# 434. Contract Indexes

```text
tenant/customer/state/date
tenant/scope_ref
```

---

# 435. SLA Indexes

```text
tenant/work_order/clock_type
deadline/state
```

---

# 436. Charge Indexes

```text
tenant/work_order/billing_state
source_type/source_id
invoice_ref
```

---

# 437. Cost Indexes

```text
work_order/date
subject/date
source_type/source_id
```

---

# 438. Partitioning

Do not partition Day 1.

Measure high-volume Visits/Meter Readings/Service history first.

---

# 439. PostgreSQL First

No OpenSearch required Day 1.

---

# 440. Redis

Not source of truth and not required Day 1.

---

# 441. Search

Search by:

```text
Request Number
Work Order Number
Customer
Site
Asset Tag
Installed Base Serial
Product
Technician
Contract
```

---

# 442. Arabic Search

Normalize Arabic names/locations where applicable.

---

# 443. Commands — Requests

Representative:

```text
CreateServiceRequest
TriageServiceRequest
LinkRequestToWorkOrder
ResolveServiceRequest
CloseServiceRequest
CancelServiceRequest
```

---

# 444. Commands — Work Orders

```text
CreateWorkOrder
CreateWorkOrderFromRequest
CreateWorkOrderFromMaintenanceDue
UpdateDraftWorkOrder
ReadyWorkOrderForScheduling
StartWorkOrder
PutWorkOrderWaiting
CompleteWorkOrderTechnically
CloseWorkOrder
CancelWorkOrder
ReopenWorkOrder
CreateFollowUpWorkOrder
```

---

# 445. Commands — Visits

```text
CreateVisit
ScheduleVisit
RescheduleVisit
DispatchVisit
StartTravel
RecordArrival
StartVisitWork
PauseVisitWork
ResumeVisitWork
CompleteVisit
CancelVisit
RecordNoAccess
```

---

# 446. Commands — Assignments

```text
AssignResource
AssignCrew
ReassignResource
RemoveAssignment
```

---

# 447. Commands — Installed Base

```text
CreateInstalledBaseItem
CreateInstalledBaseFromDelivery
ActivateInstalledBaseItem
MoveInstalledBaseItem
InstallComponent
RemoveComponent
ReplaceComponent
RetireInstalledBaseItem
```

---

# 448. Commands — Diagnostics

```text
RecordDiagnosis
RecordFailureMode
RecordRootCause
RecordResolution
CreateRework
```

---

# 449. Commands — Checklists

```text
ApplyWorkTemplate
RecordChecklistAnswer
RecordMeasurement
AttachWorkPhoto
```

---

# 450. Commands — Preventive

```text
CreateMaintenancePlan
CreateMaintenancePlanVersion
PublishMaintenancePlanVersion
GenerateMaintenanceForecast
ConvertMaintenanceDueToWorkOrder
SkipMaintenanceDue
PostponeMaintenanceDue
```

---

# 451. Commands — Meter

```text
RecordMeterReading
CorrectMeterReading
ResetMeter
```

---

# 452. Commands — Downtime

```text
StartAssetDowntime
EndAssetDowntime
CorrectAssetDowntime
```

---

# 453. Commands — Parts

Service commands should request Inventory action:

```text
AddPartRequirement
RequestPartReservation
RequestPartIssue
ConfirmPartConsumed
RequestPartReturn
```

Inventory owns movements.

---

# 454. Commands — Warranty / Contract

```text
CreateWarranty
CreateWarrantyCoverageVersion
CreateServiceContract
CreateServiceContractVersion
ActivateServiceContract
SuspendServiceContract
RenewServiceContract
ResolveServiceEntitlement
```

---

# 455. Commands — SLA

```text
StartSlaClock
PauseSlaClock
ResumeSlaClock
CompleteSlaClock
```

normally orchestrated by business transitions.

---

# 456. Commands — Charges

```text
GenerateServiceCharges
ReviewServiceCharge
ApplyCoverageDecision
WriteOffServiceCharge
RequestServiceInvoice
ApplyInvoiceResult
CorrectServiceCharge
```

---

# 457. No Generic `SetInvoiceId`

Use invoice allocation result.

---

# 458. No Generic `SetPartConsumed`

Use Inventory-confirmed movement/consumption.

---

# 459. No Generic `SetWarrantyCovered=true`

Use Coverage Resolver.

---

# 460. No Generic `SetSlaDueDate`

Use profile/clock engine.

---

# 461. No Generic `SetMaintenanceCost`

Derived from cost facts.

---

# 462. No Generic `SetAssetBookValue`

Forbidden.

---

# 463. Contracts

Recommended:

```text
Todir.Service.Contracts
```

---

# 464. Backend Namespaces

```text
Todir.Modules.Service.Domain
Todir.Modules.Service.Application
Todir.Modules.Service.Infrastructure
Todir.Service.Contracts
```

---

# 465. Internal Areas

Potential:

```text
Requests/
InstalledBase/
WorkOrders/
Visits/
Resources/
Templates/
Inspections/
Preventive/
Meters/
Downtime/
Parts/
Entitlements/
Warranty/
Contracts/
Sla/
Costs/
Billing/
MobileSync/
Reporting/
```

---

# 466. API Base

Recommended:

```text
/api/v1/service
```

---

# 467. Request APIs

```text
GET  /requests
POST /requests
GET  /requests/{id}
POST /requests/{id}/triage
POST /requests/{id}/work-orders
POST /requests/{id}/resolve
POST /requests/{id}/close
```

---

# 468. Work Order APIs

```text
GET  /work-orders
POST /work-orders
GET  /work-orders/{id}
PUT  /work-orders/{id}
POST /work-orders/{id}/ready
POST /work-orders/{id}/waiting
POST /work-orders/{id}/technical-complete
POST /work-orders/{id}/close
POST /work-orders/{id}/cancel
```

---

# 469. Visit APIs

```text
GET  /visits
POST /work-orders/{id}/visits
POST /visits/{id}/schedule
POST /visits/{id}/dispatch
POST /visits/{id}/travel
POST /visits/{id}/arrive
POST /visits/{id}/start
POST /visits/{id}/complete
```

---

# 470. Mobile APIs

Prefer dedicated compact resource shapes:

```text
GET /mobile/my-day
GET /mobile/visits/{id}
POST /mobile/commands
POST /mobile/sync
```

exact transport can evolve.

---

# 471. Installed Base APIs

```text
GET  /installed-base
POST /installed-base
GET  /installed-base/{id}
GET  /installed-base/{id}/history
POST /installed-base/{id}/components
POST /installed-base/{id}/replace-component
```

---

# 472. Preventive APIs

```text
GET  /maintenance-plans
POST /maintenance-plans
POST /maintenance-plans/{id}/versions
GET  /maintenance-due
POST /maintenance-due/{id}/create-work
```

---

# 473. Meter APIs

```text
GET  /subjects/{type}/{id}/meters
POST /subjects/{type}/{id}/meter-readings
```

---

# 474. Contract APIs

```text
GET  /contracts
POST /contracts
GET  /contracts/{id}
POST /contracts/{id}/versions
POST /contracts/{id}/activate
POST /contracts/{id}/renew
```

---

# 475. Entitlement API

```text
POST /entitlements/resolve
```

returns explainable coverage result.

---

# 476. Charge APIs

```text
GET  /work-orders/{id}/charges
POST /work-orders/{id}/charges/generate
POST /charges/{id}/write-off
POST /work-orders/{id}/invoice-request
```

---

# 477. Dispatch APIs

```text
GET /dispatch/unscheduled
GET /dispatch/resources
GET /dispatch/calendar
```

---

# 478. Service History APIs

```text
GET /subjects/{type}/{id}/service-history
```

permission-aware.

---

# 479. Error Codes

Stable examples:

```text
service.request_not_found
service.work_order_not_found
service.work_order_closed
service.work_order_not_ready
service.visit_not_found
service.visit_schedule_conflict
service.resource_unavailable
service.resource_skill_missing

service.installed_base_not_found
service.subject_invalid
service.component_cycle

service.template_missing
service.checklist_required
service.measurement_out_of_range

service.maintenance_due_not_found
service.meter_reading_invalid
service.meter_decreased_unexpectedly

service.part_not_available
service.part_not_reserved
service.part_consumption_conflict

service.coverage_not_resolved
service.warranty_not_applicable
service.contract_not_active
service.contract_limit_exceeded

service.sla_profile_missing
service.sla_pause_not_allowed

service.billing_owner_conflict
service.charge_already_invoiced
service.invoice_request_pending

service.project_billing_owner_required
service.permission_denied
service.concurrent_change
service.idempotency_conflict
```

---

# 480. Friendly Error — Part Missing

Show:

```text
Required: Filter X × 2
Available in van: 0
Available HQ: 6

Actions:
Request Transfer
Create Purchase Request
Continue Diagnosis
```

---

# 481. Friendly Error — Coverage

Show:

```text
Labor       Covered by Contract
Part        Warranty Covered
Travel      Customer Billable
```

not generic "partially covered".

---

# 482. Friendly Error — SLA

Show:

```text
Resolution SLA due in 1h 20m
Clock is running
```

and why.

---

# 483. Friendly Error — Close

Show exact blockers:

```text
2 required checklist answers missing
1 consumed part not synced
Customer acceptance required
Coverage not resolved
```

---

# 484. Permissions

Initial vocabulary:

```text
service.request.read
service.request.create
service.request.triage
service.request.resolve

service.work.read
service.work.create
service.work.plan
service.work.schedule
service.work.execute
service.work.complete
service.work.close
service.work.reopen

service.dispatch.read
service.dispatch.manage

service.installed_base.read
service.installed_base.manage

service.template.read
service.template.manage

service.maintenance_plan.read
service.maintenance_plan.manage
service.meter.read
service.meter.record

service.parts.read
service.parts.request
service.parts.consume

service.warranty.read
service.warranty.manage
service.contract.read
service.contract.manage
service.entitlement.override

service.sla.read
service.sla.manage

service.cost.read
service.cost_rate.read
service.billing.read
service.billing.manage
service.billing.writeoff
service.billing.request_invoice

service.documents.read
service.documents.manage
service.audit.read
service.export
service.settings.manage
```

---

# 485. Scope Model

Combine with:

```text
OwnAssignments
Team
Territory
Branch
LegalEntity
Customer
SpecificWorkOrder
AllAuthorized
```

---

# 486. Technician Scope

Technician normally sees:

```text
assigned Visits
required work context
relevant customer/site/equipment
parts/checklists
```

not all customer financials/contracts.

---

# 487. Dispatcher Scope

Can see schedules/resources and enough customer/service data.

---

# 488. Service Manager

Broader work/contract/SLA visibility.

---

# 489. Maintenance Manager

Internal asset maintenance access without customer billing if not needed.

---

# 490. Billing/Finance

Service charges/cost without dispatcher execution rights.

---

# 491. Cost Rate Privacy

Technician does not see cost rate by default.

---

# 492. Customer Contract Privacy

Technician can see only entitlement outcome/necessary instructions.

Not full contract prices.

---

# 493. Installed Base Security

Customer-specific assets protected by customer/project/territory scope.

---

# 494. RLS

Tenant isolation on every Service table.

---

# 495. Field-Level Redaction

Examples:

```text
contract price
cost rate
supplier recovery
sensitive site instructions
customer contacts
```

permission-aware.

---

# 496. File Security

Photos/service reports/warranty docs use signed short-lived Platform Files access.

---

# 497. Signature Security

Access restricted.

---

# 498. Offline Security

Cached device data minimized and encrypted.

---

# 499. Revoked Technician

Future sync denies access; device cleanup/revocation policy.

---

# 500. Audit Critical Actions

Capture:

```text
Request priority change
Work Order creation/cancel/close/reopen
Visit scheduling/reassignment
Checklist overrides
Meter corrections
Maintenance skip/postpone
Part consumption/return requests
Warranty/contract version changes
Entitlement override
SLA pause/resume override
Customer acceptance
Charge write-off
Invoice request
Offline conflict resolution
Large export
```

---

# 501. Audit vs Service History

Both.

Service history explains work.

Audit explains actor/permission/system action.

---

# 502. Performance Mission

Fast field/mobile interaction and scalable dispatch/service history.

---

# 503. Technician My Day Target

```text
p95 <= 100-250ms
```

server-side for cached/indexed read.

---

# 504. Work Order Open Target

Core data:

```text
p95 <= 150-350ms
```

without huge history/files.

---

# 505. Dispatch Board Target

```text
p95 <= 200-500ms
```

for bounded day/region/resource window.

---

# 506. Installed Base Search

```text
p95 <= 100-250ms
```

by serial/tag/customer.

---

# 507. Service History

Keyset pagination.

---

# 508. Maintenance Due Query

```text
p95 <= 150-300ms
```

indexed horizon filters.

---

# 509. SLA Monitor

No full Work Order scans.

Index active clocks/deadlines.

---

# 510. No N+1

Dispatch board cannot query each Visit separately for:

```text
customer
site
technician
SLA
parts
```

---

# 511. Large Scale Tests

Architecture stress:

```text
1M Installed Base Items
5M Work Orders
10M Visits
50M Meter Readings
100k active preventive schedules
50k technicians/resources
large service history
```

depending target enterprise tenant.

---

# 512. Meter Data

Operational meter readings can become high-volume.

IoT telemetry should not be dumped into Service relational core without retention/aggregation design.

---

# 513. IoT Later

Use external telemetry/time-series capability if volume justifies.

Service stores actionable readings/events.

---

# 514. Preventive Generation

Batch/set-based.

---

# 515. Contract Recurrence Generation

Idempotent.

---

# 516. SLA Timers

Do not create one OS timer per Work Order.

Use deadlines + scheduled query/job.

---

# 517. Background Jobs

Quartz/Worker:

```text
maintenance forecast
contract recurrence
SLA warning/escalation
service reminders
service report generation
projection rebuild
large exports
offline conflict/retry support
```

---

# 518. Notification

Platform Notifications.

---

# 519. Notification Examples

```text
Work assigned
Visit rescheduled
SLA at risk
Part available
Customer acceptance needed
Maintenance overdue
Contract expiring
Warranty expiring
```

---

# 520. Notification Failure

No state corruption.

---

# 521. Observability Metrics

Potential:

```text
service_request_created_total
service_work_order_cycle_duration
service_visit_start_delay
service_sla_breach_total
service_first_time_fix_rate
service_repeat_failure_total
service_maintenance_due_overdue
service_part_unavailable_total
service_offline_sync_conflict_total
service_charge_generation_duration
service_invoice_request_failure_total
service_installed_base_search_duration
```

No customer/work-order IDs as metric labels.

---

# 522. Tracing — Work Order Creation

```text
Load Request
Resolve Subject
Apply Template
Resolve Entitlement/SLA
Create Work
Create Requirements
Audit
Outbox
Commit
```

---

# 523. Tracing — Visit Completion

```text
Validate Assignment
Time
Checklist
Measurements
Parts
Diagnosis
Photos
Completion
Follow-up
Sync/Audit
```

---

# 524. Tracing — Coverage/Billing

```text
Load Contract/Warranty
Resolve Coverage
Calculate Charges
Validate Billing Owner
Request Invoice/Project Billing
```

---

# 525. Tracing — Preventive

```text
Plan Version
Due Calculation
Meter/Calendar
Forecast
Work Conversion
```

---

# 526. Logging

Never dump:

```text
customer signatures
site access secrets
full contract documents
PII
offline cache payload
```

unnecessarily.

---

# 527. Reports — Service Operations

```text
Open Service Requests
Open Work Orders
Unscheduled Work
Visits Today
Overdue Work
Waiting Work
Work By Type
Work By Customer
Work By Site
Work By Technician
```

---

# 528. Reports — SLA

```text
Response SLA
Arrival SLA
Resolution SLA
Breaches
At Risk
By Contract
By Priority
By Customer
```

---

# 529. Reports — Maintenance

```text
Preventive Due
Preventive Compliance
Corrective vs Preventive
Asset Downtime
MTBF
MTTR
Failure Frequency
Repeat Failures
Maintenance Cost by Asset
```

---

# 530. Reports — Installed Base

```text
Installed Equipment
Warranty Expiring
Contract Coverage
Service History
Component Replacements
Out Of Service
```

---

# 531. Reports — Parts

```text
Parts Required
Unavailable Parts
Consumed Parts
Returned Parts
Parts by Work Type
Repeat Part Failures
```

Inventory remains quantity/cost authority.

---

# 532. Reports — Technician

```text
Assigned Visits
Completed Visits
Travel Time
Work Time
First-Time Fix
SLA performance
```

avoid simplistic employee ranking without context.

---

# 533. Reports — Contract

```text
Contract Coverage
Usage
Overages
Upcoming PM
Contract Service Cost
Contract Billing summary
```

---

# 534. Reports — Warranty

```text
Warranty Work
Customer Warranty Cost
Supplier Recoverable
Warranty Expiry
Failure By Product/Model
```

---

# 535. Reports — Financial

```text
Service Cost
Service Charges
Billable vs Covered
Invoice Status
Operational Service Margin
```

basis clearly labeled.

---

# 536. Reports — Close Exceptions

Work Orders blocked from close and reason.

---

# 537. Drilldown

Every KPI to Work Order/Visit/source.

---

# 538. Migration

Support legacy maintenance/service data.

---

# 539. Import — Installed Base

Fields:

```text
Legacy Equipment ID
Customer
Owner
Site
Product/SKU optional
Serial
Parent
Install Date
Warranty
Status
```

---

# 540. Import — Company Asset Links

Match exact FixedAsset Number/ExternalRef.

Do not duplicate.

---

# 541. Import — Service Requests/Work Orders

Open work prioritized.

Historical work can be:

```text
Detailed
Summary
NoHistory
```

---

# 542. Import — Maintenance Plans

Preserve:

```text
next due
last completed
trigger type
meter threshold
```

---

# 543. Import — Meter Readings

Import enough history to establish next due and analytics if reliable.

---

# 544. Import — Contracts

Active/open contracts with scope/coverage.

---

# 545. Import — Warranty

Current effective coverage.

---

# 546. Import — Service History

Do not fabricate root cause/parts if legacy lacks them.

---

# 547. Migration Provenance

```text
SourceSystem
LegacyId
BatchId
ImportedAt
AsOfDate
```

---

# 548. Opening Work

Open Work Orders reconcile:

```text
state
technician
parts
customer
coverage
scheduled dates
```

---

# 549. No Fake Inventory Movement

Import service part history as historical reference if source stock movement absent.

Do not invent Inventory transactions.

---

# 550. No Fake Invoice

Billing history links migrated invoices from Invoicing.

---

# 551. Go-Live Boundary

After date:

normal Service + owning module integrations.

---

# 552. Setup Wizard — Internal Maintenance

Simple:

```text
1. Choose Fixed Asset categories to maintain
2. Define Work Types/Templates
3. Define Maintenance Team
4. Optional preventive plans
5. Go Live
```

---

# 553. Setup Wizard — Customer Service

Simple:

```text
1. Service Legal Entity
2. Service Work Types
3. Technicians/Teams
4. Customer Sites/Installed Base
5. Pricing/Billing owner
6. Warranty/Contract use?
7. SLA use?
8. Go Live
```

---

# 554. No Irrelevant Setup Blockers

No contracts? No contract setup required.

No warranty? Hide it.

No meter maintenance? Hide meters.

No field billing? Hide charges.

---

# 555. Small Internal Maintenance Defaults

```text
Request          Optional
Work Order       ON
One Technician   ON
Simple Calendar  ON
Parts            ON if Inventory
Preventive       Basic calendar
SLA              OFF
Contracts        OFF
Customer Billing OFF
Meters           Optional
Offline          Architecture-ready
```

---

# 556. Small Field Service Defaults

```text
Request          Optional
Work Order       ON
Visit            ON
Technician       ON
Installed Base   Optional but recommended
Parts            ON
Warranty         Simple
Contract         Optional
SLA              Optional
Billing          ON
Advanced Dispatch OFF
```

---

# 557. Enterprise Capabilities

Enable:

```text
Installed Base hierarchy
Crews
Skills/certifications
Territories
Multiple Visits
SLA
Service Contracts
Entitlements
Customer + Supplier Warranty
Meter PM
Maintenance Rounds
Downtime/Reliability
Van Stock
Depot Repair
Offline execution
Advanced dispatch
```

---

# 558. UX Principle — Technician

Minimum taps.

---

# 559. UX Principle — Dispatcher

Exceptions + calendar.

---

# 560. UX Principle — Maintenance Manager

Asset health/due work.



# 561. UX Principle — Service Manager

Coverage/SLA/billing exceptions.

---

# 562. Request Intake

User should enter minimum:

```text
Customer/Site or Asset
Problem/Need
Priority if not inferred
Preferred time optional
```

---

# 563. Request Defaults

Infer:

```text
Customer
Site
Installed Base
Contract
SLA
Contact
```

when known.

---

# 564. Work Order Creation

Apply template automatically after Work Type selection.

---

# 565. Dispatcher Should Not Rebuild Scope Manually

Template + Request context prefill.

---

# 566. Technician Should Not Re-enter Customer Data

Locked.

---

# 567. Technician Should Not Re-enter Product/Serial If Work Already Targets Equipment

Locked.

---

# 568. Technician Should Not Calculate Coverage

Coverage resolver/system.

---

# 569. Technician Can See Coverage Outcome

Only actionable:

```text
Covered
Customer approval required
Part chargeable
```

---

# 570. Customer-Facing Status

Simpler than internal states:

```text
Request Received
Scheduled
Technician On The Way
In Service
Waiting
Completed
Closed
```

---

# 571. Customer Does Not See Internal Notes

Permission/content classification.

---

# 572. Customer Portal Later

Can show:

```text
Requests
Appointments
Equipment
Contracts
Service Reports
Invoices
```

narrow external access.

---

# 573. Business Scenario 1 — Internal Machine Failure

```text
Machine stops
→ Maintenance Request
→ Work Order
→ Downtime starts
→ Technician assigned
→ Diagnosis
→ Part consumed
→ Repair/test
→ Downtime ends
→ Work closes
→ Cost shown in Asset history
```

---

# 574. Scenario 1 Invariant

Repair cost does not change Asset Book value automatically.

---

# 575. Business Scenario 2 — Preventive Internal Maintenance

```text
Plan every 90 days
→ Maintenance Due
→ Work Order
→ Checklist
→ Measurements
→ Complete
→ Next Due
```

---

# 576. Scenario 2 Invariant

Schedule logic respects Fixed vs Last Completion policy.

---

# 577. Business Scenario 3 — Customer Ad-Hoc Repair

```text
Customer calls
→ Request
→ Work Order
→ Visit
→ Labor + Part
→ Coverage says no contract
→ Service Charge
→ Invoice
```

---

# 578. Business Scenario 4 — Warranty Repair

```text
Installed Base
→ Warranty active
→ Failure
→ Work
→ Labor covered
→ Part covered
→ Travel billable
```

---

# 579. Scenario 4 Invariant

Warranty does not zero every line automatically.

---

# 580. Business Scenario 5 — Service Contract PM

```text
Annual Contract
→ Quarterly maintenance
→ Due entries
→ Work Orders
→ Visits
→ Included usage
→ Overage if limits exceeded
```

---

# 581. Business Scenario 6 — Supplier Warranty

```text
Customer equipment fails
→ Customer warranty covers repair
→ Supplier warranty covers failed part
→ Customer pays 0 for part
→ Supplier recovery tracked
```

---

# 582. Business Scenario 7 — Two Visits

```text
Visit 1 diagnose
→ missing part
→ Purchase/Transfer
Visit 2 install/test
→ close
```

---

# 583. Scenario 7 Invariant

One Work Order, multiple Visits.

---

# 584. Business Scenario 8 — Crew

```text
Elevator repair
→ 2 technicians required
→ crew/2 assignments
→ one Visit
```

---

# 585. Business Scenario 9 — Maintenance Round

```text
20 factory machines
→ one inspection round
→ per-asset results
→ 2 follow-up repairs
```

---

# 586. Business Scenario 10 — Meter PM

```text
Vehicle
→ 49,800 KM
→ reading 50,100
→ 50,000 KM maintenance due
→ Work Order
```

---

# 587. Business Scenario 11 — Project-Linked Installation

```text
Project installs equipment
→ Service Work Orders execute visits
→ Service time/parts feed Project
→ Projects owns customer billing
```

---

# 588. Scenario 11 Invariant

Service must not create duplicate invoice charges.

---

# 589. Business Scenario 12 — Workshop Repair

```text
Customer component received
→ Workshop Work Order
→ part replaced
→ test
→ return to customer
```

---

# 590. Business Scenario 13 — Component Replacement

```text
Installed generator
→ old controller removed
→ new controller installed
→ component history updated
```

---

# 591. Business Scenario 14 — Offline Technician

```text
Visit assigned while online
→ loses connection
→ arrival/checklist/parts/photo/signature locally
→ reconnect
→ idempotent sync
→ central validation
```

---

# 592. Test — Request != Work Order

Request can exist unplanned.

---

# 593. Test — One Request Many Work Orders

Links exact.

---

# 594. Test — One Work Order Many Visits

History exact.

---

# 595. Test — Multiple Technicians

Assignments exact.

---

# 596. Test — Customer Equipment Not Fixed Asset

No Fixed Asset row required.

---

# 597. Test — Internal Fixed Asset

Service references existing Asset without copying.

---

# 598. Test — Installed Base Product Optional

Legacy equipment valid.

---

# 599. Test — Installed Base Hierarchy

No cycles.

---

# 600. Test — Component Replacement

Old/new component history exact.

---

# 601. Test — Owner / Customer / Bill-To Different

Workflow and invoice request correct.

---

# 602. Test — Work Template Version

Old Work Order retains old checklist.

---

# 603. Test — Required Checklist

Cannot technically complete when required answers missing.

---

# 604. Test — Measurement UOM

Mandatory where measurement type.

---

# 605. Test — Out-of-Range Measurement

Configured warning/block/follow-up exact.

---

# 606. Test — Reported vs Diagnosis

Distinct fields/history.

---

# 607. Test — Root Cause Analytics

Typed values searchable.

---

# 608. Test — Repeat Failure

Flag based on configured window without merging incidents.

---

# 609. Test — Preventive Fixed Calendar

Late completion does not incorrectly shift next due.

---

# 610. Test — From Last Completion

Next due shifts correctly.

---

# 611. Test — Meter Threshold

Correct due generation crossing threshold.

---

# 612. Test — Meter Decrease

Rejected or explicit reset/rollover.

---

# 613. Test — Maintenance Due

Forecast exists before Work Order.

---

# 614. Test — Preventive Generation Retry

No duplicate Work Order.

---

# 615. Test — Maintenance Round

Per-subject result preserved.

---

# 616. Test — Downtime

Can start before Work Order and end after repair.

---

# 617. Test — MTBF

Uses qualifying failure definition.

---

# 618. Test — MTTR

Uses configured duration basis.

---

# 619. Test — Required Part

Does not change stock.

---

# 620. Test — Reserve Part

Inventory is authority.

---

# 621. Test — Consume Part

One Inventory effect only.

---

# 622. Test — Offline Consume Retry

No double stock issue.

---

# 623. Test — Part Return

Inventory receives correct quantity.

---

# 624. Test — Van Stock

Same Inventory location truth.

---

# 625. Test — Missing Part

Creates follow-up/procurement without closing work falsely.

---

# 626. Test — Supplier Service

PO/invoice source cost appears once.

---

# 627. Test — Technician Expense

Expense source appears once.

---

# 628. Test — Labor Cost Privacy

Technician/dispatcher cannot see salary.

---

# 629. Test — Warranty Active

Resolver checks date + scope + failure/charge rule.

---

# 630. Test — Warranty Expired

No false coverage.

---

# 631. Test — Partial Warranty

Labor covered, travel billable.

---

# 632. Test — Customer + Supplier Warranty

Customer and supplier recovery separate.

---

# 633. Test — Contract Included Visit

Usage decremented exactly once.

---

# 634. Test — Contract Overage

Correct billable Service Charge.

---

# 635. Test — Contract Expiry

New work not covered after expiry absent renewal/grace rule.

---

# 636. Test — SLA Response

Correct business calendar.

---

# 637. Test — SLA Pause

Only allowed pause reasons stop clock.

---

# 638. Test — SLA Breach

Breach remains historical after completion.

---

# 639. Test — Priority Change

Does not silently rewrite original SLA promise unless rule explicitly recalculates.

---

# 640. Test — Technical Complete != Accepted

Works.

---

# 641. Test — Accepted != Invoiced

Works.

---

# 642. Test — Service Billing Owner

Generates charges/invoice request.

---

# 643. Test — Project Billing Owner

No Service invoice request; Project receives source facts.

---

# 644. Test — Billing Owner Conflict

Blocked.

---

# 645. Test — Warranty Charge

Customer billable zero but cost preserved.

---

# 646. Test — Goodwill

Requires authority/reason.

---

# 647. Test — Charge Generation Retry

No duplicate.

---

# 648. Test — Invoice Request Retry

No duplicate invoice.

---

# 649. Test — Invoice Correction

Service billing projection reconciles.

---

# 650. Test — Capital Improvement Candidate

Does not alter Fixed Asset until accepted by `22`.

---

# 651. Test — Work Order Closed

Ordinary edits rejected.

---

# 652. Test — Follow-Up

Creates linked new work.

---

# 653. Test — No Access Visit

Visit closes; Work Order remains actionable.

---

# 654. Test — Customer Reschedule

Prior schedule preserved.

---

# 655. Test — Resource Leave

Unavailable employee not scheduled without override.

---

# 656. Test — Crew

All required resource assignments valid.

---

# 657. Test — Offline Arrival

Sync idempotent.

---

# 658. Test — Offline Checklist Conflict

Conflict handled; no silent overwrite.

---

# 659. Test — Offline Completion With Missing Central Requirement

Central close rejected/actionable while local data preserved.

---

# 660. Test — Offline Photo Retry

One file/evidence relation.

---

# 661. Test — Offline Signature

Preserved and authorized.

---

# 662. Test — Cross Tenant RLS

No data leakage.

---

# 663. Test — Technician IDOR

Cannot access unassigned unauthorized Visit.

---

# 664. Test — Dispatcher Financial Redaction

No cost/contract price if not permitted.

---

# 665. Test — File Access

Signed URL scoped.

---

# 666. Test — Installed Base Search Scale

Measured.

---

# 667. Test — 1M Installed Base

Indexes/pagination.

---

# 668. Test — 5M Work Orders

Operational list stays performant.

---

# 669. Test — SLA Monitor Scale

Active deadlines efficiently checked.

---

# 670. Test — Preventive 100k Plans

Generation efficient/idempotent.

---

# 671. Test — 50M Meter Readings

Hot latest-reading path indexed; retention strategy reviewed.

---

# 672. Property Test — Part Flow

```text
Consumed <= Issued/available according to Inventory policy
```

Service cannot invent quantity.

---

# 673. Property Test — Contract Usage

```text
Consumed usage
<= Included + approved overage
```

for capped rules.

---

# 674. Property Test — Service Charge

Invoice allocations cannot exceed charge absent explicit adjustment.

---

# 675. Property Test — Installed Base

One active parent relationship, no cycles.

---

# 676. Property Test — SLA

Elapsed counted time + paused time explains deadline/result.

---

# 677. Property Test — Preventive

Each Due converts to at most one active generated Work Order unless explicit regeneration after cancellation.

---

# 678. Property Test — Work Close

Required completion conditions all satisfied or explicit exception exists.

---

# 679. Architecture Test — No Fixed Asset Copy

Service references Assets contracts.

---

# 680. Architecture Test — No Inventory Ledger

Service does not own stock balances/movements.

---

# 681. Architecture Test — No Employee Master

HR contract only.

---

# 682. Architecture Test — No Project Duplicate Billing

Billing owner invariant.

---

# 683. Architecture Test — No Invoice Table

Invoicing contract only.

---

# 684. Architecture Test — No Journal Entry

Ledger ownership.

---

# 685. Architecture Test — No Salary Read

Payroll internals forbidden.

---

# 686. Red-Team — Request = Work Order

Reject.

---

# 687. Red-Team — Work Order = Visit

Reject.

---

# 688. Red-Team — One Technician Field On Work Order Only

Reject architecture limitation.

---

# 689. Red-Team — Customer Equipment Stored In Fixed Assets

Reject by default.

---

# 690. Red-Team — Internal Asset Duplicated In Service Equipment

Reject.

---

# 691. Red-Team — CustomerId Means All Parties

Reject.

---

# 692. Red-Team — Warranty Active Means Free

Reject.

---

# 693. Red-Team — Service Contract Means Warranty

Reject.

---

# 694. Red-Team — SLA = Priority

Reject.

---

# 695. Red-Team — Waiting Always Pauses SLA

Reject.

---

# 696. Red-Team — Preventive Plan Creates Infinite Work Orders

Reject.

---

# 697. Red-Team — Repeat Every X = Last Completion Always

Reject universal assumption.

---

# 698. Red-Team — Meter Is Custom Number

Reject.

---

# 699. Red-Team — Checklist Is Notes

Reject for structured inspection.

---

# 700. Red-Team — Part Required Means Stock Consumed

Reject.

---

# 701. Red-Team — Work Order Has Its Own Inventory

Reject.

---

# 702. Red-Team — Van Stock In Service Table

Reject.

---

# 703. Red-Team — Maintenance Cost Adds Asset Value

Reject.

---

# 704. Red-Team — Completed Means Invoiced

Reject.

---

# 705. Red-Team — Technician Time = HR Attendance

Reject.

---

# 706. Red-Team — Project-linked Work Billed By Both Modules

Reject.

---

# 707. Red-Team — Offline Last-Write-Wins

Reject for critical data.

---

# 708. Red-Team — Customer Signature Closes Accounting Automatically

Reject.

---

# 709. Red-Team — Not Found Asset During Verification Means Dispose

Not Service-specific, but any maintenance exception cannot trigger financial disposal.

---

# 710. Competitive Benchmark — Microsoft Dynamics 365 Field Service

Current official patterns useful to TODIR include:

```text
Work Order separated from Bookable Resource Booking
Customer Assets
Incident Types / work templates
Agreements generating recurring Work Orders and invoices
Inventory / technician truck stock
Inspections
Mobile/offline execution
Service history
```

Official reference pages include:
- `Work order architecture - Dynamics 365 Field Service`
- `Customer agreements overview`
- `Build a service history for assets`
- Field Service inventory/purchasing/mobile/inspections documentation.

TODIR adopts:
- Work Order vs Visit/Booking separation;
- Installed Base service history;
- recurring service agreements;
- mobile/offline readiness;
- template-driven work.

TODIR avoids creating a second inventory engine because Inventory already exists as a core ERP module.

---

# 711. Competitive Benchmark — Oracle Maintenance / Service Logistics

Current Oracle service/maintenance flows demonstrate:

```text
Installed Base assets
Maintenance programs
Maintenance forecasts/work orders
Service Requests
Field Service Work Orders
Parts requirements
Debrief of parts/labor/expenses
Charges/invoices
Depot repair
```

Official references:
- `Overview of Preventive Maintenance Tasks for Customer Assets`
- `Create Service Requests and Field Service Work Orders`
- `How You Use Assets in Service Logistics`
- `Set Up Field Service Preventive Maintenance`

TODIR adopts the separation between:
- Installed Base;
- preventive maintenance;
- service request/work order;
- parts;
- execution/debrief;
- billing.

TODIR presents this through one simpler product experience.

---

# 712. Competitive Benchmark — IFS Cloud

IFS is a strong reference for deep service business:

```text
Service Contracts
SLA
Equipment/objects
Customer Warranty
Supplier Warranty
Work Tasks
Work Assignments
Material
Mobile online/offline
Cost/revenue
Maintenance execution
```

TODIR adopts:
- line-level entitlement logic;
- distinction between customer and supplier warranty;
- SLA clocks/calendars;
- assignment-based execution;
- offline-ready work.

TODIR avoids requiring IFS-level setup for small service companies.

---

# 713. Competitive Benchmark — Salesforce Field Service

Useful patterns:

```text
Assets / Installed Products
Asset hierarchy
Work Orders
Service Appointments
Service Resources
Crews
Territories
Skills
Maintenance Plans
Mobile/offline
```

TODIR adopts:
- Installed Base hierarchy;
- Appointment/Visit separate from Work Order;
- resource/crew scheduling foundation;
- maintenance-plan and field execution patterns.

---

# 714. Competitive Benchmark — SAP Field Service and Asset Management

Useful enterprise patterns:

```text
planning/dispatch
resource filters
skills
customer/equipment/location context
crews
asset maintenance alignment
```

TODIR keeps the scheduling model extensible while launching with manual/simple scheduling first.

---

# 715. Competitive Benchmark — Acumatica Field Service

Acumatica is especially useful for mid-market simplicity:

```text
Service Order
Appointment
Equipment
Contracts
Scheduling
Billing
Internal/regular service scenarios
```

TODIR adopts:
- request/order/appointment separation;
- practical mid-market workflows;
- optional contract/billing depth.

---

# 716. Competitive Benchmark — Odoo 19

Odoo provides a useful simplicity reference:

```text
Corrective/Preventive Maintenance Requests
Equipment
Maintenance teams/calendar
Field Service tasks
Worksheets
Products used in service
Default technician warehouse
```

Current Odoo documentation also shows Field Service products using Inventory/warehouse stock rather than a totally separate stock concept.

TODIR adopts:
- simple service execution UX;
- reusable worksheets/templates;
- direct part-use actions backed by Inventory.

TODIR adds stronger:
- Request/Work/Visit separation;
- Installed Base;
- entitlement/warranty;
- SLA;
- meter schedules;
- contract usage;
- offline invariants;
- billing ownership.

---

# 717. Competitive Product Rule

Competitor feature existence proves a business need.

TODIR does not copy competitor screen complexity.

---

# 718. Competitive Product Target

> **Odoo/Acumatica-like simplicity + Dynamics/IFS/Oracle/Salesforce-grade business correctness.**

---

# 719. Agent Implementation Philosophy

Business execution first.

Do not begin with a giant Work Order form.

---

# 720. Implementation Sequence

```text
SV00 Repository / Dependency Verification
SV01 Module Boundary + Schema + RLS
SV02 Work Types / Template Foundation
SV03 Internal / Customer Context Core
SV04 Installed Base
SV05 Service Requests
SV06 Work Orders
SV07 Visits / Assignments
SV08 Technician Mobile Read Models
SV09 Checklists / Measurements / Photos
SV10 Diagnosis / Failure / Resolution
SV11 Inventory Part Requirements
SV12 Part Reservation / Issue / Consumption / Return
SV13 Labor Time / Cost
SV14 Service Cost Facts
SV15 Warranty Core
SV16 Service Contracts
SV17 Entitlement Resolver
SV18 SLA Engine
SV19 Service Charges
SV20 Invoicing Integration
SV21 Projects Billing Ownership Integration
SV22 Preventive Maintenance Plans
SV23 Maintenance Due / Forecast
SV24 Meter Readings / Meter PM
SV25 Maintenance Rounds
SV26 Downtime / Reliability
SV27 Installed Base Component Replacement
SV28 Supplier Warranty / Recovery Seam
SV29 External Vendor / Purchasing Integration
SV30 Expenses Integration
SV31 Fixed Asset Maintenance History / Capital Improvement
SV32 Service Report / Customer Acceptance
SV33 Dispatch Board / Resource Requirements
SV34 Crew / Skills / Territories
SV35 Offline-Ready Command / Sync Foundation
SV36 Depot / Workshop Repair Seam
SV37 Notifications / Customer Communication
SV38 Reports / Analytics
SV39 Migration
SV40 Security / Privacy / Offline Security
SV41 Performance / Scale
SV42 Runbooks / Recovery
SV43 Internal Maintenance Acceptance
SV44 Field Technician Acceptance
SV45 Dispatcher Acceptance
SV46 Service Manager / Contract Acceptance
SV47 Finance / Billing Acceptance
SV48 Cross-Module Reconciliation
SV49 Offline Acceptance
SV50 Cross-Module E2E
SV51 Release Gate
```

---

# 721. SV00 Goal

Inspect actual contracts before coding.

---

# 722. SV00 Must Verify

```text
Assets queries
Catalog Product/UOM
Inventory reservation/issue/return
HR Employee/availability
Purchasing request
Expenses WorkOrder dimension possibility
Projects source/billing ownership
Invoicing invoice request
Subledger/Payments
Files
Approvals
Numbering
Audit
Jobs
Outbox/Inbox
RLS
```

---

# 723. SV00 Exit Gate

No mini Inventory/HR/Projects/Invoicing planned.

---

# 724. SV01 Goal

Create isolated module.

---

# 725. SV01 Build

```text
Todir.Modules.Service.Domain
Todir.Modules.Service.Application
Todir.Modules.Service.Infrastructure
Todir.Service.Contracts
business_service
ServiceDbContext
module manifest
RLS
architecture tests
```

---

# 726. SV01 Exit Gate

Boundaries enforced.

---

# 727. SV02 Goal

Work Template/version foundation.

---

# 728. SV02 Exit Gate

Repeatable work does not rely on copied free text.

---

# 729. SV03 Goal

Shared execution with separate Internal/Customer contexts.

---

# 730. SV03 Exit Gate

Internal work has no forced Customer/Invoice fields.

---

# 731. SV04 Goal

Installed Base.

---

# 732. SV04 Exit Gate

Customer equipment/service history works without Fixed Asset duplication.

---

# 733. SV05 Goal

Service Request intake/triage.

---

# 734. SV05 Exit Gate

Request exists independently from Work Order.

---

# 735. SV06 Goal

Work Order execution scope.

---

# 736. SV06 Exit Gate

One Work Order supports multiple Visits.

---

# 737. SV07 Goal

Scheduling/assignment foundation.

---

# 738. SV07 Exit Gate

Technician is assignment, not Work Order field assumption.

---

# 739. SV08 Goal

Fast mobile Today/work context.

---

# 740. SV08 Exit Gate

Technician does not load full ERP graph.



# 741. SV09 Goal

Structured inspections/checklists.

---

# 742. SV09 Build

```text
Question types
Measurements
UOM
Required answers
Photos
Template snapshot
```

---

# 743. SV09 Exit Gate

No inspection truth hidden in notes.

---

# 744. SV10 Goal

Failure/diagnosis/resolution history.

---

# 745. SV10 Exit Gate

Repeat failures/root causes measurable.

---

# 746. SV11 Goal

Parts planning without stock mutation.

---

# 747. SV11 Exit Gate

Required Parts exist independently from Inventory Reservation.

---

# 748. SV12 Goal

Full part execution through Inventory.

---

# 749. SV12 Exit Gate

Required/Reserved/Issued/Consumed/Returned remain distinct.

---

# 750. SV13 Goal

Service time/cost without Attendance/salary leakage.

---

# 751. SV13 Exit Gate

Travel/Work separate; HR Attendance untouched.

---

# 752. SV14 Goal

Rebuildable Service Cost Facts.

---

# 753. SV14 Exit Gate

Maintenance/service cost drills to canonical sources.

---

# 754. SV15 Goal

Customer Warranty.

---

# 755. SV15 Build

```text
Warranty
Coverage version
Subject scope
Labor/Parts/Travel rules
```

---

# 756. SV15 Exit Gate

Warranty Active does not mean all charges zero.

---

# 757. SV16 Goal

Service Contract.

---

# 758. SV16 Build

```text
Scope
Dates
Coverage
Included usage
Recurrence
Billing arrangement seam
SLA ref
```

---

# 759. SV16 Exit Gate

Contract independent from Invoice.

---

# 760. SV17 Goal

Entitlement Resolver.

---

# 761. SV17 Build

Resolve per charge/source:

```text
Customer Warranty
Supplier Warranty
Contract
Paid
Goodwill
Project
```

---

# 762. SV17 Exit Gate

Coverage outcome explainable line-by-line.

---

# 763. SV18 Goal

SLA Clock Engine.

---

# 764. SV18 Build

```text
Response
Arrival
Resolution
Calendars
Pause/Resume
Warnings/Breach
```

---

# 765. SV18 Exit Gate

Waiting state does not arbitrarily pause clock.

---

# 766. SV19 Goal

Service Charge Engine.

---

# 767. SV19 Build

```text
Labor
Parts
Travel
Fees
External service
Expense
Coverage
Caps
Write-off
```

---

# 768. SV19 Exit Gate

Cost and customer liability remain distinct.

---

# 769. SV20 Goal

Invoicing integration.

---

# 770. SV20 Exit Gate

Service never becomes legal Invoice owner.

---

# 771. SV21 Goal

Project Billing ownership.

---

# 772. SV21 Exit Gate

Project-linked Work is billed once by selected owner.

---

# 773. SV22 Goal

Preventive Maintenance Plans.

---

# 774. SV22 Exit Gate

Fixed vs floating schedule semantics correct.

---

# 775. SV23 Goal

Forecast/Due layer.

---

# 776. SV23 Exit Gate

Maintenance Due exists before Work Order and retry-safe conversion.

---

# 777. SV24 Goal

Meter-based maintenance.

---

# 778. SV24 Exit Gate

Threshold/reset/reading history exact.

---

# 779. SV25 Goal

Maintenance Rounds.

---

# 780. SV25 Exit Gate

One execution grouping supports per-subject result/follow-up.

---

# 781. SV26 Goal

Downtime/reliability.

---

# 782. SV26 Exit Gate

Downtime differs from Visit duration; MTBF/MTTR definitions documented.

---

# 783. SV27 Goal

Installed Base replacement history.

---

# 784. SV27 Exit Gate

Serialized component install/remove trace complete.

---

# 785. SV28 Goal

Supplier Warranty recovery seam.

---

# 786. SV28 Exit Gate

Supplier recovery does not alter customer coverage truth.

---

# 787. SV29 Goal

External Vendor/Purchasing integration.

---

# 788. SV29 Exit Gate

No mini-PO inside Service.

---

# 789. SV30 Goal

Expenses integration.

---

# 790. SV30 Exit Gate

Technician expense remains Expenses-owned.

---

# 791. SV31 Goal

Fixed Assets integration.

---

# 792. SV31 Build

```text
Maintenance History query
Cost summary
Capital Improvement Candidate
```

---

# 793. SV31 Exit Gate

Service never changes Asset Book Value.

---

# 794. SV32 Goal

Service Report / Customer Acceptance.

---

# 795. SV32 Exit Gate

Technical Complete, Accepted, Closed remain distinct.

---

# 796. SV33 Goal

Dispatcher productivity.

---

# 797. SV33 Build

```text
Unscheduled work
Calendar
resource requirements
SLA risk
parts readiness
```

---

# 798. SV33 Exit Gate

Simple scheduling fast without optimization engine.

---

# 799. SV34 Goal

Enterprise resources.

---

# 800. SV34 Build

```text
Crew
Skills
Certifications
Territories
```

---

# 801. SV34 Exit Gate

Simple one-technician company unaffected.

---

# 802. SV35 Goal

Offline-ready command/sync foundation.

---

# 803. SV35 Build

```text
client IDs
idempotency
sync state
conflicts
attachment retry
selective sync
```

---

# 804. SV35 Exit Gate

No last-write-wins for critical facts.

---

# 805. SV36 Goal

Workshop/depot repair seam.

---

# 806. SV36 Exit Gate

Off-site repair possible without inventing stock ownership.

---

# 807. SV37 Goal

Useful communications.

---

# 808. SV37 Exit Gate

Notification/customer communication failure does not corrupt work state.

---

# 809. SV38 Goal

Operational/maintenance/contract/financial reporting.

---

# 810. SV38 Exit Gate

Every KPI has documented definition and drilldown.

---

# 811. SV39 Goal

Migration.

---

# 812. SV39 Exit Gate

Installed Base/open Work/Plans/Contracts reconcile and preserve provenance.

---

# 813. SV40 Goal

Security/privacy/offline hardening.

---

# 814. SV40 Attack Cases

```text
Cross-tenant service read
Technician accesses unassigned customer
Forged InstalledBaseId
Forged FixedAssetId
Forged Inventory part movement
Forged Customer acceptance
Unauthorized warranty override
Unauthorized SLA pause
Unauthorized charge write-off
Project/Service double billing
Offline replay
Offline duplicate consumption
Stolen device cache exposure
Signed file URL leakage
```

---

# 815. SV40 Exit Gate

RLS + authorization + offline controls pass.

---

# 816. SV41 Goal

Performance and scale.

---

# 817. SV41 Load Tests

```text
1M Installed Base
5M Work Orders
10M Visits
50M Meter Readings
100k preventive plans
50k resources
large dispatch day
large SLA monitor
large service history
offline reconnect burst
```

---

# 818. SV41 Exit Gate

Measured targets pass or ADR.

---

# 819. SV42 Goal

Semantic recovery without DB surgery.

---

# 820. SV42 Runbooks

Required:

```text
Duplicate Request
Duplicate generated Work Order
Wrong technician assignment
Visit sync conflict
Checklist conflict
Part consumption rejected
Inventory unavailable after offline work
Warranty misclassification
Contract usage mismatch
SLA clock wrong/paused incorrectly
Charge calculation error
Invoice request unknown/failure
Project billing ownership conflict
Preventive plan duplication
Meter bad reading
Installed Base component replacement correction
Projection rebuild
DR restore validation
```

---

# 821. SV42 Forbidden Runbook

Never normal fix:

```text
UPDATE work_order SET state='Closed'
UPDATE service_charge SET customer_amount=0
UPDATE meter_reading SET value=...
DELETE consumed_part...
UPDATE installed_base SET serial=...
```

without semantic command/correction.

---

# 822. SV43 Internal Maintenance Acceptance

Reviewer:

```text
Maintenance Manager / Plant Engineer
```

---

# 823. SV43 Scenario A — Breakdown

```text
Asset failure
→ Request/Work
→ Downtime
→ Technician
→ Part
→ Diagnosis
→ Repair
→ Test
→ Close
```

---

# 824. SV43 Scenario B — Preventive

```text
Plan
→ Due
→ Work
→ Checklist
→ Next due
```

---

# 825. SV43 Scenario C — Meter

Threshold maintenance.

---

# 826. SV43 Pass Criteria

```text
no duplicate Asset master
parts reconcile
downtime correct
history complete
simple daily use
```

---

# 827. SV44 Field Technician Acceptance

Use real technician persona.

---

# 828. SV44 Scenario

```text
Open Today
Travel
Arrive
See equipment/history
Diagnose
Use part
Checklist
Photo
Complete
Customer signature
```

---

# 829. SV44 Pass Criteria

```text
minimal taps
mobile readable
no accounting jargon
no duplicate data entry
clear missing part workflow
offline-safe behavior
```

---

# 830. SV45 Dispatcher Acceptance

Scenario:

```text
20 unscheduled jobs
10 technicians
skills/availability
SLA risk
parts readiness
reschedule/reassign
```

---

# 831. SV45 Pass Criteria

Dispatcher can plan day without spreadsheet.

---

# 832. SV46 Service Manager / Contract Acceptance

Scenario:

```text
Contract customer
quarterly PM
included labor
parts cap
urgent repair
SLA
overage
```

---

# 833. SV46 Pass Criteria

Coverage/usage/SLA explainable.

---

# 834. SV47 Finance / Billing Acceptance

Scenario:

```text
Completed customer work
labor + part + travel
warranty/contract split
service charges
invoice request
invoice/payment
margin
```

---

# 835. SV47 Pass Criteria

No duplicate invoice/cost, every charge explainable.

---

# 836. SV48 Cross-Module Reconciliation

Validate:

```text
Service part consumption
= Inventory work-order-linked consumption

External service cost
= Purchasing/Invoicing allocation

Technician expenses
= Expenses allocation

Project-linked service cost
= Projects source facts

Service invoices
= Invoicing charge allocations

Collections
= Subledger/Payments

Internal asset maintenance
= Fixed Assets service history projection
```

---

# 837. SV48 Exit Gate

Zero unexplained differences.

---

# 838. SV49 Offline Acceptance

Test:

```text
Technician downloads assigned day
loses network
travels/arrives
records checklist/readings
uses part intent
takes photos
gets signature
completes
reconnects
syncs
```

---

# 839. SV49 Conflict Scenario

Central dispatcher reassigned/cancelled Work while technician offline.

System must:

```text
preserve local evidence
detect conflict
prevent false close/duplicate work
route to human resolution
```

---

# 840. SV49 Exit Gate

No lost work and no silent overwrite/double stock event.

---

# 841. SV50 E2E — Customer Repair

```text
CRM/Customer
→ Request
→ Work Order
→ Visit
→ Installed Base
→ Part Inventory
→ Technician time
→ Coverage
→ Charges
→ Invoice
→ Payment
→ Service History
```

---

# 842. SV50 E2E — Internal Maintenance

```text
Fixed Asset
→ Preventive Due
→ Work Order
→ Technician
→ Inventory Parts
→ Downtime
→ Cost
→ Asset History
```

---

# 843. SV50 E2E — Contract Preventive

```text
Service Contract
→ Maintenance Plan
→ Due
→ Work
→ Included usage
→ Overage if any
→ Contract reporting
```

---

# 844. SV50 E2E — Warranty

```text
Installed Base
→ Failure
→ Customer Warranty
→ Supplier Warranty
→ Work
→ Customer liability
→ Supplier recovery seam
```

---

# 845. SV50 E2E — Project Work

```text
Project
→ Service Work Order
→ Visits/Parts/Time
→ Project Cost/Billing source
→ Project Invoice
```

with one billing owner.

---

# 846. SV50 Exit Gate

All ownership boundaries pass in real integration tests.

---

# 847. SV51 Release Gate

Service does not ship because Work Order CRUD exists.

It ships when service demand, execution, parts, coverage, billing and history are trustworthy together.

---

# 848. SV51 Required Sign-Off

```text
Product Architect
Service Manager
Maintenance Manager
Field Technician Reviewer
Dispatcher Reviewer
Inventory Lead
Fixed Assets Lead
Projects Lead
HR Reviewer
Finance/Billing Reviewer
Security Reviewer
Offline/Mobile Reviewer
Performance Engineer
QA Lead
```

---

# 849. SV51 Mandatory Evidence

```text
Automated tests
Architecture boundary tests
RLS/security report
Offline conflict/idempotency tests
Inventory reconciliation
Cost reconciliation
Coverage/warranty vectors
SLA vectors
Preventive schedule vectors
Meter vectors
Billing ownership tests
Service Charge / Invoice reconciliation
Mobile technician acceptance
Dispatcher acceptance
Maintenance acceptance
Contract/billing acceptance
Performance benchmarks
Known limitations
```

---

# 850. Build Now — MUST

```text
Shared Work Execution Core
Internal Maintenance
Customer Field Service
Service Requests
Work Orders
Visits
Assignments
Installed Base
Installed Base hierarchy
Work Templates
Checklists/Measurements
Diagnosis/Root Cause/Resolution
Parts requirements
Inventory reservation/issue/consume/return integration
Labor time/cost
Warranty
Service Contracts
Entitlement Resolver
SLA foundation
Service Charges
Invoicing integration
Project billing ownership
Preventive Plans
Maintenance Due
Meter foundation
Maintenance Rounds
Downtime/reliability
Component replacement
Service Report
Customer acceptance
Dispatch
Offline-ready APIs/sync semantics
Fixed Asset history integration
Permissions/RLS/Audit
Reports
Migration
Performance
Runbooks
Acceptance gates
```

---

# 851. Build Later / Evidence-Based

```text
Advanced route optimization
Continuous GPS tracking
IoT predictive maintenance
Advanced spare-parts optimization
Full depot-repair logistics
Full supplier warranty claims
Advanced EHS / Permit-to-Work
Advanced fleet maintenance
AI autonomous dispatch
AI autonomous warranty decisions
Complex global service-contract revenue recognition
Customer self-service portal
Remote AR/IoT diagnostics
```

---

# 852. Final Source-of-Truth Matrix

```text
Service Request             → Service
Work Order                  → Service
Visit / Assignment          → Service
Installed Customer Equipment→ Service
Work Template               → Service
Diagnosis / Root Cause      → Service
Maintenance Plan/Due        → Service
Meter Service Context       → Service
Warranty/Contract Coverage  → Service
SLA                         → Service
Service Charge              → Service

Company Fixed Asset         → Fixed Assets
Product/UOM                 → Catalog
Stock/Part Movement         → Inventory
Employee                    → HR
Attendance                  → HR
Purchase Order              → Purchasing
Supplier Invoice            → Invoicing
Employee Expense            → Expenses
Project                     → Projects
Customer Invoice            → Invoicing
Receivable                  → Subledger
Payment                     → Payments
Journal Entry               → Ledger
```

---

# 853. Final Simple Internal Maintenance Flow

```text
Asset Problem
→ Work Order
→ Technician
→ Part
→ Repair
→ Complete
```

---

# 854. Final Preventive Flow

```text
Maintenance Plan
→ Maintenance Due
→ Work Order
→ Visit
→ Checklist
→ Completion
→ Next Due
```

---

# 855. Final Customer Service Flow

```text
Request
→ Work Order
→ Visit
→ Diagnosis
→ Parts/Time
→ Technical Complete
→ Acceptance
→ Coverage
→ Service Charges
→ Invoice
```

---

# 856. Final Warranty Flow

```text
Failure
→ Coverage Resolver
→ Customer Warranty
→ Supplier Warranty
→ Charge split
```

---

# 857. Final Contract Flow

```text
Service Contract
→ Entitlement + SLA + PM
→ Work
→ Usage
→ Overage / Included
→ Billing
```

---

# 858. Final Request Rule

> **A Service Request captures demand; a Work Order authorizes and structures execution; a Visit represents one execution occurrence.**

---

# 859. Final Equipment Rule

> **Company-owned Assets remain owned by Fixed Assets; customer/third-party equipment serviced by TODIR belongs to the Installed Base.**

---

# 860. Final Party Rule

> **Equipment owner, service customer, requester, service location, contract holder and bill-to can be different and must not be collapsed into one Customer field.**

---

# 861. Final Template Rule

> **Repeat work is generated from versioned Work Templates so checklists, skills, parts and expected duration remain consistent and historically reproducible.**

---

# 862. Final Diagnostic Rule

> **Reported symptom, failure mode, root cause and resolution are separate facts; notes supplement them but do not replace them.**

---

# 863. Final Preventive Rule

> **A Maintenance Plan creates forecast/due work according to explicit fixed, floating or meter-based rules; a Due item is not a Work Order until converted.**

---

# 864. Final Meter Rule

> **Meters are typed measurements with UOM, history, reset/rollover semantics and validation; they are not arbitrary custom numbers.**

---

# 865. Final Parts Rule

> **Service expresses demand and usage context; Inventory owns reservation, issue, consumption, return and stock truth.**

---

# 866. Final Van Stock Rule

> **A technician van is an Inventory location, never a second stock ledger in Service.**

---

# 867. Final Time Rule

> **Travel, service work and HR Attendance are separate facts; service time can create cost/billing but never replaces attendance.**

---

# 868. Final Warranty Rule

> **Warranty is not a boolean and does not mean free service; coverage is resolved per labor, parts, travel and other charge categories.**

---

# 869. Final Contract Rule

> **Service Contract defines coverage, usage, recurrence, SLA and commercial rules; it is not the Invoice.**

---

# 870. Final SLA Rule

> **Priority, asset criticality and SLA are different concepts; SLA clocks use versioned business calendars and explicit pause rules.**

---

# 871. Final Billing Rule

> **Service Charge is the commercial result of executed service after coverage; the legal Invoice remains owned by Invoicing.**

---

# 872. Final Project Billing Rule

> **When Projects owns billing for project-linked service work, Service supplies execution/cost/billing-source facts and must never bill the same work independently.**

---

# 873. Final Maintenance Cost Rule

> **Maintenance cost can inform repair/replace decisions but never changes Fixed Asset Book Value without an approved Fixed Assets capital-addition process.**

---

# 874. Final Completion Rule

> **Technical completion, customer acceptance, financial close, invoicing and payment are separate lifecycle dimensions.**

---

# 875. Final Offline Rule

> **Offline work must be idempotent, conflict-aware and selectively synchronized; critical facts never use silent last-write-wins reconciliation.**

---

# 876. Final Simplicity Rule

> **A small company can schedule a technician, consume a part and close a repair in a few actions without learning SLA, entitlement, crew, meter or contract machinery.**

---

# 877. Final Enterprise Rule

> **A larger service organization can enable Installed Base hierarchies, contracts, SLA, crews, meters, preventive rounds, van stock, offline execution and complex coverage without replacing the Work core.**

---

# 878. Final Product Verdict

TODIR Service & Maintenance is release-ready only when:

```text
Request, Work Order and Visit are distinct.
One Work Order can have multiple Visits and resources.
Internal Fixed Assets are not duplicated.
Customer Installed Base is first-class.
Owner/Customer/Bill-To/Site distinctions work.
Templates are versioned.
Inspections are structured.
Failure/Root Cause/Resolution are traceable.
Preventive fixed/floating/meter schedules work.
Maintenance Due is separate from Work Order.
Meter history is trustworthy.
Downtime and reliability metrics are explainable.
Required/Reserved/Issued/Consumed/Returned parts remain distinct.
Inventory owns every stock effect.
Technician van is Inventory-owned.
Labor cost never exposes salary.
Customer and supplier warranty can coexist.
Coverage is line-level.
Service Contracts and SLA are distinct.
SLA pause/calendar behavior is reproducible.
Technical Completion and Customer Acceptance are separate.
Service Charges and Invoices are separate.
Projects and Service never double-bill.
Maintenance cost never mutates Asset value.
Mobile workflow is fast.
Offline operations retry without duplicates or silent conflicts.
Cross-module reconciliation shows zero unexplained difference.
Technician, Dispatcher, Maintenance, Contract and Finance acceptance tests pass.
```

---

# 879. Final Agent Rule

For every Service implementation decision, the Agent must answer:

> **What demand was reported, what exact work is authorized, what equipment/site is involved, which visit/resource executes it, what parts/time/evidence were used, what coverage/SLA applies, who owns stock/cost/billing/accounting truth, what can happen offline, and how will the full service history remain explainable without forcing the technician through ERP complexity?**

If any answer is unclear, the implementation is incomplete.

---

# 880. No Separate Service Subfeature Packs

Do **not** create separate architecture packs for:

```text
Field Service
Internal Maintenance
Installed Base
Work Orders
Visits
Preventive Maintenance
Meters
Warranty
Service Contracts
SLA
Technician Scheduling
Service Billing
```

They are all part of `24 Service & Maintenance`.

A future deeply specialized EHS, Fleet, IoT Predictive Maintenance or Depot Repair domain may become its own module only when evidence justifies a separate bounded context.

---

# 881. Research Basis — Current Competitor Patterns

This design intentionally incorporates verified current patterns from official documentation for:

```text
Microsoft Dynamics 365 Field Service
Oracle Maintenance / Service Logistics
IFS Cloud Service / Maintenance
Salesforce Field Service
SAP Field Service and Asset Management
Acumatica Field Service
Odoo 19 Maintenance / Field Service
```

Key adopted principles:

```text
Work Order ≠ Appointment/Booking
Installed Base is first-class
Preventive schedules precede execution
Agreements/contracts generate recurring work
Parts belong to inventory/supply chain
Mobile/offline is a core field-service concern
Work templates standardize repeat jobs
Service history is asset/equipment-centric
SLA and entitlement require explicit rules
```

TODIR deliberately applies those principles through its existing ERP module ownership boundaries rather than copying competitor module duplication.

---

# 882. Research Reference — Dynamics 365 Field Service

Official product documentation reviewed includes:

- Work order architecture.
- Customer agreements.
- Customer asset service history.
- Field Service inventory / truck stock.
- Inspections.
- Mobile/offline workflows.

The design takes the separation between Work Order and scheduled execution and uses TODIR Inventory instead of a second service-stock source of truth.

---

# 883. Research Reference — Oracle

Official Oracle Service Logistics / Maintenance documentation reviewed includes:

- Preventive Maintenance for customer assets.
- Maintenance Work Orders.
- Service Requests and Field Service Work Orders.
- Installed Base.
- Parts requirements / debrief.
- Service charges / invoice generation.
- Depot repair flows.

The design keeps those business stages but reduces required setup for simple companies.

---

# 884. Research Reference — Odoo 19

Official Odoo 19 documentation reviewed includes:

- Maintenance requests.
- Equipment maintenance.
- Maintenance calendar.
- Field Service.
- Worksheets.
- Field Service product/warehouse usage.

The design adopts the usability lesson: technicians should act on the work directly while Inventory and other ERP domains update behind the workflow.

---

# 885. Architecture Closure

The module must remain:

```text
Simple outside.
Powerful inside.
One canonical truth per domain.
Offline-ready.
Exception-driven.
History-preserving.
```

Any implementation that achieves a pretty Work Order screen but loses source ownership, service history, entitlement correctness, inventory integrity or offline safety fails this pack.

---

**END OF FILE — 24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
