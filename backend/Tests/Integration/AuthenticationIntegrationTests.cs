using Core.Identity;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class AuthenticationIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task AuthenticationRoutesByDomainEnforcesLockoutAndUsesOpaqueSingleUseChallenges()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the authentication integration gate.");
        var appPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenant = TenantId.New();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        var loginDomain = $"auth-{tenant.Value:N}.test";
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = appPassword }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{appPassword}'; set role erp_owner; set app.tenant_id = '{tenant.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenant.Value}', 'auth-{tenant.Value:N}', 'Auth Fixture', 'Active', '{loginDomain}', '{loginDomain}', now(), 1); insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version) values ('{identityId.Value}', '{tenant.Value}', 'auth@{loginDomain}', 'AUTH@{loginDomain.ToUpperInvariant()}', 'auth', 'Auth User', true, '{IdentityConventions.HashPassword("correct")}', 'Active', now(), '{Guid.CreateVersion7()}', 1); insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ('{membershipId.Value}', '{tenant.Value}', '{identityId.Value}', 'Auth User', 'Active', now(), 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var store = new NpgsqlIdentityStore(dataSource);
            var authenticated = await store.AuthenticateAsync($" auth@{loginDomain} ", "correct");
            Assert.Equal(identityId, authenticated!.Id);
            Assert.Equal(tenant, authenticated.TenantId);

            for (var attempt = 0; attempt < IdentityConventions.FailedLoginThreshold; attempt++) Assert.Null(await store.AuthenticateAsync($"AUTH@{loginDomain.ToUpperInvariant()}", "wrong"));
            Assert.Null(await store.AuthenticateAsync($"AUTH@{loginDomain.ToUpperInvariant()}", "correct"));
            Assert.Null(await store.AuthenticateAsync("unknown@unknown.test", "correct"));

            var challenges = new NpgsqlAuthenticationChallengeStore(dataSource);
            var challenge = await challenges.CreateAsync(identityId, AuthenticationNextStep.VerifyMfa, DateTimeOffset.UtcNow.AddMinutes(5));
            var token = challenge.Token!;
            Assert.Null((await challenges.FindAsync(token))!.Token);
            Assert.True(await challenges.ConsumeAsync(token, identityId, AuthenticationNextStep.VerifyMfa));
            Assert.False(await challenges.ConsumeAsync(token, identityId, AuthenticationNextStep.VerifyMfa));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenant.Value}'; delete from platform.auth_challenge where identity_id = '{identityId.Value}'; delete from platform.membership where id = '{membershipId.Value}'; delete from platform.identity where id = '{identityId.Value}'; delete from platform.tenant where id = '{tenant.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
