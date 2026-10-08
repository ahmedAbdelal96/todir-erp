# 29 — TODIR ERP SaaS Helpdesk & Customer Support Requirements & Technical Design Pack

**File:** `29_ERP_SAAS_HELPDESK_CUSTOMER_SUPPORT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + SUPPORT CASES + OMNICHANNEL + SLA + ROUTING + KNOWLEDGE + PORTAL + AI ASSISTANCE + CROSS-MODULE HANDOFFS + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-07  
**Audience:** Product Owner, Product Architect, Customer Support Manager, Support Agent, Contact Center Lead, Customer Experience Lead, Service Manager, Quality Manager, CRM/Sales Lead, Portal Product Lead, Backend/Frontend Leads, Integration Architect, Security Engineer, Privacy Lead, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **HIGH — CUSTOMER COMMUNICATION + SLA + PRIVACY + CUSTOMER TRUST + OPERATIONAL HANDOFFS + OMNICHANNEL DATA**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `13_ERP_SAAS_INVENTORY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

### Locked upstream truths

- Directory owns canonical Party, Customer and Contact identity.
- CRM explicitly leaves customer support tickets to a future Support/Helpdesk capability.
- CRM owns commercial Leads, Opportunities, seller activities and commercial relationship profile.
- Service & Maintenance owns service demand, Installed Base, Service Requests, Work Orders, Visits, technicians, warranty/service-contract execution context and service-billing eligibility.
- Quality owns formal Quality Issues, NCR, disposition, CAPA and quality investigation.
- Sales owns customer commercial orders and return orchestration where applicable.
- Invoicing owns legal invoices/credit notes.
- Payments owns actual money movement/refunds.
- Inventory owns physical stock, shipment/return movements, lots/serials and availability.
- Subledger owns receivable/open-item truth.
- Platform owns Files, Audit, Notifications, Jobs, Custom Fields and Approvals.
- External/portal identity is not the same entity as Directory Contact.
- No Support code may directly write CRM, Service, Quality, Sales, Inventory, Invoicing, Payments or Subledger tables.
- Cross-domain actions must use approved contracts/commands.
- Support channel providers are transport adapters, not Support domain truth.
- AI follows `AI → Tool Layer → Permissions → Application Commands/Queries → Domain`; never AI → Database.

---

# 0. Executive Decision

TODIR `29 Helpdesk & Customer Support` is the canonical customer-support capability that answers:

> **What problem or request did the customer contact us about, through which channel, who is the customer/requester, what business context matters, who owns the response, what SLA applies, what has already been communicated, what internal or downstream work is required, what resolution was delivered, and can TODIR prove the entire customer-support journey afterward?**

The module is **not** merely a Ticket table.

It is:

```text
Customer Support Case Core
+
Omnichannel Conversations
+
Agent Work Queues
+
Routing / Capacity / Skills foundation
+
Support SLA
+
Support Entitlement / Policy
+
Knowledge Base
+
Customer Self-Service / Portal
+
Escalation / Collaboration
+
AI Agent Assistance
+
Cross-module Handoffs
+
Support Analytics / CSAT
```

---

# 1. Central Product Question

> **Can a customer contact the company once, through the channel they naturally use, and have TODIR preserve context until the issue is truly resolved without forcing the customer to repeat their story across Sales, Service, Quality, Finance or Support teams?**

---

# 2. Product Law — One Customer Problem, One Coherent Case

One business issue may involve multiple communications:

```text
WhatsApp
Email
Phone
Portal
Live Chat
```

That does **not** automatically mean five Tickets.

---

# 3. Product Law — Channel Is Transport, Case Is Business Meaning

```text
Message
!=
Conversation
!=
Support Case
```

Absolute.

---

# 4. Product Law — Domain Separation Must Not Fragment Customer Experience

The Support agent may press:

```text
[Create Service Request]
[Create Quality Issue]
[Request Return]
[Open CRM Lead]
[Request Refund Review]
```

from one workspace.

The downstream owning module still owns the resulting business object.

---

# 5. Product Law — Customer Must Not Repeat Context

When authorized, the agent should see relevant read-only context such as:

```text
Customer
Contacts
Recent orders
Invoices
Outstanding summary
Payments summary
Products purchased
Delivery/return context
Installed equipment
Warranty/service contract outcome
Prior service work
Prior tickets
Open quality case
```

without asking the customer to repeat information TODIR already knows.

---

# 6. Product Law — Support Is Not A Second ERP

Support owns the customer-support case and conversation.

It does not own:

```text
Order
Invoice
Payment
Refund
Inventory movement
Service Work Order
NCR
Opportunity
```

---

# 7. Product Law — Small Company First

A small company must be able to run:

```text
Customer message
→ Ticket
→ Assign
→ Reply
→ Resolve
```

without configuring:

```text
skills
complex routing
entitlements
multiple SLA KPIs
contact-center presence
AI routing
customer portal roles
major-issue hierarchy
```

unless needed.

---

# 8. Product Law — Enterprise Depth Is Optional

Larger organizations can enable:

```text
Omnichannel queues
Business-hour SLA
Skills
Capacity
Entitlements
Escalation matrix
B2B account access
Knowledge governance
Portal
Quality/Service handoffs
AI assistance
Advanced analytics
```

without replacing Support Core.

---

# 9. Absolute Domain Distinctions

```text
Support Case != Conversation
Conversation != Message

Support Case != CRM Opportunity
Support Case != Sales Order
Support Case != Service Request
Support Case != Work Order
Support Case != Quality Issue
Support Case != NCR
Support Case != Customer Return
Support Case != Refund
Support Case != Invoice
Support Case != Payment
```

---

# 10. Lifecycle Distinctions

```text
Resolved != Closed
Open != Waiting On Customer
Open != Waiting On Internal Team
Reopened != New
Merged != Deleted
```

---

# 11. Classification Distinctions

```text
Priority != Severity
Category != Resolution
Customer Tier != Case Priority
Support Entitlement != Service Warranty
Support SLA != Service Work SLA
```

---

# 12. Workforce Distinctions

```text
Support Agent != Employee
Agent Presence != HR Attendance
Queue Membership != HR Department
Support Skill != HR Position
```

An Agent may reference an IAM User/Employee, but Support routing state remains Support-owned.

---

# 13. Communication Distinctions

```text
Customer-visible Reply != Internal Note
Sent != Delivered
Delivered != Read
Acknowledgement != Meaningful First Response
AI Draft != Sent Reply
```

---

# 14. Knowledge Distinctions

```text
Knowledge Article != Ticket
Knowledge Article != AI Answer
Draft Article != Published Article
Internal Article != Public Article
```

---

# 15. Support Contexts

Initial:

```text
General Customer Support
Order/Delivery Support
Billing/Invoice Support
Payment Support
Product Support
Technical Support
Return/Refund Request Intake
Field Service Intake
Quality Complaint Intake
Account/Portal Support
```

These are case categories/routing contexts, not duplicate domain implementations.

---

# 16. Module Name

Technical module key recommendation:

```text
support
```

Projects:

```text
Todir.Modules.Support.Domain
Todir.Modules.Support.Application
Todir.Modules.Support.Infrastructure
Todir.Support.Contracts
```

---

# 17. Database Schema

Recommended:

```text
business_support
```

---

# 18. DbContext

```text
SupportDbContext
```

Support tables only.

---

# 19. Support Case

Canonical entity:

```text
SupportCase
```

UI may call it:

```text
Ticket
```

for familiar/simple UX.

---

# 20. Why `SupportCase` Internally

A case can survive:

```text
channel changes
multiple conversations
agent transfers
internal escalations
service/quality handoffs
```

while remaining one customer problem/request.

---

# 21. Support Case Concept

```text
SupportCase
{
    Id
    Number
    TenantId
    OwningLegalEntityId?
    Subject
    DescriptionSnapshot?
    RequesterRef?
    CustomerPartyRef?
    State
    WaitingOn?
    Priority
    Severity?
    CategoryRef?
    QueueRef?
    AssignedAgentRef?
    SupportPolicySnapshotRef?
    CreatedAt
    ResolvedAt?
    ClosedAt?
}
```

---

# 22. Case Numbering

Use Platform Numbering.

Example:

```text
SUP-2026-000123
```

No custom sequence engine inside Support.

---

# 23. Owning Legal Entity

A case may initially enter an unclassified/shared tenant intake queue.

Once business context is known, resolve owning Legal Entity where applicable.

---

# 24. Shared Group Support Center

A multi-entity tenant may run one shared support center.

The case still records clear organizational ownership before any entity-specific financial/service action.

---

# 25. Requester

Requester is the person/channel identity contacting support.

Requester may be:

```text
Directory Contact
Portal Identity linked to Contact
Unknown inbound email
Unknown WhatsApp number
Anonymous web form user
```

according to channel/policy.

---

# 26. Requester != Customer Account

B2B example:

```text
Customer Party = ABC Manufacturing LLC
Requester       = Mona, Maintenance Manager
```

---

# 27. A Contact Can Represent Multiple Parties

Do not assume one email/phone always maps to exactly one customer account.

If ambiguous, agent selects/validates context.

---

# 28. Unknown Requester

Support must not force every inbound sender into Directory immediately.

Use a Support-local external requester/channel identity until validated/matched.

---

# 29. Unknown Requester Concept

```text
SupportExternalIdentity
{
    ChannelType
    NormalizedAddress
    DisplayName?
    ProviderIdentityRef?
    VerificationState
    LinkedContactRef?
}
```

This is communication identity, not canonical Party master.

---

# 30. Directory Promotion Rule

When appropriate:

```text
Search Directory
→ Link existing Contact/Party
or
→ authorized creation through Directory command
```

Never silently create duplicate customers from every inbound message.

---

# 31. Identity Match Order

Suggested:

```text
Verified portal identity
Explicit provider/customer mapping
Verified exact email/phone/contact channel
Known external reference
Agent-confirmed match
```

Never auto-match by name alone.

---

# 32. Identity Reassignment

Changing the customer/requester link after case creation requires:

```text
permission
reason
audit
```

because it can expose private customer data.

---

# 33. Case Subject

Short human-readable summary.

Can be user-entered, extracted from email subject, or AI-suggested.

AI suggestion is editable and not authoritative.

---

# 34. Case Description

Initial customer issue snapshot.

Original inbound message remains in Conversation history.

---

# 35. Case Category

Versioned controlled taxonomy.

Examples:

```text
Order
Delivery
Invoice
Payment
Return
Refund
Product Question
Technical Issue
Warranty
Service
Quality Complaint
Portal/Login
Other
```

---

# 36. Category Hierarchy

Optional two/three level:

```text
Technical
  → Generator
     → Unexpected Shutdown
```

Avoid unlimited deep category trees.

---

# 37. Category Versioning

Historical cases retain category label/code snapshot if taxonomy later changes.

---

# 38. Case Type

Optional higher-level classification:

```text
Question
Incident
Request
Complaint
```

Do not confuse Support `Incident` with ITSM major incident management.

---

# 39. Complaint

A Customer Complaint is a support/business intake meaning.

It is not automatically a Quality NCR.

---

# 40. Quality Handoff

When complaint indicates possible nonconformance:

```text
Support Case
→ Create/Link Quality Issue
```

`27 Quality` decides whether NCR exists.

Quality itself defines Complaint != NCR; Support preserves customer communication context.

---

# 41. Product Context

Case may reference:

```text
ProductVariantRef
```

from Catalog.

Support does not duplicate Product.

---

# 42. Sales Context

Case may reference:

```text
Quotation
Sales Order
Delivery
Return
```

through contracts/typed external refs.

---

# 43. Invoice Context

Case can reference invoice/credit-note source.

Invoicing remains owner.

---

# 44. Payment Context

Case can reference payment/refund/payment attempt.

Payments remains owner.

---

# 45. Service Context

Case may link:

```text
Installed Base Item
Service Request
Work Order
Visit
Warranty/Contract resolution
```

from `24`.

---

# 46. Quality Context

Case may link:

```text
Quality Issue
NCR
CAPA public status summary where permitted
```

from `27`.

---

# 47. Generic Case Reference

Typed reference model:

```text
SupportCaseReference
{
    CaseId
    ReferenceType
    ReferenceId
    DisplaySnapshot?
    RelationshipType
}
```

No cross-module FK required.

---

# 48. Reference Types

Potential:

```text
Party
Contact
Product
SalesOrder
Delivery
Invoice
Payment
InventoryLot
InventorySerial
InstalledBaseItem
ServiceRequest
WorkOrder
QualityIssue
NCR
CRMOpportunity
Project
```

Add only approved types.

---

# 49. Reference Permission

A linked business object does not automatically grant the Support agent full source-domain access.

---

# 50. Support Context Read Model

Build a customer/context projection composed from approved module contracts.

Do not copy canonical source data into editable Support tables.

---

# 51. Customer 360 Alignment

CRM Customer 360 remains commercial workspace.

Support workspace composes a **Support Customer Context** optimized for issue resolution.

---

# 52. Support Customer Context

Recommended summary:

```text
Customer / Requester
Language
Support tier
Open tickets
Recent orders
Recent invoices
Outstanding summary if permitted
Recent products
Installed equipment
Warranty/service contract outcome
Recent service work
Quality alerts/cases relevant to this issue
```

---

# 53. Financial Privacy

Support agent does not automatically see:

```text
full account ledger
banking details
all payment instruments
profitability
internal credit analysis
```

Show only job-required summary.

---

# 54. Case State

Recommended core states:

```text
New
Open
Resolved
Closed
Cancelled
Merged
```

---

# 55. Why Keep States Small

Do not encode every waiting reason as a separate state.

Use a separate `WaitingOn`.

---

# 56. Waiting On

Recommended:

```text
None
Customer
InternalTeam
FieldService
Quality
Sales
Finance
ThirdParty
Provider
```

extensible controlled codes.

---

# 57. Open + Waiting Customer

Valid combination:

```text
State     = Open
WaitingOn = Customer
```

---

# 58. Open + Waiting Internal

Valid combination:

```text
State     = Open
WaitingOn = InternalTeam
```

---

# 59. Resolved

Support team believes the customer issue/request has been addressed.

---

# 60. Closed

The support lifecycle has ended according to company policy.

---

# 61. Auto Close

Optional:

```text
Resolved
→ no customer reply for N days
→ Closed
```

via Job.

---

# 62. Reopen

A customer reply or authorized agent action may reopen according to policy.

---

# 63. Reopen Window

Policy may define:

```text
7 days
30 days
Always
Never; create follow-up case
```

---

# 64. Late Reply After Close

Options:

```text
Reopen original
Create follow-up linked case
Agent decision
```

policy-driven.

---

# 65. State History

Every lifecycle change stores:

```text
from
to
actor
timestamp
reason/context
```

---

# 66. Waiting History

Track periods of:

```text
Waiting Customer
Waiting Internal
Waiting Third Party
```

for SLA/analytics.

---

# 67. Priority

Represents work ordering/urgency for Support operations.

Recommended:

```text
Low
Normal
High
Urgent
```

---

# 68. Severity

Represents business impact/technical seriousness.

Recommended foundation:

```text
S1 Critical
S2 High
S3 Medium
S4 Low
```

labels configurable.

---

# 69. Priority != Severity Example

```text
VIP billing-document request:
Severity = Low
Priority = High
```

Valid.

---

# 70. Critical Severity

Examples:

```text
Customer production is completely stopped
Safety-critical failure
Widespread inability to use paid service
```

according to business policy.

---

# 71. Severity Determination

Can consider:

```text
Affected users/sites
Business impact
Workaround available
Safety/regulatory risk
Revenue/service impact
```

---

# 72. Priority Inputs

Can consider:

```text
Severity
Customer tier
SLA risk
Age
Escalation
Business importance
```

but output must remain explainable.

---

# 73. AI Priority Suggestion

Allowed.

AI cannot silently downgrade or close a critical case.

---

# 74. Case Ownership

Support ownership layers:

```text
Support Team
Queue
Assigned Agent
```

These are distinct.

---

# 75. Support Team

Organizational support unit.

