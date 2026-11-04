using System.Text.Json;

namespace Modules.Payroll.Services;

public sealed record PayrollComponentRule(string Code, string Category, decimal? FixedAmount = null, string? PercentageOf = null, decimal Percentage = 0m, int Priority = 100, int Scale = 2);
public sealed record PayrollCalculationInput(decimal BaseSalary, IReadOnlyDictionary<string, decimal> Inputs, IReadOnlyDictionary<string, decimal> Quantities);
public sealed record PayrollCalculationLine(string Code, string Category, decimal Amount, string Source, string Explanation);
public sealed record PayrollCalculationResult(IReadOnlyList<PayrollCalculationLine> Lines, decimal Gross, decimal EmployeeDeductions, decimal EmployerContributions, decimal Net, decimal EmployerCost, string Fingerprint);

/// <summary>Deterministic, bounded payroll calculation core. It accepts typed rules only and never evaluates executable text.</summary>
public sealed class PayrollCalculationEngine
{
    public static PayrollCalculationResult Calculate(PayrollCalculationInput input, IReadOnlyList<PayrollComponentRule> rules)
    {
        if (input.BaseSalary < 0m) throw new PayrollCalculationException("payroll.base_salary_invalid");
        var ordered = TopologicalOrder(rules); var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["BASE"] = input.BaseSalary };
        var lines = new List<PayrollCalculationLine> { new("BASE", "Earning", Round(input.BaseSalary, 2), "HRSnapshot", "Base salary from the frozen HR compensation context.") };
        foreach (var rule in ordered)
        {
            var amount = rule.FixedAmount ?? (rule.PercentageOf is { } reference ? Resolve(values, reference) * rule.Percentage / 100m : input.Inputs.TryGetValue(rule.Code, out var supplied) ? supplied : 0m);
            if (amount < 0m) throw new PayrollCalculationException("payroll.component_amount_invalid");
            amount = Round(amount, rule.Scale);
            values[rule.Code] = amount;
            if (amount == 0m && rule.Category == "Informational") continue;
            lines.Add(new(rule.Code, rule.Category, amount, rule.FixedAmount is not null ? "Structure" : input.Inputs.ContainsKey(rule.Code) ? "ApprovedInput" : "Formula", Explain(rule, amount)));
        }
        var gross = Round(lines.Where(x => x.Category is "Earning").Sum(x => x.Amount), 2);
        var deductions = Round(lines.Where(x => x.Category is "EmployeeDeduction" or "Recovery").Sum(x => x.Amount), 2);
        var employer = Round(lines.Where(x => x.Category == "EmployerContribution").Sum(x => x.Amount), 2);
        var net = Round(gross - deductions, 2); if (net < 0m) throw new PayrollCalculationException("payroll.negative_net_pay");
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input.BaseSalary, input.Inputs, input.Quantities, rules }))).ToLowerInvariant();
        return new(lines, gross, deductions, employer, net, Round(gross + employer, 2), fingerprint);
    }

    private static List<PayrollComponentRule> TopologicalOrder(IReadOnlyList<PayrollComponentRule> rules)
    {
        var byCode = rules.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase); var result = new List<PayrollComponentRule>(); var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(PayrollComponentRule rule) { if (visited.Contains(rule.Code)) return; if (!visiting.Add(rule.Code)) throw new PayrollCalculationException("payroll.formula_cycle"); if (rule.PercentageOf is { } reference && !string.Equals(reference, "BASE", StringComparison.OrdinalIgnoreCase)) { if (!byCode.TryGetValue(reference, out var dependency)) throw new PayrollCalculationException("payroll.formula_reference_unknown"); Visit(dependency); } visiting.Remove(rule.Code); visited.Add(rule.Code); result.Add(rule); }
        foreach (var rule in rules.OrderBy(x => x.Priority).ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)) Visit(rule); return result;
    }
    private static decimal Resolve(Dictionary<string, decimal> values, string reference) => values.TryGetValue(reference, out var value) ? value : throw new PayrollCalculationException("payroll.formula_reference_unknown");
    private static decimal Round(decimal value, int scale) => decimal.Round(value, Math.Clamp(scale, 0, 6), MidpointRounding.ToEven);
    private static string Explain(PayrollComponentRule rule, decimal amount) => rule.PercentageOf is { } reference ? $"{amount:0.00} = {rule.Percentage:0.####}% من {reference}." : rule.FixedAmount is not null ? $"قيمة ثابتة من إصدار هيكل الرواتب: {amount:0.00}." : $"قيمة مدخلة معتمدة أو قاعدة مكون: {amount:0.00}.";
}

public sealed class PayrollCalculationException(string code) : Exception(code) { public string Code { get; } = code; }
