using Npgsql;
using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Modules.Directory.Repositories;
using Shared.Primitives.Identifiers;
using Xunit;

namespace DirectoryTests;

public sealed class DirectoryAdvancedDatabaseTests
{
    [Fact]
    public async Task SeededDirectorySupportsVisibilitySearchReadinessAndImportIdempotency()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);

        await using var admin = NpgsqlDataSource.Create(adminConnectionString);
        var tenantId = await ScalarAsync<Guid>(admin, "select id from platform.tenant where slug='atlas-trading'");
        var legalEntityId = await ScalarAsync<Guid>(admin, "select id from platform.legal_entity where tenant_id=$1 order by id limit 1", tenantId);
        var restrictedPartyId = await ScalarAsync<Guid>(admin, "select party_id from foundation_directory.party_visibility where tenant_id=$1 and scope_type='SelectedLegalEntities' limit 1", tenantId);
        var alias = await ScalarAsync<string>(admin, "select alias from foundation_directory.party_alias where tenant_id=$1 and party_id=$2 order by id limit 1", tenantId, restrictedPartyId);

        await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new DirectoryRepository(new Core.Transactions.NpgsqlTransactionCoordinator(runtime), runtime);
        var advanced = new DirectoryAdvancedRepository(runtime, repository, Array.Empty<IPartyMergeContributor>(), Array.Empty<IPartyReadinessRule>());

        var tenantWide = await advanced.GetVisibleAsync(new TenantId(tenantId), restrictedPartyId, null, CancellationToken.None);
        Assert.Null(tenantWide);
        var scoped = await advanced.GetVisibleAsync(new TenantId(tenantId), restrictedPartyId, legalEntityId, CancellationToken.None);
        Assert.NotNull(scoped);

        var search = await advanced.ListVisibleAsync(new TenantId(tenantId), alias, null, null, null, legalEntityId, 1, 100, CancellationToken.None);
        Assert.Contains(search.Items, item => item.Id == scoped!.Id);

        var readiness = await advanced.ReadinessAsync(new TenantId(tenantId), scoped.Id, CancellationToken.None);
        Assert.NotNull(readiness);
        Assert.NotEmpty(readiness!.Capabilities!);
        Assert.Contains(readiness.Capabilities!, capability => capability.Capability == "CanSell");

        var key = $"advanced-test-{Guid.CreateVersion7():N}";
        try
        {
            var request = new DirectoryImportRequest([new DirectoryImportRow($"TEST-{key}", PartyType.Organization, "Preview Import Party")], false);
            var first = await advanced.ImportAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(), request, key, Guid.CreateVersion7(), CancellationToken.None);
            var replay = await advanced.ImportAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(), request, key, Guid.CreateVersion7(), CancellationToken.None);
            Assert.False(first.Committed);
            Assert.Equal(first.OperationId, replay.OperationId);
        }
        finally
        {
            await using var connection = await admin.OpenConnectionAsync();
            await using var outbox = new NpgsqlCommand("delete from platform.outbox_message where tenant_id=$1 and message_type='DirectoryPartyImportCompletedV1' and payload->>'operationId' in (select id::text from foundation_directory.import_operation where tenant_id=$1 and idempotency_key=$2)", connection);
            outbox.Parameters.AddWithValue(tenantId); outbox.Parameters.AddWithValue(key);
            await outbox.ExecuteNonQueryAsync();
            await using var cleanup = new NpgsqlCommand("delete from foundation_directory.import_operation where tenant_id=$1 and idempotency_key=$2", connection);
            cleanup.Parameters.AddWithValue(tenantId); cleanup.Parameters.AddWithValue(key);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MergeCreatesRedirectAndResolvesTheCanonicalParty()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        await using var admin = NpgsqlDataSource.Create(adminConnectionString);
        var tenantId = await ScalarAsync<Guid>(admin, "select id from platform.tenant where slug='atlas-trading'");
        await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new DirectoryRepository(new Core.Transactions.NpgsqlTransactionCoordinator(runtime), runtime);
        var advanced = new DirectoryAdvancedRepository(runtime, repository, Array.Empty<IPartyMergeContributor>(), Array.Empty<IPartyReadinessRule>());
        var tenant = new TenantId(tenantId);
        PartyDetails? master = null;
        PartyDetails? source = null;
        try
        {
            master = await repository.CreateAsync(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), new CreatePartyRequest(PartyType.Organization, "Merge Master Test", null, null, null, null, null, null, null, "EG", "en", [PartyRoleCode.Customer]), Guid.CreateVersion7(), null, true, null, CancellationToken.None);
            source = await repository.CreateAsync(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), new CreatePartyRequest(PartyType.Organization, "Merge Source Test", null, null, null, null, null, null, null, "EG", "en", [PartyRoleCode.Supplier]), Guid.CreateVersion7(), null, true, null, CancellationToken.None);
            var merged = await advanced.MergeAsync(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), source.Id, new MergePartyRequest(master.Id, "Duplicate party verified by Directory acceptance test", true), Guid.CreateVersion7(), CancellationToken.None);
            Assert.Equal(master.Id, merged.MasterPartyId);
            var resolved = await advanced.GetVisibleAsync(tenant, source.Id, null, CancellationToken.None);
            Assert.NotNull(resolved);
            Assert.Equal(master.Id, resolved!.Id);
        }
        finally
        {
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var id in new[] { source?.Id, master?.Id }.Where(x => x.HasValue).Select(x => x!.Value)) await DeletePartyAsync(connection, tenantId, id);
        }
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlDataSource dataSource, string sql, params object[] values)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a value."));
    }

    private static async Task ResetRuntimePasswordAsync(string adminConnectionString)
    {
        await using var admin = NpgsqlDataSource.Create(adminConnectionString);
        await using var connection = await admin.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("alter role erp_app password 'todir-app-dev'", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeletePartyAsync(NpgsqlConnection connection, Guid tenantId, Guid partyId)
    {
        foreach (var sql in new[]
        {
            "delete from foundation_directory.duplicate_candidate where tenant_id=$1 and (party_id=$2 or candidate_party_id=$2)",
            "delete from foundation_directory.party_merge_redirect where tenant_id=$1 and (source_party_id=$2 or target_party_id=$2)",
            "delete from foundation_directory.party_identifier where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.contact_point where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party_address where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party_role where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party_visibility where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party_alias where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party_contact where tenant_id=$1 and party_id=$2",
            "delete from foundation_directory.party where tenant_id=$1 and id=$2",
            "delete from platform.outbox_message where tenant_id=$1 and causation_id=$2",
            "delete from platform.audit_entry where tenant_id=$1 and entity_id=$2"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(partyId);
            await command.ExecuteNonQueryAsync();
        }
    }
}
