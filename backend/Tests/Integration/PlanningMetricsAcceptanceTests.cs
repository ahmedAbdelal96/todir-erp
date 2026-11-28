using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Planning;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningMetricsAcceptanceTests
{
    [Fact(DisplayName = "FP45 Planning metrics are exported with bounded dimensions")]
    public async Task PlanningMetricsAreExportedWithBoundedDimensions()
    {
        var connection = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for Planning metrics acceptance.");

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connection)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
            .UseSetting("TODIR_METRICS_ENABLED", "true")
            .UseSetting("TODIR_METRICS_TOKEN", "planning-metrics-test-token"));
        using var client = factory.CreateClient();

        PlanningMetrics.Decision("block");
        PlanningMetrics.Failure("actual_refresh", "planning.test_failure");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/metrics");
        request.Headers.Authorization = new("Bearer", "planning-metrics-test-token");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("todir_planning_budget_block_decisions_total", body, StringComparison.Ordinal);
        Assert.Contains("todir_planning_workflow_failures_total", body, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant_id", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document_id", body, StringComparison.OrdinalIgnoreCase);
    }
}
