# 17 — TODIR ERP SaaS HR / Employee Digital File / Attendance / Leave Requirements & Technical Design Pack

**File:** `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + PEOPLE + EMPLOYEE DIGITAL RECORD + HR OPERATIONS + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, HR Manager, HR Specialist, Payroll Lead, Legal/Compliance Reviewer, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — PERSONAL DATA + EMPLOYMENT HISTORY + PAYROLL INPUTS + OPERATIONAL TRUST**

---

# 0. Executive Decision

TODIR HR is the company's **canonical digital employee file and employment-operation system**.

Its primary job is not to create more HR workflow.

Its primary job is to replace fragmented:

```text
paper employee folders
Excel sheets
scanned contracts in random folders
attendance notebooks
leave sheets
insurance papers
salary-change letters
penalty papers
certificates
HR emails
```

with one secure, searchable and auditable Employee Workspace.

The central product question is:

> **How can a company open one employee record and know everything that is relevant to that employee, what is true now, what changed historically, which documents prove it, and which HR events should affect Payroll—without forcing a small company into enterprise HR bureaucracy?**

---

# 1. One HR Business Module, One Pack

This file is the complete design baseline for the customer-facing **HR** module.

It contains:

```text
Employee Core
Employment / Assignment
Departments
Positions
Manager hierarchy
Contracts
Contract amendments/renewals
Employee Documents
Certificates / Qualifications / Licenses
Insurance / statutory enrollment status
Work Calendars
Shifts
Attendance
Check-in / Check-out
Attendance corrections
Absence
Overtime input/approval
Leave
Leave balances
Allowances / HR compensation instructions
Penalties / disciplinary actions
Onboarding
Offboarding
Termination / Resignation
Rehire
Employee History / Timeline
Employee Self-Service (optional)
Manager Self-Service (optional)
Payroll-linked history/views
Reporting
Permissions / field restrictions
Privacy / audit
APIs
Database model
Performance
Migration
Tests
Implementation Plan
```

Do not create separate design packs for Attendance, Leave, Contracts, Documents or Employee Archive.

---

# 2. Internal Boundaries Still Matter

One file and one customer-facing HR module does **not** mean one giant aggregate.

Inside HR, keep explicit bounded areas:

```text
Employee Core
Organization Assignment
Contracts
Employee Documents
Qualifications
Insurance/Compliance Profile
Attendance
Leave
HR Adjustments
Onboarding/Offboarding
History
```

They may have separate application services/read models internally.

They remain one purchasable/user-facing HR module.

---

# 3. Payroll Remains Separate

`18 Payroll` is a separate business module.

HR does **not** calculate final payroll.

Architecture:

```text
HR
├── Employee / Employment truth
├── Contractual terms
├── Attendance truth
├── Leave truth
├── Approved HR adjustments
└── Insurance/statutory employee status
          ↓
        Payroll
          ↓
Salary calculation
Statutory deductions/contributions
Payroll Run
Payslip
Accounting Facts
Payment
```

---

# 4. HR Mission

For every employee, TODIR must answer:

```text
Who is this employee?
Where do they work?
What is their current position?
Who is their manager?
What contracts have they had?
What is their employment status?
What is their agreed compensation context?
What allowances/penalties were approved?
What attendance/leave history exists?
Are required employee documents valid?
Are they insured/enrolled where applicable?
What certificates/qualifications do they have?
What changed and when?
Who changed/approved it?
What Payroll results are linked to them?
```

---

# 5. Employee Workspace Is the Center

Recommended Employee Workspace:

```text
Employee
├── Overview
├── Employment
├── Compensation
├── Attendance
├── Leave
├── Allowances & Penalties
├── Insurance / Compliance
├── Contracts
├── Documents
├── Certificates & Qualifications
├── Payroll
└── History
```

Tabs are capability/permission-aware.

---

# 6. Simple Outside, Powerful Inside

Small company can use:

```text
Create Employee
→ Job / Branch
→ Contract
→ Check In / Out
→ Leave
```

and nothing more.

Large company can enable:

```text
Departments
Positions
Shift Rotations
Attendance policies
Leave accrual
Approvals
Insurance compliance
Document expiry
Onboarding checklists
Offboarding controls
Manager hierarchy
Employee self-service
Advanced field restrictions
```

Same HR core.

---

# 7. No Workflow for Workflow's Sake

Default:

```text
Create Employee
→ Save
```

not:

```text
Draft
→ HR Review
→ Manager Review
→ Legal Review
→ Approve
→ Activate
```

unless company policy requires it.

---

# 8. Data Once Principle

The same employee fact must not be separately retyped in:

```text
HR
Payroll
Expenses
Projects
POS
Accounting
```

Other modules consume HR contracts.

---

# 9. User ≠ Employee

Locked:

```text
User Account
!=
Employee
```

Employee may never log into TODIR.

User may be:

```text
external accountant
consultant
owner
system admin
```

and not be an Employee.

---

# 10. Optional User Link

An Employee can optionally link to one User/Membership identity for:

```text
Self-Service
Manager actions
Check-in UX
Expense claims
Project tasks
POS cashier identity
```

according to module needs.

---

# 11. Never Auto-Create User For Every Employee

A warehouse worker/person without system access still has a complete Employee record.

---

# 12. Employee vs Directory Party

HR owns the workforce Employee identity and sensitive HR profile.

Do **not** require every Employee to become a Directory Party.

Optional Party link may exist only when another domain genuinely needs a Party identity.

---

# 13. Why Employee Is Not Just Party

HR contains employee-specific and highly sensitive facts:

```text
employment
manager
contract
salary context
attendance
leave
insurance
discipline
identity documents
```

These do not belong to general customer/supplier Directory.

---

# 14. Employee Master Scope

Recommended conceptual model:

```text
Employee
= tenant-level workforce identity

Employment
= LegalEntity-specific employment relationship
```

This avoids duplicating the person when the same employee changes Legal Entity or is rehired.

---

# 15. Small Company UX Hides This Split

User sees:

```text
New Employee
```

System can create:

```text
Employee
+
Employment
```

internally in one action.

---

# 16. Employee Core

Concept:

```text
Employee
{
    Id
    TenantId

    DisplayName
    ArabicName?
    EnglishName?
    PreferredName?

    PrimaryEmail?
    PrimaryPhone?

    DateOfBirth?          // restricted
    NationalityCode?      // only where useful
    Gender?               // optional/legal-context only

    UserId?               // optional link
    DirectoryPartyId?     // optional link

    OverallStatus
    CreatedAt
    CreatedBy
    RowVersion
}
```

---

# 17. Sensitive Personal Fields Are Minimal

Do not collect a field merely because HR systems traditionally have it.

Ask only if:

```text
required by employment
required by Country Pack
required for payroll/compliance
required by company policy
useful for employee operation
```

---

# 18. Employee Number

Employee number is a stable business identifier.

Recommended ownership:

```text
Employment.EmployeeNumber
```

because numbering can differ by Legal Entity.

---

# 19. Employee Numbering

Use Platform Numbering.

No `MAX(employee_no)+1`.

---

# 20. Employee Number Change

Default: stable after employment activation.

If changed:

```text
controlled command
history
alias/old-number search
audit
```

---

# 21. Employee Search

Search by authorized fields:

```text
Employee Number
Name AR
Name EN
Work email
Phone if permitted
Position
Department
Branch
```

Sensitive identifiers only with permission.

---

# 22. Arabic Search

Support normalized Arabic search.

Do not require exact diacritics/form.

---

# 23. Duplicate Employee Detection

Before creating, consider:

```text
exact national identifier where permitted
employee number
name + date of birth + phone
email
prior terminated employee
```

Return suggestions.

Never auto-merge.

---

# 24. Rehire Detection

If a former employee returns:

prefer:

```text
same Employee
+
new Employment
```

not duplicate Employee.

---

# 25. Employment

Concept:

```text
Employment
{
    Id
    TenantId
    EmployeeId
    LegalEntityId

    EmployeeNumber
    HireDate
    OriginalHireDate?
    EmploymentType
    EmploymentStatus

    CurrentAssignmentId?
    CurrentContractId?

    LastWorkingDate?
    TerminationReasonCode?

    CreatedAt
    RowVersion
}
```

---

# 26. Employment Types

Configurable controlled vocabulary:

```text
FullTime
PartTime
Temporary
FixedTerm
Probationary
Intern
Seasonal
Other
```

Country Pack/company policy may narrow/extend.

---

# 27. Employment Status

Keep understandable:

```text
PreHire
Active
Suspended
NoticePeriod
Terminated
Ended
```

Short leave is not a permanent Employment status.

---

# 28. Terminated Employee

Never delete.

Historical record remains.

---

# 29. Employment Assignment

Concept:

```text
EmploymentAssignment
{
    Id
    EmploymentId
    EffectiveFrom
    EffectiveTo?

    BranchId?
    DepartmentId?
    PositionId?
    ManagerEmploymentId?

    WorkLocationId?
    WorkCalendarId?

    ReasonCode?
    ChangeRef?
}
```

---

# 30. Why Effective-Dated Assignment

Do not overwrite:

```text
Branch
Department
Position
Manager
```

and forget the past.

---

# 31. Assignment History Example

```text
2026-01-01
Sales Rep
Cairo
Manager A

2026-07-01
Senior Sales Rep
Cairo
Manager A

2027-02-01
Sales Supervisor
Alexandria
Manager B
```

---

# 32. Current Assignment

Derived/resolved from active effective-dated assignment.

Can be cached as rebuildable projection/reference.

---

# 33. Future Assignment

Scheduled promotion/transfer can have future `EffectiveFrom`.

Must not become current early.

---

# 34. Overlapping Assignment

Default block for conflicting primary assignments.

---

# 35. Branch

Branch is Organization-owned.

HR references `BranchId`.

---

# 36. Department

Concept:

```text
Department
{
    Id
    LegalEntityId
    Code
    ArabicName
    EnglishName?
    ParentDepartmentId?
    ManagerEmploymentId?
    Status
}
```

---

# 37. Department Hierarchy

Supports parent/child.

Prevent cycles.

---

# 38. Position / Job Title

Concept:

```text
Position
{
    Id
    LegalEntityId?
    Code
    ArabicTitle
    EnglishTitle?
    DepartmentId?
    JobFamily?
    GradeRef? later/optional
    Status
}
```

---

# 39. Position Is Not Employee

One Position definition can apply to many employees.

---

# 40. Manager Relationship

Manager is an Employment reference, not a User reference.



# 41. Manager Self-Service

Manager actions require linked User + permissions.

The organizational manager relationship remains valid even if manager has no login.

---

# 42. Manager Cycle Prevention

Prevent recursive manager chains where employee ultimately manages themselves.

---

# 43. Org Chart

Derived from Department + Manager relationships.

Optional UI.

Not separate source of truth.

---

# 44. Employment Event History

Typed events include:

```text
Hired
Activated
Transferred
Promoted
Demoted
ManagerChanged
DepartmentChanged
BranchChanged
PositionChanged
ContractStarted
ContractRenewed
CompensationChanged
InsuranceChanged
Suspended
Resigned
Terminated
Rehired
```

---

# 45. Employment Event

Concept:

```text
EmploymentEvent
{
    Id
    TenantId
    EmployeeId
    EmploymentId
    EventType
    EffectiveDate
    OccurredAt
    ActorUserId?
    ReasonCode?
    SourceRef?
    SummarySnapshot
}
```

---

# 46. History Is Typed

Do not replace employment history with one generic free-text audit timeline.

---

# 47. Employee Overview

Default cards:

```text
Name / Employee Number
Status
Legal Entity
Branch
Department
Position
Manager
Hire Date
Current Contract
Attendance Today
Leave Summary
Document Alerts
```

Only fields user can view.

---

# 48. Employee Timeline

Combined read view may show:

```text
Employment
Contracts
Compensation
Attendance corrections
Leave
Allowances
Penalties
Documents
Insurance
Payroll
```

but each underlying domain remains typed.

---

# 49. Timeline Performance

Lazy-load and paginate.

Do not load an employee's entire multi-year history at page start.

---

# 50. Personal Contact

Separate work and personal contact when useful.

Personal contact has stricter permissions.

---

# 51. Personal Address

Sensitive.

Collect only when required/useful.

---

# 52. Emergency Contacts

Optional:

```text
Name
Relationship
Phone
Alternative phone?
Notes?
```

restricted.

---

# 53. Employee Identifiers

Country-aware typed identifiers.

Examples may include:

```text
National ID
Passport
Residence Permit
Work Permit
Country-specific identifier
```

Core does not hardcode country formats.

---

# 54. Employee Identifier Model

Concept:

```text
EmployeeIdentifier
{
    Id
    EmployeeId
    Type
    CountryCode?
    ValueEncrypted
    MaskedDisplay
    IssueDate?
    ExpiryDate?
    Issuer?
    Status
    FileRef?
}
```

---

# 55. Sensitive Identifier Security

Never place full identifier in:

```text
logs
analytics labels
URLs
search telemetry
client errors
```

---

# 56. Identifier Reveal

Masked by default.

Reveal requires explicit permission and may be audited.

---

# 57. Expiry Status

Derived:

```text
Valid
ExpiringSoon
Expired
```

not manually typed.

---

# 58. Expiry Alert Configuration

Allow configurable alert windows.

Country Pack may provide recommended/required defaults.

---

# 59. Work Calendar

Reusable:

```text
WorkCalendar
{
    Id
    LegalEntityId
    Name
    TimeZone
    WeeklyPattern
    HolidayCalendarRef?
    Status
}
```

---

# 60. Workweek Is Not Globally Hardcoded

Different Legal Entities/locations may have different weekends.

---

# 61. Holiday Calendar

Country Pack may contribute official holidays.

Company can add internal holidays.

---

# 62. Holiday Change History

Effective/versioned enough to reproduce Attendance/Leave/Payroll decisions.

---

# 63. Contract Is First-Class

Contracts are not attachments only.

TODIR stores both:

```text
structured contract facts
+
signed contract FileRef
```

---

# 64. Employment Contract

Concept:

```text
EmploymentContract
{
    Id
    EmploymentId

    ContractNumber?
    ContractType
    StartDate
    EndDate?

    ProbationStart?
    ProbationEnd?

    WorkCalendarId?
    WorkingHoursBasis?

    NoticePeriodTerms?
    CompensationAgreementRef?

    CountryProviderVersion?
    Status

    SignedFileRef?
    CreatedAt
    RowVersion
}
```

---

# 65. Contract Lifecycle

Recommended:

```text
Draft
Active
Expired
Ended
Superseded
CancelledBeforeStart
```

---

# 66. Contract Draft

Editable before activation/signing boundary.

---

# 67. Active Contract

Do not silently overwrite material terms.

---

# 68. Contract Renewal

Create:

```text
new contract
or
new controlled renewal/version
```

with relation to previous contract.

---

# 69. Contract History Example

```text
Contract 1
2026-01-01 → 2026-12-31

