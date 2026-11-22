# Redis Platform Foundation Execution Checkpoint

Status: complete and locally verified on `dev` after the real HR consistency closure on 2026-11-23.

Authorized scope: shared Redis cache contract, PostgreSQL revision and invalidation, durable Outbox/RabbitMQ/Worker/Inbox propagation, real local Redis, safe HR and stable FinanceBase currency integrations, frontend freshness, security/isolation, failure behavior, observability, governance, and handoff.

Completed and verified:

- Platform cache contract with Redis read-through, TTL, serialization version, tenant/entity/branch/membership/permission scoping, stale-fill revision checks, stampede lock, bounded metrics, and PostgreSQL fallback.
- HR directory/basic detail integration; sensitive HR data remains non-cacheable. HR invalidation now covers tenant-wide, entity, and branch scopes, including the old scope on transfers.
- FinanceBase active currency reference integration with a one-hour stable-reference policy.
- `platform.cache_dependency_revision`, HR triggers, same-transaction durable invalidation Outbox, Worker consumer, Inbox idempotency, and atomic Lua monotonic Redis markers. PostgreSQL remains authoritative; Redis outage leaves the Inbox retryable.
- Local Redis 7.4 AOF service with bounded memory and `allkeys-lru`, published only on `127.0.0.1:6381` because Memurai owns 6380 and Windows Redis owns 6379 on this workstation. The multiplexer reconnects after unavailable startup and is disposed by the host.
- Frontend HR revision polling (10 seconds) invalidates list/detail queries without touching unsaved form state; query freshness is bounded eventual, not an immediate cross-session push guarantee.
- Real-stack automated acceptance covers atomic ordering, two-instance stale-fill protection, tenant/permission scope, and PostgreSQL revision authority.
- HR employee profile correction is now a canonical versioned workflow: `PUT /api/v1/hr/employees/{id}/profile` validates `hr.employee.manage`, locks and checks the expected version, updates the directory projection, appends audit/timeline evidence, and advances the tenant-wide cache dependency revision in the same transaction.
- Two real browser users and two API instances proved cross-session freshness: User B changed a seeded employee and User A's already-open workspace displayed the new Arabic name without a page reload; the second user verified search and detail after propagation. The final Playwright run passed 1/1 with 81 seeded directory rows and 6.744 seconds freshness latency.
- Fresh disposable database `todir_redis_hr_1795385688183` verified 245 ordered migration scripts and the development seed. The final profile mutation produced a published outbox invalidation at revision 339; RabbitMQ delivery and Worker `platform-cache-invalidation` Inbox processing were persisted in PostgreSQL.
- A controlled local Redis stop/restart proved PostgreSQL fallback logging and recovery on both API instances and the Worker; the API health endpoint stayed `200`, Redis returned healthy, and no volume or database was destroyed. The authenticated HR fallback path remains covered by the existing real-stack API implementation and acceptance tests.
- The focused HR API integration passed 1/1 for the profile route's permission boundary, optimistic-concurrency `409`, cross-entity denial, and persisted `EmployeeProfileCorrected` audit/timeline evidence.

Final evidence: [REDIS_PLATFORM_ACCEPTANCE_MATRIX.md](REDIS_PLATFORM_ACCEPTANCE_MATRIX.md).

Known unrelated repository gates: the pre-existing frontend localization guard flags Manufacturing CSS, and the frozen Inventory OpenAPI file is missing a current endpoint. PJ38 heavyweight performance certification remains deferred by Product Owner decision. The HR directory route is intentionally legal-entity-contexted in the current shell; the backend freshness/profile contracts also support tenant-wide scope. HR manager-only `read_team` navigation remains outside this consistency closure because it does not grant directory mutation.
