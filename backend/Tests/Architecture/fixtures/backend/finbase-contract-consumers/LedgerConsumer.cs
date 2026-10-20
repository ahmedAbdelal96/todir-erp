using Shared.Contracts.FinanceBase;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace FinanceBase.ContractConsumers;

public sealed class LedgerConsumer(IFinanceBaseCapabilities financeBase)
{
    public async Task<LedgerBasis?> ResolveAsync(TenantId tenantId, LegalEntityId legalEntityId, string transactionCurrencyCode, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var context = await financeBase.ResolveCalculationContextAsync(tenantId, legalEntityId, CurrencyCode.Normalize(transactionCurrencyCode), FinancialCalculationPurpose.Accounting, businessDate, cancellationToken);
        return context is null ? null : new(context, Money.Create("250.75", transactionCurrencyCode), Money.Create("12537.50", context.BaseCurrencyCode));
    }
}

public sealed record LedgerBasis(FinancialCalculationContextSnapshotContract Context, Money TransactionAmount, Money BaseAmount);
