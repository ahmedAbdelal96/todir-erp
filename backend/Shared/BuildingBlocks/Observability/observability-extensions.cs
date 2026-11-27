using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

namespace Shared.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, string service, string environment)
    {
        var sqlCommandMeasurementEnabled = string.Equals(
            Environment.GetEnvironmentVariable("TODIR_PERFORMANCE_SQL_COUNT"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (sqlCommandMeasurementEnabled)
        {
            SqlCommandMeasurement.Enable();
        }

        services.AddSingleton(new ObservabilityOptions(service, environment, sqlCommandMeasurementEnabled));
        services.AddSingleton(provider => DailyFileLoggingOptions.From(provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), service, environment));
        services.AddHostedService<LogRetentionService>();
        services.AddSerilog((provider, logger) =>
        {
            var options = provider.GetRequiredService<DailyFileLoggingOptions>();
            DailyFileLoggerConfiguration.Configure(logger, options);
        });
        var otlpEndpoint = Environment.GetEnvironmentVariable("TODIR_OTEL_EXPORTER_OTLP_ENDPOINT");
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(service)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("Todir.Request")
                    .AddSource("Core.Handler")
                    .AddSource("Core.Database")
                    .AddSource("Subledger")
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint)) tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics => metrics
                .AddMeter(CoreMetrics.Meter.Name)
                .AddMeter("Todir.Platform.Cache")
                .AddMeter("Todir.Worker")
                .AddMeter("Subledger")
                .AddMeter("Todir.Planning")
                .AddMeter("Npgsql")
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());
        return services;
    }

    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationMiddleware>();

    public static IApplicationBuilder UseRequestContextLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestContextLoggingMiddleware>();

    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
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
public static class CoreMetrics
{
    public static readonly Meter Meter = new("Core", "1.0.0");
    public static readonly Counter<long> HttpRequests = Meter.CreateCounter<long>("todir.http.requests", "{request}");
    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>("todir.http.request.duration", "s");
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("todir.outbox.published", "{message}");
    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>("todir.outbox.failed", "{message}");
    public static readonly Counter<long> InboxProcessed = Meter.CreateCounter<long>("todir.inbox.processed", "{message}");
    public static readonly Counter<long> InboxDuplicate = Meter.CreateCounter<long>("todir.inbox.duplicate", "{message}");
    public static readonly Counter<long> ImportChunksScheduled = Meter.CreateCounter<long>("todir.import.chunks.scheduled", "{chunk}");
    public static readonly Counter<long> AuthorizationSubjectDatabaseLoads = Meter.CreateCounter<long>("todir.authorization.subject.database.loads", "{load}");
    public static readonly Counter<long> AuthorizationSubjectRequestCacheHits = Meter.CreateCounter<long>("todir.authorization.subject.request.cache.hits", "{hit}");
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
public static class WorkerMetrics
{
    public static readonly Meter Meter = new("Todir.Worker", "1.0.0");
    public static readonly Counter<long> CycleFailures = Meter.CreateCounter<long>("todir.worker.cycle.failures", "{cycle}");
    public static readonly Counter<long> MessagesProcessed = Meter.CreateCounter<long>("todir.worker.messages.processed", "{message}");
    public static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>("todir.worker.consumer.failures", "{failure}");
    public static readonly Counter<long> ConsumerMessagesProcessed = Meter.CreateCounter<long>("todir.worker.consumer.messages.processed", "{message}");
    private static long lastCycleStartedUnixSeconds;
    private static long lastCycleSucceededUnixSeconds;
    private static long lastConsumerProcessedUnixSeconds;
    private static int postgresAvailable;
    private static int rabbitMqAvailable;

