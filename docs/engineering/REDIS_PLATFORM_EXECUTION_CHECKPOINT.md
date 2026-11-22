# Redis Platform Foundation Execution Checkpoint

Status: complete and locally verified on `dev`.

Authorized scope: shared Redis cache contract, PostgreSQL revision and invalidation, durable Outbox/RabbitMQ/Worker/Inbox propagation, real local Redis, safe HR and stable FinanceBase currency integrations, frontend freshness, security/isolation, failure behavior, observability, governance, and handoff.

Completed and verified:

- Platform cache contract with Redis read-through, TTL, serialization version, tenant/entity/branch/membership/permission scoping, stale-fill revision checks, stampede lock, bounded metrics, and PostgreSQL fallback.
- HR directory/basic detail integration; sensitive HR data remains non-cacheable.
- FinanceBase active currency reference integration with a one-hour stable-reference policy.
- `platform.cache_dependency_revision`, HR triggers, same-transaction durable invalidation Outbox, Worker consumer, Inbox idempotency, and monotonic Redis markers.
- Local Redis 7.4 AOF service with bounded memory and `allkeys-lru`, published on host port 6380 because Windows Redis already owns 6379.
- Two API instances, Worker, RabbitMQ, PostgreSQL, Redis restart fallback, tenant isolation, and fresh Development Seed proof.

Final evidence: [REDIS_PLATFORM_ACCEPTANCE_MATRIX.md](REDIS_PLATFORM_ACCEPTANCE_MATRIX.md).

Known unrelated repository gates: the pre-existing frontend localization guard flags Manufacturing CSS, and the frozen Inventory OpenAPI file is missing a current endpoint. PJ38 heavyweight performance certification remains deferred by Product Owner decision.
