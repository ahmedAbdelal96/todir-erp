# Module 24 — Service, Maintenance & Field Service Product & UX Acceptance Report

**Module:** Module 24 — Service, Maintenance & Field Service (`الخدمة والصيانة والعمليات الميدانية`)  
**Repository Branch:** `dev`  
**Git Remote:** `origin/dev`  
**Canonical Specification:** `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Gate Status:** `FRONTEND COMPLETE` (Ready for Human Product Owner Acceptance)

---

## 1. Product & Architecture Design Standards

### 1.1 Visual Hierarchy & Corporate Palette
- **Application Shell:** Anchored by the corporate Dark Oxford Navy header (`#102a43`), with ice-blue active accents (`#38bdf8`) and pure white headings (`#ffffff`).
- **Canvas & Contrast:** Crisp slate background canvas (`#f0f4f8`), eliminating low-contrast or borderless "ghost" cards.
- **Card Styling:** Solid white cards (`#ffffff`) with 1px border perimeter (`#cbd5e1`), dual-layer elevation shadows, and accent borders denoting state.
- **Strict Separation of Concerns:**
  - Table and List views are strictly dedicated to browsing, filtering, and search.
  - Creation workflows open focused Modal Dialogs (`NewRequestModal`, `NewWorkOrderModal`, `NewContractModal`, etc.).
  - Deep inspection uses a dedicated 360-degree Inspector Modal Drawer.
  - Zero mixed read/write collision across all 9 workspaces.

### 1.2 Enterprise Arabic Vocabulary Compliance
All user-facing Arabic microcopy strictly follows the Enterprise Arabic Vocabulary Charter:
- `أمر عمل` for Work Order (never `طلب عمل` or literal transliteration).
- `طلب خدمة` for Service Request (never `تذكرة دعم`).
- `زيارة ميدانية` for Field Visit.
- `الجدولة والتوزيع` for Dispatch & Scheduling.
- `يومي الميداني` for Technician My Day.
- `قاعدة المعدات المركبة` for Installed Base.
- `الصيانة الوقائية` for Preventive Maintenance.
- `العقود والضمانات و SLA` for Service Contracts, Warranties & SLA.
- `التحقق من الاستحقاق` for Entitlement Resolution.
- `الرسوم والمطالبات المالية` for Service Charges & Billing.
- `جهة الفوترة: المشاريع (Projects)` vs `الخدمة (Service)` vs `بدون فوترة`.

---

## 2. Real-Stack User Journeys & Acceptance Evidence

### Journey 1: Service Operations Overview & Authoritative KPIs
- Authenticated user (`admin@atlas.com`) views live aggregated metrics from PostgreSQL.
- **Verification:** Hardcoded metrics, trends (`+12%`, `-5%`), and fake percentages (`99.4%`) were completely purged. Real metrics bind to `GET /api/v1/service/reports/kpi` with legal-entity scoping.
- **Artifact:** `artifacts/service-acceptance/real-stack/01-real-service-overview-desktop.png`

### Journey 2: Service Request Lifecycle & Database Commitment
- Users register customer field service or internal asset maintenance requests.
- **Verification:** Form selects installed base item (`ضاغط تبريد العميل`), auto-fills customer party ID (`cdcdd524-629a-7f56-a9ae-19b80fc05a48`), and commits record to `business_service.service_request`. Browser is reloaded and confirms persisted record in table.
- **Artifact:** `artifacts/service-acceptance/real-stack/02-real-service-request-triaged.png`

### Journey 3: Work Orders & Billing Boundary Enforcement
- Dispatchers and managers create and review Work Orders across Customer Field Service and Internal Maintenance contexts.
- **Verification:** Projects billing boundary is respected. Work orders with `BillingOwner == "Projects"` display clear blue badges; `Service` displays green; `None` displays neutral slate. Persisted to live database.
- **Artifact:** `artifacts/service-acceptance/real-stack/03-real-work-order-lifecycle.png`

### Journey 4: Dispatcher Workbench & Unscheduled Orders Queue
- Dispatchers review scheduled field visits and the unscheduled work orders queue.
- **Verification:** Surface loads real scheduled visits and ready work orders without synthetic rows.
- **Artifact:** `artifacts/service-acceptance/real-stack/04-real-dispatcher-visit-assigned.png`

