# ERP SaaS — Production-Ready Technology Stack & Repository Architecture

**Document status:** APPROVED ENGINEERING BASELINE — v1.0  
**Date:** 2026-10-05  
**Purpose:** Define the technology stack, repository structure, module template, messaging architecture, engineering rules, production infrastructure, security, testing, observability, CI/CD, and operational standards **before implementation of the first ERP module**.

> This document is an engineering baseline. Business-module requirements remain governed by the ERP Master Module & Dependency Blueprint and by the detailed Requirements Research Pack for each module.

---

# 0. Executive Decision

The ERP will be built as a:

> **Multi-tenant Modular Monolith with a Message Broker from Day 1**

with:

- One codebase.
- Strong module boundaries.
- PostgreSQL as the system of record.
- RabbitMQ for asynchronous messaging.
- MassTransit as the .NET messaging abstraction.
- Transactional Outbox / Consumer Inbox from Day 1.
- Synchronous in-process contracts for invariants requiring an immediate answer.
- Integration Events through RabbitMQ after database commit.
- No microservices initially.
- Architecture that permits selective service extraction later without redesigning business contracts.

The objective is not to make the system "distributed" early.

The objective is to make it:

- Correct.
- Modular.
- Observable.
- Recoverable.
- Secure.
- Testable.
- Deployable.
- Upgradeable.
- Ready to scale when evidence requires it.

---

# 1. Locked Technology Stack

## 1.1 Backend

| Area | Decision |
|---|---|
| Language | C# |
| Runtime | .NET 10 LTS |
| Web Framework | ASP.NET Core 10 |
| Data Access | EF Core 10 |
| PostgreSQL Provider | Npgsql |
| Hot-path SQL | Dapper / raw SQL only when justified by measurement |
| Validation | FluentValidation |
| Date/Time | NodaTime |
| API | REST/JSON + OpenAPI |
| Authentication store | ASP.NET Core Identity |
| OAuth2/OIDC | OpenIddict |
| Background consumers | .NET Worker Service |
| Message abstraction | MassTransit |
| Message Broker | RabbitMQ 4.x supported release |
| Scheduled jobs | Quartz.NET + PostgreSQL persistent store |
| Object Storage | S3-compatible |
| Observability | OpenTelemetry |
| Structured logging | Serilog |
| Health checks | ASP.NET Core Health Checks |
| Unit tests | xUnit |
| Integration tests | xUnit + Testcontainers |
| External integration tests | WireMock.Net |
| Architecture tests | NetArchTest or ArchUnitNET |

### Version policy

Do not hard-code the project forever to the patch releases named in this document.

Rules:

1. Pin exact versions in source control.
2. Use supported stable releases only.
3. Patch updates are routine maintenance.
4. Minor/major updates require CI compatibility tests.
5. Preview/Beta/RC versions are forbidden in production unless an ADR explicitly approves them.

At the date of this document:

- .NET 10 is the active LTS release.
- EF Core 10 is the aligned LTS ORM.
- PostgreSQL 18 is stable; PostgreSQL 19 is still pre-GA and is not approved.
- RabbitMQ 4.x is the approved broker family.

---

## 1.2 Frontend

| Area | Decision |
|---|---|
| UI Framework | React 19.3 |
| Language | TypeScript |
| Build tool | Vite 8.1 |
| Package manager | pnpm, exact version pinned via Corepack |
| Node.js | Node 24 LTS |
| Routing | React Router |
| Server state | TanStack Query v5 |
| Forms | React Hook Form |
| Validation | Zod |
| UI primitives | Headless UI primitives (Spike selection) |
| Complex ERP grids | Platform DataGrid wrapper (Spike selection) |
| Local shared UI state | Zustand only when justified |
| Internationalization | i18next + react-i18next |
| Unit/component tests | Vitest + React Testing Library |
| E2E | Playwright |
| API client | Generated from OpenAPI |
| Error tracking | Sentry or approved equivalent |

### Important frontend rules

- Redux is **not** the default.
- Server state does not go into Zustand.
- API DTO types are generated, not manually duplicated.
- Business calculations are not implemented independently in the browser.
- The UI never decides permissions by itself; backend authorization is authoritative.
- Reusable UI primitives and components are brand-neutral and token-driven.
- Paid/commercial dependencies are strictly forbidden for core functionality (Free-Only Core Dependency Rule).

---

# 2. Why RabbitMQ + MassTransit from Day 1

The ERP will use a Message Broker from the beginning.

The selected architecture is:

```text
Application Transaction
        │
        ├── Database changes
        ├── Domain changes
        └── Transactional Outbox row
                │
                │ COMMIT
                ▼
          Outbox Dispatcher
                │
                ▼
            RabbitMQ
                │
        ┌───────┼────────┐
        ▼       ▼        ▼
    Consumer Consumer Consumer
        │
        └── Inbox / Idempotency
```

## Why RabbitMQ

RabbitMQ matches our primary needs:

- Commands.
- Integration Events.
- Routing.
- Consumer queues.
- Retry/redelivery.
- Dead-letter/error queues.
- Work distribution.
- Reliable asynchronous processing.
- Operationally simpler than event-stream infrastructure for the initial ERP.

Kafka is **not** selected as the default because the system initially needs transactional business messaging more than a large distributed event log.

Kafka may be considered later for:

- High-volume event streaming.
- CDC pipelines.
- Large analytics/event platforms.

It is not required to build the ERP correctly.

---

# 3. Message Broker Does Not Mean "Everything Async"

This rule is non-negotiable.

## 3.1 Use synchronous application contracts when

The caller needs the answer before it may proceed.

Examples:

- Reserve inventory before confirming an order.
- Credit check.
- Approval guard.
- Validate a fiscal period.
- Post financial effects that must be atomic with the document.
- Validate module entitlement.
- Compute tax.
- Resolve price.

Example:

```text
Sales.ConfirmOrder()
    │
    ├── Pricing.Resolve()
    ├── Tax.Compute()
    ├── Credit.Check()
    └── Inventory.Reserve()
```

If reservation fails, order confirmation fails.

RabbitMQ is not the right tool for that immediate invariant.

---

## 3.2 Use RabbitMQ when

The action can happen after the source transaction commits.

Examples:

- Invoice posted notification.
- E-invoice government submission.
- Email/WhatsApp.
- Search projection.
- Analytics.
- CRM activity update.
- External webhooks.
- Rebuilding read models.
- Document processing.
- AI/OCR work.
- Long-running integrations.

Example:

```text
Invoice transaction commits
       ↓
Outbox
       ↓
RabbitMQ: InvoicePosted.v1
       ├── Compliance consumer
       ├── Notification consumer
       ├── Reporting projection
       └── Integration webhook consumer
```

---

# 4. Transactional Outbox Is Mandatory

Never do this:

```text
Save DB
Publish RabbitMQ message
```

as two independent operations.

Failure between the two creates inconsistent state.

Instead:

```text
BEGIN DB TRANSACTION

Update domain data
Insert outbox message

COMMIT

Outbox dispatcher publishes later
```

We will use **MassTransit + EF Core transactional outbox capabilities** wherever they fit the module transaction model.

Every message consumer must also be idempotent.

---

# 5. Inbox / Idempotency Is Mandatory

RabbitMQ delivery should be treated as:

> At-least-once delivery.

A consumer may receive the same message again.

Therefore:

```text
Message ID
   ↓
Inbox check
   ↓
Already processed?
   ├── Yes → return success
   └── No  → execute + record
```

Every externally-triggered write should also support an idempotency key.

Examples:

- Public API POST.
- Payment callback.
- E-commerce order import.
- Government callback.
- Webhook.
- AI Tool call.
- Broker command.

---

# 6. Message Envelope Standard

All integration messages must contain, either in the message or standard headers:

```text
message_id
message_type
schema_version

tenant_id
legal_entity_id? 
branch_id?

correlation_id
causation_id

actor_id?
actor_type

occurred_at_utc
trace_id

source_module
source_ref?
```

## Rules

- Never put credentials in messages.
- Avoid unnecessary PII.
- Avoid huge payloads.
- Prefer IDs + immutable business snapshot required by the consumer.
- Events describe something that **already happened**.
- Commands request that one owner perform an action.
- Event names use past tense.
- Message schema changes are backward-compatible where possible.

