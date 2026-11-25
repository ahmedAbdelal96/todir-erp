# P0-B05 — Runtime Security Evidence

**Result:** `PASS — LOCAL RUNTIME SECURITY GATES`

## Harness security smoke

The diagnostic, final 100-request controlled load, and historical repeat load authenticated separate Atlas and Delta sessions against generated databases and executed the same three checks:

| Check | Diagnostic | Load | Repeat load |
|---|---:|---:|---:|
| Unauthenticated request denied | `401 PASS` | `401 PASS` | `401 PASS` |
| Delta session against Atlas legal entity denied | `403 PASS` | `403 PASS` | `403 PASS` |
| Atlas session against Delta legal entity denied | `403 PASS` | `403 PASS` | `403 PASS` |

Each run also passed `21/21` authenticated functional JSON checks and `8/8` write-smoke operations on the seeded Atlas/Delta tenants. The API logs contain the expected unauthenticated `missing_token` authorization messages and no unhandled exception, 5xx or database-exception markers.

## Existing database security tests

The existing security suite ran against the isolated migrated database `todir_p0d_final_20261125` in the disposable PostgreSQL 18.6 container after the P0-D canonical migration:

```text
dotnet test backend/Tests/Security/SecurityTests.csproj --no-restore --configuration Release
Passed: 7, Failed: 0, Skipped: 0
```

The canonical seed regression suite also ran against newly created disposable databases and exercised forced RLS, runtime-role boundaries, tenant isolation, idempotent seed reruns, concurrent DbMigrator serialization, bounded lease timeout, invalid configuration rejection and terminated-waiter recovery:

```text
dotnet test backend/Tests/SeedData/SeedDataTests.csproj --no-restore --configuration Release
Passed: 9, Failed: 0, Skipped: 0
```

These results are runtime evidence for the local disposable environment. They do not certify customer configuration, production credentials, Worker isolation, or a production deployment topology.
