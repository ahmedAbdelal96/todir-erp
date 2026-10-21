using System.Globalization;
using Modules.FinanceBase.DTOs;
using Modules.FinanceBase.Entities;
using Modules.FinanceBase.Repositories;
using Shared.Contracts.FinanceBase;
using Shared.Primitives.Identifiers;
using Core.Services;

namespace Modules.FinanceBase.Services;

public sealed class FinanceBaseCapabilities(FinanceBaseRepository repository, ILegalEntityFinancialReadStore organization, TimeProvider clock) : IFinanceBaseCapabilities
{
    public async Task<FinancialCalculationContextSnapshotContract?> ResolveCalculationContextAsync(TenantId tenantId, LegalEntityId legalEntityId, string transactionCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var baseCurrency = await organization.GetBaseCurrencyCodeAsync(tenantId, legalEntityId, cancellationToken);
        if (baseCurrency is null) return null;
        var policy = await repository.GetPolicyAsync(tenantId, legalEntityId, cancellationToken);
        if (policy is null) return null;
        var rounding = (await repository.ListRoundingPoliciesAsync(tenantId, cancellationToken)).FirstOrDefault(item => item.Id == policy.RoundingPolicyId);
        if (rounding is null) return null;
        ExchangeRateSnapshotContract? rate = null;
        if (!string.Equals(baseCurrency, transactionCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            var converted = await repository.ConvertAsync(tenantId, new ConvertMoneyRequest(legalEntityId.Value, "1", transactionCurrencyCode, baseCurrency, MapPurpose(purpose), businessDate), cancellationToken);
            rate = Map(converted.Snapshot, purpose);
        }
        var contextRules = (rounding.ContextRules ?? []).Select(rule => new FinancialRoundingRuleSnapshotContract(Enum.Parse<FinancialRoundingContext>(rule.Context.ToString(), true), Enum.Parse<FinancialRoundingMode>(rule.Mode.ToString(), true))).ToArray();
        var roundingSnapshot = new FinancialRoundingPolicySnapshotContract(rounding.Id, rounding.Version, rounding.MoneyScale, rounding.CashIncrement, Enum.Parse<FinancialLineDocumentStrategy>(rounding.LineDocumentStrategy.ToString(), true), contextRules);
        return new(tenantId, legalEntityId, baseCurrency, transactionCurrencyCode, purpose, businessDate, new(legalEntityId.Value, policy.RoundingPolicyId, policy.ExchangeRateMaxAgeDays, policy.ManualRateOverrideAllowed, policy.Version), roundingSnapshot, rate, clock.GetUtcNow());
    }

    public async Task<PaymentTermSnapshotContract?> ResolvePaymentTermAsync(TenantId tenantId, Guid paymentTermId, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var history = await repository.GetPaymentTermHistoryAsync(tenantId, paymentTermId, cancellationToken);
        var version = history?.Versions.Where(item => (item.Status is "Active" or "Expired") && item.EffectiveFrom <= businessDate && (item.EffectiveTo is null || businessDate < item.EffectiveTo)).OrderByDescending(item => item.EffectiveFrom).FirstOrDefault();
        if (history is null || version is null) return null;
        return new(paymentTermId, version.Id, history.Code, version.VersionNumber, version.EffectiveFrom, version.EffectiveTo, version.Rules.Select(rule => new PaymentTermRuleSnapshotContract(rule.Sequence, rule.AllocationType.ToString(), rule.Percentage, rule.FixedAmount, rule.DueDateBasis.ToString(), rule.Days, rule.DayOfMonth, rule.MonthOffset)).ToArray());
    }

    public async Task<CommercialPriceConversionContract> ConvertCommercialUnitPriceAsync(TenantId tenantId, LegalEntityId legalEntityId, string amount, string fromCurrencyCode, string toCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var converted = await repository.ConvertAsync(tenantId, new ConvertMoneyRequest(legalEntityId.Value, amount, fromCurrencyCode, toCurrencyCode, ExchangeRatePurpose.Commercial, businessDate), cancellationToken);
        return new(converted.SourceAmount, converted.SourceCurrency, converted.TargetAmount, converted.TargetCurrency, converted.Rate, Map(converted.Snapshot, purpose));
    }

    private static ExchangeRateSnapshotContract? Map(ExchangeRateSnapshotDto? snapshot, FinancialCalculationPurpose purpose) => snapshot is null ? null : new(snapshot.TenantId, snapshot.LegalEntityId, snapshot.ApprovedRateId, snapshot.FromCurrency, snapshot.ToCurrency, snapshot.Rate, purpose, snapshot.EffectiveDate, snapshot.Source, snapshot.Version, snapshot.ManualOverride, snapshot.CapturedAt);
    private static ExchangeRatePurpose MapPurpose(FinancialCalculationPurpose purpose) => purpose switch { FinancialCalculationPurpose.Purchase => ExchangeRatePurpose.Commercial, FinancialCalculationPurpose.Tax => ExchangeRatePurpose.Tax, FinancialCalculationPurpose.Accounting => ExchangeRatePurpose.Accounting, _ => ExchangeRatePurpose.Commercial };
}
