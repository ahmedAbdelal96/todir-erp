# 18 — TODIR ERP SaaS Payroll / Salary Calculation / Loans / Payslips Requirements & Technical Design Pack

**File:** `18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + PAYROLL ENGINE + COMPENSATION CALCULATION + EMPLOYEE RECEIVABLES + PAYMENT/ACCOUNTING HANDOFF + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Payroll Manager, HR Manager, Finance/Accounting Lead, Country Payroll Specialist, Backend/Frontend Leads, Security Engineer, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — EMPLOYEE MONEY + STATUTORY COMPLIANCE + ACCOUNTING + COMPANY TRUST**

---

## Canonical dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Payroll consumes HR truth and must not duplicate HR.

Payroll emits semantic accounting facts and payment instructions; it does not become Ledger or Payments.

---

# 0. Executive Decision

TODIR Payroll owns the deterministic conversion of approved employment/payroll inputs into:

```text
Gross Earnings
Employee Deductions
Employer Contributions
Net Pay
Employer Cost
Payslip
Payroll Accounting Facts
Payment Instructions / Bank/WPS outputs
Employee Loan/Advance Recovery Schedule
```

The central question is:

> **For this employee, in this payroll period, under these exact rules and inputs, how much is owed, how much is deducted, why, when should it be paid, and can TODIR reproduce the answer exactly years later?**

---

# 1. Payroll Is Not "Monthly Salary = One Number"

Real payroll contains:

```text
Base salary
Recurring allowances
One-time additions
Bonuses
Commissions
Overtime
Absence deductions
Late/early-leave effects
Unpaid leave
Penalties
Tax
Social/statutory contributions
Employee loans
Salary advances
Installments
Retroactive differences
Proration
Final settlement
Off-cycle corrections
```

The engine must model these explicitly.

---

# 2. Period ≠ Cutoff ≠ Pay Date

Absolute product rule:

```text
Payroll Period
!=
Input Cutoff
!=
Calculation Date
!=
Finalization Date
!=
Pay Date
!=
Actual Payment Date
```

Never collapse these into one `salary_month`.

---

# 3. Example — Company Paying on Day 1

```text
Payroll Period:
01 Jan → 31 Jan

Cutoff:
27 Jan

Calculation:
28 Jan

Pay Date:
01 Feb
```

---

# 4. Example — Company Paying on Day 10

```text
Payroll Period:
01 Jan → 31 Jan

Cutoff:
31 Jan

Calculation:
02 Feb

Pay Date:
10 Feb
```

---

# 5. Example — Multiple Payroll Groups

Same Legal Entity may have:

```text
Office Employees
Monthly
Pay Day = 1

Factory Workers
Monthly
Pay Day = 10

Temporary Workers
Weekly
Pay Day = Friday
```

Do not store one company-wide `pay_day`.

---

# 6. Simple Outside, Powerful Inside

Small company default:

```text
Open Payroll
→ Calculate
→ Review Exceptions
→ Finalize
→ Pay
```

Large company can enable:

```text
Multiple payroll groups
Approvals
SoD
Complex components
Retro pay
Off-cycle runs
Advanced loans
Payment files
WPS/statutory outputs
Advanced variance controls
```

Same Payroll Core.

---

# 7. No Workflow for Workflow's Sake

Do not force:

```text
Prepare
→ Submit
→ Department Review
→ HR Review
→ Finance Review
→ Director Approval
→ Authorize
→ Release
```

for a small company.

Optional approval is a capability, not the universal core flow.

---

# 8. Exception-Driven Payroll

Normal employees should not require manual review.

Example:

```text
197 Ready ✓

3 Need Review
- Missing statutory configuration
- Negative net pay
- Attendance correction pending
```

Payroll operator focuses on exceptions.

---

# 9. Deterministic Payroll

For the same:

```text
Employee
Payroll Period
Input Snapshot
Salary Structure Version
Payroll Component Version
Country Provider Version
Calculation Engine Version
```

TODIR must produce the same calculation result.

---

# 10. AI Does Not Calculate Final Payroll

AI may explain/summarize.

Final payroll calculation is deterministic code/rules only.

---

# 11. Payroll Owns

Payroll owns:

```text
Payroll Group
Payroll Calendar/Period generation
Payroll Run
Payroll Run Employee
Payroll Input Snapshot
Salary Structure
Payroll Component Definition
Payroll Component Version
Employee Payroll Assignment
Payroll Calculation
Payslip
Payslip Lines
Payroll Adjustment/Correction Run
Off-Cycle Run
Retroactive Calculation
Employee Loan/Advance Arrangement
Recovery/Installment Schedule
Payroll Payment Instruction
Payroll Accounting Fact creation
Payroll Country Provider contract
Payroll Reports/History
```

---

# 12. Payroll Does NOT Own

```text
Employee identity                 → HR
Employment / job / manager        → HR
Contract                          → HR
Attendance                        → HR
Leave                             → HR
HR disciplinary decision          → HR
HR contractual compensation truth → HR
Bank/cash money movement          → Payments
Journal Entry                     → Ledger
Tax/legal payroll rules           → Payroll Country Provider
Generic account balances          → Accounting/Ledger
```

---

# 13. HR → Payroll Boundary

HR supplies typed, approved facts:

```text
Employment eligibility
Contract context
Contractual compensation
Approved HR allowances/penalties
Attendance summary
Absence
Overtime approved
Leave paid/unpaid context
Insurance/statutory enrollment context
Termination/last working date
```

Payroll converts those facts into money.

---

# 14. HR Snapshot Is Mandatory

Payroll Run consumes a versioned/cutoff snapshot.

It must not finalize by querying mutable current HR rows one employee at a time.

---

# 15. Payroll Input Snapshot

Concept:

```text
PayrollInputSnapshot
{
    Id
    PayrollRunId
    EmployeeId
    EmploymentId

    HrSnapshotVersion
    SnapshotCutoffAt

    EmploymentSnapshot
    ContractSnapshot
    CompensationSnapshot
    AttendanceSummary
    LeaveInputs
    OvertimeInputs
    HrAdjustmentInputs
    StatutoryEnrollmentContext

    CapturedAt
    SourceVersions
}
```

Use typed normalized structures where hot/queryable; bounded immutable JSON snapshots can be used where appropriate.

---

# 16. Snapshot Freeze

Before Finalize, operator may refresh inputs according to policy.

After Finalize:

```text
Input Snapshot immutable
```

---

# 17. HR Change After Finalize

Does not rewrite old Payslip.

It creates:

```text
Retro adjustment
Off-cycle correction
Next-run delta
```

according to policy.

---

# 18. Payroll Group

A Payroll Group determines how a population is paid.

Concept:

```text
PayrollGroup
{
    Id
    TenantId
    LegalEntityId

    Code
    Name

    Frequency
    PeriodRule
    CutoffRule
    PayDateRule
    HolidayAdjustmentRule

    CountryPayrollProviderId
    DefaultSalaryStructureId?

    Status
    Version
}
```

---

# 19. Payroll Group Is Not Department

Employees from different departments can share Payroll Group.

---

# 20. Employee Payroll Assignment

Concept:

```text
EmployeePayrollAssignment
{
    Id
    EmploymentId
    PayrollGroupId
    SalaryStructureId

    EffectiveFrom
    EffectiveTo?

    PayrollCurrency
    Status
    Version
}
```

---

# 21. Assignment Effective Dating

Future assignment changes are supported.

Ambiguous overlapping primary payroll assignments are blocked.

---

# 22. Payroll Frequency

Foundation supports:

```text
Monthly
Weekly
Biweekly
SemiMonthly
```

Expose only frequencies needed by market/company.

---

# 23. Monthly Is Default UX, Not Architecture Law

MENA-first packaging may default to Monthly.

Core remains frequency-aware.

---

# 24. Payroll Period

Concept:

```text
PayrollPeriod
{
    Id
    PayrollGroupId

    Sequence
    StartDate
    EndDate

    CutoffAt
    PlannedCalculationDate?
    PlannedPayDate

    Status
}
```

---

# 25. Payroll Period Generation

Generated from Payroll Group calendar/rules.

Can be pre-created for year.

---

# 26. Explicit Period Override

Authorized admin may adjust a future period/pay date when holiday/company event requires.

History/audit required.

---

# 27. Pay Date Rule Examples

```text
1st of next month
10th of next month
Last calendar day
Last working day
N days after period end
Explicit period date
```

---

# 28. Holiday Adjustment

Policy:

```text
PreviousWorkingDay
NextWorkingDay
ExactDate
ManualReview
```

---

# 29. Working Day Source

Use HR Work Calendar/Country holiday capability.

Do not duplicate holiday rules.

---

# 30. Cutoff Rule Examples

```text
Period End
N days before Period End
Fixed day of month
Explicit period cutoff
```

---

# 31. Cutoff Is Input Boundary

Inputs with effective/source timestamp after cutoff are:

```text
Deferred
or
Included by explicit refresh/exception
```

according to Payroll Group policy.

---

# 32. Deferred Input Visibility

UI must explain:

```text
Added after cutoff
Scheduled for next run
```

No hidden missing allowance/deduction.

---

# 33. Payroll Run

A Payroll Run processes one Payroll Group/Period/Run Type.

Concept:

```text
PayrollRun
{
    Id
    TenantId
    LegalEntityId

    PayrollGroupId
    PayrollPeriodId
    RunType

    InputCutoffAt
    CalculationVersion

    CalculationState
    ApprovalState
    FinalizationState
    AccountingState
    PaymentState
    ComplianceOutputState

    CreatedBy
    CreatedAt
    FinalizedAt?
    RowVersion
}
```

---

# 34. Separate State Dimensions

Do not create one overloaded Payroll status.

Keep:

```text
Calculation State
Approval State
Finalization State
Accounting State
Payment State
Compliance Output State
```

separate.

---

# 35. Calculation State

Recommended:

```text
Draft
InputsCaptured
Calculated
NeedsReview
Ready
```

---

# 36. Approval State

Optional:

```text
NotRequired
Pending
Approved
Rejected
```

---

# 37. Finalization State

```text
NotFinalized
Finalized
CorrectedByLaterRun
```

---

# 38. Payment State

Projection from payment instructions/results:

```text
NotPrepared
Prepared
PartiallyPaid
Paid
PartiallyFailed
Failed
```

---

# 39. Accounting State

Projection:

```text
NotPosted
Posted
PostingFailed
Reversed/Adjusted
```

Ledger remains accounting authority.

---

# 40. Regular Run Uniqueness

Default invariant:

```text
one Regular finalized payroll run
per PayrollGroup + PayrollPeriod
```

Additional differences use supplemental/off-cycle/correction runs.

---

# 41. Run Types

```text
Regular
OffCycle
Adjustment
FinalSettlement
Retroactive
BonusOnly
```

Keep UI simple; internal type remains explicit.

---

# 42. Salary Structure

Defines reusable calculation component composition.

Concept:

```text
SalaryStructure
{
    Id
    LegalEntityId?
    Code
    Name
    EffectiveFrom
    EffectiveTo?
    Version
    Status
}
```

---

# 43. Salary Structure Version

Once used in finalized Payroll:

```text
immutable
```

Future changes create new version.

---

# 44. Payroll Component

Examples:

```text
BasicSalary
HousingAllowance
TransportAllowance
MealAllowance
Overtime
Bonus
Commission
AbsenceDeduction
LateDeduction
Penalty
IncomeTax
SocialInsuranceEmployee
SocialInsuranceEmployer
LoanRecovery
AdvanceRecovery
OtherAddition
OtherDeduction
```

---

# 45. Component Definition

Concept:

```text
PayrollComponentDefinition
{
    Id
    Code
    NameAr
    NameEn?

    Direction            // Earning / Deduction / EmployerContribution / Informational
    Classification
    TaxTreatmentRole?
    InsuranceTreatmentRole?
    AccountingRole?

    CalculationKind
    Priority
    RoundingPolicyRef

    CountryOwned?
    Status
}
```

---

# 46. Component Version

Calculation behavior is versioned.

---

# 47. Component Direction

Use explicit:

```text
Earning
EmployeeDeduction
EmployerContribution
Informational
Recovery
```

depending final enum design.

---

# 48. Fixed vs Variable

Component calculation can be:

```text
Fixed
InputAmount
PercentageOfTypedBase
Rate × Quantity
CountryProvider
ConstrainedFormula
```

---

# 49. Recurring vs One-Time Is Assignment/Input Behavior

Do not encode all recurrence into component definition itself.

Same Bonus component can be one-time; same allowance can be recurring.

---

# 50. Salary Structure Assignment

Employee Payroll Assignment references Salary Structure version/context.

Employee-specific overrides remain controlled.

---

# 51. Component Input Source

Every calculated line records source category:

```text
ContractualCompensation
HRAdjustment
Attendance
Leave
LoanSchedule
ManualPayrollInput
CountryProvider
DerivedFormula
RetroDifference
```

---

# 52. Explainability

Every Payslip line must answer:

```text
What component?
What amount?
Which source input?
Which formula/rule?
Which version?
```

---

# 53. Formula Engine

Use constrained deterministic expression engine.

No arbitrary:

```text
C#
JavaScript
Python
SQL
```

per tenant.

---

# 54. Formula Capabilities

Typed operations only:

```text
arithmetic
comparisons
if/else
typed component references
typed quantities
bounded functions
```

---

# 55. Formula Dependency Graph

Component dependencies must be acyclic.

Cycle:

```text
configuration error
```

---

# 56. Formula Evaluation Order

Derived from explicit dependency graph + priority only where independent.

Never insertion order.

---

# 57. Formula Validation

Before publishing structure/version:

```text
unknown reference
cycle
type mismatch
division by zero risk
unsupported function
missing country role
```

must fail.

---

# 58. Golden Test Vectors

Every country/statutory formula and critical salary formula requires input→expected output vectors.

---

# 59. Component Rounding

Use FinBase rounding.

No arbitrary `Math.Round`.

---

# 60. Payroll Currency

Typically Legal Entity/payroll assignment currency.

Multi-currency payroll must be explicit.

Do not silently mix currencies within one Payslip.

---

# 61. Base Salary

HR contractual compensation is source.

Payroll uses effective snapshot.

---

# 62. Contractual Amount Is Not Net Pay

Obvious but critical.

Payroll calculates additions/deductions.

---

# 63. Employee-Specific Recurring Component

Concept:

```text
RecurringPayrollInstruction
{
    EmploymentId
    ComponentCode
    EffectiveFrom
    EffectiveTo?
    Amount/Rate/Reference
    Source = HR / Payroll
    ApprovalRef?
    Version
}
```

Where HR owns the decision, Payroll stores immutable input/reference rather than becoming HR source.

---

# 64. One-Time Payroll Input

Concept:

```text
OneTimePayrollInput
{
    Id
    EmploymentId
    TargetPeriodId?
    EffectiveDate
    ComponentCode
    Quantity?
    Rate?
    Amount?
    SourceType
    SourceRef
    Reason
    Status
}
```

---

# 65. Input Status

```text
Draft
Approved
Consumed
Cancelled
Deferred
```

---

# 66. Approved Input Consumed Once

A one-time addition/deduction cannot accidentally apply in two runs.

---

# 67. One-Time Input Idempotency

Unique source + purpose + effective period.

---

# 68. Manual Payroll Input

Allowed only for authorized Payroll users.

Must record:

```text
component
amount/quantity
reason
effective period
actor
approval if configured
```

---

# 69. Manual Input Cannot Override Statutory Formula Silently

Country-owned components require explicit provider-approved override mechanism if legally permitted.

---

# 70. Salary Structure Default For Small Company

TODIR can seed:

```text
Basic Salary
Approved Allowances
Approved Deductions
Country Statutory Components
Net Pay
```

No formula configuration required to start.

---

# 71. Gross Earnings

Define explicitly as sum of earning components classified into Gross.

---

# 72. Employee Deductions

Separate:

```text
Statutory
HR/Policy
Loan/Advance Recovery
Other approved
```

---

# 73. Employer Contributions

Not subtracted from employee Net unless rule says employee share.

Show separately.

---

# 74. Net Pay

Concept:

```text
Net Pay
=
Eligible Employee Earnings
-
Eligible Employee Deductions
```

with exact component classifications.

---

# 75. Employer Total Cost

Concept:

```text
Employer Cost
=
Earnings
+
Employer Contributions
+
Other Employer-Paid Costs
```

according to component classification.

---

# 76. Net Pay Cannot Be Arbitrarily Edited

If wrong, fix input/rule and recalculate before Finalize.

After Finalize use adjustment.

---

# 77. Calculation Pipeline

Recommended:

```text
1. Resolve Run/Period/Group
2. Resolve eligible employees
3. Capture HR snapshot
4. Resolve Payroll Assignment/Structure
5. Load recurring inputs
6. Load approved one-time inputs
7. Load loans/advance installments
8. Resolve attendance/leave quantities
9. Apply proration
10. Evaluate earnings
11. Evaluate statutory bases/components
12. Evaluate deductions/recoveries
13. Apply limits/priorities
14. Compute employer contributions
15. Compute Net Pay
16. Validate
17. Build explanation
18. Persist calculation draft/result
```

---

# 78. Calculation Is Pure-ish

Given captured inputs/config versions, core calculation should avoid external IO.

---

# 79. No External API During Employee Calculation

Country provider runtime uses locally versioned/persisted rules/config.

Government/bank network calls never in calculation transaction.

---

# 80. Employee Payroll Calculation Result

Concept:

```text
PayrollRunEmployee
{
    Id
    PayrollRunId
    EmployeeId
    EmploymentId

    InputSnapshotId
    SalaryStructureVersionId
    CountryProviderVersion

    GrossAmount
    EmployeeDeductionAmount
    EmployerContributionAmount
    NetPayAmount
    EmployerCostAmount

    ValidationState
    ExceptionCount

    CalculationHash
    CalculatedAt
    RowVersion
}
```



# 81. Calculation Hash

A deterministic hash/fingerprint can include:

```text
input snapshot version
structure version
component versions
country provider version
engine version
```

to prove what produced result.

---

# 82. Recalculation Before Finalize

Allowed.

Creates/updates draft calculation result.

Previous draft calculation can be retained for audit/variance if valuable.

---

# 83. Recalculation After Finalize

Forbidden for the finalized Payslip.

Use:

```text
Adjustment Run
Retro Run
Off-Cycle Run
```

---

# 84. Payroll Preview

Before Finalize:

```text
Employee
Gross
Deductions
Net
Employer Cost
Exceptions
Difference vs previous period
```

---

# 85. Payroll Variance Review

Core control.

Flag:

```text
Net change over threshold
Gross change over threshold
New employee
Terminated employee
Zero Net
Negative Net
Large bonus
Large deduction
Missing attendance input
Missing statutory setup
Unexpected missing component
```

---

# 86. Variance Thresholds

Configurable.

No universal percentage hardcoded.

---

# 87. Explain Variance

Example:

```text
October Net 14,200
September Net 18,450
Difference -4,250

