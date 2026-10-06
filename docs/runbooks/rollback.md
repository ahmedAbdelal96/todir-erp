# Rollback

Stop promotion, preserve logs and correlation IDs, and roll back API, worker, and web to the last known-good immutable image digests. Do not roll back a database migration by deleting or rewriting posted data. If the schema change is incompatible, use the documented forward-fix migration, then repeat smoke and authorization checks.
