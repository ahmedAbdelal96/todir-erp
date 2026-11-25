# Module 28 Backend Coverage Matrix

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This matrix is deliberately requirement-led. `PASS` means the backend behavior and a repeatable evidence path exist; `PARTIAL` means a useful slice exists but the canonical exit gate is not yet met; `OPEN` means the mandatory scope is not implemented. A green build is not used as evidence for a business workflow.

| ID | Capability | Database / owner | API / implementation | Evidence | Status |
|---|---|---|---|---|---|
| FP00 | Repository/dependency verification | Planning manifest and solution | `Planning.csproj`, registration | Release build, architecture | PARTIAL |
| FP01 | Module boundary/schema/RLS | `business_planning`, FORCE RLS | Transaction-coordinator repository | Disposable migration | PASS |
| FP02 | Fiscal calendar/planning cycle | `planning_cycle` and state checks | cycle CRUD/lifecycle | Seed + authenticated cycle read | PASS |
| FP03 | Typed models/dimensions | model/version/dimension/member tables | model/version and dimension/member commands | Real HTTP dimension/member journey | PARTIAL |
| FP04 | Metrics/mappings | metric, mapping, source metadata | mapping command; refresh identity | Mapping HTTP path | PARTIAL |
| FP05 | Sparse coordinates/input facts | signature uniqueness | grid query/batch/paste | Seed grid read; tests | PASS |
| FP06 | Version/scenario core | plan/scenario/access tables | clone/share/adopt-preview | Real scenario path; immutability pending | PARTIAL |
| FP07 | Grid query/batch | paged coordinate indexes | query/write/paste and legacy spread | API composition/runtime grid | PASS |
| FP08 | Actual snapshot/Ledger | snapshot/source watermark | keyed refresh/run status | Idempotent refresh path | PARTIAL |
| FP09 | Original budget baseline | baseline state/immutability | create/current/revision approval | Seed/SQL checks | PARTIAL |
| FP10 | Revisions/transfers | revision/transfer | bucket resolution/approval | Targeted E2E pending | PARTIAL |
| FP11 | Department submissions | submission units/coordinates | create/submit/return/approve/lock | Real HTTP transition | PARTIAL |
| FP12 | Assumptions/drivers | assumption/value | create/value/list | Seed/API | PASS |
| FP13 | Formula DAG | formula/dependency | whitelist and cycle rejection | 4 Planning tests + HTTP | PARTIAL |
| FP14 | Calculation/trace | run/trace/calculated facts | deterministic revenue calculation | Existing path; full DAG open | PARTIAL |
| FP15 | Spreading | spread rule/input facts | exact residual allocation on `spread-exact` | unit test + HTTP conservation | PARTIAL |
| FP16 | Allocations | rule/result with lineage | weighted allocation | HTTP conservation | PARTIAL |
| FP17 | Revenue | revenue driver | volume × price | Seed/recalculate path | PARTIAL |
| FP18 | OPEX | grid inputs only | no dedicated driver/category workflow | No complete evidence | OPEN |
| FP19 | Workforce | workforce lines | add/list/import/cost-impact | Seed + HTTP; owner integration open | PARTIAL |
| FP20 | CapEx | CapEx lifecycle table | add/list/prioritize/approve | Seed + HTTP; depreciation open | PARTIAL |
| FP21 | Planning FX | versioned rates | rate input | API path; conversion trace open | PARTIAL |
| FP22 | Integrated P&L | statement projection table | deterministic P&L query | HTTP P&L; mapping incomplete | PARTIAL |
| FP23 | Working capital | assumptions | assumption write | Seed/API; derived model open | PARTIAL |
| FP24 | Projected balance sheet | no complete model | not implemented | No balance proof | OPEN |
| FP25 | Projected cash flow | proxy only | no reconciled integrated cash model | Proxy not acceptance evidence | OPEN |
| FP26 | Rolling forecast | cutoff field only | no horizon roll job | No evidence | OPEN |
| FP27 | What-if | scenario state/access | clone/share/adopt | HTTP scenario path | PARTIAL |
| FP28 | Variance/drivers | variance/comments | query and driver endpoint | API path; decomposition open | PARTIAL |
| FP29 | Forecast accuracy | accuracy projection | aggregate error/bias | Implementation; fixture open | PARTIAL |
| FP30 | Budget policy | policy/buckets, effective dates, horizon and published-version links | annual/quarterly/monthly period normalization, validated bucket hints, published-version and legal-entity checks | Isolated PostgreSQL owner acceptance; department/cost-center authorization mapping remains open | PARTIAL |
| FP31 | Availability | bucket/reservation ledger and policy basis | source-kind-aware availability, currency checks, negative-budget and shadow-mode handling; reconciled owner snapshots are not double-counted | 8/8 focused PostgreSQL tests plus BC-T10 `40,000/60,000/20,000 → 80,000` owner vector | PASS for BC01 |
| FP32 | Atomic check/reserve | row locks/source uniqueness | owner transaction enlistment, bounded overrides, deterministic correction/reconciliation rebuild | BC-T05 concurrent `80,000 + 80,000` approval and BC-T06 injected rollback pass; omitted-bucket/idempotency/rebuild tests pass | PASS for BC01 |
| FP33 | Purchasing commitments | Planning-owned reservation ledger plus Purchasing-owned requisition/conversion lineage | Requisition draft/submit/approve, partial/full PR→PO conversion, row locks, idempotency and precommitment replacement | Real authenticated disposable PR `100,000 → 40,000 + 60,000` HTTP vector; BC-T05/06 approval and rollback; duplicate and over-limit checks | PASS for BC01; broader receipt/return lifecycle remains outside BC01 |
| FP34 | Expenses/commitments | Planning-owned reservation ledger plus durable reversal intent | Expense create/update/finalize/delete/reversal enlists reservation/consumption/correction; failed correction remains retryable | BC-T08 real PostgreSQL failure/retry; intent `Failed → Completed`, one Ledger reversal and one Planning correction | PASS for BC01 |
| FP35 | Commitment→Actual | consumption/correction facts and bucket actual total | posted supplier invoice consumes once; credit note and Expense reversal intent create bounded correction facts | Supplier-invoice/credit correction owner test, BC-T08 recovery, no-reservation fail-closed and correction replay pass | PASS for BC01; wider module lifecycle remains partial |
| FP36 | Overrides/thresholds | source-bound override with used amount, validity and approval link | request/decision API through Platform Approvals; bounded exact-source usage and SoD permission | BC-T09 authenticated requester/approver/unauthorized HTTP journey; bounded source/version usage and retry pass | PASS for BC01 |
| FP37 | Department planner | submission persistence | no frontend in this mission | Backend slice only | PARTIAL |
| FP38 | CFO/owner views | variance/statements partial | no complete rollup/action contract | No complete evidence | OPEN |
| FP39 | Excel/CSV | no staging/validation | no safe file endpoints | Mandatory gate absent | OPEN |
| FP40 | Multi-entity rollup | entity keys exist | no rollup projection | No proof | OPEN |
| FP41 | Security/SoD | RLS/permissions/lifecycle | route authorization | security/architecture suites | PARTIAL |
| FP42 | Audit/history | audit on implemented writes | lifecycle audit | SQL/API inspection | PARTIAL |
| FP43 | Migration/opening budget | bootstrap/forward-fix, owner reconciliation and reconciliation runs | disposable migration/seed, strict Block guard requires successful owner reconciliation, owner-source comparison and bounded rebuild | BC-T10 authenticated discrepancy detection, rebuild, zero-discrepancy readiness and strict activation window pass | PASS for BC01 |
| FP44 | Performance/scale | grid/availability indexes | bounded queries | No formal scale certification | PARTIAL |
| FP45 | Observability | platform logging/outbox | no planning metrics | No dedicated proof | OPEN |
| FP46 | Runbooks/recovery | migration/seed, owner reconciliation and reversal intent | bounded rebuild, durable owner result, failed reversal intent and retry semantics | BC-T06/08 failure-injection recovery and BC-T10 owner watermark/rebuild evidence; production runbook scope remains broader | PASS for BC01 |
| FP47 | Small company | basic seed budget/forecast | core cycle/grid/control | Foundation runtime | PARTIAL |
| FP48 | Department manager | submission backend slice | no scoped UI/delegated approval | No complete proof | OPEN |
| FP49 | FP&A | drivers/variance partial | no integrated forecast workflow | No complete proof | OPEN |
| FP50 | CFO | P&L partial | BS/CF/rollup absent | No complete proof | OPEN |
| FP51 | Purchasing control | requisition/PO lineage and reservation source identity | Purchasing-owned PR lifecycle, conversion replacement, PO approval history/outbox and server-side bucket resolution | Real disposable 100k PR conversion and 40/60 reservation inspection pass; full receipt/return vector remains open | PARTIAL |
| FP52 | Cross-module reconciliation | owner financial-fact contracts and durable owner result | Purchasing and Ledger expose bounded owner facts; Planning compares source facts to bucket totals and records watermark/discrepancy | BC-T10 real Expense/PO/PR owner vector plus authenticated reconcile/rebuild/zero-discrepancy verification | PASS for BC01 |
| FP53 | E2E/release | migration/RLS verified | BC01 APIs and source seams | BC-T01–T12 evidence, Ledger 56/56 twice, authenticated HTTP, migration/seed, architecture/OpenAPI and Release gates; complete Module 28 gate not | PASS for BC01 |

