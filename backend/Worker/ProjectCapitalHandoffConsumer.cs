using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Modules.FixedAssets;
using Modules.FixedAssets.Services;

namespace Worker;

/// <summary>Turns an approved project capital handoff into a Fixed Assets capitalization candidate.</summary>
public sealed class ProjectCapitalHandoffConsumer(FixedAssetService fixedAssets, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "projects-capital-handoff";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "ProjectCapitalHandoffRequestedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            if (!TryGuid(root, "legalEntityId", out var legalEntityId) || !TryGuid(root, "handoffId", out var handoffId) ||
                !TryGuid(root, "costFactId", out var costFactId) || !TryDecimal(root, "amount", out var amount) || amount <= 0m)
            {
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }

            var snapshot = JsonSerializer.SerializeToElement(new
            {
                projectId = TryGuid(root, "projectId", out var projectId) ? projectId : (Guid?)null,
                handoffId,
                costFactId,
                amount,
                currencyCode = root.TryGetProperty("currencyCode", out var currency) ? currency.GetString() : null
            });
            await fixedAssets.CreateCapitalizationCandidateAsync(message.TenantId, message.CausationId,
                new CreateCapitalizationCandidateRequest(legalEntityId, "ProjectCostFact", costFactId.ToString("D"), amount, snapshot, $"project-handoff:{handoffId:D}"), context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
            throw;
        }
    }

    private static bool TryGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        return root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out value);
    }
    private static bool TryDecimal(JsonElement root, string name, out decimal value)
    {
        value = 0m;
        return root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value);
    }
}

public sealed class ProjectCapitalHandoffConsumerDefinition : ConsumerDefinition<ProjectCapitalHandoffConsumer>
{
    public ProjectCapitalHandoffConsumerDefinition() { EndpointName = ProjectCapitalHandoffConsumer.EndpointName; ConcurrentMessageLimit = 2; }
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ProjectCapitalHandoffConsumer> consumerConfigurator, IRegistrationContext context)
        => endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)));
}
