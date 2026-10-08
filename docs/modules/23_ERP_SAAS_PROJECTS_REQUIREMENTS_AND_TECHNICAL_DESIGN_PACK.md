# 23 — TODIR ERP SaaS Projects / Project Operations / Project Costing / Billing / Profitability Requirements & Technical Design Pack

**File:** `23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + PROJECT OPERATIONS + PROJECT COST CONTROL + PROJECT BILLING + PROFITABILITY + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Project Manager, PMO, Professional Services Lead, Finance/Project Accountant, Sales Lead, HR/Payroll Lead, Purchasing Lead, Inventory Lead, Fixed Assets Lead, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — CUSTOMER DELIVERY + COST + REVENUE + COMMITMENTS + BILLING + MARGIN + CAPITAL PROJECTS**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `22_ERP_SAAS_FIXED_ASSETS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Locked upstream truths:
- Directory owns Party/Customer identity.
- Sales owns Quotation / commercial sale / optional Sales Order.
- HR owns Employee/Employment truth; Projects may assign Employee resources but must not duplicate employee data.
- HR Attendance is not Project Timesheet.
- Payroll owns salary/payslip truth and individual salary data is restricted.
- Expenses can carry Project as a cost dimension but Expenses remains canonical for expense truth.
- Purchasing owns Purchase commitment.
- Inventory owns physical movements/material valuation.
- Invoicing owns final customer invoices and supplier invoices.
- Subledger owns receivable/open-item truth.
- Payments owns actual money movement.
- Fixed Assets owns Asset/CIP financial asset lifecycle.
- Ledger owns Journal Entries/account determination.
- Projects owns project/task/time/project-budget/project-change/project-billing-eligibility facts.

---

# 0. Executive Decision

TODIR Projects is the **project execution + project financial control + project billing orchestration** module.

It is not a generic task manager.

It is not a second Sales module.

It is not a second Payroll.

It is not a second Purchasing or Inventory.

It is not an invoicing engine.

Its central product question is:

> **For this project, what did we promise, what work remains, who is assigned, what has been spent, what is already committed, what is expected to cost by completion, what can be billed, what has been invoiced/collected, and is the project expected to make or lose money?**

For a simple company the answer must be available without enterprise project-management bureaucracy.

For a larger company the same core must support:

```text
phases
tasks
milestones
resource planning
timesheets
budgets
commitments
actual cost
forecast / ETC / EAC
project changes
fixed-price billing
time-and-material billing
milestone billing
billable expenses
billable purchases/materials
capital projects / CIP
profitability
financial close
```

---

# 1. Product Law — Simple Outside, Correct Inside

Default small-project experience:

```text
Create Project
→ Add Team
→ Add Tasks
→ Do Work
→ Track Cost
→ Bill if applicable
→ Complete
```

No mandatory:

```text
WBS
resource booking
baseline
change order
budget version
billing rule matrix
multi-stage approval
earned value
project accounting jargon
```

unless company/project type needs them.

---

# 2. Product Law — No Workflow For Workflow's Sake

A 5-person software company should be able to create:

```text
Customer Project
Customer
Manager
Start / End
Budget optional
Tasks
```

and begin work immediately.

---

# 3. Product Law — Rich Domain Does Not Mean Heavy UI

Internally TODIR can maintain:

```text
Actual
Commitment
ETC
EAC
Billing Eligibility
Rate Snapshots
Budget Versions
Change History
```

while normal users see:

```text
Spent
Committed
Still Expected
Budget
Margin
```

---

# 4. Product Law — One Action Can Orchestrate Many Domains

Example:

```text
[Create Project From Accepted Quotation]
```

can create/link:

```text
Project
Customer
Commercial source
Billing terms
Project template
Milestones
Team defaults
```

without making Project own Quotation.

---

# 5. Absolute Domain Distinctions

```text
Project != Quotation
Project != Sales Order
Project != Customer Invoice
Project != Purchase Order
Project != Supplier Invoice
Project != Expense
Project != Inventory Movement
Project != Payroll
Project != Employee
Project != Fixed Asset
Project != Journal Entry
```

---

# 6. Financial Distinctions

```text
Budget != Commitment
Commitment != Actual Cost
Actual Cost != Paid Cash
ETC != Commitment
EAC != Budget
Contract Value != Revenue
Billing Eligible != Invoiced
Invoiced != Collected
Project Profit != Project Cash Flow
```

---

# 7. Execution Distinctions

```text
Project Team Allocation != Task Assignment
Timesheet != Attendance
Task != Milestone
Milestone Reached != Customer Accepted
Customer Accepted != Billing Eligible
Project Completed != Financially Closed
Execution Progress != Billing Progress
```

---

# 8. Rate Distinctions

```text
Employee Salary != Project Cost Rate
Project Cost Rate != Customer Billing Rate
Standard Billing Rate != Contract Billing Rate
Rate Today != Historical Rate Snapshot
```

---

# 9. Change Distinction

```text
Approved Change Order
!=
Editing Original Budget / Scope / Contract silently
```

---

# 10. Actual Cost Distinction

```text
Project Actual
!=
Editable summary number
```

Actuals derive from approved/canonical source facts.

---

# 11. Projects Owns

Projects owns:

```text
Project
Project Type
Project Template
Project Phase / Work Breakdown
Project Task
Project Milestone
Project Deliverable/Acceptance context
Project Team
Resource Allocation
Generic Resource Requirement
Task Assignment
Project Time Entry / Timesheet
Timesheet Approval state
Project Budget
Budget Version
Budget Line
Project Forecast
ETC / EAC
Project Commitment projection/reference
Project Actual Cost Fact / projection
Project Billing Plan
Project Billing Rule
Project Billable Charge
Project Billing Eligibility
Project Change Request
Project Change Order
Project Baseline
Project Financial Close
Project Timeline/history
Project operational reports/read models
```

---

# 12. Projects Does NOT Own

```text
Customer identity              → Directory
Quotation/Sales Order          → Sales
Employee/Employment            → HR
Attendance                     → HR
Salary/Payslip                 → Payroll
Employee Expense               → Expenses
Purchase Order                 → Purchasing
Supplier Invoice               → Invoicing
Inventory Movement/valuation   → Inventory
Customer Invoice               → Invoicing
Receivable/Outstanding         → Subledger
Payment                        → Payments
Fixed Asset/CIP book           → Fixed Assets
Journal Entry                  → Ledger
```

---

# 13. Core Project Types

Recommended baseline:

```text
CustomerProject
InternalProject
CapitalProject
```

plus configuration rather than dozens of hardcoded types.

---

# 14. Customer Project

Has:

```text
Customer Party
commercial source optional
billing model
delivery work
costs
billing/profitability
```

---

# 15. Internal Project

Examples:

```text
Internal ERP rollout
New office setup
R&D initiative
Internal transformation
Marketing campaign project
```

No customer billing required.

---

# 16. Capital Project

Examples:

```text
Build factory
Construct office
Install production line
Major capital improvement
```

Projects handles execution/cost attribution.

Fixed Assets handles:

```text
CIP
capitalizable financial asset
final assets
depreciation
```

---

# 17. Do Not Make Every Project Customer-Facing

`CustomerPartyId` optional depending Project Type.

---

# 18. Legal Entity

Every Project belongs exactly one Legal Entity by default.

---

# 19. Owning Branch

Optional/required according to project type/company.

---

# 20. Cross-Legal-Entity Projects

Not MVP default.

Do not make one Project freely post cost/revenue into multiple Legal Entities.

Future Group/Intercompany Project design can coordinate separate entity projects.

---

# 21. Project Identity

Concept:

```text
Project
{
    Id
    TenantId
    LegalEntityId
    OwningBranchId?

    ProjectNumber
    Name
    Description?

    ProjectType
    CustomerPartyId?

    ProjectManagerEmployeeId?
    ProjectManagerUserId?

    StartDate
    TargetEndDate?
    ActualEndDate?

    LifecycleState
    ExecutionHealth

    BillingModel
    Currency?

    SourceType?
    SourceRef?

    TemplateVersionId?

    RowVersion
}
```

---

# 22. Project Number

Use Platform Numbering.

No `MAX+1`.

---

# 23. Project Name Is Not Identity

Names can repeat.

---

# 24. Project Manager

Can reference Employee where manager is workforce member.

User link optional for action/access.

---

# 25. Project Manager Is Not Necessarily Line Manager

HR manager hierarchy and Project Manager role are separate.

---

# 26. Project Lifecycle

Recommended:

```text
Draft
Active
OnHold
ExecutionCompleted
FinancialClosing
Closed
Cancelled
```

---

# 27. Execution Health

Separate:

```text
OnTrack
AtRisk
Delayed
Critical
NotAssessed
```

---

# 28. Financial Health

Derived/projection:

```text
WithinBudget
BudgetWarning
OverBudget
MarginWarning
ForecastLoss
```

---

# 29. Billing Health

Derived/projection:

```text
NoBilling
NothingDue
BillingEligible
PartiallyInvoiced
FullyInvoiced
OverdueReceivable
```

---

# 30. Avoid Mega Status

Forbidden:

```text
ActiveOverBudgetPartiallyBilledDelayed
```

---

# 31. Project Source

Can originate from:

```text
Manual
AcceptedQuotation
SalesOrder
CRMOpportunityWon
ImportedLegacy
CapitalInitiative
InternalRequest
```

typed source reference.

---

# 32. Sales → Project

Recommended simple flow:

```text
Accepted Quotation
→ [Create Project]
```

when quotation/service requires project delivery.

---

# 33. Sales Remains Commercial Authority

Projects does not copy/rewrite:

```text
quotation lines
commercial acceptance
customer agreed sales price
sales discount
```

Use typed source references/snapshots required for project setup.

---

# 34. Opportunity Is Not Project

CRM Opportunity can create/seed Project only after company-defined trigger.

No automatic Project for every Lead.

---

# 35. Pre-Sales Work

Future optional:

```text
PreSalesProject / pursuit work
```

if needed.

Do not complicate core now.

---

# 36. Project Template

Purpose:

```text
make common projects fast and consistent
```

---

# 37. Template Can Define

```text
Project Type
Phases
Tasks
Milestones
Default durations
Default roles
Resource requirements
Budget categories
Billing plan skeleton
Document requirements
Close checklist
```

---

# 38. Template Is Versioned

Existing Projects preserve source template version.

Editing template does not rewrite live Projects.

---

# 39. Template Instantiation

Creates project-owned copies/references.

No hidden ongoing coupling unless explicit template-sync feature later.

---

# 40. Simple Template Example

```text
ERP Implementation

Discovery
Configuration
Data Migration
Training
UAT
Go Live
```

---

# 41. Work Structure — Progressive Complexity

Small:

```text
Project
→ Tasks
```

Medium:

```text
Project
→ Phases
→ Tasks
```

Advanced:

```text
Project
→ Work Packages / WBS Nodes
→ Tasks
→ Milestones
```

Same core.

---

# 42. Work Breakdown Node

Concept:

```text
ProjectWorkNode
{
    Id
    ProjectId
    ParentId?
    NodeType
    Code?
    Name
    SortOrder
}
```

---

# 43. Node Types

Possible:

```text
Phase
WorkPackage
Summary
```

Tasks remain executable records.

---

# 44. WBS Is Optional

Do not force codes/hierarchy for simple projects.

---

# 45. Project Task

Concept:

```text
ProjectTask
{
    Id
    ProjectId
    WorkNodeId?

    Title
    Description?

    OwnerEmployeeId?
    OwnerUserId?

    PlannedStart?
    PlannedEnd?
    ActualStart?
    ActualEnd?

    EstimatedEffort?
    RemainingEffort?

    Status
    Priority

    BillablePolicy?
    RowVersion
}
```

---

# 46. Task Status

Recommended simple:

```text
NotStarted
InProgress
Blocked
Done
Cancelled
```

Configurable labels/views can vary.

---

# 47. Do Not Turn Core Into Jira Clone

Not Day 1:

```text
advanced issue types
story points
sprints
epics
complex automation DSL
software-dev-only semantics
```

unless separate evidence-driven capability.

---

# 48. Task Dependency

Foundation:

```text
FinishToStart
```

with optional extension later.

---

# 49. Dependency Cycle

Blocked.

---

# 50. Critical Path

Architecture seam.

Not required Day 1 for most ERP customers.

---

# 51. Milestone

Milestone is a significant project/commercial checkpoint.

It is not ordinary task work.

---

# 52. Milestone Concept

```text
ProjectMilestone
{
    Id
    ProjectId
    WorkNodeId?

    Name
    PlannedDate?
    ReachedAt?

    RequiresCustomerAcceptance
    AcceptanceState

    BillingRuleRef?
    BillingEligibleAmount?

    State
}
```

---

# 53. Milestone State

Possible:

```text
Planned
Reached
Accepted
Rejected/NeedsRework
Cancelled
```

with acceptance dimension separable.

---

# 54. Reached != Accepted

A delivery team can mark milestone work complete while customer acceptance remains pending.

---

# 55. Accepted != Invoiced

Acceptance can create billing eligibility.

Invoicing remains separate.

---

# 56. Deliverable

Optional typed object when proof/delivery matters.

---

# 57. Deliverable Can Include

```text
Name
Due date
File refs
Submission
Customer acceptance
Revision
Milestone link
```

---

# 58. Deliverable Not Required For Every Task

Only when business needs formal handoff.

---

# 59. Project Documents

Platform Files owns binary content.

Projects owns typed relationship.

---

# 60. Document Types

Examples:

```text
Contract/SOW reference
Scope
Specification
Drawing
Customer Approval
Meeting Output
Deliverable
Change Order
Acceptance Certificate
Photo
Other
```

---

# 61. Customer Contract Boundary

Legal/commercial contract may remain Sales/Contract future capability.

Projects keeps source/link and delivery/billing terms snapshot where needed.

---

# 62. Project Team

Project Team represents planned/authorized participation.

---

# 63. Team Member

Concept:

```text
ProjectTeamMember
{
    ProjectId
    EmployeeId?
    ExternalResourceRef?

    ProjectRoleId
    EffectiveFrom
    EffectiveTo?

    AllocationPercent?
    PlannedHours?
    CostRateProfileRef?
    BillingRateProfileRef?

    State
}
```

---

# 64. Employee Comes From HR

No duplicate employee master.

---

# 65. External Resource

May represent:

```text
contractor
partner consultant
generic resource
```

without pretending external person is Employee.

---

# 66. Project Role

Examples:

```text
Project Manager
Business Analyst
Developer
Engineer
Designer
Consultant
Technician
Project Accountant
```

---

# 67. Project Role != HR Position

An Employee with HR Position `Senior Developer` may act as:

```text
Solution Architect
```

on one Project.

---

# 68. Generic Resource Requirement

For planning before named assignment:

```text
Role = Senior Backend Developer
Need = 80 hours
Date range = Nov
```

---

# 69. Generic Resource Is Not Fake Employee

It is planning demand.

---

# 70. Resource Allocation

Represents capacity reserved/planned for Project.

---

# 71. Team Allocation != Task Assignment

Locked.

---

# 72. Allocation Example

```text
Ahmed
Project A
50%
Nov 1 → Dec 31
```

---

# 73. Task Assignment Example

```text
Ahmed
Task: API Integration
Estimated 30h
```

---

# 74. Allocation Can Exist Without Task Yet

