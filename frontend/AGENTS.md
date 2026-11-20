# Frontend Product, UX, and Engineering Operating Manual

Read the repository root `AGENTS.md` first. This file governs all frontend architecture, UX, and UI engineering. Together with the root manual, it provides the operational standard to deliver `FRONTEND COMPLETE`, integrated Product/UX evidence, and a result ready for human Product acceptance. Only the complete module lifecycle plus explicit human Product Owner approval may reach `MODULE COMPLETE`.

Never create a new file or folder whose basename contains `todir` in any casing; technical names describe responsibility rather than product marketing.

---

## 00. Mandatory Skill Activation: `erp-frontend-architect`

**CRITICAL RULE FOR ALL AGENTS:** Every agent undertaking ANY frontend work (including designing, coding, modifying, reviewing, simplifying, or debugging screens, forms, tables, drawers, dialogs, workflows, components, or i18n files) MUST mandatorily load, activate, and follow the `erp-frontend-architect` skill (`.agents/skills/erp-frontend-architect/SKILL.md`).

Before writing or modifying any frontend code, the agent must strictly execute:
1. **The Discovery Gate & Pre-Code Card (Step 0 & Step 2)**:
   - Identify who uses the screen, their device, and pressure level.
   - Establish the single primary next action and its consequence.
   - Cut unnecessary inputs by ~50% (system derives defaults; minimal fields).
   - Account for all 6 states: loading skeleton, guided empty state, actionable error with retry, no permission, partial data, stale data.
2. **Strict Separation of Concerns (Browse vs Enter vs Inspect)**:
   - Tables and lists are exclusively for browsing and status assessment; never mix inline forms with tables.
   - Quick entry (1-4 fields) -> centered Modal Dialog (`Dialog`).
   - Medium entry / line items (5-12 fields) -> Side Drawer.
   - Large documents / multi-stage (13+ fields) -> dedicated creation page with stepper.
   - Clicking a table row MUST open the Detail Drawer (preserving URL filters, pagination, and scroll position).
3. **Arabic-First, RTL-Native & Market Vocabulary Charter**:
   - **Market Arabic Over Literal Translation**: Every user-facing term, label, badge, and message MUST strictly follow the authoritative enterprise glossary in `.agents/skills/erp-frontend-architect/references/arabic-rtl-i18n.md`. Never use literal machine translations (e.g. use `قيد يومية` not `إدخال اليومية`; `ميزان المراجعة` not `توازن التجربة`; `إشعار دائن` not `مذكرة ائتمان`; `إذن استلام بضاعة` not `إيصال سلع`; `عميل محتمل` not `ليد` or `عميل متوقع`; `أمر شراء` not `طلب شراء`).
   - Strictly use logical CSS properties (`margin-inline-start`, `padding-inline-end`, `start`, `end`).
   - Flip directional icons in RTL; do not flip non-directional icons (checks, search).
   - Strict bidirectional text isolation (`<bdi>` or `unicode-bidi: isolate` / `<BdiText>`) around Latin codes (`INV-2026-001`), phone numbers, dates, and amounts inside Arabic copy.
   - Zero technical leakage: Never expose database IDs, table names, raw enum codes, or untranslated fallback English to Arabic business users.
4. **TODIR Design System Precedence**:
   - All visual layouts must strictly use TODIR's codified design tokens: authentic Dark Oxford Navy header (`#102a43`), slate background canvas (`#f0f4f8`), crisp white surface cards (`#ffffff`) with solid perimeter borders (`#cbd5e1`) and dual-layer elevation shadows.
   - Reusable primitives from `frontend/components/` must be used; uncompiled Tailwind utility classes are strictly prohibited.
5. **Quality Verification Ladder (Step 4)**:
   - Run typecheck, `pnpm run architecture:frontend`, `pnpm run localization:verify`, `pnpm run api:drift`, and real Playwright E2E workflows before declaring any task complete.

---

## 0. Frontend Architecture Map & Directory Structure

The repository structure is locked. The approved frontend navigation roots are:

```text
frontend/
├── app/         # Bootstrap, application providers, router, AppShell, topbar, user menu
├── core/        # API transport/httpClient, session/auth, tenant context, permissions, query cache
├── components/  # Design system tokens, accessible primitives, fields, forms, grid, page archetypes
│   ├── fields/      # Domain-aware display & input fields (MoneyDisplay, MoneyField, StatusBadge, etc.)
│   ├── forms/       # Form structure (FormField, FormSection, FormActions, ValidationSummary)
│   ├── grid/        # Responsive DataGrid, column definitions, and specialized cells
│   ├── pages/       # Reusable page archetypes (ListPage, RecordPage, DocumentPage, WizardPage, Surface, PropertyGrid)
│   └── primitives/  # Accessible UI primitives (Button, Dialog, Select, ComboBox, Table, Pagination, Icons, etc.)
├── modules/     # Autonomous vertical business slices (directory, catalog, invoicing, accounting, payments, etc.)
└── tests/       # Global, contract, accessibility (axe), browser E2E (Playwright), and visual tests
```

### Module Boundary Rules
1. `app/` owns composition, shell, topbar, navigation registry, and router. It never contains business domain logic.
2. `app/router/route-contract.ts` defines `NavigationCategory` (7 shallow groups: `workspace`, `sales`, `purchasing`, `inventory`, `finance`, `hr`, `admin`), `PermissionCondition` (`string | { anyOf?: string[]; allOf?: string[] }`), and `NavDestination` (discrete sub-tasks).
3. Primary navigation in `app-shell.tsx` groups modules by `NAVIGATION_CATEGORIES`. Modules must never be dumped as an unorganized list of 24 flat links. Active modules display permitted contextual task sub-destinations beneath the module entry.
4. Single-permission module gates are strictly forbidden. Never gate an entire operational module behind a single coarse permission (e.g. `banking.accounts.read` or `crm.lead.read`). If a user has any valid sub-capability (e.g. `banking.cash.count` or `crm.activity.read`), the module and permitted destinations must remain accessible.
5. All route authorization is backed by `GET /api/v1/authorization/effective-access` which resolves scoped permissions for the active Legal Entity and Branch. Never evaluate unscoped global claims. Direct URL navigation to forbidden routes must fail closed via `<PermissionGuard>` and display `<DefaultAccessDeniedView>`.
2. `core/` owns cross-application technical infrastructure and never imports from `modules/`. Authentication binds exactly one Tenant server-side from the authenticated session; frontend code must not invent or send Tenant identity as a normal API parameter. A workflow that genuinely requires legal-entity context must obtain it from authorized business/session context, never a fake default ID. Query keys must include only authorization and business scopes that materially change the server response; tenant-level resources must not be artificially fragmented by LegalEntityId.
3. `components/` is the single source of truth for all reusable UI primitives, fields, grids, and page archetypes. Modules NEVER build duplicate buttons, custom tables, bespoke pagers, or unstyled card wrappers.
4. `modules/<module-name>/` is self-contained. Every business module owns its own:
   - `pages/`: Page archetype implementations (`<entity>-list-page.tsx`, `<entity>-record-page.tsx`).
   - `features/`: Complex modal dialogs, workflows, and form sheets (`create-<entity>-dialog.tsx`).
   - `components/`: Domain-specific components only (e.g. `party-picker.tsx`, `account-picker.tsx`).
   - `services/`: API client adapters using `httpClient`.
   - `hooks/`: TanStack Query hooks using `queryKeyFactory`.
   - `validation/`: Zod schemas and validation rules.
   - `types.ts`: Domain models and DTO shapes.
   - `routes.tsx`: Sub-routes definition conforming to `ModuleRouteRegistration`.
   - `public.ts`: The explicit, minimal public export barrel for cross-module integration.
   - `tests/`: Slice-level component and integration tests.

