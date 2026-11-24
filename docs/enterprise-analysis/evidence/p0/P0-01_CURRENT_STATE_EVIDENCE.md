# P0-01 — Current-State Evidence

**As-of SHA:** `e9f7626953daf4c7cd426a49199dfd46fde69a8c`  
**Evidence type:** repository source, project graph, manifests, architecture documents.

## Verified inventory

| Surface | Observed state | Evidence |
|---|---|---|
| Backend modules | 25 directories and 25 manifests; all current manifests are L1–L4 | `backend/Modules/*/module.manifest.json` |
| Backend solution | API, Core, DbMigrator, Worker, Shared, Architecture, Integration, Security and module tests are in `backend/Backend.sln` | `dotnet sln backend/Backend.sln list` |
| API composition | Existing modules are explicitly registered in lines 49–74 | `backend/Api/Program.cs` |
| Worker composition | 20 message consumers, 5 hosted services and all existing modules are explicitly registered | `backend/Worker/Program.cs:54-125` |
| Frontend navigation | Destinations are registered per existing `moduleId`; no Analysis destination was found | `frontend/app/router/destination-registry.ts`, `frontend/app/router/app-router.tsx` |
| Analysis runtime | No Analysis API, schema, module manifest, worker host, seed, or frontend route was found | repository-wide targeted search |
| Schema topology | Platform owns `platform`; real modules own layer-prefixed schemas; direct cross-module table access is prohibited | `docs/adr/schema-topology.md`, `docs/architecture/solution-boundaries.md` |

## Interpretation

The design pack's proposed L6 Analysis capability and separate Analysis Worker are not current-state facts. They require an explicit P1 boundary, manifest, schema, lifecycle, entitlement, deployment, and project-graph decision. The current runtime is a modular monolith with a single operational Worker composition.

## Negative evidence limits

Absence was established by targeted inspection of runtime registration, project references, module manifests, frontend destination registry, and schema documentation. This is not a proof that no future branch or untracked local artifact exists; it is the current `dev` checkout at the SHA above.

