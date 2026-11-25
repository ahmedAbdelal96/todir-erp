# P0-09 — Evidence Verification Manifest

**Repository:** `D:\Web\full-projects\todir-erp`  
**Branch:** `dev`  
**Source baseline under verification:** `ab49df8b95a16c92804cce7405c42a9f95b0beb7`  
**P0-D verification source change:** bounded DbMigrator database-scoped advisory lease plus lifecycle/recovery regression tests; final delivery SHA is recorded in the commit and final review.  
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
| Disposable benchmark execution | PASS | Diagnostic and final 100-request controlled load completed without compatibility workaround |
| Operational performance baseline | PASS WITH LIMITS | p50/p95/p99, RPS, errors, DB/resource samples and four query plans recorded |
| Runtime security/isolation tests | PASS | Harness 401/403 matrix, SecurityTests 7/7 and final SeedDataTests result |
| Concurrent DbMigrator regression | PASS | Two migrators on a fresh database both exited 0; dynamic bootstrap identity/checksum inventory |
| Fresh post-correction startup | PASS | New disposable database: `/health` 200 and `/health/ready` 200 |
| Raw artifact reconciliation | PASS | Versioned JSON/CSV/resource archive, per-operation samples, archive manifest and SHA-256 checks in P0-B08/P0-B09 |
| Browser/UX runtime proof | NOT APPLICABLE TO P0 | No Analysis UI was implemented |

## Integrity rule

This manifest records evidence after the last source change in the P0-D mission. The only production source change was the bounded DbMigrator advisory lease; test changes cover lease timeout/cancellation/recovery and dynamic migration inventory, and benchmark tooling now retains per-request samples. No Quality implementation, unrelated migration, RLS policy, authorization, seed business behavior or frontend runtime code was changed.

## Exit

`P0_EVIDENCE_COMPLETE` — canonical migration/seed, concurrent migration safety, API targeting, functional behavior, runtime security and controlled local performance evidence passed. Production capacity certification remains outside this evidence set.

