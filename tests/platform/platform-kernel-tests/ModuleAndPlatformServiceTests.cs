using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Todir.Platform.Entitlements;
using Todir.Platform.Modules;
using Todir.Platform.Messaging;
using Todir.Platform.Services;
using Todir.Platform.Security;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace Todir.Platform.Tests;

public sealed class ModuleAndPlatformServiceTests
{
    [Theory]
    [InlineData(NotificationChannel.InApp)]
    [InlineData(NotificationChannel.Email)]
    public async Task NotificationDispatcherKeepsProviderDetailsOutsideTheCallingModule(NotificationChannel channel)
    {
        var sender = new RecordingNotificationSender(channel);
        var dispatcher = new NotificationDispatcher(new InMemoryNotificationStore(), [sender]);
        var notification = await dispatcher.EnqueueAsync(TenantId.New(), "identity:member", "membership.invited", channel, JsonSerializer.SerializeToElement(new { displayName = "Member" }));

        Assert.True(await dispatcher.DispatchNextAsync(notification.TenantId));
        Assert.Single(sender.Sent);
        Assert.Equal(notification.Id, sender.Sent[0].Id);
        Assert.False(await dispatcher.DispatchNextAsync(notification.TenantId));
    }

    [Fact]
    public void RegistryReturnsDependencyFirstOrderAndRejectsCycles()
    {
        var manifests = new[]
        {
            Manifest("base", []),
            Manifest("feature", ["base"]),
            Manifest("screen", ["feature"])
        };
        var registry = new ModuleRegistry(manifests);
        Assert.Equal(["base", "feature", "screen"], registry.TopologicalOrder());
        Assert.Equal(["feature"], registry.GetDependents("base"));
        Assert.Throws<InvalidOperationException>(() => new ModuleRegistry([Manifest("a", ["b"]), Manifest("b", ["a"])]));
    }

