# Platform Performance and Scalability Master Plan

Status: delivered local-development foundation (2026-11-22)

Scope: shared ASP.NET Core, PostgreSQL, RLS/security, MassTransit/RabbitMQ/Worker, and React/Vite delivery behavior. This plan does not introduce Redis, a read replica, partitioning, a search cluster, or a second messaging framework.

## Decision summary

TODIR remains a modular monolith with PostgreSQL as the system of record, the existing Outbox/Inbox delivery contract, and the existing API/Worker hosts. Performance work is evidence-led and bounded: first remove repeated work inside one request, bound database reads and browser payloads, then measure the real stack. Cross-process caching is deferred until a measured workload proves that a process-local or request-scoped optimization cannot meet the required envelope.

The first confirmed shared bottlenecks were:

1. `IAuthorizationSubjectStore` could execute the same tenant/membership authorization query more than once during one HTTP request. The new request-scoped cache is tenant and membership keyed, is disabled outside an HTTP request, and can be disabled with `TODIR_AUTHORIZATION_REQUEST_CACHE_ENABLED=false` for an A/B benchmark.
2. The route registry eagerly pulled large module page graphs into the initial Vite chunk. Route pages now use named dynamic imports, while the route registration remains synchronous so permission and navigation behavior do not change.
3. Existing database evidence already demonstrates bounded accounting and subledger reads. It remains authoritative; this mission does not replace it with unsupported synthetic capacity claims.

## Non-negotiable invariants

- A missing tenant context denies access. Request caching never crosses tenant, membership, legal-entity, branch, or process boundaries.
- RLS remains transaction-local through `SET LOCAL app.tenant_id`; runtime API and Worker roles never receive migration ownership or `BYPASSRLS`.
- Authorization changes invalidate the existing permission decision cache. The new subject cache lives only for the current request and cannot make a later request reuse a revoked subject.
- Posted accounting records and completed stock movements remain immutable.
- Outbox, RabbitMQ, Worker, Inbox, idempotency, and retry semantics are unchanged. A faster request must not bypass durable post-commit delivery.
- Queries remain server-paginated/bounded. No frontend change may replace an authorized API projection with client-side access to another module's tables.
- Metrics use bounded names and dimensions. Tenant IDs, party IDs, document IDs, SQL text, and user-entered search values are not metric labels.

## Discovery and evidence

The baseline review covered the request pipeline, connection pool configuration, authorization store, endpoint authorization patterns, observability, message endpoint defaults, worker consumers, TanStack Query defaults, route registration, and existing performance ADRs.

Existing evidence retained as baseline:

| Area | Evidence | Decision |
|---|---|---|
| Accounting | `docs/adr/accounting-performance-evidence.md`: local 500k journal-entry / 1m-line evidence with Trial Balance 815 ms and GL first page 278 ms | Keep bounded projections and indexes; no partitioning/read replica without a new measured ADR |
| Subledger | Existing scale fixture and reconciliation evidence | Keep scale fixture isolated; benchmark application and aging paths separately |
| HR | `docs/adr/hr-performance-scope.md` | Preserve the Product Owner's deferral of heavyweight HR certification |
| Frontend initial load | Prior checkpoint main chunk 5,405.75 kB / 1,175.64 kB gzip | Apply route-level code splitting; keep the remaining shared grid chunk visible as a next measured work item |
| Authorization | Store query opens a connection and transaction for every load; endpoint/service code contains repeated loads in financial workflows | Add request-scoped subject reuse plus hit/load metrics |

## Delivery lanes

## Market architecture comparison

The comparison is about engineering patterns, not unsupported throughput claims. Odoo's official performance guidance emphasizes profiling actual requests/SQL and batching ORM work instead of executing a query in a loop. Frappe's official architecture documents a web/background-job stack with caching, and its caching and background-job guidance describes Redis-backed cache/queue patterns. TODIR adopts the transferable principles—measure real SQL, batch repeated work, and make asynchronous work explicit—while retaining its own modular ownership, PostgreSQL RLS, Outbox/Inbox, and financial invariants. Redis remains deferred because this mission has not established a measured cross-process cache requirement.

