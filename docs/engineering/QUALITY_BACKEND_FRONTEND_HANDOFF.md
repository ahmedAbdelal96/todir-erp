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
- `POST /api/v1/quality/dispositions`, `POST /api/v1/quality/dispositions/{dispositionId}/approve`, `POST /api/v1/quality/dispositions/{dispositionId}/execute`, `GET /api/v1/quality/dispositions/{dispositionId}`
- `POST/GET /api/v1/quality/issues`, `/ncrs`, `/capas`, `/supplier-profiles`, `/scars`, `/certificates`
- `POST /api/v1/quality/capas/{capaId}/root-causes`, `POST /api/v1/quality/capas/{capaId}/actions`, `POST /api/v1/quality/capa-actions/{actionId}/complete`, `POST /api/v1/quality/capas/{capaId}/effectiveness`
- `POST /api/v1/quality/scars/{scarId}/response`, `POST /api/v1/quality/scars/{scarId}/review`

Disposition execution is approval-gated: create → approve → execute. Execution must not be presented as an Inventory release until the owner operation is confirmed. `GET /plans` and `GET /inspections` return typed `{ items, nextCursor }` keyset pages; `pageSize` is bounded to 1–100 (default 50), and the cursor must be sent unchanged for the next page. Receiving resolution is per line, so a mixed receipt may contain both `Available` and `QualityHold` lines.

Operational recovery, owner-operation reconciliation, event replay and forbidden SQL changes are documented in `docs/engineering/QUALITY_BACKEND_RUNBOOK.md`.

## UX safety notes

Use Arabic-first ERP terms: `فحص الجودة`, `احتجاز الجودة`, `إفراج`, `عدم مطابقة`, `إجراء تصحيحي`, `شهادة تحليل`. Keep list pages separate from creation/edit dialogs. Show pending/failed/blocked states, evidence references and error states; do not imply that a Passed inspection automatically releases inventory.
