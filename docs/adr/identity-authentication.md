# ADR-011 — Identity, Cookies and OpenID Connect Boundary

Status: Accepted

## Decision

Each account is tenant-bound for life. The kernel derives the tenant from the login domain, uses a PostgreSQL-backed server-side secure cookie session registry with bounded inactivity and reauthentication deadlines, PBKDF2 password verification, MFA, security-stamp revocation and a reserved boundary for authorization-code/PKCE OIDC integration; browser tokens are never stored in local storage. The detailed session and OAuth contract is maintained in `docs/architecture/TODIR_IDENTITY_SESSION_OAUTH_SECURITY_ARCHITECTURE.md`.

## Consequences

HTTP authorization is backed by server-side session and one tenant-bound authorization subject. An external identity-provider surface can be added without changing tenant or permission ownership; it must not introduce global identities or tenant switching.

## Evidence

`backend/Core/Identity`, `Hosting/PlatformHostingExtensions.cs`, `backend/DbMigrator/Database/bootstrap/platform-kernel-forward-fix-web-sessions.sql`, and identity/MFA tests. Native mobile OAuth/OIDC remains a separately identified implementation gate.
