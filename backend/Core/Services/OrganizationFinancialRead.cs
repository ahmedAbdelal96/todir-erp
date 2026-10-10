using Npgsql;
using Shared.Primitives.Identifiers;

namespace Core.Services;

/// <summary>Read-only platform contract for modules that need the canonical organization currency.</summary>
public interface ILegalEntityFinancialReadStore
{
    Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default);
}

public sealed class NullLegalEntityFinancialReadStore : ILegalEntityFinancialReadStore
{
    public Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

public sealed class NpgsqlLegalEntityFinancialReadStore(NpgsqlDataSource dataSource) : ILegalEntityFinancialReadStore
{
    public async Task<string?> GetBaseCurrencyCodeAsync(TenantId tenantId, LegalEntityId legalEntityId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var setting = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        setting.Parameters.AddWithValue(tenantId.Value.ToString("D"));
        await setting.ExecuteNonQueryAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select base_currency_code from platform_org.legal_entity where tenant_id=$1 and id=$2", connection);
        command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(legalEntityId.Value);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }
}
