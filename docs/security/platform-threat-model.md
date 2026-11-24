# Platform threat model

High-risk assets are tenant data, credentials, sessions, object URLs, messages, audit evidence, and migration privileges. This review covers the P26 exit-gate threats and points to the executable control that must remain in the release gate.

| Threat | Implemented mitigation | Evidence |
|---|---|---|
| Cross-tenant leakage | `app.tenant_id` is transaction-local; tenant tables use `FORCE ROW LEVEL SECURITY`; runtime `erp_app` is non-superuser and `NOBYPASSRLS`; dispatcher access is limited to fixed-parameter security-definer functions. | PostgreSQL role/RLS integration suite and restore verification query. |
| Privilege escalation | Backend permission evaluation requires the tenant membership subject and tenant/entity/branch scope; hidden UI state is not an authorization boundary. | Authorization integration tests and authenticated staging permission drill. |
| SSRF through connectors/files | The kernel has no arbitrary outbound connector surface; file object keys are generated from trusted IDs, original names are rejected as path components, and signed access is tenant/status/expiry bound. | File policy and cross-tenant signed URL tests. |
| Object-storage URL leakage | MinIO is private to the staging network; the `private-files` bucket is initialized with anonymous access disabled; download signatures are short-lived and HMAC-bound to tenant, object key, and file ID. | Staging bucket-init assertion and file signer tests. |
| Message forgery/replay | RabbitMQ staging uses TLS and pinned certificate validation; publisher confirmations are enabled; stable endpoint + message ID inbox claims use leases and processed-state idempotency. | RabbitMQ staging drill, worker crash-after-commit drill, and inbox tests. |
| Broken authorization/replay | Secure cookies, CSRF header validation, IP auth rate limits, membership/session-stamp validation, MFA recovery-code consumption, and session revocation are enforced server-side. | API shell smoke, identity/MFA tests, and staging auth/rate-limit drill. |
| Support abuse | Support grants are tenant-bound, reasoned, approval-gated, MFA step-up-bound, time-limited, revocable, and audited in the same persistence transaction. | Support workflow unit/integration tests and support-access runbook. |
| Secret exposure | Passwords are PBKDF2 hashes; MFA secrets are Data Protection protected; keys are persisted and certificate-encrypted outside development; secrets are masked in CI and never written to source. | Dependency/image/secret scans and staging secret generation. |
| Service-credential crossover | Staging separates the PostgreSQL administrator, `erp_app`, RabbitMQ, MinIO root, and MinIO bucket identities. The runtime containers receive no migration administrator credential. | OpenTofu variable validation, container environment review, and PostgreSQL role/RLS suite. |
| Host-network exposure | Development PostgreSQL, RabbitMQ, RabbitMQ management, and MinIO bindings are loopback-only; staging uses a private Docker network and loopback-only web binding by default. | `docker compose config` host-IP inspection and Docker port inspection. |
| Forwarded-header spoofing | API trusts `X-Forwarded-*` only from the explicitly configured private Docker CIDR; without that setting it does not trust forwarded headers. | API startup configuration review and staging reverse-proxy contract. |
| Infrastructure-state disclosure | Sensitive OpenTofu variables are marked sensitive, but provider state still contains container environment/command values; shared staging requires encrypted remote state with locking and audit. | Terraform/OpenTofu state contract in the staging runbook; local state is disposable only. |

The migration role is the only schema writer. Audit rows are append-only to the runtime role, and error/audit writes establish tenant context before touching FORCE-RLS tables. No claim of production RPO/RTO is made until a real restore drill records measurements.