Examples:

```text
Customer Care
Technical Support
Billing Support
Arabic Support
Enterprise Support
```

---

# 76. Queue

Operational work bucket.

Examples:

```text
New / Unassigned
Critical
Arabic
Billing
Technical
Returns
VIP
```

---

# 77. Queue != Team

A Team can own multiple queues.

A queue can have authorized members across teams if configured.

---

# 78. Agent

IAM user authorized to work Support cases.

Employee link optional/useful but Support does not own Employee identity.

---

# 79. Assignment

One active assigned agent by default.

Optional collaborators do not obscure primary ownership.

---

# 80. Case Collaborators

Internal users can be:

```text
Follower
Watcher
Subject Matter Expert
Approver
```

without becoming assigned owner.

---

# 81. Assignment History

Persist:

```text
queue
agent
assigned_at
unassigned_at
reason
routing_method
```

---

# 82. Manual Assignment

Day-1 required.

---

# 83. Round-Robin Routing

Optional simple automated routing.

---

# 84. Least-Loaded Routing

Uses Support capacity/workload, not HR attendance.

---

# 85. Skill-Based Routing

Advanced capability.

Potential skills:

```text
Arabic
English
Product Family
Technical Certification
Billing
Enterprise Support
```

---

# 86. Language Routing

A case detected/selected Arabic can prefer Arabic-capable agents.

---

# 87. Capacity Routing

Agent can have configured concurrent capacity.

Different work items may consume different capacity weight later.

---

# 88. Agent Presence

Support-owned operational state:

```text
Available
Busy
Away
Offline
```

---

# 89. Agent Presence != Attendance

Absolute.

Presence cannot be used as payroll attendance truth.

---

# 90. Routing Inputs

Potential:

```text
Queue
Category
Language
Priority
Severity
Customer tier
Required skill
Agent availability
Agent capacity
SLA risk
Preferred ownership
```

---

# 91. Routing Rule

Versioned deterministic rule.

---

# 92. Routing Rule Concept

```text
SupportRoutingRuleVersion
{
    Id
    Priority
    Conditions
    TargetQueue
    SkillRequirements?
    AssignmentMode
    EffectiveFrom
    EffectiveTo?
}
```

---

# 93. No Arbitrary Routing Code

No tenant SQL/C#/JS/Python.

Use constrained conditions/actions.

---

# 94. Routing Explainability

Agent/manager can answer:

```text
Why was this case routed here?
```

---

# 95. Routing Fallback

If no eligible agent:

```text
keep in queue
escalate unassigned SLA risk
```

Never silently assign outside permissions/skills.

---

# 96. Queue Overflow

Advanced:

```text
after N minutes
→ backup queue/team
```

versioned.

---

# 97. Manual Override

Supervisor can reassign with:

```text
permission
reason
audit
```

---

# 98. Support Channel

A channel is transport/interaction origin.

Initial model supports:

```text
Email
WhatsApp
Portal
WebForm
LiveChat
Phone
Manual
API
```

---

# 99. Later Channels

Potential:

```text
Facebook Messenger
Instagram
Marketplace messaging
SMS
Other provider adapters
```

not hardcoded in Support Core.

---

# 100. Provider Adapter Principle

Support domain sees normalized:

```text
Conversation
Message
Participant
Delivery state
```

Provider adapter handles channel-specific APIs/policies.

---

# 101. Channel Account

Represents configured company endpoint such as:

```text
support@company.com
WhatsApp Business number
Portal brand
Live-chat widget
```

---

# 102. Channel Account Concept

```text
SupportChannelAccount
{
    Id
    ChannelType
    ProviderKey
    ExternalAccountRef
    LegalEntityScope?
    BrandRef?
    DefaultQueueRef?
    State
}
```

Secrets stay in secret/config provider, not business table.

---

# 103. Conversation

A `SupportConversation` is a channel/thread context attached to a Support Case.

---

# 104. Conversation Concept

```text
SupportConversation
{
    Id
    CaseId
    ChannelType
    ChannelAccountId
    ExternalThreadRef?
    State
    StartedAt
    LastMessageAt
}
```

---

# 105. One Case Multiple Conversations

Example:

```text
Case SUP-100
├ Email thread
├ WhatsApp conversation
└ Phone call log
```

---

# 106. One Conversation One Case At A Time

A channel thread belongs to one active case relationship.

Moving/relinking requires controlled operation/history.

---

# 107. Message

A message is one inbound/outbound/internal communication record.

---

# 108. Message Direction

```text
Inbound
Outbound
Internal
System
```

---

# 109. Internal Note

Must never be sent to customer.

Internal note UI and API are distinct from customer reply.

---

# 110. Customer Reply

Creates outbound channel message through provider adapter.

---

# 111. Message Concept

```text
SupportMessage
{
    Id
    ConversationId
    Direction
    Visibility
    AuthorType
    AuthorRef?
    BodyNormalized
    BodyFormat
    ProviderMessageRef?
    CreatedAt
    SentAt?
    DeliveredAt?
    ReadAt?
    DeliveryState
}
```

---

# 112. Message Immutability

Once externally sent/received, content is not silently edited.

Correction occurs by follow-up message.

---

# 113. Draft Reply

Draft can be editable before send.

---

# 114. Delivery States

Normalized:

```text
Draft
Queued
Sent
Delivered
Read
Failed
Unknown
```

not every provider must support every state.

---

# 115. Delivered != Read

Absolute.

---

# 116. Send Retry

Must be idempotent where provider supports stable client/provider keys.

---

# 117. Duplicate Webhook

Provider may deliver same webhook more than once.

Inbox/idempotency mandatory.

---

# 118. Out-Of-Order Webhook

Delivery/read callbacks may arrive out of order.

State transition logic must be monotonic/defensive.

---

# 119. Message Provider Metadata

Store bounded normalized metadata and external refs.

Do not make arbitrary provider JSON canonical domain.

---

# 120. Raw Provider Evidence

Optional secured diagnostic blob/reference with retention limit.

Not exposed to ordinary agents.

---

# 121. Email Channel

Supports:

```text
Inbound message
Threading
Reply
Attachments
CC where approved
Bounce/failure
```

without becoming general mailbox client.

---

# 122. Email Threading

Use provider message/thread refs plus standards headers when available.

Do not create a new case for every reply.

---

# 123. Email Subject Change

Thread association uses stronger identity than subject text alone.

---

# 124. Email Loop Protection

Detect/support safeguards for:

```text
auto-replies
mail loops
delivery failures
vacation responders
```

---

# 125. Email Spoofing / Trust

Do not treat sender address alone as strong authentication for sensitive actions.

---

# 126. Sensitive Email Request

Actions such as account change/refund/security-sensitive request may require identity verification.

---

# 127. WhatsApp Channel

First-class market channel through provider adapter.

---

# 128. WhatsApp Domain Rule

Support Core does not hardcode vendor session/template rules.

Adapter exposes capabilities/policy state such as:

```text
freeform reply allowed?
approved template required?
message type supported?
```

---

# 129. WhatsApp Customer-Service Window

Provider adapter enforces current provider rules.

Support UI clearly shows if free-form reply is unavailable and template is required.

---

# 130. WhatsApp Templates

Provider-owned/adapter-synchronized templates.

Support can select approved template by purpose/locale.

---

# 131. WhatsApp Opt-In / Policy

Consent/provider policy handling must be explicit and country/provider compliant.

Do not infer marketing consent from support conversation.

---

# 132. WhatsApp Support != Marketing

A customer support interaction cannot automatically enroll the person in marketing campaigns.

---

# 133. Phone Channel Day 1

Support:

```text
Manual call log
Call outcome
Callback task
Phone number/context
Recording reference if provider supports
```

---

# 134. CTI Seam

Future adapter:

```text
screen pop
click-to-call
inbound call routing
call recording ref
call disposition
```

---

# 135. Do Not Build Telephony Platform

TODIR integrates with telephony/contact-center providers.

It does not implement PSTN/IVR infrastructure from scratch.

---

# 136. Live Chat

Provider/widget seam.

Conversation can be promoted into a persistent Support Case.

---

# 137. Web Form

Can create Case or pre-case intake.

Includes:

```text
anti-spam
rate limit
attachment controls
email verification where needed
```

---

# 138. Customer Portal

Authenticated self-service channel.

---

# 139. Portal Core Actions

```text
Create Ticket
View authorized Tickets
Reply
Upload attachment
See status
See approved public resolution
Search Knowledge
Rate support
```

---

# 140. Portal Identity

Portal identity is not Directory Contact itself.

Link:

```text
External Identity
↔ Directory Contact
↔ Party
```

through approved external-access/IAM foundation.

---

# 141. B2B Portal Access

Support:

```text
Requester-only access
Account Support Admin access
Selected-site access
```

through explicit authorization.

---

# 142. No Automatic Company-Wide Visibility

Being a Contact at ABC does not automatically allow viewing every ABC ticket.

---

# 143. Portal Support Admin

An authorized external role may view all support cases for allowed Party/site scope.

---

# 144. Portal Case Privacy

Case comments/attachments can contain sensitive data.

Every portal read verifies external-user authorization server-side.

---

# 145. Portal Internal Note

Never rendered externally.

---

# 146. Portal Attachment

Uses Platform Files and malware/content controls.

---

# 147. Attachment Ownership

Support stores file references/context only.

Platform Files stores bytes.

---

# 148. Attachment Security

Required:

```text
file size limits
allowed/blocked types
malware scanning seam
content disposition
authorization
audit
retention
```

---

# 149. HTML Sanitization

Inbound email/chat HTML must be sanitized before rendering.

---

# 150. Rich Text

Allowed subset only.

No arbitrary script execution.

---

# 151. Case Timeline

Unified ordered timeline includes:

```text
Customer messages
Agent replies
Internal notes
Assignment changes
State changes
SLA events
Escalations
Linked object creation/status summaries
Resolution
CSAT
```

---

# 152. Timeline != Audit Log

Timeline is user-facing operational history.

Audit remains security/compliance record.

---

# 153. Timeline Composition

Cross-module events are references/read projections, not duplicated mutable truth.

---

# 154. Timeline Performance

Initial load returns summary + recent events.

Older events paginated.

---

# 155. Case Merge

Support duplicate cases.

---

# 156. Merge Rule

```text
Secondary Case
→ State = Merged
→ MergedInto = Primary Case
```

History is preserved.

---

# 157. Merge Does Not Delete

Absolute.

---

# 158. Merge Validation

Prevent:

```text
cross-tenant merge
unauthorized customer merge
cases with incompatible privacy scope
```

without explicit handling.

---

# 159. Conversation Reparent On Merge

Can move/link conversations to primary while preserving original provenance.

---

# 160. Case Split

Sometimes one inbound case contains two unrelated issues.

Authorized agent may create child/new case and move/copy relevant context.

---

# 161. Split History

Original case records split relationship.

---

# 162. Case Relations

Support:

```text
DuplicateOf
RelatedTo
ParentOf
ChildOf
FollowUpOf
MergedInto
```

---

# 163. Known Customer Issue

Foundation for widespread recurring external issue.

---

# 164. Known Issue != ITSM Problem Record

TODIR does not implement full ITIL Problem/Change/CMDB in this module.

---

# 165. Known Issue Concept

```text
SupportKnownIssue
{
    Id
    Title
    ProductRef?
    State
    PublicSummary?
    InternalDiagnosis?
    Workaround?
    Owner
}
```

---

# 166. Known Issue Case Link

Many customer cases can link to one known issue.

---

# 167. Known Issue Benefit

Avoid 500 agents independently rediscovering the same outage/product issue.

---

# 168. Known Issue Customer Communication

Can publish approved:

```text
workaround
status
resolution notice
```

to linked cases.

No uncontrolled bulk messaging.

---

# 169. Major Customer Incident Foundation

Optional coordination mode for widespread external customer impact.

Keep smaller than full ITSM.

---

# 170. Major Incident Possible Capabilities

```text
linked cases
designated owner
impact summary
communication cadence
resolution status
post-incident follow-up
```

evidence-based.

---

# 171. Support SLA

`29` owns **Support Case SLA**.

`24` owns **Service Work/Field Service SLA**.

Never collapse them.

---

# 172. Support SLA Examples

```text
First Response
Next Response
Resolution
```

---

# 173. Service SLA Example

```text
Technician onsite within 4 hours
Repair within 8 hours
```

belongs to Service when tied to Work Order/contract execution.

---

# 174. Support SLA Policy

Versioned/effective-dated.

---

# 175. SLA Policy Concept

```text
SupportSlaPolicyVersion
{
    Id
    Name
    Applicability
    BusinessCalendarRef
    FirstResponseTarget?
    NextResponseTarget?
    ResolutionTarget?
    PausePolicy
    EffectiveFrom
    EffectiveTo?
}
```

---

# 176. SLA Applicability

Potential:

```text
Customer tier
Support policy
Category
Priority
Severity
Product
Channel
Legal Entity
```

deterministic resolution.

---

# 177. SLA Snapshot

Case stores exact resolved policy/version/targets.

Later configuration change does not rewrite historical commitments.

---

# 178. First Response SLA

Time from qualifying case start to first meaningful agent/support response.

---

# 179. Auto Acknowledgement

Default:

```text
Auto acknowledgement
!=
Meaningful first response
```

unless company explicitly defines otherwise.

---

# 180. AI Auto Reply And First Response

Low-risk autonomous answers may have a separate metric.

Do not silently count bot acknowledgment as human first response.

---

# 181. Next Response SLA

Optional.

Measures time between customer reply and next meaningful Support response.

---

# 182. Resolution SLA

Measures time to resolution under configured calendar/pause rules.

---

# 183. Business Calendar

SLA uses explicit:

```text
timezone
working days
working hours
holidays
exceptions
```

---

# 184. Multi-Country Support

Examples:

```text
Egypt support calendar
Saudi support calendar
24x7 enterprise calendar
```

---

# 185. SLA Deadline

Store computed deadline plus source policy version.

Avoid recalculating historical SLA with today's calendar.

---

# 186. SLA Clock

Represent active clock/deadline state.

Do not spawn one OS timer per Case.

---

# 187. SLA Clock Processing

Use indexed deadline queries/jobs.

---

# 188. SLA Pause

Possible pause reasons:

```text
WaitingCustomer
ApprovedExternalDependency
```

only if policy permits.

---

# 189. Waiting Internal Usually Does Not Pause

Default recommendation:

```text
Waiting Internal Team
```

does not pause customer-facing resolution SLA unless contract says so.

---

# 190. Pause Audit

Capture:

```text
reason
started_at
ended_at
actor/source
policy basis
```

---

# 191. SLA At Risk

Derived from remaining time/priority thresholds.

---

# 192. SLA Breach

Historical fact once deadline missed under policy.

Later resolution does not erase breach.

---

# 193. SLA Override

High-control:

```text
permission
reason
approval if configured
audit
```

No silent deadline edit.

---

# 194. SLA Compliance Metric

Must define:

```text
which SLA KPI
eligible cases
business-calendar basis
exclusions
```

---

# 195. Support Entitlement

Support needs to answer:

> **What level of support is this customer/request entitled to?**

---

# 196. Support Policy Profile

`29` can own operational Support policy:

```text
allowed channels
support hours
response class
queue priority
case limits if used
```

---

# 197. Service Contract Boundary

If entitlement comes from `24 Service Contract` or Warranty:

```text
24 owns commercial/service coverage
29 consumes resolved support outcome/reference
```

---

# 198. Support Entitlement Sources

Potential:

```text
Default customer support policy
Manual enterprise support profile
Service contract reference
Warranty reference
Future subscription/plan reference
Special approved agreement
```

---

# 199. Entitlement Snapshot

At case creation/triage store:

```text
source type/ref
resolved support tier
applicable channels
SLA profile
effective dates
```

for explainability.

---

# 200. Entitlement Change

Future cases use new policy.

Existing case behavior follows explicit recalculation/change policy, never silent history rewrite.

---
# 201. Entitlement Missing

If no explicit entitlement applies:

```text
fallback to company default Support policy
```

or clearly show:

