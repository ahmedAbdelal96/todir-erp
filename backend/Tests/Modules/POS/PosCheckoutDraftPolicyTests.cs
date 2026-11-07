using Xunit;

namespace POSTests;

public sealed class PosCheckoutDraftPolicyTests
{
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
