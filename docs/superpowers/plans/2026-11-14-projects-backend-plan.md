# Projects Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete backend/database scope of canonical Module 23 Projects for frontend handoff.

**Architecture:** One `Modules.Projects` implementation project follows Controller → Service → Repository → PostgreSQL. The module owns project execution, time, budget, forecast, project cost facts, billing eligibility/charges, change, close, and typed capital handoff; upstream and downstream modules remain owners of their facts.

**Tech Stack:** ASP.NET Core minimal APIs, .NET, Npgsql, PostgreSQL bootstrap migrations/RLS, existing Core transaction/audit/outbox/numbering services, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-14-projects-backend-design.md` and `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

## Global Constraints

- Work directly on `dev` and do not touch `main`.
- Use one `business_projects` schema and one Projects implementation project.
- Do not directly query or write another business module's private tables.
- Actual costs are immutable source-derived facts; corrections are additive.
- All tenant-owned tables use RLS and `FORCE ROW LEVEL SECURITY`.
- All list/report reads are filtered, explicitly sorted, and bounded.
- Do not run PJ38 heavyweight certification.

## Review Focus

- Lifecycle transitions reject invalid transitions and preserve history; covered by domain and integration tests in the Projects test project.
- Duplicate/retried time, cost, charge, invoice-request, and capital-handoff commands are idempotent; covered by repository integration vectors.
- Budget/commitment/actual/ETC/EAC and billing totals remain separate and rebuildable; covered by financial-summary vectors.
- Tenant/entity/project membership and restricted cost-rate fields cannot be bypassed; covered by RLS/authorization tests.
- Capital handoff never writes Fixed Assets tables and preserves rejection/retry state; covered by contract tests.

### Task 1: Module boundary and database foundation

**Files:** Create `backend/Modules/Projects/Projects.csproj`, `backend/Modules/Projects/module.manifest.json`, `backend/Modules/Projects/ProjectsModule.cs`; Create `backend/DbMigrator/Database/bootstrap/projects-schema.sql`; Modify API/Worker/DbMigrator composition and `Backend.sln`.

- [ ] Add the module project, manifest, module provider, and schema baseline.
- [ ] Add all normalized project, work, resource, time, rate, budget, forecast, cost, commitment, billing, change, close, capital-handoff, audit, and idempotency tables with constraints/indexes/RLS/grants.
- [ ] Add project references and registration in API, Worker, DbMigrator, and solution.
- [ ] Verify architecture and migration discovery compile.

### Task 2: Domain contracts, repository, and service workflows

**Files:** Create `backend/Modules/Projects/Entities/ProjectDomain.cs`, `DTOs/ProjectContracts.cs`, `Repositories/ProjectRepository.cs`, `Services/ProjectService.cs`.

- [ ] Add lifecycle/value validation and request/response contracts.
- [ ] Implement create/list/detail, tasks/milestones/team/allocation, time approval/correction, budget/forecast, source cost/commitment, billing charges/invoice request, change orders, close/reopen, financial summary, and capital handoff operations with transaction/audit/idempotency semantics.
- [ ] Ensure restricted cost rates are never returned by normal project-manager reads.
- [ ] Run focused unit tests and compile.

### Task 3: API and public contracts

**Files:** Modify `ProjectsModule.cs`; Create/modify `backend/Shared/Contracts/ProjectsContracts.cs`.

- [ ] Expose bounded REST endpoints under `/api/v1/projects` with central authorization and legal-entity/project scope checks.
- [ ] Register permissions, capabilities, messaging, and typed Fixed Assets handoff surface.
- [ ] Verify OpenAPI/build and direct-ID authorization behavior.

### Task 4: Development seed and focused tests

**Files:** Create `backend/DbMigrator/SeedData/ProjectsSeed.cs`; Modify `SeedRunner.cs`; Create `backend/Tests/Modules/Projects/ProjectsTests.csproj`, domain/database/integration tests.

- [ ] Seed deterministic Atlas and Delta project scenarios without fabricating upstream source facts.
- [ ] Add tests for canonical PJ40–PJ46 backend scenarios, including RLS, idempotency, corrections, and reconciliation.
- [ ] Run migration, seed, focused tests, and inspect persisted results.

### Task 5: Final gates and checkpoint

**Files:** Create/update `docs/engineering/PROJECTS_BACKEND_EXECUTION_STATE.md`.

- [ ] Run the final build, architecture, migration-policy, focused backend tests, security/RLS tests, OpenAPI checks, and `git diff --check` after the last change.
- [ ] Record exact PASS/FAIL evidence, deferred scope, unresolved upstream contract gaps, changed files, commit SHA, and remote status.
