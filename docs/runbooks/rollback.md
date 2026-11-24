# Rollback

Stop promotion, preserve sanitized logs and correlation IDs, and identify the deployed commit plus every image digest. A rollback is valid only when the last known-good application release is compatible with the database schema already present.

1. Stop promotion and keep the database, Outbox, Inbox, audit trail, inventory movements, payroll, and posted financial facts unchanged.
2. Re-apply only the API, Worker, and Web containers using their previously recorded immutable image digests. Do not rebuild from a moving tag and do not run a second migration implicitly.
3. Verify API readiness, Data Protection key continuity, authenticated permissions/RLS, Worker readiness, RabbitMQ redelivery, Inbox idempotency, and pending Outbox age.
4. If the database migration is incompatible with the older binary, block application rollback. Do not drop, rewrite, or reverse authoritative business data. Use the approved forward-fix migration and deploy a compatible application version, or stop and invoke the Disaster Recovery procedure.
5. Record the reason, release/digest transition, migration compatibility decision, data-integrity checks, recovery checks, and final promotion decision without secrets or private payloads.
