# 30 — TODIR ERP SaaS Subscriptions & Recurring Billing Requirements & Technical Design Pack

**File:** `30_ERP_SAAS_SUBSCRIPTIONS_RECURRING_BILLING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Product:** **TODIR**  
**Status:** PRODUCT + CONTRACT LIFECYCLE + RECURRING BILLING + AMENDMENTS + PRORATION + RENEWALS + COLLECTION ORCHESTRATION + ANALYTICS + REGIONAL COMPLIANCE + IMPLEMENTATION BASELINE  
**Version:** v1.0  
**Date:** 2026-10-08  
**Audience:** Product Owner, Product Architect, Finance Lead, Billing Operations Lead, Revenue Operations Lead, Sales Operations Lead, Customer Success/Service Lead, Accountant, Backend/Frontend Leads, Integration Architect, Payments Engineer, Compliance/Tax Engineer, Security Engineer, Performance Engineer, QA, Implementation Agent  
**Risk Class:** **CRITICAL — RECURRING REVENUE + CUSTOMER CONTRACT + BILLING ACCURACY + TAX DOCUMENTS + COLLECTIONS + AUDITABILITY + RETENTION**

---

## Canonical TODIR dependencies

Primary design sources:

- `01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`
- `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`
- `04_ERP_SAAS_DIRECTORY_PARTY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `05_ERP_SAAS_CATALOG_PRODUCT_UOM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `06_ERP_SAAS_FINBASE_CURRENCY_PAYMENT_TERMS_MONEY_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `07_ERP_SAAS_PRICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `08_ERP_SAAS_TAX_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `09_ERP_SAAS_LEDGER_POSTING_ENGINE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `10_ERP_SAAS_SUBLEDGER_OPEN_ITEMS_SETTLEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `11_ERP_SAAS_INVOICING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `12_ERP_SAAS_PAYMENTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `14_ERP_SAAS_SALES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `16_ERP_SAAS_ACCOUNTING_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `25_ERP_SAAS_BANKING_CASH_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`
- `29_ERP_SAAS_HELPDESK_CUSTOMER_SUPPORT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

### Architecture correction locked by this pack

The master blueprint originally listed `subscriptions -> invoicing + catalog + pricing` as hard dependencies. This pack corrects the direct ownership boundary:

```text
subscriptions -> directory + invoicing + catalog + pricing
```

`Directory` is a **direct hard dependency** because Subscriptions owns a commercial contract with a canonical Party/Customer and must never obtain customer identity transitively through Invoicing.

### Locked upstream truths

- Directory owns canonical Party, Customer and Contact identity.
- Catalog owns Product/Service identity and UOM truth.
- Pricing owns reusable price lists/rules; a signed contract stores an immutable commercial price snapshot.
- Tax owns tax definitions/calculation rules; Subscriptions never hard-codes VAT or country tax constants.
- Invoicing owns legal invoices, credit notes and debit notes.
- Subledger owns receivable/open-item and settlement truth.
- Payments owns payment attempts, gateway/token references and money movement orchestration.
- Ledger owns journal truth; Subscriptions never posts accounting directly.
- Accounting may later own/coordinate deferred revenue and recognition workflows; Subscription lines only provide service-period evidence.
- Sales owns quotation/order truth; Subscriptions may originate from Sales but never mutates Sales records as contract truth.
- Service owns work orders/visits; Helpdesk owns support cases; Subscription may expose commercial coverage/benefit facts only.
- Platform owns Jobs, Audit, Files, Notifications, Numbering, Custom Fields, Approvals, Outbox/Inbox and transaction coordination.
- Country Compliance Providers own e-invoice/e-note authority behavior.
- AI follows `AI -> Tool Layer -> Permissions -> Application Commands/Queries -> Domain`; never AI -> Database.
- SaaS billing for the TODIR product itself belongs to the **platform/control plane**, not this tenant ERP module.

# 0. Executive Decision

TODIR `30 Subscriptions & Recurring Billing` is the canonical **recurring commercial contract and billing-orchestration capability** for tenant businesses.

It answers:

> **What recurring product/service did this customer contract for, on which commercial terms, for which service period, what changed and when, what should be billed now, what must wait until renewal, what has already been billed, what remains collectible, and can TODIR reproduce every amount later without rewriting history?**

The module is **not** a recurring-invoice copier. It owns contract lifecycle and recurring billing intent; legal documents and money movement remain in their canonical modules.

---

# 1. Central Product Question

> **Can a small company create a recurring customer contract in under a minute, while a larger company can later handle amendments, proration, renewals, auto-collection, multiple lines, multiple currencies, future-effective changes and usage seams without replacing the original subscription model?**

---

# 2. Product Law — Contract Is Business Truth, Invoice Is Financial/Legal Output

```text
Subscription Contract
!=
Invoice
```

A contract says what should be provided and billed over time. Invoicing says what legal/financial document was issued. One contract can generate many invoice requests and adjustments.

---

# 3. Product Law — Recurrence Is Not Copying The Last Invoice

Never implement recurring billing as `clone previous invoice`. The previous invoice may contain tax results, discounts, corrections, one-time items, manual notes or historic prices that must not become new commercial master truth.

Recurring billing is recomputed from the **effective contract revision + billing period + policy snapshots**.

---

# 4. Product Law — Historical Contract Terms Are Immutable

Once a commercial revision becomes effective and has financial consequences, it is historical evidence. Quantity, price, discount, term or billing cadence changes are represented through an explicit `ContractAmendment` and new effective revision; no silent overwrite.

---

# 5. Product Law — Small Company First

A small company must be able to run:

```text
Customer
→ Service/Product
→ Price
→ Every Month / Quarter / Year
→ Start Date
→ Auto Renew?
→ Activate
```

without configuring rating engines, complex revenue allocation, usage meters, commitments or multi-step amendment approval unless needed.

---

# 6. Product Law — Enterprise Depth Is Progressive

Larger organizations may enable:

```text
Multiple contract lines
Future-effective amendments
Billing alignment
Advance / arrears billing
Proration policies
Renewal notice periods
Renewal uplift
Collection policies
Customer self-service
Contract benefits/coverage
Usage/rating seam
Revenue-recognition evidence
Multi-entity reporting
Advanced audit / SoD
```

without changing the core aggregate.

---

# 7. Product Law — Subscription Is Not Installment Financing

A sale of an asset for `600,000` paid as `50,000 x 12` is normally an Invoice/Open Item payment schedule, not a Subscription. Subscription means an ongoing commercial entitlement/service/product supply over time.

---

# 8. Product Law — Subscription Is Not Rental

Rental additionally owns asset availability, checkout/return, deposits, damage and late-return logic. A future Rental module may call recurring billing, but this module must not become rental inventory.

---

# 9. Product Law — Subscription Is Not Service Execution

A maintenance/support contract may create commercial coverage. Service still owns Work Orders and Visits; Helpdesk still owns Support Cases. Subscription records what the customer bought, not operational execution.

---

# 10. Product Law — Subscription Is Not TODIR SaaS Entitlement

The TODIR control plane may contain `Plan -> Subscription -> EntitlementGrant` for selling TODIR modules. Tenant ERP subscription records belong to tenant business data and must not share tables, IDs, state machines or billing jobs with control-plane SaaS billing.

---

# 11. Absolute Domain Distinctions

```text
Subscription Contract != Sales Order
Subscription Contract != Invoice
Subscription Contract != Open Item
Subscription Contract != Payment
Subscription Contract != Payment Method Token
Subscription Contract != Service Contract Execution
Subscription Contract != Support Ticket
Subscription Contract != Rental
Subscription Contract != Installment Schedule
Subscription Contract != Platform Entitlement
Subscription Contract != Revenue Recognition Schedule
```

---

# 12. Lifecycle Distinctions

```text
Active != Paid
Paid != Billed
Billed != Collected
Paused != Cancelled
NonRenewing != Cancelled
Expired != Cancelled
Suspended != Paused
Trialing != Active Paid Service
Amended != Renewed
Renewed != Reactivated
```

---

# 13. Billing Distinctions

```text
Service Period != Invoice Date
Invoice Date != Due Date
Due Date != Collection Date
Billing Frequency != Contract Term
Billing Anchor != Billing Timing
Proration != Discount
Credit Note != Contract Amendment
```

---

# 14. Pricing Distinctions

```text
Price List Rule != Contracted Price Snapshot
Contracted Price != Invoice Line Tax-Inclusive Gross
Renewal Uplift != Mid-Term Price Edit
Quantity Change != Usage Record
One-Time Charge != Recurring Charge
```

---

# 15. Module Name

Technical module key recommendation:

```text
subscriptions
```

Projects:

```text
Todir.Modules.Subscriptions.Domain
Todir.Modules.Subscriptions.Application
Todir.Modules.Subscriptions.Infrastructure
Todir.Subscriptions.Contracts
```

---

# 16. Database Schema

Recommended PostgreSQL schema:

```text
business_subscriptions
```

No Subscription table may live in `billing`, `sales`, `accounting` or platform control-plane schemas.

---

# 17. DbContext

```text
SubscriptionsDbContext
```

It owns only Subscription-domain tables and projections explicitly assigned to this module.

---

# 18. Hard Dependencies

```text
Directory
Catalog
Pricing
Invoicing
```

`FinBase`, Tax, Ledger and Subledger are reached through Invoicing and approved shared contracts where needed; do not create redundant implementation dependencies merely for convenience.

---

# 19. Optional Capabilities

Optional providers may include:

```text
Payments / AutoCollection
Approvals
Sales Origin
Service Coverage Consumer
Helpdesk Coverage Consumer
Portal / SelfService
Recurring Fulfillment
Reporting
Compliance projection
CRM context
```

Every optional capability requires an explicit fallback.

---

# 20. Canonical Aggregate

The main aggregate root is:

```text
SubscriptionContract
```

It protects term, lifecycle, active commercial revision, billing schedule, amendment ordering and billability invariants.

---

# 21. Subscription Contract Concept

```text
SubscriptionContract {
  Id
  Number
  TenantId
  LegalEntityId
  CustomerPartyRef
  ContractCurrency
  State
  StartDate
  CurrentTermStart
  CurrentTermEnd?
  BillingScheduleRef
  ActiveRevisionNo
  RenewalPolicyRef
  CollectionPolicyRef?
  OriginRef?
  ActivatedAt?
  NonRenewingAt?
  CancelledAt?
  Version
}
```

---

# 22. Contract Numbering

Use Platform Numbering. Example:

```text
SUB-2026-000123
```

Human-readable number is not the primary key and may be scoped by legal entity if numbering policy requires it.

---

# 23. Owning Legal Entity

Every active contract belongs to exactly one legal entity for billing/tax/accounting context. A group-level commercial arrangement can reference multiple child contracts; do not issue one legal invoice across unrelated legal entities without explicit legal capability.

---

# 24. Customer Party

`CustomerPartyRef` points directly to Directory. Contract snapshots preserve customer display/legal context required for historical readability, but Directory remains canonical identity owner.

---

# 25. Payer vs Service Recipient

Support future separation:

```text
Contract Customer
Payer Party
Service Recipient / Beneficiary
```

V1 may default all to the same Party. Do not hard-code that they must always be identical.

---

# 26. Bill-To Contact

The contract may reference an authorized billing contact from Directory. Contact details should be resolved/snapshotted under Invoicing rules; Subscription does not create shadow contacts.

---

# 27. Contract Currency

A contract has one commercial billing currency by default. Mixed-currency lines in one contract are forbidden in V1; create separate contracts if required. This simplifies invoicing, proration, renewal and MRR normalization.

---

# 28. Contract Term

Separate contract term from billing frequency. Example:

```text
Contract Term: 12 months
Billing Frequency: monthly
```

or

```text
Evergreen Contract
Billing Frequency: quarterly
```

---

# 29. Term Types

Initial supported values:

```text
FixedTerm
Evergreen
```

A FixedTerm contract can auto-renew, require manual renewal, or become non-renewing.

---

# 30. Contract Start Date

`StartDate` is the commercial/service commencement baseline, not automatically the first invoice date. Trial, advance billing and invoice lead days can make those differ.

---

# 31. Contract End Date

Fixed-term contracts have a term end derived from the explicit term policy. Do not calculate end dates with naive `AddMonths` only; use billing/term anchor semantics for month-end and leap-day cases.

---

# 32. Contract Revision

Every effective commercial configuration is represented as a revision:

```text
ContractRevision {
  ContractId
  RevisionNo
  EffectiveFrom
  EffectiveTo?
  SourceAmendmentRef?
  PricePolicySnapshot
  LineSnapshots[]
  BillingPolicySnapshot
  CreatedAt
}
```

---

# 33. Revision Non-Overlap Invariant

For one contract there must never be two effective commercial revisions covering the same instant/day under the selected date granularity.

---

# 34. Revision Future Scheduling

A future amendment creates a scheduled future revision. The current revision remains active until `EffectiveFrom`. Billing preview must show both current and future known changes.

---

# 35. Subscription Line

```text
SubscriptionLine {
  Id
  ContractId
  ProductRef
  DescriptionSnapshot
  UomRef?
  ChargeType
  Quantity
  PriceSnapshot
  DiscountSnapshot?
  ServicePeriodPolicy
  StartDate
  EndDate?
  BenefitRefs[]?
}
```

---

# 36. Product Reference

Lines reference Catalog Product/Service. Product rename/deactivation must not make historical contracts unreadable; store display snapshot where appropriate while retaining canonical reference.

---

# 37. Charge Types — V1

Build now:

```text
RecurringFixed
RecurringPerUnit
OneTime
```

Do not model every future rating strategy in the first release.

---

# 38. Charge Types — Future

Design seams for:

```text
UsagePerUnit
TieredUsage
VolumeUsage
CommitPlusOverage
MinimumCommitment
PrepaidCreditBurndown
```

These are DESIGN NOW / BUILD LATER unless validated by target sectors.

---

# 39. One-Time Charge

A contract may include setup, implementation, activation or installation fees. A One-Time line tracks whether its billing trigger has been consumed so retrying billing jobs cannot charge it twice.

---

# 40. Recurring Fixed Charge

Example: `Managed IT Support = 10,000 EGP/month`. Quantity may remain 1; amount comes from the contracted price snapshot.

---

# 41. Recurring Per-Unit Charge

Example: `25 users × 80 SAR/user/month`. Quantity changes are amendments, not usage events, unless business semantics genuinely mean metered consumption.

---

# 42. Quantity Precision

Use Product/UOM precision rules. Subscription quantity is decimal-capable because recurring services may be licensed by seat, device, GB, hour pack, branch or other units.

---

# 43. Contracted Price Snapshot

Pricing is master truth when quoting/resolving a price. On contract activation/amendment, Subscriptions stores an immutable agreed snapshot containing amount, currency, pricing source/reference, discount basis and relevant explanatory metadata.

---

# 44. Why Price Snapshot Is Mandatory

Changing a Price List tomorrow must not silently change a signed active contract yesterday. Renewal can deliberately choose current Pricing, but active term history must remain reproducible.

---

# 45. Price Override

Contract-specific negotiated price is allowed subject to permission/approval policy. Store who overrode, reason, source/base price and approval reference where applicable.

---

# 46. Discount Snapshot

Discount may be percentage or fixed amount according to Pricing/Invoicing support. Never re-resolve a historical discount after the billed period is finalized.

---

# 47. Tax Boundary

Subscription does not store VAT rates as canonical truth. InvoiceRequest passes line/service-period/commercial facts; Invoicing/Tax determine legal tax result at issuance time using effective rules and snapshots.

---

# 48. Billing Schedule

```text
BillingSchedule {
  FrequencyUnit
  FrequencyInterval
  AnchorPolicy
  AnchorDay?
  BillingTiming
  InvoiceLeadDays?
  ProrationPolicy
  DueTermsRef?
  CalendarRef?
}
```

---

# 49. Supported Frequencies — V1

```text
Weekly
Monthly
Quarterly
SemiAnnual
Annual
CustomMonths(N)
```

Daily recurring invoices are not a default business flow and should require explicit product evidence before support.

---

# 50. Billing Frequency != Contract Term

A one-year term billed monthly is not twelve independent contracts. Renewal occurs at term boundary; billing occurs at billing-period boundary.

---

# 51. Billing Timing

Initial values:

```text
InAdvance
InArrears
BeforePeriodStart(N days)
```

This supports common ERP/service arrangements without forcing a single SaaS-style model.

---

# 52. Service Period

Every billable recurring charge carries explicit service period start/end. Service period is later used for customer explanation, tax/document context where required, deferred revenue and dispute resolution.

---

# 53. Invoice Generation Date

Invoice generation date is computed from the billing schedule and may be before, at, or after service-period start. It must never be assumed to equal due date or collection date.

---

# 54. Due Date

Due date is produced through Invoicing/FinBase Payment Terms. Subscription may carry a payment-terms reference/snapshot choice but does not own receivable due-date truth after invoice issuance.

---

# 55. Collection Date

Auto-collection policy may attempt payment on invoice date, due date, or configured offset. Actual attempts/results belong to Payments.

---

# 56. Billing Anchor Policy

Supported anchors:

```text
Anniversary
DayOfMonth
EndOfMonth
CalendarPeriod
```

Each has deterministic month-end and leap-year rules.

---

# 57. Anniversary Billing

Example: Start 17 October -> service periods normally 17 Oct–16 Nov, 17 Nov–16 Dec. Do not silently align to calendar months.

---

# 58. Calendar Billing

Example: Start 17 October with monthly calendar alignment -> first stub period 17–31 Oct, then 1–30 Nov. Stub treatment is governed by proration policy.

---

# 59. Day-Of-Month Billing

If anchor day is `15`, periods align around that day. Define behavior when a month lacks the requested day; recommended fallback is month-end while retaining original anchor intent.

---

# 60. End-Of-Month Billing

Explicit EOM anchor must preserve EOM semantics: Jan 31 -> Feb 28/29 -> Mar 31, rather than drifting to the 28th permanently.

---

# 61. Leap-Day Contract

A Feb 29 anniversary requires an explicit policy for non-leap years, typically Feb 28 or Mar 1, and the chosen policy must be snapshotted on the contract.

---

# 62. Business Date & Timezone

Billing business dates are evaluated in the owning Legal Entity calendar/timezone. Jobs may run in UTC internally but must not create off-by-one customer periods because of server timezone.

---

# 63. Billing Calendar

Optional calendar can control business-day adjustment for invoice-generation dates. Do not automatically shift service-period boundaries unless policy explicitly says so.

---

# 64. Proration

Proration is the deterministic allocation of recurring value when a line or term starts, ends or changes inside a normal billing period. It produces explainable charge/credit components; it is not a generic discount.

---

# 65. Proration Policies — V1

```text
NoProration
ProrateByCalendarDays
FullPeriodFromChange
NextPeriodOnly
```

Default recommendation for time-based services: `ProrateByCalendarDays`, configurable per tenant/contract template.

---

# 66. Day-Based Proration

For a period with `D` accountable days and `R` remaining accountable days:

```text
credit old = old recurring amount * R / D
charge new = new recurring amount * R / D
net = charge new - credit old
```

Rounding follows Money rules and must reconcile exactly to document line totals.

---

# 67. No Millisecond Rating In V1

TODIR is an ERP, not a telecom rating engine. V1 proration is business-date/day based. Time-of-day precision is DESIGN LATER if a validated market requires it.

---

# 68. Proration Preview

Before committing an immediate change, UI/API must return an impact preview showing old unused credit, new remaining-period charge, expected adjustment, next normal recurring amount and effective date.

---

# 69. Proration Reproducibility

Store calculation inputs/version so a support accountant can reproduce why a credit or charge was calculated. Never store only the net result with no lineage.

---

# 70. Proration & Tax

Subscription calculates commercial allocation basis; Tax/Invoicing calculate document tax. Never calculate tax credits inside Subscription with hard-coded VAT arithmetic.

---

# 71. Proration & Physical Goods

Time-based proration is not automatically valid for physical recurring deliveries. If recurring fulfillment is enabled, delivery-based adjustment policy must be explicit; V1 may disallow automatic time proration for shippable lines.

---

# 72. Contract Amendment

```text
ContractAmendment {
  Id
  ContractId
  Type
  RequestedAt
  EffectiveAt
  RequestedBy
  Reason
  BeforeRevision
  ProposedRevision
  FinancialImpactPreview
  ApprovalRef?
  State
  AppliedAt?
}
```

---

# 73. Amendment Types

Initial types:

```text
AddLine
RemoveLine
QuantityChange
Upgrade
Downgrade
PriceOverride
DiscountChange
BillingScheduleChange
Pause
Resume
ExtendTerm
CancelImmediately
CancelAtTermEnd
CustomCommercialChange
```

---

# 74. Immediate Amendment

An immediate amendment becomes effective on the authorized business date and may create prorated adjustments. It must never rewrite an already issued invoice.

---

# 75. Next-Period Amendment

Change is stored now but takes effect at the next billing-period boundary. No mid-period proration is generated.

---

# 76. Next-Renewal Amendment

Change becomes part of the next term, leaving current-term commercial terms untouched.

---

# 77. Specific-Date Amendment

Future effective date may be scheduled. The engine validates that it does not create impossible revision overlap or precede already-finalized financial history.

---

# 78. Amendment Approval

High-impact changes may call optional Approvals capability based on amount, discount, retroactivity, termination or policy. Subscription state machine remains code-owned; approval only authorizes the command path.

---

# 79. Retroactive Amendment

Retroactive commercial changes are high-risk. V1 should default to forbidden after affected periods have been legally invoiced; corrections require explicit credit/debit note workflow and elevated permission rather than history rewrite.

---

# 80. Upgrade

Upgrade is a semantic wrapper around a revision change that increases/changes contracted service. It can be immediate with proration, next period, or next renewal according to policy.

---

# 81. Downgrade

Downgrade may be restricted until renewal or require notice/minimum-term checks. Do not assume every customer can reduce quantity immediately.

---

# 82. Quantity Change

Seat/device/branch quantity change is a contractual amendment. Store old quantity, new quantity, effective date and proration decision.

---

# 83. Add Line

A recurring add-on can start immediately, next period or next renewal. One-time setup fee may accompany the line and must be billed once only.

---

# 84. Remove Line

Removing a line ends future service eligibility and billing from its effective date; historical line revisions remain visible.

---

# 85. Price Change Mid-Term

Never change active contracted price merely because Pricing master changed. Mid-term price change requires an amendment with authorization and explicit effective date.

---

# 86. Future Price Change

Allow scheduling `effective 2027-01-01` today. Billing preview and renewal views must surface scheduled future price changes so operators do not create contradictory amendments.

---

# 87. Term Extension

Extending term modifies current/future term end but does not automatically change billing frequency or price. Renewal and extension remain distinct audit concepts.

---

# 88. Renewal Policy

```text
RenewalPolicy {
  Mode
  NoticeDays?
  InternalReviewDays?
  PricePolicy
  UpliftPercent?
  ApprovalPolicy?
}
```

---

# 89. Renewal Modes

```text
ManualRenewal
AutoRenew
NoRenewal
EvergreenContinuation
```

---

# 90. Renewal Price Policies

```text
KeepCurrentContractedPrice
ResolveCurrentPricing
ApplyUpliftPercent
ManualReviewRequired
```

The selected policy is snapshotted per term.

---

# 91. Renewal Uplift

Example: renew annual support with `+8%`. Uplift produces a new term/revision with auditable previous price, uplift rule, rounded new price and approval evidence where required.

---

# 92. Renewal Notice

Support customer notice and internal review windows. A renewal job may create tasks/notifications ahead of term end; it must not silently renew before policy conditions are satisfied.

---

# 93. Manual Renewal

Manual renewal creates a proposed next term for review. Current contract may enter `RenewalDue`/`NonRenewing` presentation state without losing current service until term end.

---

# 94. Auto Renewal

Auto renewal is a deterministic domain action performed by idempotent job at a defined boundary. It creates a new term/revision; it does not clone the last invoice.

---

# 95. Renewal Failure

Configuration/price resolution failure must move renewal to an exception queue and alert operations. Never invent a price or let a contract disappear silently.

---

# 96. Non-Renewing

`NonRenewing` means current term continues but no next term will be created. It is not immediate cancellation.

---

# 97. Contract Lifecycle States

Recommended lifecycle:

```text
Draft
→ PendingActivation
→ Trialing? / Active
→ NonRenewing?
→ Expired

