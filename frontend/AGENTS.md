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

Do not place every state and option on one form. Reveal information when needed. Authentication is the permanent example: initial login is email + password + `تسجيل الدخول`; after credentials, select a company only when multiple companies are available; request MFA only when required. Never show organization ID, tenant ID, or an optional MFA code on the initial login form. The general rule is `USER JOURNEY > API SHAPE`.

## 6. RTL, Arabic, responsive UX, and accessibility

RTL is mandatory and must feel natural, not merely be `direction: rtl`. Verify labels, forms, tables, dialogs, navigation, numbers, currencies, mixed Arabic/English, long product names, dates, search, icons, and directional controls. Use CSS logical properties and correct bidirectional isolation for numeric, currency, phone, and mixed-language values.

Support required viewport sizes. Do not compress a desktop table blindly onto a small screen; adapt the interaction and information hierarchy. Use semantic controls, keyboard navigation, visible focus, accessible labels, correct error association, adequate contrast, and never rely only on color. The target is zero Axe-core violations.

## 7. Platform and dependency rules

All business modules reuse the established platform. Do not create a parallel design system, form system, API client, query/cache architecture, theme engine, error mapper, page shell, runtime context, or permission UX. Use only free dependencies; commercial packages such as `ag-grid-enterprise`, `@mui/x-data-grid-pro`, and `handsontable` are prohibited. Business modules must not import `ag-grid-community`, `ag-grid-react`, `react-aria-components`, or `@react-aria/*` directly; use approved wrappers in `components/`.

Use approved primitives and fields, including buttons, fields, feedback, dialogs, menus, tabs, pagination, semantic tables, `DataGrid`, `MoneyField`/`MoneyDisplay`, quantity/percentage/date/phone/email displays, status/entity references, `FormField`, `FormSection`, `FormActions`, validation summaries, server-error mapping, and page archetypes. Standard screens use `ListPage`, `RecordPage`, `DocumentPage`, `WizardPage`, `SettingsPage`, or `DashboardPage` as appropriate and render breadcrumbs, title, actions, loading, empty, error, permission-denied, and locked states.

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
