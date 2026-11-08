using Xunit;
using Modules.POS;

namespace POSTests;

public sealed class PosReturnLifecycleTests
{
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Completed", true)]
    [InlineData("pending", true)]
    [InlineData("completed", true)]
    [InlineData("Cancelled", false)]
    [InlineData("cancelled", false)]
    [InlineData("Rejected", false)]
    [InlineData("rejected", false)]
    [InlineData("Draft", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void QualifyingReturnStatesIncludeOnlyPendingAndCompleted(string? state, bool expected)
    {
        Assert.Equal(expected, PosReturnPolicy.IsQualifyingReturnState(state));
    }

    [Fact]
    public void RepeatedPartialReturnsAreCorrectlyAccumulated()
    {
        var returns = new (string State, decimal Quantity)[]
        {
            ("Completed", 1.5m),
            ("Pending", 2.0m),
            ("Completed", 0.5m)
        };

        var totalReturned = PosReturnPolicy.AggregateReturnedQuantity(returns);

        Assert.Equal(4.0m, totalReturned);
    }

    [Fact]
    public void CancelledAndRejectedReturnsAreExcludedFromReturnedQuantity()
    {
        var returns = new (string State, decimal Quantity)[]
        {
            ("Completed", 2.0m),
            ("Cancelled", 5.0m),
            ("Rejected", 3.0m),
            ("Pending", 1.0m)
        };

        var totalReturned = PosReturnPolicy.AggregateReturnedQuantity(returns);

        Assert.Equal(3.0m, totalReturned);
    }

    [Fact]
    public void EmptyReturnsYieldZeroReturnedQuantity()
    {
        Assert.Equal(0m, PosReturnPolicy.AggregateReturnedQuantity(Array.Empty<(string, decimal)>()));
    }
}
