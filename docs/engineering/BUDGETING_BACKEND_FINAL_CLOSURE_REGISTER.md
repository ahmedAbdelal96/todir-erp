# Module 28 Backend Closure Register

Status: `NOT COMPLETE`

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

This register is the frozen backend backlog for the Module 28 recovery mission. It records only actionable release blockers or evidence gates. Existing green slices remain preserved and are not restated as backlog items.

## Audit basis

The audit compared the canonical FP00–FP53 scope with the current Planning module, `business_planning` baseline and forward-fixes, public routes, owner-source contracts, Development Seed, Planning integration tests, BC01/BC02/BC03 acceptance tests, and current `dev` source at `c063cf1b`. The working tree was clean at audit start. No frontend files are in scope.

The first recovery run also found two reproducibility defects: a fresh
alphabetical migration run referenced the Purchasing requisition table before
its owner migration created it, and Debug DbMigrator output did not contain
the current Planning seed mappings. The forward-only Purchasing base migration
below and the Release-only acceptance command correct those issues without
changing production configuration.

## Frozen remaining work

| Category | Requirement | Actual gap / release blocker | Required correction | Acceptance criterion | Priority |
|---|---|---|---|---|---|
| TEST_ONLY | BC2-T07 / FP06 / FP09 | Published-version immutability has repository and database guards, but no complete authenticated all-route regression proves that every published input, calculated, source, baseline, revision, transfer, scenario and reference route rejects mutation. | Add one disposable PostgreSQL authenticated matrix covering the published version and each mutation family; retain draft mutation proof. | Every published mutation is rejected; draft mutation remains allowed; no row changes. | P0 |
| TEST_ONLY | BC2-T09 / FP10 | Transfer conservation and deterministic bucket locking are implemented, but concurrent transfer acceptance is not proven. | Add two-transaction PostgreSQL transfer vector including same-bucket, insufficient-source and replay cases. | Exactly one conserving approval outcome per idempotent transfer; no overspend or orphan delta. | P0 |
| TEST_ONLY | BC2-T10 / FP11 | Submission coordinate scoping and locks exist, but no authenticated planner/manager matrix proves assignment isolation and post-submit coordinate locking. | Add two-actor HTTP/DB acceptance with distinct department coordinates and Platform Approval decision. | Unassigned actor cannot read/mutate the slice; submit locks only attached coordinates; approval/lock preserves history. | P0 |
| TEST_ONLY | BC2-T11 / FP41 | Platform Approvals is the governing approval engine, but delegated approval, expiry and segregation-of-duties evidence is absent. | Add approval-provider acceptance using separate requester/approver, delegated approver and expired delegation identities. | Self-approval and expired/unauthorized delegation fail; valid delegated approval succeeds and is audited. | P0 |
| IMPLEMENTATION_REQUIRED | BC2-T13 / FP07 / FP11 | Batch guards exist, but the source does not yet have a complete transaction-level race/rollback proof for duplicate coordinates, explicit zero, stale version and mixed legal entities in one mutation. | Harden or prove the write-batch transaction as required by the failing vector; add concurrent rollback acceptance. | One atomic outcome; duplicate/stale/mixed-scope requests reject without partial rows; explicit zero remains distinct from missing. | P0 |
| TEST_ONLY | BC2-T14 / FP41 | Route and persisted scenario/entity guards exist, but the required department/private/payroll/source confidentiality matrix is not executed end to end. | Add authenticated cross-tenant, cross-entity, private-scenario, sensitive-workforce and owner-source HTTP matrix. | Unauthorized reads and writes return denial without data leakage or technical details. | P0 |
| TEST_ONLY | BC2-T15 / FP08 / FP52 | Owner providers and normalized reconciliation exist, but the complete canonical source lifecycle matrix and zero-difference result across required owners is not proven after the latest changes. | Execute fresh posted/finalized/reversal/correction vectors for Ledger, Purchasing, Expenses, Payroll, Fixed Assets, Projects, Banking, Inventory, Manufacturing, Sales, CRM, HR, Subledger and FinBase. | Each source fact appears once with stable identity/version/watermark; corrections reconcile to zero unexplained difference. | P0 |
| TEST_ONLY | BC2-T16 / FP53 | BC01 evidence predates the latest BC02/source-scope changes. | Rerun the complete BC-T01–BC-T12 selection on the reusable acceptance database after the last source change. | BC01 matrix remains green with exact financial assertions. | P0 |
| TEST_ONLY | BC2-T17 / FP41 | Planning lifecycle guards exist, but disabled/not-entitled owner workflow compatibility has no runtime acceptance. | Add disabled and not-entitled owner-module vectors with Planning read/refresh/control routes. | Disabled or unavailable owner capability fails closed; no partial reservation or snapshot is created. | P0 |
| TEST_ONLY | FP03 / FP04 / FP09 / FP10 / FP13–FP30 | Core implementation exists, but several canonical exit gates have only focused slices rather than complete repeatable business vectors: mapping lifecycle, baseline lifecycle, revision/transfer, formula trace, workforce privacy, CapEx boundaries, FX and statement basis. | Add the missing focused acceptance vectors without changing working contracts. | Each vector asserts persistence, immutability, provenance, conservation and deterministic output from real PostgreSQL. | P1 |
| TEST_ONLY | FP37–FP40 / FP42 / FP45 | Department/CFO/rollup routes, audit and Planning metrics are implemented; live authorization, drill-through, audit-history and Planning-specific telemetry evidence is incomplete. | Execute authenticated report/drill-through and metrics scrape vectors; verify audit rows and bounded metric labels. | Every reported KPI has coordinate/source basis; audit is durable; metrics are emitted without tenant/document high-cardinality labels. | P1 |
| OUTSIDE_BACKEND_SCOPE | FP47–FP50 product acceptance | Human UX and frontend acceptance are separate gates; backend provides the contracts and seed but must not claim Product/UX acceptance. | Preserve as handoff dependency only. | No backend completion report claims frontend or Product Owner acceptance. | P1 |
| OUTSIDE_BACKEND_SCOPE | FP44 enterprise-scale certification and FP46 production operations | The canonical pack requires bounded local performance/runbook evidence, not enterprise-scale certification. Existing platform runbooks cover shared operations; Planning-specific recovery evidence is limited to tested semantic retries. | Record only Planning-specific recovery steps required by the P0 workflows; do not build an enterprise load program. | Local bounded-query/index review and semantic retry/rebuild commands are reproducible. | P1 |

