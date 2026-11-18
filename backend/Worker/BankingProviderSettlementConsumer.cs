using Core.Messaging;
using MassTransit;
using Modules.Banking.Repositories;
using Shared.Contracts.Banking;
using System.Text.Json;

namespace Worker;

/// <summary>Projects provider settlement batches separately from customer payment movements.</summary>
public sealed class BankingProviderSettlementConsumer(BankingRepository banking, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "banking-provider-settlements";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != "ProviderSettlementImportedV1") return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            var settlement = JsonSerializer.Deserialize<ProviderSettlementImportedV1>(message.Payload)
                ?? throw new InvalidOperationException("The provider settlement event payload is empty.");
            await banking.RecordProviderSettlementAsync(message.TenantId, settlement, message.MessageId, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class BankingProviderSettlementConsumerDefinition : ConsumerDefinition<BankingProviderSettlementConsumer>
{
    public BankingProviderSettlementConsumerDefinition() => EndpointName = BankingProviderSettlementConsumer.EndpointName;
}