### Journey 5: Field Technician My Day, Resource-Level Authorization & Discovery Isolation
- Field technicians access only their assigned visits via authenticated endpoint `GET /api/v1/service/mobile/my-day` and `GET /api/v1/service/visits`.
- **Verification:** Authenticated as `technician.service@atlas.com`. Technician transitions visit (`Travel` / `بدء التحرك`), verified in live database. Another technician cannot view or mutate visits assigned to this technician. Direct API requests to visits or work order sub-resources outside assignment return HTTP 403 Forbidden. List APIs filter visits to assigned items only, preventing unauthorized discovery. Immediate assignment revocation halts subsequent mutations instantly.
- **Backend Verification:** `IntegrationTests.ServiceSecurityIntegrationTests.TechnicianResourceLevelAuthorizationEnforced` passed.
- **Artifact:** `artifacts/service-acceptance/real-stack/05-real-technician-my-day.png`

### Journey 6: Durable Offline Queue, Local Persistence, Pending Semantics & Reconnect Sync
- Field technicians operate disconnected from network and queue work commands locally.
- **Verification:** Network disconnected via Chromium DevTools protocol. Mutation queued in tenant/user-scoped `localStorage`. Page reloaded while offline, queue verified intact in DOM. Server-received commands with incomplete processing display distinct amber badge `قيد المعالجة بالخادم (Pending on Server)` and are not conflated with authoritative completion (`synced`). Replay requires authenticated session user to match author identity. Network reconnected; user triggers sync (`مزامنة الأوامر الآن`); backend processes mutation (`POST /api/v1/service/mobile/commands`) with optimistic concurrency checks against `business_service.service_visit`.
- **Artifact:** `artifacts/service-acceptance/real-stack/06-real-technician-offline-sync.png`

### Journey 7: Authoritative Entitlement & Warranty Resolver
- Support agents and dispatchers verify coverage for parts and labor before dispatch.
- **Verification:** Calls authoritative backend engine `POST /api/v1/service/entitlements/resolve`. Table renders exact breakdown lines with coverage source (Warranty vs Contract), covered amount, customer liability, and backend explanation.
- **Artifact:** `artifacts/service-acceptance/real-stack/07-real-entitlement-engine.png`

### Journey 8: Service Charges & Multi-Currency Billing
- Finance officers inspect accrued charges across all work orders.
- **Verification:** Backend `GET /api/v1/service/charges/summary` computes multi-currency aggregates across ALL records without summing unconverted currencies (e.g. SAR vs EGP). Server-side pagination with Next/Previous navigation and total counts.
- **Artifact:** `artifacts/service-acceptance/real-stack/08-real-billing-charges.png`

### Journey 9: Field Mobility Layout (390px Viewport)
- Technicians using mobile handheld devices experience responsive touch-first card layouts.
- **Verification:** Zero overflow (`scrollWidth <= innerWidth`), touch targets >= 38px, high-contrast text ratios.
- **Artifact:** `artifacts/service-acceptance/real-stack/09-real-service-mobile-390px.png`

---

## 3. Human Product Owner Sign-off Checklist

- [x] All 9 workspace tabs functional and integrated.
- [x] All modal creation forms validate GUIDs and match backend C# DTOs.
- [x] Zero fabricated figures, hardcoded statistics, or synthetic CSV rows.
- [x] Multi-currency aggregation groups distinct currencies safely.
- [x] Durable client offline queue handles offline state, auto-replay, version conflicts with tenant/user isolation, and distinct Pending/Accepted vs Processed semantics.
- [x] Field technician resource-level authorization and discovery isolation strictly enforced at the backend and proven by direct HTTP tests.
- [x] 100% Arabic/English translation parity verified clean.
- [x] Database protection probe prevents mutation tests against dev database.
- [x] Real-stack Playwright suite (9/9) passes against live .NET backend and PostgreSQL.
- [x] Backend integration test suite passes (including `ServiceSecurityIntegrationTests` 2/2 and `ServiceOperationalAcceptanceTests` 1/1).
- [x] Mocked UI suite (6/6) passes and is explicitly classified as `[MOCKED_UI_ACCEPTANCE]`.
- [x] Ready for final Product Owner verification and acceptance.
