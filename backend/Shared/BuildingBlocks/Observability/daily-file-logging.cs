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
            ? Path.Combine(FindRepositoryRoot() ?? Directory.GetCurrentDirectory(), "artifacts", "logs")
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
            .WriteTo.Console(new ColoredCompactJsonFormatter());

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

internal sealed class ColoredCompactJsonFormatter : ITextFormatter
{
    private const string Reset = "\u001b[0m";
    private static readonly RenderedCompactJsonFormatter JsonFormatter = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        JsonFormatter.Format(logEvent, buffer);
        var content = buffer.ToString();

        if (Console.IsOutputRedirected)
        {
            output.Write(content);
            return;
        }

        output.Write(LevelColor(logEvent.Level));
        output.Write(content);
        output.Write(Reset);
    }

    private static string LevelColor(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "\u001b[90m",
        LogEventLevel.Information => "\u001b[36m",
        LogEventLevel.Warning => "\u001b[33m",
        LogEventLevel.Error => "\u001b[31m",
        LogEventLevel.Fatal => "\u001b[1;31m",
        _ => "\u001b[37m"
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
