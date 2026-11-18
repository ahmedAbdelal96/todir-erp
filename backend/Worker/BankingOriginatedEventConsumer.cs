using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using MassTransit;
using Shared.Contracts.Banking;
using Shared.Contracts.Ledger;
using Shared.Contracts.Payments;

namespace Worker;

/// <summary>Posts bank-originated events through the owning Payments or Ledger capability.</summary>
public sealed class BankingOriginatedEventConsumer(
    ITransactionCoordinator transactions,
    ITransactionalLedgerPostingCapability ledger,
    IBankOriginatedPaymentCapability payments,
    IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "banking-originated-financial-events";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != "BankOriginatedFinancialEventApprovedV1") return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            var approved = JsonSerializer.Deserialize<BankOriginatedFinancialEventApprovedV1>(message.Payload, JsonOptions)
                ?? throw new InvalidOperationException("The bank-originated approval payload is empty.");
            if (approved.EventType == "UnknownReceipt")
            {
                if (approved.MembershipId is not { } membershipId || membershipId == Guid.Empty)
                    throw new InvalidOperationException("Unknown bank receipts require the approving membership for Payments audit ownership.");
                await payments.CreateUnallocatedReceiptAsync(message.TenantId, approved.ActorId, membershipId, message.CorrelationId,
                    new BankOriginatedReceiptRequest(approved.LegalEntityId, approved.TreasuryAccountId, approved.Amount, approved.CurrencyCode,
                        approved.BookingDate, approved.BookingDate, null, $"bank-statement:{approved.StatementLineId:D}",
                        $"bank-originated:{approved.CandidateId:D}"), context.CancellationToken);
                await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
                return;
            }
            var payload = new BankOriginatedFinancialEventPayload(approved.EventType, approved.Direction, approved.Amount, approved.CurrencyCode,
                approved.TreasuryAccountId.ToString("D"), approved.Amount, approved.CurrencyCode, null);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(approved.CandidateId, message.TenantId,
                new Shared.Primitives.Identifiers.LegalEntityId(approved.LegalEntityId), AccountingFactTypes.BankOriginatedFinancialEventApproved,
                1, "banking", $"bank-originated:{approved.CandidateId:D}", $"bank-originated:{approved.CandidateId:D}",
                approved.BookingDate, approved.BookingDate, payload, message.CorrelationId, approved.ActorId);
            await transactions.ExecuteAsync(message.TenantId, async (tx, token) =>
            {
                await ledger.PostBankOriginatedAsync(tx, fact, payload, token);
                return true;
            }, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class BankingOriginatedEventConsumerDefinition : ConsumerDefinition<BankingOriginatedEventConsumer>
{
    public BankingOriginatedEventConsumerDefinition() => EndpointName = BankingOriginatedEventConsumer.EndpointName;
}
