using System.Text.Json;
using Npgsql;
using Todir.Platform.Services;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class NotificationIntegrationTests
{
    [Fact]
    public async Task NotificationIsClaimedAndRecordedAfterProviderDelivery()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the notification integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantId.Value}', 'notification-{tenantId.Value:N}', 'Notification', 'Active', now(), 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var provider = new RecordingSender(NotificationChannel.Email);
            var dispatcher = new NotificationDispatcher(new NpgsqlNotificationStore(dataSource), [provider]);
            var notification = await dispatcher.EnqueueAsync(tenantId, "identity:member", "membership.invited", NotificationChannel.Email, JsonSerializer.SerializeToElement(new { displayName = "Member" }));

            Assert.True(await dispatcher.DispatchNextAsync(tenantId));
            Assert.Single(provider.Sent);
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_notifications.notification where tenant_id = '{tenantId.Value}' and id = '{notification.Id}' and status = 'Sent'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_notifications.notification_delivery where tenant_id = '{tenantId.Value}' and notification_id = '{notification.Id}' and status = 'Sent'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform_notifications.notification_delivery where tenant_id = '{tenantId.Value}'; delete from platform_notifications.notification where tenant_id = '{tenantId.Value}'; delete from platform_tenancy.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class RecordingSender(NotificationChannel channel) : INotificationSender
    {
        public NotificationChannel Channel => channel;
        public List<Notification> Sent { get; } = [];
        public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }
}
