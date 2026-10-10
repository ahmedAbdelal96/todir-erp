# Business module execution manual

## Absolute filename and folder-name prohibition

Never create or introduce a new business-module source file, project file, or folder whose basename contains `todir` in any casing. This is absolute. Use responsibility-based names in the filesystem; product namespaces or existing assembly identifiers do not authorize new `todir` paths.

A business module is not complete at CRUD. It must follow its approved plan, preserve the common TODIR architecture, and meet every applicable exit gate. A module may not silently redefine financial or domain semantics, nor adopt a different architectural style because an agent prefers it.

## A. Governing documents to read

Read root and backend `AGENTS.md`, this file, the exact current module pack, Platform Kernel pack, Master Blueprint, production engineering baseline, relevant ADRs, and dependency public contracts. The module pack is authoritative for its domain; conflicts require stopping the affected decision and reporting it.

## B. Pre-implementation discovery

Identify purpose; owned and non-owned data; source of truth and single writer; dependencies and their direction; capabilities; tenant/legal-entity scope; permissions; invariants; synchronous and asynchronous interactions; public contracts; immutable states; external integrations; and expected access patterns.

## C. Module ownership model

The module owns only its approved data, rules, writes and lifecycle. Other modules consume approved public contracts, capabilities, events or ports. They never reach into implementation, DbContext, entities, repositories or tables. Keep hard dependencies acyclic and do not make shared helpers a hidden owner.

## D. Canonical module structure

When implementation is authorized, use this conceptual shape; create only folders justified by the module plan.

```text
backend/Modules/<Module>/
├── Controllers/   # HTTP transport and authorization boundary only
├── Services/      # use-case and business orchestration
├── Repositories/  # data access contracts and implementations when present
├── Entities/      # owned entities and invariants
├── DTOs/          # module request/response contracts
├── Validators/    # input and application validation when present
├── Data/          # persistence code when present
├── Permissions/   # module-owned permission definitions when present
├── Events/        # module-owned events when present
├── Tests/         # only module-local tests when justified
├── AGENTS.md
└── <Module>.csproj
```

Create only responsibility folders backed by real files. Do not create empty
template directories or restore `Application/`, `Infrastructure/`, `Domain/`,
or generic `Interfaces/` layers merely for convention.

## E. Dependency rules

Depend on contracts, capabilities, events and ports—not another module's internals. Preserve the approved dependency DAG. New dependencies, cross-module transactions, ownership transfers, or contract changes require review under the governing packs.

## F. Domain modeling rules

Model aggregates, entities, value objects, policies, domain services and events only where they express approved domain meaning. Put invariants where they can be enforced reliably. No business logic in Infrastructure or UI by convenience; no generic repository abstraction unless the architecture explicitly requires it; no speculative abstractions.

## G. Application and vertical-slice rules

Organize use cases as focused vertical features. Services orchestrate approved
use cases, validation and ports; they do not leak persistence detail or replace
entity invariants. Implement the plan sequence rather than jumping straight to
UI or CRUD.

## H. Persistence and database rules

Review schema and DbContext ownership, column types, nullability, constraints, unique constraints, ownership-valid foreign keys, decimal precision, timestamps/date semantics, concurrency and realistic scale. Keep persistence inside the owning module. Use transaction and database rules from the approved Platform Kernel/stack.

## I. Index and query-performance rules

Design indexes from actual reads and writes; account for composite-index ordering. Do not add equivalent-prefix duplicate indexes without justification. Avoid hidden N+1 behavior, unbounded queries and unnecessary round trips; batch and paginate where appropriate. Important or hot queries must be reviewable through query plans and index behavior when the phase supports it.

## J. Migration rules

Migrations follow approved schema ownership and migration strategy. Evaluate data safety, compatibility, rollback or expand/contract implications, ordering and operational impact. Never hide business data transformations inside an unreviewed migration.

## K. Tenant and RLS rules

Tenant isolation is a security boundary. Apply approved tenant predicates, legal-entity scope, RLS/session rules and composite-key defenses. Test cross-tenant and cross-entity denial behavior; background and integration paths must not bypass the approved context model.

## L. Permissions and security rules

Define permissions at the approved ownership layer; backend checks are authoritative. Validate inputs, protect outputs/PII, use least privilege, avoid sensitive logging, and audit security-critical behavior where required. Frontend visibility is not authorization.

## M. Transaction rules

