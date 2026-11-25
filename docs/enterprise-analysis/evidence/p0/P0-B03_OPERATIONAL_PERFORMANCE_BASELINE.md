# P0-B03 — Operational Performance Baseline

**Result:** `MEASURED — LOCAL BASELINE COMPLETE`  
**Certification boundary:** Development Seed and a disposable single-host PostgreSQL/API environment only. These results are not production-capacity, autoscaling, multi-region, or mixed-ERP certification.

## Canonical migration and seed gate

The hardened harness ran the canonical DbMigrator and Development Seed against a new `postgres:18` container without `-PreMigrationSql`. The resulting dataset was verified as:

```text
tenants=2, legalEntities=4, branches=4, parties=300,
openItems=142, productVariants=446, projects=8
```

The fresh run completed migration, seed, API startup, database identity proof, functional smoke, write smoke, security smoke, query-plan collection and cleanup. The repository corrections are present in `backend/DbMigrator/Program.cs` and `quality-forward-fix-disposition-restriction.sql`: OpenIddict is ordered before the OAuth scope fix, and the Quality maintenance migration temporarily relaxes and restores `FORCE ROW LEVEL SECURITY` transactionally.

## Measurement runs

| Run | Mode | Samples | Result | Artifact |
|---|---|---:|---|---|
| `20261125090812_10848_4c9d0e78` | diagnostic | 42 read result rows, 5 measured requests per row | PASS; p95/p99 explicitly insufficient at this sample size | `artifacts/performance/p0b-20261125-diagnostic/platform-performance-todir_perf_20261125090812_10848_4c9d0e78.json` |
| `20261125091019_41032_6b11ec83` | load | 6,300 measured reads + 300 sustained writes | PASS; 0 measured request errors | `artifacts/performance/p0b-20261125-load/platform-performance-todir_perf_20261125091019_41032_6b11ec83.json` |
| `20261125091708_30864_85500d9a` | load repeat | 6,300 measured reads + 300 sustained writes | PASS; 0 measured request errors | `artifacts/performance/p0b-20261125-load-repeat/platform-performance-todir_perf_20261125091708_30864_85500d9a.json` |

Both load runs used `RequestsPerEndpoint=100`, concurrency `1,4,8`, warm-up requests, sustained draft writes, and the harness multi-instance stage. Every load run reported `21/21` functional smoke, `3/3` security smoke, and `8/8` write smoke checks passing.

## Latency and throughput

The ranges below span the individual endpoint/tenant/concurrency result rows; they are not a single aggregate SLO.

| Run | p50 range | p95 range | p99 range | RPS range | Error rate |
|---|---:|---:|---:|---:|---:|
| Diagnostic | 21.76–186.41 ms | `INSUFFICIENT_SAMPLES` | `INSUFFICIENT_SAMPLES` | 6.94–47.02 | 0% |
| Load | 22.95–184.06 ms | 28.79–251.63 ms | 31.13–266.60 ms | 7.86–96.44 | 0% |
| Load repeat | 22.76–183.32 ms | 27.46–219.46 ms | 29.67–242.18 ms | 8.20–97.03 | 0% |

The repeat run is directionally consistent with the first load run. SQL command-count headers were captured where emitted; load rows ranged from 8.00 to 52.04 statements per request in the first run and 8.00 to 52.03 in the repeat.

## Database and resource observations

| Observation | First load | Repeat load |
|---|---:|---:|
| Peak PostgreSQL connections | 14 | 14 |
| Peak active PostgreSQL sessions | 7 | available in artifact samples |
| Peak sampled lock waits | 5 | 6 |
| Peak sampled transaction age | 0.371 s | available in artifact samples |
| PostgreSQL query plans | 4 | 4 |

The lock-wait values are transient samples during concurrent writes, not a reported error condition; request errors remained zero and no unhandled/5xx/database-exception marker was found in the API logs. The four captured plans used index scans for stock balance, projects and subledger exposure; quotations used a sequential scan because the seeded table was small (161 rows in the plan). That plan is a dataset-size observation, not production-scale index certification.

The first load Docker sampler peaked at approximately `292%` container CPU and `210.7 MiB` memory under the configured 4-CPU/4-GiB disposable limit. API process peaks were approximately `346.62 MiB` working set and `246.68 MiB` private memory for the primary instance. Docker BlockIO and API/PostgreSQL samples are retained in the run artifacts; no disk-throughput or production host-capacity claim is made.

## Scope limits

- Development Seed is intentionally small and synthetic relative to a customer ERP installation.
- RabbitMQ, Redis, Worker backlog, browser workload, production topology, autoscaling and cross-region behavior were not certified.
- The benchmark labels all outputs as local evidence and not a production capacity claim.
- Stress mode was not required for this P0 baseline and was not used to infer a safe production concurrency ceiling.
