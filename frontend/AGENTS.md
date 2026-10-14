# Frontend Product, UX, and Engineering Operating Manual

Read the repository root `AGENTS.md` first. This file governs all frontend architecture, UX, and UI engineering. Together with the root manual, it provides the operational standard to deliver `FRONTEND COMPLETE`, integrated Product/UX evidence, and a result ready for human Product acceptance. Only the complete module lifecycle plus explicit human Product Owner approval may reach `MODULE COMPLETE`.

Never create a new file or folder whose basename contains `todir` in any casing; technical names describe responsibility rather than product marketing.

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
2. `core/` owns cross-application technical infrastructure and never imports from `modules/`. Authentication binds exactly one Tenant server-side from the authenticated session; frontend code must not invent or send Tenant identity as a normal API parameter. A workflow that genuinely requires legal-entity context must obtain it from authorized business/session context, never a fake default ID.
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

### Frontend ownership and real Product/UX acceptance

Apply the root Senior Product Architect, Senior Product Designer, Senior Software / Systems Architect, Senior Developer / Engineer, and QA / Integration Owner directive to frontend work, with particular ownership of user workflows, accessible RTL UX, responsive behavior, and browser evidence. Apply this loop to every future frontend task, including defect fixes, UX, copy, routing, accessibility, responsive behavior, API integration, and tests. Do not return partial progress for a solvable in-scope defect: discover, fix, verify, inspect, and repeat. Verify affected runtime behavior in the real browser; a full module completion claim still requires every documented journey and gate.

Own the full frontend journey in the assigned scope: user tasks and role-specific decisions, workflow design, natural Arabic and RTL, API integration, permissions, loading/empty/error states, accessibility, desktop/mobile behavior, automated tests, real browser execution, screenshot inspection, and correction of Product/UX defects. Use the root coverage matrix to reconcile each MVP journey with the Module Pack, the actual backend contract, canonical seed, and observed runtime behavior. Do not implement only the visible screen or return because one Playwright click-through or screenshot succeeded.

Design for the employee doing the work and the manager reading the result. Make the common action obvious, remove safe-to-default fields and repeated entry, show trustworthy business state without requiring technical interpretation, and reveal advanced options only when useful. Use established ERP/SaaS patterns as evidence when they improve an underspecified decision, then choose the simplest workflow suitable for this Arabic-market product. Do not mirror DTOs, expose raw backend states, or make users supply internal identifiers.

Run final user journeys against the real frontend, real API, real PostgreSQL, and canonical Development Seed; use a disposable database for mutating acceptance when appropriate. Mocks, fixtures, Storybook, and simulated Playwright states do not establish final Product/UX acceptance. Open every captured screenshot and check that it visibly proves the claimed populated state, completed operation, correct Arabic copy, RTL, responsive layout, and absence of technical leaks. Check the underlying response and resulting state where screenshots cannot prove them. A preview is not proof of a committed action; tenant denial is not proof of legal-entity scope. Fix frontend-owned defects and recapture evidence before returning. For a backend-owned gap, state the exact missing capability and affected scenario, continue independent frontend work, and do not fabricate a frontend workaround.

---

## 1. Core Product & UX Principles

### Simple Outside. Powerful Inside.
TODIR is an Arabic-first, multi-tenant enterprise ERP SaaS designed for people who spend 6 to 10 hours daily inside the interface.

* **CALM & FOCUSED**: No visual noise, unnecessary gradients, decorative glow effects, or jarring animations.
* **HIGH-DENSITY & SPACE-EFFICIENT**: ERP users need information density without clutter. Use compact spacing, clear typography, and structured grids.
* **FAST & RESPONSIVE**: Every click provides instant feedback. Loading states use spinners/skeletons, never blank screens.
* **PREDICTABLE & CONSISTENT**: Every page feels like it belongs to the exact same software suite. A user who learns one list page or record page automatically knows how to use all other 50+ modules.

Product success means employees prefer using the system for daily work because entry is faster, information is easier to read, and the next action is clear. A technically working screen that forces unnecessary data entry, hides the decision-relevant state, or makes the user decode developer language does not meet this standard.

