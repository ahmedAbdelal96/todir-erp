using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Npgsql;

namespace Worker;

/// <summary>Consumes owning-module lifecycle events into CRM's immutable commercial timeline.</summary>
public sealed class CrmExternalEventConsumer(NpgsqlDataSource dataSource, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "crm-external-event-projection";
    private static readonly HashSet<string> Supported = ["QuotationAcceptedV1", "QuotationDeclinedV1", "InvoicePostedV1", "PaymentCompletedV1"];

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!Supported.Contains(message.MessageType)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            var legalEntityId = ReadGuid(root, "legalEntityId");
            var entityId = ReadGuid(root, "quotationId") ?? ReadGuid(root, "quoteId") ?? ReadGuid(root, "invoiceId") ?? ReadGuid(root, "paymentId");
            await using var connection = await dataSource.OpenConnectionAsync(context.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(context.CancellationToken);
            await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            { tenant.Parameters.AddWithValue(message.TenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(context.CancellationToken); }
            await using (var insert = new NpgsqlCommand("insert into business_crm.crm_external_event(tenant_id,id,message_id,legal_entity_id,message_type,entity_id,payload,occurred_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8) on conflict(tenant_id,message_id) do nothing", connection, transaction))
            { insert.Parameters.AddWithValue(message.TenantId.Value); insert.Parameters.AddWithValue(Guid.CreateVersion7()); insert.Parameters.AddWithValue(message.MessageId); insert.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); insert.Parameters.AddWithValue(message.MessageType); insert.Parameters.AddWithValue((object?)entityId ?? DBNull.Value); insert.Parameters.AddWithValue(message.Payload); insert.Parameters.AddWithValue(message.OccurredAt); await insert.ExecuteNonQueryAsync(context.CancellationToken); }
            if (legalEntityId is not null && entityId is not null)
            {
                var state = message.MessageType switch { "QuotationAcceptedV1" => "Accepted", "QuotationDeclinedV1" => "Declined", "InvoicePostedV1" => "InvoicePosted", "PaymentCompletedV1" => "PaymentCompleted", _ => message.MessageType };
                await using var update = new NpgsqlCommand("update business_crm.opportunity_source_link set external_state=$5,last_external_event_at=$6 where tenant_id=$1 and legal_entity_id=$2 and target_id=$3 and target_module=$4", connection, transaction);
                update.Parameters.AddWithValue(message.TenantId.Value); update.Parameters.AddWithValue(legalEntityId.Value); update.Parameters.AddWithValue(entityId.Value); update.Parameters.AddWithValue(message.MessageType.StartsWith("Quotation", StringComparison.Ordinal) ? "sales" : message.MessageType.StartsWith("Invoice", StringComparison.Ordinal) ? "invoicing" : "payments"); update.Parameters.AddWithValue(state); update.Parameters.AddWithValue(message.OccurredAt); await update.ExecuteNonQueryAsync(context.CancellationToken);
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

public sealed class CrmExternalEventConsumerDefinition : ConsumerDefinition<CrmExternalEventConsumer>
{
    public CrmExternalEventConsumerDefinition() => EndpointName = CrmExternalEventConsumer.EndpointName;
}
