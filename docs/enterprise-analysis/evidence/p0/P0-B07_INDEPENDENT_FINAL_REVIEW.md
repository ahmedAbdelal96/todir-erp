# P0-B07 — Independent Final Review and Exit Decision

## Critical review

| Question | Finding |
|---|---|
| Could the benchmark target shared data? | The hardened path rejects unlabelled/shared containers and requires a P0-owned name, image and port. The attempted runs used a separate ephemeral container. |
| Could it kill a pre-existing process? | API ports are checked before launch and cleanup tracks only PIDs started by the run. The wrapper removes only the exact container it created. |
| Could it report success when requests fail? | HTTP/transport failures are classified; however the real run never reached API traffic, so end-to-end proof remains open. |
| Could P99 be misleading? | P99 is `INSUFFICIENT_SAMPLES` below 100 samples; empty metrics are null/statused. |
| Could warm-up mix with measurement? | Warm-up requests are executed and discarded before measured traffic, with counts recorded. |
| Could debug logging distort results? | Detailed PostgreSQL logging is opt-in and diagnostic-only; load/stress do not enable it. |
| Could missing PostgreSQL metrics become zero? | Missing snapshots are null; container/API samplers are separate. |
| Could the API use another database? | The harness proves the generated database identity and gives API connections an application name, but API startup was not reached in the failed runs. |
| Could cleanup destroy unrelated resources? | The generated database name and container ownership are validated; cleanup targets only the generated database and launched processes. |
| Is the result reproducible? | The command, container labels/limits, SHA and failure run IDs are recorded; successful measurement is not reproducible until migration is repaired. |
| Are runtime security checks executed? | No. They are correctly left open rather than inferred from static RLS. |
| Are performance claims overstated? | No measured performance claim is made. |

## Exit decision

`P0_BLOCKED`

Docker was diagnosed and safely recovered. Harness hardening and helper verification passed. The critical functional prerequisite remains blocked by canonical fresh-database migration defects: OpenIddict forward-fix ordering and Quality foreign-key installation under forced RLS. Repairing production migration behavior or authorizing a complete, separately validated disposable bootstrap strategy is required before operational performance and runtime-security evidence can be accepted.

P1/P2 work must not start from these incomplete measurements.