Active → Paused → Active
Active → Suspended → Active
Active/Paused/Suspended → Cancelled
```

Keep state count small; represent billing/payment nuance separately.

---

# 98. Draft

Editable proposal/configuration inside Subscription domain before activation. It has no billing entitlement and must not create invoice requests.

---

# 99. Pending Activation

Validation may include customer validity, product activity, required price snapshot, billing schedule, legal entity, currency, approval and initial payment/mandate policy.

---

# 100. Activation

Activation freezes revision 1, calculates first term/billing periods as needed, emits `SubscriptionActivated@1` and schedules billing. Activation must be idempotent.

---

# 101. Trial

Trial is an optional lifecycle phase with explicit start/end and conversion policy. A free trial is not represented as an unexplained 100% discount unless commercial policy explicitly wants that.

---

# 102. Trial Conversion

At trial end, contract may activate paid term, require confirmation/payment method, extend trial or cancel. Every path is explicit and auditable.

---

# 103. Pause

Pause temporarily interrupts some combination of service eligibility and billing according to policy. It is not deletion or cancellation.

---

# 104. Pause Policy

Possible policy dimensions:

```text
PauseBilling: yes/no
ExtendTermForPause: yes/no
CreditUnusedTime: yes/no
SuspendBenefits: yes/no
AllowInvoicesAlreadyGenerated: yes/no
```

V1 should offer opinionated presets rather than arbitrary combinations.

---

# 105. Resume

Resume ends pause and creates a new effective revision/state transition. Depending on policy it may create proration, move next billing date or simply continue original term.

---

# 106. Suspension

Suspension is an operator/system consequence (e.g., delinquency/compliance hold) distinct from customer-requested pause. Suspension reason and source must be explicit.

---

# 107. Cancellation

Cancellation records requested/effective dates, reason, actor and financial consequences. It never deletes contract/billing/invoice history.

---

# 108. Immediate Cancellation

May generate commercial credit/adjustment only if policy/legal document status allows. Issued invoices remain immutable; use credit/debit notes through Invoicing/Compliance.

---

# 109. End-Of-Term Cancellation

Implemented as non-renewal/end-at-term where possible. Existing current-term billing continues according to contract.

---

# 110. Cancellation Reason

Use configurable reason codes plus optional note. Examples: customer request, price, nonpayment, duplicate, service unavailable, migration, fraud/compliance hold. Reasons feed churn analytics but never bypass authorization.

---

# 111. Reactivation

A cancelled/expired contract may be reactivated only by explicit policy. Prefer new term/revision preserving prior lifecycle rather than pretending the interruption never occurred.

---

# 112. Billing Period Entity

```text
BillingPeriod {
  Id
  ContractId
  PeriodSequence
  PeriodStart
  PeriodEnd
  BillingDate
  State
  RevisionNo
  ExpectedCommercialAmount?
  InvoiceRequestRef?
  InvoiceRef?
  GeneratedAt?
  FinalizedAt?
}
```

---

# 113. Why BillingPeriod Is First-Class

`NextInvoiceDate` alone cannot explain retries, missed periods, partial periods, amendments, generated documents or migration. BillingPeriod is the auditable scheduling unit.

---

# 114. Billing Period States

Recommended:

```text
Planned
Ready
InvoiceRequested
Billed
Skipped
Adjusted
Failed
Closed
```

Payment status is not a BillingPeriod state.

---

# 115. Billing Period Uniqueness

Unique invariant must prevent duplicate normal period for the same contract/sequence/revision purpose. Database uniqueness backs domain checks.

---

# 116. Billing Revision

If a period requires a later adjustment, preserve the original period and add an adjustment/revision reference rather than mutating billed history.

---

# 117. Billing Horizon

Do not materialize 20 years of monthly rows on activation. Create a bounded planning horizon and extend via job, while preserving deterministic period calculation.

---

# 118. Billing Scheduler

A worker queries `Ready`/due billing periods using indexed business dates, claims work safely, resolves the effective revision and emits invoice requests idempotently.

---

# 119. Billing Idempotency Key

Canonical key pattern:

```text
subscription:{ContractId}:period:{PeriodId}:billing-revision:{RevisionNo}
```

Same key submitted repeatedly to Invoicing must produce the same logical document request, not duplicate invoices.

---

# 120. One-Time Charge Idempotency

One-time billable unit has its own consumption key. Retrying a worker after timeout cannot charge setup fee twice.

---

# 121. InvoiceRequest Contract

Subscription produces a DTO similar to:

```text
SubscriptionInvoiceRequest {
  IdempotencyKey
  LegalEntityRef
  CustomerPartyRef
  Currency
  ContractRef
  BillingPeriodRef
  Lines[] {
    ProductRef
    Quantity
    UnitPrice
    Discount
    ServicePeriodStart
    ServicePeriodEnd
    ChargeType
    ContractLineRef
    CommercialCalculationRef
  }
  PaymentTermsRef?
  AutoCollectionIntent?
}
```

---

# 122. Invoicing Owns Document Creation

Invoicing validates document requirements, tax context, numbering, finalization and posting. Subscription stores returned `InvoiceRef`; it never inserts invoice rows.

---

# 123. Invoice Finalization Feedback

Use contract/event to reflect whether InvoiceRequest became Draft/Finalized/Rejected. Subscription period state changes only from trustworthy Invoicing outcomes and remains idempotent.

---

# 124. No Invoice Mutation

Once invoice is finalized/issued, amendment cannot edit it. Correction is a new legal adjustment document through Invoicing.

---

# 125. Credit Adjustment

Commercial engine may calculate an expected negative adjustment, but Invoicing determines whether that becomes credit note, negative future line, or other legal mechanism permitted by configured country/provider policy.

---

# 126. Debit Adjustment

Additional amount for a previously billed period must reference original business context and become a legally valid document path; never patch invoice totals.

---

# 127. Egypt E-Invoice Boundary

Egypt ETA eInvoicing treats Credit Notes and Debit Notes as documents that adjust previously issued invoices and reference prior documents. TODIR therefore routes post-issuance subscription corrections through Invoicing/Compliance; Subscription stores commercial reason and lineage only.

---

# 128. Saudi E-Invoice Boundary

ZATCA e-invoicing requires credit/debit notes to correct issued invoices and link to existing invoices under applicable business rules. Subscription amendments therefore cannot bypass the invoice/note compliance pipeline.

---

# 129. Regulatory Status

Subscription does not own regulatory submission state. It may project high-level invoice compliance status for operators but canonical authority evidence stays in Compliance/Invoicing.

---

# 130. Payment Collection Boundary

Subscription says **when/under what policy collection should be attempted**. Payments owns token references, gateway calls, attempt lifecycle, provider responses, settlement and refunds.

---

# 131. Auto-Collection Policy

```text
AutoCollectionPolicy {
  Enabled
  PaymentMethodRef?
  CollectionTiming
  GracePolicyRef?
  OnFailureAction
}
```

Do not store PAN/CVV or provider secrets.

---

# 132. Payment Method Reference

Store opaque `PaymentMethodRef`/`MandateRef` owned by Payments. Provider-specific token values remain inside payment adapter/vault boundary.

---

# 133. Automatic Payment Timing

Possible modes:

```text
OnInvoiceFinalized
OnDueDate
NdaysBeforeDue
ManualOnly
```

Actual attempt timing can be constrained by gateway/mandate rules.

---

# 134. Regional Payment Provider Seam

Adapters must support regional tokenized recurring-payment providers without embedding provider semantics in Subscription. Examples validated in market research include Paymob subscription capabilities and Amazon Payment Services token/agreement based recurring payments, including regional payment options.

---

# 135. Payment Failure

Payment failure does not mutate contract directly through provider webhook. Payments records the failed attempt and emits an approved event; Subscription evaluates commercial consequence policy.

---

# 136. Dunning Boundary

Payments handles retries; Collections may handle human follow-up/escalation; Subscription handles service/contract consequence such as grace, suspension or non-renewal. No module owns all three concerns.

---

# 137. Grace Period

A grace policy can keep service active for N days after failed/due payment. Grace start/end, source open item and action are auditable. Grace is not equivalent to extending due date in Subledger.

---

# 138. Delinquency

Delinquency is a projected commercial condition based on canonical receivable/payment signals. Subscription must not compute outstanding balance by summing invoice/payment tables itself.

---

# 139. Suspend For Nonpayment

If configured, a deterministic policy can request contract suspension after grace. Manual override requires permission/reason and does not mark invoice paid.

---

# 140. Cancel For Nonpayment

Automatic cancellation for nonpayment must be opt-in, legally/commercially appropriate and preceded by configured notice/grace. Default for B2B enterprise contracts should favor review/suspension over silent cancellation.

---

# 141. Refund Boundary

Subscription amendment/cancellation may request a refund review, but Payments owns actual refund. A contract can be cancelled while a refund remains pending.

---

# 142. Contract Benefit

To expose what recurring commercial coverage the customer bought without duplicating Service/Helpdesk truth:

```text
ContractBenefit {
  ContractLineRef
  BenefitCode
  ScopeRef?
  QuantityLimit?
  ValidFrom
  ValidTo
  PolicySnapshot
}
```

---

# 143. Why Not EntitlementGrant

Avoid the name `EntitlementGrant` here because platform/control-plane SaaS entitlements already use that concept. Recommended business names are `ContractBenefit` or `CoverageGrant`.

---

# 144. Helpdesk Coverage Consumer

Helpdesk may query whether customer has Premium Support, response tier or included support policy. It never writes Subscription state.

---

# 145. Service Coverage Consumer

Service may query active maintenance coverage, included visits or service level. Actual consumption/visits remain Service-owned; if benefit consumption is required later, define a dedicated idempotent usage/consumption contract.

---

# 146. Coverage Snapshot

Downstream case/work-order may snapshot the evaluated coverage decision so historical service treatment remains explainable if contract changes later.

---

# 147. Recurring Fulfillment — Optional

Physical recurring supply can be supported through an optional provider:

```text
IRecurringFulfillmentProvider
```

Subscription requests a commercial fulfillment occurrence; Sales/Inventory owns order/delivery/stock truth.

---

# 148. No Inventory Hard Dependency

Many subscriptions are software/services. Inventory must remain optional so enabling Subscriptions does not force stock schemas/workflows for every tenant.

---

# 149. Usage Billing — Architecture Status

**DESIGN NOW / BUILD LATER** for generalized rating. Contracts and invoice lines must already carry enough identifiers/service periods so adding usage later does not rewrite historical recurring billing.

---

# 150. Usage Meter

Future concept:

```text
UsageMeter { Id, Code, Unit, Aggregation, SourcePolicy, Active }
```

Do not build arbitrary meter ingestion until a validated customer/sector requires it.

---

# 151. Usage Record

Future concept:

```text
UsageRecord { MeterRef, ContractLineRef, ExternalUsageId, Quantity, OccurredAt, SourceRef }
```

`ExternalUsageId` is idempotency-critical.

---

# 152. Usage Aggregation

Potential strategies: SUM, MAX, LAST, COUNT, unique count. Each meter declares one deterministic aggregation/version; never let UI formulas become hidden billing code.

---

# 153. Usage Ingestion Idempotency

The same metered event delivered repeatedly must be stored/recognized once per source identity. Duplicate usage is a direct customer-billing risk.

---

# 154. Usage Corrections

Usage corrections must be append/reversal or versioned adjustment with source evidence; do not silently edit already rated usage.

---

# 155. Pay-As-You-Go

Future in-arrears usage model: meter consumption during period, close usage, rate deterministically, request invoice. Billing must define cut-off/late-arriving usage policy.

---

# 156. Fixed Fee + Overage

Future model: recurring base fee includes allowance; usage above allowance bills at overage rate. This maps to common SaaS/telecom/cloud patterns while remaining optional.

---

# 157. Commit Plus Overage

Enterprise future model: customer commits minimum amount/quantity and pays additional usage above commitment. This requires explicit commitment-credit allocation and should not be smuggled into simple quantity pricing.

---

# 158. Prepaid Credit Burndown

Future capability for AI/API/cloud-style consumption. It needs dedicated monetary/non-monetary credit ledger semantics and expiry; do not implement as a mutable balance column on Subscription.

---

# 159. Usage Cutoff

Future usage billing must define late event windows, close/finalization, re-rating and adjustment policy. No usage period may be silently re-opened after legal invoice issuance.

---

# 160. Revenue Recognition Boundary

Subscriptions supplies contractual/service-period facts. Accounting owns recognition policy/schedules or delegates to a future Revenue Recognition capability. Subscription never posts deferred revenue journals.

---

# 161. Service Period Evidence For Recognition

Invoice lines generated from subscriptions must carry:

```text
ContractRef
ContractLineRef
ServicePeriodStart
ServicePeriodEnd
ChargeType
```

so accounting can later support deferral/recognition without reconstructing contracts.

---

# 162. Annual Prepayment Example

Customer pays/invoices `12,000` annually for 12 months. Billing amount may be `12,000`, normalized MRR may be `1,000`, and accounting recognized revenue may also be `1,000/month` depending on policy. These are distinct truths.

---

# 163. MRR Definition

MRR is a normalized commercial recurring metric, not invoice revenue, cash received or accounting revenue. Define calculation version and exclusions explicitly.

---

# 164. ARR Definition

ARR is normalized annualized recurring value, commonly `MRR × 12` for compatible cadences. Annualization policy must be explicit for weekly/custom periods.

---

# 165. MRR Components

Track at least:

```text
New MRR
Expansion MRR
Contraction MRR
Churn MRR
Reactivation MRR
```

Facts are derived from contract revisions/terms, not from payment success alone.

---

# 166. MRR Exclusions

Exclude one-time setup fees, tax, refunds, pure pass-through charges and non-recurring lines by default. Usage MRR treatment must be separate and documented.

---

# 167. Churn

Customer/contract churn must distinguish voluntary cancellation, nonpayment, expiration/non-renewal and migration. Avoid one opaque churn reason.

---

# 168. Renewal Rate

Measure eligible expiring value/contracts versus renewed value/contracts with explicit cohort and date basis. Do not divide arbitrary totals across mismatched periods.

---

# 169. Contracted Revenue

Future contracted-revenue reporting can project expected charges from effective contract terms. It is a forecast-like commercial projection and must not be labeled accounting revenue.

---

# 170. Multi-Currency Contracts

Each contract is reported in original currency and optionally normalized to a reporting currency through Reporting/FinBase FX policy. Never overwrite original contractual amount with converted values.

---

# 171. MRR FX Policy

For cross-currency dashboards, choose and disclose FX basis (e.g., period-end, period-average, fixed planning FX). Same dashboard run should be reproducible.

---

# 172. Customer Portal — Boundary

Portal is a presentation/access capability. It calls Subscriptions application commands under external-user authorization; it does not own contract truth.

---

# 173. Portal Core Actions

Configurable capabilities:

```text
ViewContract
ViewUpcomingCharge
ViewInvoices via Invoicing
UpdatePaymentMethod via Payments
RequestCancel
CancelAtTermEnd
RequestPause
Resume
Upgrade/Downgrade
ChangeQuantity
```

Each action can be disabled per tenant/plan/contract.

---

# 174. Portal Self-Service Policy

Self-service must respect notice periods, minimum terms, effective-date options and pricing/approval constraints. UI convenience never bypasses domain rules.

---

# 175. Portal Financial Preview

Before a customer confirms change, show effective date, immediate charge/credit estimate and next recurring amount, localized and rounded consistently with server calculation.

---

# 176. Portal Cancellation Retention Flow

Optional product UX may present pause/downgrade offers, but refusal must not trap the customer where cancellation is contractually allowed. Keep audit of chosen outcome.

---

# 177. Sales Origin

Sales may create a subscription draft/activation request from a confirmed commercial order/quotation. Subscription then becomes owner of recurring contract lifecycle; future renewals/amendments do not mutate old Sales documents.

---

# 178. CRM Context

CRM may display renewal date, recurring value and churn risk projection. CRM does not own contract amount or renewal state.

---

# 179. Customer Success Boundary

Health scores, adoption playbooks and account success plans are not part of this module. They may consume Subscription facts later.

---

# 180. Contract Template

Optional setup entity for default schedule/term/renewal/collection policies. Template changes affect new contracts unless explicit migration/amendment is performed; they never rewrite active contracts.

---

# 181. Simple Subscription Plan UX

A simple Plan can package Product + default price + billing frequency + term + common policies. Internally the activated contract still expands to canonical lines/policy snapshots.

---

# 182. Plan != Contract

A plan is reusable setup. A contract is a customer-specific legal/commercial instance with snapshots and history.

---

# 183. Plan Versioning

Material plan changes should create a new effective version. Existing contracts keep their agreed snapshot unless renewed/amended.

---

# 184. Custom Contract Without Plan

Enterprise negotiated contracts may be created directly from lines/policies without forcing a named plan. Same domain invariants apply.

---

# 185. Contract Notes & Attachments

Files capability stores signed contract/supporting documents. Subscription stores authorized FileRefs. Access follows contract/customer/legal-entity scope; file ID alone never grants access.

---

# 186. External Contract Number

Migration/integration may store external agreement/PO/contract identifiers with uniqueness scope as configured. Internal TODIR number remains stable.

---

# 187. Customer Purchase Order Reference

B2B recurring invoices may need customer PO/reference. Store on contract or term with effective dating; InvoiceRequest snapshots the applicable reference.

---

# 188. Minimum Term

Optional minimum commitment term can restrict cancellation/downgrade. Enforcement returns explainable rule violation and authorized override path; never silently reject with generic error.

---

# 189. Notice Period

Cancellation/renewal notice can be expressed in business/calendar days according to policy. Deadline must be calculated from contract calendar and displayed clearly.

---

# 190. Early Termination Charge

DESIGN LATER unless market validation requires. If built, it must be an explicit charge formula and invoice request, never hidden inside cancellation status.

---

# 191. Contract Deposit

Deposits/advances are financial facts owned by Payments/Subledger. Subscription may require a deposit condition at activation but never stores deposit balance as canonical truth.

---

# 192. Free Period

Promotional free periods should be represented explicitly through trial/free-phase or discount policy, preserving commercial explanation. Avoid fake zero prices that destroy list/contract pricing lineage.

---

# 193. Coupon/Promotion Boundary

If future Promotions/Loyalty exists, Subscription snapshots approved discount outcome. It does not become promotion eligibility engine.

---

# 194. Tax-Inclusive Pricing

Contract may capture whether commercial price is tax-inclusive/exclusive according to Pricing/Tax/Invoicing contracts. Proration works on defined commercial basis; tax is then computed by Tax.

---

# 195. Currency Minor Units & Rounding

All calculations use FinBase Money rules. Define rounding per line/document/proration step and persist calculation version to prevent a one-cent drift across retries.

---

# 196. Rounding Residual

When distributing annual/period amount across partial periods, deterministic residual handling must ensure total equals contracted amount where policy expects equality. Residual cannot vanish.

---

# 197. Negative Invoice Request

If a commercial adjustment net is negative, do not assume Invoicing supports a negative invoice. Route an adjustment intent so Invoicing chooses credit-note/legal representation.

---

# 198. Zero Amount Period

A zero recurring amount may still represent valid free/trial/fully discounted service. Policy determines whether to create zero invoice, skip document but record period, or require regulatory document. Keep billing-period history either way.

---

# 199. Previous Invoice Unpaid Policy

Some businesses continue billing despite unpaid prior invoice; others suspend/stop. Policy must be explicit and based on Subledger/Payments signals, not a hidden scheduler behavior.

---

# 200. Bill Even If Prior Unpaid

When enabled, new periods continue to produce invoice requests while outstanding balance can grow. UI must warn operators of exposure; credit control remains Subledger/Collections policy.

---

# 201. Stop Billing On Suspension

Suspension policy determines whether billing pauses. Service suspension due to nonpayment does not inherently erase contractual charges; this is a commercial policy decision.

---

# 202. Backdated Activation

Allowed only with controlled migration/authorized business flow. Engine must preview catch-up billing periods and prevent accidental mass invoice generation.

---

# 203. Future-Dated Activation

Contract can be prepared/approved now with future start. No active benefit/billing before start unless an explicit one-time pre-start charge exists.

---

# 204. Migration — Existing Active Contracts

Import should support original start, current term start/end, next billing date, current price/quantity, renewal policy, paid-through/billed-through evidence and external references without recreating historical invoices.

---

# 205. Migration — Billed Through

Store a cutover marker such as `BilledThroughDate`/last external billing period so TODIR starts from the next legitimate period and does not duplicate legacy charges.

---

# 206. Migration — External Invoice References

Legacy invoice identifiers may be stored as external evidence links. Do not invent TODIR invoice rows for documents that were never issued inside TODIR unless the migration strategy explicitly imports them through Invoicing.

---

# 207. Migration Reconciliation

Go-live report compares imported contract value, next billing dates, active line quantities, currency, renewal dates and expected first TODIR billing against legacy system totals/sample contracts.

---

# 208. Go-Live Contract

Stateful module Go-Live requirements include dependencies active, numbering ready, billing calendar/timezone configured, default policies reviewed, migration reconciled, payment adapters validated if enabled, and dry-run billing preview approved.

---

# 209. Deactivation Contract

Module deactivation uses WindDown/ReadOnly semantics. Active contracts, scheduled billing, open amendments and downstream dependencies can block full disablement. Historical contracts remain readable according to retention policy.

---

# 210. Wind-Down

WindDown can block new activations/amendments while allowing already-due billing, cancellation and financial completion. Exact policy is tenant/admin controlled with safety checks.

---

# 211. Data Retention

Contracts, revisions, billing periods and financial linkage may be legally/business-critical. Deletion is not the default lifecycle. Apply country/tenant retention and privacy rules to personal data without destroying accounting/audit evidence.

---

# 212. Personal Data Minimization

Subscription references Party/Contact instead of duplicating personal data. Store only snapshots legally/operationally required for contract evidence.

---

# 213. Arabic-First UX

All core screens support RTL, Arabic labels, Arabic/English mixed product names, local currency formatting, Hijri display where later enabled, while canonical business dates/calculations remain unambiguous.

---

# 214. Bilingual Contract Display

Templates may render Arabic/English customer-facing contract summaries. Translation never creates different commercial truth; both render from same canonical terms.

---

# 215. Regional Weekend & Holidays

Business calendars may vary by legal entity/country. Invoice lead/notice/business-day adjustments must use configured calendar, not hard-coded Friday/Saturday assumptions.

---

# 216. Accessibility

Subscription workspace, previews, dialogs and portal actions must meet keyboard, semantic labeling, focus and contrast requirements. Financial-impact confirmation must not rely on color alone.

---

# 217. Mobile Operations

Mobile/responsive UX may support contract lookup, renewal approval, payment-status projection and simple pause/cancel requests. Complex amendment matrix can remain desktop-optimized.

---

# 218. Workspace — Small Company

Primary fields:

```text
Customer
Product/Service
Price
Billing frequency
Start date
Auto renew
```

Advanced settings stay collapsed with sensible defaults.

---

# 219. Workspace — Enterprise

Tabs/panels may expose Terms, Lines, Billing, Amendments, Renewal, Collection, Benefits, Invoices, Timeline, Audit, Analytics and Integrations without forcing them into simple create flow.

---

# 220. Contract Timeline

Human timeline includes activation, amendments, pause/resume, renewal decisions, billing periods, invoice links, payment consequence events and cancellation. It is a projection, not the raw audit log.

---

# 221. Explain Why

Every non-obvious calculated date/amount should support explanation: why next bill is this date, why proration is this amount, why renewal price changed, why billing is blocked, why suspension occurred.

---

# 222. Billing Preview

Before activation/amendment, return a server-calculated preview with upcoming periods/charges. Frontend must not independently reimplement billing math.

---

# 223. Next Invoice Projection

Projection is non-authoritative until InvoiceRequest accepted/finalized. Display as `Expected`/`Scheduled`, not issued invoice.

---

# 224. Upcoming Renewal View

Operations view groups contracts by renewal window, value, risk, owner and required action. It does not replace CRM opportunity pipeline unless explicitly integrated.

---

# 225. Exceptions Queue

Exception-based ERP view surfaces failed billing, invalid price, missing customer tax data, renewal requiring review, payment-failure consequence, stuck invoice request, duplicate external event and migration anomalies.

---

# 226. No Silent Skip

If a due period cannot be billed, it becomes a visible exception with reason and retry/remediation path. Scheduler must not simply advance `next_invoice_date`.

---

# 227. No Silent Catch-Up

If jobs were down for days, resuming can identify missed periods but bulk catch-up billing may require policy/preview, especially after migration or long outages.

---

# 228. Data Ownership — Core Tables

Core tables may include:

```text
subscription_contracts
subscription_contract_revisions
subscription_lines
subscription_line_revisions
subscription_terms
billing_schedules
billing_periods
contract_amendments
renewal_policies
collection_policy_refs
pause_periods
contract_benefits
billing_calculations
subscription_event_links
```

---

# 229. No Cross-Module Foreign Keys

Follow TODIR rule: no cross-module FK except approved Core/Foundation boundaries. Business-module references to Invoice, Payment, Sales origin, Service, Helpdesk etc. use opaque typed refs/contracts.

---

# 230. Tenant Isolation

Every table carries tenant/security scope as required by TODIR platform conventions. RLS/tenant boundary tests must prove one tenant cannot query another tenant’s contracts or billing previews.

---

# 231. Legal Entity Isolation

Authorization additionally enforces legal-entity scope. Shared group operators require explicit multi-entity permission; a contract reference is not authorization.

---

# 232. Indexes — Due Billing

Indexes must support `TenantId + LegalEntityId + BillingDate + State` and worker claim patterns. Avoid full scans of millions of historical periods.

---

# 233. Indexes — Customer Contracts

Support fast lookup by PartyRef, state, renewal date, contract number and external reference. Index only validated query patterns to control write overhead.

---

# 234. Indexes — Renewals

Use state/term-end/renewal-action date indexes for upcoming-renewal jobs and dashboards.

---

# 235. Indexes — Amendments

Index scheduled effective date/state so future changes apply without scanning historical amendments.

---

# 236. Optimistic Concurrency

Contract aggregate uses version/concurrency token. Two users cannot both modify quantity/price from the same stale revision with last-write-wins.

---

# 237. Amendment Concurrency

Creating a second scheduled amendment that conflicts with an existing future revision must fail or explicitly supersede with audit; never silently overwrite.

---

# 238. Billing Worker Claim

Use transactional claim/lease pattern such as `FOR UPDATE SKIP LOCKED` or equivalent application mechanism so horizontally scaled workers cannot bill the same period concurrently.

---

# 239. Billing Transaction Boundary

Within Subscriptions DB, claiming period + persisting calculation/request-outbox record should be atomic. External Invoicing call uses reliable contract/outbox semantics, not distributed DB transaction.

---

# 240. Outbox

Publish integration events and cross-module commands through approved outbox only after local commit. Never emit `SubscriptionBilled` before durable local state exists.

---

# 241. Inbox

Consume Invoicing/Payments/Directory lifecycle events idempotently with message ID/version checks. Duplicate delivery must be harmless.

---

# 242. Event Ordering

Do not assume network delivery order. Handlers validate aggregate/event version and can defer/retry when prerequisite event has not arrived.

---

# 243. Idempotent Commands

Activation, apply amendment, generate invoice request, renew, pause/resume and cancellation commands accept idempotency semantics where external/retry invocation is possible.

---

# 244. Event — SubscriptionActivated

```text
SubscriptionActivated@1 { ContractRef, LegalEntityRef, CustomerPartyRef, Term, RevisionNo, OccurredAt }
```

Contains no unnecessary personal/payment data.

---

# 245. Event — SubscriptionAmended

Include contract, amendment type, effective date, before/after revision numbers and commercial impact category. Downstream consumers fetch authorized details if needed.

---

# 246. Event — SubscriptionPaused

Emits effective pause date and benefit/billing impact summary. Payment state is not embedded as truth.

---

# 247. Event — SubscriptionResumed

Includes effective resume date and new revision/next billing date projection.

---

# 248. Event — SubscriptionRenewed

Includes old/new term refs, renewal policy result and recurring-value delta for projections/analytics.

---

# 249. Event — SubscriptionNonRenewing

Signals that current service continues until term end but no automatic next term is expected.

---

# 250. Event — SubscriptionCancelled

Includes effective cancellation and reason category; it must not claim invoices/refunds are resolved.

---

# 251. Event — BillingPeriodReady

Internal/application event indicating a period is due for invoice request. External consumers normally should not depend on it.

---

# 252. Event — SubscriptionBillingRequested

Carries references/idempotency only; legal invoice outcome comes from Invoicing.

---

# 253. Event — SubscriptionBillingFailed

Operational exception event with stable reason code, retryability and references. Do not include stack traces/customer secrets in public event payload.

---

# 254. Query Contract — Get Active Coverage

Read contract for downstream Helpdesk/Service:

```text
GetActiveContractBenefits(CustomerPartyRef, ContextRef, AsOf)
```

Returns authorized, bounded snapshot only.

---

# 255. Query Contract — Upcoming Charge

Returns projected next charge/service period and calculation explanation. Mark as estimate until invoice finalization.

---

# 256. Command Contract — Create From Sales

Sales may call a stable command using product/price/customer/term snapshots after its own confirmation. Subscription validates independently and returns ContractRef.

---

# 257. Command Contract — Request Invoice

Subscriptions calls Invoicing using stable versioned contract with idempotency key. Invoicing never calls Subscription tables directly.

---

# 258. Payment Event Consumption

Consume approved payment/open-item consequence events such as collection failed/succeeded only to evaluate commercial policy. Never derive ledger balances from events.

---

# 259. Directory Deactivation

If customer Party becomes inactive, active contracts are not silently cancelled. Surface exception/governance action according to Directory/business policy.

---

# 260. Catalog Deactivation

Deactivating Product blocks new lines/contracts by default but does not invalidate historical active contract automatically. Renewal may require replacement/review.

---

# 261. Pricing Master Change

May affect new contract/renewal preview only according to renewal policy. It never changes current contracted price snapshot by event propagation.

---

# 262. Caching

Cache templates/reference projections if useful; never cache mutable billing decision without version key. Billing engine must resolve authoritative contract revision and policy snapshots.

---

# 263. Read Models

Create projections optimized for customer contract list, renewal queue, billing exceptions, MRR dashboard and portal. Projections are rebuildable and never source truth.

---

# 264. Search

Search by contract number, customer, product, external reference and active states. Search index/document must honor tenant/legal-entity/field authorization and not leak confidential price data.

---

# 265. Reporting Boundary

Subscriptions exposes owner read models/facts. Cross-module finance/customer analytics belong to Reporting projections; PostgreSQL first, read replicas/DWH only when measured need exists.

---

# 266. Core Reports

Initial reports:

```text
Active Contracts
Upcoming Billing
Billing Exceptions
Upcoming Renewals
Cancelled / Expired
Recurring Revenue Bridge
MRR / ARR
Expansion / Contraction / Churn
Contract Value by Customer/Product
```

---

# 267. Invoice Reconciliation Report

Compare billable periods/requests to actual InvoiceRefs/status. Detect due period with no invoice, duplicate invoice link, rejected request or invoice without expected contract reference.

---

# 268. Payment Reconciliation View

Display Subledger/Payments projected status beside subscription without copying balance truth. Separate `contract active` from `invoice unpaid` clearly.

---

# 269. Renewal Pipeline Report

Show value expiring in 30/60/90 days, automatic vs manual, required approval, missing renewal price, customer owner and scheduled changes.

---

# 270. Churn Report

Segment by reason, product, customer cohort, country/legal entity and recurring value. Preserve voluntary/involuntary distinction when data supports it.

---

# 271. Cohort Retention

DESIGN LATER/Reporting: analyze recurring revenue/customer retention cohorts without turning Subscription OLTP tables into analytics warehouse.

---

# 272. Performance Target — Contract Read

Common contract list/detail queries should remain interactive under tenant scale using indexed projections; avoid joining live Invoicing/Payments tables synchronously for every row.

---

# 273. Performance Target — Billing Batch

Billing worker processes due periods in bounded batches with backpressure, retry and observable throughput. Horizontal scale must preserve idempotency/claim guarantees.

---

# 274. Performance Target — Preview

Simple contract/proration preview should be fast enough for interactive UX. Heavy usage rating is not allowed to degrade simple recurring billing path.

---

# 275. Scale — Millions Of Periods

Historical BillingPeriod growth is expected. Partitioning is not mandatory day one; index/retention/query design must make future partitioning possible without domain rewrite.

---

# 276. Scale — Job Sharding

Future workers may shard by tenant/legal entity/date range. No sharding key may break single-contract sequencing.

---

# 277. Scale — Hot Tenant

Large tenant must not starve smaller tenants. Scheduler can use fair batching/rate limits per tenant when measured.

---

# 278. Observability — Metrics

Track due periods, billing success/failure, invoice request latency, renewal job success, payment-consequence transitions, amendment throughput, duplicate suppression and queue age.

---

# 279. Observability — Tracing

Correlation chain:

```text
Contract
→ BillingPeriod
→ Calculation
→ InvoiceRequest
→ Invoice
→ PaymentAttempt/OpenItem projection
```

Use correlation IDs across module boundaries.

---

# 280. Observability — Structured Logs

Logs include tenant/ref IDs, reason codes and job attempt, not card tokens, personal secrets or full provider payloads.

---

# 281. Alerts

Alert on billing failure spikes, stuck due periods, renewal backlog, repeated invoice-request rejections, payment adapter outage projection and scheduler lag beyond SLA.

---

# 282. SLO — No Duplicate Billing

Duplicate invoice request for same logical period/adjustment is a severity-1 billing integrity defect even if customer has not yet paid.

---

# 283. SLO — No Missed Billing

Due period not requested within configured operational window raises an exception/alert. Recovery is controlled and reconciled.

---

# 284. Security — Permission Vocabulary

Initial permissions may include:

```text
subscriptions.contract.read
subscriptions.contract.create
subscriptions.contract.activate
subscriptions.contract.amend
subscriptions.contract.price_override
subscriptions.contract.pause
subscriptions.contract.resume
subscriptions.contract.cancel
subscriptions.contract.renew
subscriptions.billing.run
subscriptions.billing.retry
subscriptions.billing.preview
subscriptions.analytics.read
subscriptions.migration.execute
subscriptions.admin.policy
```

---

# 285. Security — Financial Preview

Users who can see contract existence may not automatically see negotiated price, discount or customer receivable status. Field/action authorization applies.

---

# 286. Security — Separation Of Duties

Enterprise policy may prevent same user from proposing and approving material price override/cancellation/retroactive adjustment. Optional Approvals provides workflow; module enforces final authorization.

---

# 287. Security — Portal

External user authorization is relationship/scope based. Knowing ContractId or PartyId never grants access. B2B account admins require explicit account scope.

---

# 288. Security — Payment Data

No PAN/CVV/provider secrets in Subscription schema/log/event. Store only opaque payment references and safe display metadata if provided by Payments.

---

# 289. Security — Attachments

File access re-checks contract/customer authorization. Signed contracts may be confidential and require stricter download/audit policy.

---

# 290. Security — Exports

Exports can contain customer/pricing data. Enforce permission, legal entity scope, row limits/background jobs and audit of sensitive exports.

---

# 291. Audit — Mandatory Actions

Audit activation, amendment proposal/approval/application, price override, schedule change, pause/resume, cancellation, renewal, billing retry/skip, migration and admin policy changes.

---

# 292. Audit != Timeline

Customer-friendly timeline may summarize events. Security audit retains actor/action/before-after/reason/correlation evidence independently.

---

# 293. Reason Codes

High-impact actions require structured reason + optional note. Do not rely only on free text for analytics/compliance.

---

# 294. AI — Allowed Assistance

AI may summarize contract history, explain upcoming invoice, draft renewal communication, flag unusual changes, suggest churn risk or help operator find policy — all through authorized tools/read models.

---

# 295. AI — Forbidden Actions

AI cannot autonomously finalize contract price, issue legal invoice, change payment method, cancel high-value contract, approve discount, post accounting or bypass approval/permission.

---

# 296. AI — Billing Explanation

AI can turn deterministic calculation evidence into plain Arabic/English explanation. It cannot invent arithmetic; numeric source comes from billing engine.

---

# 297. AI — Renewal Suggestions

AI may suggest renewal uplift/offer using history but result is advisory. Commercial policy/authorized user determines actual renewal terms.

---

# 298. AI — Data Minimization

Tools expose only contract/customer fields required for task. Payment tokens, internal secrets and unrelated customers are never passed to model.

---

# 299. Market Evidence — Odoo 19

Odoo Subscriptions supports recurring plans, automatic invoicing, renewals/upselling, automatic payment via tokenization-capable providers, portal/eCommerce integration and recurring revenue reporting. TODIR should match simplicity but preserve clearer contract/amendment ownership instead of treating the recurring sales order as the universal domain.

---

# 300. Market Evidence — ERPNext

ERPNext supports Subscription Plans and configurable prepaid/postpaid/bill-before-period behavior, calendar-month alignment and handling when previous invoices remain unpaid. TODIR adopts these real SME needs as first-class schedule policies.

---

# 301. Market Evidence — Chargebee

Chargebee demonstrates that mid-term upgrade/downgrade/quantity changes require explicit proration policy and credits/charges. TODIR adopts deterministic day-based proration and effective-dated amendments while avoiding millisecond complexity in V1.

---

# 302. Market Evidence — Stripe Billing

Stripe demonstrates modern flat-rate, per-seat, tiered, usage-based, fixed-fee-plus-overage and credit-burndown models. TODIR designs extension seams for these models but keeps V1 focused on fixed/per-unit recurring plus one-time charges.

---

# 303. Market Evidence — NetSuite SuiteBilling

NetSuite shows enterprise demand for usage and commitment/overage models. TODIR treats these as future rating capabilities rather than inflating simple SME billing from day one.

---

# 304. Market Evidence — Microsoft Dynamics 365

Dynamics 365 Subscription Billing separates recurring contract billing from revenue/expense deferrals and supports line-level billing/recognition concepts. TODIR likewise keeps contract billing separate from accounting recognition while carrying service-period evidence.

---

# 305. Market Evidence — SAP Subscription Billing

SAP supports imported metered usage and rating. TODIR therefore reserves UsageMeter/UsageRecord/rating contracts without prematurely building a full meter/rating platform.

---

# 306. Market Evidence — Zoho Billing

Zoho shows practical pause/resume and dunning workflows. TODIR separates pause/service consequences from payment retry/dunning ownership so the domains do not collapse into one billing monolith.

---

# 307. Regional Evidence — Paymob

Paymob documentation exposes subscription-plan/recurring-payment capabilities for regional merchants. TODIR integrates through Payments adapters and stores only opaque payment references.

---

# 308. Regional Evidence — Amazon Payment Services

Amazon Payment Services recurring payments use stored token/agreement references and support regional payment options such as MADA. TODIR keeps provider tokens/agreements inside Payments boundary while Subscription stores a PaymentMethod/Mandate reference.

---

# 309. Regional Evidence — Egypt ETA

ETA eInvoicing defines Credit Notes and Debit Notes as corrections to previously issued invoices. Subscription amendments after issuance must request legal adjustment documents rather than mutate invoice history.

---

# 310. Regional Evidence — Saudi ZATCA

ZATCA e-invoicing standards treat credit/debit notes as corrections linked to issued invoices. Subscription cancellation/proration logic therefore must respect Compliance/Invoicing correction paths.

---

# 311. API — Create Draft Contract

`POST /subscriptions/contracts` creates Draft using customer, currency, lines and schedule; server validates canonical refs and returns ETag/version.

---

# 312. API — Activate Contract

`POST /subscriptions/contracts/{id}/activate` requires expected version/idempotency key and returns first term, next billing projection and warnings.

---

# 313. API — Preview Activation

`POST /subscriptions/contracts/{id}/preview-activation` performs deterministic validation/calculation without mutation.

---

# 314. API — Preview Amendment

`POST /subscriptions/contracts/{id}/amendments/preview` returns before/after recurring value, proration, effective date and expected document adjustments.

---

# 315. API — Create Amendment

`POST /subscriptions/contracts/{id}/amendments` creates auditable amendment proposal; caller cannot directly patch active revision fields.

---

# 316. API — Apply Amendment

`POST /subscriptions/amendments/{id}/apply` enforces approval/effective-date/concurrency and creates revision/adjustment intents atomically.

---

# 317. API — Pause

`POST /subscriptions/contracts/{id}/pause` requires reason/effective date/policy selection and returns billing/term impact.

---

# 318. API — Resume

`POST /subscriptions/contracts/{id}/resume` previews/commits new next billing date and any proration.

---

# 319. API — Cancel

`POST /subscriptions/contracts/{id}/cancel` supports immediate or term-end behavior with reason and impact preview.

---

# 320. API — Renew

`POST /subscriptions/contracts/{id}/renew` creates/activates next term under selected renewal policy; manual renewal can remain proposed.

---

# 321. API — Mark Non-Renewing

`POST /subscriptions/contracts/{id}/non-renew` records no-next-term decision without ending current service early.

---

# 322. API — Get Contract

`GET /subscriptions/contracts/{id}` returns authorized aggregate view plus ETag.

---

# 323. API — List Contracts

`GET /subscriptions/contracts` uses cursor/page filters for customer/state/product/renewal/billing exception; no unbounded list.

---

# 324. API — Upcoming Billing

`GET /subscriptions/contracts/{id}/upcoming-billing` returns projected periods/amounts with calculation IDs.

---

# 325. API — Billing Timeline

`GET /subscriptions/contracts/{id}/billing-periods` returns paginated billing history and InvoiceRefs.

---

# 326. API — Trigger Billing Retry

Authorized operations endpoint retries one failed/requestable period using same idempotency key unless a new billing revision is intentionally created.

---

# 327. API — Contract Benefits

Authorized downstream query returns active benefits as-of a date and context.

---

# 328. API — Renewal Queue

Returns paginated upcoming renewals with action state/value, backed by projection.

---

# 329. API — Exceptions Queue

Returns billing/renewal/config exceptions with stable reason codes and remediation links.

---

# 330. API — Migration Import

Uses platform import framework: map -> validate -> preview -> dry run -> import -> reconcile -> audit; never direct table bulk insert.

---

# 331. Error — CUSTOMER_NOT_ELIGIBLE

Customer Party is missing/inactive/invalid for requested activation under policy.

---

# 332. Error — PRODUCT_NOT_ELIGIBLE

Product/service cannot be used for new subscription line.

---

# 333. Error — PRICE_UNRESOLVED

No deterministic commercial price could be snapshotted.

---

# 334. Error — CURRENCY_MISMATCH

Line/pricing source cannot satisfy contract currency policy.

---

# 335. Error — INVALID_BILLING_ANCHOR

Anchor/day/calendar configuration cannot produce deterministic periods.

---

# 336. Error — REVISION_CONFLICT

Expected aggregate version or effective revision changed since preview.

---

# 337. Error — AMENDMENT_OVERLAP

Scheduled change conflicts with another effective future revision.

---

# 338. Error — PERIOD_ALREADY_BILLED

Requested historical change would mutate an already billed period.

---

# 339. Error — PRORATION_NOT_ALLOWED

Selected line/policy cannot use requested proration method.

---

# 340. Error — RENEWAL_PRICE_REQUIRED

Renewal policy requires manual/current price that is unavailable.

---

# 341. Error — NOTICE_PERIOD_VIOLATION

Requested cancellation/nonrenewal violates configured notice rule without authorized override.

---

# 342. Error — MINIMUM_TERM_VIOLATION

Requested downgrade/cancel precedes minimum term.

---

# 343. Error — INVOICE_REQUEST_REJECTED

Invoicing rejected requested document; period enters exception state.

---

# 344. Error — PAYMENT_METHOD_REQUIRED

Activation/auto-collection policy requires authorized Payments method/mandate.

---

# 345. Error — MIGRATION_BILLED_THROUGH_INVALID

Cutover billing marker conflicts with next period/term.

---

# 346. Error — LEGAL_ENTITY_SCOPE_DENIED

Actor lacks target legal-entity permission.

---

# 347. Error — CONTRACT_READ_ONLY

Module/contract is in wind-down/read-only state for this action.

---

# 348. Error — DUPLICATE_EXTERNAL_REFERENCE

External contract/usage reference violates scoped uniqueness.

---

# 349. Error — BILLING_CALCULATION_MISMATCH

Recomputed deterministic inputs differ from persisted calculation unexpectedly; block and investigate.

---

# 350. Error — DEPENDENCY_UNAVAILABLE

Required canonical provider unavailable; no unsafe fallback.

---

# 351. Job — Extend Billing Horizon

Creates future Planned BillingPeriods within bounded horizon. Idempotent by contract/period identity.

---

# 352. Job — Due Billing Scheduler

Finds Ready due periods, claims in batches and creates InvoiceRequests reliably.

---

# 353. Job — Billing Retry

Retries retryable failures with exponential/backoff policy; does not change idempotency identity.

---

# 354. Job — Scheduled Amendment Applier

Applies authorized future amendments at effective business date with concurrency protection.

---

# 355. Job — Renewal Reminder

Creates notifications/tasks at configured internal/customer notice windows.

---

# 356. Job — Auto Renewal

Creates next term at deterministic boundary after validation; exceptions go to queue.

---

# 357. Job — Trial End

Evaluates conversion/cancel/extend policy and never silently starts paid service when prerequisites fail.

---

# 358. Job — Pause/Resume Effective

Applies future scheduled pause/resume at business date.

---

# 359. Job — Nonpayment Consequence

Evaluates approved delinquency/grace signals and applies commercial consequence idempotently.

---

# 360. Job — Projection Rebuild

Rebuilds MRR/renewal/billing list projections from canonical Subscription history.

---

# 361. Job — Reconciliation

Finds due/billed/requested inconsistencies against Invoicing refs/events and emits exception records.

---

# 362. Job — Migration Validation

Runs scheduled post-cutover comparison for first billing cycles.

---

# 363. Job — Usage Close — Future

Closes usage period under explicit cutoff when usage billing exists.

---

# 364. Job — Usage Rating — Future

Rates closed usage with versioned rate model and produces billing inputs.

---

# 365. Job — Archive/Retention

Applies authorized retention/anonymization/archive policy without breaking legal financial lineage.

---

# 366. SB00 — Domain & Architecture Baseline

Implement project/schema/AGENTS alignment, dependency correction, contracts, value objects, date/money policies and architecture tests before UI.

---

# 367. SB00 Exit Gate — Domain & Architecture Baseline

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Domain & Architecture Baseline**. No phase is complete because UI renders alone.

---

# 368. SB01 — Contract Core

Draft contract, lines, term, currency, Party/Product refs, numbering, versioning and CRUD for draft only.

---

# 369. SB01 Exit Gate — Contract Core

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Contract Core**. No phase is complete because UI renders alone.

---

# 370. SB02 — Pricing Snapshot

Integrate Pricing resolution, negotiated override permission, immutable price snapshot and preview.

---

# 371. SB02 Exit Gate — Pricing Snapshot

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Pricing Snapshot**. No phase is complete because UI renders alone.

---

# 372. SB03 — Billing Schedule Engine

Frequency, anchor, advance/arrears, service periods, month-end/leap-day/calendar behavior with golden tests.

---

# 373. SB03 Exit Gate — Billing Schedule Engine

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Billing Schedule Engine**. No phase is complete because UI renders alone.

---

# 374. SB04 — Activation & Billing Periods

Activation state machine, first term, bounded period horizon, next billing projection.

---

# 375. SB04 Exit Gate — Activation & Billing Periods

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Activation & Billing Periods**. No phase is complete because UI renders alone.

---

# 376. SB05 — InvoiceRequest Integration

Idempotent Invoicing contract, outbox/inbox, BillingPeriod result mapping and reconciliation.

---

# 377. SB05 Exit Gate — InvoiceRequest Integration

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **InvoiceRequest Integration**. No phase is complete because UI renders alone.

---

# 378. SB06 — Amendments

Effective-dated revisions, add/remove/quantity/price/discount changes, preview and conflict handling.

---

# 379. SB06 Exit Gate — Amendments

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Amendments**. No phase is complete because UI renders alone.

---

# 380. SB07 — Proration

Day-based deterministic credit/charge components, rounding evidence, legal adjustment intent.

---

# 381. SB07 Exit Gate — Proration

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Proration**. No phase is complete because UI renders alone.

---

# 382. SB08 — Renewal

Manual/auto/non-renewing, price policies, uplift, notice windows and renewal queue.

---

# 383. SB08 Exit Gate — Renewal

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Renewal**. No phase is complete because UI renders alone.

---

# 384. SB09 — Pause/Resume/Cancel

Lifecycle policies, future-effective actions, no-history-rewrite rules.

---

# 385. SB09 Exit Gate — Pause/Resume/Cancel

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Pause/Resume/Cancel**. No phase is complete because UI renders alone.

---

# 386. SB10 — Payments Orchestration

Opaque method/mandate refs, auto-collection intent, failure/grace/suspension signal handling.

---

# 387. SB10 Exit Gate — Payments Orchestration

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Payments Orchestration**. No phase is complete because UI renders alone.

---

# 388. SB11 — Benefits/Coverage

ContractBenefit model and safe downstream queries for Helpdesk/Service.

---

# 389. SB11 Exit Gate — Benefits/Coverage

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Benefits/Coverage**. No phase is complete because UI renders alone.

---

# 390. SB12 — Portal Seam

Self-service commands/preview/authorization without building full portal platform inside module.

---

# 391. SB12 Exit Gate — Portal Seam

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Portal Seam**. No phase is complete because UI renders alone.

---

# 392. SB13 — Reporting & MRR

Owner projections, MRR bridge, renewal/billing exception reports with documented formulas.

---

# 393. SB13 Exit Gate — Reporting & MRR

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Reporting & MRR**. No phase is complete because UI renders alone.

---

# 394. SB14 — Migration

Import mapping/dry-run/cutover billed-through/reconciliation.

---

# 395. SB14 Exit Gate — Migration

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Migration**. No phase is complete because UI renders alone.

---

# 396. SB15 — Security & SoD

Permissions, legal-entity scope, price confidentiality, approvals seam, audit/export controls.

---

# 397. SB15 Exit Gate — Security & SoD

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Security & SoD**. No phase is complete because UI renders alone.

---

# 398. SB16 — Performance & Ops

Worker scaling, indexes, observability, alerting, reconciliation and runbooks.

---

# 399. SB16 Exit Gate — Performance & Ops

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Performance & Ops**. No phase is complete because UI renders alone.

---

# 400. SB17 — Regional Compliance E2E

Egypt/Saudi correction-path tests through Invoicing/Compliance; no country rules embedded in Subscription.

---

# 401. SB17 Exit Gate — Regional Compliance E2E

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Regional Compliance E2E**. No phase is complete because UI renders alone.

---

# 402. SB18 — Production Readiness

Red-team, concurrency, chaos/provider outage, migration rehearsal, finance/business sign-off.

---

# 403. SB18 Exit Gate — Production Readiness

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Production Readiness**. No phase is complete because UI renders alone.

---

# 404. SB19 — Usage Seam Only

Contracts/interfaces/data identifiers for future usage; do not build generalized rating engine unless release scope explicitly includes it.

---

# 405. SB19 Exit Gate — Usage Seam Only

Phase exit requires automated tests, architecture boundary verification, migration/backward compatibility review, observability coverage, and product/finance acceptance for **Usage Seam Only**. No phase is complete because UI renders alone.

---

# 406. Runbook — Duplicate Billing Suspected

Freeze retry for affected logical period; compare idempotency keys, InvoiceRequest and Invoicing result; do not delete either document; escalate legal correction if duplicate invoice was issued.

---

# 407. Runbook — Missed Billing Period

Identify due period and root cause; preview catch-up; validate service period/tax/current contract revision; request invoice using original logical identity or approved catch-up adjustment.

---

# 408. Runbook — Wrong Proration

Block further related billing; reproduce using calculation version; if invoice issued, correct through credit/debit document path; never edit issued invoice or historical revision.

---

# 409. Runbook — Wrong Contract Price

Determine whether source was draft, amendment, renewal or migration; fix prospective revision; issued periods require legal adjustment.

---

# 410. Runbook — Renewal Did Not Run

Inspect renewal prerequisite/price/policy/job state; retry idempotently. Never manually create a shadow contract as workaround.

---

# 411. Runbook — Auto Renewal Ran Twice

Verify term uniqueness/idempotency; keep one canonical next term; if downstream documents exist, use controlled correction rather than record deletion.

---

# 412. Runbook — Scheduled Amendment Applied Early

Suspend further billing, capture audit, restore via compensating future revision; issued financial documents require adjustment route.

---

# 413. Runbook — Payment Provider Outage

Payments owns attempts. Subscription keeps contract/billing history, avoids duplicate charges, extends/pauses consequence timers only by explicit incident policy.

---

# 414. Runbook — Invoicing Unavailable

Keep BillingPeriod in requested/retryable state via outbox; do not mark Billed until acknowledged. Monitor backlog and catch up safely.

---

# 415. Runbook — Customer Disputes Renewal

Show signed/current term, renewal policy, notices, effective amendment and invoices; do not reconstruct from current plan configuration.

---

# 416. Runbook — Customer Disputes Cancellation Charge

Show cancellation request/effective date, minimum term/notice policy, proration and legal adjustment evidence.

---

# 417. Runbook — Migration Duplicate Charge

Compare BilledThroughDate and external invoice evidence; stop affected batch; correct issued duplicates legally and repair cutover marker.

---

# 418. Runbook — MRR Dashboard Wrong

Rebuild projection from contract revisions; never edit MRR fact manually. Validate FX/formula version and exclusions.

---

# 419. Runbook — Contract Customer Linked Wrong

Correct PartyRef prospectively with strict authorization/evidence; preserve prior snapshot/audit and assess invoices/privacy exposure.

---

# 420. Runbook — Billing Job Backlog

Scale workers within safe limits, confirm DB indexes/locks, prioritize oldest due with tenant fairness, watch downstream Invoicing capacity.

---

# 421. Runbook — Projection Corruption

Drop/rebuild projection from canonical events/tables; source contract/revisions remain untouched.

---

# 422. Runbook — DR Restore

Restore database, verify outbox/inbox/idempotency tables before workers resume, reconcile billing periods against Invoicing to avoid replay duplicates.

---

# 423. Forbidden Runbook

Never solve incidents by deleting issued invoices, editing historical revision dates, changing idempotency keys to force success, manually marking unpaid as paid, disabling tenant filters, or writing cross-module tables.

---

# 424. Red-Team — Clone Last Invoice

A developer proposes copying previous invoice for next month. Reject: invoice is output, contract revision is source.

---

# 425. Red-Team — Price List Change Reprices Active Contracts

Change Pricing master and ensure active term recurring amount stays unchanged unless policy/amendment says otherwise.

---

# 426. Red-Team — Quantity Edited In Place

Attempt direct quantity update on active line; must require amendment and preserve old revision.

---

# 427. Red-Team — Two Users Amend Same Contract

Both preview same version, one commits; second must receive revision conflict, not last-write-wins.

---

# 428. Red-Team — Billing Worker Runs Twice

Two workers claim same due period; only one logical InvoiceRequest may exist.

---

# 429. Red-Team — Worker Timeout After Invoice Created

Retry same key; no duplicate invoice.

---

# 430. Red-Team — One-Time Fee Retried

Setup fee remains billed once.

---

# 431. Red-Team — Month-End Drift

Jan 31 EOM contract must not drift permanently to Feb 28.

---

# 432. Red-Team — Leap-Year Drift

Feb 29 policy remains deterministic across non-leap year and later leap year.

---

# 433. Red-Team — Timezone Midnight

UTC execution near midnight must not create wrong legal-entity business date.

---

# 434. Red-Team — Calendar Alignment Stub

Mid-month start aligned to month start must produce correct stub/proration.

---

# 435. Red-Team — Upgrade Mid-Period

Old unused credit/new remaining charge reconcile and preview equals committed calculation.

---

# 436. Red-Team — Downgrade Not Allowed Until Renewal

Immediate request must be rejected or scheduled according to policy.

---

# 437. Red-Team — Retroactive Change After Issued Invoice

Must not mutate historical contract/invoice; route correction.

---

# 438. Red-Team — Cancel At Term End

Current billing continues, next renewal does not occur.

---

# 439. Red-Team — Pause != Cancel

Pause preserves contract and history and follows policy on billing/term extension.

---

# 440. Red-Team — Payment Failed

Failed attempt must not mark contract Cancelled directly from provider webhook.

---

# 441. Red-Team — Payment Succeeded

Success must not mark invoice Settled in Subscription-owned data; canonical truth stays Payments/Subledger.

---

# 442. Red-Team — Card Token In Subscription DB

Schema/security test forbids provider token/PAN/CVV storage.

---

# 443. Red-Team — Customer Id Passed As Authorization

Portal user guessing ContractId must not access another account.

---

# 444. Red-Team — Shared B2B Contact

Contact linked to multiple Parties cannot automatically see all contracts.

---

# 445. Red-Team — Legal Entity Cross-Scope

Group user without target entity permission cannot view price/contract.

---

# 446. Red-Team — Product Deactivated

Existing contract history remains; renewal/new line behavior follows policy.

---

# 447. Red-Team — Customer Deactivated

Contract not silently deleted/cancelled.

---

# 448. Red-Team — Tax Rate Changed

Future invoice tax can change under Tax rules while contracted net price remains history.

---

# 449. Red-Team — Negative Adjustment

Must become proper adjustment intent, not negative invoice assumption.

---

# 450. Red-Team — Egypt Credit Note

Post-issuance reduction must link correction through Invoicing/Compliance.

---

# 451. Red-Team — Saudi Debit/Credit Note

Material change after issued invoice follows ZATCA-compatible note path.

---

# 452. Red-Team — Physical Goods Proration

Time proration cannot automatically credit shipped goods.

---

# 453. Red-Team — Usage Event Duplicate — Future

Same external usage ID delivered twice bills once.

---

# 454. Red-Team — Late Usage — Future

Late event after period close follows explicit adjustment policy, not silent rebill.

---

# 455. Red-Team — MRR Equals Cash

Dashboard test ensures payment success/failure does not directly define MRR.

---

# 456. Red-Team — Annual Invoice Equals MRR

12,000 annual invoice normalizes to policy MRR rather than 12,000 monthly.

---

# 457. Red-Team — Renewal Price Missing

Auto renewal stops safely in exception; never uses zero/old/current arbitrary fallback.

---

# 458. Red-Team — Scheduled Amendment Collision

Two future changes on same line/date require deterministic merge/supersede or rejection.

---

# 459. Red-Team — Module Deactivation With Active Contracts

Disable is blocked/wind-down; history/billing obligations not lost.

---

# 460. Red-Team — AI Changes Price

AI tool cannot commit price override/renewal without authorized human/policy path.

---

# 461. Red-Team — AI Invents Proration

AI explanation must reference deterministic calculation; cannot supply authoritative amount itself.

---

# 462. Red-Team — Export Leaks Negotiated Prices

Unauthorized role cannot export restricted contract economics.

---

# 463. Red-Team — Projection Used As Source

Corrupt projection and prove canonical contract/billing remains correct/rebuildable.

---

# 464. Test — Draft Contract

Create/update draft with valid canonical refs; no invoice/billing side effect.

---

# 465. Test — Activation

Freeze revision 1 and first term exactly once under repeated command.

---

# 466. Test — Monthly Anniversary

17 Oct start creates expected anniversary periods.

---

# 467. Test — Calendar Month

Mid-month start creates stub + next full calendar month.

---

# 468. Test — End Of Month

Jan31/Feb/Mar maintains EOM semantics.

---

# 469. Test — Leap Day

Configured Feb29 fallback passes golden vectors.

---

# 470. Test — Weekly

Week periods remain contiguous/no gaps/overlap.

---

# 471. Test — Quarterly

Quarterly service/billing periods generated accurately.

---

# 472. Test — In Advance

Invoice date occurs at period start/lead rule.

---

# 473. Test — In Arrears

Invoice occurs after service period close.

---

# 474. Test — Bill Before Start

N-day lead calculation/business calendar correct.

---

# 475. Test — One-Time Charge

Billed once across job retry.

---

# 476. Test — Fixed Recurring

Expected recurring amount and service period.

---

# 477. Test — Per Unit

Quantity × price with money precision.

---

# 478. Test — Contract Price Snapshot

Pricing change does not alter active historical contract.

---

# 479. Test — Price Override Permission

Unauthorized override denied; approved override audited.

---

# 480. Test — Proration Upgrade

Old credit + new charge golden vector.

---

# 481. Test — Proration Downgrade

Credit behavior golden vector.

---

# 482. Test — No Proration

Change follows configured effective policy with no hidden credit.

---

# 483. Test — Next Period Change

Current period unaffected, next period new revision.

---

# 484. Test — Next Renewal Change

Current term unaffected.

---

# 485. Test — Future Date Change

Applies only when date becomes effective.

---

# 486. Test — Amendment Conflict

Stale/scheduled overlap rejected.

---

# 487. Test — Retroactive Billed Change

Blocked/adjustment path required.

---

# 488. Test — Manual Renewal

Creates proposed/authorized next term.

---

# 489. Test — Auto Renewal

Repeated job creates one next term.

---

# 490. Test — Renewal Uplift

Price rounded/audited correctly.

---

# 491. Test — Renewal Current Pricing

Resolves new Pricing version only for next term.

---

# 492. Test — Non-Renewing

No next term; current remains active until end.

---

# 493. Test — Pause

Billing/service consequences follow selected preset.

---

# 494. Test — Resume

Next billing/proration deterministic.

---

# 495. Test — Immediate Cancellation

History preserved and adjustment intent correct.

---

# 496. Test — Term-End Cancellation

Current term completes and renewal suppressed.

---

# 497. Test — Trial Convert

Converts once with prerequisites.

---

# 498. Test — Trial Fail

No silent paid activation when payment prerequisite absent.

---

# 499. Test — Billing Period Uniqueness

Database/domain prevents duplicate logical period.

---

# 500. Test — Billing Worker Concurrency

Parallel workers create one request.

---

# 501. Test — Invoice Request Retry

Same idempotency key reused.

---

# 502. Test — Invoice Rejection

Period becomes visible exception.

---

# 503. Test — Invoice Finalization

Period links correct InvoiceRef without owning invoice status.

---

# 504. Test — Payment Failure Consequence

Grace/suspension policy reacts to approved event only.

---

# 505. Test — Payment Success Projection

No local balance source created.

---

# 506. Test — Portal Authorization

External user scope enforced.

---

# 507. Test — Benefit Query

Returns only active authorized coverage as-of date.

---

# 508. Test — Cross-Tenant RLS

Tenant A cannot read B even by IDs/search/export.

---

# 509. Test — Legal Entity Scope

Entity restriction enforced on list/detail/actions.

---

# 510. Test — Sensitive Price Field

Role without commercial permission gets redacted response.

---

# 511. Test — Audit

High-impact actions carry actor/reason/before-after/correlation.

---

# 512. Test — Migration BilledThrough

First TODIR bill starts after legacy covered period.

---

# 513. Test — Migration Dry Run

No writes before explicit import; errors explained.

---

# 514. Test — MRR Monthly

Monthly recurring line normalizes correctly.

---

# 515. Test — MRR Annual

Annual value normalizes according to formula.

---

# 516. Test — MRR One-Time Exclusion

Setup fee excluded.

---

# 517. Test — MRR Expansion

Quantity/price increase emits expansion delta.

---

# 518. Test — MRR Contraction

Downgrade emits contraction delta.

---

# 519. Test — MRR Churn

Cancellation/nonrenewal categorized correctly.

---

# 520. Test — FX Reporting

Original currency preserved; reporting conversion is projection.

---

# 521. Test — Arabic RTL

Create/preview/timeline usable with Arabic and mixed content.

---

# 522. Test — Accessibility

Keyboard/screen-reader/validation/focus paths.

---

# 523. Test — Billing Batch Scale

Due-period batch throughput meets target without duplicates.

---

# 524. Test — Hot Tenant Fairness

One tenant cannot indefinitely starve others.

---

# 525. Test — Outbox Replay

Replay safe after crash.

---

# 526. Test — Inbox Duplicate

Duplicate upstream event has no duplicate state effect.

---

# 527. Test — Out-Of-Order Event

Handler safely defers/ignores based on version.

---

# 528. Test — Projection Rebuild

MRR/queues rebuild equal expected canonical output.

---

# 529. Test — DR Replay

Restore + workers resume without duplicate billing.

---

# 530. Test — Egypt Adjustment E2E

Subscription change -> Invoicing credit/debit note path -> Compliance evidence.

---

# 531. Test — Saudi Adjustment E2E

Subscription change -> compliant adjustment note path.

---

# 532. Test — Payment Provider Outage

No duplicate attempt/billing side effects from retries.

---

# 533. Test — Invoicing Outage

Periods remain recoverable and visible.

---

# 534. Test — Long Job Downtime

Missed periods identified/reconciled, not silently skipped.

---

# 535. Test — Clock/Timezone

Business dates stable around UTC/local midnight.

---

# 536. Test — Money Rounding

Proration/period totals obey FinBase precision and residual policy.

---

# 537. Test — API Idempotency

Repeated externally retried commands yield one logical effect.

---

# 538. Test — API Concurrency ETag

Stale ETag returns conflict.

---

# 539. Test — Export Security

Scoped/redacted export and audit.

---

# 540. Test — AI Tool Permission

AI sees/acts only through authorized tool layer.

---

# 541. Test — AI Billing Explanation

Explanation amount exactly matches deterministic calculation.

---

# 542. Test — Usage Seam Contract — Future

Interface can be added without changing historic recurring line IDs/service-period evidence.

---

# 543. Build Now — MUST

V1 production scope should include:

```text
Contract + lines + terms + revisions
Directory/Catalog/Pricing direct integration
Fixed + per-unit recurring charges
One-time charges
Weekly/monthly/quarterly/semiannual/annual schedules
Anniversary/calendar/EOM anchors
Advance/arrears/bill-before timing
BillingPeriod history
Idempotent InvoiceRequest
Day-based proration
Immediate/next-period/next-renewal/future amendments
Pause/resume/cancel/non-renewing
Manual/auto renewal + uplift/current-price policy
Auto-collection intent via Payments reference
Grace/suspension consequence seam
ContractBenefit seam
Portal command seam
MRR/ARR + renewal/billing exception projections
Migration cutover + reconciliation
Arabic/RTL
Security/audit/concurrency/observability/runbooks
Egypt/Saudi legal adjustment boundary tests
```

---

# 544. Build Later — Evidence Based

Do **not** force into initial release unless validated:

```text
General usage meter/rating engine
Tiered/volume usage
Commit + overage
Prepaid credit burndown
Telecom-grade sub-day rating
Complex revenue allocation / IFRS 15 engine
Physical delivery-based proration
Full rental
Customer Success platform
Advanced churn ML
Multi-party complex contract hierarchy
Automated contract legal authoring/e-signature platform
Global tax engine inside Subscriptions
```

Contracts/data seams are designed now where retrofitting later would break history.

---

# 545. Do Not Build Accounting Here

Subscriptions must never post journals, own CoA, compute AR balance or become revenue-recognition ledger.

---

# 546. Do Not Build Payments Here

Do not implement gateway SDKs, token vault, retries/settlement/refunds inside Subscriptions.

---

# 547. Do Not Build Helpdesk/Service Here

Benefits/coverage are commercial facts only; tickets/work orders/visits stay with owner modules.

---

# 548. Do Not Build Pricing Master Here

Reusable price lists/rules stay in Pricing. Contract only snapshots agreed outcome.

---

# 549. Do Not Build Legal Country Rules Here

No ETA/ZATCA constants/endpoints/schemas inside Subscription domain; use Invoicing/Compliance Country Providers.

---

# 550. Do Not Build TODIR Control Plane Billing Here

Tenant customer subscriptions and platform module entitlements remain entirely separate bounded contexts.

---

# 551. Source-of-Truth Matrix

| Truth | Owner |
|---|---|
| Party / Customer / Contact | Directory |
| Product / Service / UOM | Catalog |
| Reusable pricing rules / price lists | Pricing |
| Contracted recurring agreement | **Subscriptions** |
| Contract revision / amendment / term | **Subscriptions** |
| Billing schedule / billing-period intent | **Subscriptions** |
| Legal Invoice / Credit / Debit Note | Invoicing |
| Tax calculation master/rules | Tax |
| Receivable/Open Item/Settlement | Subledger |
| Payment attempt/method/provider/refund | Payments |
| Journal / accounting posting | Ledger |
| Accounting reporting/close | Accounting |
| Work Order / Visit | Service |
| Support Case | Helpdesk |
| Stock / Delivery | Inventory |
| Sales quotation/order | Sales |
| Portal identity/access shell | Portal/IAM |
| Country e-invoice authority behavior | Compliance Country Provider |
| TODIR SaaS plan/module entitlement | Platform Control Plane |
| Cross-module analytics/DWH | Reporting/Insights |

---

# 552. Final Contract Rule

The active SubscriptionContract revision is the source of recurring commercial intent; invoice history never becomes the contract master.

---

# 553. Final Customer Rule

Subscriptions references Directory directly; no transitive customer identity through Invoicing.

---

# 554. Final Pricing Rule

Pricing resolves reusable/master pricing; active contract stores immutable agreed snapshot.

---

# 555. Final Amendment Rule

No in-place mutation of effective commercial history; every meaningful change is explicit/effective-dated.

---

# 556. Final Billing Period Rule

Every recurring billing occurrence is represented/reconstructible as a BillingPeriod with unique identity.

---

# 557. Final Invoice Rule

Subscriptions requests legal documents; Invoicing owns them.

---

# 558. Final Payment Rule

Subscriptions may request/interpret collection consequences; Payments/Subledger own money and settlement truth.

---

# 559. Final Accounting Rule

Subscriptions never posts ledger entries or owns recognized revenue.

---

# 560. Final Service Period Rule

Generated subscription invoice lines preserve service-period evidence.

---

# 561. Final Tax Rule

No VAT/country tax constants inside Subscriptions.

---

# 562. Final Compliance Rule

Issued-document corrections flow through Invoicing/Compliance credit/debit note mechanisms.

---

# 563. Final Proration Rule

V1 uses deterministic business-day/calendar-day proration with explicit policy/version and explainable components.

---

# 564. Final Renewal Rule

Renewal creates new term/revision; it never clones the last invoice.

---

# 565. Final Pause Rule

Pause is temporary policy-driven state, not cancellation/deletion.

---

# 566. Final Cancellation Rule

Cancellation preserves all contract/billing/financial history.

---

# 567. Final Non-Renewal Rule

Non-renewing preserves current term until its legitimate end.

---

# 568. Final Trial Rule

Trial has explicit dates/conversion; no hidden 100% discount hack required.

---

# 569. Final Price-Change Rule

Price-list changes do not silently reprice active signed contracts.

---

# 570. Final Idempotency Rule

Retrying scheduler/commands/integrations must never create duplicate billing.

---

# 571. Final Concurrency Rule

Stale amendment/billing writes fail safely; no last-write-wins for money-affecting state.

---

# 572. Final Usage Rule

Usage/rating is future-ready but not prematurely built into simple recurring billing.

---

# 573. Final Benefit Rule

ContractBenefit expresses commercial coverage; downstream operational modules remain owners of execution.

---

# 574. Final Portal Rule

Self-service uses same domain commands/policies as staff; UI cannot bypass terms.

---

# 575. Final MRR Rule

MRR/ARR are normalized commercial analytics, distinct from invoices, cash and accounting revenue.

---

# 576. Final Currency Rule

Original contract currency/amount is immutable evidence; reporting FX is separate projection.

---

# 577. Final Migration Rule

Cutover must preserve billed-through boundary and never duplicate legacy charges.

---

# 578. Final Regional Rule

Arabic/RTL and regional payment/compliance adapters are first-class, but legal/provider rules stay in adapters/owner modules.

---

# 579. Final Security Rule

Contract ID, customer ref or file ref is never authorization.

---

# 580. Final Payment-Data Rule

No card PAN/CVV/provider secret in Subscription DB/log/event.

---

# 581. Final Audit Rule

Every commercial-history-changing action is attributable, reasoned and reconstructible.

---

# 582. Final Exception Rule

Failures are visible exceptions; no silent skip/catch-up/price fallback.

---

# 583. Final Simplicity Rule

Tiny business can operate with customer + service + price + frequency + start + renew; advanced controls remain progressive.

---

# 584. Final Enterprise Rule

Enterprise depth is achieved by revisions/policies/integration seams, not by duplicating separate subscription engines.

---

# 585. Final Performance Rule

Billing can scale horizontally only while preserving per-contract ordering and idempotency.

---

# 586. Final AI Rule

AI may explain/suggest; deterministic engines and authorized humans/policies commit financial outcomes.

---

# 587. Final Product Goal

Make recurring revenue boringly correct: simple to sell, predictable to bill, easy to explain, safe to change and auditable years later.

---

# 588. Integration Scenario — Sales To Subscription

Confirmed recurring quotation -> CreateDraftSubscription -> validate/snapshot pricing -> activate after policy. Sales order remains immutable origin.

---

# 589. Integration Scenario — New Customer Contract

Directory Party exists -> Contract draft -> price -> schedule -> activate -> first BillingPeriod.

---

# 590. Integration Scenario — Existing Customer Special Price

Pricing base resolved -> authorized contract override -> snapshot -> activation.

---

# 591. Integration Scenario — Monthly SaaS Seats

25 seats × recurring price -> monthly periods -> invoice requests.

---

# 592. Integration Scenario — Annual Maintenance Paid Monthly

12-month term + monthly billing; renewal separate from month 12 invoice.

---

# 593. Integration Scenario — Annual Prepay

12-month service period billed in advance; service-period evidence supports future deferral.

---

# 594. Integration Scenario — Setup Fee + Monthly

One-time setup invoice intent plus recurring monthly line; retry safe.

---

# 595. Integration Scenario — Mid-Month Start

Calendar-aligned stub period prorated then full month.

---

# 596. Integration Scenario — Mid-Month Upgrade

Amendment preview -> prorated credit/charge -> adjustment document intent -> next full period new rate.

---

# 597. Integration Scenario — Seat Increase Next Month

Schedule amendment at next boundary; no current-period adjustment.

---

# 598. Integration Scenario — Downgrade At Renewal

Current term unchanged; next-term revision reduced.

---

# 599. Integration Scenario — Future Price Increase

Schedule effective date; billing before date uses old revision, after uses new.

---

# 600. Integration Scenario — Renewal Uplift

Auto/manual renewal applies 8% uplift with new term audit.

---

# 601. Integration Scenario — Renewal Current Price List

At renewal, Pricing resolves active price and snapshots next term.

---

# 602. Integration Scenario — Renewal Missing Price

Exception queue; no silent renewal.

---

# 603. Integration Scenario — Customer Non-Renews

Mark non-renewing; current term remains active; expiration stops future periods.

---

# 604. Integration Scenario — Pause One Month

Pause policy halts billing and extends term or not per configured preset.

---

# 605. Integration Scenario — Resume Early

Compute revised period/next bill and optional proration.

---

# 606. Integration Scenario — Cancel Immediately

Create cancellation revision + adjustment intent; issued invoice corrected via Invoicing if needed.

---

# 607. Integration Scenario — Cancel End Of Term

No next renewal; current service/billing completes.

---

# 608. Integration Scenario — Invoice Finalized

Invoicing callback/event links InvoiceRef and period Billed.

---

# 609. Integration Scenario — Invoice Rejected

Period Failed/exception with retry/remediation; no next-date silent advance.

---

# 610. Integration Scenario — Automatic Payment

Invoice finalized -> Payments attempts opaque method -> outcome event.

---

# 611. Integration Scenario — Payment Failure Grace

Payment failed -> grace starts according to policy; open item remains Subledger truth.

---

# 612. Integration Scenario — Nonpayment Suspension

After grace signal -> Subscription Suspended; invoice remains unpaid, not altered.

---

# 613. Integration Scenario — Payment Later Succeeds

Payments/Subledger settle -> policy can resume service if configured.

---

# 614. Integration Scenario — Refund After Cancellation

Cancellation creates refund review/request; Payments owns actual refund lifecycle.

---

# 615. Integration Scenario — Premium Helpdesk Coverage

Helpdesk queries active ContractBenefit and snapshots support tier.

---

# 616. Integration Scenario — Maintenance Visits

Service queries coverage; visit/work-order remains Service truth.

---

# 617. Integration Scenario — Physical Monthly Delivery — Future

Subscription requests recurring fulfillment; Inventory/Sales produce actual delivery.

---

# 618. Integration Scenario — Usage Overage — Future

Usage records close -> rating -> adjustment InvoiceRequest, without modifying base recurring line history.

---

# 619. Integration Scenario — Egypt Post-Issue Reduction

Amendment requires reduction -> Invoicing emits ETA-compatible Credit Note linked to prior invoice.

---

# 620. Integration Scenario — Egypt Post-Issue Increase

Approved adjustment -> Debit Note path as legally appropriate.

---

# 621. Integration Scenario — Saudi Cancellation Correction

Issued invoice affected -> compliant credit/debit note through ZATCA provider.

---

# 622. Integration Scenario — Multi-Currency Dashboard

Contracts remain EGP/SAR/USD; Reporting converts MRR using disclosed FX policy.

---

# 623. Integration Scenario — Legacy Migration

Import active contract + billed-through + next billing -> dry run -> reconcile -> first TODIR invoice after cutover.

---

# 624. Integration Scenario — Provider Outage

Billing invoice remains valid; Payments retry handled separately; no duplicate invoice.

---

# 625. Integration Scenario — Billing Job Outage

Missed due periods identified and catch-up previewed on recovery.

---

# 626. Integration Scenario — Legal Entity Group

Contracts stay owned by one entity; group report aggregates via Reporting.

---

# 627. Integration Scenario — Portal Upgrade

Customer previews impact -> authorized amendment command -> same invariants as staff UI.

---

# 628. Final Agent Rule — Role

Implementation agent acts as Senior Product Architect + Senior Software/Systems Architect + Senior Engineer; business correctness before framework convenience.

---

# 629. Final Agent Rule — Read Governance

Before coding, read root/scoped AGENTS.md and canonical packs; do not invent local exceptions that violate repository governance.

---

# 630. Final Agent Rule — Protect Boundaries

No direct cross-module table access, no shadow Party/Product/Invoice/Payment masters.

---

# 631. Final Agent Rule — Preserve History

Never implement active contract edits as destructive row overwrite when commercial meaning changes.

---

# 632. Final Agent Rule — Money

Use FinBase Money/decimal precision; never float/double for financial amount.

---

# 633. Final Agent Rule — Dates

Use business-date/timezone/anchor policies; never naive server-local DateTime/AddMonths assumptions.

---

# 634. Final Agent Rule — Idempotency

Every retriable billing/payment/invoice integration path must have stable logical idempotency identity.

---

# 635. Final Agent Rule — Concurrency

Use aggregate version/database uniqueness/worker claim; test parallel execution.

---

# 636. Final Agent Rule — Invoice

Never write Invoicing tables or mark legal document state locally.

---

# 637. Final Agent Rule — Payment

Never store card data/provider secret; use Payments refs/contracts.

---

# 638. Final Agent Rule — Tax

Never hard-code VAT/tax/country legal constants in Subscription.

---

# 639. Final Agent Rule — Regional

Egypt/Saudi behavior is tested through Country Providers, not if(country==...) scattered in domain.

---

# 640. Final Agent Rule — Preview

Financial preview and commit use same server calculation engine/version.

---

# 641. Final Agent Rule — Explainability

Persist enough calculation lineage to explain every charge/credit/renewal amount.

---

# 642. Final Agent Rule — UI

Default UI simple; advanced settings progressive. Do not expose architecture jargon to ordinary operator.

---

# 643. Final Agent Rule — Errors

Return stable reason codes + human explanation; never swallow failed billing.

---

# 644. Final Agent Rule — Observability

Every billing job/integration has correlation, metrics and exception path before production.

---

# 645. Final Agent Rule — Tests

Golden vectors cover anchors, leap/month-end, proration, rounding, amendments, retries and regional correction E2E.

---

# 646. Final Agent Rule — Performance

Optimize measured query/job paths; do not add Kafka/ClickHouse/OpenSearch/microservices because subscription systems can be large.

---

# 647. Final Agent Rule — AI

AI never becomes source of numeric billing truth or autonomous financial authority.

---

# 648. Final Agent Rule — Migration

No production cutover without billed-through/next-bill reconciliation and first-cycle validation.

---

# 649. Final Agent Rule — Build Later

Do not implement generalized rating/credit ledger/IFRS engine until evidence; preserve seams only.

---

# 650. Final Agent Rule — Release

No release until Finance/Billing/Product/QA sign off duplicate/missed billing, proration, renewal, correction and migration scenarios.

---

# 651. Final Agent Question

Before any implementation decision ask: **Will this make the next bill correct, explainable and non-duplicated after retries, changes and legal corrections?**

---

# 652. Release Gate — Required Evidence

Production release requires evidence of:

```text
Architecture tests pass
Tenant/legal-entity isolation pass
No cross-module DB access
Golden billing-date vectors pass
Golden proration vectors pass
Money/rounding reconciliation pass
Concurrency/idempotency stress pass
InvoiceRequest retry/no-duplicate pass
Renewal/amendment conflict pass
Pause/cancel/non-renewing pass
Payment outage/recovery pass
Invoicing outage/recovery pass
Migration dry-run/reconciliation pass
Egypt adjustment E2E pass where Egypt pack enabled
Saudi adjustment E2E pass where Saudi pack enabled
Arabic/RTL/accessibility acceptance
Performance batch targets pass
Observability/alerts/runbooks exercised
Finance + Billing Ops + Product + Security + QA sign-off
```

---

# 653. Release Sign-Off

Required owners at minimum:

```text
Product Owner
Product/Domain Architect
Finance/Accounting representative
Billing/Revenue Operations representative
Backend Lead
Frontend Lead
Payments/Integration owner when auto-collection enabled
Compliance/Tax owner for country packs in release
Security
QA
Operations/SRE
```

Any unresolved financial-integrity or legal-document issue blocks release.

---

# 654. No Separate Subscription Subfeature Packs

This file is the comprehensive customer-facing Subscriptions module pack. Do not split proration, renewals, billing periods, amendments or simple usage seam into independent module packs unless a future capability becomes a truly reusable bounded context with its own ownership/lifecycle.

---

# 655. Future Separate Capability — Revenue Recognition

If TODIR later needs advanced IFRS 15/ASC 606 allocation, performance obligations, standalone selling price allocation and recognition schedules across modules, design it as Accounting/Finance capability rather than expanding Subscription into an accounting engine.

---

# 656. Future Separate Capability — Rating & Credits

If large telecom/cloud/AI customers require high-volume metering, real-time rating, shared commitments, prepaid wallets/credits and late-event re-rating, evaluate a separate reusable Rating/Credit capability consumed by Subscriptions and other domains.

---

# 657. Future Separate Capability — Rental

Rental remains a separate operations domain if market demand justifies asset availability/check-out/return/deposit/damage workflows. It may reuse recurring billing contracts.

---

# 658. Commercial Positioning

TODIR should position Subscriptions as **ERP-native recurring contracts**: simpler than specialist billing platforms for ordinary companies, but architecturally strong enough for B2B amendments, renewals, regional payments, accounting evidence and future usage models.

---

# 659. Differentiator

The differentiator is not “we can generate an invoice every month.” It is:

> **One recurring commercial truth connected cleanly to customer, product, pricing, invoicing, payment, service/support coverage and regional compliance — without duplicating those domains.**

---

# 660. Experience Target

For a small business owner: `Customer + Service + 500/month + Start + Renew` should feel enough.

For a billing operator: every exception/change must be visible and explainable.

For an accountant: no issued history is rewritten.

For an enterprise architect: boundaries, idempotency, revisions and integration contracts remain durable at scale.

---

# 661. Official Market / Regulatory Source Register

Verified during design on **2026-10-08**. These sources provide market/behavior evidence, not permission to copy competitor architecture blindly.

1. **Odoo 19 Subscriptions** — recurring plans, lifecycle, alignment, renewal/upsell/automatic payments.  
   `https://www.odoo.com/documentation/19.0/applications/sales/subscriptions.html`  
   `https://www.odoo.com/documentation/19.0/applications/sales/subscriptions/automatic_payments.html`

