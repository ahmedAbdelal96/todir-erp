# Module 28 Backend Execution State

## Authorized scope

Backend and database implementation for Module 28 on `dev`, including the full FP00–FP53 backend scope, owner-module contracts/adapters, transactionally safe Budget Control, planning APIs, migration/seed, security, jobs, operational evidence, and acceptance. Frontend files and unrelated modules are outside this mission.

## Completed and verified

- Added the `business_planning` schema migration and registered the module in the solution and manifest graph.
- Added deterministic Atlas/Delta planning seed data and registered the planning entitlement, module instance and administrator permissions.
- Added transaction-coordinator repositories and authenticated API routes for the implemented foundation.
- Added formula-DAG cycle rejection tests and deterministic DAG acceptance tests.
- Applied the migration and reran development seed on disposable database `planning_accept_7dd60d7f1806`.
- Verified the API with real cookie authentication, CSRF, PostgreSQL and RabbitMQ startup; seeded cycles, versions, grid and workforce data were readable.
- Added controlled dimension members/mappings, formula dependencies, submission units, exact spreading, weighted allocations, scenario sharing/adoption, version comparison, workforce import/cost impact, CapEx lifecycle actions, P&L query and forecast-accuracy calculation.
- Replayed migration and development seed on disposable database `planning_accept_ea22f9f5`, then dropped it; real authenticated HTTP verified dimension/member, formula, submission, exact spread, allocation conservation and P&L query.
- Added the Planning OpenAPI contract and Planning-specific static contract checks.
- Added a Planning-owned transactional Budget Control capability, forward-only schema fix for reservation kind/source currency and effective-dated policy fields, and optional source reservation integration in Purchasing and Expenses.
- Added annual/quarterly/monthly control-period normalization, published plan-version enforcement, control-basis-aware availability, negative-budget and shadow-mode decisions, and a strict Block opening-reconciliation guard.
- Added source-bound Budget Override request/decision APIs backed by Platform Approvals, exact source/version/currency matching, validity windows, bounded `used_amount`, audit evidence, and owner-module permission selection for planning approvals.
- Added canonical invoice Actual consumption and bounded correction facts for supplier credits, Expense reversal correction, and a durable Planning-owned reconciliation/rebuild run with tenant RLS and explicit runtime grants.
- Added fail-closed legal-entity, effective-policy, control-decision and currency matching checks; source amendments update the existing reservation, draft deletion releases it, and owner transactions roll back on blocked/approval-required decisions.
- Closed the optional-bucket bypass for Purchasing and Expenses: Planning now resolves an applicable bucket from legal entity, monthly period, metric and dimension context, validates explicit bucket selections, rejects uncontrolled/inactive selections, and fails closed for an active strict policy with no applicable bucket. Purchasing and Expenses use this resolution during create/update/finalize paths.
- Added PostgreSQL owner-path evidence for omitted-bucket Block behavior, invalid bucket rejection, conserved/idempotent partial consumption, bounded source-specific override use, Expense reversal correction, and projection reconciliation/rebuild (8/8 focused tests passing on disposable PostgreSQL).
- Added the minimum Purchasing-owned requisition aggregate and forward-only schema: draft/submission/approval, immutable line identity, partial/full conversion lineage, row locking, idempotency, precommitment replacement, PO approval history and outbox evidence. Fresh bootstrap ordering now places the requisition foundation before its history foreign key; disposable fresh migration plus Development Seed ran twice and completed with 273 ordered scripts.
- Added a durable Expense budget-reversal intent. The Expense remains `ReversalPending` when Ledger or Planning correction fails; a retry reuses the stored intent and does not post a second Ledger reversal. The retry path also avoids inserting a second pending intent. BC-T08 now fault-injects both the Ledger boundary and the post-Ledger Planning-correction boundary; persistence and final completion remain atomic in the same transaction.
- Added bounded public owner-fact contracts and Purchasing/Ledger providers. Posted `ExpenseRecognized` Ledger debit facts are exposed through the Ledger-owned provider using the canonical `ExpenseRecognized` semantic role, so Planning does not count the originating Expense row twice. Planning owner reconciliation compares canonical owner facts to each bucket and strict `Block` control requires a successful zero-discrepancy owner result in addition to the existing opening flag.
- Corrected availability and reservation evaluation after a successful owner reconciliation so the materialized owner projection is not double-counted with the same source reservation rows.
- Executed the real isolated Purchasing PR HTTP vector: `100,000 EGP` was approved and converted into approved `40,000` and `60,000` POs. The reservation ledger showed a released `60,000` PR precommitment and open `40,000` plus `60,000` PO commitments. Duplicate conversion was idempotent and over-remaining conversion was rejected.
- Executed BC-T05 with independent concurrent Purchasing scopes: one of two competing `80,000 EGP` approvals succeeded against a `100,000 EGP` strict bucket, one failed with `planning.budget_insufficient`, and no duplicate/orphan commitment was persisted.
- Repeated BC-T05 and BC-T06 sequentially on one disposable database after making their fixture cleanup and materialized reservation-total updates repeat-safe; the second run preserves the same one-success/one-rollback financial outcomes.
- Executed BC-T06 with a PostgreSQL owner-insert failure trigger: reservation and owner state rolled back together; a retry after trigger removal created exactly one source row and reservation.
- Executed BC-T08 with a controlled Planning correction failure after the canonical Ledger reversal: the durable reversal intent remained retryable and the new execution context completed with exactly one Ledger reversal and one Planning correction.
- Executed BC-T09 through authenticated cookie/CSRF HTTP with separate requester, approver and unauthorized identities; the approved exception was source/version bounded and consumed once.
- Executed BC-T10 with independent real Expense, approved PO and approved PR workflows. The fixture selects an unused future accounting year so repeated runs do not collide with prior owner facts. Authenticated reconciliation detected the initial `80,000 EGP` projection discrepancy, authenticated rebuild corrected it, a final zero-discrepancy run established readiness, and strict availability was `80,000 EGP`.
- Re-ran the local Outbox message → RabbitMQ → Worker → PostgreSQL Inbox Projects event path after synchronizing the fixture with the spawned Worker's actual `Application started` signal; the message applied once and replay remained idempotent.
- Corrected three source-integration defects found by that runtime vector: Purchasing approval decisions now require `purchasing.request.approve`; PR conversion reuses the existing source reservation and writes PO commitment against the resolved control bucket; Invoicing skips Planning consumption when no source reservation exists but still rejects a closed reservation. The real supplier-invoice/correction integration test then passed.
- Corrected the requested whitespace-only `LedgerRepository.cs` formatter finding without changing Ledger behavior.
- Closed the controlled-source invoice bypass: an active strict Planning policy now rejects finalization when the required source reservation is missing, while genuinely uncontrolled invoice sources remain outside Budget Control.
- Made supplier-invoice source identities unique per request (with a bounded digest and preserved project prefix) so partial and final invoices from one Purchase Order do not collide in Invoicing; the Worker project-event parser accepts the bounded suffix.
- Added Banking-owned semantic Ledger registrations and mappings needed by the seeded bank-originated posting contract.
- Made the real Ledger Worker acceptance fixture deterministic against startup log back-pressure; its focused local broker recovery test passes independently.
- Added and passed disposable PostgreSQL BC01 vectors for partial/final supplier invoices, cancellation release, missing-reservation fail-closed behavior, and the live RabbitMQ → Worker → PostgreSQL Inbox Project source-event path.

