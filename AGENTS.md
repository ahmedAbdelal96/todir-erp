# 0. Instruction routing

Always read this root file first.

Exactly three active `AGENTS.md` instruction files govern this repository: `/AGENTS.md`, `/backend/AGENTS.md`, and `/frontend/AGENTS.md`. Do not add another scoped instruction file; place shared governance in this root file and area-specific rules in the corresponding backend or frontend manual.

- For backend, database, API, security, messaging, migration, worker, or backend-test work, read `backend/AGENTS.md` before implementation.
- For frontend, React, TypeScript, UX, UI, copy, routing, frontend-test, Storybook, or Playwright work, read `frontend/AGENTS.md` before implementation.
- **Mandatory Frontend Skill Activation**: For ANY frontend task, screen, component, form, table, workflow, or localization modification, the agent MUST mandatorily load, activate, and follow the `erp-frontend-architect` skill (`.agents/skills/erp-frontend-architect/SKILL.md`). Every frontend change must pass its Discovery Gate, Pre-Code Card, UX Laws, and Domain Safety checks.
- For work spanning both areas, read both scoped files. These scoped files add area rules; this root file always remains mandatory.

## Mandatory Product & Engineering Principles

Before making any product, business, architecture, or
implementation decision, read and follow:

`/PRODUCT_ENGINEERING_PRINCIPLES.md`

These principles are mandatory throughout the project.

Apply them during requirements analysis, business workflow
design, architecture planning, implementation, and review.

Do not blindly agree with user suggestions.
Challenge weak assumptions, identify missing requirements,
and recommend well-reasoned decisions.

Project-specific approved rules and documented decisions
must also be respected. If a conflict is identified,
surface it explicitly before changing the design.

# 1. Product principles

This is a production-grade, Arabic-market-first, multi-tenant ERP SaaS. It is one codebase and a Modular Monolith. PostgreSQL is the system of record; the backend is .NET and the frontend is React/TypeScript. Strong domain ownership, tenant isolation, data integrity, and business simplicity are first-class requirements. Technical sophistication must remain behind a simple user experience.

# 1.1 Canonical module documentation governance

Every current and future module implementation is driven by its canonical Module Pack under `docs/modules/`. Before implementation, identify the exact current pack and read its full scope, business purpose, domain model, ownership, workflows, lifecycle, invariants, permissions intent, integrations, edge cases, UX intent, MVP scope, explicitly deferred scope, development seed scenarios, acceptance scenarios, and Definition of Done. The Module Pack defines **WHAT** and **WHY**; the applicable `AGENTS.md` files define **HOW**. Both remain active references throughout implementation.

Existing code, database tables, DTOs, endpoints, screens, or framework patterns are not authority when they conflict with the documented module decision. Do not implement by assumption, silently reduce the documented scope, invent undocumented behavior, or mark work complete because a build, current API, or existing test passes. Resolve ordinary implementation and UX choices using the documented business goal and sound judgment. If documentation is materially contradictory or leaves a locked business, accounting, legal, security, or architecture decision unresolved, record the exact gap, stop only the affected decision, and continue independent work until it is resolved or explicitly deferred by the Product Owner.

Every module mission report must name the canonical pack, summarize the documented scope, list the acceptance scenarios, identify MVP versus deferred items, and list unresolved gaps. No module may be reported as `MODULE COMPLETE` until this chain is covered:

```text
Canonical Module Pack
→ DB Coverage
→ Backend Coverage
→ Development Seed Coverage
→ Frontend Coverage
→ Documented Acceptance Scenarios
→ Product/UX Acceptance
```

## 1.2 Complete ownership of assigned work

These rules apply to every future repository task, including modules, features, defect fixes, refactors, integrations, migrations, security, infrastructure, UX, tests, and documentation. For every implementation mission, operate as a **Senior Product Architect + Senior Product Designer + Senior Software / Systems Architect + Senior Developer / Engineer + QA / Integration Owner** for the assigned scope. Own the quality of the entire **authorized scope**; do not act as a ticket executor waiting for the user to find each defect. A full-module assignment covers the entire capability chain; an area-only assignment cannot be presented as module completion. Consult the canonical Module Pack whenever work affects a module, and apply the relevant verification to the assigned task. Work through `understand → reconcile documentation and code → plan → implement → run → test → inspect real behavior → compare with requirements → correct → retest` until every required scenario in the assigned scope passes.

