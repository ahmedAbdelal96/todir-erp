# Data protection and disaster recovery foundation

Status: Accepted for the local development foundation

## Decision

Use PostgreSQL-native physical backups with `pg_basebackup`, WAL archiving through `archive_command`, and recovery targets through `recovery.signal`. Use PostgreSQL custom-format `pg_dump` as a logical portability and integrity check. The current repository does not introduce pgBackRest because the approved local PostgreSQL 18 image already supplies the native tools, the foundation needs one simple provider contract, and no second backup engine is required for the local acceptance scope.

The implementation is deliberately provider-oriented: a future production provider may replace the native command set only after an ADR proves equivalent checksum, encryption, retention, PITR, failure, security, and restore-drill guarantees. Native local evidence must not be described as a production SLA.

## Boundaries

- PostgreSQL owns business truth, WAL, tenant/legal-entity data, migrations, RLS, Outbox, Inbox, and audit records.
- The API and Worker never apply production migrations or seed data implicitly.
- Backup encryption uses an independent 32-byte AES-256-GCM key. The key is not stored with the artifact; its secret-manager reference and recovery escrow belong to operations.
- Data Protection keys/certificates protect application cookies and tokens and are recovered separately from database backup encryption.
- Private object storage remains a separate encrypted/versioned provider boundary. Database metadata alone is not a document backup.
- RabbitMQ is not the source of truth. Before consumers resume after restore, reconcile persisted Outbox/Inbox state and external-effect idempotency identities.

## Recovery contract

1. Fence API writes and stop or isolate external-effecting Worker consumers.
2. Capture the requested recovery timestamp/LSN and verify the backup manifest and key identity.
3. Restore only into an isolated target and validate checksum, migration compatibility, roles, grants, RLS, tenant isolation, and application readiness.
4. Reconcile Outbox/Inbox and external provider idempotency before routing traffic or resuming consumers.
5. Record measured duration, recovery point, operator, evidence path, and approval before cutover.

The local drill in `backend/DbMigrator/Database/pitr-recovery-drill.ps1` proves a baseline and target row are present while a post-target row is absent, then proves the latest archived point contains the post-target row. It also proves that missing or modified WAL fails closed.

## Operational targets

The product target is policy-owned and must be confirmed per production deployment. For this repository, local evidence records measured values only: the final full-schema drill completed in approximately 337 seconds and the lightweight repeatable drill completed in approximately 121 seconds on the developer machine. These are not production RTO claims. Production RPO/RTO, retention, archive capacity, and restore-drill cadence must be configured by the deployment owner and monitored through the alert contract.

## Rejected shortcuts

Do not overwrite the primary database during a drill, restore a backup without its manifest/key, delete an archive without retention evidence, treat a healthy broker as proof of durable business processing, resume consumers before reconciliation, or claim document recovery from database metadata alone.
