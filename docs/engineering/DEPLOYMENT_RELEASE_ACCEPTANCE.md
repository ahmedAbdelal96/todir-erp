# Deployment, Upgrade, and Rollback Reliability — Local Checkpoint

Status: `BLOCKED` for final operational acceptance until the isolated OpenTofu staging plan, two-revision upgrade/rollback drill, and required scanner runs complete in a trusted local toolchain. This document does not claim production certification.

## Scope

The release boundary owns immutable API, Worker, Migrator, and Web artifacts; migration compatibility; staging deployment; application-only rollback; Worker/RabbitMQ recovery; PostgreSQL business-truth preservation; and sanitized release evidence. PostgreSQL remains authoritative. Database rollback by deletion or rewriting posted facts is forbidden.

## Corrections implemented

- Forwarded headers use the reserved staging reverse-proxy IP through `TODIR_TRUSTED_PROXY_ADDRESSES`; the API rejects malformed addresses and mutually exclusive address/CIDR configuration. Terraform assigns the same static IP only to the Web container.
- MinIO bootstrap no longer masks policy or user failures. It explicitly verifies existing policy/user state, attaches the bucket policy, and authenticates with the application identity before succeeding.
- Deployment and rollback runbooks now require commit-SHA plus digest-pinned images, dedicated Migrator execution, compatibility checks, application-only rollback, and sanitized evidence.

## Evidence after the corrections

- API Release build: `0 warnings, 0 errors`.
- Invalid proxy address and conflicting proxy configuration: rejected with the expected startup reasons.
- Disposable MinIO bootstrap: private-files access succeeded; out-of-scope bucket access and wrong application credentials were rejected.
- Existing platform gates before this deployment-only correction remain documented in [Infrastructure Security Acceptance](INFRASTRUCTURE_SECURITY_ACCEPTANCE.md): Architecture `23/23`, Security `7/7`, Seed/Migration `5/5`, RabbitMQ `2/2`.
- Terraform formatting, MinIO shell parsing, and repository release-gate controls (`73`) pass.

## Remaining mandatory gates

- OpenTofu provider initialization/validation/plan against a uniquely named disposable staging stack. The Docker Terraform image could not download the provider because the local certificate chain rejected the GitHub release endpoint; certificate verification must remain enabled.
- Immutable image build, digest capture, vulnerability scanning, and secret scanning with available supported tools. Local `tofu`, `trivy`, `gitleaks`, `syft`, and `grype` are not installed; unavailable scanners cannot be reported as passed.
- Real Version A → Version B upgrade, controlled bad-release detection, compatible application-only rollback, incompatible migration rollback refusal/forward-fix, Worker/RabbitMQ recovery, RLS/authorization smoke, and evidence retention.

Until these gates run successfully on isolated infrastructure, the exact final declaration is intentionally withheld.
