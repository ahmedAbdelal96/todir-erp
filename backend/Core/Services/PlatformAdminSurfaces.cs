using System.Text.Json;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Services;

public sealed record LegalEntitySummary(Guid Id, string Code, string Name, string TimeZone, string Locale);
public sealed record BranchSummary(Guid Id, Guid LegalEntityId, string Code, string Name, string Status);
public sealed record TenantContextSnapshot(Guid TenantId, string Slug, string DisplayName, string Status, IReadOnlyList<LegalEntitySummary> LegalEntities, IReadOnlyList<BranchSummary> Branches);
public sealed record RoleSummary(Guid Id, string Code, string Name, bool IsSystemTemplate, bool IsActive, long Version, IReadOnlyList<string> Permissions);
public sealed record ModuleInstanceSummary(Guid Id, string ModuleId, string ScopeType, Guid? ScopeId, string Status, string ManifestVersion, long Version);
public sealed record SettingSummary(Guid DefinitionId, string Key, string DataType, string Scope, bool Sensitive, JsonElement? Value, long Version);
public sealed record AuditSummary(Guid Id, Guid? LegalEntityId, Guid? BranchId, string ActorType, Guid? ActorId, Guid? MembershipId, string Module, string Action, string EntityType, Guid EntityId, string? Reason, DateTimeOffset OccurredAt, string? TraceId);
public sealed record EntitlementSummary(long SnapshotVersion, DateTimeOffset IssuedAt, DateTimeOffset ValidUntil, string KeyId, IReadOnlyList<string> Modules, IReadOnlyList<string> Features, IReadOnlyDictionary<string, long> Limits);

public interface IPlatformAdminSurfaceStore
{
    Task<TenantContextSnapshot?> LoadTenantContextAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<bool> OrganizationScopeExistsAsync(TenantId tenantId, Guid? legalEntityId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleSummary>> ListRolesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModuleInstanceSummary>> ListModuleInstancesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SettingSummary>> ListSettingsAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditSummary>> ListAuditAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default);
    Task<EntitlementSummary?> LoadEntitlementAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}

