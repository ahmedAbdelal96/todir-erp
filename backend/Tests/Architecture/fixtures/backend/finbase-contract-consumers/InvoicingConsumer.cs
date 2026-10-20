using Shared.Contracts.FinanceBase;
using Shared.Primitives.Financial;
using Shared.Primitives.Identifiers;

namespace FinanceBase.ContractConsumers;

public sealed class InvoicingConsumer(IFinanceBaseCapabilities financeBase)
{
    public async Task<InvoiceBasis?> ResolveAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid paymentTermId, string currencyCode, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var context = await financeBase.ResolveCalculationContextAsync(tenantId, legalEntityId, CurrencyCode.Normalize(currencyCode), FinancialCalculationPurpose.Sales, businessDate, cancellationToken);
        var paymentTerm = await financeBase.ResolvePaymentTermAsync(tenantId, paymentTermId, businessDate, cancellationToken);
        if (context is null || paymentTerm is null) return null;
        var total = Money.Create("100.25", currencyCode);
        var schedule = new PaymentScheduleLineContract(1, businessDate, new MoneyContract(total.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), total.Currency));
        return new(context, paymentTerm, total, schedule);
    }
}

public sealed record InvoiceBasis(FinancialCalculationContextSnapshotContract Context, PaymentTermSnapshotContract PaymentTerm, Money Total, PaymentScheduleLineContract FirstScheduleLine);