### The Sacred Boundary
```text
DATABASE LANGUAGE != DOMAIN LANGUAGE != API LANGUAGE != USER-FACING LANGUAGE
```
* **NEVER design screens by mirroring backend DTOs**: A DTO is an integration packet, not a UX specification. Do not put one field per DTO property on a screen.
* **Internal Identifiers are Forbidden in the UI**: `TenantId`, `OrganizationId`, `LegalEntityId`, `PartyId`, GUIDs (`0192e2b3-...`), and `MfaChallengeId` are technical database plumbing. They MUST NEVER be rendered as labels, inputs, or titles. Always display the human-readable business reference code (e.g. `#INV-2026-0045`, `#PRT-001`) or the entity name.
* **Dates and Timestamps**: Never expose raw ISO strings (`2026-01-01T09:00:00+00:00`). Always format using `LocalDateDisplay` or `.toLocaleDateString("ar-EG", { year: "numeric", month: "long", day: "numeric" })`.

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
   - **Currency & Exact Numbers**: Always wrap in `<MoneyDisplay>` or `<bdi>`.

3. **Prevent Accidental Horizontal Overflow**:
   Page-level and incidental horizontal scrolling in tables, cards, or page bodies is a broken user experience. Prefer responsive layouts, useful column prioritization, and readable wrapping. A genuinely wide operational dataset may use a deliberately contained grid-level horizontal navigation pattern when wrapping would make the data unreadable; keep identifying columns frozen, provide responsive alternatives where appropriate, and make the exception an intentional business-driven design choice rather than a layout workaround.

---

## 4. Canonical Reusable Page Archetypes (The 5 Core Systems)

Every screen in TODIR MUST be built using one of the five approved canonical page archetypes located in `frontend/components/pages/`. Never create custom page containers.

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
* **Key Props**:
  - `title`: e.g. `"دليل الأطراف التجارية"`
  - `subtitle`: Brief description of what the table represents.
  - `primaryAction`: `{ label: "إضافة طرف جديد", onClick: () => setIsCreateOpen(true) }`
  - `filterBar`: `<FilterBar searchInput={<SearchField ... />} ...>`
  - `pagination`: `<Pagination page={page} totalPages={totalPages} ... />`

### 4.2. Archetype 2: `RecordPage` (Master Entity 360-View Details)
* **When to use**: Master entity profiles (Party Details, Product Details, User Account Details).
* **Composition Pipeline**:
  ```text
  RecordPage
  ├── Header: Title, business code in <bdi>, status badge, action buttons (تعديل, تعطيل, رجوع)
  ├── Top KPI Row: 3 to 4 StatCard components summarizing high-impact metrics
  ├── Main Body: Multiple structured Surface cards (Identity, Legal, Contact, Settings)
  │   └── PropertyGrid (2, 3, or 4 columns) with PropertyItem key-value displays
  ├── Tabbed Sub-Views: (Optional) Tabs for related transactions, history, audit
  └── Formatted Audit Footer: Localized Egyptian/Arabic timestamp and user info
  ```
* **StatCard Standard**:
  - `title`: Metric label (e.g. `"حالة الحساب"`, `"طبيعة الكيان"`, `"الرصيد الافتتاحي"`)
  - `value`: Main stat (e.g. `"نشط ومفعّل"`, `"منشأة تجارية"`, `"15,000.00 ر.س"`)
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
  ├── Line Items Section: Surface card containing zero-scroll items table
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

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { resourceApi } from "../services/resource-api.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";
import type { CreateResourceInput, ResourceListParams } from "../types.ts";

export function useResourceList(params: ResourceListParams = {}) {
  const context = useRuntimeContext();
  return useQuery({
    // Runtime identity scopes the cache only; httpClient sends no Tenant identity.
    queryKey: queryKeyFactory.list(context.tenant.tenantId, context.legalEntity.legalEntityId, "<owning-module>", "resource", params),
    queryFn: () => resourceApi.list(params)
  });
}

export function useCreateResource() {
  const context = useRuntimeContext();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateResourceInput) => resourceApi.create(input),
    onSuccess: () => queryClient.invalidateQueries({
      queryKey: queryKeyFactory.resource(context.tenant.tenantId, context.legalEntity.legalEntityId, "<owning-module>", "resource")
    })
  });
}
```

The tenant value above is used only to isolate client cache entries; the authenticated server session binds the request to exactly one Tenant. Never add Tenant identity to request parameters, headers, or bodies. Include legal-entity context in a request only when the owning workflow requires it and the value comes from the authorized runtime context. Never invent a fallback entity or tenant ID.

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
