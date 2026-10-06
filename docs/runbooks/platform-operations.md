# Platform operations runbook

## Deploy and rollback

Build immutable images tagged by commit SHA, run migrations from the dedicated migrator, smoke test health and API contracts, then deploy API, worker, and web. Roll back application images first; use a forward-fix for incompatible schema changes.

## Database restore

Restore into an isolated environment, verify PITR timestamp, reapply grants/policies, run RLS and migration checks, start the application against the restored database, and record RPO/RTO evidence before production use.

## Broker and outbox outage

Keep database commits where asynchronous work is not an immediate invariant. Alert on pending age and backlog; restart the dispatcher and verify publisher confirmations. Do not manually publish rows or acknowledge consumer messages before durable work.

## Poison message and replay

Inspect the message and error reason, correct the defect or quarantine payload, authorize replay, and record the operator and reason in audit. Replay remains idempotent through the inbox key.

## Worker/object storage/identity incidents

Scale or restart workers without changing tenant queues. During object-store outage keep metadata state explicit and do not issue public URLs. During identity incident revoke affected security stamps/sessions, rotate secrets, and preserve evidence.

## Secret rotation and support access

Rotate through the secret manager with overlap only for the documented transition. Break-glass access requires reason, approval where possible, MFA, visible tenant banner, expiry, and full audit. Never create a permanent support superuser.

## Tenant export/offboarding

Freeze new writes, produce a tenant-scoped export, verify checksums and audit trail, retain legal attachments according to policy, then disable access. Destructive deletion requires a separately approved retention decision.
