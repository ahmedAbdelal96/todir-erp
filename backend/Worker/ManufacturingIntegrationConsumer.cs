using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Npgsql;

namespace Worker;

/// <summary>Projects manufacturing events through the real Outbox/RabbitMQ/Inbox path and keeps replay idempotent.</summary>
public sealed class ManufacturingIntegrationConsumer(NpgsqlDataSource dataSource, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "manufacturing-integration";
    private static readonly HashSet<string> Supported = ["ProductionOrderCreatedV1", "ProductionOrderReleasedV1", "ProductionOrderStartedV1", "MaterialConsumedForProductionV1", "ProductionOutputReportedV1", "ProductionScrapReportedV1", "ProductionOrderClosedV1"];

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!Supported.Contains(message.MessageType)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            var orderId = ReadGuid(root, "productionOrderId") ?? message.CausationId;
            await using var connection = await dataSource.OpenConnectionAsync(context.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(context.CancellationToken);
            await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                tenant.Parameters.AddWithValue(message.TenantId.Value.ToString("D"));
                await tenant.ExecuteNonQueryAsync(context.CancellationToken);
            }
            var firstDelivery = false;
            await using (var idempotency = new NpgsqlCommand("insert into business_manufacturing.manufacturing_idempotency(tenant_id,id,operation,idempotency_key,request_hash,response_json) values($1,$2,$3,$4,$5,$6::jsonb) on conflict(tenant_id,operation,idempotency_key) do nothing", connection, transaction))
            {
                idempotency.Parameters.AddWithValue(message.TenantId.Value);
                idempotency.Parameters.AddWithValue(Guid.CreateVersion7());
                idempotency.Parameters.AddWithValue("worker-event");
                idempotency.Parameters.AddWithValue(message.MessageId.ToString("D"));
                idempotency.Parameters.AddWithValue(message.MessageId.ToString("D"));
                idempotency.Parameters.AddWithValue(message.Payload);
                firstDelivery = await idempotency.ExecuteNonQueryAsync(context.CancellationToken) == 1;
            }
            if (firstDelivery && message.MessageType == "ProductionOutputReportedV1" && ReadGuid(root, "outputId") is { } outputId)
            {
                await using var output = new NpgsqlCommand("update business_manufacturing.production_output set inventory_receipt_status=case when inventory_operation_id is not null then 'Committed' else inventory_receipt_status end where tenant_id=$1 and id=$2", connection, transaction);
                output.Parameters.AddWithValue(message.TenantId.Value); output.Parameters.AddWithValue(outputId); await output.ExecuteNonQueryAsync(context.CancellationToken);
            }
            if (firstDelivery) await using (var audit = new NpgsqlCommand("insert into business_manufacturing.manufacturing_audit_event(tenant_id,id,entity_type,entity_id,action,actor_id,after_json) values($1,$2,$3,$4,$5,null,$6::jsonb)", connection, transaction))
            {
                audit.Parameters.AddWithValue(message.TenantId.Value); audit.Parameters.AddWithValue(Guid.CreateVersion7()); audit.Parameters.AddWithValue("IntegrationEvent"); audit.Parameters.AddWithValue(orderId); audit.Parameters.AddWithValue($"worker.received.{message.MessageType}"); audit.Parameters.AddWithValue(message.Payload); await audit.ExecuteNonQueryAsync(context.CancellationToken);
            }
            await transaction.CommitAsync(context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private static Guid? ReadGuid(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetGuid(out var id) ? id : null;
}

public sealed class ManufacturingIntegrationConsumerDefinition : ConsumerDefinition<ManufacturingIntegrationConsumer>
{
    public ManufacturingIntegrationConsumerDefinition() { EndpointName = ManufacturingIntegrationConsumer.EndpointName; ConcurrentMessageLimit = 4; }
}
