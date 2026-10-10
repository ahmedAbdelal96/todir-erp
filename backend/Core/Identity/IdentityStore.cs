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
    Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthenticationAccessContext>> ListActiveAccessContextsAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionsAsync(IdentityId identityId, CancellationToken cancellationToken = default);
}

public sealed record MembershipInvitation(MembershipId MembershipId, string Token, DateTimeOffset ExpiresAt);
public interface IIdentityMembershipAdminStore
{
    Task<IReadOnlyList<Membership>> ListAsync(TenantId tenantId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default);
    Task<MembershipInvitation> InviteAsync(TenantId tenantId, IdentityId identityId, string displayName, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<bool> AcceptAsync(TenantId tenantId, string token, CancellationToken cancellationToken = default);
    Task<bool> SuspendAsync(TenantId tenantId, MembershipId membershipId, CancellationToken cancellationToken = default);
}

public sealed class NullIdentityStore : IIdentityStore
{
    public Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<Identity?>(null);
    public Task<Identity?> LoadActiveAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<Identity?>(null);
    public Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Membership>>([]);
    public Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Membership>>([]);
    public Task<IReadOnlyList<AuthenticationAccessContext>> ListActiveAccessContextsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuthenticationAccessContext>>([]);
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
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select id, normalized_email, email_verified, status, created_at, last_login_at, security_stamp, password_hash, failed_login_count, locked_until from platform_iam.identity where normalized_email = $1 for update", connection, transaction);
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
        var lockedUntil = reader.IsDBNull(9) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(9);
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
        await using var command = dataSource.CreateCommand("select id, normalized_email, email_verified, status, created_at, last_login_at, security_stamp, password_hash from platform_iam.identity where id = $1 and status = 'Active' limit 1");
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadIdentity(reader) : null;
    }

    public async Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version from platform_iam.membership where identity_id = $1 and tenant_id = $2 and status = 'Active' order by created_at", connection, transaction);
        command.Parameters.AddWithValue(identityId.Value); command.Parameters.AddWithValue(tenantId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memberships = new List<Membership>();
        while (await reader.ReadAsync(cancellationToken)) memberships.Add(ReadMembership(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return memberships;
    }

    public async Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version from platform_iam.list_active_memberships($1) order by created_at, id");
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memberships = new List<Membership>();
        while (await reader.ReadAsync(cancellationToken)) memberships.Add(ReadMembership(reader));
        return memberships;
    }

    public async Task<IReadOnlyList<AuthenticationAccessContext>> ListActiveAccessContextsAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version, organization_name from platform_iam.list_active_access_contexts($1) order by organization_name, created_at, id");
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var contexts = new List<AuthenticationAccessContext>();
        while (await reader.ReadAsync(cancellationToken)) contexts.Add(new(ReadMembership(reader), reader.GetString(11)));
        return contexts;
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
        await using var command = dataSource.CreateCommand("update platform_iam.identity set security_stamp = $1, version = version + 1 where id = $2 and status = 'Active'");
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(identityId.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Identity ReadIdentity(NpgsqlDataReader reader) => new(new IdentityId(reader.GetGuid(0)), reader.GetString(1), reader.GetBoolean(2), Enum.Parse<IdentityStatus>(reader.GetString(3), true), reader.GetFieldValue<DateTimeOffset>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5), reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7));
    private static Membership ReadMembership(NpgsqlDataReader reader) => new(new MembershipId(reader.GetGuid(0)), new TenantId(reader.GetGuid(1)), new IdentityId(reader.GetGuid(2)), reader.GetString(3), Enum.Parse<MembershipStatus>(reader.GetString(4), true), reader.IsDBNull(5) ? null : new LegalEntityId(reader.GetGuid(5)), reader.IsDBNull(6) ? null : new BranchId(reader.GetGuid(6)), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetInt64(10));
}

public sealed class NpgsqlIdentityMembershipAdminStore(NpgsqlDataSource dataSource) : IIdentityMembershipAdminStore
{
    public async Task<IReadOnlyList<Membership>> ListAsync(TenantId tenantId, int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "membership.pagination_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version from platform_iam.membership where tenant_id = $1 order by created_at, id limit $2 offset $3", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(limit);
        command.Parameters.AddWithValue(offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memberships = new List<Membership>();
        while (await reader.ReadAsync(cancellationToken)) memberships.Add(ReadMembership(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return memberships;
    }

    public async Task<MembershipInvitation> InviteAsync(TenantId tenantId, IdentityId identityId, string displayName, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        if (expiresAt <= DateTimeOffset.UtcNow) throw new ArgumentOutOfRangeException(nameof(expiresAt), "identity.invitation_expiry_required");
        var membershipId = MembershipId.New();
        var invitationId = Guid.CreateVersion7();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using (var membership = new NpgsqlCommand("insert into platform_iam.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ($1, $2, $3, $4, 'Invited', now(), 1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId.Value); membership.Parameters.AddWithValue(tenantId.Value); membership.Parameters.AddWithValue(identityId.Value); membership.Parameters.AddWithValue(displayName);
            await membership.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var invitation = new NpgsqlCommand("insert into platform_iam.membership_invitation (id, tenant_id, identity_id, token_digest, expires_at, created_at) values ($1, $2, $3, $4, $5, now())", connection, transaction))
        {
            invitation.Parameters.AddWithValue(invitationId); invitation.Parameters.AddWithValue(tenantId.Value); invitation.Parameters.AddWithValue(identityId.Value); invitation.Parameters.AddWithValue(digest); invitation.Parameters.AddWithValue(expiresAt);
            await invitation.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new MembershipInvitation(membershipId, token, expiresAt);
    }

    public async Task<bool> AcceptAsync(TenantId tenantId, string token, CancellationToken cancellationToken = default)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("with invitation as (select identity_id from platform_iam.membership_invitation where tenant_id = $1 and token_digest = $2 and accepted_at is null and expires_at > now()) update platform_iam.membership membership set status = 'Active', version = version + 1 from invitation where membership.tenant_id = $1 and membership.identity_id = invitation.identity_id and membership.status = 'Invited'", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(digest);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        if (changed)
        {
            await using var accepted = new NpgsqlCommand("update platform_iam.membership_invitation set accepted_at = now() where tenant_id = $1 and token_digest = $2 and accepted_at is null", connection, transaction);
            accepted.Parameters.AddWithValue(tenantId.Value); accepted.Parameters.AddWithValue(digest);
            await accepted.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task<bool> SuspendAsync(TenantId tenantId, MembershipId membershipId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.membership set status = 'Suspended', version = version + 1 where tenant_id = $1 and id = $2 and status = 'Active'", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(membershipId.Value);
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

    private static Membership ReadMembership(NpgsqlDataReader reader) => new(new MembershipId(reader.GetGuid(0)), new TenantId(reader.GetGuid(1)), new IdentityId(reader.GetGuid(2)), reader.GetString(3), Enum.Parse<MembershipStatus>(reader.GetString(4), true), reader.IsDBNull(5) ? null : new LegalEntityId(reader.GetGuid(5)), reader.IsDBNull(6) ? null : new BranchId(reader.GetGuid(6)), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetInt64(10));
}
