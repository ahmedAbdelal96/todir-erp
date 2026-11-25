# 15 — Requirements Traceability & Exact Acceptance Matrix

**Goal:** Every statement in specs has an executable acceptance path. No task accepted solely for completing code compilation or screenshots. Test case names illustrative; agents implement matching framework conventions.

| Requirement | Proposed evidence | Must pass before |
|---|---|---|
| PR-001 Dedicated analysis module route | Navigation registry + Playwright Arabic role route test | UX pilot acceptance |
| PR-002 No auto-runs on page open | Browser instrumentation 20× navigation/refresh/filter change; backend job table count unchanged | Foundation/UI acceptance |
| PR-003 Manual create + idempotency | Concurrent duplicate HTTP POST + retries; one run/one result | Foundation acceptance |
| PR-004 Filters and date basis | Valid/invalid range/property tests and server-side SQL whitelist | Every analysis type |
| PR-005 Effective access | Roles, tenant/entity/branch records, read-vs-run-vs-review negative paths | Any deployment |
| PR-006 Persistent job recovery | Host crash/lease expire/restart with exact final publication | Worker acceptance |
| PR-007 Historical integrity | Run before/after corrected source; original report checksum unchanged, freshness separate | Any historical release |
| PR-008 Explainable recommendation | Rule/evidence fixture; missing source → no unsupported recommendation | Decision pilot |
| PR-009 No autonomous business writes | Unit/integration test + architectural code review; no business state changed by analysis | Any deployment |
| PR-010 Bounded history | Cursor pagination and SQL EXPLAIN large data; stable sorted scope | History endpoints |
| PR-011 No material operational slowdown | Reproducible B01–B12 mixed load p95/p99, outbox lag, locks and resource limits | Production release |
| PR-012 Insufficient source data | Data quality fixtures, partial/insufficient display | Every analysis type |
| PR-013 Shared frontend patterns | `architecture:frontend`, localization, RTL/English, accessibility, auth route tests | UI acceptance |
| PR-014 Interpretability | Reviewer sees reason/source/caveat/alternative | Recommendation approval |
| PR-015 Export retention | Scope/PII, signed link and export auditing | When export enabled |
| PR-016 Executive view future | Explicit scoped business use case and ADR | Future phase only |

## Code-level acceptance by layer

**Owner module:** read contract correct, per-entity/record scope, run cursor, per-fact temporal fields, adequate indexes, no modification in daily transaction hot path, tests for negative business states.

**Analysis core:** schema FORCE RLS, defined manifest/dependency, correct job transitions/idempotency, immutable published snapshots, validated arithmetic and recommendation rules, audit/access.

**Worker:** separate process and DB resources, safe claim/heartbeat/lease/fence, checkpoint retries, bounded cancellation, monitoring and kill switch.

**API:** authenticated scoped GET and POST with identical error conventions, no auto-run GET, OpenAPI versioned DTOs, active definition registry and limits, pagination.

**Frontend:** natural Arabic page title; user filters; explicit submit; past-run history; recommendation detail; no analysis in existing operational page; role discoverability and session invalidation.

**Quality:** multi-tenant matrix, temporal financial fixtures, data quality gates, load tests, canary and restore, no source regression.

## Realistic acceptance data set matrix

| Fixture | Small | Medium | Large / concurrency |
|---|---|---|---|
| Tenants | 2+ isolation | several mixed module entitlements | noisy tenant and constrained role combinations |
| Legal entities | 2 with different permissions | multi-branch | large entity with concurrent payment/sales posting |
| AR/payments | full/partial/mixed/noncash/reversed | diverse currencies, delayed allocations | historical records sufficient to expose N+1 and index mistakes |
| Results | one run | different version/filter/history | many runs and large evidence lists |
| Jobs | normal complete | duplicate/cancel/retry | worker crash, many concurrent jobs, max allowed queue |
| UX | RTL/LTR | narrow role, stale caches | slow network, context switch, 403/503 and failed worker |

Dataset volumes **must be chosen after P0** to reflect target Egyptian customer ranges; do not invent production capacities or assert simulator results prove market relevance.

## Mandatory report fields delivered per stage

1. Exact branch/commit/source diff.
2. Links to current owner docs/ADRs and task spec IDs.
3. Commands executed and reproducible environment/seed data.
4. Counts and status of tests, failures and investigation; no hidden skips.
5. Measured API/DB/worker resource baseline and changed result where applicable.
6. Security evidence and negative cases, data correctness/reconciliation evidence.
7. Known open risks/limitations classified Critical/High/Medium.
8. Manifest/permission/navigation/entitlement updates and localization parity.
9. Relevant docs updated; review needed for any deferred ADR.
10. Post-merge `origin/dev` SHA and working tree status (agent branch policy; no feature branch unless user changes direction).

## Before declaring a module done

One affected module can be delivered as an implementation slice **only after** its full backend/database/API/frontend integration and associated Acceptance tests are complete. Horizontal skeletons (schema without API, API without secure frontend, UI without real facts) are **not** product complete. No destructive DB migration or production cutover without explicit governance.
# P0 evidence supplement

The P0 verification ledger is [`P0-09_EVIDENCE_VERIFICATION_MANIFEST.md`](../evidence/p0/P0-09_EVIDENCE_VERIFICATION_MANIFEST.md). Historical rows retain their original `NOT MEASURED` wording where the disposable stack was unavailable; the current P0-B runtime and performance evidence is recorded in the dated `P0-B03`, `P0-B04`, `P0-B05` and `P0-B07` artifacts.

