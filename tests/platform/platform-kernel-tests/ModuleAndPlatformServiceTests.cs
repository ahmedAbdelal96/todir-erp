using System.Text.Json;
using Todir.Platform.Modules;
using Todir.Platform.Messaging;
using Todir.Platform.Services;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace Todir.Platform.Tests;

public sealed class ModuleAndPlatformServiceTests
{
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
        var values = new[] { new SettingValue("locale", SettingScope.Tenant, "tenant", json, 1), new SettingValue("locale", SettingScope.Branch, "branch", JsonSerializer.SerializeToElement("ar"), 1) };
        Assert.Equal("ar", resolver.Resolve("locale", values, new Dictionary<string, SettingDefinition> { [definition.Key] = definition }).GetString());
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

    private static ModuleManifest Manifest(string id, IReadOnlyList<string> hard) => new(id, "1.0.0", 1, "L0", "kernel", false, "tenant", new(hard, []), new(id, []), [], new([], []), new([], []), new(false, "never_disable"));
}
