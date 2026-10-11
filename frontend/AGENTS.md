# Frontend Product, UX, and Engineering Operating Manual

Read the repository root `AGENTS.md` first. This file governs all frontend architecture, UX, UI engineering, and module implementation. Together with the root manual, it provides the complete, authoritative operational standard required to discover, design, build, and verify any ERP business module from scratch to `MODULE COMPLETE`.

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
│   ├── grid/        # Zero-horizontal-scroll DataGrid, column definitions, and specialized cells
│   ├── pages/       # Reusable page archetypes (ListPage, RecordPage, DocumentPage, WizardPage, Surface, PropertyGrid)
│   └── primitives/  # Accessible UI primitives (Button, Dialog, Select, ComboBox, Table, Pagination, Icons, etc.)
├── modules/     # Autonomous vertical business slices (directory, catalog, invoicing, accounting, payments, etc.)
└── tests/       # Global, contract, accessibility (axe), browser E2E (Playwright), and visual tests
```

### Module Boundary Rules
1. `app/` owns composition, shell, topbar, navigation registry, and router. It never contains business domain logic.
2. `core/` owns cross-application technical infrastructure and never imports from `modules/`.
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

Before frontend completion, inspect the backend and API contract against the Module Pack. If a documented user journey lacks the backend capability, contract, permission, or data needed for a correct experience, stop that journey and report the exact backend gap and affected acceptance scenario; do not work around business truth in the frontend. Frontend completion requires the documented user journeys and acceptance scenarios to work with canonical development seed data, or each uncovered item to be explicitly recorded as deferred with its reason and impact.

Every frontend module mission report must include the canonical Module Pack path, documented product/UX scope, acceptance scenarios, MVP and deferred items, backend/API gaps, seed scenarios used for verification, and unresolved gaps.

---

## 1. Core Product & UX Principles

### Simple Outside. Powerful Inside.
TODIR is an Arabic-first, multi-tenant enterprise ERP SaaS designed for people who spend 6 to 10 hours daily inside the interface.

* **CALM & FOCUSED**: No visual noise, unnecessary gradients, decorative glow effects, or jarring animations.
* **HIGH-DENSITY & SPACE-EFFICIENT**: ERP users need information density without clutter. Use compact spacing, clear typography, and structured grids.
* **FAST & RESPONSIVE**: Every click provides instant feedback. Loading states use spinners/skeletons, never blank screens.
* **PREDICTABLE & CONSISTENT**: Every page feels like it belongs to the exact same software suite. A user who learns one list page or record page automatically knows how to use all other 50+ modules.

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

3. **Zero Horizontal Scrolling Mandate**:
   Horizontal scrolling in tables, cards, or page bodies is a broken user experience.
   - All `Table` and `DataGrid` instances must occupy 100% of their container width (`table-layout: fixed`, `width: 100%`, `overflow: hidden`).
   - Columns must distribute proportionally using flex/fractional widths (`1fr`, percentage, or compact min-widths).
   - Content cells must use `overflow-wrap: anywhere; word-break: break-word` to ensure long names wrap cleanly without triggering horizontal scrollbars.

---

## 4. Canonical Reusable Page Archetypes (The 5 Core Systems)

Every screen in TODIR MUST be built using one of the five approved canonical page archetypes located in `frontend/components/pages/`. Never create custom page containers.

### 4.1. Archetype 1: `ListPage` (Tabular Records & Dynamic Grids)
* **When to use**: All master list screens (Customers, Products, Invoices, Journals, Users).
* **Composition Pipeline**:
  ```text
  ListPage
  ├── PageHeader (Title, Subtitle, Breadcrumbs, Primary Action button)
  ├── FilterBar (SearchField + Select dropdown filters + Active filter badge + Reset action)
  ├── DataGrid or Table (Zero horizontal scroll, localized cells, compact row height)
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

Follow this exact 10-step sequence to implement any new business module (e.g. `purchasing`, `inventory`, `hr`, `crm`):