---

## 0.1 Canonical Module Documentation Governance

Every frontend module mission must identify and read the exact current Module Pack under `docs/modules/` before design or implementation. Read the documented product vision, intended users, business workflows, canonical terminology, roles and permissions intent, lifecycle and edge cases, UX intent, MVP scope, explicitly deferred scope, development seed scenarios, acceptance scenarios, and Definition of Done. The Module Pack defines **WHAT** and **WHY**; this manual defines **HOW**. Keep referring to the pack during implementation and verification.

Design from the documented business workflow and user task, not from DTOs, database columns, existing endpoints, or an existing screen pattern. Use the pack's business terminology, progressive disclosure, and realistic seeded scenarios. Do not hide a documented journey, invent UI behavior by assumption, silently reduce scope, or treat passing TypeScript, component tests, or Playwright clicks as UX completion.

Before frontend completion, inspect the backend and API contract against the Module Pack. If a documented user journey lacks the backend capability, contract, permission, or data needed for a correct experience, stop that journey and report the exact backend gap and affected acceptance scenario; do not work around business truth in the frontend. Frontend completion requires the documented user journeys and acceptance scenarios to work with canonical development seed data, or each uncovered item to be explicitly deferred by the Product Owner with its reason and impact recorded.

Every frontend module mission report must include the canonical Module Pack path, documented product/UX scope, acceptance scenarios, MVP and deferred items, backend/API gaps, seed scenarios used for verification, and unresolved gaps.

## 0.2 Localization is a frontend completion requirement

Use the single `i18next` runtime as the source of truth for language. All user-facing copy, including labels, messages, validation, accessibility text, statuses, notifications, and navigation, must come from translation resources through `react-i18next`; hardcoded Arabic or English UI copy in application components, pages, features, forms, and dialogs is not allowed. Put shared/application terminology in `core/i18n`; each implemented module owns `modules/<module>/i18n/ar.ts` and `en.ts`. Keep equivalent Arabic/English key coverage and treat missing keys as defects. Arabic mode must not leak English UI copy and English mode must not leak Arabic UI copy. Preserve business and user-entered data as stored. Language persistence, document `lang`/`dir`, RTL/LTR, dates, numbers, money, and supported component locale must derive from the same runtime localization state. `FRONTEND COMPLETE` is blocked until localization coverage and both-language runtime acceptance are complete.

Run the localization coverage and hardcoded-copy guard in verification. The guard must parse frontend source structurally and check translation resource parity; narrowly document syntax-based exclusions such as tests, stories, API routes, CSS, identifiers, and business data. Never disable the guard broadly or use the other language as silent fallback.

### Frontend ownership and real Product/UX acceptance

Apply the root Senior Product Architect, Senior Product Designer, Senior Software / Systems Architect, Senior Developer / Engineer, and QA / Integration Owner directive to frontend work, with particular ownership of user workflows, accessible RTL UX, responsive behavior, and browser evidence. Apply this loop to every future frontend task, including defect fixes, UX, copy, routing, accessibility, responsive behavior, API integration, and tests. Do not return partial progress for a solvable in-scope defect: discover, fix, verify, inspect, and repeat. Verify affected runtime behavior in the real browser; a full module completion claim still requires every documented journey and gate.

Own the full frontend journey in the assigned scope: user tasks and role-specific decisions, workflow design, natural Arabic and RTL, API integration, permissions, loading/empty/error states, accessibility, desktop/mobile behavior, automated tests, real browser execution, screenshot inspection, and correction of Product/UX defects. Use the root coverage matrix to reconcile each MVP journey with the Module Pack, the actual backend contract, canonical seed, and observed runtime behavior. Do not implement only the visible screen or return because one Playwright click-through or screenshot succeeded.

Before implementing each frontend workflow, derive its critical user, permission, localization, responsive, error, retry, and committed-outcome scenarios. Implement the journey and meaningful component/browser proof together, then continue to every remaining authorized journey in the same mission. A passing screen, TypeScript build, screenshot, or vertical slice is not a delivery boundary. Keep the applicable mission checkpoint in existing task or engineering documentation and resume from it after interruption rather than restarting discovery.

Design for the employee doing the work and the manager reading the result. Make the common action obvious, remove safe-to-default fields and repeated entry, show trustworthy business state without requiring technical interpretation, and reveal advanced options only when useful. Use established ERP/SaaS patterns as evidence when they improve an underspecified decision, then choose the simplest workflow suitable for this Arabic-market product. Do not mirror DTOs, expose raw backend states, or make users supply internal identifiers.

Run final user journeys against the real frontend, real API, real PostgreSQL, and canonical Development Seed; use a disposable database for mutating acceptance when appropriate. Mocks, fixtures, Storybook, and simulated Playwright states do not establish final Product/UX acceptance. Open every captured screenshot and check that it visibly proves the claimed populated state, completed operation, correct Arabic copy, RTL, responsive layout, and absence of technical leaks. Check the underlying response and resulting state where screenshots cannot prove them. A preview is not proof of a committed action; tenant denial is not proof of legal-entity scope. Fix frontend-owned defects and recapture evidence before returning. For a backend-owned gap, state the exact missing capability and affected scenario, continue independent frontend work, and do not fabricate a frontend workaround.

### Final visual self-acceptance gate

For every frontend mission that uses browser screenshots as evidence, personally open and inspect every final screenshot after the last relevant change. The screenshot itself is the review artifact; successful capture or automated assertions alone do not establish visual acceptance. Inspect the actual business state, active language and copy, wrong-language leakage, RTL/LTR and mixed-direction text, date/number/money formatting, clipping and ellipsis, overflow, spacing and hierarchy, desktop and mobile behavior, loading/empty/error states, permissions, technical IDs/raw enums/backend messages, misleading labels or statuses, destructive-action safety, and consistency between the evidence and report. Review every category that applies to the captured journey; do not infer it from another screenshot.

If self-review finds a frontend-owned defect, fix it, rerun affected verification, recapture all evidence affected by the fix, and inspect those new screenshots before returning. Evidence must represent the final code and runtime state; do not reuse a screenshot if a later change could affect its journey, language, component, layout, formatting, or data. Every frontend/Product acceptance report that includes browser evidence must state `FINAL VISUAL SELF-REVIEW: PASS` and list the final screenshots opened, languages and viewports inspected, journeys reviewed, and defects found and fixed during self-review. Do not report `FRONTEND COMPLETE` or `PRODUCT REVIEW READY` without completing this gate.

Do not report `FRONTEND COMPLETE` while an authorized journey, backend integration, permission boundary, localization path, responsive state, browser proof, or required gate remains unfinished. If a technical session limit interrupts execution, report `INTERRUPTED` with the checkpoint and exact next action; use `BLOCKED` only for a genuine external dependency after independent frontend work is exhausted.

---

## 1. Core Product & UX Principles

### Simple Outside. Powerful Inside.
TODIR is an Arabic-first, multi-tenant enterprise ERP SaaS designed for people who spend 6 to 10 hours daily inside the interface.

* **CALM & FOCUSED**: No visual noise, unnecessary gradients, decorative glow effects, or jarring animations.
* **HIGH-DENSITY & SPACE-EFFICIENT**: ERP users need information density without clutter. Use compact spacing, clear typography, and structured grids.
* **FAST & RESPONSIVE**: Every click provides instant feedback. Loading states use spinners/skeletons, never blank screens.
* **PREDICTABLE & CONSISTENT**: Every page feels like it belongs to the exact same software suite. A user who learns one list page or record page automatically knows how to use all other 50+ modules.

Product success means employees prefer using the system for daily work because entry is faster, information is easier to read, and the next action is clear. A technically working screen that forces unnecessary data entry, hides the decision-relevant state, or makes the user decode developer language does not meet this standard.

