# Module 24 — Service, Maintenance & Field Service Frontend Coverage Matrix

**Module:** Module 24 — Service, Maintenance & Field Service (`الخدمة والصيانة والعمليات الميدانية`)  
**Canonical Specification:** `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_AND_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Coverage Scope:** Frontend + UX + Real-Backend Integration  

---

## Complete Capability Coverage Matrix

| Requirement / Capability | Business Rule & Domain Invariant | Persistence & Schema | Backend API Contract | Frontend Component / Tab | Automated Test | Evidence Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Service Dashboard & KPIs** | Authoritative aggregates; zero fabricated trend percentages | `business_service.service_kpi` / queries | `GET /api/v1/service/reports/kpi` | `ServiceOverviewTab` | E2E `01. Service Overview` | **PASS** |
| **Service Requests & Triage** | Context isolation (Customer vs Internal), priority assignment, triage transition | `business_service.service_request` | `GET/POST /api/v1/service/requests` | `ServiceRequestsTab`, `NewRequestModal` | Unit + E2E suite | **PASS** |
| **Work Orders Lifecycle** | 10 states (`Draft` -> `Ready` -> `Scheduled` -> `InProgress` -> `TechnicalCompleted` -> `Accepted` -> `Closed`) | `business_service.work_order` | `GET/POST /api/v1/service/work-orders` | `WorkOrdersTab`, `NewWorkOrderModal`, `WorkOrderDetailDrawer` | E2E `02. Work Orders` | **PASS** |
| **Projects Billing Boundary** | When `BillingOwner == "Projects"`, Service cannot invoice directly; routes through Projects module | `business_service.work_order.billing_owner` | Work order `billingOwner` field | `WorkOrdersTab` badge, `ServiceBillingTab` | Unit + E2E `02. Work Orders` | **PASS** |
| **Scheduling & Dispatch** | Time interval conflict check, visit creation, technician assignment | `business_service.service_visit`, `work_assignment` | `POST /visits`, `POST /visits/{id}/assignments` | `DispatchTab`, `AssignTechnicianModal` | Unit + E2E `03. My Day` | **PASS** |
| **Field Technician My Day** | Authenticated user assignment scope; offline command queueing; optimistic version check | `business_service.work_assignment`, `offline_command` | `GET /mobile/my-day`, `POST /mobile/commands` | `MyDayTab` | E2E `03. My Day` | **PASS** |
| **Installed Base & Equipment** | Customer serial numbers, model codes, installed date, component events | `business_service.installed_base_item` | `GET/POST /installed-base` | `InstalledBaseTab`, `NewInstalledBaseModal` | Integration tests | **PASS** |
| **Preventive Maintenance & Rounds** | Interval/calendar triggers, meter threshold, maintenance due generation and conversion | `business_service.maintenance_plan`, `maintenance_round` | `GET/POST /maintenance-plans`, `/rounds` | `PreventiveTab` | Component tests | **PASS** |
| **Contracts, Warranties & Entitlements** | Authoritative entitlement engine resolves coverage % and customer liability | `business_service.service_contract`, `warranty` | `POST /entitlements/resolve` | `ContractsWarrantyTab`, `NewContractModal` | Unit + E2E `04. Entitlements` | **PASS** |
| **Service Billing & Invoicing** | Real charges table, invoice request workflow, un-fabricated CSV export | `business_service.service_charge` | `GET /charges`, `POST /charges/{id}/request-invoice` | `ServiceBillingTab`, `CreateChargeModal` | Unit + E2E `05. Billing` | **PASS** |
| **Responsive Web Layouts** | Touch-friendly cards, high contrast, zero overflow on 390px mobile viewports | CSS tokens `#102a43`, `#f0f4f8` | Clean client rendering | All Tabs + `useIsMobile` hook | E2E `06. Mobile Layout` | **PASS** |
| **Arabic-First RTL & English LTR** | 100% key parity, zero raw developer identifiers, authentic ERP terms | `ar.ts` & `en.ts` dictionaries | `verify-localization.mjs` | Full Module | Localization gate | **PASS** |

---

## Definition of Done (DoD) Verification

- [x] **DB Coverage:** Full schema mapping in `business_service` schema.
- [x] **Backend Coverage:** All C# DTO contracts implemented and verified (`dotnet test` passed).
- [x] **Development Seed Coverage:** Seeded development scenarios available for local testing.
- [x] **Frontend Coverage:** 9 functional workspace tabs, 7 modal workflows, 360-degree drawer inspector.
- [x] **Acceptance Scenarios:** End-to-end user journeys pass in Playwright without console errors.
- [x] **Product/UX Acceptance:** Codified Oxford Navy / slate design system, zero Tailwind, RTL native.
