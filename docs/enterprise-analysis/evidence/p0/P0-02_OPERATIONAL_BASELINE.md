# P0-02 — Operational Baseline

**Result:** `P0_EVIDENCE_COMPLETE — LOCAL BASELINE`  
**Boundary:** The baseline is valid for the current Development Seed and disposable single-host environment; it is not production capacity certification.

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

## Current verified state

The latest repository corrections are present and the canonical path now completes without `-PreMigrationSql`. OpenIddict forward-fix ordering is explicit in `backend/DbMigrator/Program.cs`; the Quality disposition-restriction migration is handled through a transaction-scoped maintenance RLS override that restores `FORCE ROW LEVEL SECURITY`. See [P0-B03](P0-B03_OPERATIONAL_PERFORMANCE_BASELINE.md) and [P0-B07](P0-B07_INDEPENDENT_FINAL_REVIEW.md).

## Measurement ledger

| Measure | Status | Claimed value |
|---|---|---:|
| API p50/p95/p99 | `MEASURED` | Diagnostic p50 only; load p50/p95/p99 recorded in P0-B03 |
| Core ERP request latency | `MEASURED` | Local Development Seed baseline; not production SLO |
| Analysis request latency | `NOT APPLICABLE YET` | No Analysis runtime exists |
| Throughput/concurrency | `MEASURED` | Load at concurrency 1, 4 and 8, repeated |
| PostgreSQL query plans | `MEASURED` | Four bounded seeded-data plans per run |
| Worker queue lag/retry | `NOT EXECUTED` | Outside this API baseline |
| Tenant/RLS runtime isolation | `PASS` | Harness 401/403 checks plus SecurityTests and SeedDataTests |
| Core-vs-analysis contention | `NOT MEASURED` | — |

The existing `5% p95` statement in the design pack remains a provisional proposal, not a measured SLO.

