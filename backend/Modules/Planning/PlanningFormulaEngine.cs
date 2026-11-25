namespace Modules.Planning;

/// <summary>Validates the small, deterministic formula graph; tenant input is never executed as code.</summary>
public sealed class PlanningFormulaEngine
{
    public static void EnsureAcyclic(IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependencies)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in dependencies.Keys) Visit(node);
        void Visit(string node)
        {
            if (visited.Contains(node)) return;
            if (!visiting.Add(node)) throw new PlanningException("planning.formula_cycle");
            if (dependencies.TryGetValue(node, out var edges)) foreach (var edge in edges) if (dependencies.ContainsKey(edge)) Visit(edge);
            visiting.Remove(node); visited.Add(node);
        }
    }

    public static IReadOnlyList<decimal> AllocateExact(decimal total, IReadOnlyList<decimal> weights, int scale = 4)
    {
        if (total < 0 || weights.Count == 0 || weights.Any(weight => weight < 0) || weights.Sum() <= 0) throw new PlanningException("planning.allocation_invalid");
        var result = new decimal[weights.Count];
        var remaining = decimal.Round(total, scale, MidpointRounding.AwayFromZero);
        var totalWeight = weights.Sum();
        for (var i = 0; i < weights.Count; i++)
        {
            result[i] = i == weights.Count - 1 ? remaining : decimal.Round(total * weights[i] / totalWeight, scale, MidpointRounding.AwayFromZero);
            remaining -= result[i];
        }
        return result;
    }
}
