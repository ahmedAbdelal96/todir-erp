# Frontend engineering rules

This file governs all work under `frontend/`; read the repository root `AGENTS.md` first.

## Locked structure and dependencies

```text
frontend/
├── app/         # Composition, routing, providers, AppShell, module loading
├── core/        # API transport, generated client, auth/session, tenant context, permissions, errors, query/cache runtime
├── components/  # Tokens, theme compiler, accessible primitives, fields, forms, grid, page archetypes
├── modules/     # Business vertical slices, adapters, hooks, pages, domain components, tests
└── tests/       # Global and browser tests
```

Do not recreate `frontend/src/`, `frontend/platform/`, `apps/`, or `packages/` without explicit approval. Do not create generic dumping grounds such as `common/`, `helpers/`, `utils/`, or `misc/` without a narrowly documented responsibility. `core/` and `components/` never depend on business modules. Modules may consume only another module's explicit `public.ts`; cross-module internals and circular dependencies are forbidden. The app layer owns bootstrap, router, providers, and layout/shell; it composes and lazy-loads and must not become a business-logic layer.

All modules reuse this one platform. They must not create a parallel design system, form system, API client, query/cache architecture, theme engine, error mapper, page shell, runtime context, or permission UX. Use only free dependencies: commercial grid/UI packages such as `ag-grid-enterprise`, `@mui/x-data-grid-pro`, and `handsontable` are prohibited. Business modules must not import grid engines or React Aria engines directly; use the approved wrappers in `components/`.

Never create a new file or folder whose basename contains `todir` in any casing. Use responsibility-based names and framework conventions. Do not add module-local generic duplicates. Promote a component into `components/` only when it is domain-neutral, RTL/theme accessible, broadly reusable, and has the required story and unit test.

Business UI belongs under `frontend/modules/<module>/`. Use only needed responsibility folders such as `pages/`, `components/`, `services/`, `hooks/`, `types/`, `validation/`, and `tests/`; do not create empty templates. Module components remain module-owned by default, pages compose focused components and hooks, services own API communication, hooks own React-specific reusable behavior, and validation improves UX while the backend remains authoritative.

## UI and interaction contract

Use the approved design-system primitives and domain fields: buttons, fields, feedback, menus, dialogs, tabs, pagination, semantic tables, `DataGrid`, `MoneyField`/`MoneyDisplay`, quantity/percentage/date/phone/email displays, status/entity references, form sections/actions, server-error mapping, and page archetypes. Standard screens use `ListPage`, `RecordPage`, `DocumentPage`, `WizardPage`, `SettingsPage`, or `DashboardPage` as appropriate and render breadcrumbs, title, actions, loading, empty, error, permission-denied, and locked states.

Forms use React Hook Form, Zod, approved fields, and explicit mappers between UX state and API command DTOs. RFC-7807 field errors map through `mapServerErrorsToForm`; never parse English error strings. Distinguish default, submitting, disabled, read-only, and locked states.

The API path is generated OpenAPI client/types in `core/`, a module service adapter, TanStack Query hooks, then page/feature components. Pages and components never call raw `fetch()`. Query keys use `queryKeyFactory` and include tenant/legal-entity/module/resource scope. Switching tenants clears the query client. Verify every endpoint against the real backend OpenAPI; fixtures are not production truth.

Authoritative money, tax, quantity, discount, exchange-rate, and other financial values use `DecimalString`, `MoneyValue`, or `QuantityValue`. Never use JavaScript binary floating-point for financial truth: no `parseFloat`, `Number()`, `.toFixed()`, `Math.*`, or frontend financial calculations. Backend totals, rounding, tax, allocation, posting, and settlement remain authoritative; display through approved formatters.

Preserve independent document state dimensions such as lifecycle, settlement, approval, compliance, and accounting; do not collapse them into one status. Posted/finalized documents lock line items, dates, and amounts and expose approved reversal/correction workflows.

High-risk mutations use a client-generated `Idempotency-Key`, lock in-flight duplicate attempts, reuse the key after network timeout, and create a new key only for a genuinely new attempt. A timeout shows an explicit unknown-outcome recovery state and must not blindly resubmit. Do not optimistically update authoritative money movement, posting, settlement, stock, payroll, finalization, or destructive deletion; optimistic behavior is limited to low-risk reversible display state.

RTL and Arabic are first-class. Use CSS logical properties, correct bidi isolation for numbers/currency/phones, localized labels/errors, visible focus, keyboard navigation, ARIA labels, and zero Axe violations. Do not add country-specific legal validation without an approved country pack.

## Loading, tests, and gates

Business module pages and heavy dependencies are lazy-loaded with `React.lazy()` and `Suspense`; keep the initial shell small. Avoid query waterfalls, duplicate requests, unbounded polling, and polling after terminal job states.

Current active slices include `directory`, `catalog`, `finbase`, `accounting`, `invoicing`, `payments`, and `migration`. Inspect actual runtime responsibility before removing a slice. Preserve the current test infrastructure, Playwright, Storybook, and architecture checks; do not introduce a new test framework merely because it is popular. Runtime E2E uses real API/infrastructure when that is the intended test boundary.

Frontend tests belong in `frontend/tests/` or the owning module's `tests/`. Test meaningful behavior, contracts, RTL/accessibility, permissions, query invalidation, idempotency/unknown outcomes, and browser workflows with deterministic setup. Do not mock away the behavior under test or weaken tests to satisfy a gate.

Before declaring frontend work complete, run the applicable required gates and report exact results:

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

Compilation alone is not completion. Also run relevant backend/API/security/build checks when frontend changes cross a contract or module boundary, and keep generated output in approved ignored artifact directories.
