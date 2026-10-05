# ERP SaaS Master Module & Dependency Blueprint — v3 Approved Architecture Baseline

**Status:** Architecture Baseline — approved for Module Requirements Research  
**Date:** 2026-10-05  
**Target:** Modular multi-tenant ERP SaaS for Arabic markets  
**Initial architecture:** C# / ASP.NET Core + React/TypeScript + PostgreSQL, Modular Monolith, One Codebase  
**Primary objective:** Every customer can subscribe to only the modules they need **without broken workflows, duplicated master data, hidden circular dependencies, or per-customer code forks**.

> This document replaces the v2 candidate as the project’s architecture baseline.
> It is intentionally strict about what is **architecturally locked**, what is **designed now but built later**, and what remains **market/legal validation**.

---

# 0. Decision & Evidence Legend

## 0.1 Architecture status

- **LOCKED** — architecture decision we adopt unless future evidence forces an ADR change.
- **DESIGN NOW / BUILD LATER** — contract/data shape must be future-safe now; feature implementation is deferred.
- **MARKET VALIDATION** — product/commercial priority is not architecture truth.
- **LEGAL VERIFICATION** — country-specific legal/tax/payroll detail must be verified from current official sources before production.
- **LATER** — safe to defer without closing an important future path.

## 0.2 Evidence status

- **F** — official/primary source.
- **E** — repeated credible evidence or recognized industry pattern.
- **I** — architectural/product inference.
- **R** — recommendation.
- **U** — not yet verified.

## 0.3 Most important rule

> **Future-proof the contracts; do not future-build the product.**

We design likely future seams early only when adding them later would require changing historical data or breaking existing module contracts.

---

# 1. Executive Architecture Verdict

## 1.1 What is locked

1. **Modular Monolith first.**
2. **One codebase** for all customers.
3. **Tenant isolation is a security boundary**, not a filter convention.
4. **No customer-specific code forks.**
5. **Single Source of Truth** for every canonical business entity.
6. **Single Writer** per owned entity/table.
7. **Business modules do not read/write each other’s tables directly.**
8. **Hard dependency graph is a DAG.**
9. **Optional integrations use Ports/Capabilities/Events, not database coupling.**
10. **Posted accounting records are immutable.**
11. **Completed stock movements are immutable.**
12. **Country law lives in versioned Country Packs / adapters, not Global Core.**
13. **Accounting posting uses business facts, not account IDs from business modules.**
14. **Design Order ≠ Implementation Order ≠ Release Order.**
15. **Deactivate ≠ Delete.**
16. **Module activation/deactivation is dependency-aware and data-safe.**
17. **Role = Experience**, not only permission checks.
18. **AI never reads/writes the database directly.**
19. **Customization = metadata/config/rules/APIs before code.**
20. **Infrastructure complexity must earn its place.**

## 1.2 Structural corrections introduced in v3

The following v2 issues are corrected here:

1. Removed circular dependency between `compliance` and country packs.
2. Removed conceptual `sales ↔ crm` soft cycle; CRM may call Sales, Sales keeps only a generic origin reference.
3. Removed changing ownership of `CreditProfile`.
4. Introduced a hidden **`subledger` financial kernel** for Open Items, Settlements, Advances, Financial Exposure and Party Financial Profile.
5. `invoicing` no longer owns Open Items/Settlements.
6. `payments` can operate with correct financial balances without depending on `invoicing`.
7. Split invoice state into **Document / Accounting / Regulatory / Settlement** state dimensions.
8. Split always-on platform capabilities from dependency-activated shared business domains.
9. Added **Module Go-Live Contract** for every stateful business module, not only Ledger.
10. Added **In-Process Transaction Coordinator** so “same transaction” is implementable across module schemas.
11. Added Master Data Visibility Scope across Legal Entities.
12. Reframed Managed Ledger → Full Accounting as a reconciliation/configuration transition, not “zero migration.”
13. Replaced the unverified Egypt VAT threshold claim with the current official ETA threshold.
14. Changed default first commercial release from “billing-only” to **Trade Core**; billing-only remains a packaging option only if market evidence supports it.
15. Completed the missing final sections: gaps, deferrals, risk register, invariants, red-team, final sequences, source register and final verdict.

---

# 2. Product Architecture Principles

## 2.1 Simple outside, powerful inside

The database/domain may be rich. The employee UI must show only what the role and task require.

## 2.2 Minimum Input Principle

Do not ask a user for information that can be:

- inferred from company/entity;
- inherited from Party/Product profiles;
- calculated;
- defaulted;
- derived from previous documents;
- retrieved from an authoritative integration.

Sensitive defaults must have **Explain Why**.

## 2.3 Data Once Principle

The same business fact should not be entered independently into Sales, Accounting, CRM and Inventory.

## 2.4 Exception-based ERP

The system should automate the normal path and surface exceptions:

- credit exceeded;
- stock mismatch;
- rejected e-invoice;
- tax inconsistency;
- unmatched payment;
- failed integration;
- abnormal payroll input.

## 2.5 Correct domain boundaries before feature count

We do not compete with Odoo/SAP/Dynamics by screen count. We compete on:

- correct ownership;
- predictable upgrades;
- simple onboarding;
- strong Arabic/localization support;
- accounting integrity;
- inventory integrity;
- controlled customization.

---

# 3. Final Architecture Layers

```text
┌────────────────────────────────────────────────────────────────────┐
│ L6  SCALE / INTELLIGENCE                                            │
│     insights, DWH, advanced search, regional cells, AI agents       │
├────────────────────────────────────────────────────────────────────┤
│ L5  PLATFORM-BUSINESS                                               │
│     approvals, studio, reporting, integrations, compliance, portal │
│     migration, ai_gateway                                           │
├────────────────────────────────────────────────────────────────────┤
│ L4  PEOPLE / RELATIONSHIP                                           │
│     crm, hr_core, leave, attendance, payroll, expenses, projects    │
│     service, commissions                                            │
├────────────────────────────────────────────────────────────────────┤
│ L3  TRADE / OPERATIONS                                              │
│     sales, purchasing, inventory, pos, channels, manufacturing,     │
│     shipping, quality, maintenance                                  │
├────────────────────────────────────────────────────────────────────┤
│ L2  FINANCE                                                         │
│     ledger★, subledger★, accounting, invoicing, payments, bank_rec, │
│     cheques, collections, assets, budgeting, subscriptions          │
├────────────────────────────────────────────────────────────────────┤
│ L1  SHARED BUSINESS DOMAINS — non-purchasable, dependency-activated│
│     directory, catalog, finbase, tax, pricing                       │
├────────────────────────────────────────────────────────────────────┤
│ L0  PLATFORM KERNEL — always on, no business workflow semantics     │
│     tenancy, org, iam, audit, registry, messaging, platform services│
└────────────────────────────────────────────────────────────────────┘

★ ledger/subledger are hidden financial kernels auto-activated when required.
```

## 3.1 Important change from v2

`directory`, `catalog`, `finbase`, `tax`, and `pricing` are **not all provisioned for every tenant by default**.

They are:

- deployed in the one codebase;
- non-purchasable;
- automatically activated/provisioned when a subscribed module needs them.

Example:

- `HR + Attendance` does not require Product/Pricing/Tax data.
- `Sales` activates Directory + Catalog + Pricing + Tax + relevant FinBase capability.
- `Invoicing` activates Directory + Tax + FinBase + Ledger + Subledger.

This prevents a new “God Foundation.”

---

# 4. Module Types

## 4.1 Kernel Capability

Always available, technical, no business workflow.

Examples:

- tenancy;
- org;
- identity;
- audit;
- files;
- numbering engine;
- settings;
- jobs/outbox;
- custom field engine;
- transaction coordination.

## 4.2 Shared Business Domain

Not sold separately. Activated by dependency closure.

Examples:

- Party Directory;
- Product Catalog;
- Tax;
- Pricing;
- Exchange rates/payment terms.

## 4.3 Hidden Financial Kernel

Not sold directly but preserves financial integrity.

- `ledger`
- `subledger`

## 4.4 Purchasable Business Module

User-facing business capability:

- Sales
- Inventory
- Invoicing
- Accounting
- HR
- Payroll
- CRM
- POS
- etc.

## 4.5 Country Pack / Capability Provider

A Country Pack is **not a normal business module dependency node**.

It provides versioned capabilities such as:

- Tax seed/provider
- Compliance adapter
- Payroll statutory provider
- Legal document schema
- Retention rules
- numbering/signing requirements
- local calendars/templates

This is important because it prevents:

```text
compliance → pack → compliance
```

cycles.

---

# 5. Scope Model: Tenant, Legal Entity, Branch, Module Instance

## 5.1 Organizational hierarchy

```text
Tenant
 └── Legal Entity (1..N)
      └── Branch (0..N)
```

## 5.2 Subscription vs operational activation

A subscription may grant a module at Tenant level, but operational activation can be scoped.

Use:

```text
EntitlementGrant
  tenant_id
  module_id / feature_id
  allowed_legal_entities
  limits

ModuleInstance
  tenant_id
  module_id
  scope_type = tenant | legal_entity
  scope_id
  status
  go_live_at
  mode
```

Examples:

- CRM may be tenant-wide.
- Accounting, Tax, Compliance, Payroll, Inventory are normally Legal-Entity scoped.
- A tenant may have Egypt and Saudi Legal Entities using different country providers.

## 5.3 Country belongs to Legal Entity

Never treat `tenant.country` as the legal source of truth for compliance.

---

# 6. Canonical Data Ownership

