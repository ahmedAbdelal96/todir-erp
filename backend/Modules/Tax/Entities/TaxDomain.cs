using System.Globalization;
using NodaTime;
using Shared.Contracts.FinanceBase;
using Shared.Primitives.Financial;

namespace Modules.Tax.Entities;

public enum TaxCapability { Disabled, Enabled }
public enum TaxEvaluationState { NotEvaluatedBecauseCapabilityDisabled, Evaluated }
public enum TaxDirection { Sales, Purchase }
public enum TaxPriceMode { TaxExclusive, TaxInclusive }
public enum TaxTreatment { StandardRated, ReducedRated, ZeroRated, Exempt, OutOfScope, ReverseCharge, Import, Export, SpecialScheme }
public enum TaxCalculationMethod { PercentageOfNet, PercentageExtractedFromGross, FixedPerUnit, FixedPerLine, PercentageOfModifiedBase }
public enum TaxRuleMutability { CountryPack, TenantConfiguration, GovernmentIntegrationSystemReference }
public enum TaxRuleSetStatus { Draft, Approved, Scheduled, Active, Superseded, Retired }
public enum TaxRegistrationStatus { NotRegistered, PendingRegistration, Registered, RegistrationException, Deregistered }
public enum TaxRecoverabilityKind { Recoverable, NonRecoverable, PartiallyRecoverable }
public enum TaxRoundingStrategy { PerLine, PerTaxComponent, PerDocument }
public enum TaxDateBasis { Explicit, SupplyDate, DocumentDate, PostingDate }
public enum TaxDocumentSemantics { Invoice, PurchaseInvoice, CreditNote, PurchaseCredit }

public static class TaxRegistrationLifecycle
{
    public static void ValidateTransition(TaxRegistrationStatus current,TaxRegistrationStatus target)
    {
        if(current==target)return;
        var allowed=current switch
        {
            TaxRegistrationStatus.NotRegistered=>target is TaxRegistrationStatus.PendingRegistration or TaxRegistrationStatus.Registered or TaxRegistrationStatus.RegistrationException,
            TaxRegistrationStatus.PendingRegistration=>target is TaxRegistrationStatus.Registered or TaxRegistrationStatus.RegistrationException or TaxRegistrationStatus.Deregistered,
            TaxRegistrationStatus.Registered=>target is TaxRegistrationStatus.RegistrationException or TaxRegistrationStatus.Deregistered,
            TaxRegistrationStatus.RegistrationException=>target is TaxRegistrationStatus.PendingRegistration or TaxRegistrationStatus.Registered or TaxRegistrationStatus.Deregistered,
            TaxRegistrationStatus.Deregistered=>false,
            _=>false
        };
        if(!allowed)throw new TaxException("tax.registration_transition_invalid",$"A Tax Registration cannot transition from {current} to {target}. Create a new registration series after deregistration.");
    }
}

public static class TaxRuleSetLifecycle
{
    public static TaxRuleSetStatus Transition(TaxRuleSetStatus current, TaxRuleSetStatus target)
    {
        var allowed = current switch
        {
            TaxRuleSetStatus.Draft => target is TaxRuleSetStatus.Approved or TaxRuleSetStatus.Scheduled or TaxRuleSetStatus.Retired,
            TaxRuleSetStatus.Approved => target is TaxRuleSetStatus.Scheduled or TaxRuleSetStatus.Active or TaxRuleSetStatus.Retired,
            TaxRuleSetStatus.Scheduled => target is TaxRuleSetStatus.Active or TaxRuleSetStatus.Retired,
            TaxRuleSetStatus.Active => target is TaxRuleSetStatus.Superseded or TaxRuleSetStatus.Retired,
            TaxRuleSetStatus.Superseded => target == TaxRuleSetStatus.Retired,
            _ => false
        };
        if (!allowed) throw new TaxException("tax.rule_set_transition_invalid", $"A Tax Rule Set cannot transition from {current} to {target}.");
        return target;
    }

    public static void EnsureEditable(string sourceKind, bool mutable, bool lockedByCountryPack)
    {
        if (lockedByCountryPack || sourceKind == "CountryPack" || !mutable)
            throw new TaxException("tax.rule_locked", "This Tax rule is locked by its source and cannot be changed by tenant administration.");
    }
}

public static class TaxEffectivePeriod
{
    public static bool Contains(DateOnly date, DateOnly from, DateOnly? to) => date >= from && (to is null || date < to.Value);

