import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const requiredRunbooks = [
  "deploy.md",
  "rollback.md",
  "migration-forward-fix.md",
  "database-restore.md",
  "rabbitmq-outage.md",
  "outbox-backlog.md",
  "consumer-poison-message.md",
  "message-replay.md",
  "worker-outage.md",
  "object-storage-outage.md",
  "identity-incident.md",
  "secret-rotation.md",
  "support-access.md",
  "tenant-offboarding.md"
];
const requiredAdrs = [
  "modular-monolith.md",
  "tenant-isolation.md",
  "runtime-migration-roles.md",
  "module-schema-boundaries.md",
  "transaction-coordinator.md",
  "messaging-transport.md",
  "outbox-idempotency.md",
  "module-manifest-registry.md",
  "entitlement-activation.md",
  "module-lifecycle.md",
  "identity-authentication.md",
  "authorization-scopes.md",
  "uuidv7-identifiers.md",
  "time-policy.md",
  "numbering-strategy.md",
  "object-storage.md",
  "custom-fields.md",
  "import-framework.md",
  "observability.md",
  "migration-strategy.md"
];
const checks = [
  ["workflow disables premature package-manager cache", read(".github/workflows/repository-validation.yml").includes("package-manager-cache: false")],
  ["workflow enables Corepack before pnpm", /Enable Corepack[\s\S]*Verify pinned pnpm[\s\S]*pnpm install/.test(read(".github/workflows/repository-validation.yml"))],
  ["workflow runs migrator", read(".github/workflows/repository-validation.yml").includes("database-migrator/database-migrator.csproj")],
  ["workflow builds SHA-tagged images", read(".github/workflows/repository-validation.yml").includes('kernel-api:${GITHUB_SHA}')],
  ["workflow scans images and secrets", read(".github/workflows/repository-validation.yml").includes("aquasecurity/trivy-action") && read(".github/workflows/repository-validation.yml").includes("gitleaks/gitleaks-action")],
  ["workflow validates OpenTofu and restore", read(".github/workflows/repository-validation.yml").includes("tofu validate") && read(".github/workflows/repository-validation.yml").includes("pg_restore")],
  ["workflow verifies worker crash and quorum queue", read(".github/workflows/repository-validation.yml").includes("STAGING_EXPECT_WORKER_CRASH: true") && read("scripts/verification/staging-drills.mjs").includes("type=quorum")],
  ["runtime role cannot bypass RLS", read("ops/database/bootstrap/runtime-roles-and-schemas.sql").includes("NOBYPASSRLS")],
  ["tenant tables force RLS", read("ops/database/bootstrap/platform-kernel-schema.sql").includes("FORCE ROW LEVEL SECURITY")],
  ["migration history is module-scoped", read("src/backend/hosts/database-migrator/Program.cs").includes("module_id") && read("src/backend/hosts/database-migrator/Program.cs").includes("ModuleIdForScript")],
  ["inbox has lease and attempts", read("ops/database/bootstrap/platform-kernel-schema.sql").includes("lease_until") && read("ops/database/bootstrap/platform-kernel-schema.sql").includes("attempts")],
  ["metrics and alerts are wired", read("src/backend/shared/Todir.BuildingBlocks/Observability/observability-extensions.cs").includes("AddPrometheusExporter") && read("ops/monitoring/alerts.yml").includes("todir_outbox_oldest_pending_age_seconds")],
  ["threat model is reviewed", read("docs/security/platform-threat-model.md").includes("Cross-tenant leakage") && read("docs/security/platform-threat-model.md").includes("Support abuse")],
  ["verification record exists", read("docs/platform/kernel-verification.md").includes("focused platform tests (27)")]
];

for (const file of requiredRunbooks) checks.push([`runbook exists: ${file}`, fs.existsSync(path.join(root, "docs", "runbooks", file))]);
for (const file of requiredAdrs) checks.push([`ADR exists and is accepted: ${file}`, /Status: Accepted/.test(read(`docs/adr/${file}`))]);

const failures = checks.filter(([, passed]) => !passed).map(([name]) => name);
if (failures.length > 0) {
  console.error(failures.map(failure => `FAIL ${failure}`).join("\n"));
  process.exitCode = 1;
} else {
  console.log(`Validated ${checks.length} repository release-gate controls.`);
}