Examples:

```text
sales.order.confirmed.v1
invoicing.invoice.posted.v1
inventory.stock.moved.v1
payments.payment.received.v1
payroll.run.finalized.v1
```

---

# 7. RabbitMQ Production Rules

Production RabbitMQ must use:

- Managed RabbitMQ where practical, otherwise a production cluster.
- TLS.
- Dedicated credentials per application/environment.
- Separate vhost for each environment.
- Quorum queues for important durable workloads.
- Durable queues.
- Persistent messages where required.
- Least-privilege accounts.
- Explicit queue topology.
- Prefetch configuration.
- Retry and delayed redelivery policy.
- Error/dead-letter queues.
- Queue-depth monitoring.
- Consumer-lag monitoring.
- Connection/channel monitoring.
- Broker disk/memory alarms.
- Regular supported-version upgrades.

## Environments

```text
/erp-dev
/erp-staging
/erp-prod
```

Never share production RabbitMQ resources with test environments.

---

# 8. Retry Policy

Do not retry every exception.

## Immediate retry

Only for very short transient failures.

Example:

```text
3 retries
100ms / 300ms / 1s
```

## Redelivery

For failures that may recover later:

```text
30 seconds
2 minutes
10 minutes
30 minutes
```

Exact policy is consumer-specific.

## Do not endlessly retry

Examples:

- Validation failure.
- Missing required legal field.
- Unsupported currency.
- Invalid customer state.

These go to a clear error path, not an infinite retry loop.

---

# 9. Error Queue / Dead Letter Operations

Every production consumer must have:

- Fault metric.
- Error queue.
- Correlation ID.
- Tenant ID.
- Original message ID.
- Exception classification.
- Replay capability.
- Operator UI or support command for safe replay.

A dead message must not require direct RabbitMQ database hacking.

---

# 10. Long-Running Workflows

Do not use Sagas for ordinary CRUD.

Use Saga/state-machine patterns only when a process:

- spans multiple transactions,
- takes meaningful time,
- crosses modules/external systems,
- requires compensation,
- and must survive process restarts.

Possible future examples:

- Government e-invoice onboarding.
- Long-running payment workflow.
- Complex order fulfillment.
- Import orchestration.
- Intercompany workflow.

MassTransit state machines are allowed when justified.

---

# 11. Scheduled Jobs

RabbitMQ is not our business calendar.

Use **Quartz.NET with PostgreSQL persistent storage** for scheduled/time-based jobs:

- nightly reconciliation,
- invoice reminders,
- exchange-rate refresh,
- payroll scheduled operations,
- certificate expiry checks,
- cleanup jobs,
- scheduled reports.

Quartz jobs may publish commands/events to RabbitMQ.

Example:

```text
Quartz
  ↓
Publish RecalculateAging
  ↓
RabbitMQ
  ↓
Collections Consumer
```

---

# 12. Overall Runtime Topology — Phase 1

Production begins with a small number of deployable units:

```text
                     ┌─────────────────┐
Browser ────────────►│   ERP Web SPA   │
                     └────────┬────────┘
                              │ HTTPS
                              ▼
                     ┌─────────────────┐
                     │    ERP API      │
                     │ ASP.NET Core    │
                     └───┬─────────┬───┘
                         │         │
                  PostgreSQL    S3 Storage
                         │
                  Outbox │
                         ▼
                    RabbitMQ
                         │
                         ▼
                     ┌─────────────────┐
                     │   ERP Worker    │
                     │ MassTransit     │
                     │ Quartz          │
                     └───┬─────────┬───┘
                         │         │
                    PostgreSQL   External APIs
```

Deployables initially:

1. `erp-api`
2. `erp-worker`
3. `erp-web`

Infrastructure:

4. PostgreSQL
5. RabbitMQ
6. S3-compatible storage
7. OpenTelemetry Collector
8. Observability backend

That is production-ready without becoming microservices.

---

# 13. Monorepo Decision

Use a **monorepo**.

Reasons:

- Backend and frontend contracts evolve together.
- One CI pipeline can validate architecture.
- One pull request can update backend + generated API + UI.
- Easier release coordination before teams become very large.

Repository:

```text
erp-saas/
├── src/
├── tests/
├── docs/
├── ops/
├── tools/
├── scripts/
├── .github/
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── package.json
├── pnpm-workspace.yaml
├── pnpm-lock.yaml
├── docker-compose.yml
├── .env.example
├── README.md
└── Todir.sln
```

---

# 14. Top-Level Repository Structure

```text
erp-saas/
│
├── src/
│   ├── backend/
│   │   ├── hosts/
│   │   ├── shared/
│   │   ├── platform/
│   │   ├── foundation/
│   │   └── modules/
│   │
│   └── frontend/
│       ├── apps/
│       └── packages/
│
├── tests/
│   ├── architecture/
│   ├── integration/
│   ├── contracts/
│   ├── e2e/
│   └── performance/
│
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── modules/
│   ├── runbooks/
│   ├── api/
│   └── security/
│
├── ops/
│   ├── containers/
│   ├── compose/
│   ├── terraform/
│   ├── monitoring/
│   ├── rabbitmq/
│   └── database/
│
├── tools/
│   ├── module-generator/
│   ├── migration-cli/
│   └── dev-cli/
│
├── scripts/
│
└── .github/
    └── workflows/
```

---

# 15. Backend Hosts

```text
src/backend/hosts/

├── Todir.Api/
│   ├── Program.cs
│   ├── Bootstrap/
│   ├── Middleware/
│   ├── OpenApi/
│   ├── Health/
│   └── appsettings.*
│
├── Todir.Worker/
│   ├── Program.cs
│   ├── Consumers/
│   ├── Scheduling/
│   ├── Health/
│   └── appsettings.*
│
└── Todir.DbMigrator/
    ├── Program.cs
    └── MigrationRunner/
```

## Todir.Api responsibilities

- HTTP API.
- Authentication.
- Authorization.
- Tenant resolution.
- Rate limiting.
- Request correlation.
- Module endpoint registration.
- OpenAPI.
- Synchronous application commands.
- Transaction coordination.

## Todir.Worker responsibilities

- RabbitMQ consumers.
- Outbox dispatch support.
- External integration jobs.
- Quartz scheduled jobs.
- Read-model builders.
- Long-running jobs.

## Todir.DbMigrator responsibilities

- Controlled database migrations.
- Module schema migration ordering.
- Expand/contract migration support.
- Deployment preflight.

Production API startup **must not blindly apply migrations**.

---

# 16. Shared Backend Projects

```text
src/backend/shared/

├── Todir.SharedKernel/
├── Todir.Contracts/
├── Todir.BuildingBlocks/
└── Todir.Testing/
```

## Todir.SharedKernel

Must remain tiny.

Allowed:

- `Money`
- `Quantity`
- `EntityRef`
- typed identifiers
- domain error primitives
- result primitives
- actor types
- time abstractions
- correlation identifiers

Forbidden:

- Customer
- Product
- Invoice
- Warehouse
- Employee
- business services

If it has business meaning, it does not belong in SharedKernel.

---

## Todir.Contracts

Platform-level contracts only.

Examples:

- `ITenantContext`
- `IActorContext`
- `IClock`
- `ITransactionCoordinator`
- `IMessagePublisher`
- `IEntitlementChecker`
- `IAuditWriter`

Business-module contracts live in their own module contract projects.

---

## Todir.BuildingBlocks

Technical implementation helpers:

- EF interceptors.
- ProblemDetails.
- OpenTelemetry helpers.
- request pipeline helpers.
- messaging envelope helpers.
- idempotency middleware.
- pagination.
- API conventions.

BuildingBlocks cannot contain ERP business logic.

---

# 17. Platform Modules

```text
src/backend/platform/

├── Tenancy/
├── Organization/
├── Identity/
├── Authorization/
├── Audit/
├── Entitlements/
├── Messaging/
├── Files/
├── Localization/
├── Numbering/
├── Settings/
├── CustomFields/
├── Notifications/
├── Importing/
├── Reporting/
└── Observability/
```

These are platform capabilities.

They are not customer business modules.

---

# 18. Foundation Domains

