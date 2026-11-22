using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Primitives.Identifiers;
using StackExchange.Redis;

namespace Core.Caching;

public sealed record PlatformCacheOptions(
    bool Enabled,
    bool HrDirectoryEnabled,
    bool StableReferenceDataEnabled,
    string Configuration,
    int ConnectTimeoutMilliseconds,
    int SyncTimeoutMilliseconds,
    TimeSpan DefaultTtl,
    TimeSpan LockTtl,
    int SchemaVersion)
{
    public static PlatformCacheOptions FromConfiguration(IConfiguration configuration) => new(
        configuration.GetValue("TODIR_REDIS_ENABLED", false),
        configuration.GetValue("TODIR_CACHE_HR_DIRECTORY_ENABLED", false),
        configuration.GetValue("TODIR_CACHE_STABLE_REFERENCE_DATA_ENABLED", false),
        configuration["TODIR_REDIS_CONFIGURATION"] ?? "localhost:6379,abortConnect=false",
        Math.Clamp(configuration.GetValue("TODIR_REDIS_CONNECT_TIMEOUT_MS", 1000), 250, 10_000),
        Math.Clamp(configuration.GetValue("TODIR_REDIS_SYNC_TIMEOUT_MS", 1000), 250, 10_000),
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("TODIR_CACHE_DEFAULT_TTL_SECONDS", 60), 5, 3600)),
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("TODIR_CACHE_LOCK_TTL_SECONDS", 5), 1, 30)),
        Math.Max(1, configuration.GetValue("TODIR_CACHE_SCHEMA_VERSION", 1)));
}

public sealed record CacheScope(TenantId TenantId, Guid? LegalEntityId, Guid? BranchId, Guid? MembershipId, int PermissionVersion);
public sealed record CacheInvalidation(TenantId TenantId, string Dependency, Guid? LegalEntityId, Guid? BranchId, long Revision);

public interface IPlatformCache
{
    bool IsEnabled { get; }
    Task<T> GetOrCreateAsync<T>(CacheScope scope, string dependency, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default);
    Task InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default);
}

