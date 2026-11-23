using Xunit;

namespace ArchitectureTests;

public sealed class RedisArchitectureGovernanceTests
{
    [Fact]
    public void BusinessModulesUseOnlyTheSharedCacheBoundary()
    {
        var modulesRoot = Path.Combine(RepositoryRoot, "Modules");
        foreach (var sourceFile in Directory.EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)))
        {
            var source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("StackExchange.Redis", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IConnectionMultiplexer", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ConnectionMultiplexer", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedDeveloperGuideDocumentsTheApprovedBoundary()
    {
        var guidePath = Path.Combine(RepositoryRoot, "..", "docs", "engineering", "REDIS_PLATFORM_DEVELOPER_GUIDE.md");
        Assert.True(File.Exists(guidePath), $"Missing {guidePath}");
        var guide = File.ReadAllText(guidePath);
        Assert.Contains("IPlatformCache", guide, StringComparison.Ordinal);
        Assert.Contains("PostgreSQL remains the source of truth", guide, StringComparison.Ordinal);
        Assert.Contains("CacheKeyFingerprint.For", guide, StringComparison.Ordinal);
        Assert.Contains("lowercase 64-character SHA-256", guide, StringComparison.Ordinal);
        Assert.Contains("leader cancellation", guide, StringComparison.Ordinal);
        Assert.Contains("TODIR_CACHE_MAX_ENTRY_BYTES", guide, StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT");
            foreach (var start in new[] { configured, Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                for (var directory = new DirectoryInfo(Path.GetFullPath(start!)); directory is not null; directory = directory.Parent)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Backend.sln"))) return directory.FullName;
                    if (File.Exists(Path.Combine(directory.FullName, "backend", "Backend.sln"))) return Path.Combine(directory.FullName, "backend");
                }
            }
            throw new InvalidOperationException("Unable to locate the backend repository root.");
        }
    }
}
