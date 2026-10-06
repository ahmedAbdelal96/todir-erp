using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using Npgsql;
using Todir.Platform.Authorization;
using Todir.Platform.Entitlements;
using Todir.Platform.Messaging;
using Todir.Platform.Modules;
using Todir.Platform.Services;
using Todir.Platform.Transactions;
using Todir.SharedKernel.Identifiers;

namespace Todir.Modules.Sample;

public sealed record CreateSampleItemRequest(string Name);
public sealed record SampleItem(Guid Id, TenantId TenantId, string Name, Guid CreatedBy, DateTimeOffset CreatedAt);

public sealed class SampleItemService(ITransactionCoordinator coordinator)
{
    public Task<SampleItem> CreateAsync(
        TenantId tenantId,
        Guid actorId,
        Guid membershipId,
        string name,
        Guid correlationId,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("sample.name_required", nameof(name));
        if (name.Length > 200) throw new ArgumentException("sample.name_too_long", nameof(name));

        return coordinator.ExecuteAsync(tenantId, async (context, ct) =>
        {
            var item = new SampleItem(Guid.CreateVersion7(), tenantId, name.Trim(), actorId, DateTimeOffset.UtcNow);
            await using (var command = new NpgsqlCommand("insert into sample.sample_item (id, tenant_id, name, created_by, created_at, version) values ($1, $2, $3, $4, $5, 1)", context.Connection, context.Transaction))
            {
                command.Parameters.AddWithValue(item.Id);
                command.Parameters.AddWithValue(item.TenantId.Value);
                command.Parameters.AddWithValue(item.Name);
                command.Parameters.AddWithValue(item.CreatedBy);
                command.Parameters.AddWithValue(item.CreatedAt);
                await command.ExecuteNonQueryAsync(ct);
            }

            var audit = new AuditEntry(
                Guid.CreateVersion7(), tenantId, null, null, "Human", actorId, membershipId,
                "sample", "sample_item_created", "sample_item", item.Id, null,
                JsonSerializer.Serialize(new { item.Name }), null, "sample-api", correlationId,
                System.Diagnostics.Activity.Current?.TraceId.ToString(), item.CreatedAt, null);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, audit, ct);
            context.AddOutbox(new OutboxMessage(
                Guid.CreateVersion7(), "SampleEntityCreatedV1",
                JsonSerializer.Serialize(new { itemId = item.Id, item.Name }), tenantId,
                item.CreatedAt, correlationId, item.Id, "sample", traceParent));
            return item;
        }, cancellationToken);
    }
}

public sealed class SampleProjectionConsumer(IInboxStore inboxStore, NpgsqlDataSource dataSource) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "sample-projection";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        if (!string.Equals(context.Message.MessageType, "SampleEntityCreatedV1", StringComparison.Ordinal)) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, context.Message.MessageId, context.Message.TenantId, context.CancellationToken)) return;

        var payload = JsonSerializer.Deserialize<SampleEventPayload>(context.Message.Payload)
            ?? throw new InvalidDataException("sample.event_payload_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(context.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(context.CancellationToken);
        await using (var tenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            tenant.Parameters.AddWithValue(context.Message.TenantId.Value.ToString("D"));
            await tenant.ExecuteNonQueryAsync(context.CancellationToken);
        }
        await using (var projection = new NpgsqlCommand("insert into sample.sample_projection (tenant_id, item_id, projected_at, version) values ($1, $2, now(), 1) on conflict (tenant_id, item_id) do update set projected_at = excluded.projected_at, version = sample.sample_projection.version + 1", connection, transaction))
        {
            projection.Parameters.AddWithValue(context.Message.TenantId.Value);
            projection.Parameters.AddWithValue(payload.ItemId);
            await projection.ExecuteNonQueryAsync(context.CancellationToken);
        }
        await transaction.CommitAsync(context.CancellationToken);
        await inboxStore.MarkProcessedAsync(EndpointName, context.Message.MessageId, context.Message.TenantId, context.CancellationToken);
    }

    private sealed record SampleEventPayload([property: JsonPropertyName("itemId")] Guid ItemId, string Name);
}

public sealed class SampleProjectionConsumerDefinition : ConsumerDefinition<SampleProjectionConsumer>
{
    public SampleProjectionConsumerDefinition()
    {
        EndpointName = SampleProjectionConsumer.EndpointName;
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<SampleProjectionConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)));
    }
}

public sealed class SampleModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "sample", "1.0.0", 1, "L1", "foundation", false, "tenant",
        new(["platform-kernel"], []), new("sample", ["sample_item", "sample_projection"]),
        ["sample.item.create", "sample.item.read"], new(["sample-provider"], []),
        new(["SampleEntityCreatedV1"], ["SampleEntityCreatedV1"]), new(true, "wind_down"));
}
