# ADR-011 — Identity, Cookies and OpenID Connect Boundary

Status: Accepted

## Decision

Identity is global and membership is tenant-scoped. The kernel uses server-side secure cookie sessions, PBKDF2 password verification, MFA, security-stamp revocation and a reserved boundary for authorization-code/PKCE OIDC integration; browser tokens are never stored in local storage.

## Consequences

HTTP authorization is backed by server-side session and membership state. An external identity-provider surface can be added without changing tenant or permission ownership.

## Evidence

`backend/Core/Identity`, `Hosting/PlatformHostingExtensions.cs`, and identity/MFA tests.