```text
No support entitlement resolved
```

according to configuration.

---

# 202. Entitlement Override

Authorized supervisor may grant temporary exception with:

```text
reason
scope
expiry
approver
audit
```

---

# 203. Support Coverage Detail

Agent should see concise outcome:

```text
Remote Support       Covered
Onsite Service       Requires Service Contract
Replacement          Not determined here
Response SLA         30m
Resolution SLA       8h
```

---

# 204. Coverage Does Not Promise Refund

Support entitlement cannot directly authorize accounting/payment action unless owning module policy allows.

---

# 205. Escalation

Escalation means operational management attention/ownership change, not necessarily severity change.

---

# 206. Escalation Types

Potential:

```text
SLA Risk
SLA Breach
Customer Escalation
Management Escalation
Technical Escalation
Security/Privacy Escalation
Quality Escalation
Legal/Regulatory Escalation
```

---

# 207. Escalation Rule

Versioned deterministic automation can:

```text
notify
raise priority
move queue
add supervisor
request handoff
```

within policy.

---

# 208. Escalation != Auto Resolution

Never close or resolve because escalation workflow completed.

---

# 209. Escalation History

Capture:

```text
type
reason
trigger
owner
time
resolution
```

---

# 210. Customer Escalation Flag

Customer saying:

```text
"I want a manager"
```

can trigger management escalation without changing case severity automatically.

---

# 211. Internal Collaboration

Support agent can collaborate without exposing internal conversation to customer.

---

# 212. Internal Note Visibility

Default team-visible according to Support scope.

Restricted notes allowed only when genuine business/privacy need exists.

---

# 213. Mention

Internal mention can notify authorized user.

Does not grant case access if user lacks permission.

---

# 214. Internal Task

Prefer Platform Task/Work abstraction if one exists.

Do not build a second generic project-management system inside Support.

---

# 215. Callback

Support-specific callback can be represented as due Support activity/task.

---

# 216. Internal Handoff

Case can request help from:

```text
Billing
Sales
Quality
Field Service
Warehouse
Technical SME
```

without transferring source-domain ownership.

---

# 217. Handoff Request

Concept:

```text
SupportHandoff
{
    CaseId
    TargetCapability
    TargetOwner/Queue?
    RequestType
    Context
    State
    LinkedObjectRef?
}
```

---

# 218. Handoff States

```text
Requested
Accepted
InProgress
Completed
Rejected
Cancelled
```

---

# 219. Handoff != Case Assignment

The Support case can remain assigned to Support agent while another module/team performs work.

---

# 220. Field Service Handoff

Canonical:

```text
Support Case
→ Create Service Request
→ 24 owns Service Request / Work Order
→ Support tracks linked public progress
```

---

# 221. Do Not Duplicate Service Request

No `support_service_request` table representing the same service demand after handoff.

---

# 222. Service Status Projection

Support may show:

```text
Service Request accepted
Visit scheduled
Technician onsite
Work completed
```

through read contract/events.

---

# 223. Service Internal Detail Privacy

Support agent does not automatically see:

```text
technician cost rate
internal maintenance cost
full service contract pricing
```

---

# 224. Support Resolution After Service

Field Service technical completion does not automatically close Support Case unless policy explicitly allows.

Support may need to confirm customer outcome.

---

# 225. Quality Handoff Workflow

```text
Customer complaint
→ Support triage
→ Quality Issue requested/linked
→ Quality investigates
→ Support communicates approved outcome
```

---

# 226. Quality Internal Investigation Privacy

Customer must not see raw internal root-cause notes, CAPA details or supplier blame unless approved for external communication.

---

# 227. Quality Public Outcome

Support can consume approved summary such as:

```text
Confirmed defect
Replacement approved
No defect found
Investigation ongoing
```

from Quality/public handoff contract.

---

# 228. Sales Return Handoff

```text
Support request
→ eligibility/intake
→ Sales/Return workflow
```

Sales/Inventory own actual return transaction/movement.

---

# 229. Refund Handoff

```text
Support case
→ Request refund review
→ Payments/Invoicing/Sales owner decides/executes
```

---

# 230. Refund Status Projection

Support can show:

```text
Requested
Approved
Rejected
Processing
Paid
```

only if supplied by owner module.

---

# 231. Billing Support

Agent can inspect authorized invoice summary and resend approved invoice document/link.

No invoice editing in Support.

---

# 232. Payment Support

Agent can inspect payment status/receipt reference where permitted.

No manual payment state mutation.

---

# 233. Collections Boundary

Support may route collection-related query to Finance/Collections.

Do not expose debt-collection workflow through generic ticket actions.

---

# 234. CRM Handoff

If inbound support interaction is actually commercial interest:

```text
Support Case
→ Create CRM Lead / Opportunity referral
```

CRM owns commercial follow-up.

---

# 235. CRM Activity Boundary

Support communications remain Support history.

CRM Customer 360 may display support summary/timeline references.

---

# 236. Customer 360 Extension

CRM Customer 360 should later include:

```text
Open Support Cases
Recent Cases
CSAT summary
Major unresolved support issues
```

through Support read contract.

---

# 237. Support 360

Support's own customer workspace is optimized for issue resolution, not pipeline selling.

---

# 238. Order Support Scenario

Customer says:

```text
"Where is my order?"
```

Support can retrieve authorized order/delivery status.

No duplicate shipping tracker.

---

# 239. Missing Delivery Scenario

If logistics exception requires action:

```text
case remains Support-owned
source logistics/sales workflow handles correction
```

---

# 240. Wrong Item Scenario

Support can link:

```text
Order
Delivery
Product
Return request
```

without editing stock.

---

# 241. Technical Product Scenario

Support can link Product and Installed Base/Serial when available.

---

# 242. Installed Base Lookup

Search via `24` by:

```text
Customer
Serial
Asset tag
Product/model
Site
```

---

# 243. Warranty Answer

Support displays resolved warranty/coverage outcome from Service.

Do not copy warranty rules into Support.

---

# 244. Knowledge Base

Support owns Helpdesk Knowledge capability unless a future platform-wide Knowledge service is explicitly approved.

---

# 245. Knowledge Mission

> **Resolve common questions consistently, reduce customer effort, and give agents approved answers without turning internal ticket notes into public documentation.**

---

# 246. Knowledge Article

```text
SupportKnowledgeArticle
{
    Id
    Code?
    Title
    State
    Audience
    CategoryRef?
    ProductRefs?
    Owner
}
```

---

# 247. Knowledge Version

```text
SupportKnowledgeArticleVersion
{
    ArticleId
    Revision
    Locale
    Title
    Body
    Summary?
    State
    EffectiveFrom?
    ReviewedAt?
}
```

---

# 248. Knowledge States

Recommended:

```text
Draft
UnderReview
Published
Retired
Superseded
```

---

# 249. Knowledge Audience

```text
InternalAgent
CustomerPortal
Public
Partner
```

---

# 250. Internal Article

Can include troubleshooting/internal procedure not safe for customer publication.

---

# 251. Public Article

Contains approved customer-facing content only.

---

# 252. Localization

Each article can have:

```text
Arabic
English
other locales later
```

with independent translation/review status.

---

# 253. Arabic Knowledge UX

RTL rendering, Arabic search normalization and Arabic typography are first-class.

---

# 254. Translation != New Truth

Translated article remains linked to source article/version and review state.

---

# 255. Article Review

Optional workflow:

```text
Author
→ Reviewer
→ Publish
```

Small company may allow direct publish by authorized user.

---

# 256. Article Effective Date

Useful for policy/procedure content that changes on a future date.

---

# 257. Article Expiry / Review Due

Support scheduled review dates.

Expired/stale article can be flagged/unpublished according to policy.

---

# 258. Knowledge Ownership

Every article has accountable owner/team.

---

# 259. Article Product Context

Optional links to Product/Product Category/Service type.

Catalog remains owner.

---

# 260. Article Search

Day 1:

```text
PostgreSQL FTS/trigram
category
product
tags
locale
```

No OpenSearch required until measured need.

---

# 261. Article Suggestion

Agent workspace can suggest relevant articles based on case text/category/product.

---

# 262. Suggestion Source

Can start deterministic/search-based.

AI retrieval later/alongside.

---

# 263. Case → Article

Resolved case may suggest:

```text
Create Knowledge Draft
```

---

# 264. Never Publish Raw Ticket Automatically

Customer conversation may contain:

```text
PII
commercial secrets
names
phone numbers
account details
internal notes
```

AI/manual transformation and review required.

---

# 265. Article Feedback

Customer/agent can mark:

```text
Helpful
Not Helpful
```

and optional reason.

---

# 266. Knowledge Deflection

Measure only with defined methodology.

Example:

```text
article viewed
→ no case opened within configured session/window
```

Never claim causal deflection blindly.

---

# 267. Knowledge Usage

Track article linked/sent during case resolution.

---

# 268. Article Revision History

Published history remains accessible to authorized users.

---

# 269. Knowledge Security

Internal content must never leak into portal/public/AI external response.

---

# 270. Knowledge Permissions

Initial:

```text
ReadInternal
Author
Review
Publish
Retire
ManageTaxonomy
```

---

# 271. Canned Replies / Macros

Different from Knowledge Article.

Used for agent response efficiency.

---

# 272. Reply Template

Examples:

```text
Acknowledgement
Need more information
Delivery update
Service visit scheduled
Resolution confirmation
```

---

# 273. Reply Template Localization

Arabic/English versions.

---

# 274. Safe Variables

Examples:

```text
CustomerName
CaseNumber
AgentName
WorkOrderNumber
PortalLink
```

from approved context only.

---

# 275. No Arbitrary Template Code

No script execution.

---

# 276. Macro

Can bundle safe actions such as:

```text
set category
set waiting-on customer
insert reply draft
add tag
```

---

# 277. Macro Cannot Bypass Permission

Each underlying action still authorizes server-side.

---

# 278. Macro Send Policy

Default macro prepares reply; user reviews/sends.

Auto-send only explicit approved automation.

---

# 279. Customer Language

Sources:

```text
Directory preference
Portal preference
Channel locale
Agent selection
AI language detection
```

---

# 280. Detected Language

AI/heuristic result is metadata, not identity truth.

---

# 281. Language Change

Agent/customer can correct preferred case language.

---

# 282. Original Message Preservation

Never replace original Arabic/English message with translated text.

---

# 283. Agent Translation

Can provide translated view beside original.

---

# 284. Translation Privacy

Only approved AI/provider services with configured data policy may process case text.

---

# 285. Outbound Translation

AI can draft translated reply.

Agent approval default.

---

# 286. Customer Satisfaction — CSAT

Support owned measurement linked to Support experience.

---

# 287. CSAT Trigger

Usually after:

```text
Resolved
or
Closed
```

according to policy.

---

# 288. CSAT Question

Simple default:

```text
How satisfied were you with the support you received?
1–5
```

optional comment.

---

# 289. CSAT Delivery

Via:

```text
Email
WhatsApp template when allowed
Portal
Chat
```

provider rules respected.

---

# 290. CSAT Response

One survey invitation/response identity with replay protection.

---

# 291. CSAT != Resolution

A resolved case can have low satisfaction.

---

# 292. Low CSAT Follow-Up

Optional:

```text
create manager review task
reopen/follow-up case only by policy
```

Do not auto-reopen every low score.

---

# 293. CSAT Privacy

Customer comment may contain sensitive data; same Support privacy controls apply.

---

# 294. CES — Later

Customer Effort Score may be added if market need.

---

# 295. NPS Boundary

NPS is broader relationship metric and need not belong core Support.

Do not force it into Helpdesk Day 1.

---

# 296. Resolution

Case resolution must be structured enough for analytics and knowledge.

---

# 297. Resolution Code

Examples:

```text
Answered
Customer Education
Configuration Guidance
Order Corrected
Return Initiated
Refund Requested
Service Request Created
Technical Fix
Quality Investigation
Duplicate
No Response
Not Supported
No Issue Found
```

---

# 298. Resolution Notes

Internal detailed summary.

---

# 299. Customer-Facing Resolution

Separate approved text shown/sent to customer.

---

# 300. Resolution Prerequisites

Depending category:

```text
required fields
linked downstream work state
customer communication sent
resolution code
```

---

# 301. Resolve With Open Work Order

Allowed only by explicit policy/category.

Default technical-service case may remain open/waiting until Service outcome known.

---

# 302. Resolve With Open Quality Investigation

Allowed only if customer-support obligation has been fulfilled and policy permits.

Otherwise remain open/waiting.

---

# 303. Resolution Approval

Optional for:

```text
critical complaint
legal/regulatory complaint
VIP escalation
compensation case
```

use Platform Approvals.

---

# 304. Close Reason

Potential:

```text
ResolvedConfirmed
AutoClosedAfterResolution
DuplicateMerged
CustomerWithdrew
Invalid/Spam
Other
```

---

# 305. Spam

Spam/abuse case classification separate from normal cancellation.

---

# 306. Spam Handling

Can:

```text
mark spam
block/rate-limit source per policy
exclude from SLA/analytics where documented
```

---

# 307. Abuse Controls

Portal/web/chat endpoints need:

```text
rate limiting
bot protection
payload limits
attachment controls
```

---

# 308. Blocked Sender

High-control; avoid accidental customer lockout.

Authorized configuration and audit.

---

# 309. Contact Verification

Sensitive support actions can require:

```text
portal authentication
OTP/provider verification
known-contact confirmation
manual verification procedure
```

---

# 310. Verification State

Case/requester can show:

```text
Unverified
ChannelVerified
AuthenticatedPortal
AgentVerified
```

---

# 311. Verification Does Not Change Directory Identity Automatically

Link/update through Directory command if required.

---

# 312. Sensitive Actions

Examples requiring stronger verification:

```text
change email/phone
refund destination
financial document to new address
password/security reset
personal-data export
account ownership change
```

---

# 313. Support Does Not Own Authentication Reset

Identity/IAM provider owns actual credential reset.

Support can invoke approved recovery workflow.

---

# 314. Privacy Request Intake

Support may intake:

```text
access request
erasure request
correction request
privacy complaint
```

but dedicated privacy/compliance process may own fulfillment.

---

# 315. Privacy Request Classification

Make distinguishable and restricted.

---

# 316. Data Retention

Support data often contains PII and attachments.

Retention policy must be configurable by tenant/country/data class.

---

# 317. Retention != Immediate Delete

Legal/accounting/contractual obligations may require preservation.

Use approved retention/legal-hold policy.

---

# 318. Message Retention

Can differ by channel/business policy.

Historical case integrity must remain explainable.

---

# 319. Attachment Retention

Can differ from message metadata.

---

# 320. Legal Hold Seam

Architecture allows records subject to legal hold to be excluded from ordinary retention deletion.

---

# 321. Data Redaction

Authorized privacy process may redact personal content while preserving necessary audit/transaction structure.

Do not simply hard-delete relational history.

---

# 322. Search Redaction

Redacted content must disappear from search/index/projections after rebuild.

---

# 323. Data Export

Customer/user data export respects:

```text
permissions
privacy scope
attachments
linked source ownership
```

---

# 324. Internal Notes In Export

Not included in customer-facing export unless legal/privacy process explicitly requires and authorizes.

---

# 325. Audit

Platform Audit records security-significant actions.

---

# 326. Support Audit Critical Actions

Examples:

```text
customer reassignment
portal visibility change
message deletion/redaction request
internal/public visibility change
SLA override
priority/severity override
case merge/split
refund/service/quality handoff
entitlement override
case close/reopen
export
bulk action
```

---

# 327. No Customer Message Silent Delete

Customer-visible/received communication cannot disappear through ordinary agent delete.

Use retention/redaction governance.

---

# 328. Agent Workspace

Central screen optimized for fast resolution.

---

# 329. Agent Workspace Layout

Suggested:

```text
Left: Queue / My Work
Center: Conversation / Timeline
Right: Customer + Case Context
Bottom/Actions: Reply / Note / Handoff / Resolve
```

---

# 330. Initial Agent Load

Must not load full customer history and every source module.

