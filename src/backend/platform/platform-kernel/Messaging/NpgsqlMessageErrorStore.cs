using Npgsql;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Messaging;

public sealed class NpgsqlMessageErrorStore(NpgsqlDataSource dataSource) : IMessageErrorStore
{
    public async Task RecordAsync(MessageError messageError, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, messageError.TenantId, cancellationToken);
        const string sql = "insert into platform_messaging.message_error (id, message_id, tenant_id, consumer_endpoint, attempt, error_code, detail, occurred_at, resolved) values ($1, $2, $3, $4, $5, $6, $7, $8, $9)";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(messageError.MessageId);
        command.Parameters.AddWithValue(messageError.TenantId.Value);
        command.Parameters.AddWithValue(messageError.ConsumerEndpoint);
        command.Parameters.AddWithValue(messageError.Attempt);
        command.Parameters.AddWithValue(messageError.ErrorCode);
        command.Parameters.AddWithValue(messageError.Detail);
        command.Parameters.AddWithValue(messageError.OccurredAt);
        command.Parameters.AddWithValue(messageError.Resolved);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkResolvedAsync(Guid messageId, string consumerEndpoint, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_messaging.message_error set resolved = true where tenant_id = $1 and message_id = $2 and consumer_endpoint = $3 and resolved = false", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(consumerEndpoint);
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
