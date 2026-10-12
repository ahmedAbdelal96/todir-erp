using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Transactions;

public interface ITransactionContext : IAsyncDisposable
{
    NpgsqlConnection Connection { get; }
    NpgsqlTransaction Transaction { get; }
    void Enlist(NpgsqlCommand command);
    void AddOutbox(OutboxMessage message);
}

public interface ITransactionCoordinator
{
    Task<T> ExecuteAsync<T>(TenantId tenantId, Func<ITransactionContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessage(Guid MessageId, string MessageType, string Payload, TenantId TenantId, DateTimeOffset OccurredAt, Guid CorrelationId, Guid CausationId, string SourceModule, string? TraceParent = null);

public sealed class NpgsqlTransactionCoordinator(NpgsqlDataSource dataSource) : ITransactionCoordinator
{
    public async Task<T> ExecuteAsync<T>(TenantId tenantId, Func<ITransactionContext, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var contextCommand = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            contextCommand.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await contextCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var context = new TransactionContext(connection, transaction);
        try
        {
            var result = await action(context, cancellationToken);
            await context.FlushOutboxAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private sealed class TransactionContext(NpgsqlConnection connection, NpgsqlTransaction transaction) : ITransactionContext
    {
        private readonly List<OutboxMessage> messages = [];
        public NpgsqlConnection Connection => connection;
        public NpgsqlTransaction Transaction => transaction;
        public void Enlist(NpgsqlCommand command) => command.Transaction = transaction;
        public void AddOutbox(OutboxMessage message) => messages.Add(message);
        public async Task FlushOutboxAsync(CancellationToken cancellationToken)
        {
            foreach (var message in messages)
            {
                await using var command = new NpgsqlCommand("insert into platform.outbox_message (id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, trace_parent, status) values ($1, $2, $3::jsonb, $4, $5, $6, $7, $8, $9, 'Pending')", connection, transaction);
                command.Parameters.AddWithValue(message.MessageId); command.Parameters.AddWithValue(message.MessageType); command.Parameters.AddWithValue(message.Payload); command.Parameters.AddWithValue(message.TenantId.Value); command.Parameters.AddWithValue(message.OccurredAt); command.Parameters.AddWithValue(message.CorrelationId); command.Parameters.AddWithValue(message.CausationId); command.Parameters.AddWithValue(message.SourceModule); command.Parameters.AddWithValue((object?)message.TraceParent ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
