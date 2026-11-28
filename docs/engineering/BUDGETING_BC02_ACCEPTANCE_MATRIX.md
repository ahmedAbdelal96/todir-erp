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
| BC2-T07 | Published version immutability | `PlanningGovernanceConcurrencyAcceptanceTests` authenticated all-route published mutation matrix | Every protected mutation family rejects and no published row changes; draft-only mutation remains available | PASS |
| BC2-T08 | Original and Current Budget | `PlanningBc02ActualIntegrationTests.CurrentBudgetUsesEffectiveApprovedChangesAndNetsTransfers`; `CurrentBudgetAsync`; disposable PostgreSQL | Original 100 + approved effective revisions 20 - 5 + approved transfer in 20 - transfer out 20 = 115; pending, rejected, future changes are excluded; pre-effective as-of returns 100 | PASS |
| BC2-T09 | Budget transfer | `PlanningGovernanceConcurrencyAcceptanceTests` concurrent transfer vector | One conserving approval outcome, idempotent replay, no overspend or orphan delta | PASS |
| BC2-T10 | Department-scoped submission | `PlanningAccessGovernanceAcceptanceTests` planner/manager assignment and lock vectors | Assignment isolation and attached-coordinate-only locking pass through authenticated HTTP | PASS |
| BC2-T11 | Delegation and segregation of duties | `PlanningAccessGovernanceAcceptanceTests` independent approver and active/expired delegation vectors | Self-approval and expired delegation fail; valid delegated approval succeeds through Platform Approvals | PASS |
| BC2-T12 | Top-down target and bottom-up submission | `PlanningBc02ActualIntegrationTests.GovernedTargetAndProposalLifecyclePublishesWithoutLosingIdentity`; disposable PostgreSQL repository workflow | Target and proposal identities persist; proposal submit/adjust/approve and target approve/publish transitions complete, with proposal identity retained in the published projection | PASS |
| BC2-T13 | Concurrent and batch mutation integrity | `PlanningGovernanceConcurrencyAcceptanceTests` transactional batch rollback vector | Duplicate/stale/mixed-scope input rejects atomically and explicit zero remains distinct from missing | PASS |
| BC2-T14 | Source authorization and confidentiality | `PlanningAccessGovernanceAcceptanceTests` workforce permission, sensitive-data and tenant matrix | Unauthorized reads are denied without data leakage or technical details | PASS |
| BC2-T15 | Owner-source contract reconciliation | `PlanningOwnerFinancialIntegrationTests` 2/2 plus Package-A provider registration 1/1 | Fourteen providers are scoped/replay-stable; real Payroll/Fixed Assets/Expenses lifecycle facts match Ledger and correction nets to zero | PASS |
| BC2-T16 | BC01 regression preservation | Fresh Budget Control 8/8, Projects 14/14 and owner/Purchasing/BC-T09/BC-T10 selections | Applicable BC01 financial assertions remain green after final BC02 changes | PASS |
| BC2-T17 | Disabled-module compatibility | `PlanningAccessGovernanceAcceptanceTests` disabled, ReadOnly and not-entitled owner vector | Owner-dependent refresh/control workflows fail closed without partial state | PASS |
| BC2-S01 | Fresh migration and seed | Disposable `bc02_final_fresh_20261127`; Release DbMigrator `--seed development` | 279 ordered platform scripts applied; seed completed for Atlas and Delta; new BC02 tables/triggers installed; disposable database removed after verification | PASS |
| BC2-S02 | Development seed replay | Same disposable database with forced RLS | Seed replay completed without duplicate-key or published-row failure | PASS |
| BC2-S03 | Planning unit regression | `dotnet test backend/Tests/Modules/Planning/PlanningTests.csproj --configuration Release --no-build` | 4/4 passed | PASS |
| BC2-S04 | OpenAPI contract | `node scripts/architecture/validate-openapi.mjs` | All 9 contracts validated; Planning 78 paths, including historical Actual-basis, budget as-of, and target/proposal lifecycle routes | PASS |
| BC2-S05 | Release build | `dotnet build backend/Backend.sln --configuration Release --no-restore` | 0 errors, 0 warnings after the latest source and migration changes | PASS |
| BC2-S06 | Actual restatement schema integrity | Fresh migration plus repeated BC2-T01/T02/T03 runs | Historical snapshots accept the same source only within each snapshot; restatement does not violate cross-snapshot uniqueness | PASS |

## Delivery decision

BC02 backend acceptance is **PASS**. The owner-sourced Actual vector,
duplicate-source prevention, durable failure/restatement, migration/seed
replay, provenance fields, governance contracts and all listed governance,
confidentiality, owner-compatibility and BC01-after-change vectors are closed.
Frontend and Product Owner acceptance remain separate gates.
