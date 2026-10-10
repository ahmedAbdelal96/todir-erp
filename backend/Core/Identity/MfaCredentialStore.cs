using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using System.Security.Cryptography;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public sealed record StoredMfaCredential(string ProtectedSecret, IReadOnlyList<string> RecoveryCodeDigests);

public sealed class MfaStepUpProof
{
    internal MfaStepUpProof(IdentityId identityId, DateTimeOffset expiresAt)
    {
        IdentityId = identityId;
        ExpiresAt = expiresAt;
    }

    public IdentityId IdentityId { get; }
    public DateTimeOffset ExpiresAt { get; }
    internal bool IsValid(TimeProvider timeProvider) => timeProvider.GetUtcNow() < ExpiresAt;
}

public interface IMfaCredentialStore
{
    Task<StoredMfaCredential?> FindAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    Task SaveAsync(IdentityId identityId, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, CancellationToken cancellationToken = default);
    Task<bool> ConsumeRecoveryCodeAsync(IdentityId identityId, string digest, CancellationToken cancellationToken = default);
}

public sealed class InMemoryMfaCredentialStore : IMfaCredentialStore
{
    private readonly Dictionary<IdentityId, (string Secret, HashSet<string> RecoveryCodes)> credentials = [];
    private readonly object gate = new();

    public Task<StoredMfaCredential?> FindAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) return Task.FromResult(credentials.TryGetValue(identityId, out var value)
            ? new StoredMfaCredential(value.Secret, value.RecoveryCodes.ToArray())
            : null);
    }

    public Task SaveAsync(IdentityId identityId, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) credentials[identityId] = (protectedSecret, recoveryCodeDigests.ToHashSet(StringComparer.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<bool> ConsumeRecoveryCodeAsync(IdentityId identityId, string digest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!credentials.TryGetValue(identityId, out var value)) return Task.FromResult(false);
            var consumed = value.RecoveryCodes.Remove(digest);
            return Task.FromResult(consumed);
        }
    }
}

public sealed class NpgsqlMfaCredentialStore(NpgsqlDataSource dataSource) : IMfaCredentialStore
{
    public async Task<StoredMfaCredential?> FindAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select mfa_enabled, mfa_secret_protected from platform_iam.identity where id = $1", connection);
        command.Parameters.AddWithValue(identityId.Value);
        await using var identityReader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        if (!await identityReader.ReadAsync(cancellationToken) || !identityReader.GetBoolean(0)) return null;
        var protectedSecret = identityReader.IsDBNull(1) ? string.Empty : identityReader.GetString(1);
        await identityReader.DisposeAsync();

        await using var recovery = new NpgsqlCommand("select digest from platform_iam.mfa_recovery_code where identity_id = $1 and consumed_at is null order by digest", connection);
        recovery.Parameters.AddWithValue(identityId.Value);
        var digests = new List<string>();
        await using var reader = await recovery.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) digests.Add(reader.GetString(0));
        return new StoredMfaCredential(protectedSecret, digests);
    }

    public async Task SaveAsync(IdentityId identityId, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var identity = new NpgsqlCommand("update platform_iam.identity set mfa_secret_protected = $1, mfa_enabled = true, version = version + 1 where id = $2 and status = 'Active'", connection, transaction))
        {
            identity.Parameters.AddWithValue(protectedSecret); identity.Parameters.AddWithValue(identityId.Value);
            if (await identity.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("identity.mfa_enrollment_not_allowed");
        }
        await using (var delete = new NpgsqlCommand("delete from platform_iam.mfa_recovery_code where identity_id = $1", connection, transaction))
        {
            delete.Parameters.AddWithValue(identityId.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var digest in recoveryCodeDigests.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using var insert = new NpgsqlCommand("insert into platform_iam.mfa_recovery_code (identity_id, digest, created_at) values ($1, $2, now())", connection, transaction);
            insert.Parameters.AddWithValue(identityId.Value); insert.Parameters.AddWithValue(digest);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ConsumeRecoveryCodeAsync(IdentityId identityId, string digest, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.mfa_recovery_code set consumed_at = now() where identity_id = $1 and digest = $2 and consumed_at is null", connection);
        command.Parameters.AddWithValue(identityId.Value); command.Parameters.AddWithValue(digest);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}

public sealed class MfaCredentialManager(IMfaService service, IMfaCredentialStore store, IDataProtectionProvider protectionProvider)
{
    private readonly IDataProtector protector = protectionProvider.CreateProtector("platform-mfa-secret-v1");

    public async Task<MfaEnrollment> EnrollAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        var enrollment = service.CreateEnrollment();
        await store.SaveAsync(identityId, protector.Protect(enrollment.SecretBase32), enrollment.RecoveryCodeDigests, cancellationToken);
        return enrollment;
    }

    public async Task<bool> IsEnabledAsync(IdentityId identityId, CancellationToken cancellationToken = default) => await store.FindAsync(identityId, cancellationToken) is not null;

    public async Task<bool> VerifyAsync(IdentityId identityId, string? code, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await VerifyCodeAsync(identityId, code, now, cancellationToken);
    }

    public async Task<MfaStepUpProof?> VerifyStepUpAsync(IdentityId identityId, string? code, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return await VerifyCodeAsync(identityId, code, now, cancellationToken)
            ? new MfaStepUpProof(identityId, now.AddMinutes(5))
            : null;
    }

    private async Task<bool> VerifyCodeAsync(IdentityId identityId, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var credential = await store.FindAsync(identityId, cancellationToken);
        if (credential is null) return false;
        try
        {
            if (service.VerifyTotp(protector.Unprotect(credential.ProtectedSecret), code, now)) return true;
        }
        catch (CryptographicException) { }

        var digest = service.DigestRecoveryCode(code);
        return await store.ConsumeRecoveryCodeAsync(identityId, digest, cancellationToken);
    }
}
