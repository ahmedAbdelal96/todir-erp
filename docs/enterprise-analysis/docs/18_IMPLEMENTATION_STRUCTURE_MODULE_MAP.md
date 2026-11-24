# 18 — Proposed Code Structure, Ownership and End-to-End Dependency Map

**Status:** implementation blueprint **conditional on P0/P1**. This is not permission to create every folder immediately. Names must be reconciled with current .NET host/module conventions and accepted ADRs before code. The current runtime exposes only L1–L4 module manifests and one explicit operational Worker composition; an Analysis L6 module or second host is not yet an approved repository shape.

## A. Server code responsibilities

| Proposed component | Role | Explicitly forbidden |
|---|---|---|
| `AnalysisDefinitionCatalog` | Allowed typed definitions, validators, versions and Arabic label translation keys | Runtime arbitrary SQL or untrusted formulas |
| `AnalysisRequestService` | Authorization/entitlement/scope/filter/idempotency validation; records job | Calling owner write operations or executing heavy calculation in request |
| `AnalysisRunRepository` | Analysis-owned state and history (tenant+entity RLS), cursor queries | Direct read/write of Sales/Subledger tables |
| `AnalysisJobClaimer` | Narrow, fenced job claim; lease, fair scheduling, progress | Cross-tenant unrestricted query through `erp_app` |
| `AnalysisJobProcessor` | Bounded extraction, compute, quality checks, result publication | Running in operational API process for heavy workloads |
| `IAnalysisAlgorithm` | Versioned deterministic calculation over **approved fact DTOs** | Reimplementing official accounting/stock financial truth |
| `AnalysisSourceResolver` | Resolve module owner read capabilities, source watermark and cutoff | Importing module implementation assemblies |
| `AnalysisResultPublisher` | Atomically seal versioned output, evidence and recommendation set | Changing historic published results in place |
| `RecommendationRules` | Typed evidence-based proposal rules over validated metrics | Automatically posting business commands |
| `RecommendationReviewService` | Scope-safe review decisions and audit | Treating review as accounting/stock approval |
| `AnalysisStatus/Result API` | Safe status/results/history GET and explicit run POST | GET side effects or implicit recalculation |
| `OperationalHealth/Telemetry` | Job metrics/kill switch independent of business availability | Making core ERP health require AI worker |

**Implementation placement:** prefer one module implementation project (per `solution-boundaries.md`) and its optional Contracts project, not new Domain/Application/Infra projects per module. An analysis process role is a **host concern**, not another business domain; prove whether the existing Worker can be composed and deployed in that role, or obtain an explicit solution/deployment ADR for a new host. Required project graph and manifest semantics must be validated by architecture tests.

## B. Ownership matrix and allowed call direction

```text
[Sales/Inventory/Payments/Subledger/Banking existing owner modules]
  ↑ provide published scoped read capabilities, status/evidence, version
  │
[AnalysisSourceResolver / approved analysis process role]
  → [AnalysisDefinition/Algorithms/Rules]
  → [Analysis-owned Result/Evidence/Recommendation tables]
  → [Analysis GET API]
  → [Dedicated domain analysis route in React]

[React explicit manager POST] → [Analysis Request API] → [Job Store]

[Recommendation review] → [Analysis-owned review history]
[Future explicit action handoff] → [OWNER business Command/Approval]  (separate guarded step)
```

Never reverse the arrow to require daily Sales/Payments/Inventory calls to wait for Analysis. Do not attach AI to owner `SaveChanges`, stock posting or ledger posting. Existing module events and outbox flow remain semantically unchanged.

## C. DI/hosting boundaries

- Operational API host can register low-cost **request and history service** without starting compute loops or reading large data in startup/health probes.
- Operational `backend/Worker` continues existing event consumers and hosted services; no analysis CPU-heavy job hosted there.
- The approved analysis process role registers only required owner **read capabilities**, Analysis services, observability and a dedicated database pool/limits; do not initialize all commercial command consumers unnecessarily. Its exact host shape is **TO-VERIFY** against the current Worker composition.
- If owner read capability registration currently bootstraps heavy module commands, P0 must measure/investigate and propose a safe separate registration surface; do not bypass module boundaries.
- Worker deployment role should have independent restart/scale, small resource pool, kill switch and limited source access.
- At-most-attempt / once-final-publication semantics protected by DB fence and uniqueness, not by in-memory singleton locks.

