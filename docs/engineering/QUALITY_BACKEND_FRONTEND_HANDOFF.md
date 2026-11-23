# Module 27 Quality Management — Backend-to-Frontend Handoff

Frontend should consume `/api/v1/quality` with the authenticated tenant and legal-entity context. Do not expose tenant IDs, database names, raw SQL, internal schema names or implementation status codes as user-facing copy.

## Primary journeys

1. Quality Plans: list plans, create a plan, create/activate a version, attach applicability and characteristics.
2. Receiving inspection: create or view an inspection, enter results/evidence, complete evaluation, show QualityHold until disposition/release.
3. Inspection history: show the frozen plan snapshot, results, evaluation evidence and reinspection chain; never edit a completed result in place.
4. Restrictions and disposition: display reason, lot/product/quantity and status; require a clear release reason; separate Passed/Failed evaluation from Accept/Reject/Use as-is/Concession/Return/Scrap/Rework disposition.
5. NCR/CAPA: create issue/NCR, record containment, add corrective actions and record effectiveness evidence.
6. Supplier quality: maintain supplier risk/inspection policy, open SCAR, and issue COA/CoC only for a Passed inspection.

## Stable contracts

- `GET/POST /api/v1/quality/plans`
- `POST /api/v1/quality/plans/{planId}/versions`
- `POST /api/v1/quality/plan-versions/{versionId}/activate`
- `POST /api/v1/quality/inspections`, `GET /api/v1/quality/inspections`, `GET /api/v1/quality/inspections/{inspectionId}`
- `POST /api/v1/quality/inspections/{inspectionId}/results`
- `POST /api/v1/quality/inspections/{inspectionId}/complete`
- `POST /api/v1/quality/inspections/{inspectionId}/reinspect`
- `POST /api/v1/quality/inspections/{inspectionId}/restrictions`
- `POST /api/v1/quality/restrictions/{restrictionId}/release`
- `POST /api/v1/quality/dispositions`, `POST /api/v1/quality/dispositions/{dispositionId}/execute`
- `POST /api/v1/quality/issues`, `/ncrs`, `/capas`, `/supplier-profiles`, `/scars`, `/certificates`

## UX safety notes

Use Arabic-first ERP terms: `فحص الجودة`, `احتجاز الجودة`, `إفراج`, `عدم مطابقة`, `إجراء تصحيحي`, `شهادة تحليل`. Keep list pages separate from creation/edit dialogs. Show pending/failed/blocked states, evidence references and error states; do not imply that a Passed inspection automatically releases inventory.
