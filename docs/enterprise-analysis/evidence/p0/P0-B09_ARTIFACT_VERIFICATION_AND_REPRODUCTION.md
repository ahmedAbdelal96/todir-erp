# P0-B09 — Artifact Verification and Reproduction

**Result:** `PASS — EVIDENCE RECONCILED`  
**Branch:** `dev`  
**Verification date:** 2026-11-25 (Africa/Cairo)  
**Raw-artifact retention:** raw files are present in the local ignored `artifacts/performance` directory used for this verification. This Git commit preserves the auditable manifest, hashes and derived tables; it does not claim that multi-megabyte raw logs are durably archived in Git.

## Raw artifact set

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `artifacts/performance/p0b-20261125-diagnostic/platform-performance-todir_perf_20261125090812_10848_4c9d0e78.json` | 88,634 | `9cda1ec300d2e08f7524f3f3dbe7edfbad27a416a8d18b4eb397ba37fbf518e5` |
| `artifacts/performance/p0b-20261125-load/platform-performance-todir_perf_20261125091019_41032_6b11ec83.json` | 233,169 | `b100fc9a2b50d27223b159beced06dd6da5b14152f5493448b78f24227e5fa4e` |
| `artifacts/performance/p0b-20261125-load-repeat/platform-performance-todir_perf_20261125091708_30864_85500d9a.json` | 231,575 | `dcef60195bc7a96220dad412dee19d8a6c6ed337c1d13335ecd2ef30449322fd` |
| `artifacts/performance/p0d-20261125-final/migrator.log` | 18,224 | `3085a6a091a1dab7d99a016bebecc0c9f7f25ba1166b21b3ca9ea1d5bf7ad42f` |
| `artifacts/performance/p0d-20261125-concurrent/migrator-1-retry.err.log` | 1,961 | `17dea857103fc570a4b7e4ee55019b4129a8ad887f1ffeb8ff73c28c987d9570` |
| `artifacts/performance/p0d-20261125-concurrent/migrator-2-retry.out.log` | 18,167 | `0352d582c34e828c66d9cfc3b7855f1ea5589d41c2e78c961745efc47a6edeaf` |

The same run directories also contain the corresponding API stdout/stderr, Docker CSV, PostgreSQL CSV and process-resource CSV files. A case-insensitive scan of the retained P0-D and benchmark trees found no password assignment, bearer authorization, cookie or secret assignment pattern. Raw logs remain local and ignored because they contain operational identifiers and large sampler output.

## Verified benchmark profiles

| Run | Mode | Requests/endpoint | Warm-up | Concurrency | Multi-instance | Sustained writes | `PreMigrationSql` |
|---|---|---:|---:|---|---|---|---|
| `20261125090812_10848_4c9d0e78` | diagnostic | 5 | 2 | 1, 4 | no | no | `null` |
| `20261125091019_41032_6b11ec83` | load | 100 | 5 | 1, 4, 8 | yes, 2 API instances | yes | `null` |
| `20261125091708_30864_85500d9a` | load repeat | 100 | 5 | 1, 4, 8 | yes, 2 API instances | yes | `null` |

## Reproduction commands

The canonical path and regression evidence were run with the following command shapes. Secrets were supplied through process environment variables and are intentionally not recorded here:

```powershell
dotnet build backend\DbMigrator\DbMigrator.csproj --configuration Release --no-restore -p:MSBuildEnableWorkloadResolver=false -m:1
dotnet build backend\Tests\SeedData\SeedDataTests.csproj --configuration Release --no-restore -p:MSBuildEnableWorkloadResolver=false -m:1
dotnet test backend\Tests\SeedData\SeedDataTests.csproj --no-build --configuration Release --logger "console;verbosity=minimal"
dotnet test backend\Tests\Security\SecurityTests.csproj --no-restore --configuration Release --logger "console;verbosity=minimal"
pwsh -NoProfile -File backend\Tests\Performance\platform-performance-harness.tests.ps1
```

The direct fresh-database run used a new `template0` database in the disposable PostgreSQL 18.6 container `todir-p0d-postgres-20261125` on `127.0.0.1:25433`, then executed:

```powershell
dotnet backend\DbMigrator\bin\Release\net10.0\DbMigrator.dll --seed development
```

with `DOTNET_ENVIRONMENT=Development` and `ASPNETCORE_ENVIRONMENT=Development`, and without `-PreMigrationSql`. It exited `0`, printed `Development Seed Ready`, and reported `Applied or verified 264 ordered platform scripts from 26 module manifests.` PostgreSQL verification returned `265` applied scripts, `265` distinct `(module_id, script_name)` identities and one `pg_trgm` extension.

## Duplicate-history incident and correction

The prior intermittent failure was reproduced by launching two current DbMigrator processes concurrently against one newly created database. Before the correction, one process failed with `SQLSTATE 23505` on the global `platform_migration.applied_script` primary key; a separate concurrent run also exposed the same uncoordinated startup class through the `pg_extension_name_index` on `pg_trgm`. The root cause is the database-wide ordered operation having a check-then-apply/insert path with no cross-process lease.

The scoped correction is in `backend/DbMigrator/Program.cs`: a session-scoped PostgreSQL advisory lock keyed by `todir.dbmigrator:` plus `current_database()` is acquired before runtime bootstrap and migration work. The lock command has `CommandTimeout = 0`, so a second deployment runner waits for the first migration rather than failing at Npgsql's default 30-second command timeout. The lock is released with the connection on normal completion or process failure.

The regression `ConcurrentMigratorsSerializeAndPreserveMigrationHistory` launches two migrators against a fresh database and asserts both exit `0`, Development Seed readiness, `265` history rows and `265` distinct identities. It passed. `SeedDataTests` passed `6/6`; `SecurityTests` passed `7/7`.

## Evidence boundaries

- The first failed concurrent attempt and the pre-correction duplicate-key output are retained locally as investigation artifacts; they are not presented as successful evidence.
- The correction coordinates migration processes only. It does not alter Quality implementation, migration ordering, RLS policies, authorization, business logic or benchmark request paths.
- Development Seed is a small synthetic dataset: 2 tenants, 4 legal entities, 4 branches, 300 parties, 142 open items, 446 product variants and 8 projects. It cannot certify customer-scale capacity.
- Worker/RabbitMQ/Redis mixed-load, browser workload, production topology, autoscaling, multi-region behavior and stress-mode production limits remain outside this P0 evidence.
