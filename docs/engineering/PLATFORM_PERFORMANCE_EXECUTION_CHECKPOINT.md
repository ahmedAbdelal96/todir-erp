# Platform Performance Execution Checkpoint

Date: 2026-11-22

## Authorized scope

Local performance and scalability foundation on `dev`, without Redis, remote CI, or production release work. Preserve concurrent user changes and use disposable PostgreSQL databases for mutating checks.

## Completed and verified

- Read the repository governance, backend/frontend manuals, performance ADRs, observability, tenant-security, migration, and runtime-database decisions.
- Confirmed the main shared hypotheses from source: repeated authorization-subject loads and eager route page graphs.
- Added request-scoped authorization-subject reuse with a configuration switch and bounded metrics; the cache is tenant/membership keyed and not used by background work.
- Added route-level named dynamic imports for the heavy module page routes and removed unused public page re-exports that defeated code splitting.
- Frontend typecheck passed; production build passed. The main JS chunk moved from 2,824.93 kB / 609.59 kB gzip in the first split attempt to 1,710.40 kB / 440.92 kB gzip after removing ineffective static re-exports. The shared `grid` chunk remains 1,160.30 kB and is recorded for follow-up.
- The checked-in benchmark completed against a disposable `todir_perf_*` database. DbMigrator applied or verified 240 ordered scripts and the Development Seed completed for Atlas and Delta; the disposable database was dropped and no `todir_perf_*` database remained.
- Benchmark endpoint: Subledger party exposure, 40 measured requests at concurrency 1/4/8, real local API/PostgreSQL. Cache disabled: 15.90/47.11/57.86 RPS with p95 68.06/151.10/184.23 ms; cache enabled: 16.92/43.99/70.71 RPS with p95 75.72/148.26/188.56 ms. All 240 measured requests succeeded. Authorization counters changed from 270 database loads and 0 cache hits to 135 database loads and 135 cache hits (including warm-up accounting as recorded by the harness).
- Core tests passed 62/62; Projects module tests passed 22/22; Architecture tests passed 21/21; Security/RLS tests passed 7/7 on a disposable migrated/seeded database. Projects cross-module acceptance passed 9/9, including the real local RabbitMQ → Worker → Inbox source-event proof and duplicate-publish idempotency assertions.
- Backend Release build passed with 0 warnings and 0 errors. API drift passed. The targeted task-owned .NET formatter gate passed. `git diff --check` reports only trailing whitespace in the preserved concurrent Catalog test edits.

## Final gate notes

- `pnpm run typecheck`, `pnpm run api:drift`, frontend architecture boundary checks, production build, backend tests, isolated migration/seed, benchmark, real local Worker/RabbitMQ proof, Release build, targeted formatter, and the task-owned diff review passed.
- `pnpm run test:frontend` passed 284/285. The single failure is the existing Manufacturing CSS media-query false positive in `production-order-workspace-page.tsx`; that file is a preserved concurrent change and is outside this mission.
- `pnpm run localization:verify` reports the same existing Manufacturing CSS false positive. `pnpm run api:contract` validates the first five contracts and stops on the existing Inventory missing path `GET /api/v1/inventory/legal-entities/{entityId}/counts`; no performance-owned contract changed.
- `git diff --check` reports two existing trailing-whitespace lines in the preserved concurrent `CatalogBatchResolutionTests.cs`; task-owned files have no diff-check issue.
- The worktree is intentionally not globally clean because concurrent user changes in `backend/Api/Program.cs`, Catalog tests, and Manufacturing frontend files were preserved and not staged.

## Delivery action

Stage only task-owned performance, governance, documentation, backend, benchmark, and route-splitting files; commit directly on `dev`, push to `origin/dev` over SSH, verify the remote SHA, and retain the concurrent uncommitted files untouched. The coverage matrix is in `docs/engineering/PLATFORM_PERFORMANCE_COVERAGE_MATRIX.md`.
