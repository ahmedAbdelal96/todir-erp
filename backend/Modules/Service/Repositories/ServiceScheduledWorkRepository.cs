using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Service.Repositories;

/// <summary>Exposes the Service-owned scheduler scopes without making the Worker a Service table reader.</summary>
public sealed class ServiceScheduledWorkRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<(TenantId Tenant, Guid LegalEntityId)>> ListScheduledScopesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select tenant_id,legal_entity_id from business_service.list_scheduled_scopes() order by tenant_id,legal_entity_id", connection);
        var scopes = new List<(TenantId, Guid)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) scopes.Add((new TenantId(reader.GetGuid(0)), reader.GetGuid(1)));
        return scopes;
    }
}
