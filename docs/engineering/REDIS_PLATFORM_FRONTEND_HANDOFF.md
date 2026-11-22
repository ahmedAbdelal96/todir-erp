# Redis Platform Foundation Frontend Handoff

Backend status: ready for frontend consumption.

The frontend contract is intentionally small:

- HR employee directory and basic detail queries use TanStack Query `staleTime: 30_000` and `gcTime: 300_000`.
- The API remains authoritative; query freshness is a UX optimization and never replaces permission checks or PostgreSQL truth.
- Tenant, legal-entity, membership, and permission changes must continue to clear or invalidate affected query keys through the existing runtime context contract.
- Sensitive HR identifiers, documents, compensation, payroll, attendance, and leave balances are not cached by the backend and must not be treated as cacheable frontend reference data.
- FinanceBase `/api/v1/finbase/currencies` is a permission-gated stable reference read; the backend uses a one-hour bounded-staleness policy in development.

Verification completed: frontend typecheck, Vite production build, frontend architecture boundaries, frontend shell smoke, and HR query freshness source review. Product/UX acceptance remains part of the separate frontend mission.