```text
src/backend/foundation/

├── Directory/
├── Catalog/
├── FinBase/
├── Tax/
└── Pricing/
```

They follow the same module boundary rules as business modules.

They are shared domains, not random helper libraries.

---

# 19. Business Modules

Planned module tree:

```text
src/backend/modules/

├── Ledger/
├── Subledger/
├── Invoicing/
├── Payments/
├── Accounting/
├── BankReconciliation/
├── Collections/
├── Cheques/
├── Inventory/
├── Sales/
├── Purchasing/
├── Compliance/
├── Migration/
├── Approvals/
├── CRM/
├── HRCore/
├── Leave/
├── Attendance/
├── Payroll/
├── Expenses/
├── FixedAssets/
├── POS/
├── Channels/
├── Subscriptions/
├── Projects/
├── Commissions/
├── Manufacturing/
├── Quality/
├── Shipping/
├── Service/
├── Maintenance/
├── Portal/
├── Integrations/
└── AIGateway/
```

Do not create all implementations on Day 1.

Reserve the module IDs and dependency plan, but create code projects as their implementation begins.

---

# 20. Standard Module Project Structure

To avoid hundreds of tiny .NET projects, use:

> One implementation project + one public Contracts project per significant module.

Example:

```text
src/backend/modules/Sales/

├── Todir.Modules.Sales/
│   ├── Module.cs
│   ├── SalesModuleManifest.cs
│   │
│   ├── Domain/
│   │   ├── Aggregates/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Policies/
│   │   ├── Services/
│   │   ├── Events/
│   │   └── Errors/
│   │
│   ├── Application/
│   │   ├── Features/
│   │   ├── Ports/
│   │   ├── Permissions/
│   │   ├── Policies/
│   │   └── Mapping/
│   │
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   ├── Messaging/
│   │   ├── Integrations/
│   │   └── Configuration/
│   │
│   ├── Api/
│   │   ├── Endpoints/
│   │   ├── Requests/
│   │   └── Responses/
│   │
│   ├── ReadModels/
│   └── DependencyInjection.cs
│
├── Todir.Modules.Sales.Contracts/
│   ├── Commands/
│   ├── Queries/
│   ├── Events/
│   ├── DTOs/
│   └── Capabilities/
│
└── README.md
```

---

# 21. Feature Structure Inside a Module

Use vertical feature slices inside the module.

Example:

```text
Application/
└── Features/
    ├── Quotations/
    │   ├── Create/
    │   │   ├── CreateQuotationCommand.cs
    │   │   ├── CreateQuotationHandler.cs
    │   │   └── CreateQuotationValidator.cs
    │   │
    │   └── Send/
    │
    ├── Orders/
    │   ├── Confirm/
    │   ├── Cancel/
    │   └── Revise/
    │
    └── Returns/
        └── Authorize/
```

Avoid global folders like:

```text
Services/
Managers/
Helpers/
Utils/
```

that become dumping grounds.

---

# 22. DDD Usage Rule

Use DDD **selectively**, not ceremonially.

## Rich domain model for

- Ledger.
- Subledger.
- Inventory.
- Invoicing.
- Payments.
- Payroll.
- Order state transitions.
- Module lifecycle.

## Simpler application CRUD is acceptable for

- Tags.
- simple master data.
- reference tables.
- UI preferences.
- non-critical configuration.

Do not make a 12-class abstraction to rename a category.

---

# 23. Repository Pattern Rule

Do not implement a generic repository:

```text
IRepository<T>
GetAll
Find
Add
Update
Delete
```

EF Core already supplies a unit-of-work and data access abstraction.

Use repositories only around aggregates where they add meaningful domain boundaries.

Examples:

```text
ISalesOrderRepository
IJournalRepository
IStockLedgerRepository
```

Or access the module DbContext directly from application handlers where appropriate.

---

# 24. Database Ownership

Each module owns its own PostgreSQL schema.

Example:

```text
platform_tenancy
platform_iam
foundation_directory
foundation_catalog

finance_ledger
finance_subledger
finance_invoicing
finance_payments

trade_inventory
trade_sales
trade_purchasing

people_hr
people_payroll
```

A module writes only its own schema.

---

# 25. DbContext Ownership

Each module has its own DbContext.

Example:

```text
DirectoryDbContext
CatalogDbContext
LedgerDbContext
SubledgerDbContext
InvoicingDbContext
InventoryDbContext
SalesDbContext
```

No giant:

```text
ErpDbContext
```

containing 600 tables.

---

# 26. Cross-Module Database Rule

Forbidden:

```text
SalesDbContext.Orders
    .Include(x => x.Invoice)
    .ThenInclude(x => x.JournalEntry)
```

when Invoice and Ledger belong to other modules.

Forbidden:

- Cross-module ORM navigation.
- Writing another module's table.
- Raw SQL across business-module schemas in application flows.

Use:

- Contracts.
- Commands.
- Queries.
- Integration Events.
- Read models.

---

# 27. Database Foreign Keys

Allowed to stable platform/foundation IDs:

- tenant_id
- legal_entity_id
- branch_id
- party_id
- product_variant_id
- currency_id
- uom_id

Business-module-to-business-module references use opaque IDs/refs instead of DB FK unless an ADR proves a hard invariant requires otherwise.

---

# 28. Identifier Standard

Use UUIDv7 / sequential time-ordered GUIDs for distributed business identifiers.

.NET provides appropriate version-7 GUID support.

Rules:

- ID generated by application.
- Never expose DB sequence as business identity.
- Legal document number is **not** the entity ID.
- Document number is generated through Numbering service.

Example:

```text
invoice_id     = UUIDv7
invoice_number = INV-2026-000023
```

---

# 29. Database Naming

PostgreSQL:

```text
snake_case
```

Example:

```text
sales_order
sales_order_line
tenant_id
legal_entity_id
created_at
```

C#:

```text
PascalCase
```

Mapping is explicit via EF conventions/configurations.

---

# 30. Date and Time Standard

Never use server-local time for business logic.

Use:

- UTC `Instant` for events.
- PostgreSQL `timestamptz` for instants.
- `date` for business dates.
- IANA timezone ID on Legal Entity.
- NodaTime in backend.
- ISO-8601 in APIs.

Examples:

```text
created_at      → Instant
invoice_date    → LocalDate
due_date        → LocalDate
entity_timezone → Africa/Cairo
```

---

# 31. Money, Quantity and Decimal Standard

Never use floating-point types for finance.

No:

```text
float
double
```

for amounts.

Use decimal / PostgreSQL numeric.

Recommended starting standards:

```text
monetary total          numeric(19,4)
unit price              numeric(19,6)
quantity                numeric(19,6)
exchange rate           numeric(19,10)
tax rate                numeric(12,6)
```

Exact domain-specific precision is locked during the Finance/Catalog design packs.

Money must carry currency.

Quantity must carry UOM/context.

---

# 32. Soft Delete Rule

Do not implement one universal `is_deleted` behavior for all tables.

Use domain semantics:

- Archived.
- Cancelled.
- Voided.
- Reversed.
- Disabled.

Financial or legally relevant records are not silently deleted.

---

# 33. Optimistic Concurrency

Mutable aggregates should have a version/concurrency token.

Client update:

```text
GET version=12
PUT If-Match=12
```

If current version is 13:

```text
409 Conflict
```

or `412 Precondition Failed` according to endpoint contract.

Never silently overwrite another user's business change.

---

# 34. Multi-Tenancy Baseline

Database model:

```text
Shared PostgreSQL
+ schema per module
+ tenant_id on tenant-owned rows
+ PostgreSQL Row Level Security
```

## Mandatory request flow

```text
Authentication
   ↓
Membership
   ↓
Tenant resolution
   ↓
TenantContext
   ↓
RLS transaction context
   ↓
Application command
```

The API never trusts `tenant_id` supplied by request payload.

---

# 35. RLS Standard

Every tenant table must have RLS.

Concept:

```sql
tenant_id = current_setting('app.tenant_id')::uuid
```

Tenant context must be set at transaction/connection scope safely.

Tests must explicitly attempt cross-tenant access.

---

# 36. Authentication Architecture

Use:

