# Module 28 BC02 Acceptance Matrix

Canonical pack: `docs/modules/28_ERP_SAAS_BUDGETING_FINANCIAL_PLANNING_FPA_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md`.

This matrix preserves the original BC02 scenario identities. Supplemental
evidence uses `BC2-Sxx`; infrastructure evidence does not replace a business
scenario. PASS means the stated behavior was executed locally after the last
relevant source change. The overall BC02 delivery remains `NOT COMPLETE` until
every mandatory row has repeatable PostgreSQL/HTTP evidence.

| ID | Canonical requirement | Exact evidence | Expected / observed result | Status |
|---|---|---|---|---|
| BC2-T01 | Canonical financial Actual | `PlanningBc02ActualIntegrationTests.AuthoritativeLedgerActualRefreshIsImmutableAndIdempotent`; disposable PostgreSQL, Ledger provider, posted fixture fact | Owner fact is persisted once, published, and caller facts are rejected; replay returns the same run | PASS |
| BC2-T02 | Duplicate financial source prevention | `PlanningBc02ActualIntegrationTests.DuplicateOperationalAndLedgerIdentityIsRecognizedOnce`; disposable PostgreSQL with two owner facts sharing a recognition identity | The Ledger fact is selected once; one snapshot fact and 100 EGP total are persisted | PASS |
| BC2-T03 | Actual refresh failure, restatement, and historical basis | `PlanningBc02ActualIntegrationTests.FailedRefreshPreservesPublishedSnapshotAndCorrectionRestatesOnce`; `PublishedActualBasisRemainsPinnedAcrossRestatement`; controlled owner-source outage and corrected source amount | Failed run is durable; an already-published version pins its snapshot identity/mapping/watermark/as-of, while a later current snapshot is published without changing the historical basis | PASS |
| BC2-T04 | Actual write protection | `PlanningBc02ActualIntegrationTests.PublishedInputsAndActualSnapshotsAreProtectedAtBothBoundaries`; repository guard plus runtime `erp_app` PostgreSQL trigger | Published grid mutation is rejected and a published Actual snapshot update is rejected by the database boundary; draft refresh remains supported | PASS |
| BC2-T05 | Mapping integrity | `PlanningBc02ActualIntegrationTests.MetricMappingPublishRetireAndReplacementPreserveHistory`; disposable PostgreSQL repository lifecycle | Draft mapping publishes, retires, and is replaced by a new effective version without rewriting the retired mapping | PASS |
| BC2-T06 | Fiscal calendar | `Modules.Ledger.Tests.LedgerBehaviorIntegrationTests.FiscalPeriodsSupportCustomCalendarsOverlapAndStatusBoundaries`; fresh seeded disposable PostgreSQL | Custom fiscal-year start, 13 periods, overlap rejection, and soft/closed posting boundaries pass through the Ledger-owned calendar used by Planning | PASS |
| BC2-T07 | Published version immutability | Draft-only repository guards plus database guards for input/calculated/plan-owned rows; no all-route acceptance | Published input facts and plan-owned rows are protected; complete alternate-route proof is pending | PARTIAL |
| BC2-T08 | Original and Current Budget | `PlanningBc02ActualIntegrationTests.CurrentBudgetUsesEffectiveApprovedChangesAndNetsTransfers`; `CurrentBudgetAsync`; disposable PostgreSQL | Original 100 + approved effective revisions 20 - 5 + approved transfer in 20 - transfer out 20 = 115; pending, rejected, future changes are excluded; pre-effective as-of returns 100 | PASS |
| BC2-T09 | Budget transfer | `ApproveTransferAsync` deterministic bucket locks and replay guard; existing BC-T09/transfer evidence | Conservation and same-bucket/insufficient checks are implemented; dedicated concurrent transfer evidence is pending | PARTIAL |
| BC2-T10 | Department-scoped submission | Submission coordinate scope and lock guards; no two-planner authenticated HTTP matrix | Attached submitted coordinates lock; planner assignment/read isolation evidence is pending | PARTIAL |
| BC2-T11 | Delegation and segregation of duties | Platform Approvals integration in `RequestSubmissionApprovalAsync`/`ApproveSubmissionAsync` | Planning does not own a second approval engine; delegated/expiry/SoD evidence is pending | PARTIAL |
| BC2-T12 | Top-down target and bottom-up submission | `PlanningBc02ActualIntegrationTests.GovernedTargetAndProposalLifecyclePublishesWithoutLosingIdentity`; disposable PostgreSQL repository workflow | Target and proposal identities persist; proposal submit/adjust/approve and target approve/publish transitions complete, with proposal identity retained in the published projection | PASS |
| BC2-T13 | Concurrent and batch mutation integrity | Batch limit/duplicate/stale guards; no complete two-transaction database vector after final change | Bounded validation is present; race, rollback, explicit-zero and all-route evidence is pending | PARTIAL |
| BC2-T14 | Source authorization and confidentiality | Tenant RLS, permission/lifecycle route guards, owner providers; no dedicated BC02 resource-scope matrix | Shared boundaries remain fail-closed; department/private/payroll/source drillback evidence is pending | PARTIAL |
| BC2-T15 | Owner-source contract reconciliation | `PlanningOwnerReconciliationContracts.cs`, Ledger and Purchasing providers, BC-T10 owner reconciliation | Planning reads public owner contracts and records source identity/version/watermark; broader owner matrix is pending | PARTIAL |
| BC2-T16 | BC01 regression preservation | Existing BC01 acceptance documentation and focused regressions | Prior BC01 evidence predates the final BC02 source change; full applicable rerun is pending | NOT RUN |
| BC2-T17 | Disabled-module compatibility | Planning lifecycle is enforced on routes; no disabled/not-entitled owner workflow vector | Route lifecycle boundary exists; compatibility acceptance is pending | NOT RUN |
| BC2-S01 | Fresh migration and seed | Disposable `bc02_final_fresh_20261127`; Release DbMigrator `--seed development` | 279 ordered platform scripts applied; seed completed for Atlas and Delta; new BC02 tables/triggers installed; disposable database removed after verification | PASS |
| BC2-S02 | Development seed replay | Same disposable database with forced RLS | Seed replay completed without duplicate-key or published-row failure | PASS |
| BC2-S03 | Planning unit regression | `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-build` | 4/4 passed | PASS |
| BC2-S04 | OpenAPI contract | `node scripts/architecture/validate-openapi.mjs` | All 9 contracts validated; Planning 78 paths, including historical Actual-basis, budget as-of, and target/proposal lifecycle routes | PASS |
| BC2-S05 | Release build | `dotnet build backend/Backend.sln --configuration Release --no-restore` | 0 errors, 0 warnings after the latest source and migration changes | PASS |
| BC2-S06 | Actual restatement schema integrity | Fresh migration plus repeated BC2-T01/T02/T03 runs | Historical snapshots accept the same source only within each snapshot; restatement does not violate cross-snapshot uniqueness | PASS |

## Delivery decision

BC02 is **NOT COMPLETE**. The owner-sourced Actual vector, duplicate-source
prevention, durable failure/restatement, migration/seed replay, provenance
fields, governance target/proposal contracts and database guards are delivered. Mandatory fiscal,
published immutability matrix, department/SoD, concurrent, confidentiality,
BC01-after-final-change and disabled-module acceptance evidence remains open.
The overall Module 28 backend remains `NOT COMPLETE`; frontend and Product
Owner acceptance are separate gates.
