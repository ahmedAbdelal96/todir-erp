using Npgsql;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Core.Services;

public sealed class NpgsqlPlatformApprovalCapability(NpgsqlDataSource dataSource, TimeProvider clock) : IPlatformApprovalCapability
{
    public async Task<PlatformApprovalRequestResult> RequestAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid legalEntityId, string ownerModule, string ownerEntityType, Guid ownerEntityId, long resourceVersion, Guid? policyVersionId, string? reason, string requesterPermission = "platform.approval.request", CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || membershipId == Guid.Empty || ownerEntityId == Guid.Empty || resourceVersion < 1) throw new InvalidOperationException("platform.approval_request_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken); await EnsureAuthorizedAsync(connection, transaction, tenantId, actorId, membershipId, legalEntityId, requesterPermission, cancellationToken);
        var id = Guid.CreateVersion7();
        await using var command = new NpgsqlCommand("insert into platform.approval_request(tenant_id,id,legal_entity_id,owner_module,owner_entity_type,owner_entity_id,resource_version,policy_version_id,state,requested_by,request_reason,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Pending',$9,$10,$11) on conflict(tenant_id,owner_module,owner_entity_type,owner_entity_id,resource_version) do update set request_reason=platform.approval_request.request_reason returning id,legal_entity_id,owner_module,owner_entity_type,owner_entity_id,resource_version,policy_version_id,state,decided_by,decision_reason,created_at,decided_at,request_reason,requested_by", connection, transaction);
        Add(command, tenantId.Value, id, legalEntityId, ownerModule.Trim(), ownerEntityType.Trim(), ownerEntityId, resourceVersion, (object?)policyVersionId ?? DBNull.Value, actorId, (object?)reason ?? DBNull.Value, clock.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("platform.approval_request_failed"); var result = Read(reader); await reader.CloseAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<PlatformApprovalRequestResult> DecideAsync(TenantId tenantId, Guid actorId, Guid membershipId, Guid requestId, string decision, string? reason, CancellationToken cancellationToken = default)
    {
        if (decision is not ("Approved" or "Rejected")) throw new InvalidOperationException("platform.approval_decision_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var scope = new NpgsqlCommand("select legal_entity_id,owner_module from platform.approval_request where tenant_id=$1 and id=$2", connection, transaction); Add(scope, tenantId.Value, requestId); await using var scopeReader = await scope.ExecuteReaderAsync(cancellationToken); if (!await scopeReader.ReadAsync(cancellationToken)) throw new InvalidOperationException("platform.approval_not_found"); var legalEntityId = scopeReader.GetGuid(0); var ownerModule = scopeReader.GetString(1); await scopeReader.CloseAsync(); var decisionPermission = ownerModule switch { "planning" => "planning.budget_control.override.approve", "purchasing" => "purchasing.request.approve", _ => "expenses.approve" }; await EnsureAuthorizedAsync(connection, transaction, tenantId, actorId, membershipId, legalEntityId, decisionPermission, cancellationToken);
        await using var command = new NpgsqlCommand("update platform.approval_request set state=$4,decided_by=$5,decision_reason=$6,decided_at=$7 where tenant_id=$1 and id=$2 and state='Pending' and requested_by <> $5 returning id,legal_entity_id,owner_module,owner_entity_type,owner_entity_id,resource_version,policy_version_id,state,decided_by,decision_reason,created_at,decided_at,request_reason,requested_by", connection, transaction);
        Add(command, tenantId.Value, requestId, actorId, decision, actorId, (object?)reason ?? DBNull.Value, clock.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("platform.approval_stale_or_unauthorized"); var result = Read(reader); await reader.CloseAsync(); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<PlatformApprovalRequestResult?> GetAsync(TenantId tenantId, Guid requestId, CancellationToken cancellationToken = default)
    { await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await SetTenantAsync(connection, transaction, tenantId, cancellationToken); await using var command = new NpgsqlCommand("select id,legal_entity_id,owner_module,owner_entity_type,owner_entity_id,resource_version,policy_version_id,state,decided_by,decision_reason,created_at,decided_at,request_reason,requested_by from platform.approval_request where tenant_id=$1 and id=$2", connection, transaction); Add(command, tenantId.Value, requestId); await using var reader = await command.ExecuteReaderAsync(cancellationToken); var result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null; await reader.CloseAsync(); await transaction.CommitAsync(cancellationToken); return result; }

    private static PlatformApprovalRequestResult Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetGuid(13));
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken ct) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); Add(command, tenantId.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
    private static async Task EnsureAuthorizedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, Guid actorId, Guid membershipId, Guid legalEntityId, string permissionCode, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from platform.membership m join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id join platform.role r on r.tenant_id=a.tenant_id and r.id=a.role_id join platform.role_permission p on p.tenant_id=r.tenant_id and p.role_id=a.role_id where m.tenant_id=$1 and m.id=$2 and m.identity_id=$3 and m.status='Active' and r.is_active and p.permission_code=$4 and (a.scope_type='Tenant' or (a.scope_type='LegalEntity' and a.scope_id=$5)))", connection, transaction);
        Add(command, tenantId.Value, membershipId, actorId, permissionCode, legalEntityId);
        if (!Convert.ToBoolean(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture)) throw new InvalidOperationException("platform.approval_unauthorized");
    }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
}
