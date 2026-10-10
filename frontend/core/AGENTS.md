# Platform Runtime & Data Infrastructure Governance (`frontend/core/`)

## Identity and responsibility

The `core/` directory owns cross-cutting runtime infrastructure, security boundaries, API transport, authentication/session management, authorization/permissions, capability evaluation, context switching, caching architecture, and RFC-7807 problem details error mapping.

Core principle:
> **`core/` provides shared runtime capabilities and data infrastructure. It is NOT a business module and must never depend on business modules.**

---

## 1. Absolute filename prohibition

Never create or introduce a new file or directory whose basename contains `todir` in any casing. Use descriptive kebab-case names (e.g. `http-client.ts`, `query-keys.ts`, `runtime-context.ts`).

---

## 2. Architectural rules for `core/`

1. **Zero Business Module Dependencies:** `core/` MUST NOT import anything from `frontend/modules/` (enforced by AST boundary check).
2. **Authoritative API Transport:**
   - All network requests go through `httpClient` (`core/api/http-client.ts`), which automatically attaches CSRF tokens, Correlation IDs (`X-Correlation-Id`), Idempotency keys (`Idempotency-Key`), and ETag concurrency headers (`If-Match`).
   - OpenAPI generated contracts (`core-client.ts`) are the single source of truth for platform endpoints. Never edit generated files manually; run `pnpm api:generate`.
3. **Tenant & Entity Cache Isolation:**
   - All TanStack Query keys must be generated via `queryKeyFactory` (`core/api/query-keys.ts`) with `[tenantId, legalEntityId, ...]` prefixes.
   - Tenant context switching unconditionally executes `queryClient.clear()` to prevent data leakage across security boundaries.
4. **Permissions & Capabilities:**
   - Backend authorization remains authoritative. Frontend permission and capability sets (`core/runtime/permissions.ts`, `core/runtime/capabilities.ts`) provide UI-level guidance (enabling/disabling buttons, hiding unauthorized links) but never bypass server enforcement.
5. **Standard Error Infrastructure:**
   - Standardizes parsing and handling of RFC-7807 Problem Details and machine-readable error codes.
6. **No UI Component Library:** `core/` does not export buttons, dialogs, or layout components (these belong to `components/`).
7. **No Local Business Truth:** `core/` does not calculate invoice taxes, journal balances, or stock valuation.
