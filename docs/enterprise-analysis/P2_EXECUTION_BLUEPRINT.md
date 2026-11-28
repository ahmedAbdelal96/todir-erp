# P2 execution blueprint

P2 may start only after the owner decisions in [ARCHITECTURE_APPROVAL_BRIEF.md](ARCHITECTURE_APPROVAL_BRIEF.md) are recorded as `APPROVED`. It must remain on `dev`, use the current module/host conventions, and avoid unrelated Planning, Quality, security-policy or navigation rewrites.

## Ordered slices

1. Register the formal Analysis module manifest and explicit API/worker composition; validate solution boundaries and lifecycle/entitlement integration.
2. Add DbMigrator-owned `business_analysis` schema, RLS, indexes and immutable/publication constraints. Run disposable migration/seed and rollback/recovery checks.
3. Implement the typed Subledger read capability with bounded cursor pages, scope, cutoff, watermark, completeness and cancellation semantics.
4. Implement run validation, idempotency, durable job lifecycle, lease/fencing, retry classification, cancellation and observability.
5. Implement deterministic Collections Hygiene calculation, evidence references, immutable result publication and recommendation/no-recommendation paths.
6. Implement proposed API endpoints using existing auth/effective-access/problem-details conventions and contract tests.
7. Implement the owning-module Arabic RTL route and history/result/review surfaces; do not add global Analysis navigation.
8. Execute real-stack acceptance with disposable PostgreSQL, canonical Development Seed, tenant/entity/branch matrices and the performance protocol.

## Host and deployment shape

The recommended host is a minimal .NET Analysis worker executable in the existing backend solution, with its own process/pool/resource limits and the same approved PostgreSQL/RabbitMQ/observability infrastructure. RabbitMQ is optional wake-up/notification only; database job state survives broker loss. The existing operational Worker remains unchanged unless a new evidence-backed integration need is found.

## Release gates

No P2 release without: architecture tests, migration/RLS tests, owner-contract tests, explicit-run/idempotency tests, retry/lease/cancel/restart tests, source completeness tests, recommendation audit tests, authorization/RLS matrix, real frontend route evidence, and repeatable performance report with dataset size and environment. A successful Development Seed run is not production capacity certification.
