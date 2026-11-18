# Module 25 Banking Backend Frontend Handoff

Base URL: `/api/v1/banking`. All routes require authentication, tenant context, legal-entity scope, and the permission listed below. IDs are UUIDs; dates are ISO dates; amounts are decimal JSON numbers.

## Treasury accounts

| Method | Path | Request | Response | Permission |
|---|---|---|---|---|
| GET | `/treasury-accounts?legalEntityId=&search=&type=&status=&page=&pageSize=` | query filters; page 1, size 25 defaults | `TreasuryAccountPage` | `banking.accounts.read` |
| GET | `/treasury-accounts/{id}?legalEntityId=` | none | `TreasuryAccountDto` or 404 | `banking.accounts.read` |
| POST | `/treasury-accounts` | `CreateTreasuryAccountRequest` | 201 `TreasuryAccountDto` | `banking.accounts.create` |
| PUT | `/treasury-accounts/{id}?legalEntityId=` | name, purpose, liquidityClass, version | `TreasuryAccountDto` | `banking.accounts.edit` |
| PUT | `/treasury-accounts/{id}/status?legalEntityId=` | `targetState`, optional reason/version | `TreasuryAccountDto` | activate/suspend/close permission |
| POST | `/treasury-accounts/{id}/activate`, `/suspend`, `/close-request`, `/reopen` | `StatusRequest.version` | `TreasuryAccountDto` | lifecycle permission |
| POST | `/treasury-accounts/{id}/change-requests?legalEntityId=` | `CreateAccountChangeRequest` | `AccountChangeRequestDto` | `banking.accounts.edit` |
| GET | `/treasury-account-change-requests/{id}?legalEntityId=` | none | `AccountChangeRequestDto` | `banking.accounts.edit` |
| POST | `/treasury-account-change-requests/{id}/approve` or `/reject` | `ChangeRequestDecisionRequest` | `AccountChangeRequestDto` | `banking.accounts.change.approve` |

Create starts in Draft and Banking validates currency policy, bank masking, branch/cash rules, and profile creation. Status changes are versioned; stale versions return 409. Closed accounts cannot be reactivated.

Canonical identity invariant: for Bank, Cashbox, Wallet, and ProviderClearing, a PaymentAccount created from either Banking or Payments has the same stable id as its Banking TreasuryAccount and a non-null `treasury_account_id` pointing to that canonical row. InternalClearing is a deliberate Payments-only technical account and has no TreasuryAccount. Creation is atomic across the canonical account, compatibility projection, method configuration, and audit entry; a failed projection leaves no orphan canonical account.