Maintain a coverage matrix for each MVP capability: `canonical requirement → business rule → persistence → backend/API → Development Seed → frontend UX → automated test → real runtime proof → PASS/FAIL`. Mark a layer not applicable only with a concrete reason. Existing code, passing tests, and generated screenshots are not proof of an unverified link. A required FAIL remains work to diagnose and fix; do not turn it into a completion claim. Previewing a workflow does not prove its committed outcome, and tenant isolation does not by itself prove legal-entity isolation.

For work outside a full module mission, trace each affected requirement to its implementation and appropriate evidence. Scale checks to the changed behavior; documentation-only work does not require a database or browser run, while runtime user journeys require real-stack verification. This does not reduce any gate required for module completion.

Do not return partial progress merely because a solvable in-scope defect was discovered. For an authorized mission, discover → fix → verify → inspect → repeat; close known in-scope defects that can reasonably be corrected before the final report. For a defect owned by another layer, identify the precise boundary and evidence; fix it when the mission authorizes cross-layer work, continue independent work, and report a genuine remaining blocker without compensating with mocks or false business behavior. Incremental reports are appropriate only for a genuine external blocker, required destructive approval, or a materially unresolved locked product/architecture decision. The Senior Agent resolves ordinary implementation, workflow, and UX choices with sound judgment. The user is the final Product Owner, not the agent's primary tester or defect finder.

### 1.3 End-to-end mission ownership contract

Before coding an authorized implementation mission, perform one focused assessment of the canonical requirements, existing code and database state, users and workflows, ownership boundaries, lifecycle invariants, integrations, security/isolation, failure/concurrency/retry behavior, seed scenarios, and acceptance gates. Form an internal coverage/dependency matrix and a concise gap list; this is an execution aid, not a separate deliverable or a reason to pause for ordinary clarification.

Derive the critical acceptance scenarios before implementing each workflow and implement meaningful proof with the workflow. Vertical slices are internal execution units only: completing and testing one slice never completes a broader mission. After a slice passes, continue automatically through every remaining authorized requirement, including migrations, seed, APIs, workers, integrations, security, and acceptance tests. Do not convert a discovered gap into a request for a new prompt when it is solvable within the authorized scope.

For every substantial multi-step mission, keep a concise checkpoint in the existing applicable task or engineering document. It must state the authorized scope, completed and verified items, unfinished items, current failure or dependency, last meaningful change, next executable action, and evidence still valid. Update it after meaningful milestones and before a technical interruption. On resume, continue from repository state and checkpoint; do not restart discovery. Never claim background execution without an actual supported continuation mechanism.

### 1.4 Stopping and delivery statuses

Stop only after the authorized scope is complete, or when all independent work is exhausted and one genuine external blocker remains (locked product/legal/architecture decision, destructive approval, or unavailable authorization/infrastructure). Ordinary implementation choices, missing tests, local configuration, debugging, migrations, and code defects are engineering work, not escalation reasons. Use exactly one delivery status:

- `COMPLETE`: every applicable authorized requirement and acceptance gate is implemented and verified after the last relevant change.
- `BLOCKED`: an unavoidable external dependency prevents completion, with precise evidence and the independent work already finished.
- `INTERRUPTED`: execution ended because of a technical session/tool limit; include a resumable checkpoint and do not present the result as successful delivery.
- `NOT COMPLETE`: use when the authorized scope or evidence is still unfinished; never disguise it as a milestone or audit result.

Never report an internal implementation milestone, passing build, single workflow, or small test subset as completion of an unfinished mission. Preserve the distinctions between `BACKEND COMPLETE`, `FRONTEND COMPLETE`, `PRODUCT / UX ACCEPTANCE COMPLETE`, `INTEGRATION COMPLETE`, and `MODULE COMPLETE`.

# 2. Business first

Do not begin a feature or module from database tables, DTOs, controllers, React screens, or an existing framework pattern. First understand:

- who uses it and what real task they must complete;
- how real companies perform the normal workflow and its exceptions;
- who owns each fact, what may change, and what becomes immutable;
- permissions, accounting/financial consequences, and related modules;
- what must happen immediately and what may happen asynchronously.

Model real operating conditions, not only the happy path: company size, policies, approvals, reversals, cancellation, partial processing, concurrent users, backdated/future-dated operations, inactive and historical records, auditability, and correction without destroying history. Business correctness is more expensive to repair later than UI layout.

Make ordinary product and implementation decisions within the approved model. Think from the employee's and manager's real task: choose the simplest correct workflow that preserves truthful data, explainability, security, and future scale. When a workflow is underspecified and market practice would materially improve the choice, study relevant ERP/SaaS competitors and authoritative product documentation for terminology, interaction patterns, and user expectations. Record the reasoning where it will remain useful. Do not copy a competitor blindly or substitute market convention for a locked Module Pack decision.

# 3. User simplicity and language boundaries

The product is for ordinary business users. Aim for minimum required fields, an obvious next action, familiar terminology, progressive disclosure, no technical identifiers, and no unnecessary decisions. A user should not need ERP or accounting expertise merely to understand a label.

Keep these boundaries explicit:

```text
DATABASE LANGUAGE != DOMAIN LANGUAGE != API LANGUAGE != USER-FACING LANGUAGE
```

`TenantId`, `OrganizationId`, `LegalEntityId`, `PartyId`, `MfaChallengeId`, and `EntityRef` are internal concepts. They must not automatically become labels or input fields. Ask for a business concept only when the user genuinely understands and needs it. The frontend must not expose backend implementation details, SQL errors, stack traces, table names, or ORM terminology.

The product must make data easy to enter and read, reduce duplicate work and unnecessary steps, and let managers act on trustworthy information without technical interpretation or routine spreadsheet exports. Design each role around its job and progressively reveal advanced options. For every workflow, ask whether defaults can safely remove fields, the next action is obvious, the wording is natural to a normal employee, and the flow is faster than the current manual process. Technical correctness with needless friction is not sufficient: employees should want to work in the system.

Every user-facing screen must strictly follow the governed high-contrast corporate design system codified in `/frontend/AGENTS.md`. The application shell is anchored with the authentic Dark Oxford Navy header (`#102a43`), the page canvas uses cool neutral slate (`#f0f4f8`), and cards are pure crisp white (`#ffffff`) with solid perimeter borders (`#cbd5e1`) and dual-layer elevation shadows. Pale, borderless, low-contrast "ghost" screens where cards blend seamlessly into the background canvas are strictly prohibited as product defects.

## 3.1 Strict Separation of Concerns: Zero Mixed View/Form Collision

To compete with global ERP leaders (such as Odoo) in the Arabic enterprise market, user experience must offer total cognitive clarity. Ordinary business users (accountants, warehouse clerks, sales reps, cashiers) must never face visual chaos or ambiguous screens:

- **No Mixed Read/Write Screens**: A page must NEVER combine browsing data (tables, lists, 360-degree record profiles) with inline creation or editing forms on the same screen.
- **List Pages (`ListPage`)**: Dedicated exclusively to browsing, searching, filtering, and status assessment. Strictly forbidden to embed inline creation or edit form fields inside, beside, or below list tables.
- **Creation Workflows**: Must always be triggered by an explicit Primary Action button (e.g. `+ إضافة عميل`, `+ إنشاء فاتورة جديدة`). The button opens either:
  1. A focused, accessible **Modal Dialog** (`Dialog`) for quick/standard entity creation, OR
  2. A dedicated **Creation Page / Workspace Route** (e.g. `/new`, `/draft`) for complex documents and multi-line journals.
- **Edit Workflows**: Must always be triggered by an explicit edit action on a specific record, which opens a dedicated **Edit Modal Dialog** or navigates to a dedicated **Edit Route**.
- **Self-Explanatory microcopy**: No developer terminology, database identifiers, or raw status codes. Obvious next actions with clear visual hierarchy.

## 3.2 Arabic Market Vocabulary Charter: Zero Literal Machine Translation

