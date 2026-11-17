# Module 24 — Service, Maintenance & Field Service Frontend Execution State

**Module:** Module 24 — Service, Maintenance & Field Service (`الخدمة والصيانة والعمليات الميدانية`)  
**Repository Branch:** `dev`  
**Git Remote:** `origin/dev`  
**Backend Verified Baseline:** `2ccbe91b34d4af7c5e8cb8e70c5b5ea829d61ac8`  
**Frontend Verified Commit:** `dev`  
**Governing Manuals:** Root `AGENTS.md`, `frontend/AGENTS.md`, and Skill `erp-frontend-architect`  
**Canonical Specification:** `docs/modules/24_ERP_SAAS_SERVICE_MAINTENANCE_AND_FIELD_SERVICE_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Status:** `FRONTEND COMPLETE` (Ready for Human Product Owner Acceptance)

---

## 1. Executive Summary & Release-Blocking Issues Resolved

Following independent product architecture and acceptance review, the following critical release-blocking issues were systematically corrected and verified:

1. **Dual-Track Playwright Acceptance Architecture (Real-Stack vs Mocked UI):**
   - **Real-Stack Acceptance Suite (`frontend/tests/e2e/service-real-stack.spec.ts`):** 8/8 end-to-end user journeys executing against the real local .NET backend and isolated PostgreSQL database (`todir_service_acceptance`). Zero mocked business responses, authentic sessions (`admin@atlas.com`), real mutations persisted to the database, reloaded, and verified in browser DOM.
   - **Mocked UI Suite (`frontend/tests/e2e/service-flow.spec.ts`):** Preserved for isolated component and visual rendering tests, explicitly classified as `[MOCKED_UI_ACCEPTANCE]`. All 6 visual tests passing.

2. **Strict Database Protection Before Mutation Tests:**
   - Pre-flight automated probe against `GET /api/v1/service/target` using cryptographic timing-safe token verification.
   - Fails closed immediately if the connected database is the normal dev database (`todir`). Requires an isolated test database (e.g. `todir_service_acceptance`).

3. **Durable Client Offline Command Queue:**
   - Implemented `frontend/modules/service/utils/offline-queue.ts` using durable `localStorage` (`todir_service_offline_queue_<entityId>`).
   - When offline, network mutations are halted and commands are queued with unique `clientCommandId`, timestamps, and retry counts.
   - When connection is restored or user triggers manual sync (`مزامنة الأوامر الآن`), queue auto-replays through `POST /api/v1/service/mobile/commands`.
   - Optimistic concurrency version conflicts (`service.visit_conflict`) retain the command in conflict state for technician resolution with clear Arabic microcopy.
   - Verified via unit tests (`service-slice.test.ts`) and live browser test.

4. **Authoritative Financial Multi-Currency Aggregation & Pagination:**
   - Added `GET /api/v1/service/charges/summary` to backend, grouping totals by currency across ALL database records scoped to the legal entity.
   - Financial summaries NEVER sum distinct currencies (e.g. SAR vs EGP) into a single unconverted figure.
   - `ServiceBillingTab` renders grouped currency summaries and server-side pagination with Next/Previous navigation and total record counts.

5. **Elimination of Fabricated Metrics & Synthetic Facts:**
   - Purged all hardcoded trend percentages (`+12%`, `-5%`) and synthetic availability figures.
   - Entitlement checker uses real backend resolution engine `POST /api/v1/service/entitlements/resolve`.
   - Technician My Day uses authentic `GET /api/v1/service/mobile/my-day` scoped strictly to the authenticated user.

---

## 2. Quality Gates & Local Verification Matrix

All gates pass locally without mock compromises:

| Quality Gate | Command | Result |
| :--- | :--- | :--- |
| **Backend Service Tests** | `dotnet test backend/Tests/Modules/Service/ServiceTests.csproj` | **PASSED** (5/5 tests, 0 failures, 207ms) |
| **Localization Parity** | `node frontend/tests/localization/verify-localization.mjs` | **PASSED** (100% key parity, 0 hardcoded strings) |
| **Frontend Architecture** | `pnpm run architecture:frontend` | **PASSED** (9 architectural boundary tests clean) |
| **TypeScript Strict Check** | `pnpm --dir frontend run typecheck` | **PASSED** (`tsc --noEmit`, 0 errors) |
| **Unit / Slice Tests** | `node --experimental-strip-types frontend/modules/service/tests/service-slice.test.ts` | **PASSED** (7/7 tests, 0 failures, 63ms) |
| **Real-Stack Acceptance** | `pnpm --dir frontend exec playwright test tests/e2e/service-real-stack.spec.ts` | **PASSED** (8/8 live database tests, 23.5s) |
| **Mocked UI Acceptance** | `pnpm --dir frontend exec playwright test tests/e2e/service-flow.spec.ts` | **PASSED** (6/6 component visual tests, 12.0s) |

---

## 3. Real-Stack Visual Acceptance Artifacts

All screenshots were generated from the live database execution and archived under `artifacts/service-acceptance/real-stack/`:

1. `01-real-service-overview-desktop.png`: Corporate Oxford Navy header, slate background, live database KPI cards.
2. `02-real-service-request-triaged.png`: Service request created with customer and installed base, persisted in PostgreSQL and re-queried after browser reload.
3. `03-real-work-order-lifecycle.png`: Work order created, linked to equipment, verified billing owner badge (`Service` / `None`).
4. `04-real-dispatcher-visit-assigned.png`: Dispatcher workbench rendering scheduled visits and unscheduled orders queues.
5. `05-real-technician-my-day.png`: Authenticated technician mobile-ready day view with online indicator and durable offline queue manager.
6. `07-real-entitlement-engine.png`: Authoritative entitlement resolution engine displaying category breakdown and SLA state.
7. `08-real-billing-charges.png`: Grouped multi-currency financial KPI cards and charges table with invoice request action.
8. `09-real-service-mobile-390px.png`: Touch-first responsive mobile web layout for field technicians on 390px viewport.
