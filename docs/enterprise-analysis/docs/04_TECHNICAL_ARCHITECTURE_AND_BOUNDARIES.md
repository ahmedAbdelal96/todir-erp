# 04 — Technical Architecture, Ownership & Integration Boundaries

> **Current P1 reconciliation:** The historical proposal below is retained as design history. The current recommendation is a formally registered L4 `analysis` business module using the existing manifest/lifecycle/entitlement conventions, not an assumed L6 runtime layer. See [ARCHITECTURE_APPROVAL_BRIEF.md](../ARCHITECTURE_APPROVAL_BRIEF.md) and [CURRENT_STATE_RECONCILIATION.md](../CURRENT_STATE_RECONCILIATION.md).

**Architecture recommendation:** Incremental .NET-first Analysis capability inside the current Modular Monolith, with an independently resource-bounded compute **process role** for demanding work, not a second ERP or microservice network. **Status:** PROPOSED-ADR, except existing boundaries called out as verified. The current repository has no Analysis module, no L6 runtime manifest example, and no approved second worker host; those are compatibility gates, not implementation assumptions.

## 1. Nonnegotiable invariants

1. Daily operation API call graph **must not call Analysis** (synchronous or wait-on-queue) and must not perform new analytics calculations or heavy projection writes in the same financial transaction.
2. An analysis run begins **only** through an explicit authenticated, authorized request containing validated filters. Neither events nor schedules may create analysis runs in v1.
3. No Analysis-owned SQL over `business_sales`, `finance_subledger`, `finance_payments`, `finance_banking` or other module schemas by default; owner-provided capability/read projection is the entry point. No cross-module SQL writes. `AI`/LLM has no raw SQL access whatsoever.
4. Every operation is tenant-scoped with Legal Entity/Branch/record scope, entitlement and current permissions; service credentials are not blanket bypasses.
5. Financial truth, posting/stock movement, order approval, employee/payroll and other domain state transitions stay with original owner modules.
6. Historical published results immutable; corrections require a new run/version, not rewriting source/evidence/results.
7. New compute workloads isolate process CPU/RAM and DB pool; baseline comparison and real tests required before go-live.
8. Existing architecture validators remain enabled. All module integration uses published contracts/capabilities/events.

## 2. Proposed component view

```text
React/TypeScript — existing shell/navigation/permission runtime
  /sales/analysis   /inventory/analysis   /subledger/collections-analysis
        | GET saved runs/results                    | POST user-initiated request only
        v                                            v
   .NET API (existing authentication, effective access, tenant and scope guards)
        |                                            |
        v                                            v
   Analysis read services                      AnalysisRequestService
        |                                      validate typed filters,
        |                                      permission+entitlement,
        |                                      limits and idempotency
        |                                            |
        v                                            v
   Analysis-owned schema                         Durable Job Store
   (runs, results,                                (new run only)
    recommendations,                                    |
    evidence, reviews)                                  |
                                                          v
                                              Independently deployed .NET process role
                                              (bounded CPU/RAM/pool/parallelism)
                                                          |
                                               approved owner Read Capabilities
                                            / bounded owner-built projections
                                                          |
                                               deterministic calculations / rules
                                                          |
                                            validate + publish run results atomically
                                                          |
                                                          v
                                              Analysis-owned history & evidence

Existing Sales/Payments/Inventory/Accounting transaction paths remain unchanged.
```

This is a logical flow, **not a claim that such hosts and tables already exist**. A reader should not mistake independent runtime host for a microservice with an independent business database and identity stack.

## 3. Domain separation and source-of-truth map

| Concern | Canonical owner | Analysis may do | Analysis must not do |
|---|---|---|---|
| Sales orders, invoices, refunds | Sales / Invoicing | Consume approved scoped facts | Recompute official invoice totals or alter state |
| AR/AP item and allocation history | Subledger | Aggregate validated fact exports | Write `party_open_item`, alter settlement date |
| Money movement and payment status | Payments | Explain receipt/payment patterns | Declare bank cash received solely from `application_date` |
| Bank/cash evidence and forecast versions | Banking | Analyze confidence and trends | Create rival official treasury forecast |
| P&L, TB, accounting periods | Accounting/Ledger | Read reconciled period facts | Re-post accounting entries, independently calculate tax |
| Stock movement/availability/valuation | Inventory | Analyze approved snapshots | Reserve, transfer or mutate inventory without owner workflow |
| MRP and production | Manufacturing | Analyze completed actual/planning versions distinctly | Run a replacement MRP engine or treat planned orders as actual |
| Budget, Actual, Forecast, Scenario | Future FP&A owner | Explain/report approved inputs; propose scenarios | Publish Analysis result as approved FP&A Forecast |
| Analysis job, derived result, recommendation, review | New Analysis capability | Own these artifacts | Become source of canonical ERP transactions |

## 4. Source contract integration design