To earn deep enterprise trust across Egypt, Saudi Arabia, and the Gulf, user-facing Arabic copy must strictly use **authentic market ERP terminology**. Literal dictionary or machine translations are strictly forbidden as product defects.

Every user-facing screen, label, status badge, validation message, and document title must follow the authoritative enterprise glossary codified in `.agents/skills/erp-frontend-architect/references/arabic-rtl-i18n.md`:
- **Accounting & Finance**: Use `قيد يومية` (never `إدخال اليومية` or `مجلة`); `ميزان المراجعة` (never `توازن التجربة`); `إشعار دائن` / `إشعار مدين` (never `مذكرة ائتمان` / `مذكرة خصم`); `دفتر الأستاذ العام` (never `سجل الحسابات`); `سند صرف` / `سند قبض` (never `قسيمة دفع` / `إيصال دفع`); `مطابقة كشف الحساب البنكي` (never `تسوية البنك`); `أرباح مبقاة` (never `أرباح محتجزة`).
- **Supply Chain & Inventory**: Use `أمر شراء` for Purchase Order (never `طلب شراء`, which is reserved for internal Requisitions); `إذن استلام بضاعة` for Goods Receipt (never `إيصال سلع`); `إذن صرف بضاعة` for Delivery Note (never `مذكرة تسليم`); `تسوية مخزنية` for Inventory Adjustment (never `تعديل المخزون`); `جرد مخزني` for Stock Count (never `عد المخزون`).
- **Commercial & CRM**: Use `عميل محتمل` for Lead (never `ليد` or `عميل متوقع`); `فرصة مبيعات` for Opportunity; `عرض أسعار` for Quotation (never `اقتباس` or `تسعيرة`); `مسار الصفقات` for Pipeline (never `خط الأنابيب`).
- **HR & Payroll**: Use `مسير رواتب` for Payroll Run; `قسيمة راتب` for Payslip (never `زلة الراتب`); `الراتب الأساسي` and `البدلات` (never literal machine words).
- **Actions & System States**: Use `إرسال للاعتماد` (never `تقديم`); `اعتماد` (never `موافقة`); `ترحيل` for Post (never `نشر`); `قيد عكسي` for Reversal (never `عكس` or `استرجاع`).

---

# 4. Locked repository structure

The repository structure is closed. Approved navigation roots are:

```text
backend/     frontend/     docs/
```

Backend contains `Api/`, `Worker/`, `DbMigrator/`, `Core/`, `Modules/`, `Shared/`, and `Tests/`. Frontend contains `app/`, `core/`, `components/`, `modules/`, and `tests/`.

All backend database ownership, SQL baselines, migrations, database backup/restore helpers, and migration verification files live under `backend/DbMigrator/Database/`. `ops/` is reserved for deployment and infrastructure assets; never create or reference `ops/database` for backend database work.

Do not recreate `src/`, `src/backend/`, `src/frontend/`, `frontend/src/`, `backend/hosts/`, `backend/platform/`, `frontend/platform/`, `apps/`, `packages/`, or another parallel platform without explicit owner approval. Do not add unnecessary nesting, speculative packages, compatibility shims, or empty folders.

# 5. Naming and file discipline

Technical names describe responsibility rather than product marketing. Never create a new file or folder whose basename contains `todir` in any casing, or phase/history prefixes such as `TODIR_`, `ERP_`, `P00_`–`P99_`, `Phase1_`, `Final_`, `New_`, `Latest_`, or `V2_`. User-facing product branding may remain in UI and product documentation. Avoid generic dumping areas named `Common`, `Helpers`, `Utils`, `Misc`, or `General` unless the responsibility is explicit and narrow.

Create files only in their governed directory. Do not put random SQL, scripts, logs, generated output, temporary files, or duplicate documentation beside source. Use canonical documentation locations and ignored `artifacts/logs`, `artifacts/test-results`, `artifacts/coverage`, `artifacts/performance`, or `artifacts/tmp` for local output.

# 6. Ownership and architecture

Every business module is the single writer and source of truth for its data. No module directly queries or writes another module's tables, schema, `DbContext`, repositories, internal services, or implementation. Cross-module interaction uses approved contracts, capabilities, public APIs, or events. Preserve an acyclic dependency graph and the existing Modular Monolith.

