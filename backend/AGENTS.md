# Backend engineering operating manual

Read the repository root `AGENTS.md` first. This file governs backend work and is sufficient, together with the root, to take an approved module from discovery to `BACKEND COMPLETE`.

Never create a new file or folder whose basename contains `todir` in any casing; backend names must describe responsibility rather than product branding.

## 0. Backend map

```text
backend/
├── Api/            # HTTP host and composition root
├── Worker/         # Consumers, scheduled jobs, and long-running work
├── DbMigrator/     # Controlled migration execution
├── Core/           # Platform Kernel and reusable technical capabilities
├── Modules/        # Business modules and owned contracts/persistence
├── Shared/         # Small genuinely shared technical concepts
└── Tests/          # Architecture, Core, Integration, Security, and module tests
```

`Api/` owns startup, middleware, authentication/authorization wiring, OpenAPI, health, and module registration; it is not a business-logic location. `Worker/` owns message consumers, scheduled jobs, external async processing, and long-running work; domain rules remain in the owning module. `DbMigrator/` owns controlled, ordered migration execution; production API startup must not blindly migrate. All backend database assets live under `DbMigrator/Database/`; `ops/database` is forbidden. `Core/` owns platform capabilities and never depends on business modules. `Shared/` is reserved for small approved technical concepts such as primitives, contracts, and building blocks; business concepts such as Customer, Product, Invoice, Employee, or Warehouse stay in their modules. `Tests/` protects all boundaries and runtime behavior.

The approved backend technology is the existing ASP.NET Core/.NET modular monolith, PostgreSQL, REST/JSON/OpenAPI, MassTransit/RabbitMQ, Quartz where approved, and the existing host/migration topology. Do not add parallel infrastructure.

## 1. New module start checklist

Before writing code, record answers to these questions in the work plan or approved module documentation:

1. What business problem does this module solve?
2. Who owns its domain data, and what does it explicitly not own?
3. What modules does it depend on and what modules depend on it?
4. What are the key aggregates/entities and their identities?
5. What lifecycle states and allowed transitions exist?
6. What invariants must always hold?
7. Which operations require one transaction?
8. Which events/integrations exist and which are immediate versus asynchronous?
9. What data is tenant-owned, legal-entity-scoped, global, or reference data?
10. What historical data must remain immutable and how are corrections represented?
11. What searches, lists, sorts, filters, and projections are required?
12. What volumes, concurrency, retention, and operational workloads are realistic?

Do not code while ownership, source of truth, transaction semantics, or required business decisions are unclear. Before module work, read the exact current module pack under `docs/modules/`, the Platform Kernel pack, master blueprint, production engineering baseline, relevant ADRs, and actual backend/OpenAPI contracts.

### Mandatory canonical Module Pack gate

Backend work must begin from the exact current module pack under `docs/modules/`. Record the pack path and reconcile its documented business purpose, domain model, ownership boundaries, workflows, lifecycle transitions, invariants, permissions intent, integrations, edge cases, MVP scope, deferred scope, development seed scenarios, acceptance scenarios, and Definition of Done against the current database, backend implementation, tests, and development seed. The Module Pack is the authority for **WHAT** and **WHY**; this manual is the authority for **HOW**.

Implement every documented backend capability that is in scope, including missing work discovered during reconciliation. Preserve the documented module ownership, invariants, tenant boundaries, transaction semantics, and cross-module contracts. Add realistic, deterministic development seed coverage for every user-visible scenario required by the pack. A passing build, existing API, or passing current tests is not enough for `BACKEND COMPLETE`; that status requires documented backend scope and seed coverage to be complete, or each uncovered item to be explicitly deferred by the Product Owner with its reason and impact recorded. Do not invent behavior or silently reinterpret an unclear requirement; report the exact unresolved documentation or contract gap.

### Backend ownership and evidence loop

Own the documented backend business scope, not merely the endpoints that today's frontend calls. Reconcile each required workflow against domain rules, lifecycle and invariants, PostgreSQL schema and constraints/indexes, RLS and permissions, repositories, services, APIs/contracts, migrations, Development Seed, integration and audit/event behavior, automated tests, and real runtime results. Use the root coverage matrix to record a PASS/FAIL for every required capability. Diagnose and correct defects in these owned layers, then rerun the affected flow and required gates; do not hand ordinary failures back to the user as a request for the next fix.