Loan recovery       -2,000
2 absence days      -1,500
Allowance ended       -750
```

---

# 88. Prior Period Comparison

Compare same employee/run group with latest relevant finalized regular run.

---

# 89. New Employee

Payroll eligibility starts according to:

```text
Employment effective date
Payroll assignment
Country/company policy
```

---

# 90. Proration

Payroll owns monetary proration.

HR supplies effective dates.

---

# 91. Proration Policies

Configurable/versioned:

```text
CalendarDays
WorkingDays
Fixed30Day
ActualPeriodDays
CountryProvider
NoProration
```

---

# 92. Proration Is Component-Aware

Some components may prorate.

Others may not.

---

# 93. Mid-Period Salary Change

Payroll calculates split-period eligibility:

```text
old compensation window
new compensation window
```

according to proration policy.

---

# 94. Mid-Period Hire

Only eligible days/period fraction calculated.

---

# 95. Mid-Period Termination

Final regular/final settlement calculation respects last working date.

---

# 96. Proration Explanation

Payslip calculation trace should show:

```text
eligible dates
basis
rate
fraction
amount
```

---

# 97. Attendance Monetary Treatment

HR gives:

```text
Worked
Absent
Late
Early Leave
Approved Overtime
```

Payroll determines money.

---

# 98. Attendance Input Quantity

Use typed duration/day quantities.

No direct HR money field for attendance effect.

---

# 99. Absence Deduction

Could be:

```text
Rate × absence days/hours
Country Provider
Company policy
```

versioned.

---

# 100. Lateness

Late duration does not automatically mean payroll deduction.

Payroll policy decides.

---

# 101. Overtime

HR gives approved duration.

Payroll evaluates:

```text
eligible rate base
multiplier
country/company rule
```

---

# 102. Overtime Formula Version

Stored/traceable.

---

# 103. Paid Leave

Usually does not reduce pay unless country/company rule says otherwise.

Provider/policy authoritative.

---

# 104. Unpaid Leave

Produces payroll effect according to versioned policy.

---

# 105. Leave Quantity Source

HR-approved leave/cutoff snapshot.

---

# 106. Attendance/Leave Conflict

If snapshot has contradictory facts:

```text
exception
```

not silent arbitrary precedence.

---

# 107. Pending Attendance Correction

Can block employee/run Finalize according to policy.

---

# 108. Missing Attendance

Company can configure:

```text
AttendanceNotRequired
Warn
Block
```

by Payroll Group.

---

# 109. Salaried Employee Without Attendance Deduction

Valid.

Do not force attendance into every Payroll Group.

---

# 110. Payroll Component Priority

Needed where deduction limits/remaining Net matter.

Examples:

```text
statutory deduction
court/legal deduction if provider supports
loan recovery
optional deduction
```

Priority policy versioned.

---

# 111. Deduction Limits

May come from:

```text
Country Provider
Company policy
Component policy
```

---

# 112. Negative Net Pay

Never casually finalize without explicit policy.

Potential outcomes:

```text
Block
CarryForwardExcessDeduction
CapEligibleDeductions
RequireManualReview
```

---

# 113. Negative Net Exception

Always visible.

---

# 114. Deduction Carryforward

If an optional recovery cannot fully deduct:

```text
remaining recovery balance
```

moves forward through explicit schedule state.

---

# 115. Statutory Deduction Shortfall

Country Provider defines handling.

Do not reuse generic loan carryforward blindly.

---

# 116. Rounding Sequence

Rules define:

```text
per component
per statutory base
final totals
```

using FinBase.

---

# 117. Payroll Country Provider

Contract responsible for statutory/local payroll semantics.

---

# 118. Country Provider Inputs

Typed payroll context:

```text
Legal Entity
Employee/Employment classifications
Payroll Period
Compensation bases
Statutory enrollment
Taxable component classifications
Leave/termination context
Country-specific parameters
```

---

# 119. Country Provider Outputs

Potential:

```text
employee statutory deductions
employer contributions
tax
statutory bases
legal warnings
final settlement statutory amounts
required output/file data
```

---

# 120. Provider Version

Every finalized calculation stores exact provider version.

---

# 121. Effective-Dated Statutory Rules

Rules can change on date.

Provider resolves correct version for period/effective context.

---

# 122. Historical Reproducibility

Upgrading Country Provider never changes old finalized Payslip.

---

# 123. Country Provider Golden Vectors

Mandatory before production.

Examples include:

```text
minimum/maximum bases
threshold edges
mid-year rule change
termination
unpaid leave
rounding boundaries
```

---

# 124. No Statutory Numbers In Core

Forbidden:

```text
if country == ...
taxRate = ...
insuranceRate = ...
```

inside generic Payroll application/domain.

---

# 125. Government Integration Boundary

Government file/API submission belongs provider/compliance adapter.

Calculation remains independent of network.

---

# 126. Payroll Compliance Outputs

Examples:

```text
WPS file
bank salary file
social insurance file
tax report
statutory report
```

only through market/provider capability.

---

# 127. Output Status Separate

A finalized Payroll can exist while a compliance file is:

```text
NotRequired
NotGenerated
Generated
Submitted
Accepted
Rejected
```

where provider supports.

---

# 128. Rejected Compliance Output

Does not rewrite Payslips.

Correction process depends on provider/legal rule.

---

# 129. Employee Loan / Advance Mission

Payroll must manage recoverable employee amounts safely and transparently.

---

# 130. Loan/Advance Ownership

Payroll owns:

```text
arrangement
approved principal
recovery schedule
payroll recovery application
remaining balance projection
```

Payments owns actual disbursement/refund money movement.

Ledger owns accounting.

---

# 131. Employee Receivable Arrangement

Concept:

```text
EmployeeReceivableArrangement
{
    Id
    EmploymentId
    Type            // Advance / Loan / OtherRecovery

    OriginalAmount
    Currency
    ApprovedDate
    DisbursementDate?
    DisbursementPaymentRef?

    RecoveryStartPeriodId?
    RecoveryMethod
    PlannedInstallmentAmount?
    PlannedInstallmentCount?

    Status
    OutstandingPrincipalProjection

    ApprovalRef?
    Reason
    RowVersion
}
```

---

# 132. Advance vs Loan

Use same financial recovery engine where possible.

UX/type semantics remain distinct.

---

# 133. Interest

Day 1 recommended:

```text
principal-only
```

unless verified market/legal need demands interest.

Interest-bearing employee loans require explicit policy/legal/accounting ADR.

---

# 134. Disbursement

Payroll requests Payment.

It never marks actual cash paid itself.

---

# 135. Disbursement Failure

Arrangement can be:

```text
Approved
PendingDisbursement
Disbursed
```

No recovery should begin before policy-eligible disbursement.

---

# 136. Recovery Schedule

Concept:

```text
EmployeeRecoveryScheduleLine
{
    ArrangementId
    Sequence
    TargetPayrollPeriodId?
    PlannedAmount
    Status
    AppliedPayslipId?
    AppliedAmount?
}
```

---

# 137. Schedule Generation

Examples:

```text
Fixed installment amount
Fixed installment count
Manual schedule
Remaining balance on final installment
```

---

# 138. Final Installment

Never over-recover.

```text
AppliedAmount
<=
OutstandingAmount
```

---

# 139. Recovery Invariant

Across finalized applications:

```text
SUM(recovered principal)
<=
disbursed/approved recoverable principal
```

---

# 140. Installment Pause

Authorized:

```text
Pause period
Resume
```

with reason/audit.

---

# 141. Reschedule

Future schedule can be changed.

Past finalized recoveries remain immutable.

---

# 142. Partial Manual Repayment

If employee repays outside Payroll:

Payments records movement.

Payroll arrangement receives typed repayment confirmation/ref and reduces outstanding balance exactly once.

---

# 143. Early Settlement

Can request remaining balance recovery in Payroll or external payment.

No double collection.

---

# 144. Termination With Outstanding Loan

Final settlement policy decides:

```text
recover up to allowed amount
carry employee receivable
external settlement/payment
manual finance follow-up
```

Do not silently write off.

---

# 145. Write-Off

High-risk explicit command:

```text
authorization
reason
accounting fact
audit
```

---

# 146. Loan/Advance Employee Workspace

Show:

```text
Original
Recovered
Outstanding
Next Installment
Status
History
```

---

# 147. Loan Recovery Component

Payslip line links arrangement/schedule line.

---

# 148. Salary Advance Shortcut

UX:

```text
[Give Advance]
```

internally:

```text
create arrangement
approve
request Payment
generate recovery schedule
```

depending permissions/policy.

---

# 149. No Payment = No Fake Advance Disbursed

Critical.

---

# 150. Bonuses

Can be:

```text
one-time
recurring plan
HR approved
Payroll entered
Commission module input later
```

Payroll records source.

---

# 151. Commission Boundary

If future Commissions module owns commission calculation:

Payroll consumes approved commission amount/fact.

No duplicate sales commission engine in Payroll.

---

# 152. Penalty Boundary

HR owns disciplinary decision.

Payroll only consumes approved monetary instruction.

---

# 153. Payroll-Only Deduction

Certain non-disciplinary payroll deductions may originate in Payroll if policy allows.

Must be typed and approved.

---

# 154. Reimbursement Boundary

Expense reimbursement belongs Expenses/Payments.

Do not pretend normal expense claim reimbursement is salary unless company intentionally includes via payroll component.

---

# 155. Expense Through Payroll

If supported:

typed external input from Expenses, not duplicate expense record.

---

# 156. Retroactive Pay

Handles a past-effective change discovered after prior Payroll finalized.

---

# 157. Retro Trigger Examples

```text
salary increase effective July entered September
allowance effective prior period
corrected attendance after payroll
statutory rule/provider correction where legally applicable
```

---

# 158. Retro Does Not Rewrite Old Payslip

Old payslip remains.

New run pays/recovers delta.

---

# 159. Retro Baseline

Recreate prior period expected result using:

```text
original historical rule versions
+
corrected authoritative input
```

or provider-defined method.

---

# 160. Retro Delta

```text
Corrected Expected Amount
-
Previously Finalized Amount
=
Retro Adjustment
```

---

# 161. Retro Trace

Must show:

```text
original payslip
original input
corrected input
difference by component
target adjustment run
```

---

# 162. Retro Period Limits

Country/company policy may restrict how far back.

Provider/policy.

---

# 163. Off-Cycle Payroll

Runs outside regular schedule.

Use cases:

```text
missed employee
bonus
correction
final settlement
emergency pay
retro
```

---

# 164. Off-Cycle Does Not Reopen Regular Run

Critical.

---

# 165. Off-Cycle Period Context

Still references effective accounting/payroll dates and source period(s).

---

# 166. Bonus-Only Run

Can include only eligible component(s).

No need to recalculate unrelated salary if policy says bonus supplemental.

---

# 167. Final Settlement

Special payroll run/employee calculation on employment end.

---

# 168. Final Settlement Inputs

Potential:

```text
salary through last working day
unpaid earnings
approved overtime
approved bonus/allowance
unused leave entitlement if provider says
statutory termination/gratuity if provider says
outstanding loan/advance
other approved recoveries
tax/statutory adjustments
```

---

# 169. Country Provider Owns Statutory Termination Formula

No generic hardcoded gratuity/severance formula.

---

# 170. Final Settlement Preview

Before Finalize:

```text
Earnings
Recoveries
Statutory entitlements
Outstanding employee receivable
Net settlement
```

---

# 171. Final Settlement Is Not HR Termination

HR owns employment end.

Payroll owns money.

---

# 172. Terminated Employee Regular Run

Eligibility policy prevents duplicate full regular + final settlement amounts.

---

# 173. Final Settlement Payment

Payments executes actual payment.

---

# 174. Payslip

Canonical finalized employee-period payroll result.

---

# 175. Payslip Header

Concept:

```text
Payslip
{
    Id
    PayrollRunId
    PayrollRunEmployeeId
    EmployeeId
    EmploymentId

    PeriodStart
    PeriodEnd
    PlannedPayDate

    Currency
    Gross
    EmployeeDeductions
    EmployerContributions
    NetPay
    EmployerCost

    InputSnapshotId
    CalculationVersion
    CountryProviderVersion
    FinalizedAt
    Number?
}
```

---

# 176. Payslip Line

Concept:

```text
PayslipLine
{
    Id
    PayslipId
    ComponentCode
    ComponentVersion
    Classification

    Quantity?
    Rate?
    BaseAmount?
    Amount

    SourceType
    SourceRef?
    FormulaTraceRef?
    DisplayOrder
}
```

---

# 177. Finalized Payslip Immutable

Absolute rule.

No update/delete of monetary lines.

---

# 178. Payslip Correction

Through new linked adjustment/off-cycle run.

---

# 179. Payslip Reversal

If legally/accountingly required, use explicit reversal/correction process.

No "unfinalize and edit".

---

# 180. Payslip Display

Employee-friendly:

```text
Earnings
Deductions
Employer/Statutory Information
Net Pay
```

Advanced calculation trace hidden by default.

---

# 181. Payslip Explanation

Authorized Payroll user can drill:

```text
line
→ source input
→ rule/formula version
→ HR fact / loan / provider result
```

---

# 182. Payslip PDF

Presentation artifact.

Structured Payslip remains truth.

---

# 183. Employee Self-Service

If linked User and permitted:

```text
view payslips
download payslip PDF
view payment status
view loan/advance balance
```

---

# 184. Self-Service Privacy

Employee can only access own Payroll records.

---

# 185. Payroll History In HR

HR Employee Workspace consumes read-only Payroll summary/links.

No copied truth.

---

# 186. Payroll Finalization

Finalize means:

```text
inputs frozen
calculation frozen
Payslips immutable
one-time inputs consumed
loan recoveries applied
accounting facts emitted/created as required
payment instructions become eligible
audit/outbox committed
```

---

# 187. Finalization Atomicity

Within local database/Transaction Coordinator where applicable:

```text
Run final state
Payslips
input consumption
loan recovery applications
accounting facts
audit
outbox
```

must be consistent.

---

# 188. External Network Excluded

No bank/government API call in Finalize transaction.

---

# 189. Finalize Pre-Checks

At minimum:

```text
all employees calculated
blocking exceptions resolved
Country Provider validation
approval satisfied if required
period/cutoff valid
no stale HR snapshots if refresh required
no duplicate regular finalized run
deduction constraints valid
payment details policy satisfied if blocking
```

---

# 190. Payment Details

Depending company payment method:

```text
bank account
cash
wallet
other provider
```

Ownership of actual payment destination should be through approved Payments/employee payout profile boundary.

---

# 191. Payroll Must Not Store Raw Bank Secrets Carelessly

Sensitive payout data protected/masked.

Payments may own canonical payment account details.

---

# 192. Payment Instruction

Concept:

```text
PayrollPaymentInstruction
{
    Id
    PayslipId
    EmployeeId

    Amount
    Currency
    PlannedPayDate

    PaymentMethod
    DestinationRef

    Status
    PaymentsOperationRef?
    FailureCode?
}
```

---

# 193. Payment Instruction Is Not Payment

Actual movement truth belongs Payments.

---

# 194. Bulk Payment Request

Payroll can request payment batch.

---

# 195. Partial Payment Failure

Run can be:

```text
PartiallyPaid
```

Employee-level status exact.

---

# 196. Retry Payment

Must not duplicate money.

Payments idempotency + stable payroll source reference.

---

# 197. Actual Payment Date

From Payments result.

Do not overwrite planned Pay Date.

---

# 198. Pay Date Delay

Payslip stays same.

Payment status/history captures delay.

---

# 199. Cash Payroll

If company pays cash:

Payments/cashbox capability records payout.

Payroll still produces instruction.

---

# 200. Bank/WPS File

Generated from finalized payment instructions through country/payment provider.



# 201. Payroll Accounting Mission

Payroll tells Ledger what happened economically.

Payroll does not choose/write Journal Entry rows directly.

---

# 202. Semantic Payroll Accounting Fact

Potential fact types:

```text
PayrollRunFinalized
PayrollAdjustmentFinalized
EmployeeAdvanceDisbursed
EmployeeRecoveryApplied
PayrollPaymentCompleted
EmployerContributionRecognized
```

Exact catalog belongs Ledger/Payroll contract design.

---

# 203. Payroll Fact Contents

Semantic, not GL IDs.

Examples:

```text
salary expense amount
allowance expense amount
employer contribution amount
employee tax payable
social insurance payable
salary payable
loan/advance recovery
department/branch/cost dimensions
```

---

# 204. Ledger Determines Accounts

Account determination remains Ledger-owned.

---

# 205. Payroll Accounting Timing

Typical:

```text
Finalize Payroll
→ recognize payroll expense/liabilities
```

Payment later:

```text
Payment
→ reduce salary payable / cash
```

according to accounting design.

---

# 206. Finalize ≠ Paid

Critical.

---

# 207. Payslip Net ≠ Cash Already Moved

Critical.

---

# 208. Accounting Fact Idempotency

One finalized Payroll source/version posts once.

---

# 209. Accounting Failure

If Finalization requires accounting atomically under current architecture:

run cannot report fully finalized success while required Ledger posting failed.

Use Transaction Coordinator.

---

# 210. Payment Failure Does Not Reverse Payroll Expense Automatically

Money movement problem and payroll recognition are separate.

---

# 211. Payroll Reconciliation

Finance should be able to reconcile:

```text
Sum finalized Payslip Net
=
Payroll salary payable generated

