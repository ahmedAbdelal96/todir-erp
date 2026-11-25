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

| Run | Read p50 range | Read p95 range | Read p99 range | Read RPS range | All measured errors |
|---|---:|---:|---:|---:|---:|
| Diagnostic | 21.76–186.41 ms | `INSUFFICIENT_SAMPLES` | `INSUFFICIENT_SAMPLES` | 6.94–47.02 | 0% |
| Load | 22.95–184.06 ms | 28.79–251.63 ms | 31.13–266.60 ms | 7.86–96.44 | 0% |
| Load repeat | 22.76–183.32 ms | 27.46–219.46 ms | 29.67–242.18 ms | 8.20–97.03 | 0% |

Sustained write rows were not folded into the read ranges: first load Projects draft write p50/p95/p99 was `41.80/50.51/53.53 ms` at concurrency 1, `61.74/87.06/98.86 ms` at 4 and `99.30/283.39/402.85 ms` at 8; the repeat was `40.77/50.81/55.33`, `63.38/87.69/93.91` and `106.70/263.96/364.91 ms` respectively. All 300 write requests in each load run succeeded. The complete read/write per-operation ledger is [P0-B08](P0-B08_PER_OPERATION_BENCHMARK_AUDIT.md).

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

## P0-D post-correction verification

After the later DbMigrator coordination correction, a new `template0` database was migrated and seeded directly in a fresh disposable PostgreSQL 18.6 container. The canonical command exited `0`, used no `-PreMigrationSql`, and verified `265` applied history rows with `265` distinct identities. The API then started against that exact database: `/health` returned `200` and `/health/ready` returned `200` after the disposable runtime role was established. This startup/migration-only change does not invalidate the request-path measurements above; the raw run profiles, per-operation values and hashes are audited in [P0-B08](P0-B08_PER_OPERATION_BENCHMARK_AUDIT.md) and [P0-B09](P0-B09_ARTIFACT_VERIFICATION_AND_REPRODUCTION.md).

## Scope limits

- Development Seed is intentionally small and synthetic relative to a customer ERP installation.
- RabbitMQ, Redis, Worker backlog, browser workload, production topology, autoscaling and cross-region behavior were not certified.
- The benchmark labels all outputs as local evidence and not a production capacity claim.
- Stress mode was not required for this P0 baseline and was not used to infer a safe production concurrency ceiling.
