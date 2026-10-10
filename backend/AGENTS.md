# Backend engineering rules

This file governs all work under `backend/`; read the repository root `AGENTS.md` first.

Never create a new file or folder whose basename contains `todir` in any casing. Backend technical names describe responsibility rather than product branding.

## Responsibility map

```text
backend/
├── Api/            # HTTP host and composition root
├── Worker/         # Consumers, scheduled jobs, and background work
├── DbMigrator/     # Controlled migration execution
├── Core/           # Platform Kernel and reusable technical capabilities
├── Modules/        # Business modules and their owned contracts/persistence
├── Shared/         # Small approved cross-cutting technical contracts only
└── Tests/          # Architecture, Core, Integration, Security, and module tests
```

The backend is an ASP.NET Core/C# modular monolith using PostgreSQL, REST/OpenAPI, approved MassTransit/RabbitMQ messaging, Quartz where required, and the existing host/migration topology. `Api/` owns startup, middleware, auth wiring, OpenAPI, health, and module registration without business logic. `Worker/` owns consumers, scheduled jobs, and long-running async work without taking domain ownership. `DbMigrator/` owns controlled, ordered migrations; production API startup must not blindly migrate. Keep reusable platform behavior in `Core/`, domain behavior in the owning module, and do not recreate retired source roots or add parallel infrastructure.

## Boundaries and module ownership

- `Core/` owns platform capabilities such as tenant context, authorization primitives, transactions, audit, Outbox/Inbox, idempotency, files, notifications, imports, registry, and other approved shared kernel behavior. It must not depend on business modules.
- `Directory` (`foundation_directory`) is the single writer for Party identity, roles, identifiers, contact points, addresses, archive/restore, and merge redirects. It does not own customer/supplier workflows, accounting, inventory, sales, purchasing, or country-specific legal validation without an approved country pack.
- `Catalog` (`foundation_catalog`) is the single writer for product templates, variants, identifiers, categories, brands, units of measure, product-specific decimal conversions, and archive/restore. Every product has a default variant. It does not own pricing, tax, inventory, purchasing, or accounting fields.
- `FinanceBase` (`foundation_finbase`) owns currencies, legal-entity currency, exchange rates, rounding policies, financial policies, and payment terms. It does not own Ledger, Tax, Pricing, inventory, invoicing, payment execution, or posting behavior.
- `Sample` is a real runtime verification module for registration, API, Worker/MassTransit, schema/migrations, Outbox/Inbox, and integration behavior. Keep it small and do not copy its business semantics into real modules.
- Other modules consume public contracts/capabilities and never read another module's tables, entities, migrations, or repositories directly. Preserve the approved dependency DAG.

Before module work, read the exact current module pack under `docs/modules/`, the Platform Kernel pack, master blueprint, production engineering baseline, relevant ADRs, and actual API/OpenAPI contracts. A module is one coherent capability across domain, application, persistence, contracts/API, permissions, tests, operations, and documentation.

## Code and persistence shape

Use the narrowest cohesive structure needed by the module: `Controllers/`, `Services/`, `Repositories/`, `Entities/`, `DTOs/`, `Validators/`, `Data/`, `Permissions/`, and `Events/`. Create only folders that have real responsibility; do not create empty folders or default `Application/`, `Infrastructure/`, `UseCases/`, `Features/`, `Commands/`, `Queries/`, `Handlers/`, or `Abstractions/` trees without an exceptional approved reason. Controllers transport requests, authenticate/authorize, and map responses; they do not contain domain rules or persistence. Services orchestrate use cases, transactions, audit, and Outbox behavior; they must not issue SQL or access database connections directly. Repositories/data components own SQL, `DbContext`/Npgsql access, projections, and persistence mapping.

Keep entities and invariants in the owning domain. Keep DTOs/contracts explicit and stable; do not expose entities as API contracts. Validate commands at the boundary and in the domain. Use deterministic decimal precision, nullability, uniqueness, ownership-valid foreign keys, bounded pagination, and indexes tied to real access patterns. Review query shape and avoid N+1 and unbounded reads.

Every tenant-owned table and query must preserve tenant context and the approved composite tenant-safe relationships. Enable and force PostgreSQL RLS as required by the Platform Kernel. Writes use the approved transaction coordinator and preserve audit, idempotency, and Outbox guarantees. Posted accounting records and completed stock movements cannot be edited or deleted; use approved correction/reversal flows. Migrations are owned by the module, safe, ordered, and never hidden in arbitrary scripts.

API authorization is mandatory even when the UI hides an action. Return stable problem-details/error codes without leaking sensitive data. Messaging is post-commit where appropriate, idempotent, retry-aware, and observable. Never use customer forks or ownership bypasses.

## Testing and operations

Place cross-system tests under `backend/Tests/Architecture/`, `Core/`, `Integration/`, `Security/`, or `Modules/<module>/`. Tests use deterministic isolated data and cleanup, do not depend on live production services, and do not sleep when deterministic signaling is possible. Cover architecture boundaries, tenant isolation/RLS, authorization, API contracts, persistence, messaging, idempotency/retry, and meaningful end-to-end behavior as applicable. Shared database/broker integration suites must respect the repository's required serial execution.

Keep generated results in ignored artifact directories. Run focused tests plus solution build, architecture/security/database/API gates, and relevant module verification before reporting. Never weaken or delete a test to make a gate pass; fix the defect or stop at the governing conflict.
