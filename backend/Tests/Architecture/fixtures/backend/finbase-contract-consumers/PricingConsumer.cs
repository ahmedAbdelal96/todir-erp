using Shared.Contracts.FinanceBase;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace FinanceBase.ContractConsumers;

public sealed class PricingConsumer(IFinanceBaseCapabilities financeBase)
{
    public async Task<PricingBasis?> ResolveAsync(TenantId tenantId, LegalEntityId legalEntityId, string currencyCode, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var currency = CurrencyCode.Normalize(currencyCode);
        var context = await financeBase.ResolveCalculationContextAsync(tenantId, legalEntityId, currency, FinancialCalculationPurpose.Sales, businessDate, cancellationToken);
        return context is null ? null : new(context, Money.Create("12.345", currency), UnitPrice.Create("4.115", currency), Percentage.FromPercent(5m));
    }
}

public sealed record PricingBasis(FinancialCalculationContextSnapshotContract Context, Money NetAmount, UnitPrice UnitPrice, Percentage Discount);