| Entity / Concept | Owner | Scope | Notes |
|---|---|---|---|
| Tenant | `tenancy` | Tenant | security boundary |
| LegalEntity / Branch | `org` | Tenant | minimal legal/org identity |
| Identity / Membership / User | `iam` | platform/tenant | User ≠ Employee |
| Party / Contact / Address / Identifier | `directory` | Tenant + visibility scope | identity only |
| PartyRole | `directory` | Tenant | customer/supplier/etc. role |
| Product Template / Variant / UOM / Packaging / Barcode | `catalog` | Tenant + visibility scope | identity only |
| Currency reference | Kernel reference data | Global | ISO reference |
| Exchange Rate / Payment Term | `finbase` | Entity/Tenant | definitions |
| Tax Definitions / Profiles / Positions | `tax` | Entity/Country | versioned |
| Pricelist / PriceRule / customer price assignment | `pricing` | Entity/Tenant | price owner |
| SalesProfile | `sales` | Party×Entity | salesperson/territory/warehouse defaults |
| SupplierCommercialProfile | `purchasing` | Party×Entity | supplier code, lead time, purchase defaults |
| StockProfile / Cost / Valuation | `inventory` | Variant×Entity | no cost in Catalog |
| Account / Journal / Entry / Period / PostingRule | `ledger` | Entity | financial truth |
| PartyFinancialProfile | `subledger` | Party×Entity | credit limit/hold, financial terms/account roles |
| OpenItem / Settlement / Advance | `subledger` | Entity | AR/AP financial subledger |
| Invoice / Credit/Debit Note / Vendor Bill | `invoicing` | Entity | legal/commercial financial document |
| Payment / Receipt / Payout / Transfer | `payments` | Entity | cash/bank movement |
| Warehouse / Location / StockMove / Reservation | `inventory` | Entity | quantity truth |
| Quote / SalesOrder / SalesReturnAuthorization | `sales` | Entity | commercial commitment |
| RFQ / PurchaseOrder / PurchaseReturnAuthorization | `purchasing` | Entity | procurement commitment |
| Lead / Opportunity | `crm` | Tenant/Entity | Lead ≠ Party until qualified |
| Employee / Contract / Org people structure | `hr_core` | Entity | User ≠ Employee |
| Payslip / PayrollRun | `payroll` | Entity | immutable after finalize |
| ApprovalRequest / Policy | `approvals` | Tenant/Entity | approval metadata |
| CustomFieldDef | Kernel metadata | Tenant | values remain in owner module storage contract |

---

# 7. Master Data Visibility Across Legal Entities

Canonical master data is not duplicated merely to control visibility.

Every shared Party/Product gets a visibility policy:

```text
visibility_scope:
  TENANT_SHARED
  SELECTED_ENTITIES
  SINGLE_ENTITY
```

Supporting relation:

```text
EntityVisibility(subject_type, subject_id, legal_entity_id, access_mode)
```

## Rules

- One canonical Product/Party record remains the identity source.
- Commercial/financial profiles remain Legal-Entity specific.
- A user from Entity A cannot automatically see Entity B’s private Party/Product.
- Cross-entity sharing is explicit and auditable.
- Intercompany later can link Legal Entities without cloning Parties arbitrarily.

**Status:** LOCKED design, simple UI initially.

---

# 8. Module Ownership Rules

1. **Single Writer:** each business table has one owner module.
2. **No cross-module SQL writes.**
3. **No ORM navigation across business module schemas.**
4. Business-to-business module references use opaque IDs/Refs and Contracts.
5. Foundation IDs may have database FKs where stability is intentional.
6. Posted legal/financial snapshots do not change when master data changes.
7. A module never changes owner because a customer buys another module.
8. A provider does not know its consumers.
9. State transitions belong to the owner of that state machine.
10. Seed data is copied/proposed; future pack updates never silently overwrite tenant changes.

---

# 9. Dependency Model

## 9.1 Hard Dependency

Module cannot operate without provider.

Hard graph **must be a DAG**.

Examples:

- Accounting → Ledger
- Invoicing → Ledger + Subledger + Directory + Tax + FinBase
- Payments → Ledger + Subledger + Directory + FinBase
- Payroll → HR Core + Payroll Country Provider

## 9.2 Optional Capability

Consumer works without provider using a defined fallback.

Examples:

- Sales → Inventory fulfillment capability.
- Sales → Invoicing request capability.
- Sales → Credit check capability.
- Purchasing → Inventory receiving capability.
- CRM → Sales quotation capability.

## 9.3 Event-only

No synchronous dependency.

Examples:

- Reporting projections.
- Search indexing.
- Analytics.
- Notifications.
- Webhooks.

## 9.4 Important correction: soft relationships do not define ownership

We do **not** require every runtime soft relationship to form a theoretical DAG if it is only event/read interaction; we require:

- **Hard dependency DAG:** mandatory.
- **Compile-time implementation dependency DAG:** mandatory.
- No implementation assembly imports another implementation assembly.
- Cross-module contract packages are stable and versioned.
- No synchronous re-entrant cycles.
- Events must have correlation/causation protection.

---

# 10. Cross-Module Communication

| Need | Mechanism |
|---|---|
| Integrity-critical command | synchronous published contract |
| Optional provider | consumer-owned Port + resolver |
| Notification after commit | domain/integration event via Outbox |
| Cross-module report | read model/projection |
| UI link | EntityRef resolver |
| External integration | adapter behind Port |
| Long operation | job worker |

## Provider contracts

Provider owns stable capabilities such as:

- `Ledger.Post(Fact)`
- `Subledger.CreateOpenItem()`
- `Subledger.Allocate()`
- `Inventory.Reserve()`
- `Inventory.ExecuteMovement()`
- `Invoicing.CreateFromRequest()`
- `Directory.GetParty()`
- `Catalog.GetVariant()`
- `Pricing.Resolve()`
- `Tax.Compute(TaxContext)`

## Consumer Ports

Used where multiple providers/fallbacks may exist:

- `IFulfillment`
- `ICreditCheck`
- `IApprovalGuard`
- `IComplianceProvider`
- `IPaymentGateway`
- `IBankFeed`

---

# 11. In-Process Transaction Coordinator

The architecture requires several cross-module actions to commit atomically while remaining a Modular Monolith.

## 11.1 Required capability

Create a Kernel service:

```text
IApplicationTransaction
  Begin(tenantContext)
  Enlist(moduleDataContext)
  Commit()
  Rollback()
```

Implementation requirements:

- one PostgreSQL physical connection where possible;
- one database transaction;
- each module has its own DbContext/schema;
- each DbContext enlists in the same `DbTransaction`;
- `SET LOCAL app.tenant_id = ...` is set inside the transaction;
- Outbox rows are saved in the same transaction;
- idempotency records are saved in the same transaction.

## 11.2 Atomic paths

Must be same transaction:

- Finalized invoice → Ledger fact/posting + Subledger OpenItem.
- Completed stock movement → valuation layer + Ledger fact if financial ledger active.
- Payment posting → Ledger posting + Subledger credit/advance record.
- Settlement → Subledger allocation + FX/write-off facts.
- Payroll finalize with Ledger enabled → finalized run + posting fact.

External government APIs, WhatsApp, webhooks and search are **never** in the transaction.

**Status:** LOCKED, D0.

---

# 12. Financial Kernel Split: Ledger vs Subledger vs Accounting

## 12.1 Ledger

Owns the General Ledger:

- Chart of Accounts
- Journals
- Fiscal periods
- Journal entries
- posting rules
- account determination
- dimensions
- FX posting
- opening entries
- period locks

## 12.2 Subledger

Hidden financial kernel owning party obligations:

- `PartyFinancialProfile`
- `OpenItem`
- receivable/payable direction
- advance/unallocated balances
- `Settlement`
- allocation
- write-off relation
- cash discount relation
- realized FX settlement result
- Financial Exposure Query
- credit limit / credit hold policy

This solves two v2 problems:

1. Payments can create an advance without needing Invoicing.
2. Credit policy has one permanent owner whether Collections is subscribed or not.

## 12.3 Accounting

`accounting` is the user-facing finance application:

- CoA management
- manual journals
- close periods
- financial reports
- accountant Power Views
- audit/reconciliation workbench

It does not own the ledger data.

## 12.4 Collections

Collections adds workflow:

- collector assignment
- promise to pay
- call/follow-up
- dunning policy
- collection case
- escalation

It **does not own credit balance truth or credit limit master**.

---

# 13. Managed Ledger Mode

## 13.1 Purpose

Customers may buy Invoicing/Payments without buying the Accounting UI.

Financial documents still need internally consistent posting.

Therefore:

```text
ledger.mode = MANAGED | FULL
```

### MANAGED

- hidden CoA created from approved template;
- controlled account determination;
- no normal accounting UI;
- automatic posting only;
- audit/reconciliation still active.

### FULL

Enabled when Accounting is subscribed.

## 13.2 Managed → Full transition

Do not promise “no migration.”

Correct promise:

> No transaction reconstruction is required because historic facts were already posted, but reconciliation/configuration may be required before the ledger becomes the customer’s official operational accounting book.

Transition checklist:

1. map/confirm CoA;
2. confirm fiscal year/periods;
3. reconcile AR/AP Subledger;
4. reconcile inventory valuation;
5. validate opening/cutover differences against external accountant books;
6. reclassify through adjusting entries, never rewrite history;
7. set `FullAccountingGoLiveAt`;
8. obtain customer/accountant sign-off.

**Status:** LOCKED concept; local accounting templates require MARKET/LEGAL/domain review.

---

# 14. Invoice State Model — Four Independent Dimensions

Do not use one overloaded “Posted/Issued/Cleared” state.

## 14.1 Document lifecycle

```text
Draft
→ Finalized
→ Reversed/Corrected
```

