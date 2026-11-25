# 13 — Implementation Roadmap, Dependencies, Deliverables & Acceptance

**Process:** Technical design before implementation, measurable gates per phase, `dev` branch only, no blanket "complete" from agent report. **No fixed calendar estimates**: dates depend on baseline, actual code complexity and reproducible tests. Work is staged, but an implementation stage owns its slice end-to-end.

## Phase P0 — Repository Discovery and Operational Baseline (NO CODE CHANGE)

**Inputs:** latest `origin/dev`, current architecture/docs, owned services and DB, current frontend navigation/effective access, actual CI/local verification patterns.

**Tasks:**
1. Read root/backend/frontend `AGENTS.md`, current master architecture/navigation/permission/messaging/platform documents, owners' module packs.
2. Pin SHA and diff against this design pack's SHA; map existing APIs/business functions/DTOs/RLS/triggers/indexes/events/worker/host boundaries.
3. Generate source ownership map and definition of acceptable read capabilities for collections, sales and inventory reference use cases.
4. Run operational **baseline** on controlled realistic data; record p50/p95/p99, DB, outbox and relevant paths. If environment missing, record **NOT MEASURED** and setup steps; do not fabricate results.
5. Verify user roles and available effective access, tenant/legal entity/branch handling and enrollment/entitlement activation rules.
6. Read Claude static report and validate its date/messaging claims against the **current** code (not older `d07dd58`).
7. Produce gap/impact matrix including any likely existing critical performance blockers; do not implement unrelated fixes.

**Deliverables:** `CURRENT_STATE_EVIDENCE.md`, `BASELINE_METRICS.md` + scripts/artifacts, `SOURCE_OWNERSHIP_MATRIX.md`, changed-source register, risk gate and architecture questions. **Exit:** critical flows benchmarked or explicitly BLOCKED; no undocumented source truth assumptions.

## Phase P1 — Product & Architecture Approval (NO IMPLEMENTATION)

**Tasks:**
1. Validate natural Arabic page labels and two personas for pilot; finalize typed filter definitions & date semantics.
2. Sign off architecture ADRs in doc 14; decide module scope, worker isolation, DB job claiming, history storage, quota, permissions, subscription semantics.
3. Identify exactly which existing owner capabilities can be reused and what **new read-only public contracts** are necessary; use dependency graph/architecture fixture to prove feasibility.
4. Prepare SQL migration design (not execute), OpenAPI DTOs, frontend route registry addition, state machines, results and recommendation policy.
5. Set measurable SLO/performance budgets from P0 and runbook; choose security, data-readiness and failure gates.
6. Review product value evidence for Collections vs Sales/Inventory; select pilot or document that decision is BLOCKED pending data.

**Deliverables:** approved ADRs, signed spec per analysis, API/schema contract, detailed acceptance tests, test dataset plan, handoff plan. **Exit:** no unresolved P0 blocker or false completion claim.

## Phase P2 — Shared .NET Analysis Foundation (END-TO-END SLICE)

**Tasks:**
1. Add new bounded capability/manifest according to approved architecture, not unrestricted platform kernel expansion.
2. Create Analysis-owned schema, FORCE RLS, job lease/retry/fencing, idempotency, results/history/evidence/review base and audit integration.
3. Implement effective-access guarded APIs; **GET read only**; `POST` creates user-requested run; strict filter schemas and scope.
4. Prove an independently deployed .NET analysis process role (or an independently configured existing Worker role proven equivalent); limits, kill switch, telemetry, cancellation/recovery. Do not create a new host/project until the P1 boundary and deployment ADR are approved.
5. Add minimal shared frontend components and domain route *for pilot only*, preserving current navigation and styling.
6. Include deterministic fixture engine **without reading cross-module tables directly** and test one typed read capability.

**Acceptance:** approved contracts/build/architecture/RLS/migrations/HTTP/UI, no unexpected run creation, failure recovery, no operational regression beyond approved SLO, one full flow from click → queued → saved result → history and recommendation review.

## Phase P3 — Collections Pilot 0 (CONDITIONAL)

**Prerequisite:** Collections Data Truth/Readiness audit approved. If not, select alternative with its own owner-fact contract.

**Tasks:** owner Subledger/Payments/Banking analytical read capabilities/projection, temporal and cash classification rules, deterministic metrics and recommendations, Arabic UI and typed filters, historical snapshots/provenance/quality, human review and outcome tracking.

**Acceptance:** reconcile actuals and reversals with owner module reports, all pilot fixtures, real-stack tests, multi-tenant RLS, data quality warnings, worker noise test, user verification. No ML claims.

## Phase P4 — Hardening and Release Candidate

- Replay clean and incremental DB upgrades; backup/restore; performance mixed-load/stress; rights/entitlement transitions; cancellation/retry/dead worker; huge history pagination; corrupted/stale input detection.
- Validate Arabic & English, RTL/LTR, accessibility, no hidden unauthorized destination, no operational API drift.
- Rollout canary and kill switch, monitor critical ERP SLO and outbox, keep deploy rollback and remediation runbook.
- Independent acceptance sign-off: product, finance domain owner, security, database, frontend, performance. Human UX approval required where visuals involved.

## Phase P5 — Catalog expansion by validated value

Promote one approved domain at a time using **the same** shared job/results/recommendation engine; no copied AI services. Candidates Sales/Inventory first, then Purchasing/Banking/Accounting and later Manufacturing/HR/Projects where validated. Every domain has separate source-fact accuracy/performance gate and UX review. No all-modules big-bang release.

## Phase P6 — Predictive/Optimization maturity (future proof, not speculative sprint)

Add statistical prediction or mathematical optimization only when specific decision improvement has measurable target, historical data/time leakage checks, out-of-sample gain over deterministic baseline, economic benefit, explainability and operational cost justification. Choose Python only by comparative evidence/ADR; preserve same analysis contract and job/result schema.

## Task card standard — every coding-agent assignment

| Field | Mandatory content |
|---|---|
| Task ID/title | Unique ID and use case, never vague "build AI" |
| Current code SHA | exact `dev` baseline, generated source diffs |
| Owner module | business owner; published capabilities required |
| Input/output contract | typed API, filters, scope, page/cursor, metrics/evidence and states |
| Forbidden changes | no operational path slowdown; no direct cross-schema reads/writes; no hidden auto-trigger |
| Files allowed | task-owned code/tests/docs only, existing conventions |
| Migration impact | forward-only, clean + upgrade replay, no destructive SQL |
| Acceptance tests | exact commands and real-stack evidence; performance, security, UX |
| Delivery | docs updated; commit SHA; push only `origin/dev` per repo governance |
| Completion decision | product spec + code + automated evidence + signed acceptance, not agent narration |

## Workstream dependencies

`P0 baseline → P1 ADRs+product → P2 foundation → P3 validated pilot → P4 release gate → P5 domain-by-domain expansion`. Future P6 can start a scoped research spike after P3 data truth, not a production Python deployment.

## STOP conditions

If a task touches customer financial truth without owner-defined semantics; adds AI to write transaction path; causes cross-tenant evidence leak; uses unknown scope; fails production DB migration replay; has unexplained performance regression; auto-starts analysis on GET; or invents a result during source failure — **STOP RELEASE**, fix cause and rerun affected acceptance. Do not bypass gates to close a task.
# P0 evidence supplement

P0-B evidence is available under [`evidence/p0/`](../evidence/p0/README.md). P0 is `P0_EVIDENCE_COMPLETE` for the disposable Development Seed baseline: canonical migration/seed, runtime security and measured local performance gates passed. This does not certify production capacity or authorize P1/P2 implementation in this mission.

