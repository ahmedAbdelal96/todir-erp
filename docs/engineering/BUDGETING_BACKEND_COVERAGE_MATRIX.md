# Module 28 Backend Coverage Matrix

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This matrix is deliberately requirement-led. `PASS` means the applicable Module 28 backend behavior and a repeatable acceptance path exist. Frontend implementation, Product/UX acceptance, human approval, statutory reporting, and production-scale certification remain separate gates where noted; those boundaries do not reopen verified backend behavior. A green build is not used as evidence for a business workflow.

| ID | Capability | Database / owner | API / implementation | Evidence | Status |
|---|---|---|---|---|---|
| FP00 | Repository/dependency verification | Planning manifest and solution | `Planning.csproj`, registration | Release build 0 warnings/0 errors; Architecture 23/23; Release gates 73/73 | PASS |
| FP01 | Module boundary/schema/RLS | `business_planning`, FORCE RLS | Transaction-coordinator repository | Disposable migration | PASS |
| FP02 | Fiscal calendar/planning cycle | `planning_cycle` and state checks | cycle CRUD/lifecycle | Seed + authenticated cycle read | PASS |
| FP03 | Typed models/dimensions | model/version/dimension/member tables | model/version and dimension/member commands plus bounded list routes | Planning HTTP coverage 23/23, OpenAPI 91 paths, and Package-C scoped read models | PASS |
| FP04 | Metrics/mappings | metric metadata, versioned mapping, target FK | create/publish/retire mapping; owner refresh identity | `PlanningBc02ActualIntegrationTests.MetricMappingPublishRetireAndReplacementPreserveHistory`; BC2-T01 authoritative mapping | PASS |
| FP05 | Sparse coordinates/input facts | signature uniqueness | grid query/batch/paste | Seed grid read; tests | PASS |
| FP06 | Version/scenario core | plan/scenario/access tables | clone/share/adopt-preview plus persisted plan-version scope checks | Package-C scenario/access 2/2; BC2-T07 all published mutation routes reject changes | PASS |
| FP07 | Grid query/batch | paged coordinate indexes | query/write/paste and legacy spread | API composition/runtime grid | PASS |
| FP08 | Actual snapshot/Ledger | immutable snapshot metadata/facts, provenance, RLS | owner-contract refresh, source selection, watermark and run status; fourteen registered owner providers | BC2-T01/T02/T03 4/4; BC2-T04 write protection; BC2-T15 owner lifecycle 2/2; owner-provider replay 1/1 | PASS |
| FP09 | Original budget baseline | baseline lifecycle and immutable original amount | create/current/lifecycle routes | `PlanningBc02ActualIntegrationTests.CurrentBudgetUsesEffectiveApprovedChangesAndNetsTransfers`; BC2-T08 | PASS |
| FP10 | Revisions/transfers | idempotency columns and conserving transfer state | revision approval and deterministic transfer locking | BC2-T08 current-budget vector; BC2-T09 concurrent transfer conservation and idempotent replay | PASS |
| FP11 | Department submissions | coordinate attachment, actor fields, approval identity | submit/return/approve/lock through Platform Approval | BC2-T10 submission isolation/lock 2/2; BC2-T11 approval SoD/delegation 2/2 | PASS |
| FP12 | Assumptions/drivers | assumption/value | create/value/list | Seed/API | PASS |
| FP13 | Formula DAG | formula/dependency | constrained decimal parser, allow-listed functions, dependency ordering and cycle rejection | Planning unit 6/6 plus BC03 golden-vector integration | PASS |
| FP14 | Calculation/trace | run/trace/calculated facts | durable run/trace, input revision, stale completion rejection and authorized retry | BC03 golden vector plus `PlanningCalculationRecoveryAcceptanceTests` 1/1: stale Running run is interrupted and retried; live Running run is rejected | PASS |
| FP15 | Spreading | spread rule/input facts | exact residual allocation on `spread-exact` | Planning HTTP 23/23 and exact-spread conservation coverage | PASS |
| FP16 | Allocations | rule/result with lineage | weighted allocation | Planning HTTP 23/23 weighted-allocation conservation coverage | PASS |
| FP17 | Revenue | revenue driver | volume × price with calculated Revenue coordinates | BC03 golden vector plus Sales owner-provider registration/replay evidence | PASS |
| FP18 | OPEX | category/driver planning lines with account and cost-centre references | fixed, variable, recurring and driver-based lines with growth/inflation calculation; matching generic lines are replaced while unrelated dimensions remain | BC03 golden vector: matching generic line replaced, unrelated Travel line retained; OPEX 1,380 and NetIncome 120 | PASS |
| FP19 | Workforce | workforce lines | add/list/import/cost-impact | Planning HTTP 23/23, BC2-T14 confidentiality, and BC2-T15 real Payroll owner lifecycle | PASS |
| FP20 | CapEx | CapEx lifecycle table with explicit useful life | add/list/prioritize/approve and planning-only depreciation schedule | Planning HTTP 23/23, BC03 statement vector, and BC2-T15 Fixed Assets depreciation/reversal | PASS |
| FP21 | Planning FX | versioned rates | scenario/period conversion for management rollup | Package-C multi-entity rollup 1/1 with explicit period FX and basis | PASS |
| FP22 | Integrated P&L | statement projection table | revenue, COGS, dedicated OPEX, workforce and depreciation lines with explicit basis | BC03 golden-vector PostgreSQL integration 1/1 | PASS |
| FP23 | Working capital | assumptions | DSO/DPO/inventory-days derived AR/AP/inventory | BC03 golden-vector PostgreSQL integration 1/1 | PASS |
| FP24 | Projected balance sheet | statement projection contract | opening cash/equity plus projected assets, liabilities and balance check; incomplete models fail visibly | BC03 golden-vector balance check passes | PASS |
| FP25 | Projected cash flow | statement projection contract | integrated operating, investing and financing cash flows with closing-cash continuity | BC03 golden-vector closing cash reconciles to Balance Sheet cash | PASS |
| FP26 | Rolling forecast | governed Forecast plan versions | actual cutoff creates a new Draft horizon without rewriting source history | Package-C rolling regression 1/1; source unchanged, future periods only, cutoff regression rejected | PASS |
| FP27 | What-if | scenario state/access | clone/share/adopt with owner/access-level checks on plan-version reads and writes | Package-C scenario/access 2/2 plus persisted owner/edit/adopt checks | PASS |
| FP28 | Variance/drivers | variance/comments | query, driver and numeric decomposition endpoints | Package-C forecast/reporting suite 7/7; coordinate IDs, favorability and scoped drill-through asserted | PASS |
| FP29 | Forecast accuracy | accuracy projection | aggregate error/bias | Package-C reproducibility 1/1 with pinned Actual basis | PASS |
| FP30 | Budget policy | policy/buckets, effective dates, horizon and published-version links | annual/quarterly/monthly normalization, validated bucket hints, published-version and legal-entity checks | Budget Control 8/8 plus BC-T09/BC-T10 authenticated control evidence | PASS |
| FP31 | Availability | bucket/reservation ledger and policy basis | source-kind-aware availability, currency checks, negative-budget and shadow-mode handling; reconciled owner snapshots are not double-counted | 8/8 focused PostgreSQL tests plus BC-T10 `40,000/60,000/20,000 → 80,000` owner vector | PASS for BC01 |
| FP32 | Atomic check/reserve | row locks/source uniqueness | owner transaction enlistment, bounded overrides, deterministic correction/reconciliation rebuild | BC-T05 concurrent `80,000 + 80,000` approval and BC-T06 injected rollback pass; omitted-bucket/idempotency/rebuild tests pass | PASS for BC01 |
| FP33 | Purchasing commitments | Planning-owned reservation ledger plus Purchasing-owned requisition/conversion lineage | Requisition draft/submit/approve, partial/full PR→PO conversion, row locks, idempotency and precommitment replacement | Real authenticated disposable PR `100,000 → 40,000 + 60,000` HTTP vector; BC-T05/06 approval and rollback; duplicate and over-limit checks | PASS; physical receipt/return records remain Purchasing/Inventory-owned |
| FP34 | Expenses/commitments | Planning-owned reservation ledger plus durable reversal intent | Expense create/update/finalize/delete/reversal enlists reservation/consumption/correction; failed correction remains retryable | BC-T08 real PostgreSQL failure/retry; intent `Failed → Completed`, one Ledger reversal and one Planning correction | PASS for BC01 |
| FP35 | Commitment→Actual | consumption/correction facts and bucket actual total | posted supplier invoice consumes once; credit note and Expense reversal intent create bounded correction facts | Supplier-invoice/credit correction owner test, BC-T08 recovery, no-reservation fail-closed and correction replay pass | PASS for BC01; wider module lifecycle remains partial |
| FP36 | Overrides/thresholds | source-bound override with used amount, validity and approval link | request/decision API through Platform Approvals; bounded exact-source usage and SoD permission | BC-T09 authenticated requester/approver/unauthorized HTTP journey; bounded source/version usage and retry pass | PASS for BC01 |
| FP37 | Department planner | submission persistence | scoped department planner read model and submission routes | Package-C department read model; BC2-T10 assigned-coordinate isolation and lock 2/2; frontend remains separate | PASS |
| FP38 | CFO/owner views | variance/statements | CFO management read model with period-aware Planning FX and drill-through identity | Package-C CFO/read-model and rollup evidence; owner action permissions are enforced server-side | PASS |
| FP39 | Excel/CSV | bounded CSV import contract | CSV preview/atomic Draft-only commit plus safe first-sheet XLSX preview/commit and formula-safe bounded XLSX export/template | BC03 1/1 covers quoted CSV, malformed workbook rejection, published-version rejection, export and template ZIP evidence | PASS |
| FP40 | Multi-entity rollup | entity keys and Planning FX | authorized entity rows converted through explicit period FX rates | Package-C rollup 1/1; duplicate/nonexistent entity scopes reject and lines carry explicit FX basis | PASS |
| FP41 | Security/SoD | RLS/permissions/lifecycle | route authorization, scenario plan-version access, and Platform Approval owner scope | Architecture 23/23, Security 7/7, BC2-T07/T10/T11/T14/T17 and BC-T09 HTTP evidence | PASS |
| FP42 | Audit/history | audit on implemented writes | lifecycle audit | BC2 governance and BC01 acceptance inspect durable audit/outbox/history identities across writes, approvals, corrections and refreshes | PASS |
| FP43 | Migration/opening budget | bootstrap/forward-fix, owner reconciliation and reconciliation runs | disposable migration/seed, strict Block guard requires successful owner reconciliation, owner-source comparison and bounded rebuild | BC-T10 plus BC2-T14 migration/seed replay | PASS for covered vectors |
| FP44 | Performance/scale | grid/availability indexes | bounded queries | Performance harness passed; seeded grid and budget-control EXPLAIN plans are indexed/bounded and remain sub-millisecond on the disposable seed. This is backend bounded-query evidence, not production-scale certification. | PASS |
| FP45 | Observability | platform logging/outbox | bounded `Todir.Planning` metrics for Actual refresh, calculation, budget checks, failures, block decisions, imports and reconciliation discrepancies; exported through existing OpenTelemetry pipeline | `PlanningMetricsAcceptanceTests` 1/1: authenticated `/internal/metrics` returns Planning counters with bounded dimensions and no tenant/document labels | PASS |
| FP46 | Runbooks/recovery | migration/seed, owner reconciliation and reversal intent | bounded rebuild, durable owner result, failed reversal intent and retry semantics | BC-T06/08 failure recovery, BC-T10 watermark/rebuild, BC2-T03 snapshot recovery, and FP14 calculation restart/retry 1/1 | PASS |
| FP47 | Small company | basic seed budget/forecast | core cycle/grid/control | Development Seed, authenticated grid/control routes, BC01 and BC02 acceptance prove the backend slice; frontend/product acceptance remains separate | PASS |
| FP48 | Department manager | submission backend slice | assignment-scoped submission and delegated approval contracts | BC2-T10 2/2 and BC2-T11 delegation/SoD 2/2; no claim is made for frontend acceptance | PASS |
| FP49 | FP&A | drivers/variance/forecast/scenario backend | forecast, scenario, variance, accuracy and traceable reporting contracts | Package-C 7/7 plus BC03 integrated statements 1/1 | PASS |
| FP50 | CFO | P&L/BS/CF/rollup backend | integrated management statements and CFO read model with FX/coordinate basis | BC03 1/1 and Package-C CFO/rollup evidence; statutory reporting and human product acceptance remain separate | PASS |
| FP51 | Purchasing control | requisition/PO lineage and reservation source identity | Purchasing-owned PR lifecycle, conversion replacement, PO approval history/outbox and server-side bucket resolution | BC-T01–T12, authenticated Purchasing receipt handoff, Inventory receipt/reversal evidence, and Planning reservation/control vectors. Purchasing/Inventory own the broader receipt/return lifecycle; it is not a missing Module 28 Planning behavior. | PASS |
| FP52 | Cross-module reconciliation | owner financial-fact contracts and durable owner result | fourteen registered owner providers, deterministic scoped facts, actual snapshots, source watermark/discrepancy and bucket rebuild | Package-A registration 1/1; owner-provider replay 1/1; BC2-T15 lifecycle 2/2; BC-T10 zero-discrepancy owner reconciliation | PASS |
| FP53 | E2E/release | migration/RLS verified | complete backend delivery evidence and handoff seams | BC-T01–T12, BC02 T01–T17, BC03 1/1, Package-C 7/7, owner 3/3, Planning recovery 1/1, Ledger 56/56 twice, Security 7/7, Architecture 23/23, OpenAPI 91 paths, Release gates 73/73 | PASS |

