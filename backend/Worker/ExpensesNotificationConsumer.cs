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
        if (message.MessageType is not ("ExpenseFinalizedV1" or "ExpenseReimbursementRequestedV1" or "ExpenseMissingReceiptDeclaredV1")) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            if (root.TryGetProperty("EmployeeId", out var employee) && employee.ValueKind == JsonValueKind.String && employee.TryGetGuid(out var employeeId))
            {
                var template = message.MessageType switch
                {
                    "ExpenseFinalizedV1" => "expenses.expense_finalized",
                    "ExpenseReimbursementRequestedV1" => "expenses.reimbursement_requested",
                    _ => "expenses.missing_receipt_declared"
                };
                await notifications.EnqueueAsync(new Notification(Guid.CreateVersion7(), message.TenantId, $"employee:{employeeId:D}", template, NotificationChannel.InApp, root.Clone(), NotificationStatus.Pending, 0, DateTimeOffset.UtcNow, null), context.CancellationToken);
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