## 14.2 Accounting state

```text
NotPosted
→ Posted
→ Reversed
```

## 14.3 Regulatory state

Canonical internal states:

```text
NotRequired
PendingSubmission
Submitted
Accepted
Rejected
FailedRetryable
FailedTerminal
```

Store raw authority status separately:

```text
authority_status_code
authority_status_payload/evidence_ref
```

The Country Provider maps authority-specific states.

## 14.4 Settlement state

```text
NotApplicable
Unpaid
PartiallySettled
Settled
```

## 14.5 Release rule

Country provider supplies:

```text
CanReleaseToCustomer(documentKind, regulatoryState)
```

This avoids assuming that every country/document is “clearance before release.”

**Status:** LOCKED.

---

# 15. Tax Architecture

## 15.1 Ownership

`tax` owns:

- tax definitions;
- tax rates effective-dated;
- categories;
- positions/profiles;
- country tax code mapping;
- calculation rules.

## 15.2 Compute contract

Tax should not orchestrate Sales/Party/Product.

Caller sends:

```text
TaxContext {
  legal_entity
  jurisdiction
  document_kind
  date
  party_tax_profile_ref
  line {
     tax_category_ref
     quantity
     net_amount
     discounts
  }
}
```

`Tax.Compute(context)` returns a deterministic `TaxResult`.

The issuing document stores a full snapshot.

## 15.3 Country Packs

Country Pack supplies:

- official tax codes;
- effective rules;
- required fields;
- document mapping.

No rates or legal constants hard-coded in Sales/Invoicing.

---

# 16. Payment Terms Ownership

One definition, multiple defaults.

- `finbase` owns **PaymentTerm definitions**.
- `subledger.PartyFinancialProfile` owns default financial terms per Party×Entity.
- Sales/Purchasing may display or override a term on a document.
- Confirmed documents snapshot the chosen term.
- No independent “default payment term” sources in multiple modules.

**Status:** LOCKED.

---

# 17. Module Go-Live Contract

Every stateful module must declare:

```yaml
go_live:
  required: true|false
  effective_from: date/time
  historical_policy:
  opening_data_policy:
  open_document_transition:
  backfill_policy:
```

## 17.1 Rules

- No fake backfill merely to make history look complete.
- Historical transactions before module activation remain historical snapshots unless explicitly migrated.
- Open documents at activation require an explicit transition policy.
- Go-live actions are idempotent and auditable.

## 17.2 Examples

### Inventory activated after Sales

- opening quantity/cost imported at Inventory Go-Live;
- old closed Sales Orders remain manual historical fulfillment;
- open Sales Orders are explicitly converted into reservations/delivery plans;
- no synthetic historical StockMoves unless a migration project intentionally creates them.

### Invoicing activated after Sales

- old closed orders remain commercial history;
- chosen open orders can become invoiceable;
- opening AR/AP imported to Subledger;
- no auto-creation of past legal invoices.

### Payroll activated after HR

- balances/loans/leave inputs imported as opening data;
- old months remain external payroll history unless migration scope says otherwise.

**Status:** LOCKED.

---

# 18. Module Manifest Standard v3

```yaml
module:
  id: sales
  version: 1.0.0
  contracts_version: 1
  layer: L3
  type: business
  purchasable: true
  activation_scope: legal_entity

depends:
  hard:
    - directory
    - catalog
    - pricing
    - tax
  capabilities:
    - fulfillment?      # inventory or fallback
    - invoice_request?  # invoicing or fallback:none
    - credit_check?     # subledger provider or fallback
    - approval_guard?   # approvals or static policy
  events: []

owns:
  schema: sales
  entities:
    - Quotation
    - SalesOrder
    - SalesOrderLine
    - SalesReturnAuthorization
    - SalesProfile

references:
  opaque:
    - OriginRef
    - FulfillmentRef
    - InvoiceRef

provides:
  contracts:
    - CreateDraftQuotation
    - GetOrder
  events:
    - SalesOrderConfirmed@1
    - SalesOrderCancelled@1
    - SalesReturnAuthorized@1

permissions:
  - sales.quote.create
  - sales.order.confirm
  - sales.price.override
  - sales.discount.override

activation:
  seed: [...]
  prerequisites: [...]
  idempotent: true

go_live:
  required: true
  opening_data_policy: open_orders_optional
  backfill_policy: none_by_default

deactivation:
  policy: wind_down
  blockers: [...]
  read_only_scope: [...]
  retained: all_business_history

retention:
  policy_ref: country_and_tenant_policy

features:
  - multi_currency_sales
  - advanced_discounting

limits:
  - orders_per_month

customization:
  supported_entities: [...]
  financial_fields_locked: true

ai_tools:
  - name: draft_quotation
    risk: draft
```

## Manifest invariants

- Module ID never changes.
- Owner entity changes are breaking architecture changes.
- Hard dependency must exist in DAG.
- Every optional capability declares fallback.
- Every stateful business module declares Go-Live and Deactivation contracts.
- Every cross-module reference declares type: FK-to-foundation or OpaqueRef.
- Every write contract has idempotency semantics.

---

# 19. Subscription & Entitlement Architecture

## 19.1 Control Plane

SaaS subscription/billing lives outside tenant ERP data.

```text
ModuleCatalog
→ Plan
→ Subscription
→ EntitlementGrant
→ Feature/Limit/Scope
```

Tenant application receives signed/cached entitlement snapshots.

## 19.2 Enforcement layers

1. API/middleware.
2. Command handler.
3. UI visibility.

UI is never the security boundary.

## 19.3 Dependency closure

When customer buys a module:

- calculate hard dependencies/shared-domain dependencies;
- auto-activate hidden kernels/shared capabilities;
- verify country capability provider;
- run provisioning;
- run Go-Live workflow;
- show impact/onboarding report.

## 19.4 Feature vs Module

Use module when it owns a true domain/workflow/data lifecycle.

Use feature entitlement for bounded behavior inside an existing domain:

- number of warehouses;
- multi-currency;
- FIFO availability;
- e-invoice submission;
- lots/serials;
- API quota;
- AI credits.

Do not turn every feature into a module.

---

# 20. Activation Lifecycle

```text
Requested
→ Validating
→ Provisioning
→ AwaitingConfiguration (optional)
→ ReadyForGoLive
→ Active

Failure:
Validating → Rejected
Provisioning → FailedSafe
ReadyForGoLive → Cancelled
```

Checks:

1. subscription grant;
2. hard dependency closure;
3. required provider capabilities;
4. legal entity scope;
5. country pack/provider;
6. opening data/go-live requirements;
7. configuration health;
8. role/permission seed;
9. reconciliation where required.

---

# 21. Deactivation Lifecycle

```text
Active
→ Grace
→ WindDown
→ ReadOnly
→ Disabled/Archived
→ Reactivated
```

## Global rules

- No cascade delete.
- Historical reports remain available according to retention law/contract.
- Open workflows must be completed/cancelled/transitioned.
- Hard dependents block deactivation.
- Optional consumers fall back explicitly.
- Integration jobs are drained/cancelled safely.
- Reactivation reuses retained data.

---

# 22. Final Module Catalog

## L0 Kernel

- tenancy
- org
- iam
- audit
- module registry / entitlement client
- messaging / outbox / inbox / jobs
- transaction coordinator
- files
- numbering
- notifications
- i18n/localization runtime
- settings
- custom fields metadata engine
- import framework
- print/template engine
- report/export engine
- webhook dispatcher
- basic search
- EntityRef resolver

## L1 Shared Business Domains

- directory
- catalog
- finbase
- tax
- pricing

## L2 Finance

Hidden:
- ledger
- subledger

Purchasable:
- accounting
- invoicing
- payments
- bank_rec
- cheques
- collections
- fixed_assets
- budgeting
- subscriptions
- intercompany/consolidation later

## L3 Trade & Operations

- inventory
- sales
- purchasing
- pos
- channels
- manufacturing
- quality
- shipping
- maintenance

## L4 People & Relationship

- crm
- hr_core
- leave
- attendance
- payroll
- expenses
- projects
- service
- commissions

## L5 Platform-Business

- approvals
- studio
- reporting
- compliance
- portal
- integrations
- migration
- ai_gateway

## L6 Scale/Advanced

- insights/DWH
- advanced search
- regional cells
- advanced AI agents
- enterprise consolidation

---

# 23. Final Hard Dependency Matrix

The table shows only **hard business dependencies**. Kernel infrastructure is implicit.

| Consumer | Hard Dependencies |
|---|---|
| directory | — |
| catalog | — |
| finbase | — |
| tax | finbase |
| pricing | catalog, finbase |
| ledger | finbase |
| subledger | ledger, directory, finbase |
| accounting | ledger |
| invoicing | ledger, subledger, directory, tax, finbase |
| payments | ledger, subledger, directory, finbase |
| bank_rec | payments, ledger |
| cheques | payments, subledger |
| collections | subledger |
| fixed_assets | ledger |
| budgeting | ledger |
| subscriptions | invoicing, catalog, pricing |
| inventory | catalog |
| sales | directory, catalog, pricing, tax |
| purchasing | directory, catalog, tax |
| pos | catalog, pricing, tax, payments |
| channels | sales, catalog |
| manufacturing | inventory, catalog |
| quality | inventory |
| shipping | inventory |
| maintenance | catalog |
| crm | directory |
| hr_core | org |
| leave | hr_core |
| attendance | hr_core |
| payroll | hr_core + PayrollCountryProvider |
| expenses | hr_core, ledger |
| projects | directory |
| service | directory |
| commissions | sales |
| approvals | iam |
| studio | Kernel metadata contracts |
| reporting | Kernel report capability |
| compliance | Kernel + active CountryComplianceProvider |
| portal | iam + relevant provider modules |
| integrations | Kernel |
| migration | Kernel import framework |
| ai_gateway | iam + audit + tool registry |

