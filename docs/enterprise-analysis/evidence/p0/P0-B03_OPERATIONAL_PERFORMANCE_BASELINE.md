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
| `20261125103326_38136_663ae499` | diagnostic | 42 read result rows, 5 measured requests per row = 210 samples | PASS; p95/p99 explicitly insufficient at this sample size | [`benchmark-archive/diagnostic`](benchmark-archive/diagnostic) |
| `20261125103950_28700_b2a9b5cf` | load | 6,300 measured reads + 300 sustained writes + 100 two-instance samples | PASS; 0 measured request errors; p99 measured | [`benchmark-archive/load-100`](benchmark-archive/load-100) |
| `20261125091708_30864_85500d9a` | load repeat | 6,300 measured reads + 300 sustained writes | PASS; 0 measured request errors; historical aggregate-only JSON | [`benchmark-archive/repeat`](benchmark-archive/repeat) |

The final load run used `RequestsPerEndpoint=100`, concurrency `1,4,8`, warm-up requests, sustained draft writes, and the harness multi-instance stage. It reported `21/21` functional smoke, `3/3` security smoke, and `8/8` write smoke checks passing. The repeat retains the previously verified identical profile; its historical JSON is aggregate-only and is labelled accordingly.

## Latency and throughput

The ranges below span the individual endpoint/tenant/concurrency result rows; they are not a single aggregate SLO.

| Run | Read p50 range | Read p95 range | Read p99 range | Read RPS range | All measured errors |
|---|---:|---:|---:|---:|---:|
| Diagnostic | 21.76–186.41 ms | `INSUFFICIENT_SAMPLES` | `INSUFFICIENT_SAMPLES` | 6.94–47.02 | 0% |
| Load (100 requests/row) | 19.24–184.23 ms | 25.31–861.68 ms | 27.67–930.72 ms | 4.98–101.83 | 0% |
| Load repeat | 22.76–183.32 ms | 27.46–219.46 ms | 29.67–242.18 ms | 8.20–97.03 | 0% |

Sustained write rows were not folded into the read ranges: the final 100-request load Projects draft write p50/p95/p99 was `35.27/46.19/52.74 ms` at concurrency 1, `53.89/76.16/85.21 ms` at 4 and `96.70/255.57/392.77 ms` at 8. All 300 write requests succeeded. The complete read/write per-operation ledger is [P0-B08](P0-B08_PER_OPERATION_BENCHMARK_AUDIT.md).

The repeat run is directionally consistent with the first load run. SQL command-count headers were captured where emitted; load rows ranged from 8.00 to 52.04 statements per request in the first run and 8.00 to 52.03 in the repeat.

## Database and resource observations

| Observation | Final 100-request load | Historical repeat load |
|---|---:|---:|
| Peak PostgreSQL connections | 17 | 14 |
| Peak active PostgreSQL sessions | 2 | 2 |
| Peak sampled lock waits | 6 | available in artifact samples |
| Peak sampled transaction age | 0.005997 s | available in artifact samples |
| PostgreSQL query plans | 4 | 4 |

The lock-wait values are transient samples during concurrent writes, not a reported error condition; request errors remained zero and no unhandled/5xx/database-exception marker was found in the API logs. The four captured plans used index scans for stock balance, projects and subledger exposure; quotations used a sequential scan because the seeded table was small (161 rows in the plan). That plan is a dataset-size observation, not production-scale index certification.

The final load Docker sampler peaked at `350.87%` container CPU and `230.7 MiB` memory under the configured 4-CPU/4-GiB disposable limit. API process peaks were `333.12 MiB` working set and `231.43 MiB` private memory for the primary instance; the second instance peaked at `235.01/146.43 MiB`. Docker BlockIO and API/PostgreSQL samples are retained in the durable archive; no disk-throughput or production host-capacity claim is made.

## P0-D post-correction verification

After the later DbMigrator coordination correction, a new `template0` database was migrated and seeded directly in a fresh disposable PostgreSQL 18.6 container. The canonical command exited `0`, used no `-PreMigrationSql`, and verified canonical history identities/checksums derived from the current bootstrap directory. The API then started against that exact database: `/health` returned `200` and `/health/ready` returned `200` after the disposable runtime role was established. The bounded advisory-lease behavior and lifecycle/recovery tests are recorded in the final handoff; the durable run profiles, per-operation values and hashes are audited in [P0-B08](P0-B08_PER_OPERATION_BENCHMARK_AUDIT.md) and [P0-B09](P0-B09_ARTIFACT_VERIFICATION_AND_REPRODUCTION.md).

## Scope limits

- Development Seed is intentionally small and synthetic relative to a customer ERP installation.
- RabbitMQ, Redis, Worker backlog, browser workload, production topology, autoscaling and cross-region behavior were not certified.
- The benchmark labels all outputs as local evidence and not a production capacity claim.
- Stress mode was not required for this P0 baseline and was not used to infer a safe production concurrency ceiling.