Use summary + lazy tabs.

---

# 331. Agent Header

Show:

```text
Case number
Priority / Severity
Customer
Requester
Category
SLA countdown
Assignee
Waiting On
```

---

# 332. Reply Composer

Supports:

```text
channel-aware reply
attachments
templates
knowledge insertion
AI draft
language/translation
```

---

# 333. Internal Note Composer

Visually distinct and separate endpoint/command.

---

# 334. Dangerous UI Rule

Never use a tiny toggle that can accidentally switch an internal note into a customer reply without strong visual confirmation.

---

# 335. Context Panel

Lazy sections:

```text
Customer
Orders
Invoices
Payments
Products
Installed Base
Service
Quality
Prior Support
```

based on permissions/modules active.

---

# 336. Agent Quick Actions

Potential:

```text
Assign
Change Priority
Set Waiting On
Link Order
Link Product
Create Service Request
Create Quality Issue
Request Return
Create CRM Lead
Insert Knowledge
Resolve
```

---

# 337. Quick Action Authorization

Every command validates server-side.

---

# 338. Manager Workspace

Answers:

```text
What is unassigned?
What is at risk?
What breached?
What is aging?
Which customers/issues are escalating?
Where is capacity constrained?
```

---

# 339. Manager Home Example

```text
New                  42
Unassigned            5
At Risk              11
Breached              3
Waiting On Us        27
Waiting Customer     19
Critical              2
Avg First Response   11m
CSAT                 4.6
```

---

# 340. Queue View

Fast filters:

```text
My Cases
Unassigned
At Risk
Breached
Critical
Waiting Customer
Waiting Internal
Updated by Customer
```

---

# 341. Updated By Customer

Important work state/read flag separate from Case State.

---

# 342. Agent Collision

Two agents opening same case should see presence/assignment/concurrency hint.

Do not rely on last-write-wins.

---

# 343. Optimistic Concurrency

Mutable case header uses row version.

---

# 344. Reply Concurrency

Multiple replies can exist, but agent UI should warn if a new customer/agent message arrived while drafting.

---

# 345. Case Edit Collision

Semantic commands reject stale high-impact updates where necessary.

---

# 346. Bulk Actions

Manager can perform controlled:

```text
assign
move queue
tag
priority update
close spam
```

with per-item validation/audit.

---

# 347. Bulk Reply

Not generic.

Only approved mass-response/known-issue flow with safeguards.

---

# 348. Saved Views

Support agents/managers can save filters using Platform view capabilities.

---

# 349. Search

Search by:

```text
Case number
Customer
Requester
Email/phone
Subject
Message text
Order/invoice/work-order reference
Product
Serial
```

respecting permissions.

---

# 350. Search Day 1

PostgreSQL-first:

```text
FTS
trigram
normalized email/phone
indexed reference IDs
```

---

# 351. OpenSearch Later

Only after measured search scale/relevance need.

---

# 352. Search Security

Search result itself must not leak unauthorized case existence/snippets.

---

# 353. Search Snippet Redaction

Never include hidden internal note text in portal/customer-facing search.

---

# 354. Custom Fields

Use Platform Custom Fields for tenant-specific Support metadata.

---

# 355. Custom Field Targets

Potential:

```text
Support Case
Knowledge Article metadata
Customer support profile
```

---

# 356. Conditional Fields

Category can require specific fields.

Example:

```text
Technical Issue → Product + serial optional/required by policy
Refund Request → Order/Invoice reference
```

---

# 357. Do Not Hide Invariants In Custom Fields

Core SLA, assignment, requester, state, visibility and cross-domain references remain typed domain concepts.

---

# 358. Tags

Lightweight flexible classification.

Not replacement for structured category/severity/resolution.

---

# 359. Tags Security

No sensitive uncontrolled tags exposed to customer portal.

---

# 360. Automation

Support-specific automation must remain constrained and explainable.

---

# 361. Automation Triggers

Potential:

```text
Case Created
Customer Replied
Case Updated
SLA At Risk
SLA Breached
Waiting Too Long
Resolved
Closed
CSAT Received
Known Issue Updated
```

---

# 362. Automation Conditions

Potential:

```text
category
priority
severity
customer tier
channel
queue
waiting state
age
SLA state
```

---

# 363. Automation Actions

Potential:

```text
assign queue
set priority
notify
create internal task
add tag
apply template draft
escalate
```

Cross-domain actions require explicit contracts and permissions.

---

# 364. No Arbitrary Workflow Script

No tenant code execution.

---

# 365. Automation Loop Prevention

Track automation execution and prevent rule loops.

---

# 366. Automation Idempotency

Repeated trigger/event cannot create duplicate handoff/escalation/notification unexpectedly.

---

# 367. Business Hours Automation

Use calendar-aware due calculations where appropriate.

---

# 368. Waiting Customer Reminder

Optional:

```text
after 2 days waiting customer
→ reminder
```

then optional auto-close after further period.

---

# 369. No Harassment Automation

Reminder frequency bounded/configurable.

---

# 370. Internal Escalation Reminder

Case waiting internal beyond threshold notifies owner/manager.

---

# 371. AI Mission

> **Reduce agent effort and improve consistency while preserving human authority, customer privacy, approved policy and source-domain ownership.**

---

# 372. AI Allowed — Summarization

```text
conversation summary
case summary
handoff summary
closure summary draft
```

---

# 373. AI Allowed — Classification

Suggest:

```text
category
priority
severity
language
intent
product context
```

---

# 374. AI Allowed — Reply Draft

Draft response using:

```text
approved knowledge
case conversation
allowed customer context
```

---

# 375. AI Allowed — Knowledge Suggestion

Retrieve/rank approved articles.

---

# 376. AI Allowed — Translation

Agent-side Arabic/English translation while preserving original.

---

# 377. AI Allowed — Duplicate Suggestion

Suggest similar/open duplicate cases.

User confirms merge.

---

# 378. AI Allowed — Known Issue Suggestion

Suggest link to known widespread issue.

---

# 379. AI Allowed — Escalation Risk

Can flag sentiment/recurrence/SLA risk as suggestion.

---

# 380. Sentiment Is Not Truth

Never label customer or agent character/personality from model inference.

Use only operational escalation signal where allowed.

---

# 381. AI Grounding

Customer-facing draft/answer should ground on:

```text
Published Knowledge
Approved policy
Authorized source-domain facts
Case conversation
```

---

# 382. AI Source Visibility

Agent should be able to see which Knowledge/source facts informed answer when practical.

---

# 383. AI Permission Boundary

AI can retrieve only data the acting workflow/user is allowed to access.

---

# 384. AI No Direct DB

Absolute.

---

# 385. AI Must Not Autonomously

Day-1 forbidden:

```text
Issue refund
Create credit note
Approve warranty exception
Override SLA
Delete/redact customer data
Close critical complaint
Promise compensation
Modify order
Change payment status
Post accounting
Create NCR conclusion
```

---

# 386. AI Autonomous Low-Risk Support — Later

Possible only for narrow intents such as:

```text
opening hours
published policy FAQ
invoice-copy navigation
password-reset navigation
simple status query
```

with confidence and handoff.

---

# 387. Human Handoff

Any autonomous support path must provide clear human escalation.

---

# 388. AI Confidence

Low confidence → no autonomous final answer.

---

# 389. AI Audit

Capture:

```text
model/tool version
prompt/task type
sources/tools used
suggestion/action outcome
user accepted/edited/rejected
```

within privacy policy.

---

# 390. AI Data Minimization

Send only necessary case/context data to AI provider/tool.

---

# 391. AI Redaction

Sensitive fields can be masked before external model processing where configured.

---

# 392. AI Translation Review

For legal/financial/critical support responses, require human review.

---

# 393. AI Knowledge Draft

Can generate draft from repeated solved cases.

Never auto-publish.

---

# 394. AI Evaluation

Track quality metrics:

```text
acceptance rate
edit distance/major correction
bad suggestion reports
citation/source coverage
policy violation rate
```

---

# 395. AI Failure

Support workflow remains fully usable if AI unavailable.

---

# 396. No AI As Hard Dependency For Ticket Creation

Absolute.

---

# 397. No Vector Database Day 1 Without Evidence

PostgreSQL/approved search first.

Use vector retrieval only if measured Knowledge/relevance use case justifies it.

---

# 398. Customer Self-Service

Self-service includes:

```text
Knowledge search
Portal tickets
Case status
Approved FAQs
Guided intake
```

---

# 399. Guided Intake

Web/portal form can ask category-specific questions before case creation.

---

# 400. Guided Intake Must Stay Short

Ask only information needed to route/solve.

Do not turn customer support entry into a long ERP form.

---
# 401. Pre-Case Deflection

Before creating a case, TODIR may suggest relevant public Knowledge.

Customer can still proceed to Support.

---

# 402. Deflection Must Not Trap Customer

Never hide human-support access behind endless article loops.

---

# 403. Portal Status Labels

Customer-facing statuses may be simpler than internal state/waiting model.

Example:

```text
Received
In Progress
Waiting For You
Resolved
Closed
```

---

# 404. Portal Status Mapping

Versioned/configurable mapping from internal case state to customer-facing label.

---

# 405. Do Not Expose Internal Queue

Portal does not need internal routing/team names unless intentionally customer-facing.

---

# 406. Customer Notification

Platform Notifications/provider adapters send support updates.

---

# 407. Notification Examples

```text
Case received
Agent replied
More information required
Service visit scheduled
Case resolved
CSAT request
```

---

# 408. Notification Failure

Does not corrupt case state.

Retry separately.

---

# 409. Notification Preference

Use Directory/contact/provider preferences where available.

Support does not duplicate enterprise-wide communication preference truth.

---

# 410. Transactional Support Message vs Notification

A direct case reply belongs Conversation.

A side notification such as `Case assigned` belongs Platform Notification.

---

# 411. Data Model — Core Case Tables

Potential:

```text
support_case
support_case_state_history
support_case_wait_history
support_case_reference
support_case_relation
support_case_assignment_history
support_case_tag_link
support_external_identity
support_requester_link
```

---

# 412. Data Model — Team / Routing Tables

Potential:

```text
support_team
support_queue
support_queue_member
support_agent_profile
support_agent_skill
support_routing_rule
support_routing_rule_version
support_routing_attempt
support_escalation
```

---

# 413. Data Model — Channel Tables

Potential:

```text
support_channel_account
support_conversation
support_conversation_participant
support_message
support_message_attachment
support_message_delivery_event
support_provider_external_ref
```

---

# 414. Data Model — SLA / Entitlement Tables

Potential:

```text
support_policy_profile
support_sla_policy
support_sla_policy_version
support_sla_clock
support_sla_event
support_entitlement_snapshot
support_policy_override
```

---

# 415. Data Model — Collaboration / Automation

Potential:

```text
support_handoff
support_case_follower
support_macro
support_macro_version
support_reply_template
support_reply_template_version
support_automation_rule
support_automation_rule_version
support_automation_execution
```

---

# 416. Data Model — Knowledge

Potential:

```text
support_knowledge_article
support_knowledge_article_version
support_knowledge_category
support_knowledge_product_link
support_knowledge_feedback
support_knowledge_usage
```

---

# 417. Data Model — Known Issue / CSAT

Potential:

```text
support_known_issue
support_known_issue_case_link
support_survey_invitation
support_survey_response
support_resolution_code
```

---

# 418. Data Model — Read Models

Potential:

```text
support_case_summary_projection
support_queue_projection
support_agent_work_projection
support_customer_context_projection
support_sla_risk_projection
support_manager_kpi_projection
support_case_search_projection
support_known_issue_projection
support_knowledge_search_projection
```

---

# 419. Tables Support Must Not Own

Forbidden duplicate truth:

```text
party
contact
crm_lead
crm_opportunity
sales_order
invoice
payment
inventory_movement
inventory_balance
service_request
service_work_order
installed_base_item
quality_issue
nonconformance
journal_entry
employee
attendance
```

---

# 420. Tenant Columns

All tenant-owned Support tables include:

```text
tenant_id
```

---

# 421. RLS

```text
ENABLE ROW LEVEL SECURITY
FORCE ROW LEVEL SECURITY
```

---

# 422. Runtime Role

No ownership/BYPASSRLS.

---

# 423. IDs

UUIDv7.

---

# 424. Time

NodaTime.

Use:

```text
Instant
LocalDate
LocalTime
DateTimeZone
Duration
```

according to semantics.

---

# 425. Row Version

Mutable case/routing/policy records use optimistic concurrency.

---

# 426. Immutable Message History

Externally sent/received messages behave as finalized communication facts.

Corrections are additional messages/redaction governance, not ordinary update.

---

# 427. Effective Dating

Version/effective-date:

```text
SLA policies
routing rules
support policy profiles
knowledge versions
templates/macros where material
business calendars
```

---

# 428. Case Indexes

Likely:

```text
tenant/state/updated_at desc
tenant/queue/state/priority
tenant/assigned_agent/state/updated_at
tenant/customer_party/state/updated_at
tenant/requester_contact/updated_at
tenant/sla_due/state
tenant/category/state
```

---

# 429. Conversation Indexes

```text
tenant/case_id/last_message_at
tenant/channel_account/external_thread_ref
```

unique external-thread identity as provider semantics allow.

---

# 430. Message Indexes

```text
conversation/created_at/id
provider/channel_account/provider_message_ref
```

---

# 431. SLA Indexes

```text
deadline/state
case/kpi_type/state
```

No full case-table scan for breach monitor.

---

# 432. Search Indexes

Normalized:

```text
case number
email
phone
external refs
subject FTS
message FTS if policy allows
```

---

# 433. Knowledge Indexes

```text
locale/state/category
product link
FTS title/body
```

---

# 434. Partitioning

Do not partition Day 1.

Measure high-volume message/history tables first.

---

# 435. PostgreSQL First

No dedicated ticket database/search cluster Day 1.

---

# 436. Redis

Not canonical and not required Day 1.

Presence/caching may justify later use, but correctness cannot depend on it.

---

# 437. OpenSearch

Not required Day 1.

Use only for measured large-scale full-text/relevance need.

---

# 438. Message Body Storage

Support may store normalized support message content because it is canonical Support conversation history.

Provider mailbox remains transport/source adapter, not Support business truth.

---

# 439. Provider Message Retention

If provider content expires, TODIR still preserves Support-owned normalized conversation according to retention policy.

---

# 440. Email Attachments

Do not embed binary data in `support_message` rows.

Use File references.

---

# 441. API Base

```text
/api/v1/support/
```

---

# 442. Case APIs

Representative:

```text
GET  /cases
POST /cases
GET  /cases/{id}
POST /cases/{id}/triage
POST /cases/{id}/assign
POST /cases/{id}/move-queue
POST /cases/{id}/set-waiting
POST /cases/{id}/resolve
POST /cases/{id}/close
POST /cases/{id}/reopen
POST /cases/{id}/cancel
```

---

# 443. Message APIs

```text
GET  /cases/{id}/conversations
GET  /cases/{id}/messages
POST /cases/{id}/replies
POST /cases/{id}/internal-notes
POST /cases/{id}/drafts
```

Separate reply/note endpoints.

---

# 444. Reference APIs

```text
POST   /cases/{id}/references
DELETE /cases/{id}/references/{referenceId}
GET    /cases/{id}/context
```

---

# 445. Merge / Split APIs

```text
POST /cases/{id}/merge
POST /cases/{id}/split
```

with semantic validation.

---

# 446. Handoff APIs

```text
POST /cases/{id}/handoffs/service
POST /cases/{id}/handoffs/quality
POST /cases/{id}/handoffs/return
POST /cases/{id}/handoffs/refund-review
POST /cases/{id}/handoffs/crm
```

Avoid one generic unsafe `execute-anything` endpoint.

---

# 447. Queue APIs

```text
GET /queues
GET /queues/{id}/work
POST /queues/{id}/assign-next
```

---

# 448. Agent Work API

```text
GET /agents/me/work
```

returns compact priority/SLA worklist.

---

# 449. Routing APIs

```text
POST /routing/preview
POST /cases/{id}/route
```

