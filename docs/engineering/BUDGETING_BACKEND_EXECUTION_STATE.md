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
Planning 4/4, Architecture 23/23, OpenAPI validation (Planning 72 paths), the
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
