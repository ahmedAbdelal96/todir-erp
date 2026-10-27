using Npgsql;
using Core.Services;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class NumberingIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task DatabaseSequenceAllocatorProducesUniqueDeterministicNumbersUnderConcurrency()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the numbering integration gate.");
        var tenant = TenantId.New();
        var sequenceId = Guid.CreateVersion7();
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenant.Value}', 'numbering-{tenant.Value:N}', 'Numbering', 'Active', 'numbering-{tenant.Value:N}.local', 'numbering-{tenant.Value:N}.local', now(), 1); insert into platform.sequence_definition (id, tenant_id, sequence_key, prefix_template, padding, mode, next_value, period_key, version) values ('{sequenceId}', '{tenant.Value}', 'integration', 'INV-', 5, 'Standard', 1, '2026', 1); reset app.tenant_id; reset role;");
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var allocator = new NpgsqlSequenceAllocator(dataSource);
            var definition = new SequenceDefinition("integration", tenant, null, null, null, "2026", "INV-", 5, 1);
            var numbers = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => allocator.AllocateAsync(definition)));

            Assert.Equal(50, numbers.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains("INV-00001", numbers);
            Assert.Contains("INV-00050", numbers);
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; delete from platform.sequence_definition where tenant_id = '{tenant.Value}' and id = '{sequenceId}'; delete from platform.tenant where id = '{tenant.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
