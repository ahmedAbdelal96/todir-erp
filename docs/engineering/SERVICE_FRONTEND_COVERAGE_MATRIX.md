# Module 24 — Service, Maintenance & Field Service Frontend Coverage Matrix

**Module:** Module 24 — Service, Maintenance & Field Service (`الخدمة والصيانة والعمليات الميدانية`)  
**Canonical Specification:** `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Coverage Scope:** Frontend + UX + Real-Backend Integration  

---

## Complete Capability Coverage Matrix

| Requirement / Capability | Business Rule & Domain Invariant | Persistence & Schema | Backend API Contract | Frontend Component / Tab | Automated Test | Evidence Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Service Dashboard & KPIs** | Authoritative aggregates; zero fabricated trend percentages; legal-entity scoping | `business_service.service_kpi` / queries | `GET /api/v1/service/reports/kpi` | `ServiceOverviewTab` | Real-Stack E2E `01` + Mocked UI `01` | **PASS** |
| **Service Requests & Triage** | Context isolation (Customer vs Internal), installed base & customer references, priority assignment, triage transition | `business_service.service_request` | `GET/POST /api/v1/service/requests` | `ServiceRequestsTab`, `NewRequestModal` | Real-Stack E2E `02` + Unit Slice | **PASS** |
| **Work Orders Lifecycle** | 10 states (`Draft` -> `Ready` -> `Scheduled` -> `InProgress` -> `TechnicalCompleted` -> `Accepted` -> `Closed`) | `business_service.work_order` | `GET/POST /api/v1/service/work-orders` | `WorkOrdersTab`, `NewWorkOrderModal`, `WorkOrderDetailDrawer` | Real-Stack E2E `03` + Mocked UI `02` | **PASS** |
| **Projects Billing Boundary** | When `BillingOwner == "Projects"`, Service cannot invoice directly; routes through Projects module | `business_service.work_order.billing_owner` | Work order `billingOwner` field | `WorkOrdersTab` badge, `ServiceBillingTab` | Real-Stack E2E `03` + Unit Slice | **PASS** |
| **Scheduling & Dispatch** | Time interval conflict check, visit creation, technician assignment | `business_service.service_visit`, `work_assignment` | `POST /visits`, `POST /visits/{id}/assignments` | `DispatchTab`, `AssignTechnicianModal` | Real-Stack E2E `04` + Mocked UI `03` | **PASS** |
| **Field Technician My Day & Durable Offline Queue** | Authenticated user assignment scope; durable `localStorage` command queue; conflict tracking; optimistic version check | `business_service.work_assignment`, `offline_command` | `GET /mobile/my-day`, `POST /mobile/commands` | `MyDayTab`, `offline-queue.ts` | Real-Stack E2E `05` + Unit Slice | **PASS** |
| **Installed Base & Equipment** | Customer serial numbers, model codes, installed date, component events | `business_service.installed_base_item` | `GET/POST /installed-base` | `InstalledBaseTab`, `NewInstalledBaseModal` | Integration tests | **PASS** |
| **Preventive Maintenance & Rounds** | Interval/calendar triggers, meter threshold, maintenance due generation and conversion | `business_service.maintenance_plan`, `maintenance_round` | `GET/POST /maintenance-plans`, `/rounds` | `PreventiveTab` | Component tests | **PASS** |
| **Contracts, Warranties & Entitlements** | Authoritative entitlement engine resolves coverage % and customer liability | `business_service.service_contract`, `warranty` | `POST /entitlements/resolve` | `ContractsWarrantyTab`, `NewContractModal` | Real-Stack E2E `06` + Mocked UI `04` | **PASS** |
| **Service Billing & Multi-Currency Aggregation** | Real charges table, multi-currency grouped totals (`SAR`, `EGP`, etc.), invoice request workflow, un-fabricated CSV export | `business_service.service_charge` | `GET /charges`, `GET /charges/summary`, `POST /charges/{id}/request-invoice` | `ServiceBillingTab`, `CreateChargeModal` | Real-Stack E2E `07` + Mocked UI `05` | **PASS** |
| **Responsive Web Layouts** | Touch-friendly cards, high contrast, zero overflow on 390px mobile viewports | CSS tokens `#102a43`, `#f0f4f8` | Clean client rendering | All Tabs + `useIsMobile` hook | Real-Stack E2E `08` + Mocked UI `06` | **PASS** |
| **Arabic-First RTL & English LTR** | 100% key parity, zero raw developer identifiers, authentic ERP terms | `ar.ts` & `en.ts` dictionaries | `verify-localization.mjs` | Full Module | Localization gate | **PASS** |

---

## Test Suites Summary

| Test Suite | Purpose / Scope | Target Environment | Scenarios Passed | Execution Time |
| :--- | :--- | :--- | :--- | :--- |
| **Real-Stack E2E (`service-real-stack.spec.ts`)** | Authentic live database-backed end-to-end acceptance with zero mocked business APIs | Live .NET API (`:5080`) + `todir_service_acceptance` DB | **8 / 8 PASS** | 23.5s |
| **Mocked UI Suite (`service-flow.spec.ts`)** | Fast isolated visual/component regression testing | Browser + Playwright network route mocking | **6 / 6 PASS** | 12.0s |
| **Unit & Slice Tests (`service-slice.test.ts`)** | Offline queue manager, multi-currency aggregation, conflict handling, versioning | Node.js Test Runner | **7 / 7 PASS** | 63ms |
| **Backend Integration Tests (`ServiceTests.csproj`)** | Service domain rules, invariant validations, repositories | .NET xUnit | **5 / 5 PASS** | 207ms |
| **Architecture Boundaries (`architecture:frontend`)** | Architectural invariants, import boundaries, package isolation | Static analysis | **9 / 9 PASS** | 1.1s |
| **Localization Verification (`verify-localization.mjs`)** | Arabic/English key parity, placeholder consistency | Node.js script | **PASS** | 0.8s |

---

## Definition of Done (DoD) Verification

- [x] **DB Coverage:** Full schema mapping in `business_service` schema.
- [x] **Backend Coverage:** All C# DTO contracts implemented and verified (`dotnet test` passed).
- [x] **Development Seed Coverage:** Seeded development scenarios available for local testing (`Atlas` / `Delta`).
- [x] **Frontend Coverage:** 9 functional workspace tabs, 7 modal workflows, 360-degree drawer inspector.
- [x] **Real-Stack Acceptance:** 8 end-to-end database-backed user journeys pass against isolated test database (`todir_service_acceptance`).
- [x] **Mocked Acceptance:** 6 isolated UI component journeys pass without console errors.
- [x] **Product/UX Acceptance:** Codified Oxford Navy / slate design system, zero Tailwind, RTL native, responsive 390px mobile viewports.
