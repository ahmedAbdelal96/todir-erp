using Shared.Contracts.FinanceBase;

namespace Modules.Invoicing.Entities;

public static class InvoiceCommercialAllocation
{
    public static decimal[] Allocate(decimal amount, decimal[] bases, int scale, FinancialRoundingMode mode)
    {
        if (amount < 0m || scale is < 0 or > 8 || bases.Any(value => value < 0m))
            throw new InvoiceException("invoicing.allocation_input_invalid", "An allocation requires non-negative amounts and a supported money precision.");
        var result = new decimal[bases.Length];
        if (amount == 0m) return result;
        var total = bases.Sum();
        if (total <= 0m) throw new InvoiceException("invoicing.allocation_basis_missing", "A document adjustment requires a positive eligible line basis.");
        var remaining = amount;
        var last = -1;
        for (var index = bases.Length - 1; index >= 0; index--) if (bases[index] > 0m) { last = index; break; }
        for (var index = 0; index < bases.Length; index++)
        {
            if (bases[index] <= 0m) continue;
            // Independent rounding can over-allocate the source amount (for example,
            // 0.005 split evenly at two decimals with HalfUp). Keep the residual
            // non-negative so the final eligible line receives a valid remainder.
            var allocated = index == last ? remaining : Math.Min(remaining, Math.Max(0m, Round(amount * bases[index] / total, scale, mode)));
            result[index] = allocated;
            remaining -= allocated;
        }
        if (remaining != 0m) throw new InvoiceException("invoicing.allocation_rounding_invalid", "The configured rounding policy could not allocate the adjustment exactly.");
        return result;
    }

    private static decimal Round(decimal amount, int scale, FinancialRoundingMode mode)
    {
        var factor = (decimal)Math.Pow(10, scale);
        var scaled = amount * factor;
        var rounded = mode switch
        {
            FinancialRoundingMode.HalfUp => decimal.Round(scaled, 0, MidpointRounding.AwayFromZero),
            FinancialRoundingMode.HalfEven => decimal.Round(scaled, 0, MidpointRounding.ToEven),
            FinancialRoundingMode.Down => decimal.Truncate(scaled),
            FinancialRoundingMode.Up => decimal.Ceiling(scaled),
            _ => throw new InvoiceException("invoicing.rounding_policy_invalid", "The configured financial rounding mode is not supported.")
        };
        return rounded / factor;
    }
}