- ASP.NET Core Identity as identity/user storage.
- OpenIddict from the beginning as OAuth2/OIDC foundation.
- Secure HttpOnly cookie for browser ERP sessions.
- Authorization Code + PKCE where applicable.
- External clients/API later receive scoped OAuth credentials.

Do not store access tokens in `localStorage` for the ERP browser application.

---

# 37. Authorization Architecture

```text
Identity
   ↓
Tenant Membership
   ↓
Legal Entity Scope
   ↓
Branch Scope
   ↓
Role
   ↓
Permission
   ↓
Record / field restrictions
```

Backend is authoritative.

Frontend permission checks are only presentation logic.

Examples:

```text
sales.order.create
sales.order.confirm
sales.price.override
inventory.cost.view
ledger.period.close
payroll.salary.view
```

---

# 38. MFA

MFA support is production baseline.

At minimum:

- TOTP.
- recovery codes.
- step-up authentication hooks for sensitive future actions.

High-risk users:

- Tenant Admin.
- Finance Admin.
- Payroll Admin.
- Platform Support.

---

# 39. Browser Security

Production defaults:

- HTTPS only.
- HSTS.
- Secure cookies.
- HttpOnly cookies.
- SameSite policy.
- CSRF protection.
- Content Security Policy.
- X-Content-Type-Options.
- Frame restrictions.
- strict CORS.
- request size limits.
- upload validation.
- rate limiting.

---

# 40. Secrets Management

Never put production secrets into:

- source code,
- appsettings committed files,
- Docker image,
- RabbitMQ message,
- logs.

Local development:

- `.env` ignored by Git.
- .NET user secrets where appropriate.

Production:

- Cloud Secrets Manager / Vault-equivalent.

Examples:

- DB credentials.
- RabbitMQ credentials.
- OAuth keys.
- S3 keys.
- government certificates.
- signing credentials.
- payment API secrets.

---

# 41. Data Protection Keys

ASP.NET Core Data Protection keys must not live only on ephemeral container disk.

Persist keys to durable encrypted storage.

If multiple API instances run, they must share the correct key ring.

---

# 42. Sensitive Field Protection

Sensitive fields may require application-level encryption:

- national IDs.
- bank account information.
- payroll data.
- government credentials.

Rules:

- encryption keys separate from DB.
- masked display.
- never log plaintext.
- explicit permission to decrypt/display.

---

# 43. API Standards

Base:

```text
/api/v1/
```

Examples:

```text
GET    /api/v1/customers
POST   /api/v1/sales/orders
POST   /api/v1/sales/orders/{id}/confirm
POST   /api/v1/invoices/{id}/post
```

Prefer intent-based endpoints for state transitions.

Avoid:

```text
PUT /orders/{id} { status: "confirmed" }
```

for critical transitions.

Use:

```text
POST /orders/{id}/confirm
```

so validation/permission/audit is explicit.

---

# 44. API Error Format

Use RFC Problem Details.

Example:

```json
{
  "type": "https://errors.example.com/inventory/insufficient-stock",
  "title": "Insufficient stock",
  "status": 409,
  "code": "inventory.insufficient_stock",
  "traceId": "...",
  "errors": []
}
```

Business errors use stable machine-readable error codes.

Frontend translates user-facing text.

---

# 45. API Pagination

Standard cursor or offset pagination based on use case.

Never return 500,000 records because a frontend forgot a filter.

Every list endpoint must define:

- pagination.
- sort.
- filters.
- max page size.

---

# 46. API Idempotency

Critical create/post endpoints accept:

```text
Idempotency-Key
```

Examples:

- invoice posting.
- payment creation.
- external order import.
- government submission request.
- webhook command.

Store response/result for the idempotency window.

---

# 47. OpenAPI

OpenAPI is generated from the backend.

CI must:

- produce OpenAPI spec.
- detect unexpected breaking changes.
- generate frontend API client.
- fail if generated client differs unexpectedly.

Public API breaking changes require version strategy.

---

# 48. Frontend Monorepo Structure

```text
src/frontend/
├── package.json
├── tsconfig.json
├── vite.config.ts
├── index.html
└── src/
    ├── main.tsx
    ├── app/
    ├── platform/
    ├── design-system/
    └── modules/
```

The current repository has one frontend application. Logical boundaries are enforced within `src/frontend/src/`; separate npm packages are introduced only when independent deployment/versioning, proven cross-application reuse, or a build/tooling need justifies extraction.

---

# 49. Frontend Application Structure

```text
src/frontend/src/

├── app/
│   ├── bootstrap/
│   └── shell/
│
├── platform/
│   ├── api/
│   └── runtime/

├── design-system/
│   ├── tokens/
│   ├── theme/
│   └── grid/
│
└── modules/
    └── AGENTS.md
```

---

# 50. Standard Frontend Module

Example:

```text
modules/sales/

├── api/
├── routes/
├── pages/
├── features/
├── components/
├── queries/
├── mutations/
├── schemas/
├── permissions/
├── i18n/
└── tests/
```

Do not import internal files from another frontend module.

Expose an explicit public barrel:

```text
modules/sales/public.ts
```

when another module needs a UI integration.

---

# 51. ERP Design System & Frontend Platform

The frontend foundation is brand-neutral and token-driven, built on headless UI primitives (governed by ADR-021 and `FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`).

Create:

```text
packages/ui/
packages/design-tokens/
packages/theme/
```

with brand-neutral reusable components:

```text
Button
Field
MoneyField
QuantityField
DateField
EntitySelector
DataGrid
StatusBadge
ActionMenu
PermissionGuard
FormSection
AuditTrail
```

Business screens must consume brand-neutral design system components and semantic tokens rather than vendor-specific libraries or raw palette values.

---

# 52. RTL / Arabic

RTL is Day 1.

Test:

- Arabic labels.
- mixed Arabic/English text.
- numeric fields.
- monetary fields.
- date pickers.
- tables.
- PDF print.
- long Arabic product names.
- search normalization.

No "we will add RTL later."

---

# 53. Server State

TanStack Query owns server state.

Examples:

- Customers.
- Products.
- Orders.
- Invoices.
- Stock availability.

Use:

- query keys by module.
- cache invalidation rules.
- optimistic updates only where safe.

Do not copy server entities into Zustand.

---

# 54. Form State

React Hook Form + Zod.

Rules:

- backend validation remains authoritative.
- frontend schemas improve UX.
- generated DTO schema and form schema may differ.
- business-critical calculations are revalidated on backend.

---

# 55. Large ERP Tables & DataGrid

Use the platform `DataGrid` wrapper (governed by ADR-021 and `FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`, backed by an evidence-based free grid engine).

Required behavior:

- server-side pagination.
- server-side filters.
- saved views.
- column visibility.
- role-aware columns.
- virtualization.
- bulk actions.
- export permissions.
- exact financial value formatting without JS number conversion.
- free-only core dependency rule (no AG Grid Enterprise or paid grid licenses).

Never load the entire ledger or stock history into browser memory.

---

# 56. Message-driven UI Updates

Do not require SignalR Day 1.

Initial strategy:

- normal refetch.
- polling only where justified.
- background operation status endpoint.

SignalR may be added later for:

- import progress.
- live notifications.
- POS status.
- collaborative screens.

The broker is backend infrastructure; browser clients do not connect directly to RabbitMQ.

---

# 57. Logging Standard

Use Serilog structured logs.

Required fields:

```text
timestamp
level
service
environment
trace_id
span_id
correlation_id
tenant_id
legal_entity_id?
user_id?
module
operation
```

Never log:

- passwords.
- tokens.
- full government certificates.
- card data.
- plaintext salary where avoidable.
- sensitive identification data.

---

# 58. OpenTelemetry

Instrument:

- ASP.NET requests.
- PostgreSQL.
- RabbitMQ/MassTransit.
- outgoing HTTP.
- background jobs.
- custom domain operations.

Metrics:

- API latency/error rate.
- DB query latency.
- DB connections.
- queue depth.
- consumer processing duration.
- consumer errors.
- outbox backlog.
- dead letters.
- job failures.
- external API latency.
- compliance failures.

---

# 59. Health Endpoints

Expose:

```text
/health/live
/health/ready
```

`live`:

- process responds.

`ready`:

- required dependencies available enough to serve.

