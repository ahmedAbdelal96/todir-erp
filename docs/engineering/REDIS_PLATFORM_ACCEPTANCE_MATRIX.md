# Redis Platform Acceptance Matrix

| ID | Requirement | Evidence | Status |
| --- | --- | --- | --- |
| R1 | Shared cache contract, approved policy registry, and no module Redis client | `Core/Caching`, governance test/build | PASS |
| R2 | PostgreSQL revision + same-transaction invalidation | platform/HR forward-only migrations; rollback and RLS review | PASS |
| R3 | HR tenant/entity/branch revision scope | directory effective-max query and transfer-aware triggers | PASS |
| R4 | Outbox → RabbitMQ → Worker → Inbox | `CacheInvalidationConsumer`, existing dispatcher and Inbox contract | PASS |
| R5 | Atomic monotonic duplicate/out-of-order safety | Lua marker compare-and-set; real Redis concurrency test | PASS |
| R6 | Stale-fill race/stampede protection | exact revision/schema check; two-cache-instance and commit-during-local-coalescing real Redis tests | PASS |
| R7 | tenant/entity/permission isolation | scope key, permission version, endpoint authorization, RLS | PASS |
| R8 | Redis outage fallback and recovery | bounded timeout, PostgreSQL loader, reconnect configuration, consumer retry semantics | PASS |
| R9 | real local Redis service and loopback exposure | `ops/compose/platform-dev.yml`, healthcheck, AOF/memory policy | PASS |
| R10 | safe HR integration | directory/basic detail only; sensitive HR policy is non-cacheable | PASS |
| R11 | frontend freshness | HR revision polling every 10s; query TTL 15s/5m; focus recovery | PASS |
| R12 | HR profile correction and optimistic concurrency | `PUT /api/v1/hr/employees/{id}/profile`; tenant/entity authorization, version check, audit/timeline, same-transaction projection refresh | PASS |
| R13 | two-user, two-instance real browser freshness | Playwright evidence: User A workspace updated without reload after User B changed the employee through API instance B; search, pagination, explicit entity scope, and Arabic RTL presentation verified | PASS |
| R14 | durable invalidation after profile correction | fresh disposable PostgreSQL + Redis + RabbitMQ + Worker: Outbox `PlatformCacheInvalidatedV1` published at revision 339 and `platform-cache-invalidation` Inbox processed it | PASS |
| R15 | outage and recovery | Redis container stop produced PostgreSQL fallback warnings while HR requests remained 200; restart produced recovery logs and healthy Redis | PASS |
| R16 | performance/observability/security | bounded dependency metrics, no key PII, TLS/ACL production guard, bounded fingerprint and entry size | PASS |
| R17 | real runtime proof | local PostgreSQL + Redis + RabbitMQ + Worker; fresh evidence recorded below | PASS |
| R18 | concurrent cold-fill stampede control | real Redis acceptance at 10, 50, and 100 concurrent callers; one authoritative loader per level; joined callers revalidate PostgreSQL revision | PASS |
| R19 | PostgreSQL revision read cost | disposable seeded database plus 10,000-row revision cardinality plan; index-only read 0.121 ms, 7 shared hits | PASS |
| R20 | realistic local scalability envelope | `artifacts/performance/redis-platform-todir_redis_perf_20261123100500_296.json`; report records 800 total requests: 400 PostgreSQL-fallback and 400 Redis-enabled, with 100 requests per level at concurrency 1/10/50/100 and zero errors | PASS |
| R21 | revision writer security | tenant-context, approved-dependency, canonical-scope checks; public function execution revoked; cross-tenant/invalid-scope acceptance passed | PASS |
| R22 | developer governance | permanent developer guide plus architecture test forbidding module-owned Redis clients | PASS |
| R23 | coalescing cancellation and recovery | leader cancellation, waiter cancellation, loader failure cleanup, and successful recovery against real Redis/PostgreSQL | PASS |
| R24 | fingerprint privacy contract | canonical lowercase SHA-256 enforcement rejects raw, sensitive, uppercase, and malformed inputs | PASS |

## Local runbook

1. Start `docker compose up -d postgres rabbitmq redis` and confirm `redis-cli -h 127.0.0.1 -p 6381 ping` (Memurai owns 6380 and Windows Redis owns 6379 on this workstation).
2. Run `dotnet run --project backend/DbMigrator/DbMigrator.csproj -- --seed development` against the disposable/local development database.
3. Start API and Worker with development configuration.
4. Read the authorized HR employee directory twice and record hit/miss counters.
5. Create or change an employee/employment and verify a revision row plus a pending Outbox row.
6. Wait for the existing Outbox dispatcher and Worker; verify RabbitMQ delivery, Inbox processed state, and a monotonic Redis invalidation marker.
7. Repeat from a second API process; stop Redis and verify the same request succeeds from PostgreSQL.
8. Verify a different tenant or membership cannot observe the cached result.

