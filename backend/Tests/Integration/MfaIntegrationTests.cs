using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Core.Identity;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class MfaIntegrationTests
{
    [Fact]
    public async Task PersistentMfaProtectsSecretAndConsumesRecoveryCodeOnce()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the MFA integration gate.");
        var password = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var identityId = IdentityId.New();
        var appConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Username = "erp_app", Password = password }.ConnectionString;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"alter role erp_app password '{password}'; set role erp_owner; insert into platform_iam.identity (id, normalized_email, email_verified, status, created_at, security_stamp, version) values ('{identityId.Value}', 'MFA-{identityId.Value:N}@EXAMPLE.COM', true, 'Active', now(), '{Guid.CreateVersion7()}', 1); reset role;");

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
            var manager = new MfaCredentialManager(new MfaService(), new NpgsqlMfaCredentialStore(dataSource), DataProtectionProvider.Create("kernel-integration-tests"));
            var enrollment = await manager.EnrollAsync(identityId);

            Assert.True(await manager.IsEnabledAsync(identityId));
            Assert.True(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
            Assert.False(await manager.VerifyAsync(identityId, enrollment.RecoveryCodes[0], DateTimeOffset.UtcNow));
            Assert.NotEqual(enrollment.SecretBase32, await ScalarAsync(admin, $"select mfa_secret_protected from platform_iam.identity where id = '{identityId.Value}'"));
            Assert.Equal(64, await ScalarIntAsync(admin, $"select length(digest) from platform_iam.mfa_recovery_code where identity_id = '{identityId.Value}'"));
        }
        finally
        {
            await ExecuteAsync(admin, $"set role erp_owner; delete from platform_iam.mfa_recovery_code where identity_id = '{identityId.Value}'; delete from platform_iam.identity where id = '{identityId.Value}'; reset role;");
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
