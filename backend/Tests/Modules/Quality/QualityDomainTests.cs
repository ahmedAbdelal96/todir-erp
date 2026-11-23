using Modules.Quality.Entities;
using Xunit;

namespace Modules.Quality.Tests;

public sealed class QualityDomainTests
{
    private static readonly HashSet<string> Allowed = new(new[] { "Pass", "Fail" }, StringComparer.OrdinalIgnoreCase);
    [Fact]
    public void FullSamplingUsesEntireLot()
        => Assert.Equal(100, QualityRules.CalculateSampleSize("Full", 100m, null, null));

    [Fact]
    public void FixedSamplingNeverExceedsLot()
        => Assert.Equal(7, QualityRules.CalculateSampleSize("Fixed", 7m, 10m, null));

    [Fact]
    public void PercentageSamplingRoundsUpAndHasMinimumOne()
    {
        Assert.Equal(3, QualityRules.CalculateSampleSize("Percentage", 25m, null, 10m));
        Assert.Equal(1, QualityRules.CalculateSampleSize("Percentage", 2m, null, 0.1m));
    }

    [Fact]
    public void NumericEvaluationIsInclusiveAndRejectsMissingRequiredValue()
    {
        Assert.True(QualityRules.EvaluateNumeric(10m, 9.5m, 10.5m, true));
        Assert.False(QualityRules.EvaluateNumeric(11m, 9.5m, 10.5m, true));
        Assert.False(QualityRules.EvaluateNumeric(null, 9.5m, 10.5m, true));
    }

    [Fact]
    public void CategoricalEvaluationUsesApprovedValues()
    {
        Assert.True(QualityRules.EvaluateCategorical(" pass ", Allowed, true));
        Assert.False(QualityRules.EvaluateCategorical("Unknown", Allowed, true));
    }

    [Fact]
    public void InvalidSamplingAndEmptyFingerprintsFailClosed()
    {
        Assert.Throws<QualityException>(() => QualityRules.CalculateSampleSize("Percentage", 10m, null, 0m));
        Assert.Throws<QualityException>(() => QualityRules.NormalizeFingerprint(" "));
    }
}