Make normal engineering choices within the approved business and architecture model. If an API or seed scenario needed by a documented user task is missing, implement it in the backend mission instead of narrowing the documented scope. For product ambiguity, inspect the employee's actual workflow and relevant market patterns when useful, while preserving the Module Pack's locked ownership, financial, security, and lifecycle decisions. Escalate only a material unresolved business or architectural choice; continue independent work meanwhile.

## 2. Physical module structure

Use only folders that contain real responsibility:

```text
Controllers/
Services/
Repositories/
Entities/
DTOs/
Validators/
Data/
Permissions/
Events/
```

Do not create empty folders or introduce `Application/`, `Infrastructure/`, `Features/`, `Commands/`, `Queries/`, `Handlers/`, `UseCases/`, or `Abstractions/` trees by default. An additional folder requires a concrete approved responsibility. The default flow is `Controller → Service → Repository → Data/Database`; entities protect domain invariants.

## 3. Domain modeling

Entities represent real business concepts, not only database rows. Define identity, lifecycle, allowed transitions, required and optional facts, invariants, uniqueness, activation/deactivation or cancellation behavior, and historical requirements. Prefer behavior-rich entities where state transitions matter; do not create public-setter data bags. Keep HTTP, DTO transport, repository access, and persistence orchestration out of entities.

A module is a coherent capability across domain, application workflow, persistence, API/contracts, permissions, tests, operations, and documentation. Do not copy policy between modules. Do not use a customer fork or an ownership bypass.

## 4. Database design order

Design in this order:

```text
business model
→ data ownership
→ conceptual relationships
→ logical model
→ normalization
→ physical PostgreSQL schema
→ constraints
→ indexes
→ transaction/concurrency design
→ query/access design
→ controlled migration
```

Never begin by creating EF entities blindly. Each module owns its PostgreSQL schema/data. A business module must not write another module's schema or use its `DbContext`, repositories, or tables. Cross-module foreign keys are forbidden unless an explicit architecture decision approves them; stable identifiers and contracts are the normal boundary. Do not sacrifice module ownership for a convenient SQL join.

## 5. Normalization and source of truth

Default to a sound 3NF-style relational model for transactional ERP data:

- one fact has one authoritative storage location;
- do not duplicate editable business facts;
- do not use comma-separated values or JSON for normal relational facts;
- replace repeated columns such as `Phone1`, `Phone2`, and `Phone3` with an appropriate child structure;
- avoid giant universal tables and separate independent concepts;
- model real many-to-many relationships with junction tables;
- model history explicitly when business history matters.

Denormalize only for a measured read-performance need, with clear ownership, an explicit consistency strategy, and a known canonical source. Never denormalize merely because it seems faster.

## 6. Keys, relationships, and lifecycle

Choose identifiers deliberately. Use stable surrogate identifiers for persistent entities where appropriate; business numbers and codes are attributes, not automatically primary keys. Mutable natural keys must not be primary keys. Public/document numbers need appropriate scoped uniqueness and internal IDs are never user inputs. Tenant-owned uniqueness normally includes tenant and, where applicable, legal entity scope, for example `UNIQUE (tenant_id, normalized_code)`.

Model one-to-one, one-to-many, and many-to-many cardinality explicitly. Avoid nullable foreign keys that hide unrelated relationship types and polymorphic foreign keys where a clearer relational model exists. Name relationships after business meaning and define delete behavior intentionally. Tenant-safe relationships should use composite tenant-aware constraints where PostgreSQL can enforce that a row in Tenant A cannot reference a row in Tenant B; application checks alone are insufficient.

Do not apply one generic soft-delete rule to every record. Master/reference data commonly uses Active/Inactive. Transactional or legal records commonly require cancel, reverse, archive, and retained history. Deactivate is not delete. Never casually hard-delete legally or audit-relevant records.

## 7. Types, nullability, and constraints

