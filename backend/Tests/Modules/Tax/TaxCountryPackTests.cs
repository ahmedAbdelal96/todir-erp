using Modules.Tax.Entities;
using NodaTime;
using Xunit;

namespace Tax.Tests;

public sealed class TaxCountryPackTests
{
    [Fact]
    public void ReviewedTestPackRunsItsGoldenVectorThroughTheGenericEngine()
    {
        var input = new TaxCalculationContext(Guid.NewGuid(),Guid.NewGuid(),TaxDirection.Sales,"Invoice","Domestic",new LocalDate(2026,1,1),new LocalDate(2026,1,1),null,null,null,"ZZ","ZZ",null,"ZZ-TEST",Guid.NewGuid(),null,null,1,null,TaxPriceMode.TaxExclusive,100m,0m,0m,"ZZZ","ZZZ",null);
        var component = new TaxComponent(Guid.NewGuid(),"VAT","TEST-10",TaxTreatment.StandardRated,TaxCalculationMethod.PercentageOfNet,10m,null,"ZZZ",1,null,"TEST-RATE","TEST ONLY",null);
        var rule = new TaxRuleCandidate(Guid.NewGuid(),TaxTreatment.StandardRated,1,new DateOnly(2026,1,1),null,null,null,null,null,null,"Invoice","Domestic","ZZ",TaxRuleMutability.CountryPack,"test-1","TEST-RATE","Non-statutory fixture",[component]);
        var vector = new TaxCountryPackGoldenVector("exclusive-reference","fixture:exclusive",new LocalDate(2026,1,1),input,rule,TaxTreatment.StandardRated,"100","10","TEST-RATE","test-v1","TestOnly");
        var manifest = new TaxCountryPackManifest("TEST-ONLY","ZZ","test-v1",TaxCalculator.CalculationVersion,TaxCountryPackStatus.Installed,new LocalDate(2026,1,1),null,"TestOnly",new(true,false,false,false,false),["test-rules"],"Internal test authority","Reference scenario","fixture:exclusive",null,null,null,true);
        var package = new TaxCountryPackPackage(manifest,[rule],[vector]);

        var result = package.Validate(TaxCalculator.CalculationVersion,new HashSet<string>(["test-rules"]));

        Assert.True(result.Valid);
        Assert.Empty(result.Issues);
        Assert.Equal("10",TaxCalculator.Calculate(input,"2026-01-01","NotRegistered",rule,manifest.PackId,manifest.PackVersion,null,null).TaxTotal);
    }

    [Fact]
    public void CountryPackValidationRejectsIncompatibleUnreviewedOrEvidenceFreeStatutoryContent()
    {
        var manifest = new TaxCountryPackManifest("STATUTORY-EXAMPLE","ZZ","1.0","future-engine",TaxCountryPackStatus.Installed,new LocalDate(2026,1,1),null,"Unreviewed",new(true,false,false,false,false),["missing-rules"],"","","",null,null,null);
        var package = new TaxCountryPackPackage(manifest,[],[]);

        var result = package.Validate(TaxCalculator.CalculationVersion,new HashSet<string>());

        Assert.False(result.Valid);
        Assert.Contains(result.Issues,issue=>issue.Code=="tax.country_pack_incompatible");
        Assert.Contains(result.Issues,issue=>issue.Code=="tax.country_pack_evidence_missing");
        Assert.Contains(result.Issues,issue=>issue.Code=="tax.country_pack_review_required");
        Assert.Contains(result.Issues,issue=>issue.Code=="tax.country_pack_golden_vectors_missing");
    }

    [Fact]
    public void CountryPackCannotBeScheduledOrActivatedWithoutValidationAndCannotRewriteHistory()
    {
        var manifest = new TaxCountryPackManifest("TEST-ONLY","ZZ","1.0",TaxCalculator.CalculationVersion,TaxCountryPackStatus.Validated,new LocalDate(2026,1,1),null,"TestOnly",new(true,false,false,false,false),["test"],"Internal","Fixture","fixture",null,null,null,true);

        Assert.Throws<TaxException>(()=>TaxCountryPackLifecycle.Transition(manifest,TaxCountryPackStatus.Active));
        Assert.Equal(TaxCountryPackStatus.Active,TaxCountryPackLifecycle.Transition(manifest,TaxCountryPackStatus.Active,new(true,[])));
        Assert.Throws<TaxException>(()=>TaxCountryPackLifecycle.Transition(manifest with { Status=TaxCountryPackStatus.Active },TaxCountryPackStatus.Validated,new(true,[])));
    }

    [Fact]
    public void EffectiveCutoverUsesNewPackOnBoundaryAndRetainsOldVersion()
    {
        var oldPack = Manifest("v1",TaxCountryPackStatus.Active,new LocalDate(2026,1,1),null);
        var nextPack = Manifest("v2",TaxCountryPackStatus.Validated,new LocalDate(2026,7,1),null);
        var history = new[] { oldPack,nextPack };

        var activated = TaxCountryPackTimeline.ActivateForFuture(history,"TEST-ONLY","v2",new LocalDate(2026,7,1),new(true,[]));

        Assert.Equal("v1",TaxCountryPackTimeline.Resolve(activated,"ZZ",new LocalDate(2026,6,30))!.PackVersion);
        Assert.Equal("v2",TaxCountryPackTimeline.Resolve(activated,"ZZ",new LocalDate(2026,7,1))!.PackVersion);
        Assert.Null(oldPack.EffectiveTo);
        Assert.Equal(TaxCountryPackStatus.Superseded,activated[0].Status);
    }

    private static TaxCountryPackManifest Manifest(string version,TaxCountryPackStatus status,LocalDate from,LocalDate? to) =>
        new("TEST-ONLY","ZZ",version,TaxCalculator.CalculationVersion,status,from,to,"Approved",new(true,false,false,false,false),["test-rules"],"Internal test authority","Reference scenario","fixture",null,"Reviewer",new LocalDate(2026,1,1),true);
}
