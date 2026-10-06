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
  ["naming prohibition is recorded in applicable governance files", ["AGENTS.md", "src/backend/AGENTS.md", "src/backend/platform/AGENTS.md", "tests/AGENTS.md"].every(file => { const text = read(file).toLowerCase(); return text.includes("basename") && text.includes("todir"); })],
  ["workflow disables premature package-manager cache", read(".github/workflows/repository-validation.yml").includes("package-manager-cache: false")],
  ["workflow enables Corepack before pnpm", /Enable Corepack[\s\S]*Verify pinned pnpm[\s\S]*pnpm install/.test(read(".github/workflows/repository-validation.yml"))],
  ["workflow runs migrator", read(".github/workflows/repository-validation.yml").includes("database-migrator/database-migrator.csproj")],
  ["workflow builds SHA-tagged images", read(".github/workflows/repository-validation.yml").includes('kernel-api:${GITHUB_SHA}')],
  ["workflow scans images and secrets", read(".github/workflows/repository-validation.yml").includes("aquasecurity/trivy-action") && read(".github/workflows/repository-validation.yml").includes("gitleaks/gitleaks-action")],
  ["workflow validates OpenTofu and restore", read(".github/workflows/repository-validation.yml").includes("tofu validate") && read(".github/workflows/repository-validation.yml").includes("pg_restore")],
  ["workflow verifies worker crash and quorum queue", read(".github/workflows/repository-validation.yml").includes("STAGING_EXPECT_WORKER_CRASH: true") && read("scripts/verification/staging-drills.mjs").includes("type=quorum")],
  ["staging drills cover required load and recovery paths", ["authentication-load=", "module-registry-load=", "authorization-denials=", "external-tls-security-headers=passed", "session-cookie-security=passed", "broker-recovery=", "worker-restart=", "poison-error=recorded", "replay=audited", "auth-rate-limit-429="].every(marker => read("scripts/verification/staging-drills.mjs").includes(marker))],
  ["poison injection occurs before inbox completion and recovery waits for all messages", read("src/backend/platform/platform-kernel/Messaging/Messaging.cs").indexOf("ThrowIfPoison") < read("src/backend/platform/platform-kernel/Messaging/Messaging.cs").indexOf("TryBeginAsync") && read("scripts/verification/staging-drills.mjs").includes("recovered === messageIds.length + 1")],
  ["restore drill records measured evidence", ["backup-artifact-sha256=", "backup-duration-seconds=", "restore-duration-seconds=", "restore-schema-checksum=passed"].every(marker => read(".github/workflows/repository-validation.yml").includes(marker))],
  ["runtime role cannot bypass RLS", read("ops/database/bootstrap/runtime-roles-and-schemas.sql").includes("NOBYPASSRLS")],
  ["tenant tables force RLS", read("ops/database/bootstrap/platform-kernel-schema.sql").includes("FORCE ROW LEVEL SECURITY")],
  ["capability fallback is registered and tested", read("src/backend/platform/platform-kernel/Hosting/PlatformHostingExtensions.cs").includes("ICapabilityResolver") && read("tests/platform/platform-kernel-tests/ModuleAndPlatformServiceTests.cs").includes("CapabilityResolverUsesAnAvailableFallbackProvider")],
  ["module lifecycle supports resume, go-live, tenant-bound dependency impact, and reactivation", ["ActivateAsync", "CompleteGoLiveAsync", "CreateImpactReport", "DeactivateAsync", "candidate.TenantId == instance.TenantId"].every(marker => read("src/backend/platform/platform-kernel/Modules/ModuleSystem.cs").includes(marker)) && ["LifecycleProvisioningResumesAndActivationIsIdempotent", "StatefulLifecycleRequiresGoLiveAndSupportsOptionalFallback", "LifecycleReactivationPreservesInstanceIdentityAndConfiguration"].every(marker => read("tests/platform/platform-kernel-tests/ModuleAndPlatformServiceTests.cs").includes(marker))],
  ["database numbering concurrency gate exists", read("tests/integration/kernel-integration-tests/NumberingIntegrationTests.cs").includes("Task.WhenAll") && read("tests/integration/kernel-integration-tests/NumberingIntegrationTests.cs").includes("INV-00050")],
  ["transaction gate spans multiple module schemas and outbox", read("tests/integration/kernel-integration-tests/TransactionIntegrationTests.cs").includes("platform_org.legal_entity") && read("tests/integration/kernel-integration-tests/TransactionIntegrationTests.cs").includes("platform_authz.role") && read("tests/integration/kernel-integration-tests/TransactionIntegrationTests.cs").includes("platform_messaging.outbox_message")],
  ["scheduler is persistent clustered and non-concurrent", read("src/backend/platform/platform-kernel/Scheduling/SchedulingHostingExtensions.cs").includes("UsePersistentStore") && read("src/backend/platform/platform-kernel/Scheduling/SchedulingHostingExtensions.cs").includes("UseClustering") && read("src/backend/platform/platform-kernel/Scheduling/PlatformMaintenanceJob.cs").includes("DisallowConcurrentExecution")],
  ["platform service foundations have owned implementations and tests", ["NpgsqlAuditWriter", "NpgsqlSequenceAllocator", "SettingsResolver", "FileObjectKeys", "CustomFieldRegistry", "NotificationDispatcher", "NpgsqlImportJobScheduler"].every(marker => read("src/backend/platform/platform-kernel/Services/PlatformServices.cs").includes(marker) || read("src/backend/platform/platform-kernel/Services/NotificationServices.cs").includes(marker) || read("src/backend/platform/platform-kernel/Services/ImportSchedulingServices.cs").includes(marker)) && read("tests/platform/platform-kernel-tests/ModuleAndPlatformServiceTests.cs").includes("SettingsChooseMostSpecificOverrideAndValidateType")],
  ["migration history is module-scoped", read("src/backend/hosts/database-migrator/Program.cs").includes("module_id") && read("src/backend/hosts/database-migrator/Program.cs").includes("ModuleIdForScript") && read("src/backend/hosts/database-migrator/Program.cs").includes("add primary key (module_id, script_name)")],
  ["inbox has lease and attempts", read("ops/database/bootstrap/platform-kernel-schema.sql").includes("lease_until") && read("ops/database/bootstrap/platform-kernel-schema.sql").includes("attempts")],
  ["messaging faults are recorded and replay is audited", read("src/backend/platform/platform-kernel/Messaging/MessageRecovery.cs").includes("MessageFailureObserver") && read("src/backend/platform/platform-kernel/Messaging/MessageRecovery.cs").includes("IMessageReplayService") && read("src/backend/platform/platform-kernel/Messaging/MessageRecovery.cs").includes("message_replayed")],
  ["message replay is scoped to the requested consumer endpoint", read("src/backend/platform/platform-kernel/Messaging/MessageRecovery.cs").includes("consumer_endpoint = $3") && read("src/backend/platform/platform-kernel/Hosting/PlatformHostingExtensions.cs").includes("request.ConsumerEndpoint")],
  ["authenticated replay surface is contracted", read("src/backend/platform/platform-kernel/Hosting/PlatformHostingExtensions.cs").includes("/messaging/replay") && read("docs/api/platform-openapi.json").includes("/api/v1/messaging/replay")],
  ["OpenAPI request bodies declare content schemas", read("scripts/architecture/validate-openapi.mjs").includes("request body without content") && read("docs/api/platform-openapi.json").includes("MessageReplayRequest")],
  ["staging can exercise poison and replay", read(".github/workflows/repository-validation.yml").includes("crash-after-inbox-commit,poison-message") && read("src/backend/platform/platform-kernel/Messaging/Messaging.cs").includes("StagingPoisonV1")],
  ["staging replay route requires an ephemeral token", read("src/backend/platform/platform-kernel/Hosting/PlatformHostingExtensions.cs").includes("TODIR_STAGING_VERIFICATION") && read("src/backend/platform/platform-kernel/Hosting/PlatformHostingExtensions.cs").includes("!string.IsNullOrWhiteSpace(app.Configuration[\"TODIR_STAGING_REPLAY_TOKEN\"])") && read("ops/terraform/main.tf").includes("TODIR_STAGING_VERIFICATION=${var.staging_replay_token")],
  ["metrics and alerts are wired", read("src/backend/shared/Todir.BuildingBlocks/Observability/observability-extensions.cs").includes("AddPrometheusExporter") && read("ops/monitoring/alerts.yml").includes("todir_outbox_oldest_pending_age_seconds")],
  ["threat model is reviewed", read("docs/security/platform-threat-model.md").includes("Cross-tenant leakage") && read("docs/security/platform-threat-model.md").includes("Support abuse")],
  ["verification record exists", /focused platform tests \(34\)/.test(read("docs/platform/kernel-verification.md"))]
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
