# Redis Platform Cache Developer Guide

Redis is an optional acceleration layer in TODIR. PostgreSQL remains the source of truth for authorization, business data, cache revisions, invalidation intent, Outbox, and Inbox state. A Redis outage must leave the business operation correct; the shared cache falls back to the authoritative loader.

## Use the shared boundary

Business modules use `Core.Caching.IPlatformCache` through dependency injection. They must not reference `StackExchange.Redis`, `IConnectionMultiplexer`, Redis commands, or module-owned cache clients.

```csharp
var fingerprint = CacheKeyFingerprint.For(JsonSerializer.Serialize(new
{
    request.Search,
    request.LegalEntityId,
    request.Page,
    request.PageSize
}));

return await cache.GetOrCreateAsync(
    new CacheScope(tenant, request.LegalEntityId, branchId, membershipId, permissionVersion),
    PlatformCachePolicies.HrEmployeeDirectory,
    fingerprint,
    TimeSpan.FromSeconds(30),
    token => repository.LoadAuthoritativelyAsync(tenant, request, token),
    cancellationToken);
```

The loader must be complete, tenant-scoped, permission-safe, bounded, and correct when called on every request. `GetOrCreateAsync` accepts only the lowercase 64-character SHA-256 value returned by `CacheKeyFingerprint.For`; raw names, searches, emails, tokens, document numbers, uppercase hex, and other non-canonical values fail closed before a Redis key is built. Never put a search value, name, token, document number, or other raw user input in a Redis key or metric. The platform bounds oversized entries and skips serialized values larger than the configured cache-entry limit (`TODIR_CACHE_MAX_ENTRY_BYTES`, 64 KiB–16 MiB, default 1 MiB).

## Policy and scope rules

Every cache dependency must be registered in `PlatformCachePolicies` before use. A policy declares its owner, authorization boundary, consistency model, invalidation rule, sensitivity, and maximum TTL. Sensitive projections, authorization decisions, mutable financial truth, inventory availability, idempotency state, and legal or posted records are not cacheable.

The `CacheScope` must contain every boundary that can change the returned view:

- tenant;
- legal entity and branch when the query is scoped;
- membership and permission version for permission-sensitive views.

Do not use a broad tenant-wide key for a record or view that is entity-, branch-, membership-, or permission-dependent. Do not lower a policy TTL at a call site to hide an incorrect invalidation design.

## Freshness and invalidation

PostgreSQL revisions are authoritative. A cache envelope is accepted only when its schema version and revision match the current PostgreSQL revision. Writers advance the approved revision in the same transaction as the business change and durable `PlatformCacheInvalidatedV1` Outbox message. The Worker delivers the monotonic Redis marker through the Inbox; the marker is an optimization, not a freshness authority.

When adding a policy or writer integration, provide:

1. a forward migration if the database owns the revision trigger or function;
2. an out-of-order and duplicate invalidation test;
3. stale-fill rejection while a writer commits;
4. tenant, legal-entity, branch, membership, and permission isolation proof as applicable;
5. Redis-outage fallback proof;
6. an update to `docs/engineering/REDIS_PLATFORM_ACCEPTANCE_MATRIX.md`.

## Stampede and failure behavior

The shared implementation coalesces cold fills within a process and uses a bounded Redis lock/wait window across instances. Every caller that joins an in-flight fill re-reads the authoritative PostgreSQL revision after the shared task completes; a revision change causes that caller to retry instead of accepting the older value. The leader's caller token controls only that caller's wait. The shared fill uses an internal bounded timeout, so leader cancellation does not cancel independent waiters and waiter cancellation does not cancel the leader. A loader failure removes the fill entry and a later request may recover normally. It never waits indefinitely and never turns Redis availability into a business failure. Keep loaders short enough for the configured lock window or document the measured exception path. Avoid loaders that perform unbounded work or call back into the cache with the same key.

Do not catch a cache miss and return a partial or synthetic business result. Return the loader's PostgreSQL result. Do not use Redis as a queue, source of authorization, or write-ahead store.

## Verification checklist

For a cache change, run the focused Core tests and the real disposable PostgreSQL + Redis acceptance tests. Include cold-fill concurrency at 10, 50, and 100 callers, two-client behavior, stale-fill rejection, duplicate/out-of-order invalidation, tenant/permission isolation, invalid dependency/scope rejection, Redis outage and recovery, migration/seed verification, and the local performance harness. Record p50/p95/p99, database commands, revision-read cost, loader count, Redis key count/memory, and the exact local limits. These results are a verified development envelope, not a production capacity promise.

## Configuration and security

Development may use the loopback Redis service from `ops/compose/platform-dev.yml`. Non-development enabled Redis requires TLS and ACL credentials. Secrets belong in environment or secret configuration and never in source, keys, metrics, or logs. Keep metric dimensions bounded to dependency and outcome; never add tenant IDs, document IDs, query strings, SQL, or search values.

## Decision examples

Use these two decisions as the minimum review exercise for a new module; they are examples, not new cache integrations.

### Permission-scoped reference directory — cacheable when measured

A bounded reference directory that is read frequently, changes under an owned revision, and returns only permission-safe fields may use `IPlatformCache` after the owner registers an approved policy. The design must carry trusted tenant/entity/branch/membership/permission scope, a canonical `CacheKeyFingerprint.For` value, bounded pagination and payload size, same-transaction revision advancement, and an explicit frontend query-key/freshness contract. The module must prove the cache-enabled versus PostgreSQL-authoritative performance benefit, invalidation, isolation, outage/recovery, and cross-user freshness through the acceptance matrix before adoption.

### Operational or financial workflow — PostgreSQL/API only

A posted journal, stock availability decision, payment/idempotency workflow, authorization decision, or mutable operational command remains `PostgreSQL/API only`. Do not add a cache policy or Redis dependency to hide query cost. The frontend still uses the approved TanStack Query keys, mutation invalidation, scope clearing, and documented stale/conflict/offline behavior; the backend remains the sole authority. This is a correct and preferred outcome when cache consistency risk or operational cost outweighs a measured read benefit.
