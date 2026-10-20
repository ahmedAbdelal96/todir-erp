using Shared.Contracts.FinanceBase;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace FinanceBase.ContractConsumers;

public sealed class TaxConsumer(IFinanceBaseCapabilities financeBase)
{
    public async Task<TaxBasis?> ResolveAsync(TenantId tenantId, LegalEntityId legalEntityId, string currencyCode, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var context = await financeBase.ResolveCalculationContextAsync(tenantId, legalEntityId, CurrencyCode.Normalize(currencyCode), FinancialCalculationPurpose.Tax, businessDate, cancellationToken);
        return context is null ? null : new(context, Money.Create("100.125", currencyCode), Percentage.FromPercent(15m), FinancialRoundingContext.DocumentTotal);
    }
}

public sealed record TaxBasis(FinancialCalculationContextSnapshotContract Context, Money TaxableAmount, Percentage Rate, FinancialRoundingContext RoundingContext);
