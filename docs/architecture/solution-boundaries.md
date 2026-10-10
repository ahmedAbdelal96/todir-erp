# Solution boundaries and architecture validation

This document makes approved dependency boundaries executable before Platform Kernel or business implementation begins. The canonical design remains the Master Blueprint, production engineering baseline, and Platform Kernel technical pack.

## Enforced backend boundaries

- `Shared.Primitives` cannot reference any project.
- `*.Contracts` projects may reference only `Shared.Primitives`.
- `Shared.BuildingBlocks` may reference only `Shared.Primitives`; it cannot depend on Contracts, Core, modules, hosts, or tests.
- Platform implementation cannot reference Foundation or business-module implementation.
- Foundation and business-module implementations cannot reference another implementation directly; use approved contracts, capabilities, events, or ports.
- Domain code inside a Foundation or business module cannot semantically reference a type from that same module's Infrastructure namespace. This is checked from Roslyn semantic symbols, so it applies even when both layers intentionally share one implementation assembly.
- Convention-following Foundation and business modules must use one implementation project plus an optional `.Contracts` project. Separate `.Domain`, `.Application`, or `.Infrastructure` production projects are rejected.
- Production projects cannot reference test projects.
- The project dependency graph must be acyclic.

`backend/Tests/Architecture` discovers convention-following projects from repository metadata and validates their `ProjectReference` relationships. The intra-project layer rule compiles each implementation project's local C# source into a Roslyn semantic model and compares symbol namespaces; it does not infer dependencies from source-text patterns. Controlled fixture projects are excluded from the production graph and demonstrate that validators reject illegal references.

## Frontend boundary

`pnpm architecture:frontend` parses static imports and exports through the TypeScript compiler API. It resolves relative imports and compiler-configured `baseUrl`/`paths` aliases, then permits another module only through that module's root `public.ts` or `public.tsx` surface. Fixture modules prove that public imports are accepted while relative and alias-based internal imports are rejected. External packages are not treated as local module dependencies.

## Scope and limits

Project references, cycles, named project shape, BuildingBlocks direction, and resolvable frontend module surfaces are mechanically enforced. The SharedKernel keyword check remains a supplemental warning heuristic for a small set of known business terms; it is not proof that all business semantics are absent. Ownership, contract quality, domain meaning, and every possible indirect architectural concern remain governed by the approved architecture documents and code review.

## Adding a project or module

Follow the governing `AGENTS.md` files and module plan. Add a project to `Backend.sln` when it is a real production or test project. Do not bypass architecture tests with naming exceptions, direct implementation references, or fixture-like code in production locations.

Run:

```text
dotnet test backend/Tests/Architecture/ArchitectureTests.csproj --no-build
corepack pnpm architecture:frontend
```
