# Migration forward-fix

1. Stop application rollout while keeping the migrator evidence and failed statement.
2. Reproduce the failure on an isolated database restore.
3. Add an idempotent expand/contract migration owned by the migration role; preserve existing source-of-truth and RLS policies.
4. Run the migrator, database policy tests, tenant-isolation tests, and affected module tests.
5. Deploy the compatible application image, backfill in bounded batches, verify constraints and query plans, then remove obsolete columns only in a later approved migration.
