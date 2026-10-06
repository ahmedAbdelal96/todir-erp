# Operations

## Absolute filename and folder-name prohibition

Never create or introduce a new operational file or folder whose basename contains `todir` in any casing. This is absolute; operational paths must use responsibility-based names.

`ops/` contains reproducible, reviewable operational infrastructure—not application domain logic. Use `containers/`, `compose/`, `terraform/`, `database/`, `rabbitmq/`, and `monitoring/` by their stated purpose.

`ops/database/` is for approved deployment, administration, backup/restore, role/bootstrap and infrastructure assets; application persistence belongs inside the owning backend module's `Infrastructure/Persistence` area. Never commit secrets or environment-specific production credentials. Operational changes must preserve security, recoverability and documented runbook impact.