Payment Instructions
vs
Actual Payments

Employee/Employer statutory totals
vs
Ledger liabilities
```

---

# 212. Payroll Accounting Dimensions

Potential:

```text
Branch
Department
Cost Center
Project
```

from HR/Payroll snapshot.

---

# 213. Dimension Snapshot

If employee transfers after Payroll:

old Payslip accounting remains with original payroll-period snapshot.

---

# 214. Cost Allocation

Simple default:

employee current payroll assignment dimensions.

Advanced allocation across cost centers/projects can be later typed capability.

---

# 215. Payroll Posting Preview

Before Finalize:

finance-authorized user can preview:

```text
semantic fact totals
expected Ledger posting via Ledger preview
```

using real Posting Engine.

---

# 216. Payroll Summary By Component

Useful:

```text
Basic Salary Total
Allowances Total
Overtime Total
Employee Tax Total
Insurance Employee Total
Insurance Employer Total
Loan Recovery Total
Net Payroll
Employer Cost
```

---

# 217. Payroll Liability Report

Shows finalized but unpaid:

```text
employee
net pay
planned pay date
payment status
```

---

# 218. Payroll Cash Requirement

Forecast:

```text
Net Payroll payable
+
employer/statutory cash obligations where known
```

clearly labeled forecast/planned until actual Payment.

---

# 219. Accounting Does Not Recalculate Payroll

Accounting consumes Ledger facts/results.

---

# 220. Payroll Home

Recommended:

```text
Current Payroll
Employees Ready
Employees Need Review
Planned Pay Date
Net Payroll
Employer Cost

Attention
- Missing config
- Negative net
- Pending inputs
- Payment failures
- Statutory output issues
```

---

# 221. Small Company Payroll Home

Minimal:

```text
October Payroll

45 Employees
43 Ready
2 Need Review

Net Payroll 620,000

[Calculate]
[Review]
[Finalize]
[Pay]
```

---

# 222. Large Company Payroll Home

Optional:

```text
multiple Payroll Groups
approval queues
variance analysis
off-cycle runs
loans
retro queue
payment batches
country outputs
```

---

# 223. Payroll Run Employee List

Columns:

```text
Employee
Gross
Deductions
Net
Employer Cost
Variance
Validation
Payment Status
```

---

# 224. Sensitive Column Permissions

Not every HR/manager user can access Payroll list.

---

# 225. Employee Calculation Workspace

Show:

```text
Input Summary
Earnings
Deductions
Employer Contributions
Net
Variance
Exceptions
Trace
Payment
Accounting
```

---

# 226. Calculation Trace

Not raw formula code first.

Explain:

```text
Basic Salary:
20,000
Source: HR Compensation Agreement effective 2027-01-01

