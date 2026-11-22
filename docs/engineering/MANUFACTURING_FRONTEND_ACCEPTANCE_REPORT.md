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
- `الكمية المتبقية للصرف` for Unissued Quantity (clearly distinguished from `عجز مخزني فعلي` for actual shortage).
- `فحص الجودة والحجر الصناعي` for Quality Inspection & Quarantine.
- `تسوية وتكاليف الإنتاج` for Manufacturing Costing & WIP Reconciliation.
- `تخطيط متطلبات المواد (MRP)` for MRP Planning Runs & Order Suggestions.
- `سلسلة الأنساب والتتبع` for Lot & Serial Genealogy.

---

## 2. Acceptance Blocker Resolutions

### 2.1 P0 — Enforce Disposable Database Safety (`todir_mfg_acceptance`)
- **Authoritative Database Identity Endpoint:** Implemented `GET /internal/acceptance/database-context` in `backend/Api/Program.cs` querying PostgreSQL `current_database()`, `current_user`, host, port, environment, and returning `isDisposable: true` strictly when database name contains `acceptance` or `test` and environment is Development/Acceptance.
- **Dedicated Acceptance Config:** Created `frontend/playwright.manufacturing.config.ts` dedicated exclusively to Manufacturing real-stack acceptance testing.
- **Fail-Closed Runtime Guard:** Implemented `frontend/tests/e2e/manufacturing-acceptance-guard.ts` with `assertDisposableDatabase()`:
  - Connects to PostgreSQL directly via Docker psql and verifies `SELECT current_database()` returns `todir_mfg_acceptance`.
  - Queries `GET /internal/acceptance/database-context` to verify the actual API connection pool is bound to the target disposable database.
  - Refuses to run and immediately aborts if connected to protected developer databases (`todir`, `todir_dev`, `postgres`).
  - Strict zero fallback credentials: throws `manufacturing_guard.credentials_missing` if `TODIR_DB_PASSWORD` or `ERP_DEV_PASSWORD` are unset.
  - Enforces `reuseExistingServer: false` and dedicated ports (5089 for API, 5179 for Vite) so background development instances cannot leak into acceptance runs.
  - Guard verified with 8/8 passing unit tests in `frontend/tests/unit/manufacturing-acceptance-guard.test.ts`.

### 2.2 P1 — Catalog Lookup at Scale: Batch Identity Resolution
- **Backend Batch Endpoint:** Implemented `POST /api/v1/catalog/resolve/batch` in `backend/Modules/Catalog/Controllers/CatalogModuleExtensions.cs`, `CatalogContracts.cs`, `CatalogDetailRepository.cs`, and `CatalogDetailService.cs`.
  - Enforces bounded batch size: `values.Count > 500` throws `catalog.batch_size_exceeded`.
  - Deduplicates variant IDs before querying.
  - Queries `foundation_catalog.catalog_search_projection` with `status = 'Active'` and tenant isolation.
  - Verified with 16/16 passing Catalog tests in `backend/Tests/Modules/Catalog/CatalogTests.csproj` including dedicated `CatalogBatchResolutionTests.cs`.
- **Frontend Batch Hook & Cache:** Rewrote `frontend/modules/manufacturing/hooks/use-catalog-products.ts`:
  - Batches all missing IDs across components and BOMs into single network calls.
  - In-memory session cache eliminates duplicate lookups across re-renders.
  - Distinguishes missing items from network errors.
  - Completely eliminated 200-item scans and raw UUID exposure across all 10 manufacturing pages.

### 2.3 P1 — Quantity Meaning & Presentation
- **Unissued vs Shortage Distinction:** In `materials-page.tsx`, renamed ambiguous shortage column to `الكمية المتبقية للصرف` (`unissuedQty = required - issued`).
- **Actual Shortage Badge:** Only displays `عجز مخزني فعلي` badge when line state is explicitly `Short` (`unissuedQty > availableStock`).
- **Standardized Formatting:** Applied `formatQuantity` with 2-4 decimal places and units of measure across all quantity values in `materials-page.tsx`, `mrp-page.tsx`, `definitions-page.tsx`, and `production-order-workspace-page.tsx`.

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
- **Complete Isolation:** Runs against dedicated PostgreSQL database `todir_mfg_acceptance` on port 15432.
- **Full Schema & Migrations:** 240 platform migrations and canonical development seed scenarios applied via `DbMigrator --seed development`.
- **Harness Ports:** API listening on port 5089, Vite dev server listening on port 5179 with API proxy.

### 4.2 Comprehensive Test Suite Results

| Test Suite | Spec File / Project | Total | Passed | Failed | Status |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **Real-Stack Transactional Suite** | `manufacturing-transactions-real-stack.spec.ts` | 9 | 9 | 0 | **PASS (100%)** |
| **Acceptance Guard Unit Suite** | `manufacturing-acceptance-guard.test.ts` | 8 | 8 | 0 | **PASS (100%)** |
| **Backend Manufacturing Tests** | `ManufacturingTests.csproj` | 14 | 14 | 0 | **PASS (100%)** |
| **Backend Catalog Tests** | `CatalogTests.csproj` | 16 | 16 | 0 | **PASS (100%)** |
| **TypeScript Strict Verification** | `pnpm --dir frontend run typecheck` | — | — | 0 | **PASS (0 errors)** |
| **Architecture Boundary Lint** | `pnpm run architecture:frontend` | 27 | 27 | 0 | **PASS (100%)** |
| **Localization Parity** | `pnpm run localization:verify` | — | — | 0 | **PASS (100%)** |
| **Production Vite Build** | `pnpm --dir frontend run build` | — | — | 0 | **PASS (1.85s)** |

