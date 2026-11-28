# 16 — Engineering Agent Handoff and Governance

> **Current handoff:** This historical stage-zero handoff is retained for continuity. The active startup/closeout contract is [AGENT_HANDOFF.md](../AGENT_HANDOFF.md); the current repository pin is `c02f12e5c33f5f0ebd5e3866d447b79eb3b124d6`.

**IMPORTANT:** This is a handoff policy and **stage-zero discovery prompt**, not authorization to begin coding the whole platform. Implementation must wait until P0 evidence and P1 ADR/product gates are accepted. The repo is `dev` only according to product-owner branch governance.

## Agent operating contract

- Begin every assigned prompt: **Act as a senior ...** technical role appropriate to stage.
- Read root and scoped `AGENTS.md` files, existing Master Blueprint/Navigation/UI/Module packs and this design pack before touching code.
- Check current `origin/dev` and pin commit; this pack was audited locally at `e26de6f6192fdcf7a28802150c53cf124a5cb771`, but committed code and uncommitted worktree changes must still be separated before implementation.
- Do not substitute speculative services/modules for owner code. No generic table sweeps, ORM cross-module imports, direct source schema reads from Analysis, new entitlements/permissions engine or automatic analysis on GET.
- Prove operational performance/financial correctness and tenant/record isolation before marking any implementation slice complete.
- Avoid repeat user interruptions for minor blockers; if an architectural conflict requires sign-off, document exact evidence, options, impact and recommendation.
- Use `dev` only, follow repo's approved safe git workflow, push via configured SSH and report exact SHA when a code task is explicitly authorized. This package is docs-only; no repo mutations in this preparation task.
- Never invent tests, actual business data, benchmark numbers or approvals.

## Next permitted task: P0-only discovery prompt (English)

> Act as a Principal ERP Software Architect, Senior .NET Engineer, PostgreSQL Data Architect, Security Reviewer, Performance Engineer, and Senior Product-Technical Analyst. You own a **read-only design discovery and operational baseline** for ToDir ERP Business Analysis & Decision Recommendations.
>
> Work against the repository's latest `origin/dev`, preserving the user's single-branch `dev` policy. Read the root and scoped AGENTS.md, current approved Master Blueprint, navigation/effective-access contract, module owner technical packs, existing .NET Contracts, Workers/Messaging, PostgreSQL schema and migrations, frontend route registry, and the provided `TODIR_ENTERPRISE_ANALYSIS_DESIGN_PACK` in order. The documentation is a *design proposal*, not pre-approved code direction.
>
> First, pin and report the exact Git SHA and list all relevant changes since the audited design-pack SHA (`e26de6f6192fdcf7a28802150c53cf124a5cb771`). Verify and classify each critical statement as implemented, documented-only, inferred, unverified, or outdated. If the worktree is not clean, separate committed evidence from uncommitted changes.
>
> Perform a detailed read-only inventory of existing owner read contracts and source-of-truth semantics for Subledger, Payments, Banking, Sales, Inventory and Manufacturing. Map direct dependencies and forbidden boundaries, current .NET worker deployment shape, current navigation/access/entitlement pattern, existing result/history/audit conventions, and actual database indexes/tenant RLS. Deeply investigate cash-receipt vs application dates, corrections, partial payment, reversals, writeoffs, historical as-of and knowledge-time. **Do not assume collections is the selected commercial pilot.**
>
> Establish a reproducible performance baseline on an isolated non-destructive environment for critical Sales, Invoicing, Payments, Subledger, Inventory, POS and relevant Manufacturing flows, including p50/p95/p99, throughput, PostgreSQL CPU/locks/query plans/pool, RabbitMQ outbox lag, worker resource usage, dataset size, and exact commands. If prerequisites are unavailable, mark benchmark `NOT MEASURED`; do not invent data, bypass controls or modify important DBs.
>
> Deliver only: (1) VERIFIED_CURRENT_STATE.md with file/line evidence and exact SHA, (2) MODULE_SOURCE_FACT_AND_READ_CONTRACT_MATRIX.md, (3) DATA_TRUTH_AND_TEMPORAL_RISK_REPORT.md, (4) OPERATIONAL_PERFORMANCE_BASELINE.md plus real scripts/artifacts, (5) ARCHITECTURE_ADR_REVIEW.md identifying approved/proposed/conflicting choices, (6) precise P1 approvals and test prerequisites. Include failure modes, alternatives and your independent recommendation, not agreement with the supplied proposal.
>
> User product requirements are fixed: dedicated naturally named analysis pages **inside relevant modules**; explicit authorized button to request computation only after typed filters and period selection; **no automatic analysis** from opening/refreshing pages, events or schedules; saved immutable historical runs; evidence-based decision recommendations reviewed by humans; no direct domain mutation by AI; no observable unacceptable regression to everyday ERP operations; retain current effective-access/entitlement/UI conventions.
>
> **Do not implement Analysis backend, API, worker, migrations, new pages, Python service, or alter existing operational functions in this P0 task.** Do not rename the source modules or introduce arbitrary technology. Prefer the shortest trustworthy path to evidence. Document every blocked measurement, and stop for formal ADR approval before implementation.

## Implementation-stage task contract (use ONLY after P1 acceptance)

> Act as a Principal .NET Modular Monolith Engineer and Data/Performance Architect. Implement exactly the approved stage [P2/P3...] on latest `dev`, respecting Master Blueprint and approved ADRs. Deliver backend, database, API, frontend (when task owns a user-facing slice), permission/entitlement/navigation, tests and docs end-to-end. Do not reimplement business facts, add auto-runs, move AI into core transaction paths, or use cross-schema SQL. Establish before/after performance metrics. Run architecture/security/real-Postgres/real-stack/UI and failure recovery tests as scoped; report commands and results honestly. Use safe forward-only migrations and protect seeded DBs. Finalize task-owned docs, push the accepted scope to `origin/dev`, and report commit SHA and residual risks. The current stage is not complete until explicit acceptance gates pass.

## Code review checklist for every PR/commit

- [ ] Does any existing operation controller/service call Analysis directly or await Analysis work? If yes, reject except explicitly approved nonblocking and measured capability.
- [ ] Does any frontend operational page trigger analysis POST or load hidden analysis data? If yes, reject.
- [ ] Any module-to-module implementation import or direct SQL read/write across schemas? If yes, reject.
- [ ] Has source fact semantics been agreed by owner and historical corrections handled?
- [ ] Are source filters/permissions/tenant/legal entity and branch scope enforced by backend and SQL RLS?
- [ ] Does GET change state, create work or cause recomputation? If yes, reject.
- [ ] Are run status/result/decisions versioned, history immutable, and records paginated?
- [ ] Is worker failure/retry/cancel idempotent and operational ERP independent?
- [ ] Are p95/p99 and DB/broker resource regressions compared to real baseline?
- [ ] Are UX names natural Egyptian Arabic and localization parity preserved?
- [ ] Did the agent update affected module docs, current performance evidence and ADR links without invented PASS claims?

## Decision hygiene

Every stage's report must provide **Recommendation / Rationale / Alternatives Rejected / Evidence / Unknowns / Acceptance Criteria**. Never use «معاك حق» or consensus as architectural proof. A new idea changes approved design only with a reasoned change request that identifies affected requirements, data migrations, tests and rollback.
