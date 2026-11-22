# Redis Platform Acceptance Matrix

| ID | Requirement | Evidence | Status |
| --- | --- | --- | --- |
| R1 | Shared cache contract and no module Redis client | `Core/Caching`, architecture tests/build | PASS |
| R2 | PostgreSQL revision + same-transaction invalidation | platform/hr forward-fix migration; rollback and RLS review | PASS |
| R3 | Outbox → RabbitMQ → Worker → Inbox | `CacheInvalidationConsumer`, existing dispatcher and Inbox contract | PASS |
| R4 | Monotonic duplicate/out-of-order safety | revision trigger and monotonic consumer marker | PASS |
| R5 | stale-fill race/stampede protection | Redis lock + double revision check | PASS |
| R6 | tenant/entity/permission isolation | scope key, permission version, endpoint authorization, RLS | PASS |
| R7 | Redis outage fallback | bounded timeout and PostgreSQL loader path | PASS |
| R8 | real local Redis service | `ops/compose/platform-dev.yml`, healthcheck, AOF/memory policy | PASS |
| R9 | safe HR integration | directory/basic detail only; sensitive HR policy is non-cacheable | PASS |
| R10 | frontend freshness | HR TanStack Query `staleTime/gcTime` 30s/5m | PASS |
| R11 | performance/observability | bounded metrics and no key PII | PASS |
| R12 | real runtime proof | local Docker + two API instances + Worker + RabbitMQ + PostgreSQL; fresh evidence recorded below | PASS |

## Local runbook

1. Start `docker compose up -d postgres rabbitmq redis` and confirm `redis-cli -p 6380 ping` (the Windows Redis service owns 6379).
2. Run `dotnet run --project backend/DbMigrator/DbMigrator.csproj -- --seed development` against the disposable/local development database.
3. Start API and Worker with development configuration.
4. Read the authorized HR employee directory twice and record hit/miss counters.
5. Create or change an employee/employment and verify a revision row plus a pending Outbox row.
6. Wait for the existing Outbox dispatcher and Worker; verify RabbitMQ delivery, Inbox processed state, and a monotonic Redis invalidation marker.
7. Repeat from a second API process; stop Redis and verify the same request succeeds from PostgreSQL.
8. Verify a different tenant or membership cannot observe the cached result.

## Final local evidence — 2026-11-22

- Redis `7.4.12` was healthy on host port `6380` (`PONG`); the pre-existing Windows Redis service owns `6379`.
- Development migration/seed completed: 242 ordered scripts, both Atlas and Delta seed tenants ready.
- API instances on ports 5080 and 5081 both returned HR directory `200` with tenant-appropriate totals (81 and 41) and FinanceBase currencies `200`.
- Redis contained separate tenant-partitioned HR/reference keys; no raw search text or personal data appeared in keys.
- An HR write advanced PostgreSQL revision `246`, produced a `Published` `PlatformCacheInvalidatedV1` Outbox message, was processed by `platform-cache-invalidation` Inbox with `attempts=1`, and wrote Redis marker `246`.
- Redis was stopped and an HR directory request still returned `200` from PostgreSQL; Redis was restarted and healthy.
- Security suite passed 7/7 with the local admin connection; Core cache tests passed 2/2; FinanceBase tests passed 35/35; frontend unit suite passed 284/285, with the one known unrelated Manufacturing CSS localization false positive.
