using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class MfaIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task PersistentMfaProtectsSecretAndConsumesRecoveryCodeOnce()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the MFA integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var tenantId = TenantId.New();
        var identityId = IdentityId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; set app.tenant_id = '{tenantId.Value}'; insert into platform.tenant (id, slug, display_name, status, login_domain, normalized_login_domain, created_at, version) values ('{tenantId.Value}', 'mfa-{tenantId.Value:N}', 'MFA', 'Active', 'mfa-{tenantId.Value:N}.local', 'mfa-{tenantId.Value:N}.local', now(), 1); insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, status, created_at, security_stamp, version) values ('{identityId.Value}', '{tenantId.Value}', 'mfa-{identityId.Value:N}@mfa-{tenantId.Value:N}.local', 'MFA-{identityId.Value:N}@MFA-{tenantId.Value:N}.LOCAL', 'mfa-{identityId.Value:N}', 'MFA User', true, 'Active', now(), '{Guid.CreateVersion7()}', 1); reset app.tenant_id; reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var manager = new MfaCredentialManager(new MfaService(), new NpgsqlMfaCredentialStore(dataSource), DataProtectionProvider.Create("kernel-integration-tests"));
            var enrollment = await manager.EnrollAsync(identityId);

            Assert.True(await manager.IsEnabledAsync(identityId));
            Assert.True(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
            Assert.False(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
            Assert.NotEqual(enrollment.SecretBase32, await ScalarAsync(admin, $"select mfa_secret_protected from platform.identity where id = '{identityId.Value}'"));
            Assert.Equal(64, await ScalarIntAsync(admin, $"select length(digest) from platform.mfa_recovery_code where identity_id = '{identityId.Value}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; set app.tenant_id = '{tenantId.Value}'; delete from platform.mfa_recovery_code where identity_id = '{identityId.Value}'; delete from platform.identity where id = '{identityId.Value}'; delete from platform.tenant where id = '{tenantId.Value}'; reset app.tenant_id; reset role;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<int> ScalarIntAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
