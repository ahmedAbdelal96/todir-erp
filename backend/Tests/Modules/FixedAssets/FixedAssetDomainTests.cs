using Modules.FixedAssets;
using Xunit;

namespace Modules.FixedAssets.Tests;

public sealed class FixedAssetDomainTests
{
    [Fact]
    public void StraightLineNeverDepreciatesLandWhenNoDepreciationPolicyIsUsed()
    {
        Assert.Equal(0m, DepreciationEngine.CalculateStraightLine(100000m, 100000m, 1, 12));
    }

    [Fact]
    public void StraightLineHonoursResidualFloorAndFinalPeriod()
    {
        Assert.Equal(250m, DepreciationEngine.PeriodAmount(12000m, 0m, 48, 1));
        Assert.Equal(12000m, DepreciationEngine.CalculateStraightLine(12000m, 0m, 48, 48));
        Assert.Equal(0m, DepreciationEngine.PeriodAmount(12000m, 0m, 48, 49));
    }

    [Fact]
    public void StraightLineRejectsInvalidBasisAndLife()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DepreciationEngine.CalculateStraightLine(-1m, 0m, 12, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DepreciationEngine.CalculateStraightLine(100m, 0m, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DepreciationEngine.CalculateStraightLine(100m, 101m, 12, 1));
    }
}
