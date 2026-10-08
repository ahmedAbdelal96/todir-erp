# 21 — TODIR ERP SaaS Expenses / Employee Reimbursements / Corporate Spend / Business Advances Requirements & Technical Design Pack

**File:** `21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + EXPENSE MANAGEMENT + EMPLOYEE REIMBURSEMENT + CORPORATE SPEND + BUSINESS ADVANCES + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Finance Manager, Expense Controller, HR Lead, Project Manager, Tax/Accounting Lead, Payments Lead, Backend/Frontend Leads, Security Engineer, Data Architect, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **HIGH — COMPANY SPEND + EMPLOYEE MONEY + TAX + ACCOUNTING + FRAUD/CONTROL RISK**

---

## Canonical dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `15_ERP_SAAS_PURCHASING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `17_ERP_SAAS_HR_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `18_ERP_SAAS_PAYROLL_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

Expenses must reuse Platform Files, Audit, Approvals, Notifications, Jobs, Settings, Custom Fields and Transaction Coordinator.

---

# 0. Executive Decision

TODIR Expenses owns the **business meaning, policy control, evidence, approval and financial handoff of non-procurement company spend**.

It must answer:

```text
What was spent?
Why was it spent?
Who incurred it?
Who actually paid?
Was it employee money, company money, card, petty cash or an advance?
What document proves it?
Which policy applied?
Was it within policy?
Who approved it?
What tax is recoverable?
Which business dimension owns the cost?
Does the company owe the employee?
Does the employee owe the company?
Has reimbursement/repayment actually happened?
What accounting fact was created?
```

The product goal is:

> **Every material company expense must be explainable from business purpose to evidence to policy to approval to accounting to money settlement—without Excel, duplicate entry or fake supplier/employee identities.**

---

# 1. Absolute Domain Distinctions

```text
Expense != Payment
Expense != Card Transaction
Expense != Supplier Invoice
Expense != Purchase Order
Expense != Payroll
Expense != Employee Reimbursement
Expense != Employee Repayment
Business Expense Advance != Salary Advance
Approved Expense != Finalized Expense
Finalized Expense != Reimbursed Expense
```

These distinctions are non-negotiable.

---

# 2. Why Expenses Exists

Without a proper Expenses module, real companies manage spend through:

```text
paper receipts
WhatsApp photos
Excel sheets
employee notebooks
cash-box notebooks
card statements
emails
manual accounting journals
salary deductions
untracked advances
```

That creates:

```text
duplicate claims
missing receipts
unclear ownership
slow reimbursement
tax leakage
fraud/control gaps
unreconciled card transactions
employee-company balances nobody can explain
```

---

# 3. Business Scope

TODIR Expenses covers:

```text
Employee-paid business expenses
Company-paid incidental expenses
Corporate card spend
Petty cash spend
Business expense advances
Advance settlement
Employee reimbursement
Employee repayment to company
Expense reports/grouping
Receipts/evidence
Policy evaluation
Approval
Mileage
Per diem
Itemization
Business meal participants
Multi-currency
Tax/VAT treatment
Department/branch/cost center/project dimensions
Accounting facts
Payment handoff
Card/bank matching seams
```

---

# 4. Expenses Is Not Procurement

Formal supplier procurement remains:

```text
Purchase Order
→ Supplier Invoice
→ Payable
```

in Purchasing/Invoicing/Subledger.

Expenses is for:

```text
employee-incurred
incidental
card
cash
travel
small operational spend
```

unless company policy explicitly routes something elsewhere.

---

# 5. Decision Rule — Expenses vs Purchasing

Use Expenses when the business fact is primarily:

```text
an employee/company spend event requiring evidence and policy review
```

Use Purchasing/Invoicing when the business fact is primarily:

```text
a supplier procurement/liability process with supplier invoice/AP lifecycle
```

---

# 6. Supplier Invoice Must Not Be Duplicated As Expense

If a formal supplier invoice already exists as AP:

```text
do not create a second Expense for the same liability
```

Expense may link to source for context if needed.

---

# 7. Expense Paid Personally

Canonical flow:

```text
Employee incurs business cost
→ creates Expense
→ evidence/policy
→ approval
→ financial finalization
→ company owes employee
→ reimbursement
→ Payment
```

---

# 8. Company-Paid Expense

Canonical flow:

```text
Company card/cash/bank paid
→ Expense created/matched
→ evidence/policy
→ approval/finalization
→ accounting
```

No employee reimbursement.

---

# 9. Business Advance Flow

Canonical:

```text
Company gives employee business advance
→ employee incurs expenses
→ expenses applied to advance
→ remaining balance calculated
→ employee returns excess
OR
→ company reimburses shortfall
→ advance settled
```

---

# 10. Personal Corporate Card Charge

Canonical:

```text
Company Card Transaction
→ identified as Personal
→ NOT a business Expense
→ employee owes company
→ Employee Repayment / settlement
```

---

# 11. Expense Is Canonical Spend Claim/Event

`Expense` means:

> A business-spend fact submitted for company recognition under an expense policy.

It is not proof that cash moved.

---

# 12. Expense Report

`ExpenseReport` is optional grouping/workflow envelope.

It does not replace individual Expense truth.

---

# 13. Small Company Default

```text
Create Expense
→ Approve
→ Finalize
→ Reimburse if needed
```

No mandatory report grouping.

---

# 14. Larger Company Flow

Optional:

```text
Create many Expenses
→ group by trip/period
→ Expense Report
→ manager approval
→ finance review
→ Finalize
→ batch reimbursement
```

---

# 15. One Module, One Pack

This file includes:

```text
Expenses
Expense Reports
Categories
Policies
Receipts
OCR
Duplicate Detection
Itemization
Participants
Mileage
Per Diem
Corporate Cards
Card Inbox
Petty Cash integration
Business Advances
Reimbursements
Employee Repayments
Tax
FX
Dimensions
Approval
Accounting
Payments
Reports
Migration
Security
Performance
Tests
Implementation Plan
```

Do not split these into separate design packs.

---

# 16. Source-of-Truth Ownership

```text
Employee / Employment              → HR
Expense / Expense Report            → Expenses
Expense Category / Policy           → Expenses
Business Advance                    → Expenses
Reimbursement obligation            → Expenses financial result
Employee repayment obligation       → Expenses/Subledger-compatible financial result
Actual money movement               → Payments
Company card/bank transaction       → Banking/Card provider capability
Tax calculation/recoverability      → Tax
Files/receipt bytes                 → Platform Files
Journal Entry                       → Ledger
Project                             → Projects when active
Supplier identity                   → Directory
```

---

# 17. Employee ≠ User

Locked from HR:

```text
Employee
!=
User Account
```

Expense can be entered by Finance/HR on behalf of employee.

Self-service requires linked User.

---

# 18. Expense Header

Concept:

```text
Expense
{
    Id
    TenantId
    LegalEntityId

    EmployeeId?
    EmploymentId?

    ExpenseDate
    MerchantName?
    MerchantPartyId?

    CategoryId
    BusinessPurpose

    PaymentSourceType
    PaymentSourceRef?

    TransactionCurrency
    GrossAmount
    TaxAmount?
    NetAmount?

    PolicyVersionId
    PolicyEvaluationId

    ExpenseState
    ApprovalState
    AccountingState
    SettlementState

    ExpenseReportId?
    BusinessAdvanceId?

    CreatedBy
    CreatedAt
    RowVersion
}
```

---

# 19. Payment Source Type

Examples:

```text
EmployeePersonalFunds
CorporateCard
CompanyCash
PettyCash
CompanyBank/Direct
BusinessAdvance
OtherConfiguredCompanySource
```

---

# 20. Payment Source Changes Meaning

Same receipt can create different financial outcome based on who paid.

Example:

```text
EmployeePersonalFunds
→ company owes employee

CorporateCard
→ no employee reimbursement

BusinessAdvance
→ reduces employee advance accountability
```

---

# 21. Payment Source Is Not Payment Truth

`PaymentSourceType = CorporateCard` means business context.

The actual card/bank transaction remains external Payments/Banking/Card truth.

---

# 22. Expense State Dimensions

Do not use one overloaded status.

Separate:

```text
ExpenseState
ApprovalState
AccountingState
SettlementState
EvidenceState
PolicyState
```

---

# 23. ExpenseState

Recommended:

```text
Draft
Submitted
Finalized
Corrected
CancelledBeforeFinalization
```

---

# 24. ApprovalState

```text
NotRequired
Pending
Approved
Rejected
```

---

# 25. AccountingState

```text
NotPosted
Posted
PostingFailed
Reversed/Adjusted
```

---

# 26. SettlementState

Depending source:

```text
NotApplicable
PendingReimbursement
PartiallyReimbursed
Reimbursed
PendingEmployeeRepayment
PartiallyRepaid
Repaid
AppliedToAdvance
AdvanceSettlementPending
```

This may be projected from dedicated settlement records rather than a single enum.

---

# 27. EvidenceState

```text
NotRequired
Missing
Attached
Verified
ExceptionAccepted
```

---

# 28. PolicyState

```text
Compliant
Warning
NeedsReason
NeedsApproval
Blocked
```

A single Expense may have several policy findings.

---

# 29. Draft

Editable.

No canonical accounting/reimbursement effect.

---

# 30. Submitted

Employee/submitter says data is ready for review.

Still not accounting truth.

---

# 31. Approved

Approval alone does not mean accounting/payment happened.

---

# 32. Finalized

Financially accepted Expense snapshot is immutable.

Corrections use reversal/corrective records.

---

# 33. No Silent Edit After Finalization

Absolute.

Do not:

```text
change amount
change tax
change category
change employee
change payment source
change currency
```

on finalized Expense.

---

# 34. Correction

Use:

```text
ReverseExpense
CorrectExpense
```

or linked correction workflow.

Original remains traceable.

---

# 35. Expense Category

Expense Category is a first-class Expenses concept.

Do not model categories as Catalog Products.

---

# 36. Category Examples

```text
Travel
Hotel
Meals
Transportation
Fuel
Office Supplies
Internet
Phone
Maintenance
Marketing
Subscriptions
Professional Fees
Parking
Courier
Mileage
Per Diem
Other
```

Configurable.

---

# 37. Expense Category Model

Concept:

```text
ExpenseCategory
{
    Id
    LegalEntityId?
    Code
    NameAr
    NameEn?

    ExpenseKind
    PolicyProfileId?
    TaxProfileRef?
    AccountingRole
    ReimbursableDefault

    DetailRequirement
    EvidencePolicyRef?

    Status
    Version
}
```

---

# 38. Category Is Not GL Account

Expenses stores semantic `AccountingRole`.

Ledger determines Account.

---

# 39. Detail Requirement

Per Category:

```text
None
Itemization
Participants
Mileage
PerDiem
CustomTypedDetail
```

---

# 40. Category Versioning

Material policy/accounting/tax classification changes create version/effective dating.

Past finalized Expenses retain old semantics.

---

# 41. Subcategory

Optional for itemization/reporting:

```text
Hotel
├ Room
├ Breakfast
├ Laundry
├ Parking
└ Other
```

---

# 42. Itemization

One Expense can be broken into economically/policy-distinct lines.

---

# 43. Itemization Invariant

```text
SUM(Itemization Gross Amount)
=
Expense Gross Amount
```

within exact rounding policy.

---

# 44. Itemization Tax

Each itemization can have:

```text
Category/Subcategory
Tax treatment
Business purpose extension
```

if receipt requires.

---

# 45. Hotel Example

```text
Hotel Folio = 5,500

Room        4,000
Breakfast     500
Laundry       300
Tax           700
```

depending tax representation.

---

# 46. Itemization Does Not Create Multiple Payments

Still one source receipt/card transaction if applicable.

---

# 47. Business Meal Participants

Category may require Participants.

---

# 48. Participant Types

```text
Employee
Customer/PartyContact
Supplier Contact
External Guest
Other
```

---

# 49. Participant Privacy

Do not collect unnecessary sensitive details.

Usually:

```text
name
organization/Party link
role/type
```

sufficient.

---

# 50. Participant Requirement

Policy can depend on amount/category.

---

# 51. Business Purpose

Required for categories/company policies where meaningful.

Avoid free-text-only classification.

---

# 52. Merchant

Merchant can be:

```text
free text
Directory Party reference
card-feed merchant
```

Do not force vendor master creation.

---

# 53. Merchant Normalization

Card/OCR can suggest canonical merchant name.

Do not auto-create Supplier Party.

---

# 54. Evidence / Receipt

Receipts/evidence are first-class.

Bytes remain Platform Files-owned.

---

# 55. Expense Evidence

Concept:

```text
ExpenseEvidence
{
    Id
    ExpenseId
    EvidenceType
    FileRef
    Source
    UploadedBy
    UploadedAt
    VerificationState
    FileHash
}
```

---

# 56. Evidence Types

```text
Receipt
InvoiceImage
TaxInvoice
BookingConfirmation
MissingReceiptDeclaration
SupportingDocument
Other
```

---

# 57. Receipt Policy

Per category/policy:

```text
Required
Optional
RequiredAboveAmount
NotRequired
MissingReceiptDeclarationAllowed
```

---

# 58. Missing Receipt

Do not silently mark compliant.

Possible:

```text
Reason required
Employee declaration
Additional approval
Block
```

according to policy.

---

# 59. Missing Receipt Declaration

Structured record:

```text
ExpenseId
Reason
DeclarationTextVersion
EmployeeConfirmation
ConfirmedAt
ApprovalRef?
```

Not a blank note.

---

# 60. Evidence File Security

Use secure Platform Files references.

No public permanent URL.

---

# 61. Evidence Versioning

Replacing a receipt does not erase prior evidence after submission/finalization.

---

# 62. OCR

OCR/AI can propose:

```text
Merchant
Date
Amount
Currency
Tax
Invoice Number
Category
```

---

# 63. OCR Is Suggestion

Human/user confirmation or high-confidence policy-approved automation before authoritative fields.

---

# 64. OCR Failure

Expense creation must not block waiting for OCR.

OCR can run async.

---

# 65. Duplicate Expense Detection

Signals:

```text
Employee
Merchant
Date
Amount
Currency
Receipt/invoice number
File hash
Card transaction ref
```

---

# 66. Exact Duplicate

Stable external/card source ID:

```text
idempotent
```

---

# 67. Probable Duplicate

Warn/review.

Do not auto-delete/merge.

---

# 68. Duplicate Evidence Hash

Same receipt file on two Expenses is strong signal, not always proof.

---

# 69. Fraud/Anomaly Seam

Future rules/AI can flag:

```text
duplicate receipts
round-number anomalies
weekend spend
unusual merchant
split transactions around limits
repeated missing receipts
```

Human decision remains.

---

# 70. Policy Engine Mission

Policy tells:

```text
Is this expense allowed?
What evidence is required?
What limits apply?
What approval is needed?
What explanation is required?
```

---

# 71. Policy Is Versioned

Finalized Expense stores exact policy version/evaluation.

Never reevaluate old Expense using today's policy silently.

---

# 72. Expense Policy

Concept:

```text
ExpensePolicy
{
    Id
    LegalEntityId
    Name
    EffectiveFrom
    EffectiveTo?
    Version
    Status
}
```

---

# 73. Typed Policy Conditions

Allowed conditions may include:

```text
Category
Amount
Currency
Employee/Employment Type
Department
Position/Grade later
Branch
Legal Entity
Country
Travel Location
Payment Source
Merchant
Project
Receipt Available
Day/Time
Corporate Card Available?
```

---

# 74. No Arbitrary Policy DSL

Use constrained typed rules.

No tenant arbitrary:

```text
JavaScript
SQL
C#
Python
```

---

# 75. Policy Outcomes

```text
Allow
Warning
RequireReason
RequireReceipt
RequireApproval
RequireAdditionalApproval
Block
```

Multiple findings possible.

---

# 76. Policy Finding

Concept:

```text
ExpensePolicyFinding
{
    ExpenseId
    RuleCode
    Severity
    Outcome
    Explanation
    Limit?
    Actual?
    PolicyVersion
}
```

---

# 77. Limit Types

Examples:

```text
PerExpense
PerDay
PerNight
PerTrip
PerMonth
PerYear
PerEmployee
PerCategory
```

---

# 78. Limit Currency

Policy must define currency/FX treatment.

No comparing AED spend directly against EGP limit.

---

# 79. Limit FX

Use FinBase approved policy rate/snapshot.

---

# 80. Policy Exception

Authorized user can approve exception with:

```text
finding
reason
actor
approval
date
```

Never mutate rule.


# 81. Policy Exception Is Not Policy Change

If a manager approves one 4,500 hotel stay above a 3,000 limit:

```text
Expense exception approved
```

not:

```text
Hotel policy changed to 4,500
```

---

# 82. Policy Reason

When a rule is violated and policy permits exception:

```text
reason is mandatory
```

for configured findings.

---

# 83. Policy Snapshot

Finalized Expense preserves:

```text
PolicyVersion
Rule IDs
Actual values
Limit values
Outcome
Exception reason
Approver
```

---

# 84. Policy Evaluation Timing

Evaluate at:

```text
draft preview
submission
approval/review
financial finalization
```

but finalization uses accepted version/snapshot rules.

---

# 85. Policy Changes During Review

If policy version changes after submit:

company policy decides:

```text
Keep submission policy version
or
Re-evaluate before finalization
```

Default recommended:

```text
submitted expense preserves its evaluated policy version
```

unless law/company mandates re-evaluation.

---

# 86. Expense Approval Mission

Approval confirms business legitimacy and policy exception authority.

It does not mean cash was reimbursed.

---

# 87. Approval Engine

Reuse Platform Approvals.

Do not create a second generic approval engine.

---

# 88. Small Company Approval

Default:

```text
Employee submits
→ Manager/Owner approves
```

or authorized owner:

```text
Create + Approve
```

---

# 89. Large Company Approval

Optional:

```text
Employee
→ Manager
→ Cost Center Owner
→ Finance
```

based on policy.

---

# 90. Approval Conditions

Typed:

```text
Amount
Policy violation
Category
Department
Project
Employee grade later
Payment source
Missing receipt
Personal card/corporate card
```

---

# 91. No Approval Workflow By Default For Every Expense

Normal compliant low-value spend should be simple.

---

# 92. Partial Approval

For Expense Report grouping:

individual Expenses can be:

```text
Approved
Held
Rejected
```

without forcing all valid expenses to wait, if company policy enables.

---

# 93. Expense Report

Concept:

```text
ExpenseReport
{
    Id
    LegalEntityId
    EmployeeId?
    Title
    Purpose?
    TripRef?
    DateFrom?
    DateTo?
    State
    ApprovalState
    SubmittedAt?
    FinalizedAt?
}
```

---

# 94. Report Totals

Derived from contained Expenses.

No separate manually editable total.

---

# 95. Report Does Not Own Monetary Truth

Each Expense remains canonical for:

```text
amount
tax
category
payment source
evidence
policy result
```

---

# 96. Expense Can Exist Without Report

Important for simple companies and card transactions.

---

# 97. Expense Can Join Report Before Submission

Optional grouping.

---

# 98. Finalized Expense Report

Cannot silently remove/add already-finalized Expense.

Use correction/new report as appropriate.

---

# 99. Report Purpose

Examples:

```text
October expenses
Dubai sales trip
Project Alpha
Client workshop
```

---

# 100. Employee-Paid Expense

Payment source:

```text
EmployeePersonalFunds
```

means the employee personally funded a valid business cost.

---

# 101. Employee-Paid Financial Outcome

After finalization:

```text
Company owes Employee
```

unless offset by an outstanding Business Advance allocated to same Expense.

---

# 102. Reimbursement Obligation

Concept:

```text
ExpenseReimbursementObligation
{
    Id
    EmployeeId
    LegalEntityId
    ExpenseId
    EligibleAmount
    Currency
    ReimbursedAmount
    OutstandingAmountProjection
    Status
}
```

Exact accounting ownership may use Subledger-compatible open-item contract; avoid duplicating generic party AR/AP if a canonical financial obligation service is selected.

---

# 103. Employee Remains Employee

Do not create fake Supplier just to reimburse employee.

---

# 104. Reimbursement Amount

May be:

```text
Expense Gross
minus non-reimbursable personal portion
minus advance-applied amount
minus company-paid portion
```

according to exact source/itemization.

---

# 105. Partial Reimbursement

Supported.

Example:

```text
Eligible = 3,000
Paid     = 2,000
Outstanding = 1,000
```

---

# 106. Reimbursement Is Not Expense Edit

Payments settle an obligation.

Expense remains.

---

# 107. Reimbursement Batch

Multiple approved obligations can be paid together.

---

# 108. Reimbursement Batch Concept

```text
ExpenseReimbursementBatch
{
    Id
    LegalEntityId
    Currency
    PaymentMethod
    PlannedPaymentDate?
    State
}
```

with child allocations.

---

# 109. Batch Allocation

```text
Batch
├ Employee A  3 Expenses  2,500
├ Employee B  1 Expense     800
└ Employee C  5 Expenses  4,200
```

Actual Payments remain Payments-owned.

---

# 110. One Payment Per Employee vs Combined Company Batch

Implementation can request:

```text
one Payment per employee
```

inside one operational batch, or provider batch capability.

Never lose per-employee obligation allocation.

---

# 111. Reimbursement Payment Request

Expenses sends:

```text
Employee
Destination/payment profile
Amount
Currency
Source obligations
Idempotency key
```

to Payments.

---

# 112. Payment Destination

Canonical payout/bank destination should come from approved Payments/HR payment-profile boundary.

Do not store raw bank details casually in Expenses.

---

# 113. Reimbursement Payment States

Projection:

```text
Prepared
Pending
Completed
Failed
PartiallyCompleted
```

Payments authority.

---

# 114. Failed Reimbursement

Expense remains finalized.

Obligation remains outstanding.

Retry must be idempotent.

---

# 115. Employee Leaves Company

Approved outstanding reimbursement does not disappear on termination.

Former employee settlement remains payable according to company/legal policy.

---

# 116. Reimbursement Through Payroll

Optional future/market/company route:

```text
Approved Reimbursement
→ Payroll one-time non-wage settlement input
```

only if Payroll design explicitly supports it.

Expense does not become Salary/Earning.

---

# 117. Payroll Reimbursement Guardrail

Payroll route must preserve:

```text
ExpenseId
non-taxable/reimbursement classification as applicable
no salary-base pollution
settlement status back to Expenses
```

Country Provider determines tax treatment where required.

---

# 118. Direct Reimbursement Default

Recommended core path:

```text
Expenses
→ Payments
```

Simpler and semantically clean.

---

# 119. Company-Paid Expense

Payment source can be:

```text
CorporateCard
CompanyCash
PettyCash
CompanyBank/Direct
```

No employee reimbursement.

---

# 120. Company-Paid Expense Employee Context

Employee may still be responsible for:

```text
business purpose
receipt
category
participants
project
```

without being creditor.

---

# 121. Corporate Card Transaction Is Separate Fact

A company-card transaction comes from card/bank feed or Payments/Banking connector.

It is not automatically a finalized Expense.

---

# 122. Corporate Card Transaction Reference

Expenses stores link/projection:

```text
External/Card Transaction ID
Cardholder Employee
Merchant
Date
Amount
Currency
Card last4/token ref
Feed status
```

Canonical bank/card transaction remains owning provider/banking capability.

---

# 123. Corporate Card Inbox

Finance/employee can review:

```text
Matched
Needs Receipt
Needs Category
Needs Business Purpose
Possible Duplicate
Personal
Disputed
Credit/Refund
Unmatched
```

---

# 124. Card Transaction Matching

A card transaction can:

```text
Match existing Expense
Create draft Expense
Be marked Personal
Be marked Duplicate feed event
Be a Credit/Refund
Remain Unmatched
```

---

# 125. Auto-Create Draft

Allowed when card transaction is trustworthy.

Creates:

```text
Draft Expense
```

not Finalized Expense.

---

# 126. Auto-Fill

Card feed can prefill:

```text
Employee
Merchant
Date
Currency
Amount
PaymentSource=CorporateCard
```

User adds missing business context.

---

# 127. Auto-Match Receipt

Signals:

```text
Amount
Currency
Merchant
Date proximity
Cardholder
receipt invoice number
```

Confidence-based.

---

# 128. Exact Match

System can attach receipt suggestion automatically with transparent provenance where confidence/rules allow.

---

# 129. Ambiguous Match

Ask user.

Never silently attach receipt to wrong card transaction.

---

# 130. One Card Transaction / Multiple Expense Itemizations

Common:

```text
hotel card transaction
→ one Expense
→ multiple itemizations
```

---

# 131. Multiple Card Transactions / One Business Context

Expense Report may group them.

Do not merge actual payment transactions.

---

# 132. Card Tip / Final Amount Difference

Provider may present:

```text
authorization 500
cleared 550
```

Expense final amount should match cleared transaction or controlled split/explanation.

---

# 133. Pending Card Transaction

Do not financially finalize as exact company-paid Expense if amount can materially change, unless policy allows provisional review.

---

# 134. Cleared Card Transaction

Preferred authoritative card amount for matching.

---

# 135. Corporate Card Credit

A merchant credit/refund is not negative new Expense by default.

It should link to:

```text
original expense/transaction if known
```

and create corrective accounting/settlement handling.

---

# 136. Card Refund Matching

```text
Original Card Expense
→ Card Credit
→ corrective Expense/accounting link
```

No silent netting that hides source.

---

# 137. Personal Card Charge On Company Card

State:

```text
Personal
```

does not create business Expense.

---

# 138. Personal Charge Creates Employee Repayment Obligation

Company paid employee's personal amount.

Therefore:

```text
Employee owes Company
```

---

# 139. Employee Repayment Obligation

Concept:

```text
EmployeeRepaymentObligation
{
    Id
    EmployeeId
    LegalEntityId
    SourceType
    SourceRef
    Amount
    Currency
    RepaidAmount
    OutstandingAmountProjection
    Status
}
```

---

# 140. Repayment Sources

Examples:

```text
Personal Corporate Card Charge
Over-Reimbursement Correction
Unreturned Business Advance
Other Approved Employee Receivable
```

---

# 141. Employee Repayment Is Not Payroll Deduction By Default

Default settlement:

```text
Payments / bank/cash repayment
```

Payroll deduction only if company/legal policy and Payroll provider support.

---

# 142. Repayment Through Payroll

Optional typed recovery input.

Must comply with deduction limits/country rules.

---

# 143. Partial Employee Repayment

Supported.

---

# 144. Repayment Payment Truth

Payments owns money moving employee→company where supported.

If handled externally, authorized user can record external settlement reference through typed command, not fake Payment success.

---

# 145. Repayment Cannot Exceed Outstanding

Invariant.

---

# 146. Employee Repayment History

Employee/authorized Finance can see:

```text
Original amount
Reason
Repaid
Outstanding
Transactions
```

---

# 147. Card Transaction Reconciliation

Finance view reconciles:

```text
Card feed transaction
↔ Expense / Personal classification
↔ Receipt/evidence
↔ Accounting
```

---

# 148. Card Feed Completeness

System can show:

```text
Total card transactions
Matched to business Expense
Personal
Credits
Unmatched
```

---

# 149. Card Transaction Must Be Classified Once

One cleared company-card transaction cannot be fully matched to two independent Expenses unless explicit split/itemization design ensures allocations total exactly transaction amount.

---

# 150. Card Transaction Allocation

For one transaction split into several Expense records/categories:

```text
SUM(allocations) = cleared card amount
```

---

# 151. Card Split Allocation

Useful for:

```text
one store purchase covering office + project supplies
```

Can implement as one Expense with itemizations first; multiple Expenses only if workflow demands.

---

# 152. Company Card Assignment

Card-to-Employee mapping comes from Banking/Card/Payments capability.

Expenses consumes.

---

# 153. Unassigned Card

Transactions route to Finance/Card Inbox.

Do not guess employee by merchant.

---

# 154. Corporate Card Policy

Can require:

```text
use company card when available
```

and flag employee personal payment as policy exception.

---

# 155. Card Missing Receipt SLA

Optional reminders:

```text
1 day
3 days
7 days
```

configurable.

---

# 156. Petty Cash

Expenses can classify spend paid by Petty Cash.

Actual cash movement remains Payments/Cashbox.

---

# 157. Petty Cash Expense Flow

```text
Cashbox Payment
→ Expense evidence/category
→ accounting match
```

or:

```text
Expense
→ approved cashbox Payment
```

depending workflow.

---

# 158. Petty Cash Is Not Business Advance

Petty Cash belongs company cash custody.

Business Advance transfers accountability to Employee.

---

# 159. Petty Cash Custodian

May be Employee/User responsible for cashbox.

Payments/Cashbox owns balance.

Expenses owns spend justification.

---

# 160. Petty Cash Replenishment

Not Expense itself.

It's movement of company money into Cashbox.

Expenses link provides evidence for consumed petty cash.



# 161. Business Expense Advance Mission

A Business Expense Advance exists because the company gives an employee money before the employee incurs business costs.

It is not salary and not payroll advance.

---

# 162. Advance Business Lifecycle

```text
Requested
→ Approved
→ Issuance Requested
→ Issued
→ Partially Applied
→ Settlement Pending
→ Settled
```

with cancellation/rejection states where needed.

---

# 163. Advance Request

Concept:

```text
BusinessAdvance
{
    Id
    TenantId
    LegalEntityId
    EmployeeId
    EmploymentId?

    Purpose
    TripRef?
    RequestedAmount
    Currency

    ApprovedAmount?
    IssuedAmount?
    AppliedExpenseAmount
    ReturnedAmount
    ReimbursedShortfallAmount

    RequestedDate
    NeededByDate?
    SettlementDueDate?

    State
    ApprovalState
    PaymentState

    RowVersion
}
```

---

# 164. Advance Purpose

Examples:

```text
Business Trip
Site Visit
Purchasing/Field Errand
Event
Emergency Operation
Other Approved Purpose
```

