namespace Modules.POS;

public static class PosTotals
{
    public static decimal LineTotal(decimal quantity, decimal unitPrice, decimal discount, decimal tax)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "pos.quantity_invalid");
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "pos.unit_price_invalid");
        if (discount < 0) throw new ArgumentOutOfRangeException(nameof(discount), "pos.discount_invalid");
        if (tax < 0) throw new ArgumentOutOfRangeException(nameof(tax), "pos.tax_invalid");
        return Math.Round(quantity * unitPrice - discount + tax, 4, MidpointRounding.AwayFromZero);
    }

    public static decimal Change(decimal tendered, decimal due)
    {
        if (tendered < 0) throw new ArgumentOutOfRangeException(nameof(tendered), "pos.tender_invalid");
        if (due < 0) throw new ArgumentOutOfRangeException(nameof(due), "pos.due_invalid");
        return Math.Max(0, Math.Round(tendered - due, 4, MidpointRounding.AwayFromZero));
    }
}
