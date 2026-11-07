using Xunit;

namespace POSTests;

public sealed class PosTotalsTests
{
    [Fact]
    public void LineTotalUsesDecimalRoundingAwayFromZero()
    {
        var result = Modules.POS.PosTotals.LineTotal(1.25m, 10.12345m, 0.10m, 1.23456m);
        Assert.Equal(13.7889m, result);
    }

    [Fact]
    public void ChangeNeverBecomesNegativeForPartialTender()
    {
        Assert.Equal(0m, Modules.POS.PosTotals.Change(10m, 12m));
        Assert.Equal(3m, Modules.POS.PosTotals.Change(15m, 12m));
    }
}