    public static readonly ObservableGauge<double> CycleAge = Meter.CreateObservableGauge(
        "todir.worker.cycle.age",
        () => CycleAgeSeconds,
        "s");
    public static readonly ObservableGauge<double> SuccessfulCycleAge = Meter.CreateObservableGauge(
        "todir.worker.successful_cycle.age",
        () => SuccessfulCycleAgeSeconds,
        "s");
    public static readonly ObservableGauge<int> SuccessfulCycleAvailable = Meter.CreateObservableGauge(
        "todir.worker.successful_cycle.available",
        () => SuccessfulCycleAvailableValue);
    public static readonly ObservableGauge<int> PostgresAvailable = Meter.CreateObservableGauge(
        "todir.worker.postgres.available",
        () => Volatile.Read(ref postgresAvailable));
    public static readonly ObservableGauge<int> RabbitMqAvailable = Meter.CreateObservableGauge(
        "todir.worker.rabbitmq.available",
        () => Volatile.Read(ref rabbitMqAvailable));
    public static readonly ObservableGauge<double> ConsumerProcessingAge = Meter.CreateObservableGauge(
        "todir.worker.consumer.processing.age",
        () => ConsumerProcessingAgeSeconds,
        "s");
    public static readonly ObservableGauge<int> ConsumerProcessingAvailable = Meter.CreateObservableGauge(
        "todir.worker.consumer.processing.available",
        () => ConsumerProcessingAvailableValue);
    public static readonly ObservableGauge<int> Ready = Meter.CreateObservableGauge(
        "todir.worker.ready",
        () => IsReady() ? 1 : 0);

    public static double CycleAgeSeconds => AgeSeconds(lastCycleStartedUnixSeconds);
    public static double SuccessfulCycleAgeSeconds => AgeSeconds(lastCycleSucceededUnixSeconds);
    public static int SuccessfulCycleAvailableValue => lastCycleSucceededUnixSeconds > 0 ? 1 : 0;
    public static int PostgresAvailableValue => Volatile.Read(ref postgresAvailable);
    public static int RabbitMqAvailableValue => Volatile.Read(ref rabbitMqAvailable);
    public static double ConsumerProcessingAgeSeconds => AgeSeconds(lastConsumerProcessedUnixSeconds);
    public static int ConsumerProcessingAvailableValue => lastConsumerProcessedUnixSeconds > 0 ? 1 : 0;

