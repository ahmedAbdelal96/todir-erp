using MassTransit;
using Npgsql;
using Todir.Platform.Services;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Messaging;

public sealed class MessageFailureObserver(IMessageErrorStore errorStore) : IConsumeObserver
{
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        if (context.Message is not OutboxPublishedMessage message || context.MessageId is not { } messageId) return Task.CompletedTask;

        return errorStore.RecordAsync(
            new MessageError(
                messageId,
                message.TenantId,
                OutboxPublishedMessageConsumer.EndpointName,
                1,
                "message.consume_failed",
                exception.GetType().Name,
                DateTimeOffset.UtcNow,
                false),
            context.CancellationToken);
    }
}

public interface IMessageReplayService
{
    Task ReplayAsync(Guid messageId, TenantId tenantId, string consumerEndpoint, Guid operatorId, string reason, CancellationToken cancellationToken = default);
}

public sealed class UnavailableMessageReplayService : IMessageReplayService
{
    public Task ReplayAsync(Guid messageId, TenantId tenantId, string consumerEndpoint, Guid operatorId, string reason, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("message.replay_unavailable");
}

public sealed class NpgsqlMessageReplayService(
    NpgsqlDataSource dataSource,
    IPublishEndpoint publisher,
    IAuditWriter auditWriter) : IMessageReplayService
{
    public async Task ReplayAsync(Guid messageId, TenantId tenantId, string consumerEndpoint, Guid operatorId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("message.replay_reason_required", nameof(reason));
        if (string.IsNullOrWhiteSpace(consumerEndpoint)) throw new ArgumentException("message.replay_endpoint_required", nameof(consumerEndpoint));

        OutboxPublishedMessage message;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand("select id, message_type, payload::text, tenant_id, occurred_at, correlation_id, causation_id, source_module, trace_parent, attempts from platform_messaging.outbox_message where id = $1 and tenant_id = $2", connection, transaction);
            command.Parameters.AddWithValue(messageId);
            command.Parameters.AddWithValue(tenantId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new KeyNotFoundException("message.replay_not_found");
            message = new OutboxPublishedMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                new TenantId(reader.GetGuid(3)),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetGuid(5),
                reader.GetGuid(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9));
            await reader.DisposeAsync();

            await using var unresolved = new NpgsqlCommand("select exists (select 1 from platform_messaging.message_error where message_id = $1 and tenant_id = $2 and consumer_endpoint = $3 and resolved = false)", connection, transaction);
            unresolved.Parameters.AddWithValue(messageId);
            unresolved.Parameters.AddWithValue(tenantId.Value);
            unresolved.Parameters.AddWithValue(consumerEndpoint);
            if (!((bool?)await unresolved.ExecuteScalarAsync(cancellationToken) ?? false)) throw new InvalidOperationException("message.replay_not_authorized");
            await transaction.CommitAsync(cancellationToken);
        }

        await publisher.Publish(message, context =>
        {
            context.MessageId = message.MessageId;
            context.Headers.Set("message_type", message.MessageType);
            context.Headers.Set("tenant_id", message.TenantId.Value.ToString("D"));
            context.Headers.Set("correlation_id", message.CorrelationId.ToString("D"));
            context.Headers.Set("causation_id", message.CausationId.ToString("D"));
            if (message.TraceParent is not null) context.Headers.Set("traceparent", message.TraceParent);
        }, cancellationToken);

        await using var auditConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var auditTransaction = await auditConnection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(auditConnection, auditTransaction, tenantId, cancellationToken);
        await using (var resolve = new NpgsqlCommand("update platform_messaging.message_error set resolved = true where message_id = $1 and tenant_id = $2 and consumer_endpoint = $3 and resolved = false", auditConnection, auditTransaction))
        {
            resolve.Parameters.AddWithValue(messageId);
            resolve.Parameters.AddWithValue(tenantId.Value);
            resolve.Parameters.AddWithValue(consumerEndpoint);
            await resolve.ExecuteNonQueryAsync(cancellationToken);
        }
        var audit = new AuditEntry(
            Guid.CreateVersion7(),
            tenantId,
            null,
            null,
            "Support",
            operatorId,
            null,
            "platform-messaging",
            "message_replayed",
            "message",
            messageId,
            null,
            null,
            reason,
            "message-replay",
            message.CorrelationId,
            null,
            DateTimeOffset.UtcNow,
            System.Text.Json.JsonSerializer.Serialize(new { consumerEndpoint }));
        if (auditWriter is NpgsqlAuditWriter) await NpgsqlAuditWriter.WriteAsync(auditConnection, auditTransaction, audit, cancellationToken);
        else await auditWriter.WriteAsync(audit, cancellationToken);
        await auditTransaction.CommitAsync(cancellationToken);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
