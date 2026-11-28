# Module 28 Backend-to-Frontend Handoff

This is a backend handoff for the implemented planning foundation plus the metadata, submission, spreading, allocation and statement slices. Frontend implementation is not included in this mission.

## Runtime contract

Base route: `/api/v1/planning`. Requests require authenticated tenant context, the relevant `planning.*` permission and an active Planning module lifecycle. Cookie writes require the existing CSRF header. Tenant and legal-entity scope are enforced server-side.

Implemented route groups:

- cycles and lifecycle transitions;
- models, model versions and plan versions;
- sparse grid query/write-batch/spread/paste-preview;
- scenarios;
- assumptions and values;
- baselines, revisions, transfers and budget control;
- workforce, CapEx, revenue drivers, working capital and FX;
- actual refresh run status and variance query;
- calculation/recalculation and commentary.
- dimension members/mappings and constrained formula definitions;
- submission-unit state transitions;
- scenario share/adopt-preview and version comparison;
- exact spreading (`/grids/spread-exact`), weighted allocation, P&L query and forecast-accuracy calculation;
- workforce baseline import/cost-impact, category/driver-based OPEX (`POST/GET /opex`) and CapEx lifecycle actions.
- rolling forecast (`POST /forecasts/roll`), bounded CSV preview/commit (`POST /grid/import/preview`, `/grid/import`) and explicit-Planning-FX management rollup (`POST /reports/rollup`).
- safe first-sheet XLSX preview/commit (`POST /grid/import/xlsx/preview`, `/grid/import/xlsx`); macro, external-link, formula and oversized archive inputs fail closed;
- authorized bounded XLSX export/template (`GET /grid/export/xlsx`, `/grid/template/xlsx`) with explicit template metadata and formula-safe text cells;
- failed calculation retry (`POST /calculation-runs/{id}/retry`) reruns only a durable Failed run against the current input revision; calculation results retain an input revision and stale completion is rejected.
- owner-source registration now includes the existing Ledger/Purchasing providers plus Payroll finalized accounting facts, Expenses finalized accounting facts and Fixed Assets posted depreciation lines; the BC2-T15 disposable PostgreSQL workflow proves non-empty Payroll/Expenses/Fixed Assets facts, canonical Ledger identity matching, replay idempotency and depreciation reversal correction. Planning remains read-only at this boundary and does not access owner tables directly;
- Planning publishes bounded `Todir.Planning` OpenTelemetry metrics for Actual refresh, calculation, budget checks, block decisions, import failures and reconciliation outcomes through the existing platform pipeline;
- Planning OpenAPI contract: `docs/api/planning-openapi.json` (91 paths; static validation is separate from business acceptance). The backend also exposes `GET /dimensions`, `GET /dimensions/{id}/members`, `GET /submissions`, `POST /reports/department-planner`, `POST /reports/cfo`, and `GET /variance/{coordinateId}/decomposition`.
- Purchasing and Expenses request contracts may carry `budgetBucketId` as a validated selection hint. The backend remains authoritative: it normalizes the annual, quarterly, or monthly control horizon, resolves the applicable active bucket, rejects cross-entity/context/uncontrolled selections, and fails closed for active strict policies with no applicable bucket. Owner transactions are rolled back for blocked/approval-required decisions.
- Budget Control now normalizes annual, quarterly and monthly horizons, requires a published plan version, preserves source currency, and supports `POST /budget/overrides`, `GET /budget/overrides/{id}`, and `POST /budget/overrides/{id}/decision`. An override is a single source/document/line/version exception, is Platform-Approval governed, expires when configured, and is consumed transactionally; it never changes the approved Budget.
- Purchasing now exposes `/legal-entities/{entityId}/purchase-requisitions` create/read/submit/approve/reject/convert routes. Conversion is source-versioned and idempotent; the backend preserves PR line identity and replaces remaining precommitment with PO commitment.
- The isolated Development runtime acceptance procedure uses `ASPNETCORE_ENVIRONMENT=Development` and `DOTNET_ENVIRONMENT=Development` for the approved development OpenIddict signing material, a disposable migrated/seeded PostgreSQL database, the runtime `erp_app` role, and `TODIR_COOKIE_SECURE_POLICY=None` for local HTTP. Do not commit any of these credentials or signing artifacts. Release API output must be used for acceptance (`dotnet run --configuration Release --no-build`); Debug output can omit newly added routes.
- `POST /budget-control/reconcile` loads bounded owner facts from Purchasing and Expenses, compares Actual/Commitment/Precommitment by legal entity, period, metric and currency, persists a source watermark and discrepancy, and optionally applies a bounded projection rebuild. Strict `Block` control requires a successful zero-discrepancy owner result as well as the existing opening flag. This is still a backend contract, not frontend product acceptance.

## BC01 verified evidence