---

# 165. Advance Request Is Not Money Movement

Approved:

```text
Advance approved
```

does not mean:

```text
employee received money
```

---

# 166. Advance Issuance

Actual issuance through Payments.

---

# 167. Advance Issuance Payment

Payments source:

```text
BusinessAdvanceId
```

Stable idempotency.

---

# 168. Issued Amount

Derived/confirmed from actual Payments result.

Do not set merely because request was approved.

---

# 169. Partial Advance Issuance

Supported if business allows.

Example:

```text
Approved 10,000
Paid 6,000
Outstanding issuance 4,000
```

---

# 170. Failed Advance Payment

Advance remains approved/pending issuance.

No fake employee accountability until money actually moves.

---

# 171. Advance Accountability

Once issued:

```text
Employee accountable for issued amount
```

until applied to valid business Expenses or returned.

---

# 172. Expense Applied To Advance

Eligible Employee Expense can use:

```text
PaymentSource = BusinessAdvance
```

or allocation relation.

---

# 173. Advance Application

Concept:

```text
BusinessAdvanceApplication
{
    AdvanceId
    ExpenseId
    Amount
    Currency
    AppliedAt
    State
}
```

---

# 174. Application Invariant

```text
SUM(finalized applied expense amounts)
<=
issued advance amount + explicitly allowed cross-currency valuation semantics
```

No double application.

---

# 175. Advance Currency Same As Expense

Straightforward.

---

# 176. Advance Currency Different From Expense

Must use explicit FX policy.

Example:

```text
Advance: USD 1,000
Expense: AED 500
```

Need:

```text
original expense amount/currency
advance settlement equivalent
FX rate snapshot
FX difference if any
```

---

# 177. Advance Settlement FX

FinBase owns rate framework.

Any realized accounting difference becomes semantic fact.

No silent conversion.

---

# 178. Advance Available Balance

Rebuildable:

```text
Issued
- Applied finalized expenses
- Returned cash
- Other authorized settlement
= Outstanding Accountable Amount
```

---

# 179. Advance Balance Is Not Mutable Field

Projection may cache balance.

Canonical truth is issuance + applications + returns + settlements.

---

# 180. Advance Over-Spend

Example:

```text
Advance = 5,000
Expenses = 5,500
```

Result:

```text
Company owes employee 500
```

if all expenses approved/reimbursable.

---

# 181. Advance Under-Spend

Example:

```text
Advance = 5,000
Expenses = 4,300
```

Result:

```text
Employee owes company 700
```

until returned/settled.

---

# 182. Advance Exact Spend

```text
Advance = Expenses
```

settles with zero employee/company balance.

---

# 183. Advance Return

Employee returns unused amount via Payments/Cashbox/bank.

---

# 184. Return Is Actual Money Movement

Do not mark returned from a note.

Use Payments or authorized external settlement record.

---

# 185. Advance Return Before Expense Report Finalized

Possible.

Outstanding calculations account for both.

---

# 186. Multiple Expense Reports Against One Advance

Configurable.

Default can allow until settlement due date.

---

# 187. Multiple Advances Against One Expense

Avoid by default.

If truly needed, allocation design supports several advances but increases complexity.

Recommended:

```text
one primary advance per expense/report
```

unless evidence requires multi-allocation.

---

# 188. Advance Settlement Deadline

Company policy can require settlement:

```text
N days after trip
N days after issue
N days after return
```

---

# 189. Overdue Advance

Exception:

```text
Outstanding
Past SettlementDueDate
```

---

# 190. New Advance With Old Outstanding

Policy:

```text
Allow
Warn
RequireApproval
Block
```

---

# 191. Termination With Outstanding Advance

Offboarding/Finance must see:

```text
employee owes company
```

before final settlement.

Payroll deduction only if lawful/configured.

---

# 192. Advance Write-Off

High-risk explicit command.

Requires:

```text
reason
approval
accounting fact
audit
```

---

# 193. Advance Cancellation

Only unissued portion can cancel normally.

Issued money requires settlement, not cancellation.

---

# 194. Advance Timeline

Employee/Finance can see:

```text
Requested
Approved
Issued
Expenses applied
Cash returned
Shortfall reimbursed
Settled
```

---

# 195. Advance Reporting

```text
Active advances
Outstanding by employee
Overdue advances
Advance utilization
Returned unused cash
Advance shortfalls
```

---

# 196. Mileage Mission

Mileage is a typed reimbursable expense, not a manual amount description.

---

# 197. Mileage Detail

Concept:

```text
MileageExpenseDetail
{
    ExpenseId
    StartLocationText
    EndLocationText
    Distance
    DistanceUom
    RoundTrip
    VehicleClass?
    RatePolicyVersion
    Rate
    CalculatedAmount
}
```

---

# 198. Mileage Rate

Effective/versioned by:

```text
Legal Entity
Country
Vehicle Type?
Employee class?
Date
```

according to policy.

---

# 199. Mileage Calculation

```text
Eligible Distance × Effective Rate
```

with FinBase rounding.

---

# 200. Mileage Original Fact

Preserve:

```text
distance
UOM
rate version
```

not only final money.

---

# 201. Kilometers vs Miles

Explicit UOM.

No silent assumption.

---

# 202. Round Trip

System can derive:

```text
TotalDistance = OneWayDistance × 2
```

when selected.

---

# 203. GPS Route

Future optional assistance.

Do not require location tracking.

---

# 204. Mileage Evidence

Policy may require:

```text
route purpose
meeting/site reference
odometer proof
map route
```

but not universal.

---

# 205. Mileage Duplicate Detection

Potential duplicate by:

```text
Employee
Date
Route
Distance
Purpose
```

warn.

---

# 206. Mileage vs Company Vehicle

Policy can disallow reimbursement when company vehicle/fuel is provided.

---

# 207. Mileage vs Fuel Receipt

Company policy decides whether both can be claimed.

Flag potential double compensation.

---

# 208. Per Diem Mission

Per Diem provides fixed daily travel allowance without individual receipts for covered costs.

---

# 209. Per Diem Is Not Generic Fixed Expense

It has:

```text
travel dates/times
location
rate version
meal reductions
eligibility
```

---

# 210. Per Diem Detail

Concept:

```text
PerDiemExpenseDetail
{
    ExpenseId
    TravelLocationCode
    StartAt
    EndAt
    PolicyVersion
    Currency
    DailyLines[]
    TotalAmount
}
```

---

# 211. Per Diem Rate

Effective/versioned by:

```text
Country/City
Legal Entity
Employee group/grade if used
Trip type
Effective Date
```

---

# 212. Per Diem Daily Line

```text
Date
BaseRate
PartialDayFactor
BreakfastProvided?
LunchProvided?
DinnerProvided?
ReductionAmount
FinalDailyAmount
```

---

# 213. Provided Meal Reduction

Policy-defined.

No global percentages hardcoded.

---

# 214. Partial Travel Day

Policy/provider defines.

Could depend on:

```text
departure time
return time
hours away
```

---

# 215. Per Diem Currency

Original rate currency preserved.

FX through FinBase if reimbursement/company base differs.

---

# 216. Per Diem Receipt

Usually not required for covered allowance itself.

Supporting travel proof may be required.

Policy decides.

---

# 217. Per Diem + Actual Meal Expense

Policy may:

```text
disallow
reduce per diem
allow special exception
```

prevent double reimbursement.

---

# 218. Per Diem + Hotel

Hotel may be separate actual expense if per diem only covers meals/incidental.

Policy defines coverage.

---

# 219. Travel Seam

TODIR Expenses can group:

```text
Trip
Dates
Destination
Purpose
Expenses
Advance
Per Diem
```

without becoming full travel booking system.

---

# 220. Business Trip Reference

Concept optional:

```text
ExpenseTrip
{
    Id
    EmployeeId
    Purpose
    Destination
    StartAt
    EndAt
    ProjectId?
    AdvanceId?
}
```

---

# 221. Travel Request Boundary

Full pre-trip approval/booking can be future Travel capability.

Expenses can accept TripRef.

---

# 222. Booking Integration Boundary

Flights/hotels from travel provider may become:

```text
company-paid transaction
or
expense evidence
```

but provider is separate.

---

# 223. Multi-Currency Mission

Preserve what was actually spent and how it was valued.

---

# 224. Expense Currency

Every Expense has original transaction currency.

---

# 225. Reimbursement Currency

May differ from Expense currency.

Example:

```text
Expense = AED 500
Employee reimbursed = EGP equivalent
```

---

# 226. Base Currency

Legal Entity base currency from Organization.

---

# 227. FX Snapshot

Use FinBase:

```text
source currency
target currency
rate
rate date
purpose
provider/manual source
version
```

---

# 228. FX Purposes

Possible:

```text
ExpenseRecognition
EmployeeReimbursement
AdvanceSettlement
CardActual
```

Do not assume same rate for all.

---

# 229. Employee Card Actual FX

If personal card charged:

```text
original merchant 500 AED
employee statement 6,900 EGP
```

company policy can reimburse:

```text
actual card amount
or
company FX rate
```

versioned.

---

# 230. Corporate Card FX

Card/Bank feed may provide settlement currency/amount.

Expense retains merchant transaction currency and company-card settlement reference.

---

# 231. FX Difference

Accounting handled semantically.

Expenses should not hide difference by overwriting original amount.

---

# 232. Multi-Currency Advance

Preserve issuance currency and expense currencies.

Settlement uses explicit valuation.

---

# 233. Tax Mission

Tax Engine determines:

```text
tax amount
tax classification
recoverability
```

from expense context.

---

# 234. Expense Tax Context

Includes:

```text
Legal Entity
Country/Jurisdiction
Merchant/supplier tax context if known
Expense category tax profile
Original amounts
Document type
Tax invoice evidence
Date
```

---

# 235. Tax Amount Capture

OCR/user can input tax amount.

Tax Engine validates/calculates according to mode.

---

# 236. Recoverable VAT

Can be:

```text
FullyRecoverable
PartiallyRecoverable
NonRecoverable
NotApplicable
NeedsValidTaxInvoice
```

provider/rules determine.

---

# 237. Recoverability Is Not Employee Reimbursement

Employee may be reimbursed gross amount even if company cannot recover VAT.

Separate.

---

# 238. Missing Tax Invoice

Policy/Tax can mark:

```text
VAT not recoverable
```

without rejecting whole Expense, depending law/company.

---

# 239. Tax Snapshot

Finalized Expense stores exact TaxResult snapshot/version.

---

# 240. Tax Does Not Use Expense Category Name Parsing

Typed tax profile/classification.

---

# 241. Withholding

If certain professional-fee expense needs withholding, Tax/Country Provider handles.

Do not invent generic deduction in Expenses.

---

# 242. Reverse Charge / Special Tax

Tax Engine handles if applicable.

---

# 243. Dimensions Mission

Allocate expense to where business cost belongs.

---

# 244. Core Dimensions

Potential:

```text
Legal Entity
Branch
Department
Cost Center
Project
Customer/Party for rebill later
Campaign later
```

---

# 245. Dimension Defaults

Infer from HR Employment:

```text
Branch
Department
default cost center
```

where safe.

---

# 246. Never Ask What TODIR Knows

If employee's Branch/Department are authoritative and policy uses them:

prefill.

User can override only when business allows.

---

# 247. Dimension Override

Requires:

```text
allowed value
permission/policy
reason if required
```

---

# 248. Project Expense

If Projects active:

```text
ProjectId
Task? later
```

references Project.

Expenses does not own project master.

---

# 249. Project Billing/Rebill Seam

Future:

```text
Expense marked Billable
→ Project/Customer billing
```

but Invoicing owns customer invoice.

---

# 250. Cost Allocation Across Dimensions

One Expense can optionally split by:

```text
Cost Center
Project
Department
```

---

# 251. Allocation Invariant

```text
SUM(allocation amounts)
=
eligible Expense amount
```

---

# 252. Allocation Snapshot

Finalized allocation immutable.

---

# 253. Expense Owner vs Cost Owner

Employee who spent is not necessarily department/project bearing cost.

---

# 254. Approval Routing Can Follow Cost Owner

Optional.

---

# 255. Expense Report Cross-Entity

Do not allow one report to mix Legal Entities by default.

---

# 256. Employee Working Across Entities

Each Expense belongs exact Legal Entity.

Cross-entity reimbursement requires explicit intercompany design later.

---

# 257. Personal Portion

An Expense/Card transaction may include personal amount.

---

# 258. Personal Portion Itemization

Example:

```text
Hotel total 5,500
Business 5,000
Personal minibar 500
```

Result:

```text
Business Expense = 5,000
Personal Employee Repayment = 500
```

if company card paid all.

---

# 259. Employee-Paid Personal Portion

Simply not reimbursable.

Do not create employee repayment because company did not pay it.

---

# 260. Non-Reimbursable Business-Like Spend

Policy can say:

```text
valid evidence but employee bears amount
```

if lawful/company policy.

Must be explicit, not silently cut total.



# 261. Accounting Mission

Expenses emits semantic financial facts.

Ledger decides accounts and Journal Entries.

---

# 262. Employee-Paid Expense Accounting

Economic meaning typically:

```text
Expense/Asset/Tax
+
Employee reimbursement liability
```

Ledger resolves actual accounts.

---

# 263. Company Card Expense Accounting

Economic meaning may include:

```text
Expense/Asset/Tax
+
Corporate card payable/clearing
```

depending card/accounting model.

---

# 264. Petty Cash Expense Accounting

Economic meaning:

```text
Expense/Tax
+
Cash reduction/clearing
```

through Payments/Ledger contracts.

---

# 265. Business Advance Accounting

Issuance:

```text
Employee advance receivable/accountability
→ Cash/Bank
```

Expense application:

```text
Expense
→ reduce advance accountability
```

Return:

```text
Cash/Bank
→ reduce advance accountability
```

Shortfall reimbursement:

```text
Expense liability
→ employee reimbursement/payment
```

Ledger owns exact accounts.

---

# 266. Personal Card Charge Accounting

Company-paid personal transaction creates:

```text
Employee receivable
```

not company Expense.

---

# 267. Employee Repayment Accounting

Repayment:

```text
Cash/Bank
→ Employee receivable
```

semantic fact.

---

# 268. Tax Accounting

Recoverable/non-recoverable tax facts passed explicitly.

---

# 269. Accounting Fact Catalog

Potential semantic facts:

```text
EmployeeExpenseFinalized
CompanyPaidExpenseFinalized
CorporateCardExpenseFinalized
BusinessAdvanceIssued
BusinessAdvanceApplied
BusinessAdvanceReturned
ExpenseReimbursementRecognized
EmployeeRepaymentRecognized
ExpenseCorrected
ExpenseReversed
```

Exact names align Ledger catalog.

---

# 270. No GL IDs In Expenses

Forbidden:

```text
DebitAccountId
CreditAccountId
```

inside Expense command.

Use semantic roles/dimensions.

---

# 271. Accounting Finalization

Where accounting is required immediately, use Transaction Coordinator with Ledger.

Do not finalize financially if required posting fails.

---

