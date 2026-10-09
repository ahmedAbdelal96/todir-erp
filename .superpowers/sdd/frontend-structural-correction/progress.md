# SDD ledger — plan: docs/superpowers/plans/2026-10-08-frontend-structural-correction.md

Pre-flight: shared interfaces checked. Task 1 produces the flattened source root consumed by Tasks 2–6; Task 2 produces aliases consumed by validator/build checks; Task 3 updates the paths consumed by verification; Task 4 consumes the flattened source root and aliases; Task 5 documents the final structure; Task 6 verifies all outputs.

Ruling: execute Tasks 1–6 without commits or pushes because the user explicitly authorized Native execution but explicitly withheld Task 7 commit/push actions.

Ruling: keep `src/frontend/src/styles.css` at the source root because the request explicitly permits the single-file location and creating `styles/` would add navigation without ownership value.

Ruling: do not introduce a new BrandProfile provider or API service layer because the current shell has neither existing integration point; doing so would expand into F12/F13 behavior rather than structural correction.

Task 1: complete — verified canonical files exist under `src/frontend/`, obsolete wrappers/ownership paths are absent, empty speculative source areas were removed, and generated legacy output was moved out of the source tree.

Task 2: complete — updated application-boundary imports, TypeScript 7-compatible aliases, root sentinel path, Vite React/alias configuration, and moved theme/grid ownership; `pnpm typecheck` and `pnpm --dir src/frontend build` pass.

Task 3: complete — updated pnpm workspace/importer, root scripts, API generation, smoke/release checks, architecture defaults, CI drift path, container paths, and regenerated the lockfile without dependency changes; frozen install and API drift check pass.

Task 4: complete — added and verified the negative Design System → business-module fixture, updated the validator to classify `app`, `platform`, `design-system`, and `modules`, and preserved all existing public-surface, cycle, and paid-package protections.

Task 5: complete — updated canonical frontend architecture, production structure, ADR evidence path, and module-plan structure references; documentation now describes one flattened frontend application and justified future package extraction.

Ruling: the first parallel final-gate batch had a transient TypeScript sync named-pipe timeout caused by concurrent architecture/typecheck processes; rerunning architecture and typecheck serially passed, so no source defect was inferred.

Task 6: complete — serial final verification passed architecture checks, strict typecheck, 24 frontend tests, RTL shell smoke, API drift, 73 release gates, Vite production build, diff check, stale-path search, and final tree inspection. Docker daemon verification was unavailable in the environment and was not treated as a required structural gate.

Final review: self-review (no reviewer tool available). Reviewed the migration diff, ownership paths, aliases, validator fixtures, workspace/importer, scripts, CI/container references, docs, dependency diff, stale-path search, and generated-output hygiene. No Critical or Important findings.

Execution stopping point: Tasks 1–6 verified; no commit or push performed; F08 not started.
