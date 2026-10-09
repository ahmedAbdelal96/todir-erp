# Frontend Structural Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Flatten the single frontend application into `src/frontend/`, correct platform/design-system ownership, and preserve all Stage-1 behavior and architecture protections.

**Architecture:** Move the application root and existing source files without changing runtime contracts or business behavior. Keep application orchestration under `app/`, runtime and transport concerns under `platform/`, UI foundation contracts under `design-system/`, and business-module governance under `modules/`. Enforce the major-area dependency rules through brand-neutral TypeScript aliases and the existing architecture validator.

**Tech Stack:** React 19, TypeScript strict mode, Vite 8, pnpm 10, Node test runner, existing frontend architecture validator, existing Stage-1 runtime/theme/spike tests.

**Spec:** Approved structural-correction request supplied in the task, constrained by `docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`, `docs/architecture/02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`, `docs/architecture/03_ERP_SAAS_PLATFORM_KERNEL_AND_MODULE_SYSTEM_TECHNICAL_DESIGN_PACK.md`, and `docs/adr/frontend-platform-foundation.md`.

## Global Constraints

- Keep one frontend application rooted at `src/frontend/`; do not create `packages/` or compatibility shims.
- Do not introduce new filesystem basenames containing `todir`.
- Preserve React, Vite, strict TypeScript, RTL behavior, RuntimeContext behavior, token/theme semantics, and free-only dependency rules.
- Keep platform and design-system code independent from business-module internals; cross-module integration uses public surfaces only.
- Do not implement F08 or later product functionality, production primitives, production grid, or the F12 API/data platform.
- Do not add production dependencies for React Aria or AG Grid; no new production dependency is expected.
- Update current references; leave only clearly historical documentation references, if any.

## Review Focus

- The moved frontend package must remain the pnpm workspace project and must produce the same Vite bundle.
- The root and frontend TypeScript configurations must resolve aliases without weakening strictness.
- Theme presets must have one canonical design-system ownership location and preserve all token semantics.
- The validator must still reject internal module imports, platform/design-system module dependencies, cycles, and paid packages.
- Shell verification must continue to detect RTL, authentication/context endpoints, error/access states, and forbidden local storage.

### Task 1: Flatten the application root and relocate source ownership

**Files:**
- Move: `src/frontend/apps/todir-web/package.json` → `src/frontend/package.json`
- Move: `src/frontend/apps/todir-web/tsconfig.json` → `src/frontend/tsconfig.json`
- Move: `src/frontend/apps/todir-web/index.html` → `src/frontend/index.html`
- Move: `src/frontend/apps/todir-web/src/` → `src/frontend/src/`
- Move: `src/frontend/apps/todir-web/src/api/` → `src/frontend/src/platform/api/`
- Move: `src/frontend/apps/todir-web/src/app-shell.tsx` → `src/frontend/src/app/shell/app-shell.tsx`
- Move: `src/frontend/apps/todir-web/src/bootstrap/` → `src/frontend/src/app/bootstrap/`
- Move: `src/frontend/apps/todir-web/src/platform/grid/` → `src/frontend/src/design-system/grid/`
- Move: `src/frontend/apps/todir-web/src/platform/theme/` → `src/frontend/src/design-system/theme/`
- Move: `src/frontend/apps/todir-web/src/platform/tokens/` → `src/frontend/src/design-system/tokens/`
- Move: `src/frontend/apps/todir-web/src/platform/tokens/themes/` → `src/frontend/src/design-system/theme/presets/`
- Move: `src/frontend/apps/todir-web/src/modules/` → `src/frontend/src/modules/`
- Move: `src/frontend/apps/todir-web/src/styles.css` → `src/frontend/src/styles.css`
- Remove: obsolete empty `src/frontend/apps/`, `src/frontend/packages/`, and generated frontend `dist/` output after confirming it is ignored and reproducible.

**Interfaces:**
- Produces the canonical source root `src/frontend/src/` and ownership roots `app`, `platform`, `design-system`, and `modules`.

- [ ] Move tracked files with `git mv` while preserving file contents.
- [ ] Remove obsolete empty wrappers and the ignored generated bundle only after confirming the target paths.
- [ ] Verify no source file exists in both the old and new location.

### Task 2: Correct imports and frontend configuration

**Files:**
- Modify: `src/frontend/src/main.tsx`
- Modify: `src/frontend/src/design-system/grid/grid-contract.ts`
- Modify: moved token/theme files and indexes
- Modify: `src/frontend/tsconfig.json`
- Create: `src/frontend/vite.config.ts`
- Modify: `tsconfig.json`

**Interfaces:**
- Produces aliases `@app/*`, `@platform/*`, `@ui/*`, and `@modules/*` mapped to the four major source areas.
- Produces a Vite alias configuration using the same mappings and the existing React plugin dependency.