# 272. Approval vs Accounting

Approval:

```text
business authorization
```

Finalization:

```text
financial recognition
```

Keep separate.

---

# 273. Draft Expense Is Not Ledger Fact

No accounting before accepted/finalized state.

---

# 274. Card Transaction Pending Accounting

A card transaction may arrive before Expense details.

Possible accounting policy:

```text
Card clearing/suspense by Banking
```

outside Expenses until classification.

Do not create fake expense category.

---

# 275. Unclassified Spend

Should appear as Finance exception:

```text
Unclassified Corporate Spend
```

not hidden.

---

# 276. No Hidden Miscellaneous Fallback

Missing category/accounting mapping should block finalization or require explicit configured category.

Do not post silently to suspense just to succeed.

---

# 277. Accounting Preview

Finance can preview via Ledger Posting Engine:

```text
Expense semantic fact
→ expected Journal
```

using real engine.

---

# 278. Expense Corrections

After finalization:

```text
reverse original financial fact
+
finalize corrected Expense
```

or controlled delta if accounting policy supports and audit remains exact.

---

# 279. Expense Reversal

Original remains.

Reversal reason mandatory.

---

# 280. Reimbursement Accounting Separate From Expense Accounting

Expense can be posted before employee is reimbursed.

---

# 281. Payment Integration Mission

Payments answers:

```text
Did actual money move?
```

Expenses never claims this from approval.

---

# 282. Payment Requests

Expenses can request:

```text
Employee Reimbursement
Advance Issuance
Employee Repayment recording/collection
Advance Return
```

through Payments.

---

# 283. Stable Payment Source

Every payment request includes:

```text
SourceType
SourceId
Purpose
IdempotencyKey
```

---

# 284. Double Payment Protection

Retry cannot reimburse employee twice.

---

# 285. Unknown Payment State

Provider timeout:

```text
Pending/Unknown
```

not Failed.

Use Payments recovery.

---

# 286. Partial Reimbursement Payment

Supported where payment model permits.

Outstanding remains exact.

---

# 287. Reimbursement Payment Failure

Does not reverse valid Expense.

---

# 288. Employee Repayment Failure

Employee debt remains outstanding.

---

# 289. Advance Issuance Failure

Employee accountability not marked issued.

---

# 290. External Settlement

If Finance settles outside TODIR:

authorized command can record:

```text
external reference
date
amount
evidence
```

and create semantic payment/settlement record according to Payments boundary.

Do not simply set `Paid=true`.

---

# 291. Payment Batch

Bulk reimbursements can be prepared.

---

# 292. Reimbursement Schedule

Company may reimburse:

```text
Immediately after approval
Daily
Weekly
Twice monthly
With payroll later
Manual batch
```

Settlement scheduling is configurable.

---

# 293. Employee Notification

Employee should know:

```text
Submitted
Approved
Rejected
Finalized
Scheduled for reimbursement
Paid
Payment failed
```

without seeing accounting internals.

---

# 294. Finance Exception Queue

Finance workbench:

```text
Missing receipt
Policy violations
Card unmatched
Personal card spend
Pending advances
Overdue advances
Reimbursement failed
Employee repayment overdue
Accounting failed
Tax evidence missing
```

---

# 295. Exception-Driven Expenses

Healthy compliant Expenses should require minimal manual Finance work.

---

# 296. Employee Workspace

Recommended:

```text
My Expenses
My Reports
My Advances
My Reimbursements
My Repayments
```

if Self-Service active.

---

# 297. Employee Expense List

Show:

```text
Date
Merchant
Category
Amount
Status
Policy
Reimbursement Status
```

---

# 298. Finance Expense Workspace

Show:

```text
Expense
Employee
Payment Source
Policy Findings
Evidence
Tax
Dimensions
Approval
Accounting
Settlement
```

---

# 299. Corporate Card Inbox UX

Focus on missing action:

```text
Needs Receipt
Needs Category
Needs Employee
Needs Purpose
Possible Personal
Possible Duplicate
```

---

# 300. Advance Workspace

Show:

```text
Original Issued
Expenses Applied
Returned
Company Owes Employee
Employee Owes Company
Outstanding
Due Date
Status
```

---

# 301. Small Company UX

User should not need to understand:

```text
policy version
semantic accounting facts
subledger
tax profile code
```

to submit a normal Expense.

---

# 302. Progressive Disclosure

Advanced fields only appear when:

```text
category requires
country requires
policy requires
payment source requires
project/dimension active
```

---

# 303. Mobile-First Capture

Receipt photo and quick Expense entry are core.

But domain workflow remains identical across devices.

---

# 304. Fast Capture

Possible:

```text
Take photo
→ OCR async
→ choose/confirm category
→ business purpose
→ submit
```

---

# 305. Draft From Receipt

Can create Draft immediately before OCR completes.

---

# 306. Email Receipt Capture Seam

Future/provider:

```text
forward receipt email
→ attachment ingestion
→ OCR
→ draft/match
```

---

# 307. Messaging Capture Seam

WhatsApp/other channels may ingest receipt through connector later.

Do not hardcode channel in domain.

---

# 308. AI Suggestions

AI may:

```text
extract receipt
suggest category
suggest business purpose
suggest project
detect duplicate
summarize policy violation
suggest card transaction match
```

---

# 309. AI Restrictions

AI must not autonomously:

```text
approve high-risk expense
finalize expense
reimburse employee
mark personal charge repaid
write off advance
change tax result
write Ledger
```

---

# 310. AI Confidence

Suggestions should include confidence/source.

Low confidence → ask user.

---

# 311. Policy AI Boundary

Policy evaluation is deterministic rules.

AI can explain, not replace.

---

# 312. OCR Security

Receipts may contain:

```text
card last digits
address
tax ID
personal info
```

Use approved document/AI privacy pipeline.

---

# 313. Country Pack Responsibilities

Country/Tax provider may contribute:

```text
tax invoice requirements
VAT recoverability
mileage statutory rates/templates
per diem statutory rates/templates
employee reimbursement constraints
retention rules
required expense evidence
```

Core does not hardcode country law.

---

# 314. Company Policy vs Country Minimum

Company policy can be stricter where lawful.

Cannot override legal minimum/required tax treatment.

---

# 315. Versioned Country Rules

Finalized Expense retains relevant provider version.

---

# 316. Corporate Card Provider Seam

Interface can support:

```text
card feed
cardholder mapping
authorization/cleared state
merchant data
credits
statement cycle
```

---

# 317. Banking Seam

Future Banking/Reconciliation can match:

```text
bank/card transactions
→ Payments
→ Expenses
```

---

# 318. Card Feed Is Read/Import, Not Expense Calculation Authority

Feed supplies transaction facts.

Expense policy/accounting still TODIR.

---

# 319. External Transaction States

Potential:

```text
Pending
Cleared
Reversed
Credited
Disputed
```

source provider.

---

# 320. Disputed Card Transaction

Do not finalize normal Expense blindly.

Track dispute state/reference.

---

# 321. Chargeback/Credit

Provider transaction correction is separate from Expense correction.

Need match/reconciliation.

---

# 322. Card Statement Reconciliation

Optional read model:

```text
Statement total
Matched transactions
Credits
Personal charges
Unmatched
Difference
```

---

# 323. Expense Category Accounting Mapping

Category has semantic AccountingRole.

Ledger Account Determination can use:

```text
Legal Entity
Expense Category Role
Tax treatment
Department
Project
Payment Source
```

---

# 324. Control Account Protection

Employee reimbursement/advance/control accounts cannot be manually chosen by normal Expense user.

---

# 325. Expense Number

Optional human reference.

Use Platform Numbering.

No `MAX+1`.

---

# 326. Report Number

Optional.

Separate from Expense IDs.

---

# 327. External Source References

Support:

```text
CardTransactionId
LegacyExpenseId
OCRDocumentId
TripId
ProjectId
PaymentId
PayrollSettlementId
```

typed.

---

# 328. Idempotency

Mandatory for:

```text
card feed ingestion
receipt ingestion
Expense import
Submit
Approve
Finalize
Reimburse
Advance issuance
Advance application
Advance return
Employee repayment
External settlement
Correction
```

where retries occur.

---

# 329. Same Idempotency Key Different Payload

Conflict.

---

# 330. Concurrency — Expense Edit

Optimistic row version.

---

# 331. Concurrency — Approval vs Edit

Submitted/approval version must match.

Editing relevant data invalidates prior pending approval according to policy.

---

# 332. Concurrency — Reimbursement

Two payment jobs cannot reimburse same outstanding obligation beyond amount.

---

# 333. Concurrency — Advance Application

Two reports cannot over-apply same Advance.

---

# 334. Concurrency — Employee Repayment

Cannot over-settle receivable.

---

# 335. Concurrency — Card Match

One transaction allocation protected from duplicate full match.

---

# 336. Finalization Snapshot

Freeze:

```text
Employee/employment context
Category/version
Amounts/currency
Tax
Policy evaluation
Dimensions
Payment source
Evidence refs
Itemizations
Participants
Advance allocation
```

---

# 337. Employee/Department Changes Later

Historical Expense keeps original cost attribution snapshot unless controlled correction.

---

# 338. Expense Date vs Submission Date

Separate.

---

# 339. Expense Date vs Accounting Date

Separate.

---

# 340. Accounting Date

Ledger/period policy may choose posting date.

Do not rewrite Expense Date.

---

# 341. Late Expense

Expense from prior period can be submitted later.

Policy/Accounting decides:

```text
post current period
controlled reopen
accrual context
```

not arbitrary backdate.

---

# 342. Closed Accounting Period

Expense finalization cannot bypass Ledger period lock.

---

# 343. Future-Dated Expense

Default block unless category/business scenario permits.

---

# 344. Receipt Date Difference

OCR receipt date differing from entered Expense date:

```text
warning
```

or block per policy.

---

# 345. Merchant Currency Difference

Card settlement currency may differ from merchant transaction currency.

Preserve both.

---

# 346. Tip

Meal/service receipt tip can be itemized/treated by policy.

No universal tax/reimbursement assumption.

---

# 347. Gratuity Limit

Policy may cap/review.

---

# 348. Alcohol/Sensitive Categories

Company policy can disallow or require additional review.

Do not hardcode moral/business assumptions globally.

---

# 349. Weekend/Holiday Spend

Policy can flag.

Not automatically invalid.

---

# 350. Merchant Blacklist/Allowlist

Optional typed policy.

---

# 351. Category Requirement By Merchant

Auto-suggestion only unless deterministic company mapping.

---

# 352. Receipt Threshold

Example:

```text
Receipt required above 200 EGP
```

versioned/currency-aware.

---

# 353. Split To Avoid Limit Detection

Anomaly rule can flag multiple same-day same-merchant expenses around threshold.

Human review.

---

# 354. Repeated Missing Receipts

Trend report/control.

---

# 355. Cash Expense Risk

Cash payment may require stronger evidence/approval.

Policy-specific.

---

# 356. Advance Cash Risk

Outstanding advance aging visible.

---

# 357. Employee Owes/Company Owes Netting

Do not silently net unrelated obligations.

Explicit settlement/allocation required.

---

# 358. Advance Shortfall Reimbursement

Can net approved expense against same advance because they share settlement context.

Other obligations require explicit allocation.

---

# 359. Payroll Loan vs Business Advance

Locked:

```text
Payroll Salary Advance/Loan
→ Payroll

Business Spend Advance
→ Expenses
```

No shared mutable balance.

---

# 360. Personal Card Expense vs Salary Deduction

Employee reimbursement is not payroll earning by default.

Employee repayment is not payroll deduction by default.



# 361. Schema

Recommended:

```text
business_expenses
```

---

# 362. ExpensesDbContext

Own Expenses tables only.

No direct HR/Payments/Ledger/Tax DbContext navigation.

---

# 363. Core Tables

Potential:

```text
expense
expense_itemization
expense_participant
expense_evidence

expense_category
expense_category_version
expense_policy
expense_policy_version
expense_policy_rule
expense_policy_evaluation
expense_policy_finding

expense_report
expense_report_item

corporate_card_transaction_link
corporate_card_allocation
expense_card_match

business_advance
business_advance_application
business_advance_return

expense_reimbursement_obligation
expense_reimbursement_batch
expense_reimbursement_allocation

employee_repayment_obligation
employee_repayment_allocation

mileage_expense_detail
per_diem_expense_detail
per_diem_daily_line

expense_dimension_allocation

expense_external_ref

expense_list_projection
expense_finance_exception_projection
expense_employee_balance_projection
expense_card_inbox_projection
expense_advance_projection
```

Exact normalization may evolve.

---

# 364. Tables Not Owned By Expenses

Do not create:

```text
employee
user
party
project
payment
bank_transaction
card_account
journal_entry
tax_rule
payroll_run
supplier_invoice
```

---

# 365. Tenant Columns

All tenant-owned tables contain:

```text
tenant_id
```

---

# 366. Legal Entity

Every financial Expense belongs exactly one Legal Entity.

---

# 367. RLS

All tenant-owned tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 368. Runtime Role

No owner/BYPASSRLS.

---

# 369. Tenant Context

Explicit transaction:

```text
SET LOCAL app.tenant_id = ...
```

per Platform Kernel.

---

# 370. UUIDv7

Use for IDs.

---

# 371. Money

FinBase Money/numeric precision.

No float/double.

---

# 372. Amount Fields

Use appropriate canonical precision:

```text
numeric(28,8)
```

or FinBase-approved type.

---

# 373. FX

FinBase rate precision.

---

# 374. Dates

Use:

```text
LocalDate
Instant
DateTimeZone
```

by semantic need.

---

# 375. Row Version

Mutable drafts/reports/advances use optimistic concurrency.

---

# 376. Effective Dating

Policies, categories, rates, per-diem/mileage tables versioned/effective dated.

---

# 377. Sensitive Fields

Payment destination, tax IDs or employee personal data not duplicated unless necessary.

---

# 378. JSONB

Allowed for bounded immutable policy/evidence extraction snapshots.

Do not hide hot query fields.

---

# 379. Expense Search Projection

Common safe fields:

```text
Date
Employee
Merchant
Category
Amount
Currency
State
Policy status
Payment source
Reimbursement status
```

---

# 380. Finance Exception Projection

Rebuildable from canonical state.

---

# 381. Employee Balance Projection

Shows:

```text
Company owes employee
Employee owes company
Active advances
Pending reimbursements
```

Rebuildable from obligations/settlements.

---

# 382. Corporate Card Inbox Projection

Combines external transaction references + Expense matching state.

---

# 383. Indexes — Expenses

Likely:

```text
tenant/entity/expense_date desc
tenant/entity/state
tenant/employee/expense_date desc
tenant/category/date
tenant/payment_source/date
tenant/report
tenant/policy_state
```

---

# 384. Indexes — Evidence

```text
expense_id
file_hash
invoice_number hash/normalized where lawful
```

---

# 385. Indexes — Card Match

```text
tenant/external_source/external_transaction_id unique
tenant/employee/date/amount
status/date
```

---

# 386. Indexes — Advance

