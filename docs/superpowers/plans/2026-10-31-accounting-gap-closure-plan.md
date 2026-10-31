# Accounting Backend Gap Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close the canonical backend/database Accounting gaps and produce post-change financial certification evidence.

**Architecture:** Keep Ledger/Subledger/Inventory/FinanceBase authoritative. Add only the smallest contracts needed for Accounting orchestration, idempotency, diagnostics, and reporting. Use PostgreSQL transactions, unique identities, RLS, and outbox guarantees already present in the modular monolith.

**Tech Stack:** ASP.NET Core/.NET 10, PostgreSQL/Npgsql, xUnit, existing module contracts, OpenAPI, PowerShell repository tooling.

**Spec:** `docs/superpowers/specs/2026-10-31-accounting-gap-closure-design.md`

## Global Constraints

- Work only on `dev`; never touch `main`.
- Do not implement frontend or Banking capabilities.
- Do not duplicate Ledger, Subledger, Inventory, FinanceBase, Tax, Invoicing, or Payments truth.
- Preserve tenant and legal-entity authorization, RLS/`FORCE ROW LEVEL SECURITY`, immutable posted history, and outbox semantics.
- Production code is written only after a failing focused test exists.

## Review Focus

- Same-payload/different-payload retries must return/conflict deterministically: go-live, close, FX.
- A post/close race must not leave stale close evidence or a posting after the canonical close lock.
- A read-only health request must not create reconciliation, audit, or outbox rows.
- Empty source context with nonzero GL control balance must be a difference, not a false zero.
- A multi-line FX failure must be resumable without duplicate journal entries or untracked partial state.

### Task 1: Go-live idempotency and read-only health

**Files:**
- Modify: `backend/Modules/Accounting/DTOs/AccountingContracts.cs`
- Modify: `backend/Modules/Accounting/Controllers/AccountingModuleExtensions.cs`
- Modify: `backend/Modules/Accounting/Services/AccountingService.cs`
- Modify: `backend/Modules/Accounting/Repositories/AccountingRepository.cs`
- Modify: `backend/DbMigrator/Database/bootstrap/accounting-schema.sql`
- Modify: `backend/Tests/Modules/Accounting/AccountingPostgresIntegrationTests.cs`

**Steps:** write failing tests for duplicate go-live approval/outbox and health read mutation; run them; implement durable request hash/dedupe and pure read paths; rerun focused tests.

### Task 2: Reconciliation workbench completeness

**Files:**
- Modify: `backend/Modules/Accounting/Controllers/AccountingModuleExtensions.cs`
- Modify: `backend/Modules/Accounting/Services/AccountingService.cs`
- Modify: `backend/Modules/Accounting/Repositories/AccountingRepository.cs`
- Modify: `backend/Tests/Modules/Accounting/AccountingPostgresIntegrationTests.cs`

**Steps:** write failing tests for persisted run history/issues and zero-context/nonzero-control differences; implement read APIs and immutable issue evidence through owner contracts; run focused PostgreSQL tests.

### Task 3: Atomic close/reopen evidence

**Files:**
- Modify: `backend/Shared/Contracts/AccountingWorkspaceContracts.cs`
- Modify: `backend/Modules/Ledger/Services/LedgerService.cs`
- Modify: `backend/Modules/Ledger/Repositories/LedgerRepository.cs`
- Modify: `backend/Modules/Accounting/Services/AccountingService.cs`
- Modify: `backend/Modules/Accounting/Repositories/AccountingRepository.cs`
- Modify: `backend/Tests/Modules/Ledger/*Close*`
- Modify: `backend/Tests/Modules/Accounting/AccountingPostgresIntegrationTests.cs`

**Steps:** write failing close monotonicity and close-vs-post tests; implement a Ledger-owned locked transition with critical recheck and monotonic evidence status; run Ledger and Accounting focused integration tests.

### Task 4: Statement correctness and localization contract

**Files:**
- Modify: `backend/Modules/Accounting/DTOs/AccountingContracts.cs`
- Modify: `backend/Modules/Accounting/Services/AccountingService.cs`
- Modify: `backend/Modules/Accounting/Repositories/AccountingRepository.cs`
- Modify: `backend/Tests/Modules/Accounting/AccountingPostgresIntegrationTests.cs`

**Steps:** write failing golden-vector tests for overlapping mappings, balanced-ledger/incorrect-report mappings, effective versions, and Arabic/English labels; implement validation and response labels; rerun statement tests.

### Task 5: FX failure recovery and bounded resolution

**Files:**
- Modify: `backend/Modules/Accounting/Services/AccountingService.cs`
- Modify: `backend/Modules/Accounting/Repositories/AccountingRepository.cs`
- Modify: `backend/DbMigrator/Database/bootstrap/accounting-forward-fix-fx-revaluation.sql`
- Modify: `backend/Tests/Integration/AccountingFxRevaluationIntegrationTests.cs`

**Steps:** write a failing line-1-success/line-2-failure recovery test and repeated reversal/concurrency tests; implement explicit recoverable state transitions and bounded FinanceBase resolution; rerun FX integration tests.

### Task 6: Mapping simulator, opening orchestration, imports/exports, and API contracts

**Files:**
- Inspect and modify only the owning Ledger/Accounting contract files required by the canonical API.
- Modify: `backend/Tests/Integration/ApiCompositionTests.cs`
- Modify: `docs/api/ledger-openapi.json` or add the repository-approved Accounting API document if required.

**Steps:** write failing contract tests for simulator/preview, opening read/validation surfaces, financial exports, and Accounting paths; implement only missing owned capabilities or delegated façades; verify authorization, pagination, decimal serialization, and stable errors.

### Task 7: Final certification

**Files:** no production changes unless a focused certification regression exposes a defect.

**Steps:** run pinned SDK/build and formatting gates, fresh disposable migrations, repeated seed, runtime-role RLS/entity isolation, focused dependency regressions, golden vectors, concurrency/failure recovery, performance plans, projection rebuild, DR validation, OpenAPI checks, `git diff --check`; self-review the full AC matrix; commit once and push `origin/dev` only if all required gates pass.

