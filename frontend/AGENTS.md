# Frontend product, UX, and engineering operating manual

Read the repository root `AGENTS.md` first. This file governs frontend work and is sufficient, together with the root, to take an approved feature from user/problem discovery to `PRODUCT / UX ACCEPTANCE COMPLETE`.

Never create a new file or folder whose basename contains `todir` in any casing; frontend names must describe responsibility rather than product branding.

## 0. Frontend map

```text
frontend/
├── app/         # Bootstrap, providers, router, AppShell, module loading
├── core/        # API transport/client, auth/session, tenant context, permissions, errors, query/cache runtime
├── components/  # Tokens, theme, accessible primitives, fields, forms, grid, page archetypes
├── modules/     # Business vertical slices and domain-specific UI
└── tests/       # Global, contract, browser, accessibility, and visual tests
```

Do not recreate `frontend/src/`, `frontend/platform/`, `apps/`, or `packages/` without explicit owner approval. Within a module, create only needed `pages/`, `components/`, `services/`, `hooks/`, `types/`, `validation/`, and `tests/` folders; never create empty templates. Current active slices include `directory`, `catalog`, `finbase`, `accounting`, `invoicing`, `payments`, and `migration`.

`app/` owns composition, bootstrap, providers, layout, router, and lazy loading; it does not own business rules. `core/` owns cross-application technical runtime and never depends on modules. `components/` contains genuinely shared, accessible UI and design-system behavior; it must not become a business-module dumping ground. `modules/` owns business UI by vertical slice. Module components remain module-owned unless genuine reuse justifies promotion.

## 1. Frontend does not start from DTOs

NEVER create a screen by reading a backend DTO and creating one field per property. The backend contract is an integration contract, not a screen specification. Design the UI from the user's task, mental model, workflow, and acceptance criteria, then map that UX state explicitly to API commands and responses. Do not put backend business truth or financial calculations into the frontend merely because a DTO exposes fields.

## 2. Product discovery before UI

Before creating a module screen, answer:

- Who is the user: owner, cashier, sales employee, warehouse worker, accountant, HR employee, manager, or another real role?
- What are they trying to accomplish, and how do people in that business describe it?
- What information do they already know and what can the system infer safely?
- What mistakes are common, what action is most frequent, and what exceptional actions exist?
- What is the entry point, normal path, success, validation error, business error, permission error, empty state, loading state, retry/recovery, cancellation, and other exceptional state?

Every screen has one clear primary purpose. A user must immediately know where they are, what they can do, and the main action. Do not build giant all-purpose screens or forms.

## 3. Market language research

Before defining user-facing terminology for a new ERP module, research established products and Arabic-market conventions relevant to the module, such as Odoo, Daftra, Zoho, local accounting/POS/HR products, and domain-specific market language. Compare navigation labels, document names, action labels, field names, empty states, error language, and flow order. Do not copy competitors blindly or assume they are always correct; use research to choose the clearest natural term for the target user.

Arabic is product copy, not translation. Never translate an English phrase literally by default. Ask what a normal Arabic-speaking employee would call the concept. Avoid formal, academic, legalistic, technical, or machine-translated language when a familiar business term exists. Write for ordinary users and use short, specific, familiar words.

Useful vocabulary examples are Customer → `العميل`, Supplier/Vendor → `المورد`, Product → `المنتج`, Quotation → `عرض سعر`, Purchase Order → `أمر شراء`, Invoice → `فاتورة`, Warehouse → the intentionally approved `المستودع` or `المخزن`, Employee → `الموظف`, Attendance → `الحضور والانصراف`, and Leave → `الإجازات`. Do not alternate between terms such as `شركة`/`مؤسسة`/`منشأة` or `مخزن`/`مستودع` without an intentional module vocabulary.

## 4. User-facing copy rules

Before implementation, maintain an internal vocabulary list for the module's name, main entities, documents, actions, statuses, common errors, and navigation terms. It need not become a repository document unless useful, but it must be consistent.

Labels are specific, short, familiar, and actionable. Avoid vague labels such as `البيانات`, `القيمة`, `المعرف`, `الكيان`, `الإجراء`, or `تنفيذ` when a specific business term exists. Buttons describe the action: `حفظ`, `إضافة عميل`, `إنشاء عرض سعر`, `إرسال`, `اعتماد`, `رفض`, `إلغاء`, `تسجيل دفعة`, or `تحويل إلى فاتورة`. Avoid generic `تنفيذ`, `إجراء`, `Submit`, `Process`, or `تأكيد العملية` when the actual action is known.

