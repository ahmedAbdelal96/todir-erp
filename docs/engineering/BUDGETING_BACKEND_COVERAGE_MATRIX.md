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
| FP30 | Budget policy | policy/buckets and effective dates | policy/bucket commands plus transactional capability | Isolated reserve acceptance; source integration still partial | PARTIAL |
| FP31 | Availability | bucket/reservation ledger | availability and source-kind projections | Reserve/release runtime; full policy-version evidence pending | PARTIAL |
| FP32 | Atomic check/reserve | row locks/source uniqueness | check/reserve/adjust/release and owner transaction enlistment | Idempotency path; real contention/rollback evidence pending | PARTIAL |
| FP33 | Purchasing commitments | Planning-owned reservation ledger | Purchasing create/update/approve/cancel enlists precommitment/commitment | Source adapter is implemented; PR/PO lifecycle and receipt reconciliation evidence pending | PARTIAL |
| FP34 | Expenses/commitments | Planning-owned reservation ledger | Expense create/update/finalize/delete enlists reservation/consumption | Source adapter is implemented; approval/correction evidence pending | PARTIAL |
| FP35 | Commitment→Actual | consumption table and bucket actual total | Expense finalization consumes an open reservation transactionally | Expense path source proof; Purchasing partial/final actual reconciliation remains open | PARTIAL |
| FP36 | Overrides/thresholds | override table only | no request/approve API | No SoD proof | OPEN |
| FP37 | Department planner | submission persistence | no frontend in this mission | Backend slice only | PARTIAL |
| FP38 | CFO/owner views | variance/statements partial | no complete rollup/action contract | No complete evidence | OPEN |
| FP39 | Excel/CSV | no staging/validation | no safe file endpoints | Mandatory gate absent | OPEN |
| FP40 | Multi-entity rollup | entity keys exist | no rollup projection | No proof | OPEN |
| FP41 | Security/SoD | RLS/permissions/lifecycle | route authorization | security/architecture suites | PARTIAL |
| FP42 | Audit/history | audit on implemented writes | lifecycle audit | SQL/API inspection | PARTIAL |
| FP43 | Migration/opening budget | bootstrap/forward-fix | seed replay | Disposable migration/seed PASS; opening reconciliation gate absent | PARTIAL |
| FP44 | Performance/scale | grid/availability indexes | bounded queries | No formal scale certification | PARTIAL |
| FP45 | Observability | platform logging/outbox | no planning metrics | No dedicated proof | OPEN |
| FP46 | Runbooks/recovery | migration/seed safe | no planning recovery runbook | Documentation gap | OPEN |
| FP47 | Small company | basic seed budget/forecast | core cycle/grid/control | Foundation runtime | PARTIAL |
| FP48 | Department manager | submission backend slice | no scoped UI/delegated approval | No complete proof | OPEN |
| FP49 | FP&A | drivers/variance partial | no integrated forecast workflow | No complete proof | OPEN |
| FP50 | CFO | P&L partial | BS/CF/rollup absent | No complete proof | OPEN |
| FP51 | Purchasing control | reservation source identity | Purchasing transactional capability integration | Source reservation path exists; full PR/PO/receipt lifecycle proof absent | PARTIAL |
| FP52 | Cross-module reconciliation | source metadata fields | partial owner integration | No complete owner reconciliation jobs/evidence | PARTIAL |
| FP53 | E2E/release | migration/RLS verified | backend foundation only | Release/architecture/OpenAPI pass; module gate not | PARTIAL |

## Build Now MUST audit

The mandatory Build Now list in section 789 maps to FP02–FP53 above. The `OPEN` and `PARTIAL` rows are release blockers for the full Module 28 backend mission: OPEX, integrated BS/CF, rolling forecast, complete Purchasing/Expenses owner lifecycles, commitment reconciliation, overrides, Excel interop, rollup, observability/runbooks, and their acceptance journeys. This delivery therefore remains `NOT COMPLETE`.

## Verified commands

- `dotnet build backend/Backend.sln --configuration Release --no-restore` — PASS after the latest source change.
- `dotnet build backend/Backend.sln --configuration Release --no-restore` — PASS after the transactional source integration changes.
- `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-restore` — PASS, 4/4 after the final source integration edit.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --configuration Release --no-restore` — PASS, 23/23 after the final source integration edit.
- `node scripts/architecture/validate-openapi.mjs` — PASS; Planning contract validated with 63 paths and the eight existing contracts also passed.
- `dotnet test backend/Tests/Security/SecurityTests.csproj --configuration Release --no-restore` with disposable admin connection — PASS, 7/7.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~PlanningBudgetControlIntegrationTests` — PASS, 1/1; real PostgreSQL owner lifecycle proved reservation creation, amendment, and release.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ApiCompositionTests` — PASS, 4/4 on the disposable seeded database.
- `dotnet format` targeted changed C# files with `--verify-no-changes` — PASS. Full solution formatter remains affected by pre-existing Ledger and Planning test whitespace findings.
- Disposable PostgreSQL migration and Development Seed replay — PASS on `planning_closure_635323`; the database is disposable and must be removed after final verification.
- Real cookie/CSRF/PostgreSQL journey — existing API composition PASS, 4/4; no complete Purchasing/Expenses budget-control HTTP acceptance is claimed yet.