Overtime:
5h × approved rate rule
= 750

Loan Recovery:
Installment 4/10
= 2,000
```

---

# 227. Formula Technical Trace

Advanced restricted panel can show:

```text
component version
expression/rule
provider version
intermediate bases
rounding
```

---

# 228. Input Refresh UX

Before Finalize:

```text
Inputs captured at 25 Oct 18:00
3 approved HR changes after cutoff

[Review Changes]
[Refresh Eligible Inputs]
```

---

# 229. Refresh Is Explicit

No silent recalculation because HR changed in another tab.

---

# 230. Stale Input Detection

If high-risk source changes before Finalize:

mark Run Employee:

```text
InputChanged
```

according to policy.

---

# 231. Locking Strategy

Payroll Run calculation can process employees independently in parallel where safe.

Finalization acquires run-level state/version controls.

---

# 232. Concurrency — Same Run Calculate

Multiple calculate requests are idempotent/coordinated.

---

# 233. Concurrency — Manual Input vs Calculate

Calculation uses input version/cutoff.

Stale calculation marked invalid/recalculate-required.

---

# 234. Concurrency — Finalize vs Recalculate

Finalize requires latest accepted calculation version.

---

# 235. Concurrency — Loan Recovery

Same installment cannot apply to two Payslips/runs.

---

# 236. Concurrency — Payment Retry

Same payment instruction cannot create duplicate Payment.

---

# 237. Run Reopen

Finalized run cannot simply reopen.

Use correction/off-cycle.

---

# 238. Draft Run Delete

Unused Draft may be cancelled/deleted according to audit policy.

Finalized never deleted.

---

# 239. Cancel Run

Before Finalize:

```text
Cancelled
```

inputs remain unconsumed/released safely.

---

# 240. One-Time Input Reservation

When attached to Draft Run, policy may reserve it.

If run cancelled, input becomes eligible again.

---

# 241. Input Consumption

Only Finalize makes one-time input permanently consumed.

---

# 242. Input From HR Adjustment

Source reference preserved.

---

# 243. Input Cancellation After Consumption

Cannot cancel historical consumed source to mutate old Payslip.

Future corrective input required.

---

# 244. Payroll Approval

Optional dimension.

---

# 245. Approval Policies

Potential:

```text
Disabled
Always
AboveTotal
ByPayrollGroup
HighVarianceOnly
```

---

# 246. SoD

Large company can require:

```text
Preparer != Approver
Approver != Finalizer
Payment preparer != Payment approver
```

Payments controls final money authority.

---

# 247. Small Company SoD

May be disabled.

One authorized Payroll Manager can Calculate/Finalize.

---

# 248. Finalization Permission

Separate from Calculate.

---

# 249. Payment Permission

Separate from Finalization.

---

# 250. Country Output Permission

Separate.

---

# 251. Payroll Permissions

Initial vocabulary:

```text
payroll.home.read

payroll.group.read
payroll.group.manage

payroll.structure.read
payroll.structure.manage
payroll.component.read
payroll.component.manage

payroll.assignment.read
payroll.assignment.manage

payroll.input.read
payroll.input.create
payroll.input.approve

payroll.run.read
payroll.run.create
payroll.run.capture_inputs
payroll.run.calculate
payroll.run.review
payroll.run.approve
payroll.run.finalize
payroll.run.cancel

payroll.payslip.read
payroll.payslip.read_self
payroll.payslip.export

payroll.loan.read
payroll.loan.create
payroll.loan.approve
payroll.loan.reschedule
payroll.loan.writeoff

payroll.retro.read
payroll.retro.calculate
payroll.offcycle.create

payroll.payment.prepare
payroll.payment.read
payroll.payment.retry

payroll.compliance_output.generate
payroll.compliance_output.read

payroll.accounting_preview.read

payroll.settings.manage
payroll.audit.read
```

---

# 252. Data Scope

Payroll permissions combine with:

```text
Self
PayrollGroup
LegalEntity
AllAuthorizedEntities
SpecificEmployees
```

---

# 253. Manager Salary Access

Not automatic.

Manager is not Payroll role by default.

---

# 254. HR Compensation Access

HR may see contractual compensation.

That does not automatically grant Payslip tax/deduction visibility.

---

# 255. Finance Access

Finance may see Payroll totals/accounting without personal detailed deduction narratives where policy prefers.

---

# 256. Owner Access

Can see aggregate payroll cost without automatically seeing individual salaries.

Separate permissions.

---

# 257. Auditor Access

Read-only finalized Payroll + trace/audit.

---

# 258. Employee Access

Only own finalized Payslips/payment status/loan balance if self-service enabled.

---

# 259. API Field Redaction

Backend omits sensitive fields.

Never send all payroll data then hide in frontend.

---

# 260. Payroll Privacy

Payroll is highly sensitive.

Treat:

```text
salary
tax
insurance
loan
bank destination
deductions
payslips
```

as restricted.

---

# 261. Logs

Never log complete Payslip/input snapshots.

---

# 262. Metrics

Never use EmployeeId/name as metric labels.

---

# 263. Export

Individual payroll export permissioned.

Aggregate export can have separate permission.

---

# 264. Payslip Download

Secure short-lived file/reference if rendered.

---

# 265. Payroll Data Retention

Finalized payroll history retained according to statutory/company policy.

No casual purge.

---

# 266. Country-Specific Retention

Country Provider/legal configuration.

---

# 267. Encryption

Sensitive payment/statutory identifiers follow approved encryption/masking policy.

---

# 268. Audit Critical Actions

Capture:

```text
Payroll Group change
Salary Structure publish
Component/formula publish
Payroll assignment change
Manual payroll input
Input approval
Loan/advance approval
Loan reschedule/write-off
Run calculate/recalculate
Run approval
Run finalize
Off-cycle/retro creation
Payment batch request/retry
Country output generation
Sensitive export
```

---

# 269. Audit vs Payroll History

Audit says who acted.

Finalized Payslip/input snapshots say financial truth.

---

# 270. Schema

Recommended:

```text
business_payroll
```

---

# 271. PayrollDbContext

Own Payroll tables only.

No HR/Ledger/Payments EF navigation.

---

# 272. Core Tables

Potential:

```text
payroll_group
payroll_period

salary_structure
salary_structure_version
salary_structure_component
payroll_component_definition
payroll_component_version

employee_payroll_assignment

recurring_payroll_instruction
one_time_payroll_input

payroll_run
payroll_run_employee
payroll_input_snapshot
payroll_calculation_line
payroll_exception
payroll_variance

payslip
payslip_line
payslip_adjustment_link

employee_receivable_arrangement
employee_recovery_schedule_line
employee_recovery_application

payroll_payment_instruction
payroll_payment_batch_ref

payroll_compliance_output

payroll_external_source_ref
payroll_list_projection
payroll_employee_history_projection
```

---

# 273. Payroll Does Not Store

```text
Employee master
AttendanceRecord
LeaveRequest
JournalEntry
Payment
Bank Transaction
```

---

# 274. RLS

All tenant-owned Payroll tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 275. Runtime Role

No ownership/BYPASSRLS.

---

# 276. Tenant Context

Explicit transaction +:

```text
SET LOCAL app.tenant_id
```

per Platform Kernel.

---

# 277. Legal Entity Scope

Payroll Run belongs exactly one Legal Entity.

---

# 278. Payroll Group Entity

One Legal Entity.

No cross-entity run.

---

# 279. Cross-Entity Employee

If HR has multiple Employments:

Payroll processes the relevant Employment in its Legal Entity.

---

# 280. UUIDv7

Use for IDs.

---

# 281. Money Precision

Use FinBase `decimal`/PostgreSQL numeric rules.

---

# 282. Quantity Precision

Hours/days/rates use explicit numeric precision.

No float/double.

---

# 283. Formula Numeric Type

Decimal only.

---

# 284. Dates

Use NodaTime:

```text
LocalDate
Instant
DateTimeZone
```

according to semantics.

---

# 285. JSONB

Use bounded immutable calculation traces/snapshots only where appropriate.

Do not hide searchable hot fields.

---

# 286. Payroll Contracts

Locked naming:

```text
Todir.Payroll.Contracts
```

---

# 287. Backend Namespaces

Recommended:

```text
Todir.Modules.Payroll.Domain
Todir.Modules.Payroll.Application
Todir.Modules.Payroll.Infrastructure
Todir.Payroll.Contracts
```

---

# 288. Internal Areas

Potential:

```text
Groups/
Periods/
Structures/
Components/
Assignments/
Inputs/
Calculation/
Runs/
Payslips/
Loans/
Retro/
Payments/
Accounting/
CountryProviders/
Reports/
```

---

# 289. No Generic Payroll CRUD Service

Use business commands.

---

# 290. Payroll Group Commands

```text
CreatePayrollGroup
UpdatePayrollCalendar
GeneratePayrollPeriods
ChangePayDate
ActivatePayrollGroup
ArchivePayrollGroup
```

---

# 291. Structure Commands

```text
CreateSalaryStructure
CreateStructureVersion
AddComponent
PublishStructureVersion
ArchiveStructure
```

---

# 292. Component Commands

```text
CreatePayrollComponent
CreateComponentVersion
PublishComponentVersion
RetireComponent
```

---

# 293. Assignment Commands

```text
AssignEmployeeToPayroll
ChangeSalaryStructure
SchedulePayrollAssignmentChange
EndPayrollAssignment
```

---

# 294. Input Commands

```text
CreateOneTimePayrollInput
ApprovePayrollInput
CancelUnconsumedInput
ImportPayrollInputs
```

---

# 295. Run Commands

```text
CreatePayrollRun
CapturePayrollInputs
RefreshPayrollInputs
CalculatePayrollRun
RecalculateEmployee
ApprovePayrollRun
FinalizePayrollRun
CancelPayrollRun
```

---

# 296. Loan Commands

```text
CreateEmployeeAdvance
CreateEmployeeLoan
ApproveEmployeeReceivable
RequestDisbursement
GenerateRecoverySchedule
PauseRecovery
ResumeRecovery
RescheduleRecovery
RecordExternalRepayment
SettleEmployeeReceivable
WriteOffEmployeeReceivable
```

---

# 297. Correction Commands

```text
CreateOffCycleRun
CreateRetroRun
CreatePayrollAdjustment
CalculateFinalSettlement
FinalizeFinalSettlement
```

---

# 298. Payment Commands

```text
PreparePayrollPayments
RequestPayrollPaymentBatch
RetryFailedPayrollPayment
GeneratePayrollPaymentFile
```

---

# 299. No `SetNetSalary`

Forbidden.

---

# 300. No `SetPayslipAmount`

Forbidden.

---

# 301. No `SetLoanBalance`

Use recovery/disbursement/repayment transactions.

---

# 302. No `UnfinalizePayslip`

Forbidden.

---

# 303. API Base

```text
/api/v1/payroll
```

---

# 304. Payroll Group APIs

Representative:

```text
GET  /groups
POST /groups
PUT  /groups/{id}
POST /groups/{id}/generate-periods
GET  /groups/{id}/periods
```

---

# 305. Structure APIs

```text
GET  /salary-structures
POST /salary-structures
POST /salary-structures/{id}/versions
POST /salary-structure-versions/{id}/publish
POST /salary-structure-versions/{id}/preview
```

---

# 306. Component APIs

```text
GET  /components
POST /components
POST /components/{id}/versions
POST /component-versions/{id}/validate
POST /component-versions/{id}/publish
```

---

# 307. Assignment APIs

```text
GET  /employees/{employeeId}/assignment
POST /employments/{employmentId}/assignments
POST /assignments/{id}/schedule-change
```

---

# 308. Inputs APIs

```text
GET  /inputs
POST /inputs
POST /inputs/{id}/approve
POST /inputs/{id}/cancel
POST /inputs/import
```

---

# 309. Run APIs

```text
GET  /runs
POST /runs
GET  /runs/{id}
POST /runs/{id}/capture-inputs
POST /runs/{id}/refresh-inputs
POST /runs/{id}/calculate
POST /runs/{id}/approve
POST /runs/{id}/finalize
POST /runs/{id}/cancel
```

---

# 310. Run Employee APIs

```text
GET  /runs/{id}/employees
GET  /runs/{id}/employees/{employeeId}
POST /runs/{id}/employees/{employeeId}/recalculate
```

---

# 311. Payslip APIs

```text
GET /payslips
GET /payslips/{id}
GET /payslips/{id}/trace
GET /my/payslips
```

---

# 312. Loan APIs

```text
GET  /employee-receivables
POST /employee-receivables/advances
POST /employee-receivables/loans
POST /employee-receivables/{id}/approve
POST /employee-receivables/{id}/disburse-request
POST /employee-receivables/{id}/pause
POST /employee-receivables/{id}/reschedule
POST /employee-receivables/{id}/repay
```

---

# 313. Off-Cycle/Retro APIs

```text
POST /runs/off-cycle
POST /runs/retro
POST /final-settlements
```

---

# 314. Payment APIs

```text
POST /runs/{id}/payments/prepare
POST /runs/{id}/payments/request
GET  /runs/{id}/payments
POST /payment-instructions/{id}/retry
```

---

# 315. Accounting Preview API

```text
POST /runs/{id}/accounting-preview
```

delegates to Ledger preview.

---

# 316. Compliance Output APIs

Provider capability:

```text
POST /runs/{id}/compliance-outputs/{type}/generate
GET  /runs/{id}/compliance-outputs
```

---

# 317. API Does Not Accept Calculated Totals From Client

Server authoritative.

---

# 318. Client May Submit Manual Input Amount

Only for typed authorized input.

Server recalculates Payroll result.

---

# 319. Error Codes

Stable examples:

```text
payroll.group_not_found
payroll.period_not_open
payroll.period_conflict
payroll.assignment_missing
payroll.assignment_overlap

