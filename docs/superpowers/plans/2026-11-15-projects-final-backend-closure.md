# Projects Final Backend Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining Module 23 backend capability and acceptance gaps against the canonical Projects pack without changing module ownership or touching frontend work.

**Architecture:** Extend the existing Projects modular-monolith repository/service/API seams over the already-owned `business_projects` tables. Add only forward migrations for genuinely missing persistence, keep source facts authoritative in Expenses, Purchasing, Inventory, Invoicing, Subledger/Payments, and Fixed Assets, and prove each new vertical slice with isolated PostgreSQL tests.

**Tech Stack:** .NET 10, ASP.NET Minimal APIs, PostgreSQL/Npgsql, MassTransit, xUnit, RabbitMQ, existing RLS/Outbox/Inbox patterns.

**Spec:** `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

## Global Constraints

- Work directly on `dev`; preserve the existing verified commit and do not touch `main`.
- PostgreSQL is the system of record; Projects never owns source-module facts or payment/invoice/fixed-asset tables.
- Tenant context is mandatory; all new tables use RLS and `FORCE ROW LEVEL SECURITY`.
- Posted source facts, original budgets, approved baselines, and completed stock/invoice outcomes remain immutable; corrections are explicit projections.
- PJ38 heavyweight certification and frontend/browser work remain deferred.
- Final evidence must be captured after the last source change; no completion claim from compilation alone.

## Review Focus

- A schema-only capability must expose a bounded authorized workflow; each task adds an API and persisted acceptance test.
- A source-derived amount or currency must be present and traceable; missing values become deterministic evidence, never zero defaults.
- Cross-tenant and cross-legal-entity reads must return no data or deny access; every new query sets tenant context.
- A correction, rejection, retry, or reopening must preserve the original row and create an auditable state transition.
- Large operational views must be server-side bounded and stable; tests assert page size and ordering rather than loading all rows.

### Task 1: Capability Inventory and Coverage Matrix

**Files:**
- Modify: `docs/engineering/PROJECTS_BACKEND_EXECUTION_STATE.md`
- Create: `docs/engineering/PROJECTS_BACKEND_COVERAGE_MATRIX.md`

- [ ] Map PJ03, PJ07, PJ15, PJ21-PJ25, PJ29-PJ30, PJ33-PJ35, PJ39 and PJ40-PJ47 to current DB/API/seed/test evidence.
- [ ] Mark each item `implemented`, `partial`, `missing`, or `deferred`, with the exact implementation/test gap.
- [ ] Use the matrix to drive only the missing or partial slices below.

### Task 2: Project Operations Foundation

**Files:**
- Modify: `backend/Modules/Projects/DTOs/ProjectContracts.cs`
- Modify: `backend/Modules/Projects/Repositories/ProjectsRepository.cs`
- Modify: `backend/Modules/Projects/Services/ProjectService.cs`
- Modify: `backend/Modules/Projects/ProjectsModule.cs`
- Test: `backend/Tests/Modules/Projects/ProjectAcceptanceScenarioTests.cs`

- [ ] Add real workflows for active template/version creation, project instantiation from a template, resource requirements/allocations, deliverables, documents, and bounded project timeline reads over existing owned tables.
- [ ] Enforce project/legal-entity scope, generic-resource-versus-employee rules, lifecycle mutability, and RLS in the repository transaction.
- [ ] Add failing PostgreSQL tests first, then implement the minimal repository/service/API paths and verify persisted state.

### Task 3: Billing Plan, Change Baseline, Close Exceptions, and Operational Views

**Files:**
- Create: `backend/DbMigrator/Database/bootstrap/projects-forward-fix-operational-views.sql`
- Modify: `backend/Modules/Projects/DTOs/ProjectContracts.cs`
- Modify: `backend/Modules/Projects/Repositories/ProjectsRepository.cs`
- Modify: `backend/Modules/Projects/Services/ProjectService.cs`
- Modify: `backend/Modules/Projects/ProjectsModule.cs`
- Test: `backend/Tests/Modules/Projects/ProjectAcceptanceScenarioTests.cs`

- [ ] Add the missing owned billing-plan projection and bounded portfolio/resource/exception read models only where current schema cannot express the documented workflow.
- [ ] Expose billing-plan lines, immutable baselines/rebaseline, close-exception resolution, and portfolio/resource summaries with stable pagination.
- [ ] Ensure approved change orders affect current plan/billing basis without rewriting original scope, budget, or actual history.

### Task 4: Authentic Cross-Module Acceptance

**Files:**
- Modify: `backend/Tests/Modules/Projects/ProjectAcceptanceScenarioTests.cs`
- Modify: existing integration test project only if a real owning-module capability is required.

- [ ] Replace the PJ43 stub path with the existing Fixed Assets capability/consumer and verify accepted, rejected, duplicate, and retry recovery states.
- [ ] Replace the PJ44 stub path with the existing Invoicing source capability and verify real draft/finalize/partial/correction outcomes.
- [ ] Add deterministic PJ45/PJ46 reconciliation fixtures for Expenses, Purchasing, Inventory, Invoicing, Subledger/Payments, Fixed Assets, change orders, close/reopen, and cross-legal-entity security.
- [ ] Keep source ownership in the source module and assert zero unexplained differences from authoritative facts.

### Task 5: Regression and CI Closure

**Files:**
- Modify: `docs/engineering/PROJECTS_BACKEND_EXECUTION_STATE.md`
- Modify only if root cause is proven: affected Subledger or workflow files.

- [ ] Inspect the exact GitHub Actions run/log or document the access boundary if the configured remote remains unavailable.
- [ ] Reproduce and classify both Subledger latency failures without running PJ38 heavy certification; fix only a proven Projects regression.
- [ ] Run the final affected tests, isolated migration/seed/RLS checks, build, architecture gate, diff check, and configured repository validation set after the last change.
- [ ] Commit and push one coherent verified change set; report `COMPLETE` only if all applicable gates pass.
