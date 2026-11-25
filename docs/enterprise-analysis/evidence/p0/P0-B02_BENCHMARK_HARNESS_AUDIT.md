# P0-B02 — Benchmark Harness Technical Audit

**Result:** tooling hardening implemented; helper verification, canonical diagnostic execution and repeated controlled load execution passed.

## Corrections applied

| Area | Previous defect | Correction |
|---|---|---|
| Modes | `diagnostic`, `load` and `stress` did not materially change the workload | Mode profiles now control request count, warm-up, concurrency levels, sustained writes, multi-instance reads and stress abort limits |
| Parameters | `SkipWorker` and `SkipBrowser` were declared but unused | Removed unsupported switches; the API harness reports that Worker/browser are outside this executable's scope |
| Targeting | Hardcoded `todir-postgres-1`, port `15432`, and committed development passwords | Explicit container/port/host parameters plus environment-provided credentials; disposable container label/name/image/port are validated |
| Safety | Database-name prefix was the main cleanup guard | Generated run ID, exact disposable name validation, container ownership validation, dedicated ports, and cleanup of only the generated database/processes |
| Logging | `log_statement=all` was enabled for every run | Disabled by default; available only for diagnostic mode and recorded as instrumentation overhead |
| Percentiles | Empty samples returned `0`; tiny samples were presented as tail evidence | Helpers return `NO_SAMPLES`/`INSUFFICIENT_SAMPLES`; P99 requires 100 samples, P95 requires 20, and nearest-rank is recorded |
| Errors | Non-2xx/transport failures were not sufficiently classified | Results now distinguish HTTP 4xx, HTTP 5xx, timeout, transport and invalid functional JSON; errors are excluded from success counts |
| SQL observability | Response-level database command counts were not retained in benchmark summaries | `X-Todir-Db-Commands` is captured per request and aggregated when the API emits the header; unavailable values remain null |
| Functional proof | A successful status alone was treated as enough | Pre-measurement smoke checks require successful JSON responses; write smoke remains separately reported |
| Resource evidence | API process sampling could silently omit database resources | PostgreSQL activity, Docker container stats and API process samples are separate artifacts; unavailable values remain null |
| Failure handling | Cleanup could hide the primary failure | Failure artifacts preserve the primary exception and cleanup warnings are emitted separately |
| Environment | Process environment was left mutated | Harness snapshots and restores the environment variables it owns |
| Strict-mode artifact path | A missing custom artifact directory raised a null `.Path` property error | The harness now resolves existing directories or creates the requested repository-local path |
| Security URL interpolation | `$AtlasEntity?page` was parsed as an unset PowerShell variable | Security smoke URLs use explicit subexpression interpolation |

## Automated verification

Command:

```text
pwsh -NoProfile -File backend/Tests/Performance/platform-performance-harness.tests.ps1
```

Result: `PASS`.

The test covers mode differentiation, invalid bounded inputs, disposable-name rejection, empty/insufficient percentile samples, nearest-rank behavior, error-rate calculation and error distribution. PowerShell parser validation also passed.

## Important limits

The helper tests alone do not prove the real API, PostgreSQL, Docker, migration, seed, RLS or cleanup path. The subsequent diagnostic and two load runs supplied that local end-to-end evidence. The harness remains a Development Seed/local-host baseline and is not a production capacity certification.

The optional `-PreMigrationSql` switch is restricted to the known OpenIddict bootstrap script and a disposable target. It is a diagnostic compatibility escape hatch only; it does not mark the canonical migration path as passing and did not resolve the later Quality RLS migration failure.

