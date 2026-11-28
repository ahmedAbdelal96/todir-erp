# Future-agent handoff

## Startup checklist

1. Confirm branch `dev`, current SHA, worktree status and preserve unrelated changes.
2. Read this README, `CURRENT_STATUS.md`, `DECISION_REGISTER.md`, `ARCHITECTURE_APPROVAL_BRIEF.md`, `PILOT_SPECIFICATION.md`, and the applicable module pack.
3. Confirm the owner decisions are `APPROVED`; if not, stop before P2 implementation.
4. Reconcile current manifests, `backend/Api/Program.cs`, `backend/Worker/Program.cs`, effective access, entitlement/lifecycle and DbMigrator. Do not trust illustrative routes as implemented.
5. Revalidate the approved Subledger read contract and temporal semantics against current code.

## Closeout checklist

- Do not modify production business logic to make an Analysis test pass.
- Run migration/seed on a disposable database and verify RLS/authorization with real API and frontend paths.
- Prove no implicit run and idempotent explicit run.
- Prove immutable history, provenance, evidence authorization, retry/lease/cancel/restart and tenant/entity/branch isolation.
- Capture statistically meaningful p50/p95/p99 only with sample size, errors, concurrency, dataset size and environment.
- Inspect every fresh browser/evidence artifact after the last change.
- Update the pilot specification, acceptance matrix, manifest and checksums in the same commit.
- Stage only task-owned files; preserve untracked user changes; push one commit to `origin/dev` over SSH.

## Non-negotiable prohibitions

No global AI navigation, automatic runs, arbitrary SQL/table exports, cross-module writes, duplicate financial truth, RLS weakening, autonomous business command, Python/vector/DWH/new broker without a new approved ADR, P1/P2 expansion, or false claim that Development Seed proves production capacity.
