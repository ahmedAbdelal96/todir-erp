namespace Modules.Service.Domain;

public static class ServiceRules
{
    public static void ValidateContext(string context, Guid? customerPartyId, Guid? installedBaseId)
    {
        if (context is not ("InternalMaintenance" or "CustomerFieldService")) throw new ArgumentException("Unsupported service context.", nameof(context));
        if (context == "CustomerFieldService" && customerPartyId is null) throw new ArgumentException("Customer field service requires a customer.", nameof(customerPartyId));
        if (installedBaseId == Guid.Empty) throw new ArgumentException("An installed base or fixed asset subject is required.", nameof(installedBaseId));
        if (context == "CustomerFieldService" && installedBaseId is null) throw new ArgumentException("Customer field service requires an installed base.", nameof(installedBaseId));
    }

    public static (decimal Reserved, decimal Issued, decimal Consumed, decimal Returned) ApplyPartMovement(string action, decimal quantity, decimal required, decimal reserved, decimal issued, decimal consumed, decimal returned)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        return action switch
        {
            "reserve" when quantity <= required - reserved => (reserved + quantity, issued, consumed, returned),
            "issue" when quantity <= required - issued => (reserved, issued + quantity, consumed, returned),
            "consume" or "return" when quantity <= issued - consumed - returned => action == "consume" ? (reserved, issued, consumed + quantity, returned) : (reserved, issued, consumed, returned + quantity),
            "reverse" when quantity <= issued - consumed - returned => (reserved, issued - quantity, consumed, returned),
            _ => throw new InvalidOperationException("The part movement exceeds the current work-order quantity."),
        };
    }
}