### 1.1 Senior Frontend UI/UX Design System & Visual Excellence Standards

Every user-facing screen, card, form, table, and component in the ERP must strictly adhere to these non-negotiable visual and interaction standards. Any pull request or implementation that violates these rules is considered defective:

#### 1. Authentic Corporate Brand Shell & Dark Oxford Navy Topbar (`#102a43`)
* **Dark Topbar Grounding**: The primary application header (`.topbar`) is strictly anchored with authentic Dark Oxford Navy (`#102a43`), border-bottom `#0c1f31`, and deep drop shadow `0 4px 12px -2px rgba(16, 42, 67, 0.25), 0 2px 4px -1px rgba(0, 0, 0, 0.1)`. A pale or washed-out white topbar is forbidden as it strips visual authority and prestige from the application.
* **TopBar High-Contrast Controls**:
  * **Brand Title**: Pure white `#ffffff`, font-weight 800, letter-spacing `0.04em`.
  * **Brand Icon**: 34x34px container with vibrant royal blue gradient `linear-gradient(135deg, #2563eb 0%, #3b82f6 100%)`, bold white letter `T`, and subtle blue glow `0 2px 8px 0 rgba(37, 99, 235, 0.45)`.
  * **Company Pill**: Translucent frosted navy container (`background: rgba(255, 255, 255, 0.12)`, `border: 1px solid rgba(255, 255, 255, 0.22)`, `color: #ffffff`, `backdrop-filter: blur(8px)`), with an illuminated `#93c5fd` building icon.
  * **Context Selectors (Legal Entity & Branch)**: Crisp pure white pill card (`background: #ffffff`, `border: 1px solid #cbd5e1`, `box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.12)`). Labels must be dark slate `#475569` font-weight 700; dropdown text must be `#0f172a` font-weight 600. This white card inside the dark navy header provides immediate visual contrast and legibility.
  * **Language Switcher & User Menu**: Frosted glass buttons (`background: rgba(255, 255, 255, 0.12)`, `border: 1px solid rgba(255, 255, 255, 0.22)`, `color: #ffffff`). The user menu trigger features a vibrant avatar badge (`linear-gradient(135deg, #2563eb 0%, #3b82f6 100%)`) with white initials.
  * **User Dropdown Popover**: Pure crisp white card (`background: #ffffff`, `border: 1px solid #cbd5e1`, `box-shadow: var(--shadow-xl)`), maintaining high-contrast readability.

#### 2. Sidebar Navigation & Active Route Anchor
* **Resting Links**: Clean slate typography `color: #334e68`, font-size `0.875rem`, font-weight 600, with smooth transitions.
* **Hover State**: Subtle slate tint `background-color: #f0f4f8`, border `1px solid #cbd5e1`, text `color: #102a43`.
* **Active Route Indicator**: Active navigation items receive an authoritative Oxford Navy gradient `linear-gradient(135deg, #102a43 0%, #1e3a5f 100%)`, pure white text `color: #ffffff`, bold font-weight 700, and a deep elevation shadow `0 4px 8px -2px rgba(16, 42, 67, 0.35)`.
* **Active Icon Badge**: Active route icons are housed inside an illuminated container (`background-color: rgba(255, 255, 255, 0.18)`, `color: #93c5fd`).
* **Section Labels**: Upper-case bold slate headers `color: #486581`, font-size `0.75rem`, font-weight 800, letter-spacing `0.05em`.

#### 3. Anti-Washout Invariant & Canvas-to-Card Contrast
* **Canvas Grounding**: The application background canvas must NEVER be pure white (`#ffffff`) or faint near-white (`#f8fafc`). It must strictly use the cool neutral slate canvas: `var(--color-surface-page, #f0f4f8)` / `oklch(0.965 0.01 240)`.
* **Card Elevation**: Master cards, operational surfaces, dialogs, and table containers must be pure crisp white (`var(--color-surface-card, #ffffff)` / `oklch(1.0 0 0)`).
* **Anti-Washout Invariant (Ghost UI Defect)**: Faint, borderless, low-contrast "ghost" screens where cards blend seamlessly into the background canvas are strictly classified as product defects. Cards must always visibly pop out from the canvas with clear separation.

#### 4. Composite Multi-Layer Elevation & Shadow System
Never use single-layer faint shadows (< 0.05 opacity). Every elevated surface must have physical depth created by dual-layer ambient occlusion + directional key light:
* **`--shadow-xs`**: `0 1px 2px 0 rgba(15, 23, 42, 0.05)` (subtle controls, search inputs, filter bars, segmented buttons).
* **`--shadow-sm`**: `0 1px 3px 0 rgba(15, 23, 42, 0.08), 0 1px 2px -1px rgba(15, 23, 42, 0.05)` (standard cards, `Surface`, `StatCard`, tables, form sections).
* **`--shadow-md`**: `0 4px 6px -1px rgba(15, 23, 42, 0.08), 0 2px 4px -2px rgba(15, 23, 42, 0.04)` (hover states, dropdown menus, popovers).
* **`--shadow-lg`**: `0 10px 15px -3px rgba(15, 23, 42, 0.09), 0 4px 6px -4px rgba(15, 23, 42, 0.04)` (modals, sheets, floating toolbars).
* **`--shadow-xl`**: `0 20px 25px -5px rgba(15, 23, 42, 0.1), 0 8px 10px -6px rgba(15, 23, 42, 0.04)` (dialogs, critical action confirmation sheets, drawer overlays).

#### 5. Crisp Border Hierarchy & Specular Highlights
* **Outer Boundaries**: Card and container perimeters must use crisp slate: `1px solid var(--color-border-default, #cbd5e1)`. Invisible or washed-out borders (`#f1f5f9` or `#f8fafc`) on outer card boundaries are forbidden.
* **Internal Dividers**: Headers, footers, and table rows must use clean dividers: `1px solid var(--color-border-subtle, #e2e8f0)`.
* **Surface Card Headers**: Every `Surface` card header must feature a subtle contrast background (`var(--color-surface-subtle, #f8fafc)`), bold typography (`fontWeight: 700`, `letterSpacing: "-0.01em"`), and a 1px bottom border.

#### 6. Vibrant Enterprise Color & Semantic Intent System
* **Brand Primary**: Authoritative Oxford Navy (`#102a43`) with Royal Blue accents (`#2563eb`), providing high-contrast visual focus.
* **Typography Hierarchy**: Primary text must be deep Slate 950 (`#0f172a`), secondary text Slate 700 (`#334155`), and muted text Slate 500 (`#64748b`). Never use light gray for labels, table headers, or data values.
* **Protected Status Intents**:
  * **Success (Emerald)**: `#059669` base, `#ecfdf5` subtle bg, `#a7f3d0` border, `#065f46` content.
  * **Warning (Amber)**: `#d97706` base, `#fffbeb` subtle bg, `#fde68a` border, `#92400e` content.
  * **Danger (Rose/Red)**: `#dc2626` base, `#fef2f2` subtle bg, `#fecaca` border, `#991b1b` content.
  * **Info (Azure/Sky)**: `#0284c7` base, `#f0f9ff` subtle bg, `#bae6fd` border, `#1e40af` content.
  * **Status Badge Halo**: Every `StatusBadge` must feature a 7px status dot with a glowing halo ring (`boxShadow: 0 0 0 2px <subtle-bg>, 0 0 4px <base-color>`) for instant pre-attentive recognition.