## BC02 integrity-closure note

The original BC2-T01 through BC2-T17 identities are authoritative in
`BUDGETING_BC02_ACCEPTANCE_MATRIX.md`. The owner-sourced Actual, duplicate
refresh identity, immutable snapshot, mapping lifecycle, governance,
confidentiality, disabled-module and BC01-regression vectors are all recorded
as PASS there. A published plan version pins its Actual snapshot identity;
later refreshes publish a new current snapshot without rewriting the pinned
historical basis.

## 2026-11-28 environment and regression closure evidence

- The four prior Planning HTTP failures caused by missing Development OAuth signing configuration are corrected in `backend/Api/Program.cs`: the host environment supplied by `WebApplicationFactory.UseEnvironment` is copied into both supported configuration keys before Core services are registered. The final Planning integration filter passed 23/23 after the last source change.
- The BC2 actual fixture now establishes transaction-local `app.tenant_id` before inserting journal lines through the Ledger projection trigger. This removed the fixture-only empty-UUID failure without weakening RLS.
- The Development Manufacturing seed now persists the canonical residual `uncovered_qty=1000` for the seeded forecast demand. A separately cloned `module28_mfg_p0_20261128` database passed `ManufacturingP0PostgresAcceptanceTests` 2/2.
- Grid batch, paste-preview, CSV import, and XLSX import routes now authorize every distinct legal entity represented by the request before executing the workflow; a first-cell-only scope cannot authorize a mixed-entity request.
- The local RabbitMQ credentials were restored from the running development service for the actual broker gates. Broker integration passed 2/2, and the Projects Outbox → RabbitMQ → Worker → PostgreSQL Inbox replay passed 1/1 with exactly one applied cost fact and one processed Inbox message.
- `PlanningMetricsAcceptanceTests` exercised the authenticated `/internal/metrics` boundary after generating Planning block/failure observations. The response returned 200, included Planning counters, and excluded tenant/document identifiers from metric labels.

