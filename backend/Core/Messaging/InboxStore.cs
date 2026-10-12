using System.Collections.Concurrent;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Messaging;

public interface IInboxStore
{
    Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryInboxStore(TimeProvider? timeProvider = null, TimeSpan? leaseDuration = null) : IInboxStore
{
    private readonly ConcurrentDictionary<(string Endpoint, Guid MessageId), InboxState> messages = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan lease = leaseDuration ?? TimeSpan.FromMinutes(5);

    public Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (consumerEndpoint, messageId);
        var now = clock.GetUtcNow();
        var candidate = new InboxState(tenantId, now.Add(lease), false);
        if (messages.TryAdd(key, candidate)) return Task.FromResult(true);
        while (messages.TryGetValue(key, out var current))
        {
            if (current.TenantId != tenantId || current.Processed || current.LeaseUntil > now) return Task.FromResult(false);
            if (messages.TryUpdate(key, candidate, current)) return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (consumerEndpoint, messageId);
        while (messages.TryGetValue(key, out var current))
        {
            if (current.TenantId != tenantId || current.Processed) return Task.CompletedTask;
            if (messages.TryUpdate(key, current with { Processed = true }, current)) return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }

    private sealed record InboxState(TenantId TenantId, DateTimeOffset LeaseUntil, bool Processed);
}

public sealed class NpgsqlInboxStore(NpgsqlDataSource dataSource) : IInboxStore
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<bool> TryBeginAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("insert into platform.inbox_message (consumer_endpoint, message_id, tenant_id, received_at, lease_until, attempts) values ($1, $2, $3, now(), now() + $4, 1) on conflict (consumer_endpoint, message_id) do update set received_at = now(), lease_until = now() + $4, attempts = platform.inbox_message.attempts + 1 where platform.inbox_message.processed_at is null and (platform.inbox_message.lease_until is null or platform.inbox_message.lease_until <= now()) returning 1", connection, transaction);
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(LeaseDuration);
        var inserted = await command.ExecuteScalarAsync(cancellationToken) is not null;
        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task MarkProcessedAsync(string consumerEndpoint, Guid messageId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.inbox_message set processed_at = now(), lease_until = null where consumer_endpoint = $1 and message_id = $2 and tenant_id = $3 and processed_at is null", connection, transaction);
        command.Parameters.AddWithValue(consumerEndpoint); command.Parameters.AddWithValue(messageId); command.Parameters.AddWithValue(tenantId.Value);
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