#### 7. Professional Iconography & Container Anchors (No Floating Naked Icons)
* **No Floating Naked Icons**: Icons in KPIs, headers, table actions, and navigation must be housed in dedicated rounded badge containers with tinted background and crisp matching 1px border.
* **StatCard Hero Badges**: 46x46px containers (`borderRadius: var(--radius-lg)`), soft semantic background + matching 1px border + ambient drop shadow.
* **Sidebar Navigation Badges**: Dedicated 26x26px icon containers. Active routes receive a vibrant brand accent background, bold weight, and a crisp active border indicator.
* **Table Action Icons**: Interactive row actions must use subtle rounded icon buttons (`IconButton`) with hover elevation, never raw clickable SVG tags.

#### 8. Tactile Micro-Interactions & Interactive States
* **Primary Button**: Deep Oxford Navy `#102a43`, specular top highlight (`inset 0 1px 0 0 rgba(255, 255, 255, 0.22)`), drop shadow `0 2px 4px rgba(16, 42, 67, 0.25)`.
* **Secondary Button**: Pure white card background, 1px border `#cbd5e1`, subtle shadow, hover elevation.
* **Interactive Feedback**: Hover states must provide subtle elevation (`box-shadow: var(--shadow-md)`), and active clicks must provide tactile depression (`transform: translateY(1px)`).
* **Form Controls**: Crisp `#cbd5e1` resting border with resting shadow; on focus, a vibrant focus ring `0 0 0 3px rgba(16, 42, 67, 0.15)`.

#### 9. High-Density ERP Architecture & Scannability
* Information density must never compromise scannability.
* Maintain consistent `1.25rem - 1.5rem` card padding, `0.875rem` table cell padding, and structured multi-column property grids (`PropertyGrid`).
* All transactional amounts and dates must be cleanly isolated with `<bdi>` and standard `<MoneyDisplay>` / `<LocalDateDisplay>`.

#### 10. Mandatory Pre-Acceptance Visual Self-Review Checklist
Before declaring any frontend mission complete, the agent must personally open and inspect every captured screenshot against this 8-point visual gate:
1. **Shell Grounding**: Is the topbar authentic Dark Oxford Navy (`#102a43`), with crisp white text and high-contrast context selector pills?
2. **Canvas Separation**: Is the page canvas cool neutral slate (`#f0f4f8`), and do cards visibly pop with pure white surfaces, `#cbd5e1` borders, and dual-layer shadows?
3. **Action Authority**: Are primary buttons deep Oxford Navy `#102a43` with top specular highlight and drop shadow?
4. **Icon Containerization**: Are all KPI and header icons housed in dedicated badge containers (no floating naked icons)?
5. **Data Isolation**: Are numbers, currencies, dates, and codes cleanly isolated with `<bdi>` without reversed characters or RTL distortion?
6. **Zero Translation Leaks**: Is all copy natural Arabic without raw English enum names, GUIDs, or developer terminology?
7. **No Layout Overflow**: Are tables, grids, cards, and modal dialogs free of horizontal scroll, clipping, or truncated text?
8. **Zero View/Form Collision**: Does the page strictly separate browsing from data entry? Are all creation and editing flows cleanly isolated in dedicated Modal Dialogs or dedicated workspace routes (never inline forms embedded inside or below list tables)?

### 1.2 Mandatory Separation of Concerns: Zero Mixed View/Form Collision

TODIR ERP is built to rival top-tier global enterprise platforms (such as Odoo) in the Arabic SaaS market. Our target users are everyday business employees (accountants, inventory clerks, cashiers, sales reps, operations managers) with varying degrees of technical literacy. They must never be confused by cluttered or ambiguous screens.

#### The Ironclad Rule of View / Form Separation:
> **A page must NEVER combine browsing data (tables, lists, 360-degree record profiles) with inline creation or editing forms on the same screen.**

#### Architectural Requirements:
1. **List Pages (`ListPage`)**:
   - Sole responsibility: Searching, filtering, scanning tabular records, pagination, and launching explicit actions.
   - **STRICTLY PROHIBITED**: Embedding `<form>`, inline create/edit fields, or permanent editable draft rows directly inside or below a table on a list page.
   - **Creation Flow**: Always triggered exclusively by an explicit Primary Action button (e.g. `+ إضافة عميل`, `+ إضافة صنف`, `+ إنشاء فاتورة جديدة`). Clicking this button MUST open:
     - A focused **Modal Dialog** (`Dialog`) for quick/standard entity creation, OR
     - Navigate to a dedicated **Creation Page / Workspace Route** (e.g. `/new`, `/draft`) for complex documents with its own sticky action toolbar (`حفظ المسودة`, `اعتماد`, `إلغاء`).
2. **Record Pages (`RecordPage` - 360-Degree Entity Views)**:
   - Sole responsibility: Reading comprehensive entity information (KPI stat cards, structured PropertyGrids, tabs for related activity/history).
   - **Editing Flow**: Clicking `تعديل` on the record page header must open a dedicated **Edit Modal Dialog** (`Dialog`) or navigate to a dedicated **Edit Route** (`/:id/edit`). PropertyGrid cards must remain clean, read-only displays and must never morph in-place into messy text fields.
   - **Sub-entity Mutations**: Adding an address, contact point, identifier, or bank account must open a dedicated modal dialog triggered by an action button within that section surface.
3. **Table Row Actions**:
   - Row-level edits, status transitions, or deletions must be triggered via explicit table action buttons (e.g. `IconButton` or menu item) that open a confirmation dialog, edit modal, or drawer. Never turn plain table cells into unstyled inline inputs.
4. **Cognitive Ergonomics for Non-Technical Users**:
   - The UI must be self-explanatory with zero user training required.
   - Every screen has exactly ONE obvious primary action (highlighted in Dark Oxford Navy `#102a43`).
   - Action verbs must be completely unambiguous (`حفظ التغييرات`, `إلغاء`, `إضافة...`). Generic verbs like `تنفيذ` or `Submit` are forbidden.
   - Clear explanatory subtitles and microcopy in Arabic to guide the user without condescension.
5. **Strict Code Quality & Component Reuse**:
   - Modules MUST use the unified design system primitives (`frontend/components/primitives/`, `frontend/components/fields/`, `frontend/components/forms/`, `frontend/components/pages/`).
   - Reinventing custom buttons, raw unstyled inputs, raw HTML tables, or custom card wrappers inside modules is strictly prohibited as a code quality defect.
6. **Mandatory In-Screen Contextual Help (`PageHelpModal` & `?` Button)**:
   - Every business page archetype (`ListPage`, `RecordPage`, `DocumentPage`, or standalone workspace) must provide a contextual user guide (`PageHelpContent`) wired into `PageHeader` via the `help` prop.
   - The user opens this guide by clicking the crisp, high-contrast `?` Help button (`PageHelpButton`) in the page header.
   - The guide is tailored for everyday employees (cashiers, accountants, warehouse workers) and must contain:
     - **Purpose (`summary`)**: Clear explanation of why the user is on this page and what tasks they accomplish.
     - **Workflow Steps (`workflowSteps`)**: Numbered (1, 2, 3) step-by-step guide on how to browse, filter, or create.
     - **Key Concepts (`keyConcepts`)**: Plain-language definitions of business terms without technical or database jargon.
     - **Pro Tips (`proTips`)**: Practical tips to speed up work (e.g. keyboard shortcuts, filter resets, CSV exports).
     - **FAQs & Mistakes (`faqs`)**: Reassurance on common edge cases (e.g. "Can I delete this record?", "How to fix a typo?").
   - **Continuous Agent Synchronization**: Whenever an agent creates, refactors, or modifies a page, form, or workflow, the agent **MUST update the corresponding page help content** in module translations (`ar.ts` and `en.ts`) so in-app guidance always remains 100% accurate and synchronized with the latest UX.

### 1.3 Mandatory Search-and-Select & Smart ComboBox with Quick Create (Select-or-Create Standard)

