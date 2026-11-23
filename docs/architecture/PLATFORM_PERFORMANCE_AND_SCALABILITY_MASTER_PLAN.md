# Platform Performance and Scalability Master Plan

Status: PLATFORM PERFORMANCE & SCALABILITY FOUNDATION — CERTIFIED for the verified local development envelope, including Redis (2026-11-23)

Scope: ASP.NET Core, PostgreSQL/RLS, the shared Redis platform cache, MassTransit/RabbitMQ/Worker, and React/Vite delivery behavior. No read replica, partitioning, search cluster, second messaging framework, or GitHub Actions was introduced.

## Decisions and invariants

TODIR remains a modular monolith with PostgreSQL as system of record, durable Outbox/Inbox delivery, and request-scoped authorization subject reuse. PostgreSQL remains authoritative for authorization, accounting, inventory, idempotency, Inbox, and Outbox truth. Missing tenant context denies access; request caching is tenant/membership/request scoped; posted accounting and completed stock movements remain immutable.

The only evidence-backed product changes in this performance mission are:

1. request-scoped authorization-subject reuse with bounded load/hit metrics;
2. named route-level lazy imports and removal of static page re-exports that defeated code splitting;
3. reproducible local API, PostgreSQL, worker, broker, browser, and Redis measurement harnesses;
4. a shared cache boundary with bounded stampede, key, entry-size, and failure behavior.

No measured N+1, missing-index, or pagination defect justified a production query/index change. Critical plans were captured with `EXPLAIN (ANALYZE, BUFFERS)` on disposable seeded data. The local PostgreSQL image does not expose `pg_stat_statements`; the opt-in performance mode instead counts Npgsql command Activities per request and records transaction counts, active connections, lock waits, transaction age, resource samples, and critical plans.

## Measurement contract

Run `backend/Tests/Performance/platform-performance-benchmark.ps1` against an isolated `todir_perf_*` database. The `diagnostic`, `load`, and `stress` modes share the same seeded Atlas/Delta fixture and record commit, environment, API instances, pool maximum, errors, RPS, p50/p95/p99, SQL statements/request, transactions, active connections, lock waits, resource peaks, write-smoke outcomes, and query plans. The API's Npgsql Activity counter is opt-in through `TODIR_PERFORMANCE_SQL_COUNT=true` and is used only by this local harness.

The browser gate is `frontend/tests/e2e/performance-certification.spec.ts` on the real API/Vite/Chromium stack. It records route wall time, DOM/load timings, resource/script counts, transfer bytes, and long tasks for invoicing, accounting, inventory, payments, HR, payroll, projects, and POS. The worker gate is `backend/Tests/Performance/worker-performance-benchmark.ps1`, which proves Outbox → RabbitMQ → Worker → Inbox → PostgreSQL, backlog drain, throughput, and duplicate replay idempotency.

The Redis gate is `backend/Tests/Performance/redis-platform-benchmark.ps1`. It compares the same seeded HR directory endpoint with PostgreSQL fallback and Redis enabled at concurrency 1/10/50/100, records p50/p95/p99, RPS, measured database commands/request, Redis memory/key observations, and declares the local limit explicitly. Redis acceptance tests separately prove exact revision freshness, 10/50/100 cold-fill coalescing, two-client behavior, and security boundaries.

## Verified local envelope

