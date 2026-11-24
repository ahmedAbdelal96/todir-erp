# P0-05 — Security and Access Compatibility

**Result:** `STATICALLY COMPATIBLE; RUNTIME UNPROVEN`

## Existing controls observed

- Backend authorization models tenant, legal-entity and branch scopes in `backend/Core/Authorization/Authorization.cs`.
- Module controllers load tenant and membership claims, resolve an authorization subject, authorize a permission against a scoped resource, and apply module read/mutation guards.
- Entitlement snapshots are tenant-scoped and signature-verifiable in `backend/Core/Entitlements/Entitlements.cs` and `EntitlementStore.cs`.
- Database schemas use tenant columns and RLS policies; inspected policies use `FORCE ROW LEVEL SECURITY` and `app.tenant_id`.
- Frontend routes are guarded by permission and module IDs in `frontend/app/router/app-router.tsx`; destinations are registered in `frontend/app/router/destination-registry.ts` and covered by navigation permission tests.

## Compatibility rule for Analysis

Analysis must reuse the same backend authorization, entitlement, tenant, legal-entity and branch context. Frontend visibility is not an authorization boundary. Analysis evidence and recommendations must carry the source scope and must not widen visibility through a cross-module query or cached result.

## Missing proof

No runtime test was executed for cross-tenant denial, legal-entity mismatch, branch scope, expired entitlement, RLS with missing tenant context, cached-result leakage, or recommendation execution authorization. These are P1 acceptance gates, not assumptions.

