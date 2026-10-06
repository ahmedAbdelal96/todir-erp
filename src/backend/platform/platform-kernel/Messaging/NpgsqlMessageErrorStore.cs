using Npgsql;

namespace Todir.Platform.Messaging;

public sealed class NpgsqlMessageErrorStore(NpgsqlDataSource dataSource) : IMessageErrorStore
{
    public async Task RecordAsync(MessageError messageError, CancellationToken cancellationToken = default)
    {
        const string sql = "insert into platform_messaging.message_error (id, message_id, tenant_id, consumer_endpoint, attempt, error_code, detail, occurred_at, resolved) values ($1, $2, $3, $4, $5, $6, $7, $8, $9)";
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(messageError.MessageId);
        command.Parameters.AddWithValue(messageError.TenantId.Value);
        command.Parameters.AddWithValue(messageError.ConsumerEndpoint);
        command.Parameters.AddWithValue(messageError.Attempt);
        command.Parameters.AddWithValue(messageError.ErrorCode);
        command.Parameters.AddWithValue(messageError.Detail);
        command.Parameters.AddWithValue(messageError.OccurredAt);
        command.Parameters.AddWithValue(messageError.Resolved);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkResolvedAsync(Guid messageId, string consumerEndpoint, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("update platform_messaging.message_error set resolved = true where message_id = $1 and consumer_endpoint = $2 and resolved = false");
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(consumerEndpoint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