## 23.1 Optional capability examples

| Consumer | Optional Provider |
|---|---|
| sales | inventory, invoicing, subledger credit check, crm origin, approvals |
| purchasing | inventory, invoicing, approvals |
| inventory | ledger for valuation posting, approvals |
| invoicing | compliance, approvals, catalog, payments |
| payments | invoicing presentation, approvals, payment gateway |
| collections | sales exposure, payments activity, crm |
| crm | sales create-quotation |
| payroll | attendance, leave, ledger, payments, approvals, compliance |
| pos | inventory, invoicing, compliance, hr_core cashier |
| projects | hr_core, invoicing, sales |
| service | inventory, invoicing, hr_core |
| manufacturing | purchasing, ledger, hr_core, quality |

**Result:** Hard dependency graph is acyclic.

---

# 24. Critical No-Cycle Rules

## CRM / Sales

Allowed:

```text
CRM --optional command--> Sales.CreateDraftQuotation
Sales --stores--> OriginRef{module="crm", id, labelSnapshot}
```

Not allowed:

```text
Sales implementation → CRM implementation
```

## Compliance / Country Pack

Correct:

```text
CountryPack implements CountryComplianceProvider contract
Compliance resolves provider by LegalEntity/Country
```

Neither implementation hard-depends on the other implementation.

## Payments / Invoicing

Both depend on Subledger.

```text
Invoice → Subledger OpenItem
Payment → Subledger Credit/Advance
Settlement lives in Subledger
```

No Payments↔Invoicing ownership cycle.

---

# 25. Design Order — What Must Be Decided Before Coding

## D0 — platform and invariants

- Module Manifest schema
- hard dependency DAG validator
- tenant/RLS model
- Legal Entity/Branch scoping
- entitlement model
- ModuleInstance scope
- Outbox/Inbox/idempotency
- in-process transaction coordinator
- ActorType
- EntityRef/SourceRef
- custom fields storage contract
- numbering engine
- approval hook contract
- Money/Quantity/Rounding value objects
- Go-Live contract
- deactivation contract

## D1 — identity/master domains

- Party identity and visibility
- Product Template/Variant
- UOM/Packaging
- master snapshots
- archive/merge rules

## D2 — finance contracts

- Ledger facts catalog
- Subledger OpenItem/Settlement model
- account determination
- dimensions
- FX model
- opening/cutover model

## D3 — tax/compliance contract

- TaxContext/TaxResult
- document types
- four-dimensional invoice states
- regulatory evidence store
- provider contract
- signing/certificate contract
- release policy contract

## D4 — inventory future safety

- StockMove model
- location usage
- reservation
- valuation layer
- valuation adjustment
- return cost linkage
- lot/serial extension contract
- landed-cost extension contract
- manufacturing impact review

## D5 — commercial documents

- Quote/order
- PO
- fulfillment/receipt capabilities
- partial delivery/invoice
- returns/RMA
- down payment/progress request
- revisions

## D6 — people/payroll boundaries

- User vs Employee
- HR contract
- payroll country provider
- calculation versioning

## D7 — customization/read model contracts

- stable field IDs
- layouts/views
- rules boundary
- report/read model registration
- public API/event schema strategy

---

# 26. Implementation Order — Code

1. Platform skeleton + architecture tests.
2. Kernel: tenancy/org/iam/audit/registry/messaging/transaction coordinator/platform services.
3. `directory`.
4. `catalog`.
5. `finbase`.
6. `tax`.
7. `pricing`.
8. `ledger`.
9. `subledger`.
10. `invoicing`.
11. `payments`.
12. `accounting` basic UI.
13. `compliance` gateway + first verified country provider.
14. `inventory`.
15. `sales`.
16. `purchasing`.
17. core read models/reporting.
18. `migration` importers needed for first go-live.
19. collections-lite / approvals-lite.
20. `bank_rec`, `cheques`, full `collections` according to market evidence.
21. CRM.
22. HR Core.
23. Leave.
24. Attendance.
25. Payroll + first payroll country provider.
26. Expenses / Fixed Assets.
27. Studio L1.
28. POS / Channels / Subscriptions / Projects / Commissions.
29. Lots/Serials/FIFO/Landed Cost when sector evidence requires.
30. Manufacturing / Quality / Shipping / Maintenance / Service.
31. Intercompany / Consolidation / advanced analytics / regional cells at scale.

**LOCKED:** this is the implementation dependency order, not a promise of commercial release packaging.

---

# 27. Release Order — Product Packaging

## R0 — Internal Architecture Verification Slice

Not sold.

Includes:

- Kernel
- Directory/Catalog/FinBase/Tax/Pricing
- Ledger
- Subledger
- Invoicing
- Payments
- basic Accounting
- first Compliance provider sandbox
- migration test

Purpose:

- prove accounting invariants;
- prove module activation/deactivation;
- prove legal document lifecycle;
- prove multi-tenancy.

## R1 — First Commercial Trade Core — MARKET VALIDATION

Default hypothesis:

- Customers/Parties
- Products/UOM/Pricing
- Inventory AVCO
- Sales
- Purchasing
- Invoicing
- Payments
- Ledger Managed
- AR/AP via Subledger
- collections-lite: credit limit, aging, statements
- launch-country compliance
- Excel migration
- essential financial/operational reporting
- roles/audit

This matches the current trade/distribution product hypothesis better than selling a billing-only ERP.

### Billing-only packaging

Possible because architecture supports:

```text
Invoicing + Payments + Managed Ledger + Compliance
```

But **do not make it the primary product strategy without market evidence**.

## R2

- bank reconciliation
- cheques if validated
- full collections
- CRM
- approvals
- HR core/leave/attendance/payroll if validated
- expenses
- fixed assets
- Studio L1

## R3

- POS
- e-commerce channels
- portal
- subscriptions
- commissions
- projects
- lots/serials/expiry
- FIFO
- landed cost

## R4

- manufacturing
- quality
- shipping
- maintenance/service

## R5

- intercompany/consolidation
- DWH/insights
- advanced search
- regional cells
- selected AI automation

---

# 28. Detailed Module Cards

## 28.1 Platform Kernel

**Type:** always-on kernel  
**Purpose:** security/isolation/platform lifecycle.

### Owns

- Tenant
- LegalEntity
- Branch
- Identity
- Membership
- User
- role/permission registry
- ModuleInstance
- entitlement snapshot
- AuditLog
- Outbox/Inbox
- job metadata
- sequence engine metadata
- file metadata
- settings
- custom-field definitions
- import batch metadata

### Critical rules

- tenant context cannot be accepted from business request payload as authoritative;
- every write has actor + correlation + idempotency context;
- support access is break-glass, time-bounded and audited;
- module permissions are registered by manifests;
- audit is append-only;
- no business workflow semantics in Kernel.

### Build now

- MFA readiness
- RBAC + LegalEntity/Branch scope
- RLS plumbing
- idempotency
- outbox/inbox
- transaction coordinator
- file service
- number engine
- import engine
- basic notification
- settings/i18n
- custom fields metadata

### Later

- SAML/enterprise SSO
- regional cells control plane
- per-tenant encryption keys
- advanced partner console

---

## 28.2 Directory

**Type:** shared domain  
**Purpose:** identity of external people/organizations.

### Owns

- Party
- PartyRole
- Contact
- Address
- PartyIdentifier
- PartyBankReference where appropriate
- tags
- visibility rules

### Workflows

Create → duplicate check → activate role → merge/archive.

### Rules

- Customer and Supplier are roles, not duplicate records.
- Merge creates redirect history.
- Archived Party remains referenceable by historical documents.
- legal/tax identity is snapshotted into finalized documents.
- visibility across Legal Entities is explicit.

### MVP

- company/person
- customer/supplier roles
- contacts
- addresses
- tax/business identifiers
- duplicate detection
- merge
- import/export

### Later

- hierarchy/group structures
- government registry verification
- marketing consent profiles

---

## 28.3 Catalog

**Type:** shared domain  
**Purpose:** define what is sold/purchased/stocked.

### Owns

- ProductTemplate
- ProductVariant
- Category
- Attribute/Value
- UOM category/UOM
- Packaging
- Barcode
- Brand optional

### Locked decisions

- Template + Variant from day one; default can be 1:1.
- Goods vs Service in Catalog.
- Stockability/cost/tracking lives in Inventory.
- Price lives in Pricing.
- Base UOM immutable after transactional use except controlled migration.
- document line stores UOM conversion snapshot.

### MVP

- goods/service
- SKU/barcode
- one/multiple UOM
- packaging
- category
- bilingual names

### Design now / build later

- kit/BOM relation
- variant matrix UI
- lot/serial compatibility
- e-commerce attributes

---

## 28.4 FinBase

**Purpose:** shared finance references.

### Owns

- ExchangeRate
- ExchangeRateSource
- PaymentTerm
- financial rounding policy references

### Rules

- ISO Currency reference is platform data.
- exchange rate used by a transaction is snapshotted.
- PaymentTerm is a definition, not a customer default.

---

## 28.5 Tax

See §15.

### MVP

- VAT rate/effective date
- inclusive/exclusive
- exempt/zero/out-of-scope
- TaxProfile/Position
- deterministic Compute
- tax snapshot

### Later

- excise
- complex reverse charge
- full tax-return filing
- advanced withholding engine after official research

---

## 28.6 Pricing

### Owns

- Pricelist
- PricelistRule
- pricing profile/assignment
- effective dates
- quantity break
- currency
- controlled discounts