## D. Database and migration convention

- New schema/module-owned tables use existing DbMigrator path and migration tests; RLS FORCE and unique indexes first.
- Any new owner projection is stored and managed **within that owner's schema or published read contract**, not created directly by Analysis in a source schema.
- Avoid runtime DDL, direct cross-schema writes, unbounded JSONB result blobs or heavy trigger changes on operational tables.
- No materialized view refresh tied to invoice posting or transaction commit unless **separately approved and benchmarked**; explicit user job may compute from a bounded owner read model as appropriate.
- Schema evolution is forward-only, additive and backwards-compatible; no destructive `DROP/DELETE` against normal development/prod data.

## E. Frontend structure (illustrative Sales route)

```text
frontend/modules/sales/
  routes.tsx                 # register authorized independent business route
  pages/sales-analysis-page.tsx
  features/analysis/         # module-specific filters/metric explanations
  services/                 # thin API client calls via shared runtime, scoped keys
  i18n/{ar,en}.ts           # natural translations
  tests/                    # route, permission, forms, no-auto-POST, result history
frontend/components/        # common analysis filter form / report history / evidence UI
frontend/app/router/destination-registry.ts
```

Keep `SalesAnalysisPage` within Sales **presentation**, while `Analysis` owns server-side job results. No business UI naming `IntelligenceAndAnalytics` and no new module sidebars. For pilot Collections, actual route may live under Subledger or business-facing customer receivables section; decide with product and current navigation ownership; never inject a ghost page into Payments merely because it has payment facts.

## F. Contracts and API mapping

`frontend type-specific request` → `AnalysisRunRequest DTO` → `AnalysisDefinitionCatalog` → `TenantScopedJob` → `OwnerFactsReadCapability` → `AlgorithmEngine` → `SavedRunResult` → `Result API/History UI`.

A specific analysis definition **must not contain a raw SQL string**. It must reference an approved source fact contract, algorithm implementation and filter schema version. A new module integration should generally add only: `definition`, `owner data read adapter if missing`, `algorithm`, `recommendation policy`, `module route` and `acceptance tests`. It should **reuse** the existing job/persistence/auth/history engine.

## G. Boundaries to test mechanically

- Compile-time project references comply with `backend/Tests/Architecture` graph.
- Frontend imports to other modules only via their root `public.ts`/`public.tsx`; shared components not private direct imports.
- Route is registered in existing effective-access registry; neither parent overview permission nor free-form CSS access governs it.
- No `Analysis` namespace referenced in transaction-writing owner API/services outside explicitly approved optional read links.
- No Analysis SQL contains `INSERT/UPDATE/DELETE` into another module schema; no raw read joins from Analysis into owner schemas.
- No API `GET` creates jobs or touches compute entry point.

## H. Detailed development iteration sequence for first approved P2 slice

1. Open current commit/source docs, prepare fixture demonstrating module+host dependency constraints.
2. Confirm domain owner interfaces and typed analysis definition; approve filter/permission codes.
3. Add migration + RLS + job store under feature gate; clean and incremental upgrade tests.
4. Implement HTTP run POST/idempotency + GET history/status, no compute yet; auth/negative tests.
5. Add the approved analysis process role and one **fixture-only** algorithm verifying lease/retry/results immutability and process isolation; do not add a new host until the P1 ADR authorizes it.
6. Add result/evidence/recommendation review contract and reliable publication transaction; concurrency/crash tests.
7. Add one module's analysis frontend with reusable components, Arabic copy and permission-driven routes.
8. Run real-stack & mixed-load tests and reconcile regression to P0; fix anything that violates gates **before calling complete**.
9. Update governance and frontend/backend skills where approved architecture changes affect future agents, without loosening existing rules; document support/runbook.
10. Final review/approval, then `origin/dev` commit/push under repo policy for **task-owned** changes only.

## I. Explicit cut scope for initial foundation

Build only **one** execution engine, one history store, one permissioned analysis route, one approved fact adapter, one sample deterministic analysis, one recommendation review path, tests and observability. Do **not** create empty "AI controllers" under all 24 modules or deploy unused Python workers.