Useful planning.

---

# 75. Task Assignment Can Exceed Allocation

System should warn/reconcile, not silently change capacity.

---

# 76. Resource Capacity

HR Work Calendar provides available working capacity context.

Projects does not own employment calendar truth.

---

# 77. Leave / Absence Capacity

HR can reduce effective availability.

Projects consumes safe availability projection.

---

# 78. Do Not Use Attendance As Project Effort

Locked.

---

# 79. Timesheet Mission

Timesheet answers:

> **Where did project-working time go?**

Not:

> **Was the employee present at work?**

---

# 80. Time Entry

Concept:

```text
ProjectTimeEntry
{
    Id
    ProjectId
    TaskId?
    EmployeeId

    WorkDate
    Duration

    BillableDuration?
    NonBillableReason?

    Description?
    CostRateSnapshotRef?
    BillingRateSnapshotRef?

    State
    ApprovalState
    Source?
    RowVersion
}
```


# 81. Time Entry States

Recommended:

```text
Draft
Submitted
Approved
Rejected
FinalizedForCost
Billed/PartiallyBilled projection
Corrected
```

Keep billing/cost finalization as separate dimensions internally where needed.

---

# 82. Timesheet Approval

Small company can use:

```text
Enter Time
→ auto-approve / direct finalize
```

Larger company:

```text
Employee
→ Project Manager
→ Finalize
```

or approved workflow.

---

# 83. No Mandatory Timesheet Approval

Configurable.

---

# 84. Timesheet Quantity

Use duration/time quantity, not generic floating number without unit.

---

# 85. Work Date

Separate from:

```text
CreatedAt
SubmittedAt
ApprovedAt
BilledAt
```

---

# 86. Time Entry Correction

Before finalization:

```text
edit
```

After financial/billing effect:

```text
correct / reverse / adjustment
```

No silent overwrite.

---

# 87. Billable Time

Worked duration can differ from billable duration.

---

# 88. Example

```text
Worked 8h
Billable 6h
Internal/non-billable 2h
```

---

# 89. Non-Billable Reason

Typed:

```text
Training
Internal rework
Warranty/support inclusion
Commercial write-off
Non-billable management
Other
```

configurable.

---

# 90. Cost Time vs Billing Time

Project Cost may use:

```text
approved worked duration
```

Billing can use:

```text
approved billable duration
```

---

# 91. Cost Rate Privacy

Projects must not require raw Payroll salary.

---

# 92. Project Labor Cost Rate

Preferred integration choices:

```text
A. Payroll/Finance publishes privacy-safe cost rate/fact
or
B. Payroll publishes finalized project labor cost allocation
```

Exact contract must preserve salary privacy.

---

# 93. Salary Is Not Project Cost Rate

Project Cost Rate may include:

```text
salary allocation
employer taxes
benefits
overhead
standard burden
```

depending company policy.

---

# 94. Cost Rate Profile

Projects can own/project reference a financial rate profile if Finance config approves.

Do not expose underlying salary calculation.

---

# 95. Billing Rate

Customer commercial rate can come from:

```text
Sales contract / quotation
Project rate card
role-based billing rate
employee-specific contractual rate
task/service rate
```

with clear precedence.

---

# 96. Billing Rate Snapshot

Each financially relevant Time Entry/Billable Charge freezes applied rate/version.

---

# 97. Historical Rate Stability

Changing tomorrow's rate must not change billed/approved historical time.

---

# 98. Rate Currency

Explicit.

---

# 99. Rate UOM

Examples:

```text
per Hour
per Day
per Unit
```

never implicit.

---

# 100. Rate Card

Concept:

```text
ProjectRateCard
{
    Id
    ProjectId?
    CustomerPartyId?
    Currency
    EffectiveFrom
    EffectiveTo?
    Version
}
```

---

# 101. Rate Card Lines

Can key by:

```text
Project Role
Employee override
Service/Task type
```

keep deterministic.

---

# 102. Rate Precedence

Must be explicit.

Example:

```text
Project-specific employee rate
→ Project role rate
→ Customer contract rate
→ Company default
```

or approved alternative.

---

# 103. No Hidden Rate Fallback

Missing bill rate should surface:

```text
Unpriced billable time
```

not silently zero or random default.

---

# 104. Budget Mission

Budget answers:

> **What did the project expect/authorize to spend or bill before actual execution?**

---

# 105. Budget Types

At minimum:

```text
Cost Budget
Revenue/Sales Budget optional
```

---

# 106. Small Project Budget

Can be one number:

```text
Total Cost Budget = 500,000
```

---

# 107. Detailed Budget

Can split by:

```text
Cost Category
Work Node
Time period
Resource Role
Project Phase
```

---

# 108. Financial Breakdown Need Not Equal WBS

Locked.

---

# 109. Budget Category

Examples:

```text
Labor
Travel
Materials
Subcontractors
Purchasing
Equipment
Cloud/Software
Other Direct Cost
Contingency
```

---

# 110. Budget Line

Concept:

```text
ProjectBudgetLine
{
    ProjectId
    BudgetVersionId
    Category
    WorkNodeId?
    Period?
    ResourceRoleId?
    Quantity?
    Rate?
    Amount
    Currency
}
```

---

# 111. Budget Versioning

Budget changes preserve:

```text
Original
Approved revisions
Current approved
```

---

# 112. Original Budget Is Immutable

After baseline approval.

---

# 113. Budget Draft

Before approval can edit.

---

# 114. Budget Revision

Creates new version / approved change.

---

# 115. Budget Change Source

Could be:

```text
Project Change Order
Management Reforecast
Internal Budget Revision
```

typed.

---

# 116. Budget Approval

Optional.

Small company can approve directly.

Large company can use Platform Approvals.

---

# 117. Budget Currency

Project budget currency explicit.

---

# 118. Multi-Currency Budget

Can use base/project currency for control while source facts retain original currency.

---

# 119. Commitment Mission

Commitment answers:

> **What future project cost has the company already commercially committed to but may not yet have become actual accounting cost?**

---

# 120. Purchase Commitment

Purchasing owns Purchase Order commitment truth.

Projects consumes project allocation/projection.

---

# 121. Commitment Examples

```text
Open Purchase Order
Approved subcontract commitment
Contracted external service
Reserved procurement commitment
```

---

# 122. Commitment Is Not Supplier Invoice

A PO can be committed before supplier invoice exists.

---

# 123. Commitment Is Not Cash Paid

Obvious but critical.

---

# 124. Commitment Allocation

A Purchase Order line can allocate to:

```text
Project
Project Phase
Task/Work Node
Cost Category
```

through typed dimensions/reference.

---

# 125. One PO Can Span Projects

If Purchasing permits allocation:

```text
PO line / distribution
→ Project A
→ Project B
```

Projects consumes exact allocated commitment.

---

# 126. Project Commitment Projection

Concept:

```text
ProjectCommitment
{
    SourceType
    SourceId
    SourceLineId?
    ProjectId
    WorkNodeId?
    CostCategory
    OriginalCommittedAmount
    OpenCommittedAmount
    Currency
    State
}
```

Canonical source remains Purchasing.

---

# 127. Commitment Reduction

As PO is:

```text
received
invoiced
cancelled
closed
```

open commitment decreases according to purchasing/accounting policy.

---

# 128. Commitment To Actual Transition

Do not double count:

```text
same cost as both full commitment and actual
```

Open commitment must reflect consumed portion.

---

# 129. Example

```text
PO = 100,000
Supplier Invoice received = 40,000

Actual = 40,000
Open Commitment = 60,000
```

subject to source semantics.

---

# 130. Commitment Change

PO amendment updates commitment.

History remains source-linked.

---

# 131. Actual Cost Mission

Actual Cost answers:

> **What project cost has actually been economically recognized/approved from canonical source events?**

---

# 132. Actual Cost Sources

Potential:

```text
Approved Project Labor
Finalized Employee Expense
Supplier Invoice
Inventory Material Issue
External Service
Asset Usage/Depreciation allocation later
Other Ledger-backed direct cost fact
```

---

# 133. Actual Cost Fact

Projects can own a normalized project-cost fact/read model sourced idempotently from canonical modules.

---

# 134. Project Cost Fact Concept

```text
ProjectCostFact
{
    Id
    ProjectId
    WorkNodeId?

    SourceType
    SourceId
    SourceLineId?

    CostType
    Quantity?
    Uom?

    OriginalAmount
    OriginalCurrency
    BaseAmount
    FxSnapshotRef?

    CostDate
    AccountingDate?

    State
    CorrectionOf?
}
```

---

# 135. Project Cost Fact Is Not Journal Entry

It is project-cost analytical truth linked to source.

---

# 136. Fact Idempotency

Unique by stable:

```text
SourceType + SourceId + SourceLine/Allocation + Project
```

where appropriate.

---

# 137. Actual Cost Correction

Source module correction produces:

```text
reversal/correction ProjectCostFact
```

not in-place amount edit.

---

# 138. Expense Cost

`21 Expenses` finalized Expense with Project allocation becomes project cost.

---

# 139. Expense Still Owns

```text
receipt
policy
employee reimbursement
tax
```

Projects consumes:

```text
project allocatable cost/billable context
```

---

# 140. Billable Expense

Project commercial rule may mark finalized Expense allocation as:

```text
Billable
NonBillable
IncludedInFixedPrice
RequiresReview
```

---

# 141. Expense Markup

Example:

```text
Actual Expense Cost = 5,000
Markup = 10%
Billable Charge = 5,500
```

Do not modify Expense amount.

---

# 142. Expense Rebilling Tax

Customer billing tax is calculated by Invoicing/Tax for customer invoice.

Do not reuse supplier/employee Expense tax blindly.

---

# 143. Supplier Cost

Supplier Invoice can generate actual project cost.

---

# 144. Purchase Order Before Invoice

Creates commitment only.

---

# 145. Supplier Invoice Finalized

Creates actual cost according to allocation/accounting policy and reduces commitment exposure.

---

# 146. Inventory Material Cost

Inventory owns issue and valuation.

---

# 147. Material Issue To Project

Typed Inventory source:

```text
IssueToProject
```

or approved generic project dimension.

---

# 148. Material Cost Fact

Uses Inventory valuation result, not Product selling price.

---

# 149. Material Return From Project

Inventory return/correction produces negative/reversal project cost where appropriate.

---

# 150. Material Quantity

Project can report:

```text
Product
Quantity
UOM
Cost
```

from Inventory truth.

---

# 151. Labor Cost

Source from privacy-safe Payroll/Finance/approved Project Cost Rate.

---

# 152. Labor Cost Fact

Can be generated when Time Entry is finalized.

---

# 153. Labor Cost Before Payroll

Company can use:

```text
Standard Project Cost Rate
```

for operational project costing.

---

# 154. Payroll Actual Cost Later

If company chooses actual payroll allocation, difference can be:

```text
Labor Cost Adjustment
```

not silent rewrite.

---

# 155. Standard vs Actual Labor Cost

Policy must define one primary project-cost method.

Do not mix without reconciliation.

---

# 156. Asset Usage Cost

Optional later.

---

# 157. Asset Depreciation Allocation

If enabled:

```text
Fixed Assets Book depreciation
→ project allocation
```

through explicit rule/source.

Projects must not calculate Asset depreciation.

---

# 158. Direct vs Indirect Cost

Projects can classify:

```text
Direct
IndirectAllocated
Overhead
```

but do not build full cost accounting engine Day 1.

---

# 159. Overhead/Burden

Optional controlled rate capability later.

---

# 160. Actual Cost Is Rebuildable

Project actual summaries derive from Project Cost Facts/source projections.



# 161. Forecast Mission

Forecast answers:

> **Given what has happened and what remains, what do we now expect the project to cost and earn by completion?**

---

# 162. ETC

```text
ETC = Estimate To Complete
```

Expected remaining cost from forecast date.

---

# 163. EAC

```text
EAC = Actual Cost + ETC
```

plus explicit treatment of open commitments to avoid double counting.

---

# 164. Commitment vs ETC

Do not blindly calculate:

```text
Actual + Commitment + ETC
```

because ETC may already include committed future costs.

---

# 165. Forecast Inclusion Policy

Each ETC line should say whether:

```text
IncludesCommitments
ExcludesCommitments
```

or source category is explicit.

---

# 166. Simple Forecast

Project Manager enters:

```text
Remaining Labor      100,000
Remaining Purchases   50,000
Remaining Travel      20,000
```

---

# 167. Detailed Forecast

Can derive/suggest from:

```text
remaining task effort
resource rates
open commitments
planned expenses
planned materials
```

---

# 168. Forecast Is Not Actual

Never post Ledger from forecast.

---

# 169. Forecast Versioning

Preserve periodic snapshots:

```text
Forecast as of Jan
Forecast as of Feb
Forecast as of Mar
```

for trend.

---

# 170. Forecast Snapshot

Concept:

```text
ProjectForecastVersion
{
    ProjectId
    AsOfDate
    Version
    Approved?
    Notes?
}
```

---

# 171. Forecast Line

```text
CostCategory
WorkNode?
Period?
RemainingQuantity?
Rate?
ETCAmount
CommitmentTreatment
```

---

# 172. EAC Comparison

Show:

```text
Original Budget
Current Budget
Actual
Open Commitment
ETC
EAC
Variance At Completion
```

---

# 173. Variance At Completion

Simple:

```text
Current Budget - EAC
```

positive/negative according to display convention.

---

# 174. Forecast Loss

Customer project can flag:

```text
Expected Revenue < EAC
```

or company-defined margin threshold.

---

# 175. Margin Forecast

```text
Expected Final Margin
=
Expected Final Revenue
-
EAC
```

using explicit revenue measure.

---

# 176. Contract Value

Commercial source value.

Not accounting revenue.

---

# 177. Current Contract Value

Can include approved Change Orders.

Preserve:

```text
Original Contract Value
Approved Changes
Current Contract Value
```

---

# 178. Revenue Measures

Projects may display:

```text
Contract Value
Billing Eligible
Invoiced
Recognized Revenue if Accounting later provides
Collected
```

Never call all of these `Revenue`.

---

# 179. Billing Model

Recommended foundation:

```text
NonBillable
FixedPrice
TimeAndMaterials
Milestone
CostPlus
Mixed
```

---

# 180. Mixed Billing

One Project can have different billing rules by:

```text
work package
contract line
charge category
```

when advanced mode enabled.

---

# 181. Small Company Billing

Can choose one simple billing model at Project creation.

---

# 182. Billing Plan

Defines how billing eligibility is generated.

---

# 183. Billing Rule

Concept:

```text
ProjectBillingRule
{
    Id
    ProjectId
    RuleType
    Scope
    EffectiveFrom
    EffectiveTo?
    Currency
    Version
    Cap?
}
```

---

# 184. Billing Rule Types

Possible:

```text
FixedAmount
Milestone
TimeAtRate
ExpenseActual
ExpenseMarkup
PurchaseActual
PurchaseMarkup
MaterialActual
MaterialMarkup
CostPlus
ProgressPercent
```

Build launch-needed subset.

---

# 185. Billable Charge

First-class Project object.

---

# 186. Why Billable Charge Exists

It separates:

```text
source work/cost
```

from:

```text
invoice document
```

and allows review, caps, markup, write-off and grouping before Invoice.

---

# 187. Billable Charge Concept