```text
Step 1: Discover Backend OpenAPI & Business Rules
  └── Inspect backend controllers, DTOs, and OpenAPI paths in docs/
Step 2: Scaffold Module Directory Structure
  └── frontend/modules/<module-name>/{pages,features,components,services,hooks,validation,tests}
Step 3: Define Domain Types in types.ts
  └── Model clean entities, summaries, list parameters, and command inputs
Step 4: Define Zod Schemas in validation/<entity>-schema.ts
  └── Strict schema validation with clear Arabic error messages
Step 5: Implement API Service in services/<entity>-api.ts
  └── Use httpClient with parameterized URLs and standard HTTP methods
Step 6: Implement TanStack Query Hooks in hooks/<entity>-queries.ts
  └── Integrate queryKeyFactory for tenant/entity isolated cache management
Step 7: Implement List Page in pages/<entity>-list-page.tsx
  └── Compose ListPage, FilterBar, DataGrid/Table, Pagination, StatusBadge
Step 8: Implement Record / Details Page in pages/<entity>-record-page.tsx
  └── Compose RecordPage, StatCard KPIs, Surface cards, PropertyGrid
Step 9: Implement Create / Action Dialog in features/create-<entity>-dialog.tsx
  └── Accessible Dialog, React Hook Form / Zod, RFC-7807 server error handling
Step 10: Wire Routes & Public Contract
  └── Define routes.tsx, export barrel public.ts, register in app-router.tsx
```

---

## 7. Production Code Blueprints (Canonical Boilerplates)

### Blueprint A: API Client (`services/<entity>-api.ts`)
```ts
import { httpClient } from "../../../core/api/http-client.ts";
import type { Customer, CreateCustomerInput, CustomerListParams, CustomerListResult } from "../types.ts";

export const customerApi = {
  list: async (params: CustomerListParams = {}, tenantId?: string): Promise<CustomerListResult> => {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.status) query.set("status", params.status);
    if (params.page) query.set("page", String(params.page));
    if (params.pageSize) query.set("pageSize", String(params.pageSize));

    return httpClient<CustomerListResult>(`/api/v1/customers?${query.toString()}`, {
      ...(tenantId ? { tenantId } : {})
    });
  },

  getById: async (id: string, tenantId?: string): Promise<Customer> => {
    return httpClient<Customer>(`/api/v1/customers/${id}`, {
      ...(tenantId ? { tenantId } : {})
    });
  },

  create: async (input: CreateCustomerInput, tenantId?: string): Promise<Customer> => {
    return httpClient<Customer>("/api/v1/customers", {
      method: "POST",
      body: JSON.stringify(input),
      ...(tenantId ? { tenantId } : {})
    });
  },

  update: async (id: string, input: Partial<CreateCustomerInput> & { version: number }, tenantId?: string): Promise<Customer> => {
    return httpClient<Customer>(`/api/v1/customers/${id}`, {
      method: "PATCH",
      body: JSON.stringify(input),
      ...(tenantId ? { tenantId } : {})
    });
  }
};
```

### Blueprint B: Query & Mutation Hooks (`hooks/<entity>-queries.ts`)
```ts
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { customerApi } from "../services/customer-api.ts";
import type { Customer, CreateCustomerInput, CustomerListParams, CustomerListResult } from "../types.ts";
import { queryKeyFactory } from "../../../core/api/query-keys.ts";

export interface UseCustomerContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useCustomerList(params: CustomerListParams = {}, context: UseCustomerContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<CustomerListResult>({
    queryKey: queryKeyFactory.list(tenantId, entityId, "customers", "list", params),
    queryFn: () => customerApi.list(params, tenantId),
    staleTime: 30_000
  });
}

export function useCustomerRecord(id: string, context: UseCustomerContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<Customer>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "customers", "record", id),
    queryFn: () => customerApi.getById(id, tenantId),
    enabled: Boolean(id),
    staleTime: 60_000
  });
}

export function useCreateCustomer(context: UseCustomerContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Customer, Error, CreateCustomerInput>({
    mutationFn: (input) => customerApi.create(input, tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "customers", "list")
      });
    }
  });
}
```

