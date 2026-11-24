# 01 — Product Requirements and User Journeys

**Language:** Arabic-first for Egyptian businesses; natural terms, not literal technology translations. **Status:** USER-LOCKED experience + proposed refinement requiring acceptance.

## Problem and success criteria

ToDir already records and executes operational/financial transactions. The new feature lets an authorized manager **request an analysis of selected business facts**, examine results and evidence, see **business decision recommendations**, review them, and return to immutable historical run records. It must not distract the user or impair daily work.

Success is **not** number of charts or use of LLM. Success = trustworthy answers to operational decisions, appropriate scope, explainability, repeatable history, useful options, measurable impact, fast day-to-day ERP.

## User personas and their tasks

| Persona | Real questions | Needs and constraints |
|---|---|---|
| Company Owner / GM | Which areas are deteriorating? What options do I have? | Multi-domain summary of **saved authorized runs** only; no implicit computation; avoid financial jargon without explanation |
| Collections Manager | Which overdue customers require attention and why? | AR/paid vs noncash separation, scope by entity/team, drill to invoices, partial payments, proposed follow-up action |
| Sales Manager | Which products/branches/customers underperform during selected dates? | Revenue basis clearly named (orders / issued invoices / posted invoices), returns, taxes, currency |
| Inventory Manager | Which products show risk of shortage or overstock? | Warehouse, product, UOM, history/cutoff, reservations and inbound commitments if actually supported |
| Procurement Manager | Where are supplier delays or price deviations? | Supplier/PO/timeframe, approved vs received, currency/units |
| Finance Manager | What changed in cash exposure or working capital? | Legal entity, FX policy, ledger/subledger source and cutoff, no fake bank confirmations |
| Production Manager | Which completed orders had cost/quantity/capacity deviations? | Operational definitions and production evidence; do not mistake MRP planned data for actual |

No automatic suggestion execution; a recommendation may not exist when data is missing or analysis does not justify one.

## Core experience — 3 independent user operations

### Journey J-01: request new analysis

1. Authorized manager enters **the module's dedicated analysis page** (not its operational list).
2. GET obtains only **available analysis definitions, prior saved runs and permissions**. This must not start computation.
3. Manager selects analysis type (business label), legal entity/branch within effective scope, **date interval**, suitable filters/dimensions, comparison/metric basis if meaningful.
4. Client validates form; server repeats validation, checks authorization and entitlement; may return no-run if data availability prerequisite not met.
5. Manager confirms and clicks **«بدء التحليل»**. This is the **only permitted run-creation trigger** in v1 (a genuine authenticated POST). Request contains idempotency key.
6. Server creates durable job and returns run ID immediately; frontend shows Queued / Running / Succeeded / Failed / Canceled.
7. Results, warnings and any justified recommendations are saved and published atomically. Manager can leave page.
8. No automatic rerun or implicit update when open page, refresh, switch tenant, or view a prior run.

### Journey J-02: review past results

- Manager searches saved runs by period/filter/creator/status/type within authorized scope, sorted with cursor-based pagination.
- Opens run details and sees business period, scope, options, **data cutoff + job execution time**, input revision, metrics, quality warnings, recommendations and decisions; filter criteria may be read-only in details.
- Historical records remain versioned and **not silently overwritten**. If data changed, old run may display `بيانات أحدث متاحة` or `نتيجة قديمة` without editing original results. A new run requires a new explicit click.
- Access to old results is always re-evaluated against current permissions/tenant/entity/branch, not just the access at creation.

### Journey J-03: review recommendation and action

- Review recommendation tied to a specific run; see source facts, assumptions, options, expected benefit/risk, uncertainty, conflicts and freshness.
- Authorized person records `اعتماد التوصية للمراجعة التشغيلية`, `رفض`, or `تحتاج دراسة` with reason. In v1 this is not equivalent to executing a business transaction.
- If later enabling action handoff, module owner **re-validates current business state and current permissions**, then opens owner workflow/Draft only via approved command. Actual final approval remains within owner domain.
- Save follow-up and observed outcome separately; comparison does not rewrite historical recommendation.

## Explicit non-goals for v1

- No analysis when user opens or refreshes any page, **even the analysis page**.
- No scheduled/nightly/event-created analysis runs.
- No LLM interpretation of raw SQL or direct reads/edits to accounting tables.
- No auto-generated credit restriction, stock transfer, purchase order, journal, bank settlement, payroll change or production release.
- No user-managed arbitrary SQL/Python/rule scripting.
- No per-module independent AI engines, ACL engines or custom sidebars.
- No enterprise-wide all-modules rollout in a single release; no claim of statistically valid forecasting until real data and backtesting.
- No mandated Python service, model training pipeline, vector DB, DWH, microservices or GPU.

## Functional requirements (IDs are implementation-traceable)

| ID | Priority | Requirement / rationale |
|---|---|---|
| PR-001 | MUST | Dedicated business-named route inside each **enabled** module with a real approved use case; never embed analysis runtime inside daily operation routes |
| PR-002 | MUST | Reading analysis page, changing filters, reading history causes **zero AnalysisRun creation and zero compute jobs** |
| PR-003 | MUST | Explicit authorized POST + idempotency is the only v1 analysis trigger |
| PR-004 | MUST | Validated typed filters per analysis, start/end dates, legal entity scope, relevant dimensions and a clearly stated metric/time basis |
| PR-005 | MUST | Server enforces independent read/run/recommendation-review privileges, module availability and record/scope restrictions |
| PR-006 | MUST | Durable run status/progress and recovery; jobs survive user leaving UI |
| PR-007 | MUST | Historical result integrity: run input/scope/cutoff/model version/results/evidence visible and immutable after publish |
| PR-008 | MUST | Recommendations reference exact run and evidence, no unsupported claims or fictitious confidence |
| PR-009 | MUST | No automatic financial/stock/legal write or approval from the analysis engine |
| PR-010 | MUST | User can filter/paginate history and inspect exact prior output, including failed run metadata without sensitive traces |
| PR-011 | MUST | No regression of daily operational SLOs under mixed workloads |
| PR-012 | MUST | Data insufficiency yields explicit `لا توجد بيانات كافية` / partial result with warnings, not hallucinated totals |
| PR-013 | MUST | Existing shared frontend components, Arabic RTL, English parity, navigation/effective-access conventions |
| PR-014 | SHOULD | Explain why recommendation made, alternative action and possible downside; may expose `not enough evidence` |
| PR-015 | SHOULD | Scope-preserving export of completed run with provenance; prohibit historical result mutation |
| PR-016 | FUTURE | Cross-module company-wide saved-results view, only after ownership/permissions/tenant scope are settled |

## Product decision gates before release

- Does each selected analysis answer a measurable manager decision? If not, do not build it.
- Are source facts and date semantics documented and testable?
- Are output warnings and recommended actions natural to Egyptian business practice?
- Can the original source of every critical financial number be traced to owner modules?
- Can users with narrow access discover only their authorized analysis route and nothing else?
- Under large data, can run be canceled or queued without blocking normal transactions?

## First release recommendation

Candidate = «تحليل تحصيلات العملاء» **Pilot 0 descriptive/decision hygiene**. Not commercially committed: pass Data Readiness + market-fit gate before locking the pilot. If data truth fails, compare Inventory or Sales alternatives without changing the shared foundation architecture.
