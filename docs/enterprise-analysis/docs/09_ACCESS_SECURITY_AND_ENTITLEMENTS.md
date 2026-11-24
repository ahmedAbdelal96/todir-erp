# 09 — Authorization, Tenant Security, Entitlements and Privacy

**Governing source:** existing ToDir `docs/platform/authorization.md`, `entitlements.md`, `rls.md`, Master Blueprint and Navigation ADR. **Rule:** do not create a separate AI admin/purchasing/role/entitlement system.

## 1. Scope and permission model

Effective access = authenticated identity + selected tenant + permitted legal entities + permitted branches/records + current role permissions + module entitlement + activation/read-only mode + data classification. Server independently checks every API call, worker extraction, result/evidence fetch, review and action handoff. UI hide/show is not an authorization decision.

**Proposed permission vocabulary** (not existing in repo; exact codes subject to manifest design):

- `sales.analysis.read`, `sales.analysis.run`, `sales.analysis.recommendation.read`, `sales.analysis.recommendation.review`
- `subledger.collections_analysis.read`, `.run`, `.recommendation.read`, `.recommendation.review` or a consistent manifest-approved alternative
- `inventory.analysis.read`, `.run` ... as needed per justified use case
- Sensitive result/evidence policy may require existing financial `audit.read`/`reports.read` or explicit data permission (do not infer privilege from generic `analysis.read`).

**Distinct capabilities**, no implied permission hierarchy: read history ≠ create analysis ≠ review recommendation ≠ execute business action. A `run`-only role needs a documented policy for viewing **its own created run** or add explicit `readOwn`; do not silently grant all read or expose another manager's results. Parent navigation visibility derives from permitted child destinations, not unrelated module overview permission.

## 2. End-to-end checks

- Request `POST`: server resolves authenticated actor/tenant, current entity and branch from trusted context; rejects cross-scope IDs, unauthorized dimensions, overly broad branch lists, hidden datasets.
- Worker: `tenant_id` in persisted job belongs to system-authored request, cannot be overridden by payload or selected result. Worker applies transaction-local RLS per scope and delegated authorization, use minimal narrow dispatcher for cross-tenant job claiming.
- Result `GET`: **re-evaluate current permissions**; user may lose access after creating run. Evidence links are also scoped by current owner permissions and data sensitivity.
- Recommendation review: role + entity + record scope + optimistic concurrency, mandatory audit actor/correlation; review does not grant original module command permission.
- Tenant switch: clear stale caches, pending network response surfaces, filters and selected run; late previous-tenant responses must never rehydrate UI.
- Legal entity deactivation: respect existing read-only history and entitlement policy; block new runs if module not operational/entitled, unless explicitly approved.

## 3. RLS and DB roles

- `erp_app` remains nonowner, no BYPASSRLS. Every new tenant-owned table uses FORCE RLS with `tenant_id` and composite tenant-sensitive keys.
- Job dispatcher requiring cross-tenant queue search uses **narrow security-definer** wrapper with fixed claims only (to evaluate against existing outbox dispatcher pattern), never unbounded query granted to normal app.
- Runtime credentials for Analysis Worker distinct where useful, least privilege for read/write of **its own** schema; cross-owner source projection read via approved owner contracts only.
- Set `app.tenant_id` as `SET LOCAL` in transaction; verify pooled connection switching tenants under malicious and accidental contexts. Stronger legal-entity/branch enforcement is required on top of tenant RLS.
- Do not send tenant private financial or payroll facts to external AI providers without separate consent, entitlement, retention, legal and data processor assessment.

## 4. Entitlement options — proposed ADR, not a new system

- A module's analysis route is available only if its source module is accessible and the tenant's effective **analysis add-on entitlement**, if commercially adopted, is active.
- Whether analysis is sold as separate add-on, included in tier or limited by monthly quota is **commercial policy to decide**. Implementation must use the **existing entitlement/lifecycle** infrastructure; no independent subscription state.
- Quotas, job size and retention should be enforced server-side and auditable, not just UI. Denied requests must not enqueue work or reveal data.

## 5. Data minimization and sensitive fields

- Analysis results should preferentially store aggregated amounts + scoped opaque references, not replicate bank statements, full customer contacts, employees' salaries, ID numbers or payment processor tokens.
- Salary and employee-level analysis requires separate HR/Payroll role-specific policy; do not expose individual pay through owner-wide generic dashboard.
- Preserve finance/audit traceability with permission-scoped evidence; hide record links/PII for viewers lacking source record access.
- Encrypt stored data and artifact links where required, audit download/external export, signed short-lived links scoped to current access.
- `model input`, debug logs, tracing, error payloads and events must not leak secrets or financial row payloads.
- External providers, consent and data residency are out of initial deterministic offline-only scope until product/legal review.

## 6. Threat scenarios and acceptance tests

| Scenario | Expected outcome |
|---|---|
| Tenant A passes Tenant B run UUID in GET | 404/403 without information disclosure |
| Same user switches branch/entity mid-fetch | Old data cleared; late responses ignored; new permissions enforced |
| User has sales read, not analysis run | No POST execution; allowed operational work unaffected |
| User has analysis run, not sensitive financial report | Data extractor omits/denies sensitive facts; no privilege escalation |
| Role revoked after queued job | Defined cancel/fail-safe policy; cannot view former restricted output |
| Malformed JSON filters contain SQL-like tokens | Typed validation; no dynamic SQL execution |
| Guessing evidence ref ID across another legal entity | Owner capability enforces record scope |
| Worker connection reused across two tenants | `SET LOCAL` scope verified; no cross-tenant rows |
| Unauthorized LLM prompt asks to run command | No command capability/SQL access; only approved read/description if enabled later |
| Module entitlement expires with history present | New runs blocked; old run access per explicit existing read-only policy |

## 7. Security release gate

Required: automated backend 401/403/404 matrix per route+action+role, PostgreSQL RLS tests with real `erp_app` role and transaction pool, authorization change during run, mixed-tenant worker stress, artifact access controls, audit correlation, no leaked data in logs, and proven fail-closed frontend access. Security green in mocked tests is **insufficient**.
