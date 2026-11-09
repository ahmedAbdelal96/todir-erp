using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Modules.Expenses.Repositories;

namespace Worker;

public sealed class ExpensesOcrConsumer(ExpenseRepository expenses, IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "expenses-ocr-worker";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "expenses.ocr.requested", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var payload = JsonDocument.Parse(message.Payload);
            var root = payload.RootElement;
            if (!root.TryGetProperty("JobId", out var job) || !job.TryGetGuid(out var jobId) ||
                !root.TryGetProperty("LegalEntityId", out var entity) || !entity.TryGetGuid(out var entityId) ||
                !root.TryGetProperty("ExpenseId", out var expense) || !expense.TryGetGuid(out var expenseId) ||
                !root.TryGetProperty("EvidenceId", out var evidence) || !evidence.TryGetGuid(out var evidenceId))
                throw new InvalidDataException("expenses.ocr_event_payload_invalid");
            // Provider failures are persisted as Failed and rethrown so MassTransit can perform
            // bounded broker retries. Manual/API callers retain explicit retry semantics.
            await expenses.ProcessOcrJobAsync(message.TenantId, entityId, expenseId, jobId, evidenceId, context.CancellationToken, rethrowProviderFailure: true);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class ExpensesOcrConsumerDefinition : ConsumerDefinition<ExpensesOcrConsumer>
{
    public ExpensesOcrConsumerDefinition() => EndpointName = ExpensesOcrConsumer.EndpointName;

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ExpensesOcrConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(250)));
    }
}
