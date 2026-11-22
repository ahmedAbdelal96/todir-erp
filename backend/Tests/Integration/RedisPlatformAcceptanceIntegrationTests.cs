using Core.Caching;
using Npgsql;
using Shared.Primitives.Identifiers;
using StackExchange.Redis;
using Xunit;

namespace KernelIntegrationTests;

/// <summary>
/// Runs only against the disposable Redis/PostgreSQL acceptance stack supplied
/// by the local gate. No application business row is changed; revision bumps
/// are isolated to the acceptance database and create only disposable Outbox
/// evidence for the platform contract.
/// </summary>
[Collection("database-integration")]
public sealed class RedisPlatformAcceptanceIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task RealRedisKeepsInvalidationMarkerMonotonicForOutOfOrderEvents()
    {
        var tenant = RequiredTenant("TODIR_REDIS_ACCEPTANCE_TENANT_ID");
        var redisConfiguration = Required("TODIR_REDIS_ACCEPTANCE_REDIS");
        await using var dataSource = NpgsqlDataSource.Create(Required("TODIR_REDIS_ACCEPTANCE_CONNECTION"));
        using var multiplexer = await ConnectAsync(redisConfiguration);
        var database = multiplexer.GetDatabase();
        var marker = $"platform:cache:invalidation:{tenant.Value:D}:hr.employee.directory:all:all";
        await database.KeyDeleteAsync(marker);

        using var cache = CreateCache(multiplexer, redisConfiguration, dataSource);
        var revisions = Enumerable.Range(1, 64).OrderBy(_ => Random.Shared.Next()).Select(value =>
            cache.InvalidateAsync(new(tenant, PlatformCachePolicies.HrEmployeeDirectory.Name, null, null, value))).ToArray();
        var results = await Task.WhenAll(revisions);

        Assert.All(results, result => Assert.Equal(CacheInvalidationResult.Applied, result));
        Assert.Equal("64", (string?)await database.StringGetAsync(marker));
    }

    [Fact]
    public async Task RealPostgresRevisionBlocksStaleFillAcrossTwoRedisClients()
    {
        var tenant = RequiredTenant("TODIR_REDIS_ACCEPTANCE_TENANT_ID");
        var legalEntity = RequiredGuid("TODIR_REDIS_ACCEPTANCE_LEGAL_ENTITY_ID");
        var postgres = Required("TODIR_REDIS_ACCEPTANCE_CONNECTION");
        var redisConfiguration = Required("TODIR_REDIS_ACCEPTANCE_REDIS");
        await using var dataSource = NpgsqlDataSource.Create(postgres);
        using var firstMultiplexer = await ConnectAsync(redisConfiguration);
        using var secondMultiplexer = await ConnectAsync(redisConfiguration);
        using var firstCache = CreateCache(firstMultiplexer, redisConfiguration, dataSource);
        using var secondCache = CreateCache(secondMultiplexer, redisConfiguration, dataSource);

        await AdvanceRevisionAsync(dataSource, tenant, legalEntity);
        var fingerprint = CacheKeyFingerprint.For($"stale-fill:{Guid.CreateVersion7():D}");
        var scope = new CacheScope(tenant, legalEntity, null, Guid.CreateVersion7(), 11);
        var loaderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var oldFill = firstCache.GetOrCreateAsync(scope, PlatformCachePolicies.HrEmployeeDirectory, fingerprint, TimeSpan.FromSeconds(30), async _ =>
        {
            loaderStarted.SetResult();
            await releaseLoader.Task;
            return "old";
        });
        var loaderOrFailure = await Task.WhenAny(loaderStarted.Task, oldFill).WaitAsync(TimeSpan.FromSeconds(5));
        if (loaderOrFailure == oldFill) await oldFill;
        await loaderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await AdvanceRevisionAsync(dataSource, tenant, legalEntity);
        releaseLoader.SetResult();
        Assert.Equal("old", await oldFill);

        var cacheKey = $"platform:cache:v1:{tenant.Value:D}:{PlatformCachePolicies.HrEmployeeDirectory.Name}:{legalEntity:D}:all:{scope.MembershipId:D}:{scope.PermissionVersion}:{fingerprint}";
        Assert.False(await firstMultiplexer.GetDatabase().KeyExistsAsync(cacheKey));
        var refreshed = await secondCache.GetOrCreateAsync(scope, PlatformCachePolicies.HrEmployeeDirectory, fingerprint, TimeSpan.FromSeconds(30), _ => Task.FromResult("new"));
        Assert.Equal("new", refreshed);
    }

    [Fact]
    public async Task RealRedisSeparatesTenantAndPermissionScopes()
    {
        var tenantA = RequiredTenant("TODIR_REDIS_ACCEPTANCE_TENANT_ID");
        var tenantB = RequiredTenant("TODIR_REDIS_ACCEPTANCE_TENANT_B_ID");
        Assert.NotEqual(tenantA, tenantB);
        var redisConfiguration = Required("TODIR_REDIS_ACCEPTANCE_REDIS");
        await using var dataSource = NpgsqlDataSource.Create(Required("TODIR_REDIS_ACCEPTANCE_CONNECTION"));
        using var multiplexer = await ConnectAsync(redisConfiguration);
        using var cache = CreateCache(multiplexer, redisConfiguration, dataSource);
        var fingerprint = CacheKeyFingerprint.For("scope-isolation");

        var first = await cache.GetOrCreateAsync(new CacheScope(tenantA, null, null, Guid.CreateVersion7(), 1), PlatformCachePolicies.HrEmployeeDirectory, fingerprint, TimeSpan.FromSeconds(30), _ => Task.FromResult("atlas"));
        var second = await cache.GetOrCreateAsync(new CacheScope(tenantB, null, null, Guid.CreateVersion7(), 1), PlatformCachePolicies.HrEmployeeDirectory, fingerprint, TimeSpan.FromSeconds(30), _ => Task.FromResult("delta"));
        var permissionChanged = await cache.GetOrCreateAsync(new CacheScope(tenantA, null, null, Guid.CreateVersion7(), 2), PlatformCachePolicies.HrEmployeeDirectory, fingerprint, TimeSpan.FromSeconds(30), _ => Task.FromResult("recomputed"));

        Assert.Equal("atlas", first);
        Assert.Equal("delta", second);
        Assert.Equal("recomputed", permissionChanged);
    }

    private static RedisPlatformCache CreateCache(IConnectionMultiplexer multiplexer, string redisConfiguration, NpgsqlDataSource? dataSource = null)
    {
        var source = dataSource ?? NpgsqlDataSource.Create(Required("TODIR_REDIS_ACCEPTANCE_CONNECTION"));
        var options = new PlatformCacheOptions(true, true, true, redisConfiguration, 1000, 1000, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(2), 1);
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        return new RedisPlatformCache(multiplexer, source, options, TimeProvider.System, new PlatformCachePolicyRegistry(), Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisPlatformCache>.Instance);
    }

    private static async Task<IConnectionMultiplexer> ConnectAsync(string configuration)
    {
        var options = ConfigurationOptions.Parse(configuration);
        options.AbortOnConnectFail = false;
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
        await multiplexer.GetDatabase().PingAsync();
        return multiplexer;
    }

    private static async Task AdvanceRevisionAsync(NpgsqlDataSource dataSource, TenantId tenant, Guid legalEntity)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await context.ExecuteNonQueryAsync();
        }
        await using var command = new NpgsqlCommand("select platform.advance_cache_dependency_revision($1,$2,$3,$4,$5)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(PlatformCachePolicies.HrEmployeeDirectory.Name);
        command.Parameters.AddWithValue(legalEntity.ToString("D"));
        command.Parameters.AddWithValue(legalEntity);
        command.Parameters.AddWithValue(DBNull.Value);
        await command.ExecuteScalarAsync();
        await transaction.CommitAsync();
    }

    private static TenantId RequiredTenant(string name) => new(RequiredGuid(name));
    private static Guid RequiredGuid(string name) => Guid.Parse(Required(name));
    private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} for the Redis platform acceptance gate.");
}
