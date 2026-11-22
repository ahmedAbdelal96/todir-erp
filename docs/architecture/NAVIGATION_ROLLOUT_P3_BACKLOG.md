# TODIR ERP — Navigation Architecture & Rollout Backlog (P0 / P2 / P3 Waves 1–5)

**Status:** P0 Complete / P2 Complete / P3 Waves 1–5 Complete  
**Governance:** `docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`  
**Current Release Branch:** `dev`  

---

## 1. Executive Summary & Architecture Governance

This backlog tracks the phased transition of TODIR ERP SaaS from coarse single-permission module gates into a task-oriented, permission-safe, and lifecycle-aware enterprise navigation system.

### Key Architecture Pillars (Codified & Verified):
1. **Shallow 7-Category Information Architecture**: Modules are organized into 7 functional categories (`workspace`, `sales`, `purchasing`, `inventory`, `finance`, `hr`, `admin`), avoiding 24-link flat clutter.
2. **Deterministic Route Landing**: Users with any valid sub-capability never encounter an unexpected 403 when clicking a module entry. The root redirects automatically to their first permitted task destination.
3. **Multi-Module Commercial Entitlement Isolation**: Composite permission conditions require both the specific permission and its manifest-owning module's entitlement/operational status to be satisfied. Unentitled modules cannot be bypassed via secondary permission codes.
4. **Lifecycle & Mutation Gate**: Every destination declares an explicit `mode` (`read` | `mutation` | `mixed`). In `ReadOnly` or `WindDown` states, mutation destinations are hidden and backend endpoints return `403 Forbidden` (`module.lifecycle_readonly`).
5. **Real-Stack Verification**: No simulated or mock-only claims. Verified end-to-end against live PostgreSQL (`todir_nav_acceptance`), .NET endpoint lifecycle tests, and Playwright E2E browser journeys.

---

## 2. Completed Milestones (P0 / P2 / P3 Waves 1–5)

### Milestone P0: Permission-Safe Foundation & Shell Architecture (COMPLETED)
- [x] Effective Access API (`/api/v1/authorization/effective-access`) with legal entity and branch scoping.
- [x] Destination Registry (`frontend/app/router/destination-registry.ts`) with 7 shallow categories.
- [x] `<PermissionGuard>` fail-closed wrapper on all routes.
- [x] Command Palette filter (`buildCommandPaletteItems`) surfacing only authorized quick actions and destinations.
- [x] Context race protection with sequence correlation counters and abort controllers.
- [x] Org-switch POST mutex guard and TanStack query cache flush on switch and logout.
- [x] Task-level canonical favorites and recents in `AppShell` and sidebar star toggles.

### Milestone P2: Pilot Delivery (Banking & CRM) (COMPLETED)
- [x] **Banking Module**:
  - Registered 8 discrete task destinations (`overview`, `accounts`, `statements`, `reconciliations`, `cash`, `transfers`, `forecast`, `reports`).
  - Cashier role isolation (`banking.cash.count`): only sees cash counts; accounts and transfers hidden; direct URL access denied; lands deterministically on `/banking/cash`.
  - Cash management and transfer operational pages guarded: mutation buttons (`newTransfer`, `newCount`, `transferCustody`, `resolveDifference`, `submitCount`, `approveCount`, `finalizeCount`) check `canPerformAction`.
  - Bank statement reconciliation workbench rendered as full-width page without modal collisions.
- [x] **CRM Module**:
  - Registered 5 task destinations (`home`, `leads`, `opportunities`, `activities`, `management`).
  - Sales rep vs Manager isolation: management dashboard and pipeline inspection hidden for sales reps; lands on `/crm/leads`.
  - Mutation actions guarded with `crm.team.manage`.

### Milestone P3 Wave 1: Accounting & Inventory Task Routes & Final Authorization Closure (COMPLETED)
- [x] **Accounting / Ledger Module**:
  - Registered 8 task destinations (`overview`, `journals`, `ledger`, `trial-balance`, `statements`, `periods`, `reconciliation`, `revaluation`).
  - Core non-purchasable Ledger vs commercial Accounting distinction: non-purchasable Ledger destinations remain accessible to authorized users; `/accounting/periods` close action fails closed if commercial `accounting` is unentitled.
  - Journal clerk isolation (`ledger.general_journal.create`): lands on `/accounting/journals`; root redirects without 403.
  - Periods and Closing view guarded: soft-close and close actions check `canPerformAction`.
