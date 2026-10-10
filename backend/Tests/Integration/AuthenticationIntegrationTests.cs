using Core.Identity;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class AuthenticationIntegrationTests
{
    [Fact]
    public async Task AuthenticationUsesGlobalIdentityLockoutAndOpaqueSingleUseChallenges()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the authentication integration gate.");
        var appPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var identityId = IdentityId.New();
        var firstMembership = MembershipId.New();
        var secondMembership = MembershipId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenantA.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantA.Value}', 'auth-{tenantA.Value:N}', 'Alpha Organization', 'Active', now(), 1); set app.tenant_id = '{tenantB.Value}'; insert into platform_tenancy.tenant (id, slug, display_name, status, created_at, version) values ('{tenantB.Value}', 'auth-{tenantB.Value:N}', 'Beta Organization', 'Active', now(), 1); reset app.tenant_id; insert into platform_iam.identity (id, normalized_email, email_verified, status, password_hash, created_at, security_stamp, version) values ('{identityId.Value}', 'AUTH@EXAMPLE.COM', true, 'Active', '{IdentityConventions.HashPassword("correct")}', now(), '{Guid.CreateVersion7()}', 1); set app.tenant_id = '{tenantA.Value}'; insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{firstMembership.Value}', '{tenantA.Value}', '{identityId.Value}', 'Alpha member', 'Active', now(), 1); set app.tenant_id = '{tenantB.Value}'; insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{secondMembership.Value}', '{tenantB.Value}', '{identityId.Value}', 'Beta member', 'Active', now(), 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var store = new NpgsqlIdentityStore(dataSource);
            var authenticated = await store.AuthenticateAsync(" auth@example.com ", "correct");
            Assert.Equal(identityId, authenticated!.Id);
            var contexts = await store.ListActiveAccessContextsAsync(identityId);
            Assert.Equal(["Alpha Organization", "Beta Organization"], contexts.Select(value => value.OrganizationName).OrderBy(value => value).ToArray());

            for (var attempt = 0; attempt < IdentityConventions.FailedLoginThreshold; attempt++) Assert.Null(await store.AuthenticateAsync("AUTH@EXAMPLE.COM", "wrong"));
            Assert.Null(await store.AuthenticateAsync("AUTH@EXAMPLE.COM", "correct"));

            var challenges = new NpgsqlAuthenticationChallengeStore(dataSource);
            var challenge = await challenges.CreateAsync(identityId, AuthenticationNextStep.SelectOrganization, DateTimeOffset.UtcNow.AddMinutes(5));
            var token = challenge.Token!;
            Assert.Null((await challenges.FindAsync(token))!.Token);
            Assert.True(await challenges.ConsumeAsync(token, identityId, AuthenticationNextStep.SelectOrganization));
            Assert.False(await challenges.ConsumeAsync(token, identityId, AuthenticationNextStep.SelectOrganization));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantA.Value}'; delete from platform_iam.membership where id = '{firstMembership.Value}'; set app.tenant_id = '{tenantB.Value}'; delete from platform_iam.membership where id = '{secondMembership.Value}'; reset app.tenant_id; delete from platform_iam.auth_challenge where identity_id = '{identityId.Value}'; delete from platform_iam.identity where id = '{identityId.Value}'; set app.tenant_id = '{tenantA.Value}'; delete from platform_tenancy.tenant where id = '{tenantA.Value}'; set app.tenant_id = '{tenantB.Value}'; delete from platform_tenancy.tenant where id = '{tenantB.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