The PostgreSQL topology is fixed: the Platform Kernel owns the single `platform` schema; each real foundation or business module owns exactly one layer-prefixed schema such as `foundation_directory` or `business_sales`; `platform_migration` is reserved for migration history; `public` contains no application data. Do not recreate one schema per Platform capability, tenant, organization, or technical service.

Tenant isolation is a security boundary. Missing tenant context denies access. Backend authorization is authoritative; frontend visibility is UX only. Posted accounting records and completed stock movements are immutable. Immediate invariants use synchronous business contracts; post-commit integration uses approved Outbox/Inbox guarantees. AI never queries or writes the database directly and operates only through authorized application tools/contracts with tenant, permission, and audit controls.

Global core and modules remain country-neutral. Country-specific legal, tax, payroll, and compliance behavior belongs in approved Country Packs, providers, or adapters; do not scatter Egypt, Saudi, UAE, or other country conditionals through global code.

# 7. Backend/frontend mission separation

Backend and frontend are separate missions by default.

The backend mission owns business/domain modeling, database and migrations, repositories, services, APIs, backend security, messaging, backend tests, and runtime verification. A backend agent must not implement frontend work unless the owner explicitly assigns a combined mission.

The frontend mission owns user journeys, UX, screens, navigation, user-facing terminology, forms, components, API integration, RTL, responsive behavior, frontend tests, Playwright, and Product/UX acceptance. A frontend agent must not redesign database ownership, backend security, or backend business rules. If an API is insufficient for correct UX, report the required business capability/API change; do not hack business truth into the frontend.

# 8. Complete capability lifecycle

Use this sequence for a new business capability:

1. Business Discovery
2. Domain / Ownership Design
3. Backend + Database Design
4. Backend Implementation
5. Backend Verification
6. Product / UX Discovery
7. Frontend Design
8. Frontend Implementation
9. Integrated Runtime Verification
10. Product Acceptance

Only applicable stages that pass may support these statuses: `BACKEND COMPLETE`, `FRONTEND COMPLETE`, `PRODUCT / UX ACCEPTANCE COMPLETE`, and `INTEGRATION COMPLETE`. Frontend completion means `FRONTEND COMPLETE` + integrated Product/UX evidence + ready for human Product acceptance. Only the complete module lifecycle and explicit human Product Owner approval may reach `MODULE COMPLETE`. A passing build, existing API, frontend compilation, or Playwright click-through is never by itself module completion. The agent must finish and self-review all verifiable work before asking for human Product/UX acceptance; that human review is the final approval gate. Do not declare `MODULE COMPLETE` while required MVP coverage fails, evidence is incomplete, or human acceptance is outstanding.

User-visible backend capabilities must provide canonical realistic development seed scenarios before frontend handoff whenever seeded data is necessary to evaluate the product experience.

# 9. Local development and verification

All development and defect discovery happen locally. Use local builds, tests, Docker, PostgreSQL, RabbitMQ, and runtime checks. GitHub is storage/review infrastructure, not the normal test loop; do not push fixes to discover whether they work or wait for remote CI during implementation.

Before changing anything, inspect the repository and current status, read the applicable scoped manual and canonical architecture/module/API/ADR documents, inspect implementation and tests, identify ownership/dependencies/boundaries, then plan. Implement only approved scope, verify it, self-review it, fix clear in-scope defects, and re-verify. Compilation alone is not completion. Never weaken a security or architecture test to make a gate pass.

For a user-visible runtime journey, final acceptance uses the real frontend, real API, real PostgreSQL, and canonical Development Seed; use a disposable local database for mutating acceptance where appropriate. Mocks, Storybook, fixtures, and synthetic screenshots support development but cannot replace this proof. Open and inspect every acceptance screenshot before reporting it. Check actual record counts and business states, Arabic copy, RTL, mobile overflow, empty/loading/error states, permissions, technical leaks, safe destructive actions, and consistency between screenshots and reported results. If evidence exposes a defect, correct it and recapture it. Do not claim a populated, completed, or isolated scenario from evidence that only shows a partial state.

## 9.1 Final Return Gate — Evidence Must Follow the Last Change

