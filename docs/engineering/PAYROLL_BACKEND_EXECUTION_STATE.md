# Payroll Backend Execution State

## Mission

Complete the Module 18 v1.1 Backend + Database Build Now scope defined by the canonical Module 18 requirements pack.

## Current repository state

- Branch: dev
- Local HEAD: latest verified delivery commit on `dev` (see `git log -1`)
- Last verified origin/dev: latest verified delivery commit on `dev` (see `git ls-remote origin refs/heads/dev`)
- frontend/test-select-click.png is preserved and intentionally untracked.
- SSH publishing is configured globally through `C:\Users\Adham\.gitconfig` and verified against `origin/dev`.

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
| PY00 | Baseline verification | Verified locally | Disposable PostgreSQL 18 test database was created on port 15436, bootstrapped and seeded; the existing development database was upgraded independently. |
| PY01 | Schema, ownership and transaction RLS | Partial | Payroll repository now uses transaction-local RLS; add cross-tenant integration proof. |
| PY02-PY05 | Groups, periods, assignments, components, structures | Partial | Effective assignment selection exists; add lifecycle API/import coverage. |
| PY06 | Deterministic formula engine | Partial | Unit coverage exists; add golden vectors and persisted trace assertions. |
| PY07 | Country provider contract | Partial | Optional provider path exists; add automatic/manual independent mode tests. |
| PY08-PY10 | HR snapshot and run shell | Partial | Snapshot and run APIs exist; add concurrency and immutable snapshot integration tests. |
| PY11-PY15 | Gross-to-net, proration, attendance/leave, statutory, exceptions | Partial | Base proration and statutory safeguards exist; complete monetary attendance/leave and variance workflows. |
| PY16-PY18 | Payslip preview, finalization, trace/history | Partial | Finalization persists payslips/evidence; add reconciliation and immutability tests. |
| PY19 | Ledger integration | Verified locally | Payroll semantic Ledger capability posts balanced expense/payable lines; real `PayrollFinalizedV1` proof moved the local accounting fact to Posted and recorded journal entry `JE-01a1a579357277c38adc086faa83122e`. |
| PY20 | Payments integration | Verified locally | Added Payments-owned payroll capability, Worker handoff, and confirmation/cancellation reconciliation consumer. Real handoff proof created 80 idempotent Payments drafts; a real Payroll ExpensePayment completed through Payments and `PaymentCompletedV1` moved its instruction to `Paid` and the run to `Partial`; a second draft cancellation moved its instruction to `Failed` and the run to `Failed`. Payroll settlement uses the explicit `PayrollPayable` Ledger role without requiring an HR party. |
| PY21 | Pay-date variations | Partial | Period/pay-date fields exist; verify holiday and group-specific behavior. |
| PY22-PY25 | Advances, loans, recovery and limits | Partial | Corrected disbursement semantics: request remains Requested; explicit idempotent completion marks Disbursed and only then creates recovery installments. Failure leaves the receivable approved. Added FIFO scheduled-installment recovery application with immutable `recovery_application` evidence and guarded balance updates; pause/reschedule/repayment/write-off APIs remain required. |
| PY26-PY28 | Retro, off-cycle, final settlement | Open | Run types exist; implement complete financial workflows and duplicate prevention. |
| PY29-PY30 | Approval and self-service | Partial | Existing authorization surface; verify SoD and employee restrictions. |
| PY31-PY33 | Reports, compliance outputs, imports/opening balances | Open | Implement documented paginated reports, import preview/commit and opening balances. |
| PY34-PY36 | Scale, security, runbooks/recovery | Partial | Bounded queries and RLS path exist; Payroll/Payments concurrency and security checks pass, while unrelated Subledger performance assertions remain red. |
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

1. The remaining Module 18 scope is not yet closed: Retro, Off-cycle, Final Settlement, compliance/report/import/opening-balance workflows and their dedicated acceptance tests still need implementation.
2. The fresh mandatory backend gate still reproduces two unrelated pre-existing integration failures (Accounting Go-Live reconciliation and Catalog replay) and two unrelated Subledger performance assertions. Payroll-specific integration checks pass; these failures are not caused by the Payroll changes, but they prevent a repository-wide green gate.

## Exact next executable action

Continue with Retro/Off-cycle/Final Settlement and report/import/opening-balance workflows, then run the complete acceptance matrix against the approved disposable PostgreSQL test environment.

## Evidence log

