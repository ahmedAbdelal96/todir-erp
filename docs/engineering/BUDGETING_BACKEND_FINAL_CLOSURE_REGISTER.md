# Module 28 Backend Closure Register

Status: `BACKEND COMPLETE`

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

This register is the frozen backend backlog for the Module 28 recovery mission. It records only actionable release blockers or evidence gates. Existing green slices remain preserved and are not restated as backlog items.

## Audit basis

The audit compared the canonical FP00–FP53 scope with the current Planning module, `business_planning` baseline and forward-fixes, public routes, owner-source contracts, Development Seed, Planning integration tests, BC01/BC02/BC03 acceptance tests, and current `dev` source at acknowledged commit `21f013e39c367ca5be63b3655e1610a16dece763`. The working tree was clean at audit start. No frontend files are in scope.

The first recovery run also found two reproducibility defects: a fresh
alphabetical migration run referenced the Purchasing requisition table before
its owner migration created it, and Debug DbMigrator output did not contain
the current Planning seed mappings. The forward-only Purchasing base migration
below and the Release-only acceptance command correct those issues without
changing production configuration.

## Frozen remaining work

| Category | Requirement | Actual gap / release blocker | Required correction | Acceptance criterion | Priority |
|---|---|---|---|---|---|
| CLOSED | BC2-T07 / FP06 / FP09 | Published-version repository/database guards and all-route authenticated mutation matrix are covered. | `PlanningGovernanceConcurrencyAcceptanceTests` published-route matrix. | Every published mutation family rejects; draft-only paths remain available; no protected row changes. | P0 |
| CLOSED | BC2-T09 / FP10 | Deterministic transfer locks and concurrent conservation are covered. | `PlanningGovernanceConcurrencyAcceptanceTests` concurrent transfer vector. | One conserving approval outcome; replay is idempotent; no overspend or orphan delta. | P0 |
| CLOSED | BC2-T10 / FP11 | Authenticated assignment isolation and coordinate-level post-submit locking are covered. | `PlanningAccessGovernanceAcceptanceTests` planner/manager vectors. | Unassigned actor cannot read or mutate the slice; only attached coordinates lock. | P0 |
| CLOSED | BC2-T11 / FP41 | Platform Approval SoD, active delegation and expiry are covered. | `PlanningAccessGovernanceAcceptanceTests` independent approver and delegation/expiry vectors. | Self-approval and expired delegation fail; valid delegated approval succeeds. | P0 |
| CLOSED | BC2-T13 / FP07 / FP11 | Transactional batch duplicate/stale/zero/mixed-scope behavior is covered. | `PlanningGovernanceConcurrencyAcceptanceTests` batch rollback vector. | Invalid batch leaves no partial rows; explicit zero is preserved; stale and duplicate input reject. | P0 |
| CLOSED | BC2-T14 / FP41 | Resource-specific confidentiality is covered. | `PlanningAccessGovernanceAcceptanceTests` workforce permission, tenant and sensitive-data matrix. | Unauthorized reads are denied without data leakage or technical details. | P0 |
| CLOSED | BC2-T15 / FP08 / FP52 | Fourteen registered owner providers are scoped and replay-stable; real Payroll, Fixed Assets and Expenses lifecycle facts reconcile to Ledger identity, and corrected depreciation nets to zero. | `PlanningOwnerFinancialIntegrationTests` 2/2 plus `PlanningPackageAOwnerSourceRegistrationIntegrationTests` 1/1. | Stable identity/version/currency/scope evidence and zero unexplained correction discrepancy. | P0 |
| CLOSED | BC2-T16 / FP53 | BC01 regression was rerun after the BC02 source and lifecycle changes. | Budget Control 8/8, Projects cross-module 14/14, Purchasing/owner selections and BC-T09/BC-T10 focused gates. | Applicable BC01 financial assertions remain green. | P0 |
| CLOSED | BC2-T17 / FP41 | Disabled, ReadOnly and not-entitled owner compatibility is covered through authenticated Planning routes. | `PlanningAccessGovernanceAcceptanceTests` lifecycle compatibility vector. | Owner-dependent workflows fail closed with no partial refresh/reservation/snapshot. | P0 |
| CLOSED | FP03 / FP04 / FP09 / FP10 / FP13–FP30 | Typed dimensions, mapping lifecycle, baseline/revision/transfer, formula trace/recovery, workforce/CapEx, FX, statements, rolling forecast, scenario, variance, accuracy and import boundaries have repeatable focused evidence. | BC02 T01–T06/T08/T12, Planning unit 6/6, BC03 1/1 and Package-C 7/7. | Persistence, immutability, provenance, conservation and deterministic output are asserted on PostgreSQL. | P1 |
| CLOSED | FP37–FP40 / FP42 / FP45 | Scoped department/CFO/rollup reads, coordinate drill-through, durable Planning audit events and Planning-specific telemetry are covered. | Package-C 7/7 and `PlanningMetricsAcceptanceTests` 1/1; bounded query plans captured on the seeded database. | KPI rows carry coordinate/source basis; audit persists; metrics export without tenant/document labels. | P1 |
| CLOSED | FP47–FP53 backend responsibilities | Seeded small-company budget/control, department submission/delegation, FP&A forecast/reporting, CFO management statements/rollup, Purchasing budget control, fourteen owner providers/reconciliation and release evidence are complete. | BC2-T10/T11, Package-C 7/7, BC03 1/1, BC-T01–T12, owner/provider 3/3, and release gates below. | Backend contracts and repeatable evidence are complete; frontend/Product acceptance remains separate. Physical receipt and supplier-return records remain owned by Purchasing/Inventory. | P1 |
| OUTSIDE_BACKEND_SCOPE | FP47–FP50 product acceptance | Human UX and frontend acceptance are separate gates; backend provides the contracts and seed but must not claim Product/UX acceptance. | Preserve as handoff dependency only. | No backend completion report claims frontend or Product Owner acceptance. | P1 |
| OUTSIDE_BACKEND_SCOPE | FP44 enterprise-scale certification and FP46 production operations | The canonical pack requires bounded local performance/runbook evidence, not enterprise-scale certification. Existing platform runbooks cover shared operations; Planning-specific recovery evidence is limited to tested semantic retries. | Record only Planning-specific recovery steps required by the P0 workflows; do not build an enterprise load program. | Local bounded-query/index review and semantic retry/rebuild commands are reproducible. | P1 |

