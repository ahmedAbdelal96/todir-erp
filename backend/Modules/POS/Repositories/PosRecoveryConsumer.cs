using System.Text.Json;
using Core.Messaging;
using MassTransit;

namespace Modules.POS;

public sealed class PosRecoveryConsumer(PosService service) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "pos-recovery";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        if (string.Equals(context.Message.MessageType, "PaymentCompletedV1", StringComparison.Ordinal))
        {
            using var paymentPayload = JsonDocument.Parse(context.Message.Payload);
            if (paymentPayload.RootElement.TryGetProperty("sourceType", out var sourceType)
                && string.Equals(sourceType.GetString(), "PaymentRequest", StringComparison.Ordinal)
                && paymentPayload.RootElement.TryGetProperty("sourceReference", out var sourceReference)
                && Guid.TryParse(sourceReference.GetString(), out var paymentRequestId))
                await service.RecoverProviderCheckoutAsync(context.Message.TenantId, paymentRequestId, context.CancellationToken);
            return;
        }
        if (!string.Equals(context.Message.MessageType, "PosCheckoutRecoveryRequiredV1", StringComparison.Ordinal)) return;
        using var payload = JsonDocument.Parse(context.Message.Payload);
        if (!payload.RootElement.TryGetProperty("checkoutId", out var checkout) || !Guid.TryParse(checkout.GetString(), out var checkoutId)) throw new InvalidDataException("pos.recovery_payload_invalid");
        await service.RecoverCheckoutAsync(context.Message.TenantId, checkoutId, context.CancellationToken);
    }
}

public sealed class PosRecoveryConsumerDefinition : ConsumerDefinition<PosRecoveryConsumer>
{
    public PosRecoveryConsumerDefinition() { EndpointName = PosRecoveryConsumer.EndpointName; ConcurrentMessageLimit = 8; }
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<PosRecoveryConsumer> consumerConfigurator, IRegistrationContext context) => endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)));
}
