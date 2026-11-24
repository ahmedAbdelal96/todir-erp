# 10 — Performance Engineering, Operational Reliability & Test Program

**First principle:** do not accept an architecture because it is asynchronous or has separate code. **Measure** speed, CPU/RAM, DB pressure, broker/backlog and error rates under representative mixed workloads. No benchmarks have been executed for this design pack; every number below is a **target/proposal only**, never actual performance.

## 1. Baseline before any Analysis code (P0 mandatory)

Use reproducible isolated staging database / disposable DB with representative test data; document hardware, versions, connection-pool size, query plans, number of tenants, concurrency, network and background traffic. Avoid production-destructive tests or arbitrary changes to existing local DB.

Capture per scenario: throughput, p50/p95/p99, response error rate, DB CPU and I/O, slow queries, lock wait, buffer cache behavior, connection pool utilization, read replica lag (if present), outbox pending count/oldest age, RabbitMQ queue lag, worker memory/CPU, trace correlation. Save raw output in repo reports and script path so reruns are comparable.

**Operational flows to baseline:** Sales quote/order create-confirm, invoice post/credit note, Payments completion+application, Banking reconciliation, Subledger aging, Inventory availability/reservation/movement, POS cash/card checkout, Manufacturing release/close if its acceptance baseline is verifiable, list pagination/authorization. Choose representative sizes and actual current paths, not artificial no-op endpoints.

## 2. Performance SLO design

**No made-up guarantees.** Gate 1 records actual current baseline. Gate 2 proposes accepted p95/p99 absolute SLOs *and* allowed relative regression budget per critical flow, with statistical uncertainty and consistency across multiple runs. Draft recommendation: no meaningful regression; provisional alert threshold **5% relative p95** in comparable conditions, **not approved** and not a substitute for absolute SLO or p99/error/lock metrics. Owner and engineering must approve thresholds with baseline before implementation.

KPIs beyond API latency:

| Indicator | Why it matters |
|---|---|
| Transaction success/error rates | AI processing must not break financial correctness |
| p95/p99 for critical writes | Detect hidden tail-latency regression |
| PostgreSQL lock wait, CPU, I/O, temp space | Separate worker may still compete on same database |
| Pool saturation / timeouts | Heavy analytics can exhaust connections |
| RabbitMQ/outbox age and throughput | Protect operational event delivery |
| Analysis wait time / run duration | Jobs can be slow; user page remains responsive, transparent status |
| Data watermark age | Fast result with stale/partial facts is not trustworthy |
| Worker memory/CPU and cancel latency | Protect noisy-neighbor fairness and operations |
| Results storage growth / read query cost | Historical report volume can degrade DB and backup windows |

## 3. Benchmark matrix

| Case | Setup | Pass condition (to formalize after baseline) |
|---|---|---|
| B-01 Operational only | No analysis feature; identical baseline hardware/data | Baseline recorded with repeatability bounds |
| B-02 Analysis idle | New module, worker deployed but no run | Core API behavior and resource overhead within accepted budget |
| B-03 Analysis small | One short user-triggered job | Correct output; no regression in operational p95/p99 |
| B-04 Analysis large | Large company/time range, bounded extraction | No unbounded RAM/DB load; operational paths healthy |
| B-05 Concurrent tenants | Multiple tenants jobs + active transactional load | Fair quotas, no leak, no starvation, no exceeded budgets |
| B-06 Worker down | Stop/kill host during jobs | Operational ERP unchanged; job recoverable without duplicates |
| B-07 DB stress | Long/slow owner projection read, locks from concurrent writes | Bounded timeout/cancel/backpressure; no runaway connections |
| B-08 Large history | Many historical results and recommendations | Keyset/history pagination, bounded memory, indexed query plan |
| B-09 Cache context change | Switch tenant/legal entity/branch/role | No stale cross-scope display, no unfiltered query |
| B-10 Broker disturbance | Delay RabbitMQ operational events while analyses execute | Operational outbox lag within agreed tolerance; no accidental fanout storm |
| B-11 Migration upgrade | Upgrade nonempty versioned DB | No long exclusive lock on critical source tables; rollback app compatible |
| B-12 Failed user POST | Auth/limits/unknown type | No job/data recorded; no expensive extraction triggered |

## 4. Code and DB validation gates

- Backend: `dotnet restore/build`, architecture validator, module-specific unit/integration tests, real Postgres RLS and migration replay; formatter and `git diff --check`.
- Frontend: `corepack pnpm typecheck`, `corepack pnpm architecture:frontend`, existing Vitest and Playwright, localization/RTL/effective-access tests, controlled browser network check asserting **zero analysis POST on GET navigation**.
- API: OpenAPI shape/versioned schemas, invalid filter matrix, pagination, HTTP idempotency, concurrent requests/cancel/retry tests.
- Persistence: PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` on disposable data with safe query plans, RLS and tenant-pooled reuse, migration clean+incremental.
- Worker: lease fencing, job cancellation, dead-letter/retry, result publish atomicity, idempotency and failure injection.
- Financial: amount and currency rules, as-of/known-at, current vs historical, partial payments/credit notes/writeoff/reversal; reconciling source totals with owner reports.
- Data analysis: deterministic fixtures and independent calculations for v1; future model temporal backtesting and leakage checks before calling anything predictive.

## 5. Observability and alerts

Use existing OpenTelemetry and Core metrics conventions; add **low-cardinality, tenant-safe** metrics:

- `analysis_run_requested_total` by type/status (not tenant ID label), `analysis_run_completed_total`, `analysis_run_duration_seconds`, `analysis_job_queue_age_seconds`, `analysis_job_retries_total`, `analysis_data_quality_block_total`, `analysis_worker_active_jobs`, `analysis_result_bytes`, `analysis_recommendation_review_total` (aggregate only).
- Per-run trace includes correlation ID, sanitized run ID, definition version, source owners, waiting/extract/compute/publish stages; do **not** log raw customer/employee/finance payloads.
- Alert on stuck jobs/leases, queue age exceeding configured objective, DB pool saturation, excessive query time, p99 operational regression, outbox lag, cross-tenant policy violation and excessive storage growth.
- Admin kill switch **pauses creation and/or execution of Analysis**, preserving operating ERP, existing saved read views and audit. Clear policy whether already running jobs stop safely or finish within limits.

## 6. Release test evidence record

Each release report requires: exact commit SHA, env config (sanitized), DB dataset and size, command lines, timestamps, benchmark artifacts, pass/fail table, regressions against baseline, SQL query plans for top reads, security matrix, retry/crash verification and product acceptance. A test marked PASS without reproducible command/artifact or source scope is not sufficient.

## 7. Guard against overengineering

Do not provision DWH, cache cluster, Kafka, vector DB, GPU, Kubernetes node group, Python model-serving API or separate RabbitMQ vhost by default. They are allowed **only if one measured gate cannot be satisfied** within current architecture, and ADR records evidence, price, failure and rollback.
