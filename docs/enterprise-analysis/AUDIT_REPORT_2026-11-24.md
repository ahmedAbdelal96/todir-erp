# ToDir Enterprise Analysis Design Pack — Independent Audit

**Audit date:** 2026-11-24  
**Branch:** `dev`  
**Committed evidence SHA:** `e26de6f6192fdcf7a28802150c53cf124a5cb771`  
**Result:** `REQUIRES_CORRECTIONS`  
**Scope:** Documentation and safe, non-mutating repository verification only. No Analysis code, migration, operational code, database data, or frontend screen was changed.

## Executive decision

The design pack is strong on the user-locked behavior: explicit user-triggered analysis, saved immutable results, typed filters, human review of recommendations, owner-module execution, tenant isolation, and performance protection. It is not implementation-ready.

The main reason is architectural, not editorial: the current repository has 25 backend module directories/manifests, all using layers L1–L4, no Analysis module, no L6 runtime manifest example, and one explicitly composed operational Worker. The proposal's L6 Analysis module and separate `AnalysisWorker` host are therefore proposed shapes, not currently supported implementation facts. P2 must not create either until the boundary, manifest/schema/lifecycle/entitlement model, and process/deployment isolation are formally approved and proven.

The Collections pilot is also not ready to be locked. Current Subledger contracts expose party-scoped balance/aging/statement and open-item capabilities; a certified portfolio-wide, historical, multi-owner collections fact export is not present in the inspected code. Cash-receipt semantics, bank confirmation, allocation timing, valid-time and knowledge-time reconstruction remain evidence gates.

## Evidence and repository inventory

| Item | Result | Evidence |
|---|---|---|
| Local branch | PASS | `git branch --show-current` → `dev` |
| Current committed source | VERIFIED | `git rev-parse HEAD` → `e26de6f6192fdcf7a28802150c53cf124a5cb771` |
| Remote tracking | VERIFIED | `dev...origin/dev` at audit start |
| Worktree | NOT CLEAN before audit | Pre-existing Quality backend/frontend and engineering-document changes; preserved unchanged |
| Backend module directories | 25 | `backend/Modules` inventory |
| Backend manifests | 25 | Every current module manifest is L1–L4; no Analysis manifest |
| Frontend module directories | 26 | Includes identity/settings/migration; no analysis destination/page |
| Numbered design documents | 19 | `docs/enterprise-analysis/docs/00` through `18` |
| Historical references | 3 after correction | `reference/CLAUDE...`, `reference/PREVIOUS...`, `reference/CONSOLIDATED...` |
| Runtime benchmark | NOT MEASURED | Existing disposable harness was inspected but not executed |
| Runtime Analysis implementation | NOT PRESENT | No Analysis API, schema, worker, route, or seed was found |

The design pack was originally pinned to `8a06af00...`; the current `dev` commit is 47 commits ahead of that pin. Any older repository counts or claims are historical evidence only until rechecked.

## Documentation structure audit

### Findings and corrections

| ID | Finding | Severity | Correction |
|---|---|---:|---|
| DOC-01 | The pack's source pin and module counts were stale relative to current `dev`. | High | Updated `README.md` and `docs/00...` to the audited SHA, current counts, worktree caveat, and explicit evidence limits. |
| DOC-02 | `TODIR_BUSINESS_ANALYSIS_FULL_DESIGN_REVIEW_2026-11-22.md` was a consolidated duplicate of the numbered pack and could be mistaken for a second authority. | High | Preserved it under `reference/CONSOLIDATED_DESIGN_REVIEW_2026-11-22.md` and labelled `reference/` historical/non-normative in the index. |
| DOC-03 | The status vocabulary did not include `BLOCKED` and `DEFERRED`, although the mission requires both. | Medium | Added both statuses to `docs/14...` and used `BLOCKED` for the unresolved module/host boundary. |
| DOC-04 | The proposed `AnalysisWorker` and L6 module were described too close to an implementation blueprint despite no current runtime support. | Critical | Updated `docs/04`, `docs/06`, `docs/13`, `docs/14`, and `docs/18` to make the process role conditional and require a P1 boundary/deployment ADR. |
| DOC-05 | Existing checksums covered the original package but not the audit report and the moved consolidated reference. | Medium | Regenerated `CHECKSUMS.sha256` and extended `manifest.json` after the final documentation edits. |