payroll.structure_not_published
payroll.component_invalid
payroll.formula_cycle
payroll.formula_invalid

payroll.hr_snapshot_missing
payroll.hr_snapshot_stale
payroll.input_after_cutoff
payroll.input_already_consumed

payroll.calculation_failed
payroll.negative_net_pay
payroll.deduction_limit_exceeded
payroll.country_configuration_missing
payroll.statutory_validation_failed

payroll.run_stale
payroll.run_not_ready
payroll.run_already_finalized
payroll.regular_run_duplicate

payroll.payslip_immutable

payroll.loan_not_disbursed
payroll.loan_over_recovery
payroll.installment_already_applied
payroll.repayment_duplicate

payroll.payment_instruction_duplicate
payroll.payment_failed

payroll.accounting_failed
payroll.permission_denied
payroll.idempotency_conflict
```

---

# 320. Friendly Error — Missing HR Snapshot

> "بيانات الموظف المطلوبة لحساب المرتب غير مكتملة أو لم يتم تثبيتها لهذه الفترة."

Show missing source.

---

# 321. Friendly Error — Input After Cutoff

> "تم تسجيل هذا التعديل بعد موعد إقفال مدخلات المرتبات، وسيتم ترحيله للفترة التالية ما لم يتم تحديث مدخلات هذه الدورة بصلاحية مناسبة."

---

# 322. Friendly Error — Negative Net

Show:

```text
Earnings
Mandatory deductions
Optional recoveries
Shortfall
policy
```

---

# 323. Friendly Error — Missing Country Config

Explain exact missing statutory setup.

---

# 324. Calculation Exception

Every employee exception has:

```text
code
severity
why
affected component
action
blocking?
```

---

# 325. Run-Level Exception

Examples:

```text
missing provider
duplicate regular run
stale cutoff
unapproved run
```

---

# 326. Security — Mass Assignment

Never bind calculation entities directly from requests.

---

# 327. Security — Employee Scope

Payroll employee access server-authorized.

---

# 328. Security — Formula Publishing

High-risk permission.

Formula/config changes can affect money.

---

# 329. Security — Provider Configuration

High-risk.

---

# 330. Security — Payment File

Highly sensitive.

Permission + audit + secure file.

---

# 331. Security — Loan

Loan/advance approval and write-off require strong permissions.

---

# 332. Security — Self-Service

IDOR tests mandatory.

---

# 333. Security — Export

Large salary export separately permissioned.

---

# 334. Integration Events

Potential:

```text
PayrollRunCreatedV1
PayrollRunCalculatedV1
PayrollRunFinalizedV1
PayslipFinalizedV1

EmployeeAdvanceApprovedV1
EmployeeAdvanceDisbursementRequestedV1
EmployeeRecoveryAppliedV1

PayrollPaymentPreparedV1
PayrollPaymentCompletedV1
PayrollPaymentFailedV1

