using System.Globalization;
using System;
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

    public async Task<PaymentTermScheduleSnapshotContract?> CalculatePaymentScheduleAsync(TenantId tenantId, LegalEntityId legalEntityId, Guid paymentTermId, string transactionCurrencyCode, string totalAmount, DateOnly basisDate, CancellationToken cancellationToken = default)
    {
        var term = await ResolvePaymentTermAsync(tenantId, paymentTermId, basisDate, cancellationToken);
        if (term is null) return null;
        if (!decimal.TryParse(totalAmount, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var total) || total <= 0m)
            throw new FinanceBaseDomainException("finbase.invalid_decimal", "Payment schedule total must be a positive invariant decimal amount.");
        var context = await ResolveCalculationContextAsync(tenantId, legalEntityId, transactionCurrencyCode, FinancialCalculationPurpose.Accounting, basisDate, cancellationToken)
            ?? throw new FinanceBaseDomainException("finbase.financial_context_unavailable", "Financial rounding context is unavailable for this legal entity and currency.");
        var allocationRule = context.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.PaymentAllocation)
            ?? throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "The financial policy has no payment-allocation rounding rule.");
        var policy = RoundingPolicySnapshot.Create(context.RoundingPolicy.Id, context.RoundingPolicy.Version,
            context.RoundingPolicy.MoneyScale, Enum.Parse<LineDocumentRoundingStrategy>(context.RoundingPolicy.LineDocumentStrategy.ToString(), true),
            context.RoundingPolicy.ContextRules.Select(rule => new RoundingPolicyContextRule(Enum.Parse<RoundingContext>(rule.Context.ToString(), true), Enum.Parse<RoundingMode>(rule.Mode.ToString(), true))));
        if (!Enum.TryParse<RoundingMode>(allocationRule.Mode.ToString(), out _))
            throw new FinanceBaseDomainException("finbase.rounding_policy_invalid", "Payment-allocation rounding mode is unsupported.");
        var rules = term.Rules.Select(rule => new PaymentTermRuleInput(rule.Sequence,
            Enum.Parse<PaymentAllocation>(rule.AllocationType, true), ParseOptional(rule.PercentageRatio), ParseOptional(rule.FixedAmount),
            Enum.Parse<DueDateBasis>(rule.DueDateBasis, true), rule.Days, rule.DayOfMonth, rule.MonthOffset)).ToArray();
        var lines = PaymentScheduleCalculator.Calculate(total, basisDate, rules, policy)
            .Select(line => new PaymentScheduleLineContract(line.Sequence, line.DueDate,
                new MoneyContract(line.Amount.ToString("0.########", CultureInfo.InvariantCulture), transactionCurrencyCode.Trim().ToUpperInvariant())))
            .ToArray();
        return new(term, lines, context.RoundingPolicy, context.CapturedAt);
    }

    public async Task<CommercialPriceConversionContract> ConvertCommercialUnitPriceAsync(TenantId tenantId, LegalEntityId legalEntityId, string amount, string fromCurrencyCode, string toCurrencyCode, FinancialCalculationPurpose purpose, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var converted = await repository.ConvertAsync(tenantId, new ConvertMoneyRequest(legalEntityId.Value, amount, fromCurrencyCode, toCurrencyCode, ExchangeRatePurpose.Commercial, businessDate), cancellationToken);
        return new(converted.SourceAmount, converted.SourceCurrency, converted.TargetAmount, converted.TargetCurrency, converted.Rate, Map(converted.Snapshot, purpose));
    }

    private static ExchangeRateSnapshotContract? Map(ExchangeRateSnapshotDto? snapshot, FinancialCalculationPurpose purpose) => snapshot is null ? null : new(snapshot.TenantId, snapshot.LegalEntityId, snapshot.ApprovedRateId, snapshot.FromCurrency, snapshot.ToCurrency, snapshot.Rate, purpose, snapshot.EffectiveDate, snapshot.Source, snapshot.Version, snapshot.ManualOverride, snapshot.CapturedAt);
    private static ExchangeRatePurpose MapPurpose(FinancialCalculationPurpose purpose) => purpose switch { FinancialCalculationPurpose.Purchase => ExchangeRatePurpose.Commercial, FinancialCalculationPurpose.Tax => ExchangeRatePurpose.Tax, FinancialCalculationPurpose.Accounting => ExchangeRatePurpose.Accounting, _ => ExchangeRatePurpose.Commercial };
    private static decimal? ParseOptional(string? value) => value is null ? null : decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
