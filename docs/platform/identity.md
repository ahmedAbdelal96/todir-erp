# Identity and sessions

Identity is global; membership is tenant-scoped; Employee belongs to HR and is not part of the Kernel. Browser sessions use secure, HttpOnly, SameSite cookies. Passwords use PBKDF2 with a per-password salt, and session revocation is checked before authorization. OAuth/OIDC integrations use authorization code with PKCE; access tokens are never stored in browser local storage.
