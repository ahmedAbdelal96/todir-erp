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
            if (projectId is null && StringValue(root, "sourceModule") == "projects") projectId = GuidValue(root, "sourceDocumentId");
            if (projectId is null && message.MessageType == "InvoiceFinalizedV1" && StringValue(root, "sourceModule") == "purchasing")
                projectId = SupplierInvoiceProjectId(StringValue(root, "sourcePurpose"));
            if (projectId is null && string.Equals(StringValue(root, "sourceType"), "Projects", StringComparison.OrdinalIgnoreCase))
                projectId = GuidValue(root, "sourceReference");
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

            var sourceId = SourceId(root, message.MessageType);
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                await evidence.RecordAsync(message.TenantId, message.MessageId, message.SourceModule, message.MessageType, projectId,
                    "Ignored", "source_event_missing_source_id", message.Payload, message.OccurredAt, context.CancellationToken);
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }
            if (await evidence.IsStaleAsync(message.TenantId, message.SourceModule, sourceId, message.OccurredAt, context.CancellationToken))
            {
                await evidence.RecordAsync(message.TenantId, message.MessageId, message.SourceModule, message.MessageType, projectId,
                    "Ignored", "source_event_out_of_order", message.Payload, message.OccurredAt, context.CancellationToken);
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }
            var currency = StringValue(root, "currencyCode")?.Trim().ToUpperInvariant();
            var evidenceStatus = "Applied";
            string? evidenceReason = null;
            if (message.MessageType == "PurchaseOrderCancelledV1")
            {
                await projects.CancelCommitmentAsync(message.TenantId, projectId.Value, "purchasing", sourceId, context.CancellationToken);
            }
            else if (message.MessageType.StartsWith("PurchaseOrder", StringComparison.Ordinal))
            {
                var committed = DecimalValue(root, "committedAmount") ?? DecimalValue(root, "netTotal");
                var actualized = DecimalValue(root, "actualizedAmount");
                if (currency is null || committed is null || actualized is null)
                {
                    evidenceStatus = "Ignored"; evidenceReason = currency is null ? "source_event_missing_currency" : "source_event_missing_amount";
                }
                else
                {
                    await projects.RecordCommitmentAsync(new ProjectCommitmentIngress(message.TenantId, projectId.Value, "purchasing", sourceId,
                        DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), currency, committed.Value, actualized.Value,
                        $"projects:purchasing:{sourceId}:{message.MessageType}", legalEntityId), context.CancellationToken);
                }
            }
            else if (message.MessageType is "ExpenseFinalizedV1" or "ExpenseReversedV1" or "StockIssuedV1" or "StockReversedV1")
            {
                var amount = DecimalValue(root, "baseAmount") ?? DecimalValue(root, "costAmount") ?? DecimalValue(root, "amount");
                if (currency is null || amount is null)
                {
                    evidenceStatus = "Ignored"; evidenceReason = currency is null ? "source_event_missing_currency" : "source_event_missing_amount";
                }
                else
                {
                    // Cost facts are immutable positive facts. Reversals must arrive with a correction reference and are not guessed here.
                    if (message.MessageType is "ExpenseReversedV1" or "StockReversedV1")
                    {
                        var correctionOf = GuidValue(root, "correctionOfFactId");
                        if (correctionOf is null)
                        {
                            var reversal = await projects.RecordReversalAsync(message.TenantId, message.CausationId, projectId.Value, message.SourceModule, sourceId, Math.Abs(amount.Value), currency, DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), $"projects:{message.MessageType}:{sourceId}", message.CorrelationId, context.CancellationToken);
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
                                message.SourceModule == "inventory" ? "Material" : "Expense", currency, Math.Abs(amount.Value), currency, Math.Abs(amount.Value), null,
                                DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), $"projects:{message.MessageType}:{sourceId}", correctionOf, legalEntityId), message.CorrelationId, context.CancellationToken);
                        }
                    }
                    else
                    {
                        var signedAmount = Math.Abs(amount.Value);
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
            }
            else if (message.MessageType == "InvoiceFinalizedV1")
            {
                var requestId = ProjectInvoiceRequestId(StringValue(root, "sourcePurpose"));
                var amount = DecimalValue(root, "amount");
                if (currency is null || amount is null) { evidenceStatus = "Ignored"; evidenceReason = currency is null ? "source_event_missing_currency" : "source_event_missing_amount"; }
                else if (!Guid.TryParse(sourceId, out var invoiceId)) { evidenceStatus = "Ignored"; evidenceReason = "invoice_id_invalid"; }
                else if (string.Equals(StringValue(root, "sourceModule"), "purchasing", StringComparison.Ordinal))
                {
                    var purchaseOrderId = GuidValue(root, "sourceDocumentId");
                    if (purchaseOrderId is null) { evidenceStatus = "Ignored"; evidenceReason = "supplier_invoice_purchase_order_missing"; }
                    else await projects.ApplySupplierInvoiceActualAsync(message.TenantId, message.CausationId, projectId.Value, purchaseOrderId.Value.ToString("D"), invoiceId.ToString("D"), amount.Value, currency,
                        DateOnly.FromDateTime(message.OccurredAt.UtcDateTime), legalEntityId, message.CorrelationId, context.CancellationToken);
                }
                else if (requestId is null) { evidenceStatus = "Ignored"; evidenceReason = "invoice_request_reference_missing"; }
                else await projects.CompleteInvoiceAsync(message.TenantId, projectId.Value, requestId.Value, invoiceId, amount.Value, currency, context.CancellationToken);
            }
            else if (message.MessageType == "InvoiceCorrectedV1")
            {
                var originalInvoiceId = GuidValue(root, "originalInvoiceId");
                var correctionInvoiceId = GuidValue(root, "invoiceId");
                var amount = DecimalValue(root, "amount");
                if (currency is null || amount is null) { evidenceStatus = "Ignored"; evidenceReason = currency is null ? "source_event_missing_currency" : "source_event_missing_amount"; }
                else if (originalInvoiceId is null || correctionInvoiceId is null) { evidenceStatus = "Ignored"; evidenceReason = "invoice_correction_reference_missing"; }
                else if (!await projects.ApplyInvoiceCorrectionAsync(message.TenantId, projectId.Value, correctionInvoiceId.Value, originalInvoiceId.Value, amount.Value, currency, context.CancellationToken)) { evidenceStatus = "Ignored"; evidenceReason = "invoice_correction_allocation_not_found"; }
            }
            else if (message.MessageType is "PaymentCompletedV1" or "PaymentReversedV1")
            {
                if (currency is null || DecimalValue(root, "amount") is null) { evidenceStatus = "Ignored"; evidenceReason = currency is null ? "source_event_missing_currency" : "source_event_missing_amount"; }
                else evidenceReason = "settlement_projection_authoritative_in_subledger";
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
    private static string? SourceId(JsonElement root, string messageType) => messageType switch
    {
        "ExpenseFinalizedV1" or "ExpenseReversedV1" => StringValue(root, "expenseId"),
        "PurchaseOrderApprovedV1" or "PurchaseOrderAmendedV1" or "PurchaseOrderCancelledV1" or "PurchaseOrderClosedV1" => StringValue(root, "purchaseOrderId"),
        "StockIssuedV1" => StringValue(root, "operationId"),
        "StockReversedV1" => StringValue(root, "reversalOfOperationId") ?? StringValue(root, "operationId"),
        "InvoiceFinalizedV1" or "InvoiceCorrectedV1" => StringValue(root, "invoiceId"),
        "PaymentCompletedV1" or "PaymentReversedV1" => StringValue(root, "paymentId"),
        "FixedAssetCapitalizationAcceptedV1" or "FixedAssetCapitalizationRejectedV1" => StringValue(root, "candidateId"),
        _ => null
    };
    private static Guid? ProjectInvoiceRequestId(string? purpose)
        => purpose is not null && purpose.StartsWith("ProjectBilling:", StringComparison.Ordinal) && Guid.TryParse(purpose["ProjectBilling:".Length..], out var id) ? id : null;
    private static Guid? SupplierInvoiceProjectId(string? purpose)
        => purpose is not null && purpose.StartsWith("SupplierInvoice:Project:", StringComparison.Ordinal) && Guid.TryParse(purpose["SupplierInvoice:Project:".Length..], out var id) ? id : null;
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
