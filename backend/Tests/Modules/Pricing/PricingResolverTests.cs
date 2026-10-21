using Modules.Pricing.Entities;
using Shared.Contracts.Pricing;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Pricing.Tests;

public sealed class PricingResolverTests
{
    private static readonly Guid Variant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Template = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Unit = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid List = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid Version = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly TenantId Tenant = new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
    private static readonly LegalEntityId Entity = new(Guid.Parse("70000000-0000-0000-0000-000000000001"));

    [Fact]
    public void ResolvesVariantTierAndPercentageDiscountDeterministically()
    {
        var context = Context(quantity: 12m);
        var rules = new[]
        {
            Candidate("10000000-0000-0000-0000-000000000010", PricingProductScope.Template, PricingResultKind.FixedUnitPrice, 100m),
            Candidate("10000000-0000-0000-0000-000000000011", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 80m, min: 10m),
            Candidate("10000000-0000-0000-0000-000000000012", PricingProductScope.Variant, PricingResultKind.PercentageDiscount, 10m, min: 10m)
        };

        var result = PricingResolver.Resolve(context, ListContext(), VersionContext(), rules);

        Assert.Equal("Resolved", result.Status);
        Assert.Equal("72", result.Snapshot!.FinalUnitPrice);
        Assert.Equal("80", result.Snapshot.BaseUnitPrice);
        Assert.Equal(2, result.EquivalentWinningRuleIds.Count);
    }

    [Fact]
    public void PreservesExplicitZeroAndDistinguishesItFromMissing()
    {
        var zero = PricingResolver.Resolve(Context(), ListContext(), VersionContext(), [Candidate("10000000-0000-0000-0000-000000000020", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 0m)]);
        var missing = PricingResolver.Resolve(Context(), ListContext(), VersionContext(), []);

        Assert.Equal("Resolved", zero.Status);
        Assert.Equal("0", zero.Snapshot!.FinalUnitPrice);
        Assert.Equal("PriceMissing", missing.Status);
        Assert.Null(missing.Snapshot);
    }

    [Fact]
    public void RejectsContradictoryEqualRankCandidatesInsteadOfChoosingByOrder()
    {
        var context = Context();
        var candidates = new[]
        {
            Candidate("10000000-0000-0000-0000-000000000030", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 10m),
            Candidate("10000000-0000-0000-0000-000000000031", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 11m)
        };

        var conflict = Assert.Throws<PricingConfigurationConflictException>(() => PricingResolver.Resolve(context, ListContext(), VersionContext(), candidates));
        Assert.Equal("pricing.configuration_conflict", conflict.Code);
        Assert.Equal(2, conflict.RuleIds.Count);
        Assert.Equal(2, PricingResolver.FindConflicts(candidates).Count);
    }