### 4.3 Transactional Acceptance Scenarios Verified (`manufacturing-transactions-real-stack.spec.ts`)

| Scenario ID | Test Scenario Description | API / Domain Operation Verified | Execution Time | Result |
| :--- | :--- | :--- | :---: | :--- |
| **TX-01** | Master Definition Creation & Validation | `POST /api/v1/manufacturing/definitions` creates Discrete definition with real BOM version (201 Created); asserts negative guard (400) on invalid date interval (`validTo < validFrom`). | 11s | **PASS** |
| **TX-02** | Production Order Lifecycle Transitions | Creates order via API (Draft, v1), releases order (Released, v2), starts order (InProgress, v3); verifies row version incrementation and live UI state. | 13s | **PASS** |
| **TX-03** | Material Conservation & Reservation Guards | Asserts Material Conservation Invariant: direct consumption exceeding issued qty rejected with HTTP 400; asserts inventory reservation requirement on issuance. | 9s | **PASS** |
| **TX-04** | MRP Planning Run & Demand Pegging | Creates planning run, adds demand requirement, executes MRP calculation, verifies planned supplies, and exercises production order firming. | 14s | **PASS** |
| **TX-05** | Shop Floor Operations Queue & Progress | Navigates to Shop Floor Workbench, verifies registered work centers, scheduling horizon selector, and active operation queue visibility. | 9s | **PASS** |
| **TX-06** | Quality Policy Role Security Guard | Authenticates as restricted role (`warehouse@atlas.com`); asserts direct policy mutation returns HTTP 403 Forbidden; verifies admin UI Quality tab. | 12s | **PASS** |
| **TX-07** | Lot/Serial Genealogy Security Guard | Asserts security invariant: attempting genealogy link with forged/mismatched output IDs returns HTTP 400/404; verifies forward/reverse trace UI. | 9s | **PASS** |
| **TX-08** | Authoritative Costing & Close Rejection | Queries live costing summary verifying authoritative legal entity currency (`EGP`); attempts close mutation on incomplete order asserting 400 Bad Request rejection (`manufacturing.close_blocked`). | 11s | **PASS** |
| **TX-09** | SaaS Commercial Entitlement Invariant | Queries effective access API verifying Atlas tenant has active manufacturing entitlements; verifies unauthenticated requests return HTTP 401 Unauthorized. | 10s | **PASS** |

---

## 5. Visual Artifacts & Proof of Real-Stack Execution

All screenshots are stored in `artifacts/manufacturing-acceptance/transactions-real-stack/` and synced to the conversation artifact directory:

1. `tx-01-definition-created.png` — Creation of Production Definition with real BOM version and plant name.
2. `tx-02-order-lifecycle.png` — Status transitions (`Draft` -> `Released` -> `InProgress`) with row versioning in 360 Workspace.
3. `tx-03-material-conservation.png` — Materials management page verifying material conservation invariant.
4. `tx-04-mrp-planning-run.png` — Execution of MRP planning run with suggested supplies and netting breakdown.
5. `tx-05-shop-floor-queue.png` — Shop floor workbench with active work center operations queue.
6. `tx-06-quality-policy-guard.png` — Quality policy security guard and reassurance presentation.
7. `tx-07-genealogy-trace.png` — Forward and reverse genealogy tree with lot rejection validation.
8. `tx-08-costing-summary-authoritative.png` — Costing summary card displaying authoritative EGP operational WIP balance.
9. `tx-09-entitlement-security-verified.png` — Commercial SaaS entitlement validation across tenant states.

---

## 6. Human Product Owner Sign-off Checklist

- [x] P0: Dedicated acceptance harness `playwright.manufacturing.config.ts` enforcing `todir_mfg_acceptance` with fail-closed runtime guard.
- [x] P0: Live positive transactional journeys (TX-01 to TX-09) executed against live .NET backend and PostgreSQL; 9/9 passed in 1.9m.
- [x] P1: Catalog lookup at scale implemented via `POST /api/v1/catalog/resolve/batch` and frontend caching; zero raw UUIDs displayed.
- [x] P1: Quantity presentation corrected: unissued quantity (`الكمية المتبقية للصرف`) distinguished from shortage (`عجز مخزني فعلي`), formatted to 2-4 decimals.
- [x] All 10 workspace tabs functional, interactive, and integrated with live backend.
- [x] Costing projection dynamically queries legal-entity base currency and rejects foreign/null currencies from operational WIP.
- [x] Close readiness blocker codes translated into localized Arabic/English business advice.
- [x] Fail-closed authentication credentials enforced (zero hardcoded passwords).
- [x] 100% Arabic/English translation parity verified clean.
- [x] Concurrent work preserved untouched (`NavigationSecurityAcceptanceTests.cs` left intact).
- [x] TypeScript strict typecheck clean (0 errors).
- [x] Architecture boundary lint clean (27/27 modules).
- [x] Production build passes cleanly with 0 errors.

**Delivery Status:** **`FRONTEND COMPLETE — READY FOR HUMAN PRODUCT/UX ACCEPTANCE`**
