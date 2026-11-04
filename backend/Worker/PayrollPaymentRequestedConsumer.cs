using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using MassTransit;
using Npgsql;
using Shared.Contracts.Payments;

namespace Worker;

/// <summary>Turns finalized Payroll payment instructions into idempotent Payments drafts.</summary>
public sealed class PayrollPaymentRequestedConsumer(
    ITransactionCoordinator transactions,
    IPayrollPaymentCapability payments,
    IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "payroll-payment-requested";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (!string.Equals(message.MessageType, "PayrollPaymentRequestedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            await PrepareAsync(message, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private async Task PrepareAsync(OutboxPublishedMessage message, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;
        var runId = root.GetProperty("runId").GetGuid();
        var entityId = root.GetProperty("legalEntityId").GetGuid();

        var instructions = await transactions.ExecuteAsync(message.TenantId, async (context, token) =>
        {
            var result = new List<PayrollDisbursementInstruction>();
            await using var command = new NpgsqlCommand("select id,employment_id,amount,currency_code from business_payroll.payment_instruction where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and status in ('Prepared','Requested') order by id for update", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(message.TenantId.Value);
            command.Parameters.AddWithValue(entityId);
            command.Parameters.AddWithValue(runId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3).Trim()));
            await reader.CloseAsync();
            return (IReadOnlyList<PayrollDisbursementInstruction>)result;
        }, ct);
        if (instructions.Count == 0) return;

        var paymentIds = await payments.PreparePayrollPaymentsAsync(message.TenantId, Guid.Empty, Guid.Empty,
            message.CorrelationId, entityId, runId, instructions, ct);
        if (paymentIds.Count != instructions.Count)
            throw new InvalidOperationException("payroll.payment_instruction_result_mismatch");

        await transactions.ExecuteAsync(message.TenantId, async (context, token) =>
        {
            for (var i = 0; i < instructions.Count; i++)
            {
                await using var update = new NpgsqlCommand("update business_payroll.payment_instruction set status='Requested',external_reference=$4 where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and id=$5 and status in ('Prepared','Requested')", context.Connection, context.Transaction);
                update.Parameters.AddWithValue(message.TenantId.Value);
                update.Parameters.AddWithValue(entityId);
                update.Parameters.AddWithValue(runId);
                update.Parameters.AddWithValue(paymentIds[i].ToString("D"));
                update.Parameters.AddWithValue(instructions[i].InstructionId);
                await update.ExecuteNonQueryAsync(token);
            }
            await using var run = new NpgsqlCommand("update business_payroll.payroll_run set payment_state='Requested',updated_at=now() where tenant_id=$1 and legal_entity_id=$2 and id=$3 and payment_state in ('Ready','Requested')", context.Connection, context.Transaction);
            run.Parameters.AddWithValue(message.TenantId.Value);
            run.Parameters.AddWithValue(entityId);
            run.Parameters.AddWithValue(runId);
            await run.ExecuteNonQueryAsync(token);
            return true;
        }, ct);
    }
}

public sealed class PayrollPaymentRequestedConsumerDefinition : ConsumerDefinition<PayrollPaymentRequestedConsumer>
{
    public PayrollPaymentRequestedConsumerDefinition() => EndpointName = PayrollPaymentRequestedConsumer.EndpointName;
}
