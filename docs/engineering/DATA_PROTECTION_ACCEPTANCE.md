# Data protection acceptance

This document is the local acceptance record and operator contract for the shared data-protection foundation. It does not certify a production deployment.

## Commands

Run from the repository root on the local Windows development machine:

```powershell
pwsh -NoProfile -File backend/DbMigrator/Database/pitr-recovery-drill.ps1
pwsh -NoProfile -File backend/DbMigrator/Database/retention.ps1 -RepositoryPath <isolated-repository> -RetentionCount 2 -WhatIf
```

The PITR drill allocates unique disposable PostgreSQL 18 containers and temporary storage, applies the canonical migrator by default, creates encrypted logical backup evidence, takes a physical base backup, archives WAL, restores named and latest targets, starts the API against the restored target, checks role/RLS isolation, and rejects wrong-key, unsafe, failed-backup, missing-WAL, and corrupt-WAL paths. `-SkipApplicationMigration` is a fast probe mode for repeatable command debugging; it is not a substitute for the default full-schema acceptance run.

## Safety requirements

- The shared development PostgreSQL container and Docker volumes are never a target.
- The restore database name must be `recovery_verify_<unique>` and the restore switch must be explicit.
- Evidence and repositories must have ownership markers and must not be reused.
- Secrets are generated in memory for the drill and are not written to Git or logs.
- Worker/external-effecting consumers are not resumed until Outbox/Inbox and idempotency reconciliation is complete.

## Acceptance matrix

| Requirement | Local proof | Status |
|---|---|---|
| Logical custom backup and checksum manifest | `backup.ps1` + `pg_restore --list` | PASS |
| AES-256-GCM and wrong-key rejection | encrypted artifact and wrong key drill | PASS |
| Isolated physical restore | `pg_basebackup`, disposable target | PASS |
| Named PITR | baseline/target present, post-target absent | PASS |
| Latest archived point | post-target present at latest LSN | PASS |
| Missing/corrupt WAL | bounded recovery failure and non-ready target | PASS |
| Runtime role and FORCE RLS | tenant B is invisible under tenant A context | PASS |
| Restored API readiness | `/health/ready` against restored target | PASS |
| Failed backup detection | nonexistent database invocation | PASS |
| Retention-safe deletion | marker, verified manifests, `-WhatIf`/bounded deletion | PASS (script gate) |
| Object-storage private document recovery | provider-specific environment required | DEFERRED locally |
| RabbitMQ Outbox/Inbox replay and external-effect reconciliation | existing messaging gates + runbook sequence | DEFERRED to broker/effect drill |

Production backup destination, secret escrow, object-storage versioning/replication, capacity, alert exporter, and formal RPO/RTO approval remain deployment-owned follow-up work.

## Final local evidence

On 2026-11-23, the full-schema drill completed with all 11 checks PASS in 337.2095369 seconds. The run applied all 257 canonical migration scripts to its disposable PostgreSQL 18 origin, verified the encrypted logical artifact, restored the named PITR target with `baseline=1`, `target=1`, `postTarget=0`, verified the latest archived LSN, rejected the wrong key, unsafe restore, failed backup, missing WAL, and corrupt WAL, proved runtime-role/RLS isolation, and returned API `/health/ready` successfully. After the final secret-transport hardening change, the lightweight repeatable probe was rerun and completed all 11 checks in 123.5672869 seconds.

Post-change local gates: ArchitectureTests 23/23, SecurityTests 7/7 against the disposable `todir_test_fresh` database, selected PostgreSQL/RabbitMQ/API integration tests 5/5, transaction/outbox integration tests 4/4, Backend Release build 0 warnings/0 errors, release-gate structural controls 73/73, PowerShell parse, formatter for the changed migrator project, and `git diff --check` passed. The repository-wide formatter still reports six pre-existing Ledger whitespace findings, and the repository OpenAPI validator still reports the pre-existing Inventory frozen-contract endpoint mismatch; neither is owned by this data-protection change and neither was weakened.