2. **ERPNext Subscription / Subscription Plan** — prepaid/postpaid, bill-before-period, calendar alignment, recurring plan behavior.  
   `https://docs.frappe.io/erpnext/subscription`  
   `https://docs.frappe.io/erpnext/subscription-plan`

3. **Chargebee Proration** — day/millisecond proration and mid-term change credits/charges.  
   `https://www.chargebee.com/docs/billing/2.0/subscriptions/proration`

4. **Stripe Billing Recurring Pricing Models** — flat/per-seat/tiered/usage, fixed-fee+overage, pay-as-you-go, credits.  
   `https://docs.stripe.com/billing/subscriptions/metered-billing/thresholds`

5. **Oracle NetSuite SuiteBilling** — usage and commit-plus-overage evidence.  
   `https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_0903034054.html`

6. **Microsoft Dynamics 365 Subscription Billing** — recurring contract billing and revenue/expense deferrals separation.  
   `https://learn.microsoft.com/en-us/dynamics365/finance/accounts-receivable/subscription-billing-summary`  
   `https://learn.microsoft.com/en-us/dynamics365/finance/accounts-receivable/sb-deferrals`

7. **SAP Subscription Billing** — usage records/metering/rating architecture evidence.  
   `https://help.sap.com/docs/subscription-billing/feature-overview/usage-data`