```text
ProjectBillableCharge
{
    Id
    ProjectId

    SourceType
    SourceId
    SourceLineId?

    ChargeType

    Quantity?
    Uom?
    Rate?
    RateSnapshotRef?

    BaseAmount?
    Markup?
    BillableAmount
    Currency

    BillingState
    InvoiceRef?
    CorrectionOf?
}
```

---

# 188. Billing Charge Source Types

```text
Time
Expense
Purchase
Material
Milestone
FixedSchedule
ManualApprovedAdjustment
```

---

# 189. Billable Charge State

```text
Draft/Calculated
Eligible
Held
WrittenOff
InvoicingRequested
Invoiced
PartiallyInvoiced
Corrected
```

---

# 190. Charge != Invoice Line

Invoicing decides final legal document line construction.

---

# 191. Billing Eligibility

Means Project business rules allow charge to be invoiced.

---

# 192. Billing Eligibility != Invoice Issued

Locked.

---

# 193. Fixed Price Billing

Contract amount does not depend directly on actual cost.

---

# 194. Fixed Price Schedule

Can be:

```text
On Start
Monthly Installments
Specific Dates
Milestones
Progress Percentage
Manual approved billing events
```

---

# 195. Milestone Billing

Example:

```text
Kickoff      30%
UAT          40%
Go Live      30%
```

---

# 196. Milestone Billing Event

On accepted/reached trigger according to contract:

```text
ProjectBillableCharge
```

created/idempotent.

---

# 197. Customer Acceptance Requirement

Billing rule can require:

```text
Milestone Reached
+
Customer Accepted
```

before eligibility.

---

# 198. Acceptance Evidence

Can be:

```text
User confirmation
Customer email/document
Signed acceptance
Portal action later
```

---

# 199. Acceptance Is Immutable History

Correction/revoke requires explicit event/reason.

---

# 200. Time & Materials Billing

Approved billable Time Entries generate charges.

---

# 201. T&M Time Billing

```text
Billable Duration
×
Billing Rate Snapshot
=
Billable Charge
```

---

# 202. T&M Expense Billing

Can use:

```text
Actual Cost
Actual Cost + Markup
Contracted fixed reimbursement
```

---

# 203. T&M Purchase Billing

Supplier cost can generate charge according to rule.

---

# 204. Material Billing

Inventory material issue can generate billable charge if contract says customer pays materials.

---

# 205. Cost Plus

Concept:

```text
Eligible Cost
+ Allowed Fee/Markup
```

with typed eligible cost classes.

---

# 206. Cost Plus Does Not Mean All Costs

Rule defines eligible:

```text
Labor
Expense
Subcontract
Material
```

and exclusions.

---

# 207. Billing Markup

Can be:

```text
Percentage
Fixed fee
rate table
```

versioned.

---

# 208. Billing Cap

Rule can cap:

```text
total charge
category
period
contract line
```

---

# 209. Cap Is Not Budget

Billing cap is customer commercial limit.

Budget is project cost planning.

---

# 210. Over-Cap Charge

State:

```text
Held / NotBillable / NeedsChangeOrder
```

not silently truncated without trace.

---

# 211. Write-Off

Authorized commercial decision:

```text
Billable 10,000
Write off 2,000
Invoice 8,000
```

Preserve original eligibility and write-off reason.

---

# 212. Billing Adjustment

After invoicing, use corrective charge / invoice credit/debit flow.

Do not edit billed source silently.

---

# 213. Invoice Request

Projects sends:

```text
Customer
LegalEntity
Currency
Eligible Charges
Commercial grouping
Project refs
Billing period
```

to Invoicing.

---

# 214. Invoicing Owns

```text
Invoice Number
Tax
Legal snapshot
Document lifecycle
Credit Note
Accounting
```

---

# 215. Billing Tax

Tax Engine/Invoicing calculates customer tax.

Project billing charge can carry commercial pre-tax amount/context.

---

# 216. Invoice Allocation Back

Invoicing returns source allocations:

```text
InvoiceId
InvoiceLineId
ChargeId
Amount
```

---

# 217. Partial Invoice

A charge can be partially invoiced only if rule/business allows.

---

# 218. Invoice Cancellation/Correction

Project billing projection consumes Invoicing correction events.

---

# 219. Collected

Subledger/Payments projection.

---

# 220. Outstanding

Subledger authority.

---

# 221. Project Payment Allocation

If one Invoice spans several Projects, Invoicing allocations allow project outstanding/collection distribution.

---

# 222. Do Not Infer Collection From Bank Balance

Use canonical payment/open-item allocation.

---

# 223. Profitability Mission

Profitability should answer:

```text
What is the project worth?
What has it cost?
What is expected to cost?
What has been billed?
What margin is expected?
```

---

# 224. Actual Project Margin

Potential operational measure:

```text
Invoiced/recognized basis
-
Actual Cost
```

but label basis explicitly.

---

# 225. Expected Margin

```text
Expected Final Revenue
-
EAC
```

---

# 226. Contract Margin

```text
Current Contract Value
-
EAC
```

useful fixed-price forecast.

---

# 227. Margin Percentage

Define denominator explicitly:

```text
Margin / Revenue
```

not markup.

---

# 228. Markup Percentage != Margin Percentage

Explicit formulas.

---

# 229. Profitability Permissions

Project Manager can see:

```text
budget
actual
forecast
margin
```

only if allowed.

---

# 230. Salary Privacy

Profitability can use aggregate labor cost without exposing individual salary.

---

# 231. Financial Breakdown

Project financial reporting should support:

```text
Labor
Expenses
Purchases/Subcontract
Materials
Assets/Equipment allocation
Other
```

---

# 232. Work Breakdown vs Financial Breakdown

Separate structures with optional mapping.

---

# 233. Cost Category Mapping

Each source fact maps deterministically to Project Cost Category.

---

# 234. Project Baseline

Approved snapshot of planned scope/schedule/budget.

---

# 235. Small Project Baseline

Optional.

Could simply preserve:

```text
Original Start
Original End
Original Budget
```

when project activates.

---

# 236. Advanced Baseline

Can snapshot:

```text
WBS
task dates
planned effort
budget
milestones
resource plan
```

---

# 237. Baseline Is Immutable

New baseline/rebaseline records reason.

---

# 238. Rebaseline

High-impact.

Requires:

```text
reason
approval if configured
linked change orders
```

---

# 239. Do Not Hide Variance By Rebaseline

Keep previous baselines.

---

# 240. Project Change Management

First-class.

---

# 241. Change Request

Captures proposed change before approval.

---

# 242. Change Request Concept

```text
ProjectChangeRequest
{
    Id
    ProjectId

    Title
    Description
    RequestedBy
    RequestedAt

    ScopeImpact
    ScheduleImpact
    CostImpact
    RevenueImpact
    CommitmentImpact

    State
    ApprovalState
}
```

---

# 243. Change Request States

```text
Draft
Submitted
UnderReview
Approved
Rejected
ConvertedToChangeOrder
Cancelled
```

---

# 244. Change Order

Approved change that updates project controlled plan/commercial/budget state.

---

# 245. Change Order Concept

```text
ProjectChangeOrder
{
    Id
    ProjectId
    ChangeRequestId?

    EffectiveDate
    ApprovedAt

    BudgetDelta
    ContractValueDelta?
    ScheduleDelta?
    ScopeSnapshot

    SourceSalesRevisionRef?
    CustomerApprovalRef?
}
```

---

# 246. Customer Commercial Change

If customer-facing price/scope changes:

Sales/contract commercial source may need revision/approval.

Projects links.

---

# 247. Internal Change

Can change:

```text
budget
schedule
resource forecast
```

without customer commercial amendment.

---

# 248. Change Order Updates Current Plan

Preserve:

```text
Original
Approved Change 1
Approved Change 2
Current
```

---

# 249. Change Order Can Affect Commitment

Example:

```text
Subcontract increase
→ PO amendment
```

Purchasing owns PO amendment.

Project change links/coordinates.

---

# 250. Change Order Can Affect Billing

New milestone/rate/contract amount only after commercial approval.

---

# 251. Change Order Does Not Rewrite Historical Actuals

Absolute.

---

# 252. Scope Version

Project can store scope snapshots/version refs.

---

# 253. Project Progress

Do not use one universal percentage for all meanings.

---

# 254. Execution Progress

Derived/manual from:

```text
tasks
milestones
physical completion
```

according to project type.

---

# 255. Cost Progress

Can display:

```text
Actual / Current Budget
```

but not call it physical completion.

---

# 256. Billing Progress

```text
Invoiced / Current Contract/Billing Plan
```

where meaningful.

---

# 257. Collection Progress

```text
Collected / Invoiced
```

---

# 258. Revenue Recognition Progress

Future Accounting capability only.

---

# 259. Physical Progress

Construction/engineering may need:

```text
quantities
percent complete
measurement
```

advanced project-type capability.

---

# 260. Earned Value

Architecture seam only.

Not Day 1.

---

# 261. Capital Project Mission

Projects handles execution and cost attribution.

Fixed Assets handles financial CIP/Asset capitalization.

---

# 262. Capital Project Link

```text
Project
→ one/many CIP Assets
```

through Fixed Assets contracts.

---

# 263. Capitalizable Classification

Project source cost can be:

```text
Capitalizable
NonCapitalizable
NeedsReview
```

based on policy/accountant.

---

# 264. Projects Does Not Finalize Asset Capitalization

Fixed Assets owns decision/final asset financial truth.

---

# 265. CIP Cost Transfer

Eligible project costs can be offered to Fixed Assets:

```text
ProjectCostFact
→ Capitalization Candidate / CIP Cost Source
```

---

# 266. One Project → Many Assets

Example factory project:

```text
Land Improvement
Building
Production Line
Generator
```

---

# 267. Many Projects → One CIP

Possible but advanced; Fixed Assets allocation decides.

---

# 268. Capital Project Noncapitalizable Cost

Remains project expense/cost and not CIP.

---

# 269. Capital Project Close

Cannot close financially with unresolved capitalizable cost classification if policy requires.

---

# 270. Internal Project Close

Can close once cost/commitments resolved.

---

# 271. Customer Project Execution Complete

Means delivery work done.

---

# 272. Financial Close

Separate process.

---

# 273. Financial Close Checklist

Potential:

```text
No open timesheets requiring cost
No pending expenses requiring project allocation
No open commitments or approved exception
Supplier invoices captured
Inventory project issues/returns complete
Billable charges resolved
Invoices created or waived
Customer credits/corrections resolved
Capitalizable costs transferred if capital project
Forecast closed
```

---

# 274. Close Exceptions

Authorized Finance/PM can close with documented exception where policy allows.

---

# 275. Closed Project

No ordinary new cost/time/billing entries.

Corrections require explicit reopen/correction path.

---

# 276. Reopen

High-impact controlled action.

Preserve close history.

---

# 277. Cancelled Project

Does not delete existing actual cost/commitment.

Need resolve commercial/purchasing/billing consequences.

---

# 278. On Hold

Can prevent:

```text
new time
new procurement
billing
```

according to hold policy.

---

# 279. Project Health

Can be manually assessed plus deterministic warnings.

---

# 280. Health Signals

```text
Overdue milestone
Over budget
EAC > budget
Forecast loss
Resource shortage
Open critical blockers
Unbilled eligible amount aging
Unresolved commitments near close
```

---

# 281. AI Health Summary

AI can summarize reasons.

Deterministic metrics remain source.

---

# 282. AI Restrictions

AI must not autonomously:

```text
approve time
change budget
approve change order
mark milestone accepted
create binding invoice
change cost/billing rate
close project
capitalize asset cost
```

---

# 283. AI Useful Cases

```text
summarize project status
summarize meeting notes
draft status report
suggest risks
suggest task breakdown
detect budget anomalies
suggest ETC based on trends
identify missing time/expenses
```

Human confirms material actions.

---

# 284. Customer Portal Seam

Future external portal can show:

```text
Milestones
Deliverables
Approvals
Documents
Invoices
Payment links
```

limited.

---

# 285. Customer Is Not Internal User

Locked TODIR principle.

---

# 286. Customer Acceptance Portal

Future:

```text
Accept/Reject Deliverable
```

through narrow external access.

---

# 287. Project Communication

Do not build full chat app Day 1.

Use:

```text
comments/activity
notifications
document links
```

if platform supports.

---

# 288. Project Activity Timeline

Compose:

```text
Created
Activated
Task changes
Milestones
Time approvals
Cost facts
Budget revisions
Change orders
Billing events
Invoices
Collections
Close
```

permission-aware.

---

# 289. Project Calendar

View only.

Tasks/milestones remain canonical.

---

# 290. Portfolio

Portfolio is read model/grouping across projects.

---

# 291. Portfolio View

Show:

```text
Project
Customer
Manager
Health
Progress
Budget
Actual
Commitment
EAC
Margin
Next Milestone
```

permission-aware.

---

# 292. Program/Portfolio Hierarchy

Future optional:

```text
Program
→ Projects
```

Do not block MVP.

---

# 293. Project Tag

Lightweight classification.

Not source of financial ownership.

---

# 294. Project Department

Can default cost/report context.

---

# 295. Project Customer

Directory Party.

---

# 296. Customer Contacts

Directory contacts.

Project can assign contact roles:

```text
Sponsor
Project Contact
Approver
Billing Contact
```

without duplicate identity.

---

# 297. Project Vendor

Purchasing/Supplier identity remains Directory/Purchasing.

Project can report vendor spend.

---

# 298. Project Currency

Customer project can have contract/project currency.

---

# 299. Cost Source Currency

Preserve original currency.

---

# 300. Base Currency Reporting

FinBase converts with explicit purpose/rate snapshot.



# 301. Multi-Currency Project Rule

Never lose:

```text
Original Amount
Original Currency
Project Currency Amount
Base Currency Amount
Rate Snapshot
Rate Purpose
```

where financially material.

---

# 302. FX Changes

Historical actual/billed facts keep original snapshots.

Forecast can use current planning rate.

---

# 303. Forecast FX

Separate from accounting FX.

---

# 304. Profitability FX Basis

Dashboard must state:

```text
Base Currency
Project Currency
As-Of Rate
```

when comparing multi-currency values.

---

# 305. Project Tax Boundary

Projects does not calculate VAT/sales tax.

---

# 306. Billing Tax

Invoicing/Tax.

---

# 307. Cost Tax

Owning source:

```text
Expenses
Supplier Invoice
Inventory
```

determines tax/cost basis.

---

# 308. Net vs Gross Project Cost

Project Cost Fact should consume approved cost basis.

Do not sum gross supplier invoice if recoverable tax is excluded from economic project cost by accounting policy.

---

# 309. Cost Basis Contract

Each source module must return:

```text
project cost amount
currency/base amount
cost date
project allocation
source refs
```

not require Projects to reverse-engineer tax/accounting.

---

# 310. Accounting Boundary

Project operational actuals are not General Ledger.

---

# 311. Ledger Integration

Projects can emit facts for project-owned financial events only, such as:

```text
project-specific accrual later
billing adjustment if owned
```

but normal source costs already post through their owning modules.

---

# 312. Avoid Double Accounting

If Supplier Invoice already posts expense:

Projects must not emit second Ledger expense for same Project Cost Fact.

---

# 313. Project Dimension

Ledger facts from source modules can carry `ProjectId`/project dimension for reporting.

