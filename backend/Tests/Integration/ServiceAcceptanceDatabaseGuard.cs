using Npgsql;

namespace IntegrationTests;

internal static class ServiceAcceptanceDatabaseGuard
{
    public static string RequireConfiguredConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SERVICE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("TODIR_SERVICE_TEST_CONNECTION must point to a dedicated disposable Service acceptance database.");
        return connectionString;
    }

    public static async Task VerifyAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database?.Trim() ?? string.Empty;
        var normalizedDatabase = database.ToLowerInvariant();
        if (string.Equals(normalizedDatabase, "todir", StringComparison.Ordinal) ||
            !normalizedDatabase.Contains("acceptance", StringComparison.Ordinal) &&
            !normalizedDatabase.Contains("test", StringComparison.Ordinal) &&
            !normalizedDatabase.Contains("ci", StringComparison.Ordinal) &&
            !normalizedDatabase.Contains("disposable", StringComparison.Ordinal))
            throw new InvalidOperationException($"Service acceptance refuses database '{database}'. Use a dedicated disposable acceptance/test database.");

        var host = builder.Host?.Trim().ToLowerInvariant();
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Service acceptance refuses non-local PostgreSQL hosts.");

        await using var connection = await OpenAsync(connectionString);
        await using var command = new NpgsqlCommand("select current_database(), current_user", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The acceptance database identity could not be verified.");
        var actualDatabase = reader.GetString(0);
        var actualUser = reader.GetString(1);
        if (!string.Equals(actualDatabase, database, StringComparison.Ordinal) ||
            string.Equals(actualDatabase, "todir", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actualUser, "erp_app", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Service acceptance identity is unsafe: database '{actualDatabase}', user '{actualUser}'. Use the disposable runtime role erp_app.");
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