8. **Zoho Billing** — dunning and pause/resume operational evidence.  
   `https://www.zoho.com/us/billing/help/settings/subscriptions/dunning-management.html`

9. **Paymob Developer Documentation** — regional subscription-plan/recurring payment capability.  
   `https://developers.paymob.com/paymob-docs/developers/subscription/create-subscription-plan`

10. **Amazon Payment Services** — token/agreement based recurring payments and regional payment options.  
    `https://paymentservices.amazon.com/docs/managing-payments/recurring-payments`

11. **Egyptian Tax Authority eInvoicing SDK** — Invoice/Credit Note/Debit Note document semantics.  
    `https://sdk.invoicing.eta.gov.eg/types/`  
    `https://sdk.invoicing.eta.gov.eg/einvoicingapi/01-submit-documents/`

12. **Saudi ZATCA E-Invoicing** — invoice and credit/debit note correction requirements.  
    `https://zatca.gov.sa/en/E-Invoicing/Pages/default.aspx`  
    `https://www.zatca.gov.sa/en/e-invoicing/systemsdevelopers/pages/e-invoice-specifications.aspx`

Country law, tax rates, mandates, effective dates and payment-provider commercial availability remain **LEGAL/MARKET VERIFICATION at implementation/release time**.

---

# 662. Architecture Closure

