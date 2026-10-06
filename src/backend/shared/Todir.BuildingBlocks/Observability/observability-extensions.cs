using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Todir.BuildingBlocks.Observability;

public static class TodirObservabilityExtensions
{
    public static IServiceCollection AddTodirObservability(this IServiceCollection services, string service, string environment)
    {
        services.AddSingleton(new TodirObservabilityOptions(service, environment));
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Todir.Request")
                .AddSource("Todir.Handler")
                .AddSource("Todir.Database")
                .AddSource("Npgsql"));
        return services;
    }

    public static IApplicationBuilder UseTodirCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationMiddleware>();

    public static IApplicationBuilder UseTodirExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();

    public static IApplicationBuilder UseTodirSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            await next(context);
        });
}

public sealed record TodirObservabilityOptions(string Service, string Environment);

internal sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger, TodirObservabilityOptions options)
{
    private static readonly ActivitySource ActivitySource = new("Todir.Request");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreateCorrelationId(context.Request.Headers["X-Correlation-Id"]);
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        using var activity = ActivitySource.StartActivity("HTTP request", ActivityKind.Server);
        activity?.SetTag("service", options.Service);
        activity?.SetTag("environment", options.Environment);
        activity?.SetTag("correlation_id", correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["service"] = options.Service,
            ["environment"] = options.Environment,
            ["trace_id"] = Activity.Current?.TraceId.ToString(),
            ["span_id"] = Activity.Current?.SpanId.ToString(),
            ["correlation_id"] = correlationId
        });
        await next(context);
    }

    private static string ReadOrCreateCorrelationId(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed.ToString("D")
            : Guid.CreateVersion7().ToString("D");
}

internal sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly Action<ILogger, Exception?> LogUnhandled =
        LoggerMessage.Define(LogLevel.Error, new EventId(5000, "UnhandledRequestFailure"), "Unhandled request failure");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            LogUnhandled(logger, exception);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { title = "Request failed", status = 500 });
            }
        }
    }
}