Success messages describe what happened: `تم حفظ المنتج`, `تم إنشاء الفاتورة`, `تم تسجيل الدفعة`. Avoid `تمت العملية بنجاح` when a more specific message is possible. Errors explain what went wrong and, where useful, what to do next. Never show SQL errors, HTTP codes alone, stack traces, constraint names, or technical exception text; map backend ProblemDetails to natural user language. Empty states are contextual, such as `لا توجد فواتير حتى الآن`, and may offer the next action. Confirmation dialogs state the business consequence, such as `هل تريد إلغاء الفاتورة؟`, especially for destructive or high-impact actions.

## 5. Internal identifiers and progressive disclosure

Do not ask users to type technical identifiers unless they are genuine business-recognized identifiers. Do not expose `TenantId`, `OrganizationId`, `LegalEntityId`, `PartyId`, `EntityId`, GUIDs, or `MfaChallengeId` as normal form fields. Use search, selection, automatic context, business codes, and names.

Do not place every state and option on one form. Reveal information when needed. Authentication is the permanent example: initial login is email + password + `تسجيل الدخول`; the backend derives the tenant from the email domain and never asks the user to select or switch companies; request an initial password change or MFA only when required. Never show organization ID, tenant ID, or an optional MFA code on the initial login form. The general rule is `USER JOURNEY > API SHAPE`.

## 6. RTL, Arabic, responsive UX, and accessibility

RTL is mandatory and must feel natural, not merely be `direction: rtl`. Verify labels, forms, tables, dialogs, navigation, numbers, currencies, mixed Arabic/English, long product names, dates, search, icons, and directional controls. Use CSS logical properties and correct bidirectional isolation for numeric, currency, phone, and mixed-language values.

Support required viewport sizes. Do not compress a desktop table blindly onto a small screen; adapt the interaction and information hierarchy. Use semantic controls, keyboard navigation, visible focus, accessible labels, correct error association, adequate contrast, and never rely only on color. The target is zero Axe-core violations.

## 7. Platform and dependency rules

All business modules reuse the established platform and design system. Do not create a parallel design system, form system, API client, query/cache architecture, theme engine, error mapper, page shell, runtime context, or permission UX. Use only free dependencies; commercial packages such as `ag-grid-enterprise`, `@mui/x-data-grid-pro`, and `handsontable` are prohibited. Business modules must not import `ag-grid-community`, `ag-grid-react`, `react-aria-components`, or `@react-aria/*` directly; use approved wrappers in `components/`.

### 7.1. Semantic Design Tokens & Theme Foundation
- Use CSS variables for all styling: colors (`var(--color-surface-*)`, `var(--color-content-*)`, `var(--color-border-*)`, `var(--color-status-*)`), spacing (`var(--space-*)`), radii (`var(--radius-*)`), typography (`var(--font-size-*)`), and elevation (`var(--shadow-*)`).
- Theme tokens must support light, dark, and high-contrast modes, plus compact/comfortable density modes seamlessly.
- Never hardcode raw hex values or pixel spacing in module components.

### 7.2. App Shell & Navigation Foundation
- **TopBar Structure**: Clean, balanced SaaS topbar consisting of:
  1. Brand logo & company identity pill;
  2. Multi-entity & branch context selectors (e.g. `#entity-select`, `#branch-select`);
  3. Language quick-toggle button;
  4. Unified `UserMenu` dropdown containing: user avatar/initials, full name, email, language switch, theme switch, density toggle, and logout button.
  - *Never scatter standalone action buttons (logout, theme, density, profile) across the topbar.*
- **Launchpad / Home Screen**: Clean `PageHeader` + responsive module grid. Module cards must be interactive, clean, visually distinct, with informative descriptions and subtle hover states. Do NOT put duplicate standalone buttons inside cards repeating the card title, and avoid oversized marketing hero banners.

### 7.3. Reusable Page Archetypes & Surfaces
- Standard screens compose standardized layout primitives:
  - `PageHeader`: standardizes title, subtitle/description, breadcrumbs, status badges, primary actions, and secondary actions.
  - `PageToolbar`: provides consistent filtering, search, view switches, and secondary action bars.
  - `Surface`: provides clean card-like containment for grouped content sections with optional header, footer, and variant styling.
  - `ListPage`: standard list archetype composing `PageHeader`, filter toolbar, table/grid, and integrated loading/error/empty states.
  - `RecordPage`, `DocumentPage`, `WizardPage`, `SettingsPage`, `DashboardPage`.

