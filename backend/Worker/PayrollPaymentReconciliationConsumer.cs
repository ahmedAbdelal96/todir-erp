using System.Text.Json;
using Core.Messaging;
using Core.Transactions;
using MassTransit;
using Npgsql;

namespace Worker;

/// <summary>Reconciles Payroll payment instructions only from confirmed Payments events.</summary>
public sealed class PayrollPaymentReconciliationConsumer(
    ITransactionCoordinator transactions,
    IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "payroll-payment-reconciliation";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType is not ("PaymentCompletedV1" or "PaymentCancelledV1")) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            await ReconcileAsync(message, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }

    private Task<bool> ReconcileAsync(OutboxPublishedMessage message, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(message.Payload);
        var root = document.RootElement;
        var paymentId = root.GetProperty("paymentId").GetGuid();
        var legalEntityId = root.TryGetProperty("legalEntityId", out var entity) ? entity.GetGuid() : Guid.Empty;
        var targetStatus = message.MessageType == "PaymentCompletedV1" ? "Paid" : "Failed";
        return transactions.ExecuteAsync(message.TenantId, async (context, token) =>
        {
            await using var payment = new NpgsqlCommand("select legal_entity_id,source_type,source_reference from finance_payments.payment where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            payment.Parameters.AddWithValue(message.TenantId.Value); payment.Parameters.AddWithValue(paymentId);
            await using var reader = await payment.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return true;
            var paymentEntity = reader.GetGuid(0); var sourceType = reader.IsDBNull(1) ? null : reader.GetString(1); var sourceReference = reader.IsDBNull(2) ? null : reader.GetString(2);
            await reader.CloseAsync();
            if (!string.Equals(sourceType, "Payroll", StringComparison.Ordinal) || sourceReference is null) return true;
            var parts = sourceReference.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var runId) || !Guid.TryParse(parts[1], out var instructionId)) return true;
            if (legalEntityId != Guid.Empty && legalEntityId != paymentEntity) throw new InvalidOperationException("payroll.payment_entity_mismatch");
            await using (var update = new NpgsqlCommand("update business_payroll.payment_instruction set status=$4,external_reference=$5 where tenant_id=$1 and legal_entity_id=$2 and run_id=$3 and id=$6 and status in ('Prepared','Requested','Partial')", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(message.TenantId.Value); update.Parameters.AddWithValue(paymentEntity); update.Parameters.AddWithValue(runId); update.Parameters.AddWithValue(targetStatus); update.Parameters.AddWithValue(paymentId.ToString("D")); update.Parameters.AddWithValue(instructionId); await update.ExecuteNonQueryAsync(token);
            }
            await using var run = new NpgsqlCommand("update business_payroll.payroll_run r set payment_state=case when exists(select 1 from business_payroll.payment_instruction i where i.tenant_id=r.tenant_id and i.legal_entity_id=r.legal_entity_id and i.run_id=r.id and i.status='Failed') then 'Failed' when not exists(select 1 from business_payroll.payment_instruction i where i.tenant_id=r.tenant_id and i.legal_entity_id=r.legal_entity_id and i.run_id=r.id and i.status<>'Paid') then 'Paid' when exists(select 1 from business_payroll.payment_instruction i where i.tenant_id=r.tenant_id and i.legal_entity_id=r.legal_entity_id and i.run_id=r.id and i.status='Paid') then 'Partial' else 'Requested' end,updated_at=now() where r.tenant_id=$1 and r.legal_entity_id=$2 and r.id=$3", context.Connection, context.Transaction);
            run.Parameters.AddWithValue(message.TenantId.Value); run.Parameters.AddWithValue(paymentEntity); run.Parameters.AddWithValue(runId); await run.ExecuteNonQueryAsync(token);
            return true;
        }, ct);
    }
}

public sealed class PayrollPaymentReconciliationConsumerDefinition : ConsumerDefinition<PayrollPaymentReconciliationConsumer>
{
    public PayrollPaymentReconciliationConsumerDefinition() => EndpointName = PayrollPaymentReconciliationConsumer.EndpointName;
}
