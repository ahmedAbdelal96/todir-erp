# Security engineering

Never commit credentials, tokens, keys, connection strings, production configuration, or sensitive data. Never log secrets or sensitive plaintext. Tenant isolation is security-critical and backend authorization is authoritative.

Do not hide vulnerabilities: raise security-sensitive findings for focused review and preserve evidence safely. Production credentials never belong in this repository. Canonical security documentation belongs in `docs/security/`; security-impacting implementation must follow the applicable architecture and module plan.
