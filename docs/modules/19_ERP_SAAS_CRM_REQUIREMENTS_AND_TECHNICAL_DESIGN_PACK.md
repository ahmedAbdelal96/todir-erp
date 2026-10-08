# 19 — TODIR ERP SaaS CRM / Leads / Opportunities / Sales Pipeline Requirements & Technical Design Pack

**File:** `19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + CRM + LEAD MANAGEMENT + OPPORTUNITY PIPELINE + SELLER WORKSPACE + COMMERCIAL INTELLIGENCE + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-06  
**Audience:** Product Owner, Product Architect, Sales Manager, Sales Operations, CRM Lead, Marketing/Lead-Generation Owner, Backend/Frontend Leads, Data Architect, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **HIGH / REVENUE-CRITICAL** — A CRM that becomes data-entry overhead will not be used; a CRM that contains stale pipeline data will mislead management.

---

## Canonical dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

CRM consumes Directory/Sales/finance truth; it never duplicates their canonical records.

---

# 0. Executive Decision

TODIR CRM owns the **pre-sales commercial relationship and pipeline workflow**.

It answers:

```text
Who just showed interest?
Where did the lead come from?
Has anyone contacted them?
Who owns the lead?
Is this lead worth pursuing?
What are they trying to buy?
Did it become a real opportunity?
Who are the decision makers?
What is the opportunity worth?
What stage is it in?
What is the next action?
Is the deal healthy or stalled?
When is it expected to close?
Why did we win or lose?
Did the opportunity become a real quotation/sale?
```

The central product rule is:

> **TODIR CRM must give a salesperson more value than the time it asks them to spend entering data.**

---

# 1. CRM Commercial Truth Boundary

```text
Lead
= early prospect not yet formal master-data/customer truth

Opportunity
= active commercial possibility being pursued

Directory Party
= canonical person/company identity once qualified/needed

Sales Quotation / Sales Order
= commercial offer/commitment owned by Sales

Invoice
= billing truth owned by Invoicing

Receivable
= financial customer balance owned by Subledger

Payment
= money movement owned by Payments
```

---

# 2. Three Absolute Distinctions

```text
Lead != Customer Master
Opportunity != Sale
Pipeline Value != Revenue
```

These rules are non-negotiable.

---

# 3. Why CRM Exists

Without CRM, sales work becomes fragmented across:

```text
WhatsApp
phone calls
spreadsheets
notebooks
email
salesperson memory
personal calendars
random contact lists
```

and management cannot reliably answer:

```text
What are we likely to sell?
Which deals are real?
Which leads are being ignored?
Why are we losing?
What should the team do next?
```

---

# 4. Product Mission

TODIR CRM must make two groups successful:

```text
Salesperson
→ know what to do next with minimal admin

Sales Manager
→ see realistic pipeline, risks and forecast
```

---

# 5. Seller Experience Principle

The seller's primary screen is not a chart dashboard.

It is an actionable worklist:

```text
My Sales Today

Follow-ups Today
Overdue Follow-ups
New Leads
Deals Needing Attention

Priority
- Call ABC today
- Quote for XYZ expires tomorrow
- New WhatsApp lead not contacted
- Opportunity has no next action
```

---

# 6. Manager Experience Principle

Sales manager needs:

```text
Pipeline
Risk
Forecast
Lead response
Conversion
Win/Loss
Stale deals
Salesperson performance
Activity coverage
```

not raw activity noise.

---

# 7. Owner Experience Principle

Owner needs high-level commercial outlook:

```text
Open Pipeline
Expected/Weighted Pipeline
Likely This Period
Won This Period
Lost This Period
Top Risks
```

and must never confuse this with Accounting revenue/cash.

---

# 8. Modern CRM Benchmark Principles

TODIR should incorporate useful patterns seen across modern CRM systems:

```text
separate unqualified lead inbox
seller prioritized worklist
next-action orientation
custom pipelines
deal health / risk
stale-deal detection
predictive/AI scoring as assistance
relationship intelligence
activity timelines
sequences/cadences
forecast categories
pipeline inspection
automation with progressive disclosure
```

TODIR must not copy competitor complexity blindly.

---

# 9. Competitive Differentiation Goal

A traditional CRM often becomes:

```text
database for management
+
data-entry burden for salesperson
```

TODIR must instead be:

```text
daily sales assistant
+
credible management pipeline
+
direct bridge into the ERP transaction flow
```

---

# 10. One CRM File

This pack contains the complete CRM business module:

```text
Lead Inbox
Lead Sources
Lead Capture
Lead Qualification
Lead Assignment
Lead Deduplication
Lead Conversion

Accounts/Contacts composition
Buying Committee
Opportunities
Pipelines
Stages
Stage History
Deal Value
Expected Close
Won/Lost
Loss Reasons

Activities
Calls
Meetings
Tasks
Next Action
Timeline
Files/Notes

Deal Health
Stale/Rotted Deal Detection
Pipeline Hygiene
Forecast
Targets/Quotas foundation

Sales Ownership
CRM Teams
Territory seam

Customer 360
CRM → Directory
CRM → Sales Quotation
Sales/Invoice/AR/Payment summaries

Sequences/Cadences
Automations
External Lead Capture
Email/WhatsApp/Phone integration seams

Reports
Permissions
Audit
RLS
Performance
AI seams
Import/Migration
Tests
Implementation Plan
```

Do not split Lead, Opportunity, Activity, Forecast or CRM Automation into separate design packs.

---

# 11. CRM Owns

CRM owns:

```text
Lead
Lead Source
Lead Qualification State
Lead Assignment
Lead Conversion Record

Opportunity
Opportunity Pipeline
Opportunity Stage
Stage History
Opportunity Commercial Value estimate
Expected Close Date
Forecast Category
Win/Loss commercial outcome
Loss Reason

CRM Activity
CRM Next Action
CRM Activity Outcome

Deal Health
Pipeline Hygiene State
CRM Sequence/Cadence
CRM Automation rules limited to CRM

CRM Team / pre-sales routing context
CRM Reports/Read Models
```

---

# 12. CRM Does NOT Own

```text
Canonical customer/company identity   → Directory
Canonical contact identity            → Directory
Customer role                         → Directory
Product identity                      → Catalog
Reusable pricing rules                → Pricing
Quotation / Sales Order               → Sales
Invoice                               → Invoicing
Receivable / Outstanding              → Subledger
Payment                               → Payments
Revenue                               → Ledger/Accounting
Email provider/mailbox                → Integration provider
WhatsApp transport                    → Integration provider
Marketing campaign engine             → future Marketing capability
Customer support tickets              → future Support/Helpdesk
```

---

# 13. Directory Alignment

Locked Directory rule:

```text
CRM Lead != Party by default
```

A low-quality/incomplete/scraped lead must not pollute canonical master data.

---

# 14. Lead Conversion Alignment

Correct conversion:

```text
CRM Lead Qualified
→ Search Directory
→ Existing Party match?
    Yes → Link
    No  → Create Party
→ Add Customer Role when needed
→ Create/Link Opportunity
```

No duplicate customer identity.

---

# 15. CRM Can Link Existing Party Early

If a known customer creates a new opportunity:

```text
Opportunity
→ existing Party
```

No Lead required.

---

# 16. Lead Can Exist Without Party

Minimum early record can be:

```text
Name
Phone or Email
Source
Owner
Interest
```

depending source.

---

# 17. Lead Is Temporary Commercial Identity

Lead is not allowed to become a second permanent customer master.

Once converted:

```text
Lead retains historical capture/source context
Party becomes canonical identity
Opportunity references Party
```

---

# 18. Lead Inbox

Unqualified leads belong in a dedicated Lead Inbox.

They should not automatically clutter active opportunity pipeline.

---

# 19. Why Lead Inbox

Lead sources can create:

```text
low-quality
duplicate
incomplete
spam
early-interest
not-ready
```

records.

A separate inbox protects pipeline credibility.

---

# 20. Lead Inbox Default View

```text
New
Uncontacted
In Progress
Waiting Reply
Qualified
Not Fit
Duplicate
Archived
```

Use simple terminology.

---

# 21. Lead Lifecycle

Recommended:

```text
New
Working
Qualified
Disqualified
Converted
Archived
```

Additional communication state belongs activities, not dozens of statuses.

---

# 22. Qualification Is Not Mandatory Enterprise BANT

Small company can qualify with:

```text
Interested?
Valid contact?
Potential fit?
```

Large company may configure richer questions.

---

# 23. Qualification Profile

Optional structured fields:

```text
Need
Product/Service Interest
Approximate Budget
Expected Timing
Decision Role
Company Size/Segment
Geography
Fit Notes
```

Do not require all.

---

# 24. Qualification Configuration

Each pipeline/business type can configure:

```text
Required To Qualify
Recommended
Hidden
```

through metadata/config, not code forks.

---

# 25. Lead Minimal Create

Target UX:

```text
Name
Phone / Email
Source
Owner?
[Save]
```

Everything else progressive.

---

# 26. Lead Source

Canonical CRM definition:

```text
Referral
Website
Facebook
Instagram
WhatsApp
Phone
Email
WalkIn
Campaign
Marketplace
Import
API
Partner
Other
```

Configurable.

---

# 27. Source vs Channel

Distinguish:

```text
Lead Source
= how lead originated