## Statements

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| GET | `/treasury-accounts/{id}/statements?legalEntityId=&page=&pageSize=` | pagination | `StatementPage` | `banking.statements.read` |
| POST | `/treasury-accounts/{id}/statements/import?legalEntityId=` | `Idempotency-Key` header + `ImportStatementRequest` | `StatementDto` | `banking.statements.import` |
| GET | `/statements/{id}?legalEntityId=` | none | `StatementDto` | `banking.statements.read` |
| GET | `/statements/{id}/lines?legalEntityId=&page=&pageSize=` | pagination | `StatementLinePage` | `banking.statements.read` |
| GET | `/statement-lines/{id}?legalEntityId=` | none | `StatementLineDto` | `banking.statements.read` |
| POST | `/statements/{id}/validate` or `/process?legalEntityId=` | `ValidateStatementRequest` | `StatementDto` | `banking.statements.validate` |
| POST | `/statement-lines/{id}/classify?legalEntityId=` | `ClassifyStatementLineRequest` | `StatementLineDto` | `banking.statements.validate` |
| POST | `/statement-lines/{id}/create-payment-proposal?legalEntityId=` | `CreatePaymentProposalRequest` | bank-originated candidate | `banking.bank_originated.review` |
| POST | `/bank-originated-candidates/{id}/approve?legalEntityId=` | `BankOriginatedDecisionRequest` | candidate DTO | `banking.bank_originated.approve` |
| GET | `/opening-reconciliations?legalEntityId=&treasuryAccountId=&status=&page=&pageSize=` | pagination and optional account/status filters | `OpeningReconciliationPage` | `banking.reconciliation.read` |
| POST | `/opening-reconciliations` | `CreateOpeningReconciliationRequest` with Ledger opening batch, source system, legacy account, batch id, and opening balances | 201 `OpeningReconciliationDto` | `banking.reconciliation.match` |
| GET | `/opening-reconciliations/{id}?legalEntityId=` | none | `OpeningReconciliationDto` | `banking.reconciliation.read` |
| POST | `/opening-reconciliations/{id}/finalize?legalEntityId=` | `FinalizeOpeningReconciliationRequest.version` | `OpeningReconciliationDto` | `banking.reconciliation.finalize` |
| GET | `/pos-cash-settlements?legalEntityId=&status=&page=&pageSize=` | pagination and optional status | `PosCashSettlementPage` | `banking.cash.read` |
| POST | `/pos-cash-settlements/{id}/confirm?legalEntityId=` | `ConfirmPosCashSettlementRequest` with treasury account and version | `PosCashSettlementDto` | `banking.cash.count.approve` |
| GET | `/provider-settlements?legalEntityId=&status=&page=&pageSize=` | pagination and optional status | `ProviderSettlementPage` | `banking.reconciliation.read` |
| POST | `/provider-settlements/{id}/match?legalEntityId=` | `MatchProviderSettlementRequest` with statement line and version | `ProviderSettlementDto` | `banking.reconciliation.match` |
| GET | `/reports/bank-activity?legalEntityId=&treasuryAccountId=&classification=&direction=&fromDate=&toDate=&page=&pageSize=` | bounded statement activity filters | `BankActivityPage` | `banking.reports.read` |
| GET | `/reports/cash-to-ledger?legalEntityId=&asOfDate=` | optional as-of date | `CashToLedgerReportDto` with Banking projection, Ledger Cash control, difference, basis and timestamp | `banking.reports.read` |
| GET | `/cheques/in-transit?legalEntityId=&page=&pageSize=` | bounded list | `ChequeInTransitPage`; sourced from Payments' cheque records in Draft/Pending, not a duplicate cheque master | `banking.reports.read` |
| GET/POST | `/transaction-code-mappings?legalEntityId=` | mapping list / `CreateTransactionCodeMappingRequest` | `banking.settings.manage` |
| POST | `/reconciliation-rule-sets?legalEntityId=` | `CreateReconciliationRuleSetRequest` | `banking.settings.manage` |

Import is bounded to 10,000 lines. Retry with the same key returns the original statement; a conflicting payload must be surfaced as a business conflict. Lines are immutable evidence with an external id, content hash, direction, amount, currency, and claimed amount.

## Reconciliation

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| POST | `/reconciliations` | legalEntityId, asOfDate, statementId, notes; `bookBalance` is deprecated compatibility input and is rejected if it differs from the posted Ledger value | 201 `ReconciliationDto` with nullable `bookBalance`, `difference`, `ledgerSourceReference`, `ledgerLastPostedAt`, and `ledgerPostedLineCount` | `banking.reconciliation.read` |
| GET | `/treasury-accounts/{id}/reconciliations?legalEntityId=&page=&pageSize=` | pagination | `ReconciliationPage` | `banking.reconciliation.read` |
| POST | `/reconciliations/{id}/matches?legalEntityId=` | line ids, payment refs, external/system/difference, source | `ReconciliationDto`; payment refs are verified through Payments ownership and cannot claim another account/currency/direction/state or exceed remaining amount | `banking.reconciliation.match` |
| PUT | `/reconciliations/{id}/state?legalEntityId=` | target state + version | `ReconciliationDto` | finalize/reopen permission |
| GET | `/reconciliations?legalEntityId=&page=&pageSize=` | pagination | `ReconciliationPage` | `banking.reconciliation.read` |
| GET | `/reconciliations/{id}?legalEntityId=` | none | `ReconciliationDto` | `banking.reconciliation.read` |
| POST | `/reconciliations/{id}/run-rules` or GET `/suggestions` | none | explainable suggestions | `banking.reconciliation.match/read` |
| POST | `/reconciliations/{id}/match` or `/unmatch` | match or unmatch request + version | `ReconciliationDto` | `banking.reconciliation.match` |
| POST | `/reconciliations/{id}/finalize` or `/reopen` | version | `ReconciliationDto` | finalize/reopen permission |

