# P0-B01 — Docker and Environment Root-Cause Analysis

**Result:** Docker access recovered outside the sandbox; benchmark remains blocked by canonical disposable migration failures.  
**Starting P0 SHA:** `7fd24009cf0d464ecfad68d9dddb8f5197841ebd`  
**Latest repository SHA during runs:** `c06a86183b2b651ff23f63a11ec50847df6650ad`

## Original failure

The first preflight returned:

```text
permission denied while trying to connect to the Docker API at npipe:////./pipe/dockerDesktopLinuxEngine
```

## Diagnosis

| Check | Observed result | Interpretation |
|---|---|---|
| Docker client | 29.2.1, Windows amd64 | Client is installed |
| Context | `desktop-linux` → `npipe:////./pipe/dockerDesktopLinuxEngine` | Context is correct |
| Named pipe | Exists | Endpoint is present |
| `docker ps` inside sandbox | Access denied | Sandbox token cannot open the pipe |
| `docker-users` membership | Present | Not a missing group-membership diagnosis |
| Docker Desktop containers outside sandbox | PostgreSQL, Redis and RabbitMQ were running | Daemon/backend is available |
| `docker ps` with approved elevated read-only execution | Succeeded | Failure was execution-context pipe permission, not a stopped daemon |
| WSL/SCM inspection | Access denied from restricted context | Not used as evidence of daemon failure |

The minimum correction was to run Docker diagnostics and disposable-container operations with the approved elevated execution context. No Docker installation, ACL, group, security-policy, or WSL change was made.

## Isolation proof

The benchmark was pointed at a newly created container named `todir-p0b-postgres-20261124` with:

- image `postgres:18`;
- label `todir.perf.disposable=true` and owner label `p0b`;
- ephemeral container storage and `--restart=no`;
- dedicated loopback mapping `127.0.0.1:25432:5432`;
- bounded `--cpus=4` and `--memory=4g`;
- a generated database name matching `todir_perf_<run-id>`;
- container removal in the wrapper `finally` block.

The harness additionally validates the label, name prefix, running state, PostgreSQL image, and host-port mapping before creating a database. The shared `todir-postgres-1` container was not used for benchmark load.

## Remaining blocker

The canonical fresh DbMigrator path does not complete on the isolated container. First it fails because `platform-kernel-forward-fix-oauth-write-scope.sql` is ordered before `platform-kernel-forward-fix-openiddict.sql`; after that is exposed, the Quality migration fails while adding a foreign key under `FORCE ROW LEVEL SECURITY` on `business_quality.quality_disposition`.

Those are repository migration defects. Production migration/order changes are outside this P0-B authorization, so no workaround was promoted into production code. The exact failure is preserved in the local failure artifact for run `20261124222756_38152_73000bc9` and summarized in P0-B03.

