using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public sealed record UserDisplayPreferences(string Language, string Theme, string Density, bool SidebarCollapsed)
{
    public static UserDisplayPreferences Default { get; } = new("ar", "default", "comfortable", false);
}

public sealed record UpdateUserDisplayPreferencesRequest(string? Language, string? Theme, string? Density, bool? SidebarCollapsed);

public interface IUserDisplayPreferencesStore
{
    Task<UserDisplayPreferences> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default);
    Task SaveAsync(TenantId tenantId, IdentityId identityId, UpdateUserDisplayPreferencesRequest preferences, CancellationToken cancellationToken = default);
}

public sealed class NullUserDisplayPreferencesStore : IUserDisplayPreferencesStore
{
    public Task<UserDisplayPreferences> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(UserDisplayPreferences.Default);

    public Task SaveAsync(TenantId tenantId, IdentityId identityId, UpdateUserDisplayPreferencesRequest preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class NpgsqlUserDisplayPreferencesStore(NpgsqlDataSource dataSource) : IUserDisplayPreferencesStore
{
    public async Task<UserDisplayPreferences> LoadAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            select language, theme, density, sidebar_collapsed
            from platform.user_display_preferences
            where tenant_id = $1 and identity_id = $2
            """, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.CloseAsync();
            await transaction.CommitAsync(cancellationToken);
            return UserDisplayPreferences.Default;
        }

        var preferences = new UserDisplayPreferences(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3));
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return preferences;
    }

    public async Task SaveAsync(TenantId tenantId, IdentityId identityId, UpdateUserDisplayPreferencesRequest preferences, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into platform.user_display_preferences (tenant_id, identity_id, language, theme, density, sidebar_collapsed, updated_at)
            values ($1, $2, coalesce($3, 'ar'), coalesce($4, 'default'), coalesce($5, 'comfortable'), coalesce($6, false), now())
            on conflict (tenant_id, identity_id) do update
                set language = coalesce($3, platform.user_display_preferences.language),
                    theme = coalesce($4, platform.user_display_preferences.theme),
                    density = coalesce($5, platform.user_display_preferences.density),
                    sidebar_collapsed = coalesce($6, platform.user_display_preferences.sidebar_collapsed),
                    updated_at = now()
            """, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)preferences.Language ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)preferences.Theme ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text, Value = (object?)preferences.Density ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Boolean, Value = (object?)preferences.SidebarCollapsed ?? DBNull.Value });
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

public static class UserDisplayPreferenceRules
{
    public static bool IsValid(UpdateUserDisplayPreferencesRequest? request) => request is not null &&
        (request.Language is null or "ar" or "en") &&
        (request.Theme is null or "default" or "dark") &&
        (request.Density is null or "comfortable" or "compact") &&
        (request.Language is not null || request.Theme is not null || request.Density is not null || request.SidebarCollapsed is not null);
}
