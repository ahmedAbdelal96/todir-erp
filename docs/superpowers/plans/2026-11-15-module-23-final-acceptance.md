# Module 23 Final Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close the remaining real backend acceptance gaps for Module 23 and report only the evidence-supported delivery status.

**Architecture:** Extend existing owning-module services and public contracts only where the canonical workflow is missing. Projects remains a projection/orchestration consumer; Purchasing, Invoicing, Payments/Subledger, Inventory, Fixed Assets, Expenses, and HR/Labor remain owners of their facts. Acceptance uses the existing isolated PostgreSQL database, real service composition, Outbox/Inbox evidence, and focused RabbitMQ verification where required.

**Tech Stack:** .NET 10, PostgreSQL, Npgsql, MassTransit, RabbitMQ, xUnit, existing modular-monolith contracts and repositories.

**Spec:** `docs/modules/23_ERP_SAAS_PROJECTS_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`

## Global Constraints

- Work directly on `dev`; preserve commit `55c6ec2` and existing data.
- Do not duplicate another module's tables, business truth, or accounting behavior.
- Tenant context, legal-entity scope, RLS, idempotency, immutable posted facts, and Outbox/Inbox guarantees remain mandatory.
- PJ38 heavyweight load certification is explicitly deferred; bounded queries and indexes remain in scope.
- Final claims require evidence captured after the last relevant source change.

## Review Focus

- Partial supplier invoice plus correction must reduce the exact Purchasing commitment once and preserve source history; prove in PJ15 acceptance.
- Fixed Assets accept/reject and duplicate delivery must update the original Project handoff exactly once; prove in PJ43 acceptance.
- Invoice settlement must use Subledger/Payments-owned open items and payment applications, not Project projections; prove in PJ44/PJ45 acceptance.
- Cross-module reconciliation must compare source-owned records to Project projections, including currency and reversal boundaries; prove in PJ45 acceptance.
- Real broker delivery/recovery must be distinguished from in-memory harness behavior; prove with the existing RabbitMQ path or record a precise infrastructure blocker.

### Task 1: Complete Purchasing-to-Projects supplier lifecycle

**Files:**
- Modify existing Purchasing/Projects integration contracts and services only if the current public event lacks partial-invoice or correction facts.
- Test: `backend/Tests/Integration/ProjectsCrossModuleAcceptanceTests.cs`

- [ ] Add a failing real-stack scenario for project-linked PO approval, commitment, partial supplier invoice, correction/reversal, duplicate delivery, and remaining open commitment.
- [ ] Implement only the missing source event/capability contract in the owning module.
- [ ] Run the focused PJ15 test and verify source-owned Purchasing/Invoicing rows plus Project facts.

### Task 2: Complete Fixed Assets/CIP result acceptance

**Files:**
- Modify `backend/Tests/Integration/ProjectsCrossModuleAcceptanceTests.cs` and only the existing Fixed Assets/Projects contract path if result delivery is incomplete.

- [ ] Add failing accept and reject result cases with duplicate/retry delivery.
- [ ] Verify original handoff state, candidate state, audit/evidence rows, and no duplicate financial effect.
- [ ] Run focused PJ43 tests.

### Task 3: Complete Invoicing, Subledger, and Payments settlement

**Files:**
- Modify existing `backend/Modules/Projects`, `backend/Worker`, or shared contracts only where the approved settlement seam is incomplete.
- Test: `backend/Tests/Integration/ProjectsCrossModuleAcceptanceTests.cs`.

- [ ] Build a real project invoice through Invoicing finalization and obtain the authoritative Subledger open item.
- [ ] Apply partial and full settlement through existing Payments/Subledger service contracts, then exercise reversal/correction and idempotent retry.
- [ ] Assert source-owned open-item/application/payment rows and Project collected/outstanding composition.
- [ ] Run focused PJ44/PJ45 tests.

### Task 4: Deterministic PJ45/PJ46 reconciliation journeys

**Files:**
- Test: `backend/Tests/Integration/ProjectsCrossModuleAcceptanceTests.cs`
- Modify only the owning-module seam required by a failing scenario.
- Update `docs/engineering/PROJECTS_BACKEND_COVERAGE_MATRIX.md` and `docs/engineering/PROJECTS_BACKEND_EXECUTION_STATE.md` with exact scenario names and persisted outcomes.

- [ ] Add real fixed-price, T&M, internal, capital, and change-order journey assertions using existing services/contracts.
- [ ] Reconcile Expenses, Purchasing, Invoicing AP/AR, Inventory, labor, Payments/Subledger, and Fixed Assets source totals against Project projections.
- [ ] Run each journey independently and then the bounded grouped acceptance set.

### Task 5: Release gates and CI classification

**Files:**
- Modify only harmless baseline whitespace files if the exact formatter violations are unrelated and safe to correct.
- Update the two engineering evidence documents.

- [ ] Run focused module/security/integration gates, Release build, OpenAPI validation, formatter, and diff checks after the last source change.
- [ ] Investigate the supplied GitHub Actions run and local Subledger latency failures without changing unrelated behavior.
- [ ] Commit once, push `origin/dev`, verify local/remote SHA, and report `COMPLETE`, `BLOCKED`, `INTERRUPTED`, or `NOT COMPLETE` truthfully.
