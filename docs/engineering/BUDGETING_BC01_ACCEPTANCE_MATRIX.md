# Module 28 BC01 Acceptance Matrix

This matrix preserves the authorized BC01 scenario identities. `PASS` means the
named owner workflow and its financial assertion were executed; implementation,
compile, or Planning-only evidence is not substituted for source acceptance.

| Scenario | Canonical requirement and expected outcome | Actual evidence, command, and observed state | Status |
|---|---|---|---|
| BC-T01 | PR-to-PO conversion; approved source and resulting commitment remain financially conserved | Real authenticated Purchasing HTTP journey on disposable `bc01_accept_20261125`: PR `100,000 EGP` reached `FullyConverted`; two approved POs were persisted at `40,000` and `60,000`; reservation ledger contained `40,000 + 60,000` open Commitment and no open PR Precommitment. Duplicate conversion returned the existing PO; above-remaining conversion was rejected. | PASS |
| BC-T02 | Partial invoice recognition; open commitment decreases by recognized amount and Actual is posted once | `ProjectsCrossModuleAcceptanceTests.RealSupplierInvoiceActualReconcilesProjectCommitment`: real PostgreSQL Purchasing → Invoicing owner workflow, one of two PO units invoiced, `InvoiceFinalizedV1` produced one bounded cost/Actual fact; replay remained one fact. | PASS |
| BC-T03 | Final invoice recognition; final source amount closes the remaining commitment without double count | No complete final-invoice owner vector was executed. | NOT RUN |
| BC-T04 | Partial invoice followed by cancellation; remaining commitment is released and recognized Actual remains | No complete source cancellation-after-partial-recognition vector was executed. | NOT RUN |
| BC-T05 | Concurrent competing source approvals; at most one strict approval succeeds with no orphan reservation | No concurrent two-source approval run was executed. | NOT RUN |
| BC-T06 | Atomic source and reservation rollback on owner failure | No controlled failure after reservation and before owner completion was executed for BC01. | NOT RUN |
| BC-T07 | Partial PR conversion; `100,000 → 40,000 + 60,000`, remaining precommitment and commitment are conserved | Same real HTTP journey as BC-T01. DB inspection: PR `FullyConverted`, total converted `100,000`; released PR reservation `60,000`; PO reservations `40,000` and `60,000`; PO totals `40,000` and `60,000`. | PASS |
| BC-T08 | Expense reversal; `30,000` Actual returns exactly once to available budget | `PlanningBudgetControlIntegrationTests`, `TODIR_EXPENSES_TEST_CONNECTION=bc01_accept_20261125`: 7/7 passed, including durable reversal correction and idempotency. Full injected multi-boundary runtime failure matrix was not run. | PARTIAL |
| BC-T09 | Source-specific Budget override; bounded approval, usage and reuse prevention | `PlanningBudgetControlIntegrationTests` included bounded source-specific override use and passed 7/7 on disposable PostgreSQL. An authenticated end-to-end override decision journey remains open. | PARTIAL |
| BC-T10 | Canonical opening reconciliation; owner facts produce Actual `40,000`, Commitment `60,000`, Precommitment `20,000`, availability `80,000` | Owner reconciliation and rebuild code exists, but no complete multi-owner opening vector with independent Ledger source facts was executed. | NOT RUN |
| BC-T11 | Omitted bucket bypass prevention; active strict control rejects an uncontrolled source | `PlanningBudgetControlIntegrationTests` included omitted-bucket Block behavior and passed 7/7 on disposable PostgreSQL. | PASS |
| BC-T12 | Correction and event replay; correction is bounded and duplicate delivery is idempotent | `RealSupplierInvoiceActualReconcilesProjectCommitment` passed: supplier invoice plus credit correction remained one cost fact per source and one integration event on replay. The test uses MassTransit TestHarness, not the required live RabbitMQ Worker path. | PARTIAL |

## Additional stable evidence IDs

| ID | Requirement | Evidence | Status |
|---|---|---|---|
| BC-S01 | Planning policy, horizon, reservation, correction and reconciliation focused behavior | Planning 4/4; Expenses Planning Budget Control 7/7 on disposable PostgreSQL. | PASS |
| BC-S02 | Authenticated runtime API composition and tenant/entity security contracts | `ApiCompositionTests` with isolated runtime-role connection and Development seed password: 4/4 passed. | PASS |
| BC-S03 | Fresh migration and Development Seed replay | Disposable `bc01_accept_20261125`; 273 ordered scripts and Atlas/Delta seed completed. | PASS |
| BC-S04 | Real Purchasing PR runtime vector and approval authorization | Cookie-authenticated API; separate approver role was required; self-approval rejected; 40/60 conversion and duplicate/over-limit checks passed. | PASS |
| BC-S05 | Real supplier invoice and correction owner path | `RealSupplierInvoiceActualReconcilesProjectCommitment`: 1/1 passed after no-reservation invoicing correction. | PASS |
| BC-S06 | Ledger regression suite | 54/56 passed with the disposable DB; one bank-originated fixture lacked its Banking registration and one live Worker test lacked its dedicated Worker connection. These are not BC01 acceptance passes. | PARTIAL |
| BC-S07 | Source integration defect corrections | Purchasing approval permission now maps to `purchasing.request.approve`; PO conversion preserves an existing PR reservation and uses the resolved bucket; invoices without a Planning reservation skip budget consumption, while closed reservations still fail closed. | PASS |
| BC-S08 | Release, architecture, OpenAPI and formatter gates | `dotnet build backend/Backend.sln --configuration Release --no-restore` passed with 0 warnings/errors; Architecture 23/23 passed; OpenAPI validation passed for the 9 contracts; targeted Core/Purchasing/Planning/Invoicing/Ledger format verification passed; `git diff --check` passed. | PASS |

## Delivery decision

`BC01 NOT COMPLETE`. The real PR conversion and partial invoice/correction slices
now have evidence, but the mandatory final invoice/cancellation, concurrency and
rollback, full Expense failure-injection recovery, canonical Ledger opening
reconciliation, live RabbitMQ/Worker replay, and complete authenticated Budget
Control activation evidence remain open. This matrix does not certify Module 28
or production readiness.
