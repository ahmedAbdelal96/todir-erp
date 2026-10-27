using Xunit;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace KernelIntegrationTests
{
    [CollectionDefinition("database-integration", DisableParallelization = true)]
    public sealed class DatabaseIntegrationDefinition { }

    public abstract class DatabaseIntegrationTestBase : IAsyncLifetime
    {
        public Task InitializeAsync() => SynchronizeRuntimePasswordAsync();

        public Task DisposeAsync() => SynchronizeRuntimePasswordAsync();

        private static async Task SynchronizeRuntimePasswordAsync()
        {
            var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
                ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for database integration tests.");
            var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
                ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for database integration tests.");
            var runtimePassword = new NpgsqlConnectionStringBuilder(runtimeConnectionString).Password
                ?? throw new InvalidOperationException("TODIR_DATABASE_CONNECTION must configure the runtime role password.");

            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var prepare = new NpgsqlCommand("select format('alter role erp_app password %L', $1)", connection);
            prepare.Parameters.AddWithValue(runtimePassword);
            var statement = (string?)await prepare.ExecuteScalarAsync();
            if (string.IsNullOrWhiteSpace(statement)) throw new InvalidOperationException("Could not prepare the runtime role password reset.");
            await using var apply = new NpgsqlCommand(statement, connection);
            await apply.ExecuteNonQueryAsync();
        }
    }
}