Typing free-form text or IDs for relational business entities creates data corruption, spelling inconsistencies, and duplicate records (e.g. typing "شركة النور" vs "شركة النور للتجارة" or mistyping a product code).

#### The Law of Selection over Free Typing:
> **Any field referencing an existing domain entity (Customers, Suppliers, Products, Accounts, Currencies, Tax Rules, Units of Measure, Payment Terms) must use a searchable, keyboard-friendly ComboBox (`ComboBox` / domain pickers). Free-text `<input>` or raw string fields for relational entities are strictly prohibited.**

#### Core Requirements:
1. **Instant Search & Contextual Disambiguation**:
   - The ComboBox must query or filter records instantly as the user types, displaying clarifying secondary attributes (e.g. Party code + tax ID + city; Product SKU + unit; Account code + dual-language name).
2. **Create-on-the-Fly (`onCreateNew` Action)**:
   - When the user types an entity name that does not exist yet, the ComboBox dropdown MUST provide a prominent, accessible quick-action row:
     `+ إنشاء "[الاسم المدخل]" جديد...`
   - Clicking this action MUST open a focused modal dialog prefilled with the typed term (e.g. `QuickCreatePartyDialog`, `QuickCreateProductDialog`).
   - Upon saving the modal, the newly created record is immediately auto-selected in the form without losing the user's current draft or context.
3. **Dedicated Domain Pickers**:
   - Standard domain pickers (`PartyPicker`, `ProductPicker`, `AccountPicker`) must encapsulate this behavior and be reused across all forms. Rebuilding ad-hoc entity selectors inside individual screens is prohibited.

### 1.4 The Seven Enterprise UX Superpowers (Arabic Market Dominance Standard)

To defeat global ERP competitors like Odoo 17/18 and SAP in usability, daily speed, and operational calmness for ordinary business users, every module and screen in TODIR ERP must adhere to and integrate these seven architectural superpowers:

1. **Global Command Palette (`Ctrl + K` / Spotlight Navigation)**:
   - Global keyboard shortcut `Ctrl + K` (or `Cmd + K`) or clicking the topbar search trigger opens an accessible spotlight modal (`CommandPalette`).
   - Enables non-technical users to jump instantly to any screen, module, or high-frequency action (e.g. "فاتورة مبيعات جديدة", "إضافة صنف", "تسجيل قيد") using natural Arabic or English fuzzy keywords without hunting through menus.
   - Tracks recent actions and screens in client storage, architected for seamless synchronization with backend user profiles.

2. **Unsaved Changes Protection & Draft Auto-Recovery (`DirtyFormGuard` & `useDraftAutoSave`)**:
   - Form edits must never be lost due to accidental tab closing, page reloads, browser back-clicks, or power outages.
   - `useDirtyFormGuard` intercepts browser unloads with a clear reassurance message.
   - `useDraftAutoSave` debounces and caches dirty form state. When returning to a form with an existing draft, `DraftRecoveryBanner` offers a 1-click `[استعادة المسودة]` or `[تجاهل ومسح]`.
   - Architected with `DraftStorageAdapter` so it can plug into Redis or backend draft APIs in the future without changing form code.

3. **High-Speed Numpad & Keyboard Grid Ergonomics (`useFastGridNavigation`)**:
   - Cashiers, warehouse clerks, and accountants must not be forced to reach for the mouse while typing document lines.
   - `useFastGridNavigation` enables cell-to-cell traversal with `Enter` / `Tab`.
   - Pressing `Enter` on the last cell of the last line automatically calls `onAddRow()` and moves focus to the first cell of the newly created line.
   - `Ctrl + S` triggers quick document saving; `Alt + N` adds a new line.

4. **Sticky Floating Batch Action Bar (`FloatingBatchBar`)**:
   - When users select multiple rows across any `ListPage`, an elevated Dark Oxford Navy dock floats at the bottom of the viewport.
   - Provides immediate visual feedback (`تم تحديد X عنصر`), batch actions (`تصدير Excel`, `طباعة مجمعة`, `تغيير الحالة`), and 1-click deselect.

5. **Action Reassurance with 5-Second Undo Toast (`useUndoToast`)**:
   - Relieves user anxiety around mistakes and accidental mutations. Reversible state updates (e.g. deactivating a party, archiving a product line, removing a draft item) display a high-contrast toast with a 5-second countdown bar and an immediate **[تراجع (Undo)]** button.

6. **Slide-Over Quick Peek Drawer (`QuickPeekDrawer`)**:
   - Allows operators to inspect 360-degree entity details (customer credit limit, product stock, account balance) in a slide-over panel without leaving their active form or losing current input state. Closes with `Esc` or backdrop click.

7. **Network Resilience & Connection Guard (`NetworkStatusBanner`)**:
   - Monitors online/offline connectivity. When intermittent branch Wi-Fi drops, a calm amber banner reassures the operator that input is preserved safely locally, preventing panic and technical error dialogs. Reconnection shows a green confirmation before fading smoothly.

### The Sacred Boundary
```text
DATABASE LANGUAGE != DOMAIN LANGUAGE != API LANGUAGE != USER-FACING LANGUAGE
```
* **NEVER design screens by mirroring backend DTOs**: A DTO is an integration packet, not a UX specification. Do not put one field per DTO property on a screen.
* **Internal Identifiers are Forbidden in the UI**: `TenantId`, `OrganizationId`, `LegalEntityId`, `PartyId`, GUIDs (`0192e2b3-...`), and `MfaChallengeId` are technical database plumbing. They MUST NEVER be rendered as labels, inputs, or titles. Always display the human-readable business reference code (e.g. `#INV-2026-0045`, `#PRT-001`) or the entity name.
* **Dates and Timestamps**: Never expose raw ISO strings (`2026-01-01T09:00:00+00:00`). Business modules must use the application locale/timezone and canonical `LocalDateDisplay` / `DateTimeDisplay` components. No module-level hardcoded country locale unless an explicitly country-specific product requirement requires it.

---

## 2. Arabic-First & Market Language Dictionary

Arabic in TODIR is native product copy, never literal translation. All labels, messages, buttons, and statuses must use natural Arabic business terms familiar to accountants, cashiers, warehouse workers, and business owners.

### 2.1. Canonical ERP Domain Vocabulary
| English Concept | Canonical Arabic Term | Prohibited / Machine-Translated Terms |
|---|---|---|
| Customer | `العميل` | `زبون` / `مشتري` |
| Vendor / Supplier | `المورد` | `بائع` / `مزود` |
| Business Party | `الطرف التجاري` / `الأطراف` | `الكيان` / `الطرف الثالث` |
| Product / Item | `المنتج` / `الصنف` | `المادة` / `عنصر` |
| Service | `خدمة` (غير مخزنية) | `عمل` |
| Stock / Goods | `بضاعة مخزنية` | `سلعة` |
| Warehouse | `المستودع` / `المخزن` | `المحل` |
| Unit of Measure (UoM) | `وحدة القياس` | `المعيار` |
| SKU | `رمز التخزين التعريفي (SKU)` | `كود القطعة` |
| Barcode | `الباركود` | `رمز الشفرة` |
| Sales Invoice | `فاتورة مبيعات` | `فاتورة البيع` |
| Tax Invoice | `فاتورة ضريبية` | `فاتورة الضرائب` |
| Purchase Order | `أمر شراء` | `طلب الشراء` |
| Quotation | `عرض سعر` | `اقتباس` / `تقدير` |
| Credit Note | `إشعار دائن (تصحيح)` | `مذكرة دائنة` |
| Debit Note | `إشعار مدين` | `مذكرة مدينة` |
| Subtotal | `المجموع الفرعي (قبل الضريبة)` | `المجموع الجزئي` |
| Value Added Tax (VAT) | `ضريبة القيمة المضافة` | `الضريبة` (مجردة بدون توضيح) |
| Total Amount Due | `المبلغ الإجمالي المستحق` | `المبلغ الكلي` |
| Manual Journal Entry | `قيد يومية يدوي` | `يومية عامة` |
| General Ledger | `دفتر الأستاذ العام` | `دفتر الحسابات` |
| Chart of Accounts | `دليل الحسابات` | `شجرة الحسابات` |
| Debit | `مدين (Debit)` | `خصم` |
| Credit | `دائن (Credit)` | `إضافة` |
| Balanced | `متوازن` | `مستقر` |
| Cost Center | `مركز التكلفة` | `قسم المصاريف` |
| Payment Voucher | `سند صرف` | `إيصال دفع` |
| Receipt Voucher | `سند قبض` | `إيصال استلام` |
| Login Prefix | `بادئة الدخول` | `معرف التسجيل` |
| System Template | `قالب قياسي معتمد` | `نموذج النظام` |

