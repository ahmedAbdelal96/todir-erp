namespace Modules.POS;

public static class PosPaymentBoundaryPolicy
{
    public static bool IsProviderSuccess(string? state)
        => string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase);
}