Communication Channel
= how interaction occurred
```

A Facebook lead may later be contacted by phone.

---

# 28. Campaign Attribution Seam

CRM can store:

```text
CampaignRef
UTM source/medium/campaign
External Campaign ID
```

without becoming full Marketing Automation.

---

# 29. First Touch / Latest Touch

Preserve source attribution metadata where available.

Do not silently overwrite original acquisition source.

---

# 30. Lead Capture Sources

Core adapters/seams:

```text
Manual
Excel/CSV
Website form
API
Email inbound
WhatsApp
Meta lead sources
Other provider webhook
```

Actual provider integrations are prioritized separately.

---

# 31. External Lead Inbox

External webhook/API enters:

```text
Raw External Lead
→ Normalize
→ Idempotency
→ Duplicate Check
→ CRM Lead
```

---

# 32. Raw External Lead Evidence

Store bounded provider payload/reference where useful for support.

Do not make provider JSON the canonical Lead model.

---

# 33. External Source Idempotency

Unique:

```text
Tenant
SourceSystem
ExternalLeadId
```

where provider gives stable ID.

Retries cannot duplicate Lead.

---

# 34. Lead Deduplication

Candidate signals:

```text
Normalized phone
Normalized email
Existing Lead
Existing Directory Party/Contact
Company + person context
External source identity
```

---

# 35. Duplicate Detection Is Suggestive

Default:

```text
Possible Duplicate
```

not automatic merge.

---

# 36. Exact Duplicate

If same external source/event id:

idempotent creation returns same Lead.

---

# 37. Lead Merge

Authorized CRM user can merge duplicate Leads.

Preserve:

```text
source history
activities
notes
external refs
owner history
```

---

# 38. Lead Merge Does Not Merge Directory Automatically

Directory merge follows Directory's own controlled process.

---

# 39. Lead Assignment

Default:

```text
Manual
```

Simple companies need nothing else.

---

# 40. Advanced Assignment

Optional:

```text
Round Robin
By Branch
By Region
By Product Interest
By Lead Source
By Sales Team
By Capacity
```

---

# 41. Assignment Rule Must Be Deterministic

Same configured conditions resolve predictably.

No random hidden reassignment except explicit round-robin algorithm.

---

# 42. Assignment History

Store:

```text
Previous Owner
New Owner
Reason
Rule/User
OccurredAt
```

---

# 43. Lead Ownership

Primary Owner references:

```text
IAM User/Membership
```

optionally linked to HR Employee for reporting.

---

# 44. Salesperson Is Not New Identity

Do not create CRM-only salesperson person table.

Use authorized user/employee references.

---

# 45. CRM Team Ownership Decision

CRM owns the **pre-sales CRM team definition and routing membership**.

Sales continues to own the salesperson/team assignment recorded on its own Quotation/Order documents.

When CRM converts an Opportunity to Sales:

```text
CRM owner/team
→ passed as commercial ownership context
→ Sales records its own assignment snapshot/reference
```

No Sales document lifecycle becomes CRM-owned.

---

# 46. Team Definition

Concept:

```text
CrmSalesTeam
{
    Id
    TenantId
    LegalEntityId?
    Name
    ManagerUserId?
    Status
}
```

---

# 47. Team Membership

Effective-dated if used for reporting/routing:

```text
CrmSalesTeamMember
{
    TeamId
    UserId
    EffectiveFrom
    EffectiveTo?
}
```

---

# 48. Team Is Optional

One salesperson/company requires no team setup.

---

# 49. Lead Response SLA

Optional but valuable.

Examples:

```text
New web lead should be contacted within 30 minutes
Referral within 1 business day
```

---

# 50. SLA Is Not Universal

Configured by:

```text
Source
Priority
Pipeline
Business hours
```

if enabled.

---

# 51. First Response Measurement

Capture first meaningful outbound/inbound contact event.

Do not fake from lead open/view.

---

# 52. Unattended Lead

Lead with no meaningful follow-up past threshold.

Surface as exception.

---

# 53. Lead Priority

Simple:

```text
Low
Normal
High
Urgent
```

optional.

---

# 54. Lead Score

Can have:

```text
Rule-Based Score
Predictive/AI Score later
```

Never let opaque score replace qualification facts.

---

# 55. Rule-Based Score

Typed factors:

```text
source quality
company segment
product interest
response/engagement
budget/timing completeness
```

Versioned.

---

# 56. Predictive Score

Future AI capability.

Must expose:

```text
score
confidence
major factors
model version
```

when available.

---

# 57. Lead Score Is Advice

It does not automatically disqualify or convert by default.

---

# 58. Lead Conversion

Qualified Lead can become:

```text
Directory Party/Contact
Opportunity
or both
```

through one UX action.

---

# 59. Convert Lead UX

```text
[Qualify & Create Opportunity]
```

System:

```text
Search Directory
Deduplicate
Create/link Party
Add Customer Role if needed
Create Opportunity
Link Lead history
```

---

# 60. Conversion Idempotency

Double click/retry cannot create duplicate Party/Opportunity.

---

# 61. Disqualified Lead

Store reason:

```text
No Fit
No Budget
Invalid Contact
Duplicate
Spam
Wrong Market
No Interest
Timing
Other
```

configurable.

---

# 62. Disqualified Lead Can Be Reopened

Controlled, history preserved.

---

# 63. Lead Archive

Archive old/no-longer-active lead without deletion.

---

# 64. Opportunity Is CRM Core

Opportunity represents a real commercial possibility.

---

# 65. Opportunity Can Be Created Directly

No Lead required for:

```text
existing customer
referral already qualified
renewal
upsell
walk-in ready to buy
```

---

# 66. Opportunity Header

Concept:

```text
Opportunity
{
    Id
    TenantId
    LegalEntityId?

    PipelineId
    StageId

    Title
    PartyId?
    PrimaryContactId?

    OwnerUserId
    TeamId?

    EstimatedValue
    CurrencyCode
    ExpectedCloseDate?

    ForecastCategory
    ProbabilitySource
    Probability?

    HealthState
    Priority?

    LeadId?
    SourceId?
    CampaignRef?

    WonAt?
    LostAt?
    LostReasonId?

    RowVersion
}
```

---

# 67. Party Requirement

Opportunity may initially have no Party if still early.

Before creating formal Sales Quotation:

```text
Party(Customer Role)
```

must exist through Directory conversion.

---

# 68. Opportunity Currency

One estimated commercial currency.

Forecast aggregation across currencies must use explicit approved reporting FX context.

Do not simply add unlike currencies.

---

# 69. Estimated Value

Opportunity value is estimate.

It is not:

```text
Quotation total
Invoice total
Revenue
```

unless explicitly synchronized/reconciled.

---

# 70. Opportunity Products / Interests

Optional lightweight interest lines:

```text
Product Variant?
Category?
Description
Estimated Qty?
Estimated Value?
```

This is pipeline context, not Quotation pricing truth.

---

# 71. Do Not Duplicate Quotation Lines

Once formal quote exists:

Sales Quotation owns exact:

```text
qty
price
discount
tax
terms
```

CRM can summarize/link.

---

# 72. Multiple Quotations Per Opportunity

Valid.

CRM displays Sales-owned quotations.

---

# 73. Opportunity Pipeline

Pipeline is configurable sequence of commercial stages.

---

# 74. Multiple Pipelines

Support:

```text
New Business
Renewal
Enterprise
Retail/B2B
Different product motions
```

only if company needs them.

Small company uses one default.

---

# 75. Pipeline Definition

Concept:

```text
CrmPipeline
{
    Id
    LegalEntityId?
    Name
    Purpose
    DefaultCurrency?
    Status
}
```

---

# 76. Stage Definition

Concept:

```text
CrmPipelineStage
{
    Id
    PipelineId
    Name
    Sequence
    DefaultProbability?
    StaleAfterDuration?
    ForecastDefault?
    IsWon?
    IsLost?
    Status
}
```

---

# 77. Stage Names Are Business-Configurable

Example:

```text
New
Qualified
Discovery
Proposal
Negotiation
Won
Lost
```

Do not hardcode one methodology.

---

# 78. Stage Must Not Become Workflow Prison

Default stage move is simple.

Optional validation can require critical fields before important transitions.

---

# 79. Stage Transition

Command:

```text
MoveOpportunityStage
```

records history.

---

# 80. Stage History

Permanent:

```text
FromStage
ToStage
MovedAt
MovedBy
TimeInPriorStage
Reason?
```


---

# 81. Stage Age

Derived:

```text
Now - EnteredStageAt
```

or historical equivalent.

Used for health/staleness.

---

# 82. Stage Probability

Can be:

```text
Pipeline default
Manual override
Rule-based
Predictive/AI suggestion later
```

---

# 83. Probability Is Not Truth

It is forecasting metadata.

Do not use it for financial accounting.

---

# 84. Forecast Category

Recommended baseline:

```text
Open
Pipeline
BestCase
Commit
ClosedWon
Omitted
```

Simpler tenants can hide categories.

---

# 85. Forecast Category Is Separate From Stage

A Negotiation opportunity may be:

```text
BestCase
or
Commit
```

depending seller/manager judgment.

---

# 86. Forecast Category History

Store material changes for forecast review.

---

# 87. Expected Close Date

Seller estimate.

Must be updated if clearly overdue.

---

# 88. Overdue Expected Close

Opportunity whose expected close date passed while still open becomes hygiene exception.

---

# 89. Opportunity Value Source

Value can be:

```text
Manual estimate
Opportunity interests estimate
Latest quotation summary
Selected quotation summary
```

Source must be visible.

---

# 90. Quotation-Synced Value

If enabled, CRM can display:

```text
Opportunity Estimate       500,000
Latest Quote               535,000
```

Do not silently overwrite estimate without policy.

---

# 91. Probability-Weighted Pipeline

Concept:

```text
Weighted Value
=
Estimated Value × Forecast Probability
```

Used for sales planning only.

---

# 92. Cross-Currency Forecast

Requires reporting currency and approved FX snapshot/read service.

Show both:

```text
Original Opportunity Currency
Reporting Currency Equivalent
```

---

# 93. Won Opportunity

Won means:

```text
commercially won according to CRM definition
```

It does not automatically mean:

```text
Invoice issued
Payment received
Revenue recognized
```

---

# 94. Won Criteria

Company/pipeline may require:

```text
Accepted Quotation
Signed agreement
Purchase order from customer
Verbal confirmation
Other approved evidence
```

Do not force one globally.

---

# 95. Won Evidence

Optional:

```text
SalesQuotationRef
ContractRef
CustomerPORef
Note/FileRef
```

---

# 96. Mark Won UX

Simple:

```text
[Mark Won]
```

If no Sales document exists, system can offer:

```text
Create Quotation
Create Sales Order where enabled
```

without forcing it.

---

# 97. Lost Opportunity

Requires selected reason for useful analytics.

---

# 98. Loss Reason

Configurable:

```text
Price
No Budget
No Decision
Competitor
Timing
Product Gap
Lost Contact
Internal Priority Change
Customer Cancelled
Other
```

---

# 99. Competitor Field

Optional structured competitor reference/name.

Do not require full Competitor module Day 1.

---

# 100. Lost Notes

Reason + optional note.

Do not make unstructured note the only analytics source.

---

# 101. Reopen Lost Opportunity

Controlled.

History preserves previous loss outcome/reason.

---

# 102. Reopen Won Opportunity

Rare/high-risk.

Normally create a new Opportunity for new commercial motion.

Do not casually reverse a genuine historical Won result.

---

# 103. Renewal Opportunity

Can link to:

```text
original customer
prior Sale/Contract/Subscription reference
```

without copying prior commercial truth.

---

# 104. Upsell / Cross-Sell

Opportunity Type:

```text
NewBusiness
Renewal
Upsell
CrossSell
WinBack
Other
```

optional.

---

# 105. Opportunity Contact Roles

B2B deals need more than one contact.

---

# 106. Buying Committee

CRM can classify Directory Contacts:

```text
DecisionMaker
Champion
Influencer
TechnicalContact
FinanceContact
PurchasingContact
LegalContact
EndUser
Other
```

---

# 107. Contact Identity Remains Directory-Owned

CRM stores relationship role to Opportunity, not duplicate name/email master.

---

# 108. Opportunity Contact Role

Concept:

```text
OpportunityContactRole
{
    OpportunityId
    ContactId
    Role
    IsPrimary
    Notes?
}
```

---

# 109. Buying Committee Completeness

Optional health signal:

```text
No decision maker identified
No finance contact
Single-threaded relationship
```

for larger B2B pipelines.

---

# 110. Relationship Intelligence Foundation

CRM should capture enough structured interaction metadata to later derive:

```text
relationship recency
interaction frequency
stakeholder coverage
response patterns
engagement trend
```

without requiring AI Day 1.

---

# 111. Relationship Health Is Not Credit Risk

Keep separate from Subledger/Credit.

---

# 112. CRM Activity

Activities represent seller actions/communications.

Examples:

```text
Call
Meeting
Task
Demo
Visit
Email Follow-up
WhatsApp Follow-up
Proposal Follow-up
Deadline
Custom Activity Type
```

---

# 113. Activity Is Not Generic Audit

Audit records system changes.

Activity records commercial work.

---

# 114. Activity Model

Concept:

```text
CrmActivity
{
    Id
    TenantId

    ActivityTypeId
    Subject

    LeadId?
    OpportunityId?
    PartyId?
    ContactId?

    OwnerUserId

    DueAt?
    StartAt?
    EndAt?
    TimeZone?

    State
    Priority?

    OutcomeId?
    CompletedAt?
    Notes?

    ExternalCalendarRef?
    CommunicationRef?

    RowVersion
}
```

---

# 115. Activity States

Keep simple:

```text
Planned
Completed
Cancelled
```

Overdue is derived.

---

# 116. Activity Outcome

Structured where useful:

```text
Reached
NoAnswer
MeetingCompleted
Interested
FollowUpNeeded
NotInterested
Rescheduled
Other
```

configurable by Activity Type.

---

# 117. Next Action

Every active Opportunity should ideally answer:

```text
What is the next planned action?
When?
Who owns it?
```

---

# 118. No Next Action

Configurable pipeline hygiene warning.

---

# 119. Multiple Future Activities

Allowed.

`NextAction` is derived earliest relevant open activity or explicitly designated primary next step.

---

# 120. Seller Worklist

Priority ordering can consider:

```text
overdue activity
activity due today
new uncontacted lead
stale opportunity
quote expiry
expected close risk
high-value deal
manual priority
```

---

# 121. Worklist Must Explain Priority

Example:

```text
ABC Company
Priority: High
Why: Quote expires tomorrow + no follow-up scheduled
```

No mysterious algorithm-only ordering.

---

# 122. Activity Completion UX

One action:

```text
[Complete]
```

then optionally:

```text
Outcome
Note
Schedule next action
```

---

# 123. Complete + Next Action

Recommended fast flow:

```text
Complete Call
→ Outcome: Interested
→ Next: Demo Friday 2 PM
[Save]
```

---

# 124. Activity Notes

Notes supplement structured outcome.

Do not make note mandatory for every trivial action.

---

# 125. Meeting

Can store:

```text
participants
location / virtual link
start/end
action items
outcome
```

Calendar provider integration optional.

---

# 126. Call

TODIR can log call metadata manually or through telephony integration later.

Do not build telephony stack Day 1.

---

# 127. Email

Email integration can capture/send via connector/provider.

CRM stores communication metadata/reference, not duplicate mailbox system.

---

# 128. WhatsApp

WhatsApp integration is provider-based.

CRM stores:

```text
conversation/message references
participant
sent/received timestamp
status where provider gives it
```

and renders relevant commercial timeline.

---

# 129. Communication Consent / Policy

Country/company/privacy policies may control outreach.

Do not assume every Lead can be messaged indefinitely.

---

# 130. Communication Timeline

Unified read model can show:

```text
Lead created
Call
WhatsApp message
Meeting
Email
Stage change
Quotation created
Quotation accepted
Opportunity won
Invoice summary
Payment summary
```

Each item retains owning domain/source.

---

# 131. Timeline Is Read Composition

Do not copy every Sales/Invoice/Payment record into CRM tables just for timeline.

Use typed links/read composition/projections.

---

# 132. Timeline Ordering

Use business occurred timestamp + stable tie breaker.

---

# 133. Timeline Filters

```text
Activities
Communications
Opportunity Changes
Sales Documents
Financial Summary
Notes/Files
```

---

# 134. CRM Note

Simple internal note linked to:

```text
Lead
Opportunity
Party
```

---

# 135. Internal Note vs Customer Communication

Separate.

Never accidentally send internal note externally.

---

# 136. Files

CRM can attach:

```text
requirements document
meeting notes
customer brief
RFP
proposal reference
evidence
```

through Platform Files.

---

# 137. File Bytes

Not stored in CRM DB.

Only FileRef + metadata.

---

# 138. Opportunity Health

TODIR should have deterministic health before AI.

Recommended states:

```text
Healthy
NeedsAttention
AtRisk
Stalled
```

---

# 139. Health Factors

Configurable rules can consider:

```text
No next action
Last activity age
Days in stage
Expected close overdue
Quotation expired/expiring
No identified contact
No recent customer response
Critical activity overdue
Stage-specific missing information
```

---

# 140. Health Explanation

Always show reasons:

```text
At Risk
- No customer interaction for 14 days
- Expected close date passed 5 days ago
- No next activity scheduled
```

---

# 141. Health Is Rebuildable

Derived from CRM/source facts.

Can store projection for performance.

---

# 142. AI Health Later

AI/predictive score may augment deterministic health.

Do not replace explainable deterministic signals.

---

# 143. Stale / Rotted Opportunity

Opportunity can become stale when:

```text
no meaningful activity for threshold
or
stage age exceeds threshold
```

---

# 144. Stage-Specific Staleness

Example:

```text
New          3 days
Discovery    10 days
Proposal     14 days
Negotiation  21 days
```

configurable.

---

# 145. Stale Does Not Auto-Lose By Default

It becomes attention item.

Automation may suggest or require review.

---

# 146. Pipeline Hygiene

CRM continuously identifies:

```text
No owner
No next action
Expected close overdue
Stale stage
No recent activity
Missing value
Missing primary contact
Quote expired
Opportunity still open long after target
```

---

# 147. Pipeline Hygiene Score

Optional aggregated manager metric.

Must be composed from explicit checks.

---

# 148. Sales Manager Pipeline Inspection

View should allow sorting/filtering by:

```text
Stage
Value
Owner
Team
Health
Forecast Category
Expected Close
Age
Days In Stage
Last Activity
Next Activity
Quotation status
```

---

# 149. Pipeline Board

Kanban-style drag/drop by Stage.

Server still validates transition.

---

# 150. Drag/Drop Optimistic UI

Can optimistically move card, but rollback on backend rejection.

---

# 151. Pipeline List

Dense table alternative for managers/high-volume sellers.

---

# 152. Saved Views

Users can save filters/sorts/columns.

---

# 153. Favorites

Optional UX.

---

# 154. Forecast Mission

Help management estimate commercial outcome without pretending it is accounting truth.

---

# 155. Forecast Measures

Potential:

```text
Open Pipeline
Weighted Pipeline
Best Case
Commit
Closed Won
Expected This Period
```

---

# 156. Forecast Scope

Must define:

```text
Legal Entity
Team
Owner
Pipeline
Expected Close period
Reporting currency
```

---

# 157. Forecast By Expected Close

Default forecast period uses Opportunity expected close date.

---

# 158. Forecast Override

Sales manager may adjust forecast category/manager judgment if permissioned.

Preserve history.

---

# 159. Forecast Snapshot

Optional manager/period snapshot for historical forecast accuracy analysis.

---

# 160. Forecast Snapshot Is Not Pipeline Truth

It is historical planning evidence.

---

# 161. Forecast Accuracy

Later analytics can compare:

```text
forecast at date X
vs
actual Won/Lost outcome
```

---

# 162. Closed Won Value

CRM Won value is commercial outcome estimate/accepted amount according to policy.

Actual revenue remains accounting/Sales/Invoicing truth.

---

# 163. Actual Sales Comparison

Manager report may compose:

```text
CRM Won
Sales Quoted/Ordered
Invoices
Accounting Revenue
```

with labels.

Never collapse into one metric.

---

# 164. Sales Targets / Quotas Foundation

CRM can own simple commercial targets:

```text
Owner/Team
Period
Metric
Target Value
```

---

# 165. Target Metrics

Initial:

```text
Won Opportunity Value
Won Opportunity Count
Qualified Leads
```

Avoid target framework bloat.

---

# 166. Revenue Target Caveat

If target is financial revenue, source should be Accounting/Sales reporting, not CRM estimate.

---

# 167. Territory Seam

Optional future capability:

```text
Geography
Industry
Account segment
Named accounts
```

for routing/ownership.

Do not require Territory Day 1.

---

# 168. Account Segmentation

CRM can store/consume commercial segmentation such as:

```text
SMB
Mid-Market
Enterprise
Strategic
```

but canonical Party remains Directory.

---

# 169. Segmentation Ownership

Commercial CRM segmentation can be CRM-owned profile metadata linked to Party.

Legal/master identity remains Directory.

---

# 170. CRM Party Profile

Concept:

```text
CrmPartyProfile
{
    PartyId
    Segment?
    AccountOwnerUserId?
    TeamId?
    TerritoryRef?
    RelationshipState?
    Tags?
}
```

No duplicate name/address/tax/contact fields.

---

# 171. Account Owner

Commercial relationship owner for existing customer/account.

Opportunity can override owner.

---

# 172. Customer 360

CRM must give salesperson one commercial workspace for a known Party.

---

# 173. Customer 360 Composition

```text
Identity / Contacts            → Directory
CRM relationship/profile       → CRM
Open Opportunities             → CRM
Activities                     → CRM
Quotations / Orders            → Sales
Invoices                       → Invoicing
Outstanding                    → Subledger
Payments summary               → Payments
Recent products/purchases      → Sales/Invoicing
```

---

# 174. Customer 360 Is Not A Giant Aggregate

Lazy tabs/read composition.

---

# 175. Customer Financial Visibility

Salesperson can see only permitted summary:

```text
Outstanding
Overdue
Credit status
```

from Subledger/contracts.

---

# 176. Credit Does Not Belong CRM

CRM may display credit warning.

Sales/Subledger controls actual transaction credit checks.

---

# 177. Customer Lifetime Value

Do not invent CRM LTV from Opportunity estimates.

If shown, derive from actual Sales/Accounting history with explicit definition.

---

# 178. Last Purchase

Derived from Sales/Invoicing.

---

# 179. Last Contact

Derived from CRM communication/activity timeline.

---

# 180. Next Best Commercial Action Foundation

Rule-based suggestions can start with:

```text
Follow up overdue lead
Schedule next action
Call high-value at-risk opportunity
Follow expired quotation
Contact customer after inactivity
```

---

# 181. Suggestion ≠ Automatic Action

Default user approves/executes.

---

# 182. Sequence / Cadence

Optional sales engagement automation.

Example:

```text
Day 0  Call
Day 1  WhatsApp
Day 3  Follow-up email
Day 7  Call
Day 14 Review / Dormant
```

---

# 183. Small Company Does Not Need Cadence

Can use only `Next Action`.

---

# 184. Cadence Definition

Concept:

```text
CrmCadence
{
    Id
    Name
    TargetType
    Status
    Version
}
```

with ordered steps.

---

# 185. Cadence Step

```text
Delay / BusinessDayOffset
ActivityType
SuggestedChannel
TemplateRef?
StopConditions
```

---

# 186. Cadence Is Not Arbitrary Workflow Engine

Only sales-engagement actions.

Use Platform workflow/automation for broader cross-domain automation later.

---

# 187. Cadence Stop Conditions

Examples:

```text
Lead converted
Opportunity won
Opportunity lost
Customer replied
Manual stop
```

---

# 188. Automated Send

Day 1 recommendation:

```text
Create suggested/due activity
```

rather than autonomously send external messages.

Auto-send can be provider/policy capability later.

---

# 189. CRM Automation

Constrained automations:

```text
On lead created → assign owner
On stage change → create next task
On no activity X days → flag stale
On quote created → add follow-up
On opportunity won → create handoff task
```

---

# 190. Automation Safety

No arbitrary SQL/JS/C#.

Use typed triggers/conditions/actions.

---

# 191. Automation Versioning

Published rules versioned.

Execution stores rule/version when material.

---

# 192. Automation Idempotency

Same source event/rule version cannot create duplicate action unexpectedly.

---

# 193. Automation Loop Protection

Prevent:

```text
rule A changes field
→ rule B changes field
→ rule A ...
```

with trigger guards/execution graph.

---

# 194. CRM → Sales Quotation

Opportunity action:

```text
[Create Quotation]
```

---

# 195. Quote Creation Preconditions

Ensure:

```text
Directory Party exists
Customer Role exists if required
Sales module active
permissions valid
```

---

# 196. Quote Handoff

CRM passes:

```text
PartyId
ContactId?
OpportunityId source ref
Owner/Salesperson context
Team context?
Product interests? as draft suggestions
Currency?
commercial notes that are safe
```

Sales resolves pricing/tax/quotation truth.

---

# 197. CRM Does Not Pass Final Price As Authority

Opportunity estimate is not quotation price.

---

# 198. CRM → Sales Source Link

Sales Quotation stores CRM source reference.

CRM displays typed link.

---

# 199. Quote Conversion Idempotency

Repeated click does not create duplicate Quote for same explicit request/idempotency key.

---

# 200. Multiple Quote Creation

Allowed intentionally.

Each explicit creation gets unique purpose/request.


---

# 201. Opportunity → Won Handoff

Marking Won can trigger optional handoff checklist:

```text
Quotation accepted?
Sales Order needed?
Implementation/Project handoff later?
Customer onboarding task?
```

Do not force post-sale workflow if irrelevant.

---

# 202. Opportunity → Lost Handoff

Stop cadence/tasks according to policy.

Preserve timeline/evidence.

---

# 203. Renewal Signal

Future scheduler can create/suggest renewal opportunity based on:

```text
contract/renewal date
subscription expiry
prior opportunity metadata
```

source domain remains owner of contract/subscription date.

---

# 204. CRM Activities From Sales Events

CRM may auto-create/read reminders from:

```text
Quotation sent
Quotation expiring
Quotation declined
```

through events/contracts.

---

# 205. Sales Events Do Not Rewrite CRM Opportunity Automatically Without Policy

For example:

```text
Quotation accepted
```

may suggest/auto-mark Won only if explicit company rule.

---

# 206. Invoice Event

Invoice creation can appear in Customer 360/timeline.

Does not change Opportunity stage by itself unless configured.

---

# 207. Payment Event

Payment can appear as post-sale context.

CRM never computes settlement.

---

# 208. Opportunity Close Outcome vs Operational Sale

CRM outcome is commercial sales-management truth.

Sales/Invoice/Payment states remain separate dimensions.

---

# 209. External Communication Provider Model

Adapters can include:

```text
EmailProvider
WhatsAppProvider
CalendarProvider
TelephonyProvider
MeetingProvider
```

CRM depends on stable contracts.

---

# 210. Connection Credentials

Use Platform secret references.

Never store provider secrets in CRM rows.

---

# 211. Email Sync Privacy

Do not ingest entire mailbox blindly by default.

Scope to:

```text
linked CRM conversations
configured mailboxes
explicit BCC/sync rules
```

according to provider/privacy policy.

---

# 212. Communication Deduplication

Provider message/event ID ensures retries do not duplicate timeline entries.

---

# 213. Conversation Thread

Optional read grouping by Party/Contact/Opportunity.

Canonical provider message remains external where applicable; CRM stores normalized metadata/reference as needed.

---

# 214. Message Delivery Status

If provider supplies:

```text
Queued
Sent
Delivered
Read
Failed
```

show as communication metadata.

Do not confuse `Read` with sales qualification.

---

# 215. Template Boundary

Message/email templates can be CRM-owned content templates or Platform template capability.

Do not hardcode communication bodies in automation rules.

---

# 216. Opt-Out / Do-Not-Contact

CRM must support a contact communication restriction flag/context where required.

---

# 217. Do-Not-Contact Enforcement

Outbound action warns/blocks according to channel/legal/company policy.

---

# 218. Contact Preference

Directory may hold preferred communication metadata.

CRM consumes it.

---

# 219. CRM Custom Fields

Use Platform Custom Fields for non-core lead/opportunity attributes.

---

# 220. Custom Field Scope

Support entity targets:

```text
Lead
Opportunity
CRM Party Profile
```

---

# 221. Core Fields Must Stay Typed

Do not implement critical semantics like:

```text
Stage
Owner
Expected Close
Estimated Value
Won/Lost
Next Action
```

as arbitrary custom fields.

---

# 222. Custom Required Fields By Stage

Metadata can define stage-specific required custom/core fields.

---

# 223. Stage Validation UX

If moving to Proposal needs `ExpectedCloseDate`:

show clear inline requirement.

Do not open a separate workflow screen.

---

# 224. Tags

Optional CRM labels for lightweight classification.

Not substitute for structured Source/Stage/Segment.

---

# 225. Notes vs Fields

Use structured field when management needs filtering/reporting.

Use Note for context that does not need structured analytics.

---

# 226. CRM Permissions

Initial vocabulary:

```text
crm.home.read

