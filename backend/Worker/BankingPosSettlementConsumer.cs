using Core.Messaging;
using MassTransit;
using Modules.Banking.Repositories;
using System.Text.Json;
using Shared.Contracts.POS;

namespace Worker;

/// <summary>Receives POS close evidence without taking ownership of POS session truth.</summary>
public sealed class BankingPosSettlementConsumer(BankingRepository banking, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "banking-pos-settlement";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != "PosSessionClosedV1") return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            var eventData = JsonSerializer.Deserialize<PosSessionClosedV1>(message.Payload)
                ?? throw new InvalidOperationException("The POS session close event payload is empty.");
            await banking.ProjectPosSessionClosedAsync(message.TenantId, eventData, message.MessageId, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class BankingPosSettlementConsumerDefinition : ConsumerDefinition<BankingPosSettlementConsumer>
{
    public BankingPosSettlementConsumerDefinition() => EndpointName = BankingPosSettlementConsumer.EndpointName;
}