### 2.2. Standard Button Action Verbs
Buttons must describe the exact action clearly. Generic buttons like `تنفيذ` or `إجراء` or `Submit` are strictly prohibited.
* Primary Creation: `إضافة عميل`, `إضافة منتج`, `إنشاء فاتورة جديدة`, `تسجيل دفعة`
* Modification & Persistence: `حفظ التغييرات`, `تعديل البيانات`, `تحديث`
* Financial & Operational Lifecycle: `ترحيل إلى الحسابات`, `اعتماد المستند`, `إصدار إشعار دائن`
* Deactivation & Archival: `تعطيل الحساب`, `أرشفة الصنف`, `إلغاء الأرشفة`
* Destructive Actions: `حذف السجل نهائياً`, `إلغاء المستند`
* Navigation & Dismissal: `العودة للقائمة`, `رجوع`, `إلغاء`, `إغلاق`

### 2.3. System Feedback & Status Copy
* **Success Messages**: State exactly what happened (`تم إنشاء الفاتورة بنجاح`, `تم ترحيل القيد إلى دفتر الأستاذ`, `تم حفظ الصنف الجديد`). Avoid generic `تمت العملية بنجاح`.
* **Error Messages**: Explain the business problem in friendly terms and offer the next step. Never display SQL exceptions, status codes alone, or stack traces. Map RFC-7807 problem details to clean Arabic.
* **Empty States**: Must be contextual with an actionable next step:
  - Title: `لا توجد فواتير مبيعات حتى الآن`
  - Subtitle: `ابدأ بإنشاء أول فاتورة مبيعات لإدارة مطالبات العملاء والامتثال الضريبي.`
  - Action Button: `+ إنشاء فاتورة جديدة`

---

## 3. RTL, Bidirectional Isolation & Layout Engineering

Arabic RTL is not merely setting `dir="rtl"`. It requires strict bidirectional engineering:

1. **CSS Logical Properties Mandatory**:
   - `margin-inline-start`, `margin-inline-end`, `padding-inline`, `padding-block`.
   - `inset-inline-start`, `inset-inline-end`.
   - `border-inline-start`, `border-inline-end`.
   - `text-align: start`, `text-align: end`.
   - **STRICT PROHIBITION**: Hardcoded directional properties (`left`, `right`, `margin-left`, `margin-right`, `padding-left`, `padding-right`) are forbidden for directional layout.

2. **Bidirectional Text Isolation (`<bdi>` & `dir="ltr"`)**:
   Numbers, prices, codes, and mixed text easily corrupt when rendered inside Arabic RTL paragraphs (causing the `+` sign on phone numbers to jump to the right, or punctuation to invert).
   - **Phone Numbers**: `<bdi dir="ltr" style={{ fontFamily: "var(--font-mono)" }}>+966 50 123 4567</bdi>`
   - **SKU / Codes**: `<bdi dir="ltr">{product.sku}</bdi>`
   - **Emails**: `<bdi dir="ltr">{user.email}</bdi>`
   - **Reference Identifiers**: `<bdi>#INV-2026-0045</bdi>`
   - **Currency & Monetary Amounts**: Money must use `<MoneyDisplay>` / `<CurrencyDisplay>` with the actual business currency context, isolated with `<bdi>` where appropriate. Never hardcode currency symbols or assume a default country currency in generic page standards.

3. **Prevent Accidental Horizontal Overflow**:
   Page-level and incidental horizontal scrolling in tables, cards, or page bodies is a broken user experience. Prefer responsive layouts, useful column prioritization, and readable wrapping. A genuinely wide operational dataset may use a deliberately contained grid-level horizontal navigation pattern when wrapping would make the data unreadable; keep identifying columns frozen, provide responsive alternatives where appropriate, and make the exception an intentional business-driven design choice rather than a layout workaround.

---

## 4. Canonical Reusable Page Archetypes (The 5 Core Systems)

Business pages should use the appropriate approved page archetype when applicable (`ListPage`, `RecordPage`, `DocumentPage`, `WizardPage`, `SettingsPage` located in `frontend/components/pages/`). Do not assert that literally every screen must fit one of these five archetypes: authentication, system states, dashboards, and genuinely specialized workflows may use approved application and design-system patterns (`AuthView`, `DashboardPage`, `Surface`, etc.). Any exception must remain intentional, documented, and reusable within design system standards, not bespoke layout chaos.

### 4.1. Archetype 1: `ListPage` (Tabular Records & Dynamic Grids)
* **When to use**: All master list screens (Directory Parties and roles, Products, Invoices, Journals, Users).
* **Composition Pipeline**:
  ```text
  ListPage
  ├── PageHeader (Title, Subtitle, Breadcrumbs, Primary Action button)
  ├── FilterBar (SearchField + Select dropdown filters + Active filter badge + Reset action)
   ├── DataGrid or Table (responsive, localized cells, compact row height; contained navigation only for genuinely wide datasets)
  ├── Pagination (Server-side pager with page size selector)
  └── Contextual EmptyState / LoadingState / ErrorState
  ```
* **Key Props & Invariants**:
  - `title`: e.g. `"دليل الأطراف التجارية"`
  - `subtitle`: Brief description of what the table represents.
  - `primaryAction`: `{ label: "إضافة طرف جديد", onClick: () => setIsCreateOpen(true) }` (MUST open a dedicated Modal Dialog or navigate to dedicated creation route. NEVER render inline form inputs inside or below the table).
  - `filterBar`: `<FilterBar searchInput={<SearchField ... />} ...>`
  - `pagination`: `<Pagination page={page} totalPages={totalPages} ... />`
  - **Zero Mixed Forms**: ListPage is strictly read-only and scannable; creation/editing must be completely decoupled into modals or separate pages.

### 4.2. Archetype 2: `RecordPage` (Master Entity 360-View Details)
* **When to use**: Master entity profiles (Party Details, Product Details, User Account Details).
* **Composition Pipeline**:
  ```text
  RecordPage
  ├── Header: Title, business code in <bdi>, status badge, action buttons (تعديل, تعطيل, رجوع)
  ├── Top KPI Row: (Optional) StatCard components summarizing high-impact metrics
  ├── Main Body: Multiple structured Surface cards (Identity, Legal, Contact, Settings)
  │   └── PropertyGrid (2, 3, or 4 columns) with PropertyItem key-value displays
  ├── Tabbed Sub-Views: (Optional) Tabs for related transactions, history, audit
  └── Formatted Audit Footer: Localized timestamp using application locale/timezone and user info
  ```
* **Invariants**:
  - PropertyGrids are strictly read-only presentations.
  - Clicking `تعديل` in the header MUST open a dedicated Edit Dialog or navigate to an edit route. Never turn PropertyGrid fields into inline text inputs.