crm.lead.read
crm.lead.create
crm.lead.edit
crm.lead.assign
crm.lead.qualify
crm.lead.disqualify
crm.lead.convert
crm.lead.merge
crm.lead.import

crm.opportunity.read
crm.opportunity.create
crm.opportunity.edit
crm.opportunity.assign
crm.opportunity.move_stage
crm.opportunity.mark_won
crm.opportunity.mark_lost
crm.opportunity.reopen
crm.opportunity.create_quote

crm.activity.read
crm.activity.create
crm.activity.edit
crm.activity.complete
crm.activity.assign

crm.team.read
crm.team.manage
crm.pipeline.read
crm.pipeline.manage

crm.forecast.read
crm.forecast.manage
crm.target.read
crm.target.manage

crm.customer360.read
crm.financial_summary.read

crm.cadence.read
crm.cadence.manage
crm.automation.read
crm.automation.manage

crm.export
crm.settings.manage
crm.audit.read
```

---

# 227. Visibility Scope

Permissions combine with scope:

```text
Own
Team
Branch
LegalEntity
AllAuthorizedEntities
SpecificAccounts/Territory later
```

---

# 228. Own Opportunity Scope

Backend uses `OwnerUserId`.

Client cannot spoof scope.

---

# 229. Team Scope

Resolve team membership server-side.

---

# 230. Manager Scope

CRM team manager can see configured team scope.

Not automatically all company data.

---

# 231. Financial Summary Permission

A salesperson may see outstanding balance only if permitted.

Do not expose detailed accounting by default.

---

# 232. Customer Contact Privacy

Respect Directory contact visibility/data policies.

---

# 233. Export Security

CRM export can contain personal data.

Require permission and audit where configured.

---

# 234. Notes Security

Internal restricted note visibility can be supported if genuine need.

Default team-visible according to CRM scope.

---

# 235. Field-Level Restrictions

High-value/sensitive fields can be restricted:

```text
Forecast override
Opportunity value
Competitor detail
Financial summary
Contact private details
```

Avoid excessive field ACL complexity Day 1.

---

# 236. RLS

All tenant-owned CRM tables:

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 237. RLS Is Not Sales Visibility

RLS protects tenants.

Application authorization protects Own/Team/Branch scope.

---

# 238. Runtime Role

No ownership/BYPASSRLS.

---

# 239. Tenant Context

Explicit transaction +:

```text
SET LOCAL app.tenant_id = ...
```

per Platform Kernel.

---

# 240. Legal Entity Context

CRM may allow tenant-shared early Leads where company structure needs it.

Opportunity/Sales handoff should resolve Legal Entity before transactional sales document creation.

---

# 241. Lead Legal Entity Scope

Configurable model:

```text
TenantShared
SpecificLegalEntity
```

Do not force an early inbound Lead to know legal entity if company routes centrally.

---

# 242. Opportunity Legal Entity

Required before Sales transaction creation.

---

# 243. Branch

Opportunity can carry commercial Branch context where useful.

Branch belongs Organization.

---

# 244. CRM Schema

Recommended:

```text
business_crm
```

---

# 245. CrmDbContext

Owns CRM tables only.

No Directory/Sales/Subledger/Payments EF navigation.

---

# 246. Core Tables

Potential:

```text
lead
lead_source
lead_assignment_history
lead_conversion
lead_external_ref
lead_merge_history
lead_score_snapshot