### Blueprint C: Canonical List Page (`pages/<entity>-list-page.tsx`)
```tsx
import React, { useState } from "react";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../components/primitives/table.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { useCustomerList } from "../hooks/customer-queries.ts";
import { CreateCustomerDialog } from "../features/create-customer-dialog.tsx";
import type { CustomerSummary } from "../types.ts";

export interface CustomerListPageProps {
  readonly onSelectCustomer?: (customer: CustomerSummary) => void;
}

export function CustomerListPage({ onSelectCustomer }: CustomerListPageProps): React.JSX.Element {
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [isCreateOpen, setIsCreateOpen] = useState(false);

  const { data, isLoading, error, refetch } = useCustomerList({
    search: searchTerm.trim() || undefined,
    status: statusFilter === "all" ? undefined : statusFilter,
    page,
    pageSize
  });

  const customers = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <ListPage
      title="قائمة العملاء"
      subtitle="إدارة بيانات العملاء والحسابات التجارية والاتصال"
      breadcrumbs={[{ label: "الرئيسية", href: "/" }, { label: "العملاء" }]}
      primaryAction={{
        label: "إضافة عميل جديد",
        onClick: () => setIsCreateOpen(true)
      }}
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel="البحث عن عميل"
              placeholder="البحث بالاسم أو السجل أو الرقم الضريبي..."
              value={searchTerm}
              onChange={setSearchTerm}
            />
          }
          activeFilterCount={(statusFilter !== "all" ? 1 : 0) + (searchTerm ? 1 : 0)}
          onResetFilters={() => {
            setSearchTerm("");
            setStatusFilter("all");
            setPage(1);
          }}
        >
          <div style={{ minWidth: "160px" }}>
            <Select
              label="حالة الحساب"
              selectedKey={statusFilter}
              onSelectionChange={(key) => {
                setStatusFilter(String(key));
                setPage(1);
              }}
            >
              <SelectItem id="all" textValue="جميع الحالات">جميع الحالات</SelectItem>
              <SelectItem id="Active" textValue="نشط">نشط</SelectItem>
              <SelectItem id="Suspended" textValue="معطّل">معطّل</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
      pagination={
        totalCount > 0 ? (
          <Pagination
            page={page}
            totalPages={totalPages}
            totalItems={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
          />
        ) : undefined
      }
    >
      {isLoading ? (
        <LoadingState message="جارٍ تحميل قائمة العملاء..." />
      ) : error ? (
        <ErrorState
          title="تعذر تحميل قائمة العملاء"
          description="حدث خطأ أثناء جلب البيانات من الخادم. يرجى المحاولة مرة أخرى."
          onRetry={() => void refetch()}
        />
      ) : customers.length === 0 ? (
        <EmptyState
          title="لا يوجد عملاء مسجلون حتى الآن"
          description="ابدأ بإضافة أول عميل لتسجيل البيانات التجارية والفواتير."
          actionLabel="إضافة عميل جديد"
          onAction={() => setIsCreateOpen(true)}
        />
      ) : (
        <Table aria-label="جدول العملاء" style={{ width: "100%", tableLayout: "fixed" }}>
          <TableHeader>
            <TableRow>
              <TableHead style={{ width: "40%" }}>اسم العميل / المنشأة</TableHead>
              <TableHead style={{ width: "20%" }}>الرقم الضريبي</TableHead>
              <TableHead style={{ width: "25%" }}>البريد الإلكتروني</TableHead>
              <TableHead style={{ width: "15%", textAlign: "center" }}>الحالة</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {customers.map((c) => (
              <TableRow key={c.id}>
                <TableCell>
                  <button
                    type="button"
                    onClick={() => onSelectCustomer?.(c)}
                    style={{
                      background: "none",
                      border: "none",
                      color: "var(--color-brand-primary, #4f46e5)",
                      fontWeight: 600,
                      cursor: "pointer",
                      padding: 0,
                      textAlign: "start"
                    }}
                  >
                    {c.displayName}
                  </button>
                </TableCell>
                <TableCell>
                  <bdi style={{ fontFamily: "var(--font-mono)" }}>{c.taxNumber || "—"}</bdi>
                </TableCell>
                <TableCell>
                  <bdi dir="ltr">{c.email || "—"}</bdi>
                </TableCell>
                <TableCell style={{ textAlign: "center" }}>
                  <StatusBadge
                    intent={c.status === "Active" ? "success" : "neutral"}
                    label={c.status === "Active" ? "نشط" : "معطّل"}
                    size="small"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <CreateCustomerDialog
        isOpen={isCreateOpen}
        onOpenChange={setIsCreateOpen}
      />
    </ListPage>
  );
}
```