Relative Markdown links were checked from every Markdown file under the package; no broken local Markdown links were found at the end of the audit.

## Architecture compatibility matrix

| Proposed capability | Canonical owner today | Existing evidence | Compatibility result | Required gate |
|---|---|---|---|---|
| Analysis runs, history, evidence, review | No current owner | No Analysis module/schema/API | `PROPOSED / BLOCKED` | Choose platform-owned capability or formally add a module with manifest/schema/lifecycle/entitlement ADR |
| AR facts and settlement history | Subledger | Party balance, aging, statement, open-item/application contracts; party-scoped reads | `PARTIAL / VERIFY` | Owner-approved portfolio export, cursor semantics, temporal fixtures, query plan |
| Payment completion and application | Payments | Payment/application paths and multiple date fields | `PARTIAL / VERIFY` | Payment-method cash taxonomy, completion/value/reconciliation semantics, reconciliation fixtures |
| Bank confirmation and treasury forecast | Banking | Banking owns reconciliation and cash forecast versions | `COMPATIBLE WITH BOUNDARY` | Analysis output must remain a scenario/recommendation and never become the official forecast |
| Sales analysis | Sales/Invoicing | Existing operational modules and routes | `POSSIBLE / VERIFY` | Select invoice/order/posting basis, returns/tax/currency rules, owner read capability |
| Inventory analysis | Inventory | Stock, reservation, valuation capabilities | `POSSIBLE / VERIFY` | Define on-hand/available/UOM/cutoff and avoid replacing replenishment ownership |
| CRM forecast | CRM | Existing deterministic pipeline forecast capability | `DUPLICATION RISK` | Reuse/extend existing owner capability; do not create a rival forecast state machine |
| Manufacturing analysis | Manufacturing | Runtime module exists at current SHA | `POSSIBLE / VERIFY` | Prove actual-vs-planned production facts and cost/capacity completeness |
| FP&A forecast/budget | No runtime implementation confirmed | Pack exists; runtime module absent from current module inventory | `DEFERRED / BLOCKED` | Wait for approved FP&A owner contracts; Analysis must not publish official forecasts |
| Navigation and access | Platform/effective-access | Current destination registry and guarded module routes | `COMPATIBLE` | Add only a registered destination and manifest-matching permission after module approval |
| Execution isolation | Existing Worker deployment | `backend/Worker/Program.cs` composes operational consumers and hosted services | `PROPOSED / VERIFY` | Prove analysis-only process role or approve a new host; mixed-load test with independent pool/limits |

### Boundary recommendation

Use one .NET codebase and one Analysis execution engine, but do not call the new capability an approved L6 module yet. The current manifest/runtime boundary must be resolved first:

1. **Platform-owned option:** only if the capability is genuinely platform/reporting infrastructure and can use the existing `platform` ownership/schema rules without owning business semantics.
2. **New-module option:** add a formal module-manifest/schema/lifecycle/entitlement decision for the new capability and prove the DAG and host registration with architecture tests.

The second option is the likely long-term shape for persisted analysis results and recommendation review, but it is a proposed architectural change, not a fact established by the current repository.

For compute isolation, first test an independently deployed analysis-only process role using the existing Worker conventions. A separately named project/host is not justified by documentation alone.

## Data ownership and temporal correctness

The pack correctly separates ERP facts from derived analysis. The following items remain blocked until owner evidence exists:

- A customer collection analysis cannot treat application date or settlement date as cash receipt date.
- A historical report needs both economic validity (`business_as_of`) and what was knowable at the time (`known_at`); current tables do not prove full bitemporal reconstruction.
- Partial allocations, reversals, write-offs, credit notes, refunds, opening balances, FX and missing value dates require explicit fixture truth tables.
- Cross-owner watermarks do not automatically create one atomic snapshot. The result must state whether it is owner-consistent, partially consistent, or assembled from an approved common cutoff.
- Analysis may retain bounded evidence and derived snapshots, but cannot become a second open-item, payment, bank, inventory, ledger, or forecast source of truth.