crm_party_profile
crm_sales_team
crm_sales_team_member

crm_pipeline
crm_pipeline_stage

opportunity
opportunity_stage_history
opportunity_contact_role
opportunity_interest
opportunity_source_link
opportunity_forecast_history
opportunity_health_projection

crm_activity_type
crm_activity
crm_activity_outcome
crm_note
crm_timeline_projection

crm_loss_reason
crm_target
crm_forecast_snapshot

crm_cadence
crm_cadence_version
crm_cadence_step
crm_cadence_enrollment

crm_automation_rule
crm_automation_version
crm_automation_execution

crm_external_communication_ref
crm_list_projection
crm_seller_worklist_projection
crm_pipeline_projection
```

Do not create every optional table before feature phase.

---

# 247. Lead Table

Owns unqualified prospect capture only.

---

# 248. CRM Party Profile

Stores CRM-specific commercial relationship data linked to Directory Party.

---

# 249. Opportunity Table

Stores current operational opportunity state.

---

# 250. Stage History

Append-only business history.

---

# 251. Health Projection

Rebuildable.

Not editable truth.

---

# 252. Seller Worklist Projection

Rebuildable priority view.

---

# 253. Timeline Projection

Can combine CRM-owned events + external typed references.

---

# 254. UUIDv7

Use for IDs.

---

# 255. Row Version

Mutable Lead/Opportunity/Activity uses optimistic concurrency.

---

# 256. Money

Opportunity estimated amount uses FinBase Money/decimal semantics.

---

# 257. Dates/Time

Use NodaTime:

```text
LocalDate for expected close
Instant for activities/events
DateTimeZone when scheduling human time
```

---

# 258. Business Hours

Lead SLA/cadence may use configurable CRM business calendar or shared Platform calendar.

Do not assume 24/7 elapsed time.

---

# 259. Search Normalization

Phone/email/name/company normalized for matching/search.

Directory contracts should be reused where possible.

---

# 260. JSONB

Use only for bounded provider payload/snapshots/rare metadata.

Do not hide hot CRM filters.

---

# 261. Indexes — Lead

Likely:

```text
tenant/state/created_at desc
tenant/owner/state/created_at
tenant/source/created_at
tenant/normalized_phone
tenant/normalized_email
external source/id unique
```

---

# 262. Indexes — Opportunity

Likely:

```text
tenant/pipeline/stage
 tenant/owner/is_open/expected_close
 tenant/team/is_open/expected_close
 tenant/health/is_open
 tenant/expected_close/is_open
 tenant/party/updated_at desc
```

Exact partial indexes from PostgreSQL query plans.

---

# 263. Indexes — Activity

```text
tenant/owner/state/due_at
tenant/opportunity/due_at
tenant/lead/due_at
tenant/party/occurred_at desc
```

---

# 264. Search

PostgreSQL first.

Use normalized columns/full-text/trigram where justified.

No OpenSearch Day 1.

---

# 265. CRM Contracts

Locked naming:

```text
Todir.CRM.Contracts
```

or repository-approved acronym casing consistently.

---

# 266. Backend Namespaces

Recommended:

```text
Todir.Modules.CRM.Domain
Todir.Modules.CRM.Application
Todir.Modules.CRM.Infrastructure
Todir.CRM.Contracts
```

---

# 267. Internal Areas

Potential:

```text
Leads/
Opportunities/
Pipelines/
Activities/
Teams/
Forecasting/
Engagement/
Automations/
Customer360/
Reporting/
```

---

# 268. No Generic CRM CRUD Service

Use semantic commands.

---

# 269. Lead Commands

```text
CreateLead
AssignLead
QualifyLead
DisqualifyLead
ConvertLead
MergeLeads
ArchiveLead
ReopenLead
```

---

# 270. Opportunity Commands

```text
CreateOpportunity
AssignOpportunity
MoveOpportunityStage
ChangeExpectedCloseDate
ChangeOpportunityEstimate
SetForecastCategory
MarkOpportunityWon
MarkOpportunityLost
ReopenOpportunity
CreateSalesQuotationFromOpportunity
```

---

# 271. Activity Commands

```text
ScheduleActivity
RescheduleActivity
CompleteActivity
CancelActivity
AssignActivity
CreateNextAction
```

---

# 272. Pipeline Commands

```text
CreatePipeline
CreateStage
ReorderStages
ChangeStageRules
ArchiveStage
ArchivePipeline
```

---

# 273. Team Commands

```text
CreateCrmTeam
AddTeamMember
EndTeamMembership
AssignTeamManager
```

---

# 274. Cadence Commands

```text
CreateCadence
PublishCadenceVersion
EnrollLeadInCadence
EnrollOpportunityInCadence
PauseCadenceEnrollment
StopCadenceEnrollment
```

---

# 275. Automation Commands

```text
CreateCrmAutomation
PublishAutomationVersion
EnableAutomation
DisableAutomation
```

---

# 276. No Generic SetStatus

Forbidden public command:

```text
SetOpportunityStatus("won")
```

Use semantic transition.

---

# 277. No Generic SetCustomerBalance

Forbidden.

---

# 278. No CRM CreateInvoice

CRM creates Sales Quotation/requests Sales flow, not Invoice rows.

---

# 279. API Base

```text
/api/v1/crm
```

---

# 280. Seller Home APIs

```text
GET /home
GET /worklist
GET /attention
```

---

# 281. Lead APIs

```text
GET  /leads
POST /leads
GET  /leads/{id}
PUT  /leads/{id}
POST /leads/{id}/assign
POST /leads/{id}/qualify
POST /leads/{id}/disqualify
POST /leads/{id}/convert
POST /leads/merge
POST /leads/import
```

---

# 282. Opportunity APIs

```text
GET  /opportunities
POST /opportunities
GET  /opportunities/{id}
PUT  /opportunities/{id}
POST /opportunities/{id}/assign
POST /opportunities/{id}/move-stage
POST /opportunities/{id}/mark-won
POST /opportunities/{id}/mark-lost
POST /opportunities/{id}/reopen
POST /opportunities/{id}/create-quotation
```

---

# 283. Activity APIs

```text
GET  /activities
POST /activities
PUT  /activities/{id}
POST /activities/{id}/complete
POST /activities/{id}/cancel
```

---

# 284. Pipeline APIs

```text
GET  /pipelines
POST /pipelines
PUT  /pipelines/{id}
POST /pipelines/{id}/stages
POST /pipelines/{id}/reorder-stages
```

---

# 285. Forecast APIs

```text
GET /forecast
GET /pipeline-inspection
POST /opportunities/{id}/forecast-category
POST /forecast/snapshots
```

---

# 286. Customer 360 API

```text
GET /accounts/{partyId}/workspace
```

composed/lazy child endpoints as needed.

---

# 287. CRM Team APIs

```text
GET  /teams
POST /teams
POST /teams/{id}/members
POST /teams/{id}/members/{userId}/end
```

---

# 288. Cadence APIs

```text
GET  /cadences
POST /cadences
POST /cadences/{id}/versions
POST /cadence-versions/{id}/publish
POST /cadence-enrollments
POST /cadence-enrollments/{id}/stop
```

---

# 289. External Lead API

Use authenticated provider endpoint/webhook adapter.

Never expose unrestricted public lead creation without anti-abuse/rate limits.

---

# 290. Public Website Form

Recommended architecture:

```text
Public Lead Capture Endpoint
→ Rate limit / bot controls
→ normalization
→ CRM command
```

---

# 291. API Idempotency

Mandatory for:

```text
external lead capture
lead conversion
lead merge
opportunity quote creation
mark won/lost
external communication ingestion
automation execution
imports
```

---

# 292. Same Idempotency Key Different Payload

Conflict.

---

# 293. Concurrency — Lead Conversion

Two users cannot convert same Lead into duplicate opportunities/parties unexpectedly.

---

# 294. Concurrency — Stage Move

Stale version rejects or reconciles according to explicit command policy.

---

# 295. Concurrency — Activity Completion

Double complete returns same result/no duplicate next-action automation.

---

# 296. Concurrency — Mark Won/Lost

Exactly one current commercial outcome.

Conflicting simultaneous commands handled explicitly.

---

# 297. Error Codes

Stable examples:

```text
crm.lead_not_found
crm.lead_stale
crm.lead_duplicate_suspected
crm.lead_already_converted
crm.lead_not_qualifiable
crm.lead_conversion_conflict

