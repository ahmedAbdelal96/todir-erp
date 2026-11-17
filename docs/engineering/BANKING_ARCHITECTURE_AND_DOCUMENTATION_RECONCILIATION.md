# Module 25 Banking Architecture and Documentation Reconciliation

## Decision

`finance_banking.treasury_account` is the canonical master for every real bank, cash, petty-cash, digital-wallet, and provider-settlement account. `finance_payments.payment_account` remains a compatibility projection for payment workflows and payment-method configuration; it is not a second master.

The migration preserves stable identifiers, adds `payment_account.treasury_account_id`, backfills every real legacy account, and adds a tenant/legal-entity foreign key to the canonical record. New payment-account creation now calls the Banking capability first and writes the Payments row as a compatibility projection. Internal clearing remains a Payments-only technical account because it is not a treasury balance.

## Ownership matrix

| Fact | Owner | Consumer contract |
|---|---|---|
| Treasury account identity, lifecycle, currency policy, masking | Banking | `IBankingTreasuryAccountCapability` and `/api/v1/banking` |
| Payment document and payment method configuration | Payments | Payments API and `payments.money-movement` |
| External statement evidence and line claims | Banking | statement import and reconciliation APIs |
| Journal entries and posted accounting facts | Ledger/Accounting | accounting facts/events |
| Party/open-item balances | Directory/Subledger | subledger capabilities |
| POS/session settlement | POS | `pos.cash-settlement` contract |

## Applied reconciliation decisions

- Banking owns one `finance_banking` schema; no per-tenant or per-capability schemas were introduced.
- RLS is enabled and forced on all Banking tables. Runtime queries set `app.tenant_id`; the migration-only compatibility backfill temporarily removes `FORCE` for the two touched legacy tables and restores it before the script completes.
- External identifiers and sensitive bank values are masked; the API never returns `secret_ref`.
- Statement imports require an idempotency key, are bounded to 10,000 lines, and deduplicate by tenant/account/external line id.
- Reconciliation matches lock statement lines, enforce unclaimed-amount conservation, and record an immutable accepted match.
- Cash counts calculate from denominations, preserve expected/count/difference, and require explicit approval before finalization.
- Cash position is a projection/read model with explicit basis and staleness rather than a mutable generic balance setter.

## Known disposition

The canonical pack's 100k/1M-row performance certification is intentionally not executed in a local development acceptance run. The module has bounded pagination, supporting indexes, and a 10,000-line import guard; heavyweight certification remains a separately approved performance gate.