- [ ] Update relative imports affected by the moves, keeping internal imports cohesive and using architectural aliases for cross-area imports.
- [ ] Update the frontend tsconfig base extension, strict compiler options, bundler resolution, JSX, `baseUrl`, aliases, and inclusion of `vite.config.ts`.
- [ ] Update the root sentinel path to `src/frontend/src/app/bootstrap/typecheck-sentinel.ts`.
- [ ] Configure Vite’s React plugin and aliases without changing build output semantics.

### Task 3: Update workspace, scripts, containers, and generated-client paths

**Files:**
- Modify: `pnpm-workspace.yaml`
- Modify: `package.json`
- Modify: `scripts/api/generate-platform-client.mjs`
- Modify: `scripts/verification/frontend-shell-smoke.mjs`
- Modify: `scripts/verification/release-gates.mjs`
- Modify: `scripts/architecture/check-frontend-module-boundaries.mjs`
- Modify: `scripts/architecture/frontend-module-boundary.mjs`
- Modify: `.github/workflows/repository-validation.yml`
- Modify: `ops/containers/web.Dockerfile`
- Regenerate: `pnpm-lock.yaml` with the same dependency graph and the new workspace importer path.

**Interfaces:**
- Produces workspace project `src/frontend` and all existing root commands targeting the flattened application.
- Keeps generated client output canonical at `src/frontend/src/platform/api/platform-client.ts`.

- [ ] Replace old path defaults and hard-coded references with the flattened paths.
- [ ] Update the container copy/build/output paths.
- [ ] Run lockfile-only workspace regeneration and verify no dependency additions or removals beyond importer relocation.

### Task 4: Preserve and strengthen architecture validation

**Files:**
- Modify: `scripts/architecture/frontend-module-boundary.mjs`
- Modify: `scripts/architecture/check-frontend-module-boundaries.mjs`
- Modify: `scripts/architecture/verify-frontend-module-fixtures.mjs` only if needed for the new area checks.
- Create/modify: governed architecture fixtures under `tests/architecture/fixtures/frontend/` if required to prove design-system/platform restrictions.

**Interfaces:**
- `validateFrontendArchitecture({ rootDir, modulesRoot })` continues returning `{ violations, cycles }`.
- The validator recognizes `app`, `platform`, `design-system`, and `modules` under the flattened root.

- [ ] Add or update a negative fixture proving design-system code cannot import a business module, while preserving the existing public-surface and cycle fixtures.
- [ ] Keep paid-package detection unchanged and ensure aliases resolve from the new frontend tsconfig.
- [ ] Run architecture validation and fixture verification before broader tests.

### Task 5: Update canonical architecture documentation

**Files:**
- Modify: `docs/architecture/FRONTEND_PLATFORM_AND_DESIGN_SYSTEM_CONTRACT.md`
- Modify: `docs/architecture/02_ERP_SAAS_PRODUCTION_READY_TECH_STACK_AND_PROJECT_STRUCTURE.md`
- Modify: `docs/adr/frontend-platform-foundation.md`

**Interfaces:**
- Canonical documents describe `src/frontend/src/` as the current physical application source root and explain that logical boundaries do not require separate npm packages.

- [ ] Update only physical structure diagrams, current paths, and the package-extraction guidance.
- [ ] Preserve all approved Stage-1 technology, runtime, RTL, theme, and free-only decisions.
- [ ] Search canonical docs for current instructions that recreate `apps/todir-web` or speculative `packages/` and update them; retain only clearly historical evidence when necessary.

### Task 6: Execute the full structural verification gate

**Files:**
- Modify only in-scope files if verification exposes migration defects.
- Generate local outputs only under ignored `artifacts/` paths where a command requires them.

- [ ] Run `pnpm architecture:frontend`.
- [ ] Run `pnpm typecheck`.
- [ ] Run `pnpm test:frontend`.
- [ ] Run `pnpm frontend:smoke`.
- [ ] Run `pnpm api:drift` and the applicable release-gate command.
- [ ] Run the flattened frontend production build through `pnpm --dir src/frontend build` and the repository container/build checks applicable to this scope.
- [ ] Search for stale paths and inspect every remaining occurrence.
- [ ] Print the actual resulting frontend tree to a reasonable depth.
- [ ] Self-review architecture ownership, imports, dependency changes, generated output, behavior preservation, and documentation consistency; fix clear in-scope defects and rerun the gate.

### Task 7: Commit, push, and stop before F08

**Files:**
- Commit all reviewed structural-correction changes on `dev`.

- [ ] Create one focused commit for the coherent structural correction.
- [ ] Push normally to `origin/dev` without rewriting history.
- [ ] Report the exact requested status sections, command results, intentional old-path references, dependency outcome, deviations, and blockers.
- [ ] Stop; do not continue to F08 or implement production primitives/grid/forms/business UI.
