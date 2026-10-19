using Core.Services;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Shared.Primitives.Identifiers;

namespace Modules.FinanceBase.Services;

/// <summary>Platform calls this guard before changing the canonical legal-entity currency.</summary>
public sealed class BaseCurrencyChangeGuard(ILegalEntityFinancialReadStore entities, IFinancialGoLiveGuard financialGoLive)
{
    public async Task EnsureChangeAllowedAsync(TenantId tenantId, LegalEntityId legalEntityId, string requestedCurrency, CancellationToken cancellationToken = default)
    {
        var currentCurrency = await entities.GetBaseCurrencyCodeAsync(tenantId, legalEntityId, cancellationToken)
            ?? throw new FinanceBaseException("finbase.legal_entity_not_found", "Legal entity was not found.");
        var current = CurrencyCode.Normalize(currentCurrency);
        var requested = CurrencyCode.Normalize(requestedCurrency);
        if (current == requested) return;
        if (await financialGoLive.HasPostedFinancialFactsAsync(tenantId, legalEntityId, cancellationToken))
            throw new FinanceBaseException("finbase.base_currency_locked", "Base currency cannot be changed after financial go-live.");
    }
}
