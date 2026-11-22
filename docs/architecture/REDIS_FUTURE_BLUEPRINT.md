# Redis Future Blueprint — superseded by the Platform Foundation

The former performance-only Redis proposal is now implemented as the shared Platform Redis Foundation. The authoritative design is [REDIS_PLATFORM_FOUNDATION_DESIGN.md](REDIS_PLATFORM_FOUNDATION_DESIGN.md).

Redis remains an acceleration and coordination layer only. PostgreSQL owns business truth, cache dependency revisions, durable invalidation intent, authorization, accounting, inventory availability, idempotency, Outbox, and Inbox state. Any future cache candidate must extend the policy registry and acceptance matrix; it must not introduce a module-owned Redis client or bypass the Platform cache contract.

Implemented baseline:

- local Redis 7.4 with AOF, bounded 256 MB memory, and `allkeys-lru` development eviction policy;
- revisioned keys/envelopes with schema versioning, tenant/legal-entity/branch/membership/permission boundaries, and SHA-256 query fingerprints;
- PostgreSQL revision increments and `PlatformCacheInvalidatedV1` Outbox records in the same transaction as HR employee/employment writes;
- Worker Inbox idempotency and monotonic Redis invalidation markers;
- bounded-freshness HR employee directory/basic detail integration, with sensitive HR records explicitly non-cacheable;
- timeout-bounded Redis failure fallback to PostgreSQL.

Deferred by policy: distributed locks for business correctness, authorization truth, financial data, inventory availability, legal documents, personal identifiers, compensation, payroll, and unrestricted report caching.