## Last meaningful corrections

- Corrected Npgsql positional parameter binding in the planning repository.
- Corrected scenario cloning to persist the generated clone identity and requested name.
- Corrected budget reservation idempotency so a retry does not double-count the same source key; reservation rows are the reservation ledger and are not also added to the aggregate precommitment column.
- Corrected transfer bucket resolution to use the source and target planning coordinates and reject same-bucket transfers.
- Preserved the legacy `/grids/spread` batch-write contract and exposed exact residual-conserving spreading at `/grids/spread-exact`.

## Remaining outside BC01

1. Preserve the separate broader Module 28 work items outside BC01: full receipt/return lifecycle, wider Ledger YTD mapping, Excel/rollup/BS/CF/forecast scope, and production operational certification.
2. The final isolated BC01 regression gate is complete after the last source/documentation changes: Planning/Purchasing/Invoicing/Expenses, Ledger 56/56 twice, authenticated HTTP/security, architecture, OpenAPI, migration/seed replay and Release checks all passed locally.

## Delivery status

`BC01 VERIFIED` for this focused mission. The broader Module 28 backend remains
`NOT COMPLETE` because capabilities outside BC01 are still partial/open. No
production certification is implied.

## BC02 closure checkpoint

The current BC02 implementation adds owner-contract Actual refresh, immutable
snapshot metadata/facts, versioned metric mappings, metric governance fields,
grid mutation guards, coordinate-scoped submission locking, Platform Approval
submission decisions, baseline lifecycle guards, revision idempotency and
deterministic transfer locking. It also adds a forward-only migration and an
idempotent OPEX Ledger mapping in the Development Seed.