The invariant is `externalAmount = systemAmount + difference`. A match cannot claim more than the remaining line amount. Finalization revalidates the current posted Ledger balance, statement scope, outstanding line claims, adjustments, posting cutoff, and zero difference; a client cannot finalize by changing state alone. Reopening requires the explicit reopen permission.

## Cash and position

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| POST | `/treasury-accounts/{id}/cash-counts?legalEntityId=` | cutoffDate, custodianId, expectedAmount, denominations | 201 `CashCountDto` | `banking.cash.count` |
| GET | `/cash-accounts/{id}/counts?legalEntityId=&page=&pageSize=` | pagination | `CashCountPage` | `banking.cash.read` |
| POST | `/cash-accounts/{id}/counts?legalEntityId=` | same count request | 201 `CashCountDto` | `banking.cash.count` |
| GET | `/cash-counts/{id}?legalEntityId=` | none | `CashCountDto` | `banking.cash.read` |
| PUT | `/cash-counts/{id}/state?legalEntityId=` | target state + version | `CashCountDto` | approve/resolve permission |
| POST | `/cash-counts/{id}/submit`, `/approve`, `/finalize` | version | `CashCountDto` | corresponding cash permission |
| POST | `/cash-counts/{id}/difference-resolutions` | reason, resolution type, amount, evidence, version | resolution DTO | `banking.cash.difference.resolve` |
| POST | `/cash-accounts/{id}/custody-transfers` | from/to custodian and reason | custody DTO | `banking.cash.count` |
| POST | `/transfers` | `Idempotency-Key` + `CreateTransferRequest`; delegates movement lifecycle to Payments and records Banking transfer projection | `banking.transfers.initiate` |
| GET | `/transfers/reconciliation?legalEntityId=&accountId=&state=&page=&pageSize=` | bounded transfer projection list; filters by source/destination account and `InTransit`/`Matched` state | `TransferReconciliationPage` | `banking.transfers.read` |
| POST | `/transfers/{transferId}/reconcile?legalEntityId=` | `ReconcileTransferRequest` with source and/or destination statement-line evidence plus projection version | `TransferReconciliationDto`; one-sided evidence remains `InTransit`, both sides become `Matched` | `banking.reconciliation.match` |
| GET | `/cash-position?legalEntityId=` | legal entity | list of `CashPositionDto` | `banking.cash_position.read` |
| GET | `/cash-forecast?legalEntityId=&asOfDate=&horizonDays=` | filters | `ForecastPage` | `banking.cash_forecast.read` |
| POST | `/cash-forecast/versions` | `CreateForecastVersionRequest` | `ForecastVersionDto` | `banking.cash_forecast.manage` |
| POST | `/cash-forecast/manual-items` | `CreateForecastItemRequest` including `forecastVersionId` | `ForecastItemDto` | `banking.cash_forecast.manage` |

## Connections

| Method | Path | Request / response | Permission |
|---|---|---|---|
| GET | `/treasury-accounts/{id}/connections?legalEntityId=` | `ConnectionDto[]` | `banking.connections.read` |
| POST | `/treasury-accounts/{id}/connections` | `CreateConnectionRequest` → `ConnectionDto` | `banking.connections.manage` |
| POST | `/connections/{id}/refresh` or `/disconnect` | versioned state command | `banking.connections.manage` |

Cash totals come from denomination arithmetic. `IsStale` is true when the projection is older than 24 hours or its source is missing. `BookBalance`, `ExternalBalance`, and `AvailableBalance` are nullable; missing values are not silently treated as zero or reconciled. `BalanceBasis`, `AsOf`, and `ExternalAsOf` must be shown to users. Cash-to-Ledger compares the Banking projection with Ledger's authoritative `Cash` control balance and exposes the difference rather than hiding it.

## Cross-module event contracts

The Worker consumes `ExternalFeedTransactionReceivedV1`, `PosSessionClosedV1`, `ProviderSettlementImportedV1`, and `BankOriginatedFinancialEventApprovedV1` through the platform outbox. Each consumer uses the platform inbox and Banking database uniqueness constraints for replay safety. A closed POS session is projected as pending treasury settlement evidence; Banking does not duplicate POS session truth. A provider settlement is stored as gross, fees, adjustments, and net payout against a ProviderSettlement treasury account and is never treated as a customer receipt. Approved bank-originated candidates emit `BankOriginatedFinancialEventApprovedV1` after the candidate decision commits. `UnknownReceipt` is consumed through the Payments capability, while bank fee, interest, direct-debit, and other supported classified events use Ledger semantic posting. Frontend must display processing state and source evidence; it must not simulate either posting.

