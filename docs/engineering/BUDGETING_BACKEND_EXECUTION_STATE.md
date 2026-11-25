# Module 28 Backend Execution State

## Authorized scope

Backend and database implementation for Module 28 on `dev`, including the planning schema, tenant/RLS enforcement, development seed, core cycle/version/grid/scenario/budget-control/driver/actual/variance APIs, and focused backend verification. Frontend files and unrelated modules are outside this mission.

## Completed and verified

- Added the `business_planning` schema migration and registered the module in the solution and manifest graph.
- Added deterministic Atlas/Delta planning seed data and registered the planning entitlement, module instance and administrator permissions.
- Added transaction-coordinator repositories and authenticated API routes for the implemented foundation.
- Added formula-DAG cycle rejection tests and deterministic DAG acceptance tests.
- Applied the migration and reran development seed on disposable database `planning_accept_7dd60d7f1806`.
- Verified the API with real cookie authentication, CSRF, PostgreSQL and RabbitMQ startup; seeded cycles, versions, grid and workforce data were readable.

## Last meaningful corrections

- Corrected Npgsql positional parameter binding in the planning repository.
- Corrected scenario cloning to persist the generated clone identity and requested name.
- Corrected budget reservation idempotency so a retry does not double-count the same source key; reservation rows are the reservation ledger and are not also added to the aggregate precommitment column.
- Corrected transfer bucket resolution to use the source and target planning coordinates and reject same-bucket transfers.

## Required next executable work

1. Add the missing Build Now workflows listed in `BUDGETING_BACKEND_COVERAGE_MATRIX.md`.
2. Add source-owned contracts/adapters and real cross-module event/transaction tests.
3. Complete security-scoped submission, dimension, mapping, formula, spread/allocation and statement projections.
4. Rerun the final disposable migration/seed, API, security/RLS, architecture, Release build, formatter, OpenAPI and diff gates after the last source change.

## Delivery status

`NOT COMPLETE` until the mandatory gaps are implemented and supported by repeatable evidence. No production certification is implied.