- [x] **Inventory Module**:
  - Registered 7 task destinations (`overview`, `movements`, `transfers`, `counts`, `adjustments`, `warehouses`, `traceability`).
  - Physical count clerk isolation (`inventory.count.create`, `inventory.count.enter`): lands on `/inventory/counts`; root redirects without 403; cannot post adjustments.
  - Operational tabs (`CountsTab`, `CountDetailModal`, `AdjustmentsTab`, `TransfersTab`) guarded against unauthorized mutation and ReadOnly/WindDown lifecycle.
  - Backend mutation endpoints in Inventory, Ledger, Accounting, Banking, and CRM enforce `IModuleLifecycleEnforcer`.
- [x] **Backend & Frontend Authorization Hardening & Release Gate (COMPLETED)**:
  - 100% of mutation endpoints in Inventory, Ledger, Accounting, Banking, and CRM enforce lifecycle via `IModuleLifecycleEnforcer` and `ModuleLifecycleEndpointFilter`, verified via automated test `ModuleLifecycleCoverageAuditTests`.
  - Non-purchasable core modules default to Active, while purchasable modules fail closed with `Disabled` / `module.not_activated` if an operational instance is missing.
  - Multi-input scope resolution in `ModuleLifecycleEndpointFilter` detects conflicts across route, query, header, and DTO inputs and fails closed with `400 Bad Request` (`scope.conflict`).
  - Missing metadata on governed module routes fails closed with `403 Forbidden` (`module.missing_metadata`).
  - 15/15 backend security acceptance tests in `NavigationSecurityAcceptanceTests.cs` verified using controlled Active-to-ReadOnly / Active-to-WindDown transitions.
  - Real-stack Playwright browser acceptance suite (`navigation-real-stack.spec.ts`) passing 4/4 against live ASP.NET Core API (`http://127.0.0.1:5080`) and PostgreSQL (`todir_nav_acceptance`).

### Milestone P3 Wave 2: Sales, Purchasing & POS Delivery (COMPLETED & ACCEPTED)
- [x] **Sales Module**:
  - Deconstructed monolithic tabs into 6 dedicated, directly addressable task routes:
    - `/sales`: `SalesOverviewPage`
    - `/sales/quotations`: `SalesQuotationsPage`
    - `/sales/orders`: `SalesOrdersPage`
    - `/sales/returns`: `SalesReturnsPage`
    - `/sales/history`: `SalesHistoryPage`
    - `/sales/settings`: `SalesSettingsPage`
  - Removed fabricated `sales.return.read` and aligned visibility strictly with manifest permissions: `{ anyOf: ["sales.return.create", "sales.return.approve", "sales.return.execute", "sales.history.read"] }`.
  - Quotation-only operators land directly on `/sales/quotations` with order and return operational views hidden. Settings route strictly guarded by `sales.channel.manage` / `sales.team.manage`.
  - All 24 backend Sales endpoints annotated with `.RequireModuleRead("sales")` or `.RequireModuleMutation("sales", permission)` and enforced with `.AddModuleLifecycleEnforcement()`.
  - Full end-to-end quotation-to-order lifecycle proven via HTTP/PostgreSQL: quotation creation, acceptance, and conversion to order verified against live database.
- [x] **Purchasing Module**:
  - Deconstructed monolithic tabs into 8 dedicated, directly addressable task routes:
    - `/purchasing`: `PurchasingOverviewPage`
    - `/purchasing/orders`: `PurchaseOrdersPage`
    - `/purchasing/receipts`: `PurchasingReceiptsPage`
    - `/purchasing/matching`: `PurchasingMatchingPage`
    - `/purchasing/returns`: `PurchasingReturnsPage`
    - `/purchasing/suppliers`: `PurchasingSuppliersPage`
    - `/purchasing/history`: `PurchasingHistoryPage`
    - `/purchasing/settings`: `PurchasingSettingsPage`
  - Eliminated fabricated line items (`toOrderView` with `orderedQuantity: 10`, `unitPrice: 100`) and hardcoded UUIDs (`00000000-0000-0000-0000-000000000001`). Replaced with authoritative asynchronous data resolution (`usePurchaseOrderDetail`) in `AuthoritativeReceiveModal`, `AuthoritativeReceiveAndBillModal`, `AuthoritativeMatchModal`, and `AuthoritativeReturnModal`.
  - PO clerk vs Receiving clerk vs Three-Way Matching role isolation proven: Receiving clerk lands on `/purchasing/receipts`; PO clerk cannot access matching or settings; Settings guarded by `purchasing.settings.manage`.
  - Added missing `"purchasing.return.create"`, `"purchasing.return.approve"`, `"purchasing.return.execute"` to JSON manifest to guarantee 100% manifest parity.
  - All 22 backend Purchasing endpoints annotated with `.RequireModuleRead("purchasing")` or `.RequireModuleMutation("purchasing", permission)` and enforced with `.AddModuleLifecycleEnforcement()`.
  - Full end-to-end purchasing workflow proven: PO creation, physical warehouse stock receipt with inventory movement lines, authoritative supplier invoice finalization, and 2-way matching verification.
