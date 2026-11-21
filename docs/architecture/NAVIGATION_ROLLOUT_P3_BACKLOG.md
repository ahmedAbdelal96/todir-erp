# TODIR ERP — Navigation Architecture & Rollout Backlog (P0 / P2 / P3 Wave 1 & Wave 2)

**Status:** P0 Complete / P2 Complete / P3 Wave 1 Complete / P3 Wave 2 Complete  
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

## 2. Completed Milestones (P0 / P2 / P3 Wave 1 / P3 Wave 2)

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

### Milestone P3 Wave 2: Sales, Purchasing & POS Delivery (COMPLETED)
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

---

## 3. Explicit Hold: Subsequent Rollout Waves (Deferred Scope)

Per canonical architecture governance and explicit Product Owner instruction, the following waves remain deferred:

### Subsequent Rollout Waves (Preserved in Later Waves):
- [ ] **Wave 3**: Fixed Assets & Depreciation, Expenses, Invoicing & Billing, Payments
- [ ] **Wave 4**: HR & Employment, Payroll Runs, Attendance, Leave Management
- [ ] **Wave 5**: Manufacturing, Projects, Field Service

---

## 4. Quality Gates & Verification Evidence

| Quality Gate | Tool / Command | Status | Evidence |
|---|---|---|---|
| **TypeScript Typecheck** | `pnpm --dir frontend run typecheck` | **PASSED** | 0 errors across root and frontend tsconfig |
| **Frontend Architecture** | `pnpm run architecture:frontend` | **PASSED** | Module boundaries, fixtures, and rules 100% clean |
| **Localization Verification** | `pnpm run localization:verify` | **PASSED** | Zero missing keys, full Arabic/English parity |
| **Frontend Unit Suite** | `pnpm run test:frontend` | **PASSED** | 274/274 tests passed (57 suites) |
| **Permission Audit** | `node --test frontend/tests/navigation/permissions-manifest-audit.test.ts` | **PASSED** | 100% of permissions verified against 24 module manifests (12/12 test cases) |
| **Lifecycle Coverage Audit** | `dotnet test backend/Tests/Integration/ --filter FullyQualifiedName~ModuleLifecycleCoverageAuditTests` | **PASSED** | 6/6 tests passed (Sales, Purchasing, POS, Banking, CRM, Accounting, Inventory verified) |
| **Browser E2E UI Regression** | `pnpm --dir frontend exec playwright test tests/e2e/navigation-access.spec.ts` | **PASSED** | 9/9 browser journeys passed in Chromium (including Sales, Purchasing, POS narrow-role journeys) |
| **Frontend Production Build** | `pnpm --dir frontend build` | **PASSED** | Vite v8.3.3 built client in 1.58s with zero bundle errors |
