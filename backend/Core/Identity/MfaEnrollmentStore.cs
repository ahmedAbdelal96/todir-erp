using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public sealed record PendingMfaEnrollment(Guid EnrollmentId, IdentityId IdentityId, string ProtectedSecret, IReadOnlyList<string> RecoveryCodeDigests, DateTimeOffset ExpiresAt);

public interface IMfaEnrollmentStore
{
    Task CreateAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<PendingMfaEnrollment?> FindAsync(IdentityId identityId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> ConsumeAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class InMemoryMfaEnrollmentStore(IMfaCredentialStore credentials) : IMfaEnrollmentStore
{
    private readonly Dictionary<Guid, (IdentityId IdentityId, string TokenHash, string ProtectedSecret, IReadOnlyList<string> Digests, DateTimeOffset ExpiresAt)> pending = [];
    private readonly object gate = new();
    public Task CreateAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) pending[enrollmentId] = (identityId, tokenHash, protectedSecret, recoveryCodeDigests.ToArray(), expiresAt); return Task.CompletedTask; }
    public Task<PendingMfaEnrollment?> FindAsync(IdentityId identityId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) { foreach (var pair in pending) if (pair.Value.IdentityId == identityId && pair.Value.TokenHash == tokenHash && pair.Value.ExpiresAt > now) return Task.FromResult<PendingMfaEnrollment?>(new(pair.Key, identityId, pair.Value.ProtectedSecret, pair.Value.Digests, pair.Value.ExpiresAt)); return Task.FromResult<PendingMfaEnrollment?>(null); } }
    public Task<bool> ConsumeAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); lock (gate) { if (!pending.TryGetValue(enrollmentId, out var value) || value.IdentityId != identityId || value.TokenHash != tokenHash || value.ExpiresAt <= now) return Task.FromResult(false); credentials.SaveAsync(identityId, value.ProtectedSecret, value.Digests, cancellationToken).GetAwaiter().GetResult(); pending.Remove(enrollmentId); return Task.FromResult(true); } }
}

public sealed class NpgsqlMfaEnrollmentStore(NpgsqlDataSource dataSource) : IMfaEnrollmentStore
{
    public async Task CreateAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, string protectedSecret, IReadOnlyList<string> recoveryCodeDigests, DateTimeOffset createdAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); var tenant = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken) ?? throw new InvalidOperationException("identity.mfa_enrollment_not_allowed"); await SetTenantAsync(connection, transaction, tenant, cancellationToken);
        await using var command = new NpgsqlCommand("insert into platform.mfa_enrollment_challenge (id, tenant_id, identity_id, token_hash, protected_secret, recovery_code_digests, created_at, expires_at, status) values ($1, $2, $3, $4, $5, $6, $7, $8, 'Pending')", connection, transaction); command.Parameters.AddWithValue(enrollmentId); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(identityId.Value); command.Parameters.AddWithValue(tokenHash); command.Parameters.AddWithValue(protectedSecret); command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = recoveryCodeDigests.ToArray() }); command.Parameters.AddWithValue(createdAt); command.Parameters.AddWithValue(expiresAt); await command.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PendingMfaEnrollment?> FindAsync(IdentityId identityId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); var tenant = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken); if (tenant is null) return null; await SetTenantAsync(connection, transaction, tenant.Value, cancellationToken);
        await using var command = new NpgsqlCommand("select id, protected_secret, recovery_code_digests, expires_at from platform.mfa_enrollment_challenge where identity_id = $1 and token_hash = $2 and status = 'Pending' and expires_at > $3 order by created_at desc limit 1", connection, transaction); command.Parameters.AddWithValue(identityId.Value); command.Parameters.AddWithValue(tokenHash); command.Parameters.AddWithValue(now); await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null; var result = new PendingMfaEnrollment(reader.GetGuid(0), identityId, reader.GetString(1), reader.GetFieldValue<string[]>(2), reader.GetFieldValue<DateTimeOffset>(3)); await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<bool> ConsumeAsync(IdentityId identityId, Guid enrollmentId, string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); var tenant = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken); if (tenant is null) return false; await SetTenantAsync(connection, transaction, tenant.Value, cancellationToken);
        await using var select = new NpgsqlCommand("select protected_secret, recovery_code_digests from platform.mfa_enrollment_challenge where id = $1 and identity_id = $2 and token_hash = $3 and status = 'Pending' and expires_at > $4 for update", connection, transaction); select.Parameters.AddWithValue(enrollmentId); select.Parameters.AddWithValue(identityId.Value); select.Parameters.AddWithValue(tokenHash); select.Parameters.AddWithValue(now); await using var reader = await select.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return false; var secret = reader.GetString(0); var digests = reader.GetFieldValue<string[]>(1); await reader.DisposeAsync();
        await using var update = new NpgsqlCommand("update platform.mfa_enrollment_challenge set status = 'Confirmed', confirmed_at = $3 where id = $1 and identity_id = $2 and status = 'Pending'", connection, transaction); update.Parameters.AddWithValue(enrollmentId); update.Parameters.AddWithValue(identityId.Value); update.Parameters.AddWithValue(now); if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        await using var identity = new NpgsqlCommand("update platform.identity set mfa_secret_protected = $1, mfa_enabled = true, version = version + 1 where id = $2 and tenant_id = $3 and status = 'Active'", connection, transaction); identity.Parameters.AddWithValue(secret); identity.Parameters.AddWithValue(identityId.Value); identity.Parameters.AddWithValue(tenant.Value.Value); if (await identity.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        await using (var delete = new NpgsqlCommand("delete from platform.mfa_recovery_code where identity_id = $1", connection, transaction)) { delete.Parameters.AddWithValue(identityId.Value); await delete.ExecuteNonQueryAsync(cancellationToken); }
        foreach (var digest in digests.Distinct(StringComparer.OrdinalIgnoreCase)) { await using var insert = new NpgsqlCommand("insert into platform.mfa_recovery_code (identity_id, digest, created_at) values ($1, $2, $3)", connection, transaction); insert.Parameters.AddWithValue(identityId.Value); insert.Parameters.AddWithValue(digest); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken); return true;
    }

    private static async Task<TenantId?> ResolveTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IdentityId identityId, CancellationToken cancellationToken)
    { await using var command = new NpgsqlCommand("select tenant_id, status from platform.resolve_identity_tenant($1)", connection, transaction); command.Parameters.AddWithValue(identityId.Value); await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken); if (!await reader.ReadAsync(cancellationToken) || !string.Equals(reader.GetString(1), "Active", StringComparison.OrdinalIgnoreCase)) return null; var tenant = new TenantId(reader.GetGuid(0)); await reader.DisposeAsync(); return tenant; }
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    { await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken); }
}
