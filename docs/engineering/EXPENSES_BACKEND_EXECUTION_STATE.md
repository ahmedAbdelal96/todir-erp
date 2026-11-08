# Module 21 Expenses Backend Execution State

Canonical pack: `docs/modules/21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

## Current checkpoint

- Authorized scope: complete backend/database/API implementation for EX00–EX43; frontend and external provider activation remain outside this backend mission.
- Completed and verified: EX00 dependency inspection; EX01 `business_expenses` schema, tenant RLS, runtime grants and migration registration; core draft/submit/approve/reject/finalize/reverse API; category/version setup APIs; evidence references plus short-lived Platform Files download contract; itemization and participant persistence; deterministic policy evaluation snapshot; report/advance/card-inbox/reimbursement-batch read/write seams; employee-paid finalization creates one idempotent reimbursement obligation; authoritative Ledger ExpenseRecognized posting with immutable fact/entry references; compensating reversal with recoverable `ReversalPending` state; deterministic Atlas/Delta seed; manifest coverage for all persisted workflow entities; legal-entity permission enforcement at the Expenses endpoint group using the canonical authorization subject/permission scope pattern; Payments capability for idempotent reimbursement batch completion with EmployeeReimbursementPayable settlement and allocation-scoped obligation settlement; advance application/return accounting with employee/currency/amount guards; server-side pagination, filtering and search for reports, advances, corporate-card inbox and employee repayments.
- Verified after the latest source/migration changes: full backend solution build 0 warnings/errors; Expenses/Ledger/ExpensesTests format verification and `git diff --check`; fresh PostgreSQL bootstrap applied 155 scripts; development seed and immediate rerun completed on disposable databases; regular Expenses PostgreSQL tests pass 2/2 plus typed mileage/personal-card acceptance 1/1; isolated financial acceptance passes 1/1 through create → evidence → submit → approve → finalize → Ledger balanced entry (125 EGP) → reverse → Ledger reversal; full Payments PostgreSQL integration passes 5/5 on a fresh seeded database, including reimbursement batch → Payments completion → Ledger PaymentMade (320 EGP balanced) → obligation settlement → idempotent retry; cross-tenant RLS denial remains proven.
- Unfinished: full Platform Files/OCR lifecycle, policy/approvals provider integration, corporate-card provider matching/allocation beyond persisted exact matching/personal repayment capture, employee repayment settlement, advance application/return accounting, mileage/per-diem policy rate/version contracts, FX/tax/dimension contracts, complete reports/imports, worker recovery, authenticated permission/concurrency/idempotency red-team suite, and EX36–EX43 final gates. These are in-scope engineering gaps, not external blockers.
- Last meaningful change: added atomic Ledger posting/reversal, financial evidence migration, persisted Build Now workflow tables/APIs, Payments-owned ExpensePayment capability with allocation-scoped reimbursement settlement, deterministic typed mileage/per-diem amount persistence, idempotent corporate-card personal repayment capture, bounded advance application/return accounting, and paginated list contracts with real PostgreSQL coverage.
- Next executable action: continue the remaining EX15–EX35 workflows (employee repayment settlement, advance application/return accounting, policy/rate/version contracts, imports/reports and worker recovery), then run authenticated security/concurrency and EX36–EX43 acceptance gates without weakening financial or RLS invariants.

## Phase matrix

| Phase | Status | Evidence |
|---|---|---|
| EX00 | PASS | Dependencies and ownership reviewed against existing HR, Ledger, Payments, Files and platform patterns. |
| EX01 | PASS | `expenses-schema.sql`, tenant-local RLS and migration order verified on disposable PostgreSQL. |
| EX02–EX05 | PARTIAL | Categories, effective versions, core claims and evidence references exist; Platform Files/OCR and missing-receipt lifecycle remain. |
| EX06–EX11 | PARTIAL | Policy snapshot, participants, finalization and Ledger evidence exist; full policy/approval integration and provider-controlled accounting edge cases remain. |
| EX12–EX35 | PARTIAL | Reimbursement Payments completion and reconciliation are verified 5/5; card inbox matching/personal repayment capture and reproducible mileage/per-diem detail persistence are implemented and tested. Employee repayment settlement, provider matching/allocation, advance accounting, policy/rate/tax/FX/dimension contracts, imports/reports and worker/security acceptance remain. |
| EX36–EX43 | NOT COMPLETE | Final acceptance is intentionally unavailable until the backend workflows and persisted financial reconciliation pass. |

This document is a resumable engineering checkpoint, not a completion claim.