### 7.4. Unified Feedback & Input Primitives
- **Loading State**: Always use `LoadingState` (spinner or skeleton layout), never raw empty whitespace.
- **Error State**: Always use `ErrorState` with an icon, clear title, friendly description, and a retry action button. Never render unstyled floating red text.
- **Empty State**: Always use `EmptyState` with a contextual icon, title, description, and primary creation action when appropriate.
- **Search & Filter**: Use `SearchField` with built-in adornment slots (never absolute-positioned icons overlapping placeholder text) and compose within `FilterBar`.
- **DataGrid & Table**: Use `DataGrid` (with `enableRtl={true}` in RTL environments) or semantic `Table` for structured records.

### 7.5. RTL & Bidirectional Isolation Discipline
- All layout rules must use CSS logical properties: `margin-inline`, `padding-inline`, `inset-inline-start`, `border-inline-start`, `text-align: start`.
- Hardcoded physical properties (`left`, `right`, `margin-left`, `padding-right`) are forbidden for directional styling.
- Mixed text, entity codes, currency codes, phone numbers, and decimal numbers must use `<bdi>` or `dir="ltr"` isolation to prevent Arabic punctuation/number flips.

### 7.6. DataTable & ListPage Architectural Standard (Mandatory for All 50+ Business Modules)
To ensure complete visual and behavioral uniformity across the entire platform, every list page and tabular view across all 50+ future business modules MUST strictly adhere to this standardized architecture:

1. **Mandatory Standard Composition Pipeline**:
   - Every module list page must compose:
     `ListPage` → `FilterBar` (with `SearchField`, filter `Select` controls, active count badge, and reset action) → `DataGrid` (for dynamic datasets/virtualized columns) or `Table` (for semantic lightweight records) → `Pagination` (passed via the `pagination` prop or container bottom) → contextual `EmptyState`, `ErrorState`, and `LoadingState`.
2. **Strict Prohibition of Local Ad-hoc Tables & Pagers**:
   - Modules MUST NOT create custom HTML `<table>` tags with local ad-hoc styling.
   - Modules MUST NOT build custom pagination logic or buttons. The shared `Pagination` primitive is the single source of truth for pagination across all modules.
   - Modules MUST NOT build bespoke filter bars or detached search inputs. Use `FilterBar` or `PageToolbar`.
3. **Action Buttons & Page Actions Hierarchy**:
   - Main creation/add action: Passed as `primaryAction` to `ListPage` / `PageHeader` (e.g. `label: "إضافة عميل"`, `onClick: () => setIsCreateOpen(true)`).
   - Table row actions: Always use standard `Button` with `size="compact"` and semantically correct variants (`secondary` for details/edit, `danger` for deactivate/delete, `ghost` for subtle tertiary actions).
4. **Cell Presentation & Domain Value Display**:
   - Monetary values: Always render with `MoneyDisplay` or `MoneyCell` (never raw string concatenation or floating point numbers).
   - Dates & Datetimes: Always render with `LocalDateDisplay` or `DateCell`.
   - Statuses: Always render with `StatusBadge` or `StatusCell`.
   - Identifiers, emails, codes, and phone numbers: Always wrap with `<bdi>` or `dir="ltr"` to guarantee flawless Arabic/English bidirectional rendering.
5. **RTL / LTR & Density Integration**:
   - `DataGrid` must always receive `enableRtl={true}` in Arabic contexts.
   - All tables and grids must respect the global `density` ("comfortable" vs "compact").
6. **Zero Horizontal Scrolling Mandate (Anti-Pattern Prohibition)**:
   - Horizontal scrolling in desktop and responsive ERP record tables is strictly prohibited as a broken user experience.
   - All `Table` and `DataGrid` instances must fit 100% of their container width (`table-layout: fixed`, `width: 100%`, `overflow: hidden`, and `autoSizeStrategy={{ type: "fitGridWidth" }}`).
   - Columns must distribute proportionally using flex or percentage widths. Cells and headers must use `overflow-wrap: anywhere; word-break: break-word` so text flows cleanly without overflowing the table boundaries.

Do not create module-local generic duplicates such as a second button, table, dialog, or text field. Module-owned components are appropriate for domain behavior such as a party picker or invoice-lines editor. Promote a module component to `components/` only after genuine broad reuse, domain neutrality, accessibility, RTL/theme support, and the required story and unit test.

## 8. TypeScript, forms, and state

Use strict TypeScript. Do not use `any`, `unknown`, unsafe casts, or broad catch-all types to bypass a contract; model the type or validate at the boundary. Keep types split by responsibility as modules grow.

