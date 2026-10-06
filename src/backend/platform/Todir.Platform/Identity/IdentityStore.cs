using System.Data;
using Npgsql;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Identity;

public interface IIdentityStore
{
    Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default);
    Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default);
}

public sealed class NullIdentityStore : IIdentityStore
{
    public Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default) => Task.FromResult<Identity?>(null);
    public Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Membership>>([]);
    public Task<Membership?> FindActiveMembershipAsync(IdentityId identityId, TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<Membership?>(null);
    public Task<bool> ValidateSessionAsync(IdentityId identityId, TenantId tenantId, Guid securityStamp, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public sealed class NpgsqlIdentityStore(NpgsqlDataSource dataSource) : IIdentityStore
{
    public async Task<Identity?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("select id, normalized_email, email_verified, status, created_at, last_login_at, security_stamp, password_hash from platform_iam.identity where normalized_email = $1 limit 1");
        command.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email));
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var identity = ReadIdentity(reader);
        return identity.Status == IdentityStatus.Active && IdentityConventions.VerifyPassword(password, identity.PasswordHash ?? "") ? identity : null;
    }

    public async Task<IReadOnlyList<Membership>> ListActiveMembershipsAsync(IdentityId identityId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("select id, tenant_id, identity_id, display_name, status, default_legal_entity_id, default_branch_id, locale, timezone, created_at, version from platform_iam.membership where identity_id = $1 and status = 'Active' order by created_at");
        command.Parameters.AddWithValue(identityId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var memberships = new List<Membership>();
        while (await reader.ReadAsync(cancellationToken)) memberships.Add(ReadMembership(reader));
        return memberships;
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

    private static Identity ReadIdentity(NpgsqlDataReader reader) => new(new IdentityId(reader.GetGuid(0)), reader.GetString(1), reader.GetBoolean(2), Enum.Parse<IdentityStatus>(reader.GetString(3), true), reader.GetFieldValue<DateTimeOffset>(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5), reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7));
    private static Membership ReadMembership(NpgsqlDataReader reader) => new(new MembershipId(reader.GetGuid(0)), new TenantId(reader.GetGuid(1)), new IdentityId(reader.GetGuid(2)), reader.GetString(3), Enum.Parse<MembershipStatus>(reader.GetString(4), true), reader.IsDBNull(5) ? null : new LegalEntityId(reader.GetGuid(5)), reader.IsDBNull(6) ? null : new BranchId(reader.GetGuid(6)), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.GetInt64(10));
}