- BC-T05 and BC-T06 prove concurrent Purchasing control and owner-transaction rollback with real PostgreSQL locks, reservations and outbox state.
- BC-T08 proves durable Expense reversal recovery at both the Ledger-failure and post-Ledger/Planning-correction boundaries: the retry is idempotent and ends with one effective reversal and zero Actual. The correction/final-completion update is transactionally atomic rather than separately fault-injected.
- BC-T09 proves the authenticated override journey through cookie/CSRF HTTP and Platform Approvals with separate requester, approver and unauthorized identities.
- BC-T10 proves an independent real-source opening vector in a repeat-safe unused future year: Expense `40,000 EGP` Actual, approved PO `60,000 EGP` Commitment, approved PR `20,000 EGP` Precommitment, and `80,000 EGP` availability. Authenticated reconciliation detects the initial projection discrepancy, rebuilds it, and a final zero-discrepancy run permits strict readiness.
- The Ledger owner provider maps the canonical `ExpenseRecognized` debit semantic role; reconciled availability and reservation evaluation do not count materialized owner totals twice.
- BC2-T15 proves a real finalized Payroll run, posted Fixed Assets depreciation and reversal, and finalized Expenses owner facts reconcile to canonical Ledger identities exactly once. The Fixed Assets provider reads only Fixed Assets-owned tables and derives its deterministic run/book identity; immutable posted financial evidence is retained on the disposable database.

## BC02 backend additions

- Actual refresh reads registered owner financial-fact providers. Caller-supplied
  facts are rejected; the persisted watermark is computed from authoritative
  source identity/version/amount data. Published snapshots are immutable
  metadata plus source facts, and grid/variance reads use the latest published
  snapshot for the plan version.
- Metric mappings are versioned and have Draft/Published/Retired lifecycle;
  the Development Seed publishes the Ledger `ExpenseRecognized/OPEX` mapping.
- Grid writes reject oversized/duplicate batches, stale plan versions,
  Actual/Calculated/Source metrics and coordinates covered by Submitted,
  Approved or Locked submission units.
- Submission coordinate attachment is legal-entity and plan-version scoped;
  submit requests a Platform Approval and approval validates the owner entity,
  state and approval identity before locking the slice.
- Baseline lifecycle, revision idempotency and deterministic transfer locking
  are server-side contracts. These additions are not a claim that every BC02
  scenario is acceptance-complete; use the BC02 matrix before building product
  flows around a capability.

## Development seed

The disposable/development seed provides, per tenant:

- one FY2027 monthly annual-budget cycle;
- one active integrated model and published model version;
- one published base plan version and shared Base scenario;
- Revenue input of 150,000 EGP for 2027-01;
- a published 150,000 EGP baseline and Warn control bucket;
- aggregate workforce planning of 12 FTE at 18,000 EGP average cost;
- one 45,000 EGP prioritized CapEx item;
- two seeded OPEX lines: fixed Facilities and driver-based Utilities with explicit source and category metadata;
- one 100 × 1,500 EGP revenue driver, working-capital assumptions and USD/EGP planning FX.

## Permission boundary

The administrator seed role receives the canonical Module 28 permissions. Other roles do not receive planning access by default. The frontend must treat permission results as visibility guidance only; the API remains authoritative and must handle 401/403/lifecycle errors without technical details.

## BC02 governance handoff status

The backend exposes the first governed top-down/bottom-up slice through
`POST /api/v1/planning/targets`,
`POST /api/v1/planning/targets/proposals`, and
`POST /api/v1/planning/targets/proposals/{id}/adjust`. It persists the
management target, original department proposal, calculated gap, adjusted
amount, reason, actor and audit event. These routes are draft-plan scoped and
do not overwrite the original target or submitted proposal. Authenticated
approval/publication evidence for BC2-T12 remains open; frontend work must not
present this slice as final BC02 acceptance until the matrix is green.

Actual snapshots now carry source version, recognition identity and fiscal
basis in addition to source module/reference, line, mapping version and
watermark. Published plan inputs and Actual snapshot facts are protected by
both repository guards and PostgreSQL triggers. Development seed writes use
the controlled owner role only.

## Not yet safe to build against as final product scope

The broader Module 28 scope still has separate gaps: coordinate-scoped submission locking, configurable delegated approvals, full typed DAG execution and recovery evidence, complete owner-source lifecycle/reconciliation breadth, closed-period rolling forecast evidence, complete PO receipt/return product lifecycle, XLSX staging/permissions matrix, multi-entity reconciliation, live planning observability and complete accuracy reporting. The statement response is an integrated planning projection, not a statutory statement; it still requires broader canonical opening-balance and source-owner acceptance before product claims. Do not create UI claims for broader capabilities until the backend matrix is updated with PASS evidence.

## Recovery closure update — 2026-11-28

The backend recovery environment is reproducible with a disposable PostgreSQL
database, Release DbMigrator/Development Seed, runtime `erp_app`, administrative
`postgres`, local RabbitMQ, and both `ASPNETCORE_ENVIRONMENT` and
`DOTNET_ENVIRONMENT` set to `Development`. Use Release output for acceptance;
the earlier Debug output omitted current Planning seed mappings. A fresh
migration-order defect in the Purchasing requisition forward-fix was corrected
with a forward-only bootstrap script.

Fresh evidence now includes Planning BC02 8/8, Planning 22/22 with one
documented skip, BC03 1/1, owner financial 2/2, Package C 7/7, authenticated
Budget Control HTTP 1/1, BC01 budget-control 4/4, Projects cross-module 14/14,
seed replay 1/1, architecture 23/23, security 7/7, OpenAPI validation, and a
zero-warning Release build.

This is not a final backend product handoff. The closure register still has
P0 evidence gates for published all-route immutability, concurrent transfers,
submission assignment/isolation, delegated approvals and expiry, batch
atomicity, confidentiality, complete owner lifecycle reconciliation, disabled
owner compatibility, and complete BC01-after-change identity coverage.