```text
tenant/employee/state
tenant/entity/settlement_due_date
```

---

# 387. Indexes — Reimbursement

```text
tenant/employee/status
tenant/entity/status/currency
batch/status
```

---

# 388. Indexes — Repayment

Similar.

---

# 389. Partitioning

Do not partition Day 1.

Measure high-volume card/expense history first.

---

# 390. PostgreSQL First

No OpenSearch/warehouse required for operational module.

---

# 391. Module Contracts

Locked naming:

```text
Todir.Expenses.Contracts
```

---

# 392. Backend Namespaces

Recommended:

```text
Todir.Modules.Expenses.Domain
Todir.Modules.Expenses.Application
Todir.Modules.Expenses.Infrastructure
Todir.Expenses.Contracts
```

---

# 393. Internal Areas

Potential:

```text
Expenses/
Reports/
Categories/
Policies/
Evidence/
CorporateCards/
Advances/
Reimbursements/
Repayments/
Mileage/
PerDiem/
Tax/
Accounting/
Reporting/
```

---

# 394. No Generic CRUD Service

Use semantic commands.

---

# 395. Expense Commands

Representative:

```text
CreateExpense
UpdateDraftExpense
SubmitExpense
WithdrawSubmittedExpense
ApproveExpense
RejectExpense
HoldExpense
FinalizeExpense
ReverseExpense
CorrectExpense
```

---

# 396. Evidence Commands

```text
AttachExpenseEvidence
ReplaceDraftEvidence
VerifyExpenseEvidence
SubmitMissingReceiptDeclaration
```

---

# 397. Itemization Commands

```text
AddExpenseItemization
UpdateExpenseItemization
RemoveExpenseItemization
```

pre-finalization.

---

# 398. Participant Commands

```text
AddExpenseParticipant
RemoveExpenseParticipant
```

---

# 399. Report Commands

```text
CreateExpenseReport
AddExpenseToReport
RemoveExpenseFromReport
SubmitExpenseReport
ApproveExpenseReport
RejectExpenseReport
FinalizeExpenseReport
```

---

# 400. Policy Commands

```text
CreateExpensePolicy
CreatePolicyVersion
AddPolicyRule
ValidatePolicyVersion
PublishPolicyVersion
RetirePolicyVersion
```

---

# 401. Category Commands

```text
CreateExpenseCategory
CreateCategoryVersion
ArchiveExpenseCategory
```

---

# 402. Corporate Card Commands

```text
IngestCorporateCardTransactions
MatchCardTransactionToExpense
CreateExpenseFromCardTransaction
MarkCardTransactionPersonal
AllocateCardTransaction
UnmatchDraftCardTransaction
```

---

# 403. Advance Commands

```text
RequestBusinessAdvance
ApproveBusinessAdvance
RejectBusinessAdvance
RequestAdvanceIssuance
ApplyExpenseToAdvance
RecordAdvanceReturn
SettleBusinessAdvance
WriteOffBusinessAdvance
```

---

# 404. Reimbursement Commands

```text
PrepareReimbursements
CreateReimbursementBatch
RequestReimbursementPayments
RetryFailedReimbursement
RecordExternalReimbursementSettlement
```

---

# 405. Repayment Commands

```text
CreateEmployeeRepaymentObligation
RequestEmployeeRepayment
RecordEmployeeRepayment
RecordExternalEmployeeRepayment
WriteOffEmployeeRepayment
```

---

# 406. Mileage Commands

Usually part of Expense create/update:

```text
SetMileageDetail
```

---

# 407. Per Diem Commands

```text
CalculatePerDiem
SetPerDiemTrip
RecordProvidedMeal
```

---

# 408. No `SetPaid`

Forbidden.

---

# 409. No `SetReimbursed=true`

Forbidden.

---

# 410. No `SetAdvanceBalance`

Forbidden.

---

# 411. No `SetEmployeeDebt`

Forbidden.

---

# 412. No `SetAccountingAccountId`

Forbidden.

---

# 413. API Base

Recommended:

```text
/api/v1/expenses
```

---

# 414. Expense APIs

Representative:

```text
GET  /expenses
POST /expenses
GET  /expenses/{id}
PUT  /expenses/{id}
POST /expenses/{id}/submit
POST /expenses/{id}/approve
POST /expenses/{id}/reject
POST /expenses/{id}/finalize
POST /expenses/{id}/reverse
POST /expenses/{id}/correct
```

---

# 415. Evidence APIs

```text
POST /expenses/{id}/evidence
POST /expenses/{id}/missing-receipt-declaration
GET  /expenses/{id}/evidence
```

Files access through secure Platform Files flow.

---

# 416. Itemization APIs

```text
GET  /expenses/{id}/itemizations
POST /expenses/{id}/itemizations
PUT  /expenses/{id}/itemizations/{itemId}
DELETE /expenses/{id}/itemizations/{itemId}
```

pre-finalization only.

---

# 417. Reports APIs

```text
GET  /reports
POST /reports
POST /reports/{id}/expenses
POST /reports/{id}/submit
POST /reports/{id}/approve
POST /reports/{id}/finalize
```

---

# 418. Policy APIs

```text
GET  /policies
POST /policies
POST /policies/{id}/versions
POST /policy-versions/{id}/validate
POST /policy-versions/{id}/publish
POST /expenses/{id}/evaluate-policy
```

---

# 419. Card Inbox APIs

```text
GET  /corporate-card/inbox
POST /corporate-card/transactions/import
POST /corporate-card/transactions/{id}/match
POST /corporate-card/transactions/{id}/create-expense
POST /corporate-card/transactions/{id}/mark-personal
POST /corporate-card/transactions/{id}/allocate
```

---

# 420. Advance APIs

```text
GET  /advances
POST /advances
POST /advances/{id}/approve
POST /advances/{id}/issue
POST /advances/{id}/apply
POST /advances/{id}/return
POST /advances/{id}/settle
```

---

# 421. Reimbursement APIs

```text
GET  /reimbursements
POST /reimbursements/prepare
POST /reimbursement-batches
POST /reimbursement-batches/{id}/pay
GET  /reimbursement-batches/{id}
```

---

# 422. Repayment APIs

```text
GET  /repayments
POST /repayments/{id}/request
POST /repayments/{id}/record
```

---

# 423. Self-Service APIs

```text
GET  /my/expenses
GET  /my/reports
GET  /my/advances
GET  /my/reimbursements
GET  /my/repayments
POST /my/expenses
```

Server resolves Employee link.

---

# 424. Expense Policy Preview

Employee can see before submit:

```text
Compliant
Receipt required
Over limit
Needs reason
```

---

# 425. API Does Not Accept Accounting Totals From Client

Server authoritative.

---

# 426. API Does Not Accept PolicyResult From Client

Server evaluates.

---

# 427. Error Codes

Stable examples:

```text
expenses.expense_not_found
expenses.expense_not_editable
expenses.expense_already_finalized
expenses.expense_policy_blocked
expenses.expense_policy_reason_required
expenses.expense_receipt_required
expenses.expense_duplicate_suspected
expenses.expense_itemization_mismatch
expenses.expense_participants_required

expenses.card_transaction_not_found
expenses.card_transaction_already_allocated
expenses.card_transaction_amount_mismatch
expenses.card_transaction_pending

expenses.advance_not_issued
expenses.advance_over_applied
expenses.advance_overdue
expenses.advance_already_settled

expenses.reimbursement_over_settlement
expenses.reimbursement_payment_pending
expenses.repayment_over_settlement

expenses.tax_validation_failed
expenses.fx_rate_missing
expenses.accounting_failed
expenses.closed_period

expenses.permission_denied
expenses.concurrent_change
expenses.idempotency_conflict
```

---

# 428. Friendly Error — Missing Receipt

> "السياسة الحالية تتطلب مستندًا لهذا المصروف. ارفع الإيصال أو استخدم إقرار فقدان المستند إذا كانت الشركة تسمح بذلك."

---

# 429. Friendly Error — Policy Limit

Show:

```text
Allowed limit
Expense amount
Difference
Allowed next action
```

---

# 430. Friendly Error — Itemization

> "مجموع تفاصيل المصروف لا يساوي إجمالي المستند."

---

# 431. Friendly Error — Advance

> "المبلغ المتبقي من السلفة لا يكفي لتغطية هذا الجزء من المصروف."

---

# 432. Friendly Error — Card Pending

> "المعاملة البنكية لم تُسوَّ بعد، وقد يتغير المبلغ النهائي. يمكنك إضافة الإيصال الآن وسيؤكد TODIR المبلغ بعد التسوية."

---

# 433. Permissions

Initial vocabulary:

```text
expenses.expense.read
expenses.expense.create
expenses.expense.submit
expenses.expense.approve
expenses.expense.finalize
expenses.expense.correct

expenses.evidence.read
expenses.evidence.manage
expenses.evidence.verify

expenses.policy.read
expenses.policy.manage
expenses.policy.exception_approve

expenses.category.manage

expenses.card.read
expenses.card.match
expenses.card.mark_personal

expenses.advance.read
expenses.advance.request
expenses.advance.approve
expenses.advance.issue
expenses.advance.settle
expenses.advance.writeoff

expenses.reimbursement.read
expenses.reimbursement.prepare
expenses.reimbursement.pay

expenses.repayment.read
expenses.repayment.request
expenses.repayment.settle
expenses.repayment.writeoff

expenses.report.read
expenses.report.manage

expenses.audit.read
expenses.export
expenses.settings.manage
```

---

# 434. Scope Model

Combine permission with:

```text
Self
DirectReports
Department
Branch
LegalEntity
Project
AllAuthorizedEntities
```

---

# 435. Employee Self Scope

Server resolves User→Employee.

Do not trust supplied EmployeeId.

---

# 436. Manager Scope

Manager can approve team expenses but not automatically see:

```text
all salary
all HR documents
other financial data
```

---

# 437. Finance Scope

Finance may see all Expense financial data for authorized Legal Entity.

---

# 438. Project Manager Scope

Can approve/view project-related expenses without employee unrelated spend.

---

# 439. Sensitive Receipt

Receipts can contain personal/card information.

Access controlled.

---

# 440. Card Number

Do not store raw card PAN.

Use token/card reference/last4 from provider.

---

# 441. Employee Bank Details

Do not expose in Expense APIs.

Payments owns secure payout profile.

---

# 442. Export Security

Salary-independent but still sensitive company spend.

Dedicated export permission.

---

# 443. Audit Critical Actions

Capture:

```text
Expense submit/approve/reject/finalize
Policy exception
Missing receipt declaration
Evidence replacement
Card transaction match/personal classification
Advance approval/issue/application/return/writeoff
Reimbursement request/retry
Employee repayment request/settlement/writeoff
Correction/reversal
Policy publish
Category/accounting role change
Large export
```

---

# 444. Audit vs Business History

Both.

---

# 445. Data Retention

Country/company retention policy.

Receipts/accounting evidence may require years of retention.

---

# 446. Delete Draft

Unused Draft may be deleted.

Submitted/finalized financial evidence generally not hard-deleted.

---

# 447. Privacy Requests

Retention/legal obligations override casual deletion.

Use archive/restrict/pseudonymize where lawful.

---

# 448. AI Data Minimization

Only necessary receipt/expense fields sent to AI/OCR.

---

# 449. Integration Events

Potential:

```text
ExpenseSubmittedV1
ExpenseApprovedV1
ExpenseFinalizedV1
ExpenseReversedV1

CorporateCardExpenseMatchedV1
PersonalCardChargeIdentifiedV1

BusinessAdvanceApprovedV1
BusinessAdvanceIssuedV1
BusinessAdvanceAppliedV1
BusinessAdvanceSettledV1

ExpenseReimbursementRequestedV1
ExpenseReimbursementCompletedV1
EmployeeRepaymentRequestedV1
EmployeeRepaymentCompletedV1
```

---

# 450. Draft Noise

Do not publish every draft edit globally.



# 451. Performance Mission

Expense capture should feel lightweight.

Finance review must scale to large card/expense populations.

Correctness beats premature infrastructure complexity.

---

# 452. Employee Create Target

Draft creation:

```text
p95 <= 150-300ms
```

excluding file upload/OCR.

---

# 453. Receipt Upload

Upload asynchronously through Platform Files.

Do not block Expense draft creation on OCR.

---

# 454. OCR Latency

Background.

UI shows:

```text
Processing receipt...
```

Employee can continue.

---

# 455. Expense List Target

```text
p95 <= 100-250ms
```

for indexed paged queries.

---

# 456. Finance Inbox Target

```text
p95 <= 150-300ms
```

for actionable filters.

---

# 457. Policy Evaluation Target

Interactive:

```text
p95 <= 50-150ms
```

for normal rule count, excluding remote services.

---

# 458. Finalization Target

```text
p95 <= 200-500ms
```

for normal Expense excluding external Payments.

Measured, not contractual.

---

# 459. Corporate Card Feed Scale

Load test:

```text
1M+ historical card transactions
100k new transactions/day architecture stress
```

for larger tenants.

---

# 460. Expense Scale

Load test:

```text
1M+ Expenses
multi-year evidence metadata
many employees
```

---

# 461. Policy Rule Scale

Do not evaluate every policy rule blindly.

Compile/filter by applicability:

```text
Legal Entity
category
employee group
effective date
payment source
```

---

# 462. No N+1

Expense list cannot fetch per row:

```text
Employee
Category
Policy
Payment
Project
Tax
```

one-by-one.

---

# 463. HR Batch/Projection

Use safe Employee display projection.

---

# 464. Policy Cache

Published immutable versions can be locally cached by version.

---

# 465. Category Cache

Safe local cache.

---

# 466. Tax Calls

Use Tax batch/resolver contract where bulk finalization/import requires.

---

# 467. Card Import

Process batches with idempotency.

---

# 468. Duplicate Detection Performance

Use normalized/hash indexes.

Do not scan all historical Expenses per submit.

---

# 469. File Hash

Compute/store secure hash metadata via Files pipeline when available.

---

# 470. Search

PostgreSQL first.

No OpenSearch Day 1.

---

# 471. Arabic Search

Employee/merchant/category searches support normalized Arabic where applicable.

---

# 472. Keyset Pagination

Use for deep history:

```text
Expenses
Card transactions
Advances
Reimbursements
Audit/business timeline
```

---

# 473. Reports

Operational reports can use rebuildable read models.

Ledger remains accounting truth.

---

# 474. Expense Summary Projection

Potential:

```text
Daily/Monthly spend by:
category
employee
department
branch
project
payment source
```

rebuildable.

---

# 475. Reimbursement Projection

```text
Pending
Scheduled
Paid
Failed
```

---

# 476. Advance Aging Projection

```text
Current
Due Soon
Overdue 1-30
Overdue 31-60
Overdue 60+
```

---

# 477. Card Compliance Projection

```text
Matched
Missing Receipt
Missing Purpose
Personal
Unmatched
```

---

# 478. No Redis Canonical Balance

Do not store employee/advance balances as Redis truth.

---

# 479. Background Jobs

Use Quartz/Worker for:

