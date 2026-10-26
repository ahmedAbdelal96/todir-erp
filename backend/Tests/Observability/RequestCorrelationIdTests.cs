using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shared.BuildingBlocks.Observability;
using Xunit;

namespace ObservabilityTests;

public sealed class RequestCorrelationIdTests
{
    [Fact]
    public void ValidIncomingCorrelationIdIsNormalizedAndReusedByEndpointContext()
    {
        var expected = Guid.CreateVersion7();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = expected.ToString("B");

        var middlewareValue = RequestCorrelationId.GetOrCreate(context);
        var endpointValue = RequestCorrelationId.GetOrCreate(context);

        Assert.Equal(expected, middlewareValue);
        Assert.Equal(middlewareValue, endpointValue);
        Assert.Equal(expected.ToString("D"), context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public void MissingCorrelationIdIsGeneratedOnceForResponseAndEndpointContext()
    {
        var context = new DefaultHttpContext();

        var middlewareValue = RequestCorrelationId.GetOrCreate(context);
        var endpointValue = RequestCorrelationId.GetOrCreate(context);

        Assert.NotEqual(Guid.Empty, middlewareValue);
        Assert.Equal(middlewareValue, endpointValue);
        Assert.Equal(middlewareValue.ToString("D"), context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void InvalidCorrelationIdIsReplaced(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = value;

        var correlationId = RequestCorrelationId.GetOrCreate(context);

        Assert.NotEqual(Guid.Empty, correlationId);
        Assert.NotEqual(value, context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public void OversizedCorrelationIdIsRejectedWithoutEchoingCallerInput()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = new string('a', 1024);

        var correlationId = RequestCorrelationId.GetOrCreate(context);

        Assert.NotEqual(Guid.Empty, correlationId);
        Assert.Equal(correlationId.ToString("D"), context.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task MiddlewareAndEndpointContextShareCorrelationForValidMissingAndInvalidHeaders()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddObservability("Subledger.Correlation.Test", "Test");
        var app = builder.Build();
        app.UseCorrelation();
        app.MapGet("/correlation", (HttpContext context) =>
            Results.Ok(RequestCorrelationId.GetOrCreate(context).ToString("D")));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var expected = Guid.CreateVersion7();
        using var validRequest = new HttpRequestMessage(HttpMethod.Get, "/correlation");
        validRequest.Headers.Add("X-Correlation-Id", expected.ToString("D"));
        await AssertResponseCorrelationAsync(client, validRequest, expected);

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, "/correlation");
        await AssertResponseCorrelationAsync(client, missingRequest, null);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/correlation");
        invalidRequest.Headers.Add("X-Correlation-Id", "not-a-guid");
        await AssertResponseCorrelationAsync(client, invalidRequest, null);

        await app.StopAsync();
    }

    private static async Task AssertResponseCorrelationAsync(HttpClient client, HttpRequestMessage request, Guid? expected)
    {
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseId = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        var operationId = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.Equal(Guid.Parse(responseId), operationId);
        if (expected.HasValue) Assert.Equal(expected.Value, operationId);
    }
}
