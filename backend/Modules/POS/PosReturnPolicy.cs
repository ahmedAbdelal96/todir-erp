namespace Modules.POS;

public static class PosReturnPolicy
{
    private static readonly HashSet<string> QualifyingStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Completed"
    };

    public static bool IsQualifyingReturnState(string? state) =>
        state is not null && QualifyingStates.Contains(state);

    public static decimal AggregateReturnedQuantity(IEnumerable<(string State, decimal Quantity)> returns)
    {
        return returns
            .Where(r => IsQualifyingReturnState(r.State))
            .Sum(r => r.Quantity);
    }
}
