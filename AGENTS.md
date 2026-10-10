# Instruction Routing

Always read this file first for every repository task.

- If the task touches backend code, databases, APIs, workers, migrations, messaging, backend tests, modules, security, or persistence, read `backend/AGENTS.md` after this file.
- If the task touches frontend code, React, TypeScript, UI, routing, frontend modules, frontend tests, Storybook, or Playwright, read `frontend/AGENTS.md` after this file.
- If the task touches both areas, read both scoped files. A scoped file adds rules for its area; repository-wide rules here remain mandatory.
- For architecture, module, API, security, operations, or database decisions, read the applicable canonical documents under `docs/` before changing code. Architecture and module packs define product truth; this file defines execution governance.
- If applicable documents conflict on ownership, contracts, tenancy, authorization, transaction semantics, technology, or destructive data behavior, stop the affected decision and report the conflict instead of guessing.

# Repository governance

TODIR is an Arabic-market-first, multi-tenant ERP SaaS in one codebase and a modular monolith. RTL is a Day-1 product concern. Backend domain truth, calculations, invariants, persistence ownership, and authorization remain authoritative; frontend code renders accessible workflows on the approved platform.

The active structure is `backend/`, `frontend/`, `docs/`, `ops/`, `scripts/`, and `tools/`. Do not recreate retired `src/backend/`, `src/frontend/src/`, `hosts/`, or `platform-kernel/` runtime roots. The shared frontend platform in `frontend/` is the only design system, form system, API client, query/cache architecture, theme/token engine, error mapping system, page shell, runtime context, and permission UX.

Business correctness comes before code patterns. For every ERP feature, understand the workflow first, then determine ownership, lifecycle/state, invariants, permissions, integrations, accounting impact, exceptional flows, database model, API, UI, and tests. Do not design modules starting from screens or framework patterns. Before changing anything: inspect the repository and current status; read this file and the applicable scoped file; read the approved architecture/module/API/ADR documents; inspect implementation and tests; identify ownership, dependencies, boundaries, and migration impact; then plan. Implement only approved scope, verify it, self-review it, fix clear in-scope defects, and re-verify. Do not add speculative products, packages, folders, compatibility shims, or functionality.

Keep one source of truth and one writer per domain. Modules do not read or write another module's tables directly. Use approved synchronous contracts for immediate invariants and Outbox/Inbox guarantees for post-commit integration. Preserve an acyclic dependency graph. Tenant isolation is a security boundary; every tenant-owned path must preserve tenant context, authorization, auditability, and approved PostgreSQL RLS. Posted accounting records and completed stock movements are immutable. AI never accesses the database directly.

Every new durable file or folder must describe responsibility, not the product brand, phase, date, version, or history. Never create a new basename containing `todir` in any casing, or prefixes such as `TODIR_`, `ERP_`, `P00_`–`P99_`, `Phase1_`, `Final_`, `New_`, `Latest_`, or `V2_`. Existing numbered canonical documents are retained. Avoid meaningless `Common`, `Helpers`, `Utils`, `Misc`, and `General` names unless the responsibility is explicit and narrow. Use governed directories only; put generated local output under ignored `artifacts/logs`, `artifacts/test-results`, `artifacts/coverage`, `artifacts/performance`, or `artifacts/tmp`.

Global core and business modules remain country-neutral. Country-specific legal, tax, payroll, and compliance behavior belongs in approved country packs, providers, or adapters.

Security, correctness, and maintainability take priority over convenience. Validate inputs and permissions at the backend, avoid sensitive logs and secrets, preserve correlation context, use stable machine-readable errors, bound and paginate access, prevent N+1 queries, and design only justified indexes and constraints. Do not swallow exceptions or report partial failure as success.

All implementation and debugging happen locally. Do not push fixes to discover whether they work, use GitHub Actions as the normal development loop, or wait for remote CI during implementation; use local builds, tests, Docker, PostgreSQL, RabbitMQ, and runtime verification. `dev` is the active development branch; `main` is frozen unless explicitly authorized. Work directly on `dev`, never rewrite published history or force-push, and do not create phase branches for routine work. For an explicitly authorized implementation mission, use discover → design → implement → targeted local tests → full local verification → one commit → one push to `origin/dev`. Do not commit or push when the user explicitly excludes it. Do not create pull requests for routine solo development.

Run the applicable unit, integration, architecture, security, tenant/RLS, contract/API, frontend, browser, performance, build, and documentation checks. Compilation alone is not completion. Report alignment with the plan, changed files, exact test results, architecture/security/database/performance considerations, deviations, risks, and decisions still needed. Never claim unmeasured coverage or a passing gate.
