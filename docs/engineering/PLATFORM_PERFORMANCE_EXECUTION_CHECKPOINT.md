# Platform Performance Execution Checkpoint

Date: 2026-11-22  
Branch: `dev`  
Status: local performance foundation evidence complete; higher-scale certification remains bounded by the recorded local envelope.

## Authorized scope

Complete local performance/scalability evidence from `b0430b5eb96a7926bcb7c096d4fb9d1ae246286a`, preserve concurrent Catalog/Manufacturing edits, use disposable databases for mutating checks, do not add Redis or remote CI.

## Completed and verified

- Extended `platform-performance-benchmark.ps1` with diagnostic/load/stress modes, Atlas/Delta sessions, representative reads across Authorization/POS/Inventory/Sales/Invoicing/Payments/Accounting/HR/Payroll/Projects/Subledger, representative writes, p50/p95/p99/RPS, transactions, active connections, lock waits, transaction age, resource samples, query plans, two local API instances, and explicit local limits.
- Added `worker-performance-benchmark.ps1`: final local run drained 10 valid expense events through Outbox → RabbitMQ → Worker → Inbox → PostgreSQL in 7.92 seconds (1.26 events/s); duplicate replay left persisted cost-fact count unchanged.
- Added `performance-certification.spec.ts`: real API/Vite/Chromium evidence across 8 operational routes; 1 test passed after inspecting the fresh output.
- Final disposable API diagnostic ran after the last harness change. All read paths and all eight write smoke operations returned successful status; the write smoke included Sales quotation, Invoicing draft, Inventory warehouse, POS cart, HR employee, Payroll component, Payments account, and Projects draft.
- Opt-in request-scoped Npgsql Activity measurement is enabled only for the performance harness; the final diagnostic recorded 6–50 SQL statements per request across all 21 representative reads without exposing a production header unless the measurement flag is enabled.
- Critical plans were captured for Sales quotations, Inventory stock balance, Projects list, and Subledger exposure. No evidence-backed N+1 or missing-index change was justified.
- Existing request-scoped authorization cache and route-level code splitting were preserved. Final isolated Security/RLS was 7/7, Projects acceptance was 22/22, and the real Worker/RabbitMQ proof was rerun after the final source change.
- Updated the Master Plan, Coverage Matrix, Redis Future Blueprint, and frontend/backend governance references without adding overlapping documentation.

## Measurement limitation and supported envelope

The local PostgreSQL container has no `pg_stat_statements`; the harness does not depend on it. The opt-in request-scoped Npgsql Activity counter records SQL statements/request, while the harness also records transaction deltas, connection/active/lock samples, transaction age, process memory/CPU samples, and query plans. No mandatory local measurement gap remains for the supported envelope.

The verified envelope is local development only: two API processes against one PostgreSQL container, one local RabbitMQ/Worker, seeded Atlas/Delta, bounded request counts, and Chromium on the development Vite server. No production, autoscaling, multi-region, or PJ38-scale claim is made.

## Known repository defects retained

- Manufacturing CSS localization false-positive remains in the preserved concurrent Manufacturing change; frontend unit/localization totals remain `284/285` from the prior gate.
- Inventory OpenAPI contract still reports the missing `GET /api/v1/inventory/legal-entities/{entityId}/counts` path; `api:drift` remains passing.
- These defects are recorded and not silently changed by the performance mission.

## Final return gate

Before delivery: run the fresh final performance scripts and browser gate after the last source change, inspect their output, run Release build/formatter/diff checks, stage only performance-owned files, commit directly on `dev`, push `origin/dev`, verify remote SHA, and leave preserved concurrent files untouched.
