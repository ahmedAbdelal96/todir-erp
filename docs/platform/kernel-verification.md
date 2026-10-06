# Kernel verification record

The release gate records deterministic build, architecture and frontend boundary validation, database/RLS evidence, identity and authorization tests, module graph tests, transaction rollback tests, outbox/inbox replay tests, and operational smoke/load results. Environment-dependent gates must attach their command output and service versions; a skipped external service is not reported as passed.

## Local verification snapshot

On 2026-10-06 the solution build (0 warnings, 0 errors), focused platform tests (14), shared-kernel tests (5), architecture tests (13), frontend typecheck/build, frontend boundary validation, manifest graph validation, database policy validation, API contract validation, and `git diff --check` passed. The controlled migrator applies all application and Quartz scheduler tables under `erp_owner`; CI provisions PostgreSQL and RabbitMQ services and executes that migrator before the .NET suite. CI also builds SHA-tagged API, worker, migrator, and web images, scans them, validates OpenTofu, creates an isolated Docker staging stack, and runs API/web health smoke checks.

The PostgreSQL-backed observability, database security, RabbitMQ, and tenant-isolation suites were not executable on the workstation: the required connection variables were absent, Docker is not installed, and the local PostgreSQL 17 service is stopped and cannot be started by the current account. These gates remain environment-blocked and are not considered passed locally. RabbitMQ HA, object storage, chaos/load, restore/PITR, and staging deployment evidence require the approved staging environment; the CI workflow now provisions the required PostgreSQL/RabbitMQ services and isolated staging checks.
