# Module 25 Banking Backend Runbook

This runbook covers local development, acceptance, and operational diagnosis for the Banking & Cash Management backend. It is intentionally limited to the disposable acceptance environment and the approved local runtime; never point migration or acceptance commands at populated development data.

## Local infrastructure

- PostgreSQL acceptance container: `todir-module24-test-postgres`, port `15433`.
- RabbitMQ: `todir-rabbitmq-1`, port `5672`, vhost `/erp-dev`.
- API: `http://127.0.0.1:5080`.

Use a dedicated `todir_module25_acceptance` database and the repository's disposable credentials. Do not change cluster-wide PostgreSQL credentials.

## Migration and seed

Run the migrator with the acceptance administrator connection:

```powershell
$env:TODIR_DATABASE_CONNECTION = 'Host=127.0.0.1;Port=15433;Database=todir_module25_acceptance;Username=postgres;Password=postgres'
dotnet run --project backend/DbMigrator/DbMigrator.csproj --no-restore
```

The migrator is ordered by module manifests and is idempotent. Verify the Development seed after migration: Atlas and Delta each have four statements, one reconciliation, one submitted cash count, and one published forecast. Banking seed reconciliations intentionally have no fabricated book balance; runtime reconciliation reads the posted Ledger. Seed cash projections expose their basis and timestamps, and repeat runs must not overwrite an existing authoritative projection.

## API and Worker runtime

Run the API with the application connection and RabbitMQ settings from the local environment. The API must return `200` from `/health`; a request to a protected Banking route without authentication must return `401`.

Run the Worker with `TODIR_RABBITMQ_CONSUMER=true`. The Worker must register the external-feed, POS settlement, provider settlement, and bank-originated financial event consumers before replay testing.

Outbox messages are dispatched after commit. Consumers use Inbox idempotency. A bank-originated approval is the only accepted source for the unknown-receipt path: the approval carries the reviewer membership, the Worker calls the Payments capability, and Payments records one completed unallocated customer receipt without inventing a party or Subledger item. Replaying the same message must not create a second payment.

## Banking workflow checks

- Import statement lines with an idempotency key and external-line identity.
- Reconcile only lines belonging to the selected legal entity and account. Row locks protect claimable amounts; partial claims are allowed only within remaining amount.
- Reconciliation book balance is authoritative only when read from the Ledger for the same treasury account, legal entity, currency, and cutoff. The persisted Ledger source snapshot is historical evidence; a client-supplied value cannot override it.
- Match payment ids through the Payments read capability. Verify tenant/entity/account/currency/direction, `Completed` state, amount availability, and transactional system-claim ownership before accepting a match.
- Banking account creation and the Payments compatibility projection are one transaction and share the same canonical treasury id. Activation synchronizes the compatibility status and creates the usable payment-method configuration.
- Internal transfers begin `InTransit`. Claiming a source debit and destination credit with matching account, direction, currency, and amount transitions the projection to `Matched`. Reuse of either statement line is rejected.
- POS and provider settlement confirmation is optimistic-concurrency protected and preserves gross, fee, adjustment, and net evidence.
- Finalized statements, reconciliations, and cash counts are immutable at the database boundary.

## Troubleshooting

- `401`: confirm the request has an authenticated session and the required Banking permission; tenant context is mandatory.
- `403`: confirm the membership has permission for the target legal entity and that the resource belongs to the selected tenant/entity.
- `banking.reconciliation.*` conflicts: refresh the record and retry with the current `Version`; do not overwrite another user's claim.
- Duplicate import or replay: retain the original idempotency key/message id and inspect the Inbox/outbox evidence; do not manually insert a second business record.
- Unallocated receipt: inspect the bank statement line, approval event, and Payment source reference. A missing membership or a forged source reference must be rejected.
- Stale cash position: inspect projection timestamps and the latest committed statement/settlement events before treating it as a balance error.
- `banking.cash_projection_missing`: do not substitute zero. Rebuild or re-ingest the projection from its owning source and preserve the source watermark.

## Release evidence

The final acceptance record must include the exact migration count (229), seed counts, focused Banking/Ledger/Payments test results, Ledger-derived balance evidence, forged-input rejection, canonical account identity evidence, authenticated HTTP isolation evidence, Worker replay evidence, and a clean working tree. Do not claim heavyweight 100k/1M-row performance certification unless those separate approved benchmarks have actually been executed; this gate remains explicitly unverified for Module 25.
