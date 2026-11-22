using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Core.Caching;

public static class CacheHostingExtensions
{
    private static readonly Action<ILogger, string, Exception?> StartupFallbackLog = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2300, "RedisStartupFallback"), "Redis is unavailable at startup; platform cache will use PostgreSQL fallback (failureType={FailureType})");
    private static readonly Action<ILogger, bool, bool, bool, Exception?> StartupConfiguredLog = LoggerMessage.Define<bool, bool, bool>(LogLevel.Information, new EventId(2303, "RedisConfigured"), "Platform cache configuration: enabled={Enabled}, hrDirectory={HrDirectory}, tls={Tls}");
    private static readonly Action<ILogger, Exception?> InsecureProductionLog = LoggerMessage.Define(LogLevel.Error, new EventId(2305, "RedisInsecureProductionConfiguration"), "Redis cache is enabled outside Development without TLS and ACL credentials; platform cache is disabled");
    private static readonly Action<ILogger, Exception?> ConnectionRestoredLog = LoggerMessage.Define(LogLevel.Information, new EventId(2306, "RedisConnectionRestored"), "Redis cache connection restored; PostgreSQL remains the freshness authority");
    private static readonly Action<ILogger, Exception?> ConnectionFailedLog = LoggerMessage.Define(LogLevel.Warning, new EventId(2307, "RedisConnectionFailed"), "Redis cache connection is unavailable; requests will use PostgreSQL fallback");

    public static IServiceCollection AddPlatformCaching(this IServiceCollection services, IConfiguration configuration, NpgsqlDataSource? dataSource, string? environmentName = null)
    {
        var options = PlatformCacheOptions.FromConfiguration(configuration, environmentName);
        services.AddSingleton(options);
        services.AddSingleton<IPlatformCachePolicyRegistry, PlatformCachePolicyRegistry>();
        if (!options.Enabled || dataSource is null)
        {
            services.AddSingleton<IPlatformCache, NoopPlatformCache>();
            return services;
        }

        services.AddSingleton<IPlatformCache>(provider =>
        {
            if (options.RequireSecureTransport && (!options.TlsEnabled || string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password)))
            {
                InsecureProductionLog(provider.GetRequiredService<ILoggerFactory>().CreateLogger("PlatformCaching"), null);
                return new NoopPlatformCache();
            }

            try
            {
                var redisOptions = ConfigurationOptions.Parse(options.Configuration);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectTimeout = options.ConnectTimeoutMilliseconds;
                redisOptions.SyncTimeout = options.SyncTimeoutMilliseconds;
                redisOptions.AsyncTimeout = options.SyncTimeoutMilliseconds;
                redisOptions.KeepAlive = 15;
                redisOptions.Ssl = options.TlsEnabled;
                if (!string.IsNullOrWhiteSpace(options.Username)) redisOptions.User = options.Username;
                if (!string.IsNullOrWhiteSpace(options.Password)) redisOptions.Password = options.Password;
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PlatformCaching");
                multiplexer.ConnectionFailed += (_, _) => ConnectionFailedLog(logger, null);
                multiplexer.ConnectionRestored += (_, _) => ConnectionRestoredLog(logger, null);
                StartupConfiguredLog(logger, options.Enabled, options.HrDirectoryEnabled, options.TlsEnabled, null);
                return new RedisPlatformCache(
                    multiplexer,
                    dataSource,
                    options,
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IPlatformCachePolicyRegistry>(),
                    provider.GetRequiredService<ILogger<RedisPlatformCache>>());
            }
            catch (Exception exception) when (exception is RedisException or RedisConnectionException or RedisTimeoutException or TimeoutException or IOException)
            {
                StartupFallbackLog(provider.GetRequiredService<ILoggerFactory>().CreateLogger("PlatformCaching"), exception.GetType().Name, null);
                return new NoopPlatformCache();
            }
        });
        return services;
    }
}
