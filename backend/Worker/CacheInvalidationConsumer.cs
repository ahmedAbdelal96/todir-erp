using System.Text.Json;
using Core.Caching;
using Core.Messaging;
using MassTransit;

namespace Worker;

public sealed class CacheInvalidationConsumer(IPlatformCache cache, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "platform-cache-invalidation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "PlatformCacheInvalidatedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;

        try
        {
            var payload = JsonSerializer.Deserialize<CacheInvalidationPayload>(message.Payload, JsonOptions)
                ?? throw new InvalidDataException("platform.cache_invalidation_payload_invalid");
            if (string.IsNullOrWhiteSpace(payload.Dependency) || payload.Revision < 1) throw new InvalidDataException("platform.cache_invalidation_payload_invalid");
            await cache.InvalidateAsync(new(message.TenantId, payload.Dependency, payload.LegalEntityId, payload.BranchId, payload.Revision), context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private sealed record CacheInvalidationPayload(string Dependency, long Revision, Guid? LegalEntityId, Guid? BranchId);
}

public sealed class CacheInvalidationConsumerDefinition : ConsumerDefinition<CacheInvalidationConsumer>
{
    public CacheInvalidationConsumerDefinition() => EndpointName = CacheInvalidationConsumer.EndpointName;

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<CacheInvalidationConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)));
    }
}
