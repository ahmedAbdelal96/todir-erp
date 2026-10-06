# TODIR

TODIR is a production-grade, multi-tenant ERP SaaS for Arabic-market-first operations. It is implemented as a one-codebase Modular Monolith with strong module ownership, an ASP.NET Core backend, React/TypeScript frontend shell, PostgreSQL persistence, and production-grade platform boundaries.

## Current stage

The Platform Kernel baseline is implemented in `src/backend/platform/Todir.Platform`, with API/Worker/Migrator hosts, tenant-safe SQL bootstrap, module manifests, authorization, authentication/session foundations, transactions, messaging/outbox/inbox contracts, platform services, and the RTL frontend shell.

Environment-dependent PostgreSQL, RabbitMQ HA, restore, chaos/load, and staging gates require the approved external environment.

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

The solution includes shared primitives, platform implementation, API, Worker, and dedicated database migrator projects. TypeScript strict typechecking, frontend boundary validation, module-manifest validation, database-policy validation, and API contract validation are executable repository gates.

## Repository map

- `src/backend/` — future backend hosts, platform, foundation, and business modules.
- `src/frontend/` — future web app and frontend packages.
- `tests/` — architecture through performance test categories.
- `docs/` — canonical architecture, ADR, platform, module, API, runbook, and security documentation.
- `ops/` — operational infrastructure assets.
- `tools/` and `scripts/` — reusable engineering tools and repository automation.

Read [AGENTS.md](AGENTS.md) before contributing, then read the nearest scoped instruction file and applicable approved documents under `docs/`. Architecture and module plans are the source of truth; governance files define how to execute them.