---

# 314. Ledger Project Dimension

Project reference should be stable and immutable historically.

---

# 315. Closed Project Accounting Corrections

Ledger/source correction can still reference closed Project.

Project read models update from correction facts.

---

# 316. Revenue Recognition Boundary

Advanced Accounting capability, not core Day 1 Projects.

---

# 317. Revenue Recognition Future Seam

Inputs can include:

```text
contract value
performance obligations
milestones
cost-to-cost progress
billing history
```

but Accounting owns recognition.

---

# 318. Project Cash Flow Boundary

Payments/Subledger/Banking.

Projects can compose view.

---

# 319. Cash Flow View

Potential:

```text
Customer cash collected
Supplier cash paid
Employee reimbursement cash
Other project cash
```

with clear source.

---

# 320. Cash Flow != Profitability

Locked.

---

# 321. Security Mission

Project access must protect:

```text
customer commercial data
cost rates
profitability
employee time
budget
vendor spend
contracts/documents
```

---

# 322. Permission Vocabulary

Initial:

```text
projects.read
projects.create
projects.edit
projects.activate
projects.hold
projects.complete
projects.close
projects.reopen
projects.cancel

projects.task.read
projects.task.manage

projects.milestone.read
projects.milestone.manage
projects.milestone.accept

projects.team.read
projects.team.manage
projects.resource_plan.read
projects.resource_plan.manage

projects.time.read
projects.time.enter
projects.time.approve
projects.time.correct

projects.budget.read
projects.budget.manage
projects.budget.approve

projects.cost.read
projects.cost_detail.read
projects.cost_rate.read

projects.commitment.read
projects.forecast.read
projects.forecast.manage
projects.forecast.approve

projects.billing.read
projects.billing.manage
projects.billing.writeoff
projects.billing.request_invoice

projects.profitability.read

projects.change.read
projects.change.request
projects.change.approve

projects.documents.read
projects.documents.manage

projects.capital.read
projects.capital.classify

projects.audit.read
projects.export
projects.settings.manage
```

---

# 323. Scope Model

Combine permission with:

```text
OwnProjects
TeamProjects
ManagedProjects
Department
Branch
LegalEntity
SpecificProject
AllAuthorizedProjects
```

---

# 324. Team Member Access

Being Project Team member does not automatically grant:

```text
budget
profitability
cost rates
commercial contract
all documents
```

---

# 325. Project Manager Access

Can receive broad execution access.

Financial access configurable.

---

# 326. Cost Rate Privacy

Separate permission:

```text
projects.cost_rate.read
```

---

# 327. Individual Labor Cost Privacy

Project Manager may see:

```text
Labor Cost Total
```

without individual cost rates.

---

# 328. Billing Rate Visibility

Can differ from cost visibility.

---

# 329. Customer Commercial Value Visibility

Separate if needed.

---

# 330. External Customer Portal

Never use internal project permission role directly.

Narrow portal capability only.

---

# 331. Project Accountant

Can access project financials without task-edit rights.

---

# 332. PMO

Can portfolio view/standards without customer billing edits.

---

# 333. Resource Manager

Can see capacity/allocations without salary/profitability.

---

# 334. HR Privacy

Projects gets only necessary:

```text
Employee display
availability
employment active state
safe skill refs later
```

not full HR file.

---

# 335. Payroll Privacy

Never send full Payslip/salary breakdown to Projects API.

---

# 336. RLS

All tenant-owned Project tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 337. Runtime DB Role

No ownership/BYPASSRLS.

---

# 338. Legal Entity Scope

Enforced backend.

---

# 339. Project Membership Is Application Authorization

RLS handles tenant; application handles fine-grained project access.

Can add project-scope database policy later only if justified.

---

# 340. File Security

Project FileRefs require both:

```text
Project access
Document permission
```

---

# 341. Audit Critical Actions

Capture:

```text
Project activation/hold/complete/close/reopen/cancel
Team/resource allocation change
Time approval/correction
Budget approve/revise
Forecast approval
Rate change
Milestone acceptance
Billing rule/version change
Billable charge write-off
Invoice request
Change order approval
Capitalizable classification
Close exception
Large export
```

---

# 342. Business History vs Audit

Business timeline says what changed.

Audit says who/system performed action and permission context.

---

# 343. Data Model Schema

Recommended:

```text
business_projects
```

---

# 344. ProjectsDbContext

Own Project tables only.

No cross-module EF navigation.

---

# 345. Core Tables

Potential:

```text
project
project_type
project_template
project_template_version

project_work_node
project_task
project_task_dependency
project_milestone
project_deliverable

project_role
project_team_member
project_resource_requirement
project_resource_allocation
project_task_assignment

project_time_entry
project_time_approval

project_budget
project_budget_version
project_budget_line

project_forecast
project_forecast_version
project_forecast_line

project_commitment_projection
project_cost_fact

project_rate_card
project_rate_card_version
project_rate_line

project_billing_plan
project_billing_rule
project_billable_charge
project_invoice_allocation

project_change_request
project_change_order
project_baseline

project_document
project_external_reference

project_close
project_close_exception

project_list_projection
project_financial_summary_projection
project_portfolio_projection
project_billing_summary_projection
project_resource_summary_projection
```

---

# 346. Tables Projects Must Not Own

```text
party
employee
employment
attendance
payslip
expense
purchase_order
supplier_invoice
inventory_movement
invoice
payment
open_item
fixed_asset
journal_entry
```

---

# 347. UUIDv7

Use for IDs.

---

# 348. Project Number

Human-friendly numbering via Platform Numbering.

---

# 349. Money

FinBase exact decimal.

---

# 350. Duration

Use exact duration representation.

No floating hours if precision issues.

---

# 351. Time Zone

Work Date is employee/project local business date according to configured semantics.

Approval timestamps are `Instant`.

---

# 352. Row Version

Mutable Project/Task/Budget Draft/Forecast uses optimistic concurrency.

---

# 353. Finalized Facts

Time/cost/billing facts with financial effect corrected through semantic records.

---

# 354. Effective Dating

Rates/team allocations/versioned config.

---

# 355. Project Hierarchy Indexes

Prevent unbounded recursive scans.

---

# 356. Project Source Ref

Typed:

```text
SourceType
SourceId
```

not generic unvalidated string.

---

# 357. External Reference

For migration/connectors.

---

# 358. Project Work Node Tree

Adjacency list acceptable initially with cycle guard.

Materialized path later only if measured.

---

# 359. Task Dependencies

Unique pair + cycle validation.

---

# 360. Team Effective Dates

No silent overwrite.

---

# 361. Rate Card Version

Published-used version immutable.

---

# 362. Budget Version

Approved version immutable.

---

# 363. Forecast Version

Approved snapshot immutable.

---

# 364. Baseline

Immutable snapshot.

---

# 365. Change Order

Approved immutable except correction/reversal process.

---

# 366. Actual Cost Fact

Append/correct, no ordinary edit.

---

# 367. Billable Charge

Correction-linked once invoiced.

---

# 368. Time Entry Before Approval

Mutable.

---

# 369. Time Entry After Cost/Billing

Correction only.

---

# 370. Project Close Record

Stores:

```text
ClosedAt
ClosedBy
CloseChecklistVersion
Exceptions
```

---

# 371. Contracts

Recommended:

```text
Todir.Projects.Contracts
```

---

# 372. Backend Namespaces

```text
Todir.Modules.Projects.Domain
Todir.Modules.Projects.Application
Todir.Modules.Projects.Infrastructure
Todir.Projects.Contracts
```

---

# 373. Internal Areas

Potential:

```text
Projects/
Work/
Resources/
Time/
Budgets/
Costs/
Forecasts/
Billing/
Changes/
Capital/
Closing/
Reporting/
```

---

# 374. Project Commands

```text
CreateProject
CreateProjectFromSalesSource
UpdateProjectDraft
ActivateProject
PutProjectOnHold
ResumeProject
CompleteProjectExecution
StartProjectFinancialClose
CloseProject
ReopenProject
CancelProject
```

---

# 375. Work Commands

```text
AddWorkNode
MoveWorkNode
CreateTask
UpdateTask
AssignTask
SetTaskStatus
AddTaskDependency
CompleteTask
```

---

# 376. Milestone Commands

```text
CreateMilestone
ReachMilestone
RecordMilestoneAcceptance
RejectMilestoneAcceptance
CancelMilestone
```

---

# 377. Team Commands

```text
AddProjectTeamMember
ChangeProjectRole
AllocateResource
EndResourceAllocation
ReplaceGenericResource
AssignResourceToTask
```

---

# 378. Time Commands

```text
CreateTimeEntry
UpdateDraftTimeEntry
SubmitTimeEntry
ApproveTimeEntry
RejectTimeEntry
FinalizeTimeEntry
CorrectTimeEntry
```

---

# 379. Budget Commands

```text
CreateBudget
CreateBudgetVersion
AddBudgetLine
SubmitBudgetVersion
ApproveBudgetVersion
ActivateBudgetVersion
```

---

# 380. Forecast Commands

```text
CreateForecastVersion
UpdateForecastLine
ApproveForecast
```

---

# 381. Billing Commands

```text
CreateBillingPlan
PublishBillingRuleVersion
GenerateBillableCharges
HoldBillableCharge
ReleaseBillableCharge
WriteOffBillableCharge
RequestProjectInvoice
ApplyInvoiceResult
CorrectBillableCharge
```

---

# 382. Change Commands

```text
CreateChangeRequest
SubmitChangeRequest
ApproveChangeRequest
RejectChangeRequest
CreateChangeOrder
ApplyChangeOrder
```

---

# 383. Capital Commands

```text
ClassifyProjectCostForCapitalization
SendProjectCostToCip
ResolveCapitalizationException
```

---

# 384. No Generic `SetActualCost`

Forbidden.

---

# 385. No Generic `SetInvoicedAmount`

Forbidden.

---

# 386. No Generic `SetCollectedAmount`

Forbidden.

---

# 387. No Generic `SetProjectProfit`

Forbidden.

---

# 388. No Generic `SetBudget`

Use versioned budget command after baseline.

---

# 389. No Generic `SetEmployeeSalaryRate`

Forbidden.

---

# 390. API Base

Recommended:

```text
/api/v1/projects
```

---

# 391. Project APIs

```text
GET  /projects
POST /projects
GET  /projects/{id}
PUT  /projects/{id}
POST /projects/{id}/activate
POST /projects/{id}/hold
POST /projects/{id}/resume
POST /projects/{id}/complete
POST /projects/{id}/close
```

---

# 392. Create From Sales API

```text
POST /projects/from-sales-source
```

with source ID/idempotency.

---

# 393. Work APIs

```text
GET  /projects/{id}/work
POST /projects/{id}/work-nodes
POST /projects/{id}/tasks
PUT  /tasks/{id}
POST /tasks/{id}/complete
```

---

# 394. Milestone APIs

```text
GET  /projects/{id}/milestones
POST /projects/{id}/milestones
POST /milestones/{id}/reach
POST /milestones/{id}/accept
```

---

# 395. Team APIs

```text
GET  /projects/{id}/team
POST /projects/{id}/team
POST /projects/{id}/allocations
```

---

# 396. Time APIs

```text
GET  /my/project-time
POST /time-entries
PUT  /time-entries/{id}
POST /time-entries/{id}/submit
POST /time-entries/{id}/approve
POST /time-entries/{id}/correct
```

---

# 397. Budget APIs

```text
GET  /projects/{id}/budgets
POST /projects/{id}/budgets
POST /budgets/{id}/versions
POST /budget-versions/{id}/approve
```

---

# 398. Forecast APIs

```text
GET  /projects/{id}/forecast
POST /projects/{id}/forecast-versions
POST /forecast-versions/{id}/approve
```

---

# 399. Cost APIs

```text
GET /projects/{id}/costs
GET /projects/{id}/commitments
GET /projects/{id}/financial-summary
```

Read-only from canonical facts.

---

# 400. Billing APIs

```text
GET  /projects/{id}/billing
GET  /projects/{id}/billable-charges
POST /projects/{id}/billable-charges/generate
POST /billable-charges/{id}/write-off
POST /projects/{id}/invoice-requests
```

---

# 401. Change APIs

```text
GET  /projects/{id}/changes
POST /projects/{id}/change-requests
POST /change-requests/{id}/submit
POST /change-requests/{id}/approve
POST /change-orders/{id}/apply
```

---

# 402. Capital APIs

```text
GET  /projects/{id}/capital-costs
POST /project-costs/{id}/capitalization-classification
POST /projects/{id}/cip-transfer
```

---

# 403. Portfolio APIs

```text
GET /project-portfolio
GET /project-portfolio/health
GET /project-portfolio/resource-demand
```

permission-aware.

---

# 404. API Field Redaction

Never return:

```text
salary
payslip detail
hidden customer margin
individual cost rate
confidential document
```

unless specific permission.

---

# 405. Error Codes

Examples:

```text
projects.project_not_found
projects.project_closed
projects.project_on_hold
projects.invalid_state_transition

projects.task_dependency_cycle
projects.task_project_mismatch

projects.employee_not_active
projects.resource_overallocated
projects.resource_not_on_team

projects.time_outside_project_window
projects.time_entry_locked
projects.time_approval_required

projects.cost_rate_missing
projects.billing_rate_missing
projects.rate_currency_mismatch

projects.budget_not_approved
projects.budget_version_stale
projects.budget_exceeded

projects.commitment_source_invalid
projects.cost_source_duplicate

projects.milestone_not_reached
projects.customer_acceptance_required

projects.billing_cap_exceeded
projects.charge_already_invoiced
projects.invoice_request_pending

projects.change_order_required
projects.close_blocked_open_commitment
projects.close_blocked_unbilled_charge
projects.close_blocked_pending_time

projects.capitalization_source_invalid
projects.permission_denied
projects.concurrent_change
projects.idempotency_conflict
```

---

# 406. Friendly Error — Missing Rate

> "لا يوجد سعر فوترة صالح لهذه الساعات في تاريخ العمل. راجع Rate Card قبل إنشاء الفاتورة."

---

# 407. Friendly Error — Close Blocked

Show actionable:

```text
3 pending timesheets
1 open purchase commitment
2 billable charges not resolved
```

not generic "cannot close".

---

# 408. Friendly Error — Budget

Show:

```text
Current Budget
Actual
Open Commitments
Requested Change
Expected Exposure
```

---

# 409. Idempotency

Mandatory for:

```text
Create Project From Sale
Time finalization
Cost source ingestion
Commitment ingestion
Billable charge generation
Invoice request
Milestone billing
Change Order apply
CIP transfer
Project close
Migration/import
```

---

# 410. Concurrency — Project Edit

Optimistic version.

---

# 411. Concurrency — Time Approval

Exact submitted version.

---

# 412. Concurrency — Billing Charge Generation

Same source/rule cannot create duplicate charge.

---

# 413. Concurrency — Invoice Request

Same eligible charge cannot be invoiced twice.

---

# 414. Concurrency — Budget

Two approved revisions cannot both become current without version check.

---

# 415. Concurrency — Change Order

Applied once.

---

# 416. Concurrency — Project Close

Atomically prevents normal new operational entries according to policy.

---

# 417. Integration — Directory

Consumes:

```text
Customer Party
Contacts
Addresses if needed for display
```

