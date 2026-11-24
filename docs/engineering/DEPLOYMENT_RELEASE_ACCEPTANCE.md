# Deployment, Upgrade, and Rollback Reliability — Local Checkpoint

Status: `BLOCKED` for final operational acceptance until the isolated OpenTofu staging plan, two-revision upgrade/rollback drill, and required scanner runs complete in a trusted local toolchain. This document does not claim production certification.

## Scope

The release boundary owns immutable API, Worker, Migrator, and Web artifacts; migration compatibility; staging deployment; application-only rollback; Worker/RabbitMQ recovery; PostgreSQL business-truth preservation; and sanitized release evidence. PostgreSQL remains authoritative. Database rollback by deletion or rewriting posted facts is forbidden.

## Corrections implemented

- Forwarded headers use the reserved staging reverse-proxy IP through `TODIR_TRUSTED_PROXY_ADDRESSES`; the API rejects malformed addresses and mutually exclusive address/CIDR configuration. Terraform assigns the same static IP only to the Web container.
- MinIO bootstrap no longer masks policy or user failures. It explicitly verifies existing policy/user state, attaches the bucket policy, and authenticates with the application identity before succeeding.
- API and Worker image restore stages now copy the complete backend project graph before restore, so newly referenced modules cannot be omitted by a stale hand-maintained `COPY` list.
- The DbMigrator image now packages the complete canonical Database directory and every Core/Modules manifest discovered by the migrator. Its runtime packaging shell uses native `/bin/sh` command substitution and shell variables. The Web image runs as the unprivileged `nginx` user with a writable temporary PID path and stdout/stderr logging.
- Docker context rules explicitly exclude development settings, nested environment files, local certificates/private keys, secret directories, and local secret JSON files; they continue to admit the non-secret settings file and canonical migration scripts.
- Deployment and rollback runbooks now require commit-SHA plus digest-pinned images, dedicated Migrator execution, compatibility checks, application-only rollback, and sanitized evidence.

## Evidence after the corrections

- API Release build: `0 warnings, 0 errors`.
- Invalid proxy address and conflicting proxy configuration: rejected with the expected startup reasons.
- Disposable MinIO bootstrap: private-files access succeeded; out-of-scope bucket access and wrong application credentials were rejected.
- Existing platform gates before this deployment-only correction remain documented in [Infrastructure Security Acceptance](INFRASTRUCTURE_SECURITY_ACCEPTANCE.md): Architecture `23/23`, Security `7/7`, Seed/Migration `5/5`, RabbitMQ `2/2`.
- Terraform formatting, MinIO shell parsing, and repository release-gate controls (`73`) pass.
- Docker BuildKit syntax checks for API, Worker, DbMigrator, and Web pass with no warnings. Full immutable image builds remain unverified because the local Docker disk has only about 0.20 GB free; the task-owned API build attempt was stopped before completion and its build cache was removed. No release image was produced or accepted.
- The DbMigrator packaging command was executed in a real Linux `/bin/sh` container against the repository: all `25` module manifests and all `260` SQL migration files were copied into the exact runtime paths, including the Core manifest and canonical bootstrap scripts. A Docker context sentinel build also passed: development settings, nested env files, certificate/key extensions, secret directories, and `*-secrets.json` were absent while `backend/appsettings.json` and migration files remained present. The sentinel image was removed.
- Trust diagnosis is now precise: the host reaches `registry.terraform.io` with a verified `200` response, but the direct GitHub checksum endpoint fails Windows Schannel with `SEC_E_UNTRUSTED_ROOT`; Terraform in Docker fails the same provider checksum request with `x509: certificate signed by unknown authority`. No proxy variables were present in the host or Terraform container. Completion therefore requires an administrator-approved CA chain in the host and Docker/BuildKit trust store, or a trusted alternate validation environment; TLS verification must remain enabled.

## Remaining mandatory gates

- OpenTofu provider initialization/validation/plan against a uniquely named disposable staging stack. The Docker Terraform image could not download the provider because the local certificate chain rejected the GitHub release endpoint; certificate verification must remain enabled.
- Immutable image build, digest capture, vulnerability scanning, and secret scanning with available supported tools. Local `tofu`, `trivy`, `gitleaks`, `syft`, and `grype` are not installed; unavailable scanners cannot be reported as passed. The image build also requires additional disposable Docker disk before it can be verified.
- A read-only inventory recorded Docker Desktop storage on `D:` with approximately `0.18 GB` free, eight local base/tool images, three active shared development containers (`todir-postgres-1`, `todir-rabbitmq-1`, `todir-redis-1`), and named volumes including the shared TODIR and monitoring volumes. No unowned image, volume, or container was removed; the task-created sentinel images were removed. The remaining Docker build cache is not safely attributable for broad deletion, so additional Docker disk is required.
- Host source-level high-confidence secret-signature scan found no private-key, AWS-key, GitHub-token, or Slack-token signatures; this is supplementary evidence only and does not replace the unavailable pinned scanners. No scanner image was pulled.
- Real Version A → Version B upgrade, controlled bad-release detection, compatible application-only rollback, incompatible migration rollback refusal/forward-fix, Worker/RabbitMQ recovery, RLS/authorization smoke, and evidence retention.

Until these gates run successfully on isolated infrastructure, the exact final declaration is intentionally withheld.