Fresh local verification after the latest source changes passed: Release build,
Planning 4/4, Architecture 23/23, OpenAPI validation (Planning 78 paths), the
fresh-seed replay test 1/1, authenticated BC-T09 1/1, canonical BC-T10 1/1 and
the new owner-sourced Actual snapshot/replay test 1/1 on disposable PostgreSQL.

The BC02 acceptance matrix is `docs/engineering/BUDGETING_BC02_ACCEPTANCE_MATRIX.md`.
BC2-T02 duplicate recognition and BC2-T03 durable failed-refresh/restatement
now have repeatable disposable PostgreSQL evidence. Mapping lifecycle, fiscal
calendar, published-version alternate-route, transfer concurrency, department
submission, delegated approval, scoped confidentiality, BC01-after-final-change
and disabled-module vectors remain OPEN/PARTIAL. Therefore BC02 is `NOT
COMPLETE` and no full Module 28 or production certification is claimed.

## BC02 integrity-closure checkpoint

The latest closure adds the forward-only `planning-forward-fix-zz-bc02-integrity.sql`
migration. It introduces governed top-down targets, department proposals and
finance adjustments, preserves Actual source version/recognition/fiscal
provenance, scopes Actual-fact uniqueness to a snapshot so restatements remain
historical, and installs runtime database guards for published plan inputs and
immutable Actual snapshots. The Development Seed is explicitly allowed to
replay through the controlled `erp_owner` seed role; application role
mutations remain fail-closed.

`PlanningBc02ActualIntegrationTests` now creates a disposable canonical
ExpenseRecognized Ledger fact, refreshes it through the public Ledger owner
provider, proves source-only ingestion and replay idempotency, rejects
caller-supplied facts, recognizes a shared operational/Ledger identity once,
and preserves/replaces snapshots across a controlled source failure and
correction. Planning unit tests remain 4/4. The original BC2-T01 to BC2-T17
identities were restored in the acceptance matrix. T04-T17 still require
their mandatory business acceptance vectors and are not claimed as passed.

## BC02 P0 financial-integrity correction

The latest BC02 correction adds effective-dated Current Budget evaluation:
`Original + effective Approved Revisions + effective Approved Transfers In -
effective Approved Transfers Out`. Pending, rejected and future-dated rows are
excluded, and aggregate transfer netting is not duplicated from materialized
bucket columns. The disposable BC2-T08 regression proves the 100 + 20 - 5
revision vector, the 20-in/20-out transfer conservation vector, and an as-of
date before the changes.

Published plan versions now persist their Actual snapshot identity when they
are published. Grid, variance, driver and the new
`GET /api/v1/planning/versions/{id}/actual-basis` read path use that pinned
snapshot for historical analysis; a later owner refresh publishes a current
restatement without changing the pinned snapshot's mapping version, source
watermark, as-of date or state. This is covered by the disposable BC2-T03
historical-basis regression. The matrix remains `NOT COMPLETE` because the
independent T04-T17 acceptance vectors are still open.

