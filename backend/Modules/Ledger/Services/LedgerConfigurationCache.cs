using System.Collections.Concurrent;
using Modules.Ledger.DTOs;
using Shared.Primitives.Identifiers;

namespace Modules.Ledger.Services;

public sealed record LedgerConfigurationSnapshot(LedgerMode Mode, Guid ConfigurationVersionId, long Version, DateTimeOffset LoadedAt);

/// <summary>Short-lived exposure cache. Financial posting still performs an authoritative period/configuration check.</summary>
public sealed class LedgerConfigurationCache
{
    private readonly ConcurrentDictionary<(TenantId Tenant, LegalEntityId Entity), LedgerConfigurationSnapshot> entries = new();

    public bool TryGet(TenantId tenant, LegalEntityId entity, out LedgerConfigurationSnapshot snapshot) => entries.TryGetValue((tenant, entity), out snapshot!);

    public void Set(TenantId tenant, LegalEntityId entity, LedgerConfigurationSnapshot snapshot) => entries[(tenant, entity)] = snapshot;

    public void Invalidate(TenantId tenant, LegalEntityId entity) => entries.TryRemove((tenant, entity), out _);

    public void InvalidateTenant(TenantId tenant)
    {
        foreach (var key in entries.Keys.Where(key => key.Tenant == tenant)) entries.TryRemove(key, out _);
    }
}
