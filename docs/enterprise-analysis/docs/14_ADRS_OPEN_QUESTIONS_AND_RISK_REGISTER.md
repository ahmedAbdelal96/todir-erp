# 14 — Architecture Decision Records, Open Questions & Risk Register

**Status vocabulary:** `USER-LOCKED` (explicit product rule); `PROPOSED-ADR` (recommended pending code/metrics approval); `TO-VERIFY`; `BLOCKED` (cannot proceed until a named prerequisite is satisfied); `DEFERRED` (intentionally outside v1); `FUTURE`.

## Existing binding platform decisions

- Modular Monolith, single writer/source of truth, no direct business module implementation imports or cross-schema writes.
- L6 scale/intelligence, L5 platform reporting and AI Gateway, versioned capabilities, contract-only cross-module integration.
- Tenant nonbypass isolation and current entitlement/activation/access model; navigation permission-driven.
- Source owner controls financial truth and statutory actuals; AI cannot directly access/modify DB.

## Decision register (ready to turn into ADR files after P0)

### ADR-AN-001 — Location of new capability

**Decision proposed, currently BLOCKED:** analysis orchestration/results as a bounded capability, with owner facts via contracts and an independently deployed .NET process role. The current repository has no Analysis module, no L6 runtime manifest example, and an explicit single `backend/Worker` composition. **Why:** process isolation and ownership; avoid duplicating each module's AI. **Reject:** per-module engines, embedded compute in write paths, independent ERP microservice. **Required before P2:** approve whether this is a platform-owned capability in the `platform` schema or a new formally registered module with a new schema/lifecycle/entitlement contract; prove the Worker process-role or host/deployment shape with the solution-boundary and architecture validators.

### ADR-AN-002 — User-triggered execution policy

**Decision:** `USER-LOCKED` user `POST` only; no scheduled, event-triggered, on-page-open analyses in v1. **Reason:** user's explicit control, predictable load, historical run semantics. **Nuance:** maintenance scheduler/polling processes already submitted jobs; this is not permission to create new business runs.

### ADR-AN-003 — Read contracts and projection ownership

**Decision proposed:** Subledger owns AR authoritative facts, Payments owns payment states, Banking owns reconciliation; Analysis owns cross-owner **derived** report/evidence snapshots only. **Why:** truth accuracy, no duplicate source ownership. **Verify:** owner APIs/capabilities, contract shape, efficient pagination, actual data quality and read-model performance.

### ADR-AN-004 — Job transport and runtime isolation

**Decision proposed:** PostgreSQL durable job+lease store with **separate bounded .NET worker process**, no new RabbitMQ analytic consumer or Python service at day one. **Reason:** existing infrastructure, no fanout/extra broker burden, testable isolation. **Fallback if performance fails:** separate job queue/DB replica; implement only after benchmark. **Verify:** RLS safe cross-tenant dispatcher, pool sizing, lease fencing, fairness, operational p95/p99 under load.

### ADR-AN-005 — Persistence and immutability

**Decision proposed:** typed run/status tables + versioned result/evidence + append review/outcome, strictly immutable published output. **Why:** auditability and reproducibility. **Verify:** retention, data size, encryption, storage layout, migrations; distinguish immutable result from separately mutable freshness label.

### ADR-AN-006 — Canonical financial ownership

**Decision proposed:** Analysis never owns official Banking forecast, FP&A budgets or Accounting posting. Output labeled scenario/analysis/recommendation; owner workflow required for business action. **Why:** legal/financial audit and spec conflicts. **Verify:** current Banking and FP&A future contract semantics.

### ADR-AN-007 — Permissions and commercial entitlement

**Decision proposed:** separate run/read/recommendation-review rights in module manifest, existing access bootstrap, existing subscription infrastructure. **TO-VERIFY:** whether analysis add-on sold separately or included; retention and quotas. **Never:** new hidden AI subscription per module.

### ADR-AN-008 — Python or ML admission

**Decision:** do not deploy Python or ML without validated business advantage, backtests, privacy/performance budget and ADR. **Reason:** long-term maintainability, evidence-first. **Preserve:** language-agnostic versioned algorithm request/response shape.

### ADR-AN-009 — Pilot domain

**Decision proposed:** Collections Hygiene candidate, provided real data/source truth/market fit. **Alternative:** Inventory or Sales descriptive pilot if Collection Data Readiness fails. **Not locked:** until data and buyer evidence.

### ADR-AN-010 — UI routes and terminology

