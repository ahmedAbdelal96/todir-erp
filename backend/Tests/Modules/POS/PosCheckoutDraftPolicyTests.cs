using Xunit;

namespace POSTests;

public sealed class PosCheckoutDraftPolicyTests
{
    [Theory]
    [InlineData("Succeeded", true)]
    [InlineData("RequiresAction", false)]
    [InlineData("Failed", false)]
    [InlineData(null, false)]
    public void CashIsCapturedOnlyAfterProviderSuccess(string? providerState, bool expected)
    {
        Assert.Equal(expected, Modules.POS.PosPaymentBoundaryPolicy.IsProviderSuccess(providerState));
    }

    [Fact]
    public void ExternalPaymentStartsWithDurablePendingState()
    {
        var state = Modules.POS.PosCheckoutDraftPolicy.InitialState(creditSale: false);

        Assert.Equal("PaymentPending", state.CheckoutState);
        Assert.Equal("Pending", state.PaymentState);
    }

    [Fact]
    public void CreditSaleSkipsExternalPaymentState()
    {
        var state = Modules.POS.PosCheckoutDraftPolicy.InitialState(creditSale: true);

        Assert.Equal("CompletionPending", state.CheckoutState);
        Assert.Equal("NotRequired", state.PaymentState);
    }
}
