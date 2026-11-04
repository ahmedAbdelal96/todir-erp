using Modules.Payroll.Services;
using Xunit;

namespace PayrollTests;

public sealed class PayrollCalculationEngineTests
{
    [Fact]
    public void CalculatesDependencyOrderAndFingerprintDeterministically()
    {
        var rules = new[]
        {
            new PayrollComponentRule("HOUSING", "Earning", PercentageOf: "BASE", Percentage: 25m),
            new PayrollComponentRule("TAX", "EmployeeDeduction", PercentageOf: "BASE", Percentage: 10m),
            new PayrollComponentRule("TRANSPORT", "Earning")
        };

        var input = new PayrollCalculationInput(10000m, new Dictionary<string, decimal> { ["TRANSPORT"] = 500m }, new Dictionary<string, decimal>());
        var result = PayrollCalculationEngine.Calculate(input, rules);

        Assert.Equal(13000m, result.Gross);
        Assert.Equal(1000m, result.EmployeeDeductions);
        Assert.Equal(12000m, result.Net);
        Assert.Equal(4, result.Lines.Count);
        Assert.Equal(result.Fingerprint, PayrollCalculationEngine.Calculate(input, rules).Fingerprint);
    }

    [Fact]
    public void RejectsFormulaCyclesWithStableErrorCode()
    {
        var rules = new[]
        {
            new PayrollComponentRule("A", "Earning", PercentageOf: "B", Percentage: 10m),
            new PayrollComponentRule("B", "Earning", PercentageOf: "A", Percentage: 10m)
        };

        var exception = Assert.Throws<PayrollCalculationException>(() => PayrollCalculationEngine.Calculate(new PayrollCalculationInput(0m, new Dictionary<string, decimal>(), new Dictionary<string, decimal>()), rules));

        Assert.Equal("payroll.formula_cycle", exception.Code);
    }
}
