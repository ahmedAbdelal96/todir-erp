using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting;
using Serilog.Context;

namespace Shared.BuildingBlocks.Observability;

internal sealed record DailyFileLoggingOptions(
    bool Enabled,
    string RootDirectory,
    int RetentionDays,
    long FileSizeLimitBytes,
    int FileCountLimit,
    LogEventLevel MinimumLevel,
    string Service,
    string Environment)
{
    public static DailyFileLoggingOptions From(IConfiguration configuration, string service, string environment)
    {
        var root = configuration["TODIR_LOG_DIRECTORY"];
        var rootDirectory = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(FindRepositoryRoot() ?? Directory.GetCurrentDirectory(), "backend", "logs")
            : Path.GetFullPath(root);

        return new(
            Enabled: !bool.TryParse(configuration["TODIR_FILE_LOGGING_ENABLED"], out var enabled) || enabled,
            RootDirectory: rootDirectory,
            RetentionDays: ReadInt(configuration["TODIR_LOG_RETENTION_DAYS"], 30, 1, 3650),
            FileSizeLimitBytes: ReadLong(configuration["TODIR_LOG_FILE_SIZE_BYTES"], 50 * 1024 * 1024, 1024 * 1024, 1024L * 1024 * 1024),
            FileCountLimit: ReadInt(configuration["TODIR_LOG_FILE_COUNT_LIMIT"], 10, 1, 100),
            MinimumLevel: ParseLevel(configuration["TODIR_LOG_MINIMUM_LEVEL"] ?? configuration["Serilog:MinimumLevel:Default"]),
            Service: service,
            Environment: environment);
    }

    public string ServiceDirectory => Sanitize(Service);

    private static LogEventLevel ParseLevel(string? value) =>
        Enum.TryParse<LogEventLevel>(value, true, out var level) ? level : LogEventLevel.Information;

    private static int ReadInt(string? value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static long ReadLong(string? value, long fallback, long minimum, long maximum) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static string Sanitize(string value)
    {
        var characters = value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_').ToArray();
        return new string(characters).Trim('_', '.') switch
        {
            { Length: > 0 } safe => safe,
            _ => "service"
        };
    }

    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) return directory.FullName;
        return null;
    }
}

internal sealed class DailyLogDateEnricher : Serilog.Core.ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory) =>
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("log_date", logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
}

internal sealed class LogRetentionService(DailyFileLoggingOptions options, ILogger<LogRetentionService> logger) : BackgroundService
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception?> CleanupWarning =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5101, "LogRetentionCleanupFailed"), "Log retention cleanup could not complete for {LogService}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Cleanup();
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) Cleanup();
    }

    private void Cleanup()
    {
        if (!options.Enabled) return;
        var serviceDirectory = Path.Combine(options.RootDirectory, options.ServiceDirectory);
        if (!Directory.Exists(serviceDirectory)) return;
        var cutoff = DateTime.UtcNow.Date.AddDays(-options.RetentionDays);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(serviceDirectory))
            {
                var name = Path.GetFileName(directory);
                if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Date < cutoff)
                    Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CleanupWarning(logger, options.Service, exception);
        }
    }
}

