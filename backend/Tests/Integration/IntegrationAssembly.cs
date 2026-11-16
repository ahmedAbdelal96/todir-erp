using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace KernelIntegrationTests
{
    [CollectionDefinition("database-integration", DisableParallelization = true)]
    public sealed class DatabaseIntegrationDefinition { }

    public abstract class DatabaseIntegrationTestBase
    {
        // PostgreSQL roles are cluster-wide. Integration tests must never change the
        // shared erp_app credential as a fixture side effect. Each test group owns a
        // disposable database and receives credentials provisioned by its migrator setup;
        // invalid credentials must fail closed at the test boundary.
    }
}
