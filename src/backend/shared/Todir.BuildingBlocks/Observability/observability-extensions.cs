using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

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
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(TodirMetrics.Meter.Name)
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());
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
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (context.Request.IsHttps) context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            await next(context);
        });
}

public static class TodirMetrics
{
    public static readonly Meter Meter = new("Todir.Platform", "1.0.0");
    public static readonly Counter<long> HttpRequests = Meter.CreateCounter<long>("todir.http.requests", "{request}");
    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>("todir.http.request.duration", "s");
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("todir.outbox.published", "{message}");
    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>("todir.outbox.failed", "{message}");
    public static readonly Counter<long> InboxProcessed = Meter.CreateCounter<long>("todir.inbox.processed", "{message}");
    public static readonly Counter<long> InboxDuplicate = Meter.CreateCounter<long>("todir.inbox.duplicate", "{message}");
    public static readonly Counter<long> ImportChunksScheduled = Meter.CreateCounter<long>("todir.import.chunks.scheduled", "{chunk}");
    public static readonly Histogram<double> ModuleActivationDuration = Meter.CreateHistogram<double>("todir.module.activation.duration", "s");
    public static readonly Histogram<double> MessageProcessingDuration = Meter.CreateHistogram<double>("todir.message.processing.duration", "s");
    private static long outboxPending;
    private static double outboxOldestAgeSeconds;
    public static readonly ObservableGauge<long> OutboxPending = Meter.CreateObservableGauge("todir.outbox.pending", () => Volatile.Read(ref outboxPending), "{message}");
    public static readonly ObservableGauge<double> OutboxOldestAge = Meter.CreateObservableGauge("todir.outbox.oldest.pending.age", () => Volatile.Read(ref outboxOldestAgeSeconds), "s");

    public static void SetOutboxBacklog(long pending, double oldestAgeSeconds)
    {
        Interlocked.Exchange(ref outboxPending, pending);
        Volatile.Write(ref outboxOldestAgeSeconds, Math.Max(0, oldestAgeSeconds));
    }
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
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var endpoint = context.GetEndpoint()?.DisplayName ?? "unmatched";
            var statusClass = $"{context.Response.StatusCode / 100}xx";
            var tags = new TagList { { "method", context.Request.Method }, { "endpoint", endpoint }, { "status_class", statusClass } };
            TodirMetrics.HttpRequests.Add(1, tags);
            TodirMetrics.HttpRequestDuration.Record(duration, tags);
        }
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
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Request failed", status = 500, code = "server.error", traceId = Activity.Current?.TraceId.ToString(), correlationId = context.Response.Headers["X-Correlation-Id"].ToString() });
            }
        }
    }
}
