# P0 Foundation Closure and Handoff

**Status:** `P0_EVIDENCE_COMPLETE`  
**Branch:** `dev`  
**Verification date:** 2026-11-25 (Africa/Cairo)  
**Starting source:** `ab49df8b95a16c92804cce7405c42a9f95b0beb7`  
**Delivery commit:** the final commit containing this document; the exact pushed SHA is reported with the handoff.

## Scope and decision

This closes the existing P0-B/P0 foundation verification mission only. No Analysis, Recommendation, P1, Quality implementation, unrelated migration, security-policy weakening, customer database, or shared development database was used or changed.

The earlier P0 blocker is closed with evidence for canonical migration/seed, startup targeting, functional smoke, runtime tenant isolation, existing security tests, advisory-lock lifecycle/recovery, controlled performance, repeatability, and durable evidence retention. Development Seed remains a local verification dataset, not production capacity certification.

## DbMigrator reliability correction

The current platform-owner correction in `backend/DbMigrator/Program.cs` is present and was not refactored beyond the required bounded-wait behavior:

- the session-scoped advisory key remains `hashtextextended('todir.dbmigrator:' || current_database(), 0)`;
- the lease is acquired before runtime bootstrap, migration, or seed mutation;
- the default bounded wait is 300 seconds;
- `TODIR_MIGRATOR_LOCK_TIMEOUT_SECONDS` accepts integer values from 1 through 3600;
- timeout returns exit code 5 with an explicit no-mutation message;
- Ctrl+C cancels lease acquisition and returns exit code 5;
- disposing the database session releases the session-scoped lease;
- invalid timeout configuration is rejected with exit code 2 before database access.

The latest OpenIddict ordering and Quality RLS migration corrections were inspected and remain unchanged. Canonical history verification now derives expected identities and SHA-256 checksums from the current bootstrap SQL directory rather than hard-coding a historical row count.

## Acceptance evidence

| Gate | Result | Evidence |
|---|---|---|
| Fresh canonical migration and Development Seed | PASS | New disposable PostgreSQL 18.6 instance; no `-PreMigrationSql`; dataset snapshot 2 tenants, 4 legal entities, 4 branches, 300 parties, 142 open items, 446 product variants, 8 projects |
| Database targeting and startup | PASS | Migrator identity proof and API startup against the generated target database; `/health` and `/health/ready` returned 200 in the fresh runtime proof |
| Functional smoke | PASS | 21/21 authenticated JSON operations in each final benchmark run |
| Runtime tenant isolation | PASS | 401 unauthenticated and 403 cross-tenant/legal-entity checks in each final benchmark run |
| Write smoke | PASS | 8/8 write-smoke operations in each final benchmark run |
| Migration lease timeout/recovery | PASS | timeout-without-mutation, released-lease retry, terminated waiter recovery, invalid configuration tests |
| Existing security/tenant suite | PASS | `SecurityTests`: 7/7; `SeedDataTests`: 9/9 |
| Diagnostic benchmark | PASS | 42 read rows × 5 measured requests = 210 latency samples; no measured request errors; p95/p99 correctly marked insufficient |
| Controlled load benchmark | PASS | 63 read rows × 100 = 6,300 samples; 3 sustained-write rows × 100 = 300 samples; 2-instance stage = 100 samples; zero errors |
| Repeatability | PASS with limits | Historical identical load repeat: 6,300 reads + 300 writes, zero errors and directionally consistent ranges; its JSON is aggregate-only and explicitly labelled |
| Durable archive and checksums | PASS | `benchmark-archive/` contains JSON, PostgreSQL, Docker and process-resource artifacts plus an archive manifest and independent verifier |

## Performance results

Final controlled load run: `20261125103950_28700_b2a9b5cf`, profile `RequestsPerEndpoint=100`, warm-up 5, concurrency 1/4/8, two API instances, sustained writes, canonical migration without compatibility SQL.