Choose PostgreSQL types deliberately. Use `numeric/decimal` with approved precision and scale for money and financial values; never binary floating point for money. Use `timestamptz`/the approved instant type for real moments and `date` for business dates that are not timestamps. Do not store numbers as text, booleans as strings, or unlimited text everywhere by habit.

NULL has business meaning. Required facts are `NOT NULL`; optional columns are nullable only when absence is genuinely valid. Never use `0`, empty string, or `1900-01-01` as missing-value sentinels.

Database constraints are part of correctness. Use appropriate `PRIMARY KEY`, `FOREIGN KEY`, `UNIQUE`, `CHECK`, and `NOT NULL` constraints for positive quantities, valid ranges, status-compatible fields, ownership, and scoped business uniqueness. FluentValidation and application checks complement but do not replace database invariants.

## 8. Indexes, search, and bounded access

Indexes come from real workloads. For each important list/search/lookup determine predicates, tenant/legal-entity scope, joins, ordering, pagination, and uniqueness. Tenant scope frequently leads composite indexes; match column order to filters and sorting; index real foreign-key lookup paths; use unique indexes for business uniqueness; use partial indexes when only active/current rows matter. Avoid duplicate or redundant indexes and indexing every column because indexes cost storage and writes. Review high-volume paths with `EXPLAIN`/`EXPLAIN ANALYZE` when appropriate.

Choose B-tree, GIN, `pg_trgm`, or full-text search from evidence. Do not use expensive `%contains%` scans on large tables without a suitable strategy. Search normalization must be deterministic and Arabic-safe where applicable.

Every potentially large list API is bounded with a default and maximum page size, explicit sorting, and filtering. Use offset pagination when sufficient; use keyset/cursor pagination when high-volume stability requires it. Never return an entire large table or load it into memory.

## 9. Transactions, concurrency, numbering, and audit

Set transaction boundaries at the business-operation level. A required invariant and atomic accounting posting remain in one transaction; do not make them eventually consistent merely because messaging exists. Keep transactions as short as correctness allows and avoid external network calls while a database transaction is open.

Design for concurrent ERP users. Protect against lost updates, duplicate creation, double posting, duplicate numbers, and reservation/oversubscription conflicts with unique constraints, optimistic concurrency, atomic updates, justified locks, and idempotency. Never rely on users not clicking twice. Business numbers are not generic IDs; gapless/legal numbering uses an approved controlled strategy, scoped by tenant, legal entity, document type, or other required business boundary. Ordinary identity sequences are not a legal gapless guarantee.

For sensitive changes preserve actor, time, operation, and the required before/after or business event. Audit is not a substitute for modeling business history. Never log passwords, tokens, secrets, credentials, or sensitive personal data unnecessarily.

## 10. Tenant isolation and RLS

Tenant isolation is defense in depth, not a convention. Never trust client-provided tenant, organization, user, or permission values without server-side authorization. Every tenant-owned table and query preserves tenant context and approved composite tenant-safe relationships. PostgreSQL RLS is enabled and `FORCE ROW LEVEL SECURITY` is used wherever required by the Platform Kernel. The runtime role must not bypass RLS; missing tenant context returns no unauthorized data; tenant context is set safely per request/transaction. Verify this with real PostgreSQL, never only mocked filters or ORM predicates. Never weaken RLS or security tests to gain convenience or parallelism.

## 11. Migrations and reference data

`DbMigrator` owns migration execution. The database system is intentionally a squashed baseline and must remain stable:

- The only migration root is `backend/DbMigrator/Database/bootstrap/`.
- The current baseline consists of `runtime-roles-and-schemas.sql`, one `<module>-schema.sql` per registered module, and the final `platform-rls-finalization.sql` pass.
- `platform-kernel-schema.sql` is the complete Platform Kernel baseline and creates objects only in `platform`; it must not be split into `platform_tenancy`, `platform_iam`, `platform_org`, or other capability schemas.
- Every foundation or business module owns one layer-prefixed schema and one baseline file. A new module is not allowed to write another module's schema.
- `platform_migration.applied_script` identifies a migration by `(module_id, script_name)` and stores the checksum. Migration identity must use the file name, never an absolute or repository path.
- Never edit a baseline or an applied migration after it is used by a shared environment. Future changes use a new `<module>-forward-fix-<description>.sql` file, are additive/expand-contract, and are discovered by `DbMigrator` in deterministic order.
- `platform-rls-finalization.sql` runs after all module baselines so cross-module foreign-key creation can complete before `FORCE ROW LEVEL SECURITY` is applied. Tenant-owned tables must end with both RLS enabled and forced.
- SQL baselines must not contain application seed data, destructive reset commands, `DROP SCHEMA`, or references to the retired `ops/database` path.
- Only `DbMigrator` may create or alter application database objects. API and Worker startup must never migrate, seed, squash, or repair the database.
- A baseline squash/reset is allowed only before shared or production environments exist, with explicit owner approval and a fresh database. Once a shared environment exists, preserve its migration chain and add forward-fixes instead of deleting history.

Migrations are deterministic, idempotent where the repository strategy requires it, forward-safe, production-aware, and tested against an empty database and existing data. Add indexes/constraints safely and consider lock time and runtime for large tables. Do not casually rewrite or destroy production data and do not let API startup blindly migrate production.

Reference data has explicit ownership and versioning. Do not seed mutable customer business data as code migrations. Country-specific legal/reference data belongs in the approved country/provider architecture.

## 12. Backend layers

Controllers own HTTP routing, binding, authorization boundaries, service calls, and response mapping. They must not contain business rules, `DbContext`, SQL, Npgsql, or persistence logic.

Services orchestrate use cases, entities, focused repositories, approved module contracts, transactions, permissions, audit, and Outbox behavior. They must not contain raw persistence logic, HTTP concerns, SQL, Npgsql, or giant unrelated workflows.

Repositories own focused persistence access and expose business-useful patterns, not raw “query everything” escape hatches. Do not create an automatic `GenericRepository<T>` and do not hide business policy in queries. `Data/` owns database-specific configuration, contexts, mappings, migrations, and setup. DTOs are explicit requests/responses and never persistence entities; do not mirror database columns blindly. Validators cover input shape; domain invariants and database constraints remain separate protections.

## 12.1 Development seed data

Canonical realistic development data is a maintained backend deliverable, not a migration fixture or disposable demo. Seed infrastructure lives only in `backend/DbMigrator/SeedData/` and is run explicitly with `DbMigrator --seed development`; API and Worker startup must never seed implicitly. The command is accepted only in an explicitly configured `Development`, `Test`, or `Testing` environment and must fail immediately for Production or an unspecified environment. Local development credentials are read from the ignored shared `backend/appsettings.Development.json` under `Seed:`, alongside the shared connection string; they are never printed or committed and are hashed through the same ASP.NET Identity password path used by normal account writes. CI or other controlled environments may provide an equivalent configuration source without changing seed code.

Development seed data is deterministic, idempotent, stable-ID based, Arabic-first, realistic, and valid. It maintains the two canonical tenants `شركة أطلس للتجارة` (`atlas.com`) and `شركة دلتا للتوريدات` (`delta.com`), seeds both tenants for every implemented user-visible module, respects module ownership and the real dependency order, and never invents future-module facts. It must include meaningful pagination, Arabic/English search variation, sorting/filter variation, active/inactive lifecycle states, and other valid edge states. Tenant-owned writes must preserve RLS and explicit tenant context; real runtime verification must prove Atlas cannot see Delta and Delta cannot see Atlas. Seed reruns ensure their own records without deleting arbitrary developer data.

Every future user-visible business module must add its own `<ModuleName>Seed.cs` stage and focused seed verification before `BACKEND COMPLETE`. A backend module is not `BACKEND COMPLETE — READY FOR FRONTEND` until both backend implementation and `DEVELOPMENT SEED READY` have passed. Performance/load generators are separate from normal development seed data, and Tax or any other unimplemented module must not receive seed data in advance.

For every backend module mission report, include the canonical Module Pack path, documented backend scope, documented acceptance scenarios, MVP and deferred items, database/backend/seed coverage, and unresolved gaps. Do not report `BACKEND COMPLETE` while any documented in-scope capability or required seed scenario is missing.