Forms use React Hook Form, Zod, approved design-system fields, and explicit mappers between UX form state and API command DTOs. RFC-7807 field errors map through `mapServerErrorsToForm`; never parse English error strings. Distinguish default, submitting, disabled, read-only, and locked states. Client validation improves UX and never replaces backend validation; validation messages use user language.

Separate local UI state from server state. Use TanStack Query according to project conventions and do not copy server state unnecessarily into global stores. Query keys use `queryKeyFactory` and isolate `[tenantId, legalEntityId, module, resource, ...]`; switching tenants clears the query client. Avoid hiding arbitrary business architecture inside generic hooks.

## 9. API integration and financial truth

The production pipeline is:

```text
Backend OpenAPI → generated core client/types → module service adapter → TanStack Query hooks → page/feature component
```

API calls live in module services or the approved generated-client layer. Pages and components never call raw `fetch()`. Verify endpoints against the real backend OpenAPI; fixtures are not production truth. If the API cannot support a correct journey, report the missing backend capability instead of inventing business logic locally.

Authoritative money, tax, quantity, discount, exchange-rate, and financial values use `DecimalString`, `MoneyValue`, or `QuantityValue`. Never represent or calculate financial truth with JavaScript binary floating point: no `parseFloat`, `Number()`, `.toFixed()`, `Math.*`, or frontend tax/rounding/allocation/posting calculations. Backend totals, rounding, tax, allocation, posting, and settlement remain authoritative; display through approved exact formatters.

Preserve orthogonal document states such as lifecycle, settlement, approval, compliance, and accounting; do not collapse them into one status. Posted/finalized documents lock line items, dates, and amounts, show a clear locked reason, and offer only approved reversal/correction workflows.

## 10. Permissions, mutations, and runtime behavior

Frontend permission checks hide or disable actions for UX only; backend authorization is authoritative. Do not assume a hidden button protects an action.

High-risk mutations use a client-generated `Idempotency-Key`. Lock in-flight double clicks, reuse the key after a network timeout for the same logical attempt, and generate a new key only for a genuinely new attempt. A timeout shows an explicit unknown-outcome recovery state and must not blindly resubmit. Do not optimistically update authoritative money movement, posting, settlement, stock, payroll, finalization, or destructive deletion; optimism is limited to low-risk reversible display preferences or local UI state.

Business module pages and heavy dependencies are lazy-loaded with `React.lazy()` and `Suspense`; keep the initial shell small. Avoid waterfalls, duplicate requests, unbounded polling, unnecessary rerenders, huge downloads, large DOM tables, and massive unbounded lists. Use server-side pagination, filtering, virtualization when justified, and correct query caching. Stop background polling at terminal job states.

## 11. Product journey and screen construction

Before components, describe the journey: entry → normal path → success → validation error → business error → permission error → empty → loading → retry/recovery → cancellation → exceptional states. Forms request only information required at that moment, use safe defaults/inference, group related fields, and hide advanced options until needed.

Pages compose focused components and hooks; they do not contain API code, large business calculations, giant reusable components, or permission implementation. Services own API communication, hooks own React-specific reusable behavior, and validation owns client schemas. Keep each screen's primary purpose obvious and use the appropriate page archetype.

## 12. Testing and acceptance

Test normal flow, validation, business errors, empty states, loading/recovery, permissions, RTL, accessibility, and important business state changes. Playwright must verify real meaningful user journeys; do not write E2E merely to prove a bad form can submit. Preserve the current test framework, Storybook, architecture checks, and real API/infrastructure boundary tests; do not introduce a new framework because it is fashionable.

Before `FRONTEND COMPLETE`, perform Product/UX acceptance. Ask:

- Would a normal non-technical user understand every visible term?
- Does any field expose implementation detail or ask for something the system already knows?
- Is any label a literal translation?
- Are actions specific, errors useful, and empty states understandable?
- Are normal tasks short and advanced complexity hidden until needed?
- Does RTL feel natural at required viewport sizes?
- Can the actual cashier, warehouse employee, salesperson, accountant, HR clerk, or owner finish the main task without technical explanation?

Required checks as applicable include:

```powershell
pnpm run architecture:frontend
pnpm run typecheck
pnpm run test:frontend
pnpm run api:drift
pnpm run frontend:smoke
pnpm run release:gates
pnpm --dir frontend build
pnpm --dir frontend exec playwright test
```

## 13. Frontend completion gate

`FRONTEND COMPLETE` requires the correct user journey, market terminology, natural Arabic, API integration, RTL, responsive behavior, loading/error/empty/recovery states, permissions UX, exact financial display, frontend tests, Playwright coverage, and Product/UX acceptance. A passing build alone is not completion.
