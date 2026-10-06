using Npgsql;
using Todir.Platform.Transactions;
using Todir.SharedKernel.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class TransactionIntegrationTests
{
    [Fact]
    public async Task CoordinatorCommitsDataAndOutboxAtomically()
    {
        var adminConnectionString = Require("TODIR_P04_ADMIN_CONNECTION");
        var tenantId = TenantId.New();
        var entityId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        var password = await SetRuntimePasswordAsync(adminConnectionString);
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; INSERT INTO platform_tenancy.tenant (id, slug, display_name, status, created_at, version) VALUES ('{tenantId.Value}', 'tx-{tenantId.Value:N}', 'Transaction', 'Active', now(), 1); RESET app.tenant_id; RESET ROLE;");
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            await coordinator.ExecuteAsync(tenantId, async (context, cancellationToken) =>
            {
                await using var entity = new NpgsqlCommand("insert into platform_org.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ($1, $2, 'TX', 'Transaction Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1)", context.Connection);
                entity.Parameters.AddWithValue(entityId); entity.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(entity);
                await entity.ExecuteNonQueryAsync(cancellationToken);
                await using var role = new NpgsqlCommand("insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ($1, $2, 'TX_ROLE', 'Transaction Role', false, true, 1)", context.Connection);
                role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(role);
                await role.ExecuteNonQueryAsync(cancellationToken);
                context.AddOutbox(new OutboxMessage(messageId, "TransactionCommittedV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "kernel"));
                return true;
            });

            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_org.legal_entity where id = '{entityId}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_authz.role where id = '{roleId}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform_messaging.outbox_message where id = '{messageId}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; DELETE FROM platform_messaging.outbox_message WHERE id = '{messageId}'; DELETE FROM platform_authz.role WHERE id = '{roleId}'; DELETE FROM platform_org.legal_entity WHERE id = '{entityId}'; DELETE FROM platform_tenancy.tenant WHERE id = '{tenantId.Value}'; RESET app.tenant_id; RESET ROLE;");
        }
    }

    [Fact]
    public async Task CoordinatorRollsBackDataAndOutboxTogether()
    {
        var adminConnectionString = Require("TODIR_P04_ADMIN_CONNECTION");
        var tenantId = TenantId.New();
        var entityId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        var password = await SetRuntimePasswordAsync(adminConnectionString);
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; INSERT INTO platform_tenancy.tenant (id, slug, display_name, status, created_at, version) VALUES ('{tenantId.Value}', 'tx-rollback-{tenantId.Value:N}', 'Rollback', 'Active', now(), 1); RESET app.tenant_id; RESET ROLE;");
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<bool>(tenantId, async (context, cancellationToken) =>
            {
                await using var entity = new NpgsqlCommand("insert into platform_org.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ($1, $2, 'RB', 'Rollback Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1)", context.Connection);
                entity.Parameters.AddWithValue(entityId); entity.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(entity);
                await entity.ExecuteNonQueryAsync(cancellationToken);
                await using var role = new NpgsqlCommand("insert into platform_authz.role (id, tenant_id, code, name, is_system_template, is_active, version) values ($1, $2, 'RB_ROLE', 'Rollback Role', false, true, 1)", context.Connection);
                role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(role);
                await role.ExecuteNonQueryAsync(cancellationToken);
                context.AddOutbox(new OutboxMessage(messageId, "TransactionRolledBackV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "kernel"));
                return await Task.FromException<bool>(new InvalidOperationException("controlled.rollback"));
            }));

            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform_org.legal_entity where id = '{entityId}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform_authz.role where id = '{roleId}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform_messaging.outbox_message where id = '{messageId}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; DELETE FROM platform_tenancy.tenant WHERE id = '{tenantId.Value}'; RESET app.tenant_id; RESET ROLE;");
        }
    }

    [Fact]
    public async Task DatabaseCommandTimeoutCancelsLongRunningQuery()
    {
        var adminConnectionString = Require("TODIR_P04_ADMIN_CONNECTION");
        var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            CommandTimeout = 1
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("select pg_sleep(3)");

        await Assert.ThrowsAsync<NpgsqlException>(() => command.ExecuteScalarAsync());
    }

    private static string Require(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} to run the transaction integration gate.");

    private static async Task<string> SetRuntimePasswordAsync(string adminConnectionString)
    {
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"ALTER ROLE erp_app PASSWORD '{password}'");
        return password;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
