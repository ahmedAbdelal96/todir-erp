# Redis Platform Foundation

Status: implemented local development foundation on `dev`.

## Purpose and boundaries

Redis accelerates safe reads and broadcasts cache invalidation. It never becomes the system of record. Platform owns the mechanics (connection, serialization, keys, TTL, failure fallback, metrics, invalidation transport); the owning module owns the dependency name, projection meaning, permissions, and mutation transaction.

The policy registry starts deliberately small:

| Dependency | Policy | Scope | TTL | Reason |
| --- | --- | --- | --- | --- |
| `hr.employee.directory` | bounded staleness | tenant + legal entity + membership + permission version | 30 seconds | basic directory/display fields only |
| `finbase.currency.reference` | stable reference | tenant cache partition, permission-gated endpoint | 1 hour | controlled active currency dataset; writes are migration-owned |
| HR identifiers, documents, compensation, payroll, attendance, leave balances | non-cacheable | n/a | n/a | sensitive or operationally authoritative |

The stable currency reference example is intentionally narrow: the FinanceBase endpoint remains permission-gated, the dataset is write-locked to migrations, and the cache key is tenant-partitioned. A schema/policy version change or deployment invalidates the one-hour bounded-staleness window; mutable reference tables must not reuse this policy without an owning revision trigger.

## Consistency and invalidation

`platform.cache_dependency_revision` is PostgreSQL-owned. A write to `business_hr.employee` or `business_hr.employment` advances the relevant revision and inserts `PlatformCacheInvalidatedV1` into `platform.outbox_message` through a security-definer trigger. The trigger runs in the writer transaction, so rollback removes both the business change and invalidation intent.

The Worker dispatches the durable Outbox event through RabbitMQ. `platform-cache-invalidation` uses the existing PostgreSQL Inbox uniqueness/lease protocol, then writes a monotonic Redis marker. Duplicate, delayed, or out-of-order events cannot reduce a revision. Reads still consult PostgreSQL revision state before accepting an envelope; Redis outage or delayed delivery therefore cannot create stale business truth.

Read-through fills use a short Redis lock and a double revision check. A fill is published only when the revision before and after the database read is identical. This closes the stale-fill race and bounds stampede pressure per key.

## Key, serialization, and isolation contract

Keys contain a platform schema version, tenant, dependency, legal entity, branch, membership, permission version, and a SHA-256 fingerprint of non-sensitive query parameters. Raw names, searches, identifiers, tokens, and document values are never written to keys or metrics. Envelopes include schema version, PostgreSQL revision, cache timestamp, and value. A schema-version change is a safe global miss.

Every cache read is permission-gated before the cache is called. Membership and permission-version boundaries prevent a privileged result from crossing users or authorization changes. Tenant and legal-entity scope is never accepted from the browser as authority.

## Failure, operations, and security

The development service is `redis:7.4-alpine`, AOF-enabled, 256 MB bounded, and `allkeys-lru`, published on host port 6380 to avoid the pre-existing Windows Redis service on 6379. Connect, sync, and async timeouts are one second by default. Redis connection failures are logged as bounded operational warnings and the request executes against PostgreSQL. Cache failures never fail a business mutation. Graceful shutdown disposes the multiplexer through the service provider.

Metrics are bounded by dependency only: hits, misses, invalidations, and failures. No tenant ID, document ID, search string, SQL, or personal data is emitted. Redis is disabled by default outside development unless explicitly configured.

## Acceptance evidence

Run the local matrix in [REDIS_PLATFORM_ACCEPTANCE_MATRIX.md](../engineering/REDIS_PLATFORM_ACCEPTANCE_MATRIX.md). Required evidence is after the last code/migration change and includes migration, Core cache tests, backend Release build, Redis health, HR hit/miss/fallback behavior, Outbox → RabbitMQ → Worker → Inbox → Redis invalidation, tenant/permission isolation, and frontend 30-second freshness settings. PJ38-style heavyweight performance certification remains outside this foundation.