public sealed class NoopPlatformCache : IPlatformCache
{
    public bool IsEnabled => false;
    public Task<T> GetOrCreateAsync<T>(CacheScope scope, string dependency, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default) => loader(cancellationToken);
    public Task InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class RedisPlatformCache(
    IConnectionMultiplexer multiplexer,
    NpgsqlDataSource dataSource,
    PlatformCacheOptions options,
    TimeProvider clock,
    ILogger<RedisPlatformCache> logger) : IPlatformCache
{
    private static readonly Action<ILogger, string, Exception?> RedisFallbackLog = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2301, "RedisFallback"), "Redis cache unavailable; falling back to PostgreSQL for {Dependency}");
    private static readonly Action<ILogger, string, Exception?> InvalidationFallbackLog = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2302, "RedisInvalidationFallback"), "Redis invalidation unavailable; PostgreSQL revision remains authoritative for {Dependency}");
    private static readonly Action<ILogger, string, bool, Exception?> FirstUseLog = LoggerMessage.Define<string, bool>(LogLevel.Information, new EventId(2304, "RedisCacheFirstUse"), "Platform cache first use for {Dependency}; redisConnected={RedisConnected}");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string InvalidationPrefix = "platform:cache:invalidation:";
    private int firstUseLogged;

    public bool IsEnabled => options.Enabled && multiplexer.IsConnected;

    public async Task<T> GetOrCreateAsync<T>(CacheScope scope, string dependency, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return await loader(cancellationToken);

        if (Interlocked.Exchange(ref firstUseLogged, 1) == 0)
            FirstUseLog(logger, dependency, multiplexer.IsConnected, null);

        var revision = await ReadRevisionAsync(scope.TenantId, dependency, scope.LegalEntityId, scope.BranchId, cancellationToken);
        var key = BuildKey(scope, dependency, queryFingerprint);
        try
        {
            var database = multiplexer.GetDatabase();
            var cached = await database.StringGetAsync(key);
            if (cached.HasValue && TryDeserialize<T>(cached!, revision, out var value))
            {
                CacheMetrics.Hits.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
                return value!;
            }

            CacheMetrics.Misses.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            var lockKey = $"{key}:lock";
            var lockToken = Guid.NewGuid().ToString("N");
            var lockTaken = await database.LockTakeAsync(lockKey, lockToken, options.LockTtl);
            if (!lockTaken)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                var raced = await database.StringGetAsync(key);
                if (raced.HasValue && TryDeserialize<T>(raced!, revision, out value)) return value!;
                return await loader(cancellationToken);
            }

            try
            {
                var afterLock = await database.StringGetAsync(key);
                if (afterLock.HasValue && TryDeserialize<T>(afterLock!, revision, out value)) return value!;
                var loaded = await loader(cancellationToken);
                var afterReadRevision = await ReadRevisionAsync(scope.TenantId, dependency, scope.LegalEntityId, scope.BranchId, cancellationToken);
                if (afterReadRevision == revision)
                {
                    var envelope = new CacheEnvelope<T>(options.SchemaVersion, afterReadRevision, clock.GetUtcNow(), loaded);
                    await database.StringSetAsync(key, JsonSerializer.Serialize(envelope, JsonOptions), ttl, When.Always);
                }
                return loaded;
            }
            finally
            {
                await database.LockReleaseAsync(lockKey, lockToken);
            }
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            RedisFallbackLog(logger, dependency, exception);
            return await loader(cancellationToken);
        }
    }

    public async Task InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return;
        try
        {
            var marker = BuildInvalidationKey(invalidation);
            var database = multiplexer.GetDatabase();
            var current = await database.StringGetAsync(marker);
            if (!long.TryParse(current.ToString(), out var currentRevision) || invalidation.Revision > currentRevision)
                await database.StringSetAsync(marker, invalidation.Revision, TimeSpan.FromHours(1), When.Always);
            CacheMetrics.Invalidations.Add(1, new KeyValuePair<string, object?>("dependency", invalidation.Dependency));
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", invalidation.Dependency));
            InvalidationFallbackLog(logger, invalidation.Dependency, exception);
        }
    }

    private async Task<long> ReadRevisionAsync(TenantId tenantId, string dependency, Guid? legalEntityId, Guid? branchId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        var scopeKey = $"{legalEntityId?.ToString("D") ?? "all"}";
        await using var command = new NpgsqlCommand("select revision from platform.cache_dependency_revision where tenant_id=$1 and dependency=$2 and scope_key=$3", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(dependency);
        command.Parameters.AddWithValue(scopeKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result is long revision ? revision : result is int integer ? integer : 0;
    }

    private string BuildKey(CacheScope scope, string dependency, string queryFingerprint) =>
        $"platform:cache:v{options.SchemaVersion}:{scope.TenantId.Value:D}:{dependency}:{scope.LegalEntityId?.ToString("D") ?? "all"}:{scope.BranchId?.ToString("D") ?? "all"}:{scope.MembershipId?.ToString("D") ?? "none"}:{scope.PermissionVersion}:{queryFingerprint}";

    private static string BuildInvalidationKey(CacheInvalidation invalidation) =>
        $"{InvalidationPrefix}{invalidation.TenantId.Value:D}:{invalidation.Dependency}:{invalidation.LegalEntityId?.ToString("D") ?? "all"}:{invalidation.BranchId?.ToString("D") ?? "all"}";

    private static bool TryDeserialize<T>(RedisValue value, long revision, out T? result)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(value.ToString(), JsonOptions);
            if (envelope is not null && envelope.Revision >= revision)
            {
                result = envelope.Value;
                return true;
            }
        }
        catch (JsonException) { }
        result = default;
        return false;
    }

    private static bool IsRedisFailure(Exception exception) => exception is RedisException or RedisConnectionException or RedisTimeoutException or TimeoutException or IOException;
    private sealed record CacheEnvelope<T>(int SchemaVersion, long Revision, DateTimeOffset CachedAt, T Value);
}

public static class CacheKeyFingerprint
{
    public static string For(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class CacheMetrics
{
    public static readonly System.Diagnostics.Metrics.Meter Meter = new("Todir.Platform.Cache", "1.0.0");
    public static readonly System.Diagnostics.Metrics.Counter<long> Hits = Meter.CreateCounter<long>("todir.cache.hits");
    public static readonly System.Diagnostics.Metrics.Counter<long> Misses = Meter.CreateCounter<long>("todir.cache.misses");
    public static readonly System.Diagnostics.Metrics.Counter<long> Failures = Meter.CreateCounter<long>("todir.cache.failures");
    public static readonly System.Diagnostics.Metrics.Counter<long> Invalidations = Meter.CreateCounter<long>("todir.cache.invalidations");
}
