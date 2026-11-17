# Module 24 — Service, Maintenance & Field Service Product & UX Acceptance Report

**Module:** Module 24 — Service, Maintenance & Field Service (`الخدمة والصيانة والعمليات الميدانية`)  
**Repository Branch:** `dev`  
**Git Remote:** `origin/dev`  
**Canonical Specification:** `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_AND_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
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
- `توزيع وجدولة` for Dispatch & Scheduling.
- `يومي الميداني` for Technician My Day.
- `قاعدة المعدات المركبة` for Installed Base.
- `صيانة وقائية دورية` for Preventive Maintenance.
- `عقود الصيانة والضمانات` for Service Contracts & Warranties.
- `فحص الاستحقاق` for Entitlement Resolution.
- `رسوم الخدمة والفوترة` for Service Charges & Billing.
- `جهة الفوترة: المشاريع (Projects)` vs `الخدمة (Service)`.

---

## 2. Real-Stack User Journeys & Acceptance Evidence

### Journey 1: Service Operations Overview & KPIs
- Authenticated user views aggregated metrics (`openWorkOrders`, `openRequests`, `visitsToday`, and calculated `availabilityPercent`).
- **Verification:** Hardcoded metrics, trends (`+12%`, `-5%`), and fake percentages (`99.4%`) were completely purged. Real metrics bind to `GET /api/v1/service/reports/kpi`.
- **Evidence:** `artifacts/service-acceptance/01-service-overview-desktop.png`

### Journey 2: Work Orders & Billing Boundary Enforcement
- Dispatchers and managers review Work Orders across Customer Field Service and Internal Maintenance contexts.
- **Verification:** Projects billing boundary is respected. Work orders with `BillingOwner == "Projects"` display clear badges and cannot request service invoices directly.
- **Evidence:** `artifacts/service-acceptance/02-service-work-orders.png`

### Journey 3: Field Technician My Day & Offline Capability
- Field technicians access their assigned visits via authenticated endpoint `GET /api/v1/service/mobile/my-day`.
- **Verification:** Technicians cannot view arbitrary visits of other users. State transitions (`travel` -> `arrive` -> `start` -> `complete`) pass optimistic row versioning. Offline state queues commands to `POST /api/v1/service/mobile/commands`.
- **Evidence:** `artifacts/service-acceptance/03-service-technician-my-day.png`

### Journey 4: Entitlement Engine & SLA Breakdown
- Support agents verify coverage for parts and labor before dispatch.
- **Verification:** Calls authoritative `POST /api/v1/service/entitlements/resolve`. Table renders exact lines with coverage source (Warranty vs Contract), covered amount, customer liability, and backend explanation.
- **Evidence:** `artifacts/service-acceptance/04-service-entitlements.png`

### Journey 5: Service Billing & Invoice Request
- Finance officers inspect accrued charges across all work orders.
- **Verification:** Bound to `GET /api/v1/service/charges`. Generates real CSV exports with accurate amounts. Triggering `Request Invoice` transitions charges to `Requested` state.
- **Evidence:** `artifacts/service-acceptance/05-service-billing.png`

### Journey 6: Field Mobility (390px Viewport)
- Technicians using handheld devices experience responsive card layouts without horizontal scrolling.
- **Verification:** Zero overflow (`scrollWidth <= innerWidth`), touch targets >= 44px, high-contrast text ratios.
- **Evidence:** `artifacts/service-acceptance/06-service-mobile-390px.png`

---

## 3. Human Product Owner Sign-off Checklist

- [x] All 9 tabs functional and integrated.
- [x] All modal creation forms validate GUIDs and match C# DTOs.
- [x] Zero fabricated figures or hardcoded statistics.
- [x] 100% Arabic/English translation parity.
- [x] All Playwright end-to-end tests passing locally.
- [x] Ready for final Product Owner verification and review.