crm.opportunity_not_found
crm.opportunity_stale
crm.opportunity_stage_invalid
crm.opportunity_transition_blocked
crm.opportunity_already_closed
crm.opportunity_value_invalid
crm.opportunity_party_required_for_quote

crm.activity_not_found
crm.activity_already_completed
crm.activity_invalid_time

crm.pipeline_invalid
crm.stage_in_use
crm.manager_cycle

crm.directory_match_conflict
crm.quote_creation_conflict
crm.communication_restricted
crm.external_source_duplicate
crm.permission_denied
crm.idempotency_conflict
```

---

# 298. Friendly Error — Quote Creation

> "لا يمكن إنشاء عرض سعر قبل تحديد العميل الرسمي. راجع أو أنشئ العميل أولًا."

Provide conversion/link action.

---

# 299. Friendly Error — Stage

> "لا يمكن نقل الفرصة للمرحلة دي قبل تحديد موعد الإغلاق المتوقع."

---

# 300. Friendly Error — Duplicate Lead

Show likely matches and choices:

```text
Open existing
Merge
Keep separate
```


---

# 301. Import

CRM import supports:

```text
Leads
Opportunities
Activities
CRM Party Profiles
CRM Teams/Memberships
Historical stage outcome where available
```

---

# 302. Import Mapping

Preview source columns to typed fields/custom fields.

---

# 303. Import Duplicate Check

Before commit:

```text
Lead duplicates
Directory Party matches
Opportunity duplicates by external key
Owner/team resolution
```

---

# 304. External Owner Mapping

Legacy salesperson IDs map to authorized TODIR Users.

Never create fake Users from names automatically.

---

# 305. Historical Opportunity Migration

Can import:

```text
Open
Won
Lost
```

with historical quality metadata.

Do not fabricate detailed stage history if source lacks it.

---

# 306. Historical Activity Migration

Optional based on business value.

No need to import millions of low-value email logs blindly.

---

# 307. Migration Boundary

Define:

```text
CrmGoLiveDate
HistoricalLeadStrategy
HistoricalOpportunityStrategy
ActivityHistoryBoundary
```

---

# 308. External References

Store:

```text
legacy system
external record id
batch id
source file row
```

---

# 309. Import Idempotency

Same external record cannot duplicate on retry.

---

# 310. Export

Permissioned exports:

```text
Leads
Opportunities
Pipeline
Activities
Forecast
Won/Lost
Source performance
```

---

# 311. Export Data Scope

Respects Own/Team/Entity visibility.

---

# 312. Large Export

Background job + secure file.

---

# 313. CRM Reports — Seller

```text
My Leads
My Open Opportunities
My Activities
My Overdue Follow-ups
My Won/Lost
My Pipeline
```

---

# 314. CRM Reports — Manager

```text
Pipeline by Stage
Pipeline by Owner
Pipeline by Team
Lead Source Performance
Lead Response Time
Lead Conversion
Opportunity Conversion
Win Rate
Lost Reasons
Sales Cycle
Stage Duration
Stale Deals
Forecast
Activity Coverage
```

---

# 315. Lead Conversion Rate

Definition must state denominator.

Example:

```text
Converted Leads / Qualified-or-eligible Leads
```

Do not publish ambiguous percentage.

---

# 316. Opportunity Win Rate

Explicit:

```text
Won / (Won + Lost)
```

for selected closed cohort/time definition.

---

# 317. Sales Cycle

Define from:

```text
Opportunity Created/Qualified
→ Won/Lost
```

with chosen cohort logic.

---

# 318. Stage Duration

Derived from Stage History.

---

# 319. Lead Response Time

Derived from Lead Created → first meaningful response/activity.

---

# 320. Activity Count Is Not Sales Performance Alone

Avoid encouraging meaningless activity volume.

Use activity as context/coverage, not sole success metric.

---

# 321. Source Performance

Report:

```text
Leads
Qualified
Opportunities
Won
Estimated/Won value
conversion
```

by source.

---

# 322. Campaign Attribution Caveat

CRM attribution is commercial attribution model, not full marketing measurement platform.

---

# 323. Forecast Report

Must show:

```text
category
owner/team
period
value
currency basis
health/risk
```

---

# 324. Forecast Does Not Include Lost/Omitted By Default

Explicit filters only.

---

# 325. Pipeline Aging

Show open opportunities by age buckets.

---

# 326. Pipeline Coverage

Potential:

```text
Open weighted pipeline / target
```

only when targets enabled.

---

# 327. No Fake Revenue Dashboard

Label:

```text
Pipeline
Won Opportunities
Sales Orders
Invoiced
Revenue
Cash Collected
```

as separate metrics/sources.

---

# 328. AI Opportunities

AI may:

```text
Summarize Lead
Summarize Opportunity
Summarize Account history
Suggest next action
Suggest follow-up draft
Extract meeting action items
Suggest lead score
Suggest deal health risks
Suggest duplicate matches
Classify lead source/interest
Explain forecast risk
Summarize communication thread
```

---

# 329. AI Cannot By Default

```text
Mark Won
Mark Lost
Change price
Commit discount
Create binding Quotation
Override credit
Send legally/commercially binding communication
Create Invoice
Move money
```

without explicit approved command/permission/user confirmation.

---

# 330. AI Tool Path

```text
AI Tool Layer
→ CRM Permissions
→ CRM Query/Command
→ Directory/Sales contracts when needed
→ Audit
```

---

# 331. AI Lead Scoring

Only after enough quality labeled historical outcomes.

Do not pretend predictive accuracy with insufficient data.

---

# 332. AI Score Transparency

Expose major signals/explanation where model permits.

---

# 333. AI Conversation Summary

Must distinguish generated summary from source communication.

Original message/activity references remain.

---

# 334. AI Draft Outreach

User reviews by default.

Respect do-not-contact and channel permissions.

---

# 335. AI Data Minimization

Send only required authorized customer data.

---

# 336. AI Does Not Write Database Directly

Absolute TODIR AI rule.

---

# 337. CRM Home Performance

Initial target:

```text
p95 <= 300-600ms
```

for seller worklist + summary on realistic data.

---

# 338. Lead Inbox Target

```text
p95 <= 150-300ms
```

for indexed/filter page.

---

# 339. Pipeline Board Target

```text
p95 <= 250-500ms
```

for normal visible cards/stages with server paging/limits.

---

# 340. Opportunity Workspace Target

```text
p95 <= 200-400ms
```

for core details; timeline/360 tabs lazy.

---

# 341. Activity Worklist Target

```text
p95 <= 150-300ms
```

for owner/date/status query.

---

# 342. Scale Assumptions

Load-test for:

```text
1M+ Leads
1M+ Opportunities historical
10M+ Activities/communications
100k Directory Parties
hundreds of sellers
multi-year pipeline history
```

for architecture validation.

---

# 343. Pipeline Board Does Not Load Everything

Each stage loads bounded top page/count.

Virtualization/infinite loading for large stages.

---

# 344. Counts

Stage counts can use indexed query/projection.

---

# 345. Seller Worklist Projection

Recommended rebuildable projection based on:

```text
open activities
lead state
opportunity health
quote signal summaries
```

---

# 346. Worklist Freshness

Core CRM changes should update projection transactionally where feasible.

External-domain signals may be event/read refreshed with clear acceptable freshness.

---

# 347. No N+1

Opportunity list cannot query per row:

```text
Party
Contact
Activity
Quote
Health
```

individually.

---

# 348. Customer 360 Lazy Composition

Only initial summary + requested tabs.

---

# 349. Forecast Set-Based

No loop over opportunities in application for aggregation.

---

# 350. Stage History Query

Indexed by Opportunity and moved timestamp.

---

# 351. Redis

No Redis Day 1 for canonical CRM state.

---

# 352. Local Cache

Safe for:

```text
pipeline definitions
stage definitions
activity types
loss reasons
published automation/cadence versions
```

with version invalidation.

---

# 353. OpenSearch

Not Day 1.

PostgreSQL normalized search/trigram/full-text first.

---

# 354. Background Jobs

Use for:

```text
staleness scans
lead SLA scans
cadence step generation
large import/export
health rebuild
forecast snapshots
external sync
```

---

# 355. Job Idempotency

Repeated stale scan/cadence job must not duplicate activities/alerts.

---

# 356. Staleness Can Be Query-Derived

Do not necessarily write a row every day just to mark stale.

Projection/job only where performance/notifications require.

---

# 357. Observability Metrics

Potential:

```text
crm_lead_create_duration
crm_lead_conversion_duration
crm_first_response_time
crm_opportunity_stage_move_duration
crm_opportunity_stale_count
crm_activity_overdue_count
crm_pipeline_query_duration
crm_forecast_query_duration
crm_quote_handoff_failure_total
crm_external_lead_duplicate_total
crm_automation_failure_total
```

No Lead/Contact IDs as metric labels.

---

# 358. Tracing — Lead Conversion

```text
Authorize
Load Lead
Directory Search/Match
Create/Link Party
Add Customer Role
Create Opportunity
Create conversion history
Audit
Outbox
Commit
```

---

# 359. Tracing — Quote Handoff

```text
Authorize
Load Opportunity
Validate Party
Build Sales request
Sales create Quotation
Create typed source link
Activity/timeline event
Commit
```

---

# 360. Logging

Never dump full communications/contact personal data into logs.

---

# 361. Security — External Lead Capture

Protect against:

```text
spam
bot floods
payload abuse
oversized fields
script/HTML injection
replay
```

---

# 362. Security — Notes/HTML

Sanitize rendered rich text.

Prefer bounded safe editor format.

---

# 363. Security — File Upload

Use Platform Files malware/content controls.

---

# 364. Security — IDOR

Every Lead/Opportunity/Activity API validates row visibility scope.

---

# 365. Security — Quote Handoff

User must have both appropriate CRM action and Sales quotation permission/context.

---

# 366. Security — Team Assignment

Cannot assign to inaccessible/non-member user if policy restricts.

---

# 367. Security — Financial Summary

Subledger summary requires permission, not implied by CRM read.

---

# 368. Security — Communication Tokens

Provider OAuth/keys stored in secret manager/connector, not CRM tables.

---

# 369. Audit Critical Actions

Capture:

```text
Lead assignment
Lead qualification/disqualification
Lead conversion/merge
Opportunity owner/team change
Stage move
Expected close/value change where material
Forecast category override
Won/Lost/Reopen
Quotation creation handoff
Automation publish/enable
Cadence publish
Bulk import/export
```

---

# 370. Business History vs Audit

Stage/assignment/outcome history is business history.

Audit captures actor/system mutation evidence.

---

# 371. Test — Minimal Lead

Create Lead with minimal fields.

No Party required.

---

# 372. Test — Duplicate Candidate

Phone/email match warns, does not auto-merge.

---

# 373. Test — External Lead Retry

One Lead only.

---

# 374. Test — Lead Conversion Existing Party

Links same Party, adds role if needed, creates one Opportunity.

---

# 375. Test — Lead Conversion New Party

Creates Directory Party correctly through Directory contract.

---

# 376. Test — Double Convert

No duplicate Party/Opportunity.

---

# 377. Test — Lead Disqualify

Structured reason persisted.

---

# 378. Test — Direct Opportunity

Existing Party can create Opportunity without Lead.

---

# 379. Test — Stage Move

History exact.

---

# 380. Test — Stale Stage Move Race

Optimistic concurrency prevents lost updates.

---

# 381. Test — Stage Required Field

Transition blocked with friendly message.

---

# 382. Test — Expected Close Overdue

Hygiene warning appears.

---

# 383. Test — No Next Action

Health/worklist flags according to policy.

---

# 384. Test — Healthy Opportunity

No unnecessary warning.

---

# 385. Test — Mark Won

Commercial outcome only; no fake Invoice/Payment.

---

# 386. Test — Mark Lost

Loss reason required if configured.

---

# 387. Test — Reopen Lost

Prior loss history retained.

---

# 388. Test — Opportunity Value

Does not alter Quotation/Revenue.

---

# 389. Test — Multiple Quotations

CRM shows all typed Sales links.

---

# 390. Test — Create Quotation

Sales owns created Quotation and Pricing/Tax.

---

# 391. Test — Quote Handoff Double Click

Idempotent for same request.

---

# 392. Test — Customer 360 Outstanding

Matches Subledger summary.

---

# 393. Test — Customer 360 Invoice

Matches Invoicing source.

---

# 394. Test — Activity Complete

Outcome/history correct.

---

# 395. Test — Complete + Next

One action safely creates next Activity.

---

# 396. Test — Double Complete

No duplicate automation/next action.

---

# 397. Test — Overdue Activity

Derived correctly by timezone.

---

# 398. Test — Lead SLA

Business-hour calculation exact.

---

# 399. Test — Buying Committee

Contacts remain Directory-owned.

---

# 400. Test — Forecast Weighted Value

Exact decimal/FX/reporting rules.

---

# 401. Test — Forecast vs Revenue

No shared/corrupt metric source.

---

# 402. Test — Multiple Pipelines

Stage cannot move across pipeline accidentally without explicit controlled change.

---

# 403. Test — Archive Stage In Use

Historical opportunities retain stage label/ref; open opportunities require migration/reassignment.

---

# 404. Test — Cadence

Creates due activities exactly once.

---

# 405. Test — Cadence Stop On Reply/Won

No future unwanted activities.

---

# 406. Test — Automation Loop

Detected/prevented.

---

# 407. Test — Do Not Contact

Outbound action blocked/warned as configured.

---

# 408. Test — Cross Tenant RLS

Tenant A cannot see Tenant B CRM.

---

# 409. Test — Own Scope

Seller cannot change URL/id to another seller's private Lead.

---

# 410. Test — Team Scope

Manager sees only configured team.

---

# 411. Test — Financial Permission

CRM read user without permission does not receive outstanding amount.

---

# 412. Test — Export Scope

Export cannot bypass visibility.

---

# 413. Test — 1M Lead Search

Measured indexed performance.

---

# 414. Test — 1M Opportunity Pipeline

Manager views filtered/current pages without full scan.

---

# 415. Test — 10M Activities

Worklist/timeline cursor performance measured.

---

# 416. Test — Customer 360

Initial load avoids cross-module N+1.

---

# 417. Property Test — One Current Outcome

An Opportunity cannot be simultaneously open/won/lost.

---

# 418. Property Test — Stage History

Current Stage equals latest valid stage transition/current state.

---

# 419. Property Test — Converted Lead

One conversion record per explicit conversion lifecycle.

---

# 420. Property Test — External Idempotency

Same external source key creates one Lead/communication.


---

# 421. Architecture Test — Lead Is Not Directory Party

CRM Lead entity cannot reuse/extend Directory Party table as early-lead storage.

---

# 422. Architecture Test — Opportunity Is Not Sales Order

No Sales Order/Quotation implementation entity inside CRM.

---

# 423. Architecture Test — No Invoicing DbContext

Use contracts/read models.

---

# 424. Architecture Test — No Subledger Balance Logic

CRM never recomputes outstanding customer balance.

---

# 425. Architecture Test — No Payment Mutation

CRM cannot mark customer paid.

---

# 426. Architecture Test — No Pricing Engine Duplication

Opportunity estimate is not Pricing resolution.

---

# 427. Architecture Test — No Duplicate Contact Master

Buying committee uses Directory Contact IDs.

---

# 428. Architecture Test — No Arbitrary Automation Scripts

Typed automation only.

---

# 429. Red-Team — Every Lead Creates Customer

Reject.

---

# 430. Red-Team — Leads Live In Active Pipeline

Reject by default.

Use Lead Inbox until qualified.

---

# 431. Red-Team — Opportunity Value Is Revenue

Reject.

---

# 432. Red-Team — Mark Won Creates Invoice Automatically

Reject as default.

Sales/Invoicing remain explicit owners.

---

# 433. Red-Team — One Mandatory Pipeline

Reject for advanced companies, though one default pipeline is recommended.

---

# 434. Red-Team — 30 Required Lead Fields

Reject.

---

# 435. Red-Team — Salesperson Must Complete Long Form Before Calling

Reject.

---

# 436. Red-Team — Opportunity Has No Next-Action Concept

Reject.

---

# 437. Red-Team — Stale Opportunities Never Flagged

Reject.

---

# 438. Red-Team — Probability Is Accounting

Reject.

---

# 439. Red-Team — Weighted Pipeline Uses JS Float

Reject.

Use decimal/FinBase/reporting logic.

---

# 440. Red-Team — CRM Stores Customer Invoice Copy

Reject.

---

# 441. Red-Team — CRM Stores Customer Balance Copy

Reject.

---

# 442. Red-Team — Buying Committee Has Duplicate Contact Names

Reject.

Use Directory Contacts.

---

# 443. Red-Team — Auto-Merge Duplicate Leads

Reject by default.

---

# 444. Red-Team — AI Marks Opportunity Won

Reject by default.

---

# 445. Red-Team — AI Sends Price/Discount Commitment Without Review

Reject.

---

# 446. Red-Team — Every External Message Loaded In CRM DB As Full Mailbox

Reject.

Scope/normalize/reference.

---

# 447. Red-Team — Activity Count Is Sales KPI Of Success

Reject as sole performance metric.

---

# 448. Red-Team — Forecast Includes Closed Lost

Reject unless explicit report filter.

---

# 449. Red-Team — Manager Can See Everything Because Manager

Reject.

Use configured scope/permissions.

---

# 450. Red-Team — Pipeline Board Loads 100k Cards

Reject.

---

# 451. Red-Team — CRM Needs OpenSearch Day 1

Reject absent evidence.

---

# 452. Red-Team — CRM Needs Redis For Correctness

Reject.

---

# 453. Red-Team — Automation Can Run SQL

Reject.

---

# 454. Red-Team — Mark Lost Deletes Opportunity

Reject.

---

# 455. Red-Team — Stage Change Rewrites History

Reject.

---

# 456. Product Acceptance — Salesperson

A seller can start the day and answer:

```text
Who should I contact now?
What is overdue?
Which deals need attention?
What is my next action?
```

without building spreadsheet/filter manually.

---

# 457. Product Acceptance — Lead Capture

New inbound Lead can be saved in seconds.

---

# 458. Product Acceptance — Lead Inbox

Unqualified noise does not pollute Opportunity pipeline.

---

# 459. Product Acceptance — Qualification

Company can keep qualification simple or configure additional structured requirements.

---

# 460. Product Acceptance — Conversion

One action converts qualified Lead to Directory Party/Customer Role + Opportunity without duplicates.

---

# 461. Product Acceptance — Opportunity

Seller sees:

```text
Customer
Value
Stage
Health
Last interaction
Next action
Expected close
Quote links
Buying committee
```

in one workspace.

---

# 462. Product Acceptance — Pipeline

Sales manager can trust that active pipeline excludes unqualified lead clutter and clearly surfaces stale deals.

---

# 463. Product Acceptance — Health

At-risk state explains why.

---

# 464. Product Acceptance — Forecast

Manager can distinguish:

```text
Pipeline
Best Case
Commit
Won
Actual Revenue
```

---

# 465. Product Acceptance — Customer 360

Seller sees commercial relationship without switching through multiple modules for basic context.

---

# 466. Product Acceptance — ERP Handoff

Opportunity creates Sales Quotation without retyping customer/contact/product-interest context unnecessarily.

---

# 467. Product Acceptance — Won/Lost

Outcomes are quick to record and useful for analysis.

---

# 468. Product Acceptance — Small Company

Can operate with:

```text
Lead
Opportunity
Next Action
Quotation
```

without cadence/forecast/territory complexity.

---

# 469. Product Acceptance — Large Company

Can enable:

```text
Multiple Pipelines
Teams
Routing
Forecast Categories
Targets
Cadences
Automation
Buying Committee
Health/Scoring
```

without replacing CRM Core.

---

# 470. Product Acceptance — Data Entry Value

Routine CRM actions should save the salesperson more follow-up/search time than the data entry they require.

---

# 471. Product Acceptance — Management Reality

Open pipeline should not silently retain obviously dead/stale opportunities without warnings.

---

# 472. Implementation Agent Philosophy

Build CRM in vertical business slices.

Do not start with 100 settings screens.

Do not build AI before clean Lead/Opportunity/Activity data.

---

# 473. Agent Precedence

If conflict:

```text
19 CRM pack
→ 04 Directory for Party/Contact ownership
→ 14 Sales for Quotation/Sales ownership
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ later approved ADR
```

---

# 474. Agent Must Read Before CRM

At minimum:

```text
01
02
03
04
05
07
10
11
12
14
19
all relevant AGENTS.md
actual Directory contracts
actual Sales contracts
approved ADRs
```

---

# 475. Agent Must Inspect Repository

Before implementation report:

```text
Directory Contact model
Party roles
Sales Quotation source refs
Salesperson/team fields
Platform Files
Custom Fields
Notifications
Jobs
Outbox
RLS patterns
IAM scope model
```

---

# 476. Agent Must Not Duplicate Existing Sales Team Implementation Blindly

If `14 Sales` implementation already has a canonical SalesTeam table:

stop only the team-definition slice, document conflict, and create a focused ADR recommending one master.

Continue unaffected CRM work.

Never create two writable team masters.

---

# 477. Recommended Team ADR Outcome

Prefer:

```text
CRM team = pre-sales routing/team master when CRM active
Sales documents = assignment/source snapshot/reference
```

but repository reality must be reconciled before migration.

---

# 478. Agent Task Template

Every phase:

```text
TASK

