# Module 27 Quality Management — Frontend Coverage Matrix

**Module:** Module 27 — Quality Management (`إدارة الجودة الشاملة`)  
**Canonical Specification:** `docs/modules/27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Gate:** `ENGINEERING VERIFIED — READY FOR PRODUCT OWNER SIGN-OFF`  

---

## 1. Traceability & Acceptance Matrix

| Journey ID | Product Requirement | User Role | Canonical Route | Backend Contract | Primary Permission | 6 UI States | Automated Test | Real-Stack Evidence | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: |
| **QF-01** | Quality Overview & KPI Priority Ribbon | Quality Lead / Operations | `/quality` | `GET /api/v1/quality/inspections`, `/restrictions`, `/ncrs` | `quality.read` | Yes | Unit + E2E | `01-quality-overview.png` | **PASS** |
| **QF-02** | Quality Plans & Specifications Register | Quality Engineer | `/quality/plans` | `GET/POST /api/v1/quality/plans`, `/versions`, `/activate` | `quality.plan.manage` | Yes | Unit + E2E | `02-quality-plans-register.png` | **PASS** |
| **QF-03** | Characteristics Catalog & Test Limits | Quality Engineer | `/quality/plans` (Tab: Characteristics) | `GET/POST /api/v1/quality/characteristics` | `quality.characteristic.manage` | Yes | Unit + E2E | `03-quality-characteristics-catalog.png` | **PASS** |
| **QF-04** | Sampling Plans & AQL Statistical Methods | Quality Engineer | `/quality/plans` (Tab: Sampling) | `GET/POST /api/v1/quality/sampling-plans` | `quality.plan.manage` | Yes | Unit + E2E | `04-quality-sampling-plans.png` | **PASS** |
| **QF-05** | Inspections Queue & Operational Filtering | Inspector / Supervisor | `/quality/inspections` | `GET/POST /api/v1/quality/inspections` | `quality.inspection.create` | Yes | Unit + E2E | `05-quality-inspections-queue.png` | **PASS** |
| **QF-06** | Inspection Deep Workbench & Result Entry | Quality Inspector | `/quality/inspections/:id` | `GET /api/v1/quality/inspections/{id}`, `POST /results` | `quality.inspection.execute` | Yes | Unit + E2E | `06-inspection-workbench-active.png` | **PASS** |
| **QF-07** | Inspection Conformance & Completion | Quality Inspector | `/quality/inspections/:id` | `POST /api/v1/quality/inspections/{id}/complete` | `quality.inspection.execute` | Yes | Unit + E2E | `07-inspection-evaluation-completed.png` | **PASS** |
| **QF-08** | Strict Domain Invariant: Passed != Released | All Quality Users | `/quality/*` | Invariant Banner & Explicit Hold Release required | Domain Invariant | Yes | Unit + E2E | `08-inspection-domain-invariant-warning.png` | **PASS** |
| **QF-09** | Reinspection Workflow & Cycle Tracing | Quality Inspector | `/quality/inspections/:id` | `POST /api/v1/quality/inspections/{id}/reinspect` | `quality.inspection.review` | Yes | Unit + E2E | `09-reinspection-workflow.png` | **PASS** |
| **QF-10** | Material Holds & Restrictions Register | Quality / Warehouse | `/quality/restrictions` | `GET/POST /api/v1/quality/restrictions` | `quality.hold.manage` | Yes | Unit + E2E | `10-material-holds-restrictions.png` | **PASS** |
| **QF-11** | Partial Quantity Release & Balance Calculation | Quality / Warehouse | `/quality/restrictions` | `POST /api/v1/quality/restrictions/{id}/release` | `quality.hold.manage` | Yes | Unit + E2E | `11-partial-quantity-release-dialog.png` | **PASS** |
| **QF-12** | Dispositions Queue & MRB Technical Decisions | MRB / Quality Manager | `/quality/dispositions` | `GET/POST /api/v1/quality/dispositions`, `/approve`, `/execute` | `quality.disposition.manage` | Yes | Unit + E2E | `12-quality-dispositions-mrb.png` | **PASS** |
| **QF-13** | Quality Issues Log & Defect Classification | Quality Supervisor | `/quality/ncrs` (Tab: Issues) | `GET/POST /api/v1/quality/issues` | `quality.issue.manage` | Yes | Unit + E2E | `13-quality-issues-log.png` | **PASS** |
| **QF-14** | Non-Conformance Reports (NCR) & Containment | Quality Supervisor | `/quality/ncrs` (Tab: NCRs) | `GET/POST /api/v1/quality/ncrs`, `/containment` | `quality.ncr.manage` | Yes | Unit + E2E | `14-ncrs-containment-actions.png` | **PASS** |
| **QF-15** | Corrective & Preventive Action (CAPA) with 6M | Quality Manager | `/quality/capas` | `GET/POST /api/v1/quality/capas`, `/actions`, `/root-cause` | `quality.capa.manage` | Yes | Unit + E2E | `15-capa-root-cause-investigation.png` | **PASS** |
| **QF-16** | Supplier Quality, SCAR Queue, and COA/COC | Supplier Quality Lead | `/quality/suppliers`, `/quality/certificates` | `PUT /supplier-profiles`, `POST /scars`, `POST /certificates` | `quality.supplier.manage`, `quality.certificate.manage` | Yes | Unit + E2E | `16-supplier-quality-scar-certificates.png` | **PASS** |
| **QF-17** | Multi-Viewport & Arabic/English Responsive | All Quality Users | `/quality` | RTL Arabic & LTR English layout, tablet (768px), mobile (375px) | Platform Core | Yes | E2E | `17-multi-viewport-rtl-ltr.png` | **PASS** |
| **QF-18** | Granular Authorization & Read-Only Protection | Auditor / Restricted (`sales@atlas.com`) | `/quality/*` | `useQualityAccess` granular capability checks & lifecycle locks, 403 API rejection | `quality.read` + Granular | Yes | E2E (Real Live API) | `18-permission-lifecycle-protection.png` | **PASS** |
| **QF-19** | Keyset Pagination & Cursor Navigation | All Quality Users | `/quality/inspections` | Keyset pagination bar & server cursor tracking (132+ real records) | `quality.read` | Yes | E2E (Real DB Keyset Cursors) | Playwright Spec 19 (Zero duplicates, exact restore) | **PASS** |
| **QF-20** | Fail-Closed Security & Access Invariant Verification | System / Security | `/api/v1/quality/*` | Multi-tenant & same-tenant legal entity session isolation (MAIN vs SECONDARY `d8eb39bb-b048-7c5d-bacd-e3a923eee21c`), 401 anonymous, 403 unauthorized (`sales@atlas.com`), cross-tenant rejection (`admin@delta.com`), zero DB leakage | Security Core | Yes | E2E (Live API & DB Verification) | Playwright Spec 20 | **PASS** |

---

## 2. Real-Stack Evidence Index

All 18 visual artifacts have been generated through real-stack Playwright execution against the live PostgreSQL-backed TODIR ERP backend:

1. `01-quality-overview.png`: High-contrast Oxford Navy shell, 3-headline KPI ribbon (`#0284c7`, `#d97706`, `#dc2626`), scoped labels ("في طابور العمل المباشر"), and quick priority queues.
2. `02-quality-plans-register.png`: Quality Plans register, active version status badges, and plan creation modal trigger.
3. `03-quality-characteristics-catalog.png`: Characteristic specifications catalog with quantitative target limits and qualitative standards.
4. `04-quality-sampling-plans.png`: Sampling plans register displaying AQL sampling tables, normal/tightened inspection levels, sample sizes, and localized sampling methods.
5. `05-quality-inspections-queue.png`: Operational inspection work queue filtered by status (`Pending`, `InProgress`, `Passed`, `Failed`) and event type.
6. `06-inspection-workbench-active.png`: Deep inspection workbench (`/quality/inspections/:id`) with characteristic-by-characteristic entry and frozen plan snapshot.
7. `07-inspection-evaluation-completed.png`: Conformance evaluation with pass/fail decision recording and inspector assignment.
8. `08-inspection-domain-invariant-warning.png`: Prominent system warning banner: *"Passed != Released"* (نجاح الفحص لا يعني الإفراج التلقائي عن المخزون).
9. `09-reinspection-workflow.png`: Reinspection trigger and linked parent-inspection cycle history.
10. `10-material-holds-restrictions.png`: Active hold restrictions register with initial, released, and remaining quantity accounting, supporting physical inventory holds and administrative restrictions.
11. `11-partial-quantity-release-dialog.png`: Partial release modal enforcing remaining balance validation and operational release logging, with strict before/after inventory balance delta checks (asserting exact 25-unit partial release from `QualityHold` into `Available`: `-25 QualityHold` and `+25 Available` at exact warehouse and location).
12. `12-quality-dispositions-mrb.png`: Material Review Board (MRB) dispositions queue with source hold banner and technical decision tracking (`UseAsIs`, `Rework`, `Scrap`, `SupplierReturn`), column header for inventory execution effect, and exact -75 / +75 inventory balance delta assertions.
13. `13-quality-issues-log.png`: Quality issues log categorized by source, severity, and defect classification with localized severity levels (`Minor`, `Major`, `Critical`).
14. `14-ncrs-containment-actions.png`: Non-Conformance Report (NCR) lifecycle with immediate containment action tracking and localized states (`Contained`, `UnderInvestigation`).
15. `15-capa-root-cause-investigation.png`: CAPA workflow with Ishikawa 6M root cause logging and action tracking with localized lifecycle states (`Approved`, `Executed`, `Closed`).
16. `16-supplier-quality-scar-certificates.png`: Supplier quality profiles, SCAR dispute workflows, and Certificate of Analysis (COA/COC) issuance register.
17. `17-multi-viewport-rtl-ltr.png`: Responsive verification captured at tablet viewport (768x1024), demonstrating complete RTL alignment and zero horizontal overflow.
18. `18-permission-lifecycle-protection.png`: Visual evidence captured under restricted user (`sales@atlas.com`), demonstrating disabled creation/mutation controls and fail-closed permission enforcement.

---

## 3. Transactional Integrity & Acceptance Protection

1. **Exact Stock Movement Conservation & Catalog UOM Conversion**: In `ReconcileRestrictionAsync`, validates authoritative movement identity, operation type, status, tenant, legal entity, product, warehouse, location, lot/serial, unit conversion, movement direction, source/target status, and exact base-unit quantities using catalog UOM conversion factor (`foundation_catalog.product_uom.factor_to_base`). Signed entries must balance to zero and match original restriction base quantity.
2. **Atomic & Exactly-Once Quality Reconciliation**: In `QualityRepository.ReconcileRestrictionAsync`, confirmation execution is concurrency-safe and atomic: uses conditional SQL update (`owner_execution_status != 'Confirmed'`) and verifies outbox message existence (`platform.outbox_message`) before emitting `QualityRestrictionCreatedV1`, guaranteeing zero duplicate outbox events and eliminating concurrent confirmation race conditions.
3. **Scoped Fault Injection**: Quality reconciliation failure simulation uses a thread/task-scoped `AsyncLocal<Action<Guid, Guid>?>` mechanism (`QualityFaultInjection`) replacing public mutable static properties, preventing race conditions or state pollution across test runs. Tested in `QualityBackendAcceptanceTests.QA-20`.
4. **Server-Side Inspection Keyset Pagination & Search in Restrictions**: In `hold-disposition-modals.tsx`, inspection selection uses debounced server-side search (`search` param in `GET /api/v1/quality/inspections`) and keyset cursor pagination (`useKeysetPagination`), guaranteeing operators can find any inspection across large datasets while preserving currently selected inspection context across query switches.
5. **Fail-Closed Physical Stock Selection & Balance Invariants**: In `hold-disposition-modals.tsx`, physical hold creation strictly scopes selectable warehouses and locations to authoritative positive `Available` balances returned by `useStockBalances`. Removed all unsafe open fallbacks. Clearly differentiates loading state, zero-balance state (`noAvailableStockMsg`), and insufficient quantity alert (`insufficientStockTitle`), and validates position server-side.
6. **Authoritative Location, Lot, Serial & UOM Conservation (QA-25 & QA-26)**: Backend rejects non-matching locations, lots, or serials (`QA-26`), enforces exact base UOM conversion factors during creation and reconciliation (`QA-25`), and reconciles against archived catalog UOM records without emitting duplicate movements or phantom balances.
7. **Administrative Hold Transparency & Downstream Invariants (QA-27)**: An explicit `HoldScope=Administrative` request may preserve a product-bearing inspection link while remaining documentary (`owner_execution_status = 'NotRequired'`); it rejects Inventory identity/quantity fields, emits zero Inventory operations or stock movements, and does not move physical stock balances. Explicit `HoldScope=Physical` remains fail-closed when quantity or authoritative stock is missing.
4. **Authentic Stock Selection & Downstream Safety**: Hold creation modal eliminates hardcoded test UUID fixtures, queries live stock balances via `useStockBalances`, filters warehouses and locations to eligible stock positions, sends the explicit physical/administrative scope, and prevents silent physical-to-administrative hold downgrades when variant context is absent.
5. **Fail-Closed Acceptance Entry Protection**: In `QualityBackendAcceptanceTests.cs`, fail-closed database validation (`AssertAcceptanceDatabase`) prevents execution against protected production or development databases (`todir`, `todir_dev`, `postgres`).
6. **Isolated Acceptance Database Role Protection**: Acceptance runs use dedicated credentials targeting disposable databases without mutating shared cluster passwords (`erp_app`).