Contract 2
2027-01-01 → 2027-12-31
```

Both remain visible.

---

# 70. Contract Amendment

For a material change during active term:

```text
EmploymentContractAmendment
{
    ContractId
    EffectiveDate
    ChangeType
    PreviousSnapshot
    NewTerms
    Reason
    FileRef?
    ApprovedBy?
}
```

Exact persistence may be normalized but history must be reproducible.

---

# 71. Contract Expiry Alerts

Show:

```text
Expires in 90 days
Expires in 30 days
Expired
```

according to company/Country Pack configuration.

---

# 72. Contract Auto-Renew

Do not assume.

If contract has auto-renewal terms, store as terms/evidence.

Business process still needs explicit controlled renewal where legally/operationally required.

---

# 73. Probation

Store dates/terms.

Optional reminder before probation end.

Do not hardcode statutory duration in Core.

---

# 74. Notice Period

Store contractual/country-resolved terms.

Termination process references it.

---

# 75. Signed Contract File

Platform Files owns bytes.

HR stores `FileRef`.

---

# 76. Contract File Replacement

Never silently overwrite evidence.

New file/version remains traceable.

---

# 77. Contract Search

Search/filter:

```text
Active
Expiring
Expired
Missing signed file
Contract type
Legal Entity
Department
```

---

# 78. Employee Documents Mission

Replace physical employee archive with structured digital document library.

---

# 79. Document Categories

Potential categories:

```text
Identity
Passport
Residence/Permit
Employment Contract
CV
Certificate
Qualification
Insurance
Policy Acknowledgment
Warning/Disciplinary
Promotion Letter
Salary Change Letter
Termination Document
Company Form
Other
```

Categories configurable.

---

# 80. Employee Document

Concept:

```text
EmployeeDocument
{
    Id
    EmployeeId
    EmploymentId?

    DocumentType
    Title
    FileRef

    DocumentNumberEncrypted?
    IssueDate?
    ExpiryDate?
    Issuer?
    CountryCode?

    SensitivityClass
    VerificationStatus
    Status

    UploadedBy
    UploadedAt
}
```

---

# 81. File Storage

No file bytes in HR business tables.

Use Platform Files/object storage.

---

# 82. Document Sensitivity

Examples:

```text
Standard HR
Personal Sensitive
Compensation Sensitive
Identity Highly Sensitive
Disciplinary Restricted
Medical/Health Restricted if ever supported
```

Exact classes configurable/governed.

---

# 83. Medical Data

Do not turn Core HR into a medical-record system.

If a company needs medical fitness evidence:

prefer minimal status/document evidence with strict access.

Country/legal review required.

---

# 84. Document Verification

Optional:

```text
Unverified
Verified
Rejected
Superseded
Expired
```

---

# 85. Document Versioning

Replacement document creates new record/version relation.

Old record remains historical.

---

# 86. Document Expiry

Derived from expiry date.

Alerts through Notifications/Scheduler.

---

# 87. Missing Document Checklist

Company/Country Pack can define required document types by:

```text
Legal Entity
Employment type
Nationality/status
Position category
```

Do not globally force every document.

---

# 88. Employee File Completeness

Display:

```text
Complete
Missing 2 required documents
3 documents expiring
```

only based on enabled requirements.

---

# 89. Document Access Audit

Highly sensitive document:

```text
view/download
```

can be audit-logged according to security policy.

---

# 90. Document Download URLs

Use secure short-lived access.

Do not expose permanent public object URLs.

---

# 91. Certificates / Qualifications

Separate structured records from generic attachments.

---

# 92. Qualification

Concept:

```text
EmployeeQualification
{
    Id
    EmployeeId
    Type
    Institution
    FieldOfStudy?
    Level?
    CompletionDate?
    Country?
    FileRef?
}
```

---

# 93. Professional Certificate / License

Concept:

```text
EmployeeCredential
{
    Id
    EmployeeId
    CredentialType
    Name
    Issuer
    CredentialNumber?
    IssueDate?
    ExpiryDate?
    FileRef?
    VerificationStatus
}
```

---

# 94. Credential Expiry Alerts

Important for regulated roles.

Optional for companies that do not need it.

---

# 95. Training Boundary

Full Learning/Training Management is later.

HR may store completed credential evidence.

Do not build LMS in this pack.

---

# 96. Compensation Boundary

HR must let HR users understand the employee's contractual compensation history.

Payroll must remain the calculation authority.

---

# 97. Contractual Compensation vs Payroll Calculation

Distinguish:

```text
HR Contractual Compensation
= what company agreed / employment term

Payroll Compensation Assignment
= how Payroll calculates components for a period