* **StatCard Standard**:
  KPI/StatCard rows are optional. Never invent 3–4 metrics merely to satisfy a layout. Show KPIs only when they materially help the employee or manager understand state or make a decision.
  - `title`: Metric label (e.g. `"حالة الحساب"`, `"طبيعة الكيان"`, `"الرصيد الافتتاحي"`)
  - `value`: Main stat (e.g. `"نشط ومفعّل"`, `"منشأة تجارية"`, or formatted monetary amount via `<MoneyDisplay>` / `<CurrencyDisplay>` using the actual business currency context)
  - `subtitle`: Contextual explanation
  - `intent`: `"primary" | "success" | "warning" | "danger" | "info" | "neutral"`
  - `icon`: Relevant icon component (`<CheckCircleIcon size={20} />`)
* **PropertyItem Standard**:
  - `label`: Field label
  - `value`: Display content (or formatted component)
  - `icon`: Optional small icon (`size={14}`)
  - `isCopyable`: Enables one-click clipboard copying for codes, emails, and tax IDs
  - `rawCopyValue`: The exact plain string to copy

### 4.3. Archetype 3: `DocumentPage` (Financial & Commercial Transactions)
* **When to use**: Transactional and accounting documents (Invoices, Orders, Receipts, Journals).
* **Composition Pipeline**:
  ```text
  DocumentPage
  ├── Header: Document type badge, document number, multi-axis statuses, locked badge
  ├── Metadata Section: Surface card with key document dates, parties, currency, terms
  ├── Line Items Section: Surface card with responsive-first items grid (intentionally contained grid-level horizontal navigation only when the business dataset genuinely requires it)
  ├── Document Notes Section: Surface card for terms, notes, and payment instructions
  └── Totals & Summary Card: Structured breakdown (Subtotal, Discount, Tax, Grand Total)
  ```
* **Multi-Axis Status Model**:
  Never collapse document statuses into a single string. Financial ERPs require orthogonal dimensions:
  - `lifecycle`: `"Draft" | "Issued" | "Cancelled"`
  - `approval`: `"Pending" | "Approved" | "Rejected"`
  - `accounting`: `"Posted" | "Unposted"`
  - `compliance`: `"Valid" | "UnderReview"`
  - `settlement`: `"Paid" | "Partial" | "Unpaid"`
  - `isLocked`: boolean with `lockedReason` (e.g. `"المستند مرحل ومقيد محاسبياً"`)

### 4.4. Archetype 4: `WizardPage` (Guided Multi-Step Linear Operations)
* **When to use**: Complex sequential operations (Data Import/Migration, Initial Onboarding, Fiscal Year Closing).
* **Composition Pipeline**:
  ```text
  WizardPage
  ├── Stepper Navigation: Connected numbered steps (1, 2, 3) with ✓ checkmarks
  ├── Active Step Surface: Focused card rendering only current step's inputs
  └── Sticky Footer: Standardized buttons (السابق, التالي, إنهاء / حفظ) with loading states
  ```

### 4.5. Archetype 5: `SettingsPage` (Module & Platform Configurations)
* **When to use**: Configuration panels (Company Profile, Number Series, Tax Rules, Login Domain).
* **Composition Pipeline**:
  ```text
  SettingsPage
  ├── Vertical Navigation Rail: Categorized navigation tabs
  └── Settings Section Surfaces: Clean cards with title, description, and save action
  ```

---

## 5. Master Design System Import Catalog

Use this definitive matrix for all imports. Never import internal third-party dependencies (`ag-grid`, `react-aria`, etc.) directly into business modules.

### Page Archetypes & Surfaces
```tsx
import {
  ListPage,
  RecordPage,
  DocumentPage,
  WizardPage,
  SettingsPage,
  DashboardPage,
  PageHeader,
  PageToolbar,
  Surface,
  PropertyGrid,
  PropertyItem,
  StatCard
} from "../../../components/pages/index.ts";
```

### Form Fields & Domain Displays
```tsx
import {
  MoneyDisplay,
  MoneyField,
  StatusBadge,
  LocalDateDisplay,
  LocalDateField,
  QuantityDisplay,
  QuantityField,
  PercentageDisplay,
  PercentageField,
  EmailField,
  PhoneField,
  PasswordField,
  EntityRefLink,
  CurrencyDisplay,
  DateTimeDisplay
} from "../../../components/fields/index.ts"; // or specific component paths
```

### Accessible UI Primitives
```tsx
import {
  Button,
  Dialog,
  ConfirmDialog,
  Select,
  SelectItem,
  ComboBox,
  Checkbox,
  Switch,
  Radio,
  RadioGroup,
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
  Pagination,
  FilterBar,
  SearchField,
  TextField,
  TextArea,
  FileInput,
  Tabs,
  TabList,
  Tab,
  TabPanel,
  Alert,
  Toast,
  LoadingState,
  ErrorState,
  EmptyState,
  Spinner,
  Skeleton,
  Separator
} from "../../../components/primitives/index.ts"; // or specific component paths
```

### Vector Icons
```tsx
import {
  HomeIcon,
  UsersIcon,
  DomainIcon,
  DirectoryIcon,
  CatalogIcon,
  InvoicingIcon,
  PaymentsIcon,
  AccountingIcon,
  MigrationIcon,
  FinBaseIcon,
  SettingsIcon,
  BuildingIcon,
  UserIcon,
  ShieldIcon,
  ShieldCheckIcon,
  KeyIcon,
  MailIcon,
  PhoneIcon,
  MapPinIcon,
  TagIcon,
  FolderIcon,
  LockIcon,
  CheckIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  SearchIcon,
  FilterIcon,
  RefreshIcon
} from "../../../components/primitives/icons.tsx";
```

### Core API Client & Query Infrastructure
```tsx
import { httpClient, ApiError } from "../../../core/api/http-client.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
```

---

## 6. End-to-End Blueprint for Building Any New Module

Follow this sequence before and during implementation:

```text
1. Read the canonical Module Pack and record its scope, MVP, deferred work, and acceptance scenarios.
2. Understand the real user task and end-to-end workflow, including common exceptions.
3. Make role, product, terminology, permission, and interaction decisions from that workflow.
4. Reconcile required capabilities, data, permissions, and errors with backend/API contracts; report and resolve owned gaps.
5. Plan and implement the frontend journey, then test, inspect in the real browser, correct defects, and repeat verification.
```

Do not start product design from OpenAPI, DTOs, controllers, database columns, or existing screens. Once the workflow is understood, inspect those sources to verify integration contracts and constraints. Keep module files inside `frontend/modules/<module-name>/` and follow the module ownership, route, and verification rules above.

---

## 7. Production Code Blueprints (Canonical Boilerplates)

These are responsibility-level patterns, not a generic business model. Before adapting them, use the canonical Module Pack to select the owning module's real concepts, API routes, permissions, and fields. Keep tenant identity bound to the authenticated server session; never accept or send a client-supplied Tenant ID. Include only authorized legal-entity context when the documented workflow requires it.

### Blueprint A: API Client (`services/<resource>-api.ts`)

```ts
import { httpClient } from "../../../core/api/http-client.ts";
import type { Resource, ResourceListParams, ResourceListResult, CreateResourceInput } from "../types.ts";

// Set this route from the owning module's documented API contract.
const resourcePath = "/api/v1/<module-owned-resource>";

export const resourceApi = {
  list: (params: ResourceListParams = {}): Promise<ResourceListResult> => {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.page) query.set("page", String(params.page));
    if (params.pageSize) query.set("pageSize", String(params.pageSize));
    const suffix = query.size ? `?${query.toString()}` : "";
    return httpClient<ResourceListResult>(`${resourcePath}${suffix}`);
  },
  getById: (id: string): Promise<Resource> =>
    httpClient<Resource>(`${resourcePath}/${encodeURIComponent(id)}`),
  create: (input: CreateResourceInput): Promise<Resource> =>
    httpClient<Resource>(resourcePath, { method: "POST", body: JSON.stringify(input) })
};
```

