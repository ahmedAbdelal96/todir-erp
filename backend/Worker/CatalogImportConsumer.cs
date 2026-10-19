using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Modules.Catalog.Services;

namespace Worker;

public sealed class CatalogImportConsumer(CatalogImportService imports, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "catalog-import-worker";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "CatalogImportRequestedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var payload = JsonDocument.Parse(message.Payload);
            if (!payload.RootElement.TryGetProperty("batchId", out var batchIdElement) || !batchIdElement.TryGetGuid(out var batchId))
                throw new InvalidDataException("catalog.import_event_payload_invalid");
            await imports.CommitAsync(message.TenantId, batchId, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class CatalogImportConsumerDefinition : ConsumerDefinition<CatalogImportConsumer>
{
    public CatalogImportConsumerDefinition() => EndpointName = CatalogImportConsumer.EndpointName;
}
