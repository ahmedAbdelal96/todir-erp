namespace Modules.POS;

public static class PosCheckoutDraftPolicy
{
    public static (string CheckoutState, string PaymentState) InitialState(bool creditSale)
        => creditSale ? ("CompletionPending", "NotRequired") : ("PaymentPending", "Pending");
}
