# Module 24 Backend Coverage Matrix

| Canonical requirement | Business rule | Persistence | API | Seed | Automated proof | Runtime proof |
|---|---|---|---|---|---|---|
| Service request | Requests are tenant/legal-entity scoped and lifecycle controlled | `service_request` | `/requests`, lifecycle actions | Atlas/Delta request | Service build and rules tests | Authenticated list/create path |
| Work execution | Work order version is required for lifecycle mutation | `work_order`, visits, assignments | `/work-orders`, `/visits`, `/mobile/my-day` | Internal + customer work | Service rules tests | Seeded records read via API |
| Installed base | One authoritative equipment record, hierarchical subjects | `installed_base_item`, component events | `/installed-base`, component-events | Seeded customer equipment | RLS and seed verification | Installed-base create/list |
| Planned maintenance | Due rows are idempotent and conversion creates one work order | `maintenance_plan`, `maintenance_due` | plan, due, conversion routes | Fixed-asset plan/due | migration + seed | KPI/due data |
| Meter | Non-reset readings cannot decrease | meter definition/readings | meter route | Seed meter | service rule/DB checks | seeded meter persistence |
| Parts | Reserve/issue/consume/return cannot exceed quantity | `work_part_requirement` | `/parts/{id}` actions | consumed part projection | movement unit tests | persisted counters |
| Labor/cost | Labor replay uses idempotency key | labor/cost facts | `/labor` | seeded labor/cost | build + seed | persisted fact |
| Coverage | Active contract/warranty decisions are explainable | contract, warranty, coverage tables | `/contracts`, `/warranties`, entitlement | active contract/usage | seed verification | entitlement route |
| SLA | Profile creates bounded clocks with response/arrival/resolution deadlines | `sla_profile`, `sla_clock`, events | `/sla/profiles`, `/sla/clocks` | contract path | build | API composition |
| Billing | One Service charge can request and allocate one external invoice | charges/allocation | charges, invoice request/allocation | seeded charge | outbox persistence | KPI and charge read |
| Offline | Client command ID is idempotent; stale version is conflict | `offline_command` | `/mobile/commands` | none required | schema uniqueness | command contract |
| Security | Missing tenant context denies; tenant policies are forced | all Service tables | permission-scoped routes | two tenants | RLS query proof | authenticated tenant reads |

All entries are intentionally bounded to Service-owned truth. Cross-module records are represented by capability contracts, outbox messages, and external references rather than foreign keys into another module.
