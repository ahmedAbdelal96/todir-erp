# Frontend Business Module Execution Manual (`src/frontend/src/modules/`)

## Identity and responsibility

The `modules/` directory contains business-specific ERP workflows and vertical slices (Directory, Catalog, Sales/Invoicing, Payments, Accounting, Migration, Inventory, HR, etc.).

Core principle:
> **Business modules express rich domain workflows by assembling approved Design System components, Platform runtime utilities, and domain-specific pickers. They MUST NOT reinvent parallel platforms, UI libraries, or API clients.**

---

## 1. Absolute filename prohibition

Never create, rename, or introduce a new module file or folder whose basename contains `todir` in any casing. Use responsibility-based, lower-case kebab-case names (e.g. `party-picker.tsx`, `invoice-document-page.tsx`, `payment-api.ts`).

---

## 2. Mandatory Module Implementation Workflow

Every future coding agent implementing a TODIR business module MUST execute the following phased workflow:

```text
DISCOVER ──▶ MODEL ──▶ CONTRACT ──▶ PLAN ──▶ IMPLEMENT ──▶ TEST ──▶ VISUAL REVIEW ──▶ HARDENING ──▶ RE-VERIFY ──▶ REPORT
```

### Phase 1: DISCOVER Gate
Before writing code, read the exact approved module pack (`docs/modules/`) and identify:
- Business goals & workflows (standard and exception paths).
- Owned vs. non-owned data, Single Writer authority.
- Tenant and Legal Entity scope.
- State machines (Lifecycle, Approval, Compliance, Accounting, Settlement, Lock/Editability).
- Invariants & immutable states (e.g., posted accounting records cannot be edited in place).
- Permissions and platform capabilities.
- Real backend API contracts in `docs/api/platform-openapi.json` vs. Reference mock fixtures.

### Phase 2: MANDATORY REUSE INVENTORY
Before creating any UI component, inspect existing shared foundations:
1. **Design System Primitives & Feedback:** (`Button`, `Dialog`, `ConfirmDialog`, `Alert`, `Toast`, `ProgressBar`, `Skeleton`, `ComboBox`, `Select`, `TextField`, `TextArea`, `SearchField`, `FilterBar`, `Tabs`, `Menu`, `Pagination`, `Table`, `FileInput`, `Spinner`)
2. **Domain Fields & Displays:** (`MoneyField`, `MoneyDisplay`, `QuantityField`, `QuantityDisplay`, `PercentageField`, `PercentageDisplay`, `DateField`, `DateDisplay`, `DateTimeDisplay`, `PhoneField`, `EmailField`, `StatusBadge`, `EntityRefDisplay`)
3. **Form System:** (`FormField`, `FormSection`, `FormActions`, `ValidationSummary`, `useUnsavedChanges`, `mapServerErrorsToForm`)
4. **DataGrid:** (`DataGrid` from `@ui/grid` with exact decimal renderers)
5. **Page Archetypes:** (`ListPage`, `RecordPage`, `DocumentPage`, `SettingsPage`, `WizardPage`, `DashboardPage` from `src/frontend/src/design-system/pages/`)
6. **Platform Runtime & API:** (`httpClient`, `queryKeyFactory`, `useRuntimeContext`)
7. **Existing Module Pickers:** (`PartyPicker` from Directory, `AccountPicker` from Accounting)

The implementation plan MUST explicitly categorize:
- **Reused Shared Components:** (e.g. `ListPage`, `DataGrid`, `MoneyField`, `FilterBar`)
- **New Domain-Specific UI:** (e.g. `InvoiceLinesEditor`, `AccountPicker`)
- **Discovered Shared Foundation Gaps:** (if a generic UI pattern is missing, escalate to `design-system/` first)

---

## 3. Canonical Module Structure

A business module is organized cleanly by responsibility. Create only folders required by the feature:

