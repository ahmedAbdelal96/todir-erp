using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Identity;

/// <summary>
/// The bounded server-side session policy. The values are deliberately short and
/// explicit: browser activity can keep an active session alive, but never past
/// the reauthentication deadline.
/// </summary>
public sealed record SessionPolicy(
    TimeSpan InactivityTimeout,
    TimeSpan ReauthenticationTimeout,
    TimeSpan ActivityWriteInterval,
    TimeSpan MfaFreshness)
{
    public static SessionPolicy Default { get; } = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(24), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    public TimeSpan MobileSessionLifetime { get; init; } = TimeSpan.FromDays(30);

    public static SessionPolicy FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var policy = new SessionPolicy(
            Read(configuration, "TODIR_SESSION_INACTIVITY_MINUTES", Default.InactivityTimeout.TotalMinutes),
            Read(configuration, "TODIR_SESSION_REAUTHENTICATION_HOURS", Default.ReauthenticationTimeout.TotalHours, true),
            Read(configuration, "TODIR_SESSION_ACTIVITY_WRITE_SECONDS", Default.ActivityWriteInterval.TotalSeconds, false, true),
            Read(configuration, "TODIR_SESSION_MFA_FRESHNESS_MINUTES", Default.MfaFreshness.TotalMinutes));
        policy = policy with { MobileSessionLifetime = Read(configuration, "TODIR_MOBILE_SESSION_DAYS", Default.MobileSessionLifetime.TotalDays) };
        if (policy.InactivityTimeout <= TimeSpan.Zero || policy.ReauthenticationTimeout <= policy.InactivityTimeout || policy.ActivityWriteInterval <= TimeSpan.Zero || policy.MfaFreshness <= TimeSpan.Zero || policy.MobileSessionLifetime <= TimeSpan.Zero)
            throw new InvalidOperationException("identity.session_policy_invalid");
        return policy;
    }

    private static TimeSpan Read(Microsoft.Extensions.Configuration.IConfiguration configuration, string key, double fallback, bool hours = false, bool seconds = false)
    {
        if (!double.TryParse(configuration[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) value = fallback;
        if (!double.IsFinite(value) || value <= 0) throw new InvalidOperationException("identity.session_policy_invalid");
        return hours ? TimeSpan.FromHours(value) : seconds ? TimeSpan.FromSeconds(value) : TimeSpan.FromMinutes(value);
    }
}

public sealed class NpgsqlSessionService(NpgsqlDataSource dataSource, TimeProvider clock, SessionPolicy policy) : ISessionService
{
    public Task<SessionContext?> CreateAsync(Identity identity, Membership membership, CancellationToken cancellationToken = default) => CreateAsync(identity, membership, null, cancellationToken);

    public async Task<SessionContext?> CreateAsync(Identity identity, Membership membership, DateTimeOffset? mfaVerifiedAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Status != IdentityStatus.Active || membership.Status != MembershipStatus.Active || identity.TenantId != membership.TenantId) return null;
        var now = clock.GetUtcNow();
        var sessionId = Guid.CreateVersion7();
        var reauthenticationExpiresAt = now.Add(policy.ReauthenticationTimeout);
        var inactivityExpiresAt = Min(now.Add(policy.InactivityTimeout), reauthenticationExpiresAt);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, membership.TenantId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into platform.web_session
                (id, tenant_id, identity_id, membership_id, created_at, last_activity_at,
                 inactivity_expires_at, reauthentication_expires_at, security_stamp,
                 mfa_verified_at, status)
            values ($1, $2, $3, $4, $5, $5, $6, $7, $8, $9, 'Active')
            """, connection, transaction);
        command.Parameters.AddWithValue(sessionId);
        command.Parameters.AddWithValue(membership.TenantId.Value);
        command.Parameters.AddWithValue(identity.Id.Value);
        command.Parameters.AddWithValue(membership.Id.Value);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(inactivityExpiresAt);
        command.Parameters.AddWithValue(reauthenticationExpiresAt);
        command.Parameters.AddWithValue(identity.SecurityStamp);
        command.Parameters.AddWithValue((object?)mfaVerifiedAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SessionContext(identity.Id, membership.Id, membership.TenantId, membership.DefaultLegalEntityId, membership.DefaultBranchId, now, inactivityExpiresAt, identity.SecurityStamp, sessionId, now, reauthenticationExpiresAt, mfaVerifiedAt);
    }

    public async Task<SessionContext?> LoadAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand("""
                select s.membership_id, s.created_at, s.last_activity_at, s.inactivity_expires_at,
                       s.reauthentication_expires_at, s.security_stamp, s.mfa_verified_at,
                       m.default_legal_entity_id, m.default_branch_id
                  from platform.web_session s
                  join platform.identity i on i.tenant_id = s.tenant_id and i.id = s.identity_id
                  join platform.membership m on m.tenant_id = s.tenant_id and m.id = s.membership_id and m.identity_id = s.identity_id
                 where s.id = $1 and s.tenant_id = $2 and s.identity_id = $3
                   and s.status = 'Active' and s.security_stamp = i.security_stamp
                   and i.status = 'Active' and m.status = 'Active'
                 limit 1
                """, connection, transaction);
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(identityId.Value);
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            var session = new SessionContext(
                identityId,
                new MembershipId(reader.GetGuid(0)),
                tenantId,
                reader.IsDBNull(7) ? null : new LegalEntityId(reader.GetGuid(7)),
                reader.IsDBNull(8) ? null : new BranchId(reader.GetGuid(8)),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetGuid(5),
                sessionId,
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return session;
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    public async Task<bool> ValidateAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default)
    {
        var session = await LoadAsync(sessionId, identityId, tenantId, cancellationToken);
        return session is not null && session.SecurityStamp == securityStamp && IsValid(session, clock.GetUtcNow());
    }

    public async Task<bool> AcceptActivityAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = clock.GetUtcNow();
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand("""
                update platform.web_session
                   set last_activity_at = $4,
                       inactivity_expires_at = least($5, reauthentication_expires_at)
                 where id = $1 and tenant_id = $2 and identity_id = $3 and status = 'Active'
                   and last_activity_at <= $4 - $6::interval
                   and inactivity_expires_at > $4 and reauthentication_expires_at > $4
                """, connection, transaction);
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(identityId.Value);
            command.Parameters.AddWithValue(now);
            command.Parameters.AddWithValue(now.Add(policy.InactivityTimeout));
            command.Parameters.AddWithValue(policy.ActivityWriteInterval.ToString("c", System.Globalization.CultureInfo.InvariantCulture));
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed == 1 || await IsStillValidAsync(sessionId, identityId, tenantId, now, cancellationToken);
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public async Task<bool> MarkMfaVerifiedAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, DateTimeOffset verifiedAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
            await using var command = new NpgsqlCommand("""
                update platform.web_session
                   set mfa_verified_at = $4
                 where id = $1 and tenant_id = $2 and identity_id = $3 and status = 'Active'
                   and inactivity_expires_at > $4 and reauthentication_expires_at > $4
                """, connection, transaction);
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue(identityId.Value);
            command.Parameters.AddWithValue(verifiedAt);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    public async Task<bool> RevokeAsync(Guid sessionId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.web_session set status = 'Revoked', revoked_at = now(), revoked_reason = 'logout' where id = $1 and tenant_id = $2 and status = 'Active'", connection, transaction);
        command.Parameters.AddWithValue(sessionId); command.Parameters.AddWithValue(tenantId.Value);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task RevokeAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var resolve = new NpgsqlCommand("select tenant_id from platform.identity where id = $1 limit 1", connection, transaction);
        resolve.Parameters.AddWithValue(identityId.Value);
        var tenant = await resolve.ExecuteScalarAsync(cancellationToken);
        if (tenant is not Guid tenantId) return;
        await SetTenantAsync(connection, transaction, new TenantId(tenantId), cancellationToken);
        await using var command = new NpgsqlCommand("update platform.web_session set status = 'Revoked', revoked_at = now(), revoked_reason = 'identity_revoke' where tenant_id = $1 and identity_id = $2 and status = 'Active'", connection, transaction);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(identityId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public bool IsValid(SessionContext session, DateTimeOffset now) => now < session.ExpiresAt && (session.ReauthenticationExpiresAt is null || now < session.ReauthenticationExpiresAt);

    private async Task<bool> IsStillValidAsync(Guid sessionId, IdentityId identityId, TenantId tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await LoadAsync(sessionId, identityId, tenantId, cancellationToken);
        return session is not null && IsValid(session, now);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