Respect approved transaction boundaries and the in-process transaction coordinator. Immediate invariants use the approved synchronous approach. Do not create cross-module atomicity, distributed transactions, or partial-success semantics by assumption.

## N. Public contract rules

Public contracts are deliberate, versionable module surfaces. Keep requests, queries, events, DTOs and capabilities stable, minimal and owned. A contract change must consider consumers, compatibility, security and migration impact.

## O. Synchronous vs asynchronous interaction rules

Use synchronous contracts only for approved immediate invariants and query/command needs. Integrate asynchronously only after commit when the architecture calls for it. Do not turn a broker into a transaction coordinator or make every interaction asynchronous by fashion.

## P. Outbox, Inbox and idempotency rules

Use transactional Outbox, Inbox and idempotency mechanisms exactly where defined by the approved stack. Messages must retain required tenant/correlation context and consumers must be safe for duplicate delivery, retries and failure handling. Test duplicates, retry and outage behavior when applicable.

## Q. API conventions

Expose APIs only through the approved module/API surface. Follow approved validation, authorization, pagination, idempotency, versioning, OpenAPI and response conventions. APIs must not leak internal entities or sensitive implementation detail.

## R. Error handling

Do not swallow exceptions or report generic success after partial failure. Use approved stable machine-readable domain/business errors; preserve safe trace/correlation context and avoid exposing sensitive detail.

## S. Read model and search rules

Use read models/search only when their approved purpose warrants them. They are projections, not editable source of truth. Define freshness, rebuild, tenancy/security filtering, pagination, sorting and query cost; do not use a projection to evade ownership rules.

## T. Audit and observability

Add required audit evidence, structured logs, metrics and traces without sensitive data. Preserve correlation/causation where required. Audit logs are not application logs; operations must be able to diagnose important failure and integration paths.

## U. Frontend alignment

Coordinate with the corresponding frontend module plan and public API contracts. Backend owns business truth; frontend must not reproduce or bypass domain rules. Treat API, permissions, states and contracts as one end-to-end feature.

## V. Localization, Arabic and RTL

Where UI is applicable, provide approved localization and Arabic/RTL behavior from the first implementation, including money, quantities, dates, status and validation presentation. Do not defer RTL as a cosmetic patch.

## W. Test matrix

Select applicable domain, validation, architecture, integration/database, tenant/RLS, contract/API, authorization/security, messaging/idempotency/retry, read-model/search, frontend, E2E, performance and regression tests. Tests are deterministic and behavior-focused; never weaken them solely to make CI green.

## X. Documentation and runbook impact

Update canonical architecture/module/API/security documentation when behavior or contracts change. Assess operational runbooks for migrations, deployment, rollback, recovery, message replay and support impact. Do not create duplicate source-of-truth documents.

## Y. Definition of Done

A module is complete only when all applicable plan requirements and exit gates pass: manifest/registration, dependencies, capabilities, permissions, tenant/entity scope, persistence/schema/constraints/indexes/RLS, domain/invariants/use cases/validation, contracts/API/events, Outbox/Inbox/idempotency, audit/observability, search/import/export, frontend/localization/Arabic RTL, tests, security, performance and DB query/index review, documentation, runbook impact and module exit gate. Never claim "100% tested" without objective proof; use: "All required tests and exit gates defined for this scope pass."

## Z. Senior self-review and reporting

Use the required workflow: **DISCOVER, MODEL, PLAN, IMPLEMENT, TEST, REVIEW, FIX, RE-VERIFY, REPORT**. PLAN maps the approved module-pack sequence into concrete reviewable steps. Before reporting, review boundaries, ownership, integrity, security, tenant isolation, transactions, performance/query plans/indexes, tests, docs, observability, migration safety, temporary/debug artifacts and deviations. Report files changed, verification evidence, relevant security/performance/database considerations, risks and decisions needed.

## AA. Frontend platform alignment and business authority

1. **Backend as Sole Source of Truth:** The backend owns all domain state machines, financial arithmetic (tax liability, rounding, posting balances), inventory valuation, and authorization truth. The frontend is an accessible presentation and workflow tier; it must never be given independent financial or business calculation authority.
2. **OpenAPI as Authoritative Contract:** Backend endpoints must be documented with complete, accurate OpenAPI specifications. The frontend generates and validates its API client from these contracts.
3. **No Frontend Business Semantics Invention:** Do not design backend endpoints that force the frontend to reconstruct domain state or invent business rules. Provide rich, server-authoritative previews, exact decimal strings, and explicit multi-axis status values.