The governance correction also exposes deterministic target/proposal lifecycle
routes. Proposal identity is returned with the target projection, and the
disposable BC2-T12 regression proves submit, adjust, approve, target approve
and target publish without losing the linked proposal history.

The latest guard migration extends published-version protection to planning
coordinates, targets and proposals while leaving structural Actual-refresh
coordinate creation available. Effective-dated transfer evaluation is now
used by both transfer approval availability and Budget Control bucket reads,
so a future approved transfer cannot consume today's available budget.

The same disposable acceptance class now proves the repository and PostgreSQL
boundaries for published-grid/Actual-snapshot write protection (BC2-T04) and
the draft → published → retired → replacement metric-mapping lifecycle
(BC2-T05). T06-T11 and T13-T17 remain open until their separate calendar,
workflow, concurrency, confidentiality, owner-matrix, BC01-regression and
lifecycle compatibility vectors are executed.

## Accelerated BC03–BC06 checkpoint — 2026-11-27

The accelerated backend pass added a constrained decimal formula evaluator with
allow-listed arithmetic, comparison and aggregate functions, durable complete
calculation runs with dependency ordering and trace rows, actual-cutoff rolling
forecast draft creation, bounded CSV preview/atomic Draft-only import, explicit
Planning-FX management rollup, and integrated P&L, balance-sheet and cash-flow
projection queries. The CapEx contract now persists a positive useful-life
months value through a forward-only migration.

Fresh evidence after those changes:

- Planning formula tests: 6/6 PASS.
- Disposable PostgreSQL integrated statement, balance-check, cash continuity,
  and CSV preview/commit vector: 1/1 PASS.
- BC02 plus the new BC03 integration vectors: 9/9 PASS.
- Release build: PASS with zero warnings/errors.
- Architecture governance: 23/23 PASS.
- Planning OpenAPI validation at that checkpoint: PASS with 82 paths.
- Fresh disposable migration and Development Seed replay at that checkpoint: PASS; 280 ordered
  platform scripts applied or verified.

These results prove the implemented slices only. The full Module 28 backend is
still `NOT COMPLETE`: OPEX ownership, complete source-module actual/commitment
lifecycles, closed-period and rolling-forecast acceptance, transfer and
submission matrices, Excel/rollup breadth, observability/runbooks, and the
remaining FP/BC03–BC06 acceptance vectors still require implementation or
evidence. No frontend, production, or full-module completion is claimed.

## OPEX workflow checkpoint — 2026-11-27

The next targeted correction added the Planning-owned `opex_plan_line` model
and forward-only migration with tenant RLS, bounded scope indexing and a
null-safe coordinate uniqueness index. `POST /api/v1/planning/opex` now accepts
fixed, variable, recurring and driver-based lines with category, account and
cost-centre references, explicit base/driver/rate inputs, growth and inflation
percentages, currency and provenance. `GET /api/v1/planning/opex` is bounded
and legal-entity scoped. Integrated P&L applies dedicated OPEX lines at their
category/account/cost-centre identity, replacing a matching generic coordinate
once while preserving unrelated generic OPEX coordinates in the same period.

The Development Seed now includes two repeat-safe OPEX examples per tenant.
The disposable PostgreSQL BC03 vector proves fixed 1,000 plus 10% growth equals
1,100, driver 10 × 20 plus 5% inflation equals 210, total OPEX equals 1,310,
and the resulting NetIncome equals 190. Migration/seed replay and the combined
BC02/BC03 integration selection passed after the change. OPEX remains `PARTIAL`
until source-owner baselines, full governance/security matrices and the broader
BC04/BC05/BC06 acceptance scenarios pass.

