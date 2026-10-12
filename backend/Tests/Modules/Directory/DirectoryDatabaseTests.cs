using Npgsql;
using Modules.Directory.Services;
using Modules.Directory.DTOs;
using Modules.Directory.Entities;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace DirectoryTests;

public sealed class DirectoryDatabaseTests
{
    [Fact]
    public async Task RuntimeRoleCannotSeeAnotherTenantParty()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        var tenantA = Guid.CreateVersion7(); var tenantB = Guid.CreateVersion7(); var party = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1),($5,$6,$7,'Active',$8,$8,now(),1)", connection);
            command.Parameters.AddWithValue(tenantA); command.Parameters.AddWithValue($"directory-a-{tenantA:N}"); command.Parameters.AddWithValue("Directory A"); command.Parameters.AddWithValue($"directory-a-{tenantA:N}.local"); command.Parameters.AddWithValue(tenantB); command.Parameters.AddWithValue($"directory-b-{tenantB:N}"); command.Parameters.AddWithValue("Directory B"); command.Parameters.AddWithValue($"directory-b-{tenantB:N}.local"); await command.ExecuteNonQueryAsync();
        }
        try
        {
            await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString);
            await using var connection = await runtime.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { context.Parameters.AddWithValue(tenantA.ToString("D")); await context.ExecuteNonQueryAsync(); }
            await using (var insert = new NpgsqlCommand("insert into foundation_directory.party (tenant_id,id,party_type,status,display_name,display_name_normalized,created_by,updated_by,created_at,updated_at,version) values ($1,$2,'Organization','Active','Visible A','visible a',$3,$3,now(),now(),1)", connection, transaction)) { insert.Parameters.AddWithValue(tenantA); insert.Parameters.AddWithValue(party); insert.Parameters.AddWithValue(Guid.CreateVersion7()); await insert.ExecuteNonQueryAsync(); }
            await using (var switchTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { switchTenant.Parameters.AddWithValue(tenantB.ToString("D")); await switchTenant.ExecuteNonQueryAsync(); }
            await using var select = new NpgsqlCommand("select count(*) from foundation_directory.party", connection, transaction); Assert.Equal(0L, (long)(await select.ExecuteScalarAsync() ?? -1L)); await transaction.RollbackAsync();
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync();
            await using var deleteParty = new NpgsqlCommand("delete from foundation_directory.party where tenant_id=$1", connection); deleteParty.Parameters.AddWithValue(tenantA); await deleteParty.ExecuteNonQueryAsync();
            await using var delete = new NpgsqlCommand("delete from platform.tenant where id in ($1,$2)", connection); delete.Parameters.AddWithValue(tenantA); delete.Parameters.AddWithValue(tenantB); await delete.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task ServiceCreatesPartyAndRejectsExactIdentifierDuplicate()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DIRECTORY_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        var tenantId = Guid.CreateVersion7(); var actorId = Guid.CreateVersion7(); var membershipId = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection); command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue($"directory-service-{tenantId:N}"); command.Parameters.AddWithValue("Directory Service Test"); command.Parameters.AddWithValue($"directory-service-{tenantId:N}.local"); await command.ExecuteNonQueryAsync();
        }
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            var service = new DirectoryService(new Modules.Directory.Repositories.DirectoryRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var request = new CreatePartyRequest(PartyType.Organization, "شركة اختبار الدليل", "شركة اختبار الدليل", null, null, null, null, null, null, "EG", "ar", [PartyRoleCode.Customer], [new IdentifierInput("Tax", "١٢٣٤٥", "EG", "Tax Authority", true)], [new ContactPointInput("Email", "directory@example.com", null, true)], null);
            var created = await service.CreateAsync(new TenantId(tenantId), actorId, membershipId, request, Guid.CreateVersion7(), null, false, CancellationToken.None);
            Assert.Equal("شركة اختبار الدليل", created.DisplayName); Assert.Single(created.Identifiers); Assert.Single(created.Roles);
            await Assert.ThrowsAsync<DirectoryException>(() => service.CreateAsync(new TenantId(tenantId), actorId, membershipId, request, Guid.CreateVersion7(), null, false, CancellationToken.None));
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] { "delete from foundation_directory.party_identifier where tenant_id=$1", "delete from foundation_directory.contact_point where tenant_id=$1", "delete from foundation_directory.party_role where tenant_id=$1", "delete from foundation_directory.party_visibility where tenant_id=$1", "delete from foundation_directory.party where tenant_id=$1", "delete from platform.outbox_message where tenant_id=$1", "delete from platform.audit_entry where tenant_id=$1", "delete from platform.tenant where id=$1" }) { await using var cleanup = new NpgsqlCommand(sql, connection); cleanup.Parameters.AddWithValue(tenantId); await cleanup.ExecuteNonQueryAsync(); }
        }
    }

    private static async Task ResetRuntimePasswordAsync(string adminConnectionString)
    {
        await using var admin = NpgsqlDataSource.Create(adminConnectionString);
        await using var connection = await admin.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("alter role erp_app password 'todir-app-dev'", connection);
        await command.ExecuteNonQueryAsync();
    }
}