PayrollAdjustmentFinalizedV1
FinalSettlementFinalizedV1
```

---

# 335. Draft Events

Do not globally publish every recalculation.

---

# 336. Outbox

Finalize/meaningful state changes use Transactional Outbox.

---

# 337. Broker Is Not Calculation Invariant

RabbitMQ outage does not alter deterministic local calculation/finalization invariants.

---

# 338. Idempotency

Mandatory for:

```text
run create
capture inputs
calculate request
finalize
loan approval
disbursement request
schedule generation
external repayment
payment request
payment retry
off-cycle creation
retro creation
country output generation
import
```

where retries can occur.

---

# 339. Same Idempotency Key Different Payload

Conflict.

---

# 340. Payroll Import

Possible:

```text
Employee payroll assignments
Recurring payroll inputs
One-time additions/deductions
Opening employee loans/advances
Historical finalized payroll summary
```

---

# 341. Migration — Employee Matching

Use HR Employment stable identifiers.

No name-only matching.

---

# 342. Opening Loan Migration

Need:

```text
original amount if known
outstanding balance
future installments
legacy reference
as-of date
```

Do not fake historical recoveries if unavailable.

---

# 343. Historical Payslip Migration

Options:

```text
SummaryOnly
DetailedHistoricalPayslips
OpeningYearToDate only where required
```

explicit strategy.

---

# 344. Year-to-Date Context

Country provider may need YTD taxable/statutory bases.

Migration must capture required opening YTD values.

---

# 345. Payroll Go-Live

Per Legal Entity/Payroll Group:

```text
PayrollGoLiveDate
FirstPayrollPeriod
HistoricalPayrollStrategy
OpeningYtdStrategy
LoanOpeningStrategy
```

---

# 346. Parallel Run

Recommended migration control:

```text
legacy payroll
vs
TODIR payroll
```

for one or more test periods where feasible.

---

# 347. Parallel Run Variance

Compare by:

```text
employee
component
gross
deductions
net
employer cost
statutory totals
```

---

# 348. No "Close Enough" Without Explanation

Every variance above approved tolerance requires cause/evidence.

---

# 349. Payroll Cutover Approval

Requires:

```text
Country payroll specialist
Payroll owner
Finance/accounting reviewer
```

for production market.

---

# 350. Reports — Core

```text
Payroll Register
Payslip Register
Gross-to-Net
Component Summary
Employer Cost
Payroll Variance
Payroll Exceptions
Unpaid Payroll
Payment Status
Loans/Advances
Recovery Schedule
Retro Adjustments
Off-Cycle Runs
Final Settlements
```

---

# 351. Payroll Register

Rows:

```text
Employee
Gross
Deductions
Net
Employer Contribution
Employer Cost
Payment Status
```

---

# 352. Component Detail Report

Example:

```text
Transport Allowance by employee
Tax by employee
Loan recovery by employee
```

restricted.

---

# 353. Cost By Department/Branch

Uses payroll snapshot dimensions.

---

# 354. Payroll Expense vs Payment

Reports clearly distinguish:

```text
Payroll Cost
Payroll Liability
Payroll Paid
```

---

# 355. Payment Reconciliation Report

```text
Payslip Net
Instruction Amount
Actual Payment Amount
Difference
Status
```

---

# 356. Accounting Reconciliation Report

```text
Payroll Fact Total
Ledger Posted Total
Difference
```

---

# 357. Country Statutory Reports

Provider-owned templates/output.

---

# 358. Payroll History

Employee history across periods.

---

# 359. Comparison Report

Period-over-period.

---

# 360. Owner Payroll Summary

Permissioned aggregate:

```text
Total Gross
Total Net
Employer Contributions
Total Employer Cost
Headcount Paid
```

No individual detail unless authorized.



# 361. Payroll Performance Mission

Payroll is batch-heavy but must still feel interactive for operators.

Calculation speed cannot compromise correctness.

---

# 362. Scale Assumptions

Design/load-test for:

```text
100k employees in a Legal Entity
multiple Payroll Groups
millions of historical Payslip Lines
large attendance snapshots
large loan schedules
multi-year payroll history
```

even if typical customers are smaller.

---

# 363. Calculate Run Target

Target is measured throughput, not a fixed promise.

Initial engineering goal:

```text
10k employees
within operationally acceptable batch window
with horizontal-safe worker parallelism inside one application
```

Benchmark exact target in performance environment.

---

# 364. Employee Recalculate Target

Single employee recalculation should be interactive:

```text
p95 <= 150-300ms
```

for normal component count, excluding external IO.

---

# 365. Payroll Run List Target

```text
p95 <= 100-250ms
```

for indexed pages.

---

# 366. Run Employee Page Target

```text
p95 <= 150-300ms
```

for current page/filter with precomputed totals.

---

# 367. Payslip Open Target

```text
p95 <= 100-250ms
```

structured data.

---

# 368. Calculation Parallelism

Employees are largely independent calculation units.

Use bounded parallelism/jobs where safe.

---

# 369. No One Transaction For 100k Employee Calculation Draft

Draft calculation can process chunks.

Finalization strategy must preserve all required run invariants without huge lock duration.

---

# 370. Calculation Chunk

Potential:

```text
500 / 1000 employees
```

tuned by benchmarks.

---

# 371. Finalization Strategy

Options:

```text
precompute immutable candidate results
validate complete run
short finalization transaction for state/input consumption/facts
```

Exact implementation must preserve cross-module atomic requirements.

---

# 372. Payslip Persistence

Use set-based/bulk database operations where safe.

No one SQL roundtrip per Payslip line.

---

# 373. N+1 Forbidden

1000 employees cannot cause:

```text
1000 HR calls
1000 Country Provider config queries
1000 component-definition queries
1000 account preview queries
```

---

# 374. HR Snapshot Batch Contract

HR must provide bulk period snapshot.

---

# 375. Provider Configuration Cache

Versioned immutable config can be cached locally.

No Redis required Day 1.

---

# 376. Salary Structure Cache

Published immutable versions safe for local cache by version ID.

---

# 377. Input Query

Set-based by run/group/period.

---

# 378. Loan Schedule Query

Batch load eligible installments.

---

# 379. Calculation Engine Allocation

Avoid unnecessary object churn for very large runs.

Optimize only with profiler evidence.

---

# 380. Historical Payroll Query

Use read projections for:

```text
employee payroll history
run summaries
component summaries
```

---

# 381. No Full Payslip Line Scan For Dashboard

Use run summary projection/canonical totals.

---

# 382. Run Totals

Store canonical aggregate derived from finalized employee results transactionally/rebuildably.

---

# 383. Rebuildability

Run summary/read projection can be rebuilt from finalized Payslips.

---

# 384. Indexes

Likely:

```text
tenant/entity/group/period
tenant/entity/run/status
run/employee
employee/period desc
component/run
payment status/run
input employee/effective/status
loan employee/status
schedule target_period/status
```

---

# 385. Unique Constraints

Examples:

```text
one regular finalized run per group/period
one payslip per run employee
one recovery application per schedule line/payslip
one consumed one-time input target
external source key uniqueness
```

---

# 386. Partitioning

Do not partition Day 1.

Measure Payslip Line/Calculation Line growth first.

---

# 387. PostgreSQL First

No analytical warehouse required for operational Payroll.

---

# 388. Large Analytics

Future BI can consume events/read warehouse.

Operational truth remains PostgreSQL.

---

# 389. Redis

No canonical Payroll value in Redis.

---

# 390. Background Jobs

Use Quartz/worker for:

```text
large run calculation
large exports
payment file generation
retro candidate scanning
period generation
scheduled payment status sync where provider supports
```

---

# 391. Job Idempotency

A restarted calculation job cannot create duplicate results.

---

# 392. Calculation Progress

Run UI can display:

```text
0 / 10,000
2,500 / 10,000
...
Calculated
```

---

# 393. Partial Calculation Failure

Employee-level exceptions do not silently skip.

Run shows exact failed employees.

---

# 394. Retry Failed Calculation

Retry only failed/stale employees with same accepted input version unless full refresh requested.

---

# 395. Finalize Requires Complete Population

No missing employee silently excluded unless explicitly marked:

```text
Excluded with reason
```

and policy permits.

---

# 396. Eligible Employee Population

Captured at run input snapshot time.

---

# 397. Employee Exclusion

Reasons:

```text
not payroll eligible
employment ended before period
payroll assignment missing
explicit authorized exclusion
```

---

# 398. Exclusion Audit

Manual exclusion requires reason/permission.

---

# 399. Observability Metrics

Potential:

```text
payroll_run_calculation_duration
payroll_employee_calculation_duration
payroll_calculation_failure_total
payroll_negative_net_total
payroll_input_stale_total
payroll_finalize_duration
payroll_payment_failure_total
payroll_retro_adjustment_total
payroll_loan_over_recovery_attempt_total
payroll_country_validation_failure_total
```

---

# 400. Tracing — Employee Calculation

Spans:

```text
Load snapshot
Resolve assignment
Resolve structure
Load inputs
Load recoveries
Apply proration
Evaluate components
Country Provider
Validate deductions
Calculate totals
Persist
```

---

# 401. Tracing — Finalize

```text
Validate run version
Validate population
Validate exceptions
Validate approval
Freeze results
Consume inputs
Apply recoveries
Create accounting facts
Create payment instructions
Audit
Outbox
Commit
```

---

# 402. Logging

No full formulas with employee values unless safe/debug environment and masked.

---

# 403. Alerting

Potential:

```text
run stuck
calculation high failure rate
payment batch partial failure
Country Provider unavailable/config error
outbox backlog
payroll-accounting reconciliation difference
```

---

# 404. Support Runbook — Calculation Failure

Inspect:

```text
employee source snapshot
component/rule version
exception
provider validation
```

No SQL amount edits.

---

# 405. Support Runbook — Wrong Payroll Result

Before Finalize:

fix authoritative input/config and recalculate.

After Finalize:

create correction/retro/off-cycle.

---

# 406. Support Runbook — Duplicate Payment Risk

Use Payments idempotency/source lookup.

Never create new manual payment blindly.

---

# 407. Support Runbook — Loan Balance Difference

Rebuild from:

```text
disbursement
recovery applications
external repayments
write-offs
```

not manual balance update.

---

# 408. Support Runbook — Projection Mismatch

Rebuild read projection from canonical Payroll rows.

---

# 409. Backup/Restore Validation

After DR restore:

```text
finalized Payslip count/totals
Run totals
loan outstanding rebuild
payment refs
accounting fact links
input consumption uniqueness
```

must pass.

---

# 410. Test Strategy — Deterministic Core

Use extensive pure unit/property tests for calculator.

---

# 411. Test — Same Inputs Same Result

Byte/semantic result equality for monetary lines under same versions.

---

# 412. Test — Different Provider Version

Explicitly different result only where rule changed.

Old finalized result unchanged.

---

# 413. Test — Formula Cycle

Publish blocked.

---

# 414. Test — Unknown Component Reference

Blocked.

---

# 415. Test — Decimal Precision

Golden boundary vectors.

---

# 416. Test — Rounding

FinBase golden vectors.

---

# 417. Test — Monthly Pay Day 1

Correct planned dates.

---

# 418. Test — Monthly Pay Day 10

Correct planned dates.

---

# 419. Test — Holiday Pay Date Adjustment

Previous/Next/Exact policy exact.

---

# 420. Test — Weekly Group

Periods generated without monthly assumptions.

---

# 421. Test — Semi-Monthly Group

Correct period boundaries.

---

# 422. Test — Cutoff

Input before cutoff included.

Input after cutoff deferred/warned according to policy.

---

# 423. Test — Explicit Refresh

Eligible post-cutoff input included once after authorized refresh.

---

# 424. Test — HR Snapshot Stability

HR change after capture does not silently alter existing calculation.

---

# 425. Test — Stale Snapshot Detection

Configured high-risk source change flags employee/run.

---

# 426. Test — Base Salary

HR compensation effective amount resolved correctly.

---

# 427. Test — Future Salary

Not applied early.

---

# 428. Test — Mid-Period Salary Change

Old/new windows and proration exact.

---

# 429. Test — Mid-Period Hire

Proration exact for each supported policy.

---

# 430. Test — Mid-Period Termination

Proration/final settlement eligibility exact.

---

# 431. Test — Recurring Allowance

Applied only in effective windows.

---

# 432. Test — One-Time Bonus

Consumed exactly once.

---

# 433. Test — Cancelled Input

Not applied.

---

# 434. Test — Input Consumed In Cancelled Draft

Released correctly because only Finalize consumes permanently.

---

# 435. Test — Attendance Overtime

Approved hours × rule exact.

---

# 436. Test — Unapproved Overtime

Not paid.

---

# 437. Test — Absence Deduction

Correct quantity/base/policy.

---

# 438. Test — Paid Leave

Correct no-deduction/defined treatment.

---

# 439. Test — Unpaid Leave

Correct deduction.

---

# 440. Test — Conflicting Attendance/Leave

Exception.

---

# 441. Test — Attendance Not Required Group

Run can finalize with no attendance.

---

# 442. Test — Insurance Employee Contribution

Provider golden vector.

---

# 443. Test — Employer Contribution

Included employer cost, not employee deduction unless separate employee share.

---

# 444. Test — Tax

Provider golden vector.

---

# 445. Test — Statutory Rule Effective Change

Periods before/after boundary use correct version.

---

# 446. Test — Historical Reproduction

Old Payslip trace resolves old provider/component versions.

---

# 447. Test — Negative Net Block

Configured block prevents Finalize.

---

# 448. Test — Negative Net Carryforward

Optional recoveries deferred without over-deducting.

---

# 449. Test — Deduction Priority

High-priority/mandatory components evaluated before optional recovery according to policy.

---

# 450. Test — Loan Approval

No recovery before approved/disbursed eligibility.

---

# 451. Test — Advance Disbursement Failure

Arrangement not falsely treated as disbursed.

---

# 452. Test — Fixed Loan Installments

Schedule exact.

---

# 453. Test — Final Installment

Does not over-recover remaining balance.

---

# 454. Test — Pause Installment

No recovery for paused period.

---

# 455. Test — Reschedule

Past applied lines immutable; future schedule changes.

---

# 456. Test — Manual Repayment

Reduces outstanding exactly once.

---

# 457. Test — Early Settlement

No future payroll recovery after full settlement.

---

# 458. Test — Concurrent Recovery

Two runs cannot apply same schedule line.

---

# 459. Test — Write-Off

Explicit audit/accounting path.

---

# 460. Test — Retro Salary Increase

Old Payslip unchanged; delta new run exact.

---

# 461. Test — Retro Attendance Correction

Delta only.

---

# 462. Test — Off-Cycle Bonus

Regular Run unchanged.

---

# 463. Test — Missed Employee

Off-cycle pays employee without reopening Regular.

---

# 464. Test — Final Settlement

Country/provider + outstanding recovery + final pay exact.

---

# 465. Test — Final Settlement Duplicate

Idempotency prevents second settlement.

---

# 466. Test — Payslip Immutability

Database/application rejects edit/delete monetary lines.

---

# 467. Test — Payslip Trace

Every line links source/rule.

---

# 468. Test — Double Finalize

No duplicate Payslip/accounting fact/recovery/payment instruction.

---

# 469. Test — Regular Run Duplicate

Second Regular finalized run for same group/period blocked.

---

# 470. Test — Partial Payment Failure

Employee statuses exact; Run not falsely Paid.

---

# 471. Test — Payment Retry

No duplicate money movement.

---

# 472. Test — Accounting Fact

Payroll totals reconcile to semantic fact.

---

# 473. Test — Ledger Posting

Ledger result balances and maps without Payroll GL IDs.

---

# 474. Test — Payment After Accounting

Payroll expense/liability remains finalized even if payment pending.

---

# 475. Test — Cross Tenant RLS

No salary/payslip/input leakage.

---

# 476. Test — Cross Entity

Blocked.

---

# 477. Test — Employee Self IDOR

Cannot read another Payslip.

---

# 478. Test — Manager No Salary Permission

No Payroll detail.

---

# 479. Test — Aggregate Owner Summary

Can see authorized aggregate without individual details.

---

# 480. Test — Formula Admin Permission

Unauthorized user cannot change/publish.

---

# 481. Test — Payment File Security

Unauthorized user cannot generate/download.

---

# 482. Test — 100k Employee Calculation

Measured.

---

# 483. Test — Millions Of Payslip Lines

History/report paging measured.

---

# 484. Test — Concurrent Calculate

No duplicate calculation records/inconsistent version.

---

# 485. Test — Finalize vs Refresh Race

Finalize only accepted current snapshot/calculation.

---

# 486. Test — Restore/DR

Financial/payroll invariants pass after restore.

---

# 487. Property Test — Net Equation

For every calculation:

```text
NetPay
=
Sum(Employee Earnings classified in net)
-
Sum(Employee Deductions classified in net)
```

within exact component semantics.

---

# 488. Property Test — Employer Cost

Matches classified earnings + employer contributions/costs.

---

# 489. Property Test — Recovery

```text
TotalRecovered <= RecoverablePrincipal
```

---

# 490. Property Test — Payslip Totals

Header totals equal lines.

---

# 491. Property Test — Run Totals

Run aggregate equals sum finalized employee results.

---

# 492. Property Test — Input Consumption

One-time input consumed at most once.

---

# 493. Property Test — Finalized Immutability

No ordinary command can mutate final monetary result.

---

# 494. Architecture Test — No HR Tables

Payroll cannot use `HrDbContext`.

---

# 495. Architecture Test — No Ledger Tables

Payroll cannot create JournalEntry directly.

---

# 496. Architecture Test — No Payments Tables

Payroll cannot mark cash moved.

---

# 497. Architecture Test — No Country If/Else Spread

Generic Core cannot branch statutory logic by country across handlers.

---

# 498. Architecture Test — No Arbitrary Script Formula

Reject runtime JS/C#/SQL formulas.

---

# 499. Red-Team — Company Has One Pay Day Field

Reject.

Need Payroll Groups/Periods.

---

# 500. Red-Team — Payroll Period Is Month Enum

Reject.

Use dates/frequency.

---

# 501. Red-Team — Pay Date Equals Period End

Reject.

Separate semantics.

---

# 502. Red-Team — Payroll Reads Live HR During Finalize

Reject.

Use accepted snapshot.

---

# 503. Red-Team — Payslip Editable After Finalize

Reject.

---

# 504. Red-Team — Reopen Finalized Payroll

Reject normal workflow.

Use correction.

---

# 505. Red-Team — AI Calculates Net Salary

Reject.

---

# 506. Red-Team — Hardcoded Country Tax Rates

Reject.

---

# 507. Red-Team — Loan Balance Mutable Field Only

Reject.

Need schedule/applications/history.

---

# 508. Red-Team — Loan Recovery Before Disbursement

Reject.

---

# 509. Red-Team — Installment Can Over-Recover

Reject.

---

# 510. Red-Team — HR Penalty Text Automatically Deducts

Reject.

Typed approved input.

---

# 511. Red-Team — Attendance Late Means Automatic Deduction

Reject.

Payroll policy/provider.

---

# 512. Red-Team — Payment Failure Marks Payslip Failed

Reject.

Payslip and payment states separate.

---

# 513. Red-Team — Finalized Payroll Means Paid

Reject.

---

# 514. Red-Team — Payroll Writes GL IDs

Reject.

Semantic facts.

---

# 515. Red-Team — Bank API In Finalize Transaction

Reject.

---

# 516. Red-Team — Every Employee Reviewed Manually

Reject.

Exception-driven.

---

# 517. Red-Team — Small Company Forced Into Salary Structure Designer

Reject.

Seed safe default.

---

# 518. Red-Team — Manager Sees Team Salaries Automatically

Reject.

---

# 519. Red-Team — Owner Aggregate Permission Gives Individual Detail

Reject.

---

# 520. Red-Team — Retro Changes Old Payslip

Reject.



# 521. Product Acceptance — Small Company

Payroll user can:

```text
Open current run
See employees
Calculate
Review only exceptions
Finalize
Prepare payment
```

without formula/admin complexity.

---

# 522. Product Acceptance — Different Pay Dates

Two Payroll Groups can pay on different dates in same company with no workaround.

---

# 523. Product Acceptance — Additions/Deductions During Month

Authorized additions/deductions entered during period appear in correct run or are clearly deferred by cutoff.

---

# 524. Product Acceptance — Loans/Advances

Payroll officer can create approved advance/loan, see installments and outstanding balance, and payroll recovers exactly.

---

# 525. Product Acceptance — Finalized History

Opening old Payslip explains every amount and source.

---

# 526. Product Acceptance — Employee

Employee sees understandable Payslip and own payment status.

---

# 527. Product Acceptance — Payroll Manager

Can compare current vs previous run and detect abnormal changes quickly.

---

# 528. Product Acceptance — Finance

Can reconcile Payroll totals to Ledger and Payments.

---

# 529. Product Acceptance — Large Company

Can enable:

```text
Multiple Payroll Groups
Approval/SoD
Retro
Off-Cycle
Advanced loans
Payment files
Country outputs
```

without replacing Payroll Core.

---

# 530. Agent Implementation Philosophy

Build Payroll as ordered, financially verifiable vertical slices.

Do not implement all screens first.

Do not build generic formula designer before basic payroll works.

---

# 531. Agent Precedence

If conflict:

```text
18 Payroll pack
→ 17 HR for HR ownership/snapshot boundary
→ 09 Ledger for accounting ownership
→ 12 Payments for money movement
→ 06 FinBase for money/rounding
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ approved Country Provider/ADR
```

---

# 532. Agent Must Read

Before Payroll:

```text
01
02
03
06
09
12
16
17
all AGENTS.md
approved ADRs
actual HR contracts
actual Ledger contracts
actual Payments contracts
```

---

# 533. Agent Repository Baseline Report

Before coding, report:

```text
HR snapshot contracts existing?
Money/rounding utilities?
Ledger Fact API?
Payments request API?
Country Provider seam?
Module/RLS patterns?
```

---

# 534. Agent Must Not Duplicate HR

If required HR fact missing, implement/extend approved HR contract—not Payroll-owned employee copy.

---

# 535. Agent Must Not Duplicate Ledger

No `PayrollJournalEntry`.

---

# 536. Agent Must Not Duplicate Payments

No `PayrollBankTransaction`.

---

# 537. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL
Why this matters to Payroll operator/employee/company.

CONTEXT
Existing HR/FinBase/Ledger/Payments contracts.

ARCHITECTURE RULES

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

CALCULATION REQUIREMENTS

DATABASE REQUIREMENTS

SECURITY REQUIREMENTS

PERFORMANCE REQUIREMENTS

UX REQUIREMENTS

TESTS REQUIRED

ACCEPTANCE CRITERIA

EXIT GATE

REPORT BACK WITH
- files changed
- migrations
- formulas/rule versions
- tests
- golden vectors
- performance evidence
- security evidence
- unresolved decisions
- risks
```

