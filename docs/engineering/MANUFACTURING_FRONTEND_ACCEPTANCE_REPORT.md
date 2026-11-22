# Module 26 — Manufacturing & MRP Frontend Acceptance & Verification Report

**Module:** Module 26 — Manufacturing & MRP (`التصنيع وتخطيط متطلبات المواد`)  
**Repository Branch:** `dev` ONLY  
**Delivery Gate Status:** `FRONTEND COMPLETE — READY FOR HUMAN PRODUCT/UX ACCEPTANCE`  
**Target Delivery SHA:** Latest working commit on `dev`  
**Canonical Specification:** `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Backend Reference Documents:**
- `docs/engineering/MANUFACTURING_BACKEND_COVERAGE_MATRIX.md`
- `docs/engineering/MANUFACTURING_BACKEND_EXECUTION_STATE.md`
- `docs/engineering/MANUFACTURING_BACKEND_FRONTEND_HANDOFF.md`

---

## 1. Product & Architecture Design Standards

### 1.1 Codified High-Contrast Corporate Palette
Every user-facing screen strictly adheres to TODIR ERP's governed design system codified in `/frontend/AGENTS.md` and `.agents/skills/erp-frontend-architect/SKILL.md`:
- **Application Shell:** Anchored by the corporate Dark Oxford Navy header (`#102a43`), with active ice-blue accents (`#38bdf8`) and pure white headings (`#ffffff`).
- **Canvas & Contrast:** Crisp slate background canvas (`#f0f4f8`), eliminating low-contrast or borderless "ghost" cards.
- **Card Styling:** Solid white cards (`#ffffff`) with 1px border perimeter (`#cbd5e1`), dual-layer elevation shadows, and semantic accent borders for lifecycle states.
- **Strict Separation of Concerns:**
  - Table and List views are strictly dedicated to browsing, filtering, and search.
  - Creation workflows open focused Modal Dialogs (`CreateOrderModal`, `CreateDefinitionModal`, etc.) or navigate to dedicated workspace routes.
  - Deep inspection uses a dedicated 360-degree Inspector Drawer and comprehensive order workspace (`/manufacturing/orders/:id`).
  - Zero mixed read/write collision across all 10 workspaces.

### 1.2 Enterprise Arabic Vocabulary Compliance
All user-facing Arabic microcopy strictly follows the Enterprise Arabic Vocabulary Charter:
- `أمر تصنيع` for Production Order (never `طلب تصنيع` or machine translation).
- `بطاقة صنف مجمّع (BOM)` for Bill of Materials.
- `مسار تشغيل ومحطات عمل` for Routing & Work Centers.
- `مرحلة تشغيلية` for Operation / Routing Step.
- `صالة التشغيل وأرضية المصنع` for Shop Floor Workbench.
- `ترحيل واستهلاك المواد` for Material Issue & Consumption.
- `فحص الجودة والحجر الصناعي` for Quality Inspection & Quarantine.
- `تسوية وتكاليف الإنتاج` for Manufacturing Costing & WIP Reconciliation.
- `تخطيط متطلبات المواد (MRP)` for MRP Planning Runs & Order Suggestions.
- `سلسلة الأنساب والتتبع` for Lot & Serial Genealogy.

---

## 2. Visual & Operational Defect Resolutions

### 2.1 Materials Readiness & Issuance Page (`materials-page.tsx`)
- **Product & SKU Resolution:** Replaced raw UUIDs with human-friendly product display names and authentic SKUs (e.g. `ورق تصوير أبيض A4` with `رمز الصنف (SKU): EG-2-0001`) resolved dynamically via `useCatalogProducts()`.
- **Shortage & Accountability Status:** Added a distinctive shortage badge (`shortageQty = required - issued`) highlighting unfulfilled material requirements, alongside clear indicators for fully accounted components.
- **Compact Contextual Actions:** Replaced crowded inline table buttons with a primary contextual CTA and an accessible dropdown action menu (`aria-label`) to preserve whitespace and prevent table distortion.
- **Mobile Responsiveness:** Implemented contained horizontal table scrolling with zero page-level horizontal overflow.

