using System.Text.Json;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Projects.Repositories;

/// <summary>Persists source-event evidence owned by Projects without reading source-module tables.</summary>
public sealed class ProjectsIntegrationRepository(NpgsqlDataSource dataSource)
{
    public async Task<bool> ProjectExistsAsync(TenantId tenant, Guid projectId, Guid? legalEntityId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true); select exists(select 1 from business_projects.project where tenant_id=$1 and id=$2 and ($3::uuid is null or legal_entity_id=$3) and lifecycle_state not in ('Closed','Cancelled'))", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        command.Parameters.AddWithValue(projectId);
        command.Parameters.AddWithValue(legalEntityId ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        await reader.NextResultAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && reader.GetBoolean(0);
    }

    public async Task RecordAsync(TenantId tenant, Guid messageId, string sourceModule, string messageType, Guid? projectId,
        string status, string? reason, string payload, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var tenantCommand = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
        {
            tenantCommand.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await tenantCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var command = new NpgsqlCommand("insert into business_projects.project_integration_event(tenant_id,message_id,source_module,message_type,project_id,status,reason,payload,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9) on conflict(tenant_id,message_id) do update set status=excluded.status,reason=excluded.reason,project_id=excluded.project_id,processed_at=now()", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(sourceModule);
        command.Parameters.AddWithValue(messageType);
        command.Parameters.AddWithValue(projectId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(reason ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(payload);
        command.Parameters.AddWithValue(occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
