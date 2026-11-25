# P0-B04 — PostgreSQL and Resource Assessment

**Result:** `PASS — LOCAL RESOURCE EVIDENCE`

## Environment

- Disposable container: `todir-p0b-postgres-20261125`
- Image: `postgres:18` (`PostgreSQL 18.6`)
- Limits: 4 CPUs, 4 GiB memory, localhost port `25432`
- Database per run: generated `todir_perf_*` name from `template0`
- API: one primary instance plus the load harness's two-instance stage
- Dataset: 2 tenants, 4 legal entities, 4 branches, 300 parties, 142 open items, 446 product variants, 8 projects

## Observed samples

| Metric | Diagnostic | First load | Repeat load |
|---|---:|---:|---:|
| Peak PostgreSQL connections | 10 | 14 | 14 |
| Peak active sessions | 1 | 7 | available in artifact |
| Peak sampled lock waits | 0 | 5 | 6 |
| Peak transaction age | 0.005 s | 0.371 s | available in artifact |
| Docker sampler rows | available in artifact | 110 | available in artifact |
| PostgreSQL query plans | 4 | 4 | 4 |

The load sampler recorded approximately `292%` peak Docker CPU and `210.7 MiB` peak container memory under the configured disposable limit. The primary API process reached approximately `346.62 MiB` working set and `246.68 MiB` private memory. These are local host/container observations, not a capacity limit for production.

## Query-plan review

The captured plans used index scans for stock balance, project listing and subledger exposure. The quotation plan used a sequential scan over a small seeded table (161 rows in the plan), so it is not evidence that a production-sized quotation workload will be efficient. No production-scale I/O, vacuum, checkpoint, WAL, replica, pool-saturation, or disk-throughput certification was attempted.

All raw PostgreSQL samples, Docker stats, API process samples and four JSON `EXPLAIN (ANALYZE, BUFFERS)` plans remain in the ignored run artifact directories under `artifacts/performance/`.
