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

## Infrastructure security boundary

The staging stack uses separate credentials for the PostgreSQL administrator/migrator, `erp_app`, RabbitMQ, MinIO root, and the MinIO private-files identity. API and Worker containers receive only runtime credentials; they never receive the PostgreSQL administrator password. MinIO bootstrap fails closed unless the existing policy/user state is verified, and the application identity is limited to the `private-files` bucket policy. The development compose file uses intentionally non-production credentials and binds PostgreSQL, RabbitMQ, RabbitMQ management, and MinIO to loopback only.

OpenTofu `sensitive` variables hide values from normal plan output but do not encrypt state. Shared or staging runs must use the approved encrypted, locked, audited remote state backend. A local state file is disposable development material: do not commit, upload, or retain it after destroying an isolated stack. Rotate credentials by applying the new values, verifying service recovery, then revoking the old values; never print connection strings or secret-bearing container inspection in evidence.

The API trusts forwarded headers only from the reserved reverse-proxy IP (`TODIR_TRUSTED_PROXY_ADDRESSES`). The staging web container receives that static private address; other containers on the shared Docker subnet are not trusted proxies. Use the network-CIDR setting only for an explicitly reviewed legacy topology, never together with the address setting, and do not replace either with trust-all forwarded-header handling.

## Tenant export/offboarding

Freeze new writes, produce a tenant-scoped export, verify checksums and audit trail, retain legal attachments according to policy, then disable access. Destructive deletion requires a separately approved retention decision.
