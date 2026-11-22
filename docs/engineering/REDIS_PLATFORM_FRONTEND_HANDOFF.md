# Redis Platform Foundation Frontend Handoff

Backend/frontend consistency status: complete and locally verified for the Redis HR acceptance slice; the remaining product release and human UX approval gates are separate missions.

The frontend contract is intentionally small:

- HR employee directory and basic detail queries use TanStack Query `staleTime: 15_000` and `gcTime: 300_000`.
- HR consumers poll `/api/v1/hr/employees/freshness` every 10 seconds while a directory/detail query is mounted. A changed PostgreSQL revision invalidates the affected list/detail keys, covering mutations from another user or API instance within the bounded polling window; focus/refetch provides recovery after a background pause.
- The API remains authoritative; query freshness is a UX optimization and never replaces permission checks or PostgreSQL truth.
- Tenant, legal-entity, branch, membership, and permission changes must continue to clear or invalidate affected query keys through the existing runtime context contract. The freshness endpoint derives authoritative access scope from the authenticated context and rejects a conflicting branch query.
- This is bounded eventual freshness, not a real-time push guarantee. No SSE/WebSocket channel is introduced. Query invalidation never writes to forms or discards unsaved user input; edit/create workspaces remain separate from list/detail query state.
- Sensitive HR identifiers, documents, compensation, payroll, attendance, and leave balances are not cached by the backend and must not be treated as cacheable frontend reference data.
- FinanceBase `/api/v1/finbase/currencies` is a permission-gated stable reference read; the backend uses a one-hour bounded-staleness policy in development.

Real-stack acceptance evidence:

- Two browser users on two Vite/API instances passed the HR directory/detail freshness journey against the seeded disposable database. User A's already-open workspace updated after User B corrected the employee profile, without manual refresh; the run also covered pagination, explicit legal-entity scope, search, Arabic RTL copy, and no technical leakage.
- `PUT /api/v1/hr/employees/{id}/profile` is the canonical profile correction contract. It is versioned for optimistic concurrency and emits the same-transaction cache dependency revision consumed through Outbox → RabbitMQ → Worker → Inbox.
- Redis outage/recovery was exercised with the local container. HR requests continued through PostgreSQL fallback and both API instances recovered Redis without restart.

Verification completed: HR API contract, freshness polling, query invalidation boundaries, frontend typecheck, and real browser acceptance. Evidence is retained under `artifacts/test-results/redis-hr-real-consistency/` and is intentionally not committed.
