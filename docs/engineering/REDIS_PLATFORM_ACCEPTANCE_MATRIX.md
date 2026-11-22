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
| R12 | performance/observability/security | bounded metrics, no key PII, TLS/ACL production guard | PASS |
| R13 | real runtime proof | local PostgreSQL + Redis + RabbitMQ + Worker; fresh evidence recorded below | PASS |

## Local runbook

1. Start `docker compose up -d postgres rabbitmq redis` and confirm `redis-cli -h 127.0.0.1 -p 6381 ping` (Memurai owns 6380 and Windows Redis owns 6379 on this workstation).
2. Run `dotnet run --project backend/DbMigrator/DbMigrator.csproj -- --seed development` against the disposable/local development database.
3. Start API and Worker with development configuration.
4. Read the authorized HR employee directory twice and record hit/miss counters.
5. Create or change an employee/employment and verify a revision row plus a pending Outbox row.
6. Wait for the existing Outbox dispatcher and Worker; verify RabbitMQ delivery, Inbox processed state, and a monotonic Redis invalidation marker.
7. Repeat from a second API process; stop Redis and verify the same request succeeds from PostgreSQL.
8. Verify a different tenant or membership cannot observe the cached result.

## Final local evidence — 2026-11-22

- Fresh disposable acceptance database and the development database both applied or verified 244 ordered platform scripts. Development Seed completed for Atlas Trading and Delta Supplies with the canonical HR and cross-module records.
- Docker Redis `7.4.12` was healthy on loopback host port `6381`; the workstation's Memurai service owns `6380` and Windows Redis owns `6379`. No Redis credential or connection string was emitted by the cache startup/recovery logs.
- API instances on ports `5080` and `5081` both authenticated the seeded Atlas administrator and returned HR directory `200`; `/api/v1/hr/employees/freshness` returned a PostgreSQL revision, and Redis contained tenant/entity-scoped cache and invalidation keys without raw search text.
- `RedisPlatformAcceptanceIntegrationTests` passed 3/3 against disposable PostgreSQL + Docker Redis: atomic out-of-order marker, two-client stale-fill rejection after a PostgreSQL revision change, and tenant/permission scope separation.
- Real Outbox → RabbitMQ → Worker → Inbox → Redis proof passed: a no-op HR update in the development database produced a `Published` `PlatformCacheInvalidatedV1` event at revision `815`, Inbox processed it with `attempts=1`, and the tenant-wide Redis marker reached `815`.
- Redis outage fallback returned HR directory `200`; Redis recovery returned `200` without an API restart. A second API instance started while Redis was unavailable, served the PostgreSQL-backed directory, and continued successfully after Redis returned.
- Security/RLS passed 7/7; Core cache tests passed 3/3; HR tests passed 17/17; FinanceBase tests passed 35/35; TypeScript typecheck passed. Frontend unit tests passed 284/285, with the one pre-existing unrelated Manufacturing CSS localization false positive. OpenAPI contract validation retains the pre-existing frozen Inventory endpoint mismatch (`get /api/v1/inventory/legal-entities/{entityId}/counts`); generated-client drift passed.