Do not make liveness depend on every optional external API.

---

# 60. Observability Stack

Local/staging reference stack:

```text
OpenTelemetry Collector
Prometheus
Grafana
Tempo
Loki
Sentry
```

Production may use a managed equivalent without changing application instrumentation.

---

# 61. Audit Log Is Not Application Log

Application log:

- debugging/operations.

Audit log:

- business/security history.

Audit is:

- append-only.
- queryable by authorized users.
- has actor.
- before/after where appropriate.
- source/correlation.
- reason for sensitive overrides.

Do not reconstruct audit from Serilog.

---

# 62. Local Development Infrastructure

`docker-compose.yml` should include:

```text
postgres
rabbitmq
minio
otel-collector
prometheus
grafana
tempo
loki
mailpit
```

Optional profiles can disable heavier observability services for lightweight development.

---

# 63. Production Infrastructure Principle

Prefer managed infrastructure:

- Managed PostgreSQL.
- Managed RabbitMQ.
- Managed object storage.
- Managed secret store.
- Managed load balancer.

This reduces operational risk.

Production-ready does **not** mean self-host every component.

---

# 64. Kubernetes Decision

Do not introduce Kubernetes on Day 1.

Initial production can run containers on:

- Azure Container Apps / App Service containers,
- AWS ECS/Fargate,
- equivalent managed container platform.

Kubernetes becomes justified when:

- workload topology demands it,
- platform team exists,
- multiple independently-scaled services exist,
- operational benefit exceeds complexity.

---

# 65. Infrastructure as Code

Use **OpenTofu** for cloud-neutral Infrastructure as Code.

Structure:

```text
ops/terraform/

├── modules/
│   ├── network/
│   ├── postgres/
│   ├── rabbitmq/
│   ├── storage/
│   ├── secrets/
│   ├── app/
│   └── observability/
│
└── environments/
    ├── dev/
    ├── staging/
    └── prod/
```

Never manually click production infrastructure into existence without IaC tracking.

---

# 66. Docker Standards

Each deployable has:

- multi-stage Dockerfile.
- non-root runtime user.
- read-only filesystem where practical.
- health endpoint.
- explicit CPU/memory config in deploy platform.
- no secrets baked into image.
- deterministic build.

Images:

```text
erp-api:<commit-sha>
erp-worker:<commit-sha>
erp-web:<commit-sha>
erp-migrator:<commit-sha>
```

Do not deploy `latest`.

---

# 67. Environment Configuration

Environments:

```text
local
dev
staging
production
```

No environment-specific business logic.

Use configuration, not `if (environment == "EgyptProd")`.

---

# 68. Database Migrations

Migrations are module-owned.

Example:

```text
Sales/
Infrastructure/Persistence/Migrations/
```

Deployment order comes from module dependency graph.

Rules:

- Migration reviewed like code.
- No destructive migration in same release as old-code removal.
- Use expand/contract.
- Backfill separately for large tables.
- Schema changes are forward-compatible during rolling deploy.

---

# 69. Expand / Contract Example

Instead of:

```text
Rename column old_name → new_name
```

directly:

Release A:

```text
Add new_name
write both
backfill
```

Release B:

```text
read new_name
```

Release C:

```text
stop writing old_name
```

Release D:

```text
drop old_name
```

---

# 70. Production Data Changes

Do not use application startup code to silently mutate production business data.

Use:

- versioned migration.
- explicit seed migration.
- admin operation.
- audited maintenance command.

Country pack updates are proposals/versioned migrations, not uncontrolled startup seeding.

---

# 71. CI Pipeline

Every Pull Request:

```text
1. checkout
2. restore locked dependencies
3. format/lint
4. backend compile
5. frontend typecheck
6. unit tests
7. architecture tests
8. integration tests
9. contract tests
10. frontend component tests
11. build frontend
12. generate OpenAPI
13. API breaking-change check
14. generated-client drift check
15. migration validation
16. container build
17. dependency/security scan
```

Merge is blocked on failure.

---

# 72. CD Pipeline

```text
main
  ↓
Build immutable images
  ↓
Security scan
  ↓
Deploy Dev
  ↓
Smoke tests
  ↓
Deploy Staging
  ↓
Integration/E2E
  ↓
Manual production approval
  ↓
Run migrator
  ↓
Rolling deploy API/Worker/Web
  ↓
Post-deploy checks
```

---

# 73. Branching

Prefer trunk-based development with short-lived branches.

```text
main
feature/*
fix/*
```

No long-lived:

```text
develop
integration
release-forever
```

unless the team later demonstrates a need.

---

# 74. Pull Request Rules

A PR must:

- be small enough to review.
- contain tests.
- not violate module dependency rules.
- contain migration if schema changed.
- update contracts/version if public message/API changed.
- add ADR for architectural change.
- update observability for critical new process.

At least one review.

High-risk finance/security code requires designated owner review.

---

# 75. Architecture Tests

CI must enforce:

1. SharedKernel references no module.
2. Contracts reference no Infrastructure.
3. Module A cannot reference Module B implementation.
4. Cross-module references only target `*.Contracts`.
5. No cyclic module dependency.
6. Platform cannot depend on business module.
7. Domain cannot depend on Infrastructure.
8. Frontend module cannot import another module's internal files.

---

# 76. Module Manifest

Every module declares a manifest.

Concept:

```yaml
module:
  id: sales
  version: 1.0.0
  layer: trade
  purchasable: true

dependencies:
  hard:
    - directory
    - catalog
    - pricing
  optional:
    - inventory
    - invoicing
    - collections
    - approvals

owns:
  schema: trade_sales

messaging:
  publishes:
    - sales.order.confirmed.v1
    - sales.order.cancelled.v1
  consumes: []

permissions:
  - sales.order.create
  - sales.order.confirm
  - sales.price.override
```

Manifest validation is part of CI.

---

# 77. Module Bootstrap

Each module implements one registration point.

Conceptually:

```csharp
public static class SalesModule
{
    public static IServiceCollection AddSalesModule(...);
    public static IEndpointRouteBuilder MapSalesEndpoints(...);
}
```

Composition root selects registered modules.

Do not scatter module registration across the host.

---

# 78. Message Contract Placement

Public integration messages belong to:

```text
Todir.Modules.X.Contracts
```

Do not publish:

- EF entities.
- domain aggregate objects.
- internal application commands.

Message contracts are intentionally stable DTOs.

---

# 79. Message Versioning

Breaking event change:

Do not mutate `InvoicePosted.v1` in a breaking way.

Add:

```text
InvoicePosted.v2
```

Transition consumers.

Backward-compatible field additions may stay in version with optional/default semantics when safe.

---

# 80. Consumer Naming

Queue names are owned by consumers, not publishers.

Example:

```text
compliance.invoice-posted
reporting.invoice-posted
notifications.invoice-posted
```

Not:

```text
invoice-posted-queue
```

shared by unrelated consumers.

Each consumer group needs its own delivery.

---

# 81. Correlation and Causation

Example:

```text
HTTP request
correlation_id = C1
        ↓
ConfirmOrder command
        ↓
OrderConfirmed event
message_id = M1
correlation_id = C1
causation_id = original-command-id
        ↓
Notification
message_id = M2
correlation_id = C1
causation_id = M1
```

This makes distributed troubleshooting possible.

---

# 82. Tenant Context in Messaging

Every tenant-scoped message must carry Tenant ID in trusted message headers/envelope.

Consumer flow:

```text
receive message
   ↓
validate tenant
   ↓
create TenantContext
   ↓
begin DB transaction
   ↓
set PostgreSQL RLS tenant context
   ↓
execute consumer
```

No consumer may execute tenant data queries before TenantContext is established.

---

# 83. Broker Security Boundary

RabbitMQ is internal infrastructure.

- Not exposed publicly.
- Firewall/private network.
- TLS.
- credentials rotated.
- management UI private/admin-only.
- audit admin access.
- no browser direct access.

---

# 84. Object Storage

Use S3-compatible API.

Local:

```text
MinIO
```

Production:

- AWS S3,
- Azure Blob behind adapter,
- or compatible object service.

Object key:

```text
tenant/{tenantId}/module/{module}/entity/{entityId}/...
```

Never use original filename as the only object identifier.

