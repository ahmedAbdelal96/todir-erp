# TODIR

TODIR is a production-grade, multi-tenant ERP SaaS for Arabic-market-first operations. It is being designed as a one-codebase Modular Monolith with strong module ownership, a future ASP.NET Core backend, React/TypeScript frontend, PostgreSQL persistence, and production-grade operations.

## Current stage

P00 repository and toolchain bootstrap is established. Platform Kernel and application implementation have not begun.

## Bootstrap requirements and validation

- .NET SDK: `10.0.102`, pinned by `global.json`.
- Node.js: `24.13.0`, pinned by `.node-version` and `package.json`.
- pnpm: `10.18.0`, pinned through the root `packageManager` field and used through Corepack.

From a compatible environment, validate the bootstrap through Corepack (no global pnpm installation is required):

```text
dotnet restore Todir.sln
dotnet build Todir.sln --no-restore
corepack pnpm install --frozen-lockfile
corepack pnpm typecheck
```

The solution contains only an empty `Todir.SharedKernel` build anchor so restore/build validation is warning-free. TypeScript contains one no-business-code compiler sentinel so the strict gate is executable. Platform Kernel and application implementation have not begun.

## Repository map

- `src/backend/` — future backend hosts, platform, foundation, and business modules.
- `src/frontend/` — future web app and frontend packages.
- `tests/` — architecture through performance test categories.
- `docs/` — canonical architecture, ADR, platform, module, API, runbook, and security documentation.
- `ops/` — operational infrastructure assets.
- `tools/` and `scripts/` — reusable engineering tools and repository automation.

Read [AGENTS.md](AGENTS.md) before contributing, then read the nearest scoped instruction file and applicable approved documents under `docs/`. Architecture and module plans are the source of truth; governance files define how to execute them.
