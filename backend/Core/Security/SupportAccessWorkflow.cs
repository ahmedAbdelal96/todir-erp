using System.Text.Json;
using Npgsql;
using Core.Identity;
using Core.Messaging;
using Core.Services;
using Shared.Primitives.Identifiers;

namespace Core.Security;

public interface ISupportAccessWorkflow
{
    Task<SupportAccessGrant> RequestAsync(TenantId tenantId, Guid supportActorId, string reason, DateTimeOffset expiresAt, MfaStepUpProof proof, CancellationToken cancellationToken = default);
    Task<SupportAccessGrant?> ApproveAsync(TenantId tenantId, Guid grantId, Guid approvedBy, MfaStepUpProof proof, CancellationToken cancellationToken = default);
    Task<bool> CanAccessAsync(TenantId tenantId, Guid grantId, Guid supportActorId, MfaStepUpProof proof, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(TenantId tenantId, Guid grantId, Guid actorId, MfaStepUpProof proof, CancellationToken cancellationToken = default);
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemorySupportAccessWorkflow(SupportAccessService service) : ISupportAccessWorkflow
{
    public Task<SupportAccessGrant> RequestAsync(TenantId tenantId, Guid supportActorId, string reason, DateTimeOffset expiresAt, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!service.IsMfaProofValid(proof, supportActorId)) throw new UnauthorizedAccessException("support.mfa_required");
        return Task.FromResult(service.Request(tenantId, supportActorId, reason, expiresAt));
    }

    public Task<SupportAccessGrant?> ApproveAsync(TenantId tenantId, Guid grantId, Guid approvedBy, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!service.IsMfaProofValid(proof, approvedBy)) throw new UnauthorizedAccessException("support.mfa_required");
        return Task.FromResult<SupportAccessGrant?>(service.Approve(tenantId, grantId, approvedBy));
    }

    public Task<bool> CanAccessAsync(TenantId tenantId, Guid grantId, Guid supportActorId, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!service.IsMfaProofValid(proof, supportActorId)) return Task.FromResult(false);
        return Task.FromResult(service.CanAccess(grantId, tenantId, supportActorId));
    }

    public Task<bool> RevokeAsync(TenantId tenantId, Guid grantId, Guid actorId, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!service.IsMfaProofValid(proof, actorId)) throw new UnauthorizedAccessException("support.mfa_required");
        return Task.FromResult(service.Revoke(tenantId, grantId));
    }

    public Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(service.ExpireDueGrants());
    }
}

