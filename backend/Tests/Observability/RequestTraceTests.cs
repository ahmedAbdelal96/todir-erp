using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.BuildingBlocks.Observability;
using Xunit;

namespace ObservabilityTests;

public sealed class RequestTraceTests
{
    [Fact]
    public async Task RequestHandlerAndPostgresQueryShareOneTrace()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_P03_POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P03_POSTGRES_CONNECTION to run the PostgreSQL-backed P03 exit gate.");

        var completedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => completedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddObservability("Core.P03.Test", "Test");
        builder.Services.AddSingleton(dataSource);
        var app = builder.Build();
        app.UseCorrelation();
        app.UseExceptionHandling();
        app.UseSecurityHeaders();
        app.MapGet("/probe", async (NpgsqlDataSource source) =>
        {
            using var handlerActivity = new ActivitySource("Core.Handler").StartActivity("P03 handler", ActivityKind.Internal);
            await using var command = source.CreateCommand("SELECT 1");
            using var databaseActivity = new ActivitySource("Core.Database").StartActivity("PostgreSQL SELECT 1", ActivityKind.Client);
            databaseActivity?.SetTag("db.system", "postgresql");
            databaseActivity?.SetTag("db.operation.name", "SELECT");
            return Results.Ok(await command.ExecuteScalarAsync());
        });
        await app.StartAsync();

        using var client = app.GetTestClient();
        var correlationId = Guid.CreateVersion7().ToString("D");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
        var response = await client.GetAsync("/probe");
        response.EnsureSuccessStatusCode();
        await app.StopAsync();

        Assert.NotEmpty(completedActivities);
        var traceIds = completedActivities
            .Where(activity => activity.OperationName == "HTTP request"
                && activity.Tags.Any(tag => tag.Key == "correlation_id" && tag.Value == correlationId))
            .Select(activity => activity.TraceId)
            .Distinct()
            .ToArray();
        Assert.Single(traceIds);
        var requestActivities = completedActivities.Where(activity => activity.TraceId == traceIds[0]).ToArray();
        Assert.Contains(requestActivities, activity => activity.OperationName == "HTTP request");
        Assert.Contains(requestActivities, activity => activity.OperationName == "P03 handler");
        Assert.Contains(requestActivities, activity => activity.OperationName == "PostgreSQL SELECT 1" && activity.Tags.Any(tag => tag.Key == "db.system" && tag.Value == "postgresql"));
    }
}