```text
OCR
receipt reminders
card imports
card matching suggestions
reimbursement scheduling
advance overdue reminders
large exports
projection rebuilds
```

---

# 480. Job Idempotency

Mandatory.

---

# 481. Notifications

Use Platform Notifications.

Examples:

```text
Expense rejected
Expense approved
Receipt missing
Policy reason required
Advance approved
Advance overdue
Reimbursement paid/failed
Employee repayment due
```

---

# 482. Reminder Fatigue

Do not notify daily forever by default.

Escalation schedule configurable.

---

# 483. Observability Metrics

Potential:

```text
expenses_create_duration
expenses_policy_evaluation_duration
expenses_finalize_duration
expenses_policy_violation_total
expenses_missing_receipt_total
expenses_duplicate_warning_total
expenses_card_unmatched_count
expenses_reimbursement_failure_total
expenses_advance_overdue_count
expenses_employee_repayment_overdue_count
expenses_ocr_failure_total
```

No EmployeeId as metric label.

---

# 484. Tracing — Finalization

Spans:

```text
Load Expense
Authorize
Validate State
Evaluate/Verify Policy Snapshot
Resolve Tax
Validate Evidence
Resolve Dimensions
Create Financial Fact
Ledger Posting
Create Reimbursement/Advance/Repayment effect
Audit
Outbox
Commit
```

---

# 485. Tracing — Reimbursement

```text
Resolve obligations
Validate outstanding
Create payment requests
Payments status
Allocate settlement
```

---

# 486. Tracing — Card Match

```text
External transaction
Candidate match
User/auto decision
Expense link
policy/evidence state
```

---

# 487. Logging

Never dump:

```text
full receipt OCR text
personal bank data
card number
employee sensitive data
```

---

# 488. Reports — Core

```text
Expense Register
Expenses By Category
Expenses By Employee
Expenses By Department
Expenses By Branch
Expenses By Project
Expenses By Payment Source
Policy Violations
Missing Receipts
Pending Approval
Pending Finalization
Pending Reimbursement
Reimbursement History
Employee Repayments
Business Advances
Advance Aging
Corporate Card Reconciliation
Tax/VAT Summary
Mileage
Per Diem
```

---

# 489. Expense Register

Columns:

```text
Date
Employee
Merchant
Category
Payment Source
Original Amount/Currency
Base Amount
Tax
Policy
Approval
Accounting
Settlement
```

permission-aware.

---

# 490. Spend By Category

Operational expense analysis.

Do not present as P&L without Ledger reconciliation.

---

# 491. Spend By Employee

Sensitive.

Permission-controlled.

Not a performance ranking by default.

---

# 492. Spend By Department/Project

Uses finalized allocation snapshots.

---

# 493. Policy Violation Report

Group:

```text
Rule
Category
Employee
Department
Approver
Outcome
```

---

# 494. Missing Receipt Report

Only where receipt required.

---

# 495. Reimbursement Liability Report

Shows:

```text
Company Owes Employees
by employee
currency
age
```

---

# 496. Employee Repayment Report

Shows:

```text
Employees Owe Company
source
age
amount
```

---

# 497. Advance Aging

Critical.

---

# 498. Card Reconciliation

```text
Card feed total
Business Expense matched
Personal
Credits
Unmatched
Difference
```

---

# 499. Tax Summary

Uses Tax snapshot:

```text
Gross
Tax
Recoverable
Nonrecoverable
```

---

# 500. Expense vs Ledger Reconciliation

Finance report:

```text
Finalized Expense Facts
vs
Ledger posted totals
```

Zero unexplained difference.

---

# 501. Reimbursement vs Payments Reconciliation

```text
Reimbursement obligations
vs
Payment allocations
```

---

# 502. Advance Reconciliation

```text
Issued
Applied
Returned
Outstanding
```

equals employee advance financial control balance.

---

# 503. Card Reconciliation vs Banking

Future Banking integration:

```text
card/bank statement
vs
expense classification/accounting
```

---

# 504. Export

Formats:

```text
Excel
CSV
PDF where report-style useful
```

---

# 505. Export Numeric Integrity

Do not stringify numeric values incorrectly.

---

# 506. Large Export

Background secure file.

---

# 507. Import — Expense Categories

Support config migration.

---

# 508. Import — Policies

Prefer guided configuration, but structured import can support enterprise migration.

---

# 509. Import — Historical Expenses

Strategies:

```text
SummaryOnly
DetailedFinalized
OpenOnly
NoHistory
```

---

# 510. Import — Outstanding Reimbursements

Must preserve:

```text
Employee
Amount
Currency
Original reference
AsOf date
```

without fake Payment.

---

# 511. Import — Outstanding Advances

Need:

```text
Issued amount
Applied known amount
Returned amount
Outstanding
Currency
Due date
Legacy refs
```

---

# 512. Import — Employee Repayments

Open receivables imported explicitly.

---

# 513. Import — Card Transactions

Stable external IDs.

---

# 514. Import — Receipt Files

Can migrate to Platform Files with metadata.

---

# 515. Migration Quality

Do not fabricate:

```text
approval history
receipt verification
tax detail
FX source
```

if legacy data lacks it.

Mark migration provenance.

---

# 516. Migration Source Ref

```text
LegacySystem
LegacyExpenseId
BatchId
RowNumber
FileHash
ImportedAt
```

---

# 517. Expense Go-Live

Per Legal Entity:

```text
ExpensesGoLiveDate
HistoricalBoundary
OpenReimbursementStrategy
AdvanceOpeningStrategy
CardFeedStartDate
```

---

# 518. Parallel Controls

For migration:

compare:

```text
legacy expense totals
TODIR imported/open balances
employee reimbursements
advances
```

---

# 519. Security Test — Cross Tenant

Tenant A cannot:

```text
read
export
view receipts
settle
```

Tenant B.

---

# 520. Security Test — Cross Entity

Entity-scoped user cannot access another Legal Entity.

---

# 521. Security Test — Self

Employee cannot change ID and view another employee's Expense.

---

# 522. Security Test — Manager

Manager sees allowed team Expenses only.

---

# 523. Security Test — Finance

Authorized entity scope only.

---

# 524. Security Test — Receipt

Signed URL only after Expense permission.

---

# 525. Security Test — Corporate Card

No raw card data.

---

# 526. Security Test — Reimbursement

Unauthorized user cannot trigger Payment.

---

# 527. Security Test — Advance Write-Off

High-risk permission.

---

# 528. Security Test — Policy Publish

High-risk config permission.

---

# 529. Security Test — Export

Respects scope/field permissions.

---

# 530. Test — Minimal Employee Expense

Create:

```text
Amount
Date
Category
Business Purpose
Receipt if required
```

without enterprise fields.

---

# 531. Test — Employee Without User

Finance can create Expense for HR Employee.

---

# 532. Test — Employee Personal Funds

Finalized Expense creates reimbursement obligation.

---

# 533. Test — Company Card Expense

No employee reimbursement.

---

# 534. Test — Petty Cash Expense

No employee reimbursement; cash source linked.

---

# 535. Test — Supplier Invoice Duplicate

Formal AP invoice cannot be accidentally duplicated as Expense when source linked/matching identifies it.

---

# 536. Test — Itemization Exact

Itemization total mismatch blocked.

---

# 537. Test — Participant Requirement

Meal category with participants-required cannot submit without participants.

---

# 538. Test — Missing Receipt Allowed

Creates declaration/finding, not fake receipt.

---

# 539. Test — Missing Receipt Blocked

Submission/finalization blocked according to policy.

---

# 540. Test — Receipt Threshold

Below/above exact threshold.

---

# 541. Test — Duplicate Receipt

Warning/exact idempotent behavior.

---

# 542. Test — OCR

Suggestion never silently finalizes.

---

# 543. Test — Policy Warning

Can submit with visible warning if allowed.

---

# 544. Test — Policy Require Reason

No reason → block.

Reason → continue/approval.

---

# 545. Test — Policy Block

Cannot bypass without explicit exception capability if policy allows no exception.

---

# 546. Test — Policy Version

Old Expense remains on old policy.

---

# 547. Test — Approval

Approval does not set Reimbursed.

---

# 548. Test — Rejection

No accounting/reimbursement effect.

---

# 549. Test — Finalization

Creates immutable financial snapshot.

---

# 550. Test — Correction

Original remains; accounting correction exact.

---

# 551. Test — Closed Period

Expense cannot silently back-post.

---

# 552. Test — Reimbursement

Payment completion settles obligation exactly once.

---

# 553. Test — Reimbursement Failure

Obligation remains.

---

# 554. Test — Partial Reimbursement

Outstanding exact.

---

# 555. Test — Batch Reimbursement

Allocations reconcile.

---

# 556. Test — Terminated Employee

Outstanding approved reimbursement remains payable.

---

# 557. Test — Card Feed Retry

No duplicate external transaction.

---

# 558. Test — Card Pending To Cleared

Draft updates controlled; finalized amount not silently changed.

---

# 559. Test — Card Receipt Match

Correct match.

---

# 560. Test — Ambiguous Receipt Match

Human decision required.



# 561. Test — Personal Corporate Card Charge

No business Expense.

Creates Employee Repayment obligation.

---

# 562. Test — Partial Personal Portion

Business and personal allocations sum to card transaction exactly.

---

# 563. Test — Employee Repayment

Actual settlement reduces outstanding exactly once.

---

# 564. Test — Repayment Over-Settlement

Blocked.

---

# 565. Test — Card Credit

Links to original and creates controlled corrective effect.

---

# 566. Test — Advance Approval

No employee accountability until actual issuance.

---

# 567. Test — Advance Issuance

Payment success creates issued amount exactly once.

---

# 568. Test — Advance Payment Failure

No issued balance.

---

# 569. Test — Advance Application

Expense reduces advance accountable amount.

---

# 570. Test — Advance Over-Application

Blocked under concurrency.

---

# 571. Test — Advance Under-Spend

Employee repayment obligation/return requirement exact.

---

# 572. Test — Advance Over-Spend

Employee reimbursement shortfall exact.

---

# 573. Test — Advance Exact Settlement

Zero residual.

---

# 574. Test — Advance Cash Return

Payment/cash movement reduces outstanding.

---

# 575. Test — Advance Multi-Currency

Original currencies and settlement FX trace exact.

---

# 576. Test — Advance Overdue

Correct aging/reminder.

---

# 577. Test — New Advance With Old Outstanding

Configured policy exact.

---

# 578. Test — Employee Termination With Advance

Outstanding surfaced to offboarding/final settlement seam.

---

# 579. Test — Mileage

Distance × effective rate exact.

---

# 580. Test — Mileage Future Rate

Not applied before effective date.

---

# 581. Test — Mileage UOM

Miles/km conversion explicit.

---

# 582. Test — Mileage + Fuel Double Claim Rule

Policy warning/block exact.

---

# 583. Test — Per Diem

Daily lines and total exact.

---

# 584. Test — Per Diem Meal Reduction

Configured percentages exact.

---

# 585. Test — Per Diem Partial Day

Provider/policy golden vector.

---

# 586. Test — Per Diem + Actual Meal

Configured double-reimbursement rule exact.

---

# 587. Test — Foreign Expense

Original amount + FX snapshot + base amount exact.

---

# 588. Test — Actual Card FX

Merchant currency and settlement currency preserved.

---

# 589. Test — Tax Recoverable

TaxResult snapshot exact.

---

# 590. Test — Nonrecoverable Tax

No false recoverable VAT.

---

# 591. Test — Missing Tax Invoice

Correct policy/tax outcome.

---

# 592. Test — Dimension Default

Branch/Department inferred from historical HR Employment at Expense date where policy says.

---

# 593. Test — Employee Transfers Later

Finalized Expense retains original attribution.

---

# 594. Test — Dimension Split

Allocations sum exactly to Expense amount.

---

# 595. Test — Project Expense

Project reference valid and permission-scoped.

---

# 596. Test — Report Grouping

Adding/removing draft/submitted Expenses does not alter their amounts.

---

# 597. Test — Partial Report Approval

If enabled, valid Expense can proceed while held Expense stays pending.

---

# 598. Test — Large Receipt Upload

Async; draft remains responsive.

---

# 599. Test — OCR Failure

User can complete manually.

---

# 600. Test — Card Inbox Scale

Large transaction list indexed/paged.

---

# 601. Test — Concurrent Reimbursement Workers

Cannot overpay.

---

# 602. Test — Concurrent Advance Applications

Cannot exceed issued amount.

---

# 603. Test — Concurrent Card Matching

One cleared transaction cannot be double fully matched.

---

# 604. Test — Double Finalize

One financial result.

---

# 605. Test — Idempotent Correction

No duplicate reversal.

---

# 606. Property Test — Itemization

```text
SUM(itemization amounts) == Expense amount
```

when full itemization required.

---

# 607. Property Test — Card Allocation

```text
SUM(card allocations) <= cleared transaction amount
```

and equals it when fully classified.

---

# 608. Property Test — Reimbursement

```text
Total Reimbursed <= Eligible Reimbursement
```

---

# 609. Property Test — Repayment

```text
Total Repaid <= Employee Obligation
```

---

# 610. Property Test — Advance

```text
Issued
=
Applied Expenses
+ Returned
+ Outstanding
```

adjusted for explicit FX semantics.

---

# 611. Property Test — Expense Totals

```text
Gross = Net + Tax
```

where tax mode represents this relationship, with Tax Engine exact semantics.

---

# 612. Property Test — Dimension Allocations

```text
SUM(allocations) == allocatable amount
```

---

# 613. Property Test — Finalized Immutability

No ordinary edit command changes finalized financial fields.

---

# 614. Architecture Test — No Employee Copy

Expenses references HR Employee contracts.

---

# 615. Architecture Test — No Payment Tables

Expenses does not own actual Payment.

---

# 616. Architecture Test — No Ledger Entry Creation

Semantic facts only.

---

# 617. Architecture Test — No Tax Formula

Tax Engine only.

---

# 618. Architecture Test — No Fake Supplier For Employee

Employee reimbursement path is typed.

---

# 619. Architecture Test — No Catalog Product Categories

ExpenseCategory is independent.

---

# 620. Architecture Test — No Arbitrary Policy Script

Typed policy engine.

---

# 621. Red-Team — Expense = Supplier Invoice

Reject.

---

# 622. Red-Team — Approved Means Paid

Reject.

---

# 623. Red-Team — Card Transaction Auto-Finalizes Expense

Reject.

Draft/match/policy required.

---

# 624. Red-Team — Company Card Personal Spend As Expense

Reject.

Employee repayment.

---

# 625. Red-Team — Employee Is Supplier

Reject.

---

# 626. Red-Team — Business Advance Is Payroll Salary Advance

Reject.

---

# 627. Red-Team — Advance Balance Editable

Reject.

---

# 628. Red-Team — Reimbursement Checkbox

Reject.

Actual settlement.

---

# 629. Red-Team — Missing Receipt Always Blocks

Reject universal rule.

Policy decides.

---

# 630. Red-Team — Missing Receipt Always Allowed

Reject universal rule.

Policy decides.

---

# 631. Red-Team — Every Policy Violation Blocks

