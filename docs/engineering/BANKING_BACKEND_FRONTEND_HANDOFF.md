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
| GET/POST | `/transaction-code-mappings?legalEntityId=` | mapping list / `CreateTransactionCodeMappingRequest` | `banking.settings.manage` |
| POST | `/reconciliation-rule-sets?legalEntityId=` | `CreateReconciliationRuleSetRequest` | `banking.settings.manage` |

Import is bounded to 10,000 lines. Retry with the same key returns the original statement; a conflicting payload must be surfaced as a business conflict. Lines are immutable evidence with an external id, content hash, direction, amount, currency, and claimed amount.

## Reconciliation

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| POST | `/reconciliations` | legalEntityId, asOfDate, statementId, bookBalance, notes | 201 `ReconciliationDto` | `banking.reconciliation.read` |
| GET | `/treasury-accounts/{id}/reconciliations?legalEntityId=&page=&pageSize=` | pagination | `ReconciliationPage` | `banking.reconciliation.read` |
| POST | `/reconciliations/{id}/matches?legalEntityId=` | line ids, payment refs, external/system/difference, source | `ReconciliationDto` | `banking.reconciliation.match` |
| PUT | `/reconciliations/{id}/state?legalEntityId=` | target state + version | `ReconciliationDto` | finalize/reopen permission |
| GET | `/reconciliations?legalEntityId=&page=&pageSize=` | pagination | `ReconciliationPage` | `banking.reconciliation.read` |
| GET | `/reconciliations/{id}?legalEntityId=` | none | `ReconciliationDto` | `banking.reconciliation.read` |
| POST | `/reconciliations/{id}/run-rules` or GET `/suggestions` | none | explainable suggestions | `banking.reconciliation.match/read` |
| POST | `/reconciliations/{id}/match` or `/unmatch` | match or unmatch request + version | `ReconciliationDto` | `banking.reconciliation.match` |
| POST | `/reconciliations/{id}/finalize` or `/reopen` | version | `ReconciliationDto` | finalize/reopen permission |

The invariant is `externalAmount = systemAmount + difference`. A match cannot claim more than the remaining line amount. Open sessions may be finalized; reopening requires the explicit reopen permission.

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

Cash totals come from denomination arithmetic. `IsStale` is true when the projection is older than 24 hours. `BalanceBasis`, `AsOf`, and `ExternalAsOf` must be shown to users; a missing external balance is not silently treated as reconciled.

## Common business errors

ProblemDetails includes a stable `code`: `banking.account_invalid`, `banking.account_concurrency`, `banking.account_not_found`, `banking.currency_invalid`, `banking.import_idempotency_required`, `banking.statement_too_large`, `banking.statement_line_invalid`, `banking.match_conservation_invalid`, `banking.match_exceeds_unclaimed`, `banking.reconciliation_not_open`, `banking.reconciliation_concurrency`, `banking.cash_account_not_found`, and `banking.cash_count_concurrency`.

## Seeded journeys

Atlas and Delta each contain EGP/USD operating banks, a main cashbox, provider clearing, four statements, an open reconciliation, a submitted balanced cash count, a 30-day published forecast, and legacy Payments references. Frontend acceptance should cover list/search, create-to-activate, maker-checker change requests, import retry, statement validation/classification, line pagination, explainable suggestions, conservation conflict, cash approval, custody, connection lifecycle, forecast version/item creation, stale position display, and tenant/entity isolation.

Validated statement facts, finalized cash counts, and finalized reconciliations are guarded as immutable historical records. All list contracts are paginated where data can grow; projection responses expose balance basis and as-of timestamps.

The unknown-receipt endpoint creates a Banking candidate with an explicit `UnknownReceipt` event type; approval is a review state and does not silently create a Payments record. A Payments orchestration adapter remains a tracked cross-module completion item.

## Deferred gate

The 100k/1M-row performance certification is not represented as PASS. Normal pagination/index/query-shape work is implemented; large-scale certification requires a separate approved performance run. Current focused Banking evidence is 6/6 tests against `todir_module25_acceptance` after 218 ordered scripts.
