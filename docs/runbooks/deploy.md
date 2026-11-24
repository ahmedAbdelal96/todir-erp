# Deploy

Deployment is promotion of one immutable release, not a rebuild on the target host. Run from a clean checkout of `dev` at the selected full commit SHA; do not use the shared development containers as a staging target.

```powershell
$release = (git rev-parse HEAD).Trim()
docker build --tag "kernel-api:$release" --file ops/containers/api.Dockerfile .
docker build --tag "kernel-worker:$release" --file ops/containers/worker.Dockerfile .
docker build --tag "kernel-migrator:$release" --file ops/containers/database-migrator.Dockerfile .
docker build --tag "kernel-web:$release" --file ops/containers/web.Dockerfile .
docker image inspect "kernel-api:$release" "kernel-worker:$release" "kernel-migrator:$release" "kernel-web:$release" --format '{{.Id}} {{.RepoTags}}'
```

1. Run the Release build, architecture/security/integration gates, secret scan, and image vulnerability scan. A missing scanner is a failed prerequisite, not a pass.
2. Push each image to the approved registry and record the returned `RepoDigests`. Pass `repo:commit-sha@sha256:digest` references to OpenTofu; tag-only or `latest` references are rejected.
3. Run `tofu init`, `tofu validate`, and `tofu plan` using the approved encrypted remote state backend. Keep the plan and state out of Git and evidence attachments. Review the plan for the unique staging project, reserved reverse-proxy IP, private service network, digest-pinned images, and credential separation before applying it.
4. Apply the plan. The dedicated Migrator is the only component allowed to change the database schema; API and Worker must receive only runtime credentials.
5. Verify `/health`, `/health/ready`, the web shell, database connectivity, worker readiness, authenticated authorization boundaries, TLS/security headers, MinIO private-bucket bootstrap, RabbitMQ recovery, Outbox/Inbox idempotency, and the API contract before promotion.
6. Record commit, image digests, migration result, smoke results, failure details, and operator decision in sanitized release evidence. Never record passwords, keys, tokens, private certificates, or raw plan/state files.
