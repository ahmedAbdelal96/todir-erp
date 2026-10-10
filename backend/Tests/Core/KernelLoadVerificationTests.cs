using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core.Authorization;
using Core.Entitlements;
using Core.Modules;
using Core.Security;
using Core.Services;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Core.Tests;

public sealed class KernelLoadVerificationTests
{
    [Fact]
    public void RegistryPermissionEntitlementAndSignedUrlLoadsRemainTenantBound()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var registry = new ModuleRegistry(
        [
            Manifest("platform-kernel", []),
            Manifest("sample", ["platform-kernel"])
        ]);
        var role = new Role(Guid.CreateVersion7(), tenant, "load-reader", "Load reader", false, true, 1);
        var membership = Guid.CreateVersion7();
        var subject = new AuthorizationSubject(
            tenant,
            membership,
            [new MembershipRoleAssignment(tenant, membership, role.Id, PermissionScope.ForTenant(tenant))],
            [role],
            [new RolePermissionEntry(tenant, role.Id, "sample.item.read")],
            1);
        var authorizer = new PermissionAuthorizer();
        var key = Encoding.UTF8.GetBytes("kernel-load-entitlement-key");
        var issued = DateTimeOffset.UtcNow;
        var entitlement = CreateSignedSnapshot(tenant, key, issued);
        var guard = new EntitlementGuard(new HmacEntitlementVerifier(new Dictionary<string, byte[]> { ["current"] = key }));
        guard.SetSnapshot(entitlement);
        var file = new FileRecord(Guid.CreateVersion7(), tenant, "sample", "item", Guid.CreateVersion7(), "ignored", "item.pdf", "application/pdf", 12, "hash", FileStatus.Ready, "internal", Guid.CreateVersion7(), issued);
        var signer = new HmacFileAccessSigner("kernel-load-file-secret");
        var expiresAt = issued.AddMinutes(5);
        var signature = signer.CreateDownloadUrl(file, tenant, expiresAt).Split("signature=", StringSplitOptions.None)[1];
        var stopwatch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            Assert.Equal(["platform-kernel", "sample"], registry.TopologicalOrder());
            Assert.True(authorizer.Authorize(subject, "sample.item.read", PermissionScope.ForTenant(tenant)));
            Assert.False(authorizer.Authorize(subject, "sample.item.read", PermissionScope.ForTenant(otherTenant)));
            Assert.True(guard.IsEntitled(tenant, "sample"));
            Assert.False(guard.IsEntitled(otherTenant, "sample"));
            Assert.True(signer.VerifyDownload(file, tenant, expiresAt, signature, issued));
            Assert.False(signer.VerifyDownload(file, otherTenant, expiresAt, signature, issued));
        }

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Kernel load verification exceeded the bounded local test budget: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task AuditAndImportLoadsPreserveTenantIdentityAndIdempotency()
    {
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();
        var audit = new InMemoryAuditWriter();
        var scheduler = new InMemoryImportJobScheduler();
        var batch = new ImportBatch(Guid.CreateVersion7(), tenant, "sample", "sample-import", ImportStatus.Execute, Guid.CreateVersion7(), JsonSerializer.SerializeToElement(new { mapping = "stable" }), 0, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        var writes = Enumerable.Range(0, 100_000).Select(index => new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "System", null, null, "sample", "load_verified", "sample_item", Guid.CreateVersion7(), null, null, null, "kernel-load", Guid.CreateVersion7(), null, DateTimeOffset.UtcNow, null));

        foreach (var entry in writes) await audit.WriteAsync(entry);

        var duplicateChunks = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => scheduler.ScheduleChunkAsync(batch, 0, 100)));
        Assert.Single(duplicateChunks.Distinct());
        var uniqueChunks = await Task.WhenAll(Enumerable.Range(1, 200).Select(chunk => scheduler.ScheduleChunkAsync(batch, chunk * 100, 100)));

        Assert.Equal(100_000, audit.Snapshot().Count);
        Assert.All(audit.Snapshot(), entry => Assert.Equal(tenant, entry.TenantId));
        Assert.DoesNotContain(audit.Snapshot(), entry => entry.TenantId == otherTenant);
        Assert.Equal(200, uniqueChunks.Select(job => job.StartRow).Distinct().Count());
    }

    private static EntitlementSnapshot CreateSignedSnapshot(TenantId tenant, byte[] key, DateTimeOffset issued)
    {
        var snapshot = new EntitlementSnapshot(tenant, 1, issued, issued.AddMinutes(10), "current", new HashSet<string>(["sample"]), new HashSet<string>(), new Dictionary<string, long>(), "");
        var canonical = JsonSerializer.Serialize(new { tenant = tenant.Value, snapshot = snapshot.SnapshotVersion, issued = snapshot.IssuedAt, valid = snapshot.ValidUntil, modules = snapshot.Modules.OrderBy(value => value, StringComparer.Ordinal), features = snapshot.Features.OrderBy(value => value, StringComparer.Ordinal), limits = snapshot.Limits.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) });
        return snapshot with { Signature = Convert.ToBase64String(System.Security.Cryptography.HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonical))) };
    }

    private static ModuleManifest Manifest(string id, IReadOnlyList<string> hardDependencies) => new(id, "1.0.0", 1, "L0", "kernel", false, "tenant", new(hardDependencies, []), new(id, []), [], new([], []), new([], []), new(false, "never_disable"));
}
