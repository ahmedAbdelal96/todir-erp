using Core.Messaging;
using MassTransit;
using Modules.Banking.Services;
using Shared.Contracts.Banking;

namespace Worker;

/// <summary>Projects connector feed evidence into Banking with inbox and database idempotency.</summary>
public sealed class BankingExternalFeedConsumer(BankingService banking, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "banking-external-feed";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != "ExternalFeedTransactionReceivedV1") return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            var request = System.Text.Json.JsonSerializer.Deserialize<ExternalFeedTransactionReceivedV1>(message.Payload)
                ?? throw new InvalidOperationException("The external feed event payload is empty.");
            await banking.RecordExternalFeedTransactionAsync(message.TenantId, request, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class BankingExternalFeedConsumerDefinition : ConsumerDefinition<BankingExternalFeedConsumer>
{
    public BankingExternalFeedConsumerDefinition() => EndpointName = BankingExternalFeedConsumer.EndpointName;
}
