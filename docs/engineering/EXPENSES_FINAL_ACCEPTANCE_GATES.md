# Module 21 Expenses — Final Acceptance Gates

Canonical source: `docs/modules/21_ERP_SAAS_EXPENSES_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
Execution checkpoint: `docs/engineering/EXPENSES_BACKEND_EXECUTION_STATE.md`

This is an executable acceptance matrix. A gate is `PASS` only when the listed persisted/API evidence has been run after the last relevant source change. `PARTIAL` means implementation exists but the canonical acceptance proof is incomplete; `NOT IMPLEMENTED` means the backend contract or workflow is missing.

| Gate | Expected backend behavior | Existing implementation / executable proof | Status | Evidence |
|---|---|---|---|---|
| EX00 | Dependencies, ownership boundaries, and approved contracts are verified. | Module manifest, architecture tests, and dependency review. | PASS | Architecture 21/21; Expenses suite 34/34. |
| EX01 | `business_expenses` schema, migrations, grants, tenant RLS, and legal-entity scoping are active. | Ordered DbMigrator scripts and RLS integration tests. | PASS | Fresh disposable `todir_expenses_api_acceptance_20261109` bootstrap applied/verified all 177 ordered scripts; seed completed for Atlas and Delta. |
| EX02 | Categories and effective-dated versions can be created, published, archived, and preserved immutably. | Repository commands, SQL publication guards, category lifecycle tests. | PASS | Expenses PostgreSQL suite. |
| EX03 | Expense draft lifecycle and independent state dimensions enforce valid transitions and concurrency. | Create/update/submit/approve/finalize/reverse repository paths and persisted tests. | PASS | Expenses PostgreSQL suite. |
| EX04 | Authenticated self-service capture resolves employee identity server-side and prevents cross-employee access. | HR capability resolution and `/my` contracts. | PARTIAL | Repository proof exists; authenticated red-team matrix still required. |
| EX05 | Receipts use authorized Platform Files references, replacement history, malware state, and missing-receipt lifecycle. | Evidence lifecycle and Platform Files upload/download contracts. | PASS | `HrDocumentDownloadIntegrationTests.SensitiveDocumentRequiresSensitivePermissionAndReturnsPrivateShortLivedDownload`: real MinIO upload, private short-lived download, ClamAV fail-once recovery, clean upload, EICAR rejection, and cross-entity denial passed 1/1. |
| EX06 | OCR runs asynchronously, retries safely, fails closed without blocking manual entry, and never becomes financial authority. | OCR job/outbox/worker, retry/manual fallback, provider success/failure tests. | PARTIAL | Expenses PostgreSQL 34/34; real Worker run on disposable PostgreSQL/RabbitMQ published `expenses.ocr.requested`, persisted `Failed` with `expenses.ocr_provider_unavailable`, incremented attempt count, and marked inbox processed. Provider-success and manual-fallback assertions remain repository-level; end-to-end retry/recovery evidence is still incomplete. |
| EX07 | Itemization and participants are draft-scoped and persisted with correct totals. | Repository commands and finalization guards. | PASS | Expenses PostgreSQL suite. |
| EX08 | Typed, effective-dated policy rules evaluate applicability, findings, exceptions, and immutable snapshots. | Policy versions/evaluations and receipt/business-purpose rules. | PARTIAL | Persisted policy evidence exists; full typed exception matrix incomplete. |
| EX09 | Platform Approvals routes authorized requests, enforces scope/separation of duties, stale protection, and decision replay. | Shared approval capability and Expense linkage. | PARTIAL | Persisted replay/stale tests; full authenticated authority matrix incomplete. |
| EX10 | Finalization posts one immutable Ledger fact with FinanceBase/Tax snapshots and blocks invalid evidence/approval/card states. | Finalization/reversal repository and Ledger capability. | PASS | Persisted Ledger and reversal evidence. |
| EX11 | Employee-paid finalization creates one reimbursement obligation and immutable history. | Idempotent obligation creation and outbox event. | PASS | Persisted financial acceptance. |
| EX12 | Payments reimbursement requests settle allocations exactly once and recover failed/replayed events. | Payments capability, batch settlement, and notification consumer. | PASS | Expenses financial tests and Payments contract evidence. |
| EX13 | Reimbursement batches are paginated, versioned, approval-safe, and reconcile exact allocations. | Batch list/detail/pay command. | PASS | Expenses PostgreSQL suite. |
| EX14 | Corporate-card feed transactions preserve provider identity, pending/cleared states, duplicates, and unmatched inbox records. | Idempotent import, normalization, clear, ignore, list/reconciliation. | PARTIAL | Persisted card acceptance; external provider adapter proof incomplete. |
| EX15 | Cleared card transactions match or split-allocate exactly once and cannot be mismatched or unlinked after finalization. | Match, allocate, unmatch, amount/currency guards. | PASS | Persisted split/match tests. |
| EX16 | Personal card/company-card personal charges create one employee repayment and settle idempotently. | HR-scoped employee validation and repayment capability. | PASS | Persisted card/repayment tests. |
| EX17 | Petty cash and company-paid sources finalize with correct accounting and no reimbursement obligation. | Payment-source guards and Ledger facts. | PASS | Expenses financial evidence. |
| EX18 | Business advances request/approve with policy, actor, employee, entity, and concurrency controls. | Advance lifecycle and Platform Approval linkage. | PASS | Persisted advance approval evidence. |
| EX19 | Advance issuance moves actual money through Payments once and posts EmployeeReceivable. | Payments issuance capability and idempotency reservation. | PASS | Financial acceptance. |
| EX20 | Expense applications consume advance balances with employee/currency/FX snapshots and no oversubscription. | Apply advance command and FinanceBase context. | PASS | Cross-currency persisted evidence. |
| EX21 | Returns, shortfalls, overdue balances, write-offs, and settlement reconcile to zero. | Return/write-off/overdue paths and Ledger write-off. | PARTIAL | Core write-off/currency tests pass; complete EX39 scenario matrix remains. |
| EX22 | Mileage uses versioned effective-dated rates and reproducible snapshots. | Rate policy and mileage detail. | PASS | Expenses PostgreSQL suite. |
| EX23 | Per diem uses versioned rates, meal reductions, eligibility, and snapshots. | Rate policy and per-diem detail. | PASS | Expenses PostgreSQL suite. |
| EX24 | Multi-currency amounts preserve original values and authoritative FinanceBase FX snapshots. | Finalization and advance application FX contracts. | PASS | Persisted FX evidence. |
| EX25 | Tax/recoverability uses Tax Engine evidence without invented legal rates. | Tax capability snapshot and fail-closed behavior. | PARTIAL | Provider-neutral snapshot exists; complete Tax Engine acceptance remains. |
| EX26 | Cost dimensions/project references are validated, scoped, and immutable after financial finalization. | Dimension set/list and draft-only correction. | PASS | Expenses PostgreSQL suite. |
| EX27 | Expense reports group finalized expenses, maintain totals, and expose paginated items/history. | Report lifecycle, item add/remove, and history. | PASS | Persisted report evidence. |
| EX28 | Corrections/reversals preserve original facts and post compensating accounting. | Reverse/finalization guards and immutable history. | PASS | Ledger reversal evidence. |
| EX29 | Payroll settlement seam is explicit and does not fabricate payroll facts. | Contract seam only where configured. | PARTIAL | No complete cross-module Payroll acceptance run. |
| EX30 | Notifications/reminders are durable, idempotent, and recoverable. | Outbox notification consumer for finalization/reimbursement. | PARTIAL | Event consumer proof exists; scheduled reminders/recovery incomplete. |
| EX31 | Finance exception workbench, reports, filters, exports, and read models are bounded and accurate. | Exception summary, spend report, CSV export, paginated lists. | PARTIAL | Core reports pass; entity-scoped report detail is covered by `ReportDetailReadIsEntityScopedAndReturnsAuthoritativePersistedData`; complete read-model/import coverage remains incomplete. |
| EX32 | Imports/migrations validate rows, preserve idempotency, report outcomes, and never partially corrupt financial data. | Import batch, row outcomes, duplicate external-reference guards. | PARTIAL | Repository/import evidence exists; full migration/import acceptance incomplete. |
| EX33 | Authenticated Employee/Manager/Finance APIs enforce tenant/entity/employee scope, IDOR, RLS, and sensitive evidence controls. | Endpoint permission filters, self-service HR scope, PostgreSQL RLS. | PARTIAL | Fresh disposable authenticated `ExpensesApiIntegrationTests.PolicyCreationUsesLegalEntityRouteAndRejectsPayloadScopeMismatch` passed 1/1; full employee/manager/Finance red-team matrix remains. |
| EX34 | Scalable lists are bounded, indexed, deterministic, and have acceptable query behavior. | Page-size clamps, tenant/entity indexes, filtered reports/cards/advances. | PARTIAL | Bounded list implementation; formal scale/query-plan evidence missing. |
| EX35 | Operational runbooks document migration, worker, provider failure, replay, and recovery procedures. | Existing engineering checkpoint and platform runbooks. | PARTIAL | Expenses-specific recovery/runbook evidence incomplete. |
| EX36 | Employee acceptance completes personal funds, missing receipt, corporate card, and advance journeys with persisted outcomes. | Repository-level scenarios exist. | NOT COMPLETE | Authenticated end-user acceptance not yet executed. |
| EX37 | Finance acceptance verifies review, reimbursement, personal card repayment, card inbox, and advance outcomes. | Financial repository tests cover individual paths. | NOT COMPLETE | Complete Finance journey and persisted reconciliation matrix not executed. |
| EX38 | Provider/feed card acceptance proves pending→cleared, duplicate, normalization, match, personal, refund/credit, split, unmatched. | Card repository tests cover deterministic provider-neutral flows. | PARTIAL | External provider-boundary acceptance remains. |
| EX39 | Advance acceptance proves request→approve→issue→apply→return/shortfall→FX→overdue→termination with zero balance. | Advance issuance/application/return/write-off paths. | PARTIAL | Core persisted evidence; complete scenario matrix incomplete. |
| EX40 | Finalized expenses, Ledger/tax/control facts, Payments allocations, repayments, and advances reconcile exactly. | Ledger/Payments/FinanceBase assertions exist by workflow. | NOT COMPLETE | One complete zero-difference cross-module run not executed. |
| EX41 | Applicable country-pack rules are tested only when configured and never invented globally. | Country-neutral provider seams. | PARTIAL | Optional country certification remains external/configuration-dependent. |
| EX42 | Employee, card, personal repayment, advance, project, and optional payroll cross-module journeys pass end-to-end. | Individual contracts exist. | NOT COMPLETE | Full authenticated cross-module run not executed. |
| EX43 | Release gate has fresh migrations/seed, all mandatory tests, security, financial reconciliation, clean tree, and pushed commit. | Build/format/architecture/Expenses gates and SSH push. | NOT COMPLETE | Fresh migration/seed, authenticated API, and real Worker failure-path evidence now exist; EX06, EX33, EX36–EX42 remain incomplete. |

## Current execution baseline

- Last verified source commit before this matrix: `e5306b5b8980c8abc2c4a04a091a783a90ecff23`.
- Isolated PostgreSQL Expenses suite: `35/35 passed, 0 skipped` (includes entity-scoped report-detail read acceptance).
- Architecture suite: `21/21 passed`.
- Backend build and format verification: passed.
- First active failing gate: **EX06 OCR worker recovery evidence** (the failure-path is now proven, but retry/recovery and provider/manual fallback still need an end-to-end worker assertion), followed by EX08/EX09 policy/approval depth, EX14 provider feed, EX30/EX31/EX32 operations, EX33 red-team, and EX36–EX43.

The matrix is intentionally not a completion claim. Update each row only with executable evidence from the current repository state.