manager/admin policy endpoints separate.

---

# 450. SLA APIs

```text
GET /cases/{id}/sla
GET /sla/at-risk
POST /cases/{id}/sla/override
```

---

# 451. Knowledge APIs

```text
GET  /knowledge/articles
POST /knowledge/articles
GET  /knowledge/articles/{id}
POST /knowledge/articles/{id}/versions
POST /knowledge/versions/{id}/review
POST /knowledge/versions/{id}/publish
POST /knowledge/versions/{id}/retire
GET  /knowledge/search
```

---

# 452. Portal APIs

External route namespace separated/authenticated:

```text
POST /portal/support/cases
GET  /portal/support/cases
GET  /portal/support/cases/{id}
POST /portal/support/cases/{id}/replies
GET  /portal/support/knowledge
```

---

# 453. Webhook APIs

Provider-specific adapter ingress.

Verify:

```text
signature
timestamp/replay
provider account mapping
payload size
idempotency
```

before enqueue/domain handling.

---

# 454. Commands — Case

Representative:

```text
CreateSupportCase
TriageSupportCase
ClassifySupportCase
AssignSupportCase
MoveSupportCaseToQueue
SetSupportCasePriority
SetSupportCaseSeverity
SetWaitingOn
ResolveSupportCase
CloseSupportCase
ReopenSupportCase
CancelSupportCase
MergeSupportCases
SplitSupportCase
```

---

# 455. Commands — Communication

```text
ReceiveSupportMessage
SendSupportReply
AddInternalSupportNote
RecordPhoneInteraction
AttachSupportFile
```

---

# 456. Commands — Routing/SLA

```text
RouteSupportCase
EscalateSupportCase
ApplySupportPolicy
OverrideSupportSla
PauseSupportSla
ResumeSupportSla
```

Pause/override only if policy permits.

---

# 457. Commands — Knowledge

```text
CreateKnowledgeArticle
CreateKnowledgeRevision
SubmitKnowledgeForReview
PublishKnowledgeRevision
RetireKnowledgeArticle
RecordKnowledgeFeedback
```

---

# 458. Commands — Handoff

```text
RequestFieldService
RequestQualityInvestigation
RequestSalesReturn
RequestRefundReview
CreateCrmReferral
```

---

# 459. No Generic `SetState`

Use semantic commands.

---

# 460. No Generic `SendMessage`

Use channel/visibility-aware reply semantics with authorization.

---

# 461. No Generic `LinkCustomer`

Use verified reassignment/link command with privacy checks.

---

# 462. Events Emitted

Potential:

```text
SupportCaseCreatedV1
SupportCaseAssignedV1
SupportCaseEscalatedV1
SupportCustomerRepliedV1
SupportAgentRepliedV1
SupportCaseResolvedV1
SupportCaseClosedV1
SupportCaseReopenedV1
SupportSlaAtRiskV1
SupportSlaBreachedV1
SupportHandoffRequestedV1
SupportCsatReceivedV1
```

---

# 463. Events Consumed

Selective:

```text
ServiceRequestStateChanged
WorkOrderStateChanged
QualityIssueStateChanged
ReturnStateChanged
RefundStateChanged
Customer/ContactUpdated
```

only where a linked case needs projection update.

---

# 464. Do Not Subscribe To Everything

Avoid building a giant customer-data event mirror.

Use on-demand reads and targeted projections.

---

# 465. Cross-Module Commands

Immediate user action requiring source object creation calls approved application contract synchronously when correctness/UX requires.

---

# 466. Outbox

Use Platform Outbox for asynchronous projections/notifications/integration events.

---

# 467. Inbox

External/provider events use Inbox/idempotency.

---

# 468. RabbitMQ

Async integration only.

Do not use broker to fake immediate authorization or identity verification.

---

# 469. Idempotency Critical Paths

Mandatory for:

```text
inbound webhook
case auto-create
outbound reply
service handoff
quality handoff
refund/return request
case merge
CSAT invitation
```

---

# 470. Webhook Replay

Same provider event received repeatedly causes one semantic outcome.

---

# 471. Outbound Send Idempotency

Client retry cannot send same reply twice.

---

# 472. Handoff Idempotency

Double click does not create two Service Requests or two Quality Issues.

---

# 473. Case Creation From Email

Stable external message/thread key prevents duplicate case during provider retry.

---

# 474. Case Creation From WhatsApp

Conversation/threading policy decides whether new inbound message:

```text
continues open case
reopens/follows closed case
creates new case
```

according to configurable rules.

---

# 475. Threading Policy

Must be explicit.

Do not attach every message from same phone to one lifetime case.

---

# 476. New Topic Detection

Human/AI suggestion can propose new case for unrelated topic.

AI does not silently split customer conversation by default.

---

# 477. Concurrency — Assignment

Two agents cannot both successfully claim exclusive assignment from unassigned queue without conflict handling.

---

# 478. Claim Next

Transactional operation:

```text
select eligible next
→ assign exactly once
```

---

# 479. Concurrency — Resolve

If customer replies during resolve action, backend should detect/reconcile ordering to avoid false closed state.

---

# 480. Message Ordering

Use provider timestamp + received timestamp + stable sequence/tie-break ID.

Do not assume network arrival order equals conversation order.

---

# 481. Security Mission

Support contains a broad customer-data surface.

Least privilege is mandatory.

---

# 482. Permission Vocabulary

Initial:

```text
support.case.read
support.case.create
support.case.triage
support.case.assign
support.case.reassign
support.case.classify
support.case.resolve
support.case.close
support.case.reopen
support.case.merge
support.case.split
support.case.export

support.reply.send
support.note.internal.add
support.attachment.read
support.attachment.add

support.queue.read
support.queue.manage
support.routing.manage
support.agent.manage

support.sla.read
support.sla.manage
support.sla.override

support.entitlement.read
support.entitlement.override

support.handoff.service
support.handoff.quality
support.handoff.return
support.handoff.refund
support.handoff.crm

support.knowledge.read_internal
support.knowledge.author
support.knowledge.review
support.knowledge.publish
support.knowledge.manage

support.portal.manage
support.csat.read
support.reports.read
support.audit.read
support.settings.manage
```

---

# 483. Scope Model

Combine with:

```text
OwnCases
Queue
Team
LegalEntity
Branch
CustomerScope
ProductScope
AllAuthorized
```

---

# 484. Agent Scope

Typical agent sees:

```text
assigned/authorized queue cases
needed customer context
approved financial/service summary
```

not all company/customer data.

---

# 485. Supervisor Scope

Can view/manage team/queue cases and SLA/escalation.

Does not automatically gain Finance/HR/Quality confidential detail.

---

# 486. Support Manager

Broader Support policy/routing/report access.

---

# 487. Knowledge Manager

Can govern articles without necessarily reading all restricted customer cases.

---

# 488. Privacy Officer / Admin

Sensitive export/redaction/retention permissions separated from ordinary Support Manager.

---

# 489. Portal Scope

External authorization uses exact Party/Contact/site/case relationship.

Never internal queue permission model.

---

# 490. RLS + Application Authorization

RLS protects tenant boundary.

Application authorization protects queue/customer/entity/portal scopes.

---

# 491. Cross-Tenant Provider Ref

Provider IDs are always scoped by tenant/channel account.

Never global lookup without tenant context.

---

# 492. Webhook Authentication

Validate provider signature before tenant/business processing.

---

# 493. Provider Credential Storage

Secrets manager/configuration secret storage.

Never plain Support tables/logs.

---

# 494. Logs

Never log unnecessarily:

```text
full message bodies
access tokens
phone/email unmasked
attachments
password/reset secrets
payment details
private notes
```

---

# 495. PII Masking

Operational logs/telemetry use IDs and safe metadata.

---

# 496. Internal vs External Visibility Test

Architecture/test mandatory to prevent internal-note leakage.

---

# 497. Attachment Authorization Test

Possessing FileId does not grant access.

---

# 498. Customer Context Authorization Test

Support agent cannot pivot from one allowed case into unrelated customer finance/service data.

---

# 499. Portal Enumeration Protection

External sequential guessing/reference IDs must not expose another customer's case.

---

# 500. Rate Limit

Apply to:

```text
public forms
portal login/support create
message send
file upload
provider webhook anomaly
search abuse
```

with careful legitimate-customer limits.

---

# 501. Performance Mission

> **Opening and responding to a ticket must feel immediate even when customer history, multiple channels and cross-module context exist.**

---

# 502. Agent Worklist Target

Representative target:

```text
p95 <= 150–300ms
```

for indexed normal queue queries.

---

# 503. Case Header Target

```text
p95 <= 100–250ms
```

excluding slow optional cross-module tabs.

---

# 504. Recent Timeline Target

```text
p95 <= 150–350ms
```

for recent paged message/events window.

---

# 505. Reply Command Target

Domain accept/persist/outbox:

```text
p95 <= 150–400ms
```

provider final delivery is asynchronous where appropriate.

---

# 506. Customer Context

Initial summary target:

```text
p95 <= 200–500ms
```

using bounded parallel approved reads/projection.

---

# 507. No Cross-Module N+1

Case list cannot query per row for:

```text
customer
SLA
agent
queue
latest message
```

---

# 508. Lazy Context Tabs

Orders/Invoices/Service/Quality tabs load only when requested.

---

# 509. Keyset Pagination

Use for high-volume case/message timelines.

---

# 510. SLA Monitor

No full case scan.

Use active clock deadline index.

---

# 511. Queue Projection

Precompute/rebuildable fields needed for fast worklist:

```text
last activity
customer reply pending
SLA risk
priority
assignee
waiting on
```

---

# 512. Message Count

Do not calculate full count on every case open if expensive.

Use projection/cursor.

---

# 513. Large Scale Test Targets

Architecture rehearsal:

```text
10M Support Cases
100M Messages
10M Attachments refs
1M active cases
100k agents across large multi-tenant system
high webhook burst
high queue concurrency
large knowledge base
```

adjust to actual target customer scale.

---

# 514. Channel Burst

Inbound outage/marketing event can cause burst traffic.

Webhook ingestion must absorb/retry without losing messages.

---

# 515. Backpressure

Provider ingestion may queue normalized events when domain processing is temporarily saturated.

---

# 516. Customer Acknowledgement Under Load

If provider accepted but case processing delayed, do not falsely claim human response/resolution.

---

# 517. Background Jobs

Potential:

```text
SLA warning/breach scan
auto-close resolved cases
waiting-customer reminder
knowledge review due
CSAT invitation
projection rebuild
retention workflow
large export
known-issue notifications
provider retry/dead-letter recovery
```

---

# 518. Job Idempotency

Every recurring job handles retries safely.

---

# 519. No One Job Per Ticket Timer

Use set-based deadline queries.

---

# 520. Observability Metrics

Potential:

```text
support_case_created_total
support_case_resolved_total
support_case_reopened_total
support_first_response_seconds
support_resolution_seconds
support_sla_at_risk_total
support_sla_breach_total
support_unassigned_count
support_queue_depth
support_message_ingest_latency
support_message_send_failure_total
support_provider_webhook_duplicate_total
support_handoff_failure_total
support_csat_score
support_knowledge_search_duration
support_ai_suggestion_duration
```

---

# 521. No High-Cardinality Metric Labels

Do not label metrics with:

```text
CaseId
CustomerId
Email
Phone
AgentId if cardinality unsafe
```

---

# 522. Trace — Inbound Message

```text
Verify Provider
Resolve Tenant/Channel
Deduplicate Event
Normalize Sender/Message
Resolve Conversation/Case
Persist Message
Route/Update Case
Audit/Outbox
Commit
```

---

# 523. Trace — Agent Reply

```text
Authorize Case
Validate Visibility/Channel
Create Outbound Message
Persist/Outbox
Provider Send
Delivery Callback
```

---

# 524. Trace — Case Open

```text
Authorize
Load Case Summary
Load Recent Timeline
Load SLA
Load Support Customer Context Summary
Return
Lazy other tabs
```

---

# 525. Trace — Service Handoff

```text
Authorize Case
Validate Customer/Subject
Build Request Context
Call Service Contract
Store Link
Update Waiting/Handoff
Audit/Outbox
```

---

# 526. Trace — Quality Handoff

```text
Authorize
Validate Complaint Context
Create Quality Issue via Contract
Store Link
Update Case
Audit
```

---

# 527. Trace — SLA

```text
Resolve Policy
Snapshot Calendar/Targets
Compute Deadlines
Apply Pause/Resume Events
Evaluate At Risk/Breach
```

---

# 528. Reporting — Volume

```text
Cases Created
Cases Resolved
Open Backlog
Cases By Channel
Cases By Category
Cases By Product
Cases By Customer
Cases By Queue
```

---

# 529. Reporting — Speed

```text
First Response Time
Next Response Time
Resolution Time
Time Waiting Customer
Time Waiting Internal
Age of Open Cases
```

---

# 530. Reporting — SLA

```text
First Response SLA Met %
Resolution SLA Met %
Breaches
At-Risk
By Customer Tier
By Queue
By Category
```

---

# 531. Reporting — Quality Of Support

```text
CSAT
Reopen Rate
Escalation Rate
Transfer Rate
First Contact Resolution
Repeat Case Rate
```

with documented definitions.

---

# 532. First Contact Resolution Definition

Example definition:

```text
Resolved without handoff/reopen within 7 days
```

Company may configure, but report must show basis.

---

# 533. Reopen Rate

Define denominator/time window explicitly.

---

# 534. Agent Metrics

Use responsibly:

```text
assigned/resolved volume
response time
CSAT
SLA
quality review
```

Do not infer employee performance from presence/online minutes alone.

---

# 535. Workload vs Productivity

Queue/capacity metrics are operational routing tools, not payroll/HR truth.

---

# 536. Contact Reason Pareto

Show top categories/issues driving support volume.

---

# 537. Product Support Insights

Aggregate cases/defects/service handoffs by Product.

Catalog remains source identity.

---

# 538. Quality Feedback Loop

Support trends can feed Quality/Manufacturing product-improvement analysis through approved analytics/events.

Do not auto-create NCR for every ticket.

---

# 539. Service Feedback Loop

Repeat support cases for same Installed Base can surface potential Service issue.

---

# 540. Customer Health Input

CRM may consume high-level Support metrics as relationship context.

Support does not own full Customer Success scoring.

---

# 541. Reporting Drilldown

Every KPI can drill to authorized cases/source definition.

---

# 542. No Mystery KPI

Dashboard must explain:

```text
formula
scope
timezone/calendar
inclusion/exclusion
```

---

# 543. Market Requirement — Arabic First-Class

Support module must be fully usable in Arabic and English.

---

# 544. RTL

Case list, timeline, reply composer, Knowledge and Portal render correctly RTL.

---

# 545. Mixed Arabic/English Content

Support bidi-safe rendering for:

```text
Arabic text + serial number + invoice code + English product name
```

---

# 546. Arabic Search

Normalize common Arabic forms carefully for search without changing stored original content.

---

# 547. Arabic AI

Arabic summarization/classification/reply quality is a launch acceptance concern, not later cosmetic enhancement.

---

# 548. Market Requirement — WhatsApp

WhatsApp is first-class channel in architecture for MENA support behavior.

---

# 549. Market Requirement — Phone

Phone/call logging remains important; do not design digital-only support.

---

# 550. Market Requirement — B2B Account Support

MENA target includes distributors, factories, contractors and service businesses where one Party has many contacts/sites/equipment.

---

# 551. B2B Requester Model

Must separate:

```text
Customer Account
Requester Contact
Site
Installed Equipment
Bill-to/Contract holder where service requires
```

---

# 552. Market Requirement — Multi-Country Calendars

Egypt/Saudi/UAE/etc. can have different weekends/holidays/support hours.

---

# 553. Country Holiday Provider

Use Platform/Country capability where available.

Do not hardcode holiday calendars forever in Support.

---

# 554. Privacy — Regional Readiness

Architecture supports country-specific privacy/retention/data-residency/provider choices.

No hardcoded universal policy.

---

# 555. Provider Data Transfer