Payslip
= finalized period result
```

---

# 98. No Payroll Engine Inside HR

HR must not calculate:

```text
net salary
income tax
social insurance contribution amount
overtime money
absence monetary deduction
loan installment
final payroll
```

---

# 99. Compensation Agreement

Conceptual HR record:

```text
EmployeeCompensationAgreement
{
    Id
    EmploymentId
    EffectiveFrom
    EffectiveTo?

    BaseSalaryReferenceAmount?
    CurrencyCode
    PayFrequency?

    ContractRef?
    ReasonCode
    ApprovedBy?
    SourceFileRef?
}
```

Exact fields depend on Payroll/Country Pack boundary.

---

# 100. Why Base Salary May Appear in HR

A contractual base salary can be an employment term/evidence.

Payroll consumes or reconciles it into Payroll compensation assignment.

It is not a second payroll calculation.

---

# 101. Salary History

Employee Workspace shows effective-dated history:

```text
2026-01-01  12,000
2026-07-01  15,000
2027-02-01  19,000
```

with:

```text
reason
effective date
contract/amendment
approver
Payroll sync status
```

---

# 102. Salary Change

No overwrite of current amount without history.

Command:

```text
ChangeCompensation
```

creates new effective record/event.

---

# 103. Future Salary Change

Can be scheduled.

Payroll must apply only from eligible period/effective date.

---

# 104. Payroll Reconciliation

Where Payroll active, HR can show:

```text
Contractual compensation
Payroll compensation assignment
Status: aligned / difference / pending
```

without calculating Payroll.

---

# 105. Compensation Difference

Never hide.

Could mean:

```text
future effective change
approved exception
Payroll configuration missing
legal/statutory mismatch
```

---

# 106. Allowance Boundary

User needs allowance history in Employee file.

HR owns the **HR decision/instruction/evidence**.

Payroll owns the monetary calculation/application in Payroll Runs.

---

# 107. HR Allowance Instruction

Concept:

```text
EmployeeHrAdjustment
{
    Id
    EmploymentId
    Kind = Allowance

    AdjustmentType
    EffectiveFrom
    EffectiveTo?

    AmountOrReference?
    Currency?
    Recurrence = OneTime / Recurring / Informational

    PayrollImpact = None / SendToPayroll

    Reason
    ApprovalRef?
    AttachmentRef?
    Status
}
```

---

# 108. Allowance Types

Examples configurable:

```text
Transport
Housing
Meal
Communication
Responsibility
Temporary Assignment
Other
```

No country amounts hardcoded.

---

# 109. Recurring Allowance

HR instruction can be effective-dated.

Payroll materializes/calculates actual payroll component according to Payroll rules.

---

# 110. One-Time Allowance

Approved HR instruction can become Payroll input for specified period.

---

# 111. Penalty / Disciplinary Boundary

A disciplinary decision is not merely a negative payroll line.

HR owns the employee-relations event.

Payroll consumes monetary impact only when approved/configured.

---

# 112. Disciplinary Action

Concept:

```text
DisciplinaryAction
{
    Id
    EmploymentId

    ActionType
    IncidentDate?
    DecisionDate
    EffectiveDate?

    ReasonCode
    NarrativeRestricted?

    PayrollImpact = None / SendToPayroll
    PayrollInstructionRef?

    EvidenceFileRefs[]
    Status
    ApprovedBy?
}
```

---

# 113. Penalty Types

Company/Country Pack controlled.

Examples:

```text
Warning
Written Warning
Attendance Penalty
Suspension
Financial Penalty if legally/configurably allowed
Other
```

---

# 114. No Automatic Financial Penalty From Free Text

Any payroll-impacting penalty must become typed approved input.

---

# 115. Penalty History

Employee Workspace shows:

```text
date
type
reason
status
payroll impact
approval
evidence
```

to permitted HR users.

---

# 116. Disciplinary Privacy

Manager may not automatically see all restricted disciplinary details.

Field/action permissions apply.

---

# 117. Bonus Boundary

If bonus is an HR-approved compensation instruction:

HR records decision/evidence.

Payroll calculates/applies amount/components according to Payroll configuration.

---

# 118. Payslip History In Employee Workspace

When Payroll active:

```text
Jan 2027
Feb 2027
Mar 2027
```

with summary/links.

Payroll remains source of finalized amounts.

---

# 119. HR Must Not Copy Payslip Lines

Use Payroll contract/read model.

---

# 120. Payroll Tab Hidden When Payroll Inactive

HR remains fully usable without Payroll.



# 121. Insurance / Statutory Enrollment Mission

Companies need to know whether an employee is enrolled/insured where legally relevant and to preserve evidence/history.

Core HR owns employee enrollment/status/evidence.

Country Pack and Payroll own statutory rules/calculation details.

---

# 122. Insurance / Enrollment Profile

Concept:

```text
EmployeeStatutoryEnrollment
{
    Id
    EmploymentId
    EnrollmentType
    ProviderOrAuthority?
    IdentifierEncrypted?
    Status
    EffectiveFrom
    EffectiveTo?
    RegistrationDate?
    DeregistrationDate?
    CountryProviderVersion?
    FileRefs[]
}
```

---

# 123. Enrollment Status

Examples:

```text
NotApplicable
Pending
Active
Suspended
Ended
```

Country Pack can map local statuses.

---

# 124. Core Does Not Hardcode Contribution Rates

Forbidden.

Rates/formulas/statutory thresholds belong:

```text
Payroll Country Provider
Country Pack / Compliance Provider
```

---

# 125. Insurance Number

Sensitive.

Masked by default.

---

# 126. Insurance History

Do not overwrite:

```text
insurance number/status/start/end
```

without historical record.

---

# 127. Insurance Documents

Can include:

```text
registration evidence
authority forms
coverage evidence
termination evidence
```

via Platform Files.

---

# 128. Insurance vs Health Insurance

Do not conflate:

```text
social/statutory insurance
private medical insurance
benefit enrollment
```

Model typed enrollment categories.

---

# 129. Benefits Boundary

Full benefits administration can evolve later.

Core HR may record active benefit enrollment/status and documents.

Payroll may consume contribution/deduction inputs.

---

# 130. Country Pack Responsibilities

Country Pack may contribute:

```text
required employee identifiers
required contract fields
official holidays
statutory leave rules/minimums
insurance/enrollment data fields
employment validation
local file/report formats
```

No local legal constants in Core.

---

# 131. Compliance Version Evidence

When a Country Pack validates a record, retain provider/rule version where relevant for audit.

---

# 132. Attendance Mission

Attendance answers:

```text
Was the employee expected to work?
When did they actually arrive/leave?
Were they late?
Did they leave early?
Was there an approved leave?
Was there overtime?
Was there an exception/correction?
```

---

# 133. Attendance Is Inside HR Pack

No separate Attendance design pack.

Internally preserve bounded services/entities.

---

# 134. Simple Attendance Flow

```text
Employee
→ Check In
→ Check Out
```

System handles work date/calendar.

---

# 135. Advanced Attendance Flow

Optional:

```text
Shift Assignment
→ Device/Import Punches
→ Normalize
→ Exceptions
→ Approval/Correction
→ Lock for Payroll
```

---

# 136. Attendance Source Types

Potential:

```text
Manual
Web
Mobile later
Imported
Device later
API integration
Admin correction
```

---

# 137. Raw Punch vs Attendance Record

Separate concepts.

Raw punch:

```text
timestamp/source/device evidence
```

Attendance Record:

```text
interpreted work-day result
```

---

# 138. Why Separate Raw Punch

Device/import history must remain evidence even if normalization rule changes.

---

# 139. Attendance Punch

Concept:

```text
AttendancePunch
{
    Id
    EmploymentId
    OccurredAtInstant
    LocalTimeSnapshot
    SourceType
    SourceRef?
    DirectionHint? // in/out/unknown
    ImportedBatchId?
    CreatedAt
}
```

---

# 140. Attendance Record

Concept:

```text
AttendanceRecord
{
    Id
    EmploymentId
    WorkDate
    WorkCalendarId
    ShiftAssignmentId?

    PlannedStart?
    PlannedEnd?
    ActualFirstIn?
    ActualLastOut?

    WorkedDuration?
    LateDuration?
    EarlyLeaveDuration?
    OvertimeEligibleDuration?

    AttendanceStatus
    NormalizationVersion
    LockState

    RowVersion
}
```

---

# 141. Attendance Status

Keep clear:

```text
Present
Absent
Partial
OnApprovedLeave
Holiday
RestDay
Exception
```

Do not overload with payroll outcome.

---

# 142. Lateness

Derived from:

```text
planned start
actual first in
grace policy
```

not manually typed by default.

---

# 143. Early Departure

Derived similarly.

---

# 144. Work Duration

Deterministic from valid punch/shift rules.

---

# 145. Breaks

Shift/calendar can define:

```text
paid break
unpaid break
```

if needed.

Small company can ignore.

---

# 146. Shift Definition

Concept:

```text
ShiftDefinition
{
    Id
    LegalEntityId
    Name
    StartLocalTime
    EndLocalTime
    BreakPolicy?
    GracePolicy?
    Overnight
    Status
}
```

---

# 147. Shift Assignment

Effective-dated per Employment:

```text
ShiftAssignment
{
    EmploymentId
    ShiftDefinitionId
    EffectiveFrom
    EffectiveTo?
}
```

---

# 148. No Shift Company

Can use WorkCalendar default hours or simple check-in/out with minimal interpretation.

---

# 149. Shift Rotation

Advanced capability inside HR.

Not required Day 1 unless customer need.

---

# 150. Overnight Shift Work Date

Resolve by assigned shift policy, not naive calendar date of checkout.

---

# 151. Time Zone

Store instants in UTC plus enough local/timezone context to explain human attendance.

---

# 152. Daylight Saving

Use timezone-aware libraries/rules.

Never calculate shift duration by naive local subtraction across DST boundary.

---

# 153. Duplicate Punch

Detection:

```text
same employee
same source
near-identical timestamp
same external id
```

idempotent import.

---

# 154. Missing Punch

Create Attendance Exception.

Do not silently invent checkout.

---

# 155. Attendance Exception

Examples:

```text
MissingCheckIn
MissingCheckOut
DuplicatePunch
OutOfShiftPunch
ExcessiveLate
UnexpectedWorkOnHoliday
OverlappingShift
```

---

# 156. Exception-Driven Attendance

Normal days require no HR work.

HR sees only exceptions.

---

# 157. Manual Attendance

Authorized user may enter attendance when no device/check-in method exists.

Must capture:

```text
actor
reason/source
timestamps
```

---

# 158. Attendance Correction

Never overwrite evidence silently.

Use correction record/history.

---

# 159. Attendance Correction Model

Concept:

```text
AttendanceCorrection
{
    AttendanceRecordId
    RequestedBy?
    CorrectedBy
    ReasonCode
    BeforeSnapshot
    AfterSnapshot
    AttachmentRef?
    ApprovedBy?
    CreatedAt
}
```

---

# 160. Correction Approval

Optional.

Small company HR can correct directly with audit.

Large company can require manager/HR approval.

---

# 161. Attendance Lock

After Payroll consumes/finalizes attendance for a period:

```text
LockedForPayroll
```

normal edits blocked.

---

# 162. Post-Payroll Attendance Correction

Requires:

```text
controlled adjustment
correction event
Payroll adjustment/off-cycle input
```

Do not mutate consumed history silently.

---

# 163. Attendance Lock Owner

HR stores lock/reference to Payroll consumption state.

Payroll remains source of finalized payroll period.

---

# 164. Attendance Import

Support CSV/Excel/API batch.

---

# 165. Attendance Import Preview

Before commit:

```text
Employee match
Timestamp
Source
Validation
Duplicate
Timezone
```

---

# 166. Device Integration

Later/evidence-based.

Use adapter interface.

Do not hardcode fingerprint vendor.

---

# 167. Device Employee Mapping

External device employee id maps to Employment.

Never use name matching each import.

---

# 168. GPS / Geofence

Not Day 1 by default.

Only implement where justified, with explicit privacy/legal review.

---

# 169. Mobile Check-In

Optional future capability.

Must handle:

```text
identity/auth
time
location policy if enabled
offline/replay idempotency
```

---

# 170. Attendance Dashboard

Simple:

```text
Present Today
Absent
Late
On Leave
Exceptions
```

---

# 171. Attendance List

Filters:

```text
Date
Branch
Department
Shift
Status
Employee
Exception
```

---

# 172. Attendance Detail Privacy

Managers can see team operational attendance.

They do not automatically gain salary/identity document access.

---

# 173. Absence

Absence may be derived when:

```text
employee expected to work
AND no valid attendance
AND no approved leave
AND not holiday/rest day
```

---

# 174. Absence Classification

Examples:

```text
Unexcused
Excused
PendingExplanation
ApprovedLeave
```

Use policy.

---

# 175. Absence Correction

If leave is approved later:

attendance/absence interpretation updates through controlled integration/history.

---

# 176. Absence Payroll Impact

HR supplies approved absence facts.

Payroll calculates money effect.

---

# 177. Overtime Boundary

Attendance can calculate/identify candidate overtime duration.

HR can approve qualifying overtime.

Payroll calculates final monetary amount.

---

# 178. Overtime Request

Optional.

Small company may allow HR-approved overtime directly.

---

# 179. Overtime Record

Concept:

```text
OvertimeApproval
{
    EmploymentId
    WorkDate
    RequestedDuration?
    EligibleDuration
    ApprovedDuration
    Reason
    Status
    ApprovedBy?
    PayrollImpactRef?
}
```

---

# 180. Overtime Does Not Equal Money In HR

No wage formula here.

---

# 181. Leave Mission

Leave should feel simple:

```text
Employee requests days
→ manager/HR approves if required
→ balance updates
```

while preserving exact entitlement history.

---

# 182. Leave Is Inside HR Pack

No separate Leave design pack.

---

# 183. Leave Type

Examples configured:

```text
Annual
Sick
Unpaid
Emergency
Maternity/Parental where country/company supports
Study
Other
```

Country Pack contributes statutory definitions/rules.

---

# 184. Leave Policy

Concept:

```text
LeavePolicy
{
    Id
    LegalEntityId
    LeaveTypeId
    Eligibility
    AccrualRule
    CarryoverRule
    NegativeBalancePolicy
    ApprovalPolicy
    CalendarDayPolicy
    EffectiveFrom
    EffectiveTo?
    Version
}
```

---

# 185. No Country Rules In Core

Do not hardcode:

```text
X annual days
Y sick days
Z maternity days
```

in Core HR.

---

# 186. Leave Balance Is Ledger-Like

Canonical balance comes from immutable movements.

Never store only mutable:

```text
annual_leave_balance = 17
```

with no history.

---

# 187. Leave Balance Ledger

Concept:

```text
LeaveBalanceMovement
{
    Id
    EmploymentId
    LeaveTypeId

    EffectiveDate
    MovementType
    Quantity

    SourceRef
    PolicyVersion?
    CreatedAt
}
```

---

# 188. Leave Movement Types

Examples:

```text
Opening
Accrual
Grant
CarryOver
Adjustment
RequestReservation
LeaveTaken
CancellationRestore
Expiry
Correction
```

Exact reservation design must avoid double counting.

---

# 189. Leave Balance

Derived:

```text
Opening
+ Accruals
+ Grants
+ Adjustments
- Taken/Committed
- Expired
```

according to policy.

---

# 190. Leave Request

Concept:

```text
LeaveRequest
{
    Id
    EmploymentId
    LeaveTypeId
    StartDate
    EndDate
    PartialDay?
    RequestedQuantity
    CalculatedChargeQuantity
    Reason?
    AttachmentRef?
    Status
    PolicyVersion
    RowVersion
}
```

---

# 191. Leave Request States

Simple:

```text
Draft
Pending
Approved
Rejected
Cancelled
Completed
```

If approval disabled:

```text
Submit → Approved
```

by policy/permission.

---

# 192. Partial-Day Leave

Support:

```text
Half day
Hours
```

only when company policy/leave type allows.

---

# 193. Leave Day Calculation

Uses:

```text
WorkCalendar
Holiday Calendar
Policy
Country Pack
```

not raw date difference.

---

# 194. Weekend/Holiday Handling

Policy-driven.

Different leave types/countries may count differently.

---

# 195. Leave Availability Check

Before approval:

```text
eligibility
balance
overlap
employment dates
policy
```

---

# 196. Negative Balance

Policy:

```text
Disallow
AllowWithinLimit
RequireApproval
Allow
```

---

# 197. Leave Reservation

Approved future leave can reserve entitlement to prevent overbooking.

Must remain explainable in balance ledger.

---

# 198. Leave Cancellation

Restores eligible reserved/consumed balance through movement.

Never direct balance update.

---

# 199. Leave Adjustment

Authorized HR can correct/grant/remove balance through explicit movement with reason.

---

# 200. Leave Opening Balance

Migration creates explicit Opening movements.

---

# 201. Leave Accrual

Scheduled deterministic job based on policy.

Idempotent by employee/leave type/period/policy version.

---

# 202. Accrual Recalculation

Do not silently rewrite historical movements.

Correction/recalculation creates controlled delta movements where needed.

---

# 203. Carryover

Policy/versioned job at boundary.

Approved Product Owner decision: the annual leave-year boundary is January 1. Leave consumption is allocated FIFO by earliest expiry. Each carried amount is a separate entitlement lot with its own expiry date, calculated from the boundary and the effective policy duration. Uncarried closing-year entitlement is closed through an immutable expiry movement.

---

# 204. Expiry

Explicit ledger movement.

---

# 205. Leave Approval

Default small company:

```text
Employee/HR request
→ Manager or HR approve
```

Can be simplified to direct HR approval.

---

# 206. Approval Matrix

Optional.

No multi-stage approval by default.

---

# 207. Manager Availability

Manager can view only team leave information necessary to decide.

Not full HR file.

---

# 208. Leave Overlap

Prevent overlapping approved requests unless policy explicitly allows partial/compatible overlap.

---

# 209. Leave vs Attendance

Approved leave informs attendance interpretation.

Do not duplicate leave status manually in Attendance.

---

# 210. Leave vs Payroll

Payroll consumes:

```text
paid/unpaid leave facts
eligible quantity
effective period
policy/context
```

and calculates money.

---

# 211. Leave Lock After Payroll

Consumed finalized period changes require adjustment flow.

---

# 212. Leave Calendar

Optional visual:

```text
team leave
department
branch
```

respecting privacy.

---

# 213. Leave Balance UX

Employee/HR sees:

```text
Entitled
Taken
Scheduled
Available
Expiring
```

with "View History".

---

# 214. Balance Explanation

Every balance number can drill into movements.

---

# 215. Leave Attachments

Sick leave/evidence can attach files where policy requires.

Sensitive classification applies.

---

# 216. Leave Notifications

Optional:

```text
request submitted
approved/rejected
upcoming leave
balance expiry
```

---

# 217. Leave History

Permanent.

Rejected/cancelled requests remain per retention policy.

---

# 218. Attendance + Leave Normal Path

For small company:

```text
Check In / Out
Leave Request
Manager/HR approval
```

No complex timesheet workflow.

---

# 219. Attendance/Leave Exception Queue

HR Home can surface:

```text
Missing checkout
Unexplained absence
Pending leave
Expired document
Contract expiring
Payroll input pending
```

---

# 220. Exception Queue Is Role-Aware

Manager sees team exceptions.

HR sees company HR exceptions.

Payroll officer sees payroll-impacting unresolved inputs only.



# 221. Onboarding Mission

Onboarding must make a new employee ready without creating unnecessary bureaucracy.

---

# 222. Small Company Onboarding

Default:

```text
Create Employee
→ Employment assignment
→ Contract
→ Required documents
→ Active
```

---

# 223. Advanced Onboarding

Optional checklist:

```text
Contract signed
ID documents
Insurance/enrollment
Laptop
Email
ID card
Policy acknowledgment
Training
Manager introduction
Workspace
Other company tasks
```

---

# 224. Onboarding Checklist Is Optional

No company should be blocked because it does not use asset/email/training workflow.

---

# 225. Onboarding Template

Can vary by:

```text
Legal Entity
Department
Position
Employment Type
Country
```

---

# 226. Onboarding Task

Concept:

```text
OnboardingTask
{
    Id
    EmploymentId
    TaskType
    OwnerUserId?
    DueDate?
    Status
    CompletionRef?
    RequiredForActivation
}
```

---

# 227. Employee Activation

Default activation requirements configurable.

Critical legal/company-required items can block.

Optional onboarding tasks cannot.

---

# 228. Pre-Hire

Support creating Employee/Employment before first working day.

Useful for document/contracts preparation.

---

# 229. Pre-Hire Does Not Mean Active Payroll

Payroll eligibility depends on employment effective date and Payroll policy.

---

# 230. Offboarding Mission

Preserve the employee's full history while safely ending the employment relationship.

---

# 231. Offboarding Triggers

```text
Resignation
Termination
Contract Expiry
Retirement
End of Temporary Assignment
Other controlled reason
```

---

# 232. Offboarding Record

Concept:

```text
OffboardingCase
{
    Id
    EmploymentId
    ReasonType
    NoticeDate?
    LastWorkingDate
    EffectiveEndDate
    Status
    FinalPayrollRequired
    CreatedBy
}
```

---

# 233. Offboarding Default Flow

Small company:

```text
Set last working day
→ reason
→ final HR checks
→ terminate employment
```

---

# 234. Advanced Offboarding

Optional tasks:

```text
Access revoke
Asset return
Exit interview
Final settlement input
Insurance deregistration
Document issuance
Manager handover
Leave balance review
```

---

# 235. IAM Link on Termination

HR can request/trigger access deactivation through IAM integration.

HR does not directly manipulate IAM tables.

---

# 236. Access Deactivation Timing

Policy can be:

```text
Immediately
Last working day
Custom controlled time
```

---

# 237. Asset Return Boundary

Fixed Assets/IT asset capability owns Asset truth.

HR tracks offboarding task/reference only.

---

# 238. Final Payroll Input

HR provides:

```text
last working date
unused leave facts where policy applies
approved deductions/allowances
termination reason/context
```

Payroll/Country Provider computes money.

---

# 239. Termination Effective Date

Employment becomes ended only on valid effective date.

Historical access/report remains.

---

# 240. Terminated Employee Workspace

Read-only historical by default.

Authorized HR can add post-employment document/correction events through controlled commands.

---

# 241. Rehire

Rehire:

```text
same Employee
→ new Employment
→ new Contract
→ new Assignment
```

Historical prior employment remains.

---

# 242. Rehire Search

Create Employee flow should detect former employee and offer:

```text
Rehire existing employee
```

---

# 243. Cross-Legal-Entity Transfer

Do not silently change LegalEntityId on active Employment.

Recommended:

```text
end/move old employment according to legal process
+
new Employment in target Legal Entity
+
typed transfer relation/event
```

---

# 244. Same-Legal-Entity Branch Transfer

New effective-dated Assignment.

No new Employee/Employment.

---

# 245. Department/Position Transfer

New Assignment.

---

# 246. Promotion

New Assignment + optional Compensation/Contract change.

All linked through common change reference/event.

---

# 247. One User Action, Multiple Correct Records

UX:

```text
[Promote Employee]
```

can orchestrate:

```text
new assignment
compensation agreement
contract amendment
history event
Payroll sync request
audit
outbox
```

without forcing HR user across tabs.

---

# 248. Domain Separation Must Not Fragment HR UX

Same TODIR principle as Sales/Purchasing.

---

# 249. Employee Self-Service

Optional capability inside HR.

Requires Employee ↔ User link.

---

# 250. Self-Service Default Scope

Potential:

```text
My Profile summary
My Attendance
My Leave
My Documents allowed for self
My Contracts allowed for self
My Payslips via Payroll
My Requests
```

---

# 251. Employee Self-Edit

Do not allow employee to edit authoritative employment facts directly.

Editable self-service fields may include:

```text
phone
address
emergency contact
bank/payment details later under controlled review
```

according to policy.

---

# 252. Sensitive Self-Change

Changes to:

```text
legal name
national identifier
bank account
insurance
```

require HR review/evidence if supported.

---

# 253. Manager Self-Service

Optional:

```text
My Team
Attendance exceptions
Leave approvals
Team schedule
Employee basic profile
```

---

# 254. Manager Does Not Automatically See Salary

Compensation permission separate.

---

# 255. Manager Does Not Automatically See IDs/Documents

Field/document permissions separate.

---

# 256. HR Home

Recommended:

```text
Employees
Present Today
On Leave
Pending HR Actions
Contracts Expiring
Documents Expiring
Attendance Exceptions
New Hires
Employees Leaving
```

---

# 257. Small Company HR Home

Hide unused advanced widgets.

---

# 258. HR Search

Global HR search:

```text
Employee number
Name
Department
Position
Branch
Contract number
Document metadata where permitted
```

---

# 259. Employee Directory View

Company-internal people list can expose only safe work fields.

Do not expose private HR profile.

---

# 260. Employee List Default Columns

```text
Employee No
Name
Position
Department
Branch
Manager
Status
```

---

# 261. Sensitive Columns Opt-In

Salary/insurance/identifiers never default list columns.

---

# 262. Bulk Actions

Useful:

```text
assign department
assign manager
set work calendar
export selected safe fields
```

only when valid; historical effective dates still required.

---

# 263. Bulk Compensation Changes

High risk.

If supported:

```text
import/preview
effective date
approval
per-employee validation
Payroll sync
```

---

# 264. Bulk Termination

Not ordinary UI.

Require explicit high-risk batch workflow if genuine need.

---

# 265. Employee History Principle

> **The Employee file must answer what was true at any important point in the employee's lifecycle.**

---

# 266. Current Value + History Pattern

For:

```text
Position
Department
Branch
Manager
Contract
Compensation
Insurance
Shift
```

current value is a projection of effective-dated history where applicable.

---

# 267. No Silent Historical Rewrite

Forbidden:

```text
UPDATE employee.salary = ...
UPDATE employee.department_id = ...
```

as the only history for material facts.

---

# 268. Correction vs Real Change

Distinguish:

```text
Data correction
```

from:

```text
Business change effective on date
```

---

# 269. Correction Example

Typo in manager record entered yesterday may be corrected with audit if underlying business event was never real.

---

# 270. Business Change Example

Promotion is not correction.

Must create effective-dated history.

---

# 271. Reason Codes

Typed reason codes useful for:

```text
Transfer
Promotion
Salary change
Termination
Penalty
Leave adjustment
Attendance correction
```

with optional note.

---

# 272. Reason Configuration

Company can configure codes but cannot remove historical used codes.

---

# 273. Attachments on Events

Material events can attach supporting document.

---

# 274. Employee History Export

Authorized HR can export employment history/report.

Sensitive fields controlled.

---

# 275. Payroll Integration Contract

HR should publish/offer typed contracts such as:

```text
GetEmployeePayrollContext
GetEmploymentSnapshot
GetAttendancePeriodSummary
GetApprovedLeavePayrollInputs
GetApprovedHrAdjustments
GetStatutoryEnrollmentContext
```

Exact contract names follow repository conventions.

---

# 276. Payroll Snapshot Requirement

Payroll Run consumes a deterministic HR snapshot/cutoff.

It must not query mutable current employee data line-by-line while finalizing.

---

# 277. Payroll Cutoff

When Payroll starts/finalizes:

```text
HR inputs through cutoff/version
```

must be identifiable.

---

# 278. HR Changes During Payroll Run

Policy:

```text
include before cutoff
defer after cutoff
explicit refresh before finalization
```

No silent moving target.

---

# 279. Payroll Consumed Input Lock

Attendance/leave/adjustments used by finalized Payroll become protected.

Corrections produce delta/adjustment inputs.

---

# 280. HR Does Not Mark Payslip Final

Payroll-owned.

---

# 281. Payroll Results in Employee Workspace

Read-only from Payroll:

```text
period
gross
deductions
net
status
payslip link
```

only for permitted users.

---

# 282. Compensation Privacy

Salary-related access independent permission.

---

# 283. Payroll Officer Scope

Payroll officer can access necessary compensation/payroll inputs without all disciplinary/identity documents.

---

# 284. HR Officer Scope

HR officer can manage employment/documents but may not post/finalize Payroll.

---

# 285. HR Privacy Model

Permissions need both:

```text
Action
+
Data Scope
```

---

# 286. Data Scopes

Potential:

```text
Self
DirectReports
ManagementChain
Department
Branch
LegalEntity
AllAccessibleEntities
SpecificEmployees
```

---

# 287. Field Groups

Sensitive field groups:

```text
BasicEmployment
PersonalContact
PersonalIdentity
Compensation
Insurance
Attendance
Leave
Disciplinary
Documents
HighlySensitiveDocuments
PayrollSummary
```

---

# 288. Permission Examples

```text
hr.employee.read_basic
hr.employee.read_personal
hr.employee.manage