Store metadata in PostgreSQL.

---

# 85. Upload Security

- size limit.
- content-type validation.
- file signature validation.
- malware scan where required.
- quarantine for untrusted files.
- signed URLs.
- short expiry.
- tenant-scoped access checks.

---

# 86. Cache Policy

Redis is **not mandatory Day 1**.

Start with:

- database indexes,
- Npgsql pooling,
- process memory cache for safe reference data.

Add Redis only for proven requirements:

- distributed cache.
- shared rate limits.
- distributed locks.
- SignalR scaleout.
- heavy hot-reference cache.

Message Broker is not a cache.

---

# 87. Search Policy

PostgreSQL first:

- indexes.
- trigram.
- full-text.
- normalized Arabic search strategy.

Add OpenSearch only when measured need exists.

---

# 88. Reporting Policy

Operational reporting:

- PostgreSQL read models.
- projections.
- indexed reporting tables.

Do not execute giant cross-domain ORM graphs.

Read replicas may be introduced before a data warehouse.

---

# 89. Backup & Recovery

PostgreSQL:

- automated backups.
- point-in-time recovery.
- encrypted backup storage.
- restore tests.

Object storage:

- versioning where available.
- lifecycle policy.

RabbitMQ:

- durable queues, but RabbitMQ is **not the system of record**.
- messages must be recoverable from outbox/replay strategy when possible.

---

# 90. RPO / RTO Targets

Initial engineering targets, to be validated with hosting provider:

```text
Database RPO: <= 5 minutes
Database RTO: <= 60 minutes
```

Enterprise plans may require tighter objectives later.

Targets are not claims until disaster-recovery tests prove them.

---

# 91. Disaster Recovery Drills

At least quarterly in production maturity:

- restore DB.
- restore object metadata/files.
- rebuild workers.
- verify RabbitMQ consumers.
- replay safe outbox messages.
- validate tenant isolation after restore.

A backup that has never been restored is not a proven backup.

---

# 92. Performance Baseline

Every endpoint has a query budget.

Avoid:

- N+1.
- unbounded includes.
- loading full entities for list views.
- frontend fetching 10 endpoints for one grid row.

Performance tests begin before launch for:

- customer search.
- product search.
- sales order create/confirm.
- invoice post.
- stock availability.
- bulk import.
- aging.
- ledger queries.

---

# 93. Load Testing

Use k6 or equivalent.

Test:

- API concurrency.
- DB pooling.
- RabbitMQ throughput.
- consumer backlog recovery.
- outbox drain.
- import workload.
- noisy tenant.

Performance test data must include realistic tenant sizes.

---

# 94. Test Pyramid

```text
              E2E
            /     \
      Integration
        /         \
 Contract        Component
      \           /
          Unit
```

Prioritize domain/integration tests over thousands of mock-heavy tests.

---

# 95. Unit Tests

Test:

- pure business rules.
- state transitions.
- money/rounding.
- permissions policies.
- posting rules.
- pricing rules.
- payroll formulas.

No database required.

---

# 96. Integration Tests

Use Testcontainers for real:

- PostgreSQL.
- RabbitMQ.

Integration tests must validate:

- EF mappings.
- RLS.
- real transactions.
- outbox.
- message consumers.
- idempotency.
- migrations.
- concurrency.

Do not replace core infrastructure behavior with mocks.

---

# 97. Messaging Tests

Required scenarios:

1. DB commits and event reaches RabbitMQ.
2. DB rollback publishes nothing.
3. duplicate message is not processed twice.
4. transient consumer failure redelivers.
5. poison message reaches error queue.
6. replay does not duplicate financial state.
7. tenant context is preserved.
8. correlation IDs propagate.

---

# 98. Contract Tests

Test:

- OpenAPI compatibility.
- integration event schemas.
- provider capability contracts.
- country adapter contracts.
- payment gateway adapters.

External systems use WireMock.Net or sandbox environments.

---

# 99. E2E

Playwright scenarios focus on high-value workflows:

- login / tenant switch.
- customer create.
- product create.
- quotation.
- order.
- invoice.
- payment.
- stock receipt.
- purchase.
- permission denial.
- Arabic RTL.

Do not attempt to E2E-test every field variation.

---

# 100. Security Testing

CI:

- dependency scanning.
- container scanning.
- secret scanning.
- static analysis.

Periodic:

- penetration test.
- cross-tenant fuzz testing.
- authorization matrix testing.
- file upload attack testing.
- webhook signature tests.

---

# 101. Dependency Management

Backend:

```text
Directory.Packages.props
```

for central NuGet versions.

Frontend:

```text
pnpm-lock.yaml
```

Rules:

- lock exact resolved dependencies.
- automated dependency PRs.
- no auto-merge of major versions.
- security patches prioritized.

---

# 102. .NET Build Standards

Repository-level:

```text
Nullable = enable
TreatWarningsAsErrors = true
Deterministic = true
AnalysisLevel = latest recommended
```

Use:

- `.editorconfig`.
- analyzers.
- formatting check in CI.

Avoid framework-specific cleverness when simple C# is clearer.

---

# 103. TypeScript Standards

- strict mode.
- no implicit `any`.
- ESLint.
- formatting.
- typecheck in CI.
- generated API client excluded from hand-editing.
- no business-critical `any`.

---

# 104. Feature Flags vs Entitlements

Do not mix them.

## Entitlement

Customer is commercially allowed to use it.

Example:

```text
inventory module
multi_warehouse feature
```

## Feature Flag

Engineering rollout switch.

Example:

```text
new_stock_grid_v2
```

A feature flag is not a billing system.

---

# 105. Configuration

Three categories:

```text
Platform configuration
Tenant configuration
Module configuration
```

Examples:

Platform:

- broker host.
- telemetry exporter.

Tenant:

- language.
- theme.
- timezone.

Module:

- negative stock policy.
- invoice numbering.
- discount approval threshold.

---

# 106. Do Not Put Business Rules in appsettings

Wrong:

```json
{
  "MaxDiscount": 10
}
```

when it is tenant business configuration.

Business policies belong in audited DB configuration with versioning.

---

# 107. Internationalization

Backend returns:

- machine code.
- structured parameters.

Example:

```text
inventory.insufficient_stock
{ available: 3, requested: 7 }
```

Frontend translates.

Avoid hardcoded Arabic/English business error strings in domain code.

---

# 108. Print / PDF Architecture

Do not make browser DOM printing the legal document source of truth.

Use server-side print/template capability for:

- invoices.
- credit notes.
- statements.
- payslips.
- purchase orders.

Templates are versioned.

Posted/legal document rendering uses the document snapshot.

---

# 109. Email / Notifications

Notification channel abstraction:

```text
InApp
Email
WhatsApp
SMS
```

Domain modules publish business events.

Notification handlers decide channels/templates.

Sales does not directly call an SMTP client.

---

# 110. External HTTP Clients

Use:

- `IHttpClientFactory`.
- resilience handlers.
- timeout.
- retry only safe operations.
- circuit breaker where justified.
- tracing.
- typed clients.

No raw `new HttpClient()` per request.

---

# 111. External Credential Model

Credentials are:

- scoped per tenant/entity/connector.
- encrypted.
- stored in vault/credential store.
- versioned/rotatable.
- never returned fully to frontend.

---

# 112. Government Integration Isolation

ETA/ZATCA/UAE adapters are behind compliance ports.

Core invoice code does not contain:

```text
if Egypt
if Saudi
if UAE
```

Country-specific logic belongs to country packs/adapters.

---

# 113. Production Readiness Checklist — Platform

Before first customer:

- [ ] HTTPS.
- [ ] secure authentication.
- [ ] MFA.
- [ ] tenant isolation tests.
- [ ] RLS.
- [ ] permission tests.
- [ ] audit.
- [ ] backups.
- [ ] PITR.
- [ ] restore test.
- [ ] RabbitMQ TLS.
- [ ] outbox.
- [ ] inbox/idempotency.
- [ ] error queue.
- [ ] replay procedure.
- [ ] observability.
- [ ] health endpoints.
- [ ] alerting.
- [ ] rate limiting.
- [ ] secret management.
- [ ] object storage security.
- [ ] migrations pipeline.
- [ ] rollback/runbook.
- [ ] security scans.
- [ ] E2E smoke tests.

