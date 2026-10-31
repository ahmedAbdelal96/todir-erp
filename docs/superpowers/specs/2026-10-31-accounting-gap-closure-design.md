# Accounting Backend Gap Closure Design

## Intent

Complete the backend/database-owned Accounting scope from the canonical Accounting Design Pack without rebuilding Ledger, Subledger, Inventory, FinanceBase, Invoicing, or Payments. The result must preserve financial source-of-truth ownership and make every command retry-safe, tenant-safe, auditable, and recoverable.

## Goals

- Make go-live, close/reopen, reconciliation, statement, and FX workflows financially truthful under retries, concurrency, and partial failure.
- Separate read-only health/owner reads from persisted reconciliation commands.
- Complete the backend API contract required for later frontend work.
- Add focused PostgreSQL regressions and fresh-database certification evidence.

## Non-goals

- No React/frontend implementation.
- No Banking statement-matching or automated bank APIs.
- No duplicate Ledger/Subledger/Inventory balance truth.
- No speculative refactor or broad test-suite redesign.

## Constraints

- Work directly on `dev`.
- Ledger owns journals, CoA, posting, and period locks.
- Subledger owns AR/AP open items, statements, aging, and settlement truth.
- Inventory owns valuation.
- PostgreSQL remains the system of record with runtime RLS/`FORCE ROW LEVEL SECURITY`.
- Use the existing in-process contracts and transaction coordinator.

## Approaches

1. Add fixes inside Accounting only. Rejected where close atomicity or mapping simulation belongs to Ledger's transaction/capability boundary.
2. Rebuild financial workflows in Accounting. Rejected because it duplicates canonical truth.
3. Extend the existing ownership contracts minimally, keep Accounting as composition/orchestration, and certify with real PostgreSQL. Selected because it fixes the documented gaps while preserving the modular monolith.

## Design decisions

- Go-live approval receives a durable idempotency key and stores a payload hash; identical retries return the same approval and event, while different payloads conflict.
- Final close is a Ledger-owned transaction operation that locks the period, reruns critical readiness/reconciliation blockers, changes the canonical status, and returns the committed transition result. Accounting persists close evidence only after the canonical transition succeeds and keeps evidence status monotonic.
- Health and owner-summary reads use non-persisting source reads. Explicit reconciliation commands persist immutable runs and diagnostic issues.
- Statement mappings reject overlapping explicit/account-type coverage and return both Arabic and English labels; formulas remain constrained to named lines.
- FX posting/reversal uses deterministic per-line fact identity and an explicit recoverable `Posting`/`ReversalPending` state; retry resumes only missing lines and never duplicates posted history.
- API/OpenAPI additions follow existing module conventions and expose only capabilities that already have an owning contract.

## Success evidence

Fresh PostgreSQL migration and repeated seed, runtime-role tenant/legal-entity isolation, focused Accounting/Ledger/Subledger regressions, close/FX concurrency and failure recovery, golden statement vectors, OpenAPI contract checks, query-plan review, projection rebuild equality, `git diff --check`, clean build, one commit, and push to `origin/dev`.