### Rule

UI may show “Sale Price” on Product for simplicity, but it writes to the default Pricelist; Product does not own price.

### MVP

- default price
- customer/category price
- quantity/date
- currency
- discount policy

### Later

- rebates
- promotions
- bundles
- contract pricing complexity

---

## 28.7 Ledger

**Type:** hidden financial kernel  
**Hard:** FinBase  
**Scope:** Legal Entity.

### Owns

- CoA
- Account
- Journal
- JournalEntry/Line
- FiscalYear/Period
- PostingRule
- AccountDetermination
- Dimension
- lock dates
- posting fact inbox
- go-live metadata
- ledger mode

### Invariants

- debit = credit
- immutable posted entries
- reversal/adjustment only
- no posting to closed period
- idempotent source posting
- rule version stored with entry
- transaction/functional currency captured

### Fact examples

- CustomerInvoicePosted
- VendorBillPosted
- CreditNotePosted
- PaymentReceived
- PaymentSent
- SettlementApplied
- AdvanceReceived/Applied
- StockValuation
- StockValuationAdjustment
- PayrollRunPosted
- ExpensePosted
- DepreciationPosted
- POSSessionPosted
- ManualJournal

### Does not know

- Sales order implementation
- invoice UI
- inventory screen
- payroll calculation.

---

## 28.8 Subledger

**Type:** hidden financial kernel  
**Hard:** Ledger + Directory + FinBase

### Owns

- PartyFinancialProfile
- OpenItem
- Settlement
- Allocation
- Advance
- WriteOff
- FinancialExposure projection
- credit limit/hold policy

### OpenItem

Direction:

- Receivable
- Payable
- CustomerCredit/Advance
- SupplierAdvance

### Settlement rules

Supports:

- one-to-one
- one payment to multiple invoices
- multiple payments to one invoice
- partial
- credit note allocation
- advance application
- realized FX difference
- write-off/cash discount with authorization
- reverse settlement, never deletion

### Credit check contract

```text
CheckCredit(party, entity, proposedExposure)
→ Allow | Warn | Block | RequireApproval
```

Collections uses this truth; it does not duplicate it.

---

## 28.9 Invoicing

**Hard:** Ledger + Subledger + Directory + Tax + FinBase  
**Optional:** Compliance, Catalog, Approvals, Payments.

### Owns

- Invoice
- InvoiceLine
- CreditNote
- DebitNote
- VendorBill
- VendorCredit
- invoice communication metadata
- regulatory evidence references

### Does not own

- OpenItem
- Settlement
- Payment
- Sales Order
- Stock Movement.

### Workflow

Draft → Finalize → accounting posting + OpenItem → regulatory processing according to Country Provider → correction by credit/debit/reversal policy.

### Requirements

- full/partial/progress/down-payment request
- multi-currency
- tax snapshot
- party/product text snapshots
- links to source documents
- immutable finalized version
- separate state dimensions from §14
- PDF/structured export
- retryable compliance integration

### MVP

- customer invoice
- vendor bill
- credit/debit note
- due schedule
- bilingual template
- authority state model
- import opening documents only where legally/accountingly appropriate

---

## 28.10 Payments

**Hard:** Ledger + Subledger + Directory + FinBase

### Owns

- CashAccount
- BankAccount
- PaymentMethod
- Receipt
- Payout
- InternalTransfer
- payment batch later

### Rules

- payment posts to Ledger;
- payment creates Subledger credit/advance when relevant;
- allocation calls Subledger;
- reversing payment reverses allocations and financial posting safely;
- cash cannot go negative unless explicit policy permits.

### MVP

- receipt
- payout
- transfer
- advance
- multi-currency
- refund
- allocation UI using Subledger

---

## 28.11 Accounting

**Hard:** Ledger  
**Optional:** Subledger, Invoicing, Payments, Inventory, Assets, Budgeting.

### Purpose

Accountant’s operational application, not the data kernel.

### MVP

- CoA UI
- journal UI
- TB/GL/P&L/Balance Sheet
- period close
- reconciliation workbench
- dimensions
- Excel/PDF
- drill-through to source
- Managed→Full transition wizard/checklist

### UX

Keyboard-first Power View; owners get simplified business views.

---

## 28.12 Inventory

**Hard:** Catalog  
**Optional:** Ledger, Approvals.

### Owns

- Warehouse
- Location
- StockMove
- StockMoveLine
- Reservation
- StockBalance projection
- ValuationLayer
- ValuationAdjustment
- StockProfile
- StockCount
- ReorderRule later
- Lot/Serial later

### Core invariants

- every quantity change = StockMove
- Done move immutable
- balance is a projection
- costing per Legal Entity
- negative stock off by default
- return references original flow/cost where possible
- inventory↔GL reconciliation when Ledger active

### MVP

- receipt
- issue
- transfer
- adjustment
- return
- opening stock
- reservation
- on-hand/available/incoming/outgoing
- AVCO initially
- simple count
- historical valuation report

### Design now / build later

- FIFO
- lots/serials/expiry
- landed cost via ValuationAdjustment
- production/transit location use cases
- advanced routes
- consignment.

Do not add future columns merely because a future module might exist unless the chosen model requires them; lock the extension contract, not unnecessary physical fields.

---

## 28.13 Sales

**Hard:** Directory + Catalog + Pricing + Tax  
**Optional:** Inventory, Invoicing, Subledger credit check, Approvals, CRM origin.

### Owns

- Quotation
- SalesOrder
- line
- revision
- SalesReturnAuthorization
- SalesProfile
- salesperson/team/territory basic
- customer product code

### Does not own

- Invoice
- payment
- inventory movement
- credit balance
- GL.

### Flow

Quotation → Confirmed Order → optional reserve/deliver → optional invoice request → close/cancel/return authorization.

### Separate status dimensions

- commercial order status
- fulfillment progress
- invoicing progress

### Fallbacks

- no Inventory → `ManualFulfillment`
- no Invoicing → no invoice command; order management remains
- no Subledger → credit check fallback by commercial configuration (`AlwaysAllow` or simple static policy)
- no Approvals → static role/limit checks

### MVP

- quote/order
- multiple UOM
- price/tax resolution
- discounts
- revisions
- partial fulfillment
- invoice requests
- returns
- mobile/PWA task flow

---

## 28.14 Purchasing

**Hard:** Directory + Catalog + Tax  
**Optional:** Inventory, Invoicing, Approvals.

### Owns

- RFQ
- PO
- PO line
- revision
- PurchaseReturnAuthorization
- SupplierCommercialProfile
- SupplierInfo

### Flow

RFQ → PO → optional receipt request → optional vendor bill request → close/return.

### MVP

- PO
- partial receipts
- 2-way match
- price/quantity variance
- returns
- multi-currency
- service item purchases

### Later

- three-way match
- landed cost allocation
- vendor performance
- blanket order
- advanced requisitions

---

## 28.15 Collections

**Hard:** Subledger  
**Optional:** Sales, Payments, CRM, Notifications.

### Owns

- CollectionCase
- PromiseToPay
- FollowUp
- CollectorAssignment
- DunningPolicy

### Does not own

- OpenItem
- credit balance
- credit limit master
- settlement.

### MVP

- aging views
- overdue queue
- promises
- follow-up
- statements
- daily collection task list

---

## 28.16 Cheques

**Hard:** Payments + Subledger  
**Priority:** MARKET VALIDATION.

### Owns

- Cheque
- status history
- due/present/deposit/clear/return
- endorsement where needed later

Settlement timing is configurable/accounting-reviewed.

Do not include full cheque workflow in R1 unless market evidence confirms material use.

---

## 28.17 Bank Reconciliation

**Hard:** Payments + Ledger.

### Owns

- BankStatement
- StatementLine
- Reconciliation
- MatchRule

### MVP

- file import
- manual/partial match
- unmatch with audit
- reconciliation status

### Later

- bank APIs
- automatic matching
- cash forecasting

---

## 28.18 CRM

**Hard:** Directory  
**Optional:** Sales capability.

### Owns

- Lead
- Opportunity
- PipelineStage
- Activity
- LostReason

### Rule

Lead is raw/prospect data and not automatically a Party.

Qualification creates/links Party.

CRM can call Sales to create a Draft Quotation.

Sales only stores generic `OriginRef`; it never imports CRM implementation contracts.

---

## 28.19 HR Core

**Hard:** Org.

### Owns

- Employee
- Department
- Position
- EmploymentContract
- WorkCalendar
- employment event history
- sensitive identity docs

### Locked

`User ≠ Employee`.

Employee may have an optional User link.

### MVP

- employee
- branch/department/manager
- contract
- work calendar
- employment status
- documents/expiry alerts

---

## 28.20 Leave

**Hard:** HR Core.

### Owns

- LeaveType
- LeavePolicy
- LeaveBalanceLedger
- LeaveRequest
- Accrual

Balance is ledger-like movements, not a mutable total field.

---

## 28.21 Attendance

**Hard:** HR Core  
**Optional:** Leave, Approvals, Payroll.

### Owns

- AttendanceRecord
- shift assignment
- lateness/early departure
- overtime request

### MVP

- manual/import
- shifts
- validation
- lock after payroll consumption
- adjustment audit

### Later

- devices
- GPS/geo-fence only if justified

---

## 28.22 Payroll

**Hard:** HR Core + `PayrollCountryProvider`  
**Optional:** Attendance, Leave, Ledger, Payments, Approvals, Compliance.

### Owns

- SalaryStructure
- Component
- Compensation assignment
- PayrollRun
- Payslip
- Loan/Advance schedule
- payroll calculation version

### Rules

