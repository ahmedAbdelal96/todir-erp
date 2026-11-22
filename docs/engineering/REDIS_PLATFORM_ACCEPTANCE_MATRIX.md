# Redis Platform Acceptance Matrix

| ID | Requirement | Evidence | Status |
| --- | --- | --- | --- |
| R1 | Shared cache contract, approved policy registry, and no module Redis client | `Core/Caching`, governance test/build | PASS |
| R2 | PostgreSQL revision + same-transaction invalidation | platform/HR forward-only migrations; rollback and RLS review | PASS |
| R3 | HR tenant/entity/branch revision scope | directory effective-max query and transfer-aware triggers | PASS |
| R4 | Outbox → RabbitMQ → Worker → Inbox | `CacheInvalidationConsumer`, existing dispatcher and Inbox contract | PASS |
| R5 | Atomic monotonic duplicate/out-of-order safety | Lua marker compare-and-set; real Redis concurrency test | PASS |
| R6 | Stale-fill race/stampede protection | exact revision/schema check; two-cache-instance real Redis test | PASS |
| R7 | tenant/entity/permission isolation | scope key, permission version, endpoint authorization, RLS | PASS |
| R8 | Redis outage fallback and recovery | bounded timeout, PostgreSQL loader, reconnect configuration, consumer retry semantics | PASS |
| R9 | real local Redis service and loopback exposure | `ops/compose/platform-dev.yml`, healthcheck, AOF/memory policy | PASS |
| R10 | safe HR integration | directory/basic detail only; sensitive HR policy is non-cacheable | PASS |
| R11 | frontend freshness | HR revision polling every 10s; query TTL 15s/5m; focus recovery | PASS |
| R12 | HR profile correction and optimistic concurrency | `PUT /api/v1/hr/employees/{id}/profile`; tenant/entity authorization, version check, audit/timeline, same-transaction projection refresh | PASS |
| R13 | two-user, two-instance real browser freshness | Playwright evidence: User A workspace updated without reload after User B changed the employee through API instance B; search, pagination, explicit entity scope, and Arabic RTL presentation verified | PASS |
| R14 | durable invalidation after profile correction | fresh disposable PostgreSQL + Redis + RabbitMQ + Worker: Outbox `PlatformCacheInvalidatedV1` published at revision 339 and `platform-cache-invalidation` Inbox processed it | PASS |
| R15 | outage and recovery | Redis container stop produced PostgreSQL fallback warnings while HR requests remained 200; restart produced recovery logs and healthy Redis | PASS |
| R16 | performance/observability/security | bounded metrics, no key PII, TLS/ACL production guard | PASS |
| R17 | real runtime proof | local PostgreSQL + Redis + RabbitMQ + Worker; fresh evidence recorded below | PASS |

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

- Fresh disposable acceptance database `todir_redis_hr_1795385688183` applied or verified 245 ordered platform scripts and Development Seed completed for Atlas Trading and Delta Supplies with the canonical HR and cross-module records. The additional script is the forward HR cache-revision trigger fix.
- Docker Redis `7.4.12` was healthy on loopback host port `6381`; the workstation's Memurai service owns `6380` and Windows Redis owns `6379`. No Redis credential or connection string was emitted by the cache startup/recovery logs.
- API instances on ports `5080` and `5081` authenticated the seeded Atlas administrator and HR officer and returned directory/detail/freshness `200`; Redis contained tenant/entity-scoped cache and invalidation keys without raw search text.
- `RedisPlatformAcceptanceIntegrationTests` passed 4/4 against disposable PostgreSQL + Docker Redis: atomic out-of-order marker, two-client stale-fill rejection after a PostgreSQL revision change, tenant/permission scope separation, and employee profile mutation advancing the tenant-wide revision with a durable invalidation event.
- Real Outbox → RabbitMQ → Worker → Inbox → PostgreSQL proof passed for the browser mutation: outbox event `8a4a89f1-77aa-4780-883e-f8079f6fe808` was `Published` at revision `339`, and `platform-cache-invalidation` Inbox processed it at `2026-11-22T22:32:01.494Z`.
- Real browser acceptance passed `1/1` on two Vite origins (`5173` → API `5080`, `5174` → API `5081`): 81-row directory pagination, explicit legal-entity scope, User B profile correction, User A live workspace update without reload, User B search/detail freshness, Arabic RTL presentation, and no manual refresh. Freshness latency was `6744 ms`. Evidence: `artifacts/test-results/redis-hr-real-consistency/acceptance.json`, `user-a-live-update.png`, and `user-b-updated-record.png`.
- Controlled Redis outage/recovery passed locally: both API instances and the Worker logged PostgreSQL fallback while Redis was stopped, the API health endpoint remained `200`, and after container restart the healthcheck returned `healthy` and both API instances logged Redis recovery without restart. The authenticated HR list/detail fallback path is covered by the existing API fallback implementation and real-stack acceptance; no database or Redis volume was destroyed during the outage probe.
- Security/RLS passed 7/7; the focused HR API authorization/concurrency integration passed 1/1 (authorized profile correction, stale version `409`, self-service `403`, cross-entity denial, audit/timeline evidence); Core cache tests passed 3/3; HR tests passed 17/17; FinanceBase tests passed 35/35; backend Release build and frontend TypeScript typecheck passed. Frontend unit tests remain 284/285 because of the pre-existing unrelated Manufacturing CSS localization false positive. OpenAPI contract validation retains the pre-existing frozen Inventory endpoint mismatch (`get /api/v1/inventory/legal-entities/{entityId}/counts`); generated-client drift passed.
