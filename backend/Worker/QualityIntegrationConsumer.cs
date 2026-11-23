using Core.Messaging;
using MassTransit;
using Modules.Quality.Repositories;

namespace Worker;

public sealed class QualityIntegrationConsumer(QualityRepository quality, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "quality-integration";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType is not ("StockReceivedV1" or "ProductionOutputReportedV1" or "ProductionOperationCompletedV1" or "SalesReturnReceivedV1" or "CalibrationStatusChangedV1")) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            await quality.RecordIntegrationEventAsync(message, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class QualityIntegrationConsumerDefinition : ConsumerDefinition<QualityIntegrationConsumer>
{
    public QualityIntegrationConsumerDefinition() => EndpointName = QualityIntegrationConsumer.EndpointName;
}
