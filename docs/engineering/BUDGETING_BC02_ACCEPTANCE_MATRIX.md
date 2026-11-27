# Module 28 BC02 Acceptance Matrix

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

BC02 is a backend/database closure slice. This matrix records only evidence
run locally after the BC02 changes; implementation presence or OpenAPI shape is
not substituted for a business result.

| Scenario | Required proof | Evidence | Status |
|---|---|---|---|
| BC2-T01 | Actuals are loaded from an owner contract, not caller facts; snapshot is persisted | `PlanningBc02ActualIntegrationTests.AuthoritativeLedgerActualRefreshIsImmutableAndIdempotent`: real disposable PostgreSQL, Ledger owner provider, published snapshot and source-fact count/total assertions | PASS |
| BC2-T02 | Ledger is preferred and duplicate operational facts do not double count | Owner-source selection is implemented in `PlanningRepository.RefreshActualsAsync`; no independent duplicate-provider regression was executed in this closure | OPEN |
| BC2-T03 | Watermark is authoritative, refresh is idempotent, failure can retry | Deterministic provider watermark and replay returned the same successful run; failed-run retry was implemented but no injected failed-refresh recovery test exists | PARTIAL |
| BC2-T04 | Snapshot provenance, reconciliation state and historical immutability | New snapshot metadata/fact tables, RLS and source provenance migration; runtime snapshot total/count verified by BC2-T01 | PASS for the verified vector |
| BC2-T05 | Versioned metric mappings publish/retire and unmapped material facts fail closed | Mapping table, lifecycle repository methods and seeded `ledger/FinancialRole/OPEX → OPEX` mapping exist; no dedicated publish/retire HTTP regression was run | PARTIAL |
| BC2-T06 | Financial/operational metric metadata and controlled mappings | Metric metadata columns, create API, target-metric FK and mapping API are implemented; no full metric-governance acceptance vector was run | PARTIAL |
| BC2-T07 | Fiscal periods, plan versions and published immutability | Existing Planning lifecycle tests/build plus draft-only grid/spread/allocation guards; no dedicated BC02 fiscal/version matrix was run | PARTIAL |
| BC2-T08 | Grid batch mutation has bounded size, duplicate rejection, stale-version and Actual/Calculated/Source guards | Guards are implemented in `WriteGridAsync`; no new PostgreSQL grid mutation regression was run | PARTIAL |
| BC2-T09 | Original Budget and Current Budget have governed lifecycle | Baseline lifecycle and current-budget/revision/transfer code is implemented; no dedicated baseline lifecycle regression was run | PARTIAL |
| BC2-T10 | Revisions and transfers are conserving and retry-safe | Transfer approval now locks buckets deterministically and transfer rows for replay safety; no dedicated transfer concurrency regression was run | PARTIAL |
| BC2-T11 | Submission units attach exact coordinates and lock submitted scopes | Coordinate attachment, scope validation and Submitted/Approved/Locked write guards are implemented; no dedicated submission HTTP regression was run | PARTIAL |
| BC2-T12 | Department submission approval uses Platform Approvals and SoD | Submission approval request/decision delegates to Platform Approval and owner scope is checked; no dedicated non-self-approval HTTP regression was run | PARTIAL |
| BC2-T13 | Tenant, legal-entity, permission and lifecycle boundaries fail closed | Architecture 23/23; authenticated BC-T09 HTTP 1/1; shared RLS/authorization patterns remain active | PASS for covered boundaries; not exhaustive |
| BC2-T14 | Fresh migration and Development Seed are repeatable | Disposable `bc02_accept_20261127`: migration applied, Development Seed completed; focused `DevelopmentSeedRunsTwiceOnFreshDatabaseWithForcedRls` passed 1/1 | PASS |
| BC2-T15 | Seed contains usable plan/version/metric/mapping facts and does not duplicate on replay | Development Seed completed for Atlas/Delta and seed replay test passed; OPEX actual mapping is published | PASS for seed replay |
| BC2-T16 | OpenAPI and Release/architecture gates remain valid | OpenAPI validation passed for all 9 contracts; Planning contract has 72 paths; Release build and Architecture 23/23 passed | PASS |
| BC2-T17 | Final BC02 backend acceptance evidence covers every mandatory scenario | T02, T03 recovery, T05–T12 still lack dedicated repeatable acceptance vectors | NOT COMPLETE |

## Delivery decision

The BC02 owner-source, snapshot, migration/seed, transfer-lock, grid-guard and
Platform Approval implementation changes are present and the verified vectors
pass locally. BC02 is not declared verified because the mandatory acceptance
matrix still contains OPEN/PARTIAL scenarios. Broader Module 28 remains
`NOT COMPLETE`; frontend and Product Owner acceptance are separate gates.