    [Fact]
    public async Task SequenceAllocatorIsUniqueAndDeterministicUnderConcurrency()
    {
        var allocator = new InMemorySequenceAllocator();
        var definition = new SequenceDefinition("invoice", TenantId.New(), null, null, null, "2026", "INV-", 4, 1);
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => allocator.AllocateAsync(definition)));
        Assert.Equal(50, results.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("INV-0001", results);
        Assert.Contains("INV-0050", results);
    }

    [Fact]
    public void SettingsChooseMostSpecificOverrideAndValidateType()
    {
        var json = JsonSerializer.SerializeToElement("en");
        var definition = new SettingDefinition("locale", "string", SettingScope.Tenant, json, false);
        var resolver = new SettingsResolver();
        var definitions = new Dictionary<string, SettingDefinition> { [definition.Key] = definition };
        var values = new[] { new SettingValue("locale", SettingScope.Tenant, "tenant", json, 1), new SettingValue("locale", SettingScope.LegalEntity, "entity", JsonSerializer.SerializeToElement("fr"), 1), new SettingValue("locale", SettingScope.Branch, "branch", JsonSerializer.SerializeToElement("ar"), 1), new SettingValue("locale", SettingScope.User, "user", JsonSerializer.SerializeToElement("en-US"), 1) };
        Assert.Equal("en-US", resolver.Resolve("locale", values, definitions).GetString());
        Assert.Equal("ar", resolver.Resolve("locale", values.Where(value => value.Scope != SettingScope.User).ToArray(), definitions).GetString());
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("locale", [new SettingValue("locale", SettingScope.Tenant, "tenant", JsonSerializer.SerializeToElement(7), 1)], definitions));
    }

    [Fact]
    public void FileKeyCannotDependOnUntrustedFilename()
    {
        var file = new FileRecord(Guid.NewGuid(), TenantId.New(), "sales", "order", Guid.NewGuid(), "ignored", "../../secret.txt", "text/plain", 1, "hash", FileStatus.Ready, "normal", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var key = FileObjectKeys.Create(file);
        Assert.DoesNotContain("secret.txt", key, StringComparison.Ordinal);
        Assert.StartsWith("tenant/", key, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomFieldRenamePreservesStableReference()
    {
        var tenant = TenantId.New();
        var definition = new CustomFieldDefinition(Guid.CreateVersion7(), tenant, "invoice", "payment_reference", "مرجع الدفع", "Payment reference", "string", false, null, null, null, true, 1);
        var registry = new CustomFieldRegistry();
        registry.Register(definition);
        registry.Rename(tenant, "invoice", "payment_reference", "رقم الدفع", "Payment number");
        var renamed = registry.Find(tenant, "invoice", "payment_reference");
        Assert.Equal("payment_reference", renamed.StableKey);
        Assert.Equal("Payment number", renamed.LabelEn);
        Assert.Equal(2, renamed.Version);
    }

    [Fact]
    public void MessagingRetryIsBoundedAndUsesBackoff()
    {
        var policy = new ExponentialMessageRetryPolicy(2, TimeSpan.FromMinutes(1));
        var now = DateTimeOffset.UtcNow;
        var first = policy.Decide(1, now, new InvalidOperationException("broker"));
        var exhausted = policy.Decide(2, now, new InvalidOperationException("broker"));
        Assert.True(first.Retry);
        Assert.Equal(now.AddSeconds(2), first.NextAttemptAt);
        Assert.False(exhausted.Retry);
        Assert.Equal("messaging.retry_exhausted", exhausted.ErrorCode);
    }

    [Fact]
    public async Task MessageErrorsCanBeRecordedAndResolvedWithoutExposingPayloads()
    {
        var store = new InMemoryMessageErrorStore();
        var tenant = TenantId.New();
        var messageId = Guid.CreateVersion7();
        await store.RecordAsync(new MessageError(messageId, tenant, "platform-outbox-probe", 1, "message.consume_failed", "InvalidOperationException", DateTimeOffset.UtcNow, false));

        Assert.False(store.Snapshot().Single().Resolved);
        await store.MarkResolvedAsync(messageId, "platform-outbox-probe", tenant);
        Assert.True(store.Snapshot().Single().Resolved);
    }

    [Fact]
    public async Task InboxRejectsConcurrentDuplicatesAndAllowsOnlyOneCompletedEffect()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryInboxStore(clock, TimeSpan.FromMinutes(5));
        var tenant = TenantId.New();
        var messageId = Guid.CreateVersion7();

        Assert.True(await store.TryBeginAsync("platform-outbox-probe", messageId, tenant));
        Assert.False(await store.TryBeginAsync("platform-outbox-probe", messageId, tenant));
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await store.TryBeginAsync("platform-outbox-probe", messageId, tenant));
        await store.MarkProcessedAsync("platform-outbox-probe", messageId, tenant);
        Assert.False(await store.TryBeginAsync("platform-outbox-probe", messageId, tenant));
        Assert.False(await store.TryBeginAsync("platform-outbox-probe", messageId, TenantId.New()));
    }

    [Fact]
    public void EntitlementSnapshotsRequireSignatureAndInvalidateCapabilityAvailability()
    {
        var tenant = TenantId.New();
        var key = Encoding.UTF8.GetBytes("entitlement-key");
        var verifier = new HmacEntitlementVerifier(new Dictionary<string, byte[]> { ["current"] = key });
        var issued = DateTimeOffset.UtcNow;
        var snapshot = new EntitlementSnapshot(tenant, 1, issued, issued.AddMinutes(10), "current", new HashSet<string>(["sample"]), new HashSet<string>(), new Dictionary<string, long>(), "");
        var canonical = JsonSerializer.Serialize(new { tenant = tenant.Value, snapshot = snapshot.SnapshotVersion, issued = snapshot.IssuedAt, valid = snapshot.ValidUntil, modules = snapshot.Modules.OrderBy(value => value, StringComparer.Ordinal), features = snapshot.Features.OrderBy(value => value, StringComparer.Ordinal), limits = snapshot.Limits.OrderBy(pair => pair.Key, StringComparer.Ordinal) });
        snapshot = snapshot with { Signature = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical))) };
        var guard = new EntitlementGuard(verifier);
        guard.SetSnapshot(snapshot);
        Assert.True(guard.IsEntitled(tenant, "sample"));
        Assert.False(guard.IsEntitled(tenant, "missing"));
        Assert.Throws<InvalidOperationException>(() => guard.SetSnapshot(snapshot with { Signature = "invalid" }));
    }

    [Fact]
    public void CapabilityResolverUsesAnAvailableFallbackProvider()
    {
        var tenant = TenantId.New();
        var unavailable = new FallbackCapability(false);
        var fallback = new FallbackCapability(true);
        var resolver = new CapabilityResolver([unavailable, fallback]);

        Assert.True(resolver.IsAvailable<FallbackCapability>(new CapabilityContext(tenant)));
        Assert.Same(fallback, resolver.Resolve<FallbackCapability>(new CapabilityContext(tenant)));
    }

    [Fact]
    public void CapabilityChecksRemainTenantBoundUnderLoad()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var key = Encoding.UTF8.GetBytes("load-entitlement-key");
        var verifier = new HmacEntitlementVerifier(new Dictionary<string, byte[]> { ["current"] = key });
        var issued = DateTimeOffset.UtcNow;
        var snapshot = new EntitlementSnapshot(tenant, 1, issued, issued.AddMinutes(10), "current", new HashSet<string>(["sample"]), new HashSet<string>(), new Dictionary<string, long>(), "");
        var canonical = JsonSerializer.Serialize(new { tenant = tenant.Value, snapshot = snapshot.SnapshotVersion, issued = snapshot.IssuedAt, valid = snapshot.ValidUntil, modules = snapshot.Modules.OrderBy(value => value, StringComparer.Ordinal), features = snapshot.Features.OrderBy(value => value, StringComparer.Ordinal), limits = snapshot.Limits.OrderBy(pair => pair.Key, StringComparer.Ordinal) });
        var guard = new EntitlementGuard(verifier);
        guard.SetSnapshot(snapshot with { Signature = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical))) });
        var file = new FileRecord(Guid.NewGuid(), tenant, "kernel", "load", Guid.NewGuid(), "ignored", "load.bin", "application/pdf", 4, "hash", FileStatus.Ready, "internal", Guid.NewGuid(), issued);
        var signer = new HmacFileAccessSigner("load-file-secret");
        var expiresAt = issued.AddMinutes(5);
        var signature = signer.CreateDownloadUrl(file, tenant, expiresAt).Split("signature=", StringSplitOptions.None)[1];
        var checkpoints = new InMemoryImportCheckpointStore();

        for (var chunk = 0; chunk < 1_000; chunk++)
        {
            Assert.True(guard.IsEntitled(tenant, "sample"));
            Assert.False(guard.IsEntitled(otherTenant, "sample"));
            Assert.True(signer.VerifyDownload(file, tenant, expiresAt, signature, issued));
            Assert.False(signer.VerifyDownload(file, otherTenant, expiresAt, signature, issued));
            var checkpoint = new ImportCheckpoint(Guid.CreateVersion7(), chunk, chunk * 100, 100, $"digest-{chunk}", issued);
            Assert.True(checkpoints.TrySave(checkpoint));
            Assert.False(checkpoints.TrySave(checkpoint));
        }
    }

    [Fact]
    public async Task ImportSchedulerQueuesExecutableChunksExactlyOnceInMemory()
    {
        var tenant = TenantId.New();
        var scheduler = new InMemoryImportJobScheduler();
        var batch = new ImportBatch(Guid.CreateVersion7(), tenant, "directory", "party-import", ImportStatus.Execute, Guid.CreateVersion7(), JsonSerializer.SerializeToElement(new { mapping = "stable" }), 0, Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var job = await scheduler.ScheduleChunkAsync(batch, 100, 50);
        var duplicate = await scheduler.ScheduleChunkAsync(batch, 100, 50);

        Assert.Equal(batch.Id, job.BatchId);
        Assert.Equal(job, duplicate);
        Assert.True(scheduler.TryDequeue(out var queued));
        Assert.Equal(job, queued);
        Assert.False(scheduler.TryDequeue(out _));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.ScheduleChunkAsync(batch with { Status = ImportStatus.Validate }, 0, 10));
    }

    [Fact]
    public async Task ImportSchedulerRemainsIdempotentUnderConcurrentChunkLoad()
    {
        var tenant = TenantId.New();
        var scheduler = new InMemoryImportJobScheduler();
        var batch = new ImportBatch(Guid.CreateVersion7(), tenant, "directory", "party-import", ImportStatus.Execute, Guid.CreateVersion7(), JsonSerializer.SerializeToElement(new { mapping = "stable" }), 0, Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var duplicateCalls = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => scheduler.ScheduleChunkAsync(batch, 0, 100)));
        Assert.Single(duplicateCalls.Distinct());

        var uniqueCalls = await Task.WhenAll(Enumerable.Range(1, 100).Select(chunk => scheduler.ScheduleChunkAsync(batch, chunk * 100, 100)));
        Assert.Equal(100, uniqueCalls.Select(job => job.StartRow).Distinct().Count());
        var dequeued = 0;
        while (scheduler.TryDequeue(out _)) dequeued++;
        Assert.Equal(101, dequeued);
    }

    [Fact]
    public void LifecycleBlocksUnentitledActivationAndDependentDeactivation()
    {
        var registry = new ModuleRegistry([Manifest("base", []), Manifest("feature", ["base"])]);
        var lifecycle = new ModuleLifecycleService(registry);
        var instance = new ModuleInstance("instance", TenantId.New().Value.ToString("D"), "feature", "tenant", null, ModuleInstanceStatus.Requested, 1);
        Assert.Throws<InvalidOperationException>(() => lifecycle.Activate(instance, false));
        var validating = lifecycle.Activate(instance, true);
        Assert.Equal(ModuleInstanceStatus.Validating, validating.Status);
        Assert.Throws<InvalidOperationException>(() => ModuleLifecycleService.Deactivate(validating, true));
    }

    [Fact]
    public async Task LifecycleProvisioningResumesAndActivationIsIdempotent()
    {
        var registry = new ModuleRegistry([Manifest("base", []), Manifest("feature", ["base"])]);
        var contributor = new RecordingLifecycleContributor("feature") { FailProvisionOnce = true };
        var lifecycle = new ModuleLifecycleService(registry, [contributor]);
        var tenant = TenantId.New().Value.ToString("D");
        var baseInstance = new ModuleInstance("base-instance", tenant, "base", "tenant", null, ModuleInstanceStatus.Active, 3);
        var feature = new ModuleInstance("feature-instance", tenant, "feature", "tenant", "configuration-1", ModuleInstanceStatus.Requested, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ActivateAsync(feature, true, "activate-1", [baseInstance]));
        var activated = await lifecycle.ActivateAsync(feature, true, "activate-1", [baseInstance]);
        var retry = await lifecycle.ActivateAsync(feature, true, "activate-1", [baseInstance]);

        Assert.Equal(ModuleInstanceStatus.Active, activated.Status);
        Assert.Equal(activated, retry);
        Assert.Equal(1, contributor.ValidationCalls);
        Assert.Equal(2, contributor.ProvisionCalls);
        Assert.Equal("configuration-1", retry.ScopeId);
    }

    [Fact]
    public async Task StatefulLifecycleRequiresGoLiveAndSupportsOptionalFallback()
    {
        var registry = new ModuleRegistry([Manifest("stateful", [], true, "wind_down")]);
        var contributor = new RecordingLifecycleContributor("stateful");
        var lifecycle = new ModuleLifecycleService(registry, [contributor]);
        var instance = new ModuleInstance("stateful-instance", TenantId.New().Value.ToString("D"), "stateful", "tenant", null, ModuleInstanceStatus.Requested, 1);

        var awaiting = await lifecycle.ActivateAsync(instance, true, "activate-1", []);
        var active = await lifecycle.CompleteGoLiveAsync(instance, "activate-1");
        var impact = lifecycle.CreateImpactReport(active, [], ["optional.approvals"], ["open-operation"]);
        var windDown = await lifecycle.DeactivateAsync(active, impact, "deactivate-1");

        Assert.Equal(ModuleInstanceStatus.AwaitingGoLive, awaiting.Status);
        Assert.Equal(ModuleInstanceStatus.Active, active.Status);
        Assert.True(impact.RequiresGoLive);
        Assert.Single(impact.OptionalCapabilitiesLost);
        Assert.Equal(ModuleInstanceStatus.WindDown, windDown.Status);
        Assert.Equal(1, contributor.GoLiveCalls);
        Assert.Equal(1, contributor.WindDownCalls);
    }

    [Fact]
    public async Task LifecycleBlocksMissingDependenciesAndHardDependents()
    {
        var registry = new ModuleRegistry([Manifest("base", []), Manifest("feature", ["base"]), Manifest("dependent", ["base"])]);
        var lifecycle = new ModuleLifecycleService(registry);
        var tenant = TenantId.New().Value.ToString("D");
        var baseInstance = new ModuleInstance("base-instance", tenant, "base", "tenant", null, ModuleInstanceStatus.Active, 1);
        var feature = new ModuleInstance("feature-instance", tenant, "feature", "tenant", null, ModuleInstanceStatus.Requested, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ActivateAsync(feature, true, "activate-1", []));
        var otherTenantBase = baseInstance with { TenantId = TenantId.New().Value.ToString("D") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ActivateAsync(feature, true, "activate-2", [otherTenantBase]));
        var dependent = new ModuleInstance("dependent-instance", tenant, "dependent", "tenant", null, ModuleInstanceStatus.Active, 1);
        var impact = lifecycle.CreateImpactReport(baseInstance, [dependent]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.DeactivateAsync(baseInstance, impact, "deactivate-1"));
    }

    [Fact]
    public async Task LifecycleReactivationPreservesInstanceIdentityAndConfiguration()
    {
        var registry = new ModuleRegistry([Manifest("base", [])]);
        var lifecycle = new ModuleLifecycleService(registry);
        var instance = new ModuleInstance("base-instance", TenantId.New().Value.ToString("D"), "base", "tenant", "retained-configuration", ModuleInstanceStatus.Disabled, 8);

        var reactivated = await lifecycle.ActivateAsync(instance, true, "reactivate-1", []);

        Assert.Equal(ModuleInstanceStatus.Active, reactivated.Status);
        Assert.Equal(instance.Id, reactivated.Id);
        Assert.Equal(instance.ScopeId, reactivated.ScopeId);
        Assert.True(reactivated.Version > instance.Version);
    }

    private static ModuleManifest Manifest(string id, IReadOnlyList<string> hard, bool supportsGoLive = false, string deactivationPolicy = "never_disable") => new(id, "1.0.0", 1, "L0", "kernel", false, "tenant", new(hard, []), new(id, []), [], new([], []), new([], []), new(supportsGoLive, deactivationPolicy));

    private sealed class TestTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }

    private sealed class RecordingNotificationSender(NotificationChannel channel) : INotificationSender
    {
        public NotificationChannel Channel => channel;
        public List<Notification> Sent { get; } = [];
        public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FallbackCapability(bool available) : ICapabilityProvider, IScopedCapabilityProvider
    {
        public string Capability => "verification.fallback";
        public bool IsAvailable(CapabilityContext context) => available;
    }

    private sealed class RecordingLifecycleContributor(string moduleId) : IModuleLifecycleContributor
    {
        public string ModuleId => moduleId;
        public bool FailProvisionOnce { get; set; }
        public int ValidationCalls { get; private set; }
        public int ProvisionCalls { get; private set; }
        public int GoLiveCalls { get; private set; }
        public int WindDownCalls { get; private set; }

        public Task ValidateActivationAsync(ModuleInstance instance, CancellationToken cancellationToken = default)
        {
            ValidationCalls++;
            return Task.CompletedTask;
        }

        public Task ProvisionAsync(ModuleInstance instance, CancellationToken cancellationToken = default)
        {
            ProvisionCalls++;
            if (FailProvisionOnce)
            {
                FailProvisionOnce = false;
                throw new InvalidOperationException("controlled.provisioning_failure");
            }
            return Task.CompletedTask;
        }

        public Task PrepareGoLiveAsync(ModuleInstance instance, CancellationToken cancellationToken = default)
        {
            GoLiveCalls++;
            return Task.CompletedTask;
        }

        public Task WindDownAsync(ModuleInstance instance, CancellationToken cancellationToken = default)
        {
            WindDownCalls++;
            return Task.CompletedTask;
        }
    }
}
