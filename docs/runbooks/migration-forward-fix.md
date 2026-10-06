# Migration forward-fix

1. Stop application rollout while keeping the migrator evidence and failed statement.
2. Reproduce the failure on an isolated database restore.
3. Add an idempotent expand/contract migration owned by the migration role; preserve existing source-of-truth and RLS policies. Never edit an applied script: the migrator records a SHA-256 checksum in `platform_migration.applied_script` and rejects checksum drift.
4. Declare the owning module and hard dependencies in its `module.manifest.json`, place its migration at the manifest-resolved path, and let the migrator apply the resulting topological order.
5. Run the migrator, database policy tests, tenant-isolation tests, and affected module tests.
6. Deploy the compatible application image, backfill in bounded batches, verify constraints and query plans, then remove obsolete columns only in a later approved migration.