public sealed class NpgsqlSupportAccessWorkflow(NpgsqlDataSource dataSource, IAuditWriter auditWriter, TimeProvider timeProvider) : ISupportAccessWorkflow
{
    public async Task<SupportAccessGrant> RequestAsync(TenantId tenantId, Guid supportActorId, string reason, DateTimeOffset expiresAt, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        if (!IsMfaProofValid(proof, supportActorId)) throw new UnauthorizedAccessException("support.mfa_required");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("support.reason_required", nameof(reason));
        if (expiresAt <= timeProvider.GetUtcNow()) throw new InvalidOperationException("support.expiry_required");
        var grant = new SupportAccessGrant(Guid.CreateVersion7(), tenantId, supportActorId, reason.Trim(), null, expiresAt, SupportAccessStatus.Requested, timeProvider.GetUtcNow());
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("insert into platform_iam.support_access_grant (id, tenant_id, support_actor_id, reason, expires_at, status, created_at) values ($1, $2, $3, $4, $5, 'Requested', $6)", connection, transaction);
        command.Parameters.AddWithValue(grant.Id); command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(supportActorId); command.Parameters.AddWithValue(grant.Reason); command.Parameters.AddWithValue(expiresAt); command.Parameters.AddWithValue(grant.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await WriteAuditAsync(grant, supportActorId, "support_access_requested", connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return grant;
    }

    public async Task<SupportAccessGrant?> ApproveAsync(TenantId tenantId, Guid grantId, Guid approvedBy, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        if (!IsMfaProofValid(proof, approvedBy)) throw new UnauthorizedAccessException("support.mfa_required");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.support_access_grant set approved_by = $3, status = 'Approved' where tenant_id = $1 and id = $2 and status = 'Requested' and expires_at > now() returning id, tenant_id, support_actor_id, reason, approved_by, expires_at, status, created_at", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(grantId); command.Parameters.AddWithValue(approvedBy);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        var grant = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await reader.DisposeAsync();
        if (grant is not null) await WriteAuditAsync(grant, approvedBy, "support_access_approved", connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return grant;
    }

    public async Task<bool> CanAccessAsync(TenantId tenantId, Guid grantId, Guid supportActorId, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        if (!IsMfaProofValid(proof, supportActorId)) return false;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using (var expire = new NpgsqlCommand("update platform_iam.support_access_grant set status = 'Expired' where tenant_id = $1 and id = $2 and expires_at <= now() and status in ('Requested', 'Approved', 'Active')", connection, transaction))
        {
            expire.Parameters.AddWithValue(tenantId.Value); expire.Parameters.AddWithValue(grantId);
            await expire.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("update platform_iam.support_access_grant set status = 'Active' where tenant_id = $1 and id = $2 and support_actor_id = $3 and status in ('Approved', 'Active') and expires_at > now() returning id, tenant_id, support_actor_id, reason, approved_by, expires_at, status, created_at", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(grantId); command.Parameters.AddWithValue(supportActorId);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        var grant = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await reader.DisposeAsync();
        if (grant is not null) await WriteAuditAsync(grant, supportActorId, "support_access_used", connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return grant is not null;
    }

    public async Task<bool> RevokeAsync(TenantId tenantId, Guid grantId, Guid actorId, MfaStepUpProof proof, CancellationToken cancellationToken = default)
    {
        if (!IsMfaProofValid(proof, actorId)) throw new UnauthorizedAccessException("support.mfa_required");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken);
        await using var command = new NpgsqlCommand("update platform_iam.support_access_grant set status = 'Revoked' where tenant_id = $1 and id = $2 and status in ('Requested', 'Approved', 'Active') returning id, tenant_id, support_actor_id, reason, approved_by, expires_at, status, created_at", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(grantId);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, cancellationToken);
        var grant = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        await reader.DisposeAsync();
        if (grant is not null) await WriteAuditAsync(grant, grant.ApprovedBy ?? grant.SupportActorId, "support_access_revoked", connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return grant is not null;
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select id, tenant_id, support_actor_id, reason, approved_by, expires_at, status, created_at from platform_iam.expire_support_access_grants()", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var expired = new List<SupportAccessGrant>();
        while (await reader.ReadAsync(cancellationToken)) expired.Add(Read(reader));
        await reader.DisposeAsync();
        foreach (var grant in expired) await WriteAuditAsync(grant, grant.SupportActorId, "support_access_expired", connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expired.Count;
    }

    private async Task WriteAuditAsync(SupportAccessGrant grant, Guid actorId, string action, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var entry = CreateAuditEntry(grant, actorId, action);
        if (auditWriter is NpgsqlAuditWriter) await NpgsqlAuditWriter.WriteAsync(connection, transaction, entry, cancellationToken);
        else await auditWriter.WriteAsync(entry, cancellationToken);
    }

    private AuditEntry CreateAuditEntry(SupportAccessGrant grant, Guid actorId, string action)
        => new(Guid.CreateVersion7(), grant.TenantId, null, null, ActorType.Support.ToString(), actorId, null, "platform", action, "support_access_grant", grant.Id, null, null, grant.Reason, "platform-support", Guid.CreateVersion7(), null, timeProvider.GetUtcNow(), JsonSerializer.Serialize(new { grant.Status, grant.ExpiresAt }));

    private bool IsMfaProofValid(MfaStepUpProof? proof, Guid actorId) => proof is not null && proof.IdentityId.Value == actorId && proof.IsValid(timeProvider);

    private static SupportAccessGrant Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), new TenantId(reader.GetGuid(1)), reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetFieldValue<DateTimeOffset>(5), Enum.Parse<SupportAccessStatus>(reader.GetString(6), true), reader.GetFieldValue<DateTimeOffset>(7));

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
