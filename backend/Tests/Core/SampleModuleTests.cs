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
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the repository root.");
        }
    }
}