---

# 538. Implementation Sequence

```text
PY00 Repository + Payroll Baseline Verification
PY01 Module Boundary + Schema + RLS
PY02 Payroll Group + Calendar + Periods
PY03 Employee Payroll Assignment
PY04 Payroll Component Model
PY05 Salary Structure Versioning
PY06 Deterministic Formula Engine
PY07 Country Payroll Provider Contract
PY08 HR Batch Snapshot Contract
PY09 Payroll Input Model
PY10 Regular Payroll Run Shell
PY11 Core Gross-to-Net Calculation
PY12 Proration
PY13 Attendance / Leave Monetary Treatment
PY14 Statutory Components
PY15 Variance + Exception Engine
PY16 Payslip Draft/Preview
PY17 Payroll Finalization
PY18 Payslip History + Trace
PY19 Accounting Facts / Ledger Integration
PY20 Payment Instructions / Payments Integration
PY21 Payroll Group Pay-Date Variations
PY22 Employee Advances
PY23 Employee Loans + Recovery Schedule
PY24 Recovery / Repayment / Reschedule
PY25 Negative Net + Deduction Limits
PY26 Retroactive Payroll
PY27 Off-Cycle Payroll
PY28 Final Settlement
PY29 Payroll Approval / SoD
PY30 Employee Self-Service
PY31 Payroll Reports / Exports
PY32 Country Payment/WPS/Compliance Outputs
PY33 Migration + Opening YTD / Loans
PY34 Performance + Batch Scale
PY35 Security / Privacy Red-Team
PY36 Runbooks + Recovery
PY37 Payroll Manager Acceptance
PY38 Employee Payslip Acceptance
PY39 Finance Reconciliation Acceptance
PY40 Country Golden-Vector Acceptance
PY41 Cross-Module End-to-End Acceptance
PY42 Release Gate
```

---

# 539. PY00 — Goal

Understand repository financial/HR foundations before creating Payroll.

---

# 540. PY00 Verify

```text
HR contracts
HR effective compensation
HR attendance summary
HR leave inputs
HR adjustments
HR statutory enrollment
FinBase Money/Rounding
Ledger Accounting Fact
Transaction Coordinator
Payments source/idempotency
RLS
Outbox
Jobs
Audit
Files
```

---

# 541. PY00 Exit Gate

Exact dependency/gap map approved.

---

# 542. PY01 — Goal

Create isolated Payroll business module.

---

# 543. PY01 Implement

```text
Todir.Modules.Payroll.Domain
Todir.Modules.Payroll.Application
Todir.Modules.Payroll.Infrastructure
Todir.Payroll.Contracts
business_payroll
PayrollDbContext
manifest
RLS
architecture tests
```

---

# 544. PY01 Architecture Tests

Block:

```text
HR DbContext access
Ledger DbContext access
Payments DbContext access
Country hardcoding
arbitrary formula script
```

---

# 545. PY01 Exit Gate

Empty module boots/is isolated.

---

# 546. PY02 — Goal

Support real company payroll dates/frequencies.

---

# 547. PY02 Build

```text
PayrollGroup
Frequency
PeriodRule
CutoffRule
PayDateRule
HolidayAdjustment
PayrollPeriod generation
```

---

# 548. PY02 Must Prove

```text
Company A pay day 1
Company B/group pay day 10
multiple groups same Legal Entity
weekly/monthly foundation
holiday shift
```

---

# 549. PY02 Exit Gate

No one global `company.pay_day`.

---

# 550. PY03 — Goal

Connect Employment to correct Payroll Group/Structure.

---

# 551. PY03 Build

Effective Employee Payroll Assignment.

---

# 552. PY03 Tests

```text
future assignment
overlap block
ended employment
cross-entity
```

---

# 553. PY03 Exit Gate

Employee's payroll setup can be resolved AsOf any payroll period.

---

# 554. PY04 — Goal

Create typed Payroll Component catalog.

---

# 555. PY04 Build

```text
earning/deduction/employer contribution
classifications
source roles
accounting roles
rounding
priority
version
```

---

# 556. PY04 Exit Gate

No component is just an unnamed amount.

---

# 557. PY05 — Goal

Version reusable Salary Structures.

---

# 558. PY05 Build

Structure versions + component configuration.

---

# 559. PY05 Rule

Published used version immutable.

---

# 560. PY05 Exit Gate

Future structure changes do not alter past Payroll.

---

# 561. PY06 — Goal

Deterministic safe formula engine.

---

# 562. PY06 Build

Typed expression parser/evaluator or approved constrained engine.

---

# 563. PY06 Must Support

```text
decimal arithmetic
conditional
component/base refs
quantity × rate
bounded functions
dependency graph
```

---

# 564. PY06 Must Reject

```text
scripts
IO
DB access
reflection
random/time-dependent function
cycles
```

---

# 565. PY06 Tests

Property/golden vectors.

---

# 566. PY06 Exit Gate

Same input/versions always same result.

---

# 567. PY07 — Goal

Make country statutory payroll pluggable/versioned.

---

# 568. PY07 Build

```text
IPayrollCountryProvider
provider metadata/version
validation
statutory calculation contract
required outputs contract
golden vectors
```

---

# 569. PY07 Exit Gate

Generic Core has no country-specific statutory numbers.

---

# 570. PY08 — Goal

Get one coherent HR population/input snapshot.

---

# 571. PY08 Build/verify HR bulk contract

```text
Employment
Contract
Compensation
Attendance
Leave
Overtime
HR adjustments
Statutory enrollment
cutoff/version
```

---

# 572. PY08 Rule

No N+1 HR query per employee during large run.

---

# 573. PY08 Exit Gate

10k employee snapshot can be captured consistently in bounded calls.

---

# 574. PY09 — Goal

Model recurring/one-time/manual approved Payroll inputs.

---

# 575. PY09 Build

```text
Recurring instruction
One-time input
approval
effective dates
target period
source refs
consumption
defer after cutoff
import
```

---

# 576. PY09 Exit Gate

One-time input can never pay/deduct twice.

---

# 577. PY10 — Goal

Create Regular Payroll Run operator flow.

---

# 578. PY10 Build

```text
Create run
capture population
capture inputs
employee list
run states
progress
```

---

# 579. PY10 Exit Gate

Run contains exact eligible employee population and cutoff.

---

# 580. PY11 — Goal

Core Gross-to-Net.

---

# 581. PY11 Implement Pipeline

```text
earnings
deductions
employer contributions
net
employer cost
line trace
```

---

# 582. PY11 Golden Dataset

Create representative employees with known expected results.

---

# 583. PY11 Exit Gate

Core fixed/variable component calculations exact.

---

# 584. PY12 — Goal

Handle hires/terminations/salary changes mid-period.

---

# 585. PY12 Build

Versioned proration policies.

---

# 586. PY12 Exit Gate

Golden proration vectors exact for all enabled modes.

---

# 587. PY13 — Goal

Turn HR time facts into money safely.

---

# 588. PY13 Build

```text
absence
late if configured
overtime
paid/unpaid leave
```

using HR quantities + Payroll rules.

---

# 589. PY13 Rule

No attendance/leave money calculation inside HR.

---

# 590. PY13 Exit Gate

Attendance corrections after cutoff behave per policy.

---

# 591. PY14 — Goal

Apply statutory tax/insurance/provider results.

---

# 592. PY14 Build

Provider calculation inside deterministic pipeline.

---

# 593. PY14 Exit Gate

Country golden vectors pass at boundaries.

---

# 594. PY15 — Goal

Payroll Manager reviews exceptions, not everyone.

---

# 595. PY15 Build

```text
negative net
high variance
missing config
new/terminated
zero net
large addition/deduction
input conflict
```

---

# 596. PY15 Exit Gate

Run with 1000 normal employees can be reviewed through exception list.

---

# 597. PY16 — Goal

Provide understandable pre-finalize Payslip preview.

---

# 598. PY16 Build

Employee calculation workspace + preview PDF/read model.

---

# 599. PY16 Exit Gate

Payroll user can explain each line.

---

# 600. PY17 — Goal

Finalize immutably and exactly once.

---

# 601. PY17 Build

```text
blocking validation
approval check
snapshot freeze
Payslip persistence
input consumption
run totals
audit
outbox
```

plus accounting/payment eligibility hooks.

---

# 602. PY17 Concurrency Tests

```text
double finalize
refresh vs finalize
calculate vs finalize
```

---

# 603. PY17 Exit Gate

Finalized Payslips cannot mutate.

---

# 604. PY18 — Goal

Long-term audit/explanation.

---

# 605. PY18 Build

```text
Employee payroll history
Payslip trace
component/source/rule version
period comparison
```

---

# 606. PY18 Exit Gate

Old Payslip fully explainable after newer config changes.

---

# 607. PY19 — Goal

Correct accounting integration.

---

# 608. PY19 Build

Semantic Payroll Accounting Fact catalog + Ledger preview/post integration.

---

# 609. PY19 Rule

No GL Account IDs in Payroll facts.

---

# 610. PY19 Exit Gate

Payroll totals reconcile to Ledger postings.

---

# 611. PY20 — Goal

Pay employees without Payroll pretending to be a bank.

---

# 612. PY20 Build

```text
Payment Instructions
Batch request
Employee status
Retries
Actual Payment refs/date
```

---

# 613. PY20 Exit Gate

Partial payment failures exact; retries idempotent.

---

# 614. PY21 — Goal

Polish flexible payday/cutoff experience.

---

# 615. PY21 Build

Admin UI for:

```text
pay day
cutoff
frequency
holiday handling
future periods
```

---

# 616. PY21 Exit Gate

Payroll operator can explain planned dates without developer/config files.

---

# 617. PY22 — Goal

Production salary advances.

---

# 618. PY22 Build

```text
Advance arrangement
approval
Payment disbursement request
recovery schedule
history
```

---

# 619. PY22 Exit Gate

No recovery before actual/eligible disbursement.

---

# 620. PY23 — Goal

Employee loans/installments.

---

# 621. PY23 Build

```text
Loan
principal
installment schedule
start period
outstanding projection
```

---

# 622. PY23 Exit Gate

Final installment cannot over-recover.

---

# 623. PY24 — Goal

Real-world loan lifecycle.

---

# 624. PY24 Build

```text
pause
resume
reschedule future
external repayment
early settlement
termination handling
write-off
```

---

# 625. PY24 Exit Gate

Outstanding balance reconstructs from transactions.

---

# 626. PY25 — Goal

Safe deduction constraints/negative Net.

---

# 627. PY25 Build

```text
priority
caps
carryforward
blocking review
Country Provider limits
```

---

# 628. PY25 Exit Gate

No invalid negative payroll finalizes silently.

---

# 629. PY26 — Goal

Retroactive corrections without history rewrite.

---

# 630. PY26 Build

```text
retro trigger
recompute corrected historical expectation
delta lines
source links
new run
```

---

# 631. PY26 Exit Gate

Original Payslip unchanged; delta explainable.

---

# 632. PY27 — Goal

Off-cycle flexibility.

---

# 633. PY27 Build

```text
missed employee
bonus-only
correction
emergency
```

---

# 634. PY27 Exit Gate

Off-cycle never creates second unintended regular salary.

---

# 635. PY28 — Goal

Correct termination/final settlement.

---

# 636. PY28 Build

```text
last working date
proration
remaining earnings
leave/statutory provider
loan recovery
final Payslip
```

---

# 637. PY28 Exit Gate

Final settlement vectors reviewed by country specialist.

---

# 638. PY29 — Goal

Optional governance for larger companies.

---

# 639. PY29 Build

Approval policies + SoD.

---

# 640. PY29 Small Default

Approval disabled.

---

# 641. PY29 Exit Gate

Enabling SoD changes authority, not calculation truth.

---

# 642. PY30 — Goal

Employee can safely understand own salary.

---

# 643. PY30 Build

```text
My Payslips
PDF
Payment status
Loan/advance summary
```

---

# 644. PY30 Exit Gate

Cross-employee IDOR impossible.

---

# 645. PY31 — Goal

Operational/finance Payroll reporting.

---

# 646. PY31 Build

Core reports + secure export.

---

# 647. PY31 Exit Gate

Run/employee/report totals reconcile to canonical Payslips.

---

# 648. PY32 — Goal

Country-specific payment/statutory files.

---

# 649. PY32 Build

Provider adapters for enabled market only.

---

# 650. PY32 Rule

File generation from finalized data.

No final Payroll calculation inside export adapter.

---

# 651. PY32 Exit Gate

Provider file golden fixtures/validation pass.

---

# 652. PY33 — Goal

Safe Payroll cutover.

---

