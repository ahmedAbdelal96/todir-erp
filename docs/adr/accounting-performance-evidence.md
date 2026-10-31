# Accounting performance evidence ADR

Status: Accepted evidence record — local PostgreSQL certification run on
2026-10-31.

## Decision

Keep journal and Subledger truth in PostgreSQL with tenant/entity-scoped indexes,
keyset GL pagination, and rebuildable balance projections. Do not partition the
journal tables or add a second financial store until a measured production-shaped
workload and a reviewed ADR justify it.

## Evidence

The disposable `accounting_perf_a1` database contained 500,000 journal entries
and 1,000,000 journal lines for the Atlas tenant/entity. The real Trial Balance
query completed in 815 ms with `EXPLAIN (ANALYZE, BUFFERS)`, using parallel scans
and hash aggregation. The real keyset General Ledger first page (101 rows)
completed in 278 ms; it used the posting-date index, the journal-entry-line
composite index, and incremental sort.

The disposable `subledger_pass3_perf_a1` database contained 1,100,153 open/closed
items, 100,000 open hot-party items, and 5,000,000 application rows. The real
current aging query for the 100,000-item party completed in 1,165 ms and used
the tenant/entity/party/direction/polarity index. Historical aging and settlement
logic remain set-based and are covered by the Subledger PostgreSQL integration
tests.

Concurrency evidence includes Ledger duplicate-fact idempotency, the explicit
close-versus-post race (`ConcurrentCloseAndPostSerializeOnTheAccountingPeriod`),
concurrent period lifecycle/reversal checks, Accounting go-live approval
convergence, and Subledger competing-application serialization. The RabbitMQ
recovery regression also proves the financial outbox remains durable while the
broker is unavailable.

## Consequence

The measured 1M journal-line and 100k/5M Subledger evidence is sufficient for the
current local certification target. Ten-million-line certification remains an
environment-scale exercise. Any future partitioning or materialized-report
change must include fresh PostgreSQL plans, correctness reconciliation, and a
new reviewed ADR.