### 2.2 MRP Planning Workspace (`mrp-page.tsx`)
- **Item Resolution:** Resolved catalog product names and SKUs across all planned supply items.
- **Supply State Clarity:** Clearly distinguished `Suggested` recommendations from confirmed orders, rendering a distinct green `✓ أمر إنتاج مؤكد ومُثبَّت` badge with linked order number for firmed lines.
- **MRP Netting Guidance:** Retained the comprehensive netting logic explainer card describing how gross requirements, sales demands, unconsumed forecasts, and stock reservations interact.

### 2.3 Shop Floor Workbench (`shop-floor-page.tsx`)
- **Resolved Empty Queue Defect:** The seeded in-progress order (`MO-SEED-0001`) had a need date prior to current date. Updated the backend query in `ManufacturingOperationalExtensions.ListShopFloorQueueAsync` to include active running/overdue operations:
  `and ((o.need_date between $3 and $4) or (o.state in ('Released','InProgress') and o.need_date <= $4))`.
- **Horizon & State Filters:** Added interactive scheduling horizon selector (`activeOverdue`, `today`, `thisWeek`, `thisMonth`) and operation status filters, with an informative empty state providing a one-click filter reset.
- **Clear Order References:** Displayed human-readable order numbers (e.g. `MO-SEED-0001`) with `<BdiText>` wrapper instead of raw GUIDs.

### 2.4 Close Readiness Panel (`close-readiness-panel.tsx`)
- **Human-Readable Business Terminology:** Mapped all 9 backend blocker codes (`output_incomplete`, `material_unaccounted`, `operations_incomplete`, `output_unreconciled`, `wip_negative`, `wip_unreconciled`, `ledger_unposted`, `quality_unreconciled`, `cost_unreconciled`) to localized Arabic/English business titles.
- **Actionable Recovery Advice:** Added clear resolution guidance directing the user to the exact required action (e.g. staging missing material returns, confirming inventory receipts, completing open machine steps, or posting ledger vouchers).

### 2.5 Quality Gates Workspace (`production-order-workspace-page.tsx`)
- **Exempt Status Reassurance:** When `order.qualityState === "Exempt"`, displays a clean status badge and reassuring guidance card clarifying that this item is exempt from shop-floor inspection per engineering policy, removing misleading update CTAs.

### 2.6 Master Definitions Page (`definitions-page.tsx`)
- **Catalog & Plant Name Resolution:** Replaced raw IDs in Production Definitions, BOMs, and Formulas with human-readable plant names (e.g. `المصنع الرئيسي`), product names, and SKUs.

### 2.7 Mobile Navigation & Responsive Layout
- **Manufacturing Header (`manufacturing-header.tsx`):** Added smooth touch scrolling for workspace tabs (`scrollbarWidth: "none"`, `WebkitOverflowScrolling: "touch"`), preventing horizontal page overflow on small viewports (390px iPhone).

---

## 3. Authoritative Financial & Costing Projection

### 3.1 Dynamic Legal-Entity Base Currency Resolution
- **Removed Fallback:** Completely removed implicit `"EGP"` fallback from `ManufacturingExecutionRepository.cs`.
- **Authoritative Resolution:** Queries `platform.legal_entity.base_currency_code` dynamically. If missing, throws a strict closed `ManufacturingException("manufacturing.legal_entity_currency_missing")`.
- **Multi-Currency WIP Isolation:** Output valuation queries strictly enforce `p.currency_code = $3` (`baseCurrency`), preventing unapproved foreign or null currency valuations from aggregating into operational WIP balance.
- **Automated Unit Proof:** Unit test `CostingSummaryThrowsWhenLegalEntityBaseCurrencyIsMissing` added and verified in `ManufacturingLifecycleAndCostingTests.cs`.

