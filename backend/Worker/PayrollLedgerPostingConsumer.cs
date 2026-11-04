using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using MassTransit;
using Npgsql;
using Shared.Contracts.Ledger;
using Shared.Primitives.Identifiers;

namespace Worker;

/// <summary>Posts finalized Payroll facts through the Ledger semantic capability and records the outcome atomically.</summary>
public sealed class PayrollLedgerPostingConsumer(
    ITransactionCoordinator transactions,
    ITransactionalLedgerPostingCapability ledgerPosting,
    IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "payroll-ledger-posting";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "PayrollFinalizedV1", StringComparison.Ordinal))
            return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken))
            return;
        try
        {
            await PostAsync(message, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private async Task PostAsync(OutboxPublishedMessage message, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;
        var runId = root.GetProperty("runId").GetGuid();
        var entityId = root.GetProperty("legalEntityId").GetGuid();
        var factId = root.GetProperty("accountingFactId").GetGuid();
        var source = $"payroll:{runId:D}";

        await transactions.ExecuteAsync(message.TenantId, async (transaction, token) =>
        {
            await using var read = new NpgsqlCommand(
                "select status,total_amount,currency_code,(payload->>'gross')::numeric,(payload->>'deductions')::numeric,(payload->>'payable')::numeric from business_payroll.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update",
                transaction.Connection, transaction.Transaction);
            read.Parameters.AddWithValue(message.TenantId.Value);
            read.Parameters.AddWithValue(entityId);
            read.Parameters.AddWithValue(factId);
            await using var reader = await read.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
                throw new InvalidOperationException("payroll.accounting_fact_missing");
            var status = reader.GetString(0);
            var total = reader.GetDecimal(1);
            var currency = reader.GetString(2).Trim();
            var gross = reader.IsDBNull(3) ? total : reader.GetDecimal(3);
            var deductions = reader.IsDBNull(4) ? gross - (reader.IsDBNull(5) ? total : reader.GetDecimal(5)) : reader.GetDecimal(4);
            var payable = reader.IsDBNull(5) ? total : reader.GetDecimal(5);
            await reader.CloseAsync();
            if (status == "Posted")
                return true;
            if (status != "Ready")
                throw new InvalidOperationException("payroll.accounting_fact_not_ready");

            var payload = new PayrollPostedPayload(gross, payable, currency, deductions);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(
                factId,
                message.TenantId,
                new LegalEntityId(entityId),
                AccountingFactTypes.PayrollPosted,
                1,
                "payroll",
                source,
                source,
                DateOnly.FromDateTime(message.OccurredAt.UtcDateTime),
                DateOnly.FromDateTime(message.OccurredAt.UtcDateTime),
                payload,
                message.CorrelationId,
                Guid.Empty);
            var result = await ledgerPosting.PostPayrollAsync(transaction, fact, payload, token);
            await using var updateFact = new NpgsqlCommand(
                "update business_payroll.accounting_fact set status='Posted',payload=payload || jsonb_build_object('ledgerJournalEntryId',$4,'ledgerEntryNumber',$5),created_at=created_at where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Ready'",
                transaction.Connection, transaction.Transaction);
            updateFact.Parameters.AddWithValue(message.TenantId.Value);
            updateFact.Parameters.AddWithValue(entityId);
            updateFact.Parameters.AddWithValue(factId);
            updateFact.Parameters.AddWithValue(result.JournalEntryId);
            updateFact.Parameters.AddWithValue(result.EntryNumber);
            await updateFact.ExecuteNonQueryAsync(token);
            await using var updateRun = new NpgsqlCommand(
                "update business_payroll.payroll_run set accounting_state='Posted',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and accounting_state='Ready'",
                transaction.Connection, transaction.Transaction);
            updateRun.Parameters.AddWithValue(message.TenantId.Value);
            updateRun.Parameters.AddWithValue(entityId);
            updateRun.Parameters.AddWithValue(runId);
            await updateRun.ExecuteNonQueryAsync(token);
            return true;
        }, ct);
    }
}

public sealed class PayrollLedgerPostingConsumerDefinition : ConsumerDefinition<PayrollLedgerPostingConsumer>
{
    public PayrollLedgerPostingConsumerDefinition() => EndpointName = PayrollLedgerPostingConsumer.EndpointName;
}
