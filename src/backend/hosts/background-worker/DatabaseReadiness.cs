using Npgsql;

namespace Todir.Worker;

internal static class DatabaseReadiness
{
    public static async Task WaitForPlatformSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                await using var dataSource = NpgsqlDataSource.Create(connectionString);
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await using var command = new NpgsqlCommand("select to_regclass('platform_tenancy.tenant'), to_regclass('platform_scheduling.qrtz_locks')", connection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0) && !reader.IsDBNull(1)) return;
            }
            catch (NpgsqlException) when (attempt < 60)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException("platform.database_schema_not_ready");
    }
}
