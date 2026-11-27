using Modules.Planning;
using Xunit;

namespace PlanningTests;

public sealed class PlanningFormulaTests
{
    [Fact]
    public void RejectsFormulaCycles()
    {
        var graph = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["Revenue"] = ["Volume"],
            ["Volume"] = ["Revenue"]
        };

        var exception = Assert.Throws<PlanningException>(() => PlanningFormulaEngine.EnsureAcyclic(graph));

        Assert.Equal("planning.formula_cycle", exception.Code);
    }

    [Fact]
    public void AcceptsADeterministicDag()
    {
        var graph = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["Revenue"] = ["Volume", "Price"],
            ["Margin"] = ["Revenue", "Opex"],
            ["Volume"] = [],
            ["Price"] = [],
            ["Opex"] = []
        };

        PlanningFormulaEngine.EnsureAcyclic(graph);
    }

    [Fact]
    public void ExactAllocationConservesRoundedTotal()
    {
        var values = PlanningFormulaEngine.AllocateExact(100m, [1m, 1m, 1m]);

        Assert.Equal(100m, values.Sum());
        Assert.Equal(33.3333m, values[0]);
        Assert.Equal(33.3334m, values[2]);
    }

    [Fact]
    public void RejectsNegativeAllocationWeight()
    {
        var exception = Assert.Throws<PlanningException>(() => PlanningFormulaEngine.AllocateExact(10m, [1m, -1m]));

        Assert.Equal("planning.allocation_invalid", exception.Code);
    }

    [Fact]
    public void EvaluatesConstrainedDecimalFormulaDeterministically()
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["Revenue"] = 125.00m, ["COGS"] = 25.005m };

        var result = PlanningFormulaEngine.Evaluate("ROUND((Revenue - COGS) / Revenue * 100, 2)", values, 2);

        Assert.Equal(80m, result);
    }

    [Fact]
    public void RejectsMissingFormulaInputAndDivideByZero()
    {
        Assert.Throws<PlanningException>(() => PlanningFormulaEngine.Evaluate("Revenue + Opex", new Dictionary<string, decimal> { ["Revenue"] = 1m }));
        Assert.Throws<PlanningException>(() => PlanningFormulaEngine.Evaluate("Revenue / COGS", new Dictionary<string, decimal> { ["Revenue"] = 1m, ["COGS"] = 0m }));
    }
}
