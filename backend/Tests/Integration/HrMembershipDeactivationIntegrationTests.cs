using Core.Identity;
using Core.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrMembershipDeactivationIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task DeactivationRevokesOnlyRequestedMembershipAndInvalidatesExistingSessions()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var tenantId = Guid.Empty;
        var identityId = Guid.CreateVersion7();
        var otherIdentityId = Guid.CreateVersion7();
        var targetMembershipId = Guid.CreateVersion7();
        var otherMembershipId = Guid.CreateVersion7();
        var oldSecurityStamp = Guid.CreateVersion7();

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using (var findTenant = new NpgsqlCommand("select id from platform.tenant where slug='atlas-trading'", admin))
                tenantId = (Guid)(await findTenant.ExecuteScalarAsync() ?? throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed."));
            await using (var insertIdentity = new NpgsqlCommand("insert into platform.identity(id,normalized_email,email_verified,password_hash,status,created_at,security_stamp,version,tenant_id,email,name,login_prefix,must_change_password) values($1,$2,false,'not-used','Active',now(),$3,1,$4,$5,$6,$7,false),($8,$9,false,'not-used','Active',now(),$10,1,$4,$11,$12,$13,false)", admin))
            {
                insertIdentity.Parameters.AddWithValue(identityId); insertIdentity.Parameters.AddWithValue($"HR-LIFECYCLE-{identityId:N}@example.invalid");
                insertIdentity.Parameters.AddWithValue(oldSecurityStamp); insertIdentity.Parameters.AddWithValue(tenantId);
                insertIdentity.Parameters.AddWithValue($"hr-lifecycle-{identityId:N}@example.invalid"); insertIdentity.Parameters.AddWithValue("HR lifecycle target");
                insertIdentity.Parameters.AddWithValue($"hr-lifecycle-{identityId:N}"); insertIdentity.Parameters.AddWithValue(otherIdentityId);
                insertIdentity.Parameters.AddWithValue($"HR-LIFECYCLE-{otherIdentityId:N}@example.invalid"); insertIdentity.Parameters.AddWithValue(Guid.CreateVersion7());
                insertIdentity.Parameters.AddWithValue($"hr-lifecycle-{otherIdentityId:N}@example.invalid"); insertIdentity.Parameters.AddWithValue("HR lifecycle other");
                insertIdentity.Parameters.AddWithValue($"hr-lifecycle-{otherIdentityId:N}");
                await insertIdentity.ExecuteNonQueryAsync();
            }
            await using var insertMemberships = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($1,$3,$2,'HR lifecycle target','Active',now(),1),($4,$3,$5,'HR lifecycle other','Active',now(),1)", admin);
            insertMemberships.Parameters.AddWithValue(targetMembershipId); insertMemberships.Parameters.AddWithValue(identityId); insertMemberships.Parameters.AddWithValue(tenantId);
            insertMemberships.Parameters.AddWithValue(otherMembershipId); insertMemberships.Parameters.AddWithValue(otherIdentityId);
            await insertMemberships.ExecuteNonQueryAsync();
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var iam = new NpgsqlUserAccountAdminStore(dataSource, new NpgsqlAuditWriter(dataSource), TimeProvider.System);
        Assert.True(await iam.DeactivateMembershipAsync(new(tenantId), targetMembershipId, "RESIGNATION"));
        Assert.False(await iam.DeactivateMembershipAsync(new(tenantId), targetMembershipId, "RESIGNATION"));

        await using var verify = new NpgsqlConnection(adminConnectionString);
        await verify.OpenAsync();
        await using var query = new NpgsqlCommand("select m1.status,m2.status,i.security_stamp from platform.membership m1 join platform.membership m2 on m2.tenant_id=m1.tenant_id join platform.identity i on i.tenant_id=m1.tenant_id and i.id=m1.identity_id where m1.tenant_id=$1 and m1.id=$2 and m2.id=$3", verify);
        query.Parameters.AddWithValue(tenantId); query.Parameters.AddWithValue(targetMembershipId); query.Parameters.AddWithValue(otherMembershipId);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Revoked", reader.GetString(0));
        Assert.Equal("Active", reader.GetString(1));
        Assert.NotEqual(oldSecurityStamp, reader.GetGuid(2));
    }

    private sealed class HrPostgresFactAttribute : FactAttribute
    {
        public HrPostgresFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_TEST_DATABASE"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_TEST_DATABASE=1 only for a disposable PostgreSQL database with the canonical development seed.";
        }
    }
}