No duplicate Customer.

---

# 418. Integration — CRM

Won Opportunity can seed project creation.

CRM retains opportunity history.

---

# 419. Integration — Sales

Accepted Quotation/Sales Order can:

```text
Create Project
link Project
supply commercial source
```

---

# 420. Sales Source Revision

Change Order can link approved Sales revision.

---

# 421. Integration — HR

Consumes:

```text
Employee
Employment active state
Branch/Department context
Work calendar availability
```

---

# 422. Integration — Payroll

Consumes privacy-safe:

```text
Project labor cost rate/fact
```

if enabled.

Projects never reads Payroll tables.

---

# 423. Integration — Expenses

Expense can carry:

```text
ProjectId
WorkNode/Task optional
Billable flag/context
```

Projects consumes finalized allocation.

---

# 424. Integration — Purchasing

PO can carry:

```text
ProjectId
WorkNode?
CostCategory?
```

Projects consumes commitment.

---

# 425. Integration — Invoicing AP

Supplier Invoice produces actual source cost.

---

# 426. Integration — Inventory

Project material issue/return.

---

# 427. Integration — Invoicing AR

Projects requests invoices from billable charges.

---

# 428. Integration — Subledger

Projects composes:

```text
Outstanding
Overdue
```

for project invoice allocations.

---

# 429. Integration — Payments

Projects composes collections/cash view.

---

# 430. Integration — Fixed Assets

Capital Project sends eligible cost sources to CIP.

---

# 431. Integration — Ledger

Project dimension and reconciliation/analytics.

---

# 432. Integration Events

Potential:

```text
ProjectActivatedV1
ProjectCompletedV1
ProjectClosedV1

ProjectTimeFinalizedV1
ProjectBudgetApprovedV1
ProjectForecastApprovedV1

ProjectMilestoneReachedV1
ProjectMilestoneAcceptedV1

ProjectBillableChargeCreatedV1
ProjectInvoiceRequestedV1

ProjectChangeOrderAppliedV1
ProjectCostCapitalizationClassifiedV1
```

---

# 433. Source Events Consumed

Potential:

```text
ExpenseFinalized
ExpenseReversed

PurchaseOrderApproved/Amended/Closed
SupplierInvoiceFinalized/Corrected

InventoryIssuedToProject
InventoryReturnedFromProject

InvoiceFinalized/Corrected
PaymentAllocated

Payroll/ProjectLaborCostPublished

AssetCipCostAccepted
```

exact contracts/versioned.

---

# 434. Eventual vs Synchronous

Immediate project invariants use synchronous contracts when necessary.

Reporting/projections can consume async events.

---

# 435. No Broker For ACID Invariant

Follow Platform Kernel rules.

---

# 436. Outbox/Inbox

Cross-module async facts idempotent.

---

# 437. Project Actual Read Model Lag

If async projection:

show source sync state if delay matters.

---

# 438. Project Financial Summary

Rebuildable projection:

```text
Budget
Actual
Commitment
ETC
EAC
Contract Value
Billing Eligible
Invoiced
Collected
Expected Margin
```

---

# 439. Summary Is Not Canonical Editable Record

Absolute.

---

# 440. Reporting — Core Project

```text
Project Register
Project Portfolio
Project Status
Project Health
Projects By Customer
Projects By Manager
Projects By Branch
Projects By Type
```

---

# 441. Work Reports

```text
Tasks Due
Overdue Tasks
Milestones Due
Milestone Acceptance
Project Progress
```

---

# 442. Resource Reports

```text
Team Allocation
Resource Demand
Capacity vs Allocation
Overallocated Resources
Time By Project
Time By Employee
Time By Task
Billable vs Non-Billable Time
```

---

# 443. Financial Reports

```text
Budget vs Actual
Budget vs Actual vs Commitment
Cost Breakdown
Cost By Phase/Task
Cost By Category
Cost By Source
ETC / EAC
Variance At Completion
Project Profitability
Margin Forecast
```

---

# 444. Billing Reports

```text
Billing Eligible
Unbilled Time
Unbilled Expenses
Unbilled Purchases/Materials
Billable Charges Aging
Invoiced By Project
Collected By Project
Outstanding By Project
Billing Cap Usage
Write-Offs
```

---

# 445. Change Reports

```text
Change Requests
Approved Change Orders
Budget Changes
Contract Value Changes
Schedule Changes
```

---

# 446. Capital Project Reports

```text
Capitalizable Costs
Noncapitalizable Costs
Costs Sent To CIP
Costs Pending Classification
CIP Acceptance Status
```

---

# 447. Project Close Report

Shows unresolved close blockers.

---

# 448. Portfolio Executive View

Keep simple:

```text
Project
Health
Progress
Budget
Actual
Commitment
EAC
Expected Margin
Next Milestone
```

---

# 449. Margin Privacy

Portfolio only includes financial columns for authorized users.

---

# 450. Project Manager Home

Exception-driven:

```text
Projects At Risk
Overdue Milestones
Budget Warnings
Resource Conflicts
Pending Timesheets
Unbilled Eligible Work
Change Requests
Close Blockers
```

---

# 451. Employee My Projects

Simple:

```text
My Projects
My Tasks
My Time
Due Milestones relevant
```

---

# 452. Project Accountant Home

```text
Unapproved project cost
Unbilled charges
Budget overruns
Open commitments
Forecast loss
Projects ready to close
CIP pending
```

---

# 453. Search

By:

```text
Project Number
Name
Customer
Manager
Tag
Source Quotation/Order
```

---

# 454. Project List Target

```text
p95 <= 100-250ms
```

realistic dataset.

---

# 455. Project Workspace Target

Core overview:

```text
p95 <= 150-350ms
```

excluding large tabs.

---

# 456. Lazy Loading

Do not load:

```text
all tasks
all time
all costs
all invoices
all documents
```

on initial project open.

---

# 457. Timesheet Entry Target

Interactive:

```text
p95 <= 100-250ms
```

server write/read under normal load.

---

# 458. Financial Summary Target

```text
p95 <= 150-350ms
```

from projection.

---

# 459. Portfolio Target

Paged:

```text
p95 <= 200-500ms
```

depending financial composition.

---

# 460. No N+1

Project list cannot query per row:

```text
Customer
Manager
Budget
Actual
Commitment
Invoice
Payment
```

individually.



# 461. Resource Planning Scale

Test:

```text
100k employees/resource candidates
10k active projects
large allocation horizon
```

architecture stress where relevant.

---

# 462. Timesheet Scale

Test:

```text
millions of time entries
```

without full-table aggregation per screen.

---

# 463. Project Cost Scale

ProjectCostFact can become high-volume.

Index by:

```text
tenant/project/date
project/cost_category/date
source_type/source_id
```

---

# 464. Commitment Scale

Use projection/indexes by:

```text
project/state/source
```

---

# 465. Billing Charge Scale

Index:

```text
project/billing_state/date
source_type/source_id
invoice_ref
```

---

# 466. Task Scale

Large project can have tens of thousands tasks.

Keyset pagination/tree lazy loading.

---

# 467. No Full Tree Load

Load branch/children on demand.

---

# 468. PostgreSQL First

No OpenSearch Day 1.

---

# 469. Redis

Not required Day 1.

---

# 470. DWH/BI

Cross-project analytics can graduate later.

Operational truth remains PostgreSQL/module projections.

---

# 471. Background Jobs

Quartz/Worker for:

```text
financial projection rebuild
forecast suggestions
billing charge generation batch
resource availability refresh
large exports
import
close health checks
notifications
```

---

# 472. Notification Failure

Never corrupt Project truth.

---

# 473. Notifications

Examples:

```text
Task assigned
Milestone due
Time rejected
Budget warning
Forecast loss
Change request waiting
Billing eligible
Project close blocked
```

---

# 474. Notification Noise

User-configurable/role-aware.

---

# 475. Observability Metrics

Potential:

```text
projects_list_query_duration
projects_workspace_load_duration
projects_time_entry_duration
projects_cost_ingestion_lag
projects_commitment_ingestion_lag
projects_financial_projection_duration
projects_billable_charge_generation_duration
projects_invoice_request_failure_total
projects_over_budget_count
projects_forecast_loss_count
projects_close_blocker_count
```

No customer/project IDs as metric labels.

---

# 476. Tracing — Time Finalization

```text
Load Time
Authorize
Validate Employee/Project
Resolve Cost Rate
Resolve Billing Rate
Create Cost Fact
Create Billable Charge if applicable
Audit
Outbox
Commit
```

---

# 477. Tracing — Cost Ingestion

```text
Source event
Validate Project
Resolve allocation/category
Idempotency
Create/Reversal Cost Fact
Update projection
```

---

# 478. Tracing — Invoice Request

```text
Resolve Eligible Charges
Validate Customer/Currency
Group Charges
Request Invoicing
Persist request
Process result
Allocate Invoice
```

---

# 479. Tracing — Change Order

```text
Load Current Baseline/Budget
Validate Approval
Apply Budget/Scope/Schedule deltas
Link Sales amendment
Create new current version
Audit
Outbox
```

---

# 480. Logging

Do not log:

```text
salary
full commercial contracts
customer confidential documents
full time descriptions if sensitive
```

unnecessarily.

---

# 481. Migration Mission

Companies must move Projects from Excel/legacy tools without fake financial events.

---

# 482. Import — Project Master

Fields:

```text
Legacy Project ID
Project Number
Name
Type
Legal Entity
Branch
Customer Ref
Manager Employee Number
Start
End
State
Original Budget
Current Budget if known
Legacy Source Ref
```

---

# 483. Import — Work Structure

Optional:

```text
Phase
Task
Milestone
Parent refs
Dates
Status
```

---

# 484. Import — Team

Map by exact Employee Number/external ref.

No fuzzy auto-match.

---

# 485. Import — Timesheets

Choose historical boundary.

Do not import years of time detail unless needed.

---

# 486. Import — Budget

Preserve:

```text
Opening/Current approved budget
```

with migration provenance.

Do not fabricate missing revision history.

---

# 487. Import — Actual Costs

Strategies:

```text
SummaryOpeningActual
DetailedSourceLinked
NoHistoricalActual
```

depending data.

---

# 488. Import — Commitments

Open Purchase commitments should preferably come from Purchasing migration, not duplicate Project import.

---

# 489. Import — Invoices

Billing history should come from Invoicing migration/open items.

Projects links.

---

# 490. Import — Forecast

Current ETC/EAC can be imported as opening forecast snapshot.

---

# 491. Import — Change Orders

Import only if reliable.

Otherwise preserve current baseline and mark history unavailable.

---

# 492. Migration Provenance

Store:

```text
SourceSystem
LegacyProjectId
BatchId
ImportedAt
AsOfDate
```

---

# 493. No Fake Invoice

Absolute.

---

# 494. No Fake Timesheet

Do not create synthetic hours just to match cost.

---

# 495. Opening Project Actual

Can be aggregate category balances as of migration date.

Clearly marked.

---

# 496. Historical Project Close

Closed legacy projects can import summary/read-only.

---

# 497. Go-Live Boundary

After project go-live date:

normal semantic commands/source integrations.

---

# 498. Reconciliation Before Go-Live

For open Projects reconcile:

```text
Budget
Actual cost
Open commitments
Invoiced
Outstanding
```

against source systems/modules.

---

# 499. Product Acceptance — Simple Customer Project

User can:

```text
Create Project
Select Customer
Add tasks
Add team
Track time
See cost
Invoice milestone/time
```

without configuring WBS/baseline/forecast engine.

---

# 500. Product Acceptance — Internal Project

No customer/billing fields required.

---

# 501. Product Acceptance — Capital Project

Execution and cost work normally while capitalizable costs flow to Fixed Assets/CIP.

---

# 502. Product Acceptance — Project Manager

Can answer:

```text
What is late?
Who is working?
What remains?
What have we spent?
What are we committed to spend?
What do we expect to finish at?
```

---

# 503. Product Acceptance — Owner

Can answer:

```text
Which projects make money?
Which are forecast to lose?
Which are over budget?
Which have unbilled work?
Which have overdue collections?
```

with permissions.

---

# 504. Product Acceptance — Project Accountant

Can reconcile:

```text
actual cost
commitments
billing
invoice
collection
capital costs
```

to sources.

---

# 505. Product Acceptance — Employee

Can enter time in few steps.

No budget/accounting complexity required.

---

# 506. Product Acceptance — Sales

Can create project from won/accepted sale without duplicate Customer/commercial entry.

---

# 507. Product Acceptance — Finance

No manual spreadsheet needed to calculate actual/commitment/EAC/margin for normal project.

---

# 508. Test — Simple Project

Create/activate/add task/complete.

---

# 509. Test — Internal Project

No Customer required.

---

# 510. Test — Customer Project

Customer Party required according to type.

---

# 511. Test — Create From Quotation

One project on retry.

Commercial source preserved.

---

# 512. Test — Employee Team Member

Uses HR Employee.

---

# 513. Test — Inactive Employee

Cannot be newly allocated without permitted exception.

---

# 514. Test — Generic Resource

Can plan without fake Employee.

---

# 515. Test — Allocation vs Assignment

Independent.

---

# 516. Test — Resource Overallocation

Warn/block according to policy.

---

# 517. Test — HR Leave Availability

Availability changes, Project assignment history remains.

---

# 518. Test — Attendance Separation

Entering Project time does not create HR Attendance.

---

# 519. Test — Time Entry

Correct Employee/Project/Task/duration.

---

# 520. Test — Billable vs Worked Time

Separate quantities exact.

---

# 521. Test — Time Approval

Approved version exact.

---

# 522. Test — Time Correction After Cost

Creates correction, not history rewrite.

---

# 523. Test — Missing Cost Rate

Financial finalization blocked/warned by configured policy.

---

# 524. Test — Missing Billing Rate

Cost can finalize while billing remains exception if business permits.

---

# 525. Test — Rate Change

Historical time keeps old snapshot.

---

# 526. Test — Salary Privacy

Project Manager API does not expose salary/payslip.

---

# 527. Test — Simple Budget

One total.

---

# 528. Test — Detailed Budget

Categories sum exact.

---

# 529. Test — Budget Revision

Original preserved.

---

# 530. Test — Concurrent Budget Approval

One current version.

---

# 531. Test — Purchase Commitment

Approved PO appears as commitment.

---

# 532. Test — PO Partial Invoice

Actual increases/open commitment decreases without double count.

---

# 533. Test — PO Cancellation

Open commitment reduced.

---

# 534. Test — Expense Actual

Finalized expense creates cost once.

---

# 535. Test — Expense Reversal

Project cost reverses.

---

# 536. Test — Inventory Material Issue

Uses Inventory cost, not sales price.

---

# 537. Test — Material Return

Corrects cost.

---

# 538. Test — Labor Cost

Privacy-safe cost fact.

---

# 539. Test — Source Cost Duplicate Event

Idempotent.

---

# 540. Test — Actual Cost Cannot Edit

API/domain rejects.

---

# 541. Test — ETC

Manual remaining forecast exact.

---

# 542. Test — EAC

Formula exact with configured commitment treatment.

---

# 543. Test — Forecast Version

Old snapshot unchanged.

---

# 544. Test — Forecast Loss

Flag exact.

---

# 545. Test — Fixed Price Project

Actual costs do not change contract price.

---

# 546. Test — T&M Time Charge