### 3.2 UI Distinction: Operational WIP vs General Ledger State
- **Explicit Unavailable State:** `manufacturing-costing-page.tsx` renders `"—"` on empty or loading queries instead of a deceptive `0 EGP`.
- **Transparent Terminology:** The balance card is clearly titled **"رصيد الـ WIP التشغيلي التقديري"** with explicit notes stating that this is an operational inventory valuation estimate subject to General Ledger posting and variance clearance during financial closing.

---

## 4. Isolated Acceptance Environment & Execution Gates

### 4.1 Dedicated Acceptance Database (`todir_mfg_acceptance`)
- **Complete Isolation:** Created a dedicated acceptance database on PostgreSQL (`localhost:15432`) separated from developer data.
- **Full Schema & Migrations:** Applied all 240 platform migrations and canonical development seed scenarios via `DbMigrator --seed development`.
- **Automated Harness Connection:** `playwright.config.ts` points `TODIR_DATABASE_CONNECTION` directly to `todir_mfg_acceptance` with `reuseExistingServer: !process.env.CI`.

### 4.2 Comprehensive Test Suite Results

| Test Suite | Spec File | Total | Passed | Failed | Status |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **Real-Stack Transactional Suite** | `manufacturing-transactions-real-stack.spec.ts` | 9 | 9 | 0 | **PASS (100%)** |
| **Real-Stack Read/Smoke Suite** | `manufacturing-real-stack.spec.ts` | 9 | 9 | 0 | **PASS (100%)** |
| **Mocked Component Regression** | `manufacturing-flow.spec.ts` | 6 | 6 | 0 | **PASS (100%)** |
| **Backend Unit & Costing Tests** | `ManufacturingTests.csproj` | 14 | 14 | 0 | **PASS (100%)** |

### 4.3 Transactional Acceptance Scenarios Verified (`manufacturing-transactions-real-stack.spec.ts`)

| Scenario ID | Test Scenario Description | API / Domain Operation Verified | Result |
| :--- | :--- | :--- | :--- |
| **TX-01** | Master Definition Creation & Validation | `POST /api/v1/manufacturing/definitions` creates Discrete definition with real BOM version (201 Created); asserts negative guard (400) on invalid date interval (`validTo < validFrom`). | **PASS** |
| **TX-02** | Production Order Lifecycle Transitions | Creates order via API (Draft, v1), releases order (Released, v2), starts order (InProgress, v3); verifies row version incrementation and live UI state. | **PASS** |
| **TX-03** | Material Conservation & Reservation Guards | Asserts Material Conservation Invariant: direct consumption exceeding issued qty rejected with HTTP 400; asserts inventory reservation requirement on issuance. | **PASS** |
| **TX-04** | MRP Planning Run & Demand Pegging | Creates planning run, adds demand requirement, executes MRP calculation, verifies planned supplies, and exercises production order firming. | **PASS** |
| **TX-05** | Shop Floor Operations Queue & Progress | Navigates to Shop Floor Workbench, verifies registered work centers, scheduling horizon selector, and active operation queue visibility. | **PASS** |
| **TX-06** | Quality Policy Role Security Guard | Authenticates as restricted role (`warehouse@atlas.com`); asserts direct policy mutation returns HTTP 403 Forbidden; verifies admin UI Quality tab. | **PASS** |
| **TX-07** | Lot/Serial Genealogy Security Guard | Asserts security invariant: attempting genealogy link with forged/mismatched output IDs returns HTTP 400/404; verifies forward/reverse trace UI. | **PASS** |
| **TX-08** | Authoritative Costing & Close Rejection | Queries live costing summary verifying authoritative legal entity currency (`EGP`); attempts close mutation on incomplete order asserting 400 Bad Request rejection (`manufacturing.close_blocked`). | **PASS** |
| **TX-09** | SaaS Commercial Entitlement Invariant | Queries effective access API verifying Atlas tenant has active manufacturing entitlements; verifies unauthenticated requests return HTTP 401 Unauthorized. | **PASS** |