```text
src/frontend/src/modules/<module-name>/
├── api/              # Module API client adapters calling httpClient
├── hooks/            # TanStack Query query/mutation hooks
├── features/         # Domain-specific feature components (e.g. PartyPicker, PaymentDialog)
├── pages/            # Archetype page implementations (ListPage, RecordPage, DocumentPage)
├── types.ts          # Module domain models and DTO interfaces
├── public.ts         # Explicit public export boundary for cross-module integration
└── tests/            # Unit, contract, and slice tests
```

---

## 4. Inviolable Module Rules

### 1. Cross-Module Boundaries & Public Surface
- Other modules MUST NOT import internal module files (`pages/`, `features/`, `api/`, `hooks/`).
- Cross-module integration occurs strictly through `public.ts`.
- **Entity Pickers Architecture:** Design System owns generic selection mechanics (`ComboBox`, `Select`). Owning modules assemble domain pickers (`PartyPicker` in Directory, `AccountPicker` in Accounting, `ProductPicker` in Catalog) by binding their module query hook and API to the ComboBox, exporting them via `public.ts`.

### 2. Direct Vendor Import Ban
- NEVER import `ag-grid-community`, `ag-grid-react`, `react-aria-components`, or `@react-aria/*` directly into business modules.
- Always import from `@ui/*` or `src/frontend/src/design-system/*`.

### 3. API & TanStack Query Pipeline
- Never call raw `fetch()` directly in pages or components.
- API calls go through `src/frontend/src/modules/<module>/api/` using `httpClient`.
- Query keys MUST use `queryKeyFactory.list(...)` or `queryKeyFactory.detail(...)` to preserve tenant/entity isolation.
- Mutations for high-risk operations (payments, postings, journals, invoicing, settlements) MUST NOT use optimistic success updates. The lifecycle is strictly `submit -> pending -> backend result -> success/failure/unknown`. Idempotency keys (`Idempotency-Key` header) must be bound to the logical attempt.

### 4. Exact Financial Data Discipline
- Authoritative financial values, quantities, and prices MUST use `DecimalString`, `MoneyValue`, or `QuantityValue`.
- NEVER use JavaScript binary floating point (`number`, `parseFloat`, `Math.*`) for financial calculations.
- Tax amounts, line totals, discounts, and balances are server-authoritative.

### 5. Multi-Axis State Models
- Do NOT collapse orthogonal state machines into a single `status`. Preserve independent axes:
  - `lifecycleStatus` (Draft / Issued / Cancelled)
  - `settlementStatus` (Unpaid / PartiallyPaid / Paid)
  - `approvalStatus` (Pending / Approved / Rejected)
  - `complianceStatus` (Compliant / NonCompliant)
  - `accountingStatus` (Unposted / Posted)
- Respect posted immutability: disable in-place edits and offer only approved correction workflows.

### 6. Standard Screen States
Every module page must explicitly handle all standard screen states:
- `loading` (spinner / skeleton)
- `empty` (empty state illustration & call-to-action)
- `error` (RFC-7807 error banner with correlation ID)
- `permission-denied` (access denied state)
- `read-only` / `locked` (disabled mutations with explicit lock reason badge)

### 7. Reference-Contract Safety
- Do NOT assume Stage-3 reference fixtures are production backend contracts.
- When implementing a real module, inspect `docs/api/platform-openapi.json` and backend endpoints; reconcile reference client adapters to real endpoints.

---

## 5. Definition of Done for a Business Module

A business module frontend is DONE only when all applicable criteria are satisfied:
1. **Contract Alignment:** Strictly matches the approved domain pack (`docs/modules/`) and backend OpenAPI.
2. **Design System Consistency:** Uses standard page archetypes, fields, and primitives without custom duplications.
3. **Arabic & RTL Quality:** CSS logical properties used; verified visually in RTL layout.
4. **Accessibility (a11y):** Keyboard navigation complete, visible focus rings, 0 Axe-core violations.
5. **Exact Formatting:** All domain values formatted with exact decimal helpers.
6. **Error & Concurrency:** Server errors mapped via RFC-7807; optimistic concurrency (`If-Match`/`ETag`) handled.
7. **Automated Verification:** Architecture boundary validator, typecheck, contract tests, and Playwright E2E tests pass.