hr.compensation.read
hr.compensation.change

hr.identifier.read_masked
hr.identifier.reveal
hr.identifier.manage

hr.contract.read
hr.contract.manage

hr.document.read
hr.document.read_sensitive
hr.document.manage

hr.attendance.read
hr.attendance.manage
hr.attendance.correct

hr.leave.read
hr.leave.request
hr.leave.approve
hr.leave.adjust_balance

hr.disciplinary.read
hr.disciplinary.manage

hr.insurance.read
hr.insurance.manage

hr.offboarding.manage
hr.audit.read
```

---

# 289. Hidden UI Is Not Security

Backend enforces every field/action rule.

---

# 290. Field-Level Response Shaping

API/read model should omit/redact fields user lacks permission for.

Do not send salary then hide with CSS.

---

# 291. Sensitive Search

A user without identity permission cannot search by full national identifier.

---

# 292. Manager Scope Resolution

Backend resolves manager hierarchy from HR Employment relationships.

---

# 293. Self Scope Resolution

User must be linked to Employee.

No client-supplied EmployeeId trust.

---

# 294. Delegation

Temporary manager/approval delegation can be later/Platform Approvals capability.

Do not hardcode into Employee hierarchy.

---

# 295. Document Access

File access token issued only after HR permission check.

---

# 296. Privacy Logging

Do not log:

```text
salary values
national IDs
passport numbers
insurance numbers
document contents
disciplinary narratives
```

unless explicitly security-audited and masked.

---

# 297. Encryption

Sensitive identifier/selected personal fields use approved encryption strategy.

Keys not stored in business rows.

---

# 298. Database Backups

Encrypted according to platform policy.

HR sensitivity must be included in backup/restore threat model.

---

# 299. Audit Critical HR Actions

Capture:

```text
Employee create
Employment activation
Assignment change
Contract activation/amendment
Compensation change
Sensitive identifier reveal/change
Document upload/delete/supersede
Attendance correction
Leave balance adjustment
Penalty/disciplinary action
Insurance change
Termination
Rehire
Permission-sensitive export
```

---

# 300. Audit vs Business History

Audit answers:

```text
who changed the system
```

Business history answers:

```text
what happened to employment
```

Both required.

---

# 301. Soft Delete

Active/historical HR business records generally archive/supersede, not hard delete.

---

# 302. Draft Deletion

Unused drafts may be deleted according to policy.

Used/effective employment facts remain.

---

# 303. Data Retention

Country/legal policy may require retention after termination.

Core supports configurable retention/archival.

Do not hardcode one period globally.

---

# 304. Right-to-Privacy / Deletion Requests

Legal handling may require:

```text
restrict
pseudonymize
retain legally required employment evidence
```

Country/legal review required.

---

# 305. Sensitive Data Export

Requires dedicated permission and audit.

---

# 306. HR Reports Must Respect Field Permissions

A report/export cannot bypass UI restrictions.

---

# 307. HR Country Pack Boundary

Core remains country-neutral.

Country Pack can provide:

```text
identifier definitions
contract validations
official holiday feeds/rules
statutory leave policy templates
insurance/enrollment fields
labor/compliance validations
required forms/templates
```

---

# 308. No Legal Claims In Core

Do not encode legal values from memory/general assumptions.

Every statutory value/rule requires verified Country Pack source/version.

---

# 309. Country Pack Upgrade

Changing legal rule version must not rewrite historical employment/leave/payroll inputs.

---

# 310. Historical Rule Evidence

Store policy/provider version where necessary for reproducibility.

---

# 311. Custom Fields

Platform Custom Fields can extend Employee/Employment/Contract records.

---

# 312. Custom Field Restrictions

Sensitive custom field can have restricted visibility.

---

# 313. Custom Fields Not Payroll Inputs By Default

Arbitrary custom field cannot affect Payroll calculation until promoted to approved typed Payroll input.

---

# 314. Custom Document Types

Company can classify custom Employee Documents.

Do not allow arbitrary custom type to bypass sensitivity rules.

---

# 315. Approvals

Use Platform Approvals where advanced workflow needed.

Do not build a second generic approval engine inside HR.

---

# 316. Approval Use Cases

Optional:

```text
Leave
Attendance correction
Overtime
Compensation change
Penalty
Contract amendment
Termination
```

---

# 317. Default Small Company Approvals

Keep minimal:

```text
Leave → Manager/HR
High-risk HR change → HR authorized user
```

---

# 318. Notifications

Use Platform Notifications for:

```text
contract expiry
document expiry
probation ending
leave request
attendance exception
onboarding task
offboarding task
insurance expiry
```

---

# 319. Notification Failure

Does not corrupt HR transaction.

Outbox/retry as appropriate.

---

# 320. Scheduler

Use Platform jobs for:

```text
document expiry scans
contract expiry scans
leave accrual
carryover/expiry
future assignment activation projections
probation reminders
```

---

# 321. Scheduled Job Idempotency

Every HR scheduled financial/entitlement-affecting job must be idempotent.

---

# 322. AI Opportunities

AI may:

```text
extract document fields for review
summarize employee file
suggest duplicate employee
summarize attendance exceptions
draft HR letter
suggest missing documents
answer authorized HR questions
```

---

# 323. AI Restrictions

AI must not:

```text
terminate employee
approve leave autonomously
apply penalty
change salary
calculate final Payroll
change insurance status
reveal sensitive data without permission
write HR DB directly
```

---

# 324. OCR

OCR can propose:

```text
document type
identifier
issue/expiry dates
name
issuer
```

User verifies before authoritative save.

---

# 325. AI Sensitive Context

Only minimum authorized fields sent to AI layer.

Respect field permissions/data policy.

---

# 326. HR Events

Potential integration events:

```text
EmployeeCreatedV1
EmploymentActivatedV1
EmployeeTransferredV1
EmployeePromotedV1
EmploymentEndedV1
EmployeeRehiredV1

ContractActivatedV1
ContractExpiringV1

CompensationAgreementChangedV1
HrAdjustmentApprovedV1

AttendanceCorrectedV1
AttendancePeriodLockedV1

LeaveApprovedV1
LeaveCancelledV1
LeaveBalanceAdjustedV1

StatutoryEnrollmentChangedV1
EmployeeDocumentExpiringV1
```

Publish only meaningful boundaries.

---

# 327. Outbox

State change + integration event through Platform Outbox.

---

# 328. Broker Is Not HR Invariant

Employee creation/leave approval/attendance correction does not require RabbitMQ to commit.

---

# 329. Idempotency

Mandatory for:

```text
employee import
rehire
contract activation
compensation change
attendance import
leave accrual
leave approval
balance adjustment
termination
Payroll input export/snapshot
```

where retries possible.

---

# 330. Same Idempotency Key Different Payload

Conflict.

---

# 331. Concurrency — Employee Edit

Use row version/optimistic concurrency.

---

# 332. Concurrency — Leave Balance

Two leave approvals cannot over-consume same entitlement.

Use transaction + balance revalidation/locking where needed.

---

# 333. Concurrency — Attendance Correction

Stale correction cannot overwrite newer correction silently.

---

# 334. Concurrency — Compensation Change

Overlapping effective compensation agreements rejected/controlled.

---

# 335. Concurrency — Termination vs Transfer

Commands validate current Employment state/version.

---

# 336. Transaction Coordinator

Use for same-database cross-slice atomic operations when one user action must update:

```text
Employment + Assignment + Contract + History + Audit + Outbox
```

---

# 337. External APIs

Never call:

```text
government service
device vendor
email
SMS
document OCR provider
```

inside HR DB transaction.

---

# 338. HR Go-Live

Per Legal Entity:

```text
HrGoLiveDate
EmployeeMigrationStrategy
AttendanceHistoryBoundary
LeaveOpeningBoundary
DocumentMigrationStrategy
```

---

# 339. HR Deactivation

Never delete employee history.

Lifecycle:

```text
WindDown
ReadOnly
Disabled UX
```

according to Platform lifecycle.

---

# 340. Payroll Dependency On HR

If Payroll active, HR core cannot be destructively deactivated.

Historical employee/payroll source refs must remain.



# 341. HR Schema

Recommended:

```text
business_hr
```

One customer-facing HR module.

---

# 342. HrDbContext

Recommended one module-owned `HrDbContext` initially.

Large internal bounded areas can use internal repositories/services/read models.

Do not split into microservices/independent databases.

---

# 343. Core Tables

Potential:

```text
employee
employment
employment_assignment
employment_event

department
position
work_calendar
holiday_calendar_ref
shift_definition
shift_assignment

employment_contract
employment_contract_amendment

employee_identifier
employee_contact
employee_emergency_contact

employee_document
employee_document_requirement
employee_qualification
employee_credential

employee_compensation_agreement
employee_hr_adjustment
disciplinary_action

employee_statutory_enrollment

attendance_punch
attendance_record
attendance_exception
attendance_correction
overtime_approval

leave_type
leave_policy
leave_balance_movement
leave_request

onboarding_case
onboarding_task
offboarding_case
offboarding_task