Time × historical billing rate exact.

---

# 547. Test — Billable Expense Markup

Source Expense unchanged.

---

# 548. Test — Purchase Markup

Supplier cost and billable amount separate.

---

# 549. Test — Billing Cap

Over-cap handled explicitly.

---

# 550. Test — Charge Write-Off

Original eligible amount traceable.

---

# 551. Test — Milestone Reached Not Accepted

No billable charge when acceptance required.

---

# 552. Test — Milestone Accepted

One billing charge.

---

# 553. Test — Billing Generation Retry

No duplicate charge.

---

# 554. Test — Invoice Request Retry

No duplicate invoice request/document.

---

# 555. Test — Partial Invoice

Charge remaining amount exact if supported.

---

# 556. Test — Invoice Credit

Project billed projection correct.

---

# 557. Test — Payment Allocation

Collected/outstanding exact from Subledger/Payments.

---

# 558. Test — Invoiced Not Collected

Displays correctly.

---

# 559. Test — Change Request

No budget/scope effect until approved/applied.

---

# 560. Test — Change Order

Budget/contract/schedule deltas versioned.

---

# 561. Test — Change Order Retry

Applied once.

---

# 562. Test — Original Baseline

Never overwritten.

---

# 563. Test — Rebaseline

History retained.

---

# 564. Test — Project Complete With Open Cost

Execution completed but financial close blocked.

---

# 565. Test — Close With Open Commitment

Blocked or explicit approved exception.

---

# 566. Test — Closed Project New Time

Blocked.

---

# 567. Test — Closed Project Source Correction

Allowed through source correction/read model update.

---

# 568. Test — Capital Project Cost Classification

Capitalizable/noncapitalizable separate.

---

# 569. Test — Send Cost To CIP

Fixed Assets owns acceptance.

---

# 570. Test — Capital Cost Retry

One CIP source allocation.

---

# 571. Test — Project Cost Not Double Posted

Projects does not create second Ledger expense.

---

# 572. Test — Multi-Currency Expense Cost

Original/base preserved.

---

# 573. Test — Multi-Currency Billing

Rate/billing currency exact.

---

# 574. Test — FX Historical Stability

Old actual unchanged after rate changes.

---

# 575. Test — Cost Privacy Aggregate

Authorized manager sees total, not individual hidden rates.

---

# 576. Test — Cross Tenant RLS

Tenant isolation.

---

# 577. Test — Cross Project Authorization

Team member cannot guess other Project ID.

---

# 578. Test — Document Permission

Restricted commercial doc not returned.

---

# 579. Test — 1M Time Entries

Measured.

---

# 580. Test — Large Project Tree

Lazy and performant.

---

# 581. Test — Portfolio

No N+1.

---

# 582. Test — Cost Projection Rebuild

Matches source facts.

---

# 583. Test — Billing Projection Rebuild

Matches charges/invoices.

---

# 584. Property Test — Project Actual

```text
Project Actual
=
SUM(active ProjectCostFacts)
```

for selected scope/currency basis.

---

# 585. Property Test — Open Commitment

Cannot go below zero absent explicit reversal/credit semantics.

---

# 586. Property Test — Source Allocation

Project allocation does not exceed source allocatable amount.

---

# 587. Property Test — Billable Charge

Charge cumulative invoice allocation cannot exceed billable amount unless explicit overbilling rule.

---

# 588. Property Test — Time

Billable duration cannot exceed worked duration by default unless contract rule explicitly permits units not tied to worked time.

---

# 589. Property Test — Budget

Current approved budget equals original + approved deltas/current version semantics.

---

# 590. Property Test — Change

Unapproved Change does not alter current budget/contract/schedule.

---

# 591. Property Test — Closed Project

No ordinary operational commands accepted.

---

# 592. Architecture Test — No Employee Table

Projects uses HR Contracts.

---

# 593. Architecture Test — No Payroll Salary Read

Forbidden.

---

# 594. Architecture Test — No Expense Table Read

Use Expenses contract/events.

---

# 595. Architecture Test — No Purchase Table Direct Read

Use Purchasing contracts.

---

# 596. Architecture Test — No Inventory Table Read

Use Inventory contracts.

---

# 597. Architecture Test — No Invoice Creation Direct DB

Use Invoicing contract.

---

# 598. Architecture Test — No Payment Table Read

Use Payments/Subledger contracts.

---

# 599. Architecture Test — No Asset Book Mutation

Use Fixed Assets contracts.

---

# 600. Red-Team — Project = Task Board

Reject.

---

# 601. Red-Team — Project Actual Editable

Reject.

---

# 602. Red-Team — Budget Means Remaining Cash

Reject.

---

# 603. Red-Team — PO Is Actual Cost Immediately

Reject universal assumption.

It is commitment until actual-cost event according to source policy.

---

# 604. Red-Team — Commitment + Actual Double Count

Reject.

---

# 605. Red-Team — Timesheet Equals Attendance

Reject.

---

# 606. Red-Team — Salary Used Directly In Project UI

Reject.

---

# 607. Red-Team — Billing Rate Equals Cost Rate

Reject.

---

# 608. Red-Team — Milestone Completed Means Invoiced

Reject.

---

# 609. Red-Team — Invoice Means Collected

Reject.

---

# 610. Red-Team — Project Complete Means Closed

Reject.

---

# 611. Red-Team — Change Order Edits Original Budget

Reject.

---

# 612. Red-Team — Forecast Posts Accounting

Reject.

---

# 613. Red-Team — Billable Expense Changes Expense Value

Reject.

---

# 614. Red-Team — Project Creates Stock Movement

Reject.

---

# 615. Red-Team — Project Calculates Depreciation

Reject.

---

# 616. Red-Team — Capital Project Owns CIP Book

Reject; Fixed Assets owns.

---

# 617. Red-Team — Customer Project Requires Internal Customer Login

Reject.

---

# 618. Red-Team — WBS Mandatory

Reject.

---

# 619. Red-Team — Every Project Requires Timesheets

Reject.

---

# 620. Red-Team — Every Project Requires Billing

Reject.



# 621. Competitive Benchmark — Odoo 19

Useful current patterns from Odoo Project / Timesheets / Sales integration:

```text
Project + Tasks
Milestones
Timesheets
Profitability
Sales order/project linking
Milestone invoicing
Time and material invoicing
```

Reference:
- https://www.odoo.com/documentation/19.0/applications/services/project.html

TODIR adopts:
- simple project/task UX;
- milestone/time billing usability;
- project profitability visibility.

TODIR goes deeper on:
- commitment vs actual;
- forecast/ETC/EAC;
- change orders;
- capital projects;
- source-of-truth financial facts.

---

# 622. Competitive Benchmark — Dynamics 365 Project Operations

Useful patterns:

```text
Project Actuals
Cost vs Sales rates
Resource Bookings
Task Assignments
Generic Resources
Cost/Sales Budgets
Fixed Price and T&M
```

References:
- https://learn.microsoft.com/en-us/dynamics365/project-operations/actuals/actuals-overview
- https://learn.microsoft.com/en-us/dynamics365/project-operations/resource-management/booking-vs-assignment
- https://learn.microsoft.com/en-us/dynamics365/project-operations/resource-management/resource-management-key-concepts
- https://learn.microsoft.com/en-us/dynamics365/project-operations/budget/cost-and-sales-budget-management

TODIR adopts:
- Actuals as source-derived, not editable;
- Booking/Allocation separate from Task Assignment;
- Generic resource planning;
- cost/billing rate separation.

TODIR keeps simpler defaults.

---

# 623. Competitive Benchmark — Oracle Fusion Project Management

Useful patterns:

```text
Project Costing
Billing/revenue methods
Milestone billing
Capital Projects
Capitalizable vs noncapitalizable costs
Asset creation from project costs
```

References:
- https://docs.oracle.com/en/cloud/saas/project-management/25d/oapjb/milestone-billing-and-revenue-recognition.html
- https://docs.oracle.com/en/cloud/saas/project-management/25c/oapfm/invoice-and-revenue-method-components.html
- https://docs.oracle.com/en/cloud/saas/project-management/26a/oapjc/capital-projects-processing.html

TODIR adopts:
- milestone reached/accepted/billable separation;
- capital project cost flow;
- flexible billing method architecture.

---

# 624. Competitive Benchmark — NetSuite SuiteProjects

Useful patterns:

```text
Fixed Fee
Time-based charges
Expense-based charges
Purchase-based charges
Markup/markdown
Billing caps
Forecast charges
```

Reference:
- https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_3752830510.html

TODIR adopts first-class `ProjectBillableCharge`.

---

# 625. Competitive Benchmark — SAP Project System

Useful patterns:

```text
WBS
Planned cost
Actual cost
Commitments
Availability control
Settlement
```

References:
- https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/4dd8cb7b1c484b4b93af84d00f60fdb8/f711d553088f4308e10000000a174cb4.html
- https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/4dd8cb7b1c484b4b93af84d00f60fdb8/1448cfa991784030b1a4a39f425cfd1c.html

TODIR adopts:
- commitment as first-class;
- optional WBS;
- budget control.

TODIR avoids mandatory SAP-level setup for ordinary projects.

---

# 626. Competitive Benchmark — Acumatica Project Accounting

Useful patterns:

```text
Committed Costs
Change Requests
Change Orders
Budget impact
Revenue impact
Forecast
```

Reference:
- https://beacon.acumatica.com/r/2026-R2/Project-Accounting-Guide

TODIR adopts:
- explicit Change Request → Change Order;
- commitment/budget/revenue impact history.

---

# 627. Competitive Benchmark — Deltek Vantagepoint

Useful patterns:

```text
Actual Cost
ETC
EAC
Cost/Billing budgets
Project profitability
Professional-services economics
```

Reference:
- https://help.deltek.com/Product/Vantagepoint/7.2/proj_rev_cost_tab_of_project_review_form.html

TODIR adopts:
- forecast-to-completion as core;
- clear project margin.

---

# 628. Competitive Benchmark — monday.com

Useful patterns:

```text
Portfolio
Gantt
Workload
Planned effort
Visual resource planning
```

Reference:
- https://support.monday.com/hc/en-us/articles/360014437599-Project-management-with-monday-com

TODIR takes UX clarity, not financial architecture.

---

# 629. Competitive Product Rule

Competitor capability proves a business need.

It does not mean TODIR copies every screen, field or workflow.

---

# 630. Competitive Product Rule — Simplicity

TODIR target:

```text
Odoo/monday-like usability
+
Dynamics/Oracle/SAP/NetSuite-like financial correctness
```

without importing their setup burden by default.

---

# 631. Project Setup Wizard — Small Company

Recommended:

```text
1. Name
2. Customer? if customer project
3. Manager
4. Start/Target End
5. Billing Model if customer project
6. Budget optional
7. Template optional
8. Create
```

---

# 632. Advanced Setup

Only when enabled:

```text
WBS
resource plan
rate card
detailed budget
billing rules
baseline
capital project config
approvals
```

---

# 633. Project Type Defaults

Project Type can set:

```text
Customer required?
Billing enabled?
Timesheet enabled?
Budget enabled?
Resource planning enabled?
Capitalization enabled?
Approval policy?
Close checklist?
```

---

# 634. Project Type Is Not Hardcoded Workflow Prison

Defaults/requirements, not hundreds of code branches.

---

# 635. Feature Toggles By Capability

Examples:

```text
Timesheets
Resource Planning
Detailed Budget
Forecast
Change Orders
Billing
Capital Projects
```

hidden if inactive.

---

# 636. Default Customer Project

Recommended:

```text
Tasks = ON
Team = ON
Timesheet = optional
Budget = optional
Billing = based on Sales source
Forecast = simple
Change Orders = simple
Advanced WBS = OFF
```

---

# 637. Default Internal Project

```text
Customer = hidden
Billing = OFF
Tasks = ON
Budget = optional
Costs = ON
```

---

# 638. Default Capital Project

```text
Billing = OFF
Costs = ON
Capital classification = ON
CIP integration = ON
Detailed budget = recommended
```

---

# 639. Project Lifecycle Activation Gate

Before Active validate only enabled essentials.

---

# 640. Customer Project Activation

Possible minimum:

```text
Legal Entity
Customer
Manager/owner
valid commercial/billing context if billing enabled
```

---

# 641. Internal Project Activation

No customer/billing blocker.

---

# 642. Capital Project Activation

Fixed Assets/CIP integration can be validated before cost capitalization, not necessarily before planning work begins.

---

# 643. Rate Readiness

T&M project can start work even if billing rate missing only if policy allows, but system must prominently flag:

```text
Unpriced billable work
```

---

# 644. Budget Control Modes

```text
None
Warn
RequireApproval
Block
```

---

# 645. Budget Control Basis

Can evaluate:

```text
Actual only
Actual + Open Commitment
Forecast/EAC
```

company policy.

---

# 646. Recommended Budget Control

For purchase authorization:

```text
Actual + Open Commitment + proposed commitment
```

against controlled budget.

---

# 647. Budget Check Does Not Replace Purchasing Approval

Projects supplies budget availability result.

Purchasing owns purchase approval workflow.

---

# 648. Budget Override

Typed exception with:

```text
amount
reason
approver
budget context
```

---

# 649. Availability Control Seam

Future enterprise capability.

No complex SAP-style availability control needed for every SME.

---

# 650. Resource Utilization

Can calculate:

```text
Allocated hours / available capacity
Billable hours / available capacity
```

with explicit definition.

---

# 651. Utilization Is Not Attendance Compliance

Separate.

---

# 652. Skills Matching

Future HR Qualifications integration.

---

# 653. Resource Request

Advanced:

```text
Need role/skill/date/capacity
→ Resource Manager fulfills
```

---

# 654. Named vs Generic Resource

Both supported.

---

# 655. Soft vs Hard Booking

Advanced optional.

Simple allocation uses one state.

---

# 656. Calendar Scheduling

Task planned dates can use Project calendar/HR availability.

Do not build advanced scheduling solver Day 1.

---

# 657. Holidays

HR/Organization calendar source.

---

# 658. Workload View

Projection.

Does not modify HR schedule.

---

# 659. Task Effort

Separate:

```text
Estimated Effort
Actual Time
Remaining Effort
```

---

# 660. Remaining Effort

Can feed ETC labor forecast.

---

# 661. Task Progress

Can derive/manual.

Do not force hours-based percent for all project types.

---

# 662. Milestone Date Change

Preserve baseline/original if project baselined.

---

# 663. Customer Acceptance Rejection

Can reopen work/revision while preserving rejected attempt/evidence.

---

# 664. Deliverable Revision

Versioned FileRefs.

---

# 665. Project Billing Period

For T&M:

```text
weekly
monthly
custom
```

billing run groups eligible charges.

---

# 666. Billing Cutoff

Example:

```text
Invoice monthly through month end
```

Time/expense posted after cutoff flows next billing period or correction policy.

---

# 667. Late Time After Invoice

Do not mutate old invoice.

Create next-period charge/adjustment.

---

# 668. Late Expense After Invoice

Same principle.

---

# 669. Billing Review

Optional:

```text
Generate Draft Charges
→ Project Manager/Finance review
→ Invoice Request
```

---

# 670. Automatic Billing

Can be enabled after trust.

Default for complex projects should allow review.

---

# 671. Project Billing Grouping

Invoice can group by:

