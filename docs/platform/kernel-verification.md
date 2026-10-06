# Kernel verification record

The release gate records deterministic build, architecture and frontend boundary validation, database/RLS evidence, identity and authorization tests, module graph tests, transaction rollback tests, outbox/inbox replay tests, and operational smoke/load results. Environment-dependent gates must attach their command output and service versions; a skipped external service is not reported as passed.

## Local verification snapshot

On 2026-10-06 the solution restore/build, focused platform tests (11), shared-kernel tests (5), architecture tests (13), frontend typecheck, frontend boundary validation, manifest graph validation, database policy validation, API contract validation, API health smoke test, and `git diff --check` passed. The controlled migrator now applies all application tables under `erp_owner`; CI provisions PostgreSQL and RabbitMQ services and executes that migrator before the .NET suite.

The PostgreSQL-backed observability and security suites were not executable on the workstation: the required connection variables were absent, Docker is not installed, and the local PostgreSQL 17 service is stopped and cannot be started by the current account. These gates remain environment-blocked and are not considered passed. RabbitMQ HA, object storage, chaos/load, restore/PITR, and staging deployment evidence likewise require the approved staging environment.
