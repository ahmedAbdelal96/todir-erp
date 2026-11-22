using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Core.Caching;

public static class CacheHostingExtensions
{
    private static readonly Action<ILogger, Exception?> StartupFallbackLog = LoggerMessage.Define(LogLevel.Warning, new EventId(2300, "RedisStartupFallback"), "Redis is unavailable at startup; platform cache will use PostgreSQL fallback");
    private static readonly Action<ILogger, bool, bool, string, Exception?> StartupConfiguredLog = LoggerMessage.Define<bool, bool, string>(LogLevel.Information, new EventId(2303, "RedisConfigured"), "Platform cache configuration: enabled={Enabled}, hrDirectory={HrDirectory}, configuration={Configuration}");
    public static IServiceCollection AddPlatformCaching(this IServiceCollection services, IConfiguration configuration, NpgsqlDataSource? dataSource)
    {
        var options = PlatformCacheOptions.FromConfiguration(configuration);
        services.AddSingleton(options);
        if (!options.Enabled || dataSource is null)
        {
            services.AddSingleton<IPlatformCache, NoopPlatformCache>();
            return services;
        }

        services.AddSingleton<IPlatformCache>(provider =>
        {
            try
            {
                var redisOptions = ConfigurationOptions.Parse(options.Configuration);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectTimeout = options.ConnectTimeoutMilliseconds;
                redisOptions.SyncTimeout = options.SyncTimeoutMilliseconds;
                redisOptions.AsyncTimeout = options.SyncTimeoutMilliseconds;
                redisOptions.KeepAlive = 15;
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                multiplexer.GetDatabase().Ping();
                StartupConfiguredLog(provider.GetRequiredService<ILoggerFactory>().CreateLogger("PlatformCaching"), options.Enabled, options.HrDirectoryEnabled, options.Configuration, null);
                return new RedisPlatformCache(
                    multiplexer,
                    dataSource,
                    options,
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<RedisPlatformCache>>());
            }
            catch (Exception exception) when (exception is RedisException or RedisConnectionException or RedisTimeoutException or TimeoutException or IOException)
            {
                StartupFallbackLog(provider.GetRequiredService<ILoggerFactory>().CreateLogger("PlatformCaching"), exception);
                return new NoopPlatformCache();
            }
        });
        return services;
    }
}