References: [Odoo performance guidance](https://www.odoo.com/documentation/18.0/developer/reference/backend/performance.html), [Frappe architecture](https://docs.frappe.io/framework/user/en/basics/architecture), [Frappe caching](https://docs.frappe.io/framework/user/en/guides/caching), and [Frappe background jobs](https://docs.frappe.io/framework/user/en/api/background_jobs).

### API and authorization

- Keep authentication/session validation authoritative in the API.
- Reuse one authorization subject inside one request only.
- Measure `todir.authorization.subject.database.loads` and `todir.authorization.subject.request.cache.hits`.
- Do not add a distributed authorization cache in this mission.

### PostgreSQL

- Keep `TODIR_DB_MAX_POOL_SIZE` and command timeout explicit in local/prod configuration.
- Review query plans for critical list, report, and financial paths; verify index use with `EXPLAIN (ANALYZE, BUFFERS)` on isolated data.
- Do not add broad indexes, partitioning, read replicas, or connection multiplexing without a workload and rollback plan.

### Outbox, Worker, and RabbitMQ

- Preserve publisher confirmation, quorum queues, Inbox uniqueness, retry, and durable state transitions.
- Tune consumer concurrency only per consumer after backlog age, processing duration, database pool pressure, and duplicate/error counts are captured.
- Broker throughput is not business completion; persisted PostgreSQL outcomes are the acceptance proof.

### Frontend

- Route-level page code is loaded when the route is visited; the `AppShell` suspense boundary remains the loading/error boundary.
- TanStack Query remains the server-state owner. Query keys retain legal-entity scope where the business fact is entity-scoped.
- Lists remain paginated and bounded. Large grids are not silently converted to unbounded client-side data.
- The current production build still reports a shared `grid` chunk over 1 MB. It is recorded, not hidden by changing the warning threshold.

## Measurement contract

Use the checked-in harness `backend/Tests/Performance/platform-performance-benchmark.ps1` only against a disposable database whose name matches `todir_perf_*`. The harness records:

- endpoint, concurrency, total requests, errors, requests/second;
- p50, p95, and p99 latency;
- authorization database-load/cache-hit counters;
- API process peak working set when the harness owns the API process.

The default local profile is concurrency 1, 4, and 8 with 40 requests per endpoint. This is a diagnostic envelope, not a production capacity guarantee. A result is comparable only when the commit, database seed, endpoint, machine, connection pool, rate limit, and service state are recorded.

## Future Redis blueprint (not implemented here)

Redis may be introduced only after a measured need is approved. The future adapter must:

1. sit behind an application capability, never behind direct module access;
2. use tenant, legal-entity, branch, permission-version, and data-version components in keys where applicable;
3. define TTL, negative-cache rules, invalidation events, stampede protection, outage behavior, and memory limits before rollout;
4. fail open only for non-authoritative reference/read acceleration; authorization, financial truth, stock availability, idempotency, Inbox, and Outbox truth must fail closed or use PostgreSQL;
5. preserve request correlation and bounded metrics without logging key contents or sensitive values;
6. be tested with stale data, eviction, restart, network loss, concurrent invalidation, and tenant-crossing attempts.

The current request cache is deliberately not a precursor that silently requires Redis: it is local to one request and PostgreSQL remains authoritative.

## Acceptance gates

The final local gate is: targeted backend tests, authorization/RLS/security tests, frontend typecheck/architecture/localization/API drift, production build, isolated migration/seed, bounded benchmark, relevant real-stack API/browser flow, `dotnet build` release, applicable formatter, `git diff --check`, and a clean `dev` worktree after commit. The delivered evidence is recorded in `docs/engineering/PLATFORM_PERFORMANCE_COVERAGE_MATRIX.md` and `docs/engineering/PLATFORM_PERFORMANCE_EXECUTION_CHECKPOINT.md`. GitHub Actions and remote CI are outside this acceptance contract. Two unrelated pre-existing repository gates remain explicit: the Manufacturing CSS localization false positive and the Inventory OpenAPI missing path.

## Deferred items

Redis implementation, large-scale capacity certification, heavyweight HR certification, partitioning, read replicas, search infrastructure, and global worker-concurrency redesign remain separate decisions. They are not implied by the local envelope in this document.
