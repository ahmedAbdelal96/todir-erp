# Platform threat model

High-risk assets are tenant data, credentials, sessions, object URLs, messages, audit evidence, and migration privileges. Required controls cover cross-tenant access, role escalation, replay/forgery, SSRF, object-store leakage, support abuse, brute force, and secret exposure. Runtime roles cannot alter schema or bypass RLS; logs and audit redact credentials and tokens.