---

# 114. Production Readiness Checklist — Every Module

Before module launch:

- [ ] manifest.
- [ ] hard dependencies.
- [ ] optional dependencies.
- [ ] owner entities.
- [ ] permissions.
- [ ] activation.
- [ ] deactivation.
- [ ] go-live rules.
- [ ] migration/import.
- [ ] API.
- [ ] message contracts.
- [ ] outbox events.
- [ ] consumers idempotent.
- [ ] audit events.
- [ ] RLS tests.
- [ ] concurrency.
- [ ] performance.
- [ ] metrics.
- [ ] dashboards.
- [ ] runbook.
- [ ] backup/restore effect.
- [ ] Arabic/English UI.
- [ ] role UX.
- [ ] error handling.

---

# 115. Initial Module Implementation Sequence

Engineering order:

```text
I0  Repository + Hosts + CI
I1  Platform Kernel
I2  Messaging + RabbitMQ + Outbox/Inbox
I3  Tenancy + Org + IAM + Authorization + Audit
I4  Entitlements + Module Registry + Lifecycle
I5  Directory
I6  Catalog
I7  FinBase
I8  Tax
I9  Pricing
I10 Ledger
I11 Subledger
I12 Invoicing
I13 Payments
I14 Inventory
I15 Sales
I16 Purchasing
I17 Compliance Pack
I18 Accounting UX / Reporting
I19 Migration tooling
I20 Approvals
```

The exact business release order is separate from implementation order.

---

# 116. Day-1 Repository Bootstrap Order

## Step 1 — Toolchain

Create:

- `.NET global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `pnpm-workspace.yaml`
- `.editorconfig`
- CI pipeline.

## Step 2 — Infrastructure local stack

Docker Compose:

- PostgreSQL
- RabbitMQ
- MinIO
- observability.

## Step 3 — Hosts

- API.
- Worker.
- DbMigrator.
- Web.

## Step 4 — Platform primitives

- TenantContext.
- ActorContext.
- Correlation.
- ProblemDetails.
- OpenTelemetry.
- auth shell.

## Step 5 — Messaging

- MassTransit.
- RabbitMQ.
- Outbox.
- Inbox/idempotency.
- error handling.
- message envelope.

## Step 6 — Database isolation

- module schemas.
- RLS.
- transaction coordinator.
- migrations.

## Step 7 — Module framework

- Manifest.
- Registry.
- architecture tests.
- entitlement guard.
- module generator.

Only then begin Directory/Catalog business foundations.

---

# 117. Transaction Coordinator

Because some ERP invariants cross module boundaries within the Modular Monolith, implement an explicit in-process transaction coordinator.

Example critical flow:

```text
Invoicing.PostInvoice
   │
   ├── InvoicingDbContext changes
   ├── Subledger.CreateOpenItem()
   ├── Ledger.PostFact()
   └── Outbox InvoicePosted
```

All must share:

- same Npgsql connection.
- same PostgreSQL transaction.
- same tenant RLS context.

Then:

```text
COMMIT
```

Only after commit can Outbox publish to RabbitMQ.

This prevents the message broker from weakening financial integrity.

---

# 118. Cross-Module Transaction Rule

Cross-module synchronous transaction is allowed only for a documented invariant.

Examples that may qualify:

- Posted invoice ↔ ledger/subledger entry.
- finalized stock movement ↔ valuation.
- payment ↔ required ledger posting.

Do not use shared transactions casually for:

- notifications.
- analytics.
- search.
- CRM updates.
- external government API.

Those belong after commit through the broker.

---

# 119. Anti-Corruption Layer

External systems and legacy formats are translated at module boundaries.

Examples:

```text
ETA DTO
  ↓
ETA Adapter
  ↓
Compliance Contract
```

Not:

```text
ETA fields inside Invoice aggregate
```

Same for:

- ZATCA.
- payment gateways.
- e-commerce.
- biometric devices.
- bank files.

---

# 120. Read Model Pattern

Cross-module screens should often use dedicated read models.

Example:

Sales order screen may need:

- customer.
- availability.
- credit exposure.
- delivery state.
- invoice state.

Do not create a cross-schema ORM aggregate.

Build a query/read model with explicit contracts/projections.

---

# 121. CQRS Position

Use **light CQRS**, not two separate systems.

Commands:

- change state.
- enforce rules.

Queries:

- optimized read models.

Same PostgreSQL remains source of truth initially.

No separate write/read databases Day 1.

---

# 122. Event Sourcing Decision

Do **not** use Event Sourcing as the primary persistence architecture.

We use:

- normal transactional relational state.
- immutable financial/stock ledgers where domain requires it.
- audit log.
- integration events.

That is enough.

---

# 123. Module Extraction Readiness

A module is extractable later when:

- no implementation references from others.
- public contracts versioned.
- no cross-schema queries.
- events stable.
- its DB access isolated.
- synchronous requirements can be changed to remote contracts/sagas.

Do not extract until scale/team/compliance proves the need.

Likely future candidates:

- Compliance.
- AI/OCR.
- Integrations.
- heavy Reporting.

---

# 124. What We Explicitly Reject

Day-1 reject list:

- microservices.
- Kubernetes.
- Kafka as default business broker.
- generic repository.
- one giant DbContext.
- direct cross-module DB writes.
- direct RabbitMQ publish inside business transactions without outbox.
- consumers without idempotency.
- Redux as default.
- MongoDB as primary ERP database.
- GraphQL as primary API.
- business logic in frontend.
- arbitrary tenant code/plugins.
- direct AI database access.
- global soft-delete abstraction.
- auto-running production migrations on API startup.
- deployment using mutable `latest` images.

---

# 125. Technology Decision Register

## LOCKED

- C# / .NET 10 LTS.
- ASP.NET Core.
- EF Core + Npgsql.
- PostgreSQL.
- React + TypeScript + Vite.
- RabbitMQ from Day 1.
- MassTransit.
- Transactional Outbox.
- Consumer Inbox/Idempotency.
- Modular Monolith.
- Module-owned schemas and DbContexts.
- REST/OpenAPI.
- ASP.NET Core Identity + OpenIddict.
- Docker.
- OpenTelemetry.
- xUnit/Testcontainers.
- Playwright.
- S3-compatible object storage.
- Quartz.NET persistent scheduling.

## DESIGN NOW / BUILD WHEN NEEDED

- Redis.
- SignalR.
- OpenSearch.
- read replicas.
- DWH/ClickHouse.
- regional cells.
- advanced saga workflows.
- service extraction.

## DO NOT BUILD NOW

- Kubernetes.
- Kafka platform.
- generic plugin marketplace.
- microservice fleet.
- custom service mesh.
- data warehouse.
- separate database per microservice.

---

# 126. First Engineering ADRs

Before production module coding, create ADRs:

```text
ADR-001 Modular Monolith
ADR-002 PostgreSQL Multi-tenancy + RLS
ADR-003 RabbitMQ + MassTransit
ADR-004 Transactional Outbox/Inbox
ADR-005 Module Contracts & Dependency Rules
ADR-006 Schema-per-Module
ADR-007 In-process Transaction Coordinator
ADR-008 Identity/Auth/OpenIddict
ADR-009 React/Vite frontend architecture
ADR-010 API and OpenAPI versioning
ADR-011 UUIDv7 identifiers
ADR-012 Date/Time with NodaTime
ADR-013 Money/Quantity precision
ADR-014 Production migration strategy
ADR-015 Observability/OpenTelemetry
```

---

# 127. Definition of Done — Platform Skeleton

Platform skeleton is complete only when:

1. Developer can clone and run one command.
2. PostgreSQL/RabbitMQ/MinIO start.
3. API/Web/Worker start.
4. Login works.
5. Tenant context works.
6. RLS integration test passes.
7. One sample module registers through Manifest.
8. One DB transaction writes a sample aggregate + outbox.
9. Message arrives through RabbitMQ.
10. Worker consumes it once.
11. Duplicate delivery does not duplicate state.
12. Failed consumer goes to error handling.
13. Trace crosses API → DB → Outbox → RabbitMQ → Worker.
14. Metrics appear in Grafana/approved backend.
15. Migration command works separately.
16. CI reproduces all checks.
17. Architecture test blocks illegal project dependency.

Only after this should the first real Foundation module be implemented.

---

# 128. Developer Local Experience

Target:

```bash
git clone ...
./scripts/dev-up
```

should:

- start infrastructure.
- restore backend/frontend dependencies.
- initialize local DB.
- run migrations.
- seed local dev tenant.
- print URLs.

Then:

```text
Web       http://localhost:3000
API       https://localhost:7001
RabbitMQ  http://localhost:15672
Grafana   http://localhost:3001
MinIO     http://localhost:9001
Mailpit   http://localhost:8025
```

Exact ports are configurable.

---

# 129. Developer CLI

Create a small internal CLI later in Platform Skeleton:

```text
erp dev up
erp dev reset
erp db migrate
erp db seed
erp module list
erp module validate
erp tenant create
erp message replay
```

This reduces manual operational mistakes.

---

# 130. Module Generator

A module generator should create:

```text
implementation project
contracts project
manifest
DbContext
schema
migration folder
endpoint folder
consumer folder
unit test project/folder
integration test setup
architecture registration
frontend module shell
```

This ensures all modules start consistently.

---

# 131. Documentation Structure

```text
docs/

