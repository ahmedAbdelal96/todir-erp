# 03 — Arabic-first UX, Navigation, Forms & Historical Results

**Governed by:** `FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md` and `TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md`. No new navigation framework. Every route is an independent business task and respects effective access.

## Names users actually recognize

| Context | Approved candidate page label | Natural actions |
|---|---|---|
| Sales | تحليل المبيعات | «بدء التحليل» / «التحليلات السابقة» / «عرض النتائج» |
| Subledger/Collections | تحليل تحصيلات العملاء | «تحديد العملاء» / «بدء التحليل» / «مراجعة التوصية» |
| Inventory | تحليل المخزون | «اختيار المخزن» / «المنتجات» / «عرض التحليل» |
| Purchasing | تحليل المشتريات | «اختيار الموردين» / «الفترة» |
| Banking | تحليل السيولة النقدية | «الحسابات» / «الفترة» / «النتائج السابقة» |
| Accounting | التحليل المالي | «الفترة المالية» / «المقارنة» |
| Manufacturing | تحليل الإنتاج | «أوامر الإنتاج» / «مراكز العمل» |

Avoid UI phrases `AI Intelligence`, `Smart Decisions Engine`, `Run New Analysis`, `Decision Intelligence Workspace`, literal odd translations. **Technical type IDs and route names may be English**; visible text must reflect actual business job. Do not introduce names for modules lacking an approved analysis use case.

## Routing model, e.g. Sales (PROPOSED, not existing)

- `/sales/analysis`: analyze + saved history as one dedicated task page, no work started on GET.
- `/sales/analysis/:runId`: read-only result/decisions detail, deep-linkable; authorization rechecked.
- Other domains: `/inventory/analysis`, `/subledger/collections-analysis` (subject to existing route registry), `/banking/liquidity-analysis`.
- Canonical route entries go in `frontend/app/router/destination-registry.ts` plus module's `routes.tsx`, using the same `PermissionGuard` and module navigation registration currently present. Preserve search, recents, favorites, back/forward, translated route names, mobile behavior.
- If the module's existing `Reports` or `Forecast` page covers a different operational job, **do not silently rename or replace it**. Show a distinct business route only if approved as unique job; avoid duplicate navigation entries and horizontal tab proliferation.

## Main analysis page — content contract

1. **Header:** «تحليل تحصيلات العملاء» and short business explanation, as well as last saved run timestamp if visible.
2. **Form:** selected analysis category, user-accessible legal entity, business period (from/to), and analysis-specific filters. No global filter that assumes every module uses a date range the same way.
3. **Before starting:** explain `خلال الفترة` vs `حتى تاريخ` vs `التاريخ المتوقع`; show available data cutoff, source warnings, estimated amount of work only if calibrated.
4. **Primary action:** «بدء التحليل» (single deliberate action, disabled if scope invalid or permission absent); the button submits one POST with idempotency protection, and reports queued status. No automatic trigger on date change, onMount, route prefetch, retry handler, socket reconnect, download or preview.
5. **Previous runs:** paginated and permission-scoped list with analysis type, scope, start/end, requested by, created time, status and completion time; searching/filtering history performs **reads only**.
6. **Results:** summary KPIs with definitions, evidence table, comparison if requested, charts only where they add meaning, quality/warnings, data as-of and run version, recommendations only when justified.
7. **Recommendation detail:** problem, business impact, suggested action, supporting records and assumptions, alternative option, review status, next step, review actor/time.

## Required filter vocabulary and exact meaning