### Blueprint B: Query & Mutation Hooks (`hooks/<resource>-queries.ts`)

Generic TanStack Query examples must not assume every resource is legal-entity-scoped. Query keys must include only authorization and business scopes that materially change the server response. Tenant-level resources (such as Directory parties, users, settings, and platform configuration) must not be artificially fragmented by `LegalEntityId`. Entity-scoped resources (such as invoices, journal entries, and warehouses) must use the authorized runtime legal-entity context. Never invent default scope IDs.

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { resourceApi } from "../services/resource-api.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";
import type { CreateResourceInput, ResourceListParams } from "../types.ts";

// 1. Entity-scoped resource hook (e.g. invoices, ledger entries, warehouses):
export function useEntityScopedResourceList(params: ResourceListParams = {}) {
  const context = useRuntimeContext();
  const legalEntityId = context.legalEntity.legalEntityId;
  return useQuery({
    // Cache key includes legalEntityId only because this resource is partitioned by legal entity.
    queryKey: queryKeyFactory.list(context.tenant.tenantId, legalEntityId, "<owning-module>", "resource", params),
    queryFn: () => resourceApi.list(params)
  });
}

// 2. Tenant-level resource hook (e.g. directory parties, IAM users, catalog definitions):
export function useTenantResourceList(params: ResourceListParams = {}) {
  const context = useRuntimeContext();
  return useQuery({
    // Tenant-level resources are NOT fragmented by LegalEntityId.
    queryKey: ["todir", "tenant", context.tenant.tenantId, "module", "<owning-module>", "resource", "list", params],
    queryFn: () => resourceApi.list(params)
  });
}

export function useCreateResource(isEntityScoped = false) {
  const context = useRuntimeContext();
  const queryClient = useQueryClient();
  const legalEntityId = context.legalEntity.legalEntityId;

  return useMutation({
    mutationFn: (input: CreateResourceInput) => resourceApi.create(input),
    onSuccess: () => queryClient.invalidateQueries({
      queryKey: isEntityScoped
        ? queryKeyFactory.resource(context.tenant.tenantId, legalEntityId, "<owning-module>", "resource")
        : ["todir", "tenant", context.tenant.tenantId, "module", "<owning-module>", "resource"]
    })
  });
}
```

The tenant value above is used only to isolate client cache entries; the authenticated server session binds the request to exactly one Tenant. Never add Tenant identity to request parameters, headers, or bodies. Include legal-entity context in a query key or API request only when the owning business workflow genuinely requires it and the value comes directly from the authorized runtime legal-entity context. Never invent default scope IDs (e.g. fallback strings or placeholders) to force a key or parameter.

### Blueprint C: List and Record Pages

Compose the owning module's real resource with the shared page archetypes. Let the Module Pack and business contract determine the columns, filters, detail sections, actions, and fields; do not assume universal tax, compliance, credit, or identity attributes.

```tsx
export function ResourceListPage(): React.JSX.Element {
  const { data, isLoading, error } = useResourceList();
  const records = data?.items ?? [];

  return (
    <ListPage title="<localized module title>">
      {isLoading ? <LoadingState /> : error ? <ErrorState /> : (
        <DataGrid rows={records} columns={columns} />
      )}
    </ListPage>
  );
}

export function ResourceRecordPage({ id }: { readonly id: string }): React.JSX.Element {
  const { data, isLoading, error } = useResourceRecord(id);
  if (isLoading) return <LoadingState />;
  if (error || !data) return <ErrorState />;
  return <RecordPage title={data.displayName}>
    {/* Render only fields and actions owned by this module and allowed by permission. */}
  </RecordPage>;
}
```

Replace illustrative component props with the actual shared component contracts. Keep loading, empty, error, permission, accessibility, Arabic, and RTL behavior explicit.

### Blueprint D: Routes & Public Contract (`routes.tsx` & `public.ts`)

```tsx
export const resourceRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "<module-resource>",
    moduleIds: ["<owning-module>"],
    path: "/<module-resource>",
    label: { ar: "<اسم عربي من pack>", en: "<documented English label>" },
    permission: "<owning-module.permission>",
    icon: "<approved-icon>"
  },
  routes: [
    { path: "<module-resource>", element: <ResourceListRoute /> },
    { path: "<module-resource>/:id", element: <ResourceRecordRoute /> }
  ]
};
```

Export only the owning module's approved public contracts. Do not create a standalone Customer master or move a fact into a module that does not own it; Directory owns Party identity and relationship roles, while other business facts remain with their documented owning modules.

---
## 8. Quality Verification & Completion Gates

Before claiming `FRONTEND COMPLETE`, you MUST run and pass all 9 local verification gates:

```powershell
# 1. Verify module boundary integrity and forbidden imports
pnpm run architecture:frontend

# 2. Strict TypeScript type check (zero errors tolerated)
pnpm run typecheck

# 3. Unit and domain slice logic tests
pnpm run test:frontend

# 4. OpenAPI backend schema drift check
pnpm run api:drift

# 5. Production bundle build
pnpm --filter frontend build

# 6. Repository-wide 73 release gates verification
pnpm run release:gates

# 7. Complete Playwright browser E2E test suite
pnpm --filter frontend exec playwright test --workers=4

# 8. Check for any trailing whitespace or formatting errors
git diff --check

# 9. Verify STRICT frontend boundary isolation (Must be completely empty!)
git diff -- backend
```

A passing build alone is NEVER completion. Full compliance with the UX archetypes, natural Arabic copy, prevention of accidental overflow, RTL bidirectional isolation, and automated tests is mandatory. Reconcile all required documented acceptance scenarios with real browser evidence and fix any frontend-owned failure before claiming `FRONTEND COMPLETE`. Deliver `FRONTEND COMPLETE` with integrated Product/UX evidence and ready for human Product acceptance; the human Product Owner's approval remains necessary for `MODULE COMPLETE`.

---

## 11. Fast Execution Mode (Frontend)

Frontend work must strictly adhere to Section 12 of the repository root `AGENTS.md`:
1. **One-Pass Study:** Read the canonical Module Pack, UX requirements, and directly relevant screens/components once. Form a gap list and start implementation immediately. Do not explore unrelated modules.
2. **Gap-Driven Execution:** Touch only missing, broken, or unverified frontend scope. Do not redesign working forms, grids, or archetypes.
3. **Action-Oriented Analysis:** Translate UX needs into component code, queries, forms, or focused tests without prolonged deliberation.
4. **No Repeated Loops:** If a Playwright spec or component test fails twice, stop. Inspect browser logs, DOM selectors, and code, make a targeted fix, and rerun only the failing test.
5. **Focused Tests During Dev:** Run only the specific module unit test or targeted Playwright flow during development (e.g. `pnpm --filter frontend test:e2e tests/e2e/finbase-flow.spec.ts`). Never run all 33+ E2E tests on every code edit.
6. **Full Gates Once at the End:** Execute the complete 9-gate frontend verification suite only after the last frontend change.
7. **Environment Limits:** Use the running backend API and existing clean database; do not debug machine, browser, or server internals unless truly blocking.
8. **No Premature Polish:** Ensure business data and workflow correctness before perfecting screenshot aesthetics.
9. **Pattern Reuse:** Reuse design system primitives (`MoneyDisplay`, `StatusBadge`, `DataGrid`, `ListPage`, `RecordPage`, `Dialog`, `Select`). Never invent bespoke UI controls.
10. **Silent Execution:** Work silently without progress commentary; report only when complete or blocked by a genuine external decision.