The FP44/FP46 backend-local portion is closed by the bounded performance
harness, seeded Planning query-plan review, and BC-T06/T08/T10 recovery
evidence. Enterprise-scale certification, production operations and human
frontend/Product acceptance remain explicitly outside this backend status.

## Evidence completed during this recovery mission

- The forward-only Purchasing base migration now creates the requisition
  parent tables before the existing history forward-fix; the recovery database
  applied 286 ordered SQL files successfully.
- Release `DbMigrator --seed development` populated the current Planning seed,
  including 22 metrics and 30 mappings per tenant; the seed replay gate passed
  1/1 with forced RLS.
- Fresh Release PostgreSQL evidence passed: Planning BC02 actuals 8/8;
  Planning 23/23; BC03 analytics 1/1;
  owner financial integration 2/2; Package C forecast/reporting 7/7;
  authenticated Budget Control HTTP 1/1; architecture 23/23; security 7/7;
  OpenAPI validation; and the Release backend build.
- The complete BC01 regression set BC-T01–BC-T12 is PASS in the authoritative
  BC01 acceptance matrix. The focused budget-control selection passed 4/4
  (`BC-T05`, `BC-T06`, `BC-T09`, `BC-T10`), and the full Projects cross-module
  acceptance selection passed 14/14 after its asynchronous evidence assertion
  was made eventual.

Fresh closure evidence after the final source/test changes: BC02 governance
12/12 across the new authenticated/concurrency classes; owner lifecycle and
provider registration 3/3; Planning metrics 1/1; Expenses budget-control
integration 8/8; Planning unit tests 6/6; Package-C 7/7; BC03 1/1; and the
bounded performance harness PASS. The focused calculation recovery acceptance
also passed 1/1 after correcting the calculation-run insert to persist
`plan_version_id`; stale worker runs are interrupted and retried, while live
runs remain non-retryable. The applicable BC01 and source-owner regressions
remain green.

## Verified / preserved boundaries

The following are not backlog items: tenant/RLS baseline, Planning schema ownership, Development Seed replay, basic cycle/model/grid/assumption/driver routes, BC01 budget check/reservation/override/Expense recovery/Purchasing PR conversion slices, actual refresh idempotency/restatement guards, Scenario access guards, rolling forecast bounded copy, CSV/XLSX safety gates, integrated statement golden vector, and the release/build/architecture/OpenAPI checks already documented. They must be rerun when affected by a later change.

## Reproducible acceptance environment

Use one disposable PostgreSQL database and the existing DbMigrator/Development Seed path for the full mission. Required configuration is `ASPNETCORE_ENVIRONMENT=Development`, `DOTNET_ENVIRONMENT=Development`, runtime `TODIR_DATABASE_CONNECTION`, administrative `TODIR_P04_ADMIN_CONNECTION`, the approved development seed password, local RabbitMQ, and `TODIR_COOKIE_SECURE_POLICY=None` for local HTTP. Values and signing material stay outside the repository. Do not run isolated Planning tests against an unmigrated database.

## Release consistency checkpoint — 2026-11-28

- Canonical coverage: FP00–FP53 are PASS for the applicable backend scope in
  `BUDGETING_BACKEND_COVERAGE_MATRIX.md`.
- BC01: BC-T01–BC-T12 remain the authoritative PASS set in the BC01 matrix;
  no verified BC01 behavior was reopened.
- BC02: BC2-T01–BC2-T17 and BC2-S01–S06 are PASS in the BC02 matrix; the
  Planning contract count is 91 paths and Planning unit coverage is 6/6.
- Owner-source lifecycle: fourteen providers are scoped and replay-stable;
  real Payroll, Expenses and Fixed Assets lifecycle facts reconcile to Ledger
  identity exactly once, with depreciation correction netting to zero.
- Purchasing boundary: Module 28 Planning controls requisition/PO
  commitment and reservation lineage. Purchasing/Inventory own physical
  receipt and supplier-return lifecycle; those records are not duplicated by
  Planning.
- Frontend handoff: backend responsibilities FP47–FP53 are complete; frontend,
  Product/UX, statutory and human acceptance remain separate gates.

## Resume checkpoint

- Last source checkpoint before this consistency review: `21f013e39c367ca5be63b3655e1610a16dece763`.
- Current work package: final backend acceptance and release verification.
- Completed in this mission: the frozen P0 governance matrix; forward-only migration-order and approval-delegation fixes; reusable migrated/seeded Release database; authenticated BC02 governance, owner-source, metrics and BC01 regression evidence; and the two Expenses acceptance fixture corrections required by real failures.
- Remaining product boundary: frontend implementation and human Product/UX acceptance are separate handoff gates; no frontend files were changed.
- Delivery is `BACKEND COMPLETE`.
