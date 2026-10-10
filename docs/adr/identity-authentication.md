# ADR-011 — Identity, Cookies and OpenID Connect Boundary

Status: Accepted

## Decision

Each account is tenant-bound for life. The kernel derives the tenant from the login domain, uses server-side secure cookie sessions, PBKDF2 password verification, MFA, security-stamp revocation and a reserved boundary for authorization-code/PKCE OIDC integration; browser tokens are never stored in local storage.

## Consequences

HTTP authorization is backed by server-side session and one tenant-bound authorization subject. An external identity-provider surface can be added without changing tenant or permission ownership; it must not introduce global identities or tenant switching.

## Evidence

`backend/Core/Identity`, `Hosting/PlatformHostingExtensions.cs`, and identity/MFA tests.
