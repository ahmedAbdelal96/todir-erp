using Core.Messaging;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Modules.Catalog.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class CatalogReplayIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task PublishedCatalogEventCanBeReplayedWithoutDuplicatingProjection()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Catalog replay integration test.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for the Catalog replay integration test.");
        var runtimePassword = new NpgsqlConnectionStringBuilder(runtimeConnectionString).Password
            ?? throw new InvalidOperationException("TODIR_DATABASE_CONNECTION must configure the runtime role password.");

        Guid tenantId;
        Guid actorId;
        Guid membershipId;
        Guid baseUomId;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            var escapedPassword = runtimePassword.Replace("'", "''", StringComparison.Ordinal);
            await using (var resetPassword = new NpgsqlCommand($"alter role erp_app password '{escapedPassword}'", admin))
                await resetPassword.ExecuteNonQueryAsync();
            await using var lookup = new NpgsqlCommand("select t.id,i.id,m.id,u.id from platform.tenant t join platform.identity i on i.tenant_id=t.id and i.normalized_email='ADMIN@ATLAS.COM' join platform.membership m on m.tenant_id=t.id and m.identity_id=i.id join foundation_catalog.uom u on u.tenant_id=t.id and u.code='EA' and u.status='Active' where t.normalized_login_domain='atlas.com' order by u.id limit 1", admin);
            await using var reader = await lookup.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Atlas seed administrator and EA unit must exist.");
            tenantId = reader.GetGuid(0);
            actorId = reader.GetGuid(1);
            membershipId = reader.GetGuid(2);
            baseUomId = reader.GetGuid(3);
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var products = new ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);
        var correlationId = Guid.CreateVersion7();
        var created = await products.CreateAsync(
            new TenantId(tenantId),
            actorId,
            membershipId,
            new CreateProductRequest("Catalog replay idempotency probe", ProductNature.Goods, $"REPLAY-{Guid.CreateVersion7():N}"[..22], null, "Catalog replay idempotency probe", null, null, baseUomId),
            correlationId,
            null,
            CancellationToken.None);

        Guid messageId = Guid.Empty;
        var initialInbox = (Processed: false, Attempts: 0, ProcessedAt: (DateTimeOffset?)null);
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var message = new NpgsqlCommand("select o.id from platform.outbox_message o join platform.inbox_message i on i.tenant_id=o.tenant_id and i.message_id=o.id and i.consumer_endpoint='catalog-search-projection' and i.processed_at is not null where o.tenant_id=$1 and o.message_type='CatalogProductCreatedV1' and o.causation_id=$2 and o.status='Published' order by o.occurred_at desc limit 1", connection);
            message.Parameters.AddWithValue(tenantId);
            message.Parameters.AddWithValue(created.Id);
            messageId = (Guid)(await message.ExecuteScalarAsync() ?? Guid.Empty);
            if (messageId != Guid.Empty)
            {
                initialInbox = await ReadInboxAsync(dataSource, tenantId, "catalog-search-projection", messageId);
                if (initialInbox.Processed) break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
        Assert.NotEqual(Guid.Empty, messageId);

        var endpoint = "catalog-search-projection";
        Assert.True(initialInbox.Processed, "The real Catalog projection worker must process the original event first.");

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var failure = new NpgsqlCommand("insert into platform.message_error (id,message_id,tenant_id,consumer_endpoint,attempt,error_code,detail,occurred_at,resolved) values ($1,$2,$3,$4,1,'integration.replay_probe','Replay idempotency integration setup',$5,false)", admin);
            failure.Parameters.AddWithValue(Guid.CreateVersion7());
            failure.Parameters.AddWithValue(messageId);
            failure.Parameters.AddWithValue(tenantId);
            failure.Parameters.AddWithValue(endpoint);
            failure.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            await failure.ExecuteNonQueryAsync();
        }

        var rabbitHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST")
            ?? throw new InvalidOperationException("Set TODIR_RABBITMQ_HOST for the Catalog replay integration test.");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", rabbitHost)
            .UseSetting("TODIR_RABBITMQ_PORT", Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT") ?? "5672")
            .UseSetting("TODIR_RABBITMQ_USERNAME", Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USERNAME") ?? "todir")
            .UseSetting("TODIR_RABBITMQ_PASSWORD", Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? "todir-dev-only")
            .UseSetting("TODIR_RABBITMQ_VHOST", Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev"));
        using var scope = factory.Services.CreateScope();
        var replay = scope.ServiceProvider.GetRequiredService<IMessageReplayService>();
        await replay.ReplayAsync(messageId, new TenantId(tenantId), endpoint, actorId, "Catalog projection replay idempotency verification", CancellationToken.None);

        var finalInbox = await ReadInboxAsync(dataSource, tenantId, endpoint, messageId);
        Assert.True(finalInbox.Processed);
        Assert.Equal(initialInbox.Attempts, finalInbox.Attempts);
        Assert.Equal(initialInbox.ProcessedAt, finalInbox.ProcessedAt);
        await using (var connection = await dataSource.OpenConnectionAsync())
        {
            await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
            tenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await tenant.ExecuteNonQueryAsync();
            await using var state = new NpgsqlCommand("select (select count(*) from foundation_catalog.catalog_search_projection where tenant_id=$1 and template_id=$2),(select count(*) from platform.message_error where tenant_id=$1 and message_id=$3 and consumer_endpoint=$4 and resolved),(select count(*) from platform.audit_entry where tenant_id=$1 and action='message_replayed' and entity_id=$3 and reason='Catalog projection replay idempotency verification')", connection);
            state.Parameters.AddWithValue(tenantId);
            state.Parameters.AddWithValue(created.Id);
            state.Parameters.AddWithValue(messageId);
            state.Parameters.AddWithValue(endpoint);
            await using var reader = await state.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(1L, reader.GetInt64(1));
            Assert.Equal(1L, reader.GetInt64(2));
        }
    }

    private static async Task<(bool Processed, int Attempts, DateTimeOffset? ProcessedAt)> ReadInboxAsync(NpgsqlDataSource dataSource, Guid tenantId, string endpoint, Guid messageId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var tenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        tenant.Parameters.AddWithValue(tenantId.ToString("D"));
        await tenant.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("select processed_at is not null,attempts,processed_at from platform.inbox_message where tenant_id=$1 and consumer_endpoint=$2 and message_id=$3", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(endpoint);
        command.Parameters.AddWithValue(messageId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The real Catalog projection worker must create an inbox receipt.");
        return (reader.GetBoolean(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2));
    }
}
