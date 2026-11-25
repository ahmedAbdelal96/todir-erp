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

## Last meaningful corrections

- Corrected Npgsql positional parameter binding in the planning repository.
- Corrected scenario cloning to persist the generated clone identity and requested name.
- Corrected budget reservation idempotency so a retry does not double-count the same source key; reservation rows are the reservation ledger and are not also added to the aggregate precommitment column.
- Corrected transfer bucket resolution to use the source and target planning coordinates and reject same-bucket transfers.
- Preserved the legacy `/grids/spread` batch-write contract and exposed exact residual-conserving spreading at `/grids/spread-exact`.

## Required next executable work

1. Complete the `OPEN` and `PARTIAL` rows in `BUDGETING_BACKEND_COVERAGE_MATRIX.md`, especially PR conversion/receipt/return owner adapters, canonical opening source reconciliation and activation, integrated statements, rolling forecast, Excel interop, and complete operational evidence.
2. Add source-owned contracts/adapters and real cross-module event/transaction tests for full Purchasing and Ledger replay paths.
3. Add scoped submission-coordinate locking, configurable delegated approvals, full typed DAG execution, OPEX and multi-entity rollups.
4. Complete the final disposable source-workflow acceptance and rerun all owner-module, integration, security/RLS, architecture, Release build, formatter, OpenAPI and diff gates after every later source change.

## Delivery status

`NOT COMPLETE` for overall Module 28 and `BC01 NOT COMPLETE` for this focused mission: the delivered BC01 slice now has source corrections, overrides, policy guards and rebuild evidence, but the canonical PR/PO/receipt/return lifecycle, owner opening reconciliation/strict activation workflow, full event replay, and complete cross-module acceptance matrix still lack required evidence. No production certification is implied.