| Input | Semantics / server checks |
|---|---|
| `من تاريخ / إلى تاريخ` | Business measurement period. Server validates order, max allowed window by type, date system and financial timezone; avoid using run date for business basis |
| `حتى تاريخ` | As-of snapshot cutoff for stock, aging, balances; may be separate from activity period |
| `الشركة` | Legal Entity under tenant; only permitted selections, no arbitrary client-supplied tenant_id |
| `الفرع` | Only when source facts have reliable branch scope; all branches only with explicit all-branch permission |
| `العملاء / الموردون` | Scoped searchable picker, paginated; no unauthorized IDs and no cross-entity visibility |
| `المخزن / المنتجات / الفئة` | UOM and warehouse visibility validated against owner capabilities |
| `العملة` | Display separate currencies or show explicit FX conversion policy and rate date |
| `مقارنة مع` | Defined matched period: previous same length or same calendar period previous year; no ambiguous defaults |
| `نوع العملية` | Explicit bases: posted invoices, completed payments, received goods etc.; never implicit mixing |
| `المبالغ تشمل ضريبة؟` | Only show where the metric's owner supports a meaningful legal definition |

**Each analysis definition owns a versioned filter JSON schema and validation behavior.** Query parameters may preserve draft form state but must never trigger execution.

## Screen states and copy

| State | Arabic presentation | Behavior |
|---|---|---|
| No previous run | «لم يتم إجراء تحليل لهذه الفترة بعد» | Invite explicit user action, not autoplay |
| Queued | «طلب التحليل قيد الانتظار» | Show run ID and ability to navigate away |
| Running | «جارٍ إعداد النتائج» | Status polling/SSE is **status read**, not compute trigger |
| Succeeded | «التحليل جاهز» | Load saved results only |
| Insufficient data | «البيانات المتاحة لا تكفي للوصول إلى نتيجة موثوقة» | List missing evidence; avoid misleading charts/recommendations |
| Partial | «النتائج جزئية» | Clearly show coverage and omission; never publish as full success |
| Failed | «تعذر إكمال التحليل» | Correlation ID and controlled retry via new explicit action; no sensitive exception text |
| Stale | «توجد بيانات أحدث من المستخدمة في هذا التحليل» | Keep past report intact; optional user-requested new run |
| Unauthorized | «ليس لديك صلاحية عرض هذه البيانات» | Fail closed and hide sensitive previous content |
| Read-only entitlement | «يمكنك الاطلاع على التحليلات المحفوظة فقط» | Hide run/review controls according to backend effective access |

## Historical results and recommendation UX

- Two date labels always: **فترة البيانات** + **تم التحليل في**; include source-known-as-of / currency where relevant.
- Never show invented certainty (e.g., `90% confidence`) unless calibrated and appropriately validated.
- Explain negative measures with business labels: «متأخر عن موعد السداد»، «دفعات لم تُوزَّع على الفواتير».
- Record links open the originating business module if currently authorized; otherwise show aggregate evidence without record leakage.
- A recommendation is a **proposal**, not the same thing as manager approval or a journal/PO creation.

## Frontend implementation constraints

- Follow repo's route registry and `public.ts` module boundaries; reusable Table/DatePicker/Combobox/Filters, cards and state components from approved design system; no parallel design system.
- RTL Arabic + English LTR, dates, EGP and currency precision, accessible keyboard workflows, responsive mobile and desktop, loading/error boundaries.
- Cursor-pagination for run/record/evidence lists and server-side filters; avoid fetching all rows and frontend-only pagination.
- Query cache keys include tenant, legal entity, branch, analysis type, run ID, permission revision; invalidate and clear on switching context.
- Verify narrow permission users can discover the analysis page even without unrelated Sales overview permission; no parent permission false dependency.
- Default: **No analysis cards or background fetches on operational screens**, including Sales Orders or Invoices. Future contextual display is separate product change and requires explicit approval.

## UX acceptance scenarios

1. Open Sales Orders 20×, open analysis 20×, change filters 20× → 0 analysis create requests and 0 analytics compute jobs.
2. Run action once, double-click and HTTP retry → one effective run per idempotency key.
3. Switch legal entity while a run status is loading → previous entity's content disappears immediately.
4. View old report after new data posted → original values unchanged, source freshness label updated separately.
5. User allowed `analysis.read` but not `analysis.run` can browse history only; opposite role's behavior defined by permission matrix.
6. Arabic labels reviewed by accounting/operations users; no technical jargon required to complete the workflow.
