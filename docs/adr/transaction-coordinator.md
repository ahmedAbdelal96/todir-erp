# ADR-005 — Explicit Transaction Coordinator

Status: Accepted

## Decision

State-changing operations enlist participating data access components in one Npgsql connection and transaction, set tenant context with `SET LOCAL`, and persist outbox intents before commit.

## Consequences

Database invariants and integration intent commit atomically without distributed transactions. External brokers and providers remain outside the transaction.

## Evidence

`backend/Core/Transactions/TransactionCoordinator.cs` and PostgreSQL integration transaction tests.