The existing `docs/05...`, `docs/07...`, `docs/08...`, and `docs/12...` remain the normative design locations for these rules. They are not evidence that the contracts or data are already implemented.

## Security, entitlements, and UX findings

- Proposed permission names such as `sales.analysis.run` are not current manifest permissions. They must not be shipped or referenced by the frontend until manifest, effective-access, entitlement, route, and backend authorization parity is approved.
- Current API registration and frontend destination registry contain no Analysis route. A future page must be a dedicated business route, not a hidden tab or operational-page query.
- GET history/result endpoints must remain read-only; only the authenticated explicit POST may create a run. The existing page architecture and frontend permission guards are the correct integration points.
- Source-record access must be re-evaluated when viewing saved evidence. A run creator is not automatically entitled to view all future or sensitive evidence.
- The current repository contains pre-existing frontend files using utility-style class names such as `space-y-6` in `frontend/modules/subledger/routes.tsx`, while frontend governance prohibits Tailwind utilities. This is a pre-existing implementation discrepancy documented for the responsible frontend mission; it was not changed here and is not evidence that the proposed Analysis UI is compliant.

## Performance baseline and protection plan

**Measured results:** none. No benchmark, load test, database replay, worker crash test, RLS runtime test, or browser run was claimed or executed for this audit.

The existing `backend/Tests/Performance/platform-performance-benchmark.ps1` is a useful starting harness. It creates a disposable `todir_perf_*` database, exercises authenticated endpoints, samples PostgreSQL/process state, and records p50/p95/p99-style measurements. It is not an Analysis benchmark and does not prove future isolation.

P0 must run the existing harness only in a disposable environment and record:

- exact source SHA, seed/data volume, host resources, pool settings and background services;
- p50/p95/p99 and error rate for Sales, Invoicing, Payments, Subledger, Inventory, POS and any selected pilot owner path;
- PostgreSQL CPU/I/O, locks, pool saturation, query plans, outbox/RabbitMQ lag and Worker CPU/memory;
- the same workload with analysis idle, one bounded job, concurrent jobs, worker disabled, cancellation and restart;
- per-tenant fairness and queue/backpressure behavior.

The `5% p95` figure in `docs/10...` is correctly labelled a provisional proposal, not an approved SLO or a measured result. Production thresholds must be selected after P0 baseline and owner approval.

## Requirements traceability coverage

| Requirement family | Authoritative design document | Acceptance evidence required | Current status |
|---|---|---|---|
| Manual user-trigger only / no GET side effects | `docs/01`, `docs/04`, `docs/06`, `docs/11` | Browser network assertion + job-table count + duplicate POST test | `USER-LOCKED / NOT IMPLEMENTED` |
| Dedicated business pages and Arabic terminology | `docs/03`, `docs/16`, `docs/18` | Effective-access route test, Arabic/English parity, RTL and accessibility review | `PROPOSED / NOT IMPLEMENTED` |
| Typed domain filters and date semantics | `docs/02`, `docs/05`, `docs/17` | Owner-approved truth tables, invalid-scope matrix, property/contract tests | `PROPOSED / BLOCKED PER TYPE` |
| Immutable historical results | `docs/07`, `docs/11` | Source correction replay, sealed result hash/version, retention and access tests | `PROPOSED / NOT IMPLEMENTED` |
| Recommendation and human decision lifecycle | `docs/08`, `docs/15`, `docs/17` | Missing-evidence suppression, review concurrency, no owner mutation, outcome audit | `PROPOSED / NOT IMPLEMENTED` |
| Tenant/entity/branch and current-permission isolation | `docs/09`, `docs/15` | Real PostgreSQL RLS, worker pooled-connection reuse, role revocation and evidence tests | `USER-LOCKED / NOT IMPLEMENTED` |
| Operational performance independence | `docs/06`, `docs/10`, `docs/13` | Mixed-load baseline and failure/kill-switch evidence | `USER-LOCKED / NOT MEASURED` |

