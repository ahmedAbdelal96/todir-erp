using Core.Messaging;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class ApiCompositionTests
{
    [Fact]
    public void ApiBuildsWithValidatedPostgresTransactionAndInboxServices()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION to the configured runtime database for the API composition gate.");

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", connectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
        });

        var services = factory.Services;
        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        var expected = new NpgsqlConnectionStringBuilder(connectionString);
        var configured = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        Assert.Equal(expected.Host, configured.Host);
        Assert.Equal(expected.Port, configured.Port);
        Assert.Equal(expected.Database, configured.Database);
        Assert.Equal(expected.Username, configured.Username);
        Assert.IsType<NpgsqlTransactionCoordinator>(services.GetRequiredService<ITransactionCoordinator>());
        Assert.IsType<NpgsqlInboxStore>(services.GetRequiredService<IInboxStore>());
        Assert.NotNull(dataSource);
    }
}
