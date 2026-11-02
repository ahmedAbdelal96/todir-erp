using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;
using Core.Services;

namespace Core.Identity;

public sealed record UserAccountSummary(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string LoginPrefix,
    string Status,
    bool MustChangePassword,
    DateTimeOffset CreatedAt);

public sealed record CreatedUserAccount(UserAccountSummary User, string TemporaryPassword);
public sealed record UserAccountPage(IReadOnlyList<UserAccountSummary> Items, int TotalCount, int Page, int PageSize);

public interface IUserAccountAdminStore
{
    Task<CreatedUserAccount> CreateAsync(TenantId tenantId, string name, string emailPrefix, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<UserAccountPage> ListAsync(TenantId tenantId, string? search, bool? active, int offset, int limit, CancellationToken cancellationToken = default);
    Task<UserAccountSummary?> FindAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default);
    Task<bool> RenameAsync(TenantId tenantId, IdentityId identityId, string name, CancellationToken cancellationToken = default);
    Task<bool> RenameLoginPrefixAsync(TenantId tenantId, IdentityId identityId, string emailPrefix, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(TenantId tenantId, IdentityId identityId, bool active, CancellationToken cancellationToken = default);
    Task<string?> ResetPasswordAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default);
    Task<string> ChangeLoginDomainAsync(TenantId tenantId, string loginDomain, CancellationToken cancellationToken = default);
    Task<string?> LoadLoginDomainAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class NullUserAccountAdminStore : IUserAccountAdminStore
{
    public Task<CreatedUserAccount> CreateAsync(TenantId tenantId, string name, string emailPrefix, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default) => throw new InvalidOperationException("identity.storage_unavailable");
    public Task<UserAccountPage> ListAsync(TenantId tenantId, string? search, bool? active, int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult(new UserAccountPage([], 0, offset / Math.Max(1, limit) + 1, limit));
    public Task<UserAccountSummary?> FindAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<UserAccountSummary?>(null);
    public Task<bool> RenameAsync(TenantId tenantId, IdentityId identityId, string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> RenameLoginPrefixAsync(TenantId tenantId, IdentityId identityId, string emailPrefix, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<bool> SetActiveAsync(TenantId tenantId, IdentityId identityId, bool active, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<string?> ResetPasswordAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task<string> ChangeLoginDomainAsync(TenantId tenantId, string loginDomain, CancellationToken cancellationToken = default) => throw new InvalidOperationException("identity.storage_unavailable");
    public Task<string?> LoadLoginDomainAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

public sealed class NpgsqlUserAccountAdminStore(NpgsqlDataSource dataSource, IAuditWriter audit, TimeProvider clock) : IUserAccountAdminStore
{
    public async Task<CreatedUserAccount> CreateAsync(TenantId tenantId, string name, string emailPrefix, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var cleanName = ValidateName(name);
        var prefix = LoginDomainConventions.NormalizePrefix(emailPrefix);
        var password = CreateTemporaryPassword();
        var identityId = IdentityId.New();
        var membershipId = MembershipId.New();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var domain = await LoadTenantForUpdateAsync(connection, transaction, tenantId, cancellationToken);
        if (domain is null) throw new InvalidOperationException("tenant.not_found");
        if (!string.Equals(domain.Value.Status, "Active", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("tenant.unavailable");
        var email = LoginDomainConventions.BuildEmail(prefix, domain.Value.Domain);
        try
        {
            await using (var identity = new NpgsqlCommand("insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, must_change_password, status, created_at, security_stamp, version) values ($1, $2, $3, $4, $5, $6, false, $7, true, 'Active', $8, $9, 1)", connection, transaction))
            {
                identity.Parameters.AddWithValue(identityId.Value); identity.Parameters.AddWithValue(tenantId.Value); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email)); identity.Parameters.AddWithValue(prefix); identity.Parameters.AddWithValue(cleanName); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(clock.GetUtcNow()); identity.Parameters.AddWithValue(Guid.CreateVersion7());
                await identity.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var subject = new NpgsqlCommand("insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version) values ($1, $2, $3, $4, 'Active', $5, 1)", connection, transaction))
            {
                subject.Parameters.AddWithValue(membershipId.Value); subject.Parameters.AddWithValue(tenantId.Value); subject.Parameters.AddWithValue(identityId.Value); subject.Parameters.AddWithValue(cleanName); subject.Parameters.AddWithValue(clock.GetUtcNow());
                await subject.ExecuteNonQueryAsync(cancellationToken);
            }
            if (roleIds.Count > 0)
            {
                await using var roles = new NpgsqlCommand("insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type) select $1, $2, role.id, 'Tenant' from platform.role role where role.tenant_id = $1 and role.id = any($3) and role.is_active", connection, transaction);
                roles.Parameters.AddWithValue(tenantId.Value); roles.Parameters.AddWithValue(membershipId.Value); roles.Parameters.AddWithValue("role_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, roleIds.ToArray());
                if (await roles.ExecuteNonQueryAsync(cancellationToken) != roleIds.Count) throw new InvalidOperationException("identity.role_assignment_invalid");
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("identity.email_prefix_already_used", exception);
        }
        var result = new UserAccountSummary(identityId.Value, tenantId.Value, cleanName, email, prefix, "Active", true, clock.GetUtcNow());
        await WriteAuditAsync(tenantId, identityId.Value, "user_created", cancellationToken);
        return new(result, password);
    }

    public async Task<UserAccountPage> ListAsync(TenantId tenantId, string? search, bool? active, int offset, int limit, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "identity.pagination_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var countCommand = new NpgsqlCommand("select count(*) from platform.identity where tenant_id = $1 and ($2::text is null or lower(name) like lower($2) || '%' or normalized_email like upper($2) || '%') and ($3::boolean is null or status = case when $3 then 'Active' else 'Suspended' end)", connection, transaction);
        countCommand.Parameters.AddWithValue(tenantId.Value); countCommand.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value); countCommand.Parameters.AddWithValue((object?)active ?? DBNull.Value);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("select id, tenant_id, name, email, login_prefix, status, must_change_password, created_at from platform.identity where tenant_id = $1 and ($2::text is null or lower(name) like lower($2) || '%' or normalized_email like upper($2) || '%') and ($3::boolean is null or status = case when $3 then 'Active' else 'Suspended' end) order by name, id limit $4 offset $5", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue((object?)search?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue((object?)active ?? DBNull.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserAccountSummary>();
        while (await reader.ReadAsync(cancellationToken)) users.Add(ReadSummary(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new UserAccountPage(users, total, offset / limit + 1, limit);
    }

    public async Task<UserAccountSummary?> FindAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select id, tenant_id, name, email, login_prefix, status, must_change_password, created_at from platform.identity where tenant_id = $1 and id = $2", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        var result = await reader.ReadAsync(cancellationToken) ? ReadSummary(reader) : null;
        await reader.DisposeAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public Task<bool> RenameAsync(TenantId tenantId, IdentityId identityId, string name, CancellationToken cancellationToken = default) => UpdateNameAsync(tenantId, identityId, ValidateName(name), cancellationToken);

    private async Task<bool> UpdateNameAsync(TenantId tenantId, IdentityId identityId, string name, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.identity set name = $1, version = version + 1 where tenant_id = $2 and id = $3 and status <> 'Revoked'", connection, transaction); command.Parameters.AddWithValue(name); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(identityId.Value); var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1; await transaction.CommitAsync(cancellationToken); if (changed) await WriteAuditAsync(tenantId, identityId.Value, "user_name_changed", cancellationToken); return changed;
    }

    public async Task<bool> RenameLoginPrefixAsync(TenantId tenantId, IdentityId identityId, string emailPrefix, CancellationToken cancellationToken = default)
    {
        var prefix = LoginDomainConventions.NormalizePrefix(emailPrefix);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var domain = await LoadTenantForUpdateAsync(connection, transaction, tenantId, cancellationToken); if (domain is null) return false;
        var email = LoginDomainConventions.BuildEmail(prefix, domain.Value.Domain);
        try
        {
            await using var command = new NpgsqlCommand("update platform.identity set login_prefix = $1, email = $2, normalized_email = $3, security_stamp = $4, version = version + 1 where tenant_id = $5 and id = $6 and status <> 'Revoked'", connection, transaction); command.Parameters.AddWithValue(prefix); command.Parameters.AddWithValue(email); command.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email)); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(identityId.Value); if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
            await using var invalidate = new NpgsqlCommand("delete from platform.auth_challenge where identity_id = $1", connection, transaction); invalidate.Parameters.AddWithValue(identityId.Value); await invalidate.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken); await WriteAuditAsync(tenantId, identityId.Value, "login_email_changed", cancellationToken); return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { await transaction.RollbackAsync(cancellationToken); throw new InvalidOperationException("identity.email_prefix_already_used", exception); }
    }

    public async Task<bool> SetActiveAsync(TenantId tenantId, IdentityId identityId, bool active, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.identity set status = $1, security_stamp = $2, version = version + 1 where tenant_id = $3 and id = $4 and status <> 'Revoked'", connection, transaction); command.Parameters.AddWithValue(active ? "Active" : "Suspended"); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(identityId.Value); var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (!active) { await using var invalidate = new NpgsqlCommand("delete from platform.auth_challenge where identity_id = $1", connection, transaction); invalidate.Parameters.AddWithValue(identityId.Value); await invalidate.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken); if (changed) await WriteAuditAsync(tenantId, identityId.Value, active ? "user_reactivated" : "user_deactivated", cancellationToken); return changed;
    }

    public async Task<string?> ResetPasswordAsync(TenantId tenantId, IdentityId identityId, CancellationToken cancellationToken = default)
    {
        var password = CreateTemporaryPassword();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.identity set password_hash = $1, must_change_password = true, security_stamp = $2, failed_login_count = 0, locked_until = null, version = version + 1 where tenant_id = $3 and id = $4 and status = 'Active'", connection, transaction); command.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); command.Parameters.AddWithValue(Guid.CreateVersion7()); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(identityId.Value); if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return null;
        await using var invalidate = new NpgsqlCommand("delete from platform.auth_challenge where identity_id = $1", connection, transaction); invalidate.Parameters.AddWithValue(identityId.Value); await invalidate.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); await WriteAuditAsync(tenantId, identityId.Value, "password_reset", cancellationToken); return password;
    }

    public async Task<string> ChangeLoginDomainAsync(TenantId tenantId, string loginDomain, CancellationToken cancellationToken = default)
    {
        var domain = LoginDomainConventions.NormalizeDomain(loginDomain);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        var current = await LoadTenantForUpdateAsync(connection, transaction, tenantId, cancellationToken) ?? throw new InvalidOperationException("tenant.not_found");
        if (string.Equals(current.Domain, domain, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return domain;
        }
        try
        {
            await using var tenantConflict = new NpgsqlCommand("select 1 from platform.tenant where normalized_login_domain = $1 and id <> $2 limit 1", connection, transaction);
            tenantConflict.Parameters.AddWithValue(domain); tenantConflict.Parameters.AddWithValue(tenantId.Value);
            if (await tenantConflict.ExecuteScalarAsync(cancellationToken) is not null) throw new InvalidOperationException("tenant.login_domain_already_used");
            await using var emailConflict = new NpgsqlCommand("select 1 from platform.identity where normalized_email in (select upper(login_prefix || '@' || $1) from platform.identity where tenant_id = $2) and tenant_id <> $2 limit 1", connection, transaction);
            emailConflict.Parameters.AddWithValue(domain); emailConflict.Parameters.AddWithValue(tenantId.Value);
            if (await emailConflict.ExecuteScalarAsync(cancellationToken) is not null) throw new InvalidOperationException("tenant.login_domain_already_used");
            await using var users = new NpgsqlCommand("update platform.identity set email = login_prefix || '@' || $1, normalized_email = upper(login_prefix || '@' || $1), security_stamp = gen_random_uuid(), version = version + 1 where tenant_id = $2", connection, transaction);
            users.Parameters.AddWithValue(domain); users.Parameters.AddWithValue(tenantId.Value); await users.ExecuteNonQueryAsync(cancellationToken);
            await using var tenant = new NpgsqlCommand("update platform.tenant set login_domain = $1, normalized_login_domain = $2, version = version + 1 where id = $3", connection, transaction);
            tenant.Parameters.AddWithValue(domain); tenant.Parameters.AddWithValue(domain); tenant.Parameters.AddWithValue(tenantId.Value); await tenant.ExecuteNonQueryAsync(cancellationToken);
            await using var invalidate = new NpgsqlCommand("delete from platform.auth_challenge where identity_id in (select id from platform.identity where tenant_id = $1)", connection, transaction);
            invalidate.Parameters.AddWithValue(tenantId.Value); await invalidate.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("tenant.login_domain_already_used", exception);
        }
        await WriteAuditAsync(tenantId, tenantId.Value, "login_domain_changed", cancellationToken);
        return domain;
    }

    public async Task<string?> LoadLoginDomainAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("select login_domain from platform.tenant where id = $1", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        var domain = (string?)await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return domain;
    }

    private static async Task<(string Domain, string Status)?> LoadTenantForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select login_domain, status from platform.tenant where id = $1 for update", connection, transaction); command.Parameters.AddWithValue(tenantId.Value); await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken); return await reader.ReadAsync(cancellationToken) ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    private async Task WriteAuditAsync(TenantId tenantId, Guid entityId, string action, CancellationToken cancellationToken) => await audit.WriteAsync(new AuditEntry(Guid.CreateVersion7(), tenantId, null, null, "User", null, null, "identity", action, "User", entityId, null, null, null, "api", Guid.CreateVersion7(), null, clock.GetUtcNow(), null), cancellationToken);
    private static string ValidateName(string name) { ArgumentException.ThrowIfNullOrWhiteSpace(name); var clean = name.Trim(); if (clean.Length > 200) throw new ArgumentException("identity.name_invalid", nameof(name)); return clean; }
    private static string CreateTemporaryPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', 'A').Replace('/', 'B').TrimEnd('=') + "a1A!";
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction); command.Parameters.AddWithValue(tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(cancellationToken); }
    private static UserAccountSummary ReadSummary(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetBoolean(6), reader.GetFieldValue<DateTimeOffset>(7));
}
