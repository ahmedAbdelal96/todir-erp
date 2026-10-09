# ADR-021 — Frontend Platform Foundation and Design System Baseline

Status: Accepted

## Context

The initial engineering baseline in `02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md` specified MUI and MUI X Data Grid as default frontend choices. However, comprehensive evaluation in `docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md` established critical architectural requirements:
1. **Brand-Agnostic Foundation:** Reusable design system tokens, components, and packages must not be coupled to or named after any single commercial brand.
2. **Free-Only Core Dependency Rule:** The core ERP platform must never require paid/commercial runtime libraries or SaaS licenses to build, run, test, or operate.
3. **Arabic and RTL First-Class Quality:** Full bidirectional support using CSS logical properties and robust keyboard/screen-reader accessibility from Day 1.
4. **Exact Financial Value Semantics:** Exact decimal representation (`DecimalString`) must be preserved across boundaries without binary floating-point (`number`) distortion.
5. **Decoupled Architecture & Spikes:** Primitive components and data grids must be selected through objective, evidence-based spikes with clean platform abstraction boundaries.

## Decision

1. **Retained Technologies:**
   - React (latest stable), TypeScript strict mode, Vite SPA.
   - React Router for routing.
   - TanStack Query v5 for server state and caching.
   - React Hook Form + Zod for client-side form management and validation.
   - OpenAPI-generated TypeScript client as the authoritative transport contract with ASP.NET Core backend.
   - i18next / react-i18next for localization and RTL/LTR switching.
   - Vitest, React Testing Library, and Playwright for testing.
   - Tailwind CSS v4 for internal utility styling within the design system.

2. **Reconciled & Superseded Choices:**
   - **MUI is no longer the mandatory frontend foundation.** It is superseded by a brand-neutral, token-driven design system built upon an unstyled headless primitive foundation chosen via the F06 spike (React Aria Components vs Base UI).
   - **MUI X Data Grid is no longer the mandatory grid foundation.** It is superseded by a platform `DataGrid` abstraction backed by an open-source/free grid engine chosen via the F07 spike (AG Grid Community vs TanStack Table + Virtual).
   - **Strict Free-Only Rule:** AG Grid Enterprise, MUI Pro/Premium, or any other commercial-tier library are strictly forbidden in core.
   - **Strict Brand Neutrality:** Reusable frontend foundation packages (`@todir/ui`, `@todir/design-tokens`, etc., use brand-neutral names like `@todir/tokens`, `@todir/ui` or neutral library boundaries; component APIs never use `Todir` prefixes like `TodirButton`).
   - **Theme Engine:** Token-driven pipeline (`ThemeDefinition` → validation → perceptual OKLCH color derivation → semantic tokens → component tokens → CSS custom properties). Arbitrary tenant CSS injection is strictly forbidden.
   - **Business Truth Authority:** The backend remains the sole authority for business rules, authorization, and financial calculations.

## Consequences

- The frontend repository baseline is internally consistent with `docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`.
- No dual/competing frontend component stacks exist in the codebase.
- Future frontend modules import domain-safe semantic tokens and abstracted primitives without vendor lock-in.
- Architecture tests and linters enforce module boundaries, package isolation, and the absence of paid dependencies.

## Spike Outcomes & Decisions

1. **F06 Primitive Library Decision:**
   - **Winner: React Aria Components (@react-aria / react-aria-components v1.6.0)**
   - Score: React Aria Components 95.20 vs Base UI 85.20.
   - Justification: React Aria achieved full pass across all 9 scenarios, specifically demonstrating superior DateField / CalendarDate (timezone-safe LocalDate isolation), robust bidirectional Arabic/RTL mechanics, and compatibility with exact string decimal editing (`DecimalString`) without forcing binary floating-point `number` coercion.
   - Licensing: Apache-2.0 (100% Free Open Source).

2. **F07 DataGrid Foundation Decision:**
   - **Winner: AG Grid Community (@ag-grid-community v33.0.0)**
   - Score: AG Grid Community 93.60 vs TanStack Table + Virtual 80.70.
   - Justification: Provides a complete out-of-the-box ERP grid engine with native keyboard navigation, cell focus, virtualized rendering for 100k+ rows, column resizing, density support, and Arabic RTL scroll/theming without requiring TODIR to build and maintain a custom spreadsheet engine.
   - Licensing: MIT (Free Open Source). AG Grid Enterprise is strictly forbidden and never imported.

## Evidence

- `docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`
- `docs/architecture/02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- `tests/spikes/primitives/primitive-spike.test.ts` (F06 Primitive Spike Suite)
- `tests/spikes/grid/grid-spike.test.ts` (F07 Grid Spike Suite)
- `src/frontend/src/design-system/grid/grid-contract.ts` (Design System DataGrid Boundary)

## Physical Structure Clarification

TODIR currently has one frontend application rooted at `src/frontend/` with source under `src/frontend/src/`. The logical `app`, `platform`, `design-system`, and `modules` boundaries are enforced by TypeScript aliases and architecture validation; they do not require separate npm packages. Package extraction remains possible when independent deployment, independent versioning, proven cross-application reuse, or a build/tooling need justifies it.