Every final report must be supported by verification performed after the last relevant source, configuration, seed, migration, or UX change. A later change invalidates earlier evidence for every affected behavior. Complete this sequence before returning:

```text
last relevant change
→ run the affected system
→ rerun required verification
→ capture fresh evidence
→ inspect the fresh evidence
→ correct any in-scope defects
→ repeat the affected sequence if anything changes
→ final report
```

Do not return between capturing evidence and inspecting it. Do not reuse evidence captured before a later change that could affect the same journey, language, component, layout, formatting, or data state. The agent owns this final review; Product Owner review is approval of self-reviewed work, not a defect-discovery step. If a final review finds a defect, fix it and repeat all affected verification and evidence steps before reporting.

# 10. Git and completion discipline

Work directly on `dev`; `main` is frozen and must not be touched without explicit owner authorization. A normal implementation mission is `discover → design → implement → targeted tests → full local verification → one commit → one push`. Do not make intermediate commits/pushes, create phase branches, or create routine PRs unless explicitly requested. Never rewrite published history or force-push.

### GitHub automation

Place CI workflows, pull-request automation, repository automation, and authorized security/dependency automation under `.github/`; do not put application logic there. Introduce workflows only when toolchain and operational architecture authorize them. Workflow filenames must be short, responsibility-based kebab-case without brand or phase prefixes (for example, `validation.yml`, `security-scan.yml`, or `deploy-staging.yml`); keep the workflow set small. Never create or introduce a file or folder whose basename contains `todir` in any casing.

Do not reopen completed work without a real defect, a new requirement, or an explicit owner decision. Avoid unrelated cleanup. Stop only when destructive owner approval, an unresolved product decision, unavailable required access/infrastructure, or a locked architectural conflict genuinely blocks the assigned mission.

# 11. Comments and reporting

Code comments are English and explain why: business invariants, security, performance, concurrency, transaction, retry, or architectural decisions. Do not narrate obvious mechanics.

Before reporting, review scope, ownership, boundaries, data integrity, tenant/RLS security, permissions, API contracts, query/index behavior, performance, tests, documentation, warnings, stale code, and temporary artifacts. Confirm that every reported verification and evidence item follows the last relevant change. A module completion report must identify the canonical pack, business journeys, architecture decisions, database/migration and seed results, security and tenant isolation, API contracts, real browser/UX evidence, exact test totals, deferred scope, genuine external limitations, changed files, commit SHA, and remote status. Report exact PASS/FAIL results and decisions still needed. Never claim unmeasured coverage, a passing gate, or completion while an in-scope defect can reasonably be fixed in the current mission.

# 12. Fast execution mode

This instruction controls HOW every mission is executed across the entire repository. It does NOT reduce correctness, security, business completeness, or final verification. Its purpose is to prevent over-analysis, repeated reading, repeated test loops, benchmark rabbit holes, and unnecessary repository exploration.

### 12.1 One-pass study

Read root `AGENTS.md`, the applicable scoped manual (`backend/AGENTS.md` or `frontend/AGENTS.md`), the canonical Module Pack under `docs/modules/`, and directly relevant existing code. ONE focused pass is enough. Do NOT repeatedly reread the full Pack or the same source files unless a concrete failing behavior requires it. Do NOT explore unrelated modules "for completeness". After the first pass, produce internally a short gap list and start implementation immediately.

### 12.2 Gap-driven execution

Before coding, identify only:

- what already exists and is correct;
- what is missing;
- what is wrong;
- what must be proven before completion.

Work ONLY on those gaps. Do not rebuild working areas. Do not redesign already-approved architecture. Do not clean unrelated code. Do not refactor for taste.

### 12.3 Analysis must end in action

Do not spend long periods studying without changing or verifying something. After understanding a requirement, immediately do one of: implement it, test it, or inspect the exact code path that blocks it. Avoid broad speculative analysis.

### 12.4 No repeated command loops

If the SAME test or command fails twice: STOP rerunning it unchanged. Instead: inspect the exact error, inspect the relevant log, inspect the exact implementation path, make one targeted fix, and rerun the focused test. Do not run the same failing Playwright/test command multiple times hoping behavior changes.

