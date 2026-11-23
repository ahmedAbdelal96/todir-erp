using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
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
    public bool TlsEnabled { get; init; }
    public bool RequireSecureTransport { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int MaximumEntryBytes { get; init; } = 1_048_576;

    public static PlatformCacheOptions FromConfiguration(IConfiguration configuration, string? environmentName = null) => new(
        configuration.GetValue("TODIR_REDIS_ENABLED", false),
        configuration.GetValue("TODIR_CACHE_HR_DIRECTORY_ENABLED", false),
        configuration.GetValue("TODIR_CACHE_STABLE_REFERENCE_DATA_ENABLED", false),
        string.IsNullOrWhiteSpace(configuration["TODIR_REDIS_CONFIGURATION"]) ? "localhost:6379,abortConnect=false" : configuration["TODIR_REDIS_CONFIGURATION"]!,
        Math.Clamp(configuration.GetValue("TODIR_REDIS_CONNECT_TIMEOUT_MS", 1000), 250, 10_000),
        Math.Clamp(configuration.GetValue("TODIR_REDIS_SYNC_TIMEOUT_MS", 1000), 250, 10_000),
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("TODIR_CACHE_DEFAULT_TTL_SECONDS", 60), 5, 3600)),
        TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("TODIR_CACHE_LOCK_TTL_SECONDS", 5), 1, 30)),
        Math.Max(1, configuration.GetValue("TODIR_CACHE_SCHEMA_VERSION", 1)))
    {
        TlsEnabled = configuration.GetValue("TODIR_REDIS_TLS", false),
        RequireSecureTransport = configuration.GetValue("TODIR_REDIS_REQUIRE_TLS", false)
            || (configuration.GetValue("TODIR_REDIS_ENABLED", false)
                && !string.IsNullOrWhiteSpace(environmentName)
                && !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)),
        Username = configuration["TODIR_REDIS_USERNAME"],
        Password = configuration["TODIR_REDIS_PASSWORD"],
        MaximumEntryBytes = Math.Clamp(configuration.GetValue("TODIR_CACHE_MAX_ENTRY_BYTES", 1_048_576), 65_536, 16_777_216)
    };
}

public enum CacheConsistencyModel { BoundedStaleness, StableReference }

public sealed record CacheDependencyPolicy(
    string Name,
    CacheConsistencyModel Consistency,
    string OwningModule,
    string AuthorizationRule,
    string InvalidationRule,
    bool ContainsSensitiveData,
    TimeSpan MaximumTtl);

public static class PlatformCachePolicies
{
    public static readonly CacheDependencyPolicy HrEmployeeDirectory = new(
        "hr.employee.directory",
        CacheConsistencyModel.BoundedStaleness,
        "hr",
        "hr.employee.read_basic authorization plus membership and permission version scope",
        "PostgreSQL employee/employment revision in the writer transaction and durable Outbox invalidation",
        false,
        TimeSpan.FromSeconds(30));

    public static readonly CacheDependencyPolicy FinanceBaseCurrencyReference = new(
        "finbase.currency.reference",
        CacheConsistencyModel.StableReference,
        "finbase",
        "finbase.currency.read authorization",
        "migration-owned reference dataset and schema-version invalidation",
        false,
        TimeSpan.FromHours(1));

    private static readonly Dictionary<string, CacheDependencyPolicy> Approved =
        new Dictionary<string, CacheDependencyPolicy>(StringComparer.Ordinal)
        {
            [HrEmployeeDirectory.Name] = HrEmployeeDirectory,
            [FinanceBaseCurrencyReference.Name] = FinanceBaseCurrencyReference
        };

    public static bool TryGet(string name, out CacheDependencyPolicy policy) => Approved.TryGetValue(name, out policy!);

    public static void EnsureApproved(CacheDependencyPolicy policy, TimeSpan ttl)
    {
        if (!TryGet(policy.Name, out var approved) || approved != policy || policy.ContainsSensitiveData || ttl > approved.MaximumTtl)
            throw new InvalidOperationException("platform.cache_policy_not_approved");
    }
}

public sealed record CacheScope(TenantId TenantId, Guid? LegalEntityId, Guid? BranchId, Guid? MembershipId, int PermissionVersion);
public sealed record CacheInvalidation(TenantId TenantId, string Dependency, Guid? LegalEntityId, Guid? BranchId, long Revision);
public enum CacheInvalidationResult { Applied, Disabled, Unavailable }

public interface IPlatformCache
{
    bool IsEnabled { get; }
    Task<T> GetOrCreateAsync<T>(CacheScope scope, CacheDependencyPolicy policy, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default);
    Task<CacheInvalidationResult> InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default);
}

public sealed class NoopPlatformCache : IPlatformCache
{
    public bool IsEnabled => false;
    public Task<T> GetOrCreateAsync<T>(CacheScope scope, CacheDependencyPolicy policy, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default)
    {
        PlatformCachePolicies.EnsureApproved(policy, ttl);
        return loader(cancellationToken);
    }

    public Task<CacheInvalidationResult> InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default) =>
        Task.FromResult(CacheInvalidationResult.Disabled);
}

