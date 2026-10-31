using Shared.Contracts.Pricing;

namespace Modules.Pricing.Entities;

public class PricingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record PricingRuleCandidate(
    Guid Id,
    PricingProductScope ProductScope,
    Guid? VariantId,
    Guid? TemplateId,
    Guid? CategoryId,
    int CategoryDepth,
    Guid? BrandId,
    PricingPartyScope PartyScope,
    Guid? PartyId,
    Guid? PriceGroupId,
    Guid? UomId,
    decimal MinQuantity,
    decimal? MaxQuantity,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    int Priority,
    PricingResultKind ResultKind,
    decimal ResultValue,
    bool IsEntry = false,
    bool IsDerivedUom = false);

public sealed record PricingListContext(Guid Id, Guid LegalEntityId, PricingDirection Direction, string CurrencyCode, PricingCurrencyPolicy CurrencyPolicy, string Status);
public sealed record PricingVersionContext(Guid Id, int VersionNo, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Status);
public sealed record PricingRank(int PartySpecificity, int ProductSpecificity, int UomSpecificity, decimal QuantityFloor, int Priority) : IComparable<PricingRank>
{
    public static bool operator <(PricingRank left, PricingRank right) => left.CompareTo(right) < 0;
    public static bool operator <=(PricingRank left, PricingRank right) => left.CompareTo(right) <= 0;
    public static bool operator >(PricingRank left, PricingRank right) => left.CompareTo(right) > 0;
    public static bool operator >=(PricingRank left, PricingRank right) => left.CompareTo(right) >= 0;

    public int CompareTo(PricingRank? other)
    {
        if (other is null) return 1;
        var order = PartySpecificity.CompareTo(other.PartySpecificity);
        if (order == 0) order = ProductSpecificity.CompareTo(other.ProductSpecificity);
        if (order == 0) order = UomSpecificity.CompareTo(other.UomSpecificity);
        if (order == 0) order = QuantityFloor.CompareTo(other.QuantityFloor);
        if (order == 0) order = Priority.CompareTo(other.Priority);
        return order;
    }
}

public static class PricingResolver
{
    public const string Version = "pricing_resolver_v1";

    public static PriceResolution Resolve(PricingContext context, PricingListContext list, PricingVersionContext version, IReadOnlyList<PricingRuleCandidate> candidates, decimal conversionFactor = 1m, string? exchangeRateSnapshotJson = null, DateTimeOffset? capturedAt = null)
    {
        ValidateContext(context, list, version);
        var ranked = candidates
            .Where(rule => IsEligible(rule, context))
            .Select(rule => (Rule: rule, Rank: Rank(rule, context)))
            .OrderByDescending(item => item.Rank)
            .ToArray();

        if (ranked.Length == 0) return new("PriceMissing", null, "pricing.price_missing", "No active rule matches the product, party, unit, quantity, and pricing date.", []);
        var baseCandidates = ranked.Where(item => item.Rule.ResultKind == PricingResultKind.FixedUnitPrice).ToArray();
        if (baseCandidates.Length == 0) return new("PriceMissing", null, "pricing.price_missing", "Matching discounts require a matching base unit price.", []);
        var baseTies = baseCandidates.TakeWhile(item => item.Rank.CompareTo(baseCandidates[0].Rank) == 0).ToArray();
        if (baseTies.Select(item => item.Rule.ResultValue).Distinct().Count() > 1) throw new PricingConfigurationConflictException("Multiple equally specific base prices conflict. Resolve the price-list configuration before using it.", baseTies.Select(item => item.Rule.Id).Distinct().ToArray());
        var discountCandidates = ranked.Where(item => item.Rule.ResultKind != PricingResultKind.FixedUnitPrice).ToArray();
        var discountTies = discountCandidates.Length == 0 ? [] : discountCandidates.TakeWhile(item => item.Rank.CompareTo(discountCandidates[0].Rank) == 0).ToArray();
        if (discountTies.Select(item => (item.Rule.ResultKind, item.Rule.ResultValue)).Distinct().Count() > 1) throw new PricingConfigurationConflictException("Multiple equally specific discount rules conflict. Resolve the price-list configuration before using it.", discountTies.Select(item => item.Rule.Id).Distinct().ToArray());

        var chosen = baseTies[0].Rule;
        var basePrice = chosen.ResultValue * conversionFactor;
        decimal? discount = null;
        if (discountTies.Length > 0)
        {
            discount = discountTies[0].Rule.ResultKind == PricingResultKind.PercentageDiscount
                ? basePrice * discountTies[0].Rule.ResultValue / 100m
                : discountTies[0].Rule.ResultValue * conversionFactor;
        }
        var finalPrice = basePrice - (discount ?? 0m);
        if (finalPrice < 0m) throw new PricingException("pricing.discount_exceeds_base", "The matching discount exceeds the base unit price.");
        var now = capturedAt ?? DateTimeOffset.UtcNow;
        var snapshot = new PriceSnapshot(list.Id, version.Id, baseTies.Length == 1 ? chosen.Id : null, context.Direction.ToString(), context.ProductVariantId, context.UnitOfMeasureId, context.Quantity, context.PricingDate, list.CurrencyCode, context.TransactionCurrencyCode, Format(finalPrice), discount is null ? null : Format(basePrice), discount is null ? null : Format(discount.Value), Version, exchangeRateSnapshotJson, now);
        var reason = $"{chosen.ProductScope} / {chosen.PartyScope}; {(chosen.IsDerivedUom ? "derived from the permitted base unit" : $"unit specificity {Rank(chosen, context).UomSpecificity}")}; minimum quantity {Format(chosen.MinQuantity)}; explicit priority {chosen.Priority}.";
        return new("Resolved", snapshot, null, reason, baseTies.Select(item => item.Rule.Id).Concat(discountTies.Select(item => item.Rule.Id)).Order().ToArray());
    }