employee_external_ref
employee_list_projection
employee_timeline_projection
hr_exception_projection
```

Exact normalization can evolve without changing ownership.

---

# 344. Tables Not Owned By HR

Do not create:

```text
user_account
iam_role
payroll_run
payslip
salary_component_calculation
journal_entry
payment
asset
```

---

# 345. Tenant Columns

All tenant-owned HR tables contain `tenant_id`.

---

# 346. Legal Entity Scope

Employment-dependent rows should carry/derive LegalEntity safely.

Use tenant/entity-safe foreign keys where practical.

---

# 347. RLS

All tenant-owned HR tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 348. Runtime Role

No ownership/BYPASSRLS.

---

# 349. Tenant Session Context

Explicit transaction with:

```text
SET LOCAL app.tenant_id = ...
```

following Platform Kernel rules.

---

# 350. Field Security Is Above RLS

RLS prevents cross-tenant row access.

It does not solve:

```text
manager can see salary?
HR assistant can reveal passport?
employee can see disciplinary file?
```

Application authorization/redaction is mandatory.

---

# 351. UUIDv7

Use for HR entity IDs.

---

# 352. Date/Time Types

Use NodaTime concepts:

```text
LocalDate
Instant
DateTimeZone
Duration
```

according to semantic need.

---

# 353. Effective Dating

For histories:

```text
effective_from
effective_to
```

with no overlapping active windows where invariant requires.

---

# 354. Database Range Constraints

Where helpful, PostgreSQL exclusion constraints/range logic may protect non-overlap.

Use only with tested RLS/migration behavior.

---

# 355. Row Version

Mutable aggregates/drafts use optimistic concurrency.

---

# 356. Sensitive Values

Encrypted fields should still support:

```text
masked display
exact duplicate lookup where securely designed
```

If blind index/hash is used, document threat model.

---

# 357. Search Projection

Employee list/search projection contains only safe commonly searchable fields.

Sensitive identifiers excluded by default.

---

# 358. Employee Timeline Projection

Rebuildable from typed source records/events.

Not independent editable truth.

---

# 359. Exception Projection

Rebuildable list for:

```text
documents expiring
contracts expiring
attendance exceptions
pending leave
missing required documents
insurance issues
```

---

# 360. Indexes — Employee

Likely:

```text
tenant/legal_entity/employee_number
tenant/name normalized
tenant/status
tenant/legal_entity/department/status
tenant/legal_entity/branch/status
tenant/legal_entity/manager/status
```

---

# 361. Indexes — Contract

```text
tenant/employment/status/end_date
tenant/legal_entity/end_date where active
```

---

# 362. Indexes — Documents

```text
tenant/employee/document_type
tenant/status/expiry_date
```

---

# 363. Indexes — Attendance

High-volume:

```text
tenant/employment/work_date
tenant/legal_entity/work_date/status
tenant/work_date/exception
source external id unique
```

---

# 364. Indexes — Leave

```text
tenant/employment/start_date
tenant/status/start_date
tenant/employment/leave_type/effective movement date
```

---

# 365. Attendance Partitioning

Do not partition Day 1 without measured need.

Design append-heavy records/indexes first.

---

# 366. HR Contracts Assembly

Locked naming:

```text
Todir.HR.Contracts
```

Use `HR` or repository-approved casing consistently.

---

# 367. Backend Module Naming

Recommended:

```text
Todir.Modules.HR.Domain
Todir.Modules.HR.Application
Todir.Modules.HR.Infrastructure
Todir.HR.Contracts
```

No new `Erp.*`.

---

# 368. Internal Domain Areas

Potential code folders:

```text
Employees/
Employment/
Organization/
Contracts/
Documents/
Compensation/
Insurance/
Attendance/
Leave/
Onboarding/
Offboarding/
History/
```

---

# 369. No Generic CRUD Service

Use business commands/queries.

---

# 370. Employee Commands

Representative:

```text
CreateEmployee
CreateEmployment
ActivateEmployment
CorrectEmployeeProfile
LinkEmployeeUser
UnlinkEmployeeUser
RehireEmployee
```

---

# 371. Assignment Commands

```text
TransferEmployee
PromoteEmployee
ChangeDepartment
ChangeManager
ChangePosition
ScheduleAssignmentChange
```

Can share internal handler primitives but preserve semantic command names/evidence.

---

# 372. Contract Commands

```text
CreateContract
ActivateContract
AmendContract
RenewContract
EndContract
AttachSignedContract
```

---

# 373. Compensation Commands

```text
ChangeContractualCompensation
ApproveHrAllowance
EndHrAllowance
ApplyDisciplinaryAction
ApprovePayrollImpactInstruction
```

---

# 374. Document Commands

```text
UploadEmployeeDocument
VerifyEmployeeDocument
SupersedeEmployeeDocument
ArchiveEmployeeDocument
```

---

# 375. Insurance Commands

```text
StartStatutoryEnrollment
UpdateEnrollmentStatus
EndStatutoryEnrollment
AttachEnrollmentEvidence
```

---

# 376. Attendance Commands

```text
CheckIn
CheckOut
ImportAttendancePunches
RecordManualAttendance
RequestAttendanceCorrection
ApproveAttendanceCorrection
LockAttendancePeriodForPayroll
```

---

# 377. Leave Commands

```text
CreateLeaveRequest
SubmitLeaveRequest
ApproveLeaveRequest
RejectLeaveRequest
CancelLeaveRequest
AdjustLeaveBalance
RunLeaveAccrual
```

---

# 378. Offboarding Commands

```text
StartOffboarding
RecordResignation
TerminateEmployment
CompleteOffboardingTask
EndEmployment
```

---

# 379. No Generic SetStatus

Forbidden public command:

```text
SetEmployeeStatus("whatever")
```

Use semantic transitions.

---

# 380. No Generic SetSalary

Use effective-dated compensation command.

---

# 381. No Generic SetLeaveBalance

Use ledger movement.

---

# 382. No Generic SetAttendance

Use record/correction command with evidence.

---

# 383. API Base

Recommended:

```text
/api/v1/hr
```

---

# 384. Employee APIs

Representative:

```text
GET  /employees
POST /employees
GET  /employees/{id}
PUT  /employees/{id}/profile

GET  /employees/{id}/timeline
GET  /employees/{id}/employment
GET  /employees/{id}/documents
GET  /employees/{id}/contracts
GET  /employees/{id}/compensation
GET  /employees/{id}/payroll
```

Response fields permission-shaped.

---

# 385. Employment APIs

```text
POST /employees/{id}/employments
POST /employments/{id}/activate
POST /employments/{id}/transfer
POST /employments/{id}/promote
POST /employments/{id}/change-manager
POST /employments/{id}/end
POST /employees/{id}/rehire
```

---

# 386. Department APIs

```text
GET  /departments
POST /departments
PUT  /departments/{id}
POST /departments/{id}/archive
```

---

# 387. Position APIs

Similar controlled CRUD for definitions.

---

# 388. Contract APIs

```text
GET  /employees/{id}/contracts
POST /employments/{id}/contracts
POST /contracts/{id}/activate
POST /contracts/{id}/amend
POST /contracts/{id}/renew
POST /contracts/{id}/end
```

---

# 389. Document APIs

```text
POST /employees/{id}/documents
POST /documents/{id}/verify
POST /documents/{id}/supersede
GET  /documents/{id}/access
```

---

# 390. Compensation APIs

```text
GET  /employees/{id}/compensation
POST /employments/{id}/compensation-changes
POST /employments/{id}/hr-adjustments
POST /hr-adjustments/{id}/approve
```

---

# 391. Attendance APIs

```text
GET  /attendance
POST /attendance/check-in
POST /attendance/check-out
POST /attendance/import
POST /attendance/{id}/corrections
POST /attendance/corrections/{id}/approve
GET  /attendance/exceptions
```

---

# 392. Leave APIs

```text
GET  /leave/types
GET  /leave/balances
GET  /leave/requests
POST /leave/requests
POST /leave/requests/{id}/submit
POST /leave/requests/{id}/approve
POST /leave/requests/{id}/reject
POST /leave/requests/{id}/cancel
POST /leave/balances/adjust
```

---

# 393. Self-Service APIs

Use same authorization-aware HR application APIs or dedicated safe façade.

Do not create second truth.

---

# 394. Manager Team API

```text
GET /my-team
```

resolves manager scope server-side.

---

# 395. HR Home API

```text
GET /home
GET /exceptions
```

returns capability-aware summary.

---

# 396. Sensitive Reveal API

If supported:

```text
POST /employees/{id}/identifiers/{identifierId}/reveal
```

with step-up/audit policy.

Never include full identifier in general employee response.

---

# 397. Files Upload Flow

Upload through Platform Files.

HR command stores validated `FileRef` + metadata.

---

# 398. External Integrations

Adapters:

```text
AttendanceDeviceProvider
GovernmentEmploymentProvider later
InsuranceProvider later
DocumentOCRProvider
```

No provider-specific fields leaked across core domain unless typed capability.

---

# 399. Import Employees

First-class migration/admin feature.

Template can include:

```text
Employee number
Name
Legal Entity
Branch
Department
Position
Manager ref
Hire date
Employment type
Status
Contact
```

Sensitive fields separate where possible.

---

# 400. Employee Import Preview

Before commit:

```text
Matched department
Matched position
Manager resolution
Duplicate warning
Required field validation
Country Pack validation
```

---

# 401. Contract Import

Support active/historical contracts with source refs.

Do not require fake event dates when unknown; preserve migration quality flag.

---

# 402. Document Migration

Can upload/archive:

```text
files
metadata
expiry dates
document type
legacy path/reference
```

---

# 403. Attendance Migration

Company chooses historical boundary.

Do not force import years of punch detail if not needed.

---

# 404. Leave Opening Migration

Create explicit opening entitlement movements.

---

# 405. Leave Historical Migration

If historical requests matter, import typed history.

Otherwise define HistoricalBoundary.

---

# 406. Compensation History Migration

Import available effective salary/contract history.

Unknown history should not be fabricated.

---

# 407. Insurance Migration

Import current/historical status and evidence where available.

---

# 408. Migration Source References

Store:

```text
legacy system
legacy employee id
batch id
file hash
row reference
```

---

# 409. Import Idempotency

Same source/batch row cannot create duplicate Employee/Attendance/Leave movement.

---

# 410. Import Failure Strategy

Row-level validation report.

No half-hidden skipped employees.

---

# 411. Export

Authorized HR exports:

```text
Employee list
Employment history
Contract expiry
Document expiry
Attendance summary
Leave balance
Leave history
Insurance status
HR adjustments
```

---

# 412. Export Redaction

Only fields user can read are exported.

---

# 413. Large Export

Background job + secure file ref.

---

# 414. Reporting — Core HR

```text
Headcount
Active Employees
New Hires
Terminations
Employees by Branch
Employees by Department
Employees by Position
Contract Expiry
Document Expiry
Insurance Status
```

---

# 415. Attendance Reports

```text
Daily Attendance
Late
Early Leave
Absence
Overtime Approved
Attendance Exceptions
```

---

# 416. Leave Reports

```text
Leave Balance
Leave Taken
Upcoming Leave
Leave by Type
Leave Liability input later via Payroll/Accounting if required
```

---

# 417. Compensation Reports

Restricted:

```text
Current Contractual Compensation
Salary Change History
Active Allowances
Approved Penalties/Adjustments
Payroll Sync Differences
```

---

# 418. Employee File Completeness Report

```text
missing required document
expired document
missing active contract
missing assignment
insurance pending
```

based on enabled requirements.

---

# 419. HR Analytics Boundary

Basic operational HR metrics here.

Advanced workforce analytics/BI later.

---

# 420. Headcount Definition

Must define scope:

```text
Active Employment as of date
Legal Entity
Branch
Employment type
```

No vague dashboard number.

---

# 421. Historical Headcount

Effective-dated Employment/Assignment enables AsOf reporting.

---

# 422. Turnover Metrics

Can be derived later from hire/end events.

Definition must be explicit.

---

# 423. Performance Management Boundary

Not in core `17`.

Do not build:

```text
OKRs
360 reviews
performance cycles
succession
```

now.

---

# 424. Recruitment / ATS Boundary

Not in core `17`.

Future Recruitment can convert hired candidate → Employee.

---

# 425. Learning Boundary

Not full LMS.

Qualifications/credentials remain in HR.

---

# 426. Expenses Boundary

Expenses consumes Employee identity/employment.

HR does not own Expense Claim.

---

# 427. Projects Boundary

Projects can assign Employee/User resources.

HR owns employment truth.

---

# 428. POS Boundary

POS can reference Employee cashier when useful.

HR does not own POS session.

---

# 429. Fixed Assets Boundary

Asset can be assigned/responsible to Employee.

Asset ownership/history remains Fixed Assets.

---

# 430. Performance Principle

Employee Workspace must feel instant for normal use.

Do not load every HR subdomain up front.

---

# 431. Employee Overview Target

Initial measured target:

```text
p95 <= 200-350ms
```

for core current summary on realistic dataset.

---

# 432. Employee Search Target

```text
p95 <= 100-250ms
```

for indexed search/page under realistic company size.

---

# 433. Attendance Daily List Target

```text
p95 <= 200-400ms
```

for branch/day/status query.

---

# 434. Leave Balance Target

Interactive.

Avoid replaying entire leave ledger in application code every request.

Use transactional/rebuildable balance projection.

---

# 435. Leave Balance Projection

Allowed:

```text
employment
leave_type
available/reserved/current totals
as_of/version
```

maintained transactionally/rebuildable from movements.

---

# 436. Attendance Summary Projection

Can maintain daily/monthly summaries for performance.

Raw punches + normalized records remain source.

---

# 437. HR Home Projection

Rebuildable:

```text
headcount
exceptions
expiring docs/contracts
today attendance
pending requests
```

---

# 438. No N+1

Employee list cannot query per row:

```text
Department
Position
Manager
Contract
Document alert
```

individually.

---

# 439. Employee Workspace Composition

Core summary one bounded query.

Tabs load on demand:

```text
Attendance
Leave
Documents
History
Payroll
```

---

# 440. Attendance Volume

Load test with:

```text
10M+ punch/attendance records
```

for larger tenants.

---

# 441. Employee Volume

Load test at least:

```text
100k employees/worker records
```

for large-tenant architecture validation, even if typical customers smaller.

---

# 442. Contract/Document Expiry Scan

Use indexed date windows.

No full table scan every hour.

---

# 443. Scheduler Frequency

Expiry/reminder jobs daily usually sufficient unless business requires finer.

---

# 444. Redis

No Redis Day 1 for canonical HR truth.

---

# 445. Local Cache

Safe for:

```text
policy metadata
leave type definitions
work calendar templates
country provider metadata
```

with version invalidation.

---

# 446. Do Not Cache Sensitive Employee Profiles Loosely

Per-request authorization must remain correct.

---

# 447. PostgreSQL First

No OpenSearch Day 1.

If employee search proves insufficient at scale, measure/ADR.

---

# 448. Keyset Pagination

Use for:

```text
employee history
attendance
documents
HR events
```

deep lists.

---

# 449. Observability Metrics

Potential:

```text
hr_employee_search_duration
hr_employee_workspace_duration
hr_attendance_import_duration
hr_attendance_exception_count
hr_leave_request_duration
hr_leave_accrual_duration
hr_leave_balance_conflict_total
hr_contract_expiry_count
hr_document_expiry_count
hr_payroll_input_sync_error_total
```

No EmployeeId as metric label.

---

# 450. Tracing

Commands include:

```text
Resolve tenant/entity
Authorization
Load employment
Validate effective dates
Policy/Country Pack validation
Persist
Audit
Outbox
Commit
```

---

# 451. Logging

No sensitive employee payload dumps.

---

# 452. Alerts

Technical/operational:

```text
leave accrual job failures
attendance import failures
Payroll input sync failures
document storage failures
outbox backlog
```

---

# 453. HR Health

Optional HR admin card:

```text
Employee data readiness
Contract/document expiry
Attendance exceptions
Leave jobs
Payroll integration
```

---

# 454. Support Tool

Authorized support can inspect:

```text
IDs
states
source refs
job/error metadata
```

without revealing sensitive contents unless elevated.

---

# 455. No Direct DB Fix

Support does not edit salary/leave/attendance/termination by SQL.

Use controlled commands.



# 456. Error Codes

Stable examples:

```text
hr.employee_not_found
hr.employee_duplicate_suspected
hr.employee_user_link_conflict

