# Module 28 Backend Execution State

## Authorized scope

Backend and database implementation for Module 28 on `dev`, including the planning schema, tenant/RLS enforcement, development seed, core planning APIs, the completed metadata/submission/spread/allocation slices, and focused backend verification. Frontend files and unrelated modules are outside this mission.

## Completed and verified

- Added the `business_planning` schema migration and registered the module in the solution and manifest graph.
- Added deterministic Atlas/Delta planning seed data and registered the planning entitlement, module instance and administrator permissions.
- Added transaction-coordinator repositories and authenticated API routes for the implemented foundation.
- Added formula-DAG cycle rejection tests and deterministic DAG acceptance tests.
- Applied the migration and reran development seed on disposable database `planning_accept_7dd60d7f1806`.
- Verified the API with real cookie authentication, CSRF, PostgreSQL and RabbitMQ startup; seeded cycles, versions, grid and workforce data were readable.
- Added controlled dimension members/mappings, formula dependencies, submission units, exact spreading, weighted allocations, scenario sharing/adoption, version comparison, workforce import/cost impact, CapEx lifecycle actions, P&L query and forecast-accuracy calculation.
- Replayed migration and development seed on disposable database `planning_accept_ea22f9f5`, then dropped it; real authenticated HTTP verified dimension/member, formula, submission, exact spread, allocation conservation and P&L query.

## Last meaningful corrections

- Corrected Npgsql positional parameter binding in the planning repository.
- Corrected scenario cloning to persist the generated clone identity and requested name.
- Corrected budget reservation idempotency so a retry does not double-count the same source key; reservation rows are the reservation ledger and are not also added to the aggregate precommitment column.
- Corrected transfer bucket resolution to use the source and target planning coordinates and reject same-bucket transfers.
- Preserved the legacy `/grids/spread` batch-write contract and exposed exact residual-conserving spreading at `/grids/spread-exact`.

## Required next executable work

1. Complete the `OPEN` and `PARTIAL` rows in `BUDGETING_BACKEND_COVERAGE_MATRIX.md`, especially owner adapters, integrated statements, rolling forecast, Excel interop and reconciliation.
2. Add source-owned contracts/adapters and real cross-module event/transaction tests.
3. Add scoped submission-coordinate locking, delegated approvals, full typed DAG execution, OPEX and multi-entity rollups.
4. Rerun the final disposable migration/seed, API, security/RLS, architecture, Release build, formatter, OpenAPI and diff gates after the last source change.

## Delivery status

`NOT COMPLETE` until the mandatory gaps are implemented and supported by repeatable evidence. No production certification is implied.
