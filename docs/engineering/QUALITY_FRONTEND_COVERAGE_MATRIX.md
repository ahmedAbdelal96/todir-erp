# Module 27 Quality Management — Frontend Coverage Matrix

**Module:** Module 27 — Quality Management (`إدارة الجودة الشاملة`)  
**Canonical Specification:** `docs/modules/27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`  
**Delivery Gate:** `FRONTEND COMPLETE — READY FOR HUMAN PRODUCT/UX ACCEPTANCE`  

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

---

## 2. Real-Stack Evidence Index

All 16 visual artifacts have been generated through real-stack Playwright execution against the live PostgreSQL-backed TODIR ERP backend:

1. `01-quality-overview.png`: High-contrast Oxford Navy shell, 3-headline KPI ribbon (`#0284c7`, `#d97706`, `#dc2626`), and quick priority queues.
2. `02-quality-plans-register.png`: Quality Plans register, active version status badges, and plan creation modal trigger.
3. `03-quality-characteristics-catalog.png`: Characteristic specifications catalog with quantitative target limits and qualitative standards.
4. `04-quality-sampling-plans.png`: Sampling plans register displaying AQL sampling tables, normal/tightened inspection levels.
5. `05-quality-inspections-queue.png`: Operational inspection work queue filtered by status (`Pending`, `InProgress`, `Passed`, `Failed`) and event type.
6. `06-inspection-workbench-active.png`: Deep inspection workbench (`/quality/inspections/:id`) with characteristic-by-characteristic entry.
7. `07-inspection-evaluation-completed.png`: Conformance evaluation with pass/fail decision recording and inspector assignment.
8. `08-inspection-domain-invariant-warning.png`: Prominent system warning banner: *"Passed != Released"* (نجاح الفحص لا يعني الإفراج التلقائي عن المخزون).
9. `09-reinspection-workflow.png`: Reinspection trigger and linked parent-inspection cycle history.
10. `10-material-holds-restrictions.png`: Active hold restrictions register with initial, released, and remaining quantity accounting.
11. `11-partial-quantity-release-dialog.png`: Partial release modal enforcing remaining balance validation and operational release logging.
12. `12-quality-dispositions-mrb.png`: Material Review Board (MRB) dispositions queue with technical decision tracking (`UseAsIs`, `Rework`, `Scrap`, `SupplierReturn`).
13. `13-quality-issues-log.png`: Quality issues log categorized by source, severity, and defect classification.
14. `14-ncrs-containment-actions.png`: Non-Conformance Report (NCR) lifecycle with immediate containment action tracking.
15. `15-capa-root-cause-investigation.png`: CAPA workflow with Ishikawa 6M (Man, Machine, Material, Method, Measurement, Environment) root cause logging and action tracking.
16. `16-supplier-quality-scar-certificates.png`: Supplier quality profiles, SCAR dispute workflows, and Certificate of Analysis (COA/COC) issuance register.