The existing `docs/15_TRACEABILITY_TEST_MATRIX.md` remains the detailed acceptance matrix. This report records its current evidence status; it does not convert proposals into passing tests.

## Decision register after audit

| Decision | Status | Recommendation |
|---|---|---|
| Explicit POST-only creation | `USER-LOCKED` | Keep unchanged |
| Separate business pages/history | `USER-LOCKED` | Keep unchanged; integrate through destination registry |
| Owner modules remain sources of truth | `LOCKED` by existing architecture | Keep unchanged |
| .NET-first deterministic engine | `PROPOSED` | Keep for v1; no Python/ML service |
| Analysis module location | `BLOCKED` | Resolve platform-owned vs new module boundary in P1 ADR |
| Analysis process/host shape | `BLOCKED` | Prove existing Worker role split or approve a new host |
| PostgreSQL job store and leasing | `PROPOSED` | Benchmark contention and RLS dispatcher before approval |
| Collections as first pilot | `PROPOSED` | Do not lock until data-readiness and market-value gates pass |
| Separate official Banking/FP&A forecast ownership | `LOCKED` boundary | Keep Analysis outputs clearly non-authoritative |
| Statistical forecasting | `DEFERRED` | Require leakage-safe backtest and baseline advantage |

## Implementation readiness

**Decision: `REQUIRES_CORRECTIONS`.** The documentation audit itself is complete, but implementation must not begin beyond P0/P1 discovery. The design becomes eligible for architecture approval only after:

1. the module/host boundary is approved and validated against the current solution and manifest rules;
2. the pilot has one owner-approved fact contract with temporal and financial truth tables;
3. permissions, entitlement, retention and cross-scope result access are decided;
4. the disposable performance baseline and mixed-load protection test are measured;
5. the selected analysis specification template is completed and approved;
6. the final P1 package is rechecked against the latest `origin/dev` and a clean evidence pin.

The exact next engineering task is **P0 read-only discovery and disposable baseline**, not P2 implementation.

## Audit commands and results

| Check | Result |
|---|---|
| `git status --short --branch` | PASS; `dev` tracked `origin/dev`, pre-existing worktree changes preserved |
| `git rev-parse HEAD` | PASS; `e26de6f6192fdcf7a28802150c53cf124a5cb771` |
| Repository/module/file inventory | PASS |
| Local Markdown link resolution | PASS; no broken local links |
| Manifest numeric-document inventory | PASS; 19 manifest entries match `docs/00`–`18` |
| SHA-256 package validation | PASS after final regeneration |
| `git diff --check` | PASS after final documentation edits |
| Runtime benchmark | NOT RUN; no isolated benchmark execution claimed |
| Backend/frontend tests | NOT RUN; prohibited by docs-only scope unless needed for safe documentation validation |

## Files changed by this audit

- Updated: `README.md`, `docs/00_EVIDENCE_BASELINE_AND_DECISION_STATUS.md`, `docs/04_TECHNICAL_ARCHITECTURE_AND_BOUNDARIES.md`, `docs/06_JOBS_WORKERS_AND_ISOLATION.md`, `docs/13_DELIVERY_ROADMAP_AND_ACCEPTANCE.md`, `docs/14_ADRS_OPEN_QUESTIONS_AND_RISK_REGISTER.md`, `docs/16_IMPLEMENTATION_AGENT_HANDOFF.md`, `docs/18_IMPLEMENTATION_STRUCTURE_MODULE_MAP.md`, `manifest.json`, and `CHECKSUMS.sha256`.
- Added: this audit report.
- Reorganized without deletion: the duplicated consolidated review moved to `reference/CONSOLIDATED_DESIGN_REVIEW_2026-11-22.md` and is explicitly historical/non-normative.
- Not changed: all backend/frontend operational source and all pre-existing unrelated worktree changes.
