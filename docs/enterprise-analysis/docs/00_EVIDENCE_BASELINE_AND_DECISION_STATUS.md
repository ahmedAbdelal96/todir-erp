# 00 — Repo Evidence, Baseline & Decision Status

> **Historical baseline note (2026-11-28):** This document records the 2026-11-24 static baseline at `e9f762...`; it is not the current repository state. Read [CURRENT_STATE_RECONCILIATION.md](../CURRENT_STATE_RECONCILIATION.md) for the current `dev` commit, including the implemented Planning module. Current P1 decisions are in [DECISION_REGISTER.md](../DECISION_REGISTER.md).

**Status:** Audited static inspection / discovery input only. **No runtime testing or benchmark has been executed for this package.**

## Snapshot and review methodology

Source of evidence: local `dev` at `e9f7626953daf4c7cd426a49199dfd46fde69a8c`, audited on 2026-11-24. The worktree matched `origin/dev` before the P0 evidence files were added. The review covered the master blueprints, current module manifests, contracts, selected backend service/repository code, frontend destination registry/routes, Worker/API composition, messaging/scheduling, performance harness, Subledger/Payments schema excerpts, and the supplied historical reviews. **This is a focused architecture/documentation audit, not a codebase-wide correctness certification.** Code evidence ≠ exercised behavior/production readiness.

| ID | Finding | Evidence | Class | Consequence |
|---|---|---|---|---|
| EV-01 | .NET Modular Monolith, PostgreSQL, React/TypeScript and shared business modules exist | `README.md`, `backend/`, `frontend/` | EXISTING-VERIFIED | No greenfield architecture; reuse patterns |
| EV-02 | 25 directories and 25 manifests under `backend/Modules`, including **Manufacturing** and **Quality**; no `Analysis` module; Budgeting implementation directory absent | Local `backend/Modules` and `module.manifest.json` inventory at `e26de6f6` | EXISTING-VERIFIED | The older count and any assumption that L6 is runtime-supported are stale |
| EV-03 | 29 packs under `docs/modules`, including Manufacturing and FP&A; frontend has 26 module directories including identity/settings/migration | Local `docs/modules` and `frontend/modules` inventory | EXISTING-VERIFIED | Spec existence does NOT imply a ready runtime module |
| EV-04 | Master Blueprint locks single source of truth, single writer, modular boundaries and forbids direct AI database access | `docs/architecture/01_...BLUEPRINT.md` sections 1,8,9,28.34,28.38 | EXISTING-VERIFIED | Analysis must use approved capabilities/projections, not direct cross-schema queries |
| EV-05 | Architecture tests enforce module implementation isolation | `docs/architecture/solution-boundaries.md`, `backend/Tests/Architecture` | EXISTING-VERIFIED | New module and host must preserve these gates |
| EV-06 | UI navigation uses destination registry/effective-access and separate task routes | `frontend/app/router/destination-registry.ts`, `frontend/modules/sales/routes.tsx`, `frontend/app/shell/app-shell.tsx` | EXISTING-VERIFIED | Add analysis destinations via established registry, not separate sidebars |
| EV-07 | `Worker/Program.cs` runs many MassTransit consumers, Quartz jobs and hosted services in one executable | `backend/Worker/Program.cs:38-125` | EXISTING-VERIFIED | Heavy analysis cannot be added there by default; an independently resource-bounded process role must be proven or approved |
| EV-08 | `MessagingHostingExtensions` configures RabbitMQ quorum endpoints and common prefetch 16 | `backend/Core/Messaging/MessagingHostingExtensions.cs` | EXISTING-VERIFIED | Do not indiscriminately append heavy analytic consumer to operational bus |
| EV-09 | Outbox dispatch runs every 5 seconds and calls `claim_pending_outbox(100)` | `backend/Worker/OutboxDispatchWorker.cs` | EXISTING-VERIFIED | Track broker and outbox lag in acceptance; not measured here |
| EV-10 | Quartz is configured for Postgres persistence and clustering | `backend/Core/Scheduling/SchedulingHostingExtensions.cs` | EXISTING-VERIFIED | Scheduling available, but user forbids auto-creation of analysis runs in v1 |
| EV-11 | `Subledger` has due dates, application and reversal records, as-of balance logic and scoped queries | `backend/DbMigrator/Database/bootstrap/subledger-schema.sql`; `SubledgerRepository.cs` | EXISTING-VERIFIED | Potential Collections descriptive data foundation; cannot infer true cash receipt date from application date |
| EV-12 | `PaymentsService.ApplyToPaymentAsync` forwards request `ApplicationDate` / `SettlementDate` | `backend/Modules/Payments/Services/PaymentsService.cs` | EXISTING-VERIFIED | Analyze allocations and provenance before predicting payment behavior |
| EV-13 | Payments table has `payment_date`, `value_date`, `posting_date`, `completed_at`, `reconciliation_state` | `backend/DbMigrator/Database/bootstrap/payments-schema.sql` | EXISTING-VERIFIED | Date semantics and bank-cash proof require investigation |
| EV-14 | Banking has existing `cash_forecast_version`, `cash_forecast_item`, read and create methods | `backend/Modules/Banking/Repositories/BankingRepository.cs` | EXISTING-VERIFIED | Prevent duplicate commercial forecast ownership |
| EV-15 | CRM forecast code exists; older static review found deterministic pipeline weighting, not ML | `backend/Modules/CRM/Repositories/CrmRepository.cs` and independent report | STATIC-REVIEW | Forecast label != ML guarantee; re-verify before implementation |
| EV-16 | FP&A specs explicitly separate Actual / Budget / Forecast / Scenario and `AI Suggestion != Approved Forecast` | `docs/modules/28_...FPA...md` §§4–30 | EXISTING-VERIFIED-DOC | Never publish a recommendation as an approved financial plan |
| EV-17 | Product needs authorized user-triggered analysis, filters, saved history and recommendations | Product-owner decisions in this conversation | USER-LOCKED | No background auto-run / implicit page-triggered computation |
| EV-18 | API composition explicitly registers the current modules and has no Analysis registration; frontend destinations have no analysis route | `backend/Api/Program.cs:45-74`, `frontend/app/router/destination-registry.ts`, current module routes | EXISTING-VERIFIED | Any implementation must first pass module/manifest/navigation compatibility review |
| EV-19 | A disposable performance harness exists and is designed to create `todir_perf_*` databases, but it was not run during this audit | `backend/Tests/Performance/platform-performance-benchmark.ps1:13-26` | EXISTING-VERIFIED / TO-VERIFY | No latency, throughput, DB pressure or fairness claim is currently proven |
| EV-20 | The supplied consolidated design review duplicates the numbered pack | `reference/CONSOLIDATED_DESIGN_REVIEW_2026-11-22.md` and `docs/00`–`18` | AUDIT-CORRECTED | It is retained as historical reference only, not a second authority |