The architecture for `30 Subscriptions & Recurring Billing` is approved when implementation preserves all of these non-negotiables:

```text
1. Direct Directory dependency for customer contract identity.
2. Contract revisions are immutable/effective-dated.
3. BillingPeriod is first-class and idempotent.
4. Invoicing owns legal documents.
5. Payments/Subledger own money/settlement.
6. Pricing master cannot silently reprice active contract.
7. Post-issue corrections use legal adjustment documents.
8. Day-based proration is deterministic/explainable in V1.
9. Renewal creates next term, not copied invoice.
10. Usage/rating remains future-ready but deferred.
11. MRR is commercial analytics, not cash/accounting revenue.
12. Small-company UX stays simple while enterprise controls remain optional.
```

---

# 663. Final Product Verdict

TODIR should build Subscriptions as a **general-purpose recurring commercial contract engine for real businesses in Arabic markets**, not as a SaaS-only monthly-invoice feature.

The approved product stance is:

> **Simple outside, contract-correct inside. Every recurring amount must be reproducible from effective terms; every change must have a date and history; every invoice must be requested idempotently; every legal correction must pass through Invoicing/Compliance; and no customer should ever be charged twice because a worker retried.**

This gives TODIR a clean path from SME maintenance/support/hosting/membership billing to enterprise recurring contracts and later usage-based models without rebuilding the financial core.

---


<!-- Section count: 664 -->
