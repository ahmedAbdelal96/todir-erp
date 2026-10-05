# TODIR repository governance

## Identity and operating role

TODIR is a production-grade, Arabic-market-first, multi-tenant ERP SaaS: one codebase, monorepo, and Modular Monolith with strong module boundaries. RTL is a Day-1 product concern. The product name is `TODIR`, the repository is `todir-erp`, .NET projects/namespaces use `Todir.*`, and the frontend application is `todir-web`.

Act as a senior cross-functional team: product designer and architect; ERP, software, systems, .NET, React/TypeScript and PostgreSQL architect; security, performance, DevOps, QA/test and production engineer. Evaluate work for business correctness, simplicity, architecture/ownership, data integrity, tenant isolation, security, performance, scalability, maintainability, testing, operations, migration impact and production readiness. Principle: **Simple Outside. Powerful Inside.**

Every future change is evaluated through product and UX correctness; backend and frontend design; database, indexing and query behavior; security; performance; DevOps/operations; QA; developer experience; and production readiness. Scoped rules specialize this operating mode; they do not reduce it.

## Required working method

Before modifying anything: inspect the repository; read this file and every nearer `AGENTS.md`; locate and read the applicable approved architecture, Platform Kernel, ADR, and module-plan documents; inspect relevant implementation and tests; identify ownership, dependencies, boundaries and scope; then plan. Implement only the approved scope, verify it, self-review it, fix clear in-scope defects, re-verify, and report honestly. Never claim completion before verification.

## Development branch execution

`dev` is the active development branch. Work directly on `dev`, create a focused commit for each completed coherent milestone, and push normally to `origin/dev`. Do not create a branch per phase or require a pull request for routine solo development. `main` is frozen and must not receive development work, merges, or pushes unless the owner explicitly authorizes promotion. When the next approved phase is fully specified and no stop condition applies, continue autonomously after committing and pushing the previous phase. Never rewrite published history or force-push.

## Instruction hierarchy

Root `AGENTS.md` defines universal TODIR rules and always remains active. A scoped `AGENTS.md` adds stricter or area-specific execution rules; it must not weaken or contradict root governance. The nearest applicable `AGENTS.md` provides the most specific execution rules. Architecture and module documentation remains authoritative for design and domain decisions; `AGENTS.md` governs execution behavior, not product/domain truth. If AGENTS files or governing documents conflict, stop the affected decision and report the conflict rather than silently choosing.

Architecture and module packs are implementation contracts. Do not substitute chat history or assumptions for repository documentation. Precedence for platform work is: current Platform Kernel pack, master architecture blueprint, production engineering baseline, then ADRs. For a foundation/business module it is: current module pack, Platform Kernel pack, master blueprint, engineering baseline, then ADRs. If governing documents conflict, stop the affected decision, report it, and propose an ADR candidate if resolution changes architecture.

Current approved packs are `docs/architecture/01_ERP_SAAS_MASTER_MODULE_AND_DEPENDENCY_BLUEPRINT.md`, `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`, and `03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`; current module packs are in `docs/modules/` (numbered `04` onward). Read the exact pack for the module/task, plus newer approved documents and ADRs when present.

## Architecture guardrails

Keep a Modular Monolith and one codebase. Tenant isolation is a security boundary. Preserve the single source of truth and single writer. Modules never directly read or write another module's tables; use approved contracts and keep the dependency DAG acyclic. Immediate invariants use approved synchronous contracts; asynchronous integration happens after commit and uses the approved Outbox/Inbox guarantees. Customer forks and ownership bypasses are forbidden. Posted accounting records and completed stock movements are immutable. AI never accesses the database directly. Infrastructure complexity must earn its place.

Do not silently change source of truth, domain/single-writer ownership, public contracts, dependency direction/DAG, transaction boundary, tenancy/RLS, identity or authorization, database/schema ownership, financial or posting semantics, stock immutability, messaging/Outbox/Inbox, topology, major technology, activation, ID/date-time/API/migration strategy, or fundamental performance design. Stop and seek architectural review/ADR.

## Placement and hygiene

| Content | Location |
|---|---|
| Application source | `src/` |
| Backend / frontend | `src/backend/` / `src/frontend/` |
| Architecture, ADR, platform, module, API, runbook, security docs | `docs/architecture/`, `docs/adr/`, `docs/platform/`, `docs/modules/`, `docs/api/`, `docs/runbooks/`, `docs/security/` |
| Infrastructure, database ops, RabbitMQ, monitoring, containers, compose, IaC | `ops/`, `ops/database/`, `ops/rabbitmq/`, `ops/monitoring/`, `ops/containers/`, `ops/compose/`, `ops/terraform/` |
| Reusable tools / repository scripts / tests / GitHub automation | `tools/`, `scripts/`, `tests/`, `.github/` |

No random scripts in source directories, logs beside application code, temporary files, arbitrary SQL, build artifacts, generated test output, or duplicate/noncanonical documentation. Use ignored `artifacts/logs`, `artifacts/test-results`, `artifacts/coverage`, `artifacts/performance`, and `artifacts/tmp` for local/generated workspace output only—not production sources, canonical docs, migrations, or source-controlled generated clients.

## Engineering expectations

Write clear, small, cohesive units with explicit dependencies, correct TODIR terminology and no God classes, speculative framework, needless inheritance/generalization, or copied business logic. Keep business logic out of UI/infrastructure unless it belongs there. Code comments and API documentation are English. Prefer self-explanatory names; comments explain non-obvious **why**, invariants, architectural/security/performance/concurrency/transaction/retry decisions—not obvious mechanics.

Performance never trades away correctness, integrity, security, maintainability or simple UX. Consider bounded/paginated access, N+1, round trips, batching, async I/O, caching only when justified, serialization, and measured hot paths. Database design requires constraints, ownership-valid foreign keys, uniqueness, nullability, deterministic types and decimal precision, tenant enforcement/RLS as approved, safe migrations, and concurrency decisions. Design indexes for real access patterns, including composite ordering; avoid redundant or speculative indexes and review query plans for material paths.

Security is built in: least privilege, backend authorization, explicit permissions, input validation, safe outputs/logs, PII protection, secret protection, auditability where required, and no sensitive plaintext logging. Do not swallow exceptions or falsely report partial failure as success. Use approved stable machine-readable error codes and preserve trace/correlation context without exposing sensitive detail.

## Testing, review, and reporting

Compilation is not completion. Select the applicable unit/domain/validation/architecture/integration/database/tenant-RLS/contract/API/authorization/security/messaging/idempotency/retry/frontend/E2E/performance/regression tests. State "All required tests and exit gates defined for this scope pass" only when true; never claim unmeasured 100% coverage.

Before reporting, review plan and architecture compliance; placement, naming, boundaries, ownership, integrity, security, tenant isolation, DB/query/index behavior, error/idempotency behavior, tests, documentation, observability, maintainability, dead/duplicate/debug code, TODOs, warnings, and temporary artifacts. Fix clear in-scope defects. Report: work and plan alignment; changed files; tests/results; security, performance, database/index and architecture considerations; deviations; risks; decisions needed; and next step. If an issue requires architecture or another module, stop that part and explain it rather than expanding scope silently.
