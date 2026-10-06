using Npgsql;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Messaging;

public interface IInboxStore
{
    Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class NpgsqlInboxStore(NpgsqlDataSource dataSource) : IInboxStore
{
    public async Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("insert into platform_messaging.inbox_message (consumer_endpoint, message_id, tenant_id, received_at) values ($1, $2, $3, now()) on conflict (consumer_endpoint, message_id) do nothing", connection, transaction);
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId); command.Parameters.AddWithValue(tenantId.Value);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_messaging.inbox_message set processed_at = now() where consumer_endpoint = $1 and message_id = $2", connection, transaction);
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId);
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