External communication/AI providers may process customer data.

Configuration/governance must record provider/purpose and apply approved privacy policy.

---

# 556. Brand / Multi-Brand Support

One tenant may support multiple brands.

Architecture seam:

```text
BrandRef
Channel Accounts
Portal Theme
Knowledge audience
Reply signature
```

---

# 557. Brand != Legal Entity

A brand may span entities or an entity may host multiple brands.

Keep reference model explicit.

---

# 558. White-Label Portal

Later/optional per company branding, aligned with TODIR theming foundation.

---

# 559. Migration — Sources

Likely customer migrations:

```text
Zendesk
Freshdesk
Zoho Desk
Odoo Helpdesk
HubSpot Service
Email shared mailbox
Excel/CSV
Custom systems
```

---

# 560. Migration — Case Data

Import:

```text
Ticket number/external id
Customer/requester mapping
Subject/category
State
Priority
Assignee/team
Created/resolved/closed times
References
```

---

# 561. Migration — Conversation

Import available:

```text
messages
internal notes
attachments refs/files
sender
timestamps
channel
```

with provenance.

---

# 562. Migration — SLA

Do not invent historical SLA results when source lacks reliable policy/event data.

Mark:

```text
Unknown / Not Imported
```

rather than fake compliant/breached.

---

# 563. Migration — Knowledge

Import articles with:

```text
title
body
locale
visibility
state/version where possible
external id
```

---

# 564. Migration — Users/Agents

Map source agents to IAM users where exact.

Historical unknown/inactive agents can remain external historical actor refs.

---

# 565. Migration Provenance

```text
SourceSystem
ExternalId
ImportBatchId
ImportedAt
OriginalCreatedAt
OriginalUpdatedAt
```

---

# 566. No Fake Message

Do not synthesize customer replies to represent status changes.

---

# 567. No Fake Directory Customer

Unknown legacy sender does not force fake Party.

---

# 568. Open Case Migration

Must preserve:

```text
state
waiting on
assignee
customer
last customer message
last agent response
linked downstream work
```

as source permits.

---

# 569. SLA On Migrated Open Cases

Company chooses explicit policy:

```text
start fresh SLA at go-live
continue only if reconstructable
exclude legacy from SLA
```

with visible marker.

---

# 570. External IDs

Use stable external-reference mapping, not one column per legacy product.

---

# 571. Import Preview

Show:

```text
matched customer
unknown requester
duplicate case id
agent mapping
invalid state/category
attachment error
```

before commit where feasible.

---

# 572. Import Idempotency

Re-running same batch/external ID does not duplicate cases/messages.

---

# 573. Import Reconciliation

Report:

```text
source cases
imported
skipped
errors
messages
attachments
open-case counts
```

---

# 574. Go-Live Boundary

After cutover, normal TODIR Support channels become canonical.

Legacy system can remain read-only archive if needed.

---

# 575. Competitor Benchmark — Zendesk

TODIR adopts useful patterns:

```text
omnichannel work
queues/routing
agent capacity
skills
SLA
macros
knowledge
AI agent assistance
```

without copying Zendesk configuration sprawl.

---

# 576. Zendesk Lesson

Routing should eventually combine:

```text
availability
capacity
skills
priority/SLA risk
```

while Day 1 retains simple manual/queue routing.

---

# 577. Competitor Benchmark — Microsoft Dynamics 365 Customer Service

Useful patterns:

```text
Case as durable customer problem
Omnichannel conversations
Unified routing
SLA KPIs
Entitlements
Knowledge
Agent workspace
AI assistance
```

---

# 578. Dynamics Lesson

Case/Conversation/Entitlement/SLAs are separate business concepts and should remain separate in TODIR.

---

# 579. Competitor Benchmark — Salesforce Service Cloud

Useful patterns:

```text
Cases
Omni-Channel
Entitlements / Milestones
Knowledge
Agent console
Customer context
AI assistance
```

---

# 580. Salesforce Lesson

Agent productivity comes from one context-rich console, not from putting every domain table inside Support.

---

# 581. Competitor Benchmark — ServiceNow CSM

Useful patterns:

```text
enterprise case orchestration
skills/capacity routing
cross-department fulfillment
customer communication
major case handling
```

---

# 582. ServiceNow Lesson

TODIR needs strong handoff/orchestration but must not become full ITSM/CMDB platform for normal ERP customers.

---

# 583. Competitor Benchmark — HubSpot Service Hub

Useful patterns:

```text
simple help desk workspace
email/chat/forms/calls/messaging
SLA
portal
knowledge
customer context
```

---

# 584. HubSpot Lesson

Small-company Helpdesk must be understandable without implementation consultants.

---

# 585. Competitor Benchmark — Zoho Desk

Useful patterns:

```text
omnichannel practical support
WhatsApp
multilingual knowledge
self-service
SLA
workflow/automation
AI assistant
```

---

# 586. Zoho Lesson

MENA mid-market support benefits from practical messaging + multilingual Knowledge rather than enterprise-only contact-center assumptions.

---

# 587. Competitor Benchmark — Odoo 19 Helpdesk

Useful simplicity patterns:

```text
teams/stages
email/web form/live chat/WhatsApp intake
SLA
ratings
simple ticket workflow
ERP proximity
```

---

# 588. Odoo Lesson

TODIR keeps Odoo-level ease for simple companies but strengthens:

```text
Case vs Conversation separation
support vs service SLA separation
entitlement resolution
routing/capacity foundation
B2B portal scope
privacy
cross-domain handoffs
```

---

# 589. Competitive Product Target

> **Odoo/HubSpot-like usability + Zendesk omnichannel discipline + Dynamics/Salesforce case/entitlement correctness + ServiceNow-grade orchestration foundation + Zoho-like MENA messaging practicality.**

---

# 590. Official Research References — Zendesk

```text
https://support.zendesk.com/hc/en-us/articles/4409149119514-About-omnichannel-routing
```

---

# 591. Official Research References — Microsoft

```text
https://learn.microsoft.com/en-us/dynamics365/customer-service/implement/overview
https://learn.microsoft.com/en-gb/dynamics365/customer-service/administer/overview-cases
https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/define-service-level-agreements
```

---

# 592. Official Research References — Salesforce

```text
https://help.salesforce.com/
```

Use current Service Cloud Case / Omni-Channel / Entitlement / Knowledge documentation at implementation verification time.

---

# 593. Official Research References — ServiceNow

```text
https://www.servicenow.com/docs/r/customer-relationship-management/crm-omnichannel.html
```

---

# 594. Official Research References — HubSpot

```text
https://knowledge.hubspot.com/help-desk/overview-of-the-help-desk-workspace
https://knowledge.hubspot.com/inbox/set-up-a-support-portal
```

---

# 595. Official Research References — Zoho

```text
https://www.zoho.com/desk/omnichannel-ticketing-system.html
https://www.zoho.com/desk/knowledge-base-software.html
```

---

# 596. Official Research References — Odoo

```text
https://www.odoo.com/documentation/19.0/applications/services/helpdesk.html
```

---

# 597. Official Research Reference — WhatsApp Business

```text
https://business.whatsapp.com/policy
```

Provider/session/template policy must be reverified at implementation because channel rules change.

---

# 598. Market Reference — MEA CX

Regional research reviewed for planning includes 2026 Genesys CX findings emphasizing AI-assisted CX, connected context and faster service expectations across MEA.

Implementation teams must verify current source before market claims.

---

# 599. Market Design Implication

TODIR should prioritize:

```text
Arabic/English
WhatsApp
Phone
Email
B2B account context
fast human handoff
knowledge
AI assistance
multi-country SLA calendars
privacy governance
```

---

# 600. Do Not Copy Competitor UI

Competitor capabilities validate business needs.

TODIR retains its own simpler workflow and domain ownership architecture.

---
# 601. Agent Implementation Philosophy

Build Support as business slices.

Do not start with generic Ticket CRUD, chatbot or omnichannel provider integration before source ownership, Case/Conversation boundaries and privacy work.

---

# 602. Agent Role Directive

Implementation Agent acts as:

```text
Senior Product Architect
Senior Software/Systems Architect
Senior Customer-Support Domain Engineer
Senior Backend Developer
Senior Frontend Developer
Security/Privacy Reviewer
Performance Engineer
```

Business correctness and customer experience first.

---

# 603. Agent Central Question

For every implementation decision answer:

> **What is the customer's actual issue, which Case owns it, which conversation/message records the communication, what customer/source context is authoritative, who owns the next real-world action, which SLA/policy applies, what is visible internally vs externally, and how will we preserve privacy and history?**

---

# 604. Agent Precedence

If conflict:

```text
29 Support pack
→ owning source-domain pack
→ 03 Platform Kernel
→ 01 Master Blueprint
→ 02 Stack
→ approved ADR
```

---

# 605. Agent Must Read

At minimum:

```text
01 02 03 04 10 11 12 13 14 19 24 27 29
all AGENTS.md
approved ADRs
actual public contracts
provider adapter contracts
```

---

# 606. Agent Must Verify Repository Truth

Before code verify:

```text
Directory Party/Contact APIs
CRM Customer 360/read contracts
Sales order/return contracts
Invoice/Payment/Subledger summary contracts
Service Request/Installed Base/Work Order contracts
Quality Issue/NCR contracts
Platform Files
External/Portal IAM
Notifications
Jobs
Approvals
Custom Fields
Outbox/Inbox
RLS
provider integration foundation
```

---

# 607. Agent Must Not Duplicate Directory

No Support-owned canonical Customer/Contact master.

---

# 608. Agent Must Not Duplicate CRM

No Opportunity/Pipeline/Lead engine inside Support.

---

# 609. Agent Must Not Duplicate Service

No Work Order, technician scheduling, Installed Base or Service Contract truth inside Support.

---

# 610. Agent Must Not Duplicate Quality

No NCR/CAPA/root-cause engine inside Support.

---

# 611. Agent Must Not Duplicate Returns

No inventory/sales return transaction truth inside Support.

---

# 612. Agent Must Not Duplicate Payments

No Refund/payment execution engine inside Support.

---

# 613. Agent Must Not Duplicate Mailbox Platform

Support stores support conversation/message history and provider refs.

It does not become generic employee email client.

---

# 614. Agent Task Template

Each implementation phase uses:

```text
TASK
BUSINESS GOAL
REAL-WORLD WORKFLOW
SOURCE OF TRUTH
STATE / INVARIANTS
FILES TO READ
FILES ALLOWED TO MODIFY
DO NOT MODIFY
CHANNEL/PROVIDER REQUIREMENTS
DATABASE REQUIREMENTS
INTEGRATION REQUIREMENTS
SECURITY / PRIVACY REQUIREMENTS
PERFORMANCE REQUIREMENTS
UX REQUIREMENTS
TESTS REQUIRED
ACCEPTANCE CRITERIA
EXIT GATE
REPORT BACK WITH
```

---

# 615. Implementation Sequence

Recommended:

```text
HD00 Repository / Dependency Verification
HD01 Module Boundary + Schema + RLS
HD02 Support Case Core
HD03 Requester / Customer Identity Resolution
HD04 Categories / Priority / Severity
HD05 Teams / Queues / Manual Assignment
HD06 Case Timeline / State History
HD07 Conversation Core
HD08 Message Core / Internal Notes
HD09 Email Adapter Foundation
HD10 WhatsApp Adapter Foundation
HD11 Portal / Web Intake
HD12 Phone / Call Log Foundation
HD13 Customer Context Read Model
HD14 Case References / Source Linking
HD15 Support SLA Core
HD16 Business Calendars / Pause Rules
HD17 Entitlement / Support Policy Resolution
HD18 Escalation
HD19 Routing Rules
HD20 Capacity / Skills Foundation
HD21 Service Handoff
HD22 Quality Handoff
HD23 Sales Return / Refund / CRM Handoffs
HD24 Knowledge Core
HD25 Knowledge Search / Agent Suggestions
HD26 Reply Templates / Macros
HD27 Customer Portal
HD28 CSAT
HD29 Known Issue / Major Case Foundation
HD30 Automation Rules
HD31 AI Tool Contracts / Summaries
HD32 AI Reply / Classification / Translation
HD33 Analytics / Manager Dashboards
HD34 Migration / Import
HD35 Security / Privacy / Retention
HD36 Performance / Scale
HD37 Observability
HD38 Runbooks / Recovery
HD39 Support Agent Acceptance
HD40 Support Manager Acceptance
HD41 Portal Customer Acceptance
HD42 Omnichannel Acceptance
HD43 Service / Quality Handoff E2E
HD44 Security / Privacy Red-Team
HD45 Cross-Module Reconciliation
HD46 Release Gate
```

---

# 616. HD00 Goal

Inspect repository truth before Support implementation.

---

# 617. HD00 Exit Gate

No duplicate customer, service, quality, sales, inventory, finance or portal identity planned.

---

# 618. HD01 Goal

Create:

```text
Todir.Modules.Support.Domain
Todir.Modules.Support.Application
Todir.Modules.Support.Infrastructure
Todir.Support.Contracts
business_support
SupportDbContext
manifest
RLS
architecture tests
frontend module shell
```

---

# 619. HD01 Exit Gate

No direct implementation reference to business modules.

---

# 620. HD02 Goal

Case core:

```text
number
subject
state
waiting on
priority
owner
created/resolved/closed
```

---

# 621. HD02 Exit Gate

Simple manually-created Ticket works without channel integrations.

---

# 622. HD03 Goal

Requester/customer matching without Directory pollution.

---

# 623. HD03 Acceptance

Unknown sender can create case; later agent can safely link to existing Party/Contact with audit.

---

# 624. HD04 Goal

Structured category, priority and severity.

---

# 625. HD04 Exit Gate

Priority and severity remain independent/explainable.

---

# 626. HD05 Goal

Teams, queues and manual ownership.

---

# 627. HD05 Exit Gate

Unassigned queue and claim/assignment are concurrency-safe.

---

# 628. HD06 Goal

Immutable operational history.

---

# 629. HD06 Exit Gate

State/waiting/assignment history proves who changed what and when.

---

# 630. HD07 Goal

Conversation separate from Case.

---

# 631. HD07 Exit Gate

One Case can hold multiple channel conversations without duplicating case truth.

---

# 632. HD08 Goal

Messages/internal notes/drafts.

---

# 633. HD08 Exit Gate

Internal note cannot be exposed through external reply/portal path.

---

# 634. HD09 Goal

Email intake/reply with idempotent threading.

---

# 635. HD09 Exit Gate

Email reply continues correct case; retries do not duplicate message/case.

---

# 636. HD10 Goal

WhatsApp adapter seam and normalized conversation.

---

# 637. HD10 Exit Gate

Provider session/template rules are adapter-owned; Support Case core stays provider-neutral.

---

# 638. HD11 Goal

Secure web/portal intake foundation.

---

# 639. HD11 Exit Gate

Spam/rate limits/identity policy work; case creation remains simple.

---

# 640. HD12 Goal

Manual phone interaction/callback foundation.

---

# 641. HD12 Exit Gate

Phone support works without building telephony infrastructure.

---

# 642. HD13 Goal

One Support Customer Context read model.

---

# 643. HD13 Exit Gate

Agent sees relevant summary with permission filters and no cross-module N+1.

---

# 644. HD14 Goal

Typed links to orders/invoices/products/service/quality objects.

---

# 645. HD14 Exit Gate

References do not grant source-domain permission or duplicate source objects.

---

# 646. HD15 Goal

First-response and resolution SLA.

---

# 647. HD15 Exit Gate

Auto acknowledgement does not falsely satisfy human/meaningful response unless explicit policy.

---

# 648. HD16 Goal

Business calendar and pause/resume.

---

# 649. HD16 Exit Gate

SLA deadline is reproducible from exact policy/calendar snapshot/events.

---

# 650. HD17 Goal

Resolve Support policy/entitlement.

---

# 651. HD17 Exit Gate

Service Contract/Warranty remains `24` truth; Support consumes coverage outcome/reference.

---

# 652. HD18 Goal

Escalation and management attention.

---

# 653. HD18 Exit Gate

