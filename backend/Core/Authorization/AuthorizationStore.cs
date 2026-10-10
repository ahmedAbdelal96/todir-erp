using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Authorization;

public interface IAuthorizationSubjectStore
{
    Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default);
}

public sealed class NullAuthorizationSubjectStore : IAuthorizationSubjectStore
{
    public Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default) => Task.FromResult<AuthorizationSubject?>(null);
}

public sealed class NpgsqlAuthorizationSubjectStore(NpgsqlDataSource dataSource) : IAuthorizationSubjectStore
{
    public async Task<AuthorizationSubject?> LoadAsync(TenantId tenantId, Guid membershipId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var context = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
        {
            context.Parameters.AddWithValue(tenantId.Value.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = "select a.role_id, a.scope_type, a.scope_id, r.code, r.name, r.is_system_template, r.is_active, r.version, p.permission_code, b.legal_entity_id from platform_authz.membership_role_assignment a join platform_authz.role r on r.tenant_id = a.tenant_id and r.id = a.role_id left join platform_authz.role_permission p on p.tenant_id = a.tenant_id and p.role_id = a.role_id left join platform_org.branch b on b.tenant_id = a.tenant_id and a.scope_type = 'Branch' and b.id = a.scope_id where a.tenant_id = $1 and a.membership_id = $2";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value);
        command.Parameters.AddWithValue(membershipId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<MembershipRoleAssignment>();
        var roles = new Dictionary<Guid, Role>();
        var permissions = new HashSet<RolePermissionEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleId = reader.GetGuid(0);
            var scopeType = Enum.Parse<PermissionScopeType>(reader.GetString(1), true);
            var scopeId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            var scope = scopeType switch
            {
                PermissionScopeType.Tenant => PermissionScope.ForTenant(tenantId),
                PermissionScopeType.LegalEntity => new PermissionScope(scopeType, tenantId, new LegalEntityId(scopeId ?? throw new InvalidDataException("authorization.scope_id_missing"))),
                PermissionScopeType.Branch => PermissionScope.ForBranch(tenantId, new LegalEntityId(reader.IsDBNull(9) ? throw new InvalidDataException("authorization.legal_entity_missing") : reader.GetGuid(9)), new BranchId(scopeId ?? throw new InvalidDataException("authorization.scope_id_missing"))),
                _ => throw new InvalidDataException("authorization.scope_invalid")
            };
            assignments.Add(new MembershipRoleAssignment(tenantId, membershipId, roleId, scope));
            roles[roleId] = new Role(roleId, tenantId, reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetInt64(7));
            if (!reader.IsDBNull(8)) permissions.Add(new RolePermissionEntry(tenantId, roleId, reader.GetString(8)));
        }
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        var version = roles.Values.Select(role => role.Version).DefaultIfEmpty().Max();
        return new AuthorizationSubject(tenantId, membershipId, assignments, roles.Values.ToArray(), permissions.ToArray(), checked((int)Math.Min(version, int.MaxValue)));
    }
}