# 653. PY33 Build

```text
assignments migration
recurring inputs
opening YTD
loan balances/schedules
historical summary
parallel run comparison
```

---

# 654. PY33 Exit Gate

Parallel run differences explained/approved.

---

# 655. PY34 — Goal

Prove batch scale.

---

# 656. PY34 Load Tests

```text
100k employees
complex 50+ component structure
multiple groups
large loan population
large off-cycle
millions of Payslip Lines
concurrent operator reads
payment batch
```

---

# 657. PY34 Query Plan Evidence

Use real PostgreSQL.

---

# 658. PY34 Exit Gate

Calculation/report windows operationally acceptable and documented.

---

# 659. PY35 — Goal

Payroll privacy/security hardening.

---

# 660. PY35 Red-Team

Attack:

```text
cross-tenant Payslip
cross-entity run
employee IDOR
manager salary access
formula publish escalation
payment file access
loan write-off escalation
forged HR snapshot
forged Payment callback
double finalize
```

---

# 661. PY35 Exit Gate

RLS + authorization + source validation pass.

---

# 662. PY36 — Goal

Supported operational recovery.

---

# 663. PY36 Runbooks

Required:

```text
Missing HR snapshot
Missing country config
Formula configuration error
Calculation employee failure
Negative net
Stale inputs
Run stuck calculating
Finalize conflict
Accounting posting failure
Payment partial failure
Payment retry
Loan balance mismatch
Retro correction
Provider output rejection
Projection rebuild
DR restore validation
```

---

# 664. PY36 Forbidden Runbook

No:

```text
UPDATE payslip_line...
UPDATE loan_balance...
UPDATE net_pay...
```

as support fix.

---

# 665. PY36 Exit Gate

Every critical Payroll issue has non-destructive corrective path.

---

# 666. PY37 — Formal Payroll Manager Acceptance

Use experienced Payroll operator not involved in build.

---

# 667. PY37 Scenario A — Monthly Payroll

Reviewer:

```text
opens run
captures inputs
calculates
reviews exceptions
finalizes
prepares payment
```

without developer help.

---

# 668. PY37 Scenario B — Pay Day 10

Reviewer configures/understands group paying on 10th and generates correct period dates.

---

# 669. PY37 Scenario C — One-Time Changes

Adds:

```text
bonus
deduction
```

and confirms correct period/cutoff.

---

# 670. PY37 Scenario D — Loan/Advance

Creates advance, disburses, creates installments, runs payroll, verifies outstanding.

---

# 671. PY37 Scenario E — Salary Change

Mid-period effective change prorates and explains result.

---

# 672. PY37 Scenario F — Attendance

Absence/overtime/leave results trace to HR facts.

---

# 673. PY37 Scenario G — Retro

Past-effective change produces delta without changing old Payslip.

---

# 674. PY37 Scenario H — Final Settlement

Terminated employee gets correct final calculation.

---

# 675. PY37 Pass Criteria

Payroll reviewer confirms:

```text
workflow short
exceptions clear
totals trusted
each amount explainable
no Excel needed for normal calculation
history reproducible
```

---

# 676. PY38 — Employee Payslip Acceptance

Use employee persona.

---

# 677. PY38 Questions

Employee can understand:

```text
What did I earn?
What was added?
What was deducted?
What is my Net?
Was I paid?
What loan/advance remains?
```

---

# 678. PY38 Pass Criteria

No internal formula/code terminology needed.

---

# 679. PY39 — Finance Reconciliation Acceptance

Finance validates:

```text
Payroll Register
=
Payslip totals

Payroll Accounting Facts
=
Ledger postings

Payment Instructions
=
Payments actual movements
```

---

# 680. PY39 Exit Gate

Zero unexplained difference.

---

# 681. PY40 — Country Golden-Vector Acceptance

Country payroll specialist validates current enabled provider.

---

# 682. PY40 Must Include

```text
normal employee
high/low bases
mid-period hire
termination
unpaid leave
overtime if statutory
tax/insurance edge thresholds
rounding edges
retro scenario
final settlement
```

depending country's rules.

---

# 683. PY40 Exit Gate

No enabled provider ships without reviewed golden vectors/source/version register.

---

# 684. PY41 — Cross-Module E2E Acceptance

---

# 685. PY41 Scenario — HR → Payroll

```text
Employee
→ Contract
→ Compensation
→ Attendance/Leave
→ HR Adjustment
→ Payroll Snapshot
→ Payslip
```

exact.

---

# 686. PY41 Scenario — Payroll → Ledger

Finalize:

```text
Payroll Facts
→ Balanced Ledger posting
```

exact.

---

# 687. PY41 Scenario — Payroll → Payments

```text
Payslip Net
→ Instruction
→ Actual Payment
```

exact/idempotent.

---

# 688. PY41 Scenario — Post-Final HR Correction

```text
old Payslip stays
→ correction input
→ adjustment run
```

---

# 689. PY41 Scenario — Loan

```text
Advance
→ Payment
→ Recovery schedule
→ Payroll deductions
→ outstanding zero
```

---

# 690. PY41 Exit Gate

All core cross-module financial/payroll invariants pass in real PostgreSQL integration tests.

---

# 691. PY42 — Release Gate

Payroll is not production-ready because payslip UI exists.

It requires payroll trust evidence.

---

# 692. PY42 Required Sign-Off

```text
Product Architect
Payroll Domain Lead
HR Domain Lead
Country Payroll Specialist
Finance/Accounting Lead
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 693. PY42 Mandatory Evidence

```text
Calculation unit/property tests
Country golden vectors
RLS/security report
HR snapshot integration tests
Payslip immutability tests
Loan/advance tests
Accounting reconciliation
Payment reconciliation
Performance benchmarks
Payroll Manager Acceptance
Employee Payslip Acceptance
Parallel migration run
Known limitations
```

---

# 694. Build Now — MUST

```text
Payroll Groups
Flexible periods
Cutoffs
Pay dates
Holiday adjustment
Employee Payroll Assignment
Salary Structures
Versioned Components
Deterministic Formula Engine
Country Provider
HR snapshot
Recurring inputs
One-time inputs
Regular Payroll Run
Gross-to-Net
Proration
Attendance/Leave impact
Tax/insurance/provider components
Variance/exceptions
Payslip
Finalization
Accounting facts
Payment instructions
Advances
Loans/installments
Negative Net handling
Retro
Off-cycle
Final settlement
Permissions
Audit
Reports
Migration
Performance
Security
Runbooks
Acceptance gates
```

---

# 695. Build Later / Evidence-Based

```text
Complex interest-bearing employee loans
Garnishment/court-order engine by market
Advanced benefits flex plans
Commission engine
Equity compensation
Multi-book payroll accounting
Advanced workforce costing allocation
Direct bank APIs for every market
Government APIs for every market
AI payroll assistant
Global multi-country consolidated payroll
```

---

# 696. Fresh Small Company Defaults

Recommended:

```text
Frequency                  Monthly
One Payroll Group          ON
Pay Date                   configured during setup
Cutoff                     Period End or simple configured date
Salary Structure           seeded default
Approval                   OFF
SoD                        OFF
Variance Review            ON
Loans/Advances             ON simple
Retro                      available
Off-Cycle                  available
Country Provider           required for statutory payroll
Payment Integration        simple
Employee Self-Service      optional
```

---

# 697. Large Company Capabilities

Enable:

```text
Multiple Payroll Groups
Different pay dates
Advanced cutoffs
Approvals / SoD
Multiple structures
Retro
Off-cycle
Complex recovery schedules
Country payment files
Advanced variance thresholds
Cost dimensions
```

---

# 698. Setup Wizard

Small company setup:

```text
1. Choose Legal Entity
2. Choose Country Payroll Provider
3. Choose payroll frequency
4. Set period rule
5. Set cutoff
6. Set pay date
7. Confirm default salary structure
8. Assign employees
9. Validate statutory configuration
10. Create first run
```

---

# 699. Setup Health

Show:

```text
Country Provider         Ready
Payroll Group            Ready
Periods                  Ready
Salary Structure         Ready
Employees Assigned       43/45
Statutory Enrollment     44/45
Payment Destination      42/45
Accounting Mapping       Ready
```

---

# 700. Setup Does Not Block Irrelevant Fields

Cash-paying company need not configure bank destination if policy allows.

Attendance-not-used company need not configure shifts.

---

# 701. Payroll Home Final Design Principle

The Payroll operator should see:

```text
what period is being paid
when it will be paid
how many employees
how much total
which employees need attention
```

before configuration complexity.

---

# 702. Employee Payslip Final Design Principle

The employee should see:

```text
Earnings
Deductions
Net Pay
Payment Status
```

not formula internals.

---

# 703. Payroll Manager Final Design Principle

The Payroll manager should see:

```text
variance
exceptions
country validation
approval
totals
```

before finalization.

---

# 704. Finance Final Design Principle

Finance should see:

```text
Payroll cost
liabilities
accounting status
payment status
reconciliation
```

without recalculating Payroll.

---

# 705. Final Time Rule

> **A Payroll period is the earning/calculation window; the cutoff defines accepted inputs; the Pay Date is when payment is planned. They are different business facts.**

---

# 706. Final HR Boundary Rule

> **HR owns employee/employment/attendance/leave and approved HR decisions; Payroll owns their monetary treatment.**

---

# 707. Final Calculation Rule

> **Every finalized amount must be derived deterministically from frozen inputs and versioned rules.**

---

# 708. Final Payslip Rule

> **A finalized Payslip is immutable financial/employment evidence. Corrections create new adjustment results; they never rewrite history.**

---

# 709. Final Loan Rule

> **Employee advances and loans are transactional balances reconstructed from disbursement, recovery, repayment and write-off events—not a manually editable remaining-balance field.**

---

# 710. Final Payment Rule

> **Payroll says what should be paid; Payments says what actually moved.**

---

# 711. Final Accounting Rule

> **Payroll emits semantic accounting facts; Ledger decides the Journal Entry.**

---

# 712. Final Country Rule

> **Statutory payroll calculation belongs a versioned Payroll Country Provider with reviewed golden test vectors; generic Payroll Core never guesses local law.**

---

# 713. Final Simplicity Rule

> **A company paying 20 employees should be able to run payroll in a few actions without learning an enterprise workflow engine.**

---

# 714. Final Scale Rule

> **A company with multiple payroll populations, pay dates, complex deductions, loans, retro pay and compliance must be able to enable those controls without changing Payroll Core.**

---

# 715. Final Trust Rule

If Payroll cannot explain:

```text
why the employee received this Net amount
which inputs were used
which rules were applied
what was deducted
what loan remains
when payment was planned
whether payment actually happened
what accounting was created
```

then Payroll is not production-ready.

---

# 716. Final Source-of-Truth Matrix

```text
Employee / Employment                 → HR
Contract / Compensation agreement     → HR
Attendance / Leave / Overtime facts   → HR
HR allowance/penalty decision         → HR

Payroll calendar / group              → Payroll
Salary structures/components          → Payroll
Gross-to-Net                          → Payroll
Loan/advance recovery schedule        → Payroll
Payroll Run / Payslip                 → Payroll
Retro / Off-Cycle / Final Settlement  → Payroll

Statutory formulas                    → Payroll Country Provider
Money/Rounding                        → FinBase
Journal Entry                         → Ledger
Actual employee payment               → Payments
Accounting reporting                  → Accounting
```

---

# 717. Final Small Company Flow

```text
Configure Pay Date
→ Open Payroll
→ Calculate
→ Review Exceptions
→ Finalize
→ Pay
```

---

# 718. Final Monthly Employee Calculation

```text
HR Snapshot
  ↓
Base / Contract Compensation
  ↓
Recurring Additions
  ↓
One-Time Additions
  ↓
Attendance / Leave / Overtime
  ↓
Statutory Components
  ↓
Loans / Advances / Deductions
  ↓
Gross / Deductions / Net
  ↓
Payslip
```

---

# 719. Final Post-Payroll Flow

```text
Payslip Finalized
├── Accounting Fact → Ledger
├── Payment Instruction → Payments
├── Country Output → Provider
└── Employee History → Payroll read / HR link
```

---

# 720. Final Correction Flow

```text
Finalized Payslip
      ↓
New fact/correction discovered
      ↓
Retro / Adjustment / Off-Cycle
      ↓
New Payslip/Adjustment result
```

Original remains.

---

# 721. Final Agent Rule

For every Payroll feature, the Agent must answer:

> **Which frozen input produced this amount, which versioned rule transformed it, which invariant prevents duplication/overpayment, how can Payroll staff explain it, and what happens if the fact changes after Finalize?**

If those answers are unclear, implementation is incomplete.

---

# 722. No Separate Payroll Subfeature Packs

Do **not** create separate design packs for:

```text
Payroll Calendar
Salary Structures
Payslips
Loans
Advances
Installments
Retro Payroll
Off-Cycle Payroll
Final Settlement
Payment Files
```

They are all inside `18 Payroll`.

---

# 723. Next Planning Rule

Continue one comprehensive file per actual business module.

Do not split Payroll further.

---

**END OF FILE — 18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