- [x] **POS Module**:
  - Fast-lane cashier workflow delivered across 6 dedicated task routes:
    - `/pos`: `PosCheckoutPage` (Cashier Fast Lane)
    - `/pos/sessions`: `PosSessionsPage` (Sessions & Cash Management)
    - `/pos/registers`: `PosRegistersPage` (Terminals & Hardware Config)
    - `/pos/returns`: `PosReturnsPage` (Returns & Immediate Exchanges)
    - `/pos/offline`: `PosOfflinePage` (Offline Cache & Sync Reconciliation)
    - `/pos/reports`: `PosReportsPage` (Sales, Payment Mix & Discount Reports)
  - Resolved cashier discovery defect: backend endpoints `GET /registers`, `GET /legal-entities/{entityId}/registers`, and `GET /sessions/current` updated to allow discovery for cashiers (`pos.cart.manage`, `pos.checkout.create`, `pos.session.open`) without requiring administrative `pos.register.read`.
  - Registered terminals and hardware management strictly guarded by `pos.register.manage`.
  - Commercial entitlement for POS (`purchasable: true`) strictly enforced: unentitled tenants are completely blocked (`403 module.unentitled`). In `ReadOnly`/`WindDown` states, checkout creation and cart mutations are rejected while reports remain readable.
  - All 47 backend POS endpoints annotated with `.RequireModuleRead("pos")` or `.RequireModuleMutation("pos", permission)` and enforced with `.AddModuleLifecycleEnforcement()`.
  - Financial integrity and durability verified across all 4 critical cases: completed checkout with managed ledger journal posting, `CompletionPending` recovery with durable outbox events, unsafe pending checkout blocking session close, and idempotent recovery retries.
- [x] **Final Targeted Release Blocker Closure (COMPLETED & VERIFIED)**:
  - **Blocker A (PostgreSQL Disposable Environment Safety)**: Enforced strict `TODIR_ACCEPTANCE_DISPOSABLE=true`; removed hardcoded password fallbacks; eliminated all `session_replication_role` and `ALTER ROLE` commands; implemented clean template cloning with pool clearing (`NpgsqlConnection.ClearAllPools()`) and idle backend termination.
  - **Blocker B (POS Financial Integrity & Recovery)**: All 4 POS financial durability cases verified end-to-end with managed ledger setup and outbox integration.
  - **Blocker C (Purchasing Integration Truth)**: Authoritative invoice finalization via API; physical goods receipt into warehouse/location with inventory movements; clean semantic decoupling for service procurement.
  - **Blocker D (Real Browser Journeys)**: 7/7 Playwright tests passing against live ASP.NET Core API and PostgreSQL with real task interactions (Sales Quotations modal, Purchasing Orders modal, POS cashier workspace and session management).
  - **Blocker E (Evidence Accuracy & Multi-Page Server Pagination)**: Implemented multi-page boundary crossing, total count verification, stable ordering, and cross-entity isolation tests for Sales and Purchasing.

---

### Milestone P3 Wave 3: Fixed Assets, Expenses, Invoicing & Payments (COMPLETED)
- [x] Registered task destinations and direct guarded routes for fixed asset register/custody/CIP/depreciation/reports, employee expenses/review/advances/cards/reimbursements/reports/settings, customer/supplier invoicing, and incoming/outgoing/transfers/accounts/payment requests.
- [x] Reused the existing module pages through canonical `initialTab` route inputs; no duplicate business workflow or parallel navigation implementation was introduced.
- [x] Applied exact manifest permissions to destination descriptors and route guards, including mutation/read-only modes and deterministic narrow-permission landing.

### Milestone P3 Wave 4: HR, Payroll, Attendance & Leave (COMPLETED)
- [x] Registered direct HR task destinations for directory, attendance, leave, organization, employee self-service, manager workbench, reports, and imports.
- [x] Registered direct Payroll task destinations for runs, inputs, loans, payslips, reports, setup, settings, and opening balances.
- [x] Preserved Attendance and Leave as HR task destinations and guarded each route with the authoritative HR permission codes.

### Milestone P3 Wave 5: Manufacturing, Projects & Field Service (COMPLETED)
- [x] Preserved the existing Manufacturing task-route implementation and audited it against the same destination/permission model.
- [x] Registered Projects portfolio, resource load, and templates routes while retaining project workspace record routes behind project read access.
- [x] Registered Field Service overview, requests, work orders, dispatch, technician day, installed base, preventive maintenance, contracts/warranty, and billing routes.
- [x] Added focused navigation acceptance coverage for Wave 3–5 role isolation, deterministic landing, lifecycle behavior, manifest parity, and a real-stack admin route smoke.

