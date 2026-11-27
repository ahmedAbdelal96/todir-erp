using System.Globalization;

namespace Modules.Planning;

/// <summary>Validates the small, deterministic formula graph; tenant input is never executed as code.</summary>
public sealed class PlanningFormulaEngine
{
    /// <summary>
    /// Evaluates the constrained Planning expression language.  This parser is
    /// deliberately small: it accepts decimal literals, metric references,
    /// arithmetic, comparisons and a fixed function allow-list.  User input is
    /// never compiled or passed to a runtime interpreter.
    /// </summary>
    public static decimal Evaluate(string expression, IReadOnlyDictionary<string, decimal> values, int scale = 4)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new PlanningException("planning.formula_invalid");
        var parser = new Parser(expression, values);
        var result = parser.ParseExpression();
        if (!parser.End) throw new PlanningException("planning.formula_invalid");
        return decimal.Round(result, scale, MidpointRounding.AwayFromZero);
    }

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

    private sealed class Parser(string expression, IReadOnlyDictionary<string, decimal> values)
    {
        private readonly string expression = expression;
        private readonly IReadOnlyDictionary<string, decimal> values = values;
        private int position;

        public bool End { get { SkipWhiteSpace(); return position == expression.Length; } }

        public decimal ParseExpression() => ParseComparison();

        private decimal ParseComparison()
        {
            var left = ParseAdditive();
            while (true)
            {
                SkipWhiteSpace();
                if (Match(">=")) left = left >= ParseAdditive() ? 1m : 0m;
                else if (Match("<=")) left = left <= ParseAdditive() ? 1m : 0m;
                else if (Match("<>")) left = left != ParseAdditive() ? 1m : 0m;
                else if (Match(">")) left = left > ParseAdditive() ? 1m : 0m;
                else if (Match("<")) left = left < ParseAdditive() ? 1m : 0m;
                else if (Match("=")) left = left == ParseAdditive() ? 1m : 0m;
                else return left;
            }
        }

        private decimal ParseAdditive()
        {
            var value = ParseMultiplicative();
            while (true)
            {
                SkipWhiteSpace();
                if (Match("+")) value += ParseMultiplicative();
                else if (Match("-")) value -= ParseMultiplicative();
                else return value;
            }
        }

        private decimal ParseMultiplicative()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhiteSpace();
                if (Match("*")) value *= ParseUnary();
                else if (Match("/"))
                {
                    var divisor = ParseUnary();
                    if (divisor == 0m) throw new PlanningException("planning.formula_divide_by_zero");
                    value /= divisor;
                }
                else return value;
            }
        }

        private decimal ParseUnary()
        {
            SkipWhiteSpace();
            if (Match("+")) return ParseUnary();
            if (Match("-")) return -ParseUnary();
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            SkipWhiteSpace();
            if (Match("("))
            {
                var value = ParseExpression();
                Require(")");
                return value;
            }
            if (position < expression.Length && (char.IsDigit(expression[position]) || expression[position] == '.'))
            {
                var start = position;
                while (position < expression.Length && (char.IsDigit(expression[position]) || expression[position] == '.')) position++;
                if (!decimal.TryParse(expression[start..position], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) throw new PlanningException("planning.formula_invalid");
                return value;
            }
            var identifier = ReadIdentifier();
            if (identifier.Length == 0) throw new PlanningException("planning.formula_invalid");
            SkipWhiteSpace();
            if (Match("("))
            {
                var arguments = new List<decimal>();
                SkipWhiteSpace();
                if (!Match(")"))
                {
                    do arguments.Add(ParseExpression()); while (Match(","));
                    Require(")");
                }
                return ApplyFunction(identifier, arguments);
            }
            if (identifier.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return 1m;
            if (identifier.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return 0m;
            if (!values.TryGetValue(identifier, out var result) && !values.TryGetValue(identifier.ToUpperInvariant(), out result)) throw new PlanningException("planning.formula_input_missing");
            return result;
        }

        private static decimal ApplyFunction(string name, List<decimal> arguments) => name.ToUpperInvariant() switch
        {
            "ABS" when arguments.Count == 1 => Math.Abs(arguments[0]),
            "MIN" when arguments.Count > 0 => arguments.Min(),
            "MAX" when arguments.Count > 0 => arguments.Max(),
            "SUM" when arguments.Count > 0 => arguments.Sum(),
            "ROUND" when arguments.Count is 1 or 2 => decimal.Round(arguments[0], arguments.Count == 2 ? checked((int)arguments[1]) : 4, MidpointRounding.AwayFromZero),
            "IF" when arguments.Count == 3 => arguments[0] != 0m ? arguments[1] : arguments[2],
            _ => throw new PlanningException("planning.formula_function_invalid")
        };

        private string ReadIdentifier()
        {
            SkipWhiteSpace();
            var start = position;
            while (position < expression.Length && (char.IsLetterOrDigit(expression[position]) || expression[position] is '_' or '.')) position++;
            return expression[start..position];
        }

        private bool Match(string token)
        {
            SkipWhiteSpace();
            if (!expression.AsSpan(position).StartsWith(token, StringComparison.Ordinal)) return false;
            position += token.Length;
            return true;
        }

        private void Require(string token) { if (!Match(token)) throw new PlanningException("planning.formula_invalid"); }
        private void SkipWhiteSpace() { while (position < expression.Length && char.IsWhiteSpace(expression[position])) position++; }
    }
}