hr.employment_not_active
hr.employment_state_conflict
hr.assignment_overlap
hr.manager_cycle
hr.transfer_invalid

hr.contract_overlap
hr.contract_not_activatable
hr.contract_expired
hr.contract_amendment_conflict

hr.compensation_overlap
hr.compensation_change_not_allowed
hr.payroll_sync_pending

hr.document_expired
hr.document_required
hr.document_access_denied

hr.identifier_access_denied
hr.identifier_duplicate

hr.attendance_duplicate_punch
hr.attendance_missing_punch
hr.attendance_locked_for_payroll
hr.attendance_correction_conflict

hr.leave_insufficient_balance
hr.leave_overlap
hr.leave_not_eligible
hr.leave_policy_missing
hr.leave_balance_locked

hr.insurance_status_invalid

hr.termination_conflict
hr.rehire_conflict

hr.permission_denied
hr.concurrent_change
hr.idempotency_conflict
```

---

# 457. Friendly Error — Leave

> "الرصيد المتاح لا يكفي لهذه الإجازة."

Show:

```text
Available
Requested
Policy
```

and allowed next actions.

---

# 458. Friendly Error — Contract

> "يوجد عقد نشط يتداخل مع تاريخ العقد الجديد."

---

# 459. Friendly Error — Payroll-Locked Attendance

> "تم استخدام هذا الحضور في راتب نهائي. أنشئ تصحيحًا ليتم ترحيله كتسوية بدل تعديل السجل القديم."

---

# 460. Friendly Error — Manager Cycle

> "لا يمكن تعيين هذا المدير لأن ذلك سيُنشئ دائرة في الهيكل الإداري."

---

# 461. Security Tests — Cross Tenant

Tenant A cannot:

```text
search
read
export
download documents
modify
```

Tenant B employee data.

---

# 462. Security Tests — Cross Entity

User scoped to Entity A cannot access Entity B Employment even if same Employee master exists, unless explicit authorized cross-entity HR permission.

---

# 463. Security Test — Salary Hidden

Manager without compensation permission:

API omits/redacts salary.

---

# 464. Security Test — Identifier Hidden

Normal HR viewer cannot reveal full national identifier.

---

# 465. Security Test — Sensitive Document

User without sensitive-document permission cannot obtain signed download URL.

---

# 466. Security Test — Self Scope

Employee User cannot change path EmployeeId to read another employee.

---

# 467. Security Test — Manager Scope

Manager sees only permitted reports/team.

---

# 468. Security Test — Export

Export respects exact field restrictions.

---

# 469. Security Test — Logs

Automated test/scanner verifies sensitive values not emitted in structured logs where feasible.

---

# 470. Test — Create Employee Minimal

Small company can create:

```text
Name
Employee Number/default
Legal Entity
Branch/Department/Position as configured
Hire Date
```

without enterprise fields.

---

# 471. Test — User Not Required

Employee creation succeeds with no User account.

---

# 472. Test — Optional User Link

Existing authorized User links exactly once.

---

# 473. Test — Duplicate User Link

One User cannot accidentally represent two active Employees unless explicit supported exception/ADR.

---

# 474. Test — Rehire

Same Employee gets new Employment.

Old history remains.

---

# 475. Test — Assignment History

Transfer changes current projection on effective date while prior assignment remains.

---

# 476. Test — Future Promotion

Not current before effective date.

---

# 477. Test — Assignment Overlap

Invalid overlap blocked.

---

# 478. Test — Manager Cycle

Blocked.

---

# 479. Test — Department Cycle

Blocked.

---

# 480. Test — Position Archive

Historical employees still display old Position title/reference.

---

# 481. Test — Contract Activation

Draft→Active with exact terms/evidence.

---

# 482. Test — Contract Renewal

Old contract remains; new linked contract becomes current when effective.

---

# 483. Test — Contract Amendment

Material term change history preserved.

---

# 484. Test — Contract Expiry Alert

Generated at configured threshold, idempotently.

---

# 485. Test — Missing Required Document

Readiness alert appears only where requirement applies.

---

# 486. Test — Document Supersede

New file becomes current; old remains historical.

---

# 487. Test — Document Security

File URL not reusable publicly.

---

# 488. Test — Credential Expiry

Correct derived status/alert.

---

# 489. Test — Compensation Change

Effective-dated history exact.

---

# 490. Test — Future Salary Change

Current value remains old until effective date.

---

# 491. Test — Payroll Link

Payroll receives correct effective compensation context.

---

# 492. Test — HR Allowance

Approved instruction reaches Payroll input exactly once.

---

# 493. Test — HR Penalty

Disciplinary history exists independently of Payroll monetary result.

---

# 494. Test — Penalty Without Payroll Impact

No deduction created.

---

# 495. Test — Payroll Inactive

HR allowances/penalties/history still usable as HR records if company needs, without pretending payroll result exists.

---

# 496. Test — Insurance Active

Current status + history exact.

---

# 497. Test — Insurance Number Masking

Default API/view masked.

---

# 498. Test — Country Pack Validation

Invalid country-specific field rejected by provider, not hardcoded Core branch.

---

# 499. Test — Simple Check-In/Out

Creates correct punches/AttendanceRecord.

---

# 500. Test — Duplicate Check-In

Idempotent/controlled.

---

# 501. Test — Missing Check-Out

Exception, no invented time.

---

# 502. Test — Overnight Shift

WorkDate/duration correct.

---

# 503. Test — Timezone

Same UTC instant maps correctly to local attendance semantics.

---

# 504. Test — Attendance Correction

Original evidence/history preserved.

---

# 505. Test — Direct HR Correction

When approval disabled, authorized HR can correct with reason/audit in one action.

---

# 506. Test — Approval Correction

When enabled, correction waits for approval.

---

# 507. Test — Payroll-Locked Attendance

Normal edit blocked.

---

# 508. Test — Post-Payroll Correction

Produces adjustment path without changing consumed snapshot.

---

# 509. Test — Attendance Import Retry

No duplicate punches.

---

# 510. Test — Leave Opening

Opening movement creates correct balance.

---

# 511. Test — Leave Accrual

Idempotent period accrual.

---

# 512. Test — Leave Balance

Movement replay/projection yields same result.

---

# 513. Test — Concurrent Leave Approval

Cannot consume balance twice beyond policy.

---

# 514. Test — Leave Weekend Rule

Uses active WorkCalendar/policy/Country Pack.

---

# 515. Test — Partial Day

Correct charge quantity.

---

# 516. Test — Leave Cancellation

Restores eligible balance via movement.

---

# 517. Test — Leave Overlap

Blocked.

---

# 518. Test — Negative Leave Policy

Each configured mode behaves exactly.

---

# 519. Test — Leave After Payroll Lock

Controlled adjustment path.

---

# 520. Test — Approved Leave and Attendance

Attendance displays OnApprovedLeave rather than false absence.

---

# 521. Test — Absence

Expected working day + no attendance/leave derives absence.

---

# 522. Test — Overtime

Approved duration delivered to Payroll; HR never calculates money.

---

# 523. Test — Onboarding Simple

No checklist required beyond configured minimum.

---

# 524. Test — Advanced Onboarding

Required task can block activation; optional task cannot.

---

# 525. Test — Termination

Employment ends; Employee not deleted.

---

# 526. Test — IAM Deactivation Request

Linked access deactivation happens through IAM contract/event, not HR DB write.

---

# 527. Test — Offboarding Final Payroll

Payroll gets correct last-working-date/context.

---

# 528. Test — Rehire After Termination

New Employment links same Employee.

---

# 529. Test — Cross-Entity Transfer

No silent LegalEntity mutation.

---

# 530. Test — Timeline

All typed events ordered correctly by effective/occurred semantics.

---

# 531. Test — As-Of Employment

Query can answer:

```text
employee's branch/department/position on date X
```

exactly.

---

# 532. Test — Historical Manager

AsOf manager correct after later changes.

---

# 533. Test — Historical Compensation

AsOf agreement correct.

---

# 534. Test — Historical Contract

Correct active contract on date.

---

# 535. Test — 100k Employee Search

Measured indexed performance.

---

# 536. Test — 10M Attendance Records

Daily/employee query meets target without N+1/full scan.

---

# 537. Test — Large Timeline

Cursor paging.

---

# 538. Test — Expiry Job Scale

Contract/document expiry scan bounded/indexed.

---

# 539. Test — HR Home

No per-employee queries.

---

# 540. Test — Employee Workspace

Initial load does not fetch all attendance/leave/payroll/document history.

---

# 541. Property Test — Assignment Non-Overlap

For primary assignment:

```text
no two effective intervals overlap
```

for same Employment.

---

# 542. Property Test — Contract Policy

Where policy allows one active primary contract:

active contract intervals do not overlap.

---

# 543. Property Test — Compensation

No ambiguous overlapping primary compensation agreements.

---

# 544. Property Test — Leave Ledger

Projection balance equals sum of canonical movements under policy semantics.

---

# 545. Property Test — Leave Idempotency

Same accrual key produces one movement.

---

# 546. Property Test — Attendance Lock

Consumed records cannot mutate through ordinary commands.

---

# 547. Architecture Test — HR Does Not Own Payroll

No:

```text
PayrollRun
Payslip finalization
Payroll tax formula
Net salary calculation
```

inside HR module.

---

# 548. Architecture Test — HR Does Not Own IAM User

Only optional UserId contract/reference.

---

# 549. Architecture Test — HR Does Not Own Files Bytes

Only FileRef.

---

# 550. Architecture Test — HR Does Not Own Org Branch

References Organization.

---

# 551. Architecture Test — No Direct Payroll DbContext

Use Payroll contracts/events.

---

# 552. Architecture Test — No Direct IAM DbContext

Use IAM contracts.

---

# 553. Architecture Test — No Direct Object Storage SDK In Domain

File infrastructure adapter/platform service only.

---

# 554. Red-Team — Employee = User

Reject.

---

# 555. Red-Team — Every Employee Must Have Login

Reject.

---

# 556. Red-Team — Salary Stored Only as Mutable Employee Field

Reject.

---

# 557. Red-Team — Department Stored Only on Employee Row

Reject for history.

Use effective Assignment.

---

# 558. Red-Team — Contract File Only, No Structured Data

Reject.

Need searchable structured contract facts + file evidence.

---

# 559. Red-Team — Contract Edited In Place After Active

Reject for material terms.

---

# 560. Red-Team — Leave Balance Mutable Number

Reject.

Ledger movements.

---

# 561. Red-Team — Attendance Punch Deleted To Correct

Reject.

Correction/history.

---

# 562. Red-Team — Attendance After Payroll Edited In Place

Reject.

---

# 563. Red-Team — HR Calculates Net Salary

Reject.

---

# 564. Red-Team — HR Hardcodes Egypt/Saudi/UAE Statutory Rates

Reject.

Country/Payroll provider.

---

# 565. Red-Team — Manager Can See Salary Because They Manage Employee

Reject.

Separate permission.

---

# 566. Red-Team — API Sends Salary Then UI Hides It

Reject.

Backend redaction.

---

# 567. Red-Team — Permanent Public Employee Document URL

Reject.

---

# 568. Red-Team — Employee Deleted On Termination

Reject.

---

# 569. Red-Team — Rehire Creates Duplicate Person Automatically

Reject.

Offer rehire.

---

# 570. Red-Team — GPS Tracking By Default

Reject.

---

# 571. Red-Team — Leave Approval Has Five Steps For Small Company

Reject.

---

# 572. Red-Team — Every HR Feature Loads On Employee Page

Reject.

Progressive/lazy.

---

# 573. Red-Team — Disciplinary Free Text Automatically Deducts Salary

Reject.

Typed approved Payroll input only.

---

# 574. Red-Team — OCR Auto-Saves National ID

Reject.

Human verification.

---

# 575. Red-Team — AI Terminates/Disciplines Employee

Reject.

---

# 576. Product Acceptance — Small Company

User can:

```text
Create Employee
Add contract
Upload documents
Track attendance
Approve leave
View history
```

without advanced setup.

---

# 577. Product Acceptance — Paperless Employee File

HR opens one Employee Workspace and finds:

```text
current employment
contracts
documents
certificates
insurance
attendance
leave
allowances/penalties
payroll-linked history
full timeline
```

according to permission.

---

# 578. Product Acceptance — Large Company

Can enable:

```text
multiple departments/branches
manager hierarchy
shifts
leave accrual
approvals
sensitive permissions
onboarding/offboarding
Payroll integration
```

without replacing core.

---

# 579. Product Acceptance — HR Privacy

A manager can perform operational tasks without seeing restricted HR details.

---

# 580. Product Acceptance — Employee Self-Service

Linked Employee/User can access only own allowed data.

---

# 581. Product Acceptance — History

No HR manager needs to ask:

```text
When did salary change?
What was old contract?
When did employee transfer?
Who approved penalty?
Was employee insured?
```

without finding answer in TODIR.

---

# 582. Product Acceptance — Speed

Normal Employee Workspace opens fast and tabs load progressively.

---

# 583. Product Acceptance — Payroll Readiness

HR supplies deterministic, approved, cutoff-safe inputs to Payroll.

---

# 584. Product Acceptance — No Paper Dependency

A company can operate employee archive digitally where local legal acceptance permits.

TODIR does not claim digital file legally replaces originals in every jurisdiction; Country/legal policy decides required originals.



# 585. Agent Implementation Philosophy

The coding Agent must implement HR in ordered, verifiable vertical slices.

Do not start with:

```text
all database tables
then all APIs
then all UI
```

without proving real employee workflows.

Each phase must deliver usable business behavior + tests + security.

---

# 586. Agent Precedence

If implementation conflicts:

```text
17 HR pack
→ 01 Master Blueprint for platform/module laws
→ 03 Platform Kernel
→ 02 Stack
→ applicable Country Pack
→ later approved ADR
```

Payroll ownership remains governed by `18 Payroll` when created.

---

# 587. Agent Must Read

Before HR implementation:

```text
01
02
03
17
relevant Organization/IAM/File/Audit implementation
all AGENTS.md
approved ADRs
repository module patterns
```

Also inspect Payroll contracts if they already exist.

---

# 588. Agent Must Inspect Repository First

Report:

```text
Already exists
Partially exists
Missing
Conflicts
```

for HR foundations.

Do not duplicate existing platform Files/IAM/Approvals/Notifications.

---

# 589. Agent Task Template

Every phase should be assigned with:

```text
TASK