## Release consistency decision

The applicable Module 28 backend requirements FP00–FP53 are PASS with the evidence
listed above. Purchasing/Inventory own the physical receipt and supplier-return
workflow; Module 28 owns the commitment/reservation boundary and does not claim
to own or duplicate those records. Frontend implementation, Product/UX
acceptance, statutory reporting and human approval remain separate gates.

## Verified commands

- `dotnet build backend/Backend.sln --configuration Release --no-restore` — PASS after the latest source change, 0 warnings/0 errors.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Planning"` — PASS, 23/23 after the final multi-entity lifecycle guard change.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BrokerIntegrationTests"` — PASS, 2/2 against local RabbitMQ.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RealProjectsSourceEventFlowsThroughLocalWorkerAndInboxExactlyOnce"` — PASS, 1/1 against local RabbitMQ, Worker and disposable PostgreSQL.
- `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-restore` — PASS, 6/6 after the final source integration edit.
- `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --configuration Release --no-restore` — PASS, 23/23 after the final source integration edit.
- `node scripts/architecture/validate-openapi.mjs` — PASS; Planning contract validated with 91 paths and the eight existing contracts also passed.
- `dotnet test backend/Tests/Security/SecurityTests.csproj --configuration Release --no-restore` with disposable admin connection — PASS, 7/7.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-build --filter FullyQualifiedName~PlanningBudgetControlIntegrationTests` — PASS, 8/8 on disposable PostgreSQL; omitted-bucket strict control, partial consumption/correction idempotency, bounded source-specific override use, expense owner lifecycle, and projection reconciliation/rebuild were asserted.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApiCompositionTests"` with Development signing configuration, the isolated runtime-role connection and seed password — PASS, 4/4.
- Real disposable Purchasing HTTP PR vector — PASS: `100,000 EGP` approved, converted to approved `40,000` and `60,000` POs, duplicate conversion idempotent, over-limit conversion rejected, and reservation ledger inspected.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RealSupplierInvoiceActualReconcilesProjectCommitment"` — PASS, 1/1 after the source-reservation guard correction.
- `dotnet format` targeted changed C# files with `--verify-no-changes` — PASS after correcting the requested whitespace-only `LedgerRepository.cs` finding. Full unrelated solution findings are not treated as BC01 evidence.
- Disposable PostgreSQL migration and Development Seed replay — PASS on the recovery database; 286 ordered SQL files, PR/reversal-intent/owner-reconciliation tables, RLS/grants, and Atlas/Delta seed completed.
- Real cookie/CSRF/PostgreSQL Purchasing journey — PR conversion and approval authorization passed on the isolated runtime role. Physical receipt and supplier-return ownership remains with Purchasing/Inventory; the Module 28 Planning commitment boundary is covered by BC-T01–T12 and the receipt handoff evidence.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ConcurrentCompetingPurchasingApprovalsConserveOneStrictCommitment|FullyQualifiedName~PurchasingReservationRollsBackWithOwnerTransactionAndRetrySucceedsOnce|FullyQualifiedName~CanonicalOwnerReconciliationAgreesWithRealSourceWorkflows"` — PASS for the BC-T05/06/10 focused integration vectors on disposable PostgreSQL; BC-T05 and BC-T06 passed twice sequentially on the repeat-safe fixture, and BC-T10 observed `40,000 EGP` Actual, `60,000 EGP` Commitment, `20,000 EGP` Precommitment and `80,000 EGP` availability after authenticated reconciliation/rebuild using a unique future year.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PlanningBudgetControlHttpAcceptanceTests"` — PASS for the authenticated BC-T09 requester/approver/unauthorized HTTP journey with cookie/CSRF and PostgreSQL inspection.
- `dotnet test backend/Tests/Modules/Expenses/ExpensesTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BudgetReversalRecoversAfterLedgerCommitBeforePlanningCorrection|FullyQualifiedName~BudgetReversalLedgerFailureLeavesRetryableIntent"` — PASS 2/2 for the durable BC-T08 failure/retry paths; the strict opening/omitted-bucket boundary is included in the separate Planning Budget Control 8/8 run above.
- `dotnet test backend/Tests/Modules/Ledger/LedgerTests.csproj --configuration Release --no-build` — PASS `56/56`, repeated twice on disposable PostgreSQL after the fixture isolation correction.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "DisplayName~BC2-T01"` — PASS, 1/1; owner-sourced immutable Actual snapshot, replay idempotency and caller-fact rejection.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "DisplayName~BC2-T01|DisplayName~BC2-T02|DisplayName~BC2-T03"` — PASS, 4/4 after the final owner-source change; canonical source-only refresh, duplicate operational/Ledger identity collapse, durable source failure and corrected restatement.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-restore --filter "DisplayName~BC2-T15"` — PASS, 1/1 after the final owner-source change; real Payroll finalization, Fixed Assets depreciation/reversal, Expenses facts, Ledger identity matching, Planning replay idempotency and zero-net depreciation correction on disposable PostgreSQL.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "DisplayName~BC2-T01|DisplayName~BC2-T02|DisplayName~BC2-T03"` — PASS, 4/4 on the disposable PostgreSQL fixture after the owner-source change.
- `dotnet test backend/Tests/SeedData/SeedDataTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DevelopmentSeedRunsTwiceOnFreshDatabaseWithForcedRls"` — PASS, 1/1 on local disposable PostgreSQL.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "DisplayName~BC-T09"` — PASS, 1/1 authenticated Planning HTTP budget approval.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "DisplayName~BC-T10"` — PASS, 1/1 canonical owner reconciliation after correcting grouped Ledger debit/credit semantics.
- `dotnet test backend/Tests/Integration/IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PlanningCalculationRecoveryAcceptanceTests"` — PASS, 1/1 after correcting the calculation-run insert to persist `plan_version_id`; stale worker runs are marked interrupted and retried, while live runs remain non-retryable.