- deterministic calculations only
- Country Provider owns statutory formulas/files/rules
- finalized Payslip immutable
- corrections by adjustment/off-cycle
- golden test vectors required before production
- AI cannot calculate/finalize payroll

---

## 28.23 Expenses

**Hard:** HR Core + Ledger  
**Optional:** Payments, Approvals, Tax.

Flow:

Draft → Manager approval → Finance review → Post → Reimburse.

OCR can draft receipt lines only.

---

## 28.24 Fixed Assets

**Hard:** Ledger  
**Optional:** Purchasing/Invoicing, HR.

Owns:

- Asset
- Category
- DepreciationSchedule
- transfer
- disposal

MVP:

- straight-line
- one additional method only if accounting review requires
- monthly depreciation job
- disposal gain/loss.

---

## 28.25 POS

**Hard:** Catalog + Pricing + Tax + Payments  
**Optional:** Inventory + Invoicing + Compliance + HR cashier.

### Design now

- device idempotency
- POS Session
- receipt document type
- cash close
- regulatory document provider
- inventory issue
- offline conflict model

### Build later

R3 unless validated earlier.

---

## 28.26 Channels / E-commerce

**Hard:** Sales + Catalog  
**Optional:** Inventory, Payments, Invoicing.

Owns:

- ChannelConnection
- ExternalRef mapping
- sync job/checkpoint
- mapping rules

Imports external orders idempotently.

---

## 28.27 Subscriptions

**Hard:** Invoicing + Catalog + Pricing.

Owns:

- Contract
- RecurrenceSchedule
- Renewal
- usage records later

Produces InvoiceRequest.

---

## 28.28 Manufacturing

**Hard:** Inventory + Catalog  
**Optional:** Purchasing, Ledger, HR, Quality.

Design now:

- production location usage
- BOM per Variant
- valuation adjustment
- WIP fact contract
- scrap
- component/finished-goods movement semantics

Build later:

- work centers
- APS
- shop floor
- IoT
- full MRP planning until sector need exists.

---

## 28.29 Projects

**Hard:** Directory  
**Optional:** HR, Invoicing, Sales, Expenses.

Owns project/task/time records. Billing via Invoicing capability.

---

## 28.30 Service / Maintenance

Service owns request/work order/SLA.

Optional:

- Inventory spare parts
- HR technician
- Invoicing billing
- Sales contract

Maintenance may remain separate for internal assets/equipment if market requires; do not prematurely merge until domain research.

---

## 28.31 Quality / Shipping

### Quality

Hard Inventory; later Manufacturing.

### Shipping

Hard Inventory or Sales fulfillment capability depending final domain research.

They are not in first commercial ERP unless sector demands.

---

## 28.32 Approvals

**Hard:** IAM.

Provides:

```text
IApprovalGuard.Evaluate(context)
→ Allow | Deny | RequireApproval
```

State machines stay code-owned by business modules.

Policy engine only decides authorization path.

No arbitrary JS/SQL/loops.

---

## 28.33 Studio

L1 first:

- custom fields
- labels
- layouts
- field visibility
- defaults
- role views
- print templates
- saved filters
- dashboard composition

L2 later:

- constrained rules/notifications.

All customization uses stable Field IDs and versioned config.

Never:

- arbitrary code
- SQL
- per-customer fork
- core schema mutation outside approved metadata.

---

## 28.34 Reporting

Platform report service + owner module Read Models.

Operational truth remains in modules.

Cross-module analytics uses projections.

PostgreSQL first.

Add read replica before DWH.

No OpenSearch/ClickHouse until measured need.

---

## 28.35 Migration

Must exist before first production customer.

Framework:

Upload → Mapping → Validate → Data Quality → Preview → Dry Run → Import → Reconcile → Audit.

Dependency order:

1. Org/users
2. CoA if finance active
3. Parties
4. Catalog/UOM
5. Pricing
6. Opening stock
7. Ledger opening
8. Subledger open items/advances
9. Employees/balances
10. Assets
11. optional open commercial orders.

---

## 28.36 Compliance Gateway & Country Providers

### Generic gateway owns

- submission job
- status tracking
- evidence store
- certificate/credential references
- retry/DLQ
- monitoring
- adapter resolution.

### Country Provider supplies

- document mapping
- release policy
- signing requirement
- authority endpoint behavior
- official validation
- retention/legal metadata
- effective version.

No legal constant belongs in Invoicing Core.

---

## 28.37 Integrations

- REST public API `/v1`
- OAuth/API keys
- scopes
- rate limits
- idempotency
- signed webhooks
- retry/replay
- connector credential vault
- ExternalRef map
- checkpointed jobs

External input always enters through application Commands, never database writes.

---

## 28.38 AI Gateway

AI → approved Tool → permission check as user/service → Command/Query → Domain → Audit.

Risk classes:

- read
- draft
- write-low
- write-sensitive

Default policy:

- financial/tax/payroll/legal actions = no autonomous finalization.
- AI can draft, explain, classify, match, detect anomalies.

---

# 29. Quote-to-Cash — Final Responsibility Map

```text
[CRM optional]
Lead → Opportunity
        │
        ▼ optional command
[Sales]
Quotation → SalesOrder
              │
              ├─ CreditCheck ───────────► [Subledger]
              ├─ ApprovalGuard ─────────► [Approvals]
              ├─ FulfillmentRequest ────► [Inventory optional]
              │                              │
              │                              └─ StockMove + Valuation → [Ledger optional]
              └─ InvoiceRequest ─────────► [Invoicing optional]
                                             │
                                             ├─ Tax.Compute
                                             ├─ Ledger.Post
                                             ├─ Subledger.CreateOpenItem
                                             └─ Event → [Compliance optional]
[Payments]
Receipt → Ledger.Post + Subledger.CreateCredit/Advance
              │
              └─ Allocate ───────────────► [Subledger Settlement]

[BankRec optional]
Bank line ↔ Payment
```

Sales does not own downstream financial/stock data.

---

# 30. Procure-to-Pay — Final Responsibility Map

```text
[Purchasing]
RFQ → PO
       │
       ├─ ReceiptRequest ─────► [Inventory optional]
       │                           └─ Stock/Valuation
       └─ VendorBillRequest ──► [Invoicing optional]
                                    ├─ Ledger.Post
                                    └─ Subledger Payable OpenItem

[Payments]
Payout → Ledger + Subledger credit
           └─ Allocation → Settlement

[BankRec]
Statement match
```

---

# 31. Returns & Corrections

## Sales return

Sales owns Return Authorization.

Inventory owns physical reverse flow.

Invoicing owns Credit/Debit document.

Subledger owns financial offset/settlement.

Payments owns refund.

## Purchasing return

Purchasing owns Return Authorization.

Inventory owns physical issue back to supplier.

Invoicing owns Vendor Credit/Debit.

Subledger owns financial offset.

## Rule

No module silently mutates a historical document belonging to another module.

---

# 32. Multi-tenancy

## Model

Shared PostgreSQL deployment, module schemas, `tenant_id` on tenant-owned tables, RLS, composite tenant keys where useful, cell-ready routing later.

## Guardrails

1. tenant context from trusted auth/middleware.
2. `SET LOCAL` within DB transaction.
3. RLS on all tenant tables.
4. tenant namespacing on caches/files/jobs/search.
5. background jobs include tenant context.
6. cross-tenant fuzz/security tests.
7. support access audited.
8. tenant export/offboarding capability.
9. backups/PITR + restore drills.

## Cell readiness

Store Tenant Directory with `cell_id` from day one.

Do not build multi-cell control plane until needed.

---

# 33. Multi-company / Legal Entities

Day 1 design:

- Tenant → LegalEntity → Branch
- shared identities/users
- canonical Party/Product with visibility scopes
- Entity-specific commercial/financial profiles
- CoA per Entity
- tax/country provider per Entity
- module activation per Entity where relevant
- numbering per Entity
- currency/accounting go-live per Entity

Later:

- mirrored intercompany trade
- eliminations
- consolidation
- shared services accounting

---

# 34. Security, Audit & Separation of Duties

## Critical data

Encrypt/mask as appropriate:

- national IDs
- residency/passport
- bank details
- salary
- certificates/secrets.

## Audit critical actions

- permissions
- price override
- credit override
- stock adjustment
- negative stock override
- invoice finalize/reverse
- settlement/unsettlement
- payment reverse
- journal/reopen period
- payroll finalization
- certificate changes
- support access.

## Separation of duties

Configurable rule examples:

- creator cannot approve high-value transaction.
- payroll editor cannot finalize same run.
- bank account editor cannot approve payment.
- period re-open requires separate role.

---

# 35. Customization Contract

## Level 1 — Configuration

- custom fields
- layouts
- labels
- role views
- required/hidden
- defaults
- templates
- saved filters
- dashboards

## Level 2 — Rules

- conditional approval
- notification
- simple field action
- create draft
- block action

Limits:

- no loops
- no arbitrary script
- execution budget
- audit
- versioning
- dry run.

## Level 3 — APIs/Webhooks

External custom behavior.

## Level 4 — Official Module

Repeated domain need becomes a supported product module.

---

# 36. Reporting Architecture

## Operational

Owned by source module.

Examples:

- Inventory valuation → Inventory.
- Sales performance → Sales.
- Aging → Subledger/Collections view.
- Payroll reports → Payroll.

## Financial

Ledger/Accounting.

## Cross-module

Projection/read model with explicit owner.

Example:

```text
Sales Margin Projection
  order revenue snapshot
+ inventory COGS
```

No live cross-schema reporting joins as the default architecture.

---

# 37. Compliance — Verified Baseline vs Open Legal Questions