Escalation does not mutate resolution/severity invisibly.

---

# 654. HD19 Goal

Rule-based routing.

---

# 655. HD19 Exit Gate

Routing decision explains rule and fallback.

---

# 656. HD20 Goal

Skills/capacity/presence foundation.

---

# 657. HD20 Exit Gate

Agent Presence never becomes HR Attendance.

---

# 658. HD21 Goal

Create/link Service Request from Support Case.

---

# 659. HD21 Exit Gate

Exactly one Service Request per intended handoff under retries; Service owns work lifecycle.

---

# 660. HD22 Goal

Create/link Quality Issue.

---

# 661. HD22 Exit Gate

Complaint and NCR remain distinct; Support receives only approved status/outcome.

---

# 662. HD23 Goal

Return/refund/CRM referral handoffs.

---

# 663. HD23 Exit Gate

Support never edits stock/payment/opportunity truth directly.

---

# 664. HD24 Goal

Knowledge article lifecycle/locales/visibility.

---

# 665. HD24 Exit Gate

Internal/public content cannot cross visibility boundary accidentally.

---

# 666. HD25 Goal

Fast Knowledge search/suggestion.

---

# 667. HD25 Exit Gate

Arabic/English search works; permissions/locale honored.

---

# 668. HD26 Goal

Templates/macros for agent speed.

---

# 669. HD26 Exit Gate

Macro actions re-authorize and cannot bypass workflow.

---

# 670. HD27 Goal

Customer portal support lifecycle.

---

# 671. HD27 Exit Gate

Requester/account-admin scopes cannot see another customer's case.

---

# 672. HD28 Goal

CSAT.

---

# 673. HD28 Exit Gate

Survey link/token cannot be replayed to alter another case/response.

---

# 674. HD29 Goal

Known Issue / widespread customer problem foundation.

---

# 675. HD29 Exit Gate

Many cases can share approved public status without full ITSM implementation.

---

# 676. HD30 Goal

Constrained support automation.

---

# 677. HD30 Exit Gate

No automation loops/arbitrary code/permission bypass.

---

# 678. HD31 Goal

AI summary/classification Tool Layer.

---

# 679. HD31 Exit Gate

AI can fail/unplug without blocking Support workflow.

---

# 680. HD32 Goal

AI reply/translation/knowledge suggestions.

---

# 681. HD32 Exit Gate

Grounding/permissions/human review operate as policy requires.

---

# 682. HD33 Goal

Support dashboards/reports.

---

# 683. HD33 Exit Gate

Every KPI definition/drilldown is explicit and reproducible.

---

# 684. HD34 Goal

Migration from external helpdesk/email/Excel.

---

# 685. HD34 Exit Gate

Open cases/messages/requesters reconcile; no fake customers/messages/SLA history.

---

# 686. HD35 Goal

Privacy/security/retention.

---

# 687. HD35 Exit Gate

Portal/internal-note/file/customer-context leak tests all pass.

---

# 688. HD36 Goal

Scale/performance.

---

# 689. HD36 Exit Gate

Queue/case/timeline/search/ingest load targets measured using PostgreSQL-first implementation.

---

# 690. HD37 Goal

Metrics/tracing/alerts.

---

# 691. HD37 Exit Gate

Provider/SLA/queue/handoff failures are observable without logging customer message bodies.

---

# 692. HD38 Runbooks

Required:

```text
Inbound webhook duplicated
Inbound message not linked to case
Wrong customer linked
Email thread split incorrectly
WhatsApp send rejected/template needed
Outbound reply uncertain result
Internal note exposure incident
Attachment malware/scan failure
SLA deadline wrong
SLA monitor delayed
Case assigned to unavailable/unauthorized agent
Duplicate Service Request handoff
Duplicate Quality Issue handoff
Provider outage
Queue projection stale
Search projection stale
Portal authorization incident
AI bad/unsafe suggestion
Knowledge article published incorrectly
Retention/redaction failure
Import reconciliation mismatch
```

---

# 693. Runbook — Uncertain Outbound Send

Never blindly retry if provider outcome is unknown and duplicate customer message is possible.

Use provider idempotency/status lookup where available.

---

# 694. Runbook — Wrong Customer Link

Controlled correction:

```text
restrict access immediately
reassign identity with audit
review exposed context/messages
privacy incident assessment if needed
```

No direct SQL.

---

# 695. Runbook — Internal Note Exposure

Treat as security/privacy incident.

Do not simply delete evidence and continue.

---

# 696. Runbook — SLA Misconfiguration

Fix policy version for future; historical affected cases require explicit recalculation/migration decision with audit.

---

# 697. Runbook — Projection Rebuild

All read/search projections rebuildable from canonical Support/source facts.

---

# 698. Forbidden Runbook

Never use ordinary recovery such as:

```text
UPDATE support_case SET state='Closed'
DELETE FROM support_message ...
UPDATE support_sla_clock SET breached=false
```

without semantic correction tooling.

---

# 699. HD39 Agent Acceptance

Support Agent can:

```text
open queue
claim case
understand customer context
reply
add internal note
link order/product
create service/quality handoff
set waiting
resolve
```

quickly and safely.

---

# 700. HD39 Arabic Acceptance

Agent handles Arabic inbound, RTL timeline, Arabic Knowledge and Arabic reply without broken layout/bidi/search.

---

# 701. HD40 Manager Acceptance

Manager can answer:

```text
What is unassigned?
What is at risk?
What breached?
What is aging?
Which queue is overloaded?
What issues repeat?
How satisfied are customers?
```

without exporting to Excel for normal daily management.

---

# 702. HD41 Portal Acceptance

External customer can:

```text
create case
see only allowed cases
reply
upload
track status
use Knowledge
rate resolution
```

---

# 703. HD42 Omnichannel Acceptance

Same customer issue can continue across supported channels while preserving one Case context and exact message provenance.

---

# 704. HD43 Service E2E

```text
Customer WhatsApp
→ Support Case
→ Installed Base lookup
→ Service Request
→ Work Order / Visit
→ Service completion
→ Support reply
→ Resolve
→ CSAT
```

---

# 705. HD43 Quality E2E

```text
Customer complaint
→ Support Case
→ Quality Issue
→ Quality investigation/NCR if confirmed
→ approved customer outcome
→ Support communication
→ Resolve
```

---

# 706. HD43 Return E2E

```text
Customer asks return
→ Support intake
→ Sales/Inventory return workflow
→ status back to Support
→ customer informed
```

---

# 707. HD43 Refund E2E

```text
Support request
→ refund review
→ Payments owner execution
→ Support sees authorized status
→ customer informed
```

---

# 708. HD44 Red-Team Goal

Attack privacy, routing, channel, SLA and cross-domain boundaries.

---

# 709. Red-Team — Internal Note Sent To Customer

Must be prevented by UI/API/domain tests.

---

# 710. Red-Team — Portal User Reads Other Customer Ticket

Reject server-side.

---

# 711. Red-Team — Case Reference Grants Invoice Access

Reject.

Reference does not grant source permission.

---

# 712. Red-Team — Email Address Auto-Matches Wrong B2B Account

Reject ambiguous match.

---

# 713. Red-Team — Same Phone Number Means Same Customer

Reject universal assumption.

---

# 714. Red-Team — WhatsApp Message Creates Duplicate Ticket On Retry

Reject via idempotency/thread policy.

---

# 715. Red-Team — Webhook Without Signature

Reject.

---

# 716. Red-Team — Outbound Retry Sends Duplicate Customer Reply

Reject.

---

# 717. Red-Team — Auto Acknowledgement Counts As First Human Response

Reject default.

---

# 718. Red-Team — Waiting Internal Pauses SLA Automatically

Reject universal assumption.

---

# 719. Red-Team — Editing SLA Deadline Directly

Reject.

---

# 720. Red-Team — Change SLA Policy Rewrites Historical Cases

Reject.

---

# 721. Red-Team — Resolve Case Closes Work Order

Reject.

---

# 722. Red-Team — Work Order Completion Always Closes Case

Reject universal assumption.

---

# 723. Red-Team — Customer Complaint Automatically Becomes NCR

Reject.

---

# 724. Red-Team — Support Agent Sets Refund Paid

Reject.

---

# 725. Red-Team — Support Agent Changes Inventory Return State

Reject.

---

# 726. Red-Team — Support Agent Sees Full Ledger

Reject by least privilege.

---

# 727. Red-Team — Merge Deletes Secondary Ticket History

Reject.

---

# 728. Red-Team — Split Duplicates Customer Message As Two Originals

Reject provenance loss.

---

# 729. Red-Team — AI Sends Refund Promise

Reject policy/tool permission.

---

# 730. Red-Team — AI Reads Unauthorized Customer Invoice

Reject retrieval permission.

---

# 731. Red-Team — AI Publishes Knowledge Directly

Reject default.

---

# 732. Red-Team — Translation Replaces Original Customer Text

Reject.

---

# 733. Red-Team — Internal Knowledge Used In Public Bot Answer

Reject audience filter.

---

# 734. Red-Team — Agent Presence Used As Attendance

Reject.

---

# 735. Red-Team — Queue Membership Equals HR Department

Reject.

---

# 736. Red-Team — Provider Credential Stored In DB Row

Reject secret handling.

---

# 737. Red-Team — Raw HTML Executes In Agent UI

Reject sanitization.

---

# 738. Red-Team — Attachment FileId Bypasses Case Authorization

Reject.

---

# 739. Red-Team — Search Snippet Leaks Internal Note

Reject.

---

# 740. Red-Team — Customer Portal Exposes Internal SLA Notes

Reject.

---

# 741. Red-Team — Closed Case Customer Reply Lost

Reject.

Apply explicit reopen/follow-up policy.

---

# 742. Red-Team — Case Is Closed To Improve KPI

Require appropriate permissions/audit and preserve customer reply/reopen metrics.

---

# 743. Red-Team — Spam Exclusion Manipulates SLA Reporting

Reporting definitions/audit must make exclusions visible.

---

# 744. Red-Team — Reassign Customer After Reading Wrong Data

Treat as privacy issue; correction alone may be insufficient.

---

# 745. Red-Team — External Support Admin Sees Sister Company Cases

Reject unless explicitly authorized Party/group scope.

---

# 746. Red-Team — One Lifetime WhatsApp Thread = One Lifetime Ticket

Reject.

Threading policy needs issue/session semantics.

---

# 747. Red-Team — New Customer Topic Auto-Merged By AI

Reject default.

Suggestion only.

---

# 748. Red-Team — Service Entitlement Copied Into Support And Drifts

Reject duplicate truth.

---

# 749. Red-Team — Support Creates Shadow Installed Base

Reject.

Use `24`.

---

# 750. Red-Team — Support Creates Shadow Product

Reject.

Use Catalog.

---

# 751. Red-Team — Support Creates Shadow Invoice

Reject.

Use Invoicing.

---

# 752. Red-Team — Support Creates Shadow Refund Ledger

Reject.

Use Payments/Invoicing.

---

# 753. Test — Manual Case

Create/assign/reply/resolve works with no provider integration.

---

# 754. Test — Unknown Requester

Unknown email creates case without fake Directory Party.

---

# 755. Test — Existing Contact Match

Verified exact context links correctly.

---

# 756. Test — Ambiguous Contact

No automatic wrong Party assignment.

---

# 757. Test — Identity Reassignment

Permission/reason/audit required.

---

# 758. Test — Case/Conversation

One Case accepts multiple conversations.

---

# 759. Test — Conversation/Message

Messages preserve channel/provenance/order.

---

# 760. Test — Internal Note

Never returned to portal/customer response API.

---

# 761. Test — Email Threading

Reply attaches to correct conversation/case.

---

# 762. Test — Email Duplicate

Provider retry does not duplicate.

---

# 763. Test — WhatsApp Duplicate

Webhook replay idempotent.

---

# 764. Test — Delivery Callback Ordering

Read/delivered state handles out-of-order callbacks safely.

---

# 765. Test — Outbound Retry

No double-send.

---

# 766. Test — Claim Concurrency

Two agents claiming same unassigned case result in one owner.

---

# 767. Test — Priority/Severity

Independent values/history.

---

# 768. Test — State/Waiting

Open + WaitingCustomer works without fake state explosion.

---

# 769. Test — Resolved/Closed

Resolution and closure timestamps/state remain distinct.

---

# 770. Test — Reopen

Customer reply follows configured reopen/follow-up policy.

---

# 771. Test — Merge

Secondary history remains.

---

# 772. Test — Split

Message/reference provenance preserved.

---

# 773. Test — SLA First Response

Auto acknowledgement excluded by default.

---

# 774. Test — SLA Business Hours

Deadline respects timezone, hours, weekends, holidays.

---

# 775. Test — SLA Pause

Only allowed waiting reasons pause.

---

# 776. Test — SLA Breach

Breach remains historical after resolution.

---

# 777. Test — SLA Version

Changing policy does not rewrite historical target.

---

# 778. Test — Entitlement

Service contract reference resolves without Support copying contract rules.

---

# 779. Test — Routing

Correct queue/skill/capacity selection and fallback.

---

# 780. Test — Routing Explainability

Reason/rule version visible.

---

# 781. Test — Presence

Does not mutate HR attendance.

---

# 782. Test — Service Handoff Retry

Exactly one intended Service Request.

---

# 783. Test — Quality Handoff Retry

Exactly one intended Quality Issue.

---

# 784. Test — Refund Handoff

Support cannot set final payment/refund state.

---

# 785. Test — Return Handoff

Support cannot write Inventory movement.

---

# 786. Test — Knowledge Visibility

Internal article never exposed to public/portal search.

---

# 787. Test — Knowledge Revision

Published old revision remains historical.

---

# 788. Test — Knowledge Localization

Arabic and English versions maintain correct locale/RTL.

---

# 789. Test — Portal Requester Scope

Requester sees only authorized own cases.

---

# 790. Test — Portal Account Admin Scope

Sees only explicitly authorized Party/site cases.

---

# 791. Test — CSAT Token

Cannot submit for another case/replay outside policy.

---

# 792. Test — AI Permission

AI Tool Layer cannot fetch source data the acting user/workflow cannot access.

---

# 793. Test — AI Internal Knowledge

Public response retrieval excludes internal-only articles.

---

# 794. Test — AI Failure

Manual Support remains functional.

---

# 795. Test — Search Privacy

Unauthorized cases/messages do not appear in results/snippets/counts.

---

# 796. Test — Attachment Privacy

Unauthorized FileId access rejected.

---

# 797. Test — Cross-Tenant RLS

No case/message/article leakage.

---

# 798. Test — Provider Tenant Mapping

Same provider external IDs in two tenants cannot collide/leak.

---

# 799. Test — Queue Scale

Large active queue returns within performance target without N+1.

---

# 800. Test — Timeline Scale

Case with very large message history loads recent window quickly and paginates deterministically.

---
# 801. Test — Webhook Burst

High inbound provider burst preserves all accepted events, deduplicates retries and applies backpressure safely.

---

# 802. Test — SLA Monitor Scale

Large active-case population evaluates at-risk/breach through indexed deadlines, not full scans.

---

# 803. Test — Knowledge Search Scale

Large Arabic/English article base remains usable with PostgreSQL-first search baseline.

---

# 804. Test — Customer Context Performance

Initial context summary is bounded; source tabs are lazy and authorized.

---

# 805. Test — Provider Outage

New internal/manual cases continue; provider sends queue/retry; no false delivered state.

---

# 806. Test — Outbox Failure

Committed case state remains durable; projection/notification can recover.

---

# 807. Test — Projection Rebuild

Queue/SLA/search/customer context projections rebuild from source facts.

---

# 808. Test — Migration Open Cases

Counts/states/last replies/owners reconcile to source migration report.

---

# 809. Test — Historical SLA Unknown

Legacy case without source SLA evidence is not fabricated as compliant/breached.

---

# 810. Test — Arabic Mixed Content

Arabic sentence containing invoice/order/serial English tokens renders and copies correctly.

---

# 811. Test — Accessibility

