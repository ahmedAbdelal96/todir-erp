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

Create starts in Draft and Banking validates currency policy, bank masking, branch/cash rules, and profile creation. Status changes are versioned; stale versions return 409. Closed accounts cannot be reactivated.

## Statements

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| GET | `/treasury-accounts/{id}/statements?legalEntityId=&page=&pageSize=` | pagination | `StatementPage` | `banking.statements.read` |
| POST | `/treasury-accounts/{id}/statements/import?legalEntityId=` | `Idempotency-Key` header + `ImportStatementRequest` | `StatementDto` | `banking.statements.import` |
| GET | `/statements/{id}?legalEntityId=` | none | `StatementDto` | `banking.statements.read` |
| GET | `/statements/{id}/lines?legalEntityId=&page=&pageSize=` | pagination | `StatementLinePage` | `banking.statements.read` |

Import is bounded to 10,000 lines. Retry with the same key returns the original statement; a conflicting payload must be surfaced as a business conflict. Lines are immutable evidence with an external id, content hash, direction, amount, currency, and claimed amount.

## Reconciliation

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| POST | `/reconciliations` | legalEntityId, asOfDate, statementId, bookBalance, notes | 201 `ReconciliationDto` | `banking.reconciliation.read` |
| GET | `/treasury-accounts/{id}/reconciliations?legalEntityId=&page=&pageSize=` | pagination | `ReconciliationPage` | `banking.reconciliation.read` |
| POST | `/reconciliations/{id}/matches?legalEntityId=` | line ids, payment refs, external/system/difference, source | `ReconciliationDto` | `banking.reconciliation.match` |
| PUT | `/reconciliations/{id}/state?legalEntityId=` | target state + version | `ReconciliationDto` | finalize/reopen permission |

The invariant is `externalAmount = systemAmount + difference`. A match cannot claim more than the remaining line amount. Open sessions may be finalized; reopening requires the explicit reopen permission.

## Cash and position

| Method | Path | Request/filters | Response | Permission |
|---|---|---|---|---|
| POST | `/treasury-accounts/{id}/cash-counts?legalEntityId=` | cutoffDate, custodianId, expectedAmount, denominations | 201 `CashCountDto` | `banking.cash.count` |
| GET | `/cash-counts/{id}?legalEntityId=` | none | `CashCountDto` | `banking.cash.read` |
| PUT | `/cash-counts/{id}/state?legalEntityId=` | target state + version | `CashCountDto` | approve/resolve permission |
| GET | `/cash-position?legalEntityId=` | legal entity | list of `CashPositionDto` | `banking.cash_position.read` |

Cash totals come from denomination arithmetic. `IsStale` is true when the projection is older than 24 hours. `BalanceBasis`, `AsOf`, and `ExternalAsOf` must be shown to users; a missing external balance is not silently treated as reconciled.

## Common business errors

ProblemDetails includes a stable `code`: `banking.account_invalid`, `banking.account_concurrency`, `banking.account_not_found`, `banking.currency_invalid`, `banking.import_idempotency_required`, `banking.statement_too_large`, `banking.statement_line_invalid`, `banking.match_conservation_invalid`, `banking.match_exceeds_unclaimed`, `banking.reconciliation_not_open`, `banking.reconciliation_concurrency`, `banking.cash_account_not_found`, and `banking.cash_count_concurrency`.

## Seeded journeys

Atlas and Delta each contain EGP/USD operating banks, a main cashbox, provider clearing, four statements, an open reconciliation, a submitted balanced cash count, a 30-day published forecast, and legacy Payments references. Frontend acceptance should cover list/search, create-to-activate, import retry, line pagination, conservation conflict, cash approval, stale position display, and tenant/entity isolation.

## Deferred gate

The 100k/1M-row performance certification is not represented as PASS. Normal pagination/index/query-shape work is implemented; large-scale certification requires a separate approved performance run.
