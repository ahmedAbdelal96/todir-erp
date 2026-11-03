using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public sealed record UserNavigationSnapshot(IReadOnlyList<string> Favorites, IReadOnlyList<string> Recent);
public sealed record NavigationKeyRequest(string NavigationKey);

public interface IUserNavigationPreferencesStore
{
    Task<UserNavigationSnapshot> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(TenantId tenantId, IdentityId identityId, string navigationKey, bool favorite, CancellationToken cancellationToken = default);
    Task RecordVisitAsync(TenantId tenantId, IdentityId identityId, string navigationKey, CancellationToken cancellationToken = default);
}

public sealed class NullUserNavigationPreferencesStore : IUserNavigationPreferencesStore
{
    public Task<UserNavigationSnapshot> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserNavigationSnapshot([], []));

    public Task SetFavoriteAsync(TenantId tenantId, IdentityId identityId, string navigationKey, bool favorite, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RecordVisitAsync(TenantId tenantId, IdentityId identityId, string navigationKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NpgsqlUserNavigationPreferencesStore(NpgsqlDataSource dataSource) : IUserNavigationPreferencesStore
{
    private const int RecentLimit = 10;

    public async Task<UserNavigationSnapshot> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);

        await using var command = new NpgsqlCommand("""
            select
                coalesce((
                    select array_agg(favorite.navigation_key order by favorite.favorite_at desc, favorite.navigation_key)
                    from (
                        select navigation_key, favorite_at
                        from platform.user_navigation_state
                        where tenant_id = $1 and identity_id = $2 and favorite_at is not null
                        order by favorite_at desc, navigation_key
                        limit 50
                    ) favorite
                ), array[]::text[]),
                coalesce((
                    select array_agg(recent.navigation_key order by recent.last_visited_at desc, recent.navigation_key)
                    from (
                        select navigation_key, last_visited_at
                        from platform.user_navigation_state
                        where tenant_id = $1 and identity_id = $2 and last_visited_at is not null
                        order by last_visited_at desc, navigation_key
                        limit 10
                    ) recent
                ), array[]::text[])
            """, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new UserNavigationSnapshot([], []);
        var result = new UserNavigationSnapshot(reader.GetFieldValue<string[]>(0), reader.GetFieldValue<string[]>(1));
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task SetFavoriteAsync(TenantId tenantId, IdentityId identityId, string navigationKey, bool favorite, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);

        if (favorite)
        {
            await using var command = new NpgsqlCommand("""
                insert into platform.user_navigation_state (tenant_id, identity_id, navigation_key, favorite_at, last_visited_at, created_at, updated_at)
                values ($1, $2, $3, now(), null, now(), now())
                on conflict (tenant_id, identity_id, navigation_key) do update
                    set favorite_at = coalesce(platform.user_navigation_state.favorite_at, now()),
                        updated_at = now()
                """, connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(identityId.Value);
            command.Parameters.AddWithValue(navigationKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using (var update = new NpgsqlCommand("update platform.user_navigation_state set favorite_at = null, updated_at = now() where tenant_id = $1 and identity_id = $2 and navigation_key = $3 and favorite_at is not null", connection, transaction))
            {
                update.Parameters.AddWithValue(tenantId.Value);
                update.Parameters.AddWithValue(identityId.Value);
                update.Parameters.AddWithValue(navigationKey);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var delete = new NpgsqlCommand("delete from platform.user_navigation_state where tenant_id = $1 and identity_id = $2 and navigation_key = $3 and favorite_at is null and last_visited_at is null", connection, transaction);
            delete.Parameters.AddWithValue(tenantId.Value);
            delete.Parameters.AddWithValue(identityId.Value);
            delete.Parameters.AddWithValue(navigationKey);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordVisitAsync(TenantId tenantId, IdentityId identityId, string navigationKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);

        await using var command = new NpgsqlCommand("""
            with recorded as (
                insert into platform.user_navigation_state (tenant_id, identity_id, navigation_key, favorite_at, last_visited_at, created_at, updated_at)
                values ($1, $2, $3, null, now(), now(), now())
                on conflict (tenant_id, identity_id, navigation_key) do update
                    set last_visited_at = now(), updated_at = now()
                returning tenant_id, identity_id
            )
            delete from platform.user_navigation_state stale
            using recorded
            where stale.tenant_id = recorded.tenant_id
              and stale.identity_id = recorded.identity_id
              and stale.favorite_at is null
              and stale.last_visited_at is not null
              and stale.navigation_key not in (
                  select navigation_key
                  from platform.user_navigation_state
                  where tenant_id = $1 and identity_id = $2 and last_visited_at is not null
                  order by last_visited_at desc, navigation_key
                  limit 10
              )
            """, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(navigationKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public static class UserNavigationKey
{
    public static bool IsValid(string? value)
    {
        if (value is not { Length: >= 1 and <= 64 }) return false;
        var last = value[^1];
        var hasValidBoundary = value[0] is >= 'a' and <= 'z' && (last is >= 'a' and <= 'z' or >= '0' and <= '9');
        return hasValidBoundary &&
               !value.Contains("--", StringComparison.Ordinal) &&
               value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }
}
