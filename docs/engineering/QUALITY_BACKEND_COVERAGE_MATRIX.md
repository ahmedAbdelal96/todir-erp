# Module 27 Quality Management — Backend Coverage Matrix

Canonical pack: `docs/modules/27_ERP_SAAS_QUALITY_MANAGEMENT_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

| Capability | Business rule | Persistence | Backend/API | Development seed | Automated proof | Runtime proof | Status |
|---|---|---|---|---|---|---|---|
| Quality plans and effective versions | A plan is resolved by context and the applied version is frozen | `quality_plan`, `quality_plan_version`, applicability, snapshot | Plan/version/applicability routes | Receiving plan + active version | Quality domain and migration checks | Seeded Atlas/Delta inspection snapshot | PASS |
| Characteristics/specifications | Numeric, categorical, text and Boolean results are evaluated against the version snapshot | characteristic, specification, plan characteristic | Characteristics/specification routes and result evaluation | Weight characteristic, 9.5–10.5 KG | Numeric/categorical/required tests | Seeded passed result | PASS |
| Sampling | Full, Fixed and Percentage are deterministic and bounded by lot quantity | sampling plan/version, quality sample | Sampling create and calculate route | 10% plan and ten samples | Sampling unit tests | Local seeded rows | PASS |
| Inspection lifecycle | Results are recorded, evaluated, completed and reinspection supersedes history | inspection, results, evaluation, correction, review | Create/list/detail/result/complete/reinspect | Completed receiving inspection | Decision/evidence unit and SQL constraints | Local PostgreSQL seed | PASS |
| Receiving restriction | Mandatory receipt is resolved before Inventory receives and starts in QualityHold | restriction plus typed Shared contract | Purchasing → Quality resolution → Inventory status | Released QualityHold evidence | Compile and migration proof | Local PostgreSQL migration/seed plus isolated Inventory integration | PASS |
| Manufacturing gate | Manufacturing consumes verified Quality evidence and fails closed when unavailable | Existing manufacturing gate; Quality evaluation is source | Existing provider seam now has Quality implementation | Ready seam; no fake Boolean pass | Manufacturing integration compile | Provider reads completed evaluation | PASS |
| Disposition and holds | Evaluation is separate from disposition; release requires a reason | restriction, disposition, deviation, concession | Hold/release/disposition routes | Released hold and seeded NCR | SQL state constraints | Local RLS check | PASS |
| NCR, containment and CAPA | Defect, NCR, containment, corrective action and effectiveness remain distinct | issue, NCR, containment, CAPA tables | Issue/NCR/containment/CAPA/effectiveness routes | Closed NCR/CAPA scenario | Quality unit tests and migration | Local seed | PASS |
| Supplier quality and certificates | Supplier profile can require receiving inspection; certificates require Passed | supplier profile/SCAR/certificates | Supplier/SCAR/COA/CoC routes | Supplier profile and COA seam | Build and schema proof | Local seed | PASS |
| Messaging and integration durability | Inbound cross-module events are processed once through the existing Outbox/RabbitMQ/Worker/Inbox path | `quality_integration_event` and platform Inbox | `quality-integration` Worker endpoint | Seeded event-compatible tenant data | Architecture/build proof | Published outbox + processed Inbox + persisted Quality integration events | PASS |
| Security and isolation | Every Quality row is tenant-owned and forced through RLS | 47 tables, tenant policy loop | Authenticated module group and permission checks | Both canonical tenants | RLS SQL evidence | `erp_app` no-context/tenant-context query plus SecurityTests 7/7 | PASS |

Build Later remains outside this mission: advanced dynamic sampling/ISO tables, SPC charts/signals, Cp/Cpk dashboards, LIMS/retained samples, advanced audits/recall, regulatory e-records, IoT and AI recommendations.

## Correction delta

- Inventory status changes used by Quality now cross a typed `IInventoryQualityCapabilities` contract with an owner idempotency key and persisted owner execution state; Quality does not silently release a failed owner operation.
- Receiving resolution is per purchase-order line and fails closed when the Quality capability is unavailable. Inspection creation is fail-closed for non-AdHoc events without an effective plan.
- Result entry now rejects characteristics outside the frozen plan, duplicate sample/characteristic results, missing required evidence, invalid UOM and uncalibrated instruments; completion cannot pass with no results.
- Dispositions now require an explicit approval transition before execution. Lists have a bounded page-size parameter capped at 100.
- Seed verification corrected Quality references to actual Catalog/Purchasing rows; current local validation reports zero invalid product or plan-version references.
- The remaining QA-01..QA-10 evidence must be refreshed against the corrected Worker/owner-operation path before this matrix can be used as a final completion claim.
