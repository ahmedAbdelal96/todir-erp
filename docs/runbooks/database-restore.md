# Database restore and PITR

This runbook is for a controlled restore. It is not permission to overwrite the primary database.

## Before recovery

1. Declare the incident, target timestamp/LSN, operator, and intended legal-entity/tenant scope.
2. Fence API writes and stop or isolate Workers that can create external effects. Do not resume them because RabbitMQ is healthy.
3. Confirm the backup manifest, SHA-256, encryption-key reference, PostgreSQL major version, and repository availability.
4. Use a disposable PostgreSQL target with a unique name. The shared development database, `todir-postgres-*` containers, and persistent volumes are protected targets. A restore target is accepted only inside a PostgreSQL container carrying the recovery disposable-ownership labels; an unrecognized or shared container is rejected.

## Restore

For local validation, run:

```powershell
pwsh -NoProfile -File backend/DbMigrator/Database/pitr-recovery-drill.ps1
```

For an operator-controlled logical restore, use `restore-verify.ps1` with `-AllowDisposableRestore` and a `recovery_verify_<unique>` database name. The script refuses protected development containers/databases, unrecognized containers, and any target database that already exists. It validates the manifest and checksum, decrypts only with the supplied key, restores with `pg_restore --exit-on-error`, proves a verification table and FORCE RLS, and removes only the database created by this invocation by default. It never pre-drops a name and never uses forced deletion during cleanup.

For physical PITR, restore `pg_basebackup` into the isolated target, provide only the approved WAL archive through `restore_command`, create `recovery.signal`, and set exactly one recovery target. A named target must prove pre-target rows exist and post-target rows do not. A latest-LSN target must prove the latest committed row exists. Missing or corrupt WAL must fail visibly; never promote a target that did not reach the requested recovery point.

## Evidence and retention

The PITR drill preserves a sanitized `recovery-evidence.json` under its unique `artifacts` evidence directory for both success and failure. It records the commit, PostgreSQL version, backup ID, recovery target, individual checks, duration, failure summary when applicable, and final acceptance status. Temporary database containers, private backup bytes, decrypted files, and isolated storage are cleaned separately. API smoke logs and encryption material are not part of retained evidence.

Evidence is explicitly pruned with the ownership-checked helper; it never deletes logical backup repositories or physical base-backup/WAL data:

```powershell
pwsh -NoProfile -File backend/DbMigrator/Database/evidence-retention.ps1 -ArtifactsRoot artifacts -RetentionCount 10 -WhatIf
pwsh -NoProfile -File backend/DbMigrator/Database/evidence-retention.ps1 -ArtifactsRoot artifacts -RetentionCount 10 -Delete
```

`retention.ps1` manages only verified `logical-custom` repositories. It rechecks manifest shape, safe artifact references, byte counts, and SHA-256 checksums before dry-run or deletion, and preserves at least one valid logical backup. Physical base-backup and WAL/PITR retention require a separately approved deployment policy and are not altered by this helper. `backup.ps1` requires a valid encryption key unless the process explicitly declares `Development`, `Test`, or `Testing`; production-oriented calls must use `-RequireEncryption`.

## After restore

Run the controlled `DbMigrator` against the isolated target, then verify role ownership, grants, FORCE RLS, tenant and legal-entity isolation, referential integrity, journals/inventory projections, audit rows, Outbox/Inbox state, and `/health/ready`. Compare migration checksums with the source version. Restore private object-storage objects from their provider backup/version boundary and verify checksums; database file metadata is not the object bytes.

Before resuming Workers, reconcile pending Outbox/Inbox messages and every external-effect idempotency identity. Resume consumers in a bounded window and monitor duplicate/retry outcomes. Record target timestamp, measured RPO/RTO, WAL lag, backup ID, key reference, evidence paths, and approval before traffic cutover.

## Failure handling

Wrong key, checksum mismatch, incompatible major version, missing/corrupt WAL, wrong target, interrupted restore, unavailable repository, or failed readiness is a hard failure. Keep the source untouched, preserve operator-visible logs/evidence, clean only resources owned by the drill, and retry into a new isolated target after the cause is corrected.