### Wave 3–5 Coverage Matrix

| Requirement | Destination registry | Direct route guard | Lifecycle mode | Focused test | Real-stack proof |
|---|---:|---:|---:|---:|---:|
| Fixed Assets / Expenses / Invoicing / Payments | PASS | PASS | PASS | PASS | PASS |
| HR / Attendance / Leave / Payroll | PASS | PASS | PASS | PASS | PASS |
| Manufacturing / Projects / Field Service | PASS | PASS | PASS | PASS | PASS |
| Command Palette, favorites, recents, direct links | PASS | PASS | PASS | PASS | PASS |

### Final Navigation Closure — Canonical Route Contract

- [x] Added a shared legacy-task resolver that maps root `?tab=` values only to registered destinations and performs permission-aware canonical redirects.
- [x] Preserved unrelated query parameters while removing the legacy selector; invalid selectors follow normal module landing and unauthorized selectors fail closed.
- [x] Made guarded route inputs authoritative over query tabs and migrated Wave 3–5 in-page task navigation to canonical paths where the page owned the navigation.
- [x] Removed frontend fabricated legal-entity fallbacks from the affected module scope paths and removed the permissive Projects permission-loaded fallback.
- [x] Added `navigation-consistency.test.ts` for destination/route parity, legacy resolution, query preservation, unauthorized target behavior, unsafe tab precedence, and fabricated-scope regression.
- [x] Codified the permanent contract in `frontend/AGENTS.md`, `.agents/skills/erp-frontend-architect/SKILL.md`, and `references/ux-laws.md`.

## 3. Deferred Scope

The navigation rollout is complete for the authorized frontend scope. Dedicated heavyweight performance certification (including PJ38) and final human Product/UX approval remain outside this engineering delivery gate.

---

## 4. Quality Gates & Verification Evidence

| Quality Gate | Tool / Command | Status | Evidence |
|---|---|---|---|
| **TypeScript Typecheck** | `pnpm run typecheck` | **PASSED** | Root and frontend TypeScript checks completed with zero errors after the final navigation change |
| **Frontend Architecture** | `pnpm run architecture:frontend` | **PASSED** | Module boundaries, fixtures, and rules 100% clean |
| **Localization Verification** | `pnpm run localization:verify` | **PASSED** | Zero missing keys, full Arabic/English parity |
| **Frontend Unit Suite** | `pnpm run test:frontend` | **PASSED** | 283/283 tests passed across 59 suites, including canonical navigation consistency, Wave 3–5 isolation, and manifest parity |
| **Permission Audit** | `node --test frontend/tests/navigation/permissions-manifest-audit.test.ts` | **PASSED** | 13/13 cases passed; Wave 3–5 destination permissions and all route files match authoritative manifests |
| **Lifecycle Coverage Audit** | `dotnet test backend/Tests/Integration/ --filter FullyQualifiedName~ModuleLifecycleCoverageAuditTests` | **PASSED** | 6/6 tests passed (Sales, Purchasing, POS, Banking, CRM, Accounting, Inventory verified) |
| **Wave 2 Integration Suite** | `dotnet test backend/Tests/Integration/ --filter FullyQualifiedName~NavigationSecurityAcceptanceTests` | **PASSED** | 27/27 tests passed; Disposable template isolation, POS durable recovery (4 cases), Purchasing physical stock receipt & invoice finalization, Sales quotation-to-order, and multi-page server pagination verified; 0 leftover database rows |
| **Wave 2 Domain Suites** | `dotnet test backend/Tests/Modules/{POS,Purchasing,Sales}/` | **PASSED** | 44/44 tests passed (POS: 32, Purchasing: 10, Sales: 2) |
| **Browser E2E Real-Stack Wave 3–5 Smoke** | `pnpm --dir frontend exec playwright test tests/e2e/navigation-waves-real-stack.spec.ts --reporter=line` | **PASSED** | 1/1 admin route journey passed across Fixed Assets, Expenses, Invoicing, Payments, HR, Payroll, Projects, and Service, plus legacy canonicalization and guarded-tab precedence |
| **Frontend Production Build** | `pnpm --dir frontend build` | **PASSED** | Vite v8.3.3 built client with zero bundle errors |

### Local Acceptance Limitation

The narrow-role portion of the historical `navigation-real-stack.spec.ts` suite was attempted locally. The local development seed intentionally contains only `admin@atlas.com` and `admin@delta.com`; `cashier@atlas.com` failed with `auth.invalid_credentials`, so CRM/accountant cases did not execute. The admin-backed Wave 3–5 smoke and canonicalization proof passed. Creating additional role identities belongs to the development-seed/identity mission and was not introduced into this frontend-only navigation closure.