**Decision:** analysis pages are independent within the relevant module, user-language labels, explicit filters and create button, history read-only. **No** auto-analysis triggers or injected cards in daily operational pages; any future contextual surfacing requires separate product change approval.

## Open questions blocking coding

| ID | Question | Proposed owner | Required evidence |
|---|---|---|---|
| Q-01 | What precisely constitutes cash received per payment method and bank reconciliation state? | Payments + Banking + finance SME | code review, workflow cases, data samples |
| Q-02 | Can source owner build portfolio-wide historical facts without long locks or N+1? | Subledger + DBA | SQL EXPLAIN, actual dataset size, contract |
| Q-03 | What is valid-time + knowledge-time meaning for each source event? | Finance owner + data architect | temporal fixtures and history comparisons |
| Q-04 | What are measured acceptable operational p95/p99 SLO and regression tolerance? | Performance + product owner | P0 baseline and load profile |
| Q-05 | How are entitlements/quota/retention priced and activated? | Product owner | current packaging and commercial research |
| Q-06 | Which user can read results created by another manager, same branch/entity? | Security + product | role/record permission matrix |
| Q-07 | Does user need global executive summary in v1? | Product | use case validation; none added by default |
| Q-08 | Which filters and exact period basis are mandatory for pilot? | Business owner | data definitions and Arabic UX testing |
| Q-09 | How to keep historical source/evidence accessible vs privacy deletion? | Security + legal | retention/compliance decision per country |
| Q-10 | Can dedicated worker be hosted independent of operational Worker with current deploy pipeline? | DevOps + backend | non-prod deploy test, resource limits |
| Q-11 | Which controls allow new run when analysis paused? | Operations + product | kill switch operational runbook |
| Q-12 | Which domain produces action follow-up and approves it? | Owner module | existing workflow and authority checks |

## Prioritized risk register

| ID | Risk | Severity | Mitigation / proof gate |
|---|---|---|---|
| R-01 | Incorrect cash vs credit/writeoff interpretation | **Critical** | Per-method taxonomy approved; accounting reconciliation and temporal fixtures |
| R-02 | Analysis query impacts live writes | **Critical** | Separate worker + DB pool + owner projection + mixed-load regression; pause on regression |
| R-03 | Tenant/entity/branch data leak through saved result or worker | **Critical** | RLS FORCE, scoped contract, worker dispatcher security and current access checks |
| R-04 | Backdated facts leak future knowledge into historical evaluation | High | As-of/known-at rules, controlled replay, independent historic report verification |
| R-05 | Duplicate or partially published result after retries | High | Job lease fencing, idempotency, atomic publish, crash injection |
| R-06 | Shadow official Forecast/Actual in Analysis | High | Owner-canonical mapping, labels and explicit FP&A/Banking ADR |
| R-07 | Reports mislabeled as AI or nonactionable recommendations | High | Real business questions, data quality gate, evidence/impact and pilot customer feedback |
| R-08 | Exploding result/evidence storage and DB I/O | High | Size/retention budget, bounded artifacts, indexed pagination, monitored growth |
| R-09 | User loses access but historical result remains visible | High | Revalidate current effective access, cache invalidation, evidence read guard |
| R-10 | Analyst claims zero runtime overhead without measurement | High | P0 baseline, B01–B12 benchmark matrix, code+env evidence |
| R-11 | Agent builds all modules at once based on catalog | High | Catalog candidates vs committed stages, DoR and per-module ownership gates |
| R-12 | Python/infra premature addition | Medium | measured trigger/ADR and comparable benchmarks only |
| R-13 | Overengineering of generic analysis schema/hypercube | Medium | typed definitions and JSONB only for bounded results, pilot-first contract testing |
| R-14 | Inaccurate Arabic names/filter semantics | Medium | SME UX review, clear business date definitions |
| R-15 | Legacy code drift from pinned SHA | High | Always diff latest origin/dev, re-evaluate conflicts and re-run affected tests |

## Decision authority

Product owner decides business value and workflow changes, with design and engineering challenge. Finance owners decide official calculation semantics, security defines access, performance engineering certifies SLO baselines, architecture owner approves ADR/module graph; no agent can silently override these responsibilities. Unresolved **Critical** risks block implementation of affected slice, not discovery or documentation.
# P0 evidence supplement

The current risk register and P1 decision brief are maintained in [`P0-06_ARCHITECTURE_RISK_REGISTER.md`](../evidence/p0/P0-06_ARCHITECTURE_RISK_REGISTER.md) and [`P0-08_P1_DECISION_BRIEF.md`](../evidence/p0/P0-08_P1_DECISION_BRIEF.md).

