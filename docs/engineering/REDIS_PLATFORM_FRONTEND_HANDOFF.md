# Redis Platform Foundation Frontend Handoff

Backend status: ready for frontend consumption.

The frontend contract is intentionally small:

- HR employee directory and basic detail queries use TanStack Query `staleTime: 15_000` and `gcTime: 300_000`.
- HR consumers poll `/api/v1/hr/employees/freshness` every 10 seconds while a directory/detail query is mounted. A changed PostgreSQL revision invalidates the affected list/detail keys, covering mutations from another user or API instance within the bounded polling window; focus/refetch provides recovery after a background pause.
- The API remains authoritative; query freshness is a UX optimization and never replaces permission checks or PostgreSQL truth.
- Tenant, legal-entity, branch, membership, and permission changes must continue to clear or invalidate affected query keys through the existing runtime context contract. The freshness endpoint derives authoritative access scope from the authenticated context and rejects a conflicting branch query.
- This is bounded eventual freshness, not a real-time push guarantee. No SSE/WebSocket channel is introduced. Query invalidation never writes to forms or discards unsaved user input; edit/create workspaces remain separate from list/detail query state.
- Sensitive HR identifiers, documents, compensation, payroll, attendance, and leave balances are not cached by the backend and must not be treated as cacheable frontend reference data.
- FinanceBase `/api/v1/finbase/currencies` is a permission-gated stable reference read; the backend uses a one-hour bounded-staleness policy in development.

Verification completed for this backend handoff: HR API contract, freshness polling source review, and query invalidation boundaries. Frontend typecheck/Vite/UX acceptance remains part of the separate frontend mission and must be rerun after frontend integration changes.
