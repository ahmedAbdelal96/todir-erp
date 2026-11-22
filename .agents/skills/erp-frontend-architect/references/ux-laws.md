# TODIR ERP UX Laws & Interaction Architecture

ERP screens are high-frequency business tools. An accountant, sales manager, or inventory clerk works in TODIR 8 hours a day. Every extra click, sluggish reload, or confusing layout adds daily operational fatigue.

---

## Law 1: The 5-Second Rule

Within 5 seconds of opening any screen, a business user must clearly understand:
1. **Where they are** (Entity and scope, e.g. "فواتير المبيعات — شركة أطلس للتجارة").
2. **Current state** (Status pills, count chips, filter summaries).
3. **The next primary action** (Prominently styled, unambiguous label).

**Guidelines:**
- **Know once, ask never**: Never ask the user to re-select tenant, company, current legal entity, active user, or today's date if the context already knows it.
- **Single Primary Action**: Only one prominent primary button per view (`+ إضافة عميل`, `+ إنشاء فاتورة جديدة`).

---

## Law 2: Strict Separation of Concerns (Zero Mixed View/Form Collision)

TODIR enforces strict separation between browsing data and editing/creating data.
**A screen must NEVER combine a data register/table with an inline creation form.**

| Interaction Scope | Field Count | Container Archetype | Rules |
|---|---|---|---|
| **Quick Entry / Status Change / Confirm** | 1 to 4 | **Centered Modal Dialog (`Dialog`)** | Backdrop overlay, Esc/click-outside dismiss guard on dirty state. |
| **Medium Workflow / Line Item / Secondary Profile** | 5 to 12 | **Side Drawer or Focused Dialog** | Slides from reading start (right in RTL), table remains visible in background. |
| **Complex Document / Multi-line Journal / Contract** | 13+ or multi-stage | **Dedicated Creation Route (`/new`, `/draft`)** | Clean full-width canvas, stepper, draft autosave, explicit back button. |
| **Inspect Record Details** | n/a | **Detail Drawer (360 Inspector)** | Triggered by row click; never navigates away; preserves table scroll, filters, and page. |

---

## Law 3: High-Contrast Corporate Design Standard

Pale, washed-out, borderless screens cause severe eye strain and look amateurish. TODIR enforces a crisp corporate visual hierarchy:

- **Header Anchor**: Dark Oxford Navy (`#102a43`).
- **Page Canvas**: Cool neutral slate (`#f0f4f8`).
- **Surface Cards**: Pure crisp white (`#ffffff`).
- **Perimeter Borders**: Solid perimeter border (`#cbd5e1`, 1px solid) on all cards, tables, and dialogs.
- **Dual-Layer Shadows**: Elevation depth (`0 1px 3px rgba(15,23,42,0.08), 0 1px 2px rgba(15,23,42,0.04)`).
- **Prohibited**: Borderless "ghost" cards that melt into the canvas without boundary contrast.

---

## Law 4: Compact Metric Ribbon & The Rule of Three

Do not bury tables under an endless grid of oversized KPI cards. On operational screens:

- **Metric Ribbon**: Slim, compact strip under tabs or table toolbar.
- **Rule of Three**: At first glance, show at most **three** headline figures:
  1. *Total / Planned* (e.g. إجمالي المستهدف)
  2. *Actual / Realized* (e.g. المحقق فعلياً)
  3. *Remaining / Variance* (e.g. المتبقي / الانحراف)
- **Status Counts as Filter Chips**: Instead of a card per status, use clickable status filter pills:
  `[الكل 120] [جديد 15] [قيد المعالجة 45] [معتمد 60]`
  Clicking a chip instantly filters the table.
- **Data-first density**: KPI cards are compact summaries, not dashboards inside the dashboard. Use content-sized height, restrained vertical padding, and a stable hierarchy of title → value → short helper text. Do not add fixed heights or empty space merely to make cards feel large.
- **Protect the data surface**: Tables, lists, and operational panels receive the majority of the viewport. A KPI ribbon must not force the user to scroll past oversized cards before reaching actionable records. When more metrics are needed, use compact chips or a summary row, then place detailed values in the relevant table or panel.
- **Responsive behavior**: Keep the ribbon in a dense multi-column layout when space allows and stack it without horizontal overflow on narrow screens. Compactness must never reduce contrast, readable type, RTL alignment, or the separation between a metric label and its value.

---

## Law 5: The Six Mandatory UI States

Every view, tab, table, and inspector must gracefully support:

1. **Loading Skeleton**: Reserve exact layout dimensions with pulsing gray skeletons to prevent cumulative layout shift (CLS).
2. **Guided Empty State**: When no data exists, display a clear icon, explanatory Arabic text, and a primary CTA button to create the first item.
3. **Actionable Error State**: When API calls fail, display an understandable Arabic error message with a prominent `إعادة المحاولة` (Retry) button.
4. **No Permission State**: If the user lacks access, show an informative, calm state; never let them attempt an action and crash with HTTP 403.
5. **Partial / Filtered State**: When filters yield zero results, display `لا توجد نتائج مطابقة لبحثك` with a `مسح التصفية` (Clear Filters) button.
6. **Stale / Concurrency State**: When an item was updated by another user, notify calmly: `تم تعديل هذا السجل بواسطة مستخدم آخر` with a refresh action.

---

## Law 6: Deep-Linkable Workspaces

Every meaningful filter and selection must be reflected in the URL query string:
- `?page=2&status=Active&search=Atlas&id=<selected-record-id>`
- Refreshing the browser or sharing a URL must restore the exact same view, active tab, table page, and open detail drawer.
- Search input must be debounced (300ms) before updating URL and issuing API queries.

---

## Law 7: Task Discoverability & Independent Destination Deep Linking

Users interact with enterprise software by tasks, not by monolithic system modules. A cashier comes to count physical cash, an accountant reconciles statement lines, and a sales rep manages leads.

1. **Shallow Grouped Primary Navigation**:
   - The primary navigation groups all modules into 7 clear domain categories: `Workspace` (الرئيسية), `Sales & Customers` (المبيعات والعملاء), `Purchasing & Suppliers` (المشتريات والموردون), `Inventory & Operations` (المخزون والتشغيل), `Finance & Treasury` (المالية والخزائن), `Human Resources` (الموارد البشرية), `Administration & Settings` (الإدارة والإعدادات).
2. **Zero Single-Permission Module Gates**:
   - Never gate a module behind a single coarse permission. An employee with cash-count permission must never be blocked from the Banking module just because they lack bank-account management rights.
3. **Dedicated Task URLs**:
   - Every major task must have a distinct, shareable URL route (e.g. `/banking/cash`, `/banking/statements`, `/crm/leads`, `/crm/opportunities`).
4. **Canonical Deep Linking and Safe Legacy Compatibility**:
   - Direct nested paths (`/banking/cash`) are canonical for independent tasks. A legacy query entry (`/banking?tab=cash`) may be accepted only as a one-time redirect to the registered canonical path after destination permission evaluation.
   - The redirect must remove only `tab`, preserve unrelated query parameters, fail closed for an unauthorized target, and never allow `?tab=` to override a protected `initialTab` on a guarded route. Query tabs remain valid only for the same-record/workbench context.
5. **Fail-Closed Protection**:
   - Direct manual URL entry into an unauthorized task destination must fail closed and render `<DefaultAccessDeniedView>` with an explanation and clean fallback, never a blank screen, uncaught crash, or partial data leak.