internal static class DailyFileLoggerConfiguration
{
    public static void Configure(LoggerConfiguration configuration, DailyFileLoggingOptions options)
    {
        configuration
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", options.Service)
            .Enrich.WithProperty("environment", options.Environment)
            .Enrich.With<DailyLogDateEnricher>()
            .WriteTo.Console(new ReadableConsoleLogFormatter());

        if (!options.Enabled) return;

        configuration.WriteTo.Map(
            "log_date",
            "unknown",
            (date, sink) =>
            {
                var directory = Path.Combine(options.RootDirectory, options.ServiceDirectory, date);
                Directory.CreateDirectory(directory);
                sink.File(
                    new CompactJsonFormatter(),
                    Path.Combine(directory, "application-.jsonl"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: options.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: options.FileCountLimit,
                    shared: true);
            },
            sinkMapCountLimit: 3);

        configuration.WriteTo.Map(
            "log_date",
            "unknown",
            (date, sink) =>
            {
                var directory = Path.Combine(options.RootDirectory, options.ServiceDirectory, date);
                Directory.CreateDirectory(directory);
                sink.File(
                    new CompactJsonFormatter(),
                    Path.Combine(directory, "errors-.jsonl"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: options.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: options.FileCountLimit,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    shared: true);
            },
            sinkMapCountLimit: 3);
    }
}

internal sealed class ReadableConsoleLogFormatter : ITextFormatter
{
    private const string Reset = "\u001b[0m";
    private const string Dim = "\u001b[90m";
    private const string White = "\u001b[37m";
    private const string Cyan = "\u001b[36m";
    private const string Blue = "\u001b[34m";
    private const string Magenta = "\u001b[35m";
    private const string Green = "\u001b[32m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string BoldRed = "\u001b[1;31m";

    public void Format(LogEvent logEvent, TextWriter output)
    {
        var color = !Console.IsOutputRedirected;
        Write(output, $"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}]", Dim, color);
        output.Write(' ');
        Write(output, LevelLabel(logEvent.Level), LevelColor(logEvent.Level), color);
        output.Write(' ');
        Write(output, Property(logEvent, "service") ?? "backend", Magenta, color);
        output.Write(' ');
        Write(output, Property(logEvent, "module") ?? "platform", Cyan, color);
        output.Write(' ');

        var method = Property(logEvent, "HttpMethod") ?? Property(logEvent, "Method");
        var path = Property(logEvent, "HttpPath") ?? Property(logEvent, "Path") ?? Property(logEvent, "RequestPath");
        if (!string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path))
        {
            Write(output, method, Blue, color);
            output.Write(' ');
            Write(output, path, White, color);
            var status = IntegerProperty(logEvent, "StatusCode");
            if (status.HasValue)
            {
                output.Write(" -> ");
                Write(output, status.Value.ToString(CultureInfo.InvariantCulture), StatusColor(status.Value), color);
            }

            var duration = NumberProperty(logEvent, "DurationMs") ?? NumberProperty(logEvent, "ElapsedMilliseconds");
            if (duration.HasValue)
            {
                output.Write(" ");
                Write(output, $"{duration.Value.ToString("0.##", CultureInfo.InvariantCulture)}ms", Dim, color);
            }

            output.Write(" | ");
        }

        Write(output, logEvent.RenderMessage(CultureInfo.InvariantCulture), MessageColor(logEvent.Level), color);
        var correlation = Property(logEvent, "correlation_id");
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            output.Write(" | ");
            Write(output, $"corr={correlation}", Dim, color);
        }

        if (logEvent.Exception is not null)
        {
            output.Write(" | ");
            Write(output, $"{logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}", MessageColor(logEvent.Level), color);
        }

        output.WriteLine();
    }

    private static void Write(TextWriter output, string value, string color, bool useColor)
    {
        if (useColor) output.Write(color);
        output.Write(value);
        if (useColor) output.Write(Reset);
    }

    private static string? Property(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: string text } ? text : null;

    private static int? IntegerProperty(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: int number } ? number : null;

    private static double? NumberProperty(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: double number } ? number : null;

    private static string LevelLabel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "LOG"
    };

    private static string LevelColor(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => Dim,
        LogEventLevel.Information => Green,
        LogEventLevel.Warning => Yellow,
        LogEventLevel.Error => Red,
        LogEventLevel.Fatal => BoldRed,
        _ => White
    };

    private static string MessageColor(LogEventLevel level) => level >= LogEventLevel.Error ? Red : White;

    private static string StatusColor(int statusCode) => statusCode switch
    {
        >= 500 => Red,
        >= 400 => Yellow,
        >= 300 => Cyan,
        _ => Green
    };
}

internal static class LogContextScope
{
    public static IDisposable Push(string propertyName, string? value) =>
        string.IsNullOrWhiteSpace(value) ? NoopDisposable.Instance : LogContext.PushProperty(propertyName, value);

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}
