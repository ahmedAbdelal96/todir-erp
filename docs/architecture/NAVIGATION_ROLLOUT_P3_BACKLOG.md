# TODIR ERP — Navigation Architecture & Rollout Backlog (P0 / P2 / P3 Wave 1)

**Status:** P0 Complete / P2 Complete / P3 Wave 1 Complete  
**Governance:** `docs/architecture/TODIR_NAVIGATION_ACCESS_UX_ARCHITECTURE_DECISION.md`, `frontend/AGENTS.md`, `backend/AGENTS.md`  
**Current Release Branch:** `dev`  

---

## 1. Executive Summary & Architecture Governance

This backlog tracks the phased transition of TODIR ERP SaaS from coarse single-permission module gates into a task-oriented, permission-safe, and lifecycle-aware enterprise navigation system.

### Key Architecture Pillars (Codified & Verified):
1. **Shallow 7-Category Information Architecture**: Modules are organized into 7 functional categories (`workspace`, `sales`, `purchasing`, `inventory`, `finance`, `hr`, `admin`), avoiding 24-link flat clutter.
2. **Deterministic Route Landing**: Users with any valid sub-capability never encounter an unexpected 403 when clicking a module entry. The root redirects automatically to their first permitted task destination.
3. **Multi-Module Commercial Entitlement Isolation**: Composite permission conditions (e.g. `/accounting/periods` combining Ledger and Accounting) require both the specific permission and its manifest-owning module's entitlement/operational status to be satisfied. Unentitled modules cannot be bypassed via secondary permission codes.
4. **Lifecycle & Mutation Gate**: Every destination declares an explicit `mode` (`read` | `mutation` | `mixed`). In `ReadOnly` or `WindDown` states, mutation destinations are hidden and backend endpoints return `403 Forbidden` (`module.lifecycle_readonly`).
5. **Real-Stack Verification**: No simulated or mock-only claims. Verified end-to-end against live PostgreSQL (`todir_nav_acceptance`) and Playwright E2E browser journeys.

---

## 2. Completed Milestones (P0 / P2 / P3 Wave 1)

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
  - Zero cluster-global `ALTER ROLE` commands or schema constraint alterations in tests. Test harness checks database safety before execution (`acceptance` or `test` naming required).
  - 15/15 backend security acceptance tests in `NavigationSecurityAcceptanceTests.cs` verified using controlled Active-to-ReadOnly / Active-to-WindDown transitions.
  - Real-stack Playwright browser acceptance suite (`navigation-real-stack.spec.ts`) passing 4/4 against live ASP.NET Core API (`http://127.0.0.1:5080`) and PostgreSQL (`todir_nav_acceptance`).

---

## 3. Explicit Hold: P3 Wave 2 (Deferred Scope)

Per explicit Product Owner instruction, **P3 Wave 2 must remain untouched** during this release.
Do not proceed to P3 Wave 2 until authorized:
- [ ] Sales & Commercial Task Destinations (Wave 2)
- [ ] Purchasing & Supplier Task Destinations (Wave 2)
- [ ] Fixed Assets & Depreciation Task Destinations (Wave 2)
- [ ] HR & Payroll Task Destinations (Wave 2)

---

## 4. Quality Gates & Verification Evidence

| Quality Gate | Tool / Command | Status | Evidence |
|---|---|---|---|
| **TypeScript Typecheck** | `pnpm --dir frontend run typecheck` | **PASSED** | 0 errors across root and frontend tsconfig |
| **Frontend Architecture** | `pnpm run architecture:frontend` | **PASSED** | Module boundaries, fixtures, and rules 100% clean |
| **Localization Verification** | `pnpm run localization:verify` | **PASSED** | Zero missing keys, full Arabic/English parity |
| **Frontend Unit Suite** | `pnpm run test:frontend` | **PASSED** | 260/260 tests passed (55 suites) |
| **Permission Audit** | `node --test frontend/tests/navigation/permissions-manifest-audit.test.ts` | **PASSED** | 100% of permissions verified against 24 module manifests |
| **Lifecycle Coverage Audit** | `dotnet test backend/Tests/Integration/ --filter FullyQualifiedName~ModuleLifecycleCoverageAuditTests` | **PASSED** | 2/2 tests passed (route coverage, reviewed exceptions catalog, fail-closed missing metadata) |
| **Lifecycle Security Acceptance** | `dotnet test backend/Tests/Integration/ --filter FullyQualifiedName~NavigationSecurityAcceptanceTests` | **PASSED** | 15/15 tests passed with controlled Active -> ReadOnly transitions against live PostgreSQL (`todir_nav_acceptance`) |
| **Browser E2E UI Regression** | `pnpm --dir frontend exec playwright test tests/e2e/navigation-access.spec.ts` | **PASSED** | 6/6 mock-based browser journeys passed |
| **Browser Real-Stack Acceptance** | `pnpm --dir frontend exec playwright test tests/e2e/navigation-real-stack.spec.ts` | **PASSED** | 4/4 real-stack browser journeys passed against live ASP.NET Core API and PostgreSQL |
