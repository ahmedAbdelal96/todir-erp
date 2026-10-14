using Npgsql;
using System.Text.Json;
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

    [Fact]
    public async Task PartyAuditExposesPersistedActorChangesAndReasonWithoutLeakingAcrossTenants()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        await using var admin = NpgsqlDataSource.Create(adminConnectionString);
        var atlasId = await ScalarAsync<Guid>(admin, "select id from platform.tenant where slug='atlas-trading'");
        var deltaId = await ScalarAsync<Guid>(admin, "select id from platform.tenant where slug='delta-supplies'");
        Guid actorId;
        Guid membershipId;
        string actorName;
        await using (var connection = await admin.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("select m.identity_id,m.id,m.display_name from platform.membership m where m.tenant_id=$1 and m.status='Active' order by m.id limit 1", connection))
        {
            command.Parameters.AddWithValue(atlasId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            actorId = reader.GetGuid(0); membershipId = reader.GetGuid(1); actorName = reader.GetString(2);
        }

        await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new DirectoryRepository(new Core.Transactions.NpgsqlTransactionCoordinator(runtime), runtime);
        var advanced = new DirectoryAdvancedRepository(runtime, repository, Array.Empty<IPartyMergeContributor>(), Array.Empty<IPartyReadinessRule>());
        var tenant = new TenantId(atlasId);
        PartyDetails? master = null;
        PartyDetails? source = null;
        var startedAt = DateTimeOffset.UtcNow;
        var editEndedAt = startedAt;
        const string mergeReason = "Duplicate confirmed during audit contract regression.";
        try
        {
            master = await repository.CreateAsync(tenant, actorId, membershipId, new CreatePartyRequest(PartyType.Organization, "Audit master before", "Audit legal before", null, null, null, null, null, null, "EG", "en", [PartyRoleCode.Customer]), Guid.CreateVersion7(), null, true, null, CancellationToken.None);
            source = await repository.CreateAsync(tenant, actorId, membershipId, new CreatePartyRequest(PartyType.Organization, "Audit source", null, null, null, null, null, null, null, "EG", "en", [PartyRoleCode.Supplier]), Guid.CreateVersion7(), null, true, null, CancellationToken.None);
            await repository.UpdateAsync(tenant, actorId, membershipId, master.Id, new UpdatePartyRequest("Audit master after", "Audit legal after", null, null, null, null, null, null, "EG", "en", master.Version), Guid.CreateVersion7(), null, CancellationToken.None);
            editEndedAt = DateTimeOffset.UtcNow;

            var entries = await advanced.AuditAsync(tenant, master.Id, CancellationToken.None);
            var edit = Assert.Single(entries, entry => entry.Action == "party_identity_changed");
            Assert.Equal("Human", edit.ActorType);
            Assert.Equal(actorId, edit.ActorId);
            Assert.Equal(actorName, edit.ActorName);
            Assert.InRange(edit.OccurredAt, startedAt, editEndedAt);
            Assert.Contains(edit.Changes, change => change.Field == "displayName" && change.BeforeValue?.GetString() == "Audit master before" && change.AfterValue?.GetString() == "Audit master after");
            Assert.Contains(edit.Changes, change => change.Field == "legalName" && change.BeforeValue?.GetString() == "Audit legal before" && change.AfterValue?.GetString() == "Audit legal after");
            Assert.DoesNotContain(edit.Changes, change => change.Field.Contains("identifier", StringComparison.OrdinalIgnoreCase) || change.Field.Contains("email", StringComparison.OrdinalIgnoreCase) || change.Field.Contains("phone", StringComparison.OrdinalIgnoreCase));
            Assert.False(JsonSerializer.Serialize(edit).Contains("identifierValue", StringComparison.OrdinalIgnoreCase));

            const string sensitiveIdentifier = "AUDIT-MUST-NOT-EXPOSE-884201";
            await repository.AddIdentifierAsync(tenant, actorId, membershipId, master.Id, new IdentifierInput("Tax", sensitiveIdentifier, "EG", "Test Authority", true), Guid.CreateVersion7(), CancellationToken.None);
            var identifierAudit = Assert.Single(await advanced.AuditAsync(tenant, master.Id, CancellationToken.None), entry => entry.Action == "party_identifier_added");
            Assert.Contains(identifierAudit.Changes, change => change.Field == "type" && change.AfterValue?.GetString() == "Tax");
            Assert.False(JsonSerializer.Serialize(identifierAudit).Contains(sensitiveIdentifier, StringComparison.Ordinal));

            await advanced.MergeAsync(tenant, actorId, membershipId, source.Id, new MergePartyRequest(master.Id, mergeReason, true), Guid.CreateVersion7(), CancellationToken.None);
            var merge = Assert.Single(await advanced.AuditAsync(tenant, source.Id, CancellationToken.None), entry => entry.Action == "party_merged");
            Assert.Equal(mergeReason, merge.Reason);

            Assert.Empty(await advanced.AuditAsync(new TenantId(deltaId), master.Id, CancellationToken.None));
            Assert.NotNull(await repository.GetAsync(tenant, master.Id, true, CancellationToken.None));
            Assert.NotEmpty(await advanced.AuditAsync(tenant, master.Id, CancellationToken.None));
        }
        finally
        {
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var id in new[] { source?.Id, master?.Id }.Where(x => x.HasValue).Select(x => x!.Value)) await DeletePartyAsync(connection, atlasId, id);
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