    public static IReadOnlyList<PricingRuleCandidate> FindConflicts(IReadOnlyList<PricingRuleCandidate> rules)
    {
        var conflicts = new List<PricingRuleCandidate>();
        for (var left = 0; left < rules.Count; left++)
            for (var right = left + 1; right < rules.Count; right++)
            {
                if (SameScopeAndRank(rules[left], rules[right]) && rules[left].ResultKind == rules[right].ResultKind && DateRangesOverlap(rules[left], rules[right]) && QuantityRangesOverlap(rules[left], rules[right]) && rules[left].ResultValue != rules[right].ResultValue)
                {
                    conflicts.Add(rules[left]);
                    conflicts.Add(rules[right]);
                }
            }
        return conflicts.DistinctBy(rule => rule.Id).ToArray();
    }

    private static void ValidateContext(PricingContext context, PricingListContext list, PricingVersionContext version)
    {
        if (context.TenantId.Value == Guid.Empty || context.LegalEntityId.Value == Guid.Empty || context.ProductVariantId == Guid.Empty || context.ProductTemplateId == Guid.Empty || context.UnitOfMeasureId == Guid.Empty || context.Quantity <= 0m)
            throw new PricingException("pricing.context_invalid", "Tenant, legal entity, product, unit, and positive quantity are required.");
        if (context.Direction != list.Direction || context.LegalEntityId.Value != list.LegalEntityId)
            throw new PricingException("pricing.list_scope_invalid", "The selected price list does not match this legal entity and pricing direction.");
        if (context.PriceListId is { } requestedListId && requestedListId != list.Id)
            throw new PricingException("pricing.list_scope_invalid", "The selected price list does not match the pricing request.");
        if (list.Status != "Active") throw new PricingException("pricing.list_archived", "Archived price lists cannot be used for new price resolutions.");
        if (context.PricingDate < version.EffectiveFrom || version.EffectiveTo is { } end && context.PricingDate >= end)
            throw new PricingException("pricing.version_not_effective", "No price-list version is effective for the requested pricing date.");
        if (context.TransactionCurrencyCode.Length != 3) throw new PricingException("pricing.currency_invalid", "Transaction currency must be a normalized ISO currency code.");
    }

    private static bool IsEligible(PricingRuleCandidate rule, PricingContext context)
    {
        if (rule.ValidFrom is { } from && context.PricingDate < from || rule.ValidTo is { } to && context.PricingDate >= to) return false;
        if (context.Quantity < rule.MinQuantity || rule.MaxQuantity is { } maximum && context.Quantity >= maximum) return false;
        if (rule.UomId is { } unit && unit != context.UnitOfMeasureId) return false;
        if (rule.PartyScope == PricingPartyScope.SpecificParty && rule.PartyId != context.PartyId) return false;
        if (rule.PartyScope == PricingPartyScope.PriceGroup && rule.PriceGroupId != context.PriceGroupId) return false;
        return rule.ProductScope switch
        {
            PricingProductScope.Variant => rule.VariantId == context.ProductVariantId,
            PricingProductScope.Template => rule.TemplateId == context.ProductTemplateId,
            PricingProductScope.Category => rule.CategoryId is { } categoryId && context.CategoryPath.Contains(categoryId),
            PricingProductScope.Brand => rule.BrandId == context.BrandId,
            PricingProductScope.AllProducts => true,
            _ => false
        };
    }

    private static PricingRank Rank(PricingRuleCandidate rule, PricingContext context)
    {
        var party = rule.PartyScope switch { PricingPartyScope.SpecificParty => 3, PricingPartyScope.PriceGroup => 2, _ => 1 };
        var categoryIndex = rule.CategoryId is { } category ? Array.IndexOf(context.CategoryPath.ToArray(), category) : -1;
        var product = rule.ProductScope switch { PricingProductScope.Variant => 6, PricingProductScope.Template => 5, PricingProductScope.Category => 4 + context.CategoryPath.Count - categoryIndex, PricingProductScope.Brand => 2, _ => 1 };
        var uom = rule.UomId is null ? 1 : rule.IsDerivedUom ? 2 : rule.UomId == context.UnitOfMeasureId ? 3 : 0;
        return new(party, product, uom, rule.MinQuantity, rule.Priority);
    }

    private static decimal CalculateRaw(PricingRuleCandidate rule) => rule.ResultValue;

    private static bool SameScopeAndRank(PricingRuleCandidate first, PricingRuleCandidate second) =>
        first.ProductScope == second.ProductScope && first.VariantId == second.VariantId && first.TemplateId == second.TemplateId && first.CategoryId == second.CategoryId && first.BrandId == second.BrandId && first.PartyScope == second.PartyScope && first.PartyId == second.PartyId && first.PriceGroupId == second.PriceGroupId && first.UomId == second.UomId && first.MinQuantity == second.MinQuantity && first.Priority == second.Priority;
    private static bool DateRangesOverlap(PricingRuleCandidate first, PricingRuleCandidate second) => (first.ValidTo is null || second.ValidFrom is null || second.ValidFrom < first.ValidTo) && (second.ValidTo is null || first.ValidFrom is null || first.ValidFrom < second.ValidTo);
    private static bool QuantityRangesOverlap(PricingRuleCandidate first, PricingRuleCandidate second) => (first.MaxQuantity is null || second.MinQuantity < first.MaxQuantity) && (second.MaxQuantity is null || first.MinQuantity < second.MaxQuantity);
    private static string Format(decimal value) => value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
}
