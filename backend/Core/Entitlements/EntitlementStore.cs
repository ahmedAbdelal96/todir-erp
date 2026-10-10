using System.Text.Json;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Entitlements;

public interface IEntitlementSnapshotStore
{
    Task<EntitlementSnapshot?> LoadLatestAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class NpgsqlEntitlementSnapshotStore(NpgsqlDataSource dataSource) : IEntitlementSnapshotStore
{
    public async Task<EntitlementSnapshot?> LoadLatestAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand("select snapshot_version, issued_at, valid_until, key_id, modules::text, features::text, limits::text, signature from platform_entitlements.entitlement_snapshot where tenant_id = $1 order by snapshot_version desc limit 1", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var snapshot = new EntitlementSnapshot(
            tenantId,
            reader.GetInt64(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetString(3),
            DeserializeSet(reader.GetString(4)),
            DeserializeSet(reader.GetString(5)),
            JsonSerializer.Deserialize<Dictionary<string, long>>(reader.GetString(6)) ?? [],
            reader.GetString(7));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    }

    private static HashSet<string> DeserializeSet(string json) =>
        (JsonSerializer.Deserialize<string[]>(json) ?? []).ToHashSet(StringComparer.Ordinal);
}
