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

## 2. SaaS Commercial Entitlement Invariants Restored

### 2.1 Reversion of Unauthorized Policy Change
- **Manifest Restoration:** Restored `purchasable: true` in both:
  - `backend/Modules/Manufacturing/module.manifest.json`
  - `backend/Modules/Manufacturing/ManufacturingModule.cs` (`ManufacturingModuleManifestProvider`).
- **Development Seed Coverage:** Updated `backend/DbMigrator/SeedData/PlatformSeed.cs` to ensure canonical development tenants (`Atlas` and `Delta`) have an active `platform.entitlement_snapshot` containing `"manufacturing"` and an active `platform.module_instance` record with `status = 'Active'`.

### 2.2 Strict Lifecycle & Access Enforcement
The backend `NpgsqlModuleLifecycleEnforcer` and security architecture are verified across all tenant subscription states:
- **Active Tenant:** Full read, execution, and mutation permissions allowed.
- **ReadOnly Tenant:** Read queries succeed; mutation endpoints return HTTP 403 Forbidden with reason `module.lifecycle_readonly`.
- **Disabled / Unentitled Tenant:** Both read and mutation endpoints return HTTP 403 Forbidden with reason `module.unentitled` or `module.disabled`.
- **Automated Proof:** Verified via `backend/Tests/Modules/Manufacturing/ManufacturingLifecycleAndCostingTests.cs` (7/7 unit tests covering lifecycle status evaluation and response models).

---

## 3. Authoritative Financial & Costing Projection

### 3.1 Legal-Entity Base Currency Resolution
- **Removed Hardcoded Fallback:** Purged hardcoded `"EGP"` string in `ManufacturingExecutionRepository.GetCostingSummaryAsync`.
- **Dynamic Resolution:** Base currency is now queried dynamically from the authoritative legal-entity profile (`platform.legal_entity.base_currency_code`).
- **Multi-Currency Isolation:** WIP calculation queries strictly filter actual cost facts and outputs by `$3` (`baseCurrency`) to prevent combining multi-currency figures (e.g. SAR vs EGP) into a single misleading operational balance.

### 3.2 UI Distinction: Operational WIP vs General Ledger State
- **Explicit Unavailable State:** `manufacturing-costing-page.tsx` now renders `"—"` on empty or failed queries instead of falling back to misleading `0 EGP`.
- **Transparent Terminology:** The balance card is clearly titled **"رصيد الـ WIP التشغيلي التقديري"** (Estimated Operational WIP Balance) with notes explicitly explaining that this is an operational inventory valuation estimate subject to formal General Ledger posting and variance clearance during financial closing.

---

## 4. Comprehensive Test Suite Classification & Execution Gates

### 4.1 Mocked Regression Suite (`manufacturing-flow.spec.ts`)
- **Status:** **PASS** (6/6 tests passed).
- **Scope:** Fast UI regression and form interaction tests using intercepted mocked routes.
- **Classification:** Strictly labeled as client-side regression suite; not claimed as full-stack proof.

### 4.2 Real-Stack Read-Only & Smoke Suite (`manufacturing-real-stack.spec.ts`)
- **Status:** **PASS** (9/9 tests passed).
- **Scope:** Real authentication against live .NET API (`http://127.0.0.1:5080`) and PostgreSQL (`15432`), verifying live read models, filter operations, 360-degree drawers, and mobile layout.
- **Security:** Hardcoded passwords removed; fail-closed credentials enforced via environment variables (`ERP_DEV_PASSWORD` / `SEED_DEFAULT_PASSWORD`).

### 4.3 Real-Stack Transactional Acceptance Suite (`manufacturing-transactions-real-stack.spec.ts`)
- **Status:** **PASS** (9/9 tests passed).
- **Scope:** Genuine transactional business mutations committed against live .NET backend and PostgreSQL.

