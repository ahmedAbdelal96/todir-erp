# Redis Platform Foundation Execution Checkpoint

Status: complete and locally verified on `dev` after the consistency closure.

Authorized scope: shared Redis cache contract, PostgreSQL revision and invalidation, durable Outbox/RabbitMQ/Worker/Inbox propagation, real local Redis, safe HR and stable FinanceBase currency integrations, frontend freshness, security/isolation, failure behavior, observability, governance, and handoff.

Completed and verified:

- Platform cache contract with Redis read-through, TTL, serialization version, tenant/entity/branch/membership/permission scoping, stale-fill revision checks, stampede lock, bounded metrics, and PostgreSQL fallback.
- HR directory/basic detail integration; sensitive HR data remains non-cacheable. HR invalidation now covers tenant-wide, entity, and branch scopes, including the old scope on transfers.
- FinanceBase active currency reference integration with a one-hour stable-reference policy.
- `platform.cache_dependency_revision`, HR triggers, same-transaction durable invalidation Outbox, Worker consumer, Inbox idempotency, and atomic Lua monotonic Redis markers. PostgreSQL remains authoritative; Redis outage leaves the Inbox retryable.
- Local Redis 7.4 AOF service with bounded memory and `allkeys-lru`, published only on `127.0.0.1:6381` because Memurai owns 6380 and Windows Redis owns 6379 on this workstation. The multiplexer reconnects after unavailable startup and is disposed by the host.
- Frontend HR revision polling (10 seconds) invalidates list/detail queries without touching unsaved form state; query freshness is bounded eventual, not an immediate cross-session push guarantee.
- Real-stack automated acceptance covers atomic ordering, two-instance stale-fill protection, tenant/permission scope, and PostgreSQL revision authority.

Final evidence: [REDIS_PLATFORM_ACCEPTANCE_MATRIX.md](REDIS_PLATFORM_ACCEPTANCE_MATRIX.md).

Known unrelated repository gates: the pre-existing frontend localization guard flags Manufacturing CSS, and the frozen Inventory OpenAPI file is missing a current endpoint. PJ38 heavyweight performance certification remains deferred by Product Owner decision.