## 37.1 Egypt — official facts confirmed

**F — Egyptian Tax Authority official SDK:**

- ETA publishes an ERP/POS integration SDK.
- ERP/POS systems obtain API access credentials.
- integration uses access tokens/APIs.
- taxpayers obtain an X.509 eSeal certificate configured for document signing.
- eInvoice/eReceipt integration is an official API-based integration model.

**F — current ETA VAT registration threshold page:**

- mandatory VAT registration threshold shown by ETA is **EGP 500,000**.
- importers/exporters/distribution agents have special registration treatment independent of normal threshold under the cited ETA rules.

### Not locked as architecture fact

The VAT threshold must **not** be used as the same thing as “who is obligated to use every e-invoicing/e-receipt flow.” Applicability of specific ETA systems must be checked against current official mandates by taxpayer/activity/document type.

### LEGAL VERIFICATION before Egypt production

- exact eInvoice/eReceipt applicability for target ICP;
- signing deployment options and custody model;
- cancellation/credit-note rules;
- tax rounding;
- withholding representation;
- document retention;
- rate limits/offline/failure windows;
- B2C receipt requirements.

---

## 37.2 Saudi Arabia — official facts confirmed

**F — ZATCA announcement dated 24 July 2026:**

Wave 25 includes taxpayers whose VAT-subject revenues exceeded **SAR 187,500** during 2022, 2023, 2024, or 2025, with integration with Fatoora required no later than **1 February 2027** for targeted taxpayers.

Architecture implication:

- versioned country provider;
- onboarding/certificate/solution-unit readiness;
- structured invoice/required fields;
- regulatory state/evidence;
- no hard-coded wave threshold in core code.

### LEGAL VERIFICATION before production

- standard vs simplified release/reporting semantics;
- cryptographic/CSID onboarding details for chosen implementation;
- current technical specifications;
- data retention;
- exact QR/hash/ICV rules at implementation date.

---

## 37.3 UAE — official facts confirmed

**F — UAE Ministry of Finance:**

- eInvoicing is based on structured electronic invoice exchange.
- Accredited Service Providers (ASPs) are part of the official model.
- businesses with annual revenue ≥ AED 50m are scheduled for mandatory implementation from **1 January 2027**.
- businesses below AED 50m are scheduled from **1 July 2027**.
- Government Entities are scheduled from **1 October 2027**.
- 2026 amendment moved the ASP appointment date for the ≥AED 50m group to **30 October 2026** while retaining the 1 January 2027 implementation date.

Architecture implication:

- UAE provider is an ASP integration adapter, not direct assumption of government endpoint ownership;
- provider credentials and onboarding must be replaceable/versioned.

---

# 38. What Was Missing and Is Now Required

1. Subledger boundary.
2. stable CreditProfile owner.
3. Module Go-Live contract.
4. in-process transaction coordinator.
5. master-data visibility scope.
6. module activation scope per Legal Entity.
7. regulatory release-policy contract.
8. separate invoice state dimensions.
9. provider-based country-pack architecture.
10. managed-to-full accounting transition.
11. explicit support/offboarding/tenant export.
12. contract versioning and deprecation.
13. data retention policy reference.
14. large-import reconciliation.
15. module deactivation impact report.
16. open-document transition policy.
17. architecture scenario tests in CI.
18. public API idempotency requirement.
19. financial reconciliation invariants.
20. Design-now impact review for manufacturing/POS/intercompany.

---

# 39. What Must Be Removed or Deferred

Do not build early:

- Microservices
- Kafka
- Kubernetes-specific architecture
- full plugin marketplace/runtime
- arbitrary customer code
- full drag/drop page builder
- generic workflow programming language
- native mobile apps
- multi-cell control plane
- ClickHouse/DWH
- OpenSearch
- autonomous AI agents
- manufacturing
- enterprise consolidation
- complex multi-GAAP
- advanced WMS
- APS
- IoT
- advanced treasury
- marketing automation
- free-form SQL report builder.

---

# 40. Global Non-Negotiable Invariants

1. No tenant can reference another tenant’s row.
2. Tenant context is trusted infrastructure context.
3. Posted journal entry is immutable.
4. Journal entry debits equal credits.
5. Completed StockMove is immutable.
6. Every inventory quantity change has a StockMove.
7. Finalized invoice snapshots identity/tax/price data.
8. Party/Product master change cannot rewrite historical documents.
9. OpenItem balance changes only through Subledger-approved operations.
10. Settlement reversal is explicit and audited.
11. No business module writes another business module’s tables.
12. No hard dependency cycle.
13. No customer code fork.
14. No legal-country rule in global business core unless universally true.
15. No AI direct database access.
16. Deactivation never deletes historical financial/stock/payroll records.
17. Every stateful module has Go-Live rules.
18. Every external write is idempotent or has duplicate protection.
19. Every integration credential resides in a secrets/certificate vault abstraction.
20. Every critical override is auditable.
21. User ≠ Employee.
22. Customer/Supplier are Party roles.
23. Product cost does not belong to Catalog.
24. Product selling price does not belong to Product identity.
25. Sales does not know GL account IDs.
26. Inventory does not know SalesOrder implementation.
27. Ledger does not know Invoice implementation.
28. Country providers can evolve without rewriting finalized document history.
29. Release packaging never changes domain ownership.
30. Entitlement/UI hiding is not authorization.

---

# 41. Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Overbuilding foundation | slows MVP | dependency-activated shared domains |
| Circular module contracts | long-term coupling | hard DAG + architecture tests |
| Managed ledger differs from accountant reality | accounting cutover failure | Managed→Full reconciliation gate |
| Wrong OpenItem ownership | payments/collections coupling | Subledger kernel |
| Country-law change | compliance outage | versioned providers + evidence store |
| Product model too simple | POS/MRP rework | Template/Variant/UOM design now |
| Inventory valuation flaw | trust/accounting failure | immutable moves/layers + reconciliation |
| Customer-specific customization | upgrade collapse | L1–L4 model |
| Cross-tenant leak | catastrophic | RLS + context + tests |
| ERP becomes too complex | adoption failure | Role=Experience + minimum input |
| Billing-only product enters commodity market | weak wedge | release order marked MARKET VALIDATION |
| AVCO wrong for initial sector | costing mismatch | MARKET VALIDATION before locking valuation policy for launch |
| Cheques overbuilt | wasted scope | MARKET VALIDATION |
| Payroll legal error | high legal risk | Country Provider + specialist sign-off |
| Async misuse for accounting | inconsistent state | same transaction coordinator |
| Premature scale architecture | cost/ops burden | Postgres + worker first |

---

# 42. Red-Team Scenarios

## Scenario 1 — Sales only

Works.

Hard domains: Directory/Catalog/Pricing/Tax.

No Inventory:
- manual fulfillment.

No Invoicing:
- quote/order only.

No Accounting:
- no financial posting because no legal financial document is created.

Commercial packaging may bundle Invoicing, but architecture does not require it.

---

## Scenario 2 — Sales + Inventory

Works.

- order reserve/delivery uses Inventory.
- no invoice/OpenItem unless Invoicing is active.
- inventory cost remains operational.
- Ledger is only required if financial valuation posting is enabled by chosen package/policy.

---

## Scenario 3 — Sales + Invoicing + Payments, no Accounting UI

Works.

Auto:
- Ledger Managed
- Subledger

Flow:
- invoice posts
- receivable OpenItem
- receipt/payment
- settlement
- no accountant UI.

---

## Scenario 4 — Add Accounting after 2 years

Works through Managed→Full transition.

No transaction reconstruction.

Requires reconciliation/config sign-off.

---

## Scenario 5 — Activate Inventory after Sales history exists

No backfill by default.

Use opening stock + explicit open-order transition.

---

## Scenario 6 — Disable Inventory with open orders

Impact report blocks or requires:

- complete/cancel stock docs;
- transition unfulfilled order lines to manual fulfillment;
- close inventory/GL reconciliation.

History becomes read-only.

---

## Scenario 7 — CRM disabled

Party and Sales remain untouched.

Lead/opportunity history is read-only/exportable.

Sales `OriginRef` label snapshot remains displayable.

---

## Scenario 8 — Collections disabled

Credit balance/limit stays because Subledger owns it.

Only follow-up/dunning workflow disappears.

---

## Scenario 9 — Invoicing disabled while Payments remains

Allowed only after impact check.

Payments can still record payments/advances through Subledger/Ledger, but cannot allocate to new Invoice documents.

Historical invoice references remain.

---

## Scenario 10 — Country changes for a Legal Entity

Not treated as a trivial setting edit.

Requires a controlled migration/go-live project:

- legal identifiers;
- tax;
- CoA implications;
- compliance provider;
- document numbering;
- retention.

Country cannot be casually changed after transactions.

---

## Scenario 11 — Tenant with Egypt + Saudi Legal Entities

Works.

Each entity:

- own CoA
- country provider
- tax config
- compliance credentials
- module instance state
- numbering.

Shared Party/Product only according to visibility policies.

---

## Scenario 12 — 10,000 tenants, most use 3 modules

Works conceptually:

- one deployed codebase;
- lazy domain provisioning;
- entitlement capability set;
- RLS;
- shared Postgres until measured cell need;
- job fairness/quota controls.

No per-tenant schemas.

---

## Scenario 13 — Duplicate external request

Idempotency key prevents duplicate order/payment/invoice.

Outbox/Inbox prevents duplicated integration effects.

---

## Scenario 14 — Government API outage

Internal finalized document may exist according to country rules, but RegulatoryState remains Pending/FailedRetryable.

Release behavior is decided by Country Provider.

Retry is external job, not DB transaction.

---

## Scenario 15 — Customer becomes supplier