## 13. Authorization, authentication, APIs, and errors

Backend authorization is authoritative. Authentication derives one tenant from the login domain and binds the account to that tenant for the session. Check the tenant-bound authorization subject, role/permission, legal-entity scope, and business-state permission where applicable. There is no organization-selection or tenant-switching authentication flow. UI hiding is not security.

Authentication APIs model the user journey. Normal login begins with email and password, not `TenantId`, `OrganizationId`, or internal IDs. The backend parses the email domain, resolves exactly one active tenant, and continues automatically. If an initial password change or MFA is required, return its distinct short-lived challenge/state. MFA is not a random optional field on the initial login form.

Design REST/JSON/OpenAPI around business capabilities, not automatic database CRUD. Keep routes and contracts stable, list endpoints bounded, and error responses consistent `ProblemDetails` with stable machine-readable codes and frontend-usable context. Do not expose SQL errors, stack traces, table names, ORM language, database constraint names, or unnecessary internal identifiers; translate failures into business/application meaning.

## 14. Messaging and integration

Use RabbitMQ/MassTransit only for genuine asynchronous integration. Use transactional Outbox/Inbox where applicable, make consumers idempotent and retry-aware, and preserve trace/correlation context. Events do not replace normal synchronous method calls; immediate invariants use synchronous contracts. Post-commit integration must not report success before its required transaction commits.

## 15. Performance and test order

Before backend completion review query count, N+1 behavior, unnecessary includes, selected columns, indexes, list bounds, payload size, repeated round trips, transaction duration, and cache correctness. Caching must preserve tenant isolation. Measure before introducing special optimization.

Use this verification order as applicable:

1. domain/unit focused tests;
2. repository/database tests;
3. service/use-case tests;
4. API/integration tests;
5. security/RLS tests;
6. architecture tests;
7. runtime/messaging tests.

Use real infrastructure for infrastructure boundaries and deterministic isolated data. Do not sleep for synchronization when deterministic signaling exists. Do not mock away the database security boundary and do not weaken tests to make them pass.

## 16. Backend completion gate

Before reporting `BACKEND COMPLETE` or `BACKEND COMPLETE + DEVELOPMENT SEED READY`, verify: ownership and non-ownership are correct; invariants and transitions are modeled; schema is normalized; relationships, nullability, types, constraints, and indexes are correct and justified; migrations pass on a fresh disposable PostgreSQL database; the canonical Development Seed produces every required scenario on that database; real PostgreSQL RLS and permissions pass; API/OpenAPI and stable errors pass; architecture passes; performance-sensitive queries are reviewed; integration/idempotency/retry behavior is covered; audit/observability is adequate; the documented workflows run through the real API; required backend gates are green; and no frontend code was added to a backend-only mission. Inspect the fresh-database and runtime results yourself. A known required failure remains backend work, not a completion report.

## 17. Durable module ownership

`Directory` (`foundation_directory`) is the single writer for Party identity, Person/Organization identity, roles/classifications including Customer/Supplier, identifiers, contact points, addresses, archive/restore, and merge redirects. It does not own sales, purchasing, pricing, tax, accounting, inventory, customer/supplier duplicate masters, or user identity merely because a Party is a customer.

`Catalog` (`foundation_catalog`) is the single writer for product templates, variants, SKU, barcodes, units of measure and product-specific conversions, categories, brands, aliases, translations, references, readiness/search metadata, and archive/restore. It does not own selling or purchase price, tax rate, stock quantity, accounting accounts, preferred supplier, sales workflow, or purchasing workflow.

`FinanceBase` (`foundation_finbase`) owns currencies, money/rounding behavior, legal-entity currency, exchange rates, financial policies, payment terms, payment schedules, and approved legal-entity finance settings. It does not own Chart of Accounts, Fiscal Periods, Ledger posting, Tax, invoice lifecycle, payment lifecycle, or duplicate money/rounding implementations.

`Sample` is a small real runtime-verification module for module registration, API integration, Worker/MassTransit, schema/migrations, Outbox/Inbox, and integration behavior. It is not a business-domain template; do not copy its business semantics into real ERP modules.
