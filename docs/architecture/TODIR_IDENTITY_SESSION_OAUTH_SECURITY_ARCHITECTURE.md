# TODIR Identity, Web Session, Mobile OAuth and Token Security Architecture

Status: Web-session, native OAuth/OIDC, and local critical identity-correction evidence implemented; production certificate rotation and external deployment acceptance remain delivery gates.

## Scope and trust boundaries

The Platform Kernel owns identity, tenant-bound membership, web sessions, MFA credentials, CSRF protection, authentication schemes, and authorization subjects. PostgreSQL is authoritative for session validity and revocation. Browser clients receive only an `HttpOnly`, `Secure`, `SameSite=Strict` cookie; they never receive or persist a bearer access token.

Tenant, legal-entity, branch, membership, device, and future OAuth-client scope are server-derived and independently validated. A client must not select a tenant by header or by token claim supplied by the browser.

## Web session contract

`platform.web_session` is the authoritative cross-instance registry. A cookie is valid only when its session identifier, identity, tenant, security stamp, active membership, and active identity match a live row under PostgreSQL RLS. Validation fails closed when the registry is unavailable. The ASP.NET cookie middleware does not use sliding expiration.

The default policy is:

| Rule | Default |
| --- | ---: |
| Inactivity timeout | 30 minutes |
| Reauthentication deadline | 24 hours |
| Activity write interval | 60 seconds |
| Fresh MFA proof | 5 minutes |
| Warning window | 2 minutes before inactivity expiry |

Only meaningful browser activity (pointer, keyboard, touch, focus, or an explicit Continue action) can request a bounded activity write. Query polling, background refreshes, and rendering do not extend a session. Activity cannot move the inactivity expiry beyond the reauthentication deadline. Logout and global revocation mark the authoritative row unusable; no process-local revocation list is trusted for production sessions.

## Cookie and bearer separation

The browser ERP uses the cookie scheme and CSRF protection for state-changing requests. Future OAuth resource endpoints must use a separate bearer scheme and must not inherit browser-cookie authorization implicitly. CSRF is required for cookie-authenticated mutations and is not a substitute for bearer token validation.

## Native mobile OAuth/OIDC contract

The approved protocol is OpenIddict Authorization Code + PKCE with `S256`, public mobile clients, system-browser authorization, exact pre-registered redirect URIs, state and nonce validation, one-use authorization codes, short-lived access tokens, rotating refresh-token families, family reuse detection, revocation, discovery metadata, and published signing keys. Password Grant, Implicit Grant, embedded WebViews, and mobile client secrets are prohibited.

The durable OpenIddict application/authorization/token store is owned by PostgreSQL under `platform`, and the authorization, token, revocation, introspection, discovery, and JWKS endpoints are enabled. The seeded native public client uses an exact custom redirect URI. Server configuration exposes only `S256`, enforces PKCE, issues five-minute access tokens, and uses rolling refresh tokens with zero reuse leeway so reuse invalidates the family. Each authorization creates a durable `platform.mobile_session`; mobile bearer claims carry only that mobile session identity, never the browser `session_id`. Browser inactivity or browser logout therefore does not revoke an unrelated mobile session, while the authoritative identity security stamp, account status, membership status, and explicit global revocation invalidate both. Bearer validation uses the local OpenIddict server and then revalidates the PostgreSQL mobile session/security stamp on each authorized API request. A configured web login URI provides the system-browser bridge; the frontend returns only to a same-origin API `/connect/authorize` request after web authentication.

Resource scope enforcement is centralized at the API boundary. Cookie-authenticated web requests continue through the ERP permission, tenant, legal-entity, branch, entitlement, lifecycle, RLS, and CSRF controls. A valid bearer request must also carry `tenant.read` for reads or `tenant.write` for mutations; `tenant.write` is a superset for read operations. A scope never grants an ERP permission, and missing or unrecognized resource scopes fail closed before a module handler runs. OpenIddict application permissions and the registered resource scopes are explicit; there is no implicit unrestricted API scope.

Authorization freshness is fail-closed. `PermissionAuthorizer` evaluates the tenant-bound subject loaded from PostgreSQL for the current request and does not retain process-wide allow/deny decisions. A grant requires one and the same role to be owned by the tenant, assigned to the current membership, active, applicable to the exact requested scope, and explicitly contain the requested permission; active permissions from a different role never activate an inactive role's grant. Role assignments, active-role state, wildcard handling, module lifecycle, and revocation therefore cannot be bypassed by a stale decision cache. The `/authorization/permissions` and `/authorization/effective-access` projections use the same active roles and current tenant/legal-entity/branch scope, and exclude inactive-role and unauthorized wildcard entries; frontend permission checks remain advisory and never replace backend authorization.

Development uses OpenIddict development certificates. Non-development startup fails closed unless both encrypted signing and encryption PKCS#12 certificates with private keys are supplied through the explicit certificate settings. A document-only OAuth surface or an in-memory token test is not acceptance evidence.

Future external API access is similarly bounded to registered clients, explicit tenant/scope grants, security-stamp/account-status checks, and auditable revocation. One bounded machine-to-machine client is a future acceptance fixture, not a browser session substitute.

## Sensitive operations and MFA

MFA verification is recorded with the server-side session and must be fresh for sensitive actions. Recovery codes are one-use digests. Enrollment is staged in a short-lived, token-hashed server challenge and only becomes active after a confirmed TOTP; replacement and other sensitive operations require a fresh server-side proof. No-MFA accounts do not satisfy a sensitive-operation MFA requirement, and frontend claims are not trusted.

## Acceptance matrix

| Capability | Current evidence | Status |
| --- | --- | --- |
| PostgreSQL-backed session registry, security-stamp comparison, and RLS | Migration, Npgsql service, focused unit coverage, real HTTP old-cookie rejection after identity stamp change | PASS locally |
| 30m inactivity / 24h reauth / bounded activity | Session policy and deterministic clock tests | PASS locally |
| Cross-instance fail-closed validation | Authoritative row validation path; shared PostgreSQL session authority | PASS by design; multi-process runtime evidence remains a deployment gate |
| Cookie/CSRF browser boundary | Actual cookie/bearer authentication result is used; forged bearer headers do not bypass CSRF | PASS locally |
| Bearer resource scopes | Central tenant.read/tenant.write boundary before API handlers, with ERP permissions still authoritative | PASS locally; external-client scope catalog remains a deployment gate |
| Authorization freshness and effective-access projection | PostgreSQL subject loaded per request; same active scoped role must own each permission; inactive roles and raw wildcard entries do not grant concrete permissions | PASS locally |
| Session warning and Continue UX | React activity hook, i18n, dialog | PASS by typecheck/build; real browser proof pending |
| Native mobile OAuth/OIDC | Durable store, discovery/JWKS, code+PKCE HTTP flow, exact redirect, dedicated mobile session, and system-browser bridge | PASS locally; production/mobile-client release proof pending |
| Refresh rotation, reuse detection, discovery/signing keys | Five-minute access tokens, rolling refresh family, zero-leeway reuse rejection, development/prod certificate fail-closed rules | PASS locally; production certificate rotation evidence pending |
| Fresh MFA enforcement on sensitive operations | Server-side five-minute MFA proof, staged enrollment confirmation, password/session/admin-reset/login-domain/replay guards | PASS for guarded platform operations; broader module-specific high-risk command audit remains a separate gate |

The truthful delivery status remains **NOT COMPLETE** for production/mobile release until browser/mobile-client acceptance and production certificate rotation evidence are captured. The local backend foundation has repeatable HTTP evidence; this document does not claim production certification.
