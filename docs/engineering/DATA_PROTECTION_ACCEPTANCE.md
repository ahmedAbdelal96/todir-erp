# Data protection acceptance

This document is the local acceptance record and operator contract for the shared data-protection foundation. It does not certify a production deployment.

## Commands

Run from the repository root on the local Windows development machine:

```powershell
pwsh -NoProfile -File backend/DbMigrator/Database/pitr-recovery-drill.ps1
pwsh -NoProfile -File backend/DbMigrator/Database/retention.ps1 -RepositoryPath <isolated-repository> -RetentionCount 2 -WhatIf
pwsh -NoProfile -File backend/DbMigrator/Database/restore-safety-regression.ps1
pwsh -NoProfile -File backend/DbMigrator/Database/evidence-retention.ps1 -ArtifactsRoot artifacts -RetentionCount 10 -WhatIf
```

The PITR drill allocates unique disposable PostgreSQL 18 containers and temporary storage, applies the canonical migrator by default, creates encrypted logical backup evidence, takes a physical base backup, archives WAL, restores named and latest targets, starts the API against the restored target, checks role/RLS isolation, and rejects wrong-key, unsafe restore, failed-backup, missing-WAL, and corrupt-WAL paths with cause-specific assertions. With no `-EvidenceRoot`, each invocation creates a new unique `artifacts/recovery-<guid>` directory and refuses explicit reuse of an existing directory. Evidence is retained by default and contains only sanitized run metadata and results. `-SkipApplicationMigration` is a fast probe mode for repeatable command debugging; it is not a substitute for the default full-schema acceptance run.

## Safety requirements

- The shared development PostgreSQL container and Docker volumes are never a target.
- The restore database name must be `recovery_verify_<unique>` and the restore switch must be explicit.
- Existing restore targets are never overwritten or dropped; disposable ownership labels are required and cleanup is limited to targets created by the current run.
- Evidence and repositories must have ownership markers and must not be reused.
- Secrets are generated in memory for the drill and are not written to Git or retained evidence; backup encryption is explicit outside development/test environments.
- Logical retention validates every manifest and artifact checksum before it removes any artifact and never touches physical base-backup/WAL chains.
- Worker/external-effecting consumers are not resumed until Outbox/Inbox and idempotency reconciliation is complete.

## Acceptance matrix

| Requirement | Local proof | Status |
|---|---|---|
| Logical custom backup and checksum manifest | `backup.ps1` + `pg_restore --list` | PASS |
| AES-256-GCM and wrong-key rejection | encrypted artifact and wrong key drill | PASS |
| Isolated physical restore | `pg_basebackup`, disposable target | PASS |
| Named PITR | baseline/target present, post-target absent | PASS |
| Latest archived point | post-target present at latest LSN | PASS |
| Missing/corrupt WAL | bounded recovery failure with WAL retrieval/corruption cause asserted | PASS |
| Runtime role and FORCE RLS | tenant B is invisible under tenant A context | PASS |
| Restored API readiness | `/health/ready` against restored target | PASS |
| Failed backup detection | nonexistent database invocation | PASS |
| Existing/similarly named database protection | disposable labeled container, protected marker database, no pre-drop | PASS |
| Unrecognized container rejection | unlabeled disposable container | PASS |
| Disposable target cleanup ownership | current-run target created and removed; pre-existing target preserved | PASS |
| Sanitized success/failure evidence | retained `recovery-evidence.json`, no secret/private backup bytes | PASS |
| Retention-safe deletion | manifest shape, artifact existence, byte count, checksum, `-WhatIf` and bounded deletion | PASS (isolated repository) |
| Evidence retention safety | ownership markers, explicit `-Delete`, bounded count | PASS (isolated evidence root) |
| Negative-test reliability | existing-target, wrong-key, unsafe-target, failed-backup, missing-WAL, corrupt-WAL, and checksum tests assert their intended causes | PASS |
| Default drill repeatability | two consecutive no-argument runs retained separate unique evidence directories | PASS |
| Object-storage private document recovery | provider-specific environment required | DEFERRED locally |
| RabbitMQ Outbox/Inbox replay and external-effect reconciliation | existing messaging gates + runbook sequence | DEFERRED to broker/effect drill |

Production backup destination, secret escrow, object-storage versioning/replication, capacity, alert exporter, and formal RPO/RTO approval remain deployment-owned follow-up work.

## Final local evidence

On 2026-11-23, two consecutive no-argument full-schema drills completed with all 11 checks PASS: `artifacts/recovery-5cd5c91c6b6a4a0ca2726aa973d6ba17` in 589.7745928 seconds and `artifacts/recovery-205376de39bf4f6bb29d1accf75f65dc` in 469.584509 seconds. Each run applied all 257 canonical migration scripts to its disposable PostgreSQL 18 origin, verified the encrypted logical artifact, restored the named PITR target with `baseline=1`, `target=1`, `postTarget=0`, verified the latest archived LSN, asserted the specific wrong-key, unsafe-target, failed-backup, missing-WAL, and corrupt-WAL causes, proved runtime-role/RLS isolation, and returned API `/health/ready` successfully. After the final commit, a fast probe also completed all 11 checks in 163.2696615 seconds with evidence under `artifacts/recovery-7c174c8756c543719d9d4434427baa92`. The focused safety regression completed all six checks PASS, including a valid `recovery_verify_<32-hex>` existing-database target rejected by the real already-exists guard with its marker preserved, protected-name rejection, unrecognized-container rejection, checksum rejection, and current-run target cleanup. Previous failed drills remain retained as sanitized `finalAcceptance=FAIL` evidence without secrets or backup bytes.

Post-change local gates: ArchitectureTests 23/23, SecurityTests 7/7 against the disposable `todir_test_fresh` database, selected PostgreSQL/RabbitMQ integration tests 3/3, Backend Release build 0 warnings/0 errors, release-gate structural controls 73/73, PowerShell parse, formatter for the changed migrator project, explicit evidence-reuse rejection, and `git diff --check` passed. The repository-wide formatter still reports six pre-existing Ledger whitespace findings, and the repository OpenAPI validator still reports the pre-existing Inventory frozen-contract endpoint mismatch; neither is owned by this data-protection change and neither was weakened.
