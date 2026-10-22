using Modules.Tax.Services;
using Modules.Tax.Repositories;
using Xunit;

namespace Tax.Tests;

public sealed class TaxCoverageBehaviorTests
{
    private static readonly Guid Entity = Guid.CreateVersion7();
    private static readonly Guid Product = Guid.CreateVersion7();

    [Fact]
    public void HealthyPersistedFactsEvaluateGreen()
    {
        var result = TaxCoverageService.Evaluate(HealthyFacts());
        Assert.Equal("Green", result.Health);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void IncompatibleCountryPackBlocksCoverage()
    {
        var result = TaxCoverageService.Evaluate(HealthyFacts() with { CountryPackStatus = "Incompatible" });
        Assert.Equal("Blocked", result.Health);
        Assert.Contains(result.Issues, issue => issue.Code == "CountryPackIncompatible");
    }

    [Fact]
    public void MissingRegistrationBlocksCoverage()
    {
        var result = TaxCoverageService.Evaluate(HealthyFacts() with { RegistrationStatus = null });
        Assert.Equal("Blocked", result.Health);
        Assert.Contains(result.Issues, issue => issue.Code == "RegistrationMissing");
    }

    [Fact]
    public void MissingProductClassificationBlocksCoverage()
    {
        var result = TaxCoverageService.Evaluate(HealthyFacts() with { ProductCoverage = "Missing" });
        Assert.Equal("Blocked", result.Health);
        Assert.Contains(result.Issues, issue => issue.Code == "ProductClassificationMissing");
    }

    [Fact]
    public void MissingRuleSetIsDistinctFromMissingApplicableRule()
    {
        var missingSet = TaxCoverageService.Evaluate(HealthyFacts() with { HasRuleSet = false, HasApplicableRule = false });
        var missingRule = TaxCoverageService.Evaluate(HealthyFacts() with { HasApplicableRule = false });
        Assert.Contains(missingSet.Issues, issue => issue.Code == "RuleSetMissing");
        Assert.DoesNotContain(missingSet.Issues, issue => issue.Code == "MissingRule");
        Assert.Contains(missingRule.Issues, issue => issue.Code == "MissingRule");
    }

    [Fact]
    public void EqualRankConflictBlocksOtherwiseHealthyCoverage()
    {
        var conflict = TaxRuleConflictValidator.HasConflicts([new("StandardRated", "Exempt", Guid.CreateVersion7(), Guid.CreateVersion7())]);
        var result = TaxCoverageService.Evaluate(HealthyFacts() with { HasRuleConflict = conflict });
        Assert.Equal("Blocked", result.Health);
        Assert.Contains(result.Issues, issue => issue.Code == "RuleConflict");
    }

    [Fact]
    public void IneffectiveRegistrationIsActionable()
    {
        var result = TaxCoverageService.Evaluate(HealthyFacts() with { RegistrationOutOfPeriod = true });
        Assert.Contains(result.Issues, issue => issue.Code == "RegistrationNotEffective");
    }

    private static TaxCoverageFacts HealthyFacts() => new(Entity, new DateOnly(2026, 1, 1), "EG", "Enabled", "Available", "Registered", false,
        "Configured", "Configured", "Covered", "NotRequested", true, false, true, Product, null);
}
