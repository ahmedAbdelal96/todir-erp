using Npgsql;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Messaging;

public interface IInboxStore
{
    Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, CancellationToken cancellationToken = default);
}

public sealed class NpgsqlInboxStore(NpgsqlDataSource dataSource) : IInboxStore
{
    public async Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("insert into platform_messaging.inbox_message (consumer_endpoint, message_id, tenant_id, received_at) values ($1, $2, $3, now()) on conflict (consumer_endpoint, message_id) do nothing");
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId); command.Parameters.AddWithValue(tenantId.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("update platform_messaging.inbox_message set processed_at = now() where consumer_endpoint = $1 and message_id = $2");
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