public sealed class NullPlatformAdminSurfaceStore : IPlatformAdminSurfaceStore
{
    public Task<TenantContextSnapshot?> LoadTenantContextAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<TenantContextSnapshot?>(null);
    public Task<bool> OrganizationScopeExistsAsync(TenantId tenantId, Guid? legalEntityId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RoleSummary>>([]);
    public Task<IReadOnlyList<ModuleInstanceSummary>> ListModuleInstancesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ModuleInstanceSummary>>([]);
    public Task<IReadOnlyList<SettingSummary>> ListSettingsAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SettingSummary>>([]);
    public Task<IReadOnlyList<AuditSummary>> ListAuditAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditSummary>>([]);
    public Task<EntitlementSummary?> LoadEntitlementAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.FromResult<EntitlementSummary?>(null);
}

public sealed class NpgsqlPlatformAdminSurfaceStore(NpgsqlDataSource dataSource) : IPlatformAdminSurfaceStore
{
    public async Task<TenantContextSnapshot?> LoadTenantContextAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            TenantContextSnapshot snapshot;
            await using (var tenant = new NpgsqlCommand("select id, slug, display_name, status from platform.tenant where id = $1", connection, transaction))
            {
                tenant.Parameters.AddWithValue(tenantId.Value);
                await using var reader = await tenant.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
                if (!await reader.ReadAsync(token)) return null;
                snapshot = new TenantContextSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), [], []);
            }

            var legalEntities = new List<LegalEntitySummary>();
            await using (var entities = new NpgsqlCommand("select id, code, legal_name, timezone, locale from platform.legal_entity where tenant_id = $1 order by code, id", connection, transaction))
            {
                entities.Parameters.AddWithValue(tenantId.Value);
                await using var entityReader = await entities.ExecuteReaderAsync(token);
                while (await entityReader.ReadAsync(token)) legalEntities.Add(new(entityReader.GetGuid(0), entityReader.GetString(1), entityReader.GetString(2), entityReader.GetString(3), entityReader.GetString(4)));
            }

            var branches = new List<BranchSummary>();
            await using (var branchCommand = new NpgsqlCommand("select id, legal_entity_id, code, name, status from platform.branch where tenant_id = $1 order by legal_entity_id, code, id", connection, transaction))
            {
                branchCommand.Parameters.AddWithValue(tenantId.Value);
                await using var branchReader = await branchCommand.ExecuteReaderAsync(token);
                while (await branchReader.ReadAsync(token)) branches.Add(new(branchReader.GetGuid(0), branchReader.GetGuid(1), branchReader.GetString(2), branchReader.GetString(3), branchReader.GetString(4)));
            }

            return snapshot with { LegalEntities = legalEntities, Branches = branches };
        }, cancellationToken);
    }

    public async Task<bool> OrganizationScopeExistsAsync(TenantId tenantId, Guid? legalEntityId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (branchId is not null && legalEntityId is null) return false;
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            const string sql = "select case when $2::uuid is null then exists (select 1 from platform.legal_entity where tenant_id = $1 and id = $3) else exists (select 1 from platform.branch where tenant_id = $1 and id = $2 and legal_entity_id = $3) end";
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value);
            command.Parameters.AddWithValue((object?)branchId ?? DBNull.Value);
            command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
            return (bool)(await command.ExecuteScalarAsync(token))!;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ValidatePage(offset, limit);
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            const string sql = "select r.id, r.code, r.name, r.is_system_template, r.is_active, r.version, coalesce(array_agg(p.permission_code order by p.permission_code) filter (where p.permission_code is not null), '{}') from platform.role r left join platform.role_permission p on p.tenant_id = r.tenant_id and p.role_id = r.id where r.tenant_id = $1 group by r.id, r.code, r.name, r.is_system_template, r.is_active, r.version order by r.code, r.id limit $2 offset $3";
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
            await using var reader = await command.ExecuteReaderAsync(token);
            var result = new List<RoleSummary>();
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetInt64(5), reader.GetFieldValue<string[]>(6)));
            return result;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ModuleInstanceSummary>> ListModuleInstancesAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ValidatePage(offset, limit);
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            await using var command = new NpgsqlCommand("select id, module_id, scope_type, scope_id, status, manifest_version, version from platform.module_instance where tenant_id = $1 order by module_id, id limit $2 offset $3", connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
            await using var reader = await command.ExecuteReaderAsync(token);
            var result = new List<ModuleInstanceSummary>();
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6)));
            return result;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SettingSummary>> ListSettingsAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ValidatePage(offset, limit);
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            const string sql = "select d.id, d.setting_key, d.data_type, d.scope, d.sensitive, v.value::text, coalesce(v.version, d.version) from platform.setting_definition d left join platform.setting_value v on v.setting_definition_id = d.id and v.tenant_id = $1 order by d.setting_key, v.scope, v.scope_id limit $2 offset $3";
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
            await using var reader = await command.ExecuteReaderAsync(token);
            var result = new List<SettingSummary>();
            while (await reader.ReadAsync(token))
            {
                JsonElement? value = null;
                if (!reader.IsDBNull(5) && !reader.GetBoolean(4)) using (var document = JsonDocument.Parse(reader.GetString(5))) value = document.RootElement.Clone();
                result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), value, reader.GetInt64(6)));
            }
            return result;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditSummary>> ListAuditAsync(TenantId tenantId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ValidatePage(offset, limit);
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            await using var command = new NpgsqlCommand("select id, legal_entity_id, branch_id, actor_type, actor_id, membership_id, module, action, entity_type, entity_id, reason, occurred_at, trace_id from platform.audit_entry where tenant_id = $1 order by occurred_at desc, id desc limit $2 offset $3", connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(limit); command.Parameters.AddWithValue(offset);
            await using var reader = await command.ExecuteReaderAsync(token);
            var result = new List<AuditSummary>();
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetString(12)));
            return result;
        }, cancellationToken);
    }

    public async Task<EntitlementSummary?> LoadEntitlementAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await WithTenantTransactionAsync(tenantId, async (connection, transaction, token) =>
        {
            await using var command = new NpgsqlCommand("select snapshot_version, issued_at, valid_until, key_id, modules::text, features::text, limits::text from platform.entitlement_snapshot where tenant_id = $1 order by snapshot_version desc limit 1", connection, transaction);
            command.Parameters.AddWithValue(tenantId.Value);
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
            if (!await reader.ReadAsync(token)) return null;
            var modules = JsonSerializer.Deserialize<string[]>(reader.GetString(4)) ?? [];
            var features = JsonSerializer.Deserialize<string[]>(reader.GetString(5)) ?? [];
            var limits = JsonSerializer.Deserialize<Dictionary<string, long>>(reader.GetString(6)) ?? [];
            return new EntitlementSummary(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetString(3), modules, features, limits);
        }, cancellationToken);
    }

    private async Task<TResult> WithTenantTransactionAsync<TResult>(TenantId tenantId, Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (connection)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction))
            {
                command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var result = await operation(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "platform.pagination_invalid");
    }
}
