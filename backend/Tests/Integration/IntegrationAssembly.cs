using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace KernelIntegrationTests
{
    [CollectionDefinition("database-integration", DisableParallelization = true)]
    public sealed class DatabaseIntegrationDefinition { }
}