```text
Project
Milestone
Task/Phase
Charge Type
Contract line
```

Invoicing renders legal document.

---

# 672. Multiple Projects One Invoice

Possible later/if Invoicing supports, but source allocations must remain exact.

---

# 673. One Project Multiple Invoices

Normal.

---

# 674. Billing Currency

Contract/project billing currency.

---

# 675. Expense Original Currency vs Billing Currency

Charge converts through commercial FX rule if needed.

Do not overwrite source cost.

---

# 676. Customer Reimbursable Expense

Can be:

```text
At Cost
At Cost + Markup
Fixed Reimbursement
Nonbillable
```

---

# 677. Customer-Reimbursable Purchase

Same.

---

# 678. Billing Approval

Platform Approvals optional for high-value/manual charges.

---

# 679. Manual Billable Charge

Allowed only controlled cases:

```text
approved fixed adjustment
contract fee
change-order fee
```

requires source/reason.

---

# 680. Manual Charge Cannot Fake Time/Expense

Use correct ChargeType.

---

# 681. Project Credit

Customer commercial credit handled by Invoicing correction.

Projects consumes result.

---

# 682. Project Retainer

Future Contract/Subscription/Prepayment capability.

Do not model as random negative billing charge.

---

# 683. Advance Billing

Future optional if customer prepayments/deposits supported via Invoicing/Subledger.

---

# 684. Revenue Contract Boundary

Do not build full IFRS 15/ASC 606 revenue contract accounting in Projects MVP.

---

# 685. Construction Progress Billing

Design seam:

```text
Measurement/Progress Certificate
→ Billing Eligibility
```

future sector-specific capability.

---

# 686. Retention/Holdback

Construction-specific later.

Do not pollute simple projects now.

---

# 687. Subcontract Management

Purchasing owns commercial subcontract PO/commitment.

Projects supplies project scope/cost dimension.

Advanced construction subcontract module later if evidence.

---

# 688. Project Procurement Request

Projects may initiate request with:

```text
Project
Work Node
Need
Budget context
```

Purchasing owns Purchase Request/PO.

---

# 689. Project Purchase Visibility

Read-only composition:

```text
PO
Supplier
Committed
Received
Invoiced
Open
```

---

# 690. Project Expense Visibility

Read-only composition.

---

# 691. Project Material Visibility

Read-only Inventory composition.

---

# 692. Project Invoice Visibility

Read-only Invoicing composition.

---

# 693. Project Collection Visibility

Read-only Subledger/Payments composition.

---

# 694. One Workspace, Many Owners

Project workspace can feel unified while backend truth remains modular.

This is a key TODIR principle.

---

# 695. Data Once Principle

Do not ask user to re-enter:

```text
Customer
Employee
Expense amount
PO value
Invoice value
Payment amount
Material cost
```

if canonical module already knows it.

---

# 696. Explain Number

Every financial card should support drilldown:

```text
Actual Cost 450,000
→ Labor 200,000
→ Expenses 40,000
→ Supplier Cost 150,000
→ Materials 60,000
```

then source transactions.

---

# 697. Explain EAC

Show:

```text
Actual
Open commitments
Remaining forecast
assumptions
```

---

# 698. Explain Margin

Show revenue basis + cost basis.

---

# 699. No Black-Box Dashboard

Numbers must be traceable.

---

# 700. Project Close Acceptance

Reviewer can explain why close is allowed.

---

# 701. Formal Acceptance — Simple Project User

A non-PMO user creates/manages a simple project without seeing:

```text
WBS
EAC jargon
commitment accounting
charge engine
```

unless needed.

---

# 702. Formal Acceptance — Professional Services

Scenario:

```text
Customer
T&M
Team
Timesheets
Expenses
Billing rates
Monthly invoice
Actual cost
Margin
```

works end-to-end.

---

# 703. Formal Acceptance — Fixed Price

Scenario:

```text
1M contract
milestones
actual cost
forecast
change order
billing
margin
```

works.

---

# 704. Formal Acceptance — Internal Project

Scenario:

```text
budget
employees
expenses
purchases
materials
no billing
```

works.

---

# 705. Formal Acceptance — Capital Project

Scenario:

```text
project costs
capital classification
CIP
final assets
```

works without Projects owning depreciation.

---

# 706. Formal Acceptance — Change Order

Scenario:

```text
Original 1M
Customer adds 150k
Cost +80k
End +3 weeks
```

history clearly shows original/current.

---

# 707. Formal Acceptance — Forecast

Project Manager updates ETC; owner sees EAC/margin.

No accounting entry.

---

# 708. Formal Acceptance — Commitments

Approved PO visible before supplier bill.

No false available budget.

---

# 709. Formal Acceptance — Close

Execution complete but outstanding cost/billing prevents financial close until resolved/exception.

---

# 710. Agent Implementation Philosophy

Implement Projects as business/financial vertical slices.

Wrong:

```text
all task screens
then later add finance
```

Correct:

```text
prove simple Project
then time/cost
then budget/commitment
then billing/profitability
```

---

# 711. Agent Precedence

If conflict:

```text
23 Projects pack
→ owning domain pack for source truth
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ Country Pack
→ approved ADR
```

---

# 712. Owning Domain Packs Override

Examples:

```text
HR defines Employee truth.
Purchasing defines PO commitment truth.
Inventory defines material cost.
Invoicing defines Invoice.
Payments/Subledger define collection.
Fixed Assets defines CIP.
Ledger defines Journal.
```

Projects must adapt contracts, not duplicate.

---

# 713. Agent Must Read

At minimum:

```text
01
02
03
04
06
09
10
11
12
13
14
15
16
17
18
21
22
23
all AGENTS.md
approved ADRs
actual public contracts
```

---

# 714. Agent Baseline Report

Before code, map:

```text
Directory Customer contract
Sales accepted source
HR Employee/availability
Payroll privacy-safe cost contract
Expenses Project allocation
Purchasing Project/commitment refs
Invoicing AP/AR source links
Inventory project issue/cost
Subledger invoice allocation
Payments collection
Assets CIP input
Ledger project dimension
Files
Approvals
Audit
Numbering
Outbox/Inbox
RLS
```

---

# 715. Agent Must Not Invent Mini Modules

If a required contract is missing:

```text
extend owning module contract through approved design
```

Never create:

```text
project_supplier_invoice
project_payment
project_stock
project_employee_salary
```

duplicates.

---

# 716. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL
What real project-management/financial problem this solves.

REAL-WORLD WORKFLOW

SOURCE OF TRUTH

STATE / INVARIANTS

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

DATABASE REQUIREMENTS

INTEGRATION REQUIREMENTS

FINANCIAL REQUIREMENTS

SECURITY / PRIVACY REQUIREMENTS

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
- reconciliation evidence
- performance evidence
- security/privacy evidence
- unresolved decisions
- risks
```

---

# 717. Implementation Sequence

```text
PJ00 Repository / Dependency Verification
PJ01 Module Boundary + Schema + RLS
PJ02 Project Core + Types
PJ03 Project Template Foundation
PJ04 Simple Tasks / Phases
PJ05 Milestones / Acceptance
PJ06 Project Team + Roles
PJ07 Resource Allocation + Generic Resources
PJ08 Timesheets
PJ09 Timesheet Approval / Correction
PJ10 Privacy-Safe Labor Cost
PJ11 Billing Rate Cards
PJ12 Project Cost Fact Foundation
PJ13 Expenses Cost Integration
PJ14 Purchasing Commitment Integration
PJ15 Supplier Actual Cost Integration
PJ16 Inventory Material Cost Integration
PJ17 Budget Core
PJ18 Budget Versions / Control
PJ19 Forecast / ETC / EAC
PJ20 Financial Summary Projection
PJ21 Fixed Price Billing
PJ22 Milestone Billing
PJ23 T&M Time Billing
PJ24 Billable Expenses / Purchases / Materials
PJ25 Billable Charge Engine
PJ26 Invoice Request / Allocation
PJ27 Subledger / Collection Projection
PJ28 Profitability
PJ29 Change Requests / Change Orders
PJ30 Baselines / Rebaseline
PJ31 Project Close
PJ32 Capital Project / CIP Integration
PJ33 Portfolio / Resource Views
PJ34 Documents / Timeline
PJ35 Notifications / Exceptions
PJ36 Migration
PJ37 Security / Privacy
PJ38 Performance / Scale
PJ39 Runbooks / Recovery
PJ40 Simple User Acceptance
PJ41 Professional Services Acceptance
PJ42 Project Accountant Acceptance
PJ43 Capital Project Acceptance
PJ44 Sales/Billing Acceptance
PJ45 Cross-Module Financial Reconciliation
PJ46 Cross-Module E2E
PJ47 Release Gate
```

---

# 718. PJ00 Goal

Inspect existing repository/contracts before implementation.

---

# 719. PJ00 Exit Gate

Dependency matrix:

```text
Exists
Partial
Missing
Conflict
```

No duplicate ownership planned.

---

# 720. PJ01 Goal

Create isolated Projects module.



# 721. PJ01 Build

```text
Todir.Modules.Projects.Domain
Todir.Modules.Projects.Application
Todir.Modules.Projects.Infrastructure
Todir.Projects.Contracts
business_projects
ProjectsDbContext
module manifest
RLS
architecture tests
```

---

# 722. PJ01 Exit Gate

No direct cross-module DbContext access.

---

# 723. PJ02 Goal

Create simple correct Project core.

---

# 724. PJ02 Build

```text
Project
Project Type
Lifecycle
Manager
Customer optional
Branch
Source
```

---

# 725. PJ02 Exit Gate

Customer/Internal/Capital types work without irrelevant required fields.

---

# 726. PJ03 Goal

Reusable project setup without template lock-in.

---

# 727. PJ03 Build

Versioned templates.

---

# 728. PJ03 Exit Gate

Editing template cannot mutate existing Project.

---

# 729. PJ04 Goal

Simple work execution.

---

# 730. PJ04 Build

```text
Phase optional
Task
Owner
Dates
Priority
Status
Dependency foundation
```

---

# 731. PJ04 Exit Gate

User can run Project without WBS knowledge.

---

# 732. PJ05 Goal

Formal milestone/deliverable business semantics.

---

# 733. PJ05 Build

```text
Milestone
Reached
Customer Acceptance
Deliverable/evidence
```

---

# 734. PJ05 Exit Gate

Reached, Accepted and Invoiced remain separate.

---

# 735. PJ06 Goal

Project Team with HR ownership intact.

---

# 736. PJ06 Build

```text
Employee refs
Project roles
Effective dates
External/generic seam
```

---

# 737. PJ06 Exit Gate

No employee duplication.

---

# 738. PJ07 Goal

Capacity/resource planning.

---

# 739. PJ07 Build

```text
Allocation
Generic resource demand
Task assignment distinction
```

---

# 740. PJ07 Exit Gate

Allocation and Task Assignment reconcile but remain independent.

---

# 741. PJ08 Goal

Fast project time capture.

---

# 742. PJ08 Build

```text
My Time
Project
Task
Date
Duration
Billable flag/context
```

---

# 743. PJ08 Exit Gate

Timesheet has zero effect on HR Attendance truth.

---

# 744. PJ09 Goal

Controlled Time approval/correction.

---

# 745. PJ09 Exit Gate

Financially used Time is immutable/corrective.

---

# 746. PJ10 Goal

Labor cost without salary leakage.

---

# 747. PJ10 Build

Privacy-safe Payroll/Finance cost-rate/fact contract.

---

# 748. PJ10 Exit Gate

Project financials work while manager cannot inspect hidden salary.

---

# 749. PJ11 Goal

Versioned customer billing rates.

---

# 750. PJ11 Build

```text
Rate Card
Role rate
Employee override
Currency
Effective dates
```

---

# 751. PJ11 Exit Gate

Historical Time uses exact rate snapshot.

---

# 752. PJ12 Goal

Canonical normalized Project Cost Fact layer.

---

# 753. PJ12 Build

```text
Source refs
Cost category
Original/base currency
Correction relationship
Idempotency
```

---

# 754. PJ12 Exit Gate

Actual cost cannot be edited manually.

---

# 755. PJ13 Goal

Expenses integration.

---

# 756. PJ13 Exit Gate

Finalized Project Expense appears exactly once and reverses correctly.

---

# 757. PJ14 Goal

Purchase commitments.

---

# 758. PJ14 Exit Gate

Approved/open PO exposure visible before invoice.

---

# 759. PJ15 Goal

Supplier actual cost.

---

# 760. PJ15 Exit Gate

Supplier invoice actual and open commitment reconcile without double count.

---

# 761. PJ16 Goal

Inventory material cost.

---

# 762. PJ16 Exit Gate

Material Issue/Return cost matches Inventory valuation.

---

# 763. PJ17 Goal

Simple and detailed Cost Budget.

---

# 764. PJ17 Exit Gate

Simple one-number budget works; detail optional.

---

# 765. PJ18 Goal

Budget control/versioning.

---

# 766. PJ18 Build

```text
Original
Current approved
Revisions
Warn/Approve/Block modes
```

---

# 767. PJ18 Exit Gate

No approved history overwrite.

---

# 768. PJ19 Goal

Project forecasting.

---

# 769. PJ19 Build

```text
ETC
EAC
Forecast versions
commitment inclusion semantics
```

---

# 770. PJ19 Exit Gate

Project manager can explain EAC from underlying forecast.

---

# 771. PJ20 Goal

Fast financial summary.

---

# 772. PJ20 Build

Projection:

```text
Budget
Actual
Commitment
ETC
EAC
Contract
Billing Eligible
Invoiced
Collected
Margin
```

---

# 773. PJ20 Exit Gate

Projection rebuild equals sources.

---

# 774. PJ21 Goal

Fixed Price billing foundation.

---

# 775. PJ21 Build

```text
Fixed amount
schedule
contract/current value
billing plan
```

---

# 776. PJ21 Exit Gate

Actual cost does not alter fixed customer price.

---

# 777. PJ22 Goal

Milestone billing.

---

# 778. PJ22 Exit Gate

Contract acceptance conditions respected; one eligible charge.

---

# 779. PJ23 Goal

Time & Materials billing.

---

# 780. PJ23 Exit Gate

Approved billable time × historical rate = charge.

---

# 781. PJ24 Goal

Rebill source costs correctly.

---

# 782. PJ24 Build

```text
Expenses
Purchases
Materials
At cost
Markup
Nonbillable
```

---

# 783. PJ24 Exit Gate

Source cost remains unchanged.

---

# 784. PJ25 Goal

One Billable Charge engine for all billing sources.

---

# 785. PJ25 Exit Gate

Caps/write-offs/eligibility/retries traceable.

---

# 786. PJ26 Goal

Invoicing orchestration.

---

# 787. PJ26 Build

```text
Invoice Request
Charge grouping
Invoicing result
Invoice allocation
```

---

# 788. PJ26 Exit Gate

Projects never becomes legal Invoice engine.

---

# 789. PJ27 Goal

Receivable/collection visibility.

---

# 790. PJ27 Exit Gate

Invoiced, Outstanding and Collected match canonical Subledger/Payments.

---

# 791. PJ28 Goal

Trustworthy profitability.

---

# 792. PJ28 Build

```text
Actual margin
Expected final margin
Cost breakdown
Revenue basis labels
```

---

# 793. PJ28 Exit Gate

Every number drillable/explainable.

---

# 794. PJ29 Goal

Project Change Management.

---

# 795. PJ29 Build

```text
Change Request
Impact
Approval
Change Order
Budget/contract/schedule deltas
```

---

# 796. PJ29 Exit Gate

Original scope/budget/value retained.

---

# 797. PJ30 Goal

Baseline/variance.

---

# 798. PJ30 Exit Gate

Rebaseline cannot hide historical variance.

---

# 799. PJ31 Goal

Correct project completion/financial close.

---

# 800. PJ31 Build

```text
Execution Complete
Financial Closing
Close blockers
Exceptions
Close
Reopen
```

---

# 801. PJ31 Exit Gate

Completed Project with unresolved financial facts cannot silently become fully Closed.

---

# 802. PJ32 Goal

Capital Projects.

---

# 803. PJ32 Build

```text
Capital classification
CIP candidate
Fixed Assets handoff
acceptance/error feedback
```

---

# 804. PJ32 Exit Gate

Projects does not own CIP Books/depreciation.

---

# 805. PJ33 Goal

Portfolio/resource visibility.

---

# 806. PJ33 Exit Gate

Managers get fast portfolio without loading project internals per row.

---

# 807. PJ34 Goal

Documents/timeline.

---

# 808. PJ34 Exit Gate

Files secure; timeline traceable.

---

# 809. PJ35 Goal

Exception-driven notifications.

---

# 810. PJ35 Exit Gate

No notification failure changes core state.

---

# 811. PJ36 Goal

Safe migration.

---

# 812. PJ36 Exit Gate

Open Projects reconcile with source modules and migration provenance.

---

# 813. PJ37 Goal

Security/privacy red-team.

---

# 814. PJ37 Attack Cases

```text
Cross-tenant project read
Team member reads unrelated project
Project manager reads salary
Forged EmployeeId
Forged CustomerId
Forged FileRef
Unauthorized budget revision
Unauthorized billable charge write-off
Unauthorized change order
Unauthorized invoice request
Closed project data mutation
Capital cost classification abuse
Large export leakage
```

---

# 815. PJ37 Exit Gate

RLS + app authorization + field redaction + Files access pass.

---

# 816. PJ38 Goal

Performance and scale.

---

# 817. PJ38 Load Tests

```text
100k Projects
10k active Projects
1M+ Tasks
5M+ Time Entries
10M Project Cost Facts architecture stress
Large commitment feed
Large portfolio
Billing run
Financial projection rebuild
```

---

# 818. PJ38 Query Plans

Use real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot paths.

---

# 819. PJ38 Exit Gate

Targets pass or evidence-based ADR.

---

# 820. PJ39 Goal

Operational recovery without database surgery.

---

# 821. PJ39 Runbooks

Required:

```text
Source cost duplicate
Commitment projection mismatch
Missing rate
Wrong time approval
Incorrect billing charge before invoice
Invoice request failed/unknown
Invoice allocation mismatch
Budget projection mismatch
Change Order partially failed
Project close blocked unexpectedly
CIP handoff rejected
Financial summary projection rebuild
Import reconciliation mismatch
DR restore validation
```

---

# 822. PJ39 Forbidden Runbook

Never ordinary support fix:

```text
UPDATE project SET actual_cost=...
UPDATE project SET invoiced=...
UPDATE project SET margin=...
DELETE billed_time_entry...
UPDATE budget SET original_amount=...
```

---

# 823. PJ39 Exit Gate

Critical failures have semantic correction/recovery.

---

# 824. PJ40 Simple User Acceptance

Use non-PMO business user.

---

# 825. PJ40 Scenario

```text
Create project
choose customer
add tasks
add team
update work
complete milestone
see project health
```

without financial jargon.

---

# 826. PJ40 Pass Criteria

```text
few steps
clear status
no enterprise setup required
no duplicate data
```

---

# 827. PJ41 Professional Services Acceptance

Reviewer runs:

```text
T&M project
team
time
expenses
monthly billing
invoice
actual cost
margin
```

---

# 828. PJ41 Pass Criteria

No spreadsheet to calculate billable work or margin.

---

# 829. PJ42 Project Accountant Acceptance

Reviewer handles:

```text
Budget
Commitments
Actual
Forecast
EAC
Billing
Close
```

---

# 830. PJ42 Pass Criteria

Every project financial number reconciles/drills to source.

---

# 831. PJ43 Capital Project Acceptance

Reviewer:

```text
Build Factory Project
→ collect costs
→ classify
→ CIP
→ final asset
```

---

# 832. PJ43 Pass Criteria

Noncapitalizable cost remains project cost; capitalizable cost accepted by Fixed Assets once.

---

# 833. PJ44 Sales/Billing Acceptance

Reviewer:

```text
Accepted Quotation
→ Project
→ Change Order
→ Milestone
→ Invoice
→ Collection
```

---

# 834. PJ44 Pass Criteria

Sales commercial history, Project delivery and Invoice truth remain separate.

---

# 835. PJ45 Cross-Module Financial Reconciliation

Validate:

```text
Project Expense Costs
= Expenses project allocations

