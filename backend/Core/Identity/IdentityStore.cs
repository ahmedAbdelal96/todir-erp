using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

public interface IIdentityStore
{
    Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Identity?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    Task<bool> CompleteInitialPasswordChangeAsync(IdentityId identityId, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(IdentityId identityId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default);
}

public sealed class NullIdentityStore : IIdentityStore
{
    public Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<Identity?>(null);
    public Task<Identity?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<Identity?>(null);
    public Task<bool> CompleteInitialPasswordChangeAsync(IdentityId identityId, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> ChangePasswordAsync(IdentityId identityId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<Membership?>(null);
    public Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public sealed class NpgsqlIdentityStore(NpgsqlDataSource dataSource) : IIdentityStore
{
    private const string DummyPasswordHash = "pbkdf2-sha256$120000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = IdentityConventions.NormalizeEmail(email);
        var at = normalizedEmail.LastIndexOf('@');
        if (at <= 0 || at == normalizedEmail.Length - 1)
        {
            IdentityConventions.VerifyPassword(password, DummyPasswordHash);
            return null;
        }
        var domain = normalizedEmail[(at + 1)..];
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var route = new NpgsqlCommand("select tenant_id, status from platform_iam.resolve_login_domain($1)", connection, transaction);
        route.Parameters.AddWithValue(domain);
        await using var routeReader = await route.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await routeReader.ReadAsync(cancellationToken) || !string.Equals(routeReader.GetString(1), "Active", StringComparison.OrdinalIgnoreCase))
        {
            await routeReader.DisposeAsync();
            IdentityConventions.VerifyPassword(password, DummyPasswordHash);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var tenantId = new TenantId(routeReader.GetGuid(0));
        await routeReader.DisposeAsync();
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id, normalized_email, email_verified, status, created_at, last_login_at, security_stamp, password_hash, tenant_id, name, login_prefix, must_change_password, mfa_enabled, failed_login_count, locked_until from platform_iam.identity where normalized_email = $1 for update", connection, transaction);
        command.Parameters.AddWithValue(normalizedEmail);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            IdentityConventions.VerifyPassword(password, DummyPasswordHash);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var identity = ReadIdentity(reader);
        var lockedUntil = reader.IsDBNull(14) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(14);
        await reader.DisposeAsync();
        var now = DateTimeOffset.UtcNow;
        var locked = lockedUntil is not null && lockedUntil > now;
        var valid = identity.Status == IdentityStatus.Active && !locked && IdentityConventions.VerifyPassword(password, identity.PasswordHash ?? "");
        if (!valid)
        {
            if (identity.Status == IdentityStatus.Active && !locked)
            {
                await using var failure = new NpgsqlCommand("update platform_iam.identity set failed_login_count = least(failed_login_count + 1, $2), locked_until = case when failed_login_count + 1 >= $2 then $3 else locked_until end, version = version + 1 where id = $1", connection, transaction);
                failure.Parameters.AddWithValue(identity.Id.Value);
                failure.Parameters.AddWithValue(IdentityConventions.FailedLoginThreshold);
                failure.Parameters.AddWithValue(now.Add(IdentityConventions.LockoutDuration));
                await failure.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        await using (var success = new NpgsqlCommand("update platform_iam.identity set failed_login_count = 0, locked_until = null, last_login_at = $2, version = version + 1 where id = $1", connection, transaction))
        {
            success.Parameters.AddWithValue(identity.Id.Value);
            success.Parameters.AddWithValue(now);
            await success.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return identity with { LastLoginAt = now };
    }

    public async Task<Identity?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var tenantId = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken);
        if (tenantId is null) return null;
        await SetTenantAsync(connection, transaction, tenantId.Value, cancellationToken);
        await using var command = new NpgsqlCommand("select id, normalized_email, email_verified, status, created_at, last_login_at, security_stamp, password_hash, tenant_id, name, login_prefix, must_change_password, mfa_enabled from platform_iam.identity where id = $1 and status = 'Active' limit 1", connection, transaction);
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        var identity = await reader.ReadAsync(cancellationToken) ? ReadIdentity(reader) : null;
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return identity;
    }

    public async Task<bool> CompleteInitialPasswordChangeAsync(IdentityId identityId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(newPassword)) return false;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var tenantId = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken);
        if (tenantId is null) return false;
        await SetTenantAsync(connection, transaction, tenantId.Value, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.identity set password_hash = $1, must_change_password = false, security_stamp = $2, failed_login_count = 0, locked_until = null, version = version + 1 where id = $3 and tenant_id = $4 and status = 'Active' and must_change_password = true", connection, transaction);
        command.Parameters.AddWithValue(IdentityConventions.HashPassword(newPassword));
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(tenantId.Value.Value);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task<bool> ChangePasswordAsync(IdentityId identityId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!PasswordPolicy.IsValid(newPassword)) return false;
        var identity = await LoadActiveAsync(identityId, cancellationToken);
        if (identity is null || !IdentityConventions.VerifyPassword(currentPassword, identity.PasswordHash ?? "")) return false;
        if (identity.TenantId is null) return false;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, identity.TenantId.Value, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.identity set password_hash = $1, security_stamp = $2, version = version + 1 where id = $3 and tenant_id = $4 and status = 'Active'", connection, transaction);
        command.Parameters.AddWithValue(IdentityConventions.HashPassword(newPassword));
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(identity.TenantId.Value.Value);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            tenantCommand.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenantCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version from platform_iam.membership where identity_id = $1 and tenant_id = $2 and status = 'Active' limit 1", connection, transaction);
        command.Parameters.AddWithValue(identityId.Value); command.Parameters.AddWithValue(tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        var membership = await reader.ReadAsync(cancellationToken) ? ReadMembership(reader) : null;
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return membership;
    }

    public async Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            tenantCommand.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await tenantCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand("select 1 from platform_iam.identity i join platform_iam.membership m on m.identity_id = i.id where i.id = $1 and i.security_stamp = $2 and i.status = 'Active' and m.tenant_id = $3 and m.status = 'Active' limit 1", connection, transaction);
        command.Parameters.AddWithValue(identityId.Value);
        command.Parameters.AddWithValue(securityStamp);
        command.Parameters.AddWithValue(tenantId.Value);
        var valid = await command.ExecuteScalarAsync(cancellationToken) is not null;
        await transaction.CommitAsync(cancellationToken);
        return valid;
    }

    public async Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var tenantId = await ResolveTenantAsync(connection, transaction, identityId, cancellationToken);
        if (tenantId is null) return false;
        await SetTenantAsync(connection, transaction, tenantId.Value, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.identity set security_stamp = $1, version = version + 1 where id = $2 and status = 'Active'", connection, transaction);
        command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(identityId.Value);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<TenantId?> ResolveTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IdentityId identityId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select tenant_id, status from platform_iam.resolve_identity_tenant($1)", connection, transaction);
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !string.Equals(reader.GetString(1), "Active", StringComparison.OrdinalIgnoreCase)) return null;
        return new TenantId(reader.GetGuid(0));
    }

    private static Identity ReadIdentity(NpgsqlDataReader reader) => new(
        new IdentityId(reader.GetGuid(0)),
        reader.GetString(1),
        reader.GetBoolean(2),
        Enum.Parse<IdentityStatus>(reader.GetString(3), true),
        reader.GetFieldValue<DateTimeOffset>(4),
        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetGuid(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.FieldCount > 8 && !reader.IsDBNull(8) ? new TenantId(reader.GetGuid(8)) : null,
        reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null,
        reader.FieldCount > 10 && !reader.IsDBNull(10) ? reader.GetString(10) : null,
        reader.FieldCount > 11 && !reader.IsDBNull(11) && reader.GetBoolean(11),
        reader.FieldCount > 12 && !reader.IsDBNull(12) && reader.GetBoolean(12));
    private static Membership ReadMembership(NpgsqlDataReader reader) => new(new MembershipId(reader.GetGuid(0)), new TenantId(reader.GetGuid(1)), new IdentityId(reader.GetGuid(2)), reader.GetString(3), Enum.Parse<MembershipStatus>(reader.GetString(4), true), reader.IsDBNull(5) ? null : new LegalEntityId(reader.GetGuid(5)), reader.IsDBNull(6) ? null : new BranchId(reader.GetGuid(6)), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetInt64(10));
}
