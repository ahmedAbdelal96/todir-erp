# P0-02 — Operational Baseline

**Result:** `NOT MEASURED`  
**Reason:** Docker Desktop API access was denied before the disposable harness could start.

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

## Attempt and blocker

The preflight command was:

```text
docker ps --format ...
```

It failed with:

```text
permission denied while trying to connect to the Docker API at npipe:////./pipe/dockerDesktopLinuxEngine
```

Because disposable ownership could not be positively verified, the benchmark was not retried and no database, API process, load, query-plan, or latency evidence was produced.

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