Project Purchase Commitments
= Purchasing open commitment allocations

Project Supplier Actual
= Invoicing AP project allocations

Project Material Cost
= Inventory project valuation facts

Project Labor Cost
= approved privacy-safe labor cost facts

Project Invoiced
= Invoicing project allocations

Project Collected/Outstanding
= Subledger/Payments allocations

Capital Costs Sent
= Fixed Assets accepted CIP source allocations
```

---

# 836. PJ45 Exit Gate

Zero unexplained differences.

---

# 837. PJ46 Cross-Module E2E — Fixed Price

```text
CRM/Sales
→ Accepted Quotation
→ Project
→ Tasks/Team
→ Expenses/Purchases/Materials
→ Actual/Commitment/EAC
→ Milestone accepted
→ Invoice
→ Payment
→ Profitability
→ Close
```

---

# 838. PJ46 E2E — T&M

```text
Employee
→ Project Time
→ Approval
→ Labor Cost
→ Billable Charge
→ Invoice
→ Payment
```

---

# 839. PJ46 E2E — Internal

```text
Internal Project
→ Budget
→ Time/Expense/Purchase
→ EAC
→ Complete
→ Close
```

No customer billing.

---

# 840. PJ46 E2E — Capital

```text
Capital Project
→ Supplier/Expense/Inventory cost
→ Project Actual
→ Capitalizable
→ CIP
→ Fixed Assets
```

---

# 841. PJ46 E2E — Change Order

```text
Original Scope
→ Change Request
→ Customer/Sales approval
→ Change Order
→ Budget/Contract/Schedule update
→ new billing
```

---

# 842. PJ46 Exit Gate

All ownership/invariants pass on real PostgreSQL integration tests.

---

# 843. PJ47 Release Gate

Projects does not ship because Tasks work.

It ships when work, cost, commitments, billing and profitability can be trusted together.

---

# 844. PJ47 Required Sign-Off

```text
Product Architect
Project Manager / PMO reviewer
Professional Services reviewer
Project Accountant / Finance
Sales reviewer
HR/Payroll reviewer
Purchasing/Inventory reviewer
Fixed Assets reviewer
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 845. PJ47 Mandatory Evidence

```text
Automated tests
RLS/security report
Salary/cost privacy tests
Budget/commitment/actual reconciliation
Timesheet correction tests
Rate snapshot tests
Billing charge reconciliation
Invoice allocation reconciliation
Forecast/EAC vectors
Change Order history
Capital Project/CIP tests
Performance benchmarks
Simple user acceptance
Professional services acceptance
Project accountant acceptance
Cross-module E2E
Known limitations
```

---

# 846. Build Now — MUST

```text
Project Core
Customer/Internal/Capital project types
Templates
Phases/Tasks
Milestones
Team
Resource allocation foundation
Timesheets
Privacy-safe labor cost
Billing rate cards
Project Cost Facts
Expense cost integration
Purchase commitments
Supplier actual costs
Inventory material cost
Simple/detailed budgets
Budget versions
Forecast / ETC / EAC
Financial summary
Fixed Price billing
Milestone billing
T&M billing
Billable Expenses/Purchases/Materials
Billable Charges
Invoice request/allocation
Collection view
Profitability
Change Requests/Orders
Simple baselines
Project Close
Capital Project/CIP integration
Permissions/RLS/Audit
Reports
Migration
Performance
Runbooks
Acceptance gates
```

---

# 847. Build Later / Evidence-Based

```text
Primavera-grade scheduling
Advanced critical path / PERT
Earned Value Management
Government contract accounting
Complex construction progress certificates
Retention/Holdback
Advanced subcontract module
Program/Portfolio hierarchy
Advanced resource optimization
Skill matching
Soft/Hard bookings
Full revenue recognition engine
Intercompany projects
Advanced project cash forecasting
Customer portal
AI autonomous planning
```

---

# 848. Fresh Small Company Defaults

Recommended:

```text
Tasks                 ON
Phases                Optional
Milestones            Optional
Team                  ON
Timesheets            Optional
Resource Booking      OFF
Simple Budget         Optional
Detailed Budget       OFF
Commitments           automatic if Purchasing linked
Forecast              Simple
Advanced WBS          OFF
Baseline              Simple/automatic
Change Orders         Simple
Billing               based on project type
Capital Project       OFF unless type
Multi-stage Approval  OFF
```

---

# 849. Professional Services Defaults

```text
Timesheets       ON
Cost rates       ON
Billing rates    ON
T&M/Fixed Price  ON
Expenses billing ON if contract
Forecast/EAC     ON
Profitability    ON
```

---

# 850. Capital Project Defaults

```text
Detailed cost categories
Purchasing integration
Expenses integration
Inventory material integration if relevant
Budget
Forecast
Capital classification
CIP integration
Billing OFF
```

---

# 851. Final Source-of-Truth Matrix

```text
Project / Task / Milestone        → Projects
Project Team / Allocation         → Projects
Project Time                      → Projects
Project Budget                    → Projects
Forecast / ETC / EAC              → Projects
Project Change                    → Projects
Billable Charge                   → Projects

Customer                           → Directory
Quotation / Sales Order            → Sales
Employee / Employment              → HR
Attendance                         → HR
Payroll/Payslip                    → Payroll
Expense                            → Expenses
Purchase Commitment                → Purchasing
Supplier Invoice                   → Invoicing
Inventory Material Movement/Cost   → Inventory
Customer Invoice                   → Invoicing
Receivable/Outstanding             → Subledger
Payment/Collection                 → Payments
CIP / Fixed Asset                  → Fixed Assets
Journal Entry                      → Ledger
```

---

# 852. Final Simple Project Flow

```text
Create
→ Team / Tasks
→ Work
→ Cost
→ Bill if needed
→ Complete
→ Financial Close
```

---

# 853. Final Fixed-Price Flow

```text
Accepted Sale
→ Project
→ Work
→ Actual + Commitment + Forecast
→ Milestone/Acceptance
→ Billable Charge
→ Invoice
→ Collection
→ Margin
```

---

# 854. Final T&M Flow

```text
Time / Expense / Material
→ Approval/finalization
→ Project Cost
→ Billing Rule
→ Billable Charge
→ Invoice
```

---

# 855. Final Budget Rule

> **Budget is a planned/approved control value. It is never overwritten by Actual, Commitment or Forecast.**

---

# 856. Final Commitment Rule

> **Open commitments represent commercially committed future project cost and must be visible before the supplier cost becomes Actual.**

---

# 857. Final Actual Rule

> **Project Actual Cost is derived from canonical source facts and corrections; users never type the project's actual cost total.**

---

# 858. Final Forecast Rule

> **ETC/EAC are planning forecasts, not accounting facts, and must show their assumptions and commitment treatment.**

---

# 859. Final Time Rule

> **Project Timesheet explains where work effort went; HR Attendance explains whether the employee worked. They must never be the same ledger.**

---

# 860. Final Rate Rule

> **Employee salary, project labor cost and customer billing rate are separate facts with separate permissions and historical snapshots.**

---

# 861. Final Billing Rule

> **Billing eligibility and billable charges belong to Project commercial execution; the final legal Invoice belongs to Invoicing.**

---

# 862. Final Milestone Rule

> **Reached, Accepted, Billing Eligible, Invoiced and Collected are separate states/facts.**

---

# 863. Final Change Rule

> **Scope, budget, schedule and customer-value changes are approved deltas with history, never silent edits to the original project baseline.**

---

# 864. Final Profitability Rule

> **Every profitability number must identify its revenue basis, cost basis, currency basis and allow drilldown to source facts.**

---

# 865. Final Close Rule

> **Execution completion means the work is done; financial close means project cost, commitments, billing and required capitalization are resolved.**

---

# 866. Final Capital Project Rule

> **Projects manages execution and project cost attribution; Fixed Assets owns CIP, capitalization and depreciation.**

---

# 867. Final Simplicity Rule

> **A small company must be able to use Projects without learning WBS, ETC, EAC, charge engines or enterprise resource booking terminology. Advanced controls appear only when needed.**

---

# 868. Final Enterprise Rule

> **A larger company can enable detailed budgets, commitments, forecasts, resource planning, change orders and capital-project controls without replacing the Project core.**

---

# 869. Final Product Verdict

TODIR Projects is release-ready only when:

```text
Simple projects do not require enterprise setup.
Customer/Internal/Capital projects have correct distinct defaults.
Customer/Employee/source data is never duplicated.
Timesheet and Attendance remain separate.
Resource Allocation and Task Assignment remain separate.
Salary is not exposed to project managers.
Cost Rate and Billing Rate remain separate.
Budget, Commitment, Actual and Forecast remain separate.
Open PO commitment is visible before supplier invoice.
Actual cost is source-derived and immutable/corrective.
Expense and material project costs come from their owning modules.
ETC/EAC are versioned/explainable.
Fixed Price, Milestone and T&M billing work correctly.
Billable Charge is separate from Invoice.
Milestone Reached, Accepted, Invoiced and Collected are distinct.
Change Orders preserve original/current values.
Project Complete is separate from Financial Close.
Capital Projects integrate with Fixed Assets/CIP without duplicating Asset logic.
Project profitability drills to source.
Multi-currency history preserves rate snapshots.
Project financial summaries rebuild from source facts.
Cross-module reconciliation has zero unexplained differences.
Performance remains acceptable at large project/time/cost scale.
Simple-user and project-accountant acceptance tests pass.
```

---

# 870. Final Agent Rule

For every Projects implementation decision, the Agent must answer:

> **What real project event happened, which module owns the source truth, is this plan/commitment/actual/billing/accounting/cash, what history must remain immutable, how will retries/corrections avoid duplication, and can a normal user complete the workflow without understanding the internal financial machinery?**

If those answers are unclear, the implementation is incomplete.

---

# 871. No Separate Projects Subfeature Packs

Do **not** create separate architecture packs for:

```text
Tasks
Milestones
Timesheets
Project Budgets
Project Billing
Project Profitability
Project Forecasts
Change Orders
Capital Projects
```

They are all part of `23 Projects`.

A future sector-specific Construction module may extend Projects if evidence proves materially different domain needs.

---

# 872. Next Planning Rule

Continue one comprehensive file per actual business module.

Select `24` by product/MVP priority after evaluating the remaining business domains and dependencies; do not create a new file merely because the sequence number is next.

---

**END OF FILE — 23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
