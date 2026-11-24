using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.BuildingBlocks.Observability;
using Xunit;

namespace ObservabilityTests;

public sealed class DailyFileLoggingTests
{
    private static readonly Action<ILogger, Exception?> ExpectedError =
        LoggerMessage.Define(LogLevel.Error, new EventId(9001, "ExpectedStructuredError"), "Expected structured error event");

    [Fact]
    public async Task WritesStructuredRequestAndErrorEventsIntoTheCurrentDailyDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "todir-log-test-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration["TODIR_LOG_DIRECTORY"] = directory;
            builder.Configuration["TODIR_LOG_RETENTION_DAYS"] = "30";
            builder.Configuration["TODIR_LOG_MINIMUM_LEVEL"] = "Information";
            builder.Services.AddObservability("Observability.Test", "Test");
            var app = builder.Build();
            app.UseCorrelation();
            app.MapGet("/probe/{requestId}", (string requestId) => Results.Ok(new { status = "ok", requestId }));
            await app.StartAsync();

            using var client = app.GetTestClient();
            var correlationId = Guid.CreateVersion7().ToString("D");
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            const string sensitiveRequestId = "customer-email@example.test";
            var response = await client.GetAsync($"/probe/{sensitiveRequestId}");
            response.EnsureSuccessStatusCode();
            ExpectedError(app.Services.GetRequiredService<ILogger<DailyFileLoggingTests>>(), null);

            await app.StopAsync();
            await app.DisposeAsync();

            string[] files = [];
            for (var attempt = 0; attempt < 20 && files.Length == 0; attempt++)
            {
                await Task.Delay(50);
                files = Directory.GetFiles(directory, "*.jsonl", SearchOption.AllDirectories);
            }
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("application-", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("errors-", StringComparison.Ordinal));

            var applicationContent = string.Join(Environment.NewLine, files.Where(file => Path.GetFileName(file).StartsWith("application-", StringComparison.Ordinal)).Select(File.ReadAllText));
            Assert.Contains(correlationId, applicationContent, StringComparison.Ordinal);
            Assert.Contains("Observability.Test", applicationContent, StringComparison.Ordinal);
            Assert.Contains("\"environment\":\"Test\"", applicationContent, StringComparison.Ordinal);
            Assert.Contains("HTTP request completed", applicationContent, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitiveRequestId, applicationContent, StringComparison.Ordinal);
            Assert.Contains("probe/{requestId}", applicationContent, StringComparison.Ordinal);

            foreach (var file in files)
                foreach (var line in await File.ReadAllLinesAsync(file))
                    using (JsonDocument.Parse(line)) { }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