Directory adds Supplier role.

No duplicate Party.

Separate Sales/Purchasing/Financial profiles are created lazily.

---

# 43. Testing Strategy Required by Architecture

## Architecture tests

- hard graph acyclic
- no implementation namespace imports across modules
- no cross-business-schema FK
- no direct table access
- every optional dependency has fallback
- every module manifest valid
- every stateful module has Go-Live/deactivation metadata

## Contract tests

Provider/consumer compatibility.

## Scenario tests

At least:

- activation combinations
- deactivation combinations
- tenant isolation
- multi-entity
- module added after history
- partial fulfillment/invoicing/payment
- returns/reversals
- duplicate commands
- integration retry
- closed fiscal period
- negative stock attempt
- cross-tenant fuzz.

## Golden tests

Required for:

- tax calculations
- payroll country calculations
- accounting posting facts
- inventory costing.

---

# 44. Module Requirements Research Sequence

Architecture is now sufficiently defined to stop redesigning the whole ERP in every module study.

Research order:

1. Platform Kernel & Module System
2. Directory / Party
3. Catalog / Product / UOM
4. FinBase + Pricing + Tax
5. Ledger
6. Subledger
7. Invoicing
8. Payments
9. Inventory
10. Sales + Collections-lite
11. Purchasing
12. Compliance for launch country
13. Accounting UX/reporting
14. Migration
15. Approvals
16. CRM
17. HR Core
18. Leave + Attendance
19. Payroll
20. other modules according to product evidence

Each Research Pack must cover:

- personas
- real workflows
- required fields
- states
- business rules
- edge cases
- permissions
- audit
- imports
- reports
- country needs
- competitor patterns
- UX
- mobile
- APIs
- customization
- MVP/Later/Do-not-build
- evidence status
- contract changes proposed.

**Rule:** A module study may refine internals but must submit an ADR if it proposes changing a locked cross-module boundary.

---

# 45. Build Gates

## Before production code

Must close:

1. module manifest schema;
2. tenancy/RLS PoC;
3. transaction coordinator PoC;
4. Party/Variant identifier strategy;
5. Money/rounding;
6. Ledger Fact contract;
7. Subledger model;
8. tax context;
9. invoice state model;
10. Go-Live contract.

## Before first commercial pilot

Must close:

1. launch country compliance with official test environment;
2. accountant sign-off on posting rules;
3. inventory costing sign-off;
4. migration rehearsal;
5. tenant isolation security test;
6. backup/restore drill;
7. real data reconciliation;
8. role/permission review.

---

# 46. Source Register — Official/Primary Sources Used in v3

Accessed: **2026-10-05**

## Egypt

1. Egyptian Tax Authority — eInvoicing & eReceipt SDK:
   - `https://sdk.invoicing.eta.gov.eg/start/`
   - confirms ERP/POS registration, API credentials, tokens/APIs, and X.509 eSeal certificate integration.

2. Egyptian Tax Authority — Mandatory VAT registration threshold:
   - `https://www.eta.gov.eg/ar/node/1379`
   - current ETA page states EGP 500,000 mandatory VAT registration threshold, with special treatment for importers/exporters/distribution agents.

3. Egyptian Tax Authority — Electronic invoice guides:
   - `https://eta.gov.eg/en/content/guides-dealing-electronic-invoices-system`

## Saudi Arabia

4. ZATCA — Wave 25 Integration Phase announcement, 24 July 2026:
   - `https://www.zatca.gov.sa/en/MediaCenter/News/Pages/Wave25-E-invoicing.aspx`
   - official threshold and integration date used in this document.

## UAE

5. UAE Ministry of Finance — eInvoicing portal:
   - `https://mof.gov.ae/en/about-us/initiatives/einvoicing/`

6. UAE Ministry of Finance — Ministerial Decision No. 244 of 2025 and amendment No. 66 of 2026.
   - official implementation timelines and amended ASP appointment date.

7. UAE Ministry of Finance — Accredited Service Providers:
   - `https://mof.gov.ae/en/about-us/initiatives/einvoicing/einvoicing-accredited-service-providers-asps/`

## Architecture reference

8. Microsoft Learn — Business Central Module Architecture:
   - `https://learn.microsoft.com/dynamics365/business-central/dev-itpro/developer/devenv-blueprint`
   - supports the principle of module facades, internal encapsulation, explicit dependencies and extensibility boundaries.

9. Microsoft Learn — Business Central Extensibility Overview:
   - `https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/developer/devenv-extensibility-overview`

These competitor references are architecture inspiration only, not a requirement to copy Business Central.

---

# 47. Market Validation Items — Deliberately Not Locked

The architecture supports these choices, but the market should decide priority:

- Egypt vs Saudi vs UAE first.
- wholesale/distribution vs e-commerce comparison cohort.
- AVCO vs FIFO launch priority.
- cheques in R1/R2.
- lots/serials/expiry timing.
- landed cost timing.
- Billing-only package as a wedge.
- HR/Payroll release timing.
- exact pricing model.
- accountant channel.
- mobile native app need.
- advanced approval/workflow scope.

We do not block architecture work waiting for 35 interviews, but we also do not relabel hypotheses as facts.

---

# 48. Legal Verification Items — Must Never Be Guessed

Before production per country:

- current e-invoicing applicability;
- document type and lifecycle;
- official schema/API version;
- signing/certificate custody;
- cancellation/credit-note rules;
- rounding;
- tax code mapping;
- retention;
- privacy/data-residency obligations;
- payroll statutory formulas;
- government file formats;
- statutory deadlines.

Create a `LegalDecisionRecord` with:

```text
country
topic
official_source
source_version/date
effective_date
implementation_decision
verified_by
review_due
```

---

# 49. Final Dependency Diagram

```text
                              ┌──────────────┐
                              │ PLATFORM K0  │
                              └──────┬───────┘
                                     │
                ┌────────────────────┼────────────────────┐
                ▼                    ▼                    ▼
           directory              catalog              finbase
                │                    │                    │
                │                    ├────► pricing       ├────► tax
                │                    │                    │
                └──────────────┐     │      ┌─────────────┘
                               ▼     ▼      ▼
                              ledger       sales
                                │           │ \
                                ▼           │  \ optional
                            subledger       │   ├────► inventory
                             │   │          │   └────► invoicing
                             │   │          │
                    ┌────────┘   └──────┐   │
                    ▼                   ▼   │
                invoicing            payments
                    │                   │
                    │                   ├────► bank_rec
                    │                   └────► cheques
                    │
                    └────► compliance provider (optional)

subledger ─────► collections
ledger ────────► accounting
catalog ───────► inventory ─────► manufacturing
directory ─────► crm ──optional──► sales
org ───────────► hr_core ─► leave / attendance ─► payroll(country provider)
ledger + hr_core ─► expenses
ledger ─────────► fixed_assets
```

Arrows mean consumer → provider/dependency conceptually; optional arrows are resolved by capability rather than implementation coupling.

---

# 50. Final Build Sequence

## Phase A — Architecture Platform

1. Platform skeleton
2. Kernel
3. module registry/manifests
4. tenancy/RLS
5. transaction coordinator
6. audit/idempotency/outbox
7. entitlement snapshot
8. custom fields/import/report foundations

## Phase B — Shared Domain

9. Directory
10. Catalog
11. FinBase
12. Tax
13. Pricing

## Phase C — Financial Integrity

14. Ledger
15. Subledger
16. Invoicing
17. Payments
18. Accounting basic
19. Compliance gateway/provider sandbox

## Phase D — Trade ERP

20. Inventory
21. Sales
22. Purchasing
23. Collections-lite
24. core reporting
25. migration
26. launch-country production compliance

## Phase E — Expansion

27. approvals
28. bank rec / cheques if validated
29. CRM
30. HR / leave / attendance / payroll
31. expenses/assets
32. Studio

## Phase F — Commerce / Industry

33. POS/channels/subscriptions/projects/commissions
34. lots/serials/FIFO/landed cost by evidence
35. manufacturing/quality/shipping/service/maintenance

## Phase G — Scale

36. intercompany/consolidation
37. read replicas / DWH if measured
38. advanced search if measured
39. regional cells
40. advanced AI

---

# 51. FINAL ARCHITECT VERDICT

## Is the architecture ready for Module Requirements Research?

# **READY**

With the following boundaries locked:

- Module ownership model
- Hard dependency DAG
- Ledger/Subledger split
- Invoicing boundary
- Payments boundary
- Party/Product ownership
- module activation/deactivation model
- legal entity/country provider model
- transaction consistency model
- Go-Live contract
- customization strategy
- multi-tenancy model

## Is it ready for blind production coding of the whole ERP?

# **NO — intentionally**

Production implementation must proceed module-by-module through Research Packs and gates.

That is not architectural uncertainty; it is disciplined product engineering.

## What is allowed to change later without reopening the whole blueprint?

- internal fields
- screen layouts
- reports
- optional features
- workflows within module boundary
- release priority
- country-specific details
- market-specific features

## What requires a formal ADR and architecture review?

- moving entity ownership
- adding hard dependency
- cross-module database access
- changing financial kernel responsibility
- changing tenant isolation
- adding arbitrary extension code
- changing legal entity scoping
- changing immutable historical rules
- making a hidden kernel purchasable or customer-specific.

---

# 52. Next Official Step

The project should now stop producing additional “whole ERP architecture” drafts.

Start:

# **Module Requirements Research Pack 01 — Platform Kernel & Module System**

Then proceed in the sequence in §44.

The v3 file is the architecture baseline against which every later Module Pack is reviewed.

---

**End — ERP SaaS Master Module & Dependency Blueprint v3**