public sealed class RedisPlatformCache(
    IConnectionMultiplexer multiplexer,
    NpgsqlDataSource dataSource,
    PlatformCacheOptions options,
    TimeProvider clock,
    IPlatformCachePolicyRegistry policyRegistry,
    ILogger<RedisPlatformCache> logger) : IPlatformCache, IDisposable
{
    private static readonly Action<ILogger, string, Exception?> RedisFallbackLog = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2301, "RedisFallback"), "Redis cache unavailable; falling back to PostgreSQL for {Dependency}");
    private static readonly Action<ILogger, string, Exception?> InvalidationFallbackLog = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2302, "RedisInvalidationFallback"), "Redis invalidation unavailable; PostgreSQL revision remains authoritative for {Dependency}");
    private static readonly Action<ILogger, string, bool, Exception?> FirstUseLog = LoggerMessage.Define<string, bool>(LogLevel.Information, new EventId(2304, "RedisCacheFirstUse"), "Platform cache first use for {Dependency}; redisConnected={RedisConnected}");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string InvalidationPrefix = "platform:cache:invalidation:";
    private const string MonotonicRevisionScript = "local current=redis.call('GET',KEYS[1]); local incoming=tonumber(ARGV[1]); if (not current) or (incoming > tonumber(current)) then redis.call('SET',KEYS[1],ARGV[1],'EX',ARGV[2]); return 1 end return 0";
    private const int MaximumFingerprintLength = 128;
    private readonly ConcurrentDictionary<string, Task<object?>> localFills = new(StringComparer.Ordinal);
    private int firstUseLogged;

    public bool IsEnabled => options.Enabled && multiplexer.IsConnected;

    public async Task<T> GetOrCreateAsync<T>(CacheScope scope, CacheDependencyPolicy policy, string queryFingerprint, TimeSpan ttl, Func<CancellationToken, Task<T>> loader, CancellationToken cancellationToken = default)
    {
        PlatformCachePolicies.EnsureApproved(policy, ttl);
        var dependency = policy.Name;
        if (!options.Enabled) return await loader(cancellationToken);

        if (Interlocked.Exchange(ref firstUseLogged, 1) == 0)
            FirstUseLog(logger, dependency, multiplexer.IsConnected, null);

        var key = BuildKey(scope, dependency, queryFingerprint);
        try
        {
            var database = multiplexer.GetDatabase();
            return await GetOrFillCoalescedAsync(scope, dependency, key, ttl, loader, database, cancellationToken);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            RedisFallbackLog(logger, dependency, exception);
            return await loader(cancellationToken);
        }
    }

    private async Task<T> GetOrFillCoalescedAsync<T>(CacheScope scope, string dependency, string key, TimeSpan ttl,
        Func<CancellationToken, Task<T>> loader, IDatabase database, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (localFills.TryGetValue(key, out var existing))
            {
                CacheMetrics.CoalescedWaits.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
                var result = await existing.WaitAsync(cancellationToken);
                if (result is null) return default!;
                if (result is T typed) return typed;
                throw new InvalidOperationException("platform.cache_key_type_collision");
            }

            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!localFills.TryAdd(key, completion.Task)) continue;
            try
            {
                var loaded = await ReadOrFillAsync(scope, dependency, key, ttl, loader, database, cancellationToken);
                completion.TrySetResult(loaded);
                return loaded;
            }
            catch (Exception exception) when (IsRedisFailure(exception))
            {
                CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
                RedisFallbackLog(logger, dependency, exception);
                try
                {
                    var fallback = await loader(cancellationToken);
                    completion.TrySetResult(fallback);
                    return fallback;
                }
                catch (Exception fallbackException)
                {
                    completion.TrySetException(fallbackException);
                    throw;
                }
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                throw;
            }
            finally
            {
                localFills.TryRemove(new KeyValuePair<string, Task<object?>>(key, completion.Task));
            }
        }
    }

    private async Task<T> ReadOrFillAsync<T>(CacheScope scope, string dependency, string key, TimeSpan ttl,
        Func<CancellationToken, Task<T>> loader, IDatabase database, CancellationToken cancellationToken)
    {
        var revision = await ReadRevisionAsync(scope.TenantId, dependency, scope.LegalEntityId, scope.BranchId, cancellationToken);
        var cached = await database.StringGetAsync(key);
        if (cached.HasValue && TryDeserialize<T>(cached!, revision, out var value))
        {
            CacheMetrics.Hits.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            return value!;
        }

        CacheMetrics.Misses.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
        return await FillWithDistributedLockAsync(scope, dependency, key, revision, ttl, loader, database, cancellationToken);
    }

    private async Task<T> FillWithDistributedLockAsync<T>(CacheScope scope, string dependency, string key, long revision, TimeSpan ttl,
        Func<CancellationToken, Task<T>> loader, IDatabase database, CancellationToken cancellationToken)
    {
        var lockKey = $"{key}:lock";
        var lockToken = Guid.NewGuid().ToString("N");
        var lockTaken = await database.LockTakeAsync(lockKey, lockToken, options.LockTtl);
        if (!lockTaken)
        {
            CacheMetrics.StampedeWaits.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            var deadline = clock.GetUtcNow() + options.LockTtl;
            while (clock.GetUtcNow() < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                var raced = await database.StringGetAsync(key);
                if (raced.HasValue && TryDeserialize<T>(raced!, revision, out var racedValue)) return racedValue!;
            }
            return await loader(cancellationToken);
        }

        try
        {
            var afterLock = await database.StringGetAsync(key);
            if (afterLock.HasValue && TryDeserialize<T>(afterLock!, revision, out var value)) return value!;
            var loaded = await loader(cancellationToken);
            var afterReadRevision = await ReadRevisionAsync(scope.TenantId, dependency, scope.LegalEntityId, scope.BranchId, cancellationToken);
            if (afterReadRevision == revision)
            {
                var envelope = new CacheEnvelope<T>(options.SchemaVersion, afterReadRevision, clock.GetUtcNow(), loaded);
                var serialized = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
                if (serialized.Length <= options.MaximumEntryBytes)
                    await database.StringSetAsync(key, serialized, ttl, When.Always);
                else
                    CacheMetrics.OversizedSkipped.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            }
            return loaded;
        }
        finally
        {
            try { await database.LockReleaseAsync(lockKey, lockToken); }
            catch (Exception exception) when (IsRedisFailure(exception))
            {
                CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", dependency));
            }
        }
    }

    public async Task<CacheInvalidationResult> InvalidateAsync(CacheInvalidation invalidation, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return CacheInvalidationResult.Disabled;
        if (!policyRegistry.IsApproved(invalidation.Dependency)) throw new InvalidOperationException("platform.cache_policy_not_approved");
        try
        {
            var marker = BuildInvalidationKey(invalidation);
            var database = multiplexer.GetDatabase();
            await database.ScriptEvaluateAsync(MonotonicRevisionScript, [marker], [invalidation.Revision, 3600]);
            CacheMetrics.Invalidations.Add(1, new KeyValuePair<string, object?>("dependency", invalidation.Dependency));
            return CacheInvalidationResult.Applied;
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            CacheMetrics.Failures.Add(1, new KeyValuePair<string, object?>("dependency", invalidation.Dependency));
            InvalidationFallbackLog(logger, invalidation.Dependency, exception);
            return CacheInvalidationResult.Unavailable;
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
        const string revisionSql = "select coalesce(max(revision),0) from platform.cache_dependency_revision where tenant_id=$1 and dependency=$2 and ($3::uuid is null or legal_entity_id is null or legal_entity_id=$3) and ($4::uuid is null or branch_id is null or branch_id=$4)";
        await using var command = new NpgsqlCommand(revisionSql, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(dependency);
        command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue((object?)branchId ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result is long revision ? revision : result is int integer ? integer : Convert.ToInt64(result ?? 0L, CultureInfo.InvariantCulture);
    }

    private string BuildKey(CacheScope scope, string dependency, string queryFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryFingerprint);
        var boundedFingerprint = queryFingerprint.Length <= MaximumFingerprintLength ? queryFingerprint : CacheKeyFingerprint.For(queryFingerprint);
        return $"platform:cache:v{options.SchemaVersion}:{scope.TenantId.Value:D}:{dependency}:{scope.LegalEntityId?.ToString("D") ?? "all"}:{scope.BranchId?.ToString("D") ?? "all"}:{scope.MembershipId?.ToString("D") ?? "none"}:{scope.PermissionVersion}:{boundedFingerprint}";
    }

    private static string BuildInvalidationKey(CacheInvalidation invalidation) =>
        $"{InvalidationPrefix}{invalidation.TenantId.Value:D}:{invalidation.Dependency}:{invalidation.LegalEntityId?.ToString("D") ?? "all"}:{invalidation.BranchId?.ToString("D") ?? "all"}";

    private bool TryDeserialize<T>(RedisValue value, long revision, out T? result)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(value.ToString(), JsonOptions);
            if (envelope is not null && envelope.SchemaVersion == options.SchemaVersion && envelope.Revision == revision)
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

    public void Dispose() => multiplexer.Dispose();
}

public interface IPlatformCachePolicyRegistry
{
    bool IsApproved(string dependency);
}

public sealed class PlatformCachePolicyRegistry : IPlatformCachePolicyRegistry
{
    public bool IsApproved(string dependency) => PlatformCachePolicies.TryGet(dependency, out _);
}

public static class CacheKeyFingerprint
{
    public static string For(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
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
    public static readonly System.Diagnostics.Metrics.Counter<long> CoalescedWaits = Meter.CreateCounter<long>("todir.cache.coalesced_waits");
    public static readonly System.Diagnostics.Metrics.Counter<long> StampedeWaits = Meter.CreateCounter<long>("todir.cache.stampede_waits");
    public static readonly System.Diagnostics.Metrics.Counter<long> OversizedSkipped = Meter.CreateCounter<long>("todir.cache.oversized_skipped");
}