    public static bool Overlaps(DateOnly firstFrom, DateOnly? firstTo, DateOnly secondFrom, DateOnly? secondTo) =>
        (firstTo is null || secondFrom < firstTo.Value) && (secondTo is null || firstFrom < secondTo.Value);
}

public sealed record TaxProfileCandidate(Guid Id, Guid? VariantId, Guid? LegalEntityId, string? CountryCode, string? SchemeCode, Guid CategoryId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public static class TaxProfileResolver
{
    public static TaxProfileCandidate? Select(IEnumerable<TaxProfileCandidate> candidates, Guid? variantId, Guid legalEntityId, string? countryCode, string? schemeCode, DateOnly taxDate)
    {
        var matching = candidates.Where(profile => TaxEffectivePeriod.Contains(taxDate, profile.EffectiveFrom, profile.EffectiveTo)
            && (profile.VariantId is null || profile.VariantId == variantId)
            && (profile.LegalEntityId is null || profile.LegalEntityId == legalEntityId)
            && (profile.CountryCode is null || profile.CountryCode == countryCode)
            && (profile.SchemeCode is null || profile.SchemeCode == schemeCode)).ToArray();
        if (matching.Length == 0) return null;
        static (int Variant, int Entity, int Country, int Scheme) Rank(TaxProfileCandidate p) => (p.VariantId is null ? 0 : 1,p.LegalEntityId is null ? 0 : 1,p.CountryCode is null ? 0 : 1,p.SchemeCode is null ? 0 : 1);
        var rank = matching.Select(Rank).OrderByDescending(value => value.Variant).ThenByDescending(value => value.Entity).ThenByDescending(value => value.Country).ThenByDescending(value => value.Scheme).First();
        var best = matching.Where(profile => Rank(profile) == rank).ToArray();
        if (best.Length > 1 && best.Any(profile => profile.CategoryId != best[0].CategoryId)) throw new TaxException("tax.profile_conflict", "Several equally scoped Tax Profiles match this context.");
        return best[0];
    }
}

public sealed class TaxException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record TaxCalculationContext(
    Guid TenantId,
    Guid LegalEntityId,
    TaxDirection Direction,
    string DocumentKind,
    string TransactionType,
    LocalDate DocumentDate,
    LocalDate? TaxDate,
    LocalDate? SupplyDate,
    LocalDate? PostingDate,
    Guid? PartyId,
    string? PartyCountryCode,
    string? PlaceOfSupplyCountryCode,
    Guid? PartyTaxCategoryId,
    string? JurisdictionCode,
    Guid? ProductTemplateId,
    Guid? ProductVariantId,
    Guid? ProductTaxCategoryId,
    decimal Quantity,
    Guid? UnitOfMeasureId,
    TaxPriceMode PriceMode,
    decimal CommercialAmount,
    decimal DiscountAmount,
    decimal TaxableCharges,
    string TransactionCurrency,
    string BaseCurrency,
    Guid? RegistrationId,
    LocalDate? ExplicitTaxDate = null,
    string? TaxSchemeCode = null,
    TaxDocumentSemantics DocumentSemantics = TaxDocumentSemantics.Invoice,
    string? QuantitySnapshot = null,
    IReadOnlyDictionary<string, string>? Dimensions = null);

public sealed record TaxDatePolicy(TaxDateBasis Basis, string LegalEntityTimeZone, long Version);
public sealed record TaxDateResolution(LocalDate Date, string Reason, long PolicyVersion);

public static class TaxDateResolver
{
    public static TaxDateResolution Resolve(TaxCalculationContext context, TaxDatePolicy policy)
    {
        if (context.ExplicitTaxDate is { } explicitDate) return new(explicitDate, "Explicit Tax Date override", policy.Version);
        if (context.TaxDate is { } suppliedTaxDate) return new(suppliedTaxDate, "Tax Date supplied by the consuming document", policy.Version);
        var resolved = policy.Basis switch
        {
            TaxDateBasis.SupplyDate when context.SupplyDate is { } supply => new TaxDateResolution(supply, "Legal entity Tax Date policy uses Supply Date", policy.Version),
            TaxDateBasis.DocumentDate => new TaxDateResolution(context.DocumentDate, "Legal entity Tax Date policy uses Document Date", policy.Version),
            TaxDateBasis.PostingDate when context.PostingDate is { } posting => new TaxDateResolution(posting, "Legal entity Tax Date policy uses Posting Date", policy.Version),
            _ => throw new TaxException("tax.tax_date_unresolved", "Tax Date is required because no effective Tax Date rule can resolve it from the supplied document dates.")
        };
        return resolved;
    }

    public static LocalDate FromInstant(Instant instant, string legalEntityTimeZone)
    {
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(legalEntityTimeZone)
            ?? throw new TaxException("tax.timezone_invalid", "The legal entity time zone is not supported.");
        return instant.InZone(zone).Date;
    }
}

public sealed record TaxRuleCandidate(
    Guid Id,
    TaxTreatment Treatment,
    int Priority,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid? RegistrationId,
    string? JurisdictionCode,
    TaxDirection? Direction,
    Guid? PartyCategoryId,
    Guid? ProductCategoryId,
    string? DocumentKind,
    string? TransactionType,
    string? PlaceOfSupplyCountryCode,
    TaxRuleMutability Mutability,
    string RuleVersion,
    string? LegalReasonCode,
    string? LegalReasonText,
    IReadOnlyList<TaxComponent> Components);

public sealed record ResolvedTaxRuleCandidate(TaxRuleCandidate Rule, string PackCode, string PackVersion);

public static class TaxRuleResolver
{
    public static ResolvedTaxRuleCandidate? Select(IEnumerable<ResolvedTaxRuleCandidate> source)
    {
        var candidates = source.ToArray();
        if (candidates.Length == 0) return null;
        static (int Priority, int Specificity) Rank(TaxRuleCandidate rule) =>
            (rule.Priority,
                (rule.RegistrationId is null ? 0 : 1) + (rule.JurisdictionCode is null ? 0 : 1) + (rule.Direction is null ? 0 : 1)
                + (rule.PartyCategoryId is null ? 0 : 1) + (rule.ProductCategoryId is null ? 0 : 1) + (rule.DocumentKind is null ? 0 : 1)
                + (rule.TransactionType is null ? 0 : 1) + (rule.PlaceOfSupplyCountryCode is null ? 0 : 1));
        var bestRank = candidates.Select(candidate => Rank(candidate.Rule)).OrderByDescending(rank => rank.Priority).ThenByDescending(rank => rank.Specificity).First();
        var best = candidates.Where(candidate => Rank(candidate.Rule) == bestRank).ToArray();
        if (best.Length != 1)
            throw new TaxException("tax.configuration_conflict", "Several equally specific Tax rules match this context. Configure one unambiguous rule before using it.");
        return best[0];
    }
}

public sealed record TaxComponent(
    Guid Id,
    string TaxTypeCode,
    string TaxCode,
    TaxTreatment Treatment,
    TaxCalculationMethod Method,
    decimal? Rate,
    decimal? FixedAmount,
    string CurrencyCode,
    int Sequence,
    int? BaseComponentSequence,
    string? LegalReasonCode = null,
    string? ReportingCode = null,
    decimal? RecoverablePercentage = null);

public sealed record TaxLine(
    string TaxTypeCode,
    string TaxCode,
    string TreatmentCode,
    string? Rate,
    string? FixedAmountPerUnit,
    string TaxableBase,
    string TaxAmount,
    string Currency,
    string Jurisdiction,
    string? LegalReasonCode,
    string? LegalReasonText,
    TaxRecoverability? Recoverability,
    string? ReportingCode,
    Guid RuleId,
    string RuleVersion);

public sealed record TaxRecoverability(TaxRecoverabilityKind Kind, string RecoverablePercentage, string RecoverableAmount, string NonRecoverableAmount);

public sealed record TaxExplanation(
    string CountryPackCode,
    string CountryPackVersion,
    string TaxDate,
    string Jurisdiction,
    string RegistrationStatus,
    string? PartyTreatment,
    string? ProductCategory,
    Guid? MatchedRuleId,
    IReadOnlyList<string> ComponentCodes,
    string Rounding,
    string Summary,
    string? PlaceOfSupplyReason = null,
    string? PartyEvidence = null,
    string? ProductEvidence = null,
    string? LegalReasonCode = null,
    string? LegalReasonText = null);

public sealed record TaxRoundingEvidence(
    string Strategy,
    int MoneyScale,
    string Mode,
    string UnroundedTaxTotal,
    string PreReconciliationTotal,
    string Residual,
    string AllocationTarget,
    string FinalTotal);

public sealed record TaxResult(
    TaxEvaluationState EvaluationState,
    TaxTreatment? Treatment,
    string NetAmount,
    string TaxTotal,
    string GrossAmount,
    IReadOnlyList<TaxLine> TaxLines,
    string JurisdictionSnapshot,
    string RegistrationSnapshot,
    string? RuleSetVersion,
    string CalculationVersion,
    TaxExplanation Explanation,
    string? TaxFxSnapshot = null,
    string? RoundingPolicyVersion = null,
    TaxRoundingEvidence? RoundingEvidence = null,
    string? QuantitySnapshot = null);

public sealed record TaxAccountingFact(
    string TaxTypeCode,
    string TaxCode,
    string TreatmentCode,
    string TaxableBase,
    string TaxAmount,
    string RecoverableAmount,
    string NonRecoverableAmount,
    string CurrencyCode,
    string TaxDate,
    string JurisdictionCode,
    string ReportingCode,
    Guid? PartyId = null,
    Guid? ProductTemplateId = null,
    Guid? ProductVariantId = null,
    string? SourceDocumentKind = null,
    Guid? SourceDocumentId = null,
    Guid? ProductTaxCategoryId = null,
    IReadOnlyDictionary<string, string>? Dimensions = null);

public static class TaxCalculator
{
    public const string CalculationVersion = "tax_calculation_v1";

    public static TaxResult Disabled(TaxCalculationContext context, string taxDate)
    {
        var amount = context.CommercialAmount - context.DiscountAmount + context.TaxableCharges;
        var sign = context.DocumentSemantics is TaxDocumentSemantics.CreditNote or TaxDocumentSemantics.PurchaseCredit ? -1m : 1m;
        var net = Format(amount * sign);
        return new(TaxEvaluationState.NotEvaluatedBecauseCapabilityDisabled, null, net, "0", net, [],
            context.JurisdictionCode ?? "Unresolved", "NotEvaluatedBecauseCapabilityDisabled", null, CalculationVersion,
            new("None", "None", taxDate, context.JurisdictionCode ?? "Unresolved", "NotEvaluated", null, null, null, [], "NotApplied", "Tax was not evaluated because Tax is disabled for this legal entity."));
    }

    public static TaxResult Calculate(TaxCalculationContext context, string taxDate, string registrationStatus, TaxRuleCandidate rule, string packCode, string packVersion, string? partyTreatment, string? productCategory, TaxRoundingStrategy rounding = TaxRoundingStrategy.PerLine, int moneyScale = 2, FinancialRoundingMode roundingMode = FinancialRoundingMode.HalfUp, string? taxFxSnapshot = null, string? roundingPolicyVersion = null)
    {
        if (context.TenantId == Guid.Empty || context.LegalEntityId == Guid.Empty || context.Quantity <= 0m || moneyScale is < 0 or > 8)
            throw new TaxException("tax.context_invalid", "Required tax calculation facts are missing or invalid.");
        var date = DateOnly.ParseExact(taxDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (date < rule.EffectiveFrom || rule.EffectiveTo is { } end && date >= end)
            throw new TaxException("tax.rule_not_effective", "No approved tax rule is effective for the Tax Date.");
        if (rule.Components.Count == 0 && rule.Treatment is not (TaxTreatment.ZeroRated or TaxTreatment.Exempt or TaxTreatment.OutOfScope))
            throw new TaxException("tax.rule_components_missing", "The selected tax rule has no calculation components.");

        var exactBase = context.CommercialAmount - context.DiscountAmount + context.TaxableCharges;
        if (exactBase < 0m) throw new TaxException("tax.base_negative", "Discounts cannot make the taxable amount negative.");
        var components = rule.Components.OrderBy(component => component.Sequence).ToArray();
        EnsureAcyclic(components);
        var lines = new List<TaxLine>(components.Length);
        var componentAmounts = new Dictionary<int, decimal>();
        var exactComponentTaxes = new List<(TaxComponent Component, decimal Base, decimal Amount)>();
        foreach (var component in components)
        {
            if (component.Method == TaxCalculationMethod.PercentageOfModifiedBase && component.BaseComponentSequence is null)
                throw new TaxException("tax.component_base_required", "Percentage of Modified Base requires an explicit prior component dependency.");
            var taxableBase = component.BaseComponentSequence is { } baseSequence
                ? exactBase + (componentAmounts.TryGetValue(baseSequence, out var baseTax) ? baseTax : throw new TaxException("tax.component_base_missing", "A tax component references a missing base component."))
                : exactBase;
            var exactTax = rule.Treatment is TaxTreatment.ZeroRated or TaxTreatment.Exempt or TaxTreatment.OutOfScope ? 0m : component.Method switch
            {
                TaxCalculationMethod.PercentageOfNet => taxableBase * RequiredRate(component) / 100m,
                TaxCalculationMethod.PercentageExtractedFromGross => context.PriceMode == TaxPriceMode.TaxInclusive ? taxableBase * RequiredRate(component) / (100m + RequiredRate(component)) : taxableBase * RequiredRate(component) / 100m,
                TaxCalculationMethod.FixedPerUnit => RequiredFixed(component) * context.Quantity,
                TaxCalculationMethod.FixedPerLine => RequiredFixed(component),
                TaxCalculationMethod.PercentageOfModifiedBase => taxableBase * RequiredRate(component) / 100m,
                _ => throw new TaxException("tax.calculation_method_unsupported", "The tax calculation method is not supported.")
            };
            exactComponentTaxes.Add((component, taxableBase, exactTax));
            var provisional = rounding == TaxRoundingStrategy.PerTaxComponent ? Round(exactTax, moneyScale, roundingMode) : exactTax;
            componentAmounts[component.Sequence] = provisional;
        }

        var exactTaxTotal = exactComponentTaxes.Sum(component => component.Amount);
        var taxTotal = rounding == TaxRoundingStrategy.PerTaxComponent
            ? exactComponentTaxes.Sum(component => Round(component.Amount, moneyScale, roundingMode))
            : Round(exactTaxTotal, moneyScale, roundingMode);
        var preReconciliation = exactComponentTaxes.Sum(component => rounding == TaxRoundingStrategy.PerTaxComponent
            ? Round(component.Amount, moneyScale, roundingMode)
            : Round(component.Amount, moneyScale, roundingMode));
        var residual = taxTotal - preReconciliation;
        var allocationTarget = residual == 0m || exactComponentTaxes.Count == 0 ? "None" : exactComponentTaxes[^1].Component.TaxCode;
        for (var index = 0; index < exactComponentTaxes.Count; index++)
        {
            var item = exactComponentTaxes[index];
            var componentAmount = Round(item.Amount, moneyScale, roundingMode);
            if (index == exactComponentTaxes.Count - 1 && rounding != TaxRoundingStrategy.PerTaxComponent) componentAmount += residual;
            var sign = context.DocumentSemantics is TaxDocumentSemantics.CreditNote or TaxDocumentSemantics.PurchaseCredit ? -1m : 1m;
            componentAmount *= sign;
            var recoverability = item.Component.RecoverablePercentage is { } recoverable ? MakeRecoverability(componentAmount, recoverable, moneyScale, roundingMode) : null;
            lines.Add(new(item.Component.TaxTypeCode, item.Component.TaxCode, item.Component.Treatment.ToString(), item.Component.Rate is { } rate ? Format(rate) : null,
                item.Component.Method == TaxCalculationMethod.FixedPerUnit ? Format(item.Component.FixedAmount!.Value) : null, Format(item.Base * sign), Format(componentAmount), context.TransactionCurrency,
                context.JurisdictionCode ?? "Unresolved", item.Component.LegalReasonCode ?? rule.LegalReasonCode, rule.LegalReasonText, recoverability, item.Component.ReportingCode, rule.Id, rule.RuleVersion));
        }
        var signFactor = context.DocumentSemantics is TaxDocumentSemantics.CreditNote or TaxDocumentSemantics.PurchaseCredit ? -1m : 1m;
        taxTotal *= signFactor;
        var unsignedTaxTotal = taxTotal * signFactor;
        var gross = context.PriceMode == TaxPriceMode.TaxInclusive ? exactBase : exactBase + unsignedTaxTotal;
        var net = context.PriceMode == TaxPriceMode.TaxInclusive ? gross - unsignedTaxTotal : exactBase;
        net *= signFactor;
        gross *= signFactor;
        if (rule.Treatment is TaxTreatment.ZeroRated or TaxTreatment.Exempt or TaxTreatment.OutOfScope) { taxTotal = 0m; gross = net; }
        if (rule.Treatment == TaxTreatment.ReverseCharge && lines.Count == 0) throw new TaxException("tax.reverse_charge_rule_incomplete", "The reverse charge rule must provide its reviewed statutory components.");
        var explanation = new TaxExplanation(packCode, packVersion, taxDate, context.JurisdictionCode ?? "Unresolved", registrationStatus, partyTreatment, productCategory, rule.Id, lines.Select(line => line.TaxCode).ToArray(), rounding.ToString(), $"{rule.Treatment} applied by rule {rule.Id} from {packCode} {packVersion}.", LegalReasonCode: rule.LegalReasonCode, LegalReasonText: rule.LegalReasonText);
        var roundingEvidence = new TaxRoundingEvidence(rounding.ToString(), moneyScale, roundingMode.ToString(), Format(exactTaxTotal * signFactor), Format(preReconciliation * signFactor), Format(residual * signFactor), allocationTarget, Format(taxTotal));
        return new(TaxEvaluationState.Evaluated, rule.Treatment, Format(net), Format(taxTotal), Format(gross), lines, context.JurisdictionCode ?? "Unresolved", registrationStatus, rule.RuleVersion, CalculationVersion, explanation, taxFxSnapshot, roundingPolicyVersion, roundingEvidence, context.QuantitySnapshot);
    }

    public static TaxAccountingFact ToAccountingFact(TaxLine line, string taxDate, TaxCalculationContext? context = null, string? sourceDocumentKind = null, Guid? sourceDocumentId = null)
    {
        var recoverable = line.Recoverability is null ? 0m : decimal.Parse(line.Recoverability.RecoverableAmount, CultureInfo.InvariantCulture);
        var nonRecoverable = line.Recoverability is null ? decimal.Parse(line.TaxAmount, CultureInfo.InvariantCulture) : decimal.Parse(line.Recoverability.NonRecoverableAmount, CultureInfo.InvariantCulture);
        return new(line.TaxTypeCode, line.TaxCode, line.TreatmentCode, line.TaxableBase, line.TaxAmount, Format(recoverable), Format(nonRecoverable), line.Currency, taxDate, line.Jurisdiction, line.ReportingCode ?? "", context?.PartyId, context?.ProductTemplateId, context?.ProductVariantId, sourceDocumentKind, sourceDocumentId, context?.ProductTaxCategoryId, context?.Dimensions);
    }

    private static decimal RequiredRate(TaxComponent component) => component.Rate is >= 0m and <= 1000m ? component.Rate.Value : throw new TaxException("tax.component_rate_invalid", "A percentage tax component requires a supported rate.");
    private static decimal RequiredFixed(TaxComponent component) => component.FixedAmount is >= 0m ? component.FixedAmount.Value : throw new TaxException("tax.component_amount_invalid", "A fixed tax component requires a nonnegative amount.");
    private static TaxRecoverability MakeRecoverability(decimal amount, decimal percentage, int scale, FinancialRoundingMode roundingMode)
    {
        if (percentage is < 0m or > 100m) throw new TaxException("tax.recoverability_invalid", "Recoverable percentage must be between zero and one hundred.");
        var recoverable = Round(amount * percentage / 100m, scale, roundingMode);
        var kind = percentage == 0m ? TaxRecoverabilityKind.NonRecoverable : percentage == 100m ? TaxRecoverabilityKind.Recoverable : TaxRecoverabilityKind.PartiallyRecoverable;
        return new(kind, Format(percentage), Format(recoverable), Format(amount - recoverable));
    }
    private static decimal Round(decimal amount, int scale, FinancialRoundingMode mode) => FinancialRounding.Round(amount, scale, Enum.Parse<FinancialRoundingModeValue>(mode.ToString(), true));
    private static void EnsureAcyclic(TaxComponent[] components)
    {
        if (components.Any(component => component.Sequence < 1) || components.Select(component => component.Sequence).Distinct().Count() != components.Length)
            throw new TaxException("tax.component_sequence_invalid", "Tax Stack component sequence numbers must be positive and unique.");
        var bySequence = components.ToDictionary(component => component.Sequence);
        foreach (var component in components)
        {
            var seen = new HashSet<int>(); var current = component;
            while (current.BaseComponentSequence is { } dependency)
            {
                if (!seen.Add(dependency)) throw new TaxException("tax.component_cycle", "Tax stack component dependencies must not contain a cycle.");
                current = bySequence.TryGetValue(dependency, out var next) ? next : throw new TaxException("tax.component_base_missing", "A tax component references a missing base component.");
            }
        }
        if (components.Any(component => component.BaseComponentSequence is { } dependency && dependency >= component.Sequence))
            throw new TaxException("tax.component_dependency_order_invalid", "A Tax Stack dependency must reference an earlier component sequence.");
    }
    private static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
