namespace Modules.Hr.Entities;

public static class LeavePolicyRules
{
    public static decimal ValidateRequestedQuantity(string unit, bool partialDayAllowed, decimal? requested, decimal fullPeriodQuantity)
    {
        if (unit is not ("Days" or "Hours")) throw new HrException("hr.leave_unit_invalid");
        if (unit == "Hours" && requested is null) throw new HrException("hr.leave_quantity_required");
        var quantity = requested ?? fullPeriodQuantity;
        if (quantity <= 0 || unit == "Days" && quantity > fullPeriodQuantity || unit == "Hours" && quantity > 24 || !partialDayAllowed && quantity != decimal.Truncate(quantity))
            throw new HrException("hr.leave_quantity_invalid");
        return quantity;
    }

    public static void EnsureBalanceAllowsRequest(string policy, decimal negativeLimit, decimal available, decimal requested)
    {
        if (requested <= 0 || negativeLimit < 0) throw new HrException("hr.leave_quantity_invalid");
        if (policy == "Disallow" && available < requested || policy == "AllowWithinLimit" && available - requested < -negativeLimit)
            throw new HrException("hr.leave_balance_insufficient");
        if (policy is not ("Disallow" or "AllowWithinLimit" or "RequireApproval" or "Allow"))
            throw new HrException("hr.leave_negative_balance_policy_invalid");
    }
}
