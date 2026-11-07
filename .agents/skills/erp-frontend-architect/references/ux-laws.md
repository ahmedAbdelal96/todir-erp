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