### Blueprint D: Canonical 360 Record Page (`pages/<entity>-record-page.tsx`)
```tsx
import React, { useState } from "react";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PropertyGrid, PropertyItem, StatCard } from "../../../components/pages/property-grid.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { BuildingIcon, MailIcon, PhoneIcon, CheckCircleIcon, TagIcon } from "../../../components/primitives/icons.tsx";
import { useCustomerRecord } from "../hooks/customer-queries.ts";

export interface CustomerRecordPageProps {
  readonly customerId: string;
  readonly onBack: () => void;
}

export function CustomerRecordPage({ customerId, onBack }: CustomerRecordPageProps): React.JSX.Element {
  const { data: customer, isLoading, error } = useCustomerRecord(customerId);
  const [editing, setEditing] = useState(false);

  if (isLoading) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "360px" }}>
        <Spinner size="large" aria-label="جارٍ تحميل بيانات العميل..." />
      </div>
    );
  }

  if (error || !customer) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "var(--color-status-danger)", fontWeight: 600, marginBottom: "1rem" }}>
          تعذر العثور على سجل العميل المطلوب.
        </p>
        <Button variant="secondary" onPress={onBack}>العودة لقائمة العملاء</Button>
      </div>
    );
  }

  const isActive = customer.status === "Active";

  const kpis = (
    <>
      <StatCard
        title="حالة الحساب"
        value={isActive ? "نشط ومفعل" : "معطّل مؤقتاً"}
        intent={isActive ? "success" : "neutral"}
        icon={<CheckCircleIcon size={20} />}
        subtitle="حالة التعامل التجاري وإصدار الفواتير"
      />
      <StatCard
        title="الحد الائتماني المعتمد"
        value={`${customer.creditLimit || "0.00"} ر.س`}
        intent="primary"
        icon={<TagIcon size={20} />}
        subtitle="أقصى رصيد آجل مسموح به"
      />
      <StatCard
        title="حالة الامتثال الضريبي"
        value={customer.taxNumber ? "مسجل ضريبياً" : "غير مسجل"}
        intent={customer.taxNumber ? "info" : "neutral"}
        icon={<BuildingIcon size={20} />}
        subtitle="مطابق لمتطلبات الفاتورة الإلكترونية"
      />
    </>
  );

  return (
    <RecordPage
      title={customer.displayName}
      recordCode={customer.code ? `#${customer.code}` : undefined}
      status={isActive ? "success" : "neutral"}
      kpiCards={kpis}
      breadcrumbs={[
        { label: "العملاء", onClick: onBack },
        { label: customer.displayName }
      ]}
      headerActions={
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          <Button variant="primary" onPress={() => setEditing(true)}>تعديل البيانات</Button>
          <Button variant="secondary" onPress={onBack}>رجوع</Button>
        </div>
      }
      auditInfo={{
        createdAt: customer.createdAt,
        updatedAt: customer.updatedAt
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
        {/* Surface 1: الهوية والبيانات التجارية */}
        <Surface
          title="الهوية والبيانات التجارية"
          description="المعلومات الأساسية والأسماء القانونية والسجلات الرسمية"
        >
          <PropertyGrid columns={3}>
            <PropertyItem
              label="الاسم التجاري"
              value={customer.displayName}
              icon={<BuildingIcon size={16} />}
            />
            <PropertyItem
              label="الاسم القانوني الكامل"
              value={customer.legalName || "—"}
            />
            <PropertyItem
              label="الرقم الضريبي (VAT)"
              value={customer.taxNumber ? <bdi style={{ fontFamily: "var(--font-mono)" }}>{customer.taxNumber}</bdi> : "—"}
              isCopyable={Boolean(customer.taxNumber)}
              rawCopyValue={customer.taxNumber}
            />
            <PropertyItem
              label="حالة الحساب"
              value={
                <StatusBadge
                  intent={isActive ? "success" : "neutral"}
                  label={isActive ? "نشط" : "معطّل"}
                  size="small"
                />
              }
            />
          </PropertyGrid>
        </Surface>

        {/* Surface 2: قنوات التواصل والعناوين */}
        <Surface
          title="قنوات الاتصال والعناوين"
          description="البريد الإلكتروني المعتمد وأرقام الهواتف والمقر الجغرافي"
        >
          <PropertyGrid columns={2}>
            <PropertyItem
              label="البريد الإلكتروني"
              value={customer.email ? <bdi dir="ltr">{customer.email}</bdi> : "—"}
              icon={<MailIcon size={16} />}
              isCopyable={Boolean(customer.email)}
              rawCopyValue={customer.email}
            />
            <PropertyItem
              label="رقم الهاتف"
              value={customer.phone ? <bdi dir="ltr" style={{ fontFamily: "var(--font-mono)" }}>{customer.phone}</bdi> : "—"}
              icon={<PhoneIcon size={16} />}
            />
            <PropertyItem
              label="العنوان الوطني / المقر"
              value={customer.address || "لم يتم تسجيل عنوان."}
              isFullWidth={true}
            />
          </PropertyGrid>
        </Surface>
      </div>
    </RecordPage>
  );
}
```

### Blueprint E: Routes & Public Contract (`routes.tsx` & `public.ts`)
```tsx
// routes.tsx
import { lazy } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";

