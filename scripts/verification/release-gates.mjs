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
  "migration-strategy.md",
  "frontend-platform-foundation.md"
];
const checks = [
  ["naming prohibition is recorded in applicable governance files", ["AGENTS.md", "backend/AGENTS.md", "frontend/AGENTS.md"].every(file => { const text = read(file).toLowerCase(); return text.includes("basename") && text.includes("todir"); })],
  ["workflow disables premature package-manager cache", read(".github/workflows/repository-validation.yml").includes("package-manager-cache: false")],
  ["workflow enables Corepack before pnpm", /Enable Corepack[\s\S]*Verify pinned pnpm[\s\S]*pnpm install/.test(read(".github/workflows/repository-validation.yml"))],
  ["workflow runs migrator", read(".github/workflows/repository-validation.yml").includes("DbMigrator/DbMigrator.csproj")],
  ["workflow builds SHA-tagged images", read(".github/workflows/repository-validation.yml").includes('kernel-api:${GITHUB_SHA}')],
  ["workflow scans images and secrets", read(".github/workflows/repository-validation.yml").includes("aquasecurity/trivy-action@v0.36.0") && read(".github/workflows/repository-validation.yml").includes("gitleaks/gitleaks-action")],
  ["workflow validates OpenTofu and restore", read(".github/workflows/repository-validation.yml").includes("tofu validate") && read(".github/workflows/repository-validation.yml").includes("pg_restore")],
  ["workflow verifies clustered workers, worker crash, scheduler restart, and quorum queue", read(".github/workflows/repository-validation.yml").includes("STAGING_EXPECT_WORKER_CRASH: true") && read(".github/workflows/repository-validation.yml").includes("STAGING_WORKER_SECONDARY_CONTAINER") && read("scripts/verification/staging-drills.mjs").includes("clustered-workers=2") && read("scripts/verification/staging-drills.mjs").includes("scheduler-expiry=passed") && read("scripts/verification/staging-drills.mjs").includes("type=quorum") && read("backend/Core/Scheduling/SchedulingHostingExtensions.cs").includes("TODIR_MAINTENANCE_INTERVAL_SECONDS")],
  ["staging drills cover required load and recovery paths", ["authentication-load=", "module-registry-load=", "authorization-denials=", "external-tls-security-headers=passed", "session-cookie-security=passed", "broker-recovery=", "worker-restart=", "scheduler-expiry=passed", "poison-error=recorded", "replay=audited", "auth-rate-limit-429="].every(marker => read("scripts/verification/staging-drills.mjs").includes(marker))],
  ["P28 load workloads have executable evidence", ["RegistryPermissionEntitlementAndSignedUrlLoadsRemainTenantBound", "AuditAndImportLoadsPreserveTenantIdentityAndIdempotency"].every(marker => read("backend/Tests/Core/KernelLoadVerificationTests.cs").includes(marker)) && read("backend/Tests/Integration/BrokerIntegrationTests.cs").includes("RabbitMqDrainsARealConsumerThroughputBurstWithoutDuplicates")],
  ["tenant user administration has an HTTP pipeline verification", ["/users", "TenantBoundUserAdministrationRunsThroughTheHttpPipeline"].every(marker => read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes(marker) || read("backend/Tests/Integration/PlatformApiFlowTests.cs").includes(marker))],
  ["platform admin surfaces and real organization context are HTTP-verified", ["/session/context", "/context/organization", "/tenants/{tenantId:guid}/roles", "/tenants/{tenantId:guid}/settings", "/tenants/{tenantId:guid}/audit", "IPlatformAdminSurfaceStore"].every(marker => read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes(marker) || read("backend/Core/Services/PlatformAdminSurfaces.cs").includes(marker)) && read("backend/Tests/Integration/PlatformApiFlowTests.cs").includes("/api/v1/context/organization") && read("frontend/app/shell/app-shell.tsx").includes("/api/v1/session/context")],
  ["Sample endpoint enforces and reloads signed entitlement snapshots", read("backend/Tests/Integration/PlatformApiFlowTests.cs").includes("SampleEndpointRequiresEntitlementThenLoadsTheTenantSnapshot") && read("backend/Modules/Sample/SampleModuleExtensions.cs").includes("LoadLatestAsync")],
  ["poison injection occurs before inbox completion and recovery waits for all messages", read("backend/Core/Messaging/Messaging.cs").indexOf("ThrowIfPoison") < read("backend/Core/Messaging/Messaging.cs").indexOf("TryBeginAsync") && read("scripts/verification/staging-drills.mjs").includes("recovered === normalMessageIds.length + 1")],
  ["restore drill records measured evidence", ["backup-artifact-sha256=", "backup-duration-seconds=", "restore-duration-seconds=", "restore-schema-checksum=passed"].every(marker => read(".github/workflows/repository-validation.yml").includes(marker))],
  ["runtime role cannot bypass RLS", read("backend/DbMigrator/Database/bootstrap/runtime-roles-and-schemas.sql").includes("NOBYPASSRLS")],
  ["tenant tables force RLS", read("backend/DbMigrator/Database/bootstrap/platform-rls-finalization.sql").includes("FORCE ROW LEVEL SECURITY")],
  ["capability fallback is registered and tested", read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes("ICapabilityResolver") && read("backend/Tests/Core/ModuleAndPlatformServiceTests.cs").includes("CapabilityResolverUsesAnAvailableFallbackProvider")],
  ["SampleModuleA and SampleModuleB fixtures prove manifest graph behavior", ["backend/Tests/Architecture/fixtures/modules/sample-a/module.manifest.json", "backend/Tests/Architecture/fixtures/modules/sample-b/module.manifest.json"].every(file => fs.existsSync(path.join(root, file))) && read("backend/Tests/Core/SampleModuleTests.cs").includes("SampleModuleAFixturesResolveBeforeSampleModuleBAndRejectInvalidGraphs")],
  ["sample module verification slice is registered and integration-tested", read("backend/Modules/Sample/module.manifest.json").includes('"id": "sample"') && read("backend/Modules/Sample/SampleItems.cs").includes("SampleEntityCreatedV1") && read("backend/Modules/Sample/SampleModuleExtensions.cs").includes("sample.item.create") && read("backend/Tests/Integration/SampleModuleIntegrationTests.cs").includes("SampleCreateWritesModuleAuditAndOutboxAtomically")],
  ["entitlement snapshots load through tenant-bound persistence", read("backend/Core/Entitlements/EntitlementStore.cs").includes("LoadLatestAsync") && read("backend/Tests/Integration/EntitlementIntegrationTests.cs").includes("LatestTenantSnapshotLoadsThroughTheRlsBoundStore")],
  ["module lifecycle supports resume, go-live, tenant-bound dependency impact, and reactivation", ["ActivateAsync", "CompleteGoLiveAsync", "CreateImpactReport", "DeactivateAsync", "candidate.TenantId == instance.TenantId"].every(marker => read("backend/Core/Modules/ModuleSystem.cs").includes(marker)) && ["LifecycleProvisioningResumesAndActivationIsIdempotent", "StatefulLifecycleRequiresGoLiveAndSupportsOptionalFallback", "LifecycleReactivationPreservesInstanceIdentityAndConfiguration"].every(marker => read("backend/Tests/Core/ModuleAndPlatformServiceTests.cs").includes(marker))],
  ["database numbering concurrency gate exists", read("backend/Tests/Integration/NumberingIntegrationTests.cs").includes("Task.WhenAll") && read("backend/Tests/Integration/NumberingIntegrationTests.cs").includes("INV-00050")],
  ["transaction gate spans SampleModuleA and SampleModuleB schemas with RLS and outbox", read("backend/Tests/Integration/TransactionIntegrationTests.cs").includes("SampleModuleAAndBContextsCommitAndRollbackWithTenantRlsAndOutbox") && read("backend/Tests/Integration/TransactionIntegrationTests.cs").includes("sample_a.entity") && read("backend/Tests/Integration/TransactionIntegrationTests.cs").includes("sample_b.entity") && read("backend/Tests/Integration/TransactionIntegrationTests.cs").includes("platform.outbox_message")],
  ["scheduler is persistent clustered and non-concurrent", read("backend/Core/Scheduling/SchedulingHostingExtensions.cs").includes("UsePersistentStore") && read("backend/Core/Scheduling/SchedulingHostingExtensions.cs").includes("UseClustering") && read("backend/Core/Scheduling/PlatformMaintenanceJob.cs").includes("DisallowConcurrentExecution")],
  ["platform service foundations have owned implementations and tests", ["NpgsqlAuditWriter", "NpgsqlSequenceAllocator", "SettingsResolver", "FileObjectKeys", "CustomFieldRegistry", "NotificationDispatcher", "NpgsqlImportJobScheduler"].every(marker => read("backend/Core/Services/PlatformServices.cs").includes(marker) || read("backend/Core/Services/NotificationServices.cs").includes(marker) || read("backend/Core/Services/ImportSchedulingServices.cs").includes(marker)) && read("backend/Tests/Core/ModuleAndPlatformServiceTests.cs").includes("SettingsChooseMostSpecificOverrideAndValidateType")],
  ["migration history is module-scoped", read("backend/DbMigrator/Program.cs").includes("module_id") && read("backend/DbMigrator/Program.cs").includes("ModuleIdForScript") && read("backend/DbMigrator/Program.cs").includes("primary key (module_id, script_name)") && read("backend/DbMigrator/Program.cs").includes("ReadAllTextAsync(runtimeScript)") && read("backend/DbMigrator/Program.cs").includes("backend/DbMigrator/Database") && read("backend/DbMigrator/Program.cs").includes("grant create on database")],
  ["inbox has lease and attempts", read("backend/DbMigrator/Database/bootstrap/platform-kernel-schema.sql").includes("lease_until") && read("backend/DbMigrator/Database/bootstrap/platform-kernel-schema.sql").includes("attempts")],
  ["messaging faults are recorded and replay is audited", read("backend/Core/Messaging/MessageRecovery.cs").includes("MessageFailureObserver") && read("backend/Core/Messaging/MessageRecovery.cs").includes("IMessageReplayService") && read("backend/Core/Messaging/MessageRecovery.cs").includes("message_replayed")],
  ["message replay is scoped to the requested consumer endpoint", read("backend/Core/Messaging/MessageRecovery.cs").includes("consumer_endpoint = $3") && read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes("request.ConsumerEndpoint")],
  ["authenticated replay surface is contracted", read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes("/messaging/replay") && read("docs/api/platform-openapi.json").includes("/api/v1/messaging/replay")],
  ["OpenAPI request bodies declare content schemas", read("scripts/architecture/validate-openapi.mjs").includes("request body without content") && read("docs/api/platform-openapi.json").includes("MessageReplayRequest")],
  ["staging can exercise poison and replay", read(".github/workflows/repository-validation.yml").includes("crash-after-inbox-commit,poison-message") && read("backend/Core/Messaging/Messaging.cs").includes("StagingPoisonV1")],
  ["staging exercises the Sample event consumer and signed entitlement path", read("scripts/verification/staging-drills.mjs").includes("SampleEntityCreatedV1") && read("scripts/verification/staging-drills.mjs").includes("sample-projection") && read("ops/terraform/main.tf").includes("TODIR_ENTITLEMENT_HMAC_KEY") && read(".github/workflows/repository-validation.yml").includes("STAGING_ENTITLEMENT_HMAC_KEY")],
  ["staging replay route requires an ephemeral token", read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes("TODIR_STAGING_VERIFICATION") && read("backend/Core/Hosting/PlatformHostingExtensions.cs").includes("!string.IsNullOrWhiteSpace(app.Configuration[\"TODIR_STAGING_REPLAY_TOKEN\"])") && read("ops/terraform/main.tf").includes("TODIR_STAGING_VERIFICATION=${var.staging_replay_token")],
  ["metrics and alerts are wired", read("backend/Shared/BuildingBlocks/Observability/observability-extensions.cs").includes("AddPrometheusExporter") && read("ops/monitoring/alerts.yml").includes("todir_outbox_oldest_pending_age_seconds")],
  ["threat model is reviewed", read("docs/security/platform-threat-model.md").includes("Cross-tenant leakage") && read("docs/security/platform-threat-model.md").includes("Support abuse")],
  ["verification record exists", /focused platform tests \(38\)/.test(read("docs/platform/kernel-verification.md"))]
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
