using System.Text.Json;
using Core.Messaging;
using Core.Services;
using MassTransit;

namespace Worker;

public sealed class ExpensesNotificationConsumer(INotificationStore notifications, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "expenses-notification-worker";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "ExpenseFinalizedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            if (root.TryGetProperty("EmployeeId", out var employee) && employee.ValueKind == JsonValueKind.String && employee.TryGetGuid(out var employeeId))
            {
                await notifications.EnqueueAsync(new Notification(Guid.CreateVersion7(), message.TenantId, $"employee:{employeeId:D}", "expenses.expense_finalized", NotificationChannel.InApp, root.Clone(), NotificationStatus.Pending, 0, DateTimeOffset.UtcNow, null), context.CancellationToken);
            }

            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class ExpensesNotificationConsumerDefinition : ConsumerDefinition<ExpensesNotificationConsumer>
{
    public ExpensesNotificationConsumerDefinition() => EndpointName = ExpensesNotificationConsumer.EndpointName;
}
