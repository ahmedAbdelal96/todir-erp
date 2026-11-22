using Core.Caching;
using Shared.Primitives.Identifiers;
using Xunit;

namespace CoreTests;

public sealed class PlatformCacheTests
{
    [Fact]
    public async Task DisabledCacheAlwaysUsesAuthoritativeLoader()
    {
        var cache = new NoopPlatformCache();
        var calls = 0;
        var scope = new CacheScope(new TenantId(Guid.CreateVersion7()), null, null, Guid.CreateVersion7(), 7);

        var first = await cache.GetOrCreateAsync(scope, PlatformCachePolicies.HrEmployeeDirectory, CacheKeyFingerprint.For("page-1"), TimeSpan.FromSeconds(30), _ =>
        {
            calls++;
            return Task.FromResult("authoritative");
        });
        var second = await cache.GetOrCreateAsync(scope, PlatformCachePolicies.HrEmployeeDirectory, CacheKeyFingerprint.For("page-1"), TimeSpan.FromSeconds(30), _ =>
        {
            calls++;
            return Task.FromResult("authoritative-2");
        });

        Assert.Equal("authoritative", first);
        Assert.Equal("authoritative-2", second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void FingerprintDoesNotExposeTheSearchValue()
    {
        var fingerprint = CacheKeyFingerprint.For("employee search: أحمد");
        Assert.DoesNotContain("أحمد", fingerprint, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
    }

    [Fact]
    public async Task UnapprovedDependencyCannotBeCached()
    {
        var cache = new NoopPlatformCache();
        var scope = new CacheScope(new TenantId(Guid.CreateVersion7()), null, null, null, 0);
        var policy = new CacheDependencyPolicy("hr.employee.identifier", CacheConsistencyModel.BoundedStaleness, "hr", "unknown", "unknown", true, TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(scope, policy, CacheKeyFingerprint.For("sensitive"), TimeSpan.FromSeconds(1), _ => Task.FromResult("forbidden")));
    }
}
