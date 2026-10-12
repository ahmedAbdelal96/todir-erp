using System.Collections.Concurrent;
using System.Text.Json;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Services;

public interface INotificationStore
{
    Task EnqueueAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Notification?> ClaimNextPendingAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task MarkSentAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Notification notification, string errorCode, CancellationToken cancellationToken = default);
}

public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly ConcurrentDictionary<Guid, Notification> notifications = new();

    public Task EnqueueAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!notifications.TryAdd(notification.Id, notification)) throw new InvalidOperationException("notification.duplicate");
        return Task.CompletedTask;
    }

    public Task<Notification?> ClaimNextPendingAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var pair in notifications.Where(item => item.Value.TenantId == tenantId && item.Value.Status == NotificationStatus.Pending).OrderBy(item => item.Value.CreatedAt))
        {
            var sending = pair.Value with { Status = NotificationStatus.Sending, Attempts = pair.Value.Attempts + 1 };
            if (notifications.TryUpdate(pair.Key, sending, pair.Value)) return Task.FromResult<Notification?>(sending);
        }
        return Task.FromResult<Notification?>(null);
    }

    public Task MarkSentAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        notifications.AddOrUpdate(notification.Id, notification with { Status = NotificationStatus.Sent, SentAt = DateTimeOffset.UtcNow }, (_, current) => current with { Status = NotificationStatus.Sent, SentAt = DateTimeOffset.UtcNow });
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Notification notification, string errorCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        notifications.AddOrUpdate(notification.Id, notification with { Status = NotificationStatus.Failed }, (_, current) => current with { Status = NotificationStatus.Failed });
        return Task.CompletedTask;
    }
}

public sealed class NpgsqlNotificationStore(NpgsqlDataSource dataSource) : INotificationStore
{
    public async Task EnqueueAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, notification.TenantId, cancellationToken);
        await using var command = new NpgsqlCommand("insert into platform.notification (id, tenant_id, recipient_ref, template_key, channel, payload, status, attempts, created_at) values ($1, $2, $3, $4, $5, $6::jsonb, 'Pending', 0, $7)", connection, transaction);
        command.Parameters.AddWithValue(notification.Id); command.Parameters.AddWithValue(notification.TenantId.Value); command.Parameters.AddWithValue(notification.RecipientRef); command.Parameters.AddWithValue(notification.TemplateKey); command.Parameters.AddWithValue(notification.Channel.ToString()); command.Parameters.AddWithValue(notification.Payload.GetRawText()); command.Parameters.AddWithValue(notification.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Notification?> ClaimNextPendingAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("with candidate as (select id from platform.notification where tenant_id = $1 and status = 'Pending' order by created_at, id for update skip locked limit 1) update platform.notification notification set status = 'Sending', attempts = attempts + 1 from candidate where notification.id = candidate.id and notification.tenant_id = $1 returning notification.id, notification.tenant_id, notification.recipient_ref, notification.template_key, notification.channel, notification.payload::text, notification.status, notification.attempts, notification.created_at, notification.sent_at", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var notification = Read(reader);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return notification;
    }

    public Task MarkSentAsync(Notification notification, CancellationToken cancellationToken = default) => UpdateAsync(notification, "Sent", null, cancellationToken);

    public Task MarkFailedAsync(Notification notification, string errorCode, CancellationToken cancellationToken = default) => UpdateAsync(notification, "Failed", errorCode, cancellationToken);

    private async Task UpdateAsync(Notification notification, string status, string? errorCode, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, notification.TenantId, cancellationToken);
        await using (var update = new NpgsqlCommand("update platform.notification set status = $1, sent_at = case when $1 = 'Sent' then now() else sent_at end where tenant_id = $2 and id = $3", connection, transaction))
        {
            update.Parameters.AddWithValue(status); update.Parameters.AddWithValue(notification.TenantId.Value); update.Parameters.AddWithValue(notification.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var delivery = new NpgsqlCommand("insert into platform.notification_delivery (id, tenant_id, notification_id, channel, status, attempt_at, error_code) values ($1, $2, $3, $4, $5, now(), $6)", connection, transaction))
        {
            delivery.Parameters.AddWithValue(Guid.CreateVersion7()); delivery.Parameters.AddWithValue(notification.TenantId.Value); delivery.Parameters.AddWithValue(notification.Id); delivery.Parameters.AddWithValue(notification.Channel.ToString()); delivery.Parameters.AddWithValue(status); delivery.Parameters.AddWithValue((object?)errorCode ?? DBNull.Value);
            await delivery.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static Notification Read(NpgsqlDataReader reader)
    {
        using var payload = JsonDocument.Parse(reader.GetString(5));
        return new Notification(reader.GetGuid(0), new TenantId(reader.GetGuid(1)), reader.GetString(2), reader.GetString(3), Enum.Parse<NotificationChannel>(reader.GetString(4), true), payload.RootElement.Clone(), Enum.Parse<NotificationStatus>(reader.GetString(6), true), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class NotificationDispatcher(INotificationStore store, IEnumerable<INotificationSender> senders)
{
    private readonly Dictionary<NotificationChannel, INotificationSender> senders = senders.ToDictionary(sender => sender.Channel);

    public async Task<Notification> EnqueueAsync(TenantId tenantId, string recipientRef, string templateKey, NotificationChannel channel, JsonElement payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        var notification = new Notification(Guid.CreateVersion7(), tenantId, recipientRef, templateKey, channel, payload.Clone(), NotificationStatus.Pending, 0, DateTimeOffset.UtcNow, null);
        await store.EnqueueAsync(notification, cancellationToken);
        return notification;
    }

    public async Task<bool> DispatchNextAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var notification = await store.ClaimNextPendingAsync(tenantId, cancellationToken);
        if (notification is null) return false;
        if (!senders.TryGetValue(notification.Channel, out var sender))
        {
            await store.MarkFailedAsync(notification, "notification.channel_unavailable", cancellationToken);
            throw new InvalidOperationException("notification.channel_unavailable");
        }
        try
        {
            await sender.SendAsync(notification, cancellationToken);
            await store.MarkSentAsync(notification, cancellationToken);
            return true;
        }
        catch
        {
            await store.MarkFailedAsync(notification, "notification.delivery_failed", cancellationToken);
            throw;
        }
    }
}