- dotnet build backend/Backend.sln --no-restore /m:1 /v:q: passed, 0 warnings/errors after the latest local changes.
- dotnet test backend/Tests/Modules/Payroll/PayrollTests.csproj /v:q: 4 passed.
- dotnet test backend/Tests/Modules/Payments/PaymentsTests.csproj /v:q: 23 passed.
- Payroll whitespace format verification: passed.
- Full analyzer verification: passed with existing informational diagnostics only.
- DbMigrator development seed: passed; 130 ordered scripts applied or verified.
- Runtime API health and Payroll runs list: HTTP 200 on the real local stack.
- DbMigrator was rebuilt and rerun after Ledger seed edits; each seeded legal entity now has PayrollExpense/PayrollPayable roles, mappings, and `PAYROLL_POSTED_V1` rule legs.
- Real Worker Ledger proof: finalized Payroll fact status `Ready` → `Posted`, balanced journal created, and Payroll run `accounting_state='Posted'`.
- Real Worker Payments handoff proof: 80/80 `payment_instruction` rows `Prepared` → `Requested`, 80 Payments `ExpensePayment` drafts created idempotently, and Payroll run `payment_state='Requested'`.
- Payroll payment event replay proof: second delivery left the count at 80 Payments drafts and reached inbox `attempts=2`, `processed_at` set; no duplicate drafts were created.
- Final local solution build after the latest Payments audit/outbox change: passed, 0 warnings/errors.
- Payroll disbursement path now locks Approved receivables, marks them Disbursed, and generates bounded recovery installments in the same transaction; Payroll module build and 4 tests passed after this change.
- Fresh final verification after the receivable change: `dotnet build backend/Backend.sln --no-restore /m:1 /v:q` passed with 0 warnings/errors; Payroll 4/4 and Payments 23/23 tests passed; `git diff --check` passed.
- Critical disbursement correction: request no longer marks a receivable Disbursed or creates recovery installments; explicit completion is the only path that marks Disbursed and schedules recovery, while explicit failure leaves it approved.
- Migration ordering correction: `payroll-forward-fix-statutory-settings.sql` now has priority 4 before calculation evidence priority 5; fresh disposable bootstrap passed all 129 scripts and existing `todir` upgrade rerun passed with all scripts already applied.
- Payroll Ledger payload now carries Tax/Insurance/Recovery classifications; enabled amounts resolve explicit semantic liability/receivable mappings, while zero amounts in NotManaged mode do not require those mappings. Payroll posting uses the run planned pay date carried in `PayrollFinalizedV1`, not event delivery time.
- Worker reconciliation consumer added for confirmed `PaymentCompletedV1`/`PaymentCancelledV1` events; Payroll run state derives from instruction states (`Paid`, `Partial`, `Failed`, or `Requested`).
- Real Payments confirmation proof after the consumer and PayrollPayable settlement path: payment `01a1a608-e8b7-7637-ac73-04e1d48ddf3d` completed successfully, inbox `payroll-payment-reconciliation` processed its `PaymentCompletedV1`, instruction `ff19e99d-ee64-4ce1-b832-d54cc31d9aba` became `Paid`, and the run became `Partial`. Payment `01a1a608-e8b3-7758-bc2f-7a713cd63e15` was cancelled, its instruction `fef3a795-4993-494b-931e-363e4210489f` became `Failed`, and the run became `Failed`.
- Payments completion now accepts Payroll `ExpensePayment` drafts without inventing a party/open-item; Ledger posts the cash movement against the explicit `PayrollPayable` role.
- Recovery lifecycle migration applied as script 129; calculation includes only due, Disbursed receivables and finalization records guarded FIFO applications in `recovery_application` while settling installment and receivable balances atomically.
- Disposable test environment was resolved locally from the repository workflow: PostgreSQL 18 container `payroll-p04-postgres` on port 15436, admin/runtime connection variables, serial role-password resets, and canonical seed. Fresh bootstrap and existing development upgrade both passed with 130 ordered scripts.
- Payments RLS empty-context correction applied as script 130; a pooled runtime connection with no tenant now returns zero rows instead of raising an empty-string UUID cast, and the previously failing concurrent Payments completion test passed 1/1.
- Fresh mandatory backend gate results: Architecture 21/21, Core 61/61, Accounting 8/8, Catalog 13/13, Directory 11/11, FinanceBase 35/35, Inventory 12/12, Invoicing 17/17, Ledger 55/55, Payments 23/23, Pricing 17/17, Purchasing 10/10, Sales 2/2, Tax 62/62, Observability 7/7, Primitives 5/5, Security 7/7, SeedData 5/5. Integration was 50 passed, 2 failed, 14 skipped; failures were the existing Accounting Go-Live and Catalog Replay scenarios. Subledger was 39 passed and 2 failed in unrelated latency assertions.
- Payroll-specific integration evidence on the disposable database: payment concurrency 1/1, HR payroll delta 2/2, HR report export 2/2, leave-opening import 1/1, and employee import 2/2.
- Final post-change runtime check: rebuilt `backend/Backend.sln` and `DbMigrator` with 0 warnings/errors, started the current API and Worker binaries, API `/health` returned HTTP 200, and Worker registered `payroll-payment-reconciliation` successfully.
- SSH delivery check: `git push origin dev` succeeded and `git rev-parse HEAD` equals `git ls-remote origin refs/heads/dev`.
