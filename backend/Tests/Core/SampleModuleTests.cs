using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Modules.Sample;
using Core.Hosting;
using Core.Modules;
using Xunit;

namespace Core.Tests;

public sealed class SampleModuleTests
{
    [Fact]
    public void SampleManifestIsRegisteredWithoutMakingTheKernelDependOnItsImplementation()
    {
        var services = new ServiceCollection();
        services.AddCore(new ConfigurationBuilder().Build());
        services.AddSampleModule();
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IModuleRegistry>();
        var sample = registry.Find("sample");

        Assert.Equal("sample", sample.Owns.Schema);
        Assert.Contains("platform-kernel", sample.Dependencies.Hard);
        Assert.Contains("sample.item.create", sample.Permissions);
        Assert.Contains(registry.GetAll(), module => module.Id == "platform-kernel");
        Assert.Contains(registry.GetAll(), module => module.Id == "sample");
    }

    [Fact]
    public void SampleModuleAFixturesResolveBeforeSampleModuleBAndRejectInvalidGraphs()
    {
        var sampleA = ModuleRegistry.Load(File.ReadAllText(Path.Combine(RepositoryRoot, "Tests", "Architecture", "fixtures", "modules", "sample-a", "module.manifest.json")));
        var sampleB = ModuleRegistry.Load(File.ReadAllText(Path.Combine(RepositoryRoot, "Tests", "Architecture", "fixtures", "modules", "sample-b", "module.manifest.json")));
        var registry = new ModuleRegistry([sampleB, sampleA]);

        Assert.Equal(["sample-a", "sample-b"], registry.TopologicalOrder());
        Assert.Equal(sampleB, registry.Find("sample-b"));
        Assert.Equal(["sample-a"], registry.GetHardDependencies("sample-b"));
        Assert.Throws<InvalidOperationException>(() => { _ = new ModuleRegistry([sampleB]); });
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new ModuleRegistry([
                sampleA with { Id = "cycle-a", Dependencies = new(["cycle-b"], []) },
                sampleB with { Id = "cycle-b", Dependencies = new(["cycle-a"], []) }]);
        });

        using var parsed = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "Tests", "Architecture", "fixtures", "modules", "sample-a", "module.manifest.json")));
        Assert.True(parsed.RootElement.TryGetProperty("dependencies", out var dependencies));
        Assert.True(dependencies.TryGetProperty("hard", out var hardDependencies));
        Assert.Equal(JsonValueKind.Array, hardDependencies.ValueKind);
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
            throw new InvalidOperationException("Unable to locate the backend repository root from the configured root, working directory, or test output.");
        }
    }
}
