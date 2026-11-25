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
}