### 12.5 Focused tests during development

During implementation, run only tests relevant to the code being changed (e.g. changed FinBase component → FinBase unit test only; changed entity-context → entity-context + affected module tests; changed one E2E flow → that exact E2E flow). Do NOT run the full repository test suite after every edit.

### 12.6 Full gates only once at the end

The full gate belongs strictly after the LAST meaningful change. Only then run the full suite: build, architecture, localization, typecheck, unit tests, API drift, integration/security where applicable, full Playwright, release gates, and git diff checks. If the final gate exposes a defect: fix the defect, rerun only affected checks, and then execute one fresh final gate.

### 12.7 Environment debugging limit

Do not spend hours investigating Windows, Docker, WSL, antivirus, Code Integrity, ports, SDK installation, or local machine internals unless the environment truly blocks the mission. First prefer: an already-working local service, an existing clean database, the repository's approved commands, and the simplest supported local path. If an environment issue is unrelated to product implementation, do not turn it into a subsystem investigation.

### 12.8 No performance rabbit holes

Do not create large-scale benchmarks, loaders, synthetic datasets, staging frameworks, or tuning experiments unless the Product Owner explicitly requests a Performance Hardening mission. Normal module implementation requires only: pagination, bounded queries, indexes, no obvious N+1, reasonable query plans, and normal responsive UI behavior. Large-scale certification is separate.

### 12.9 No premature polish

Order of work:

```text
business correctness
→ persistence/API correctness
→ integration
→ core UX
→ localization/responsiveness
→ visual polish
→ final verification
```

Do not spend long periods polishing screenshots before the workflow actually works.

### 12.10 Reuse approved patterns

When TODIR already has an approved implementation pattern, reuse it (repository/service structure, TanStack Query setup, dialogs, tables/cards, localization, error mapping, pagination, entity context, permissions). Do not research or invent a new pattern unless the existing pattern genuinely cannot satisfy the requirement.

### 12.11 Do not implement future modules

Stay inside the current module. If the Pack mentions future integrations, provide contracts/seams/tests only where required. Do not build future modules to prove readiness.

### 12.12 Autonomous senior decisions

For ordinary implementation choices, MAKE THE SENIOR DECISION YOURSELF. Do not stop to ask the Product Owner about normal naming, normal UI placement, obvious validation, ordinary component structure, standard API wiring, or routine test organization. Ask only when there is a genuine product/domain decision with multiple materially different business outcomes.

### 12.13 Silent execution and truthful interruption reporting

Do not send progress narration such as "I am now inspecting...", "I will next review...", "the test is still running...", or "I will verify when it completes...". Continue working silently. Return only with `COMPLETE`, `BLOCKED`, `INTERRUPTED`, or `NOT COMPLETE` as defined in Section 1.4. A technical session/tool limit is `INTERRUPTED`, never successful completion; persist or reference the latest checkpoint and the exact resumable next action. A genuine external blocker is reported only after independent authorized work is exhausted.

### 12.14 Execution target

The goal is NOT maximum analysis. The goal is:

```text
MINIMUM NECESSARY ANALYSIS + CORRECT IMPLEMENTATION + STRONG FINAL PROOF
```

Prefer completing a correct vertical slice over studying every possible future edge case.

### 12.15 Anti-loop self-check

Before any repeated read/test/tool action ask: _"Will this produce new information?"_ If NO, do not run it.
Before creating any new abstraction/tool/test harness ask: _"Is this required to ship the current mission?"_ If NO, do not build it.
Before touching an already-passed area ask: _"Do I have a concrete regression or requirement gap?"_ If NO, leave it alone.

### 12.16 Final mission reconciliation

Before the final response, reconcile the original authorized request against the internal coverage matrix and the actual diff. For every applicable requirement, confirm implementation, relevant automated tests, real integration/runtime evidence where required, security/authorization and isolation, persisted business outcomes, seed coverage, and post-change verification. Reuse earlier evidence only when the final changes cannot invalidate it. If a final gate exposes an in-scope defect, fix it, rerun the affected checks, and perform one fresh final gate before reporting. Report unrelated repository failures separately and never claim whole-repository release readiness while mandatory global gates are red.