## Final local evidence — 2026-11-23

- Fresh disposable acceptance database `todir_redis_hr_1795385688183` applied or verified 247 ordered platform scripts and Development Seed completed for Atlas Trading and Delta Supplies with the canonical HR and cross-module records. The additional scripts harden the security-definer revision writer and preserve the assignment `entity:all` scope.
- Docker Redis `7.4.12` was healthy on loopback host port `6381`; the workstation's Memurai service owns `6380` and Windows Redis owns `6379`. No Redis credential or connection string was emitted by the cache startup/recovery logs.
- API instances on ports `5080` and `5081` authenticated the seeded Atlas administrator and HR officer and returned directory/detail/freshness `200`; Redis contained tenant/entity-scoped cache and invalidation keys without raw search text.
- `RedisPlatformAcceptanceIntegrationTests` passed 12/12 against disposable PostgreSQL + Docker Redis: atomic out-of-order marker, two-client stale-fill rejection after a PostgreSQL revision change, tenant/permission scope separation, employee profile mutation advancing the tenant-wide revision with a durable invalidation event, 10/50/100 cold-fill coalescing, commit-during-local-coalescing revalidation, leader/waiter cancellation isolation, loader failure cleanup/recovery, and cross-tenant/invalid-dependency/invalid-scope rejection.
- Real Outbox → RabbitMQ → Worker → Inbox → PostgreSQL proof passed for the browser mutation: outbox event `8a4a89f1-77aa-4780-883e-f8079f6fe808` was `Published` at revision `339`, and `platform-cache-invalidation` Inbox processed it at `2026-11-22T22:32:01.494Z`.
- Real browser acceptance passed `1/1` on two Vite origins (`5173` → API `5080`, `5174` → API `5081`): 81-row directory pagination, explicit legal-entity scope, User B profile correction, User A live workspace update without reload, User B search/detail freshness, Arabic RTL presentation, and no manual refresh. Freshness latency was `6744 ms`. Evidence: `artifacts/test-results/redis-hr-real-consistency/acceptance.json`, `user-a-live-update.png`, and `user-b-updated-record.png`.
- Controlled Redis outage/recovery passed locally: a fresh API instance returned authenticated HR directory `200` while Redis was stopped, logged PostgreSQL fallback, and after container restart Redis returned `healthy` with a connection-restored log without API restart. The earlier two-instance/Worker outage evidence remains valid; no database or Redis volume was destroyed during either probe.
- Core cache tests passed 9/9; architecture governance passed with the new shared-boundary checks; backend Release build passed with 0 warnings/errors. The focused local Redis benchmark's default run is 800 total HR list requests: 400 PostgreSQL-fallback and 400 Redis-enabled (100 at each of concurrency 1/10/50/100 per mode), with the report's `RequestSummary` recording 800 successes and 0 errors. Redis-enabled DB commands/request measured 6.00/6.07/6.02/6.05 at concurrency 1/10/50/100; the extra authoritative revision check for joined callers is intentional correctness cost. Redis-enabled p95 was 27.25/106.06/90.19/187.12 ms at concurrency 1/10/50/100. Redis used 1,646,264 bytes in the local service and produced one matching HR cache key for the measured endpoint; the local service remained below its 256 MiB cap with zero evictions observed. PostgreSQL revision reads measured 0.303 ms/6 shared hits on the seeded six-row table and 0.121 ms/7 shared hits with 10,000 synthetic revision rows, using `ix_platform_cache_dependency_revision_scope` with an index-only scan. The measured local ceiling is 100 concurrent requests in one API process; this is not a production capacity claim.
- Security/RLS passed 7/7; the focused HR API authorization/concurrency integration passed 1/1 (authorized profile correction, stale version `409`, self-service `403`, cross-entity denial, audit/timeline evidence); HR tests passed 17/17; FinanceBase tests passed 35/35. Frontend unit tests remain 284/285 because of the pre-existing unrelated Manufacturing CSS localization false positive. OpenAPI contract validation retains the pre-existing frozen Inventory endpoint mismatch (`get /api/v1/inventory/legal-entities/{entityId}/counts`); generated-client drift passed.
