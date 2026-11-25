# P0-B09 — Artifact Verification and Reproduction

**Result:** `PASS — EVIDENCE RECONCILED`  
**Branch:** `dev`  
**Verification date:** 2026-11-25 (Africa/Cairo)  
**Raw-artifact retention:** the final diagnostic and 100-request load JSON/CSV/resource artifacts are durably archived under `benchmark-archive/`, with an archive manifest and independent verifier. The historical repeat JSON is retained as aggregate-only evidence and is explicitly labelled.

## Durable artifact set

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `benchmark-archive/diagnostic/platform-performance-todir_perf_20261125103326_38136_663ae499.json` | 94,658 | `9b42887a6cd8e8086ad79e84c16c9ea2490ebe0c2aea7dfb155390fd0266aab3` |
| `benchmark-archive/load-100/platform-performance-todir_perf_20261125103950_28700_b2a9b5cf.json` | 338,854 | `dd87b626c553619ea17d61221f92c2c09fed3940a2a9d379ef017111ae8ec4a0` |
| `benchmark-archive/repeat/platform-performance-todir_perf_20261125091708_30864_85500d9a.json` | 231,575 | `dcef60195bc7a96220dad412dee19d8a6c6ed337c1d13335ecd2ef30449322fd` |
| `benchmark-archive/archive-manifest.json` | 3,531 | `7ab37e68f8b316517eaa8ef0df42b7483e96707598cebe9b1931eb42d1b0ea38` |
| `artifacts/performance/p0d-20261125-final/migrator.log` | 18,224 | `3085a6a091a1dab7d99a016bebecc0c9f7f25ba1166b21b3ca9ea1d5bf7ad42f` |
| `artifacts/performance/p0d-20261125-concurrent/migrator-1-retry.err.log` | 1,961 | `17dea857103fc570a4b7e4ee55019b4129a8ad887f1ffeb8ff73c28c987d9570` |
| `artifacts/performance/p0d-20261125-concurrent/migrator-2-retry.out.log` | 18,167 | `0352d582c34e828c66d9cfc3b7855f1ea5589d41c2e78c961745efc47a6edeaf` |

The archive also contains the corresponding Docker CSV, PostgreSQL CSV and process-resource CSV files; API stdout/stderr logs were intentionally excluded because they are not needed to recompute the reported metrics. The archive manifest records every durable file's byte count and SHA-256. A case-insensitive scan found no password assignment, bearer authorization, cookie or known secret assignment pattern.

## Verified benchmark profiles

| Run | Mode | Requests/endpoint | Warm-up | Concurrency | Multi-instance | Sustained writes | `PreMigrationSql` |
|---|---|---:|---:|---|---|---|---|
| `20261125103326_38136_663ae499` | diagnostic | 5 | 2 | 1, 4 | no | no | `null` |
| `20261125103950_28700_b2a9b5cf` | load | 100 | 5 | 1, 4, 8 | yes, 2 API instances | yes | `null` |
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

The direct fresh-database run used a new `template0` database in the disposable PostgreSQL 18.6 container `todir-p0b-postgres-20261125-final` on `127.0.0.1:25433`, then executed:

```powershell
dotnet backend\DbMigrator\bin\Release\net10.0\DbMigrator.dll --seed development
```

with `DOTNET_ENVIRONMENT=Development` and `ASPNETCORE_ENVIRONMENT=Development`, and without `-PreMigrationSql`. It exited `0`, printed `Development Seed Ready`, and the test-side inventory verified every current bootstrap script identity and checksum in `platform_migration.applied_script`.

## Duplicate-history incident and correction

The prior intermittent failure was reproduced by launching two current DbMigrator processes concurrently against one newly created database. Before the correction, one process failed with `SQLSTATE 23505` on the global `platform_migration.applied_script` primary key; a separate concurrent run also exposed the same uncoordinated startup class through the `pg_extension_name_index` on `pg_trgm`. The root cause is the database-wide ordered operation having a check-then-apply/insert path with no cross-process lease.

The scoped correction is in `backend/DbMigrator/Program.cs`: a session-scoped PostgreSQL advisory lock keyed by `todir.dbmigrator:` plus `current_database()` is acquired before runtime bootstrap and migration work. The lock command uses a bounded `CommandTimeout` configured by `TODIR_MIGRATOR_LOCK_TIMEOUT_SECONDS` (default 300 seconds, allowed 1–3600). Timeout and Ctrl+C cancellation return exit code 5 before mutation; connection disposal releases the session-scoped lock.

The regression `ConcurrentMigratorsSerializeAndPreserveMigrationHistory` launches two migrators against a fresh database and asserts both exit `0`, Development Seed readiness and the dynamic canonical inventory. Additional regressions cover timeout without mutation, released-lease retry, terminated waiting-process recovery and invalid timeout rejection. Final suite totals are recorded in `P0_FINAL_ACCEPTANCE_AND_HANDOFF.md`.

## Evidence boundaries

- The first failed concurrent attempt and the pre-correction duplicate-key output are retained locally as investigation artifacts; they are not presented as successful evidence.
- The correction coordinates migration processes only. It does not alter Quality implementation, migration ordering, RLS policies, authorization, business logic or benchmark request paths.
- Development Seed is a small synthetic dataset: 2 tenants, 4 legal entities, 4 branches, 300 parties, 142 open items, 446 product variants and 8 projects. It cannot certify customer-scale capacity.
- Worker/RabbitMQ/Redis mixed-load, browser workload, production topology, autoscaling, multi-region behavior and stress-mode production limits remain outside this P0 evidence.
