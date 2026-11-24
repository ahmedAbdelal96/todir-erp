using Npgsql;
using MassTransit;
using Core.Messaging;
using Shared.Primitives.Identifiers;
using Shared.BuildingBlocks.Observability;

namespace Worker;

public sealed class OutboxDispatchWorker(
    IServiceProvider services,
    ILogger<OutboxDispatchWorker> logger,
    IMessageRetryPolicy retryPolicy) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> DispatchFailure = LoggerMessage.Define(LogLevel.Error, new EventId(7001, "OutboxDispatchFailure"), "Outbox dispatch cycle failed; pending rows remain durable for retry.");
    private static readonly Action<ILogger, int, Exception?> PendingSelected = LoggerMessage.Define<int>(LogLevel.Information, new EventId(7002, "OutboxPendingSelected"), "Outbox dispatch selected {MessageCount} pending messages.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            WorkerMetrics.MarkCycleStarted();
            try
            {
                await DrainAsync(stoppingToken);
                WorkerMetrics.MarkCycleSucceeded();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                WorkerMetrics.MarkCycleFailed();
                DispatchFailure(logger, exception);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DrainCoreAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            WorkerMetrics.MarkPostgresUnavailable();
            throw;
        }
    }

    private async Task DrainCoreAsync(CancellationToken cancellationToken)
    {
        var dataSource = services.GetService<NpgsqlDataSource>();
        if (dataSource is null)
        {
            WorkerMetrics.MarkPostgresUnavailable();
            throw new InvalidOperationException("worker.postgres_dependency_unavailable");
        }

        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetService<IPublishEndpoint>();
        var bus = scope.ServiceProvider.GetService<IBusControl>();
        if (publisher is null || bus is null)
        {
            WorkerMetrics.MarkRabbitMqUnavailable();
            throw new InvalidOperationException("worker.rabbitmq_dependency_unavailable");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        WorkerMetrics.MarkPostgresAvailable();
        BusHealthResult busHealth;
        try
        {
            busHealth = bus.CheckHealth();
        }
        catch
        {
            WorkerMetrics.MarkRabbitMqUnavailable();
            throw;
        }
        if (busHealth.Status != BusHealthStatus.Healthy)
        {
            WorkerMetrics.MarkRabbitMqUnavailable();
            throw new InvalidOperationException("worker.rabbitmq_transport_unavailable");
        }
        WorkerMetrics.MarkRabbitMqAvailable();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var backlog = new NpgsqlCommand("select pending_count, oldest_age_seconds from platform.observe_pending_outbox()", connection, transaction);
        await using (var backlogReader = await backlog.ExecuteReaderAsync(cancellationToken))
        {
            if (await backlogReader.ReadAsync(cancellationToken)) CoreMetrics.SetOutboxBacklog(backlogReader.GetInt64(0), backlogReader.GetDouble(1));
        }
        await using var command = new NpgsqlCommand("select id, message_type, payload, tenant_id, occurred_at, correlation_id, causation_id, source_module, trace_parent, attempts from platform.claim_pending_outbox(100)", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var messages = new List<OutboxPublishedMessage>();
        while (await reader.ReadAsync(cancellationToken)) messages.Add(new OutboxPublishedMessage(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), new TenantId(reader.GetGuid(3)), reader.GetFieldValue<DateTimeOffset>(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9)));
        await reader.DisposeAsync();
        if (messages.Count > 0) PendingSelected(logger, messages.Count, null);
        var messageProcessingFailed = false;
        foreach (var message in messages)
        {
            try
            {
                try
                {
                    await publisher.Publish(new OutboxPublishedMessage(message.MessageId, message.MessageType, message.Payload, message.TenantId, message.OccurredAt, message.CorrelationId, message.CausationId, message.SourceModule, message.TraceParent, message.Attempts), context =>
                    {
                        context.MessageId = message.MessageId;
                        context.Headers.Set("message_type", message.MessageType);
                        context.Headers.Set("tenant_id", message.TenantId.Value.ToString("D"));
                        context.Headers.Set("correlation_id", message.CorrelationId.ToString("D"));
                        context.Headers.Set("causation_id", message.CausationId.ToString("D"));
                        if (message.TraceParent is not null) context.Headers.Set("traceparent", message.TraceParent);
                    }, cancellationToken);
                }
                catch (Exception publishException) when (publishException is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    WorkerMetrics.MarkRabbitMqUnavailable();
                    throw;
                }
                await using var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
                tenantContext.Parameters.AddWithValue(message.TenantId.Value.ToString("D"));
                await tenantContext.ExecuteNonQueryAsync(cancellationToken);
                await using var mark = new NpgsqlCommand("update platform.outbox_message set status = 'Published', published_at = now(), last_error = null where id = $1 and status = 'Pending'", connection, transaction);
                mark.Parameters.AddWithValue(message.MessageId);
                await mark.ExecuteNonQueryAsync(cancellationToken);
                CoreMetrics.OutboxPublished.Add(1, new KeyValuePair<string, object?>("source_module", message.SourceModule));
                WorkerMetrics.MarkMessageProcessed();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                messageProcessingFailed = true;
                var decision = retryPolicy.Decide(message.Attempts, DateTimeOffset.UtcNow, exception);
                await using var tenantContext = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
                tenantContext.Parameters.AddWithValue(message.TenantId.Value.ToString("D"));
                await tenantContext.ExecuteNonQueryAsync(cancellationToken);
                await using var failure = new NpgsqlCommand("update platform.outbox_message set status = case when $2 then 'Pending' else 'Failed' end, next_attempt_at = $3, last_error = $4 where id = $1 and status = 'Pending'", connection, transaction);
                failure.Parameters.AddWithValue(message.MessageId);
                failure.Parameters.AddWithValue(decision.Retry);
                failure.Parameters.AddWithValue((object?)decision.NextAttemptAt ?? DBNull.Value);
                failure.Parameters.AddWithValue(exception.Message[..Math.Min(exception.Message.Length, 2000)]);
                await failure.ExecuteNonQueryAsync(cancellationToken);
                CoreMetrics.OutboxFailed.Add(1, new KeyValuePair<string, object?>("source_module", message.SourceModule));
            }
        }
        await transaction.CommitAsync(cancellationToken);
        if (messageProcessingFailed) throw new InvalidOperationException("worker.outbox_message_processing_failed");
    }
}
