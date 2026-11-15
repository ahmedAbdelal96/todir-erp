using System.Text.Json;
using Core.Messaging;
using Core.Services;
using MassTransit;

namespace Worker;

public sealed class ProjectsNotificationConsumer(INotificationStore notifications, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "projects-notification-worker";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "ProjectCloseExceptionCreatedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            if (root.TryGetProperty("managerUserId", out var manager) && manager.ValueKind == JsonValueKind.String && manager.TryGetGuid(out var managerUserId))
                await notifications.EnqueueAsync(new Notification(Guid.CreateVersion7(), message.TenantId, $"user:{managerUserId:D}", "projects.close_exception_created", NotificationChannel.InApp, root.Clone(), NotificationStatus.Pending, 0, DateTimeOffset.UtcNow, null), context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class ProjectsNotificationConsumerDefinition : ConsumerDefinition<ProjectsNotificationConsumer>
{
    public ProjectsNotificationConsumerDefinition() => EndpointName = ProjectsNotificationConsumer.EndpointName;
}