Reject.

Warnings/approval/exception supported.

---

# 632. Red-Team — Policy Re-Evaluated Using Today Rules

Reject for historical finalized Expense.

---

# 633. Red-Team — OCR Is Financial Authority

Reject.

---

# 634. Red-Team — Duplicate Detection Auto-Rejects Similar Receipt

Reject unless exact idempotent source.

---

# 635. Red-Team — Every Merchant Must Be Supplier

Reject.

---

# 636. Red-Team — Every Expense Needs Report

Reject.

---

# 637. Red-Team — Every Expense Needs Multi-Step Approval

Reject.

---

# 638. Red-Team — Tax = Entered Tax Field

Reject.

Tax Engine validates/resolves.

---

# 639. Red-Team — Foreign Expense Stores Base Amount Only

Reject.

Preserve original currency.

---

# 640. Red-Team — Mileage Is Free-Text Amount

Reject.

Store distance/rate/version.

---

# 641. Red-Team — Per Diem Is Flat Manual Amount

Reject.

Typed travel/rate calculation.

---

# 642. Red-Team — Employee Debt Netted Invisibly Against Reimbursement

Reject.

Explicit allocation/settlement.

---

# 643. Red-Team — Card Pending Transaction Treated As Cleared

Reject.

---

# 644. Red-Team — Finance Can SQL Fix Advance

Reject.

Semantic correction.

---

# 645. Competitive Benchmark Principle — Low Data Entry

Modern spend systems reduce employee input by using:

```text
card feed
receipt OCR
HR defaults
policy defaults
merchant/category suggestions
```

TODIR adopts the principle:

> **Do not ask the employee for a fact TODIR can safely infer.**

---

# 646. Competitive Benchmark Principle — Card Inbox

Card-first systems prove that:

```text
unmatched transaction management
```

is as important as traditional reimbursement claims.

TODIR treats it as first-class.

---

# 647. Competitive Benchmark Principle — Cash Advances

Enterprise expense systems prove business cash advance settlement needs its own lifecycle.

TODIR strengthens this for markets where cash advances remain common.

---

# 648. Competitive Benchmark Principle — Individual Expense First

Expense can be handled individually.

Report grouping is optional.

This reduces unnecessary workflow for small companies.

---

# 649. Competitive Benchmark Principle — Policy Automation

Policies should identify exceptions rather than force Finance to inspect every normal Expense manually.

---

# 650. Competitive Benchmark Principle — Employee Repayment

Spend platforms show the inverse flow matters:

```text
Employee owes Company
```

not only company owes employee.

---

# 651. Product Acceptance — Small Company

Owner/accountant can:

```text
record employee Expense
attach receipt
approve
finalize
reimburse
```

without policy/workflow configuration complexity.

---

# 652. Product Acceptance — Employee

Employee can:

```text
capture receipt quickly
see policy issue before submit
track approval
track reimbursement
see advance balance
```

without accounting knowledge.

---

# 653. Product Acceptance — Finance

Finance can answer:

```text
What is unapproved?
What is missing evidence?
What is out of policy?
What company owes employees?
What employees owe company?
What card transactions are unmatched?
What advances are overdue?
What tax is recoverable?
```

from one workbench/read model.

---

# 654. Product Acceptance — Corporate Cards

Finance can reconcile every cleared transaction into:

```text
Business Expense
Personal
Credit
Unmatched
```

with no unexplained amount.

---

# 655. Product Acceptance — Advances

Finance/employee can explain every unit of advance:

```text
Issued
Spent
Returned
Outstanding
Shortfall Reimbursed
```

---

# 656. Product Acceptance — Policy

An expense approved as exception shows:

```text
which rule
what limit
actual amount
why exception was accepted
who approved
```

---

# 657. Product Acceptance — History

A finalized Expense years later can explain:

```text
employee
merchant
business purpose
category
policy version
receipt
tax
FX
dimensions
approval
accounting
reimbursement/settlement
```

---

# 658. Product Acceptance — No Duplicate Entry

Corporate card transaction does not require retyping:

```text
date
merchant
currency
amount
cardholder
```

if feed already knows them.

---

# 659. Product Acceptance — No Fake Identities

Employee remains Employee.

Merchant need not become Supplier.

---

# 660. Product Acceptance — ERP Integration

Expenses natively uses:

```text
HR
Tax
FinBase
Payments
Ledger
Projects
Files
```

without external synchronization hacks.

---

# 661. Agent Implementation Philosophy

Implement business slices end-to-end.

Do not start with all screens/tables and wire accounting later.

---

# 662. Agent Precedence

If conflict:

```text
21 Expenses pack
→ owning domain pack (HR/Tax/Payments/Ledger/Purchasing/Payroll)
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ Country Pack
→ approved ADR
```

---

# 663. Agent Must Read

At minimum:

```text
01
02
03
04
06
08
09
10
12
15
16
17
18
21
all AGENTS.md
approved ADRs
actual module public contracts
```

---

# 664. Agent Baseline Report

Before coding:

```text
HR Employee contract
Tax expense context support
FinBase FX
Payments employee payout
Payments company receipt/repayment
Ledger semantic fact contract
Platform Files
Approvals
Audit
Jobs
RLS
Project contracts if already present
Bank/Card connector seams if present
```

---

# 665. Agent Must Not Duplicate Missing Dependency

If Payments lacks employee reimbursement source support:

extend public contract through approved architecture.

Do not build mini-Payments inside Expenses.

---

# 666. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL
Real company problem being solved.

REAL-WORLD WORKFLOW

SOURCE OF TRUTH
Owner of Employee, Tax, Payment, Accounting, Project, File.

STATE / INVARIANTS

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

POLICY REQUIREMENTS

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
- contracts used/added
- tests/results
- policy vectors
- accounting/payment reconciliation
- performance evidence
- security evidence
- unresolved decisions
- risks
```

---

# 667. Implementation Sequence

```text
EX00 Repository / Dependency Verification
EX01 Module Boundary + Schema + RLS
EX02 Expense Category Foundation
EX03 Expense Core + State Dimensions
EX04 Employee Self-Service Capture
EX05 Files / Receipt Evidence
EX06 OCR / Receipt Suggestions
EX07 Itemization + Participants
EX08 Policy Engine
EX09 Platform Approval Integration
EX10 Financial Finalization
EX11 Employee Reimbursement Obligation
EX12 Payments Reimbursement Integration
EX13 Reimbursement Batch
EX14 Corporate Card Transaction Inbox
EX15 Card Transaction Matching
EX16 Personal Card Charge + Employee Repayment
EX17 Petty Cash / Company-Paid Sources
EX18 Business Advance Request/Approval
EX19 Advance Payment Issuance
EX20 Advance Application To Expenses
EX21 Advance Return / Shortfall / Settlement
EX22 Mileage
EX23 Per Diem
EX24 Multi-Currency / FX
EX25 Tax / Recoverability
EX26 Dimensions / Projects
EX27 Expense Report Grouping
EX28 Corrections / Reversals
EX29 Payroll Settlement Seam
EX30 Notifications / Reminders
EX31 Reports / Finance Exception Workbench
EX32 Import / Migration
EX33 Security / Privacy Red-Team
EX34 Performance / Scale
EX35 Runbooks / Recovery
EX36 Employee Acceptance
EX37 Finance Acceptance
EX38 Corporate Card Acceptance
EX39 Advance Acceptance
EX40 Tax / Accounting / Payments Reconciliation
EX41 Country Pack Acceptance
EX42 Cross-Module End-to-End Acceptance
EX43 Release Gate
```

---

# 668. EX00 — Goal

Map actual dependencies before implementation.

---

# 669. EX00 Exit Gate

No duplicate Employee/Payment/Tax/Ledger/File capability planned.

---

# 670. EX01 — Goal

Create isolated Expenses module.

---

# 671. EX01 Implement

```text
Todir.Modules.Expenses.Domain
Todir.Modules.Expenses.Application
Todir.Modules.Expenses.Infrastructure
Todir.Expenses.Contracts
business_expenses
ExpensesDbContext
module manifest
RLS
architecture tests
```

---

# 672. EX01 Architecture Tests

Block:

```text
direct HR DbContext
direct Payments DbContext
direct Ledger DbContext
direct Tax DbContext
fake employee supplier
arbitrary policy script
```

---

# 673. EX01 Exit Gate

Module boots secure/isolated.

---

# 674. EX02 — Goal

Define Expense categories semantically.

---

# 675. EX02 Build

```text
Category
Subcategory
Detail Requirement
Accounting Role
Evidence profile
Effective version
```

---

# 676. EX02 Exit Gate

No Catalog Product hack.

---

# 677. EX03 — Goal

Create Expense with correct state dimensions.

---

# 678. EX03 Build

```text
Draft
Submit
Approval state
Finalization state
Payment source
settlement projection
```

---

# 679. EX03 Exit Gate

Approved != Finalized != Reimbursed proven by tests.

---

# 680. EX04 — Goal

Fast employee capture.

---

# 681. EX04 Build

```text
My Expenses
New Expense
HR defaults
policy preview
```

---

# 682. EX04 Exit Gate

Employee enters only facts TODIR cannot safely infer.

---

# 683. EX05 — Goal

Paperless evidence.

---

# 684. EX05 Build

Platform Files integration, secure evidence, receipt requirements.

---

# 685. EX05 Exit Gate

No public file URLs; missing receipt policy works.

---

# 686. EX06 — Goal

Reduce manual data entry.

---

# 687. EX06 Build

Async OCR/suggestions/duplicate hints.

---

# 688. EX06 Exit Gate

OCR failure never blocks manual Expense; AI never finalizes.

---

# 689. EX07 — Goal

Support real receipts.

---

# 690. EX07 Build

```text
Itemization
Participants
personal/business split
```

---

# 691. EX07 Exit Gate

Allocation/itemization totals exact.

---

# 692. EX08 — Goal

Production policy engine.

---

# 693. EX08 Build

```text
versioned typed rules
limits
receipt requirements
findings
exceptions
```

---

# 694. EX08 Exit Gate

Old finalized Expense still explains old policy after new version published.

---

# 695. EX09 — Goal

Optional approvals without complexity.

---

# 696. EX09 Build

Platform Approvals adapter/routing.

---

# 697. EX09 Small Default

One manager/owner or NotRequired.

---

# 698. EX09 Exit Gate

No second generic approval engine.

---

# 699. EX10 — Goal

Turn approved Expense into immutable financial truth.

---

# 700. EX10 Build

```text
Tax snapshot
Policy snapshot
Dimension snapshot
Accounting semantic fact
Audit
Outbox
```

---

# 701. EX10 Exit Gate

Required Ledger posting and Expense finalization consistent.

---

# 702. EX11 — Goal

Company owes Employee correctly.

---

# 703. EX11 Build

Reimbursement obligation/projection.

---

# 704. EX11 Exit Gate

Employee is never converted to Supplier.

---

# 705. EX12 — Goal

Actual employee payment.

---

# 706. EX12 Build

Payments request/status/allocation/idempotency.

---

# 707. EX12 Exit Gate

Failed payment leaves obligation open.

---

# 708. EX13 — Goal

Efficient bulk reimbursement.

---

# 709. EX13 Exit Gate

Batch totals = obligation allocations = Payment sources.

---

# 710. EX14 — Goal

Corporate Card Inbox.

---

# 711. EX14 Build

```text
feed ingestion
pending/cleared
cardholder
merchant
amount
status
```

---

# 712. EX14 Exit Gate

Feed retry creates no duplicate.

---

# 713. EX15 — Goal

Match card spend to business evidence.

---

# 714. EX15 Build

```text
match existing Expense
create Draft
receipt match
allocation
unmatched queue
```

---

# 715. EX15 Exit Gate

Every cleared transaction classifiable with amount reconciliation.

---

# 716. EX16 — Goal

Handle personal corporate-card use correctly.

---

# 717. EX16 Build

Employee repayment obligation + settlement.

---

# 718. EX16 Exit Gate

Personal amount never enters business Expense totals.

---

# 719. EX17 — Goal

Company Cash/Petty Cash sources.

---

# 720. EX17 Exit Gate

Expenses justify spend; Payments/Cashbox owns money.



# 721. EX18 — Goal

Model Business Expense Advances correctly.

---

# 722. EX18 Build

```text
Request
Approval
Purpose
Currency
Needed date
Settlement deadline
```

---

# 723. EX18 Exit Gate

Approved Advance does not imply money issued.

---

# 724. EX19 — Goal

Issue Advance through Payments.

---

# 725. EX19 Build

```text
Payment request
Payment result
Issued amount
failure/retry
```

---

# 726. EX19 Exit Gate

Employee accountability begins only from actual issued amount.

---

# 727. EX20 — Goal

Apply finalized Expenses to Advance.

---

# 728. EX20 Build

```text
Advance application
remaining balance
concurrency
FX support
```

---

# 729. EX20 Exit Gate

Two reports cannot over-apply one Advance.

---

# 730. EX21 — Goal

Complete Advance settlement.

---

# 731. EX21 Build

```text
unused cash return
employee shortfall reimbursement
overdue
write-off controlled
settled state
```

---

# 732. EX21 Exit Gate

Issued amount fully explained by applied/returned/outstanding.

---

# 733. EX22 — Goal

Typed Mileage Expense.

---

# 734. EX22 Build

```text
distance
UOM
round trip
rate version
calculation
policy
```

---

# 735. EX22 Exit Gate

Mileage amount reproducible from distance/rate.

---

# 736. EX23 — Goal

Typed Per Diem.

---

# 737. EX23 Build

```text
trip dates/times
location
rate version
partial days
provided meals
daily lines
```

---

# 738. EX23 Exit Gate

Per diem amount reproducible and no duplicate actual-meal reimbursement where prohibited.

---

# 739. EX24 — Goal

Correct multi-currency.

---

# 740. EX24 Build

Original currency + base/reimbursement/advance settlement FX snapshots through FinBase.

---

# 741. EX24 Exit Gate

No original-currency loss.

---

# 742. EX25 — Goal

Correct tax/recoverability.

---

# 743. EX25 Build

Tax Engine contract + document/evidence requirements.

---

# 744. EX25 Exit Gate

Recoverable VAT reconciles to Tax/Ledger facts.

---

# 745. EX26 — Goal

Allocate cost to correct business dimension.

---

# 746. EX26 Build

```text
Branch
Department
Cost Center
Project
split allocations
defaults
```

---

# 747. EX26 Exit Gate

Historical attribution survives later employee transfer.

---

# 748. EX27 — Goal

Optional Expense Report grouping.

---

# 749. EX27 Build

Report create/group/submit/approve with individual Expense truth intact.

---

# 750. EX27 Exit Gate

Small companies still do individual Expense without report.

---

# 751. EX28 — Goal

Financial correction without history rewrite.

---

# 752. EX28 Build

```text
Reverse
Correct
Linked source
Ledger correction
Reimbursement/repayment adjustment
```

---

# 753. EX28 Exit Gate

Finalized original immutable.

---

# 754. EX29 — Goal

Optional Payroll settlement seam.

---

# 755. EX29 Build

Only if Payroll supports typed non-wage reimbursement/recovery.

---

# 756. EX29 Exit Gate

Expense reimbursement does not contaminate salary/tax bases.

---

# 757. EX30 — Goal

Useful reminders, not noise.

---

# 758. EX30 Build

```text
missing receipt
approval pending
advance due
reimbursement result
employee repayment due
```

---

# 759. EX30 Exit Gate

Notification failure never corrupts Expense truth.

---

# 760. EX31 — Goal

Finance works by exceptions.

---

# 761. EX31 Build

Finance Workbench + operational reports/reconciliations.

---

# 762. EX31 Exit Gate

Finance can identify all unresolved spend without spreadsheets.

---

# 763. EX32 — Goal

Safe migration from Excel/legacy.

---

# 764. EX32 Build

```text
categories
open Expenses
outstanding reimbursements
advances
employee repayments
card transaction refs
evidence
```

---

# 765. EX32 Exit Gate

Opening employee/company balances reconcile.

---

# 766. EX33 — Goal

Security/privacy hardening.

---

# 767. EX33 Attack Cases

```text
Cross-tenant expense read
Self-service IDOR
Manager reads unrelated team
Forged EmployeeId
Forged ProjectId
Forged FileRef
Unauthorized reimbursement
Unauthorized Advance issuance
Unauthorized write-off
Card data leakage
Receipt URL leakage
Export leakage
```

---

# 768. EX33 Exit Gate

RLS + app auth + Files auth all pass.

---

# 769. EX34 — Goal

Prove scale.

---

# 770. EX34 Load Tests

```text
1M Expenses
1M Card Transactions
100k employees
large Finance Inbox
bulk reimbursement
receipt/OCR burst
policy evaluation burst
advance aging
large export
```

---

# 771. EX34 Query Plan Review

Real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot queries.

---

# 772. EX34 Exit Gate

Measured targets pass or evidence-based ADR.

---

# 773. EX35 — Goal

Recover operational failures without data corruption.

---

# 774. EX35 Runbooks

Required:

```text
OCR failed
Receipt inaccessible
Duplicate suspected
Policy misconfiguration
Approval stuck
Accounting failed
Reimbursement failed/pending
Card feed duplicate
Card match wrong before finalization
Personal card classification correction
Advance payment failed
Advance application mismatch
Advance overdue
Employee repayment failed
FX rate missing
Tax validation failed
Projection rebuild
DR restore validation
```

---

# 775. EX35 Forbidden Runbook

Never normal support fix:

```text
UPDATE expense SET reimbursed = true
UPDATE business_advance SET balance = ...
DELETE finalized expense
UPDATE employee_debt = ...
```

---

# 776. EX35 Exit Gate

Critical operational failures have semantic recovery.

---

# 777. EX36 — Employee Acceptance Test

Use employee not involved in build.

---

# 778. EX36 Scenario A — Personal Funds

Employee:

```text
takes receipt photo
creates expense
sees policy
submits
tracks approval
tracks reimbursement
```

without finance help.

---

# 779. EX36 Scenario B — Missing Receipt

Employee understands exactly:

```text
why receipt is required
whether declaration is allowed
what next step is
```

---

# 780. EX36 Scenario C — Corporate Card

Employee sees transaction and only supplies missing:

```text
receipt
purpose
category/project if needed
```

not card facts TODIR knows.

---

# 781. EX36 Scenario D — Advance

Employee sees:

```text
Issued
Applied
Remaining
Due
```

and settles correctly.

---

# 782. EX36 Pass Criteria

```text
minimal data entry
clear status
clear policy
no accounting terminology
reimbursement status trustworthy
```

---

# 783. EX37 — Finance Acceptance Test

Use Expense/Finance controller.

---

# 784. EX37 Scenario A — Review

Finance finds:

```text
policy exception
receipt
tax
dimensions
payment source
```

quickly.

---

# 785. EX37 Scenario B — Employee Reimbursement

Finance finalizes and reimburses without creating fake supplier.

---

# 786. EX37 Scenario C — Personal Card Charge

Finance marks card transaction Personal and tracks Employee Repayment.

---

# 787. EX37 Scenario D — Card Inbox

Finance reconciles mixed transaction set:

```text
business
personal
credit
unmatched
```

---

# 788. EX37 Scenario E — Advance

Finance settles:

```text
under-spend
over-spend
exact spend
```

correctly.

---

# 789. EX37 Pass Criteria

No spreadsheet/manual journal needed for normal workflows.

---

# 790. EX38 — Corporate Card Acceptance

Test provider/feed adapter with:

```text
pending → cleared
duplicate webhook/import
merchant normalization
receipt match
personal charge
credit/refund
split allocation
unmatched transaction
```

---

# 791. EX38 Exit Gate

Every cleared amount classified exactly once or explicitly Unmatched.

---

# 792. EX39 — Business Advance Acceptance

Test:

```text
Request
Approve
Issue
Apply expenses
Return excess
Reimburse shortfall
Multi-currency
Overdue
Termination
```

---

# 793. EX39 Exit Gate

No unexplained Advance balance.

---

# 794. EX40 — Financial Reconciliation Acceptance

Finance validates:

```text
Finalized Expenses
=
Ledger expense/tax/control facts

