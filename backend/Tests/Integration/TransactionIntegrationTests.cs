using Npgsql;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class TransactionIntegrationTests : DatabaseIntegrationTestBase
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
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantId.Value}', 'tx-{tenantId.Value:N}', 'Transaction', 'Active', 'tx-{tenantId.Value:N}.local', 'tx-{tenantId.Value:N}.local', now(), 1); RESET app.tenant_id; RESET ROLE;");
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            await coordinator.ExecuteAsync(tenantId, async (context, cancellationToken) =>
            {
                await using var entity = new NpgsqlCommand("insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ($1, $2, 'TX', 'Transaction Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1)", context.Connection);
                entity.Parameters.AddWithValue(entityId); entity.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(entity);
                await entity.ExecuteNonQueryAsync(cancellationToken);
                await using var role = new NpgsqlCommand("insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version) values ($1, $2, 'TX_ROLE', 'Transaction Role', false, true, 1)", context.Connection);
                role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(role);
                await role.ExecuteNonQueryAsync(cancellationToken);
                context.AddOutbox(new OutboxMessage(messageId, "TransactionCommittedV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "kernel"));
                return true;
            });

            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.legal_entity where id = '{entityId}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.role where id = '{roleId}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.outbox_message where id = '{messageId}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; DELETE FROM platform.outbox_message WHERE id = '{messageId}'; DELETE FROM platform.role WHERE id = '{roleId}'; DELETE FROM platform.legal_entity WHERE id = '{entityId}'; DELETE FROM platform.tenant WHERE id = '{tenantId.Value}'; RESET app.tenant_id; RESET ROLE;");
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
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; INSERT INTO platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) VALUES ('{tenantId.Value}', 'tx-rollback-{tenantId.Value:N}', 'Rollback', 'Active', 'tx-rollback-{tenantId.Value:N}.local', 'tx-rollback-{tenantId.Value:N}.local', now(), 1); RESET app.tenant_id; RESET ROLE;");
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<bool>(tenantId, async (context, cancellationToken) =>
            {
                await using var entity = new NpgsqlCommand("insert into platform.legal_entity (id, tenant_id, code, legal_name, country_code, base_currency_code, timezone, locale, status, created_at, version) values ($1, $2, 'RB', 'Rollback Entity', 'EG', 'EGP', 'Africa/Cairo', 'ar-EG', 'Active', now(), 1)", context.Connection);
                entity.Parameters.AddWithValue(entityId); entity.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(entity);
                await entity.ExecuteNonQueryAsync(cancellationToken);
                await using var role = new NpgsqlCommand("insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version) values ($1, $2, 'RB_ROLE', 'Rollback Role', false, true, 1)", context.Connection);
                role.Parameters.AddWithValue(roleId); role.Parameters.AddWithValue(tenantId.Value);
                context.Enlist(role);
                await role.ExecuteNonQueryAsync(cancellationToken);
                context.AddOutbox(new OutboxMessage(messageId, "TransactionRolledBackV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "kernel"));
                return await Task.FromException<bool>(new InvalidOperationException("controlled.rollback"));
            }));

            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform.legal_entity where id = '{entityId}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform.role where id = '{roleId}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform.outbox_message where id = '{messageId}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"SET ROLE erp_owner; SET app.tenant_id = '{tenantId.Value}'; DELETE FROM platform.tenant WHERE id = '{tenantId.Value}'; RESET app.tenant_id; RESET ROLE;");
        }
    }

    [Fact]
    public async Task SampleModuleAAndBContextsCommitAndRollbackWithTenantRlsAndOutbox()
    {
        var adminConnectionString = Require("TODIR_P04_ADMIN_CONNECTION");
        var tenantId = TenantId.New();
        var successA = Guid.CreateVersion7();
        var successB = Guid.CreateVersion7();
        var successMessage = Guid.CreateVersion7();
        var rollbackA = Guid.CreateVersion7();
        var rollbackB = Guid.CreateVersion7();
        var rollbackMessage = Guid.CreateVersion7();
        var password = await SetRuntimePasswordAsync(adminConnectionString);
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await ExecuteAsync(admin, "set role erp_owner; create schema sample_a; create schema sample_b; create table sample_a.entity (id uuid primary key, tenant_id uuid not null); create table sample_b.entity (id uuid primary key, tenant_id uuid not null); alter table sample_a.entity enable row level security; alter table sample_a.entity force row level security; alter table sample_b.entity enable row level security; alter table sample_b.entity force row level security; create policy tenant_isolation on sample_a.entity using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid); create policy tenant_isolation on sample_b.entity using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid); grant usage on schema sample_a, sample_b to erp_app; grant select, insert on sample_a.entity, sample_b.entity to erp_app; reset role;");
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'sample-ab-{tenantId.Value:N}', 'Sample A/B', 'Active', 'sample-ab-{tenantId.Value:N}.local', 'sample-ab-{tenantId.Value:N}.local', now(), 1); reset app.tenant_id; reset role;");

            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            var visibleCounts = await coordinator.ExecuteAsync(tenantId, async (context, cancellationToken) =>
            {
                await InsertSampleRowAsync(context, "sample_a.entity", successA, tenantId, cancellationToken);
                await InsertSampleRowAsync(context, "sample_b.entity", successB, tenantId, cancellationToken);
                context.AddOutbox(new OutboxMessage(successMessage, "SampleModulesCommittedV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "sample-a"));
                return (await CountSampleRowsAsync(context, "sample_a.entity", cancellationToken), await CountSampleRowsAsync(context, "sample_b.entity", cancellationToken));
            });

            Assert.Equal((1L, 1L), visibleCounts);
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from sample_a.entity where id = '{successA}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from sample_b.entity where id = '{successB}'"));
            Assert.Equal(1L, await ScalarAsync(admin, $"select count(*) from platform.outbox_message where id = '{successMessage}'"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<bool>(tenantId, async (context, cancellationToken) =>
            {
                await InsertSampleRowAsync(context, "sample_a.entity", rollbackA, tenantId, cancellationToken);
                await InsertSampleRowAsync(context, "sample_b.entity", rollbackB, tenantId, cancellationToken);
                context.AddOutbox(new OutboxMessage(rollbackMessage, "SampleModulesRolledBackV1", "{}", tenantId, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "sample-a"));
                throw new InvalidOperationException("sample-b-controlled-failure");
            }));

            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from sample_a.entity where id = '{rollbackA}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from sample_b.entity where id = '{rollbackB}'"));
            Assert.Equal(0L, await ScalarAsync(admin, $"select count(*) from platform.outbox_message where id = '{rollbackMessage}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform.outbox_message where id in ('{successMessage}', '{rollbackMessage}'); delete from platform.tenant where id = '{tenantId.Value}'; reset app.tenant_id; drop schema if exists sample_a cascade; drop schema if exists sample_b cascade; reset role;");
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

    private static async Task InsertSampleRowAsync(ITransactionContext context, string table, Guid id, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"insert into {table} (id, tenant_id) values ($1, $2)", context.Connection, context.Transaction);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(tenantId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountSampleRowsAsync(ITransactionContext context, string table, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"select count(*) from {table}", context.Connection, context.Transaction);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
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
