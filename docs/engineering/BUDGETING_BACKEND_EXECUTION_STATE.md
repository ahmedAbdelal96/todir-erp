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
- Added PostgreSQL owner-path evidence for omitted-bucket Block behavior, invalid bucket rejection, conserved/idempotent partial consumption, bounded source-specific override use, Expense reversal correction, and projection reconciliation/rebuild (7/7 focused tests passing on disposable database `bc01_closure_20261125`).
- Added the minimum Purchasing-owned requisition aggregate and forward-only schema: draft/submission/approval, immutable line identity, partial/full conversion lineage, row locking, idempotency, precommitment replacement, PO approval history and outbox evidence. Fresh bootstrap ordering now places the requisition foundation before its history foreign key; disposable fresh migration plus Development Seed ran twice and completed with 273 ordered scripts.
- Added a durable Expense budget-reversal intent. The Expense remains `ReversalPending` when Ledger or Planning correction fails; a retry reuses the stored intent and does not post a second Ledger reversal. The retry path also avoids inserting a second pending intent.
- Added bounded public owner-fact contracts and Purchasing/Ledger providers. Posted `ExpenseRecognized` Ledger debit facts are exposed through the Ledger-owned provider so Planning does not count the originating Expense row twice. Planning owner reconciliation compares canonical owner facts to each bucket and strict `Block` control requires a successful zero-discrepancy owner result in addition to the existing opening flag.
- Executed the real isolated Purchasing PR HTTP vector: `100,000 EGP` was approved and converted into approved `40,000` and `60,000` POs. The reservation ledger showed a released `60,000` PR precommitment and open `40,000` plus `60,000` PO commitments. Duplicate conversion was idempotent and over-remaining conversion was rejected.
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

## Required next executable work

1. Complete the approved-PO receipt/return/credit/reversal vector and authenticated Budget Control activation evidence.
2. Complete canonical Ledger YTD mapping beyond posted expense facts and runtime outbox/inbox replay acceptance using the isolated runtime role.
3. Complete source-watermark invalidation, semantic correction workflow and strict activation HTTP evidence; the current owner result is durable but not yet a complete approval/recovery workflow.
4. Complete the concurrency, failure-injection and runtime-role security matrix, then rerun all applicable owner-module, integration, architecture, Release build, formatter, OpenAPI and diff gates after the last source change.

## Delivery status

`NOT COMPLETE` for overall Module 28 and `BC01 NOT COMPLETE` for this focused mission: the PR conversion, partial/final/cancellation invoice vector, correction/replay and live Worker path pass, but mandatory receipt/return/credit/reversal coverage, competing-approval concurrency, controlled rollback/failure injection, canonical multi-owner reconciliation, and complete strict-activation evidence remain open. No production certification is implied.