| Measure | Result |
|---|---:|
| Read p50 range across 63 result rows | 19.24–184.23 ms |
| Read p95 range across 63 result rows | 25.31–861.68 ms |
| Read p99 range across 63 result rows | 27.67–930.72 ms; measured with 100 samples per row |
| Read throughput range | 4.98–101.83 RPS |
| Sustained write p50/p95/p99 at concurrency 1 | 35.27 / 46.19 / 52.74 ms |
| Sustained write p50/p95/p99 at concurrency 4 | 53.89 / 76.16 / 85.21 ms |
| Sustained write p50/p95/p99 at concurrency 8 | 96.70 / 255.57 / 392.77 ms |
| Sustained write errors | 0/300 |
| Peak sampled PostgreSQL connections / active sessions | 17 / 2 |
| Peak sampled PostgreSQL lock waits | 6 |
| Peak sampled transaction age | 0.005997 s |
| Peak Docker CPU / memory | 350.87% / 230.7 MiB of 4 GiB |
| API process peak working/private memory | 333.12 / 231.43 MiB; second instance 235.01 / 146.43 MiB |

The high-end rows are per-operation/tenant/concurrency observations, not aggregate SLOs. The seed table cardinalities are too small for customer-scale query-plan or capacity claims. Quotations still exhibit the small-table sequential-scan observation documented in P0-B03. No Redis, RabbitMQ, Worker backlog, browser, autoscaling, multi-region, production host, or mixed-ERP load was certified.

## Commands executed

```powershell
dotnet build backend\DbMigrator\DbMigrator.csproj --configuration Release --no-restore -p:MSBuildEnableWorkloadResolver=false -m:1
dotnet build backend\Tests\SeedData\SeedDataTests.csproj --configuration Release --no-restore -p:MSBuildEnableWorkloadResolver=false -m:1
dotnet test backend\Tests\SeedData\SeedDataTests.csproj --no-build --configuration Release --logger "console;verbosity=minimal"
dotnet test backend\Tests\Security\SecurityTests.csproj --no-restore --configuration Release --logger "console;verbosity=minimal"
dotnet test backend\Tests\Architecture\ArchitectureTests.csproj --no-restore --configuration Release --logger "console;verbosity=minimal"
pwsh -NoProfile -File backend\Tests\Performance\platform-performance-harness.tests.ps1
pwsh -NoProfile -File backend\Tests\Performance\platform-performance-benchmark.ps1 -Mode diagnostic -PostgresContainer todir-p0b-postgres-20261125-final -PostgresPort 25433 -ApiPort 18591 -ArtifactDirectory docs\enterprise-analysis\evidence\p0\benchmark-archive\diagnostic
pwsh -NoProfile -File backend\Tests\Performance\platform-performance-benchmark.ps1 -Mode load -RequestsPerEndpoint 100 -PostgresContainer todir-p0b-postgres-20261125-final -PostgresPort 25433 -ApiPort 18593 -ArtifactDirectory docs\enterprise-analysis\evidence\p0\benchmark-archive\load-100
```

Secrets were passed only through process arguments/environment during local execution and are not recorded in the archive. The archive verifier confirmed JSON validity, `PreMigrationSql = null`, request/sample reconciliation where samples exist, zero measured request errors, and no sensitive-pattern matches.

## Remaining limits and handoff

P0 is locally evidenced and may hand off to the separately governed decision process. The following remain intentionally outside this closure: P1 implementation, Analysis/Recommendation behavior, production-scale data certification, Worker/broker/Redis contention, browser performance, deployment topology, autoscaling and multi-region certification. Any future P1 mission must retain the no-regression guardrail and re-run the real-stack security/isolation and performance gates after its last relevant change.

The final evidence index, P0-02, P0-B03, P0-B05, P0-B07, P0-B08, P0-B09, repository manifest and checksums are updated together with this handoff.
