# P0-02 — Operational Baseline

**Result:** `NOT MEASURED`  
**Reason:** Docker access was diagnosed and recovered, but canonical disposable migration/seed failed before API startup.

## Harness review

`backend/Tests/Performance/platform-performance-benchmark.ps1` was inspected in full. It is a local disposable harness that:

- creates a unique `todir_perf_*` database from `template0`;
- runs the DbMigrator against that database;
- seeds development data and creates isolated auth permissions;
- starts API instances against the disposable connection string;
- exercises authenticated reads and write-smoke paths across Projects, Sales, Invoicing, Inventory, POS, HR, Payroll and Payments;
- samples PostgreSQL/process state and records query plans under `artifacts/performance`;
- drops the generated database in `finally` unless `-KeepDatabase` is explicitly supplied.

The harness is suitable as a starting point for a safe P0 baseline. It is not an Analysis benchmark and does not prove future process isolation.

## Initial environment failure and recovery

The preflight command was:

```text
docker ps --format ...
```

It failed with:

```text
permission denied while trying to connect to the Docker API at npipe:////./pipe/dockerDesktopLinuxEngine
```

The initial sandbox could not open the Docker Desktop named pipe. Approved elevated read-only execution proved Docker Desktop was running and allowed creation of a separately labelled ephemeral PostgreSQL container. No shared development container was used for load.

## Current blocker

The canonical DbMigrator then failed on two repository migration defects: OpenIddict scope forward-fix ordering, followed by the Quality `quality_disposition` foreign-key forward-fix under `FORCE ROW LEVEL SECURITY`. See [P0-B01](P0-B01_DOCKER_ENVIRONMENT_ROOT_CAUSE.md) and [P0-B03](P0-B03_OPERATIONAL_PERFORMANCE_BASELINE.md). Production migration changes are outside P0-B scope, so no performance number was fabricated.

## Measurement ledger

| Measure | Status | Claimed value |
|---|---|---:|
| API p50/p95/p99 | `NOT MEASURED` | — |
| Core ERP request latency | `NOT MEASURED` | — |
| Analysis request latency | `NOT APPLICABLE YET` | No Analysis runtime exists |
| Throughput/concurrency | `NOT MEASURED` | — |
| PostgreSQL query plans | `NOT MEASURED` | — |
| Worker queue lag/retry | `NOT MEASURED` | — |
| Tenant/RLS runtime isolation | `NOT MEASURED` | — |
| Core-vs-analysis contention | `NOT MEASURED` | — |

The existing `5% p95` statement in the design pack remains a provisional proposal, not a measured SLO.