### 4.4 Quality & Governance Verification Gates
- **TypeScript Strict Check:** `pnpm --dir frontend run typecheck` — 0 errors.
- **Frontend Architecture Lint:** `pnpm run architecture:frontend` — 100% clean, all module boundaries respected.
- **Localization Parity:** `pnpm run localization:verify` — 100% clean Arabic/English resource parity.
- **Production Build:** `pnpm --dir frontend run build` — Clean production bundle compiled in 1.90s.
- **Concurrent Repository Tree Preservation:** `backend/Tests/Integration/NavigationSecurityAcceptanceTests.cs` preserved untouched.

---

## 5. Visual Artifacts & Proof of Real-Stack Execution

All screenshots are stored in `artifacts/manufacturing-acceptance/` and copied to the conversation artifact directory:

### Transactional Real-Stack Artifacts (`transactions-real-stack/`)
1. `tx-01-definition-created.png` — Creation of Production Definition with real BOM version and plant name.
2. `tx-02-order-lifecycle.png` — Status transitions (`Draft` -> `Released` -> `InProgress`) with row versioning in 360 Workspace.
3. `tx-03-material-conservation.png` — Materials management page verifying material conservation invariant.
4. `tx-04-mrp-planning-run.png` — Execution of MRP planning run with suggested supplies and netting breakdown.
5. `tx-05-shop-floor-queue.png` — Shop floor workbench with active work center operations queue.
6. `tx-06-quality-policy-guard.png` — Quality policy security guard and reassurance presentation.
7. `tx-07-genealogy-trace.png` — Forward and reverse genealogy tree with lot rejection validation.
8. `tx-08-costing-summary-authoritative.png` — Costing summary card displaying authoritative EGP operational WIP balance.
9. `tx-09-entitlement-security-verified.png` — Commercial SaaS entitlement validation across tenant states.

### Read-Only / Smoke Suite Artifacts (`real-stack/`)
1. `01-real-orders-register.png` — Orders register with live filtering, pagination, and status summaries.
2. `02-real-order-workspace-360.png` — 360-degree order workspace with tabbed navigation and operation timeline.
3. `03-real-materials-execution.png` — Bill of materials execution and component reservation status.
4. `04-real-mrp-planning.png` — MRP parameters, master production schedule, and demand forecast.
5. `05-real-shop-floor.png` — Dispatch board, work center workload, and active job tracking.
6. `06-real-quality-policy-guard.png` — Quality hold register and inspection parameter checklists.
7. `07-real-costing-summary-wip.png` — Operational WIP summary and actual vs standard variance breakdown.
8. `08-real-master-definitions.png` — Engineering definitions, routings, and bill of materials catalog.
9. `09-real-mobile-390px-orders.png` — Responsive mobile layout for floor operators (390px viewport, zero horizontal overflow).

---

## 6. Human Product Owner Sign-off Checklist

- [x] All 10 workspace tabs functional, interactive, and integrated with live backend.
- [x] Dedicated acceptance database `todir_mfg_acceptance` provisioned with all 240 migrations and canonical seed.
- [x] Real-stack transactional suite (9/9) proves full business lifecycle against live .NET API and PostgreSQL.
- [x] Real-stack read/smoke suite (9/9) verifies live read models, filters, and mobile responsiveness.
- [x] Mocked regression suite (6/6) categorized and passing.
- [x] Backend tests (14/14) passing including currency fallback unit tests.
- [x] Visual and operational defects on Materials, MRP, Shop Floor, Quality, Costing, and Definitions fully resolved.
- [x] Costing projection dynamically queries legal-entity base currency and rejects foreign/null currencies from operational WIP.
- [x] Close readiness blocker codes translated into localized Arabic/English business advice.
- [x] Fail-closed authentication credentials enforced (zero hardcoded passwords).
- [x] 100% Arabic/English translation parity verified clean.
- [x] Concurrent work preserved untouched (`NavigationSecurityAcceptanceTests.cs` left intact).
- [x] Production build passes cleanly with 0 TypeScript errors.

**Delivery Status:** **`FRONTEND COMPLETE — READY FOR HUMAN PRODUCT/UX ACCEPTANCE`**