The calculation path now records the Planning input revision, takes a durable
transaction savepoint, rejects stale completion, preserves prior calculated
facts when execution fails, persists a Failed run and exposes an authorized
retry route. The spreadsheet boundary now accepts a bounded first-sheet XLSX
preview/commit path through the existing CSV mutation guard; macros, formulas,
external links and unsafe archive sizes are rejected. Authorized bounded XLSX
export and metadata-bearing empty template routes emit formula-safe text only.
The BC03 integration selection passed again after these changes, including a
generated XLSX preview and export/template assertions. Full staging,
failure-injection/restart evidence and the wider Module 28 acceptance matrix
remain open.

## Final targeted evidence — 2026-11-27

After the OPEX, calculation-recovery and XLSX changes, the final targeted gates
were rerun: Planning Release build passed with zero warnings/errors; Planning
unit tests passed 6/6; Architecture passed 23/23; the BC03 PostgreSQL
selection passed 1/1 after the identity-scoped OPEX correction; Planning
OpenAPI validation passed with 88 paths; migration policy passed for 284 SQL
files; and formatter plus
`git diff --check` passed. The incremental disposable database replayed the
281 ordered scripts and Development Seed successfully, including two OPEX
lines per tenant. These fresh results still do not close the remaining
mandatory FP00–FP53 acceptance gaps listed above.

## Owner-contract and observability continuation — 2026-11-27

The owner boundary now has concrete Payroll and Fixed Assets providers in
addition to the existing Ledger and Purchasing providers. Payroll publishes
finalized `PayrollPosted` accounting facts as OPEX Actuals; Fixed Assets
publishes posted depreciation-run lines as DEPRECIATION Actuals. Both providers
set tenant context, honor optional legal-entity scope, retain source identity,
recognition identity, currency and accrual basis, and are registered only by
their owning modules. This is a contract and registration closure slice; a
non-empty independent Payroll/Fixed Assets reconciliation fixture and the
remaining Sales, HR, Banking, Inventory, Manufacturing, Projects, Subledger
and FinBase source matrix are still required before FP00/FP08/FP52 can PASS.

Planning now emits bounded `Todir.Planning` OpenTelemetry metrics for actual
refresh duration/failure, calculation duration/failure, budget-check duration,
block decisions, CSV import failures and reconciliation workflow outcomes.
The existing observability pipeline exports the meter; live Prometheus and
alert evidence remains outstanding, so FP45 is PARTIAL rather than PASS.

## Owner financial integration checkpoint — 2026-11-28

The owner-source continuation now includes an Expenses provider that reads only
finalized, posted Expenses-owned rows and carries legal entity, period,
currency, project/task/category dimensions and stable recognition identity.
Payroll owner facts use the finalized gross amount rather than net pay. Fixed
Assets owner facts read only Fixed Assets-owned depreciation runs, derive the
currency from the owning book definition, and use the deterministic
`depreciation-run:{run}:book:{book}` identity shared with the canonical Ledger
posting. The Planning Development Seed now publishes OPEX and DEPRECIATION
metric mappings for Payroll, Expenses, Ledger and Fixed Assets.

`PlanningOwnerFinancialIntegrationTests` (`BC2-T15`) passed against the
disposable `bc02_closure_final_20261127` PostgreSQL fixture after the final
source change. It creates and finalizes a real Payroll run, posts and reverses a
real Fixed Assets depreciation run, observes non-empty Expenses facts, matches
each Payroll/Fixed Assets/Expenses fact to the canonical Ledger identity and
amount, proves Planning refresh replay idempotency, and proves the posted
depreciation plus reversal nets to zero in the corrected snapshot. The fixture
uses an isolated future fiscal year/period and retains only immutable posted
financial evidence; transient asset/depreciation rows are cleaned up.

This closes the non-empty Payroll/Expenses/Fixed Assets evidence gap for the
implemented owner boundary but does not close the broader Module 28 mission.
Sales/CRM, HR, Banking, Inventory, Manufacturing, Projects, Subledger and
FinBase owner-source matrices, FP38 CFO views, FP40 multi-entity rollup, FP45
live Prometheus evidence, and the remaining BC02/FP acceptance vectors remain
PARTIAL or OPEN in the coverage matrix. Delivery status remains `NOT COMPLETE`.