    public static void MarkCycleStarted() => Interlocked.Exchange(ref lastCycleStartedUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public static void MarkCycleSucceeded() => Interlocked.Exchange(ref lastCycleSucceededUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public static void MarkCycleFailed() => CycleFailures.Add(1);
    public static void MarkMessageProcessed() => MessagesProcessed.Add(1);
    public static void MarkConsumerProcessed()
    {
        Interlocked.Exchange(ref lastConsumerProcessedUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        ConsumerMessagesProcessed.Add(1);
    }
    public static void MarkConsumerFailed() => ConsumerFailures.Add(1);
    public static void MarkPostgresAvailable() => Interlocked.Exchange(ref postgresAvailable, 1);
    public static void MarkPostgresUnavailable() => Interlocked.Exchange(ref postgresAvailable, 0);
    public static void MarkRabbitMqAvailable() => Interlocked.Exchange(ref rabbitMqAvailable, 1);
    public static void MarkRabbitMqUnavailable() => Interlocked.Exchange(ref rabbitMqAvailable, 0);

    public static bool IsReady(TimeSpan? maximumSuccessfulCycleAge = null)
    {
        var maximumAge = maximumSuccessfulCycleAge ?? TimeSpan.FromSeconds(30);
        return Volatile.Read(ref postgresAvailable) == 1
            && Volatile.Read(ref rabbitMqAvailable) == 1
            && SuccessfulCycleAvailableValue == 1
            && SuccessfulCycleAgeSeconds <= maximumAge.TotalSeconds;
    }

    private static double AgeSeconds(long timestamp) => timestamp <= 0
        ? 0
        : Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp);
}

public sealed record ObservabilityOptions(string Service, string Environment, bool SqlCommandMeasurementEnabled = false);

internal sealed class CorrelationMiddleware(RequestDelegate next, Microsoft.Extensions.Logging.ILogger<CorrelationMiddleware> logger, ObservabilityOptions options)
{
    private static readonly ActivitySource ActivitySource = new("Todir.Request");
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, string, int, double, Exception?> RequestCompleted =
        LoggerMessage.Define<string, string, int, double>(LogLevel.Information, new EventId(1000, "HttpRequestCompleted"), "HTTP request completed {HttpMethod} {HttpPath} with {StatusCode} in {DurationMs} ms");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = RequestCorrelationId.GetOrCreate(context).ToString("D");
        using var activity = ActivitySource.StartActivity("HTTP request", ActivityKind.Server);
        using var sqlMeasurement = options.SqlCommandMeasurementEnabled ? SqlCommandMeasurement.Begin() : null;
        if (sqlMeasurement is not null)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Todir-Db-Commands"] = sqlMeasurement.CommandCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Task.CompletedTask;
            });
        }
        activity?.SetTag("service", options.Service);
        activity?.SetTag("environment", options.Environment);
        activity?.SetTag("correlation_id", correlationId);
        using var serviceScope = LogContextScope.Push("service", options.Service);
        using var environmentScope = LogContextScope.Push("environment", options.Environment);
        using var traceScope = LogContextScope.Push("trace_id", Activity.Current?.TraceId.ToString());
        using var spanScope = LogContextScope.Push("span_id", Activity.Current?.SpanId.ToString());
        using var correlationScope = LogContextScope.Push("correlation_id", correlationId);
        using var moduleScope = LogContextScope.Push("module", ResolveModule(context.Request.Path));
        var endpoint = context.GetEndpoint()?.DisplayName ?? "unmatched";
        using var operationScope = LogContextScope.Push("operation", $"{context.Request.Method} {endpoint}");
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var statusClass = $"{context.Response.StatusCode / 100}xx";
            var tags = new TagList { { "method", context.Request.Method }, { "endpoint", endpoint }, { "status_class", statusClass } };
            CoreMetrics.HttpRequests.Add(1, tags);
            CoreMetrics.HttpRequestDuration.Record(duration, tags);
            RequestCompleted(
                logger,
                context.Request.Method,
                endpoint,
                context.Response.StatusCode,
                Math.Round(duration * 1000, 2),
                null);
        }
    }

    private static string ResolveModule(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return segments.Length >= 3 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) && string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase)
            ? segments[2]
            : "platform";
    }
}

internal static class SqlCommandMeasurement
{
    private static readonly AsyncLocal<Scope?> Current = new();
    private static readonly object Sync = new();
    private static ActivityListener? listener;

    public static void Enable()
    {
        lock (Sync)
        {
            if (listener is not null)
            {
                return;
            }

            listener = new ActivityListener
            {
                ShouldListenTo = source => string.Equals(source.Name, "Npgsql", StringComparison.Ordinal),
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = static activity =>
                {
                    if (activity.Kind == ActivityKind.Client && Current.Value is { } scope)
                    {
                        scope.Increment();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }
    }

    public static Scope Begin()
    {
        var scope = new Scope(Current.Value);
        Current.Value = scope;
        return scope;
    }

    public sealed class Scope(Scope? parent) : IDisposable
    {
        private int commandCount;

        public int CommandCount => Volatile.Read(ref commandCount);

        public void Increment() => Interlocked.Increment(ref commandCount);

        public void Dispose() => Current.Value = parent;
    }
}

internal sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var tenantScope = LogContextScope.Push("tenant_id", context.User.FindFirst("tenant_id")?.Value);
        using var legalEntityScope = LogContextScope.Push("legal_entity_id", context.User.FindFirst("legal_entity_id")?.Value);
        using var userScope = LogContextScope.Push("user_id", context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        using var membershipScope = LogContextScope.Push("membership_id", context.User.FindFirst("membership_id")?.Value);
        await next(context);
    }
}

internal sealed class ExceptionHandlingMiddleware(RequestDelegate next, Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> LogUnhandled =
        LoggerMessage.Define(LogLevel.Error, new EventId(5000, "UnhandledRequestFailure"), "Unhandled request failure");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // A browser navigation can cancel an in-flight request after the
            // database operation has completed. The client is already gone,
            // so this is not an unhandled server failure and a 500 response
            // cannot be delivered. Keep logging for non-request cancellations.
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                return;
            }
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
