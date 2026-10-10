# TODIR Frontend Engineering Constitution

## Identity, role, and core principles

This document is the authoritative engineering constitution for all frontend development in TODIR (`frontend`). It governs the architectural structure, shared-component usage, API integration, data formatting, state management, accessibility, RTL quality, and quality gates for every frontend feature and business module.

Core principle:
> **Simple Outside. Powerful Inside.**
> **Every module may have unique business workflows, but it must not invent a new platform, UX language, API architecture, design system, or engineering style.**

---

## 1. Absolute filename and folder-name prohibition

Never create, rename, or introduce a new file or folder whose basename contains `todir` in any casing (e.g. `todir-`, `Todir`, `TODIR`). Use lower-case, responsibility-based kebab-case names for all files, components, utilities, and directories.

---

## 2. Frontend ownership and directory model

All frontend source code resides under `frontend/` partitioned into four strictly governed layers:

```text
frontend/
├── app/            # Application composition, routing, providers, AppShell, module lazy-loading
├── core/       # Cross-cutting runtime, API transport, auth/session, permissions, tenant/entity context, errors, caching
├── components/     # Shared primitives, tokens, theme compiler, fields, forms, DataGrid, page archetypes
└── modules/        # Business-specific domain workflows and vertical slices
```

**Forbidden:** Generic dumping grounds such as `common/`, `helpers/`, `utils/`, or `misc/` without a strictly bounded responsibility.

---

## 3. Dependency direction and architectural boundaries

The frontend architecture follows a strict downward dependency flow:

