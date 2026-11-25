# P0-09 — Evidence Verification Manifest

**Repository:** `D:\Web\full-projects\todir-erp`  
**Branch:** `dev`  
**Source baseline under verification:** `c85f59121dc06e8c4f2d5a25590a534c82e836af`  
**P0-D verification source change:** DbMigrator database-scoped advisory lease plus concurrent regression test; final delivery SHA is recorded in the commit and final review.  
**Verification date:** 2026-11-25

## Completed checks

| Check | Result | Evidence |
|---|---|---|
| Root/scoped instructions loaded | PASS | root, principles, backend, frontend, ERP frontend profile/skill |
| Worktree and branch inspected | PASS | `dev`, clean before P0 evidence edits |
| Solution/module inventory | PASS | `dotnet sln backend/Backend.sln list`; module directory/manifest inventory |
| API/Worker composition | PASS | `backend/Api/Program.cs`, `backend/Worker/Program.cs`, project references |
| Data contracts/source ownership | PASS | Subledger, Payments, Banking, CRM, Accounting targeted source inspection |
| Tenant/RLS/access static review | PASS | authorization, entitlements, controllers, bootstrap SQL, frontend guards |
| Disposable benchmark harness review | PASS | `backend/Tests/Performance/platform-performance-benchmark.ps1` |
| Docker root-cause diagnosis | PASS | `P0-B01_DOCKER_ENVIRONMENT_ROOT_CAUSE.md` |
| Harness helper/mode verification | PASS | `platform-performance-harness.tests.ps1`; parser check |
| Disposable benchmark execution | PASS | Diagnostic and two controlled load runs completed without compatibility workaround |
| Operational performance baseline | PASS WITH LIMITS | p50/p95/p99, RPS, errors, DB/resource samples and four query plans recorded |
| Runtime security/isolation tests | PASS | Harness 401/403 matrix, SecurityTests 7/7 and SeedDataTests 6/6 |
| Concurrent DbMigrator regression | PASS | Two migrators on a fresh database both exited 0; 265 unique history identities |
| Fresh post-correction startup | PASS | New disposable database: `/health` 200 and `/health/ready` 200 |
| Raw artifact reconciliation | PASS | Per-operation audit, run profiles, resource observations and SHA-256 manifest in P0-B08/P0-B09 |
| Browser/UX runtime proof | NOT APPLICABLE TO P0 | No Analysis UI was implemented |

## Integrity rule

This manifest records evidence after the last source change in the P0-D mission. The only production source change was the DbMigrator advisory lease; the only test source change was its concurrent regression coverage. No Quality implementation, unrelated migration, RLS policy, authorization, seed business behavior or frontend runtime code was changed.

## Exit

`P0_EVIDENCE_COMPLETE` — canonical migration/seed, concurrent migration safety, API targeting, functional behavior, runtime security and controlled local performance evidence passed. Production capacity certification remains outside this evidence set.

