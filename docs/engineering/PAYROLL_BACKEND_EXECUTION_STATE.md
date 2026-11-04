# Payroll Backend Execution State

## Mission

Complete the Module 18 v1.1 Backend + Database Build Now scope defined by the canonical Module 18 requirements pack.

## Current repository state

- Branch: dev
- Local HEAD: current mission commit on `dev` (verify with `git log -1`)
- Last verified origin/dev: 9c53c7a
- frontend/test-select-click.png is preserved and intentionally untracked.
- SSH publishing is blocked by GitHub Permission denied (publickey); local implementation and verification continue independently.

## Execution order

1. Calculation integrity and effective-dated configuration
2. Attendance, leave, proration, exceptions and variance
3. Payslip finalization and historical evidence
4. Ledger semantic fact posting and reconciliation
5. Payments disbursement, confirmation and reconciliation
6. Advances, loans, schedules, recovery, reschedule, settlement and write-off
7. Retro, off-cycle and final settlement
8. Reports, exports, imports and opening balances
9. Authorization, audit, outbox/worker recovery, RLS and concurrency
10. Real PostgreSQL integration/security/financial acceptance tests
11. Full local quality gates, commit, SSH push and final evidence

## Capability checklist

| ID | Capability | Status | Evidence / next action |
|---|---|---|---|
| PY00 | Baseline verification | Partial | Build, seed and runtime checks exist; complete acceptance matrix. |
| PY01 | Schema, ownership and transaction RLS | Partial | Payroll repository now uses transaction-local RLS; add cross-tenant integration proof. |
| PY02-PY05 | Groups, periods, assignments, components, structures | Partial | Effective assignment selection exists; add lifecycle API/import coverage. |
| PY06 | Deterministic formula engine | Partial | Unit coverage exists; add golden vectors and persisted trace assertions. |
| PY07 | Country provider contract | Partial | Optional provider path exists; add automatic/manual independent mode tests. |
| PY08-PY10 | HR snapshot and run shell | Partial | Snapshot and run APIs exist; add concurrency and immutable snapshot integration tests. |
| PY11-PY15 | Gross-to-net, proration, attendance/leave, statutory, exceptions | Partial | Base proration and statutory safeguards exist; complete monetary attendance/leave and variance workflows. |
| PY16-PY18 | Payslip preview, finalization, trace/history | Partial | Finalization persists payslips/evidence; add reconciliation and immutability tests. |
| PY19 | Ledger integration | Verified locally | Payroll semantic Ledger capability posts balanced expense/payable lines; real `PayrollFinalizedV1` proof moved the local accounting fact to Posted and recorded journal entry `JE-01a1a579357277c38adc086faa83122e`. |
| PY20 | Payments integration | Partial / handoff verified | Added Payments-owned payroll capability and Worker consumer. Real `PayrollPaymentRequestedV1` proof created 80 idempotent Payments drafts, moved 80 Payroll instructions to Requested, and moved the run to Requested. Provider confirmation/completion and reconciliation remain required before Paid. |
| PY21 | Pay-date variations | Partial | Period/pay-date fields exist; verify holiday and group-specific behavior. |
| PY22-PY25 | Advances, loans, recovery and limits | Partial | Approved receivable disbursement now transitions to Disbursed and creates idempotent recovery installments; recovery application, pause/reschedule, repayment, settlement and write-off remain required. |
| PY26-PY28 | Retro, off-cycle, final settlement | Open | Run types exist; implement complete financial workflows and duplicate prevention. |
| PY29-PY30 | Approval and self-service | Partial | Existing authorization surface; verify SoD and employee restrictions. |
| PY31-PY33 | Reports, compliance outputs, imports/opening balances | Open | Implement documented paginated reports, import preview/commit and opening balances. |
| PY34-PY36 | Scale, security, runbooks/recovery | Partial | Bounded queries and RLS path exist; add concurrency, interruption recovery and red-team tests. |
| PY37-PY42 | Acceptance and release gates | Open | Requires real financial integration evidence and full regression gates. |

## Acceptance criteria

- Exact expected payroll monetary values reconcile across run, employee rows, payslips and lines.
- Historical input, component version, statutory mode/version and rule evidence is immutable.
- Tenant and legal-entity isolation is enforced by PostgreSQL RLS and authorization.
- Ledger produces a balanced, idempotent posted journal or an explicit failure state.
- Payments produces actual confirmation/reconciliation states; Requested is never reported as Paid.
- Loan/advance balance cannot be over-recovered or recovered twice under concurrency.
- Retro/off-cycle/final settlement do not rewrite old payslips or duplicate regular salary.
- Seed reruns are idempotent and never overwrite user-managed or posted data.

## Current technical blockers

1. GitHub SSH authorization for the origin repository is unavailable locally. Required external action: authorize the public key corresponding to C:\Users\Adham\.ssh\id_ed25519.pub. Do not share the private key.
2. Payments provider confirmation/reconciliation is not yet implemented for payroll salary drafts; the verified state is Requested, not Paid.

## Exact next executable action

Implement Payments provider confirmation/reconciliation for payroll salary drafts, then close loan/retro/off-cycle/final-settlement/report/import gaps and run the complete acceptance matrix.

## Evidence log

- dotnet build backend/Backend.sln --no-restore /m:1 /v:q: passed, 0 warnings/errors after the latest local changes.
- dotnet test backend/Tests/Modules/Payroll/PayrollTests.csproj /v:q: 4 passed.
- dotnet test backend/Tests/Modules/Payments/PaymentsTests.csproj /v:q: 23 passed.
- Payroll whitespace format verification: passed.
- Full analyzer verification: passed with existing informational diagnostics only.
- DbMigrator development seed: passed; 128 ordered scripts applied or verified.
- Runtime API health and Payroll runs list: HTTP 200 on the real local stack.
- DbMigrator was rebuilt and rerun after Ledger seed edits; each seeded legal entity now has PayrollExpense/PayrollPayable roles, mappings, and `PAYROLL_POSTED_V1` rule legs.
- Real Worker Ledger proof: finalized Payroll fact status `Ready` → `Posted`, balanced journal created, and Payroll run `accounting_state='Posted'`.
- Real Worker Payments handoff proof: 80/80 `payment_instruction` rows `Prepared` → `Requested`, 80 Payments `ExpensePayment` drafts created idempotently, and Payroll run `payment_state='Requested'`.
- Payroll payment event replay proof: second delivery left the count at 80 Payments drafts and reached inbox `attempts=2`, `processed_at` set; no duplicate drafts were created.
- Final local solution build after the latest Payments audit/outbox change: passed, 0 warnings/errors.
- Payroll disbursement path now locks Approved receivables, marks them Disbursed, and generates bounded recovery installments in the same transaction; Payroll module build and 4 tests passed after this change.
- Fresh final verification after the receivable change: `dotnet build backend/Backend.sln --no-restore /m:1 /v:q` passed with 0 warnings/errors; Payroll 4/4 and Payments 23/23 tests passed; `git diff --check` passed.