Existing module implementation projects must not import other implementations. For new demand, use an owner-owned capability such as **illustrative** `ICollectionsAnalysisFactsReadCapability` in `backend/Shared/Contracts`, implemented in Subledger; the consumer obtains it via existing DI/module registration (verify actual contract pattern first). For cross-domain snapshots, the Analysis orchestrator requests **independent owner facts** with common authorization scope, cutoff and version, then joins *records identified by approved opaque references*, **never SQL joins across owner tables**.

**Read contract shape:** `Scope {tenant, legal_entity, optional branch/record}`, `BusinessPeriod / AsOf / KnownAt`, `Criteria`, `PageLimit + Cursor`, `SchemaVersion`, `SourceWatermark`, `CompletenessWarnings`, `Facts[]`. No raw entity models or arbitrary SQL expressions. Owner validates inputs and tenant scope. A module may materialize its own read model to make this efficient; Analysis stores only bounded/protected derived data required by each run.

If reading entire data set requires 1000 paginated calls, create a purpose-designed **owner-scope, bounded export/projection** after benchmarks. Do not loop per party (`N+1`) or return unbounded lists.

## 5. Component packaging — proposed

```text
backend/
  Modules/
    Analysis/                # proposed L6 capability; manifest and contracts review required
      Controllers/           # only if existing module pattern requires public route controllers
      Services/
      Repositories/
      Rules/
      Models/
      module.manifest.json
    Subledger/              # optional owner-scoped analytic read adapter ONLY
    Payments/               # optional owner-scoped analytic read adapter ONLY
    ...existing modules...
  Shared/Contracts/
    AnalysisContracts.cs    # candidate; keep contracts small/versioned
    SubledgerAnalysisReadContracts.cs
    Worker/                   # existing host; analysis-only process role is a compatibility option
      Program.cs              # no second composition without an approved host ADR
  Tests/
    Analysis...             # unit, integration, security, performance and E2E
frontend/
  modules/sales/            # add one independently permitted route/page using existing patterns
  modules/subledger/       # collections analysis route when pilot approved
  components/              # reusable analysis form / history / evidence UI, if genuinely common
  app/router/              # destination registry; no new sidebar or routing layer
docs/
  ...this design pack...
```

**Do not create these files just because a diagram lists them**. The current runtime has manifests only for L1–L4 modules and the API/Worker composition is explicit; it does not prove that a new L6 module or a new host can be registered safely. Before P2, choose and approve one boundary: a platform-owned capability consistent with the existing `platform` schema, or a formally added Analysis module with manifest/schema/lifecycle/entitlement/deployment support. Reuse the current subscription system. The technical internal module name is a proposal and is not the label shown to end users.

## 6. Explicit alternatives considered

| Option | Benefit | Cost/Failure mode | Recommendation |
|---|---|---|---|
| Put analysis loops into existing Sales/Inventory transaction service | Fewest files | Latency coupling; no resource isolation; risk of violating no-regression requirement | **Reject** |
| Duplicate each module's calculations in an AI subsystem | Fast prototype | Conflicting financial truth and hidden divergence | **Reject** |
| Direct read-only SQL from Analysis across all module schemas | Easy queries | Breaks ownership, RLS ambiguity, schema upgrade coupling | **Reject** |
| One new Python microservice immediately | ML ecosystem | Runtime/deployment/privacy/observability cost before need | **Defer** |
| .NET module + jobs inside operational Worker process | Simple deployment | Same CPU/RAM/pool; noisy neighbor | **Reject for heavy jobs** |
| .NET capability + independently deployed process role + owner read contracts | Single codebase, compatible contracts, controllable resource isolation | Requires proof that the current Worker composition can be split safely, DB still shared | **Recommend conditionally** |
| Independent queue/broker dedicated day 1 | Supports high throughput | Added operational complexity; duplicate messaging semantics | **Defer until benchmark** |

## 7. Availability and graceful degradation

- If analysis host down, operational APIs remain available. Queued analysis runs stay queued or time out with clear state, no blocked invoice/payment/stock operations.
- If run store inaccessible, **only** dedicated analysis pages degrade; no mandatory startup dependency in Sales/Payments host beyond standard module registration.
- Analysis schema migrations must be backward compatible with existing platform; forward-only migrations, safe rollout order, no destructive data operations.
- If data export slow, use bounded execution, `statement_timeout`, budgeted pool, cancel and defer instead of starving transactional load.
- If data quality insufficient, publish `InsufficientData` result with evidence, or a failed validation run as specified—not imaginary forecasts.

## 8. Python-ready boundary (not implemented in v1)

Introduce a **versioned algorithm interface** with deterministic input facts and result schema independent of programming language. .NET v1 runs deterministic algorithms. When a pilot demands statistical training/optimization, compare .NET libraries and Python against baseline correctness, performance, cost and maintenance, using a versioned job payload and egress/secret/privacy controls. Python **never becomes a second ERP owner**.

## 9. Where architecture decisions must be recorded

ADR-A: Layer/module boundary and host. ADR-B: owner facts/read projection. ADR-C: queue vs DB claim, separate CPU/DB pools. ADR-D: historical temporal semantics. ADR-E: entitlement/navigation. See doc 14. Existing approved Master Blueprint wins until amendment is formally approved.