BUSINESS GOAL
Why seller/manager needs this.

CONTEXT
Which modules own customer/sales/financial truth.

ARCHITECTURE RULES

FILES TO READ

FILES ALLOWED TO MODIFY

DO NOT MODIFY

IMPLEMENTATION REQUIREMENTS

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
- tests/results
- query-plan/performance evidence
- security evidence
- source-of-truth integrations
- unresolved decisions
- risks
```

---

# 479. Implementation Sequence

```text
CR00 Repository + CRM Baseline Verification
CR01 Module Boundary + Schema + RLS
CR02 Lead Core + Lead Inbox
CR03 Lead Search + Duplicate Detection
CR04 Lead Source + External Capture Foundation
CR05 Lead Assignment + Ownership
CR06 Lead Qualification + Disqualification
CR07 Directory Conversion
CR08 CRM Teams
CR09 Opportunity Core
CR10 Pipelines + Stages
CR11 Stage History + Aging
CR12 Activities + Next Action
CR13 Seller Worklist
CR14 Opportunity Health + Staleness
CR15 Buying Committee / Contact Roles
CR16 Opportunity Interests
CR17 Opportunity → Sales Quotation
CR18 Won / Lost + Loss Reasons
CR19 Customer 360
CR20 Communication Timeline
CR21 Email/WhatsApp/Calendar Integration Seams
CR22 CRM Party Profile / Account Ownership
CR23 Forecast Categories + Pipeline Forecast
CR24 Targets / Quotas Foundation
CR25 Lead Routing Advanced Rules
CR26 Cadences / Sequences
CR27 CRM Automation
CR28 Imports / Migration
CR29 Reports / Sales Analytics
CR30 Permissions + Visibility Scope + Audit
CR31 Custom Fields + Stage Requirements
CR32 AI Readiness / AI Tool Contracts
CR33 Performance + Scale
CR34 Security / Privacy Red-Team
CR35 Runbooks / Recovery
CR36 Seller Acceptance Test
CR37 Sales Manager Acceptance Test
CR38 CRM → Sales E2E Acceptance
CR39 Data Quality / Pipeline Hygiene Acceptance
CR40 Release Gate
```

---

# 480. CR00 — Goal

Understand existing Directory/Sales/platform implementation.

---

# 481. CR00 Verify

```text
Party/Contact
Customer Role
Directory duplicate search
Sales Quotation command/source ref
Sales salesperson/team fields
IAM User/Employee links
Custom Fields
Files
Outbox
Notifications
Scheduler
RLS
```

---

# 482. CR00 Exit Gate

Exact reuse/gap report.

---

# 483. CR01 — Goal

Create CRM boundary without duplicating Directory/Sales.

---

# 484. CR01 Build

```text
Todir.Modules.CRM.Domain
Todir.Modules.CRM.Application
Todir.Modules.CRM.Infrastructure
Todir.CRM.Contracts
business_crm
CrmDbContext
manifest
RLS
architecture tests
frontend module shell
```

---

# 485. CR01 Exit Gate

No direct cross-module implementation references.

---

# 486. CR02 — Goal

Fast, low-friction Lead Inbox.

---

# 487. CR02 Build

```text
Lead
minimal create
states
owner
list/inbox
archive
```

---

# 488. CR02 Exit Gate

Salesperson creates Lead in seconds with no Party.

---

# 489. CR03 — Goal

Prevent obvious duplicate prospect clutter.

---

# 490. CR03 Build

```text
normalized phone/email
Lead duplicate candidates
Directory candidate search
merge foundation
```

---

# 491. CR03 Exit Gate

Duplicate warning useful, no auto-merge.

---

# 492. CR04 — Goal

Prepare inbound lead ecosystem.

---

# 493. CR04 Build

```text
Lead Source
ExternalRef
public/provider capture command
idempotency
rate limiting seam
import
```

---

# 494. CR04 Exit Gate

Webhook retry creates one Lead.

---

# 495. CR05 — Goal

Every Lead has clear responsibility when company wants assignment.

---

# 496. CR05 Build

Owner, assignment history, manual assignment.

---

# 497. CR05 Exit Gate

No unowned Lead unless policy allows.

---

# 498. CR06 — Goal

Turn raw prospect into meaningful qualified pipeline input.

---

# 499. CR06 Build

```text
qualification fields
qualify/disqualify
reasons
optional stage requirements
```

---

# 500. CR06 Exit Gate

Small company can qualify with minimal steps.

---

# 501. CR07 — Goal

Safe Lead → Directory/Opportunity conversion.

---

# 502. CR07 Build

Directory search/link/create, Customer Role, conversion record, idempotency.

---

# 503. CR07 Exit Gate

No duplicate Party on double conversion.

---

# 504. CR08 — Goal

Team routing/reporting without salesperson identity duplication.

---

# 505. CR08 Build

Team/membership only after resolving Sales-team ownership ADR if implementation conflict exists.

---

# 506. CR08 Exit Gate

One canonical writable team master.

---

# 507. CR09 — Goal

Production Opportunity workspace core.

---

# 508. CR09 Build

```text
Opportunity
owner/team
Party/contact links
value/currency
expected close
source/type
```

---

# 509. CR09 Exit Gate

Existing customer can create Opportunity directly.

---

# 510. CR10 — Goal

Configurable simple pipelines.

---

# 511. CR10 Build

Pipeline, Stage, reorder, archive controls.

---

# 512. CR10 Exit Gate

One default pipeline works with no methodology configuration.

---

# 513. CR11 — Goal

Permanent stage history and aging.

---

# 514. CR11 Build

Stage transitions, stage age, AsOf/history queries.

---

# 515. CR11 Exit Gate

Manager can answer how long deals spend in stages.

---

# 516. CR12 — Goal

Make next action first-class.

---

# 517. CR12 Build

Activity types, schedule/complete/cancel, outcome, next action.

---

# 518. CR12 Exit Gate

Seller can complete activity + schedule next in one flow.

---

# 519. CR13 — Goal

Seller always knows what to do next.

---

# 520. CR13 Build

Priority worklist + explainable reasons.

---

# 521. CR13 Exit Gate

Seller Acceptance worklist scenario passes.

---

# 522. CR14 — Goal

Make pipeline self-cleaning/attention-driven.

---

# 523. CR14 Build

Health rules, stale thresholds, hygiene exceptions.

---

# 524. CR14 Exit Gate

Dead/stale deals are visible without manager manually inspecting every record.

---

# 525. CR15 — Goal

B2B buying committee support.

---

# 526. CR15 Build

OpportunityContactRole using Directory Contacts.

---

# 527. CR15 Exit Gate

No duplicate CRM Contact identity.

---

# 528. CR16 — Goal

Capture early product/service interest without duplicating Quotation.

---

# 529. CR16 Build

Lightweight typed interest lines.

---

# 530. CR16 Exit Gate

Interest can prefill/suggest Sales Quote draft, never become price truth.

---

# 531. CR17 — Goal

Seamless CRM → Sales handoff.

---

# 532. CR17 Build

`CreateSalesQuotationFromOpportunity` typed contract + source link + idempotency.

---

# 533. CR17 Exit Gate

No customer/product context unnecessary re-entry.

---

# 534. CR18 — Goal

Reliable outcome analytics.

---

# 535. CR18 Build

Won/Lost commands, reasons, reopen policy, history.

---

# 536. CR18 Exit Gate

Win/Loss reports use structured outcomes.

---

# 537. CR19 — Goal

Customer 360 without duplicate ERP truth.

---

# 538. CR19 Build

Lazy composed views from Directory, CRM, Sales, Invoicing, Subledger, Payments.

---

# 539. CR19 Exit Gate

Every displayed cross-domain number/source can be traced to owner.

---

# 540. CR20 — Goal

One understandable commercial timeline.

---

# 541. CR20 Build

Typed CRM events + source-domain refs + filters/pagination.

---

# 542. CR20 Exit Gate

Timeline does not copy entire Invoice/Payment records.

---

# 543. CR21 — Goal

Provider-ready communications.

---

# 544. CR21 Build

Email/WhatsApp/calendar adapter contracts, normalized refs, idempotent ingestion.

---

# 545. CR21 Exit Gate

CRM Core has no provider credentials/proprietary transport assumptions.

---

# 546. CR22 — Goal

Existing-customer commercial ownership/segment context.

---

# 547. CR22 Build

CrmPartyProfile.

---

# 548. CR22 Exit Gate

No duplicate Party master fields.

---

# 549. CR23 — Goal

Credible sales forecast.

---

# 550. CR23 Build

Forecast category, weighted values, reporting currency context, manager view.

---

# 551. CR23 Exit Gate

Forecast labels never imply accounting revenue.

---

# 552. CR24 — Goal

Simple sales targets where needed.

---

# 553. CR24 Build

Owner/team period targets for CRM metrics.

---

# 554. CR24 Exit Gate

Targets can be disabled entirely.

---

# 555. CR25 — Goal

Scale inbound routing.

---

# 556. CR25 Build

Round-robin + rule assignment with deterministic explanation.

---

# 557. CR25 Exit Gate

Every auto-assigned Lead explains rule/reason.

---

# 558. CR26 — Goal

Optional cadence/sequence support.

---

# 559. CR26 Build

Versioned steps, enrollment, due activities, stop conditions.

---

# 560. CR26 Exit Gate

No unwanted duplicate follow-ups after stop condition.

---

# 561. CR27 — Goal

Bounded CRM automation.

---

# 562. CR27 Build

Typed triggers/conditions/actions, execution history, loop prevention.

---

# 563. CR27 Exit Gate

No arbitrary scripts.

---

# 564. CR28 — Goal

Migrate from spreadsheets/legacy CRM.

---

# 565. CR28 Build

Lead/opportunity/activity imports, owner/source mapping, duplicate preview, legacy refs.

---

# 566. CR28 Exit Gate

No silent skipped records; migration report explicit.

---

# 567. CR29 — Goal

Manager analytics from trustworthy operational data.

---

# 568. CR29 Build

Conversion, win/loss, cycle, source, stage duration, stale, forecast reports.

---

# 569. CR29 Exit Gate

Every KPI has documented definition.

---

# 570. CR30 — Goal

Production visibility and audit.

---

# 571. CR30 Build

Own/team/entity scopes, permissions, financial-summary permission, export/audit.

---

# 572. CR30 Exit Gate

Frontend hidden fields/actions are never the only security.

---

# 573. CR31 — Goal

Allow market/customer variation without forks.

---

# 574. CR31 Build

Custom fields, stage requirements, saved views.

---

# 575. CR31 Exit Gate

Core critical semantics remain typed.

---

# 576. CR32 — Goal

Prepare AI without making AI source of truth.

---

# 577. CR32 Build

Authorized CRM query/tool contracts for summary, score suggestions, next-action suggestions, draft outreach.

---

# 578. CR32 Exit Gate

AI cannot bypass business commands/permissions.

---

# 579. CR33 — Goal

Prove scale.

---

# 580. CR33 Load Tests

At minimum:

```text
1M Leads
1M historical Opportunities
10M Activities
500 sellers
large Team pipeline
large Customer 360 history
lead import burst
external capture burst
forecast aggregation
```

---

# 581. CR33 Query Plan Evidence

Use real PostgreSQL:

```text
EXPLAIN (ANALYZE, BUFFERS)
```

for hot paths.

---

# 582. CR33 Exit Gate

Targets met or evidence-based ADR for optimization.

---

# 583. CR34 — Goal

Security/privacy hardening.

---

# 584. CR34 Red-Team

Attack:

```text
cross-tenant read
seller IDOR
team scope bypass
financial summary leakage
external lead spam/replay
rich-text XSS
file access leak
provider secret exposure
quote-handoff permission bypass
export scope bypass
automation privilege escalation
```

---

# 585. CR34 Exit Gate

RLS + application auth + input security pass.

---

# 586. CR35 — Goal

Supported recovery, not SQL repair.

---

# 587. CR35 Runbooks

Required:

```text
Duplicate Lead
Wrong Lead owner
Lead conversion conflict
Directory match ambiguity
Opportunity stage conflict
Stale health projection
External webhook duplicate/failure
Email/WhatsApp sync issue
Quotation handoff failure
Automation failure/loop prevention
Cadence duplicate prevention
Pipeline projection rebuild
Import rollback/correction
Restore/DR validation
```

---

# 588. CR35 Forbidden Runbook

No ordinary:

```text
UPDATE opportunity SET stage_id ...
DELETE lead ...
UPDATE party_balance ...
```

---

# 589. CR36 — Formal Seller Acceptance Test

Use salesperson not involved in implementation.

---

# 590. CR36 Scenario A — Start Day

Seller opens CRM and immediately sees:

```text
who to call
what is overdue
new leads
at-risk deals
```

---

# 591. CR36 Scenario B — New Lead

Create in seconds.

---

# 592. CR36 Scenario C — Qualify

Contact lead, record outcome, schedule next action, qualify.

---

# 593. CR36 Scenario D — Opportunity

Create/convert Opportunity, identify contacts, move stages, update expected close.

---

# 594. CR36 Scenario E — Quotation

Create Sales Quotation with minimal re-entry.

---

# 595. CR36 Scenario F — Follow-Up

Complete activity + next action quickly.

---

# 596. CR36 Scenario G — Win/Loss

Mark outcome and reason without cumbersome workflow.

---

# 597. CR36 Pass Criteria

Seller confirms:

```text
CRM helps me remember/prioritize
I do not re-enter customer data unnecessarily
I can see customer context
routine actions are fast
```

---

# 598. CR37 — Formal Sales Manager Acceptance

---

# 599. CR37 Scenario A — Pipeline Inspection

Manager identifies:

```text
largest deals
at-risk deals
stale deals
overdue expected closes
no-next-action deals
```

quickly.

---

# 600. CR37 Scenario B — Forecast

Manager separates:

```text
Pipeline
Best Case
Commit
Won
```

and understands reporting currency.

---

# 601. CR37 Scenario C — Team Performance

Review:

```text
lead response
conversion
win/loss
stage duration
source performance
```

with clear definitions.

---

# 602. CR37 Scenario D — Pipeline Hygiene

Manager finds/cleans stale opportunities without spreadsheet.

---

# 603. CR37 Pass Criteria

Manager trusts CRM pipeline enough to run sales meeting from TODIR.

---

# 604. CR38 — CRM → Sales E2E Acceptance

Scenario:

```text
External Lead
→ Lead Inbox
→ Activity
→ Qualify
→ Directory Party/Customer Role
→ Opportunity
→ Buying Committee
→ Create Quotation
→ Quotation Accepted
→ Opportunity Won
```

Verify no duplicated ownership truth.

---

# 605. CR38 Finance Boundary

Then:

```text
Sales/Invoice/Payment
```

appear in Customer 360 only through owning modules.

---

# 606. CR38 Exit Gate

CRM never needs direct Sales/Invoicing/Subledger tables.

---

# 607. CR39 — Data Quality / Pipeline Hygiene Acceptance

Seed:

```text
unowned lead
uncontacted lead
stale opportunity
no-next-action opportunity
overdue close date
expired quotation
healthy opportunity
```

---

# 608. CR39 Exit Gate

Each issue appears correctly; healthy deal has no false warning.

---

# 609. CR40 — Release Gate

CRM does not ship because Kanban looks good.

It ships when seller adoption + pipeline credibility are proven.

---

# 610. CR40 Required Sign-Off

```text
Product Architect
Sales/CRM Domain Lead
Experienced Salesperson Reviewer
Sales Manager Reviewer
Directory Owner
Sales Module Owner
Backend Lead
Frontend Lead
Security Reviewer
Performance Engineer
QA Lead
```

---

# 611. CR40 Mandatory Evidence

```text
Automated tests
RLS/visibility report
Lead conversion idempotency
Directory duplicate integration tests
CRM→Sales E2E
Pipeline hygiene test
Seller Acceptance
Sales Manager Acceptance
Performance benchmarks
Import/migration test
Known limitations
```

---

# 612. Build Now — MUST

```text
Lead Inbox
Lead Source
Minimal Lead Capture
Duplicate Detection
Lead Assignment
Qualification/Disqualification
Directory Conversion
Opportunity
Pipelines/Stages
Stage History
Activities
Next Action
Seller Worklist
Deal Health
Stale Deal Detection
Buying Committee
Opportunity Interests
CRM → Sales Quotation
Won/Lost
Loss Reasons
Customer 360
Commercial Timeline
CRM Party Profile
Forecast Categories
Basic Forecast
Permissions/Scopes
Audit
Custom Fields
Import/Export
Core Reports
Performance
Security
Runbooks
Acceptance Gates
```

---

# 613. Build Next / High Value

```text
Advanced routing
Cadences/Sequences
CRM Automation
Email Sync
WhatsApp integration
Calendar integration
Targets/Quotas
AI summaries
AI next-action suggestions
AI scoring when data sufficient
```

---

# 614. Build Later / Evidence-Based

```text
Full Marketing Automation
Campaign Journey Builder
Complex Attribution
Territory Management Suite
Partner/Reseller Portal
Conversation Intelligence transcription
Telephony platform
Autonomous Sales Agent
Advanced Account Planning
Revenue Intelligence warehouse
Customer Success module
Helpdesk/Support
```

---

# 615. Fresh Small Company Defaults

Recommended:

```text
Lead Inbox                ON
One Pipeline              ON
Lead Scoring              OFF
Advanced Qualification    OFF
Manual Assignment         ON
Teams                     optional
Next Action               ON
Deal Health               ON simple
Forecast Categories       simple/optional
Cadences                  OFF
Automation                OFF/simple
AI                         OFF until connected/configured
Customer 360              ON
CRM → Sales Quote         ON
```

---

# 616. Larger Sales Organization Capabilities

Enable:

```text
Multiple Pipelines
Teams
Round Robin
SLA
Qualification Rules
Buying Committee
Forecast Categories
Targets
Cadences
Automation
Relationship Health
Advanced reporting
AI insights
```

---

# 617. CRM Setup Wizard

Small company:

```text
1. Name default Pipeline
2. Confirm stages
3. Add/assign sales users
4. Confirm Lead Sources
5. Choose stale thresholds
6. Start
```

No huge configuration project.

---

# 618. Existing Company Migration Setup

Add:

```text
Lead import
Opportunity import
Stage mapping
Owner mapping
Directory matching
Historical boundary
```

---

# 619. Seller Home Final Principle

> **TODIR CRM should answer “What should I do next?” before it asks the seller to update another field.**

---

# 620. Pipeline Final Principle

> **An active pipeline must represent opportunities the company is genuinely pursuing; unqualified or dead records must not quietly inflate it.**

---

# 621. Data Entry Final Principle

> **Capture data automatically when safe, infer defaults when reliable, and ask the seller only for information that changes the next commercial decision.**

---

# 622. Lead Final Rule

> **A Lead is temporary prospect context; it becomes canonical Party data only through controlled qualification/conversion.**

---

# 623. Opportunity Final Rule

> **An Opportunity is a commercial possibility, not a Sales Order, Invoice, receivable or revenue entry.**

---

# 624. Activity Final Rule

> **Every important Lead/Opportunity should have a clear owner and next commercial action unless intentionally paused/closed.**

---

# 625. Health Final Rule

> **Deal health must be explainable. AI may add insight, but TODIR must always expose concrete risk signals.**

---

# 626. Forecast Final Rule

> **Pipeline and forecast are sales-management estimates; accounting revenue and cash remain separate truths.**

---

# 627. Customer 360 Final Rule

> **CRM composes customer context from owning domains; it does not copy the ERP into CRM tables.**

---

# 628. Sales Handoff Final Rule

> **CRM owns the pursuit; Sales owns the formal quotation/order. Handoff should feel like one action to the seller without merging domain ownership.**

---

# 629. Automation Final Rule

> **Automation removes repetitive selling administration; it must not become an opaque workflow engine that controls sellers unnecessarily.**

---

# 630. AI Final Rule

> **AI suggests, summarizes and prioritizes; authorized TODIR commands remain the path for commercial state changes.**

---

# 631. Final Source-of-Truth Matrix

```text
Early Lead / Qualification          → CRM
Lead Source / Assignment            → CRM
Opportunity / Pipeline / Stage      → CRM
Activities / Next Action            → CRM
Deal Health / Forecast              → CRM
CRM Team / Routing                  → CRM