## Build Now MUST audit

The mandatory Build Now list in section 789 maps to FP02–FP53 above. The `OPEN` and `PARTIAL` rows are release blockers for the full Module 28 backend mission: OPEX, integrated BS/CF, rolling forecast, complete Purchasing/Expenses owner lifecycles, commitment reconciliation, overrides, Excel interop, rollup, observability/runbooks, and their acceptance journeys. This delivery therefore remains `NOT COMPLETE`.

## Verified commands

- `dotnet build backend/Backend.sln --configuration Release --no-restore` — PASS after the latest source change, 0 warnings/0 errors.
- `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-restore` — PASS, 4/4 after the final source integration edit.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --configuration Release --no-restore` — PASS, 23/23 after the final source integration edit.
- `node scripts/architecture/validate-openapi.mjs` — PASS; Planning contract validated with 67 paths and the eight existing contracts also passed.
- `dotnet test backend/Tests/Security/SecurityTests.csproj --configuration Release --no-restore` with disposable admin connection — PASS, 7/7.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-build --filter FullyQualifiedName~PlanningBudgetControlIntegrationTests` — PASS, 8/8 on disposable PostgreSQL; omitted-bucket strict control, partial consumption/correction idempotency, bounded source-specific override use, expense owner lifecycle, and projection reconciliation/rebuild were asserted.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApiCompositionTests"` with Development signing configuration, the isolated runtime-role connection and seed password — PASS, 4/4.
- Real disposable Purchasing HTTP PR vector — PASS: `100,000 EGP` approved, converted to approved `40,000` and `60,000` POs, duplicate conversion idempotent, over-limit conversion rejected, and reservation ledger inspected.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RealSupplierInvoiceActualReconcilesProjectCommitment"` — PASS, 1/1 after the source-reservation guard correction.
- `dotnet format` targeted changed C# files with `--verify-no-changes` — PASS after correcting the requested whitespace-only `LedgerRepository.cs` finding. Full unrelated solution findings are not treated as BC01 evidence.
- Disposable PostgreSQL migration and Development Seed replay — PASS on `bc01_pr_20261125`; 273 ordered scripts, PR/reversal-intent/owner-reconciliation tables, RLS/grants, and Atlas/Delta seed completed. The database is disposable and must be removed after final verification.
- Real cookie/CSRF/PostgreSQL Purchasing journey — PR conversion and approval authorization passed on the isolated runtime role. Complete Expenses/Planning control HTTP acceptance, strict activation, live Worker replay and full receipt/return chain remain open.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ConcurrentCompetingPurchasingApprovalsConserveOneStrictCommitment|FullyQualifiedName~PurchasingReservationRollsBackWithOwnerTransactionAndRetrySucceedsOnce|FullyQualifiedName~CanonicalOwnerReconciliationAgreesWithRealSourceWorkflows"` — PASS for the BC-T05/06/10 focused integration vectors on disposable PostgreSQL; BC-T05 and BC-T06 passed twice sequentially on the repeat-safe fixture, and BC-T10 observed `40,000 EGP` Actual, `60,000 EGP` Commitment, `20,000 EGP` Precommitment and `80,000 EGP` availability after authenticated reconciliation/rebuild using a unique future year.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PlanningBudgetControlHttpAcceptanceTests"` — PASS for the authenticated BC-T09 requester/approver/unauthorized HTTP journey with cookie/CSRF and PostgreSQL inspection.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BudgetReversalRecoversAfterLedgerCommitBeforePlanningCorrection|FullyQualifiedName~BudgetReversalLedgerFailureLeavesRetryableIntent"` — PASS 2/2 for the durable BC-T08 failure/retry paths; the strict opening/omitted-bucket boundary is included in the separate Planning Budget Control 8/8 run above.
- `dotnet test backend/Tests/Modules/Ledger/LedgerTests.csproj --configuration Release --no-build` — PASS `56/56`, repeated twice on disposable PostgreSQL after the fixture isolation correction.
