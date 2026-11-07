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

    [Fact]
    public void ExactTenderHasNoChange()
    {
        Assert.Equal(0m, Modules.POS.PosTotals.Change(12m, 12m));
    }

    [Fact]
    public void NegativeLineInputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Modules.POS.PosTotals.LineTotal(0m, 10m, 0m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Modules.POS.PosTotals.LineTotal(1m, 10m, -1m, 0m));
    }

    [Fact]
    public void TaxAndDiscountAreIncludedBeforeMoneyRounding()
    {
        Assert.Equal(31.3426m, Modules.POS.PosTotals.LineTotal(2m, 15.12345m, 1.25m, 2.34567m));
    }
}
