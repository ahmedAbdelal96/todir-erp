# P01 solution boundaries and architecture validation

P01 makes approved dependency boundaries executable before Platform Kernel or business implementation begins. The canonical design remains the Master Blueprint, production engineering baseline, and Platform Kernel technical pack.

## Enforced backend boundaries

- `Todir.SharedKernel` cannot reference any project and is checked for the explicitly forbidden module-owned ERP terms from the approved P01 boundary (for example `Invoice`, `Customer`, and `Warehouse`).
- `*.Contracts` projects may reference only `Todir.SharedKernel`.
- Platform implementation cannot reference Foundation or business-module implementation.
- Foundation and business-module implementations cannot reference another implementation directly; use approved contracts, capabilities, events, or ports.
- Domain cannot reference Infrastructure.
- Production projects cannot reference test projects.
- The project dependency graph must be acyclic.

`tests/architecture/Todir.ArchitectureTests` discovers convention-following projects from repository metadata and validates their `ProjectReference` relationships. Controlled fixture projects are excluded from the production graph and demonstrate that validators reject illegal references.

## Frontend boundary

`pnpm architecture:frontend` parses imports with the TypeScript compiler API. A frontend module may import another module only through that module's root `public.ts` or `public.tsx` surface. Fixture modules prove that a public import is accepted and an internal import is rejected.

## Adding a project or module

Follow the governing `AGENTS.md` files and module plan. Add a project to `Todir.sln` when it is a real production or test project. Do not bypass architecture tests with naming exceptions, direct implementation references, or fixture-like code in production locations.

Run:

```text
dotnet test tests/architecture/Todir.ArchitectureTests/Todir.ArchitectureTests.csproj --no-build
corepack pnpm architecture:frontend
```
