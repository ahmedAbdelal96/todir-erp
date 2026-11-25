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
| FP31 | Availability | bucket/reservation ledger and policy basis | source-kind-aware availability, currency checks, negative-budget and shadow-mode handling | 7/7 focused PostgreSQL tests; full carryforward/FX evidence remains open | PARTIAL |
| FP32 | Atomic check/reserve | row locks/source uniqueness | owner transaction enlistment, bounded overrides, deterministic correction/reconciliation rebuild | omitted-bucket Block, idempotent partial consumption/correction and rebuild acceptance pass; contention/rollback matrix remains open | PARTIAL |
| FP33 | Purchasing commitments | Planning-owned reservation ledger | Purchasing create/update/approve/cancel enlists precommitment/commitment and validates context | Source adapter is implemented; PR conversion, receipt/return and full PO amendment evidence remain open | PARTIAL |
| FP34 | Expenses/commitments | Planning-owned reservation ledger | Expense create/update/finalize/delete/reversal enlists reservation/consumption/correction | owner lifecycle and reversal correction path implemented; authenticated full correction journey remains open | PARTIAL |
| FP35 | Commitment→Actual | consumption/correction facts and bucket actual total | posted supplier invoice consumes once; credit note and expense reversal create bounded correction facts | focused actual/correction tests pass; complete Ledger event replay and multi-invoice evidence remain open | PARTIAL |
| FP36 | Overrides/thresholds | source-bound override with used amount, validity and approval link | request/decision API through Platform Approvals; bounded exact-source usage and SoD permission | direct bounded-usage acceptance passes; real authenticated approval journey and configurable policy evidence remain open | PARTIAL |
| FP37 | Department planner | submission persistence | no frontend in this mission | Backend slice only | PARTIAL |
| FP38 | CFO/owner views | variance/statements partial | no complete rollup/action contract | No complete evidence | OPEN |
| FP39 | Excel/CSV | no staging/validation | no safe file endpoints | Mandatory gate absent | OPEN |
| FP40 | Multi-entity rollup | entity keys exist | no rollup projection | No proof | OPEN |
| FP41 | Security/SoD | RLS/permissions/lifecycle | route authorization | security/architecture suites | PARTIAL |
| FP42 | Audit/history | audit on implemented writes | lifecycle audit | SQL/API inspection | PARTIAL |
| FP43 | Migration/opening budget | bootstrap/forward-fix and reconciliation runs | disposable migration/seed, strict Block opening-reconciliation guard, reconciliation/rebuild endpoint | 269-script migration/seed PASS; canonical owner opening mapping and activation workflow remain open | PARTIAL |
| FP44 | Performance/scale | grid/availability indexes | bounded queries | No formal scale certification | PARTIAL |
| FP45 | Observability | platform logging/outbox | no planning metrics | No dedicated proof | OPEN |
| FP46 | Runbooks/recovery | migration/seed and reconciliation runs | bounded projection rebuild with durable success/failure evidence | runbook added for supported rebuild/reconciliation; source discrepancy repair workflow remains open | PARTIAL |
| FP47 | Small company | basic seed budget/forecast | core cycle/grid/control | Foundation runtime | PARTIAL |
| FP48 | Department manager | submission backend slice | no scoped UI/delegated approval | No complete proof | OPEN |
| FP49 | FP&A | drivers/variance partial | no integrated forecast workflow | No complete proof | OPEN |
| FP50 | CFO | P&L partial | BS/CF/rollup absent | No complete proof | OPEN |
| FP51 | Purchasing control | reservation source identity | Purchasing transactional capability integration with server-side bucket resolution and commitment kind | source reservation and amendment path pass; full PR/PO/receipt lifecycle proof absent | PARTIAL |
| FP52 | Cross-module reconciliation | source metadata and correction facts | supplier invoice/credit and expense reversal seams plus planning projection rebuild | focused source/correction/rebuild evidence; complete owner reconciliation jobs absent | PARTIAL |
| FP53 | E2E/release | migration/RLS verified | BC01 APIs and source seams | Release build, architecture, OpenAPI and 7/7 focused PostgreSQL acceptance pass; complete module gate not | PARTIAL |

## Build Now MUST audit

The mandatory Build Now list in section 789 maps to FP02–FP53 above. The `OPEN` and `PARTIAL` rows are release blockers for the full Module 28 backend mission: OPEX, integrated BS/CF, rolling forecast, complete Purchasing/Expenses owner lifecycles, commitment reconciliation, overrides, Excel interop, rollup, observability/runbooks, and their acceptance journeys. This delivery therefore remains `NOT COMPLETE`.

## Verified commands

- `dotnet build backend/Backend.sln --configuration Release --no-restore` — PASS after the latest source change.
- `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-restore` — PASS, 4/4 after the final source integration edit.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --configuration Release --no-restore` — PASS, 23/23 after the final source integration edit.
- `node scripts/architecture/validate-openapi.mjs` — PASS; Planning contract validated with 67 paths and the eight existing contracts also passed.
- `dotnet test backend/Tests/Security/SecurityTests.csproj --configuration Release --no-restore` with disposable admin connection — PASS, 7/7.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-build --filter FullyQualifiedName~PlanningBudgetControlIntegrationTests` — PASS, 7/7 on disposable `bc01_closure_20261125`; omitted-bucket strict control, partial consumption/correction idempotency, bounded source-specific override use, expense owner lifecycle, and projection reconciliation/rebuild were asserted.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter ApiCompositionTests.ApiBuildsWithValidatedPostgresTransactionAndInboxServices|ApiCompositionTests.DevelopmentOpenApiIncludesFinBaseRoutesAndFinancialAmountsAsStrings` — PASS, 2/2 with the disposable database's administrative connection. The remaining POS and Banking HTTP cases were not accepted on the app-role connection because the local PostgreSQL role password is out of sync with the documented development connection; this is an environment gate, not BC01 evidence.
- `dotnet format` targeted changed C# files with `--verify-no-changes` — PASS. Full solution formatter remains affected by pre-existing Ledger and Planning test whitespace findings.
- Disposable PostgreSQL migration and Development Seed replay — PASS on `bc01_closure_20261125`; 269 ordered scripts, RLS/grants for the reconciliation run table, and Atlas/Delta seed completed. The database is disposable and must be removed after final verification.
- Real cookie/CSRF/PostgreSQL journey — no complete Purchasing/Expenses budget-control HTTP acceptance is claimed yet; the API composition contract subset above passed, while the app-role login cases remain an environment gate.