const CustomerListPage = lazy(() => import("./pages/customer-list-page.tsx").then(m => ({ default: m.CustomerListPage })));
const CustomerRecordPage = lazy(() => import("./pages/customer-record-page.tsx").then(m => ({ default: m.CustomerRecordPage })));

function CustomerListRoute(): React.JSX.Element {
  const navigate = useNavigate();
  return <CustomerListPage onSelectCustomer={(c) => navigate(`/customers/${encodeURIComponent(c.id)}`)} />;
}

function CustomerRecordRoute(): React.JSX.Element {
  const navigate = useNavigate();
  const { customerId = "" } = useParams();
  return <CustomerRecordPage customerId={customerId} onBack={() => navigate("/customers")} />;
}

export const customerRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "customers",
    moduleIds: ["customers"],
    path: "/customers",
    label: { ar: "العملاء", en: "Customers" },
    permission: "crm.customer.read",
    icon: "directory"
  },
  routes: [
    { path: "customers", element: <CustomerListRoute /> },
    { path: "customers/:customerId", element: <CustomerRecordRoute /> }
  ]
};
```

```ts
// public.ts
export type { Customer, CustomerSummary, CreateCustomerInput } from "./types.ts";
export { customerApi } from "./services/customer-api.ts";
export { customerRoutes } from "./routes.tsx";
```

---

## 8. Quality Verification & Completion Gates

Before claiming `FRONTEND COMPLETE` or `MODULE COMPLETE`, you MUST run and pass all 9 local verification gates:

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

A passing build alone is NEVER completion. Full compliance with the UX archetypes, natural Arabic copy, zero horizontal scrolling, RTL bidirectional isolation, and automated tests is mandatory.
