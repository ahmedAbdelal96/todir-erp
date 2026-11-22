# Redis Future Blueprint

Redis is intentionally not part of the current platform performance foundation. PostgreSQL remains the source of truth and the existing request-scoped authorization cache is sufficient for the measured in-request duplication.

Before implementation, a future Redis proposal must include a measured workload, ownership, key/version design, TTL and invalidation rules, stampede protection, bounded memory policy, outage behavior, security review, and a rollback plan. Redis cannot become the source of truth for authorization, accounting, inventory availability, idempotency, Outbox, or Inbox state.

Approved future candidates are limited to:

- read acceleration for safe, versioned reference data;
- distributed ephemeral coordination where PostgreSQL row locking is demonstrably insufficient;
- shared session/draft acceleration only when the authoritative state and recovery path are explicit.

Every candidate must preserve tenant and legal-entity isolation in keys and invalidation. A broker is not a cache, and a cache hit is never financial or inventory proof.
