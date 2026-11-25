# Module 28 Backend Coverage Matrix

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This delivery covers the Module 28 backend foundation and the executable workflows listed below. It does not claim complete Module 28 coverage; the remaining mandatory gaps are recorded explicitly instead of being hidden by a green build.

| Capability | Database | Backend/API | Seed | Automated/runtime evidence | Status |
|---|---|---|---|---|---|
| Planning cycle and lifecycle | `planning_cycle`, RLS, state checks | create/list/open/submit/approve/publish/close/archive | FY2027 Approved cycle | Planning unit tests; authenticated `GET /api/v1/planning/cycles` | PASS |
| Models, model versions, plan versions | model/version tables and tenant FKs | create model/version, create/list/clone/advance plan version | Corporate model and published version | authenticated version listing and clone path | PASS |
| Sparse grid | coordinates and input facts with unique signatures | query, write batch, clear, paste-preview, spread route | Revenue input 150,000 EGP | authenticated grid query returned the seeded cell | PASS |
| Scenarios and assumptions | scenario/assumption/value tables | create/clone scenario; create/value assumption | Shared Base scenario | authenticated scenario clone returned a new Draft | PASS |
| Baseline, revisions, transfers | baseline/revision/transfer tables | current budget, revision approval, coordinate-resolved transfer approval | Published 150,000 EGP baseline | migration and unit/build gates; transfer needs additional end-to-end evidence | PARTIAL |
| Budget control | policy/bucket/reservation tables | check, check-and-reserve, availability, release; idempotency key | Warn policy and 150,000 EGP bucket | authenticated reserve and isolated SQL evidence; final rerun required after last fix | PASS |
| Workforce and CapEx | workforce/capex tables | add/list workforce and add CapEx | 12 FTE and 45,000 EGP CapEx item | authenticated workforce list | PARTIAL |
| Revenue drivers and calculation trace | driver/calculated-fact/run/trace tables | driver write and deterministic volume×price recalculation | 100 × 1,500 EGP driver | formula DAG unit tests; authenticated recalculation evidence still required | PARTIAL |
| Actual snapshots and variance | refresh-run/snapshot/source/variance tables | idempotent keyed refresh, run status, variance query | no fake actuals | API path implemented; source-contract integration proof still required | PARTIAL |
| Tenant/RLS boundary | FORCE RLS on all planning tables; app grants | tenant context through transaction coordinator | Atlas and Delta seed | disposable migration inspection required in final gate | PASS |
| Outbox/audit | planning audit events and published outbox message | publish emits `BudgetPublishedV1`; audit writes on lifecycle changes | seeded planning data | code/build inspection; worker delivery is not yet a Module 28 acceptance proof | PARTIAL |

## Mandatory gaps remaining

- Submission units, slice submit/return, scoped department approval, and approval delegation are not implemented.
- Dimension members, metric mappings, explicit formula persistence/DAG execution, spreading, allocations, and allocation audit are not implemented as complete workflows.
- Full driver-based OPEX, workforce cost impact, CapEx prioritization/approval, integrated income statement/balance sheet/cash-flow projections, FX translation, rolling forecast accuracy, and multi-entity roll-up are not complete.
- Cross-module source contracts/events for Ledger, Projects, Banking, HR, Payroll, Purchasing, Expenses, Sales, Inventory, Manufacturing, Fixed Assets and Subledger require owner-approved adapters and real event/transaction acceptance evidence.
- Excel import/export safety, operational job controls, and the full FP00–FP53 acceptance matrix require additional implementation and verification.

Therefore the current delivery status is `NOT COMPLETE` for the full Module 28 backend mission, while the implemented foundation is suitable for continued backend work.