## Source links (pin to SHA)

Base URL `https://github.com/ahmedAbdelal96/todir-erp/blob/e26de6f6192fdcf7a28802150c53cf124a5cb771/`, followed by exact paths above. The running branch **must be reverified** before implementation, and any baseline drift documented. The URL reflects committed code only; local uncommitted changes remain outside this evidence pin.

## Open investigation ledger — these are NOT settled facts

| ID | Unknown | Cheapest reliable verification |
|---|---|---|
| U-01 | Actual performance P50/P95/P99 and DB/broker throughput | Local/staging baseline at realistic data size; no guessed numbers |
| U-02 | Complete payment-to-bank confirmation lifecycle, including method-specific value dates | Read code paths/triggers and real-stack test plus accounting owner review |
| U-03 | True as-of knowledge-time reconstruction under retroactive operations | Temporal truth tables + seeded controlled fixtures + matching historical reports |
| U-04 | Number of useful invoices per customer and quality of client historical data | Permissioned audit on sanitized client data; no unrestricted production extracts |
| U-05 | Whether adding an Analysis module requires new manifest type/permission layout | Build a minimal nonproduction architectural fixture; run architecture validators |
| U-06 | Whether all targeted analysis dimensions exist and are stable across modules | Read each owner pack + code + data contract before catalog promotion |
| U-07 | Tenant resource quotas and noisy-neighbor behavior in real hosting | Simulate concurrent workers/tenants under controlled workloads |
| U-08 | Current deploy/scale resource limits in production | Inspect actual environment, not Terraform alone |
| U-09 | Customer demand and willingness to pay for first analysis | Egyptian business interviews and observed manual decisions |
| U-10 | Exact module entitlements under current SaaS packaging | Review Kernel entitlement checks and live UX before choosing pricing |

## Governing conflict-resolution order

1. Business/user requirements expressly stated here, subject to hard security and correctness checks.
2. Approved existing repo Master Blueprint / ADRs and current module owner specifications.
3. Verified implementation; mismatches become documented drift, not assumed approval.
4. This package's `PROPOSED-ADR` sections, pending approval.
5. Independent reviewer suggestions / other AI opinions are evidence to assess, never authority.

If sources conflict: **stop the affected slice, record the exact conflict, propose a measured choice and ADR; do not silently rewrite production behavior.**
# P0 evidence supplement

The current repository-grounded discovery is recorded in [`evidence/p0/README.md`](../evidence/p0/README.md). Its exit is `P0_PARTIALLY_VERIFIED`: static evidence is complete, while the disposable operational baseline and runtime isolation proof remain unmeasured because Docker API access was denied.

