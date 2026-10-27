using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Modules.Payments.Services;

namespace Worker;

/// <summary>Runs the provider boundary after the request transaction has committed.</summary>
public sealed class PaymentProviderOperationConsumer(PaymentsService payments, IInboxStore inboxStore)
    : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "payments-provider-operations";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != "PaymentProviderOperationRequestedV1") return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var payload = JsonDocument.Parse(message.Payload);
            if (!payload.RootElement.TryGetProperty("operationId", out var value) || !value.TryGetGuid(out var operationId))
                throw new InvalidOperationException("The provider operation event does not contain a valid operation reference.");
            await payments.StartReferenceProviderOperationAsync(message.TenantId, operationId, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class PaymentProviderOperationConsumerDefinition : ConsumerDefinition<PaymentProviderOperationConsumer>
{
    public PaymentProviderOperationConsumerDefinition() => EndpointName = PaymentProviderOperationConsumer.EndpointName;
}