Canonical Party / Contact           → Directory
Product                             → Catalog
Price Rules                         → Pricing
Quotation / Sales Order             → Sales
Invoice                             → Invoicing
Receivable / Credit Exposure        → Subledger
Payment                             → Payments
Revenue / Profit                    → Ledger / Accounting
```

---

# 632. Final Simple Seller Flow

```text
New Lead
→ Contact
→ Set Next Action
→ Qualify
→ Opportunity
→ Create Quotation
→ Follow Up
→ Won / Lost
```

---

# 633. Final Advanced Seller Flow

```text
Inbound Lead
→ Routing
→ Qualification
→ Cadence
→ Opportunity
→ Buying Committee
→ Deal Health
→ Forecast
→ Multiple Quotations
→ Negotiation
→ Won
→ Sales/Post-Sale Handoff
```

Only enabled complexity appears.

---

# 634. Final Manager Flow

```text
Pipeline Inspection
→ At-Risk / Stale
→ Coach / Reassign / Update
→ Forecast
→ Win/Loss Review
→ Source / Conversion Review
```

---

# 635. Final Commercial Trust Requirement

If management cannot explain why an Opportunity is:

```text
in pipeline
at this stage
worth this amount
expected this date
healthy/at-risk
owned by this seller
```

then the pipeline is not trustworthy.

---

# 636. Final Product Verdict

TODIR CRM is approved only when:

```text
Lead does not pollute Directory before qualification.
Lead Inbox keeps unqualified prospects outside active pipeline.
Duplicate detection works without unsafe auto-merge.
Lead conversion is idempotent and reuses Directory Party.
Opportunity is distinct from Sales/Invoice/Revenue.
Pipeline is configurable but simple by default.
Stage history is permanent.
Seller always has clear next-action support.
Activities are fast to complete/schedule.
Deal health is explainable.
Stale opportunities are surfaced.
Won/Lost reasons are structured.
Buying committee uses Directory Contacts.
Customer 360 composes real ERP sources.
CRM→Sales Quotation handoff avoids re-entry and preserves ownership.
Forecast is clearly separated from revenue.
Seller/manager visibility is backend-enforced.
External lead capture is idempotent and abuse-resistant.
Automation is typed and loop-safe.
AI cannot bypass permissions/business commands.
Small-company CRM stays simple.
Large-company controls can expand without replacing the core.
Seller Acceptance passes.
Sales Manager Acceptance passes.
CRM→Sales E2E passes.
Pipeline Hygiene Acceptance passes.
```

---

# 637. Final Agent Rule

For every CRM feature, the Agent must answer:

> **What commercial decision does this data help someone make, who owns the underlying truth, what should the seller do next, how do we prevent stale/duplicate data, and how do we prove the pipeline is honest?**

If those answers are unclear, implementation is incomplete.

---

# 638. No Separate CRM Subfeature Packs

Do **not** create separate design packs for:

```text
Leads
Opportunities
Activities
Pipeline
Forecast
Cadences
CRM Automation
Customer 360
```

They are all inside `19 CRM`.

---

# 639. Next Planning Rule

Continue one comprehensive file per actual business module.

Do not split CRM further.

---

**END OF FILE — 19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md**