Reimbursements
=
Payments allocations

Employee Repayments
=
Payments/company receipts

Advances
=
Issued - applied - returned - outstanding
```

---

# 795. EX40 Exit Gate

Zero unexplained difference.

---

# 796. EX41 — Country Pack Acceptance

Country/tax specialist validates:

```text
receipt/tax invoice requirements
VAT recoverability
mileage/per diem statutory templates if enabled
retention
reimbursement constraints
```

---

# 797. EX41 Exit Gate

No country-specific production mode without verified provider rules/source/version.

---

# 798. EX42 — Cross-Module E2E Acceptance

---

# 799. EX42 Scenario — Employee Expense

```text
HR Employee
→ Expense
→ Tax
→ Approval
→ Ledger
→ Reimbursement
→ Payments
```

---

# 800. EX42 Scenario — Corporate Card

```text
Card Transaction
→ Expense
→ Evidence
→ Policy
→ Tax
→ Ledger
```

no employee reimbursement.

---

# 801. EX42 Scenario — Personal Card Charge

```text
Company Card
→ Personal
→ Employee Repayment
→ Payment back
→ Ledger
```

---

# 802. EX42 Scenario — Advance

```text
Advance
→ Payment to Employee
→ Expenses
→ Settlement
→ Return or Reimbursement
→ Ledger
```

---

# 803. EX42 Scenario — Project Expense

```text
Employee
→ Expense
→ Project dimension
→ Ledger
```

with project source integrity.

---

# 804. EX42 Scenario — Payroll Settlement Optional

If enabled:

```text
Expense Reimbursement
→ typed Payroll input
→ Payslip/settlement
→ Expenses marked settled from actual result
```

without salary-base corruption.

---

# 805. EX42 Exit Gate

All cross-module invariants pass on real PostgreSQL integration tests.

---

# 806. EX43 — Release Gate

Expenses does not ship because Expense form exists.

It ships when company spend can be trusted end-to-end.

---

# 807. EX43 Required Sign-Off

```text
Product Architect
Finance/Expense Controller
Accounting Lead
Tax Specialist
HR Lead
Payments Lead
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
Country Specialist for enabled market
```

---

# 808. EX43 Mandatory Evidence

```text
Automated tests
RLS/security tests
Policy golden vectors
Tax vectors
Reimbursement reconciliation
Employee repayment reconciliation
Advance reconciliation
Card matching reconciliation
Ledger reconciliation
Performance benchmarks
Employee acceptance
Finance acceptance
Migration opening balances
Known limitations
```

---

# 809. Build Now — MUST

```text
Expense Core
Categories
Evidence/Receipts
Itemization
Participants
Policy Engine
Approvals
Financial Finalization
Employee Reimbursements
Reimbursement Batch
Corporate Card Inbox
Card Matching
Personal Card Charges
Employee Repayments
Petty Cash source
Business Advances
Advance Settlement
Mileage
Per Diem
Multi-Currency
Tax
Dimensions/Project refs
Expense Reports optional
Corrections
Permissions
Audit
Reports
Migration
Performance
Runbooks
Acceptance gates
```

---

# 810. Build Later / Evidence-Based

```text
Full Travel Booking
Travel Agency Integration
Corporate Card Issuance/Limit Management
Advanced Merchant Network Controls
Complex Global Per Diem Libraries
Automated Government Expense Filings
Customer Rebilling Automation
Advanced Fraud ML
Carbon Tracking
Complex Fleet Expense
AI Expense Agent with action authority
```

---

# 811. Fresh Small Company Defaults

Recommended:

```text
Individual Expense           ON
Expense Reports              Optional/OFF
One simple approval          ON or owner-direct
Receipts                     category/threshold based
OCR                          optional
Corporate Card Inbox         OFF until connected
Business Advances            ON simple
Mileage                      ON if needed
Per Diem                     OFF until configured
Projects                     hidden if inactive
Reimbursement                direct Payments
Payroll reimbursement        OFF
Advanced policy rules        OFF
```

---

# 812. Larger Company Capabilities

Enable:

```text
Expense Reports
Multi-stage approvals
Corporate Cards
Card Inbox
Advanced policies
Department/project limits
Business Advances
Mileage
Per Diem
Missing Receipt Declaration
Tax recoverability controls
Reimbursement schedules
Employee repayment
Advanced reports
```

---

# 813. Setup Wizard

Recommended:

```text
1. Legal Entity
2. Create/select Expense Categories
3. Configure receipt requirements
4. Configure simple policy/limits
5. Configure approval mode
6. Configure reimbursement payment method
7. Enable Business Advances if used
8. Enable Mileage/Per Diem if used
9. Connect Corporate Card feed if used
10. Validate Tax/Accounting mappings
11. Go Live
```

---

# 814. Setup Health

Show:

```text
Categories              Ready
Policy                  Ready
Approvals               Ready
Tax                     Ready
Accounting              Ready
Payments/Reimbursement  Ready
Corporate Cards         Not Connected / Ready
Mileage                 Disabled / Ready
Per Diem                Disabled / Ready
```

---

# 815. No Irrelevant Setup Blockers

Company without cards:

```text
no card setup required
```

Company without per diem:

```text
no rate setup required
```

---

# 816. Final Employee-Paid Flow

```text
Expense
→ Evidence
→ Policy
→ Approval
→ Finalize
→ Employee Reimbursement Obligation
→ Payment
```

---

# 817. Final Company-Card Flow

```text
Card Transaction
→ Draft/Match Expense
→ Evidence
→ Policy
→ Approval/Finalize
→ Accounting
```

---

# 818. Final Personal Card Flow

```text
Company Card Transaction
→ Personal Classification
→ Employee Repayment Obligation
→ Employee Pays Company
```

No business Expense.

---

# 819. Final Advance Flow

```text
Request
→ Approve
→ Issue Payment
→ Employee Accountable
→ Apply Finalized Expenses
→ Return Excess / Reimburse Shortfall
→ Settled
```

---

# 820. Final Mileage Flow

```text
Distance
×
Effective Versioned Rate
=
Expense
```

---

# 821. Final Per Diem Flow

```text
Travel location/duration
→ Rate version
→ Daily eligibility
→ Meal/partial-day reductions
→ Expense
```

---

# 822. Final Accounting Flow

```text
Finalized Expense
→ Semantic Accounting Fact
→ Ledger
```

No manual GL IDs.

---

# 823. Final Payment Flow

```text
Expense determines obligation
Payments proves settlement
```

---

# 824. Final Policy Rule

> **Policy findings explain and control spend; they never silently rewrite what the employee actually spent.**

---

# 825. Final Evidence Rule

> **A missing receipt is a policy/evidence state, not permission to invent evidence and not universally a reason to reject the business expense.**

---

# 826. Final Card Rule

> **A corporate-card transaction is a money-source fact that must be classified and evidenced; it is not automatically a business Expense.**

---

# 827. Final Employee Rule

> **Employees remain Employees. TODIR does not create fake Suppliers merely to reimburse them.**

---

# 828. Final Advance Rule

> **Business Advance balances are reconstructed from issuance, applied Expenses, returns and settlements; they are never a manually editable number.**

---

# 829. Final Repayment Rule

> **When the employee owes the company, TODIR creates an explicit employee repayment obligation instead of disguising it as a negative Expense.**

---

# 830. Final Tax Rule

> **Expense tax and VAT recoverability come from Tax/Country rules and documentary context; Expenses does not guess tax treatment.**

---

# 831. Final FX Rule

> **TODIR preserves original transaction currency and every material FX snapshot; base-currency conversion never erases what was actually spent.**

---

# 832. Final Procurement Rule

> **Expenses handles spend claims/incidental spend; formal supplier procurement/AP remains Purchasing + Invoicing.**

---

# 833. Final Simplicity Rule

> **A small company must be able to submit, approve and reimburse a normal Expense in a few actions without configuring enterprise spend-management machinery.**

---

# 834. Final Enterprise Rule

> **A larger company must be able to add corporate cards, policies, cash advances, multi-stage controls and reconciliation without replacing the Expenses core.**

---

# 835. Final Product Verdict

TODIR Expenses is release-ready only when:

```text
Employee-paid and company-paid spend are distinct.
Approval never pretends payment happened.
Corporate-card transactions are reconciled, not blindly auto-expensed.
Personal company-card spend becomes employee repayment, not business expense.
Employee reimbursement works without fake supplier identity.
Business Advances preserve full issuance/application/return history.
Advance under/over-spend resolves correctly.
Mileage preserves distance/rate/version.
Per Diem preserves daily/rate/reduction logic.
Receipts/evidence are secure and searchable.
Missing-receipt behavior is policy-driven.
Policy versions remain historically reproducible.
Itemizations reconcile exactly to the expense.
Tax/recoverability comes from Tax.
Original currency is never lost.
Dimensions preserve historical cost attribution.
Finalized Expenses are immutable.
Corrections do not rewrite history.
Reimbursements and repayments settle through Payments.
Accounting uses semantic facts and reconciles to Ledger.
Small-company workflow stays simple.
Enterprise controls remain optional.
Employee and Finance acceptance tests pass.
Cross-module reconciliation shows zero unexplained difference.
```

---

# 836. Final Agent Rule

For every Expenses implementation decision, the Agent must answer:

> **Who actually paid, what business cost occurred, what evidence proves it, which policy version governs it, who owes whom after finalization, which domain owns the actual money/accounting truth, and how will the result still be explainable years later?**

If any answer is unclear, the implementation is incomplete.

---

# 837. No Separate Expenses Subfeature Packs

Do **not** create separate architecture packs for:

```text
Expense Reports
Corporate Cards
Employee Reimbursements
Business Advances
Mileage
Per Diem
Employee Repayments
Receipt OCR
Expense Policies
```

They are all part of `21 Expenses`.

---

# 838. Next Planning Rule

Continue one comprehensive file per actual business module.

Choose the next module based on product/MVP priority, while preserving already-approved ownership boundaries.

---

**END OF FILE — 21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