BUSINESS GOAL
What HR user problem this solves.

CONTEXT
Existing source-of-truth modules/services.

ARCHITECTURE RULES
Non-negotiable ownership/history/privacy rules.

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

DATABASE REQUIREMENTS

PRIVACY / SECURITY REQUIREMENTS

PERFORMANCE REQUIREMENTS

UX REQUIREMENTS

TESTS REQUIRED

ACCEPTANCE CRITERIA

EXIT GATE

REPORT BACK WITH
- files changed
- migrations
- tests
- performance evidence
- security evidence
- unresolved decisions
- risks
```

---

# 590. Agent Must Not Over-Engineer

Do not introduce:

```text
microservices
Kafka
Redis
OpenSearch
complex workflow DSL
separate Leave service
separate Attendance service
```

without measured/approved need.

---

# 591. Agent Must Not Under-Engineer History

Simplicity does not justify overwriting:

```text
contract
assignment
compensation
attendance corrections
leave balance history
insurance history
```

---

# 592. Implementation Sequence

```text
HR00 Repository + Architecture Baseline
HR01 Module Boundary + Schema + RLS
HR02 Employee + Employment Core
HR03 Employee Search/List/Workspace Shell
HR04 Departments + Positions
HR05 Effective-Dated Assignment + Manager Hierarchy
HR06 Employment History / Timeline
HR07 Contracts
HR08 Employee Documents + Secure Files
HR09 Identifiers + Personal/Emergency Data Security
HR10 Qualifications + Credentials
HR11 Insurance / Statutory Enrollment
HR12 Contractual Compensation History
HR13 HR Allowances / Penalties / Adjustments
HR14 Payroll Integration Contracts
HR15 Work Calendars + Holidays
HR16 Shifts + Shift Assignments
HR17 Attendance Punch + Normalization
HR18 Attendance Exceptions + Corrections
HR19 Attendance Payroll Lock
HR20 Leave Types + Policies
HR21 Leave Balance Ledger
HR22 Leave Requests + Approval
HR23 Leave Accrual / Carryover / Expiry
HR24 Attendance + Leave Integration
HR25 Overtime / Absence Inputs
HR26 Onboarding
HR27 Offboarding / Termination / Rehire
HR28 Self-Service + Manager Self-Service
HR29 HR Home + Exception Queue
HR30 Reporting + Exports
HR31 Import / Migration
HR32 Notifications + Expiry Jobs
HR33 Permissions + Field Restrictions + Audit
HR34 Country Pack / Localization Integration
HR35 Performance + Load
HR36 Security / Privacy Red-Team
HR37 Runbooks / Recovery
HR38 HR Acceptance Test
HR39 Payroll Input Acceptance
HR40 Release Gate
```

---

# 593. HR00 — Goal

Understand the existing platform/repository before creating HR.

---

# 594. HR00 Verify

```text
Organization LegalEntity/Branch contracts
IAM User/Membership contracts
Platform Files
Audit
Notifications
Scheduler
Outbox
RLS conventions
Numbering
Custom Fields
Approvals
Transaction Coordinator
```

---

# 595. HR00 Output

Agent report maps each HR dependency to actual repository implementation.

---

# 596. HR00 Exit Gate

No duplicate platform service planned.

---

# 597. HR01 — Goal

Create one HR module boundary with secure schema.

---

# 598. HR01 Implement

```text
Todir.Modules.HR.Domain
Todir.Modules.HR.Application
Todir.Modules.HR.Infrastructure
Todir.HR.Contracts
business_hr schema
HrDbContext
module manifest
RLS baseline
architecture tests
```

---

# 599. HR01 Module Manifest

Concept:

```text
id: hr
type: business-module
purchasable: true
```

Internal capabilities:

```text
hr.core
hr.contracts
hr.documents
hr.attendance
hr.leave
hr.insurance
hr.self_service
```

---

# 600. HR01 Historical Blueprint Consolidation

Previous planning treated HR Core, Leave and Attendance as separate architecture slices.

This pack consolidates them under one customer-facing `hr` module while keeping internal bounded areas.

Payroll remains independent.

---

# 601. HR01 Architecture Tests

Fail if HR:

```text
owns User entity
owns Branch entity
owns Payslip/PayrollRun
stores file bytes
references Payroll implementation assembly
bypasses platform Audit/RLS
```

---

# 602. HR01 Exit Gate

Module boots with RLS-isolated empty HR schema and capability-aware frontend shell.

---

# 603. HR02 — Goal

Create trustworthy Employee/Employment identity.

---

# 604. HR02 Implement

```text
Employee
Employment
Employee Numbering
status
optional User link
optional Party link
rehire foundation
```

---

# 605. HR02 UX

One `New Employee` action creates simple company record.

---

# 606. HR02 Tests

```text
User not required
number uniqueness
cross-tenant
cross-entity
rehire detection
concurrency
```

---

# 607. HR02 Exit Gate

Employee can exist correctly with no login, no Payroll and minimal data.

---

# 608. HR03 — Goal

Employee Workspace is useful immediately.

---

# 609. HR03 Build

```text
Employee list
search
overview
tabs shell
safe current employment summary
```

---

# 610. HR03 Rule

Only load overview data initially.

---

# 611. HR03 Exit Gate

HR user finds an employee quickly and understands current employment.

---

# 612. HR04 — Goal

Model basic organization structure owned by HR.

---

# 613. HR04 Build

```text
Department
Department hierarchy
Position
archive/history-safe definitions
```

---

# 614. HR04 Tests

```text
hierarchy cycle
duplicate code
archive used item
entity scope
```

---

# 615. HR04 Exit Gate

Departments/positions can be used without creating account/user records.

---

# 616. HR05 — Goal

Preserve complete organizational assignment history.

---

# 617. HR05 Build

```text
EmploymentAssignment
effective dates
Branch
Department
Position
Manager
transfer/promotion/change manager commands
```

---

# 618. HR05 Tests

```text
future assignment
overlap
manager cycle
AsOf query
```

---

# 619. HR05 Exit Gate

TODIR can answer where/under whom employee worked on any known historical date.

---

# 620. HR06 — Goal

Create meaningful permanent Employee Timeline.

---

# 621. HR06 Build

Typed Employment Events + unified timeline projection.

---

# 622. HR06 Exit Gate

Current + prior assignments/events traceable without Audit-table spelunking.

---

# 623. HR07 — Goal

Make Contracts a complete digital employment record.

---

# 624. HR07 Build

```text
Contract structured data
draft/activate/end
renewal
amendment
expiry
signed file link
history
```

---

# 625. HR07 No Workflow Bloat

Small company can:

```text
Add Contract
→ Save/Activate
```

with correct permissions.

---

# 626. HR07 Exit Gate

Old contract/amendment never disappears after renewal.

---

# 627. HR08 — Goal

Replace paper employee archive safely.

---

# 628. HR08 Build

```text
Document metadata
FileRef
categories
sensitivity
expiry
verification
supersede/version
required document rules
secure download
```

---

# 629. HR08 Security Tests

Permission and signed-access URL.

---

# 630. HR08 Exit Gate

Company can store/search employee files without public file access or DB blobs.

---

# 631. HR09 — Goal

Protect sensitive identity/personal information.

---

# 632. HR09 Build

```text
typed identifiers
masking
encryption
reveal permission
personal/emergency contacts
field restriction policy
```

---

# 633. HR09 Exit Gate

Normal HR/manager APIs do not leak restricted identifiers.

---

# 634. HR10 — Goal

Structured qualifications/credentials.

---

# 635. HR10 Build

```text
qualification
credential/license
expiry
verification
file
```

---

# 636. HR10 Exit Gate

HR can query expiring employee credentials without searching document folders manually.

---

# 637. HR11 — Goal

Track insurance/statutory enrollment without hardcoding local payroll law.

---

# 638. HR11 Build

```text
typed enrollment
identifier
status history
effective dates
documents
Country Pack validation
```

---

# 639. HR11 Exit Gate

HR can answer whether employee is enrolled/insured according to configured local capability and show evidence/history.

---

# 640. HR12 — Goal

Keep contractual compensation history separate from Payroll calculations.

---

# 641. HR12 Build

```text
effective compensation agreement
salary history
future changes
contract link
reason/evidence
Payroll sync/read status
```

---

# 642. HR12 Tests

No overlapping ambiguous current agreements.

---

# 643. HR12 Exit Gate

HR sees salary history; no net-pay formula exists in HR code.

---

# 644. HR13 — Goal

Record allowances/penalties correctly as HR decisions and Payroll inputs.

---

# 645. HR13 Build

```text
HR adjustment/instruction
allowance
disciplinary action
evidence
approval optional
Payroll-impact flag
history
```

---

# 646. HR13 Exit Gate

Free text cannot directly become a payroll deduction.

---

# 647. HR14 — Goal

Define deterministic HR→Payroll boundary before implementing Payroll.

---

# 648. HR14 Contracts

At minimum define versioned read/snapshot capabilities for:

```text
Employment
Contract
Compensation
Approved HR Adjustments
Attendance Summary
Leave Inputs
Overtime
Insurance Context
```

---

# 649. HR14 Snapshot Rule

Payroll consumes batch/snapshot by period/cutoff.

No per-employee mutable N+1 truth during finalization.

---

# 650. HR14 Exit Gate

A fake/stub Payroll consumer can retrieve one coherent period input package without HR internals.

---

# 651. HR15 — Goal

Correct work calendar/timezone semantics.

---

# 652. HR15 Build

```text
WorkCalendar
weekly pattern
holiday refs
timezone
effective assignment
```

---

# 653. HR15 Exit Gate

Working/nonworking day resolution is deterministic across Legal Entities.

---

# 654. HR16 — Goal

Support simple and shift-based companies.

---

# 655. HR16 Build

```text
ShiftDefinition
ShiftAssignment
overnight handling
grace/break policies
```

---

# 656. HR16 Exit Gate

Company with no shifts still works; shift company gets correct planned time.

---

# 657. HR17 — Goal

Create canonical Attendance truth.

---

# 658. HR17 Build

```text
Punch
Manual source
Import source
Normalization
AttendanceRecord
duplicate protection
```

---

# 659. HR17 Exit Gate

Check-in/out and batch import produce same canonical normalized model.

---

# 660. HR18 — Goal

Make HR work exception-driven.

---

# 661. HR18 Build

```text
Missing punch
AttendanceException
Correction
optional approval
audit
```

---

# 662. HR18 Exit Gate

Normal days need no HR action; broken days are clear/actionable.

---

# 663. HR19 — Goal

Protect attendance used by Payroll.

---

# 664. HR19 Build

```text
Payroll consumption reference
lock
post-lock correction/delta path
```

---

# 665. HR19 Exit Gate

Finalized Payroll cannot be undermined by silent attendance edits.

---

# 666. HR20 — Goal

Define leave types/policies without country hardcoding.

---

# 667. HR20 Build

```text
LeaveType
LeavePolicy
effective/versioned rules
Country Pack templates
```

---

# 668. HR20 Exit Gate

Policy can model simple annual leave and local statutory types through configuration/provider.

---

# 669. HR21 — Goal

Trustworthy leave entitlement history.

---

# 670. HR21 Build

```text
LeaveBalanceMovement
balance projection
opening
grant
adjustment
reservation/taken semantics
```

---

# 671. HR21 Exit Gate

Every displayed leave balance can be explained by movements.

---

# 672. HR22 — Goal

Fast Leave Request flow.

---

# 673. HR22 Build

```text
Request
calculate charge
availability
submit
approve/reject
cancel
```

---

# 674. HR22 Small Default

One manager/HR approval at most; optionally direct authorized approval.

---

# 675. HR22 Exit Gate

No unnecessary multi-step workflow.

---

# 676. HR23 — Goal

Automate entitlements safely.

---

# 677. HR23 Build

```text
Accrual
Carryover
Expiry
Jobs
Idempotency
Corrections
```

---

# 678. HR23 Exit Gate

Re-running job cannot duplicate entitlement.

---

# 679. HR24 — Goal

Attendance/Leave tell one coherent absence story.

---

# 680. HR24 Build

Approved leave integration, absence derivation, late retroactive approval handling.

---

# 681. HR24 Exit Gate

Employee on approved leave is not falsely shown absent.

---

# 682. HR25 — Goal

Provide Payroll-ready overtime/absence facts without money calculation.

---

# 683. HR25 Build

```text
absence classification
overtime eligible/approved duration
Payroll input contracts
```

---

# 684. HR25 Exit Gate

Payroll receives durations/facts, not HR-computed money.

---

# 685. HR26 — Goal

Simple onboarding with optional advanced checklist.

---

# 686. HR26 Build

```text
PreHire
OnboardingCase
templates/tasks
activation gates
```

---

# 687. HR26 Exit Gate

Small company can skip non-required checklist complexity.

---

# 688. HR27 — Goal

Complete employee lifecycle safely.

---

# 689. HR27 Build

```text
Resignation
Termination
Offboarding
Last Working Date
IAM deactivation request
Payroll final input
Rehire
Cross-entity transfer
```

---

# 690. HR27 Exit Gate

Termination never deletes Employee; rehire preserves history.

---

# 691. HR28 — Goal

Optional employee/manager self-service without separate truth.

---

# 692. HR28 Build

```text
My Profile
My Attendance
My Leave
My Documents
My Payslips link
My Team
Manager approvals
```

---

# 693. HR28 Exit Gate

All self/team scopes server-resolved; no IDOR.

---

# 694. HR29 — Goal

HR Home focuses attention on exceptions.

---

# 695. HR29 Build

```text
Headcount
Today Attendance
Pending HR actions
Contracts/Documents expiring
Attendance exceptions
New hires
Offboarding
Payroll-input warnings
```

---

# 696. HR29 Exit Gate

Healthy company does not show noisy task flood.

---

# 697. HR30 — Goal

Operational reports and secure exports.

---

# 698. HR30 Build

Core HR/Attendance/Leave/Expiry reports.

---

# 699. HR30 Exit Gate

Reports use explicit definitions and respect field permissions.

---

# 700. HR31 — Goal

Migrate companies from paper/Excel/legacy HR.



# 701. HR31 Build

```text
Employee import
Employment/assignment import
Contract import
Document migration
Compensation history import
Insurance import
Attendance history boundary
Leave opening/history
Legacy refs
Validation preview
Idempotency
```

---

# 702. HR31 Migration Rule

Do not fabricate missing history.

Mark imported quality/context explicitly when source is incomplete.

---

# 703. HR31 Exit Gate

Active employees, their current employment truth and required opening entitlements reconcile to approved migration source.

---

# 704. HR32 — Goal

Automate reminders without corrupting business state.

---

# 705. HR32 Build

```text
Contract expiry
Document expiry
Credential expiry
Probation reminder
Insurance expiry
Leave balance expiry
Onboarding/offboarding reminders
```

---

# 706. HR32 Exit Gate

Jobs are idempotent, indexed and notification failure does not roll back HR truth.

---

# 707. HR33 — Goal

Protect employee data correctly.

---

# 708. HR33 Build

```text
permissions
scope policies
field redaction
sensitive reveal
document access
audit
export permission
SoD where relevant
```

---

# 709. HR33 Mandatory Personas

Test:

```text
HR Admin
HR Specialist
Manager
Payroll Officer
Employee Self-Service
Owner/Executive HR viewer
Auditor/Compliance reader
System Admin without HR data permission
```

---

# 710. HR33 Exit Gate

Each persona receives only required data/actions.

---

# 711. HR34 — Goal

Keep Core HR country-neutral while enabling regional compliance.

---

# 712. HR34 Build

Provider interfaces for:

```text
Employee identifier validation
Contract requirements
Holiday calendar
Statutory leave templates/rules
Insurance/enrollment definitions
Employment validations
Official form/report adapters later
```

---

# 713. HR34 Golden Rule

No statutory constants in generic HR Core.

---

# 714. HR34 Exit Gate

At least one test Country Provider proves extension path without country-specific branching throughout HR.

---

# 715. HR35 — Goal

Prove performance for real HR datasets.

---

# 716. HR35 Load Tests

At minimum:

```text
100k employees
10M attendance records
large leave ledger
multi-year employee timeline
thousands of expiring docs/contracts
large daily attendance import
concurrent leave approvals
employee search
HR Home
```

---

# 717. HR35 Query Plan Review

Use real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot searches/reports in performance environment.

---

# 718. HR35 No Premature Infrastructure

Do not introduce search cluster/cache before measured evidence.

---

# 719. HR35 Exit Gate

Measured targets met or evidence-based ADR created.

---

# 720. HR36 — Goal

Privacy/security red-team before release.

---

# 721. HR36 Attack Cases

```text
Cross-tenant employee read
Cross-entity employment read
Manager guesses another EmployeeId
Employee self-service IDOR
Salary visible in network response but hidden UI
Sensitive document URL reuse
Identifier leakage in logs
Unauthorized export
Attendance correction after Payroll lock
Leave balance tampering
Forged manager relationship
Forged FileRef
Country Provider bypass
```

---

# 722. HR36 Exit Gate

RLS + application authorization + field redaction + secure Files access all pass.

---

# 723. HR37 — Goal

Operations can diagnose/repair HR without destroying history.

---

# 724. HR37 Runbooks

Required:

```text
Duplicate Employee suspected
Wrong User link
Assignment overlap migration issue
Manager cycle
Contract wrongly activated
Document upload failed
Expiry job failed
Attendance import duplicate
Missing punch
Attendance normalization error
Payroll-locked correction
Leave accrual failed
Leave balance mismatch
Payroll input sync failed
Termination/IAM deactivation mismatch
Projection rebuild
Restore/DR validation
```

---

# 725. HR37 Forbidden Runbook

Never ordinary support fix:

```text
UPDATE employee SET salary...
UPDATE leave_balance...
DELETE attendance_punch...
UPDATE employment SET department...
```

---

# 726. HR37 Correction Principle

Use semantic corrective commands and append history.

---

# 727. HR37 Exit Gate

Critical failures have supported diagnosis/correction paths.

---

# 728. HR38 — Formal HR Acceptance Test

Use HR professional who did not build the module.

---

# 729. HR38 Scenario A — New Employee

Reviewer creates:

```text
Employee
Employment
Assignment
Contract
Required documents
```

without developer help.

---

# 730. HR38 Scenario B — Employee File

Reviewer finds:

```text
current job
manager
contract
salary history
insurance
documents
certificates
attendance
leave
adjustments
history
```

subject to permission.

---

# 731. HR38 Scenario C — Promotion

Reviewer promotes employee and confirms:

```text
old assignment preserved
new assignment effective correctly
salary/contract changes linked
history clear
```

---

# 732. HR38 Scenario D — Contract Renewal

Reviewer renews contract without overwriting previous evidence.

---

# 733. HR38 Scenario E — Attendance

Reviewer:

```text
checks employee attendance
finds missing checkout
corrects with reason
```

in minimal steps.

---

# 734. HR38 Scenario F — Leave

Reviewer:

```text
checks balance
creates leave
approves
sees balance movement
cancels
sees restoration
```

---

# 735. HR38 Scenario G — Insurance/Documents

Reviewer identifies:

```text
employee not enrolled/pending
document expiring
contract expiring
```

without spreadsheet.

---

# 736. HR38 Scenario H — Termination/Rehire

Reviewer ends employment then rehires same Employee without duplicate history.

---

# 737. HR38 Pass Criteria

Reviewer confirms:

```text
employee file complete
history trustworthy
workflows short
search fast
privacy understandable
documents easy to find
no paper/Excel dependency for normal HR operation
```

---

# 738. HR39 — Payroll Input Acceptance

Run with Payroll stub/test consumer or actual `18 Payroll` implementation later.

---

# 739. HR39 Input Package

For payroll period, retrieve:

```text
eligible employees
employment status
contract context
compensation agreement
approved allowances/penalties
attendance summary
absence
overtime approved
leave paid/unpaid context
insurance/enrollment context
cutoff/version
```

---

# 740. HR39 No Money Calculation

HR package contains facts/approved inputs.

Payroll determines monetary result.

---

# 741. HR39 Scenario — Salary Change Mid-Month

Payroll receives correct effective context according to Payroll policy.

HR does not prorate final salary itself.

---

# 742. HR39 Scenario — Late Attendance Correction

If period already finalized:

HR generates correction/delta input for Payroll adjustment path.

---

# 743. HR39 Scenario — Leave Change After Cutoff

No silent mutation of consumed Payroll snapshot.

---

# 744. HR39 Exit Gate

Payroll can deterministically reproduce which HR inputs it consumed.

---

# 745. HR40 — Release Gate

HR does not ship because forms exist.

It ships when the company can trust the employee digital file.

---

# 746. HR40 Required Sign-Off

```text
Product Architect
Senior HR Reviewer
Payroll Domain Reviewer
Backend Lead
Frontend Lead
Security/Privacy Reviewer
Performance Engineer
QA Lead
Country Pack/Compliance Reviewer for enabled market
```

---

# 747. HR40 Mandatory Evidence

```text
Automated tests
RLS/authorization report
Field-redaction tests
Document security tests
History/effective-date tests
Attendance/Leave concurrency tests
Performance benchmarks
Migration test
HR Acceptance results
Payroll input acceptance
Known limitations
```

---

# 748. Build Now — MUST

```text
Employee + Employment
Departments + Positions
Manager hierarchy
Effective assignment history
Contracts + renewals/amendments
Employee Documents
Sensitive identifiers
Qualifications/credentials
Insurance/enrollment status
Contractual compensation history
Allowances/penalties HR inputs
Payroll integration contracts
Work Calendars
Shifts
Attendance
Corrections/exceptions
Payroll lock
Leave policies
Leave balance ledger
Leave requests
Accrual/carryover/expiry
Absence/overtime input
Onboarding
Offboarding
Termination/Rehire
Employee Timeline
HR Home
Permissions/field restrictions
Audit
Import/export
Performance
Country Pack seams
HR acceptance tests
```

---

# 749. Build Later / Evidence-Based

```text
Recruitment / ATS
Full LMS / Training
Performance Reviews
OKRs
360 Feedback
Succession Planning
Advanced Workforce Planning
Position Headcount Budgeting
Complex Shift Optimization
Biometric Vendor Integrations
GPS / Geofencing
Advanced Benefits Administration
Employee Engagement
Survey Platform
AI HR Copilot
Government HR integrations per market
```

Do not delay trustworthy employee file for these.

---

# 750. Fresh Small Company Defaults

Recommended:

```text
Employee Core              ON
Contracts                  ON
Documents                  ON
Attendance                 Simple
Leave                      Simple
Shifts                     OFF unless needed
Leave Accrual Advanced     OFF unless configured
Onboarding Checklist       OFF
Offboarding Checklist      OFF
Manager Approval           Simple
Employee Self-Service      OFF until users linked
Sensitive Field Controls   ON
Payroll Integration        Available, inactive until Payroll
```

---

# 751. Large Company Capability Preset

Can enable:

```text
Department hierarchy
Manager hierarchy
Shift schedules
Leave accrual
Approval policies
Document requirements
Insurance compliance
Onboarding/offboarding checklists
Self-Service
Manager Self-Service
Advanced field permissions
```

without changing HR Core.

---

# 752. Employee File Completeness Goal

Opening one employee should eliminate searching through:

```text
paper folder
HR Excel
attendance sheet
contract folder
insurance folder
salary letters
certificate folder
email history
```

for normal authorized HR questions.

---

# 753. Employee History Goal

TODIR must answer:

```text
What was the employee's role on date X?
Which branch?
Which manager?
Which contract?
What compensation agreement?
Was insurance active?
What leave/attendance changes occurred?
Which document supported the change?
Who performed the action?
```

when underlying source data exists.

---

# 754. Privacy Goal

The Employee Workspace is comprehensive **for authorized users**, not universally visible.

---

# 755. Simplicity Goal

A company with 20 employees should not need:

```text
grade structures
complex shifts
approval matrices
onboarding workflows
benefits engines
```

just to create employee and track attendance/leave.

---

# 756. Scale Goal

A company with thousands of employees must not outgrow the same HR core.

Advanced controls are capabilities, not a new product fork.

---

# 757. Final Source-of-Truth Matrix

```text
User Account / IAM                   → Platform IAM
Legal Entity / Branch                → Organization
Employee / Employment                → HR
Department / Position                → HR
Assignment / Manager History         → HR
Contract                             → HR
Employee Documents                   → HR + Platform Files
Qualifications / Credentials         → HR
Insurance/Enrollment Status          → HR + Country Provider
Attendance                           → HR
Leave / Entitlement                  → HR
HR Allowance/Penalty Decision        → HR
Payroll Calculation / Payslip        → Payroll
Payroll Statutory Formula            → Payroll Country Provider
Payroll Accounting                   → Payroll → Ledger
Employee Payment                     → Payroll/Payments
```

---

# 758. Final Employee Flow — Small Company

```text
Create Employee
→ Add Contract
→ Upload Documents
→ Track Attendance
→ Manage Leave
→ View History
```

---

# 759. Final Employee Flow — Large Company

```text
PreHire
→ Onboarding
→ Assignment
→ Contract
→ Documents/Insurance
→ Shift/Attendance
→ Leave
→ Compensation/HR Adjustments
→ Payroll Inputs
→ Transfers/Promotions
→ Offboarding
→ Historical Archive
```

Only enabled complexity appears.

---

# 760. Final Contract Rule

> **A contract is structured employment truth plus documentary evidence; it is never only a PDF attachment and never silently overwritten.**

---

# 761. Final History Rule

> **Material employee changes are effective-dated/append-history business events, not mutable fields that erase the past.**

---

# 762. Final Attendance Rule

> **Raw evidence and corrections remain traceable; Payroll-consumed attendance cannot be silently changed.**

---

# 763. Final Leave Rule

> **Leave balance is explainable from ledger movements, not a mysterious mutable number.**

---

# 764. Final Compensation Rule

> **HR records contractual/approved compensation facts; Payroll calculates the paycheck.**

---

# 765. Final Privacy Rule

> **Being a manager, HR user, system admin or employee does not automatically grant access to every employee field/document.**

---

# 766. Final Country Rule

> **Country-specific legal requirements belong versioned Country/Payroll providers; Core HR never guesses statutory law.**

---

# 767. Final Product Verdict

TODIR HR is approved only when:

```text
Employee != User is preserved.
Employee can exist without login.
One Employee can preserve multiple employments/rehire history.
Branch/department/position/manager history is effective-dated.
Contracts are structured, versioned and linked to signed files.
Salary/compensation history is preserved without duplicating Payroll calculations.
Allowances/penalties are typed HR decisions and safe Payroll inputs.
Insurance/enrollment history is country-aware and secure.
Employee documents replace fragmented archive while respecting privacy.
Qualifications/certificates are searchable and expiry-aware.
Attendance is explainable and correction-safe.
Leave balance is ledger-based and concurrency-safe.
Payroll-consumed HR inputs cannot be silently rewritten.
Termination never deletes history.
Rehire does not create duplicate workforce identity.
Small-company HR remains fast and simple.
Large-company controls can be enabled progressively.
Field-level security prevents salary/ID/document leakage.
Every material change is auditable and historically explainable.
```

---

# 768. Final Agent Rule

For every HR feature, the Agent must answer:

> **What employee fact does this represent, who owns it, what is the current value, how is history preserved, who is allowed to see/change it, and what downstream module consumes it?**

If those answers are unclear, implementation is incomplete.

---

# 769. Next Module

The next business module should be:

> **`18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**

Payroll should consume the deterministic HR foundation defined here and own:

```text
Salary Structure
Payroll Components
Compensation Assignment
Payroll Period/Run
Gross-to-Net Calculation
Country Statutory Rules
Attendance/Leave monetary treatment
Allowances/Deductions calculation
Payslip
Loans/Advances where approved
Finalization/Corrections
Accounting Facts
Payment/WPS outputs
Payroll History
```

HR must not be rewritten inside Payroll.

---

**END OF FILE — 17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