```text
Business Modules (`modules/*`)
      │
      ├──▶ Shared Components (`components/*` / `@ui/*`)
      └──▶ Core Runtime & API (`core/*`)
```

### Inviolable boundary rules (Mechanically enforced):
1. **Core and Design System MUST NOT depend on Business Modules.**
2. **Cross-module internal imports are strictly forbidden.** Modules may only consume another module's explicit public API via `frontend/modules/<module>/public.ts`.
3. **No circular dependencies** between frontend modules.
4. **Direct vendor imports of grid or primitive engines are forbidden in business modules:**
   - Modules MUST NOT import `ag-grid-community` or `ag-grid-react` directly (use `@ui/grid` or `components/grid`).
   - Modules MUST NOT import `react-aria-components` or `@react-aria/*` directly (use `@ui/*` or `components/primitives`).
5. **100% Free-Only Core Dependency Rule:** Commercial libraries (such as `ag-grid-enterprise`, `@mui/x-data-grid-pro`, `handsontable`, etc.) are strictly prohibited.

---

## 4. Design System as the mandatory UI language

Business modules MUST use the approved Design System components for all standard interactions. Creating local ad-hoc duplicates is strictly forbidden.

### Approved Standard Components:
- **Primitives & Feedback:** `Button`, `IconButton`, `TextField`, `TextArea`, `SearchField`, `FilterBar`, `Checkbox`, `Radio`, `Switch`, `Select`, `ComboBox`, `Dialog`, `ConfirmDialog`, `Alert`, `Toast` (`ToastProvider`, `useToast`), `ProgressBar`, `Skeleton`, `Popover`, `Tooltip`, `Menu`, `Tabs`, `Pagination`, `Table` (semantic HTML), `FileInput`, `Spinner`.
- **Domain Fields & Displays:** `MoneyField`, `MoneyDisplay`, `QuantityField`, `QuantityDisplay`, `PercentageField`, `PercentageDisplay`, `DateField`, `DateDisplay`, `DateTimeDisplay`, `PhoneField`, `EmailField`, `StatusBadge`, `EntityRefDisplay`.
- **Form System:** `FormField`, `FormSection`, `FormActions`, `ValidationSummary`, `useUnsavedChanges`, `mapServerErrorsToForm`.
- **DataGrid:** `DataGrid` (wrapping AG Grid Community with full RTL, keyboard navigation, density engine, and exact decimal renderers).
- **Page Archetypes:** `ListPage`, `RecordPage`, `DocumentPage`, `SettingsPage`, `WizardPage`, `DashboardPage` (located strictly under `frontend/components/pages/`).

### Local duplication prohibition:
Never create module-local generic components like `sales-button.tsx`, `invoice-dialog.tsx`, `accounting-text-field.tsx`, or `custom-table.tsx`. Module-owned components are permitted only when expressing domain-specific logic (e.g. `invoice-lines-editor.tsx`, `party-picker.tsx`, `account-picker.tsx`).

### Shared component promotion rule:
Do not promote module UI into `components/` unless it is domain-neutral, broadly reusable, fully accessible, supports RTL/theming, and has an approved Storybook story and unit test.

---

## 5. Page construction & archetypes standard

All module screens must be composed using the approved page archetype:

1. **`ListPage`:** For standard entity collections, search, multi-axis filtering, bulk actions, and data grids.
2. **`RecordPage`:** For master record creation and detail editing with metadata headers, tabs, and form sections.
3. **`DocumentPage`:** For lifecycle-oriented business documents (Invoices, Orders, Journal Entries) requiring multi-axis status badges (`lifecycle`, `approval`, `compliance`, `accounting`, `settlement`), line item tables, server totals summary, and concurrency handling.
4. **`WizardPage`:** For multi-step guided flows (e.g. Migration Import Wizard) with step progression, pre-validation/dry-run, and execution reconciliation.
5. **`SettingsPage`:** For configuration and administrative forms.

Every page must consistently render: breadcrumbs, page title, standard action buttons, loading, empty, error, permission-denied, and locked states.

---

## 6. Form system & server validation

All business forms must follow the standard form architecture:
```text
React Hook Form + Zod Schema + Design System Fields + FormField / FormSection / FormActions
```
- **Form State vs. API DTO:** Separate UX form state from API command DTOs using explicit mapper functions.
- **Server Error Mapping:** RFC-7807 Problem Details field errors must be mapped to form fields via the approved `mapServerErrorsToForm` utility. Never parse English error strings.
- **Form States:** Distinctly handle `default`, `submitting`, `disabled`, `readOnly`, and `locked` states.

---

## 7. API integration & TanStack Query standard

Every production module must follow the structured API pipeline:
```text
Backend OpenAPI (`docs/api/platform-openapi.json`)
      ↓
Generated Core Client / Types
      ↓
Module Service Adapter (`frontend/modules/<module>/services/`)
      ↓
TanStack Query Hooks (`frontend/modules/<module>/hooks/`)
      ↓
Page / Feature Component
```
- **No Direct `fetch()`:** Components and pages must never invoke raw `fetch()` directly.
- **Cache Isolation:** Query keys must use `queryKeyFactory` to strictly isolate cache entries by `[tenantId, legalEntityId, module, resource, ...]`. Switching tenants unconditionally triggers `queryClient.clear()`.
- **Reference Contracts vs. Real APIs:** Future agents must verify whether an endpoint exists in the real backend OpenAPI before treating it as production truth. Reference test fixtures must be explicitly recognized and reconciled when the real backend module is built.

---

## 8. Exact financial domain values & arithmetic

**Absolute Rule:** Authoritative financial values must NEVER be represented as or computed using JavaScript binary floating-point (`number`, `parseFloat`, `Number()`, `.toFixed()`, `Math.*`).

- Financial amounts, prices, tax amounts, line totals, discounts, and exchange rates must use `DecimalString`, `MoneyValue`, or `QuantityValue`.
- Frontend arithmetic for financial truth is prohibited. Calculations (tax liability, rounding, discount allocation, ledger posting balance, settlement allocation) are strictly owned by the backend.
- UI displays use `formatMoney`, `formatQuantity`, `formatPercentage`, and `formatLocalDate`.

---

## 9. State dimensions & document immutability

- **Multi-Axis Status Model:** Do NOT collapse independent domain dimensions into a single `status` enum. For business documents, preserve orthogonal state machines:
  - `lifecycleStatus` (e.g., Draft, Issued, Cancelled)
  - `settlementStatus` (e.g., Unpaid, PartiallyPaid, Paid, Overpaid)
  - `approvalStatus` (e.g., Pending, Approved, Rejected)
  - `complianceStatus` (e.g., Compliant, NonCompliant)
  - `accountingStatus` (e.g., Unposted, Posted)
- **Posted/Finalized Immutability:** When a document or journal is posted, the frontend must strictly lock line items, posting dates, and amounts, displaying explicit lock indicators (`lockedReason`) and offering only approved reversal/correction workflows.

---

## 10. Idempotency & mutation safety

- **High-Risk Operations:** Money movement, financial posting, and state transitions must enforce client-generated idempotency keys (`Idempotency-Key` header).
- **Logical Attempt Lifecycle:** The idempotency key is bound to the *logical business attempt*. In-flight double clicks are locked; retrying after a network timeout reuses the same key; a genuinely new transaction generates a new key.
- **Unknown Outcome Handling:** If a high-risk mutation times out (HTTP 504), display an explicit "Unknown Outcome / حالة غير مؤكدة" recovery dialog; do not resubmit blindly.
- **Optimistic Mutation Safety Policy:** Optimistic updates MUST NOT be used for authoritative high-risk business commands (e.g. payment execution, invoice finalization, journal posting, financial settlement, stock movements, payroll finalization, destructive deletion). For high-risk operations, the lifecycle is strictly `submit -> pending/loading -> backend result -> success/failure/unknown`. Optimistic updates are permitted ONLY for low-risk, reversible UX state (e.g., expanding an accordion, toggling local display preferences). Backend authority is final.

---

## 11. Arabic, RTL, and Accessibility (a11y)

- **Arabic/RTL First-Class:** RTL is a Day-1 requirement. Use CSS logical properties (`margin-inline`, `padding-inline`, `inset-inline-start/end`). Ensure numeric inputs, currency symbols, and phone numbers render with correct bidirectional isolation.
- **Accessibility:** Ensure full keyboard operability (Tab order, Enter/Space activation, Escape dismissal, Arrow key navigation in DataGrid/Combobox), visible focus rings, ARIA labels, and zero Axe-core violations.

---

## 12. Performance & module lazy-loading

- **AppShell Lazy Loading:** `AppShell` and routing MUST dynamically import (`React.lazy()` + `Suspense`) business module pages to avoid bundling heavy module dependencies (such as AG Grid and document workflows) into the initial application startup chunk.
- **Query Efficiency:** Avoid waterfalls, duplicate queries, and unbounded polling. Background job queries (such as import wizard status) must stop polling immediately on terminal states (`Succeeded`, `Failed`, `PartialSuccess`).

---

## 13. Quality gates & verification

Before declaring any frontend work complete, the agent must run and pass:
```powershell
pnpm run architecture:frontend    # Module boundary AST verification
pnpm run typecheck                # Strict TypeScript check (0 errors)
pnpm run test:frontend            # Unit & contract tests
pnpm run api:drift                # OpenAPI generated client check
pnpm run frontend:smoke           # Runtime context & auth smoke tests
pnpm run release:gates            # Release gate controls (73/73)
pnpm --dir frontend build        # Vite production build
pnpm --dir frontend exec playwright test # Browser E2E, accessibility & visual regression
```