    [Fact]
    public void PartyAndProductSpecificityAreDeterministicAcrossCandidateOrder()
    {
        var party = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var group = Guid.Parse("90000000-0000-0000-0000-000000000002");
        var rootCategory = Guid.Parse("90000000-0000-0000-0000-000000000003");
        var leafCategory = Guid.Parse("90000000-0000-0000-0000-000000000004");
        var context = Context() with { PartyId = party, PriceGroupId = group, CategoryPath = [leafCategory, rootCategory] };
        var rules = new[]
        {
            NewCandidate("10000000-0000-0000-0000-000000000040", PricingProductScope.AllProducts, PricingPartyScope.AllParties, 100m),
            NewCandidate("10000000-0000-0000-0000-000000000041", PricingProductScope.Category, PricingPartyScope.AllParties, 90m, category: rootCategory),
            NewCandidate("10000000-0000-0000-0000-000000000042", PricingProductScope.Category, PricingPartyScope.AllParties, 80m, category: leafCategory),
            NewCandidate("10000000-0000-0000-0000-000000000043", PricingProductScope.Template, PricingPartyScope.AllParties, 70m),
            NewCandidate("10000000-0000-0000-0000-000000000044", PricingProductScope.Variant, PricingPartyScope.PriceGroup, 60m, priceGroup: group),
            NewCandidate("10000000-0000-0000-0000-000000000045", PricingProductScope.AllProducts, PricingPartyScope.SpecificParty, 50m, partyId: party)
        };

        foreach (var order in new[] { rules, rules.Reverse().ToArray() })
            Assert.Equal("50", PricingResolver.Resolve(context, ListContext(), VersionContext(), order).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void QuantityTiersUseExclusiveUpperBoundAndHighestQualifyingMinimum()
    {
        var firstTier = Candidate("10000000-0000-0000-0000-000000000050", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 100m) with { MaxQuantity = 10m };
        var secondTier = Candidate("10000000-0000-0000-0000-000000000051", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 80m, min: 10m);

        Assert.Equal("100", PricingResolver.Resolve(Context(9.99m), ListContext(), VersionContext(), [firstTier, secondTier]).Snapshot!.FinalUnitPrice);
        Assert.Equal("80", PricingResolver.Resolve(Context(10m), ListContext(), VersionContext(), [secondTier, firstTier]).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void ExplicitUomPriceOutranksDerivedAndGeneralUnitRules()
    {
        var exact = Candidate("10000000-0000-0000-0000-000000000060", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 240m) with { UomId = Unit };
        var derived = Candidate("10000000-0000-0000-0000-000000000061", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 200m) with { UomId = Unit, IsDerivedUom = true };
        var anyUnit = Candidate("10000000-0000-0000-0000-000000000062", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 180m) with { UomId = null };

        Assert.Equal("240", PricingResolver.Resolve(Context(), ListContext(), VersionContext(), [derived, anyUnit, exact]).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void SnapshotFromEarlierVersionDoesNotChangeWhenLaterVersionResolves()
    {
        var oldVersion = VersionContext();
        var original = PricingResolver.Resolve(Context(), ListContext(), oldVersion, [Candidate("10000000-0000-0000-0000-000000000070", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 25m)]).Snapshot!;
        _ = PricingResolver.Resolve(Context(), ListContext(), oldVersion with { Id = Guid.NewGuid(), VersionNo = 2 }, [Candidate("10000000-0000-0000-0000-000000000071", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 40m)]);

        Assert.Equal("25", original.FinalUnitPrice);
        Assert.Equal(Version, original.PriceListVersionId);
    }

    [Fact]
    public void DirectionAndListContextAreExplicitAndIndependent()
    {
        var buyingContext = Context() with { Direction = PricingDirection.Buying, PriceListId = Guid.NewGuid() };
        var buyingList = ListContext() with { Id = buyingContext.PriceListId!.Value, Direction = PricingDirection.Buying };
        var buyingVersion = VersionContext() with { Id = Guid.NewGuid() };
        var buying = PricingResolver.Resolve(buyingContext, buyingList, buyingVersion,
            [Candidate("10000000-0000-0000-0000-000000000080", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 42m)]);

        Assert.Equal("42", buying.Snapshot!.FinalUnitPrice);
        Assert.Equal("Buying", buying.Snapshot.Direction);
        Assert.Throws<PricingException>(() => PricingResolver.Resolve(Context(), buyingList, buyingVersion, []));
        Assert.Throws<PricingException>(() => PricingResolver.Resolve(Context() with { PriceListId = Guid.NewGuid() }, ListContext(), VersionContext(), []));
    }

    [Fact]
    public void PartySpecificityAndProductSpecificityUseDocumentedOrder()
    {
        var party = Guid.Parse("90000000-0000-0000-0000-000000000011");
        var group = Guid.Parse("90000000-0000-0000-0000-000000000012");
        var context = Context() with { PartyId = party, PriceGroupId = group };
        var candidates = new[]
        {
            NewCandidate("10000000-0000-0000-0000-000000000081", PricingProductScope.Variant, PricingPartyScope.AllParties, 100m),
            NewCandidate("10000000-0000-0000-0000-000000000082", PricingProductScope.Variant, PricingPartyScope.PriceGroup, 90m, priceGroup: group),
            NewCandidate("10000000-0000-0000-0000-000000000083", PricingProductScope.Variant, PricingPartyScope.SpecificParty, 80m, partyId: party),
            NewCandidate("10000000-0000-0000-0000-000000000084", PricingProductScope.Template, PricingPartyScope.AllParties, 70m)
        };
        Assert.Equal("80", PricingResolver.Resolve(context, ListContext(), VersionContext(), candidates).Snapshot!.FinalUnitPrice);
        Assert.Equal("100", PricingResolver.Resolve(Context(), ListContext(), VersionContext(), candidates.Where(item => item.PartyScope == PricingPartyScope.AllParties).ToArray()).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void VariantBeatsTemplateAndDeepCategoryBeatsParentAndBrand()
    {
        var leaf = Guid.Parse("90000000-0000-0000-0000-000000000021");
        var parent = Guid.Parse("90000000-0000-0000-0000-000000000022");
        var brand = Guid.Parse("90000000-0000-0000-0000-000000000023");
        var categoryContext = Context() with { CategoryPath = [leaf, parent], BrandId = brand };
        var categoryRules = new[]
        {
            NewCandidate("10000000-0000-0000-0000-000000000085", PricingProductScope.Brand, PricingPartyScope.AllParties, 30m) with { BrandId = brand },
            NewCandidate("10000000-0000-0000-0000-000000000086", PricingProductScope.Category, PricingPartyScope.AllParties, 20m, category: parent) with { CategoryDepth = 1 },
            NewCandidate("10000000-0000-0000-0000-000000000087", PricingProductScope.Category, PricingPartyScope.AllParties, 10m, category: leaf) with { CategoryDepth = 2 }
        };
        Assert.Equal("10", PricingResolver.Resolve(categoryContext, ListContext(), VersionContext(), categoryRules).Snapshot!.FinalUnitPrice);

        var productRules = new[]
        {
            Candidate("10000000-0000-0000-0000-000000000088", PricingProductScope.Template, PricingResultKind.FixedUnitPrice, 20m),
            Candidate("10000000-0000-0000-0000-000000000089", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 10m)
        };
        Assert.Equal("10", PricingResolver.Resolve(Context(), ListContext(), VersionContext(), productRules).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void ExactUomAndExplicitCartonPriceBeatDerivedCommercialPrice()
    {
        var derivedUnit = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var directCarton = Candidate("10000000-0000-0000-0000-000000000090", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 210m) with { UomId = Unit };
        var derivedCarton = Candidate("10000000-0000-0000-0000-000000000091", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 240m) with { UomId = Unit, IsDerivedUom = true };
        var exactPiece = Candidate("10000000-0000-0000-0000-000000000092", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 10m) with { UomId = derivedUnit };
        var derivedPiece = Candidate("10000000-0000-0000-0000-000000000093", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 11m) with { UomId = derivedUnit, IsDerivedUom = true };
        Assert.Equal("210", PricingResolver.Resolve(Context(), ListContext(), VersionContext(), [derivedCarton, directCarton]).Snapshot!.FinalUnitPrice);
        Assert.Equal("10", PricingResolver.Resolve(Context() with { UnitOfMeasureId = derivedUnit }, ListContext(), VersionContext(), [exactPiece, derivedPiece]).Snapshot!.FinalUnitPrice);
    }

    [Fact]
    public void QuantityAndDateBoundariesAreExplicitAndHalfOpen()
    {
        var low = Candidate("10000000-0000-0000-0000-000000000094", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 100m) with { MaxQuantity = 10m, ValidTo = new DateOnly(2026, 2, 1) };
        var high = Candidate("10000000-0000-0000-0000-000000000095", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 80m, min: 10m) with { ValidFrom = new DateOnly(2026, 2, 1) };
        Assert.Equal("100", PricingResolver.Resolve(Context(9.999m), ListContext(), VersionContext(), [low, high]).Snapshot!.FinalUnitPrice);
        Assert.Equal("80", PricingResolver.Resolve(Context(10m) with { PricingDate = new DateOnly(2026, 2, 1) }, ListContext(), VersionContext(), [low, high]).Snapshot!.FinalUnitPrice);
        Assert.Equal("80", PricingResolver.Resolve(Context(10.001m) with { PricingDate = new DateOnly(2026, 2, 2) }, ListContext(), VersionContext(), [low, high]).Snapshot!.FinalUnitPrice);
        Assert.Throws<PricingException>(() => PricingResolver.Resolve(Context() with { PricingDate = new DateOnly(2025, 12, 31) }, ListContext(), VersionContext(), [low]));
    }

    [Fact]
    public void PriorityBreaksOnlyOtherwiseEqualRankAndEquivalentRowsIgnoreIdsAndInputOrder()
    {
        var rules = new[]
        {
            Candidate("10000000-0000-0000-0000-000000000096", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 20m) with { Priority = 1 },
            Candidate("f0000000-0000-0000-0000-000000000001", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 10m) with { Priority = 2 },
            Candidate("00000000-0000-0000-0000-000000000001", PricingProductScope.Template, PricingResultKind.FixedUnitPrice, 1m) with { Priority = 99 }
        };
        var first = PricingResolver.Resolve(Context(), ListContext(), VersionContext(), rules);
        var second = PricingResolver.Resolve(Context(), ListContext(), VersionContext(), rules.Reverse().ToArray());
        Assert.Equal("10", first.Snapshot!.FinalUnitPrice);
        Assert.Equal(first.Snapshot.FinalUnitPrice, second.Snapshot!.FinalUnitPrice);
        Assert.Equal(first.EquivalentWinningRuleIds, second.EquivalentWinningRuleIds);
    }

    [Fact]
    public void FinBaseConversionEvidenceIsRetainedInHighPrecisionSnapshot()
    {
        const string fx = "{\"approvedRateId\":\"a1\",\"fromCurrency\":\"USD\",\"toCurrency\":\"EGP\",\"rate\":\"50.125\",\"purpose\":\"Sales\"}";
        var list = ListContext() with { CurrencyCode = "USD", CurrencyPolicy = PricingCurrencyPolicy.AllowFinBaseConversion };
        var context = Context() with { TransactionCurrencyCode = "EGP" };
        var resolved = PricingResolver.Resolve(context, list, VersionContext(), [Candidate("10000000-0000-0000-0000-000000000097", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 1.123456789m)], 50.125m, fx);
        Assert.Equal("56.313271548625", resolved.Snapshot!.FinalUnitPrice);
        Assert.Equal(fx, resolved.Snapshot.ExchangeRateSnapshotJson);
        Assert.Contains("Variant", resolved.Explanation);
        Assert.Equal(PricingResolver.Version, resolved.Snapshot.ResolverVersion);
    }

    [Fact]
    public void ArchivedListsCannotResolveAndHistoricalVersionMustCoverExplicitDate()
    {
        Assert.Throws<PricingException>(() => PricingResolver.Resolve(Context(), ListContext() with { Status = "Archived" }, VersionContext(), []));
        Assert.Throws<PricingException>(() => PricingResolver.Resolve(Context() with { PricingDate = new DateOnly(2027, 1, 1) }, ListContext(), VersionContext() with { EffectiveTo = new DateOnly(2027, 1, 1) }, []));
        var historical = PricingResolver.Resolve(Context() with { PricingDate = new DateOnly(2025, 12, 15) }, ListContext(), VersionContext() with { EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveTo = new DateOnly(2026, 1, 1) }, [Candidate("10000000-0000-0000-0000-000000000098", PricingProductScope.Variant, PricingResultKind.FixedUnitPrice, 15m)]);
        Assert.Equal("15", historical.Snapshot!.FinalUnitPrice);
    }

    private static PricingContext Context(decimal quantity = 1m) => new(Tenant, Entity, PricingDirection.Selling, new DateOnly(2026, 1, 5), "EGP", List, null, null, Variant, Template, [], null, Unit, quantity);
    private static PricingListContext ListContext() => new(List, Entity.Value, PricingDirection.Selling, "EGP", PricingCurrencyPolicy.ExactCurrency, "Active");
    private static PricingVersionContext VersionContext() => new(Version, 1, new DateOnly(2026, 1, 1), null, "Active");
    private static PricingRuleCandidate Candidate(string id, PricingProductScope scope, PricingResultKind kind, decimal value, decimal min = 0m) => new(Guid.Parse(id), scope, scope == PricingProductScope.Variant ? Variant : null, scope == PricingProductScope.Template ? Template : null, null, 0, null, PricingPartyScope.AllParties, null, null, null, min, null, null, null, 0, kind, value);
    private static PricingRuleCandidate NewCandidate(string id, PricingProductScope productScope, PricingPartyScope partyScope, decimal value, Guid? category = null, Guid? partyId = null, Guid? priceGroup = null) => new(Guid.Parse(id), productScope, productScope == PricingProductScope.Variant ? Variant : null, productScope == PricingProductScope.Template ? Template : null, category, category is null ? 0 : 1, null, partyScope, partyId, priceGroup, null, 0m, null, null, null, 0, PricingResultKind.FixedUnitPrice, value);
}