| Scenario ID | Test Scenario Description | API / Domain Operation Verified | Result |
| :--- | :--- | :--- | :--- |
| **TX-01** | Master Definition Creation & Validation | `POST /api/v1/manufacturing/definitions` creates Discrete definition; verified in UI list. | **PASS** |
| **TX-02** | Production Order Lifecycle Transitions | Transition `Draft` -> `Released` -> `InProgress` on order `MO-00000001` with status badges verified. | **PASS** |
| **TX-03** | Material Issue & Conservation Guard | `POST /api/v1/manufacturing/executions/{id}/materials/issue` commits material consumption and updates actual usage. | **PASS** |
| **TX-04** | MRP Planning Run & Order Suggestion | `POST /api/v1/manufacturing/mrp/runs` triggers MRP run; verified suggestions queue and regeneration. | **PASS** |
| **TX-05** | Shop Floor Dispatch & Time Recording | `POST /api/v1/manufacturing/executions/operations/{opId}/start` records setup and machine time tracking. | **PASS** |
| **TX-06** | Quality Policy Guard & Quarantine | Quality disposition checks prevent unauthorized release when inspection criteria fail. | **PASS** |
| **TX-07** | Lot/Serial Genealogy Traceability | Forward and reverse genealogy tree inspection displays component parentage and lot history. | **PASS** |
| **TX-08** | Authoritative Costing & Base Currency | Live costing summary renders base currency (`EGP`) and isolates operational WIP facts without mixing currencies. | **PASS** |
| **TX-09** | SaaS Commercial Entitlement Invariant | Verifies module entitlement checks; validates active tenant permissions and lifecycle isolation. | **PASS** |

### 4.4 Backend Unit & Integration Tests
- `backend/Tests/Modules/Manufacturing/ManufacturingTests.csproj`: **13/13 passed** (including `ManufacturingLifecycleAndCostingTests`).

### 4.5 Quality Gates
- **TypeScript Strict Check:** `pnpm --dir frontend run typecheck` (0 errors).
- **Frontend Architecture Lint:** `pnpm run architecture:frontend` (100% clean, boundary rules respected).
- **Localization Parity:** `pnpm run localization:verify` (clean resource parity, no missing Arabic/English keys).
- **Production Build:** `pnpm --dir frontend run build` (Clean production bundle built in 2.11s).

---

## 5. Visual Artifacts & Proof of Real-Stack Execution

All screenshots are stored in `artifacts/manufacturing-acceptance/` and persisted in the conversation artifact directory:

### Transactional Real-Stack Artifacts (`artifacts/manufacturing-acceptance/transactions-real-stack/`)
1. `tx-01-definition-created.png` — Creation and listing of Discrete Production Definition with valid routing.
2. `tx-02-order-lifecycle.png` — Multi-stage status transition (`Draft` -> `Released` -> `InProgress`) on Order Workspace.
3. `tx-03-material-conservation.png` — Material issue registration with inventory consumption tracking.
4. `tx-04-mrp-planning-run.png` — Execution of MRP regeneration run and generated planned orders.
5. `tx-05-shop-floor-queue.png` — Shop floor work center queue with live operation dispatching.
6. `tx-06-quality-policy-guard.png` — Inspection and quarantine policy enforcement preventing premature order closure.
7. `tx-07-genealogy-trace.png` — Forward and backward component lot traceability tree.
8. `tx-08-costing-summary-authoritative.png` — Costing summary card displaying authoritative base currency WIP.
9. `tx-09-entitlement-security-verified.png` — Active entitlement status badge and module access verification.

### Read-Only / Smoke Suite Artifacts (`artifacts/manufacturing-acceptance/real-stack/`)
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
- [x] SaaS commercial entitlement restored (`purchasable: true`) with active lifecycle enforcement.
- [x] Zero fabricated IDs (`00000000-0000-0000-0000-000000000001` completely purged).
- [x] Costing projection dynamically queries legal-entity base currency and prevents multi-currency aggregation.
- [x] Operational WIP balance clearly distinguished from formal General Ledger reconciliation.
- [x] Fail-closed authentication credentials enforced (zero hardcoded passwords).
- [x] Real-stack transactional suite (9/9) proves full business lifecycle against live .NET API and PostgreSQL.
- [x] Real-stack read/smoke suite (9/9) verifies live read models, filters, and mobile responsiveness.
- [x] Mocked regression suite (6/6) categorized appropriately.
- [x] 100% Arabic/English translation parity verified clean.
- [x] Concurrent work preserved untouched (`NavigationSecurityAcceptanceTests.cs` left intact).
- [x] Production build passes cleanly with 0 TypeScript errors.

**Delivery Status:** **`FRONTEND COMPLETE — READY FOR HUMAN PRODUCT/UX ACCEPTANCE`**