├── architecture/
│   ├── master-blueprint.md
│   └── technology-baseline.md
│
├── adr/
│
├── modules/
│   ├── directory/
│   ├── catalog/
│   ├── ledger/
│   └── ...
│
├── runbooks/
│   ├── deploy.md
│   ├── rollback.md
│   ├── restore.md
│   ├── rabbitmq-replay.md
│   └── incident-response.md
│
└── security/
    ├── threat-model.md
    ├── tenant-isolation.md
    └── secrets.md
```

---

# 132. Runbooks Required Before Launch

At minimum:

- Deploy.
- Rollback application.
- Rollback/forward-fix migration.
- Restore database.
- RabbitMQ consumer failure.
- Replay dead message.
- Outbox backlog.
- Government API outage.
- Object storage outage.
- compromised credential.
- tenant data export.
- break-glass support access.

---

# 133. Alerting Baseline

Alert on:

- API 5xx increase.
- p95 latency regression.
- PostgreSQL connection saturation.
- PostgreSQL disk.
- RabbitMQ memory/disk alarm.
- queue depth sustained.
- error queue messages.
- outbox backlog.
- worker crash loop.
- scheduled job failure.
- backup failure.
- certificate expiration.
- compliance submission backlog.

Avoid alerts on every single recoverable retry.

---

# 134. Broker Failure Behavior

If RabbitMQ is temporarily unavailable:

- Source transaction can still commit **if async effects are not required for transaction correctness**.
- Outbox retains pending messages.
- Outbox dispatcher retries when broker returns.
- User sees the core operation as successful if legally/business-wise safe.
- Operational dashboards show backlog.

If the external side effect is legally required before final user release, the domain state reflects `Pending`, not fake success.

---

# 135. Database Failure Behavior

If PostgreSQL is unavailable:

- writes fail.
- API returns service-unavailable behavior.
- workers stop acking messages until safe.
- no message is acknowledged before durable DB completion where DB state is required.

Never acknowledge a broker message and then hope a failed DB write can be recovered manually.

---

# 136. Consumer Transaction Rule

For a DB-writing consumer:

```text
Receive
  ↓
Begin DB transaction
  ↓
Inbox/idempotency check
  ↓
Business changes
  ↓
New outbox messages
  ↓
Commit
  ↓
Ack original message
```

This is the default reliability model.

---

# 137. User-Facing Async Operations

For long work:

```text
POST /imports
→ 202 Accepted
→ operation_id
```

Worker processes job.

Frontend:

```text
GET /operations/{id}
```

or later SignalR update.

Do not hold HTTP connection for a ten-minute import.

---

# 138. Business Continuity of Broker Messages

An integration event is not the only copy of core business truth.

Example:

`InvoicePosted` can be rebuilt from posted invoice state if required.

For critical projections/integrations, replay strategy is documented.

Broker retention is not accounting retention.

---

# 139. Data Export / Tenant Offboarding

Architecture must support:

- tenant-owned business export.
- attachments export.
- master data.
- transaction data.
- audit where contract/legal policy allows.

No module activation model may make historical data inaccessible to the platform internally.

---

# 140. Engineering Quality Gates Before Module 01

Before starting the first real module Requirements implementation, lock:

- [ ] technology baseline accepted.
- [ ] module template accepted.
- [ ] RabbitMQ topology accepted.
- [ ] MassTransit PoC works.
- [ ] Outbox/inbox PoC works.
- [ ] RLS PoC works.
- [ ] transaction coordinator PoC works.
- [ ] Identity/TenantContext works.
- [ ] API standards accepted.
- [ ] frontend shell works.
- [ ] OpenAPI client generation works.
- [ ] CI pipeline works.
- [ ] Testcontainers works.
- [ ] OpenTelemetry trace end-to-end works.

---

# 141. Source / Version Verification

The versions below were checked against official project sources on 2026-10-05:

- .NET support policy:
  https://dotnet.microsoft.com/en-us/platform/support/policy
- EF Core 10:
  https://learn.microsoft.com/ef/core/what-is-new/ef-core-10.0/whatsnew
- PostgreSQL releases:
  https://www.postgresql.org/docs/release/
- RabbitMQ release information:
  https://www.rabbitmq.com/release-information
- React versions:
  https://react.dev/versions
- Vite releases:
  https://vite.dev/blog
- TanStack Query:
  https://tanstack.com/query/latest/docs/framework/react
- MassTransit:
  https://masstransit.io/documentation

Dependency versions must remain pinned in source control and maintained through a controlled upgrade process.

---

# 142. Final Engineering Verdict

The recommended production baseline is:

```text
C# / .NET 10 LTS
ASP.NET Core 10
EF Core 10 + Npgsql
PostgreSQL 18

RabbitMQ 4.x
MassTransit
Transactional Outbox
Consumer Inbox / Idempotency
Quartz.NET

React 19.3
TypeScript
Vite 8.1
React Router
TanStack Query v5
React Hook Form
Zod
Headless UI Primitives (Spike-selected)
Platform DataGrid (Spike-selected)
Tailwind CSS v4
i18next

ASP.NET Core Identity
OpenIddict

S3-compatible storage
Docker
OpenTelemetry
Serilog
Sentry

xUnit
Testcontainers
WireMock.Net
Vitest
React Testing Library
Playwright

OpenTofu
GitHub Actions (default CI/CD)
```

Architecture:

```text
Multi-tenant Modular Monolith
+ Strong module ownership
+ Schema/DbContext per module
+ Synchronous contracts for invariants
+ RabbitMQ for asynchronous integration
+ Transactional Outbox
+ Idempotent consumers
+ Explicit module manifests
+ RLS tenant isolation
+ One codebase
+ Production operational discipline
```

## Core rule

> **Production-ready from Day 1 does not mean maximum infrastructure from Day 1.**

It means the decisions that are expensive to retrofit are solved from the beginning:

- tenant isolation,
- message reliability,
- module boundaries,
- authentication,
- authorization,
- audit,
- idempotency,
- transaction integrity,
- observability,
- backup/restore,
- migration safety,
- testing,
- CI/CD,
- security,
- versioned contracts.

And the infrastructure that is safe to add later remains deferred until it earns its complexity:

- Kubernetes,
- Kafka,
- Redis,
- OpenSearch,
- ClickHouse,
- Microservices,
- Regional Cells.

---

# 143. Next Step

The next implementation artifact should be:

> **PLATFORM KERNEL & MODULE SYSTEM — Requirements + Technical Design Pack**

It must turn this engineering baseline into concrete:

- database schemas,
- interfaces,
- message envelope code,
- RabbitMQ topology,
- MassTransit configuration,
- outbox/inbox implementation,
- tenant/RLS implementation,
- authentication flows,
- permission model,
- transaction coordinator,
- module manifest schema,
- entitlement lifecycle,
- observability conventions,
- bootstrap code,
- CI pipelines,
- Docker Compose,
- architecture tests,
- and acceptance tests.

Only after that pack is approved should the production repository skeleton be generated.