Keyboard navigation, screen-reader labels, focus states and composer actions meet approved accessibility baseline.

---

# 812. Test — Mobile Agent

Responsive emergency/on-call usage can read/reply/assign without desktop-only blockers where enabled.

---

# 813. Test — Portal Mobile

Customer can create/reply/view case on common mobile browser.

---

# 814. Build Now — MUST

```text
Support Case Core
Requester/Customer resolution
Categories/Priority/Severity
Teams / Queues / Manual Assignment
Case State + Waiting On
Case Timeline
Conversation / Message / Internal Note
Email integration foundation
WhatsApp provider foundation
Web/Portal intake foundation
Phone call log foundation
Customer Context summary
Typed business references
First Response + Resolution SLA
Business calendars / pause rules
Support Policy / Entitlement resolution
Escalation
Rule-based routing foundation
Service handoff
Quality handoff
Return/refund/CRM referral handoffs
Knowledge Base Arabic/English
Knowledge search
Reply templates/macros
Customer Portal
CSAT
Known Issue foundation
Constrained automations
AI Tool Layer foundation
AI summaries/classification/reply/translation suggestions
Reports/Dashboards
Migration
Security/Privacy/Retention
Performance/Observability
Runbooks
Acceptance gates
```

---

# 815. Build Later — Evidence-Based

```text
Full CTI / IVR integration
Advanced voice routing
Advanced workforce management
Predictive AI routing
Autonomous AI support for wider intents
Advanced bot orchestration
Social DMs beyond launch channels
Advanced co-browse
Video support
Advanced sentiment analytics
Customer Success module
Full ITSM Incident/Problem/Change/CMDB
Advanced WFM forecasting/scheduling
Dedicated search/vector infrastructure
Advanced QA scorecards/coaching suite
Advanced conversation intelligence
```

---

# 816. Do Not Build Full ITSM Here

Explicitly outside `29` core:

```text
CMDB
Change Management
CAB
Internal IT Service Management
IT Asset Discovery
DevOps Incident Management
Problem Management framework
```

unless later approved as separate capability.

---

# 817. Do Not Build Full WFM Here

Support routing capacity != workforce scheduling/payroll.

Advanced contact-center WFM can be future evidence-based capability.

---

# 818. Do Not Build Marketing Automation Here

Support WhatsApp/email cannot become campaign engine.

CRM/future Marketing owns campaigns/consent use cases.

---

# 819. Do Not Build Customer Success Here

Support case metrics can feed future Customer Success.

Renewal risk/account success plans are not Helpdesk truth.

---

# 820. Source-of-Truth Matrix

```text
Customer / Party / Contact              → Directory
Commercial Lead / Opportunity           → CRM
Support Case / Ticket                   → 29 Support
Support Conversation / Message          → 29 Support
Support Queue / Routing                  → 29 Support
Support SLA / Case SLA Clock            → 29 Support
Support Knowledge                       → 29 Support
Support CSAT                             → 29 Support
Support Known Issue                     → 29 Support

Product / Variant                       → Catalog
Sales Order / Commercial Return         → Sales
Invoice / Credit Note                   → Invoicing
Receivable / Outstanding                → Subledger
Payment / Refund Movement               → Payments
Stock / Delivery / Return Movement      → Inventory
Installed Base / Service Request        → 24 Service
Work Order / Visit / Technician Work    → 24 Service
Warranty / Service Contract Coverage    → 24 Service
Quality Issue / NCR / CAPA              → 27 Quality
Employee / Attendance                   → HR
File Bytes                              → Platform Files
Portal/Internal Identity                → IAM / External Access
Notifications                           → Platform Notifications
```

---

# 821. Final Case Rule

> **A Support Case represents one customer-support problem/request and can span multiple communications, agents and downstream handoffs without becoming those downstream business objects.**

---

# 822. Final Conversation Rule

> **Conversation is the channel/thread container; it is not the business Case itself.**

---

# 823. Final Message Rule

> **Messages preserve exact communication provenance and visibility; customer replies and internal notes use separate semantic commands.**

---

# 824. Final Customer Rule

> **Support references Directory Customer/Contact when known, but unknown inbound identities do not automatically create or overwrite canonical Party master.**

---

# 825. Final B2B Rule

> **Customer Account, Requester Contact, Site and Installed Equipment remain distinct so business support works for real multi-contact companies.**

---

# 826. Final Context Rule

> **Support composes authorized source-domain context for the agent but does not fork Orders, Invoices, Payments, Service, Quality or Inventory truth.**

---

# 827. Final Priority Rule

> **Priority expresses work order; Severity expresses impact. They may influence each other but are never the same field.**

---

# 828. Final State Rule

> **Case lifecycle stays small; `WaitingOn` explains dependency without creating dozens of fake statuses.**

---

# 829. Final Resolution Rule

> **Resolved means Support believes the issue has been addressed; Closed means the support lifecycle has ended according to policy.**

---

# 830. Final Reopen Rule

> **A later customer reply follows explicit reopen/follow-up policy and is never silently discarded.**

---

# 831. Final Merge Rule

> **Merging duplicate cases preserves both histories and provenance; merge is never deletion.**

---

# 832. Final Queue Rule

> **Support Team, Queue and Assigned Agent are separate operational concepts.**

---

# 833. Final Presence Rule

> **Agent availability/capacity exists for Support routing and must never become HR Attendance or payroll truth.**

---

# 834. Final Routing Rule

> **Routing decisions are deterministic/explainable under the configured rule set, with safe fallback when no eligible agent exists.**

---

# 835. Final Omnichannel Rule

> **WhatsApp, Email, Portal, Chat and Phone are channel adapters around one Case/Conversation model—not separate Helpdesk products.**

---

# 836. Final Provider Rule

> **Channel-specific session, template, delivery and policy rules live behind provider adapters and are reverified as providers change.**

---

# 837. Final WhatsApp Rule

> **WhatsApp is first-class for MENA support but Support Core remains provider/channel neutral and never treats support consent as marketing consent.**

---

# 838. Final Email Rule

> **Support email preserves threading and customer communication history without becoming a generic employee mailbox client.**

---

# 839. Final Phone Rule

> **TODIR supports phone interaction context and CTI integration seams without building a telecom platform.**

---

# 840. Final Portal Rule

> **Portal identity is explicitly authorized to Party/Contact/case scopes; being a company contact does not automatically expose every company ticket.**

---

# 841. Final Internal Note Rule

> **Internal notes are structurally and visually separate from customer-visible replies, and must never leak through portal, notifications, search or AI external answers.**

---

# 842. Final Attachment Rule

> **File bytes belong to Platform Files; Support owns only authorized case/message attachment relationships.**

---

# 843. Final SLA Ownership Rule

> **Support Case SLA belongs `29`; onsite/repair/service-execution SLA belongs `24`.**

---

# 844. Final First Response Rule

> **An automatic acknowledgement does not count as a meaningful human/support first response by default.**

---

# 845. Final SLA History Rule

> **SLA policy/calendar versions and pause/breach events preserve historical truth; configuration changes never rewrite past commitments silently.**

---

# 846. Final Waiting Rule

> **Waiting on Customer may pause SLA only by policy; Waiting on our own internal team does not automatically stop the customer's clock.**

---

# 847. Final Entitlement Rule

> **Support can own operational support policy, but Service Warranty/Contract coverage remains `24` truth and is consumed by reference/outcome.**

---

# 848. Final Service Handoff Rule

> **A Support Case can request and track Service work, but Service Request/Work Order/Visit execution remain `24` source of truth.**

---

# 849. Final Quality Handoff Rule

> **A customer complaint can create a Quality Issue handoff, but Complaint != NCR and Quality owns investigation/disposition/CAPA.**

---

# 850. Final Return Rule

> **Support can intake/request a Return; Sales/Inventory own commercial and physical return execution.**

---

# 851. Final Refund Rule

> **Support can request/referral a refund decision; Payments/Invoicing/Sales own financial execution and status.**

---

# 852. Final CRM Rule

> **Support resolves customer problems; CRM owns lead/opportunity/commercial growth. Handoffs connect them without duplicating either.**

---

# 853. Final Knowledge Rule

> **Knowledge is versioned, localized, audience-controlled and reviewed; raw tickets are never published as customer documentation automatically.**

---

# 854. Final Arabic Rule

> **Arabic is a first-class support language across RTL UI, search, Knowledge, portal, AI assistance and channel messaging.**

---

# 855. Final AI Rule

> **AI assists with summary, classification, retrieval, translation and drafting through authorized tools; it never becomes an uncontrolled source of policy or transactional authority.**

---

# 856. Final AI Grounding Rule

> **Customer-facing AI output is grounded in approved Knowledge and authorized source facts; internal-only knowledge and inaccessible customer data are excluded.**

---

# 857. Final AI Handoff Rule

> **Any autonomous low-risk support path must provide a clear human handoff, with confidence/guardrails and audit.**

---

# 858. Final Privacy Rule

> **Support data is customer data: least privilege, tenant isolation, portal authorization, retention, audit, secure attachments and provider governance are product requirements from Day 1.**

---

# 859. Final Search Rule

> **Search respects the same authorization as direct reads and must never leak case existence, internal-note snippets or restricted customer context.**

---

# 860. Final Idempotency Rule

> **Inbound retries, outbound retries and cross-module handoff retries cannot create duplicate cases, messages, Service Requests, Quality Issues or financial requests.**

---

# 861. Final Performance Rule

> **Fast Support comes from indexed worklists, recent-window timelines, lazy context composition, projections and async provider work—not from copying every ERP domain into Support.**

---

# 862. Final Analytics Rule

> **Every support KPI has a documented definition, scope and drilldown; agent presence/online time is never treated as employee-performance truth.**

---

# 863. Final Market Rule

> **TODIR Support is designed for MENA reality: Arabic/English, WhatsApp, phone, B2B account structures, multi-country SLA calendars, privacy governance and human-centered AI assistance.**

---

# 864. Final Simple Company Rule

> **A small company can receive a message, identify the customer, assign it, reply and resolve it in seconds without configuring enterprise contact-center machinery.**

---

# 865. Final Enterprise Rule

> **A larger organization can enable omnichannel routing, skills, capacity, SLA, entitlements, portal scopes, Knowledge governance, known issues, AI and cross-department handoffs without replacing the Case core.**

---

# 866. Final No-ITSM Rule

> **Customer Support is not an excuse to build CMDB/Change/CAB/Internal ITSM inside this module.**

---

# 867. Final No-Marketing Rule

> **Support communications remain service interactions; marketing campaigns/consent use cases belong their owning capability.**

---

# 868. Final No-Customer-Success Rule

> **Support metrics can feed relationship health, but Customer Success/renewal planning is a separate future business capability if needed.**

---

# 869. Final Agent Rule — Customer Journey

For each slice, the Agent must be able to answer:

```text
Who is contacting us?
Which customer/account are they acting for?
What is the actual support issue?
Which Case owns it?
Which messages/conversations belong to it?
What policy/SLA applies?
Who owns the next response?
What are we waiting on?
Which source-domain object is relevant?
What downstream handoff is required?
What can the customer see?
What can the agent see?
How is privacy protected?
How do retries avoid duplicates?
How is history preserved?
```

If any answer is unclear, implementation is incomplete.

---

# 870. Final Agent Rule — UX

Before adding a field or step ask:

```text
Does the agent/customer need to choose this now?
Can TODIR infer it safely from customer/channel/policy/context?
Can it be progressive disclosure?
```

Prefer safe inference and simple defaults.

---

# 871. Final Agent Rule — Ownership

Before creating a table ask:

```text
Is Support truly the source of truth?
Or should this be a reference/read model/contract to Directory, CRM, Service, Quality, Sales, Inventory, Finance or Platform?
```

---

# 872. Final Agent Rule — Privacy

Before exposing source context ask:

```text
Does this Support user need this data to solve the case?
Is the source permission still enforced?
Could this appear in portal/search/AI by mistake?
```

---

# 873. Final Agent Rule — Communication

Before sending any message ask:

```text
Which channel?
Customer-visible or internal?
Which identity/account?
Is provider policy satisfied?
Is this send idempotent?
Is the content approved/grounded?
```

---

# 874. Final Agent Rule — SLA

Before computing a deadline answer:

```text
Which KPI?
Which SLA policy version?
Which calendar/timezone?
What starts the clock?
What can pause it?
What counts as response/resolution?
```

---

# 875. Final Agent Rule — Handoff

Before cross-domain action answer:

```text
Who owns the resulting object?
Is the handoff synchronous or async?
What idempotency key prevents duplicates?
What status may Support display afterward?
```

---

# 876. Final Agent Rule — AI

Before AI touches a case answer:

```text
What exact task?
Which approved sources?
Which permissions?
What data leaves TODIR?
Is human approval required?
What is the fallback if AI fails?
```

---

# 877. Final Release Philosophy

Support does not ship because a Ticket CRUD page exists.

It ships when:

```text
customer context is reliable
messages are not lost/duplicated
internal notes never leak
queues/routing are fast
SLA is correct/reproducible
service/quality/return/refund handoffs preserve ownership
portal access is safe
Arabic/English work properly
knowledge is governed
AI is bounded and useful
privacy/security tests pass
support managers can operate without spreadsheets
```

---

# 878. HD46 Release Gate — Required Evidence

```text
Architecture boundary tests
RLS report
Portal authorization tests
Internal-note leakage tests
Provider webhook signature/idempotency tests
Outbound send idempotency tests
SLA business-calendar tests
SLA pause/breach/version tests
Requester/customer identity tests
Service handoff tests
Quality handoff tests
Return/refund ownership tests
Knowledge visibility/localization tests
Arabic RTL/search tests
AI permission/grounding tests
Migration reconciliation
Queue/timeline/search benchmarks
Webhook burst benchmark
Runbooks
Known limitations
```

---

# 879. Release Sign-Off

Recommended:

```text
Product Architect
Customer Support Manager
Support Agent representative
Service Manager
Quality Lead
CRM/Sales Lead
Security/Privacy Reviewer
Integration Engineer
Performance Engineer
QA Lead
```

---

# 880. No Separate Support Subfeature Packs

Do **not** create separate architecture packs for:

```text
Ticketing
Omnichannel
SLA
Routing
Knowledge
Portal
CSAT
Support AI
Known Issues
```

They are slices of `29 Helpdesk & Customer Support`.

---

# 881. Future Separate Capability — Contact Center Platform

Only if market evidence requires deep:

```text
IVR
voice recording platform
workforce management
quality monitoring/coaching
predictive dialer
telephony infrastructure
```

should a dedicated contact-center capability be considered.

---

# 882. Future Separate Capability — Customer Success

If needed later:

```text
health score
renewal risk
success plans
QBRs
adoption
expansion
```

can become separate customer-success capability.

---

# 883. Future Separate Capability — ITSM

If internal IT service management becomes a real product goal, design a dedicated boundary rather than stretching Customer Support.

---

# 884. Final Commercial Positioning

TODIR Helpdesk should not compete by saying:

```text
"We also have tickets."
```

It should compete on:

```text
Customer issue
+
ERP context
+
Service/Quality handoff
+
Arabic/WhatsApp practicality
+
Fast agent UX
+
Governed AI
```

inside one coherent product.

---

# 885. Final Differentiator

> **The agent can understand the customer's issue together with the relevant order, invoice, payment, product, installed equipment, warranty, service work and quality context without duplicating ownership or asking the customer to repeat what TODIR already knows.**

---

# 886. Final Experience Target

Real daily workflow:

```text
Customer contacts company
→ TODIR identifies context
→ Case created/continued
→ correct queue/agent
→ SLA visible
→ agent sees relevant ERP context
→ answer immediately or hand off
→ downstream work updates context
→ agent communicates resolution
→ customer rates support
→ organization learns from repeated issues
```

---

# 887. Final Product Verdict

TODIR `29 Helpdesk & Customer Support` is release-ready only when it behaves as a **customer-resolution layer over the ERP**, not an isolated ticket database.

---

**END OF FILE — `29_ERP_SAAS_HELPDESK_CUSTOMER_SUPPORT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`**