## Evidence completed during this recovery mission

- The forward-only Purchasing base migration now creates the requisition
  parent tables before the existing history forward-fix; a fresh disposable
  database applied 282 ordered scripts successfully.
- Release `DbMigrator --seed development` populated the current Planning seed,
  including 22 metrics and 30 mappings per tenant; the seed replay gate passed
  1/1 with forced RLS.
- Fresh Release PostgreSQL evidence passed: Planning BC02 actuals 8/8;
  Planning 22/22 with the documented Manufacturing skip; BC03 analytics 1/1;
  owner financial integration 2/2; Package C forecast/reporting 7/7;
  authenticated Budget Control HTTP 1/1; architecture 23/23; security 7/7;
  OpenAPI validation; and the Release backend build.
- The BC01 budget-control selection passed 4/4 (`BC-T05`, `BC-T06`, `BC-T09`,
  `BC-T10`). The full Projects cross-module acceptance selection passed 14/14
  after its asynchronous evidence assertion was made eventual; this is
  source-owner evidence, not a claim that every BC01–BC12 identity was
  rerun.

These results close the stale-seed and migration-ordering blockers and refresh
the listed slices. They do not close the remaining P0 acceptance rows above.

## Verified / preserved boundaries

The following are not backlog items: tenant/RLS baseline, Planning schema ownership, Development Seed replay, basic cycle/model/grid/assumption/driver routes, BC01 budget check/reservation/override/Expense recovery/Purchasing PR conversion slices, actual refresh idempotency/restatement guards, Scenario access guards, rolling forecast bounded copy, CSV/XLSX safety gates, integrated statement golden vector, and the release/build/architecture/OpenAPI checks already documented. They must be rerun when affected by a later change.

## Reproducible acceptance environment

Use one disposable PostgreSQL database and the existing DbMigrator/Development Seed path for the full mission. Required configuration is `ASPNETCORE_ENVIRONMENT=Development`, `DOTNET_ENVIRONMENT=Development`, runtime `TODIR_DATABASE_CONNECTION`, administrative `TODIR_P04_ADMIN_CONNECTION`, the approved development seed password, local RabbitMQ, and `TODIR_COOKIE_SECURE_POLICY=None` for local HTTP. Values and signing material stay outside the repository. Do not run isolated Planning tests against an unmigrated database.

## Resume checkpoint

- Last source checkpoint: `c063cf1b5a7f105f4d5f1528c4fc3789d1948a18`.
- Current work package: P0 acceptance closure for BC02 and BC01-after-change regression.
- Completed in this mission: independent audit; frozen register; forward-only migration-order fix; reusable migrated/seeded Release database; fresh Planning, BC01 budget-control, source-owner, seed, architecture, security and OpenAPI evidence; and the asynchronous source-event acceptance correction.
- Next executable action: add and execute the missing authenticated P0 vectors for published all-route immutability, concurrent transfers, assigned submission isolation, delegated/expired approval, batch atomicity, confidentiality, complete owner lifecycle reconciliation, and disabled/not-entitled owners. Correct the first demonstrated in-scope defect rather than rerunning an unchanged failure.
- Delivery remains `NOT COMPLETE` until all P0 rows have fresh evidence after the final source change.
