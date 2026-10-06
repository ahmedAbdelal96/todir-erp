using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Todir.Modules.Sample;
using Todir.Platform.Hosting;
using Todir.Platform.Modules;
using Xunit;

namespace Todir.Platform.Tests;

public sealed class SampleModuleTests
{
    [Fact]
    public void SampleManifestIsRegisteredWithoutMakingTheKernelDependOnItsImplementation()
    {
        var services = new ServiceCollection();
        services.AddTodirPlatform(new ConfigurationBuilder().Build());
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
}
