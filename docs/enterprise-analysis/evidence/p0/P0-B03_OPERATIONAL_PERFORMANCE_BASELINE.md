# P0-B03 — Operational Performance Baseline

**Result:** `NOT MEASURED`  
**Reason:** the disposable database could not complete canonical migration and development seed, so no API request was measured.

## Attempt ledger

| Run | Environment | Result | Failure |
|---|---|---|---|
| `20261124222418_31848_82e1633e` | isolated PostgreSQL container; canonical migrator | FAIL | OpenIddict scope table missing because forward-fix ordering is invalid |
| `20261124222523_37716_08f318f3` | isolated PostgreSQL container; rebuilt DbMigrator | FAIL | Same canonical OpenIddict ordering defect |
| `20261124222650_31636_6f23ed2f` | isolated container; test-only OpenIddict pre-bootstrap attempt | FAIL | Pre-bootstrap attempted before `platform` schema existed |
| `20261124222721_12716_3e2af174` | isolated container; corrected pre-bootstrap | FAIL | `erp_app` role was not yet present for the compatibility script |
| `20261124222756_38152_73000bc9` | isolated container; corrected compatibility setup | FAIL | Quality forward-fix adds a foreign key while `quality_disposition` is forced under RLS |

The last failure is stored locally as `artifacts/performance/platform-performance-todir_perf_20261124222756_38152_73000bc9.failure.json`; it contains no credentials.

## Measurement table

| Operation/workload | Dataset | Concurrency | Samples | p50 | p95 | p99 | Throughput | Error rate | Repeatability | Artifact |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| API diagnostic reads | No seeded API dataset | — | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | No API startup | failure artifact |
| API write smoke | No API startup | — | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | Not reached | failure artifact |
| Sustained load | No API startup | — | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | Not reached | none |
| Stress | No API startup | — | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | `NOT MEASURED` | Not reached | none |

No p50/p95/p99, RPS, API error rate, connection pool, lock, CPU, memory, I/O or worker result is claimed. A failure to migrate is not an ERP performance result.

