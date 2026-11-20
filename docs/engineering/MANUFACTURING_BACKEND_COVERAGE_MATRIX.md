# Module 26 Manufacturing / MRP Backend Coverage Matrix

Canonical pack: `docs/modules/26_ERP_SAAS_MANUFACTURING_MRP_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This matrix records the current backend delivery evidence. It is deliberately limited to the authorized backend, database, integrations, development-seed, and local acceptance scope; frontend implementation remains a separate mission.

| Capability | Business rule / persistence | API / integration | Seed | Automated / runtime evidence | Status |
|---|---|---|---|---|---|
| Plant and product manufacturing profile | `business_manufacturing.plant`, `manufacturing_product_profile`; tenant/entity keys and RLS | Plant/profile endpoints | Main plant and three profiles per tenant | Isolated migration + seed | PASS |
| BOM versioning | BOM, version, component lines; effective dates and immutable state transitions | BOM/version/approve/activate/supersede endpoints | Active finished-goods BOM | Manufacturing domain tests + migration | PASS |
| Formula/process foundation | Formula versions, ingredients, co-/by-product output lines | Formula/version endpoints | Schema available; no canonical process fixture | Build proof only | PARTIAL |
| Routing and work centers | Routing versions, operations, work centers, calendars | Routing/version/work-center/calendar endpoints | Assembly work center | Build proof + seed | PASS |
| Definition resolver and release snapshot | Deterministic legal-entity/plant/product/date/quantity resolution; immutable snapshot hash | Resolve and production-order endpoints | Approved definition and snapshot | Build + seeded snapshot | PASS |
| Production order lifecycle | Optimistic row version, release/start/hold/resume/complete/close/cancel guards | Production-order endpoints | In-progress partial order | Domain tests; close guard code review | PASS |
| Material conservation | Required/reserved/issued/consumed/returned/scrapped remain distinct; source-key idempotency | Generic material endpoint plus reserve/issue/consume/return aliases | 100 issued = 60 consumed + 40 returned | 6 domain tests; seeded SQL invariant | PASS |
| Inventory orchestration | Manufacturing calls shared Inventory capabilities for reservation, issue, return, and output receipt; no Inventory table writes; confirmed owner evidence is required before the Manufacturing fact | Execution service, tenant/entity-scoped operation snapshot/recovery capability | Inventory-compatible product/warehouse data | Isolated migration/build; fresh authenticated runtime recapture required after evidence-boundary change | PARTIAL |
| Partial output and scrap | Output idempotency, provisional/final value fields, scrap facts, partial completion; output quantity/product is validated before Inventory | Output and scrap endpoints | 60/100 output and cost facts | Seeded SQL invariant; strict request hash code; fresh runtime retry/unknown-outcome proof still required | PARTIAL |
| Cost facts and variance | Planned snapshot, immutable actual facts, derived cost response, variance persistence; material issues create an idempotent Material actual-cost fact from Inventory valuation evidence | Cost and cost-fact endpoints | EGP planned/actual/variance scenario | Seeded SQL + build; end-to-end valued issue reconciliation still required | PARTIAL |
| MRP netting and pegging | Forecast consumption, accepted-demand netting, firmed-supply exclusion, warehouse/UOM-scoped stock pools, due-date supply allocation, pegging | Planning run, demand, run, supply, firm endpoints | 1,000 forecast / 600 accepted Sales in the same PACK bucket; seeded gross/net requirement is 1,000 | Fresh authenticated isolated runtime proved A: no available finished stock => 1,000 PACK Production; B: 600 available finished stock => 400 PACK Production; C: 1,200 accepted demand against 1,000 forecast => 1,200 PACK Production; D: the separate 100-unit EA Purchase supply remained on component variant 2. Forecast consumption persisted as 600 and source-line pegging remained present | PASS |
| Sales/Purchasing source refresh | Sales confirmed remaining demand and eligible Purchasing PO supply are read through owner capabilities; Manufacturing persists planning snapshots without owning source tables | Authenticated `POST /planning-runs/{id}/run` refreshes owner sources before netting | Atlas/Delta each contain confirmed Sales 600 and approved Purchasing 100 inside the horizon | Local API run returned `salesDemandCount=1`, `purchaseSupplyCount=1`; source-line refs and due dates persisted; repeated run remained idempotent | PASS |
| Operation execution and close readiness | Optimistic row version, per-report idempotency, operation events, predecessor/quality guards, material/output/cost close blockers, negative WIP fail-closed | Operations, operation action, close-readiness endpoints | Seeded in-progress operation; close is blocked by partial output/cost | Earlier authenticated API proof; negative-WIP and post-change runtime recapture required | PARTIAL |
| Capacity read model | Calendar hours, efficiency, planned operation minutes and explicit Unknown/Overloaded state | Plant capacity endpoint | Seeded work center; no calendar yields Unknown | Authenticated API returned capacity row with Unknown state and bounded date range | PASS |
| Genealogy command/read model | Input-event/output linkage with allocation and idempotency constraints | Genealogy create, forward and reverse endpoints | No canonical genealogy event fixture | Persistence and API route build proof only; real linked lot/event acceptance remains | PARTIAL |
| Outbox / RabbitMQ / Inbox | Manufacturing events are published transactionally and consumed with inbox + idempotency | Worker `manufacturing-integration` endpoint | Production output event probe | Real local broker proof: fresh `ProductionOrderStartedV1` reached RabbitMQ and Worker once (`Published`, one Inbox row, one Manufacturing idempotency row, one audit row) | PASS |
| RLS and server authorization | Forced tenant RLS and permission-aware API access | API group requires authorization/rate limit | Two seeded tenants | Runtime query: Atlas=1, Delta through Atlas=0 | PASS |
| Genealogy, shop floor, capacity readiness, external demand projections, close/readiness reporting | Tables/seams exist, but complete command/read-model workflows and all cross-module source consumers are not yet delivered | — | No full acceptance fixture | No complete end-to-end proof | NOT COMPLETE |

## Mandatory local evidence

- `dotnet build backend/Backend.sln /m:1 /nr:false --no-restore`: PASS, zero warnings/errors after the last source change; API and Worker local runtime startup also passed.
- Manufacturing domain tests: 6/6 PASS.
- Sales owner capability tests: 2/2 PASS; Purchasing owner capability tests: 10/10 PASS.
- Architecture tests: 21/21 PASS.
- Isolated DbMigrator migration: 234 ordered scripts PASS, including `manufacturing-schema.sql`, `manufacturing-forward-fix-cost-corrections.sql`, `manufacturing-forward-fix-execution-readiness.sql`, and the execution-idempotency hash migration.
- Isolated Development Seed: Atlas and Delta completed; manufacturing fixture created for each tenant.
- Development seed regression: fresh disposable database, migration plus seed twice, 1/1 PASS.
- Real local RabbitMQ/Worker: a fresh `ProductionOrderStartedV1` outbox message reached RabbitMQ and the Worker; the outbox row became `Published`, with one `manufacturing-integration` Inbox row, one Manufacturing worker-idempotency row, and one audit row.
- RLS probe: Atlas manufacturing rows visible in Atlas context; Delta rows invisible in the Atlas context.
- Security tests: 7/7 PASS when run against the isolated acceptance database.
- API runtime: `/health` 200 and `/openapi/v1.json` 200; authenticated isolated runtime proved the MRP A/B/C cases above, including owner-published Sales/Purchasing source refresh, correct PACK UOM normalization, due-date filtering, 600 forecast consumption, exact source-line pegging, and no duplicate source supplies after rerun. The local Projects cross-module/RabbitMQ acceptance suite passed 9/9.
- .NET formatter: `dotnet format backend/Backend.sln --no-restore --verify-no-changes` PASS after formatting the new Manufacturing project.
- `git diff --check`: PASS after the final source/doc changes.

The matrix must be updated when the remaining NOT COMPLETE rows are implemented; it must not be converted to a completion claim from compilation alone. The current execution-boundary corrections intentionally downgrade rows whose post-change authenticated runtime evidence has not yet been recaptured.
