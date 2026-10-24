# ADR: Ledger posting foundation

Status: Accepted for Pass 1 (LG00-LG10)

## Decision

The Ledger module is the only writer of general-ledger journal entries. Business modules publish semantic, versioned accounting facts; they never choose ledger accounts and never write `finance_ledger` tables. The posting engine resolves a deterministic, versioned rule and account mapping inside one PostgreSQL transaction.

The module owns one tenant-scoped `finance_ledger` schema. Every row carries tenant and legal-entity identity. RLS is enabled on every table and the runtime role is not allowed to bypass it. Posted journal entries and lines are immutable; corrections are a later reversal capability.

Facts retain their typed payload snapshot and a SHA-256 hash. The source identity and idempotency key are unique per tenant/entity, so retries return the original result while a conflicting payload is rejected. Posting date resolves an explicit fiscal period and all applicable financial locks are enforced before persistence.

## Deliberate boundaries

Pass 1 includes the chart of accounts, journals, fiscal periods and locks, typed fact inbox, posting rules and mapping primitives, deterministic invoice posting, balance validation, and development seed. Transaction coordination integrations, reversals, dimensions, multicurrency revaluation, manual journals, read models, and UI remain deferred to later ledger passes.

## Consequences

The database is the final integrity boundary for tenant ownership, legal-entity relationships, period overlap, balance-safe lines, and immutability. The API exposes setup/read surfaces only; internal callers use `ILedgerPostingEngine`. The design keeps source-document semantics independent from account codes and makes every posted result explainable from stored fact, rule, mapping, and currency snapshots.
