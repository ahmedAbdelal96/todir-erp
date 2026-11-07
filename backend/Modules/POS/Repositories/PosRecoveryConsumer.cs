using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Npgsql;

namespace Modules.POS;

public sealed class PosRecoveryConsumer(NpgsqlDataSource dataSource) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "pos-recovery";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        if (!string.Equals(context.Message.MessageType, "PosCheckoutRecoveryRequiredV1", StringComparison.Ordinal)) return;
        using var payload = JsonDocument.Parse(context.Message.Payload);
        if (!payload.RootElement.TryGetProperty("checkoutId", out var checkout) || !Guid.TryParse(checkout.GetString(), out var checkoutId)) throw new InvalidDataException("pos.recovery_payload_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(context.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(context.CancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { tenant.Parameters.AddWithValue(context.Message.TenantId.Value.ToString("D")); await tenant.ExecuteNonQueryAsync(context.CancellationToken); }
        await using (var update = new NpgsqlCommand("update business_pos.pos_checkout set state=case when state='CompletionPending' then 'RecoveryRequired' else state end,recovery_reason=coalesce(recovery_reason,'downstream_completion_pending'),updated_at=now(),row_version=row_version+1 where tenant_id=$1 and id=$2 and state in ('CompletionPending','RecoveryRequired')", connection, transaction)) { update.Parameters.AddWithValue(context.Message.TenantId.Value); update.Parameters.AddWithValue(checkoutId); await update.ExecuteNonQueryAsync(context.CancellationToken); }
        await transaction.CommitAsync(context.CancellationToken);
    }
}

public sealed class PosRecoveryConsumerDefinition : ConsumerDefinition<PosRecoveryConsumer>
{
    public PosRecoveryConsumerDefinition() { EndpointName = PosRecoveryConsumer.EndpointName; ConcurrentMessageLimit = 8; }
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<PosRecoveryConsumer> consumerConfigurator, IRegistrationContext context) => endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)));
}