- API read coverage: Authorization, POS, Inventory, Sales, Invoicing, Payments, Accounting, HR, Payroll, Projects, and Subledger; Atlas and Delta tenant sessions; 5 requests at concurrency 1/4 on the final load run, all read requests successful.
- Final broad-load representative p95 at concurrency 4: Atlas ranged from 58.49 ms (Payments) to 236.30 ms (Accounting health); Projects 71.16 ms; Inventory 83.32 ms; Subledger 75.84 ms. Delta ranged from 46.70 ms (Invoicing) to 174.25 ms (Accounting health); Projects 84.10 ms. These are local observations, not SLAs.
- API write smoke: Projects, Sales quotation, Invoicing draft, Inventory warehouse, POS cart, HR employee, Payroll component, and Payments account all returned HTTP 201 in the final diagnostic run.
- PostgreSQL: sampled peak active connections up to 2 and zero lock waits in the final diagnostic run; transaction durations remained below the captured sub-second sample; representative Sales, Inventory, Projects, and Subledger plans were captured.
- Worker: 10 contract-valid expense events drained through the local broker in the final run in 7.92 seconds (1.26 events/s), 10 Inbox rows and 10 project cost facts persisted; replaying one published message did not change cost-fact count. This is a small local diagnostic, not a capacity SLA.
- Browser: the final real-stack Playwright test passed across 8 operational routes; the first cold route reported 971.4 ms DOMContentLoaded, 12.96 MB transferred script resources, and zero long tasks. Subsequent route navigations were 476–627 ms DOMContentLoaded with the local Vite module graph and cached resources.
- Multi-instance: the final API harness starts two local API instances against the same disposable PostgreSQL database and reports the mixed Projects read result separately. It does not claim autoscaling, cross-region, or production node capacity.
- SQL: the final diagnostic recorded 6–50 SQL statements/request across the 21 representative reads; the mixed two-instance Projects read recorded 7 statements/request. This is local diagnostic evidence, not a universal query budget for every endpoint or data volume.
- Redis: the focused run passed 800/800 requests in PostgreSQL-fallback and 800/800 with Redis enabled at concurrency 1/10/50/100. Warm Redis reduced the HR endpoint from about 7.00–7.08 to 4.78–4.99 measured DB commands/request at concurrency 10–100; enabled p95 was 26.64/111.44/67.99/188.18 ms at concurrency 1/10/50/100. The run observed 1,472,368 bytes used by the local Redis service, one matching endpoint key, and zero evictions. This is a one-process local envelope with a 100-concurrent-request ceiling, not a production capacity claim.
- Redis revision read: with 10,000 synthetic revision rows in the disposable database, `ix_platform_cache_dependency_revision_scope` produced an index-only plan at 0.121 ms and 7 shared hits; the seeded six-row plan was 0.303 ms and 6 shared hits with a sequential scan chosen by PostgreSQL. The two-command tenant-context transaction is retained because RLS must see the transaction-local tenant setting.

## Budgets and regression policy

These are local regression budgets, not customer-facing SLAs: read p95 ≤ 250 ms for normal list/detail operations at concurrency 4; write-smoke p95 is observed and must not regress by >25% without an evidence-backed explanation; zero HTTP errors on seeded authorized paths; zero lock waits in the controlled run; worker duplicate replay must preserve persisted fact count; browser cold DOMContentLoaded ≤ 1,500 ms and zero long tasks on the measured operational routes. Accounting health is a heavier diagnostic operation and is tracked separately from normal list budgets.

The benchmark JSON under `artifacts/performance` is the repeatable regression artifact. A regression gate must compare the same mode, seed, request count, concurrency, pool size, and machine class; it must fail on errors, duplicate financial outcomes, lock waits, or budget breaches. No claim is made for hardware or concurrency not measured here.

## Existing quality defects retained explicitly

- Manufacturing localization/unit test has a known CSS media-query false positive in the preserved concurrent Manufacturing change.
- The frozen Inventory OpenAPI contract still misses `GET /api/v1/inventory/legal-entities/{entityId}/counts` while implementation exposes it. This is recorded, not silently marked PASS.
- Higher-scale/PJ38-style certification, autoscaling, multi-region capacity, partitioning, read replicas, and global worker-concurrency redesign remain deferred. Redis is no longer deferred for the verified local foundation envelope; production-scale Redis capacity remains unclaimed.

## Acceptance gates

The applicable local gates remain backend tests, authorization/RLS/security, migration/seed, real Worker/RabbitMQ proof, real browser proof, frontend typecheck/architecture/build, architecture validation, Release build, formatter, OpenAPI drift/contract review, and `git diff --check`. Remote CI/GitHub Actions is outside the acceptance contract.

References: [Odoo performance guidance](https://www.odoo.com/documentation/18.0/developer/reference/backend/performance.html), [Frappe architecture](https://docs.frappe.io/framework/user/en/basics/architecture), [Frappe caching](https://docs.frappe.io/framework/user/en/guides/caching), and [Frappe background jobs](https://docs.frappe.io/framework/user/en/api/background_jobs).
