using System.Globalization;
using Modules.Tax.Entities;
using NodaTime;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Tax;
using Shared.Primitives.Financial;
using Xunit;

namespace Tax.Tests;

public sealed class TaxCalculatorTests
{
    private static readonly Guid RuleId = Guid.Parse("c715b719-20a8-4bcb-9fbb-29ab674527ea");
    private static readonly LocalDate EffectiveDate = new(2026, 1, 1);

    [Fact]
    public void TaxExclusivePercentageCalculatesFromNet()
    {
        var result = Calculate(Context(100m, TaxPriceMode.TaxExclusive), Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]));
        Assert.Equal("100", result.NetAmount);
        Assert.Equal("10", result.TaxTotal);
        Assert.Equal("110", result.GrossAmount);
    }

    [Fact]
    public void TaxInclusivePercentageIsExtractedFromGross()
    {
        var result = Calculate(Context(110m, TaxPriceMode.TaxInclusive), Rule("StandardRated", [Component(TaxCalculationMethod.PercentageExtractedFromGross, 10m)]));
        Assert.Equal("100", result.NetAmount);
        Assert.Equal("10", result.TaxTotal);
        Assert.Equal("110", result.GrossAmount);
    }

    [Fact]
    public void TaxInclusiveDiscountReducesTheTaxableGrossBeforeExtraction()
    {
        var context = Context(110m, TaxPriceMode.TaxInclusive) with { DiscountAmount = 10m };
        var result = Calculate(context, Rule("StandardRated", [Component(TaxCalculationMethod.PercentageExtractedFromGross, 10m)]));
        Assert.Equal("90.91", result.NetAmount);
        Assert.Equal("9.09", result.TaxTotal);
        Assert.Equal("100", result.GrossAmount);
    }

    [Fact]
    public void EqualRankContradictoryRulesAlwaysFailRegardlessOfInputOrder()
    {
        var standard = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]) with { Priority = 4 };
        var exempt = Rule("Exempt", [], TaxTreatment.Exempt) with { Id = Guid.NewGuid(), Priority = 4 };
        foreach (var candidates in new[] { new[] { standard, exempt }, new[] { exempt, standard } })
        {
            var exception = Assert.Throws<TaxException>(() => TaxRuleResolver.Select(candidates.Select(rule => new ResolvedTaxRuleCandidate(rule, "ENGINE-TEST", "1"))));
            Assert.Equal("tax.configuration_conflict", exception.Code);
        }
    }

    [Fact]
    public void MoreSpecificRuleWinsWithoutUsingInsertionOrder()
    {
        var broad = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]) with { Priority = 1 };
        var specific = broad with { Id = Guid.NewGuid(), Priority = 1, ProductCategoryId = Guid.NewGuid() };
        var selected = TaxRuleResolver.Select([new(specific, "ENGINE-TEST", "1"), new(broad, "ENGINE-TEST", "1")]);
        Assert.Equal(specific.Id, selected!.Rule.Id);
    }

    [Fact]
    public void RuleSetLifecycleRejectsEditingAnActiveVersion()
    {
        Assert.Throws<TaxException>(() => TaxRuleSetLifecycle.Transition(TaxRuleSetStatus.Active, TaxRuleSetStatus.Draft));
        Assert.Equal(TaxRuleSetStatus.Superseded, TaxRuleSetLifecycle.Transition(TaxRuleSetStatus.Active, TaxRuleSetStatus.Superseded));
        Assert.Equal(TaxRuleSetStatus.Scheduled, TaxRuleSetLifecycle.Transition(TaxRuleSetStatus.Approved, TaxRuleSetStatus.Scheduled));
        Assert.Equal(TaxRuleSetStatus.Active, TaxRuleSetLifecycle.Transition(TaxRuleSetStatus.Scheduled, TaxRuleSetStatus.Active));
    }

    [Fact]
    public void CountryPackRulesRejectTenantEdits()
    {
        var exception = Assert.Throws<TaxException>(() => TaxRuleSetLifecycle.EnsureEditable("CountryPack", false, true));
        Assert.Equal("tax.rule_locked", exception.Code);
        TaxRuleSetLifecycle.EnsureEditable("TenantConfiguration", true, false);
    }

    [Fact]
    public void EffectiveDatesAreInclusiveAtStartAndExclusiveAtEnd()
    {
        Assert.False(TaxEffectivePeriod.Contains(new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 1), null));
        Assert.True(TaxEffectivePeriod.Contains(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)));
        Assert.False(TaxEffectivePeriod.Contains(new DateOnly(2026, 3, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void ProfileResolverPrefersVariantAndRejectsEqualRankConflict()
    {
        var template = new TaxProfileCandidate(Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), new DateOnly(2026, 1, 1), null);
        var variant = new TaxProfileCandidate(Guid.NewGuid(), Guid.Parse("a5a9be1c-5174-44d6-aa55-05c2918d51c0"), null, null, null, Guid.NewGuid(), new DateOnly(2026, 1, 1), null);
        var chosen = TaxProfileResolver.Select([template, variant], variant.VariantId, Guid.NewGuid(), "EG", "VAT", new DateOnly(2026, 2, 1));
        Assert.Equal(variant.Id, chosen!.Id);
        var conflicting = variant with { Id = Guid.NewGuid(), CategoryId = Guid.NewGuid() };
        Assert.Throws<TaxException>(() => TaxProfileResolver.Select([variant, conflicting], variant.VariantId, Guid.NewGuid(), "EG", "VAT", new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void ProfileResolverHonorsEffectiveBoundaryAndLegalEntityScope()
    {
        var category = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var profile = new TaxProfileCandidate(Guid.NewGuid(), null, entity, "EG", "VAT", category, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
        Assert.Equal(profile, TaxProfileResolver.Select([profile], null, entity, "EG", "VAT", new DateOnly(2026, 1, 31)));
        Assert.Null(TaxProfileResolver.Select([profile], null, entity, "EG", "VAT", new DateOnly(2026, 2, 1)));
        Assert.Null(TaxProfileResolver.Select([profile], null, Guid.NewGuid(), "EG", "VAT", new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void RegistrationMembershipPeriodsRejectOverlapAtTheBoundary()
    {
        Assert.True(TaxEffectivePeriod.Overlaps(new DateOnly(2026, 1, 1), null, new DateOnly(2026, 2, 1), null));
        Assert.False(TaxEffectivePeriod.Overlaps(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 1), null));
    }

    [Fact]
    public void RegistrationStatusTransitionsRetainDeregisteredHistory()
    {
        TaxRegistrationLifecycle.ValidateTransition(TaxRegistrationStatus.Registered, TaxRegistrationStatus.Deregistered);
        TaxRegistrationLifecycle.ValidateTransition(TaxRegistrationStatus.PendingRegistration, TaxRegistrationStatus.Registered);
        Assert.Throws<TaxException>(() => TaxRegistrationLifecycle.ValidateTransition(TaxRegistrationStatus.Deregistered, TaxRegistrationStatus.Registered));
    }

    [Fact]
    public void RegistrationAndMembershipEffectiveLookupsUseHalfOpenPeriods()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 2, 1);
        Assert.True(TaxEffectivePeriod.Contains(start, start, end));
        Assert.True(TaxEffectivePeriod.Contains(new DateOnly(2026, 1, 31), start, end));
        Assert.False(TaxEffectivePeriod.Contains(end, start, end));
        Assert.False(TaxEffectivePeriod.Overlaps(start, end, end, null));
        TaxRegistrationLifecycle.ValidateTransition(TaxRegistrationStatus.Registered, TaxRegistrationStatus.Deregistered);
        Assert.Throws<TaxException>(() => TaxRegistrationLifecycle.ValidateTransition(TaxRegistrationStatus.Deregistered, TaxRegistrationStatus.Registered));
    }

    [Theory]
    [InlineData("2025-12-31", false)]
    [InlineData("2026-01-01", true)]
    [InlineData("2026-01-31", true)]
    [InlineData("2026-02-01", false)]
    public void RuleEffectivePeriodUsesInclusiveStartAndExclusiveEnd(string date, bool expected)
    {
        var rule = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]) with { EffectiveTo = new DateOnly(2026, 2, 1) };
        var parsedDate = DateOnly.Parse(date, CultureInfo.InvariantCulture);
        var shouldCalculate = parsedDate >= rule.EffectiveFrom && rule.EffectiveTo is { } end && parsedDate < end;
        Assert.Equal(expected, shouldCalculate);
    }

    [Theory]
    [InlineData(TaxTreatment.ZeroRated)]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.OutOfScope)]
    public void ZeroAmountTreatmentsRemainDistinct(TaxTreatment treatment)
    {
        var rule = Rule(treatment.ToString(), [], treatment);
        var result = TaxCalculator.Calculate(Context(100m, TaxPriceMode.TaxExclusive), "2026-01-01", "NotRegistered", rule, "ENGINE-DEMO", "fixture-v1", null, "category");
        Assert.Equal(treatment, result.Treatment);
        Assert.Equal("0", result.TaxTotal);
        Assert.Empty(result.TaxLines);
    }

    [Fact]
    public void CapabilityDisabledIsNotAZeroRatedEvaluation()
    {
        var result = TaxCalculator.Disabled(Context(110m, TaxPriceMode.TaxInclusive), "2026-01-01");
        Assert.Equal(TaxEvaluationState.NotEvaluatedBecauseCapabilityDisabled, result.EvaluationState);
        Assert.Null(result.Treatment);
        Assert.Equal("110", result.NetAmount);
        Assert.Equal("110", result.GrossAmount);
        Assert.Equal("0", result.TaxTotal);
        Assert.Empty(result.TaxLines);
    }

    [Fact]
    public void DisabledCapabilityPassesTheCommercialTotalThroughAfterDiscountAndCharges()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive) with { DiscountAmount = 10m, TaxableCharges = 5m };
        var result = TaxCalculator.Disabled(context, "2026-01-01");
        Assert.Equal("95", result.NetAmount);
        Assert.Equal("95", result.GrossAmount);
        Assert.Empty(result.TaxLines);
    }

    [Fact]
    public void FixedAndCompoundComponentsUseOrderedBases()
    {
        var first = Component(TaxCalculationMethod.PercentageOfNet, 10m, sequence: 1);
        var second = Component(TaxCalculationMethod.PercentageOfModifiedBase, 5m, sequence: 2, baseSequence: 1);
        var fixedTax = Component(TaxCalculationMethod.FixedPerUnit, null, fixedAmount: 2m, sequence: 3);
        var rule = Rule("SpecialScheme", [first, second, fixedTax], TaxTreatment.SpecialScheme);
        var result = Calculate(Context(100m, TaxPriceMode.TaxExclusive) with { Quantity = 3m }, rule);
        Assert.Equal("10", result.TaxLines[0].TaxAmount);
        Assert.Equal("5.5", result.TaxLines[1].TaxAmount);
        Assert.Equal("6", result.TaxLines[2].TaxAmount);
        Assert.Equal("21.5", result.TaxTotal);
    }

    [Fact]
    public void AllFiveTypedCalculationMethodsUseExactDecimals()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive) with { Quantity = 3m };
        var components = new[]
        {
            Component(TaxCalculationMethod.PercentageOfNet, 10m, sequence: 1),
            Component(TaxCalculationMethod.PercentageExtractedFromGross, 10m, sequence: 2),
            Component(TaxCalculationMethod.FixedPerUnit, null, fixedAmount: 2m, sequence: 3),
            Component(TaxCalculationMethod.FixedPerLine, null, fixedAmount: 4m, sequence: 4),
            Component(TaxCalculationMethod.PercentageOfModifiedBase, 5m, sequence: 5, baseSequence: 1)
        };
        var result = Calculate(context, Rule("SpecialScheme", components, TaxTreatment.SpecialScheme));
        Assert.Collection(result.TaxLines,
            line => Assert.Equal("10", line.TaxAmount),
            line => Assert.Equal("10", line.TaxAmount),
            line => Assert.Equal("6", line.TaxAmount),
            line => Assert.Equal("4", line.TaxAmount),
            line => Assert.Equal("5.5", line.TaxAmount));
    }

    [Fact]
    public void ParallelComponentsPreserveLinesAndReconcileRealResidual()
    {
        var first = Component(TaxCalculationMethod.PercentageOfNet, 10m, sequence: 1);
        var second = Component(TaxCalculationMethod.PercentageOfNet, 10m, sequence: 2);
        var result = TaxCalculator.Calculate(Context(0.04m, TaxPriceMode.TaxExclusive), "2026-01-01", "Registered", Rule("SpecialScheme", [first, second], TaxTreatment.SpecialScheme), "ENGINE-DEMO", "fixture-v1", null, "general");
        Assert.Equal(2, result.TaxLines.Count);
        Assert.Equal("0", result.TaxLines[0].TaxAmount);
        Assert.Equal("0.01", result.TaxLines[1].TaxAmount);
        Assert.Equal("0.01", result.TaxTotal);
        Assert.Equal("0.01", result.RoundingEvidence!.Residual);
        Assert.Equal(second.TaxCode, result.RoundingEvidence.AllocationTarget);
    }

    [Fact]
    public void ComponentDependencyCyclesAndImplicitModifiedBasesAreRejected()
    {
        var first = Component(TaxCalculationMethod.PercentageOfModifiedBase, 10m, sequence: 1, baseSequence: 2);
        var second = Component(TaxCalculationMethod.PercentageOfModifiedBase, 10m, sequence: 2, baseSequence: 1);
        Assert.Equal("tax.component_cycle", Assert.Throws<TaxException>(() => Calculate(Context(100m, TaxPriceMode.TaxExclusive), Rule("SpecialScheme", [first, second], TaxTreatment.SpecialScheme))).Code);
        var implicitBase = Component(TaxCalculationMethod.PercentageOfModifiedBase, 10m);
        Assert.Equal("tax.component_base_required", Assert.Throws<TaxException>(() => Calculate(Context(100m, TaxPriceMode.TaxExclusive), Rule("SpecialScheme", [implicitBase], TaxTreatment.SpecialScheme))).Code);
    }

    [Fact]
    public void CalculatorIsDeterministicForIdenticalResolvedInputs()
    {
        var context = Context(123456789012345678.12345678m, TaxPriceMode.TaxExclusive);
        var rule = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 17.125m)]);
        var first = Calculate(context, rule);
        var second = Calculate(context, rule);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(second));
        Assert.Equal("21141975118364197.38", first.TaxTotal);
    }

    [Fact]
    public void FinBaseRoundingModesHandleMidpointAndSmallPrecisionExplicitly()
    {
        var context = Context(0.05m, TaxPriceMode.TaxExclusive);
        var rule = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]);
        var halfUp = TaxCalculator.Calculate(context, "2026-01-01", "Registered", rule, "ENGINE-DEMO", "fixture-v1", null, "general", TaxRoundingStrategy.PerLine, 2, FinancialRoundingMode.HalfUp, roundingPolicyVersion: "12");
        var halfEven = TaxCalculator.Calculate(context, "2026-01-01", "Registered", rule, "ENGINE-DEMO", "fixture-v1", null, "general", TaxRoundingStrategy.PerLine, 2, FinancialRoundingMode.HalfEven, roundingPolicyVersion: "13");
        Assert.Equal("0.01", halfUp.TaxTotal);
        Assert.Equal("0", halfEven.TaxTotal);
        Assert.Equal("12", halfUp.RoundingPolicyVersion);
        Assert.Equal("0.01", halfUp.RoundingEvidence!.FinalTotal);
        Assert.Equal(1.24m, FinancialRounding.Round(1.231m, 2, FinancialRoundingModeValue.Up));
        Assert.Equal(1.23m, FinancialRounding.Round(1.239m, 2, FinancialRoundingModeValue.Down));
    }

    [Fact]
    public void CreditNoteProducesSignedReversalFromPositiveCommercialInput()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive) with { DocumentSemantics = TaxDocumentSemantics.CreditNote };
        var result = Calculate(context, Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]));
        Assert.Equal("-100", result.NetAmount);
        Assert.Equal("-10", result.TaxTotal);
        Assert.Equal("-110", result.GrossAmount);
        Assert.Equal("-10", result.TaxLines[0].TaxAmount);
    }

    [Fact]
    public void AccountingFactCarriesTaxFactsWithoutOwningLedgerAccounts()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive) with { ProductTaxCategoryId = Guid.NewGuid() };
        var line = Calculate(context, Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)])).TaxLines[0];
        var fact = TaxCalculator.ToAccountingFact(line, "2026-01-01", context, "Invoice", Guid.NewGuid());
        Assert.Equal(context.ProductTaxCategoryId, fact.ProductTaxCategoryId);
        Assert.Equal("Invoice", fact.SourceDocumentKind);
        Assert.DoesNotContain(typeof(TaxAccountingFact).GetProperties(), property => property.Name.Contains("Account", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoverabilityPreservesRecoverableAndNonRecoverableFacts()
    {
        var component = Component(TaxCalculationMethod.PercentageOfNet, 10m) with { RecoverablePercentage = 50m };
        var result = Calculate(Context(100m, TaxPriceMode.TaxExclusive), Rule("StandardRated", [component]));
        var recoverability = result.TaxLines[0].Recoverability!;
        Assert.Equal(TaxRecoverabilityKind.PartiallyRecoverable, recoverability.Kind);
        Assert.Equal("5", recoverability.RecoverableAmount);
        Assert.Equal("5", recoverability.NonRecoverableAmount);
        var fact = TaxCalculator.ToAccountingFact(result.TaxLines[0], "2026-01-01");
        Assert.Equal("5", fact.RecoverableAmount);
        Assert.Equal("5", fact.NonRecoverableAmount);
        Assert.Equal("100", fact.TaxableBase);
        Assert.Equal("DEMO", fact.TaxTypeCode);
    }

    [Theory]
    [InlineData(0, TaxRecoverabilityKind.NonRecoverable, "0", "10")]
    [InlineData(100, TaxRecoverabilityKind.Recoverable, "10", "0")]
    public void PurchaseRecoverabilityHandlesFullyRecoverableAndNonRecoverable(int percentage, TaxRecoverabilityKind kind, string recoverable, string nonRecoverable)
    {
        var component = Component(TaxCalculationMethod.PercentageOfNet, 10m) with { RecoverablePercentage = percentage };
        var result = Calculate(Context(100m, TaxPriceMode.TaxExclusive) with { Direction = TaxDirection.Purchase }, Rule("StandardRated", [component]));
        Assert.Equal(kind, result.TaxLines[0].Recoverability!.Kind);
        Assert.Equal(recoverable, result.TaxLines[0].Recoverability!.RecoverableAmount);
        Assert.Equal(nonRecoverable, result.TaxLines[0].Recoverability!.NonRecoverableAmount);
    }

    [Fact]
    public void TaxDateUsesExplicitPolicyAndDoesNotReadCurrentDate()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive) with { TaxDate = null, SupplyDate = new LocalDate(2026, 2, 1), PostingDate = new LocalDate(2026, 3, 1) };
        var resolution = TaxDateResolver.Resolve(context, new(TaxDateBasis.SupplyDate, "Africa/Cairo", 3));
        Assert.Equal(new LocalDate(2026, 2, 1), resolution.Date);
        Assert.Equal("Legal entity Tax Date policy uses Supply Date", resolution.Reason);
    }

    [Fact]
    public void SnapshotValueRemainsStableWhenTheActiveRuleChanges()
    {
        var context = Context(100m, TaxPriceMode.TaxExclusive);
        var originalRule = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 10m)]);
        var changedRule = Rule("StandardRated", [Component(TaxCalculationMethod.PercentageOfNet, 12m)]) with { Id = Guid.NewGuid(), RuleVersion = "2" };
        var originalResult = Calculate(context, originalRule);
        var snapshot = new TaxSnapshotContract(Guid.NewGuid(), "Invoice", Guid.NewGuid(), Guid.NewGuid(), "2026-01-01", originalResult.EvaluationState.ToString(), originalResult.RuleSetVersion, originalResult.CalculationVersion,
            new Shared.Contracts.Tax.TaxContextContract(new Shared.Primitives.Identifiers.TenantId(context.TenantId), new Shared.Primitives.Identifiers.LegalEntityId(context.LegalEntityId), "Sales", "Invoice", "Domestic", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, null, null, null, null, null, null, null, 1m, null, "TaxExclusive", "100", "0", "0", "EGP", "EGP", null),
            new Shared.Contracts.Tax.TaxResultContract(originalResult.EvaluationState.ToString(), originalResult.Treatment?.ToString(), originalResult.NetAmount, originalResult.TaxTotal, originalResult.GrossAmount, originalResult.TaxLines.Select(line => new Shared.Contracts.Tax.TaxLineContract(line.TaxTypeCode, line.TaxCode, line.TreatmentCode, line.Rate, line.FixedAmountPerUnit, line.TaxableBase, line.TaxAmount, line.Currency, line.Jurisdiction, line.LegalReasonCode, line.LegalReasonText, line.ReportingCode, line.RuleId, line.RuleVersion)).ToArray(), originalResult.JurisdictionSnapshot, originalResult.RegistrationSnapshot, originalResult.RuleSetVersion, originalResult.CalculationVersion, new(originalResult.Explanation.CountryPackCode, originalResult.Explanation.CountryPackVersion, originalResult.Explanation.TaxDate, originalResult.Explanation.Jurisdiction, originalResult.Explanation.RegistrationStatus, originalResult.Explanation.PartyTreatment, originalResult.Explanation.ProductCategory, originalResult.Explanation.MatchedRuleId, originalResult.Explanation.ComponentCodes, originalResult.Explanation.Rounding, originalResult.Explanation.Summary)), DateTimeOffset.UnixEpoch);

        var changedResult = Calculate(context, changedRule);

        Assert.Equal("10", snapshot.Result.TaxTotal);
        Assert.Equal("12", changedResult.TaxTotal);
        Assert.Equal("1", snapshot.Result.TaxLines[0].RuleVersion);
    }

    private static TaxResult Calculate(TaxCalculationContext context, TaxRuleCandidate rule) => TaxCalculator.Calculate(context, "2026-01-01", "Registered", rule, "ENGINE-DEMO", "fixture-v1", null, "general");
    private static TaxCalculationContext Context(decimal amount, TaxPriceMode mode) => new(
        TenantId: Guid.NewGuid(), LegalEntityId: Guid.NewGuid(), Direction: TaxDirection.Sales, DocumentKind: "Invoice", TransactionType: "Domestic",
        DocumentDate: EffectiveDate, TaxDate: EffectiveDate, SupplyDate: null, PostingDate: null, PartyId: null, PartyCountryCode: null, PlaceOfSupplyCountryCode: null,
        PartyTaxCategoryId: null, JurisdictionCode: null, ProductTemplateId: null, ProductVariantId: null, ProductTaxCategoryId: null, Quantity: 1m, UnitOfMeasureId: null,
        PriceMode: mode, CommercialAmount: amount, DiscountAmount: 0m, TaxableCharges: 0m, TransactionCurrency: "EGP", BaseCurrency: "EGP", RegistrationId: null);
    private static TaxRuleCandidate Rule(string code, IReadOnlyList<TaxComponent> components, TaxTreatment? treatment = null) => new(
        Id: RuleId, Treatment: treatment ?? Enum.Parse<TaxTreatment>(code), Priority: 0, EffectiveFrom: new DateOnly(2026, 1, 1), EffectiveTo: null,
        RegistrationId: null, JurisdictionCode: null, Direction: null, PartyCategoryId: null, ProductCategoryId: null, DocumentKind: null, TransactionType: null,
        PlaceOfSupplyCountryCode: null, Mutability: TaxRuleMutability.TenantConfiguration, RuleVersion: "1", LegalReasonCode: null, LegalReasonText: "Development fixture only", Components: components);
    private static TaxComponent Component(TaxCalculationMethod method, decimal? rate, decimal? fixedAmount = null, int sequence = 1, int? baseSequence = null) => new(Guid.NewGuid(), "DEMO", "DEMO-TAX", TaxTreatment.StandardRated, method, rate, fixedAmount, "EGP", sequence, baseSequence);
}
