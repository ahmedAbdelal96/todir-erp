using System.Globalization;
using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Modules.Projects.Repositories;
using Shared.Contracts.Projects;

namespace Worker;

/// <summary>Projects consumes source events through contracts; absent allocation is recorded, never guessed.</summary>
public sealed class ProjectsSourceEventConsumer(
    IProjectSourceIntegrationCapabilities projects,
    ProjectsIntegrationRepository evidence,
    IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "projects-source-integration";
    private static readonly HashSet<string> EventTypes = new(StringComparer.Ordinal)
    {
        "ExpenseFinalizedV1", "ExpenseReversedV1", "PurchaseOrderApprovedV1", "PurchaseOrderAmendedV1",
        "PurchaseOrderCancelledV1", "PurchaseOrderClosedV1", "StockIssuedV1", "StockReversedV1",
        "InvoiceFinalizedV1", "InvoiceCorrectedV1", "PaymentCompletedV1", "PaymentReversedV1",
        "FixedAssetCapitalizationAcceptedV1", "FixedAssetCapitalizationRejectedV1"
    };

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!EventTypes.Contains(message.MessageType)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            var root = document.RootElement;
            var projectId = GuidValue(root, "projectId");
            if (projectId is null)
            {
                await evidence.RecordAsync(message.TenantId, message.MessageId, message.SourceModule, message.MessageType, null,
                    "Unallocated", "source_event_missing_project_allocation", message.Payload, message.OccurredAt, context.CancellationToken);
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }
            var legalEntityId = GuidValue(root, "legalEntityId");
            if (!await evidence.ProjectExistsAsync(message.TenantId, projectId.Value, legalEntityId, context.CancellationToken))
            {
                await evidence.RecordAsync(message.TenantId, message.MessageId, message.SourceModule, message.MessageType, null,
                    "Unallocated", "project_not_found_in_tenant", message.Payload, message.OccurredAt, context.CancellationToken);
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }

            var sourceId = (message.MessageType == "StockReversedV1" ? StringValue(root, "reversalOfOperationId") : null)
                ?? StringValue(root, "expenseId") ?? StringValue(root, "purchaseOrderId") ?? StringValue(root, "operationId")
                ?? StringValue(root, "invoiceId") ?? StringValue(root, "paymentId") ?? message.CausationId.ToString("D");
            var currency = (StringValue(root, "currencyCode") ?? "EGP").ToUpperInvariant();
            var evidenceStatus = "Applied";
            string? evidenceReason = null;
            if (message.MessageType == "PurchaseOrderCancelledV1")
            {
                await projects.CancelCommitmentAsync(message.TenantId, projectId.Value, "purchasing", sourceId, context.CancellationToken);
            }
            else if (message.MessageType.StartsWith("PurchaseOrder", StringComparison.Ordinal))
            {
                var committed = DecimalValue(root, "committedAmount") ?? DecimalValue(root, "netTotal") ?? 0m;
                var actualized = DecimalValue(root, "actualizedAmount") ?? 0m;
                await projects.RecordCommitmentAsync(new ProjectCommitmentIngress(message.TenantId, projectId.Value, "purchasing", sourceId,
                    DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), currency, committed, actualized,
                    $"projects:purchasing:{sourceId}:{message.MessageType}", legalEntityId), context.CancellationToken);
            }
            else if (message.MessageType is "ExpenseFinalizedV1" or "ExpenseReversedV1" or "StockIssuedV1" or "StockReversedV1")
            {
                var amount = DecimalValue(root, "baseAmount") ?? DecimalValue(root, "costAmount") ?? DecimalValue(root, "amount") ?? 0m;
                // Cost facts are immutable positive facts. Reversals must arrive with a correction reference and are not guessed here.
                if (message.MessageType is "ExpenseReversedV1" or "StockReversedV1")
                {
                    var correctionOf = GuidValue(root, "correctionOfFactId");
                    if (correctionOf is null)
                    {
                        var reversal = await projects.RecordReversalAsync(message.TenantId, message.CausationId, projectId.Value, message.SourceModule, sourceId, Math.Abs(amount), currency, DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), $"projects:{message.MessageType}:{sourceId}", message.CorrelationId, context.CancellationToken);
                        if (reversal is null)
                        {
                            evidenceStatus = "Ignored";
                            evidenceReason = "reversal_source_fact_not_found";
                        }
                    }
                    else
                    {
                        await projects.RecordCostAsync(new ProjectCostFactIngress(message.TenantId, message.CausationId, projectId.Value, null,
                            message.SourceModule, message.MessageType, sourceId, null,
                            message.SourceModule == "inventory" ? "Material" : "Expense", currency, Math.Abs(amount), currency, Math.Abs(amount), null,
                            DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), $"projects:{message.MessageType}:{sourceId}", correctionOf, legalEntityId), message.CorrelationId, context.CancellationToken);
                    }
                }
                else
                {
                    var signedAmount = Math.Abs(amount);
                    if (signedAmount == 0m && message.MessageType == "StockIssuedV1")
                    {
                        evidenceStatus = "Ignored";
                        evidenceReason = "inventory_valuation_missing";
                    }
                    else await projects.RecordCostAsync(new ProjectCostFactIngress(message.TenantId, message.CausationId, projectId.Value, null,
                        message.SourceModule, message.MessageType, sourceId, null,
                        message.SourceModule == "inventory" ? "Material" : "Expense", currency, signedAmount, currency, signedAmount, null,
                        DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), $"projects:{message.MessageType}:{sourceId}", null, legalEntityId), message.CorrelationId, context.CancellationToken);
                }
            }
            else if (message.MessageType is "FixedAssetCapitalizationAcceptedV1" or "FixedAssetCapitalizationRejectedV1")
            {
                var handoffId = GuidValue(root, "handoffId");
                if (handoffId is null)
                {
                    evidenceStatus = "Ignored";
                    evidenceReason = "capital_handoff_id_missing";
                }
                else
                {
                    await projects.ApplyCapitalHandoffResultAsync(message.TenantId, handoffId.Value,
                        message.MessageType == "FixedAssetCapitalizationAcceptedV1" ? "Accepted" : "Rejected",
                        StringValue(root, "candidateId"), context.CancellationToken);
                }
            }
            else
            {
                evidenceStatus = "Ignored";
                evidenceReason = "source_event_requires_project_allocation_contract";
            }
            await evidence.RecordAsync(message.TenantId, message.MessageId, message.SourceModule, message.MessageType, projectId,
                evidenceStatus, evidenceReason, message.Payload, message.OccurredAt, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
            throw;
        }
    }

    private static Guid? GuidValue(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var result) ? result : null;
    private static string? StringValue(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static decimal? DecimalValue(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}

public sealed class ProjectsSourceEventConsumerDefinition : ConsumerDefinition<ProjectsSourceEventConsumer>
{
    public ProjectsSourceEventConsumerDefinition() { EndpointName = ProjectsSourceEventConsumer.EndpointName; ConcurrentMessageLimit = 4; }
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ProjectsSourceEventConsumer> consumerConfigurator, IRegistrationContext context)
        => endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)));
}