## Common business errors

ProblemDetails includes a stable `code`: `banking.account_invalid`, `banking.account_concurrency`, `banking.account_not_found`, `banking.currency_invalid`, `banking.import_idempotency_required`, `banking.statement_too_large`, `banking.statement_line_invalid`, `banking.match_conservation_invalid`, `banking.match_exceeds_unclaimed`, `banking.reconciliation_not_open`, `banking.reconciliation_concurrency`, `banking.transfer_not_found`, `banking.transfer_evidence_required`, `banking.transfer_evidence_duplicate`, `banking.transfer_evidence_invalid`, `banking.transfer_evidence_concurrency`, `banking.transfer_concurrency`, `banking.cash_account_not_found`, and `banking.cash_count_concurrency`.

## Seeded journeys

Atlas and Delta each contain EGP/USD operating banks, a main cashbox, provider clearing, four statements, an open reconciliation, a submitted balanced cash count, a 30-day published forecast, legacy Payments references, and a legal-entity-scoped `banking-reader` user. Frontend acceptance should cover list/search, create-to-activate, maker-checker change requests, import retry, statement validation/classification, line pagination, explainable suggestions, conservation conflict, cash approval, custody, connection lifecycle, forecast version/item creation, transfer initiation plus one-sided/two-sided external evidence reconciliation, opening reconciliation provenance/finalization, stale position display, and tenant/entity isolation. The restricted reader journey must show own-entity data, forbid a sibling entity and cross-tenant entity, and never render `secretRef`.

Validated statement facts, finalized cash counts, and finalized reconciliations are guarded as immutable historical records. All list contracts are paginated where data can grow; projection responses expose balance basis and as-of timestamps.

The statement classification determines the candidate event type: `BankFee`, `InterestIncome`, `InterestCharge`, `DirectDebit`, or `UnknownReceipt`. Approval is immutable review evidence and emits the post-commit event above with the approving membership. Bank fee, interest, and direct-debit approvals are consumed by Worker and posted through the Ledger semantic capability with idempotent accounting facts. `UnknownReceipt` is consumed by Worker through the Payments capability, creating one completed, unallocated incoming `CustomerReceipt` with `SourceType=BankingUnknownReceipt`, no PartyId, no Subledger item, and idempotency keys derived from the candidate; it never invents a customer. Later customer/invoice allocation remains a Payments/Subledger workflow and does not create a second bank movement.

POS close projections are listed through `PosCashSettlementPage` and confirmed with an active same-currency cash account and optimistic version. Provider settlements are listed through `ProviderSettlementPage`; matching claims the payout against a same-currency statement line under row locks, links both records, and projects the treasury activity. Internal transfers have explicit source/destination currencies and optimistic versioning: claiming only the source or destination evidence preserves `InTransit`; claiming both distinct, account- and direction-matching statement lines sets `Matched` and clears in-transit. Stale versions and duplicate matches return business conflicts. Bank activity retains statement provenance and remaining claimable amount. Cheque visibility is deliberately a cross-module read capability: Payments remains the single writer and only unsettled `Cheque` payments are returned as in-transit evidence.

## Acceptance and deferred gate

Current focused Banking evidence is 15 PostgreSQL integration tests plus 4 rules tests against `todir_module25_acceptance_final` after 229 ordered scripts. It includes Ledger-derived reconciliation, forged book-balance rejection, Payments-owned matching, both-direction canonical Banking/Payments account identity, all monetary type mappings and the InternalClearing exception, atomic rollback, duplicate-code concurrency, unknown receipt and internal-transfer evidence reconciliation, opening reconciliation, authenticated HTTP isolation, and live Worker replay, plus focused Ledger and Payments acceptance. The 100k/1M-row performance certification is not represented as PASS. Normal pagination/index/query-shape work is implemented; large-scale certification requires a separate approved performance run.
