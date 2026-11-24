# Infrastructure Security — Local Hardening Acceptance

Status: local hardening scope accepted only after the final local gates below. This is not production security certification.

## Findings closed

| Finding | Severity | Correction | Local evidence |
|---|---:|---|---|
| Development PostgreSQL, RabbitMQ, and MinIO ports were published on all host interfaces | High | Compose bindings are loopback-only; staging publishes only the explicitly bound web boundary, loopback by default | `docker compose config --format json` reports `host_ip=127.0.0.1` for the internal services |
| Development service passwords were embedded in committed Compose configuration | High | Compose requires process-environment secrets; the launcher fails closed when any local administrator secret is missing or shorter than 16 characters | Compose interpolation with disposable test values passed; missing-secret validation rejected startup; repository search found no development password literals |
| Staging reused the PostgreSQL administrator password for `erp_app` | Critical | Separate `database_admin_password` and `database_runtime_password`; API/Workers receive only `erp_app` | OpenTofu variable validation rejects equal credentials; PostgreSQL role/RLS acceptance remains passing |
| Staging reused RabbitMQ and MinIO credentials | High | Separate RabbitMQ, MinIO root, and MinIO private-files credentials; MinIO app policy is bucket-scoped | Disposable MinIO policy and wrong-credential test passed; RabbitMQ wrong-credential test passed |
| API trusted every container on the staging Docker subnet as a proxy | Critical | The staging web container receives a reserved static private IP and the API trusts only `TODIR_TRUSTED_PROXY_ADDRESSES`; legacy CIDR mode is mutually exclusive and remains fail-closed on malformed input | Invalid address and conflicting-setting startup tests rejected; Terraform contract assigns the reserved proxy address |
| MinIO bootstrap masked policy/user initialization errors | Critical | Bootstrap now fails on unexpected policy or user errors, verifies existing state through the root identity, attaches the policy, and authenticates with the application identity before exiting successfully | Disposable MinIO bootstrap passed; out-of-scope bucket access and wrong application credentials were rejected |
| OpenTofu `sensitive` flags could be mistaken for encrypted state | High | State contract now explicitly requires encrypted remote state, locking, access control, and audit for shared/staging use | Runbook documents state exposure and disposable-local-state rules |
| Staging could accept mutable image tags | High | Application image inputs require the release commit SHA tag plus a trailing sha256 digest; PostgreSQL and RabbitMQ base images are digest-pinned | OpenTofu variable contract and checked-in image references; provider validation remains deployment-toolchain dependent |

## Security evidence

- PostgreSQL wrong credential from a network client: rejected.
- Runtime role: `erp_app` is non-superuser, `NOBYPASSRLS`, and cannot assume `erp_owner`.
- RabbitMQ wrong credential: rejected by the real local broker.
- Disposable MinIO: wrong credential rejected; anonymous access disabled; bucket-scoped policy created for the non-root identity.
- Development Compose credentials: PostgreSQL administrator, RabbitMQ, and MinIO passwords are supplied by `TODIR_DEV_*` environment variables only; the launcher does not print or persist them.
- Production API startup without encrypted Data Protection key configuration: failed closed.
- Invalid trusted-proxy CIDR: API startup failed closed with `TODIR_TRUSTED_PROXY_NETWORKS must contain valid CIDR networks.`
- Invalid trusted-proxy address and simultaneous address/CIDR configuration: API startup failed closed with the expected reason.
- MinIO bootstrap: existing-policy/user checks are explicit; private-files access succeeded, out-of-scope bucket access and wrong credentials failed.
- Prometheus/metrics access controls and PostgreSQL/RLS/security suites remain governed by their existing acceptance gates.

## Final local gates

- Backend Release build: `0 warnings, 0 errors`.
- Architecture suite: `23/23 passed`.
- PostgreSQL security suite: `7/7 passed` against the isolated local acceptance database.
- Development seed/migration regression: `5/5 passed` against disposable databases.
- Real RabbitMQ integration: `2/2 passed`.
- Development Compose validation, Terraform formatting, PowerShell parsing, and `git diff --check`: passed.

## Deployment dependencies and limitations

- OpenTofu CLI and provider validation must run in the deployment toolchain; the local host did not have OpenTofu, and the Docker Terraform validation image could not download the Docker provider because the local certificate chain rejected the registry download. Terraform formatting passed locally.
- `trivy`, `gitleaks`, `syft`, and `grype` were not installed locally; no unavailable scanner is represented as a pass.
- Production secret-manager integration, encrypted remote OpenTofu state, certificate issuance/rotation, managed Redis TLS/ACL, provider-specific object storage endpoint, and ingress policy remain deployment-owned prerequisites.
- Development credentials are intentionally non-production and are ignored by Git. They must never be copied into staging or production.
